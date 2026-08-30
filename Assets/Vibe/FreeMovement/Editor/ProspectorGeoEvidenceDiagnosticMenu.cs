using UnityEditor;
using UnityEngine;
using DeepCore.FreeMovement;

namespace DeepCore.FreeMovement.EditorTools
{
    /// <summary>Menu entry for geological evidence diagnostic (debug only).</summary>
    public static class ProspectorGeoEvidenceDiagnosticMenu
    {
        [MenuItem("Deep Core/Diagnostics/Run Geo Evidence Diagnostic")]
        public static void Run()
        {
            string path = ProspectorGeoEvidenceDiagnostic.Run();
            Debug.Log($"[GEO DIAG] Wrote {path}");
            EditorUtility.RevealInFinder(path);
        }
    }
}
