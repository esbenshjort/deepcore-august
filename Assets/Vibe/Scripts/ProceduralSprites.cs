using UnityEngine;

namespace DeepCore.Vibe
{
    /// <summary>
    /// Tiny runtime sprite factory for look-dev. No art pipeline required.
    /// </summary>
    public static class ProceduralSprites
    {
        public static Sprite Floor(int size = 32)
        {
            var tex = NewTex(size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Hash(x, y);
                float shade = 0.22f + n * 0.08f;
                // Warm dusty brown floor
                tex.SetPixel(x, y, new Color(shade * 1.15f, shade * 0.75f, shade * 0.45f, 1f));
            }
            return ToSprite(tex);
        }

        public static Sprite RockFull(int size = 32)
        {
            var tex = NewTex(size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Hash(x, y);
                float shade = 0.08f + n * 0.07f;
                // Occasional crack lines
                if ((x + y * 3) % 17 == 0) shade *= 0.55f;
                tex.SetPixel(x, y, new Color(shade, shade * 1.05f, shade * 1.1f, 1f));
            }
            return ToSprite(tex);
        }

        /// <summary>
        /// Rock with a lit/soft edge toward open space. edgeMask: 1 = rock edge faces that side.
        /// Bits: N=1 E=2 S=4 W=8 (open neighbors).
        /// </summary>
        public static Sprite RockEdge(int openMask, int size = 32)
        {
            var tex = NewTex(size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Hash(x + openMask * 13, y + openMask * 7);
                float shade = 0.09f + n * 0.06f;

                float edge = 0f;
                float fx = x / (size - 1f);
                float fy = y / (size - 1f);
                if ((openMask & 1) != 0) edge = Mathf.Max(edge, Mathf.InverseLerp(0.55f, 1f, fy)); // N open
                if ((openMask & 4) != 0) edge = Mathf.Max(edge, Mathf.InverseLerp(0.55f, 1f, 1f - fy)); // S
                if ((openMask & 2) != 0) edge = Mathf.Max(edge, Mathf.InverseLerp(0.55f, 1f, fx)); // E
                if ((openMask & 8) != 0) edge = Mathf.Max(edge, Mathf.InverseLerp(0.55f, 1f, 1f - fx)); // W

                shade = Mathf.Lerp(shade, shade + 0.14f, edge);
                tex.SetPixel(x, y, new Color(shade * 1.05f, shade, shade * 0.95f, 1f));
            }
            return ToSprite(tex);
        }

        public static Sprite Excavator(int size = 96, int footprint = 3)
        {
            var tex = NewTex(size);
            Clear(tex, new Color(0, 0, 0, 0));

            // Tracks
            FillRect(tex, 6, 10, 18, size - 20, new Color(0.18f, 0.16f, 0.12f, 1f));
            FillRect(tex, size - 24, 10, 18, size - 20, new Color(0.18f, 0.16f, 0.12f, 1f));

            // Body — industrial yellow/orange
            FillRect(tex, 20, 18, size - 40, size - 40, new Color(0.85f, 0.55f, 0.12f, 1f));
            FillRect(tex, 28, 28, size - 56, size - 56, new Color(0.72f, 0.42f, 0.08f, 1f));

            // Cabin
            FillRect(tex, size / 2 - 10, size / 2 - 6, 28, 22, new Color(0.25f, 0.28f, 0.3f, 1f));

            // Drill facing north
            FillRect(tex, size / 2 - 6, size - 22, 12, 18, new Color(0.45f, 0.48f, 0.5f, 1f));
            FillRect(tex, size / 2 - 3, size - 14, 6, 12, new Color(0.7f, 0.72f, 0.75f, 1f));

            return ToSprite(tex, size / (float)footprint);
        }

        public static Sprite Worker(int size = 96)
        {
            var tex = NewTex(size);
            Clear(tex, new Color(0, 0, 0, 0));

            // Soft footprint pad so 3x3 read is clear
            FillCircle(tex, size / 2, size / 2, size / 2 - 4, new Color(0.12f, 0.14f, 0.16f, 0.35f));

            // Body
            FillRect(tex, size / 2 - 10, size / 2 - 18, 20, 28, new Color(0.35f, 0.4f, 0.45f, 1f));
            // Head
            FillCircle(tex, size / 2, size / 2 + 18, 10, new Color(0.75f, 0.62f, 0.48f, 1f));
            // Helmet lamp
            FillCircle(tex, size / 2, size / 2 + 24, 4, new Color(1f, 0.85f, 0.4f, 1f));
            // Shoulders
            FillRect(tex, size / 2 - 16, size / 2 - 2, 32, 10, new Color(0.28f, 0.32f, 0.36f, 1f));

            return ToSprite(tex, size / 3f);
        }

        public static Sprite Torch(int size = 24)
        {
            var tex = NewTex(size);
            Clear(tex, new Color(0, 0, 0, 0));
            FillRect(tex, size / 2 - 2, 2, 4, size / 2, new Color(0.25f, 0.15f, 0.08f, 1f));
            FillCircle(tex, size / 2, size / 2 + 4, 6, new Color(1f, 0.7f, 0.25f, 1f));
            FillCircle(tex, size / 2, size / 2 + 6, 3, new Color(1f, 0.95f, 0.7f, 1f));
            return ToSprite(tex);
        }

        public static Sprite SoftLightCookie(int size = 64)
        {
            var tex = NewTex(size, FilterMode.Bilinear);
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            return ToSprite(tex);
        }

        /// <summary>Organic topology set — silhouettes break the square grid read.</summary>
        public static class Topology
        {
            public static Sprite Full(int size = 64) => Build(size, RockTopology.Kind.Full);
            public static Sprite Edge(int size = 64) => Build(size, RockTopology.Kind.Edge);
            public static Sprite OuterCorner(int size = 64) => Build(size, RockTopology.Kind.OuterCorner);
            public static Sprite InnerCorner(int size = 64) => Build(size, RockTopology.Kind.InnerCorner);
            public static Sprite Tip(int size = 64) => Build(size, RockTopology.Kind.Tip);

            static Sprite Build(int size, RockTopology.Kind kind)
            {
                var tex = NewTex(size, FilterMode.Bilinear);
                Clear(tex, new Color(0, 0, 0, 0));

                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (size - 1f);
                    float v = y / (size - 1f);
                    float wave = (Mathf.Sin(u * 18f + kind.GetHashCode()) + Mathf.Sin(v * 14f + 2f)) * 0.028f
                                 + (Hash(x, y + (int)kind * 17) - 0.5f) * 0.04f;

                    float inside = Coverage(kind, u, v, wave);
                    if (inside <= 0f) continue;

                    float n = Hash(x * 3, y * 5);
                    float shade = 0.07f + n * 0.08f;
                    // Lit lip near the open edge of the silhouette
                    float lip = 1f - inside;
                    shade += lip * 0.16f;
                    if ((x + y * 2) % 19 == 0) shade *= 0.6f;

                    float a = Mathf.Clamp01(inside * 8f);
                    tex.SetPixel(x, y, new Color(shade * 1.05f, shade * 1.0f, shade * 0.95f, a));
                }

                return ToSprite(tex);
            }

            /// <summary>
            /// Canonical orientations (rotation applied later):
            /// Edge/Tip open toward SOUTH. OuterCorner rock in NW. InnerCorner open toward SE.
            /// Returns 0..1 coverage (soft edge).
            /// </summary>
            static float Coverage(RockTopology.Kind kind, float u, float v, float wave)
            {
                return kind switch
                {
                    RockTopology.Kind.Full => SoftFull(u, v, wave),
                    RockTopology.Kind.Edge => SoftEdge(u, v, wave),
                    RockTopology.Kind.OuterCorner => SoftOuter(u, v, wave),
                    RockTopology.Kind.InnerCorner => SoftInner(u, v, wave),
                    RockTopology.Kind.Tip => SoftTip(u, v, wave),
                    _ => SoftFull(u, v, wave),
                };
            }

            static float SoftFull(float u, float v, float wave)
            {
                // Nearly solid — only tiny border nibble so interiors read as mass
                float d = Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f));
                return Mathf.Clamp01((0.56f + wave * 0.3f - d) / 0.05f);
            }

            static float SoftEdge(float u, float v, float wave)
            {
                // Continuous cliff face: rock fills NORTH, open SOUTH.
                // Mild wave so adjacent edge tiles still join into one wall.
                float horizon = 0.38f + Mathf.Sin(u * Mathf.PI * 2f) * 0.04f + wave * 0.35f;
                float inside = (v - horizon) / 0.08f + 1f;
                // Keep rock side fully opaque so vertical walls don't become "teeth"
                if (v > horizon + 0.08f) inside = 1f;
                return Mathf.Clamp01(inside);
            }

            static float SoftOuter(float u, float v, float wave)
            {
                // Convex rock in NW, open toward SE — quarter-circle lip
                float dx = u + 0.02f;
                float dy = 1.02f - v;
                float r = 0.92f + wave * 0.4f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                return Mathf.Clamp01((r - d) / 0.1f);
            }

            static float SoftInner(float u, float v, float wave)
            {
                // Concave notch toward SE — rock mass with a bitten corner
                float dx = 1.02f - u;
                float dy = v + 0.02f;
                float hole = Mathf.Sqrt(dx * dx + dy * dy);
                float voidness = Mathf.Clamp01((0.62f + wave * 0.3f - hole) / 0.1f);
                return 1f - voidness;
            }

            static float SoftTip(float u, float v, float wave)
            {
                // Peninsula pointing SOUTH — only for true tips, not walls
                float cx = 0.5f;
                float cy = 0.78f;
                float dx = (u - cx) / (0.36f + wave * 0.2f);
                float dy = (v - cy) / 0.7f;
                float d = dx * dx + dy * dy;
                return Mathf.Clamp01((1f - d) / 0.2f);
            }
        }

        static Texture2D NewTex(int size, FilterMode filter = FilterMode.Point)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = filter,
                wrapMode = TextureWrapMode.Clamp,
                name = "VibeProc"
            };
            return tex;
        }

        static Sprite ToSprite(Texture2D tex, float pixelsPerUnit = -1f)
        {
            tex.Apply();
            float ppu = pixelsPerUnit > 0f ? pixelsPerUnit : tex.width;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
        }

        static void Clear(Texture2D tex, Color c)
        {
            var fill = new Color[tex.width * tex.height];
            for (int i = 0; i < fill.Length; i++) fill[i] = c;
            tex.SetPixels(fill);
        }

        static void FillRect(Texture2D tex, int x, int y, int w, int h, Color c)
        {
            for (int py = y; py < y + h; py++)
            for (int px = x; px < x + w; px++)
            {
                if (px < 0 || py < 0 || px >= tex.width || py >= tex.height) continue;
                tex.SetPixel(px, py, c);
            }
        }

        static void FillCircle(Texture2D tex, int cx, int cy, int r, Color c)
        {
            int r2 = r * r;
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || y < 0 || x >= tex.width || y >= tex.height) continue;
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy <= r2) tex.SetPixel(x, y, c);
            }
        }

        static float Hash(int x, int y)
        {
            int n = x * 374761393 + y * 668265263;
            n = (n ^ (n >> 13)) * 1274126177;
            return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
        }
    }
}
