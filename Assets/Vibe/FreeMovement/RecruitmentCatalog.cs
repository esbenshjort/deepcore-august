using System;
using System.Collections.Generic;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Predefined Recruitment V1 candidates — stable for testing, varied archetypes.
    /// Minimum 5 per job. Expensive ≠ objectively better.
    /// </summary>
    public static class RecruitmentCatalog
    {
        public static readonly JobType[] HireJobs =
        {
            JobType.Excavation,
            JobType.Prospecting,
            JobType.Hauling,
            JobType.Refining,
            JobType.Engineering,
            JobType.Steward,
        };

        static List<RecruitmentCandidate> _all;
        static Dictionary<JobType, List<RecruitmentCandidate>> _byJob;

        public static IReadOnlyList<RecruitmentCandidate> All
        {
            get
            {
                Ensure();
                return _all;
            }
        }

        public static IReadOnlyList<RecruitmentCandidate> ForJob(JobType job)
        {
            Ensure();
            return _byJob.TryGetValue(job, out var list)
                ? list
                : (IReadOnlyList<RecruitmentCandidate>)Array.Empty<RecruitmentCandidate>();
        }

        public static RecruitmentCandidate Find(string candidateId)
        {
            Ensure();
            for (int i = 0; i < _all.Count; i++)
                if (_all[i].CandidateId == candidateId)
                    return _all[i];
            return null;
        }

        static void Ensure()
        {
            if (_all != null) return;
            _all = new List<RecruitmentCandidate>(32);
            _byJob = new Dictionary<JobType, List<RecruitmentCandidate>>(8);
            foreach (var job in HireJobs)
                _byJob[job] = new List<RecruitmentCandidate>(6);

            AddExcavators();
            AddProspectors();
            AddHaulers();
            AddRefiners();
            AddEngineers();
            AddStewards();
        }

        static void Add(RecruitmentCandidate c)
        {
            _all.Add(c);
            if (!_byJob.TryGetValue(c.TargetJob, out var list))
            {
                list = new List<RecruitmentCandidate>(6);
                _byJob[c.TargetJob] = list;
            }
            list.Add(c);
        }

        static WorkerStats S(Action<WorkerStats> build)
        {
            var s = WorkerStats.CreateBaseline();
            build?.Invoke(s);
            s.ClampAll();
            return s;
        }

        static void Set(WorkerStats s, WorkerStatId id, int v) => s.Set(id, v);

        // ——— EXCAVATORS ———
        static void AddExcavators()
        {
            Add(new RecruitmentCandidate
            {
                CandidateId = "ex.brute.unstable",
                DisplayName = "Rook Harlan",
                TargetJob = JobType.Excavation,
                Age = 34,
                CareerStanding = "Hard-rock veteran",
                Background = "Tunnel bruiser who clears face like a storm. Drinks hard, cools bad, fights the bit when it fights back.",
                WageAsk = 185,
                SuitabilitySummary = "Peak dig power and lithology — volatile under heat. Will out-cut safer diggers until overheat bites.",
                PersonalityTags = new[] { "confrontational", "competitive", "blunt" },
                Traits = new[] { RecruitmentTraitId.Reckless, RecruitmentTraitId.ShortTemper },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.RawPower, 18);
                    Set(s, WorkerStatId.Lithology, 16);
                    Set(s, WorkerStatId.Stamina, 15);
                    Set(s, WorkerStatId.Toughness, 14);
                    Set(s, WorkerStatId.Determination, 17);
                    Set(s, WorkerStatId.Bravery, 16);
                    Set(s, WorkerStatId.Finesse, 6);
                    Set(s, WorkerStatId.SafetyProtocol, 5);
                    Set(s, WorkerStatId.Composure, 5);
                    Set(s, WorkerStatId.HeatTolerance, 7);
                    Set(s, WorkerStatId.Affinity, 4);
                    Set(s, WorkerStatId.Tolerance, 4);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "ex.reliable.generalist",
                DisplayName = "Sable Quinn",
                TargetJob = JobType.Excavation,
                Age = 41,
                CareerStanding = "Steady contractor",
                Background = "Ten years of face work without heroics. Shows up, digs clean, goes home. Crew likes the quiet.",
                WageAsk = 120,
                SuitabilitySummary = "Balanced excavator — solid power, finesse, and heat sense. No flash, few disasters.",
                PersonalityTags = new[] { "calm", "patient", "supportive" },
                Traits = new[] { RecruitmentTraitId.TeamPlayer, RecruitmentTraitId.Methodical },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.RawPower, 13);
                    Set(s, WorkerStatId.Lithology, 12);
                    Set(s, WorkerStatId.Finesse, 12);
                    Set(s, WorkerStatId.Stamina, 13);
                    Set(s, WorkerStatId.Recovery, 12);
                    Set(s, WorkerStatId.HeatTolerance, 12);
                    Set(s, WorkerStatId.SafetyProtocol, 13);
                    Set(s, WorkerStatId.Focus, 12);
                    Set(s, WorkerStatId.Composure, 13);
                    Set(s, WorkerStatId.Affinity, 13);
                    Set(s, WorkerStatId.SpatialGeometry, 11);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "ex.tech.fragile",
                DisplayName = "Ivy Chen",
                TargetJob = JobType.Excavation,
                Age = 29,
                CareerStanding = "Precision cutter",
                Background = "Ex-lab tech who learned the bit for weak-point work. Brilliant reads; body pays for every mistake.",
                WageAsk = 155,
                SuitabilitySummary = "Excellent finesse, spatial, and lithology — physically fragile and injury-prone.",
                PersonalityTags = new[] { "patient", "independent", "blunt" },
                Traits = new[] { RecruitmentTraitId.Methodical, RecruitmentTraitId.InjuryProne },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Finesse, 18);
                    Set(s, WorkerStatId.SpatialGeometry, 17);
                    Set(s, WorkerStatId.Lithology, 15);
                    Set(s, WorkerStatId.Focus, 16);
                    Set(s, WorkerStatId.Calibration, 14);
                    Set(s, WorkerStatId.RawPower, 8);
                    Set(s, WorkerStatId.Stamina, 7);
                    Set(s, WorkerStatId.Toughness, 5);
                    Set(s, WorkerStatId.HeavyLifting, 6);
                    Set(s, WorkerStatId.Balance, 7);
                    Set(s, WorkerStatId.HeatTolerance, 9);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "ex.green.cheap",
                DisplayName = "Paz Ortega",
                TargetJob = JobType.Excavation,
                Age = 22,
                CareerStanding = "Green hire",
                Background = "First real contract. Strong back, soft hands, eager to prove something. Wage is the pitch.",
                WageAsk = 55,
                SuitabilitySummary = "Cheap muscle. Raw power is fine; technique, heat sense, and rock knowledge are green.",
                PersonalityTags = new[] { "competitive", "supportive", "calm" },
                Traits = new[] { RecruitmentTraitId.FastLearner, RecruitmentTraitId.PoorRecovery },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.RawPower, 14);
                    Set(s, WorkerStatId.Stamina, 12);
                    Set(s, WorkerStatId.HeavyLifting, 13);
                    Set(s, WorkerStatId.Lithology, 5);
                    Set(s, WorkerStatId.Finesse, 5);
                    Set(s, WorkerStatId.SpatialGeometry, 6);
                    Set(s, WorkerStatId.SafetyProtocol, 6);
                    Set(s, WorkerStatId.HeatTolerance, 7);
                    Set(s, WorkerStatId.Focus, 8);
                    Set(s, WorkerStatId.Recovery, 6);
                    Set(s, WorkerStatId.Affinity, 12);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "ex.veteran.pricey",
                DisplayName = "Mara Voss",
                TargetJob = JobType.Excavation,
                Age = 48,
                CareerStanding = "Site lead excavator",
                Background = "Ran faces for three outfits. Expensive because she finishes tunnels and keeps juniors alive. Not the strongest swing.",
                WageAsk = 210,
                SuitabilitySummary = "Premium safety + lithology + composure. Power is good, not legendary — you pay for judgment.",
                PersonalityTags = new[] { "calm", "blunt", "supportive" },
                Traits = new[] { RecruitmentTraitId.VeteranMiner, RecruitmentTraitId.NaturalLeader },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.RawPower, 12);
                    Set(s, WorkerStatId.Lithology, 17);
                    Set(s, WorkerStatId.SafetyProtocol, 18);
                    Set(s, WorkerStatId.Finesse, 14);
                    Set(s, WorkerStatId.HeatTolerance, 15);
                    Set(s, WorkerStatId.Composure, 16);
                    Set(s, WorkerStatId.Focus, 14);
                    Set(s, WorkerStatId.Toughness, 13);
                    Set(s, WorkerStatId.Leadership, 15);
                    Set(s, WorkerStatId.Stamina, 11);
                    Set(s, WorkerStatId.Determination, 13);
                }),
            });
        }

        // ——— PROSPECTORS ———
        static void AddProspectors()
        {
            Add(new RecruitmentCandidate
            {
                CandidateId = "pr.ace.unstable",
                DisplayName = "Cass Reed",
                TargetJob = JobType.Prospecting,
                Age = 36,
                CareerStanding = "Survey virtuoso",
                Background = "Reads rock like sheet music. Also disappears mid-shift when the feed gets boring. Genius with a short fuse for people.",
                WageAsk = 195,
                SuitabilitySummary = "Elite calibration/acoustics/spatial — socially difficult and short-tempered.",
                PersonalityTags = new[] { "independent", "blunt", "confrontational" },
                Traits = new[] { RecruitmentTraitId.LoneWolf, RecruitmentTraitId.ShortTemper },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Calibration, 19);
                    Set(s, WorkerStatId.Acoustics, 18);
                    Set(s, WorkerStatId.SpatialGeometry, 18);
                    Set(s, WorkerStatId.Focus, 17);
                    Set(s, WorkerStatId.Mathematics, 16);
                    Set(s, WorkerStatId.Intuition, 15);
                    Set(s, WorkerStatId.Lithology, 14);
                    Set(s, WorkerStatId.Affinity, 3);
                    Set(s, WorkerStatId.Empathy, 4);
                    Set(s, WorkerStatId.Tolerance, 4);
                    Set(s, WorkerStatId.Composure, 6);
                    Set(s, WorkerStatId.HeavyLifting, 5);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "pr.field.generalist",
                DisplayName = "Jonah Pike",
                TargetJob = JobType.Prospecting,
                Age = 33,
                CareerStanding = "Field surveyor",
                Background = "Carries the gear, plants the cone, walks the grid. Not a desk genius — reliable returns and a steady temper.",
                WageAsk = 110,
                SuitabilitySummary = "Solid field setup + acoustics. Analysis is average; temperament is crew-friendly.",
                PersonalityTags = new[] { "patient", "calm", "supportive" },
                Traits = new[] { RecruitmentTraitId.TeamPlayer, RecruitmentTraitId.HeavySleeper },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Calibration, 12);
                    Set(s, WorkerStatId.Acoustics, 13);
                    Set(s, WorkerStatId.Mechanics, 14);
                    Set(s, WorkerStatId.HeavyLifting, 13);
                    Set(s, WorkerStatId.Focus, 12);
                    Set(s, WorkerStatId.SpatialGeometry, 11);
                    Set(s, WorkerStatId.Composure, 13);
                    Set(s, WorkerStatId.Affinity, 13);
                    Set(s, WorkerStatId.Mathematics, 9);
                    Set(s, WorkerStatId.Chemistry, 8);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "pr.analyst.fragile",
                DisplayName = "Elena Ruiz",
                TargetJob = JobType.Prospecting,
                Age = 31,
                CareerStanding = "Desk analyst",
                Background = "Lives for anomaly conclusions. Soft body, sharp mind — hates hauling scanners up scree.",
                WageAsk = 160,
                SuitabilitySummary = "Top analysis stack (math/geo/chem/intuition). Weak setup / lifting / stamina.",
                PersonalityTags = new[] { "patient", "independent", "blunt" },
                Traits = new[] { RecruitmentTraitId.Methodical, RecruitmentTraitId.InjuryProne },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Mathematics, 18);
                    Set(s, WorkerStatId.Mineralogy, 17);
                    Set(s, WorkerStatId.Lithology, 16);
                    Set(s, WorkerStatId.Chemistry, 17);
                    Set(s, WorkerStatId.Intuition, 16);
                    Set(s, WorkerStatId.Focus, 15);
                    Set(s, WorkerStatId.Composure, 14);
                    Set(s, WorkerStatId.Calibration, 13);
                    Set(s, WorkerStatId.Acoustics, 11);
                    Set(s, WorkerStatId.HeavyLifting, 4);
                    Set(s, WorkerStatId.Stamina, 6);
                    Set(s, WorkerStatId.Mechanics, 7);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "pr.green.cheap",
                DisplayName = "Tess Nolan",
                TargetJob = JobType.Prospecting,
                Age = 24,
                CareerStanding = "Junior tech",
                Background = "Fresh from a short training course. Asks good questions, misreads noise as signal sometimes.",
                WageAsk = 48,
                SuitabilitySummary = "Inexpensive learner. Core survey stats below mid — upside if you can mentor.",
                PersonalityTags = new[] { "supportive", "competitive", "calm" },
                Traits = new[] { RecruitmentTraitId.FastLearner },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Calibration, 7);
                    Set(s, WorkerStatId.Acoustics, 7);
                    Set(s, WorkerStatId.Focus, 9);
                    Set(s, WorkerStatId.SpatialGeometry, 7);
                    Set(s, WorkerStatId.Mathematics, 8);
                    Set(s, WorkerStatId.WorkRate, 12);
                    Set(s, WorkerStatId.Affinity, 13);
                    Set(s, WorkerStatId.Mechanics, 8);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "pr.veteran.pricey",
                DisplayName = "Lewis Crowe",
                TargetJob = JobType.Prospecting,
                Age = 52,
                CareerStanding = "Senior geophysicist",
                Background = "Has written papers and buried claims. Costs a fortune. Methodical to a fault — slow setups, rare false positives.",
                WageAsk = 230,
                SuitabilitySummary = "Premium calibration and domain knowledge. Work rate is deliberately slow; wage is steep.",
                PersonalityTags = new[] { "patient", "blunt", "independent" },
                Traits = new[] { RecruitmentTraitId.VeteranMiner, RecruitmentTraitId.Methodical },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Calibration, 17);
                    Set(s, WorkerStatId.Acoustics, 16);
                    Set(s, WorkerStatId.SpatialGeometry, 16);
                    Set(s, WorkerStatId.Lithology, 17);
                    Set(s, WorkerStatId.Mineralogy, 16);
                    Set(s, WorkerStatId.Mathematics, 15);
                    Set(s, WorkerStatId.Focus, 15);
                    Set(s, WorkerStatId.Composure, 16);
                    Set(s, WorkerStatId.Intuition, 14);
                    Set(s, WorkerStatId.WorkRate, 7);
                    Set(s, WorkerStatId.HeavyLifting, 8);
                }),
            });
        }

        // ——— HAULERS ———
        static void AddHaulers()
        {
            Add(new RecruitmentCandidate
            {
                CandidateId = "ha.packmule",
                DisplayName = "Kowalski Brin",
                TargetJob = JobType.Hauling,
                Age = 38,
                CareerStanding = "Cart specialist",
                Background = "Built like a winch. Routes are secondary — if it fits on a cart, it moves.",
                WageAsk = 125,
                SuitabilitySummary = "Peak lifting + stamina. Logistics middling; socially blunt but reliable.",
                PersonalityTags = new[] { "blunt", "independent", "calm" },
                Traits = new[] { RecruitmentTraitId.LoneWolf },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.HeavyLifting, 19);
                    Set(s, WorkerStatId.Stamina, 17);
                    Set(s, WorkerStatId.Toughness, 15);
                    Set(s, WorkerStatId.Agility, 9);
                    Set(s, WorkerStatId.Logistics, 10);
                    Set(s, WorkerStatId.SpatialGeometry, 9);
                    Set(s, WorkerStatId.Affinity, 7);
                    Set(s, WorkerStatId.Recovery, 11);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "ha.router",
                DisplayName = "Nia Holt",
                TargetJob = JobType.Hauling,
                Age = 35,
                CareerStanding = "Yard router",
                Background = "Thinks in loops and choke points. Prefers smart loads over raw muscle.",
                WageAsk = 140,
                SuitabilitySummary = "Strong logistics + spatial + agility. Lifting is merely good — brains over brawn.",
                PersonalityTags = new[] { "patient", "supportive", "competitive" },
                Traits = new[] { RecruitmentTraitId.Methodical, RecruitmentTraitId.TeamPlayer },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Logistics, 18);
                    Set(s, WorkerStatId.SpatialGeometry, 16);
                    Set(s, WorkerStatId.Agility, 15);
                    Set(s, WorkerStatId.Focus, 14);
                    Set(s, WorkerStatId.HeavyLifting, 11);
                    Set(s, WorkerStatId.Stamina, 12);
                    Set(s, WorkerStatId.Affinity, 14);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "ha.green.cheap",
                DisplayName = "Milo Vargas",
                TargetJob = JobType.Hauling,
                Age = 21,
                CareerStanding = "Day laborer",
                Background = "Needs the work. Fast feet, soft spine for long hauls. Learns if you don't yell.",
                WageAsk = 42,
                SuitabilitySummary = "Cheapest seat. Agility okay; endurance and logistics green.",
                PersonalityTags = new[] { "supportive", "calm", "competitive" },
                Traits = new[] { RecruitmentTraitId.FastLearner, RecruitmentTraitId.PoorRecovery },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Agility, 12);
                    Set(s, WorkerStatId.HeavyLifting, 8);
                    Set(s, WorkerStatId.Stamina, 7);
                    Set(s, WorkerStatId.Logistics, 5);
                    Set(s, WorkerStatId.SpatialGeometry, 6);
                    Set(s, WorkerStatId.Recovery, 5);
                    Set(s, WorkerStatId.Affinity, 12);
                    Set(s, WorkerStatId.WorkRate, 13);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "ha.social.strong",
                DisplayName = "Dara Kim",
                TargetJob = JobType.Hauling,
                Age = 30,
                CareerStanding = "Camp runner",
                Background = "Keeps morale up on the path. Not the strongest hauler — the one people want on shift.",
                WageAsk = 105,
                SuitabilitySummary = "Socially strong hauler. Physical stats mid; chemistry and leadership elevated.",
                PersonalityTags = new[] { "supportive", "calm", "patient" },
                Traits = new[] { RecruitmentTraitId.TeamPlayer, RecruitmentTraitId.NaturalLeader },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.HeavyLifting, 11);
                    Set(s, WorkerStatId.Stamina, 11);
                    Set(s, WorkerStatId.Logistics, 12);
                    Set(s, WorkerStatId.Agility, 12);
                    Set(s, WorkerStatId.Affinity, 17);
                    Set(s, WorkerStatId.Empathy, 16);
                    Set(s, WorkerStatId.Leadership, 14);
                    Set(s, WorkerStatId.Tolerance, 15);
                    Set(s, WorkerStatId.Composure, 13);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "ha.pricey.average",
                DisplayName = "Hector Vale",
                TargetJob = JobType.Hauling,
                Age = 44,
                CareerStanding = "Union hauler",
                Background = "Demands senior rates for mid performance. Safe, slow, hard to fire culturally.",
                WageAsk = 175,
                SuitabilitySummary = "Expensive for average stats. Safety-minded; not a pace-setter. Tradeoff is stability vs value.",
                PersonalityTags = new[] { "blunt", "patient", "independent" },
                Traits = new[] { RecruitmentTraitId.HeavySleeper },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.HeavyLifting, 12);
                    Set(s, WorkerStatId.Stamina, 12);
                    Set(s, WorkerStatId.Logistics, 11);
                    Set(s, WorkerStatId.Agility, 10);
                    Set(s, WorkerStatId.SafetyProtocol, 14);
                    Set(s, WorkerStatId.Composure, 12);
                    Set(s, WorkerStatId.WorkRate, 8);
                }),
            });
        }

        // ——— REFINERS ———
        static void AddRefiners()
        {
            Add(new RecruitmentCandidate
            {
                CandidateId = "rf.chem.ace",
                DisplayName = "Vera Sol",
                TargetJob = JobType.Refining,
                Age = 37,
                CareerStanding = "Circuit chemist",
                Background = "Obsessed with wash purity. Hates interruption. Will correct your feed rates mid-sentence.",
                WageAsk = 170,
                SuitabilitySummary = "Elite chemistry/mineralogy/calibration. Socially cold — lone wolf at the wash.",
                PersonalityTags = new[] { "blunt", "independent", "confrontational" },
                Traits = new[] { RecruitmentTraitId.Methodical, RecruitmentTraitId.LoneWolf },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Chemistry, 19);
                    Set(s, WorkerStatId.Mineralogy, 18);
                    Set(s, WorkerStatId.Calibration, 16);
                    Set(s, WorkerStatId.Finesse, 15);
                    Set(s, WorkerStatId.Focus, 16);
                    Set(s, WorkerStatId.Affinity, 4);
                    Set(s, WorkerStatId.Empathy, 5);
                    Set(s, WorkerStatId.Tolerance, 5);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "rf.steady",
                DisplayName = "Omar Haddad",
                TargetJob = JobType.Refining,
                Age = 40,
                CareerStanding = "Wash operator",
                Background = "Runs a clean circuit shift after shift. Not flashy. Crew trusts the numbers.",
                WageAsk = 115,
                SuitabilitySummary = "Reliable generalist refiner — good chem/focus without extremes.",
                PersonalityTags = new[] { "calm", "patient", "supportive" },
                Traits = new[] { RecruitmentTraitId.TeamPlayer },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Chemistry, 13);
                    Set(s, WorkerStatId.Mineralogy, 13);
                    Set(s, WorkerStatId.Finesse, 12);
                    Set(s, WorkerStatId.Focus, 13);
                    Set(s, WorkerStatId.Calibration, 12);
                    Set(s, WorkerStatId.Composure, 13);
                    Set(s, WorkerStatId.Affinity, 12);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "rf.green.cheap",
                DisplayName = "Sky Patel",
                TargetJob = JobType.Refining,
                Age = 23,
                CareerStanding = "Apprentice",
                Background = "Watched one wash season. Eager, undertrained, underpriced.",
                WageAsk = 45,
                SuitabilitySummary = "Cheap seat. Chemistry and finesse below mid — Fast Learner upside.",
                PersonalityTags = new[] { "supportive", "competitive", "calm" },
                Traits = new[] { RecruitmentTraitId.FastLearner },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Chemistry, 6);
                    Set(s, WorkerStatId.Mineralogy, 7);
                    Set(s, WorkerStatId.Finesse, 7);
                    Set(s, WorkerStatId.Focus, 9);
                    Set(s, WorkerStatId.Calibration, 6);
                    Set(s, WorkerStatId.WorkRate, 12);
                    Set(s, WorkerStatId.Affinity, 13);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "rf.reckless.pace",
                DisplayName = "Jex Morrow",
                TargetJob = JobType.Refining,
                Age = 28,
                CareerStanding = "Throughput pusher",
                Background = "Maxes feed rate. Hits quotas and occasionally floods the circuit. Thrill over protocol.",
                WageAsk = 130,
                SuitabilitySummary = "High work rate and determination — safety and finesse compromised.",
                PersonalityTags = new[] { "competitive", "confrontational", "blunt" },
                Traits = new[] { RecruitmentTraitId.Reckless, RecruitmentTraitId.ShortTemper },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.WorkRate, 18);
                    Set(s, WorkerStatId.Determination, 16);
                    Set(s, WorkerStatId.Chemistry, 12);
                    Set(s, WorkerStatId.Focus, 10);
                    Set(s, WorkerStatId.Finesse, 8);
                    Set(s, WorkerStatId.SafetyProtocol, 5);
                    Set(s, WorkerStatId.Calibration, 9);
                    Set(s, WorkerStatId.Composure, 6);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "rf.pricey.care",
                DisplayName = "Helena Croft",
                TargetJob = JobType.Refining,
                Age = 49,
                CareerStanding = "Senior metallurgist",
                Background = "Demands lab-grade discipline on a camp wash. Expensive. Saves ore others lose — if you can afford the pace.",
                WageAsk = 220,
                SuitabilitySummary = "Premium chem/mineralogy/safety. Slow work rate; wage is the tax on perfectionism.",
                PersonalityTags = new[] { "patient", "blunt", "independent" },
                Traits = new[] { RecruitmentTraitId.Methodical, RecruitmentTraitId.CalmUnderPressure },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Chemistry, 17);
                    Set(s, WorkerStatId.Mineralogy, 17);
                    Set(s, WorkerStatId.Finesse, 15);
                    Set(s, WorkerStatId.Calibration, 15);
                    Set(s, WorkerStatId.Focus, 15);
                    Set(s, WorkerStatId.SafetyProtocol, 16);
                    Set(s, WorkerStatId.Composure, 15);
                    Set(s, WorkerStatId.WorkRate, 7);
                }),
            });
        }

        // ——— ENGINEERS ———
        static void AddEngineers()
        {
            Add(new RecruitmentCandidate
            {
                CandidateId = "en.systems.ace",
                DisplayName = "Viktor Hale",
                TargetJob = JobType.Engineering,
                Age = 42,
                CareerStanding = "Systems engineer",
                Background = "Can rebuild a winch blindfolded. Prefers machines to people. Respect is earned slowly.",
                WageAsk = 180,
                SuitabilitySummary = "Elite mechanics/calibration. Socially cool — strong on systems, weak on warmth.",
                PersonalityTags = new[] { "blunt", "independent", "calm" },
                Traits = new[] { RecruitmentTraitId.Methodical, RecruitmentTraitId.LoneWolf },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Mechanics, 19);
                    Set(s, WorkerStatId.Calibration, 17);
                    Set(s, WorkerStatId.SpatialGeometry, 15);
                    Set(s, WorkerStatId.Focus, 15);
                    Set(s, WorkerStatId.HeavyLifting, 12);
                    Set(s, WorkerStatId.Affinity, 5);
                    Set(s, WorkerStatId.Empathy, 5);
                    Set(s, WorkerStatId.Leadership, 8);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "en.grit.strong",
                DisplayName = "Bram Okonkwo",
                TargetJob = JobType.Engineering,
                Age = 36,
                CareerStanding = "Field fitter",
                Background = "Hauls supports, drives pins, sweats the install. Theory is secondary to getting steel in the ground.",
                WageAsk = 125,
                SuitabilitySummary = "Physically strong engineer — lifting and grit over fine calibration.",
                PersonalityTags = new[] { "supportive", "competitive", "blunt" },
                Traits = new[] { RecruitmentTraitId.TeamPlayer, RecruitmentTraitId.Reckless },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.HeavyLifting, 18);
                    Set(s, WorkerStatId.Stamina, 15);
                    Set(s, WorkerStatId.Toughness, 14);
                    Set(s, WorkerStatId.Mechanics, 12);
                    Set(s, WorkerStatId.Calibration, 9);
                    Set(s, WorkerStatId.Focus, 10);
                    Set(s, WorkerStatId.Determination, 15);
                    Set(s, WorkerStatId.Bravery, 14);
                    Set(s, WorkerStatId.SafetyProtocol, 7);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "en.green.cheap",
                DisplayName = "Ryn Adler",
                TargetJob = JobType.Engineering,
                Age = 25,
                CareerStanding = "Junior fitter",
                Background = "Apprenticeship unfinished. Cheap, careful, still learning which way the torque goes.",
                WageAsk = 50,
                SuitabilitySummary = "Low wage, low mechanics. Fast Learner — needs supervision.",
                PersonalityTags = new[] { "calm", "supportive", "patient" },
                Traits = new[] { RecruitmentTraitId.FastLearner, RecruitmentTraitId.InjuryProne },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Mechanics, 6);
                    Set(s, WorkerStatId.Calibration, 7);
                    Set(s, WorkerStatId.HeavyLifting, 9);
                    Set(s, WorkerStatId.Focus, 9);
                    Set(s, WorkerStatId.SpatialGeometry, 8);
                    Set(s, WorkerStatId.Toughness, 6);
                    Set(s, WorkerStatId.Affinity, 13);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "en.leader.social",
                DisplayName = "Anya Petrov",
                TargetJob = JobType.Engineering,
                Age = 39,
                CareerStanding = "Crew lead engineer",
                Background = "Coordinates repairs and people. Mechanic mid-tier — leadership and calm are the product.",
                WageAsk = 155,
                SuitabilitySummary = "Socially strong engineer. Mechanics good, not ace — elevates excavator coop chemistry.",
                PersonalityTags = new[] { "calm", "supportive", "patient" },
                Traits = new[] { RecruitmentTraitId.NaturalLeader, RecruitmentTraitId.CalmUnderPressure },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Mechanics, 13);
                    Set(s, WorkerStatId.Calibration, 12);
                    Set(s, WorkerStatId.Focus, 13);
                    Set(s, WorkerStatId.HeavyLifting, 11);
                    Set(s, WorkerStatId.Leadership, 17);
                    Set(s, WorkerStatId.Composure, 15);
                    Set(s, WorkerStatId.Affinity, 15);
                    Set(s, WorkerStatId.Empathy, 14);
                    Set(s, WorkerStatId.Bravery, 13);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "en.pricey.temper",
                DisplayName = "Gideon Frost",
                TargetJob = JobType.Engineering,
                Age = 46,
                CareerStanding = "Consultant engineer",
                Background = "Brilliant under contract rates. Explodes when plans change. Expensive and difficult — not always better on-site.",
                WageAsk = 215,
                SuitabilitySummary = "High mechanics/focus — short temper and poor recovery tax the wage. Not objectively best.",
                PersonalityTags = new[] { "confrontational", "blunt", "independent" },
                Traits = new[] { RecruitmentTraitId.ShortTemper, RecruitmentTraitId.PoorRecovery },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Mechanics, 17);
                    Set(s, WorkerStatId.Calibration, 16);
                    Set(s, WorkerStatId.SpatialGeometry, 15);
                    Set(s, WorkerStatId.Focus, 16);
                    Set(s, WorkerStatId.HeavyLifting, 10);
                    Set(s, WorkerStatId.Composure, 5);
                    Set(s, WorkerStatId.Tolerance, 4);
                    Set(s, WorkerStatId.Recovery, 6);
                    Set(s, WorkerStatId.Affinity, 6);
                }),
            });
        }

        // ——— STEWARDS ———
        static void AddStewards()
        {
            Add(new RecruitmentCandidate
            {
                CandidateId = "st.care.steady",
                DisplayName = "Nora Vale",
                TargetJob = JobType.Steward,
                Age = 38,
                CareerStanding = "Camp steward",
                Background = "Keeps kitchens clean and wounds wrapped. Quiet competence — meals arrive on time.",
                WageAsk = 110,
                SuitabilitySummary = "Balanced steward — chemistry, empathy, and logistics without drama.",
                PersonalityTags = new[] { "calm", "supportive", "patient" },
                Traits = new[] { RecruitmentTraitId.TeamPlayer, RecruitmentTraitId.Methodical },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Chemistry, 14);
                    Set(s, WorkerStatId.Finesse, 13);
                    Set(s, WorkerStatId.Empathy, 15);
                    Set(s, WorkerStatId.Recovery, 13);
                    Set(s, WorkerStatId.Logistics, 13);
                    Set(s, WorkerStatId.SafetyProtocol, 14);
                    Set(s, WorkerStatId.Focus, 12);
                    Set(s, WorkerStatId.Composure, 14);
                    Set(s, WorkerStatId.WorkRate, 12);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "st.chef.volatile",
                DisplayName = "Pepper Dunn",
                TargetJob = JobType.Steward,
                Age = 29,
                CareerStanding = "Field cook",
                Background = "Brilliant when focused — disastrous when tired. Flavor first, hygiene second.",
                WageAsk = 140,
                SuitabilitySummary = "High chemistry/finesse — composure and safety lag. Great meals or gut bombs.",
                PersonalityTags = new[] { "blunt", "competitive", "independent" },
                Traits = new[] { RecruitmentTraitId.Reckless, RecruitmentTraitId.ShortTemper },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Chemistry, 17);
                    Set(s, WorkerStatId.Finesse, 16);
                    Set(s, WorkerStatId.WorkRate, 15);
                    Set(s, WorkerStatId.Focus, 11);
                    Set(s, WorkerStatId.Composure, 6);
                    Set(s, WorkerStatId.SafetyProtocol, 6);
                    Set(s, WorkerStatId.Logistics, 8);
                    Set(s, WorkerStatId.Empathy, 8);
                    Set(s, WorkerStatId.Tolerance, 5);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "st.medic.soft",
                DisplayName = "Ellis Marrow",
                TargetJob = JobType.Steward,
                Age = 44,
                CareerStanding = "Camp medic aide",
                Background = "Knows wraps and rest better than recipes. Food is adequate; care is why you hire them.",
                WageAsk = 125,
                SuitabilitySummary = "Recovery/empathy lead — chemistry mid. Stabilizes wounds; meals stay Normal.",
                PersonalityTags = new[] { "calm", "supportive", "patient" },
                Traits = new[] { RecruitmentTraitId.CalmUnderPressure, RecruitmentTraitId.TeamPlayer },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Recovery, 17);
                    Set(s, WorkerStatId.Empathy, 16);
                    Set(s, WorkerStatId.Composure, 15);
                    Set(s, WorkerStatId.Focus, 13);
                    Set(s, WorkerStatId.Chemistry, 10);
                    Set(s, WorkerStatId.Finesse, 10);
                    Set(s, WorkerStatId.SafetyProtocol, 13);
                    Set(s, WorkerStatId.Logistics, 11);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "st.hygiene.strict",
                DisplayName = "Marta Okeke",
                TargetJob = JobType.Steward,
                Age = 51,
                CareerStanding = "Sanitation lead",
                Background = "Scrubs first, seasoning later. Camp stays clean; meals are safe and plain.",
                WageAsk = 115,
                SuitabilitySummary = "Logistics/safety dominate — food competence solid, not flashy.",
                PersonalityTags = new[] { "blunt", "methodical", "independent" },
                Traits = new[] { RecruitmentTraitId.Methodical, RecruitmentTraitId.PoorRecovery },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Logistics, 17);
                    Set(s, WorkerStatId.SafetyProtocol, 17);
                    Set(s, WorkerStatId.Chemistry, 12);
                    Set(s, WorkerStatId.Finesse, 11);
                    Set(s, WorkerStatId.WorkRate, 13);
                    Set(s, WorkerStatId.Composure, 12);
                    Set(s, WorkerStatId.Empathy, 9);
                    Set(s, WorkerStatId.Recovery, 7);
                }),
            });

            Add(new RecruitmentCandidate
            {
                CandidateId = "st.green.cheap",
                DisplayName = "Jon Park",
                TargetJob = JobType.Steward,
                Age = 23,
                CareerStanding = "Camp helper",
                Background = "Willing hands, thin résumé. Cheap — needs supervision on food safety.",
                WageAsk = 45,
                SuitabilitySummary = "Low wage, low chemistry. Fast Learner — risk until trained.",
                PersonalityTags = new[] { "calm", "supportive", "patient" },
                Traits = new[] { RecruitmentTraitId.FastLearner, RecruitmentTraitId.InjuryProne },
                BaseStats = S(s =>
                {
                    Set(s, WorkerStatId.Chemistry, 6);
                    Set(s, WorkerStatId.Finesse, 7);
                    Set(s, WorkerStatId.Empathy, 12);
                    Set(s, WorkerStatId.WorkRate, 10);
                    Set(s, WorkerStatId.Logistics, 8);
                    Set(s, WorkerStatId.SafetyProtocol, 7);
                    Set(s, WorkerStatId.Focus, 9);
                    Set(s, WorkerStatId.Composure, 10);
                    Set(s, WorkerStatId.Affinity, 13);
                }),
            });
        }
    }
}
