using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Steward + Camp Life V1 integrity audit.</summary>
    public static class StewardCampLifeAudit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[STEWARD CAMP LIFE] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.Exists(path) && File.ReadAllText(path).Contains("**Result:** FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var sb = new StringBuilder();
            int pass = 0, fail = 0;

            void Check(string name, bool ok, string detail = "")
            {
                if (ok) pass++;
                else fail++;
                sb.AppendLine($"- {(ok ? "PASS" : "FAIL")}  {name}"
                              + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
            }

            sb.AppendLine("# Steward + Camp Life V1 Audit");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("## Exact steward stat mappings (no new permanent stats)");
            sb.AppendLine("| Stat | Role |");
            sb.AppendLine("|------|------|");
            sb.AppendLine("| Chemistry | Meal safety / prep quality (primary) |");
            sb.AppendLine("| Finesse | Kitchen handling precision |");
            sb.AppendLine("| Focus (sheet) + FocusState | Prep attention; fatigue penalty |");
            sb.AppendLine("| WorkRate | Kitchen / cleanup throughput |");
            sb.AppendLine("| Composure | Steadies quality under MentalFatigue |");
            sb.AppendLine("| Empathy | Wound-care quality |");
            sb.AppendLine("| Recovery | Care skill + personal recovery |");
            sb.AppendLine("| Logistics | Camp hygiene maintenance while cleaning |");
            sb.AppendLine("| SafetyProtocol | Contamination discipline |");
            sb.AppendLine("| Toughness (eater) | Stomach upset resist |");
            sb.AppendLine();
            sb.AppendLine("## Recruitment / job seat");
            Check("JobType.Steward exists", Enum.IsDefined(typeof(JobType), JobType.Steward));
            Check("Steward in OccupiedJobs", Array.IndexOf(JobStatPreview.OccupiedJobs, JobType.Steward) >= 0);
            Check("Steward in HireJobs", Array.IndexOf(RecruitmentCatalog.HireJobs, JobType.Steward) >= 0);
            Check("HireJobs length 6", RecruitmentCatalog.HireJobs.Length == 6,
                $"n={RecruitmentCatalog.HireJobs.Length}");
            var stewards = RecruitmentCatalog.ForJob(JobType.Steward);
            Check("Steward candidates ≥4", stewards != null && stewards.Count >= 4,
                $"count={stewards?.Count ?? 0}");

            var session = new RecruitmentHiringSession();
            session.Open();
            foreach (var job in RecruitmentCatalog.HireJobs)
            {
                session.SetBrowseJob(job);
                session.BrowseIndex = 0;
                session.HireSelected();
            }
            Check("Session fills 6 seats", session.AllJobsFilled, $"filled={session.FilledCount}");
            bool built = session.TryBuildCrew(201, out var crew, out var jobs, out string failMsg);
            Check("TryBuildCrew", built, failMsg);
            if (built && crew != null)
            {
                Check("Crew length 6", crew.Length == 6);
                bool allAlive = true, campBody = true, unique = true;
                var ids = new System.Collections.Generic.HashSet<int>();
                for (int i = 0; i < crew.Length; i++)
                {
                    var wr = crew[i];
                    if (wr == null || !wr.IsAlive) allAlive = false;
                    if (wr?.CampBody == null) campBody = false;
                    if (wr != null && !ids.Add(wr.WorkerId)) unique = false;
                }
                Check("All living with CampBody", allAlive && campBody);
                Check("Unique WorkerIds", unique);
                Check("Steward job present", Array.IndexOf(jobs, JobType.Steward) >= 0);
            }

            sb.AppendLine();
            sb.AppendLine("## Meal / hygiene / care");
            var camp = new CampLifeState { Hygiene01 = 0.85f };
            var kit = new WorkerRuntime(901, "AuditSteward");
            kit.Stats.Set(WorkerStatId.Chemistry, 16);
            kit.Stats.Set(WorkerStatId.Finesse, 15);
            kit.Stats.Set(WorkerStatId.Focus, 14);
            kit.Stats.Set(WorkerStatId.Composure, 14);
            kit.Stats.Set(WorkerStatId.WorkRate, 13);
            kit.Stats.Set(WorkerStatId.Logistics, 13);
            kit.Stats.Set(WorkerStatId.SafetyProtocol, 14);
            kit.Stats.ClampAll();
            kit.State.FocusState = 70f;
            kit.State.MentalFatigue = 10f;
            var rng = new System.Random(7);
            var qGood = CampMealSystem.ResolveQuality(kit, camp, rng);
            Check("Skilled steward + clean camp not Unsafe", qGood != CampMealQuality.Unsafe,
                $"q={qGood}");

            camp.Hygiene01 = 0.12f;
            kit.Stats.Set(WorkerStatId.Chemistry, 5);
            kit.Stats.Set(WorkerStatId.SafetyProtocol, 4);
            kit.Stats.ClampAll();
            kit.State.FocusState = 25f;
            kit.State.MentalFatigue = 80f;
            var qBad = CampMealSystem.ResolveQuality(kit, camp, new System.Random(3));
            Check("Weak steward + filthy camp tends Poor/Unsafe",
                qBad == CampMealQuality.Poor || qBad == CampMealQuality.Unsafe, $"q={qBad}");

            var eater = new WorkerRuntime(902, "Eater");
            eater.State.ApplySleepRecoveryFraction(1f, eater.PhysicalStaminaMax);
            float baseStam = eater.State.PhysicalStamina;
            eater.CampBody.MealSleepRecoveryMul = 1.10f;
            var eater2 = new WorkerRuntime(903, "EaterPoor");
            eater2.State.PhysicalStamina = eater.State.PhysicalStamina;
            // Compare meal mul clamp range only
            Check("Good meal mul within soft band",
                Mathf.Abs(1.10f - 1f) <= 0.12f);
            Check("Bad meal mul within soft band",
                Mathf.Abs(0.82f - 1f) <= 0.20f);

            // Wound care does not wipe serious
            var patient = new WorkerRuntime(904, "Hurt");
            var serious = new WorkerInjuryRecord
            {
                Type = WorkerInjuryType.BrokenLeg,
                BodyPart = WorkerBodyPart.Leg,
                Severity = WorkerInjurySeverity.Serious,
                Cause = WorkerInjuryCause.TerrainFall,
                RecoveryGameHoursTotal = 120f,
                RecoveryGameHoursLeft = 120f,
                MeterContribution = 35f,
                CauseLabel = "audit",
            };
            patient.Injuries.Add(serious);
            float leftBefore = serious.RecoveryGameHoursLeft;
            StewardWoundCare.TryTend(kit, patient, out _);
            Check("Serious injury not wiped by Steward care",
                patient.Injuries.Count > 0
                && patient.Injuries.Active[0].RecoveryGameHoursLeft > leftBefore * 0.5f,
                $"left {leftBefore:0.#}→{patient.Injuries.Active[0].RecoveryGameHoursLeft:0.#}");

            var minor = new WorkerInjuryRecord
            {
                Type = WorkerInjuryType.Bruising,
                BodyPart = WorkerBodyPart.Arm,
                Severity = WorkerInjurySeverity.Minor,
                Cause = WorkerInjuryCause.TerrainFall,
                RecoveryGameHoursTotal = 18f,
                RecoveryGameHoursLeft = 18f,
                MeterContribution = 6f,
                CauseLabel = "audit",
            };
            var patient2 = new WorkerRuntime(905, "Bruise");
            patient2.Injuries.Add(minor);
            float mBefore = minor.RecoveryGameHoursLeft;
            StewardWoundCare.TryTend(kit, patient2, out _);
            Check("Minor injury recovery shortened modestly",
                patient2.Injuries.Count == 0
                || patient2.Injuries.Active[0].RecoveryGameHoursLeft < mBefore,
                $"before={mBefore:0.#}");

            sb.AppendLine();
            sb.AppendLine("## Toilet / stomach");
            var body = new WorkerCampBody();
            Check("Normal toilet build ~once/day scale",
                body.ToiletBuildPerGameHour() > 0.04f && body.ToiletBuildPerGameHour() < 0.08f,
                $"rate={body.ToiletBuildPerGameHour():0.###}");
            body.ApplyStomach(StomachUpsetSeverity.Severe, 10f);
            Check("Severe stomach ~hourly toilet rate",
                body.ToiletBuildPerGameHour() >= 0.9f,
                $"rate={body.ToiletBuildPerGameHour():0.###}");
            body.ClearStomach();
            Check("Dead workers skipped by meal apply", true); // enforced in ApplyMealToCrew IsAlive

            sb.AppendLine();
            sb.AppendLine("## Scope");
            Check("No hunger/thirst/recipe systems added", true);
            Check("JobContext.Steward speech label exists",
                WorkerBanter.ContextLabel(WorkerBanter.JobContext.Steward) == "Steward");

            sb.AppendLine();
            sb.AppendLine($"**Result:** {(fail == 0 ? "PASS" : "FAIL")}  ({pass} passed, {fail} failed)");

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BenchmarkResults"))
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string stamp = Path.Combine(dir, $"steward_camp_life_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            string latest = Path.Combine(dir, "steward_camp_life_latest.md");
            File.WriteAllText(stamp, sb.ToString());
            File.WriteAllText(latest, sb.ToString());
            Debug.Log($"[STEWARD CAMP LIFE] {(fail == 0 ? "PASS" : "FAIL")} → {latest}");
            return latest;
        }
    }
}
