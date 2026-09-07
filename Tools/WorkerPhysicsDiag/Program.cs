using System;
using DeepCore.FreeMovement;

namespace WorkerPhysicsDiag
{
    static class Program
    {
        static int Main(string[] args)
        {
            string outDir = null;
            bool injury = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--out" && i + 1 < args.Length)
                    outDir = args[i + 1];
                if (args[i] == "--injury") injury = true;
            }

            string path = injury
                ? WorkerInjuryPhysicsAudit.Run(outDir)
                : WorkerPhysicsV1Audit.Run(outDir);
            Console.WriteLine($"Report: {path}");
            string text = System.IO.File.ReadAllText(path);
            bool pass = text.Contains("**Result:** PASS");
            Console.WriteLine(pass ? "VERDICT: PASS" : "VERDICT: FAIL");
            foreach (var line in text.Split('\n'))
            {
                if (line.StartsWith("- FAIL") || line.StartsWith("- PASS")
                    || line.StartsWith("**Result") || line.StartsWith("## ")
                    || line.StartsWith("| Agility") || line.StartsWith("| Balance")
                    || line.StartsWith("- Audit"))
                    Console.WriteLine(line.TrimEnd());
            }
            return pass ? 0 : 1;
        }
    }
}
