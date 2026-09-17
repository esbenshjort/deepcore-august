using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Large dig map to test 4-socket cells (rock / bedrock×3 hard / gold).
    /// Gold shine scales with gold socket count (1 faint → 4 blazing).
    /// </summary>
    public sealed class FreeMovementSocketMapRunner : MonoBehaviour
    {
        public const int CellsAcrossWorker = 12;

        [SerializeField] float cellSize = 0.1f;
        [SerializeField] float cameraSize = 10f;
        [SerializeField] float cameraFollow = 5f;

        FineTerrainWorld _world;
        FreeWorkerController _worker;
        HaulerPerson _hauler;
        ProspectorPerson _prospector;
        RefinerPerson _refiner;
        EngineerPerson _engineer;
        StewardPerson _steward;
        CampLifeState _campLife = new CampLifeState();
        CampToiletSite _toiletCamp;
        ProspectorWorkstationSite _prospectWorkstation;
        System.Random _campRng = new System.Random(42);
        readonly ShiftPlanner _shiftPlanner = new ShiftPlanner();
        readonly ManagerRelationshipStore _managerRels = new ManagerRelationshipStore();
        readonly CrewDayTracker _dayTracker = new CrewDayTracker();
        readonly ManagerCommAntiSpam _managerCommSpam = new ManagerCommAntiSpam();
        readonly ManagerIntentStore _managerIntents = new ManagerIntentStore();
        ManagerCommResult _lastTalkResult;
        float _lastTalkResultUntilUnscaled;
        float _lastClaustroRefuseBannerUnscaled;
        ManagerInterveneOutcome _lastIntervene;
        string _mgrRelHoverTip;
        Vector2 _mgrRelHoverGui;
        float _sleepStartedAbsolute = -1f;
        float _idealSleepHoursThisNight = 10f;
        float _sleepRecoveryScale = 1f;
        float _campEveningEndAbsolute = -1f;
        bool _showDailySummary;
        /// <summary>Stage A prototype roster — identity + shared WorkerStats. Indexed by WorkerId order.</summary>
        [System.NonSerialized] WorkerRuntime[] _crewWorkers;
        [System.NonSerialized] WorkerRuntime _workerLewis;
        [System.NonSerialized] WorkerRuntime _workerMara;
        [System.NonSerialized] WorkerRuntime _workerKowalski;
        [System.NonSerialized] WorkerRuntime _workerElena;
        [System.NonSerialized] WorkerRuntime _workerViktor;
        [System.NonSerialized] WorkerRuntime _workerSteward;
        readonly WorkerAssignmentManager _assignments = new();
        readonly WorkerPresenceRegistry _presence = new();
        readonly WorkerStateEventService _stateEvents = new();
        readonly SocialAuraLiveSystem _socialAura = new();
        readonly SocialAuraPresenter _socialPresenter = new();
        readonly SocialAuraPlaytestTracker _socialPlaytest = new();
        readonly RelationshipWorkPlaytestTracker _coopWorkPlaytest = new();
        readonly ProspectorDrySpellTracker _prospectorDrySpell = new();
        float _lastSocialGameHoursDelta;
        bool _socialDevDrawWorld;
        Vector2 _socialDevScroll;
        int _socialDevPairId;
        readonly List<SocialAuraLiveSystem.NearbyDebug> _socialNearbyScratch = new(8);
        readonly List<SocialMemoryEntry> _socialMemoryScratch = new(16);
        readonly List<SocialMemoryEntry> _coopMemScratch = new(8);
        readonly List<RelationshipTrajectoryEntry> _trajectoryScratch = new(8);
        readonly List<NicknameEvidenceEntry> _nicknameEvidenceScratch = new(8);
        /// <summary>DEV-only rolling encounter+presentation stamps (playtest panel).</summary>
        readonly List<SocialDevEncounterStamp> _socialDevHistory = new(12);
        /// <summary>Short-lived comic escalation flash for arguments / fights (presentation only).</summary>
        float _socialEscalationUntil;
        Vector2 _socialEscalationGui;
        SocialSpeechValence _socialEscalationValence;

        struct SocialDevEncounterStamp
        {
            public SocialEncounterLog Log;
            public int Day;
            public float GameHour;
            public bool Presented;
            public bool SuppressedDistance;
            public bool SuppressedSleep;
            public string InitiatorLine;
            public string ResponseLine;
            public int LinesQueued;
        }

        Transform _avatarRoot;
        bool _devForceShowHiddenAvatars;
        int _assignmentDevWorkerIndex;
        bool _engineerWasEnRoute;
        bool _engineerWasRepairing;
        LogisticsTrafficMap _logisticsTraffic;
        MineInfrastructure _mineInfra;
        FreeMovementTerrainView _terrainView;
        InfrastructureDebugOverlay _infraDebug;
        TunnelCollapseSystem _collapse;
        bool _devCollapsePanel;
        RescueMission _activeRescue;
        int _nextRescueMissionId = 1;
        readonly WorkPriorityDirector _priorities = new();
        int _prioUiSelectedWorkerId;
        string _prioUiSelectedTaskId = WorkerGenericTaskIds.Rescue;
        bool _devPriorityPanel;
        string _prioHoverTaskTip;
        string _prioHoverCellTip;
        readonly List<WorkTaskDefinition> _prioJobTaskBuf = new(12);
        Transform _trackRoot;
        DeliveryCalculator _calc;
        BasecampYard _yard;
        Stockpile _hoverPile;
        Transform _worldRoot;
        Transform _looseRoot;
        Transform _lanternRoot;
        Transform _goalMarker;
        Sprite _pixel;
        Sprite _goalSprite;
        TacticalMapOverlay _tactical;
        ScanViewOverlay _scanView;
        ProspectorTacticalViewOverlay _playerTactical;
        ProspectorScanHistory _scanHistory;
        bool _scanHistoryBrowserOpen;
        Vector2 _scanHistoryScroll;
        readonly List<ProspectorScanRecord> _scanHistoryListScratch = new(16);
        readonly List<ProspectorFinding> _anomalyTimelineScratch = new(24);
        /// <summary>DEBUG omniscient map — labels as TRUTH VIEW. Not player evidence.</summary>
        TacticalMapOverlay TruthView => _tactical;

        string _findingToast;
        float _findingToastUntil;

        bool _scannerPlaceMode;
        bool _scannerPlanMode;
        ProspectorScannerEquipment _scannerGhost;
        ProspectorScannerEquipment _fieldScanner;
        Vector2 _scannerPlaceFacing = Vector2.up;
        string _scannerPlaceFail = "";
        bool _scannerPlaceValid;
        GasPocketFx _gasFx;
        CampSleepSite _sleepCamp;
        Light2D _globalLight;
        ExcavatedPathfinder[] _personNav = System.Array.Empty<ExcavatedPathfinder>();
        /// <summary>Person commute body radius (WorkerAvatar). Not host/machine footprint.</summary>
        const float AvatarCommuteRadius = 0.12f;
        /// <summary>
        /// LEGACY host footprints — excavator dig radius etc. Not used for person commute after F4.
        /// Index order historically matched ControlWorker, not WorkerRuntime roster.
        /// </summary>
        readonly float[] _providerFootprintR = { 0.22f, 0.12f, 0.14f, 0.12f, 0.12f, 0.12f };
        readonly WorkerBanter _banter = new();
        readonly RecruitmentHiringSession _hiring = new();
        /// <summary>Next WorkerId block for hired crews (avoids clashing with prototype 1–5).</summary>
        int _nextHireWorkerId = 101;
        bool _crewFromRecruitment;
        /// <summary>
        /// LEGACY enum stub — unused by selection / ST / banter after F5.
        /// Kept only for obsolete SelectWorker / DrawWorkerCard stubs.
        /// </summary>
        enum ControlWorker : byte { Prospector = 0, Excavator = 1, Hauler = 2, Refiner = 3, Engineer = 4 }
        ControlWorker _control = ControlWorker.Prospector;
        /// <summary>F2 canonical selected person (WorkerId). Stable across job reassignment.</summary>
        int _selectedWorkerId;

        // ——— Day / shift cycle (24h clock; planner default 08:00–16:00) ———
        enum CrewPhase : byte { OnShift = 0, HeadingHome = 1, Asleep = 2, HeadingOut = 3, CampEvening = 4 }
        /// <summary>F4: physical presence independent of assignment.</summary>
        enum WorkerPhysicalState : byte
        {
            Operating = 0,
            Idle = 1,
            CommutingHome = 2,
            Sleeping = 3,
            CommutingToWork = 4,
            Dead = 5,
            AtCamp = 6,
            Incapacitated = 7,
        }
        float ShiftStartHour => _shiftPlanner.ShiftStartHour;
        float ShiftEndHour => _shiftPlanner.ShiftEndHour;
        /// <summary>Real seconds per in-game hour (~shift ≈ 2 min).</summary>
        const float SecondsPerGameHour = 12f;
        /// <summary>Hard cap for physical commute home (real seconds).</summary>
        const float CommuteEmergencyTimeoutSec = 6f;
        /// <summary>If anyone is path-stranded, unlock after this.</summary>
        const float CommuteStrandedUnlockSec = 2f;
        /// <summary>How close is "at camp" for evening commute.</summary>
        const float CommuteArriveRadius = 0.48f;
        /// <summary>No meaningful movement for this long → soft-arrive that person.</summary>
        const float CommuteStuckSec = 1.1f;
        float _gameHour = 7.7f; // just before first whistle — watch them emerge
        int _dayIndex = 1;
        /// <summary>Monotonic game hours for scan metadata (survives day wrap).</summary>
        float _absoluteGameHours = 7.7f;
        CrewPhase _crewPhase = CrewPhase.HeadingOut;
        /// <summary>Provider morning bookmarks (machines/stations) — NOT crew person destinations.</summary>
        Vector2 _providerPostExcavator;
        Vector2 _providerPostProspector;
        Vector2 _providerPostHauler;
        Vector2 _providerPostRefiner;
        Vector2 _providerPostEngineer;
        Vector2 _providerPostSteward;
        /// <summary>Where the excavator machine was left at whistle — next shift avatar walks here.</summary>
        Vector2 _excavatorDigResume;
        bool _hasExcavatorDigResume;
        /// <summary>Person commute flags — indexed by crew roster order (Lewis…Viktor), not ControlWorker.</summary>
        bool[] _personArrived = System.Array.Empty<bool>();
        bool[] _personStranded = System.Array.Empty<bool>();
        float[] _personStuckTimer = System.Array.Empty<float>();
        Vector2[] _personLastCommutePos = System.Array.Empty<Vector2>();
        /// <summary>Subtle per-person lateral path stagger (world units), roster order.</summary>
        float[] _personLateral = { -0.035f, 0.04f, -0.02f, 0.03f, 0.015f, -0.01f };
        /// <summary>Inside-tent bed offsets from tent door (enter the house, don't loiter in the yard).</summary>
        static readonly Vector2[] CampHouseInteriorOffsets =
        {
            new(0.55f, 0.05f),
            new(0.85f, -0.25f),
            new(0.35f, -0.35f),
            new(1.05f, 0.15f),
            new(0.70f, 0.35f),
            new(1.15f, -0.15f),
            new(0.45f, 0.25f),
            new(0.95f, -0.40f),
        };
        float _commuteTimer;
        // Prefer finishing physical commute. Emergency timeout is CommuteEmergencyTimeoutSec.
        [SerializeField] bool navDebugDraw;
        [SerializeField] bool navDebugLog;

        // HUD hit-rects (GUI space, y-down) — block world dig/aim clicks
        readonly List<Rect> _hudBlockers = new(12);
        // Left column reserve — floating panels must dock outside this (no overlap)
        float _leftHudRight = 220f;
        float _leftHudBottom = 120f;
        float _leftHudContentTop = 50f;
        int _goldCells;
        int _goldSocketsTotal;
        int _goldFoundCells;
        int _goldFoundSockets;
        int _bedrockCells;
        int _lanternCount;
        /// <summary>Lowest excavated cell Y (deeper = smaller Y). Surface reference = StartY.</summary>
        int _deepestExcavatedY = int.MaxValue;
        AudioSource _mineAmbience;
        float _ambienceRetryAt;
        bool _ambienceLogged;
        readonly Mission01Runtime _mission01 = new();
        Mission01DevOverlay _mission01DevOverlay;

        float WorkerRadius => CellsAcrossWorker * 0.5f * cellSize;
        int StartX => _world.Width / 2;
        int StartY => 42;
        Vector2 BasecampPos => _world.CellCenter(StartX, StartY - 12);

        /// <summary>Persistent navigation anchor for return-to-camp (tent door or yard pad).</summary>
        Vector2 CampNavDestination
        {
            get
            {
                Vector2 door = _sleepCamp != null ? _sleepCamp.TentDoor : BasecampPos;
                return SnapPostToTunnel(door, AvatarCommuteRadius);
            }
        }

        bool OnShiftHours => _gameHour >= ShiftStartHour && _gameHour < ShiftEndHour;

        string FormatClock()
        {
            int h = Mathf.FloorToInt(_gameHour) % 24;
            int m = Mathf.FloorToInt((_gameHour - Mathf.Floor(_gameHour)) * 60f);
            return $"{h:00}:{m:00}";
        }

        void Start() => Build();

        void Build()
        {
            EnsureCamera();
            EnsureGlobalLight();
            EnsureMineAmbience();
            _pixel = DigVisualKit.Pixel;
            _goalSprite = MakeGoalSprite();

            // Larger mountain field — route planning / long digs
            int tw = 360;
            int th = 300;
            _world = new FineTerrainWorld(tw, th, cellSize);
            BuildMap();
            CountStats();

            _worldRoot = new GameObject("SocketMapWorld").transform;
            _looseRoot = new GameObject("Loose").transform;
            _looseRoot.SetParent(_worldRoot);
            _lanternRoot = new GameObject("Lanterns").transform;
            _lanternRoot.SetParent(_worldRoot);

            var viewGo = new GameObject("TerrainView");
            viewGo.transform.SetParent(_worldRoot, false);
            _terrainView = viewGo.AddComponent<FreeMovementTerrainView>();
            _terrainView.Setup(_world, fogOfWarGold: false, strongCliffEdges: true);
            // Rock wall ShadowCaster2D — used by crew headlamps only (lanterns keep shadows off).
            viewGo.AddComponent<RockWallShadows>().Setup(_world);
            TunnelAmbientFx.Attach(_worldRoot, _world, _looseRoot);
            GoldVeinShine.Attach(_worldRoot, _world);
            _tactical = TacticalMapOverlay.Attach(_worldRoot, _world);
            _mission01DevOverlay = Mission01DevOverlay.Attach(_worldRoot, _world);
            _mission01DevOverlay.Rebuild(_mission01.LastValidation);
            _mission01DevOverlay.Visible = false;
            _scanView = ScanViewOverlay.Attach(_worldRoot, _world);
            _scanView.ClearHints();
            _scanHistory = new ProspectorScanHistory();
            _scanHistory.Findings.FindingAdded += OnProspectorFindingAdded;
            _playerTactical = ProspectorTacticalViewOverlay.Attach(_worldRoot, _world, _scanHistory);
            _gasFx = GasPocketFx.Attach(_worldRoot, _world);
            _gasFx.PocketBreached += () => TryAssignedBanter(JobType.Excavation, "GasPocket",
                "Gas pocket! Purple haze — vent it.",
                "Hollow chamber. Air's wrong in here.",
                "Broke into a gas void. Watch the bit.",
                "Purple fog. Pocket's open.");

            DigVisualKit.PlaceLantern(_lanternRoot, _world.CellCenter(StartX, StartY - 4), local: true, intensity: 2.6f);
            _lanternCount = 1;

            _trackRoot = new GameObject("TrackRoot").transform;
            _trackRoot.SetParent(_worldRoot, false);

            _calc = new DeliveryCalculator();
            _yard = BasecampYard.Spawn(_worldRoot, BasecampPos);
            _sleepCamp = CampSleepSite.Spawn(_worldRoot, BasecampPos);
            _calc.BindStockpiles(_yard.Rock, _yard.Gold, _yard.RefinedGold, _yard.Dirt,
                _yard.Diamond, _yard.RefinedDiamond);
            SpawnWorker(_worldRoot);
            _hauler = HaulerPerson.Spawn(_worldRoot, _world, _yard.DropPoint, _calc);
            _refiner = RefinerPerson.Spawn(_worldRoot, _world, _yard, _calc);
            _engineer = EngineerPerson.Spawn(_worldRoot, _world,
                _yard != null ? _yard.DropPoint + new Vector2(0.55f, 0.35f) : BasecampPos);
            _engineer.BindExcavator(_worker);
            Vector2 stewardPost = _sleepCamp != null
                ? _sleepCamp.TentDoor + new Vector2(0.95f, -0.85f)
                : BasecampPos + new Vector2(-2.2f, -0.8f);
            _steward = StewardPerson.Spawn(_worldRoot, _world, stewardPost);
            _steward.BindCamp(_campLife);
            _steward.BindCrewLookup(() => _crewWorkers);
            _steward.BindPatientWorldPos(CrewWorldPos);
            _steward.BindSocialMemory(
                () => _socialAura != null && _socialAura.IsBootstrapped ? _socialAura.Memory : null,
                () => _absoluteGameHours);
            if (_sleepCamp != null)
            {
                _toiletCamp = CampToiletSite.Spawn(_worldRoot, BasecampPos, _sleepCamp.TentDoor);
                Vector2 washerPos = _yard?.Washer != null
                    ? (Vector2)_yard.Washer.transform.localPosition
                    : BasecampPos + new Vector2(-0.05f, -0.35f);
                _prospectWorkstation = ProspectorWorkstationSite.Spawn(_worldRoot, washerPos);
            }

            _logisticsTraffic = new LogisticsTrafficMap(_world);
            _terrainView?.BindPresentation(_logisticsTraffic, BasecampPos, campRadiusCells: 24f);
            _mineInfra = new MineInfrastructure(_world, _logisticsTraffic, _trackRoot, _lanternRoot);
            _mineInfra.BindExcavator(_worker);
            _mineInfra.BindDropPoint(_yard.DropPoint);
            _mineInfra.RegisterLantern(_world.CellCenter(StartX, StartY - 4));
            _collapse = new TunnelCollapseSystem(_world, _mineInfra, _worldRoot);
            _collapse.BindCamp(BasecampPos);
            // Starter pad must stay walkable — never begin with yard debris
            _collapse.ClearDebrisNearCamp(TunnelCollapseSystem.CampSafeRadiusWorld);
            _hauler.BindLogistics(_logisticsTraffic, _mineInfra);
            _engineer.BindInfrastructure(_mineInfra);
            _infraDebug = InfrastructureDebugOverlay.Attach(_worldRoot, _world, _mineInfra);
            _infraDebug.Visible = false;
            if (_worker != null)
                _worker.DebrisStrike += OnExcavatorDebrisStrike;
            _prospector = ProspectorPerson.Spawn(_worldRoot, _world,
                _world.CellCenter(StartX - 6, StartY - 2), _scanView);
            _prospector.BindScanHistory(_scanHistory);

            // Persistent Worker Body V1: hosts are equipment/FSM — never show duplicate people
            PersistentWorkerBody.HideOperatorBodiesOnHosts(
                _worker, _prospector, _hauler, _refiner, _engineer, _steward);

            BootstrapPrototypeCrew();

            // Default schedule: 08:00–16:00 (8h). SHIFT planner can change later.
            _shiftPlanner.SetShift(ShiftPlanner.DefaultStart, ShiftPlanner.DefaultEnd);

            _prospector.BindExcavator(_worker);
            _prospector.BindRefiner(_refiner);
            if (_prospectWorkstation != null)
            {
                _prospector.BindWorkstation(_prospectWorkstation);
                _refiner.SetConsultationMeetOverride(_prospectWorkstation.RefinerStand);
            }

            RefreshProviderWorkPosts();

            InjuryResponseHub.OnInjuryApplied = OnInjuryResponseApplied;

            // First whistle — walk to stable work posts, then EnterOnShift unlocks jobs
            BeginHeadingOut(announce: false);
            _prospector.ScanStarted += () => TryAssignedBanter(JobType.Prospecting, "ScanStarted",
                "Radar live. Sweeping the dark.",
                "Listening to the rock…",
                "Cone up. Let's read the mountain.");
            _prospector.GoldHintFound += () => TryAssignedBanter(JobType.Prospecting, "GoldHint",
                "Soft amber return — possible vein.",
                "Gold whisper on the scope. Mark it.",
                "That's not noise. That's money.",
                "Faint yellow. Don't lose the bearing.");
            _prospector.BedrockHintFound += () => TryAssignedBanter(JobType.Prospecting, "BedrockHint",
                "Hard mass ahead — route around if you can.",
                "Bedrock lobe on scan. Plan the cut.",
                "Solid wall signature. Find the seam.",
                "Cyan plate. Excavator's going to hate that.");
            _prospector.GasHintFound += () => TryAssignedBanter(JobType.Prospecting, "GasHint",
                "Purple void on the scope — sealed pocket.",
                "Gas signature. Don't punch that blind.",
                "Hollow return. Air's wrong in there.",
                "Pressure pocket. Mark it and dig careful.");
            _prospector.SurveyWhisper += () => TryAssignedBanter(JobType.Prospecting, "SurveyWhisper",
                "Studying the wall… maybe something.",
                "Could be gold. Could be wishful thinking.",
                "Grain looks promising. No guarantees.",
                "Quiet return. Don't bet the trip on it.",
                "Nothing clean — still poking around.");
            _prospector.AssistNote += () => TryAssignedBanter(JobType.Prospecting, "AssistNote",
                "Reading the face for you.",
                "Seam mapped — dig should bite cleaner.",
                "Rock grain noted. Don't thank me yet.",
                "Behind you. Softening the bite.");
            _prospector.InvestigationFinding += () =>
            {
                TryAssignedBanter(JobType.Prospecting, "InvestigationFinding",
                    "Boss. I've got something. Check the Tactical View.",
                    "New findings, boss. Tactical View — look.",
                    "Got something. Worth a look on Tactical.");
            };

            _hauler.PickedGold += () => TryAssignedBanter(JobType.Hauling, "PickedGold",
                "Gold in the cart. Easy does it.",
                "Yellow load — this trip pays.",
                "Careful on the corners. Precious cargo.");
            _hauler.PickedRock += () => TryAssignedBanter(JobType.Hauling, "PickedRock",
                "Another rock. Cart's gettin' heavy.",
                "Fillin' up on stone. Base wants it anyway.");
            _hauler.Deposited += () => TryAssignedBanter(JobType.Hauling, "Deposited",
                "Dropped at base. Back into the hole.",
                "Stockpile fed. Round trip done.",
                "Unload complete. Seekin' the next pile.");

            _refiner.StartedWash += () => TryAssignedBanter(JobType.Refining, "StartedWash",
                "Cell in the drum. Splitting sockets…",
                "Washer spinning. Let's see what she holds.",
                "One cell at a time — no shortcuts.");
            _refiner.FoundGold += () => TryAssignedBanter(JobType.Refining, "FoundGold",
                "Yellow in the rinse!",
                "Socket paid out. Into the clean pile.",
                "That's a keeper.");
            _refiner.FoundDiamond += () => TryAssignedBanter(JobType.Refining, "FoundDiamond",
                "Ice! Diamond socket — clean as glass.",
                "Crystal in the wash. That's the money.",
                "Hard sparkle. Refined diamond out.");
            _refiner.BatchDone += () => TryAssignedBanter(JobType.Refining, "BatchDone",
                "Batch clear. Next cell.",
                "Dirt one way, precious the other.",
                "Sockets divided. Machine ready.");

            _control = ControlWorker.Prospector;
            FrameCamera();

            Debug.Log($"[SocketMap] SHIFT 08–18 · N skip sleep · SPACE radar→scan · worker cards");
        }

        void BuildMap()
        {
            _world.ResetAllSolidRock();
            int tw = _world.Width, th = _world.Height;

            for (int x = 0; x < tw; x++)
            {
                _world.Set(x, 0, FineTerrainWorld.MakeBedrock());
                _world.Set(x, th - 1, FineTerrainWorld.MakeBedrock());
            }
            for (int y = 0; y < th; y++)
            {
                _world.Set(0, y, FineTerrainWorld.MakeBedrock());
                _world.Set(tw - 1, y, FineTerrainWorld.MakeBedrock());
            }

            // Camp / dig mouth — wide soft apron into the mountain + south pad for camp props
            // Toilet/fire/beds sit south of BasecampPos; half-oval only carved upward from floorY.
            int floorY = StartY - 34; // was StartY-16 — covers toilet/fire apron (~cell Y8+)
            GoldVeinPlacer.ExcavateHalfOval(_world, StartX, floorY, radiusX: 56, radiusY: 38);
            GoldVeinPlacer.ExcavateHalfOval(_world, StartX, StartY - 2, radiusX: 18, radiusY: 10);
            GoldVeinPlacer.ExcavateHalfOval(_world, StartX, StartY - 12, radiusX: 36, radiusY: 16);
            // Rectangular camp pad under southern props (fire, toilet, beds, piles)
            ExcavateCampSouthPad();

            // Soft highway + sparse landmarks, then Mission 01 prospecting layout
            GoldVeinPlacer.BuildSocketMapStarterMaze(_world, StartX, StartY);
            var layout = Mission01Geology.Apply(_world, StartX, StartY);
            var validation = Mission01Geology.Validate(_world, layout);
            _mission01.Reset(layout, validation);
            if (validation.Passed)
                DigHoodLog.Push("MISSION 01 | Map ready — find GOLD + DIA in 14 days");
            else
                DigHoodLog.Push($"MISSION 01 | Map validation FAIL ({validation.Failures.Count})");
        }

        /// <summary>
        /// Fill south of BasecampPos so toilet/fire/beds sit on excavated floor (no void black).
        /// </summary>
        void ExcavateCampSouthPad()
        {
            // Toilet ~cell Y9, fire ~Y15, Basecamp cell Y30 — pad from Y8 to Y30
            int y0 = StartY - 34;
            int y1 = StartY - 12;
            int x0 = StartX - 48;
            int w = 96;
            int h = Mathf.Max(1, y1 - y0);
            _world.ExcavateRect(x0, y0, w, h);
        }

        void CountStats()
        {
            _goldCells = 0;
            _goldSocketsTotal = 0;
            _bedrockCells = 0;
            for (int y = 0; y < _world.Height; y++)
            for (int x = 0; x < _world.Width; x++)
            {
                var c = _world.Get(x, y);
                if (c.Phase == TerrainPhase.Excavated) continue;
                if (c.IsUndamageableBorder) continue;
                int g = c.GoldCount;
                if (g > 0)
                {
                    _goldCells++;
                    _goldSocketsTotal += g;
                }
                if (c.BedrockCount >= 2) _bedrockCells++;
            }
        }

        void SpawnWorker(Transform root)
        {
            var pinsRoot = new GameObject("DigRoutePins").transform;
            pinsRoot.SetParent(root, false);
            _goalMarker = pinsRoot; // reuse field as pins root

            var go = new GameObject("Driller");
            go.transform.SetParent(root);
            go.transform.localScale = Vector3.one;

            // No omni body glow — only the helmet flashlight cone lights ahead
            _worker = go.AddComponent<FreeWorkerController>();
            _worker.Setup(_world, _world.CellCenter(StartX, StartY), WorkerRadius,
                pinsRoot, OnBroke, OnDigImpact, _goalSprite);
            _worker.SetRouteVisible(false);
            _worker.ResolveClaustroRefuseCeiling = ResolveClaustroRefuseCeiling;
            _worker.OnClaustroRefusePause = PresentClaustroRefuseBanner;
            DigVisualKit.AttachDrillerVisual(go, _worker);

            _worker.WeakPointFound += (wx, wy) =>
            {
                WeakPointFx.Play(_worldRoot, _world, wx, wy);
                TryAssignedBanter(JobType.Excavation, "WeakPoint",
                    "Nailed it.",
                    "Weak Point!",
                    "Got this.",
                    "There — soft spot.",
                    "Crack opens. Push it.");
            };
            _balance.Bind(_worker);
        }

        void BindWorkerStateEventService()
        {
            WorkerStateClock.GameHours = _absoluteGameHours;
            _stateEvents.Bind(FindCrewWorker);
            WorkerStateEventHub.Service = _stateEvents;
            _prospectorDrySpell.Reset();
        }

        /// <summary>V1.2C: apply activity demand / idle recovery from live job FSMs.</summary>
        void TickProspectorDrySpell(float onShiftHoursDelta)
        {
            if (_crewWorkers == null || onShiftHoursDelta <= 0f) return;
            // Active Prospecting assignment only — unassigned / other jobs do not accrue dry-spell.
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null) continue;
                var asg = _assignments.GetAssignment(wr.WorkerId);
                if (asg == null || asg.JobType != JobType.Prospecting) continue;
                if (!CanPerformJobActions(wr)) continue;
                _prospectorDrySpell.Tick(
                    wr.WorkerId,
                    onShiftHoursDelta,
                    asg.ProviderId ?? "");
                // Only one prospecting seat in prototype roster.
                break;
            }
        }

        readonly List<Vector2> _claustroCoworkerScratch = new(8);

        void TickClaustrophobia(float hoursDelta)
        {
            if (_crewWorkers == null || hoursDelta <= 0f || _world == null) return;
            if (_crewPhase == CrewPhase.Asleep) return; // sleep recovery handles via WorkerState

            _claustroCoworkerScratch.Clear();
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null || !wr.IsAlive) continue;
                _claustroCoworkerScratch.Add(CrewWorldPos(wr));
            }

            Vector2? excavPos = _worker != null ? _worker.Position : (Vector2?)null;
            Vector2 camp = BasecampPos;

            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr?.State == null || !wr.IsAlive) continue;
                Vector2 pos = CrewWorldPos(wr);

                // Exclude self from isolation check
                _claustroCoworkerScratch[i] = camp + Vector2.one * 999f; // temp remove self
                var env = ClaustrophobiaSystem.SampleEnvironment(
                    _world, pos, camp, _mineInfra, excavPos, _claustroCoworkerScratch);
                _claustroCoworkerScratch[i] = pos;

                var asg = _assignments?.GetAssignment(wr.WorkerId);
                JobType job = asg != null ? asg.JobType : JobType.Unassigned;
                string pid = asg?.ProviderId ?? "";

                ClaustrophobiaSystem.TickPerson(
                    wr, hoursDelta, env, wr.State.TrappedFromCamp, job, pid,
                    tryBanter: (w, line) =>
                    {
                        if (w == null || string.IsNullOrEmpty(line)) return;
                        var a = _assignments?.GetAssignment(w.WorkerId);
                        var j = a != null ? a.JobType : JobType.Unassigned;
                        if (!TryWorkerBanter(w, j, "Claustrophobia", line))
                            TryAuthoredBanter(w.WorkerId, w.DisplayName, j, "Claustrophobia", line);
                    },
                    onSeekExit: OnClaustroSeekExit);
            }
        }

        void OnClaustroSeekExit(WorkerRuntime wr)
        {
            if (wr == null || wr.State == null) return;

            // Open camp/yard — never pause dig from confinement (false positives at perimeter)
            if (Vector2.Distance(CrewWorldPos(wr), BasecampPos) < 5.2f)
            {
                wr.State.ClaustroSeekingExit = false;
                _worker?.ResumeExecutionIfPossible();
                return;
            }

            // Manager push willingness — still complying; do not re-pause
            var flags = _managerIntents.Get(wr.WorkerId);
            if (flags.AcceptedPush
                && flags.WillingUntilStress > ClaustrophobiaBands.CriticalAt
                && wr.State.ClaustrophobicStress < flags.WillingUntilStress)
                return;

            // Pause dig execution — never clear / replace player-painted route
            if (_worker != null && _worker.AssignedWorkerId == wr.WorkerId)
                _worker.PauseExecution(
                    "CLAUSTROPHOBIA | REFUSES TO CONTINUE — plan retained");

            var rel = _managerRels?.Get(wr.WorkerId);
            if (rel != null && wr.State.ClaustrophobicStress >= ClaustrophobiaBands.CriticalAt)
                rel.Add(-0.15f, 0f, 0.35f);

            PresentClaustroRefuseBanner(wr);
        }

        void PresentClaustroRefuseBanner(WorkerRuntime wr)
        {
            if (wr == null) return;
            if (Time.unscaledTime < _lastClaustroRefuseBannerUnscaled + 2.8f) return;
            _lastClaustroRefuseBannerUnscaled = Time.unscaledTime;
            string line = "Not deeper. Not like this.";
            if (ClaustrophobiaSystem.TryGetLastCauses(wr.WorkerId, out var c)
                && c.Light >= TunnelLightBand.PitchBlack)
                line = "I can't stay down here. Get me out.";
            _lastTalkResult = new ManagerCommResult
            {
                WorkerId = wr.WorkerId,
                DisplayName = wr.DisplayName,
                Reaction = ManagerReactionKind.Refused,
                ReactionLabel = "REFUSES TO CONTINUE",
                Line = line,
                Valence = SocialSpeechValence.Severe,
                AcceptedIntent = false,
                WhyPlain = "Critical confinement — paused, route pending",
            };
            _lastTalkResultUntilUnscaled = Time.unscaledTime + 4.2f;
            var asg = _assignments?.GetAssignment(wr.WorkerId);
            var job = asg != null ? asg.JobType : JobType.Unassigned;
            TryAuthoredSocialBanter(
                wr.WorkerId, wr.DisplayName, job, "Claustrophobia", line,
                SocialSpeechValence.Severe);
            DigHoodLog.Push($"CLAUSTRO | {wr.DisplayName}: REFUSES TO CONTINUE — plan retained");
        }

        void TickJobDemands(float gameHoursDelta)
        {
            if (_crewWorkers == null || _assignments == null || gameHoursDelta <= 0f) return;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr?.State == null) continue;

                if (!CanPerformJobActions(wr))
                {
                    WorkerJobDemand.TickIdleOrRest(wr, gameHoursDelta, resting: wr.State.IsResting);
                    continue;
                }

                var asg = _assignments.GetAssignment(wr.WorkerId);
                var job = asg != null ? asg.JobType : JobType.Unassigned;
                if (job == JobType.Unassigned)
                {
                    WorkerJobDemand.TickIdleOrRest(wr, gameHoursDelta, resting: false);
                    continue;
                }

                JobDemandProfile demand = ResolveDemandFor(wr, job);
                string providerId = asg != null ? asg.ProviderId : "";
                if (demand.IsIdle || wr.State.IsResting)
                    WorkerJobDemand.TickIdleOrRest(wr, gameHoursDelta, resting: wr.State.IsResting);
                else
                    WorkerJobDemand.TickActive(wr, demand, gameHoursDelta, job, providerId);
            }
        }

        JobDemandProfile ResolveDemandFor(WorkerRuntime wr, JobType job)
        {
            switch (job)
            {
                case JobType.Excavation:
                    return ReferenceEquals(_worker?.AssignedWorker, wr)
                        ? JobDemandCatalog.ForExcavation(_worker)
                        : JobDemandProfile.Idle;
                case JobType.Prospecting:
                    return ReferenceEquals(_prospector?.AssignedWorker, wr)
                        ? JobDemandCatalog.ForProspecting(_prospector)
                        : JobDemandProfile.Idle;
                case JobType.Hauling:
                    return ReferenceEquals(_hauler?.AssignedWorker, wr)
                        ? JobDemandCatalog.ForHauling(_hauler)
                        : JobDemandProfile.Idle;
                case JobType.Refining:
                    return ReferenceEquals(_refiner?.AssignedWorker, wr)
                        ? JobDemandCatalog.ForRefining(_refiner)
                        : JobDemandProfile.Idle;
                case JobType.Engineering:
                    return ReferenceEquals(_engineer?.AssignedWorker, wr)
                        ? JobDemandCatalog.ForEngineering(_engineer)
                        : JobDemandProfile.Idle;
                default:
                    return JobDemandProfile.Idle;
            }
        }

        JobDemandProfile AuditResolveDemand(int workerId)
        {
            var wr = FindCrewWorker(workerId);
            if (wr == null) return JobDemandProfile.Idle;
            return ResolveDemandFor(wr, AuditJobOf(workerId));
        }

        /// <summary>
        /// Stage A: create 5 WorkerRuntime people and bind each role body to their shared stats sheet.
        /// Fixed 1:1 — no reassignment yet.
        /// </summary>
        void BootstrapPrototypeCrew()
        {
            _workerLewis = new WorkerRuntime(1, "Lewis");
            _workerMara = new WorkerRuntime(2, "Mara");
            _workerKowalski = new WorkerRuntime(3, "Kowalski");
            _workerElena = new WorkerRuntime(4, "Elena");
            _workerViktor = new WorkerRuntime(5, "Viktor");
            _workerSteward = new WorkerRuntime(6, "Kit");
            // Prototype Steward: solid camp support without breaking prior 5-role tests.
            _workerSteward.Stats.Set(WorkerStatId.Chemistry, 13);
            _workerSteward.Stats.Set(WorkerStatId.Finesse, 12);
            _workerSteward.Stats.Set(WorkerStatId.Empathy, 14);
            _workerSteward.Stats.Set(WorkerStatId.Recovery, 13);
            _workerSteward.Stats.Set(WorkerStatId.Logistics, 12);
            _workerSteward.Stats.Set(WorkerStatId.SafetyProtocol, 13);
            _workerSteward.Stats.Set(WorkerStatId.Focus, 12);
            _workerSteward.Stats.Set(WorkerStatId.Composure, 13);
            _workerSteward.Stats.Set(WorkerStatId.WorkRate, 12);
            _workerSteward.Stats.ClampAll();
            _crewWorkers = new[]
            {
                _workerLewis,
                _workerMara,
                _workerKowalski,
                _workerElena,
                _workerViktor,
                _workerSteward,
            };

            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                if (_crewWorkers[i] == null) continue;
                WorkerPriorityPrefs.ApplyCrewDefaults(_crewWorkers[i], _crewWorkers[i].DisplayName);
            }
            _prioUiSelectedWorkerId = _workerLewis != null ? _workerLewis.WorkerId : 0;

            BindWorkerStateEventService();

            _prospector?.BindWorker(_workerLewis);
            _worker?.BindWorker(_workerMara);
            _hauler?.BindWorker(_workerKowalski);
            _refiner?.BindWorker(_workerElena);
            _engineer?.BindWorker(_workerViktor);
            _steward?.BindWorker(_workerSteward);

            // Verify: body.Stats == WorkerRuntime.Stats (same ref); workers do not share sheets
            Debug.Assert(_prospector == null || ReferenceEquals(_prospector.Stats, _workerLewis.Stats));
            Debug.Assert(_worker == null || ReferenceEquals(_worker.Stats, _workerMara.Stats));
            Debug.Assert(_hauler == null || ReferenceEquals(_hauler.Stats, _workerKowalski.Stats));
            Debug.Assert(_refiner == null || ReferenceEquals(_refiner.Stats, _workerElena.Stats));
            Debug.Assert(_engineer == null || ReferenceEquals(_engineer.Stats, _workerViktor.Stats));
            Debug.Assert(_steward == null || ReferenceEquals(_steward.Stats, _workerSteward.Stats));
            Debug.Assert(!ReferenceEquals(_workerLewis.Stats, _workerMara.Stats));
            Debug.Assert(!ReferenceEquals(_workerMara.Stats, _workerKowalski.Stats));
            Debug.Assert(!ReferenceEquals(_workerKowalski.Stats, _workerElena.Stats));
            Debug.Assert(!ReferenceEquals(_workerElena.Stats, _workerViktor.Stats));
            Debug.Assert(!ReferenceEquals(_workerViktor.Stats, _workerSteward.Stats));

            // Balance harness captured excavator sheet before BindWorker — refresh baseline from Mara
            if (_worker != null)
                _balance.Bind(_worker);

            _sheetBaselineByWorkerId.Clear();
            _sheetBaselineCaptured.Clear();
            _sheetProfileByWorkerId.Clear();

            // Stage B: assignment model bootstrap, then stamp live provider ids
            _assignments.BootstrapPrototypeDefaults(_crewWorkers, _absoluteGameHours);
            StampLiveProviderAssignments();
            _assignments.AssertPrototypeIntegrity(_crewWorkers);
            AssertStageBBodyJobMapping();
            _assignmentDevWorkerIndex = 0;
            SyncProspectingFindingsAuthor();
            SpawnCrewAvatars();
            RefreshAllAvatarPresence();
            CaptureSheetBaselinesForCrew();
            // F2: person-first selection — Lewis by default (not legacy role slot)
            _control = ControlWorker.Prospector; // LEGACY unused stub
            if (_workerLewis != null)
                SelectPersonById(_workerLewis.WorkerId);
            else if (_crewWorkers != null && _crewWorkers.Length > 0 && _crewWorkers[0] != null)
                SelectPersonById(_crewWorkers[0].WorkerId);

            DigHoodLog.Push(
                "CREW | F0.5–F5 — person commute + person-authored banter");

            _socialAura.Bootstrap(_crewWorkers);
            EarlyCrewPressure.Deactivate();
            _socialPlaytest.Reset(_dayIndex, _socialAura);
            _coopWorkPlaytest.Reset(_dayIndex);
            BindExcavatorEngineerCooperation();
            _crewFromRecruitment = false;
            _managerRels.EnsureCrew(_crewWorkers);
            _dayTracker.EnsureCrew(_crewWorkers);
        }

        void BindExcavatorEngineerCooperation()
        {
            if (_engineer == null) return;
            _engineer.BindCooperation(EvaluateExcavatorEngineerCoop);
            _engineer.OnRepairDispatched = coop =>
                _coopWorkPlaytest.NotifyDispatch(_dayIndex, coop);
            _engineer.OnRepairBegun = coop =>
                _coopWorkPlaytest.NotifyRepairBegin(_dayIndex, coop);
            _engineer.OnRepairCompleted = c =>
                _coopWorkPlaytest.NotifyRepairComplete(_dayIndex, c);
            _engineer.OnRepairInterrupted = () =>
                _coopWorkPlaytest.NotifyRepairInterrupted(_dayIndex);
        }

        CooperationAssessment EvaluateExcavatorEngineerCoop()
        {
            var excavOp = _worker != null ? _worker.AssignedWorker : null;
            var engOp = _engineer != null ? _engineer.AssignedWorker : null;
            if (excavOp == null || engOp == null || !_socialAura.IsBootstrapped)
                return CooperationAssessment.Inactive();
            return ExcavatorEngineerCooperation.Evaluate(excavOp, engOp, _socialAura.World);
        }

        void DevCycleTestRelationship()
        {
            if (!_socialAura.IsBootstrapped) return;
            int mara = _workerMara != null ? _workerMara.WorkerId : RelationshipWorkPlaytestTracker.DefaultExcavId;
            int viktor = _workerViktor != null ? _workerViktor.WorkerId : RelationshipWorkPlaytestTracker.DefaultEngId;
            var preset = _coopWorkPlaytest.CycleAndApply(
                _socialAura.World, _socialAura.Memory, mara, viktor, _absoluteGameHours);
            DigHoodLog.Push($"DEV | SET TEST RELATIONSHIP → {_coopWorkPlaytest.LastPresetLabel} (Mara↔Viktor)");
            _ = preset;
        }

        void SpawnCrewAvatars()
        {
            if (_crewWorkers == null || _worldRoot == null) return;
            _presence.Clear();
            if (_avatarRoot != null)
                Destroy(_avatarRoot.gameObject);
            _avatarRoot = new GameObject("WorkerAvatars").transform;
            _avatarRoot.SetParent(_worldRoot, false);

            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null) continue;
                // Seed near default host so first Show (if any) is not origin pile-up
                Vector2 seed = GetDefaultAvatarSeedPosition(wr);
                var av = WorkerAvatar.Spawn(_avatarRoot, wr.WorkerId, wr.DisplayName, seed);
                av.BindWorker(wr);
                _presence.Register(av);
            }
        }

        /// <summary>
        /// Recruitment V1: open hiring UI. Does not touch the active crew until CONFIRM.
        /// </summary>
        void OpenRecruitmentHiring()
        {
            _hiring.Open();
            DigHoodLog.Push("RECRUIT | Hiring screen open — crew unchanged until CONFIRM");
        }

        void CancelRecruitmentHiring()
        {
            _hiring.Close();
            DigHoodLog.Push("RECRUIT | Cancelled — active crew preserved");
        }

        void ConfirmRecruitmentHiring()
        {
            if (!_hiring.TryBuildCrew(_nextHireWorkerId, out var crew, out var jobs, out string fail))
            {
                _hiring.StatusMessage = fail;
                DigHoodLog.Push($"RECRUIT | Confirm blocked — {fail}");
                return;
            }

            _nextHireWorkerId += 10;
            ReplaceActiveCrew(crew, jobs, fromRecruitment: true);
            _hiring.Close();
            DigHoodLog.Push("RECRUIT | Crew confirmed — mission crew replaced");
            BeginHeadingOut(announce: true);
        }

        /// <summary>DEV: restore Lewis/Mara/Kowalski/Elena/Viktor prototype crew.</summary>
        void ResetToDefaultCrew()
        {
            _hiring.Close();
            ForceClearAllJobHosts();
            _assignments.Clear();
            BootstrapPrototypeCrew();
            DigHoodLog.Push("RECRUIT | RESET TO DEFAULT CREW");
            BeginHeadingOut(announce: false);
        }

        void ForceClearAllJobHosts()
        {
            foreach (var job in JobStatPreview.OccupiedJobs)
            {
                YieldHost(job);
                ClearHost(job);
            }
        }

        void SyncNamedCrewAliasesFromJobs()
        {
            _workerLewis = FindWorkerAssignedTo(JobType.Prospecting);
            _workerMara = FindWorkerAssignedTo(JobType.Excavation);
            _workerKowalski = FindWorkerAssignedTo(JobType.Hauling);
            _workerElena = FindWorkerAssignedTo(JobType.Refining);
            _workerViktor = FindWorkerAssignedTo(JobType.Engineering);
            _workerSteward = FindWorkerAssignedTo(JobType.Steward);
        }

        WorkerRuntime FindWorkerAssignedTo(JobType job)
        {
            int wid = _assignments.GetWorkerIdForJob(job);
            return wid > 0 ? FindCrewWorker(wid) : null;
        }

        void ReplaceActiveCrew(WorkerRuntime[] crew, JobType[] jobs, bool fromRecruitment)
        {
            if (crew == null || jobs == null || crew.Length != jobs.Length)
                return;

            ForceClearAllJobHosts();
            _assignments.Clear();
            _banter.Clear();
            _crewWorkers = crew;
            _crewFromRecruitment = fromRecruitment;

            BindWorkerStateEventService();

            for (int i = 0; i < crew.Length; i++)
            {
                var wr = crew[i];
                var job = jobs[i];
                if (wr == null || job == JobType.Unassigned) continue;
                BindHost(job, wr);
                _assignments.AssignInternal(
                    wr.WorkerId, job, ResolveProviderId(job), _absoluteGameHours);
                NotifyProviderAssigned(job, wr);
            }

            SyncNamedCrewAliasesFromJobs();
            StampLiveProviderAssignments();
            _assignments.AssertPrototypeIntegrity(_crewWorkers);
            AssertStageBBodyJobMapping();

            _sheetBaselineByWorkerId.Clear();
            _sheetBaselineCaptured.Clear();
            _sheetProfileByWorkerId.Clear();

            if (_worker != null)
                _balance.Bind(_worker);

            SyncProspectingFindingsAuthor();
            SpawnCrewAvatars();
            RefreshAllAvatarPresence();
            CaptureSheetBaselinesForCrew();

            if (_crewWorkers.Length > 0 && _crewWorkers[0] != null)
                SelectPersonById(_crewWorkers[0].WorkerId);

            _socialAura.Bootstrap(_crewWorkers);
            if (fromRecruitment)
                EarlyCrewPressure.ActivateForHiredCrew(_socialAura.World, _crewWorkers);
            else
                EarlyCrewPressure.Deactivate();
            _socialPlaytest.Reset(_dayIndex, _socialAura);
            _coopWorkPlaytest.Reset(_dayIndex);
            BindExcavatorEngineerCooperation();
            RefreshProviderWorkPosts();
        }

        Vector2 GetDefaultAvatarSeedPosition(WorkerRuntime wr)
        {
            if (wr == null) return Vector2.zero;
            var job = GetAssignmentJob(wr);
            if (job != JobType.Unassigned)
            {
                Vector2 op = GetProviderOperatePoint(job);
                return op + job switch
                {
                    JobType.Prospecting => new Vector2(-0.35f, 0.2f),
                    JobType.Excavation => new Vector2(0.35f, 0.15f),
                    JobType.Hauling => new Vector2(-0.3f, -0.25f),
                    JobType.Refining => new Vector2(0.3f, -0.2f),
                    JobType.Engineering => new Vector2(0.25f, 0.3f),
                    JobType.Steward => new Vector2(-0.2f, 0.15f),
                    _ => Vector2.zero,
                };
            }
            // Legacy prototype seed by WorkerId 1–6
            return wr.WorkerId switch
            {
                1 => _prospector != null ? _prospector.Position + new Vector2(-0.35f, 0.2f) : Vector2.zero,
                2 => _worker != null ? _worker.Position + new Vector2(0.35f, 0.15f) : Vector2.zero,
                3 => _hauler != null ? _hauler.Position + new Vector2(-0.3f, -0.25f) : Vector2.zero,
                4 => _refiner != null ? _refiner.Position + new Vector2(0.3f, -0.2f) : Vector2.zero,
                5 => _engineer != null ? _engineer.Position + new Vector2(0.25f, 0.3f) : Vector2.zero,
                6 => _steward != null ? _steward.Position + new Vector2(-0.2f, 0.15f) : Vector2.zero,
                _ => Vector2.zero,
            };
        }

        /// <summary>
        /// Physical person position for a job: where the operator is while working.
        /// Uses the behaviour host Transform (person/machine) so presence tracks real motion.
        /// </summary>
        Vector2 GetProviderOperatePoint(JobType job)
        {
            switch (job)
            {
                case JobType.Prospecting:
                    // Operator body is ProspectorPerson (travels to scanner, investigates, etc.)
                    return _prospector != null ? _prospector.Position : Vector2.zero;
                case JobType.Excavation:
                    return _worker != null ? _worker.Position : Vector2.zero;
                case JobType.Hauling:
                    return _hauler != null ? _hauler.Position : Vector2.zero;
                case JobType.Refining:
                    return _refiner != null ? _refiner.Position : Vector2.zero;
                case JobType.Engineering:
                    return _engineer != null ? _engineer.Position : Vector2.zero;
                case JobType.Steward:
                    return _steward != null ? _steward.Position : Vector2.zero;
                default:
                    return Vector2.zero;
            }
        }

        /// <summary>Exit snap near host when leaving (V1 — no travel automation).</summary>
        Vector2 GetProviderExitPoint(JobType job)
        {
            Vector2 op = GetProviderOperatePoint(job);
            if (job == JobType.Excavation)
                return ExcavatorCabin.ExitWorld(op);
            return op + job switch
            {
                JobType.Hauling => new Vector2(-0.28f, 0.12f),
                JobType.Prospecting => new Vector2(0.22f, -0.18f),
                JobType.Refining => new Vector2(-0.35f, -0.15f),
                JobType.Engineering => new Vector2(0.3f, -0.2f),
                JobType.Steward => new Vector2(-0.25f, -0.15f),
                _ => Vector2.zero,
            };
        }

        void ParkAvatarLeavingJob(WorkerRuntime wr, JobType job)
        {
            if (wr == null || job == JobType.Unassigned) return;
            var av = _presence.Get(wr);
            if (av == null) return;
            // Leave host operator body hidden (equipment stays); excavator cabin exit
            PersistentWorkerBody.SetOperatorBodyVisibleForJob(
                job, _worker, _prospector, _hauler, _refiner, _engineer, _steward, false);
            if (PersistentWorkerBody.IsMachineCabinJob(job) && _worker != null)
                ExcavatorCabin.Exit(av, _worker.Position);
            else
            {
                av.ParkAt(GetProviderExitPoint(job));
                av.ClearFollowing();
                av.Show();
            }
            av.SetDevForceShowHidden(_devForceShowHiddenAvatars);
        }

        /// <summary>
        /// Sync every avatar Transform to assignment while OnShift:
        /// Excavation → cabin hide; other jobs → persistent avatar visible at host (host body hidden).
        /// Off-shift: commute/sleep owns presence — do not snap to providers.
        /// </summary>
        void RefreshAllAvatarPresence()
        {
            if (_crewWorkers == null) return;
            if (_crewPhase != CrewPhase.OnShift && _crewPhase != CrewPhase.HeadingOut)
                return;

            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null) continue;
                var av = _presence.Get(wr.WorkerId);
                if (av == null) continue;

                var asg = _assignments.GetAssignment(wr.WorkerId);
                var L = _dayTracker.Get(wr.WorkerId);
                if (asg == null || asg.JobType == JobType.Unassigned || L == null || !L.ArrivedWork)
                {
                    av.ClearFollowing();
                    if (av.IsVisuallyHidden)
                        av.Show();
                    av.SetDevForceShowHidden(_devForceShowHiddenAvatars);
                    continue;
                }

                Vector2 op = GetProviderOperatePoint(asg.JobType);
                float d = Vector2.Distance(av.PresencePosition, op);
                if (d > 0.85f)
                {
                    av.ClearFollowing();
                    if (av.IsVisuallyHidden) av.Show();
                    continue;
                }
                SeatAvatarAtWork(wr, av);
            }
        }

        /// <summary>Assigned avatars while OnShift: Transform tracks host (visibility separate).</summary>
        void SyncMovingAssignedAvatars()
        {
            if (_crewPhase != CrewPhase.OnShift && _crewPhase != CrewPhase.HeadingOut) return;
            if (_crewWorkers == null) return;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null) continue;
                if (wr.CampBody != null
                    && (wr.CampBody.InjuryReturnActive || wr.CampBody.ToiletTripActive
                        || wr.CampBody.RescueDutyActive || wr.CampBody.BeingRescued
                        || wr.CampBody.RescueReturning || wr.CampBody.SeekingStewardCare
                        || wr.CampBody.PriorityDutyActive))
                    continue;
                if (wr.State != null && wr.State.Incapacitated) continue;
                var L = _dayTracker.Get(wr.WorkerId);
                if (L == null || !L.ArrivedWork) continue;
                var asg = _assignments.GetAssignment(wr.WorkerId);
                if (asg == null || asg.JobType == JobType.Unassigned) continue;
                var av = _presence.Get(wr.WorkerId);
                if (av == null) continue;
                av.SetPresencePosition(GetProviderOperatePoint(asg.JobType));
                av.SetFollowing(asg.ProviderId);
                // Keep cabin vs person presentation stable while host moves
                if (PersistentWorkerBody.IsMachineCabinJob(asg.JobType))
                {
                    if (!av.IsVisuallyHidden) av.Hide();
                }
                else if (av.IsVisuallyHidden)
                    av.Show();
            }
        }

        void StampLiveProviderAssignments()
        {
            foreach (var job in JobStatPreview.OccupiedJobs)
            {
                int wid = _assignments.GetWorkerIdForJob(job);
                if (wid <= 0) continue;
                var wr = FindCrewWorker(wid);
                if (wr == null) continue;
                string pid = ResolveProviderId(job);
                if (string.IsNullOrEmpty(pid)) continue;
                _assignments.AssignInternal(wr.WorkerId, job, pid, _absoluteGameHours);
            }
        }

        void SyncProspectingFindingsAuthor()
        {
            var wr = _prospector != null ? _prospector.AssignedWorker : null;
            _scanHistory?.Findings?.SetAuthor(wr);
        }

        IWorkProvider GetProvider(JobType job) => job switch
        {
            JobType.Prospecting => (IWorkProvider)_fieldScanner
                ?? null, // scanner may be null — prospecting body still works via BehaviourKey
            JobType.Excavation => _worker,
            JobType.Hauling => _hauler,
            JobType.Refining => _refiner,
            JobType.Engineering => _engineer,
            JobType.Steward => _steward,
            _ => null,
        };

        string ResolveProviderId(JobType job)
        {
            var p = GetProvider(job);
            if (p != null && !string.IsNullOrEmpty(p.ProviderId))
                return p.ProviderId;
            if (job == JobType.Prospecting && _fieldScanner != null)
                return _fieldScanner.ProviderId;
            return JobStatPreview.BehaviourKey(job);
        }

        WorkerRuntime GetBodyAssignedWorker(JobType job) => job switch
        {
            JobType.Prospecting => _prospector != null ? _prospector.AssignedWorker : null,
            JobType.Excavation => _worker != null ? _worker.AssignedWorker : null,
            JobType.Hauling => _hauler != null ? _hauler.AssignedWorker : null,
            JobType.Refining => _refiner != null ? _refiner.AssignedWorker : null,
            JobType.Engineering => _engineer != null ? _engineer.AssignedWorker : null,
            JobType.Steward => _steward != null ? _steward.AssignedWorker : null,
            _ => null,
        };

        // ——— F1: selected WorkerId + control resolver (gameplay routing) ———

        WorkerRuntime FindCrewWorker(int workerId)
        {
            if (workerId <= 0 || _crewWorkers == null) return null;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var w = _crewWorkers[i];
                if (w != null && w.WorkerId == workerId) return w;
            }
            return null;
        }

        /// <summary>F3 canonical selection. Does not change when assignment changes.</summary>
        void SelectPersonById(int workerId)
        {
            if (workerId <= 0 || FindCrewWorker(workerId) == null)
            {
                _selectedWorkerId = 0;
                return;
            }
            _selectedWorkerId = workerId;
            // F3: open sheet follows newly selected person (does not auto-open)
            if (_openStatsWorkerId > 0)
                _openStatsWorkerId = workerId;
        }

        void SelectPerson(WorkerRuntime wr)
        {
            if (wr == null) return;
            SelectPersonById(wr.WorkerId);
        }

        void ToggleStatsSheetForWorker(int workerId)
        {
            if (FindCrewWorker(workerId) == null) return;
            if (_openStatsWorkerId == workerId)
            {
                _openStatsWorkerId = 0;
                return;
            }
            _openStatsWorkerId = workerId;
            if (_selectedWorkerId != workerId)
                SelectPersonById(workerId);
        }

        void ToggleStatsSheetForSelected()
        {
            if (_selectedWorkerId <= 0) return;
            ToggleStatsSheetForWorker(_selectedWorkerId);
        }

        // SyncLegacyControlDeferredFromSelection removed in F5 — banter no longer uses _control.

        /// <summary>
        /// Maps job → legacy profile-recipe slot for <see cref="WorkerStatProfiles.Build"/> only.
        /// Not used for ST sheet targeting after F3.
        /// </summary>
        static int JobToLegacyRoleIndex(JobType job) => job switch
        {
            JobType.Prospecting => 0,
            JobType.Excavation => 1,
            JobType.Hauling => 2,
            JobType.Refining => 3,
            JobType.Engineering => 4,
            JobType.Steward => 5,
            _ => -1,
        };

        /// <summary>
        /// Banter speech label only. Unassigned/camp/commute → Unassigned;
        /// never maps into excavation demand, injury, or work restrictions.
        /// </summary>
        static WorkerBanter.JobContext JobContextFrom(JobType job) => job switch
        {
            JobType.Prospecting => WorkerBanter.JobContext.Prospecting,
            JobType.Excavation => WorkerBanter.JobContext.Excavation,
            JobType.Hauling => WorkerBanter.JobContext.Hauling,
            JobType.Refining => WorkerBanter.JobContext.Refining,
            JobType.Engineering => WorkerBanter.JobContext.Engineering,
            JobType.Steward => WorkerBanter.JobContext.Steward,
            _ => WorkerBanter.JobContext.Unassigned,
        };

        /// <summary>
        /// F5: job-host event → AssignedWorker → WorkerId speech.
        /// Vacant host or off-shift/commute: no worker speech.
        /// Authorship freezes inside <see cref="WorkerBanter.TrySay"/>.
        /// </summary>
        bool TryAssignedBanter(JobType job, string sourceEvent, params string[] lines)
        {
            if (job == JobType.Unassigned) return false;
            return TryWorkerBanter(GetBodyAssignedWorker(job), job, sourceEvent, lines);
        }

        bool TryWorkerBanter(WorkerRuntime wr, JobType job, string sourceEvent, params string[] lines)
        {
            if (wr == null) return false;
            if (!CanPerformJobActions(wr)) return false;
            if (job == JobType.Unassigned) return false;
            return _banter.TrySay(
                wr.WorkerId,
                wr.DisplayName,
                JobContextFrom(job),
                sourceEvent,
                _absoluteGameHours,
                lines);
        }

        /// <summary>
        /// Findings / delayed attribution: use frozen WorkerId from the event, not current assignment.
        /// Still requires the author to exist in crew; does not require CanPerform (historical).
        /// </summary>
        bool TryAuthoredBanter(int workerId, string displayName, JobType jobContext,
            string sourceEvent, params string[] lines)
        {
            if (workerId <= 0) return false;
            var wr = FindCrewWorker(workerId);
            string name = wr != null ? wr.DisplayName : displayName;
            if (string.IsNullOrEmpty(name)) name = $"Worker {workerId}";
            if (jobContext == JobType.Unassigned) jobContext = JobType.Prospecting;
            return _banter.TrySay(
                workerId,
                name,
                JobContextFrom(jobContext),
                sourceEvent,
                _absoluteGameHours,
                lines);
        }

        static Color AccentForWorkerId(int id) => id switch
        {
            1 => UiCyan,
            2 => UiAmber,
            3 => UiGreen,
            4 => new Color(0.7f, 0.55f, 1f),
            5 => new Color(1f, 0.55f, 0.22f),
            _ => AccentForHiredId(id),
        };

        static Color AccentForHiredId(int id)
        {
            // Stable palette for recruitment WorkerIds (101+)
            int h = id * 397 ^ (id << 3);
            if (h < 0) h = -h;
            float hue = (h % 360) / 360f;
            return Color.HSVToRGB(hue, 0.55f, 0.95f);
        }

        string ProviderLabelForAssignment(WorkerAssignment asg)
        {
            if (asg == null || string.IsNullOrEmpty(asg.ProviderId)) return "—";
            if (asg.ProviderId.Contains(".")) return asg.ProviderId;
            return asg.ProviderDisplayLabel;
        }

        /// <summary>
        /// Canonical answer: which person is selected, and which host receives their job input.
        /// Off-shift / commute: physical target is WorkerAvatar even if assignment persists.
        /// </summary>
        WorkerControlTarget ResolveControlTarget()
        {
            var wr = FindCrewWorker(_selectedWorkerId);
            if (wr == null)
                return WorkerControlTarget.None;

            var asg = _assignments.GetAssignment(wr.WorkerId);
            var job = asg != null ? asg.JobType : JobType.Unassigned;
            string providerId = asg != null ? asg.ProviderId : "";
            var avatar = _presence.Get(wr.WorkerId);
            var phys = GetPhysicalState(wr);
            string host;
            if (phys != WorkerPhysicalState.Operating && phys != WorkerPhysicalState.Idle)
            {
                host = phys switch
                {
                    WorkerPhysicalState.CommutingHome => "WorkerAvatar (commuting home)",
                    WorkerPhysicalState.CommutingToWork => "WorkerAvatar (commuting to work)",
                    WorkerPhysicalState.Sleeping => "WorkerAvatar (sleeping)",
                    WorkerPhysicalState.AtCamp => "WorkerAvatar (camp evening)",
                    _ => "WorkerAvatar",
                };
            }
            else
            {
                host = job switch
                {
                    JobType.Prospecting => "ProspectorPerson",
                    JobType.Excavation => "FreeWorkerController",
                    JobType.Hauling => "HaulerPerson",
                    JobType.Refining => "RefinerPerson",
                    JobType.Engineering => "EngineerPerson",
                    JobType.Steward => "StewardPerson",
                    _ => "WorkerAvatar (idle)",
                };
            }
            return new WorkerControlTarget(wr, job, providerId, avatar, host);
        }

        WorkerPhysicalState GetPhysicalState(WorkerRuntime wr)
        {
            if (wr == null) return WorkerPhysicalState.Idle;
            if (!wr.IsAlive) return WorkerPhysicalState.Dead;
            if (wr.State != null && wr.State.Incapacitated)
                return WorkerPhysicalState.Incapacitated;
            if (PersonTaskAuthority(wr))
            {
                if (wr.CampBody != null && wr.CampBody.SeekingStewardCare)
                    return WorkerPhysicalState.AtCamp;
                return WorkerPhysicalState.Idle;
            }
            switch (_crewPhase)
            {
                case CrewPhase.HeadingHome: return WorkerPhysicalState.CommutingHome;
                case CrewPhase.CampEvening: return WorkerPhysicalState.AtCamp;
                case CrewPhase.Asleep: return WorkerPhysicalState.Sleeping;
                case CrewPhase.HeadingOut: return WorkerPhysicalState.CommutingToWork;
                default:
                {
                    var L = _dayTracker.Get(wr.WorkerId);
                    if (L != null && !L.ArrivedWork)
                        return WorkerPhysicalState.CommutingToWork;
                    var asg = _assignments.GetAssignment(wr.WorkerId);
                    if (asg != null && asg.JobType != JobType.Unassigned)
                        return WorkerPhysicalState.Operating;
                    return WorkerPhysicalState.Idle;
                }
            }
        }

        /// <summary>
        /// Person avatar is sole physical authority (not job host). Used by rescue / care / toilet.
        /// </summary>
        static bool PersonTaskAuthority(WorkerRuntime wr)
        {
            if (wr?.CampBody == null) return false;
            var b = wr.CampBody;
            return b.RescueDutyActive || b.BeingRescued || b.RescueReturning
                   || b.InjuryReturnActive || b.ToiletTripActive
                   || b.SeekingStewardCare || b.PriorityDutyActive;
        }

        /// <summary>Assignment alone is not enough — person must be OnShift (or morning arrive), Operating, and arrived at work.</summary>
        bool CanPerformJobActions(WorkerRuntime wr)
        {
            if (wr == null || !wr.IsAlive) return false;
            if (_crewPhase != CrewPhase.OnShift && _crewPhase != CrewPhase.HeadingOut) return false;
            if (wr.State != null && wr.State.Incapacitated) return false;
            if (wr.CampBody != null && wr.CampBody.ToiletTripActive) return false;
            if (wr.CampBody != null && wr.CampBody.RescueDutyActive) return false;
            if (wr.CampBody != null && wr.CampBody.BeingRescued) return false;
            if (wr.CampBody != null && wr.CampBody.RescueReturning) return false;
            if (wr.CampBody != null && wr.CampBody.PriorityDutyActive) return false;
            // Care commute blocks work. Stale SeekingStewardCare after NeedsCare cleared must not soft-lock jobs.
            if (wr.CampBody != null && wr.CampBody.InjuryReturnActive) return false;
            if (wr.CampBody != null && wr.CampBody.SeekingStewardCare
                && wr.State != null && wr.State.NeedsCare)
                return false;
            var status = InjuryResponse.EvaluateWorkStatus(wr);
            if (status == WorkerInjuryWorkStatus.OffDuty
                || status == WorkerInjuryWorkStatus.Incapacitated)
                return false;
            var L = _dayTracker.Get(wr.WorkerId);
            if (L == null || !L.ArrivedWork) return false;
            if (_crewPhase == CrewPhase.HeadingOut)
            {
                var asg = _assignments.GetAssignment(wr.WorkerId);
                return asg != null && asg.JobType != JobType.Unassigned;
            }
            return GetPhysicalState(wr) == WorkerPhysicalState.Operating;
        }

        bool CanPerformSelectedJobActions()
        {
            return CanPerformJobActions(FindCrewWorker(_selectedWorkerId));
        }

        /// <summary>
        /// Selected person is assigned to this job (UI / input routing).
        /// Does NOT require CanPerform — simulation Tick already gates that separately.
        /// Scanner Y/C and excavator paint keys must work when the assigned person is selected;
        /// placement/plan entry still enforce OnShift inside their Enter* methods.
        /// </summary>
        bool SelectedJobIs(JobType job)
        {
            var t = ResolveControlTarget();
            return t.IsAssigned && t.JobType == job;
        }

        Transform ResolvePhysicalFollowTransform(in WorkerControlTarget t)
        {
            if (!t.HasPerson) return null;
            var phys = GetPhysicalState(t.Worker);
            // Commute / sleep / idle: camera follows the person avatar
            if (phys != WorkerPhysicalState.Operating)
                return t.Avatar != null ? t.Avatar.transform : null;
            // Excavator cabin: follow the machine; other jobs: follow persistent person
            if (t.JobType == JobType.Excavation)
                return _worker != null ? _worker.transform : t.Avatar?.transform;
            return t.Avatar != null ? t.Avatar.transform : null;
        }

        /// <summary>Trivial idle WASD for unassigned selected person (no pathfinding).</summary>
        void TickIdleAvatarMovement(WorkerAvatar avatar, Vector2 wasd)
        {
            if (avatar == null || wasd.sqrMagnitude < 0.01f) return;
            var wr = FindCrewWorker(avatar.WorkerId);
            float speed = WorkerLocomotion.WalkSpeedAt(
                wr, _world, avatar.PresencePosition, AvatarCommuteRadius,
                roleBias: 0.85f, carriedLoad01: 0f, isMoving: true);
            Vector2 next = avatar.PresencePosition + wasd.normalized * (speed * Time.deltaTime);
            avatar.SetPresencePosition(next);
        }

        /// <summary>
        /// Stage E transactional assign: can-leave → can-enter → release → bind.
        /// Never leaves a worker on two providers.
        /// </summary>
        bool TryAssignJob(WorkerRuntime worker, JobType job, out string reason)
        {
            reason = "";
            if (worker == null)
            {
                reason = "No worker";
                return false;
            }
            if (!worker.IsAlive)
            {
                reason = "Worker is dead";
                return false;
            }
            if (job == JobType.Unassigned)
                return TryUnassignJob(GetAssignmentJob(worker), out reason);

            if (!HostExistsForJob(job))
            {
                reason = $"No host for {job}";
                return false;
            }

            // 2. Target can accept?
            if (job == JobType.Prospecting && _fieldScanner != null
                && !_fieldScanner.CanAssign(worker, out reason))
                return false;
            var target = GetProvider(job);
            if (target != null && !target.CanAssign(worker, out reason))
                return false;
            if (job == JobType.Prospecting && _fieldScanner == null && _prospector == null)
            {
                reason = "No Prospecting body";
                return false;
            }

            // Already here?
            var existing = _assignments.GetAssignment(worker.WorkerId);
            if (existing != null && existing.JobType == job
                && ReferenceEquals(GetBodyAssignedWorker(job), worker))
            {
                string pid = ResolveProviderId(job);
                _assignments.AssignInternal(worker.WorkerId, job, pid, _absoluteGameHours);
                NotifyProviderAssigned(job, worker);
                if (job == JobType.Prospecting) SyncProspectingFindingsAuthor();
                reason = "Already assigned";
                return true;
            }

            // 1. Can leave current provider safely?
            var currentJob = existing != null ? existing.JobType : JobType.Unassigned;
            if (currentJob != JobType.Unassigned && currentJob != job)
            {
                if (!CanReleaseFromJob(currentJob, out reason))
                    return false;
            }

            // Evict current occupant of target (they become Unassigned)
            var occupant = GetBodyAssignedWorker(job);
            if (occupant != null && !ReferenceEquals(occupant, worker))
            {
                YieldHost(job);
                ParkAvatarLeavingJob(occupant, job);
                ClearHost(job);
                _assignments.Unassign(occupant.WorkerId);
            }
            else
            {
                YieldHost(job);
            }

            // Release worker from previous job bodies (after leave check passed)
            if (currentJob != JobType.Unassigned && currentJob != job)
            {
                ParkAvatarLeavingJob(worker, currentJob);
                ReleaseWorkerBody(worker, currentJob);
            }

            string providerId = ResolveProviderId(job);
            if (!_assignments.AssignInternal(worker.WorkerId, job, providerId, _absoluteGameHours))
            {
                reason = "Assignment manager rejected";
                return false;
            }

            SyncBodyBindingsAfterAssign();
            BindHost(job, worker);
            NotifyProviderAssigned(job, worker);
            if (job == JobType.Prospecting)
            {
                SyncProspectingFindingsAuthor();
                if (_fieldScanner != null
                    && _fieldScanner.State == ProspectorScannerState.Packed
                    && _crewPhase == CrewPhase.OnShift)
                    _prospector.AssignScannerSetup(_fieldScanner);
            }

            RefreshAllAvatarPresence();

            Debug.Assert(_assignments.CountWorkersOnJob(job) == 1);
            Debug.Assert(!_assignments.HasDuplicateJobViolation(_crewWorkers));
            DigHoodLog.Push(
                $"ASSIGN | {job} → {worker.DisplayName} (id {worker.WorkerId}) @ {providerId}");
            reason = "OK";
            return true;
        }

        bool TryUnassignJob(JobType job, out string reason)
        {
            reason = "";
            if (job == JobType.Unassigned)
            {
                reason = "Nothing to unassign";
                return false;
            }
            if (!CanReleaseFromJob(job, out reason))
                return false;

            var wr = GetBodyAssignedWorker(job);
            YieldHost(job);
            if (wr != null)
                ParkAvatarLeavingJob(wr, job);
            ClearHost(job);
            if (wr != null)
                _assignments.Unassign(wr.WorkerId);
            if (job == JobType.Prospecting)
                SyncProspectingFindingsAuthor();

            RefreshAllAvatarPresence();

            DigHoodLog.Push($"ASSIGN | {job} → Unassigned");
            reason = "OK";
            return true;
        }

        JobType GetAssignmentJob(WorkerRuntime worker)
        {
            var a = worker != null ? _assignments.GetAssignment(worker.WorkerId) : null;
            return a != null ? a.JobType : JobType.Unassigned;
        }

        bool HostExistsForJob(JobType job) => job switch
        {
            JobType.Prospecting => _prospector != null,
            JobType.Excavation => _worker != null,
            JobType.Hauling => _hauler != null,
            JobType.Refining => _refiner != null,
            JobType.Engineering => _engineer != null,
            JobType.Steward => _steward != null,
            _ => false,
        };

        bool CanReleaseFromJob(JobType job, out string reason)
        {
            reason = "OK";
            if (job == JobType.Prospecting
                && _fieldScanner != null
                && _fieldScanner.State == ProspectorScannerState.Scanning)
            {
                reason = "Scan in progress — finish first";
                return false;
            }
            return true;
        }

        void YieldHost(JobType job)
        {
            switch (job)
            {
                case JobType.Prospecting: _prospector?.YieldForReassignment(); break;
                case JobType.Excavation: _worker?.YieldForReassignment(); break;
                case JobType.Hauling: _hauler?.YieldForReassignment(); break;
                case JobType.Refining: _refiner?.YieldForReassignment(); break;
                case JobType.Engineering: _engineer?.YieldForReassignment(); break;
                case JobType.Steward: _steward?.YieldForReassignment(); break;
            }
        }

        void ClearHost(JobType job)
        {
            switch (job)
            {
                case JobType.Prospecting:
                    _prospector?.ClearWorker();
                    _fieldScanner?.NotifyUnassigned();
                    break;
                case JobType.Excavation:
                    _worker?.ClearWorker();
                    _worker?.NotifyUnassigned();
                    break;
                case JobType.Hauling:
                    _hauler?.ClearWorker();
                    _hauler?.NotifyUnassigned();
                    break;
                case JobType.Refining:
                    _refiner?.ClearWorker();
                    _refiner?.NotifyUnassigned();
                    break;
                case JobType.Engineering:
                    _engineer?.ClearWorker();
                    _engineer?.NotifyUnassigned();
                    break;
                case JobType.Steward:
                    _steward?.ClearWorker();
                    _steward?.NotifyUnassigned();
                    break;
            }
        }

        void BindHost(JobType job, WorkerRuntime worker)
        {
            switch (job)
            {
                case JobType.Prospecting: _prospector?.BindWorker(worker); break;
                case JobType.Excavation: _worker?.BindWorker(worker); break;
                case JobType.Hauling: _hauler?.BindWorker(worker); break;
                case JobType.Refining: _refiner?.BindWorker(worker); break;
                case JobType.Engineering: _engineer?.BindWorker(worker); break;
                case JobType.Steward: _steward?.BindWorker(worker); break;
            }
        }

        void NotifyProviderAssigned(JobType job, WorkerRuntime worker)
        {
            switch (job)
            {
                case JobType.Prospecting: _fieldScanner?.NotifyAssigned(worker); break;
                case JobType.Excavation: _worker?.NotifyAssigned(worker); break;
                case JobType.Hauling: _hauler?.NotifyAssigned(worker); break;
                case JobType.Refining: _refiner?.NotifyAssigned(worker); break;
                case JobType.Engineering: _engineer?.NotifyAssigned(worker); break;
                case JobType.Steward: _steward?.NotifyAssigned(worker); break;
            }
        }

        void ReleaseWorkerBody(WorkerRuntime worker, JobType job)
        {
            if (worker == null) return;
            if (!ReferenceEquals(GetBodyAssignedWorker(job), worker)) return;
            YieldHost(job);
            ClearHost(job);
        }

        /// <summary>
        /// Clear leftover body binds so assignment manager and hosts agree (no dual ownership).
        /// </summary>
        void SyncBodyBindingsAfterAssign()
        {
            if (_crewWorkers == null) return;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var w = _crewWorkers[i];
                if (w == null) continue;
                var asg = _assignments.GetAssignment(w.WorkerId);
                JobType should = asg != null ? asg.JobType : JobType.Unassigned;

                void DetachIfWrong(JobType hostJob, System.Func<WorkerRuntime> get, System.Action clear)
                {
                    var bound = get();
                    if (bound != null && ReferenceEquals(bound, w) && should != hostJob)
                    {
                        YieldHost(hostJob);
                        clear();
                    }
                }

                DetachIfWrong(JobType.Prospecting,
                    () => _prospector != null ? _prospector.AssignedWorker : null,
                    () =>
                    {
                        _prospector?.ClearWorker();
                        if (_fieldScanner != null && _fieldScanner.AssignedWorkerId == w.WorkerId)
                            _fieldScanner.NotifyUnassigned();
                    });
                DetachIfWrong(JobType.Excavation,
                    () => _worker != null ? _worker.AssignedWorker : null,
                    () => { _worker?.ClearWorker(); _worker?.NotifyUnassigned(); });
                DetachIfWrong(JobType.Hauling,
                    () => _hauler != null ? _hauler.AssignedWorker : null,
                    () => { _hauler?.ClearWorker(); _hauler?.NotifyUnassigned(); });
                DetachIfWrong(JobType.Refining,
                    () => _refiner != null ? _refiner.AssignedWorker : null,
                    () => { _refiner?.ClearWorker(); _refiner?.NotifyUnassigned(); });
                DetachIfWrong(JobType.Engineering,
                    () => _engineer != null ? _engineer.AssignedWorker : null,
                    () => { _engineer?.ClearWorker(); _engineer?.NotifyUnassigned(); });
                DetachIfWrong(JobType.Steward,
                    () => _steward != null ? _steward.AssignedWorker : null,
                    () => { _steward?.ClearWorker(); _steward?.NotifyUnassigned(); });
            }
        }

        // Thin wrappers — DEV UI / call sites
        bool TryAssignProspecting(WorkerRuntime w, out string reason) =>
            TryAssignJob(w, JobType.Prospecting, out reason);
        bool TryUnassignProspecting(out string reason) =>
            TryUnassignJob(JobType.Prospecting, out reason);
        bool TryAssignExcavation(WorkerRuntime w, out string reason) =>
            TryAssignJob(w, JobType.Excavation, out reason);
        bool TryUnassignExcavation(out string reason) =>
            TryUnassignJob(JobType.Excavation, out reason);
        bool TryAssignHauling(WorkerRuntime w, out string reason) =>
            TryAssignJob(w, JobType.Hauling, out reason);
        bool TryUnassignHauling(out string reason) =>
            TryUnassignJob(JobType.Hauling, out reason);
        bool TryAssignRefining(WorkerRuntime w, out string reason) =>
            TryAssignJob(w, JobType.Refining, out reason);
        bool TryUnassignRefining(out string reason) =>
            TryUnassignJob(JobType.Refining, out reason);
        bool TryAssignEngineering(WorkerRuntime w, out string reason) =>
            TryAssignJob(w, JobType.Engineering, out reason);
        bool TryUnassignEngineering(out string reason) =>
            TryUnassignJob(JobType.Engineering, out reason);

        bool IsJobAssignBlocked(WorkerRuntime worker, JobType targetJob, out string reason)
        {
            reason = "";
            if (worker == null) { reason = "No worker"; return true; }
            if (targetJob == JobType.Prospecting && _fieldScanner != null
                && !_fieldScanner.CanAssign(worker, out reason))
                return true;
            var asg = _assignments.GetAssignment(worker.WorkerId);
            if (asg != null && asg.JobType != JobType.Unassigned && asg.JobType != targetJob
                && !CanReleaseFromJob(asg.JobType, out reason))
                return true;
            return false;
        }

        /// <summary>Stage B: assignment JobType matches Stage A body binds (provider may be live id).</summary>
        void AssertStageBBodyJobMapping()
        {
            void Check(WorkerRuntime wr, JobType job, params string[] allowedProviders)
            {
                if (wr == null) return;
                var a = _assignments.GetAssignment(wr.WorkerId);
                Debug.Assert(a != null && a.JobType == job, $"{wr.WorkerId} job mismatch");
                bool ok = false;
                for (int i = 0; i < allowedProviders.Length; i++)
                {
                    string allow = allowedProviders[i];
                    if (a.ProviderId == allow) { ok = true; break; }
                    if (allow.EndsWith(".*")
                        && a.ProviderId.StartsWith(allow.Substring(0, allow.Length - 1)))
                    { ok = true; break; }
                }
                Debug.Assert(ok, $"{wr.WorkerId} provider {a.ProviderId} not allowed");
            }

            Check(_workerLewis, JobType.Prospecting, "body.prospector", "scanner.*");
            Check(_workerMara, JobType.Excavation, "body.excavator", "excavator.*");
            Check(_workerKowalski, JobType.Hauling, "body.hauler", "hauler.*");
            Check(_workerElena, JobType.Refining, "body.refiner", "washer.*");
            Check(_workerViktor, JobType.Engineering, "body.engineer", "engineer.*");
            Check(_workerSteward, JobType.Steward, "body.steward", "steward.*");
        }

        void OnDigImpact(TerrainCell before, bool broke)
        {
            if (before.DiamondCount > 0 && broke)
            {
                TryAssignedBanter(JobType.Excavation, "DiamondBroke",
                    "Diamonds! Catch that shimmer — haul it!",
                    "Ice in the rock. Real stones!",
                    "Crystal flash — don't lose that pile.",
                    "Hard glitter. That's a diamond pocket.");
                return;
            }
            if (before.GoldCount > 0 && broke)
            {
                TryAssignedBanter(JobType.Excavation, "GoldBroke",
                    "Paydirt! That's the real stuff.",
                    "Gold in the teeth of the bit — haul it!",
                    "There she is. Yellow as sin.",
                    "Struck a pocket. Don't blink.");
                return;
            }
            if (before.BedrockCount >= 2)
            {
                if (broke)
                    TryAssignedBanter(JobType.Excavation, "BedrockBroke",
                        "Finally chewed through that plate.",
                        "Bedrock's done. Took its sweet time.",
                        "Hard stuff cracked. Moving on.");
                else
                    TryAssignedBanter(JobType.Excavation, "BedrockHit",
                        "Solid plate. This'll eat the morning.",
                        "Bedrock. Bit's complainin' already.",
                        "Hard face — find a seam or grind.",
                        "Feels like diggin' a bank vault.");
            }
        }

        void OnBroke(int x, int y)
        {
            var c = _world.Get(x, y);
            int g = c.GoldCount;
            if (g > 0)
            {
                _goldFoundCells++;
                _goldFoundSockets += g;
            }
            Vector2 tip = _worker.DrillTip(0.4f);
            Vector2 face = Vector2.Lerp(_world.CellCenter(x, y), tip, 0.75f);
            tip = Vector2.Lerp(tip, face, 0.5f);
            // One loose pile per cell — 2–4 floor chips, same sockets/value as dug wall
            LoosePile.SpawnCellFromDrill(_looseRoot, tip, _worker.Facing, c, _world.CellSize, WorkerRadius, x, y);
            // Quiet lingering dust in the opened cell — subtle, fades slowly
            ExcavationDustFx.SpawnLingering(_looseRoot, _world.CellCenter(x, y), _world.CellSize,
                heavy: true);
            if (Random.value < 0.75f)
                ExcavationDustFx.SpawnLingering(_looseRoot,
                    tip + Random.insideUnitCircle * (_world.CellSize * 0.55f),
                    _world.CellSize * 0.95f, heavy: false);
            NoteExcavationDepth(y);
        }

        void NoteExcavationDepth(int cellY)
        {
            if (cellY < _deepestExcavatedY)
                _deepestExcavatedY = cellY;
        }

        /// <summary>Meters below entry (StartY). Positive when digging down.</summary>
        float CurrentDepthMeters
        {
            get
            {
                if (_world == null) return 0f;
                int tipY = StartY;
                if (_worker != null)
                {
                    var tip = _worker.DrillTip(0.35f);
                    tipY = _world.WorldToCell(tip).y;
                }
                int deepest = _deepestExcavatedY == int.MaxValue
                    ? tipY
                    : Mathf.Min(_deepestExcavatedY, tipY);
                int cellsDown = Mathf.Max(0, StartY - deepest);
                return cellsDown * _world.CellSize;
            }
        }

        void TryPlaceLantern()
        {
            if (_lanternCount >= 16) return;
            Vector2 pos = _worker.Position;
            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse != null && cam != null)
            {
                Vector2 screen = mouse.position.ReadValue();
                Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
                var cell = _world.WorldToCell(world);
                if (_world.InBounds(cell.x, cell.y) && _world.IsExcavated(cell.x, cell.y))
                    pos = world;
            }
            var at = _world.WorldToCell(pos);
            if (!_world.InBounds(at.x, at.y) || !_world.IsExcavated(at.x, at.y))
                pos = _worker.Position;

            DigVisualKit.PlaceLantern(_lanternRoot, pos, local: true, intensity: 2.6f);
            _mineInfra?.RegisterLantern(pos);
            _lanternCount++;
        }

        void Update()
        {
            if (_worker == null) return;
            EnsureMineAmbience();
            TickClock();
            TickCrewPhase();
            TickTunnelCollapse();
            TickPrioritySystem();

            var kb = Keyboard.current;
            Vector2 wasd = Vector2.zero;
            bool scanPulse = false;
            if (kb != null)
            {
                if (kb.wKey.isPressed) wasd.y += 1f;
                if (kb.sKey.isPressed) wasd.y -= 1f;
                if (kb.aKey.isPressed) wasd.x -= 1f;
                if (kb.dKey.isPressed) wasd.x += 1f;
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    if (_scannerPlanMode) ExitScannerPlanMode();
                    else if (_scannerPlaceMode) ExitScannerPlacementMode();
                    else if (SelectedJobIs(JobType.Excavation))
                        _worker.ClearRoute();
                }
                if (kb.backspaceKey.wasPressedThisFrame && SelectedJobIs(JobType.Excavation))
                    _worker.UndoLastPin();
                if (SelectedJobIs(JobType.Excavation) &&
                    (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                    _worker.AddPin(_worker.Position, replaceRoute: false);
                // Tunnel width 1–5 while excavator route planning is active
                if (SelectedJobIs(JobType.Excavation) && _worker != null)
                {
                    if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
                        _worker.SetPlannedTunnelWidth(1);
                    if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
                        _worker.SetPlannedTunnelWidth(2);
                    if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
                        _worker.SetPlannedTunnelWidth(3);
                    if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame)
                        _worker.SetPlannedTunnelWidth(4);
                    if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame)
                        _worker.SetPlannedTunnelWidth(5);
                }
                if (kb.rKey.wasPressedThisFrame) ResetMap();
                if (kb.semicolonKey.wasPressedThisFrame)
                    ToggleDevMode(); // Ø on Nordic layouts (physical Semicolon / Ø key)
                if (kb.bKey.wasPressedThisFrame && DevMode.Enabled)
                {
                    _showBalanceHarness = !_showBalanceHarness;
                    _hudPopup = _showBalanceHarness ? HudPopupKind.Balance : HudPopupKind.None;
                }
                if (kb.lKey.wasPressedThisFrame) TryPlaceLantern();
                if (kb.tKey.wasPressedThisFrame && DevMode.Enabled) _tactical?.Toggle();
                if (kb.tabKey.wasPressedThisFrame) CycleSelectedPerson(+1);
                if (kb.iKey.wasPressedThisFrame) ToggleStatsSheetForSelected();
                if (kb.uKey.wasPressedThisFrame) _playerTactical?.Toggle();
                if (kb.hKey.wasPressedThisFrame
                    && _playerTactical != null && _playerTactical.Visible)
                    _scanHistoryBrowserOpen = !_scanHistoryBrowserOpen;
                if (kb.nKey.wasPressedThisFrame) SkipSleep();
                if (kb.yKey.wasPressedThisFrame && SelectedJobIs(JobType.Prospecting))
                {
                    if (_scannerPlanMode) ExitScannerPlanMode();
                    ToggleScannerPlacementMode();
                }
                if (kb.cKey.wasPressedThisFrame && SelectedJobIs(JobType.Prospecting))
                    ToggleScannerPlanMode();
                if (kb.equalsKey.wasPressedThisFrame && DevMode.Enabled
                    && SelectedJobIs(JobType.Prospecting))
                {
                    if (_fieldScanner != null && _fieldScanner.State == ProspectorScannerState.Scanning)
                        _fieldScanner.DebugForceCompleteScan(_absoluteGameHours);
                    else
                        _prospector?.DebugForceFinishScannerSetup();
                }
                if (_prospector != null && SelectedJobIs(JobType.Prospecting))
                {
                    bool boost = DevMode.Enabled
                        && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);
                    if (_prospector.IsSettingUpScanner)
                        _prospector.SetDebugSetupSpeedMul(boost ? 20f : 1f);
                    if (_fieldScanner != null && _fieldScanner.State == ProspectorScannerState.Scanning)
                        _prospector.SetDebugScanSpeedMul(boost ? 120f : 12f);
                    else
                        _prospector.SetDebugScanSpeedMul(1f);
                }
                if (SelectedJobIs(JobType.Prospecting) && kb.digit1Key.wasPressedThisFrame && !_scannerPlaceMode)
                {
                    _prospector?.SetDistance(ScanDistance.Short);
                    if (_scannerPlanMode) ApplyPlanPresetsFromProspector();
                }
                if (SelectedJobIs(JobType.Prospecting) && kb.digit2Key.wasPressedThisFrame && !_scannerPlaceMode)
                {
                    _prospector?.SetDistance(ScanDistance.Medium);
                    if (_scannerPlanMode) ApplyPlanPresetsFromProspector();
                }
                if (SelectedJobIs(JobType.Prospecting) && kb.digit3Key.wasPressedThisFrame && !_scannerPlaceMode)
                {
                    _prospector?.SetDistance(ScanDistance.Long);
                    if (_scannerPlanMode) ApplyPlanPresetsFromProspector();
                }
                if (kb.qKey.wasPressedThisFrame)
                {
                    if (_scannerPlaceMode && SelectedJobIs(JobType.Prospecting))
                        RotateScannerPlacement(-15f);
                    else if (SelectedJobIs(JobType.Prospecting))
                    {
                        _prospector?.SetWidth(ScanWidth.Narrow);
                        if (_scannerPlanMode) ApplyPlanPresetsFromProspector();
                    }
                }
                if (kb.eKey.wasPressedThisFrame)
                {
                    if (_scannerPlaceMode && SelectedJobIs(JobType.Prospecting))
                        RotateScannerPlacement(+15f);
                    else if (SelectedJobIs(JobType.Prospecting))
                    {
                        _prospector?.SetWidth(ScanWidth.Wide);
                        if (_scannerPlanMode) ApplyPlanPresetsFromProspector();
                    }
                }
                if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                    && _scannerPlanMode && SelectedJobIs(JobType.Prospecting))
                    ConfirmScannerPlan();
                if (SelectedJobIs(JobType.Prospecting) && _prospector != null && _crewPhase == CrewPhase.OnShift
                    && !_scannerPlaceMode && !_scannerPlanMode)
                {
                    if (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame)
                        _prospector.SetRadar(false);
                    else if (kb.spaceKey.wasPressedThisFrame && !_prospector.HasScannerAssignment)
                    {
                        if (!_prospector.RadarOn)
                            _prospector.SetRadar(true);
                    }
                }
                if (kb.gKey.wasPressedThisFrame && SelectedJobIs(JobType.Hauling))
                    _hauler?.TogglePreferGold();
                if (kb.gKey.wasPressedThisFrame && SelectedJobIs(JobType.Refining))
                    _refiner?.TogglePriority();
            }

            // F1: tick ALL job hosts from assigned-worker eligibility — never from UI selection.
            // WASD / player job input only routes to the selected person's current job.
            // HeadingOut: people who have ArrivedWork may already operate (others still walking)
            if (_crewPhase == CrewPhase.OnShift || _crewPhase == CrewPhase.HeadingOut)
            {
                var ctl = ResolveControlTarget();
                // Player drive input only — excavator keeps Tick(zero) for autonomous paint dig
                Vector2 digWasd = CanPerformJobActions(ctl.Worker) && ctl.JobType == JobType.Excavation
                    ? wasd : Vector2.zero;
                Vector2 prosWasd = CanPerformJobActions(ctl.Worker) && ctl.JobType == JobType.Prospecting
                    ? wasd : Vector2.zero;
                Vector2 refWasd = CanPerformJobActions(ctl.Worker) && ctl.JobType == JobType.Refining
                    ? wasd : Vector2.zero;
                bool prosPulse = ctl.JobType == JobType.Prospecting && scanPulse
                    && CanPerformJobActions(ctl.Worker);

                _prospector?.SetHudVisible(ctl.JobType == JobType.Prospecting
                    && CanPerformJobActions(ctl.Worker));
                bool ToiletBusy(WorkerRuntime wr) =>
                    wr?.CampBody != null && wr.CampBody.ToiletTripActive;
                // Excavation simulation: AssignedWorker + CanPerform + priority authority
                if (!ToiletBusy(_worker?.AssignedWorker)
                    && CanPerformJobActions(_worker?.AssignedWorker)
                    && PriorityAllowsHostTick(_worker?.AssignedWorker, JobType.Excavation))
                    _worker.Tick(digWasd);
                else if (_worker != null)
                    _worker.TickPassiveOnly();
                if (!ToiletBusy(_prospector?.AssignedWorker)
                    && CanPerformJobActions(_prospector?.AssignedWorker)
                    && PriorityAllowsHostTick(_prospector?.AssignedWorker, JobType.Prospecting))
                    _prospector?.Tick(prosWasd, prosPulse);
                if (!ToiletBusy(_refiner?.AssignedWorker)
                    && CanPerformJobActions(_refiner?.AssignedWorker)
                    && PriorityAllowsHostTick(_refiner?.AssignedWorker, JobType.Refining))
                    _refiner?.Tick(refWasd);

                if (ctl.IsIdlePerson && GetPhysicalState(ctl.Worker) == WorkerPhysicalState.Idle
                    && !ToiletBusy(ctl.Worker))
                    TickIdleAvatarMovement(ctl.Avatar, wasd);

                if (!ToiletBusy(_hauler?.AssignedWorker)
                    && CanPerformJobActions(_hauler?.AssignedWorker)
                    && PriorityAllowsHostTick(_hauler?.AssignedWorker, JobType.Hauling))
                    _hauler?.Tick();
                float hoursNow = Time.deltaTime / SecondsPerGameHour;
                _hauler?.SetGameHours(_absoluteGameHours);
                if (!ToiletBusy(_engineer?.AssignedWorker)
                    && CanPerformJobActions(_engineer?.AssignedWorker)
                    && PriorityAllowsHostTick(_engineer?.AssignedWorker, JobType.Engineering))
                    _engineer?.Tick(hoursNow, _absoluteGameHours);
                if (!ToiletBusy(_steward?.AssignedWorker)
                    && CanPerformJobActions(_steward?.AssignedWorker)
                    && PriorityAllowsHostTick(_steward?.AssignedWorker, JobType.Steward))
                    _steward?.Tick(hoursNow, onShift: _crewPhase == CrewPhase.OnShift
                        || _crewPhase == CrewPhase.HeadingOut);
                if (_engineer != null)
                {
                    if (_engineer.IsEnRoute && !_engineerWasEnRoute)
                        TryAssignedBanter(JobType.Engineering, "EngineerEnRoute",
                            "She's redlined. I'm moving.",
                            "Heat spike — engineer en route.",
                            "Don't touch the bit. I've got it.");
                    if (_engineer.IsRepairing && !_engineerWasRepairing)
                        TryAssignedBanter(JobType.Engineering, "EngineerRepairing",
                            "Bit's glowing. Hang on — I'll clear the jam.",
                            "Overheat lock. Coolant and wrench, coming in.",
                            "Stand by. I'm on the drill.");
                    _engineerWasEnRoute = _engineer.IsEnRoute;
                    _engineerWasRepairing = _engineer.IsRepairing;
                }
            }
            else
            {
                _prospector?.SetHudVisible(false);
                // Washer can finish mid-walk-home
                _yard?.Washer?.Tick();
            }

            DiscoverGasNearCrew();
            UpdateStockpileHover();
            SyncRoutePinsToScanView();
            _balance.Tick(Time.deltaTime);
            // F1: leave scanner modes if selected person is no longer Prospecting
            if ((_scannerPlaceMode || _scannerPlanMode) && !SelectedJobIs(JobType.Prospecting))
            {
                ExitScannerPlanMode();
                ExitScannerPlacementMode();
            }
            UpdateScannerPlacementGhost();
            FollowCamera();
        }

        void TickClock()
        {
            float prev = _gameHour;
            float hoursDelta = Time.deltaTime / SecondsPerGameHour;
            _lastSocialGameHoursDelta = hoursDelta;
            _gameHour += hoursDelta;
            _absoluteGameHours += hoursDelta;
            WorkerStateClock.GameHours = _absoluteGameHours;
            if (_gameHour >= 24f)
            {
                _gameHour -= 24f;
                _dayIndex++;
                _coopWorkPlaytest.EnsureDay(_dayIndex);
                EarlyCrewPressure.NotifyWorkingDayAdvanced();
            }
            ApplyDayNightLight();

            // V1.2B: conservative daytime meter drift while OnShift (not job demand)
            if (_crewPhase == CrewPhase.OnShift && _crewWorkers != null)
            {
                for (int i = 0; i < _crewWorkers.Length; i++)
                    WorkerStateDaytimeRecovery.Tick(_crewWorkers[i], hoursDelta);
                TickJobDemands(hoursDelta);
                TickProspectorDrySpell(hoursDelta);
                TickClaustrophobia(hoursDelta);
            }

            TickInjuryRecovery(hoursDelta, sleeping: _crewPhase == CrewPhase.Asleep);
            TickInjuryCareReturns();
            TickCampLifeBody(hoursDelta);
            TickToiletTrips(hoursDelta);
            TickCrewDayAccrual(hoursDelta);
            _managerIntents.Tick(_absoluteGameHours);
            PresentPendingAccidents();

            _scanHistory?.Findings.SetGameHours(_absoluteGameHours);

            _logisticsTraffic?.SetGameHours(_absoluteGameHours);
            _logisticsTraffic?.TickDecay(hoursDelta);
            _hauler?.SetGameHours(_absoluteGameHours);
            _mineInfra?.SetGameHours(_absoluteGameHours);
            _collapse?.SetGameHours(_absoluteGameHours);

            // Worker-required prospector labour only while OnShift (assignment may persist overnight)
            if (_crewPhase == CrewPhase.OnShift)
            {
                _prospector?.TickScannerGameTime(hoursDelta);
                _prospector?.TickInvestigationGameTime(hoursDelta, _absoluteGameHours);
            }

            // Equipment scan may finish overnight (like washer) — no new worker setup
            if (_fieldScanner != null && _fieldScanner.State == ProspectorScannerState.Scanning)
            {
                float mul = _prospector != null ? _prospector.DebugScanSpeedMul : 1f;
                _fieldScanner.TickScanGameHours(hoursDelta * mul, _absoluteGameHours);
            }

            // Desk analysis requires OnShift presence
            if (_crewPhase == CrewPhase.OnShift)
            {
                bool deskActive = _prospector == null
                    || _prospector.WorkMode != ProspectorWorkMode.Investigate
                    || _prospector.Investigation.DeskAnalysisActive;
                if (deskActive)
                {
                    float mul = 1f;
                    var kb = Keyboard.current;
                    if (kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed)
                        && _scanHistory != null && _scanHistory.Analyst.HasWork)
                        mul = 8f;
                    float excavDist = -1f;
                    var cur = _scanHistory?.Analyst?.Current;
                    if (cur != null && _prospector != null)
                        excavDist = _prospector.Investigation.ExcavatorDistanceCells(cur);
                    _scanHistory?.TickAnalysisGameHours(hoursDelta * mul, _absoluteGameHours, excavDist);
                }
            }

            // Phase transitions driven by clock edges
            if (_crewPhase == CrewPhase.OnShift && prev < ShiftEndHour && _gameHour >= ShiftEndHour)
                BeginHeadingHome();
            if (_crewPhase == CrewPhase.Asleep && prev < ShiftStartHour && _gameHour >= ShiftStartHour)
            {
                // Do not invent remaining recovery — short nights stay short.
                BeginHeadingOut(announce: true);
            }

            // Sleep recovery: person-level for entire crew (not Excavator host)
            if (_crewPhase == CrewPhase.Asleep)
                TickCrewSleepRecovery(Time.deltaTime);

            int refinedGold = _calc != null ? _calc.RefinedGold : 0;
            int refinedDia = _calc != null ? _calc.RefinedDiamond : 0;
            _mission01.Sync(_dayIndex, refinedGold, refinedDia, _gameHour, prev, ShiftEndHour);
        }

        void ToggleScannerPlacementMode()
        {
            if (_scannerPlaceMode) ExitScannerPlacementMode();
            else EnterScannerPlacementMode();
        }

        void EnterScannerPlacementMode()
        {
            if (_world == null || _prospector == null || _worldRoot == null) return;
            if (_crewPhase != CrewPhase.OnShift)
            {
                DigHoodLog.Push("SCANNER | Placement only during shift");
                return;
            }
            if (_fieldScanner != null && _fieldScanner.State == ProspectorScannerState.Scanning)
            {
                DigHoodLog.Push("SCANNER | Cannot redeploy while SCANNING");
                return;
            }

            ExitScannerPlanMode();
            _scannerPlaceMode = true;
            _prospector.SetRadar(false);
            _prospector.SetWorkMode(ProspectorWorkMode.Manual);
            if (_scannerGhost == null)
            {
                _scannerGhost = ProspectorScannerEquipment.Spawn(
                    _worldRoot, _world, _prospector.Position, _scannerPlaceFacing);
            }
            _scannerGhost.ShowPlanningPreview(false);
            DigHoodLog.Push("SCANNER | PLACEMENT MODE — LMB confirm · Q/E rotate · Esc cancel");
            Debug.Log("[SCANNER] Placement mode ON — LMB confirm, Q/E rotate, Esc cancel");
        }

        void ExitScannerPlacementMode()
        {
            _scannerPlaceMode = false;
            _scannerPlaceFail = "";
            if (_scannerGhost != null)
            {
                Destroy(_scannerGhost.gameObject);
                _scannerGhost = null;
            }
        }

        void ToggleScannerPlanMode()
        {
            if (_scannerPlanMode) ExitScannerPlanMode();
            else EnterScannerPlanMode();
        }

        void EnterScannerPlanMode()
        {
            if (_fieldScanner == null || _prospector == null) return;
            if (_fieldScanner.State != ProspectorScannerState.Ready)
            {
                DigHoodLog.Push($"SCAN | Plan needs READY (now {_fieldScanner.StateLabel})");
                return;
            }
            if (_crewPhase != CrewPhase.OnShift)
            {
                DigHoodLog.Push("SCAN | Planning only during shift");
                return;
            }

            ExitScannerPlacementMode();
            _scannerPlanMode = true;
            _prospector.SetRadar(false);
            _prospector.SetWorkMode(ProspectorWorkMode.Manual);
            ApplyPlanPresetsFromProspector();
            _fieldScanner.SetPreviewVisible(true);
            _fieldScanner.ShowPlanningPreview(true);

            float size01 = ProspectorScanFormulas.Size01(
                _fieldScanner.PlannedRangeCells, _fieldScanner.PlannedHalfAngleDeg, _fieldScanner.Spec);
            float hours = ProspectorScanFormulas.ScanDurationHours(_prospector.Stats, size01);
            DigHoodLog.Push(
                $"SCAN | PLANNING — 1/2/3 range · Q/E width · Enter/LMB confirm · Esc cancel | " +
                $"est {hours:0.#}h ({ProspectorScanFormulas.DaysFromHours(hours):0.##}d)");
            Debug.Log($"[SCAN] Planning mode — estimated duration {hours:0.##}h");
        }

        void ExitScannerPlanMode()
        {
            if (!_scannerPlanMode) return;
            _scannerPlanMode = false;
            _fieldScanner?.SetPreviewVisible(false);
        }

        void ApplyPlanPresetsFromProspector()
        {
            if (_fieldScanner == null || _prospector == null) return;
            _fieldScanner.ApplyDistancePreset(_prospector.Distance);
            _fieldScanner.ApplyWidthPreset(_prospector.Width);
            _fieldScanner.ShowPlanningPreview(true);
        }

        void ConfirmScannerPlan()
        {
            if (!_scannerPlanMode || _fieldScanner == null || _prospector == null) return;
            if (_fieldScanner.State != ProspectorScannerState.Ready) return;

            _scanHistory ??= new ProspectorScanHistory();
            if (_scanHistory.Findings != null)
            {
                // Idempotent re-bind if history was lazily created after Start
                _scanHistory.Findings.FindingAdded -= OnProspectorFindingAdded;
                _scanHistory.Findings.FindingAdded += OnProspectorFindingAdded;
            }
            SyncProspectingFindingsAuthor();
            var wr = _prospector.AssignedWorker;
            var profile = wr != null
                ? GetSheetProfile(wr.WorkerId)
                : WorkerSheetProfile.Baseline;
            if (!_fieldScanner.TryBeginScan(
                    _scanHistory, _prospector.Stats,
                    prospectorName: wr != null ? wr.DisplayName : "Prospector",
                    absoluteGameHours: _absoluteGameHours,
                    prospectorId: wr != null ? wr.WorkerId.ToString() : "0",
                    prospectorProfile: profile,
                    prospectorProfileLabel: WorkerStatProfiles.Label(
                        (byte)0, profile),
                    workerId: wr != null ? wr.WorkerId : 0))
            {
                DigHoodLog.Push("SCAN | Confirm failed — empty area or busy");
                return;
            }

            _prospector.NotifyScanStarted();
            _scannerPlanMode = false;
            _fieldScanner.SetPreviewVisible(false);
            if (_playerTactical != null && !_playerTactical.Visible)
                _playerTactical.SetVisible(true);
            DigHoodLog.Push($"SCAN | CONFIRMED → SCANNING | id #{_fieldScanner.ActiveSession?.Record?.ScanId}");
        }

        void RotateScannerPlacement(float degrees)
        {
            float ang = Mathf.Atan2(_scannerPlaceFacing.y, _scannerPlaceFacing.x) * Mathf.Rad2Deg;
            ang += degrees;
            float r = ang * Mathf.Deg2Rad;
            _scannerPlaceFacing = new Vector2(Mathf.Cos(r), Mathf.Sin(r)).normalized;
            if (_scannerGhost != null)
                _scannerGhost.SetFacing(_scannerPlaceFacing);
        }

        void UpdateScannerPlacementGhost()
        {
            if (!_scannerPlaceMode || _scannerGhost == null || _world == null || _prospector == null)
                return;

            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse == null || cam == null) return;

            Vector2 screen = mouse.position.ReadValue();
            Vector3 world3 = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            Vector2 world = world3; // worldRoot is identity — same space as worker localPosition

            bool ok = ProspectorScannerPlacement.TryValidate(
                _world, _prospector, world, _prospector.BodyRadius,
                out Vector2 center, out string fail);
            _scannerPlaceValid = ok;
            _scannerPlaceFail = ok ? "" : fail;
            _scannerGhost.transform.localPosition = center;
            _scannerGhost.SetFacing(_scannerPlaceFacing);
            _scannerGhost.ShowPlanningPreview(ok);
        }

        void ConfirmScannerPlacement()
        {
            if (!_scannerPlaceMode || !_scannerPlaceValid || _scannerGhost == null || _prospector == null)
                return;

            Vector2 pos = _scannerGhost.Position;
            Vector2 facing = _scannerPlaceFacing;

            // Replace previous field scanner if any (Stage 1: one kit)
            if (_fieldScanner != null)
            {
                _prospector.CancelScannerAssignment(clearEquipment: false);
                Destroy(_fieldScanner.gameObject);
                _fieldScanner = null;
            }

            ExitScannerPlacementMode();

            _fieldScanner = ProspectorScannerEquipment.Spawn(
                _worldRoot, _world, pos, facing);
            _fieldScanner.SetPreviewVisible(false);

            // Link scanner as Prospecting provider for whoever currently holds the job
            var wr = _prospector.AssignedWorker;
            if (wr != null)
            {
                _assignments.AssignInternal(
                    wr.WorkerId, JobType.Prospecting, _fieldScanner.ProviderId, _absoluteGameHours);
                _fieldScanner.NotifyAssigned(wr);
            }
            _prospector.AssignScannerSetup(_fieldScanner);
            DigHoodLog.Push(
                $"SCANNER | PLACED {_fieldScanner.ProviderId} | by {(wr != null ? wr.DisplayName : "?")} | " +
                $"pos {pos.x:0.00},{pos.y:0.00}");
            Debug.Log($"[SCANNER] Placed PACKED at ({pos.x:0.00},{pos.y:0.00}) — Prospector traveling to set up");
        }

        void OnExcavatorDebrisStrike(int x, int y)
        {
            if (_collapse == null || _worker == null) return;
            var wr = _worker.AssignedWorker;
            if (wr == null) return;
            var field = _collapse.FindNearestClearable(_world.CellCenter(x, y), 2.2f);
            if (field == null) return;
            // Meaningful chip per dig — debris easier than bedrock but still serious
            _collapse.TickClearance(field, wr, 0.14f, asExcavator: true, out string st);
            if (!string.IsNullOrEmpty(st))
                DigHoodLog.Push($"EXCAVATOR | {st}");
            _collapse.RefreshTrappedFlags(_crewWorkers, CrewWorldPos);
        }

        Vector2 CrewWorldPos(WorkerRuntime wr)
        {
            if (wr == null) return BasecampPos;
            // Person-task / incap: avatar is sole physical authority — never snap to host/machine
            if ((wr.State != null && wr.State.Incapacitated) || PersonTaskAuthority(wr))
            {
                var avAuth = _presence.Get(wr.WorkerId);
                return avAuth != null ? avAuth.PresencePosition : BasecampPos;
            }
            // Prefer live host when operating; else avatar presence
            if (_crewPhase == CrewPhase.OnShift && wr.State != null && !wr.State.Incapacitated)
            {
                var asg = _assignments.GetAssignment(wr.WorkerId);
                if (asg != null)
                {
                    switch (asg.JobType)
                    {
                        case JobType.Excavation when _worker != null: return _worker.Position;
                        case JobType.Hauling when _hauler != null: return _hauler.transform.localPosition;
                        case JobType.Engineering when _engineer != null: return _engineer.Position;
                        case JobType.Prospecting when _prospector != null: return _prospector.Position;
                        case JobType.Refining when _refiner != null: return _refiner.Position;
                        case JobType.Steward when _steward != null: return _steward.Position;
                    }
                }
            }
            var av = _presence.Get(wr.WorkerId);
            return av != null ? av.PresencePosition : BasecampPos;
        }

        void SetCrewWorldPos(WorkerRuntime wr, Vector2 pos)
        {
            if (wr == null) return;
            var av = _presence.Get(wr.WorkerId);
            if (av != null)
            {
                av.SetPresencePosition(pos);
                av.ClearFollowing();
                av.Show();
            }
        }

        /// <summary>Yield job host; person avatar becomes physical authority. Assignment reserved.</summary>
        void LeaveHostForPersonTask(WorkerRuntime wr, string reason)
        {
            if (wr == null) return;
            var asg = _assignments.GetAssignment(wr.WorkerId);
            var av = _presence.Get(wr.WorkerId);
            if (asg != null && asg.JobType != JobType.Unassigned)
            {
                YieldHost(asg.JobType);
                PersistentWorkerBody.SetOperatorBodyVisibleForJob(
                    asg.JobType, _worker, _prospector, _hauler, _refiner, _engineer, _steward, false);
                if (av != null)
                {
                    if (PersistentWorkerBody.IsMachineCabinJob(asg.JobType) && _worker != null)
                        ExcavatorCabin.Exit(av, _worker.Position);
                    else
                    {
                        Vector2 exit = GetProviderExitPoint(asg.JobType);
                        float d = Vector2.Distance(av.PresencePosition, exit);
                        if (d > 0.85f)
                            RelocateAvatar(av, exit, reason, "LeaveHostForPersonTask");
                        else
                            av.SetPresencePosition(exit);
                        av.ClearFollowing();
                        av.Show();
                    }
                    av.SetDevForceShowHidden(_devForceShowHiddenAvatars);
                }
            }
            else if (av != null)
            {
                av.ClearFollowing();
                av.Show();
            }
        }

        void TickTunnelCollapse()
        {
            if (_collapse == null || _world == null) return;
            float hoursDelta = Time.deltaTime / SecondsPerGameHour;
            SocialMemoryStore mem = _socialAura != null && _socialAura.IsBootstrapped
                ? _socialAura.Memory : null;

            if (_crewPhase == CrewPhase.OnShift)
            {
                _collapse.TickNatural(hoursDelta, _crewWorkers, CrewWorldPos, mem, _managerRels);
                TickDebrisClearanceAndRescue(hoursDelta, mem);
            }
            else
            {
                // Still refresh trapped flags overnight — cycle continues
                _collapse.RefreshTrappedFlags(_crewWorkers, CrewWorldPos);
            }
        }

        void TickPrioritySystem()
        {
            if (_crewWorkers == null || _crewPhase != CrewPhase.OnShift) return;
            float hoursDelta = Time.deltaTime / SecondsPerGameHour;
            _priorities.BindHosts(
                _world, _collapse, _mineInfra, _worker, _hauler, _refiner, _engineer,
                _prospector, _scanHistory != null ? _scanHistory.Analyst : null,
                _steward, _campLife, _crewWorkers, CrewWorldPos, BasecampPos);

            // Continuous active-task validation (OFF / unavailable must stop voluntary work)
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null) continue;
                bool incap = wr.State != null && wr.State.Incapacitated;
                if (!wr.IsAlive || incap)
                {
                    ReleaseUnavailableWorkerClaims(wr, incap ? "incapacitated" : "worker dead");
                    continue;
                }
                if (wr.Priorities == null) continue;
                if (_priorities.InvalidateActiveIfNeeded(wr))
                    TemporaryYieldHostForPriority(wr, "priority invalidate");
                else
                    TemporaryYieldHostForPriority(wr, "priority host gate");
            }

            bool eval = _priorities.ShouldEvaluate(hoursDelta, _crewWorkers);
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null || !wr.IsAlive) continue;
                if (wr.Priorities == null)
                {
                    wr.Priorities = new WorkerPriorityPrefs();
                    WorkerPriorityPrefs.ApplyCrewDefaults(wr, wr.DisplayName);
                }

                // Accrue only while actually performing the active task (not idle on station)
                if (!WorkPriorityResolver.IsHardBlocked(wr, out _)
                    && !string.IsNullOrEmpty(wr.Priorities.ActiveTaskId)
                    && !wr.Priorities.IsOff(wr.Priorities.ActiveTaskId)
                    && IsActuallyPerformingPriorityTask(wr, wr.Priorities.ActiveTaskId))
                {
                    _priorities.AccrueActive(wr, hoursDelta);
                }

                if (wr.CampBody != null && wr.CampBody.PriorityDutyActive)
                {
                    string duty = wr.Priorities.ActiveTaskId ?? "";
                    if (!string.IsNullOrEmpty(duty) && wr.Priorities.IsOff(duty))
                    {
                        wr.CampBody.PriorityDutyActive = false;
                        wr.CampBody.RescueReturning = true;
                        wr.Priorities.ClearActiveTask("priority OFF");
                    }
                    else
                        TickPriorityDutyLifecycle(wr);
                }

                if (!eval) continue;
                if (WorkPriorityResolver.IsHardBlocked(wr, out string block))
                {
                    wr.Priorities.LastUnavailableReason = block;
                    wr.Priorities.ResolverDirty = false;
                    continue;
                }

                var job = GetAssignmentJob(wr);
                var result = _priorities.ResolveFor(
                    wr, job, CrewWorldPos(wr), _absoluteGameHours, emergencyOk: true);
                _priorities.ApplyResolve(wr, result, _absoluteGameHours);
                SyncHostToActivePriorityTask(wr);

                if (string.IsNullOrEmpty(result.TaskId)) continue;

                // Dispatch side-effects (existing systems)
                DispatchPriorityTask(wr, result.TaskId, hoursDelta);
            }

            if (eval)
                TryClaimStationsByPriority();
        }

        /// <summary>
        /// Persistent assignment stays. Yield execution only when priority forbids this host tick.
        /// Never TryUnassignJob from priority paths.
        /// </summary>
        void TemporaryYieldHostForPriority(WorkerRuntime wr, string reason)
        {
            if (wr == null) return;
            var job = GetAssignmentJob(wr);
            if (job == JobType.Unassigned) return;
            if (WorkPriorityResolver.HostAllowsVoluntaryWork(wr, job)) return;
            // Already yielded (body vacant) — keep assignment
            if (!ReferenceEquals(GetBodyAssignedWorker(job), wr)) return;
            LeaveHostForPersonTask(wr, reason);
            if (wr.Priorities != null)
                wr.Priorities.LastHostYieldedForOff = true;
        }

        /// <summary>
        /// Match host execution to ActiveTask without destroying persistent JobType assignment.
        /// Cross-station work uses soft claim (TryClaimStations) or person duty — not Unassign.
        /// </summary>
        void SyncHostToActivePriorityTask(WorkerRuntime wr)
        {
            if (wr?.Priorities == null) return;
            if (wr.CampBody != null
                && (wr.CampBody.RescueDutyActive || wr.CampBody.BeingRescued
                    || wr.CampBody.PriorityDutyActive))
                return;

            var have = GetAssignmentJob(wr);
            if (have == JobType.Unassigned) return; // do not invent specialization here

            string active = wr.Priorities.ActiveTaskId ?? "";
            var want = WorkPriorityResolver.StationJobForTask(active);

            // Priority forbids running home host right now → temporary yield only
            if (!WorkPriorityResolver.HostAllowsVoluntaryWork(wr, have))
            {
                TemporaryYieldHostForPriority(wr, "sync yield " + active);
                // Soft claim may later TryAssignJob to want (exclusive station) — that is
                // an explicit reassignment, not a blanket Unassign-to-none.
                return;
            }

            // Allowed on home station — rebind if previously yielded and seat is free
            if (ReferenceEquals(GetBodyAssignedWorker(have), wr)) return;
            if (GetBodyAssignedWorker(have) != null) return;
            BindHost(have, wr);
            NotifyProviderAssigned(have, wr);
            var av = _presence.Get(wr.WorkerId);
            if (av != null)
                SeatAvatarAtWork(wr, av);
            if (wr.CampBody != null)
                wr.CampBody.RescueReturning = false;
        }

        /// <summary>
        /// Specialization host may tick only if priority authority allows voluntary work.
        /// </summary>
        bool PriorityAllowsHostTick(WorkerRuntime wr, JobType job) =>
            wr != null && WorkPriorityResolver.HostAllowsVoluntaryWork(wr, job);

        /// <summary>Dead/incap workers must not hold exclusive station claims.</summary>
        void ReleaseUnavailableWorkerClaims(WorkerRuntime wr, string reason)
        {
            if (wr == null) return;
            if (wr.Priorities != null && !string.IsNullOrEmpty(wr.Priorities.ActiveTaskId))
                wr.Priorities.ClearActiveTask(reason);
            var job = GetAssignmentJob(wr);
            if (job != JobType.Unassigned)
                TryUnassignJob(job, out _);
            if (wr.CampBody != null)
            {
                if (wr.CampBody.RescueDutyActive)
                    AbortRescueMission(reason);
                wr.CampBody.PriorityDutyActive = false;
                wr.CampBody.RescueDutyActive = false;
            }
            _priorities.RequestImmediateEval();
        }

        /// <summary>
        /// Hour targets accrue only during real performance — not while assigned but idle.
        /// </summary>
        bool IsActuallyPerformingPriorityTask(WorkerRuntime wr, string taskId)
        {
            if (wr == null || string.IsNullOrEmpty(taskId)) return false;
            if (taskId == WorkerGenericTaskIds.Rescue)
                return wr.CampBody != null && wr.CampBody.RescueDutyActive;
            if (taskId == WorkerGenericTaskIds.ClearDebris)
            {
                if (!CanPerformJobActions(wr)
                    && (wr.CampBody == null || !wr.CampBody.PriorityDutyActive))
                    return false;
                Vector2 pos = CrewWorldPos(wr);
                var field = _collapse?.FindNearestClearable(pos, 2.6f);
                // Accrue only while at debris — not while walking PriorityDuty
                return field != null && Vector2.Distance(pos, field.Epicenter) <= 2.6f;
            }
            if (taskId == WorkerGenericTaskIds.TreatInjuries)
            {
                if (Vector2.Distance(CrewWorldPos(wr), BasecampPos) > 5.5f) return false;
                for (int i = 0; _crewWorkers != null && i < _crewWorkers.Length; i++)
                {
                    var p = _crewWorkers[i];
                    if (p?.State != null && p.State.NeedsCare && !p.State.Incapacitated)
                        return true;
                }
                return false;
            }

            if (!CanPerformJobActions(wr)) return false;
            var job = GetAssignmentJob(wr);
            var station = WorkPriorityResolver.StationJobForTask(taskId);
            if (station != JobType.Unassigned && job != station) return false;

            switch (taskId)
            {
                case WorkerGenericTaskIds.Excavate:
                    // Dig time only — not idle on machine with paint pending
                    return _worker != null && _worker.IsActivelyDigging;
                case WorkerGenericTaskIds.HaulMaterials:
                    if (_hauler == null) return false;
                    string hl = _hauler.ActivityLabel ?? "";
                    // Not SEEK travel — only load / haul / unload
                    return (hl.IndexOf("LOADING", System.StringComparison.OrdinalIgnoreCase) >= 0
                            || hl.IndexOf("HAUL", System.StringComparison.OrdinalIgnoreCase) >= 0
                            || hl.IndexOf("UNLOAD", System.StringComparison.OrdinalIgnoreCase) >= 0)
                           && (LoosePile.LiveCount > 0 || _hauler.CargoCount > 0);
                case WorkerGenericTaskIds.RefineOre:
                    if (_refiner == null) return false;
                    string rl = _refiner.ActivityLabel ?? "";
                    // Wash / carry work — not idle fetch travel alone
                    return rl.IndexOf("WASH", System.StringComparison.OrdinalIgnoreCase) >= 0
                           || rl.IndexOf("CARRY", System.StringComparison.OrdinalIgnoreCase) >= 0
                           || rl.IndexOf("UNLOAD", System.StringComparison.OrdinalIgnoreCase) >= 0;
                case WorkerGenericTaskIds.Prospect:
                    return _prospector != null
                           && (_prospector.WorkMode != ProspectorWorkMode.Manual
                               || _prospector.HasScannerAssignment
                               || _prospector.IsSettingUpScanner);
                case WorkerGenericTaskIds.AnalyseSurvey:
                    return _scanHistory != null && _scanHistory.Analyst != null
                           && _scanHistory.Analyst.HasWork;
                case WorkerGenericTaskIds.InstallSupports:
                    return _engineer != null
                           && _engineer.WorkKind == EngineerWorkKind.BuildingSupport;
                case WorkerGenericTaskIds.InstallLighting:
                    return _engineer != null
                           && _engineer.WorkKind == EngineerWorkKind.InstallingLantern;
                case WorkerGenericTaskIds.RepairEquipment:
                    return _engineer != null && _engineer.IsRepairing;
                case WorkerGenericTaskIds.PrepareMeals:
                case WorkerGenericTaskIds.CleanCamp:
                case WorkerGenericTaskIds.TendCampSystems:
                    return _steward != null && _steward.WorkKind != StewardWorkKind.Idle
                           && _steward.WorkKind != StewardWorkKind.TendingWounds;
                default:
                    return false;
            }
        }

        void TickPriorityDutyLifecycle(WorkerRuntime wr)
        {
            if (wr?.CampBody == null || !wr.CampBody.PriorityDutyActive) return;
            string active = wr.Priorities != null ? wr.Priorities.ActiveTaskId : "";
            if (active == WorkerGenericTaskIds.ClearDebris)
            {
                var field = _collapse?.FindNearestClearable(CrewWorldPos(wr), 3.5f);
                if (field == null)
                {
                    wr.CampBody.PriorityDutyActive = false;
                    wr.CampBody.RescueReturning = true;
                    DigHoodLog.Push($"PRIORITY | {wr.DisplayName} clear-debris duty ended — returning");
                }
                return;
            }
            if (active == WorkerGenericTaskIds.TreatInjuries)
            {
                if (Vector2.Distance(CrewWorldPos(wr), BasecampPos) <= 5.5f)
                {
                    // Stay at camp while treating; release when no patients
                    bool any = false;
                    for (int i = 0; _crewWorkers != null && i < _crewWorkers.Length; i++)
                    {
                        var p = _crewWorkers[i];
                        if (p?.State != null && p.State.NeedsCare && !p.State.Incapacitated)
                            any = true;
                    }
                    if (!any)
                    {
                        wr.CampBody.PriorityDutyActive = false;
                        wr.CampBody.RescueReturning = true;
                    }
                }
                return;
            }
            // Other priority duties: release when active task is a station job again
            if (WorkPriorityResolver.StationJobForTask(active) != JobType.Unassigned
                || string.IsNullOrEmpty(active))
            {
                wr.CampBody.PriorityDutyActive = false;
                wr.CampBody.RescueReturning = true;
            }
        }

        void DispatchPriorityTask(WorkerRuntime wr, string taskId, float hoursDelta)
        {
            if (wr == null || string.IsNullOrEmpty(taskId)) return;

            // Rescue — existing universal rescue accepts via PickUniversalRescuer (respects OFF)
            if (taskId == WorkerGenericTaskIds.Rescue)
                return;

            // Clear debris — any eligible worker near field (not JobType-locked)
            if (taskId == WorkerGenericTaskIds.ClearDebris && _collapse != null)
            {
                if (!CanPerformJobActions(wr) && (wr.CampBody == null || !wr.CampBody.PriorityDutyActive))
                    return;
                Vector2 pos = CrewWorldPos(wr);
                var field = _collapse.FindNearestClearable(pos, 2.6f);
                if (field == null || !_collapse.CanClearDebris(wr, asExcavator: false)) return;
                if (!_collapse.IsReachableFromCamp(pos) && !_collapse.HasOpenTunnelPath(pos, field.Epicenter))
                    return;
                // If on specialization host far from debris, leave for person clear duty
                if (CanPerformJobActions(wr) && Vector2.Distance(pos, field.Epicenter) > 2.6f)
                {
                    // Walk as person toward debris
                    BeginPriorityDuty(wr, taskId);
                    return;
                }
                if (wr.CampBody != null && wr.CampBody.PriorityDutyActive)
                {
                    var av = _presence.Get(wr.WorkerId);
                    int idx = CrewRosterIndex(wr);
                    if (av != null && idx >= 0)
                        TryStepRescueAvatar(idx, av, field.Epicenter, 0.9f);
                    pos = CrewWorldPos(wr);
                    field = _collapse.FindNearestClearable(pos, 2.6f);
                }
                if (field != null && Vector2.Distance(CrewWorldPos(wr), field.Epicenter) <= 2.6f)
                {
                    _collapse.TickClearance(field, wr, hoursDelta * 0.85f, asExcavator: false, out _);
                    _collapse.RefreshTrappedFlags(_crewWorkers, CrewWorldPos);
                }
                return;
            }

            // Treat injuries at camp — StewardWoundCare (any worker with Treat priority)
            if (taskId == WorkerGenericTaskIds.TreatInjuries)
            {
                if (Vector2.Distance(CrewWorldPos(wr), BasecampPos) > 5.5f)
                {
                    BeginPriorityDuty(wr, taskId);
                    var av = _presence.Get(wr.WorkerId);
                    int idx = CrewRosterIndex(wr);
                    if (av != null && idx >= 0)
                        TryStepRescueAvatar(idx, av, CampNavDestination, 0.95f);
                    return;
                }
                // Tend nearest NeedsCare patient
                WorkerRuntime patient = null;
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    var p = _crewWorkers[i];
                    if (p == null || p.WorkerId == wr.WorkerId || !p.IsAlive) continue;
                    if (p.State != null && p.State.Incapacitated) continue;
                    if (p.State == null || !p.State.NeedsCare) continue;
                    patient = p;
                    break;
                }
                if (patient != null)
                {
                    SocialMemoryStore mem = _socialAura != null && _socialAura.IsBootstrapped
                        ? _socialAura.Memory : null;
                    if (StewardWoundCare.TryTend(wr, patient, out string res, mem, _absoluteGameHours)
                        && Time.frameCount % 90 == 0)
                        DigHoodLog.Push($"TREAT | {wr.DisplayName}: {res} → {patient.DisplayName}");
                }
            }
        }

        void BeginPriorityDuty(WorkerRuntime wr, string taskId)
        {
            if (wr?.CampBody == null) return;
            if (wr.CampBody.PriorityDutyActive) return;
            if (wr.CampBody.RescueDutyActive) return;
            LeaveHostForPersonTask(wr, "priority duty " + taskId);
            wr.CampBody.PriorityDutyActive = true;
            if (wr.Priorities != null)
            {
                wr.Priorities.ActiveTaskId = taskId;
                wr.Priorities.ActiveTaskStartedGameHours = _absoluteGameHours;
            }
        }

        void EndPriorityDutyIfIdle(WorkerRuntime wr)
        {
            if (wr?.CampBody == null || !wr.CampBody.PriorityDutyActive) return;
            // Return when active task is station work again or empty
            string active = wr.Priorities != null ? wr.Priorities.ActiveTaskId : "";
            var station = WorkPriorityResolver.StationJobForTask(active);
            if (station != JobType.Unassigned || string.IsNullOrEmpty(active)
                || active == WorkerGenericTaskIds.Rescue)
            {
                wr.CampBody.PriorityDutyActive = false;
                wr.CampBody.RescueReturning = true;
            }
        }

        /// <summary>
        /// Soft station claim: if exclusive host work exists, prefer highest-priority eligible worker.
        /// Anti-thrash via MinCommit on ActiveTaskStartedGameHours.
        /// </summary>
        void TryClaimStationsByPriority()
        {
            if (_crewWorkers == null || _assignments == null) return;
            TryClaimStation(JobType.Excavation, WorkerGenericTaskIds.Excavate);
            TryClaimStation(JobType.Hauling, WorkerGenericTaskIds.HaulMaterials);
            TryClaimStation(JobType.Refining, WorkerGenericTaskIds.RefineOre);
            TryClaimStation(JobType.Prospecting, WorkerGenericTaskIds.Prospect);
            TryClaimStation(JobType.Engineering, WorkerGenericTaskIds.InstallSupports);
            TryClaimStation(JobType.Steward, WorkerGenericTaskIds.TreatInjuries);
        }

        void TryClaimStation(JobType stationJob, string primaryTaskId)
        {
            var avail = _priorities.Context.Probe(primaryTaskId);
            // Engineering: also lighting/repair
            if (stationJob == JobType.Engineering && !avail.Available)
            {
                avail = _priorities.Context.Probe(WorkerGenericTaskIds.InstallLighting);
                if (!avail.Available)
                    avail = _priorities.Context.Probe(WorkerGenericTaskIds.RepairEquipment);
            }
            if (stationJob == JobType.Prospecting && !avail.Available)
                avail = _priorities.Context.Probe(WorkerGenericTaskIds.AnalyseSurvey);
            if (stationJob == JobType.Steward && !avail.Available)
            {
                avail = _priorities.Context.Probe(WorkerGenericTaskIds.PrepareMeals);
                if (!avail.Available)
                    avail = _priorities.Context.Probe(WorkerGenericTaskIds.CleanCamp);
            }
            if (!avail.Available) return;

            int currentId = _assignments.GetWorkerIdForJob(stationJob);
            WorkerRuntime best = null;
            float bestScore = -1f;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null || !wr.IsAlive) continue;
                if (WorkPriorityResolver.IsHardBlocked(wr, out _)) continue;
                if (wr.Priorities != null && wr.Priorities.IsOff(primaryTaskId)
                    && stationJob != JobType.Engineering && stationJob != JobType.Steward
                    && stationJob != JobType.Prospecting)
                    continue;
                // Must not be OFF for at least one relevant task
                if (wr.Priorities != null)
                {
                    bool any = !wr.Priorities.IsOff(primaryTaskId);
                    if (stationJob == JobType.Engineering)
                        any = any || !wr.Priorities.IsOff(WorkerGenericTaskIds.InstallLighting)
                              || !wr.Priorities.IsOff(WorkerGenericTaskIds.RepairEquipment);
                    if (stationJob == JobType.Prospecting)
                        any = any || !wr.Priorities.IsOff(WorkerGenericTaskIds.AnalyseSurvey);
                    if (stationJob == JobType.Steward)
                        any = any || !wr.Priorities.IsOff(WorkerGenericTaskIds.PrepareMeals)
                              || !wr.Priorities.IsOff(WorkerGenericTaskIds.CleanCamp)
                              || !wr.Priorities.IsOff(WorkerGenericTaskIds.TreatInjuries);
                    if (!any) continue;
                }

                var job = GetAssignmentJob(wr);
                var res = _priorities.ResolveFor(
                    wr, job, CrewWorldPos(wr), _absoluteGameHours, true);
                // Hard rule: only workers whose winning resolve wants this station
                if (WorkPriorityResolver.StationJobForTask(res.TaskId) != stationJob)
                    continue;

                // Band-first score, then soft factors — never let P2 beat P1 for the seat
                float score = (5 - (int)wr.Priorities.GetPriority(primaryTaskId)) * 1000f
                              + WorkPriorityResolver.Suitability01(
                                  wr, WorkTaskRegistry.Get(primaryTaskId), job) * 20f
                              + wr.Priorities.TargetDeficit(primaryTaskId) * 15f;
                if (wr.WorkerId == currentId) score += 25f; // same-band continuity
                if (score > bestScore) { bestScore = score; best = wr; }
            }

            if (best == null) return;
            if (best.WorkerId == currentId) return;
            // Only fill vacant exclusive seats. Stealing via TryAssignJob Unassigns the
            // previous specialist (→ "Unassigned · …"). Soft claim must not do that.
            // Higher-band workers yield their home host via TemporaryYieldHostForPriority
            // and wait for a free seat / player reassignment.
            if (currentId > 0)
            {
                var cur = FindCrewWorker(currentId);
                if (cur != null && cur.IsAlive) return;
            }

            if (!TryAssignJob(best, stationJob, out string reason))
            {
                if (DevMode.Enabled && Time.frameCount % 180 == 0)
                    DigHoodLog.Push($"PRIORITY | claim {stationJob} → {best.DisplayName} failed: {reason}");
                return;
            }
            DigHoodLog.Push($"PRIORITY | {best.DisplayName} claimed {stationJob} for {primaryTaskId}");
        }


        void TickDebrisClearanceAndRescue(float hoursDelta, SocialMemoryStore mem)
        {
            if (_collapse == null || _crewWorkers == null) return;

            // Hauler auto-clear when near debris — only if Clear Debris not OFF
            if (_hauler != null && CanPerformJobActions(_hauler.AssignedWorker))
            {
                var hWr = _hauler.AssignedWorker;
                if (hWr.Priorities == null || !hWr.Priorities.IsOff(WorkerGenericTaskIds.ClearDebris))
                {
                Vector2 hPos = _hauler.transform.localPosition;
                var field = _collapse.FindNearestClearable(hPos, 2.4f);
                if (field != null && _collapse.CanClearDebris(hWr, asExcavator: false)
                    && _collapse.IsReachableFromCamp(hPos))
                {
                    if (_collapse.TickClearance(field, hWr, hoursDelta * 0.85f, asExcavator: false, out string st))
                        DigHoodLog.Push($"HAULER | {st}");
                    else if (!string.IsNullOrEmpty(st) && Time.frameCount % 90 == 0)
                        DigHoodLog.Push($"HAULER | {st}");
                }
                }
            }

            TickUniversalRescue(hoursDelta, mem);
            TickRescueReturning(hoursDelta);
            _collapse.RefreshTrappedFlags(_crewWorkers, CrewWorldPos);
        }

        void TickUniversalRescue(float hoursDelta, SocialMemoryStore mem)
        {
            if (_collapse == null || _crewWorkers == null) return;
            EnsurePersonNav();

            // Drop stale mission
            if (_activeRescue != null)
            {
                var r = FindCrewWorker(_activeRescue.RescuerId);
                var c = FindCrewWorker(_activeRescue.CasualtyId);
                if (r == null || c == null || !r.IsAlive || !c.IsAlive
                    || (c.State != null && !c.State.Incapacitated
                        && (c.CampBody == null || !c.CampBody.BeingRescued)))
                {
                    AbortRescueMission("casualty recovered or invalid");
                }
                else if (r.Priorities != null && r.Priorities.IsOff(WorkerGenericTaskIds.Rescue))
                {
                    // Voluntary Rescue OFF aborts mid-mission (hard casualty state remains)
                    if (r.Priorities != null)
                        r.Priorities.ClearActiveTask("priority OFF");
                    AbortRescueMission("rescuer Rescue priority OFF");
                }
            }

            if (_activeRescue == null)
            {
                var casualty = _collapse.FindIncapacitatedNeedingRescue(_crewWorkers, CrewWorldPos);
                if (casualty == null || !WorkerRescue.NeedsRescue(casualty)) return;
                if (casualty.CampBody != null && casualty.CampBody.BeingRescued) return;

                var rescuer = PickUniversalRescuer(casualty);
                if (rescuer == null) return;
                BeginUniversalRescue(rescuer, casualty);
            }

            if (_activeRescue == null) return;
            var rescuerWr = FindCrewWorker(_activeRescue.RescuerId);
            var casualtyWr = FindCrewWorker(_activeRescue.CasualtyId);
            if (rescuerWr == null || casualtyWr == null) return;

            int rIdx = CrewRosterIndex(rescuerWr);
            var rAv = _presence.Get(rescuerWr.WorkerId);
            var cAv = _presence.Get(casualtyWr.WorkerId);
            if (rAv == null || cAv == null) return;

            Vector2 rPos = rAv.PresencePosition;
            Vector2 cPos = cAv.PresencePosition;
            bool pathOpen = _collapse.HasOpenTunnelPath(rPos, cPos);

            switch (_activeRescue.Phase)
            {
                case RescueMissionPhase.Approach:
                {
                    if (!pathOpen)
                    {
                        _activeRescue.LastStatus = "ACCESS BLOCKED";
                        if (Time.frameCount % 120 == 0)
                            DigHoodLog.Push(
                                $"RESCUE | {rescuerWr.DisplayName} — ACCESS BLOCKED (no walk through debris)");
                        // Path as far as open tunnel allows toward casualty (no teleport)
                        TryStepRescueAvatar(rIdx, rAv, cPos,
                            WorkerRescue.ApproachSpeedMul(rescuerWr) * 0.55f);
                        break;
                    }

                    float arrive = 0.55f;
                    if ((cPos - rPos).sqrMagnitude <= arrive * arrive)
                    {
                        _activeRescue.Phase = RescueMissionPhase.Assist;
                        _activeRescue.AssistHoursLeft = WorkerRescue.AssistDurationHours(rescuerWr);
                        _activeRescue.LastStatus = $"ASSIST {casualtyWr.DisplayName}";
                        DigHoodLog.Push(
                            $"RESCUE | {rescuerWr.DisplayName} reached {casualtyWr.DisplayName} — assisting");
                        break;
                    }

                    float spdMul = WorkerRescue.ApproachSpeedMul(rescuerWr);
                    TryStepRescueAvatar(rIdx, rAv, cPos, spdMul);
                    rescuerWr.State.SpendStamina(
                        WorkerRescue.StaminaTaxPerGameHour(rescuerWr, false) * hoursDelta * 10f);
                    _activeRescue.LastStatus = $"REACHING {casualtyWr.DisplayName}";
                    break;
                }
                case RescueMissionPhase.Assist:
                {
                    if (!pathOpen)
                    {
                        _activeRescue.LastStatus = "ACCESS BLOCKED";
                        break;
                    }
                    _activeRescue.AssistHoursLeft -= hoursDelta;
                    rescuerWr.State.SpendStamina(
                        WorkerRescue.StaminaTaxPerGameHour(rescuerWr, false) * hoursDelta * 10f);
                    // Empathy/composure: slight frustration relief for casualty while assisted
                    float emp = rescuerWr.Stats.Get(WorkerStatId.Empathy) / 20f;
                    casualtyWr.State.Frustration = Mathf.Max(0f,
                        casualtyWr.State.Frustration - hoursDelta * (1.2f + emp * 2f));
                    _activeRescue.LastStatus = $"ASSIST {casualtyWr.DisplayName}";
                    if (_activeRescue.AssistHoursLeft <= 0f)
                    {
                        _activeRescue.Phase = RescueMissionPhase.Carry;
                        DigHoodLog.Push(
                            $"RESCUE | {rescuerWr.DisplayName} carrying {casualtyWr.DisplayName} → camp");
                    }
                    break;
                }
                case RescueMissionPhase.Carry:
                {
                    Vector2 door = CampNavDestination;
                    bool pathToCamp = _collapse.HasOpenTunnelPath(rPos, door)
                                     || _collapse.IsReachableFromCamp(rPos);
                    if (!pathToCamp)
                    {
                        _activeRescue.LastStatus = "ACCESS BLOCKED";
                        if (Time.frameCount % 120 == 0)
                            DigHoodLog.Push($"RESCUE | CARRY BLOCKED — clear debris first");
                        break;
                    }
                    if (!pathOpen && Vector2.Distance(rPos, cPos) > 0.7f)
                    {
                        // Stay with casualty — do not leave them behind through solid rubble
                        _activeRescue.LastStatus = "ACCESS BLOCKED";
                        break;
                    }

                    float carryMul = WorkerRescue.CarrySpeedMul(rescuerWr);
                    // Move rescuer toward camp; casualty stays with rescuer (no teleport)
                    TryStepRescueAvatar(rIdx, rAv, door, carryMul);
                    Vector2 after = rAv.PresencePosition;
                    cAv.SetPresencePosition(after + (cPos - rPos).normalized * 0.12f);
                    cAv.ClearFollowing();
                    cAv.Show();

                    rescuerWr.State.SpendStamina(
                        WorkerRescue.StaminaTaxPerGameHour(rescuerWr, true) * hoursDelta * 10f);
                    rescuerWr.State.AddFrustration(hoursDelta * 1.5f);
                    _activeRescue.LastStatus = $"CARRY {casualtyWr.DisplayName}";

                    float arriveCamp = 1.35f;
                    if ((door - after).sqrMagnitude <= arriveCamp * arriveCamp
                        || Vector2.Distance(after, BasecampPos) < 2.2f)
                    {
                        CompleteUniversalRescue(rescuerWr, casualtyWr, mem);
                    }
                    break;
                }
            }

            if (_activeRescue != null && !string.IsNullOrEmpty(_activeRescue.LastStatus)
                && Time.frameCount % 60 == 0)
                DigHoodLog.Push($"RESCUE | {_activeRescue.LastStatus}");
        }

        void TryStepRescueAvatar(int rosterIndex, WorkerAvatar avatar, Vector2 target, float speedMul)
        {
            if (avatar == null || _world == null) return;
            if (rosterIndex < 0 || rosterIndex >= _personNav.Length) return;
            float speed = WorkerLocomotion.WalkSpeedAt(
                FindCrewWorker(avatar.WorkerId), _world, avatar.PresencePosition,
                AvatarCommuteRadius, roleBias: 1f, carriedLoad01: speedMul < 0.5f ? 0.55f : 0.15f,
                isMoving: true) * Mathf.Max(0.35f, speedMul);
            if (_personNav[rosterIndex] == null) return;
            _personNav[rosterIndex].CampReturnMode = false;
            _personNav[rosterIndex].Follow(
                avatar.PresencePosition,
                target,
                speed,
                AvatarCommuteRadius,
                face: _ => { },
                tryStep: (dir, step) => PersonAvatarTryStep(avatar, dir, step));
        }

        WorkerRuntime PickUniversalRescuer(WorkerRuntime casualty)
        {
            if (casualty == null || _crewWorkers == null) return null;
            Vector2 cPos = CrewWorldPos(casualty);
            WorkerRuntime best = null;
            float bestScore = -1f;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null || wr.WorkerId == casualty.WorkerId) continue;
                if (!WorkerRescue.CanAcceptRescueDuty(wr)) continue;
                if (wr.Priorities != null && wr.Priorities.IsOff(WorkerGenericTaskIds.Rescue))
                    continue;
                // Must be on shift and arrived (same gate as work — not JobType)
                if (_crewPhase != CrewPhase.OnShift && _crewPhase != CrewPhase.HeadingOut)
                    continue;
                var L = _dayTracker.Get(wr.WorkerId);
                if (L == null || !L.ArrivedWork) continue;
                // Seeking care at camp: Kit may still leave to rescue
                if (wr.CampBody != null && wr.CampBody.SeekingStewardCare
                    && wr.State != null && wr.State.NeedsCare
                    && wr.WorkerId != (_workerSteward != null ? _workerSteward.WorkerId : -1))
                    continue;

                Vector2 rPos = CrewWorldPos(wr);
                // Rescuer cut off from camp AND no path to casualty → cannot help yet
                bool pathCas = _collapse != null && _collapse.HasOpenTunnelPath(rPos, cPos);
                if (!pathCas && _collapse != null && !_collapse.IsReachableFromCamp(rPos))
                    continue;

                var asg = _assignments.GetAssignment(wr.WorkerId);
                var job = asg != null ? asg.JobType : JobType.Unassigned;
                float score = WorkerRescue.ScoreCandidate(wr, job, rPos, cPos, pathCas);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = wr;
                }
            }
            return best;
        }

        void BeginUniversalRescue(WorkerRuntime rescuer, WorkerRuntime casualty)
        {
            if (rescuer == null || casualty == null) return;

            // Casualty: yield host so dig/haul does not continue while body lies injured
            LeaveHostForPersonTask(casualty, "rescue casualty leave host");
            if (casualty.CampBody != null)
            {
                casualty.CampBody.BeingRescued = true;
                casualty.CampBody.InjuryReturnActive = false;
            }

            LeaveHostForPersonTask(rescuer, "rescue duty leave host");
            if (rescuer.CampBody != null)
            {
                rescuer.CampBody.RescueDutyActive = true;
                rescuer.CampBody.RescueReturning = false;
                rescuer.CampBody.SeekingStewardCare = false;
            }

            _activeRescue = new RescueMission
            {
                MissionId = _nextRescueMissionId++,
                RescuerId = rescuer.WorkerId,
                CasualtyId = casualty.WorkerId,
                Phase = RescueMissionPhase.Approach,
                AssistHoursLeft = WorkerRescue.AssistDurationHours(rescuer),
                StartedGameHours = _absoluteGameHours,
                LastStatus = $"RESCUE → {casualty.DisplayName}",
            };
            _activeRescue.ContributorIds.Add(rescuer.WorkerId);

            DigHoodLog.Push(
                $"RESCUE | {rescuer.DisplayName} accepted {WorkerRescue.TaskId} for {casualty.DisplayName} "
                + $"(job was {GetAssignmentJob(rescuer)} — specialization yielded, not permission)");
            if (rescuer.Priorities != null)
            {
                rescuer.Priorities.ActiveTaskId = WorkerGenericTaskIds.Rescue;
                rescuer.Priorities.ActiveTaskStartedGameHours = _absoluteGameHours;
                rescuer.Priorities.LastResolveReason = "emergency rescue";
            }
        }

        void CompleteUniversalRescue(WorkerRuntime rescuer, WorkerRuntime casualty, SocialMemoryStore mem)
        {
            if (_collapse == null || rescuer == null || casualty == null) return;
            _collapse.FinalizeRescueAtCamp(rescuer, casualty, mem, out _);

            if (rescuer.CampBody != null)
            {
                rescuer.CampBody.RescueDutyActive = false;
                rescuer.CampBody.RescueReturning = true;
            }

            // Park casualty at camp slot — seeking Steward (no host snap)
            int cIdx = CrewRosterIndex(casualty);
            var cAv = _presence.Get(casualty.WorkerId);
            if (cAv != null)
            {
                Vector2 slot = cIdx >= 0 ? CampSlot(cIdx) : CampNavDestination;
                cAv.SetPresencePosition(slot);
                cAv.ClearFollowing();
                cAv.Show();
            }

            DigHoodLog.Push(
                $"RESCUE | COMPLETE — {casualty.DisplayName} SeekingStewardCare; "
                + $"{rescuer.DisplayName} returning to assignment");
            _activeRescue = null;
        }

        void AbortRescueMission(string reason)
        {
            if (_activeRescue == null) return;
            var r = FindCrewWorker(_activeRescue.RescuerId);
            var c = FindCrewWorker(_activeRescue.CasualtyId);
            if (r?.CampBody != null)
            {
                r.CampBody.RescueDutyActive = false;
                if (!r.CampBody.RescueReturning)
                    r.CampBody.RescueReturning = true;
            }
            if (c?.CampBody != null)
                c.CampBody.BeingRescued = false;
            DigHoodLog.Push($"RESCUE | aborted — {reason}");
            _activeRescue = null;
        }

        void TickRescueReturning(float hoursDelta)
        {
            if (_crewWorkers == null) return;
            EnsurePersonNav();
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr?.CampBody == null || !wr.CampBody.RescueReturning || !wr.IsAlive)
                    continue;
                if (wr.CampBody.RescueDutyActive) continue;

                var asg = _assignments.GetAssignment(wr.WorkerId);
                var av = _presence.Get(wr.WorkerId);
                if (av == null)
                {
                    wr.CampBody.RescueReturning = false;
                    continue;
                }

                if (asg == null || asg.JobType == JobType.Unassigned)
                {
                    wr.CampBody.RescueReturning = false;
                    continue;
                }

                Vector2 target = GetProviderExitPoint(asg.JobType);
                float arrive = 0.55f;
                if ((target - av.PresencePosition).sqrMagnitude <= arrive * arrive)
                {
                    wr.CampBody.RescueReturning = false;
                    BindHost(asg.JobType, wr);
                    NotifyProviderAssigned(asg.JobType, wr);
                    SeatAvatarAtWork(wr, av);
                    DigHoodLog.Push($"RESCUE | {wr.DisplayName} resumed {asg.JobType}");
                    continue;
                }

                float mul = WorkerRescue.ApproachSpeedMul(wr);
                TryStepRescueAvatar(i, av, target, mul);
                wr.State?.SpendStamina(
                    WorkerRescue.StaminaTaxPerGameHour(wr, false) * hoursDelta * 6f);
            }
        }

        int CrewRosterIndex(WorkerRuntime wr)
        {
            if (wr == null || _crewWorkers == null) return -1;
            for (int i = 0; i < _crewWorkers.Length; i++)
                if (_crewWorkers[i] != null && _crewWorkers[i].WorkerId == wr.WorkerId)
                    return i;
            return -1;
        }

        void TickCrewPhase()
        {
            if (_sleepCamp == null || _world == null) return;
            EnsurePersonNav();
            if (_crewPhase == CrewPhase.HeadingHome)
            {
                _commuteTimer += Time.deltaTime;
                Vector2 door = CampNavDestination;
                bool all = true;
                if (_crewWorkers != null)
                {
                    for (int i = 0; i < _crewWorkers.Length; i++)
                    {
                        var wr = _crewWorkers[i];
                        if (wr == null || !wr.IsAlive) { _personArrived[i] = true; continue; }
                        // Trapped / incapacitated: NEVER teleport home — stay at physical site
                        if (wr.State != null && wr.State.Incapacitated)
                        {
                            _personArrived[i] = true;
                            DigHoodLog.Push($"COMMUTE | {wr.DisplayName} cannot reach camp — incapacitated");
                            continue;
                        }
                        if (wr.State != null && wr.State.TrappedFromCamp)
                        {
                            _personArrived[i] = true;
                            DigHoodLog.Push($"COMMUTE | {wr.DisplayName} cannot reach camp — trapped");
                            continue;
                        }
                        var av = _presence.Get(wr.WorkerId);
                        Vector2 target = CampSlot(i);
                        all &= StepPersonCommute(i, av, target);
                        if (_personArrived[i])
                            EnterCampHouse(wr, av);
                    }
                }
                // Physical commute — stranded/late stay put (no soft-arrive teleport).
                bool anyStranded = false;
                bool anyStillWalking = false;
                for (int i = 0; i < _personArrived.Length; i++)
                {
                    if (_personArrived[i]) continue;
                    if (_personStranded[i]) anyStranded = true;
                    else anyStillWalking = true;
                }
                if (all)
                    EnterCampEvening();
                else if (!anyStillWalking && anyStranded)
                {
                    DigHoodLog.Push(
                        "COMMUTE | Some crew stranded en route to camp — evening without teleport");
                    EnterCampEvening();
                }
                else if (_commuteTimer >= CommuteEmergencyTimeoutSec)
                {
                    DigHoodLog.Push(
                        "COMMUTE | Evening phase advance — late/stranded remain in field (no teleport)");
                    EnterCampEvening();
                }
            }
            else if (_crewPhase == CrewPhase.CampEvening)
            {
                // Late homecomers still walking into camp
                TickLateCommuteHome();
                if (_absoluteGameHours >= _campEveningEndAbsolute
                    || HoursUntilMorning(_gameHour) <= 2.2f)
                    EnterSleep();
            }
            else if (_crewPhase == CrewPhase.HeadingOut)
            {
                _commuteTimer += Time.deltaTime;
                bool all = true;
                if (_crewWorkers != null)
                {
                    for (int i = 0; i < _crewWorkers.Length; i++)
                    {
                        var wr = _crewWorkers[i];
                        if (wr == null || !wr.IsAlive) { _personArrived[i] = true; continue; }
                        if (wr.State != null && wr.State.Incapacitated)
                        {
                            _personArrived[i] = true;
                            continue;
                        }
                        if (wr.State != null && wr.State.TrappedFromCamp)
                        {
                            // Stay at physical site — do not invent a path through debris
                            _personArrived[i] = true;
                            continue;
                        }
                        var av = _presence.Get(wr.WorkerId);
                        Vector2 target = GetInstrumentMorningPost(wr);
                        bool wasArrived = _personArrived[i];
                        all &= StepPersonCommute(i, av, target);
                        var L = _dayTracker.Get(wr.WorkerId);
                        if (_personArrived[i])
                        {
                            L.ArrivedWork = true;
                            if (!wasArrived)
                                SeatAvatarAtWork(wr, av);
                        }
                    }
                }
                bool anyStranded = false;
                bool anyStillWalking = false;
                for (int i = 0; i < _personArrived.Length; i++)
                {
                    if (_personArrived[i]) continue;
                    if (_personStranded[i]) anyStranded = true;
                    else anyStillWalking = true;
                }
                if (all)
                    EnterOnShift();
                else if (!anyStillWalking && anyStranded)
                {
                    DigHoodLog.Push(
                        "COMMUTE | Some crew stranded en route to work — shift live without teleport");
                    EnterOnShift();
                }
                else if (_commuteTimer >= CommuteEmergencyTimeoutSec)
                {
                    DigHoodLog.Push(
                        "COMMUTE | Shift phase advance — late/stranded keep physical position (no teleport)");
                    EnterOnShift();
                }
            }
            else if (_crewPhase == CrewPhase.OnShift)
            {
                TickLateCommuteToWork();
            }
        }

        void TickLateCommuteToWork()
        {
            if (_crewWorkers == null) return;
            EnsurePersonNav();
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null || !wr.IsAlive) continue;
                var L = _dayTracker.Get(wr.WorkerId);
                if (L.ArrivedWork) continue;
                if (wr.State != null && wr.State.Incapacitated) continue;
                // Stay at physical site — do not invent a path through debris
                if (wr.State != null && wr.State.TrappedFromCamp) continue;
                if (_personStranded != null && i < _personStranded.Length && _personStranded[i])
                    continue;
                var av = _presence.Get(wr.WorkerId);
                if (av == null) continue;
                bool was = _personArrived[i];
                StepPersonCommute(i, av, GetInstrumentMorningPost(wr));
                if (_personArrived[i])
                {
                    L.ArrivedWork = true;
                    if (!was) SeatAvatarAtWork(wr, av);
                }
            }
        }

        void TickLateCommuteHome()
        {
            if (_crewWorkers == null) return;
            EnsurePersonNav();
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null || !wr.IsAlive) continue;
                var L = _dayTracker.Get(wr.WorkerId);
                if (L.ArrivedHome) continue;
                if (wr.State != null && (wr.State.Incapacitated || wr.State.TrappedFromCamp))
                    continue;
                if (_personStranded != null && i < _personStranded.Length && _personStranded[i])
                    continue;
                var av = _presence.Get(wr.WorkerId);
                if (av == null) continue;
                StepPersonCommute(i, av, CampSlot(i));
                if (_personArrived[i])
                    EnterCampHouse(wr, av);
            }
        }

        /// <summary>
        /// Arrive at equipment: excavator → enter cabin (hide avatar);
        /// other jobs → same persistent avatar stays visible; host person sprite stays off.
        /// </summary>
        void SeatAvatarAtWork(WorkerRuntime wr, WorkerAvatar av)
        {
            if (wr == null || av == null) return;
            var asg = _assignments.GetAssignment(wr.WorkerId);
            if (asg == null || asg.JobType == JobType.Unassigned) return;

            Vector2 op = GetProviderOperatePoint(asg.JobType);
            av.SetPresencePosition(op);
            PersistentWorkerBody.SetOperatorBodyVisibleForJob(
                asg.JobType, _worker, _prospector, _hauler, _refiner, _engineer, _steward, false);

            if (PersistentWorkerBody.IsMachineCabinJob(asg.JobType))
                ExcavatorCabin.Enter(av, asg.ProviderId);
            else
            {
                av.SetFollowing(asg.ProviderId);
                av.Show();
            }
            av.SetDevForceShowHidden(_devForceShowHiddenAvatars);
        }

        /// <summary>
        /// DEV/audit only — never call from normal commute. Logs as relocation.
        /// Skips Incapacitated (never soft-teleport through blocked tunnels).
        /// </summary>
        void SoftArriveAllWork()
        {
            if (_crewWorkers == null) return;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null || !wr.IsAlive) continue;
                // Never soft-teleport incap / trapped through blocked tunnels
                if (wr.State != null && (wr.State.Incapacitated || wr.State.TrappedFromCamp))
                {
                    _personArrived[i] = true;
                    continue;
                }
                var av = _presence.Get(wr.WorkerId);
                if (av == null) continue;
                RelocateAvatar(av, GetInstrumentMorningPost(wr), "DEV soft-arrive work", "SoftArriveAllWork");
                if (wr.State != null) wr.State.TrappedFromCamp = false;
                _personArrived[i] = true;
                _dayTracker.Get(wr.WorkerId).ArrivedWork = true;
            }
        }

        void RelocateAvatar(WorkerAvatar av, Vector2 to, string reason, string source)
        {
            if (av == null) return;
            Vector2 from = av.PresencePosition;
            av.SetPresencePosition(to);
            WorkerRelocationLog.Report(av.DisplayName, from, to, reason, source);
        }

        Vector2 CampSlot(int rosterIndex)
        {
            // Walk into the tent house — interior bed slots, not yard scatter
            Vector2 door = CampNavDestination;
            Vector2 inside = door + CampHouseInteriorOffsets[rosterIndex % CampHouseInteriorOffsets.Length];
            return SnapPostToTunnel(inside, AvatarCommuteRadius * 0.55f);
        }

        void EnterCampHouse(WorkerRuntime wr, WorkerAvatar av)
        {
            if (av == null) return;
            av.ClearFollowing();
            av.Hide();
            av.SetDevForceShowHidden(_devForceShowHiddenAvatars);
            if (wr != null)
                _dayTracker.Get(wr.WorkerId).ArrivedHome = true;
        }

        /// <summary>
        /// Keep soft-arrive / idle posts from collapsing onto one SnapPost cell (yard pile-up).
        /// </summary>
        Vector2 SpreadOpenSlot(Vector2 desired, int rosterIndex)
        {
            Vector2 basePos = SnapPostToTunnel(desired, AvatarCommuteRadius);
            float ang = (rosterIndex * 2.399963f) + rosterIndex * 0.35f; // golden-angle-ish
            float rad = 0.55f + (rosterIndex % 4) * 0.35f;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float a = ang + attempt * 0.7f;
                float r = rad + attempt * 0.2f;
                Vector2 cand = basePos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                var c = _world.WorldToCell(cand);
                if (_world.IsTunnelOpen(c.x, c.y) && !_world.CircleHitsSolid(cand, AvatarCommuteRadius * 0.7f))
                    return cand;
            }
            return basePos;
        }

        Vector2 SnapPostToTunnel(Vector2 pos, float bodyR)
        {
            var c = _world.WorldToCell(pos);
            if (_world.IsTunnelOpen(c.x, c.y) && !_world.CircleHitsSolid(pos, bodyR * 0.7f))
                return pos;
            Vector2 p = pos;
            return TrySnapToNearestTunnel(ref p, bodyR) ? p : pos;
        }

        /// <summary>
        /// Refresh provider location bookmarks (machines/stations stay put).
        /// Person morning destinations resolve via assignment → provider, not these alone.
        /// </summary>
        void RefreshProviderWorkPosts()
        {
            if (_hasExcavatorDigResume)
                _providerPostExcavator = SnapPostToTunnel(_excavatorDigResume, _providerFootprintR[0]);
            else if (_worker != null)
                _providerPostExcavator = SnapPostToTunnel(_worker.Position, _providerFootprintR[0]);
            else
                _providerPostExcavator = SnapPostToTunnel(_world.CellCenter(StartX, StartY), _providerFootprintR[0]);

            if (_prospector != null && _prospector.WorkMode == ProspectorWorkMode.Investigate
                && _prospectWorkstation == null)
            {
                _providerPostProspector = SnapPostToTunnel(
                    _prospector.Investigation.ResumeWorldPosition, AvatarCommuteRadius);
            }
            else if (_prospectWorkstation != null)
            {
                _providerPostProspector = SnapPostToTunnel(
                    _prospectWorkstation.ProspectorStand, AvatarCommuteRadius);
            }
            else if (_prospector != null)
                _providerPostProspector = SnapPostToTunnel(_prospector.Position, AvatarCommuteRadius);
            else
                _providerPostProspector = SnapPostToTunnel(_world.CellCenter(StartX - 4, StartY - 1), AvatarCommuteRadius);

            // Prefer live hosts overnight — instruments stay where they were left
            _providerPostHauler = SnapPostToTunnel(
                _hauler != null ? _hauler.Position : (_yard != null ? _yard.DropPoint : BasecampPos),
                AvatarCommuteRadius);
            _providerPostRefiner = SnapPostToTunnel(
                _refiner != null ? _refiner.WorkPoint : BasecampPos,
                AvatarCommuteRadius);
            _providerPostSteward = SnapPostToTunnel(
                _steward != null ? _steward.Position
                    : (_sleepCamp != null ? _sleepCamp.TentDoor + new Vector2(0.95f, -0.85f) : BasecampPos),
                AvatarCommuteRadius);
            _providerPostEngineer = SnapPostToTunnel(
                _engineer != null ? _engineer.Position
                    : (_yard != null ? _yard.DropPoint + new Vector2(0.55f, 0.35f) : BasecampPos),
                AvatarCommuteRadius);
        }

        /// <summary>
        /// Exact instrument / work-post for morning commute — no ring spread.
        /// Spread was pushing targets off reachable cells so crew never "arrived".
        /// </summary>
        Vector2 GetInstrumentMorningPost(WorkerRuntime wr)
        {
            if (wr == null) return CampNavDestination;
            var asg = _assignments.GetAssignment(wr.WorkerId);
            int roster = _crewWorkers != null ? System.Array.IndexOf(_crewWorkers, wr) : 0;
            if (roster < 0) roster = 0;
            if (asg == null || asg.JobType == JobType.Unassigned)
                return CampSlot(roster);

            if (asg.JobType == JobType.Excavation && _hasExcavatorDigResume)
                return SnapPostToTunnel(_excavatorDigResume, AvatarCommuteRadius);
            if (asg.JobType == JobType.Excavation && _worker != null)
                return SnapPostToTunnel(_worker.Position, AvatarCommuteRadius);

            return asg.JobType switch
            {
                JobType.Prospecting => _providerPostProspector,
                JobType.Excavation => _providerPostExcavator,
                JobType.Hauling => _providerPostHauler,
                JobType.Refining => _providerPostRefiner,
                JobType.Engineering => _providerPostEngineer,
                JobType.Steward => _providerPostSteward,
                _ => CampNavDestination,
            };
        }

        /// <summary>Assigned jobs → instruments; unassigned → spread camp idle.</summary>
        Vector2 GetMorningPersonDestination(WorkerRuntime wr, int rosterIndex)
        {
            if (wr == null) return CampSlot(rosterIndex);
            var asg = _assignments.GetAssignment(wr.WorkerId);
            if (asg == null || asg.JobType == JobType.Unassigned)
                return CampSlot(rosterIndex);
            return GetInstrumentMorningPost(wr);
        }

        void EnsureCrewCommuteCapacity()
        {
            int n = _crewWorkers != null ? _crewWorkers.Length : 0;
            if (n < 1) n = 1;
            if (_personNav != null && _personNav.Length == n
                && _personArrived != null && _personArrived.Length == n
                && _personStuckTimer != null && _personStuckTimer.Length == n)
                return;
            var oldNav = _personNav;
            _personNav = new ExcavatedPathfinder[n];
            _personArrived = new bool[n];
            _personStranded = new bool[n];
            _personStuckTimer = new float[n];
            _personLastCommutePos = new Vector2[n];
            if (_personLateral == null || _personLateral.Length < n)
            {
                var lat = new float[n];
                for (int i = 0; i < n; i++)
                    lat[i] = i < (_personLateral?.Length ?? 0)
                        ? _personLateral[i]
                        : ((i % 2 == 0) ? -0.02f : 0.025f) * (1f + i * 0.1f);
                _personLateral = lat;
            }
            for (int i = 0; i < n; i++)
            {
                if (oldNav != null && i < oldNav.Length && oldNav[i] != null)
                    _personNav[i] = oldNav[i];
            }
        }

        void EnsurePersonNav()
        {
            EnsureCrewCommuteCapacity();
            TunnelNavGrid.DebugLog = navDebugLog;
            TunnelPathfinder.DebugLog = navDebugLog;
            TunnelPathfinder.DebugDraw = navDebugDraw;
            for (int i = 0; i < _personNav.Length; i++)
            {
                if (_personNav[i] == null)
                {
                    _personNav[i] = new ExcavatedPathfinder(_world, AvatarCommuteRadius);
                    _personNav[i].LateralOffset = _personLateral[i % _personLateral.Length];
                }
                _personNav[i].SetAgentRadius(AvatarCommuteRadius);
            }
        }

        bool StepPersonCommute(int rosterIndex, WorkerAvatar avatar, Vector2 target)
        {
            if (avatar == null) { _personArrived[rosterIndex] = true; return true; }
            if (_personArrived[rosterIndex]) return true;
            if (_personStranded[rosterIndex]) return false;

            Vector2 p = avatar.PresencePosition;
            float arrive = CommuteArriveRadius;
            if ((target - p).sqrMagnitude <= arrive * arrive)
            {
                avatar.SetPresencePosition(target);
                _personArrived[rosterIndex] = true;
                _personStuckTimer[rosterIndex] = 0f;
                _personNav[rosterIndex]?.Invalidate();
                return true;
            }

            var cell = _world.WorldToCell(p);
            if (!_world.IsTunnelOpen(cell.x, cell.y))
            {
                if (TrySnapToNearestTunnel(ref p, AvatarCommuteRadius))
                    avatar.SetPresencePosition(p);
            }

            float speed = WorkerLocomotion.WalkSpeedAt(
                FindCrewWorker(avatar.WorkerId),
                _world,
                p,
                AvatarCommuteRadius,
                roleBias: 1f,
                carriedLoad01: 0f,
                isMoving: true);
            bool done = _personNav[rosterIndex].Follow(
                p,
                target,
                speed,
                AvatarCommuteRadius,
                face: _ => { },
                tryStep: (dir, step) => PersonAvatarTryStep(avatar, dir, step));

            Vector2 after = avatar.PresencePosition;
            float moved = (after - _personLastCommutePos[rosterIndex]).magnitude;
            if (moved < 0.012f)
                _personStuckTimer[rosterIndex] += Time.deltaTime;
            else
                _personStuckTimer[rosterIndex] = 0f;
            _personLastCommutePos[rosterIndex] = after;

            if (_personNav[rosterIndex] != null && _personNav[rosterIndex].Stranded)
            {
                _personStranded[rosterIndex] = true;
                DigHoodLog.Push(
                    $"CREW {avatar.DisplayName} | STRANDED — no path to destination");
                return false;
            }

            // Stuck walking in place — remain blocked (gameplay info). Never soft-arrive teleport.
            if (_personStuckTimer[rosterIndex] >= CommuteStuckSec)
            {
                if (!_personStranded[rosterIndex])
                {
                    _personStranded[rosterIndex] = true;
                    DigHoodLog.Push(
                        $"CREW {avatar.DisplayName} | PATH BLOCKED — stuck en route (no teleport)");
                }
                return false;
            }

            if (done)
            {
                // Small arrive settle within radius — not a map teleport
                avatar.SetPresencePosition(target);
                _personArrived[rosterIndex] = true;
                _personStuckTimer[rosterIndex] = 0f;
                _personNav[rosterIndex]?.Invalidate();
            }
            return _personArrived[rosterIndex];
        }

        bool PersonAvatarTryStep(WorkerAvatar avatar, Vector2 dir, float step)
        {
            if (avatar == null || dir.sqrMagnitude < 0.00001f) return false;
            Vector2 next = avatar.PresencePosition + dir.normalized * step;
            if (_world.CircleHitsSolid(next, AvatarCommuteRadius * 0.85f)) return false;
            avatar.SetPresencePosition(next);
            return true;
        }

        bool TrySnapToNearestTunnel(ref Vector2 p, float bodyR)
        {
            var c = _world.WorldToCell(p);
            for (int r = 1; r <= 8; r++)
            {
                for (int oy = -r; oy <= r; oy++)
                for (int ox = -r; ox <= r; ox++)
                {
                    if (Mathf.Abs(ox) != r && Mathf.Abs(oy) != r) continue;
                    int x = c.x + ox, y = c.y + oy;
                    if (!_world.IsTunnelOpen(x, y)) continue;
                    Vector2 cand = _world.CellCenter(x, y);
                    if (_world.CircleHitsSolid(cand, bodyR * 0.7f)) continue;
                    p = cand;
                    return true;
                }
            }
            return false;
        }

        void BeginHeadingHome()
        {
            _crewPhase = CrewPhase.HeadingHome;
            _commuteTimer = 0f;
            _campEveningEndAbsolute = -1f;
            // Keep camp pad open for knock-off walking
            _collapse?.ClearDebrisNearCamp(TunnelCollapseSystem.CampSafeRadiusWorld);
            EnsurePersonNav();
            for (int i = 0; i < _personArrived.Length; i++)
            {
                _personArrived[i] = false;
                _personStranded[i] = false;
                if (_personStuckTimer != null && i < _personStuckTimer.Length)
                    _personStuckTimer[i] = 0f;
            }
            for (int i = 0; i < _personNav.Length; i++)
            {
                if (_personNav[i] == null) continue;
                _personNav[i].CampReturnMode = true;
                _personNav[i].LateralOffset = _personLateral[i % _personLateral.Length];
                _personNav[i].Invalidate();
            }

            // Remember dig face for machine bookmark — excavator stays put and goes idle
            if (_worker != null)
            {
                _excavatorDigResume = SnapPostToTunnel(
                    _worker.CaptureShiftBreakBookmark(), _providerFootprintR[0]);
                _hasExcavatorDigResume = true;
                YieldHost(JobType.Excavation);
                DigHoodLog.Push(
                    $"SHIFT BREAK | Excavator idle @ {_excavatorDigResume.x:0.0},{_excavatorDigResume.y:0.0}");
            }
            // Park other instruments (stop active work presentation)
            YieldHost(JobType.Prospecting);
            YieldHost(JobType.Hauling);
            YieldHost(JobType.Refining);
            YieldHost(JobType.Engineering);
            YieldHost(JobType.Steward);

            // Persistent people leave equipment: excavator cabin exit; host person sprites stay off
            if (_crewWorkers != null)
            {
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    var wr = _crewWorkers[i];
                    if (wr == null) continue;
                    var av = _presence.Get(wr.WorkerId);
                    if (av == null) continue;
                    var asg = _assignments.GetAssignment(wr.WorkerId);
                    if (asg != null && asg.JobType != JobType.Unassigned)
                    {
                        PersistentWorkerBody.SetOperatorBodyVisibleForJob(
                            asg.JobType, _worker, _prospector, _hauler, _refiner, _engineer, _steward,
                            false);
                        if (PersistentWorkerBody.IsMachineCabinJob(asg.JobType) && _worker != null)
                        {
                            Vector2 exit = ExcavatorCabin.ExitWorld(_worker.Position);
                            float dExit = Vector2.Distance(av.PresencePosition, exit);
                            if (dExit > 0.85f && av.IsVisuallyHidden)
                                RelocateAvatar(av, exit, "shift-end excavator exit", "BeginHeadingHome");
                            ExcavatorCabin.Exit(av, _worker.Position);
                        }
                        else
                        {
                            Vector2 atHost = GetProviderOperatePoint(asg.JobType);
                            float d = Vector2.Distance(av.PresencePosition, atHost);
                            if (d > 0.85f)
                                RelocateAvatar(av, atHost, "shift-end at host", "BeginHeadingHome");
                            else
                                av.SetPresencePosition(atHost);
                            av.ClearFollowing();
                            av.Show();
                        }
                    }
                    else
                    {
                        av.ClearFollowing();
                        av.Show();
                    }
                    Vector2 p = av.PresencePosition;
                    var cell = _world != null ? _world.WorldToCell(p) : default;
                    if (_world != null && !_world.IsTunnelOpen(cell.x, cell.y))
                    {
                        if (TrySnapToNearestTunnel(ref p, AvatarCommuteRadius))
                            RelocateAvatar(av, p, "shift-end embedded in solid", "BeginHeadingHome");
                    }
                    if (_personLastCommutePos != null && i < _personLastCommutePos.Length)
                        _personLastCommutePos[i] = av.PresencePosition;
                    av.SetDevForceShowHidden(_devForceShowHiddenAvatars);
                    _dayTracker.Get(wr.WorkerId).ArrivedHome = false;
                }
            }

            _prospector?.SetRadar(false);
            DigHoodLog.Push("SHIFT END | People commute home — providers stay");
        }

        void ServeEveningMealIfNeeded()
        {
            if (_campLife == null || _campLife.MealServedTonight) return;
            var steward = _steward != null ? _steward.AssignedWorker : _workerSteward;
            if (steward == null)
                steward = FindWorkerAssignedTo(JobType.Steward);
            var quality = CampMealSystem.ResolveQuality(steward, _campLife, _campRng);
            SocialMemoryStore mem = _socialAura != null && _socialAura.IsBootstrapped
                ? _socialAura.Memory : null;
            CampMealSystem.ApplyMealToCrew(
                _crewWorkers, steward, _campLife, quality, mem, _absoluteGameHours, _campRng);
            _sleepCamp?.NotifyMealServed(quality);
            DigHoodLog.Push(
                $"MEAL | {_campLife.MealLabel} · hygiene {_campLife.HygieneLabel}"
                + (_campLife.LastSickCount > 0 ? $" · sick {_campLife.LastSickCount}" : ""));
        }

        void TickCampLifeBody(float gameHours)
        {
            if (gameHours <= 0f || _crewWorkers == null) return;
            // Natural hygiene drift when steward not actively cleaning (on-shift tick covers push)
            if (_crewPhase != CrewPhase.OnShift && _campLife != null)
                _campLife.DriftHygiene(gameHours, 0f);
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null || !wr.IsAlive || wr.CampBody == null) continue;
                var body = wr.CampBody;
                if (!body.ToiletTripActive)
                {
                    float rate = body.ToiletBuildPerGameHour()
                                 * WorkerCampBody.ToiletPersonalRateMul(wr.WorkerId);
                    body.ToiletNeed01 = Mathf.Clamp01(body.ToiletNeed01 + rate * gameHours);
                }
                body.TickStomach(gameHours);
                body.ApplyStomachMeterPressure(wr.State, gameHours);
                if (_crewPhase == CrewPhase.OnShift
                    && !body.ToiletTripActive
                    && (_toiletCamp == null || !_toiletCamp.IsOccupied))
                {
                    float thresh = WorkerCampBody.ToiletTripThreshold(wr.WorkerId);
                    bool urgent = body.ToiletUrgencyCritical
                                  || (body.ToiletUrgencyHigh && body.HasStomachUpset)
                                  || body.ToiletNeed01 >= thresh;
                    if (urgent)
                        BeginToiletTrip(wr);
                }
            }
        }

        void BeginToiletTrip(WorkerRuntime wr)
        {
            if (wr == null || wr.CampBody == null || !wr.IsAlive) return;
            if (_toiletCamp == null) return;
            if (_crewPhase != CrewPhase.OnShift && _crewPhase != CrewPhase.HeadingHome
                && _crewPhase != CrewPhase.CampEvening) return;
            var body = wr.CampBody;
            if (body.ToiletTripActive) return;
            // Single stall — wait your turn (others keep building need)
            if (_toiletCamp.IsOccupied && _toiletCamp.OccupantWorkerId != wr.WorkerId)
                return;
            body.ToiletTripActive = true;
            body.ToiletReturning = false;
            body.ToiletUseSecondsLeft = 0f;
            var av = _presence.Get(wr.WorkerId);
            if (av == null) return;
            var asg = _assignments.GetAssignment(wr.WorkerId);
            if (asg != null && asg.JobType != JobType.Unassigned)
            {
                PersistentWorkerBody.SetOperatorBodyVisibleForJob(
                    asg.JobType, _worker, _prospector, _hauler, _refiner, _engineer, _steward, false);
                if (PersistentWorkerBody.IsMachineCabinJob(asg.JobType) && _worker != null)
                {
                    _worker.ParkMachineIdle();
                    ExcavatorCabin.Exit(av, _worker.Position);
                }
                else
                {
                    Vector2 exit = GetProviderExitPoint(asg.JobType);
                    float d = Vector2.Distance(av.PresencePosition, exit);
                    if (d > 0.85f)
                        RelocateAvatar(av, exit, "toilet leave host", "BeginToiletTrip");
                    else
                        av.SetPresencePosition(exit);
                    av.ClearFollowing();
                    av.Show();
                }
                av.SetDevForceShowHidden(_devForceShowHiddenAvatars);
            }
            DigHoodLog.Push($"TOILET | {wr.DisplayName} heading to camp WC");
        }

        void TickToiletTrips(float gameHoursDelta)
        {
            if (_crewWorkers == null || _toiletCamp == null) return;
            float dt = gameHoursDelta * SecondsPerGameHour; // real seconds approx
            EnsurePersonNav();
            Vector2 door = SnapPostToTunnel(_toiletCamp.Door, AvatarCommuteRadius);
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null || !wr.IsAlive || wr.CampBody == null) continue;
                var body = wr.CampBody;
                if (!body.ToiletTripActive) continue;
                var av = _presence.Get(wr.WorkerId);
                if (av == null) { body.ToiletTripActive = false; continue; }

                if (body.ToiletUseSecondsLeft > 0f)
                {
                    // Inside stall — disappear
                    if (!av.IsVisuallyHidden)
                    {
                        av.SetPresencePosition(_toiletCamp.StallCenter);
                        av.Hide();
                    }
                    body.ToiletUseSecondsLeft -= dt;
                    if (body.ToiletUseSecondsLeft > 0f) continue;
                    _toiletCamp.Release(wr.WorkerId);
                    body.ToiletNeed01 = 0f;
                    body.LastToiletGameHours = _absoluteGameHours;
                    body.ToiletReturning = true;
                    av.ParkAt(door);
                    av.Show();
                    av.SetDevForceShowHidden(_devForceShowHiddenAvatars);
                    DigHoodLog.Push($"TOILET | {wr.DisplayName} walking back to post");
                    continue;
                }

                if (body.ToiletReturning)
                {
                    Vector2 backTarget = door;
                    var asg = _assignments.GetAssignment(wr.WorkerId);
                    if (_crewPhase == CrewPhase.OnShift && asg != null && asg.JobType != JobType.Unassigned)
                        backTarget = GetProviderOperatePoint(asg.JobType);
                    else
                        backTarget = CampSlot(i);
                    float arriveR = 0.45f;
                    Vector2 bp = av.PresencePosition;
                    if ((backTarget - bp).sqrMagnitude <= arriveR * arriveR)
                    {
                        body.ToiletReturning = false;
                        body.ToiletTripActive = false;
                        if (_crewPhase == CrewPhase.OnShift && asg != null && asg.JobType != JobType.Unassigned)
                        {
                            _dayTracker.Get(wr.WorkerId).ArrivedWork = true;
                            SeatAvatarAtWork(wr, av);
                        }
                        else if (_crewPhase == CrewPhase.HeadingHome || _crewPhase == CrewPhase.CampEvening)
                            EnterCampHouse(wr, av);
                        DigHoodLog.Push($"TOILET | {wr.DisplayName} returned");
                        continue;
                    }
                    float spd = WorkerLocomotion.WalkSpeedAt(
                        wr, _world, bp, AvatarCommuteRadius, 1f, 0f, true);
                    if (i < _personNav.Length && _personNav[i] != null)
                    {
                        _personNav[i].Follow(
                            bp, backTarget, spd, AvatarCommuteRadius,
                            face: _ => { },
                            tryStep: (dir, step) => PersonAvatarTryStep(av, dir, step));
                    }
                    else
                        av.SetPresencePosition(Vector2.MoveTowards(bp, backTarget, spd * Time.deltaTime));
                    continue;
                }

                // Walk to toilet door — queue if occupied
                float arrive = 0.28f;
                Vector2 p = av.PresencePosition;
                if ((door - p).sqrMagnitude <= arrive * arrive)
                {
                    if (_toiletCamp.IsOccupied && _toiletCamp.OccupantWorkerId != wr.WorkerId)
                    {
                        av.SetPresencePosition(door + new Vector2(-0.22f, -0.12f));
                        continue;
                    }
                    if (!_toiletCamp.TryOccupy(wr.WorkerId))
                        continue;
                    av.SetPresencePosition(_toiletCamp.StallCenter);
                    av.Hide();
                    av.SetDevForceShowHidden(_devForceShowHiddenAvatars);
                    body.ToiletUseSecondsLeft = 4.2f + (wr.WorkerId % 5) * 0.55f;
                    DigHoodLog.Push($"TOILET | {wr.DisplayName} entered stall");
                    continue;
                }
                float speed = WorkerLocomotion.WalkSpeedAt(
                    wr, _world, p, AvatarCommuteRadius, 1f, 0f, true);
                if (i < _personNav.Length && _personNav[i] != null)
                {
                    _personNav[i].Follow(
                        p, door, speed, AvatarCommuteRadius,
                        face: _ => { },
                        tryStep: (dir, step) => PersonAvatarTryStep(av, dir, step));
                }
                else
                {
                    Vector2 step = Vector2.MoveTowards(p, door, speed * Time.deltaTime);
                    av.SetPresencePosition(step);
                }
            }
        }

        void DrawShiftPlannerPanel()
        {
            const float pw = 280f;
            float ph = 268f;
            float bx = Screen.width - pw - 12f - HudToolStripReserve;
            if (bx < 160f) bx = 160f;
            var r = new Rect(bx, 96f, pw, ph);
            DrawCyberPanel(r, lit: true, accentOverride: UiCyan);
            Block(r);
            float x = r.x + 10f;
            float y = r.y + 6f;
            float inner = pw - 20f;
            GUI.Label(new Rect(x, y, inner, 12f), "SHIFT PLANNER", LabelStyle(8, UiMute, bold: true));
            y += 16f;

            string startS = $"{Mathf.FloorToInt(_shiftPlanner.ShiftStartHour):00}:00";
            string endS = $"{Mathf.FloorToInt(_shiftPlanner.ShiftEndHour):00}:00";
            GUI.Label(new Rect(x, y, inner, 14f),
                $"START  {startS}    END  {endS}",
                LabelStyle(10, UiWhite, bold: true));
            y += 18f;

            float bw = (inner - 8f) / 4f;
            float bh = 20f;
            void B(Rect rr, string lab, System.Action act)
            {
                Block(rr);
                if (DrawCyberButton(rr, lab, selected: false, accent: UiCyan))
                    act();
            }
            B(new Rect(x, y, bw, bh), "S−1", () => _shiftPlanner.NudgeStart(-1f));
            B(new Rect(x + bw + 2f, y, bw, bh), "S+1", () => _shiftPlanner.NudgeStart(1f));
            B(new Rect(x + 2f * (bw + 2f), y, bw, bh), "E−1", () => _shiftPlanner.NudgeEnd(-1f));
            B(new Rect(x + 3f * (bw + 2f), y, bw, bh), "E+1", () => _shiftPlanner.NudgeEnd(1f));
            y += bh + 6f;

            B(new Rect(x, y, bw, bh), "6H", () => _shiftPlanner.SetWorkLength(6f));
            B(new Rect(x + bw + 2f, y, bw, bh), "8H", () => _shiftPlanner.SetWorkLength(8f));
            B(new Rect(x + 2f * (bw + 2f), y, bw, bh), "10H", () => _shiftPlanner.SetWorkLength(10f));
            B(new Rect(x + 3f * (bw + 2f), y, bw, bh), "12H", () => _shiftPlanner.SetWorkLength(12f));
            y += bh + 10f;

            float work = _shiftPlanner.PlannedWorkHours;
            Color bandCol = work <= 8.01f ? UiGreen : work <= 10.01f ? UiAmber : UiRed;
            GUI.Label(new Rect(x, y, inner, 12f),
                $"{work:0.#} HOURS  ·  {_shiftPlanner.WorkBandLabel}",
                LabelStyle(9, bandCol, bold: true));
            y += 14f;
            GUI.Label(new Rect(x, y, inner, 28f), _shiftPlanner.WorkBandWarning,
                LabelStyle(8, UiDim));
            y += 30f;

            GUI.Label(new Rect(x, y, inner, 12f),
                $"Work {_shiftPlanner.FormatHours(work)}  ·  Camp ~{_shiftPlanner.FormatHours(_shiftPlanner.ExpectedFreeCampHours)}",
                LabelStyle(8, UiWhite));
            y += 14f;
            GUI.Label(new Rect(x, y, inner, 12f),
                $"Commute ~{_shiftPlanner.FormatHours(_shiftPlanner.ExpectedCommuteOneWayHours * 2f)} round-trip",
                LabelStyle(8, UiDim));
            y += 14f;
            string sleepLines = _shiftPlanner.SleepEstimateLine();
            foreach (var line in sleepLines.Split('\n'))
            {
                GUI.Label(new Rect(x, y, inner, 12f), line,
                    LabelStyle(8, line.StartsWith("Long") || line.StartsWith("Short") ? UiAmber : UiCyan));
                y += 13f;
            }

            y += 6f;
            var sel = FindCrewWorker(_selectedWorkerId);
            if (sel != null)
            {
                var mgr = _managerRels.Get(sel.WorkerId);
                GUI.Label(new Rect(x, y, inner, 11f),
                    $"MGR · {sel.DisplayName}  T{mgr.Trust:0} R{mgr.Respect:0} Res{mgr.Resentment:0}",
                    LabelStyle(7, UiMute));
            }
        }

        void DrawPrioritiesPanel()
        {
            if (_crewWorkers == null) return;
            _prioHoverTaskTip = null;
            _prioHoverCellTip = null;
            _priorities.BindHosts(
                _world, _collapse, _mineInfra, _worker, _hauler, _refiner, _engineer,
                _prospector, _scanHistory != null ? _scanHistory.Analyst : null,
                _steward, _campLife, _crewWorkers, CrewWorldPos, BasecampPos);

            Event e = Event.current;
            int nWorkers = 0;
            for (int i = 0; i < _crewWorkers.Length; i++)
                if (_crewWorkers[i] != null) nWorkers++;

            var selWr = FindCrewWorker(_prioUiSelectedWorkerId);
            if (selWr == null && _crewWorkers.Length > 0)
            {
                selWr = _crewWorkers[0];
                if (selWr != null) _prioUiSelectedWorkerId = selWr.WorkerId;
            }
            var selJob = GetAssignmentJob(selWr);
            WorkTaskRegistry.CollectForJob(selJob, _prioJobTaskBuf);

            const float rowH = 30f;
            const float taskRowH = 32f;
            const float dutyRowH = 32f;
            const float sectionGap = 18f;
            float assignH = 20f + nWorkers * rowH;
            float detailH = 22f + Mathf.Max(1, _prioJobTaskBuf.Count) * taskRowH;
            float dutiesH = 20f + 3 * dutyRowH;
            float pw = 360f;
            float ph = Mathf.Min(
                Screen.height - 80f,
                64f + assignH + sectionGap + detailH + sectionGap + dutiesH + 40f);
            // Clear of right tool strip — never sit under ASSIGN / PRIO buttons.
            float bx = Screen.width - pw - 16f - HudToolStripReserve;
            float by = 52f;
            var panel = new Rect(bx, by, pw, ph);
            DeepCoreBentoUi.DrawPanel(panel);
            Block(panel);

            float pad = 18f;
            float x = panel.x + pad;
            float y = panel.y + 16f;
            float inner = pw - pad * 2f;

            GUI.Label(new Rect(x, y, inner, 22f), "Work Priorities",
                DeepCoreBentoUi.Label(16, DeepCoreBentoUi.TextPrimary, bold: true));
            y += 22f;
            GUI.Label(new Rect(x, y, inner, 16f),
                "Click a priority to cycle · right-click reverses",
                DeepCoreBentoUi.Label(11, DeepCoreBentoUi.TextMuted));
            y += 22f;

            GUI.Label(new Rect(x, y, inner, 14f), "Crew",
                DeepCoreBentoUi.Label(11, DeepCoreBentoUi.TextMuted, bold: true));
            y += 16f;

            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null) continue;
                var job = GetAssignmentJob(wr);
                bool sel = wr.WorkerId == _prioUiSelectedWorkerId;
                var row = new Rect(x, y, inner, rowH - 2f);
                Block(row);
                bool hover = row.Contains(e.mousePosition);
                DeepCoreBentoUi.DrawSelected(row, sel, hover);

                Color nameCol = sel ? DeepCoreBentoUi.TextPrimary : DeepCoreBentoUi.TextSecondary;
                GUI.Label(new Rect(row.x + 12f, row.y + 6f, 92f, 18f),
                    wr.DisplayName,
                    DeepCoreBentoUi.Label(13, nameCol, bold: sel));

                GUI.Label(new Rect(row.x + 108f, row.y + 7f, 110f, 16f), ProfessionLabel(job),
                    DeepCoreBentoUi.Label(12,
                        sel ? DeepCoreBentoUi.TextPrimary : DeepCoreBentoUi.TextMuted));

                string taskLab = WorkPriorityDirector.ActiveTaskShortLabel(wr);
                if (!string.IsNullOrEmpty(taskLab))
                {
                    GUI.Label(new Rect(row.x + 220f, row.y + 7f, inner - 232f, 16f),
                        taskLab,
                        DeepCoreBentoUi.Label(12, DeepCoreBentoUi.Positive));
                }

                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                    _prioUiSelectedWorkerId = wr.WorkerId;
                y += rowH;
            }

            y += 8f;
            DeepCoreBentoUi.DrawDivider(x, y, inner);
            y += sectionGap - 4f;

            string detailTitle = selWr != null
                ? $"{selWr.DisplayName}  ·  {ProfessionLabel(selJob)}"
                : "—";
            GUI.Label(new Rect(x, y, inner, 16f), detailTitle,
                DeepCoreBentoUi.Label(13, DeepCoreBentoUi.TextPrimary, bold: true));
            y += 20f;

            if (selWr?.Priorities == null || selJob == JobType.Unassigned)
            {
                GUI.Label(new Rect(x, y, inner, 18f),
                    "Assign a profession in Crew to edit priorities.",
                    DeepCoreBentoUi.Label(12, DeepCoreBentoUi.TextMuted));
                y += taskRowH;
            }
            else
            {
                for (int t = 0; t < _prioJobTaskBuf.Count; t++)
                {
                    var def = _prioJobTaskBuf[t];
                    var pref = selWr.Priorities;
                    var prio = pref.GetPriority(def.Id);
                    bool active = pref.ActiveTaskId == def.Id;
                    bool taskSel = def.Id == _prioUiSelectedTaskId;
                    var row = new Rect(x, y, inner, taskRowH - 2f);
                    Block(row);
                    bool hover = row.Contains(e.mousePosition);
                    DeepCoreBentoUi.DrawSelected(row, taskSel, hover);

                    Color nameC = taskSel || active
                        ? DeepCoreBentoUi.TextPrimary
                        : DeepCoreBentoUi.TextSecondary;
                    GUI.Label(new Rect(row.x + 12f, row.y + 7f, 168f, 18f),
                        def.DisplayName,
                        DeepCoreBentoUi.Label(13, nameC, bold: taskSel || active));

                    string lab = prio == WorkPriorityLevel.Off ? "Off" : ((int)prio).ToString();
                    var chip = new Rect(row.x + 188f, row.y + 4f, 40f, 22f);
                    Block(chip);
                    if (DeepCoreBentoUi.DrawPriorityChip(chip, lab, active, taskSel))
                    {
                        pref.CyclePriority(def.Id, reverse: false);
                        _prioUiSelectedTaskId = def.Id;
                    }
                    if (chip.Contains(e.mousePosition) && e.type == EventType.MouseDown && e.button == 1)
                    {
                        pref.CyclePriority(def.Id, reverse: true);
                        _prioUiSelectedTaskId = def.Id;
                        e.Use();
                    }

                    string status = active ? "Active" : "Idle";
                    Color statusC = active ? DeepCoreBentoUi.Positive : DeepCoreBentoUi.TextMuted;
                    GUI.Label(new Rect(row.x + 240f, row.y + 7f, 70f, 18f), status,
                        DeepCoreBentoUi.Label(12, statusC, bold: active));

                    if (hover)
                    {
                        _prioHoverTaskTip = $"{def.DisplayName}\n{def.Tooltip}";
                        _prioHoverCellTip =
                            $"{selWr.DisplayName} · {def.ShortName}\n" +
                            $"Priority: {lab}\n" +
                            WorkPriorityResolver.WhySuitable(selWr, def, selJob);
                    }
                    if (GUI.Button(new Rect(row.x, row.y, 180f, row.height), GUIContent.none, GUIStyle.none))
                        _prioUiSelectedTaskId = def.Id;

                    y += taskRowH;
                }
            }

            y += 8f;
            DeepCoreBentoUi.DrawDivider(x, y, inner);
            y += sectionGap - 4f;

            GUI.Label(new Rect(x, y, inner, 14f), "Crew duties",
                DeepCoreBentoUi.Label(11, DeepCoreBentoUi.TextMuted, bold: true));
            y += 16f;

            DrawCrewDutyRow(ref y, x, inner, selWr, WorkerGenericTaskIds.Rescue,
                "Rescue", e);
            DrawCrewDutyRow(ref y, x, inner, selWr, WorkerGenericTaskIds.TreatInjuries,
                "Emergency first aid", e);
            DrawCrewDutyRow(ref y, x, inner, selWr, WorkerGenericTaskIds.ClearDebris,
                "Emergency access clearance", e);

            y += 10f;
            GUI.Label(new Rect(x, y, inner, 32f),
                "Priorities never change profession. Use Crew assignment to reassign jobs.",
                DeepCoreBentoUi.LabelWrap(11, DeepCoreBentoUi.TextMuted));
        }

        static string ProfessionLabel(JobType job) => job switch
        {
            JobType.Prospecting => "Prospector",
            JobType.Excavation => "Excavator",
            JobType.Hauling => "Hauler",
            JobType.Refining => "Refiner",
            JobType.Engineering => "Engineer",
            JobType.Steward => "Steward",
            _ => "Unassigned",
        };

        void DrawCrewDutyRow(
            ref float y, float x, float inner, WorkerRuntime selWr, string taskId, string label, Event e)
        {
            const float dutyRowH = 32f;
            var row = new Rect(x, y, inner, dutyRowH - 2f);
            Block(row);
            bool hover = row.Contains(e.mousePosition);
            bool active = selWr?.Priorities != null && selWr.Priorities.ActiveTaskId == taskId;
            DeepCoreBentoUi.DrawSelected(row, false, hover);

            GUI.Label(new Rect(row.x + 12f, row.y + 7f, inner - 120f, 18f), label,
                DeepCoreBentoUi.Label(13,
                    active ? DeepCoreBentoUi.TextPrimary : DeepCoreBentoUi.TextSecondary));

            var btn = new Rect(row.xMax - 92f, row.y + 3f, 82f, 24f);
            Block(btn);
            bool responding = selWr?.Priorities != null
                              && selWr.Priorities.GetPriority(taskId) != WorkPriorityLevel.Off
                              && (int)selWr.Priorities.GetPriority(taskId) <= 2;
            if (DeepCoreBentoUi.DrawControl(btn, "Respond", active: responding || active))
            {
                if (selWr?.Priorities != null)
                {
                    if (selWr.Priorities.GetPriority(taskId) == WorkPriorityLevel.Off)
                        selWr.Priorities.SetPriority(taskId, WorkPriorityLevel.P1);
                    else
                        selWr.Priorities.CyclePriority(taskId, reverse: false);
                    _prioUiSelectedTaskId = taskId;
                    if (selWr != null) _prioUiSelectedWorkerId = selWr.WorkerId;
                }
            }
            y += dutyRowH;
        }

        static void DrawRectBorder(Rect r, Color c, float thickness)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - thickness, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - thickness, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        void DrawPriorityDevDiag()
        {
            if (_crewWorkers == null) return;
            _priorities.BindHosts(
                _world, _collapse, _mineInfra, _worker, _hauler, _refiner, _engineer,
                _prospector, _scanHistory != null ? _scanHistory.Analyst : null,
                _steward, _campLife, _crewWorkers, CrewWorldPos, BasecampPos);

            var focus = FindCrewWorker(_selectedWorkerId) ?? _crewWorkers[0];
            float pw = 400f;
            float ph = 460f;
            var panel = new Rect(12f, 56f, pw, ph);
            DrawCyberPanel(panel, lit: false, accentOverride: UiAmber);
            Block(panel);
            float x = panel.x + 10f;
            float y = panel.y + 6f;
            GUI.Label(new Rect(x, y, pw - 20f, 14f), "PRIORITY DIAG (DEV)",
                LabelStyle(10, UiAmber, bold: true));
            y += 16f;
            if (focus?.Priorities == null)
            {
                GUI.Label(new Rect(x, y, pw - 20f, 12f), "No worker / prefs", LabelStyle(9, UiDim));
                return;
            }
            var p = focus.Priorities;
            string task = WorkPriorityDirector.ActiveTaskShortLabel(focus);
            if (string.IsNullOrEmpty(task)) task = "—";
            string prioLab = string.IsNullOrEmpty(p.ActiveTaskId) ? "—"
                : p.IsOff(p.ActiveTaskId) ? "OFF"
                : ((int)p.GetPriority(p.ActiveTaskId)).ToString();
            GUI.Label(new Rect(x, y, pw - 20f, 12f),
                $"{focus.DisplayName}  task={task}  prio={prioLab}",
                LabelStyle(9, UiWhite, bold: true));
            y += 14f;
            GUI.Label(new Rect(x, y, pw - 20f, 12f),
                $"WinningPriorityBand: {(p.LastWinningPriorityBand > 0 ? p.LastWinningPriorityBand.ToString() : "—")}",
                LabelStyle(9, UiGreen, bold: true));
            y += 13f;
            if (!string.IsNullOrEmpty(p.LastBandRejectHint))
            {
                GUI.Label(new Rect(x, y, pw - 20f, 12f), p.LastBandRejectHint,
                    LabelStyle(8, UiAmber));
                y += 13f;
            }
            var avail = string.IsNullOrEmpty(p.ActiveTaskId)
                ? WorkAvailabilityResult.None("—")
                : _priorities.Context.Probe(p.ActiveTaskId);
            GUI.Label(new Rect(x, y, pw - 20f, 12f),
                $"Avail={(avail.Available ? "Y" : "N")} ({avail.Reason})  " +
                $"Valid={(p.ActiveTaskValid ? "Y" : "N")}  inv={p.ActiveInvalidationReason}",
                LabelStyle(8, avail.Available && p.ActiveTaskValid ? UiCyan : UiAmber));
            y += 13f;
            // Compact band candidates for Haul / Refine / active
            void BandLine(string tid)
            {
                var def = WorkTaskRegistry.Get(tid);
                if (def == null) return;
                var pr = p.GetPriority(tid);
                var av = _priorities.Context.Probe(tid);
                string rej = pr == WorkPriorityLevel.Off ? "OFF"
                    : !av.Available ? "NO WORK"
                    : (p.LastWinningPriorityBand > 0 && (int)pr > p.LastWinningPriorityBand)
                        ? "LOWER PRIORITY BAND"
                        : (p.ActiveTaskId == tid ? "SELECTED" : "cand");
                GUI.Label(new Rect(x, y, pw - 20f, 11f),
                    $"{def.ShortName} P{(pr == WorkPriorityLevel.Off ? "OFF" : ((int)pr).ToString())} " +
                    $"avail={(av.Available ? "Y" : "N")} → {rej}",
                    LabelStyle(8, rej == "SELECTED" ? UiGreen
                        : rej == "LOWER PRIORITY BAND" ? UiAmber : UiDim));
                y += 11f;
            }
            BandLine(WorkerGenericTaskIds.HaulMaterials);
            BandLine(WorkerGenericTaskIds.RefineOre);
            BandLine(WorkerGenericTaskIds.Excavate);
            BandLine(WorkerGenericTaskIds.InstallSupports);
            y += 2f;
            var asgJob = GetAssignmentJob(focus);
            bool hostOk = WorkPriorityResolver.HostAllowsVoluntaryWork(focus, asgJob);
            bool performing = IsActuallyPerformingPriorityTask(focus, p.ActiveTaskId);
            GUI.Label(new Rect(x, y, pw - 20f, 12f),
                $"Host {asgJob} allow={hostOk}  performing={performing}  dirty={(p.ResolverDirty ? "Y" : "N")}  yieldedOff={(p.LastHostYieldedForOff ? "Y" : "N")}",
                LabelStyle(8, UiMute));
            y += 13f;
            float tgt = string.IsNullOrEmpty(p.ActiveTaskId) ? 0f : p.GetTargetHours(p.ActiveTaskId);
            float done = string.IsNullOrEmpty(p.ActiveTaskId) ? 0f : p.GetWorkedHours(p.ActiveTaskId);
            GUI.Label(new Rect(x, y, pw - 20f, 12f),
                $"Target {tgt:0.0}h  Worked {done:0.0}h  Deficit {p.TargetDeficit(p.ActiveTaskId):0.0}h",
                LabelStyle(8, UiCyan));
            y += 13f;
            GUI.Label(new Rect(x, y, pw - 20f, 12f),
                $"Score {p.LastScoreTotal:0}  emerg={(p.LastEmergencyOverride ? "Y" : "N")}  hint={p.LastTargetHint}",
                LabelStyle(8, UiWhite));
            y += 13f;
            GUI.Label(new Rect(x, y, pw - 20f, 36f),
                $"Prio {p.LastScorePriority:0}  Def {p.LastScoreDeficit:0}  Suit {p.LastScoreSuit:0}\n" +
                $"Dist {p.LastScoreDistance:0}  Cond {p.LastScoreCondition:0}  Cont {p.LastScoreContinuity:0}  Emg {p.LastScoreEmergency:0}",
                LabelStyle(8, UiDim));
            y += 38f;
            GUI.Label(new Rect(x, y, pw - 20f, 24f),
                $"Selected: {p.LastResolveReason}\nRejected/Unavailable: {p.LastUnavailableReason}",
                LabelStyle(8, UiDim));
            y += 28f;
            GUI.Label(new Rect(x, y, pw - 20f, 12f),
                $"Duty={focus.CampBody != null && focus.CampBody.PriorityDutyActive}  " +
                $"Rescue={focus.CampBody != null && focus.CampBody.RescueDutyActive}",
                LabelStyle(8, UiMute));
            y += 16f;
            // Compact crew active list
            for (int i = 0; i < _crewWorkers.Length && i < 6; i++)
            {
                var wr = _crewWorkers[i];
                if (wr?.Priorities == null) continue;
                string t = WorkPriorityDirector.ActiveTaskShortLabel(wr);
                if (string.IsNullOrEmpty(t)) t = "—";
                string offMark = !string.IsNullOrEmpty(wr.Priorities.ActiveTaskId)
                                 && wr.Priorities.IsOff(wr.Priorities.ActiveTaskId) ? "!" : "";
                GUI.Label(new Rect(x, y, pw - 20f, 11f),
                    $"{wr.DisplayName}: {t}{offMark}",
                    LabelStyle(8, wr.WorkerId == focus.WorkerId ? UiGreen : UiDim));
                y += 11f;
            }
        }

        void DrawDailySummaryOverlay()
        {
            var s = _dayTracker.LastSummary;
            if (s == null) { _showDailySummary = false; return; }
            const float pw = 300f;
            float ph = 120f + Mathf.Min(6, s.Lines.Count) * 36f;
            float bx = (Screen.width - pw) * 0.5f;
            float by = 72f;
            var r = new Rect(bx, by, pw, ph);
            DrawCyberPanel(r, lit: true, accentOverride: UiAmber);
            Block(r);
            float x = r.x + 12f;
            float y = r.y + 8f;
            float inner = pw - 24f;
            GUI.Label(new Rect(x, y, inner, 14f), "YESTERDAY", LabelStyle(10, UiAmber, bold: true));
            y += 18f;
            GUI.Label(new Rect(x, y, inner, 12f),
                $"Worked: {_shiftPlanner.FormatHours(s.AvgWorkHours)}", LabelStyle(9, UiWhite));
            y += 13f;
            GUI.Label(new Rect(x, y, inner, 12f),
                $"Commute: {_shiftPlanner.FormatHours(s.AvgCommuteHours)}", LabelStyle(9, UiWhite));
            y += 13f;
            GUI.Label(new Rect(x, y, inner, 12f),
                $"Camp/free time: {_shiftPlanner.FormatHours(s.AvgCampHours)}", LabelStyle(9, UiWhite));
            y += 13f;
            GUI.Label(new Rect(x, y, inner, 12f),
                $"Sleep: {_shiftPlanner.FormatHours(s.AvgSleepHours)}", LabelStyle(9, UiCyan));
            y += 16f;
            // Light priority breakdown for selected / first workers
            if (_crewWorkers != null)
            {
                int shownP = 0;
                for (int wi = 0; wi < _crewWorkers.Length && shownP < 3; wi++)
                {
                    var wr = _crewWorkers[wi];
                    if (wr?.Priorities == null) continue;
                    var all = WorkTaskRegistry.All;
                    var bits = new System.Text.StringBuilder();
                    bits.Append(wr.DisplayName).Append(':');
                    int n = 0;
                    for (int ti = 0; ti < all.Count && n < 4; ti++)
                    {
                        float wh = wr.Priorities.GetWorkedHours(all[ti].Id);
                        if (wh < 0.05f) continue;
                        float tg = wr.Priorities.GetTargetHours(all[ti].Id);
                        bits.Append(' ').Append(all[ti].ShortName).Append(' ')
                            .Append(wh.ToString("0.0"));
                        if (tg > 0.01f) bits.Append('/').Append(tg.ToString("0.0"));
                        bits.Append('h');
                        n++;
                    }
                    if (n == 0) continue;
                    GUI.Label(new Rect(x, y, inner, 12f), bits.ToString(), LabelStyle(8, UiDim));
                    y += 12f;
                    shownP++;
                }
                y += 4f;
            }
            int shown = 0;
            for (int i = 0; i < s.Lines.Count && shown < 5; i++)
            {
                foreach (var line in s.Lines[i].Split('\n'))
                {
                    GUI.Label(new Rect(x, y, inner, 12f), line,
                        LabelStyle(8, line.EndsWith(":") ? UiWhite : UiDim, bold: line.EndsWith(":")));
                    y += 12f;
                }
                y += 4f;
                shown++;
            }
            var close = new Rect(r.xMax - 72f, r.y + 6f, 60f, 18f);
            Block(close);
            if (DrawCyberButton(close, "OK", selected: false, accent: UiGreen))
                _showDailySummary = false;
        }

        ManagerCommContext BuildManagerCommContext(WorkerRuntime wr)
        {
            var ctx = new ManagerCommContext
            {
                Worker = wr,
                Mgr = wr != null ? _managerRels.Get(wr.WorkerId) : null,
                GameHours = _absoluteGameHours,
                OnShift = _crewPhase == CrewPhase.OnShift,
                Asleep = _crewPhase == CrewPhase.Asleep,
                AtCamp = _crewPhase == CrewPhase.CampEvening,
                Commuting = _crewPhase == CrewPhase.HeadingHome
                            || _crewPhase == CrewPhase.HeadingOut,
                MissionTight = _mission01 != null
                               && _mission01.Outcome == Mission01Outcome.Active
                               && _mission01.DaysRemaining <= 4
                               && (!_mission01.GoldFound || !_mission01.DiamondFound),
                MissionOk = _mission01 != null
                            && _mission01.GoldFound && _mission01.DiamondFound,
            };
            if (wr != null)
            {
                var L = _dayTracker.Get(wr.WorkerId);
                ctx.WorkedHoursToday = L.WorkHours;
                ctx.OvertimeHours = Mathf.Max(0f, L.WorkHours - ShiftPlanner.NominalWorkHours);
                ctx.ConsecutiveOvertimeDays = L.ConsecutiveOvertimeDays;
                ctx.SleepDeficit01 = L.SleepDeficit01;
                var st = wr.State;
                if (st != null)
                {
                    float perf = (st.FocusState / 100f) * 0.45f
                                 + (st.Morale / 100f) * 0.35f
                                 - (st.Frustration / 100f) * 0.25f;
                    ctx.Performance01 = Mathf.Clamp01(perf + 0.15f);
                }
                // Recent success/failure from event history (last ~3h)
                if (wr.EventHistory != null)
                {
                    for (int i = wr.EventHistory.Items.Count - 1; i >= 0; i--)
                    {
                        var rec = wr.EventHistory.Items[i];
                        if (rec?.Event == null) continue;
                        if (_absoluteGameHours - rec.Event.GameHours > 3f) break;
                        if (rec.Event.EventType == WorkerStateEventType.ProgressSuccess
                            || rec.Event.EventType == WorkerStateEventType.MajorSuccess
                            || rec.Event.EventType == WorkerStateEventType.Discovery)
                            ctx.RecentSuccess = true;
                        if (rec.Event.EventType == WorkerStateEventType.RepeatedFailure
                            || rec.Event.EventType == WorkerStateEventType.WorkBlocked
                            || rec.Event.EventType == WorkerStateEventType.InvestigationFailure)
                            ctx.RecentFailure = true;
                    }
                }
            }
            return ctx;
        }

        void PresentManagerCommResult(ManagerCommResult result, ManagerTalkAction? talk = null)
        {
            if (result == null) return;
            _lastTalkResult = result;
            _lastTalkResultUntilUnscaled = Time.unscaledTime + 3.8f;
            if (!result.SpamBlocked && !string.IsNullOrEmpty(result.Line))
            {
                var wr = FindCrewWorker(result.WorkerId);
                var asg = wr != null ? _assignments.GetAssignment(wr.WorkerId) : null;
                var job = asg != null ? asg.JobType : JobType.Unassigned;
                TryAuthoredSocialBanter(
                    result.WorkerId,
                    result.DisplayName,
                    job,
                    "ManagerTalk",
                    result.Line,
                    result.Valence);
            }
            ManagerCommSystem.ApplyResult(
                result, _managerRels,
                _socialAura != null && _socialAura.IsBootstrapped ? _socialAura.Memory : null,
                _managerIntents, _absoluteGameHours, talk);

            // Accepted confinement push → resume SAME painted route (never rewrite plan)
            if (talk == ManagerTalkAction.PushHarder
                && result.AcceptedIntent
                && _worker != null
                && _worker.AssignedWorkerId == result.WorkerId)
            {
                _worker.ResumeExecutionIfPossible();
            }

            if (talk == ManagerTalkAction.TakeABreak
                && result.AcceptedIntent
                && _worker != null
                && _worker.AssignedWorkerId == result.WorkerId)
            {
                var breakWr = FindCrewWorker(result.WorkerId);
                if (breakWr?.State != null
                    && breakWr.State.ClaustrophobicStress >= ClaustrophobiaBands.ElevatedAt
                    && !_worker.ExecutionPaused)
                {
                    _worker.PauseExecution("MANAGER | Take a break — plan retained");
                }
            }

            DigHoodLog.Push(
                $"TALK | {result.DisplayName}: {result.ReactionLabel}"
                + (result.SpamBlocked ? " (blocked)" : ""));
        }

        float ResolveClaustroRefuseCeiling(WorkerRuntime wr)
        {
            if (wr == null) return ClaustrophobiaBands.CriticalAt;
            var f = _managerIntents.Get(wr.WorkerId);
            if (f.AcceptedPush && f.WillingUntilStress > ClaustrophobiaBands.CriticalAt)
                return f.WillingUntilStress;
            return ClaustrophobiaBands.CriticalAt;
        }

        void DrawManagerTalkPanel()
        {
            var wr = FindCrewWorker(_selectedWorkerId);
            if (wr == null || !wr.IsAlive)
            {
                const float pw0 = 240f;
                var r0 = new Rect(Screen.width - pw0 - 12f - HudToolStripReserve, 96f, pw0, 64f);
                DrawCyberPanel(r0, lit: true, accentOverride: UiGreen);
                Block(r0);
                GUI.Label(new Rect(r0.x + 10f, r0.y + 14f, pw0 - 20f, 30f),
                    "Select a living worker\nto TALK.", LabelStyle(9, UiDim));
                return;
            }

            var ctx = BuildManagerCommContext(wr);
            const float pw = 292f;
            float ph = 318f;
            float bx = Screen.width - pw - 12f - HudToolStripReserve;
            if (bx < 160f) bx = 160f;
            var r = new Rect(bx, 96f, pw, ph);
            DrawCyberPanel(r, lit: true, accentOverride: UiGreen);
            Block(r);
            float x = r.x + 10f;
            float y = r.y + 6f;
            float inner = pw - 20f;

            GUI.Label(new Rect(x, y, inner, 12f), "TALK", LabelStyle(8, UiMute, bold: true));
            y += 14f;
            GUI.Label(new Rect(x, y, inner, 14f), wr.DisplayName.ToUpperInvariant(),
                LabelStyle(11, UiWhite, bold: true));
            y += 18f;

            var mgr = _managerRels.Get(wr.WorkerId);
            var lab = ManagerRelationUi.Classify(mgr);
            GUI.Label(new Rect(x, y, inner, 12f),
                $"YOUR RELATIONSHIP · {ManagerRelationUi.LabelText(lab)}",
                LabelStyle(8, UiCyan, bold: true));
            y += 14f;
            GUI.Label(new Rect(x, y, inner, 11f),
                $"Trust {mgr.Trust:0}  Respect {mgr.Respect:0}  Resentment {mgr.Resentment:0}",
                LabelStyle(8, UiDim));
            y += 16f;

            void Act(string label, ManagerTalkAction action, Color accent)
            {
                if (!ManagerCommSystem.TalkActionAvailable(action, ctx)) return;
                var br = new Rect(x, y, inner, 22f);
                Block(br);
                if (DrawCyberButton(br, label, selected: false, accent: accent))
                {
                    var result = ManagerCommSystem.EvaluateTalk(
                        action, ctx, _managerCommSpam, _managerIntents);
                    PresentManagerCommResult(result, action);
                }
                y += 24f;
            }

            GUI.Label(new Rect(x, y, inner, 11f), "WORK", LabelStyle(8, UiMute, bold: true));
            y += 13f;
            Act("PUSH HARDER", ManagerTalkAction.PushHarder, UiAmber);
            Act("KEEP IT UP", ManagerTalkAction.KeepItUp, UiGreen);
            Act("EASE OFF", ManagerTalkAction.EaseOff, UiCyan);
            Act("TAKE A BREAK", ManagerTalkAction.TakeABreak, UiCyan);
            y += 4f;
            GUI.Label(new Rect(x, y, inner, 11f), "PERSONAL", LabelStyle(8, UiMute, bold: true));
            y += 13f;
            Act("PRAISE", ManagerTalkAction.Praise, UiGreen);
            Act("ENCOURAGE", ManagerTalkAction.Encourage, UiCyan);
            Act("CHECK IN", ManagerTalkAction.CheckIn, UiCyan);
            Act("CRITICIZE", ManagerTalkAction.Criticize, UiAmber);

            if (_lastTalkResult != null && _lastTalkResult.WorkerId == wr.WorkerId
                && Time.unscaledTime < _lastTalkResultUntilUnscaled)
            {
                y += 6f;
                Color rc = SocialSpeechVisuals.Accent(_lastTalkResult.Valence);
                GUI.Label(new Rect(x, y, inner, 14f), _lastTalkResult.ReactionLabel,
                    LabelStyle(10, rc, bold: true));
            }
        }

        void DrawManagerCrewTalkPanel()
        {
            const float pw = 280f;
            float ph = 220f;
            float bx = Screen.width - pw - 12f - HudToolStripReserve;
            if (bx < 160f) bx = 160f;
            var r = new Rect(bx, 96f, pw, ph);
            DrawCyberPanel(r, lit: true, accentOverride: UiAmber);
            Block(r);
            float x = r.x + 10f;
            float y = r.y + 6f;
            float inner = pw - 20f;
            GUI.Label(new Rect(x, y, inner, 12f), "CREW MESSAGE", LabelStyle(8, UiMute, bold: true));
            y += 16f;
            GUI.Label(new Rect(x, y, inner, 22f),
                "Each worker hears this alone.\nNo universal buff.",
                LabelStyle(8, UiDim));
            y += 28f;

            void Crew(string label, ManagerCrewAction action)
            {
                var br = new Rect(x, y, inner, 22f);
                Block(br);
                if (DrawCyberButton(br, label, selected: false, accent: UiAmber))
                    BroadcastCrewAction(action);
                y += 24f;
            }
            Crew("PUSH FOR THE DAY", ManagerCrewAction.PushForTheDay);
            Crew("STEADY DAY", ManagerCrewAction.SteadyDay);
            Crew("TAKE IT EASY", ManagerCrewAction.TakeItEasy);
            Crew("GOOD WORK", ManagerCrewAction.GoodWork);
            Crew("WE NEED TO TURN THIS AROUND", ManagerCrewAction.TurnThisAround);
        }

        void BroadcastCrewAction(ManagerCrewAction action)
        {
            int crewCode = 3000 + (int)action;
            if (!_managerCommSpam.CanDo(0, crewCode, _absoluteGameHours, out string note))
            {
                DigHoodLog.Push($"CREW TALK | {note}");
                return;
            }
            _managerCommSpam.Record(0, crewCode, _absoluteGameHours,
                ManagerCommAntiSpam.CrewActionCooldownHours);
            if (_crewWorkers == null) return;
            ManagerCommResult spotlight = null;
            int spoken = 0;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null || !wr.IsAlive) continue;
                if (_crewPhase == CrewPhase.Asleep) continue;
                var ctx = BuildManagerCommContext(wr);
                var result = ManagerCommSystem.EvaluateCrewMember(
                    action, ctx, _managerCommSpam, crewCode);
                if (result == null) continue;
                ManagerCommSystem.ApplyResult(
                    result, _managerRels,
                    _socialAura != null && _socialAura.IsBootstrapped ? _socialAura.Memory : null,
                    _managerIntents, _absoluteGameHours,
                    ManagerCommSystem.TalkFromCrew(action));
                // Each living worker speaks — not only the selected spotlight
                if (!string.IsNullOrEmpty(result.Line))
                {
                    var asg = _assignments.GetAssignment(wr.WorkerId);
                    if (TryAuthoredSocialBanter(
                        wr.WorkerId, wr.DisplayName,
                        asg != null ? asg.JobType : JobType.Unassigned,
                        "CrewTalk", result.Line, result.Valence))
                        spoken++;
                }
                if (spotlight == null
                    || result.Valence > spotlight.Valence
                    || (result.WorkerId == _selectedWorkerId))
                    spotlight = result;
            }
            if (spotlight != null)
            {
                _lastTalkResult = spotlight;
                _lastTalkResultUntilUnscaled = Time.unscaledTime + 3.5f;
            }
            DigHoodLog.Push($"CREW TALK | {action} · {spoken} replied");
        }

        void DrawLastManagerTalkFlash()
        {
            if (_lastTalkResult == null || Time.unscaledTime > _lastTalkResultUntilUnscaled)
                return;
            var res = _lastTalkResult;
            bool hasLine = !string.IsNullOrEmpty(res.Line) && !res.SpamBlocked;
            float pw = 260f;
            float ph = hasLine ? 72f : 52f;
            var r = new Rect((Screen.width - pw) * 0.5f, 48f, pw, ph);
            Color accent = SocialSpeechVisuals.Accent(res.Valence);
            DrawCyberPanel(r, lit: true, accentOverride: accent);
            GUI.Label(new Rect(r.x + 10f, r.y + 6f, pw - 20f, 14f),
                res.DisplayName.ToUpperInvariant(), LabelStyle(9, UiWhite, bold: true));
            GUI.Label(new Rect(r.x + 10f, r.y + 22f, pw - 20f, 16f),
                res.ReactionLabel, LabelStyle(11, accent, bold: true));
            if (hasLine)
            {
                GUI.Label(new Rect(r.x + 10f, r.y + 42f, pw - 20f, 24f),
                    "\"" + res.Line + "\"", LabelStyle(8, UiDim));
            }
        }

        void DrawManagerInterveneOverlay()
        {
            if (_socialAura == null || !_socialAura.IsBootstrapped) return;
            var session = _socialAura.Conflict.FindAnyActiveSession();
            if (session == null) return;
            var a = FindCrewWorker(session.IdA);
            var b = FindCrewWorker(session.IdB);
            if (a == null || b == null) return;
            bool isFight = session.FightOccurred;

            float pw = 300f;
            float ph = isFight ? 210f : 230f;
            var r = new Rect(12f, 96f, pw, ph);
            DrawCyberPanel(r, lit: true, accentOverride: isFight ? UiRed : UiAmber);
            Block(r);
            float x = r.x + 10f;
            float y = r.y + 6f;
            float inner = pw - 20f;
            GUI.Label(new Rect(x, y, inner, 12f),
                isFight ? "FIGHT — INTERVENE" : "ARGUMENT — INTERVENE",
                LabelStyle(9, isFight ? UiRed : UiAmber, bold: true));
            y += 16f;
            GUI.Label(new Rect(x, y, inner, 14f),
                $"{a.DisplayName}  ·  {b.DisplayName}",
                LabelStyle(10, UiWhite, bold: true));
            y += 18f;

            void IBtn(string lab, ManagerInterveneAction act)
            {
                var br = new Rect(x, y, inner, 20f);
                Block(br);
                if (DrawCyberButton(br, lab, selected: false,
                        accent: act == ManagerInterveneAction.StayOut ? UiDim : UiCyan))
                    DoManagerIntervene(act, session, a, b, isFight);
                y += 22f;
            }

            if (isFight)
            {
                IBtn("ORDER THEM TO STOP", ManagerInterveneAction.OrderStop);
                IBtn("BREAK IT UP", ManagerInterveneAction.BreakItUp);
                IBtn("CALL ANOTHER WORKER TO HELP", ManagerInterveneAction.CallHelp);
                IBtn("STAY OUT OF IT", ManagerInterveneAction.StayOut);
            }
            else
            {
                IBtn("CALM DOWN", ManagerInterveneAction.CalmDown);
                IBtn("BACK TO WORK", ManagerInterveneAction.BackToWork);
                IBtn("HEAR THEM OUT", ManagerInterveneAction.HearThemOut);
                IBtn($"SIDE WITH {a.DisplayName.ToUpperInvariant()}", ManagerInterveneAction.SideWithA);
                IBtn($"SIDE WITH {b.DisplayName.ToUpperInvariant()}", ManagerInterveneAction.SideWithB);
                IBtn("STAY OUT OF IT", ManagerInterveneAction.StayOut);
            }

            if (_lastIntervene != null && !string.IsNullOrEmpty(_lastIntervene.Note))
            {
                y += 4f;
                GUI.Label(new Rect(x, y, inner, 14f), _lastIntervene.Note,
                    LabelStyle(8, _lastIntervene.Success ? UiGreen : UiAmber));
            }
        }

        void DoManagerIntervene(
            ManagerInterveneAction action,
            SocialArgumentSession session,
            WorkerRuntime a,
            WorkerRuntime b,
            bool isFight)
        {
            var outcome = ManagerCommSystem.EvaluateIntervene(
                action, session, a, b, _managerRels, _managerCommSpam,
                _absoluteGameHours, isFight, _campRng);
            _lastIntervene = outcome;
            if (outcome.SpamBlocked)
            {
                DigHoodLog.Push($"INTERVENE | {outcome.Note}");
                return;
            }

            // Apply speech + memory (relation already applied inside EvaluateIntervene for sides/authority)
            // Avoid double-adding manager deltas: ApplyResult again would stack.
            // Present speech only:
            if (outcome.ResultA != null)
            {
                var ra = outcome.ResultA;
                _lastTalkResult = ra;
                _lastTalkResultUntilUnscaled = Time.unscaledTime + 3f;
                TryAuthoredSocialBanter(ra.WorkerId, ra.DisplayName, JobType.Unassigned,
                    "Intervene", ra.Line, ra.Valence);
                WriteManagerMemoryOnly(ra);
            }
            if (outcome.ResultB != null)
            {
                var rb = outcome.ResultB;
                TryAuthoredSocialBanter(rb.WorkerId, rb.DisplayName, JobType.Unassigned,
                    "Intervene", rb.Line, rb.Valence);
                WriteManagerMemoryOnly(rb);
            }

            if (outcome.SideFavorId > 0 && outcome.SideAgainstId > 0 && _socialAura.IsBootstrapped)
            {
                var fav = _socialAura.Relation(outcome.SideFavorId, outcome.SideAgainstId);
                var against = _socialAura.Relation(outcome.SideAgainstId, outcome.SideFavorId);
                fav?.Add(0.15f, 0.1f, -0.2f);
                against?.Add(-0.35f, -0.2f, 0.55f);
            }

            if (outcome.ResolveConflict)
            {
                _socialAura.Conflict.TryManagerIntervene(
                    _socialAura.World, session, _absoluteGameHours,
                    successResolve: true, whyTag: "MANAGER_" + action);
            }
            else if (action != ManagerInterveneAction.StayOut)
            {
                _socialAura.Conflict.TryManagerIntervene(
                    _socialAura.World, session, _absoluteGameHours,
                    successResolve: false, whyTag: "MANAGER_FAIL_" + action);
            }

            DigHoodLog.Push($"INTERVENE | {action} · {outcome.Note}");
        }

        void WriteManagerMemoryOnly(ManagerCommResult result)
        {
            if (result?.MemoryType == null || result.MemoryStrength < 0.35f) return;
            if (_socialAura == null || !_socialAura.IsBootstrapped) return;
            _socialAura.Memory.Add(new SocialMemoryEntry
            {
                Type = result.MemoryType.Value,
                Strength = result.MemoryStrength,
                GameTime = _absoluteGameHours,
                Context = SocialContext.None,
                ObserverId = result.WorkerId,
                TargetId = ManagerRelationshipStore.ManagerId,
                SourceRef = "ManagerIntervene/" + result.ReactionLabel,
                Significance = result.MemoryStrength >= 0.55f
                    ? SocialMemorySignificance.Significant
                    : SocialMemorySignificance.Ordinary,
            });
        }

        void DrawManagerRelationOnSheet(ref float y, float x0, float innerW, WorkerRuntime wr)
        {
            var mgr = _managerRels.Get(wr.WorkerId);
            var lab = ManagerRelationUi.Classify(mgr);
            GUI.Label(new Rect(x0, y, innerW, 12f), "YOUR RELATIONSHIP",
                LabelStyle(9, UiCyan, bold: true));
            y += 14f;
            GUI.Label(new Rect(x0, y, innerW, 12f), ManagerRelationUi.LabelText(lab),
                LabelStyle(10, UiWhite, bold: true));
            y += 14f;
            GUI.Label(new Rect(x0, y, innerW, 11f),
                $"Trust {mgr.Trust:0}   Respect {mgr.Respect:0}   Resentment {mgr.Resentment:0}",
                LabelStyle(8, UiDim));
            var tipRect = new Rect(x0, y - 26f, innerW, 40f);
            if (tipRect.Contains(Event.current.mousePosition))
            {
                var L = _dayTracker.Get(wr.WorkerId);
                SocialMemoryStore mem = _socialAura != null && _socialAura.IsBootstrapped
                    ? _socialAura.Memory : null;
                _mgrRelHoverTip = ManagerRelationUi.BuildWhyHover(mgr, mem, wr.WorkerId, L);
                _mgrRelHoverGui = Event.current.mousePosition;
            }
            y += 14f;
            var talkBtn = new Rect(x0, y, Mathf.Min(120f, innerW), 20f);
            Block(talkBtn);
            if (DrawCyberButton(talkBtn, "TALK", selected: _hudPopup == HudPopupKind.Talk,
                    accent: UiGreen))
            {
                SelectPersonById(wr.WorkerId);
                _hudPopup = HudPopupKind.Talk;
            }
            y += 26f;
        }

        void DrawCampLifePanel()
        {
            const float pw = 260f;
            float ph = DevMode.Enabled ? 420f : 168f;
            float bx = Screen.width - pw - 12f - HudToolStripReserve;
            if (bx < 160f) bx = 160f;
            float by = 96f;
            var r = new Rect(bx, by, pw, ph);
            DrawCyberPanel(r, lit: true, accentOverride: UiAmber);
            Block(r);
            float x = r.x + 10f;
            float y = r.y + 6f;
            float inner = pw - 20f;
            GUI.Label(new Rect(x, y, inner, 12f), "CAMP", LabelStyle(8, UiMute, bold: true));
            y += 16f;
            string meal = _campLife != null ? _campLife.MealLabel : "—";
            string hyg = _campLife != null ? _campLife.HygieneLabel : "—";
            string act = _campLife != null ? _campLife.StewardActivity : "—";
            GUI.Label(new Rect(x, y, inner, 12f), $"TONIGHT'S MEAL  {meal}",
                LabelStyle(9, UiWhite, bold: true));
            y += 14f;
            GUI.Label(new Rect(x, y, inner, 12f), $"CAMP HYGIENE  {hyg}",
                LabelStyle(9, UiCyan));
            y += 14f;
            GUI.Label(new Rect(x, y, inner, 12f), $"STEWARD  {act}",
                LabelStyle(8, UiDim));
            y += 16f;
            GUI.Label(new Rect(x, y, inner, 11f), "INJURED CREW", LabelStyle(7, UiMute, bold: true));
            y += 12f;
            int listed = 0;
            if (_crewWorkers != null)
            {
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    var wr = _crewWorkers[i];
                    if (wr == null || !wr.IsAlive) continue;
                    bool care = wr.State != null && wr.State.NeedsCare;
                    if (!care && (wr.Injuries == null || wr.Injuries.Count <= 0)) continue;
                    var status = InjuryResponse.EvaluateWorkStatus(wr);
                    WorkerInjuryRecord worst = null;
                    if (wr.Injuries != null)
                    {
                        for (int ii = 0; ii < wr.Injuries.Active.Count; ii++)
                        {
                            var wound = wr.Injuries.Active[ii];
                            if (wound == null || !wound.Active) continue;
                            if (worst == null || (int)wound.Severity > (int)worst.Severity)
                                worst = wound;
                        }
                    }
                    string statusLab = InjuryResponse.StatusLabel(status);
                    string line = worst != null
                        ? $"{wr.DisplayName}  {worst.DisplayName}  {statusLab}"
                        : $"{wr.DisplayName}  {statusLab}";
                    Color c = status == WorkerInjuryWorkStatus.Incapacitated ? UiRed
                        : status == WorkerInjuryWorkStatus.OffDuty || care ? UiAmber
                        : UiWhite;
                    GUI.Label(new Rect(x, y, inner, 11f), line, LabelStyle(7, c));
                    y += 12f;
                    listed++;
                    if (listed >= 4) break;
                }
            }
            if (listed == 0)
            {
                GUI.Label(new Rect(x, y, inner, 11f), "(none)", LabelStyle(7, UiDim));
                y += 12f;
            }
            if (!DevMode.Enabled) return;

            y += 8f;
            GUI.Label(new Rect(x, y, inner, 11f), "DEV CONTROLS", LabelStyle(7, UiMute, bold: true));
            y += 12f;
            float bw = (inner - 4f) * 0.5f;
            float bh = 18f;
            void DevBtn(Rect rr, string label, System.Action actn)
            {
                Block(rr);
                if (DrawCyberButton(rr, label, selected: false, accent: UiAmber))
                    actn();
            }
            DevBtn(new Rect(x, y, bw, bh), "GOOD MEAL", () => DevForceMeal(CampMealQuality.Good));
            DevBtn(new Rect(x + bw + 4f, y, bw, bh), "BAD MEAL", () => DevForceMeal(CampMealQuality.Poor));
            y += bh + 3f;
            DevBtn(new Rect(x, y, bw, bh), "UNSAFE MEAL", () => DevForceMeal(CampMealQuality.Unsafe));
            DevBtn(new Rect(x + bw + 4f, y, bw, bh), "CLEAN CAMP", () =>
            {
                _campLife?.ForceHygiene(CampHygieneBand.Clean);
                DigHoodLog.Push("DEV | CLEAN CAMP");
            });
            y += bh + 3f;
            DevBtn(new Rect(x, y, bw, bh), "DIRTY CAMP", () =>
            {
                _campLife?.ForceHygiene(CampHygieneBand.Filthy);
                DigHoodLog.Push("DEV | DIRTY CAMP");
            });
            y += bh + 3f;
            var sel = FindCrewWorker(_selectedWorkerId);
            DevBtn(new Rect(x, y, bw, bh), "MILD UPSET", () => DevStomach(sel, StomachUpsetSeverity.Mild));
            DevBtn(new Rect(x + bw + 4f, y, bw, bh), "MOD UPSET", () => DevStomach(sel, StomachUpsetSeverity.Moderate));
            y += bh + 3f;
            DevBtn(new Rect(x, y, inner, bh), "SEVERE UPSET (SEL)", () => DevStomach(sel, StomachUpsetSeverity.Severe));
            y += bh + 8f;
            GUI.Label(new Rect(x, y, inner, 11f), "ROSTER STATUS DEMO (SEL)",
                LabelStyle(7, UiMute, bold: true));
            y += 12f;
            DevBtn(new Rect(x, y, bw, bh), "INJ MINOR", () => DevRosterInjury(sel, WorkerInjurySeverity.Minor));
            DevBtn(new Rect(x + bw + 4f, y, bw, bh), "INJ MOD", () => DevRosterInjury(sel, WorkerInjurySeverity.Moderate));
            y += bh + 3f;
            DevBtn(new Rect(x, y, bw, bh), "INJ SERIOUS", () => DevRosterInjury(sel, WorkerInjurySeverity.Serious));
            DevBtn(new Rect(x + bw + 4f, y, bw, bh), "NEEDS CARE", () => DevRosterNeedsCare(sel));
            y += bh + 3f;
            DevBtn(new Rect(x, y, bw, bh), "EXHAUST", () => DevRosterExhaustion(sel));
            DevBtn(new Rect(x + bw + 4f, y, bw, bh), "GRUDGE", () => DevRosterGrudge(sel));
            y += bh + 3f;
            DevBtn(new Rect(x, y, bw, bh), "MARK DEAD", () =>
            {
                if (sel == null || !sel.IsAlive) return;
                sel.State.MarkDead(_absoluteGameHours, 0);
                ReleaseUnavailableWorkerClaims(sel, "worker dead");
                DigHoodLog.Push($"DEV | mark dead → {sel.DisplayName}");
            });
            DevBtn(new Rect(x + bw + 4f, y, bw, bh), "CLEAR STATUS", () => DevRosterClearStatus(sel));
            y += bh + 10f;
            GUI.Label(new Rect(x, y, inner, 11f), "DEBRIS / COLLAPSE", LabelStyle(7, UiMute, bold: true));
            y += 12f;
            DevBtn(new Rect(x, y, bw, bh), "SHOW STABILITY", () =>
            {
                if (_collapse != null)
                    _collapse.ShowStabilityOverlay = !_collapse.ShowStabilityOverlay;
                DigHoodLog.Push($"DEV | SHOW STABILITY {(_collapse != null && _collapse.ShowStabilityOverlay)}");
            });
            DevBtn(new Rect(x + bw + 4f, y, bw, bh), "WEAKEN AREA", () =>
            {
                Vector2 p = sel != null ? CrewWorldPos(sel) : (_worker != null ? _worker.Position : BasecampPos);
                var c = _world.WorldToCell(p);
                _collapse?.WeakenAreaDev(c);
            });
            y += bh + 3f;
            DevBtn(new Rect(x, y, bw, bh), "FORCE MINOR", () => DevForceCollapse(CollapseSeverity.MinorDebris));
            DevBtn(new Rect(x + bw + 4f, y, bw, bh), "FORCE BLOCK", () => DevForceCollapse(CollapseSeverity.Blocking));
            y += bh + 3f;
            DevBtn(new Rect(x, y, bw, bh), "FORCE MAJOR", () => DevForceCollapse(CollapseSeverity.Major));
            DevBtn(new Rect(x + bw + 4f, y, bw, bh), "FORCE HIT", () => DevForceDebrisHit(sel));
            y += bh + 3f;
            DevBtn(new Rect(x, y, bw, bh), "FORCE INCAP", () =>
            {
                if (sel?.State == null || !sel.IsAlive) return;
                WorkerAccidentSystem.ApplyTypedInjury(sel, WorkerInjuryType.BrokenLeg,
                    WorkerInjuryCause.TunnelCollapse, "DEV force incap");
                WorkerAccidentSystem.ApplyTypedInjury(sel, WorkerInjuryType.Concussion,
                    WorkerInjuryCause.TunnelCollapse, "DEV force incap");
                sel.State.MarkIncapacitated(_absoluteGameHours, "DEV force");
                DigHoodLog.Push($"DEV | INCAPACITATED → {sel.DisplayName}");
            });
            DevBtn(new Rect(x + bw + 4f, y, bw, bh), "CLEAR DEBRIS", () =>
            {
                _collapse?.ClearAllDebrisDev();
                _collapse?.RefreshTrappedFlags(_crewWorkers, CrewWorldPos);
                DigHoodLog.Push("DEV | CLEAR DEBRIS");
            });
        }

        void DevForceCollapse(CollapseSeverity sev)
        {
            if (_collapse == null || _world == null) return;
            Vector2 p = _worker != null ? _worker.Position : BasecampPos + Vector2.up * 3f;
            // Prefer a cell behind excavator toward camp so dig face can get cut off
            Vector2 towardCamp = (BasecampPos - p).normalized;
            Vector2 epic = p + towardCamp * (_world.CellSize * 2.5f);
            var cell = _world.WorldToCell(epic);
            if (!_world.IsTunnelOpen(cell.x, cell.y) && !_world.IsExcavated(cell.x, cell.y))
                cell = _world.WorldToCell(p);
            SocialMemoryStore mem = _socialAura != null && _socialAura.IsBootstrapped
                ? _socialAura.Memory : null;
            _collapse.TriggerCollapse(cell, sev, _crewWorkers, CrewWorldPos, mem, _managerRels, forced: true);
            DigHoodLog.Push($"DEV | FORCE {sev} @ ({cell.x},{cell.y})");
        }

        void DevForceDebrisHit(WorkerRuntime wr)
        {
            if (wr?.State == null || !wr.IsAlive) return;
            var zone = DebrisImpactZone.Overhead;
            CollapseInjuryResolver.ResolveHits(wr, CollapseSeverity.Blocking, zone);
            DigHoodLog.Push($"DEV | DEBRIS HIT → {wr.DisplayName}");
        }

        /// <summary>TEMP DEV: selected-worker commute / path / relocation readout.</summary>
        void DrawWorkerMovementDebug()
        {
            if (!DevMode.Enabled) return;
            var wr = FindCrewWorker(_selectedWorkerId);
            if (wr == null) return;
            var av = _presence.Get(wr.WorkerId);
            var L = _dayTracker.Get(wr.WorkerId);
            var asg = _assignments.GetAssignment(wr.WorkerId);
            int roster = -1;
            if (_crewWorkers != null)
            {
                for (int i = 0; i < _crewWorkers.Length; i++)
                    if (ReferenceEquals(_crewWorkers[i], wr)) { roster = i; break; }
            }

            string life = _crewPhase switch
            {
                CrewPhase.OnShift when L != null && L.ArrivedWork => "Working",
                CrewPhase.OnShift => "CommuteToWork (late)",
                CrewPhase.HeadingOut => "CommuteToWork",
                CrewPhase.HeadingHome => "CommuteToCamp",
                CrewPhase.CampEvening => "Camp",
                CrewPhase.Asleep => "Sleeping",
                _ => _crewPhase.ToString(),
            };

            string target = "—";
            Vector2 targetPos = default;
            if (_crewPhase == CrewPhase.HeadingHome
                || (_crewPhase == CrewPhase.CampEvening && L != null && !L.ArrivedHome))
            {
                target = "Camp";
                targetPos = roster >= 0 ? CampSlot(roster) : CampNavDestination;
            }
            else if (_crewPhase == CrewPhase.HeadingOut
                     || (_crewPhase == CrewPhase.OnShift && L != null && !L.ArrivedWork))
            {
                target = asg != null ? asg.JobType.ToString() : "Work";
                targetPos = GetInstrumentMorningPost(wr);
            }
            else if (asg != null && asg.JobType != JobType.Unassigned)
            {
                target = asg.JobType.ToString();
                targetPos = GetProviderOperatePoint(asg.JobType);
            }

            Vector2 pos = av != null ? av.PresencePosition : CrewWorldPos(wr);
            float dist = target != "—" ? Vector2.Distance(pos, targetPos) : 0f;

            string path = "OK";
            if (wr.State != null && wr.State.Incapacitated) path = "INVALID (incap)";
            else if (roster >= 0 && _personStranded != null && roster < _personStranded.Length
                     && _personStranded[roster])
                path = "BLOCKED";
            else if (av != null && _world != null)
            {
                var c = _world.WorldToCell(pos);
                if (!_world.IsTunnelOpen(c.x, c.y)) path = "INVALID (off-map)";
            }

            string loco = "Waiting";
            if (wr.State != null && wr.State.Incapacitated) loco = "Incapacitated";
            else if (path == "BLOCKED") loco = "Blocked";
            else if (life.Contains("Commute")
                     || (_crewPhase == CrewPhase.OnShift && L != null && !L.ArrivedWork))
                loco = "Walking";
            else if (life == "Working") loco = "Working";
            else if (life == "Camp" || life == "Sleeping") loco = "AtCamp";

            var box = new Rect(12f, Screen.height - 132f, 320f, 122f);
            DrawCyberPanel(box, lit: path != "OK");
            Block(box);
            float x = box.x + 8f, y = box.y + 4f, w = box.width - 16f;
            GUI.Label(new Rect(x, y, w, 12f), $"MOVE DEBUG · {wr.DisplayName}",
                LabelStyle(8, UiCyan, bold: true));
            y += 14f;
            GUI.Label(new Rect(x, y, w, 11f), $"LIFECYCLE: {life}", LabelStyle(8, UiWhite));
            y += 12f;
            GUI.Label(new Rect(x, y, w, 11f), $"TARGET: {target}", LabelStyle(8, UiMute));
            y += 12f;
            GUI.Label(new Rect(x, y, w, 11f), $"PATH: {path}   DIST: {dist:0.0}",
                LabelStyle(8, path == "OK" ? UiGreen : UiAmber));
            y += 12f;
            GUI.Label(new Rect(x, y, w, 11f), $"LOCOMOTION: {loco}", LabelStyle(8, UiMute));
            y += 12f;
            if (asg != null && asg.JobType == JobType.Excavation && _worker != null)
            {
                string dig = _worker.MachineActivityLabel;
                GUI.Label(new Rect(x, y, w, 11f),
                    $"EXCAV: {dig}  H={_worker.Heat:0}  plan={_worker.RouteCount}",
                    LabelStyle(8, dig == "MINING" ? UiGreen
                        : dig == "AWAITING ORDERS" || dig == "REFUSED" ? UiAmber
                        : UiMute));
                y += 12f;
            }
            GUI.Label(new Rect(x, y, w, 11f),
                $"LAST RELOC: {WorkerRelocationLog.LastSummary()}",
                LabelStyle(7, UiDim));
        }

        void DrawCollapseEventBanners()
        {
            if (_collapse == null) return;
            var banners = _collapse.Banners;
            if (banners == null || banners.Count == 0) return;
            float y = 50f;
            for (int i = 0; i < banners.Count; i++)
            {
                var b = banners[i];
                if (b == null) continue;
                float h = 54f;
                if (!string.IsNullOrEmpty(b.Line2)) h += 14f;
                if (!string.IsNullOrEmpty(b.Line3)) h += 14f;
                var r = new Rect(Screen.width * 0.5f - 160f, y, 320f, h);
                DrawCyberPanel(r, lit: b.Critical);
                Block(r);
                float ly = r.y + 6f;
                GUI.Label(new Rect(r.x + 10f, ly, r.width - 20f, 14f), b.Title,
                    LabelStyle(10, b.Critical ? UiAmber : UiCyan, bold: true));
                ly += 16f;
                if (!string.IsNullOrEmpty(b.Line1))
                {
                    GUI.Label(new Rect(r.x + 10f, ly, r.width - 20f, 13f), b.Line1,
                        LabelStyle(9, UiWhite, bold: true));
                    ly += 14f;
                }
                if (!string.IsNullOrEmpty(b.Line2))
                {
                    GUI.Label(new Rect(r.x + 10f, ly, r.width - 20f, 12f), b.Line2,
                        LabelStyle(8, UiMute));
                    ly += 13f;
                }
                if (!string.IsNullOrEmpty(b.Line3))
                {
                    GUI.Label(new Rect(r.x + 10f, ly, r.width - 20f, 12f), b.Line3,
                        LabelStyle(8, b.Critical ? new Color(1f, 0.35f, 0.3f) : UiDim));
                }
                y += h + 6f;
            }

            // Stability overlay readout (selected / excavator cell)
            if (_collapse.ShowStabilityOverlay && _world != null)
            {
                Vector2 p = _worker != null ? _worker.Position : BasecampPos;
                var c = _world.WorldToCell(p);
                var band = _collapse.BandAt(c.x, c.y);
                float risk = _collapse.RawRiskAt(c.x, c.y);
                int trapped = 0, incap = 0, blocks = 0;
                if (_crewWorkers != null)
                {
                    for (int i = 0; i < _crewWorkers.Length; i++)
                    {
                        var wr = _crewWorkers[i];
                        if (wr?.State == null) continue;
                        if (wr.State.TrappedFromCamp) trapped++;
                        if (wr.State.Incapacitated) incap++;
                    }
                }
                for (int i = 0; i < _collapse.Fields.Count; i++)
                    if (_collapse.Fields[i] != null && !_collapse.Fields[i].Cleared && _collapse.Fields[i].BlocksNav)
                        blocks++;
                float supportDist = _mineInfra != null ? _mineInfra.NearestSupportDist(p) : 999f;
                var panel = new Rect(12f, Screen.height - 118f, 280f, 100f);
                DrawCyberPanel(panel, lit: band >= TunnelStabilityBand.Unstable);
                GUI.Label(new Rect(panel.x + 8f, panel.y + 6f, 260f, 12f),
                    $"STABILITY  {_collapse.BandLabel(band)}", LabelStyle(9, UiCyan, bold: true));
                GUI.Label(new Rect(panel.x + 8f, panel.y + 22f, 260f, 11f),
                    $"Support dist {supportDist:0.0}  risk-band only (no %)", LabelStyle(8, UiDim));
                GUI.Label(new Rect(panel.x + 8f, panel.y + 36f, 260f, 11f),
                    $"Blockages {blocks}  Trapped {trapped}  Incap {incap}", LabelStyle(8, UiWhite));
                GUI.Label(new Rect(panel.x + 8f, panel.y + 50f, 260f, 11f),
                    $"Debris fields {_collapse.Fields.Count}  cell ({c.x},{c.y})", LabelStyle(8, UiMute));
                GUI.Label(new Rect(panel.x + 8f, panel.y + 64f, 260f, 11f),
                    $"Warn {_collapse.AuditWarnings}  Min {_collapse.AuditMinor}  Blk {_collapse.AuditBlocking}  Maj {_collapse.AuditMajor}",
                    LabelStyle(7, UiDim));
                _ = risk;
            }
        }

        void DevRosterInjury(WorkerRuntime wr, WorkerInjurySeverity sev)
        {
            if (wr == null || !wr.IsAlive) return;
            var type = sev switch
            {
                WorkerInjurySeverity.Minor => WorkerInjuryType.Bruising,
                WorkerInjurySeverity.Moderate => WorkerInjuryType.SevereSprain,
                // Serious arm: OffDuty + walkable (leg fractures route through INCAP / rescue)
                _ => WorkerInjuryType.BrokenArm,
            };
            var part = sev >= WorkerInjurySeverity.Serious
                ? WorkerBodyPart.Arm
                : sev == WorkerInjurySeverity.Moderate ? WorkerBodyPart.Knee : WorkerBodyPart.Arm;
            WorkerAccidentSystem.ApplyTypedInjury(
                wr, type, WorkerInjuryCause.TerrainFall, "DEV roster demo", part);
            DigHoodLog.Push($"DEV | roster injury {sev} → {wr.DisplayName}");
        }

        void DevRosterNeedsCare(WorkerRuntime wr)
        {
            if (wr == null || !wr.IsAlive || wr.State == null) return;
            wr.State.NeedsCare = true;
            if (wr.Injuries.Count == 0)
                DevRosterInjury(wr, WorkerInjurySeverity.Serious);
            else
                wr.Injuries.SyncNeedsCare(wr.State);
            DigHoodLog.Push($"DEV | NeedsCare → {wr.DisplayName}");
        }

        void DevRosterExhaustion(WorkerRuntime wr)
        {
            if (wr == null || !wr.IsAlive || wr.State == null) return;
            WorkerJobDemand.EnsureStaminaPrimed(wr);
            wr.State.SetStamina(wr.PhysicalStaminaMax * 0.05f, wr.PhysicalStaminaMax);
            wr.State.ExhaustionLatched = true;
            DigHoodLog.Push($"DEV | exhaustion → {wr.DisplayName}");
        }

        void DevRosterGrudge(WorkerRuntime wr)
        {
            if (wr == null || !wr.IsAlive || !_socialAura.IsBootstrapped) return;
            WorkerRuntime other = null;
            if (_crewWorkers != null)
            {
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    var o = _crewWorkers[i];
                    if (o != null && o.IsAlive && o.WorkerId != wr.WorkerId)
                    {
                        other = o;
                        break;
                    }
                }
            }
            if (other == null) return;
            ApplySocialDevPairPreset("Grudge", wr.WorkerId, other.WorkerId);
            DigHoodLog.Push($"DEV | grudge → {wr.DisplayName}↔{other.DisplayName}");
        }

        void DevRosterClearStatus(WorkerRuntime wr)
        {
            if (wr == null) return;
            if (wr.State != null)
            {
                wr.State.NeedsCare = false;
                wr.State.ExhaustionLatched = false;
                wr.State.ClearIncapacitated();
                wr.State.TrappedFromCamp = false;
                if (wr.IsAlive)
                {
                    WorkerJobDemand.EnsureStaminaPrimed(wr);
                    wr.State.SetStamina(wr.PhysicalStaminaMax, wr.PhysicalStaminaMax);
                }
            }
            wr.CampBody?.ClearStomach();
            // Clear injuries by ticking huge recovery — avoid private list wipe
            if (wr.Injuries != null)
            {
                for (int i = wr.Injuries.Active.Count - 1; i >= 0; i--)
                {
                    var inj = wr.Injuries.Active[i];
                    if (inj != null) inj.RecoveryGameHoursLeft = 0f;
                }
                wr.Injuries.TickRecovery(0.1f, 1f, sleeping: false);
                wr.Injuries.SyncNeedsCare(wr.State);
            }
            DigHoodLog.Push($"DEV | clear roster status → {wr.DisplayName}");
        }

        void DevForceMeal(CampMealQuality q)
        {
            if (_campLife == null) return;
            _campLife.MealServedTonight = false;
            var steward = _steward != null ? _steward.AssignedWorker : FindWorkerAssignedTo(JobType.Steward);
            SocialMemoryStore mem = _socialAura != null && _socialAura.IsBootstrapped
                ? _socialAura.Memory : null;
            CampMealSystem.ApplyMealToCrew(
                _crewWorkers, steward, _campLife, q, mem, _absoluteGameHours, _campRng);
            _sleepCamp?.NotifyMealServed(q);
            DigHoodLog.Push($"DEV | FORCE MEAL {_campLife.MealLabel}");
        }

        void DevStomach(WorkerRuntime wr, StomachUpsetSeverity sev)
        {
            if (wr == null || !wr.IsAlive) return;
            float hrs = sev switch
            {
                StomachUpsetSeverity.Mild => 6f,
                StomachUpsetSeverity.Moderate => 10f,
                _ => 14f,
            };
            wr.CampBody.ApplyStomach(sev, hrs);
            DigHoodLog.Push($"DEV | {sev} stomach → {wr.DisplayName}");
        }

        void EnterCampEvening()
        {
            ServeEveningMealIfNeeded();
            _crewPhase = CrewPhase.CampEvening;
            float untilMorning = HoursUntilMorning(_gameHour);
            float preferred = ShiftPlanner.PreferredSleepHours;
            // Camp fills leftover before preferred sleep; long commute → late arrival → shorter camp + sleep
            float campWant = Mathf.Max(0.2f, untilMorning - preferred);
            float campH = Mathf.Min(
                Mathf.Max(_shiftPlanner.ExpectedCampBufferHours, _shiftPlanner.ExpectedFreeCampHours * 0.45f),
                campWant);
            // Never eat the whole night — leave at least a thin sleep floor
            campH = Mathf.Min(campH, Mathf.Max(0.15f, untilMorning - 2.5f));
            _campEveningEndAbsolute = _absoluteGameHours + campH;
            if (_crewWorkers != null)
            {
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    var wr = _crewWorkers[i];
                    if (wr == null) continue;
                    // Don't force ArrivedHome — late walkers keep commuting into the house
                    if (i < _personArrived.Length && _personArrived[i])
                    {
                        var av = _presence.Get(wr.WorkerId);
                        EnterCampHouse(wr, av);
                    }
                }
            }
            DigHoodLog.Push(
                $"CAMP EVENING | {_shiftPlanner.FormatHours(campH)} free · then sleep ~{_shiftPlanner.FormatHours(Mathf.Max(0f, untilMorning - campH))}");
        }

        void EnterSleep()
        {
            // Meal should already have run at camp evening; keep as safety for SkipSleep paths
            ServeEveningMealIfNeeded();
            _crewPhase = CrewPhase.Asleep;
            _sleepRecoveryApplied01 = 0f;
            _sleepStartedAbsolute = _absoluteGameHours;
            // Available window until morning whistle — short commute leftover = less recovery
            float available = HoursUntilMorning(_gameHour);
            _idealSleepHoursThisNight = Mathf.Max(2.5f, available);
            _sleepRecoveryScale = Mathf.Clamp01(
                available / WorkerSleepRecovery.TypicalNightGameHours);
            // Hide at current camp presence — do NOT teleport; do NOT move providers
            if (_crewWorkers != null)
            {
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    var wr = _crewWorkers[i];
                    if (wr == null) continue;
                    var L = _dayTracker.Get(wr.WorkerId);
                    L.ArrivedHome = true;
                    L.Sleeping = true;
                    L.BedtimeGameHour = _gameHour;
                    var av = _presence.Get(wr.WorkerId);
                    if (av == null) continue;
                    av.ClearFollowing();
                    av.Hide();
                    av.SetDevForceShowHidden(_devForceShowHiddenAvatars);
                }
            }
            float sleepEst = HoursUntilMorning(_gameHour);
            DigHoodLog.Push(
                $"SLEEP | Bedtime {FormatClock()} · window ~{_shiftPlanner.FormatHours(sleepEst)}");
            _worker?.ResetHeatAfterRest();
        }

        void BeginHeadingOut(bool announce)
        {
            // Close sleep ledger only when waking from sleep
            if (_crewPhase == CrewPhase.Asleep || _sleepStartedAbsolute >= 0f)
                FinalizeSleepLedgerAndSummary();
            _crewPhase = CrewPhase.HeadingOut;
            _commuteTimer = 0f;
            if (_campLife != null) _campLife.MealServedTonight = false;
            if (_crewWorkers != null)
            {
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    var wr = _crewWorkers[i];
                    if (wr == null) continue;
                    wr.CampBody?.ClearMealTonight();
                    // Do NOT clear TrappedFromCamp here — stay at physical site until rescue/debris clear
                    var L = _dayTracker.Get(wr.WorkerId);
                    L.Sleeping = false;
                    L.WakeGameHour = _gameHour;
                    L.ArrivedWork = false;
                    ClearStaleStewardCareFlag(wr);
                }
            }

            int cleared = _collapse != null
                ? _collapse.ClearDebrisNearCamp(TunnelCollapseSystem.CampSafeRadiusWorld)
                : 0;
            if (cleared > 0)
                DigHoodLog.Push($"SHIFT START | Cleared {cleared} camp-zone debris cell(s)");

            RefreshProviderWorkPosts();

            // Ensure vacant/host person sprites stay off — only WorkerAvatars walk
            PersistentWorkerBody.HideOperatorBodiesOnHosts(
                _worker, _prospector, _hauler, _refiner, _engineer, _steward);

            EnsurePersonNav();
            for (int i = 0; i < _personArrived.Length; i++)
            {
                _personArrived[i] = false;
                _personStranded[i] = false;
                if (_personStuckTimer != null && i < _personStuckTimer.Length)
                    _personStuckTimer[i] = 0f;
            }
            for (int i = 0; i < _personNav.Length; i++)
            {
                if (_personNav[i] == null) continue;
                _personNav[i].CampReturnMode = false;
                _personNav[i].LateralOffset = 0f;
                _personNav[i].Invalidate();
            }

            // Reveal at current camp positions — physical walk to instruments. No teleport.
            if (_crewWorkers != null)
            {
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    var wr = _crewWorkers[i];
                    if (wr == null) continue;
                    var av = _presence.Get(wr.WorkerId);
                    if (av == null) continue;
                    av.ClearFollowing();
                    Vector2 p = av.PresencePosition;
                    var cell = _world.WorldToCell(p);
                    if (!_world.IsTunnelOpen(cell.x, cell.y))
                    {
                        // Catastrophic only: wake inside solid rock after bad sleep pos
                        if (TrySnapToNearestTunnel(ref p, AvatarCommuteRadius))
                            RelocateAvatar(av, p, "wake embedded in solid", "BeginHeadingOut");
                    }
                    if (_personLastCommutePos != null && i < _personLastCommutePos.Length)
                        _personLastCommutePos[i] = av.PresencePosition;
                    av.Show();
                    av.SetDevForceShowHidden(_devForceShowHiddenAvatars);
                }
            }

            if (announce)
                DigHoodLog.Push("SHIFT START | Walking to instruments — physical commute");
        }

        void EnterOnShift()
        {
            _crewPhase = CrewPhase.OnShift;
            _commuteTimer = 0f;
            RefreshProviderWorkPosts();
            // Providers stay overnighted — do NOT SoftTeleport hosts to bookmarks
            StampLiveProviderAssignments();
            // Seat only workers who physically arrived; late keep walking via TickLateCommuteToWork
            RefreshAllAvatarPresence();

            if (_hasExcavatorDigResume && _worker != null && _worker.RouteCount > 0)
                DigHoodLog.Push(
                    $"SHIFT START | Excavator still at dig face | Plan {_worker.RouteCount} cell{(_worker.RouteCount == 1 ? "" : "s")}");
            if (_prospector != null && _prospector.WorkMode == ProspectorWorkMode.Investigate)
                DigHoodLog.Push($"SHIFT START | Prospector resumes {_prospector.Investigation.PlayerWorkLabel}");
            if (_refiner != null && _refiner.IsInConsultation)
                DigHoodLog.Push("SHIFT START | Refiner resumes consult");
            if (_engineer != null && _engineer.IsInfrastructureWork)
                DigHoodLog.Push($"SHIFT START | Engineer resumes {_engineer.WorkLabel}");
            if (_worker != null && _worker.IsOverheated)
                DigHoodLog.Push("SHIFT LIVE | Excavator OVERHEATED — engineer should dispatch");

            DigHoodLog.Push(
                $"SHIFT LIVE | {_shiftPlanner.FormatHours(_shiftPlanner.PlannedWorkHours)}"
                + $" · {_shiftPlanner.WorkBandLabel} · jobs unlocked for arrivals");

            _socialAura.NotifyShiftStart(_crewWorkers);
            _dayTracker.BeginNewDayKeepStreaks(_crewWorkers);
            if (_crewWorkers != null)
            {
                for (int pi = 0; pi < _crewWorkers.Length; pi++)
                    _crewWorkers[pi]?.Priorities?.ResetShiftAccumulation();
            }
            // Ledger reset wipes ArrivedWork — restore anyone who already physically arrived this morning
            if (_crewWorkers != null)
            {
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    var wr = _crewWorkers[i];
                    if (wr == null) continue;
                    if (i < _personArrived.Length && _personArrived[i])
                        _dayTracker.Get(wr.WorkerId).ArrivedWork = true;
                    ClearStaleStewardCareFlag(wr);
                }
            }
            _managerRels.EnsureCrew(_crewWorkers);

            // Resume excavator dig against retained overnight plan
            if (_worker != null && _worker.RouteCount > 0)
                _worker.ResumeExecutionIfPossible();
        }

        /// <summary>
        /// SeekingStewardCare must not outlive NeedsCare — otherwise jobs soft-lock after sleep
        /// (operator seats at machine but CanPerform stays false).
        /// </summary>
        void ClearStaleStewardCareFlag(WorkerRuntime wr)
        {
            if (wr?.CampBody == null || wr.State == null) return;
            if (!wr.CampBody.SeekingStewardCare) return;
            if (wr.State.NeedsCare) return;
            if (InjuryResponse.EvaluateWorkStatus(wr) == WorkerInjuryWorkStatus.OffDuty) return;
            wr.CampBody.SeekingStewardCare = false;
            DigHoodLog.Push($"INJURY | {wr.DisplayName} care flag cleared — fit for work");
        }

        void FinalizeSleepLedgerAndSummary()
        {
            if (_crewWorkers == null) return;
            float slept = _sleepStartedAbsolute >= 0f
                ? Mathf.Max(0f, _absoluteGameHours - _sleepStartedAbsolute)
                : HoursUntilMorning(_gameHour);
            float ideal = Mathf.Max(4f, _idealSleepHoursThisNight);
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null || !wr.IsAlive) continue;
                var L = _dayTracker.Get(wr.WorkerId);
                if (L.SleepHours < slept * 0.5f)
                    L.SleepHours = slept; // ensure sleep accrued if tick missed
                L.SleepDeficit01 = Mathf.Clamp01(1f - (slept / ideal));
                OvertimePressure.FinalizeDayOvertimeStreak(L, _shiftPlanner.PlannedWorkHours);
                // Reasonable schedule slowly rebuilds manager trust
                bool reasonable = L.WorkHours <= ShiftPlanner.NominalWorkHours + 0.5f
                                  && L.SleepDeficit01 < 0.25f;
                if (reasonable)
                    _managerRels.TickGentleRecovery(wr, 1f, true);
            }
            _dayTracker.BuildSummary(_dayIndex, _crewWorkers, _managerRels, _shiftPlanner);
            _showDailySummary = true;
            _sleepStartedAbsolute = -1f;
        }

        /// <summary>Fast-forward night — jump to next 08:00 and walk out (avatars only).</summary>
        public void SkipSleep()
        {
            if (_crewPhase == CrewPhase.OnShift || _crewPhase == CrewPhase.HeadingOut)
                return;

            ServeEveningMealIfNeeded();
            float hoursLeft = HoursUntilMorning(_gameHour);
            // Short remaining window → less recovery (do not invent a full night).
            _idealSleepHoursThisNight = Mathf.Max(2.5f, hoursLeft);
            _sleepRecoveryScale = Mathf.Clamp01(
                hoursLeft / WorkerSleepRecovery.TypicalNightGameHours);
            float nightFrac = 1f; // fraction of *this* night's window
            // If already asleep with partial recovery, only apply the remainder
            float remaining = Mathf.Max(0f, nightFrac - _sleepRecoveryApplied01);
            if (_crewPhase != CrewPhase.Asleep)
            {
                // HeadingHome skip: apply scaled recovery for remaining hours only
                remaining = nightFrac;
                _sleepRecoveryApplied01 = 0f;
            }
            ApplyCrewSleepRecoveryFraction(remaining);
            _sleepRecoveryApplied01 = 1f;

            if (_gameHour >= ShiftStartHour)
            {
                _dayIndex++;
                _coopWorkPlaytest.EnsureDay(_dayIndex);
            }
            _gameHour = ShiftStartHour;
            _worker?.ResetHeatAfterRest();
            BeginHeadingOut(announce: true);
            ApplyDayNightLight();
        }

        float _sleepRecoveryApplied01;

        /// <summary>Real-time sleep tick while Asleep — prorates one full night of recovery.</summary>
        void TickCrewSleepRecovery(float deltaTime)
        {
            float ideal = Mathf.Max(4f, _idealSleepHoursThisNight);
            float nightRealSec = ideal * SecondsPerGameHour;
            if (nightRealSec <= 0.001f) return;
            float add = deltaTime / nightRealSec;
            float room = Mathf.Max(0f, 1f - _sleepRecoveryApplied01);
            add = Mathf.Min(add, room);
            if (add <= 0f) return;
            ApplyCrewSleepRecoveryFraction(add);
            _sleepRecoveryApplied01 += add;
        }

        void TickCrewDayAccrual(float gameHours)
        {
            if (gameHours <= 0f || _crewWorkers == null) return;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null || !wr.IsAlive) continue;
                var L = _dayTracker.Get(wr.WorkerId);
                switch (_crewPhase)
                {
                    case CrewPhase.OnShift:
                        _dayTracker.Accrue(wr.WorkerId, CrewDaySegment.Work, gameHours);
                        OvertimePressure.TickDuringWork(
                            wr, L, _managerRels, _shiftPlanner.PlannedWorkHours,
                            gameHours, _absoluteGameHours);
                        break;
                    case CrewPhase.HeadingHome:
                        if (L.ArrivedHome || (i < _personArrived.Length && _personArrived[i]))
                            _dayTracker.Accrue(wr.WorkerId, CrewDaySegment.CampFree, gameHours);
                        else
                            _dayTracker.Accrue(wr.WorkerId, CrewDaySegment.CommuteHome, gameHours);
                        break;
                    case CrewPhase.CampEvening:
                        _dayTracker.Accrue(wr.WorkerId, CrewDaySegment.CampFree, gameHours);
                        break;
                    case CrewPhase.Asleep:
                        _dayTracker.Accrue(wr.WorkerId, CrewDaySegment.Sleep, gameHours);
                        break;
                    case CrewPhase.HeadingOut:
                        if (L.ArrivedWork || (i < _personArrived.Length && _personArrived[i]))
                            break;
                        _dayTracker.Accrue(wr.WorkerId, CrewDaySegment.CommuteOut, gameHours);
                        break;
                }
            }
        }

        void ApplyCrewSleepRecoveryFraction(float nightFraction01)
        {
            if (nightFraction01 <= 0f || _crewWorkers == null) return;
            // nightFraction01 is already relative to this night's ideal window
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr?.Stats == null || wr.Injuries == null || wr.State == null || !wr.IsAlive)
                    continue;
                // Trapped / incap at collapse site — no proper sleep (exhaustion rises)
                if (wr.State.Incapacitated || wr.State.TrappedFromCamp)
                {
                    wr.State.Frustration = Mathf.Min(100f, wr.State.Frustration + 10f * nightFraction01);
                    wr.State.Morale = Mathf.Max(0f, wr.State.Morale - 8f * nightFraction01);
                    wr.State.MentalFatigue = Mathf.Min(100f, wr.State.MentalFatigue + 12f * nightFraction01);
                    // Tiny injury tick only — fractures do not sleep-heal
                    float trappedRec01 = (wr.Stats.Get(WorkerStatId.Recovery) - 1) / 19f;
                    wr.Injuries.TickRecovery(nightFraction01 * 0.15f, trappedRec01, sleeping: false);
                    wr.Injuries.SyncNeedsCare(wr.State);
                    continue;
                }
                float mealMul = wr.CampBody != null ? wr.CampBody.MealSleepRecoveryMul : 1f;
                // Meal softens recovery only — never a giant productivity mul.
                // _sleepRecoveryScale < 1 when bedtime left a short night.
                float adj = nightFraction01
                            * Mathf.Clamp(mealMul, 0.80f, 1.12f)
                            * Mathf.Clamp(_sleepRecoveryScale, 0.35f, 1f);
                wr.State.ApplySleepRecoveryFraction(adj, wr.PhysicalStaminaMax);
                // Sleep helps recovery rate but does not wipe fractures (see TickInjuryRecovery)
                float rec01 = (wr.Stats.Get(WorkerStatId.Recovery) - 1) / 19f;
                float sleepHoursScale = Mathf.Max(4f, _idealSleepHoursThisNight);
                wr.Injuries.TickRecovery(
                    nightFraction01 * sleepHoursScale * 0.35f * Mathf.Lerp(1f, mealMul, 0.35f),
                    rec01,
                    sleeping: true);
                wr.Injuries.SyncInjuryMeter(wr.State);
                wr.Injuries.SyncNeedsCare(wr.State);
                // Severe stomach disrupts sleep recovery slightly
                if (wr.CampBody != null && wr.CampBody.HasStomachUpset
                    && wr.CampBody.StomachUpset >= StomachUpsetSeverity.Moderate)
                {
                    wr.State.FocusState = Mathf.Max(8f, wr.State.FocusState - 4f * nightFraction01);
                    wr.State.MentalFatigue = Mathf.Min(100f, wr.State.MentalFatigue + 6f * nightFraction01);
                }
            }
        }

        void TickInjuryRecovery(float gameHoursDelta, bool sleeping)
        {
            if (gameHoursDelta <= 0f || _crewWorkers == null) return;
            // Sleep path already advances recovery in ApplyCrewSleepRecoveryFraction
            if (sleeping) return;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                // Unity may deserialize [Serializable] WorkerRuntime shells without ctor → null Stats/Injuries
                if (wr?.Stats == null || wr.Injuries == null || wr.State == null) continue;
                float rec01 = (wr.Stats.Get(WorkerStatId.Recovery) - 1) / 19f;
                wr.Injuries.TickRecovery(gameHoursDelta, rec01, sleeping: false);
                wr.Injuries.SyncInjuryMeter(wr.State);
                wr.Injuries.SyncNeedsCare(wr.State);
                ClearStaleStewardCareFlag(wr);
            }
        }

        void OnInjuryResponseApplied(WorkerRuntime wr, WorkerInjuryRecord rec)
        {
            if (wr == null || rec == null) return;
            bool seekCamp = InjuryResponse.AfterInjuryApplied(wr, rec, out string log);
            if (!string.IsNullOrEmpty(log))
                DigHoodLog.Push($"INJURY | {log}");
            FeedInjurySocialAftermath(wr, rec);
            if (seekCamp)
                BeginInjuryReturnToCamp(wr);
        }

        /// <summary>
        /// Feed existing Social Memory + Manager axes — no scripted emotional conclusions.
        /// </summary>
        void FeedInjurySocialAftermath(WorkerRuntime wr, WorkerInjuryRecord rec)
        {
            if (wr?.State == null || rec == null) return;
            if (rec.Severity < WorkerInjurySeverity.Moderate) return;

            SocialMemoryStore mem = _socialAura != null && _socialAura.IsBootstrapped
                ? _socialAura.Memory
                : null;
            float t = _absoluteGameHours;

            if (mem != null && rec.Severity >= WorkerInjurySeverity.Serious)
            {
                float str = rec.Severity >= WorkerInjurySeverity.Critical ? 0.72f : 0.58f;
                mem.Add(new SocialMemoryEntry
                {
                    Type = SocialMemoryType.SharedHardship,
                    Strength = str,
                    GameTime = t,
                    ObserverId = wr.WorkerId,
                    TargetId = wr.WorkerId,
                    Context = SocialContext.Emergency,
                    SourceRef = $"injury:{rec.Type}",
                    Significance = rec.Severity >= WorkerInjurySeverity.Critical
                        ? SocialMemorySignificance.Major
                        : SocialMemorySignificance.Significant,
                });

                Vector2 origin = CrewWorldPos(wr);
                if (_crewWorkers != null)
                {
                    for (int i = 0; i < _crewWorkers.Length; i++)
                    {
                        var w = _crewWorkers[i];
                        if (w == null || !w.IsAlive || w.WorkerId == wr.WorkerId) continue;
                        if (Vector2.Distance(CrewWorldPos(w), origin) > 5.5f) continue;
                        mem.Add(new SocialMemoryEntry
                        {
                            Type = SocialMemoryType.WitnessedSeriousAccident,
                            Strength = 0.55f,
                            GameTime = t,
                            ObserverId = w.WorkerId,
                            TargetId = wr.WorkerId,
                            Context = SocialContext.Emergency,
                            SourceRef = $"injury.witness:{rec.Type}",
                            Significance = SocialMemorySignificance.Significant,
                        });
                    }
                }
            }

            // Manager axis: only when the worker is already under pressure / overtime
            var ledger = _dayTracker != null ? _dayTracker.Get(wr.WorkerId) : null;
            bool pressure = wr.State.ExhaustionLatched
                || wr.State.Frustration >= 40f
                || (ledger != null && (ledger.WorkHours >= 9f || ledger.ConsecutiveOvertimeDays > 0));
            if (pressure && rec.Severity >= WorkerInjurySeverity.Moderate)
            {
                float resent = rec.Severity >= WorkerInjurySeverity.Serious ? 3.2f : 1.6f;
                _managerRels.Get(wr.WorkerId)?.Add(-0.8f, 0f, resent);
                if (mem != null)
                {
                    mem.Add(new SocialMemoryEntry
                    {
                        Type = SocialMemoryType.LetMeDown,
                        Strength = Mathf.Clamp01(0.4f + resent * 0.08f),
                        GameTime = t,
                        ObserverId = wr.WorkerId,
                        TargetId = ManagerRelationshipStore.ManagerId,
                        Context = SocialContext.SharedProblem,
                        SourceRef = $"injury.mgr:{rec.Type}",
                        Significance = rec.Severity >= WorkerInjurySeverity.Serious
                            ? SocialMemorySignificance.Significant
                            : SocialMemorySignificance.Ordinary,
                    });
                }
            }

            float repeat = InjuryResponse.RepeatedInjuryExtraFrustration(wr.Injuries);
            if (repeat > 0.1f)
                wr.State.AddFrustration(repeat);
        }

        /// <summary>
        /// Yield instruments in place; physically walk to camp for Steward care. No teleport.
        /// Assignment reserved (host cleared of operator body; provider stays).
        /// </summary>
        void BeginInjuryReturnToCamp(WorkerRuntime wr)
        {
            if (wr == null || !wr.IsAlive) return;
            if (wr.State != null && wr.State.Incapacitated) return;
            if (wr.CampBody == null) return;
            if (wr.CampBody.InjuryReturnActive || wr.CampBody.SeekingStewardCare) return;
            if (!InjuryResponse.CanIndependentWalk(wr)) return;

            wr.CampBody.InjuryReturnActive = true;
            wr.CampBody.SeekingStewardCare = false;
            wr.State.NeedsCare = true;

            var status = InjuryResponse.EvaluateWorkStatus(wr);
            float abandon = InjuryResponse.AbandonWorkFrustration(status);
            if (abandon > 0.1f)
                wr.State.AddFrustration(abandon);

            var asg = _assignments.GetAssignment(wr.WorkerId);
            var av = _presence.Get(wr.WorkerId);
            if (asg != null && asg.JobType != JobType.Unassigned)
            {
                // Yield provider work — keep assignment record reserved
                YieldHost(asg.JobType);
                PersistentWorkerBody.SetOperatorBodyVisibleForJob(
                    asg.JobType, _worker, _prospector, _hauler, _refiner, _engineer, _steward, false);
                if (av != null)
                {
                    if (PersistentWorkerBody.IsMachineCabinJob(asg.JobType) && _worker != null)
                        ExcavatorCabin.Exit(av, _worker.Position);
                    else
                    {
                        Vector2 exit = GetProviderExitPoint(asg.JobType);
                        float d = Vector2.Distance(av.PresencePosition, exit);
                        if (d > 0.85f)
                            RelocateAvatar(av, exit, "injury leave host", "BeginInjuryReturnToCamp");
                        else
                            av.SetPresencePosition(exit);
                        av.ClearFollowing();
                        av.Show();
                    }
                    av.SetDevForceShowHidden(_devForceShowHiddenAvatars);
                }
            }
            else if (av != null)
            {
                av.ClearFollowing();
                av.Show();
            }

            DigHoodLog.Push(
                $"INJURY | {wr.DisplayName} leaving post → camp care ({InjuryResponse.StatusLabel(status)})");
        }

        void TickInjuryCareReturns()
        {
            if (_crewWorkers == null || _world == null) return;
            EnsurePersonNav();
            Vector2 door = CampNavDestination;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr?.CampBody == null || !wr.IsAlive) continue;
                if (!wr.CampBody.InjuryReturnActive) continue;
                if (wr.State != null && wr.State.Incapacitated)
                {
                    wr.CampBody.InjuryReturnActive = false;
                    continue;
                }
                var av = _presence.Get(wr.WorkerId);
                if (av == null)
                {
                    wr.CampBody.InjuryReturnActive = false;
                    continue;
                }

                Vector2 target = CampSlot(i);
                float arrive = 0.55f;
                Vector2 p = av.PresencePosition;
                if ((target - p).sqrMagnitude <= arrive * arrive
                    || (door - p).sqrMagnitude <= arrive * arrive)
                {
                    wr.CampBody.InjuryReturnActive = false;
                    wr.CampBody.SeekingStewardCare = true;
                    wr.State.NeedsCare = true;
                    DigHoodLog.Push($"INJURY | {wr.DisplayName} arrived camp — seeking Steward");
                    continue;
                }

                float speed = WorkerLocomotion.WalkSpeedAt(
                    wr, _world, p, AvatarCommuteRadius, 0.95f, 0f, true);
                if (i < _personNav.Length && _personNav[i] != null)
                {
                    _personNav[i].CampReturnMode = true;
                    _personNav[i].Follow(
                        p, target, speed, AvatarCommuteRadius,
                        face: _ => { },
                        tryStep: (dir, step) => PersonAvatarTryStep(av, dir, step));
                    if (_personNav[i].Stranded)
                    {
                        DigHoodLog.Push(
                            $"INJURY | {wr.DisplayName} PATH BLOCKED returning to camp (no teleport)");
                    }
                }
                else
                    av.SetPresencePosition(Vector2.MoveTowards(p, target, speed * Time.deltaTime));
            }
        }

        WorkerAccidentReport _lastPresentedAccident;

        void PresentPendingAccidents()
        {
            var report = WorkerAccidentSystem.LastReport;
            if (report == null || ReferenceEquals(report, _lastPresentedAccident)) return;
            _lastPresentedAccident = report;

            var wr = FindCrewWorker(report.WorkerId);
            if (wr == null) return;

            string line1 = report.Headline;
            string line2 = report.Detail;
            if (report.Injury != null
                && report.Outcome == WorkerAccidentOutcome.FellInjured)
            {
                line1 = report.Headline;
                line2 = report.Injury.DisplayName;
            }

            var valence = report.Injury != null && report.Injury.Severity >= WorkerInjurySeverity.Serious
                ? SocialSpeechValence.Severe
                : report.Outcome >= WorkerAccidentOutcome.Fell
                    ? SocialSpeechValence.Negative
                    : SocialSpeechValence.Neutral;

            // JobContext is speech metadata only (label on Speech). Accident
            // lines/outcomes come from WorkerAccidentReport — never from job.
            var asg = _assignments.GetAssignment(wr.WorkerId);
            var jobCtx = asg != null
                ? JobContextFrom(asg.JobType)
                : WorkerBanter.JobContext.Unassigned;

            _banter.TrySaySocial(
                wr.WorkerId,
                wr.DisplayName,
                jobCtx,
                "Accident",
                WorkerStateClock.GameHours,
                valence,
                string.IsNullOrEmpty(line2) ? line1 : $"{line1}\n{line2}");

            DigHoodLog.Push(
                $"ACCIDENT | {wr.DisplayName} | {report.Headline}"
                + (string.IsNullOrEmpty(report.Detail) ? "" : $" | {report.Detail}"));
        }

        /// <summary>Game hours from <paramref name="hour"/> until next shift start.</summary>
        float HoursUntilMorning(float hour)
        {
            float start = ShiftStartHour;
            if (hour < start)
                return start - hour;
            return (24f - hour) + start;
        }

        void DiscoverGasNearCrew()
        {
            if (_world == null) return;
            if (_crewWorkers != null)
            {
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    var wr = _crewWorkers[i];
                    if (wr == null) continue;
                    var av = _presence.Get(wr.WorkerId);
                    if (av != null)
                        _world.DiscoverGasAround(av.PresencePosition);
                }
            }
            if (_crewPhase == CrewPhase.OnShift && _worker != null)
                _world.DiscoverGasAround(_worker.Position);
        }

        void ApplyDayNightLight()
        {
            if (_globalLight == null) return;
            // Underground — subtle day/night swing on global fill
            bool day = OnShiftHours;
            float target = day ? 0.038f : 0.014f;
            Color col = day
                ? new Color(0.42f, 0.45f, 0.52f)
                : new Color(0.22f, 0.28f, 0.42f);
            _globalLight.intensity = Mathf.MoveTowards(_globalLight.intensity, target, Time.deltaTime * 0.04f);
            _globalLight.color = Color.Lerp(_globalLight.color, col, Time.deltaTime * 1.2f);

            // Camp quieter when asleep; evening stays warmer/alive
            float nightQuiet = _crewPhase == CrewPhase.Asleep ? 1f
                : _crewPhase == CrewPhase.CampEvening ? 0.25f
                : _crewPhase == CrewPhase.HeadingHome ? 0.15f
                : 0f;
            _sleepCamp?.SetNightQuiet01(nightQuiet);

            if (_mineAmbience != null)
            {
                float volTarget = _crewPhase == CrewPhase.Asleep ? 0.22f
                    : _crewPhase == CrewPhase.CampEvening ? 0.36f
                    : 0.48f;
                _mineAmbience.volume = Mathf.MoveTowards(
                    _mineAmbience.volume, volTarget, Time.deltaTime * 0.15f);
                float pitchTarget = _crewPhase == CrewPhase.Asleep ? 0.94f : 1f;
                _mineAmbience.pitch = Mathf.MoveTowards(
                    _mineAmbience.pitch, pitchTarget, Time.deltaTime * 0.08f);
            }
        }

        void SyncRoutePinsToScanView()
        {
            // Dig route no longer gated on retired Scan View
            _worker?.SetRouteVisible(SelectedJobIs(JobType.Excavation));
        }

        void LateUpdate()
        {
            // After OnGUI so HUD clicks never become dig goals
            HandleMouse();
            // F0.5/F4: presence follows hosts only while OnShift operating
            SyncMovingAssignedAvatars();
            // Stage 1: social proximity after canonical avatar positions are current
            TickSocialAura();
        }

        SocialPresenceKind MapSocialPresence(WorkerRuntime wr)
        {
            switch (GetPhysicalState(wr))
            {
                case WorkerPhysicalState.Operating: return SocialPresenceKind.Operating;
                case WorkerPhysicalState.Idle: return SocialPresenceKind.Idle;
                case WorkerPhysicalState.CommutingHome: return SocialPresenceKind.CommutingHome;
                case WorkerPhysicalState.Sleeping: return SocialPresenceKind.Sleeping;
                case WorkerPhysicalState.CommutingToWork: return SocialPresenceKind.CommutingToWork;
                case WorkerPhysicalState.AtCamp: return SocialPresenceKind.Idle;
                case WorkerPhysicalState.Incapacitated:
                case WorkerPhysicalState.Dead:
                    return SocialPresenceKind.Sleeping;
                default: return SocialPresenceKind.Idle;
            }
        }

        void TickSocialAura()
        {
            if (_crewWorkers == null || !_socialAura.IsBootstrapped) return;
            float dt = _lastSocialGameHoursDelta;
            if (dt <= 0f) dt = Time.deltaTime / SecondsPerGameHour;
            int encBefore = _socialAura.TotalEncounters;
            _socialAura.Tick(
                _crewWorkers,
                _presence,
                dt,
                MapSocialPresence,
                id =>
                {
                    var asg = _assignments.GetAssignment(id);
                    return asg != null ? asg.JobType : JobType.Unassigned;
                },
                isAsleep: _crewPhase == CrewPhase.Asleep,
                campCenter: CampNavDestination,
                campRadius: 2.4f);

            // Stage 2: present newly resolved encounter (math already applied).
            if (_socialAura.TotalEncounters > encBefore && _socialAura.LastEncounter != null)
                TryPresentSocialEncounter(_socialAura.LastEncounter);

            // Rounds 3–5: present argument / fight / witness lines via existing banter.
            TryPresentSocialConflictBeats();

            _socialPlaytest.ObserveTick(
                _dayIndex,
                _socialAura,
                _crewWorkers,
                _presence,
                MapSocialPresence,
                dt,
                encBefore);

            TickSocialPresentation();
        }

        void TryPresentSocialConflictBeats()
        {
            var conflict = _socialAura?.Conflict;
            if (conflict == null) return;
            var pending = conflict.PendingPresent;
            if (pending == null || pending.Count == 0) return;

            // Present at most two beats per tick so banter stays readable.
            int shown = 0;
            for (int i = 0; i < pending.Count && shown < 2; i++)
            {
                var ev = pending[i];
                if (ev == null || string.IsNullOrEmpty(ev.Line)) continue;
                int speakerId = ev.IsWitness ? ev.WitnessId : ev.SpeakerId;
                if (speakerId <= 0) speakerId = ev.WorkerA;
                var wr = FindCrewWorker(speakerId);
                if (wr == null || !wr.IsAlive) continue;
                if (MapSocialPresence(wr) == SocialPresenceKind.Sleeping) continue;

                JobType job = _assignments.GetAssignment(speakerId)?.JobType ?? JobType.Unassigned;
                string role = ev.IsFight ? "conflict/fight"
                    : ev.IsWitness ? "conflict/witness"
                    : "conflict/argument";
                var valence = SocialSpeechVisuals.FromConflict(ev);
                if (TryAuthoredSocialBanter(speakerId, wr.DisplayName, job, role, ev.Line, valence))
                    shown++;
            }
            conflict.ClearPendingPresent();
        }

        void TryPresentSocialEncounter(SocialEncounterLog log)
        {
            if (log == null) return;
            var init = FindCrewWorker(log.InitiatorId);
            var target = FindCrewWorker(log.TargetId);
            if (init == null || target == null) return;

            JobType initJob = _assignments.GetAssignment(log.InitiatorId)?.JobType ?? JobType.Unassigned;
            JobType targetJob = _assignments.GetAssignment(log.TargetId)?.JobType ?? JobType.Unassigned;
            Vector2 initPos = _presence.Get(log.InitiatorId)?.PresencePosition ?? Vector2.zero;
            Vector2 targetPos = _presence.Get(log.TargetId)?.PresencePosition ?? Vector2.zero;

            // Presentation tone only — does not alter encounter resolution / deltas.
            SocialDialogueTone initTone = default;
            SocialDialogueTone respTone = default;
            if (_socialAura != null)
            {
                var relIT = _socialAura.World.Relation(log.InitiatorId, log.TargetId);
                var relTI = _socialAura.World.Relation(log.TargetId, log.InitiatorId);
                var mem = _socialAura.Memory;
                var memIT = mem != null
                    ? mem.GetToward(log.InitiatorId, log.TargetId)
                    : System.Array.Empty<SocialMemoryEntry>();
                var memTI = mem != null
                    ? mem.GetToward(log.TargetId, log.InitiatorId)
                    : System.Array.Empty<SocialMemoryEntry>();

                Vector2? excavPos = _worker != null ? _worker.Position : (Vector2?)null;
                var sit = SocialSituationFlags.FromWorkers(
                    init, target, initPos, _world, BasecampPos, _mineInfra, excavPos);
                if (log.Context == SocialContext.Camp) { /* camp talk allowed */ }
                if (init?.State != null && init.State.ClaustrophobicStress >= 70f)
                    SocialPersonKnowledge.Instance.ObserveConfinementFear(
                        log.TargetId, log.InitiatorId, init.State.ClaustrophobicStress, _absoluteGameHours);

                initTone = SocialDialogueTone.From(init, relIT, memIT, sit, log.TargetId, _absoluteGameHours);
                respTone = SocialDialogueTone.From(target, relTI, memTI, sit, log.InitiatorId, _absoluteGameHours);
            }

            _socialPresenter.TryEnqueue(
                log,
                init,
                target,
                initPos,
                targetPos,
                MapSocialPresence(init),
                MapSocialPresence(target),
                initJob,
                targetJob,
                Time.unscaledTime,
                initTone,
                respTone);

            // DEV playtest history only — does not affect social math.
            var pres = _socialPresenter.LastPresentation;
            _socialDevHistory.Add(new SocialDevEncounterStamp
            {
                Log = log,
                Day = _dayIndex,
                GameHour = _gameHour,
                Presented = pres.Presented,
                SuppressedDistance = pres.SuppressedDistance,
                SuppressedSleep = pres.SuppressedSleep,
                InitiatorLine = pres.InitiatorLine,
                ResponseLine = pres.ResponseLine,
                LinesQueued = pres.LinesQueued,
            });
            while (_socialDevHistory.Count > 10)
                _socialDevHistory.RemoveAt(0);

            _socialPlaytest.ObservePresentation(_dayIndex, _socialAura, pres.Presented);
        }

        void TickSocialPresentation()
        {
            _socialPresenter.Tick(Time.unscaledTime, line =>
                TryAuthoredSocialBanter(
                    line.WorkerId,
                    line.DisplayName,
                    line.JobContext,
                    line.Role,
                    line.Text,
                    line.Valence));
        }

        /// <summary>Social Aura speech — WorkerId-authored, priority over ambient banter.</summary>
        bool TryAuthoredSocialBanter(
            int workerId,
            string displayName,
            JobType jobContext,
            string role,
            string line,
            SocialSpeechValence valence = SocialSpeechValence.Neutral)
        {
            if (workerId <= 0 || string.IsNullOrEmpty(line)) return false;
            var wr = FindCrewWorker(workerId);
            string name = wr != null ? wr.DisplayName : displayName;
            if (string.IsNullOrEmpty(name)) name = $"Worker {workerId}";
            if (jobContext == JobType.Unassigned) jobContext = JobType.Prospecting;
            bool ok = _banter.TrySaySocial(
                workerId,
                name,
                JobContextFrom(jobContext),
                $"SocialAura/{role}",
                _absoluteGameHours,
                valence,
                line);
            if (ok && SocialSpeechVisuals.WantsEscalationPulse(valence))
                TriggerSocialEscalationPulse(workerId, valence);
            return ok;
        }

        void CycleSelectedPerson(int delta)
        {
            if (_crewWorkers == null || _crewWorkers.Length == 0) return;
            int n = _crewWorkers.Length;
            int idx = 0;
            for (int i = 0; i < n; i++)
            {
                if (_crewWorkers[i] != null && _crewWorkers[i].WorkerId == _selectedWorkerId)
                {
                    idx = i;
                    break;
                }
            }
            idx = (idx + delta) % n;
            if (idx < 0) idx += n;
            // Skip nulls
            for (int k = 0; k < n; k++)
            {
                int j = (idx + k) % n;
                if (_crewWorkers[j] != null)
                {
                    SelectPersonById(_crewWorkers[j].WorkerId);
                    return;
                }
            }
        }

        void UpdateStockpileHover()
        {
            _hoverPile = null;
            if (_yard == null) return;
            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse == null || cam == null) return;
            Vector2 screen = mouse.position.ReadValue();
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            _hoverPile = _yard.HitTest(world);
        }

        void HandleMouse()
        {
            if (_crewPhase != CrewPhase.OnShift) return;
            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse == null || cam == null) return;
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.isPressed || kb.leftAltKey.isPressed || kb.rightAltKey.isPressed)
                && !_scannerPlaceMode && !_scannerPlanMode) return;
            if (mouse.middleButton.isPressed) return;

            Vector2 screen = mouse.position.ReadValue();
            Vector2 guiPt = new(screen.x, Screen.height - screen.y);
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));

            // Always finalize / sample active paint stroke even if cursor crosses HUD
            if (_worker != null && SelectedJobIs(JobType.Excavation) && _worker.PaintPlan.IsStroking)
            {
                if (mouse.leftButton.wasReleasedThisFrame || !mouse.leftButton.isPressed)
                {
                    _worker.CommitPaintStroke();
                    UpdateExcavationWidthRing(world, guiPt);
                    return;
                }
                if (mouse.rightButton.wasPressedThisFrame)
                {
                    _worker.CancelPaintStroke();
                    UpdateExcavationWidthRing(world, guiPt);
                    return;
                }
                _worker.SamplePaintStroke(world);
                UpdateExcavationWidthRing(world, overHud: false); // stroking — keep ring on brush
                return;
            }

            if (IsOverHud(guiPt))
            {
                if (SelectedJobIs(JobType.Excavation))
                    _worker?.HideBrushWidthRing();
                return;
            }

            if (_scannerPlaceMode)
            {
                if (!SelectedJobIs(JobType.Prospecting)) return;
                if (mouse.leftButton.wasPressedThisFrame)
                    ConfirmScannerPlacement();
                return;
            }

            if (_scannerPlanMode)
            {
                if (!SelectedJobIs(JobType.Prospecting)) return;
                if (mouse.leftButton.wasPressedThisFrame)
                    ConfirmScannerPlan();
                return;
            }

            if (SelectedJobIs(JobType.Prospecting) && _prospector != null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                    _prospector.FaceToward(world);
                return;
            }

            if (!SelectedJobIs(JobType.Excavation) || _worker == null)
            {
                _worker?.HideBrushWidthRing();
                return;
            }

            // Planning / paint hover — faint width ring at tile under cursor
            UpdateExcavationWidthRing(world, overHud: false);

            // RMB: erase planned tiles under cursor (or cancel in-progress stroke)
            if (mouse.rightButton.wasPressedThisFrame)
            {
                if (_worker.PaintPlan.IsStroking)
                    _worker.CancelPaintStroke();
                else
                    _worker.ErasePaintAt(world);
                return;
            }

            // Tile-paint routing: hold LMB drag → release commits
            if (mouse.leftButton.wasPressedThisFrame)
            {
                bool replace = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
                if (replace)
                    _worker.ClearRoute();

                var op = _worker.AssignedWorker;
                if (op?.State != null
                    && op.State.ClaustrophobicStress >= ClaustrophobiaBands.SevereAt
                    && world.y < _worker.Position.y - 0.15f)
                {
                    _managerRels.Get(op.WorkerId)?.Add(-0.25f, 0f, 0.55f);
                    WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                        op.WorkerId,
                        WorkerStateEventType.ManagerCommunication,
                        3.2f,
                        "ForcedDeeper",
                        JobType.Excavation,
                        _worker.ProviderId ?? ""));
                }
                _worker.BeginPaintStroke(world);
                UpdateExcavationWidthRing(world, overHud: false);
                return;
            }
        }

        void UpdateExcavationWidthRing(Vector3 world, Vector2 guiPt) =>
            UpdateExcavationWidthRing(world, IsOverHud(guiPt));

        void UpdateExcavationWidthRing(Vector3 world, bool overHud)
        {
            if (_worker == null || !SelectedJobIs(JobType.Excavation))
            {
                _worker?.HideBrushWidthRing();
                return;
            }
            if (overHud && !_worker.PaintPlan.IsStroking)
            {
                _worker.HideBrushWidthRing();
                return;
            }
            _worker.UpdateBrushWidthRingAt(world, planningVisible: true);
        }

        bool IsOverHud(Vector2 guiPoint)
        {
            for (int i = 0; i < _hudBlockers.Count; i++)
                if (_hudBlockers[i].Contains(guiPoint)) return true;
            return false;
        }

        void FollowCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 t;
            if (_crewPhase == CrewPhase.Asleep && _sleepCamp != null)
                t = new Vector3(_sleepCamp.FirePos.x, _sleepCamp.FirePos.y, -10f);
            else
            {
                // F2: camera follows selected person (host if assigned, avatar if idle)
                var ctl = ResolveControlTarget();
                Transform follow = ResolvePhysicalFollowTransform(in ctl);
                if (follow == null) return;
                t = follow.position;
                t.z = -10f;
            }
            cam.transform.position = Vector3.Lerp(cam.transform.position, t, 1f - Mathf.Exp(-cameraFollow * Time.deltaTime));
        }

        void ResetMap()
        {
            ExitScannerPlanMode();
            ExitScannerPlacementMode();
            if (_fieldScanner != null)
            {
                Destroy(_fieldScanner.gameObject);
                _fieldScanner = null;
            }
            BuildMap();
            CountStats();
            _goldFoundCells = 0;
            _goldFoundSockets = 0;
            for (int i = _looseRoot.childCount - 1; i >= 0; i--)
                Destroy(_looseRoot.GetChild(i).gameObject);
            _worker.Setup(_world, _world.CellCenter(StartX, StartY), WorkerRadius,
                _goalMarker, OnBroke, OnDigImpact, _goalSprite);
            _balance.Bind(_worker);
            _balance.ResetRun();
            _prospector?.BindExcavator(_worker);
            _prospector?.BindRefiner(_refiner);
            _calc?.Reset();
            _hauler?.ResetToBase();
            _refiner?.ResetToBase();
            _prospector?.ResetTo(_world.CellCenter(StartX - 6, StartY - 2));
            _world.ClearRockStudy();
            _yard?.Reset();
            _scanView?.ClearHints();
            _scanHistory?.ClearAll();
            _playerTactical?.MarkDirty();
            _gasFx?.Rescan();
            _banter.Clear();
            _socialPresenter.ClearQueue();
            _hoverPile = null;
            RefreshProviderWorkPosts();
            _dayIndex = 1;
            _gameHour = 7.7f;
            _absoluteGameHours = 7.7f;
            _deepestExcavatedY = int.MaxValue;
            if (_mission01DevOverlay != null)
            {
                _mission01DevOverlay.Rebuild(_mission01.LastValidation);
                _mission01DevOverlay.Visible = _mission01.DevValidationVisible;
            }
            BeginHeadingOut(announce: false);
            var shine = _worldRoot != null ? _worldRoot.GetComponentInChildren<GoldVeinShine>() : null;
            shine?.Rescan();
            if (_tactical != null)
                _tactical.MarkDirty();
        }

        void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                cam = go.AddComponent<Camera>();
                go.tag = "MainCamera";
                go.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.orthographicSize = cameraSize;
            cam.backgroundColor = new Color(0.01f, 0.01f, 0.014f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            if (cam.GetComponent<UniversalAdditionalCameraData>() == null)
                cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            if (cam.GetComponent<TrackpadPan>() == null)
                cam.gameObject.AddComponent<TrackpadPan>();
        }

        void EnsureMineAmbience()
        {
            if (_mineAmbience != null)
            {
                if (_mineAmbience.clip != null && !_mineAmbience.isPlaying)
                    _mineAmbience.Play();
                return;
            }

            if (Time.unscaledTime < _ambienceRetryAt) return;
            _ambienceRetryAt = Time.unscaledTime + 1.5f;

            var clip = Resources.Load<AudioClip>("Audio/Background/new_background");
            if (clip == null)
                clip = Resources.Load<AudioClip>("Audio/Background/new background");
            if (clip == null)
            {
                if (!_ambienceLogged)
                {
                    DigHoodLog.Push("AUDIO | new background clip missing (check Resources import)");
                    _ambienceLogged = true;
                }
                return;
            }

            var listener = FindAnyObjectByType<AudioListener>();
            if (listener == null && Camera.main != null)
                Camera.main.gameObject.AddComponent<AudioListener>();
            AudioListener.pause = false;
            if (AudioListener.volume < 0.01f)
                AudioListener.volume = 1f;

            var go = new GameObject("MineAmbience");
            go.transform.SetParent(transform, false);
            _mineAmbience = go.AddComponent<AudioSource>();
            _mineAmbience.clip = clip;
            _mineAmbience.loop = true;
            _mineAmbience.playOnAwake = false;
            _mineAmbience.spatialBlend = 0f;
            _mineAmbience.priority = 32;
            _mineAmbience.bypassListenerEffects = true;
            _mineAmbience.volume = 0.48f;
            _mineAmbience.pitch = 1f;
            _mineAmbience.Play();
            DigHoodLog.Push("AUDIO | new background playing");
            _ambienceLogged = true;
        }

        void FrameCamera()
        {
            var cam = Camera.main;
            if (cam == null || _worker == null) return;
            var p = _worker.transform.position;
            cam.transform.position = new Vector3(p.x, p.y, -10f);
            cam.orthographicSize = cameraSize;
        }

        void EnsureGlobalLight()
        {
            foreach (var l in FindObjectsByType<Light2D>())
            {
                if (l.lightType != Light2D.LightType.Global) continue;
                _globalLight = l;
                l.intensity = 0.025f;
                l.color = new Color(0.35f, 0.4f, 0.55f);
                return;
            }
            var go = new GameObject("Global Light 2D");
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 0.025f;
            light.color = new Color(0.35f, 0.4f, 0.55f);
            _globalLight = light;
        }

        // Industrial cyberpunk mining HUD palette — thin glass over the dig
        static readonly Color UiBg = new(0.02f, 0.04f, 0.07f, 0.26f);
        static readonly Color UiBgHot = new(0.04f, 0.09f, 0.12f, 0.34f);
        static readonly Color UiCyan = new(0.25f, 0.92f, 1f, 1f);
        static readonly Color UiGreen = new(0.35f, 1f, 0.55f, 1f);
        static readonly Color UiAmber = new(1f, 0.72f, 0.22f, 1f);
        static readonly Color UiRed = new(1f, 0.32f, 0.28f, 1f);
        static readonly Color UiWhite = new(0.9f, 0.95f, 0.98f, 1f);
        static readonly Color UiDim = new(0.45f, 0.58f, 0.65f, 1f);
        static readonly Color UiMute = new(0.28f, 0.38f, 0.44f, 1f);

        // Legacy aliases used by older draw helpers in this file
        static readonly Color CpCyan = UiCyan;
        static readonly Color CpGold = UiAmber;
        static readonly Color CpDim = UiDim;
        static readonly Color CpPanel = UiBg;
        static readonly Color CpBtnIdle = new(0.05f, 0.1f, 0.14f, 0.32f);
        static readonly Color CpBtnOn = new(0.08f, 0.28f, 0.36f, 0.42f);

        float _uiPulse;
        bool _showBalanceHarness;
        readonly ExcavatorBalanceHarness _balance = new();

        /// <summary>Non-essential HUD panels — one pop-up at a time (tool strip under Tactical/Truth).</summary>
        enum HudPopupKind : byte
        {
            None = 0,
            Comms,
            Keys,
            Assign,
            Control,
            Sheet,
            Banter,
            Presence,
            Runtime,
            Activity,
            Balance,
            Social,
            Mission,
            Camp,
            Shift,
            Priorities,
            Talk,
            CrewTalk,
        }

        HudPopupKind _hudPopup;

        /// <summary>F3: open sheet target WorkerId (0 = closed). Person-owned, not role-index.</summary>
        int _openStatsWorkerId;
        /// <summary>Baseline snapshots keyed by WorkerId (supports hired ids beyond 1–5).</summary>
        readonly Dictionary<int, WorkerStats> _sheetBaselineByWorkerId = new(8);
        readonly Dictionary<int, bool> _sheetBaselineCaptured = new(8);
        readonly Dictionary<int, WorkerSheetProfile> _sheetProfileByWorkerId = new(8);

        void OnDrawGizmos()
        {
            if (DevMode.Enabled && _socialDevDrawWorld && _crewWorkers != null && _socialAura.IsBootstrapped)
            {
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    var wr = _crewWorkers[i];
                    if (wr == null) continue;
                    var av = _presence.Get(wr.WorkerId);
                    if (av == null) continue;
                    var actor = _socialAura.World.Get(wr.WorkerId);
                    if (actor == null) continue;
                    actor.RefreshExpression();
                    float r = SocialAuraLiveTuning.EffectiveReach(actor.Expression);
                    Vector3 p = new Vector3(av.PresencePosition.x, av.PresencePosition.y, 0f);
                    Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.18f);
                    Gizmos.DrawWireSphere(p, r);
                }

                // Exposed pair lines
                for (int i = 0; i < _crewWorkers.Length; i++)
                for (int j = i + 1; j < _crewWorkers.Length; j++)
                {
                    var a = _crewWorkers[i];
                    var b = _crewWorkers[j];
                    if (a == null || b == null) continue;
                    var avA = _presence.Get(a.WorkerId);
                    var avB = _presence.Get(b.WorkerId);
                    if (avA == null || avB == null) continue;
                    var actA = _socialAura.World.Get(a.WorkerId);
                    var actB = _socialAura.World.Get(b.WorkerId);
                    if (actA == null || actB == null) continue;
                    actA.RefreshExpression();
                    actB.RefreshExpression();
                    float dist = Vector2.Distance(avA.PresencePosition, avB.PresencePosition);
                    float falloff = SocialAuraLiveTuning.DistanceFalloff(
                        dist,
                        SocialAuraLiveTuning.EffectiveReach(actA.Expression),
                        SocialAuraLiveTuning.EffectiveReach(actB.Expression));
                    if (falloff <= 0.001f) continue;
                    var pair = _socialAura.Pair(a.WorkerId, b.WorkerId);
                    Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.25f + 0.55f * Mathf.Clamp01(pair.InteractionPressure));
                    Gizmos.DrawLine(
                        new Vector3(avA.PresencePosition.x, avA.PresencePosition.y, 0f),
                        new Vector3(avB.PresencePosition.x, avB.PresencePosition.y, 0f));
                }
            }

            if (!navDebugDraw || _world == null) return;
            for (int i = 0; i < _personNav.Length; i++)
            {
                var eng = _personNav[i]?.Engine;
                if (eng == null) continue;
                var raw = eng.LastRawPath;
                var smooth = eng.LastSmoothPath;
                if (raw != null && raw.Count > 1)
                {
                    Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.35f);
                    for (int k = 1; k < raw.Count; k++)
                        Gizmos.DrawLine(raw[k - 1], raw[k]);
                }
                if (smooth != null && smooth.Count > 1)
                {
                    Gizmos.color = new Color(0.2f, 1f, 0.45f, 0.9f);
                    for (int k = 1; k < smooth.Count; k++)
                        Gizmos.DrawLine(smooth[k - 1], smooth[k]);
                }
            }
        }

        void OnGUI()
        {
            _hudBlockers.Clear();
            _uiPulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.4f);

            var cardTitle = LabelStyle(12, UiCyan, bold: true);
            var cardSub = LabelStyle(10, UiMute);
            var barLabel = LabelStyle(9, UiCyan, bold: true);
            var barMute = LabelStyle(9, UiMute);
            var barDim = LabelStyle(9, UiDim);

            // ——— Top instrumentation bar (resources + clock in one slim strip) ———
            bool canSkip = _crewPhase == CrewPhase.Asleep
                || _crewPhase == CrewPhase.HeadingHome
                || _crewPhase == CrewPhase.CampEvening;
            const float barH = 34f;
            float barW = Mathf.Min(780f, Mathf.Max(560f, Screen.width - 180f));
            var topBar = new Rect(10, 8, barW, barH);
            DrawCyberPanel(topBar, lit: _crewPhase == CrewPhase.OnShift);
            Block(topBar);
            DrawCollapseEventBanners();
            if (DevMode.Enabled)
                DrawWorkerMovementDebug();

            if (DevMode.Enabled)
            {
                var devBadge = new Rect(Screen.width - 56f, Screen.height - 28f, 44f, 18f);
                DrawCyberPanel(devBadge, lit: true, accentOverride: UiAmber);
                GUI.Label(new Rect(devBadge.x + 6f, devBadge.y + 2f, 36f, 14f), "DEV",
                    LabelStyle(9, UiAmber, bold: true));
            }

            float px = topBar.x + 12f;
            float py = topBar.y + 8f;
            float rowH = 18f;

            void BarStat(string label, string value, Color valueCol, float labelW, float valueW)
            {
                GUI.Label(new Rect(px, py + 1f, labelW, rowH), label, barLabel);
                GUI.Label(new Rect(px + labelW, py - 1f, valueW, rowH), value,
                    LabelStyle(13, valueCol, bold: true));
                px += labelW + valueW + 10f;
            }

            void BarSep()
            {
                DrawVLine(px, topBar.y + 8f, barH - 16f, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.22f));
                px += 10f;
            }

            if (_calc != null)
            {
                BarStat("GOLD", $"{_calc.RefinedGold}", UiGreen, 36f, 28f);
                BarStat("DIA", $"{_calc.RefinedDiamond}", UiCyan, 28f, 28f);
                BarStat("DIRT", $"{_calc.DirtPieces}", UiCyan, 34f, 40f);
                BarStat("ORE", $"{_calc.GoldSockets + _calc.DiamondSockets}", UiWhite, 28f, 28f);
                BarStat("WASH", $"{_calc.CellsWashed}", UiWhite, 36f, 36f);
                BarSep();
                BarStat("DEPTH", $"{CurrentDepthMeters:0.0}m", UiAmber, 42f, 44f);
                BarSep();
                string haul = _calc.CarryPiles > 0
                    ? $"HAUL {_calc.CarryPiles}  R{_calc.CarryRockMass:0}/G{_calc.CarryGoldSockets}"
                    : "HAULER SEEKING…";
                GUI.Label(new Rect(px, py + 1f, 168f, rowH), haul, barDim);
                px += 172f;
            }

            // Clock cluster — right-aligned in the bar
            string phaseShort = _crewPhase switch
            {
                CrewPhase.OnShift => "SHIFT",
                CrewPhase.HeadingHome => "KNOCK OFF",
                CrewPhase.CampEvening => "CAMP",
                CrewPhase.Asleep => "ASLEEP",
                CrewPhase.HeadingOut => "START",
                _ => "",
            };
            float skipW = canSkip ? 110f : 0f;
            float clockClusterW = (canSkip ? 200f : 160f) + skipW;
            float cxClock = topBar.xMax - 12f - clockClusterW;
            if (px < cxClock - 8f)
            {
                DrawVLine(cxClock - 8f, topBar.y + 8f, barH - 16f,
                    new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.22f));
            }
            GUI.Label(new Rect(cxClock, py + 1f, 28f, rowH), $"D{_dayIndex}", barLabel);
            GUI.Label(new Rect(cxClock + 28f, py - 1f, 48f, rowH), FormatClock(),
                LabelStyle(13, UiAmber, bold: true));
            string shiftBand = $"{_shiftPlanner.PlannedWorkHours:0}H";
            GUI.Label(new Rect(cxClock + 76f, py + 1f, 36f, rowH), shiftBand,
                LabelStyle(9, _shiftPlanner.PlannedWorkHours <= 8.01f ? UiGreen : UiAmber, bold: true));
            GUI.Label(new Rect(cxClock + 112f, py + 1f, 72f, rowH), phaseShort, barMute);
            if (canSkip)
            {
                var skipR = new Rect(topBar.xMax - 12f - skipW, topBar.y + 5f, skipW, 24f);
                Block(skipR);
                if (DrawCyberButton(skipR, "SKIP // N", selected: false, accent: UiAmber))
                    SkipSleep();
            }

            // Mission HUD shares the right column with exclusive popups — hide while open.
            if (_hudPopup == HudPopupKind.None)
                DrawMission01Hud(Mathf.Max(topBar.xMax + 8f, Screen.width - 360f), topBar.yMax + 6f);

            // ——— Left crew column (scanner stacks above roster; never overlaps) ———
            float faceW = WorkerFaceMonitor.DefaultWidth;
            float faceGap = 5f;
            float cardW = 176f, cardH = 108f, cardGap = 5f;
            const float statsBtnW = 36f;
            float faceX = 10f;
            float cx = faceX + faceW + faceGap;
            float leftColW = faceW + faceGap + cardW + 6f + statsBtnW;
            float leftColRight = faceX + leftColW;

            bool showScannerHud = _prospector != null && SelectedJobIs(JobType.Prospecting);
            bool scannerExpanded = showScannerHud && (_scannerPlanMode
                || (_fieldScanner != null && _fieldScanner.State == ProspectorScannerState.Scanning));
            float scannerPanelH = !showScannerHud ? 0f
                : _scannerPlaceMode ? 78f
                : scannerExpanded ? 118f
                : 88f;

            float leftColY = topBar.yMax + 8f;
            if (showScannerHud)
                DrawHeavyScannerHud(faceX, leftColY, leftColW, scannerPanelH);

            float leftStackH = showScannerHud ? scannerPanelH : 0f;
            float cy = leftColY + leftStackH + (showScannerHud ? 8f : 0f);
            float sheetDockX = leftColRight + 14f;
            bool sheetOpen = _openStatsWorkerId > 0;
            float afterLeft = sheetOpen ? sheetDockX + 300f : leftColRight;
            // Roster fallback only — live speech anchors to world avatars (see DrawWorldSpeechBubbles)
            float banterX = afterLeft + 14f;
            _rosterMeterTooltip = null;
            _mgrRelHoverTip = null;

            if (_crewWorkers != null)
            {
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    var wr = _crewWorkers[i];
                    if (wr == null) continue;
                    float rowY = cy + (cardH + cardGap) * i;
                    var asg = _assignments.GetAssignment(wr.WorkerId);
                    var job = asg != null ? asg.JobType : JobType.Unassigned;
                    string jobLine = JobStatPreview.DisplayName(job);
                    string taskLab = WorkPriorityDirector.ActiveTaskShortLabel(wr);
                    if (!string.IsNullOrEmpty(taskLab))
                        jobLine = $"{jobLine} · {taskLab}";
                    string provLine = job == JobType.Unassigned
                        ? "none"
                        : ProviderLabelForAssignment(asg);
                    Color accent = AccentForWorkerId(wr.WorkerId);
                    var faceR = new Rect(faceX, rowY, faceW, cardH);
                    Block(faceR);
                    bool faceSel = wr.WorkerId == _selectedWorkerId;
                    WorkerFaceMonitor.Draw(faceR, wr, accent, _uiPulse, faceSel);
                    if (GUI.Button(faceR, GUIContent.none, GUIStyle.none))
                        SelectPersonById(wr.WorkerId);
                    DrawPersonRosterCard(
                        new Rect(cx, rowY, cardW, cardH),
                        wr, jobLine, provLine, accent, cardTitle, cardSub);
                    DrawRosterStatsButton(cx + cardW + 6f, rowY, cardH, wr.WorkerId, accent);
                    bool hasAvatar = _presence != null && _presence.Get(wr.WorkerId) != null;
                    if (!hasAvatar
                        && (job != JobType.Unassigned || _banter.GetForWorker(wr.WorkerId) != null))
                        DrawBanterBubble(banterX, rowY, wr);
                }
            }

            float rosterExtraY = cy + (cardH + cardGap) * Mathf.Max(5, _crewWorkers?.Length ?? 5) + 4f;
            // Reserve left stack only — do not invent space for the anomaly panel here
            // (that panel expands _leftHudRight when drawn). Inflating early shoved
            // speech into the empty mid-screen void.
            _leftHudContentTop = leftColY;
            _leftHudRight = afterLeft;
            _leftHudBottom = rosterExtraY;

            DrawWorldSpeechBubbles();

            if (sheetOpen)
                DrawWorkerStatsSheet(sheetDockX, cy, _openStatsWorkerId);

            if (SelectedJobIs(JobType.Hauling) && _hauler != null)
            {
                var goldBtn = new Rect(cx, rosterExtraY, cardW, 28f);
                Block(goldBtn);
                if (DrawCyberButton(goldBtn, _hauler.PreferGold ? "PRECIOUS FIRST // ON" : "PRECIOUS FIRST // OFF",
                        selected: _hauler.PreferGold, accent: UiAmber))
                    _hauler.TogglePreferGold();
                rosterExtraY += 32f;
                if (DevMode.Enabled)
                {
                    GUI.Label(new Rect(cx, rosterExtraY, cardW, 16f),
                        $"TRACK MUL  {_hauler.DebugTrackSpeedMul:0.00}×",
                        LabelStyle(9, UiDim));
                    rosterExtraY += 18f;
                }
            }

            if (SelectedJobIs(JobType.Engineering) && _engineer != null)
            {
                if (DevMode.Enabled)
                {
                    GUI.Label(new Rect(cx, rosterExtraY, cardW, 18f),
                        _engineer.DebugStatus,
                        LabelStyle(11, UiAmber));
                    rosterExtraY += 18f;
                    var coopNow = EvaluateExcavatorEngineerCoop();
                    if (coopNow.Active)
                    {
                        GUI.Label(new Rect(cx, rosterExtraY, cardW + 40f, 14f),
                            $"COOP Q {coopNow.Quality:0.00}  spd×{coopNow.DispatchSpeedMul:0.00}  dur×{coopNow.RepairDurationMul:0.00}",
                            LabelStyle(9, UiCyan));
                        rosterExtraY += 14f;
                        GUI.Label(new Rect(cx, rosterExtraY, cardW + 80f, 14f),
                            TruncateDev(coopNow.Factors ?? "", 52),
                            LabelStyle(8, UiDim));
                        rosterExtraY += 14f;
                        GUI.Label(new Rect(cx, rosterExtraY, cardW + 80f, 14f),
                            $"LAST  {ExcavatorEngineerCooperation.LastConsequence}  {TruncateDev(ExcavatorEngineerCooperation.LastConsequenceDetail, 40)}",
                            LabelStyle(8, UiMute));
                        rosterExtraY += 14f;
                    }
                    if (_engineer.HasDebugTarget)
                    {
                        var tc = _engineer.DebugTargetCell;
                        GUI.Label(new Rect(cx, rosterExtraY, cardW, 16f),
                            $"TARGET  ({tc.x},{tc.y})",
                            LabelStyle(9, UiCyan));
                        rosterExtraY += 16f;
                    }
                    if (_mineInfra != null)
                    {
                        GUI.Label(new Rect(cx, rosterExtraY, cardW, 16f),
                            $"LAMP {_mineInfra.LanternCount}  SUPPORT {_mineInfra.SupportCount}",
                            LabelStyle(9, UiDim));
                        rosterExtraY += 16f;
                    }
                    string lanDbg = _engineer.EvalLanternDebug;
                    string supDbg = _engineer.EvalSupportDebug;
                    if (!string.IsNullOrEmpty(lanDbg))
                    {
                        float lh = Mathf.Min(54f, 12f + lanDbg.Length * 0.12f);
                        GUI.Label(new Rect(cx, rosterExtraY, cardW + 80f, lh), lanDbg, LabelStyle(8, UiDim));
                        rosterExtraY += lh + 2f;
                    }
                    if (!string.IsNullOrEmpty(supDbg))
                    {
                        float sh = Mathf.Min(54f, 12f + supDbg.Length * 0.12f);
                        GUI.Label(new Rect(cx, rosterExtraY, cardW + 80f, sh), supDbg, LabelStyle(8, UiDim));
                        rosterExtraY += sh + 4f;
                    }
                    var dbgBtn = new Rect(cx, rosterExtraY, cardW, 28f);
                    Block(dbgBtn);
                    bool dbgOn = _infraDebug != null && _infraDebug.Visible;
                    if (DrawCyberButton(dbgBtn, dbgOn ? "INFRA DEBUG // ON" : "INFRA DEBUG // OFF",
                            selected: dbgOn, accent: new Color(1f, 0.55f, 0.22f)))
                    {
                        if (_infraDebug != null)
                            _infraDebug.Visible = !dbgOn;
                    }
                    rosterExtraY += 32f;
                }
            }

            if (SelectedJobIs(JobType.Refining) && _refiner != null)
            {
                var rockBtn = new Rect(cx, rosterExtraY, cardW, 28f);
                var goldBtn = new Rect(cx, rosterExtraY + 32f, cardW, 28f);
                var diaBtn = new Rect(cx, rosterExtraY + 64f, cardW, 28f);
                Block(rockBtn); Block(goldBtn); Block(diaBtn);
                if (DrawCyberButton(rockBtn, "PRIORITY // ORE ROCK",
                        selected: _refiner.Priority == RefinerPriority.OreRock, accent: UiDim))
                    _refiner.SetPriority(RefinerPriority.OreRock);
                if (DrawCyberButton(goldBtn, "PRIORITY // ORE GOLD",
                        selected: _refiner.Priority == RefinerPriority.GoldOre, accent: UiAmber))
                    _refiner.SetPriority(RefinerPriority.GoldOre);
                if (DrawCyberButton(diaBtn, "PRIORITY // ORE DIA",
                        selected: _refiner.Priority == RefinerPriority.DiamondOre, accent: UiCyan))
                    _refiner.SetPriority(RefinerPriority.DiamondOre);
                rosterExtraY += 96f;
            }

            _leftHudBottom = Mathf.Max(_leftHudBottom, rosterExtraY);

            // Excavator sheet docks beside the roster (not world-anchored)
            // (drawn above via DrawWorkerStatsSheet when _openStatsWorkerId is set)

            if (_hoverPile != null)
            {
                var tip = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 11,
                    alignment = TextAnchor.UpperLeft,
                    normal = { textColor = UiCyan },
                    padding = new RectOffset(10, 10, 8, 8)
                };
                var mouse = Mouse.current;
                float mx = mouse != null ? mouse.position.ReadValue().x : 200f;
                float my = mouse != null ? Screen.height - mouse.position.ReadValue().y : 200f;
                var tipR = new Rect(mx + 14, my + 14, 200, 118);
                DrawCyberPanel(tipR, lit: false);
                GUI.Label(new Rect(tipR.x + 10, tipR.y + 8, tipR.width - 16, tipR.height - 12),
                    _hoverPile.HoverInfo, tip);
            }

            // ——— Right overlays ———
            float bw = 140f, bh = 28f;
            float bx = Screen.width - bw - 12f;
            float by = 10f;

            bool tacPlayerOn = _playerTactical != null && _playerTactical.Visible;
            var rTacPlayer = new Rect(bx, by, bw, bh);
            Block(rTacPlayer);
            if (DrawCyberButton(rTacPlayer,
                    tacPlayerOn ? "TACTICAL // ON" : "TACTICAL VIEW",
                    selected: tacPlayerOn))
            {
                _playerTactical?.Toggle();
                if (_playerTactical == null || !_playerTactical.Visible)
                    _scanHistoryBrowserOpen = false;
            }

            if (tacPlayerOn)
            {
                var rHist = new Rect(bx, by + bh + 6f, bw, bh);
                Block(rHist);
                bool histOpen = _scanHistoryBrowserOpen
                    || (_scanHistory != null && _scanHistory.IsHistoricalMode);
                if (DrawCyberButton(rHist,
                        histOpen ? "HISTORY // ON" : "HISTORY [H]",
                        selected: histOpen, accent: UiAmber))
                    _scanHistoryBrowserOpen = !_scanHistoryBrowserOpen;
                by += bh + 6f;
            }

            bool tacOn = DevMode.Enabled && _tactical != null && _tactical.Visible;
            if (DevMode.Enabled)
            {
                var rTac = new Rect(bx, by + bh + 6, bw, bh);
                Block(rTac);
                if (DrawCyberButton(rTac, tacOn ? "TRUTH VIEW // ON" : "TRUTH VIEW", selected: tacOn, accent: UiAmber))
                    _tactical?.Toggle();
                by = rTac.yMax + 10f;
            }
            else
            {
                by = by + bh + 10f;
            }

            // Compact tool strip — non-essentials open as exclusive pop-ups
            DrawHudPopupToolStrip(bx, by, bw, 22f);

            DrawScanHistoryBrowser();
            DrawHistoricalTacticalBanner();
            if (DevMode.Enabled)
                DrawScanHistoryDevReadout();

            if (DevMode.Enabled)
            {
                if (_hudPopup == HudPopupKind.Runtime) DrawWorkerRuntimeDevPanel();
                if (_hudPopup == HudPopupKind.Assign) DrawWorkerAssignmentDevPanel();
                if (_hudPopup == HudPopupKind.Presence) DrawWorkerPresenceDevPanel();
                if (_hudPopup == HudPopupKind.Social) DrawSocialAuraDevPanel();
                if (_hudPopup == HudPopupKind.Control) DrawWorkerControlDevPanel();
                if (_hudPopup == HudPopupKind.Sheet) DrawWorkerSheetDevPanel();
                if (_hudPopup == HudPopupKind.Banter) DrawBanterDevPanel();
                if (_hudPopup == HudPopupKind.Activity) DrawProspectorDevActivityBox();
                if (_hudPopup == HudPopupKind.Balance || _showBalanceHarness) DrawBalanceHarnessPanel();
                if (_hudPopup == HudPopupKind.Mission) DrawMission01DevPanel();
            }
            if (_hudPopup == HudPopupKind.Keys) DrawKeybindingsPanel();
            if (_hudPopup == HudPopupKind.Comms) DrawDigHoodLog();
            if (_hudPopup == HudPopupKind.Camp) DrawCampLifePanel();
            if (_hudPopup == HudPopupKind.Shift) DrawShiftPlannerPanel();
            if (_hudPopup == HudPopupKind.Priorities) DrawPrioritiesPanel();
            if (_hudPopup == HudPopupKind.Talk) DrawManagerTalkPanel();
            if (_hudPopup == HudPopupKind.CrewTalk) DrawManagerCrewTalkPanel();
            if (DevMode.Enabled && _devPriorityPanel) DrawPriorityDevDiag();
            if (_showDailySummary) DrawDailySummaryOverlay();
            DrawManagerInterveneOverlay();
            DrawLastManagerTalkFlash();
            if (!string.IsNullOrEmpty(_mgrRelHoverTip))
            {
                _rosterMeterTooltip = _mgrRelHoverTip;
                _rosterMeterTooltipGui = _mgrRelHoverGui;
            }

            DrawFindingToast();
            DrawProspectorThinkingPanel();
            DrawTunnelWidthPlanningHud();
            DrawRosterMeterTooltip();
            DrawAnomalyHoverTooltip();

            // Recruitment V1 overlay — last so it covers HUD; cancel leaves crew untouched
            if (_hiring.IsOpen)
            {
                var hireResult = RecruitmentHiringUi.Draw(
                    _hiring,
                    _uiPulse,
                    (r, label, selected, accent) =>
                    {
                        Block(r);
                        return DrawCyberButton(r, label, selected, accent);
                    },
                    Block);
                if (hireResult == HiringUiResult.Cancel)
                    CancelRecruitmentHiring();
                else if (hireResult == HiringUiResult.Confirm)
                    ConfirmRecruitmentHiring();
            }
        }

        void ToggleDevMode()
        {
            DevMode.Toggle();
            if (!DevMode.Enabled)
                ApplyDevModeOffCleanup();
        }

        void ApplyDevModeOffCleanup()
        {
            if (IsDevOnlyHudPopup(_hudPopup))
                _hudPopup = HudPopupKind.None;
            _showBalanceHarness = false;
            _socialDevDrawWorld = false;
            if (_tactical != null)
                _tactical.Visible = false;
            if (_infraDebug != null)
                _infraDebug.Visible = false;
            if (_mission01 != null)
                _mission01.DevValidationVisible = false;
            if (_collapse != null)
                _collapse.ShowStabilityOverlay = false;
        }

        static bool IsDevOnlyHudPopup(HudPopupKind kind) => kind switch
        {
            HudPopupKind.Assign or HudPopupKind.Control or HudPopupKind.Sheet
                or HudPopupKind.Banter or HudPopupKind.Presence or HudPopupKind.Social
                or HudPopupKind.Runtime or HudPopupKind.Activity or HudPopupKind.Balance
                or HudPopupKind.Mission => true,
            _ => false,
        };

        void ToggleHudPopup(HudPopupKind kind)
        {
            if (IsDevOnlyHudPopup(kind) && !DevMode.Enabled)
                return;
            if (_hudPopup == kind)
            {
                _hudPopup = HudPopupKind.None;
                if (kind == HudPopupKind.Balance) _showBalanceHarness = false;
                return;
            }

            _hudPopup = kind;
            _showBalanceHarness = kind == HudPopupKind.Balance;
        }

        void DrawHudPopupToolStrip(float x, float y, float w, float h)
        {
            GUI.Label(new Rect(x, y, w, 12f), "GAME", LabelStyle(8, UiMute, bold: true));
            y += 14f;
            var startR = new Rect(x, y, w, h + 4f);
            Block(startR);
            if (DrawCyberButton(startR, "START GAME", selected: _hiring.IsOpen, accent: UiGreen))
                OpenRecruitmentHiring();
            y += h + 10f;

            var resetR = new Rect(x, y, w, h);
            Block(resetR);
            if (DrawCyberButton(resetR, "RESET CREW", selected: false, accent: UiAmber))
                ResetToDefaultCrew();
            y += h + 12f;

            GUI.Label(new Rect(x, y, w, 12f), "PANELS", LabelStyle(8, UiMute, bold: true));
            y += 14f;

            void StripBtn(string label, HudPopupKind kind, Color accent)
            {
                var r = new Rect(x, y, w, h);
                Block(r);
                if (DrawCyberButton(r, label, selected: _hudPopup == kind, accent: accent))
                    ToggleHudPopup(kind);
                y += h + 4f;
            }

            StripBtn("COMMS", HudPopupKind.Comms, UiCyan);
            StripBtn("KEYS", HudPopupKind.Keys, UiDim);
            StripBtn("CAMP", HudPopupKind.Camp, UiAmber);
            StripBtn("SHIFT", HudPopupKind.Shift, UiCyan);
            StripBtn("PRIORITIES", HudPopupKind.Priorities, UiGreen);
            StripBtn("TALK", HudPopupKind.Talk, UiGreen);
            StripBtn("CREW", HudPopupKind.CrewTalk, UiAmber);

            if (!DevMode.Enabled) return;

            y += 6f;
            GUI.Label(new Rect(x, y, w, 12f), "DEV", LabelStyle(8, UiAmber, bold: true));
            y += 14f;
            StripBtn("ASSIGN", HudPopupKind.Assign, UiGreen);
            StripBtn("CTRL", HudPopupKind.Control, UiGreen);
            StripBtn("SHEET", HudPopupKind.Sheet, UiAmber);
            StripBtn("BANTER", HudPopupKind.Banter, new Color(0.7f, 0.55f, 1f));
            StripBtn("PRESENCE", HudPopupKind.Presence, UiCyan);
            StripBtn("SOCIAL", HudPopupKind.Social, new Color(0.45f, 0.85f, 1f));
            StripBtn("RUNTIME", HudPopupKind.Runtime, UiDim);
            StripBtn("ACTIVITY", HudPopupKind.Activity, UiAmber);
            StripBtn("BALANCE", HudPopupKind.Balance, UiAmber);
            StripBtn("MISSION", HudPopupKind.Mission, UiGreen);
            if (DevMode.Enabled)
            {
                y += 4f;
                var pr = new Rect(x, y, w, 22f);
                Block(pr);
                if (DrawCyberButton(pr, _devPriorityPanel ? "PRIO DIAG ON" : "PRIO DIAG",
                        selected: _devPriorityPanel, accent: UiAmber))
                    _devPriorityPanel = !_devPriorityPanel;
                y += 24f;
            }
        }

        /// <summary>Right margin reserved for the always-visible panel tool strip.</summary>
        const float HudToolStripReserve = 152f;

        void DrawMission01Hud(float x, float y)
        {
            float w = 210f, h = 78f;
            // Keep on-screen when top bar is wide
            if (x + w > Screen.width - 12f)
                x = Mathf.Max(10f, Screen.width - w - 12f);
            var panel = new Rect(x, y, w, h);
            bool lit = _mission01.Outcome == Mission01Outcome.Active;
            DrawCyberPanel(panel, lit: lit);
            Block(panel);

            var title = LabelStyle(9, UiCyan, bold: true);
            GUI.Label(new Rect(panel.x + 10f, panel.y + 6f, w - 16f, 14f), "MISSION 01 // 14-DAY", title);

            string gold = _mission01.GoldFound ? "FOUND" : "NOT FOUND";
            string dia = _mission01.DiamondFound ? "FOUND" : "NOT FOUND";
            Color goldCol = _mission01.GoldFound ? UiGreen : UiMute;
            Color diaCol = _mission01.DiamondFound ? UiCyan : UiMute;
            GUI.Label(new Rect(panel.x + 10f, panel.y + 24f, 44f, 14f), "GOLD", LabelStyle(9, UiMute, bold: true));
            GUI.Label(new Rect(panel.x + 52f, panel.y + 22f, 90f, 16f), gold, LabelStyle(12, goldCol, bold: true));
            GUI.Label(new Rect(panel.x + 10f, panel.y + 42f, 70f, 14f), "DIAMONDS", LabelStyle(9, UiMute, bold: true));
            GUI.Label(new Rect(panel.x + 82f, panel.y + 40f, 90f, 16f), dia, LabelStyle(12, diaCol, bold: true));

            string days = _mission01.Outcome == Mission01Outcome.Failed
                ? "0"
                : $"{_mission01.DaysRemaining}";
            GUI.Label(new Rect(panel.x + 10f, panel.y + 58f, 100f, 14f), "DAYS REMAINING",
                LabelStyle(8, UiMute, bold: true));
            GUI.Label(new Rect(panel.x + 118f, panel.y + 56f, 70f, 16f), days,
                LabelStyle(13, UiAmber, bold: true));

            if (_mission01.Outcome == Mission01Outcome.Complete)
            {
                GUI.Label(new Rect(panel.x + 10f, panel.y + 6f, w - 20f, 14f), "MISSION COMPLETE",
                    LabelStyle(9, UiGreen, bold: true));
            }
            else if (_mission01.Outcome == Mission01Outcome.Failed)
            {
                GUI.Label(new Rect(panel.x + 10f, panel.y + 6f, w - 20f, 14f), "MISSION FAILED",
                    LabelStyle(9, UiRed, bold: true));
            }
        }

        void DrawMission01DevPanel()
        {
            if (!DevMode.Enabled) return;
            float pw = 320f, ph = 280f;
            float px = Screen.width - pw - 24f;
            float py = 96f;
            var panel = new Rect(px, py, pw, ph);
            DrawCyberPanel(panel, lit: true);
            Block(panel);

            float x = panel.x + 12f;
            float y = panel.y + 10f;
            float inner = pw - 24f;
            GUI.Label(new Rect(x, y, inner, 14f), "DEV // MISSION 01 VALIDATION",
                LabelStyle(10, UiAmber, bold: true));
            y += 18f;

            var v = _mission01.LastValidation;
            string status = v == null ? "NO DATA" : (v.Passed ? "PASS" : "FAIL");
            GUI.Label(new Rect(x, y, inner, 14f), $"MAP CHECK · {status}",
                LabelStyle(11, v != null && v.Passed ? UiGreen : UiRed, bold: true));
            y += 18f;

            if (v != null)
            {
                GUI.Label(new Rect(x, y, inner, 12f),
                    $"Gold softDist {v.GoldSoftDist} · approaches {v.GoldApproaches}",
                    LabelStyle(9, UiWhite));
                y += 14f;
                GUI.Label(new Rect(x, y, inner, 12f),
                    $"Dia softDist {v.DiamondSoftDist} · approaches {v.DiamondApproaches}",
                    LabelStyle(9, UiWhite));
                y += 14f;
                GUI.Label(new Rect(x, y, inner, 12f),
                    $"G cells {v.GoldCells} · D cells {v.DiamondCells} · Bed {v.BedrockCells} · Gas {v.GasCells}",
                    LabelStyle(9, UiDim));
                y += 14f;
                GUI.Label(new Rect(x, y, inner, 12f),
                    $"DirectN blocked {v.DirectNorthBlockedByHazard} · Tempt G/D {v.GoldTemptBlocked}/{v.DiamondTemptBlocked}",
                    LabelStyle(9, UiDim));
                y += 16f;
                if (v.Failures.Count > 0)
                {
                    GUI.Label(new Rect(x, y, inner, 12f), "FAILURES", LabelStyle(8, UiRed, bold: true));
                    y += 12f;
                    int n = Mathf.Min(4, v.Failures.Count);
                    for (int i = 0; i < n; i++)
                    {
                        GUI.Label(new Rect(x, y, inner, 12f), "· " + v.Failures[i], LabelStyle(8, UiMute));
                        y += 12f;
                    }
                }
            }

            y = panel.yMax - 64f;
            var rVal = new Rect(x, y, inner, 24f);
            Block(rVal);
            bool on = _mission01.DevValidationVisible;
            if (DrawCyberButton(rVal, on ? "ROUTE OVERLAY // ON" : "ROUTE OVERLAY",
                    selected: on, accent: UiAmber))
            {
                _mission01.DevValidationVisible = !on;
                if (_mission01DevOverlay != null)
                {
                    if (_mission01.LastValidation != null)
                        _mission01DevOverlay.Rebuild(_mission01.LastValidation);
                    _mission01DevOverlay.Visible = _mission01.DevValidationVisible;
                }
                if (_mission01.DevValidationVisible)
                    _tactical?.SetVisible(true);
            }

            y += 28f;
            var rRe = new Rect(x, y, inner, 24f);
            Block(rRe);
            if (DrawCyberButton(rRe, "REVALIDATE MAP", selected: false, accent: UiCyan))
            {
                if (_world != null)
                {
                    var layout = _mission01.Layout;
                    var validation = Mission01Geology.Validate(_world, layout);
                    _mission01.LastValidation = validation;
                    _mission01DevOverlay?.Rebuild(validation);
                    DigHoodLog.Push(validation.Passed
                        ? "MISSION 01 | Revalidate PASS"
                        : $"MISSION 01 | Revalidate FAIL ({validation.Failures.Count})");
                }
            }
        }

        public string AuditMission01Report()
        {
            var v = _mission01.LastValidation;
            if (v == null && _world != null)
            {
                var layout = Mission01Geology.MakeLayout(StartX, StartY);
                v = Mission01Geology.Validate(_world, layout);
                _mission01.LastValidation = v;
            }
            return v != null ? v.ToReport() : "# Mission 01 — Map Validation\n\n**Result:** FAIL\n\n- No world built.\n";
        }

        void DrawBalanceHarnessPanel()
        {
            if (!DevMode.Enabled) return;
            if (!_showBalanceHarness || _worker == null) return;

            const float panelW = 320f;
            const float panelH = 520f;
            float top = 96f;
            float maxH = Mathf.Max(280f, Screen.height - top - 14f);
            float useH = Mathf.Min(panelH, maxH);
            // Dock left of the right-edge panel tool strip
            var panel = new Rect(Screen.width - panelW - 12f - HudToolStripReserve, top, panelW, useH);
            DrawCyberPanel(panel, lit: true, accentOverride: UiAmber);
            Block(panel);

            var hdr = LabelStyle(10, UiAmber, bold: true);
            var mute = LabelStyle(9, UiMute);
            var val = LabelStyle(11, UiWhite, bold: true);
            var dim = LabelStyle(9, UiDim);

            float x = panel.x + 12f;
            float y = panel.y + 8f;
            float inner = panelW - 24f;

            GUI.Label(new Rect(x, y, inner, 14f), "BALANCE // EXCAVATOR", hdr);
            y += 16f;
            GUI.Label(new Rect(x, y, inner, 12f),
                $"ACTIVE  {ExcavatorBalanceHarness.ProfileLabel(_balance.Profile)}",
                LabelStyle(10, UiCyan, bold: true));
            y += 16f;
            DrawHLine(x, y, inner, new Color(UiAmber.r, UiAmber.g, UiAmber.b, 0.25f));
            y += 8f;

            float btnW = (inner - 6f) * 0.5f;
            float btnH = 22f;
            if (DrawCyberButton(new Rect(x, y, btnW, btnH), "BRUTE",
                    selected: _balance.Profile == ExcavatorTestProfile.Brute, accent: UiAmber))
                _balance.ApplyProfile(ExcavatorTestProfile.Brute);
            if (DrawCyberButton(new Rect(x + btnW + 6f, y, btnW, btnH), "TECH",
                    selected: _balance.Profile == ExcavatorTestProfile.Technician, accent: UiCyan))
                _balance.ApplyProfile(ExcavatorTestProfile.Technician);
            y += btnH + 4f;
            if (DrawCyberButton(new Rect(x, y, btnW, btnH), "PRO",
                    selected: _balance.Profile == ExcavatorTestProfile.Professional, accent: UiGreen))
                _balance.ApplyProfile(ExcavatorTestProfile.Professional);
            if (DrawCyberButton(new Rect(x + btnW + 6f, y, btnW, btnH), "COWBOY",
                    selected: _balance.Profile == ExcavatorTestProfile.Cowboy, accent: new Color(1f, 0.45f, 0.25f)))
                _balance.ApplyProfile(ExcavatorTestProfile.Cowboy);
            y += btnH + 4f;
            if (DrawCyberButton(new Rect(x, y, btnW, btnH), "ACE",
                    selected: _balance.Profile == ExcavatorTestProfile.Ace, accent: new Color(1f, 0.92f, 0.35f)))
                _balance.ApplyProfile(ExcavatorTestProfile.Ace);
            if (DrawCyberButton(new Rect(x + btnW + 6f, y, btnW, btnH), "GREEN",
                    selected: _balance.Profile == ExcavatorTestProfile.Green, accent: UiMute))
                _balance.ApplyProfile(ExcavatorTestProfile.Green);
            y += btnH + 4f;
            if (DrawCyberButton(new Rect(x, y, btnW, btnH), "BASELINE",
                    selected: _balance.Profile == ExcavatorTestProfile.Baseline, accent: UiDim))
                _balance.ApplyProfile(ExcavatorTestProfile.Baseline);
            if (DrawCyberButton(new Rect(x + btnW + 6f, y, btnW, btnH), "RESET RUN",
                    selected: false, accent: UiAmber))
                _balance.ResetRun();
            y += btnH + 8f;
            DrawHLine(x, y, inner, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.15f));
            y += 8f;

            var m = _balance.Metrics;
            void Row(string label, string value)
            {
                GUI.Label(new Rect(x, y, 150f, 15f), label, mute);
                GUI.Label(new Rect(x + 150f, y, inner - 150f, 15f), value, val);
                y += 15.5f;
            }

            Row("ROCK DESTROYED", $"{m.RockDestroyed}");
            Row("BEDROCK DESTROYED", $"{m.BedrockDestroyed}");
            Row("AVG SEC / ROCK", $"{m.AvgSecondsPerRock:0.00}");
            Row("AVG SEC / BEDROCK", $"{m.AvgSecondsPerBedrock:0.00}");
            Row("WEAK POINT", $"{m.WeakPointSuccesses}/{m.WeakPointAttempts}");
            Row("AVG HEAT", $"{m.AvgHeat:0.#}");
            Row("MAX HEAT", $"{m.MaxHeat:0.#}");
            Row("ZONE NORMAL %", $"{m.ZonePct(m.ZoneNormalSec):0.#}");
            Row("ZONE OPTIMAL %", $"{m.ZonePct(m.ZoneOptimalSec):0.#}");
            Row("ZONE DANGER %", $"{m.ZonePct(m.ZoneDangerSec):0.#}");
            Row("ZONE EXTREME %", $"{m.ZonePct(m.ZoneExtremeSec):0.#}");
            Row("OVERHEAT EVENTS", $"{m.OverheatEvents}");
            Row("INJURY EVENTS", $"{m.InjuryEvents}");
            Row("FRUST START→END", $"{m.FrustrationStart:0.#} → {m.FrustrationEnd:0.#}");
            Row("STAM START→END",
                $"{m.StaminaStart:0.#}/{m.StaminaMaxAtStart:0.#} → {m.StaminaEnd:0.#}");

            y += 6f;
            var tip = LabelStyle(9, UiDim);
            tip.wordWrap = true;
            GUI.Label(new Rect(x, y, inner, 32f),
                "B toggle · RESET RUN clears metrics +\nHeat/Stamina/Frust/Injury (map kept)",
                tip);

            var close = new Rect(panel.xMax - 54f, panel.y + 6f, 42f, 18f);
            if (DrawCyberButton(close, "×", selected: false, accent: UiAmber))
            {
                _showBalanceHarness = false;
                if (_hudPopup == HudPopupKind.Balance) _hudPopup = HudPopupKind.None;
            }
        }

        void DrawRosterStatsButton(float x, float y, float cardH, int workerId, Color accent)
        {
            // F3: ST targets WorkerId — works for assigned and Unassigned.
            var r = new Rect(x, y + (cardH - 28f) * 0.5f, 36f, 28f);
            Block(r);
            bool on = _openStatsWorkerId == workerId;
            if (DrawCyberButton(r, "ST", selected: on, accent: accent))
                ToggleStatsSheetForWorker(workerId);
        }

        readonly List<CrewRosterStatusUi.Badge> _rosterStatusBadges = new(6);

        void DrawPersonRosterCard(
            Rect r,
            WorkerRuntime wr,
            string jobLine,
            string providerLine,
            Color accent,
            GUIStyle titleStyle,
            GUIStyle subStyle)
        {
            bool on = wr != null && wr.WorkerId == _selectedWorkerId;
            Block(r);

            DrawCyberPanel(r, lit: on, accentOverride: on ? accent : default);
            var prev = GUI.color;
            GUI.color = on
                ? new Color(accent.r, accent.g, accent.b, 0.55f + 0.35f * _uiPulse)
                : new Color(accent.r, accent.g, accent.b, 0.2f);
            GUI.DrawTexture(new Rect(r.x + 6, r.y + 8, 2f, r.height - 16), Texture2D.whiteTexture);
            GUI.color = prev;

            // Compact act-on-this status icons (top-right) — hover for detail
            CrewRosterStatusUi.Collect(wr, _rosterStatusBadges, RosterGrudgeTooltip);
            float iconW = CrewRosterStatusUi.Draw(
                r, _rosterStatusBadges, GUI.skin.label,
                out string statusTip, out Vector2 statusGui);
            if (!string.IsNullOrEmpty(statusTip))
            {
                _rosterMeterTooltip = statusTip;
                _rosterMeterTooltipGui = statusGui;
            }

            float nameW = r.width - 22f - iconW - 4f;
            var tStyle = new GUIStyle(titleStyle) { normal = { textColor = on ? accent : UiDim } };
            GUI.Label(new Rect(r.x + 14, r.y + 4, nameW, 14),
                wr.DisplayName.ToUpperInvariant(), tStyle);
            GUI.Label(new Rect(r.x + 14, r.y + 18, r.width - 20, 12), jobLine, subStyle);
            GUI.Label(new Rect(r.x + 14, r.y + 30, r.width - 20, 11), providerLine,
                LabelStyle(7, UiMute));
            if (wr != null && !wr.IsAlive)
                GUI.Label(new Rect(r.x + 14, r.y + 41, r.width - 20, 9), "// DEAD",
                    LabelStyle(6, new Color(1f, 0.35f, 0.3f, 0.85f)));
            else if (on)
                GUI.Label(new Rect(r.x + 14, r.y + 41, r.width - 20, 9), "// SELECTED",
                    LabelStyle(6, new Color(accent.r, accent.g, accent.b, 0.65f)));

            DrawRosterStateBars(r, wr);

            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                SelectPersonById(wr.WorkerId);
        }

        string RosterGrudgeTooltip(WorkerRuntime wr)
        {
            if (wr == null || !_socialAura.IsBootstrapped || _crewWorkers == null)
                return null;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var other = _crewWorkers[i];
                if (other == null || other.WorkerId == wr.WorkerId || !other.IsAlive)
                    continue;
                var rel = _socialAura.Relation(wr.WorkerId, other.WorkerId);
                if (rel == null) continue;
                var mem = _socialAura.Memory.GetToward(wr.WorkerId, other.WorkerId);
                var cls = RelationshipClassifier.Classify(rel, mem, out _);
                if (cls == RelationshipClass.Grudge)
                {
                    return $"GRUDGE\nToward {other.DisplayName}\n"
                           + "Lasting hostility — social risk.\n"
                           + "Work with them carefully.";
                }
                // Active argument also surfaces as conflict
                if (_socialAura.Conflict != null
                    && _socialAura.Conflict.HasActiveArgument(wr.WorkerId, other.WorkerId))
                {
                    return $"ACTIVE CONFLICT\nWith {other.DisplayName}\n"
                           + "Argument in progress.\n"
                           + "Social pressure is high.";
                }
            }
            return null;
        }

        // Stamina green · Focus cyan · Frustration amber · Morale teal
        static readonly Color RosterStaminaCol = new(0.32f, 0.92f, 0.48f, 1f);
        static readonly Color RosterFocusCol = new(0.25f, 0.88f, 1f, 1f);
        static readonly Color RosterFrustrationCol = new(1f, 0.62f, 0.18f, 1f);
        static readonly Color RosterMoraleCol = new(0.45f, 0.95f, 0.78f, 1f);

        string _rosterMeterTooltip;
        Vector2 _rosterMeterTooltipGui;

        void DrawRosterStateBars(Rect card, WorkerRuntime wr)
        {
            if (wr?.State == null) return;
            var st = wr.State;
            float stamMax = Mathf.Max(1f, wr.PhysicalStaminaMax);
            float stam = Mathf.Clamp(st.PhysicalStamina, 0f, stamMax);

            const float barH = 9f;
            const float gap = 2f;
            float stackH = barH * 5f + gap * 4f;
            float x = card.x + 12f;
            float w = card.width - 20f;
            float y = card.yMax - stackH - 5f;

            DrawRosterMeterBar(new Rect(x, y, w, barH), stam / stamMax, RosterStaminaCol,
                "STAMINA", "STAM", $"{stam:0}/{stamMax:0}", "Physical reserve.");
            y += barH + gap;
            DrawRosterMeterBar(new Rect(x, y, w, barH), st.FocusState / 100f, RosterFocusCol,
                "FOCUS", "FOCUS", $"{st.FocusState:0}", "Current concentration.");
            y += barH + gap;
            DrawRosterMeterBar(new Rect(x, y, w, barH), st.Frustration / 100f, RosterFrustrationCol,
                "FRUSTRATION", "FRUST", $"{st.Frustration:0}", "Pressure and irritation.");
            y += barH + gap;
            DrawRosterMeterBar(new Rect(x, y, w, barH), st.Morale / 100f, RosterMoraleCol,
                "MORALE", "MORALE", $"{st.Morale:0}", "Broader outlook.");
            y += barH + gap;
            float claustro = st.ClaustrophobicStress / 100f;
            DrawRosterMeterBar(new Rect(x, y, w, barH), claustro,
                ClaustrophobiaBands.BarColor(st.ClaustrophobicStress),
                "CONFINEMENT STRESS", "CONFINE", $"{st.ClaustrophobicStress:0}",
                ClaustrophobiaSystem.BuildTooltip(wr));
        }

        void DrawRosterMeterBar(
            Rect r, float fill01, Color accent,
            string name, string shortLabel, string value, string desc)
        {
            fill01 = Mathf.Clamp01(fill01);
            var prev = GUI.color;
            GUI.color = new Color(0.04f, 0.06f, 0.08f, 0.62f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            float fill = fill01 * r.width;
            if (fill > 0.4f)
            {
                GUI.color = new Color(accent.r, accent.g, accent.b, 0.72f);
                GUI.DrawTexture(new Rect(r.x, r.y, fill, r.height), Texture2D.whiteTexture);
            }
            // Thin top edge — cyber hairline, not a glow slab
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.35f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), Texture2D.whiteTexture);
            GUI.color = prev;

            // Quiet in-bar identity — small, uppercase, does not compete with fill
            // Bypass LabelStyle floor so tags stay visibly smaller than card titles.
            var tag = new GUIStyle(GUI.skin.label)
            {
                fontSize = 7,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.78f, 0.88f, 0.92f, 0.9f) },
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0),
            };
            var val = new GUIStyle(GUI.skin.label)
            {
                fontSize = 7,
                fontStyle = FontStyle.Normal,
                normal = { textColor = new Color(0.92f, 0.96f, 1f, 0.78f) },
                alignment = TextAnchor.MiddleRight,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0),
            };
            GUI.Label(new Rect(r.x + 3f, r.y, r.width * 0.55f, r.height), shortLabel, tag);
            GUI.Label(new Rect(r.x + r.width * 0.4f, r.y, r.width * 0.58f - 3f, r.height),
                value, val);

            // Invisible hit — set tooltip without stealing the card click (checked before card button)
            var e = Event.current;
            if (e != null && e.type == EventType.Repaint && r.Contains(e.mousePosition))
            {
                _rosterMeterTooltip = $"{name}  {value}\n{desc}";
                _rosterMeterTooltipGui = e.mousePosition;
            }
        }

        void DrawTunnelWidthPlanningHud()
        {
            if (!SelectedJobIs(JobType.Excavation) || _worker == null) return;
            int w = _worker.PlannedTunnelWidth;
            int active = _worker.ActiveTunnelWidth;
            const float pw = 220f;
            const float ph = 92f;
            var r = new Rect(Screen.width * 0.5f - pw * 0.5f, Screen.height - ph - 18f, pw, ph);
            DrawCyberPanel(r, lit: true, accentOverride: TunnelWidthSpec.PreviewColor(w));
            Block(r);

            var title = LabelStyle(11, UiCyan, bold: true);
            var body = LabelStyle(10, UiWhite);
            var mute = LabelStyle(9, UiDim);
            GUI.Label(new Rect(r.x + 12, r.y + 8, pw - 24, 14),
                $"BRUSH {w}  ·  {TunnelWidthSpec.BrushCells(w)} CELLS  ·  {TunnelWidthSpec.Label(w)}", title);
            GUI.Label(new Rect(r.x + 12, r.y + 26, pw - 24, 14),
                $"{TunnelWidthSpec.ShortHint(w)}  ·  LMB paint · RMB erase", body);
            GUI.Label(new Rect(r.x + 12, r.y + 44, pw - 24, 14),
                $"Keys 1–5 · LMB plan · active dig W{active}", mute);
            string access = w <= 2 ? "Access: person only"
                : w == 3 ? "Access: tight machine/haul"
                : "Access: operational corridor";
            GUI.Label(new Rect(r.x + 12, r.y + 62, pw - 24, 14), access, mute);
        }

        void DrawRosterMeterTooltip()
        {
            if (string.IsNullOrEmpty(_rosterMeterTooltip)) return;
            var mouse = Mouse.current;
            Vector2 gui = _rosterMeterTooltipGui;
            if (mouse != null)
            {
                Vector2 screen = mouse.position.ReadValue();
                gui = new Vector2(screen.x, Screen.height - screen.y);
            }

            string[] lines = _rosterMeterTooltip.Split('\n');
            int lineCount = Mathf.Max(1, lines.Length);
            float lineH = 12f;
            float padY = 6f;
            float tw = 196f;
            float th = padY * 2f + lineCount * lineH + 2f;
            float tx = Mathf.Clamp(gui.x + 12f, 8f, Screen.width - tw - 8f);
            float ty = Mathf.Clamp(gui.y + 14f, 8f, Screen.height - th - 8f);
            var panel = new Rect(tx, ty, tw, th);
            DrawCyberPanel(panel, lit: true, accentOverride: UiCyan);
            Block(panel);

            float ly = panel.y + padY;
            for (int i = 0; i < lines.Length; i++)
            {
                bool head = i == 0;
                Color col = head ? UiWhite
                    : lines[i].StartsWith("Needs Care") || lines[i].StartsWith("NEEDS")
                        ? UiAmber
                        : lines[i].StartsWith("Estimated") ? UiDim
                        : UiMute;
                GUI.Label(new Rect(panel.x + 8f, ly, tw - 16f, lineH),
                    lines[i], LabelStyle(head ? 9 : 8, col, bold: head));
                ly += lineH;
            }
        }

        WorkerSheetProfile GetSheetProfile(int workerId)
        {
            return _sheetProfileByWorkerId.TryGetValue(workerId, out var p)
                ? p
                : WorkerSheetProfile.Baseline;
        }

        void CaptureSheetBaselinesForCrew()
        {
            if (_crewWorkers == null) return;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null) continue;
                EnsureSheetBaseline(wr.WorkerId);
            }
        }

        void EnsureSheetBaseline(int workerId)
        {
            if (workerId <= 0) return;
            if (_sheetBaselineCaptured.TryGetValue(workerId, out bool captured) && captured)
                return;
            var wr = FindCrewWorker(workerId);
            if (wr?.Stats == null) return;
            if (!_sheetBaselineByWorkerId.TryGetValue(workerId, out var snap) || snap == null)
            {
                snap = new WorkerStats();
                _sheetBaselineByWorkerId[workerId] = snap;
            }
            snap.CopyFrom(wr.Stats);
            _sheetBaselineCaptured[workerId] = true;
        }

        /// <summary>
        /// Apply a debug profile to a person's WorkerRuntime.Stats.
        /// Excavation ACE/BRUTE/… routes through balance harness when that person is excavating.
        /// Specialty ArchA/B/C use the current job's recipe (or are hidden in UI when Unassigned).
        /// </summary>
        void ApplySheetProfile(int workerId, WorkerSheetProfile profile)
        {
            var wr = FindCrewWorker(workerId);
            if (wr?.Stats == null) return;
            EnsureSheetBaseline(workerId);
            _sheetProfileByWorkerId[workerId] = profile;

            var job = GetAssignmentJob(wr);

            // Excavation benchmark tooling — only when this person is the excavator operator
            if (job == JobType.Excavation
                && _worker != null
                && ReferenceEquals(_worker.AssignedWorker, wr))
            {
                _balance.ApplyProfile(WorkerStatProfiles.ToExcavator(profile));
                return;
            }

            if (profile == WorkerSheetProfile.Baseline)
            {
                if (_sheetBaselineByWorkerId.TryGetValue(workerId, out var baseSnap) && baseSnap != null)
                    wr.Stats.CopyFrom(baseSnap);
                return;
            }

            int recipeRole = JobToLegacyRoleIndex(job);
            if (recipeRole >= 0)
            {
                wr.Stats.CopyFrom(WorkerStatProfiles.Build((byte)recipeRole, profile));
                return;
            }

            // Unassigned: Ace / Green as generic person presets (no job recipe)
            if (profile == WorkerSheetProfile.Ace)
                wr.Stats.CopyFrom(BuildGenericPersonPreset(19));
            else if (profile == WorkerSheetProfile.Green)
                wr.Stats.CopyFrom(BuildGenericPersonPreset(3));
            else if (_sheetBaselineByWorkerId.TryGetValue(workerId, out var snap) && snap != null)
                wr.Stats.CopyFrom(snap);
        }

        static WorkerStats BuildGenericPersonPreset(int value)
        {
            var s = WorkerStats.CreateBaseline();
            for (int i = 0; i < WorkerStats.StatCount; i++)
                s.Set((WorkerStatId)i, value);
            return s;
        }

        void DrawWorkerStatsSheet(float px, float py, int workerId)
        {
            var wr = FindCrewWorker(workerId);
            if (wr?.Stats == null) return;
            var stats = wr.Stats;
            EnsureSheetBaseline(workerId);

            var asg = _assignments.GetAssignment(workerId);
            var job = asg != null ? asg.JobType : JobType.Unassigned;
            var jobDef = JobStatPreview.Get(job);
            Color accent = AccentForWorkerId(workerId);

            // Keep excavator sheet profile in sync with balance harness when this person is excavating
            if (job == JobType.Excavation
                && _worker != null
                && ReferenceEquals(_worker.AssignedWorker, wr))
                _sheetProfileByWorkerId[workerId] = WorkerStatProfiles.FromExcavator(_balance.Profile);

            const float panelW = 300f;
            float maxBottom = Screen.height - 12f;
            float digHoodTop = Screen.height - 160f;
            float panelH = Mathf.Clamp(digHoodTop - py - 8f, 440f, 720f);
            if (py + panelH > maxBottom)
                panelH = Mathf.Max(360f, maxBottom - py);

            var panel = new Rect(px, py, panelW, panelH);
            DrawCyberPanel(panel, lit: true, accentOverride: accent);
            Block(panel);

            var hdr = LabelStyle(11, accent, bold: true);
            var dim = LabelStyle(9, UiDim);
            var val = LabelStyle(11, UiWhite, bold: true);
            var pillar = LabelStyle(9, UiCyan, bold: true);
            var mute = LabelStyle(9, UiMute);
            var sec = LabelStyle(9, UiCyan, bold: true);

            float x0 = panel.x + 12f;
            float y = panel.y + 8f;
            float innerW = panelW - 24f;

            // ——— Person identity (not role title) ———
            GUI.Label(new Rect(x0, y, innerW - 48f, 16f),
                wr.DisplayName.ToUpperInvariant(), hdr);
            y += 16f;
            if (DevMode.Enabled)
            {
                GUI.Label(new Rect(x0, y, innerW, 12f),
                    $"id {wr.WorkerId}  ·  {wr.StatsRefLabel}",
                    LabelStyle(8, UiMute));
                y += 13f;
            }
            GUI.Label(new Rect(x0, y, innerW, 12f),
                JobStatPreview.DisplayName(job),
                LabelStyle(9, UiCyan));
            y += 13f;
            if (DevMode.Enabled && job != JobType.Unassigned)
            {
                GUI.Label(new Rect(x0, y, innerW, 12f),
                    $"Provider: {ProviderLabelForAssignment(asg)}",
                    LabelStyle(8, UiAmber));
                y += 13f;
            }
            DrawHLine(x0, y, innerW, new Color(accent.r, accent.g, accent.b, 0.28f));
            y += 8f;

            if (DevMode.Enabled)
            {
                // ——— Person profiles (DEV) ———
                GUI.Label(new Rect(x0, y, innerW, 12f), "PERSON PROFILE", sec);
                y += 14f;
                float btnW = (innerW - 10f) / 3f;
                float btnH = 22f;
                WorkerSheetProfile[] personProfiles =
                {
                    WorkerSheetProfile.Baseline,
                    WorkerSheetProfile.Ace,
                    WorkerSheetProfile.Green,
                };
                for (int i = 0; i < personProfiles.Length; i++)
                {
                    var p = personProfiles[i];
                    var br = new Rect(x0 + i * (btnW + 5f), y, btnW, btnH);
                    Block(br);
                    bool sel = GetSheetProfile(workerId) == p;
                    string lab = p == WorkerSheetProfile.Baseline ? "BASE"
                        : p == WorkerSheetProfile.Ace ? "ACE" : "GREEN";
                    if (DrawCyberButton(br, lab, selected: sel, accent: WorkerStatProfiles.Accent(p)))
                        ApplySheetProfile(workerId, p);
                }
                y += btnH + 6f;

                // ——— Job-specific test presets (DEV) ———
                y = DrawJobTestPresetRow(ref y, x0, innerW, btnW, btnH, job, workerId);

                DrawHLine(x0, y, innerW, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.15f));
                y += 8f;
            }

            // ——— Personal conditions (always person-owned) ———
            y = DrawPersonalConditionsBlock(ref y, x0, innerW, wr, mute, sec);

            DrawHLine(x0, y, innerW, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.15f));
            y += 8f;
            DrawManagerRelationOnSheet(ref y, x0, innerW, wr);

            // ——— Machine context (Excavation only) ———
            if (job == JobType.Excavation && _worker != null
                && ReferenceEquals(_worker.AssignedWorker, wr))
            {
                y = DrawExcavatorMachineBlock(ref y, x0, innerW, mute, sec);
            }

            // ——— Prospecting context (optional) ———
            if (job == JobType.Prospecting && _prospector != null
                && ReferenceEquals(_prospector.AssignedWorker, wr))
            {
                y = DrawProspectingContextBlock(ref y, x0, innerW, sec);
            }

            // ——— Relevant-stat hint ———
            HashSet<WorkerStatId> relevant = null;
            if (job != JobType.Unassigned && jobDef.RelevantStats.Count > 0)
            {
                relevant = ScratchRelevantStats(jobDef.RelevantStats);
                string relTitle = jobDef.RelevantStatsAreProvisional
                    ? "RELEVANT STATS — PROVISIONAL"
                    : "JOB-RELEVANT (highlighted)";
                GUI.Label(new Rect(x0, y, innerW, 12f), relTitle,
                    LabelStyle(8, jobDef.RelevantStatsAreProvisional ? UiAmber : UiMute, bold: true));
                y += 14f;
            }

            DrawStatPillar(ref y, x0, innerW, "BODY", WorkerStatId.RawPower, 10, stats,
                pillar, dim, val, relevant);
            y += 4f;
            DrawStatPillar(ref y, x0, innerW, "MIND", WorkerStatId.Calibration, 10, stats,
                pillar, dim, val, relevant);
            y += 4f;
            DrawStatPillar(ref y, x0, innerW, "SOUL", WorkerStatId.Composure, 10, stats,
                pillar, dim, val, relevant);

            var close = new Rect(panel.xMax - 54f, panel.y + 6f, 42f, 18f);
            if (DrawCyberButton(close, "×", selected: false, accent: accent))
                _openStatsWorkerId = 0;
        }

        readonly HashSet<WorkerStatId> _sheetRelevantScratch = new(16);

        HashSet<WorkerStatId> ScratchRelevantStats(IReadOnlyList<WorkerStatId> src)
        {
            _sheetRelevantScratch.Clear();
            for (int i = 0; i < src.Count; i++)
                _sheetRelevantScratch.Add(src[i]);
            return _sheetRelevantScratch;
        }

        float DrawJobTestPresetRow(ref float y, float x0, float innerW, float btnW, float btnH,
            JobType job, int workerId)
        {
            int recipeRole = JobToLegacyRoleIndex(job);
            if (recipeRole < 0) return y;

            string section = job switch
            {
                JobType.Prospecting => "PROSPECTING TEST PRESET",
                JobType.Excavation => "EXCAVATION BENCHMARK",
                JobType.Hauling => "HAULING TEST (PROVISIONAL)",
                JobType.Refining => "REFINING TEST (PROVISIONAL)",
                JobType.Engineering => "ENGINEERING TEST (PROVISIONAL)",
                JobType.Steward => "STEWARD CAMP SUPPORT",
                _ => "JOB TEST PRESET",
            };
            GUI.Label(new Rect(x0, y, innerW, 12f), section, LabelStyle(8, UiAmber, bold: true));
            y += 14f;

            WorkerSheetProfile[] arches =
            {
                WorkerSheetProfile.ArchA,
                WorkerSheetProfile.ArchB,
                WorkerSheetProfile.ArchC,
            };
            for (int i = 0; i < arches.Length; i++)
            {
                var p = arches[i];
                var br = new Rect(x0 + i * (btnW + 5f), y, btnW, btnH);
                Block(br);
                bool sel = GetSheetProfile(workerId) == p;
                string lab = WorkerStatProfiles.Label((byte)recipeRole, p);
                if (DrawCyberButton(br, lab, selected: sel, accent: WorkerStatProfiles.Accent(p)))
                    ApplySheetProfile(workerId, p);
            }
            y += btnH + 6f;
            return y;
        }

        float DrawPersonalConditionsBlock(ref float y, float x0, float innerW,
            WorkerRuntime wr, GUIStyle mute, GUIStyle sec)
        {
            GUI.Label(new Rect(x0, y, innerW, 12f),
                DevMode.Enabled ? "PERSON STATE · V1.2C" : "PERSON STATE", sec);
            y += 14f;

            var st = wr.State;
            var asg = _assignments.GetAssignment(wr.WorkerId);
            var job = asg != null ? asg.JobType : JobType.Unassigned;
            var demand = ResolveDemandFor(wr, job);
            GUI.Label(new Rect(x0, y, innerW, 11f),
                $"Current: {demand.ActivityLabel}",
                LabelStyle(8, UiCyan, bold: true));
            y += 12f;
            if (DevMode.Enabled)
            {
                GUI.Label(new Rect(x0, y, innerW, 11f),
                    $"DEMAND  P {demand.Physical:0.00}  M {demand.Mental:0.00}  A {demand.Attention:0.00}",
                    mute);
                y += 13f;

                string tags = "";
                if (st.IsResting) tags += "RESTING ";
                if (WorkerJobDemand.StaminaRatio(wr) <= JobDemandTuning.ExhaustionEnterRatio
                    && st.StaminaPrimed)
                    tags += "EXHAUSTED ";
                if (st.MentalFatigue >= JobDemandTuning.HighMentalFatigue) tags += "HIGH MENTAL FATIGUE ";
                if (st.FocusState < JobDemandTuning.LowFocusState) tags += "LOW FOCUS ";
                if (!string.IsNullOrEmpty(tags))
                {
                    GUI.Label(new Rect(x0, y, innerW, 11f), tags.Trim(),
                        LabelStyle(8, UiAmber, bold: true));
                    y += 12f;
                }
            }

            float stamMax = wr.PhysicalStaminaMax;
            float stam = st.PhysicalStamina;
            if (!st.StaminaPrimed && stam <= 0f)
                stam = stamMax; // display full until first excavator prime
            string stamState = PersonalStaminaStateLabel(st, stam, stamMax);
            Color stamCol = stamState switch
            {
                "RESTING" => UiCyan,
                "EXHAUSTED" => new Color(1f, 0.3f, 0.28f, 1f),
                "TIRED" => UiAmber,
                _ => UiGreen,
            };
            DrawConditionRow(ref y, x0, innerW, "PHYS STAM",
                $"{stam:0.#}/{stamMax:0.#}", stamState, stam / Mathf.Max(1f, stamMax), stamCol, mute);

            DrawMeterRow(ref y, x0, innerW, "MENTAL FAT", st.MentalFatigue, mute,
                st.MentalFatigue >= 70f ? new Color(1f, 0.35f, 0.3f) :
                st.MentalFatigue >= 40f ? UiAmber : UiDim);
            DrawMeterRow(ref y, x0, innerW, "FOCUS", st.FocusState, mute,
                st.FocusState >= 70f ? UiGreen :
                st.FocusState >= 40f ? UiCyan : UiAmber);
            DrawMeterRow(ref y, x0, innerW, "FRUST.", st.Frustration, mute,
                st.Frustration >= 70f ? new Color(1f, 0.35f, 0.3f) :
                st.Frustration >= 40f ? UiAmber : UiDim);
            DrawMeterRow(ref y, x0, innerW, "MORALE", st.Morale, mute,
                st.Morale >= 65f ? UiGreen :
                st.Morale >= 40f ? UiCyan : UiAmber);
            DrawMeterRow(ref y, x0, innerW, "CONFINEMENT", st.ClaustrophobicStress, mute,
                ClaustrophobiaBands.BarColor(st.ClaustrophobicStress));

            float inj = st.Injury;
            Color injCol = inj >= FreeWorkerController.InjuryCareThreshold
                ? new Color(1f, 0.28f, 0.28f, 1f)
                : inj >= FreeWorkerController.InjurySlowThreshold
                    ? UiAmber
                    : UiDim;
            string injState = st.NeedsCare
                ? "NEEDS CARE"
                : inj >= FreeWorkerController.InjuryCareThreshold
                    ? "NEEDS CARE"
                    : inj >= FreeWorkerController.InjurySlowThreshold
                        ? "HURT"
                        : "OK";
            DrawConditionRow(ref y, x0, innerW, "INJURY",
                $"{inj:0.#}/100", injState, inj / 100f, injCol, mute);

            // Toilet / stomach — only when relevant (not permanent digestive HUD clutter)
            if (wr.CampBody != null)
            {
                var cb = wr.CampBody;
                if (cb.HasStomachUpset)
                {
                    y += 4f;
                    GUI.Label(new Rect(x0, y, innerW, 11f),
                        $"STOMACH  {cb.StomachUpset}  ·  {cb.StomachUpsetHoursLeft:0.#}h",
                        LabelStyle(8, UiAmber, bold: true));
                    y += 12f;
                }
                if (cb.ToiletTripActive || cb.ToiletNeed01 >= 0.55f || cb.HasStomachUpset)
                {
                    string toiletLbl = cb.ToiletTripActive
                        ? "AT TOILET"
                        : cb.ToiletNeed01 >= 0.82f ? "URGENT" : "RISING";
                    DrawConditionRow(ref y, x0, innerW, "TOILET",
                        $"{cb.ToiletNeed01 * 100f:0}%", toiletLbl, cb.ToiletNeed01,
                        cb.ToiletNeed01 >= 0.82f ? UiAmber : UiDim, mute);
                }
            }

            // Active typed injuries (person-owned)
            if (wr.Injuries != null && wr.Injuries.Count > 0)
            {
                y += 6f;
                GUI.Label(new Rect(x0, y, innerW, 11f), "ACTIVE INJURIES",
                    LabelStyle(8, UiAmber, bold: true));
                y += 12f;
                for (int i = 0; i < wr.Injuries.Active.Count; i++)
                {
                    var rec = wr.Injuries.Active[i];
                    if (rec == null || !rec.Active) continue;
                    int days = Mathf.Max(1, Mathf.CeilToInt(rec.RecoveryGameHoursLeft / 24f));
                    var row = new Rect(x0, y, innerW, 12f);
                    Color sevCol = rec.Severity >= WorkerInjurySeverity.Serious
                        ? new Color(1f, 0.32f, 0.3f)
                        : rec.Severity >= WorkerInjurySeverity.Moderate ? UiAmber : UiDim;
                    GUI.Label(row,
                        $"{rec.DisplayName}  ·  {rec.SeverityLabel}  ·  ~{days}d",
                        LabelStyle(7, sevCol));
                    if (row.Contains(Event.current.mousePosition))
                        GUI.tooltip = rec.HoverSummary();
                    y += 12f;
                }
                if (!string.IsNullOrEmpty(GUI.tooltip))
                {
                    var tip = new Rect(x0, y, innerW, 54f);
                    GUI.color = new Color(0.04f, 0.08f, 0.12f, 0.92f);
                    GUI.DrawTexture(tip, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(tip.x + 4, tip.y + 2, tip.width - 8, tip.height - 4),
                        GUI.tooltip, LabelStyle(6, UiWhite));
                    y += 56f;
                }
            }

            if (DevMode.Enabled)
            {
                y += 4f;
                GUI.Label(new Rect(x0, y, innerW, 11f),
                    $"WALK×{WorkerLocomotion.InjuryMoveMul(wr):0.00}  MANUAL×{WorkerInjuryConsequences.ManualWorkMul(wr.Injuries):0.00}  LOAD×{WorkerInjuryConsequences.LoadCarryMul(wr.Injuries):0.00}",
                    mute);
                y += 12f;
                GUI.Label(new Rect(x0, y, innerW, 11f),
                    $"Traits  Stam {wr.Stats.Get(WorkerStatId.Stamina)}  Rec {wr.Stats.Get(WorkerStatId.Recovery)}  " +
                    $"Foc {wr.Stats.Get(WorkerStatId.Focus)}  Comp {wr.Stats.Get(WorkerStatId.Composure)}  " +
                    $"Det {wr.Stats.Get(WorkerStatId.Determination)}  Tol {wr.Stats.Get(WorkerStatId.Tolerance)}",
                    mute);
                y += 14f;
                GUI.Label(new Rect(x0, y, innerW, 10f),
                    $"StateRef {wr.StateRefLabel}", mute);
                y += 12f;

                y = DrawRecentStateEventsBlock(ref y, x0, innerW, wr, mute, sec);
            }

            DrawHLine(x0, y, innerW, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.15f));
            y += 8f;
            return y;
        }

        float DrawRecentStateEventsBlock(ref float y, float x0, float innerW,
            WorkerRuntime wr, GUIStyle mute, GUIStyle sec)
        {
            if (!DevMode.Enabled) return y;
            GUI.Label(new Rect(x0, y, innerW, 12f), "RECENT STATE EVENTS · V1.2B", sec);
            y += 14f;
            var hist = wr.EventHistory;
            if (hist == null || hist.Items.Count == 0)
            {
                GUI.Label(new Rect(x0, y, innerW, 12f), "(none)", mute);
                y += 14f;
                return y;
            }

            int start = Mathf.Max(0, hist.Items.Count - 8);
            for (int i = hist.Items.Count - 1; i >= start; i--)
            {
                var r = hist.Items[i];
                if (r?.Event == null) continue;
                var e = r.Event;
                GUI.Label(new Rect(x0, y, innerW, 11f),
                    $"[{e.GameHours:0.0}h] {e.EventType}",
                    LabelStyle(8, UiCyan, bold: true));
                y += 11f;
                GUI.Label(new Rect(x0, y, innerW, 10f),
                    $"{e.Source} · {e.JobType}",
                    mute);
                y += 10f;
                string deltas = $"Frust {Signed(r.DeltaFrustration)}  Morale {Signed(r.DeltaMorale)}";
                if (Mathf.Abs(r.DeltaMentalFatigue) > 0.05f)
                    deltas += $"  MF {Signed(r.DeltaMentalFatigue)}";
                if (Mathf.Abs(r.DeltaFocusState) > 0.05f)
                    deltas += $"  FS {Signed(r.DeltaFocusState)}";
                GUI.Label(new Rect(x0, y, innerW, 10f), deltas, LabelStyle(8, UiWhite));
                y += 12f;
            }
            return y;
        }

        static string Signed(float v) =>
            v >= 0f ? $"+{v:0.#}" : $"{v:0.#}";

        void DrawMeterRow(ref float y, float x0, float innerW, string label, float value01to100,
            GUIStyle mute, Color col)
        {
            DrawConditionRow(ref y, x0, innerW, label,
                $"{value01to100:0.#}/100", "", value01to100 / 100f, col, mute);
        }

        static string PersonalStaminaStateLabel(WorkerState st, float stam, float stamMax)
        {
            if (st.IsResting) return "RESTING";
            float r = stam / Mathf.Max(1f, stamMax);
            if (r <= FreeWorkerController.StaminaExhaustedRatio) return "EXHAUSTED";
            if (r <= FreeWorkerController.StaminaTiredRatio) return "TIRED";
            return "OK";
        }

        float DrawExcavatorMachineBlock(ref float y, float x0, float innerW,
            GUIStyle mute, GUIStyle sec)
        {
            GUI.Label(new Rect(x0, y, innerW, 12f), "MACHINE", sec);
            y += 14f;

            Color heatCol = _worker.HeatZone switch
            {
                ExcavatorHeatZone.Optimal => UiGreen,
                ExcavatorHeatZone.Danger => UiAmber,
                ExcavatorHeatZone.Extreme => new Color(1f, 0.45f, 0.2f, 1f),
                ExcavatorHeatZone.Overheated => new Color(1f, 0.25f, 0.28f, 1f),
                _ => UiCyan,
            };
            string zoneLabel = _worker.HeatZone switch
            {
                ExcavatorHeatZone.Optimal => "OPTIMAL",
                ExcavatorHeatZone.Danger => "DANGER",
                ExcavatorHeatZone.Extreme => "EXTREME",
                ExcavatorHeatZone.Overheated => "OVERHEATED",
                _ => "NORMAL",
            };
            DrawConditionRow(ref y, x0, innerW, "HEAT",
                $"{_worker.Heat:0.#}/100", zoneLabel, _worker.Heat / 100f, heatCol, mute);

            string activity = _worker.MachineActivityLabel;
            Color actCol = activity switch
            {
                "OVERHEATED" => new Color(1f, 0.25f, 0.28f, 1f),
                "COOLING" => UiCyan,
                "MINING" => UiGreen,
                _ => UiDim,
            };
            GUI.Label(new Rect(x0, y, 58f, 14f), "ZONE", mute);
            GUI.Label(new Rect(x0 + 58f, y, 88f, 14f), zoneLabel, LabelStyle(10, UiWhite, bold: true));
            GUI.Label(new Rect(x0 + 148f, y, innerW - 148f, 14f), activity,
                LabelStyle(9, actCol, bold: true));
            y += 16f;

            if (DevMode.Enabled)
            {
                GUI.Label(new Rect(x0, y, innerW, 12f),
                    $"Plan {_worker.RouteCount}  ·  Tool {_worker.ToolPower}  ·  {_worker.ProviderId}",
                    LabelStyle(8, UiDim));
                y += 14f;
            }

            DrawHLine(x0, y, innerW, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.15f));
            y += 8f;
            return y;
        }

        float DrawProspectingContextBlock(ref float y, float x0, float innerW, GUIStyle sec)
        {
            GUI.Label(new Rect(x0, y, innerW, 12f), "PROSPECTING CONTEXT", sec);
            y += 14f;
            string scan = _fieldScanner != null ? _fieldScanner.StateLabel : "no kit";
            GUI.Label(new Rect(x0, y, innerW, 12f),
                $"Scanner  {scan}",
                LabelStyle(8, UiCyan));
            y += 12f;
            if (_prospector != null)
            {
                GUI.Label(new Rect(x0, y, innerW, 12f),
                    $"Mode  {_prospector.WorkMode}  ·  {_prospector.Investigation.PlayerWorkLabel}",
                    LabelStyle(8, UiDim));
                y += 12f;
            }
            DrawHLine(x0, y, innerW, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.15f));
            y += 8f;
            return y;
        }

        void DrawConditionRow(ref float y, float x, float w, string label, string value,
            string state, float fill01, Color accent, GUIStyle mute)
        {
            GUI.Label(new Rect(x, y, 58f, 14f), label, mute);
            GUI.Label(new Rect(x + 58f, y, 88f, 14f), value, LabelStyle(10, UiWhite, bold: true));
            GUI.Label(new Rect(x + 148f, y, w - 148f, 14f), state,
                LabelStyle(9, accent, bold: true));
            y += 14f;

            float barW = w;
            float barH = 4f;
            var prev = GUI.color;
            GUI.color = new Color(0.05f, 0.08f, 0.12f, 0.4f);
            GUI.DrawTexture(new Rect(x, y, barW, barH), Texture2D.whiteTexture);
            float fill = Mathf.Clamp01(fill01) * barW;
            if (fill > 0.5f)
            {
                GUI.color = new Color(accent.r, accent.g, accent.b, 0.75f);
                GUI.DrawTexture(new Rect(x, y, fill, barH), Texture2D.whiteTexture);
            }
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.35f);
            GUI.DrawTexture(new Rect(x, y, barW, 1f), Texture2D.whiteTexture);
            GUI.color = prev;
            y += 8f;
        }

        static void DrawStatPillar(ref float y, float x, float w, string title,
            WorkerStatId start, int count, WorkerStats stats,
            GUIStyle pillar, GUIStyle dim, GUIStyle val,
            HashSet<WorkerStatId> relevant = null)
        {
            GUI.Label(new Rect(x, y, w, 13f), title, pillar);
            y += 15f;
            float colW = w * 0.5f;
            float rowY = y;
            for (int i = 0; i < count; i++)
            {
                var id = (WorkerStatId)((int)start + i);
                bool left = (i % 2) == 0;
                if (left && i > 0) rowY += 15f;
                float cx = left ? x : x + colW;

                bool hi = relevant != null && relevant.Contains(id);
                string name = StatShortName(id);
                if (hi) name = "· " + name;
                int v = stats.Get(id);
                GUI.Label(new Rect(cx, rowY, colW - 36f, 14f), name,
                    hi ? new GUIStyle(dim) { normal = { textColor = new Color(0.25f, 0.92f, 1f, 1f) } } : dim);
                GUI.Label(new Rect(cx + colW - 34f, rowY, 30f, 14f), v.ToString(),
                    hi ? val : new GUIStyle(val) { normal = { textColor = new Color(0.55f, 0.62f, 0.68f, 1f) } });
            }
            y = rowY + 18f;
        }

        static string StatShortName(WorkerStatId id) => id switch
        {
            WorkerStatId.RawPower => "RAW POWER",
            WorkerStatId.HeavyLifting => "HEAVY LIFT",
            WorkerStatId.HeatTolerance => "HEAT TOL.",
            WorkerStatId.SpatialGeometry => "SPATIAL GEO",
            WorkerStatId.SafetyProtocol => "SAFETY",
            WorkerStatId.WorkRate => "WORK RATE",
            _ => id.ToString().ToUpperInvariant(),
        };

        void DrawDigHoodLog()
        {
            const float panelW = 520f;
            float lineH = 14f;
            var findings = _scanHistory?.Findings;
            int findingN = findings != null ? findings.Recent.Count : 0;
            int digN = DigHoodLog.Lines.Count;

            // Prefer showing Prospector findings; keep a few dig lines underneath
            int digShow = findingN > 0 ? Mathf.Min(3, digN) : digN;
            float findingsBlock = findingN > 0 ? 18f + findingN * lineH + 6f : 0f;
            float digBlock = 18f + Mathf.Max(1, digShow) * lineH;
            float panelH = 28f + findingsBlock + digBlock + 10f;
            // Center-bottom — clear of left roster (+ face monitors)
            var r = new Rect(Mathf.Max(290f, (Screen.width - panelW) * 0.5f - 40f),
                Screen.height - panelH - 12f, panelW, panelH);
            DrawCyberPanel(r, lit: false);
            Block(r);

            GUI.Label(new Rect(r.x + 12, r.y + 6, panelW - 24, 14),
                "DIG HOOD // COMMS", LabelStyle(10, UiCyan, bold: true));
            DrawHLine(r.x + 12, r.y + 22, panelW - 24, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.2f));

            float y = r.y + 28f;

            if (findingN > 0)
            {
                GUI.Label(new Rect(r.x + 12, y, panelW - 24, 14),
                    "PROSPECTOR FINDINGS", LabelStyle(9, UiAmber, bold: true));
                y += 16f;
                var findStyle = LabelStyle(9, UiWhite);
                // Recent queue is oldest→newest; show newest last (bottom)
                foreach (var f in findings.Recent)
                {
                    var row = new Rect(r.x + 10, y - 1f, panelW - 20, lineH + 2f);
                    bool hi = findings.IsHighlightActive(out int hs, out int ha)
                              && hs == f.ScanId && ha == f.AnomalyId;
                    if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                    {
                        findings.Highlight(f.ScanId, f.AnomalyId, 20f);
                        MarkAnomalyFindingSeen(f.ScanId, f.AnomalyId);
                        if (_playerTactical != null && !_playerTactical.Visible)
                            _playerTactical.SetVisible(true);
                        _playerTactical?.MarkDirty();
                    }
                    var st = hi ? LabelStyle(9, UiCyan, bold: true) : findStyle;
                    GUI.Label(new Rect(r.x + 12, y, panelW - 24, lineH + 2f), f.Message, st);
                    y += lineH;
                }
                y += 4f;
                DrawHLine(r.x + 12, y, panelW - 24, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.12f));
                y += 6f;
            }

            GUI.Label(new Rect(r.x + 12, y, panelW - 24, 14),
                "SYSTEM", LabelStyle(9, UiMute, bold: true));
            y += 15f;
            var lineStyle = LabelStyle(9, UiDim);
            if (digShow == 0)
            {
                GUI.Label(new Rect(r.x + 12, y, panelW - 24, lineH + 2f),
                    findingN > 0 ? "—" : "waiting for events…", lineStyle);
                return;
            }

            // Show only the last digShow lines
            int skip = digN - digShow;
            int i = 0;
            foreach (var line in DigHoodLog.Lines)
            {
                if (i++ < skip) continue;
                GUI.Label(new Rect(r.x + 12, y, panelW - 24, lineH + 2f), line, lineStyle);
                y += lineH;
            }
        }

        void DrawKeybindingsPanel()
        {
            const float panelW = 248f;
            const float panelH = 490f;
            // Left of right-edge tool strip
            var r = new Rect(Screen.width - panelW - 12f - HudToolStripReserve, Screen.height - panelH - 12f, panelW, panelH);
            DrawCyberPanel(r, lit: false);
            Block(r);

            var hdr = LabelStyle(10, UiCyan, bold: true);
            var key = LabelStyle(10, UiAmber, bold: true);
            var desc = LabelStyle(10, UiDim);
            GUI.Label(new Rect(r.x + 12, r.y + 8, panelW - 24, 16), "CONTROLS", hdr);
            DrawHLine(r.x + 12, r.y + 26, panelW - 24, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.2f));

            (string k, string d)[] rows =
            {
                ("TAB", "Cycle worker (5)"),
                ("I", "ST sheet (selected)"),
                ("WASD", "Move / drive"),
                ("LMB", "Aim · add dig pin"),
                ("Shift+LMB", "Replace dig route"),
                ("Enter", "Pin · confirm scan plan"),
                ("RMB / ⌫", "Undo last pin"),
                ("Esc", "Clear dig / cancel place"),
                ("Y", "Scanner placement mode"),
                ("C", "Scan planning (READY)"),
                ("Q / E", "Scan width · place rotate"),
                ("SPACE", "Radar cone on (preview)"),
                ("SHIFT", "Radar off"),
                ("1–5", "Dig tunnel width (excavator)"),
                ("1 / 2 / 3", "Scan short · med · long"),
                ("U", "Tactical View (anomalies)"),
                ("H", "Scan History (from Tactical)"),
                ("Ø", "DEV MODE toggle"),
                ("T", "Truth View (DEV)"),
                ("Alt", "Speed boost scan/setup (DEV)"),
                ("=", "Force READY / finish scan (DEV)"),
                ("N", "Skip sleep → 08:00"),
                ("G", "Hauler / Refiner priority"),
                ("L", "Place lantern"),
                ("R", "Reset map"),
                ("B", "Balance harness (DEV)"),
            };

            float y = r.y + 34f;
            for (int i = 0; i < rows.Length; i++)
            {
                GUI.Label(new Rect(r.x + 12, y, 88, 15), rows[i].k, key);
                GUI.Label(new Rect(r.x + 102, y, panelW - 118, 15), rows[i].d, desc);
                y += 16.5f;
            }
        }

        static readonly Dictionary<int, GUIStyle> _labelStyleCache = new(24);

        static GUIStyle LabelStyle(int size, Color color, bool bold = false)
        {
            // Floor tiny DEV/body sizes so IMGUI stays readable without redesign.
            int s = size < 9 ? 9 : size;
            int key = (s << 1) | (bold ? 1 : 0);
            if (!_labelStyleCache.TryGetValue(key, out var st))
            {
                st = new GUIStyle(GUI.skin.label)
                {
                    fontSize = s,
                    fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                    richText = false,
                    clipping = TextClipping.Overflow,
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = false,
                    padding = new RectOffset(0, 0, 0, 2),
                };
                _labelStyleCache[key] = st;
            }
            st.normal.textColor = color;
            return st;
        }

        void DrawHeavyScannerHud(float x, float y, float width, float height)
        {
            if (_prospector == null) return;
            width = Mathf.Max(180f, width);
            var scanEq = new Rect(x, y, width, height);
            DrawCyberPanel(scanEq, lit: true);
            Block(scanEq);

            float lx = x + 10f;
            float textW = width - 20f;
            GUI.Label(new Rect(lx, y + 6, textW, 14), "HEAVY SCANNER // STAGE 5",
                LabelStyle(10, UiCyan, bold: true));

            if (_scannerPlaceMode)
            {
                GUI.Label(new Rect(lx, y + 24, textW, 14),
                    _scannerPlaceValid ? "VALID PLACEMENT" : $"INVALID — {_scannerPlaceFail}",
                    LabelStyle(10, _scannerPlaceValid ? UiGreen : new Color(1f, 0.4f, 0.35f), bold: true));
                GUI.Label(new Rect(lx, y + 42, textW, 14),
                    "LMB confirm · Q/E rotate · Esc/Y cancel", LabelStyle(9, UiDim));
                GUI.Label(new Rect(lx, y + 58, textW, 14),
                    "Preview = survey cone (equipment max)", LabelStyle(9, UiMute));
            }
            else if (_scannerPlanMode && _fieldScanner != null)
            {
                float size01 = ProspectorScanFormulas.Size01(
                    _fieldScanner.PlannedRangeCells, _fieldScanner.PlannedHalfAngleDeg, _fieldScanner.Spec);
                float hours = ProspectorScanFormulas.ScanDurationHours(_prospector.Stats, size01);
                GUI.Label(new Rect(lx, y + 24, textW, 14),
                    $"PLAN  {_fieldScanner.PlannedRangeCells:0.#}c  {_fieldScanner.PlannedHalfAngleDeg:0.#}°",
                    LabelStyle(10, UiWhite, bold: true));
                GUI.Label(new Rect(lx, y + 42, textW, 14),
                    $"Est {hours:0.#}h ({ProspectorScanFormulas.DaysFromHours(hours):0.##}d)  size {size01:0.##}",
                    LabelStyle(9, UiAmber));
                GUI.Label(new Rect(lx, y + 60, textW, 14),
                    "1/2/3 range · Q/E width · Enter confirm", LabelStyle(9, UiDim));
                GUI.Label(new Rect(lx, y + 78, textW, 14),
                    $"Cal {_prospector.Stats.Get(WorkerStatId.Calibration)}  Foc {_prospector.Stats.Get(WorkerStatId.Focus)}  Sp {_prospector.Stats.Get(WorkerStatId.SpatialGeometry)}",
                    LabelStyle(9, UiMute));
            }
            else if (_fieldScanner != null && _fieldScanner.State == ProspectorScannerState.Scanning
                     && _fieldScanner.ActiveSession != null)
            {
                var s = _fieldScanner.ActiveSession;
                int id = s.Record != null ? s.Record.ScanId : 0;
                int anom = s.Record != null ? s.Record.Anomalies.Count : 0;
                float rem = Mathf.Max(0f, s.DurationHours - s.ElapsedHours);
                GUI.Label(new Rect(lx, y + 24, textW, 14),
                    $"SCAN #{id}  {s.Progress01 * 100f:0.#}%",
                    LabelStyle(10, UiAmber, bold: true));
                GUI.Label(new Rect(lx, y + 42, textW, 14),
                    $"{s.ElapsedHours:0.#}h · rem {rem:0.#}h · anom {anom}", LabelStyle(9, UiWhite));
                GUI.Label(new Rect(lx, y + 60, textW, 14),
                    $"Reach {s.CurrentReachCells:0.#}/{s.MaxDistanceCells:0.#}  ev {s.EvidenceThisScan}",
                    LabelStyle(9, UiDim));
                GUI.Label(new Rect(lx, y + 78, textW, 14),
                    $"Stored {_scanHistory?.EvidenceCount ?? 0} · scan auto 12× · analysis 1× (Alt 8×)",
                    LabelStyle(9, UiMute));
                GUI.Label(new Rect(lx, y + 96, textW, 14),
                    AnalyseHudLine(),
                    LabelStyle(9, UiMute));
            }
            else if (_fieldScanner != null)
            {
                string line = $"STATE {_fieldScanner.StateLabel}";
                if (_fieldScanner.State == ProspectorScannerState.SettingUp)
                    line += $"  {_fieldScanner.SetupElapsedHours:0.00}/{_fieldScanner.SetupDurationHours:0.00}h";
                GUI.Label(new Rect(lx, y + 24, textW, 14), line, LabelStyle(10, UiWhite, bold: true));
                GUI.Label(new Rect(lx, y + 42, textW, 14),
                    _fieldScanner.State == ProspectorScannerState.SettingUp
                        ? "Alt 20× setup · = force READY"
                        : _fieldScanner.State == ProspectorScannerState.Ready
                            ? "C = plan scan · Y = redeploy"
                            : "Y = place / redeploy",
                    LabelStyle(9, UiDim));
                GUI.Label(new Rect(lx, y + 58, textW, 14),
                    $"Evidence {_scanHistory?.EvidenceCount ?? 0} · Anomalies {_scanHistory?.AnomalyCountDisplay ?? 0}",
                    LabelStyle(9, UiMute));
                GUI.Label(new Rect(lx, y + 58 + 16, textW, 14),
                    AnalyseHudLine(),
                    LabelStyle(9, UiMute));
            }
            else
            {
                GUI.Label(new Rect(lx, y + 24, textW, 14), "No scanner deployed", LabelStyle(10, UiDim));
                GUI.Label(new Rect(lx, y + 42, textW, 14), "Y = Scanner Placement Mode", LabelStyle(9, UiDim));
            }
        }

        string AnalyseHudLine()
        {
            if (_prospector != null && _prospector.WorkMode == ProspectorWorkMode.Investigate)
            {
                string inv = _prospector.InvestigationDebugLine;
                if (!string.IsNullOrEmpty(inv)) return inv;
            }

            var analyst = _scanHistory?.Analyst;
            if (analyst == null || !analyst.HasWork)
            {
                int assessed = 0;
                var scan = _scanHistory?.DisplayScan;
                if (scan != null)
                {
                    for (int i = 0; i < scan.Anomalies.Count; i++)
                        if (scan.Anomalies[i].AnalysisStatus == AnomalyAnalysisStatus.Assessed)
                            assessed++;
                }
                return assessed > 0
                    ? $"Analysis done · {assessed} assessed · hover U"
                    : "Analysis idle · hover U";
            }
            var cur = analyst.Current;
            if (cur != null)
            {
                string work = ProspectorAnomaly.NeedWorkLabel(cur.CurrentNeed);
                return $"#{cur.AnomalyId:00} · {work} · q {analyst.QueueCount}";
            }
            return $"Analysis queue {analyst.QueueCount}";
        }

        static string ShortInvestigationSub(ProspectorPerson p)
        {
            if (p == null) return "AUTO // INVESTIGATE";
            string line = p.InvestigationDebugLine;
            if (string.IsNullOrEmpty(line)) return "AUTO // INVESTIGATE";
            // Compact for worker card
            if (line.Length <= 22) return line;
            int cut = line.IndexOf('·');
            return cut > 0 ? line.Substring(0, cut).Trim() : line.Substring(0, 22);
        }

        void OnProspectorFindingAdded(ProspectorFinding f)
        {
            if (f == null) return;
            // Strong toast only when a real finding is published (ReadyForFinding → Assessed)
            if (f.EventType != ProspectorFindingEventType.AnalysisCompleted)
                return;

            string spoken = ProspectorAnomaly.SpokenId(f.AnomalyId);
            string author = !string.IsNullOrEmpty(f.WorkerDisplayName)
                ? f.WorkerDisplayName
                : (f.WorkerId > 0 ? $"Worker {f.WorkerId}" : "Prospector");
            ShowFindingToast(string.IsNullOrEmpty(f.Message)
                ? $"{author}: Boss, I've got something on {spoken}. Check Tactical."
                : f.Message);

            // Frozen author from finding — not whoever is currently Prospecting
            TryAuthoredBanter(
                f.WorkerId,
                f.WorkerDisplayName,
                JobType.Prospecting,
                "FindingCompleted",
                $"Boss, I've got something on {spoken}. Check Tactical.",
                $"Finding on {spoken}. Open Tactical.",
                $"Conclusion ready on {spoken} — look at Tactical.");

            if (f.WorkerId > 0)
            {
                WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                    f.WorkerId,
                    WorkerStateEventType.Discovery,
                    5f,
                    $"FindingAssessed#{f.AnomalyId:00}",
                    JobType.Prospecting,
                    _fieldScanner != null ? _fieldScanner.ProviderId : "body.prospector"));
                _prospectorDrySpell.NotifyDiscovery();
            }
        }

        void ShowFindingToast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            _findingToast = message;
            _findingToastUntil = Time.unscaledTime + 18f;
        }

        void DrawFindingToast()
        {
            if (string.IsNullOrEmpty(_findingToast)) return;
            if (Time.unscaledTime > _findingToastUntil)
            {
                _findingToast = null;
                return;
            }

            const float tw = 560f;
            const float th = 64f;
            float px = (Screen.width - tw) * 0.5f;
            // Stay clear of top bar + left column / thinking dock
            float py = Mathf.Max(88f, _leftHudContentTop);
            if (px < _leftHudRight + 8f)
                px = _leftHudRight + 12f;
            float rightLimit = Screen.width - HudToolStripReserve - 16f;
            if (px + tw > rightLimit)
                px = Mathf.Max(12f, rightLimit - tw);
            var r = new Rect(px, py, tw, th);
            DrawCyberPanel(r, lit: true, accentOverride: UiAmber);
            Block(r);
            GUI.Label(new Rect(r.x + 14, r.y + 10, tw - 28, 14),
                "FINDING // PERSON", LabelStyle(10, UiAmber, bold: true));
            GUI.Label(new Rect(r.x + 14, r.y + 30, tw - 28, 22),
                _findingToast, LabelStyle(13, UiWhite, bold: true));
        }

        /// <summary>
        /// Player-facing investigation thought — CURRENT WORK / CURRENT QUESTION.
        /// No hidden values or percentages.
        /// Docked beside the left crew column so it never covers scanner/roster cards.
        /// </summary>
        void DrawProspectorThinkingPanel()
        {
            if (_prospector == null) return;
            // Live investigation HUD — hide while browsing Scan History
            if (_scanHistoryBrowserOpen
                || (_scanHistory != null && _scanHistory.IsHistoricalMode))
                return;
            if (_prospector.WorkMode != ProspectorWorkMode.Investigate) return;

            var loop = _prospector.Investigation;
            if (loop.FocusAnomalyId <= 0) return;

            string work = loop.PlayerWorkLabel;
            string question = loop.PlayerQuestionLabel;
            if (string.IsNullOrEmpty(work) && string.IsNullOrEmpty(question)) return;

            const float tw = 300f;
            float th = string.IsNullOrEmpty(question) ? 78f : 112f;
            float rightLimit = Screen.width - HudToolStripReserve - 16f;
            if (_hudPopup != HudPopupKind.None)
                rightLimit -= 300f;

            // Prefer dock to the right of the left HUD stack (scanner + roster + sheet)
            float px = _leftHudRight + 14f;
            float py = _leftHudContentTop;
            // If that would collide with the right tool strip / open popup, drop below the roster
            if (px + tw > rightLimit)
            {
                px = 10f;
                py = _leftHudBottom + 10f;
            }

            px = Mathf.Clamp(px, 10f, Mathf.Max(10f, rightLimit - tw));
            py = Mathf.Clamp(py, 50f, Mathf.Max(50f, Screen.height - th - 12f));

            var r = new Rect(px, py, tw, th);
            // Expand left reserve so later speech clamps stay clear of this panel
            _leftHudRight = Mathf.Max(_leftHudRight, r.xMax);
            _leftHudBottom = Mathf.Max(_leftHudBottom, r.yMax);
            DrawCyberPanel(r, lit: true);
            Block(r);

            float lx = r.x + 12f;
            float ty = r.y + 8f;
            float textW = tw - 24f;

            GUI.Label(new Rect(lx, ty, textW, 14),
                $"ANOMALY #{loop.FocusAnomalyId:00}",
                LabelStyle(11, UiCyan, bold: true));
            ty += 18f;
            GUI.Label(new Rect(lx, ty, textW, 12), "CURRENT WORK",
                LabelStyle(8, UiMute, bold: true));
            ty += 13f;
            GUI.Label(new Rect(lx, ty, textW, 16), work,
                LabelStyle(11, UiWhite, bold: true));
            ty += 18f;

            if (!string.IsNullOrEmpty(question))
            {
                GUI.Label(new Rect(lx, ty, textW, 12), "CURRENT QUESTION",
                    LabelStyle(8, UiMute, bold: true));
                ty += 13f;
                GUI.Label(new Rect(lx, ty, textW, 28), $"\"{question}\"",
                    LabelStyle(10, UiAmber));
            }
        }

        void MarkAnomalyFindingSeen(int scanId, int anomalyId)
        {
            var scan = _scanHistory?.DisplayScan;
            if (scan == null || scan.ScanId != scanId) return;
            for (int i = 0; i < scan.Anomalies.Count; i++)
            {
                if (scan.Anomalies[i].AnomalyId != anomalyId) continue;
                scan.Anomalies[i].MarkFindingSeen();
                return;
            }
        }

        /// <summary>DEV-only activity readout — evidence-driven investigation plan.</summary>
        void DrawProspectorDevActivityBox()
        {
            if (!DevMode.Enabled) return;
            if (_prospector == null) return;
            // Live investigation DEV — conflicts with History browser / historical map
            if (_scanHistoryBrowserOpen
                || (_scanHistory != null && _scanHistory.IsHistoricalMode))
                return;
            if (_prospector.WorkMode != ProspectorWorkMode.Investigate
                && !SelectedJobIs(JobType.Prospecting))
                return;

            var loop = _prospector.Investigation;
            var analyst = _scanHistory?.Analyst;
            var cur = analyst?.Current;
            if (cur == null && loop.FocusAnomalyId > 0)
            {
                var scan = _scanHistory?.DisplayScan;
                if (scan != null)
                {
                    for (int i = 0; i < scan.Anomalies.Count; i++)
                        if (scan.Anomalies[i].AnomalyId == loop.FocusAnomalyId)
                        {
                            cur = scan.Anomalies[i];
                            break;
                        }
                }
            }

            Vector2 goal = loop.HasTripStand ? loop.TripStand : loop.DeskWorldPosition;
            if (loop.State == ProspectorInvestigationState.ReturningToAnalysis
                || loop.State == ProspectorInvestigationState.Analysing
                || loop.State == ProspectorInvestigationState.Idle)
                goal = loop.DeskWorldPosition;

            float dist = Vector2.Distance(_prospector.Position, goal);
            bool dwelling = loop.State == ProspectorInvestigationState.InspectingRock
                            || loop.State == ProspectorInvestigationState.ConsultingRefiner;
            float dwell01 = dwelling && loop.DwellNeeded > 0.001f
                ? Mathf.Clamp01(loop.DwellHours / loop.DwellNeeded)
                : 0f;

            string moveLine = loop.State switch
            {
                ProspectorInvestigationState.WalkingToExcavator => $"→ excavator  {dist:0.00}m",
                ProspectorInvestigationState.WalkingToLooseRock => $"→ loose rock  {dist:0.00}m",
                ProspectorInvestigationState.WalkingToRefiner => $"→ analysis table  {dist:0.00}m",
                ProspectorInvestigationState.ReturningToAnalysis => $"→ analysis table  {dist:0.00}m",
                ProspectorInvestigationState.InspectingRock =>
                    $"dwell  {loop.DwellHours:0.00}/{loop.DwellNeeded:0.00}h  ({dwell01 * 100f:0}%)",
                ProspectorInvestigationState.ConsultingRefiner =>
                    $"consult  {loop.DwellHours:0.00}/{loop.DwellNeeded:0.00}h" +
                    (_refiner != null && _refiner.IsInConsultation ? "  · table" : ""),
                ProspectorInvestigationState.Analysing =>
                    cur != null && analyst != null && analyst.IsDeskClockActive
                        ? $"table clock  {cur.AnalysisElapsedHours:0.00}/{cur.AnalysisDurationHours:0.00}h"
                        : $"at table  dist {dist:0.00}m",
                _ => dist < 0.6f ? "near table" : $"dist {dist:0.00}m",
            };

            // Evidence lines
            var evidenceKinds = new[]
            {
                AnomalyEvidenceKind.ScanInterpretation,
                AnomalyEvidenceKind.FieldEvidence,
                AnomalyEvidenceKind.LooseRockEvidence,
                AnomalyEvidenceKind.RefinerOpinion,
                AnomalyEvidenceKind.CrossCheck,
            };
            int evidenceLines = 0;
            if (cur != null)
            {
                for (int i = 0; i < evidenceKinds.Length; i++)
                {
                    bool planned = EvidenceKindPlannedOrDone(cur, evidenceKinds[i]);
                    if (planned) evidenceLines++;
                }
            }

            const float tw = 420f;
            float th = 196f + evidenceLines * 13f + (cur != null && cur.RemainingNeeds.Count > 0 ? 28f : 14f);
            if (cur != null) th += 52f;
            float px = Screen.width - tw - 12f - HudToolStripReserve;
            float py = 100f;
            var r = new Rect(px, py, tw, th);
            DrawCyberPanel(r, lit: true);
            Block(r);

            float lx = r.x + 12f;
            float ty = r.y + 8f;
            float twt = tw - 24f;

            GUI.Label(new Rect(lx, ty, twt, 14), "DEV // PROSPECTOR ACTIVITY",
                LabelStyle(9, UiAmber, bold: true));
            ty += 16f;
            DrawHLine(lx, ty, twt, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.22f));
            ty += 6f;

            // Workstation / timing DEV readout
            string wsState = _prospectWorkstation == null
                ? "WORKSTATION  none"
                : loop.State == ProspectorInvestigationState.ConsultingRefiner
                    ? "WORKSTATION  consult at table"
                    : loop.State == ProspectorInvestigationState.Analysing
                      || (loop.State == ProspectorInvestigationState.Idle
                          && Vector2.Distance(_prospector.Position, loop.DeskWorldPosition) < 0.7f)
                        ? "WORKSTATION  analysing at table"
                        : loop.State == ProspectorInvestigationState.ReturningToAnalysis
                          || loop.State == ProspectorInvestigationState.WalkingToRefiner
                            ? $"WORKSTATION  transit ({loop.State})"
                            : "WORKSTATION  idle / away";
            GUI.Label(new Rect(lx, ty, twt, 12), wsState, LabelStyle(8, UiCyan, bold: true));
            ty += 13f;
            float consultH = ProspectorScanFormulas.UseTestingScanDurations
                ? ProspectorScanFormulas.TestingConsultDwellHours
                : ProspectorScanFormulas.ProductionConsultDwellHours;
            string analysisDur = cur != null && cur.AnalysisDurationHours > 0.001f
                ? $"{cur.AnalysisDurationHours:0.00}h"
                : "—";
            GUI.Label(new Rect(lx, ty, twt, 12),
                $"TIMING  analysis {analysisDur}  ·  consult {consultH:0.000}h",
                LabelStyle(8, UiDim));
            ty += 14f;

            if (cur != null)
            {
                GUI.Label(new Rect(lx, ty, twt, 14), $"ANOMALY #{cur.AnomalyId:00}",
                    LabelStyle(11, UiCyan, bold: true));
                ty += 16f;
                GUI.Label(new Rect(lx, ty, twt, 14),
                    $"Current work: {loop.PlayerWorkLabel}",
                    LabelStyle(9, UiWhite));
                ty += 14f;
                string reason = string.IsNullOrEmpty(loop.PlayerQuestionLabel) ? "—" : loop.PlayerQuestionLabel;
                GUI.Label(new Rect(lx, ty, twt, 28), $"Current question: \"{reason}\"",
                    LabelStyle(9, UiMute));
                ty += 30f;

                GUI.Label(new Rect(lx, ty, twt, 12), "Evidence:",
                    LabelStyle(8, UiCyan, bold: true));
                ty += 13f;
                for (int i = 0; i < evidenceKinds.Length; i++)
                {
                    var kind = evidenceKinds[i];
                    if (!EvidenceKindPlannedOrDone(cur, kind)) continue;
                    bool done = cur.HasEvidence(kind);
                    GUI.Label(new Rect(lx, ty, twt, 12),
                        $"· {ProspectorAnomaly.EvidenceStatusLine(kind, done)}",
                        LabelStyle(8, done ? UiGreen : UiDim));
                    ty += 13f;
                }

                ty += 4f;
                string next;
                if (cur.RemainingNeeds.Count > 1)
                    next = ProspectorAnomaly.NeedNextLabel(cur.RemainingNeeds[1]);
                else if (cur.CurrentNeed == AnomalyInvestigationNeed.ReadyForFinding)
                    next = "Publish finding";
                else if (cur.CurrentNeed == AnomalyInvestigationNeed.Complete)
                    next = "Done";
                else if (cur.RemainingNeeds.Count == 1)
                    next = ProspectorAnomaly.NeedNextLabel(cur.RemainingNeeds[0]);
                else
                    next = ProspectorAnomaly.NeedNextLabel(cur.CurrentNeed);
                GUI.Label(new Rect(lx, ty, twt, 12), "Next:",
                    LabelStyle(8, UiCyan, bold: true));
                ty += 13f;
                GUI.Label(new Rect(lx, ty, twt, 12), $"· {next}",
                    LabelStyle(8, UiAmber));
                ty += 14f;

                // Truth/DEV geo signals
                var h = cur.HiddenSignals;
                var o = cur.ObservedSignals;
                GUI.Label(new Rect(lx, ty, twt, 12),
                    $"TRUTH  {cur.DominantMaterial}/{cur.MaterialVariant}",
                    LabelStyle(8, UiMute));
                ty += 12f;
                GUI.Label(new Rect(lx, ty, twt, 12),
                    $"H Ret{h.ReturnStrength:0.00} Att{h.Attenuation:0.00} Con{h.Conductivity:0.00} " +
                    $"Coh{h.StructuralCoherence:0.00} {h.BoundaryCharacter}@{h.BoundarySharpness01:0.00}",
                    LabelStyle(7, UiDim));
                ty += 12f;
                GUI.Label(new Rect(lx, ty, twt, 12),
                    $"O Ret{(o.HasReturn ? o.Values.ReturnStrength.ToString("0.00") : "—")} " +
                    $"Att{(o.HasAttenuation ? o.Values.Attenuation.ToString("0.00") : "—")} " +
                    $"Con{(o.HasConductivity ? o.Values.Conductivity.ToString("0.00") : "—")} " +
                    $"Coh{(o.HasCoherence ? o.Values.StructuralCoherence.ToString("0.00") : "—")} " +
                    $"{(o.HasBoundary ? $"{o.Values.BoundaryCharacter}@{o.Values.BoundarySharpness01:0.00}" : "—")}",
                    LabelStyle(7, UiGreen));
                ty += 12f;
                GUI.Label(new Rect(lx, ty, twt, 12),
                    $"agree {ProspectorGeoEvidence.AgreementScore(cur):0.00}  " +
                    $"tension {ProspectorGeoEvidence.TraitTension(cur):0.00}  " +
                    $"q {o.ObservationQuality01:0.00}" +
                    (o.HasContradictionFlag ? "  CONTRADICT" : ""),
                    LabelStyle(7, UiMute));
                ty += 14f;
            }
            else
            {
                GUI.Label(new Rect(lx, ty, twt, 14), "No active anomaly",
                    LabelStyle(10, UiDim));
                ty += 16f;
                GUI.Label(new Rect(lx, ty, twt, 14), loop.DebugStateLabel,
                    LabelStyle(10, UiCyan, bold: true));
                ty += 14f;
            }

            GUI.Label(new Rect(lx, ty, twt, 12), $"MOVE  {moveLine}",
                LabelStyle(8, UiGreen));
        }

        static bool EvidenceKindPlannedOrDone(ProspectorAnomaly a, AnomalyEvidenceKind kind)
        {
            if (a.HasEvidence(kind)) return true;
            AnomalyInvestigationNeed need = kind switch
            {
                AnomalyEvidenceKind.ScanInterpretation => AnomalyInvestigationNeed.NeedDeskInterpretation,
                AnomalyEvidenceKind.FieldEvidence => AnomalyInvestigationNeed.NeedFieldEvidence,
                AnomalyEvidenceKind.LooseRockEvidence => AnomalyInvestigationNeed.NeedLooseRockInspection,
                AnomalyEvidenceKind.RefinerOpinion => AnomalyInvestigationNeed.NeedRefinerConsultation,
                AnomalyEvidenceKind.CrossCheck => AnomalyInvestigationNeed.NeedCrossCheck,
                _ => AnomalyInvestigationNeed.None,
            };
            if (a.CurrentNeed == need) return true;
            for (int i = 0; i < a.RemainingNeeds.Count; i++)
                if (a.RemainingNeeds[i] == need) return true;
            // Always show scan interpretation once plan started or analysing
            if (kind == AnomalyEvidenceKind.ScanInterpretation
                && (a.InvestigationPlanBuilt
                    || a.AnalysisStatus == AnomalyAnalysisStatus.Analysing
                    || a.CurrentNeed != AnomalyInvestigationNeed.None))
                return true;
            return false;
        }

        void DrawWorkerControlDevPanel()
        {
            const float pw = 278f;
            float ph = 380f;
            float bx = Screen.width - pw - 12f - HudToolStripReserve;
            float by = 96f;
            var r = new Rect(bx, by, pw, ph);
            DrawCyberPanel(r, lit: true, accentOverride: UiGreen);
            Block(r);

            float x = r.x + 8f;
            float y = r.y + 6f;
            float inner = pw - 16f;

            GUI.Label(new Rect(x, y, inner, 11f), "DEV // CONTROL · F2/F4 PERSON-FIRST",
                LabelStyle(8, UiMute, bold: true));
            y += 13f;

            // TAB order = fixed crew roster WorkerIds (Lewis→…→Viktor)
            string tabOrder = "TAB ORDER =";
            if (_crewWorkers != null)
            {
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    if (_crewWorkers[i] == null) continue;
                    tabOrder += $" {_crewWorkers[i].WorkerId}";
                }
            }
            GUI.Label(new Rect(x, y, inner, 10f), tabOrder, LabelStyle(6, UiDim));
            y += 12f;

            var ctl = ResolveControlTarget();
            if (!ctl.HasPerson)
            {
                GUI.Label(new Rect(x, y, inner, 12f), "SELECTED PERSON  (none)",
                    LabelStyle(8, UiAmber, bold: true));
                return;
            }

            GUI.Label(new Rect(x, y, inner, 12f),
                $"SELECTED PERSON  {ctl.Worker.DisplayName} [{ctl.WorkerId}]",
                LabelStyle(8, UiWhite, bold: true));
            y += 13f;
            GUI.Label(new Rect(x, y, inner, 11f),
                $"CURRENT JOB  {JobStatPreview.DisplayName(ctl.JobType)}",
                LabelStyle(8, UiCyan));
            y += 12f;
            GUI.Label(new Rect(x, y, inner, 11f),
                $"PROVIDER  {(string.IsNullOrEmpty(ctl.ProviderId) ? "none" : ctl.ProviderId)}",
                LabelStyle(7, UiAmber));
            y += 12f;
            GUI.Label(new Rect(x, y, inner, 11f),
                $"CAMERA TARGET  {ctl.HostLabel}",
                LabelStyle(7, UiGreen));
            y += 12f;
            if (ctl.Avatar != null)
            {
                var p = ctl.Avatar.PresencePosition;
                string vis = ctl.Avatar.IsVisuallyHidden ? "false" : "true";
                GUI.Label(new Rect(x, y, inner, 11f),
                    $"PHYSICAL PERSON  @{p.x:0.00},{p.y:0.00}  Visible:{vis}",
                    LabelStyle(7, UiDim));
                y += 12f;
            }
            GUI.Label(new Rect(x, y, inner, 11f),
                $"LEGACY CONTROL  {_control} [UNUSED STUB]",
                LabelStyle(7, UiMute));
            y += 12f;
            var phys = GetPhysicalState(ctl.Worker);
            GUI.Label(new Rect(x, y, inner, 11f),
                $"PHYSICAL STATE  {phys}",
                LabelStyle(7, UiAmber));
            y += 11f;
            GUI.Label(new Rect(x, y, inner, 11f),
                $"CanPerformJobActions  {CanPerformJobActions(ctl.Worker).ToString().ToLowerInvariant()}",
                LabelStyle(7, UiDim));
            y += 13f;

            // Universal Worker Physics V1 readout
            DrawHLine(x, y, inner, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.22f));
            y += 4f;
            GUI.Label(new Rect(x, y, inner, 10f), "WALK PHYSICS  (person-owned)",
                LabelStyle(7, UiCyan, bold: true));
            y += 11f;

            var wr = ctl.Worker;
            var loco = wr.Locomotion;
            var profile = WorkerPhysicalProfile.From(wr);
            Vector2 samplePos = ctl.Avatar != null
                ? ctl.Avatar.PresencePosition
                : (GetProviderOperatePoint(ctl.JobType));
            var terrain = WorkerTerrainSampler.Sample(_world, samplePos, AvatarCommuteRadius);
            // Refresh readout even when idle
            float preview = WorkerLocomotion.EvaluateWalkSpeed(
                wr, terrain, roleBias: 1f, carriedLoad01: 0f, isMoving: false, deltaTime: 0f);

            GUI.Label(new Rect(x, y, inner, 10f),
                $"MoveSpeed  {loco.LastEffectiveSpeed:0.00}  (preview {preview:0.00}  base {profile.MoveSpeed:0.00})",
                LabelStyle(7, UiWhite));
            y += 10f;
            GUI.Label(new Rect(x, y, inner, 10f),
                $"Balance  {profile.Balance01:0.00}  FootingR {profile.FootingResist:0.00}  Adapt {profile.TerrainAdapt:0.00}",
                LabelStyle(6, UiDim));
            y += 10f;
            GUI.Label(new Rect(x, y, inner, 10f),
                $"Terrain  {terrain.Kind}  diff {terrain.Difficulty01:0.00}  clear {terrain.Clearance}  mul×{loco.LastTerrainMul:0.00}",
                LabelStyle(6, UiAmber));
            y += 10f;
            float stamMax = wr.PhysicalStaminaMax;
            float stam = wr.State != null ? wr.State.PhysicalStamina : 0f;
            GUI.Label(new Rect(x, y, inner, 10f),
                $"Stamina  {stam:0}/{stamMax:0}  move×{loco.LastStaminaMul:0.00}  Endur {profile.Endurance:0.00}",
                LabelStyle(6, UiGreen));
            y += 10f;
            GUI.Label(new Rect(x, y, inner, 10f),
                $"Injury  {(wr.State != null ? wr.State.Injury : 0f):0.0}  move×{loco.LastInjuryMul:0.00}  NeedsCare {(wr.State != null && wr.State.NeedsCare)}",
                LabelStyle(6, wr.State != null && wr.State.Injury >= 15f ? UiAmber : UiDim));
            y += 10f;
            GUI.Label(new Rect(x, y, inner, 10f),
                $"Load  mul×{loco.LastLoadMul:0.00}  HeavyLift handling {profile.LoadHandling:0.00}",
                LabelStyle(6, UiDim));
            y += 10f;
            string lastEv = loco.LastEvent == WorkerFootingEvent.None
                ? "none"
                : loco.LastEventDetail;
            if (lastEv.Length > 42) lastEv = lastEv.Substring(0, 40) + "…";
            GUI.Label(new Rect(x, y, inner, 10f),
                $"Last footing  {lastEv}",
                LabelStyle(6, loco.LastEvent != WorkerFootingEvent.None ? UiAmber : UiMute));
            y += 11f;
            GUI.Label(new Rect(x, y, inner, 10f),
                $"Footing expose {loco.ExposureAccum:0.00}/{WorkerLocomotion.ExposurePerCheck:0.0}  forceOut {WorkerInjuryConsequences.ForcesOutOfWork(wr.Injuries)}",
                LabelStyle(6, UiMute));
            y += 12f;

            GUI.Label(new Rect(x, y, inner, 10f), "INJURIES  (person-owned)",
                LabelStyle(7, UiAmber, bold: true));
            y += 11f;
            if (wr.Injuries.Count == 0)
            {
                GUI.Label(new Rect(x, y, inner, 10f), "(none)", LabelStyle(6, UiMute));
                y += 10f;
            }
            else
            {
                int shown = 0;
                for (int i = 0; i < wr.Injuries.Active.Count && shown < 4; i++)
                {
                    var rec = wr.Injuries.Active[i];
                    if (rec == null || !rec.Active) continue;
                    GUI.Label(new Rect(x, y, inner, 10f),
                        $"{rec.DisplayName}  {rec.SeverityLabel}  {rec.Cause}  left {rec.RecoveryGameHoursLeft:0.0}h",
                        LabelStyle(6, UiWhite));
                    y += 10f;
                    shown++;
                }
                GUI.Label(new Rect(x, y, inner, 10f),
                    $"WALK×{WorkerInjuryConsequences.WalkSpeedMul(wr.Injuries):0.00}  MANUAL×{WorkerInjuryConsequences.ManualWorkMul(wr.Injuries):0.00}  LOAD×{WorkerInjuryConsequences.LoadCarryMul(wr.Injuries):0.00}",
                    LabelStyle(6, UiCyan));
                y += 10f;
            }

            var acc = loco.LastAccident ?? WorkerAccidentSystem.LastReport;
            if (acc != null && acc.WorkerId == wr.WorkerId)
            {
                GUI.Label(new Rect(x, y, inner, 10f),
                    $"Last accident  {acc.Headline}  {acc.Detail}",
                    LabelStyle(6, UiAmber));
                y += 10f;
            }
            if (wr.Injuries.RecentAccidents.Count > 0)
            {
                string hist = wr.Injuries.RecentAccidents[0];
                if (hist.Length > 44) hist = hist.Substring(0, 42) + "…";
                GUI.Label(new Rect(x, y, inner, 10f), hist, LabelStyle(6, UiMute));
            }
        }

        void DrawBanterDevPanel()
        {
            const float pw = 258f;
            float ph = 118f;
            float bx = Screen.width - pw - 12f - HudToolStripReserve;
            float by = 96f;
            var r = new Rect(bx, by, pw, ph);
            DrawCyberPanel(r, lit: true, accentOverride: new Color(0.7f, 0.55f, 1f));
            Block(r);

            float x = r.x + 8f;
            float y = r.y + 6f;
            float inner = pw - 16f;

            GUI.Label(new Rect(x, y, inner, 11f), "DEV // BANTER · F5 PERSON AUTHOR",
                LabelStyle(8, UiMute, bold: true));
            y += 13f;

            var last = _banter.LastSpeech;
            if (last == null || string.IsNullOrEmpty(last.Text))
            {
                GUI.Label(new Rect(x, y, inner, 12f), "LAST SPEECH  (none)",
                    LabelStyle(8, UiDim));
                return;
            }

            GUI.Label(new Rect(x, y, inner, 12f),
                $"Speaker  {last.DisplayName} [{last.WorkerId}]",
                LabelStyle(8, UiWhite, bold: true));
            y += 12f;
            GUI.Label(new Rect(x, y, inner, 11f),
                $"Job Context  {WorkerBanter.ContextLabel(last.Context)}",
                LabelStyle(7, UiCyan));
            y += 11f;
            var wr = FindCrewWorker(last.WorkerId);
            var asg = wr != null ? _assignments.GetAssignment(wr.WorkerId) : null;
            string prov = asg != null && !string.IsNullOrEmpty(asg.ProviderId) ? asg.ProviderId : "none";
            GUI.Label(new Rect(x, y, inner, 11f),
                $"Provider  {prov}",
                LabelStyle(7, UiAmber));
            y += 11f;
            string phys = wr != null ? GetPhysicalState(wr).ToString() : "?";
            GUI.Label(new Rect(x, y, inner, 11f),
                $"Physical State  {phys}",
                LabelStyle(7, UiDim));
            y += 11f;
            GUI.Label(new Rect(x, y, inner, 11f),
                $"Source Event  {last.SourceEvent}",
                LabelStyle(7, UiGreen));
            y += 11f;
            GUI.Label(new Rect(x, y, inner, 11f),
                $"Authored @  {last.AuthoredGameHours:0.00}h",
                LabelStyle(6, UiMute));
            y += 11f;
            var tip = LabelStyle(7, UiWhite);
            tip.wordWrap = true;
            GUI.Label(new Rect(x, y, inner, 28f), $"\"{last.Text}\"", tip);
        }

        void DrawWorkerSheetDevPanel()
        {
            const float pw = 258f;
            float ph = 128f;
            float bx = Screen.width - pw - 12f - HudToolStripReserve;
            float by = 96f;
            var r = new Rect(bx, by, pw, ph);
            DrawCyberPanel(r, lit: true, accentOverride: UiAmber);
            Block(r);

            float x = r.x + 8f;
            float y = r.y + 6f;
            float inner = pw - 16f;

            GUI.Label(new Rect(x, y, inner, 11f), "DEV // ST SHEET · F3 PERSON-OWNED",
                LabelStyle(8, UiMute, bold: true));
            y += 13f;

            if (_openStatsWorkerId <= 0)
            {
                GUI.Label(new Rect(x, y, inner, 12f), "ST SHEET TARGET  (closed)",
                    LabelStyle(8, UiDim));
                return;
            }

            var wr = FindCrewWorker(_openStatsWorkerId);
            if (wr == null)
            {
                GUI.Label(new Rect(x, y, inner, 12f), "ST SHEET TARGET  (missing)",
                    LabelStyle(8, UiAmber));
                return;
            }

            var asg = _assignments.GetAssignment(wr.WorkerId);
            var job = asg != null ? asg.JobType : JobType.Unassigned;
            var def = JobStatPreview.Get(job);

            GUI.Label(new Rect(x, y, inner, 12f),
                $"ST SHEET TARGET  {wr.DisplayName} [WorkerId {wr.WorkerId}]",
                LabelStyle(8, UiWhite, bold: true));
            y += 13f;
            GUI.Label(new Rect(x, y, inner, 11f),
                $"Stats Ref  {wr.StatsRefLabel}",
                LabelStyle(7, UiCyan));
            y += 12f;
            GUI.Label(new Rect(x, y, inner, 11f),
                $"Current Job  {JobStatPreview.DisplayName(job)}",
                LabelStyle(7, UiGreen));
            y += 11f;
            GUI.Label(new Rect(x, y, inner, 11f),
                $"Provider  {(job == JobType.Unassigned ? "none" : ProviderLabelForAssignment(asg))}",
                LabelStyle(7, UiAmber));
            y += 11f;
            string src = job == JobType.Unassigned
                ? "(none — Unassigned)"
                : $"JobDefinition.{job}";
            GUI.Label(new Rect(x, y, inner, 11f),
                $"Relevant Stats Source  {src}",
                LabelStyle(7, UiDim));
            y += 11f;
            GUI.Label(new Rect(x, y, inner, 11f),
                $"Provisional  {(job == JobType.Unassigned ? "n/a" : def.RelevantStatsAreProvisional.ToString().ToLowerInvariant())}",
                LabelStyle(7, def.RelevantStatsAreProvisional ? UiAmber : UiMute));
            y += 11f;
            GUI.Label(new Rect(x, y, inner, 11f),
                $"Baseline Owner  WorkerId {wr.WorkerId}" +
                (_sheetBaselineCaptured.TryGetValue(wr.WorkerId, out bool cap) && cap
                    ? "  ✓"
                    : "  (pending)"),
                LabelStyle(7, UiMute));
        }

        void DrawWorkerPresenceDevPanel()
        {
            if (_crewWorkers == null || _crewWorkers.Length == 0) return;

            const float pw = 248f;
            float ph = 28f + _crewWorkers.Length * 78f + 28f;
            // Right-docked pop-up (was left — overlapped roster)
            float bx = Screen.width - pw - 12f - HudToolStripReserve;
            float by = 96f;
            var r = new Rect(bx, by, pw, Mathf.Min(ph, Screen.height - by - 8f));
            DrawCyberPanel(r, lit: true, accentOverride: UiCyan);
            Block(r);

            float x = r.x + 8f;
            float y = r.y + 6f;
            float inner = pw - 16f;

            GUI.Label(new Rect(x, y, inner, 12f), "DEV // PRESENCE · F4 SHIFT",
                LabelStyle(8, UiMute, bold: true));
            y += 14f;

            var tog = new Rect(x, y, inner, 18f);
            Block(tog);
            if (DrawCyberButton(tog,
                    _devForceShowHiddenAvatars ? "GHOST HIDDEN · ON" : "GHOST HIDDEN · OFF",
                    selected: _devForceShowHiddenAvatars, accent: UiAmber))
            {
                _devForceShowHiddenAvatars = !_devForceShowHiddenAvatars;
                if (_crewPhase == CrewPhase.OnShift)
                    RefreshAllAvatarPresence();
                else if (_crewWorkers != null)
                {
                    for (int i = 0; i < _crewWorkers.Length; i++)
                    {
                        var w = _crewWorkers[i];
                        if (w == null) continue;
                        _presence.Get(w.WorkerId)?.SetDevForceShowHidden(_devForceShowHiddenAvatars);
                    }
                }
            }
            y += 22f;

            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null) continue;
                var asg = _assignments.GetAssignment(wr.WorkerId);
                var job = asg != null ? asg.JobType : JobType.Unassigned;
                var av = _presence.Get(wr.WorkerId);
                var phys = GetPhysicalState(wr);
                bool canAct = CanPerformJobActions(wr);
                string vis = av == null ? "?" : (av.IsVisuallyHidden ? "no" : "yes");
                Vector2 aPos = av != null ? av.PresencePosition : Vector2.zero;
                string prov = job == JobType.Unassigned
                    ? "none"
                    : (asg != null ? asg.ProviderId : "—");
                Vector2 pPos = job != JobType.Unassigned
                    ? GetProviderOperatePoint(job)
                    : Vector2.zero;

                GUI.Label(new Rect(x, y, inner, 11f),
                    $"{wr.DisplayName.ToUpperInvariant()} [{wr.WorkerId}]",
                    LabelStyle(8, UiWhite, bold: true));
                y += 11f;
                GUI.Label(new Rect(x, y, inner, 10f),
                    $"Assignment: {JobStatPreview.DisplayName(job)}",
                    LabelStyle(7, UiCyan));
                y += 10f;
                GUI.Label(new Rect(x, y, inner, 10f),
                    $"Physical: {phys}",
                    LabelStyle(7, UiAmber));
                y += 10f;
                GUI.Label(new Rect(x, y, inner, 10f),
                    $"Avatar Visible: {vis}  @{aPos.x:0.00},{aPos.y:0.00}",
                    LabelStyle(6, UiDim));
                y += 10f;
                GUI.Label(new Rect(x, y, inner, 10f),
                    job == JobType.Unassigned
                        ? "Provider: none"
                        : $"Provider: {prov}  @{pPos.x:0.00},{pPos.y:0.00}",
                    LabelStyle(6, UiGreen));
                y += 10f;
                GUI.Label(new Rect(x, y, inner, 10f),
                    $"CanPerformJobActions: {canAct.ToString().ToLowerInvariant()}",
                    LabelStyle(6, canAct ? UiGreen : UiMute));
                y += 14f;
            }
        }

        void DrawSocialAuraDevPanel()
        {
            if (_crewWorkers == null || _crewWorkers.Length == 0) return;

            const float pw = 340f;
            float ph = Mathf.Min(560f, Screen.height - 104f);
            float bx = Screen.width - pw - 12f - HudToolStripReserve;
            float by = 96f;
            var r = new Rect(bx, by, pw, ph);
            DrawCyberPanel(r, lit: true, accentOverride: new Color(0.45f, 0.85f, 1f));
            Block(r);

            float pad = 8f;
            float x = r.x + pad;
            float y = r.y + 6f;
            float inner = pw - pad * 2f;

            var wr = FindCrewWorker(_selectedWorkerId) ?? _crewWorkers[0];
            if (wr == null) return;
            bool alive = wr.IsAlive;
            string aliveLabel = alive ? "ALIVE" : "DEAD";
            Color aliveCol = alive ? UiGreen : new Color(1f, 0.32f, 0.28f, 1f);

            GUI.Label(new Rect(x, y, inner, 12f), "SOCIAL · RESULTS",
                LabelStyle(8, UiMute, bold: true));
            y += 14f;
            GUI.Label(new Rect(x, y, inner, 12f),
                $"{wr.DisplayName.ToUpperInvariant()}  ·  {aliveLabel}",
                LabelStyle(9, aliveCol, bold: true));
            y += 14f;

            var tog = new Rect(x, y, inner, 18f);
            Block(tog);
            if (DrawCyberButton(tog,
                    _socialDevDrawWorld ? "WORLD DEBUG · ON" : "WORLD DEBUG · OFF",
                    selected: _socialDevDrawWorld, accent: UiAmber))
                _socialDevDrawWorld = !_socialDevDrawWorld;
            y += 22f;

            float frBtnW = (inner - 6f) * 0.5f;
            var frAdd = new Rect(x, y, frBtnW, 18f);
            var frReset = new Rect(x + frBtnW + 6f, y, frBtnW, 18f);
            Block(frAdd);
            Block(frReset);
            if (DrawCyberButton(frAdd, "DEV +25 FRUST", selected: false, accent: UiAmber))
                DevAddFrustrationAll(25f);
            if (DrawCyberButton(frReset, "DEV RESET FRUST", selected: false, accent: UiDim))
                DevResetFrustrationAll();
            y += 22f;

            int pairId = ResolveSocialDevPairId(wr.WorkerId);
            _socialDevPairId = pairId;
            var pairWr = pairId > 0 ? FindCrewWorker(pairId) : null;

            float viewH = r.yMax - y - 6f;
            var view = new Rect(x, y, inner, viewH);
            float contentW = inner - 14f;
            float contentH = 920f + (_crewWorkers != null ? _crewWorkers.Length * 22f : 0f)
                + (_socialAura.Conflict != null ? _socialAura.Conflict.DevHistory.Count * 12f : 0f);
            _socialDevScroll = GUI.BeginScrollView(view, _socialDevScroll,
                new Rect(0f, 0f, contentW, contentH), false, true);
            float sx = 0f;
            float sy = 0f;
            float sw = contentW;

            // ——— PAIR SELECTION ———
            GUI.Label(new Rect(sx, sy, sw, 11f), "PAIR", LabelStyle(8, UiMute, bold: true));
            sy += 13f;

            _socialAura.FillNearbyDebug(
                wr.WorkerId, _crewWorkers, _presence, MapSocialPresence,
                id =>
                {
                    var asg = _assignments.GetAssignment(id);
                    return asg != null ? asg.JobType : JobType.Unassigned;
                },
                _socialNearbyScratch);

            float btnH = 18f;
            float gap = 4f;
            float colW = (sw - gap) * 0.5f;
            int drawn = 0;
            if (_crewWorkers != null)
            {
                // Prefer nearby living first, then remaining living crew.
                var order = new List<WorkerRuntime>(8);
                for (int i = 0; i < _socialNearbyScratch.Count; i++)
                {
                    var n = FindCrewWorker(_socialNearbyScratch[i].OtherId);
                    if (n != null && n.WorkerId != wr.WorkerId && n.IsAlive && !order.Contains(n))
                        order.Add(n);
                }
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    var o = _crewWorkers[i];
                    if (o == null || o.WorkerId == wr.WorkerId || !o.IsAlive) continue;
                    if (!order.Contains(o)) order.Add(o);
                }

                for (int i = 0; i < order.Count; i++)
                {
                    var o = order[i];
                    int col = drawn % 2;
                    int row = drawn / 2;
                    float bxBtn = sx + col * (colW + gap);
                    float byBtn = sy + row * (btnH + gap);
                    var br = new Rect(bxBtn, byBtn, colW, btnH);
                    bool sel = o.WorkerId == pairId;
                    if (DrawCyberButton(br, o.DisplayName.ToUpperInvariant(), selected: sel, accent: UiCyan))
                        _socialDevPairId = o.WorkerId;
                    drawn++;
                }
                if (drawn > 0)
                    sy += ((drawn + 1) / 2) * (btnH + gap) + 4f;
                else
                {
                    GUI.Label(new Rect(sx, sy, sw, 11f), "(no other living crew)", LabelStyle(8, UiDim));
                    sy += 14f;
                }
            }

            pairId = _socialDevPairId;
            pairWr = pairId > 0 ? FindCrewWorker(pairId) : null;

            // ——— RELATIONSHIP ———
            GUI.Label(new Rect(sx, sy, sw, 11f), "RELATIONSHIP", LabelStyle(8, UiMute, bold: true));
            sy += 13f;

            if (pairWr == null)
            {
                GUI.Label(new Rect(sx, sy, sw, 11f), "(select a pair partner)", LabelStyle(8, UiDim));
                sy += 14f;
            }
            else
            {
                var memStore = _socialAura.Memory;
                var pairRel = _socialAura.Relation(wr.WorkerId, pairWr.WorkerId);
                var towardPair = memStore != null
                    ? memStore.GetToward(wr.WorkerId, pairWr.WorkerId)
                    : System.Array.Empty<SocialMemoryEntry>();
                var relClass = RelationshipClassifier.Classify(pairRel, towardPair, out _);
                string traj = SocialDevTrajectoryLabel(wr.WorkerId, pairWr.WorkerId);
                var intent = SocialIntentModel.Evaluate(
                    wr.WorkerId, pairWr.WorkerId, pairRel, towardPair, wr.State, wr.Stats);
                string intentLabel = intent.Kind switch
                {
                    SocialIntentKind.Seek => "Seek",
                    SocialIntentKind.Avoid => "Avoid",
                    _ => "Neutral",
                };
                float frA = wr.State != null ? wr.State.Frustration : 0f;
                float frB = pairWr.State != null ? pairWr.State.Frustration : 0f;
                string tension = SocialDevTensionLabel(pairRel.Hostility, frA, frB);
                Color tensCol = tension switch
                {
                    "Critical" => new Color(1f, 0.28f, 0.28f, 1f),
                    "Hostile" => new Color(1f, 0.4f, 0.28f, 1f),
                    "Tense" => UiAmber,
                    "Irritated" => UiAmber,
                    _ => UiGreen,
                };

                GUI.Label(new Rect(sx, sy, sw, 11f),
                    $"{wr.DisplayName} ↔ {pairWr.DisplayName}",
                    LabelStyle(8, UiCyan, bold: true));
                sy += 12f;
                GUI.Label(new Rect(sx, sy, sw, 11f),
                    $"{relClass}  ·  {traj}  ·  {intentLabel}",
                    LabelStyle(8, UiWhite));
                sy += 12f;
                GUI.Label(new Rect(sx, sy, sw, 11f),
                    $"Tension  {tension}",
                    LabelStyle(8, tensCol, bold: true));
                sy += 14f;

                DrawSocialRelAxisBar(ref sy, sx, sw, "TRUST",
                    (pairRel.Trust - SocialDirectedRelation.Min)
                    / (SocialDirectedRelation.Max - SocialDirectedRelation.Min), UiCyan);
                DrawSocialRelAxisBar(ref sy, sx, sw, "WARMTH",
                    (pairRel.Warmth - SocialDirectedRelation.Min)
                    / (SocialDirectedRelation.Max - SocialDirectedRelation.Min), UiGreen);
                DrawSocialRelAxisBar(ref sy, sx, sw, "HOSTILITY",
                    Mathf.Clamp01(pairRel.Hostility / SocialDirectedRelation.Max),
                    new Color(1f, 0.35f, 0.3f, 1f));
                DrawSocialRelAxisBar(ref sy, sx, sw, "RESPECT",
                    Mathf.Clamp01(pairRel.Respect / SocialDirectedRelation.RespectMax), UiAmber);
                sy += 4f;

                // ——— WHAT'S HAPPENING ———
                GUI.Label(new Rect(sx, sy, sw, 11f), "WHAT'S HAPPENING",
                    LabelStyle(8, UiMute, bold: true));
                sy += 13f;
                string summary = BuildSocialPairPlainSummary(wr, pairWr);
                GUI.Label(new Rect(sx, sy, sw, 28f), TruncateDev(summary, 96),
                    LabelStyle(8, UiWhite));
                sy += 30f;

                // ——— IMPORTANT HISTORY ———
                GUI.Label(new Rect(sx, sy, sw, 11f), "IMPORTANT HISTORY",
                    LabelStyle(8, UiMute, bold: true));
                sy += 13f;
                FillSocialPairImportantHistory(wr.WorkerId, pairWr.WorkerId, _socialMemoryScratch, 6);
                if (_socialMemoryScratch.Count == 0)
                {
                    GUI.Label(new Rect(sx, sy, sw, 11f), "(none yet)", LabelStyle(8, UiDim));
                    sy += 13f;
                }
                else
                {
                    for (int m = 0; m < _socialMemoryScratch.Count; m++)
                    {
                        var e = _socialMemoryScratch[m];
                        string whoObs = FindCrewWorker(e.ObserverId)?.DisplayName ?? $"#{e.ObserverId}";
                        string whoTgt = FindCrewWorker(e.TargetId)?.DisplayName ?? $"#{e.TargetId}";
                        string phrase = SocialMemoryPlainPhrase(e.Type);
                        string when = FormatSocialConflictClock(e.GameTime);
                        GUI.Label(new Rect(sx, sy, sw, 11f),
                            $"{when}  {whoObs}: {phrase} ({whoTgt})",
                            LabelStyle(8, UiWhite));
                        sy += 12f;
                    }
                }
                sy += 4f;

                // ——— CURRENT CONFLICT ———
                GUI.Label(new Rect(sx, sy, sw, 11f), "CURRENT CONFLICT",
                    LabelStyle(8, UiMute, bold: true));
                sy += 13f;
                string conflictPlain = BuildCurrentConflictPlain(wr.WorkerId, pairWr.WorkerId);
                var conflictLines = conflictPlain.Split('\n');
                for (int ci = 0; ci < conflictLines.Length; ci++)
                {
                    GUI.Label(new Rect(sx, sy, sw, 11f), conflictLines[ci],
                        LabelStyle(8, conflictPlain.StartsWith("No ") ? UiDim : UiAmber));
                    sy += 12f;
                }
                sy += 4f;
            }

            // ——— CREW OVERVIEW ———
            GUI.Label(new Rect(sx, sy, sw, 11f), "CREW OVERVIEW", LabelStyle(8, UiMute, bold: true));
            sy += 13f;
            int noteworthy = 0;
            if (_crewWorkers != null && _socialAura.IsBootstrapped)
            {
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    var a = _crewWorkers[i];
                    if (a == null) continue;
                    if (!a.IsAlive)
                    {
                        GUI.Label(new Rect(sx, sy, sw, 11f),
                            $"{a.DisplayName}  ·  DEAD",
                            LabelStyle(8, new Color(1f, 0.32f, 0.28f, 1f)));
                        sy += 12f;
                        noteworthy++;
                    }
                }
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    var a = _crewWorkers[i];
                    if (a == null || !a.IsAlive) continue;
                    for (int j = i + 1; j < _crewWorkers.Length; j++)
                    {
                        var b = _crewWorkers[j];
                        if (b == null || !b.IsAlive) continue;
                        var rel = _socialAura.Relation(a.WorkerId, b.WorkerId);
                        float frAvg = 0f;
                        int frN = 0;
                        if (a.State != null) { frAvg += a.State.Frustration; frN++; }
                        if (b.State != null) { frAvg += b.State.Frustration; frN++; }
                        if (frN > 0) frAvg /= frN;
                        string tens = SocialDevTensionLabel(rel.Hostility, frAvg, frAvg);
                        bool active = _socialAura.Conflict != null
                            && _socialAura.Conflict.HasActiveArgument(a.WorkerId, b.WorkerId);
                        if (!active && tens != "Hostile" && tens != "Critical")
                            continue;
                        string tag = active ? "CONFLICT" : tens.ToUpperInvariant();
                        Color c = active || tens == "Critical"
                            ? new Color(1f, 0.32f, 0.28f, 1f)
                            : UiAmber;
                        GUI.Label(new Rect(sx, sy, sw, 11f),
                            $"{a.DisplayName} ↔ {b.DisplayName}  ·  {tag}",
                            LabelStyle(8, c));
                        sy += 12f;
                        noteworthy++;
                    }
                }
            }
            if (noteworthy == 0)
            {
                GUI.Label(new Rect(sx, sy, sw, 11f), "(quiet crew)", LabelStyle(8, UiDim));
                sy += 13f;
            }
            sy += 4f;

            // ——— CONFLICT HISTORY ———
            GUI.Label(new Rect(sx, sy, sw, 11f), "CONFLICT HISTORY",
                LabelStyle(8, UiMute, bold: true));
            sy += 13f;
            var hist = _socialAura.Conflict != null ? _socialAura.Conflict.DevHistory : null;
            if (hist == null || hist.Count == 0)
            {
                GUI.Label(new Rect(sx, sy, sw, 11f), "(none yet)", LabelStyle(8, UiDim));
                sy += 13f;
            }
            else
            {
                int shown = 0;
                for (int i = hist.Count - 1; i >= 0 && shown < 16; i--)
                {
                    var e = hist[i];
                    if (e == null) continue;
                    GUI.Label(new Rect(sx, sy, sw, 11f),
                        TruncateDev(FormatSocialConflictHistoryLine(e), 52),
                        LabelStyle(8, UiWhite));
                    sy += 12f;
                    shown++;
                }
            }
            sy += 6f;

            // ——— DEV CONTROLS (Round 8) ———
            GUI.Label(new Rect(sx, sy, sw, 11f), "DEV CONTROLS", LabelStyle(8, UiMute, bold: true));
            sy += 13f;

            bool pairOk = pairWr != null && wr != null;
            float half = (sw - gap) * 0.5f;

            void DevPresetBtn(ref float yPos, string label, string preset, Color accent)
            {
                var br = new Rect(sx, yPos, sw, 18f);
                if (DrawCyberButton(br, label, selected: false, accent: accent) && pairOk)
                {
                    ApplySocialDevPairPreset(preset, wr.WorkerId, pairWr.WorkerId);
                    DigHoodLog.Push(
                        $"DEV | SOCIAL PRESET {preset} → {wr.DisplayName}↔{pairWr.DisplayName}");
                }
                yPos += 22f;
            }

            DevPresetBtn(ref sy, "HEALTHY", "Healthy", UiGreen);
            DevPresetBtn(ref sy, "RIVALRY", "Rivalry", UiAmber);
            DevPresetBtn(ref sy, "GRUDGE", "Grudge", new Color(1f, 0.4f, 0.28f, 1f));
            DevPresetBtn(ref sy, "CRITICAL CONFLICT", "CriticalConflict", new Color(1f, 0.28f, 0.28f, 1f));

            // ——— DIALOGUE VISUAL EXAMPLES (presentation only) ———
            GUI.Label(new Rect(sx, sy, sw, 11f), "DIALOGUE VISUALS", LabelStyle(8, UiMute, bold: true));
            sy += 13f;
            float q = (sw - gap * 3f) * 0.25f;
            void ToneExample(ref float yPos, float xOff, float wBtn, string label,
                SocialSpeechValence valence, Color accent, string line)
            {
                var br = new Rect(sx + xOff, yPos, wBtn, 18f);
                if (DrawCyberButton(br, label, selected: false, accent: accent) && wr != null)
                {
                    JobType job = _assignments.GetAssignment(wr.WorkerId)?.JobType ?? JobType.Prospecting;
                    TryAuthoredSocialBanter(
                        wr.WorkerId, wr.DisplayName, job,
                        $"dev/{SocialSpeechVisuals.Label(valence).ToLowerInvariant()}",
                        line, valence);
                    DigHoodLog.Push(
                        $"DEV | DIALOGUE {SocialSpeechVisuals.Label(valence)} → {wr.DisplayName}");
                }
            }
            ToneExample(ref sy, 0f, q, "POS", SocialSpeechValence.Positive, UiGreen,
                "Good work — keep that pace.");
            ToneExample(ref sy, q + gap, q, "NEU", SocialSpeechValence.Neutral, UiCyan,
                "Copy. Moving on the next sector.");
            ToneExample(ref sy, (q + gap) * 2f, q, "NEG", SocialSpeechValence.Negative, UiAmber,
                "That joke landed wrong. Don't.");
            ToneExample(ref sy, (q + gap) * 3f, q, "SEV", SocialSpeechValence.Severe,
                new Color(1f, 0.28f, 0.32f, 1f),
                "Back off — now.");
            sy += 22f;

            var argR = new Rect(sx, sy, half, 18f);
            var fightR = new Rect(sx + half + gap, sy, half, 18f);
            if (DrawCyberButton(argR, "FORCE ARGUMENT", selected: false, accent: UiAmber) && pairOk)
            {
                var ev = _socialAura.Conflict.ForceArgument(
                    _socialAura.World, wr.WorkerId, pairWr.WorkerId, _absoluteGameHours);
                DigHoodLog.Push(ev != null
                    ? $"DEV | FORCE ARGUMENT {wr.DisplayName}↔{pairWr.DisplayName}"
                    : $"DEV | FORCE ARGUMENT failed {wr.DisplayName}↔{pairWr.DisplayName}");
            }
            if (DrawCyberButton(fightR, "FORCE FIGHT", selected: false, accent: new Color(1f, 0.4f, 0.28f, 1f))
                && pairOk)
            {
                var ev = _socialAura.Conflict.ForceFight(
                    _socialAura.World, wr.WorkerId, pairWr.WorkerId, _absoluteGameHours);
                DigHoodLog.Push(ev != null
                    ? $"DEV | FORCE FIGHT {wr.DisplayName}↔{pairWr.DisplayName}"
                    : $"DEV | FORCE FIGHT failed {wr.DisplayName}↔{pairWr.DisplayName}");
            }
            sy += 22f;

            var lethalR = new Rect(sx, sy, sw, 18f);
            if (DrawCyberButton(lethalR, "FORCE LETHAL PIPELINE", selected: false,
                    accent: new Color(1f, 0.28f, 0.28f, 1f)) && pairOk)
            {
                int killerId = wr.WorkerId;
                int victimId = pairWr.WorkerId;
                var ev = _socialAura.Conflict.ForceLethalPipeline(
                    _socialAura.World, killerId, victimId, _absoluteGameHours);
                if (ev != null && (ev.IsLethal || ev.Outcome == SocialArgumentOutcome.Death))
                {
                    int deadId = ev.VictimId > 0 ? ev.VictimId : victimId;
                    var deadWr = FindCrewWorker(deadId);
                    _assignments.Unassign(deadId);
                    DigHoodLog.Push(
                        $"DEV | FORCE LETHAL — {(deadWr != null ? deadWr.DisplayName : $"#{deadId}")} dead; unassigned");
                }
                else
                {
                    DigHoodLog.Push(
                        $"DEV | FORCE LETHAL failed {wr.DisplayName}→{pairWr.DisplayName}");
                }
            }
            sy += 24f;

            GUI.EndScrollView();
        }

        int ResolveSocialDevPairId(int selectedId)
        {
            if (_crewWorkers == null) return 0;
            bool PairValid(int id)
            {
                if (id <= 0 || id == selectedId) return false;
                var w = FindCrewWorker(id);
                return w != null && w.IsAlive;
            }

            if (PairValid(_socialDevPairId))
                return _socialDevPairId;

            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var o = _crewWorkers[i];
                if (o != null && o.WorkerId != selectedId && o.IsAlive)
                    return o.WorkerId;
            }
            return 0;
        }

        static string SocialDevTensionLabel(float hostility, float frustrationA, float frustrationB)
        {
            float fr = Mathf.Max(frustrationA, frustrationB);
            if (hostility >= 16f || (hostility >= 14f && fr >= 80f)) return "Critical";
            if (hostility >= 12f) return "Hostile";
            if (hostility >= 7.5f) return "Tense";
            if (hostility >= 3f || fr >= 55f) return "Irritated";
            return "Calm";
        }

        string SocialDevTrajectoryLabel(int observerId, int targetId)
        {
            if (_socialAura?.Trajectory == null) return "Stable";
            _socialAura.Trajectory.GetRecent(observerId, targetId, _trajectoryScratch, 6);
            if (_trajectoryScratch.Count == 0) return "Stable";

            float score = 0f;
            for (int i = 0; i < _trajectoryScratch.Count; i++)
            {
                var e = _trajectoryScratch[i];
                float d = e.To - e.From;
                if (e.Axis == RelationshipAxis.Hostility)
                    score -= d;
                else
                    score += d;
            }
            if (score > 0.8f) return "Improving";
            if (score < -0.8f) return "Deteriorating";
            return "Stable";
        }

        string BuildSocialPairPlainSummary(WorkerRuntime wrA, WorkerRuntime wrB)
        {
            if (wrA == null || wrB == null) return "No pair selected.";
            var rel = _socialAura.Relation(wrA.WorkerId, wrB.WorkerId);
            var mem = _socialAura.Memory != null
                ? _socialAura.Memory.GetToward(wrA.WorkerId, wrB.WorkerId)
                : System.Array.Empty<SocialMemoryEntry>();
            var cls = RelationshipClassifier.Classify(rel, mem, out _);
            var intent = SocialIntentModel.Evaluate(
                wrA.WorkerId, wrB.WorkerId, rel, mem, wrA.State, wrB.Stats);
            float frA = wrA.State != null ? wrA.State.Frustration : 0f;
            float frB = wrB.State != null ? wrB.State.Frustration : 0f;
            string tension = SocialDevTensionLabel(rel.Hostility, frA, frB);
            bool active = _socialAura.Conflict != null
                && _socialAura.Conflict.HasActiveArgument(wrA.WorkerId, wrB.WorkerId);
            string na = wrA.DisplayName;
            string nb = wrB.DisplayName;

            if (active)
                return $"Repeated arguments are damaging the bond between {na} and {nb}.";
            if (intent.Kind == SocialIntentKind.Avoid)
                return $"{na} is actively avoiding {nb}.";
            if (intent.Kind == SocialIntentKind.Seek && rel.Warmth >= 4f)
                return $"{na} seeks {nb}'s company.";
            if (cls == RelationshipClass.Rivalry)
                return $"They dislike each other but still respect each other's work.";
            if (cls == RelationshipClass.Grudge)
                return $"{na} holds a lasting grudge against {nb}.";
            if (cls == RelationshipClass.Bonded || cls == RelationshipClass.Friendly)
                return $"{na} and {nb} get along — the bond holds.";
            if (cls == RelationshipClass.Professional)
                return $"{na} and {nb} keep it professional.";
            if (tension == "Critical" || tension == "Hostile")
                return $"{na} is becoming increasingly hostile toward {nb}.";
            if (tension == "Tense" || tension == "Irritated")
                return $"Tension is rising between {na} and {nb}.";
            if (SocialDevTrajectoryLabel(wrA.WorkerId, wrB.WorkerId) == "Deteriorating")
                return $"The relationship between {na} and {nb} is sliding.";
            if (SocialDevTrajectoryLabel(wrA.WorkerId, wrB.WorkerId) == "Improving")
                return $"Things are slowly improving between {na} and {nb}.";
            return $"{na} and {nb} are mostly neutral.";
        }

        static string SocialMemoryPlainPhrase(SocialMemoryType t) => t switch
        {
            SocialMemoryType.HelpedMe => "helped",
            SocialMemoryType.SupportedMe => "supported",
            SocialMemoryType.TookMySide => "took their side",
            SocialMemoryType.WorkedWellTogether => "worked well together",
            SocialMemoryType.SharedSuccess => "shared a success",
            SocialMemoryType.SharedHardship => "shared hardship",
            SocialMemoryType.Apologized => "apologized",
            SocialMemoryType.LetMeDown => "let them down",
            SocialMemoryType.BlamedMe => "blamed them",
            SocialMemoryType.InsultedMe => "insulted them",
            SocialMemoryType.FailedTogether => "failed together",
            SocialMemoryType.HurtBy => "hurt them in a fight",
            SocialMemoryType.WitnessedViolence => "saw a fight",
            SocialMemoryType.KilledBy => "killed them",
            SocialMemoryType.WitnessedDeath => "witnessed a death",
            _ => t.ToString(),
        };

        void FillSocialPairImportantHistory(
            int idA, int idB, List<SocialMemoryEntry> into, int max)
        {
            into.Clear();
            if (_socialAura.Memory == null || max <= 0) return;
            var ranked = new List<SocialMemoryEntry>(24);
            void Collect(IReadOnlyList<SocialMemoryEntry> src)
            {
                if (src == null) return;
                for (int i = 0; i < src.Count; i++)
                {
                    var e = src[i];
                    if (e == null) continue;
                    if (e.Significance == SocialMemorySignificance.Ordinary && e.Strength < 0.55f)
                        continue;
                    if (!IsSocialHistoryMemory(e.Type)) continue;
                    ranked.Add(e);
                }
            }
            Collect(_socialAura.Memory.GetToward(idA, idB));
            Collect(_socialAura.Memory.GetToward(idB, idA));
            ranked.Sort((a, b) => a.GameTime.CompareTo(b.GameTime));
            for (int i = 0; i < ranked.Count && into.Count < max; i++)
                into.Add(ranked[i]);
        }

        static bool IsSocialHistoryMemory(SocialMemoryType t) =>
            t == SocialMemoryType.InsultedMe
            || t == SocialMemoryType.BlamedMe
            || t == SocialMemoryType.SupportedMe
            || t == SocialMemoryType.HelpedMe
            || t == SocialMemoryType.TookMySide
            || t == SocialMemoryType.Apologized
            || t == SocialMemoryType.HurtBy
            || t == SocialMemoryType.WitnessedViolence
            || t == SocialMemoryType.KilledBy
            || t == SocialMemoryType.WitnessedDeath
            || t == SocialMemoryType.FailedTogether
            || t == SocialMemoryType.LetMeDown;

        string BuildCurrentConflictPlain(int idA, int idB)
        {
            var session = _socialAura.Conflict?.GetSession(idA, idB);
            SocialConflictEvent last = null;
            var hist = _socialAura.Conflict?.DevHistory;
            if (hist != null)
            {
                for (int i = hist.Count - 1; i >= 0; i--)
                {
                    var e = hist[i];
                    if (e == null) continue;
                    bool involves =
                        (e.WorkerA == idA && e.WorkerB == idB)
                        || (e.WorkerA == idB && e.WorkerB == idA)
                        || (e.KillerId == idA && e.VictimId == idB)
                        || (e.KillerId == idB && e.VictimId == idA);
                    if (!involves) continue;
                    last = e;
                    break;
                }
            }

            bool live = session != null && session.IsActive;
            bool lethal = session != null
                && (session.LethalOccurred || session.FightSeverity == SocialFightSeverity.Lethal);
            bool fight = session != null && session.FightOccurred;
            bool lastSerious = last != null
                && (last.IsLethal
                    || last.IsFight
                    || last.Outcome == SocialArgumentOutcome.Death
                    || last.Outcome == SocialArgumentOutcome.CriticalInjury
                    || last.FightSeverity >= SocialFightSeverity.Severe);

            if (!live && !lethal && !fight && !lastSerious)
                return "No active conflict.";

            string na = FindCrewWorker(idA)?.DisplayName ?? $"#{idA}";
            string nb = FindCrewWorker(idB)?.DisplayName ?? $"#{idB}";

            if (lethal)
            {
                string killer = FindCrewWorker(session.KillerId)?.DisplayName
                    ?? (last != null ? FindCrewWorker(last.KillerId)?.DisplayName : null)
                    ?? "?";
                string victim = FindCrewWorker(session.VictimId)?.DisplayName
                    ?? (last != null ? FindCrewWorker(last.VictimId)?.DisplayName : null)
                    ?? "?";
                string wit = BuildWitnessPlain(last);
                return $"LETHAL\n{na} ↔ {nb}\n{victim} killed by {killer}"
                    + (string.IsNullOrEmpty(wit) ? "" : $"\n{wit}");
            }

            if (fight)
            {
                string sev = session.FightSeverity switch
                {
                    SocialFightSeverity.Lethal => "Lethal",
                    SocialFightSeverity.Critical => "Critical",
                    SocialFightSeverity.Severe => "Severe",
                    SocialFightSeverity.Scuffle => "Scuffle",
                    _ => "Fight",
                };
                string injury = "";
                if (last != null)
                {
                    if (last.InjuryA >= 40f || last.InjuryB >= 40f)
                        injury = "\nSerious injury";
                    else if (last.InjuryA > 0f || last.InjuryB > 0f)
                        injury = "\nSomeone was hurt";
                }
                string wit = BuildWitnessPlain(last);
                return $"FIGHT\n{na} ↔ {nb}\n{sev}{injury}"
                    + (string.IsNullOrEmpty(wit) ? "" : $"\n{wit}");
            }

            if (live)
            {
                string phase = session.Phase switch
                {
                    SocialArgumentPhase.Opening => "Opening",
                    SocialArgumentPhase.Exchange => "Ongoing",
                    SocialArgumentPhase.Peak => "Escalating",
                    SocialArgumentPhase.Resolving => "Winding down",
                    _ => session.Phase.ToString(),
                };
                return $"ARGUMENT\n{na} ↔ {nb}\n{phase}";
            }

            if (last != null)
            {
                if (last.IsLethal || last.Outcome == SocialArgumentOutcome.Death)
                {
                    string killer = FindCrewWorker(last.KillerId)?.DisplayName ?? "?";
                    string victim = FindCrewWorker(last.VictimId)?.DisplayName ?? "?";
                    return $"LETHAL\n{na} ↔ {nb}\n{victim} killed by {killer}";
                }
                if (last.IsFight || last.FightSeverity >= SocialFightSeverity.Severe)
                    return $"FIGHT\n{na} ↔ {nb}\n{last.FightSeverity}";
                if (last.Outcome == SocialArgumentOutcome.CriticalInjury)
                    return $"SEVERE\n{na} ↔ {nb}\nCritical injury";
            }

            return "No active conflict.";
        }

        string BuildWitnessPlain(SocialConflictEvent last)
        {
            if (last == null || !last.IsWitness || last.WitnessId <= 0) return "";
            string wit = FindCrewWorker(last.WitnessId)?.DisplayName ?? $"#{last.WitnessId}";
            return last.WitnessAction switch
            {
                SocialWitnessAction.VerbalIntervene => $"{wit} intervening verbally",
                SocialWitnessAction.SupportSomeone => $"{wit} taking a side",
                SocialWitnessAction.DeEscalate => $"{wit} trying to calm them",
                SocialWitnessAction.BreakUpFight => $"{wit} breaking up the fight",
                _ => $"{wit} watching",
            };
        }

        string FormatSocialConflictHistoryLine(SocialConflictEvent e)
        {
            string when = FormatSocialConflictClock(e.GameHours);
            string na = FindCrewWorker(e.WorkerA)?.DisplayName ?? $"#{e.WorkerA}";
            string nb = FindCrewWorker(e.WorkerB)?.DisplayName ?? $"#{e.WorkerB}";
            string kind;
            if (e.IsLethal || e.Outcome == SocialArgumentOutcome.Death
                || e.FightSeverity == SocialFightSeverity.Lethal)
                kind = "LETHAL";
            else if (e.Outcome == SocialArgumentOutcome.CriticalInjury
                     || e.FightSeverity == SocialFightSeverity.Critical)
                kind = "CRITICAL";
            else if (e.IsSevereFight || e.FightSeverity == SocialFightSeverity.Severe)
                kind = "SEVERE";
            else if (e.IsFight)
                kind = "FIGHT";
            else if (e.IsWitness)
                kind = "WITNESS";
            else
                kind = "ARGUMENT";

            string outcome = e.Outcome != SocialArgumentOutcome.None
                ? PlainConflictOutcome(e.Outcome)
                : (e.IsWitness ? PlainWitnessAction(e.WitnessAction) : e.Phase.ToString());
            return $"{when}  {kind}  {na}↔{nb}  {outcome}";
        }

        static string PlainConflictOutcome(SocialArgumentOutcome o) => o switch
        {
            SocialArgumentOutcome.BacksDown => "backed down",
            SocialArgumentOutcome.MutualDisengage => "disengaged",
            SocialArgumentOutcome.PartialResolution => "partial calm",
            SocialArgumentOutcome.Apology => "apology",
            SocialArgumentOutcome.GrudgeStrengthened => "grudge deepened",
            SocialArgumentOutcome.RelationshipWorsens => "relations worsened",
            SocialArgumentOutcome.EscalateFurther => "escalated",
            SocialArgumentOutcome.FightBreaksOut => "fight broke out",
            SocialArgumentOutcome.CriticalInjury => "critical injury",
            SocialArgumentOutcome.Death => "death",
            _ => o.ToString(),
        };

        static string PlainWitnessAction(SocialWitnessAction a) => a switch
        {
            SocialWitnessAction.VerbalIntervene => "verbal intervene",
            SocialWitnessAction.SupportSomeone => "took a side",
            SocialWitnessAction.DeEscalate => "de-escalated",
            SocialWitnessAction.BreakUpFight => "broke up fight",
            SocialWitnessAction.Ignore => "ignored",
            _ => a.ToString(),
        };

        static string FormatSocialConflictClock(float gameHours)
        {
            ProspectorScanFormulas.FormatDayClock(gameHours, out int day, out int hour, out int minute);
            return $"D{day} {hour:00}:{minute:00}";
        }

        void DrawSocialRelAxisBar(ref float sy, float sx, float sw, string label, float fill01, Color col)
        {
            GUI.Label(new Rect(sx, sy, 64f, 10f), label, LabelStyle(7, UiMute));
            float barX = sx + 66f;
            float barW = sw - 66f;
            float barH = 8f;
            var prev = GUI.color;
            GUI.color = new Color(0.06f, 0.1f, 0.14f, 0.85f);
            GUI.DrawTexture(new Rect(barX, sy + 1f, barW, barH), Texture2D.whiteTexture);
            float fill = Mathf.Clamp01(fill01) * barW;
            if (fill > 0.01f)
            {
                GUI.color = new Color(col.r, col.g, col.b, 0.85f);
                GUI.DrawTexture(new Rect(barX, sy + 1f, fill, barH), Texture2D.whiteTexture);
            }
            GUI.color = new Color(col.r, col.g, col.b, 0.45f);
            GUI.DrawTexture(new Rect(barX, sy + 1f, barW, 1f), Texture2D.whiteTexture);
            GUI.color = prev;
            sy += 12f;
        }

        void ApplySocialDevPairPreset(string preset, int idA, int idB)
        {
            if (!_socialAura.IsBootstrapped || idA <= 0 || idB <= 0 || idA == idB) return;
            var world = _socialAura.World;
            var memory = _socialAura.Memory;
            memory?.ClearPair(idA, idB);

            void SetBoth(float t, float w, float h, float r)
            {
                void One(SocialDirectedRelation rel)
                {
                    if (rel == null) return;
                    rel.Trust = t;
                    rel.Warmth = w;
                    rel.Hostility = h;
                    rel.Respect = r;
                    rel.Clamp();
                }
                One(world.Relation(idA, idB));
                One(world.Relation(idB, idA));
            }

            void Plant(int obs, int tgt, SocialMemoryType type, float str, bool major)
            {
                memory?.Add(new SocialMemoryEntry
                {
                    ObserverId = obs,
                    TargetId = tgt,
                    Type = type,
                    Strength = str,
                    GameTime = _absoluteGameHours - 2f,
                    Context = SocialContext.Camp,
                    SourceRef = "DEV_PRESET",
                    Significance = major
                        ? SocialMemorySignificance.Major
                        : SocialMemorySignificance.Significant,
                });
            }

            var wrA = FindCrewWorker(idA);
            var wrB = FindCrewWorker(idB);

            switch (preset)
            {
                case "Healthy":
                    SetBoth(t: 9f, w: 4f, h: 0.5f, r: 72f);
                    if (wrA?.State != null) wrA.State.Frustration = WorkerState.DefaultFrustration;
                    if (wrB?.State != null) wrB.State.Frustration = WorkerState.DefaultFrustration;
                    Plant(idA, idB, SocialMemoryType.WorkedWellTogether, 0.7f, false);
                    Plant(idB, idA, SocialMemoryType.HelpedMe, 0.65f, false);
                    break;

                case "Rivalry":
                    SetBoth(t: 2f, w: -2f, h: 12f, r: 78f);
                    if (wrA?.State != null) wrA.State.Frustration = 48f;
                    if (wrB?.State != null) wrB.State.Frustration = 48f;
                    Plant(idA, idB, SocialMemoryType.InsultedMe, 0.6f, false);
                    Plant(idB, idA, SocialMemoryType.WorkedWellTogether, 0.55f, false);
                    break;

                case "Grudge":
                    SetBoth(t: -7f, w: -3f, h: 10f, r: 40f);
                    if (wrA?.State != null) wrA.State.Frustration = 62f;
                    if (wrB?.State != null) wrB.State.Frustration = 58f;
                    Plant(idA, idB, SocialMemoryType.InsultedMe, 0.9f, true);
                    Plant(idA, idB, SocialMemoryType.BlamedMe, 0.85f, true);
                    Plant(idB, idA, SocialMemoryType.InsultedMe, 0.8f, true);
                    Plant(idB, idA, SocialMemoryType.LetMeDown, 0.75f, true);
                    break;

                case "CriticalConflict":
                    SetBoth(t: -10f, w: -8f, h: 17f, r: 30f);
                    if (wrA?.State != null) wrA.State.Frustration = 88f;
                    if (wrB?.State != null) wrB.State.Frustration = 85f;
                    Plant(idA, idB, SocialMemoryType.InsultedMe, 0.95f, true);
                    Plant(idA, idB, SocialMemoryType.BlamedMe, 0.9f, true);
                    Plant(idA, idB, SocialMemoryType.HurtBy, 0.9f, true);
                    Plant(idB, idA, SocialMemoryType.InsultedMe, 0.9f, true);
                    Plant(idB, idA, SocialMemoryType.FailedTogether, 0.85f, true);
                    Plant(idB, idA, SocialMemoryType.HurtBy, 0.85f, true);
                    break;
            }

            world.Get(idA)?.RefreshExpression();
            world.Get(idB)?.RefreshExpression();
        }


        /// <summary>DEV SOCIAL only — bumps Frustration on every crew person (clamped). No events.</summary>
        void DevAddFrustrationAll(float amount)
        {
            if (_crewWorkers == null || amount == 0f) return;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr?.State == null) continue;
                wr.State.AddFrustration(amount);
                _socialAura.World.Get(wr.WorkerId)?.RefreshExpression();
            }
        }

        /// <summary>DEV SOCIAL only — restores Frustration to WorkerState.DefaultFrustration. No events.</summary>
        void DevResetFrustrationAll()
        {
            if (_crewWorkers == null) return;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr?.State == null) continue;
                wr.State.Frustration = WorkerState.DefaultFrustration;
                _socialAura.World.Get(wr.WorkerId)?.RefreshExpression();
            }
        }

        static string FormatHourClock(float gameHour)
        {
            int h = Mathf.FloorToInt(gameHour) % 24;
            if (h < 0) h += 24;
            int m = Mathf.FloorToInt((gameHour - Mathf.Floor(gameHour)) * 60f);
            if (m < 0) m = 0;
            if (m > 59) m = 59;
            return $"{h:00}:{m:00}";
        }

        static string TruncateDev(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        void DrawWorkerRuntimeDevPanel()
        {
            if (_crewWorkers == null || _crewWorkers.Length == 0) return;

            const float pw = 268f;
            const float rowH = 22f;
            float ph = 22f + 6 * rowH + 10f;
            float bx = Screen.width - pw - 12f - HudToolStripReserve;
            float by = Screen.height - ph - 12f;
            if (bx < 220f) bx = 220f;
            var r = new Rect(bx, by, pw, ph);
            DrawCyberPanel(r, lit: false, accentOverride: UiDim);
            Block(r);

            float x = r.x + 10f;
            float y = r.y + 6f;
            float inner = pw - 20f;
            GUI.Label(new Rect(x, y, inner, 12f), "DEV // WORKER RUNTIME · STAGE A",
                LabelStyle(8, UiMute, bold: true));
            y += 16f;

            DrawWorkerRuntimeDevRow(ref y, x, inner, "PROSPECTOR BODY", _prospector?.AssignedWorker);
            DrawWorkerRuntimeDevRow(ref y, x, inner, "EXCAVATOR BODY", _worker?.AssignedWorker);
            DrawWorkerRuntimeDevRow(ref y, x, inner, "HAULER BODY", _hauler?.AssignedWorker);
            DrawWorkerRuntimeDevRow(ref y, x, inner, "REFINER BODY", _refiner?.AssignedWorker);
            DrawWorkerRuntimeDevRow(ref y, x, inner, "ENGINEER BODY", _engineer?.AssignedWorker);
            DrawWorkerRuntimeDevRow(ref y, x, inner, "STEWARD BODY", _steward?.AssignedWorker);
        }

        void DrawWorkerRuntimeDevRow(ref float y, float x, float inner, string body, WorkerRuntime wr)
        {
            if (wr == null)
            {
                GUI.Label(new Rect(x, y, inner, 12f), $"{body}  ·  (unbound)",
                    LabelStyle(8, UiDim));
                y += 18f;
                return;
            }

            // Body.Stats and Worker.Stats must be the same object
            bool sameRef = false;
            WorkerStats bodyStats = null;
            if (body.StartsWith("PROSPECTOR")) bodyStats = _prospector?.Stats;
            else if (body.StartsWith("EXCAVATOR")) bodyStats = _worker?.Stats;
            else if (body.StartsWith("HAULER")) bodyStats = _hauler?.Stats;
            else if (body.StartsWith("REFINER")) bodyStats = _refiner?.Stats;
            else if (body.StartsWith("ENGINEER")) bodyStats = _engineer?.Stats;
            else if (body.StartsWith("STEWARD")) bodyStats = _steward?.Stats;
            sameRef = ReferenceEquals(bodyStats, wr.Stats);

            GUI.Label(new Rect(x, y, inner, 12f),
                $"{body}  {wr.DisplayName}  id:{wr.WorkerId}  {wr.StatsRefLabel}" +
                (sameRef ? "  ✓" : "  ✗ REF MISMATCH"),
                LabelStyle(8, sameRef ? UiGreen : new Color(1f, 0.35f, 0.3f), bold: !sameRef));
            y += 18f;
        }

        void DrawWorkerAssignmentDevPanel()
        {
            if (_crewWorkers == null || _crewWorkers.Length == 0) return;

            int idx = Mathf.Clamp(_assignmentDevWorkerIndex, 0, _crewWorkers.Length - 1);
            var wr = _crewWorkers[idx];
            if (wr == null) return;

            var asg = _assignments.GetAssignment(wr.WorkerId);
            var job = asg != null ? asg.JobType : JobType.Unassigned;
            var def = JobStatPreview.Get(job);
            int nStats = def.RelevantStats.Count;

            const float pw = 300f;
            // Worker summary + 6 compact providers + stats
            float ph = 72f + 6f * 96f + 18f + nStats * 12f + 20f;
            float bx = Screen.width - pw - 12f - HudToolStripReserve;
            if (bx < 160f) bx = 160f;
            float by = Mathf.Max(8f, 96f);
            var r = new Rect(bx, by, pw, Mathf.Min(ph, Screen.height - by - 8f));
            DrawCyberPanel(r, lit: true, accentOverride: UiCyan);
            Block(r);

            float x = r.x + 10f;
            float y = r.y + 6f;
            float inner = pw - 20f;

            GUI.Label(new Rect(x, y, inner, 12f), "DEV // JOB ASSIGNMENT · STAGE E",
                LabelStyle(8, UiMute, bold: true));
            y += 14f;

            float btnW = (inner - 8f * 4) / 5f;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var w = _crewWorkers[i];
                if (w == null) continue;
                var br = new Rect(x + i * (btnW + 8f), y, btnW, 18f);
                Block(br);
                string label = w.DisplayName.Length <= 4
                    ? w.DisplayName
                    : w.DisplayName.Substring(0, 3);
                if (DrawCyberButton(br, label, selected: i == idx, accent: UiCyan))
                    _assignmentDevWorkerIndex = i;
            }
            y += 22f;

            // Worker-centric summary
            GUI.Label(new Rect(x, y, inner, 12f),
                $"{wr.DisplayName}  ·  {JobStatPreview.DisplayName(job)}",
                LabelStyle(9, UiWhite, bold: true));
            y += 13f;
            string prov = asg != null ? asg.ProviderId : "—";
            if (asg != null && !string.IsNullOrEmpty(asg.ProviderId)
                && !asg.ProviderId.Contains("."))
                prov = asg.ProviderDisplayLabel;
            GUI.Label(new Rect(x, y, inner, 11f), $"Provider: {prov}", LabelStyle(7, UiAmber));
            y += 14f;

            y = DrawAssignProviderBlock(ref y, x, inner,
                "SCANNER · PROSPECTING",
                _prospector?.AssignedWorker,
                _fieldScanner != null
                    ? $"{_fieldScanner.ProviderId} · {_fieldScanner.StateLabel}"
                    : "body.prospector (no kit)",
                JobType.Prospecting);

            y = DrawAssignProviderBlock(ref y, x, inner,
                "EXCAVATOR · EXCAVATION",
                _worker?.AssignedWorker,
                _worker != null
                    ? $"{_worker.ProviderId} · {_worker.MachineActivityLabel} · H{_worker.Heat:0}"
                    : "—",
                JobType.Excavation);

            y = DrawAssignProviderBlock(ref y, x, inner,
                "CART · HAULING",
                _hauler?.AssignedWorker,
                _hauler != null
                    ? $"{_hauler.ProviderId} · {_hauler.ActivityLabel} · cargo {_hauler.CargoCount}"
                    : "—",
                JobType.Hauling);

            y = DrawAssignProviderBlock(ref y, x, inner,
                "WASHER · REFINING",
                _refiner?.AssignedWorker,
                _refiner != null
                    ? $"{_refiner.ProviderId} · {_refiner.ActivityLabel}"
                    : "—",
                JobType.Refining);

            y = DrawAssignProviderBlock(ref y, x, inner,
                "KIT · ENGINEERING",
                _engineer?.AssignedWorker,
                _engineer != null
                    ? $"{_engineer.ProviderId} · {_engineer.WorkLabel}"
                    : "—",
                JobType.Engineering);

            y = DrawAssignProviderBlock(ref y, x, inner,
                "CAMP · STEWARD",
                _steward?.AssignedWorker,
                _steward != null
                    ? $"{_steward.ProviderId} · {_steward.WorkLabel}"
                    : "—",
                JobType.Steward);

            string statsTitle = def.RelevantStatsAreProvisional
                ? "RELEVANT STATS (PROVISIONAL)"
                : "RELEVANT STATS";
            GUI.Label(new Rect(x, y, inner, 11f), statsTitle, LabelStyle(7, UiMute, bold: true));
            y += 12f;

            if (nStats == 0)
            {
                GUI.Label(new Rect(x, y, inner, 11f), "(none)", LabelStyle(7, UiDim));
                return;
            }

            for (int i = 0; i < nStats; i++)
            {
                var sid = def.RelevantStats[i];
                int v = wr.Stats != null ? wr.Stats.Get(sid) : 0;
                GUI.Label(new Rect(x, y, inner, 11f),
                    $"{JobStatPreview.StatDisplayName(sid)}  {v}",
                    LabelStyle(7, UiWhite));
                y += 12f;
            }
        }

        float DrawAssignProviderBlock(
            ref float y, float x, float inner,
            string title,
            WorkerRuntime assigned,
            string stateLine,
            JobType job)
        {
            GUI.Label(new Rect(x, y, inner, 11f), title, LabelStyle(7, UiAmber, bold: true));
            y += 12f;
            GUI.Label(new Rect(x, y, inner, 11f),
                assigned != null
                    ? $"Assigned: {assigned.DisplayName}"
                    : "VACANT",
                LabelStyle(7, assigned != null ? UiWhite : UiAmber, bold: assigned == null));
            y += 11f;
            GUI.Label(new Rect(x, y, inner, 10f), stateLine, LabelStyle(6, UiDim));
            y += 12f;

            float abW = (inner - 4f) / 5f;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var w = _crewWorkers[i];
                if (w == null) continue;
                var abr = new Rect(x + i * (abW + 1f), y, abW, 16f);
                Block(abr);
                bool isCur = assigned != null && ReferenceEquals(assigned, w);
                bool blocked = IsJobAssignBlocked(w, job, out string blockWhy);
                string shortName = w.DisplayName.Length <= 3
                    ? w.DisplayName
                    : w.DisplayName.Substring(0, 3);
                string lab = blocked ? $"{shortName}!" : shortName;
                if (DrawCyberButton(abr, lab, selected: isCur,
                        accent: blocked ? UiDim : UiGreen)
                    && !blocked)
                {
                    if (TryAssignJob(w, job, out string why))
                        DigHoodLog.Push($"ASSIGN OK | {why}");
                    else
                        DigHoodLog.Push($"ASSIGN BLOCKED | {why}");
                }
                if (blocked && abr.Contains(Event.current.mousePosition))
                    GUI.Label(new Rect(x, y + 16f, inner, 10f), blockWhy, LabelStyle(6, UiAmber));
            }
            y += 18f;

            var unR = new Rect(x, y, inner, 16f);
            Block(unR);
            bool unBlock = !CanReleaseFromJob(job, out string unWhy);
            if (DrawCyberButton(unR, unBlock ? $"UNASSIGN ({unWhy})" : "UNASSIGN",
                    accent: unBlock ? UiDim : UiAmber)
                && !unBlock)
            {
                if (TryUnassignJob(job, out string why))
                    DigHoodLog.Push($"UNASSIGN OK | {why}");
                else
                    DigHoodLog.Push($"UNASSIGN BLOCKED | {why}");
            }
            y += 20f;
            return y;
        }

        void DrawScanHistoryBrowser()
        {
            if (!_scanHistoryBrowserOpen) return;
            if (_playerTactical == null || !_playerTactical.Visible) return;
            if (_scanHistory == null) return;

            // Dock mid-left, clear of worker roster + face monitors (~290px) and Dig Hood / CONTROLS.
            const float panelW = 300f;
            float panelX = 290f;
            float panelY = 96f;
            float panelH = Mathf.Min(440f, Screen.height - panelY - 130f);
            var panel = new Rect(panelX, panelY, panelW, panelH);
            DrawCyberPanel(panel, lit: true, accentOverride: UiAmber);
            Block(panel);

            float x = panel.x + 12f;
            float y = panel.y + 8f;
            float inner = panelW - 24f;

            GUI.Label(new Rect(x, y, inner, 14f), "SCAN HISTORY",
                LabelStyle(10, UiAmber, bold: true));
            y += 16f;
            GUI.Label(new Rect(x, y, inner, 12f), "IMMUTABLE SNAPSHOTS · NEWEST FIRST",
                LabelStyle(8, UiMute));
            y += 16f;
            DrawHLine(x, y, inner, new Color(UiAmber.r, UiAmber.g, UiAmber.b, 0.25f));
            y += 8f;

            float navH = 26f;
            float navW = (inner - 8f) * 0.5f;
            var prevR = new Rect(x, y, navW, navH);
            var nextR = new Rect(x + navW + 8f, y, navW, navH);
            Block(prevR); Block(nextR);
            if (DrawCyberButton(prevR, "< PREV", accent: UiCyan))
                _scanHistory.TrySelectAdjacentHistorical(+1); // newest-first list: +1 = older
            if (DrawCyberButton(nextR, "NEXT >", accent: UiCyan))
                _scanHistory.TrySelectAdjacentHistorical(-1); // newer
            y += navH + 6f;

            var retR = new Rect(x, y, inner, navH);
            Block(retR);
            if (DrawCyberButton(retR, "RETURN TO CURRENT TACTICAL",
                    selected: !_scanHistory.IsHistoricalMode, accent: UiGreen))
            {
                _scanHistory.ReturnToCurrentTactical();
                _playerTactical?.MarkDirty();
            }
            y += navH + 8f;

            // Compact DEV line inside this panel (avoids a second right-side DEV box)
            bool hist = _scanHistory.IsHistoricalMode;
            var view = _scanHistory.ViewScan;
            int scanId = view != null ? view.ScanId : -1;
            int anomN = hist
                ? (view != null ? view.SpatialSnapshots.Count : 0)
                : (view != null ? view.Anomalies.Count : 0);
            int findN = scanId >= 0 ? _scanHistory.Findings.CountForScan(scanId) : 0;
            GUI.Label(new Rect(x, y, inner, 12f),
                (hist ? "HISTORICAL · FROZEN" : "CURRENT · LIVE") +
                $"  ·  #{(scanId >= 0 ? scanId.ToString("00") : "—")}" +
                $"  ·  ANOM {anomN}  ·  FIND {findN}",
                LabelStyle(8, hist ? UiAmber : UiGreen));
            y += 16f;

            _scanHistory.CopyCompletedScansNewestFirst(_scanHistoryListScratch);
            if (_scanHistoryListScratch.Count == 0)
            {
                GUI.Label(new Rect(x, y, inner, 40f), "No completed scans yet.",
                    LabelStyle(9, UiDim));
                return;
            }

            float listTop = y;
            float listH = panel.yMax - listTop - 12f;
            var listOuter = new Rect(x, listTop, inner, listH);
            Block(listOuter);

            float rowH = 72f;
            float contentH = _scanHistoryListScratch.Count * rowH + 8f;
            float viewW = inner - 16f;
            _scanHistoryScroll = GUI.BeginScrollView(
                listOuter, _scanHistoryScroll, new Rect(0f, 0f, viewW, contentH));

            for (int i = 0; i < _scanHistoryListScratch.Count; i++)
            {
                var rec = _scanHistoryListScratch[i];
                var row = new Rect(0f, i * rowH, viewW, rowH - 4f);
                bool sel = _scanHistory.IsHistoricalMode && _scanHistory.SelectedScanId == rec.ScanId;
                DrawCyberPanel(row, lit: sel, accentOverride: sel ? UiAmber : UiCyan);

                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                {
                    _scanHistory.EnterHistoricalScan(rec.ScanId);
                    _playerTactical?.MarkDirty();
                }

                float lx = row.x + 8f;
                float ty = row.y + 6f;
                float tw = row.width - 16f;
                GUI.Label(new Rect(lx, ty, tw, 14f),
                    $"SCAN #{rec.ScanId:00}",
                    LabelStyle(10, sel ? UiAmber : UiCyan, bold: true));
                ty += 14f;
                GUI.Label(new Rect(lx, ty, tw, 12f),
                    ProspectorScanFormulas.FormatDayClockLabel(rec.CompletionGameHours),
                    LabelStyle(8, UiWhite));
                ty += 13f;
                string facing = ProspectorScanFormulas.FormatFacingLabel(rec.ScannerFacing);
                GUI.Label(new Rect(lx, ty, tw, 12f),
                    $"LOC ({rec.ScannerPosition.x:0.0},{rec.ScannerPosition.y:0.0})  ·  {facing}  ·  " +
                    $"{rec.ProspectorName} (id {rec.WorkerId})",
                    LabelStyle(8, UiDim));
                ty += 13f;
                GUI.Label(new Rect(lx, ty, tw, 12f),
                    $"RNG {rec.PlannedRangeCells:0.#}  ·  ±{rec.PlannedHalfAngleDeg:0.#}°  ·  " +
                    $"{rec.ProspectorProfileLabel}  ·  ANOM {rec.SpatialSnapshots.Count}",
                    LabelStyle(8, UiMute));
            }

            GUI.EndScrollView();
        }

        void DrawHistoricalTacticalBanner()
        {
            if (_playerTactical == null || !_playerTactical.Visible) return;
            if (_scanHistory == null || !_scanHistory.IsHistoricalMode) return;
            var scan = _scanHistory.ViewScan;
            if (scan == null) return;

            const float bw = 360f;
            const float bh = 78f;
            var r = new Rect((Screen.width - bw) * 0.5f, 10f, bw, bh);
            DrawCyberPanel(r, lit: true, accentOverride: UiAmber);
            Block(r);

            float lx = r.x + 14f;
            float ty = r.y + 8f;
            float tw = bw - 28f;
            GUI.Label(new Rect(lx, ty, tw, 14f), "SCAN HISTORY",
                LabelStyle(10, UiAmber, bold: true));
            ty += 16f;
            GUI.Label(new Rect(lx, ty, tw, 14f), $"SCAN #{scan.ScanId:00}",
                LabelStyle(12, UiWhite, bold: true));
            ty += 16f;
            GUI.Label(new Rect(lx, ty, tw, 12f),
                ProspectorScanFormulas.FormatDayClockLabel(scan.CompletionGameHours),
                LabelStyle(9, UiCyan));
            ty += 14f;
            GUI.Label(new Rect(lx, ty, tw, 12f), "HISTORICAL DATA — NOT LIVE INTEL",
                LabelStyle(8, UiAmber, bold: true));
        }

        void DrawScanHistoryDevReadout()
        {
            if (!DevMode.Enabled) return;
            // Compact DEV line lives inside the History browser when open.
            // When historical without browser, show a thin strip under the top-right toggles.
            if (_playerTactical == null || !_playerTactical.Visible) return;
            if (_scanHistory == null) return;
            if (_scanHistoryBrowserOpen) return;
            if (!_scanHistory.IsHistoricalMode) return;

            const float pw = 220f;
            const float ph = 72f;
            float bx = Screen.width - pw - 12f;
            float by = 10f + 28f + 6f + 28f + 6f + 28f + 10f;
            var r = new Rect(bx, by, pw, ph);
            DrawCyberPanel(r, lit: false, accentOverride: UiAmber);
            Block(r);

            float x = r.x + 10f;
            float y = r.y + 6f;
            float inner = pw - 20f;
            var view = _scanHistory.ViewScan;
            int scanId = view != null ? view.ScanId : -1;
            int anomN = view != null ? view.SpatialSnapshots.Count : 0;
            int findN = scanId >= 0 ? _scanHistory.Findings.CountForScan(scanId) : 0;

            GUI.Label(new Rect(x, y, inner, 12f), "DEV // HISTORICAL",
                LabelStyle(8, UiMute, bold: true));
            y += 14f;
            GUI.Label(new Rect(x, y, inner, 12f), "DATA: FROZEN SNAPSHOT",
                LabelStyle(9, UiAmber, bold: true));
            y += 14f;
            GUI.Label(new Rect(x, y, inner, 12f),
                $"Scan #{(scanId >= 0 ? scanId.ToString("00") : "—")}  ·  ANOM {anomN}  ·  FIND {findN}",
                LabelStyle(8, UiWhite));
            y += 14f;
            if (view != null)
            {
                GUI.Label(new Rect(x, y, inner, 12f),
                    $"{view.ProspectorProfileLabel} · {view.ProspectorId}",
                    LabelStyle(8, UiDim));
            }
        }

        void DrawAnomalyHoverTooltip()
        {
            if (_playerTactical == null || !_playerTactical.Visible || _world == null) return;
            if (_scannerPlaceMode || _scannerPlanMode) return;

            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse == null || cam == null) return;

            Vector2 screen = mouse.position.ReadValue();
            Vector2 guiPt = new(screen.x, Screen.height - screen.y);
            if (IsOverHud(guiPt)) return;

            Vector3 world3 = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));

            if (_scanHistory != null && _scanHistory.IsHistoricalMode)
            {
                DrawHistoricalAnomalyTimeline(world3, guiPt);
                return;
            }

            if (!_playerTactical.TryPickAnomaly(world3, out var a) || a == null) return;

            bool assessed = a.AnalysisStatus == AnomalyAnalysisStatus.Assessed && a.Assessment != null;
            bool focused = _prospector != null
                && _prospector.WorkMode == ProspectorWorkMode.Investigate
                && _prospector.Investigation.FocusAnomalyId == a.AnomalyId;
            bool showNew = a.HasUnreadFinding;

            if (showNew)
            {
                a.MarkFindingSeen();
                _playerTactical?.MarkDirty();
            }

            _anomalyTimelineScratch.Clear();
            _scanHistory?.Findings?.CollectForAnomaly(a.ScanId, a.AnomalyId, _anomalyTimelineScratch);
            int eventShow = Mathf.Min(8, _anomalyTimelineScratch.Count);
            int historyShow = eventShow > 0 ? 0 : Mathf.Min(5, a.FindingHistory.Count);

            float th = 78f;
            if (assessed) th += 52f;
            else if (a.AnalysisStatus == AnomalyAnalysisStatus.Analysing
                     || a.CurrentNeed != AnomalyInvestigationNeed.None) th += 36f;
            if (!string.IsNullOrEmpty(a.LatestFindingSummary) && assessed) th += 16f;
            if (eventShow > 0) th += 18f + eventShow * 28f;
            else
            {
                th += historyShow * 14f;
                if (historyShow > 0) th += 18f;
            }
            const float tw = 288f;
            float px = Mathf.Clamp(guiPt.x + 14f, 8f, Screen.width - tw - 8f);
            float py = Mathf.Clamp(guiPt.y + 14f, 8f, Screen.height - th - 8f);
            // Don't cover the left crew / scanner column
            if (px < _leftHudRight + 8f && py < _leftHudBottom)
                px = _leftHudRight + 10f;
            if (px + tw > Screen.width - HudToolStripReserve - 8f)
                px = Mathf.Max(8f, Screen.width - HudToolStripReserve - tw - 8f);
            var r = new Rect(px, py, tw, th);
            DrawCyberPanel(r, lit: true);

            float lx = r.x + 10f;
            float ty = r.y + 8f;
            float textW = tw - 20f;

            GUI.Label(new Rect(lx, ty, textW, 14), $"ANOMALY #{a.AnomalyId:00}",
                LabelStyle(10, UiCyan, bold: true));
            ty += 18f;

            if (assessed)
            {
                GUI.Label(new Rect(lx, ty, textW, 14),
                    showNew ? "NEW FINDING" : "FINDING",
                    LabelStyle(9, showNew ? UiAmber : UiCyan, bold: true));
                ty += 15f;
                string discovered = !string.IsNullOrEmpty(a.LatestFindingSummary)
                    ? a.LatestFindingSummary
                    : a.QualitativeAssessment;
                GUI.Label(new Rect(lx, ty, textW, 14), discovered,
                    LabelStyle(9, UiWhite));
                ty += 15f;
                GUI.Label(new Rect(lx, ty, textW, 14),
                    $"Assessment: {a.QualitativeAssessment}",
                    LabelStyle(9, UiGreen));
                ty += 18f;
            }
            else
            {
                string work;
                string reason;
                if (focused)
                {
                    work = _prospector.Investigation.PlayerWorkLabel;
                    reason = _prospector.Investigation.PlayerQuestionLabel;
                }
                else if (a.CurrentNeed != AnomalyInvestigationNeed.None)
                {
                    work = ProspectorAnomaly.NeedWorkLabel(a.CurrentNeed);
                    reason = a.NeedDetail;
                }
                else
                {
                    work = "Raw scan data";
                    reason = "Awaiting investigation";
                }

                GUI.Label(new Rect(lx, ty, textW, 14), $"CURRENT WORK: {work}",
                    LabelStyle(9, UiAmber, bold: true));
                ty += 15f;
                if (!string.IsNullOrEmpty(reason))
                {
                    GUI.Label(new Rect(lx, ty, textW, 28), $"\"{reason}\"",
                        LabelStyle(9, UiDim));
                    ty += 18f;
                }
            }

            if (eventShow > 0)
                DrawFindingTimelineEvents(lx, ty, textW, eventShow);
            else if (historyShow > 0)
            {
                GUI.Label(new Rect(lx, ty, textW, 14), "FINDING HISTORY",
                    LabelStyle(8, UiCyan, bold: true));
                ty += 14f;
                int start = a.FindingHistory.Count - historyShow;
                for (int i = start; i < a.FindingHistory.Count; i++)
                {
                    GUI.Label(new Rect(lx, ty, textW, 14), $"· {a.FindingHistory[i]}",
                        LabelStyle(8, UiWhite));
                    ty += 14f;
                }
            }
        }

        void DrawHistoricalAnomalyTimeline(Vector3 world3, Vector2 guiPt)
        {
            if (!_playerTactical.TryPickHistorical(world3, out var snap) || snap == null) return;
            if (_scanHistory?.Findings == null) return;

            _scanHistory.Findings.CollectForAnomaly(snap.ScanId, snap.AnomalyId, _anomalyTimelineScratch);
            int eventShow = Mathf.Min(10, _anomalyTimelineScratch.Count);
            float th = 48f + (eventShow > 0 ? 18f + eventShow * 28f : 24f);
            const float tw = 300f;
            float px = Mathf.Clamp(guiPt.x + 14f, 8f, Screen.width - tw - 8f);
            float py = Mathf.Clamp(guiPt.y + 14f, 8f, Screen.height - th - 8f);
            var r = new Rect(px, py, tw, th);
            DrawCyberPanel(r, lit: true, accentOverride: UiAmber);

            float lx = r.x + 10f;
            float ty = r.y + 8f;
            float textW = tw - 20f;

            GUI.Label(new Rect(lx, ty, textW, 14), $"ANOMALY #{snap.AnomalyId:00}",
                LabelStyle(10, UiAmber, bold: true));
            ty += 16f;
            GUI.Label(new Rect(lx, ty, textW, 12), "HISTORICAL TIMELINE",
                LabelStyle(8, UiCyan, bold: true));
            ty += 16f;

            if (eventShow == 0)
            {
                GUI.Label(new Rect(lx, ty, textW, 14), "No investigation events recorded.",
                    LabelStyle(8, UiDim));
                return;
            }

            DrawFindingTimelineEvents(lx, ty, textW, eventShow);
        }

        float DrawFindingTimelineEvents(float lx, float ty, float textW, int eventShow)
        {
            GUI.Label(new Rect(lx, ty, textW, 14), "FINDING HISTORY",
                LabelStyle(8, UiCyan, bold: true));
            ty += 14f;
            int start = Mathf.Max(0, _anomalyTimelineScratch.Count - eventShow);
            for (int i = start; i < _anomalyTimelineScratch.Count; i++)
            {
                var f = _anomalyTimelineScratch[i];
                string clock = ProspectorScanFormulas.FormatDayClockLabel(f.GameTimestampHours);
                string src = !string.IsNullOrEmpty(f.SourceLabel) ? f.SourceLabel : f.EventType.ToString();
                GUI.Label(new Rect(lx, ty, textW, 12), $"{clock}  ·  {src}",
                    LabelStyle(8, UiAmber, bold: true));
                ty += 12f;
                string body = !string.IsNullOrEmpty(f.AssessmentTitle) &&
                              (f.EventType == ProspectorFindingEventType.AnalysisCompleted
                               || f.EventType == ProspectorFindingEventType.AssessmentUpdated)
                    ? f.AssessmentTitle
                    : f.Message;
                if (body.Length > 64) body = body.Substring(0, 61) + "…";
                GUI.Label(new Rect(lx, ty, textW, 12), body, LabelStyle(8, UiWhite));
                ty += 14f;
                if (f.HasConfidence)
                {
                    GUI.Label(new Rect(lx, ty, textW, 11),
                        $"Confidence: {AnomalyAssessment.ConfidenceLabel(f.Confidence)}",
                        LabelStyle(7, UiDim));
                    ty += 12f;
                }
            }
            return ty;
        }


        readonly List<RawScanObservation> _spatialDebugBuf = new(128);

        bool TryAnomalySpatialDebug(
            ProspectorAnomaly a,
            out Vector2 truthCenter,
            out Vector2 believedCenter,
            out Vector2 meanOffset,
            out float meanBoundaryError,
            out float meanAbsOffset,
            out int sampleCount)
        {
            truthCenter = default;
            believedCenter = default;
            meanOffset = default;
            meanBoundaryError = 0f;
            meanAbsOffset = 0f;
            sampleCount = 0;
            var scan = _scanHistory?.DisplayScan;
            if (scan == null || a == null) return false;

            _spatialDebugBuf.Clear();
            for (int i = 0; i < scan.Observations.Count; i++)
            {
                var o = scan.Observations[i];
                if (o.ScanId != a.ScanId) continue;
                // Match believed evidence tiles
                bool hit = false;
                for (int t = 0; t < a.EvidenceTiles.Count; t++)
                {
                    if (a.EvidenceTiles[t].x == o.CellX && a.EvidenceTiles[t].y == o.CellY)
                    {
                        hit = true;
                        break;
                    }
                }
                if (hit) _spatialDebugBuf.Add(o);
            }

            ProspectorSpatialUncertainty.ComputeBeliefErrorMetrics(
                _spatialDebugBuf,
                out truthCenter, out believedCenter, out meanOffset,
                out meanBoundaryError, out meanAbsOffset, out sampleCount);
            return sampleCount > 0;
        }

        void DrawBanterBubble(float x, float y, WorkerRuntime wr)
        {
            if (wr == null) return;
            var speech = _banter.GetForWorker(wr.WorkerId);
            if (speech == null || string.IsNullOrEmpty(speech.Text)) return;

            Color accent = AccentForWorkerId(wr.WorkerId);
            const float bw = 248f;
            const float bh = 52f;
            var r = new Rect(x, y, bw, bh);

            // Keep speech clear of the right tool strip + open DEV popup,
            // and never snap back over the left crew column.
            float rightLimit = Screen.width - HudToolStripReserve - 16f;
            if (_hudPopup == HudPopupKind.Social)
                rightLimit -= 348f;
            else if (_hudPopup != HudPopupKind.None)
                rightLimit -= 300f;
            float leftLimit = _leftHudRight + 10f;
            if (r.x < leftLimit)
                r.x = leftLimit;
            if (r.xMax > rightLimit)
                r.x = Mathf.Max(leftLimit, rightLimit - r.width);

            DrawSocialSpeechBubble(
                r,
                speech,
                accent,
                nameSize: 9,
                bodySize: 11,
                life: 1f);
        }

        /// <summary>
        /// Job banter + social lines anchored above the speaking worker in world space.
        /// Social lines get comic valence chrome from resolved outcomes.
        /// </summary>
        void DrawWorldSpeechBubbles()
        {
            if (_crewWorkers == null || _presence == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null) continue;
                var speech = _banter.GetForWorker(wr.WorkerId);
                if (speech == null || string.IsNullOrEmpty(speech.Text))
                    continue;

                var av = _presence.Get(wr.WorkerId);
                if (av == null) continue;

                Vector3 world = av.transform.position + new Vector3(0f, 0.42f, 0f);
                Vector3 sp = cam.WorldToScreenPoint(world);
                if (sp.z < 0.05f) continue;

                float guiX = sp.x;
                float guiY = Screen.height - sp.y;
                Color identityAccent = AccentForWorkerId(wr.WorkerId);

                bool social = speech.IsSocial && speech.Valence != SocialSpeechValence.None;
                float bw = social && speech.Valence == SocialSpeechValence.Severe ? 210f : 196f;
                float bh = speech.Text.Length > 42 ? 52f : (social ? 46f : 40f);
                if (social) bh += 4f;
                float naturalX = guiX - bw * 0.5f;
                float naturalY = guiY - bh - 4f;
                float rightLimit = Screen.width - bw - HudToolStripReserve - 8f;
                var r = new Rect(
                    Mathf.Clamp(naturalX, 8f, Mathf.Max(8f, rightLimit)),
                    Mathf.Clamp(naturalY, 48f, Mathf.Max(48f, Screen.height - bh - 8f)),
                    bw, bh);

                float life = Mathf.Clamp01((speech.Until - Time.unscaledTime) / 4f);
                DrawSocialSpeechBubble(r, speech, identityAccent, nameSize: 8, bodySize: 10, life: life);
            }

            DrawSocialEscalationFx();
        }

        void TriggerSocialEscalationPulse(int workerId, SocialSpeechValence valence)
        {
            var cam = Camera.main;
            var av = _presence != null ? _presence.Get(workerId) : null;
            if (cam != null && av != null)
            {
                Vector3 sp = cam.WorldToScreenPoint(av.transform.position);
                if (sp.z > 0.05f)
                    _socialEscalationGui = new Vector2(sp.x, Screen.height - sp.y);
                else
                    _socialEscalationGui = new Vector2(Screen.width * 0.5f, Screen.height * 0.45f);
            }
            else
                _socialEscalationGui = new Vector2(Screen.width * 0.5f, Screen.height * 0.45f);

            _socialEscalationValence = valence;
            float dur = valence == SocialSpeechValence.Severe ? 1.15f : 0.7f;
            _socialEscalationUntil = Time.unscaledTime + dur;
        }

        void DrawSocialEscalationFx()
        {
            float remain = _socialEscalationUntil - Time.unscaledTime;
            if (remain <= 0f) return;

            float total = _socialEscalationValence == SocialSpeechValence.Severe ? 1.15f : 0.7f;
            float u = 1f - Mathf.Clamp01(remain / total);
            Color c = SocialSpeechVisuals.Accent(_socialEscalationValence);
            float a = (1f - u) * (_socialEscalationValence == SocialSpeechValence.Severe ? 0.55f : 0.35f);
            float radius = 18f + u * (_socialEscalationValence == SocialSpeechValence.Severe ? 72f : 48f);

            // Expanding industrial ring — hairline brackets, not soft bloom
            var prev = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, a);
            float cx = _socialEscalationGui.x;
            float cy = _socialEscalationGui.y;
            float t = _socialEscalationValence == SocialSpeechValence.Severe ? 2.5f : 1.5f;
            // Four corner brackets of a diamond / octagon approximation
            GUI.DrawTexture(new Rect(cx - radius, cy - t * 0.5f, radius * 2f, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - t * 0.5f, cy - radius, t, radius * 2f), Texture2D.whiteTexture);
            float inset = radius * 0.7f;
            GUI.color = new Color(c.r, c.g, c.b, a * 0.7f);
            GUI.DrawTexture(new Rect(cx - inset, cy - inset, 10f, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - inset, cy - inset, t, 10f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + inset - 10f, cy - inset, 10f, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + inset - t, cy - inset, t, 10f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - inset, cy + inset - t, 10f, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - inset, cy + inset - 10f, t, 10f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + inset - 10f, cy + inset - t, 10f, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + inset - t, cy + inset - 10f, t, 10f), Texture2D.whiteTexture);

            if (_socialEscalationValence == SocialSpeechValence.Severe && u < 0.35f)
            {
                GUI.color = new Color(c.r, c.g, c.b, (0.35f - u) * 1.2f);
                GUI.Label(new Rect(cx - 40f, cy - radius - 16f, 80f, 14f),
                    "CONFLICT", LabelStyle(9, c, bold: true));
            }
            GUI.color = prev;
        }

        /// <summary>
        /// Social comic bubble (valence chrome) or plain job caption.
        /// </summary>
        void DrawSocialSpeechBubble(
            Rect r,
            WorkerBanter.Speech speech,
            Color identityAccent,
            int nameSize,
            int bodySize,
            float life)
        {
            if (speech == null) return;
            bool social = speech.IsSocial && speech.Valence != SocialSpeechValence.None;
            if (!social)
            {
                var nameCol = new Color(identityAccent.r, identityAccent.g, identityAccent.b, 0.55f + 0.4f * life);
                var bodyCol = new Color(UiWhite.r, UiWhite.g, UiWhite.b, 0.72f + 0.22f * life);
                DrawSpeechCaption(r, speech.DisplayName.ToUpperInvariant(), speech.Text,
                    nameCol, bodyCol, nameSize, bodySize);
                return;
            }

            var valence = speech.Valence;
            Color accent = SocialSpeechVisuals.Accent(valence);
            Color fill = SocialSpeechVisuals.FillTint(valence);
            fill.a *= 0.55f + 0.45f * life;
            accent = new Color(accent.r, accent.g, accent.b, 0.5f + 0.5f * life);

            var prev = GUI.color;
            GUI.color = fill;
            GUI.DrawTexture(r, Texture2D.whiteTexture);

            float borderT = valence == SocialSpeechValence.Severe ? 2.5f : 1.5f;
            // Pulse severe border
            if (valence == SocialSpeechValence.Severe)
                borderT += 0.6f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 10f));
            GUI.color = accent;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, borderT), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - borderT, r.width, borderT), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, borderT, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - borderT, r.y, borderT, r.height), Texture2D.whiteTexture);

            // Corner brackets — sharper for severe
            float br = valence == SocialSpeechValence.Severe ? 11f : 8f;
            float bt = valence == SocialSpeechValence.Severe ? 2f : 1.5f;
            GUI.DrawTexture(new Rect(r.x, r.y, br, bt), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, bt, br), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - br, r.y, br, bt), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - bt, r.y, bt, br), Texture2D.whiteTexture);
            if (valence == SocialSpeechValence.Severe)
            {
                // Agitated bottom ticks
                GUI.DrawTexture(new Rect(r.x + 6f, r.yMax - bt, 8f, bt), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.x + 18f, r.yMax - bt - 2f, 6f, bt), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.xMax - 20f, r.yMax - bt, 10f, bt), Texture2D.whiteTexture);
            }
            GUI.color = prev;

            // Glyph badge
            string glyph = SocialSpeechVisuals.Glyph(valence);
            var glyphR = new Rect(r.x + 5f, r.y + 3f, 14f, 14f);
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.25f + 0.35f * life);
            GUI.DrawTexture(glyphR, Texture2D.whiteTexture);
            GUI.color = prev;
            var gStyle = LabelStyle(11, accent, bold: true);
            gStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(glyphR, glyph, gStyle);

            float textX = r.x + 22f;
            float textW = r.width - 28f;
            var nameCol2 = new Color(accent.r, accent.g, accent.b, 0.7f + 0.3f * life);
            var bodyCol2 = new Color(UiWhite.r, UiWhite.g, UiWhite.b, 0.78f + 0.2f * life);
            // Soft identity tint on name secondary
            var nameR = new Rect(textX, r.y + 3f, textW, 13f);
            var bodyR = new Rect(textX, r.y + 16f, textW, r.height - 20f);
            Color shadow = new(0f, 0f, 0f, 0.55f);
            GUI.Label(new Rect(nameR.x + 1f, nameR.y + 1f, nameR.width, nameR.height),
                speech.DisplayName.ToUpperInvariant(), LabelStyle(nameSize, shadow, bold: true));
            GUI.Label(nameR, speech.DisplayName.ToUpperInvariant(), LabelStyle(nameSize, nameCol2, bold: true));
            var bodyShadow = LabelStyle(bodySize, shadow);
            bodyShadow.wordWrap = true;
            var bodyMain = LabelStyle(bodySize, bodyCol2);
            bodyMain.wordWrap = true;
            GUI.Label(new Rect(bodyR.x + 1f, bodyR.y + 1f, bodyR.width, bodyR.height),
                speech.Text, bodyShadow);
            GUI.Label(bodyR, speech.Text, bodyMain);
        }

        /// <summary>Floating dialogue — text only, soft shadow, no panel chrome.</summary>
        void DrawSpeechCaption(Rect r, string name, string text, Color accent,
            int nameSize, int bodySize)
        {
            var nameCol = new Color(accent.r, accent.g, accent.b, 0.88f);
            var bodyCol = new Color(UiWhite.r, UiWhite.g, UiWhite.b, 0.9f);
            DrawSpeechCaption(r, name, text, nameCol, bodyCol, nameSize, bodySize);
        }

        void DrawSpeechCaption(Rect r, string name, string text, Color nameCol, Color bodyCol,
            int nameSize, int bodySize)
        {
            Color shadow = new(0f, 0f, 0f, 0.55f);
            var nameR = new Rect(r.x, r.y, r.width, 13f);
            var bodyR = new Rect(r.x, r.y + 14f, r.width, r.height - 14f);

            GUI.Label(new Rect(nameR.x + 1f, nameR.y + 1f, nameR.width, nameR.height),
                name, LabelStyle(nameSize, shadow, bold: true));
            GUI.Label(nameR, name, LabelStyle(nameSize, nameCol, bold: true));

            var bodyShadow = LabelStyle(bodySize, shadow);
            bodyShadow.wordWrap = true;
            var bodyMain = LabelStyle(bodySize, bodyCol);
            bodyMain.wordWrap = true;
            GUI.Label(new Rect(bodyR.x + 1f, bodyR.y + 1f, bodyR.width, bodyR.height),
                text, bodyShadow);
            GUI.Label(bodyR, text, bodyMain);
        }

        bool DrawCyberButton(Rect r, string label, bool selected = false, Color accent = default)
        {
            if (accent.a <= 0.001f) accent = UiCyan;
            Vector2 mouse = Event.current != null
                ? Event.current.mousePosition
                : Vector2.zero;
            bool hover = r.Contains(mouse);

            var prev = GUI.color;
            GUI.color = selected ? UiBgHot : (hover ? new Color(0.05f, 0.1f, 0.14f, 0.4f) : CpBtnIdle);
            GUI.DrawTexture(r, Texture2D.whiteTexture);

            float borderA = selected ? (0.55f + 0.25f * _uiPulse) : (hover ? 0.55f : 0.28f);
            Color border = new(accent.r, accent.g, accent.b, borderA);
            DrawBorder(r, border, selected || hover ? 2f : 1f);
            // Chamfer ticks
            GUI.color = new Color(accent.r, accent.g, accent.b, selected ? 0.85f : 0.4f);
            GUI.DrawTexture(new Rect(r.x, r.y, 12f, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, 1f, 8f), Texture2D.whiteTexture);
            if (selected)
            {
                GUI.color = new Color(accent.r, accent.g, accent.b, 0.7f);
                GUI.DrawTexture(new Rect(r.x + 4, r.yMax - 3f, r.width - 8, 1.5f), Texture2D.whiteTexture);
            }
            GUI.color = prev;

            var st = LabelStyle(11, selected ? accent : (hover ? UiWhite : UiDim), bold: selected);
            st.alignment = TextAnchor.MiddleCenter;
            GUI.Label(r, label, st);

            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        void Block(Rect r) => _hudBlockers.Add(r);

        void DrawCyberPanel(Rect r, bool lit = false, Color accentOverride = default)
        {
            Color accent = accentOverride.a > 0.001f ? accentOverride : UiCyan;
            var prev = GUI.color;
            GUI.color = lit ? UiBgHot : UiBg;
            GUI.DrawTexture(r, Texture2D.whiteTexture);

            // Very subtle scan lines
            GUI.color = new Color(1f, 1f, 1f, 0.015f);
            for (float y = r.y + 2; y < r.yMax; y += 3f)
                GUI.DrawTexture(new Rect(r.x + 2, y, r.width - 4, 1f), Texture2D.whiteTexture);

            float edgeA = lit ? 0.4f + 0.15f * _uiPulse : 0.28f;
            DrawBorder(r, new Color(accent.r, accent.g, accent.b, edgeA), 1f);

            // Corner brackets
            GUI.color = new Color(accent.r, accent.g, accent.b, lit ? 0.85f : 0.5f);
            const float L = 14f;
            GUI.DrawTexture(new Rect(r.x, r.y, L, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, 1f, L), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - L, r.y, L, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 1f, r.y, 1f, L), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1f, L, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - L, 1f, L), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - L, r.yMax - 1f, L, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 1f, r.yMax - L, 1f, L), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        static void DrawBorder(Rect r, Color c, float thick)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - thick, r.width, thick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, thick, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - thick, r.y, thick, r.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        static void DrawHLine(float x, float y, float w, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, 1f), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        static void DrawVLine(float x, float y, float h, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, 1f, h), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        static Sprite MakeGoalSprite()
        {
            // Soft neon waypoint dot (not a bulky arrow)
            const int s = 16;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            float cx = (s - 1) * 0.5f, cy = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - cx) / 3.2f;
                float dy = (y - cy) / 3.2f;
                float d = dx * dx + dy * dy;
                if (d > 1f)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                // Soft outer halo, brighter core
                float core = Mathf.Clamp01(1f - d * 2.4f);
                float g = 0.55f + 0.45f * core;
                tex.SetPixel(x, y, new Color(0.35f * g, 1f * g, 0.55f * g, a * 0.85f));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        // ——— V1.1 lock audit hooks (batchmode / ForceBuild) ———

        public void ForceBuildForAudit()
        {
            if (_crewWorkers != null)
            {
                if (_crewPhase != CrewPhase.OnShift)
                    EnterOnShift();
                SoftArriveAllWork(); // after EnterOnShift — day ledger reset must not wipe ArrivedWork
                return;
            }
            Build();
            // Build leaves HeadingOut; EnterOnShift resets day ledgers, then seat operators
            if (_crewPhase != CrewPhase.OnShift)
                EnterOnShift();
            SoftArriveAllWork(); // audit-only seat — not normal gameplay
        }

        public int AuditCrewCount() => _crewWorkers != null ? _crewWorkers.Length : 0;

        public bool AuditUniqueWorkerIds()
        {
            if (_crewWorkers == null) return false;
            var seen = new HashSet<int>();
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var w = _crewWorkers[i];
                if (w == null || w.WorkerId <= 0) return false;
                if (!seen.Add(w.WorkerId)) return false;
            }
            return seen.Count == 5;
        }

        public bool AuditDistinctStatsRefs()
        {
            if (_crewWorkers == null) return false;
            for (int i = 0; i < _crewWorkers.Length; i++)
            for (int j = i + 1; j < _crewWorkers.Length; j++)
            {
                if (_crewWorkers[i] == null || _crewWorkers[j] == null) return false;
                if (ReferenceEquals(_crewWorkers[i].Stats, _crewWorkers[j].Stats)) return false;
            }
            return true;
        }

        public bool AuditOneAvatarPerWorker()
        {
            if (_crewWorkers == null) return false;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var w = _crewWorkers[i];
                if (w == null) return false;
                if (_presence.Get(w.WorkerId) == null) return false;
            }
            return true;
        }

        public bool AuditDefaultJobMap()
        {
            return AuditJobOf(1) == JobType.Prospecting
                && AuditJobOf(2) == JobType.Excavation
                && AuditJobOf(3) == JobType.Hauling
                && AuditJobOf(4) == JobType.Refining
                && AuditJobOf(5) == JobType.Engineering;
        }

        public bool AuditHasDuplicateJobs() =>
            _assignments != null && _assignments.HasDuplicateJobViolation(_crewWorkers);

        public int AuditSelectedWorkerId() => _selectedWorkerId;

        public void AuditSelectPerson(int workerId) => SelectPersonById(workerId);

        public bool AuditSelectedJobIs(JobType job) => SelectedJobIs(job);

        public bool AuditCanPerform(int workerId) =>
            CanPerformJobActions(FindCrewWorker(workerId));

        public string AuditControlHostLabel() => ResolveControlTarget().HostLabel ?? "";

        public string AuditStatsRef(int workerId)
        {
            var wr = FindCrewWorker(workerId);
            return wr != null ? wr.StatsRefLabel : "";
        }

        public JobType AuditJobOf(int workerId)
        {
            var a = _assignments.GetAssignment(workerId);
            return a != null ? a.JobType : JobType.Unassigned;
        }

        public bool AuditTryAssign(int workerId, JobType job, out string reason) =>
            TryAssignJob(FindCrewWorker(workerId), job, out reason);

        public bool AuditTryUnassign(JobType job, out string reason) =>
            TryUnassignJob(job, out reason);

        public int AuditWorkersOnJob(JobType job) => _assignments.CountWorkersOnJob(job);

        public bool AuditTryBanter(JobType job, string source) =>
            TryAssignedBanter(job, source, "V1.1 audit line.");

        public void AuditClearBanter()
        {
            _banter.Clear();
            _socialPresenter.ClearQueue();
        }

        public int AuditLastBanterWorkerId() =>
            _banter.LastSpeech != null ? _banter.LastSpeech.WorkerId : 0;

        public Vector2 AuditProviderPos(JobType job) => GetProviderOperatePoint(job);

        public int AuditFindingsAuthorId()
        {
            var wr = GetBodyAssignedWorker(JobType.Prospecting);
            return wr != null ? wr.WorkerId : 0;
        }

        public bool AuditAllJobsVacant() =>
            AuditWorkersOnJob(JobType.Prospecting) == 0
            && AuditWorkersOnJob(JobType.Excavation) == 0
            && AuditWorkersOnJob(JobType.Hauling) == 0
            && AuditWorkersOnJob(JobType.Refining) == 0
            && AuditWorkersOnJob(JobType.Engineering) == 0;

        public int AuditVisibleAvatarCount()
        {
            int n = 0;
            if (_crewWorkers == null) return 0;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var w = _crewWorkers[i];
                if (w == null) continue;
                var av = _presence.Get(w.WorkerId);
                if (av != null && !av.IsVisuallyHidden) n++;
            }
            return n;
        }

        public int AuditHiddenAssignedAvatarCount()
        {
            int n = 0;
            if (_crewWorkers == null) return 0;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var w = _crewWorkers[i];
                if (w == null) continue;
                var asg = _assignments.GetAssignment(w.WorkerId);
                if (asg == null || asg.JobType == JobType.Unassigned) continue;
                var av = _presence.Get(w.WorkerId);
                if (av != null && av.IsVisuallyHidden) n++;
            }
            return n;
        }

        public bool AuditAssignedAvatarsFollowing()
        {
            if (_crewWorkers == null) return false;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var w = _crewWorkers[i];
                if (w == null) continue;
                var asg = _assignments.GetAssignment(w.WorkerId);
                if (asg == null || asg.JobType == JobType.Unassigned) continue;
                var av = _presence.Get(w.WorkerId);
                if (av == null || string.IsNullOrEmpty(av.FollowingProviderId)) return false;
            }
            return true;
        }

        public Vector2[] AuditAllProviderPositions() => new[]
        {
            AuditProviderPos(JobType.Prospecting),
            AuditProviderPos(JobType.Excavation),
            AuditProviderPos(JobType.Hauling),
            AuditProviderPos(JobType.Refining),
            AuditProviderPos(JobType.Engineering),
        };

        public bool AuditProvidersUnmoved(Vector2[] before, float epsSqr)
        {
            if (before == null || before.Length < 5) return false;
            var now = AuditAllProviderPositions();
            for (int i = 0; i < 5; i++)
                if ((now[i] - before[i]).sqrMagnitude > epsSqr) return false;
            return true;
        }

        public void AuditBeginHeadingHome() => BeginHeadingHome();
        public void AuditEnterSleep() => EnterSleep();
        public void AuditSkipSleep() => SkipSleep();
        public void AuditEnterOnShift() => EnterOnShift();

        public string AuditCrewPhaseName() => _crewPhase.ToString();

        public bool AuditCanRelease(JobType job, out string reason) =>
            CanReleaseFromJob(job, out reason);

        public bool AuditPersonalConditionsStable(int workerId) => AuditWorkerStateStable(workerId);

        public bool AuditWorkerStateStable(int workerId)
        {
            var wr = FindCrewWorker(workerId);
            if (wr == null) return false;
            var bag = wr.State;
            var job = AuditJobOf(workerId);
            if (job != JobType.Unassigned)
            {
                TryUnassignJob(job, out _);
                bool same = ReferenceEquals(bag, wr.State);
                TryAssignJob(wr, job, out _);
                return same && ReferenceEquals(bag, wr.State);
            }
            return ReferenceEquals(bag, wr.State);
        }

        public WorkerState AuditWorkerState(int workerId) => FindCrewWorker(workerId)?.State;

        public WorkerRuntime AuditCrewWorker(int workerId) => FindCrewWorker(workerId);

        public WorkerStateEventRecord AuditEmitForced(WorkerStateEvent e) =>
            _stateEvents.EmitForced(e);

        public WorkerStateEventRecord AuditEmit(WorkerStateEvent e) =>
            _stateEvents.Emit(e);

        public void AuditClearEventGate() => _stateEvents.ClearGate();

        public int AuditEventHistoryCount(int workerId) =>
            FindCrewWorker(workerId)?.EventHistory.Items.Count ?? 0;

        public WorkerStateEventRecord AuditLastEvent(int workerId)
        {
            var hist = FindCrewWorker(workerId)?.EventHistory;
            if (hist == null || hist.Items.Count == 0) return null;
            return hist.Items[hist.Items.Count - 1];
        }

        public void AuditTickDaytime(float gameHours) 
        {
            if (_crewWorkers == null) return;
            for (int i = 0; i < _crewWorkers.Length; i++)
                WorkerStateDaytimeRecovery.Tick(_crewWorkers[i], gameHours);
        }

        public void AuditApplyCrewSleepRecovery(float nightFraction01) =>
            ApplyCrewSleepRecoveryFraction(nightFraction01);

        public float AuditExcavatorHeat() => _worker != null ? _worker.Heat : -1f;

        // ——— Social Aura Stage 1 audit hooks ———

        public SocialAuraLiveSystem AuditSocialAura => _socialAura;
        public SocialAuraPresenter AuditSocialPresenter => _socialPresenter;
        public WorkerBanter AuditBanter => _banter;

        public bool AuditTryPresentSocial(SocialEncounterLog log) =>
            TryPresentSocialEncounterForAudit(log);

        bool TryPresentSocialEncounterForAudit(SocialEncounterLog log)
        {
            if (log == null) return false;
            TryPresentSocialEncounter(log);
            return _socialPresenter.LastPresentation.Presented;
        }

        public int AuditTickSocialPresentation()
        {
            TickSocialPresentation();
            return _socialPresenter.QueuedCount;
        }

        public bool AuditTryAmbientBanter(int workerId, string line)
        {
            var wr = FindCrewWorker(workerId);
            if (wr == null) return false;
            return _banter.TrySay(
                workerId,
                wr.DisplayName,
                WorkerBanter.JobContext.Excavation,
                "Audit/Ambient",
                _absoluteGameHours,
                line);
        }

        public bool AuditTrySocialBanter(int workerId, string line)
        {
            var wr = FindCrewWorker(workerId);
            if (wr == null) return false;
            return TryAuthoredSocialBanter(
                workerId, wr.DisplayName, JobType.Excavation, "audit", line);
        }

        public Vector2 AuditAvatarPos(int workerId)
        {
            var av = _presence.Get(workerId);
            return av != null ? av.PresencePosition : Vector2.zero;
        }

        public Vector2 AuditProviderOperatePos(JobType job) => GetProviderOperatePoint(job);

        public bool AuditAvatarHidden(int workerId)
        {
            var av = _presence.Get(workerId);
            return av != null && av.IsVisuallyHidden;
        }

        public string AuditPhysicalStateName(int workerId)
        {
            var wr = FindCrewWorker(workerId);
            return wr == null ? "null" : GetPhysicalState(wr).ToString();
        }

        public bool AuditSocialEligible(int workerId)
        {
            var wr = FindCrewWorker(workerId);
            return wr != null && SocialAuraEligibility.IsEligible(wr, MapSocialPresence(wr));
        }

        public void AuditTickSocial(float gameHoursDelta, bool syncMovingAvatars = true)
        {
            // Position-forced smoke tests must skip sync — otherwise Operating avatars snap back to hosts.
            if (syncMovingAvatars)
                SyncMovingAssignedAvatars();
            if (_crewWorkers == null) return;
            if (!_socialAura.IsBootstrapped)
                _socialAura.Bootstrap(_crewWorkers);
            int encBefore = _socialAura.TotalEncounters;
            _socialAura.Tick(
                _crewWorkers,
                _presence,
                gameHoursDelta,
                MapSocialPresence,
                id =>
                {
                    var asg = _assignments.GetAssignment(id);
                    return asg != null ? asg.JobType : JobType.Unassigned;
                },
                isAsleep: _crewPhase == CrewPhase.Asleep,
                campCenter: CampNavDestination);
            if (_socialAura.TotalEncounters > encBefore && _socialAura.LastEncounter != null)
                TryPresentSocialEncounter(_socialAura.LastEncounter);
            TickSocialPresentation();
        }

        public void AuditForceCrewPhaseAsleep()
        {
            // Soft sleep entry for social eligibility tests without full commute
            _crewPhase = CrewPhase.Asleep;
        }

        public void AuditForceCrewPhaseOnShift()
        {
            if (_crewPhase != CrewPhase.OnShift)
                EnterOnShift();
        }

        public SocialDirectedRelation AuditRelation(int from, int to) =>
            _socialAura.Relation(from, to);

        public SocialPairTransient AuditPair(int a, int b) =>
            _socialAura.Pair(a, b);

        public void AuditSetAvatarPos(int workerId, Vector2 pos)
        {
            _presence.Get(workerId)?.SetPresencePosition(pos);
        }
    }
}
