#if SOCIAL_AURA_DIAG
namespace DeepCore.FreeMovement
{
    public sealed class WorkerStateEventHistory
    {
        public System.Collections.Generic.List<object> Items { get; } = new();
    }

    public sealed class FineTerrainWorld
    {
        public float CellSize => 0.1f;
        public TunnelNavGrid Navigation => null;
        public UnityEngine.Vector2Int WorldToCell(UnityEngine.Vector2 world) =>
            new UnityEngine.Vector2Int(0, 0);
    }

    public sealed class TunnelNavGrid
    {
        public int GetClearance(int x, int y) => 8;
    }

    public static class LoosePile
    {
        public static bool IsOnLoose(UnityEngine.Vector2 p, float r = 0.12f) => false;
    }

    public static class WorkerStateEventHub
    {
        public static object Service;
        public static object Emit(WorkerStateEvent e) => null;
        public static object EmitForced(WorkerStateEvent e) => null;
    }

    public static class DigHoodLog
    {
        public static void Push(string line) { }
    }

    public sealed class WorkerCampBody
    {
        public float ToiletNeed01;
        public float MealSleepRecoveryMul = 1f;
        public bool ToiletTripActive;
        public bool HasStomachUpset => false;
        public void ClearMealTonight() { }
        public void ClearStomach() { }
    }

    public static class WorkerJobDemand
    {
        public static float StaminaRatio(WorkerRuntime wr)
        {
            if (wr?.State == null) return 1f;
            float max = UnityEngine.Mathf.Max(1f, wr.PhysicalStaminaMax);
            float stam = wr.State.PhysicalStamina;
            if (!wr.State.StaminaPrimed && stam <= 0f) return 1f;
            return UnityEngine.Mathf.Clamp01(stam / max);
        }
    }

    public sealed class NicknameEvidenceStore
    {
        public static NicknameEvidenceStore Instance { get; } = new();
        public void ObserveMajorSocialMemory(SocialMemoryEntry e) { }
    }

    public enum SocialSpeechValence : byte
    {
        None = 0,
        Positive = 1,
        Neutral = 2,
        Negative = 3,
        Severe = 4,
    }

    // Minimal conflict types for ManagerComm intervene evaluation
    public enum SocialArgumentPhase : byte
    {
        None = 0,
        Opening = 1,
        Exchange = 2,
        Peak = 3,
        Resolving = 4,
        Resolved = 5,
    }

    public sealed class SocialArgumentSession
    {
        public int IdA;
        public int IdB;
        public SocialArgumentPhase Phase = SocialArgumentPhase.None;
        public bool FightOccurred;
        public bool IsActive =>
            Phase != SocialArgumentPhase.None && Phase != SocialArgumentPhase.Resolved;
    }
}
#endif
