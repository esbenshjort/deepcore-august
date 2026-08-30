using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Organic soft-field floor + ~2 cells of readable rock wall beyond the dig
    /// so gold veins can be sensed in the face. Dark rock lip (not a bright overlay).
    /// </summary>
    public sealed class FreeMovementTerrainView : MonoBehaviour
    {
        const int Ppu = 4;
        const int PadCells = 4;
        /// <summary>How far into solid rock the wall stays visible (cell units).</summary>
        const float WallRevealCells = 2.6f;

        FineTerrainWorld _world;
        Texture2D _floorTex;
        Texture2D _wallTex;
        Color32[] _floorPx;
        Color32[] _wallPx;
        bool _pending;
        int _qx0, _qy0, _qx1, _qy1;
        float _cooldown;

        static readonly Color32 FloorA = new(62, 40, 22, 255);
        static readonly Color32 FloorB = new(78, 50, 28, 255);
        static readonly Color32 FloorC = new(48, 32, 18, 255);
        static readonly Color32 FloorWarm = new(88, 58, 32, 255);
        static readonly Color32 RockTint = new(42, 34, 28, 255);

        // Dark rock — only readable when lit
        static readonly Color32 WallA = new(28, 24, 20, 255);
        static readonly Color32 WallB = new(36, 30, 26, 255);
        static readonly Color32 WallC = new(22, 20, 18, 255);
        static readonly Color32 WallGrain = new(40, 34, 28, 255);

        static readonly Color32 GoldC = new(220, 160, 42, 255);
        static readonly Color32 GoldBright = new(255, 210, 70, 255);
        static readonly Color32 GoldSpeck = new(255, 235, 140, 255);
        static readonly Color32 DiaDeep = new(40, 90, 140, 255);
        static readonly Color32 DiaMid = new(120, 190, 230, 255);
        static readonly Color32 DiaHi = new(210, 240, 255, 255);
        static readonly Color32 DiaFlash = new(255, 250, 255, 255);
        static readonly Color32 DiaPink = new(220, 170, 210, 255);
        static readonly Color32 Clear = new(0, 0, 0, 0);

        public void Setup(FineTerrainWorld world, bool fogOfWarGold = false, bool strongCliffEdges = true)
        {
            _world = world;
            _world.RegionChanged += OnRegion;

            int w = world.Width * Ppu;
            int h = world.Height * Ppu;
            float ppuWorld = Ppu / world.CellSize;

            _floorTex = MakeTex(w, h);
            _wallTex = MakeTex(w, h);
            _floorPx = new Color32[w * h];
            _wallPx = new Color32[w * h];

            MakeLayer("FloorView", _floorTex, w, h, ppuWorld, 0, lit: true);
            // Lit wall — without lanterns the dig face vanishes into black
            MakeLayer("RockWall", _wallTex, w, h, ppuWorld, 2, lit: true);

            RebuildRect(0, 0, world.Width - 1, world.Height - 1);
            ApplyAll();
            _pending = false;
        }

        void MakeLayer(string name, Texture2D tex, int w, int h, float ppu, int order, bool lit)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero, ppu);
            sr.sortingOrder = order;
            if (lit) DigVisualKit.ApplyLit(sr);
            else
            {
                var sh = Shader.Find("Sprites/Default");
                if (sh != null) sr.sharedMaterial = new Material(sh);
            }
        }

        static Texture2D MakeTex(int w, int h) => new(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        void OnDestroy()
        {
            if (_world != null) _world.RegionChanged -= OnRegion;
        }

        void OnRegion(int x0, int y0, int x1, int y1)
        {
            x0 = Mathf.Max(0, x0 - PadCells);
            y0 = Mathf.Max(0, y0 - PadCells);
            x1 = Mathf.Min(_world.Width - 1, x1 + PadCells);
            y1 = Mathf.Min(_world.Height - 1, y1 + PadCells);

            if (!_pending)
            {
                _qx0 = x0; _qy0 = y0; _qx1 = x1; _qy1 = y1;
                _pending = true;
            }
            else
            {
                _qx0 = Mathf.Min(_qx0, x0);
                _qy0 = Mathf.Min(_qy0, y0);
                _qx1 = Mathf.Max(_qx1, x1);
                _qy1 = Mathf.Max(_qy1, y1);
            }
        }

        void LateUpdate()
        {
            if (!_pending) return;
            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f) return;
            _cooldown = 0.08f;

            int x0 = _qx0, y0 = _qy0, x1 = _qx1, y1 = _qy1;
            _pending = false;
            RebuildRect(x0, y0, x1, y1);
            ApplyRect(x0, y0, x1, y1);
        }

        void RebuildRect(int cx0, int cy0, int cx1, int cy1)
        {
            int tw = _world.Width;
            int th = _world.Height;
            int w = tw * Ppu;
            float inv = 1f / Ppu;

            int px0 = cx0 * Ppu;
            int py0 = cy0 * Ppu;
            int px1 = (cx1 + 1) * Ppu;
            int py1 = (cy1 + 1) * Ppu;

            for (int py = py0; py < py1; py++)
            {
                float ty = (py + 0.5f) * inv;
                for (int px = px0; px < px1; px++)
                {
                    float tx = (px + 0.5f) * inv;
                    int i = py * w + px;

                    float field = SampleOpen(tx, ty, tw, th);
                    field += 0.14f * Mathf.PerlinNoise(tx * 0.55f + 2.1f, ty * 0.55f) - 0.07f;
                    field += 0.06f * Mathf.PerlinNoise(tx * 1.4f, ty * 1.4f) - 0.03f;

                    // Gas void: no floor until seen (breach mouth / entered). Stops lanterns
                    // lighting the pocket through rock before you punch in.
                    int scx = Mathf.Clamp(Mathf.FloorToInt(tx), 0, tw - 1);
                    int scy = Mathf.Clamp(Mathf.FloorToInt(ty), 0, th - 1);
                    if (_world.IsGas(scx, scy) && !_world.IsFloorOpen(scx, scy) && field > 0f)
                        field = -0.4f;

                    _wallPx[i] = Clear;

                    if (field > 0f)
                    {
                        float n = Hash(px, py);
                        float grain = Mathf.PerlinNoise(tx * 1.6f, ty * 1.6f);
                        var col = n < 0.28f ? FloorA : (n < 0.62f ? FloorB : FloorC);
                        if (grain > 0.72f) col = Lerp(col, FloorWarm, 0.35f);
                        if (Hash(px * 3, py * 5) > 0.93f) col = FloorC;
                        col = Lerp(col, FloorA, grain * 0.1f);

                        if (field < 0.5f)
                        {
                            float t = 1f - field / 0.5f;
                            t = t * t;
                            col = Lerp(col, RockTint, t * 0.22f);
                            if (Hash(px + 17, py + 31) < t * 0.22f)
                                col = StampStoneTint(col, px, py, t);
                        }

                        int fcx = Mathf.Clamp(Mathf.FloorToInt(tx), 0, tw - 1);
                        int fcy = Mathf.Clamp(Mathf.FloorToInt(ty), 0, th - 1);
                        if (field < 0.7f && NearGold(fcx, fcy))
                        {
                            float g = (0.7f - field) * 0.85f;
                            if (Hash(px * 5, py * 11) > 0.55f)
                                col = Lerp(col, GoldC, g * 0.5f);
                            if (Hash(px * 9, py * 3) > 0.82f)
                                col = Lerp(col, GoldBright, g * 0.35f);
                        }

                        float a = field > 0.12f ? 1f : Mathf.Clamp01(field / 0.12f);
                        col.a = (byte)(a * 255f);
                        _floorPx[i] = col;
                    }
                    else
                    {
                        _floorPx[i] = Clear;

                        float depth = Mathf.Abs(field);
                        if (depth > WallRevealCells) continue;

                        // 1 at dig face → 0 at ~2 cells into rock
                        float reveal = 1f - depth / WallRevealCells;
                        reveal = reveal * reveal * (3f - 2f * reveal);

                        int cx = Mathf.Clamp(Mathf.FloorToInt(tx), 0, tw - 1);
                        int cy = Mathf.Clamp(Mathf.FloorToInt(ty), 0, th - 1);
                        var cell = _world.Get(cx, cy);
                        // Unseen gas void reads as ordinary rock until you break in
                        bool hideGas = _world.IsGas(cx, cy) && !_world.IsFloorOpen(cx, cy);
                        float n = Mathf.PerlinNoise(tx * 0.35f + 8f, ty * 0.35f);
                        float strata = Mathf.PerlinNoise(tx * 0.2f, ty * 0.5f);

                        Color32 rock = n < 0.35f ? WallA : (n < 0.7f ? WallB : WallC);
                        rock = Lerp(rock, WallGrain, strata * 0.35f);
                        if (Hash(px * 3, py * 7) > 0.75f)
                            rock = Lerp(rock, WallGrain, 0.3f);

                        if (!hideGas && cell.Phase == TerrainPhase.Damaged)
                        {
                            float d = cell.MaxDurability <= 0 ? 1f : cell.DamageState / (float)cell.MaxDurability;
                            rock = Lerp(rock, new Color32(80, 52, 30, 255), d * 0.4f * reveal);
                        }

                        // Gold: 1 socket = faint, 4 sockets = fully yellow & shiny
                        int goldN = hideGas ? 0 : cell.GoldCount;
                        if (goldN > 0)
                        {
                            float t = goldN / 4f; // 0.25 … 1
                            float g = (0.12f + t * 0.88f) * reveal;
                            rock = Lerp(rock, GoldC, g);
                            if (Hash(px * 5, py * 11) > 0.72f - t * 0.35f)
                                rock = Lerp(rock, GoldBright, (0.25f + t * 0.7f) * reveal);
                            if (goldN >= 3 && Hash(px * 9, py * 3) > 0.78f)
                                rock = Lerp(rock, GoldSpeck, 0.45f * reveal);
                            if (goldN >= 4 && Hash(px * 13, py * 17) > 0.9f)
                                rock = Lerp(rock, GoldSpeck, 0.5f * reveal);
                        }

                        // Diamonds: cool crystalline cyan / white facets
                        int diaN = hideGas ? 0 : cell.DiamondCount;
                        if (diaN > 0)
                        {
                            float t = diaN / 4f;
                            rock = Lerp(rock, DiaDeep, (0.2f + t * 0.55f) * reveal);
                            float facet = Hash(px * 17, py * 23);
                            if (facet > 0.78f - t * 0.4f)
                                rock = Lerp(rock, DiaMid, (0.4f + t * 0.4f) * reveal);
                            if (facet > 0.9f - t * 0.25f)
                                rock = Lerp(rock, DiaHi, (0.5f + t * 0.35f) * reveal);
                            if (diaN >= 2 && Hash(px * 29, py * 7) > 0.88f)
                                rock = Lerp(rock, DiaFlash, 0.55f * reveal);
                            if (diaN >= 3 && Hash(px * 41, py * 13) > 0.86f)
                                rock = Lerp(rock, DiaPink, 0.35f * reveal);
                        }
                        else if (goldN <= 0 && cell.BedrockCount >= 2)
                        {
                            // Cooler, denser look for bedrock-heavy cells
                            rock = Lerp(rock, new Color32(18, 20, 24, 255), 0.35f * reveal);
                        }

                        // Soft long fade into black across the outer half of the wall band
                        float a = reveal;
                        float fadeStart = WallRevealCells * 0.35f;
                        if (depth > fadeStart)
                        {
                            float outer = 1f - (depth - fadeStart) / (WallRevealCells - fadeStart);
                            outer = Mathf.Clamp01(outer);
                            outer = outer * outer * (3f - 2f * outer); // smoothstep
                            a *= outer;
                        }
                        rock.a = (byte)(Mathf.Clamp01(a * 0.88f) * 255f);
                        _wallPx[i] = rock;
                    }
                }
            }

            // Chunky loose rock along dig-face floor edge (reference rubble lip)
            StampBorderRubble(cx0, cy0, cx1, cy1, tw, th, w);
        }

        /// <summary>
        /// Scatter angular rubble chips on open floor that touches solid wall —
        /// breaks the hard floor/wall seam like the reference cavern lip.
        /// </summary>
        void StampBorderRubble(int cx0, int cy0, int cx1, int cy1, int tw, int th, int texW)
        {
            int texH = th * Ppu;
            int x0 = Mathf.Max(1, cx0 - 1);
            int y0 = Mathf.Max(1, cy0 - 1);
            int x1 = Mathf.Min(tw - 2, cx1 + 1);
            int y1 = Mathf.Min(th - 2, cy1 + 1);

            for (int cy = y0; cy <= y1; cy++)
            for (int cx = x0; cx <= x1; cx++)
            {
                // Only on excavated floor that touches solid rock
                if (!_world.IsExcavated(cx, cy)) continue;
                if (_world.IsGas(cx, cy) && !_world.IsFloorOpen(cx, cy)) continue;

                int contacts = 0;
                float pullX = 0f, pullY = 0f;
                for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0) continue;
                    int nx = cx + ox, ny = cy + oy;
                    if (!_world.InBounds(nx, ny)) continue;
                    if (_world.IsExcavated(nx, ny)) continue;
                    contacts++;
                    pullX += ox;
                    pullY += oy;
                }
                if (contacts == 0) continue;

                // Density: more chips when more wall neighbors (corner heaps)
                int chips = contacts >= 3 ? 5 : (contacts == 2 ? 4 : 3);
                float invLen = 1f / Mathf.Max(0.01f, Mathf.Sqrt(pullX * pullX + pullY * pullY));
                float nxDir = pullX * invLen;
                float nyDir = pullY * invLen;

                float basePx = (cx + 0.5f) * Ppu;
                float basePy = (cy + 0.5f) * Ppu;

                for (int k = 0; k < chips; k++)
                {
                    int seed = cx * 73856093 ^ cy * 19349663 ^ (k * 83492791);
                    float u = Hash01(cx, cy, 11 + k * 3);
                    float v = Hash01(cx, cy, 29 + k * 5);
                    // Bias toward the wall, spill a little onto floor
                    float along = (u - 0.5f) * Ppu * 0.85f;
                    float intoWall = (0.15f + v * 0.55f) * Ppu; // toward solid
                    float intoFloor = (Hash01(cx, cy, 41 + k) - 0.35f) * Ppu * 0.35f;
                    float px = basePx + nxDir * (intoWall * 0.35f - intoFloor) - nyDir * along;
                    float py = basePy + nyDir * (intoWall * 0.35f - intoFloor) + nxDir * along;

                    // Size mix: large / mid / pebble (reference)
                    float sizeRoll = Hash01(cx, cy, 53 + k * 7);
                    float radius = sizeRoll > 0.72f ? Ppu * 0.42f
                        : sizeRoll > 0.35f ? Ppu * 0.28f
                        : Ppu * 0.14f;
                    radius *= 0.85f + Hash01(cx, cy, 61 + k) * 0.35f;

                    DigVisualKit.StampRubbleChip(_floorPx, texW, texH, px, py, radius, seed);
                }

                // Extra pebbles hugging the seam
                for (int k = 0; k < 2; k++)
                {
                    int seed = cx * 9973 ^ cy * 7919 ^ (k * 13 + 99);
                    float u = Hash01(cx, cy, 77 + k);
                    float px = basePx + nxDir * Ppu * 0.28f + (u - 0.5f) * Ppu * 0.7f;
                    float py = basePy + nyDir * Ppu * 0.28f + (Hash01(cx, cy, 83 + k) - 0.5f) * Ppu * 0.7f;
                    DigVisualKit.StampRubbleChip(_floorPx, texW, texH, px, py, Ppu * 0.11f, seed);
                }
            }
        }

        bool NearGold(int cx, int cy)
        {
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int x = cx + ox, y = cy + oy;
                if (!_world.InBounds(x, y)) continue;
                if (_world.Get(x, y).IsPreciousOre) return true;
            }
            return false;
        }

        void ApplyAll()
        {
            _floorTex.SetPixels32(_floorPx);
            _floorTex.Apply(false);
            _wallTex.SetPixels32(_wallPx);
            _wallTex.Apply(false);
        }

        void ApplyRect(int cx0, int cy0, int cx1, int cy1)
        {
            int w = _world.Width * Ppu;
            int px0 = cx0 * Ppu;
            int py0 = cy0 * Ppu;
            int bw = (cx1 + 1) * Ppu - px0;
            int bh = (cy1 + 1) * Ppu - py0;

            var floorBlock = new Color32[bw * bh];
            var wallBlock = new Color32[bw * bh];
            for (int y = 0; y < bh; y++)
            for (int x = 0; x < bw; x++)
            {
                int src = (py0 + y) * w + (px0 + x);
                int dst = y * bw + x;
                floorBlock[dst] = _floorPx[src];
                wallBlock[dst] = _wallPx[src];
            }
            _floorTex.SetPixels32(px0, py0, bw, bh, floorBlock);
            _wallTex.SetPixels32(px0, py0, bw, bh, wallBlock);
            _floorTex.Apply(false);
            _wallTex.Apply(false);
        }

        float SampleOpen(float tx, float ty, int tw, int th)
        {
            int cx = Mathf.FloorToInt(tx);
            int cy = Mathf.FloorToInt(ty);
            float best = -2.5f;
            // Tighter neighborhood so dig corridors read narrower
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int x = cx + ox, y = cy + oy;
                if (x < 0 || y < 0 || x >= tw || y >= th) continue;
                var s = _world.Get(x, y);
                float jx = (Hash01(x, y, 3) - 0.5f) * 0.22f;
                float jy = (Hash01(x, y, 7) - 0.5f) * 0.22f;
                float dx = tx - (x + 0.5f + jx);
                float dy = ty - (y + 0.5f + jy);
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                if (s.Phase == TerrainPhase.Excavated && _world.IsFloorOpen(x, y))
                    best = Mathf.Max(best, 0.72f + Hash01(x, y, 9) * 0.12f - d);
                else if (s.Phase == TerrainPhase.Damaged)
                {
                    float t = s.MaxDurability <= 0 ? 0.5f : s.DamageState / (float)s.MaxDurability;
                    best = Mathf.Max(best, (0.22f + t * 0.35f) - d);
                }
            }
            return best;
        }

        static Color32 StampStoneTint(Color32 floor, int px, int py, float near)
        {
            float h = Hash(px * 19, py * 23);
            Color32 stone = h < 0.4f
                ? new Color32(48, 44, 40, 255)
                : (h < 0.75f ? new Color32(62, 52, 40, 255) : new Color32(78, 64, 46, 255));
            return Lerp(floor, stone, 0.35f + near * 0.25f);
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
                255);
        }
    }
}
