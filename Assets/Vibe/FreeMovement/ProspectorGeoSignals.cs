using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Qualitative boundary class — independent of sharpness.
    /// Allows "sharp fracture-bound", "diffuse irregular", etc.
    /// </summary>
    public enum AnomalyBoundaryCharacter : byte
    {
        Diffuse = 0,
        FractureBound = 1,
        Irregular = 2,
        Layered = 3,
        Sharp = 4,
    }

    /// <summary>
    /// Material variant that nudges overlapping profiles (deterministic).
    /// </summary>
    public enum AnomalyMaterialVariant : byte
    {
        Nominal = 0,
        FracturedBedrock = 1,
        DenseMineralisation = 2,
        VeinMineralisation = 3,
        TrappedGasPocket = 4,
        MixedContact = 5,
    }

    /// <summary>
    /// Hidden or observed physical survey traits (0..1 continuous + discrete boundary class).
    /// Player never sees raw floats.
    /// </summary>
    public struct AnomalyGeoSignals
    {
        public float ReturnStrength;
        public float Attenuation;
        public float Conductivity;
        public float StructuralCoherence;
        public AnomalyBoundaryCharacter BoundaryCharacter;
        /// <summary>How crisp the leading edge is — orthogonal to character class.</summary>
        public float BoundarySharpness01;

        public void ClampAll()
        {
            ReturnStrength = Mathf.Clamp01(ReturnStrength);
            Attenuation = Mathf.Clamp01(Attenuation);
            Conductivity = Mathf.Clamp01(Conductivity);
            StructuralCoherence = Mathf.Clamp01(StructuralCoherence);
            BoundarySharpness01 = Mathf.Clamp01(BoundarySharpness01);
        }
    }

    /// <summary>
    /// Progressive Prospector reading of hidden signals. Traits unlock by work type.
    /// </summary>
    public struct AnomalyObservedSignals
    {
        public AnomalyGeoSignals Values;
        public bool HasReturn;
        public bool HasAttenuation;
        public bool HasConductivity;
        public bool HasCoherence;
        public bool HasBoundary;
        public float ObservationQuality01;

        /// <summary>Desk vs field agreement (−1 contradict … +1 support). Soft flag.</summary>
        public float FieldMatch01;
        public bool HasFieldMatch;

        /// <summary>Loose rock vs scan agreement.</summary>
        public float LooseRockMatch01;
        public bool HasLooseRockMatch;

        /// <summary>Refiner mineralisation relevance (−1 hazard lean … +1 mineral lean).</summary>
        public float MineralRelevance01;
        public bool HasMineralRelevance;

        public bool HasContradictionFlag;
        public int TraitsKnownCount
        {
            get
            {
                int n = 0;
                if (HasReturn) n++;
                if (HasAttenuation) n++;
                if (HasConductivity) n++;
                if (HasCoherence) n++;
                if (HasBoundary) n++;
                return n;
            }
        }
    }
}
