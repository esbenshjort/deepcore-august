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
                    int bedrock = rng.NextDouble() < 0.15 ? rng.Next(1, 3) : 0;
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
                if (ox * ox + oy * oy > rad * rad) continue;
                int x = cx + ox, y = cy + oy;
                if (!w.InBounds(x, y) || w.IsExcavated(x, y)) continue;
                var c = w.Get(x, y);
                if (c.IsUndamageableBorder) continue;
                int bedrock = rng.Next(2, 5); // 2–4 bedrock sockets (hard)
                int gold = rng.NextDouble() < 0.08 ? 1 : 0;
                gold = Mathf.Min(gold, 4 - bedrock);
                w.Set(x, y, FineTerrainWorld.FromCounts(4 - bedrock - gold, bedrock, gold));
            }
            w.EndBatch();
        }
    }
}
