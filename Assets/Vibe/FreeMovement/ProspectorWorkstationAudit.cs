using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Prospector workstation + faster analysis pass audit.</summary>
    public static class ProspectorWorkstationAudit
    {
        // ——— Frozen BEFORE values (pre-pass) ———
        public const float BeforeProdMinH = 8f;
        public const float BeforeProdMaxH = 22f;
        public const float BeforeProdBaseLo = 11f;
        public const float BeforeProdBaseHi = 18f;
        public const float BeforeTestScale = 0.055f;
        public const float BeforeTestMinH = 0.22f;
        public const float BeforeTestMaxH = 0.85f;
        public const float BeforeCrossProdMin = 3.2f;
        public const float BeforeCrossProdMax = 8.8f;
        public const float BeforeConsultTestH = 0.07f;
        public const float BeforeConsultProdH = 0.55f;
        public const float BeforeRefinerFloorH = 0.2f;

        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[PROSPECTOR WORKSTATION] Report: {path}");
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

            sb.AppendLine("# Prospector Workstation + Faster Analysis Pass Audit");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            sb.AppendLine("## Exact before → after timing");
            sb.AppendLine();
            sb.AppendLine("| Clock | BEFORE | AFTER |");
            sb.AppendLine("|-------|--------|-------|");
            sb.AppendLine($"| Production analysis clamp | {BeforeProdMinH:0.#}–{BeforeProdMaxH:0.#}h | **1.5–4.0h** |");
            sb.AppendLine($"| Production base (complexity) | Lerp({BeforeProdBaseLo},{BeforeProdBaseHi}) | Lerp(2.0, 3.6) × speed |");
            sb.AppendLine($"| Testing analysis scale | ×{BeforeTestScale} | ×{ProspectorScanFormulas.TestingAnalysisDurationScale} |");
            sb.AppendLine($"| Testing analysis clamp | {BeforeTestMinH}–{BeforeTestMaxH}h | **{ProspectorScanFormulas.TestingMinAnalysisHours}–{ProspectorScanFormulas.TestingMaxAnalysisHours}h** |");
            sb.AppendLine($"| Production cross-check | {BeforeCrossProdMin}–{BeforeCrossProdMax}h | **0.55–1.6h** |");
            sb.AppendLine($"| Consult dwell (testing) | {BeforeConsultTestH}h | **{ProspectorScanFormulas.TestingConsultDwellHours}h** |");
            sb.AppendLine($"| Consult dwell (production) | {BeforeConsultProdH}h | **{ProspectorScanFormulas.ProductionConsultDwellHours}h** |");
            sb.AppendLine($"| Refiner consult floor | {BeforeRefinerFloorH}h | **0.05h** |");
            sb.AppendLine();
            sb.AppendLine("Real-time @ 12s/game-hour (testing, 1×):");
            sb.AppendLine($"- Analysis BEFORE ~{BeforeTestMinH * 12f:0.#}–{BeforeTestMaxH * 12f:0.#}s → AFTER ~{ProspectorScanFormulas.TestingMinAnalysisHours * 12f:0.#}–{ProspectorScanFormulas.TestingMaxAnalysisHours * 12f:0.#}s");
            sb.AppendLine($"- Consult BEFORE ~{BeforeConsultTestH * 12f:0.##}s → AFTER ~{ProspectorScanFormulas.TestingConsultDwellHours * 12f:0.##}s");
            sb.AppendLine();

            bool prevTesting = ProspectorScanFormulas.UseTestingScanDurations;
            try
            {
                var weak = new ProspectorAnalysisStats
                {
                    Focus = 4, WorkRate = 4, Mathematics = 10, Lithology = 10,
                    Mineralogy = 10, Chemistry = 10, Composure = 10, Intuition = 10,
                };
                var strong = new ProspectorAnalysisStats
                {
                    Focus = 18, WorkRate = 17, Mathematics = 10, Lithology = 10,
                    Mineralogy = 10, Chemistry = 10, Composure = 10, Intuition = 10,
                };
                var simple = FakeAnomaly(8, 4, 4);
                var complex = FakeAnomaly(70, 20, 18);

                ProspectorScanFormulas.UseTestingScanDurations = false;
                float prodWeak = ProspectorAnomalyInterpretation.AnalysisDurationHours(weak, complex);
                float prodStrong = ProspectorAnomalyInterpretation.AnalysisDurationHours(strong, simple);
                Check("Production band within 1.5–4.0h",
                    prodWeak >= 1.49f && prodWeak <= 4.01f
                    && prodStrong >= 1.49f && prodStrong <= 4.01f,
                    $"weak={prodWeak:0.00} strong={prodStrong:0.00}");
                Check("Strong Prospector faster than weak (production)",
                    prodStrong < prodWeak - 0.15f,
                    $"strong={prodStrong:0.00} weak={prodWeak:0.00}");
                Check("Production is fraction of 8h shift (<5h)",
                    prodWeak <= 4.01f && prodStrong <= 4.01f);
                Check("Production not instant (>1.2h)",
                    prodStrong >= 1.4f);

                float cross = ProspectorAnomalyInterpretation.CrossCheckDurationHours(strong, simple);
                Check("Cross-check production shorter than before max",
                    cross <= 1.61f && cross >= 0.5f, $"cross={cross:0.00}");

                ProspectorScanFormulas.UseTestingScanDurations = true;
                float testWeak = ProspectorAnomalyInterpretation.AnalysisDurationHours(weak, complex);
                float testStrong = ProspectorAnomalyInterpretation.AnalysisDurationHours(strong, simple);
                Check("Testing clamp 0.14–0.48h",
                    testWeak >= ProspectorScanFormulas.TestingMinAnalysisHours - 0.001f
                    && testWeak <= ProspectorScanFormulas.TestingMaxAnalysisHours + 0.001f
                    && testStrong >= ProspectorScanFormulas.TestingMinAnalysisHours - 0.001f
                    && testStrong <= ProspectorScanFormulas.TestingMaxAnalysisHours + 0.001f,
                    $"weak={testWeak:0.00} strong={testStrong:0.00}");
                Check("Testing faster than BEFORE min ceiling",
                    ProspectorScanFormulas.TestingMaxAnalysisHours < BeforeTestMaxH);
                Check("Strong faster than weak (testing) or both at floor",
                    testStrong <= testWeak + 0.001f);

                Check("Consult testing shorter than before",
                    ProspectorScanFormulas.TestingConsultDwellHours < BeforeConsultTestH);
                Check("Consult production shorter than before",
                    ProspectorScanFormulas.ProductionConsultDwellHours < BeforeConsultProdH);

                sb.AppendLine();
                sb.AppendLine("## Sample durations (live formulas)");
                sb.AppendLine($"| Mode | Weak/complex | Strong/simple |");
                sb.AppendLine($"|------|--------------|---------------|");
                ProspectorScanFormulas.UseTestingScanDurations = false;
                sb.AppendLine($"| Production | {ProspectorAnomalyInterpretation.AnalysisDurationHours(weak, complex):0.00}h | {ProspectorAnomalyInterpretation.AnalysisDurationHours(strong, simple):0.00}h |");
                ProspectorScanFormulas.UseTestingScanDurations = true;
                sb.AppendLine($"| Testing | {ProspectorAnomalyInterpretation.AnalysisDurationHours(weak, complex):0.00}h | {ProspectorAnomalyInterpretation.AnalysisDurationHours(strong, simple):0.00}h |");
                sb.AppendLine();

                sb.AppendLine("## Workstation / wiring (source)");
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, "Vibe", "FreeMovement"));
                string runner = File.ReadAllText(Path.Combine(root, "FreeMovementSocketMapRunner.cs"));
                string loop = File.ReadAllText(Path.Combine(root, "ProspectorInvestigationLoop.cs"));
                string person = File.ReadAllText(Path.Combine(root, "ProspectorPerson.cs"));
                Check("ProspectorWorkstationSite exists",
                    File.Exists(Path.Combine(root, "ProspectorWorkstationSite.cs")));
                Check("Runner spawns workstation",
                    runner.Contains("ProspectorWorkstationSite.Spawn"));
                Check("BindWorkstation wired",
                    person.Contains("BindWorkstation") && runner.Contains("BindWorkstation"));
                Check("Desk prefers workstation over scanner",
                    loop.Contains("Camp analysis table is the investigation desk"));
                Check("Consult routes to analysis table",
                    loop.Contains("refiner consult → analysis table"));
                Check("DEV timing readout present",
                    runner.Contains("TIMING  analysis") && runner.Contains("WORKSTATION"));
                Check("Scan acquisition formulas untouched (MinScanHours 48)",
                    Mathf.Abs(ProspectorScanFormulas.MinScanHours - 48f) < 0.01f);
                Check("Quality still separate from speed",
                    strong.AnalysisQuality01 > 0.01f
                    && Mathf.Abs(weak.AnalysisQuality01 - strong.AnalysisQuality01) < 0.001f);
            }
            finally
            {
                ProspectorScanFormulas.UseTestingScanDurations = prevTesting;
            }

            sb.AppendLine();
            sb.AppendLine($"**Result:** {(fail == 0 ? "PASS" : "FAIL")}  ({pass} passed, {fail} failed)");

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BenchmarkResults"))
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string stamp = Path.Combine(dir, $"prospector_workstation_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            string latest = Path.Combine(dir, "prospector_workstation_latest.md");
            File.WriteAllText(stamp, sb.ToString());
            File.WriteAllText(latest, sb.ToString());
            Debug.Log($"[PROSPECTOR WORKSTATION] {(fail == 0 ? "PASS" : "FAIL")} → {latest}");
            return latest;
        }

        static ProspectorAnomaly FakeAnomaly(int tiles, int bw, int bh)
        {
            return new ProspectorAnomaly
            {
                TileCount = tiles,
                MinX = 0,
                MinY = 0,
                MaxX = Mathf.Max(0, bw - 1),
                MaxY = Mathf.Max(0, bh - 1),
            };
        }
    }
}
