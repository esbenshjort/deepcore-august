#if SOCIAL_AURA_DIAG
namespace DeepCore.FreeMovement
{
    /// <summary>Offline diag stub — mirrors SocialAuraLive eligibility without live runner deps.</summary>
    public enum SocialPresenceKind : byte
    {
        Operating = 0,
        Idle = 1,
        CommutingHome = 2,
        Sleeping = 3,
        CommutingToWork = 4,
    }

    public static class SocialAuraEligibility
    {
        public const float InjurySuppressThreshold = 70f;

        public static bool IsEligible(WorkerRuntime wr, SocialPresenceKind presence)
        {
            if (wr?.State == null) return false;
            if (presence == SocialPresenceKind.Sleeping) return false;
            if (wr.State.NeedsCare) return false;
            if (wr.State.Injury >= InjurySuppressThreshold) return false;
            return presence == SocialPresenceKind.Operating
                   || presence == SocialPresenceKind.Idle
                   || presence == SocialPresenceKind.CommutingHome
                   || presence == SocialPresenceKind.CommutingToWork;
        }
    }

    public sealed class WorkerStateEventHistory
    {
        public System.Collections.Generic.List<object> Items { get; } = new();
    }

    /// <summary>Diag stub — real clock lives in WorkerStateEvent.cs (Unity).</summary>
    public static class WorkerStateClock
    {
        public static float GameHours { get; set; }
    }

    /// <summary>Diag stub — nickname evidence observation is a no-op offline.</summary>
    public sealed class NicknameEvidenceStore
    {
        public static NicknameEvidenceStore Instance { get; } = new NicknameEvidenceStore();
        public void ObserveMajorSocialMemory(SocialMemoryEntry entry) { }
        public void ObserveEvent(WorkerStateEvent e) { }
    }

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

    public sealed class WorkerStateEvent
    {
        public int WorkerId;
        public WorkerStateEventType EventType;
        public float Magnitude;
        public float GameHours;
        public string Source;
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

    public sealed class WorkerStateEventRecord
    {
        public WorkerStateEvent Event;
        public float DeltaFrustration;
        public float DeltaMorale;
        public float DeltaMentalFatigue;
        public float DeltaFocusState;
    }

    public static class WorkerStateEventHub
    {
        public static WorkerStateEventService Service;
        public static WorkerStateEventRecord Emit(WorkerStateEvent e) =>
            Service != null ? Service.Emit(e) : null;
    }

    /// <summary>Minimal offline service stub (Injury emits are no-ops unless bound).</summary>
    public sealed class WorkerStateEventService
    {
        System.Func<int, WorkerRuntime> _resolve;
        public void Bind(System.Func<int, WorkerRuntime> resolveWorker) => _resolve = resolveWorker;
        public WorkerStateEventRecord Emit(WorkerStateEvent e)
        {
            if (e == null) return null;
            var wr = _resolve?.Invoke(e.WorkerId);
            if (wr?.State == null) return null;
            if (e.EventType == WorkerStateEventType.Injury)
                wr.State.AddInjury(e.Magnitude);
            return new WorkerStateEventRecord { Event = e };
        }
        public WorkerStateEventRecord EmitForced(WorkerStateEvent e) => Emit(e);
        public void ClearGate() { }
    }
}
#endif
