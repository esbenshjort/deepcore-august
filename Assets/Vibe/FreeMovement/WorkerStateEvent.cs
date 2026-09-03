using System;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// V1.2B: semantic person-state event types.
    /// Prefer shared meanings (WorkBlocked) over per-job enums.
    /// </summary>
    public enum WorkerStateEventType : byte
    {
        ProgressSuccess = 0,
        MajorSuccess = 1,
        WorkBlocked = 2,
        RepeatedFailure = 3,
        EquipmentProblem = 4,
        EquipmentRecovered = 5,
        Discovery = 6,
        InvestigationFailure = 7,
        Injury = 8,
        PhysicalExhaustion = 9,
    }

    /// <summary>
    /// Plain save-ready person-targeted state event.
    /// WorkerId is frozen at creation — never reinterpreted as current operator.
    /// No MonoBehaviour / Transform / host object refs.
    /// </summary>
    [Serializable]
    public sealed class WorkerStateEvent
    {
        public int WorkerId;
        public WorkerStateEventType EventType;
        /// <summary>Semantic intensity (typical 0.5–20 for work reactions).</summary>
        public float Magnitude;
        public float GameHours;
        public string Source;
        /// <summary>0 = none.</summary>
        public int RelatedWorkerId;
        public JobType JobType;
        public string ProviderId;

        public static WorkerStateEvent Create(
            int workerId,
            WorkerStateEventType type,
            float magnitude,
            string source,
            JobType job = JobType.Unassigned,
            string providerId = null,
            int relatedWorkerId = 0)
        {
            return new WorkerStateEvent
            {
                WorkerId = workerId,
                EventType = type,
                Magnitude = magnitude,
                GameHours = WorkerStateClock.GameHours,
                Source = source ?? "",
                RelatedWorkerId = relatedWorkerId,
                JobType = job,
                ProviderId = providerId ?? "",
            };
        }
    }

    /// <summary>History row: event + applied meter deltas.</summary>
    [Serializable]
    public sealed class WorkerStateEventRecord
    {
        public WorkerStateEvent Event;
        public float DeltaFrustration;
        public float DeltaMorale;
        public float DeltaMentalFatigue;
        public float DeltaFocusState;
    }

    /// <summary>Runner stamps absolute game hours so emitters stay host-agnostic.</summary>
    public static class WorkerStateClock
    {
        public static float GameHours { get; set; }
    }
}
