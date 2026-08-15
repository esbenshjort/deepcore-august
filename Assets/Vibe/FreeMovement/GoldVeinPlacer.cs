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
        /// Sparse organic gold veins — findable ribbons, not map-wide dust.
        /// Richer / denser farther from start; near start almost none.
        /// </summary>
        public static void PlaceOrganicGold(FineTerrainWorld w, int startX, int startY, int seed)
        {
            var rng = new System.Random(seed);
            float maxDist = Mathf.Sqrt(w.Width * w.Width + w.Height * w.Height) * 0.78f;
            float ox = seed * 0.13f;
            float oy = seed * 0.27f;

            w.BeginBatch();
            for (int y = 1; y < w.Height - 1; y++)
            for (int x = 1; x < w.Width - 1; x++)
            {
                if (w.IsExcavated(x, y)) continue;
                var c = w.Get(x, y);
                if (c.IsUndamageableBorder) continue;
                if (c.BedrockCount >= 4) continue;

                float dx = x - startX;
                float dy = y - startY;
                float depth = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / maxDist);
                if (depth < 0.18f) continue; // clear near-start band

                // Narrow ridge = vein core (strict — avoids speckled map)
                float g1 = Mathf.PerlinNoise(x * 0.032f + ox, y * 0.032f + oy);
                float g2 = Mathf.PerlinNoise(x * 0.07f + ox + 5f, y * 0.07f + oy + 9f);
                float ridge = 1f - Mathf.Abs(g1 - 0.5f) * 2f;
                bool onVein = ridge > 0.88f && g2 > 0.42f && g2 < 0.78f;

                // Rare pocket only far out
                float g3 = Mathf.PerlinNoise(x * 0.09f + 18f, y * 0.09f + 7f);
                bool pocket = depth > 0.55f && g3 > 0.88f && ridge > 0.55f;

                if (!onVein && !pocket) continue;

                // Even on a ridge, thin the vein so it's a path to follow — not a sheet
                float keep = onVein
                    ? Mathf.Lerp(0.22f, 0.55f, depth * depth)
                    : Mathf.Lerp(0.12f, 0.35f, depth);
                if (rng.NextDouble() > keep) continue;

                int maxG = depth < 0.4f ? 1 : (depth < 0.65f ? 2 : (depth < 0.82f ? 3 : 4));
                int minG = depth < 0.6f ? 1 : 2;
                if (pocket && depth > 0.7f) minG = 3;
                int gold = rng.Next(minG, maxG + 1);

                int bedrock = c.BedrockCount;
                if (bedrock >= 3) bedrock = Mathf.Min(2, 4 - gold);
                if (bedrock + gold > 4) bedrock = 4 - gold;

                w.Set(x, y, FineTerrainWorld.FromCounts(4 - gold - bedrock, bedrock, gold));
            }
            w.EndBatch();

            // A few deliberate long veins to chase (still organic wobble)
            PlaceVein(w, 0.22f, 0.45f, 0.48f, 0.82f, 0.012f, seed + 11, minGrade: 2, maxGrade: 4);
            PlaceVein(w, 0.58f, 0.50f, 0.82f, 0.88f, 0.011f, seed + 22, minGrade: 2, maxGrade: 4);
            PlaceVein(w, 0.35f, 0.62f, 0.70f, 0.92f, 0.01f, seed + 33, minGrade: 3, maxGrade: 4);
            PlaceCluster(w, 0.50f, 0.90f, 0.028f, 10, seed + 44);
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
