#if SOCIAL_AURA_DIAG
namespace DeepCore.FreeMovement
{
    public enum RecruitmentTraitId : byte { None = 0 }

    public sealed class WorkerStateEventHistory
    {
        public System.Collections.Generic.List<object> Items { get; } = new();
    }

    /// <summary>Minimal LoosePile surface for terrain sampling offline.</summary>
    public static class LoosePile
    {
        public static bool IsOnLoose(UnityEngine.Vector2 terrainPos, float bodyRadius = 0.12f) => false;
    }

    public sealed class FineTerrainWorld
    {
        public float CellSize => 0.1f;
        public TunnelNavGrid Navigation => null;
        public UnityEngine.Vector2Int WorldToCell(UnityEngine.Vector2 world) =>
            new UnityEngine.Vector2Int(0, 0);
    }

    public sealed class TunnelNavGrid
    {
        public int GetClearance(int x, int y) => 8;
    }

    public static class WorkerStateEventHub
    {
        public static object Service;
        public static object Emit(WorkerStateEvent e) => null;
        public static object EmitForced(WorkerStateEvent e) => null;
    }
}
#endif
