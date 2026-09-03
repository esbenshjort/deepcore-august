#if UNITY_EDITOR
using UnityEditor;

namespace DeepCore.FreeMovement
{
    public static class WorkerV11LockAuditMenu
    {
        [MenuItem("DeepCore/Diagnostics/Run V1.1 Lock Audit")]
        public static void Run() => WorkerV11LockAudit.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run V1.2A State Verify")]
        public static void RunV12A() => WorkerV12AStateVerify.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run V1.2B Event Audit")]
        public static void RunV12B() => WorkerV12BEventAudit.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run V1.2C Demand Audit")]
        public static void RunV12C() => WorkerV12CDemandAudit.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run Social Aura Stage 0 Sim")]
        public static void RunSocialAuraS0() => SocialAuraStage0Sim.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run Social Aura Stage 1 Audit")]
        public static void RunSocialAuraS1() => SocialAuraStage1Audit.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run Social Aura Stage 2 Audit")]
        public static void RunSocialAuraS2() => SocialAuraStage2Audit.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run Social Aura Stage 2 Logic Audit")]
        public static void RunSocialAuraS2Logic()
        {
            string path = SocialAuraStage2LogicAudit.Run();
            UnityEngine.Debug.Log($"[SOCIAL AURA S2 LOGIC] Report: {path}");
        }

        [MenuItem("DeepCore/Diagnostics/Run Frustration Loop Diagnostic")]
        public static void RunFrustrationLoop()
        {
            string path = FrustrationLoopDiagnostic.Run();
            UnityEngine.Debug.Log($"[FRUSTRATION LOOP] Report: {path}");
        }

        [MenuItem("DeepCore/Diagnostics/Run Prospector Dry-Spell Timing Audit")]
        public static void RunDrySpellTiming()
        {
            string path = ProspectorDrySpellTimingAudit.Run();
            UnityEngine.Debug.Log($"[DRY SPELL TIMING] Report: {path}");
        }

        [MenuItem("DeepCore/Diagnostics/Run Social Memory V1 Audit")]
        public static void RunSocialMemoryV1()
        {
            string path = SocialMemoryV1Audit.Run();
            UnityEngine.Debug.Log($"[SOCIAL MEMORY V1] Report: {path}");
        }

        [MenuItem("DeepCore/Diagnostics/Run Relationship V1.1 Audit")]
        public static void RunRelationshipV11()
        {
            string path = RelationshipV11Audit.Run();
            UnityEngine.Debug.Log($"[RELATIONSHIP V1.1] Report: {path}");
        }

        [MenuItem("DeepCore/Diagnostics/Run Relationship-Aware Work V1 Audit")]
        public static void RunCoopWorkV1()
        {
            string path = RelationshipAwareWorkV1Audit.Run();
            UnityEngine.Debug.Log($"[COOP WORK V1] Report: {path}");
        }

        [MenuItem("DeepCore/Diagnostics/Run Relationship Work Playtest Audit")]
        public static void RunCoopPlaytest()
        {
            string path = RelationshipWorkPlaytestAudit.Run();
            UnityEngine.Debug.Log($"[COOP PLAYTEST] Report: {path}");
        }
    }
}
#endif
