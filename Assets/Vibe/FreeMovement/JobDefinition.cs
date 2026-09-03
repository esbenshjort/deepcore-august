using System;
using System.Collections.Generic;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Describes how a job interprets a worker's existing <see cref="WorkerStats"/>.
    /// Does not own or duplicate stats. No suitability score / XP / class.
    /// </summary>
    public sealed class JobDefinition
    {
        public JobType JobType { get; }
        public string DisplayName { get; }
        /// <summary>Stage B temporary key — maps to existing role body / future IWorkProvider.</summary>
        public string BehaviourKey { get; }
        /// <summary>Underlying stats this job currently cares about (player preview).</summary>
        public IReadOnlyList<WorkerStatId> RelevantStats { get; }
        /// <summary>
        /// True when RelevantStats are UI/intended only — not yet read by live job formulas.
        /// Hauling / Refining / Engineering until Stage formula work.
        /// </summary>
        public bool RelevantStatsAreProvisional { get; }

        public JobDefinition(
            JobType jobType,
            string displayName,
            string behaviourKey,
            WorkerStatId[] relevantStats,
            bool relevantStatsAreProvisional = false)
        {
            JobType = jobType;
            DisplayName = displayName ?? jobType.ToString();
            BehaviourKey = behaviourKey ?? "";
            RelevantStats = relevantStats ?? Array.Empty<WorkerStatId>();
            RelevantStatsAreProvisional = relevantStatsAreProvisional;
        }
    }

    /// <summary>
    /// Central static job catalog + stat preview lists.
    /// Source of truth: existing live formulas where they exist; otherwise small intended sets.
    /// </summary>
    public static class JobStatPreview
    {
        static readonly JobDefinition UnassignedDef = new(
            JobType.Unassigned, "Unassigned", "", Array.Empty<WorkerStatId>());

        /// <summary>
        /// Prospecting — scan duration, setup, spatial detection, desk/investigation analysis.
        /// From ProspectorScanFormulas, ProspectorScannerSetup, ProspectorAnomalyInterpretation,
        /// ProspectorDetection / ScanSession.
        /// </summary>
        static readonly JobDefinition ProspectingDef = new(
            JobType.Prospecting,
            "Prospecting",
            "body.prospector",
            new[]
            {
                WorkerStatId.Calibration,
                WorkerStatId.Focus,
                WorkerStatId.Acoustics,
                WorkerStatId.SpatialGeometry,
                WorkerStatId.Mechanics,
                WorkerStatId.HeavyLifting,
                WorkerStatId.Mathematics,
                WorkerStatId.Lithology,
                WorkerStatId.Mineralogy,
                WorkerStatId.Chemistry,
                WorkerStatId.Composure,
                WorkerStatId.Intuition,
                WorkerStatId.WorkRate,
            });

        /// <summary>
        /// Excavation — MiningDamage, weak points, stamina, heat/safety / cadence rolls.
        /// From MiningDamage + FreeWorkerController dig/heat loops.
        /// </summary>
        static readonly JobDefinition ExcavationDef = new(
            JobType.Excavation,
            "Excavation",
            "body.excavator",
            new[]
            {
                WorkerStatId.RawPower,
                WorkerStatId.Lithology,
                WorkerStatId.SpatialGeometry,
                WorkerStatId.Finesse,
                WorkerStatId.Stamina,
                WorkerStatId.Recovery,
                WorkerStatId.HeatTolerance,
                WorkerStatId.Rhythm,
                WorkerStatId.SafetyProtocol,
                WorkerStatId.Focus,
                WorkerStatId.Determination,
                WorkerStatId.Composure,
                WorkerStatId.Toughness,
            });

        /// <summary>
        /// Hauling — PROVISIONAL UI preview. No live formula reads yet.
        /// </summary>
        static readonly JobDefinition HaulingDef = new(
            JobType.Hauling,
            "Hauling",
            "body.hauler",
            new[]
            {
                WorkerStatId.HeavyLifting,
                WorkerStatId.Stamina,
                WorkerStatId.Logistics,
                WorkerStatId.Agility,
                WorkerStatId.SpatialGeometry,
            },
            relevantStatsAreProvisional: true);

        /// <summary>
        /// Refining — PROVISIONAL UI preview. No live formula reads yet.
        /// </summary>
        static readonly JobDefinition RefiningDef = new(
            JobType.Refining,
            "Refining",
            "body.refiner",
            new[]
            {
                WorkerStatId.Chemistry,
                WorkerStatId.Mineralogy,
                WorkerStatId.Finesse,
                WorkerStatId.Focus,
                WorkerStatId.Calibration,
            },
            relevantStatsAreProvisional: true);

        /// <summary>
        /// Engineering — PROVISIONAL UI preview. No live formula reads yet.
        /// </summary>
        static readonly JobDefinition EngineeringDef = new(
            JobType.Engineering,
            "Engineering",
            "body.engineer",
            new[]
            {
                WorkerStatId.Mechanics,
                WorkerStatId.HeavyLifting,
                WorkerStatId.Calibration,
                WorkerStatId.SpatialGeometry,
                WorkerStatId.Focus,
            },
            relevantStatsAreProvisional: true);

        static readonly Dictionary<JobType, JobDefinition> ByType = new()
        {
            { JobType.Unassigned, UnassignedDef },
            { JobType.Prospecting, ProspectingDef },
            { JobType.Excavation, ExcavationDef },
            { JobType.Hauling, HaulingDef },
            { JobType.Refining, RefiningDef },
            { JobType.Engineering, EngineeringDef },
        };

        public static JobDefinition Get(JobType job) =>
            ByType.TryGetValue(job, out var d) ? d : UnassignedDef;

        public static IReadOnlyList<WorkerStatId> RelevantStats(JobType job) =>
            Get(job).RelevantStats;

        public static string DisplayName(JobType job) => Get(job).DisplayName;

        public static string BehaviourKey(JobType job) => Get(job).BehaviourKey;

        public static string StatDisplayName(WorkerStatId id) => id switch
        {
            WorkerStatId.RawPower => "Raw Power",
            WorkerStatId.Stamina => "Stamina",
            WorkerStatId.Recovery => "Recovery",
            WorkerStatId.Balance => "Balance",
            WorkerStatId.Finesse => "Finesse",
            WorkerStatId.HeavyLifting => "Heavy Lifting",
            WorkerStatId.Rhythm => "Rhythm",
            WorkerStatId.HeatTolerance => "Heat Tolerance",
            WorkerStatId.Agility => "Agility",
            WorkerStatId.Toughness => "Toughness",
            WorkerStatId.Calibration => "Calibration",
            WorkerStatId.Mathematics => "Mathematics",
            WorkerStatId.Lithology => "Lithology",
            WorkerStatId.Mineralogy => "Mineralogy",
            WorkerStatId.Acoustics => "Acoustics",
            WorkerStatId.Mechanics => "Mechanics",
            WorkerStatId.SpatialGeometry => "Spatial Geometry",
            WorkerStatId.Chemistry => "Chemistry",
            WorkerStatId.Logistics => "Logistics",
            WorkerStatId.SafetyProtocol => "Safety Protocol",
            WorkerStatId.Composure => "Composure",
            WorkerStatId.Bravery => "Bravery",
            WorkerStatId.Affinity => "Affinity",
            WorkerStatId.Focus => "Focus",
            WorkerStatId.WorkRate => "Work Rate",
            WorkerStatId.Determination => "Determination",
            WorkerStatId.Tolerance => "Tolerance",
            WorkerStatId.Empathy => "Empathy",
            WorkerStatId.Leadership => "Leadership",
            WorkerStatId.Intuition => "Intuition",
            _ => id.ToString(),
        };

        /// <summary>Default jobs that have a prototype worker (excludes Unassigned).</summary>
        public static readonly JobType[] OccupiedJobs =
        {
            JobType.Prospecting,
            JobType.Excavation,
            JobType.Hauling,
            JobType.Refining,
            JobType.Engineering,
        };
    }
}
