using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    public enum ExcavationPlanCellState : byte
    {
        Valid = 0,
        Invalid = 1,
        Excavating = 2,
        Completed = 3,
    }

    /// <summary>One painted excavation cell — spatial plan truth (tile coords).</summary>
    public struct ExcavationPlanCell
    {
        public int X;
        public int Y;
        public byte Width;
        public ExcavationPlanCellState State;

        public long Key => ((long)Y << 32) ^ (uint)X;
        public static long MakeKey(int x, int y) => ((long)y << 32) ^ (uint)x;
    }

    /// <summary>
    /// Excavator Tile-Paint Routing V1 — stroke → connected cell plan.
    /// Does not dig; FreeWorkerController consumes the plan.
    /// </summary>
    public sealed class ExcavatorTilePaintPlan
    {
        readonly Dictionary<long, ExcavationPlanCell> _cells = new(512);
        readonly List<Vector2Int> _strokeCenter = new(256);
        readonly HashSet<long> _strokePaintScratch = new(512);
        readonly HashSet<long> _reachableScratch = new(2048);
        readonly Dictionary<long, int> _openDistScratch = new(4096);
        readonly Queue<Vector2Int> _bfs = new(512);

        FineTerrainWorld _world;
        int _brushWidth = TunnelWidthSpec.Standard;
        bool _stroking;
        Vector2Int _lastSample = new(int.MinValue, int.MinValue);
        int _strokeId;

        public int BrushWidth
        {
            get => _brushWidth;
            set => _brushWidth = TunnelWidthSpec.Clamp(value);
        }

        public int CellCount => _cells.Count;

        /// <summary>Valid plan cells not yet started.</summary>
        public int PendingValidCount
        {
            get
            {
                int n = 0;
                foreach (var kv in _cells)
                    if (kv.Value.State == ExcavationPlanCellState.Valid) n++;
                return n;
            }
        }

        /// <summary>Valid + Excavating — real remaining dig work (not Completed/Invalid).</summary>
        public int PendingDigCount
        {
            get
            {
                int n = 0;
                foreach (var kv in _cells)
                {
                    var s = kv.Value.State;
                    if (s == ExcavationPlanCellState.Valid
                        || s == ExcavationPlanCellState.Excavating)
                        n++;
                }
                return n;
            }
        }

        public bool HasPlan => _cells.Count > 0;
        public bool IsStroking => _stroking;
        public int StrokeId => _strokeId;

        public void Bind(FineTerrainWorld world) => _world = world;

        public void Clear()
        {
            _cells.Clear();
            _strokeCenter.Clear();
            _strokePaintScratch.Clear();
            _stroking = false;
            _lastSample = new Vector2Int(int.MinValue, int.MinValue);
        }

        public bool TryGet(int x, int y, out ExcavationPlanCell cell) =>
            _cells.TryGetValue(ExcavationPlanCell.MakeKey(x, y), out cell);

        public bool IsPendingDig(int x, int y) =>
            _cells.TryGetValue(ExcavationPlanCell.MakeKey(x, y), out var c)
            && (c.State == ExcavationPlanCellState.Valid || c.State == ExcavationPlanCellState.Excavating);

        public IEnumerable<ExcavationPlanCell> AllCells() => _cells.Values;

        public void BeginStroke(Vector2 worldPos)
        {
            if (_world == null) return;
            _stroking = true;
            _strokeCenter.Clear();
            _strokePaintScratch.Clear();
            _lastSample = new Vector2Int(int.MinValue, int.MinValue);
            SampleStroke(worldPos);
        }

        public void SampleStroke(Vector2 worldPos)
        {
            if (!_stroking || _world == null) return;
            var cell = _world.WorldToCell(worldPos);
            if (!_world.InBounds(cell.x, cell.y)) return;
            if (cell == _lastSample) return;

            if (_lastSample.x == int.MinValue)
            {
                _strokeCenter.Add(cell);
                _lastSample = cell;
                RebuildStrokePreview();
                return;
            }

            // Supercover / Bresenham interpolation — no gaps on fast drags
            AppendLineCells(_lastSample, cell, _strokeCenter);
            _lastSample = cell;
            RebuildStrokePreview();
        }

        public void CancelStroke()
        {
            _stroking = false;
            _strokeCenter.Clear();
            _strokePaintScratch.Clear();
            _lastSample = new Vector2Int(int.MinValue, int.MinValue);
        }

        /// <summary>Commit preview stroke into the persistent plan. Returns cells newly accepted.</summary>
        public int CommitStroke(Vector2 excavatorWorld, ICollection<long> existingOpenSeeds = null)
        {
            if (!_stroking || _world == null)
            {
                CancelStroke();
                return 0;
            }

            RebuildStrokePreview();
            int added = 0;
            byte w = (byte)_brushWidth;

            // Classify connectivity against open terrain + existing valid plan
            BuildReachableSeeds(excavatorWorld, existingOpenSeeds);

            foreach (long key in _strokePaintScratch)
            {
                int x = (int)(key & 0xffffffff);
                int y = (int)(key >> 32);
                if (!_world.InBounds(x, y)) continue;

                var state = ClassifyCell(x, y);
                if (state == ExcavationPlanCellState.Completed)
                    continue; // already open — not a dig order

                if (!_reachableScratch.Contains(key) && state == ExcavationPlanCellState.Valid)
                    state = ExcavationPlanCellState.Invalid;

                if (_cells.TryGetValue(key, out var existing))
                {
                    if (existing.State == ExcavationPlanCellState.Completed) continue;
                    // Upgrade invalid→valid if now connected; keep excavating
                    if (existing.State == ExcavationPlanCellState.Excavating) continue;
                    existing.Width = w;
                    existing.State = state;
                    _cells[key] = existing;
                    added++;
                    continue;
                }

                _cells[key] = new ExcavationPlanCell
                {
                    X = x,
                    Y = y,
                    Width = w,
                    State = state,
                };
                added++;
            }

            _strokeId++;
            CancelStroke();
            RevalidateConnectivity(excavatorWorld);
            return added;
        }

        /// <summary>RMB erase: remove planned cells under brush that are not yet excavated.</summary>
        public int EraseAt(Vector2 worldPos, int brushWidth)
        {
            if (_world == null) return 0;
            int w = TunnelWidthSpec.Clamp(brushWidth);
            var c = _world.WorldToCell(worldPos);
            int removed = 0;
            float half = (w - 1) * 0.5f;
            int r = Mathf.CeilToInt(half + 0.01f);
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx * dx + dy * dy > (half + 0.6f) * (half + 0.6f)) continue;
                int x = c.x + dx, y = c.y + dy;
                long key = ExcavationPlanCell.MakeKey(x, y);
                if (!_cells.TryGetValue(key, out var cell)) continue;
                if (cell.State == ExcavationPlanCellState.Completed) continue;
                if (_world.IsExcavated(x, y)) continue;
                _cells.Remove(key);
                removed++;
            }
            return removed;
        }

        public void MarkExcavating(int x, int y)
        {
            long key = ExcavationPlanCell.MakeKey(x, y);
            if (!_cells.TryGetValue(key, out var c)) return;
            if (c.State == ExcavationPlanCellState.Completed) return;
            c.State = ExcavationPlanCellState.Excavating;
            _cells[key] = c;
        }

        public void NotifyCellExcavated(int x, int y)
        {
            long key = ExcavationPlanCell.MakeKey(x, y);
            if (!_cells.TryGetValue(key, out var c)) return;
            c.State = ExcavationPlanCellState.Completed;
            _cells[key] = c;
        }

        public void RevalidateConnectivity(Vector2 excavatorWorld)
        {
            if (_world == null) return;
            BuildReachableSeeds(excavatorWorld, null);
            // GrowThroughPlan already ran inside BuildReachableSeeds

            var keys = new List<long>(_cells.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                long key = keys[i];
                var c = _cells[key];
                if (c.State == ExcavationPlanCellState.Completed)
                    continue;

                if (_world.IsExcavated(c.X, c.Y) || _world.IsTunnelOpen(c.X, c.Y))
                {
                    c.State = ExcavationPlanCellState.Completed;
                    _cells[key] = c;
                    continue;
                }

                var geo = ClassifyCell(c.X, c.Y);
                if (geo == ExcavationPlanCellState.Invalid)
                {
                    c.State = ExcavationPlanCellState.Invalid;
                    _cells[key] = c;
                    continue;
                }

                // Reclassify Excavating too — prevents a stranded excavating face from soft-locking
                c.State = _reachableScratch.Contains(key)
                    ? (c.State == ExcavationPlanCellState.Excavating
                        ? ExcavationPlanCellState.Excavating
                        : ExcavationPlanCellState.Valid)
                    : ExcavationPlanCellState.Invalid;
                _cells[key] = c;
            }
        }

        /// <summary>
        /// Workable excavation front: pending painted rock that has an open standing cell
        /// the excavator can reach through EXISTING traversable terrain only.
        /// Does NOT treat painted cells as walkable — no inventing connecting tunnels.
        /// </summary>
        public bool TryPickAccessibleWorkFront(
            Vector2 fromWorld,
            int minClearance,
            out int workX,
            out int workY,
            out int width,
            out Vector2 standWorld)
        {
            workX = workY = -1;
            width = _brushWidth;
            standWorld = fromWorld;
            if (_world == null || _cells.Count == 0) return false;

            var nav = _world.Navigation;
            if (nav == null) return false;
            if (!nav.IsBuilt) nav.Rebuild();

            BuildOpenReachability(fromWorld, minClearance, out var openDist);

            float best = float.MaxValue;
            int bestWorkX = -1, bestWorkY = -1, bestW = _brushWidth;
            int bestStandX = -1, bestStandY = -1;

            foreach (var kv in _cells)
            {
                var c = kv.Value;
                if (c.State != ExcavationPlanCellState.Valid
                    && c.State != ExcavationPlanCellState.Excavating)
                    continue;

                if (!_world.IsMovementBlocker(c.X, c.Y) && !_world.HasBlockingDebris(c.X, c.Y))
                {
                    var done = c;
                    done.State = ExcavationPlanCellState.Completed;
                    _cells[kv.Key] = done;
                    continue;
                }

                // Workable front requires adjacency to open floor
                if (!AdjacentToOpen(c.X, c.Y)) continue;

                for (int i = 0; i < 4; i++)
                {
                    int sx = c.X + (i == 0 ? 1 : i == 1 ? -1 : 0);
                    int sy = c.Y + (i == 2 ? 1 : i == 3 ? -1 : 0);
                    if (!_world.InBounds(sx, sy)) continue;
                    // Stand must be open tunnel (nav-walkable), not merely excavated-with-HP
                    if (!_world.IsTunnelOpen(sx, sy)) continue;
                    if (_world.HasBlockingDebris(sx, sy)) continue;
                    if (!nav.CanAgentStand(sx, sy, minClearance)) continue;

                    long sk = ExcavationPlanCell.MakeKey(sx, sy);
                    if (!openDist.TryGetValue(sk, out int dist)) continue;

                    // Prefer closer stands; slight bias to keep excavating current face
                    float score = dist;
                    if (c.State == ExcavationPlanCellState.Excavating) score -= 0.5f;
                    if (score < best)
                    {
                        best = score;
                        bestWorkX = c.X;
                        bestWorkY = c.Y;
                        bestW = c.Width;
                        bestStandX = sx;
                        bestStandY = sy;
                    }
                }
            }

            if (bestWorkX < 0) return false;
            workX = bestWorkX;
            workY = bestWorkY;
            width = bestW;
            standWorld = _world.CellCenter(bestStandX, bestStandY);
            return true;
        }

        /// <summary>
        /// BFS through open/excavated floor only — never through solid or pending paint.
        /// </summary>
        void BuildOpenReachability(Vector2 fromWorld, int minClearance, out Dictionary<long, int> openDist)
        {
            openDist = _openDistScratch;
            openDist.Clear();
            _bfs.Clear();
            if (_world == null) return;
            var nav = _world.Navigation;
            if (nav == null) return;

            var start = _world.WorldToCell(fromWorld);
            if (!nav.CanAgentStand(start.x, start.y, minClearance))
                start = nav.NearestStandable(start.x, start.y, minClearance);

            if (nav.CanAgentStand(start.x, start.y, minClearance))
            {
                long sk0 = ExcavationPlanCell.MakeKey(start.x, start.y);
                openDist[sk0] = 0;
                _bfs.Enqueue(start);
            }
            else
            {
                // Chassis center may sit on a non-standable tile while the body is in open
                // tunnel — seed nearby open stands so access BFS is not empty.
                int r = 4;
                int x0 = Mathf.Max(0, start.x - r);
                int x1 = Mathf.Min(_world.Width - 1, start.x + r);
                int y0 = Mathf.Max(0, start.y - r);
                int y1 = Mathf.Min(_world.Height - 1, start.y + r);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!nav.CanAgentStand(x, y, minClearance)) continue;
                    long sk = ExcavationPlanCell.MakeKey(x, y);
                    if (openDist.ContainsKey(sk)) continue;
                    openDist[sk] = 0;
                    _bfs.Enqueue(new Vector2Int(x, y));
                }
                if (_bfs.Count == 0) return;
            }

            while (_bfs.Count > 0)
            {
                var p = _bfs.Dequeue();
                long pk = ExcavationPlanCell.MakeKey(p.x, p.y);
                if (!openDist.TryGetValue(pk, out int d0)) continue;
                for (int i = 0; i < 4; i++)
                {
                    int nx = p.x + (i == 0 ? 1 : i == 1 ? -1 : 0);
                    int ny = p.y + (i == 2 ? 1 : i == 3 ? -1 : 0);
                    if (!_world.InBounds(nx, ny)) continue;
                    // Walkable open tunnel only — matches CanAgentStand / nav grid
                    if (!_world.IsTunnelOpen(nx, ny)) continue;
                    if (_world.HasBlockingDebris(nx, ny)) continue;
                    if (!nav.CanAgentStand(nx, ny, minClearance)) continue;
                    long nk = ExcavationPlanCell.MakeKey(nx, ny);
                    if (openDist.ContainsKey(nk)) continue;
                    openDist[nk] = d0 + 1;
                    _bfs.Enqueue(new Vector2Int(nx, ny));
                }
            }
        }

        /// <summary>
        /// Legacy nearest-pending picker. Prefer <see cref="TryPickAccessibleWorkFront"/> —
        /// this grows through painted cells and is NOT open-tunnel access.
        /// </summary>
        public bool TryPickNextWorkCell(Vector2 fromWorld, out int x, out int y, out int width)
        {
            x = y = -1;
            width = _brushWidth;
            if (_world == null || _cells.Count == 0) return false;

            BuildReachableSeeds(fromWorld, null);
            GrowThroughPlan();

            float best = float.MaxValue;
            int bestX = -1, bestY = -1;
            int bestW = _brushWidth;
            Vector2 from = fromWorld;

            foreach (var kv in _cells)
            {
                var c = kv.Value;
                if (c.State != ExcavationPlanCellState.Valid && c.State != ExcavationPlanCellState.Excavating)
                    continue;
                if (!_world.IsMovementBlocker(c.X, c.Y) && !_world.HasBlockingDebris(c.X, c.Y))
                {
                    // Already clear — complete
                    var done = c;
                    done.State = ExcavationPlanCellState.Completed;
                    _cells[kv.Key] = done;
                    continue;
                }
                if (!_reachableScratch.Contains(kv.Key)) continue;

                // Prefer cells on the frontier (adjacent to open terrain)
                bool frontier = AdjacentToOpen(c.X, c.Y);
                Vector2 cc = _world.CellCenter(c.X, c.Y);
                float d = Vector2.Distance(from, cc);
                float score = d + (frontier ? 0f : 8f);
                if (score < best)
                {
                    best = score;
                    bestX = c.X;
                    bestY = c.Y;
                    bestW = c.Width;
                }
            }

            if (bestX < 0) return false;
            x = bestX;
            y = bestY;
            width = bestW;
            return true;
        }

        /// <summary>Preview cells for current in-progress stroke (not yet committed).</summary>
        public void GetStrokePreview(List<(int x, int y, bool valid)> into, Vector2 excavatorWorld)
        {
            into.Clear();
            if (!_stroking || _world == null) return;
            BuildReachableSeeds(excavatorWorld, null);
            // Temporary grow through preview
            foreach (long key in _strokePaintScratch)
            {
                if (_reachableScratch.Contains(key)) continue;
                // allow preview adjacency through other preview cells
            }
            GrowSetThroughKeys(_strokePaintScratch);

            foreach (long key in _strokePaintScratch)
            {
                int x = (int)(key & 0xffffffff);
                int y = (int)(key >> 32);
                var state = ClassifyCell(x, y);
                bool valid = state == ExcavationPlanCellState.Valid && _reachableScratch.Contains(key);
                if (state == ExcavationPlanCellState.Completed)
                    continue;
                into.Add((x, y, valid));
            }
        }

        // ——— Stroke geometry ———

        void RebuildStrokePreview()
        {
            _strokePaintScratch.Clear();
            if (_strokeCenter.Count == 0) return;

            // Dilate centerline into corridor of BrushWidth
            DilateCenterline(_strokeCenter, _brushWidth, _strokePaintScratch);
        }

        public static void AppendLineCells(Vector2Int a, Vector2Int b, List<Vector2Int> into)
        {
            // Amanatides & Woo style supercover — continuous cells, no diagonal gaps
            int x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;
            if (into.Count == 0 || into[into.Count - 1] != a)
                into.Add(a);
            if (x0 == x1 && y0 == y1) return;

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int x = x0, y = y0;

            if (dx >= dy)
            {
                int err = dx;
                while (x != x1)
                {
                    err -= dy * 2;
                    x += sx;
                    if (err < 0)
                    {
                        y += sy;
                        err += dx * 2;
                        // Include both orthogonal neighbors when stepping diagonally
                        AddUnique(into, new Vector2Int(x - sx, y));
                        AddUnique(into, new Vector2Int(x, y - sy));
                    }
                    AddUnique(into, new Vector2Int(x, y));
                }
            }
            else
            {
                int err = dy;
                while (y != y1)
                {
                    err -= dx * 2;
                    y += sy;
                    if (err < 0)
                    {
                        x += sx;
                        err += dy * 2;
                        AddUnique(into, new Vector2Int(x - sx, y));
                        AddUnique(into, new Vector2Int(x, y - sy));
                    }
                    AddUnique(into, new Vector2Int(x, y));
                }
            }

            AddUnique(into, b);
        }

        static void AddUnique(List<Vector2Int> into, Vector2Int p)
        {
            if (into.Count == 0 || into[into.Count - 1] != p)
                into.Add(p);
        }

        /// <summary>
        /// Corridor dilation around centerline using BrushCells(widthClass).
        /// Width class 1–5 → 3/5/8/12/16 cell passages.
        /// Even widths use asymmetric halfLo/halfHi so the corridor does not wobble.
        /// </summary>
        public static void DilateCenterline(List<Vector2Int> center, int widthClass, HashSet<long> into)
        {
            int cls = TunnelWidthSpec.Clamp(widthClass);
            int cells = TunnelWidthSpec.BrushCells(cls);
            // Deterministic perpendicular extents (even: one extra cell on +perp)
            int halfLo = (cells - 1) / 2;
            int halfHi = cells / 2;

            for (int i = 0; i < center.Count; i++)
            {
                Vector2Int p = center[i];
                Vector2 perp = Vector2.right;
                if (i + 1 < center.Count)
                {
                    Vector2Int q = center[i + 1];
                    Vector2 dir = new(q.x - p.x, q.y - p.y);
                    if (dir.sqrMagnitude > 0.01f)
                    {
                        dir.Normalize();
                        perp = new Vector2(-dir.y, dir.x);
                        // Deterministic perp sign: prefer +X, then +Y
                        if (perp.x < -0.01f || (Mathf.Abs(perp.x) < 0.01f && perp.y < 0f))
                            perp = -perp;
                    }
                }
                else if (i > 0)
                {
                    Vector2Int prev = center[i - 1];
                    Vector2 dir = new(p.x - prev.x, p.y - prev.y);
                    if (dir.sqrMagnitude > 0.01f)
                    {
                        dir.Normalize();
                        perp = new Vector2(-dir.y, dir.x);
                        if (perp.x < -0.01f || (Mathf.Abs(perp.x) < 0.01f && perp.y < 0f))
                            perp = -perp;
                    }
                }

                StampPerp(p.x, p.y, perp, halfLo, halfHi, into);

                if (i + 1 >= center.Count) continue;
                Vector2Int qn = center[i + 1];
                Vector2 seg = new(qn.x - p.x, qn.y - p.y);
                float len = seg.magnitude;
                if (len < 0.01f) continue;
                Vector2 dirN = seg / len;
                Vector2 perpN = new(-dirN.y, dirN.x);
                if (perpN.x < -0.01f || (Mathf.Abs(perpN.x) < 0.01f && perpN.y < 0f))
                    perpN = -perpN;

                int steps = Mathf.Max(1, Mathf.CeilToInt(len * 2f));
                for (int s = 0; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    float cx = p.x + (qn.x - p.x) * t;
                    float cy = p.y + (qn.y - p.y) * t;
                    StampPerpFloat(cx, cy, perpN, halfLo, halfHi, into);
                }

                // Corner interior fill (previous→p→next) — fill gap, avoid outer bulge
                if (i > 0)
                {
                    Vector2Int prev = center[i - 1];
                    Vector2 d0 = new(p.x - prev.x, p.y - prev.y);
                    Vector2 d1 = new(qn.x - p.x, qn.y - p.y);
                    if (d0.sqrMagnitude > 0.01f && d1.sqrMagnitude > 0.01f)
                    {
                        d0.Normalize();
                        d1.Normalize();
                        float cross = d0.x * d1.y - d0.y * d1.x;
                        if (Mathf.Abs(cross) > 0.2f)
                        {
                            Vector2 bis = d0 + d1;
                            if (bis.sqrMagnitude > 0.01f)
                            {
                                bis.Normalize();
                                Vector2 inward = cross > 0
                                    ? new Vector2(-bis.y, bis.x)
                                    : new Vector2(bis.y, -bis.x);
                                int fillMax = halfLo;
                                for (int o = 1; o <= fillMax; o++)
                                {
                                    int ix = Mathf.RoundToInt(p.x + inward.x * o);
                                    int iy = Mathf.RoundToInt(p.y + inward.y * o);
                                    into.Add(ExcavationPlanCell.MakeKey(ix, iy));
                                }
                            }
                        }
                    }
                }
            }
        }

        static void StampPerp(int cx, int cy, Vector2 perp, int halfLo, int halfHi, HashSet<long> into)
        {
            for (int o = -halfLo; o <= halfHi; o++)
            {
                int ix = Mathf.RoundToInt(cx + perp.x * o);
                int iy = Mathf.RoundToInt(cy + perp.y * o);
                into.Add(ExcavationPlanCell.MakeKey(ix, iy));
            }
        }

        static void StampPerpFloat(float cx, float cy, Vector2 perp, int halfLo, int halfHi, HashSet<long> into)
        {
            for (int o = -halfLo; o <= halfHi; o++)
            {
                int ix = Mathf.RoundToInt(cx + perp.x * o);
                int iy = Mathf.RoundToInt(cy + perp.y * o);
                into.Add(ExcavationPlanCell.MakeKey(ix, iy));
            }
        }

        ExcavationPlanCellState ClassifyCell(int x, int y)
        {
            if (_world == null || !_world.InBounds(x, y))
                return ExcavationPlanCellState.Invalid;
            if (_world.IsExcavated(x, y) || _world.IsTunnelOpen(x, y))
                return ExcavationPlanCellState.Completed;

            var cell = _world.Get(x, y);
            if (cell.IsUndamageableBorder)
                return ExcavationPlanCellState.Invalid;

            if (_world.IsMovementBlocker(x, y) || _world.HasBlockingDebris(x, y))
                return ExcavationPlanCellState.Valid;

            return ExcavationPlanCellState.Invalid;
        }

        void BuildReachableSeeds(Vector2 excavatorWorld, ICollection<long> extraSeeds)
        {
            _reachableScratch.Clear();
            _bfs.Clear();
            if (_world == null) return;

            var ec = _world.WorldToCell(excavatorWorld);
            TrySeed(ec.x, ec.y);

            const int seedR = 48;
            int x0 = Mathf.Max(0, ec.x - seedR);
            int x1 = Mathf.Min(_world.Width - 1, ec.x + seedR);
            int y0 = Mathf.Max(0, ec.y - seedR);
            int y1 = Mathf.Min(_world.Height - 1, ec.y + seedR);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (_world.IsTunnelOpen(x, y) || _world.IsExcavated(x, y))
                    TrySeed(x, y);
            }

            if (extraSeeds != null)
            {
                foreach (long k in extraSeeds)
                {
                    int x = (int)(k & 0xffffffff);
                    int y = (int)(k >> 32);
                    TrySeed(x, y);
                }
            }

            foreach (var kv in _cells)
            {
                if (kv.Value.State == ExcavationPlanCellState.Valid
                    || kv.Value.State == ExcavationPlanCellState.Excavating
                    || kv.Value.State == ExcavationPlanCellState.Completed)
                {
                    if (AdjacentToOpen(kv.Value.X, kv.Value.Y) || _reachableScratch.Contains(kv.Key))
                        TrySeed(kv.Value.X, kv.Value.Y);
                }
            }

            GrowThroughPlan();
        }

        void TrySeed(int x, int y)
        {
            if (_world == null || !_world.InBounds(x, y)) return;
            long key = ExcavationPlanCell.MakeKey(x, y);
            if (!_reachableScratch.Add(key)) return;
            _bfs.Enqueue(new Vector2Int(x, y));
        }

        void GrowThroughPlan()
        {
            while (_bfs.Count > 0)
            {
                var p = _bfs.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    int nx = p.x + (i == 0 ? 1 : i == 1 ? -1 : 0);
                    int ny = p.y + (i == 2 ? 1 : i == 3 ? -1 : 0);
                    if (_world == null || !_world.InBounds(nx, ny)) continue;
                    long key = ExcavationPlanCell.MakeKey(nx, ny);
                    if (_reachableScratch.Contains(key)) continue;

                    bool open = _world.IsTunnelOpen(nx, ny) || _world.IsExcavated(nx, ny);
                    bool planned = _cells.TryGetValue(key, out var pc)
                                   && (pc.State == ExcavationPlanCellState.Valid
                                       || pc.State == ExcavationPlanCellState.Excavating
                                       || pc.State == ExcavationPlanCellState.Completed);
                    bool preview = _strokePaintScratch.Contains(key)
                                   && ClassifyCell(nx, ny) != ExcavationPlanCellState.Invalid;

                    if (!open && !planned && !preview) continue;
                    _reachableScratch.Add(key);
                    _bfs.Enqueue(new Vector2Int(nx, ny));
                }
            }
        }

        void GrowSetThroughKeys(HashSet<long> keys)
        {
            _bfs.Clear();
            foreach (long k in _reachableScratch)
                _bfs.Enqueue(new Vector2Int((int)(k & 0xffffffff), (int)(k >> 32)));

            while (_bfs.Count > 0)
            {
                var p = _bfs.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    int nx = p.x + (i == 0 ? 1 : i == 1 ? -1 : 0);
                    int ny = p.y + (i == 2 ? 1 : i == 3 ? -1 : 0);
                    if (_world == null || !_world.InBounds(nx, ny)) continue;
                    long key = ExcavationPlanCell.MakeKey(nx, ny);
                    if (_reachableScratch.Contains(key)) continue;
                    bool open = _world.IsTunnelOpen(nx, ny) || _world.IsExcavated(nx, ny);
                    if (!open && !keys.Contains(key)) continue;
                    if (!open && ClassifyCell(nx, ny) == ExcavationPlanCellState.Invalid) continue;
                    _reachableScratch.Add(key);
                    _bfs.Enqueue(new Vector2Int(nx, ny));
                }
            }
        }

        bool AdjacentToOpen(int x, int y)
        {
            if (_world == null) return false;
            for (int i = 0; i < 4; i++)
            {
                int nx = x + (i == 0 ? 1 : i == 1 ? -1 : 0);
                int ny = y + (i == 2 ? 1 : i == 3 ? -1 : 0);
                if (!_world.InBounds(nx, ny)) continue;
                if (_world.IsTunnelOpen(nx, ny) || _world.IsExcavated(nx, ny))
                    return true;
            }
            return false;
        }

        /// <summary>Half-width in world units from width CLASS (uses BrushCells, not class-as-count).</summary>
        public static float PaintDigHalfWorld(int widthClass, float cellSize)
        {
            return TunnelWidthSpec.BrushHalfCells(widthClass) * cellSize;
        }
    }

    /// <summary>Batched translucent quad mesh for planned excavation cells.</summary>
    public sealed class ExcavatorPaintPreview
    {
        MeshFilter _mf;
        MeshRenderer _mr;
        Mesh _mesh;
        readonly List<Vector3> _verts = new(1024);
        readonly List<Color32> _cols = new(1024);
        readonly List<int> _tris = new(2048);
        Transform _root;
        bool _visible = true;

        // Subtle brush-width footprint ring (planning only)
        LineRenderer _widthRing;
        const int WidthRingSegments = 56;
        // Matches ColEdge cyan/teal — ring only, very low opacity
        static readonly Color WidthRingColor = new(0.42f, 0.82f, 0.88f, 0.16f);

        // Blueprint overlay — quiet cyan/teal; invalid slightly stronger; committed quieter than live preview
        static readonly Color32 ColValid = new(70, 190, 200, 28);
        static readonly Color32 ColInvalid = new(220, 75, 65, 55);
        static readonly Color32 ColExcavating = new(255, 190, 70, 48);
        static readonly Color32 ColDone = new(90, 110, 130, 12);
        static readonly Color32 ColPreviewOk = new(90, 210, 230, 42);
        static readonly Color32 ColPreviewBad = new(255, 90, 70, 62);
        static readonly Color32 ColEdge = new(120, 210, 220, 50);

        public void Setup(Transform parent)
        {
            var go = new GameObject("ExcavationPaintPreview");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            _root = go.transform;
            _mf = go.AddComponent<MeshFilter>();
            _mr = go.AddComponent<MeshRenderer>();
            _mesh = new Mesh { name = "ExcavationPaintMesh" };
            _mesh.MarkDynamic();
            _mf.sharedMesh = _mesh;
            var sh = Shader.Find("Sprites/Default");
            if (sh != null)
            {
                _mr.sharedMaterial = new Material(sh) { color = Color.white };
            }
            _mr.sortingOrder = 33;
            EnsureWidthRing();
        }

        void EnsureWidthRing()
        {
            if (_widthRing != null || _root == null) return;
            var ringGo = new GameObject("BrushWidthRing");
            ringGo.transform.SetParent(_root, false);
            _widthRing = ringGo.AddComponent<LineRenderer>();
            _widthRing.useWorldSpace = true;
            _widthRing.loop = true;
            _widthRing.positionCount = WidthRingSegments;
            _widthRing.widthMultiplier = 1f;
            // Hairline — must not obscure geology / blueprint fills
            float hair = 0.012f;
            _widthRing.startWidth = hair;
            _widthRing.endWidth = hair;
            _widthRing.numCornerVertices = 2;
            _widthRing.numCapVertices = 2;
            _widthRing.sortingOrder = 34;
            var sh = Shader.Find("Sprites/Default");
            if (sh != null)
                _widthRing.material = new Material(sh) { color = Color.white };
            _widthRing.startColor = _widthRing.endColor = WidthRingColor;
            _widthRing.enabled = false;
        }

        /// <summary>
        /// Faint ring = actual dig footprint radius (BrushHalfCells × cellSize = DigHalfCells).
        /// Ring only; planning/paint hover only.
        /// </summary>
        public void UpdateBrushWidthRing(Vector2 centerWorld, float cellSize, int widthClass, bool show)
        {
            EnsureWidthRing();
            if (_widthRing == null) return;

            bool on = show && _visible && cellSize > 0.0001f;
            _widthRing.enabled = on;
            if (!on) return;

            int w = TunnelWidthSpec.Clamp(widthClass);
            // Exact excavation half-width used by paint dig envelope / machine geometry
            float radius = TunnelWidthSpec.BrushHalfCells(w) * cellSize;
            Debug.Assert(
                Mathf.Approximately(radius, TunnelWidthSpec.DigHalfCells(w) * cellSize),
                "Width ring must match DigHalfCells / paint dig envelope");

            // Slightly thinner hairline on larger brushes so W5 stays quiet
            float hair = Mathf.Lerp(0.011f, 0.009f, (w - 1) / 4f);
            _widthRing.startWidth = hair;
            _widthRing.endWidth = hair;
            _widthRing.startColor = _widthRing.endColor = WidthRingColor;

            float z = _root != null ? _root.position.z - 0.01f : -0.03f;
            for (int i = 0; i < WidthRingSegments; i++)
            {
                float a = (i / (float)WidthRingSegments) * Mathf.PI * 2f;
                _widthRing.SetPosition(i, new Vector3(
                    centerWorld.x + Mathf.Cos(a) * radius,
                    centerWorld.y + Mathf.Sin(a) * radius,
                    z));
            }
        }

        public void SetVisible(bool v)
        {
            _visible = v;
            if (_root != null) _root.gameObject.SetActive(v);
            if (!v && _widthRing != null)
                _widthRing.enabled = false;
        }

        public void Rebuild(FineTerrainWorld world, ExcavatorTilePaintPlan plan,
            List<(int x, int y, bool valid)> strokePreview)
        {
            _verts.Clear();
            _cols.Clear();
            _tris.Clear();
            if (world == null || !_visible)
            {
                if (_mesh != null) _mesh.Clear();
                return;
            }

            float cs = world.CellSize;
            float pad = cs * 0.12f; // inset fill so geology shows through
            float edge = cs * 0.04f;

            if (plan != null)
            {
                foreach (var c in plan.AllCells())
                {
                    Color32 col = c.State switch
                    {
                        ExcavationPlanCellState.Valid => ColValid,
                        ExcavationPlanCellState.Invalid => ColInvalid,
                        ExcavationPlanCellState.Excavating => ColExcavating,
                        ExcavationPlanCellState.Completed => ColDone,
                        _ => ColValid,
                    };
                    AddQuad(c.X, c.Y, cs, pad, col);
                    if (c.State == ExcavationPlanCellState.Valid
                        || c.State == ExcavationPlanCellState.Excavating
                        || c.State == ExcavationPlanCellState.Invalid)
                        AddEdgeFrame(c.X, c.Y, cs, edge, ColEdge);
                }
            }

            if (strokePreview != null)
            {
                for (int i = 0; i < strokePreview.Count; i++)
                {
                    var s = strokePreview[i];
                    AddQuad(s.x, s.y, cs, pad * 0.7f, s.valid ? ColPreviewOk : ColPreviewBad);
                    AddEdgeFrame(s.x, s.y, cs, edge, s.valid ? ColEdge : ColPreviewBad);
                }
            }

            _mesh.Clear();
            if (_verts.Count == 0) return;
            _mesh.SetVertices(_verts);
            _mesh.SetColors(_cols);
            _mesh.SetTriangles(_tris, 0);
            _mesh.RecalculateBounds();
        }

        void AddQuad(int x, int y, float cs, float pad, Color32 col)
        {
            int vi = _verts.Count;
            float x0 = x * cs + pad;
            float y0 = y * cs + pad;
            float x1 = (x + 1) * cs - pad;
            float y1 = (y + 1) * cs - pad;
            _verts.Add(new Vector3(x0, y0, 0f));
            _verts.Add(new Vector3(x0, y1, 0f));
            _verts.Add(new Vector3(x1, y1, 0f));
            _verts.Add(new Vector3(x1, y0, 0f));
            _cols.Add(col);
            _cols.Add(col);
            _cols.Add(col);
            _cols.Add(col);
            _tris.Add(vi);
            _tris.Add(vi + 1);
            _tris.Add(vi + 2);
            _tris.Add(vi);
            _tris.Add(vi + 2);
            _tris.Add(vi + 3);
        }

        void AddEdgeFrame(int x, int y, float cs, float t, Color32 col)
        {
            float x0 = x * cs;
            float y0 = y * cs;
            float x1 = (x + 1) * cs;
            float y1 = (y + 1) * cs;
            // Four thin edge quads
            AddRawQuad(x0, y0, x1, y0 + t, col);
            AddRawQuad(x0, y1 - t, x1, y1, col);
            AddRawQuad(x0, y0, x0 + t, y1, col);
            AddRawQuad(x1 - t, y0, x1, y1, col);
        }

        void AddRawQuad(float x0, float y0, float x1, float y1, Color32 col)
        {
            int vi = _verts.Count;
            _verts.Add(new Vector3(x0, y0, 0f));
            _verts.Add(new Vector3(x0, y1, 0f));
            _verts.Add(new Vector3(x1, y1, 0f));
            _verts.Add(new Vector3(x1, y0, 0f));
            _cols.Add(col);
            _cols.Add(col);
            _cols.Add(col);
            _cols.Add(col);
            _tris.Add(vi);
            _tris.Add(vi + 1);
            _tris.Add(vi + 2);
            _tris.Add(vi);
            _tris.Add(vi + 2);
            _tris.Add(vi + 3);
        }
    }
}
