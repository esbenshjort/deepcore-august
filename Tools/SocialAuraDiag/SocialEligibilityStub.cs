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
}
#endif
