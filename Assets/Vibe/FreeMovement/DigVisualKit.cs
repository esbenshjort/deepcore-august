using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>Shared lit materials + nicer dig/gold sprites for cozy mining look.</summary>
    public static class DigVisualKit
    {
        static Material _lit;
        static Sprite _rockPile;
        static Sprite _goldNugget;
        static Sprite _diamondCrystal;
        static Sprite _lanternBody;
        static Sprite _pixel;

        public static Material LitMaterial
        {
            get
            {
                if (_lit != null) return _lit;
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (shader == null)
                {
                    Debug.LogWarning("[DigVisualKit] Sprite-Lit-Default missing — darkness will not work. Check URP 2D.");
                    shader = Shader.Find("Sprites/Default");
                }
                _lit = new Material(shader);
                return _lit;
            }
        }

        public static void ApplyLit(SpriteRenderer sr)
        {
            if (sr == null) return;
            sr.sharedMaterial = LitMaterial;
        }

        static Material _unlit;
        public static void ApplyUnlit(SpriteRenderer sr)
        {
            if (sr == null) return;
            if (_unlit == null)
            {
                var sh = Shader.Find("Sprites/Default");
                if (sh != null) _unlit = new Material(sh);
            }
            if (_unlit != null) sr.sharedMaterial = _unlit;
        }

        public static Sprite Pixel
        {
            get
            {
                if (_pixel != null) return _pixel;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    tex.SetPixel(x, y, Color.white);
                tex.Apply();
                _pixel = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                return _pixel;
            }
        }

        public static Sprite RockPile
        {
            get
            {
                if (_rockPile != null) return _rockPile;
                _rockPile = MakeBlobSprite(48, rock: true);
                return _rockPile;
            }
        }

        public static Sprite GoldNugget
        {
            get
            {
                if (_goldNugget != null) return _goldNugget;
                _goldNugget = MakeBlobSprite(48, rock: false);
                return _goldNugget;
            }
        }

        public static Sprite DiamondCrystal
        {
            get
            {
                if (_diamondCrystal != null) return _diamondCrystal;
                _diamondCrystal = MakeDiamondSprite(40);
                return _diamondCrystal;
            }
        }

        /// <summary>
        /// Loose excavated chunk — chunky angular rubble matching the wall-border reference
        /// (warm brown, dark outline, lit facets). Gold / diamond flecks overlay when present.
        /// </summary>
        public static Sprite MakeWallChunk(byte goldGrade, int bedrockCount, int seed,
            byte diamondGrade = 0)
        {
            const int s = 28;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Clear(tex, s);

            // Reference rubble palette — same family as FreeMovementTerrainView floor
            Color outline = new(0.05f, 0.03f, 0.02f, 1f);
            Color shade = new(0.14f, 0.09f, 0.05f, 1f);
            Color mid = new(0.30f, 0.19f, 0.10f, 1f);
            Color body = new(0.38f, 0.24f, 0.13f, 1f);
            Color warm = new(0.48f, 0.32f, 0.16f, 1f);
            Color lit = new(0.62f, 0.42f, 0.22f, 1f);
            Color hi = new(0.78f, 0.55f, 0.30f, 1f);

            if (bedrockCount >= 2)
            {
                mid = Color.Lerp(mid, new Color(0.12f, 0.12f, 0.13f), 0.35f);
                body = Color.Lerp(body, new Color(0.16f, 0.16f, 0.17f), 0.35f);
                warm = Color.Lerp(warm, new Color(0.22f, 0.22f, 0.24f), 0.3f);
            }

            Color goldC = new(220 / 255f, 160 / 255f, 42 / 255f);
            Color goldBright = new(255 / 255f, 210 / 255f, 70 / 255f);
            Color goldSpeck = new(255 / 255f, 235 / 255f, 140 / 255f);
            Color diaDeep = new(40 / 255f, 90 / 255f, 140 / 255f);
            Color diaMid = new(120 / 255f, 190 / 255f, 230 / 255f);
            Color diaHi = new(210 / 255f, 240 / 255f, 255 / 255f);
            Color diaFlash = new(255 / 255f, 250 / 255f, 255 / 255f);
            Color diaPink = new(220 / 255f, 170 / 255f, 210 / 255f);

            float cx = (s - 1) * 0.5f, cy = (s - 1) * 0.5f;
            float ox = (seed % 97) * 0.19f;
            float oy = (seed % 53) * 0.27f;
            Vector2 light = new(-0.55f, 0.75f);

            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - cx) / (s * 0.42f);
                float dy = (y - cy) / (s * 0.42f);
                float n = Mathf.PerlinNoise(x * 0.22f + ox, y * 0.22f + oy);
                float n2 = Mathf.PerlinNoise(x * 0.48f + ox + 4f, y * 0.48f + oy);
                float n3 = Mathf.PerlinNoise(x * 0.9f + oy, y * 0.9f + ox);

                float d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                d = Mathf.Lerp(d, Mathf.Sqrt(dx * dx + dy * dy), 0.35f);
                d += (n - 0.5f) * 0.28f;
                d += (n2 - 0.5f) * 0.18f;
                d += Mathf.Abs(Mathf.Sin(dx * 6.2f + n3 * 4f)) * 0.06f;
                d += Mathf.Abs(Mathf.Cos(dy * 5.5f - n * 3f)) * 0.05f;
                if (d > 1.02f) continue;

                float edge = Mathf.Clamp01((1.02f - d) * 3.4f);
                bool isOutline = edge < 0.38f;

                Vector2 grad = new(
                    Mathf.PerlinNoise(x * 0.35f + ox + 1f, y * 0.35f + oy) - 0.5f,
                    Mathf.PerlinNoise(x * 0.35f + ox, y * 0.35f + oy + 2f) - 0.5f);
                grad += new Vector2(-dx, -dy) * 0.35f;
                if (grad.sqrMagnitude > 0.0001f) grad.Normalize();
                float ndot = Vector2.Dot(grad, light);

                Color rock;
                if (isOutline)
                    rock = outline;
                else if (ndot > 0.35f)
                    rock = Color.Lerp(warm, lit, Mathf.Clamp01((ndot - 0.35f) / 0.45f));
                else if (ndot > 0.05f)
                    rock = Color.Lerp(body, warm, (ndot - 0.05f) / 0.3f);
                else if (ndot > -0.25f)
                    rock = Color.Lerp(shade, mid, (ndot + 0.25f) / 0.3f);
                else
                    rock = Color.Lerp(outline, shade, 0.45f);

                if (!isOutline && Hash(x * 3 + seed, y * 7) > 0.82f)
                    rock = Color.Lerp(rock, shade, 0.45f);
                if (!isOutline && ndot > 0.5f && Hash(x * 11 + seed, y * 5) > 0.88f)
                    rock = Color.Lerp(rock, hi, 0.4f);

                int goldN = goldGrade;
                if (goldN > 0 && !isOutline)
                {
                    float t = goldN / 4f;
                    float g = 0.12f + t * 0.88f;
                    rock = Color.Lerp(rock, goldC, g * 0.85f);
                    if (Hash(x * 5 + seed, y * 11) > 0.72f - t * 0.35f)
                        rock = Color.Lerp(rock, goldBright, 0.25f + t * 0.7f);
                    if (goldN >= 3 && Hash(x * 9 + seed, y * 3) > 0.78f)
                        rock = Color.Lerp(rock, goldSpeck, 0.45f);
                    if (goldN >= 4 && Hash(x * 13 + seed, y * 17) > 0.9f)
                        rock = Color.Lerp(rock, goldSpeck, 0.5f);
                }

                int diaN = diamondGrade;
                if (diaN > 0 && !isOutline)
                {
                    float t = diaN / 4f;
                    rock = Color.Lerp(rock, diaDeep, 0.18f + t * 0.55f);
                    float facet = Hash(x * 17 + seed, y * 23);
                    if (facet > 0.78f - t * 0.4f)
                        rock = Color.Lerp(rock, diaMid, 0.45f + t * 0.4f);
                    if (facet > 0.9f - t * 0.25f)
                        rock = Color.Lerp(rock, diaHi, 0.55f + t * 0.35f);
                    if (diaN >= 2 && Hash(x * 29 + seed, y * 7) > 0.88f)
                        rock = Color.Lerp(rock, diaFlash, 0.65f);
                    if (diaN >= 3 && Hash(x * 41 + seed, y * 13) > 0.86f)
                        rock = Color.Lerp(rock, diaPink, 0.4f);
                }

                rock.a = 1f;
                tex.SetPixel(x, y, rock);
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        /// <summary>
        /// Floor rubble — irregular multi-lobe chunk so piles don't read as dug wall tiles.
        /// Same warm industrial palette as <see cref="MakeWallChunk"/>.
        /// </summary>
        public static Sprite MakeLooseRockChunk(byte goldGrade, int bedrockCount, int seed,
            byte diamondGrade = 0)
        {
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Clear(tex, s);

            Color outline = new(0.05f, 0.03f, 0.02f, 1f);
            Color shade = new(0.14f, 0.09f, 0.05f, 1f);
            Color mid = new(0.30f, 0.19f, 0.10f, 1f);
            Color body = new(0.38f, 0.24f, 0.13f, 1f);
            Color warm = new(0.48f, 0.32f, 0.16f, 1f);
            Color lit = new(0.62f, 0.42f, 0.22f, 1f);
            Color hi = new(0.78f, 0.55f, 0.30f, 1f);

            if (bedrockCount >= 2)
            {
                mid = Color.Lerp(mid, new Color(0.12f, 0.12f, 0.13f), 0.35f);
                body = Color.Lerp(body, new Color(0.16f, 0.16f, 0.17f), 0.35f);
                warm = Color.Lerp(warm, new Color(0.22f, 0.22f, 0.24f), 0.3f);
            }

            Color goldC = new(220 / 255f, 160 / 255f, 42 / 255f);
            Color goldBright = new(255 / 255f, 210 / 255f, 70 / 255f);
            Color goldSpeck = new(255 / 255f, 235 / 255f, 140 / 255f);
            Color diaDeep = new(40 / 255f, 90 / 255f, 140 / 255f);
            Color diaMid = new(120 / 255f, 190 / 255f, 230 / 255f);
            Color diaHi = new(210 / 255f, 240 / 255f, 255 / 255f);
            Color diaFlash = new(255 / 255f, 250 / 255f, 255 / 255f);
            Color diaPink = new(220 / 255f, 170 / 255f, 210 / 255f);

            float ox = (seed % 97) * 0.19f;
            float oy = (seed % 53) * 0.27f;
            Vector2 light = new(-0.55f, 0.75f);

            // 2–3 irregular lobes — breaks the square wall-tile silhouette
            int lobes = 2 + (seed & 1);
            var lobeCx = new float[3];
            var lobeCy = new float[3];
            var lobeRx = new float[3];
            var lobeRy = new float[3];
            float ang0 = (seed % 360) * Mathf.Deg2Rad;
            for (int i = 0; i < lobes; i++)
            {
                float a = ang0 + i * (Mathf.PI * 2f / lobes) + Hash(seed + i * 17, i * 9) * 0.9f;
                float rad = 3.2f + Hash(seed + i * 31, 40 + i) * 4.5f;
                lobeCx[i] = (s - 1) * 0.5f + Mathf.Cos(a) * rad;
                lobeCy[i] = (s - 1) * 0.5f + Mathf.Sin(a) * rad * 0.85f;
                lobeRx[i] = 7.5f + Hash(seed + i * 7, 11) * 5.5f;
                lobeRy[i] = 5.5f + Hash(seed + i * 13, 19) * 5.0f;
            }

            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.2f + ox, y * 0.2f + oy);
                float n2 = Mathf.PerlinNoise(x * 0.55f + ox + 3f, y * 0.55f + oy);
                float n3 = Mathf.PerlinNoise(x * 1.05f + oy, y * 1.05f + ox);

                float best = 99f;
                float wx = 0f, wy = 0f;
                for (int i = 0; i < lobes; i++)
                {
                    float dx = (x - lobeCx[i]) / Mathf.Max(0.01f, lobeRx[i]);
                    float dy = (y - lobeCy[i]) / Mathf.Max(0.01f, lobeRy[i]);
                    // Squircle + noise → jagged rock edges
                    float d = Mathf.Pow(Mathf.Abs(dx), 1.65f) + Mathf.Pow(Mathf.Abs(dy), 1.65f);
                    d = Mathf.Sqrt(Mathf.Max(0f, d));
                    d += (n - 0.5f) * 0.34f;
                    d += (n2 - 0.5f) * 0.22f;
                    d += Mathf.Abs(Mathf.Sin(dx * 7.1f + n3 * 5f)) * 0.08f;
                    d += Mathf.Abs(Mathf.Cos(dy * 6.2f - n * 4f)) * 0.07f;
                    if (d < best)
                    {
                        best = d;
                        wx = dx;
                        wy = dy;
                    }
                }

                if (best > 1.05f) continue;

                float edge = Mathf.Clamp01((1.05f - best) * 3.2f);
                bool isOutline = edge < 0.36f;

                Vector2 grad = new(
                    Mathf.PerlinNoise(x * 0.38f + ox + 1f, y * 0.38f + oy) - 0.5f,
                    Mathf.PerlinNoise(x * 0.38f + ox, y * 0.38f + oy + 2f) - 0.5f);
                grad += new Vector2(-wx, -wy) * 0.4f;
                if (grad.sqrMagnitude > 0.0001f) grad.Normalize();
                float ndot = Vector2.Dot(grad, light);

                Color rock;
                if (isOutline)
                    rock = outline;
                else if (ndot > 0.35f)
                    rock = Color.Lerp(warm, lit, Mathf.Clamp01((ndot - 0.35f) / 0.45f));
                else if (ndot > 0.05f)
                    rock = Color.Lerp(body, warm, (ndot - 0.05f) / 0.3f);
                else if (ndot > -0.25f)
                    rock = Color.Lerp(shade, mid, (ndot + 0.25f) / 0.3f);
                else
                    rock = Color.Lerp(outline, shade, 0.45f);

                if (!isOutline && Hash(x * 3 + seed, y * 7) > 0.8f)
                    rock = Color.Lerp(rock, shade, 0.5f);
                if (!isOutline && ndot > 0.5f && Hash(x * 11 + seed, y * 5) > 0.86f)
                    rock = Color.Lerp(rock, hi, 0.45f);
                // Fracture cracks
                if (!isOutline && Hash(x * 19 + seed, y * 23) > 0.93f)
                    rock = Color.Lerp(rock, outline, 0.55f);

                int goldN = goldGrade;
                if (goldN > 0 && !isOutline)
                {
                    // Flecks / veins — not a solid gold disc
                    float t = goldN / 4f;
                    if (Hash(x * 5 + seed, y * 11) > 0.78f - t * 0.28f)
                        rock = Color.Lerp(rock, goldC, 0.35f + t * 0.4f);
                    if (Hash(x * 9 + seed, y * 3) > 0.86f - t * 0.2f)
                        rock = Color.Lerp(rock, goldBright, 0.4f + t * 0.35f);
                    if (goldN >= 3 && Hash(x * 13 + seed, y * 17) > 0.9f)
                        rock = Color.Lerp(rock, goldSpeck, 0.5f);
                }

                int diaN = diamondGrade;
                if (diaN > 0 && !isOutline)
                {
                    float t = diaN / 4f;
                    rock = Color.Lerp(rock, diaDeep, 0.18f + t * 0.55f);
                    float facet = Hash(x * 17 + seed, y * 23);
                    if (facet > 0.78f - t * 0.4f)
                        rock = Color.Lerp(rock, diaMid, 0.45f + t * 0.4f);
                    if (facet > 0.9f - t * 0.25f)
                        rock = Color.Lerp(rock, diaHi, 0.55f + t * 0.35f);
                    if (diaN >= 2 && Hash(x * 29 + seed, y * 7) > 0.88f)
                        rock = Color.Lerp(rock, diaFlash, 0.65f);
                    if (diaN >= 3 && Hash(x * 41 + seed, y * 13) > 0.86f)
                        rock = Color.Lerp(rock, diaPink, 0.4f);
                }

                rock.a = 1f;
                tex.SetPixel(x, y, rock);
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        /// <summary>
        /// Stamp one angular rubble chip into a Color32 buffer (floor border debris).
        /// Matches MakeWallChunk look — warm brown, dark rim, lit facets.
        /// </summary>
        public static void StampRubbleChip(Color32[] px, int stride, int texH,
            float cx, float cy, float radiusPx, int seed)
        {
            if (px == null || radiusPx < 0.8f) return;
            int x0 = Mathf.FloorToInt(cx - radiusPx - 1);
            int x1 = Mathf.CeilToInt(cx + radiusPx + 1);
            int y0 = Mathf.FloorToInt(cy - radiusPx - 1);
            int y1 = Mathf.CeilToInt(cy + radiusPx + 1);
            float ox = (seed % 97) * 0.19f;
            float oy = (seed % 53) * 0.27f;
            Vector2 light = new(-0.55f, 0.75f);

            Color32 outline = new(12, 8, 5, 255);
            Color32 shade = new(36, 24, 14, 255);
            Color32 mid = new(62, 40, 22, 255);
            Color32 body = new(78, 50, 28, 255);
            Color32 warm = new(98, 64, 34, 255);
            Color32 lit = new(118, 78, 40, 255);

            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if ((uint)x >= (uint)stride || (uint)y >= (uint)texH) continue;
                float dx = (x - cx) / radiusPx;
                float dy = (y - cy) / radiusPx;
                float n = Mathf.PerlinNoise(x * 0.35f + ox, y * 0.35f + oy);
                float n2 = Mathf.PerlinNoise(x * 0.7f + ox + 3f, y * 0.7f + oy);
                float d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                d = Mathf.Lerp(d, Mathf.Sqrt(dx * dx + dy * dy), 0.4f);
                d += (n - 0.5f) * 0.32f;
                d += (n2 - 0.5f) * 0.16f;
                d += Mathf.Abs(Mathf.Sin(dx * 5.5f + seed)) * 0.05f;
                if (d > 1.02f) continue;

                float edge = Mathf.Clamp01((1.02f - d) * 3.2f);
                int i = y * stride + x;
                if (edge < 0.25f)
                {
                    var under = px[i];
                    if (under.a > 10)
                        px[i] = Lerp32(under, outline, 0.55f);
                    continue;
                }

                Vector2 grad = new(
                    Mathf.PerlinNoise(x * 0.4f + ox + 1f, y * 0.4f + oy) - 0.5f - dx * 0.3f,
                    Mathf.PerlinNoise(x * 0.4f + ox, y * 0.4f + oy + 2f) - 0.5f - dy * 0.3f);
                if (grad.sqrMagnitude > 0.0001f) grad.Normalize();
                float ndot = Vector2.Dot(grad, light);

                Color32 rock;
                if (edge < 0.42f)
                    rock = outline;
                else if (ndot > 0.35f)
                    rock = lit;
                else if (ndot > 0.08f)
                    rock = warm;
                else if (ndot > -0.2f)
                    rock = body;
                else
                    rock = shade;

                if (edge > 0.42f && Hash(x * 3 + seed, y * 7) > 0.85f)
                    rock = Lerp32(rock, mid, 0.4f);

                px[i] = rock;
            }
        }

        static Color32 Lerp32(Color32 a, Color32 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color32(
                (byte)(a.r + (b.r - a.r) * t),
                (byte)(a.g + (b.g - a.g) * t),
                (byte)(a.b + (b.b - a.b) * t),
                (byte)(a.a + (b.a - a.a) * t));
        }

        static float Hash(int x, int y)
        {
            int n = x * 374761393 + y * 668265263;
            n = (n ^ (n >> 13)) * 1274126177;
            return ((n ^ (n >> 16)) & 0x7fffffff) / (float)0x7fffffff;
        }

        public static Sprite LanternBody
        {
            get
            {
                if (_lanternBody != null) return _lanternBody;
                _lanternBody = MakeIndustrialLantern();
                return _lanternBody;
            }
        }

        static Sprite _lanternGlow;

        public static Sprite LanternGlow
        {
            get
            {
                if (_lanternGlow != null) return _lanternGlow;
                _lanternGlow = MakeLanternGlowCore();
                return _lanternGlow;
            }
        }

        /// <summary>
        /// True top-down industrial mine lantern — circular cage, orange dial, amber core.
        /// Matches Deep Core crew art direction (helmet-centered top-down language).
        /// </summary>
        static Sprite MakeIndustrialLantern()
        {
            const int s = 56;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            ClearRect(tex, s, s);

            Color orange = new(0.92f, 0.48f, 0.12f);
            Color orangeHi = new(1f, 0.62f, 0.22f);
            Color orangeDeep = new(0.62f, 0.28f, 0.08f);
            Color rust = new(0.45f, 0.22f, 0.1f);
            Color black = new(0.06f, 0.06f, 0.07f);
            Color charcoal = new(0.14f, 0.15f, 0.16f);
            Color metal = new(0.42f, 0.44f, 0.48f);
            Color metalHi = new(0.68f, 0.7f, 0.74f);
            Color metalDeep = new(0.24f, 0.26f, 0.29f);
            Color outline = new(0.02f, 0.02f, 0.03f);
            Color glowDeep = new(0.85f, 0.35f, 0.08f);
            Color glow = new(1f, 0.55f, 0.15f);
            Color glowHi = new(1f, 0.82f, 0.35f);
            Color glowCore = new(1f, 0.95f, 0.7f);

            void Dot(int x, int y, Color c)
            {
                if ((uint)x < s && (uint)y < s) tex.SetPixel(x, y, c);
            }

            void Box(int x0, int y0, int bw, int bh, Color c)
            {
                for (int y = y0; y < y0 + bh; y++)
                for (int x = x0; x < x0 + bw; x++)
                    Dot(x, y, c);
            }

            void Disc(float cx, float cy, float rx, float ry, Color fill, Color? edge = null)
            {
                int x0 = Mathf.FloorToInt(cx - rx - 1);
                int x1 = Mathf.CeilToInt(cx + rx + 1);
                int y0 = Mathf.FloorToInt(cy - ry - 1);
                int y1 = Mathf.CeilToInt(cy + ry + 1);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x - cx) / Mathf.Max(0.01f, rx);
                    float dy = (y - cy) / Mathf.Max(0.01f, ry);
                    float d = dx * dx + dy * dy;
                    if (d > 1.02f) continue;
                    if (edge.HasValue && d > 0.78f) Dot(x, y, edge.Value);
                    else Dot(x, y, fill);
                }
            }

            void Weather(int x0, int y0, int bw, int bh, int seed)
            {
                for (int y = y0; y < y0 + bh; y++)
                for (int x = x0; x < x0 + bw; x++)
                {
                    if ((uint)x >= s || (uint)y >= s) continue;
                    var c = tex.GetPixel(x, y);
                    if (c.a < 0.1f) continue;
                    float n = Mathf.PerlinNoise(x * 0.4f + seed, y * 0.4f);
                    if (n > 0.74f) tex.SetPixel(x, y, Color.Lerp(c, rust, 0.4f));
                    else if (n < 0.2f) tex.SetPixel(x, y, Color.Lerp(c, black, 0.28f));
                    else if (n > 0.55f && n < 0.58f) tex.SetPixel(x, y, Color.Lerp(c, metalHi, 0.35f));
                }
            }

            void Bolt(int x, int y)
            {
                Box(x - 1, y - 1, 3, 3, charcoal);
                Dot(x, y, metalHi);
            }

            float cx = 27.5f, cy = 26.5f;

            // Outer base ring (gunmetal foot seen from above)
            Disc(cx, cy, 22, 22, outline, null);
            Disc(cx, cy, 20.5f, 20.5f, charcoal, outline);
            Disc(cx, cy, 18, 18, metalDeep, null);

            // Alternating orange / grey base segments
            for (int a = 0; a < 8; a++)
            {
                float ang = a * Mathf.PI * 0.25f + 0.2f;
                for (int r = 14; r <= 19; r++)
                {
                    int x = Mathf.RoundToInt(cx + Mathf.Cos(ang) * r);
                    int y = Mathf.RoundToInt(cy + Mathf.Sin(ang) * r);
                    Color seg = (a % 2 == 0) ? orangeDeep : charcoal;
                    Box(x - 1, y - 1, 3, 3, seg);
                    if (a % 2 == 0) Dot(x, y, orange);
                }
            }

            // Protective cage struts (radial)
            for (int a = 0; a < 6; a++)
            {
                float ang = a * Mathf.PI / 3f;
                for (int r = 6; r <= 16; r++)
                {
                    int x = Mathf.RoundToInt(cx + Mathf.Cos(ang) * r);
                    int y = Mathf.RoundToInt(cy + Mathf.Sin(ang) * r);
                    Dot(x, y, charcoal);
                    if (r % 3 == 0) Dot(x, y, metal);
                }
            }

            // Glass chamber glow (warm amber disc)
            Disc(cx, cy, 11, 11, new Color(0.45f, 0.2f, 0.06f, 0.75f), outline);
            Disc(cx, cy, 9, 9, glowDeep, null);
            Disc(cx, cy, 6.5f, 6.5f, glow, null);
            Disc(cx, cy, 3.5f, 3.5f, glowHi, null);

            // Dual filament rods (seen as two bright bars from above)
            Box(Mathf.RoundToInt(cx) - 4, Mathf.RoundToInt(cy) - 5, 2, 10, glowHi);
            Box(Mathf.RoundToInt(cx) + 2, Mathf.RoundToInt(cy) - 5, 2, 10, glowHi);
            Dot(Mathf.RoundToInt(cx) - 3, Mathf.RoundToInt(cy), glowCore);
            Dot(Mathf.RoundToInt(cx) + 3, Mathf.RoundToInt(cy), glowCore);
            Dot(Mathf.RoundToInt(cx) - 3, Mathf.RoundToInt(cy) + 2, Color.white);
            Dot(Mathf.RoundToInt(cx) + 3, Mathf.RoundToInt(cy) - 2, Color.white);

            // Top cap / orange dial (center identity)
            Disc(cx, cy + 0.5f, 7.5f, 7.5f, orangeDeep, outline);
            Disc(cx, cy + 0.5f, 6f, 6f, orange, null);
            Disc(cx, cy + 0.5f, 3.5f, 3.5f, orangeHi, null);
            // Triangle mark on dial
            Dot(Mathf.RoundToInt(cx), Mathf.RoundToInt(cy) - 1, charcoal);
            Dot(Mathf.RoundToInt(cx) - 1, Mathf.RoundToInt(cy), charcoal);
            Dot(Mathf.RoundToInt(cx) + 1, Mathf.RoundToInt(cy), charcoal);
            Dot(Mathf.RoundToInt(cx), Mathf.RoundToInt(cy) + 1, metalDeep);

            Bolt(Mathf.RoundToInt(cx) - 12, Mathf.RoundToInt(cy) - 10);
            Bolt(Mathf.RoundToInt(cx) + 12, Mathf.RoundToInt(cy) - 10);
            Bolt(Mathf.RoundToInt(cx) - 12, Mathf.RoundToInt(cy) + 10);
            Bolt(Mathf.RoundToInt(cx) + 12, Mathf.RoundToInt(cy) + 10);

            // Amber status pip
            Box(Mathf.RoundToInt(cx) + 8, Mathf.RoundToInt(cy) + 8, 3, 3, glow);
            Dot(Mathf.RoundToInt(cx) + 9, Mathf.RoundToInt(cy) + 9, glowCore);

            // Carry handle — arc on the "north" side (top of sprite)
            for (int i = 0; i <= 14; i++)
            {
                float t = i / 14f;
                float ang = Mathf.PI * 0.18f + t * Mathf.PI * 0.64f;
                float r = 23.5f;
                int x = Mathf.RoundToInt(cx + Mathf.Cos(ang) * r);
                int y = Mathf.RoundToInt(cy + Mathf.Sin(ang) * r + 2f);
                Box(x - 1, y - 1, 3, 3, charcoal);
                Dot(x, y, metalDeep);
            }
            // Orange grip on handle crown
            for (int i = 4; i <= 10; i++)
            {
                float t = i / 14f;
                float ang = Mathf.PI * 0.18f + t * Mathf.PI * 0.64f;
                float r = 24.2f;
                int x = Mathf.RoundToInt(cx + Mathf.Cos(ang) * r);
                int y = Mathf.RoundToInt(cy + Mathf.Sin(ang) * r + 2f);
                Box(x - 1, y - 1, 3, 2, orangeDeep);
                Dot(x, y, orange);
            }
            // Handle hinges
            Box(Mathf.RoundToInt(cx) - 16, Mathf.RoundToInt(cy) + 8, 5, 4, metalDeep);
            Box(Mathf.RoundToInt(cx) + 12, Mathf.RoundToInt(cy) + 8, 5, 4, metalDeep);
            Bolt(Mathf.RoundToInt(cx) - 14, Mathf.RoundToInt(cy) + 10);
            Bolt(Mathf.RoundToInt(cx) + 14, Mathf.RoundToInt(cy) + 10);

            Weather(4, 4, 48, 48, 9);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.48f), s);
        }

        static Sprite MakeLanternGlowCore()
        {
            const int s = 16;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            Clear(tex, s);
            float cx = (s - 1) * 0.5f, cy = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - cx) / 6.5f;
                float dy = (y - cy) / 6.5f;
                float d = dx * dx + dy * dy;
                if (d > 1f) continue;
                float t = 1f - d;
                Color c = Color.Lerp(new Color(1f, 0.4f, 0.08f, 0f),
                    new Color(1f, 0.9f, 0.55f, 0.95f), t * t);
                if (d < 0.18f) c = new Color(1f, 0.98f, 0.85f, 1f);
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        static void ClearRect(Texture2D tex, int w, int h)
        {
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, new Color(0, 0, 0, 0));
        }

        public static Light2D ConfigurePointLight(Light2D light, Color color, float intensity, float outer,
            float inner = 0.15f, bool shadows = true, float falloff = 0.55f)
        {
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.pointLightInnerRadius = inner;
            light.pointLightOuterRadius = outer;
            light.falloffIntensity = falloff;
            light.shadowsEnabled = shadows;
            light.shadowIntensity = shadows ? 0.95f : 0f;
            light.shadowSoftness = 0.55f;
            light.shadowSoftnessFalloffIntensity = 0.55f;
            light.overlapOperation = Light2D.OverlapOperation.AlphaBlend;
            return light;
        }

        public static GameObject PlaceLantern(Transform parent, Vector2 localOrWorld, bool local, float intensity = 2.1f)
        {
            var go = new GameObject("Lantern");
            go.transform.SetParent(parent, !local);
            if (local) go.transform.localPosition = localOrWorld;
            else go.transform.position = localOrWorld;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LanternBody;
            sr.sortingOrder = 28;
            ApplyLit(sr);
            // Top-down lantern — compact footprint
            go.transform.localScale = Vector3.one * 0.25f;

            // Unlit filament bloom centered on the lantern
            var glow = new GameObject("FilamentGlow");
            glow.transform.SetParent(go.transform, false);
            glow.transform.localPosition = Vector3.zero;
            glow.transform.localScale = Vector3.one * 0.7f;
            var gsr = glow.AddComponent<SpriteRenderer>();
            gsr.sprite = LanternGlow;
            gsr.sortingOrder = 29;
            var sh = Shader.Find("Sprites/Default");
            if (sh != null) gsr.sharedMaterial = new Material(sh);
            gsr.color = new Color(1f, 0.75f, 0.35f, 0.85f);

            // Warm industrial amber pool — no 2D shadows (per-cell casters × lanterns is too costly)
            var light = go.AddComponent<Light2D>();
            ConfigurePointLight(light,
                new Color(1f, 0.52f, 0.18f),
                intensity,
                outer: 3.15f,
                inner: 0.12f,
                shadows: false,
                falloff: 0.7f);

            go.AddComponent<CosyLantern>().Init(go.transform.position, light, intensity);
            return go;
        }

        /// <summary>
        /// Builds the playable digger visual via <see cref="CrewVisualKit"/> art direction.
        /// </summary>
        public static void AttachDrillerVisual(GameObject root, FreeWorkerController worker)
        {
            CrewVisualKit.AttachExcavator(root, worker);
        }

        static Sprite MakeBlobSprite(int s, bool rock)
        {
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            Clear(tex, s);
            float cx = (s - 1) * 0.5f, cy = (s - 1) * 0.42f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - cx) / (s * 0.38f);
                float dy = (y - cy) / (s * 0.32f);
                float d = dx * dx + dy * dy;
                float n = Mathf.PerlinNoise(x * 0.18f + (rock ? 2f : 9f), y * 0.18f);
                d -= (n - 0.5f) * 0.35f;
                if (d > 1f) continue;

                float edge = Mathf.Clamp01((1f - d) * 1.4f);
                Color c;
                if (rock)
                {
                    c = Color.Lerp(new Color(0.22f, 0.2f, 0.18f), new Color(0.55f, 0.42f, 0.28f), n);
                    c = Color.Lerp(c, new Color(0.12f, 0.11f, 0.1f), 1f - edge);
                }
                else
                {
                    // Mid gold — lit by lanterns/excavator, nearly gone in the dark
                    c = Color.Lerp(new Color(0.45f, 0.3f, 0.08f), new Color(0.95f, 0.72f, 0.22f), n * edge);
                    if (n > 0.75f) c = Color.Lerp(c, new Color(1f, 0.9f, 0.5f), 0.25f * edge);
                    c = Color.Lerp(new Color(0.22f, 0.15f, 0.05f), c, edge);
                }
                c.a = Mathf.Clamp01(edge * 1.2f);
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.35f), s);
        }

        static Sprite MakeDiamondSprite(int s)
        {
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Clear(tex, s);
            float cx = (s - 1) * 0.5f, cy = (s - 1) * 0.48f;
            Color deep = new(0.15f, 0.35f, 0.55f);
            Color mid = new(0.45f, 0.75f, 0.95f);
            Color hi = new(0.85f, 0.95f, 1f);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - cx) / (s * 0.28f);
                float dy = (y - cy) / (s * 0.36f);
                // Octahedral / diamond silhouette
                float d = Mathf.Abs(dx) + Mathf.Abs(dy);
                if (d > 1.05f) continue;
                float edge = Mathf.Clamp01((1.05f - d) * 2.2f);
                Color c = Color.Lerp(deep, mid, edge);
                // Facet ridges
                if (Mathf.Abs(dx) < 0.08f || Mathf.Abs(dy) < 0.08f)
                    c = Color.Lerp(c, hi, 0.45f);
                if (Mathf.Abs(Mathf.Abs(dx) - Mathf.Abs(dy)) < 0.07f)
                    c = Color.Lerp(c, hi, 0.35f);
                if (d < 0.25f) c = Color.Lerp(c, Color.white, 0.55f);
                c.a = Mathf.Clamp01(edge * 1.15f);
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.4f), s);
        }

        static void Clear(Texture2D tex, int s)
        {
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, new Color(0, 0, 0, 0));
        }
    }
}
