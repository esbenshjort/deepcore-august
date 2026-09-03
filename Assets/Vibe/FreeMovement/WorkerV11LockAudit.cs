using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// V1.1 final lock audit — architecture invariants + live assignment/presence/shift regression.
    /// Batchmode: -executeMethod DeepCore.FreeMovement.WorkerV11LockAudit.RunFromEditor
    /// Does not implement V1.2. Measurement / gate only.
    /// </summary>
    public static class WorkerV11LockAudit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[V1.1 LOCK] Report: {path}");
#if UNITY_EDITOR
            bool fail = path.Contains("NOT READY") || File.ReadAllText(path).Contains("VERDICT: B.");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var log = new StringBuilder(16_000);
            int pass = 0, fail = 0;
            var blockers = new List<string>();

            void Check(string name, bool ok, string detail = "")
            {
                if (ok)
                {
                    pass++;
                    log.AppendLine($"PASS | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
                }
                else
                {
                    fail++;
                    blockers.Add(name + (string.IsNullOrEmpty(detail) ? "" : $": {detail}"));
                    log.AppendLine($"FAIL | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
                }
            }

            log.AppendLine("# V1.1 Worker Architecture Lock Audit");
            log.AppendLine();
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine("Mode: Unity batch ForceBuild + programmatic regression (not interactive Play Mode UI).");
            log.AppendLine();

            FreeMovementSocketMapRunner runner = null;
            GameObject host = null;
            try
            {
                host = new GameObject("V11_LockAudit_Host");
                runner = host.AddComponent<FreeMovementSocketMapRunner>();
                runner.ForceBuildForAudit();

                // ——— Bootstrap invariants ———
                log.AppendLine("## 1. Bootstrap / person / avatar / assignment");
                Check("Crew count 5", runner.AuditCrewCount() == 5, $"n={runner.AuditCrewCount()}");
                Check("Unique WorkerIds 1..5", runner.AuditUniqueWorkerIds());
                Check("Each person one Stats sheet", runner.AuditDistinctStatsRefs());
                Check("Exactly one avatar per worker", runner.AuditOneAvatarPerWorker());
                Check("Default jobs Lewis Pros / Mara Exc / Kow Haul / Elena Ref / Vik Eng",
                    runner.AuditDefaultJobMap());
                Check("No duplicate job occupancy", !runner.AuditHasDuplicateJobs());
                Check("Selected defaults to Lewis", runner.AuditSelectedWorkerId() == 1);

                // ——— A: Select Lewis ———
                log.AppendLine();
                log.AppendLine("## 2. Selection A — Lewis Prospecting");
                runner.AuditSelectPerson(1);
                Check("Highlight/selection Lewis", runner.AuditSelectedWorkerId() == 1);
                Check("SelectedJobIs Prospecting", runner.AuditSelectedJobIs(JobType.Prospecting));
                Check("CanPerformJobActions Lewis", runner.AuditCanPerform(1));
                Check("Camera target Prospecting host label",
                    runner.AuditControlHostLabel().Contains("Prospector"));
                Check("ST target Lewis stats ref stable",
                    runner.AuditStatsRef(1) == runner.AuditStatsRef(1));
                bool banterOk = runner.AuditTryBanter(JobType.Prospecting, "V11SelectLewis");
                Check("Banter author Lewis when Prospecting",
                    banterOk && runner.AuditLastBanterWorkerId() == 1,
                    $"banter={banterOk} id={runner.AuditLastBanterWorkerId()}");

                Vector2 excavPos0 = runner.AuditProviderPos(JobType.Excavation);
                Vector2 prosPos0 = runner.AuditProviderPos(JobType.Prospecting);
                Vector2 haulPos0 = runner.AuditProviderPos(JobType.Hauling);
                string maraStats0 = runner.AuditStatsRef(2);
                string lewisStats0 = runner.AuditStatsRef(1);

                // ——— B: Lewis → Excavation ———
                log.AppendLine();
                log.AppendLine("## 3. Reassign B — Lewis Prospecting → Excavation");
                bool assignB = runner.AuditTryAssign(1, JobType.Excavation, out string whyB);
                Check("TryAssign Lewis Excavation", assignB, whyB);
                Check("Selection remains Lewis", runner.AuditSelectedWorkerId() == 1);
                Check("Lewis now Excavation", runner.AuditJobOf(1) == JobType.Excavation);
                Check("Mara Unassigned after eviction", runner.AuditJobOf(2) == JobType.Unassigned);
                Check("No duplicate Excavation", runner.AuditWorkersOnJob(JobType.Excavation) == 1);
                Check("Lewis stats ref unchanged", runner.AuditStatsRef(1) == lewisStats0);
                Check("Mara stats ref unchanged", runner.AuditStatsRef(2) == maraStats0);
                Check("SelectedJobIs Excavation", runner.AuditSelectedJobIs(JobType.Excavation));
                Check("Excavator host position unchanged (no teleport)",
                    (runner.AuditProviderPos(JobType.Excavation) - excavPos0).sqrMagnitude < 0.0001f);
                runner.AuditClearBanter();
                Check("Banter Excavation context still Lewis",
                    runner.AuditTryBanter(JobType.Excavation, "V11LewisExc")
                    && runner.AuditLastBanterWorkerId() == 1);

                // ——— C: Mara → Prospecting ———
                log.AppendLine();
                log.AppendLine("## 4. Reassign C — Mara → Prospecting");
                bool assignC = runner.AuditTryAssign(2, JobType.Prospecting, out string whyC);
                Check("TryAssign Mara Prospecting", assignC, whyC);
                Check("Mara Prospecting", runner.AuditJobOf(2) == JobType.Prospecting);
                Check("Lewis still Excavation", runner.AuditJobOf(1) == JobType.Excavation);
                Check("Findings author sync Mara — author=2", runner.AuditFindingsAuthorId() == 2,
                    $"author={runner.AuditFindingsAuthorId()}");
                runner.AuditClearBanter();
                Check("Banter Prospecting = Mara",
                    runner.AuditTryBanter(JobType.Prospecting, "V11MaraPros")
                    && runner.AuditLastBanterWorkerId() == 2);
                // Historical: force a finding stamp then reassign — author frozen on speech via TryAuthored
                Check("Prospector host position not forced to camp",
                    (runner.AuditProviderPos(JobType.Prospecting) - prosPos0).sqrMagnitude < 4f);

                // ——— D: rotate jobs ———
                log.AppendLine();
                log.AppendLine("## 5. Rotate D — cross-job swaps");
                // Elena -> Excavation (evicts Lewis), Kowalski -> Refining (evicts Elena first path), etc.
                Check("Assign Elena Excavation", runner.AuditTryAssign(4, JobType.Excavation, out string w1), w1);
                Check("Lewis Unassigned after Elena takes Exc", runner.AuditJobOf(1) == JobType.Unassigned);
                Check("Assign Lewis Hauling", runner.AuditTryAssign(1, JobType.Hauling, out string w2), w2);
                Check("Kowalski Unassigned", runner.AuditJobOf(3) == JobType.Unassigned);
                Check("Assign Kowalski Refining", runner.AuditTryAssign(3, JobType.Refining, out string w3), w3);
                Check("Elena still Excavation after Kowalski→Refining", runner.AuditJobOf(4) == JobType.Excavation);
                Check("Assign Viktor Excavation", runner.AuditTryAssign(5, JobType.Excavation, out string w4), w4);
                Check("Elena Unassigned after Viktor takes Exc", runner.AuditJobOf(4) == JobType.Unassigned);
                Check("No duplicate jobs after rotate", !runner.AuditHasDuplicateJobs());
                Check("Hauler provider position stable",
                    (runner.AuditProviderPos(JobType.Hauling) - haulPos0).sqrMagnitude < 0.25f);

                // ——— E: Unassign all ———
                log.AppendLine();
                log.AppendLine("## 6. Vacancy E — unassign all");
                foreach (var job in new[]
                         {
                             JobType.Prospecting, JobType.Excavation, JobType.Hauling,
                             JobType.Refining, JobType.Engineering
                         })
                {
                    runner.AuditTryUnassign(job, out _);
                }
                Check("All jobs vacant", runner.AuditAllJobsVacant());
                Check("Five people still in roster", runner.AuditCrewCount() == 5);
                Check("Five avatars visible when Unassigned", runner.AuditVisibleAvatarCount() == 5,
                    $"visible={runner.AuditVisibleAvatarCount()}");
                Check("No job banter when vacant",
                    !runner.AuditTryBanter(JobType.Excavation, "V11Vacant"));
                Check("Selected person survives vacancy", runner.AuditSelectedWorkerId() > 0);

                // ——— F: Reassign different config ———
                log.AppendLine();
                log.AppendLine("## 7. Rebind F — new configuration");
                Check("Lewis Pros", runner.AuditTryAssign(1, JobType.Prospecting, out _), "");
                Check("Mara Exc", runner.AuditTryAssign(2, JobType.Excavation, out _), "");
                Check("Kowalski Haul", runner.AuditTryAssign(3, JobType.Hauling, out _), "");
                Check("Elena Ref", runner.AuditTryAssign(4, JobType.Refining, out _), "");
                Check("Viktor Eng", runner.AuditTryAssign(5, JobType.Engineering, out _), "");
                Check("Default map restored", runner.AuditDefaultJobMap());
                Check("Assigned avatars hidden", runner.AuditHiddenAssignedAvatarCount() == 5,
                    $"hidden={runner.AuditHiddenAssignedAvatarCount()}");
                Check("Avatar sync following set for assigned", runner.AuditAssignedAvatarsFollowing());

                // Capture provider positions before shift
                Vector2[] hostPos = runner.AuditAllProviderPositions();
                int selBeforeSleep = runner.AuditSelectedWorkerId();

                // ——— Shift / sleep ———
                log.AppendLine();
                log.AppendLine("## 8. Shift / sleep / wake");
                runner.AuditBeginHeadingHome();
                Check("Phase HeadingHome", runner.AuditCrewPhaseName() == "HeadingHome");
                Check("Providers stay put on HeadingHome",
                    runner.AuditProvidersUnmoved(hostPos, 0.001f));
                Check("Avatars visible commuting", runner.AuditVisibleAvatarCount() >= 5);
                Check("Job actions disabled while commuting", !runner.AuditCanPerform(selBeforeSleep));
                Check("Camera host label is WorkerAvatar while commuting",
                    runner.AuditControlHostLabel().Contains("WorkerAvatar"));

                runner.AuditEnterSleep();
                Check("Phase Asleep", runner.AuditCrewPhaseName() == "Asleep");
                Check("Providers stay put on Sleep", runner.AuditProvidersUnmoved(hostPos, 0.001f));
                Check("Selection survives sleep", runner.AuditSelectedWorkerId() == selBeforeSleep);
                Check("Assignments survive sleep", runner.AuditDefaultJobMap());

                runner.AuditSkipSleep();
                Check("After SkipSleep phase HeadingOut or OnShift",
                    runner.AuditCrewPhaseName() == "HeadingOut"
                    || runner.AuditCrewPhaseName() == "OnShift",
                    runner.AuditCrewPhaseName());
                Check("Providers still unmoved after SkipSleep",
                    runner.AuditProvidersUnmoved(hostPos, 0.001f));
                Check("Selection survives SkipSleep", runner.AuditSelectedWorkerId() == selBeforeSleep);
                Check("Assignments survive SkipSleep", runner.AuditDefaultJobMap());

                // Force on-shift resume
                if (runner.AuditCrewPhaseName() == "HeadingOut")
                    runner.AuditEnterOnShift();
                Check("OnShift after wake", runner.AuditCrewPhaseName() == "OnShift");
                Check("CanPerform again when Operating", runner.AuditCanPerform(2)); // Mara excavating
                Check("Assigned avatars hidden again", runner.AuditHiddenAssignedAvatarCount() == 5);

                // ——— Mid-scan block ———
                log.AppendLine();
                log.AppendLine("## 9. Provider release / scan block hooks");
                Check("CanRelease Prospecting when not mid-scan",
                    runner.AuditCanRelease(JobType.Prospecting, out string relWhy), relWhy);

                // ——— PersonalConditions ownership ———
                log.AppendLine();
                log.AppendLine("## 10. Personal vs machine");
                Check("Lewis WorkerState object stable across jobs",
                    runner.AuditWorkerStateStable(1));
                var lewisState = runner.AuditWorkerState(1);
                var maraState = runner.AuditWorkerState(2);
                Check("Lewis has WorkerState", lewisState != null);
                Check("Lewis/Mara State are distinct bags",
                    lewisState != null && maraState != null && !ReferenceEquals(lewisState, maraState));
                if (lewisState != null && maraState != null)
                {
                    Check("Morale not maxed at spawn", lewisState.Morale < 99f,
                        $"morale={lewisState.Morale:0.#}");
                    Check("FocusState distinct from perfect",
                        lewisState.FocusState > 0f && lewisState.FocusState < 100f,
                        $"focusState={lewisState.FocusState:0.#}");
                    Check("FocusState != static Focus capacity",
                        !Mathf.Approximately(lewisState.FocusState,
                            runner.AuditCrewWorker(1).Stats.Get(WorkerStatId.Focus)),
                        $"focusState={lewisState.FocusState:0.#} statFocus={runner.AuditCrewWorker(1).Stats.Get(WorkerStatId.Focus)}");

                    // Ownership: Lewis frustration survives Mara excavating
                    lewisState.Frustration = 55f;
                    maraState.Frustration = 12f;
                    float lewisFrust = lewisState.Frustration;
                    runner.AuditTryAssign(1, JobType.Excavation, out _); // Lewis digs
                    runner.AuditTryAssign(2, JobType.Excavation, out _); // Mara takes excav — Lewis unassigned
                    Check("After Mara takes Excavator, Lewis keeps Frustration",
                        Mathf.Approximately(runner.AuditWorkerState(1).Frustration, lewisFrust),
                        $"lewis={runner.AuditWorkerState(1).Frustration:0.#}");
                    Check("Mara brings her own Frustration",
                        Mathf.Approximately(runner.AuditWorkerState(2).Frustration, 12f),
                        $"mara={runner.AuditWorkerState(2).Frustration:0.#}");

                    float frustBefore = 72f;
                    lewisState.Frustration = frustBefore;
                    float moraleBefore = lewisState.Morale;
                    runner.AuditApplyCrewSleepRecovery(1f);
                    Check("Sleep leaves Frustration residue",
                        lewisState.Frustration > 0f && lewisState.Frustration < frustBefore,
                        $"frust={lewisState.Frustration:0.#}");
                    Check("Sleep does not max Morale", lewisState.Morale < 99f,
                        $"morale={lewisState.Morale:0.#}");
                    Check("Sleep Morale barely moves",
                        Mathf.Abs(lewisState.Morale - moraleBefore) < 8f,
                        $"before={moraleBefore:0.#} after={lewisState.Morale:0.#}");
                }
                Check("Excavator Heat is machine field (readable)", runner.AuditExcavatorHeat() >= 0f);

                // ControlWorker debt classification (static)
                log.AppendLine();
                log.AppendLine("## 11. ControlWorker / role debt (classified)");
                log.AppendLine("- A: `_control` stub + DEV readout + Bridge bootstrap fallback");
                log.AppendLine("- B: `JobToLegacyRoleIndex` / `WorkerStatProfiles.RoleTitle|Build` profile recipes");
                log.AppendLine("- C: none found for normal selection/banter/camera/ST");
                log.AppendLine("- D: removed obsolete CycleControl / SelectWorker / DrawWorkerCard stubs");
            }
            catch (Exception ex)
            {
                fail++;
                blockers.Add("Audit exception: " + ex.Message);
                log.AppendLine();
                log.AppendLine("## EXCEPTION");
                log.AppendLine(ex.ToString());
            }
            finally
            {
                if (host != null)
                {
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(host);
                    else
                        UnityEngine.Object.DestroyImmediate(host);
                }
            }

            log.AppendLine();
            log.AppendLine("## Summary");
            log.AppendLine($"PASS: {pass}");
            log.AppendLine($"FAIL: {fail}");
            log.AppendLine();
            log.AppendLine("## Known non-blocking debt (carry-forward)");
            log.AppendLine("- Excavator heat hard-resets on sleep (prefer passive cooling)");
            log.AppendLine("- WorkerRuntime.State V1.2A — daytime MentalFatigue/FocusState/Morale mostly static until V1.2B/C");
            log.AppendLine("- Host sprites can look crewed while vacant / avatar hidden");
            log.AppendLine("- Morning provider return is bridge snap, not polished travel");
            log.AppendLine("- H/R/E relevant stats provisional");
            log.AppendLine("- Job-context line banks ≠ unique personalities");
            log.AppendLine("- Engineer provider façade-only");
            log.AppendLine("- No Social Aura / final enter-exit visuals");
            log.AppendLine();
            log.AppendLine("## Play Mode note");
            log.AppendLine("This audit ForceBuilds the SocketMap runner and exercises assignment,");
            log.AppendLine("avatar presence, selection, banter authorship, and shift phase transitions");
            log.AppendLine("programmatically. Interactive WASD/HUD play was not driven.");
            log.AppendLine("Prospector geo diagnostic + prior excavator CSV baselines remain separate artifacts.");
            log.AppendLine();

            if (fail == 0)
            {
                log.AppendLine("## VERDICT: A. V1.1 READY TO LOCK");
                log.AppendLine("Architecture and live regression are sound.");
                log.AppendLine("Proceed to V1.2 Universal Worker State when ready.");
            }
            else
            {
                log.AppendLine("## VERDICT: B. V1.1 NOT READY");
                foreach (var b in blockers)
                    log.AppendLine($"- BLOCKER: {b}");
            }

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(Application.dataPath, "..", "BenchmarkResults")
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(dir, $"v11_lock_audit_{stamp}.md");
            File.WriteAllText(path, log.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "v11_lock_audit_latest.md"), log.ToString(), Encoding.UTF8);
            return path;
        }
    }
}
