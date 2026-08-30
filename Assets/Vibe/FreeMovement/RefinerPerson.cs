using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Runs the wash machine: pulls intact ore cells from stockpiles, processes
    /// every socket visually, and splits gold / dirt into output piles.
    /// </summary>
    public sealed class RefinerPerson : MonoBehaviour
    {
        enum State { Idle, Fetch, CarryToWash, WaitWash, Consulting }

        FineTerrainWorld _world;
        BasecampYard _yard;
        DeliveryCalculator _calc;
        WashMachine _washer;
        float _moveSpeed = 1.25f;

        State _state = State.Idle;
        State _resumeAfterConsult = State.Idle;
        OreCell _held;
        RefinerPriority _heldRecipe = RefinerPriority.GoldOre;
        bool _holding;
        SpriteRenderer _heldSr;
        Transform _facing;
        float _idleTimer;
        bool _consultActive;
        bool _discussing;
        Vector2 _consultMeet;
        Vector2 _consultFaceToward;
        float _consultHoursLeft;

        public RefinerPriority Priority { get; private set; } = RefinerPriority.GoldOre;
        public Vector2 Position => transform.localPosition;
        public bool IsInConsultation => _consultActive;
        public bool IsDiscussing => _discussing;

        /// <summary>Fixed meet point by the washer — Prospector walks here instead of chasing.</summary>
        public Vector2 ConsultationMeetPoint => WorkPoint;

        /// <summary>Body / Mind / Soul sheet. Data only — unused by wash logic yet.</summary>
        public WorkerStats Stats => _stats ??= new WorkerStats();

        [SerializeField] WorkerStats _stats = new WorkerStats();

        public event System.Action StartedWash;
        public event System.Action FoundGold;
        public event System.Action FoundDiamond;
        public event System.Action BatchDone;

        public void SetPriority(RefinerPriority p) => Priority = p;
        public void TogglePriority() =>
            Priority = Priority switch
            {
                RefinerPriority.GoldOre => RefinerPriority.DiamondOre,
                RefinerPriority.DiamondOre => RefinerPriority.OreRock,
                _ => RefinerPriority.GoldOre,
            };

        public static RefinerPerson Spawn(Transform parent, FineTerrainWorld world,
            BasecampYard yard, DeliveryCalculator calc)
        {
            var go = new GameObject("Refiner");
            go.transform.SetParent(yard.Root != null ? yard.Root : parent, false);
            go.transform.localPosition = (Vector2)yard.Washer.transform.localPosition
                                        + new Vector2(-0.45f, -0.35f);

            var facing = new GameObject("Facing");
            facing.transform.SetParent(go.transform, false);
            CrewVisualKit.AttachRefiner(facing.transform, out _);

            var held = new GameObject("HeldCell");
            held.transform.SetParent(facing.transform, false);
            held.transform.localPosition = new Vector3(0.12f, 0.08f, 0f);
            var hsr = held.AddComponent<SpriteRenderer>();
            hsr.sortingOrder = 43;
            DigVisualKit.ApplyLit(hsr);
            held.SetActive(false);

            var light = go.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(light,
                new Color(1f, 0.6f, 0.35f),
                intensity: 0.04f,
                outer: 0.2f,
                inner: 0.02f,
                shadows: false,
                falloff: 0.9f);

            var r = go.AddComponent<RefinerPerson>();
            r._world = world;
            r._yard = yard;
            r._calc = calc;
            r._washer = yard.Washer;
            r._facing = facing.transform;
            r._heldSr = hsr;
            r._heldSr.transform.localScale = Vector3.one * 0.11f;

            if (r._washer != null)
            {
                r._washer.BatchComplete += r.OnBatchComplete;
                r._washer.FoundGold += () => r.FoundGold?.Invoke();
                r._washer.FoundDiamond += () => r.FoundDiamond?.Invoke();
            }
            return r;
        }

        public void ResetToBase()
        {
            EndConsultation();
            _holding = false;
            if (_heldSr != null) _heldSr.gameObject.SetActive(false);
            _held = default;
            _state = State.Idle;
            _idleTimer = 0f;
            if (_yard?.Washer != null)
                transform.localPosition = (Vector2)_yard.Washer.transform.localPosition
                                          + new Vector2(-0.45f, -0.35f);
            _washer?.ResetMachine();
        }

        /// <summary>Move only — keep wash / consult / held-cell job memory across sleep.</summary>
        public void SoftTeleport(Vector2 pos)
        {
            transform.localPosition = pos;
        }

        public void TeleportTo(Vector2 pos)
        {
            SoftTeleport(pos);
        }

        /// <summary>
        /// Prospector needs a consult: pause fetch/carry, keep held ore, walk to meet.
        /// Washer may keep spinning if already running.
        /// </summary>
        public void RequestConsultation(Vector2 meetNearProspector, float expectedHours)
        {
            if (_consultActive) return;
            _consultActive = true;
            _discussing = false;
            _consultMeet = meetNearProspector;
            _consultHoursLeft = Mathf.Max(0.2f, expectedHours);
            if (_state != State.Consulting)
            {
                _resumeAfterConsult = _state;
                _state = State.Consulting;
            }
            DigHoodLog.Push("REFINER | pausing for Prospector consult");
        }

        public void BeginDiscussion(Vector2 faceToward)
        {
            if (!_consultActive) RequestConsultation(Position, 1f);
            _discussing = true;
            _consultFaceToward = faceToward;
        }

        public void TickConsultation(float hoursDelta, Vector2 faceToward)
        {
            if (!_consultActive) return;
            _consultFaceToward = faceToward;
            if (_discussing)
                _consultHoursLeft -= hoursDelta;
        }

        public void EndConsultation()
        {
            if (!_consultActive && _state != State.Consulting) return;
            _consultActive = false;
            _discussing = false;
            if (_state == State.Consulting)
            {
                _state = _resumeAfterConsult;
                if (_state == State.Consulting) _state = State.Idle;
                // If we were WaitWash and machine finished overnight/during, recover
                if (_state == State.WaitWash && _washer != null && !_washer.IsBusy)
                {
                    _state = State.Idle;
                    _idleTimer = 0.05f;
                }
                if (_state == State.Idle)
                    _idleTimer = 0.05f;
            }
            DigHoodLog.Push("REFINER | consult done — resuming wash");
        }

        public Vector2 WorkPoint =>
            _yard?.Washer != null
                ? (Vector2)_yard.Washer.transform.localPosition + new Vector2(-0.45f, -0.35f)
                : Position;

        public void SetCrewVisible(bool on)
        {
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
                r.enabled = on;
            foreach (var l in GetComponentsInChildren<Light2D>(true))
                l.enabled = on;
        }

        public void Tick(Vector2 wasd)
        {
            if (_yard == null || _washer == null) return;

            _washer.Tick();

            // Manual drive only when not mid-wash / mid-consult
            bool driving = wasd.sqrMagnitude > 0.01f
                           && _state != State.WaitWash
                           && _state != State.Consulting;
            if (driving)
            {
                Face(wasd.normalized);
                Step(wasd.normalized);
                return;
            }

            switch (_state)
            {
                case State.Idle:
                    _idleTimer -= Time.deltaTime;
                    if (_idleTimer > 0f) break;
                    if (_washer.IsBusy) break;
                    if (TryBeginFetch())
                        _state = State.Fetch;
                    else
                        _idleTimer = 0.2f;
                    break;

                case State.Fetch:
                    TickFetch();
                    break;

                case State.CarryToWash:
                    TickCarry();
                    break;

                case State.WaitWash:
                    if (!_washer.IsBusy)
                    {
                        _state = State.Idle;
                        _idleTimer = 0.05f;
                    }
                    else
                        Face(LocalOf(_washer.transform) - Position);
                    break;

                case State.Consulting:
                    TickConsultingPresence();
                    break;
            }
        }

        void TickConsultingPresence()
        {
            // Walk to meet if still approaching; then stand and face Prospector
            Vector2 meet = _consultMeet.sqrMagnitude > 0.0001f ? _consultMeet : WorkPoint;
            Vector2 toMeet = meet - Position;
            if (!_discussing && toMeet.sqrMagnitude > 0.06f)
            {
                Face(toMeet.normalized);
                Step(toMeet.normalized);
                return;
            }

            Vector2 face = _consultFaceToward.sqrMagnitude > 0.0001f
                ? _consultFaceToward - Position
                : LocalOf(_washer.transform) - Position;
            if (face.sqrMagnitude > 0.0001f)
                Face(face.normalized);
        }

        /// <summary>Stockpiles live under Basecamp root — always compare in our parent space.</summary>
        Vector2 LocalOf(Transform t)
        {
            if (t == null) return Position;
            if (transform.parent == null) return t.position;
            return transform.parent.InverseTransformPoint(t.position);
        }

        bool TryBeginFetch()
        {
            Stockpile primary = PickPrimary();
            Stockpile secondary = PickSecondary();
            Stockpile tertiary = PickTertiary();
            if (primary != null && primary.HasCells) return true;
            if (secondary != null && secondary.HasCells) return true;
            if (tertiary != null && tertiary.HasCells) return true;
            return false;
        }

        Stockpile PickPrimary() => Priority switch
        {
            RefinerPriority.DiamondOre => _yard.Diamond,
            RefinerPriority.GoldOre => _yard.Gold,
            _ => _yard.Rock,
        };

        Stockpile PickSecondary() => Priority switch
        {
            RefinerPriority.DiamondOre => _yard.Gold,
            RefinerPriority.GoldOre => _yard.Diamond,
            _ => _yard.Gold,
        };

        Stockpile PickTertiary() => Priority switch
        {
            RefinerPriority.OreRock => _yard.Diamond,
            _ => _yard.Rock,
        };

        void TickFetch()
        {
            Stockpile primary = PickPrimary();
            Stockpile secondary = PickSecondary();
            Stockpile tertiary = PickTertiary();
            Stockpile src = primary != null && primary.HasCells ? primary
                : secondary != null && secondary.HasCells ? secondary
                : tertiary != null && tertiary.HasCells ? tertiary
                : null;

            if (src == null)
            {
                _state = State.Idle;
                return;
            }

            Vector2 dest = LocalOf(src.transform);
            Vector2 to = dest - Position;
            if (to.sqrMagnitude > 0.05f)
            {
                Face(to.normalized);
                Step(to.normalized);
                return;
            }

            if (!src.TryWithdrawCell(out _held))
            {
                _state = State.Idle;
                return;
            }

            _heldRecipe = src.Kind switch
            {
                StockpileKind.Diamond => RefinerPriority.DiamondOre,
                StockpileKind.Gold => RefinerPriority.GoldOre,
                _ => RefinerPriority.OreRock,
            };
            _holding = true;
            ShowHeld(_held);
            _state = State.CarryToWash;
        }

        void TickCarry()
        {
            Vector2 dest = LocalOf(_washer.transform) + new Vector2(0f, -0.35f);
            Vector2 to = dest - Position;
            if (to.sqrMagnitude > 0.04f)
            {
                Face(to.normalized);
                Step(to.normalized);
                return;
            }

            if (!_holding)
            {
                _state = State.Idle;
                return;
            }

            if (_washer.TryBegin(_held, _heldRecipe))
            {
                _holding = false;
                if (_heldSr != null) _heldSr.gameObject.SetActive(false);
                _state = State.WaitWash;
                StartedWash?.Invoke();
            }
            // If busy, stand and retry next frame
        }

        void OnBatchComplete(int gold, int dirt, int diamond)
        {
            _calc?.NotifyWashResult(gold, dirt, diamond);
            if (_state == State.Consulting)
                _resumeAfterConsult = State.Idle;
            else
                _state = State.Idle;
            _idleTimer = 0.08f;
            BatchDone?.Invoke();
        }

        void ShowHeld(OreCell cell)
        {
            if (_heldSr == null) return;
            int seed = cell.Sockets * 1337 + 17;
            _heldSr.sprite = DigVisualKit.MakeWallChunk((byte)cell.GoldCount, cell.BedrockCount, seed,
                (byte)cell.DiamondCount);
            _heldSr.gameObject.SetActive(true);
            if (cell.IsDiamondOre || cell.IsGoldOre)
                OreShimmer.Attach(_heldSr.transform, cell.IsDiamondOre,
                    (byte)cell.GoldCount, (byte)cell.DiamondCount, 0.11f);
        }

        void Face(Vector2 dir)
        {
            if (_facing == null || dir.sqrMagnitude < 0.0001f) return;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            _facing.localRotation = Quaternion.RotateTowards(
                _facing.localRotation,
                Quaternion.Euler(0f, 0f, ang),
                420f * Time.deltaTime);
        }

        void Step(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            float mul = LoosePile.SpeedMulAt(Position, 0.12f);
            transform.localPosition = Position + dir.normalized * (_moveSpeed * mul * Time.deltaTime);
        }

        void OnValidate()
        {
            _stats?.ClampAll();
        }
    }
}
