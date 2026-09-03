using System;
using DeepCore.FreeMovement;

namespace FrustrationDiag
{
    static class Program
    {
        static int Main(string[] args)
        {
            string outDir = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--out" && i + 1 < args.Length)
                    outDir = args[i + 1];
            }

            // Reuse SocialAuraDiag Unity shim define for Mathf/Application
            string path = FrustrationLoopDiagnostic.Run(outDir);
            Console.WriteLine($"Report: {path}");
            string text = System.IO.File.ReadAllText(path);
            bool ready = text.Contains("RECOMMENDATION: READY")
                || text.Contains("RECOMMENDATION: MOSTLY READY");
            Console.WriteLine(ready ? "VERDICT: READY" : "VERDICT: NEEDS WORK");
            // Print key table for the console summary
            foreach (var line in text.Split('\n'))
            {
                if (line.StartsWith("| ") || line.StartsWith("## ") || line.StartsWith("- Lewis")
                    || line.StartsWith("- Mara") || line.StartsWith("- Elena")
                    || line.StartsWith("- Hard") || line.StartsWith("- Discovery")
                    || line.StartsWith("RECOMMENDATION") || line.StartsWith("- RESULT")
                    || line.StartsWith("- ProspectorDrySpell") || line.StartsWith("- Daytime")
                    || line.StartsWith("- Sleep") || line.StartsWith("- ProgressSuccess")
                    || line.StartsWith("- DrySpell"))
                    Console.WriteLine(line.TrimEnd());
            }
            return ready ? 0 : 1;
        }
    }
}
