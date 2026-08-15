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
        enum State { Idle, Fetch, CarryToWash, WaitWash }

        FineTerrainWorld _world;
        BasecampYard _yard;
        DeliveryCalculator _calc;
        WashMachine _washer;
        float _moveSpeed = 1.25f;

        State _state = State.Idle;
        OreCell _held;
        RefinerPriority _heldRecipe = RefinerPriority.GoldOre;
        bool _holding;
        SpriteRenderer _heldSr;
        Transform _facing;
        float _idleTimer;

        public RefinerPriority Priority { get; private set; } = RefinerPriority.GoldOre;
        public Vector2 Position => transform.localPosition;

        public event System.Action StartedWash;
        public event System.Action FoundGold;
        public event System.Action BatchDone;

        public void SetPriority(RefinerPriority p) => Priority = p;
        public void TogglePriority() =>
            Priority = Priority == RefinerPriority.GoldOre
                ? RefinerPriority.OreRock
                : RefinerPriority.GoldOre;

        public static RefinerPerson Spawn(Transform parent, FineTerrainWorld world,
            BasecampYard yard, DeliveryCalculator calc)
        {
            var go = new GameObject("Refiner");
            go.transform.SetParent(yard.Root != null ? yard.Root : parent, false);
            go.transform.localPosition = (Vector2)yard.Washer.transform.localPosition
                                        + new Vector2(-0.45f, -0.35f);

            var facing = new GameObject("Facing");
            facing.transform.SetParent(go.transform, false);

            var body = new GameObject("Body");
            body.transform.SetParent(facing.transform, false);
            var sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = MakeBodySprite();
            sr.sortingOrder = 42;
            DigVisualKit.ApplyLit(sr);
            body.transform.localScale = Vector3.one * 0.36f;

            var held = new GameObject("HeldCell");
            held.transform.SetParent(facing.transform, false);
            held.transform.localPosition = new Vector3(0.12f, 0.08f, 0f);
            var hsr = held.AddComponent<SpriteRenderer>();
            hsr.sortingOrder = 43;
            DigVisualKit.ApplyLit(hsr);
            held.SetActive(false);

            var light = go.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(light,
                new Color(0.7f, 0.55f, 1f),
                intensity: 0.32f,
                outer: 0.95f,
                inner: 0.05f,
                shadows: false,
                falloff: 0.7f);

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
            }
            return r;
        }

        public void ResetToBase()
        {
            _holding = false;
            if (_heldSr != null) _heldSr.gameObject.SetActive(false);
            _state = State.Idle;
            _idleTimer = 0f;
            if (_yard?.Washer != null)
                transform.localPosition = (Vector2)_yard.Washer.transform.localPosition
                                          + new Vector2(-0.45f, -0.35f);
            _washer?.ResetMachine();
        }

        public void TeleportTo(Vector2 pos)
        {
            _holding = false;
            if (_heldSr != null) _heldSr.gameObject.SetActive(false);
            _state = State.Idle;
            _idleTimer = 0.4f;
            transform.localPosition = pos;
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

            // Manual drive only when not mid-wash — never stall the machine loop
            bool driving = wasd.sqrMagnitude > 0.01f && _state != State.WaitWash;
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
                    // Safety: if machine somehow idled without event, recover
                    if (!_washer.IsBusy)
                    {
                        _state = State.Idle;
                        _idleTimer = 0.05f;
                    }
                    else
                        Face(LocalOf(_washer.transform) - Position);
                    break;
            }
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
            // Preferred input pile first, then fallback (ORE ROCK / ORE GOLD — not outputs)
            Stockpile primary = Priority == RefinerPriority.GoldOre ? _yard.Gold : _yard.Rock;
            Stockpile secondary = Priority == RefinerPriority.GoldOre ? _yard.Rock : _yard.Gold;
            if (primary != null && primary.HasCells) return true;
            if (secondary != null && secondary.HasCells) return true;
            return false;
        }

        void TickFetch()
        {
            Stockpile primary = Priority == RefinerPriority.GoldOre ? _yard.Gold : _yard.Rock;
            Stockpile secondary = Priority == RefinerPriority.GoldOre ? _yard.Rock : _yard.Gold;
            Stockpile src = primary != null && primary.HasCells ? primary
                : secondary != null && secondary.HasCells ? secondary
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

            _heldRecipe = src.Kind == StockpileKind.Gold
                ? RefinerPriority.GoldOre
                : RefinerPriority.OreRock;
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

        void OnBatchComplete(int gold, int dirt)
        {
            _calc?.NotifyWashResult(gold, dirt);
            _state = State.Idle;
            _idleTimer = 0.08f; // immediately look for next cell
            BatchDone?.Invoke();
        }

        void ShowHeld(OreCell cell)
        {
            if (_heldSr == null) return;
            int seed = cell.Sockets * 1337 + 17;
            _heldSr.sprite = DigVisualKit.MakeWallChunk((byte)cell.GoldCount, cell.BedrockCount, seed);
            _heldSr.gameObject.SetActive(true);
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

        static Sprite MakeBodySprite()
        {
            const int s = 16;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, Color.clear);

            // coat
            for (int y = 2; y < 12; y++)
            for (int x = 4; x < 12; x++)
                tex.SetPixel(x, y, new Color(0.45f, 0.35f, 0.7f));
            // head
            for (int y = 11; y < 15; y++)
            for (int x = 5; x < 11; x++)
                tex.SetPixel(x, y, new Color(0.85f, 0.75f, 0.65f));
            // apron stripe
            for (int y = 3; y < 10; y++)
            for (int x = 7; x < 9; x++)
                tex.SetPixel(x, y, new Color(0.2f, 0.85f, 0.9f));

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.2f), s);
        }
    }
}
