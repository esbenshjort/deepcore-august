using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Player-facing raw anomaly evidence (believed coordinates).
    /// No material labels. Spatial mistakes are frozen into this Scan's history.
    /// </summary>
    [Serializable]
    public struct RawScanObservation
    {
        public int ScanId;
        /// <summary>Believed cell — Tactical View / grouping / analysis use this.</summary>
        public int CellX;
        public int CellY;
        public float DiscoveredGameHours;
        public float SignalStrength;
        public float ObservationQuality;
        public float DistanceFromScannerCells;

        /// <summary>Ground-Truth cell that produced this reading (debug / history). Unchanged world.</summary>
        public int TruthCellX;
        public int TruthCellY;
        public int RegionOffsetX;
        public int RegionOffsetY;
        public int BoundaryExtraX;
        public int BoundaryExtraY;
        public bool IsFalsePositive;

        /// <summary>
        /// Internal only — Stage-4 signal generation. Never read by Tactical View labels.
        /// </summary>
        [SerializeField] internal ProspectorHiddenTruthKind HiddenTruthKind;
    }

    /// <summary>
    /// Immutable spatial silhouette for one anomaly at scan freeze.
    /// Historical Tactical draws from these — never from live ProspectorAnomaly tiles.
    /// </summary>
    [Serializable]
    public sealed class AnomalySpatialSnapshot
    {
        public int AnomalyId;
        public int ScanId;
        public Vector2 ApproximateCenterCells;
        public int MinX, MinY, MaxX, MaxY;
        public int TileCount;
        public float MeanSignalStrength;
        public float MaxSignalStrength;
        public float GeometryQuality;
        public float DistanceFromScannerCells;
        public readonly List<Vector2Int> EvidenceTiles = new(32);
        public readonly List<Vector2Int> SilhouetteTiles = new(48);
        public readonly List<Vector2Int> UncertainEdgeTiles = new(24);

        public bool ContainsCell(int x, int y)
        {
            for (int i = 0; i < SilhouetteTiles.Count; i++)
                if (SilhouetteTiles[i].x == x && SilhouetteTiles[i].y == y) return true;
            for (int i = 0; i < UncertainEdgeTiles.Count; i++)
                if (UncertainEdgeTiles[i].x == x && UncertainEdgeTiles[i].y == y) return true;
            for (int i = 0; i < EvidenceTiles.Count; i++)
                if (EvidenceTiles[i].x == x && EvidenceTiles[i].y == y) return true;
            return false;
        }

        public static AnomalySpatialSnapshot FromAnomaly(ProspectorAnomaly a)
        {
            if (a == null) return null;
            var s = new AnomalySpatialSnapshot
            {
                AnomalyId = a.AnomalyId,
                ScanId = a.ScanId,
                ApproximateCenterCells = a.ApproximateCenterCells,
                MinX = a.MinX,
                MinY = a.MinY,
                MaxX = a.MaxX,
                MaxY = a.MaxY,
                TileCount = a.TileCount,
                MeanSignalStrength = a.MeanSignalStrength,
                MaxSignalStrength = a.MaxSignalStrength,
                GeometryQuality = a.GeometryQuality,
                DistanceFromScannerCells = a.DistanceFromScannerCells,
            };
            s.EvidenceTiles.AddRange(a.EvidenceTiles);
            s.SilhouetteTiles.AddRange(a.SilhouetteTiles);
            s.UncertainEdgeTiles.AddRange(a.UncertainEdgeTiles);
            return s;
        }
    }

    public enum ProspectorTacticalViewMode : byte
    {
        Current = 0,
        Historical = 1,
    }

    /// <summary>
    /// One completed or in-progress heavy-scanner pass. Beginning of Scan History.
    /// Anomalies + assessments belong to this Scan ID only — never rewritten by later scans.
    /// </summary>
    [Serializable]
    public sealed class ProspectorScanRecord
    {
        public int ScanId;
        public Vector2 ScannerPosition;
        public Vector2 ScannerFacing;
        public float PlannedRangeCells;
        public float PlannedHalfAngleDeg;
        public string ProspectorName = "Prospector";
        /// <summary>Stable identity frozen at scan start — do not resolve from live worker.</summary>
        public string ProspectorId = "prospector";
        /// <summary>Numeric WorkerId frozen at scan start (Stage C attribution).</summary>
        public int WorkerId;
        public WorkerSheetProfile ProspectorProfile = WorkerSheetProfile.Baseline;
        public string ProspectorProfileLabel = "BASE";
        public float StartGameHours;
        public float CompletionGameHours;
        public int Calibration;
        public int Focus;
        public int Acoustics;
        public int SpatialGeometry;
        public int Mathematics;
        public int Lithology;
        public int Mineralogy;
        public int Chemistry;
        public int Composure;
        public int Intuition;
        public int WorkRate;
        public float PlannedDurationHours;
        public float Size01;
        public bool Completed;
        /// <summary>After completion, anomaly silhouettes are immutable historical snapshot.</summary>
        public bool FrozenAnomalies;
        public readonly List<RawScanObservation> Observations = new(256);
        /// <summary>
        /// Live anomaly objects (investigation continues after freeze).
        /// Historical map UI must use <see cref="SpatialSnapshots"/> instead.
        /// </summary>
        public readonly List<ProspectorAnomaly> Anomalies = new(32);
        /// <summary>Frozen spatial silhouettes captured at scan completion.</summary>
        public readonly List<AnomalySpatialSnapshot> SpatialSnapshots = new(32);

        public float Progress01 =>
            PlannedDurationHours > 0.001f
                ? Mathf.Clamp01((CompletionGameHours > 0f
                    ? CompletionGameHours - StartGameHours
                    : 0f) / PlannedDurationHours)
                : 0f;

        public ProspectorAnalysisStats AnalysisStats => new()
        {
            Mathematics = Mathematics,
            Lithology = Lithology,
            Mineralogy = Mineralogy,
            Chemistry = Chemistry,
            Composure = Composure,
            Intuition = Intuition,
            Focus = Focus,
            WorkRate = WorkRate,
        };

        public void CaptureSpatialSnapshots()
        {
            SpatialSnapshots.Clear();
            for (int i = 0; i < Anomalies.Count; i++)
            {
                var snap = AnomalySpatialSnapshot.FromAnomaly(Anomalies[i]);
                if (snap != null) SpatialSnapshots.Add(snap);
            }
            SpatialSnapshots.Sort((a, b) =>
                a.DistanceFromScannerCells.CompareTo(b.DistanceFromScannerCells));
        }

        public bool TryGetSpatialAt(int cellX, int cellY, out AnomalySpatialSnapshot snap)
        {
            snap = null;
            AnomalySpatialSnapshot best = null;
            for (int i = 0; i < SpatialSnapshots.Count; i++)
            {
                var s = SpatialSnapshots[i];
                if (!s.ContainsCell(cellX, cellY)) continue;
                if (best == null
                    || s.TileCount > best.TileCount
                    || (s.TileCount == best.TileCount
                        && s.DistanceFromScannerCells < best.DistanceFromScannerCells))
                    best = s;
            }
            snap = best;
            return best != null;
        }
    }

    /// <summary>
    /// Persistent raw scan store. Evidence / anomalies / assessments never auto-cleared between scans.
    /// Different Scan IDs stay separate observations (no cross-scan merge).
    /// </summary>
    public sealed class ProspectorScanHistory
    {
        readonly List<ProspectorScanRecord> _scans = new(16);
        readonly List<RawScanObservation> _allEvidence = new(1024);
        readonly List<ProspectorScanRecord> _completedScratch = new(16);
        int _nextScanId = 1;
        int _nextAnomalyId = 1;
        ProspectorAnomalyGrouper _activeGrouper;
        readonly ProspectorAnomalyAnalyst _analyst = new();
        readonly ProspectorFindingsLog _findings = new();

        public IReadOnlyList<ProspectorScanRecord> Scans => _scans;
        public IReadOnlyList<RawScanObservation> AllEvidence => _allEvidence;
        public ProspectorScanRecord Active { get; private set; }
        public int EvidenceCount => _allEvidence.Count;
        public int NextScanId => _nextScanId;
        public int AnomalyCountDisplay => ViewScan != null
            ? (IsHistoricalMode
                ? ViewScan.SpatialSnapshots.Count
                : ViewScan.Anomalies.Count)
            : 0;
        public ProspectorAnomalyAnalyst Analyst => _analyst;
        public ProspectorFindingsLog Findings => _findings;

        public ProspectorTacticalViewMode ViewMode { get; private set; } = ProspectorTacticalViewMode.Current;
        public int SelectedScanId { get; private set; } = -1;
        public bool IsHistoricalMode => ViewMode == ProspectorTacticalViewMode.Historical;

        /// <summary>
        /// Tactical default: active scan while running, else most recent completed scan.
        /// Does not blend anomalies across Scan IDs.
        /// </summary>
        public ProspectorScanRecord DisplayScan
        {
            get
            {
                if (Active != null) return Active;
                for (int i = _scans.Count - 1; i >= 0; i--)
                    if (_scans[i].Completed) return _scans[i];
                return _scans.Count > 0 ? _scans[_scans.Count - 1] : null;
            }
        }

        /// <summary>
        /// What Tactical should render: selected historical scan, or live DisplayScan.
        /// </summary>
        public ProspectorScanRecord ViewScan
        {
            get
            {
                if (IsHistoricalMode && TryGetScan(SelectedScanId, out var hist) && hist.Completed)
                    return hist;
                return DisplayScan;
            }
        }

        public event Action EvidenceChanged;
        public event Action AnomaliesChanged;
        public event Action ViewModeChanged;

        public ProspectorScanRecord BeginScan(
            Vector2 scannerPos,
            Vector2 scannerFacing,
            float plannedRangeCells,
            float plannedHalfAngleDeg,
            string prospectorName,
            float startGameHours,
            int calibration,
            int focus,
            int acoustics,
            int spatialGeometry,
            in ProspectorAnalysisStats analysisStats,
            float plannedDurationHours,
            float size01,
            string prospectorId = null,
            WorkerSheetProfile prospectorProfile = WorkerSheetProfile.Baseline,
            string prospectorProfileLabel = null,
            int workerId = 0)
        {
            // New scan always returns player to live tactical
            ReturnToCurrentTactical(notify: false);

            string name = string.IsNullOrEmpty(prospectorName) ? "Prospector" : prospectorName;
            string profileLabel = !string.IsNullOrEmpty(prospectorProfileLabel)
                ? prospectorProfileLabel
                : WorkerStatProfiles.Label(0, prospectorProfile); // role 0 = Prospector

            var rec = new ProspectorScanRecord
            {
                ScanId = _nextScanId++,
                ScannerPosition = scannerPos,
                ScannerFacing = scannerFacing.sqrMagnitude > 0.0001f
                    ? scannerFacing.normalized
                    : Vector2.up,
                PlannedRangeCells = plannedRangeCells,
                PlannedHalfAngleDeg = plannedHalfAngleDeg,
                ProspectorName = name,
                ProspectorId = string.IsNullOrEmpty(prospectorId) ? name : prospectorId,
                WorkerId = workerId,
                ProspectorProfile = prospectorProfile,
                ProspectorProfileLabel = profileLabel,
                StartGameHours = startGameHours,
                CompletionGameHours = 0f,
                Calibration = calibration,
                Focus = focus,
                Acoustics = acoustics,
                SpatialGeometry = spatialGeometry,
                Mathematics = analysisStats.Mathematics,
                Lithology = analysisStats.Lithology,
                Mineralogy = analysisStats.Mineralogy,
                Chemistry = analysisStats.Chemistry,
                Composure = analysisStats.Composure,
                Intuition = analysisStats.Intuition,
                WorkRate = analysisStats.WorkRate,
                PlannedDurationHours = plannedDurationHours,
                Size01 = size01,
                Completed = false,
                FrozenAnomalies = false,
            };
            _scans.Add(rec);
            Active = rec;
            _activeGrouper = new ProspectorAnomalyGrouper(
                rec, _nextAnomalyId, spatialGeometry, calibration);
            _activeGrouper.Merged += OnAnomaliesMerged;
            _activeGrouper.AnomalyCreated += OnAnomalyCreated;
            _analyst.Bind(rec, analysisStats);
            _analyst.Findings = _findings;
            return rec;
        }

        void OnAnomalyCreated(ProspectorAnomaly a)
        {
            _findings.NotifyAnomalyDetected(a);
            AnomaliesChanged?.Invoke();
        }

        public void AddObservation(RawScanObservation obs)
        {
            if (Active != null && obs.ScanId == Active.ScanId)
                Active.Observations.Add(obs);
            _allEvidence.Add(obs);
            _activeGrouper?.Ingest(obs);
            if (_activeGrouper?.LastTouched != null)
                _findings.NotifySignificantEvidence(_activeGrouper.LastTouched);
            // Analysis waits until scan freeze — raw evidence only during the sweep
            EvidenceChanged?.Invoke();
            AnomaliesChanged?.Invoke();
        }

        void OnAnomaliesMerged(ProspectorAnomaly keep, ProspectorAnomaly absorb)
        {
            _analyst.NotifyMerged(keep, absorb);
            AnomaliesChanged?.Invoke();
        }

        public void CompleteActive(float completionGameHours)
        {
            if (Active == null) return;
            if (_activeGrouper != null)
            {
                _activeGrouper.Merged -= OnAnomaliesMerged;
                _activeGrouper.AnomalyCreated -= OnAnomalyCreated;
                _activeGrouper.Freeze();
                _nextAnomalyId = _activeGrouper.NextAnomalyId;
            }
            _activeGrouper = null;
            Active.Completed = true;
            Active.CompletionGameHours = completionGameHours;
            // Freeze spatial silhouettes once — historical UI never re-reads live anomaly tiles
            Active.CaptureSpatialSnapshots();
            _analyst.OnScanFrozen();
            // Keep analyst bound to this completed scan until queue drains / next BeginScan
            Active = null;
            AnomaliesChanged?.Invoke();
        }

        /// <summary>Advance live analysis (during and after scan until queue empty).</summary>
        public void TickAnalysisGameHours(
            float gameHoursDelta,
            float absoluteGameHours,
            float excavatorDistanceCells = -1f)
        {
            _findings.SetGameHours(absoluteGameHours);
            if (!_analyst.HasWork) return;
            if (_analyst.TickGameHours(gameHoursDelta, excavatorDistanceCells))
                AnomaliesChanged?.Invoke();
        }

        public bool TryGetScan(int scanId, out ProspectorScanRecord record)
        {
            record = null;
            for (int i = 0; i < _scans.Count; i++)
            {
                if (_scans[i].ScanId != scanId) continue;
                record = _scans[i];
                return true;
            }
            return false;
        }

        /// <summary>Completed scans, newest first (for History browser).</summary>
        public void CopyCompletedScansNewestFirst(List<ProspectorScanRecord> into)
        {
            into.Clear();
            for (int i = _scans.Count - 1; i >= 0; i--)
                if (_scans[i].Completed) into.Add(_scans[i]);
        }

        public void EnterHistoricalScan(int scanId)
        {
            if (!TryGetScan(scanId, out var rec) || !rec.Completed) return;
            ViewMode = ProspectorTacticalViewMode.Historical;
            SelectedScanId = scanId;
            ViewModeChanged?.Invoke();
            AnomaliesChanged?.Invoke();
        }

        public void ReturnToCurrentTactical(bool notify = true)
        {
            bool changed = IsHistoricalMode || SelectedScanId >= 0;
            ViewMode = ProspectorTacticalViewMode.Current;
            SelectedScanId = -1;
            if (notify && changed)
            {
                ViewModeChanged?.Invoke();
                AnomaliesChanged?.Invoke();
            }
        }

        public bool TrySelectAdjacentHistorical(int delta)
        {
            CopyCompletedScansNewestFirst(_completedScratch);
            if (_completedScratch.Count == 0) return false;

            int idx = 0;
            if (IsHistoricalMode)
            {
                idx = -1;
                for (int i = 0; i < _completedScratch.Count; i++)
                {
                    if (_completedScratch[i].ScanId != SelectedScanId) continue;
                    idx = i;
                    break;
                }
                if (idx < 0) idx = 0;
            }
            else
            {
                // From live Current → open newest completed snapshot
                if (_completedScratch.Count == 0) return false;
                EnterHistoricalScan(_completedScratch[0].ScanId);
                return true;
            }

            int next = Mathf.Clamp(idx + delta, 0, _completedScratch.Count - 1);
            if (next == idx && IsHistoricalMode) return false;
            EnterHistoricalScan(_completedScratch[next].ScanId);
            return true;
        }

        /// <summary>
        /// Pick for live Current mode (mutable anomaly). Historical mode uses spatial snapshots.
        /// </summary>
        public bool TryGetAnomalyAt(int cellX, int cellY, out ProspectorAnomaly anomaly)
        {
            anomaly = null;
            if (IsHistoricalMode) return false;
            var scan = DisplayScan;
            if (scan == null) return false;
            ProspectorAnomaly best = null;
            for (int i = 0; i < scan.Anomalies.Count; i++)
            {
                var a = scan.Anomalies[i];
                if (!a.ContainsCell(cellX, cellY)) continue;
                if (best == null
                    || a.TileCount > best.TileCount
                    || (a.TileCount == best.TileCount
                        && a.DistanceFromScannerCells < best.DistanceFromScannerCells))
                    best = a;
            }
            anomaly = best;
            return best != null;
        }

        public bool TryGetHistoricalSpatialAt(
            int cellX, int cellY, out AnomalySpatialSnapshot snap)
        {
            snap = null;
            if (!IsHistoricalMode) return false;
            var scan = ViewScan;
            if (scan == null) return false;
            return scan.TryGetSpatialAt(cellX, cellY, out snap);
        }

        public void CopyDisplayAnomaliesSorted(List<ProspectorAnomaly> into)
        {
            into.Clear();
            if (IsHistoricalMode) return;
            var scan = DisplayScan;
            if (scan == null) return;
            into.AddRange(scan.Anomalies);
            into.Sort((a, b) => a.DistanceFromScannerCells.CompareTo(b.DistanceFromScannerCells));
        }

        public void CopyViewSpatialSnapshotsSorted(List<AnomalySpatialSnapshot> into)
        {
            into.Clear();
            var scan = ViewScan;
            if (scan == null) return;
            into.AddRange(scan.SpatialSnapshots);
            into.Sort((a, b) => a.DistanceFromScannerCells.CompareTo(b.DistanceFromScannerCells));
        }

        public void ClearAll()
        {
            if (_activeGrouper != null)
            {
                _activeGrouper.Merged -= OnAnomaliesMerged;
                _activeGrouper.AnomalyCreated -= OnAnomalyCreated;
            }
            _scans.Clear();
            _allEvidence.Clear();
            Active = null;
            _activeGrouper = null;
            _analyst.Clear();
            _findings.Clear();
            _nextScanId = 1;
            _nextAnomalyId = 1;
            ReturnToCurrentTactical(notify: false);
            EvidenceChanged?.Invoke();
            AnomaliesChanged?.Invoke();
            ViewModeChanged?.Invoke();
        }
    }
}
