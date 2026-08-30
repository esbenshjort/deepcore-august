using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Internal Ground-Truth kind used only for Stage-4+ analysis hooks.
    /// Never exposed through player-facing evidence APIs or Tactical View.
    /// </summary>
    public enum ProspectorHiddenTruthKind : byte
    {
        None = 0,
        Bedrock = 1,
        Gold = 2,
        Gas = 3,
        Diamond = 4,
    }

    /// <summary>
    /// Inputs for anomaly detection. Stage 2 is threshold-based;
    /// Stage 4 can add false positives/negatives without rewriting callers.
    /// </summary>
    public readonly struct ProspectorDetectionContext
    {
        public readonly int Acoustics;
        public readonly int Calibration;
        public readonly float DistanceCells;
        public readonly float MaxRangeCells;
        /// <summary>Anonymous signal 0..1 from ground truth intensity — not a material label.</summary>
        public readonly float RawSignal01;
        public readonly int CellX;
        public readonly int CellY;
        public readonly int ScanId;

        public ProspectorDetectionContext(
            int acoustics, int calibration,
            float distanceCells, float maxRangeCells,
            float rawSignal01, int cellX, int cellY, int scanId)
        {
            Acoustics = acoustics;
            Calibration = calibration;
            DistanceCells = distanceCells;
            MaxRangeCells = maxRangeCells;
            RawSignal01 = rawSignal01;
            CellX = cellX;
            CellY = cellY;
            ScanId = scanId;
        }
    }

    /// <summary>
    /// Stage-2 detection. Acoustics = primary detect · Calibration = secondary quality.
    /// No per-frame Random — pass/fail is deterministic from context.
    /// </summary>
    public static class ProspectorDetection
    {
#if !GEO_DIAG_CONSOLE
        /// <summary>
        /// Map Ground Truth cell → anonymous raw signal + hidden kind (analysis only).
        /// Returns false when the cell has no Stage-2 anomaly source.
        /// </summary>
        public static bool TrySampleGroundTruth(
            FineTerrainWorld world, int x, int y,
            out float rawSignal01,
            out ProspectorHiddenTruthKind hiddenKind)
        {
            rawSignal01 = 0f;
            hiddenKind = ProspectorHiddenTruthKind.None;
            if (world == null || !world.InBounds(x, y)) return false;

            if (world.IsGas(x, y))
            {
                hiddenKind = ProspectorHiddenTruthKind.Gas;
                rawSignal01 = 0.92f;
                return true;
            }

            var cell = world.Get(x, y);
            if (cell.IsUndamageableBorder) return false;
            if (world.IsTunnelOpen(x, y)) return false;
            if (world.IsExcavated(x, y)) return false;

            if (cell.DiamondCount > 0)
            {
                hiddenKind = ProspectorHiddenTruthKind.Diamond;
                rawSignal01 = Mathf.Clamp01(0.48f + cell.DiamondCount * 0.13f);
                return true;
            }

            if (cell.GoldCount > 0)
            {
                hiddenKind = ProspectorHiddenTruthKind.Gold;
                rawSignal01 = Mathf.Clamp01(0.42f + cell.GoldCount * 0.14f);
                return true;
            }

            if (cell.BedrockCount >= 2)
            {
                hiddenKind = ProspectorHiddenTruthKind.Bedrock;
                rawSignal01 = cell.BedrockCount >= 3 ? 0.78f : 0.58f;
                return true;
            }

            return false;
        }
#endif

        /// <summary>
        /// Decide whether a raw anomaly signal becomes player evidence.
        /// Higher Acoustics lowers the detection threshold (weak signals more likely).
        /// Calibration lifts observation quality. No classification.
        /// </summary>
        public static bool TryDetect(
            in ProspectorDetectionContext ctx,
            out float signalStrength,
            out float observationQuality)
        {
            float acoustics01 = (WorkerStats.Clamp(ctx.Acoustics) - 1) / 18f;
            float calib01 = (WorkerStats.Clamp(ctx.Calibration) - 1) / 18f;
            float range = Mathf.Max(1f, ctx.MaxRangeCells);
            float dist01 = Mathf.Clamp01(ctx.DistanceCells / range);
            float distFalloff = 1f - 0.45f * dist01;

            // Primary: Acoustics improves effective sensitivity
            float sensitivity = 0.55f + acoustics01 * 0.45f;
            float effective = Mathf.Clamp01(ctx.RawSignal01 * distFalloff * sensitivity);

            // Higher Acoustics → lower threshold (detect weaker returns)
            float threshold = Mathf.Lerp(0.40f, 0.16f, acoustics01);

            observationQuality = Mathf.Clamp01(
                (0.32f + calib01 * 0.48f + acoustics01 * 0.2f) * distFalloff);
            signalStrength = effective;

            // Stage-4 hook + Stage spatial FN: StableNoise used by ProspectorSpatialUncertainty
            _ = StableNoise01(ctx.ScanId, ctx.CellX, ctx.CellY);

            return effective + 0.0001f >= threshold;
        }

        /// <summary>Deterministic 0..1 noise for future false-positive / false-negative models.</summary>
        public static float StableNoise01(int scanId, int x, int y)
        {
            unchecked
            {
                uint h = (uint)(scanId * 73856093) ^ (uint)(x * 19349663) ^ (uint)(y * 83492791);
                h ^= h >> 13;
                h *= 1274126177u;
                return (h & 0xFFFF) / 65535f;
            }
        }
    }
}
