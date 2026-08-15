using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Shared tunnel navigation: BFS on excavated cells so workers follow corridors
    /// instead of walking into walls / dead-end pockets.
    /// </summary>
    public sealed class ExcavatedPathfinder
    {
        FineTerrainWorld _world;
        readonly List<Vector2> _path = new(128);
        readonly Queue<int> _q = new(1024);
        readonly List<int> _chain = new(64);
        int[] _came;
        int[] _stamp;
        int _gen;
        int _pathI;
        Vector2 _goal;
        float _repathT;
        float _arriveRadius = 0.04f;

        public ExcavatedPathfinder(FineTerrainWorld world) => Bind(world);

        public void Bind(FineTerrainWorld world)
        {
            _world = world;
            Invalidate();
            EnsureBuffers();
        }

        public void Invalidate()
        {
            _path.Clear();
            _pathI = 0;
            _goal = new Vector2(float.NaN, float.NaN);
            _repathT = 0f;
        }

        void EnsureBuffers()
        {
            if (_world == null) return;
            int n = _world.Width * _world.Height;
            if (_came != null && _came.Length == n) return;
            _came = new int[n];
            _stamp = new int[n];
        }

        /// <summary>
        /// Step one frame toward goal along excavated tunnel. Returns true when close enough.
        /// </summary>
        public bool Follow(Vector2 from, Vector2 goal, float moveSpeed, float bodyRadius,
            System.Action<Vector2> face, System.Func<Vector2, float, bool> tryStep)
        {
            if (_world == null) return true;

            float arrive = Mathf.Max(_arriveRadius, bodyRadius * 0.9f);
            if ((goal - from).sqrMagnitude <= arrive * arrive)
            {
                Invalidate();
                return true;
            }

            _repathT -= Time.deltaTime;
            bool goalMoved = float.IsNaN(_goal.x) || (goal - _goal).sqrMagnitude > 0.06f * 0.06f;
            if (_path.Count == 0 || _pathI >= _path.Count || goalMoved || _repathT <= 0f)
            {
                Build(from, goal);
                _goal = goal;
                _repathT = 0.45f;
            }

            Vector2 waypoint = goal;
            if (_path.Count > 0 && _pathI < _path.Count)
            {
                waypoint = _path[_pathI];
                float wpR = Mathf.Max(0.03f, _world.CellSize * 0.55f);
                if ((waypoint - from).sqrMagnitude < wpR * wpR)
                {
                    _pathI++;
                    waypoint = _pathI < _path.Count ? _path[_pathI] : goal;
                }
            }

            Vector2 dir = waypoint - from;
            if (dir.sqrMagnitude < 0.00001f) return false;
            dir.Normalize();
            face?.Invoke(dir);

            float step = moveSpeed * Time.deltaTime;
            if (tryStep != null)
            {
                if (!tryStep(dir, step) &&
                    !tryStep(new Vector2(dir.x, 0f), step) &&
                    !tryStep(new Vector2(0f, dir.y), step))
                {
                    // Side slip then force repath
                    Vector2 perp = new(-dir.y, dir.x);
                    if (!tryStep((dir + perp * 0.7f).normalized, step) &&
                        !tryStep((dir - perp * 0.7f).normalized, step))
                        _repathT = 0f;
                }
            }

            return (goal - from).sqrMagnitude <= arrive * arrive;
        }

        void Build(Vector2 from, Vector2 to)
        {
            _path.Clear();
            _pathI = 0;
            EnsureBuffers();
            if (_world == null || _came == null) return;

            var start = _world.WorldToCell(from);
            var goal = _world.WorldToCell(to);
            if (!_world.InBounds(start.x, start.y)) return;

            if (!_world.IsExcavated(start.x, start.y) || !_world.IsTunnelOpen(start.x, start.y))
                start = NearestExcavated(start.x, start.y);
            if (!_world.InBounds(goal.x, goal.y) || !_world.IsTunnelOpen(goal.x, goal.y))
                goal = NearestExcavated(goal.x, goal.y);
            if (!_world.InBounds(start.x, start.y) || !_world.InBounds(goal.x, goal.y)) return;

            if (start.x == goal.x && start.y == goal.y)
            {
                _path.Add(to);
                return;
            }

            int w = _world.Width;
            int startI = start.y * w + start.x;
            int goalI = goal.y * w + goal.x;
            int goalX = goal.x, goalY = goal.y;

            _gen++;
            if (_gen == int.MaxValue)
            {
                System.Array.Clear(_stamp, 0, _stamp.Length);
                _gen = 1;
            }

            _q.Clear();
            _q.Enqueue(startI);
            _stamp[startI] = _gen;
            _came[startI] = -1;

            bool found = false;
            int bestI = startI;
            int bestDist = Manhattan(start.x, start.y, goalX, goalY);
            int guard = 0;
            const int maxExpand = 16000;

            while (_q.Count > 0 && guard++ < maxExpand)
            {
                int cur = _q.Dequeue();
                int cx = cur % w;
                int cy = cur / w;
                int dGoal = Manhattan(cx, cy, goalX, goalY);
                if (dGoal < bestDist)
                {
                    bestDist = dGoal;
                    bestI = cur;
                }

                if (cur == goalI)
                {
                    found = true;
                    bestI = cur;
                    break;
                }

                Enqueue(cx + 1, cy, cur, w);
                Enqueue(cx - 1, cy, cur, w);
                Enqueue(cx, cy + 1, cur, w);
                Enqueue(cx, cy - 1, cur, w);
            }

            // If goal unreachable (blocked / disconnected), path to closest reachable cell
            int endI = found ? goalI : bestI;

            _chain.Clear();
            for (int at = endI; at >= 0; at = _came[at])
            {
                _chain.Add(at);
                if (at == startI) break;
            }
            _chain.Reverse();

            for (int i = 0; i < _chain.Count; i++)
            {
                // Thin waypoints for smoother follow
                if (i != 0 && i != _chain.Count - 1 && (i % 2) != 0) continue;
                int idx = _chain[i];
                _path.Add(_world.CellCenter(idx % w, idx / w));
            }

            if (found)
            {
                if (_path.Count == 0 || (_path[_path.Count - 1] - to).sqrMagnitude > 0.0001f)
                    _path.Add(to);
            }
        }

        void Enqueue(int x, int y, int from, int w)
        {
            if (!_world.InBounds(x, y) || !_world.IsTunnelOpen(x, y)) return;
            int i = y * w + x;
            if (_stamp[i] == _gen) return;
            _stamp[i] = _gen;
            _came[i] = from;
            _q.Enqueue(i);
        }

        Vector2Int NearestExcavated(int x, int y)
        {
            Vector2Int best = new(x, y);
            int bestD = int.MaxValue;
            for (int r = 0; r <= 12; r++)
            {
                for (int oy = -r; oy <= r; oy++)
                for (int ox = -r; ox <= r; ox++)
                {
                    if (Mathf.Abs(ox) != r && Mathf.Abs(oy) != r) continue;
                    int nx = x + ox, ny = y + oy;
                    if (!_world.InBounds(nx, ny) || !_world.IsTunnelOpen(nx, ny)) continue;
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

        static int Manhattan(int ax, int ay, int bx, int by) =>
            Mathf.Abs(ax - bx) + Mathf.Abs(ay - by);
    }
}
