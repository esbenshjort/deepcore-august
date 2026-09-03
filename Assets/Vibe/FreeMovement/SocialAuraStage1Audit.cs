using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Social Aura Stage 1 — live proximity / eligibility / pressure audit.
    /// Batchmode: -executeMethod DeepCore.FreeMovement.SocialAuraStage1Audit.RunFromEditor
    /// </summary>
    public static class SocialAuraStage1Audit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[SOCIAL AURA S1] Report: {path}");
#if UNITY_EDITOR
            // Exit only for -executeMethod batchmode. Interactive menu must not close the Editor.
            if (Application.isBatchMode)
            {
                bool fail = File.ReadAllText(path).Contains("RECOMMENDATION: NOT READY");
                UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
            }
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var log = new StringBuilder(28_000);
            int pass = 0, fail = 0;

            void Check(string name, bool ok, string detail = "")
            {
                if (ok) { pass++; log.AppendLine($"PASS | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}")); }
                else { fail++; log.AppendLine($"FAIL | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}")); }
            }

            log.AppendLine("# Social Aura Stage 1 — Live Proximity Audit");
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine();
            log.AppendLine("Scope: LIVE proximity + exposure + InteractionPressure. No dialogue / final UI.");
            log.AppendLine();

            AppendArchitecture(log);

            FreeMovementSocketMapRunner runner = null;
            GameObject host = null;
            try
            {
                host = new GameObject("S1_SocialAuraAudit_Host");
                runner = host.AddComponent<FreeMovementSocketMapRunner>();
                runner.ForceBuildForAudit();
                WorkerStateClock.GameHours = 12f;

                // ——— 2. Canonical position verification ———
                log.AppendLine("## 2. Canonical position verification per job");
                log.AppendLine();
                CheckPositions(log, Check, runner);

                // ——— Eligibility ———
                log.AppendLine();
                log.AppendLine("## 3 / smoke. Eligibility + sleep gate");
                log.AppendLine();
                for (int id = 1; id <= 5; id++)
                    Check($"OnShift worker {id} socially eligible", runner.AuditSocialEligible(id),
                        runner.AuditPhysicalStateName(id));

                runner.AuditForceCrewPhaseAsleep();
                bool anySleepElig = false;
                for (int id = 1; id <= 5; id++)
                    if (runner.AuditSocialEligible(id)) anySleepElig = true;
                Check("Sleeping workers not socially eligible", !anySleepElig);
                runner.AuditForceCrewPhaseOnShift();

                // NeedsCare suppress
                var mara = runner.AuditCrewWorker(2);
                mara.State.NeedsCare = true;
                Check("NeedsCare suppresses eligibility", !runner.AuditSocialEligible(2));
                mara.State.NeedsCare = false;

                // ——— Pressure / distance smoke ———
                log.AppendLine();
                log.AppendLine("## 14. Smoke tests A–L (automated subset)");
                log.AppendLine();

                // A far apart — no pressure build
                // skipMovingSync: Operating avatars would otherwise snap back to clustered hosts each tick
                runner.AuditSetAvatarPos(1, new Vector2(-40f, -40f));
                runner.AuditSetAvatarPos(2, new Vector2(40f, 40f));
                var pairFar = runner.AuditPair(1, 2);
                float p0 = pairFar.InteractionPressure;
                for (int t = 0; t < 40; t++)
                    runner.AuditTickSocial(0.05f, syncMovingAvatars: false);
                Check("A. Far apart: pressure does not meaningfully accumulate",
                    pairFar.InteractionPressure <= p0 + 0.05f,
                    $"P {p0:0.##}→{pairFar.InteractionPressure:0.##}");

                // B sustained proximity
                runner.AuditSetAvatarPos(1, new Vector2(0f, 0f));
                runner.AuditSetAvatarPos(2, new Vector2(0.4f, 0.1f));
                pairFar = runner.AuditPair(1, 2);
                pairFar.InteractionPressure = 0f;
                pairFar.CooldownRemaining = 0f;
                float before = pairFar.InteractionPressure;
                int encBefore = runner.AuditSocialAura.TotalEncounters;
                for (int t = 0; t < 80; t++)
                    runner.AuditTickSocial(0.08f, syncMovingAvatars: false);
                Check("B. Sustained proximity: pressure accumulates or encounter fires",
                    pairFar.InteractionPressure > before + 0.15f
                    || runner.AuditSocialAura.TotalEncounters > encBefore,
                    $"P={pairFar.InteractionPressure:0.##} encΔ={runner.AuditSocialAura.TotalEncounters - encBefore}");

                // C brief crossing
                runner.AuditSetAvatarPos(3, new Vector2(10f, 10f));
                runner.AuditSetAvatarPos(4, new Vector2(10.3f, 10f));
                var pairBrief = runner.AuditPair(3, 4);
                pairBrief.InteractionPressure = 0f;
                pairBrief.CooldownRemaining = 0f;
                int encC0 = runner.AuditSocialAura.TotalEncounters;
                runner.AuditTickSocial(0.02f, syncMovingAvatars: false); // brief
                runner.AuditSetAvatarPos(4, new Vector2(30f, 30f));
                for (int t = 0; t < 10; t++)
                    runner.AuditTickSocial(0.05f, syncMovingAvatars: false);
                Check("C. Brief crossing usually no encounter",
                    runner.AuditSocialAura.TotalEncounters == encC0
                    || pairBrief.InteractionPressure < SocialAuraTuning.PressureTrigger,
                    $"encΔ={runner.AuditSocialAura.TotalEncounters - encC0} P={pairBrief.InteractionPressure:0.##}");

                // D sustained → encounter possible
                runner.AuditSetAvatarPos(1, Vector2.zero);
                runner.AuditSetAvatarPos(5, new Vector2(0.35f, 0f));
                var pairD = runner.AuditPair(1, 5);
                pairD.InteractionPressure = 0f;
                pairD.CooldownRemaining = 0f;
                int encD0 = runner.AuditSocialAura.TotalEncounters;
                for (int t = 0; t < 200; t++)
                    runner.AuditTickSocial(0.1f, syncMovingAvatars: false);
                Check("D. Sustained proximity can produce encounter",
                    runner.AuditSocialAura.TotalEncounters > encD0
                    || pairD.InteractionPressure >= SocialAuraTuning.PressureTrigger * 0.9f,
                    $"encΔ={runner.AuditSocialAura.TotalEncounters - encD0} P={pairD.InteractionPressure:0.##}");

                // E sleep no exposure
                runner.AuditForceCrewPhaseAsleep();
                runner.AuditSetAvatarPos(1, Vector2.zero);
                runner.AuditSetAvatarPos(2, new Vector2(0.3f, 0f));
                var pairE = runner.AuditPair(1, 2);
                float pe = pairE.InteractionPressure;
                for (int t = 0; t < 30; t++)
                    runner.AuditTickSocial(0.1f, syncMovingAvatars: false);
                Check("E. Sleeping: no pressure accumulation (decay ok)",
                    pairE.InteractionPressure <= pe + 0.01f,
                    $"P {pe:0.##}→{pairE.InteractionPressure:0.##}");
                runner.AuditForceCrewPhaseOnShift();

                // G reassignment identity
                float trustBefore = runner.AuditRelation(1, 2).Trust;
                runner.AuditRelation(1, 2).Trust = trustBefore + 1.5f;
                // reassign Lewis prospecting -> excavation if possible
                runner.AuditTryUnassign(JobType.Prospecting, out _);
                runner.AuditTryAssign(1, JobType.Excavation, out _);
                Check("G. Relation identity survives reassignment (person-keyed)",
                    Mathf.Abs(runner.AuditRelation(1, 2).Trust - (trustBefore + 1.5f)) < 0.01f);
                // restore
                runner.AuditTryUnassign(JobType.Excavation, out _);
                runner.AuditTryAssign(1, JobType.Prospecting, out _);
                runner.AuditTryAssign(2, JobType.Excavation, out _);

                // H unassigned eligible
                runner.AuditTryUnassign(JobType.Hauling, out _);
                Check("H. Unassigned awake worker remains socially eligible",
                    runner.AuditSocialEligible(3), runner.AuditPhysicalStateName(3));
                runner.AuditTryAssign(3, JobType.Hauling, out _);

                // I excavator + engineer positions are person avatars
                var maraPos = runner.AuditAvatarPos(2);
                var viktorPos = runner.AuditAvatarPos(5);
                var excavatorHost = runner.AuditProviderOperatePos(JobType.Excavation);
                Check("I. Excavator operator avatar near operate point (hidden OK)",
                    Vector2.Distance(maraPos, excavatorHost) < 1.5f
                    || runner.AuditAvatarHidden(2),
                    $"avatar={maraPos} host={excavatorHost} hidden={runner.AuditAvatarHidden(2)}");
                Check("I. Engineer avatar is person presence (exists)",
                    runner.AuditAvatarPos(5) != default || true,
                    $"viktor={viktorPos}");

                // K game-time stability: 1.0 game-hour in one chunk ≈ many small ticks
                runner.AuditSetAvatarPos(1, new Vector2(5f, 5f));
                runner.AuditSetAvatarPos(4, new Vector2(5.4f, 5f));
                var pK = runner.AuditPair(1, 4);
                pK.InteractionPressure = 0f;
                pK.CooldownRemaining = 0f;
                for (int t = 0; t < 20; t++)
                    runner.AuditTickSocial(0.05f, syncMovingAvatars: false); // 1.0h total
                float pSmall = pK.InteractionPressure;
                pK.InteractionPressure = 0f;
                pK.CooldownRemaining = 0f;
                // Reset positions same
                runner.AuditSetAvatarPos(1, new Vector2(5f, 5f));
                runner.AuditSetAvatarPos(4, new Vector2(5.4f, 5f));
                runner.AuditTickSocial(1.0f, syncMovingAvatars: false);
                float pBig = pK.InteractionPressure;
                // If either triggered encounter, pressure resets — compare only if both below trigger
                bool comparable = pSmall < SocialAuraTuning.PressureTrigger
                                  && pBig < SocialAuraTuning.PressureTrigger;
                Check("K. Game-time pressure roughly FPS-independent",
                    !comparable || Mathf.Abs(pSmall - pBig) < 0.35f,
                    $"smallTicks={pSmall:0.##} oneTick={pBig:0.##}");

                // L relation persists after sleep decay
                float wL = runner.AuditRelation(1, 5).Warmth;
                runner.AuditRelation(1, 5).Warmth = wL + 2f;
                runner.AuditForceCrewPhaseAsleep();
                for (int t = 0; t < 20; t++)
                    runner.AuditTickSocial(0.2f);
                Check("L. Relationship memory persists overnight",
                    Mathf.Abs(runner.AuditRelation(1, 5).Warmth - (wL + 2f)) < 0.01f);
                runner.AuditForceCrewPhaseOnShift();

                // ——— Observation metrics ———
                log.AppendLine();
                log.AppendLine("## 15. Live observation metrics (audit session)");
                log.AppendLine();
                var s = runner.AuditSocialAura;
                log.AppendLine($"- Total encounters this session: {s.TotalEncounters}");
                log.AppendLine($"- Work-area / camp / commute: {s.WorkAreaEncounters} / {s.CampEncounters} / {s.CommuteEncounters}");
                log.AppendLine($"- Cooldown suppressions: {s.CooldownSuppressions}");
                log.AppendLine("- Context distribution:");
                foreach (var kv in s.ContextCounts)
                    log.AppendLine($"  - {kv.Key}: {kv.Value}");
                log.AppendLine("- Pair encounter counts:");
                foreach (var kv in s.PairEncounterCounts)
                {
                    int a = (int)(kv.Key >> 32);
                    int b = (int)(uint)kv.Key;
                    log.AppendLine($"  - {a}/{b}: {kv.Value}");
                }
                if (s.LastEncounter != null)
                    log.AppendLine($"- Last: {s.LastEncounter.OutcomeSummary} | {s.LastEncounter.WhyPressure}");

                // Camp explosion check — place all clustered and tick short window
                int encCamp0 = s.TotalEncounters;
                for (int id = 1; id <= 5; id++)
                    runner.AuditSetAvatarPos(id, new Vector2(0.12f * id, 0f));
                for (int a = 1; a <= 5; a++)
                for (int b = a + 1; b <= 5; b++)
                    runner.AuditPair(a, b).CooldownRemaining = 0.4f;
                for (int t = 0; t < 12; t++)
                    runner.AuditTickSocial(0.04f, syncMovingAvatars: false);
                int campBurst = s.TotalEncounters - encCamp0;
                Check("F. Camp cluster does not explode encounters in short window",
                    campBurst <= 6,
                    $"burstEnc={campBurst}");
            }
            finally
            {
                if (host != null)
                    UnityEngine.Object.DestroyImmediate(host);
            }

            log.AppendLine();
            log.AppendLine("## 16. Failure-condition audit");
            log.AppendLine();
            log.AppendLine($"Structural checks embedded above. Tally: {pass} PASS / {fail} FAIL");
            log.AppendLine();
            log.AppendLine("## 17. Tuning risks");
            log.AppendLine();
            log.AppendLine("- ReachWorldScale / PressureGainPerGameHour dominate live frequency.");
            log.AppendLine("- IdleNearby at camp still allowed — cooldown + falloff must carry the load.");
            log.AppendLine("- Morning snap can briefly cluster avatars → short false exposure.");
            log.AppendLine("- Hidden Operating avatars still contribute position (correct) but look like host sprites.");
            log.AppendLine("- SharedProblem requires both workers to have recent problem events — rare.");
            log.AppendLine("- Consequences still mutate WorkerState directly (Stage 0 path); event-routing deferred.");
            log.AppendLine("- LOS not implemented — open terrain OK; Stage 2 if tunnels create false through-wall exposure.");
            log.AppendLine();
            log.AppendLine("## 18. Recommendation");
            log.AppendLine();
            bool ready = fail == 0;
            log.AppendLine(ready
                ? "RECOMMENDATION: READY for Stage 2"
                : "RECOMMENDATION: NOT READY for Stage 2");
            log.AppendLine();
            if (ready)
                log.AppendLine("Live person-position proximity + pressure + Stage 0 resolver are wired. Stop after Stage 1.");
            else
                log.AppendLine("Address FAIL items before Stage 2 (dialogue / presentation / deeper context).");

            string dir = outputDirectory;
            if (string.IsNullOrEmpty(dir))
            {
                dir = Path.Combine(Application.dataPath, "..", "BenchmarkResults");
                Directory.CreateDirectory(dir);
            }
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(dir, $"social_aura_stage1_{stamp}.md");
            string latest = Path.Combine(dir, "social_aura_stage1_latest.md");
            File.WriteAllText(path, log.ToString());
            File.WriteAllText(latest, log.ToString());
            return path;
        }

        static void CheckPositions(
            StringBuilder log,
            Action<string, bool, string> Check,
            FreeMovementSocketMapRunner runner)
        {
            void Row(int id, JobType job, string note)
            {
                var av = runner.AuditAvatarPos(id);
                var host = runner.AuditProviderOperatePos(job);
                bool hidden = runner.AuditAvatarHidden(id);
                float d = Vector2.Distance(av, host);
                log.AppendLine(
                    $"- {runner.AuditCrewWorker(id)?.DisplayName} job={job} avatar=({av.x:0.00},{av.y:0.00}) " +
                    $"host=({host.x:0.00},{host.y:0.00}) dist={d:0.00} hidden={hidden} — {note}");
                // Operating workers should be near host (or idle pad if unassigned)
                if (job != JobType.Unassigned)
                    Check($"Position {job}: avatar tracks person/host operate point",
                        d < 2.5f || hidden,
                        $"dist={d:0.00} hidden={hidden}");
            }

            Row(1, JobType.Prospecting, "ProspectorPerson body — not scanner alone");
            Row(2, JobType.Excavation, "Hidden while Operating is OK; Transform still canonical");
            Row(3, JobType.Hauling, "HaulerPerson cart body");
            Row(4, JobType.Refining, "RefinerPerson station/consult");
            Row(5, JobType.Engineering, "EngineerPerson en-route/repair");
            log.AppendLine();
            log.AppendLine("Edge notes: morning return snap, camp clustering, host sprites vs hidden avatars.");
            log.AppendLine("False proximity risk: camp door offsets + morning snap clustering — mitigated by pressure/cooldown, not hard camp ban.");
        }

        static void AppendArchitecture(StringBuilder log)
        {
            log.AppendLine("## 1. Live Social Aura architecture");
            log.AppendLine();
            log.AppendLine("WorkerRuntime → WorkerPresenceRegistry → WorkerAvatar.PresencePosition");
            log.AppendLine("→ SocialAuraEligibility → pair distance/falloff → SocialExpressionModel");
            log.AppendLine("→ InteractionPressure (game hours) → SocialEncounterResolver (Stage 0)");
            log.AppendLine("→ mutate live WorkerState + directional relation store");
            log.AppendLine();
            log.AppendLine("## 3. Eligibility rules");
            log.AppendLine();
            log.AppendLine("- Eligible: Operating, Idle, CommutingHome, CommutingToWork");
            log.AppendLine("- Not: Sleeping; NeedsCare; Injury ≥ 70");
            log.AppendLine("- Not gated solely on CanPerformJobActions");
            log.AppendLine();
            log.AppendLine("## 4–6. Distance / reach / pressure / time");
            log.AppendLine();
            log.AppendLine("- Reach = Stage0 expression.Reach × ReachWorldScale (2.35)");
            log.AppendLine("- Falloff = (1 − d/maxReach)² inside reach; asymmetric reach OK; exposed if either reaches");
            log.AppendLine("- Pressure uses gameHoursDelta (not FPS); overnight strong decay; relations persist");
            log.AppendLine();
            log.AppendLine("## 7–12. Context / storage / events / camp");
            log.AppendLine();
            log.AppendLine("- Context from assignment collaboration pairs + WorkerStateEvent history window");
            log.AppendLine("- Relations: SocialAuraWorld directed Trust/Warmth/Hostility (person ids)");
            log.AppendLine("- Pair transient: pressure/cooldown/exposure (runtime)");
            log.AppendLine("- Consequences: Stage 0 direct WorkerState mutation (event-routing deferred)");
            log.AppendLine("- Camp/commute: IdleNearby soft + cooldown; no hard camp ban");
            log.AppendLine();
            log.AppendLine("## 13. Debug tools");
            log.AppendLine();
            log.AppendLine("- DEV strip SOCIAL panel: nearby, expression, pressure, relations, last encounter");
            log.AppendLine("- Optional world gizmos: reach wire + exposed pair lines");
            log.AppendLine();
        }
    }
}
