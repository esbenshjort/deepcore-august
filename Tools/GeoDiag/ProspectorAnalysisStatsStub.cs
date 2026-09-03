#if GEO_DIAG_CONSOLE
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Console duplicate of ProspectorAnalysisStats (from ProspectorAnomalyInterpretation)
    /// so GeoDiag does not pull scan-duration / equipment dependencies.
    /// Keep in sync with the Unity source struct.
    /// </summary>
    public struct ProspectorAnalysisStats
    {
        public int Mathematics;
        public int Lithology;
        public int Mineralogy;
        public int Chemistry;
        public int Composure;
        public int Intuition;
        public int Focus;
        public int WorkRate;
        public int Calibration;
        public int Acoustics;
        public int SpatialGeometry;

        public static ProspectorAnalysisStats From(WorkerStats stats) => new()
        {
            Mathematics = stats != null ? stats.Get(WorkerStatId.Mathematics) : WorkerStats.Baseline,
            Lithology = stats != null ? stats.Get(WorkerStatId.Lithology) : WorkerStats.Baseline,
            Mineralogy = stats != null ? stats.Get(WorkerStatId.Mineralogy) : WorkerStats.Baseline,
            Chemistry = stats != null ? stats.Get(WorkerStatId.Chemistry) : WorkerStats.Baseline,
            Composure = stats != null ? stats.Get(WorkerStatId.Composure) : WorkerStats.Baseline,
            Intuition = stats != null ? stats.Get(WorkerStatId.Intuition) : WorkerStats.Baseline,
            Focus = stats != null ? stats.Get(WorkerStatId.Focus) : WorkerStats.Baseline,
            WorkRate = stats != null ? stats.Get(WorkerStatId.WorkRate) : WorkerStats.Baseline,
            Calibration = stats != null ? stats.Get(WorkerStatId.Calibration) : WorkerStats.Baseline,
            Acoustics = stats != null ? stats.Get(WorkerStatId.Acoustics) : WorkerStats.Baseline,
            SpatialGeometry = stats != null ? stats.Get(WorkerStatId.SpatialGeometry) : WorkerStats.Baseline,
        };

        public float AnalysisSpeed01
        {
            get
            {
                float f = (WorkerStats.Clamp(Focus) - 1) / 18f;
                float w = (WorkerStats.Clamp(WorkRate) - 1) / 18f;
                return Mathf.Clamp01(f * 0.7f + w * 0.3f);
            }
        }

        public float AnalysisQuality01
        {
            get
            {
                float m = (WorkerStats.Clamp(Mathematics) - 1) / 18f;
                float l = (WorkerStats.Clamp(Lithology) - 1) / 18f;
                float n = (WorkerStats.Clamp(Mineralogy) - 1) / 18f;
                float c = (WorkerStats.Clamp(Chemistry) - 1) / 18f;
                float p = (WorkerStats.Clamp(Composure) - 1) / 18f;
                float i = (WorkerStats.Clamp(Intuition) - 1) / 18f;
                return Mathf.Clamp01(m * 0.22f + l * 0.18f + n * 0.18f + c * 0.18f + p * 0.14f + i * 0.1f);
            }
        }
    }
}
#endif
