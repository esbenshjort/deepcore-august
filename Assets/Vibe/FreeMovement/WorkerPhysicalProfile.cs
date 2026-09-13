using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Derived walking capability from existing <see cref="WorkerStats"/> — not new sheet fields.
    /// Distinct physical roles; do not collapse into one Mobility score.
    /// </summary>
    public readonly struct WorkerPhysicalProfile
    {
        /// <summary>
        /// World units/sec for Agility=10, clear tunnel, rested, unloaded.
        /// Tuned for mine visual scale (person-sized sprites) — brisk walk, not a jog.
        /// </summary>
        public const float ReferenceWalkSpeed = 0.58f;

        /// <summary>Hard ceiling for normal walking (before footing slow). No silent run.</summary>
        public const float MaxNormalWalkSpeed = 0.72f;

        /// <summary>Role intent clamp — hosts may bias slightly, never invent sprint.</summary>
        public const float RoleBiasMin = 0.88f;
        public const float RoleBiasMax = 1.08f;

        /// <summary>Base walk speed from Agility (+ light Rhythm cadence).</summary>
        public readonly float MoveSpeed;

        /// <summary>How quickly the walker responds to direction changes (Agility + Finesse).</summary>
        public readonly float AccelerationMul;

        /// <summary>0–1 footing stability from Balance.</summary>
        public readonly float Balance01;

        /// <summary>Resists stumble/slip (Balance + Toughness).</summary>
        public readonly float FootingResist;

        /// <summary>Handles awkward / narrow / uneven ground (SpatialGeometry + Agility).</summary>
        public readonly float TerrainAdapt;

        /// <summary>Carried-load tolerance (HeavyLifting).</summary>
        public readonly float LoadHandling;

        /// <summary>Endurance factor from Stamina (feeds fatigue interaction, not a second Mobility).</summary>
        public readonly float Endurance;

        /// <summary>Recovery from bad footing / interruption (Recovery).</summary>
        public readonly float FootingRecovery;

        /// <summary>Stability under difficult conditions (Focus + Composure sheet stats).</summary>
        public readonly float StressStability;

        public WorkerPhysicalProfile(
            float moveSpeed,
            float accelerationMul,
            float balance01,
            float footingResist,
            float terrainAdapt,
            float loadHandling,
            float endurance,
            float footingRecovery,
            float stressStability)
        {
            MoveSpeed = moveSpeed;
            AccelerationMul = accelerationMul;
            Balance01 = balance01;
            FootingResist = footingResist;
            TerrainAdapt = terrainAdapt;
            LoadHandling = loadHandling;
            Endurance = endurance;
            FootingRecovery = footingRecovery;
            StressStability = stressStability;
        }

        public static WorkerPhysicalProfile From(WorkerStats stats)
        {
            if (stats == null) stats = WorkerStats.CreateBaseline();

            int agi = stats.Get(WorkerStatId.Agility);
            int bal = stats.Get(WorkerStatId.Balance);
            int stam = stats.Get(WorkerStatId.Stamina);
            int rec = stats.Get(WorkerStatId.Recovery);
            int heavy = stats.Get(WorkerStatId.HeavyLifting);
            int spatial = stats.Get(WorkerStatId.SpatialGeometry);
            int finesse = stats.Get(WorkerStatId.Finesse);
            int rhythm = stats.Get(WorkerStatId.Rhythm);
            int tough = stats.Get(WorkerStatId.Toughness);
            int focus = stats.Get(WorkerStatId.Focus);
            int composure = stats.Get(WorkerStatId.Composure);

            // Agility owns pace modestly; Rhythm is a tiny cadence nudge.
            float agiMul = Mathf.Lerp(0.88f, 1.12f, (agi - 1) / 19f);
            float rhythmMul = 1f + (rhythm - 10) * 0.005f;
            float move = ReferenceWalkSpeed * agiMul * rhythmMul;

            float accel = Mathf.Lerp(0.72f, 1.28f, (agi - 1) / 19f)
                          * (1f + (finesse - 10) * 0.012f);

            float balance01 = Mathf.Clamp01((bal - 1) / 19f);
            float footing = Mathf.Clamp(
                0.35f + bal * 0.035f + tough * 0.012f, 0.4f, 1.35f);
            float terrainAdapt = Mathf.Clamp(
                0.45f + spatial * 0.028f + agi * 0.018f, 0.5f, 1.4f);
            float load = Mathf.Clamp(0.55f + heavy * 0.035f, 0.55f, 1.35f);
            float endurance = Mathf.Clamp(0.7f + stam * 0.03f, 0.7f, 1.3f);
            float footingRec = Mathf.Clamp(0.65f + rec * 0.03f, 0.65f, 1.35f);
            float stress = Mathf.Clamp(
                0.55f + focus * 0.02f + composure * 0.022f, 0.55f, 1.35f);

            return new WorkerPhysicalProfile(
                move, accel, balance01, footing, terrainAdapt, load, endurance, footingRec, stress);
        }

        public static WorkerPhysicalProfile From(WorkerRuntime wr) =>
            From(wr?.Stats);
    }
}
