using System;
using System.IO;
using DeepCore.FreeMovement;

public static class GeoDiagConsole
{
    public static int Main(string[] args)
    {
        string outDir = args.Length > 0
            ? args[0]
            : "/Users/esbenhjort/Documents/Projects/Tilemap_graphics_test/BenchmarkResults";
        Directory.CreateDirectory(outDir);
        string path = ProspectorGeoEvidenceDiagnostic.Run(outDir);
        Console.WriteLine("REPORT_PATH=" + path);
        Console.WriteLine(File.ReadAllText(Path.Combine(outDir, "geo_evidence_diagnostic_latest.md")));
        return 0;
    }
}
