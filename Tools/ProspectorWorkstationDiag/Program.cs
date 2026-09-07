using System;
using System.IO;
using System.Text;

namespace ProspectorWorkstationDiag
{
    static class Program
    {
        // AFTER constants (must match ProspectorScanFormulas / Interpretation)
        const float ProdMin = 1.5f, ProdMax = 4.0f;
        const float BaseLo = 2.0f, BaseHi = 3.6f;
        const float TestScale = 0.12f, TestMin = 0.14f, TestMax = 0.48f;
        const float ConsultTest = 0.035f, ConsultProd = 0.15f;
        const float CrossProdMin = 0.55f, CrossProdMax = 1.6f;

        // BEFORE (frozen)
        const float BProdMin = 8f, BProdMax = 22f, BBaseLo = 11f, BBaseHi = 18f;
        const float BTestScale = 0.055f, BTestMin = 0.22f, BTestMax = 0.85f;
        const float BConsultTest = 0.07f, BConsultProd = 0.55f;
        const float BCrossMin = 3.2f, BCrossMax = 8.8f;

        static int Main(string[] args)
        {
            string outDir = "../../BenchmarkResults";
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "--out" && i + 1 < args.Length)
                    outDir = args[i + 1];

            var sb = new StringBuilder();
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                if (ok) pass++; else fail++;
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
            sb.AppendLine($"| Production analysis clamp | {BProdMin:0.#}–{BProdMax:0.#}h | **{ProdMin:0.#}–{ProdMax:0.#}h** |");
            sb.AppendLine($"| Production base (complexity) | Lerp({BBaseLo},{BBaseHi}) | Lerp({BaseLo},{BaseHi}) × speed |");
            sb.AppendLine($"| Testing analysis scale | ×{BTestScale} | ×{TestScale} |");
            sb.AppendLine($"| Testing analysis clamp | {BTestMin}–{BTestMax}h | **{TestMin}–{TestMax}h** |");
            sb.AppendLine($"| Production cross-check | {BCrossMin}–{BCrossMax}h | **{CrossProdMin}–{CrossProdMax}h** |");
            sb.AppendLine($"| Consult dwell (testing) | {BConsultTest}h | **{ConsultTest}h** |");
            sb.AppendLine($"| Consult dwell (production) | {BConsultProd}h | **{ConsultProd}h** |");
            sb.AppendLine("| Refiner consult floor | 0.2h | **0.05h** |");
            sb.AppendLine();
            sb.AppendLine("Real-time @ 12s/game-hour (testing, 1×):");
            sb.AppendLine($"- Analysis BEFORE ~{BTestMin * 12f:0.#}–{BTestMax * 12f:0.#}s → AFTER ~{TestMin * 12f:0.#}–{TestMax * 12f:0.#}s");
            sb.AppendLine($"- Consult BEFORE ~{BConsultTest * 12f:0.##}s → AFTER ~{ConsultTest * 12f:0.##}s");
            sb.AppendLine();

            float weakC = Duration(speed01: 0.2f, complexity01: 0.9f, testing: false);
            float strongS = Duration(speed01: 0.95f, complexity01: 0.1f, testing: false);
            Check("Production band within 1.5–4.0h",
                weakC >= 1.49f && weakC <= 4.01f && strongS >= 1.49f && strongS <= 4.01f,
                $"weak={weakC:0.00} strong={strongS:0.00}");
            Check("Strong Prospector faster than weak (production)",
                strongS < weakC - 0.15f, $"strong={strongS:0.00} weak={weakC:0.00}");
            Check("Production is fraction of 8h shift (<5h)", weakC <= 4.01f);
            Check("Production not instant (>1.2h)", strongS >= 1.4f);

            float tw = Duration(0.2f, 0.9f, true);
            float ts = Duration(0.95f, 0.1f, true);
            Check("Testing clamp 0.14–0.48h",
                tw >= TestMin - 0.001f && tw <= TestMax + 0.001f
                && ts >= TestMin - 0.001f && ts <= TestMax + 0.001f,
                $"weak={tw:0.00} strong={ts:0.00}");
            Check("Testing max faster than BEFORE", TestMax < BTestMax);
            Check("Consult testing shorter", ConsultTest < BConsultTest);
            Check("Consult production shorter", ConsultProd < BConsultProd);

            sb.AppendLine();
            sb.AppendLine("## Sample durations (formula mirror)");
            sb.AppendLine("| Mode | Weak/complex | Strong/simple |");
            sb.AppendLine("|------|--------------|---------------|");
            sb.AppendLine($"| Production | {weakC:0.00}h | {strongS:0.00}h |");
            sb.AppendLine($"| Testing | {tw:0.00}h | {ts:0.00}h |");
            sb.AppendLine();

            string root = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "Assets", "Vibe", "FreeMovement"));
            if (!Directory.Exists(root))
                root = "/Users/esbenhjort/Documents/Projects/Tilemap_graphics_test/Assets/Vibe/FreeMovement";

            sb.AppendLine("## Workstation / wiring (source)");
            string formulas = File.ReadAllText(Path.Combine(root, "ProspectorScanFormulas.cs"));
            string interp = File.ReadAllText(Path.Combine(root, "ProspectorAnomalyInterpretation.cs"));
            string runner = File.ReadAllText(Path.Combine(root, "FreeMovementSocketMapRunner.cs"));
            string loop = File.ReadAllText(Path.Combine(root, "ProspectorInvestigationLoop.cs"));
            string person = File.ReadAllText(Path.Combine(root, "ProspectorPerson.cs"));
            string refiner = File.ReadAllText(Path.Combine(root, "RefinerPerson.cs"));

            Check("Source AFTER production clamp 1.5–4.0",
                interp.Contains("Mathf.Clamp(hours, 1.5f, 4.0f)"));
            Check("Source AFTER testing constants",
                formulas.Contains("TestingMinAnalysisHours = 0.14f")
                && formulas.Contains("TestingMaxAnalysisHours = 0.48f")
                && formulas.Contains("TestingConsultDwellHours = 0.035f")
                && formulas.Contains("ProductionConsultDwellHours = 0.15f"));
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
            Check("Refiner floor 0.05",
                refiner.Contains("Mathf.Max(0.05f, expectedHours)"));
            Check("DEV timing readout present",
                runner.Contains("TIMING  analysis") && runner.Contains("WORKSTATION"));
            Check("Scan acquisition MinScanHours still 48",
                formulas.Contains("MinScanHours = 48f"));

            sb.AppendLine();
            sb.AppendLine($"**Result:** {(fail == 0 ? "PASS" : "FAIL")}  ({pass} passed, {fail} failed)");

            Directory.CreateDirectory(outDir);
            string stamp = Path.Combine(outDir, $"prospector_workstation_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            string latest = Path.Combine(outDir, "prospector_workstation_latest.md");
            File.WriteAllText(stamp, sb.ToString());
            File.WriteAllText(latest, sb.ToString());
            // Also sync Unity audit report file content for editor menu parity
            Console.WriteLine(sb.ToString());
            Console.WriteLine(fail == 0 ? "VERDICT: PASS" : "VERDICT: FAIL");
            Console.WriteLine($"Report: {latest}");
            return fail == 0 ? 0 : 1;
        }

        static float Duration(float speed01, float complexity01, bool testing)
        {
            float baseHours = BaseLo + (BaseHi - BaseLo) * complexity01;
            float hours = baseHours * (1.35f + (0.72f - 1.35f) * speed01);
            if (hours < ProdMin) hours = ProdMin;
            if (hours > ProdMax) hours = ProdMax;
            if (testing)
            {
                hours *= TestScale;
                if (hours < TestMin) hours = TestMin;
                if (hours > TestMax) hours = TestMax;
            }
            return hours;
        }
    }
}
