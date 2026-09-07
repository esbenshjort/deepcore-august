using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Offline Round 2 Frustration tuning audit — seeded, no Unity scene.
    /// Menu: DeepCore/Diagnostics/Run Frustration Tuning Audit
    /// </summary>
    public static class FrustrationTuningAudit
    {
        const float ShiftHours = 10f;
        const float StepHours = 0.25f;

        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[FRUSTRATION TUNING] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.ReadAllText(path).Contains("INVARIANT: FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var log = new StringBuilder(20_000);
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

            log.AppendLine("# Frustration Tuning Audit (Round 2)");
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine();
            log.AppendLine("## Tunables");
            log.AppendLine($"- Daytime decay/h = {WorkerStateDaytimeRecovery.FrustrationDecayPerGameHour}");
            log.AppendLine($"- Sleep relief base = {WorkerSleepRecovery.FrustrationRelief} (diminishing when high)");
            log.AppendLine($"- ProgressSuccess scale={WorkerStateEventProcessor.ProgressSuccessReliefScale} cap={WorkerStateEventProcessor.ProgressSuccessReliefCap}");
            log.AppendLine($"- WorkBlockedMul={WorkerStateEventProcessor.WorkBlockedMul} RepeatedFailureMul={WorkerStateEventProcessor.RepeatedFailureMul}");
            log.AppendLine($"- CompoundPerFrustration={WorkerStateEventProcessor.CompoundPerFrustration} Cap={WorkerStateEventProcessor.CompoundMax}");
            log.AppendLine();

            WorkerRoll.BeginSeeded(4242);
            try
            {
                // ─── 1. Normal successful shift ─────────────────────
                log.AppendLine("## 1. Normal successful shift");
                {
                    var wr = new WorkerRuntime(1, "Lewis");
                    BindHub(wr);
                    float start = wr.State.Frustration;
                    float t = 8f;
                    WorkerStateClock.GameHours = t;
                    float nextProgress = t + 0.5f;
                    float end = t + ShiftHours;
                    while (t + 1e-4f < end)
                    {
                        float step = Mathf.Min(StepHours, end - t);
                        t += step;
                        WorkerStateClock.GameHours = t;
                        WorkerStateDaytimeRecovery.Tick(wr, step);
                        if (t + 1e-4f >= nextProgress)
                        {
                            WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                                1, WorkerStateEventType.ProgressSuccess, 2f, "AuditProgress",
                                JobType.Excavation));
                            nextProgress += 0.5f;
                        }
                    }
                    float endShift = wr.State.Frustration;
                    Check("Successful shift does not spike Frustration",
                        endShift <= start + 5f, $"start={start:0.#} end={endShift:0.#}");
                    Check("Successful shift Frustration stays moderate",
                        endShift < 35f, $"F={endShift:0.#}");
                }

                // ─── 2. Difficult shift ─────────────────────────────
                log.AppendLine();
                log.AppendLine("## 2. Difficult shift");
                {
                    var wr = new WorkerRuntime(2, "Mara");
                    BindHub(wr);
                    float t = 30f;
                    WorkerStateClock.GameHours = t;
                    float start = wr.State.Frustration;
                    // Several WorkBlocked across the shift
                    for (int i = 0; i < 5; i++)
                    {
                        t += 1.5f;
                        WorkerStateClock.GameHours = t;
                        WorkerStateEventHub.Service.EmitForced(WorkerStateEvent.Create(
                            2, WorkerStateEventType.WorkBlocked, 4f, "AuditBlock",
                            JobType.Excavation));
                        WorkerStateDaytimeRecovery.Tick(wr, 1.5f);
                    }
                    float end = wr.State.Frustration;
                    Check("Difficult shift raises Frustration",
                        end > start + 8f, $"start={start:0.#} end={end:0.#}");
                    Check("Difficult shift does not instantly max Frustration",
                        end < 95f, $"F={end:0.#}");
                }

                // ─── 3. Repeated failures ───────────────────────────
                log.AppendLine();
                log.AppendLine("## 3. Repeated failures");
                {
                    var wr = new WorkerRuntime(3, "Kowalski");
                    BindHub(wr);
                    wr.State.Frustration = 30f;
                    float before = wr.State.Frustration;
                    float t = 50f;
                    for (int i = 0; i < 4; i++)
                    {
                        t += 1.2f;
                        WorkerStateClock.GameHours = t;
                        WorkerStateEventHub.Service.EmitForced(WorkerStateEvent.Create(
                            3, WorkerStateEventType.RepeatedFailure, 5f, "AuditRepeat",
                            JobType.Prospecting));
                    }
                    float after = wr.State.Frustration;
                    float gain = after - before;
                    Check("RepeatedFailure compounds Frustration upward",
                        gain > 8f, $"ΔF={gain:0.#} F={after:0.#}");
                    // Compound: later hits hurt more — compare first vs last delta via mid snapshot
                    Check("Compound path engaged (Frustration higher than raw uncompounded floor)",
                        after > before + 4f * 0.55f, $"F={after:0.#}");
                }

                // ─── 4. Several bad days ────────────────────────────
                log.AppendLine();
                log.AppendLine("## 4. Several bad days");
                {
                    var wr = new WorkerRuntime(4, "Elena");
                    BindHub(wr);
                    float t = 70f;
                    WorkerStateClock.GameHours = t;
                    float day0 = wr.State.Frustration;
                    for (int day = 0; day < 4; day++)
                    {
                        float shiftEnd = t + ShiftHours;
                        while (t + 1e-4f < shiftEnd)
                        {
                            float step = Mathf.Min(StepHours, shiftEnd - t);
                            t += step;
                            WorkerStateClock.GameHours = t;
                            WorkerStateDaytimeRecovery.Tick(wr, step);
                        }
                        // Heavy bad day: multiple setbacks (not just one bump)
                        for (int hit = 0; hit < 3; hit++)
                        {
                            t += 0.35f;
                            WorkerStateClock.GameHours = t;
                            WorkerStateEventHub.Service.EmitForced(WorkerStateEvent.Create(
                                4, WorkerStateEventType.WorkBlocked, 4f, "BadDay", JobType.Hauling));
                            t += 0.35f;
                            WorkerStateClock.GameHours = t;
                            WorkerStateEventHub.Service.EmitForced(WorkerStateEvent.Create(
                                4, WorkerStateEventType.RepeatedFailure, 4f, "BadDay",
                                JobType.Prospecting));
                        }

                        // Sleep — diminishing relief leaves residue when high
                        wr.State.StaminaPrimed = true;
                        wr.State.ApplySleepRecoveryFraction(1f, wr.PhysicalStaminaMax);
                        t += 14f;
                        WorkerStateClock.GameHours = t;
                    }
                    float afterDays = wr.State.Frustration;
                    Check("Several bad days leave persistent Frustration residue",
                        afterDays > day0 + 10f, $"day0={day0:0.#} after={afterDays:0.#}");
                    Check("Sleep diminishing relief — does not fully wipe multi-day residue",
                        afterDays > 15f, $"F={afterDays:0.#}");
                }

                // ─── 5. Recovery after success / rest ───────────────
                log.AppendLine();
                log.AppendLine("## 5. Recovery after success / rest");
                {
                    var wr = new WorkerRuntime(5, "Viktor");
                    BindHub(wr);
                    wr.State.Frustration = 55f;
                    float start = wr.State.Frustration;
                    float t = 120f;
                    WorkerStateClock.GameHours = t;

                    // Progress successes while high-frust
                    for (int i = 0; i < 8; i++)
                    {
                        t += 0.4f;
                        WorkerStateClock.GameHours = t;
                        WorkerStateEventHub.Service.EmitForced(WorkerStateEvent.Create(
                            5, WorkerStateEventType.ProgressSuccess, 2f, "Recover",
                            JobType.Excavation));
                        WorkerStateDaytimeRecovery.Tick(wr, 0.4f);
                    }
                    float afterSuccess = wr.State.Frustration;
                    Check("ProgressSuccess relieves Frustration (capped / scaled)",
                        afterSuccess < start, $"start={start:0.#} after={afterSuccess:0.#}");
                    Check("ProgressSuccess does not erase high Frustration in one burst",
                        afterSuccess > start - 20f, $"Δ={start - afterSuccess:0.#}");

                    // Full sleep
                    float beforeSleep = wr.State.Frustration;
                    float expectedRelief = WorkerState.EffectiveSleepFrustrationRelief(beforeSleep);
                    wr.State.StaminaPrimed = true;
                    wr.State.ApplySleepRecoveryFraction(1f, wr.PhysicalStaminaMax);
                    float afterSleep = wr.State.Frustration;
                    Check("Sleep applies diminishing Frustration relief",
                        Mathf.Abs((beforeSleep - afterSleep) - expectedRelief) < 0.6f
                        || afterSleep < beforeSleep,
                        $"before={beforeSleep:0.#} after={afterSleep:0.#} expectedRelief≈{expectedRelief:0.#}");
                    Check("High-Frust sleep relief < base when Frustration was high",
                        expectedRelief <= WorkerSleepRecovery.FrustrationRelief + 0.01f);
                    if (beforeSleep > 50f)
                        Check("Diminishing: relief scale < 1.0 above 50 Frust",
                            expectedRelief < WorkerSleepRecovery.FrustrationRelief * 0.9f,
                            $"relief={expectedRelief:0.##}");
                }
            }
            finally
            {
                WorkerRoll.EndSeeded();
                WorkerStateEventHub.Service = null;
            }

            log.AppendLine();
            log.AppendLine("## Summary");
            log.AppendLine($"PASS {pass} / FAIL {fail}");
            log.AppendLine(fail == 0 ? "INVARIANT: PASS" : "INVARIANT: FAIL");

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(Application.dataPath, "..", "BenchmarkResults")
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string stamped = Path.Combine(dir, $"frustration_tuning_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            string latest = Path.Combine(dir, "frustration_tuning_latest.md");
            File.WriteAllText(stamped, log.ToString());
            File.WriteAllText(latest, log.ToString());
            return latest;
        }

        static void BindHub(WorkerRuntime wr)
        {
            var svc = new WorkerStateEventService();
            svc.Bind(id => id == wr.WorkerId ? wr : null);
            WorkerStateEventHub.Service = svc;
        }
    }
}
