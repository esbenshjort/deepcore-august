using System;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Pillars of worker capability. Soul later modulates mental state / reaction —
    /// not direct physical output.
    /// </summary>
    public enum WorkerStatPillar : byte
    {
        Body = 0,
        Mind = 1,
        Soul = 2,
    }

    /// <summary>
    /// All 30 worker attributes. Stable IDs for indexed get/set from other systems.
    /// </summary>
    public enum WorkerStatId : byte
    {
        // BODY — physical capability
        RawPower = 0,
        Stamina = 1,
        Recovery = 2,
        Balance = 3,
        Finesse = 4,
        HeavyLifting = 5,
        Rhythm = 6,
        HeatTolerance = 7,
        Agility = 8,
        Toughness = 9,

        // MIND — technical expertise / intelligent task performance
        Calibration = 10,
        Mathematics = 11,
        Lithology = 12,
        Mineralogy = 13,
        Acoustics = 14,
        Mechanics = 15,
        SpatialGeometry = 16,
        Chemistry = 17,
        Logistics = 18,
        SafetyProtocol = 19,

        // SOUL — reaction when things go well or badly (mental state later)
        Composure = 20,
        Bravery = 21,
        Affinity = 22,
        Focus = 23,
        WorkRate = 24,
        Determination = 25,
        Tolerance = 26,
        Empathy = 27,
        Leadership = 28,
        Intuition = 29,
    }

    /// <summary>
    /// Centralized worker attribute sheet. Data only — no formulas or gameplay effects.
    /// Values are integers clamped to <see cref="Min"/>–<see cref="Max"/>.
    /// </summary>
    [Serializable]
    public sealed class WorkerStats
    {
        public const int Min = 1;
        public const int Max = 20;
        public const int StatCount = 30;
        public const int Baseline = 10;

        [Header("BODY")]
        [Range(Min, Max)] public int rawPower = Baseline;
        [Range(Min, Max)] public int stamina = Baseline;
        [Range(Min, Max)] public int recovery = Baseline;
        [Range(Min, Max)] public int balance = Baseline;
        [Range(Min, Max)] public int finesse = Baseline;
        [Range(Min, Max)] public int heavyLifting = Baseline;
        [Range(Min, Max)] public int rhythm = Baseline;
        [Range(Min, Max)] public int heatTolerance = Baseline;
        [Range(Min, Max)] public int agility = Baseline;
        [Range(Min, Max)] public int toughness = Baseline;

        [Header("MIND")]
        [Range(Min, Max)] public int calibration = Baseline;
        [Range(Min, Max)] public int mathematics = Baseline;
        [Range(Min, Max)] public int lithology = Baseline;
        [Range(Min, Max)] public int mineralogy = Baseline;
        [Range(Min, Max)] public int acoustics = Baseline;
        [Range(Min, Max)] public int mechanics = Baseline;
        [Range(Min, Max)] public int spatialGeometry = Baseline;
        [Range(Min, Max)] public int chemistry = Baseline;
        [Range(Min, Max)] public int logistics = Baseline;
        [Range(Min, Max)] public int safetyProtocol = Baseline;

        [Header("SOUL")]
        [Range(Min, Max)] public int composure = Baseline;
        [Range(Min, Max)] public int bravery = Baseline;
        [Range(Min, Max)] public int affinity = Baseline;
        [Range(Min, Max)] public int focus = Baseline;
        [Range(Min, Max)] public int workRate = Baseline;
        [Range(Min, Max)] public int determination = Baseline;
        [Range(Min, Max)] public int tolerance = Baseline;
        [Range(Min, Max)] public int empathy = Baseline;
        [Range(Min, Max)] public int leadership = Baseline;
        [Range(Min, Max)] public int intuition = Baseline;

        /// <summary>Mid-range sheet (all stats = <see cref="Baseline"/>).</summary>
        public static WorkerStats CreateBaseline() => new WorkerStats();

        /// <summary>Clamp a raw value into the legal 1–20 range.</summary>
        public static int Clamp(int value) => Mathf.Clamp(value, Min, Max);

        public static WorkerStatPillar PillarOf(WorkerStatId id)
        {
            int i = (int)id;
            if (i < 10) return WorkerStatPillar.Body;
            if (i < 20) return WorkerStatPillar.Mind;
            return WorkerStatPillar.Soul;
        }

        public int Get(WorkerStatId id) => id switch
        {
            WorkerStatId.RawPower => rawPower,
            WorkerStatId.Stamina => stamina,
            WorkerStatId.Recovery => recovery,
            WorkerStatId.Balance => balance,
            WorkerStatId.Finesse => finesse,
            WorkerStatId.HeavyLifting => heavyLifting,
            WorkerStatId.Rhythm => rhythm,
            WorkerStatId.HeatTolerance => heatTolerance,
            WorkerStatId.Agility => agility,
            WorkerStatId.Toughness => toughness,
            WorkerStatId.Calibration => calibration,
            WorkerStatId.Mathematics => mathematics,
            WorkerStatId.Lithology => lithology,
            WorkerStatId.Mineralogy => mineralogy,
            WorkerStatId.Acoustics => acoustics,
            WorkerStatId.Mechanics => mechanics,
            WorkerStatId.SpatialGeometry => spatialGeometry,
            WorkerStatId.Chemistry => chemistry,
            WorkerStatId.Logistics => logistics,
            WorkerStatId.SafetyProtocol => safetyProtocol,
            WorkerStatId.Composure => composure,
            WorkerStatId.Bravery => bravery,
            WorkerStatId.Affinity => affinity,
            WorkerStatId.Focus => focus,
            WorkerStatId.WorkRate => workRate,
            WorkerStatId.Determination => determination,
            WorkerStatId.Tolerance => tolerance,
            WorkerStatId.Empathy => empathy,
            WorkerStatId.Leadership => leadership,
            WorkerStatId.Intuition => intuition,
            _ => Baseline,
        };

        /// <summary>Set a stat, clamping into 1–20.</summary>
        public void Set(WorkerStatId id, int value)
        {
            value = Clamp(value);
            switch (id)
            {
                case WorkerStatId.RawPower: rawPower = value; break;
                case WorkerStatId.Stamina: stamina = value; break;
                case WorkerStatId.Recovery: recovery = value; break;
                case WorkerStatId.Balance: balance = value; break;
                case WorkerStatId.Finesse: finesse = value; break;
                case WorkerStatId.HeavyLifting: heavyLifting = value; break;
                case WorkerStatId.Rhythm: rhythm = value; break;
                case WorkerStatId.HeatTolerance: heatTolerance = value; break;
                case WorkerStatId.Agility: agility = value; break;
                case WorkerStatId.Toughness: toughness = value; break;
                case WorkerStatId.Calibration: calibration = value; break;
                case WorkerStatId.Mathematics: mathematics = value; break;
                case WorkerStatId.Lithology: lithology = value; break;
                case WorkerStatId.Mineralogy: mineralogy = value; break;
                case WorkerStatId.Acoustics: acoustics = value; break;
                case WorkerStatId.Mechanics: mechanics = value; break;
                case WorkerStatId.SpatialGeometry: spatialGeometry = value; break;
                case WorkerStatId.Chemistry: chemistry = value; break;
                case WorkerStatId.Logistics: logistics = value; break;
                case WorkerStatId.SafetyProtocol: safetyProtocol = value; break;
                case WorkerStatId.Composure: composure = value; break;
                case WorkerStatId.Bravery: bravery = value; break;
                case WorkerStatId.Affinity: affinity = value; break;
                case WorkerStatId.Focus: focus = value; break;
                case WorkerStatId.WorkRate: workRate = value; break;
                case WorkerStatId.Determination: determination = value; break;
                case WorkerStatId.Tolerance: tolerance = value; break;
                case WorkerStatId.Empathy: empathy = value; break;
                case WorkerStatId.Leadership: leadership = value; break;
                case WorkerStatId.Intuition: intuition = value; break;
            }
        }

        /// <summary>Clamp every field into 1–20 (e.g. after Inspector edits).</summary>
        public void ClampAll()
        {
            for (int i = 0; i < StatCount; i++)
                Set((WorkerStatId)i, Get((WorkerStatId)i));
        }

        public void CopyFrom(WorkerStats other)
        {
            if (other == null) return;
            for (int i = 0; i < StatCount; i++)
            {
                var id = (WorkerStatId)i;
                Set(id, other.Get(id));
            }
        }

        public WorkerStats Clone()
        {
            var copy = new WorkerStats();
            copy.CopyFrom(this);
            return copy;
        }
    }
}
