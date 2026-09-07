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

    public sealed class NicknameEvidenceStore
    {
        public static NicknameEvidenceStore Instance { get; } = new NicknameEvidenceStore();
        public void ObserveMajorSocialMemory(SocialMemoryEntry entry) { }
        public void ObserveEvent(WorkerStateEvent e) { }
    }

    public static class DigHoodLog
    {
        public static void Push(string line) { }
    }

    /// <summary>Presentation enum only — visuals not compiled into this diag.</summary>
    public enum SocialSpeechValence : byte
    {
        None = 0,
        Positive = 1,
        Neutral = 2,
        Negative = 3,
        Severe = 4,
    }

    public enum RecruitmentTraitId : byte
    {
        None = 0,
    }

    /// <summary>Diag stub — presentation helpers unused by EarlyCrewPressureAudit.</summary>
    public static class SocialSpeechVisuals
    {
        public static SocialSpeechValence FromEncounter(SocialEncounterLog log) =>
            SocialSpeechValence.Neutral;
        public static SocialSpeechValence FromConflict(SocialConflictEvent ev) =>
            SocialSpeechValence.Neutral;
        public static SocialSpeechValence FromOutcomeSummary(string summary) =>
            SocialSpeechValence.Neutral;
        public static SocialSpeechValence FromAuraLine(SocialEncounterLog log, string role) =>
            SocialSpeechValence.Neutral;
    }
}
#endif
