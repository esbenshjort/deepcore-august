using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Industrial basecamp art — washer, stockpile pads, tent camp, firepit.
    /// Same orange / gunmetal / weathering language as <see cref="CrewVisualKit"/>.
    /// </summary>
    public static class YardVisualKit
    {
        static readonly Color Orange = new(0.92f, 0.48f, 0.12f);
        static readonly Color OrangeHi = new(1f, 0.62f, 0.22f);
        static readonly Color OrangeDeep = new(0.62f, 0.28f, 0.08f);
        static readonly Color Rust = new(0.45f, 0.22f, 0.1f);
        static readonly Color Suit = new(0.42f, 0.34f, 0.26f);
        static readonly Color Black = new(0.06f, 0.06f, 0.07f);
        static readonly Color Charcoal = new(0.14f, 0.15f, 0.16f);
        static readonly Color Metal = new(0.48f, 0.5f, 0.54f);
        static readonly Color MetalHi = new(0.72f, 0.74f, 0.78f);
        static readonly Color MetalDeep = new(0.28f, 0.3f, 0.33f);
        static readonly Color HazardY = new(0.95f, 0.82f, 0.12f);
        static readonly Color Outline = new(0.02f, 0.02f, 0.03f);
        static readonly Color Green = new(0.3f, 0.95f, 0.4f);
        static readonly Color GreenCore = new(0.75f, 1f, 0.8f);
        static readonly Color Cyan = new(0.3f, 0.85f, 0.95f);
        static readonly Color CyanDeep = new(0.12f, 0.35f, 0.42f);
        static readonly Color Gold = new(0.95f, 0.72f, 0.18f);
        static readonly Color GoldHi = new(1f, 0.9f, 0.4f);
        static readonly Color Canvas = new(0.48f, 0.36f, 0.2f);
        static readonly Color CanvasDeep = new(0.28f, 0.2f, 0.1f);
        static readonly Color CanvasHi = new(0.62f, 0.46f, 0.26f);
        static readonly Color Ember = new(1f, 0.4f, 0.08f);
        static readonly Color FlameMid = new(1f, 0.7f, 0.2f);
        static readonly Color FlameCore = new(1f, 0.95f, 0.55f);

        static Sprite _yardPlatform;
        static Sprite _washerBody;
        static Sprite _washerHood;
        static Sprite _washerDrum;
        static Sprite _sleepPad;
        static Sprite _tent;
        static Sprite _tentFlap;
        static Sprite _bedrolls;
        static Sprite _firePit;
        static Sprite _flame;
        static Sprite _crate;
        static Sprite _aircon;
        static Sprite _powerCable;
        static Sprite _powerBox;
        static Sprite _padRock, _padGold, _padRefined, _padDirt, _padDiamond, _padRefinedDia;
        static Sprite _labelRock, _labelGold, _labelRefined, _labelDirt, _labelDiamond, _labelRefinedDia;

        // ——— Draw helpers ———

        static void Clear(Texture2D tex, int w, int h)
        {
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, Color.clear);
        }

        static void Dot(Texture2D tex, int w, int h, int x, int y, Color c)
        {
            if ((uint)x < w && (uint)y < h) tex.SetPixel(x, y, c);
        }

        static void Box(Texture2D tex, int w, int h, int x0, int y0, int bw, int bh, Color c)
        {
            for (int y = y0; y < y0 + bh; y++)
            for (int x = x0; x < x0 + bw; x++)
                Dot(tex, w, h, x, y, c);
        }

        static void Disc(Texture2D tex, int w, int h, float cx, float cy, float rx, float ry, Color fill, Color? edge = null)
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
                if (edge.HasValue && d > 0.78f) Dot(tex, w, h, x, y, edge.Value);
                else Dot(tex, w, h, x, y, fill);
            }
        }

        static void Weather(Texture2D tex, int w, int h, int x0, int y0, int bw, int bh, int seed)
        {
            for (int y = y0; y < y0 + bh; y++)
            for (int x = x0; x < x0 + bw; x++)
            {
                if ((uint)x >= w || (uint)y >= h) continue;
                var c = tex.GetPixel(x, y);
                if (c.a < 0.1f) continue;
                float n = Mathf.PerlinNoise(x * 0.38f + seed, y * 0.38f);
                if (n > 0.74f) tex.SetPixel(x, y, Color.Lerp(c, Rust, 0.38f));
                else if (n < 0.2f) tex.SetPixel(x, y, Color.Lerp(c, Black, 0.25f));
                else if (n > 0.55f && n < 0.58f) tex.SetPixel(x, y, Color.Lerp(c, MetalHi, 0.3f));
            }
        }

        static void HazardDiag(Texture2D tex, int w, int h, int x0, int y0, int bw, int bh)
        {
            for (int y = y0; y < y0 + bh; y++)
            for (int x = x0; x < x0 + bw; x++)
            {
                bool dark = ((x + y) / 3) % 2 == 0;
                Dot(tex, w, h, x, y, dark ? Black : HazardY);
            }
        }

        static void Bolt(Texture2D tex, int w, int h, int x, int y)
        {
            Box(tex, w, h, x - 1, y - 1, 3, 3, Charcoal);
            Dot(tex, w, h, x, y, MetalHi);
        }

        // ——— Yard platform (washer cluster only) ———

        public static Sprite YardPlatform
        {
            get
            {
                if (_yardPlatform != null) return _yardPlatform;
                const int s = 96;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                Clear(tex, s, s);

                // Squared industrial slab with chamfered corners
                for (int y = 8; y < 88; y++)
                for (int x = 10; x < 86; x++)
                {
                    bool chamfer =
                        (x < 16 && y < 14) || (x > 79 && y < 14) ||
                        (x < 16 && y > 81) || (x > 79 && y > 81);
                    if (chamfer) continue;
                    bool rim = x < 13 || x > 82 || y < 11 || y > 84;
                    Color c = rim ? Charcoal : MetalDeep;
                    float n = Mathf.PerlinNoise(x * 0.12f, y * 0.12f);
                    c = Color.Lerp(c, Suit, n * 0.15f);
                    tex.SetPixel(x, y, new Color(c.r, c.g, c.b, 0.94f));
                }
                // Panel seams
                Box(tex, s, s, 18, 20, 1, 56, Charcoal);
                Box(tex, s, s, 48, 18, 1, 60, Black);
                Box(tex, s, s, 76, 20, 1, 56, Charcoal);
                Box(tex, s, s, 16, 48, 64, 1, Charcoal);
                // Hazard corners
                HazardDiag(tex, s, s, 12, 12, 10, 5);
                HazardDiag(tex, s, s, 74, 12, 10, 5);
                HazardDiag(tex, s, s, 12, 79, 10, 5);
                HazardDiag(tex, s, s, 74, 79, 10, 5);
                // Center wash mount ring
                Disc(tex, s, s, 48, 46, 14, 12, Charcoal, Outline);
                Disc(tex, s, s, 48, 46, 11, 9, MetalDeep, null);
                Disc(tex, s, s, 48, 46, 7, 5.5f, Black, null);
                Bolt(tex, s, s, 38, 38);
                Bolt(tex, s, s, 58, 38);
                Bolt(tex, s, s, 38, 54);
                Bolt(tex, s, s, 58, 54);
                Weather(tex, s, s, 10, 8, 76, 80, 5);

                tex.Apply();
                _yardPlatform = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.45f), s);
                return _yardPlatform;
            }
        }

        // ——— Washer ———

        public static Sprite WasherBody
        {
            get
            {
                if (_washerBody != null) return _washerBody;
                const int s = 80;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                Clear(tex, s, s);

                // Main chassis
                Box(tex, s, s, 8, 10, 64, 52, Outline);
                Box(tex, s, s, 9, 11, 62, 50, Charcoal);
                Box(tex, s, s, 12, 14, 56, 44, MetalDeep);
                Box(tex, s, s, 16, 18, 48, 36, Black);
                // Orange shoulder rails
                Box(tex, s, s, 8, 10, 64, 5, OrangeDeep);
                Box(tex, s, s, 9, 11, 62, 3, Orange);
                Box(tex, s, s, 8, 57, 64, 5, OrangeDeep);
                Box(tex, s, s, 9, 58, 62, 3, Orange);
                // Side panels
                Box(tex, s, s, 8, 18, 6, 36, Metal);
                Box(tex, s, s, 66, 18, 6, 36, Metal);
                // Intake mouth (top)
                Box(tex, s, s, 28, 52, 24, 8, Outline);
                Box(tex, s, s, 29, 53, 22, 6, Charcoal);
                Box(tex, s, s, 32, 54, 16, 4, CyanDeep);
                Dot(tex, s, s, 40, 56, Cyan);
                // Left chute → dirt
                Box(tex, s, s, 4, 8, 14, 12, Outline);
                Box(tex, s, s, 5, 9, 12, 10, Suit);
                Box(tex, s, s, 7, 11, 8, 6, MetalDeep);
                HazardDiag(tex, s, s, 5, 9, 12, 3);
                // Right chute → refined gold
                Box(tex, s, s, 62, 8, 14, 12, Outline);
                Box(tex, s, s, 63, 9, 12, 10, OrangeDeep);
                Box(tex, s, s, 65, 11, 8, 6, Gold);
                Dot(tex, s, s, 68, 13, GoldHi);
                // Control strip / lamps
                Box(tex, s, s, 20, 12, 40, 5, Charcoal);
                for (int i = 0; i < 4; i++)
                {
                    int lx = 24 + i * 9;
                    Box(tex, s, s, lx, 13, 4, 3, Black);
                    Dot(tex, s, s, lx + 1, 14, i == 1 ? Green : MetalDeep);
                    if (i == 1) Dot(tex, s, s, lx + 1, 14, GreenCore);
                }
                // Rivets
                Bolt(tex, s, s, 14, 20);
                Bolt(tex, s, s, 66, 20);
                Bolt(tex, s, s, 14, 50);
                Bolt(tex, s, s, 66, 50);
                Weather(tex, s, s, 8, 10, 64, 52, 9);

                tex.Apply();
                _washerBody = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.35f), s);
                return _washerBody;
            }
        }

        public static Sprite WasherHood
        {
            get
            {
                if (_washerHood != null) return _washerHood;
                const int s = 40;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                Clear(tex, s, s);
                // Domed metal hood with cyan viewing slit
                Disc(tex, s, s, 20, 14, 16, 12, Outline, null);
                Disc(tex, s, s, 20, 14, 14.5f, 10.5f, MetalDeep, Outline);
                Disc(tex, s, s, 20, 15, 11, 8, Charcoal, null);
                Disc(tex, s, s, 20, 16, 8, 5.5f, CyanDeep, null);
                Disc(tex, s, s, 20, 16, 5, 3.5f, Cyan, null);
                Dot(tex, s, s, 20, 16, new Color(0.7f, 0.95f, 1f));
                // Hinge bar
                Box(tex, s, s, 6, 22, 28, 4, Charcoal);
                Box(tex, s, s, 8, 23, 24, 2, Metal);
                Bolt(tex, s, s, 10, 24);
                Bolt(tex, s, s, 30, 24);
                Weather(tex, s, s, 4, 4, 32, 24, 4);
                tex.Apply();
                _washerHood = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.25f), s);
                return _washerHood;
            }
        }

        public static Sprite WasherDrum
        {
            get
            {
                if (_washerDrum != null) return _washerDrum;
                const int s = 40;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                Clear(tex, s, s);
                Disc(tex, s, s, 20, 20, 16, 16, Outline, null);
                Disc(tex, s, s, 20, 20, 14.5f, 14.5f, MetalDeep, Outline);
                Disc(tex, s, s, 20, 20, 11, 11, Charcoal, null);
                Disc(tex, s, s, 20, 20, 6, 6, Metal, null);
                // Spokes
                for (int a = 0; a < 8; a++)
                {
                    float ang = a * Mathf.PI * 0.25f;
                    for (int r = 4; r < 13; r++)
                    {
                        int x = Mathf.RoundToInt(20 + Mathf.Cos(ang) * r);
                        int y = Mathf.RoundToInt(20 + Mathf.Sin(ang) * r);
                        Dot(tex, s, s, x, y, a % 2 == 0 ? OrangeDeep : MetalHi);
                    }
                }
                Disc(tex, s, s, 20, 20, 3, 3, Orange, null);
                Dot(tex, s, s, 20, 20, OrangeHi);
                Weather(tex, s, s, 4, 4, 32, 32, 6);
                tex.Apply();
                _washerDrum = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
                return _washerDrum;
            }
        }

        // ——— Stockpiles ———

        public static Sprite StockpilePad(StockpileKind kind)
        {
            switch (kind)
            {
                case StockpileKind.Gold:
                    if (_padGold != null) return _padGold;
                    return _padGold = MakeStockPad(new Color(0.55f, 0.4f, 0.1f), Gold);
                case StockpileKind.RefinedGold:
                    if (_padRefined != null) return _padRefined;
                    return _padRefined = MakeStockPad(new Color(0.6f, 0.48f, 0.12f), GoldHi);
                case StockpileKind.Diamond:
                    if (_padDiamond != null) return _padDiamond;
                    return _padDiamond = MakeStockPad(new Color(0.12f, 0.28f, 0.42f), Cyan);
                case StockpileKind.RefinedDiamond:
                    if (_padRefinedDia != null) return _padRefinedDia;
                    return _padRefinedDia = MakeStockPad(new Color(0.18f, 0.35f, 0.5f),
                        new Color(0.75f, 0.92f, 1f));
                case StockpileKind.Dirt:
                    if (_padDirt != null) return _padDirt;
                    return _padDirt = MakeStockPad(new Color(0.28f, 0.24f, 0.18f), Suit);
                default:
                    if (_padRock != null) return _padRock;
                    return _padRock = MakeStockPad(new Color(0.3f, 0.28f, 0.26f), Metal);
            }
        }

        static Sprite MakeStockPad(Color bed, Color accent)
        {
            const int s = 56;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Clear(tex, s, s);
            // Raised square metal bay
            Box(tex, s, s, 4, 6, 48, 42, Outline);
            Box(tex, s, s, 5, 7, 46, 40, Charcoal);
            Box(tex, s, s, 8, 10, 40, 34, bed);
            Box(tex, s, s, 10, 12, 36, 30, Color.Lerp(bed, Black, 0.35f));
            // Grate lines
            for (int i = 0; i < 5; i++)
                Box(tex, s, s, 12, 14 + i * 6, 32, 1, Charcoal);
            // Accent stripe
            Box(tex, s, s, 8, 40, 40, 4, accent);
            Box(tex, s, s, 10, 41, 36, 2, Color.Lerp(accent, Whiteish(accent), 0.35f));
            HazardDiag(tex, s, s, 5, 7, 10, 4);
            HazardDiag(tex, s, s, 41, 7, 10, 4);
            Bolt(tex, s, s, 8, 10);
            Bolt(tex, s, s, 48, 10);
            Bolt(tex, s, s, 8, 42);
            Bolt(tex, s, s, 48, 42);
            Weather(tex, s, s, 4, 6, 48, 42, 8);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.45f), s);
        }

        static Color Whiteish(Color c) => Color.Lerp(c, Color.white, 0.45f);

        public static Sprite StockpileLabel(StockpileKind kind)
        {
            switch (kind)
            {
                case StockpileKind.Gold:
                    if (_labelGold != null) return _labelGold;
                    return _labelGold = MakeLabelPlaque(Gold);
                case StockpileKind.RefinedGold:
                    if (_labelRefined != null) return _labelRefined;
                    return _labelRefined = MakeLabelPlaque(GoldHi);
                case StockpileKind.Diamond:
                    if (_labelDiamond != null) return _labelDiamond;
                    return _labelDiamond = MakeLabelPlaque(Cyan);
                case StockpileKind.RefinedDiamond:
                    if (_labelRefinedDia != null) return _labelRefinedDia;
                    return _labelRefinedDia = MakeLabelPlaque(new Color(0.75f, 0.92f, 1f));
                case StockpileKind.Dirt:
                    if (_labelDirt != null) return _labelDirt;
                    return _labelDirt = MakeLabelPlaque(Suit);
                default:
                    if (_labelRock != null) return _labelRock;
                    return _labelRock = MakeLabelPlaque(Metal);
            }
        }

        static Sprite MakeLabelPlaque(Color ink)
        {
            const int s = 36;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Clear(tex, s, s);
            Box(tex, s, s, 2, 10, 32, 16, Outline);
            Box(tex, s, s, 3, 11, 30, 14, Charcoal);
            Box(tex, s, s, 5, 13, 26, 10, MetalDeep);
            Box(tex, s, s, 7, 15, 22, 6, ink);
            Box(tex, s, s, 9, 16, 18, 4, Color.Lerp(ink, Black, 0.35f));
            // Segmented readout bars
            for (int i = 0; i < 5; i++)
                Dot(tex, s, s, 10 + i * 4, 18, Color.Lerp(ink, Color.white, 0.5f));
            Bolt(tex, s, s, 5, 12);
            Bolt(tex, s, s, 31, 12);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        // ——— Camp ———

        public static Sprite SleepPad
        {
            get
            {
                if (_sleepPad != null) return _sleepPad;
                const int s = 96;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                Clear(tex, s, s);
                for (int y = 4; y < 92; y++)
                for (int x = 4; x < 92; x++)
                {
                    float dx = (x - 48f) / 40f;
                    float dy = (y - 46f) / 38f;
                    float d = dx * dx + dy * dy;
                    if (d > 1f) continue;
                    float n = Mathf.PerlinNoise(x * 0.18f, y * 0.18f);
                    var c = Color.Lerp(new Color(0.26f, 0.18f, 0.11f), new Color(0.4f, 0.3f, 0.16f), n);
                    if (d > 0.78f) c = Color.Lerp(c, Charcoal, 0.55f);
                    c.a = d > 0.9f ? 0.55f : 0.92f;
                    tex.SetPixel(x, y, c);
                }
                // Trampled path toward yard
                Box(tex, s, s, 22, 36, 40, 10, new Color(0.22f, 0.16f, 0.1f, 0.7f));
                Weather(tex, s, s, 8, 8, 80, 80, 2);
                tex.Apply();
                _sleepPad = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.4f), s);
                return _sleepPad;
            }
        }

        public static Sprite Tent
        {
            get
            {
                if (_tent != null) return _tent;
                // Large wall / marquee tent — top-down elongated canvas hall
                const int s = 112;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                Clear(tex, s, s);

                // Outer guy-line footprint (slightly rounded rectangle)
                for (int y = 10; y < 100; y++)
                for (int x = 8; x < 104; x++)
                {
                    float nx = (x - 56f) / 46f;
                    float ny = (y - 55f) / 42f;
                    // Soft stadium / capsule tent plan
                    float ax = Mathf.Abs(nx);
                    float ay = Mathf.Abs(ny);
                    float d = ax > 0.72f
                        ? Mathf.Sqrt((ax - 0.72f) * (ax - 0.72f) / 0.28f / 0.28f + ay * ay)
                        : ay;
                    if (d > 1.02f) continue;

                    float edge = d;
                    Color c;
                    if (edge > 0.92f)
                        c = Outline;
                    else if (edge > 0.82f)
                        c = CanvasDeep;
                    else if (edge > 0.7f)
                        c = Color.Lerp(Canvas, OrangeDeep, 0.35f); // reinforced skirt
                    else
                    {
                        // Ridge / panel folds down the length
                        float fold = Mathf.Abs(Mathf.Sin(nx * Mathf.PI * 2.2f)) * 0.25f;
                        c = Color.Lerp(CanvasHi, Canvas, edge * 0.7f + fold);
                        if (Mathf.Abs(nx) < 0.08f)
                            c = Color.Lerp(c, Charcoal, 0.45f); // center ridge pole
                        else if (Mathf.Abs(nx) < 0.14f)
                            c = Color.Lerp(c, MetalDeep, 0.3f);
                    }

                    // Canvas weave + weathering grit
                    if (((x + y) % 8) == 0) c = Color.Lerp(c, CanvasDeep, 0.22f);
                    if (y > 88 && edge < 0.85f)
                        c = Color.Lerp(c, Orange, 0.2f); // rain flap hem
                    tex.SetPixel(x, y, c);
                }

                // Ridge pole hardware
                Box(tex, s, s, 54, 14, 4, 82, Charcoal);
                Box(tex, s, s, 55, 16, 2, 78, MetalDeep);
                // End caps / porch frame
                Box(tex, s, s, 18, 18, 6, 6, OrangeDeep);
                Box(tex, s, s, 88, 18, 6, 6, OrangeDeep);
                Box(tex, s, s, 18, 88, 6, 6, Orange);
                Box(tex, s, s, 88, 88, 6, 6, Orange);
                // Guy pegs
                Box(tex, s, s, 10, 52, 4, 4, Metal);
                Box(tex, s, s, 98, 52, 4, 4, Metal);
                Box(tex, s, s, 40, 8, 4, 4, MetalHi);
                Box(tex, s, s, 68, 8, 4, 4, MetalHi);
                // Hazard stripe on entry hem
                HazardDiag(tex, s, s, 30, 92, 52, 5);
                // Roof vents (circular patches)
                Disc(tex, s, s, 40, 40, 5, 5, Charcoal, Outline);
                Disc(tex, s, s, 40, 40, 3, 3, MetalDeep, null);
                Disc(tex, s, s, 72, 40, 5, 5, Charcoal, Outline);
                Disc(tex, s, s, 72, 40, 3, 3, MetalDeep, null);
                Weather(tex, s, s, 10, 12, 92, 88, 17);
                tex.Apply();
                _tent = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.35f), s);
                return _tent;
            }
        }

        public static Sprite TentFlap
        {
            get
            {
                if (_tentFlap != null) return _tentFlap;
                const int s = 36;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                Clear(tex, s, s);
                Box(tex, s, s, 4, 2, 28, 30, Outline);
                Box(tex, s, s, 5, 3, 26, 28, CanvasDeep);
                Box(tex, s, s, 8, 6, 20, 22, Black);
                // Warm interior glow
                Box(tex, s, s, 12, 10, 12, 14, new Color(0.55f, 0.28f, 0.1f, 0.55f));
                Dot(tex, s, s, 18, 16, Ember);
                // Zipper track
                Box(tex, s, s, 17, 4, 2, 26, MetalDeep);
                for (int i = 0; i < 8; i++)
                    Dot(tex, s, s, 17, 6 + i * 3, MetalHi);
                HazardDiag(tex, s, s, 6, 28, 24, 3);
                tex.Apply();
                _tentFlap = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.15f), s);
                return _tentFlap;
            }
        }

        /// <summary>Industrial roof AC / condenser — top-down gunmetal box with fan + exhaust.</summary>
        public static Sprite Aircon
        {
            get
            {
                if (_aircon != null) return _aircon;
                const int s = 64;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                Clear(tex, s, s);

                // Chassis
                Box(tex, s, s, 6, 8, 52, 46, Outline);
                Box(tex, s, s, 7, 9, 50, 44, Charcoal);
                Box(tex, s, s, 10, 12, 44, 38, MetalDeep);
                // Orange shoulder rails
                Box(tex, s, s, 6, 8, 52, 5, OrangeDeep);
                Box(tex, s, s, 7, 9, 50, 3, Orange);
                Box(tex, s, s, 6, 49, 52, 5, OrangeDeep);
                Box(tex, s, s, 7, 50, 50, 3, Orange);
                // Fan grille (circular condenser)
                Disc(tex, s, s, 28, 30, 14, 14, Outline, null);
                Disc(tex, s, s, 28, 30, 12.5f, 12.5f, Charcoal, Outline);
                Disc(tex, s, s, 28, 30, 10, 10, Black, null);
                for (int a = 0; a < 12; a++)
                {
                    float ang = a * Mathf.PI / 6f;
                    for (int r = 2; r < 10; r++)
                    {
                        int x = Mathf.RoundToInt(28 + Mathf.Cos(ang) * r);
                        int y = Mathf.RoundToInt(30 + Mathf.Sin(ang) * r);
                        Dot(tex, s, s, x, y, a % 2 == 0 ? Metal : MetalDeep);
                    }
                }
                Disc(tex, s, s, 28, 30, 3, 3, Orange, null);
                Dot(tex, s, s, 28, 30, OrangeHi);
                // Exhaust stack / steam vent
                Box(tex, s, s, 44, 18, 10, 22, Outline);
                Box(tex, s, s, 45, 19, 8, 20, Metal);
                Box(tex, s, s, 46, 20, 6, 6, Black);
                Box(tex, s, s, 47, 34, 4, 4, CyanDeep);
                Dot(tex, s, s, 49, 36, Cyan);
                // Control / power strip
                Box(tex, s, s, 12, 44, 28, 6, Charcoal);
                Box(tex, s, s, 14, 45, 4, 4, Green);
                Dot(tex, s, s, 15, 46, GreenCore);
                Box(tex, s, s, 20, 45, 4, 4, CyanDeep);
                Dot(tex, s, s, 21, 46, Cyan);
                Box(tex, s, s, 26, 45, 4, 4, Black);
                Box(tex, s, s, 32, 45, 4, 4, HazardY);
                // Cable gland
                Box(tex, s, s, 4, 28, 6, 8, Outline);
                Box(tex, s, s, 5, 29, 4, 6, Black);
                HazardDiag(tex, s, s, 8, 14, 10, 4);
                Bolt(tex, s, s, 12, 14);
                Bolt(tex, s, s, 50, 14);
                Bolt(tex, s, s, 12, 48);
                Bolt(tex, s, s, 50, 48);
                Weather(tex, s, s, 6, 8, 52, 46, 21);
                tex.Apply();
                _aircon = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
                return _aircon;
            }
        }

        public static Sprite PowerCable
        {
            get
            {
                if (_powerCable != null) return _powerCable;
                const int w = 48, h = 20;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                Clear(tex, w, h);
                // Thick industrial umbilical with orange sheath + cyan core hint
                for (int x = 2; x < w - 2; x++)
                {
                    float wave = Mathf.Sin(x * 0.35f) * 2.2f;
                    int y = Mathf.RoundToInt(h * 0.5f + wave);
                    Box(tex, w, h, x, y - 3, 1, 6, Outline);
                    Box(tex, w, h, x, y - 2, 1, 4, OrangeDeep);
                    Box(tex, w, h, x, y - 1, 1, 2, Charcoal);
                    if (x % 5 == 0) Dot(tex, w, h, x, y, CyanDeep);
                }
                tex.Apply();
                _powerCable = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
                return _powerCable;
            }
        }

        public static Sprite PowerBox
        {
            get
            {
                if (_powerBox != null) return _powerBox;
                const int s = 28;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                Clear(tex, s, s);
                Box(tex, s, s, 4, 4, 20, 20, Outline);
                Box(tex, s, s, 5, 5, 18, 18, Charcoal);
                Box(tex, s, s, 7, 7, 14, 14, MetalDeep);
                // Live LEDs
                Box(tex, s, s, 9, 10, 4, 4, Green);
                Dot(tex, s, s, 10, 11, GreenCore);
                Box(tex, s, s, 15, 10, 4, 4, Cyan);
                Dot(tex, s, s, 16, 11, new Color(0.7f, 0.95f, 1f));
                Box(tex, s, s, 9, 16, 10, 3, HazardY);
                Bolt(tex, s, s, 8, 8);
                Bolt(tex, s, s, 20, 8);
                Weather(tex, s, s, 4, 4, 20, 20, 5);
                tex.Apply();
                _powerBox = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
                return _powerBox;
            }
        }

        public static Sprite Bedrolls
        {
            get
            {
                if (_bedrolls != null) return _bedrolls;
                const int s = 40;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                Clear(tex, s, s);
                void Roll(int y0, Color body, Color strap)
                {
                    Box(tex, s, s, 4, y0, 32, 10, Outline);
                    Box(tex, s, s, 5, y0 + 1, 30, 8, body);
                    Box(tex, s, s, 6, y0 + 2, 28, 6, Color.Lerp(body, Black, 0.2f));
                    Box(tex, s, s, 14, y0, 4, 10, strap);
                    Box(tex, s, s, 24, y0, 3, 10, strap);
                    Disc(tex, s, s, 8, y0 + 5, 3, 4, Color.Lerp(body, Black, 0.35f), Outline);
                    Disc(tex, s, s, 32, y0 + 5, 3, 4, Color.Lerp(body, Black, 0.35f), Outline);
                }
                Roll(8, new Color(0.22f, 0.32f, 0.36f), OrangeDeep);
                Roll(22, Canvas, Charcoal);
                Weather(tex, s, s, 4, 8, 32, 26, 3);
                tex.Apply();
                _bedrolls = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
                return _bedrolls;
            }
        }

        public static Sprite FirePit
        {
            get
            {
                if (_firePit != null) return _firePit;
                const int s = 48;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                Clear(tex, s, s);
                // Metal barrel rim (cut drum)
                Disc(tex, s, s, 24, 24, 18, 18, Outline, null);
                Disc(tex, s, s, 24, 24, 16.5f, 16.5f, Charcoal, Outline);
                Disc(tex, s, s, 24, 24, 13, 13, MetalDeep, null);
                // Stone / brick ring accents
                for (int a = 0; a < 10; a++)
                {
                    float ang = a * Mathf.PI * 0.2f;
                    int x = Mathf.RoundToInt(24 + Mathf.Cos(ang) * 15f);
                    int y = Mathf.RoundToInt(24 + Mathf.Sin(ang) * 15f);
                    Disc(tex, s, s, x, y, 3.2f, 2.8f, Suit, Outline);
                    Dot(tex, s, s, x, y, Metal);
                }
                // Ash bed
                Disc(tex, s, s, 24, 24, 8, 8, Black, null);
                Disc(tex, s, s, 24, 24, 5, 5, new Color(0.18f, 0.14f, 0.1f), null);
                // Charred logs
                Box(tex, s, s, 14, 22, 20, 4, CanvasDeep);
                Box(tex, s, s, 16, 23, 16, 2, Black);
                Box(tex, s, s, 20, 18, 4, 14, new Color(0.2f, 0.12f, 0.08f));
                // Orange hazard paint remnant on barrel
                Box(tex, s, s, 10, 30, 8, 3, OrangeDeep);
                Box(tex, s, s, 30, 16, 8, 3, Orange);
                Weather(tex, s, s, 6, 6, 36, 36, 11);
                tex.Apply();
                _firePit = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
                return _firePit;
            }
        }

        public static Sprite Flame
        {
            get
            {
                if (_flame != null) return _flame;
                const int s = 32;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                Clear(tex, s, s);
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - 15.5f) / 7f;
                    float dy = (y - 5f) / 18f;
                    float wobble = Mathf.Sin(y * 0.45f) * 0.12f;
                    dx += wobble;
                    float d = dx * dx + dy * dy * 0.5f;
                    if (d > 1f || y < 2) continue;
                    float t = 1f - d;
                    Color c = Color.Lerp(Ember, FlameMid, t);
                    if (t > 0.55f) c = Color.Lerp(c, FlameCore, (t - 0.55f) / 0.45f);
                    c.a = Mathf.Clamp01(0.55f + t * 0.45f);
                    tex.SetPixel(x, y, c);
                }
                // Hot core
                Disc(tex, s, s, 16, 10, 3, 4, FlameCore, null);
                Dot(tex, s, s, 16, 11, Color.white);
                tex.Apply();
                _flame = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.12f), s);
                return _flame;
            }
        }

        public static Sprite Crate
        {
            get
            {
                if (_crate != null) return _crate;
                const int s = 28;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                Clear(tex, s, s);
                Box(tex, s, s, 2, 4, 24, 20, Outline);
                Box(tex, s, s, 3, 5, 22, 18, Suit);
                Box(tex, s, s, 5, 7, 18, 14, Canvas);
                Box(tex, s, s, 3, 5, 22, 3, Charcoal);
                HazardDiag(tex, s, s, 5, 16, 18, 4);
                Bolt(tex, s, s, 5, 8);
                Bolt(tex, s, s, 22, 8);
                Bolt(tex, s, s, 5, 20);
                Bolt(tex, s, s, 22, 20);
                Weather(tex, s, s, 2, 4, 24, 20, 7);
                tex.Apply();
                _crate = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
                return _crate;
            }
        }
    }
}
