using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    public enum ProspectorFindingEventType : byte
    {
        AnomalyDetected = 0,
        AnalysisBegun = 1,
        SignificantEvidence = 2,
        AssessmentUpdated = 3,
        ConfidenceChanged = 4,
        AnalysisCompleted = 5,
        InvestigationNote = 6,
    }

    /// <summary>
    /// Historical Prospector communication event. Functional copy only — no personality.
    /// Bound to Scan ID + Anomaly ID for later Scan History.
    /// Immutable once appended — never overwrite earlier assessments.
    /// </summary>
    [Serializable]
    public sealed class ProspectorFinding
    {
        public int FindingId;
        public float GameTimestampHours;
        public int ScanId;
        public int AnomalyId;
        public ProspectorFindingEventType EventType;
        /// <summary>Player timeline heading, e.g. INITIAL SURVEY / Desk analysis / ASSESSMENT.</summary>
        public string SourceLabel = "";
        public string Message = "";
        /// <summary>Assessment wording believed at this moment (empty if unchanged).</summary>
        public string AssessmentTitle = "";
        public AssessmentConfidence Confidence = AssessmentConfidence.Low;
        public bool HasConfidence;
        /// <summary>Worker who generated this event (frozen). 0 = unknown / legacy.</summary>
        public int WorkerId;
        public string WorkerDisplayName = "";
        public readonly List<string> EvidenceClues = new(4);

        public string ShortLabel => Message;
    }

    /// <summary>
    /// Persistent findings store with anti-spam gates.
    /// Recent findings feed Dig Hood; full list is Scan History material.
    /// </summary>
    public sealed class ProspectorFindingsLog
    {
        public const int RecentDisplayCapacity = 6;
        public const int HistoryCapacity = 512;

        /// <summary>Min game-hours between findings for the same anomaly (non-complete events).</summary>
        public static float MinGapSameAnomalyHours =>
            ProspectorScanFormulas.UseTestingScanDurations ? 0.02f : 0.35f;

        /// <summary>Min game-hours between any two findings globally.</summary>
        public static float MinGapGlobalHours =>
            ProspectorScanFormulas.UseTestingScanDurations ? 0.008f : 0.12f;

        readonly List<ProspectorFinding> _all = new(128);
        readonly Queue<ProspectorFinding> _recent = new(RecentDisplayCapacity);
        readonly Dictionary<int, AnomalyAnnounceState> _announce = new(32);
        int _nextId = 1;
        float _lastAnyFindingHours = -999f;
        float _gameHours;
        int _authorWorkerId;
        string _authorDisplayName = "";

        public IReadOnlyList<ProspectorFinding> All => _all;
        public IReadOnlyCollection<ProspectorFinding> Recent => _recent;
        public int HighlightAnomalyId { get; private set; } = -1;
        public int HighlightScanId { get; private set; } = -1;
        public float HighlightUntilUnscaled { get; private set; }

        public event Action FindingsChanged;
        public event Action<ProspectorFinding> FindingAdded;

        struct AnomalyAnnounceState
        {
            public float LastFindingHours;
            public int TilesAtLastEvidenceFinding;
            public string LastTitle;
            public AssessmentConfidence LastConfidence;
            public bool HasConfidence;
            public bool DetectedAnnounced;
            public bool BegunAnnounced;
            public bool CompletedAnnounced;
            public bool MidPreviewDone;
        }

        public void SetGameHours(float absoluteGameHours) => _gameHours = absoluteGameHours;

        /// <summary>Stamp author on newly appended findings (Stage C attribution).</summary>
        public void SetAuthor(WorkerRuntime worker)
        {
            if (worker == null)
            {
                _authorWorkerId = 0;
                _authorDisplayName = "";
                return;
            }
            _authorWorkerId = worker.WorkerId;
            _authorDisplayName = worker.DisplayName ?? "";
        }

        public void Clear()
        {
            _all.Clear();
            _recent.Clear();
            _announce.Clear();
            _nextId = 1;
            _lastAnyFindingHours = -999f;
            _authorWorkerId = 0;
            _authorDisplayName = "";
            HighlightAnomalyId = -1;
            HighlightScanId = -1;
            FindingsChanged?.Invoke();
        }

        public void Highlight(int scanId, int anomalyId, float seconds = 12f)
        {
            HighlightScanId = scanId;
            HighlightAnomalyId = anomalyId;
            HighlightUntilUnscaled = Time.unscaledTime + seconds;
            FindingsChanged?.Invoke();
        }

        public bool IsHighlightActive(out int scanId, out int anomalyId)
        {
            scanId = HighlightScanId;
            anomalyId = HighlightAnomalyId;
            if (anomalyId < 0) return false;
            if (Time.unscaledTime > HighlightUntilUnscaled)
            {
                HighlightAnomalyId = -1;
                HighlightScanId = -1;
                return false;
            }
            return true;
        }

        public void PushInvestigationFinding(
            int scanId, int anomalyId, string message, string clue = null,
            string sourceLabel = null)
        {
            var f = new ProspectorFinding
            {
                GameTimestampHours = _gameHours,
                ScanId = scanId,
                AnomalyId = anomalyId,
                EventType = ProspectorFindingEventType.InvestigationNote,
                SourceLabel = string.IsNullOrEmpty(sourceLabel) ? "Investigation" : sourceLabel,
                Message = message ?? "Investigation note.",
                HasConfidence = false,
            };
            if (!string.IsNullOrEmpty(clue))
                f.EvidenceClues.Add(clue);
            TryAdd(f, force: true);
        }

        /// <summary>
        /// Immutable evidence notes for Scan History timeline. Never mutates prior events.
        /// </summary>
        public void PushEvidenceNotes(
            ProspectorAnomaly a,
            string[] lines,
            string sourceLabel)
        {
            if (a == null || lines == null || lines.Length == 0) return;
            string joined = lines[0] ?? "";
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i])) continue;
                if (joined.Length > 0) joined += " · ";
                joined += lines[i];
            }
            if (string.IsNullOrEmpty(joined)) return;

            bool assessed = a.AnalysisStatus == AnomalyAnalysisStatus.Assessed && a.Assessment != null;
            var f = new ProspectorFinding
            {
                GameTimestampHours = _gameHours,
                ScanId = a.ScanId,
                AnomalyId = a.AnomalyId,
                EventType = ProspectorFindingEventType.InvestigationNote,
                SourceLabel = string.IsNullOrEmpty(sourceLabel) ? "Investigation" : sourceLabel,
                Message = joined,
                AssessmentTitle = assessed ? (a.Assessment.QualitativeLabel ?? "") : "",
                Confidence = assessed ? a.Assessment.Confidence : AssessmentConfidence.Low,
                HasConfidence = assessed,
            };
            for (int i = 0; i < lines.Length && i < 4; i++)
            {
                if (!string.IsNullOrEmpty(lines[i]))
                    f.EvidenceClues.Add(lines[i]);
            }
            TryAdd(f, force: true);
        }

        public void NotifyAnomalyDetected(ProspectorAnomaly a)
        {
            if (a == null) return;
            var st = GetState(a.AnomalyId);
            if (st.DetectedAnnounced) return;
            st.DetectedAnnounced = true;
            st.TilesAtLastEvidenceFinding = a.TileCount;
            SetState(a.AnomalyId, st);

            TryAdd(new ProspectorFinding
            {
                GameTimestampHours = _gameHours,
                ScanId = a.ScanId,
                AnomalyId = a.AnomalyId,
                EventType = ProspectorFindingEventType.AnomalyDetected,
                SourceLabel = "Detected",
                Message = $"Anomaly #{a.AnomalyId:00} detected.",
            }, force: true);
        }

        public void NotifyAnalysisBegun(ProspectorAnomaly a)
        {
            if (a == null) return;
            var st = GetState(a.AnomalyId);
            if (st.BegunAnnounced) return;
            st.BegunAnnounced = true;
            SetState(a.AnomalyId, st);
            // Quiet Dig Hood — initial survey text goes to immutable findings log.
            string initial = ProspectorAnomalyInterpretation.InitialScanFindingText(a);
            TryAdd(new ProspectorFinding
            {
                GameTimestampHours = _gameHours,
                ScanId = a.ScanId,
                AnomalyId = a.AnomalyId,
                EventType = ProspectorFindingEventType.AnalysisBegun,
                SourceLabel = "INITIAL SURVEY",
                Message = initial,
            }, force: true);
        }

        public void NotifyAnalysisProgress(ProspectorAnomaly a, in ProspectorAnalysisStats stats)
        {
            // Private work — no continuous player updates mid-analysis.
            _ = a;
            _ = stats;
        }

        public void NotifySignificantEvidence(ProspectorAnomaly a)
        {
            if (a == null) return;
            // Only while analysing or after enough size — avoid early spam
            if (a.AnalysisStatus == AnomalyAnalysisStatus.Unanalysed
                && a.TileCount < ProspectorAnomalyInterpretation.MinTilesToAnalyse * 2)
                return;

            var st = GetState(a.AnomalyId);
            int prev = st.TilesAtLastEvidenceFinding;
            int need = Mathf.Max(ProspectorAnomalyInterpretation.MinTilesToAnalyse,
                Mathf.CeilToInt(prev * 1.35f));
            if (prev > 0 && a.TileCount < need) return;
            if (!CanAnnounceAnomaly(a.AnomalyId, st)) return;

            st.TilesAtLastEvidenceFinding = a.TileCount;
            st.LastFindingHours = _gameHours;
            SetState(a.AnomalyId, st);

            TryAdd(new ProspectorFinding
            {
                GameTimestampHours = _gameHours,
                ScanId = a.ScanId,
                AnomalyId = a.AnomalyId,
                EventType = ProspectorFindingEventType.SignificantEvidence,
                SourceLabel = "Evidence",
                Message = $"Anomaly #{a.AnomalyId:00}: significant new evidence ({a.TileCount} tiles).",
                AssessmentTitle = a.Assessment != null ? a.Assessment.Title : "",
                Confidence = a.Assessment != null ? a.Assessment.Confidence : AssessmentConfidence.Low,
                HasConfidence = a.AnalysisStatus == AnomalyAnalysisStatus.Assessed,
            });
        }

        /// <summary>
        /// First publish → ASSESSMENT. Later publish with different wording → REVISED ASSESSMENT
        /// (prior event kept; never overwritten).
        /// </summary>
        public void NotifyAnalysisCompleted(ProspectorAnomaly a)
        {
            if (a == null || a.Assessment == null) return;
            var st = GetState(a.AnomalyId);
            string title = a.Assessment.Title ?? "";
            var conf = a.Assessment.Confidence;
            string qualitative = a.QualitativeAssessment;

            if (!st.CompletedAnnounced)
            {
                st.CompletedAnnounced = true;
                st.LastTitle = qualitative;
                st.LastConfidence = conf;
                st.HasConfidence = true;
                st.LastFindingHours = _gameHours;
                SetState(a.AnomalyId, st);

                var done = new ProspectorFinding
                {
                    GameTimestampHours = _gameHours,
                    ScanId = a.ScanId,
                    AnomalyId = a.AnomalyId,
                    EventType = ProspectorFindingEventType.AnalysisCompleted,
                    SourceLabel = "ASSESSMENT",
                    Message =
                        $"PROSPECTOR: Boss, I've got something on {ProspectorAnomaly.SpokenId(a.AnomalyId)}. Check Tactical.",
                    AssessmentTitle = qualitative,
                    Confidence = conf,
                    HasConfidence = true,
                };
                if (!string.IsNullOrEmpty(qualitative))
                    done.EvidenceClues.Add(qualitative);
                CopyClues(done, a.Assessment);
                TryAdd(done, force: true);
                Highlight(a.ScanId, a.AnomalyId, 20f);
                a.MarkFindingUnread(
                    string.IsNullOrEmpty(a.LatestFindingSummary) ? qualitative : a.LatestFindingSummary);
                return;
            }

            // Revision — append only; keep earlier ASSESSMENT event intact
            bool sameTitle = string.Equals(st.LastTitle, qualitative, StringComparison.Ordinal);
            bool sameConf = st.HasConfidence && st.LastConfidence == conf;
            if (sameTitle && sameConf) return;

            st.LastTitle = qualitative;
            st.LastConfidence = conf;
            st.HasConfidence = true;
            st.LastFindingHours = _gameHours;
            SetState(a.AnomalyId, st);

            var revised = new ProspectorFinding
            {
                GameTimestampHours = _gameHours,
                ScanId = a.ScanId,
                AnomalyId = a.AnomalyId,
                EventType = ProspectorFindingEventType.AssessmentUpdated,
                SourceLabel = "REVISED ASSESSMENT",
                Message = qualitative,
                AssessmentTitle = qualitative,
                Confidence = conf,
                HasConfidence = true,
            };
            if (!string.IsNullOrEmpty(qualitative))
                revised.EvidenceClues.Add(qualitative);
            CopyClues(revised, a.Assessment);
            TryAdd(revised, force: true);
            Highlight(a.ScanId, a.AnomalyId, 14f);
            a.MarkFindingUnread(qualitative);
            _ = title;
        }

        /// <summary>Chronological findings for one anomaly within one scan (history browser).</summary>
        public void CollectForAnomaly(int scanId, int anomalyId, List<ProspectorFinding> into)
        {
            into.Clear();
            for (int i = 0; i < _all.Count; i++)
            {
                var f = _all[i];
                if (f.ScanId == scanId && f.AnomalyId == anomalyId)
                    into.Add(f);
            }
        }

        public int CountForScan(int scanId)
        {
            int n = 0;
            for (int i = 0; i < _all.Count; i++)
                if (_all[i].ScanId == scanId) n++;
            return n;
        }

        public int CountForAnomaly(int scanId, int anomalyId)
        {
            int n = 0;
            for (int i = 0; i < _all.Count; i++)
                if (_all[i].ScanId == scanId && _all[i].AnomalyId == anomalyId) n++;
            return n;
        }

        static void CopyClues(ProspectorFinding f, AnomalyAssessment a)
        {
            if (a == null) return;
            for (int i = 0; i < a.EvidenceNotes.Count && i < 4; i++)
                f.EvidenceClues.Add(a.EvidenceNotes[i]);
        }

        bool CanAnnounceAnomaly(int anomalyId, AnomalyAnnounceState st)
        {
            if (_gameHours - st.LastFindingHours < MinGapSameAnomalyHours) return false;
            if (_gameHours - _lastAnyFindingHours < MinGapGlobalHours) return false;
            return true;
        }

        bool TryAdd(ProspectorFinding f, bool force = false)
        {
            if (f == null) return false;
            if (!force)
            {
                var st = GetState(f.AnomalyId);
                if (!CanAnnounceAnomaly(f.AnomalyId, st)) return false;
                st.LastFindingHours = _gameHours;
                SetState(f.AnomalyId, st);
            }
            else
            {
                // Still respect a very small global gap except for forced history events stacked same frame
                if (_gameHours - _lastAnyFindingHours < MinGapGlobalHours * 0.25f
                    && f.EventType != ProspectorFindingEventType.AnomalyDetected
                    && f.EventType != ProspectorFindingEventType.AnalysisBegun
                    && f.EventType != ProspectorFindingEventType.AnalysisCompleted
                    && f.EventType != ProspectorFindingEventType.AssessmentUpdated
                    && f.EventType != ProspectorFindingEventType.InvestigationNote)
                    return false;
            }

            f.FindingId = _nextId++;
            f.GameTimestampHours = _gameHours;
            if (f.WorkerId <= 0 && _authorWorkerId > 0)
            {
                f.WorkerId = _authorWorkerId;
                f.WorkerDisplayName = _authorDisplayName;
            }
            _all.Add(f);
            while (_all.Count > HistoryCapacity)
                _all.RemoveAt(0);

            _recent.Enqueue(f);
            while (_recent.Count > RecentDisplayCapacity)
                _recent.Dequeue();

            _lastAnyFindingHours = _gameHours;
            DigHoodLog.Push($"FINDING | {f.Message}");
            FindingAdded?.Invoke(f);
            FindingsChanged?.Invoke();
            return true;
        }

        AnomalyAnnounceState GetState(int id)
        {
            if (_announce.TryGetValue(id, out var st)) return st;
            return new AnomalyAnnounceState { TilesAtLastEvidenceFinding = 0, LastFindingHours = -999f };
        }

        void SetState(int id, AnomalyAnnounceState st) => _announce[id] = st;
    }
}
