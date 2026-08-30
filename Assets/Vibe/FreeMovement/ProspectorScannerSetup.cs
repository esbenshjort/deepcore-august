using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Heavy field scanner lifecycle. Stage 1–2: Packed → SettingUp → Ready → Scanning.
    /// Later stages can extend with Analysing without rewriting callers.
    /// </summary>
    public enum ProspectorScannerState : byte
    {
        Packed = 0,
        SettingUp = 1,
        Ready = 2,
        Scanning = 3,
        // Future:
        // Analysing = 4,
    }

    /// <summary>
    /// Equipment capability vs worker ability. Stats never expand past these physical limits.
    /// </summary>
    [System.Serializable]
    public struct ProspectorScannerEquipmentSpec
    {
        /// <summary>Temporary Stage-1 survey cone length (cells).</summary>
        public float MaxRangeCells;
        /// <summary>Half-angle of planned survey cone (degrees).</summary>
        public float ConeHalfAngleDeg;

        public static ProspectorScannerEquipmentSpec Default => new()
        {
            // Area ∝ range² · angle — √2 range ≈ 2× physical scan area (shape preserved)
            MaxRangeCells = 56f * 1.41421356f, // ≈ 79.2
            ConeHalfAngleDeg = 52f,
        };
    }

    /// <summary>
    /// Centralized setup-duration formula. Reads WorkerStats only — no local duplicates.
    /// Target band: excellent ~1h · competent ~2–3h · poor ~5–6h (game hours).
    /// </summary>
    public static class ProspectorScannerSetup
    {
        public const float MinHours = 1f;
        public const float MaxHours = 6f;

        /// <summary>
        /// SetupHours = clamp(6.2 − Mechanics×0.24 − HeavyLifting×0.06, 1, 6).
        /// Mechanics is primary assembly/alignment; Heavy Lifting secondary kit handling.
        /// </summary>
        public static float SetupDurationHours(WorkerStats stats)
        {
            int mechanics = stats != null ? stats.Get(WorkerStatId.Mechanics) : WorkerStats.Baseline;
            int lift = stats != null ? stats.Get(WorkerStatId.HeavyLifting) : WorkerStats.Baseline;
            return SetupDurationHours(mechanics, lift);
        }

        public static float SetupDurationHours(int mechanics, int heavyLifting)
        {
            mechanics = WorkerStats.Clamp(mechanics);
            heavyLifting = WorkerStats.Clamp(heavyLifting);
            float hours = 6.2f - mechanics * 0.24f - heavyLifting * 0.06f;
            return Mathf.Clamp(hours, MinHours, MaxHours);
        }
    }
}
