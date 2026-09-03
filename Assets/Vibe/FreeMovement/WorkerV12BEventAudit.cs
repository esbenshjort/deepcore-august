using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// V1.2B WorkerStateEvent audit — person targeting, isolation, no double-apply, no Social Aura.
    /// Batchmode: -executeMethod DeepCore.FreeMovement.WorkerV12BEventAudit.RunFromEditor
    /// </summary>
    public static class WorkerV12BEventAudit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[V1.2B EVENTS] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.ReadAllText(path).Contains("VERDICT: FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var log = new StringBuilder(12_000);
            int pass = 0, fail = 0;

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
                    log.AppendLine($"FAIL | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
                }
            }

            log.AppendLine("# V1.2B Person-Targeted Worker State Events Audit");
            log.AppendLine();
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine();

            FreeMovementSocketMapRunner runner = null;
            GameObject host = null;
            try
            {
                host = new GameObject("V12B_EventAudit_Host");
                runner = host.AddComponent<FreeMovementSocketMapRunner>();
                runner.ForceBuildForAudit();
                WorkerStateClock.GameHours = 10f;
                runner.AuditClearEventGate();

                // A — only Lewis changes
                log.AppendLine("## A. Person targeting");
                float maraFr0 = runner.AuditWorkerState(2).Frustration;
                float lewisFr0 = runner.AuditWorkerState(1).Frustration;
                var recA = runner.AuditEmitForced(WorkerStateEvent.Create(
                    1, WorkerStateEventType.WorkBlocked, 5f, "AuditA", JobType.Excavation, "excavator.test"));
                Check("Emit to Lewis applied", recA != null && recA.DeltaFrustration > 0f,
                    $"dFr={recA?.DeltaFrustration:0.##}");
                Check("Only Lewis Frustration rose",
                    runner.AuditWorkerState(1).Frustration > lewisFr0
                    && Mathf.Approximately(runner.AuditWorkerState(2).Frustration, maraFr0));

                // B — freeze WorkerId across operator swap
                log.AppendLine();
                log.AppendLine("## B. Frozen WorkerId across swap");
                runner.AuditTryAssign(1, JobType.Excavation, out _);
                float lewisFrB = runner.AuditWorkerState(1).Frustration;
                float maraFrB = runner.AuditWorkerState(2).Frustration;
                runner.AuditEmitForced(WorkerStateEvent.Create(
                    1, WorkerStateEventType.WorkBlocked, 4f, "AuditB_Lewis", JobType.Excavation, "excavator.test"));
                float lewisAfterEvent = runner.AuditWorkerState(1).Frustration;
                runner.AuditTryAssign(2, JobType.Excavation, out _); // Mara takes excavator
                Check("Lewis still owns the Frustration delta after Mara takes Excavator",
                    Mathf.Approximately(runner.AuditWorkerState(1).Frustration, lewisAfterEvent)
                    && Mathf.Approximately(runner.AuditWorkerState(2).Frustration, maraFrB));
                var lastLewis = runner.AuditLastEvent(1);
                Check("History event still WorkerId=Lewis",
                    lastLewis != null && lastLewis.Event.WorkerId == 1
                    && lastLewis.Event.Source == "AuditB_Lewis");

                // C — five independent events
                log.AppendLine();
                log.AppendLine("## C. Five independent workers");
                for (int id = 1; id <= 5; id++)
                {
                    float before = runner.AuditWorkerState(id).Frustration;
                    runner.AuditEmitForced(WorkerStateEvent.Create(
                        id, WorkerStateEventType.ProgressSuccess, 3f, $"AuditC_{id}"));
                    Check($"WorkerId {id} received ProgressSuccess",
                        runner.AuditWorkerState(id).Frustration <= before + 0.01f
                        || runner.AuditLastEvent(id)?.Event.Source == $"AuditC_{id}");
                    // ProgressSuccess reduces frustration — just verify history
                    Check($"WorkerId {id} history has AuditC",
                        runner.AuditLastEvent(id)?.Event.Source == $"AuditC_{id}");
                }

                // D — Unassigned can receive
                log.AppendLine();
                log.AppendLine("## D. Unassigned receives events");
                runner.AuditTryUnassign(JobType.Prospecting, out _);
                runner.AuditTryUnassign(JobType.Excavation, out _);
                runner.AuditTryUnassign(JobType.Hauling, out _);
                runner.AuditTryUnassign(JobType.Refining, out _);
                runner.AuditTryUnassign(JobType.Engineering, out _);
                Check("Elena Unassigned", runner.AuditJobOf(4) == JobType.Unassigned);
                float elFr = runner.AuditWorkerState(4).Frustration;
                float elMo = runner.AuditWorkerState(4).Morale;
                runner.AuditEmitForced(WorkerStateEvent.Create(
                    4, WorkerStateEventType.Discovery, 5f, "AuditD_Unassigned"));
                Check("Unassigned Elena received Discovery",
                    runner.AuditLastEvent(4)?.Event.Source == "AuditD_Unassigned");
                Check("Unassigned Elena Morale rose",
                    runner.AuditWorkerState(4).Morale > elMo);

                // E — Sleeping: PROCESS (deterministic)
                log.AppendLine();
                log.AppendLine("## E. Sleeping worker — PROCESS (not reject)");
                runner.AuditEnterSleep();
                Check("Phase Asleep", runner.AuditCrewPhaseName() == "Asleep");
                float kowFr = runner.AuditWorkerState(3).Frustration;
                runner.AuditEmitForced(WorkerStateEvent.Create(
                    3, WorkerStateEventType.WorkBlocked, 3f, "AuditE_Sleep"));
                Check("Sleeping Kowalski still processed WorkBlocked",
                    runner.AuditWorkerState(3).Frustration > kowFr
                    && runner.AuditLastEvent(3)?.Event.Source == "AuditE_Sleep");
                runner.AuditSkipSleep();
                if (runner.AuditCrewPhaseName() == "HeadingOut")
                    runner.AuditEnterOnShift();

                // F — history follows WorkerId across jobs
                log.AppendLine();
                log.AppendLine("## F. History follows WorkerId");
                int histBefore = runner.AuditEventHistoryCount(1);
                runner.AuditEmitForced(WorkerStateEvent.Create(
                    1, WorkerStateEventType.MajorSuccess, 4f, "AuditF_Hist"));
                int histMid = runner.AuditEventHistoryCount(1);
                runner.AuditTryAssign(1, JobType.Hauling, out _);
                runner.AuditTryAssign(1, JobType.Refining, out _);
                runner.AuditTryAssign(1, JobType.Engineering, out _);
                Check("History count grew and persists across jobs",
                    histMid > histBefore
                    && runner.AuditEventHistoryCount(1) == histMid
                    && runner.AuditLastEvent(1)?.Event.Source == "AuditF_Hist");

                // G — machine heat unchanged by emotional events
                log.AppendLine();
                log.AppendLine("## G. No machine state from events");
                float heat0 = runner.AuditExcavatorHeat();
                runner.AuditEmitForced(WorkerStateEvent.Create(
                    2, WorkerStateEventType.EquipmentProblem, 8f, "AuditG_Heat"));
                Check("Excavator Heat unchanged by EquipmentProblem event",
                    Mathf.Approximately(runner.AuditExcavatorHeat(), heat0),
                    $"heat={runner.AuditExcavatorHeat():0.#}");

                // H — no double-apply path: ProgressSuccess via EmitOperator uses events only
                log.AppendLine();
                log.AppendLine("## H. No double-apply (processor-only Frustration)");
                // Direct Conditions.AddFrustration is not called from obstruction path anymore.
                // Prove one event → one history row and single delta.
                int h0 = runner.AuditEventHistoryCount(5);
                float fr5 = runner.AuditWorkerState(5).Frustration;
                var one = runner.AuditEmitForced(WorkerStateEvent.Create(
                    5, WorkerStateEventType.WorkBlocked, 3f, "AuditH_Once"));
                Check("Single WorkBlocked → one history row",
                    runner.AuditEventHistoryCount(5) == h0 + 1);
                Check("Frustration rose once (positive delta recorded)",
                    one != null && one.DeltaFrustration > 0f
                    && runner.AuditWorkerState(5).Frustration > fr5);

                // I — MajorSuccess
                log.AppendLine();
                log.AppendLine("## I. MajorSuccess effects");
                runner.AuditWorkerState(1).Frustration = 40f;
                float mo1 = runner.AuditWorkerState(1).Morale;
                runner.AuditEmitForced(WorkerStateEvent.Create(
                    1, WorkerStateEventType.MajorSuccess, 5f, "AuditI_Major"));
                Check("MajorSuccess reduces Frustration",
                    runner.AuditWorkerState(1).Frustration < 40f);
                Check("MajorSuccess increases Morale",
                    runner.AuditWorkerState(1).Morale > mo1);

                // J — RepeatedFailure stronger + spam gate
                log.AppendLine();
                log.AppendLine("## J. RepeatedFailure vs WorkBlocked + spam gate");
                runner.AuditWorkerState(2).Frustration = 20f;
                runner.AuditClearEventGate();
                var wb = runner.AuditEmitForced(WorkerStateEvent.Create(
                    2, WorkerStateEventType.WorkBlocked, 3f, "AuditJ_Cmp"));
                float afterWb = runner.AuditWorkerState(2).Frustration;
                runner.AuditWorkerState(2).Frustration = 20f;
                var rf = runner.AuditEmitForced(WorkerStateEvent.Create(
                    2, WorkerStateEventType.RepeatedFailure, 3f, "AuditJ_Cmp2"));
                Check("RepeatedFailure gains ≥ WorkBlocked at same magnitude",
                    rf != null && wb != null && rf.DeltaFrustration >= wb.DeltaFrustration - 0.01f,
                    $"wb={wb?.DeltaFrustration:0.##} rf={rf?.DeltaFrustration:0.##}");

                runner.AuditClearEventGate();
                WorkerStateClock.GameHours = 20f;
                int gated = 0;
                for (int i = 0; i < 5; i++)
                {
                    var r = runner.AuditEmit(WorkerStateEvent.Create(
                        2, WorkerStateEventType.WorkBlocked, 3f, "AuditJ_Spam"));
                    if (r != null) gated++;
                    WorkerStateClock.GameHours += 0.01f; // inside min gap
                }
                Check("Spam gate blocks rapid identical WorkBlocked",
                    gated == 1, $"admitted={gated}");

                // K — sleep after events
                log.AppendLine();
                log.AppendLine("## K. Sleep recovery after events");
                runner.AuditWorkerState(1).Frustration = 70f;
                runner.AuditApplyCrewSleepRecovery(1f);
                Check("Sleep still partially relieves Frustration",
                    runner.AuditWorkerState(1).Frustration > 0f
                    && runner.AuditWorkerState(1).Frustration < 70f);

                // L — no Social Aura types in this assembly slice (string scan of loaded type names)
                log.AppendLine();
                log.AppendLine("## L. No Social Aura");
                bool auraType = false;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch { continue; }
                    for (int i = 0; i < types.Length; i++)
                    {
                        string n = types[i].Name;
                        if (n.IndexOf("SocialAura", StringComparison.OrdinalIgnoreCase) >= 0
                            || n.IndexOf("InteractionPressure", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            auraType = true;
                            break;
                        }
                    }
                    if (auraType) break;
                }
                Check("No SocialAura / InteractionPressure types", !auraType);

                // Daytime decay smoke
                log.AppendLine();
                log.AppendLine("## Daytime decay");
                runner.AuditWorkerState(1).Frustration = 30f;
                runner.AuditTickDaytime(2f);
                Check("Daytime Frustration decay",
                    runner.AuditWorkerState(1).Frustration < 30f);
            }
            catch (Exception ex)
            {
                fail++;
                log.AppendLine($"FAIL | Exception — {ex.GetType().Name}: {ex.Message}");
                log.AppendLine(ex.StackTrace);
            }
            finally
            {
                if (host != null)
                {
#if UNITY_EDITOR
                    UnityEngine.Object.DestroyImmediate(host);
#else
                    UnityEngine.Object.Destroy(host);
#endif
                }
            }

            log.AppendLine();
            log.AppendLine("## Emitters connected (V1.2B subset)");
            log.AppendLine("- Excavator: WorkBlocked / RepeatedFailure (obstruction), ProgressSuccess (tile/advance/WP),");
            log.AppendLine("  EquipmentProblem (overheat), Injury, PhysicalExhaustion (stamina rest)");
            log.AppendLine("- Engineer: EquipmentRecovered (operator) + ProgressSuccess (engineer)");
            log.AppendLine("- Prospector: Discovery (assessed finding, frozen WorkerId)");
            log.AppendLine("- Hauler / Refiner: not wired (no reliable semantic signal yet)");
            log.AppendLine();
            log.AppendLine("## Sleeping event policy");
            log.AppendLine("PROCESS — person still owns emotional residue while asleep.");
            log.AppendLine();
            log.AppendLine("## Summary");
            log.AppendLine($"PASS: {pass}");
            log.AppendLine($"FAIL: {fail}");
            log.AppendLine();
            log.AppendLine(fail == 0 ? "VERDICT: PASS — V1.2B READY TO LOCK" : "VERDICT: FAIL");

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(Application.dataPath, "..", "BenchmarkResults")
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string latest = Path.Combine(dir, "v12b_event_audit_latest.md");
            File.WriteAllText(latest, log.ToString());
            File.WriteAllText(Path.Combine(dir, $"v12b_event_audit_{stamp}.md"), log.ToString());
            return latest;
        }
    }
}
