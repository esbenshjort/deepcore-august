using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Slow, deliberate hauler: scoops one loose cell at a time into an 8-slot cart,
    /// then returns the batch to basecamp. Paths on excavated cells (BFS) so tunnels don't trap it.
    /// Stage E: one cart/host; assigned <see cref="WorkerRuntime"/> supplies identity/stats.
    /// Implements <see cref="IWorkProvider"/> for Hauling (manager remains assignment authority).
    /// </summary>
    public sealed class HaulerPerson : MonoBehaviour, IWorkProvider
    {
        public const int CartSlots = 8;
        static int _nextProviderSerial = 1;

        enum State { Seek, PickUp, Return, Deposit }

        FineTerrainWorld _world;
        DeliveryCalculator _calc;
        Vector2 _basecamp;
        float _radius = 0.055f;
        float _moveSpeed = 0.68f;          // deliberate walk — heavy boots
        float _pickupRadius = 0.55f;
        int _maxCarryPiles = CartSlots;
        float _pickupDuration = 1.15f;     // heave each cell into the cart
        float _depositBaseDuration = 1.05f;
        float _depositPerPiece = 0.22f;    // unloading a full cart takes real time
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

        // Shared tunnel A* path
        ExcavatedPathfinder _nav;
        readonly List<Vector2> _path = new(128);
        int _pathI;
        Vector2 _pathGoal;
        float _repathTimer;

        LogisticsTrafficMap _traffic;
        MineInfrastructure _infra;
        Vector2Int _lastTrafficCell = new(int.MinValue, int.MinValue);
        float _gameHours;

        // Briefly skip piles we couldn't reach
        readonly Dictionary<int, float> _softIgnore = new(16);
        static readonly List<int> _tmpIgnoreKeys = new(8);

        public Vector2 Position => transform.localPosition;
        public DeliveryCalculator Calculator => _calc;
        public bool PreferGold { get; private set; }
        public int CargoCount => _cargoCount;
        public string ActivityLabel => _state switch
        {
            State.Seek => PreferGold ? "SEEK PRECIOUS" : "SEEK",
            State.PickUp => "LOADING",
            State.Return => $"HAUL {_cargoCount}/{CartSlots}",
            State.Deposit => "UNLOAD",
            _ => "IDLE",
        };

        /// <summary>Debug: current track speed multiplier under the hauler (1 if no track).</summary>
        public float DebugTrackSpeedMul =>
            _infra != null ? _infra.TrackSpeedMulAt(Position) : 1f;

        public void BindLogistics(LogisticsTrafficMap traffic, MineInfrastructure infra)
        {
            _traffic = traffic;
            _infra = infra;
        }

        public void SetGameHours(float absoluteGameHours) => _gameHours = absoluteGameHours;

        /// <summary>Body / Mind / Soul sheet. Provisional for haul formulas — unused by live haul logic yet.</summary>
        public WorkerStats Stats =>
            _assignedWorker != null ? _assignedWorker.Stats : (_stats ??= new WorkerStats());

        public WorkerRuntime AssignedWorker => _assignedWorker;

        // ——— IWorkProvider (Hauling) ———
        public string ProviderId { get; private set; } = "hauler.0";
        public JobType JobType => DeepCore.FreeMovement.JobType.Hauling;
        public int AssignedWorkerId { get; private set; } = -1;
        public bool IsAvailable => true;

        public bool CanAssign(WorkerRuntime worker, out string reason)
        {
            reason = "";
            if (worker == null)
            {
                reason = "No worker";
                return false;
            }
            return true;
        }

        public void NotifyAssigned(WorkerRuntime worker) =>
            AssignedWorkerId = worker != null ? worker.WorkerId : -1;

        public void NotifyUnassigned() => AssignedWorkerId = -1;

        [SerializeField] WorkerStats _stats = new WorkerStats();
        WorkerRuntime _assignedWorker;

        public void BindWorker(WorkerRuntime worker)
        {
            if (worker == null || worker.Stats == null) return;
            EnsureProviderId();
            _assignedWorker = worker;
            _stats = worker.Stats;
            _stats.ClampAll();
            WorkerJobDemand.EnsureStaminaPrimed(worker);
            NotifyAssigned(worker);
        }

        public void ClearWorker()
        {
            _assignedWorker = null;
            _stats = WorkerStats.CreateBaseline();
            AssignedWorkerId = -1;
        }

        /// <summary>
        /// Drop live navigation/pickup claim. Cargo, PreferGold, and haul FSM persist on the cart/host.
        /// </summary>
        public void YieldForReassignment()
        {
            if (_target != null)
            {
                _target.Claimed = false;
                _target = null;
            }
            if (_state == State.PickUp)
            {
                _state = State.Seek;
                _pickupTimer = 0f;
            }
            InvalidatePath();
            DigHoodLog.Push(
                $"ASSIGN | Hauler yield | Cargo {_cargoCount}/{CartSlots} | {ActivityLabel}");
        }

        void EnsureProviderId()
        {
            if (string.IsNullOrEmpty(ProviderId) || ProviderId == "hauler.0")
                ProviderId = $"hauler.{_nextProviderSerial++}";
        }

        public void SetPreferGold(bool on)
        {
            if (PreferGold == on) return;
            PreferGold = on;
            if (PreferGold && IsLive(_target) && !_target.IsPrecious)
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
            EnsureNav();
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

        public void SoftTeleport(Vector2 pos)
        {
            // Keep PreferGold, cargo, and FSM — only drop live pile claim and repath
            if (_target != null)
            {
                _target.Claimed = false;
                _target = null;
            }
            if (_state == State.PickUp)
                _state = State.Seek;
            _pickupTimer = 0f;
            transform.localPosition = pos;
            InvalidatePath();
        }

        public void TeleportTo(Vector2 pos) => SoftTeleport(pos);

        public void SetCrewVisible(bool on)
        {
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
                r.enabled = on;
            foreach (var l in GetComponentsInChildren<Light2D>(true))
                l.enabled = on;
        }

        void EnsureNav()
        {
            if (_world == null) return;
            if (_nav == null)
            {
                _nav = new ExcavatedPathfinder(_world, _radius);
                _nav.LateralOffset = 0.04f;
            }
            _nav.SetAgentRadius(_radius);
        }

        void InvalidatePath()
        {
            _path.Clear();
            _pathI = 0;
            _pathGoal = new Vector2(float.NaN, float.NaN);
            _repathTimer = 0f;
            _nav?.Invalidate();
        }

        public static HaulerPerson Spawn(Transform parent, FineTerrainWorld world, Vector2 basecamp,
            DeliveryCalculator calc)
        {
            var go = new GameObject("Hauler");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = basecamp;

            var facing = new GameObject("Facing");
            facing.transform.SetParent(go.transform, false);
            CrewVisualKit.AttachHauler(facing.transform, out _);
            var body = facing.transform.Find("Body");

            // Invisible load rack sits in the backpack hopper (sprite owns the crate silhouette).
            const float cartScale = 0.42f;
            float pieceWorld = world.CellSize * 0.9f;
            float slotLocal = pieceWorld / cartScale;

            var cart = new GameObject("Cart");
            cart.transform.SetParent(facing.transform, false);
            cart.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            cart.transform.localScale = Vector3.one * cartScale;

            var loadRoot = new GameObject("Load");
            loadRoot.transform.SetParent(cart.transform, false);
            loadRoot.transform.localPosition = new Vector3(0f, 0.02f, 0f);

            var h = go.AddComponent<HaulerPerson>();
            h.EnsureProviderId();
            h._facing = facing.transform;
            h._body = body != null ? body : facing.transform;
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
                ssr.sortingOrder = 44 + row;
                DigVisualKit.ApplyLit(ssr);
                slot.SetActive(false);
                h._slotSr[i] = ssr;
            }

            var light = go.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(light,
                new Color(0.75f, 0.88f, 1f),
                intensity: 0.04f,
                outer: 0.2f,
                inner: 0.02f,
                shadows: false,
                falloff: 0.9f);

            h.Setup(world, basecamp, calc);
            return h;
        }

        public event System.Action PickedGold;
        public event System.Action PickedRock;
        public event System.Action Deposited;

        public void Tick()
        {
            if (_world == null || _calc == null) return;
            // F0.5b vacancy: cart stays; no haul FSM progression
            if (_assignedWorker == null) return;
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
                SoftIgnore(_target, PreferGold && _target.IsPrecious ? 2.5f : 4f);
                _target.Claimed = false;
                _target = null;
                _stuckFrames = 0;
                _seekStuckTimer = 0f;
                InvalidatePath();
                if (AssignedWorkerId > 0)
                {
                    WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                        AssignedWorkerId,
                        WorkerStateEventType.WorkBlocked,
                        2.5f,
                        "HaulRouteStuck",
                        JobType.Hauling,
                        ProviderId));
                }
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
            float reachR = _target.IsPrecious ? _pickupRadius * 1.25f : _pickupRadius;
            if ((pilePos - Position).sqrMagnitude <= reachR * reachR)
            {
                _state = State.PickUp;
                // Rock is heavier to lift; precious still takes a solid heave
                float heave = _target.IsPrecious ? 0.95f : 1.2f;
                if (_target.Mass > 6f) heave += 0.2f;
                _pickupTimer = _pickupDuration * heave;
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
                    if (p.IsDiamond) score = d - 2_000_000f - p.DiamondGrade * 80_000f;
                    else if (p.IsGold) score = d - p.GoldGrade * 50_000f;
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
            bool gold = _target.IsPrecious;
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
                int pieces = Mathf.Max(_cargoCount, _calc != null ? _calc.CarryPiles : 0);
                _depositTimer = _depositBaseDuration + pieces * _depositPerPiece;
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
            if (AssignedWorkerId > 0)
            {
                WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                    AssignedWorkerId,
                    WorkerStateEventType.ProgressSuccess,
                    3f,
                    "HaulDelivered",
                    JobType.Hauling,
                    ProviderId));
            }
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

            int seed = i * 7919 + pile.GoldGrade * 97 + pile.DiamondGrade * 53 + (int)(pile.Mass * 10f);
            sr.sprite = DigVisualKit.MakeWallChunk(pile.GoldGrade, bedrock, seed, pile.DiamondGrade);
            if (pile.IsDiamond || pile.IsGold)
                OreShimmer.Attach(sr.transform, pile.IsDiamond, pile.GoldGrade, pile.DiamondGrade, 0.08f);
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
            EnsureNav();
            if (_nav == null) return;
            if (!_nav.TryFindPath(from, to, _path))
            {
                // Soft fail: head toward goal cell center; never through solid rock.
                if (_world != null)
                {
                    var g = _world.WorldToCell(to);
                    if (_world.IsTunnelOpen(g.x, g.y))
                        _path.Add(_world.CellCenter(g.x, g.y));
                }
            }
        }


        void StepToward(Vector2 waypoint, Vector2 ultimateGoal)
        {
            Vector2 pos = Position;
            Vector2 to = waypoint - pos;
            if (to.sqrMagnitude < 0.00001f) return;
            Vector2 dir = to.normalized;
            Face(dir);

            float step = _moveSpeed * LoosePile.SpeedMulAt(pos, _radius) * Time.deltaTime;
            if (_infra != null)
                step *= _infra.TrackSpeedMulAt(pos);
            step *= WorkerJobDemand.PhysicalMoveMul(_assignedWorker);
            // Loaded cart is a grind — fuller = slower (full cart ≈ 40% walk speed)
            if (_calc != null && _calc.CarryPiles > 0)
            {
                float load = Mathf.Clamp01(_cargoCount / (float)CartSlots);
                step *= Mathf.Lerp(0.72f, 0.40f, load);
            }
            else if (_state == State.Seek && IsLive(_target))
            {
                // Closing on a pile: settle into a careful approach
                step *= 0.82f;
            }

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
            RecordTrafficIfCellChanged();
            return true;
        }

        void RecordTrafficIfCellChanged()
        {
            if (_traffic == null || _world == null) return;
            _traffic.SetGameHours(_gameHours);
            var c = _world.WorldToCell(Position);
            if (c.x == _lastTrafficCell.x && c.y == _lastTrafficCell.y) return;
            _lastTrafficCell = c;
            bool loaded = _cargoCount > 0 || (_calc != null && _calc.CarryPiles > 0);
            _traffic.RecordVisit(c.x, c.y, loaded);
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

        void OnValidate()
        {
            _stats?.ClampAll();
        }
    }
}
