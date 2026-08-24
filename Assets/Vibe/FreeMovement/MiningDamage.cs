using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Deterministic excavator-vs-rock damage. Does not roll D20 —
    /// <see cref="WorkerRoll"/> is for discrete uncertain events only (e.g. Weak Point).
    /// </summary>
    public static class MiningDamage
    {
        /// <summary>Default excavator bit contribution to DigPower.</summary>
        public const int DefaultExcavatorToolPower = 10;

        /// <summary>Breakdown of one strike (debug / future UI).</summary>
        public readonly struct Result
        {
            public readonly int ToolPower;
            public readonly int RawPower;
            public readonly int Lithology;
            public readonly int RockArmor;
            public readonly int DigPower;
            /// <summary>Lithology × 0.5</summary>
            public readonly float BaseArmorPenetration;
            /// <summary>SpatialGeometry × 0.5 when the tile has an active Weak Point; else 0.</summary>
            public readonly float WeakPointArmorPenetration;
            /// <summary>Base + Weak Point penetration.</summary>
            public readonly float ArmorPenetration;
            public readonly float EffectiveArmor;
            public readonly int Damage;

            public Result(
                int toolPower,
                int rawPower,
                int lithology,
                int rockArmor,
                int digPower,
                float baseArmorPenetration,
                float weakPointArmorPenetration,
                float armorPenetration,
                float effectiveArmor,
                int damage)
            {
                ToolPower = toolPower;
                RawPower = rawPower;
                Lithology = lithology;
                RockArmor = rockArmor;
                DigPower = digPower;
                BaseArmorPenetration = baseArmorPenetration;
                WeakPointArmorPenetration = weakPointArmorPenetration;
                ArmorPenetration = armorPenetration;
                EffectiveArmor = effectiveArmor;
                Damage = damage;
            }

            public override string ToString() =>
                $"DigPower={DigPower} (tool {ToolPower}+raw {RawPower}) " +
                $"vs armor {RockArmor} (base pen {BaseArmorPenetration:0.#} + wp {WeakPointArmorPenetration:0.#} → eff {EffectiveArmor:0.#}) " +
                $"→ dmg {Damage}";
        }

        /// <summary>
        /// DigPower = ToolPower + RawPower
        /// BaseArmorPenetration = Lithology × 0.5
        /// WeakPointArmorPenetration = SpatialGeometry × 0.5 when tile has Weak Point (else 0)
        /// TotalArmorPenetration = Base + WeakPoint
        /// EffectiveArmor = max(0, RockArmor − TotalArmorPenetration)
        /// Damage = max(1, DigPower − EffectiveArmor)
        /// </summary>
        public static Result Compute(
            int toolPower,
            WorkerStats stats,
            int rockArmor,
            float weakPointArmorPenetration = 0f)
        {
            int rawPower = stats != null
                ? stats.Get(WorkerStatId.RawPower)
                : WorkerStats.Baseline;
            int lithology = stats != null
                ? stats.Get(WorkerStatId.Lithology)
                : WorkerStats.Baseline;

            int digPower = toolPower + rawPower;
            float basePen = lithology * 0.5f;
            float wpPen = Mathf.Max(0f, weakPointArmorPenetration);
            float totalPen = basePen + wpPen;
            float effectiveArmor = Mathf.Max(0f, rockArmor - totalPen);
            int damage = Mathf.Max(1, Mathf.FloorToInt(digPower - effectiveArmor));

            return new Result(
                toolPower,
                rawPower,
                lithology,
                rockArmor,
                digPower,
                basePen,
                wpPen,
                totalPen,
                effectiveArmor,
                damage);
        }

        public static int ComputeAmount(
            int toolPower,
            WorkerStats stats,
            int rockArmor,
            float weakPointArmorPenetration = 0f) =>
            Compute(toolPower, stats, rockArmor, weakPointArmorPenetration).Damage;

        /// <summary>
        /// Strike a concrete tile. Weak Point pen uses Spatial Geometry only when
        /// <see cref="TerrainCell.HasWeakPoint"/> is set.
        /// </summary>
        public static Result Compute(int toolPower, WorkerStats stats, in TerrainCell cell)
        {
            float wpPen = 0f;
            if (cell.HasWeakPoint && stats != null)
                wpPen = stats.Get(WorkerStatId.SpatialGeometry) * 0.5f;
            return Compute(toolPower, stats, cell.Armor, wpPen);
        }
    }
}
