using System.IO;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Round 9 death/balance entry — delegates to <see cref="SocialConflictAudit.Run"/>.
    /// Primary report: BenchmarkResults/social_conflict_death_latest.md
    /// </summary>
    public static class SocialConflictDeathAudit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[SOCIAL CONFLICT DEATH] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.Exists(path) && File.ReadAllText(path).Contains("INVARIANT: FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null) =>
            SocialConflictAudit.Run(outputDirectory);
    }
}
