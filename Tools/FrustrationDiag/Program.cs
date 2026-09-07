using System;
using DeepCore.FreeMovement;

namespace FrustrationDiag
{
    static class Program
    {
        static int Main(string[] args)
        {
            string outDir = null;
            bool tuning = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--tuning") tuning = true;
                if (args[i] == "--out" && i + 1 < args.Length)
                    outDir = args[i + 1];
            }

            string path = tuning
                ? FrustrationTuningAudit.Run(outDir)
                : FrustrationLoopDiagnostic.Run(outDir);
            Console.WriteLine($"Report: {path}");
            string text = System.IO.File.ReadAllText(path);
            bool ready = tuning
                ? text.Contains("INVARIANT: PASS")
                : text.Contains("RECOMMENDATION: READY")
                  || text.Contains("RECOMMENDATION: MOSTLY READY")
                  || text.Contains("INVARIANT: PASS");
            Console.WriteLine(ready ? "VERDICT: READY" : "VERDICT: NEEDS WORK");
            if (tuning)
            {
                foreach (var line in text.Split('\n'))
                {
                    if (line.StartsWith("PASS |") || line.StartsWith("FAIL |")
                        || line.StartsWith("## ") || line.StartsWith("INVARIANT")
                        || line.StartsWith("PASS "))
                        Console.WriteLine(line.TrimEnd());
                }
            }
            return ready ? 0 : 1;
        }
    }
}
