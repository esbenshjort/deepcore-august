using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Slow, deliberate hauler: scoops one loose cell at a time into an 8-slot cart,
    /// then returns the batch to basecamp. Paths on excavated cells (BFS) so tunnels don't trap it.
    /// </summary>
    public sealed class HaulerPerson : MonoBehaviour
    {
        public const int CartSlots = 8;

        enum State { Seek, PickUp, Return, Deposit }

        FineTerrainWorld _world;
        DeliveryCalculator _calc;
        Vector2 _basecamp;
        float _radius = 0.055f;
        float _moveSpeed = 1.15f;
        float _pickupRadius = 0.55f;
        int _maxCarryPiles = CartSlots;
        float _pickupDuration = 0.35f;
        float _seekStuckTimer;
        float _claimRecoverTimer;

        State _state = State.Seek;
        LoosePile _target;
        float _depositTimer;
        float _pickupTimer;
        int _stuckFrames;
        int _escapeSign = 1;
        Transform _facing;
        Transform _body;
        Transform _cart;
        readonly SpriteRenderer[] _slotSr = new SpriteRenderer[CartSlots];
        int _cargoCount;
        float _slotLocalScale = 0.2f;
        float _pieceWorldSize = 0.09f;

        // Cell BFS path
        readonly List<Vector2> _path = new(128);
        int _pathI;
        Vector2 _pathGoal;
        float _repathTimer;
        readonly Queue<int> _bfsQ = new(512);
        int[] _bfsCame;
        int[] _bfsStamp;
        int _bfsGen;

        // Briefly skip piles we couldn't reach
        readonly Dictionary<int, float> _softIgnore = new(16);
        static readonly List<int> _tmpIgnoreKeys = new(8);

        public Vector2 Position => transform.localPosition;
        public DeliveryCalculator Calculator => _calc;
        public bool PreferGold { get; private set; }

        public void SetPreferGold(bool on)
        {
            if (PreferGold == on) return;
            PreferGold = on;
            if (PreferGold && IsLive(_target) && !_target.IsGold)
            {
                _target.Claimed = false;
                _target = null;
                if (_state == State.PickUp) _state = State.Seek;
                InvalidatePath();
            }
        }

        public void TogglePreferGold() => SetPreferGold(!PreferGold);

        public void Setup(FineTerrainWorld world, Vector2 basecamp, DeliveryCalculator calc,
            float radius = 0.11f)
        {
            _world = world;
            _basecamp = basecamp;
            _calc = calc;
            _radius = 0.055f;
            _pieceWorldSize = world != null ? world.CellSize * 0.9f : 0.09f;
            transform.localPosition = basecamp;
            _state = State.Seek;
            _target = null;
            _depositTimer = 0f;
            _pickupTimer = 0f;
            _stuckFrames = 0;
            InvalidatePath();
            _softIgnore.Clear();
            EnsureBfsBuffers();
            ClearCargoSlots();
        }

        public void ResetToBase()
        {
            if (_target != null)
            {
                _target.Claimed = false;
                _target = null;
            }
            _state = State.Seek;
            _depositTimer = 0f;
            _pickupTimer = 0f;
            _stuckFrames = 0;
            if (_calc != null) _calc.ClearCarry();
            transform.localPosition = _basecamp;
            InvalidatePath();
            _softIgnore.Clear();
            ClearCargoSlots();
        }

        public void TeleportTo(Vector2 pos)
        {
            if (_target != null)
            {
                _target.Claimed = false;
                _target = null;
            }
            _state = State.Seek;
            _depositTimer = 0f;
            _pickupTimer = 0f;
            if (_calc != null) _calc.ClearCarry();
            transform.localPosition = pos;
            InvalidatePath();
            ClearCargoSlots();
        }

        public void SetCrewVisible(bool on)
        {
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
                r.enabled = on;
            foreach (var l in GetComponentsInChildren<Light2D>(true))
                l.enabled = on;
        }

        void EnsureBfsBuffers()
        {
            if (_world == null) return;
            int n = _world.Width * _world.Height;
            if (_bfsCame != null && _bfsCame.Length == n) return;
            _bfsCame = new int[n];
            _bfsStamp = new int[n];
        }

        void InvalidatePath()
        {
            _path.Clear();
            _pathI = 0;
            _pathGoal = new Vector2(float.NaN, float.NaN);
            _repathTimer = 0f;
        }

        public static HaulerPerson Spawn(Transform parent, FineTerrainWorld world, Vector2 basecamp,
            DeliveryCalculator calc)
        {
            var go = new GameObject("Hauler");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = basecamp;

            var facing = new GameObject("Facing");
            facing.transform.SetParent(go.transform, false);

            var body = new GameObject("Body");
            body.transform.SetParent(facing.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            var sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = MakePersonSprite();
            sr.sortingOrder = 38;
            DigVisualKit.ApplyLit(sr);
            body.transform.localScale = Vector3.one * 0.4f;

            const float cartScale = 0.48f;
            float pieceWorld = world.CellSize * 0.9f;
            float slotLocal = pieceWorld / cartScale;

            var cart = new GameObject("Cart");
            cart.transform.SetParent(facing.transform, false);
            cart.transform.localPosition = new Vector3(0f, -0.28f, 0f);
            var csr = cart.AddComponent<SpriteRenderer>();
            csr.sprite = MakeCartSprite();
            csr.sortingOrder = 37;
            DigVisualKit.ApplyLit(csr);
            cart.transform.localScale = Vector3.one * cartScale;

            var loadRoot = new GameObject("Load");
            loadRoot.transform.SetParent(cart.transform, false);
            loadRoot.transform.localPosition = new Vector3(0f, 0.08f, 0f);

            var h = go.AddComponent<HaulerPerson>();
            h._facing = facing.transform;
            h._body = body.transform;
            h._cart = cart.transform;
            h._pieceWorldSize = pieceWorld;
            h._slotLocalScale = slotLocal;

            float pitch = slotLocal * 1.05f;
            for (int i = 0; i < CartSlots; i++)
            {
                int col = i % 4;
                int row = i / 4;
                var slot = new GameObject($"Slot{i}");
                slot.transform.SetParent(loadRoot.transform, false);
                slot.transform.localPosition = new Vector3(
                    (col - 1.5f) * pitch,
                    (row - 0.4f) * pitch,
                    0f);
                slot.transform.localScale = Vector3.one * slotLocal;
                slot.transform.localRotation = Quaternion.Euler(0f, 0f, (i % 3 - 1) * 4f);
                var ssr = slot.AddComponent<SpriteRenderer>();
                ssr.sortingOrder = 39 + row;
                DigVisualKit.ApplyLit(ssr);
                slot.SetActive(false);
                h._slotSr[i] = ssr;
            }

            var light = go.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(light,
                new Color(0.7f, 0.85f, 1f),
                intensity: 0.2f,
                outer: 0.75f,
                inner: 0.04f,
                shadows: false,
                falloff: 0.8f);

            h.Setup(world, basecamp, calc);
            return h;
        }

        public event System.Action PickedGold;
        public event System.Action PickedRock;
        public event System.Action Deposited;

        public void Tick()
        {
            if (_world == null || _calc == null) return;
            PruneIgnores();

            switch (_state)
            {
                case State.Seek: TickSeek(); break;
                case State.PickUp: TickPickUp(); break;
                case State.Return: TickReturn(); break;
                case State.Deposit: TickDeposit(); break;
            }
        }

        void PruneIgnores()
        {
            if (_softIgnore.Count == 0) return;
            float t = Time.time;
            _tmpIgnoreKeys.Clear();
            foreach (var kv in _softIgnore)
                if (kv.Value <= t) _tmpIgnoreKeys.Add(kv.Key);
            for (int i = 0; i < _tmpIgnoreKeys.Count; i++)
                _softIgnore.Remove(_tmpIgnoreKeys[i]);
        }

        bool IsIgnored(LoosePile p)
        {
            if (p == null) return true;
            int id = p.GetInstanceID();
            return _softIgnore.TryGetValue(id, out float until) && until > Time.time;
        }

        void SoftIgnore(LoosePile p, float seconds)
        {
            if (p == null) return;
            _softIgnore[p.GetInstanceID()] = Time.time + seconds;
        }

        void TickSeek()
        {
            Transform space = transform.parent;

            if (ShouldReturn())
            {
                BeginReturn();
                return;
            }

            if (IsLive(_target) && (_stuckFrames > 55 || _seekStuckTimer > 5f))
            {
                SoftIgnore(_target, PreferGold && _target.IsGold ? 2.5f : 4f);
                _target.Claimed = false;
                _target = null;
                _stuckFrames = 0;
                _seekStuckTimer = 0f;
                InvalidatePath();
            }

            if (!IsLive(_target))
            {
                _target = FindTarget(space, unclaimedOnly: true);
                if (_target == null && LoosePile.LiveCount > 0)
                {
                    _claimRecoverTimer += Time.deltaTime;
                    if (_claimRecoverTimer > 0.5f)
                    {
                        LoosePile.ClearAllClaims();
                        _claimRecoverTimer = 0f;
                        _softIgnore.Clear();
                    }
                    _target = FindTarget(space, unclaimedOnly: true);
                }
                else _claimRecoverTimer = 0f;

                if (_target != null)
                {
                    _target.Claimed = true;
                    InvalidatePath();
                }
            }

            if (!IsLive(_target))
            {
                var any = FindTarget(space, unclaimedOnly: false);
                if (any != null)
                {
                    MoveAlongPath(ApproachPoint(LoosePile.PileTerrainPos(any, space)));
                    return;
                }
                if (_cargoCount == 0 && (Position - _basecamp).sqrMagnitude > 0.05f)
                    MoveAlongPath(_basecamp);
                return;
            }

            Vector2 pilePos = LoosePile.PileTerrainPos(_target, space);
            float reachR = _target.IsGold ? _pickupRadius * 1.25f : _pickupRadius;
            if ((pilePos - Position).sqrMagnitude <= reachR * reachR)
            {
                _state = State.PickUp;
                _pickupTimer = _pickupDuration;
                _seekStuckTimer = 0f;
                InvalidatePath();
                return;
            }

            Vector2 stand = ApproachPoint(pilePos);
            float distBefore = (pilePos - Position).sqrMagnitude;
            MoveAlongPath(stand);
            float distAfter = (pilePos - Position).sqrMagnitude;
            if (distAfter < distBefore - 0.00005f)
                _seekStuckTimer = Mathf.Max(0f, _seekStuckTimer - Time.deltaTime * 2.5f);
            else
                _seekStuckTimer += Time.deltaTime;
        }

        LoosePile FindTarget(Transform space, bool unclaimedOnly)
        {
            LoosePile best = null;
            float bestScore = float.MaxValue;
            float maxSqr = 200f * 200f;
            var all = LoosePile.All;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p == null) continue;
                if (unclaimedOnly && p.Claimed) continue;
                if (IsIgnored(p)) continue;
                Vector2 pos = LoosePile.PileTerrainPos(p, space);
                float d = (pos - Position).sqrMagnitude;
                if (d > maxSqr) continue;
                float score = d;
                if (PreferGold)
                {
                    if (p.IsGold) score = d - p.GoldGrade * 50_000f;
                    else score = d + 1_000_000f;
                }
                if (score < bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>Stand in open excavated space near the pile (not inside the wall).</summary>
        Vector2 ApproachPoint(Vector2 pilePos)
        {
            float cs = _world.CellSize;
            Vector2 best = pilePos;
            float bestScore = float.MaxValue;
            bool found = false;
            for (int oy = -4; oy <= 4; oy++)
            for (int ox = -4; ox <= 4; ox++)
            {
                var cell = _world.WorldToCell(pilePos + new Vector2(ox * cs, oy * cs));
                if (!_world.InBounds(cell.x, cell.y) || !_world.IsExcavated(cell.x, cell.y)) continue;
                Vector2 c = _world.CellCenter(cell.x, cell.y);
                if (_world.CircleHitsSolid(c, _radius * 0.7f)) continue;
                float toPile = (c - pilePos).sqrMagnitude;
                float score = toPile + (c - Position).sqrMagnitude * 0.05f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = c;
                    found = true;
                }
            }
            return found ? best : pilePos;
        }

        void TickPickUp()
        {
            if (!IsLive(_target))
            {
                _target = null;
                _state = State.Seek;
                return;
            }

            _pickupTimer -= Time.deltaTime;
            if (_pickupTimer > 0f) return;

            PlaceIntoCart(_target);
            bool gold = _target.IsGold;
            _calc.AddCarry(_target);
            Destroy(_target.gameObject);
            _target = null;
            _stuckFrames = 0;
            _seekStuckTimer = 0f;
            InvalidatePath();
            if (gold) PickedGold?.Invoke();
            else PickedRock?.Invoke();

            if (ShouldReturn())
                BeginReturn();
            else
                _state = State.Seek;
        }

        void TickReturn()
        {
            if ((_basecamp - Position).sqrMagnitude < 0.12f * 0.12f)
            {
                _state = State.Deposit;
                _depositTimer = 0.45f;
                InvalidatePath();
                return;
            }
            MoveAlongPath(_basecamp);
            if (_stuckFrames > 40)
            {
                ForceNudgeTowardOpen(_basecamp);
                _stuckFrames = 0;
                InvalidatePath();
            }
        }

        void TickDeposit()
        {
            _depositTimer -= Time.deltaTime;
            if (_depositTimer > 0f) return;
            _calc.DepositCarry();
            ClearCargoSlots();
            LoosePile.ClearAllClaims();
            _softIgnore.Clear();
            Deposited?.Invoke();
            _state = State.Seek;
        }

        bool ShouldReturn()
        {
            if (_cargoCount <= 0 && (_calc == null || _calc.CarryPiles <= 0)) return false;
            if (_cargoCount >= CartSlots || (_calc != null && _calc.CarryPiles >= _maxCarryPiles))
                return true;
            return LoosePile.LiveCount == 0;
        }

        void BeginReturn()
        {
            if (IsLive(_target))
            {
                _target.Claimed = false;
                _target = null;
            }
            else _target = null;
            InvalidatePath();
            _state = State.Return;
        }

        void PlaceIntoCart(LoosePile pile)
        {
            if (pile == null || _cargoCount >= CartSlots) return;
            int i = _cargoCount++;
            var sr = _slotSr[i];
            if (sr == null) return;

            int bedrock = 0;
            for (int s = 0; s < 4; s++)
                if (((pile.Sockets >> (s * 2)) & 0b11) == (int)SocketKind.Bedrock) bedrock++;

            int seed = i * 7919 + pile.GoldGrade * 97 + (int)(pile.Mass * 10f);
            sr.sprite = DigVisualKit.MakeWallChunk(pile.GoldGrade, bedrock, seed);
            sr.color = Color.white;
            sr.gameObject.SetActive(true);
            sr.transform.localScale = Vector3.one * (_slotLocalScale * 0.75f);
        }

        void ClearCargoSlots()
        {
            _cargoCount = 0;
            for (int i = 0; i < CartSlots; i++)
            {
                if (_slotSr[i] == null) continue;
                _slotSr[i].gameObject.SetActive(false);
                _slotSr[i].sprite = null;
                _slotSr[i].transform.localScale = Vector3.one * _slotLocalScale;
            }
        }

        void LateUpdate()
        {
            for (int i = 0; i < _cargoCount; i++)
            {
                var t = _slotSr[i];
                if (t == null || !t.gameObject.activeSelf) continue;
                t.transform.localScale = Vector3.Lerp(
                    t.transform.localScale, Vector3.one * _slotLocalScale, 1f - Mathf.Exp(-10f * Time.deltaTime));
            }
        }

        void MoveAlongPath(Vector2 goal)
        {
            UnstickIfEmbedded();

            _repathTimer -= Time.deltaTime;
            bool goalMoved = (goal - _pathGoal).sqrMagnitude > 0.04f;
            if (_path.Count == 0 || _pathI >= _path.Count || goalMoved || _repathTimer <= 0f)
            {
                BuildPath(Position, goal);
                _pathGoal = goal;
                _repathTimer = 0.55f;
            }

            Vector2 waypoint = goal;
            if (_path.Count > 0 && _pathI < _path.Count)
            {
                waypoint = _path[_pathI];
                if ((waypoint - Position).sqrMagnitude < 0.035f * 0.035f)
                {
                    _pathI++;
                    if (_pathI < _path.Count) waypoint = _path[_pathI];
                    else waypoint = goal;
                }
            }

            StepToward(waypoint, goal);
        }

        void BuildPath(Vector2 from, Vector2 to)
        {
            _path.Clear();
            _pathI = 0;
            EnsureBfsBuffers();
            if (_world == null) return;

            var start = _world.WorldToCell(from);
            var goal = _world.WorldToCell(to);
            if (!_world.InBounds(start.x, start.y)) return;

            if (!_world.IsTunnelOpen(start.x, start.y))
                start = NearestExcavated(start.x, start.y);
            if (!_world.InBounds(goal.x, goal.y) || !_world.IsTunnelOpen(goal.x, goal.y))
                goal = NearestExcavated(goal.x, goal.y);
            if (!_world.InBounds(start.x, start.y) || !_world.InBounds(goal.x, goal.y)) return;

            if (start.x == goal.x && start.y == goal.y)
            {
                _path.Add(to);
                return;
            }

            int w = _world.Width;
            int startI = start.y * w + start.x;
            int goalI = goal.y * w + goal.x;
            _bfsGen++;
            if (_bfsGen == int.MaxValue)
            {
                System.Array.Clear(_bfsStamp, 0, _bfsStamp.Length);
                _bfsGen = 1;
            }

            _bfsQ.Clear();
            _bfsQ.Enqueue(startI);
            _bfsStamp[startI] = _bfsGen;
            _bfsCame[startI] = -1;
            bool found = false;
            int guard = 0;
            const int maxExpand = 12000;

            while (_bfsQ.Count > 0 && guard++ < maxExpand)
            {
                int cur = _bfsQ.Dequeue();
                if (cur == goalI)
                {
                    found = true;
                    break;
                }
                int cx = cur % w;
                int cy = cur / w;
                TryEnqueue(cx + 1, cy, cur, w);
                TryEnqueue(cx - 1, cy, cur, w);
                TryEnqueue(cx, cy + 1, cur, w);
                TryEnqueue(cx, cy - 1, cur, w);
            }

            if (!found)
            {
                _path.Add(_world.CellCenter(goal.x, goal.y));
                return;
            }

            var chain = new List<int>(64);
            for (int at = goalI; at >= 0; at = _bfsCame[at])
            {
                chain.Add(at);
                if (at == startI) break;
            }
            chain.Reverse();
            for (int i = 0; i < chain.Count; i++)
            {
                if (i != 0 && i != chain.Count - 1 && (i % 2) != 0) continue;
                int idx = chain[i];
                _path.Add(_world.CellCenter(idx % w, idx / w));
            }
            if (_path.Count == 0 || (_path[_path.Count - 1] - to).sqrMagnitude > 0.0001f)
                _path.Add(to);
        }

        void TryEnqueue(int x, int y, int from, int w)
        {
            if (!_world.InBounds(x, y) || !_world.IsTunnelOpen(x, y)) return;
            int i = y * w + x;
            if (_bfsStamp[i] == _bfsGen) return;
            _bfsStamp[i] = _bfsGen;
            _bfsCame[i] = from;
            _bfsQ.Enqueue(i);
        }

        Vector2Int NearestExcavated(int x, int y)
        {
            Vector2Int best = new(x, y);
            int bestD = int.MaxValue;
            for (int r = 0; r <= 8; r++)
            {
                for (int oy = -r; oy <= r; oy++)
                for (int ox = -r; ox <= r; ox++)
                {
                    if (Mathf.Abs(ox) != r && Mathf.Abs(oy) != r) continue;
                    int nx = x + ox, ny = y + oy;
                    if (!_world.InBounds(nx, ny) || !_world.IsTunnelOpen(nx, ny)) continue;
                    int d = Mathf.Abs(ox) + Mathf.Abs(oy);
                    if (d < bestD)
                    {
                        bestD = d;
                        best = new Vector2Int(nx, ny);
                    }
                }
                if (bestD < int.MaxValue) break;
            }
            return best;
        }

        void StepToward(Vector2 waypoint, Vector2 ultimateGoal)
        {
            Vector2 pos = Position;
            Vector2 to = waypoint - pos;
            if (to.sqrMagnitude < 0.00001f) return;
            Vector2 dir = to.normalized;
            Face(dir);

            float step = _moveSpeed * LoosePile.SpeedMulAt(pos, _radius) * Time.deltaTime;
            if (_calc.CarryPiles > 0)
                step *= Mathf.Lerp(1f, 0.75f, _cargoCount / (float)CartSlots);

            float r = _radius;
            if (_stuckFrames > 6) r = _radius * 0.55f;
            if (_stuckFrames > 18) r = _radius * 0.28f;
            if (_stuckFrames > 35) r = _world.CellSize * 0.2f;

            if (TryStep(pos, dir, step, r) ||
                TryStep(pos, new Vector2(dir.x, 0f), step, r) ||
                TryStep(pos, new Vector2(0f, dir.y), step, r))
            {
                _stuckFrames = 0;
                return;
            }

            Vector2 perp = new(-dir.y, dir.x);
            float[] blends = { 0.35f, 0.7f, 1.1f, 1.6f, 2.2f };
            for (int i = 0; i < blends.Length; i++)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 alt = (dir + perp * (s * _escapeSign * blends[i])).normalized;
                    if (TryStep(pos, alt, step, r))
                    {
                        _stuckFrames = 0;
                        _escapeSign = s;
                        return;
                    }
                }
            }

            if (TryOpenCellStep(pos, waypoint, step, r))
            {
                _stuckFrames = 0;
                return;
            }

            _stuckFrames++;
            _escapeSign = -_escapeSign;

            if (_stuckFrames > 12)
                ForceNudgeTowardOpen(ultimateGoal);
            if (_stuckFrames > 25)
                InvalidatePath();
        }

        void UnstickIfEmbedded()
        {
            Vector2 pos = Position;
            if (!_world.CircleHitsSolid(pos, _radius * 0.85f)) return;
            ForceNudgeTowardOpen(pos);
        }

        void ForceNudgeTowardOpen(Vector2 prefer)
        {
            float cs = _world.CellSize;
            Vector2 pos = Position;
            Vector2 best = pos;
            float bestScore = float.MaxValue;
            const int reach = 7;
            for (int oy = -reach; oy <= reach; oy++)
            for (int ox = -reach; ox <= reach; ox++)
            {
                Vector2 cand = pos + new Vector2(ox * cs * 0.4f, oy * cs * 0.4f);
                if (_world.CircleHitsSolid(cand, cs * 0.22f)) continue;
                float score = Vector2.Distance(cand, prefer) + Vector2.Distance(cand, pos) * 0.25f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = cand;
                }
            }

            if (bestScore < 1e20f)
                transform.localPosition = Vector2.MoveTowards(pos, best, _moveSpeed * Time.deltaTime * 1.8f);
        }

        bool TryOpenCellStep(Vector2 pos, Vector2 goal, float step, float radius)
        {
            float cs = _world.CellSize;
            Vector2 bestDir = Vector2.zero;
            float bestScore = float.MaxValue;
            float curDist = Vector2.Distance(pos, goal);

            for (int oy = -3; oy <= 3; oy++)
            for (int ox = -3; ox <= 3; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                int cx = Mathf.FloorToInt(pos.x / cs) + ox;
                int cy = Mathf.FloorToInt(pos.y / cs) + oy;
                if (!_world.InBounds(cx, cy) || !_world.IsExcavated(cx, cy)) continue;
                Vector2 center = _world.CellCenter(cx, cy);
                if (_world.CircleHitsSolid(center, radius * 0.8f)) continue;
                float dGoal = Vector2.Distance(center, goal);
                float score = dGoal + Vector2.Distance(pos, center) * 0.2f;
                if (dGoal > curDist + cs * 1.5f) score += 2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestDir = center - pos;
                }
            }

            if (bestDir.sqrMagnitude < 0.0001f) return false;
            return TryStep(pos, bestDir.normalized, step, radius);
        }

        bool TryStep(Vector2 pos, Vector2 dir, float step, float radius = -1f)
        {
            if (dir.sqrMagnitude < 0.0001f) return false;
            float r = radius > 0f ? radius : _radius;
            Vector2 tryPos = pos + dir.normalized * step;
            if (_world.CircleHitsSolid(tryPos, r)) return false;
            transform.localPosition = tryPos;
            return true;
        }

        static bool IsLive(LoosePile p) => p != null && p;

        void Face(Vector2 dir)
        {
            if (_facing == null || dir.sqrMagnitude < 0.001f) return;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            _facing.localRotation = Quaternion.RotateTowards(
                _facing.localRotation,
                Quaternion.Euler(0f, 0f, ang),
                220f * Time.deltaTime);
        }

        static Sprite MakePersonSprite()
        {
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, new Color(0, 0, 0, 0));

            void Fill(int x0, int y0, int w, int h, Color c)
            {
                for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    if (x >= 0 && y >= 0 && x < s && y < s) tex.SetPixel(x, y, c);
            }

            Fill(11, 2, 4, 8, new Color(0.15f, 0.18f, 0.28f));
            Fill(17, 2, 4, 8, new Color(0.15f, 0.18f, 0.28f));
            Fill(10, 9, 12, 12, new Color(0.25f, 0.55f, 0.72f));
            Fill(6, 11, 4, 7, new Color(0.25f, 0.55f, 0.72f));
            Fill(22, 11, 4, 7, new Color(0.25f, 0.55f, 0.72f));
            Fill(12, 21, 8, 8, new Color(0.92f, 0.75f, 0.58f));
            Fill(11, 26, 10, 5, new Color(0.95f, 0.75f, 0.2f));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.15f), s);
        }

        static Sprite MakeCartSprite()
        {
            const int s = 80;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, new Color(0, 0, 0, 0));

            void Fill(int x0, int y0, int w, int h, Color c)
            {
                for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    if (x >= 0 && y >= 0 && x < s && y < s) tex.SetPixel(x, y, c);
            }

            Fill(6, 18, 68, 34, new Color(0.38f, 0.25f, 0.14f));
            Fill(10, 21, 60, 26, new Color(0.52f, 0.36f, 0.18f));
            Fill(14, 24, 52, 20, new Color(0.28f, 0.18f, 0.1f));
            Fill(6, 50, 68, 4, new Color(0.3f, 0.2f, 0.11f));
            Fill(6, 18, 4, 36, new Color(0.3f, 0.2f, 0.11f));
            Fill(70, 18, 4, 36, new Color(0.3f, 0.2f, 0.11f));
            Fill(34, 52, 12, 16, new Color(0.34f, 0.24f, 0.13f));
            Fill(16, 4, 14, 14, new Color(0.16f, 0.14f, 0.12f));
            Fill(50, 4, 14, 14, new Color(0.16f, 0.14f, 0.12f));
            Fill(20, 8, 6, 6, new Color(0.4f, 0.38f, 0.35f));
            Fill(54, 8, 6, 6, new Color(0.4f, 0.38f, 0.35f));

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.9f), s);
        }
    }
}
