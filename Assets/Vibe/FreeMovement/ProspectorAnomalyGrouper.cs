using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Incremental anomaly grouping for one active Scan.
    /// Favours coherent geological masses (previous Tactical readability).
    /// </summary>
    public sealed class ProspectorAnomalyGrouper
    {
        /// <summary>
        /// Chebyshev link radius. 2 keeps diffuse gas coherent without merging
        /// unrelated distant formations.
        /// </summary>
        public const int LinkRadius = 2;

        /// <summary>Solid micro-clusters below this are merged or culled on freeze.</summary>
        public const int MinSolidTiles = 4;

        readonly ProspectorScanRecord _record;
        readonly Dictionary<long, int> _tileToAnomaly = new(256);
        readonly Dictionary<int, ProspectorAnomaly> _byId = new(32);
        readonly List<int> _neighborBuf = new(8);
        int _nextAnomalyId;
        int _spatialGeometry;
        int _calibration;

        public ProspectorAnomalyGrouper(
            ProspectorScanRecord record,
            int nextAnomalyId,
            int spatialGeometry,
            int calibration)
        {
            _record = record;
            _nextAnomalyId = Mathf.Max(1, nextAnomalyId);
            _spatialGeometry = spatialGeometry;
            _calibration = calibration;
        }

        public int NextAnomalyId => _nextAnomalyId;
        public ProspectorAnomaly LastTouched { get; private set; }

        public event System.Action<ProspectorAnomaly, ProspectorAnomaly> Merged;
        public event System.Action<ProspectorAnomaly> AnomalyCreated;

        public static float GeometryQuality01(int spatialGeometry, int calibration)
        {
            float s = (WorkerStats.Clamp(spatialGeometry) - 1) / 18f;
            float c = (WorkerStats.Clamp(calibration) - 1) / 18f;
            return Mathf.Clamp01(s * 0.65f + c * 0.35f);
        }

        public void Ingest(in RawScanObservation obs)
        {
            if (_record == null || _record.FrozenAnomalies) return;
            if (obs.ScanId != _record.ScanId) return;

            long key = Pack(obs.CellX, obs.CellY);
            if (_tileToAnomaly.ContainsKey(key)) return;

            CollectNeighborAnomalyIds(obs.CellX, obs.CellY, _neighborBuf);

            ProspectorAnomaly target;
            if (_neighborBuf.Count == 0)
            {
                target = CreateAnomaly(obs);
                RefreshDerived(target);
                RebuildSilhouette(target);
                LastTouched = target;
                AnomalyCreated?.Invoke(target);
            }
            else
            {
                int keepId = _neighborBuf[0];
                for (int i = 1; i < _neighborBuf.Count; i++)
                    if (_neighborBuf[i] < keepId) keepId = _neighborBuf[i];

                target = _byId[keepId];
                for (int i = 0; i < _neighborBuf.Count; i++)
                {
                    int id = _neighborBuf[i];
                    if (id == keepId) continue;
                    MergeInto(keepId, id);
                }

                AddEvidenceTile(target, obs);
                RefreshDerived(target);
                RebuildSilhouette(target);
                LastTouched = target;
            }
        }

        void CollectNeighborAnomalyIds(int x, int y, List<int> into)
        {
            into.Clear();
            for (int dy = -LinkRadius; dy <= LinkRadius; dy++)
            for (int dx = -LinkRadius; dx <= LinkRadius; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) > LinkRadius) continue;
                if (!_tileToAnomaly.TryGetValue(Pack(x + dx, y + dy), out int id)) continue;
                if (!into.Contains(id)) into.Add(id);
            }
        }

        ProspectorAnomaly CreateAnomaly(in RawScanObservation obs)
        {
            var a = new ProspectorAnomaly
            {
                AnomalyId = _nextAnomalyId++,
                ScanId = _record.ScanId,
                AnalysisStatus = AnomalyAnalysisStatus.Unanalysed,
                GeometryQuality = GeometryQuality01(_spatialGeometry, _calibration),
                Frozen = false,
            };
            AddEvidenceTile(a, obs);
            _byId[a.AnomalyId] = a;
            _record.Anomalies.Add(a);
            DigHoodLog.Push($"ANOMALY #{a.AnomalyId:00} NEW | scan #{a.ScanId}");
            return a;
        }

        void AddEvidenceTile(ProspectorAnomaly a, in RawScanObservation obs)
        {
            var cell = new Vector2Int(obs.CellX, obs.CellY);
            a.EvidenceTiles.Add(cell);
            _tileToAnomaly[Pack(obs.CellX, obs.CellY)] = a.AnomalyId;
            a.TallyHidden(obs.HiddenTruthKind);

            int n = a.EvidenceTiles.Count;
            if (n == 1)
            {
                a.MeanSignalStrength = obs.SignalStrength;
                a.MaxSignalStrength = obs.SignalStrength;
                a.DistanceFromScannerCells = obs.DistanceFromScannerCells;
            }
            else
            {
                a.MeanSignalStrength += (obs.SignalStrength - a.MeanSignalStrength) / n;
                if (obs.SignalStrength > a.MaxSignalStrength)
                    a.MaxSignalStrength = obs.SignalStrength;
                if (obs.DistanceFromScannerCells < a.DistanceFromScannerCells)
                    a.DistanceFromScannerCells = obs.DistanceFromScannerCells;
            }
        }

        void MergeInto(int keepId, int absorbId)
        {
            if (!_byId.TryGetValue(keepId, out var keep)) return;
            if (!_byId.TryGetValue(absorbId, out var absorb)) return;

            int beforeCount = keep.EvidenceTiles.Count;
            float beforeMean = keep.MeanSignalStrength;
            int absorbCount = absorb.EvidenceTiles.Count;

            for (int i = 0; i < absorb.EvidenceTiles.Count; i++)
            {
                var t = absorb.EvidenceTiles[i];
                keep.EvidenceTiles.Add(t);
                _tileToAnomaly[Pack(t.x, t.y)] = keepId;
            }

            int total = beforeCount + absorbCount;
            if (total > 0)
                keep.MeanSignalStrength =
                    (beforeMean * beforeCount + absorb.MeanSignalStrength * absorbCount) / total;
            if (absorb.MaxSignalStrength > keep.MaxSignalStrength)
                keep.MaxSignalStrength = absorb.MaxSignalStrength;
            if (absorb.DistanceFromScannerCells < keep.DistanceFromScannerCells)
                keep.DistanceFromScannerCells = absorb.DistanceFromScannerCells;
            keep.AbsorbHiddenTallies(absorb);

            _byId.Remove(absorbId);
            _record.Anomalies.Remove(absorb);
            DigHoodLog.Push($"ANOMALY #{absorbId:00} → #{keepId:00} MERGE | scan #{keep.ScanId}");
            Merged?.Invoke(keep, absorb);
        }

        void RefreshDerived(ProspectorAnomaly a)
        {
            a.TileCount = a.EvidenceTiles.Count;
            if (a.TileCount == 0) return;

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            float cx = 0f, cy = 0f;
            for (int i = 0; i < a.EvidenceTiles.Count; i++)
            {
                var t = a.EvidenceTiles[i];
                if (t.x < minX) minX = t.x;
                if (t.y < minY) minY = t.y;
                if (t.x > maxX) maxX = t.x;
                if (t.y > maxY) maxY = t.y;
                cx += t.x;
                cy += t.y;
            }
            a.MinX = minX;
            a.MinY = minY;
            a.MaxX = maxX;
            a.MaxY = maxY;
            a.ApproximateCenterCells = new Vector2(cx / a.TileCount + 0.5f, cy / a.TileCount + 0.5f);
            a.GeometryQuality = GeometryQuality01(_spatialGeometry, _calibration);
        }

        static bool IsDiffuse(ProspectorAnomaly a) =>
            a.TileCount > 0 && a.HiddenGasTiles * 2 >= a.TileCount;

        void RebuildSilhouette(ProspectorAnomaly a)
        {
            a.SilhouetteTiles.Clear();
            a.UncertainEdgeTiles.Clear();

            var core = new HashSet<long>();
            for (int i = 0; i < a.EvidenceTiles.Count; i++)
            {
                var t = a.EvidenceTiles[i];
                if (core.Add(Pack(t.x, t.y)))
                    a.SilhouetteTiles.Add(t);
            }

            bool diffuse = IsDiffuse(a);
            if (!diffuse)
            {
                CloseSolidGaps(a, core, passes: 3);
                ApplyModestSolidMorph(a, core);
            }

            BuildUncertainPerimeter(a, core, diffuse);
        }

        /// <summary>
        /// Faded boundary halo (player-facing). Coherent rings around the silhouette —
        /// not salt-and-pepper. Thickness grows as GeometryQuality (Spatial+Calibration) drops.
        /// </summary>
        void BuildUncertainPerimeter(ProspectorAnomaly a, HashSet<long> core, bool diffuse)
        {
            float q = Mathf.Clamp01(a.GeometryQuality);
            // High precision → 1 thin ring. Low → wider faded belt.
            int ringCount = q >= 0.72f ? 1 : (q >= 0.42f ? 2 : 2);
            if (diffuse && q < 0.55f) ringCount = Mathf.Max(ringCount, 2);

            // Coverage of each ring (still morphology-based; noise only thins slightly)
            float ring1Fill = Mathf.Lerp(1f, 0.78f, q);          // nearly continuous rim
            float ring2Fill = Mathf.Lerp(0.88f, 0.28f, q);        // outer belt when imprecise
            if (diffuse)
            {
                ring1Fill = Mathf.Lerp(0.92f, 0.62f, q);
                ring2Fill = Mathf.Lerp(0.75f, 0.22f, q);
            }

            var occupied = new HashSet<long>(core);
            var frontier = new List<Vector2Int>(a.SilhouetteTiles);

            for (int ring = 1; ring <= ringCount; ring++)
            {
                float fill = ring == 1 ? ring1Fill : ring2Fill;
                if (fill < 0.08f) break;

                var next = new List<Vector2Int>(frontier.Count * 2);
                for (int i = 0; i < frontier.Count; i++)
                {
                    var t = frontier[i];
                    // 8-neighbour halo — continuous faded rim like the previous Tactical look
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = t.x + dx, ny = t.y + dy;
                        long nk = Pack(nx, ny);
                        if (occupied.Contains(nk)) continue;

                        float n = ProspectorDetection.StableNoise01(
                            a.AnomalyId * 31 + ring * 7 + dx * 3, nx, ny);
                        if (n > fill) continue;

                        occupied.Add(nk);
                        var cell = new Vector2Int(nx, ny);
                        a.UncertainEdgeTiles.Add(cell);
                        next.Add(cell);
                    }
                }

                frontier = next;
                if (frontier.Count == 0) break;
            }
        }

        /// <summary>
        /// Modest enlarge/shrink of solid silhouette from GeometryQuality (Calibration+Spatial).
        /// Topology-preserving morphological step — not scatter.
        /// </summary>
        void ApplyModestSolidMorph(ProspectorAnomaly a, HashSet<long> core)
        {
            if (a.SilhouetteTiles.Count < MinSolidTiles) return;
            float q = a.GeometryQuality;
            float n = ProspectorDetection.StableNoise01(a.ScanId, a.AnomalyId * 17, a.MinX + a.MinY);

            // Low quality → slight grow or slight erode (readable, not noisy)
            if (q >= 0.72f) return;

            if (n < 0.45f)
                DilateSilhouetteOnce(a, core);
            else if (n > 0.72f && q < 0.45f)
                ErodeSilhouetteOnce(a, core);
        }

        static void DilateSilhouetteOnce(ProspectorAnomaly a, HashSet<long> core)
        {
            var add = new List<Vector2Int>(32);
            int count = a.SilhouetteTiles.Count;
            for (int i = 0; i < count; i++)
            {
                var t = a.SilhouetteTiles[i];
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (dx != 0 && dy != 0) continue; // cardinal only — keeps mass chunky
                    int nx = t.x + dx, ny = t.y + dy;
                    if (core.Contains(Pack(nx, ny))) continue;
                    // Prefer cells that already touch 2+ silhouette tiles (smooth rim)
                    int touch = 0;
                    if (core.Contains(Pack(nx - 1, ny))) touch++;
                    if (core.Contains(Pack(nx + 1, ny))) touch++;
                    if (core.Contains(Pack(nx, ny - 1))) touch++;
                    if (core.Contains(Pack(nx, ny + 1))) touch++;
                    if (touch < 2) continue;
                    add.Add(new Vector2Int(nx, ny));
                }
            }
            for (int i = 0; i < add.Count; i++)
            {
                var t = add[i];
                if (core.Add(Pack(t.x, t.y)))
                    a.SilhouetteTiles.Add(t);
            }
        }

        static void ErodeSilhouetteOnce(ProspectorAnomaly a, HashSet<long> core)
        {
            var remove = new List<Vector2Int>(16);
            for (int i = 0; i < a.SilhouetteTiles.Count; i++)
            {
                var t = a.SilhouetteTiles[i];
                int touch = 0;
                if (core.Contains(Pack(t.x - 1, t.y))) touch++;
                if (core.Contains(Pack(t.x + 1, t.y))) touch++;
                if (core.Contains(Pack(t.x, t.y - 1))) touch++;
                if (core.Contains(Pack(t.x, t.y + 1))) touch++;
                // Only peel weakly attached rim cells
                if (touch <= 1) remove.Add(t);
            }
            if (a.SilhouetteTiles.Count - remove.Count < MinSolidTiles) return;
            for (int i = 0; i < remove.Count; i++)
            {
                var t = remove[i];
                core.Remove(Pack(t.x, t.y));
                a.SilhouetteTiles.Remove(t);
            }
        }

        static void CloseSolidGaps(ProspectorAnomaly a, HashSet<long> core, int passes)
        {
            if (a.SilhouetteTiles.Count < 2) return;

            int minX = a.MinX - 1, maxX = a.MaxX + 1;
            int minY = a.MinY - 1, maxY = a.MaxY + 1;
            var add = new List<Vector2Int>(32);

            for (int pass = 0; pass < passes; pass++)
            {
                add.Clear();
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    long k = Pack(x, y);
                    if (core.Contains(k)) continue;

                    bool L = core.Contains(Pack(x - 1, y));
                    bool R = core.Contains(Pack(x + 1, y));
                    bool D = core.Contains(Pack(x, y - 1));
                    bool U = core.Contains(Pack(x, y + 1));
                    int orth = (L ? 1 : 0) + (R ? 1 : 0) + (D ? 1 : 0) + (U ? 1 : 0);
                    bool bridge = (L && R) || (U && D);
                    // Fill bridges and any 2-neighbor pocket (previous mass look)
                    if (!bridge && orth < 2) continue;
                    add.Add(new Vector2Int(x, y));
                }

                for (int i = 0; i < add.Count; i++)
                {
                    var t = add[i];
                    if (core.Add(Pack(t.x, t.y)))
                        a.SilhouetteTiles.Add(t);
                }
                if (add.Count == 0) break;
            }
        }

        public void Freeze()
        {
            if (_record == null) return;

            ConsolidateFragments();
            CullTinySolids();

            for (int i = 0; i < _record.Anomalies.Count; i++)
            {
                var a = _record.Anomalies[i];
                RefreshDerived(a);
                RebuildSilhouette(a);
                ProspectorGeoSignalProfiles.SampleHidden(a);
                a.Frozen = true;
            }
            _record.FrozenAnomalies = true;
            _record.Anomalies.Sort((x, y) =>
                x.DistanceFromScannerCells.CompareTo(y.DistanceFromScannerCells));
            DigHoodLog.Push(
                $"SCAN #{_record.ScanId} ANOMALIES FROZEN | count {_record.Anomalies.Count}");
        }

        /// <summary>
        /// Merge solid anomalies whose bounds nearly touch — undoes region-tear mini-clusters.
        /// </summary>
        void ConsolidateFragments()
        {
            bool merged;
            do
            {
                merged = false;
                var list = _record.Anomalies;
                for (int i = 0; i < list.Count && !merged; i++)
                {
                    var a = list[i];
                    if (IsDiffuse(a)) continue;
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        var b = list[j];
                        if (IsDiffuse(b)) continue;
                        if (!BoundsNear(a, b, gap: 2)) continue;
                        int keep = Mathf.Min(a.AnomalyId, b.AnomalyId);
                        int abs = keep == a.AnomalyId ? b.AnomalyId : a.AnomalyId;
                        MergeInto(keep, abs);
                        merged = true;
                        break;
                    }
                }
            } while (merged);
        }

        static bool BoundsNear(ProspectorAnomaly a, ProspectorAnomaly b, int gap)
        {
            // Expanded AABB proximity (Chebyshev between boxes)
            int dx = 0;
            if (a.MaxX < b.MinX) dx = b.MinX - a.MaxX;
            else if (b.MaxX < a.MinX) dx = a.MinX - b.MaxX;
            int dy = 0;
            if (a.MaxY < b.MinY) dy = b.MinY - a.MaxY;
            else if (b.MaxY < a.MinY) dy = a.MinY - b.MaxY;
            return Mathf.Max(dx, dy) <= gap;
        }

        void CullTinySolids()
        {
            var doomed = new List<int>(8);
            for (int i = 0; i < _record.Anomalies.Count; i++)
            {
                var a = _record.Anomalies[i];
                if (IsDiffuse(a)) continue;
                if (a.TileCount >= MinSolidTiles) continue;

                // Try absorb into nearest solid neighbour
                int bestId = -1;
                float bestDist = float.MaxValue;
                for (int j = 0; j < _record.Anomalies.Count; j++)
                {
                    var b = _record.Anomalies[j];
                    if (b.AnomalyId == a.AnomalyId) continue;
                    if (IsDiffuse(b)) continue;
                    if (b.TileCount < MinSolidTiles) continue;
                    if (!BoundsNear(a, b, gap: 4)) continue;
                    float d = Vector2.Distance(a.ApproximateCenterCells, b.ApproximateCenterCells);
                    if (d < bestDist) { bestDist = d; bestId = b.AnomalyId; }
                }

                if (bestId >= 0)
                    MergeInto(bestId, a.AnomalyId);
                else
                    doomed.Add(a.AnomalyId);
            }

            for (int i = 0; i < doomed.Count; i++)
                RemoveAnomaly(doomed[i]);
        }

        void RemoveAnomaly(int id)
        {
            if (!_byId.TryGetValue(id, out var a)) return;
            for (int i = 0; i < a.EvidenceTiles.Count; i++)
            {
                var t = a.EvidenceTiles[i];
                long k = Pack(t.x, t.y);
                if (_tileToAnomaly.TryGetValue(k, out int mapped) && mapped == id)
                    _tileToAnomaly.Remove(k);
            }
            _byId.Remove(id);
            _record.Anomalies.Remove(a);
            DigHoodLog.Push($"ANOMALY #{id:00} CULL (micro) | scan #{a.ScanId}");
        }

        static long Pack(int x, int y) => ((long)x << 32) ^ (uint)y;
    }
}
