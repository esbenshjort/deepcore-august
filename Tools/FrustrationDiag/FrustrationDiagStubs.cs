#if SOCIAL_AURA_DIAG
namespace DeepCore.FreeMovement
{
    /// <summary>Offline stub — NicknameEvidenceStore is a no-op for FrustrationDiag.</summary>
    public sealed class NicknameEvidenceStore
    {
        public static NicknameEvidenceStore Instance { get; } = new NicknameEvidenceStore();
        public void ObserveEvent(WorkerStateEvent e) { }
        public void ObserveMajorSocialMemory(SocialMemoryEntry entry) { }
    }

    /// <summary>Forward decl so NicknameEvidence stub compiles without SocialMemory.cs.</summary>
    public sealed class SocialMemoryEntry { }

    public static class DigHoodLog
    {
        public static void Push(string line) { }
    }
}
#endif
