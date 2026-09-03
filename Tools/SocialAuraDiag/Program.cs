using System;
using DeepCore.FreeMovement;

namespace SocialAuraDiag
{
    static class Program
    {
        static int Main(string[] args)
        {
            string outDir = null;
            bool stage2 = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--stage2") stage2 = true;
                if (args[i] == "--out" && i + 1 < args.Length)
                    outDir = args[i + 1];
            }

            string path = stage2
                ? SocialAuraStage2LogicAudit.Run(outDir)
                : SocialAuraStage0Sim.Run(outDir);
            Console.WriteLine($"Report: {path}");
            string text = System.IO.File.ReadAllText(path);
            bool ready = text.Contains("RECOMMENDATION: READY");
            Console.WriteLine(ready ? "VERDICT: READY" : "VERDICT: NOT READY");
            return ready ? 0 : 1;
        }
    }
}
