using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Agent size / clearance requirements for shared tunnel navigation.
    /// Workers and machines can share the same engine with different profiles.
    /// </summary>
    public struct PathAgentProfile
    {
        /// <summary>Minimum clearance (tiles to nearest solid). 1 = may walk beside walls.</summary>
        public int MinClearance;

        /// <summary>World-space body radius for Follow stepping / LOS checks.</summary>
        public float BodyRadius;

        /// <summary>
        /// Persistent lateral offset along the path (±). Applied only where clearance allows.
        /// Keep small for natural stagger without wall hugs.
        /// </summary>
        public float LateralOffset;

        public static PathAgentProfile Worker(float bodyRadius = 0.12f, float lateralOffset = 0f) =>
            new PathAgentProfile
            {
                MinClearance = 1,
                BodyRadius = bodyRadius,
                LateralOffset = lateralOffset,
            };

        public static PathAgentProfile Machine(float bodyRadius, int minClearance = 2) =>
            new PathAgentProfile
            {
                MinClearance = Mathf.Max(1, minClearance),
                BodyRadius = bodyRadius,
                LateralOffset = 0f,
            };
    }

    /// <summary>
    /// Extensible path cost weights. A* always produces valid routes;
    /// future Logistics can bias selection via these scales / noise — never invent illegal cells.
    /// </summary>
    public struct PathCostPolicy
    {
        public float DistanceScale;
        public float WallProximityScale;

        /// <summary>Reserved for future Logistics imperfect selection (0 = off).</summary>
        public float SelectionNoise;

        public static PathCostPolicy Default => new PathCostPolicy
        {
            DistanceScale = 1f,
            WallProximityScale = 1f,
            SelectionNoise = 0f,
        };
    }

    public sealed class PathResult
    {
        public bool Success;
        public bool Stranded;
        public readonly List<Vector2> Waypoints = new(64);
        public int NodeCount;
        public float Length;
        public string DebugNote;

        public void Clear()
        {
            Success = false;
            Stranded = false;
            Waypoints.Clear();
            NodeCount = 0;
            Length = 0f;
            DebugNote = null;
        }
    }

    /// <summary>
    /// Walkability + clearance cache over <see cref="FineTerrainWorld"/>.
    /// Walkable = IsTunnelOpen (excavated, Hp==0, not sealed gas).
    /// Clearance = Chebyshev distance to nearest solid tile.
    /// </summary>
    public sealed class TunnelNavGrid
    {
        public const int MaxClearance = 15;

        readonly FineTerrainWorld _world;
        byte[] _clearance; // 0 = solid / blocked; 1+ = distance to solid
        bool _built;
        int _w, _h;

        // Clearance BFS scratch
        readonly Queue<int> _q = new(4096);
        int[] _stamp;
        int _gen;

        public FineTerrainWorld World => _world;
        public bool IsBuilt => _built;
        public static bool DebugLog;

        public TunnelNavGrid(FineTerrainWorld world)
        {
            _world = world;
            EnsureBuffers();
        }

        void EnsureBuffers()
        {
            if (_world == null) return;
            if (_clearance != null && _w == _world.Width && _h == _world.Height) return;
            _w = _world.Width;
            _h = _world.Height;
            int n = _w * _h;
            _clearance = new byte[n];
            _stamp = new int[n];
            _built = false;
        }

        public void Rebuild()
        {
            EnsureBuffers();
            if (_world == null || _clearance == null) return;

            int n = _w * _h;
            for (int i = 0; i < n; i++)
                _clearance[i] = 0;

            _q.Clear();
            _gen++;
            if (_gen == int.MaxValue)
            {
                System.Array.Clear(_stamp, 0, _stamp.Length);
                _gen = 1;
            }

            // Multi-source BFS from solids into walkable cells → clearance
            for (int y = 0; y < _h; y++)
            for (int x = 0; x < _w; x++)
            {
                if (IsWalkableCell(x, y)) continue;
                int i = y * _w + x;
                _stamp[i] = _gen;
                // Seed neighbors that are walkable
                TrySeedClearance(x + 1, y, 1);
                TrySeedClearance(x - 1, y, 1);
                TrySeedClearance(x, y + 1, 1);
                TrySeedClearance(x, y - 1, 1);
                TrySeedClearance(x + 1, y + 1, 1);
                TrySeedClearance(x + 1, y - 1, 1);
                TrySeedClearance(x - 1, y + 1, 1);
                TrySeedClearance(x - 1, y - 1, 1);
            }

            while (_q.Count > 0)
            {
                int cur = _q.Dequeue();
                int cx = cur % _w;
                int cy = cur / _w;
                int next = _clearance[cur] + 1;
                if (next > MaxClearance) continue;
                ExpandClearance(cx + 1, cy, next);
                ExpandClearance(cx - 1, cy, next);
                ExpandClearance(cx, cy + 1, next);
                ExpandClearance(cx, cy - 1, next);
                ExpandClearance(cx + 1, cy + 1, next);
                ExpandClearance(cx + 1, cy - 1, next);
                ExpandClearance(cx - 1, cy + 1, next);
                ExpandClearance(cx - 1, cy - 1, next);
            }

            _built = true;
            if (DebugLog)
                DigHoodLog.Push($"NAV | Clearance rebuilt {_w}x{_h}");
        }

        void TrySeedClearance(int x, int y, int value)
        {
            if (!_world.InBounds(x, y) || !IsWalkableCell(x, y)) return;
            int i = y * _w + x;
            if (_stamp[i] == _gen) return;
            _stamp[i] = _gen;
            _clearance[i] = (byte)value;
            _q.Enqueue(i);
        }

        void ExpandClearance(int x, int y, int value)
        {
            if (!_world.InBounds(x, y) || !IsWalkableCell(x, y)) return;
            int i = y * _w + x;
            if (_clearance[i] != 0 && _clearance[i] <= value) return;
            _clearance[i] = (byte)Mathf.Min(MaxClearance, value);
            if (_stamp[i] == _gen) return;
            _stamp[i] = _gen;
            _q.Enqueue(i);
        }

        /// <summary>True when the tile is traversable for any agent (before size filter).</summary>
        public bool IsWalkableCell(int x, int y) =>
            _world != null && _world.IsTunnelOpen(x, y);

        public bool IsSolidCell(int x, int y) => !IsWalkableCell(x, y);

        public int GetClearance(int x, int y)
        {
            if (!_built) Rebuild();
            if (_world == null || !_world.InBounds(x, y)) return 0;
            if (!IsWalkableCell(x, y)) return 0;
            return _clearance[y * _w + x];
        }

        public bool CanAgentStand(int x, int y, int minClearance)
        {
            if (!IsWalkableCell(x, y)) return false;
            return GetClearance(x, y) >= Mathf.Max(1, minClearance);
        }

        /// <summary>
        /// Wall proximity cost from clearance (center-of-tunnel preference).
        /// clearance 1=+8, 2=+4, 3=+2, 4+=+0.
        /// </summary>
        public static float WallProximityCost(int clearance) => clearance switch
        {
            <= 0 => 1000f,
            1 => 8f,
            2 => 4f,
            3 => 2f,
            _ => 0f,
        };

        /// <summary>
        /// Mark clearance stale after excavation / tile change.
        /// Rebuild is deferred until the next path query (avoids per-dig full scans).
        /// </summary>
        public void NotifyTileChanged(int x, int y)
        {
            _built = false;
            if (DebugLog)
                DigHoodLog.Push($"NAV | Tile changed ({x},{y}) — clearance dirty");
        }

        public Vector2Int NearestStandable(int x, int y, int minClearance, int maxRadius = 16)
        {
            if (!_built) Rebuild();
            if (CanAgentStand(x, y, minClearance)) return new Vector2Int(x, y);

            Vector2Int best = new(x, y);
            int bestD = int.MaxValue;
            for (int r = 1; r <= maxRadius; r++)
            {
                for (int oy = -r; oy <= r; oy++)
                for (int ox = -r; ox <= r; ox++)
                {
                    if (Mathf.Abs(ox) != r && Mathf.Abs(oy) != r) continue;
                    int nx = x + ox, ny = y + oy;
                    if (!CanAgentStand(nx, ny, minClearance)) continue;
                    int d = Mathf.Abs(ox) + Mathf.Abs(oy);
                    if (d < bestD)
                    {
                        bestD = d;
                        best = new Vector2Int(nx, ny);
                    }
                }
                if (bestD < int.MaxValue) break;
            }
            return best;
        }
    }

    /// <summary>
    /// Grid A* over <see cref="TunnelNavGrid"/> with wall-proximity costs, path smoothing,
    /// and optional lateral offset. Owns worker pathfinding for DEEP CORE tunnels.
    /// </summary>
    public sealed class TunnelPathfinder
    {
        readonly TunnelNavGrid _grid;
        readonly List<Vector2> _raw = new(128);
        readonly List<Vector2> _smooth = new(128);
        readonly List<int> _chain = new(128);

        // A* buffers
        float[] _g;
        int[] _came;
        int[] _stamp;
        int[] _heap;
        int _heapN;
        int _gen;
        int _w, _h;

        public TunnelNavGrid Grid => _grid;
        public static bool DebugDraw;
        public static bool DebugLog;

        // Last debug snapshot
        public IReadOnlyList<Vector2> LastRawPath => _raw;
        public IReadOnlyList<Vector2> LastSmoothPath => _smooth;

        public TunnelPathfinder(TunnelNavGrid grid)
        {
            _grid = grid;
            EnsureBuffers();
        }

        public TunnelPathfinder(FineTerrainWorld world) : this(world.Navigation) { }

        void EnsureBuffers()
        {
            var world = _grid.World;
            if (world == null) return;
            if (_g != null && _w == world.Width && _h == world.Height) return;
            _w = world.Width;
            _h = world.Height;
            int n = _w * _h;
            _g = new float[n];
            _came = new int[n];
            _stamp = new int[n];
            _heap = new int[n + 2];
            _heapN = 0;
        }

        /// <summary>
        /// Find a path. Always returns only walkable routes. On failure sets Stranded when requested.
        /// </summary>
        public PathResult FindPath(
            Vector2 fromWorld,
            Vector2 toWorld,
            PathAgentProfile agent,
            PathCostPolicy policy = default,
            bool markStrandedIfFailed = false)
        {
            var result = new PathResult();
            FindPath(fromWorld, toWorld, agent, policy, markStrandedIfFailed, result);
            return result;
        }

        public void FindPath(
            Vector2 fromWorld,
            Vector2 toWorld,
            PathAgentProfile agent,
            PathCostPolicy policy,
            bool markStrandedIfFailed,
            PathResult result)
        {
            result.Clear();
            var world = _grid.World;
            if (world == null) return;
            if (policy.DistanceScale <= 0f) policy = PathCostPolicy.Default;

            EnsureBuffers();
            if (!_grid.IsBuilt) _grid.Rebuild();

            var start = world.WorldToCell(fromWorld);
            var goal = world.WorldToCell(toWorld);
            int minC = Mathf.Max(1, agent.MinClearance);

            if (!_grid.CanAgentStand(start.x, start.y, minC))
                start = _grid.NearestStandable(start.x, start.y, minC);
            if (!_grid.CanAgentStand(goal.x, goal.y, minC))
                goal = _grid.NearestStandable(goal.x, goal.y, minC);

            if (!_grid.CanAgentStand(start.x, start.y, minC) ||
                !_grid.CanAgentStand(goal.x, goal.y, minC))
            {
                result.DebugNote = "no standable start/goal";
                if (markStrandedIfFailed) result.Stranded = true;
                if (DebugLog) DigHoodLog.Push("NAV | PATH FAIL | no standable endpoints");
                return;
            }

            if (start.x == goal.x && start.y == goal.y)
            {
                result.Success = true;
                result.Waypoints.Add(toWorld);
                result.NodeCount = 1;
                result.Length = Vector2.Distance(fromWorld, toWorld);
                _raw.Clear();
                _raw.Add(toWorld);
                _smooth.Clear();
                _smooth.Add(toWorld);
                return;
            }

            if (!RunAStar(start, goal, minC, policy))
            {
                result.DebugNote = "unreachable";
                if (markStrandedIfFailed) result.Stranded = true;
                if (DebugLog)
                    DigHoodLog.Push($"NAV | PATH FAIL | {start.x},{start.y} → {goal.x},{goal.y}");
                return;
            }

            // Reconstruct cell chain → world points
            _raw.Clear();
            _chain.Clear();
            int goalI = goal.y * _w + goal.x;
            int startI = start.y * _w + start.x;
            for (int at = goalI; at >= 0; at = _came[at])
            {
                _chain.Add(at);
                if (at == startI) break;
            }
            _chain.Reverse();

            for (int i = 0; i < _chain.Count; i++)
            {
                int idx = _chain[i];
                _raw.Add(world.CellCenter(idx % _w, idx / _w));
            }
            if (_raw.Count > 0)
                _raw[_raw.Count - 1] = toWorld;

            SmoothPath(_raw, _smooth, minC, agent.BodyRadius);
            ApplyLateralOffset(_smooth, agent, minC);

            result.Success = _smooth.Count > 0;
            result.NodeCount = _smooth.Count;
            result.Waypoints.AddRange(_smooth);
            result.Length = PathLength(_smooth);
            result.DebugNote = $"nodes {_chain.Count}→{_smooth.Count}";

            if (DebugLog)
                DigHoodLog.Push(
                    $"NAV | PATH OK | len {result.Length:0.#} | raw {_chain.Count} | smooth {_smooth.Count}");
        }

        /// <summary>
        /// Camp return with rebuild + nearest-tile retry. Logs PATH HOME | FOUND/RETRY/STRANDED.
        /// </summary>
        public PathResult FindPathHome(
            Vector2 fromWorld,
            Vector2 campWorld,
            PathAgentProfile agent,
            PathCostPolicy policy = default)
        {
            if (policy.DistanceScale <= 0f) policy = PathCostPolicy.Default;
            var result = new PathResult();

            FindPath(fromWorld, campWorld, agent, policy, markStrandedIfFailed: false, result);
            if (result.Success)
            {
                DigHoodLog.Push(
                    $"PATH HOME | FOUND | length {result.Length:0.#} | nodes {result.NodeCount}");
                return result;
            }

            DigHoodLog.Push("PATH HOME | RETRY");
            _grid.Rebuild();

            var world = _grid.World;
            var near = world.WorldToCell(fromWorld);
            near = _grid.NearestStandable(near.x, near.y, Mathf.Max(1, agent.MinClearance), 24);
            Vector2 retryFrom = world.CellCenter(near.x, near.y);

            FindPath(retryFrom, campWorld, agent, policy, markStrandedIfFailed: true, result);
            if (result.Success)
            {
                DigHoodLog.Push(
                    $"PATH HOME | FOUND | length {result.Length:0.#} | nodes {result.NodeCount}");
                return result;
            }

            result.Stranded = true;
            DigHoodLog.Push("PATH HOME | STRANDED");
            return result;
        }

        bool RunAStar(Vector2Int start, Vector2Int goal, int minClearance, PathCostPolicy policy)
        {
            int startI = start.y * _w + start.x;
            int goalI = goal.y * _w + goal.x;
            int n = _w * _h;

            _gen++;
            if (_gen == int.MaxValue)
            {
                System.Array.Clear(_stamp, 0, _stamp.Length);
                _gen = 1;
            }

            for (int i = 0; i < n; i++)
                _g[i] = float.PositiveInfinity;

            _heapN = 0;
            _g[startI] = 0f;
            _came[startI] = -1;
            _stamp[startI] = _gen;
            HeapPush(startI, Heuristic(start.x, start.y, goal.x, goal.y) * policy.DistanceScale);

            int guard = 0;
            const int maxExpand = 80000;

            while (_heapN > 0 && guard++ < maxExpand)
            {
                int cur = HeapPop();
                if (cur == goalI) return true;

                int cx = cur % _w;
                int cy = cur / _w;
                float gCur = _g[cur];

                TryRelax(cx + 1, cy, cur, gCur, 1f, goal, minClearance, policy);
                TryRelax(cx - 1, cy, cur, gCur, 1f, goal, minClearance, policy);
                TryRelax(cx, cy + 1, cur, gCur, 1f, goal, minClearance, policy);
                TryRelax(cx, cy - 1, cur, gCur, 1f, goal, minClearance, policy);
                // Diagonals — only if both ortho neighbors standable (no corner cut)
                TryRelaxDiag(cx + 1, cy + 1, cx, cy, cur, gCur, 1.4142f, goal, minClearance, policy);
                TryRelaxDiag(cx + 1, cy - 1, cx, cy, cur, gCur, 1.4142f, goal, minClearance, policy);
                TryRelaxDiag(cx - 1, cy + 1, cx, cy, cur, gCur, 1.4142f, goal, minClearance, policy);
                TryRelaxDiag(cx - 1, cy - 1, cx, cy, cur, gCur, 1.4142f, goal, minClearance, policy);
            }

            return _stamp[goalI] == _gen && !float.IsInfinity(_g[goalI]);
        }

        void TryRelaxDiag(int nx, int ny, int px, int py, int from, float gCur, float stepDist,
            Vector2Int goal, int minClearance, PathCostPolicy policy)
        {
            // Prevent cutting solid corners
            if (!_grid.CanAgentStand(nx, py, minClearance) || !_grid.CanAgentStand(px, ny, minClearance))
                return;
            TryRelax(nx, ny, from, gCur, stepDist, goal, minClearance, policy);
        }

        void TryRelax(int nx, int ny, int from, float gCur, float stepDist,
            Vector2Int goal, int minClearance, PathCostPolicy policy)
        {
            if (!_grid.CanAgentStand(nx, ny, minClearance)) return;
            int ni = ny * _w + nx;
            int clear = _grid.GetClearance(nx, ny);
            float stepCost = stepDist * policy.DistanceScale
                             + TunnelNavGrid.WallProximityCost(clear) * policy.WallProximityScale;

            // Future Logistics hook: SelectionNoise biases among valid edges without opening solids.
            if (policy.SelectionNoise > 0.001f)
                stepCost += policy.SelectionNoise * Hash01(nx, ny, from);

            float ng = gCur + stepCost;
            if (ng >= _g[ni]) return;

            _g[ni] = ng;
            _came[ni] = from;
            _stamp[ni] = _gen;
            float f = ng + Heuristic(nx, ny, goal.x, goal.y) * policy.DistanceScale;
            HeapPush(ni, f);
        }

        static float Heuristic(int ax, int ay, int bx, int by)
        {
            int dx = Mathf.Abs(ax - bx);
            int dy = Mathf.Abs(ay - by);
            // Octile
            int m = Mathf.Min(dx, dy);
            return (dx + dy) + (1.4142f - 2f) * m;
        }

        static float Hash01(int x, int y, int salt)
        {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
            h ^= h >> 13;
            h *= 1274126177u;
            return (h & 0xFFFF) / 65535f;
        }

        /// <summary>String-pull smoothing — skip nodes when the segment stays in standable tiles.</summary>
        void SmoothPath(List<Vector2> raw, List<Vector2> dst, int minClearance, float bodyRadius)
        {
            dst.Clear();
            if (raw.Count == 0) return;
            if (raw.Count <= 2)
            {
                dst.AddRange(raw);
                return;
            }

            dst.Add(raw[0]);
            int anchor = 0;
            for (int i = 2; i < raw.Count; i++)
            {
                if (!LineStandable(raw[anchor], raw[i], minClearance, bodyRadius))
                {
                    dst.Add(raw[i - 1]);
                    anchor = i - 1;
                }
            }
            dst.Add(raw[raw.Count - 1]);
        }

        bool LineStandable(Vector2 a, Vector2 b, int minClearance, float bodyRadius)
        {
            var world = _grid.World;
            float dist = Vector2.Distance(a, b);
            float step = Mathf.Max(world.CellSize * 0.35f, bodyRadius * 0.5f);
            int samples = Mathf.Max(2, Mathf.CeilToInt(dist / step));
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector2 p = Vector2.Lerp(a, b, t);
                var c = world.WorldToCell(p);
                if (!_grid.CanAgentStand(c.x, c.y, minClearance)) return false;
                if (world.CircleHitsSolid(p, Mathf.Max(0.02f, bodyRadius * 0.75f))) return false;
            }
            return true;
        }

        void ApplyLateralOffset(List<Vector2> path, PathAgentProfile agent, int minClearance)
        {
            float offset = agent.LateralOffset;
            if (Mathf.Abs(offset) < 0.001f || path.Count < 2) return;

            var world = _grid.World;
            for (int i = 0; i < path.Count; i++)
            {
                Vector2 dir;
                if (i < path.Count - 1) dir = path[i + 1] - path[i];
                else dir = path[i] - path[i - 1];
                if (dir.sqrMagnitude < 0.0001f) continue;
                dir.Normalize();
                Vector2 perp = new(-dir.y, dir.x);

                // Scale offset by spare clearance at this cell
                var cell = world.WorldToCell(path[i]);
                int clear = _grid.GetClearance(cell.x, cell.y);
                float spare = Mathf.Max(0f, (clear - minClearance) * world.CellSize * 0.35f);
                float use = Mathf.Clamp(offset, -spare, spare);
                if (Mathf.Abs(use) < 0.001f) continue;

                Vector2 candidate = path[i] + perp * use;
                var cc = world.WorldToCell(candidate);
                if (!_grid.CanAgentStand(cc.x, cc.y, minClearance)) continue;
                if (world.CircleHitsSolid(candidate, Mathf.Max(0.02f, agent.BodyRadius * 0.75f))) continue;
                path[i] = candidate;
            }
        }

        static float PathLength(List<Vector2> path)
        {
            float len = 0f;
            for (int i = 1; i < path.Count; i++)
                len += Vector2.Distance(path[i - 1], path[i]);
            return len;
        }

        // --- binary min-heap on f-score (stored parallel via recompute from _g + heuristic is hard;
        // we push (index) with f baked into a side array) ---
        float[] _fScore;
        void HeapPush(int node, float f)
        {
            if (_fScore == null || _fScore.Length != _g.Length)
                _fScore = new float[_g.Length];
            _fScore[node] = f;
            _heap[++_heapN] = node;
            int i = _heapN;
            while (i > 1)
            {
                int p = i >> 1;
                if (_fScore[_heap[p]] <= _fScore[_heap[i]]) break;
                int tmp = _heap[p];
                _heap[p] = _heap[i];
                _heap[i] = tmp;
                i = p;
            }
        }

        int HeapPop()
        {
            int root = _heap[1];
            _heap[1] = _heap[_heapN--];
            int i = 1;
            while (true)
            {
                int l = i << 1;
                int r = l + 1;
                if (l > _heapN) break;
                int s = (r <= _heapN && _fScore[_heap[r]] < _fScore[_heap[l]]) ? r : l;
                if (_fScore[_heap[i]] <= _fScore[_heap[s]]) break;
                int tmp = _heap[i];
                _heap[i] = _heap[s];
                _heap[s] = tmp;
                i = s;
            }
            return root;
        }
    }
}
