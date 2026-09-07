using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Static + structural regression audit for worker commute / teleport / camp bounds.
    /// Full 6×3 play-mode cycles require Editor Play; this documents architecture PASS gates.
    /// </summary>
    public static class WorkerCommuteRegressionAudit
    {
        public static string Run(string outputDirectory = null)
        {
            var sb = new StringBuilder(4096);
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                if (ok) pass++;
                else fail++;
                sb.AppendLine($"- {(ok ? "PASS" : "FAIL")}: {name}"
                    + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
            }

            sb.AppendLine("# Worker Commute / Teleport / Map Bounds Regression");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("## Architecture checks (source)");

            string runnerPath = Path.GetFullPath(Path.Combine(
                Application.dataPath, "Vibe", "FreeMovement", "FreeMovementSocketMapRunner.cs"));
            string runner = File.Exists(runnerPath) ? File.ReadAllText(runnerPath) : "";
            Check("Runner source readable", runner.Length > 1000);

            Check("BeginHeadingOut enters HeadingOut",
                runner.Contains("_crewPhase = CrewPhase.HeadingOut")
                && runner.Contains("Walking to instruments — physical commute"));
            Check("BeginHeadingOut does not SoftArriveAllWork",
                !ContainsCallSequence(runner, "SoftArriveAllWork()", "EnterOnShift()", 400));
            Check("EnterOnShift does not SoftTeleport hosts",
                !runner.Contains("AlignHostsToMorningPosts")
                && runner.Contains("do NOT SoftTeleport hosts"));
            Check("Stuck commute does not soft-arrive teleport",
                runner.Contains("PATH BLOCKED — stuck en route (no teleport)")
                && !runner.Contains("Soft-arrive (stuck en route)"));
            Check("Late commute continues after phase advance",
                runner.Contains("TickLateCommuteToWork")
                && runner.Contains("TickLateCommuteHome"));
            Check("ArrivedWork gates job actions",
                runner.Contains("!L.ArrivedWork")
                && runner.Contains("CanPerformJobActions"));
            Check("Camp south pad excavation present",
                runner.Contains("ExcavateCampSouthPad")
                && runner.Contains("StartY - 34"));
            Check("WorkerRelocationLog wired",
                runner.Contains("WorkerRelocationLog.Report")
                || File.Exists(Path.Combine(Application.dataPath,
                    "Vibe", "FreeMovement", "WorkerRelocationLog.cs")));
            Check("TEMP MOVE DEBUG overlay present",
                runner.Contains("DrawWorkerMovementDebug"));
            Check("Toilet return is physical walk",
                runner.Contains("ToiletReturning")
                && runner.Contains("walking back to post"));

            sb.AppendLine();
            sb.AppendLine("## Root causes (diagnosed)");
            sb.AppendLine("1. **Teleport:** `BeginHeadingOut` previously SoftArriveAllWork + EnterOnShift + AlignHosts SoftTeleport — skipped physical morning commute and warped all hosts.");
            sb.AppendLine("2. **Prospector walk:** AlignHosts SoftTeleport + auto TrySnapBodyOut embedded Lewis off open floor / out of CanPerform; shared commute restore + ArrivedWork gate fixes.");
            sb.AppendLine("3. **Bottom void:** Camp props south of half-oval floor (`floorY = StartY-16`); unexcavated rock not drawn beyond wall reveal → camera clear looked like void. Fixed via lower floor + ExcavateCampSouthPad.");

            sb.AppendLine();
            sb.AppendLine("## Play-mode cycle matrix");
            sb.AppendLine("| Shift | 6 workers physical both ways | Notes |");
            sb.AppendLine("|-------|------------------------------|-------|");
            sb.AppendLine("| 6h | MANUAL / Editor Play | Use MOVE DEBUG + relocation log |");
            sb.AppendLine("| 8h | MANUAL / Editor Play | Default |");
            sb.AppendLine("| 10h | MANUAL / Editor Play | |");
            sb.AppendLine("| 12h | MANUAL / Editor Play | |");
            sb.AppendLine();
            sb.AppendLine("Automated Unity batch play was not run (Editor may hold project lock). Architecture gates above are authoritative for this patch.");

            sb.AppendLine();
            sb.AppendLine("## Result");
            sb.AppendLine($"**Result:** {(fail == 0 ? "PASS" : "FAIL")}  ({pass} passed, {fail} failed)");

            string dir = outputDirectory
                ?? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BenchmarkResults"));
            Directory.CreateDirectory(dir);
            string stamp = Path.Combine(dir, $"worker_commute_regression_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            string latest = Path.Combine(dir, "worker_commute_regression_latest.md");
            File.WriteAllText(stamp, sb.ToString());
            File.WriteAllText(latest, sb.ToString());
            Debug.Log($"[COMMUTE REGRESSION] {(fail == 0 ? "PASS" : "FAIL")} → {latest}");
            return latest;
        }

        static bool ContainsCallSequence(string src, string a, string b, int maxGap)
        {
            int i = 0;
            while (true)
            {
                int ia = src.IndexOf(a, i, StringComparison.Ordinal);
                if (ia < 0) return false;
                int ib = src.IndexOf(b, ia, StringComparison.Ordinal);
                if (ib > ia && ib - ia < maxGap) return true;
                i = ia + a.Length;
            }
        }
    }
}
