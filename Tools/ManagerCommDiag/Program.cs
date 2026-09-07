using System;
using DeepCore.FreeMovement;

namespace ManagerCommDiag
{
    static class Program
    {
        static int Main(string[] args)
        {
            string outDir = null;
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "--out" && i + 1 < args.Length)
                    outDir = args[i + 1];
            string path = ManagerCommAudit.Run(outDir);
            Console.WriteLine($"Report: {path}");
            string text = System.IO.File.ReadAllText(path);
            bool pass = text.Contains("**Result:** PASS");
            Console.WriteLine(pass ? "VERDICT: PASS" : "VERDICT: FAIL");
            foreach (var line in text.Split('\n'))
            {
                if (line.StartsWith("- ") || line.StartsWith("**Result")
                    || line.StartsWith("## ") || line.StartsWith("| "))
                    Console.WriteLine(line.TrimEnd());
            }
            return pass ? 0 : 1;
        }
    }
}
