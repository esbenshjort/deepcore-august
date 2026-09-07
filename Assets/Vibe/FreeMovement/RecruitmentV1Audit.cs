using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Static Recruitment V1 integrity checks + BenchmarkResults report.</summary>
    public static class RecruitmentV1Audit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[RECRUITMENT V1] Report: {path}");
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

            sb.AppendLine("# Recruitment V1 Audit");
            sb.AppendLine();
            sb.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("## Catalog");

            int jobsOk = 0;
            foreach (var job in RecruitmentCatalog.HireJobs)
            {
                var list = RecruitmentCatalog.ForJob(job);
                bool ok = list != null && list.Count >= 4;
                Check($"Candidates for {job}", ok, $"count={list?.Count ?? 0}");
                if (ok) jobsOk++;
            }
            Check("All hire jobs have ≥4 candidates", jobsOk == RecruitmentCatalog.HireJobs.Length);

            // Unique candidate ids
            var ids = new System.Collections.Generic.HashSet<string>();
            bool unique = true;
            foreach (var c in RecruitmentCatalog.All)
            {
                if (!ids.Add(c.CandidateId)) unique = false;
                Check($"Candidate {c.CandidateId} has stats",
                    c.BaseStats != null && c.DisplayName != null);
            }
            Check("Candidate IDs unique", unique);

            sb.AppendLine();
            sb.AppendLine("## Materialize / identity");
            var session = new RecruitmentHiringSession();
            session.Open();
            // Hire first candidate per job
            foreach (var job in RecruitmentCatalog.HireJobs)
            {
                session.SetBrowseJob(job);
                session.BrowseIndex = 0;
                session.HireSelected();
            }
            Check($"Session fills {RecruitmentCatalog.HireJobs.Length} seats", session.AllJobsFilled, $"filled={session.FilledCount}");
            bool built = session.TryBuildCrew(101, out var crew, out var jobs, out string failMsg);
            Check("TryBuildCrew", built, failMsg);
            if (built && crew != null)
            {
                Check($"Crew length {RecruitmentCatalog.HireJobs.Length}",
                    crew.Length == RecruitmentCatalog.HireJobs.Length);
                var wid = new System.Collections.Generic.HashSet<int>();
                var statsRefs = new System.Collections.Generic.HashSet<int>();
                bool uniqueIds = true, uniqueSheets = true, stateOk = true, traitsOk = true;
                for (int i = 0; i < crew.Length; i++)
                {
                    var wr = crew[i];
                    if (wr == null) { uniqueIds = false; continue; }
                    if (!wid.Add(wr.WorkerId)) uniqueIds = false;
                    if (!statsRefs.Add(RuntimeHelpersHash(wr.Stats))) uniqueSheets = false;
                    if (wr.State == null || !wr.State.IsAlive) stateOk = false;
                    if (wr.Identity == null || wr.Identity.Traits == null) traitsOk = false;
                    Check($"Job map [{i}] {jobs[i]} → {wr.DisplayName} id={wr.WorkerId}",
                        wr.WorkerId > 0);
                }
                Check("Unique WorkerIds", uniqueIds);
                Check("Unique Stats instances", uniqueSheets);
                Check("WorkerState initialized / alive", stateOk);
                Check("Identity + traits persist", traitsOk);
            }

            sb.AppendLine();
            sb.AppendLine("## Stat tooltips (job-specific)");
            var sample = WorkerStatId.RawPower;
            var (exText, exImp, _) = RecruitmentStatTooltips.ForJob(sample, JobType.Excavation);
            var (prText, prImp, _) = RecruitmentStatTooltips.ForJob(sample, JobType.Prospecting);
            Check("Raw Power Excavation HIGH",
                exImp == StatJobImportance.High && exText.Contains("dig power"));
            Check("Raw Power Prospecting not HIGH",
                prImp != StatJobImportance.High);
            Check("Tooltip text changes by job", exText != prText);

            var (minEx, minImp, _) = RecruitmentStatTooltips.ForJob(WorkerStatId.Mineralogy, JobType.Excavation);
            Check("Mineralogy Excavation LOW", minImp == StatJobImportance.Low || minImp == StatJobImportance.Irrelevant);

            sb.AppendLine();
            sb.AppendLine("## Traits");
            var baseStats = WorkerStats.CreateBaseline();
            int before = baseStats.Get(WorkerStatId.Composure);
            RecruitmentTraits.ApplyToStats(baseStats, new[] { RecruitmentTraitId.CalmUnderPressure });
            Check("CalmUnderPressure bumps Composure",
                baseStats.Get(WorkerStatId.Composure) > before);

            sb.AppendLine();
            sb.AppendLine("## Session cancel safety");
            var session2 = new RecruitmentHiringSession();
            session2.Open();
            session2.SetBrowseJob(JobType.Excavation);
            session2.HireSelected();
            session2.Close();
            Check("Close clears IsOpen", !session2.IsOpen);

            sb.AppendLine();
            sb.AppendLine("## Runtime notes (play-mode)");
            sb.AppendLine("- START GAME opens hiring UI without replacing crew until CONFIRM.");
            sb.AppendLine("- CONFIRM CREW calls ReplaceActiveCrew → unique WorkerIds, TryAssign/BindHost, SpawnCrewAvatars, SocialAura.Bootstrap.");
            sb.AppendLine("- RESET TO DEFAULT CREW restores BootstrapPrototypeCrew (Lewis/Mara/Kowalski/Elena/Viktor).");
            sb.AppendLine("- Dev crew path unchanged when START GAME is never used.");
            sb.AppendLine();
            sb.AppendLine($"**Result:** {(fail == 0 ? "PASS" : "FAIL")}  ({pass} checks passed, {fail} failed)");

            string dir = outputDirectory
                ?? Path.Combine(Application.dataPath, "..", "BenchmarkResults");
            Directory.CreateDirectory(dir);
            string latest = Path.Combine(dir, "recruitment_v1_latest.md");
            string stamped = Path.Combine(dir,
                $"recruitment_v1_{System.DateTime.Now:yyyyMMdd_HHmmss}.md");
            File.WriteAllText(latest, sb.ToString());
            File.WriteAllText(stamped, sb.ToString());
            Debug.Log($"[RECRUITMENT V1] {(fail == 0 ? "PASS" : "FAIL")} → {latest}");
            return latest;
        }

        static int RuntimeHelpersHash(object o) =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
    }
}
