using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Camp social opportunity V1 foundation audit.</summary>
    public static class CampSocialV1Audit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[CAMP SOCIAL V1] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.ReadAllText(path).Contains("INVARIANT: FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var log = new StringBuilder(10_000);
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                if (ok) { pass++; log.AppendLine($"PASS | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}")); }
                else { fail++; log.AppendLine($"FAIL | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}")); }
            }

            log.AppendLine("# Camp Social Opportunity V1 Audit");
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine();

            var a = new WorkerRuntime(1, "A");
            var b = new WorkerRuntime(2, "B");

            var camp = SocialContextLive.Derive(
                a, b, JobType.Unassigned, JobType.Unassigned,
                SocialPresenceKind.Idle, SocialPresenceKind.Idle,
                100f, bothInCamp: true);
            Check("Camp context when both idle in camp", camp == SocialContext.Camp, camp.ToString());

            var sleep = SocialContextLive.Derive(
                a, b, JobType.Unassigned, JobType.Unassigned,
                SocialPresenceKind.Sleeping, SocialPresenceKind.Idle,
                100f, bothInCamp: true);
            Check("Sleeping presence does not yield Camp (falls through)",
                sleep != SocialContext.Camp, sleep.ToString());

            var work = SocialContextLive.Derive(
                a, b, JobType.Excavation, JobType.Excavation,
                SocialPresenceKind.Operating, SocialPresenceKind.Operating,
                100f, bothInCamp: true);
            Check("Operating pair keeps WorkingTogether (camp does not override)",
                work == SocialContext.WorkingTogether, work.ToString());

            var idleNoCamp = SocialContextLive.Derive(
                a, b, JobType.Unassigned, JobType.Unassigned,
                SocialPresenceKind.Idle, SocialPresenceKind.Idle,
                100f, bothInCamp: false);
            Check("Idle outside camp → IdleNearby",
                idleNoCamp == SocialContext.IdleNearby, idleNoCamp.ToString());

            Check("Camp ContextMul is distinct and modest (0.90)",
                Mathf.Abs(SocialAuraTuning.ContextMul(SocialContext.Camp) - 0.90f) < 0.001f);
            Check("IdleNearby ContextMul unchanged (0.85)",
                Mathf.Abs(SocialAuraTuning.ContextMul(SocialContext.IdleNearby) - 0.85f) < 0.001f);
            Check("WorkingTogether ContextMul unchanged (1.25)",
                Mathf.Abs(SocialAuraTuning.ContextMul(SocialContext.WorkingTogether) - 1.25f) < 0.001f);
            Check("PressureTrigger unchanged",
                Mathf.Abs(SocialAuraTuning.PressureTrigger - 1.05f) < 0.001f);
            Check("Sleeping still ineligible",
                !SocialAuraEligibility.IsEligible(a, SocialPresenceKind.Sleeping));

            // Camp does not guarantee positive — Resolve still uses normal action selection
            var world = new SocialAuraWorld();
            var sa = new SocialSimActor(1, "A", WorkerStats.CreateBaseline(), WorkerState.CreateDefault());
            var sb = new SocialSimActor(2, "B", WorkerStats.CreateBaseline(), WorkerState.CreateDefault());
            sa.State.Frustration = 70f;
            sb.State.Frustration = 65f;
            sa.State.Morale = 30f;
            sb.State.Morale = 28f;
            world.AddActor(sa);
            world.AddActor(sb);
            world.BeginShift(1);
            int clash = 0, positive = 0, total = 0;
            for (int i = 0; i < 80; i++)
            {
                var e = world.Expose(1, 2, SocialContext.Camp, 1.5f);
                if (e == null) continue;
                total++;
                if (e.OutcomeSummary != null && e.OutcomeSummary.Contains("CLASH")) clash++;
                if (e.OutcomeSummary != null && e.OutcomeSummary.Contains("POSITIVE")) positive++;
            }
            Check("Camp encounters can resolve (existing aura)", total > 0, $"n={total}");
            Check("Camp is not universally positive (clash or mixed possible)",
                total == 0 || clash > 0 || positive < total,
                $"pos={positive} clash={clash} total={total}");

            log.AppendLine();
            log.AppendLine("## Summary");
            log.AppendLine($"PASS {pass} / FAIL {fail}");
            log.AppendLine(fail == 0 ? "INVARIANT: PASS" : "INVARIANT: FAIL");
            log.AppendLine();
            log.AppendLine("## Files");
            log.AppendLine("- SocialAuraStage0Types.cs (SocialContext.Camp)");
            log.AppendLine("- SocialAuraLive.cs (Camp derive + counts)");
            log.AppendLine("- CampSocialV1Audit.cs");

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(Application.dataPath, "..", "BenchmarkResults")
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string latest = Path.Combine(dir, "camp_social_v1_latest.md");
            File.WriteAllText(Path.Combine(dir, $"camp_social_v1_{DateTime.Now:yyyyMMdd_HHmmss}.md"), log.ToString());
            File.WriteAllText(latest, log.ToString());
            return latest;
        }
    }
}
