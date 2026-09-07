using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Builds Mission 01 geology, validates routes, writes BenchmarkResults report.</summary>
    public static class Mission01Audit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[MISSION 01] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.Exists(path) && File.ReadAllText(path).Contains("**Result:** FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var result = BuildAndValidate();
            var sb = new StringBuilder();
            sb.Append(result.ToReport());
            sb.AppendLine();
            sb.AppendLine("## Map design summary");
            sb.AppendLine("- Base: organic SocketMap mountain (bedrock lobes, early gold/diamond teases, gas field).");
            sb.AppendLine("- Overlay: west/east soft trunks → mid junction → dual approaches to deep Gold (NE) and Diamond (NW).");
            sb.AppendLine("- Wrong turns: direct-north bedrock ridge; diagonal gas pockets; chamber shortcut bedrock plugs.");
            sb.AppendLine("- Soft routes always exist around hazards; prospecting must interpret clues (no safe-path reveal).");
            sb.AppendLine("- Objective: refine ≥1 GOLD and ≥1 DIA before Day 14 shift end.");
            sb.AppendLine();
            sb.AppendLine($"- Gold chamber: {result.Layout.GoldChamber}");
            sb.AppendLine($"- Diamond chamber: {result.Layout.DiamondChamber}");
            sb.AppendLine($"- Junctions W/E/Mid: {result.Layout.JunctionWest} / {result.Layout.JunctionEast} / {result.Layout.JunctionMid}");

            string dir = outputDirectory
                ?? Path.Combine(Application.dataPath, "..", "BenchmarkResults");
            Directory.CreateDirectory(dir);
            string latest = Path.Combine(dir, "mission01_prospecting_latest.md");
            string stamped = Path.Combine(dir,
                $"mission01_prospecting_{System.DateTime.Now:yyyyMMdd_HHmmss}.md");
            File.WriteAllText(latest, sb.ToString());
            File.WriteAllText(stamped, sb.ToString());
            Debug.Log($"[MISSION 01] {(result.Passed ? "PASS" : "FAIL")} → {latest}");
            return latest;
        }

        public static Mission01Geology.ValidationResult BuildAndValidate()
        {
            const float cellSize = 0.1f;
            int tw = 360, th = 300;
            int sx = tw / 2;
            int sy = 42;
            var w = new FineTerrainWorld(tw, th, cellSize);

            for (int x = 0; x < tw; x++)
            {
                w.Set(x, 0, FineTerrainWorld.MakeBedrock());
                w.Set(x, th - 1, FineTerrainWorld.MakeBedrock());
            }
            for (int y = 0; y < th; y++)
            {
                w.Set(0, y, FineTerrainWorld.MakeBedrock());
                w.Set(tw - 1, y, FineTerrainWorld.MakeBedrock());
            }

            int floorY = sy - 16;
            GoldVeinPlacer.ExcavateHalfOval(w, sx, floorY, radiusX: 56, radiusY: 20);
            GoldVeinPlacer.ExcavateHalfOval(w, sx, sy - 2, radiusX: 18, radiusY: 10);
            GoldVeinPlacer.ExcavateHalfOval(w, sx, sy - 12, radiusX: 36, radiusY: 16);

            GoldVeinPlacer.BuildSocketMapStarterMaze(w, sx, sy);
            var layout = Mission01Geology.Apply(w, sx, sy);
            return Mission01Geology.Validate(w, layout);
        }
    }
}
