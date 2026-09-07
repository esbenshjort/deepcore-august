using System;
using System.Collections.Generic;

namespace DeepCore.FreeMovement
{
    public enum RecruitmentTraitKind : byte
    {
        Positive = 0,
        Negative = 1,
        Mixed = 2,
    }

    public enum RecruitmentTraitId : byte
    {
        CalmUnderPressure = 0,
        FastLearner = 1,
        VeteranMiner = 2,
        PoorRecovery = 3,
        ShortTemper = 4,
        TeamPlayer = 5,
        LoneWolf = 6,
        HeavySleeper = 7,
        InjuryProne = 8,
        Methodical = 9,
        Reckless = 10,
        NaturalLeader = 11,
    }

    /// <summary>Modest V1 trait — supports hiring tradeoffs, not a perk tree.</summary>
    public readonly struct RecruitmentTraitDef
    {
        public readonly RecruitmentTraitId Id;
        public readonly string Name;
        public readonly RecruitmentTraitKind Kind;
        public readonly string Summary;
        /// <summary>One-line effect description (stat deltas applied on hire).</summary>
        public readonly string EffectLabel;

        public RecruitmentTraitDef(
            RecruitmentTraitId id,
            string name,
            RecruitmentTraitKind kind,
            string summary,
            string effectLabel)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Summary = summary;
            EffectLabel = effectLabel;
        }
    }

    public static class RecruitmentTraits
    {
        static readonly Dictionary<RecruitmentTraitId, RecruitmentTraitDef> ById = new()
        {
            {
                RecruitmentTraitId.CalmUnderPressure,
                new(RecruitmentTraitId.CalmUnderPressure, "Calm Under Pressure",
                    RecruitmentTraitKind.Positive,
                    "Keeps a cool head when heat, risk, or conflict rises.",
                    "+2 Composure, +1 Focus")
            },
            {
                RecruitmentTraitId.FastLearner,
                new(RecruitmentTraitId.FastLearner, "Fast Learner",
                    RecruitmentTraitKind.Positive,
                    "Picks up field technique quickly once shown once.",
                    "+1 Calibration, +1 Work Rate")
            },
            {
                RecruitmentTraitId.VeteranMiner,
                new(RecruitmentTraitId.VeteranMiner, "Veteran Miner",
                    RecruitmentTraitKind.Positive,
                    "Years underground — knows when rock is lying.",
                    "+2 Lithology, +1 Safety Protocol, +1 Toughness")
            },
            {
                RecruitmentTraitId.PoorRecovery,
                new(RecruitmentTraitId.PoorRecovery, "Poor Recovery",
                    RecruitmentTraitKind.Negative,
                    "Takes longer to bounce back from hard shifts.",
                    "−2 Recovery, −1 Stamina")
            },
            {
                RecruitmentTraitId.ShortTemper,
                new(RecruitmentTraitId.ShortTemper, "Short Temper",
                    RecruitmentTraitKind.Negative,
                    "Snaps under friction; crew chemistry suffers.",
                    "−2 Composure, −1 Tolerance")
            },
            {
                RecruitmentTraitId.TeamPlayer,
                new(RecruitmentTraitId.TeamPlayer, "Team Player",
                    RecruitmentTraitKind.Positive,
                    "Naturally supports partners and shared work.",
                    "+2 Affinity, +1 Empathy")
            },
            {
                RecruitmentTraitId.LoneWolf,
                new(RecruitmentTraitId.LoneWolf, "Lone Wolf",
                    RecruitmentTraitKind.Mixed,
                    "Independent and capable alone; cold in groups.",
                    "+1 Determination, −2 Affinity")
            },
            {
                RecruitmentTraitId.HeavySleeper,
                new(RecruitmentTraitId.HeavySleeper, "Heavy Sleeper",
                    RecruitmentTraitKind.Mixed,
                    "Rest hits hard — slow to wake, deep recovery when asleep.",
                    "+1 Recovery, −1 Agility")
            },
            {
                RecruitmentTraitId.InjuryProne,
                new(RecruitmentTraitId.InjuryProne, "Injury Prone",
                    RecruitmentTraitKind.Negative,
                    "Takes hits poorly; overheat and accidents linger.",
                    "−2 Toughness, −1 Balance")
            },
            {
                RecruitmentTraitId.Methodical,
                new(RecruitmentTraitId.Methodical, "Methodical",
                    RecruitmentTraitKind.Positive,
                    "Slow, careful, precise — hates shortcuts.",
                    "+2 Calibration, +1 Safety Protocol, −1 Work Rate")
            },
            {
                RecruitmentTraitId.Reckless,
                new(RecruitmentTraitId.Reckless, "Reckless",
                    RecruitmentTraitKind.Mixed,
                    "Pushes hard past safe limits; results or wreckage.",
                    "+2 Determination, +1 Bravery, −2 Safety Protocol")
            },
            {
                RecruitmentTraitId.NaturalLeader,
                new(RecruitmentTraitId.NaturalLeader, "Natural Leader",
                    RecruitmentTraitKind.Positive,
                    "Crew listens when this person speaks.",
                    "+2 Leadership, +1 Bravery")
            },
        };

        public static RecruitmentTraitDef Get(RecruitmentTraitId id) =>
            ById.TryGetValue(id, out var d) ? d : default;

        public static string DisplayName(RecruitmentTraitId id) => Get(id).Name ?? id.ToString();

        /// <summary>Apply modest hire-time stat deltas. Call once when materializing a WorkerRuntime.</summary>
        public static void ApplyToStats(WorkerStats stats, IReadOnlyList<RecruitmentTraitId> traits)
        {
            if (stats == null || traits == null) return;
            for (int i = 0; i < traits.Count; i++)
                ApplyOne(stats, traits[i]);
            stats.ClampAll();
        }

        static void ApplyOne(WorkerStats s, RecruitmentTraitId id)
        {
            switch (id)
            {
                case RecruitmentTraitId.CalmUnderPressure:
                    Bump(s, WorkerStatId.Composure, 2);
                    Bump(s, WorkerStatId.Focus, 1);
                    break;
                case RecruitmentTraitId.FastLearner:
                    Bump(s, WorkerStatId.Calibration, 1);
                    Bump(s, WorkerStatId.WorkRate, 1);
                    break;
                case RecruitmentTraitId.VeteranMiner:
                    Bump(s, WorkerStatId.Lithology, 2);
                    Bump(s, WorkerStatId.SafetyProtocol, 1);
                    Bump(s, WorkerStatId.Toughness, 1);
                    break;
                case RecruitmentTraitId.PoorRecovery:
                    Bump(s, WorkerStatId.Recovery, -2);
                    Bump(s, WorkerStatId.Stamina, -1);
                    break;
                case RecruitmentTraitId.ShortTemper:
                    Bump(s, WorkerStatId.Composure, -2);
                    Bump(s, WorkerStatId.Tolerance, -1);
                    break;
                case RecruitmentTraitId.TeamPlayer:
                    Bump(s, WorkerStatId.Affinity, 2);
                    Bump(s, WorkerStatId.Empathy, 1);
                    break;
                case RecruitmentTraitId.LoneWolf:
                    Bump(s, WorkerStatId.Determination, 1);
                    Bump(s, WorkerStatId.Affinity, -2);
                    break;
                case RecruitmentTraitId.HeavySleeper:
                    Bump(s, WorkerStatId.Recovery, 1);
                    Bump(s, WorkerStatId.Agility, -1);
                    break;
                case RecruitmentTraitId.InjuryProne:
                    Bump(s, WorkerStatId.Toughness, -2);
                    Bump(s, WorkerStatId.Balance, -1);
                    break;
                case RecruitmentTraitId.Methodical:
                    Bump(s, WorkerStatId.Calibration, 2);
                    Bump(s, WorkerStatId.SafetyProtocol, 1);
                    Bump(s, WorkerStatId.WorkRate, -1);
                    break;
                case RecruitmentTraitId.Reckless:
                    Bump(s, WorkerStatId.Determination, 2);
                    Bump(s, WorkerStatId.Bravery, 1);
                    Bump(s, WorkerStatId.SafetyProtocol, -2);
                    break;
                case RecruitmentTraitId.NaturalLeader:
                    Bump(s, WorkerStatId.Leadership, 2);
                    Bump(s, WorkerStatId.Bravery, 1);
                    break;
            }
        }

        static void Bump(WorkerStats s, WorkerStatId id, int delta) =>
            s.Set(id, s.Get(id) + delta);
    }
}
