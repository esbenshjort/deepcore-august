using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// One-shot cyber crack / ring flash on a rock cell when Weak Point succeeds.
    /// Hairline neon only — no opaque bloom.
    /// </summary>
    public static class WeakPointFx
    {
        const int SortOrder = 46;

        public static void Play(Transform parent, FineTerrainWorld world, int x, int y)
        {
            if (world == null || !world.InBounds(x, y)) return;

            Vector2 center = world.CellCenter(x, y);
            float cell = world.CellSize;

            var root = new GameObject("WeakPointBurst");
            if (parent != null)
                root.transform.SetParent(parent, false);
            root.transform.localPosition = center;

            var fx = root.AddComponent<WeakPointBurstDriver>();
            fx.Begin(cell);
        }

        static Sprite MakeRingSprite(int size, float thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            float cx = (size - 1) * 0.5f;
            float outer = size * 0.48f;
            float inner = outer - thickness;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cx;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = 0f;
                if (r <= outer && r >= inner)
                    a = 1f - Mathf.Abs((r - (inner + outer) * 0.5f) / (thickness * 0.5f));
                a = Mathf.Clamp01(a);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeCrossSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, new Color(0, 0, 0, 0));

            int mid = size / 2;
            for (int i = 2; i < size - 2; i++)
            {
                tex.SetPixel(i, mid, Color.white);
                tex.SetPixel(i, mid - 1, new Color(1, 1, 1, 0.55f));
                tex.SetPixel(mid, i, Color.white);
                tex.SetPixel(mid - 1, i, new Color(1, 1, 1, 0.55f));
            }
            // Corner brackets
            for (int i = 0; i < 4; i++)
            {
                tex.SetPixel(1 + i, 1, Color.white);
                tex.SetPixel(1, 1 + i, Color.white);
                tex.SetPixel(size - 2 - i, 1, Color.white);
                tex.SetPixel(size - 2, 1 + i, Color.white);
                tex.SetPixel(1 + i, size - 2, Color.white);
                tex.SetPixel(1, size - 2 - i, Color.white);
                tex.SetPixel(size - 2 - i, size - 2, Color.white);
                tex.SetPixel(size - 2, size - 2 - i, Color.white);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        sealed class WeakPointBurstDriver : MonoBehaviour
        {
            static Sprite _ring;
            static Sprite _cross;

            SpriteRenderer _ringA;
            SpriteRenderer _ringB;
            SpriteRenderer _mark;
            float _age;
            float _cell;
            const float Life = 0.55f;

            public void Begin(float cellSize)
            {
                _cell = cellSize;
                if (_ring == null) _ring = MakeRingSprite(64, 2.2f);
                if (_cross == null) _cross = MakeCrossSprite(32);

                _ringA = MakeSr("RingA", _ring, new Color(0.25f, 0.92f, 1f, 0.85f));
                _ringB = MakeSr("RingB", _ring, new Color(1f, 0.72f, 0.22f, 0.55f));
                _mark = MakeSr("Mark", _cross, new Color(0.35f, 1f, 0.9f, 0.95f));

                float s0 = cellSize * 0.55f;
                _ringA.transform.localScale = Vector3.one * s0;
                _ringB.transform.localScale = Vector3.one * (s0 * 0.7f);
                _mark.transform.localScale = Vector3.one * (cellSize * 0.85f);
            }

            SpriteRenderer MakeSr(string name, Sprite sprite, Color color)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = color;
                sr.sortingOrder = SortOrder;
                DigVisualKit.ApplyLit(sr);
                return sr;
            }

            void Update()
            {
                _age += Time.deltaTime;
                float u = Mathf.Clamp01(_age / Life);

                // Expanding cyan ring
                float ra = Mathf.Lerp(0.55f, 2.4f, EaseOut(u));
                _ringA.transform.localScale = Vector3.one * (_cell * ra);
                SetA(_ringA, Mathf.Lerp(0.9f, 0f, u));

                // Delayed amber ring
                float ub = Mathf.Clamp01((u - 0.12f) / 0.88f);
                float rb = Mathf.Lerp(0.45f, 2.0f, EaseOut(ub));
                _ringB.transform.localScale = Vector3.one * (_cell * rb);
                SetA(_ringB, Mathf.Lerp(0.65f, 0f, ub));

                // Center mark: quick punch then fade
                float markScale = u < 0.18f
                    ? Mathf.Lerp(0.4f, 1.15f, u / 0.18f)
                    : Mathf.Lerp(1.15f, 0.85f, (u - 0.18f) / 0.82f);
                _mark.transform.localScale = Vector3.one * (_cell * markScale);
                SetA(_mark, u < 0.35f ? 1f : Mathf.Lerp(1f, 0f, (u - 0.35f) / 0.65f));

                if (_age >= Life)
                    Destroy(gameObject);
            }

            static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

            static void SetA(SpriteRenderer sr, float a)
            {
                if (sr == null) return;
                var c = sr.color;
                c.a = Mathf.Clamp01(a);
                sr.color = c;
            }
        }
    }
}
