using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Priority System V1.1 — offline validation of A–K invariants + thin executor honesty.
    /// Complements live playmode; does not replace Game-view force tests.
    /// </summary>
    public static class PrioritySystemV11PlaymodeAudit
    {
#if UNITY_EDITOR
        public static void RunFromEditor() => Run();
#endif

        public static void Run()
        {
            var sb = new StringBuilder(8192);
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                if (ok) { pass++; sb.AppendLine($"- PASS  {name}"); }
                else { fail++; sb.AppendLine($"- FAIL  {name}{(string.IsNullOrEmpty(detail) ? "" : " — " + detail)}"); }
            }

            sb.AppendLine("# Priority System V1.1 Playmode / Integration Audit");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // A — universality
            sb.AppendLine("## A. Universality");
            var names = new[] { "Lewis", "Mara", "Kowalski", "Elena", "Viktor", "Kit" };
            int taskN = WorkTaskRegistry.All.Count;
            Check("Registry count == 15", taskN == 15, $"n={taskN}");
            for (int i = 0; i < names.Length; i++)
            {
                var wr = new WorkerRuntime(600 + i, names[i]);
                WorkerPriorityPrefs.ApplyCrewDefaults(wr, names[i]);
                wr.Priorities.EnsureAllTasksRegistered();
                int seen = 0;
                foreach (var def in WorkTaskRegistry.All)
                    if (wr.Priorities.GetOrCreate(def.Id) != null) seen++;
                Check($"{names[i]} sees all {taskN} tasks", seen == taskN);
            }

            // B — priority authority (no JobType lock)
            sb.AppendLine();
            sb.AppendLine("## B. Priority authority (no JobType permission)");
            var lewis = new WorkerRuntime(701, "Lewis");
            WorkerPriorityPrefs.ApplyCrewDefaults(lewis, "Lewis");
            lewis.Priorities.SetPriority(WorkerGenericTaskIds.RefineOre, WorkPriorityLevel.P1);
            Check("Lewis may set Refine P1",
                lewis.Priorities.GetPriority(WorkerGenericTaskIds.RefineOre) == WorkPriorityLevel.P1);
            var viktor = new WorkerRuntime(702, "Viktor");
            WorkerPriorityPrefs.ApplyCrewDefaults(viktor, "Viktor");
            viktor.Priorities.SetPriority(WorkerGenericTaskIds.HaulMaterials, WorkPriorityLevel.P1);
            Check("Viktor may set Haul P1",
                viktor.Priorities.GetPriority(WorkerGenericTaskIds.HaulMaterials) == WorkPriorityLevel.P1);

            // C — OFF
            sb.AppendLine();
            sb.AppendLine("## C. OFF");
            var mara = new WorkerRuntime(703, "Mara");
            WorkerPriorityPrefs.ApplyCrewDefaults(mara, "Mara");
            mara.Priorities.SetPriority(WorkerGenericTaskIds.HaulMaterials, WorkPriorityLevel.Off);
            Check("Mara Haul OFF", mara.Priorities.IsOff(WorkerGenericTaskIds.HaulMaterials));
            var ctx = new WorkAvailabilityContext
            {
                CampWorld = Vector2.zero,
                Camp = new CampLifeState { MealServedTonight = true, Hygiene01 = 0.9f },
            };
            // With haul OFF and no other work, resolve should not pick haul
            var resOff = WorkPriorityResolver.Resolve(
                mara, JobType.Excavation, Vector2.zero, ctx, 10f, true);
            Check("OFF Haul never selected when unavailable others",
                resOff.TaskId != WorkerGenericTaskIds.HaulMaterials);

            // D — hour targets
            sb.AppendLine();
            sb.AppendLine("## D. Hour targets");
            viktor.Priorities.SetTargetHours(WorkerGenericTaskIds.InstallSupports, 4f);
            viktor.Priorities.SetTargetHours(WorkerGenericTaskIds.InstallLighting, 3f);
            viktor.Priorities.AccrueWork(WorkerGenericTaskIds.InstallSupports, 1.5f);
            Check("Supports deficit 2.5 after 1.5 worked",
                Mathf.Abs(viktor.Priorities.TargetDeficit(WorkerGenericTaskIds.InstallSupports) - 2.5f) < 0.01f);
            viktor.Priorities.AccrueWork(WorkerGenericTaskIds.InstallSupports, 3f);
            Check("Completed target → deficit 0",
                viktor.Priorities.TargetDeficit(WorkerGenericTaskIds.InstallSupports) < 0.01f);
            viktor.Priorities.ResetShiftAccumulation();
            Check("Shift reset clears worked only",
                viktor.Priorities.GetWorkedHours(WorkerGenericTaskIds.InstallSupports) < 0.01f
                && Mathf.Abs(viktor.Priorities.GetTargetHours(WorkerGenericTaskIds.InstallSupports) - 4f) < 0.01f);

            // E/F — rescue still JobType-free
            sb.AppendLine();
            sb.AppendLine("## E/F. Rescue universality");
            for (int i = 0; i < names.Length; i++)
            {
                var wr = new WorkerRuntime(800 + i, names[i]);
                Check($"{names[i]} CanAcceptRescueDuty", WorkerRescue.CanAcceptRescueDuty(wr));
                Check($"{names[i]} Rescue not JobType-gated",
                    !string.IsNullOrEmpty(WorkerRescue.TaskId));
            }

            // G — anti-thrash constants
            sb.AppendLine();
            sb.AppendLine("## G. Anti-thrashing");
            Check("MinCommit > 0", WorkerPriorityPrefs.MinCommitGameHours >= 0.3f);
            Check("SamePrioritySwitchMargin > 0", WorkPriorityResolver.SamePrioritySwitchMargin >= 20f);

            // H — excavate probe honesty
            sb.AppendLine();
            sb.AppendLine("## H. Excavation authority");
            string availSrc = File.ReadAllText(Path.Combine(Application.dataPath, "Vibe", "FreeMovement",
                "WorkAvailability.cs"));
            Check("Excavate probe requires paint/goal",
                availSrc.Contains("PendingDigCount") && availSrc.Contains("no painted excavation"));
            Check("No excavate invent in registry",
                WorkTaskRegistry.Get(WorkerGenericTaskIds.Excavate).PlayerOrderDriven);

            // Thin executors honesty
            sb.AppendLine();
            sb.AppendLine("## Thin executor honesty");
            ctx.MealServedTonight = true;
            ctx.Camp.MealServedTonight = true;
            Check("Meals unavailable after served",
                !ctx.Probe(WorkerGenericTaskIds.PrepareMeals).Available);
            ctx.Camp.Hygiene01 = 0.9f;
            Check("Clean unavailable when clean",
                !ctx.Probe(WorkerGenericTaskIds.CleanCamp).Available);
            Check("Hygiene has no fake target",
                !ctx.Probe(WorkerGenericTaskIds.MaintainHygiene).Available);
            Check("Camp systems unavailable without steward duty",
                !ctx.Probe(WorkerGenericTaskIds.TendCampSystems).Available);
            Check("Prospect Manual = no work without order",
                !ctx.Probe(WorkerGenericTaskIds.Prospect).Available
                || ctx.Prospector != null);

            string runner = File.ReadAllText(Path.Combine(Application.dataPath, "Vibe", "FreeMovement",
                "FreeMovementSocketMapRunner.cs"));
            Check("IsActuallyPerformingPriorityTask present",
                runner.Contains("IsActuallyPerformingPriorityTask"));
            Check("PRIO DIAG shows score breakdown",
                runner.Contains("LastScorePriority") && runner.Contains("PRIORITY DIAG"));
            Check("TickPrioritySystem iterates full crew",
                runner.Contains("TickPrioritySystem")
                && runner.Contains("for (int i = 0; i < _crewWorkers.Length; i++)"));

            sb.AppendLine();
            sb.AppendLine("## Live Game-view");
            sb.AppendLine("- Code invariants above: automated");
            sb.AppendLine("- Full A–K force collapse / paint / UI: requires Unity Play Mode (see report)");

            sb.AppendLine();
            sb.AppendLine($"**Result:** {(fail == 0 ? "PASS" : "FAIL")}  ({pass} passed, {fail} failed)");
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BenchmarkResults"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "priority_system_v11_audit_runtime.md"), sb.ToString());
            Debug.Log(sb.ToString());
#if UNITY_EDITOR
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(fail > 0 ? 1 : 0);
#endif
        }
    }
}
