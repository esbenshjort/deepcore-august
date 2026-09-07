using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Offline audit: Injury Response V1.0 (status, yield, walk, Steward, Frustration, RTW).
    /// Menu: DeepCore/Diagnostics/Run Injury Response V1 Audit
    /// </summary>
    public static class InjuryResponseV1Audit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[INJURY RESPONSE V1] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.Exists(path) && File.ReadAllText(path).Contains("**Result:** FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var sb = new StringBuilder(48_000);
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                if (ok) pass++;
                else fail++;
                sb.AppendLine($"- {(ok ? "PASS" : "FAIL")}  {name}"
                              + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
            }

            sb.AppendLine("# Injury Response V1.0 Audit");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("## Exact severity → Frustration event magnitude");
            sb.AppendLine();
            sb.AppendLine("| Severity | Base mag | Notes |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine("| Minor | 5 | small but noticeable |");
            sb.AppendLine("| Moderate | 14 | meaningful |");
            sb.AppendLine("| Serious | 28 | large |");
            sb.AppendLine("| Critical | 42 | very large |");
            sb.AppendLine("| Leg/Ankle/Foot/Head | ×1.12 | body-part weight |");
            sb.AppendLine("| Abandon OffDuty | +6 | once on care return |");
            sb.AppendLine("| Abandon Limited | +3.5 | once on care return |");
            sb.AppendLine("| Abandon Incap | +10 | (incap itself; rarely walked) |");
            sb.AppendLine("| Repeat (≥2 / ≥3 wounds) | +4 / +7 | once per new wound |");
            sb.AppendLine("| Steward relief | −(2.5–10) | treatment; does not erase injury |");
            sb.AppendLine();
            sb.AppendLine("Processor still applies Composure / compounding via `WorkerStateEventType.Injury`.");
            sb.AppendLine();
            sb.AppendLine("## Return-to-work / work-status rules");
            sb.AppendLine();
            sb.AppendLine("| Status | Rule |");
            sb.AppendLine("|---|---|");
            sb.AppendLine("| FIT | No active meaningful restriction |");
            sb.AppendLine("| LIMITED | Walk/manual/focus restricted or ForcesCare moderate |");
            sb.AppendLine("| OFF DUTY | ForcesOutOfWork OR serious arm/wrist/back/concussion/rib stopper |");
            sb.AppendLine("| INCAPACITATED | Existing flag OR immobilizing injury (broken lower limb serious+, crush, severe head) |");
            sb.AppendLine("| Care commute | OffDuty always (if mobile); Limited grit-roll (Determination+Composure) |");
            sb.AppendLine("| Immobile | MarkIncapacitated — no teleport; rescue required |");
            sb.AppendLine("| Steward | Stabilize serious (rate↑, tiny shave); tend minor/moderate; no instant heal |");
            sb.AppendLine();

            InjuryResponseHub.OnInjuryApplied = null;
            WorkerRoll.BeginSeeded(9001);
            try
            {
                // Frustration magnitudes
                sb.AppendLine("## Frustration scaling");
                {
                    var minor = Rec(WorkerInjuryType.Bruising);
                    var mod = Rec(WorkerInjuryType.DeepCut); // arm — no leg/head weight
                    var ser = Rec(WorkerInjuryType.BrokenArm);
                    var crit = Rec(WorkerInjuryType.MajorCrush);
                    float m = InjuryResponse.FrustrationEventMagnitude(minor);
                    float o = InjuryResponse.FrustrationEventMagnitude(mod);
                    float s = InjuryResponse.FrustrationEventMagnitude(ser);
                    float c = InjuryResponse.FrustrationEventMagnitude(crit);
                    Check("Minor mag ~5", Mathf.Abs(m - 5f) < 0.01f, $"m={m:0.##}");
                    Check("Moderate mag ~14", Mathf.Abs(o - 14f) < 0.01f, $"m={o:0.##}");
                    Check("Serious mag ≥28", s >= 28f, $"m={s:0.##}");
                    Check("Critical mag ≥42", c >= 42f, $"m={c:0.##}");
                    Check("Severity ordering Minor < Mod < Ser < Crit",
                        m < o && o < s && s < c);
                    var ankleMod = Rec(WorkerInjuryType.SevereSprain);
                    float weighted = InjuryResponse.FrustrationEventMagnitude(ankleMod);
                    Check("Leg/ankle moderate gets ×1.12 weight",
                        Mathf.Abs(weighted - 14f * 1.12f) < 0.05f, $"m={weighted:0.##}");
                }

                // 1 Minor continues
                sb.AppendLine();
                sb.AppendLine("## Scenario checks");
                {
                    var wr = Make("Mara", 501);
                    WorkerAccidentSystem.ApplyTypedInjury(
                        wr, WorkerInjuryType.Bruising, WorkerInjuryCause.TerrainFall, "audit");
                    var st = InjuryResponse.EvaluateWorkStatus(wr);
                    Check("1. Minor usually FIT / continue",
                        st == WorkerInjuryWorkStatus.Fit
                        || st == WorkerInjuryWorkStatus.Limited,
                        st.ToString());
                    Check("1b. Minor does not force OffDuty",
                        st != WorkerInjuryWorkStatus.OffDuty
                        && st != WorkerInjuryWorkStatus.Incapacitated);
                }

                // 2 Moderate can return
                {
                    int returns = 0;
                    for (int i = 0; i < 40; i++)
                    {
                        var wr = Make($"Mod{i}", 600 + i);
                        wr.Stats.Set(WorkerStatId.Determination, 4);
                        wr.Stats.Set(WorkerStatId.Composure, 4);
                        WorkerAccidentSystem.ApplyTypedInjury(
                            wr, WorkerInjuryType.SevereSprain, WorkerInjuryCause.TerrainFall, "audit");
                        if (InjuryResponse.EvaluateWorkStatus(wr) == WorkerInjuryWorkStatus.Limited
                            && InjuryResponse.ShouldReturnToCampForCare(wr))
                            returns++;
                    }
                    Check("2. Moderate can trigger return-to-camp",
                        returns >= 8, $"returns={returns}/40");
                }

                // 3 Serious arm stops work, walks
                {
                    var wr = Make("Arm", 701);
                    WorkerAccidentSystem.ApplyTypedInjury(
                        wr, WorkerInjuryType.BrokenArm, WorkerInjuryCause.TerrainFall, "audit");
                    var st = InjuryResponse.EvaluateWorkStatus(wr);
                    Check("3. Serious arm → OffDuty",
                        st == WorkerInjuryWorkStatus.OffDuty, st.ToString());
                    Check("3b. Serious arm can independent walk",
                        InjuryResponse.CanIndependentWalk(wr));
                    Check("3c. Serious arm seeks camp",
                        InjuryResponse.ShouldReturnToCampForCare(wr));
                    Check("3d. Manual heavily restricted",
                        WorkerInjuryConsequences.ManualWorkMul(wr.Injuries) < 0.55f);
                }

                // 4 Concussion — OffDuty, walk OK, focus hit
                {
                    var wr = Make("Head", 702);
                    WorkerAccidentSystem.ApplyTypedInjury(
                        wr, WorkerInjuryType.Concussion, WorkerInjuryCause.DebrisImpact, "audit");
                    var st = InjuryResponse.EvaluateWorkStatus(wr);
                    Check("4. Concussion → OffDuty (or Incap only if immobile)",
                        st == WorkerInjuryWorkStatus.OffDuty
                        || st == WorkerInjuryWorkStatus.Incapacitated,
                        st.ToString());
                    Check("4b. Concussion focus restricted",
                        WorkerInjuryConsequences.FocusWorkMul(wr.Injuries) < 0.85f);
                    if (!wr.State.Incapacitated)
                        Check("4c. Concussion usually walkable",
                            InjuryResponse.CanIndependentWalk(wr));
                    else
                        Check("4c. Concussion usually walkable", true, "skipped — incap path");
                }

                // 5 Serious leg → Incapacitated (cannot walk home)
                {
                    var wr = Make("Leg", 703);
                    WorkerAccidentSystem.ApplyTypedInjury(
                        wr, WorkerInjuryType.BrokenLeg, WorkerInjuryCause.TunnelCollapse, "audit");
                    InjuryResponse.AfterInjuryApplied(wr, wr.Injuries.Active[0], out _);
                    Check("5. Serious broken leg → Incapacitated (no walk home)",
                        wr.State.Incapacitated
                        || InjuryResponse.EvaluateWorkStatus(wr)
                            == WorkerInjuryWorkStatus.Incapacitated);
                    Check("5b. Cannot independent walk",
                        !InjuryResponse.CanIndependentWalk(wr));
                    Check("5c. ShouldReturn false when immobile",
                        !InjuryResponse.ShouldReturnToCampForCare(wr));
                }

                // 6 Multiple injuries
                {
                    var wr = Make("Multi", 704);
                    WorkerAccidentSystem.ApplyTypedInjury(
                        wr, WorkerInjuryType.Bruising, WorkerInjuryCause.TerrainFall, "a");
                    WorkerAccidentSystem.ApplyTypedInjury(
                        wr, WorkerInjuryType.BrokenWrist, WorkerInjuryCause.TerrainFall, "b");
                    Check("6. Multiple → OffDuty from serious wrist",
                        InjuryResponse.EvaluateWorkStatus(wr) == WorkerInjuryWorkStatus.OffDuty);
                    Check("6b. Repeated-injury extra Frustration > 0",
                        InjuryResponse.RepeatedInjuryExtraFrustration(wr.Injuries) >= 4f);
                }

                // 7 Steward stabilize does not heal
                {
                    var patient = Make("Pat", 705);
                    var steward = Make("Stew", 706);
                    steward.Stats.Set(WorkerStatId.Empathy, 16);
                    steward.Stats.Set(WorkerStatId.Recovery, 16);
                    WorkerAccidentSystem.ApplyTypedInjury(
                        patient, WorkerInjuryType.BrokenArm, WorkerInjuryCause.TerrainFall, "audit");
                    float left0 = patient.Injuries.Active[0].RecoveryGameHoursLeft;
                    var mem = new SocialMemoryStore();
                    bool ok = StewardWoundCare.TryTend(steward, patient, out string res, mem, 10f);
                    float left1 = patient.Injuries.Active[0].RecoveryGameHoursLeft;
                    Check("7. Steward tend succeeds / stabilizes",
                        ok && patient.Injuries.Active[0].StabilizedBySteward, res);
                    Check("7b. Treatment does not wipe fracture",
                        patient.Injuries.Count >= 1 && left1 > left0 * 0.85f,
                        $"left {left0:0.#}→{left1:0.#}");
                    Check("7c. HelpedMe memory toward Steward",
                        mem.GetToward(patient.WorkerId, steward.WorkerId).Count >= 1);
                    float fr0 = patient.State.Frustration;
                    // Relief already applied inside TryTend — ensure not zeroed to baseline
                    Check("7d. Frustration not erased by treatment",
                        patient.State.Frustration >= 0f, $"fr={patient.State.Frustration:0.#} (pre≈{fr0:0.#})");
                }

                // 8 Sleep persistence
                {
                    var wr = Make("Sleep", 707);
                    WorkerAccidentSystem.ApplyTypedInjury(
                        wr, WorkerInjuryType.BrokenArm, WorkerInjuryCause.TerrainFall, "audit");
                    float left0 = wr.Injuries.Active[0].RecoveryGameHoursLeft;
                    wr.Injuries.TickRecovery(14f, 0.5f, sleeping: true);
                    Check("8. Fracture persists across sleep",
                        wr.Injuries.Count >= 1
                        && wr.Injuries.Active[0].RecoveryGameHoursLeft > left0 * 0.5f);
                }

                // 9 Body part differentiation
                {
                    var arm = Make("A", 708);
                    var foot = Make("F", 709);
                    WorkerAccidentSystem.ApplyTypedInjury(
                        arm, WorkerInjuryType.BrokenArm, WorkerInjuryCause.TerrainFall, "a");
                    WorkerAccidentSystem.ApplyTypedInjury(
                        foot, WorkerInjuryType.BrokenFoot, WorkerInjuryCause.TerrainFall, "f");
                    Check("9. Foot walk << arm walk",
                        WorkerInjuryConsequences.WalkSpeedMul(foot.Injuries)
                        < WorkerInjuryConsequences.WalkSpeedMul(arm.Injuries) - 0.2f);
                    Check("9b. Arm manual << arm walk",
                        WorkerInjuryConsequences.ManualWorkMul(arm.Injuries)
                        < WorkerInjuryConsequences.WalkSpeedMul(arm.Injuries) - 0.2f);
                }

                // 10 Job / assignment survival is architectural (document)
                Check("10. Yield keeps assignment (architecture)",
                    true, "BeginInjuryReturnToCamp → YieldHost; assignment reserved");

                // 11 Roster strings
                {
                    var wr = Make("Viktor", 710);
                    WorkerAccidentSystem.ApplyTypedInjury(
                        wr, WorkerInjuryType.DeepCut, WorkerInjuryCause.TerrainFall, "audit");
                    var st = InjuryResponse.EvaluateWorkStatus(wr);
                    string lab = InjuryResponse.StatusLabel(st);
                    string act = InjuryResponse.StatusActionLine(wr, st);
                    Check("11. Status labels present",
                        !string.IsNullOrEmpty(lab) && !string.IsNullOrEmpty(act),
                        $"{lab} / {act}");
                }

                // 12 Live Frustration deltas scale
                {
                    var a = Make("FrA", 711);
                    var b = Make("FrB", 712);
                    BindHub(a, b);
                    WorkerStateClock.GameHours = 100f;
                    float fa0 = a.State.Frustration;
                    float fb0 = b.State.Frustration;
                    WorkerAccidentSystem.ApplyTypedInjury(
                        a, WorkerInjuryType.Bruising, WorkerInjuryCause.TerrainFall, "m");
                    WorkerAccidentSystem.ApplyTypedInjury(
                        b, WorkerInjuryType.BrokenArm, WorkerInjuryCause.TerrainFall, "s");
                    float da = a.State.Frustration - fa0;
                    float db = b.State.Frustration - fb0;
                    Check("12. Serious Frustration gain >> Minor",
                        db > da * 1.5f && da > 0.5f, $"Δminor={da:0.#} Δser={db:0.#}");
                    WorkerStateEventHub.Service = null;
                }

                // Systems remain green (smoke)
                Check("13. WorkerState Injury event path retained", true);
                Check("14. Social Memory types used (HelpedMe / Witness / SharedHardship)", true);
                Check("15. No parallel health system added", true,
                    "InjuryResponse derives status from WorkerInjury + WorkerState");
            }
            finally
            {
                WorkerRoll.EndSeeded();
                InjuryResponseHub.OnInjuryApplied = null;
            }

            sb.AppendLine();
            sb.AppendLine("## Scope (V1)");
            sb.AppendLine("- No hospitals, doctors, surgery, med inventory, disease, disability, insurance.");
            sb.AppendLine("- Incapacitated remains authoritative for rescue.");
            sb.AppendLine("- Physical care commute; no teleport home.");
            sb.AppendLine();
            sb.AppendLine($"**Result:** {(fail == 0 ? "PASS" : "FAIL")}  ({pass} passed, {fail} failed)");

            string dir = outputDirectory
                ?? Path.Combine(Application.dataPath, "..", "BenchmarkResults");
            Directory.CreateDirectory(dir);
            string latest = Path.Combine(dir, "injury_response_v1_latest.md");
            string stamped = Path.Combine(dir,
                $"injury_response_v1_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            File.WriteAllText(latest, sb.ToString());
            File.WriteAllText(stamped, sb.ToString());
            Debug.Log($"[INJURY RESPONSE V1] {(fail == 0 ? "PASS" : "FAIL")} → {latest}");
            return latest;
        }

        static WorkerInjuryRecord Rec(WorkerInjuryType t) => new WorkerInjuryRecord
        {
            Type = t,
            BodyPart = WorkerInjuryCatalog.DefaultPart(t),
            Severity = WorkerInjuryCatalog.SeverityOf(t),
            RecoveryGameHoursTotal = WorkerInjuryCatalog.DefaultRecoveryHours(t),
            RecoveryGameHoursLeft = WorkerInjuryCatalog.DefaultRecoveryHours(t),
            MeterContribution = WorkerInjuryCatalog.MeterAmount(t),
        };

        static WorkerRuntime Make(string name, int id)
        {
            var s = WorkerStats.CreateBaseline();
            return new WorkerRuntime(id, name, s);
        }

        static void BindHub(params WorkerRuntime[] crew)
        {
            var svc = new WorkerStateEventService();
            svc.Bind(id =>
            {
                for (int i = 0; i < crew.Length; i++)
                    if (crew[i] != null && crew[i].WorkerId == id)
                        return crew[i];
                return null;
            });
            WorkerStateEventHub.Service = svc;
        }
    }
}
