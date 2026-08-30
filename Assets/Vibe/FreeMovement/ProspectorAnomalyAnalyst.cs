using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Closest-first anomaly analysis queue.
    /// Desk clocks are work units (interpretation / cross-check) — not trip triggers.
    /// Findings publish only via <see cref="PublishFinding"/> when ReadyForFinding.
    /// </summary>
    public sealed class ProspectorAnomalyAnalyst
    {
        readonly List<ProspectorAnomaly> _queue = new(16);
        ProspectorScanRecord _record;
        ProspectorAnalysisStats _stats;
        ProspectorAnomaly _current;
        int _lastLoggedProgressPct = -1;
        AnomalyEvidenceKind _activeDeskStep = AnomalyEvidenceKind.ScanInterpretation;

        public ProspectorFindingsLog Findings { get; set; }
        public ProspectorAnomaly Current => _current;
        public int QueueCount => _queue.Count;
        public bool HasWork =>
            _current != null
            || _queue.Count > 0
            || HasIncompleteAnomalies();

        bool AnalysisUnlocked => _record != null && _record.FrozenAnomalies;

        /// <summary>Desk clock only runs for interpretation / cross-check needs.</summary>
        public bool IsDeskClockActive =>
            _current != null
            && (_current.CurrentNeed == AnomalyInvestigationNeed.NeedDeskInterpretation
                || _current.CurrentNeed == AnomalyInvestigationNeed.NeedCrossCheck);

        public void Bind(ProspectorScanRecord record, in ProspectorAnalysisStats stats)
        {
            if (_current != null && _current.AnalysisStatus == AnomalyAnalysisStatus.Analysing)
                AbandonCurrentWithoutPublish();
            _queue.Clear();
            _record = record;
            _stats = stats;
            _current = null;
            _lastLoggedProgressPct = -1;
        }

        public void Clear()
        {
            _queue.Clear();
            _current = null;
            _record = null;
            _lastLoggedProgressPct = -1;
        }

        public void RefreshQueueFromRecord()
        {
            if (!AnalysisUnlocked) return;
            for (int i = 0; i < _record.Anomalies.Count; i++)
                TryEnqueue(_record.Anomalies[i]);
            SortQueueClosestFirst();
        }

        public void TryEnqueue(ProspectorAnomaly a)
        {
            if (!AnalysisUnlocked) return;
            if (a == null || _record == null) return;
            if (a.ScanId != _record.ScanId) return;
            if (a.AnalysisStatus == AnomalyAnalysisStatus.Assessed) return;
            if (a.CurrentNeed == AnomalyInvestigationNeed.Complete) return;
            if (a.QueuedForAnalysis) return;
            if (a.TileCount < ProspectorAnomalyInterpretation.MinTilesToAnalyse) return;
            // Mid-plan anomalies are resumed via TryStartNext, not re-queued as Unanalysed
            if (a.AnalysisStatus == AnomalyAnalysisStatus.Analysing) return;

            a.QueuedForAnalysis = true;
            _queue.Add(a);
            SortQueueClosestFirst();
        }

        public void NotifyMerged(ProspectorAnomaly keep, ProspectorAnomaly absorb)
        {
            if (absorb == null) return;
            RemoveFromQueue(absorb);
            absorb.QueuedForAnalysis = false;

            if (_current == absorb)
            {
                if (keep != null && keep.CurrentNeed != AnomalyInvestigationNeed.Complete
                    && keep.AnalysisStatus != AnomalyAnalysisStatus.Assessed)
                {
                    keep.AnalysisStatus = AnomalyAnalysisStatus.Analysing;
                    keep.AnalysisElapsedHours = absorb.AnalysisElapsedHours;
                    keep.AnalysisDurationHours = absorb.AnalysisDurationHours;
                    keep.Assessment ??= new AnomalyAssessment();
                    keep.Assessment.AnalysisProgress01 = absorb.AnalysisProgress01;
                    keep.QueuedForAnalysis = true;
                    _current = keep;
                    RemoveFromQueue(keep);
                    RefreshInternalBelief();
                }
                else
                {
                    _current = null;
                }
                absorb.AnalysisStatus = AnomalyAnalysisStatus.Unanalysed;
            }
            else if (keep != null
                     && absorb.AnalysisStatus == AnomalyAnalysisStatus.Assessed
                     && keep.AnalysisStatus != AnomalyAnalysisStatus.Assessed)
            {
                keep.Assessment = absorb.Assessment;
                keep.AnalysisStatus = AnomalyAnalysisStatus.Assessed;
                keep.QueuedForAnalysis = false;
                ProspectorInvestigationPlanner.MarkComplete(keep);
                RemoveFromQueue(keep);
            }
            else if (AnalysisUnlocked
                     && keep != null
                     && keep.AnalysisStatus == AnomalyAnalysisStatus.Unanalysed
                     && !keep.QueuedForAnalysis
                     && keep.TileCount >= ProspectorAnomalyInterpretation.MinTilesToAnalyse)
            {
                TryEnqueue(keep);
            }
        }

        void RemoveFromQueue(ProspectorAnomaly a)
        {
            for (int i = _queue.Count - 1; i >= 0; i--)
                if (_queue[i] == a) _queue.RemoveAt(i);
        }

        void SortQueueClosestFirst()
        {
            _queue.Sort((x, y) =>
            {
                int d = x.DistanceFromScannerCells.CompareTo(y.DistanceFromScannerCells);
                return d != 0 ? d : x.AnomalyId.CompareTo(y.AnomalyId);
            });
        }

        /// <summary>
        /// Advance desk work clock. <paramref name="excavatorDistanceCells"/> feeds the planner
        /// when the first desk interpretation completes (negative = unknown).
        /// </summary>
        public bool TickGameHours(float gameHoursDelta, float excavatorDistanceCells = -1f)
        {
            if (gameHoursDelta <= 0f || !AnalysisUnlocked) return false;
            bool changed = false;

            if (_current == null)
                changed |= TryStartNext();

            if (_current == null) return changed;

            EnsureDeskClockMatchesNeed();

            if (!IsDeskClockActive)
                return changed;

            _current.AnalysisElapsedHours += gameHoursDelta;
            if (_current.Assessment == null)
                _current.Assessment = new AnomalyAssessment { QualitativeLabel = "Unclear" };

            RefreshInternalBelief();
            LogProgressIfNeeded();
            Findings?.NotifyAnalysisProgress(_current, _stats);
            changed = true;

            if (_current.AnalysisElapsedHours + 0.0001f < _current.AnalysisDurationHours)
                return changed;

            CompleteDeskWorkUnit(excavatorDistanceCells);
            return true;
        }

        /// <summary>
        /// When planner advances to CrossCheck, reset a dedicated desk clock.
        /// </summary>
        public void EnsureDeskClockMatchesNeed()
        {
            if (_current == null) return;

            if (_current.CurrentNeed == AnomalyInvestigationNeed.NeedDeskInterpretation
                && _activeDeskStep != AnomalyEvidenceKind.ScanInterpretation)
            {
                BeginDeskInterpretationClock();
            }
            else if (_current.CurrentNeed == AnomalyInvestigationNeed.NeedCrossCheck
                     && _activeDeskStep != AnomalyEvidenceKind.CrossCheck)
            {
                BeginCrossCheckClock();
            }
        }

        void BeginDeskInterpretationClock()
        {
            _activeDeskStep = AnomalyEvidenceKind.ScanInterpretation;
            _current.AnalysisElapsedHours = 0f;
            _current.AnalysisDurationHours =
                ProspectorAnomalyInterpretation.AnalysisDurationHours(_stats, _current);
            _lastLoggedProgressPct = -1;
            ProspectorInvestigationPlanner.BeginDeskInterpretation(_current);
        }

        void BeginCrossCheckClock()
        {
            _activeDeskStep = AnomalyEvidenceKind.CrossCheck;
            _current.AnalysisElapsedHours = 0f;
            _current.AnalysisDurationHours =
                ProspectorAnomalyInterpretation.CrossCheckDurationHours(_stats, _current);
            _lastLoggedProgressPct = -1;
            _current.SetNeed(
                AnomalyInvestigationNeed.NeedCrossCheck,
                ProspectorInvestigationPlanner.ReasonForNeed(
                    AnomalyInvestigationNeed.NeedCrossCheck, _current));
            DigHoodLog.Push(
                $"PROSPECTOR | cross-check desk clock {_current.AnalysisDurationHours:0.##}h " +
                $"(#{_current.AnomalyId:00})");
        }

        void CompleteDeskWorkUnit(float excavatorDistanceCells)
        {
            if (_current == null) return;
            var a = _current;
            a.AnalysisElapsedHours = a.AnalysisDurationHours;

            if (_activeDeskStep == AnomalyEvidenceKind.ScanInterpretation
                || a.CurrentNeed == AnomalyInvestigationNeed.NeedDeskInterpretation)
            {
                var lines = ProspectorGeoEvidence.RevealDeskInterpretation(a, _stats);
                ProspectorGeoEvidence.AppendFindings(a, lines, Findings, "Desk analysis");
                ProspectorInvestigationPlanner.ApplyAfterEvidence(
                    a, AnomalyEvidenceKind.ScanInterpretation, excavatorDistanceCells);
                DigHoodLog.Push(
                    $"PROSPECTOR | desk interpretation done → {a.CurrentNeed} (#{a.AnomalyId:00})");
                Debug.Log(
                    $"[PROSPECTOR] Desk interpretation complete: Anomaly #{a.AnomalyId:00} " +
                    $"next={a.CurrentNeed} reason={a.NeedDetail}");
                return;
            }

            if (_activeDeskStep == AnomalyEvidenceKind.CrossCheck
                || a.CurrentNeed == AnomalyInvestigationNeed.NeedCrossCheck)
            {
                var lines = ProspectorGeoEvidence.RevealCrossCheck(a, _stats);
                ProspectorGeoEvidence.AppendFindings(a, lines, Findings, "Cross-check");
                ProspectorInvestigationPlanner.ApplyAfterEvidence(
                    a, AnomalyEvidenceKind.CrossCheck, excavatorDistanceCells);
                DigHoodLog.Push(
                    $"PROSPECTOR | cross-check done → {a.CurrentNeed} (#{a.AnomalyId:00})");
                Debug.Log(
                    $"[PROSPECTOR] Cross-check complete: Anomaly #{a.AnomalyId:00} " +
                    $"next={a.CurrentNeed}");
            }
        }

        public bool PublishFinding()
        {
            if (_current == null) return false;
            if (_current.CurrentNeed != AnomalyInvestigationNeed.ReadyForFinding)
                return false;

            var a = _current;
            float skill = ProspectorGeoEvidence.PublishSkill01(a, _stats);
            // Skilled: Mixed = contradiction — chase remaining useful evidence before closing
            if (ProspectorInvestigationPlanner.TryDeferMixedWithFollowUp(a, skill))
            {
                DigHoodLog.Push(
                    $"PROSPECTOR | contradiction on #{a.AnomalyId:00} — continuing ({a.CurrentNeed})");
                Debug.Log(
                    $"[PROSPECTOR] Deferred Mixed publish: Anomaly #{a.AnomalyId:00} → {a.CurrentNeed}");
                return false;
            }

            a.Assessment = ProspectorGeoEvidence.BuildAssessment(a, _stats);
            a.AnalysisStatus = AnomalyAnalysisStatus.Assessed;
            a.AnalysisElapsedHours = a.AnalysisDurationHours;
            a.QueuedForAnalysis = false;
            string findingLine =
                ProspectorAnomalyInterpretation.RevisedInterpretationFindingText(a.Assessment);
            a.AddFindingEntry(findingLine);
            a.MarkFindingUnread(findingLine);
            ProspectorInvestigationPlanner.MarkComplete(a);
            Findings?.NotifyAnalysisCompleted(a);
            DigHoodLog.Push(
                $"PROSPECTOR | FINDING published Anomaly #{a.AnomalyId:00} — {a.QualitativeAssessment}");
            Debug.Log(
                $"[PROSPECTOR] Finding published: Anomaly #{a.AnomalyId:00} | {a.QualitativeAssessment}");

            _current = null;
            _lastLoggedProgressPct = -1;

            if (TryStartNext())
                Debug.Log($"[PROSPECTOR] Next anomaly: #{_current.AnomalyId:00}");
            else
                Debug.Log("[PROSPECTOR] Next anomaly: (none — queue empty)");

            return true;
        }

        void AbandonCurrentWithoutPublish()
        {
            if (_current == null) return;
            _current.QueuedForAnalysis = false;
            _current = null;
        }

        void LogProgressIfNeeded()
        {
            if (_current == null) return;
            int pct = Mathf.Clamp(Mathf.FloorToInt(_current.AnalysisProgress01 * 100f), 0, 100);
            int bucket = (pct / 10) * 10;
            if (bucket == _lastLoggedProgressPct) return;
            if (bucket == 0 && _lastLoggedProgressPct < 0)
            {
                _lastLoggedProgressPct = 0;
                return;
            }
            if (bucket <= _lastLoggedProgressPct) return;
            _lastLoggedProgressPct = bucket;
            Debug.Log($"[PROSPECTOR] Desk clock (debug): {bucket}% ({_activeDeskStep})");
        }

        void RefreshInternalBelief()
        {
            if (_current == null) return;
            if (_current.Assessment == null)
                _current.Assessment = new AnomalyAssessment { QualitativeLabel = "Unclear" };
            ProspectorAnomalyInterpretation.ApplyLiveBelief(
                _current.Assessment, _current, _stats, _current.AnalysisProgress01);
            if (_current.AnalysisStatus == AnomalyAnalysisStatus.Analysing)
                _current.Assessment.QualitativeLabel = "Unclear";
        }

        bool TryStartNext()
        {
            if (!AnalysisUnlocked) return false;
            SortQueueClosestFirst();
            while (_queue.Count > 0)
            {
                var next = _queue[0];
                _queue.RemoveAt(0);
                if (next == null) continue;
                if (next.AnalysisStatus == AnomalyAnalysisStatus.Assessed) continue;
                if (next.CurrentNeed == AnomalyInvestigationNeed.Complete) continue;
                if (next.TileCount < ProspectorAnomalyInterpretation.MinTilesToAnalyse)
                {
                    next.QueuedForAnalysis = false;
                    continue;
                }

                _current = next;
                _current.AnalysisStatus = AnomalyAnalysisStatus.Analysing;
                _current.EvidenceAlreadyCollected.Clear();
                _current.RemainingNeeds.Clear();
                _current.InvestigationPlanBuilt = false;
                _current.Assessment = new AnomalyAssessment
                {
                    Title = "ANALYSING…",
                    AnalysisProgress01 = 0f,
                    Confidence = AssessmentConfidence.VeryLow,
                    QualitativeLabel = "Unclear",
                    BeliefBedrockPct = 34,
                    BeliefGoldPct = 33,
                    BeliefGasPct = 33,
                };
                BeginDeskInterpretationClock();
                RefreshInternalBelief();
                if (_current.FindingHistory.Count == 0)
                    _current.AddFindingEntry(
                        ProspectorAnomalyInterpretation.InitialScanFindingText(_current));
                Findings?.NotifyAnalysisBegun(_current);
                Debug.Log(
                    $"[PROSPECTOR] Analysis started: Anomaly #{_current.AnomalyId:00} | " +
                    $"desk {_current.AnalysisDurationHours:0.##}h | tiles {_current.TileCount}");
                return true;
            }

            // Resume incomplete anomaly still mid-plan (e.g. after sleep) if not queued
            if (_record != null)
            {
                for (int i = 0; i < _record.Anomalies.Count; i++)
                {
                    var a = _record.Anomalies[i];
                    if (a.CurrentNeed == AnomalyInvestigationNeed.Complete) continue;
                    if (a.AnalysisStatus == AnomalyAnalysisStatus.Assessed) continue;
                    if (a.AnalysisStatus == AnomalyAnalysisStatus.Analysing
                        || a.InvestigationPlanBuilt
                        || a.HasEvidence(AnomalyEvidenceKind.ScanInterpretation))
                    {
                        _current = a;
                        a.QueuedForAnalysis = true;
                        EnsureDeskClockMatchesNeed();
                        return true;
                    }
                }
            }

            _current = null;
            return false;
        }

        bool HasIncompleteAnomalies()
        {
            if (_record == null) return false;
            for (int i = 0; i < _record.Anomalies.Count; i++)
            {
                var a = _record.Anomalies[i];
                if (a.CurrentNeed != AnomalyInvestigationNeed.Complete
                    && a.AnalysisStatus != AnomalyAnalysisStatus.Assessed
                    && a.TileCount >= ProspectorAnomalyInterpretation.MinTilesToAnalyse)
                    return true;
            }
            return false;
        }

        public void OnScanFrozen()
        {
            RefreshQueueFromRecord();
            DigHoodLog.Push(
                $"SCAN #{_record.ScanId} FROZEN | raw anomalies {_record.Anomalies.Count} — analysis queueing");
            Debug.Log(
                $"[PROSPECTOR] Scan #{_record.ScanId} frozen — " +
                $"{_record.Anomalies.Count} raw anomalies queued for timed analysis");
        }
    }
}
