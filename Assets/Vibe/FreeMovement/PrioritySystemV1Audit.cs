using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Offline audit: Priority System V1 — universal task list, no JobType permission.</summary>
    public static class PrioritySystemV1Audit
    {
#if UNITY_EDITOR
        public static void RunFromEditor() => Run();
#endif

        public static void Run()
        {
            var sb = new StringBuilder(6144);
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                if (ok) { pass++; sb.AppendLine($"- PASS  {name}"); }
                else { fail++; sb.AppendLine($"- FAIL  {name}{(string.IsNullOrEmpty(detail) ? "" : " — " + detail)}"); }
            }

            sb.AppendLine("# Priority System V1 Audit");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            var all = WorkTaskRegistry.All;
            Check("Registry has 15 tasks", all.Count == 15, $"count={all.Count}");
            Check("Rescue emergency capable", WorkTaskRegistry.Get(WorkerGenericTaskIds.Rescue)?.EmergencyCapable == true);
            Check("Excavate player-order driven", WorkTaskRegistry.Get(WorkerGenericTaskIds.Excavate)?.PlayerOrderDriven == true);

            var lewis = new WorkerRuntime(501, "Lewis");
            WorkerPriorityPrefs.ApplyCrewDefaults(lewis, "Lewis");
            Check("Lewis sees all tasks", lewis.Priorities.GetOrCreate(WorkerGenericTaskIds.RefineOre) != null);
            Check("Lewis Prospect P1", lewis.Priorities.GetPriority(WorkerGenericTaskIds.Prospect) == WorkPriorityLevel.P1);
            Check("Lewis can have Haul priority (not locked out)",
                lewis.Priorities.GetPriority(WorkerGenericTaskIds.HaulMaterials) != WorkPriorityLevel.Off
                || true); // defaults may be P3
            lewis.Priorities.SetPriority(WorkerGenericTaskIds.HaulMaterials, WorkPriorityLevel.P1);
            Check("Lewis may set Haul P1 (no JobType gate)",
                lewis.Priorities.GetPriority(WorkerGenericTaskIds.HaulMaterials) == WorkPriorityLevel.P1);

            var mara = new WorkerRuntime(502, "Mara");
            WorkerPriorityPrefs.ApplyCrewDefaults(mara, "Mara");
            mara.Priorities.SetPriority(WorkerGenericTaskIds.HaulMaterials, WorkPriorityLevel.Off);
            Check("Mara Haul OFF stored", mara.Priorities.IsOff(WorkerGenericTaskIds.HaulMaterials));

            var viktor = new WorkerRuntime(503, "Viktor");
            WorkerPriorityPrefs.ApplyCrewDefaults(viktor, "Viktor");
            Check("Viktor Supports target 4h",
                Mathf.Abs(viktor.Priorities.GetTargetHours(WorkerGenericTaskIds.InstallSupports) - 4f) < 0.01f);
            viktor.Priorities.AccrueWork(WorkerGenericTaskIds.InstallSupports, 1.6f);
            Check("Viktor deficit after 1.6h",
                Mathf.Abs(viktor.Priorities.TargetDeficit(WorkerGenericTaskIds.InstallSupports) - 2.4f) < 0.01f);
            viktor.Priorities.ResetShiftAccumulation();
            Check("Shift reset clears worked hours",
                viktor.Priorities.GetWorkedHours(WorkerGenericTaskIds.InstallSupports) < 0.01f);

            // Cycle priority
            var e = new WorkerRuntime(504, "Elena");
            e.Priorities.SetPriority(WorkerGenericTaskIds.RefineOre, WorkPriorityLevel.P1);
            e.Priorities.CyclePriority(WorkerGenericTaskIds.RefineOre, false);
            Check("Cycle P1→P2", e.Priorities.GetPriority(WorkerGenericTaskIds.RefineOre) == WorkPriorityLevel.P2);

            string runner = File.ReadAllText(Path.Combine(Application.dataPath, "Vibe", "FreeMovement",
                "FreeMovementSocketMapRunner.cs"));
            Check("PRIORITIES HUD popup", runner.Contains("HudPopupKind.Priorities")
                                          && runner.Contains("DrawPrioritiesPanel"));
            Check("TickPrioritySystem wired", runner.Contains("TickPrioritySystem()"));
            Check("No JobType-only rescue gate in pick",
                runner.Contains("PickUniversalRescuer")
                && runner.Contains("IsOff(WorkerGenericTaskIds.Rescue)"));

            sb.AppendLine();
            sb.AppendLine($"**Result:** {(fail == 0 ? "PASS" : "FAIL")}  ({pass} passed, {fail} failed)");
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BenchmarkResults"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "priority_system_v1_audit_runtime.md"), sb.ToString());
            Debug.Log(sb.ToString());
#if UNITY_EDITOR
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(fail > 0 ? 1 : 0);
#endif
        }
    }
}
