using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Offline audit: Universal Rescue Integration V1 — JobType is never a permission gate.</summary>
    public static class UniversalRescueV1Audit
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoRunInBatch()
        {
            if (!Application.isBatchMode) return;
            if (!string.Equals(Environment.GetEnvironmentVariable("DEEPCORE_AUDIT"), "universal_rescue_v1",
                    StringComparison.OrdinalIgnoreCase))
                return;
            RunFromEditor();
        }

#if UNITY_EDITOR
        public static void RunFromEditor() => Run();
#endif

        public static void Run()
        {
            var sb = new StringBuilder(4096);
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                if (ok) { pass++; sb.AppendLine($"- PASS  {name}"); }
                else { fail++; sb.AppendLine($"- FAIL  {name}{(string.IsNullOrEmpty(detail) ? "" : " — " + detail)}"); }
            }

            sb.AppendLine("# Universal Rescue Integration V1 Audit");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            string runner = Read("FreeMovementSocketMapRunner.cs");
            string rescue = Read("WorkerRescue.cs");
            string collapse = Read("TunnelCollapse.cs");
            string camp = Read("CampLife.cs");

            sb.AppendLine("## Architecture (source)");
            Check("WorkerRescue / TaskId present",
                rescue.Contains("WorkerGenericTaskIds.Rescue") && rescue.Contains("CanAcceptRescueDuty"));
            Check("No JobType permission in CanAcceptRescueDuty",
                rescue.Contains("CanAcceptRescueDuty") && !ContainsJobTypeGate(rescue));
            Check("CampBody rescue flags",
                camp.Contains("RescueDutyActive") && camp.Contains("BeingRescued")
                && camp.Contains("RescueReturning"));
            Check("FinalizeRescueAtCamp handoff",
                collapse.Contains("FinalizeRescueAtCamp") && collapse.Contains("SeekingStewardCare"));
            Check("HasOpenTunnelPath public", collapse.Contains("HasOpenTunnelPath"));
            Check("Runner TickUniversalRescue",
                runner.Contains("TickUniversalRescue") && runner.Contains("PickUniversalRescuer"));
            Check("Prior Hauler/Engineer-only rescue removed",
                !runner.Contains("Rescue: hauler or engineer reaches incapacitated")
                && runner.Contains("PickUniversalRescuer"));
            Check("PersonTaskAuthority blocks host snap",
                runner.Contains("PersonTaskAuthority") && runner.Contains("SeekingStewardCare"));
            Check("ContributorIds list for future cooperation",
                rescue.Contains("ContributorIds"));

            sb.AppendLine();
            sb.AppendLine("## Eligibility — all prototype crew (no JobType gate)");
            var names = new[] { "Lewis", "Mara", "Kowalski", "Elena", "Viktor", "Kit" };
            var jobs = new[]
            {
                JobType.Prospecting, JobType.Excavation, JobType.Hauling,
                JobType.Refining, JobType.Engineering, JobType.Steward,
            };
            for (int i = 0; i < names.Length; i++)
            {
                var wr = new WorkerRuntime(100 + i, names[i]);
                bool can = WorkerRescue.CanAcceptRescueDuty(wr);
                Check($"{names[i]} CanAccept (assigned conceptually {jobs[i]})", can);
                float score = WorkerRescue.ScoreCandidate(wr, jobs[i], Vector2.zero, Vector2.right * 3f, true);
                Check($"{names[i]} ScoreCandidate > 0", score > 0f, $"score={score:0.00}");
            }

            sb.AppendLine();
            sb.AppendLine("## Performance differentiation (stats, not job lock)");
            var weak = new WorkerRuntime(201, "Weak");
            weak.Stats.Set(WorkerStatId.HeavyLifting, 4);
            weak.Stats.Set(WorkerStatId.RawPower, 4);
            weak.Stats.Set(WorkerStatId.Stamina, 5);
            weak.Stats.ClampAll();
            var strong = new WorkerRuntime(202, "Strong");
            strong.Stats.Set(WorkerStatId.HeavyLifting, 18);
            strong.Stats.Set(WorkerStatId.RawPower, 17);
            strong.Stats.Set(WorkerStatId.Stamina, 16);
            strong.Stats.ClampAll();
            float cw = WorkerRescue.CarrySpeedMul(weak);
            float cs = WorkerRescue.CarrySpeedMul(strong);
            Check("Strong carry mul > weak carry mul", cs > cw, $"weak={cw:0.00} strong={cs:0.00}");

            var incap = new WorkerRuntime(203, "Down");
            incap.State.MarkIncapacitated(1f, "audit");
            Check("Incapacitated cannot accept rescue duty", !WorkerRescue.CanAcceptRescueDuty(incap));
            Check("Incapacitated NeedsRescue", WorkerRescue.NeedsRescue(incap));

            sb.AppendLine();
            sb.AppendLine($"## Result");
            sb.AppendLine($"**Result:** {(fail == 0 ? "PASS" : "FAIL")}  ({pass} passed, {fail} failed)");

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BenchmarkResults"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "universal_rescue_integration_v1_audit_runtime.md");
            File.WriteAllText(path, sb.ToString());
            Debug.Log(sb.ToString());
            Debug.Log($"[UniversalRescueV1Audit] wrote {path}");
#if UNITY_EDITOR
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(fail > 0 ? 1 : 0);
#endif
        }

        static bool ContainsJobTypeGate(string rescueSrc)
        {
            // Fail if CanAccept body checks JobType equality as permission
            int idx = rescueSrc.IndexOf("CanAcceptRescueDuty", StringComparison.Ordinal);
            if (idx < 0) return true;
            int end = rescueSrc.IndexOf("NeedsRescue", idx, StringComparison.Ordinal);
            if (end < 0) end = Math.Min(rescueSrc.Length, idx + 1200);
            string body = rescueSrc.Substring(idx, end - idx);
            return body.Contains("JobType.") && body.Contains("return false");
        }

        static string Read(string file)
        {
            string p = Path.Combine(Application.dataPath, "Vibe", "FreeMovement", file);
            return File.Exists(p) ? File.ReadAllText(p) : "";
        }
    }
}
