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
        /// Organic bedrock: large continuous lobes / ridges, soft corridors to snake through.
        /// Speckles and tiny islands are culled so soft rock stays passable; real masses hurt.
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

                // Lower frequencies → fewer speckles, bigger coherent masses
                float n1 = Mathf.PerlinNoise(x * 0.018f + ox, y * 0.018f + oy);
                float n2 = Mathf.PerlinNoise(x * 0.042f + ox + 8f, y * 0.042f + oy + 3f);
                float n3 = Mathf.PerlinNoise(x * 0.011f + ox + 20f, y * 0.011f + oy + 11f);

                // Wider soft corridors through the field
                float pass = Mathf.PerlinNoise(x * 0.016f + 40f + ox, y * 0.016f + 17f + oy);
                bool corridor = pass > 0.36f && pass < 0.64f;
                if (corridor && n2 < 0.78f) continue;

                // Thick ridges
                float ridge = 1f - Mathf.Abs(n1 - 0.5f) * 2f;
                bool vein = ridge > 0.82f && n2 > 0.42f && n3 > 0.38f;

                // Large clumps only (high threshold — no pepper noise)
                float clump = n2 * 0.55f + n3 * 0.45f;
                float warp = Mathf.PerlinNoise(x * 0.055f + oy, y * 0.055f + ox);
                bool blob = clump > 0.80f + warp * 0.05f;

                if (!vein && !blob) continue;

                int bedrock;
                if (vein && blob) bedrock = 4;
                else if (vein) bedrock = ridge > 0.91f ? 4 : 3;
                else bedrock = clump > 0.88f ? 4 : 3;

                // Soft fringe on veins only — still diggable but not speckled 2-socket crumbs
                if (vein && ridge < 0.86f && !blob) bedrock = 3;

                w.Set(x, y, FineTerrainWorld.FromCounts(4 - bedrock, bedrock, 0));
            }
            w.EndBatch();

            CullSmallBedrockIslands(w, minCells: 55);
            FillTinyRockHolesInBedrock(w, maxHoleCells: 28);
        }

        /// <summary>Remove isolated / tiny bedrock speckles so paths stay open.</summary>
        static void CullSmallBedrockIslands(FineTerrainWorld w, int minCells)
        {
            int n = w.Width * w.Height;
            var stamp = new int[n];
            var q = new System.Collections.Generic.Queue<int>(256);
            int gen = 0;
            var component = new System.Collections.Generic.List<int>(128);

            w.BeginBatch();
            for (int y = 1; y < w.Height - 1; y++)
            for (int x = 1; x < w.Width - 1; x++)
            {
                int i = y * w.Width + x;
                if (stamp[i] != 0) continue;
                if (w.IsExcavated(x, y)) continue;
                var cell = w.Get(x, y);
                if (cell.IsUndamageableBorder || cell.BedrockCount < 2) continue;

                gen++;
                component.Clear();
                q.Clear();
                q.Enqueue(i);
                stamp[i] = gen;

                while (q.Count > 0)
                {
                    int cur = q.Dequeue();
                    component.Add(cur);
                    int cx = cur % w.Width;
                    int cy = cur / w.Width;
                    TryBedEnqueue(w, stamp, q, gen, cx + 1, cy);
                    TryBedEnqueue(w, stamp, q, gen, cx - 1, cy);
                    TryBedEnqueue(w, stamp, q, gen, cx, cy + 1);
                    TryBedEnqueue(w, stamp, q, gen, cx, cy - 1);
                }

                if (component.Count >= minCells) continue;
                for (int k = 0; k < component.Count; k++)
                {
                    int idx = component[k];
                    int bx = idx % w.Width;
                    int by = idx / w.Width;
                    w.Set(bx, by, FineTerrainWorld.FromCounts(4, 0, 0));
                }
            }
            w.EndBatch();
        }

        static void TryBedEnqueue(FineTerrainWorld w, int[] stamp,
            System.Collections.Generic.Queue<int> q, int gen, int x, int y)
        {
            if (!w.InBounds(x, y) || w.IsExcavated(x, y)) return;
            int i = y * w.Width + x;
            if (stamp[i] == gen) return;
            var c = w.Get(x, y);
            if (c.IsUndamageableBorder || c.BedrockCount < 2) return;
            stamp[i] = gen;
            q.Enqueue(i);
        }

        /// <summary>
        /// Seal tiny soft-rock pockets trapped inside bedrock so hitting a mass feels solid.
        /// </summary>
        static void FillTinyRockHolesInBedrock(FineTerrainWorld w, int maxHoleCells)
        {
            int n = w.Width * w.Height;
            var stamp = new int[n];
            var q = new System.Collections.Generic.Queue<int>(256);
            int gen = 0;
            var component = new System.Collections.Generic.List<int>(64);

            w.BeginBatch();
            for (int y = 1; y < w.Height - 1; y++)
            for (int x = 1; x < w.Width - 1; x++)
            {
                int i = y * w.Width + x;
                if (stamp[i] != 0) continue;
                if (w.IsExcavated(x, y)) continue;
                var cell = w.Get(x, y);
                if (cell.IsUndamageableBorder || cell.BedrockCount >= 2) continue;

                gen++;
                component.Clear();
                q.Clear();
                q.Enqueue(i);
                stamp[i] = gen;
                bool touchesBorderOrOpen = false;

                while (q.Count > 0)
                {
                    int cur = q.Dequeue();
                    component.Add(cur);
                    int cx = cur % w.Width;
                    int cy = cur / w.Width;
                    if (cx <= 1 || cy <= 1 || cx >= w.Width - 2 || cy >= w.Height - 2)
                        touchesBorderOrOpen = true;
                    TryRockHoleEnqueue(w, stamp, q, gen, cx + 1, cy, ref touchesBorderOrOpen);
                    TryRockHoleEnqueue(w, stamp, q, gen, cx - 1, cy, ref touchesBorderOrOpen);
                    TryRockHoleEnqueue(w, stamp, q, gen, cx, cy + 1, ref touchesBorderOrOpen);
                    TryRockHoleEnqueue(w, stamp, q, gen, cx, cy - 1, ref touchesBorderOrOpen);
                }

                if (touchesBorderOrOpen || component.Count > maxHoleCells) continue;
                for (int k = 0; k < component.Count; k++)
                {
                    int idx = component[k];
                    int bx = idx % w.Width;
                    int by = idx / w.Width;
                    w.Set(bx, by, FineTerrainWorld.FromCounts(0, 4, 0));
                }
            }
            w.EndBatch();
        }

        static void TryRockHoleEnqueue(FineTerrainWorld w, int[] stamp,
            System.Collections.Generic.Queue<int> q, int gen, int x, int y, ref bool touchesOpen)
        {
            if (!w.InBounds(x, y))
            {
                touchesOpen = true;
                return;
            }
            if (w.IsExcavated(x, y))
            {
                touchesOpen = true;
                return;
            }
            int i = y * w.Width + x;
            if (stamp[i] == gen) return;
            var c = w.Get(x, y);
            if (c.IsUndamageableBorder)
            {
                touchesOpen = true;
                return;
            }
            if (c.BedrockCount >= 2) return;
            stamp[i] = gen;
            q.Enqueue(i);
        }

        /// <summary>
        /// Gold: more veins across the mountain, plus a few small near-camp teases.
        /// Deep veins stay richer; near start stays mostly empty except intentional pockets.
        /// </summary>
        public static void PlaceOrganicGold(FineTerrainWorld w, int startX, int startY, int seed)
        {
            var rng = new System.Random(seed);
            float maxDist = Mathf.Sqrt(w.Width * w.Width + w.Height * w.Height) * 0.85f;

            // Deep / mid veins (more than before)
            PlaceVein(w, 0.22f, 0.48f, 0.40f, 0.82f, 0.0075f, seed + 11, minGrade: 2, maxGrade: 3);
            PlaceVein(w, 0.58f, 0.52f, 0.80f, 0.88f, 0.007f, seed + 22, minGrade: 2, maxGrade: 4);
            PlaceVein(w, 0.42f, 0.62f, 0.60f, 0.95f, 0.0065f, seed + 33, minGrade: 3, maxGrade: 4);
            PlaceVein(w, 0.12f, 0.65f, 0.32f, 0.92f, 0.006f, seed + 77, minGrade: 2, maxGrade: 4);
            PlaceVein(w, 0.70f, 0.45f, 0.90f, 0.78f, 0.0065f, seed + 88, minGrade: 2, maxGrade: 3);
            PlaceVein(w, 0.35f, 0.40f, 0.55f, 0.68f, 0.0055f, seed + 99, minGrade: 2, maxGrade: 3);
            PlaceVein(w, 0.48f, 0.78f, 0.72f, 0.96f, 0.006f, seed + 111, minGrade: 3, maxGrade: 4);

            PlaceCluster(w, 0.30f, 0.88f, 0.018f, 6, seed + 44);
            PlaceCluster(w, 0.75f, 0.82f, 0.016f, 5, seed + 55);
            PlaceCluster(w, 0.52f, 0.94f, 0.014f, 5, seed + 66);
            PlaceCluster(w, 0.18f, 0.72f, 0.015f, 5, seed + 122);
            PlaceCluster(w, 0.85f, 0.60f, 0.014f, 4, seed + 133);

            // Clear accidental gold too close to camp (intentional near-camp veins placed after)
            float clearR = Mathf.Min(w.Width, w.Height) * 0.18f;
            w.BeginBatch();
            for (int y = 1; y < w.Height - 1; y++)
            for (int x = 1; x < w.Width - 1; x++)
            {
                float dx = x - startX;
                float dy = y - startY;
                if (dx * dx + dy * dy > clearR * clearR) continue;
                var c = w.Get(x, y);
                if ((c.GoldCount <= 0 && c.DiamondCount <= 0) || c.IsUndamageableBorder || w.IsExcavated(x, y)) continue;
                int bed = c.BedrockCount;
                w.Set(x, y, FineTerrainWorld.FromCounts(4 - bed, bed, 0, 0));
            }
            w.EndBatch();

            // Thin deep gold a bit so veins stay readable (not solid carpets)
            w.BeginBatch();
            for (int y = 1; y < w.Height - 1; y++)
            for (int x = 1; x < w.Width - 1; x++)
            {
                var c = w.Get(x, y);
                if (c.GoldCount <= 0 || c.IsUndamageableBorder || w.IsExcavated(x, y)) continue;
                float dx = x - startX;
                float dy = y - startY;
                float depth = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / maxDist);
                float keep = Mathf.Lerp(0.22f, 0.62f, depth * depth);
                if (rng.NextDouble() > keep)
                {
                    int bed = c.BedrockCount;
                    int dia = c.DiamondCount;
                    w.Set(x, y, FineTerrainWorld.FromCounts(4 - bed - dia, bed, 0, dia));
                }
            }
            w.EndBatch();

            // Small near-camp teases — short veins / clusters just outside the pad
            float invW = 1f / w.Width;
            float invH = 1f / w.Height;
            float sx = startX * invW;
            float sy = startY * invH;
            PlaceVein(w, sx - 0.04f, sy + 0.06f, sx + 0.05f, sy + 0.14f, 0.004f, seed + 201, minGrade: 1, maxGrade: 2);
            PlaceVein(w, sx + 0.06f, sy + 0.04f, sx + 0.14f, sy + 0.10f, 0.0035f, seed + 202, minGrade: 1, maxGrade: 2);
            PlaceCluster(w, sx - 0.08f, sy + 0.10f, 0.010f, 4, seed + 203);
            PlaceCluster(w, sx + 0.10f, sy + 0.12f, 0.009f, 3, seed + 204);
        }

        /// <summary>
        /// Diamond pipes / pockets — rarer than gold, cool crystalline clusters.
        /// Includes tiny early-game teases near camp.
        /// </summary>
        public static void PlaceOrganicDiamond(FineTerrainWorld w, int startX, int startY, int seed)
        {
            var rng = new System.Random(seed + 9001);
            float invW = 1f / w.Width;
            float invH = 1f / w.Height;
            float sx = startX * invW;
            float sy = startY * invH;

            // Mid / deep diamond pipes (narrower than gold veins)
            PlaceDiamondVein(w, 0.28f, 0.55f, 0.38f, 0.78f, 0.0045f, seed + 301, 1, 3);
            PlaceDiamondVein(w, 0.62f, 0.58f, 0.78f, 0.86f, 0.004f, seed + 302, 2, 4);
            PlaceDiamondVein(w, 0.45f, 0.70f, 0.58f, 0.94f, 0.0038f, seed + 303, 2, 3);
            PlaceDiamondVein(w, 0.15f, 0.72f, 0.28f, 0.90f, 0.0035f, seed + 304, 1, 3);
            PlaceDiamondVein(w, 0.78f, 0.50f, 0.92f, 0.72f, 0.004f, seed + 305, 2, 4);

            PlaceDiamondCluster(w, 0.35f, 0.90f, 0.012f, 4, seed + 311);
            PlaceDiamondCluster(w, 0.70f, 0.88f, 0.011f, 4, seed + 312);
            PlaceDiamondCluster(w, 0.52f, 0.96f, 0.010f, 3, seed + 313);

            // Clear accidental diamonds too close — then plant intentional early teases
            float clearR = Mathf.Min(w.Width, w.Height) * 0.16f;
            w.BeginBatch();
            for (int y = 1; y < w.Height - 1; y++)
            for (int x = 1; x < w.Width - 1; x++)
            {
                float dx = x - startX;
                float dy = y - startY;
                if (dx * dx + dy * dy > clearR * clearR) continue;
                var c = w.Get(x, y);
                if (c.DiamondCount <= 0 || c.IsUndamageableBorder || w.IsExcavated(x, y)) continue;
                int bed = c.BedrockCount;
                int gold = c.GoldCount;
                w.Set(x, y, FineTerrainWorld.FromCounts(4 - bed - gold, bed, gold, 0));
            }
            w.EndBatch();

            // Early-game diamond pockets — small, findable without deep push
            PlaceDiamondVein(w, sx - 0.02f, sy + 0.08f, sx + 0.04f, sy + 0.16f, 0.003f, seed + 401, 1, 2);
            PlaceDiamondCluster(w, sx + 0.09f, sy + 0.11f, 0.008f, 3, seed + 402);
            PlaceDiamondCluster(w, sx - 0.11f, sy + 0.14f, 0.007f, 2, seed + 403);

            // Thin dense carpets
            w.BeginBatch();
            for (int y = 1; y < w.Height - 1; y++)
            for (int x = 1; x < w.Width - 1; x++)
            {
                var c = w.Get(x, y);
                if (c.DiamondCount <= 0 || c.IsUndamageableBorder || w.IsExcavated(x, y)) continue;
                if (rng.NextDouble() > 0.72)
                {
                    int bed = c.BedrockCount;
                    int gold = c.GoldCount;
                    w.Set(x, y, FineTerrainWorld.FromCounts(4 - bed - gold, bed, gold, 0));
                }
            }
            w.EndBatch();
        }

        public static void PlaceDiamondVein(FineTerrainWorld w, float nx0, float ny0, float nx1, float ny1,
            float halfNorm, int seed, byte minGrade = 1, byte maxGrade = 3)
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
                float wobble = (float)(rng.NextDouble() - 0.5) * half * 0.7f;
                int cx = Mathf.RoundToInt(Mathf.Lerp(ax, bx, t) + wobble);
                int cy = Mathf.RoundToInt(Mathf.Lerp(ay, by, t) + wobble * 0.55f);
                int r = Mathf.Max(1, half - rng.Next(0, Mathf.Max(1, half / 2)));
                for (int oy = -r; oy <= r; oy++)
                for (int ox = -r; ox <= r; ox++)
                {
                    if (ox * ox + oy * oy > r * r) continue;
                    int x = cx + ox, y = cy + oy;
                    if (!w.InBounds(x, y) || w.IsExcavated(x, y)) continue;
                    var c = w.Get(x, y);
                    if (c.IsUndamageableBorder) continue;
                    // Prefer soft rock; light diamond sprinkle on gold edges ok
                    int gold = c.GoldCount;
                    int bed = c.BedrockCount >= 3 ? 0 : Mathf.Min(c.BedrockCount, 1);
                    int dia = rng.Next(minGrade, maxGrade + 1);
                    dia = Mathf.Min(dia, 4 - gold - bed);
                    if (dia <= 0) continue;
                    w.Set(x, y, FineTerrainWorld.FromCounts(4 - gold - bed - dia, bed, gold, dia));
                }
            }
            w.EndBatch();
        }

        public static void PlaceDiamondCluster(FineTerrainWorld w, float nx, float ny, float radiusNorm,
            int count, int seed)
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
                int gold = c.GoldCount;
                int bed = Mathf.Min(c.BedrockCount, 1);
                int dia = rng.Next(1, 4);
                dia = Mathf.Min(dia, 4 - gold - bed);
                if (dia <= 0) continue;
                w.Set(x, y, FineTerrainWorld.FromCounts(4 - gold - bed - dia, bed, gold, dia));
            }
        }

        /// <summary>
        /// Soft corridors through bedrock so the mountain is challenging but navigable
        /// without forced bedrock punches. Leaves gas / gold / diamond intact.
        /// </summary>
        public static void CarveSoftExplorationCorridors(FineTerrainWorld w, int startX, int startY, int seed)
        {
            float ox = seed * 0.13f;
            float oy = seed * 0.29f;
            w.BeginBatch();
            for (int y = 1; y < w.Height - 1; y++)
            for (int x = 1; x < w.Width - 1; x++)
            {
                if (w.IsExcavated(x, y) || w.IsGas(x, y)) continue;
                var c = w.Get(x, y);
                if (c.IsUndamageableBorder) continue;
                if (c.IsPreciousOre) continue; // keep mineral finds

                float pass = Mathf.PerlinNoise(x * 0.014f + ox, y * 0.014f + oy);
                float branch = Mathf.PerlinNoise(x * 0.028f + oy, y * 0.028f + ox);
                bool corridor = (pass > 0.40f && pass < 0.60f) || (branch > 0.46f && branch < 0.54f);
                if (!corridor) continue;
                if (c.BedrockCount < 2) continue;
                w.Set(x, y, FineTerrainWorld.FromCounts(4, 0, 0, 0));
            }
            w.EndBatch();

            // Soft approach cone north of camp — early dig without wall of bedrock
            FillSoftRockRect(w, startX - 22, startY + 1, 44, 36);
            FillSoftRockRect(w, startX - 14, startY + 30, 28, 28);
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
                    soft.MaxHp = (byte)Mathf.Max(2, soft.MaxHp / 3);
                    soft.Hp = soft.MaxHp;
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

        /// <summary>
        /// Full exploration mountain: organic bedrock, gold + diamond veins, gas pockets,
        /// soft corridors to snake through. Early gold/diamond teases near camp.
        /// </summary>
        public static void BuildSocketMapStarterMaze(FineTerrainWorld w, int sx, int sy)
        {
            const int seed = 4242;
            PlaceOrganicBedrock(w, seed);
            CarveSoftExplorationCorridors(w, sx, sy, seed + 7);
            PlaceOrganicGold(w, sx, sy, seed + 101);
            PlaceOrganicDiamond(w, sx, sy, seed + 202);
            PlaceGasPockets(w, sx, sy, seed + 303);

            // Landmark diamond chamber deep north (reward for careful pathfinding)
            FillDiamondRect(w, sx - 8, sy + 70, 16, 10, minGrade: 2, maxGrade: 4);
            // Landmark gold chamber slightly offset
            FillGoldRect(w, sx + 18, sy + 66, 18, 12, minGrade: 3, maxGrade: 4);
            // Far gas diversion west — avoidable via soft corridors
            FillSoftRockRect(w, sx - 72, sy + 40, 22, 22);
            TryCarveGasPocket(w, sx - 64, sy + 48, rx: 5, ry: 4, stretch: 1f, rockBuffer: 4);
        }

        /// <summary>Force diggable soft rock (clears accidental bedrock from the corridor).</summary>
        public static void FillSoftRockRect(FineTerrainWorld w, int x0, int y0, int width, int height)
        {
            for (int y = y0; y < y0 + height; y++)
            for (int x = x0; x < x0 + width; x++)
            {
                if (!w.InBounds(x, y) || w.IsExcavated(x, y)) continue;
                var c = w.Get(x, y);
                if (c.IsUndamageableBorder) continue;
                if (w.IsGas(x, y)) continue;
                if (c.IsPreciousOre) continue;
                w.Set(x, y, FineTerrainWorld.FromCounts(4, 0, 0, 0));
            }
        }

        public static void FillBedrockRect(FineTerrainWorld w, int x0, int y0, int width, int height)
        {
            for (int y = y0; y < y0 + height; y++)
            for (int x = x0; x < x0 + width; x++)
            {
                if (!w.InBounds(x, y) || w.IsExcavated(x, y)) continue;
                var c = w.Get(x, y);
                if (c.IsUndamageableBorder) continue;
                w.Set(x, y, FineTerrainWorld.FromCounts(0, 4, 0, 0));
            }
        }

        public static void FillGoldRect(
            FineTerrainWorld w, int x0, int y0, int width, int height,
            int minGrade, int maxGrade)
        {
            minGrade = Mathf.Clamp(minGrade, 1, 4);
            maxGrade = Mathf.Clamp(maxGrade, minGrade, 4);
            int midX = x0 + width / 2;
            int midY = y0 + height / 2;
            for (int y = y0; y < y0 + height; y++)
            for (int x = x0; x < x0 + width; x++)
            {
                if (!w.InBounds(x, y) || w.IsExcavated(x, y)) continue;
                var c = w.Get(x, y);
                if (c.IsUndamageableBorder) continue;

                float dx = (x - midX) / (float)Mathf.Max(1, width * 0.5f);
                float dy = (y - midY) / (float)Mathf.Max(1, height * 0.5f);
                float edge = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                int gold = edge < 0.45f ? maxGrade
                    : edge < 0.75f ? Mathf.Max(minGrade, maxGrade - 1)
                    : minGrade;
                w.Set(x, y, FineTerrainWorld.FromCounts(4 - gold, 0, gold, 0));
            }
        }

        public static void FillDiamondRect(
            FineTerrainWorld w, int x0, int y0, int width, int height,
            int minGrade, int maxGrade)
        {
            minGrade = Mathf.Clamp(minGrade, 1, 4);
            maxGrade = Mathf.Clamp(maxGrade, minGrade, 4);
            int midX = x0 + width / 2;
            int midY = y0 + height / 2;
            for (int y = y0; y < y0 + height; y++)
            for (int x = x0; x < x0 + width; x++)
            {
                if (!w.InBounds(x, y) || w.IsExcavated(x, y)) continue;
                var c = w.Get(x, y);
                if (c.IsUndamageableBorder) continue;

                float dx = (x - midX) / (float)Mathf.Max(1, width * 0.5f);
                float dy = (y - midY) / (float)Mathf.Max(1, height * 0.5f);
                float edge = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                int dia = edge < 0.4f ? maxGrade
                    : edge < 0.7f ? Mathf.Max(minGrade, maxGrade - 1)
                    : minGrade;
                w.Set(x, y, FineTerrainWorld.FromCounts(4 - dia, 0, 0, dia));
            }
        }

        /// <summary>Public wrapper for mission / audit gas placement.</summary>
        public static bool TryPlaceGasPocket(FineTerrainWorld w, int cx, int cy, int rx, int ry,
            float stretch = 1f, int rockBuffer = 3) =>
            TryCarveGasPocket(w, cx, cy, rx, ry, stretch, rockBuffer);
    }
}
