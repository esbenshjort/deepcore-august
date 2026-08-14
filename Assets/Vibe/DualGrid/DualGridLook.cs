using UnityEngine;

namespace DeepCore.DualGrid
{
    /// <summary>Procedural dark-and-cozy sprites for DualGrid test.</summary>
    public static class DualGridLook
    {
        public static Sprite MakePixel()
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                tex.SetPixel(x, y, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
        }

        public static Sprite MakeFloor(int size = 32)
        {
            var tex = NoiseTex(size, seed: 17);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = tex.GetPixel(x, y).r;
                // warm brown excavated floor
                var c = Color.Lerp(
                    new Color(0.28f, 0.17f, 0.1f),
                    new Color(0.52f, 0.34f, 0.18f),
                    n);
                // sparse pebbles
                if (Hash(x, y, 91) > 0.93f)
                    c = Color.Lerp(c, new Color(0.18f, 0.14f, 0.11f), 0.55f);
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        public static Sprite MakeRock(int size = 32, float damage = 0f)
        {
            var tex = NoiseTex(size, seed: 3 + (int)(damage * 40));
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = tex.GetPixel(x, y).r;
                var dark = new Color(0.07f, 0.075f, 0.09f);
                var mid = new Color(0.16f, 0.17f, 0.2f);
                var lit = new Color(0.26f, 0.25f, 0.28f);
                var c = Color.Lerp(dark, Color.Lerp(mid, lit, n), 0.55f + n * 0.45f);
                // damage shows warmer cracked earth peeking through
                if (damage > 0f)
                {
                    float crack = Mathf.PerlinNoise(x * 0.35f + damage * 3f, y * 0.35f);
                    if (crack > 0.62f - damage * 0.2f)
                        c = Color.Lerp(c, new Color(0.35f, 0.24f, 0.14f), damage * 0.7f);
                }
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        public static Sprite MakeExcavator(int size = 48)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Clear(tex);

            // body
            FillRect(tex, 10, 12, 28, 24, new Color(0.86f, 0.62f, 0.12f));
            FillRect(tex, 12, 14, 24, 14, new Color(0.72f, 0.5f, 0.08f));
            // cabin
            FillRect(tex, 16, 22, 12, 10, new Color(0.35f, 0.42f, 0.48f));
            FillRect(tex, 18, 24, 8, 6, new Color(0.55f, 0.75f, 0.85f, 0.85f));
            // treads
            FillRect(tex, 8, 8, 32, 6, new Color(0.18f, 0.18f, 0.2f));
            FillRect(tex, 8, 34, 32, 6, new Color(0.18f, 0.18f, 0.2f));
            // drill / nose (faces +Y / up)
            FillRect(tex, 20, 36, 8, 8, new Color(0.55f, 0.55f, 0.58f));
            FillRect(tex, 22, 42, 4, 4, new Color(0.75f, 0.75f, 0.78f));

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        public static Sprite MakeWorker(int size = 24)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Clear(tex);
            FillCircle(tex, size * 0.5f, size * 0.5f, size * 0.38f, new Color(0.35f, 0.78f, 0.9f));
            FillCircle(tex, size * 0.5f, size * 0.55f, size * 0.18f, new Color(0.15f, 0.2f, 0.25f));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        public static Sprite MakeGoalMarker(int size = 32)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Clear(tex);
            // green arrow pointing up
            for (int y = 4; y < size - 4; y++)
            for (int x = size / 2 - 2; x <= size / 2 + 2; x++)
                tex.SetPixel(x, y, new Color(0.35f, 0.95f, 0.4f, 0.95f));
            for (int i = 0; i < 10; i++)
            for (int x = size / 2 - i; x <= size / 2 + i; x++)
            {
                int y = size - 6 - i;
                if (x >= 0 && x < size && y >= 0 && y < size)
                    tex.SetPixel(x, y, new Color(0.35f, 0.95f, 0.4f, 0.95f));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Texture2D NoiseTex(int size, int seed)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise((x + seed * 13) * 0.18f, (y + seed * 7) * 0.18f);
                n = Mathf.Clamp01(n * 0.7f + Hash(x, y, seed) * 0.3f);
                tex.SetPixel(x, y, new Color(n, n, n, 1f));
            }
            return tex;
        }

        static void Clear(Texture2D tex)
        {
            var clear = new Color(0, 0, 0, 0);
            for (int y = 0; y < tex.height; y++)
            for (int x = 0; x < tex.width; x++)
                tex.SetPixel(x, y, clear);
        }

        static void FillRect(Texture2D tex, int x0, int y0, int w, int h, Color c)
        {
            for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                if (x < 0 || y < 0 || x >= tex.width || y >= tex.height) continue;
                tex.SetPixel(x, y, c);
            }
        }

        static void FillCircle(Texture2D tex, float cx, float cy, float r, Color c)
        {
            float r2 = r * r;
            for (int y = 0; y < tex.height; y++)
            for (int x = 0; x < tex.width; x++)
            {
                float dx = x + 0.5f - cx;
                float dy = y + 0.5f - cy;
                if (dx * dx + dy * dy <= r2) tex.SetPixel(x, y, c);
            }
        }

        static float Hash(int x, int y, int seed)
        {
            uint n = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
            n = (n ^ (n >> 13)) * 1274126177u;
            return (n & 0xFFFF) / 65535f;
        }
    }
}
