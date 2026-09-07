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
            bool dialogueDepth = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--stage2") stage2 = true;
                if (args[i] == "--dialogue-depth") dialogueDepth = true;
                if (args[i] == "--out" && i + 1 < args.Length)
                    outDir = args[i + 1];
            }

            string path;
            if (dialogueDepth)
                path = SocialDialogueDepthAudit.Run(outDir);
            else if (stage2)
                path = SocialAuraStage2LogicAudit.Run(outDir);
            else
                path = SocialAuraStage0Sim.Run(outDir);

            Console.WriteLine($"Report: {path}");
            string text = System.IO.File.ReadAllText(path);
            // Stage2LogicAudit → RECOMMENDATION: READY; depth/other → INVARIANT: PASS
            bool ready = text.Contains("RECOMMENDATION: READY") || text.Contains("INVARIANT: PASS");
            Console.WriteLine(ready ? "VERDICT: READY" : "VERDICT: NOT READY");
            return ready ? 0 : 1;
        }
    }
}
