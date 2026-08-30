using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    public enum AnomalyAnalysisStatus : byte
    {
        Unanalysed = 0,
        Analysing = 1,
        Assessed = 2,
    }

    public enum AssessmentConfidence : byte
    {
        VeryLow = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        VeryHigh = 4,
    }

    /// <summary>
    /// Discrete evidence units gathered while investigating an anomaly.
    /// Planner decides the next need from what is still missing.
    /// </summary>
    public enum AnomalyEvidenceKind : byte
    {
        ScanInterpretation = 0,
        FieldEvidence = 1,
        LooseRockEvidence = 2,
        RefinerOpinion = 3,
        CrossCheck = 4,
    }

    /// <summary>
    /// Evidence-driven investigation need. Work sequence comes from the planner —
    /// not from analysis progress percentages.
    /// </summary>
    public enum AnomalyInvestigationNeed : byte
    {
        None = 0,
        NeedDeskInterpretation = 1,
        NeedFieldEvidence = 2,
        NeedLooseRockInspection = 3,
        NeedRefinerConsultation = 4,
        NeedCrossCheck = 5,
        ReadyForFinding = 6,
        Complete = 7,
    }

    /// <summary>
    /// Player-facing assessment. Belief % kept internal/debug — not shown in Tactical UI.
    /// </summary>
    [Serializable]
    public sealed class AnomalyAssessment
    {
        public string Title = "";
        public readonly List<string> EvidenceNotes = new(4);
        public AssessmentConfidence Confidence = AssessmentConfidence.Low;
        public float AnalysisProgress01;

        /// <summary>Temporary qualitative readout for the player (not Bedrock/Gold/Gas %).</summary>
        public string QualitativeLabel = "Unclear";

        /// <summary>Internal belief 0–100 — not player-facing.</summary>
        public int BeliefBedrockPct;
        public int BeliefGoldPct;
        public int BeliefGasPct;

        public static string ConfidenceLabel(AssessmentConfidence c) => c switch
        {
            AssessmentConfidence.VeryLow => "VERY LOW",
            AssessmentConfidence.Low => "LOW",
            AssessmentConfidence.Medium => "MEDIUM",
            AssessmentConfidence.High => "HIGH",
            AssessmentConfidence.VeryHigh => "VERY HIGH",
            _ => "LOW",
        };
    }

    /// <summary>
    /// Player-facing anomaly silhouette from one Scan's raw evidence.
    /// Assessment is interpretation — not Ground Truth material labels.
    /// </summary>
    [Serializable]
    public sealed class ProspectorAnomaly
    {
        public int AnomalyId;
        public int ScanId;
        public AnomalyAnalysisStatus AnalysisStatus = AnomalyAnalysisStatus.Unanalysed;
        public AnomalyAssessment Assessment;

        /// <summary>True evidence cells belonging to this anomaly (this Scan only).</summary>
        public readonly List<Vector2Int> EvidenceTiles = new(32);

        /// <summary>Display silhouette core (evidence ± geometry distortion).</summary>
        public readonly List<Vector2Int> SilhouetteTiles = new(48);

        /// <summary>Uncertain fringe — low geometry quality only. Not Ground Truth.</summary>
        public readonly List<Vector2Int> UncertainEdgeTiles = new(24);

        /// <summary>Chronological investigation findings (player-facing, placeholder text OK).</summary>
        public readonly List<string> FindingHistory = new(8);

        /// <summary>
        /// True after a published finding until the player notices it in Tactical View.
        /// </summary>
        public bool HasUnreadFinding;

        /// <summary>Latest published discovery line shown under NEW FINDING.</summary>
        public string LatestFindingSummary = "";

        /// <summary>Evidence units already recorded for this anomaly.</summary>
        public readonly List<AnomalyEvidenceKind> EvidenceAlreadyCollected = new(6);

        /// <summary>Ordered remaining needs after desk interpretation (planner-owned).</summary>
        public readonly List<AnomalyInvestigationNeed> RemainingNeeds = new(6);

        public AnomalyInvestigationNeed CurrentNeed = AnomalyInvestigationNeed.None;
        /// <summary>Short why-text for HUD, e.g. "composition signal remains ambiguous".</summary>
        public string NeedDetail = "";

        /// <summary>True after placeholder plan was built from the first desk step.</summary>
        public bool InvestigationPlanBuilt;

        /// <summary>Hidden physical survey traits — Truth/DEV only.</summary>
        public AnomalyGeoSignals HiddenSignals;
        /// <summary>Progressive observed traits — filled by investigation work.</summary>
        public AnomalyObservedSignals ObservedSignals;
        public ProspectorHiddenTruthKind DominantMaterial;
        public AnomalyMaterialVariant MaterialVariant;

        public Vector2 ApproximateCenterCells;
        public int MinX, MinY, MaxX, MaxY;
        public int TileCount;
        public float MeanSignalStrength;
        public float MaxSignalStrength;
        public float GeometryQuality;
        /// <summary>Closest evidence tile distance to scanner — closest-first analysis.</summary>
        public float DistanceFromScannerCells = float.MaxValue;
        public bool Frozen;

        /// <summary>Internal tallies for Stage-4 signal generation — never shown to player.</summary>
        internal int HiddenBedrockTiles;
        internal int HiddenGoldTiles;
        internal int HiddenGasTiles;

        public float AnalysisElapsedHours;
        public float AnalysisDurationHours;
        public bool QueuedForAnalysis;

        public int BoundsWidth => MaxX - MinX + 1;
        public int BoundsHeight => MaxY - MinY + 1;
        public int ApproximateSizeCells => TileCount;

        /// <summary>Internal/debug progress only — do not show to the player.</summary>
        public float AnalysisProgress01 =>
            AnalysisStatus == AnomalyAnalysisStatus.Assessed || CurrentNeed == AnomalyInvestigationNeed.Complete
                ? 1f
                : AnalysisDurationHours > 0.0001f
                    ? Mathf.Clamp01(AnalysisElapsedHours / AnalysisDurationHours)
                    : 0f;

        public string QualitativeAssessment =>
            Assessment != null && !string.IsNullOrEmpty(Assessment.QualitativeLabel)
                ? Assessment.QualitativeLabel
                : "Unclear";

        /// <summary>Alias for NeedDetail — player/dev “Reason” line.</summary>
        public string Reason => NeedDetail;

        public bool HasEvidence(AnomalyEvidenceKind kind)
        {
            for (int i = 0; i < EvidenceAlreadyCollected.Count; i++)
                if (EvidenceAlreadyCollected[i] == kind) return true;
            return false;
        }

        public void RecordEvidence(AnomalyEvidenceKind kind)
        {
            if (HasEvidence(kind)) return;
            EvidenceAlreadyCollected.Add(kind);
        }

        public static string EvidenceLabel(AnomalyEvidenceKind kind) => kind switch
        {
            AnomalyEvidenceKind.ScanInterpretation => "Scan interpretation",
            AnomalyEvidenceKind.FieldEvidence => "Field inspection",
            AnomalyEvidenceKind.LooseRockEvidence => "Loose rock inspection",
            AnomalyEvidenceKind.RefinerOpinion => "Refiner opinion",
            AnomalyEvidenceKind.CrossCheck => "Cross-check",
            _ => "Evidence",
        };

        public static string EvidenceStatusLine(AnomalyEvidenceKind kind, bool done) =>
            done ? $"{EvidenceLabel(kind)} complete" : $"{EvidenceLabel(kind)} pending";

        public static string NeedWorkLabel(AnomalyInvestigationNeed need) => need switch
        {
            AnomalyInvestigationNeed.NeedDeskInterpretation => "Reviewing scan response",
            AnomalyInvestigationNeed.NeedFieldEvidence => "Gathering field evidence",
            AnomalyInvestigationNeed.NeedLooseRockInspection => "Inspecting excavation face",
            AnomalyInvestigationNeed.NeedRefinerConsultation => "Going to Refiner",
            AnomalyInvestigationNeed.NeedCrossCheck => "Cross-checking evidence",
            AnomalyInvestigationNeed.ReadyForFinding => "Preparing conclusion",
            AnomalyInvestigationNeed.Complete => "Complete",
            _ => "Standby",
        };

        public static string NeedNextLabel(AnomalyInvestigationNeed need) => need switch
        {
            AnomalyInvestigationNeed.NeedDeskInterpretation => "Desk interpretation",
            AnomalyInvestigationNeed.NeedFieldEvidence => "Field inspection",
            AnomalyInvestigationNeed.NeedLooseRockInspection => "Loose rock inspection",
            AnomalyInvestigationNeed.NeedRefinerConsultation => "Refiner consultation",
            AnomalyInvestigationNeed.NeedCrossCheck => "Cross-check findings",
            AnomalyInvestigationNeed.ReadyForFinding => "Publish finding",
            AnomalyInvestigationNeed.Complete => "Done",
            _ => "—",
        };

        /// <summary>Spoken anomaly name for natural Prospector lines (Twelve, not #12).</summary>
        public static string SpokenId(int anomalyId)
        {
            if (anomalyId <= 0) return "this anomaly";
            if (anomalyId < SmallOrdinals.Length) return SmallOrdinals[anomalyId];
            return $"Anomaly {anomalyId}";
        }

        static readonly string[] SmallOrdinals =
        {
            "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine",
            "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen",
            "Eighteen", "Nineteen", "Twenty", "Twenty-One", "Twenty-Two", "Twenty-Three",
            "Twenty-Four", "Twenty-Five", "Twenty-Six", "Twenty-Seven", "Twenty-Eight",
            "Twenty-Nine", "Thirty", "Thirty-One", "Thirty-Two", "Thirty-Three", "Thirty-Four",
            "Thirty-Five", "Thirty-Six", "Thirty-Seven", "Thirty-Eight", "Thirty-Nine", "Forty",
        };

        public void MarkFindingUnread(string summary)
        {
            HasUnreadFinding = true;
            LatestFindingSummary = summary ?? "";
        }

        public void MarkFindingSeen()
        {
            HasUnreadFinding = false;
        }

        public void SetNeed(AnomalyInvestigationNeed need, string detail)
        {
            CurrentNeed = need;
            NeedDetail = detail ?? "";
        }

        public void AddFindingEntry(string entry)
        {
            if (string.IsNullOrEmpty(entry)) return;
            FindingHistory.Add(entry);
            while (FindingHistory.Count > 12)
                FindingHistory.RemoveAt(0);
        }

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

        internal void TallyHidden(ProspectorHiddenTruthKind kind)
        {
            switch (kind)
            {
                case ProspectorHiddenTruthKind.Bedrock: HiddenBedrockTiles++; break;
                case ProspectorHiddenTruthKind.Gold:
                case ProspectorHiddenTruthKind.Diamond: HiddenGoldTiles++; break;
                case ProspectorHiddenTruthKind.Gas: HiddenGasTiles++; break;
            }
        }

        internal void AbsorbHiddenTallies(ProspectorAnomaly other)
        {
            if (other == null) return;
            HiddenBedrockTiles += other.HiddenBedrockTiles;
            HiddenGoldTiles += other.HiddenGoldTiles;
            HiddenGasTiles += other.HiddenGasTiles;
            for (int i = 0; i < other.FindingHistory.Count; i++)
                AddFindingEntry(other.FindingHistory[i]);
            for (int i = 0; i < other.EvidenceAlreadyCollected.Count; i++)
                RecordEvidence(other.EvidenceAlreadyCollected[i]);
            if (other.Assessment != null && Assessment == null)
                Assessment = other.Assessment;
            else if (other.Assessment != null && Assessment != null
                     && string.IsNullOrEmpty(Assessment.QualitativeLabel)
                     && !string.IsNullOrEmpty(other.Assessment.QualitativeLabel))
                Assessment.QualitativeLabel = other.Assessment.QualitativeLabel;
        }
    }
}
