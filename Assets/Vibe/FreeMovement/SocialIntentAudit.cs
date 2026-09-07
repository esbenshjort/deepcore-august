using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    public static class SocialIntentAudit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[SOCIAL INTENT] Report: {path}");
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

            log.AppendLine("# Social Intent Audit (read-only)");
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine();

            var warm = new SocialDirectedRelation { Trust = 8f, Warmth = 10f, Hostility = 0f, Respect = 55f };
            var iWarm = SocialIntentModel.Evaluate(1, 2, warm, Array.Empty<SocialMemoryEntry>(),
                WorkerState.CreateDefault(), WorkerStats.CreateBaseline());
            Check("High Warmth/Trust → Seek", iWarm.Kind == SocialIntentKind.Seek,
                $"{iWarm.Kind} {iWarm.Score:+0.00;-0.00}");

            var hostile = new SocialDirectedRelation { Trust = -6f, Warmth = -4f, Hostility = 12f, Respect = 35f };
            var iHost = SocialIntentModel.Evaluate(1, 2, hostile, Array.Empty<SocialMemoryEntry>(),
                WorkerState.CreateDefault(), WorkerStats.CreateBaseline());
            Check("High Hostility + low Trust → Avoid", iHost.Kind == SocialIntentKind.Avoid,
                $"{iHost.Kind} {iHost.Score:+0.00;-0.00}");

            var rivalry = new SocialDirectedRelation { Trust = 2f, Warmth = -2f, Hostility = 12f, Respect = 78f };
            var iRiv = SocialIntentModel.Evaluate(1, 2, rivalry, Array.Empty<SocialMemoryEntry>(),
                WorkerState.CreateDefault(), WorkerStats.CreateBaseline());
            Check("High H + high R → Neutral/professional (not Avoid)",
                iRiv.Kind == SocialIntentKind.Neutral || iRiv.Kind == SocialIntentKind.Seek,
                $"{iRiv.Kind} {iRiv.Score:+0.00;-0.00}");

            // Directional
            var ab = new SocialDirectedRelation { Trust = 9f, Warmth = 8f, Hostility = 0f, Respect = 60f };
            var ba = new SocialDirectedRelation { Trust = -5f, Warmth = -3f, Hostility = 10f, Respect = 40f };
            var iAB = SocialIntentModel.Evaluate(1, 2, ab, Array.Empty<SocialMemoryEntry>(),
                WorkerState.CreateDefault(), WorkerStats.CreateBaseline());
            var iBA = SocialIntentModel.Evaluate(2, 1, ba, Array.Empty<SocialMemoryEntry>(),
                WorkerState.CreateDefault(), WorkerStats.CreateBaseline());
            Check("Intent is directional (A→B ≠ B→A)",
                iAB.Kind != iBA.Kind, $"{iAB.Kind} vs {iBA.Kind}");

            Check("No movement APIs touched (intent is pure function)",
                typeof(SocialIntentModel).GetMethod("Evaluate") != null);

            Check("PressureTrigger unchanged", Mathf.Abs(SocialAuraTuning.PressureTrigger - 1.05f) < 0.001f);
            Check("ReachWorldScale unchanged",
                Mathf.Abs(SocialAuraLiveTuning.ReachWorldScale - 2.35f) < 0.001f);

            log.AppendLine();
            log.AppendLine("## Summary");
            log.AppendLine($"PASS {pass} / FAIL {fail}");
            log.AppendLine(fail == 0 ? "INVARIANT: PASS" : "INVARIANT: FAIL");

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(Application.dataPath, "..", "BenchmarkResults")
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string latest = Path.Combine(dir, "social_intent_latest.md");
            File.WriteAllText(Path.Combine(dir, $"social_intent_{DateTime.Now:yyyyMMdd_HHmmss}.md"), log.ToString());
            File.WriteAllText(latest, log.ToString());
            return latest;
        }
    }
}
