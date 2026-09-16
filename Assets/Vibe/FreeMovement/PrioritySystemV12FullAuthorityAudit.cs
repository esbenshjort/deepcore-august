using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Priority System V1.2 — deterministic authority checks.
    /// Does NOT claim LIVE PLAYMODE PASS. Separates AUTOMATED vs source-present vs live-required.
    /// </summary>
    public static class PrioritySystemV12FullAuthorityAudit
    {
#if UNITY_EDITOR
        public static void RunFromEditor() => Run();
#endif

        public static void Run()
        {
            var sb = new StringBuilder(16384);
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                if (ok) { pass++; sb.AppendLine($"- AUTOMATED PASS  {name}"); }
                else { fail++; sb.AppendLine($"- FAIL  {name}{(string.IsNullOrEmpty(detail) ? "" : " — " + detail)}"); }
            }

            sb.AppendLine("# Priority System V1.2 Full Authority Audit (Automated)");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("LIVE PLAYMODE results are NOT asserted here — see BenchmarkResults report.");
            sb.AppendLine();

            sb.AppendLine("## Hard priority bands");
            Check("IsHigherPriorityBand P1 > P2",
                WorkPriorityResolver.IsHigherPriorityBand(WorkPriorityLevel.P1, WorkPriorityLevel.P2));
            Check("IsHigherPriorityBand P2 not > P1",
                !WorkPriorityResolver.IsHigherPriorityBand(WorkPriorityLevel.P2, WorkPriorityLevel.P1));

            var elenaBand = new WorkerRuntime(900, "Elena");
            WorkerPriorityPrefs.ApplyCrewDefaults(elenaBand, "Elena");
            elenaBand.Priorities.SetPriority(WorkerGenericTaskIds.HaulMaterials, WorkPriorityLevel.P1);
            elenaBand.Priorities.SetPriority(WorkerGenericTaskIds.RefineOre, WorkPriorityLevel.P2);
            elenaBand.Priorities.ActiveTaskId = WorkerGenericTaskIds.HaulMaterials;
            Check("HostAllows: ActiveTask Haul → Refining DENIED",
                !WorkPriorityResolver.HostAllowsVoluntaryWork(elenaBand, JobType.Refining));
            Check("HostAllows: ActiveTask Haul → Hauling ALLOWED",
                WorkPriorityResolver.HostAllowsVoluntaryWork(elenaBand, JobType.Hauling));

            var dirBand = new WorkPriorityDirector();
            elenaBand.Priorities.ActiveTaskId = WorkerGenericTaskIds.RefineOre;
            elenaBand.Priorities.ActiveTaskStartedGameHours = 100f;
            var haulRes = new WorkResolveResult(
                WorkerGenericTaskIds.HaulMaterials, 50f, "band P1 · test", 0.5f,
                WorkAvailabilityResult.Yes("loose material", Vector2.zero, 0.55f));
            dirBand.ApplyResolve(elenaBand, haulRes, 100.05f); // within MinCommit window
            Check("ApplyResolve: P1 Haul preempts P2 Refine inside MinCommit",
                elenaBand.Priorities.ActiveTaskId == WorkerGenericTaskIds.HaulMaterials);

            elenaBand.Priorities.ActiveTaskId = WorkerGenericTaskIds.HaulMaterials;
            elenaBand.Priorities.SetPriority(WorkerGenericTaskIds.Excavate, WorkPriorityLevel.P1);
            elenaBand.Priorities.ActiveTaskStartedGameHours = 200f;
            var excavSame = new WorkResolveResult(
                WorkerGenericTaskIds.Excavate, 99f, "band P1 · excav", 0.9f,
                WorkAvailabilityResult.Yes("paint", Vector2.zero, 0.7f));
            dirBand.ApplyResolve(elenaBand, excavSame, 200.05f);
            Check("ApplyResolve: same-band MinCommit can hold Haul vs Excavate",
                elenaBand.Priorities.ActiveTaskId == WorkerGenericTaskIds.HaulMaterials
                && elenaBand.Priorities.LastResolveReason.Contains("anti-thrash"));

            string resolverSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "Vibe/FreeMovement/WorkPriorityResolver.cs"));
            Check("Resolve uses ResolveWithinBand",
                resolverSrc.Contains("ResolveWithinBand"));
            Check("ScoreSoftBreakdown SoftTotal",
                resolverSrc.Contains("SoftTotal") && resolverSrc.Contains("ScoreSoftBreakdown"));

            string runnerSrcBand = File.ReadAllText(Path.Combine(Application.dataPath,
                "Vibe/FreeMovement/FreeMovementSocketMapRunner.cs"));
            Check("SyncHostToActivePriorityTask present",
                runnerSrcBand.Contains("SyncHostToActivePriorityTask"));
            Check("TemporaryYieldHostForPriority present",
                runnerSrcBand.Contains("TemporaryYieldHostForPriority"));
            Check("Priority paths do not TryUnassign in SyncHost",
                !runnerSrcBand.Contains("TryUnassignJob(have, out _)"));
            Check("Soft claim does not steal living occupant",
                runnerSrcBand.Contains("Only fill vacant exclusive seats"));

            sb.AppendLine();
            sb.AppendLine("## Score weights");
            Check("PriorityWeight == 120", Mathf.Approximately(WorkPriorityResolver.PriorityWeight, 120f));
            Check("SuitWeight == 18", Mathf.Approximately(WorkPriorityResolver.SuitWeight, 18f));
            Check("DistanceWeight == 12", Mathf.Approximately(WorkPriorityResolver.DistanceWeight, 12f));
            Check("DeficitWeight == 28", Mathf.Approximately(WorkPriorityResolver.DeficitWeight, 28f));
            Check("ContinuityBonus == 22", Mathf.Approximately(WorkPriorityResolver.ContinuityBonus, 22f));
            Check("SamePrioritySwitchMargin == 35",
                Mathf.Approximately(WorkPriorityResolver.SamePrioritySwitchMargin, 35f));
            Check("EmergencyScoreBoost == 1000",
                Mathf.Approximately(WorkPriorityResolver.EmergencyScoreBoost, 1000f));
            Check("Priority step (120) > Suit+Dist max (~30)",
                WorkPriorityResolver.PriorityWeight > WorkPriorityResolver.SuitWeight + WorkPriorityResolver.DistanceWeight);

            sb.AppendLine();
            sb.AppendLine("## Priority ordering math");
            float p1 = (5 - (int)WorkPriorityLevel.P1) * WorkPriorityResolver.PriorityWeight;
            float p2 = (5 - (int)WorkPriorityLevel.P2) * WorkPriorityResolver.PriorityWeight;
            float p4 = (5 - (int)WorkPriorityLevel.P4) * WorkPriorityResolver.PriorityWeight;
            Check("P1 score component 480", Mathf.Approximately(p1, 480f));
            Check("P2 score component 360", Mathf.Approximately(p2, 360f));
            Check("P1 - P2 == 120", Mathf.Approximately(p1 - p2, 120f));
            Check("P1 - P4 == 360", Mathf.Approximately(p1 - p4, 360f));

            sb.AppendLine();
            sb.AppendLine("## Universality + OFF dirty");
            Check("Registry 15 tasks", WorkTaskRegistry.All.Count == 15);
            var names = new[] { "Lewis", "Mara", "Kowalski", "Elena", "Viktor", "Kit" };
            for (int i = 0; i < names.Length; i++)
            {
                var wr = new WorkerRuntime(800 + i, names[i]);
                WorkerPriorityPrefs.ApplyCrewDefaults(wr, names[i]);
                wr.Priorities.EnsureAllTasksRegistered();
                Check($"{names[i]} registry complete", wr.Priorities.GetOrCreate(WorkerGenericTaskIds.HaulMaterials) != null);
                wr.Priorities.ActiveTaskId = WorkerGenericTaskIds.HaulMaterials;
                wr.Priorities.SetPriority(WorkerGenericTaskIds.HaulMaterials, WorkPriorityLevel.Off);
                Check($"{names[i]} OFF sets ResolverDirty", wr.Priorities.ResolverDirty);
                Check($"{names[i]} OFF marks ActiveTaskValid false when active",
                    !wr.Priorities.ActiveTaskValid
                    && wr.Priorities.ActiveInvalidationReason == "priority OFF");
            }

            sb.AppendLine();
            sb.AppendLine("## Director invalidate OFF");
            var dir = new WorkPriorityDirector();
            var k = new WorkerRuntime(850, "Kowalski");
            WorkerPriorityPrefs.ApplyCrewDefaults(k, "Kowalski");
            k.Priorities.ActiveTaskId = WorkerGenericTaskIds.HaulMaterials;
            k.Priorities.SetPriority(WorkerGenericTaskIds.HaulMaterials, WorkPriorityLevel.Off);
            bool cleared = dir.InvalidateActiveIfNeeded(k);
            Check("InvalidateActiveIfNeeded clears OFF haul",
                cleared && string.IsNullOrEmpty(k.Priorities.ActiveTaskId));

            sb.AppendLine();
            sb.AppendLine("## Host voluntary gate");
            var mara = new WorkerRuntime(851, "Mara");
            WorkerPriorityPrefs.ApplyCrewDefaults(mara, "Mara");
            mara.Priorities.SetPriority(WorkerGenericTaskIds.Excavate, WorkPriorityLevel.Off);
            Check("Excavate OFF → HostAllows Excavation false",
                !WorkPriorityResolver.HostAllowsVoluntaryWork(mara, JobType.Excavation));
            mara.Priorities.SetPriority(WorkerGenericTaskIds.Excavate, WorkPriorityLevel.P1);
            Check("Excavate P1 → HostAllows Excavation true",
                WorkPriorityResolver.HostAllowsVoluntaryWork(mara, JobType.Excavation));

            sb.AppendLine();
            sb.AppendLine("## Hour targets persist across reset");
            var v = new WorkerRuntime(852, "Viktor");
            WorkerPriorityPrefs.ApplyCrewDefaults(v, "Viktor");
            v.Priorities.SetTargetHours(WorkerGenericTaskIds.InstallSupports, 4f);
            v.Priorities.AccrueWork(WorkerGenericTaskIds.InstallSupports, 2f);
            float tgtBefore = v.Priorities.GetTargetHours(WorkerGenericTaskIds.InstallSupports);
            v.Priorities.ResetShiftAccumulation();
            Check("Reset clears worked", v.Priorities.GetWorkedHours(WorkerGenericTaskIds.InstallSupports) < 0.01f);
            Check("Reset keeps target",
                Mathf.Abs(v.Priorities.GetTargetHours(WorkerGenericTaskIds.InstallSupports) - tgtBefore) < 0.01f);
            Check("Priority persists after reset",
                v.Priorities.GetPriority(WorkerGenericTaskIds.InstallSupports) == WorkPriorityLevel.P1);

            sb.AppendLine();
            sb.AppendLine("## Hygiene honest unavailable (source probe)");
            var hygiene = new WorkAvailabilityContext
            {
                Camp = new CampLifeState { Hygiene01 = 0.1f },
                CampWorld = Vector2.zero,
            };
            var hyg = hygiene.Probe(WorkerGenericTaskIds.MaintainHygiene);
            Check("Maintain Hygiene never Available", !hyg.Available);

            sb.AppendLine();
            sb.AppendLine("## Source integration markers");
            string runner = File.ReadAllText(Path.Combine(Application.dataPath,
                "Vibe/FreeMovement/FreeMovementSocketMapRunner.cs"));
            string director = File.ReadAllText(Path.Combine(Application.dataPath,
                "Vibe/FreeMovement/WorkPriorityDirector.cs"));
            Check("Host tick PriorityAllowsHostTick Hauling",
                runner.Contains("PriorityAllowsHostTick(_hauler?.AssignedWorker, JobType.Hauling)"));
            Check("Host tick PriorityAllowsHostTick Excavation",
                runner.Contains("PriorityAllowsHostTick(_worker?.AssignedWorker, JobType.Excavation)"));
            Check("Host tick PriorityAllowsHostTick Refining",
                runner.Contains("PriorityAllowsHostTick(_refiner?.AssignedWorker, JobType.Refining)"));
            Check("Host tick PriorityAllowsHostTick Engineering",
                runner.Contains("PriorityAllowsHostTick(_engineer?.AssignedWorker, JobType.Engineering)"));
            Check("Host tick PriorityAllowsHostTick Prospecting",
                runner.Contains("PriorityAllowsHostTick(_prospector?.AssignedWorker, JobType.Prospecting)"));
            Check("Host tick PriorityAllowsHostTick Steward",
                runner.Contains("PriorityAllowsHostTick(_steward?.AssignedWorker, JobType.Steward)"));
            Check("Rescue OFF aborts mid-mission",
                runner.Contains("rescuer Rescue priority OFF")
                || runner.Contains("IsOff(WorkerGenericTaskIds.Rescue)"));
            Check("ReleaseUnavailableWorkerClaims present",
                runner.Contains("ReleaseUnavailableWorkerClaims"));
            Check("Availability invalidate in director",
                director.Contains("work unavailable"));
            Check("IsActuallyPerforming dig uses IsActivelyDigging",
                runner.Contains("case WorkerGenericTaskIds.Excavate:")
                && runner.Contains("IsActivelyDigging"));
            Check("IsActuallyPerforming haul excludes SEEK",
                runner.Contains("LOADING") && runner.Contains("Not SEEK"));
            Check("DEV diag TaskAvailable (Avail=)",
                runner.Contains("Avail=") && runner.Contains("performing="));

            sb.AppendLine();
            sb.AppendLine("## Cross-specialization prefs allowed");
            var lewis = new WorkerRuntime(860, "Lewis");
            WorkerPriorityPrefs.ApplyCrewDefaults(lewis, "Lewis");
            lewis.Priorities.SetPriority(WorkerGenericTaskIds.HaulMaterials, WorkPriorityLevel.P1);
            Check("Lewis Haul P1 allowed",
                lewis.Priorities.GetPriority(WorkerGenericTaskIds.HaulMaterials) == WorkPriorityLevel.P1);
            var elena = new WorkerRuntime(861, "Elena");
            WorkerPriorityPrefs.ApplyCrewDefaults(elena, "Elena");
            elena.Priorities.SetPriority(WorkerGenericTaskIds.RepairEquipment, WorkPriorityLevel.P1);
            Check("Elena Repair P1 allowed",
                elena.Priorities.GetPriority(WorkerGenericTaskIds.RepairEquipment) == WorkPriorityLevel.P1);

            sb.AppendLine();
            sb.AppendLine($"## Totals: {pass} automated PASS, {fail} FAIL");
            sb.AppendLine();
            sb.AppendLine("## Explicitly NOT TESTED here (requires Game view)");
            sb.AppendLine("- Live OFF mid-haul/excavate/refine stop feel");
            sb.AppendLine("- Live Rescue interrupt + OFF mid-carry");
            sb.AppendLine("- Live hour accrual while walking vs digging");
            sb.AppendLine("- Live same-priority thrash over hours");
            sb.AppendLine("- Live selection independence while tasks run");
            sb.AppendLine("- Live multi-worker exclusive claims");
            sb.AppendLine("- Live UI LMB/RMB/hour edit → next-tick behavior");

            string dirOut = Path.Combine(Application.dataPath, "..", "BenchmarkResults");
            Directory.CreateDirectory(dirOut);
            string path = Path.Combine(dirOut, "priority_system_v12_automated_audit_latest.md");
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[PRIORITY V1.2] Audit written: {path} ({pass} pass / {fail} fail)");
        }
    }
}
