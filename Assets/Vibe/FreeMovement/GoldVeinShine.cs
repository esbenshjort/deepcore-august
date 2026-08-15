using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Rare, tiny specular fleck on discovered rich gold (dig-face, 3–4 sockets).
    /// Subtle detail only — not a frequent flare.
    /// </summary>
    public sealed class GoldVeinShine : MonoBehaviour
    {
        const int MinGrade = 3;
        const int MaxActive = 2;

        FineTerrainWorld _world;
        readonly List<Vector2Int> _rich = new(128);
        readonly List<Twinkle> _active = new(MaxActive);
        float _nextPick;
        Material _unlit;
        Sprite _glint;

        struct Twinkle
        {
            public GameObject Go;
            public SpriteRenderer Sr;
            public float T;
            public float Duration;
            public float BaseScale;
        }

        public static GoldVeinShine Attach(Transform parent, FineTerrainWorld world)
        {
            var go = new GameObject("GoldVeinShine");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<GoldVeinShine>();
            fx.Bind(world);
            return fx;
        }

        public void Bind(FineTerrainWorld world)
        {
            if (_world != null)
                _world.RegionChanged -= OnRegion;
            _world = world;
            if (_world != null)
                _world.RegionChanged += OnRegion;
            EnsureAssets();
            RescanAll();
            _nextPick = Random.Range(2.5f, 5f);
        }

        public void Rescan() => RescanAll();

        void OnDestroy()
        {
            if (_world != null)
                _world.RegionChanged -= OnRegion;
        }

        void OnRegion(int x0, int y0, int x1, int y1)
        {
            if (_world == null) return;
            int ax0 = Mathf.Max(0, x0 - 1);
            int ay0 = Mathf.Max(0, y0 - 1);
            int ax1 = Mathf.Min(_world.Width - 1, x1 + 1);
            int ay1 = Mathf.Min(_world.Height - 1, y1 + 1);

            for (int i = _rich.Count - 1; i >= 0; i--)
            {
                var c = _rich[i];
                if (c.x < ax0 || c.x > ax1 || c.y < ay0 || c.y > ay1) continue;
                if (!IsShineCandidate(c.x, c.y))
                    _rich.RemoveAt(i);
            }

            for (int y = ay0; y <= ay1; y++)
            for (int x = ax0; x <= ax1; x++)
            {
                if (!IsShineCandidate(x, y)) continue;
                var key = new Vector2Int(x, y);
                if (!_rich.Contains(key))
                    _rich.Add(key);
            }
        }

        void RescanAll()
        {
            _rich.Clear();
            if (_world == null) return;
            for (int y = 0; y < _world.Height; y++)
            for (int x = 0; x < _world.Width; x++)
            {
                if (IsShineCandidate(x, y))
                    _rich.Add(new Vector2Int(x, y));
            }
        }

        bool IsShineCandidate(int x, int y)
        {
            if (_world == null || !_world.InBounds(x, y)) return false;
            var c = _world.Get(x, y);
            if (c.Phase == TerrainPhase.Excavated) return false;
            if (c.GoldCount < MinGrade) return false;
            return TouchesExcavated(x, y);
        }

        bool TouchesExcavated(int x, int y)
        {
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                if (_world.IsExcavated(x + ox, y + oy)) return true;
            }
            return false;
        }

        void EnsureAssets()
        {
            if (_unlit == null)
            {
                var sh = Shader.Find("Sprites/Default");
                _unlit = sh != null ? new Material(sh) : DigVisualKit.LitMaterial;
            }
            if (_glint == null)
                _glint = MakeGlintSprite();
        }

        void Update()
        {
            if (_world == null) return;
            TickTwinkles();

            _nextPick -= Time.deltaTime;
            if (_nextPick > 0f) return;
            // Rare — a quiet wink every few seconds at most
            _nextPick = Random.Range(3.5f, 7.5f);
            if (_rich.Count == 0 || _active.Count >= MaxActive) return;
            if (Random.value > 0.55f)
            {
                // Often skip even when due — keeps it sparse
                return;
            }

            Vector2 cam = Vector2.zero;
            if (Camera.main != null && transform.parent != null)
                cam = transform.parent.InverseTransformPoint(Camera.main.transform.position);

            int best = -1;
            float bestScore = float.MaxValue;
            int tries = Mathf.Min(6, _rich.Count);
            for (int t = 0; t < tries; t++)
            {
                int i = Random.Range(0, _rich.Count);
                var cell = _rich[i];
                if (!IsShineCandidate(cell.x, cell.y))
                {
                    _rich.RemoveAt(i);
                    continue;
                }
                Vector2 p = _world.CellCenter(cell.x, cell.y);
                float score = (p - cam).sqrMagnitude * Random.Range(0.8f, 1.2f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            if (best < 0) return;
            var pick = _rich[best];
            if (!IsShineCandidate(pick.x, pick.y)) return;
            BeginTwinkle(_world.CellCenter(pick.x, pick.y), _world.Get(pick.x, pick.y).GoldCount);
        }

        void BeginTwinkle(Vector2 cellCenter, int grade)
        {
            EnsureAssets();
            float cs = _world.CellSize;
            Vector2 offset = Random.insideUnitCircle * (cs * 0.22f);
            var go = new GameObject("GoldShimmer");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = cellCenter + offset;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _glint;
            sr.sharedMaterial = _unlit;
            sr.sortingOrder = 24;
            sr.color = new Color(1f, 0.95f, 0.7f, 0f);

            float baseScale = (grade >= 4 ? 0.045f : 0.035f) * Random.Range(0.9f, 1.1f);
            go.transform.localScale = Vector3.one * baseScale;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 45f));

            _active.Add(new Twinkle
            {
                Go = go,
                Sr = sr,
                T = 0f,
                Duration = Random.Range(0.18f, 0.32f),
                BaseScale = baseScale,
            });
        }

        void TickTwinkles()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var t = _active[i];
                if (t.Go == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }

                t.T += Time.deltaTime;
                float u = Mathf.Clamp01(t.T / t.Duration);
                float flash = u < 0.4f ? u / 0.4f : 1f - (u - 0.4f) / 0.6f;
                flash = flash * flash * (3f - 2f * flash);

                if (t.Sr != null)
                {
                    var c = t.Sr.color;
                    // Soft, low alpha — a fleck, not a star
                    c.a = flash * 0.55f;
                    t.Sr.color = c;
                    t.Go.transform.localScale = Vector3.one * (t.BaseScale * (0.7f + flash * 0.35f));
                }

                _active[i] = t;
                if (u >= 1f)
                {
                    Destroy(t.Go);
                    _active.RemoveAt(i);
                }
            }
        }

        static Sprite MakeGlintSprite()
        {
            const int s = 8;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            float cx = (s - 1) * 0.5f, cy = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = Mathf.Abs(x - cx);
                float dy = Mathf.Abs(y - cy);
                float d = Mathf.Max(dx, dy); // tiny square fleck
                float a = d <= 1.2f ? 1f : (d <= 2f ? 0.35f : 0f);
                tex.SetPixel(x, y, new Color(1f, 0.95f, 0.7f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }
    }
}
