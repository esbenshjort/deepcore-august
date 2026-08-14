using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.DualGrid
{
    public static class DualGridPathfinder
    {
        static readonly Vector2Int[] Dirs8 =
        {
            new(0, 1), new(1, 1), new(1, 0), new(1, -1),
            new(0, -1), new(-1, -1), new(-1, 0), new(-1, 1),
        };

        public static bool FindPath(
            DualGridWorld world,
            Vector2Int from,
            Vector2Int to,
            bool walkableOnly,
            List<Vector2Int> outPath)
        {
            outPath.Clear();
            if (from == to) return true;
            if (!world.InNavBounds(to.x, to.y)) return false;
            if (walkableOnly && !world.IsNavWalkable(to.x, to.y)) return false;

            var came = new Dictionary<Vector2Int, Vector2Int>();
            var cost = new Dictionary<Vector2Int, int>();
            var open = new List<Vector2Int> { from };
            came[from] = from;
            cost[from] = 0;

            while (open.Count > 0)
            {
                int best = 0;
                int bestScore = int.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    int f = cost[open[i]] + Heuristic(open[i], to);
                    if (f < bestScore) { bestScore = f; best = i; }
                }

                var cur = open[best];
                open.RemoveAt(best);
                if (cur == to) break;

                foreach (var d in Dirs8)
                {
                    var n = cur + d;
                    if (!CanStep(world, cur, n, walkableOnly)) continue;

                    int stepCost = walkableOnly ? 10 : (10 + world.CountUnexcavatedInNav(n.x, n.y));
                    if (d.x != 0 && d.y != 0) stepCost += 4;

                    int g = cost[cur] + stepCost;
                    if (cost.TryGetValue(n, out int old) && g >= old) continue;
                    came[n] = cur;
                    cost[n] = g;
                    if (!open.Contains(n)) open.Add(n);
                }
            }

            if (!came.ContainsKey(to)) return false;
            var stack = new List<Vector2Int>();
            var step = to;
            while (step != from)
            {
                stack.Add(step);
                step = came[step];
            }
            stack.Reverse();
            outPath.AddRange(stack);
            return true;
        }

        public static bool CanStep(DualGridWorld world, Vector2Int from, Vector2Int to, bool walkableOnly)
        {
            if (!world.InNavBounds(to.x, to.y)) return false;
            if (walkableOnly && !world.IsNavWalkable(to.x, to.y)) return false;

            int dx = to.x - from.x;
            int dy = to.y - from.y;
            if (Mathf.Abs(dx) > 1 || Mathf.Abs(dy) > 1) return false;
            if (dx == 0 && dy == 0) return false;

            // Diagonal: no corner-cutting through rock when walking on excavated only
            if (dx != 0 && dy != 0 && walkableOnly)
            {
                var a = new Vector2Int(from.x + dx, from.y);
                var b = new Vector2Int(from.x, from.y + dy);
                if (!world.IsNavWalkable(a.x, a.y) || !world.IsNavWalkable(b.x, b.y))
                    return false;
            }
            return true;
        }

        static int Heuristic(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            return 10 * Mathf.Max(dx, dy) + 4 * Mathf.Min(dx, dy);
        }
    }
}
