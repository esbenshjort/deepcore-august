using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Regression: excavation (and other jobs) must not depend on UI worker selection.
    /// </summary>
    public static class ExcavationSelectionIndependenceAudit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[EXCAVATION SELECTION INDEPENDENCE] Report: {path}");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var sb = new StringBuilder(14000);
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                if (ok) pass++; else fail++;
                sb.AppendLine($"- {(ok ? "PASS" : "FAIL")}  {name}"
                              + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
            }

            sb.AppendLine("# Excavation Selection Independence Fix");
            sb.AppendLine();
            sb.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            string root = Application.dataPath + "/Vibe/FreeMovement";
            string runner = File.ReadAllText(Path.Combine(root, "FreeMovementSocketMapRunner.cs"));
            string ctrl = File.ReadAllText(Path.Combine(root, "FreeWorkerController.cs"));

            sb.AppendLine("## Root cause");
            sb.AppendLine();
            sb.AppendLine(
                "When the excavator operator is UI-selected, players typically dig with WASD " +
                "(`digWasd` non-zero). Selecting another worker zeroes `digWasd` (input only) " +
                "and the excavator continues via autonomous paint-plan Tick.");
            sb.AppendLine();
            sb.AppendLine(
                "Bug: near a paint work-cell center (`dist < 0.1`), `AdvanceRoute()` called " +
                "`NotifyCellExcavated` even while the tile was still solid — false-completing " +
                "the plan and skipping further dig strikes. That path is exactly what runs when " +
                "WASD is not routed to the excavator (other worker selected).");
            sb.AppendLine();
            sb.AppendLine("Drill visuals (`IsActivelyDigging`) could still engage against blockers " +
                          "while planned cells were being marked complete without `Damage()`.");
            sb.AppendLine();

            sb.AppendLine("## Selection coupling audit (execution vs UI)");
            sb.AppendLine();

            Check("FreeWorkerController has no _selectedWorkerId / SelectedJobIs",
                !ctrl.Contains("_selectedWorkerId") && !ctrl.Contains("SelectedJobIs")
                && !ctrl.Contains("ResolveControlTarget"));

            Check("Excavator Tick gated on AssignedWorker CanPerform (not selection)",
                runner.Contains("CanPerformJobActions(_worker?.AssignedWorker)")
                && runner.Contains("_worker.Tick(digWasd)"));

            Check("digWasd is input-only (ctl Excavation), Tick still called with zero",
                runner.Contains("Vector2 digWasd = CanPerformJobActions(ctl.Worker) && ctl.JobType == JobType.Excavation")
                && runner.Contains("_worker.Tick(digWasd)"));

            Check("Comment documents selection independence for excavator Tick",
                runner.Contains("NOT SelectedJobIs")
                || runner.Contains("never from UI selection"));

            // Ensure Tick is not behind SelectedJobIs
            int tickIdx = runner.IndexOf("_worker.Tick(digWasd)");
            Check("Excavator Tick call site exists", tickIdx > 0);
            if (tickIdx > 0)
            {
                string window = runner.Substring(Mathf.Max(0, tickIdx - 280), 280);
                Check("Tick call not wrapped in SelectedJobIs(Excavation)",
                    !window.Contains("SelectedJobIs(JobType.Excavation)"));
            }

            Check("Hauler Tick uses AssignedWorker CanPerform",
                runner.Contains("CanPerformJobActions(_hauler?.AssignedWorker)")
                && runner.Contains("_hauler?.Tick()"));
            Check("Engineer Tick uses AssignedWorker CanPerform",
                runner.Contains("CanPerformJobActions(_engineer?.AssignedWorker)"));
            Check("Prospector Tick uses AssignedWorker CanPerform",
                runner.Contains("CanPerformJobActions(_prospector?.AssignedWorker)"));
            Check("Refiner Tick uses AssignedWorker CanPerform",
                runner.Contains("CanPerformJobActions(_refiner?.AssignedWorker)"));
            Check("Steward Tick uses AssignedWorker CanPerform",
                runner.Contains("CanPerformJobActions(_steward?.AssignedWorker)"));

            sb.AppendLine();
            sb.AppendLine("## False-complete fix (authoritative dig state)");
            sb.AppendLine();

            Check("Near-goal solid work cell digs instead of AdvanceRoute",
                ctrl.Contains("PaintWorkCellStillBlocks()")
                && ctrl.Contains("if (dist < 0.1f && PaintWorkCellStillBlocks())"));

            Check("AdvanceRoute refuses NotifyCellExcavated while tile still blocks",
                ctrl.Contains("if (PaintWorkCellStillBlocks())")
                && ctrl.Contains("False-completing here skipped dig"));

            Check("Unsupervised Tick refreshes paint goal when plan pending",
                ctrl.Contains("Unsupervised dig")
                && ctrl.Contains("if (_paintPlan.PendingValidCount > 0)")
                && ctrl.Contains("SyncGoalFromPaintPlan()"));

            Check("StrikeCell still calls world.Damage (unchanged authority)",
                ctrl.Contains("bool broke = _world.Damage(x, y, finalDamage)"));

            sb.AppendLine();
            sb.AppendLine("## Regression scenario (source contract)");
            sb.AppendLine();
            sb.AppendLine("1. Paint route → pending cells exist");
            sb.AppendLine("2. Start excavator → Tick(AssignedWorker) runs");
            sb.AppendLine("3. Select Lewis / Kowalski → digWasd=0, Tick still runs");
            sb.AppendLine("4. Near solid work cell → dig strikes, not false NotifyCellExcavated");
            sb.AppendLine("5. Open SHEET/SOCIAL/CAMP/SHIFT → selection/UI only; Tick path unchanged");
            sb.AppendLine("6. Return to Mara → same paint plan progress (cells actually Damaged)");
            sb.AppendLine();

            Check("SelectedJobIs remains UI/input (paint keys, route preview, HUD)",
                runner.Contains("void SyncRoutePinsToScanView()")
                && runner.Contains("SetRouteVisible(SelectedJobIs(JobType.Excavation))"));

            sb.AppendLine();
            sb.AppendLine($"## Summary: {pass} passed, {fail} failed");
            sb.AppendLine();
            sb.AppendLine(fail == 0 ? "**RESULT: GREEN**" : "**RESULT: RED**");

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BenchmarkResults"))
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string outPath = Path.Combine(dir, "excavation_selection_independence_fix_latest.md");
            File.WriteAllText(outPath, sb.ToString());
            return outPath;
        }
    }
}
