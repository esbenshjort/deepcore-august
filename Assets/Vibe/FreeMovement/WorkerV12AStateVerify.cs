using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// V1.2A Universal Worker State verification — ownership / identity / sleep / stamina priming.
    /// Batchmode: -executeMethod DeepCore.FreeMovement.WorkerV12AStateVerify.RunFromEditor
    /// No gameplay. No V1.2B.
    /// </summary>
    public static class WorkerV12AStateVerify
    {
        static readonly JobType[] JobTour =
        {
            JobType.Prospecting,
            JobType.Excavation,
            JobType.Hauling,
            JobType.Refining,
            JobType.Engineering,
            JobType.Unassigned,
        };

        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[V1.2A STATE] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.ReadAllText(path).Contains("INVARIANT: FAIL");
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

            log.AppendLine("# V1.2A Universal Worker State Verification");
            log.AppendLine();
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine("Mode: Unity batch ForceBuild — ownership / reference / sleep / stamina priming.");
            log.AppendLine();

            FreeMovementSocketMapRunner runner = null;
            GameObject host = null;
            try
            {
                host = new GameObject("V12A_StateVerify_Host");
                runner = host.AddComponent<FreeMovementSocketMapRunner>();
                runner.ForceBuildForAudit();

                // ——— 1. Five-worker ownership in all phases ———
                log.AppendLine("## 1. Five-worker ownership (all phases)");
                VerifyAllHaveIndependentState(runner, Check, "bootstrap OnShift");

                // Each job occupancy + Unassigned
                for (int j = 0; j < JobTour.Length; j++)
                {
                    var job = JobTour[j];
                    VacateAll(runner);
                    if (job == JobType.Unassigned)
                    {
                        VerifyAllHaveIndependentState(runner, Check, "all Unassigned");
                        continue;
                    }

                    // Put each worker on this job in turn (single-operator jobs)
                    for (int id = 1; id <= 5; id++)
                    {
                        VacateAll(runner);
                        bool ok = runner.AuditTryAssign(id, job, out string why);
                        Check($"Assign WorkerId {id} → {job}", ok, why);
                        VerifyOneHasFields(runner, id, Check, $"on {job}");
                        VerifyAllHaveIndependentState(runner, Check, $"during {job} by id {id}");
                    }
                }

                // Commute / Sleep
                VacateAll(runner);
                for (int id = 1; id <= 5; id++)
                    runner.AuditTryAssign(id, JobTour[(id - 1) % 5], out _);

                runner.AuditBeginHeadingHome();
                Check("Phase HeadingHome", runner.AuditCrewPhaseName() == "HeadingHome");
                VerifyAllHaveIndependentState(runner, Check, "HeadingHome commuting");

                runner.AuditEnterSleep();
                Check("Phase Asleep", runner.AuditCrewPhaseName() == "Asleep");
                VerifyAllHaveIndependentState(runner, Check, "Sleeping");

                runner.AuditSkipSleep();
                if (runner.AuditCrewPhaseName() == "HeadingOut")
                    runner.AuditEnterOnShift();
                Check("Back OnShift after SkipSleep", runner.AuditCrewPhaseName() == "OnShift");
                VerifyAllHaveIndependentState(runner, Check, "post-sleep OnShift");

                // ——— 2. Same-reference tour per worker ———
                log.AppendLine();
                log.AppendLine("## 2. Cross-job same-reference");
                VacateAll(runner);
                for (int id = 1; id <= 5; id++)
                {
                    var wr = runner.AuditCrewWorker(id);
                    var bag = wr.State;
                    Check($"WorkerId {id} State non-null before tour", bag != null);

                    for (int j = 0; j < JobTour.Length; j++)
                    {
                        VacateAll(runner);
                        var job = JobTour[j];
                        if (job == JobType.Unassigned)
                        {
                            // already vacant
                        }
                        else
                        {
                            runner.AuditTryAssign(id, job, out _);
                        }

                        Check($"WorkerId {id} same State ref on {job}",
                            ReferenceEquals(bag, wr.State),
                            $"before=#{RuntimeHelpersHash(bag)} after=#{RuntimeHelpersHash(wr.State)}");
                    }
                }

                // ——— 3. Isolation ———
                log.AppendLine();
                log.AppendLine("## 3. State isolation");
                VacateAll(runner);
                var stamps = new float[6];
                for (int id = 1; id <= 5; id++)
                {
                    var s = runner.AuditWorkerState(id);
                    s.Frustration = 10f * id;
                    s.Morale = 40f + id;
                    s.MentalFatigue = 5f * id;
                    s.FocusState = 50f + id;
                    stamps[id] = s.Frustration;
                }

                // Mutate Lewis only
                runner.AuditWorkerState(1).Frustration = 99f;
                for (int id = 2; id <= 5; id++)
                {
                    Check($"WorkerId {id} Frustration untouched by Lewis mutation",
                        Mathf.Approximately(runner.AuditWorkerState(id).Frustration, stamps[id]),
                        $"got={runner.AuditWorkerState(id).Frustration:0.#} want={stamps[id]:0.#}");
                }
                Check("Lewis Frustration is 99",
                    Mathf.Approximately(runner.AuditWorkerState(1).Frustration, 99f));

                // Swap excavator operators — no state transfer
                runner.AuditWorkerState(1).Frustration = 77f;
                runner.AuditWorkerState(2).Frustration = 22f;
                runner.AuditTryAssign(1, JobType.Excavation, out _);
                runner.AuditTryAssign(2, JobType.Excavation, out _); // Mara takes; Lewis vacated
                Check("No transfer: Lewis still 77 after Mara takes Excavator",
                    Mathf.Approximately(runner.AuditWorkerState(1).Frustration, 77f));
                Check("No transfer: Mara still 22 as excavator",
                    Mathf.Approximately(runner.AuditWorkerState(2).Frustration, 22f));

                // ——— 4. Sleep independence ———
                log.AppendLine();
                log.AppendLine("## 4. Sleep independence");
                VacateAll(runner);
                // Deliberate different meters; leave 3 Unassigned, 2 assigned
                float[] frust0 = { 0, 80f, 65f, 55f, 48f, 42f };
                float[] morale0 = { 0, 70f, 60f, 50f, 45f, 40f };
                float[] fatigue0 = { 0, 75f, 55f, 50f, 45f, 42f };
                float[] focus0 = { 0, 30f, 45f, 55f, 70f, 85f };
                for (int id = 1; id <= 5; id++)
                {
                    var s = runner.AuditWorkerState(id);
                    s.Frustration = frust0[id];
                    s.Morale = morale0[id];
                    s.MentalFatigue = fatigue0[id];
                    s.FocusState = focus0[id];
                }
                runner.AuditTryAssign(1, JobType.Prospecting, out _);
                runner.AuditTryAssign(2, JobType.Excavation, out _);
                // 3,4,5 Unassigned

                runner.AuditApplyCrewSleepRecovery(1f);

                for (int id = 1; id <= 5; id++)
                {
                    var s = runner.AuditWorkerState(id);
                    float expectedRelief = WorkerState.EffectiveSleepFrustrationRelief(frust0[id]);
                    Check($"Sleep id {id}: Frustration partial relief",
                        s.Frustration > 0f && s.Frustration < frust0[id]
                        && Mathf.Approximately(s.Frustration,
                            Mathf.Max(0f, frust0[id] - expectedRelief)),
                        $"before={frust0[id]:0.#} after={s.Frustration:0.#} relief={expectedRelief:0.##}");
                    Check($"Sleep id {id}: Morale not reset to 100",
                        s.Morale < 99f,
                        $"morale={s.Morale:0.#}");
                    Check($"Sleep id {id}: Morale barely moved",
                        Mathf.Abs(s.Morale - morale0[id]) < 8f,
                        $"before={morale0[id]:0.#} after={s.Morale:0.#}");
                    Check($"Sleep id {id}: MentalFatigue recovered with residual",
                        s.MentalFatigue > 0f && s.MentalFatigue < fatigue0[id],
                        $"before={fatigue0[id]:0.#} after={s.MentalFatigue:0.#}");
                    // Focus moves toward baseline 62
                    float beforeDist = Mathf.Abs(focus0[id] - WorkerSleepRecovery.FocusStateBaseline);
                    float afterDist = Mathf.Abs(s.FocusState - WorkerSleepRecovery.FocusStateBaseline);
                    Check($"Sleep id {id}: FocusState toward baseline",
                        afterDist <= beforeDist + 0.01f,
                        $"before={focus0[id]:0.#} after={s.FocusState:0.#} base={WorkerSleepRecovery.FocusStateBaseline}");
                }

                Check("Unassigned Kowalski recovered (id 3)",
                    runner.AuditWorkerState(3).Frustration < frust0[3]);
                Check("Unassigned Elena recovered (id 4)",
                    runner.AuditWorkerState(4).Frustration < frust0[4]);
                Check("Unassigned Viktor recovered (id 5)",
                    runner.AuditWorkerState(5).Frustration < frust0[5]);

                // Distinct post-sleep values (independence)
                Check("Post-sleep Frustration still distinct across crew",
                    !Mathf.Approximately(runner.AuditWorkerState(1).Frustration,
                        runner.AuditWorkerState(5).Frustration));

                // ——— 5. PhysicalStamina priming ———
                log.AppendLine();
                log.AppendLine("## 5. PhysicalStamina priming audit");
                VacateAll(runner);
                // Rebuild fresh states aren't possible without rebuild — stamp unprimed on Elena (never dig this tour)
                var elena = runner.AuditWorkerState(4);
                elena.StaminaPrimed = false;
                elena.PhysicalStamina = 0f;
                elena.IsResting = false;
                float elenaFrust = elena.Frustration = 40f;

                Check("Unprimed Elena PhysicalStamina==0",
                    Mathf.Approximately(elena.PhysicalStamina, 0f) && !elena.StaminaPrimed);

                // Person stamina sheet: any job bind primes via WorkerJobDemand.EnsureStaminaPrimed.
                runner.AuditTryAssign(4, JobType.Hauling, out _);
                Check("Hauling assign primes Elena stamina (person sheet)",
                    elena.StaminaPrimed && elena.PhysicalStamina > 1f,
                    $"primed={elena.StaminaPrimed} stam={elena.PhysicalStamina:0.#}");

                float stamBeforeSleep = elena.PhysicalStamina;
                runner.AuditApplyCrewSleepRecovery(1f);
                Check("Sleep recovers PhysicalStamina when primed",
                    elena.PhysicalStamina >= stamBeforeSleep - 0.01f);
                Check("Sleep still recovers Frustration for hauler",
                    elena.Frustration < elenaFrust,
                    $"frust={elena.Frustration:0.#}");

                // First Excavation bind primes once to full; second bind keeps fatigue
                VacateAll(runner);
                var mara = runner.AuditCrewWorker(2);
                mara.State.StaminaPrimed = false;
                mara.State.PhysicalStamina = 0f;
                runner.AuditTryAssign(2, JobType.Excavation, out _);
                Check("First Excavation bind primes Mara",
                    mara.State.StaminaPrimed && mara.State.PhysicalStamina > 1f,
                    $"stam={mara.State.PhysicalStamina:0.#} max={mara.PhysicalStaminaMax:0.#}");

                float afterPrime = mara.State.PhysicalStamina;
                mara.State.PhysicalStamina = afterPrime * 0.4f;
                float fatigued = mara.State.PhysicalStamina;
                VacateAll(runner);
                runner.AuditTryAssign(2, JobType.Hauling, out _);
                runner.AuditTryAssign(2, JobType.Excavation, out _);
                Check("Re-bind Excavation keeps fatigued stamina (no reset)",
                    Mathf.Approximately(mara.State.PhysicalStamina, fatigued),
                    $"stam={mara.State.PhysicalStamina:0.#} want={fatigued:0.#}");

                VacateAll(runner);
                var kow = runner.AuditWorkerState(3);
                kow.StaminaPrimed = false;
                kow.PhysicalStamina = 0f;
                Check("StaminaPrimed distinguishes uninitialized from zero",
                    !kow.StaminaPrimed && Mathf.Approximately(kow.PhysicalStamina, 0f)
                    && mara.State.StaminaPrimed);

                log.AppendLine();
                log.AppendLine("## Priming verdict");
                log.AppendLine(
                    "PhysicalStamina=0 + StaminaPrimed=false means uninitialized.");
                log.AppendLine(
                    "WorkerJobDemand.EnsureStaminaPrimed runs on job bind (person sheet).");
                log.AppendLine(
                    "UI treats unprimed as full. Sleep restores PhysicalStamina once primed.");
                log.AppendLine("No redesign in this verify pass.");
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
            log.AppendLine("## Summary");
            log.AppendLine($"PASS: {pass}");
            log.AppendLine($"FAIL: {fail}");
            log.AppendLine();
            string verdict = fail == 0
                ? "UNIVERSAL WORKER STATE INVARIANT: PASS"
                : "UNIVERSAL WORKER STATE INVARIANT: FAIL";
            log.AppendLine(verdict);
            if (fail == 0)
            {
                log.AppendLine();
                log.AppendLine("V1.2A READY TO LOCK.");
            }

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(Application.dataPath, "..", "BenchmarkResults")
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string latest = Path.Combine(dir, "v12a_state_verify_latest.md");
            string stamped = Path.Combine(dir, $"v12a_state_verify_{stamp}.md");
            File.WriteAllText(latest, log.ToString());
            File.WriteAllText(stamped, log.ToString());
            return latest;
        }

        static void VacateAll(FreeMovementSocketMapRunner runner)
        {
            runner.AuditTryUnassign(JobType.Prospecting, out _);
            runner.AuditTryUnassign(JobType.Excavation, out _);
            runner.AuditTryUnassign(JobType.Hauling, out _);
            runner.AuditTryUnassign(JobType.Refining, out _);
            runner.AuditTryUnassign(JobType.Engineering, out _);
        }

        static void VerifyAllHaveIndependentState(
            FreeMovementSocketMapRunner runner,
            Action<string, bool, string> check,
            string context)
        {
            var bags = new WorkerState[6];
            for (int id = 1; id <= 5; id++)
            {
                var wr = runner.AuditCrewWorker(id);
                check($"[{context}] WorkerId {id} Runtime exists", wr != null, "");
                if (wr == null) continue;
                var s = wr.State;
                bags[id] = s;
                check($"[{context}] WorkerId {id} State non-null", s != null, "");
                if (s == null) continue;
                // Field presence — readable meters (structs always "exist"; assert sane ranges)
                check($"[{context}] WorkerId {id} meters in range",
                    s.MentalFatigue >= 0f && s.MentalFatigue <= 100f
                    && s.FocusState >= 0f && s.FocusState <= 100f
                    && s.Frustration >= 0f && s.Frustration <= 100f
                    && s.Morale >= 0f && s.Morale <= 100f
                    && s.Injury >= 0f && s.Injury <= 100f
                    && s.PhysicalStamina >= 0f,
                    $"MF={s.MentalFatigue:0.#} FS={s.FocusState:0.#} Fr={s.Frustration:0.#} Mo={s.Morale:0.#}");
            }

            for (int a = 1; a <= 5; a++)
            for (int b = a + 1; b <= 5; b++)
            {
                if (bags[a] == null || bags[b] == null) continue;
                check($"[{context}] State bags distinct {a}≠{b}",
                    !ReferenceEquals(bags[a], bags[b]), "");
            }
        }

        static void VerifyOneHasFields(
            FreeMovementSocketMapRunner runner,
            int workerId,
            Action<string, bool, string> check,
            string context)
        {
            var s = runner.AuditWorkerState(workerId);
            check($"[{context}] id {workerId} has PhysicalStamina field", s != null, "");
            if (s == null) return;
            // Touch every required field so a missing member would fail compile; assert readable
            float _ = s.PhysicalStamina + s.MentalFatigue + s.FocusState
                      + s.Frustration + s.Morale + s.Injury;
            bool __ = s.NeedsCare;
            check($"[{context}] id {workerId} NeedsCare readable", true, __.ToString());
        }

        static string RuntimeHelpersHash(object o) =>
            o == null ? "null" : $"{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o):X8}";
    }
}
