using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Field mechanic: waits at basecamp, runs to the excavator when it OVERHEATS,
    /// repairs the drill, then walks back. Paths on excavated tunnel cells.
    /// </summary>
    public sealed class EngineerPerson : MonoBehaviour
    {
        public const float MoveSpeed = 1.45f;
        public const float BodyRadius = 0.12f;
        public const float RepairSeconds = 2.6f;
        public const float ArriveRadius = 0.38f;

        enum State : byte { IdleAtPost, ToExcavator, Repairing, ReturnToPost }

        FineTerrainWorld _world;
        FreeWorkerController _excavator;
        ExcavatedPathfinder _nav;
        Vector2 _post;
        State _state = State.IdleAtPost;
        float _repairTimer;
        Transform _facing;
        bool _announcedDispatch;

        public Vector2 Position => transform.localPosition;
        public bool IsRepairing => _state == State.Repairing;
        public bool IsEnRoute => _state == State.ToExcavator;
        public bool IsReturning => _state == State.ReturnToPost;

        /// <summary>Body / Mind / Soul sheet. Data only — unused by repair logic yet.</summary>
        public WorkerStats Stats => _stats ??= new WorkerStats();

        [SerializeField] WorkerStats _stats = new WorkerStats();

        public static EngineerPerson Spawn(Transform parent, FineTerrainWorld world, Vector2 post)
        {
            var go = new GameObject("Engineer");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = post;

            var facing = new GameObject("Facing");
            facing.transform.SetParent(go.transform, false);

            var body = new GameObject("Body");
            body.transform.SetParent(facing.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            var sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = MakePersonSprite();
            sr.sortingOrder = 39;
            DigVisualKit.ApplyLit(sr);
            body.transform.localScale = Vector3.one * 0.4f;

            var tool = new GameObject("Wrench");
            tool.transform.SetParent(facing.transform, false);
            tool.transform.localPosition = new Vector3(0.12f, 0.02f, 0f);
            var tsr = tool.AddComponent<SpriteRenderer>();
            tsr.sprite = MakeWrenchSprite();
            tsr.sortingOrder = 40;
            DigVisualKit.ApplyLit(tsr);
            tool.transform.localScale = Vector3.one * 0.22f;

            var e = go.AddComponent<EngineerPerson>();
            e._facing = facing.transform;
            e.Setup(world, post);
            return e;
        }

        public void Setup(FineTerrainWorld world, Vector2 post)
        {
            _world = world;
            _post = post;
            _nav = new ExcavatedPathfinder(world);
            _state = State.IdleAtPost;
            _repairTimer = 0f;
            _announcedDispatch = false;
            transform.localPosition = post;
        }

        public void BindExcavator(FreeWorkerController excavator) => _excavator = excavator;

        public void SetPost(Vector2 post) => _post = post;

        public void TeleportTo(Vector2 pos)
        {
            transform.localPosition = pos;
            _nav?.Invalidate();
            if (_state != State.Repairing)
            {
                _state = State.IdleAtPost;
                _repairTimer = 0f;
                _announcedDispatch = false;
            }
        }

        public void ResetToPost()
        {
            _state = State.IdleAtPost;
            _repairTimer = 0f;
            _announcedDispatch = false;
            transform.localPosition = _post;
            _nav?.Invalidate();
        }

        public void SetCrewVisible(bool on)
        {
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
                r.enabled = on;
            foreach (var l in GetComponentsInChildren<Light2D>(true))
                l.enabled = on;
        }

        public void Tick()
        {
            if (_world == null) return;

            bool needsRepair = _excavator != null && _excavator.IsOverheated;

            switch (_state)
            {
                case State.IdleAtPost:
                    if (needsRepair)
                        BeginDispatch();
                    break;

                case State.ToExcavator:
                    if (!needsRepair)
                    {
                        _state = State.ReturnToPost;
                        _nav?.Invalidate();
                        break;
                    }
                    if (FollowTo(RepairStandPoint()))
                        BeginRepair();
                    break;

                case State.Repairing:
                    _repairTimer -= Time.deltaTime;
                    FaceDir((RepairStandPoint() - Position).normalized);
                    if (_repairTimer > 0f) break;
                    FinishRepair();
                    break;

                case State.ReturnToPost:
                    if (needsRepair)
                    {
                        BeginDispatch();
                        break;
                    }
                    if (FollowTo(_post))
                    {
                        _state = State.IdleAtPost;
                        _announcedDispatch = false;
                        DigHoodLog.Push("REPAIR | Engineer back at post");
                    }
                    break;
            }
        }

        void BeginDispatch()
        {
            _state = State.ToExcavator;
            _nav?.Invalidate();
            if (!_announcedDispatch)
            {
                _announcedDispatch = true;
                DigHoodLog.Push("REPAIR | Engineer dispatched — excavator OVERHEATED");
            }
        }

        void BeginRepair()
        {
            _state = State.Repairing;
            _repairTimer = RepairSeconds;
            DigHoodLog.Push("REPAIR | Engineer on site — fixing drill…");
        }

        void FinishRepair()
        {
            if (_excavator != null && _excavator.IsOverheated)
                _excavator.ClearOverheatByEngineer();
            _state = State.ReturnToPost;
            _nav?.Invalidate();
            DigHoodLog.Push("REPAIR | Fix complete — excavator online");
        }

        Vector2 RepairStandPoint()
        {
            if (_excavator == null) return _post;
            Vector2 dig = _excavator.Position;
            Vector2 delta = Position - dig;
            if (delta.sqrMagnitude < 0.0001f)
                delta = Vector2.down;
            return dig + delta.normalized * ArriveRadius;
        }

        bool FollowTo(Vector2 goal)
        {
            Vector2 from = Position;
            if ((goal - from).sqrMagnitude <= ArriveRadius * ArriveRadius * 0.55f)
            {
                _nav?.Invalidate();
                return true;
            }

            return _nav.Follow(
                from,
                goal,
                MoveSpeed,
                BodyRadius,
                face: FaceDir,
                tryStep: TryStep);
        }

        bool TryStep(Vector2 dir, float step)
        {
            if (dir.sqrMagnitude < 0.00001f) return false;
            Vector2 next = Position + dir.normalized * step;
            if (_world.CircleHitsSolid(next, BodyRadius * 0.85f)) return false;
            transform.localPosition = next;
            return true;
        }

        void FaceDir(Vector2 dir)
        {
            if (_facing == null || dir.sqrMagnitude < 0.0001f) return;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            _facing.localRotation = Quaternion.RotateTowards(
                _facing.localRotation,
                Quaternion.Euler(0f, 0f, ang),
                260f * Time.deltaTime);
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

            // Orange overalls + hard hat — reads as mechanic
            Fill(11, 2, 4, 8, new Color(0.18f, 0.16f, 0.14f));
            Fill(17, 2, 4, 8, new Color(0.18f, 0.16f, 0.14f));
            Fill(10, 9, 12, 12, new Color(0.85f, 0.45f, 0.18f));
            Fill(6, 11, 4, 7, new Color(0.85f, 0.45f, 0.18f));
            Fill(22, 11, 4, 7, new Color(0.85f, 0.45f, 0.18f));
            Fill(12, 21, 8, 8, new Color(0.92f, 0.75f, 0.58f));
            Fill(11, 26, 10, 5, new Color(0.95f, 0.78f, 0.2f));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.15f), s);
        }

        static Sprite MakeWrenchSprite()
        {
            const int s = 16;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, new Color(0, 0, 0, 0));
            Color metal = new(0.72f, 0.78f, 0.85f);
            for (int i = 2; i < 14; i++)
            {
                tex.SetPixel(i, i, metal);
                tex.SetPixel(i, Mathf.Min(s - 1, i + 1), metal);
                tex.SetPixel(7, i, metal);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        void OnValidate() => _stats?.ClampAll();
    }
}
