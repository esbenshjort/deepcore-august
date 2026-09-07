#if SOCIAL_AURA_DIAG
namespace DeepCore.FreeMovement
{
    public struct AnomalyGeoSignals { }
    public struct AnomalyObservedSignals { }
    public enum ProspectorHiddenTruthKind : byte { None = 0 }
    public enum AnomalyMaterialVariant : byte { None = 0 }
    public struct ProspectorScannerEquipmentSpec
    {
        public float MaxRangeCells;
        public float ConeHalfAngleDeg;
    }
}
#endif
