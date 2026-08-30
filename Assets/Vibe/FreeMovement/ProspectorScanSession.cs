using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Progressive heavy-scanner pass: closest → farthest through a planned cone.
    /// Driven by game hours only. Does not classify materials.
    /// </summary>
    public sealed class ProspectorScanSession
    {
        struct ScanCell
        {
            public int X, Y;
            public float DistCells;
        }

        readonly List<ScanCell> _cells = new(2048);
        int _nextIndex;
        FineTerrainWorld _world;
        ProspectorScanHistory _history;
        ProspectorScanRecord _record;
        Vector2 _origin;
        float _maxDistCells;
        float _elapsedHours;
        float _durationHours;
        int _acoustics;
        int _calibration;
        float _startGameHours;

        public bool IsActive => _record != null && !_record.Completed;
        public ProspectorScanRecord Record => _record;
        public float ElapsedHours => _elapsedHours;
        public float DurationHours => _durationHours;
        public float Progress01 =>
            _durationHours > 0.001f ? Mathf.Clamp01(_elapsedHours / _durationHours) : 0f;
        public float CurrentReachCells => _maxDistCells * Progress01;
        public float MaxDistanceCells => _maxDistCells;
        public int CellsTotal => _cells.Count;
        public int CellsProcessed => _nextIndex;
        public int EvidenceThisScan => _record != null ? _record.Observations.Count : 0;

        public bool TryBegin(
            FineTerrainWorld world,
            ProspectorScanHistory history,
            ProspectorScannerEquipment scanner,
            WorkerStats stats,
            string prospectorName,
            float absoluteGameHours,
            float plannedRangeCells,
            float plannedHalfAngleDeg,
            string prospectorId = null,
            WorkerSheetProfile prospectorProfile = WorkerSheetProfile.Baseline,
            string prospectorProfileLabel = null)
        {
            if (world == null || history == null || scanner == null) return false;
            if (scanner.State != ProspectorScannerState.Ready) return false;
            if (IsActive) return false;

            var spec = scanner.Spec;
            plannedRangeCells = Mathf.Clamp(plannedRangeCells, 1f, spec.MaxRangeCells);
            plannedHalfAngleDeg = Mathf.Clamp(plannedHalfAngleDeg, 1f, spec.ConeHalfAngleDeg);

            float size01 = ProspectorScanFormulas.Size01(
                plannedRangeCells, plannedHalfAngleDeg, spec);
            int calib = stats != null ? stats.Get(WorkerStatId.Calibration) : WorkerStats.Baseline;
            int focus = stats != null ? stats.Get(WorkerStatId.Focus) : WorkerStats.Baseline;
            int acoustics = stats != null ? stats.Get(WorkerStatId.Acoustics) : WorkerStats.Baseline;
            int spatial = stats != null ? stats.Get(WorkerStatId.SpatialGeometry) : WorkerStats.Baseline;
            var analysisStats = ProspectorAnalysisStats.From(stats);
            float duration = ProspectorScanFormulas.ScanDurationHours(calib, focus, size01);

            _world = world;
            _history = history;
            _origin = scanner.Position;
            _maxDistCells = plannedRangeCells;
            _elapsedHours = 0f;
            _durationHours = duration;
            _acoustics = acoustics;
            _calibration = calib;
            _startGameHours = absoluteGameHours;
            _nextIndex = 0;

            BuildSortedCells(plannedRangeCells, plannedHalfAngleDeg, scanner.Facing);
            if (_cells.Count == 0)
            {
                DigHoodLog.Push("SCAN | No scannable cells in planned area");
                return false;
            }

            _record = history.BeginScan(
                scanner.Position,
                scanner.Facing,
                plannedRangeCells,
                plannedHalfAngleDeg,
                prospectorName,
                absoluteGameHours,
                calib, focus, acoustics, spatial,
                analysisStats,
                duration, size01,
                prospectorId,
                prospectorProfile,
                prospectorProfileLabel);

            DigHoodLog.Push(
                $"SCAN #{_record.ScanId} START | cells {_cells.Count} | " +
                $"duration {duration:0.#}h | Cal {calib} Foc {focus} Ac {acoustics} Sp {spatial} | " +
                $"Math {analysisStats.Mathematics} Lith {analysisStats.Lithology} " +
                $"Min {analysisStats.Mineralogy} Chem {analysisStats.Chemistry}");
            Debug.Log(
                $"[SCAN #{_record.ScanId}] START duration={duration:0.##}h " +
                $"cells={_cells.Count} Math={analysisStats.Mathematics} " +
                $"Lith={analysisStats.Lithology} Min={analysisStats.Mineralogy} Chem={analysisStats.Chemistry}");
            return true;
        }

        void BuildSortedCells(float rangeCells, float halfAngleDeg, Vector2 facing)
        {
            _cells.Clear();
            if (_world == null) return;
            if (facing.sqrMagnitude < 0.0001f) facing = Vector2.up;
            facing.Normalize();

            float cs = _world.CellSize;
            float rangeWorld = rangeCells * cs;
            float cosHalf = Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);
            var originCell = _world.WorldToCell(_origin);
            int pad = Mathf.CeilToInt(rangeCells) + 2;

            var seen = new HashSet<long>();
            for (int dy = -pad; dy <= pad; dy++)
            for (int dx = -pad; dx <= pad; dx++)
            {
                int x = originCell.x + dx;
                int y = originCell.y + dy;
                if (!_world.InBounds(x, y)) continue;

                Vector2 center = _world.CellCenter(x, y);
                Vector2 delta = center - _origin;
                float distWorld = delta.magnitude;
                if (distWorld > rangeWorld + cs * 0.5f) continue;
                if (distWorld < 0.001f) continue;

                Vector2 dir = delta / distWorld;
                if (Vector2.Dot(facing, dir) < cosHalf - 0.0001f) continue;

                // Scannable rock / sealed gas only — open tunnel skipped
                bool gas = _world.IsGas(x, y);
                if (_world.IsTunnelOpen(x, y) && !gas) continue;
                if (_world.IsExcavated(x, y) && !gas) continue;
                var terrain = _world.Get(x, y);
                if (terrain.IsUndamageableBorder) continue;

                long key = ((long)x << 32) ^ (uint)y;
                if (!seen.Add(key)) continue;

                _cells.Add(new ScanCell
                {
                    X = x,
                    Y = y,
                    DistCells = distWorld / cs,
                });
            }

            _cells.Sort((a, b) => a.DistCells.CompareTo(b.DistCells));
            if (_cells.Count > 0)
                _maxDistCells = Mathf.Max(_maxDistCells, _cells[_cells.Count - 1].DistCells);
        }

        /// <summary>Advance using game-hour delta. Returns true when the scan completes.</summary>
        public bool TickGameHours(float gameHoursDelta, float absoluteGameHoursNow)
        {
            if (!IsActive || gameHoursDelta <= 0f) return false;

            _elapsedHours += gameHoursDelta;
            float reach = CurrentReachCells;

            while (_nextIndex < _cells.Count && _cells[_nextIndex].DistCells <= reach + 0.0001f)
            {
                ProcessCell(_cells[_nextIndex], absoluteGameHoursNow);
                _nextIndex++;
            }

            if (_elapsedHours + 0.0001f < _durationHours && _nextIndex < _cells.Count)
                return false;

            // Finish any remaining cells at completion
            while (_nextIndex < _cells.Count)
            {
                ProcessCell(_cells[_nextIndex], absoluteGameHoursNow);
                _nextIndex++;
            }

            Complete(absoluteGameHoursNow);
            return true;
        }

        void ProcessCell(ScanCell cell, float absoluteGameHours)
        {
            if (_record == null || _world == null) return;

            var stats = new ProspectorSpatialUncertainty.UncertaintyStats
            {
                SpatialGeometry = _record.SpatialGeometry,
                Calibration = _record.Calibration,
                Acoustics = _acoustics,
                ScanId = _record.ScanId,
                MaxRangeCells = _maxDistCells,
            };

            if (!ProspectorSpatialUncertainty.TryBuildObservation(
                    _world, cell.X, cell.Y, cell.DistCells, stats, out var warp))
                return;

            // Distance from scanner uses believed position (what Prospector thinks)
            Vector2 believedCenter = _world.CellCenter(warp.BelievedX, warp.BelievedY);
            float believedDist = Vector2.Distance(_origin, believedCenter) / Mathf.Max(0.0001f, _world.CellSize);

            var obs = new RawScanObservation
            {
                ScanId = _record.ScanId,
                CellX = warp.BelievedX,
                CellY = warp.BelievedY,
                TruthCellX = warp.TruthX,
                TruthCellY = warp.TruthY,
                RegionOffsetX = warp.OffsetX,
                RegionOffsetY = warp.OffsetY,
                BoundaryExtraX = warp.BoundaryExtraX,
                BoundaryExtraY = warp.BoundaryExtraY,
                IsFalsePositive = warp.IsFalsePositive,
                DiscoveredGameHours = absoluteGameHours,
                SignalStrength = warp.SignalStrength,
                ObservationQuality = warp.ObservationQuality,
                DistanceFromScannerCells = believedDist,
                HiddenTruthKind = warp.HiddenKind,
            };
            _history.AddObservation(obs);
        }

        void Complete(float absoluteGameHours)
        {
            if (_record == null) return;
            int id = _record.ScanId;
            int n = _record.Observations.Count;
            int anomalyN = _record.Anomalies.Count;
            _history.CompleteActive(absoluteGameHours);
            DigHoodLog.Push(
                $"SCAN #{id} COMPLETE | evidence {n} | anomalies {anomalyN} | " +
                $"elapsed {_elapsedHours:0.#}h | abs t {absoluteGameHours:0.##}h");
            Debug.Log(
                $"[SCAN #{id}] COMPLETE evidence={n} anomalies={anomalyN} elapsed={_elapsedHours:0.##}h");
            _record = null;
            _cells.Clear();
            _nextIndex = 0;
        }

        public void DebugForceComplete(float absoluteGameHours)
        {
            if (!IsActive) return;
            _elapsedHours = _durationHours;
            while (_nextIndex < _cells.Count)
            {
                ProcessCell(_cells[_nextIndex], absoluteGameHours);
                _nextIndex++;
            }
            Complete(absoluteGameHours);
        }

        public void Abort()
        {
            _record = null;
            _cells.Clear();
            _nextIndex = 0;
            _elapsedHours = 0f;
        }
    }
}
