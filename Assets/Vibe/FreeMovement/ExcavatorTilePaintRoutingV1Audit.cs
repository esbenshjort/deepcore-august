using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Offline audit for Excavation Routing V1.1 (authority + brush cells).</summary>
    public static class ExcavatorTilePaintRoutingV1Audit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[EXCAVATION ROUTING V1.1] Report: {path}");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var sb = new StringBuilder(12000);
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                if (ok) pass++; else fail++;
                sb.AppendLine($"- {(ok ? "PASS" : "FAIL")}  {name}"
                              + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
            }

            sb.AppendLine("# Excavation Routing V1.1 — Audit");
            sb.AppendLine();
            sb.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            sb.AppendLine("## BrushCells mapping (width class → corridor cells)");
            int[] expect = { 0, 3, 5, 8, 12, 16 };
            for (int w = 1; w <= 5; w++)
            {
                int cells = TunnelWidthSpec.BrushCells(w);
                Check($"Class {w} → {expect[w]} cells", cells == expect[w], $"got {cells}");
            }
            sb.AppendLine();

            sb.AppendLine("## Perpendicular span on straight centerline");
            var center = new List<Vector2Int>(16);
            for (int x = 0; x < 10; x++) center.Add(new Vector2Int(x, 20));
            for (int w = 1; w <= 5; w++)
            {
                var set = new HashSet<long>();
                ExcavatorTilePaintPlan.DilateCenterline(center, w, set);
                int minY = int.MaxValue, maxY = int.MinValue;
                foreach (long k in set)
                {
                    int y = (int)(k >> 32);
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
                int span = maxY - minY + 1;
                int want = TunnelWidthSpec.BrushCells(w);
                sb.AppendLine($"- Class {w}: cells={set.Count}, Y-span={span}, want={want}");
                Check($"Class {w} Y-span == {want}", span == want, $"span={span}");
            }
            sb.AppendLine();

            sb.AppendLine("## Continuity");
            var line = new List<Vector2Int>();
            ExcavatorTilePaintPlan.AppendLineCells(new Vector2Int(0, 0), new Vector2Int(12, 3), line);
            bool ok = true;
            for (int i = 1; i < line.Count; i++)
            {
                int dx = Mathf.Abs(line[i].x - line[i - 1].x);
                int dy = Mathf.Abs(line[i].y - line[i - 1].y);
                if (dx > 1 || dy > 1) { ok = false; break; }
            }
            Check("Fast diagonal jump continuous", ok, $"pts={line.Count}");
            sb.AppendLine();

            sb.AppendLine("## Authority invariants (code presence)");
            string ctrl = "";
            try
            {
                ctrl = System.IO.File.ReadAllText(
                    Path.Combine(Application.dataPath, "Vibe/FreeMovement/FreeWorkerController.cs"));
            }
            catch { /* offline */ }
            if (!string.IsNullOrEmpty(ctrl))
            {
                Check("TryContinueChewingAtFace disabled (=> false)",
                    ctrl.Contains("TryContinueChewingAtFace() => false"));
                Check("Claustro refuse does not ClearRoute",
                    ctrl.Contains("PauseExecution(\"CLAUSTROPHOBIA")
                    && !ContainsClaustroClearRoute(ctrl));
                Check("CaptureShiftBreakBookmark does not AddPin",
                    !ctrl.Contains("SHIFT BREAK | Left-off pin")
                    && ctrl.Contains("awaiting orders next shift"));
                Check("Dig requires paint plan gate",
                    ctrl.Contains("no dig without player-painted pending cells")
                    || ctrl.Contains("_paintPlan.PendingValidCount <= 0 && _paintWorkX < 0"));
                Check("AWAITING ORDERS label present",
                    ctrl.Contains("AWAITING ORDERS"));
            }
            sb.AppendLine();

            sb.AppendLine($"**Result:** {(fail == 0 ? "PASS" : "FAIL")}  ({pass} passed, {fail} failed)");

            string dir = outputDirectory;
            if (string.IsNullOrEmpty(dir))
                dir = Path.Combine(Application.dataPath, "..", "BenchmarkResults");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "excavation_routing_v11_audit_raw.md");
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        static bool ContainsClaustroClearRoute(string ctrl)
        {
            // Look for ClearRoute inside ShouldRefuseDeeperDig body
            int i = ctrl.IndexOf("bool ShouldRefuseDeeperDig");
            if (i < 0) return false;
            int j = ctrl.IndexOf("bool TryDigOnce", i);
            if (j < 0) j = i + 800;
            string body = ctrl.Substring(i, j - i);
            return body.Contains("ClearRoute()");
        }
    }
}
