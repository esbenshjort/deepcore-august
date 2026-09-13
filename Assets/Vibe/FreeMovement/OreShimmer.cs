using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Persistent crystalline shimmer on discovered precious ore (loose piles, washer sockets).
    /// Small realistic glints — cool white/cyan for diamonds, warm for rich gold.
    /// </summary>
    public sealed class OreShimmer : MonoBehaviour
    {
        struct Glint
        {
            public Transform Tr;
            public SpriteRenderer Sr;
            public float Phase;
            public float Speed;
            public float BaseScale;
            public Vector2 Local;
        }

        Glint[] _glints;
        bool _diamond;
        Color _tint;
        static Sprite _spark;
        static Material _unlit;

        public static OreShimmer Attach(Transform parent, bool diamond, byte goldGrade,
            byte diamondGrade, float cellSize)
        {
            var existing = parent.GetComponent<OreShimmer>();
            if (existing != null)
            {
                existing.Configure(diamond, goldGrade, diamondGrade, cellSize);
                return existing;
            }
            var fx = parent.gameObject.AddComponent<OreShimmer>();
            fx.Configure(diamond, goldGrade, diamondGrade, cellSize);
            return fx;
        }

        public void Configure(bool diamond, byte goldGrade, byte diamondGrade, float cellSize)
        {
            _diamond = diamond || diamondGrade > 0;
            int n = _diamond
                ? Mathf.Clamp(2 + diamondGrade, 2, 5)
                : Mathf.Clamp(2 + goldGrade, 2, 5);
            _tint = _diamond
                ? new Color(0.85f, 0.95f, 1f, 1f)
                : new Color(1f, 0.92f, 0.55f, 1f);

            EnsureAssets();
            ClearGlints();
            _glints = new Glint[n];
            float r = Mathf.Max(0.02f, cellSize * 0.32f);
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject($"Glint{i}");
                go.transform.SetParent(transform, false);
                Vector2 loc = Random.insideUnitCircle * r;
                go.transform.localPosition = loc;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _spark;
                sr.sharedMaterial = _unlit;
                sr.sortingOrder = 34;
                float bs = (_diamond ? 0.028f : 0.026f) * Random.Range(0.85f, 1.2f)
                           * Mathf.Max(cellSize / 0.1f, 0.6f);
                go.transform.localScale = Vector3.one * bs;
                var c = _tint;
                c.a = 0f;
                sr.color = c;
                _glints[i] = new Glint
                {
                    Tr = go.transform,
                    Sr = sr,
                    Phase = Random.Range(0f, Mathf.PI * 2f),
                    Speed = Random.Range(2.2f, 4.8f) * (_diamond ? 1.15f : 1.05f),
                    BaseScale = bs,
                    Local = loc,
                };
            }
        }

        void ClearGlints()
        {
            if (_glints == null) return;
            for (int i = 0; i < _glints.Length; i++)
            {
                if (_glints[i].Tr != null)
                    Destroy(_glints[i].Tr.gameObject);
            }
            _glints = null;
        }

        void OnDestroy() => ClearGlints();

        void Update()
        {
            if (_glints == null) return;
            float t = Time.time;
            for (int i = 0; i < _glints.Length; i++)
            {
                ref var g = ref _glints[i];
                if (g.Sr == null) continue;
                // Sharp specular pulse — brief flash, long dark (like real crystal facets)
                float wave = Mathf.Sin(t * g.Speed + g.Phase);
                float flash = Mathf.Pow(Mathf.Max(0f, wave), _diamond ? 8f : 7f);
                // Occasional secondary catch-light
                float secondary = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * g.Speed * 0.37f + g.Phase * 1.7f)), 12f);
                float a = flash * (_diamond ? 0.85f : 0.8f) + secondary * 0.35f;
                var c = _tint;
                if (_diamond && secondary > 0.4f)
                    c = Color.Lerp(c, new Color(1f, 0.75f, 0.95f, 1f), 0.35f);
                else if (!_diamond && secondary > 0.4f)
                    c = Color.Lerp(c, new Color(1f, 0.98f, 0.75f, 1f), 0.4f);
                c.a = a;
                g.Sr.color = c;
                float s = g.BaseScale * (0.7f + flash * 0.55f + secondary * 0.25f);
                g.Tr.localScale = Vector3.one * s;
                g.Tr.localRotation = Quaternion.Euler(0f, 0f, (t * 25f + g.Phase * 40f) % 360f);
                // Micro drift along crystal face
                g.Tr.localPosition = g.Local + new Vector2(
                    Mathf.Sin(t * 0.7f + g.Phase) * 0.004f,
                    Mathf.Cos(t * 0.9f + g.Phase) * 0.004f);
            }
        }

        static void EnsureAssets()
        {
            if (_spark != null) return;
            const int s = 10;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            float cx = (s - 1) * 0.5f, cy = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = x - cx, dy = y - cy;
                // Cross sparkle (diamond facet catch)
                float arm = Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dy));
                float along = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                float a = 0f;
                if (along < 4.2f && arm < 0.85f)
                    a = 1f - along / 4.2f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < 1.2f) a = Mathf.Max(a, 1f - d / 1.2f);
                a = Mathf.Clamp01(a);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _spark = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
            var sh = Shader.Find("Sprites/Default");
            _unlit = sh != null ? new Material(sh) : null;
        }
    }
}
