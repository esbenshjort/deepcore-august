using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Person identity + permanent attribute sheet + mutable <see cref="WorkerState"/>.
    /// Do not key gameplay by <see cref="DisplayName"/> — use <see cref="WorkerId"/>.
    /// </summary>
    [Serializable]
    public sealed class WorkerRuntime
    {
        static readonly Dictionary<int, WorkerRuntime> ById = new(16);

        public int WorkerId { get; }
        /// <summary>Prototype / UI label only — never use as a dictionary key or save identity.</summary>
        public string DisplayName { get; set; }
        /// <summary>Single shared sheet for this person across all job assignments.</summary>
        public WorkerStats Stats { get; }

        /// <summary>
        /// Optional biography / recruitment card (age, traits, personality tags).
        /// Null on legacy prototype crew until set.
        /// </summary>
        public WorkerIdentityProfile Identity { get; set; }

        /// <summary>
        /// V1.2A: mutable person state (stamina, fatigue, focus, frustration, morale, injury).
        /// Follows WorkerId — never owned by a job host.
        /// </summary>
        public WorkerState State { get; } = WorkerState.CreateDefault();

        /// <summary>V1.2B: recent person-targeted state events (rolling).</summary>
        public WorkerStateEventHistory EventHistory { get; } = new();

        /// <summary>
        /// Person-owned walk/footing transient — follows this WorkerId across jobs.
        /// Never owned by a job host.
        /// </summary>
            public WorkerLocomotionState Locomotion { get; } = new();

        /// <summary>
        /// Person-owned persistent injuries — follows WorkerId across jobs/equipment.
        /// Always non-null after construction; guarded for Unity deserialization shells.
        /// </summary>
        public WorkerInjuryStore Injuries { get; private set; } = new();

        /// <summary>
        /// Person-owned camp body needs (toilet urgency, stomach upset, meal recovery).
        /// </summary>
        public WorkerCampBody CampBody { get; private set; } = new();

        /// <summary>Obsolete name — use <see cref="State"/>.</summary>
        [Obsolete("V1.2A: use WorkerRuntime.State")]
        public WorkerState PersonalConditions => State;

        public WorkerRuntime(int workerId, string displayName, WorkerStats stats = null)
        {
            if (workerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(workerId), "WorkerId must be positive.");
            WorkerId = workerId;
            DisplayName = string.IsNullOrEmpty(displayName) ? $"Worker {workerId}" : displayName;
            Stats = stats ?? WorkerStats.CreateBaseline();
            Stats.ClampAll();
            if (Injuries == null) Injuries = new WorkerInjuryStore();
            if (CampBody == null) CampBody = new WorkerCampBody();
            ById[workerId] = this;
        }

        /// <summary>Lookup live person by WorkerId (injury / accident wiring).</summary>
        public static WorkerRuntime Find(int workerId) =>
            workerId > 0 && ById.TryGetValue(workerId, out var wr) ? wr : null;

        /// <summary>Identity hash of the Stats object — DEV proof of shared reference.</summary>
        public string StatsRefLabel =>
            $"#{RuntimeHelpers.GetHashCode(Stats):X8}";

        /// <summary>Identity hash of State — DEV proof the same bag follows the person.</summary>
        public string StateRefLabel =>
            $"#{RuntimeHelpers.GetHashCode(State):X8}";

        /// <summary>False after lethal social outcome. Identity / memory / relations persist.</summary>
        public bool IsAlive => State == null || State.IsAlive;

        /// <summary>Physical stamina max used by Excavator / sleep recovery: 50 + Body Stamina×5.</summary>
        public float PhysicalStaminaMax
        {
            get
            {
                int stam = Stats != null ? Stats.Get(WorkerStatId.Stamina) : WorkerStats.Baseline;
                return 50f + stam * 5f;
            }
        }

        public override string ToString() =>
            $"{DisplayName} (id {WorkerId})";
    }
}
