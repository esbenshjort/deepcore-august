using UnityEngine;

namespace DeepCore.DualGrid
{
    /// <summary>
    /// Soft-field cave render: square data stays, silhouette becomes organic / fluid.
    /// </summary>
    public sealed class DualGridFluidView : MonoBehaviour
    {
        const int Ppu = 8; // pixels per terrain cell

        DualGridWorld _world;
        SpriteRenderer _floorSr;
        SpriteRenderer _rockSr;
        Texture2D _floorTex;
        Texture2D _rockTex;
        Color32[] _floorPixels;
        Color32[] _rockPixels;
        Sprite _floorSprite;
        Sprite _rockSprite;
        bool _dirty = true;

        static readonly Color32 FloorA = new(92, 58, 32, 255);
        static readonly Color32 FloorB = new(128, 82, 42, 255);
        static readonly Color32 FloorC = new(70, 44, 26, 255);
        static readonly Color32 RockDeep = new(18, 19, 24, 255);
        static readonly Color32 RockMid = new(38, 40, 48, 255);
        static readonly Color32 RockEdge = new(52, 50, 56, 255);
        static readonly Color32 Clear = new(0, 0, 0, 0);

        public void Setup(DualGridWorld world)
        {
            _world = world;
            _world.Changed += () => _dirty = true;

            int tw = _world.TerrainWidth;
            int th = _world.TerrainHeight;
            int w = tw * Ppu;
            int h = th * Ppu;

            _floorTex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _rockTex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _floorPixels = new Color32[w * h];
            _rockPixels = new Color32[w * h];

            _floorSprite = Sprite.Create(_floorTex, new Rect(0, 0, w, h), new Vector2(0f, 0f), Ppu * DualGridWorld.TerrainPerNav);
            _rockSprite = Sprite.Create(_rockTex, new Rect(0, 0, w, h), new Vector2(0f, 0f), Ppu * DualGridWorld.TerrainPerNav);

            // World size of sprite: (tw*Ppu) / (Ppu*5) = tw/5 = NavWidth. Same for height.
            // Pivot bottom-left at origin — covers nav map [0..NavW] x [0..NavH].

            var floorGo = new GameObject("FluidFloor");
            floorGo.transform.SetParent(transform, false);
            floorGo.transform.localPosition = Vector3.zero;
            _floorSr = floorGo.AddComponent<SpriteRenderer>();
            _floorSr.sprite = _floorSprite;
            _floorSr.sortingOrder = 0;

            var rockGo = new GameObject("FluidRock");
            rockGo.transform.SetParent(transform, false);
            rockGo.transform.localPosition = Vector3.zero;
            _rockSr = rockGo.AddComponent<SpriteRenderer>();
            _rockSr.sprite = _rockSprite;
            _rockSr.sortingOrder = 2;

            Rebuild();
        }

        void LateUpdate()
        {
            if (_dirty) Rebuild();
        }

        public void Rebuild()
        {
            if (_world == null) return;
            _dirty = false;

            int tw = _world.TerrainWidth;
            int th = _world.TerrainHeight;
            int w = tw * Ppu;
            int h = th * Ppu;
            float invPpu = 1f / Ppu;

            for (int py = 0; py < h; py++)
            {
                float ty = (py + 0.5f) * invPpu; // terrain-space
                for (int px = 0; px < w; px++)
                {
                    float tx = (px + 0.5f) * invPpu;
                    int i = py * w + px;

                    float field = SampleExcavatedField(tx, ty, tw, th);
                    // organic wobble on the isoline
                    float wobble = 0.1f * Fractal(tx * 0.55f, ty * 0.55f, 2);
                    field += wobble - 0.05f;

                    float edge = Mathf.Abs(field);
                    bool floor = field > 0f;

                    if (floor)
                    {
                        // world UV from terrain coords (terrainPerNav cells = 1 world unit)
                        float wx = tx / DualGridWorld.TerrainPerNav;
                        float wy = ty / DualGridWorld.TerrainPerNav;
                        Color32 floorCol = DualGridArt.SampleFloor(wx, wy);
                        if (Hash(px * 3, py * 7) > 0.96f)
                            floorCol = Lerp(floorCol, FloorC, 0.4f);

                        _floorPixels[i] = floorCol;
                        _rockPixels[i] = Clear;
                    }
                    else
                    {
                        _floorPixels[i] = Clear;

                        float wx = tx / DualGridWorld.TerrainPerNav;
                        float wy = ty / DualGridWorld.TerrainPerNav;
                        float dmg = SampleDamage(tx, ty, tw, th);
                        Color32 rock = DualGridArt.SampleRock(wx, wy, dmg);

                        if (edge < 0.35f)
                        {
                            float rim = 1f - edge / 0.35f;
                            rock = Lerp(rock, RockEdge, rim * 0.35f);
                            rock = Lerp(rock, new Color32(70, 48, 32, 255), rim * 0.2f);
                        }

                        _rockPixels[i] = rock;
                    }
                }
            }

            _floorTex.SetPixels32(_floorPixels);
            _floorTex.Apply(false);
            _rockTex.SetPixels32(_rockPixels);
            _rockTex.Apply(false);
        }

        /// <summary>
        /// Positive = open / excavated. Damaged rock contributes partial openness (5 stages).
        /// </summary>
        float SampleExcavatedField(float tx, float ty, int tw, int th)
        {
            int cx = Mathf.FloorToInt(tx);
            int cy = Mathf.FloorToInt(ty);
            float best = -2f;

            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int x = cx + ox;
                int y = cy + oy;
                if (x < 0 || y < 0 || x >= tw || y >= th) continue;
                var state = _world.GetTerrain(x, y);

                float jx = (Hash01(x, y, 11) - 0.5f) * 0.32f;
                float jy = (Hash01(x, y, 29) - 0.5f) * 0.32f;
                float dx = tx - (x + 0.5f + jx);
                float dy = ty - (y + 0.5f + jy);
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                if (state == TerrainState.Excavated)
                {
                    float radius = 0.72f + Hash01(x, y, 47) * 0.22f;
                    best = Mathf.Max(best, radius - d);
                }
                else if (state > TerrainState.Solid)
                {
                    // Partial breakdown — rock thins as damage rises (stadier 1–4)
                    float t = (int)state / 5f; // 0.2 .. 0.8
                    float radius = (0.25f + t * 0.5f) * (0.85f + Hash01(x, y, 47) * 0.2f);
                    best = Mathf.Max(best, radius - d - 0.15f);
                }
            }

            best = Mathf.Max(best, DiagonalBridge(tx, ty, tw, th));
            return best;
        }

        float DiagonalBridge(float tx, float ty, int tw, int th)
        {
            int cx = Mathf.FloorToInt(tx);
            int cy = Mathf.FloorToInt(ty);
            float fx = tx - cx;
            float fy = ty - cy;
            float best = -2f;

            // Four corner solids of this cell's dual
            bool Exc(int x, int y) =>
                x >= 0 && y >= 0 && x < tw && y < th &&
                _world.GetTerrain(x, y) == TerrainState.Excavated;

            // Bridge NE-SW diagonal open
            if (Exc(cx, cy) && Exc(cx + 1, cy + 1) && !Exc(cx + 1, cy) && !Exc(cx, cy + 1))
            {
                float along = (fx + fy) * 0.5f;
                float dist = Mathf.Abs(fx - fy);
                best = Mathf.Max(best, 0.38f - dist - Mathf.Abs(along - 0.5f) * 0.15f);
            }
            if (Exc(cx + 1, cy) && Exc(cx, cy + 1) && !Exc(cx, cy) && !Exc(cx + 1, cy + 1))
            {
                float along = (fx + (1f - fy)) * 0.5f;
                float dist = Mathf.Abs(fx - (1f - fy));
                best = Mathf.Max(best, 0.38f - dist - Mathf.Abs(along - 0.5f) * 0.15f);
            }
            return best;
        }

        float SampleDamage(float tx, float ty, int tw, int th)
        {
            int cx = Mathf.Clamp(Mathf.FloorToInt(tx), 0, tw - 1);
            int cy = Mathf.Clamp(Mathf.FloorToInt(ty), 0, th - 1);
            float max = 0f;
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int x = cx + ox, y = cy + oy;
                if (x < 0 || y < 0 || x >= tw || y >= th) continue;
                var s = _world.GetTerrain(x, y);
                if (s == TerrainState.Solid || s == TerrainState.Excavated) continue;
                float t = (int)s / 4f;
                float dx = tx - (x + 0.5f);
                float dy = ty - (y + 0.5f);
                float fall = 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.1f);
                max = Mathf.Max(max, t * fall);
            }
            return max;
        }

        static float Fractal(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x * freq, y * freq) * amp;
                freq *= 2.03f;
                amp *= 0.5f;
            }
            return sum;
        }

        static float Hash(int x, int y)
        {
            uint n = (uint)(x * 374761393 + y * 668265263);
            n = (n ^ (n >> 13)) * 1274126177u;
            return (n & 0xFFFF) / 65535f;
        }

        static float Hash01(int x, int y, int seed)
        {
            uint n = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
            n = (n ^ (n >> 13)) * 1274126177u;
            return (n & 0xFFFF) / 65535f;
        }

        static Color32 Lerp(Color32 a, Color32 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color32(
                (byte)(a.r + (b.r - a.r) * t),
                (byte)(a.g + (b.g - a.g) * t),
                (byte)(a.b + (b.b - a.b) * t),
                (byte)(a.a + (b.a - a.a) * t));
        }
    }
}
