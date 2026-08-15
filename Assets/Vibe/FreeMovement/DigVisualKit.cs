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

        /// <summary>
        /// Loose cell chunk that matches dig-face wall look: dark speckled rock + gold flecks by grade.
        /// </summary>
        public static Sprite MakeWallChunk(byte goldGrade, int bedrockCount, int seed)
        {
            const int s = 24;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Clear(tex, s);

            // Wall palette (same family as FreeMovementTerrainView)
            Color wallA = new(28 / 255f, 24 / 255f, 20 / 255f);
            Color wallB = new(36 / 255f, 30 / 255f, 26 / 255f);
            Color wallC = new(22 / 255f, 20 / 255f, 18 / 255f);
            Color grain = new(40 / 255f, 34 / 255f, 28 / 255f);
            Color goldC = new(220 / 255f, 160 / 255f, 42 / 255f);
            Color goldBright = new(255 / 255f, 210 / 255f, 70 / 255f);
            Color goldSpeck = new(255 / 255f, 235 / 255f, 140 / 255f);

            if (bedrockCount >= 2)
            {
                wallA = Color.Lerp(wallA, new Color(0.07f, 0.08f, 0.09f), 0.35f);
                wallB = Color.Lerp(wallB, new Color(0.09f, 0.1f, 0.11f), 0.35f);
            }

            float cx = (s - 1) * 0.5f, cy = (s - 1) * 0.5f;
            float ox = (seed % 97) * 0.17f;
            float oy = (seed % 53) * 0.23f;

            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - cx) / (s * 0.42f);
                float dy = (y - cy) / (s * 0.42f);
                float n = Mathf.PerlinNoise(x * 0.28f + ox, y * 0.28f + oy);
                float n2 = Mathf.PerlinNoise(x * 0.55f + ox + 3f, y * 0.55f + oy);
                // Irregular rock chip silhouette (not a perfect square)
                float d = dx * dx + dy * dy;
                d -= (n - 0.5f) * 0.45f;
                d -= (n2 - 0.5f) * 0.2f;
                if (d > 1f) continue;

                float edge = Mathf.Clamp01((1f - d) * 1.6f);
                Color rock = n < 0.35f ? wallA : (n < 0.7f ? wallB : wallC);
                rock = Color.Lerp(rock, grain, n2 * 0.35f);
                if (Hash(x * 3 + seed, y * 7) > 0.75f)
                    rock = Color.Lerp(rock, grain, 0.3f);

                int goldN = goldGrade;
                if (goldN > 0)
                {
                    float t = goldN / 4f;
                    float g = 0.12f + t * 0.88f;
                    if (Hash(x * 5 + seed, y * 11) > 0.55f - t * 0.25f)
                        rock = Color.Lerp(rock, goldC, g * 0.85f);
                    if (Hash(x * 9 + seed, y * 3) > 0.78f - t * 0.3f)
                        rock = Color.Lerp(rock, goldBright, (0.25f + t * 0.55f));
                    if (goldN >= 3 && Hash(x * 13 + seed, y * 17) > 0.82f)
                        rock = Color.Lerp(rock, goldSpeck, 0.45f);
                }

                rock.a = Mathf.Clamp01(edge * 1.15f);
                // Slight darken at rim so it reads as a chunk on the floor
                if (edge < 0.45f)
                    rock = Color.Lerp(rock, new Color(0.05f, 0.04f, 0.03f, rock.a), 0.35f);
                tex.SetPixel(x, y, rock);
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
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
                const int s = 32;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                Clear(tex, s);
                // Hook
                Fill(tex, s, 14, 26, 4, 4, new Color(0.55f, 0.55f, 0.6f));
                // Cage
                Fill(tex, s, 10, 8, 12, 16, new Color(0.35f, 0.28f, 0.18f));
                // Glass glow
                Fill(tex, s, 12, 10, 8, 10, new Color(1f, 0.75f, 0.3f, 0.95f));
                // Highlight
                Fill(tex, s, 13, 16, 3, 3, new Color(1f, 0.95f, 0.7f, 0.9f));
                tex.Apply();
                _lanternBody = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.15f), s);
                return _lanternBody;
            }
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
            go.transform.localScale = Vector3.one * 0.55f;

            // Single hard cozy pool — no fill wash that lights the whole mine
            var light = go.AddComponent<Light2D>();
            ConfigurePointLight(light,
                new Color(1f, 0.58f, 0.22f),
                intensity,
                outer: 3.1f,
                inner: 0.1f,
                shadows: false,
                falloff: 0.72f);

            go.AddComponent<CosyLantern>().Init(go.transform.position, light, intensity);
            return go;
        }

        static Sprite _driller;
        static Sprite _drillBit;

        /// <summary>Top-down miner with a large forward drill (sprite "up" = dig direction).</summary>
        public static Sprite Driller
        {
            get
            {
                if (_driller != null) return _driller;
                _driller = MakeDrillerSprite();
                return _driller;
            }
        }

        public static Sprite DrillBit
        {
            get
            {
                if (_drillBit != null) return _drillBit;
                _drillBit = MakeDrillBitSprite();
                return _drillBit;
            }
        }

        /// <summary>
        /// Builds the playable digger visual: person + oversized drill facing +Y.
        /// Dig footprint scale is applied by the caller on the root.
        /// </summary>
        public static void AttachDrillerVisual(GameObject root, FreeWorkerController worker)
        {
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, -0.06f, 0f);
            body.transform.localScale = Vector3.one * 0.5f;
            var bsr = body.AddComponent<SpriteRenderer>();
            bsr.sprite = Driller;
            bsr.sortingOrder = 40;
            ApplyLit(bsr);

            var drill = new GameObject("DrillBit");
            drill.transform.SetParent(root.transform, false);
            // Tip ahead of body (local +Y = dig facing). Visual only — dig radius unchanged.
            drill.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            drill.transform.localRotation = Quaternion.identity;
            drill.transform.localScale = Vector3.one * 0.42f;
            var dsr = drill.AddComponent<SpriteRenderer>();
            dsr.sprite = DrillBit;
            dsr.sortingOrder = 41;
            ApplyLit(dsr);

            var anim = root.GetComponent<DrillerVisual>();
            if (anim == null) anim = root.AddComponent<DrillerVisual>();
            anim.Init(worker, drill.transform, body.transform);
        }

        static Sprite MakeDrillerSprite()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            Clear(tex, s);

            // Boots
            Fill(tex, s, 22, 6, 7, 8, new Color(0.18f, 0.16f, 0.14f));
            Fill(tex, s, 35, 6, 7, 8, new Color(0.18f, 0.16f, 0.14f));
            // Legs
            Fill(tex, s, 23, 12, 6, 10, new Color(0.2f, 0.28f, 0.38f));
            Fill(tex, s, 35, 12, 6, 10, new Color(0.2f, 0.28f, 0.38f));
            // Body / coveralls (orange miner — distinct from blue hauler)
            Fill(tex, s, 20, 20, 24, 16, new Color(0.82f, 0.42f, 0.12f));
            Fill(tex, s, 24, 24, 16, 8, new Color(0.55f, 0.3f, 0.1f)); // harness
            // Arms holding drill
            Fill(tex, s, 14, 26, 8, 7, new Color(0.82f, 0.42f, 0.12f));
            Fill(tex, s, 42, 26, 8, 7, new Color(0.82f, 0.42f, 0.12f));
            // Hands
            Fill(tex, s, 16, 32, 5, 5, new Color(0.9f, 0.72f, 0.55f));
            Fill(tex, s, 43, 32, 5, 5, new Color(0.9f, 0.72f, 0.55f));
            // Head
            Fill(tex, s, 26, 34, 12, 10, new Color(0.92f, 0.74f, 0.58f));
            // Hard hat
            Fill(tex, s, 24, 42, 16, 8, new Color(0.95f, 0.72f, 0.15f));
            Fill(tex, s, 28, 48, 8, 3, new Color(0.85f, 0.6f, 0.1f));
            // Drill housing on chest / upward (static part of body art)
            Fill(tex, s, 27, 28, 10, 6, new Color(0.35f, 0.38f, 0.42f));

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.35f), s);
        }

        static Sprite MakeDrillBitSprite()
        {
            const int s = 48;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            Clear(tex, s);

            // Shaft (along +Y from pivot near bottom)
            Fill(tex, s, 20, 4, 8, 22, new Color(0.4f, 0.42f, 0.46f));
            Fill(tex, s, 22, 6, 4, 18, new Color(0.55f, 0.58f, 0.62f));
            // Motor housing
            Fill(tex, s, 16, 2, 16, 10, new Color(0.28f, 0.3f, 0.34f));
            Fill(tex, s, 18, 4, 12, 6, new Color(0.75f, 0.45f, 0.12f));
            // Auger / bit tip (wide, reads oversized)
            Fill(tex, s, 14, 24, 20, 8, new Color(0.5f, 0.52f, 0.55f));
            Fill(tex, s, 17, 30, 14, 8, new Color(0.62f, 0.64f, 0.68f));
            Fill(tex, s, 20, 36, 8, 8, new Color(0.72f, 0.74f, 0.78f));
            // Tip point
            for (int i = 0; i < 6; i++)
                Fill(tex, s, 22 - i / 2, 42 + i, 4 + i, 1, new Color(0.85f, 0.86f, 0.88f));
            // Helix hint
            Fill(tex, s, 15, 26, 4, 3, new Color(0.35f, 0.36f, 0.4f));
            Fill(tex, s, 29, 32, 4, 3, new Color(0.35f, 0.36f, 0.4f));

            tex.Apply();
            // Pivot near motor / hands
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.15f), s);
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

        static void Clear(Texture2D tex, int s)
        {
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, new Color(0, 0, 0, 0));
        }

        static void Fill(Texture2D tex, int s, int x0, int y0, int w, int h, Color c)
        {
            for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                if (x >= 0 && y >= 0 && x < s && y < s) tex.SetPixel(x, y, c);
        }
    }
}
