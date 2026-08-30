using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Centralized material profile ranges + freeze-time hidden signal sampling.
    /// Overlapping ranges by design — no single trait uniquely IDs a material.
    /// Tune here only.
    /// </summary>
    public static class ProspectorGeoSignalProfiles
    {
        public struct TraitRange
        {
            public float Min, Max;
            public TraitRange(float min, float max) { Min = min; Max = max; }
            public float Sample(float u01) => Mathf.Lerp(Min, Max, Mathf.Clamp01(u01));
        }

        public struct MaterialProfile
        {
            public TraitRange ReturnStrength;
            public TraitRange Attenuation;
            public TraitRange Conductivity;
            public TraitRange StructuralCoherence;
            public TraitRange BoundarySharpness;
            /// <summary>Weights for Diffuse / FractureBound / Irregular / Layered / Sharp (sum need not be 1).</summary>
            public float WDiffuse, WFracture, WIrregular, WLayered, WSharp;
        }

        // ——— Tunables: bedrock ———
        public static readonly MaterialProfile Bedrock = new()
        {
            ReturnStrength = new(0.62f, 0.92f),
            Attenuation = new(0.18f, 0.52f),
            Conductivity = new(0.22f, 0.58f),
            StructuralCoherence = new(0.58f, 0.92f),
            BoundarySharpness = new(0.55f, 0.95f),
            WDiffuse = 0.05f, WFracture = 0.12f, WIrregular = 0.18f, WLayered = 0.35f, WSharp = 0.40f,
        };

        // ——— Tunables: gold-bearing mineralisation (ore zone) ———
        public static readonly MaterialProfile Mineralisation = new()
        {
            ReturnStrength = new(0.48f, 0.82f),
            Attenuation = new(0.35f, 0.68f),
            Conductivity = new(0.48f, 0.88f),
            StructuralCoherence = new(0.38f, 0.72f),
            BoundarySharpness = new(0.35f, 0.78f),
            WDiffuse = 0.08f, WFracture = 0.15f, WIrregular = 0.38f, WLayered = 0.28f, WSharp = 0.12f,
        };

        // ——— Tunables: gas ———
        public static readonly MaterialProfile Gas = new()
        {
            ReturnStrength = new(0.28f, 0.78f),
            Attenuation = new(0.55f, 0.92f),
            Conductivity = new(0.18f, 0.55f),
            StructuralCoherence = new(0.12f, 0.48f),
            BoundarySharpness = new(0.08f, 0.55f),
            WDiffuse = 0.42f, WFracture = 0.28f, WIrregular = 0.18f, WLayered = 0.08f, WSharp = 0.08f,
        };

        /// <summary>
        /// Sample hidden signals for a frozen anomaly from tile tallies + silhouette shape.
        /// Deterministic from ScanId / AnomalyId.
        /// </summary>
        public static void SampleHidden(ProspectorAnomaly a)
        {
            if (a == null) return;

            ResolveDominant(a, out var dominant, out var secondary, out float mix01);
            var variant = PickVariant(a, dominant);
            a.DominantMaterial = dominant;
            a.MaterialVariant = variant;

            var primary = ProfileOf(dominant);
            var signals = SampleProfile(primary, a.ScanId, a.AnomalyId, salt: 0);

            if (secondary != ProspectorHiddenTruthKind.None && mix01 > 0.15f)
            {
                var other = SampleProfile(ProfileOf(secondary), a.ScanId, a.AnomalyId, salt: 17);
                float t = Mathf.Clamp01(mix01);
                signals.ReturnStrength = Mathf.Lerp(signals.ReturnStrength, other.ReturnStrength, t * 0.45f);
                signals.Attenuation = Mathf.Lerp(signals.Attenuation, other.Attenuation, t * 0.45f);
                signals.Conductivity = Mathf.Lerp(signals.Conductivity, other.Conductivity, t * 0.45f);
                signals.StructuralCoherence = Mathf.Lerp(signals.StructuralCoherence, other.StructuralCoherence, t * 0.45f);
                signals.BoundarySharpness01 = Mathf.Lerp(signals.BoundarySharpness01, other.BoundarySharpness01, t * 0.4f);
                if (t > 0.35f
                    && Noise(a.ScanId, a.AnomalyId, 91) > 0.55f)
                    signals.BoundaryCharacter = other.BoundaryCharacter;
            }

            ApplyVariant(ref signals, variant, a.ScanId, a.AnomalyId);
            ApplyShapeBias(ref signals, a);
            signals.ClampAll();
            a.HiddenSignals = signals;
            a.ObservedSignals = default;
        }

        static MaterialProfile ProfileOf(ProspectorHiddenTruthKind k) => k switch
        {
            ProspectorHiddenTruthKind.Bedrock => Bedrock,
            ProspectorHiddenTruthKind.Gold => Mineralisation,
            ProspectorHiddenTruthKind.Diamond => Mineralisation,
            ProspectorHiddenTruthKind.Gas => Gas,
            _ => Bedrock,
        };

        static void ResolveDominant(
            ProspectorAnomaly a,
            out ProspectorHiddenTruthKind dominant,
            out ProspectorHiddenTruthKind secondary,
            out float mix01)
        {
            int bed = a.HiddenBedrockTiles;
            int gold = a.HiddenGoldTiles;
            int gas = a.HiddenGasTiles;
            int total = bed + gold + gas;
            dominant = ProspectorHiddenTruthKind.None;
            secondary = ProspectorHiddenTruthKind.None;
            mix01 = 0f;
            if (total <= 0)
            {
                // No tallies — infer from geometry weakly toward rock
                dominant = ProspectorHiddenTruthKind.Bedrock;
                return;
            }

            // Rank
            int best = bed; dominant = ProspectorHiddenTruthKind.Bedrock;
            if (gold > best) { best = gold; dominant = ProspectorHiddenTruthKind.Gold; }
            if (gas > best) { best = gas; dominant = ProspectorHiddenTruthKind.Gas; }

            int second = 0;
            if (dominant != ProspectorHiddenTruthKind.Bedrock && bed > second)
            { second = bed; secondary = ProspectorHiddenTruthKind.Bedrock; }
            if (dominant != ProspectorHiddenTruthKind.Gold && gold > second)
            { second = gold; secondary = ProspectorHiddenTruthKind.Gold; }
            if (dominant != ProspectorHiddenTruthKind.Gas && gas > second)
            { second = gas; secondary = ProspectorHiddenTruthKind.Gas; }

            mix01 = second > 0 ? second / (float)total : 0f;
        }

        static AnomalyMaterialVariant PickVariant(ProspectorAnomaly a, ProspectorHiddenTruthKind dominant)
        {
            float area = Mathf.Max(1f, a.BoundsWidth * a.BoundsHeight);
            float density = a.TileCount / area;
            float u = Noise(a.ScanId, a.AnomalyId, 3);

            if (a.HiddenBedrockTiles > 0 && a.HiddenGasTiles > 0 && u > 0.55f)
                return AnomalyMaterialVariant.MixedContact;
            if (a.HiddenGoldTiles > 0 && a.HiddenBedrockTiles > 0 && u > 0.6f)
                return AnomalyMaterialVariant.MixedContact;

            switch (dominant)
            {
                case ProspectorHiddenTruthKind.Bedrock:
                    if (a.GeometryQuality < 0.45f || density < 0.4f || u > 0.72f)
                        return AnomalyMaterialVariant.FracturedBedrock;
                    break;
                case ProspectorHiddenTruthKind.Gold:
                case ProspectorHiddenTruthKind.Diamond:
                    if (a.BoundsWidth >= a.BoundsHeight * 2 || a.BoundsHeight >= a.BoundsWidth * 2)
                        return AnomalyMaterialVariant.VeinMineralisation;
                    if (density > 0.55f && a.MeanSignalStrength > 0.55f)
                        return AnomalyMaterialVariant.DenseMineralisation;
                    break;
                case ProspectorHiddenTruthKind.Gas:
                    if (a.GeometryQuality > 0.55f || a.MaxSignalStrength > 0.7f)
                        return AnomalyMaterialVariant.TrappedGasPocket;
                    break;
            }
            return AnomalyMaterialVariant.Nominal;
        }

        static AnomalyGeoSignals SampleProfile(in MaterialProfile p, int scanId, int anomalyId, int salt)
        {
            var s = new AnomalyGeoSignals
            {
                ReturnStrength = p.ReturnStrength.Sample(Noise(scanId, anomalyId, 10 + salt)),
                Attenuation = p.Attenuation.Sample(Noise(scanId, anomalyId, 20 + salt)),
                Conductivity = p.Conductivity.Sample(Noise(scanId, anomalyId, 30 + salt)),
                StructuralCoherence = p.StructuralCoherence.Sample(Noise(scanId, anomalyId, 40 + salt)),
                BoundarySharpness01 = p.BoundarySharpness.Sample(Noise(scanId, anomalyId, 50 + salt)),
                BoundaryCharacter = SampleBoundaryClass(p, Noise(scanId, anomalyId, 60 + salt)),
            };
            return s;
        }

        static AnomalyBoundaryCharacter SampleBoundaryClass(in MaterialProfile p, float u)
        {
            float sum = p.WDiffuse + p.WFracture + p.WIrregular + p.WLayered + p.WSharp;
            if (sum < 0.0001f) return AnomalyBoundaryCharacter.Irregular;
            float x = u * sum;
            if (x < p.WDiffuse) return AnomalyBoundaryCharacter.Diffuse;
            x -= p.WDiffuse;
            if (x < p.WFracture) return AnomalyBoundaryCharacter.FractureBound;
            x -= p.WFracture;
            if (x < p.WIrregular) return AnomalyBoundaryCharacter.Irregular;
            x -= p.WIrregular;
            if (x < p.WLayered) return AnomalyBoundaryCharacter.Layered;
            return AnomalyBoundaryCharacter.Sharp;
        }

        static void ApplyVariant(ref AnomalyGeoSignals s, AnomalyMaterialVariant v, int scanId, int anomalyId)
        {
            switch (v)
            {
                case AnomalyMaterialVariant.FracturedBedrock:
                    s.StructuralCoherence = Mathf.Clamp01(s.StructuralCoherence - 0.20f);
                    s.Attenuation = Mathf.Clamp01(s.Attenuation + 0.10f);
                    s.BoundaryCharacter = Noise(scanId, anomalyId, 71) > 0.5f
                        ? AnomalyBoundaryCharacter.Irregular
                        : AnomalyBoundaryCharacter.FractureBound;
                    s.BoundarySharpness01 = Mathf.Clamp01(s.BoundarySharpness01 - 0.12f);
                    break;
                case AnomalyMaterialVariant.DenseMineralisation:
                    s.ReturnStrength = Mathf.Clamp01(s.ReturnStrength + 0.10f);
                    s.StructuralCoherence = Mathf.Clamp01(s.StructuralCoherence + 0.12f);
                    s.BoundarySharpness01 = Mathf.Clamp01(s.BoundarySharpness01 + 0.10f);
                    if (s.BoundaryCharacter == AnomalyBoundaryCharacter.Diffuse)
                        s.BoundaryCharacter = AnomalyBoundaryCharacter.Layered;
                    break;
                case AnomalyMaterialVariant.VeinMineralisation:
                    s.StructuralCoherence = Mathf.Clamp01(s.StructuralCoherence - 0.08f);
                    s.BoundaryCharacter = AnomalyBoundaryCharacter.Irregular;
                    break;
                case AnomalyMaterialVariant.TrappedGasPocket:
                    s.BoundarySharpness01 = Mathf.Clamp01(s.BoundarySharpness01 + 0.35f);
                    s.ReturnStrength = Mathf.Clamp01(s.ReturnStrength + 0.15f);
                    if (s.BoundaryCharacter == AnomalyBoundaryCharacter.Diffuse)
                        s.BoundaryCharacter = AnomalyBoundaryCharacter.FractureBound;
                    break;
                case AnomalyMaterialVariant.MixedContact:
                    s.BoundaryCharacter = AnomalyBoundaryCharacter.Irregular;
                    s.StructuralCoherence = Mathf.Clamp01(s.StructuralCoherence - 0.06f);
                    break;
            }
        }

        static void ApplyShapeBias(ref AnomalyGeoSignals s, ProspectorAnomaly a)
        {
            float area = Mathf.Max(1f, a.BoundsWidth * a.BoundsHeight);
            float density = Mathf.Clamp01(a.TileCount / area);
            // Sparse silhouette → slightly less coherence / softer edge
            if (density < 0.35f)
            {
                s.StructuralCoherence = Mathf.Clamp01(s.StructuralCoherence - 0.06f);
                s.BoundarySharpness01 = Mathf.Clamp01(s.BoundarySharpness01 - 0.05f);
            }
            if (a.GeometryQuality < 0.4f)
                s.BoundarySharpness01 = Mathf.Clamp01(s.BoundarySharpness01 - 0.08f);
            if (a.MeanSignalStrength > 0.65f)
                s.ReturnStrength = Mathf.Clamp01(Mathf.Max(s.ReturnStrength, a.MeanSignalStrength * 0.85f));
        }

        static float Noise(int scanId, int anomalyId, int salt) =>
            ProspectorDetection.StableNoise01(scanId, anomalyId * 31 + salt, salt * 17);

        /// <summary>
        /// Diagnostic-only: force dominant material + variant while using real profile sampling
        /// and ApplyVariant / ApplyShapeBias. Does not change production freeze path.
        /// </summary>
        public static void SampleHiddenForced(
            ProspectorAnomaly a,
            ProspectorHiddenTruthKind dominant,
            AnomalyMaterialVariant variant,
            int sampleIndex)
        {
            if (a == null) return;
            a.DominantMaterial = dominant;
            a.MaterialVariant = variant;
            a.ScanId = Mathf.Max(1, a.ScanId);
            a.AnomalyId = Mathf.Max(1, a.AnomalyId > 0 ? a.AnomalyId : sampleIndex + 1);

            // Seed tallies for mixed / shape bias consistency
            switch (dominant)
            {
                case ProspectorHiddenTruthKind.Bedrock:
                    a.HiddenBedrockTiles = 20;
                    a.HiddenGoldTiles = variant == AnomalyMaterialVariant.MixedContact ? 8 : 0;
                    a.HiddenGasTiles = 0;
                    break;
                case ProspectorHiddenTruthKind.Gold:
                case ProspectorHiddenTruthKind.Diamond:
                    a.HiddenGoldTiles = 18;
                    a.HiddenBedrockTiles = variant == AnomalyMaterialVariant.MixedContact ? 8 : 2;
                    a.HiddenGasTiles = 0;
                    break;
                case ProspectorHiddenTruthKind.Gas:
                    a.HiddenGasTiles = 16;
                    a.HiddenBedrockTiles = variant == AnomalyMaterialVariant.TrappedGasPocket
                                           || variant == AnomalyMaterialVariant.MixedContact
                        ? 6
                        : 0;
                    a.HiddenGoldTiles = 0;
                    break;
                default:
                    a.HiddenBedrockTiles = 10;
                    a.HiddenGoldTiles = 10;
                    a.HiddenGasTiles = 0;
                    break;
            }

            if (variant == AnomalyMaterialVariant.MixedContact && dominant == ProspectorHiddenTruthKind.Gas)
            {
                a.HiddenGasTiles = 12;
                a.HiddenBedrockTiles = 10;
            }

            var primary = ProfileOf(dominant == ProspectorHiddenTruthKind.None
                ? ProspectorHiddenTruthKind.Bedrock
                : dominant);
            var signals = SampleProfile(primary, a.ScanId, a.AnomalyId, salt: sampleIndex % 23);
            ApplyVariant(ref signals, variant, a.ScanId, a.AnomalyId);
            ApplyShapeBias(ref signals, a);
            signals.ClampAll();
            a.HiddenSignals = signals;
            a.ObservedSignals = default;
        }
    }
}
