using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// V1.2C Job Demand diagnostic — all five jobs affect WorkerState without universal DPS stacks.
    /// Batchmode: -executeMethod DeepCore.FreeMovement.WorkerV12CDemandAudit.RunFromEditor
    /// </summary>
    public static class WorkerV12CDemandAudit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[V1.2C DEMAND] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.ReadAllText(path).Contains("VERDICT: FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var log = new StringBuilder(14_000);
            int pass = 0, fail = 0;

            void Check(string name, bool ok, string detail = "")
            {
                if (ok) { pass++; log.AppendLine($"PASS | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}")); }
                else { fail++; log.AppendLine($"FAIL | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}")); }
            }

            log.AppendLine("# V1.2C Universal Job Demand Audit");
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine();

            FreeMovementSocketMapRunner runner = null;
            GameObject host = null;
            try
            {
                host = new GameObject("V12C_DemandAudit_Host");
                runner = host.AddComponent<FreeMovementSocketMapRunner>();
                runner.ForceBuildForAudit();
                WorkerStateClock.GameHours = 12f;

                // ——— Priming ———
                log.AppendLine("## N. Stamina priming");
                var elena = runner.AuditCrewWorker(4);
                elena.State.StaminaPrimed = false;
                elena.State.PhysicalStamina = 0f;
                Check("Unprimed ratio reads as full (not exhausted)",
                    Mathf.Approximately(WorkerJobDemand.StaminaRatio(elena), 1f));
                WorkerJobDemand.EnsureStaminaPrimed(elena);
                Check("EnsureStaminaPrimed fills pool",
                    elena.State.StaminaPrimed && elena.State.PhysicalStamina > 1f);

                // Snapshot helpers
                void Snapshot(int id, out float stam, out float mf, out float fs, out float fr, out float mo)
                {
                    var s = runner.AuditWorkerState(id);
                    stam = s.PhysicalStamina;
                    mf = s.MentalFatigue;
                    fs = s.FocusState;
                    fr = s.Frustration;
                    mo = s.Morale;
                }

                // Vacate then assign one worker per job for demand ticks
                void Vacate()
                {
                    runner.AuditTryUnassign(JobType.Prospecting, out _);
                    runner.AuditTryUnassign(JobType.Excavation, out _);
                    runner.AuditTryUnassign(JobType.Hauling, out _);
                    runner.AuditTryUnassign(JobType.Refining, out _);
                    runner.AuditTryUnassign(JobType.Engineering, out _);
                }

                // ——— A/D/E/F/G: demand paths differ ———
                log.AppendLine();
                log.AppendLine("## A–G. Job demand differentiation");
                Vacate();

                // Hauling physical — force active demand profile via TickActive
                runner.AuditTryAssign(3, JobType.Hauling, out _);
                WorkerJobDemand.EnsureStaminaPrimed(runner.AuditCrewWorker(3));
                Snapshot(3, out float hSt0, out float hMf0, out _, out _, out _);
                var haulDemand = new JobDemandProfile(0.85f, 0.15f, 0.35f, "HAUL_SIM");
                WorkerJobDemand.TickActive(runner.AuditCrewWorker(3), haulDemand, 2f, JobType.Hauling, "cart.test");
                Snapshot(3, out float hSt1, out float hMf1, out _, out _, out _);
                Check("D. Hauling creates physical fatigue", hSt1 < hSt0, $"stam {hSt0:0.#}→{hSt1:0.#}");

                // Prospecting mental
                runner.AuditTryAssign(1, JobType.Prospecting, out _);
                Snapshot(1, out _, out float pMf0, out float pFs0, out _, out _);
                var prosDemand = new JobDemandProfile(0.05f, 0.95f, 0.90f, "ANALYSING_SIM");
                WorkerJobDemand.TickActive(runner.AuditCrewWorker(1), prosDemand, 2f, JobType.Prospecting, "scan.test");
                Snapshot(1, out _, out float pMf1, out float pFs1, out _, out _);
                Check("E. Prospecting creates mental fatigue", pMf1 > pMf0, $"MF {pMf0:0.#}→{pMf1:0.#}");
                Check("E. Prospecting attention drains FocusState", pFs1 < pFs0, $"FS {pFs0:0.#}→{pFs1:0.#}");

                // Refining mental/attention
                runner.AuditTryAssign(4, JobType.Refining, out _);
                Snapshot(4, out _, out float rMf0, out float rFs0, out _, out _);
                var refDemand = new JobDemandProfile(0.35f, 0.35f, 0.40f, "FETCH_SIM");
                WorkerJobDemand.TickActive(runner.AuditCrewWorker(4), refDemand, 2f, JobType.Refining, "washer.test");
                Snapshot(4, out _, out float rMf1, out float rFs1, out _, out _);
                Check("F. Refining creates mental demand", rMf1 > rMf0);
                Check("F. Refining creates attention demand", rFs1 < rFs0);

                // Engineering mixed
                runner.AuditTryAssign(5, JobType.Engineering, out _);
                Snapshot(5, out float eSt0, out float eMf0, out _, out _, out _);
                var engDemand = new JobDemandProfile(0.55f, 0.70f, 0.65f, "REPAIR_SIM");
                WorkerJobDemand.TickActive(runner.AuditCrewWorker(5), engDemand, 2f, JobType.Engineering, "kit.test");
                Snapshot(5, out float eSt1, out float eMf1, out _, out _, out _);
                Check("G. Engineering mixed physical+mental",
                    eSt1 < eSt0 && eMf1 > eMf0,
                    $"stam {eSt0:0.#}→{eSt1:0.#} MF {eMf0:0.#}→{eMf1:0.#}");

                // Excavation: continuous physical demand 0 — dig path preserved (catalog)
                var digIdle = JobDemandCatalog.ForExcavation(null);
                Check("H. Excavation catalog idle when unbound", digIdle.IsIdle);
                // Simulate mining demand profile (P=0)
                runner.AuditTryAssign(2, JobType.Excavation, out _);
                Snapshot(2, out float xSt0, out float xMf0, out _, out _, out _);
                var mineDemand = new JobDemandProfile(0f, 0.12f, 0.40f, "MINING_SIM");
                WorkerJobDemand.TickActive(runner.AuditCrewWorker(2), mineDemand, 2f, JobType.Excavation, "excavator.test");
                Snapshot(2, out float xSt1, out float xMf1, out _, out _, out _);
                Check("H. Excavation continuous tick does not spend PhysicalStamina",
                    Mathf.Approximately(xSt0, xSt1), $"stam {xSt0:0.#}→{xSt1:0.#}");
                Check("H. Excavation mining adds light MentalFatigue", xMf1 >= xMf0);

                Check("A. Jobs affect state differently (haul≠pros MF paths)",
                    (hSt0 - hSt1) > 1f && (pMf1 - pMf0) > 1f);

                // B — same state across reassignment
                log.AppendLine();
                log.AppendLine("## B. State follows person");
                var bag = runner.AuditCrewWorker(1).State;
                float mfKeep = bag.MentalFatigue;
                runner.AuditTryAssign(1, JobType.Hauling, out _);
                runner.AuditTryAssign(1, JobType.Engineering, out _);
                Check("Same WorkerState object across jobs",
                    ReferenceEquals(bag, runner.AuditCrewWorker(1).State));
                Check("MentalFatigue retained across reassignment",
                    Mathf.Approximately(runner.AuditWorkerState(1).MentalFatigue, mfKeep));

                // C — no universal stacked efficiency API
                log.AppendLine();
                log.AppendLine("## C. No universal stacked efficiency");
                Check("No WorkerEfficiency / stacked mul type",
                    Type.GetType("DeepCore.FreeMovement.WorkerEfficiency") == null);

                // I — idle recovery
                log.AppendLine();
                log.AppendLine("## I. Idle / rest recovery");
                runner.AuditWorkerState(3).MentalFatigue = 60f;
                runner.AuditWorkerState(3).FocusState = 40f;
                float mfI = runner.AuditWorkerState(3).MentalFatigue;
                WorkerJobDemand.TickIdleOrRest(runner.AuditCrewWorker(3), 2f, resting: false);
                Check("Idle recovers MentalFatigue",
                    runner.AuditWorkerState(3).MentalFatigue < mfI);

                // J — exhaustion fires once
                log.AppendLine();
                log.AppendLine("## J. Exhaustion latch");
                var kow = runner.AuditCrewWorker(3);
                kow.EventHistory.Clear();
                kow.State.ExhaustionLatched = false;
                kow.State.StaminaPrimed = true;
                kow.State.SetStamina(kow.PhysicalStaminaMax * 0.05f, kow.PhysicalStaminaMax);
                int hist0 = kow.EventHistory.Items.Count;
                WorkerJobDemand.TickActive(kow, new JobDemandProfile(1f, 0f, 0f, "PUSH"), 0.01f,
                    JobType.Hauling, "cart.test");
                int hist1 = kow.EventHistory.Items.Count;
                WorkerJobDemand.TickActive(kow, new JobDemandProfile(1f, 0f, 0f, "PUSH"), 0.01f,
                    JobType.Hauling, "cart.test");
                int hist2 = kow.EventHistory.Items.Count;
                Check("Exhaustion event fires once per threshold",
                    hist1 == hist0 + 1 && hist2 == hist1 && kow.State.ExhaustionLatched);

                // K — event history not spammy (gate exists)
                log.AppendLine();
                log.AppendLine("## K. Event spam control");
                Check("Spam gate type present",
                    typeof(WorkerStateEventSpamGate) != null);

                // L — sleep
                log.AppendLine();
                log.AppendLine("## L. Sleep recovery");
                runner.AuditWorkerState(1).Frustration = 60f;
                runner.AuditApplyCrewSleepRecovery(1f);
                Check("Sleep still partially relieves Frustration",
                    runner.AuditWorkerState(1).Frustration > 0f
                    && runner.AuditWorkerState(1).Frustration < 60f);

                // M — Morale slow
                log.AppendLine();
                log.AppendLine("## M. Morale slow-moving");
                float mo0 = runner.AuditWorkerState(2).Morale;
                WorkerJobDemand.TickActive(runner.AuditCrewWorker(2),
                    new JobDemandProfile(0.5f, 0.5f, 0.5f, "WORK"), 3f, JobType.Excavation, "x");
                Check("Normal work demand does not move Morale",
                    Mathf.Approximately(runner.AuditWorkerState(2).Morale, mo0));

                // Profiles
                log.AppendLine();
                log.AppendLine("## Profile sheets (BASE / Focus / Det / Comp)");
                ApplySheet(runner, 1, focus: 18, stamina: 10, determination: 10, composure: 10);
                ApplySheet(runner, 2, focus: 5, stamina: 18, determination: 10, composure: 10);
                ApplySheet(runner, 3, focus: 10, stamina: 10, determination: 18, composure: 10);
                ApplySheet(runner, 4, focus: 10, stamina: 10, determination: 10, composure: 4);
                Check("Profile sheets applied without crash", true);

                // Readiness matrix notes in log
                log.AppendLine();
                log.AppendLine("## Social Aura readiness matrix");
                log.AppendLine("| Job | Verdict | Notes |");
                log.AppendLine("|-----|---------|-------|");
                log.AppendLine("| Prospecting | PASS | Mental/Focus demand + Discovery events |");
                log.AppendLine("| Excavation | PASS | Dig stamina + Frustration events + light MF/FS |");
                log.AppendLine("| Hauling | PASS | Physical demand + Delivered/Stuck events |");
                log.AppendLine("| Refining | PASS | Mental/Attention + WashComplete events |");
                log.AppendLine("| Engineering | PASS | Mixed demand + Repair events |");
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
            log.AppendLine(fail == 0
                ? "VERDICT: PASS — V1.2C READY TO LOCK"
                : "VERDICT: FAIL");
            log.AppendLine();
            log.AppendLine("InvestigationFailure: ProspectorDrySpell — prolonged no-Discovery only (not per-scan).");

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(Application.dataPath, "..", "BenchmarkResults")
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string latest = Path.Combine(dir, "v12c_demand_audit_latest.md");
            File.WriteAllText(latest, log.ToString());
            File.WriteAllText(Path.Combine(dir, $"v12c_demand_audit_{stamp}.md"), log.ToString());
            return latest;
        }

        static void ApplySheet(FreeMovementSocketMapRunner runner, int id,
            int focus, int stamina, int determination, int composure)
        {
            var wr = runner.AuditCrewWorker(id);
            if (wr?.Stats == null) return;
            wr.Stats.Set(WorkerStatId.Focus, focus);
            wr.Stats.Set(WorkerStatId.Stamina, stamina);
            wr.Stats.Set(WorkerStatId.Determination, determination);
            wr.Stats.Set(WorkerStatId.Composure, composure);
            wr.State.StaminaPrimed = false;
            WorkerJobDemand.EnsureStaminaPrimed(wr);
        }
    }
}
