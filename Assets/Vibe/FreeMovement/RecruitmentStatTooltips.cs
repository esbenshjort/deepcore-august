namespace DeepCore.FreeMovement
{
    public enum StatJobImportance : byte
    {
        High = 0,
        Useful = 1,
        Situational = 2,
        Low = 3,
        Irrelevant = 4,
    }

    /// <summary>
    /// Recruitment V1 tooltips — general meaning + job-specific relevance from live formulas
    /// (Excavation / Prospecting) or JobStatPreview provisional lists (Haul / Refine / Engineer).
    /// </summary>
    public static class RecruitmentStatTooltips
    {
        public static string ImportanceLabel(StatJobImportance i) => i switch
        {
            StatJobImportance.High => "HIGH",
            StatJobImportance.Useful => "USEFUL",
            StatJobImportance.Situational => "SITUATIONAL",
            StatJobImportance.Low => "LOW",
            _ => "IRRELEVANT",
        };

        public static string General(WorkerStatId id) => id switch
        {
            WorkerStatId.RawPower =>
                "Physical force and ability to apply sustained power.",
            WorkerStatId.Stamina =>
                "Endurance pool size. Higher stamina means a larger physical reserve before rest.",
            WorkerStatId.Recovery =>
                "How quickly physical reserve returns when resting or pausing.",
            WorkerStatId.Balance =>
                "Bodily stability — footing, posture, and resisting knocks.",
            WorkerStatId.Finesse =>
                "Fine motor control and precise tool work under pressure.",
            WorkerStatId.HeavyLifting =>
                "Ability to move and position heavy loads and equipment.",
            WorkerStatId.Rhythm =>
                "Working cadence — keeping a steady, efficient physical tempo.",
            WorkerStatId.HeatTolerance =>
                "Resistance to heat stress from tools, rock, and confined dig faces.",
            WorkerStatId.Agility =>
                "Speed and lightness of movement in tight or uneven ground.",
            WorkerStatId.Toughness =>
                "Hardiness against injury when systems fail or overheat.",
            WorkerStatId.Calibration =>
                "Instrument setup, sensor discipline, and precise measurement work.",
            WorkerStatId.Mathematics =>
                "Quantitative reasoning — models, rates, and structured analysis.",
            WorkerStatId.Lithology =>
                "Knowledge of rock types, hardness, and geological structure.",
            WorkerStatId.Mineralogy =>
                "Knowledge of minerals and geological material composition.",
            WorkerStatId.Acoustics =>
                "Reading vibration and sound returns through rock and air.",
            WorkerStatId.Mechanics =>
                "Machines, linkages, and keeping hardware working in the field.",
            WorkerStatId.SpatialGeometry =>
                "3D spatial judgment — voids, weak points, routes, and layout.",
            WorkerStatId.Chemistry =>
                "Chemical processes — reagents, wash circuits, and material reaction.",
            WorkerStatId.Logistics =>
                "Route planning, load sequencing, and moving material efficiently.",
            WorkerStatId.SafetyProtocol =>
                "Discipline around heat limits, procedures, and avoiding catastrophe.",
            WorkerStatId.Composure =>
                "Emotional steadiness when pressure, heat, or conflict rises.",
            WorkerStatId.Bravery =>
                "Willingness to act under risk rather than freeze or bail.",
            WorkerStatId.Affinity =>
                "Warmth and social approachability toward other crew.",
            WorkerStatId.Focus =>
                "Concentration under distraction — resists pressure on fine work.",
            WorkerStatId.WorkRate =>
                "Drive to keep output moving once a task is underway.",
            WorkerStatId.Determination =>
                "Will to push through hard stops instead of backing off.",
            WorkerStatId.Tolerance =>
                "Patience with others' flaws and friction.",
            WorkerStatId.Empathy =>
                "Reading and responding to others' emotional state.",
            WorkerStatId.Leadership =>
                "Ability to set tone and pull a crew's attention.",
            WorkerStatId.Intuition =>
                "Gut read on incomplete evidence — pattern sense without full data.",
            _ => "Worker attribute.",
        };

        public static (string ForJob, StatJobImportance Importance, bool Provisional)
            ForJob(WorkerStatId id, JobType job)
        {
            return job switch
            {
                JobType.Excavation => Excavation(id),
                JobType.Prospecting => Prospecting(id),
                JobType.Hauling => Hauling(id),
                JobType.Refining => Refining(id),
                JobType.Engineering => Engineering(id),
                JobType.Steward => Steward(id),
                _ => ("No active job selected.", StatJobImportance.Irrelevant, false),
            };
        }

        static (string, StatJobImportance, bool) Excavation(WorkerStatId id) => id switch
        {
            WorkerStatId.RawPower => (
                "Adds directly to dig power (tool power + Raw Power). Primary excavation force against armor.",
                StatJobImportance.High, false),
            WorkerStatId.Lithology => (
                "Armor penetration = Lithology × 0.5. Critical against hard material.",
                StatJobImportance.High, false),
            WorkerStatId.SpatialGeometry => (
                "When a weak point is exposed, adds Spatial Geometry × 0.5 penetration.",
                StatJobImportance.Useful, false),
            WorkerStatId.Finesse => (
                "Weak-point discovery: D20 + effective Finesse vs material DC. Core precision dig skill.",
                StatJobImportance.High, false),
            WorkerStatId.Stamina => (
                "Sets max physical stamina pool (50 + Stamina×5). Digging spends stamina per strike.",
                StatJobImportance.High, false),
            WorkerStatId.Recovery => (
                "Rest recover rate while paused: 1 + Recovery×0.20 per second.",
                StatJobImportance.Useful, false),
            WorkerStatId.HeatTolerance => (
                "Reduces finesse penalties in Danger/Extreme heat zones.",
                StatJobImportance.Useful, false),
            WorkerStatId.Rhythm => (
                "Trims dig heat generation when above baseline (still floored).",
                StatJobImportance.Situational, false),
            WorkerStatId.SafetyProtocol => (
                "Lowers dig heat gain and raises preferred max heat before cool-down checks.",
                StatJobImportance.Useful, false),
            WorkerStatId.Focus => (
                "Resists Frustration penalty on Finesse (pressure = Frust−25 minus Focus×2).",
                StatJobImportance.Useful, false),
            WorkerStatId.Determination => (
                "At preferred max heat: D20+Determination vs DC 22 — push or cool.",
                StatJobImportance.Situational, false),
            WorkerStatId.Composure => (
                "At extreme heat (≥95): D20+Composure vs DC 25 — cool or risk.",
                StatJobImportance.Situational, false),
            WorkerStatId.Toughness => (
                "On OVERHEAT: D20+Toughness vs DC 24 — fail adds Injury.",
                StatJobImportance.Situational, false),
            WorkerStatId.Mineralogy => (
                "Limited direct impact. Mostly useful for reading material rather than excavation force.",
                StatJobImportance.Low, false),
            WorkerStatId.HeavyLifting or WorkerStatId.Agility or WorkerStatId.Balance => (
                "Not read by live dig formulas. May matter for future movement / load work.",
                StatJobImportance.Low, false),
            WorkerStatId.Affinity or WorkerStatId.Empathy or WorkerStatId.Leadership
                or WorkerStatId.Tolerance => (
                "No dig DPS. Affects crew chemistry and Social Aura, not excavation output.",
                StatJobImportance.Irrelevant, false),
            _ => (
                "Not used by live excavation damage / heat / weak-point formulas.",
                StatJobImportance.Irrelevant, false),
        };

        static (string, StatJobImportance, bool) Prospecting(WorkerStatId id) => id switch
        {
            WorkerStatId.Calibration => (
                "Primary scan skill (with Focus). Shortens survey duration; also observation quality and spatial uncertainty.",
                StatJobImportance.High, false),
            WorkerStatId.Focus => (
                "Scan duration skill component and analysis speed. Resists distraction during survey work.",
                StatJobImportance.High, false),
            WorkerStatId.Acoustics => (
                "Primary detection sensitivity / threshold for field returns.",
                StatJobImportance.High, false),
            WorkerStatId.SpatialGeometry => (
                "Belief warp, false negatives/positives, and spatial quality in uncertainty model.",
                StatJobImportance.High, false),
            WorkerStatId.Mechanics => (
                "Scanner setup hours: higher Mechanics shortens field setup time.",
                StatJobImportance.Useful, false),
            WorkerStatId.HeavyLifting => (
                "Minor setup speed assist when placing heavy scanner gear.",
                StatJobImportance.Situational, false),
            WorkerStatId.Mathematics or WorkerStatId.Lithology or WorkerStatId.Mineralogy
                or WorkerStatId.Chemistry => (
                "Analysis quality weights for anomaly interpretation (desk / investigation).",
                StatJobImportance.Useful, false),
            WorkerStatId.Composure => (
                "Contributes to analysis quality under incomplete evidence.",
                StatJobImportance.Useful, false),
            WorkerStatId.Intuition => (
                "Analysis quality weight — gut read when data is incomplete.",
                StatJobImportance.Useful, false),
            WorkerStatId.WorkRate => (
                "Analysis speed only (with Focus). Explicitly unused for scan duration.",
                StatJobImportance.Situational, false),
            WorkerStatId.RawPower => (
                "No live prospecting formula. Excavation-side physical force.",
                StatJobImportance.Irrelevant, false),
            WorkerStatId.Affinity or WorkerStatId.Empathy or WorkerStatId.Leadership => (
                "Social / aura — not scan or analysis output.",
                StatJobImportance.Irrelevant, false),
            _ => (
                "Not read by live prospecting scan / setup / analysis formulas.",
                StatJobImportance.Low, false),
        };

        static (string, StatJobImportance, bool) Hauling(WorkerStatId id)
        {
            bool listed = IsRelevant(JobType.Hauling, id);
            if (!listed)
                return (
                    "Not on the intended hauling preview list. Live haul speed currently uses fatigue only — sheet stats unused.",
                    StatJobImportance.Irrelevant, true);
            return id switch
            {
                WorkerStatId.HeavyLifting => (
                    "Intended: load capacity and heavy cart work. Live haul formulas do not read this yet.",
                    StatJobImportance.High, true),
                WorkerStatId.Stamina => (
                    "Intended: sustained carrying endurance. Live path uses fatigue mul only today.",
                    StatJobImportance.Useful, true),
                WorkerStatId.Logistics => (
                    "Intended: route and load sequencing. Not yet in live DeliveryCalculator.",
                    StatJobImportance.Useful, true),
                WorkerStatId.Agility => (
                    "Intended: navigating tight tunnels with a load. Provisional.",
                    StatJobImportance.Situational, true),
                WorkerStatId.SpatialGeometry => (
                    "Intended: reading tunnel layout while hauling. Provisional.",
                    StatJobImportance.Situational, true),
                _ => (
                    "On hauling preview list — provisional until haul formulas land.",
                    StatJobImportance.Useful, true),
            };
        }

        static (string, StatJobImportance, bool) Refining(WorkerStatId id)
        {
            if (!IsRelevant(JobType.Refining, id))
                return (
                    "Not on the intended refining preview list. Wash/refine machines do not read sheet stats yet.",
                    StatJobImportance.Irrelevant, true);
            return id switch
            {
                WorkerStatId.Chemistry => (
                    "Intended: wash-circuit and reagent judgment. Live wash logic does not read this yet.",
                    StatJobImportance.High, true),
                WorkerStatId.Mineralogy => (
                    "Intended: recognizing ore grade through the circuit. Provisional.",
                    StatJobImportance.High, true),
                WorkerStatId.Finesse => (
                    "Intended: careful feed / circuit handling. Provisional.",
                    StatJobImportance.Useful, true),
                WorkerStatId.Focus => (
                    "Intended: sustaining attention on the wash run. Provisional.",
                    StatJobImportance.Useful, true),
                WorkerStatId.Calibration => (
                    "Intended: tuning circuit settings. Provisional.",
                    StatJobImportance.Useful, true),
                _ => (
                    "On refining preview list — provisional until refine formulas land.",
                    StatJobImportance.Useful, true),
            };
        }

        static (string, StatJobImportance, bool) Engineering(WorkerStatId id)
        {
            if (!IsRelevant(JobType.Engineering, id))
                return (
                    "Not on the intended engineering preview list. Repair speed is not sheet-driven yet (coop uses relationships).",
                    StatJobImportance.Irrelevant, true);
            return id switch
            {
                WorkerStatId.Mechanics => (
                    "Intended: core repair / systems work. Live engineer host does not read sheet yet.",
                    StatJobImportance.High, true),
                WorkerStatId.HeavyLifting => (
                    "Intended: placing supports and heavy infra. Provisional.",
                    StatJobImportance.Useful, true),
                WorkerStatId.Calibration => (
                    "Intended: precise infra tuning. Provisional.",
                    StatJobImportance.Useful, true),
                WorkerStatId.SpatialGeometry => (
                    "Intended: layout of tracks / supports. Provisional.",
                    StatJobImportance.Situational, true),
                WorkerStatId.Focus => (
                    "Intended: careful repair under pressure. Provisional.",
                    StatJobImportance.Useful, true),
                _ => (
                    "On engineering preview list — provisional until engineer formulas land.",
                    StatJobImportance.Useful, true),
            };
        }

        static (string, StatJobImportance, bool) Steward(WorkerStatId id)
        {
            if (!IsRelevant(JobType.Steward, id))
                return (
                    "Not on steward job list. Camp meal / hygiene / care ignore this sheet row.",
                    StatJobImportance.Irrelevant, false);
            return id switch
            {
                WorkerStatId.Chemistry => (
                    "Food safety & prep chemistry — major meal quality / contamination resist.",
                    StatJobImportance.High, false),
                WorkerStatId.Finesse => (
                    "Kitchen handling precision — raises meal quality with Chemistry.",
                    StatJobImportance.High, false),
                WorkerStatId.Focus => (
                    "Sustained prep attention; FocusState also penalizes tired stewards.",
                    StatJobImportance.Useful, false),
                WorkerStatId.WorkRate => (
                    "Throughput on kitchen / cleanup loops.",
                    StatJobImportance.Useful, false),
                WorkerStatId.Composure => (
                    "Steadies meal quality under MentalFatigue pressure.",
                    StatJobImportance.Useful, false),
                WorkerStatId.Empathy => (
                    "Wound-care quality — modest recovery aid for minor/moderate injuries.",
                    StatJobImportance.High, false),
                WorkerStatId.Recovery => (
                    "Care skill + personal recovery; aids tending without magic heals.",
                    StatJobImportance.High, false),
                WorkerStatId.Logistics => (
                    "Camp organization — hygiene maintenance while cleaning.",
                    StatJobImportance.Useful, false),
                WorkerStatId.SafetyProtocol => (
                    "Contamination discipline — pushes meals away from Unsafe.",
                    StatJobImportance.Useful, false),
                _ => (
                    "On steward relevant list.",
                    StatJobImportance.Useful, false),
            };
        }

        static bool IsRelevant(JobType job, WorkerStatId id)
        {
            var list = JobStatPreview.RelevantStats(job);
            for (int i = 0; i < list.Count; i++)
                if (list[i] == id) return true;
            return false;
        }
    }
}
