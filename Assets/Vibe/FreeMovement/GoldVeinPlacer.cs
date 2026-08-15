using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Places mixed socket cells (rock / bedrock / gold) for dig tests.</summary>
    public static class GoldVeinPlacer
    {
        public static void PlaceVein(FineTerrainWorld w, float nx0, float ny0, float nx1, float ny1,
            float halfNorm, int seed, byte minGrade = 1, byte maxGrade = 4)
        {
            var rng = new System.Random(seed);
            int ax = Mathf.RoundToInt(nx0 * w.Width);
            int ay = Mathf.RoundToInt(ny0 * w.Height);
            int bx = Mathf.RoundToInt(nx1 * w.Width);
            int by = Mathf.RoundToInt(ny1 * w.Height);
            int half = Mathf.Max(1, Mathf.RoundToInt(halfNorm * Mathf.Min(w.Width, w.Height)));
            int steps = Mathf.Max(Mathf.Abs(bx - ax), Mathf.Abs(by - ay)) * 2 + 1;

            w.BeginBatch();
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)Mathf.Max(1, steps);
                float wobble = (float)(rng.NextDouble() - 0.5) * half * 0.8f;
                int cx = Mathf.RoundToInt(Mathf.Lerp(ax, bx, t) + wobble);
                int cy = Mathf.RoundToInt(Mathf.Lerp(ay, by, t) + wobble * 0.6f);
                int r = Mathf.Max(1, half - rng.Next(0, Mathf.Max(1, half / 2)));
                for (int oy = -r; oy <= r; oy++)
                for (int ox = -r; ox <= r; ox++)
                {
                    if (ox * ox + oy * oy > r * r) continue;
                    int x = cx + ox, y = cy + oy;
                    if (!w.InBounds(x, y) || w.IsExcavated(x, y)) continue;
                    var c = w.Get(x, y);
                    if (c.IsUndamageableBorder) continue;
                    int gold = rng.Next(minGrade, maxGrade + 1);
                    int bedrock = rng.NextDouble() < 0.12 ? rng.Next(1, 2) : 0;
                    bedrock = Mathf.Min(bedrock, 4 - gold);
                    w.Set(x, y, FineTerrainWorld.FromCounts(4 - gold - bedrock, bedrock, gold));
                }
            }
            w.EndBatch();
        }

        public static void PlaceCluster(FineTerrainWorld w, float nx, float ny, float radiusNorm, int count, int seed)
        {
            var rng = new System.Random(seed);
            int cx = Mathf.RoundToInt(nx * w.Width);
            int cy = Mathf.RoundToInt(ny * w.Height);
            int rad = Mathf.Max(2, Mathf.RoundToInt(radiusNorm * Mathf.Min(w.Width, w.Height)));
            for (int i = 0; i < count; i++)
            {
                float ang = (float)(rng.NextDouble() * Mathf.PI * 2);
                float dist = (float)rng.NextDouble() * rad;
                int x = cx + Mathf.RoundToInt(Mathf.Cos(ang) * dist);
                int y = cy + Mathf.RoundToInt(Mathf.Sin(ang) * dist);
                if (!w.InBounds(x, y) || w.IsExcavated(x, y)) continue;
                var c = w.Get(x, y);
                if (c.IsUndamageableBorder) continue;
                int gold = rng.Next(1, 5);
                w.Set(x, y, FineTerrainWorld.FromCounts(4 - gold, 0, gold));
            }
        }

        /// <summary>
        /// Organic bedrock: noisy veins + irregular clumps, with soft pass-corridors
        /// so a skilled digger can always snake around.
        /// </summary>
        public static void PlaceOrganicBedrock(FineTerrainWorld w, int seed)
        {
            float ox = seed * 0.17f;
            float oy = seed * 0.31f;
            w.BeginBatch();
            for (int y = 1; y < w.Height - 1; y++)
            for (int x = 1; x < w.Width - 1; x++)
            {
                if (w.IsExcavated(x, y)) continue;
                var c = w.Get(x, y);
                if (c.IsUndamageableBorder) continue;

                float n1 = Mathf.PerlinNoise(x * 0.038f + ox, y * 0.038f + oy);
                float n2 = Mathf.PerlinNoise(x * 0.09f + ox + 8f, y * 0.09f + oy + 3f);
                float n3 = Mathf.PerlinNoise(x * 0.02f + ox + 20f, y * 0.02f + oy + 11f);

                // Soft corridors — always diggable rock lanes through the field
                float pass = Mathf.PerlinNoise(x * 0.028f + 40f + ox, y * 0.028f + 17f + oy);
                bool corridor = pass > 0.40f && pass < 0.60f;
                if (corridor && n2 < 0.72f) continue;

                // Vein-like: near ridge of low-frequency noise
                float ridge = 1f - Mathf.Abs(n1 - 0.5f) * 2f;
                bool vein = ridge > 0.78f && n2 > 0.35f && n3 > 0.32f;

                // Clumps: irregular blobs (warped threshold, not circles)
                float clump = n2 * 0.65f + n3 * 0.35f;
                float warp = Mathf.PerlinNoise(x * 0.14f + oy, y * 0.14f + ox);
                bool blob = clump > 0.74f + warp * 0.08f;

                if (!vein && !blob) continue;

                int bedrock;
                if (vein && blob) bedrock = 4;
                else if (vein) bedrock = ridge > 0.9f ? 4 : 3;
                else bedrock = clump > 0.85f ? 4 : 3;

                // Thin edges of veins stay diggable but still hard (2 sockets)
                if (vein && ridge < 0.84f && !blob) bedrock = 2;

                w.Set(x, y, FineTerrainWorld.FromCounts(4 - bedrock, bedrock, 0));
            }
            w.EndBatch();
        }

        /// <summary>
        /// Rare gold: only thin veins + far pockets. Near start is empty —
        /// finding a signal should feel like a real win.
        /// </summary>
        public static void PlaceOrganicGold(FineTerrainWorld w, int startX, int startY, int seed)
        {
            var rng = new System.Random(seed);
            float maxDist = Mathf.Sqrt(w.Width * w.Width + w.Height * w.Height) * 0.85f;

            PlaceVein(w, 0.28f, 0.55f, 0.42f, 0.88f, 0.007f, seed + 11, minGrade: 2, maxGrade: 3);
            PlaceVein(w, 0.62f, 0.58f, 0.78f, 0.90f, 0.0065f, seed + 22, minGrade: 2, maxGrade: 4);
            PlaceVein(w, 0.45f, 0.70f, 0.58f, 0.95f, 0.006f, seed + 33, minGrade: 3, maxGrade: 4);

            PlaceCluster(w, 0.38f, 0.86f, 0.016f, 5, seed + 44);
            PlaceCluster(w, 0.72f, 0.84f, 0.014f, 4, seed + 55);
            PlaceCluster(w, 0.55f, 0.93f, 0.012f, 4, seed + 66);

            float clearR = Mathf.Min(w.Width, w.Height) * 0.22f;
            w.BeginBatch();
            for (int y = 1; y < w.Height - 1; y++)
            for (int x = 1; x < w.Width - 1; x++)
            {
                float dx = x - startX;
                float dy = y - startY;
                if (dx * dx + dy * dy > clearR * clearR) continue;
                var c = w.Get(x, y);
                if (c.GoldCount <= 0 || c.IsUndamageableBorder || w.IsExcavated(x, y)) continue;
                int bed = c.BedrockCount;
                w.Set(x, y, FineTerrainWorld.FromCounts(4 - bed, bed, 0));
            }
            w.EndBatch();

            w.BeginBatch();
            for (int y = 1; y < w.Height - 1; y++)
            for (int x = 1; x < w.Width - 1; x++)
            {
                var c = w.Get(x, y);
                if (c.GoldCount <= 0 || c.IsUndamageableBorder || w.IsExcavated(x, y)) continue;
                float dx = x - startX;
                float dy = y - startY;
                float depth = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / maxDist);
                float keep = Mathf.Lerp(0.15f, 0.5f, depth * depth);
                if (rng.NextDouble() > keep)
                {
                    int bed = c.BedrockCount;
                    w.Set(x, y, FineTerrainWorld.FromCounts(4 - bed, bed, 0));
                }
            }
            w.EndBatch();
        }

        /// <summary>
        /// Sealed empty caverns inside solid rock. Marked as gas — when the tunnel
        /// breaks in, purple smoke vents (no rock inside the pocket).
        /// NOTE: when adding map features (minerals / pockets / hazards), update
        /// ProspectorPerson scan detection + ScanViewOverlay + tactical legend.
        /// </summary>
        public static void PlaceGasPockets(FineTerrainWorld w, int startX, int startY, int seed)
        {
            var rng = new System.Random(seed);

            w.BeginBatch();

            // Test pocket: a few meters into solid rock north-east of camp — fully sealed
            // so lighting / floor never show it until the excavator punches in.
            if (!TryCarveGasPocket(w, startX + 30, startY + 28, rx: 4, ry: 3, stretch: 0.85f, rockBuffer: 3)
                && !TryCarveGasPocket(w, startX + 36, startY + 22, rx: 4, ry: 3, stretch: 0.85f, rockBuffer: 3)
                && !TryCarveGasPocket(w, startX - 32, startY + 26, rx: 4, ry: 3, stretch: 0.85f, rockBuffer: 3))
            {
                // Last resort: farther out on a clear ray
                TryCarveGasPocket(w, startX + 40, startY + 34, rx: 3, ry: 3, stretch: 0.9f, rockBuffer: 2);
            }

            int count = 6 + rng.Next(0, 4);
            float minDist = Mathf.Min(w.Width, w.Height) * 0.22f;

            for (int p = 0; p < count; p++)
            {
                int cx = 0, cy = 0;
                bool ok = false;
                int rx = 0, ry = 0;
                float stretch = 1f;
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    cx = rng.Next(18, w.Width - 18);
                    cy = rng.Next(18, w.Height - 18);
                    float dx = cx - startX, dy = cy - startY;
                    if (dx * dx + dy * dy < minDist * minDist) continue;
                    if (w.IsExcavated(cx, cy) || w.Get(cx, cy).IsUndamageableBorder) continue;
                    rx = rng.Next(3, 7);
                    ry = rng.Next(3, 6);
                    stretch = 0.7f + (float)rng.NextDouble() * 0.5f;
                    if (!CanCarveGasPocket(w, cx, cy, rx, ry, stretch, rockBuffer: 2)) continue;
                    ok = true;
                    break;
                }
                if (!ok) continue;
                CarveGasPocket(w, cx, cy, rx, ry, stretch);
            }
            w.EndBatch();
        }

        static bool TryCarveGasPocket(FineTerrainWorld w, int cx, int cy, int rx, int ry,
            float stretch, int rockBuffer)
        {
            if (!CanCarveGasPocket(w, cx, cy, rx, ry, stretch, rockBuffer))
                return false;
            CarveGasPocket(w, cx, cy, rx, ry, stretch);
            return true;
        }

        /// <summary>
        /// Pocket must sit fully in solid rock with a solid buffer from any open tunnel
        /// so it never starts breached or lit.
        /// </summary>
        static bool CanCarveGasPocket(FineTerrainWorld w, int cx, int cy, int rx, int ry,
            float stretch, int rockBuffer)
        {
            int pad = Mathf.Max(rx, ry) + 1 + rockBuffer;
            for (int oy = -pad; oy <= pad; oy++)
            for (int ox = -pad; ox <= pad; ox++)
            {
                int x = cx + ox, y = cy + oy;
                if (!w.InBounds(x, y)) return false;

                float nx = ox / (float)Mathf.Max(1, rx);
                float ny = oy / (float)Mathf.Max(1, ry);
                float d = nx * nx + ny * ny * stretch;
                // Inside pocket or soft shell — must be solid rock (not open / not already gas)
                if (d <= 1.55f)
                {
                    if (w.IsTunnelOpen(x, y) || w.IsGas(x, y)) return false;
                    if (w.IsExcavated(x, y)) return false;
                    if (w.Get(x, y).IsUndamageableBorder) return false;
                }

                // Rock buffer ring: no open tunnel within rockBuffer of the pocket body
                if (d <= 1f)
                {
                    for (int by = -rockBuffer; by <= rockBuffer; by++)
                    for (int bx = -rockBuffer; bx <= rockBuffer; bx++)
                    {
                        if (bx == 0 && by == 0) continue;
                        int px = x + bx, py = y + by;
                        if (!w.InBounds(px, py)) return false;
                        if (w.IsTunnelOpen(px, py)) return false;
                    }
                }
            }
            return true;
        }

        static void CarveGasPocket(FineTerrainWorld w, int cx, int cy, int rx, int ry, float stretch)
        {
            for (int oy = -ry - 1; oy <= ry + 1; oy++)
            for (int ox = -rx - 1; ox <= rx + 1; ox++)
            {
                float nx = ox / (float)Mathf.Max(1, rx);
                float ny = oy / (float)Mathf.Max(1, ry);
                float d = nx * nx + ny * ny * stretch;
                int x = cx + ox, y = cy + oy;
                if (!w.InBounds(x, y)) continue;
                var cell = w.Get(x, y);
                if (cell.IsUndamageableBorder) continue;
                if (w.IsTunnelOpen(x, y) || w.IsExcavated(x, y)) continue;

                if (d <= 1f)
                {
                    w.InstantExcavate(x, y, notify: false);
                    w.MarkGas(x, y, true);
                }
                else if (d <= 1.55f)
                {
                    // Soft crust — diggable, but still solid until you punch through
                    w.Set(x, y, FineTerrainWorld.FromCounts(4, 0, 0));
                    var soft = w.Get(x, y);
                    soft.Durability = (byte)Mathf.Max(2, soft.Durability / 3);
                    soft.MaxDurability = soft.Durability;
                    soft.DamageState = 0;
                    w.Set(x, y, soft);
                }
            }
        }

        /// <summary>
        /// Half-oval chamber: flat floor at yFloor, curved roof/sides rising to radiusY.
        /// </summary>
        public static void ExcavateHalfOval(FineTerrainWorld w, int cx, int yFloor, int radiusX, int radiusY)
        {
            w.BeginBatch();
            for (int oy = 0; oy <= radiusY; oy++)
            for (int ox = -radiusX; ox <= radiusX; ox++)
            {
                float nx = ox / (float)Mathf.Max(1, radiusX);
                float ny = oy / (float)Mathf.Max(1, radiusY);
                if (nx * nx + ny * ny > 1f) continue;
                int x = cx + ox, y = yFloor + oy;
                if (!w.InBounds(x, y)) continue;
                w.InstantExcavate(x, y, notify: false);
            }
            w.EndBatch();
        }

        // --- legacy helpers kept for other runners ---

        public static void PlaceBedrockPatch(FineTerrainWorld w, float nx, float ny, float radiusNorm, int seed)
        {
            var rng = new System.Random(seed);
            int cx = Mathf.RoundToInt(nx * w.Width);
            int cy = Mathf.RoundToInt(ny * w.Height);
            int rad = Mathf.Max(2, Mathf.RoundToInt(radiusNorm * Mathf.Min(w.Width, w.Height)));
            w.BeginBatch();
            for (int oy = -rad; oy <= rad; oy++)
            for (int ox = -rad; ox <= rad; ox++)
            {
                float warp = Mathf.PerlinNoise((cx + ox) * 0.12f + seed, (cy + oy) * 0.12f) - 0.5f;
                float r2 = (rad + warp * rad * 0.45f);
                if (ox * ox + oy * oy > r2 * r2) continue;
                int x = cx + ox, y = cy + oy;
                if (!w.InBounds(x, y) || w.IsExcavated(x, y)) continue;
                var c = w.Get(x, y);
                if (c.IsUndamageableBorder) continue;
                int bedrock = rng.Next(2, 5);
                w.Set(x, y, FineTerrainWorld.FromCounts(4 - bedrock, bedrock, 0));
            }
            w.EndBatch();
        }

        public static void PlaceBedrockRidge(FineTerrainWorld w, float nx0, float ny0, float nx1, float ny1,
            float halfNorm, int seed, float wobbleAmp = 1f)
        {
            var rng = new System.Random(seed);
            int ax = Mathf.RoundToInt(nx0 * w.Width);
            int ay = Mathf.RoundToInt(ny0 * w.Height);
            int bx = Mathf.RoundToInt(nx1 * w.Width);
            int by = Mathf.RoundToInt(ny1 * w.Height);
            int half = Mathf.Max(2, Mathf.RoundToInt(halfNorm * Mathf.Min(w.Width, w.Height)));
            int steps = Mathf.Max(Mathf.Abs(bx - ax), Mathf.Abs(by - ay)) * 3 + 1;
            w.BeginBatch();
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)Mathf.Max(1, steps);
                float wobble = Mathf.Sin(t * Mathf.PI * 5f + seed) * half * 1.4f * wobbleAmp;
                wobble += (float)(rng.NextDouble() - 0.5) * half * 0.7f;
                int cx = Mathf.RoundToInt(Mathf.Lerp(ax, bx, t) + wobble);
                int cy = Mathf.RoundToInt(Mathf.Lerp(ay, by, t) + wobble * 0.4f);
                int r = half + rng.Next(0, Mathf.Max(1, half / 2));
                for (int oy = -r; oy <= r; oy++)
                for (int ox = -r; ox <= r; ox++)
                {
                    if (ox * ox + oy * oy > r * r) continue;
                    int x = cx + ox, y = cy + oy;
                    if (!w.InBounds(x, y) || w.IsExcavated(x, y)) continue;
                    var c = w.Get(x, y);
                    if (c.IsUndamageableBorder) continue;
                    int bedrock = rng.Next(3, 5);
                    w.Set(x, y, FineTerrainWorld.FromCounts(4 - bedrock, bedrock, 0));
                }
            }
            w.EndBatch();
        }

        public static void SprinkleDepthGold(FineTerrainWorld w, int startX, int startY, int seed) =>
            PlaceOrganicGold(w, startX, startY, seed);
    }
}
