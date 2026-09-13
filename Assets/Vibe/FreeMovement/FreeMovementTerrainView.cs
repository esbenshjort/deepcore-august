using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// World Depth + Environmental Storytelling V1 — builds on Graphics Overhaul V1.
    /// Point atlases retained. Adds wall volume, geological patches, excavation age,
    /// traffic wear, and camp grounding — systemic (not screenshot-only).
    /// </summary>
    public sealed class FreeMovementTerrainView : MonoBehaviour
    {
        /// <summary>Texels per cell. 8 ≈ same apparent density as Point loose-rock chips at ortho 10.</summary>
        const int Ppu = 8;
        const int PadCells = 4;
        /// <summary>How far into solid rock the wall stays visible (cell units).</summary>
        const float WallRevealCells = 3.25f;

        FineTerrainWorld _world;
        LogisticsTrafficMap _traffic;
        Vector2 _campWorld;
        float _campRadiusCells = 22f;
        bool _hasCamp;
        Texture2D _floorTex;
        Texture2D _wallTex;
        Color32[] _floorPx;
        Color32[] _wallPx;
        /// <summary>Reusable ApplyRect upload buffers — grow on demand, never shrink-alloc every call.</summary>
        Color32[] _applyFloorBlock = System.Array.Empty<Color32>();
        Color32[] _applyWallBlock = System.Array.Empty<Color32>();
        bool _pending;
        int _qx0, _qy0, _qx1, _qy1;
        float _cooldown;
        float _campRefresh;

        // Compact dirt — lower visual frequency than old noisy dither
        static readonly Color32 FloorA = new(58, 38, 22, 255);
        static readonly Color32 FloorB = new(70, 46, 26, 255);
        static readonly Color32 FloorC = new(46, 32, 18, 255);
        static readonly Color32 FloorWarm = new(82, 54, 30, 255);
        static readonly Color32 FloorTraffic = new(64, 42, 24, 255);
        static readonly Color32 FloorCamp = new(52, 36, 24, 255);
        static readonly Color32 FloorFresh = new(88, 58, 32, 255);
        static readonly Color32 FloorClay = new(92, 62, 42, 255);
        static readonly Color32 FloorDamp = new(42, 36, 30, 255);
        static readonly Color32 RockTint = new(40, 32, 26, 255);

        // Dark rock mass — lit dig face reads; outer band falls to black
        static readonly Color32 WallA = new(26, 22, 18, 255);
        static readonly Color32 WallB = new(34, 28, 24, 255);
        static readonly Color32 WallC = new(20, 18, 16, 255);
        static readonly Color32 WallGrain = new(42, 34, 28, 255);
        static readonly Color32 WallLip = new(52, 40, 30, 255);
        static readonly Color32 WallLipLit = new(72, 52, 34, 255);
        static readonly Color32 WallFace = new(30, 24, 20, 255);
        static readonly Color32 WallCrack = new(14, 12, 10, 255);
        static readonly Color32 WallContact = new(12, 10, 9, 255);
        static readonly Color32 WallHard = new(18, 20, 24, 255);
        static readonly Color32 WallClay = new(48, 34, 26, 255);
        static readonly Color32 WallDamp = new(22, 24, 26, 255);
        static readonly Color32 WallStain = new(58, 36, 22, 255);
        static readonly Color32 WallSilhouette = new(8, 7, 6, 255);

        static readonly Color32 GoldC = new(220, 160, 42, 255);
        static readonly Color32 GoldBright = new(255, 210, 70, 255);
        static readonly Color32 GoldSpeck = new(255, 235, 140, 255);
        static readonly Color32 DiaDeep = new(40, 90, 140, 255);
        static readonly Color32 DiaMid = new(120, 190, 230, 255);
        static readonly Color32 DiaHi = new(210, 240, 255, 255);
        static readonly Color32 DiaFlash = new(255, 250, 255, 255);
        static readonly Color32 DiaPink = new(220, 170, 210, 255);
        static readonly Color32 Clear = new(0, 0, 0, 0);

        enum GeoPatch : byte { Compact, Fractured, HardStone, Clay, Damp, MineralStain }

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
            MakeLayer("RockWall", _wallTex, w, h, ppuWorld, 2, lit: true);

            RebuildRect(0, 0, world.Width - 1, world.Height - 1);
            ApplyAll();
            _pending = false;
        }

        /// <summary>Wire haul traffic + camp anchor for wear / lived-in floor (presentation only).</summary>
        public void BindPresentation(LogisticsTrafficMap traffic, Vector2 campWorld, float campRadiusCells = 22f)
        {
            if (_traffic != null)
                _traffic.CellVisited -= OnTrafficVisit;
            _traffic = traffic;
            if (_traffic != null)
                _traffic.CellVisited += OnTrafficVisit;
            _campWorld = campWorld;
            _campRadiusCells = Mathf.Max(4f, campRadiusCells);
            _hasCamp = true;
            // Initial camp wear pass
            DirtyCampBand();
        }

        void OnTrafficVisit(int x, int y) => OnRegion(x, y, x, y);

        void DirtyCampBand()
        {
            if (!_hasCamp || _world == null) return;
            var c = _world.WorldToCell(_campWorld);
            int r = Mathf.CeilToInt(_campRadiusCells);
            OnRegion(c.x - r, c.y - r, c.x + r, c.y + r);
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

        /// <summary>Point filter — matches machinery / loose-rock fidelity (not soft Bilinear mush).</summary>
        static Texture2D MakeTex(int w, int h) => new(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        void OnDestroy()
        {
            if (_world != null) _world.RegionChanged -= OnRegion;
            if (_traffic != null) _traffic.CellVisited -= OnTrafficVisit;
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
            // Slow camp re-paint so accumulated traffic wear stays visible without per-frame cost
            if (_hasCamp && _traffic != null)
            {
                _campRefresh -= Time.deltaTime;
                if (_campRefresh <= 0f)
                {
                    _campRefresh = 5.5f;
                    DirtyCampBand();
                }
            }

            if (!_pending) return;
            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f) return;
            _cooldown = 0.1f;

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
                    // Controlled irregularity — not soft airbrush mush
                    field += 0.08f * Mathf.PerlinNoise(tx * 0.7f + 2.1f, ty * 0.7f) - 0.04f;
                    field += 0.035f * Mathf.PerlinNoise(tx * 2.1f, ty * 2.1f) - 0.017f;

                    int scx = Mathf.Clamp(Mathf.FloorToInt(tx), 0, tw - 1);
                    int scy = Mathf.Clamp(Mathf.FloorToInt(ty), 0, th - 1);
                    if (_world.IsGas(scx, scy) && !_world.IsFloorOpen(scx, scy) && field > 0f)
                        field = -0.4f;

                    _wallPx[i] = Clear;

                    if (field > 0f)
                        _floorPx[i] = PaintFloor(px, py, tx, ty, field, tw, th);
                    else
                    {
                        _floorPx[i] = Clear;
                        _wallPx[i] = PaintWall(px, py, tx, ty, field, tw, th);
                    }
                }
            }

            StampBorderRubble(cx0, cy0, cx1, cy1, tw, th, w);
            StampWallLipChips(cx0, cy0, cx1, cy1, tw, th, w);
            StampLargeFormations(cx0, cy0, cx1, cy1, tw, th, w);
        }

        static GeoPatch SampleGeo(float tx, float ty)
        {
            // Large coherent patches — not per-tile noise
            float a = Mathf.PerlinNoise(tx * 0.032f + 1.7f, ty * 0.032f + 3.1f);
            float b = Mathf.PerlinNoise(tx * 0.07f + 9.2f, ty * 0.07f);
            float fault = Mathf.PerlinNoise(tx * 0.055f + 20f, ty * 0.12f);
            if (a < 0.20f) return GeoPatch.HardStone;
            if (a > 0.82f && b > 0.52f) return GeoPatch.Clay;
            if (b < 0.24f) return GeoPatch.Damp;
            if (Mathf.Abs(fault - 0.5f) < 0.045f && a > 0.4f) return GeoPatch.MineralStain;
            if (a > 0.52f && a < 0.72f && b > 0.45f) return GeoPatch.Fractured;
            return GeoPatch.Compact;
        }

        Color32 PaintFloor(int px, int py, float tx, float ty, float field, int tw, int th)
        {
            float macro = Mathf.PerlinNoise(tx * 0.45f, ty * 0.45f);
            float grain = Mathf.PerlinNoise(tx * 1.15f + 4f, ty * 1.15f);
            float n = Hash(px, py);
            var geo = SampleGeo(tx, ty);

            var col = macro < 0.33f ? FloorA : (macro < 0.68f ? FloorB : FloorC);
            if (grain > 0.78f) col = Lerp(col, FloorWarm, 0.28f);
            if (n > 0.94f) col = FloorC;

            // Geological floor patches
            switch (geo)
            {
                case GeoPatch.Clay:
                    col = Lerp(col, FloorClay, 0.38f);
                    break;
                case GeoPatch.Damp:
                    col = Lerp(col, FloorDamp, 0.32f);
                    if (grain > 0.7f) col = Lerp(col, FloorDamp, 0.2f);
                    break;
                case GeoPatch.HardStone:
                    col = Lerp(col, RockTint, 0.22f);
                    break;
                case GeoPatch.Fractured:
                    if (Hash(px * 3, py * 5) > 0.88f)
                        col = StampStoneTint(col, px, py, 0.55f);
                    break;
                case GeoPatch.MineralStain:
                    col = Lerp(col, FloorWarm, 0.18f);
                    break;
            }

            int fcx = Mathf.Clamp(Mathf.FloorToInt(tx), 0, tw - 1);
            int fcy = Mathf.Clamp(Mathf.FloorToInt(ty), 0, th - 1);

            // —— Excavation history ——
            float age = _world.ExcavationAge01(fcx, fcy); // 0 fresh · 1 settled
            float fresh = 1f - age;
            if (fresh > 0.55f)
            {
                float f = (fresh - 0.55f) / 0.45f;
                col = Lerp(col, FloorFresh, f * 0.28f);
                if (field < 0.55f && Hash(px + 3, py + 9) < f * 0.22f)
                    col = StampStoneTint(col, px, py, f);
            }
            else if (age > 0.55f)
            {
                float o = (age - 0.55f) / 0.45f;
                col = Lerp(col, FloorC, o * 0.3f); // compacted settled tunnel
            }

            // Hauler traffic wear
            float traffic = _traffic != null ? _traffic.Intensity01(fcx, fcy, 28f) : 0f;
            if (traffic > 0.08f)
                col = Lerp(col, FloorTraffic, traffic * 0.5f);
            if (traffic > 0.35f && grain > 0.48f && grain < 0.58f)
                col = Lerp(col, FloorC, 0.18f); // path compaction strip

            // Camp lived-in apron
            if (_hasCamp)
            {
                float camp = CampInfluence01(fcx, fcy);
                if (camp > 0.05f)
                {
                    col = Lerp(col, FloorCamp, camp * 0.42f);
                    if (Hash(px * 7, py * 11) > 0.92f - camp * 0.15f)
                        col = Lerp(col, RockTint, 0.2f); // grime flecks
                }
            }

            // Disturbed band near wall — rock tint + embedded fragments
            if (field < 0.48f)
            {
                float t = 1f - field / 0.48f;
                t = t * t;
                float disturb = t * (0.22f + fresh * 0.18f);
                col = Lerp(col, RockTint, disturb);
                if (Hash(px + 17, py + 31) < t * (0.12f + fresh * 0.14f))
                    col = StampStoneTint(col, px, py, t);
            }
            else if (field > 0.55f && grain > 0.55f && grain < 0.62f && traffic < 0.2f)
                col = Lerp(col, FloorTraffic, 0.14f);

            if (field < 0.7f && NearGold(fcx, fcy))
            {
                float g = (0.7f - field) * 0.85f;
                if (Hash(px * 5, py * 11) > 0.55f)
                    col = Lerp(col, GoldC, g * 0.5f);
                if (Hash(px * 9, py * 3) > 0.82f)
                    col = Lerp(col, GoldBright, g * 0.35f);
            }

            float a = field > 0.07f ? 1f : Mathf.Clamp01(field / 0.07f);
            col.a = (byte)(a * 255f);
            return col;
        }

        float CampInfluence01(int cx, int cy)
        {
            if (!_hasCamp) return 0f;
            Vector2 p = _world.CellCenter(cx, cy);
            float dCells = Vector2.Distance(p, _campWorld) / Mathf.Max(0.01f, _world.CellSize);
            return Mathf.Clamp01(1f - dCells / _campRadiusCells);
        }

        Color32 PaintWall(int px, int py, float tx, float ty, float field, int tw, int th)
        {
            float depth = Mathf.Abs(field);
            if (depth > WallRevealCells) return Clear;

            float reveal = 1f - depth / WallRevealCells;
            reveal = reveal * reveal * (3f - 2f * reveal);

            int cx = Mathf.Clamp(Mathf.FloorToInt(tx), 0, tw - 1);
            int cy = Mathf.Clamp(Mathf.FloorToInt(ty), 0, th - 1);
            var cell = _world.Get(cx, cy);
            bool hideGas = _world.IsGas(cx, cy) && !_world.IsFloorOpen(cx, cy);
            var geo = SampleGeo(tx, ty);

            float n = Mathf.PerlinNoise(tx * 0.42f + 8f, ty * 0.42f);
            float strata = Mathf.PerlinNoise(tx * 0.14f, ty * 0.55f);
            float crackN = Mathf.PerlinNoise(tx * 3.4f + 1.7f, ty * 3.4f);
            float shelf = Mathf.PerlinNoise(tx * 0.9f + 4f, ty * 2.4f);

            Color32 rock = n < 0.35f ? WallA : (n < 0.7f ? WallB : WallC);
            rock = Lerp(rock, WallGrain, strata * 0.32f);
            if (Hash(px * 3, py * 7) > 0.78f)
                rock = Lerp(rock, WallGrain, 0.28f);

            // Geological wall patches
            switch (geo)
            {
                case GeoPatch.HardStone:
                    rock = Lerp(rock, WallHard, 0.45f);
                    break;
                case GeoPatch.Clay:
                    rock = Lerp(rock, WallClay, 0.4f);
                    break;
                case GeoPatch.Damp:
                    rock = Lerp(rock, WallDamp, 0.35f);
                    break;
                case GeoPatch.MineralStain:
                    rock = Lerp(rock, WallStain, 0.28f);
                    break;
                case GeoPatch.Fractured:
                    if (crackN > 0.58f && crackN < 0.72f)
                        rock = Lerp(rock, WallCrack, 0.45f);
                    break;
            }

            // —— Physical depth: lip → face → contact → mass → silhouette ——
            if (depth < 0.32f)
            {
                // Fractured upper lip / overhang feel
                float lip = 1f - depth / 0.32f;
                float facet = Hash(px * 11, py * 13);
                rock = Lerp(rock, WallLip, lip * 0.58f);
                if (facet > 0.7f)
                    rock = Lerp(rock, WallLipLit, lip * 0.48f);
                if (crackN > 0.62f && crackN < 0.69f)
                    rock = Lerp(rock, WallCrack, lip * 0.75f);
                if (Hash(px * 19, py * 23) > 0.9f)
                    rock = Lerp(rock, WallLipLit, lip * 0.55f);
                // Occasional shelf ledge along dig face
                if (shelf > 0.72f && lip > 0.4f)
                    rock = Lerp(rock, WallLipLit, 0.25f);
            }
            else if (depth < 0.85f)
            {
                // Visible darker wall face (volume, not flat cutout)
                float face = 1f - (depth - 0.32f) / 0.53f;
                rock = Lerp(rock, WallFace, face * 0.55f);
                rock = Lerp(rock, WallContact, face * 0.22f);
                if (crackN > 0.68f && Hash(px, py) > 0.5f)
                    rock = Lerp(rock, WallCrack, face * 0.4f);
                if (strata > 0.62f && strata < 0.68f)
                    rock = Lerp(rock, WallGrain, face * 0.35f); // strata band
            }
            else if (depth < 1.55f)
            {
                // Strong contact / occlusion zone
                float c = 1f - (depth - 0.85f) / 0.7f;
                rock = Lerp(rock, WallContact, 0.35f + c * 0.5f);
                if (crackN > 0.75f)
                    rock = Lerp(rock, WallCrack, c * 0.3f);
            }
            else
            {
                // Outer silhouette — rock disappearing into darkness
                rock = Lerp(rock, WallSilhouette, 0.55f);
            }

            // Fresh excavation scars on near-face damaged / just-opened neighbors
            float ageNear = NeighborFreshness01(cx, cy);
            if (depth < 0.7f && ageNear > 0.55f)
                rock = Lerp(rock, WallLipLit, (ageNear - 0.55f) * 0.22f);

            if (!hideGas && cell.Phase == TerrainPhase.Damaged)
            {
                float d = cell.MaxDurability <= 0 ? 1f : cell.DamageState / (float)cell.MaxDurability;
                rock = Lerp(rock, new Color32(80, 52, 30, 255), d * 0.4f * reveal);
            }

            int goldN = hideGas ? 0 : cell.GoldCount;
            if (goldN > 0)
            {
                float t = goldN / 4f;
                float g = (0.12f + t * 0.88f) * reveal;
                rock = Lerp(rock, GoldC, g);
                if (Hash(px * 5, py * 11) > 0.72f - t * 0.35f)
                    rock = Lerp(rock, GoldBright, (0.25f + t * 0.7f) * reveal);
                if (goldN >= 3 && Hash(px * 9, py * 3) > 0.78f)
                    rock = Lerp(rock, GoldSpeck, 0.45f * reveal);
                if (goldN >= 4 && Hash(px * 13, py * 17) > 0.9f)
                    rock = Lerp(rock, GoldSpeck, 0.5f * reveal);
            }

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
                rock = Lerp(rock, WallHard, 0.4f * reveal);
            }

            // Outer fade — dig-face opaque; silhouette holds faint rock into darkness
            float a = reveal;
            float fadeStart = WallRevealCells * 0.48f;
            if (depth > fadeStart)
            {
                float outer = 1f - (depth - fadeStart) / (WallRevealCells - fadeStart);
                outer = Mathf.Clamp01(outer);
                outer = outer * outer;
                // Keep a whisper of silhouette instead of hard map-end
                a *= Mathf.Max(outer, depth < WallRevealCells * 0.92f ? 0.12f : 0f);
            }
            if (depth < 0.28f)
                a = Mathf.Max(a, 0.94f);

            rock.a = (byte)(Mathf.Clamp01(a * 0.96f) * 255f);
            return rock;
        }

        /// <summary>Max freshness (1-age) among open neighbors — scars on dig face.</summary>
        float NeighborFreshness01(int cx, int cy)
        {
            float best = 0f;
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                int nx = cx + ox, ny = cy + oy;
                if (!_world.InBounds(nx, ny) || !_world.IsExcavated(nx, ny)) continue;
                best = Mathf.Max(best, 1f - _world.ExcavationAge01(nx, ny));
            }
            return best;
        }

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

                float age = _world.ExcavationAge01(cx, cy);
                float fresh = 1f - age;
                float traffic = _traffic != null ? _traffic.Intensity01(cx, cy, 28f) : 0f;
                // Fresh dig: more rubble. Settled / high-traffic: less loose debris.
                float chipMul = Mathf.Lerp(0.45f, 1.15f, fresh) * (1f - traffic * 0.55f);
                int chips = Mathf.Max(1, Mathf.RoundToInt((contacts >= 3 ? 5 : (contacts == 2 ? 4 : 3)) * chipMul));

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
                    float along = (u - 0.5f) * Ppu * 0.85f;
                    float intoWall = (0.15f + v * 0.55f) * Ppu;
                    float intoFloor = (Hash01(cx, cy, 41 + k) - 0.35f) * Ppu * 0.35f;
                    float px = basePx + nxDir * (intoWall * 0.35f - intoFloor) - nyDir * along;
                    float py = basePy + nyDir * (intoWall * 0.35f - intoFloor) + nxDir * along;

                    float sizeRoll = Hash01(cx, cy, 53 + k * 7);
                    float radius = sizeRoll > 0.72f ? Ppu * 0.38f
                        : sizeRoll > 0.35f ? Ppu * 0.24f
                        : Ppu * 0.12f;
                    radius *= 0.85f + Hash01(cx, cy, 61 + k) * 0.35f;

                    DigVisualKit.StampRubbleChip(_floorPx, texW, texH, px, py, radius, seed);
                }

                int micro = fresh > 0.6f ? 2 : (traffic > 0.4f ? 0 : 1);
                for (int k = 0; k < micro; k++)
                {
                    int seed = cx * 9973 ^ cy * 7919 ^ (k * 13 + 99);
                    float u = Hash01(cx, cy, 77 + k);
                    float px = basePx + nxDir * Ppu * 0.28f + (u - 0.5f) * Ppu * 0.7f;
                    float py = basePy + nyDir * Ppu * 0.28f + (Hash01(cx, cy, 83 + k) - 0.5f) * Ppu * 0.7f;
                    DigVisualKit.StampRubbleChip(_floorPx, texW, texH, px, py, Ppu * 0.1f, seed);
                }
            }
        }

        void StampWallLipChips(int cx0, int cy0, int cx1, int cy1, int tw, int th, int texW)
        {
            int texH = th * Ppu;
            int x0 = Mathf.Max(1, cx0 - 1);
            int y0 = Mathf.Max(1, cy0 - 1);
            int x1 = Mathf.Min(tw - 2, cx1 + 1);
            int y1 = Mathf.Min(th - 2, cy1 + 1);

            for (int cy = y0; cy <= y1; cy++)
            for (int cx = x0; cx <= x1; cx++)
            {
                if (_world.IsExcavated(cx, cy)) continue;
                if (_world.IsGas(cx, cy) && !_world.IsFloorOpen(cx, cy)) continue;

                bool touchesOpen = false;
                float pullX = 0f, pullY = 0f;
                for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0) continue;
                    int nx = cx + ox, ny = cy + oy;
                    if (!_world.InBounds(nx, ny)) continue;
                    if (!_world.IsExcavated(nx, ny)) continue;
                    touchesOpen = true;
                    pullX -= ox;
                    pullY -= oy;
                }
                if (!touchesOpen) continue;
                if (Hash01(cx, cy, 101) < 0.55f) continue;

                float invLen = 1f / Mathf.Max(0.01f, Mathf.Sqrt(pullX * pullX + pullY * pullY));
                float nxDir = pullX * invLen;
                float nyDir = pullY * invLen;
                float basePx = (cx + 0.5f) * Ppu + nxDir * Ppu * 0.15f;
                float basePy = (cy + 0.5f) * Ppu + nyDir * Ppu * 0.15f;
                int seed = cx * 9127 ^ cy * 3301;
                float r = Ppu * (0.14f + Hash01(cx, cy, 111) * 0.16f);
                DigVisualKit.StampRubbleChip(_wallPx, texW, texH, basePx, basePy, r, seed);
            }
        }

        /// <summary>
        /// Sparse LARGE geological forms — shelves, fault cracks, embedded slabs, floor boulders.
        /// Improves composition without decorating every cell.
        /// </summary>
        void StampLargeFormations(int cx0, int cy0, int cx1, int cy1, int tw, int th, int texW)
        {
            int texH = th * Ppu;
            int x0 = Mathf.Max(1, cx0);
            int y0 = Mathf.Max(1, cy0);
            int x1 = Mathf.Min(tw - 2, cx1);
            int y1 = Mathf.Min(th - 2, cy1);

            for (int cy = y0; cy <= y1; cy++)
            for (int cx = x0; cx <= x1; cx++)
            {
                float landmark = Hash01(cx, cy, 404);
                if (landmark < 0.965f) continue;

                bool open = _world.IsExcavated(cx, cy);
                float basePx = (cx + 0.5f) * Ppu;
                float basePy = (cy + 0.5f) * Ppu;
                int seed = (int)((uint)cx * 2654435761u ^ (uint)cy * 2246822519u);

                if (!open)
                {
                    // Embedded slab / fault on wall mass near tunnels only
                    if (!TouchesOpen(cx, cy)) continue;
                    float len = Ppu * (0.55f + Hash01(cx, cy, 501) * 0.7f);
                    float ang = Hash01(cx, cy, 502) * Mathf.PI;
                    int steps = Mathf.Max(3, Mathf.RoundToInt(len / (Ppu * 0.12f)));
                    for (int s = 0; s < steps; s++)
                    {
                        float t = s / (float)(steps - 1);
                        float px = basePx + Mathf.Cos(ang) * (t - 0.5f) * len;
                        float py = basePy + Mathf.Sin(ang) * (t - 0.5f) * len * 0.55f;
                        float r = Ppu * (0.1f + (1f - Mathf.Abs(t - 0.5f) * 2f) * 0.12f);
                        DigVisualKit.StampRubbleChip(_wallPx, texW, texH, px, py, r, seed + s);
                    }
                }
                else
                {
                    // Occasional floor boulder / shelf fragment (not in heavy traffic)
                    float traffic = _traffic != null ? _traffic.Intensity01(cx, cy, 28f) : 0f;
                    if (traffic > 0.45f) continue;
                    if (CampInfluence01(cx, cy) > 0.55f && landmark < 0.99f) continue;
                    float r = Ppu * (0.28f + Hash01(cx, cy, 511) * 0.22f);
                    DigVisualKit.StampRubbleChip(_floorPx, texW, texH, basePx, basePy, r, seed);
                    DigVisualKit.StampRubbleChip(_floorPx, texW, texH,
                        basePx + Ppu * 0.2f, basePy - Ppu * 0.1f, r * 0.55f, seed + 3);
                }
            }
        }

        bool TouchesOpen(int cx, int cy)
        {
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                int nx = cx + ox, ny = cy + oy;
                if (_world.InBounds(nx, ny) && _world.IsExcavated(nx, ny)) return true;
            }
            return false;
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
            int need = bw * bh;
            EnsureApplyBlocks(need);

            var floorBlock = _applyFloorBlock;
            var wallBlock = _applyWallBlock;
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

        void EnsureApplyBlocks(int need)
        {
            if (_applyFloorBlock.Length < need)
                _applyFloorBlock = new Color32[need];
            if (_applyWallBlock.Length < need)
                _applyWallBlock = new Color32[need];
        }

        float SampleOpen(float tx, float ty, int tw, int th)
        {
            int cx = Mathf.FloorToInt(tx);
            int cy = Mathf.FloorToInt(ty);
            float best = -2.5f;
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int x = cx + ox, y = cy + oy;
                if (x < 0 || y < 0 || x >= tw || y >= th) continue;
                var s = _world.Get(x, y);
                float jx = (Hash01(x, y, 3) - 0.5f) * 0.12f;
                float jy = (Hash01(x, y, 7) - 0.5f) * 0.12f;
                // Slight overhang bias — dig-face lip reads above floor
                jy += (Hash01(x, y, 11) - 0.35f) * 0.04f;
                float dx = tx - (x + 0.5f + jx);
                float dy = ty - (y + 0.5f + jy);
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                if (s.Phase == TerrainPhase.Excavated && _world.IsFloorOpen(x, y))
                    best = Mathf.Max(best, 0.68f + Hash01(x, y, 9) * 0.1f - d);
                else if (s.Phase == TerrainPhase.Damaged)
                {
                    float t = s.MaxDurability <= 0 ? 0.5f : s.DamageState / (float)s.MaxDurability;
                    best = Mathf.Max(best, (0.2f + t * 0.32f) - d);
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
