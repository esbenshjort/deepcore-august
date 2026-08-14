using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Independent of excavator: always scoops loose piles while any remain, returns batches to basecamp.
    /// </summary>
    public sealed class HaulerPerson : MonoBehaviour
    {
        enum State { Seek, PickUp, Return, Deposit }

        FineTerrainWorld _world;
        DeliveryCalculator _calc;
        Vector2 _basecamp;
        float _radius = 0.11f;
        float _moveSpeed = 1.95f;
        float _pickupRadius = 0.35f;
        float _maxCarryMass = 22f;
        int _maxCarryPiles = 6;

        State _state = State.Seek;
        LoosePile _target;
        float _depositTimer;
        float _batchIdleTimer;
        int _stuckFrames;
        int _escapeSign = 1;
        SpriteRenderer _bag;
        Transform _body;

        public Vector2 Position => transform.localPosition;
        public DeliveryCalculator Calculator => _calc;

        public void Setup(FineTerrainWorld world, Vector2 basecamp, DeliveryCalculator calc,
            float radius = 0.11f)
        {
            _world = world;
            _basecamp = basecamp;
            _calc = calc;
            _radius = radius;
            transform.localPosition = basecamp;
            _state = State.Seek;
            _target = null;
            _depositTimer = 0f;
            _batchIdleTimer = 0f;
            _stuckFrames = 0;
            UpdateBagVisual();
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
            _batchIdleTimer = 0f;
            _stuckFrames = 0;
            if (_calc != null) _calc.ClearCarry();
            transform.localPosition = _basecamp;
            UpdateBagVisual();
        }

        public static HaulerPerson Spawn(Transform parent, FineTerrainWorld world, Vector2 basecamp,
            DeliveryCalculator calc)
        {
            var go = new GameObject("Hauler");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = basecamp;

            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            var sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = MakePersonSprite();
            sr.sortingOrder = 38;
            DigVisualKit.ApplyLit(sr);
            body.transform.localScale = Vector3.one * 0.42f;

            var bag = new GameObject("Bag");
            bag.transform.SetParent(go.transform, false);
            bag.transform.localPosition = new Vector3(-0.12f, 0.02f, 0f);
            var bsr = bag.AddComponent<SpriteRenderer>();
            bsr.sprite = DigVisualKit.RockPile;
            bsr.sortingOrder = 39;
            DigVisualKit.ApplyLit(bsr);
            bag.transform.localScale = Vector3.one * 0.12f;
            bag.SetActive(false);

            var light = go.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(light,
                new Color(0.7f, 0.85f, 1f),
                intensity: 0.22f,
                outer: 0.85f,
                inner: 0.04f,
                shadows: false,
                falloff: 0.8f);

            var h = go.AddComponent<HaulerPerson>();
            h._body = body.transform;
            h._bag = bsr;
            h.Setup(world, basecamp, calc);
            return h;
        }

        /// <summary>Called every frame from the runner — independent of excavator input.</summary>
        public void Tick()
        {
            if (_world == null || _calc == null) return;

            switch (_state)
            {
                case State.Seek: TickSeek(); break;
                case State.PickUp: TickPickUp(); break;
                case State.Return: TickReturn(); break;
                case State.Deposit: TickDeposit(); break;
            }
        }

        void TickSeek()
        {
            if (ShouldReturn())
            {
                BeginReturn();
                return;
            }

            // Only pick a new target when we don't have a live one (don't drop claimed target each frame)
            if (!IsLive(_target))
            {
                _target = LoosePile.FindNearest(TerrainPos(transform), maxDist: 80f, unclaimedOnly: true);
                if (_target != null) _target.Claimed = true;
            }

            if (!IsLive(_target))
            {
                if (_calc.CarryPiles > 0)
                {
                    _batchIdleTimer += Time.deltaTime;
                    if (_batchIdleTimer > 0.25f) BeginReturn();
                }
                else
                {
                    _batchIdleTimer = 0f;
                    // Idle near base — still wander slightly so it's obvious he's alive
                    if ((Position - _basecamp).sqrMagnitude > 0.04f)
                        MoveToward(_basecamp);
                }
                return;
            }

            _batchIdleTimer = 0f;
            Vector2 pilePos = TerrainPos(_target.transform);
            if ((pilePos - Position).sqrMagnitude <= _pickupRadius * _pickupRadius)
            {
                _state = State.PickUp;
                return;
            }

            MoveToward(pilePos);
        }

        void TickPickUp()
        {
            if (!IsLive(_target))
            {
                _target = null;
                _state = State.Seek;
                return;
            }

            _calc.AddCarry(_target);
            Destroy(_target.gameObject);
            _target = null;
            UpdateBagVisual();

            if (ShouldReturn())
                BeginReturn();
            else
                _state = State.Seek;
        }

        void TickReturn()
        {
            if ((_basecamp - Position).sqrMagnitude < 0.08f * 0.08f)
            {
                _state = State.Deposit;
                _depositTimer = 0.28f;
                return;
            }
            MoveToward(_basecamp);
        }

        void TickDeposit()
        {
            _depositTimer -= Time.deltaTime;
            if (_depositTimer > 0f) return;
            _calc.DepositCarry();
            UpdateBagVisual();
            _state = State.Seek;
        }

        bool ShouldReturn()
        {
            if (_calc.CarryPiles <= 0) return false;
            if (_calc.CarryPiles >= _maxCarryPiles) return true;
            if (_calc.CarryRock >= _maxCarryMass) return true;
            // Carrying a batch and nothing left on the map
            if (LoosePile.All.Count == 0) return true;
            // Carrying and no unclaimed piles left nearby / at all
            var any = LoosePile.FindNearest(Position, maxDist: 80f, unclaimedOnly: true);
            if (any == null && !IsLive(_target)) return true;
            return false;
        }

        void BeginReturn()
        {
            if (IsLive(_target))
            {
                _target.Claimed = false;
                _target = null;
            }
            else _target = null;
            _state = State.Return;
            _batchIdleTimer = 0f;
        }

        void MoveToward(Vector2 goal)
        {
            UnstickIfEmbedded();

            Vector2 pos = Position;
            Vector2 to = goal - pos;
            if (to.sqrMagnitude < 0.0001f) return;
            Vector2 dir = to.normalized;
            Face(dir);

            float step = _moveSpeed * Time.deltaTime;
            if (_calc.CarryPiles > 0)
                step *= Mathf.Lerp(1f, 0.78f, _calc.CarryRock / _maxCarryMass);

            float r = _stuckFrames > 10 ? _radius * 0.65f : _radius;
            if (_stuckFrames > 25) r = _radius * 0.45f;

            if (TryStep(pos, dir, step, r) ||
                TryStep(pos, new Vector2(dir.x, 0f), step, r) ||
                TryStep(pos, new Vector2(0f, dir.y), step, r))
            {
                _stuckFrames = 0;
                return;
            }

            Vector2 perp = new(-dir.y, dir.x);
            float[] blends = { 0.25f, 0.5f, 0.85f, 1.2f, 1.8f };
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

            if (TryOpenCellStep(pos, goal, step, r))
            {
                _stuckFrames = 0;
                return;
            }

            _stuckFrames++;
            _escapeSign = -_escapeSign;

            if (_stuckFrames > 18)
                ForceNudgeTowardOpen(goal);
        }

        void UnstickIfEmbedded()
        {
            Vector2 pos = Position;
            if (!_world.CircleHitsSolid(pos, _radius * 0.9f)) return;
            ForceNudgeTowardOpen(pos);
        }

        void ForceNudgeTowardOpen(Vector2 prefer)
        {
            float cs = _world.CellSize;
            Vector2 pos = Position;
            Vector2 best = pos;
            float bestScore = float.MaxValue;
            const int reach = 5;
            for (int oy = -reach; oy <= reach; oy++)
            for (int ox = -reach; ox <= reach; ox++)
            {
                Vector2 cand = pos + new Vector2(ox * cs * 0.45f, oy * cs * 0.45f);
                if (_world.CircleHitsSolid(cand, _radius * 0.45f)) continue;
                float score = Vector2.Distance(cand, prefer) + Vector2.Distance(cand, pos) * 0.3f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = cand;
                }
            }

            if (bestScore < 1e20f)
            {
                transform.localPosition = Vector2.MoveTowards(pos, best, _moveSpeed * Time.deltaTime * 1.8f);
                _stuckFrames = 0;
            }
        }

        bool TryOpenCellStep(Vector2 pos, Vector2 goal, float step, float radius)
        {
            float cs = _world.CellSize;
            Vector2 bestDir = Vector2.zero;
            float bestScore = float.MaxValue;
            float curDist = Vector2.Distance(pos, goal);

            for (int oy = -2; oy <= 2; oy++)
            for (int ox = -2; ox <= 2; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                int cx = Mathf.FloorToInt(pos.x / cs) + ox;
                int cy = Mathf.FloorToInt(pos.y / cs) + oy;
                if (!_world.InBounds(cx, cy) || !_world.IsExcavated(cx, cy)) continue;
                Vector2 center = _world.CellCenter(cx, cy);
                if (_world.CircleHitsSolid(center, radius)) continue;
                float dGoal = Vector2.Distance(center, goal);
                if (dGoal >= curDist + cs * 0.2f) continue;
                float score = dGoal + Vector2.Distance(pos, center) * 0.25f;
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

        Vector2 TerrainPos(Transform t)
        {
            if (transform.parent == null) return t.position;
            return transform.parent.InverseTransformPoint(t.position);
        }

        static bool IsLive(LoosePile p) => p != null && p;

        void Face(Vector2 dir)
        {
            if (_body == null || dir.sqrMagnitude < 0.001f) return;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            _body.localRotation = Quaternion.RotateTowards(
                _body.localRotation,
                Quaternion.Euler(0f, 0f, ang),
                480f * Time.deltaTime);
        }

        void UpdateBagVisual()
        {
            if (_bag == null) return;
            bool has = _calc != null && _calc.CarryPiles > 0;
            _bag.gameObject.SetActive(has);
            if (!has) return;
            float t = Mathf.Clamp01(_calc.CarryRock / _maxCarryMass);
            _bag.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, 0.28f, t);
            _bag.color = _calc.CarryGoldPiles > 0
                ? Color.Lerp(new Color(0.9f, 0.7f, 0.25f), new Color(1f, 0.85f, 0.35f),
                    Mathf.Clamp01(_calc.CarryGoldSockets / 8f))
                : new Color(0.55f, 0.48f, 0.4f);
            _bag.sprite = _calc.CarryGoldPiles > 0 ? DigVisualKit.GoldNugget : DigVisualKit.RockPile;
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
    }
}
