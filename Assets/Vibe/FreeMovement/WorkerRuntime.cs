using System;
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
        public int WorkerId { get; }
        /// <summary>Prototype / UI label only — never use as a dictionary key or save identity.</summary>
        public string DisplayName { get; set; }
        /// <summary>Single shared sheet for this person across all job assignments.</summary>
        public WorkerStats Stats { get; }

        /// <summary>
        /// V1.2A: mutable person state (stamina, fatigue, focus, frustration, morale, injury).
        /// Follows WorkerId — never owned by a job host.
        /// </summary>
        public WorkerState State { get; } = WorkerState.CreateDefault();

        /// <summary>V1.2B: recent person-targeted state events (rolling).</summary>
        public WorkerStateEventHistory EventHistory { get; } = new();

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
        }

        /// <summary>Identity hash of the Stats object — DEV proof of shared reference.</summary>
        public string StatsRefLabel =>
            $"#{RuntimeHelpers.GetHashCode(Stats):X8}";

        /// <summary>Identity hash of State — DEV proof the same bag follows the person.</summary>
        public string StateRefLabel =>
            $"#{RuntimeHelpers.GetHashCode(State):X8}";

        /// <summary>Physical stamina max used by Excavator / sleep recovery: 50 + Body Stamina×5.</summary>
        public float PhysicalStaminaMax => 50f + Stats.Get(WorkerStatId.Stamina) * 5f;

        public override string ToString() =>
            $"{DisplayName} (id {WorkerId})";
    }
}
