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

        [MenuItem("DeepCore/Diagnostics/Run Relationship Trajectory Audit")]
        public static void RunTrajectory()
        {
            string path = RelationshipTrajectoryAudit.Run();
            UnityEngine.Debug.Log($"[TRAJECTORY] Report: {path}");
        }

        [MenuItem("DeepCore/Diagnostics/Run Camp Social V1 Audit")]
        public static void RunCampSocial()
        {
            string path = CampSocialV1Audit.Run();
            UnityEngine.Debug.Log($"[CAMP SOCIAL V1] Report: {path}");
        }

        [MenuItem("DeepCore/Diagnostics/Run Social Intent Audit")]
        public static void RunSocialIntent()
        {
            string path = SocialIntentAudit.Run();
            UnityEngine.Debug.Log($"[SOCIAL INTENT] Report: {path}");
        }

        [MenuItem("DeepCore/Diagnostics/Run Nickname Evidence Audit")]
        public static void RunNicknameEvidence()
        {
            string path = NicknameEvidenceAudit.Run();
            UnityEngine.Debug.Log($"[NICKNAME EVIDENCE] Report: {path}");
        }

        [MenuItem("DeepCore/Diagnostics/Run Frustration Tuning Audit")]
        public static void RunFrustrationTuning()
        {
            string path = FrustrationTuningAudit.Run();
            UnityEngine.Debug.Log($"[FRUSTRATION TUNING] Report: {path}");
        }

        [MenuItem("DeepCore/Diagnostics/Run Social Conflict Audit")]
        public static void RunSocialConflict()
        {
            string path = SocialConflictAudit.Run();
            UnityEngine.Debug.Log($"[SOCIAL CONFLICT] Report: {path}");
        }

        [MenuItem("DeepCore/Diagnostics/Run Social Conflict Death Audit")]
        public static void RunSocialConflictDeath()
        {
            string path = SocialConflictDeathAudit.Run();
            UnityEngine.Debug.Log($"[SOCIAL CONFLICT DEATH] Report: {path}");
        }

        [MenuItem("DeepCore/Diagnostics/Run Mission 01 Prospecting Audit")]
        public static void RunMission01()
        {
            string path = Mission01Audit.Run();
            UnityEngine.Debug.Log($"[MISSION 01] Report: {path}");
        }

        [MenuItem("DeepCore/Diagnostics/Run Recruitment V1 Audit")]
        public static void RunRecruitmentV1() => RecruitmentV1Audit.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run Social Dialogue Visual Audit")]
        public static void RunSocialDialogueVisual() => SocialDialogueVisualAudit.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run Dialogue Bible V2.1 Audit")]
        public static void RunDialogueBibleV21() => SocialDialogueBibleV21Audit.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run Excavator Tile-Paint V1 Audit")]
        public static void RunExcavatorTilePaintV1() => ExcavatorTilePaintRoutingV1Audit.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run Early Crew Pressure Audit")]
        public static void RunEarlyCrewPressure() => EarlyCrewPressureAudit.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run System Integration Cleanup Audit")]
        public static void RunSystemIntegrationCleanup() =>
            SystemIntegrationCleanupAudit.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run Persistent Worker Body V1 Audit")]
        public static void RunPersistentWorkerBodyV1() => PersistentWorkerBodyV1Audit.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run Injury Response V1 Audit")]
        public static void RunInjuryResponseV1() => InjuryResponseV1Audit.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run Worker Physics V1 Audit")]
        public static void RunWorkerPhysicsV1() => WorkerPhysicsV1Audit.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run Worker Injury Physics Audit")]
        public static void RunWorkerInjuryPhysics() => WorkerInjuryPhysicsAudit.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run Steward Camp Life Audit")]
        public static void RunStewardCampLife() => StewardCampLifeAudit.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run Crew Cycle Audit")]
        public static void RunCrewCycle() => CrewCycleAudit.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run Manager Comm Audit")]
        public static void RunManagerComm() => ManagerCommAudit.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run Prospector Workstation Audit")]
        public static void RunProspectorWorkstation() => ProspectorWorkstationAudit.RunFromEditor();

        [MenuItem("DeepCore/Diagnostics/Run Debris Collapse V1 Audit")]
        public static void RunDebrisCollapseV1() => DebrisCollapseV1Audit.RunFromEditor();
    }
}
#endif
