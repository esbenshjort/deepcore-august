using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Outcome of one explicit D20 + stat check. No crit effects yet — naturals are flags only.
    /// </summary>
    public readonly struct WorkerRollResult
    {
        /// <summary>Raw die face, 1–20.</summary>
        public readonly int D20;

        /// <summary>Which worker attribute was added (if any).</summary>
        public readonly WorkerStatId StatId;

        /// <summary>Stat value used in the total (1–20), or 0 if none.</summary>
        public readonly int StatValue;

        /// <summary>Sum of optional situational modifiers (can be negative).</summary>
        public readonly int Modifiers;

        /// <summary>D20 + StatValue + Modifiers.</summary>
        public readonly int Total;

        /// <summary>Difficulty class that was checked against.</summary>
        public readonly int DC;

        /// <summary>True when Total &gt;= DC.</summary>
        public readonly bool Success;

        /// <summary>Raw die showed 1 (flag only — no auto-fail / crit effects yet).</summary>
        public readonly bool NaturalOne;

        /// <summary>Raw die showed 20 (flag only — no auto-success / crit effects yet).</summary>
        public readonly bool NaturalTwenty;

        public WorkerRollResult(
            int d20,
            WorkerStatId statId,
            int statValue,
            int modifiers,
            int total,
            int dc,
            bool success,
            bool naturalOne,
            bool naturalTwenty)
        {
            D20 = d20;
            StatId = statId;
            StatValue = statValue;
            Modifiers = modifiers;
            Total = total;
            DC = dc;
            Success = success;
            NaturalOne = naturalOne;
            NaturalTwenty = naturalTwenty;
        }

        public override string ToString()
        {
            string verdict = Success ? "SUCCESS" : "FAIL";
            string nat = NaturalOne ? " [NAT 1]" : NaturalTwenty ? " [NAT 20]" : "";
            return $"d20={D20} + {StatId}({StatValue}) + mod({Modifiers}) = {Total} vs DC {DC} → {verdict}{nat}";
        }
    }

    /// <summary>
    /// Centralized D20 / DC checks for all worker mechanics.
    /// Call only when a gameplay system explicitly wants a roll — never from Update/tick loops.
    /// </summary>
    public static class WorkerRoll
    {
        public const int DieMin = 1;
        public const int DieMax = 20;

        static System.Random _seededRng;
        static bool _useSeeded;

        /// <summary>
        /// Begin deterministic rolls for balance benchmarks. Does not affect UnityEngine.Random
        /// (visuals / non-combat). Call <see cref="EndSeeded"/> when the run finishes.
        /// </summary>
        public static void BeginSeeded(int seed)
        {
            _seededRng = new System.Random(seed);
            _useSeeded = true;
        }

        public static void EndSeeded()
        {
            _useSeeded = false;
            _seededRng = null;
        }

        public static bool IsSeeded => _useSeeded;

        /// <summary>
        /// RollTotal = d20 + relevantStat + modifiers. Success if RollTotal &gt;= DC.
        /// </summary>
        public static WorkerRollResult Check(
            WorkerStats stats,
            WorkerStatId stat,
            int difficultyClass,
            int modifiers = 0)
        {
            int d20 = RollD20();
            return Resolve(d20, stats, stat, difficultyClass, modifiers);
        }

        /// <summary>
        /// Same as <see cref="Check"/> but uses a provided die face (tests / seeded debug).
        /// Die is clamped to 1–20.
        /// </summary>
        public static WorkerRollResult CheckWithDie(
            int d20,
            WorkerStats stats,
            WorkerStatId stat,
            int difficultyClass,
            int modifiers = 0)
        {
            d20 = Mathf.Clamp(d20, DieMin, DieMax);
            return Resolve(d20, stats, stat, difficultyClass, modifiers);
        }

        /// <summary>
        /// Roll using a raw stat value (1–20) when you already resolved the attribute.
        /// </summary>
        public static WorkerRollResult CheckStatValue(
            int statValue,
            int difficultyClass,
            int modifiers = 0,
            WorkerStatId statId = WorkerStatId.RawPower)
        {
            int d20 = RollD20();
            return Resolve(d20, statId, WorkerStats.Clamp(statValue), difficultyClass, modifiers);
        }

        /// <summary>Uniform integer in [1, 20]. Seeded when <see cref="BeginSeeded"/> is active.</summary>
        public static int RollD20()
        {
            if (_useSeeded && _seededRng != null)
                return _seededRng.Next(DieMin, DieMax + 1);
            return Random.Range(DieMin, DieMax + 1);
        }

        static WorkerRollResult Resolve(
            int d20,
            WorkerStats stats,
            WorkerStatId stat,
            int dc,
            int modifiers)
        {
            int statValue = stats != null ? stats.Get(stat) : WorkerStats.Baseline;
            return Resolve(d20, stat, statValue, dc, modifiers);
        }

        static WorkerRollResult Resolve(
            int d20,
            WorkerStatId statId,
            int statValue,
            int dc,
            int modifiers)
        {
            int total = d20 + statValue + modifiers;
            bool success = total >= dc;
            return new WorkerRollResult(
                d20,
                statId,
                statValue,
                modifiers,
                total,
                dc,
                success,
                naturalOne: d20 == DieMin,
                naturalTwenty: d20 == DieMax);
        }
    }
}
