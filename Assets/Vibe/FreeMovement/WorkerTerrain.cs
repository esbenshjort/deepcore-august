using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Walk-surface kinds for person locomotion (not a full physics sim).</summary>
    public enum WorkerTerrainKind : byte
    {
        Normal = 0,
        LooseRock = 1,
        Rough = 2,
        SteepUneven = 3,
        Rubble = 4,
        WetSlippery = 5,
    }

    /// <summary>Sampled footing at a world position.</summary>
    public readonly struct WorkerTerrainSample
    {
        public readonly WorkerTerrainKind Kind;
        /// <summary>0 = clear tunnel, 1 = worst footing in this model.</summary>
        public readonly float Difficulty01;
        public readonly int Clearance;
        public readonly bool OnLooseRock;

        public WorkerTerrainSample(
            WorkerTerrainKind kind, float difficulty01, int clearance, bool onLooseRock)
        {
            Kind = kind;
            Difficulty01 = Mathf.Clamp01(difficulty01);
            Clearance = clearance;
            OnLooseRock = onLooseRock;
        }

        public static WorkerTerrainSample Normal =>
            new WorkerTerrainSample(WorkerTerrainKind.Normal, 0f, 8, false);
    }

    /// <summary>
    /// Reads world + loose piles into a small terrain difficulty model for walking.
    /// </summary>
    public static class WorkerTerrainSampler
    {
        public static WorkerTerrainSample Sample(
            FineTerrainWorld world, Vector2 worldPos, float bodyRadius = 0.12f)
        {
            bool loose = LoosePile.IsOnLoose(worldPos, bodyRadius);
            int clearance = 8;
            if (world != null)
            {
                var cell = world.WorldToCell(worldPos);
                if (world.Navigation != null)
                    clearance = world.Navigation.GetClearance(cell.x, cell.y);
            }

            WorkerTerrainKind kind;
            float diff;

            if (loose && clearance <= 2)
            {
                kind = WorkerTerrainKind.Rubble;
                diff = 0.92f;
            }
            else if (loose)
            {
                kind = WorkerTerrainKind.LooseRock;
                diff = 0.72f;
            }
            else if (clearance <= 1)
            {
                kind = WorkerTerrainKind.SteepUneven;
                diff = 0.78f;
            }
            else if (clearance <= 2)
            {
                kind = WorkerTerrainKind.Rough;
                diff = 0.48f;
            }
            else
            {
                kind = WorkerTerrainKind.Normal;
                diff = clearance <= 3 ? 0.12f : 0f;
            }

            return new WorkerTerrainSample(kind, diff, clearance, loose);
        }

        /// <summary>Offline / audit constructor without world.</summary>
        public static WorkerTerrainSample ForKind(WorkerTerrainKind kind, bool loose = false)
        {
            return kind switch
            {
                WorkerTerrainKind.LooseRock =>
                    new WorkerTerrainSample(kind, 0.72f, 6, true),
                WorkerTerrainKind.Rough =>
                    new WorkerTerrainSample(kind, 0.48f, 2, loose),
                WorkerTerrainKind.SteepUneven =>
                    new WorkerTerrainSample(kind, 0.78f, 1, loose),
                WorkerTerrainKind.Rubble =>
                    new WorkerTerrainSample(kind, 0.92f, 1, true),
                WorkerTerrainKind.WetSlippery =>
                    new WorkerTerrainSample(kind, 0.68f, 5, false),
                _ => WorkerTerrainSample.Normal,
            };
        }
    }
}
