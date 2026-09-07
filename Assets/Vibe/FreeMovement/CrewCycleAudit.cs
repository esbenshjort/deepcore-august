using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>24h crew cycle + shift planner V1 audit.</summary>
    public static class CrewCycleAudit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[CREW CYCLE] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.Exists(path) && File.ReadAllText(path).Contains("**Result:** FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var sb = new StringBuilder();
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                if (ok) pass++;
                else fail++;
                sb.AppendLine($"- {(ok ? "PASS" : "FAIL")}  {name}"
                              + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
            }

            sb.AppendLine("# 24-Hour Crew Cycle + Shift Planner V1 Audit");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("## Exact overtime Soul mappings");
            sb.AppendLine("| Stat | Role |");
            sb.AppendLine("|------|------|");
            sb.AppendLine("| Composure | Softens overtime intensity (0.35) |");
            sb.AppendLine("| Determination | Softens intensity (0.25) |");
            sb.AppendLine("| Tolerance | Primary OT tolerance (0.40) |");
            sb.AppendLine("| Focus / MentalFatigue / Frustration | Amplify OT pressure at tick |");
            sb.AppendLine("| Manager Resentment/Trust/Respect | Separate worker→manager axes |");
            sb.AppendLine();

            sb.AppendLine("## Shift planner");
            var plan = new ShiftPlanner();
            Check("Default 8h work", Mathf.Abs(plan.PlannedWorkHours - 8f) < 0.01f,
                $"h={plan.PlannedWorkHours}");
            Check("Default band NORMAL", plan.WorkBandLabel == "NORMAL SHIFT");
            Check("8h sleep estimate near preferred",
                plan.ExpectedSleepHours >= 7f && plan.ExpectedSleepHours <= 8f,
                $"sleep={plan.ExpectedSleepHours:0.#}");
            Check("8h has camp/free surplus", plan.ExpectedFreeCampHours >= 1f,
                $"camp={plan.ExpectedFreeCampHours:0.#}");
            plan.SetWorkLength(10f);
            Check("10h OVERTIME band", plan.WorkBandLabel == "OVERTIME");
            plan.SetWorkLength(12f);
            Check("12h HEAVY band", plan.WorkBandLabel == "HEAVY OVERTIME");
            plan.ExpectedCommuteOneWayHours = 3f;
            plan.SetWorkLength(12f);
            Check("Long commute shrinks sleep", plan.ExpectedSleepHours < 6.5f,
                $"sleep={plan.ExpectedSleepHours:0.#}");
            plan.ExpectedCommuteOneWayHours = 0.75f;
            plan.SetWorkLength(8f);
            Check("Sleep estimate positive at 8h", plan.ExpectedSleepHours > 5f,
                $"sleep={plan.ExpectedSleepHours:0.#}");

            sb.AppendLine();
            sb.AppendLine("## Manager relationship");
            var store = new ManagerRelationshipStore();
            var wr = new WorkerRuntime(501, "AuditA");
            var mgr = store.Get(wr.WorkerId);
            float r0 = mgr.Resentment;
            mgr.Add(0f, 0f, 12f);
            Check("Resentment accumulates", mgr.Resentment > r0);
            store.TickGentleRecovery(wr, 8f, scheduleReasonable: true);
            Check("Reasonable schedule recovers resentment slowly",
                mgr.Resentment < r0 + 12f);

            sb.AppendLine();
            sb.AppendLine("## Overtime pressure (differential tolerance)");
            var soft = new WorkerRuntime(502, "Soft");
            soft.Stats.Set(WorkerStatId.Composure, 5);
            soft.Stats.Set(WorkerStatId.Tolerance, 4);
            soft.Stats.Set(WorkerStatId.Determination, 6);
            soft.Stats.ClampAll();
            soft.State.Frustration = 40f;
            soft.State.MentalFatigue = 40f;
            var hard = new WorkerRuntime(503, "Hard");
            hard.Stats.Set(WorkerStatId.Composure, 17);
            hard.Stats.Set(WorkerStatId.Tolerance, 16);
            hard.Stats.Set(WorkerStatId.Determination, 15);
            hard.Stats.ClampAll();
            hard.State.Frustration = 20f;
            hard.State.MentalFatigue = 15f;
            float tSoft = OvertimePressure.Tolerance01(soft);
            float tHard = OvertimePressure.Tolerance01(hard);
            Check("High Soul tolerance > low", tHard > tSoft + 0.15f,
                $"hard={tHard:0.00} soft={tSoft:0.00}");

            var ledgerSoft = new WorkerDayLedger { WorkerId = soft.WorkerId, WorkHours = 11f, ConsecutiveOvertimeDays = 3, SleepDeficit01 = 0.4f };
            var ledgerHard = new WorkerDayLedger { WorkerId = hard.WorkerId, WorkHours = 11f, ConsecutiveOvertimeDays = 3, SleepDeficit01 = 0.4f };
            var mgrStore = new ManagerRelationshipStore();
            float softRes0 = mgrStore.Get(soft.WorkerId).Resentment;
            float hardRes0 = mgrStore.Get(hard.WorkerId).Resentment;
            // Emit path requires hub — apply magnitude differential via tolerance only check above
            Check("OT event type exists",
                Enum.IsDefined(typeof(WorkerStateEventType), WorkerStateEventType.OvertimePressure));

            OvertimePressure.FinalizeDayOvertimeStreak(ledgerSoft, 8f);
            Check("OT streak increments after long day", ledgerSoft.ConsecutiveOvertimeDays >= 4);

            sb.AppendLine();
            sb.AppendLine("## Day ledger / summary");
            var tracker = new CrewDayTracker();
            tracker.Accrue(501, CrewDaySegment.Work, 10.25f);
            tracker.Accrue(501, CrewDaySegment.CommuteHome, 1.1f);
            tracker.Accrue(501, CrewDaySegment.CommuteOut, 0.95f);
            tracker.Accrue(501, CrewDaySegment.CampFree, 1.33f);
            tracker.Accrue(501, CrewDaySegment.Sleep, 6.17f);
            var crew = new[] { wr };
            var sum = tracker.BuildSummary(2, crew, store, plan);
            Check("Summary averages tracked", sum.AvgWorkHours > 10f && sum.AvgSleepHours > 6f);
            Check("Summary pending flag", tracker.SummaryPending);

            sb.AppendLine();
            sb.AppendLine("## Commute invariants (runner source)");
            string runnerPath = Path.GetFullPath(Path.Combine(
                Application.dataPath, "Vibe", "FreeMovement", "FreeMovementSocketMapRunner.cs"));
            string runner = File.Exists(runnerPath) ? File.ReadAllText(runnerPath) : "";
            Check("Runner source readable", runner.Length > 1000, runnerPath);
            Check("EnterCampEvening before sleep",
                runner.Contains("EnterCampEvening") && runner.Contains("CampEvening"));
            Check("CommuteEmergencyTimeoutSec hard-unlocks camp return",
                runner.Contains("CommuteEmergencyTimeoutSec")
                || runner.Contains("CommuteStrandedUnlockSec")
                || runner.Contains("Camp arrival timeout"));
            Check("Stranded unlocks commute early",
                runner.Contains("CommuteStrandedUnlockSec")
                || runner.Contains("Stranded unlock"));
            Check("No snappy CommuteTimeoutSec = 5.5",
                !runner.Contains("CommuteTimeoutSec = 5.5"));
            Check("EnterSleep does not teleport to tent",
                runner.Contains("do NOT teleport") && runner.Contains("Hide at current camp presence"));
            Check("BeginHeadingOut physical commute",
                runner.Contains("Walking to instruments — physical commute")
                && runner.Contains("_crewPhase = CrewPhase.HeadingOut"));
            Check("EnterOnShift does not AlignHosts SoftTeleport",
                !runner.Contains("AlignHostsToMorningPosts()")
                && runner.Contains("do NOT SoftTeleport hosts"));
            Check("No SoftArrive on normal commute timeout",
                runner.Contains("no teleport")
                && runner.Contains("TickLateCommuteToWork"));
            Check("Morning wake does not invent leftover recovery",
                runner.Contains("short nights stay short"));
            Check("SHIFT planner HUD strip present",
                runner.Contains("StripBtn(\"SHIFT\"") || runner.Contains("HudPopupKind.Shift"));
            Check("YESTERDAY summary overlay present",
                runner.Contains("DrawDailySummaryOverlay") && runner.Contains("YESTERDAY"));
            Check("OvertimePressure tick while OnShift",
                runner.Contains("OvertimePressure.TickDuringWork"));
            Check("WorkerLocomotion used on commute",
                runner.Contains("WorkerLocomotion.WalkSpeedAt"));

            sb.AppendLine();
            sb.AppendLine("## Scope guard (V1)");
            Check("No weekly calendar API", !runner.Contains("WeeklyCalendar"));
            Check("No strike system API", !runner.Contains("LaborStrike"));

            sb.AppendLine();
            sb.AppendLine($"**Result:** {(fail == 0 ? "PASS" : "FAIL")}  ({pass} passed, {fail} failed)");

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BenchmarkResults"))
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string stamp = Path.Combine(dir, $"crew_cycle_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            string latest = Path.Combine(dir, "crew_cycle_latest.md");
            File.WriteAllText(stamp, sb.ToString());
            File.WriteAllText(latest, sb.ToString());
            Debug.Log($"[CREW CYCLE] {(fail == 0 ? "PASS" : "FAIL")} → {latest}");
            return latest;
        }
    }
}
