using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Dev/test stat archetypes shared across crew roles.
    /// Slot meaning is role-specific — see <see cref="Label"/> / <see cref="Build"/>.
    /// </summary>
    public enum WorkerSheetProfile : byte
    {
        Baseline = 0,
        Ace = 1,
        Green = 2,
        /// <summary>Role specialty A (e.g. Brute / Survey / Packmule).</summary>
        ArchA = 3,
        /// <summary>Role specialty B (e.g. Technician / Analyst / Router).</summary>
        ArchB = 4,
        /// <summary>Role specialty C (e.g. Cowboy / Field Tech / Chemist).</summary>
        ArchC = 5,
    }

    /// <summary>
    /// Builds temporary WorkerStats sheets per crew role for A/B playtesting.
    /// Unspecified stats stay at baseline 10.
    /// </summary>
    public static class WorkerStatProfiles
    {
        public const int ProfileCount = 6;

        public static string RoleTitle(byte roleIndex) => roleIndex switch
        {
            0 => "PROSPECTOR",
            1 => "EXCAVATOR",
            2 => "HAULER",
            3 => "REFINER",
            4 => "ENGINEER",
            _ => "WORKER",
        };

        public static string Label(byte roleIndex, WorkerSheetProfile profile)
        {
            if (profile == WorkerSheetProfile.Baseline) return "BASE";
            if (profile == WorkerSheetProfile.Ace) return "ACE";
            if (profile == WorkerSheetProfile.Green) return "GREEN";

            return roleIndex switch
            {
                0 => profile switch // Prospector
                {
                    WorkerSheetProfile.ArchA => "SURVEY",
                    WorkerSheetProfile.ArchB => "ANALYST",
                    WorkerSheetProfile.ArchC => "FIELD",
                    _ => "?",
                },
                1 => profile switch // Excavator
                {
                    WorkerSheetProfile.ArchA => "BRUTE",
                    WorkerSheetProfile.ArchB => "TECH",
                    WorkerSheetProfile.ArchC => "COWBOY",
                    _ => "?",
                },
                2 => profile switch // Hauler
                {
                    WorkerSheetProfile.ArchA => "PACK",
                    WorkerSheetProfile.ArchB => "ROUTER",
                    WorkerSheetProfile.ArchC => "STEADY",
                    _ => "?",
                },
                3 => profile switch // Refiner
                {
                    WorkerSheetProfile.ArchA => "CHEM",
                    WorkerSheetProfile.ArchB => "PACE",
                    WorkerSheetProfile.ArchC => "CARE",
                    _ => "?",
                },
                4 => profile switch // Engineer
                {
                    WorkerSheetProfile.ArchA => "TRACK",
                    WorkerSheetProfile.ArchB => "SYSTEMS",
                    WorkerSheetProfile.ArchC => "GRIT",
                    _ => "?",
                },
                _ => "?",
            };
        }

        public static Color Accent(WorkerSheetProfile profile) => profile switch
        {
            WorkerSheetProfile.Baseline => new Color(0.45f, 0.58f, 0.65f, 1f),
            WorkerSheetProfile.Ace => new Color(1f, 0.92f, 0.35f, 1f),
            WorkerSheetProfile.Green => new Color(0.28f, 0.38f, 0.44f, 1f),
            WorkerSheetProfile.ArchA => new Color(1f, 0.72f, 0.22f, 1f),
            WorkerSheetProfile.ArchB => new Color(0.25f, 0.92f, 1f, 1f),
            WorkerSheetProfile.ArchC => new Color(1f, 0.45f, 0.25f, 1f),
            _ => Color.white,
        };

        /// <summary>Map excavator sheet profiles onto the balance-harness enum.</summary>
        public static ExcavatorTestProfile ToExcavator(WorkerSheetProfile p) => p switch
        {
            WorkerSheetProfile.Baseline => ExcavatorTestProfile.Baseline,
            WorkerSheetProfile.Ace => ExcavatorTestProfile.Ace,
            WorkerSheetProfile.Green => ExcavatorTestProfile.Green,
            WorkerSheetProfile.ArchA => ExcavatorTestProfile.Brute,
            WorkerSheetProfile.ArchB => ExcavatorTestProfile.Technician,
            WorkerSheetProfile.ArchC => ExcavatorTestProfile.Cowboy,
            _ => ExcavatorTestProfile.Baseline,
        };

        public static WorkerSheetProfile FromExcavator(ExcavatorTestProfile p) => p switch
        {
            ExcavatorTestProfile.Baseline => WorkerSheetProfile.Baseline,
            ExcavatorTestProfile.Ace => WorkerSheetProfile.Ace,
            ExcavatorTestProfile.Green => WorkerSheetProfile.Green,
            ExcavatorTestProfile.Brute => WorkerSheetProfile.ArchA,
            ExcavatorTestProfile.Technician => WorkerSheetProfile.ArchB,
            ExcavatorTestProfile.Cowboy => WorkerSheetProfile.ArchC,
            // Professional maps nearest to ArchB+Baseline hybrid — treat as ArchB for sheet UI
            ExcavatorTestProfile.Professional => WorkerSheetProfile.ArchB,
            _ => WorkerSheetProfile.Baseline,
        };

        public static WorkerStats Build(byte roleIndex, WorkerSheetProfile profile)
        {
            if (profile == WorkerSheetProfile.Baseline)
                return WorkerStats.CreateBaseline();

            if (roleIndex == 1) // Excavator — reuse harness sheets
                return ExcavatorBalanceHarness.BuildProfile(ToExcavator(profile));

            var s = WorkerStats.CreateBaseline();
            switch (roleIndex)
            {
                case 0: BuildProspector(s, profile); break;
                case 2: BuildHauler(s, profile); break;
                case 3: BuildRefiner(s, profile); break;
                case 4: BuildEngineer(s, profile); break;
            }
            return s;
        }

        static void BuildProspector(WorkerStats s, WorkerSheetProfile profile)
        {
            switch (profile)
            {
                case WorkerSheetProfile.Ace:
                    SetMany(s, 19,
                        WorkerStatId.Calibration, WorkerStatId.Acoustics, WorkerStatId.SpatialGeometry,
                        WorkerStatId.Focus, WorkerStatId.Mathematics, WorkerStatId.Lithology,
                        WorkerStatId.Mineralogy, WorkerStatId.Chemistry, WorkerStatId.Intuition,
                        WorkerStatId.Mechanics, WorkerStatId.HeavyLifting, WorkerStatId.Composure,
                        WorkerStatId.WorkRate);
                    break;
                case WorkerSheetProfile.Green:
                    SetMany(s, 3,
                        WorkerStatId.Calibration, WorkerStatId.Acoustics, WorkerStatId.SpatialGeometry,
                        WorkerStatId.Focus, WorkerStatId.Mathematics, WorkerStatId.Lithology,
                        WorkerStatId.Mineralogy, WorkerStatId.Chemistry, WorkerStatId.Intuition,
                        WorkerStatId.Mechanics, WorkerStatId.HeavyLifting, WorkerStatId.Composure,
                        WorkerStatId.WorkRate);
                    break;
                case WorkerSheetProfile.ArchA: // Survey — field scan strength
                    s.Set(WorkerStatId.Calibration, 18);
                    s.Set(WorkerStatId.Acoustics, 17);
                    s.Set(WorkerStatId.SpatialGeometry, 17);
                    s.Set(WorkerStatId.Focus, 16);
                    s.Set(WorkerStatId.Mathematics, 8);
                    s.Set(WorkerStatId.Intuition, 12);
                    break;
                case WorkerSheetProfile.ArchB: // Analyst — desk interpretation
                    s.Set(WorkerStatId.Mathematics, 18);
                    s.Set(WorkerStatId.Lithology, 17);
                    s.Set(WorkerStatId.Mineralogy, 17);
                    s.Set(WorkerStatId.Chemistry, 16);
                    s.Set(WorkerStatId.Intuition, 16);
                    s.Set(WorkerStatId.Composure, 15);
                    s.Set(WorkerStatId.Calibration, 9);
                    s.Set(WorkerStatId.Acoustics, 8);
                    break;
                case WorkerSheetProfile.ArchC: // Field tech — deploy / haul gear
                    s.Set(WorkerStatId.Mechanics, 18);
                    s.Set(WorkerStatId.HeavyLifting, 17);
                    s.Set(WorkerStatId.Stamina, 15);
                    s.Set(WorkerStatId.Calibration, 14);
                    s.Set(WorkerStatId.WorkRate, 14);
                    s.Set(WorkerStatId.Mathematics, 7);
                    break;
            }
        }

        static void BuildHauler(WorkerStats s, WorkerSheetProfile profile)
        {
            switch (profile)
            {
                case WorkerSheetProfile.Ace:
                    SetMany(s, 19,
                        WorkerStatId.HeavyLifting, WorkerStatId.Stamina, WorkerStatId.Toughness,
                        WorkerStatId.Logistics, WorkerStatId.SpatialGeometry, WorkerStatId.Agility,
                        WorkerStatId.Rhythm, WorkerStatId.WorkRate, WorkerStatId.Determination,
                        WorkerStatId.Balance, WorkerStatId.Recovery);
                    break;
                case WorkerSheetProfile.Green:
                    SetMany(s, 3,
                        WorkerStatId.HeavyLifting, WorkerStatId.Stamina, WorkerStatId.Toughness,
                        WorkerStatId.Logistics, WorkerStatId.SpatialGeometry, WorkerStatId.Agility,
                        WorkerStatId.Rhythm, WorkerStatId.WorkRate, WorkerStatId.Determination,
                        WorkerStatId.Balance, WorkerStatId.Recovery);
                    break;
                case WorkerSheetProfile.ArchA: // Packmule
                    s.Set(WorkerStatId.HeavyLifting, 18);
                    s.Set(WorkerStatId.Stamina, 17);
                    s.Set(WorkerStatId.Toughness, 16);
                    s.Set(WorkerStatId.Recovery, 15);
                    s.Set(WorkerStatId.Agility, 7);
                    s.Set(WorkerStatId.Logistics, 9);
                    break;
                case WorkerSheetProfile.ArchB: // Router
                    s.Set(WorkerStatId.Logistics, 18);
                    s.Set(WorkerStatId.SpatialGeometry, 17);
                    s.Set(WorkerStatId.Agility, 16);
                    s.Set(WorkerStatId.Balance, 15);
                    s.Set(WorkerStatId.HeavyLifting, 9);
                    break;
                case WorkerSheetProfile.ArchC: // Steady
                    s.Set(WorkerStatId.Rhythm, 17);
                    s.Set(WorkerStatId.WorkRate, 16);
                    s.Set(WorkerStatId.Composure, 16);
                    s.Set(WorkerStatId.Determination, 15);
                    s.Set(WorkerStatId.Stamina, 14);
                    break;
            }
        }

        static void BuildRefiner(WorkerStats s, WorkerSheetProfile profile)
        {
            switch (profile)
            {
                case WorkerSheetProfile.Ace:
                    SetMany(s, 19,
                        WorkerStatId.Chemistry, WorkerStatId.Mineralogy, WorkerStatId.Finesse,
                        WorkerStatId.Focus, WorkerStatId.WorkRate, WorkerStatId.Rhythm,
                        WorkerStatId.Stamina, WorkerStatId.SafetyProtocol, WorkerStatId.Composure,
                        WorkerStatId.Calibration);
                    break;
                case WorkerSheetProfile.Green:
                    SetMany(s, 3,
                        WorkerStatId.Chemistry, WorkerStatId.Mineralogy, WorkerStatId.Finesse,
                        WorkerStatId.Focus, WorkerStatId.WorkRate, WorkerStatId.Rhythm,
                        WorkerStatId.Stamina, WorkerStatId.SafetyProtocol, WorkerStatId.Composure,
                        WorkerStatId.Calibration);
                    break;
                case WorkerSheetProfile.ArchA: // Chemist
                    s.Set(WorkerStatId.Chemistry, 18);
                    s.Set(WorkerStatId.Mineralogy, 17);
                    s.Set(WorkerStatId.Calibration, 15);
                    s.Set(WorkerStatId.Focus, 15);
                    s.Set(WorkerStatId.WorkRate, 9);
                    break;
                case WorkerSheetProfile.ArchB: // Pace washer
                    s.Set(WorkerStatId.WorkRate, 18);
                    s.Set(WorkerStatId.Rhythm, 17);
                    s.Set(WorkerStatId.Stamina, 16);
                    s.Set(WorkerStatId.Determination, 14);
                    s.Set(WorkerStatId.Chemistry, 9);
                    break;
                case WorkerSheetProfile.ArchC: // Careful
                    s.Set(WorkerStatId.Finesse, 18);
                    s.Set(WorkerStatId.Focus, 17);
                    s.Set(WorkerStatId.SafetyProtocol, 16);
                    s.Set(WorkerStatId.Composure, 15);
                    s.Set(WorkerStatId.WorkRate, 8);
                    break;
            }
        }

        static void BuildEngineer(WorkerStats s, WorkerSheetProfile profile)
        {
            switch (profile)
            {
                case WorkerSheetProfile.Ace:
                    SetMany(s, 19,
                        WorkerStatId.Mechanics, WorkerStatId.HeavyLifting, WorkerStatId.Calibration,
                        WorkerStatId.SpatialGeometry, WorkerStatId.Stamina, WorkerStatId.Focus,
                        WorkerStatId.Determination, WorkerStatId.WorkRate, WorkerStatId.Toughness,
                        WorkerStatId.Logistics);
                    break;
                case WorkerSheetProfile.Green:
                    SetMany(s, 3,
                        WorkerStatId.Mechanics, WorkerStatId.HeavyLifting, WorkerStatId.Calibration,
                        WorkerStatId.SpatialGeometry, WorkerStatId.Stamina, WorkerStatId.Focus,
                        WorkerStatId.Determination, WorkerStatId.WorkRate, WorkerStatId.Toughness,
                        WorkerStatId.Logistics);
                    break;
                case WorkerSheetProfile.ArchA: // Track hand
                    s.Set(WorkerStatId.HeavyLifting, 18);
                    s.Set(WorkerStatId.Mechanics, 15);
                    s.Set(WorkerStatId.Stamina, 16);
                    s.Set(WorkerStatId.Toughness, 15);
                    s.Set(WorkerStatId.Calibration, 9);
                    break;
                case WorkerSheetProfile.ArchB: // Systems
                    s.Set(WorkerStatId.Mechanics, 18);
                    s.Set(WorkerStatId.Calibration, 17);
                    s.Set(WorkerStatId.SpatialGeometry, 16);
                    s.Set(WorkerStatId.Logistics, 15);
                    s.Set(WorkerStatId.Focus, 15);
                    s.Set(WorkerStatId.HeavyLifting, 8);
                    break;
                case WorkerSheetProfile.ArchC: // Grit
                    s.Set(WorkerStatId.Determination, 18);
                    s.Set(WorkerStatId.Focus, 16);
                    s.Set(WorkerStatId.Stamina, 16);
                    s.Set(WorkerStatId.Toughness, 15);
                    s.Set(WorkerStatId.WorkRate, 14);
                    break;
            }
        }

        static void SetMany(WorkerStats s, int value, params WorkerStatId[] ids)
        {
            for (int i = 0; i < ids.Length; i++)
                s.Set(ids[i], value);
        }
    }
}
