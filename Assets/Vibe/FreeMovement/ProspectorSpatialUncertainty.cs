using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Scan-stable spatial uncertainty for RAW evidence.
    /// Position = shared region translate (Spatial). Shape = modest clip + tiny edge push.
    /// Preserves neighbor connectivity so Tactical silhouettes stay readable masses.
    /// </summary>
    public static class ProspectorSpatialUncertainty
    {
        /// <summary>
        /// Large region so a whole geological body usually shares one offset
        /// (avoids tearing one formation into many mini-anomalies).
        /// </summary>
        public const int RegionCells = 28;

        public struct UncertaintyStats
        {
            public int SpatialGeometry;
            public int Calibration;
            public int Acoustics;
            public int ScanId;
            public float MaxRangeCells;
        }

        public struct WarpResult
        {
            public int BelievedX;
            public int BelievedY;
            public int TruthX;
            public int TruthY;
            public int OffsetX;
            public int OffsetY;
            public int BoundaryExtraX;
            public int BoundaryExtraY;
            public bool DroppedAsFalseNegative;
            public bool IsFalsePositive;
            public float SignalStrength;
            public float ObservationQuality;
            public ProspectorHiddenTruthKind HiddenKind;
        }

        struct RegionBelief
        {
            public int OffsetX;
            public int OffsetY;
            public float ClipNx;
            public float ClipNy;
            public float ClipThreshold;
            public int EdgePushX;
            public int EdgePushY;
            public int CenterX;
            public int CenterY;
            public float ShapeSkillAtRange;
        }

        public static bool TryBuildObservation(
            FineTerrainWorld world,
            int truthX, int truthY,
            float distanceCells,
            in UncertaintyStats stats,
            out WarpResult result)
        {
            result = default;
            result.TruthX = truthX;
            result.TruthY = truthY;

            float dist01 = Mathf.Clamp01(distanceCells / Mathf.Max(1f, stats.MaxRangeCells));
            float spatial01 = Stat01(stats.SpatialGeometry);
            float calib01 = Stat01(stats.Calibration);
            float acoustics01 = Stat01(stats.Acoustics);

            bool hasTruth = ProspectorDetection.TrySampleGroundTruth(
                world, truthX, truthY, out float rawSignal, out var hidden);

            if (hasTruth)
            {
                var ctx = new ProspectorDetectionContext(
                    stats.Acoustics, stats.Calibration,
                    distanceCells, stats.MaxRangeCells,
                    rawSignal, truthX, truthY, stats.ScanId);

                if (!ProspectorDetection.TryDetect(ctx, out float strength, out float quality))
                {
                    result.DroppedAsFalseNegative = true;
                    return false;
                }

                if (IsFalseNegative(stats.ScanId, truthX, truthY, strength, dist01, acoustics01, hidden))
                {
                    result.DroppedAsFalseNegative = true;
                    return false;
                }

                var region = BuildRegionBelief(truthX, truthY, dist01, spatial01, calib01, stats.ScanId);

                // Modest coherent incompleteness (far / low Calibration+Spatial) — drops a section, not pixels
                if (IsClipped(truthX, truthY, region, hidden))
                {
                    result.DroppedAsFalseNegative = true;
                    return false;
                }

                ProjectBelief(truthX, truthY, region, world, out int bx, out int by, out int ex, out int ey);

                result.BelievedX = bx;
                result.BelievedY = by;
                result.OffsetX = region.OffsetX;
                result.OffsetY = region.OffsetY;
                result.BoundaryExtraX = ex;
                result.BoundaryExtraY = ey;
                result.SignalStrength = strength;
                result.ObservationQuality = quality * Mathf.Lerp(0.82f, 1f, spatial01 * 0.5f + calib01 * 0.5f);
                result.HiddenKind = hidden;
                result.IsFalsePositive = false;
                return true;
            }

            // Extremely rare compact FP seed — solids should not look like loose rock
            if (!TryFalsePositiveBlob(world, truthX, truthY, dist01, acoustics01, calib01, stats.ScanId,
                    out float fpStrength, out float fpQuality))
                return false;

            var fpRegion = BuildRegionBelief(truthX, truthY, dist01, spatial01, calib01, stats.ScanId);
            ProjectBelief(truthX, truthY, fpRegion, world, out int fbx, out int fby, out int fex, out int fey);

            result.BelievedX = fbx;
            result.BelievedY = fby;
            result.OffsetX = fpRegion.OffsetX;
            result.OffsetY = fpRegion.OffsetY;
            result.BoundaryExtraX = fex;
            result.BoundaryExtraY = fey;
            result.SignalStrength = fpStrength;
            result.ObservationQuality = fpQuality;
            result.HiddenKind = ProspectorHiddenTruthKind.None;
            result.IsFalsePositive = true;
            return true;
        }

        static RegionBelief BuildRegionBelief(
            int truthX, int truthY,
            float dist01,
            float spatial01,
            float calib01,
            int scanId)
        {
            int rx = FloorDiv(truthX, RegionCells);
            int ry = FloorDiv(truthY, RegionCells);
            int centerX = rx * RegionCells + RegionCells / 2;
            int centerY = ry * RegionCells + RegionCells / 2;

            // ── 1) POSITION ERROR — Spatial Geometry; entire region translates as one ──
            float nOx = ProspectorDetection.StableNoise01(scanId, rx * 31, ry * 17);
            float nOy = ProspectorDetection.StableNoise01(scanId, rx * 19 + 7, ry * 41);
            float maxOffset = Mathf.Lerp(5.0f, 0.3f, spatial01);
            int offsetX = Mathf.RoundToInt((nOx * 2f - 1f) * maxOffset);
            int offsetY = Mathf.RoundToInt((nOy * 2f - 1f) * maxOffset);

            // ── 2) MODEST SHAPE — Calibration + Spatial (+ distance). No scale/shear scatter. ──
            float shapeSkill = spatial01 * 0.55f + calib01 * 0.45f;
            float skillAtRange = Mathf.Clamp01(shapeSkill * (1f - 0.22f * dist01));

            float nCx = ProspectorDetection.StableNoise01(scanId, rx * 3 + 1, ry * 5) * 2f - 1f;
            float nCy = ProspectorDetection.StableNoise01(scanId, rx * 5, ry * 3 + 1) * 2f - 1f;
            float cLen = Mathf.Sqrt(nCx * nCx + nCy * nCy);
            if (cLen < 0.001f) { nCx = 1f; nCy = 0f; cLen = 1f; }
            nCx /= cLen;
            nCy /= cLen;

            // High threshold = almost no clip. Only bite when skill is low / far.
            float clipAmount = (1f - skillAtRange) * (0.2f + 0.55f * dist01);
            float clipThreshold = Mathf.Lerp(RegionCells * 1.1f, RegionCells * 0.28f, clipAmount);

            // Tiny coherent edge push (0–1 tile) along clip axis — imperfect rim, not scatter
            int edgePush = 0;
            if (skillAtRange < 0.55f)
            {
                float ep = ProspectorDetection.StableNoise01(scanId, rx + 40, ry + 40);
                if (ep > 0.55f)
                    edgePush = ep > 0.78f ? 1 : -1;
            }

            return new RegionBelief
            {
                OffsetX = offsetX,
                OffsetY = offsetY,
                ClipNx = nCx,
                ClipNy = nCy,
                ClipThreshold = clipThreshold,
                EdgePushX = Mathf.RoundToInt(nCx * edgePush),
                EdgePushY = Mathf.RoundToInt(nCy * edgePush),
                CenterX = centerX,
                CenterY = centerY,
                ShapeSkillAtRange = skillAtRange,
            };
        }

        static bool IsClipped(
            int truthX, int truthY, in RegionBelief region, ProspectorHiddenTruthKind kind)
        {
            float lx = truthX - region.CenterX;
            float ly = truthY - region.CenterY;
            float d = lx * region.ClipNx + ly * region.ClipNy;

            // Gas: slightly more incomplete; solids: only outer clip bite
            float thresh = region.ClipThreshold;
            if (kind == ProspectorHiddenTruthKind.Gas)
                thresh *= 0.82f;
            else
                thresh *= 1.05f;

            return d > thresh;
        }

        /// <summary>
        /// Topology-preserving: believed = truth + shared offset (+ optional 1-tile rim push).
        /// No independent per-cell scale — that was the salt-and-pepper source.
        /// </summary>
        static void ProjectBelief(
            int truthX, int truthY,
            in RegionBelief region,
            FineTerrainWorld world,
            out int believedX, out int believedY,
            out int boundaryExtraX, out int boundaryExtraY)
        {
            float lx = truthX - region.CenterX;
            float ly = truthY - region.CenterY;
            float d = lx * region.ClipNx + ly * region.ClipNy;

            // Rim cells (near the clip plane) get the shared edge push — keeps shape coherent
            int ex = 0, ey = 0;
            float rimBand = Mathf.Lerp(2.5f, 0.8f, region.ShapeSkillAtRange);
            if (d > region.ClipThreshold - rimBand)
            {
                ex = region.EdgePushX;
                ey = region.EdgePushY;
            }

            believedX = truthX + region.OffsetX + ex;
            believedY = truthY + region.OffsetY + ey;
            boundaryExtraX = ex;
            boundaryExtraY = ey;

            if (world != null && !world.InBounds(believedX, believedY))
            {
                believedX = Mathf.Clamp(believedX, 0, world.Width - 1);
                believedY = Mathf.Clamp(believedY, 0, world.Height - 1);
            }
        }

        public static Vector2Int RegionOffset(int scanId, int truthX, int truthY, int spatialGeometry)
        {
            float spatial01 = Stat01(spatialGeometry);
            int rx = FloorDiv(truthX, RegionCells);
            int ry = FloorDiv(truthY, RegionCells);
            float nOx = ProspectorDetection.StableNoise01(scanId, rx * 31, ry * 17);
            float nOy = ProspectorDetection.StableNoise01(scanId, rx * 19 + 7, ry * 41);
            float maxOffset = Mathf.Lerp(5.0f, 0.3f, spatial01);
            return new Vector2Int(
                Mathf.RoundToInt((nOx * 2f - 1f) * maxOffset),
                Mathf.RoundToInt((nOy * 2f - 1f) * maxOffset));
        }

        static bool IsFalseNegative(
            int scanId, int x, int y, float strength, float dist01, float acoustics01,
            ProspectorHiddenTruthKind kind)
        {
            float weakness = 1f - Mathf.Clamp01(strength);
            float baseMiss = weakness * (1f - acoustics01) * (0.3f + 0.7f * dist01);

            // Gas may be diffuse; Gold/Bedrock stay filled masses
            float missChance = kind == ProspectorHiddenTruthKind.Gas
                ? baseMiss * 0.28f
                : baseMiss * 0.02f;

            float n = ProspectorDetection.StableNoise01(scanId ^ 0xA5A5, x * 13, y * 29);
            return n < missChance;
        }

        static bool TryFalsePositiveBlob(
            FineTerrainWorld world,
            int x, int y,
            float dist01,
            float acoustics01,
            float calib01,
            int scanId,
            out float strength,
            out float quality)
        {
            strength = 0f;
            quality = 0f;
            if (world == null || !world.InBounds(x, y)) return false;
            if (world.IsTunnelOpen(x, y) || world.IsGas(x, y)) return false;
            if (world.IsExcavated(x, y)) return false;
            if (world.Get(x, y).IsUndamageableBorder) return false;

            float seedChance = (1f - acoustics01) * 0.0012f * (0.15f + 0.85f * dist01);
            seedChance *= Mathf.Lerp(1.05f, 0.7f, calib01);

            int seedX = 0, seedY = 0;
            bool foundSeed = false;
            for (int dy = -1; dy <= 1 && !foundSeed; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int sx = x + dx, sy = y + dy;
                if (!world.InBounds(sx, sy)) continue;
                float sn = ProspectorDetection.StableNoise01(scanId ^ 0x5C5C, sx + 101, sy + 303);
                if (sn >= seedChance) continue;
                foundSeed = true;
                seedX = sx;
                seedY = sy;
            }
            if (!foundSeed) return false;

            int cheb = Mathf.Max(Mathf.Abs(x - seedX), Mathf.Abs(y - seedY));
            if (cheb > 1) return false;

            float fill = ProspectorDetection.StableNoise01(scanId, x * 17 + seedX, y * 19 + seedY);
            float need = cheb == 0 ? 1f : (x == seedX || y == seedY ? 0.45f : 0.18f);
            if (fill > need) return false;

            strength = Mathf.Lerp(0.14f, 0.28f, ProspectorDetection.StableNoise01(scanId, seedX, seedY));
            quality = Mathf.Lerp(0.1f, 0.3f, calib01) * (1f - 0.4f * dist01);
            return true;
        }

        static float Stat01(int v) => (WorkerStats.Clamp(v) - 1) / 18f;

        static int FloorDiv(int a, int b)
        {
            if (a >= 0) return a / b;
            return (a - b + 1) / b;
        }

        public static void ComputeBeliefErrorMetrics(
            System.Collections.Generic.IReadOnlyList<RawScanObservation> obs,
            out Vector2 truthCenter,
            out Vector2 believedCenter,
            out Vector2 meanOffset,
            out float meanBoundaryError,
            out float meanAbsOffset,
            out int sampleCount)
        {
            truthCenter = default;
            believedCenter = default;
            meanOffset = default;
            meanBoundaryError = 0f;
            meanAbsOffset = 0f;
            sampleCount = 0;
            if (obs == null || obs.Count == 0) return;

            float tx = 0, ty = 0, bx = 0, by = 0, ox = 0, oy = 0, be = 0, ao = 0;
            int n = 0;
            for (int i = 0; i < obs.Count; i++)
            {
                var o = obs[i];
                if (o.IsFalsePositive) continue;
                tx += o.TruthCellX + 0.5f;
                ty += o.TruthCellY + 0.5f;
                bx += o.CellX + 0.5f;
                by += o.CellY + 0.5f;
                ox += o.RegionOffsetX;
                oy += o.RegionOffsetY;
                be += Mathf.Abs(o.BoundaryExtraX) + Mathf.Abs(o.BoundaryExtraY);
                ao += Mathf.Abs(o.RegionOffsetX) + Mathf.Abs(o.RegionOffsetY);
                n++;
            }
            if (n == 0) return;
            sampleCount = n;
            float inv = 1f / n;
            truthCenter = new Vector2(tx * inv, ty * inv);
            believedCenter = new Vector2(bx * inv, by * inv);
            meanOffset = new Vector2(ox * inv, oy * inv);
            meanBoundaryError = be * inv;
            meanAbsOffset = ao * inv;
        }
    }
}
