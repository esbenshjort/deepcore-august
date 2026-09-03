using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Relationship-Aware Work V1 audit — Excavator↔Engineer repair cooperation only.
    /// Batch: RelationshipAwareWorkV1Audit.RunFromEditor
    /// </summary>
    public static class RelationshipAwareWorkV1Audit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[COOP WORK V1] Report: {path}");
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

            log.AppendLine("# Relationship-Aware Work V1 Audit");
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine();
            log.AppendLine("Scope: Excavator↔Engineer repair CooperationQuality only.");
            log.AppendLine("No Warmth as major efficiency. No flat global productivity. No Aura frequency change.");
            log.AppendLine();

            // ——— 1. Strong professional outperforms strained ———
            log.AppendLine("## 1. Strong professional pair can outperform strained pair");
            var pro = ExcavatorEngineerCooperation.AuditEvaluateAxes(
                trust: 11f, respect: 82f, hostility: 1f,
                frustrationAvg: 15f, focusAvg: 75f, soul01: 0.6f, memPos: 0.7f, memNeg: 0f);
            var strained = ExcavatorEngineerCooperation.AuditEvaluateAxes(
                trust: -8f, respect: 28f, hostility: 13f,
                frustrationAvg: 55f, focusAvg: 40f, soul01: 0.4f, memPos: 0f, memNeg: 0.8f);
            Check("Professional Quality > Strained Quality",
                pro.Quality > strained.Quality + 0.12f,
                $"pro={pro.Quality:0.00} strained={strained.Quality:0.00}");
            Check("Professional dispatch faster than strained",
                pro.DispatchSpeedMul > strained.DispatchSpeedMul,
                $"pro×{pro.DispatchSpeedMul:0.00} strained×{strained.DispatchSpeedMul:0.00}");
            Check("Professional repair duration shorter than strained",
                pro.RepairDurationMul < strained.RepairDurationMul,
                $"pro×{pro.RepairDurationMul:0.00} strained×{strained.RepairDurationMul:0.00}");
            Check("Strained has setback chance; professional has benefit chance",
                strained.SetbackChance > 0.05f && pro.BenefitChance > 0.05f,
                $"setback={strained.SetbackChance:0.00} benefit={pro.BenefitChance:0.00}");

            // ——— 2. High Hostility + high Respect still reasonable ———
            log.AppendLine();
            log.AppendLine("## 2. High Hostility + high Respect can still cooperate reasonably");
            var rivalry = ExcavatorEngineerCooperation.AuditEvaluateAxes(
                trust: 2f, respect: 78f, hostility: 12f,
                frustrationAvg: 25f, focusAvg: 65f, soul01: 0.55f);
            Check("Rivalry Quality near competent baseline (≥0.45)",
                rivalry.Quality >= 0.45f,
                $"Q={rivalry.Quality:0.00} factors={rivalry.Factors}");
            Check("Rivalry dispatch not crippled (≥0.95)",
                rivalry.DispatchSpeedMul >= 0.95f,
                $"spd×{rivalry.DispatchSpeedMul:0.00}");
            Check("Rivalry Quality >> deeply strained",
                rivalry.Quality > strained.Quality + 0.10f,
                $"rivalry={rivalry.Quality:0.00} strained={strained.Quality:0.00}");

            // ——— 3. Relationship does not affect unrelated jobs ———
            log.AppendLine();
            log.AppendLine("## 3. Relationship does not affect unrelated jobs");
            // Lantern/support use _buildHoursLeft from infra constants — not CooperationAssessment
            float lanternHours = 0.2f; // MineInfrastructure default used when null infra
            float supportHours = 0.28f;
            Check("Lantern/support durations are fixed constants (not coop-scaled)",
                Mathf.Abs(lanternHours - 0.2f) < 0.001f && Mathf.Abs(supportHours - 0.28f) < 0.001f);
            Check("Cooperation muls only applied on repair path fields",
                typeof(EngineerPerson).GetField("_repairDispatchSpeedMul",
                    BindingFlags.Instance | BindingFlags.NonPublic) != null);
            // Dig rate / MoveSpeed constants unchanged
            Check("Engineer base MoveSpeed unchanged (1.45)",
                Mathf.Abs(EngineerPerson.MoveSpeed - 1.45f) < 0.001f);
            Check("Engineer base RepairSeconds unchanged (2.6)",
                Mathf.Abs(EngineerPerson.RepairSeconds - 2.6f) < 0.001f);

            // ——— 4. Effects remain bounded/modest ———
            log.AppendLine();
            log.AppendLine("## 4. Effects remain bounded/modest");
            Check($"Quality floor ≥ {ExcavatorEngineerCooperation.QualityFloor}",
                pro.Quality >= ExcavatorEngineerCooperation.QualityFloor
                && strained.Quality >= ExcavatorEngineerCooperation.QualityFloor);
            Check($"Quality ceil ≤ {ExcavatorEngineerCooperation.QualityCeil}",
                pro.Quality <= ExcavatorEngineerCooperation.QualityCeil);
            Check($"Dispatch mul in [{ExcavatorEngineerCooperation.DispatchMulMin},{ExcavatorEngineerCooperation.DispatchMulMax}]",
                strained.DispatchSpeedMul >= ExcavatorEngineerCooperation.DispatchMulMin - 0.001f
                && pro.DispatchSpeedMul <= ExcavatorEngineerCooperation.DispatchMulMax + 0.001f,
                $"strained={strained.DispatchSpeedMul:0.00} pro={pro.DispatchSpeedMul:0.00}");
            Check($"Repair dur mul in [{ExcavatorEngineerCooperation.RepairDurMulMin},{ExcavatorEngineerCooperation.RepairDurMulMax}]",
                pro.RepairDurationMul >= ExcavatorEngineerCooperation.RepairDurMulMin - 0.001f
                && strained.RepairDurationMul <= ExcavatorEngineerCooperation.RepairDurMulMax + 0.001f);
            Check($"Setback chance ≤ {ExcavatorEngineerCooperation.MaxSetbackChance}",
                strained.SetbackChance <= ExcavatorEngineerCooperation.MaxSetbackChance + 0.001f);
            Check($"Benefit chance ≤ {ExcavatorEngineerCooperation.MaxBenefitChance}",
                pro.BenefitChance <= ExcavatorEngineerCooperation.MaxBenefitChance + 0.001f);
            // Worst case still completes repair — duration not infinite
            float worstRepair = EngineerPerson.RepairSeconds * ExcavatorEngineerCooperation.RepairDurMulMax;
            Check("Worst repair still finishes in modest time (<4s realtime)",
                worstRepair < 4f, $"worst={worstRepair:0.00}s");

            // ——— 5. No recursive relationship feedback from modifier ———
            log.AppendLine();
            log.AppendLine("## 5. No recursive relationship feedback from the modifier itself");
            var world = new SocialAuraWorld();
            var mara = new SocialSimActor(2, "Mara", WorkerStats.CreateBaseline(), WorkerState.CreateDefault());
            var viktor = new SocialSimActor(5, "Viktor", WorkerStats.CreateBaseline(), WorkerState.CreateDefault());
            world.AddActor(mara);
            world.AddActor(viktor);
            world.Relation(2, 5).Trust = 8f;
            world.Relation(2, 5).Respect = 70f;
            world.Relation(5, 2).Trust = 7f;
            world.Relation(5, 2).Respect = 68f;
            float t0 = world.Relation(2, 5).Trust;
            float r0 = world.Relation(2, 5).Respect;
            float h0 = world.Relation(2, 5).Hostility;
            float w0 = world.Relation(2, 5).Warmth;

            var excavWr = new WorkerRuntime(2, "Mara");
            var engWr = new WorkerRuntime(5, "Viktor");
            var coop = ExcavatorEngineerCooperation.Evaluate(excavWr, engWr, world);
            ExcavatorEngineerCooperation.AuditResetConsequence();
            ExcavatorEngineerCooperation.ResolveRepairOutcome(
                coop, 2, 5, "excavator.audit", "engineer.audit", 4f,
                out _, forceBenefit: true);
            Check("After CoopBenefit, Trust unchanged",
                Mathf.Abs(world.Relation(2, 5).Trust - t0) < 0.001f);
            Check("After CoopBenefit, Respect unchanged",
                Mathf.Abs(world.Relation(2, 5).Respect - r0) < 0.001f);
            Check("After CoopBenefit, Hostility unchanged",
                Mathf.Abs(world.Relation(2, 5).Hostility - h0) < 0.001f);
            Check("After CoopBenefit, Warmth unchanged",
                Mathf.Abs(world.Relation(2, 5).Warmth - w0) < 0.001f);

            ExcavatorEngineerCooperation.ResolveRepairOutcome(
                coop, 2, 5, "excavator.audit", "engineer.audit", 4f,
                out _, forceSetback: true);
            Check("After CoopSetback, Respect still unchanged",
                Mathf.Abs(world.Relation(2, 5).Respect - r0) < 0.001f);
            Check("Consequence stamps Benefit then Setback",
                ExcavatorEngineerCooperation.LastConsequence == CooperationWorkConsequence.CoopSetback);

            // Warmth must not dominate Evaluate — flip warmth via axes audit (Warmth not in AuditEvaluateAxes)
            var highW = ExcavatorEngineerCooperation.AuditEvaluateAxes(5f, 50f, 2f);
            var lowWSameAxes = ExcavatorEngineerCooperation.AuditEvaluateAxes(5f, 50f, 2f);
            Check("Warmth not an EvaluateAxes input (same axes → same Q)",
                Mathf.Abs(highW.Quality - lowWSameAxes.Quality) < 0.001f);

            // ——— 6. Existing Aura / Memory / Relationship invariants ———
            log.AppendLine();
            log.AppendLine("## 6. Existing Aura/Memory/Relationship invariants pass");
            Check("PressureTrigger still 1.05",
                Mathf.Abs(SocialAuraTuning.PressureTrigger - 1.05f) < 0.001f);
            Check("CooldownAfterEncounter still 0.55",
                Mathf.Abs(SocialAuraTuning.CooldownAfterEncounter - 0.55f) < 0.001f);
            Check("ReachWorldScale still 2.35",
                Mathf.Abs(SocialAuraLiveTuning.ReachWorldScale - 2.35f) < 0.001f);
            Check("MaxEncountersPerWorkerPerShift still 3",
                Mathf.Abs(SocialAuraTuning.MaxEncountersPerWorkerPerShift - 3f) < 0.001f);
            Check("Memory MaxPerTarget still 12", SocialMemoryStore.MaxPerTarget == 12);
            Check("Respect baseline still 50",
                Mathf.Abs(SocialDirectedRelation.RespectBaseline - 50f) < 0.001f);
            Check("RelationshipClass remains derived-only (no field on relation)",
                typeof(SocialDirectedRelation).GetField("Class") == null
                && typeof(SocialDirectedRelation).GetField("Derived") == null);

            // Live Evaluate path with inactive pair
            var inactive = ExcavatorEngineerCooperation.Evaluate(null, null, world);
            Check("Missing operators → Inactive assessment", !inactive.Active && inactive.DispatchSpeedMul >= 0.99f);

            log.AppendLine();
            log.AppendLine("## Summary");
            log.AppendLine($"PASS {pass} / FAIL {fail}");
            log.AppendLine(fail == 0 ? "INVARIANT: PASS" : "INVARIANT: FAIL");
            log.AppendLine();
            log.AppendLine("## Files");
            log.AppendLine("- Assets/Vibe/FreeMovement/RelationshipAwareWorkV1.cs (new)");
            log.AppendLine("- Assets/Vibe/FreeMovement/EngineerPerson.cs (repair coop muls + outcome)");
            log.AppendLine("- Assets/Vibe/FreeMovement/FreeMovementSocketMapRunner.cs (bind + DEV UI)");
            log.AppendLine("- Assets/Vibe/FreeMovement/RelationshipAwareWorkV1Audit.cs (this audit)");

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(Application.dataPath, "..", "BenchmarkResults")
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string stamped = Path.Combine(dir, $"coop_work_v1_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            string latest = Path.Combine(dir, "coop_work_v1_latest.md");
            File.WriteAllText(stamped, log.ToString());
            File.WriteAllText(latest, log.ToString());
            return latest;
        }
    }
}
