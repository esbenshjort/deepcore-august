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
        /// <summary>Stage A prototype roster — identity + shared WorkerStats. Indexed by WorkerId order.</summary>
        WorkerRuntime[] _crewWorkers;
        WorkerRuntime _workerLewis;
        WorkerRuntime _workerMara;
        WorkerRuntime _workerKowalski;
        WorkerRuntime _workerElena;
        WorkerRuntime _workerViktor;
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
        readonly List<SocialAuraLiveSystem.NearbyDebug> _socialNearbyScratch = new(8);
        readonly List<SocialMemoryEntry> _socialMemoryScratch = new(16);
        readonly List<SocialMemoryEntry> _coopMemScratch = new(8);
        /// <summary>DEV-only rolling encounter+presentation stamps (playtest panel).</summary>
        readonly List<SocialDevEncounterStamp> _socialDevHistory = new(12);

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
        InfrastructureDebugOverlay _infraDebug;
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
        readonly ExcavatedPathfinder[] _personNav = new ExcavatedPathfinder[5];
        /// <summary>Person commute body radius (WorkerAvatar). Not host/machine footprint.</summary>
        const float AvatarCommuteRadius = 0.12f;
        /// <summary>
        /// LEGACY host footprints — excavator dig radius etc. Not used for person commute after F4.
        /// Index order historically matched ControlWorker, not WorkerRuntime roster.
        /// </summary>
        readonly float[] _providerFootprintR = { 0.22f, 0.12f, 0.14f, 0.12f, 0.12f };
        readonly WorkerBanter _banter = new();
        /// <summary>
        /// LEGACY enum stub — unused by selection / ST / banter after F5.
        /// Kept only for obsolete SelectWorker / DrawWorkerCard stubs.
        /// </summary>
        enum ControlWorker : byte { Prospector = 0, Excavator = 1, Hauler = 2, Refiner = 3, Engineer = 4 }
        ControlWorker _control = ControlWorker.Prospector;
        /// <summary>F2 canonical selected person (WorkerId). Stable across job reassignment.</summary>
        int _selectedWorkerId;

        // ——— Day / shift cycle (24h clock, shift 08:00–18:00) ———
        enum CrewPhase : byte { OnShift = 0, HeadingHome = 1, Asleep = 2, HeadingOut = 3 }
        /// <summary>F4: physical presence independent of assignment.</summary>
        enum WorkerPhysicalState : byte
        {
            Operating = 0,
            Idle = 1,
            CommutingHome = 2,
            Sleeping = 3,
            CommutingToWork = 4,
        }
        const float ShiftStartHour = 8f;
        const float ShiftEndHour = 18f;
        /// <summary>Real seconds per in-game hour (~shift ≈ 2 min, night ≈ 2.3 min).</summary>
        const float SecondsPerGameHour = 12f;
        const float TravelSpeed = 1.35f;
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
        /// <summary>Where the excavator machine was left at whistle — next shift avatar walks here.</summary>
        Vector2 _excavatorDigResume;
        bool _hasExcavatorDigResume;
        /// <summary>Person commute flags — indexed by crew roster order (Lewis…Viktor), not ControlWorker.</summary>
        bool[] _personArrived = { false, false, false, false, false };
        bool[] _personStranded = { false, false, false, false, false };
        /// <summary>Subtle per-person lateral path stagger (world units), roster order.</summary>
        readonly float[] _personLateral = { -0.035f, 0.04f, -0.02f, 0.03f, 0.015f };
        static readonly Vector2[] CampDoorOffsets =
        {
            new(0.10f, -0.15f),
            new(-0.20f, 0.05f),
            new(0.25f, 0.10f),
            new(-0.05f, -0.25f),
            new(0.30f, 0.15f),
        };
        float _commuteTimer;
        const float CommuteTimeoutSec = 10f;
        [SerializeField] bool navDebugDraw;
        [SerializeField] bool navDebugLog;

        // HUD hit-rects (GUI space, y-down) — block world dig/aim clicks
        readonly List<Rect> _hudBlockers = new(12);
        int _goldCells;
        int _goldSocketsTotal;
        int _goldFoundCells;
        int _goldFoundSockets;
        int _bedrockCells;
        int _lanternCount;

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
            viewGo.AddComponent<FreeMovementTerrainView>()
                .Setup(_world, fogOfWarGold: false, strongCliffEdges: true);
            // Rock wall ShadowCaster2D — used by crew headlamps only (lanterns keep shadows off).
            viewGo.AddComponent<RockWallShadows>().Setup(_world);
            GoldVeinShine.Attach(_worldRoot, _world);
            _tactical = TacticalMapOverlay.Attach(_worldRoot, _world);
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

            _logisticsTraffic = new LogisticsTrafficMap(_world);
            _mineInfra = new MineInfrastructure(_world, _logisticsTraffic, _trackRoot, _lanternRoot);
            _mineInfra.BindExcavator(_worker);
            _mineInfra.BindDropPoint(_yard.DropPoint);
            _mineInfra.RegisterLantern(_world.CellCenter(StartX, StartY - 4));
            _hauler.BindLogistics(_logisticsTraffic, _mineInfra);
            _engineer.BindInfrastructure(_mineInfra);
            _infraDebug = InfrastructureDebugOverlay.Attach(_worldRoot, _world, _mineInfra);
            _infraDebug.Visible = false;
            _prospector = ProspectorPerson.Spawn(_worldRoot, _world,
                _world.CellCenter(StartX - 6, StartY - 2), _scanView);
            _prospector.BindScanHistory(_scanHistory);

            BootstrapPrototypeCrew();

            RefreshProviderWorkPosts();

            // First whistle — avatars emerge from the tent; providers stay at spawn posts
            BeginHeadingOut(announce: false);
            _prospector.BindExcavator(_worker);
            _prospector.BindRefiner(_refiner);
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

            // Camp / dig mouth — wide soft apron into the mountain
            int floorY = StartY - 16;
            GoldVeinPlacer.ExcavateHalfOval(_world, StartX, floorY, radiusX: 56, radiusY: 20);
            GoldVeinPlacer.ExcavateHalfOval(_world, StartX, StartY - 2, radiusX: 18, radiusY: 10);
            GoldVeinPlacer.ExcavateHalfOval(_world, StartX, StartY - 12, radiusX: 36, radiusY: 16);

            // Soft highway + sparse landmarks (bedrock / gas / gold spaced for clean tunnelling)
            GoldVeinPlacer.BuildSocketMapStarterMaze(_world, StartX, StartY);
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

        void TickJobDemands(float gameHoursDelta)
        {
            if (_crewWorkers == null || gameHoursDelta <= 0f) return;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null) continue;

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
            _crewWorkers = new[]
            {
                _workerLewis,
                _workerMara,
                _workerKowalski,
                _workerElena,
                _workerViktor,
            };

            BindWorkerStateEventService();

            _prospector?.BindWorker(_workerLewis);
            _worker?.BindWorker(_workerMara);
            _hauler?.BindWorker(_workerKowalski);
            _refiner?.BindWorker(_workerElena);
            _engineer?.BindWorker(_workerViktor);

            // Verify: body.Stats == WorkerRuntime.Stats (same ref); workers do not share sheets
            Debug.Assert(_prospector == null || ReferenceEquals(_prospector.Stats, _workerLewis.Stats));
            Debug.Assert(_worker == null || ReferenceEquals(_worker.Stats, _workerMara.Stats));
            Debug.Assert(_hauler == null || ReferenceEquals(_hauler.Stats, _workerKowalski.Stats));
            Debug.Assert(_refiner == null || ReferenceEquals(_refiner.Stats, _workerElena.Stats));
            Debug.Assert(_engineer == null || ReferenceEquals(_engineer.Stats, _workerViktor.Stats));
            Debug.Assert(!ReferenceEquals(_workerLewis.Stats, _workerMara.Stats));
            Debug.Assert(!ReferenceEquals(_workerMara.Stats, _workerKowalski.Stats));
            Debug.Assert(!ReferenceEquals(_workerKowalski.Stats, _workerElena.Stats));
            Debug.Assert(!ReferenceEquals(_workerElena.Stats, _workerViktor.Stats));

            // Balance harness captured excavator sheet before BindWorker — refresh baseline from Mara
            if (_worker != null)
                _balance.Bind(_worker);

            for (int i = 1; i < _sheetBaselineByWorkerId.Length; i++)
            {
                _sheetBaselineCaptured[i] = false;
                _sheetProfileByWorkerId[i] = WorkerSheetProfile.Baseline;
            }

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
            _socialPlaytest.Reset(_dayIndex, _socialAura);
            _coopWorkPlaytest.Reset(_dayIndex);
            BindExcavatorEngineerCooperation();
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
                _presence.Register(av);
            }
        }

        Vector2 GetDefaultAvatarSeedPosition(WorkerRuntime wr)
        {
            if (wr == null) return Vector2.zero;
            // Match Stage B default mapping for seed only
            return wr.WorkerId switch
            {
                1 => _prospector != null ? _prospector.Position + new Vector2(-0.35f, 0.2f) : Vector2.zero,
                2 => _worker != null ? _worker.Position + new Vector2(0.35f, 0.15f) : Vector2.zero,
                3 => _hauler != null ? _hauler.Position + new Vector2(-0.3f, -0.25f) : Vector2.zero,
                4 => _refiner != null ? _refiner.Position + new Vector2(0.3f, -0.2f) : Vector2.zero,
                5 => _engineer != null ? _engineer.Position + new Vector2(0.25f, 0.3f) : Vector2.zero,
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
                default:
                    return Vector2.zero;
            }
        }

        /// <summary>Exit snap near host when leaving (V1 — no travel automation).</summary>
        Vector2 GetProviderExitPoint(JobType job)
        {
            Vector2 op = GetProviderOperatePoint(job);
            return op + job switch
            {
                JobType.Excavation => new Vector2(0.32f, 0.1f),
                JobType.Hauling => new Vector2(-0.28f, 0.12f),
                JobType.Prospecting => new Vector2(0.22f, -0.18f),
                JobType.Refining => new Vector2(-0.35f, -0.15f),
                JobType.Engineering => new Vector2(0.3f, -0.2f),
                _ => Vector2.zero,
            };
        }

        void ParkAvatarLeavingJob(WorkerRuntime wr, JobType job)
        {
            if (wr == null || job == JobType.Unassigned) return;
            var av = _presence.Get(wr);
            if (av == null) return;
            av.ParkAt(GetProviderExitPoint(job));
            av.ClearFollowing();
            av.Show();
            av.SetDevForceShowHidden(_devForceShowHiddenAvatars);
        }

        /// <summary>
        /// Sync every avatar Transform to assignment while OnShift:
        /// assigned → operate point + hide; unassigned → visible.
        /// Off-shift: commute/sleep owns presence — do not snap to providers.
        /// </summary>
        void RefreshAllAvatarPresence()
        {
            if (_crewWorkers == null) return;
            if (_crewPhase != CrewPhase.OnShift)
                return;

            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null) continue;
                var av = _presence.Get(wr.WorkerId);
                if (av == null) continue;

                var asg = _assignments.GetAssignment(wr.WorkerId);
                if (asg == null || asg.JobType == JobType.Unassigned)
                {
                    av.ClearFollowing();
                    if (av.IsVisuallyHidden)
                        av.Show();
                    av.SetDevForceShowHidden(_devForceShowHiddenAvatars);
                    continue;
                }

                av.SetPresencePosition(GetProviderOperatePoint(asg.JobType));
                av.SetFollowing(asg.ProviderId);
                av.Hide();
                av.SetDevForceShowHidden(_devForceShowHiddenAvatars);
            }
        }

        /// <summary>Assigned avatars while OnShift: Transform tracks host (visibility separate).</summary>
        void SyncMovingAssignedAvatars()
        {
            if (_crewPhase != CrewPhase.OnShift) return;
            if (_crewWorkers == null) return;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null) continue;
                var asg = _assignments.GetAssignment(wr.WorkerId);
                if (asg == null || asg.JobType == JobType.Unassigned) continue;
                var av = _presence.Get(wr.WorkerId);
                if (av == null) continue;
                av.SetPresencePosition(GetProviderOperatePoint(asg.JobType));
                av.SetFollowing(asg.ProviderId);
            }
        }

        void StampLiveProviderAssignments()
        {
            void Stamp(WorkerRuntime wr, JobType job, string providerId)
            {
                if (wr == null || string.IsNullOrEmpty(providerId)) return;
                _assignments.AssignInternal(wr.WorkerId, job, providerId, _absoluteGameHours);
            }

            Stamp(_workerLewis, JobType.Prospecting,
                _fieldScanner != null
                    ? _fieldScanner.ProviderId
                    : JobStatPreview.BehaviourKey(JobType.Prospecting));
            Stamp(_workerMara, JobType.Excavation, _worker != null ? _worker.ProviderId : null);
            Stamp(_workerKowalski, JobType.Hauling, _hauler != null ? _hauler.ProviderId : null);
            Stamp(_workerElena, JobType.Refining, _refiner != null ? _refiner.ProviderId : null);
            Stamp(_workerViktor, JobType.Engineering, _engineer != null ? _engineer.ProviderId : null);
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
            _ => null,
        };

        // ——— F1: selected WorkerId + control resolver (gameplay routing) ———

        static JobType LegacySlotToJob(ControlWorker slot) => slot switch
        {
            ControlWorker.Prospector => JobType.Prospecting,
            ControlWorker.Excavator => JobType.Excavation,
            ControlWorker.Hauler => JobType.Hauling,
            ControlWorker.Refiner => JobType.Refining,
            ControlWorker.Engineer => JobType.Engineering,
            _ => JobType.Unassigned,
        };

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

        /// <summary>
        /// Temporary debug bridge only — NOT used by TAB/roster after F2.
        /// Legacy UI slot → AssignedWorker on that job → selectedWorkerId.
        /// </summary>
        [System.Obsolete("F2/F5: debug-only — TAB/roster use SelectPersonById")]
        void BridgeSelectedWorkerFromLegacySlot()
        {
            var wr = GetBodyAssignedWorker(LegacySlotToJob(_control));
            _selectedWorkerId = wr != null ? wr.WorkerId : 0;
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
            _ => -1,
        };

        static WorkerBanter.JobContext JobContextFrom(JobType job) => job switch
        {
            JobType.Prospecting => WorkerBanter.JobContext.Prospecting,
            JobType.Excavation => WorkerBanter.JobContext.Excavation,
            JobType.Hauling => WorkerBanter.JobContext.Hauling,
            JobType.Refining => WorkerBanter.JobContext.Refining,
            JobType.Engineering => WorkerBanter.JobContext.Engineering,
            _ => WorkerBanter.JobContext.Prospecting,
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
            _ => UiDim,
        };

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
                    _ => "WorkerAvatar (idle)",
                };
            }
            return new WorkerControlTarget(wr, job, providerId, avatar, host);
        }

        WorkerPhysicalState GetPhysicalState(WorkerRuntime wr)
        {
            if (wr == null) return WorkerPhysicalState.Idle;
            switch (_crewPhase)
            {
                case CrewPhase.HeadingHome: return WorkerPhysicalState.CommutingHome;
                case CrewPhase.Asleep: return WorkerPhysicalState.Sleeping;
                case CrewPhase.HeadingOut: return WorkerPhysicalState.CommutingToWork;
                default:
                {
                    var asg = _assignments.GetAssignment(wr.WorkerId);
                    if (asg != null && asg.JobType != JobType.Unassigned)
                        return WorkerPhysicalState.Operating;
                    return WorkerPhysicalState.Idle;
                }
            }
        }

        /// <summary>Assignment alone is not enough — person must be OnShift and Operating.</summary>
        bool CanPerformJobActions(WorkerRuntime wr)
        {
            if (wr == null || _crewPhase != CrewPhase.OnShift) return false;
            return GetPhysicalState(wr) == WorkerPhysicalState.Operating;
        }

        bool CanPerformSelectedJobActions()
        {
            return CanPerformJobActions(FindCrewWorker(_selectedWorkerId));
        }

        bool SelectedJobIs(JobType job)
        {
            if (!CanPerformSelectedJobActions()) return false;
            var t = ResolveControlTarget();
            return t.IsAssigned && t.JobType == job;
        }

        Transform ResolvePhysicalFollowTransform(in WorkerControlTarget t)
        {
            if (!t.HasPerson) return null;
            var phys = GetPhysicalState(t.Worker);
            // Commute / sleep / idle: camera follows the person avatar, not the leftover provider
            if (phys != WorkerPhysicalState.Operating)
                return t.Avatar != null ? t.Avatar.transform : null;
            return t.JobType switch
            {
                JobType.Prospecting => _prospector != null ? _prospector.transform : null,
                JobType.Excavation => _worker != null ? _worker.transform : null,
                JobType.Hauling => _hauler != null ? _hauler.transform : null,
                JobType.Refining => _refiner != null ? _refiner.transform : null,
                JobType.Engineering => _engineer != null ? _engineer.transform : null,
                _ => t.Avatar != null ? t.Avatar.transform : null,
            };
        }

        /// <summary>Trivial idle WASD for unassigned selected person (no pathfinding).</summary>
        void TickIdleAvatarMovement(WorkerAvatar avatar, Vector2 wasd)
        {
            if (avatar == null || wasd.sqrMagnitude < 0.01f) return;
            const float speed = 1.1f;
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
            // One loose piece per cell — same size/colour/sockets as the dug wall cell
            LoosePile.SpawnCellFromDrill(_looseRoot, tip, _worker.Facing, c, _world.CellSize, WorkerRadius, x, y);
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
            TickClock();
            TickCrewPhase();

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
                if (kb.rKey.wasPressedThisFrame) ResetMap();
                if (kb.bKey.wasPressedThisFrame)
                {
                    _showBalanceHarness = !_showBalanceHarness;
                    _hudPopup = _showBalanceHarness ? HudPopupKind.Balance : HudPopupKind.None;
                }
                if (kb.lKey.wasPressedThisFrame) TryPlaceLantern();
                if (kb.tKey.wasPressedThisFrame) _tactical?.Toggle();
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
                if (kb.equalsKey.wasPressedThisFrame && SelectedJobIs(JobType.Prospecting))
                {
                    if (_fieldScanner != null && _fieldScanner.State == ProspectorScannerState.Scanning)
                        _fieldScanner.DebugForceCompleteScan(_absoluteGameHours);
                    else
                        _prospector?.DebugForceFinishScannerSetup();
                }
                if (_prospector != null && SelectedJobIs(JobType.Prospecting))
                {
                    bool boost = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
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

            // F1: tick all hosts; WASD / player job input only to selected person's current job
            if (_crewPhase == CrewPhase.OnShift)
            {
                var ctl = ResolveControlTarget();
                Vector2 digWasd = ctl.JobType == JobType.Excavation ? wasd : Vector2.zero;
                Vector2 prosWasd = ctl.JobType == JobType.Prospecting ? wasd : Vector2.zero;
                Vector2 refWasd = ctl.JobType == JobType.Refining ? wasd : Vector2.zero;
                bool prosPulse = ctl.JobType == JobType.Prospecting && scanPulse;

                _prospector?.SetHudVisible(ctl.JobType == JobType.Prospecting);
                _worker.Tick(digWasd);
                _prospector?.Tick(prosWasd, prosPulse);
                _refiner?.Tick(refWasd);

                if (ctl.IsIdlePerson && GetPhysicalState(ctl.Worker) == WorkerPhysicalState.Idle)
                    TickIdleAvatarMovement(ctl.Avatar, wasd);

                _hauler?.Tick();
                float hoursNow = Time.deltaTime / SecondsPerGameHour;
                _hauler?.SetGameHours(_absoluteGameHours);
                _engineer?.Tick(hoursNow, _absoluteGameHours);
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
            }
            ApplyDayNightLight();

            // V1.2B: conservative daytime meter drift while OnShift (not job demand)
            if (_crewPhase == CrewPhase.OnShift && _crewWorkers != null)
            {
                for (int i = 0; i < _crewWorkers.Length; i++)
                    WorkerStateDaytimeRecovery.Tick(_crewWorkers[i], hoursDelta);
                TickJobDemands(hoursDelta);
                TickProspectorDrySpell(hoursDelta);
            }

            _scanHistory?.Findings.SetGameHours(_absoluteGameHours);

            _logisticsTraffic?.SetGameHours(_absoluteGameHours);
            _logisticsTraffic?.TickDecay(hoursDelta);
            _hauler?.SetGameHours(_absoluteGameHours);

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
                ApplyCrewSleepRecoveryFraction(Mathf.Max(0f, 1f - _sleepRecoveryApplied01));
                _sleepRecoveryApplied01 = 1f;
                BeginHeadingOut(announce: true);
            }

            // Sleep recovery: person-level for entire crew (not Excavator host)
            if (_crewPhase == CrewPhase.Asleep)
                TickCrewSleepRecovery(Time.deltaTime);
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
                        if (wr == null) { _personArrived[i] = true; continue; }
                        var av = _presence.Get(wr.WorkerId);
                        Vector2 target = door + CampDoorOffsets[i % CampDoorOffsets.Length];
                        all &= StepPersonCommute(i, av, target);
                    }
                }
                bool anyStranded = false;
                for (int i = 0; i < _personStranded.Length; i++)
                    if (_personStranded[i]) anyStranded = true;
                if (all) EnterSleep();
                else if (_commuteTimer >= CommuteTimeoutSec && !anyStranded) EnterSleep();
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
                        if (wr == null) { _personArrived[i] = true; continue; }
                        var av = _presence.Get(wr.WorkerId);
                        Vector2 target = GetMorningPersonDestination(wr, i);
                        all &= StepPersonCommute(i, av, target);
                    }
                }
                if (all || _commuteTimer >= CommuteTimeoutSec) EnterOnShift();
            }
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
            else
                _providerPostExcavator = SnapPostToTunnel(_world.CellCenter(StartX, StartY), _providerFootprintR[0]);

            if (_prospector != null && _prospector.WorkMode == ProspectorWorkMode.Investigate)
            {
                _providerPostProspector = SnapPostToTunnel(
                    _prospector.Investigation.ResumeWorldPosition, AvatarCommuteRadius);
            }
            else
                _providerPostProspector = SnapPostToTunnel(_world.CellCenter(StartX - 4, StartY - 1), AvatarCommuteRadius);

            _providerPostHauler = SnapPostToTunnel(_yard != null ? _yard.DropPoint : BasecampPos, AvatarCommuteRadius);
            _providerPostRefiner = SnapPostToTunnel(_refiner != null ? _refiner.WorkPoint : BasecampPos, AvatarCommuteRadius);
            _providerPostEngineer = SnapPostToTunnel(
                _yard != null ? _yard.DropPoint + new Vector2(0.55f, 0.35f) : BasecampPos, AvatarCommuteRadius);
        }

        /// <summary>
        /// Morning destination: WorkerId → assignment → provider operate point.
        /// Unassigned → camp idle pad. Temporary bridge: walk/snap to provider then hide avatar.
        /// </summary>
        Vector2 GetMorningPersonDestination(WorkerRuntime wr, int rosterIndex)
        {
            if (wr == null) return CampNavDestination;
            var asg = _assignments.GetAssignment(wr.WorkerId);
            if (asg == null || asg.JobType == JobType.Unassigned)
            {
                Vector2 idle = CampNavDestination + CampDoorOffsets[rosterIndex % CampDoorOffsets.Length]
                    + new Vector2(0.4f, -0.35f);
                return SnapPostToTunnel(idle, AvatarCommuteRadius);
            }

            if (asg.JobType == JobType.Excavation && _hasExcavatorDigResume)
                return SnapPostToTunnel(_excavatorDigResume, AvatarCommuteRadius);

            Vector2 live = GetProviderOperatePoint(asg.JobType);
            if (live.sqrMagnitude > 0.0001f)
                return SnapPostToTunnel(live, AvatarCommuteRadius);

            return asg.JobType switch
            {
                JobType.Prospecting => _providerPostProspector,
                JobType.Excavation => _providerPostExcavator,
                JobType.Hauling => _providerPostHauler,
                JobType.Refining => _providerPostRefiner,
                JobType.Engineering => _providerPostEngineer,
                _ => CampNavDestination,
            };
        }

        void EnsurePersonNav()
        {
            TunnelNavGrid.DebugLog = navDebugLog;
            TunnelPathfinder.DebugLog = navDebugLog;
            TunnelPathfinder.DebugDraw = navDebugDraw;
            for (int i = 0; i < _personNav.Length; i++)
            {
                if (_personNav[i] == null)
                {
                    _personNav[i] = new ExcavatedPathfinder(_world, AvatarCommuteRadius);
                    _personNav[i].LateralOffset = _personLateral[i];
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
            float arrive = Mathf.Max(0.1f, AvatarCommuteRadius * 0.85f);
            if ((target - p).sqrMagnitude <= arrive * arrive)
            {
                avatar.SetPresencePosition(target);
                _personArrived[rosterIndex] = true;
                _personNav[rosterIndex]?.Invalidate();
                return true;
            }

            var cell = _world.WorldToCell(p);
            if (!_world.IsTunnelOpen(cell.x, cell.y))
            {
                if (TrySnapToNearestTunnel(ref p, AvatarCommuteRadius))
                    avatar.SetPresencePosition(p);
            }

            float speed = TravelSpeed * LoosePile.SpeedMulAt(p, AvatarCommuteRadius);
            bool done = _personNav[rosterIndex].Follow(
                p,
                target,
                speed,
                AvatarCommuteRadius,
                face: _ => { },
                tryStep: (dir, step) => PersonAvatarTryStep(avatar, dir, step));

            if (_personNav[rosterIndex] != null && _personNav[rosterIndex].Stranded)
            {
                _personStranded[rosterIndex] = true;
                DigHoodLog.Push(
                    $"CREW {avatar.DisplayName} | STRANDED — no path to destination");
                return false;
            }

            if (done)
            {
                avatar.SetPresencePosition(target);
                _personArrived[rosterIndex] = true;
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
            for (int i = 0; i < _personArrived.Length; i++)
            {
                _personArrived[i] = false;
                _personStranded[i] = false;
            }
            EnsurePersonNav();
            for (int i = 0; i < _personNav.Length; i++)
            {
                if (_personNav[i] == null) continue;
                _personNav[i].CampReturnMode = true;
                _personNav[i].Invalidate();
            }

            // Remember dig face for machine bookmark — excavator stays put
            if (_worker != null)
            {
                _excavatorDigResume = SnapPostToTunnel(
                    _worker.CaptureShiftBreakBookmark(), _providerFootprintR[0]);
                _hasExcavatorDigResume = true;
                DigHoodLog.Push(
                    $"SHIFT BREAK | Excavator stays @ {_excavatorDigResume.x:0.0},{_excavatorDigResume.y:0.0}");
            }

            // Reveal WorkerAvatars at provider / idle positions — hosts do not walk home
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
                        av.SetPresencePosition(GetProviderOperatePoint(asg.JobType));
                    av.ClearFollowing();
                    av.Show();
                    av.SetDevForceShowHidden(_devForceShowHiddenAvatars);
                }
            }

            _prospector?.SetRadar(false);
            DigHoodLog.Push("SHIFT END | People commute home — providers stay");
            // F5: no commute/off-shift job banter yet
        }

        void EnterSleep()
        {
            _crewPhase = CrewPhase.Asleep;
            _sleepRecoveryApplied01 = 0f;
            // Park/hide avatars at tent — do NOT teleport providers
            if (_crewWorkers != null)
            {
                Vector2 door = CampNavDestination;
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    var wr = _crewWorkers[i];
                    if (wr == null) continue;
                    var av = _presence.Get(wr.WorkerId);
                    if (av == null) continue;
                    av.SetPresencePosition(door + CampDoorOffsets[i % CampDoorOffsets.Length]);
                    av.ClearFollowing();
                    av.Hide(); // prototype: hidden at tent while asleep
                    av.SetDevForceShowHidden(_devForceShowHiddenAvatars);
                }
            }
            // Overnight rest fully cools the excavator machine (stays at dig face).
            _worker?.ResetHeatAfterRest();
        }

        void BeginHeadingOut(bool announce)
        {
            _crewPhase = CrewPhase.HeadingOut;
            _commuteTimer = 0f;
            RefreshProviderWorkPosts();
            for (int i = 0; i < _personArrived.Length; i++)
            {
                _personArrived[i] = false;
                _personStranded[i] = false;
            }
            EnsurePersonNav();
            for (int i = 0; i < _personNav.Length; i++)
            {
                if (_personNav[i] == null) continue;
                _personNav[i].CampReturnMode = false;
                _personNav[i].Invalidate();
            }

            // Avatars emerge from camp — providers stay where they overnighted
            Vector2 door = CampNavDestination;
            if (_crewWorkers != null)
            {
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    var wr = _crewWorkers[i];
                    if (wr == null) continue;
                    var av = _presence.Get(wr.WorkerId);
                    if (av == null) continue;
                    av.SetPresencePosition(door + CampDoorOffsets[i % CampDoorOffsets.Length]);
                    av.ClearFollowing();
                    av.Show();
                    av.SetDevForceShowHidden(_devForceShowHiddenAvatars);
                }
            }

            if (announce)
                DigHoodLog.Push("SHIFT START | Avatars heading to work");
        }

        void EnterOnShift()
        {
            _crewPhase = CrewPhase.OnShift;
            _commuteTimer = 0f;
            RefreshProviderWorkPosts();

            // Temporary morning bridge: snap avatars to assignment→provider, then hide if operating
            if (_crewWorkers != null)
            {
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    var wr = _crewWorkers[i];
                    if (wr == null) continue;
                    var av = _presence.Get(wr.WorkerId);
                    if (av == null) continue;
                    av.SetPresencePosition(GetMorningPersonDestination(wr, i));
                }
            }
            RefreshAllAvatarPresence();

            if (_hasExcavatorDigResume && _worker != null && _worker.RouteCount > 0)
                DigHoodLog.Push(
                    $"SHIFT START | Excavator still at dig face | Route {_worker.RouteCount} pin{(_worker.RouteCount == 1 ? "" : "s")}");
            if (_prospector != null && _prospector.WorkMode == ProspectorWorkMode.Investigate)
                DigHoodLog.Push($"SHIFT START | Prospector resumes {_prospector.Investigation.PlayerWorkLabel}");
            if (_refiner != null && _refiner.IsInConsultation)
                DigHoodLog.Push("SHIFT START | Refiner resumes consult");
            if (_engineer != null && _engineer.IsInfrastructureWork)
                DigHoodLog.Push($"SHIFT START | Engineer resumes {_engineer.WorkLabel}");

            _socialAura.NotifyShiftStart(_crewWorkers);
        }

        /// <summary>Fast-forward night — jump to next 08:00 and walk out (avatars only).</summary>
        public void SkipSleep()
        {
            if (_crewPhase == CrewPhase.OnShift || _crewPhase == CrewPhase.HeadingOut)
                return;

            float hoursLeft = HoursUntilMorning(_gameHour);
            float nightFrac = Mathf.Clamp01(hoursLeft / WorkerSleepRecovery.TypicalNightGameHours);
            // If already asleep with partial recovery, only apply the remainder
            float remaining = Mathf.Max(0f, nightFrac - _sleepRecoveryApplied01);
            if (_crewPhase != CrewPhase.Asleep)
            {
                // HeadingHome skip: full remaining night from current hour
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
            float nightRealSec = WorkerSleepRecovery.TypicalNightGameHours * SecondsPerGameHour;
            if (nightRealSec <= 0.001f) return;
            float add = deltaTime / nightRealSec;
            float room = Mathf.Max(0f, 1f - _sleepRecoveryApplied01);
            add = Mathf.Min(add, room);
            if (add <= 0f) return;
            ApplyCrewSleepRecoveryFraction(add);
            _sleepRecoveryApplied01 += add;
        }

        void ApplyCrewSleepRecoveryFraction(float nightFraction01)
        {
            if (nightFraction01 <= 0f || _crewWorkers == null) return;
            for (int i = 0; i < _crewWorkers.Length; i++)
            {
                var wr = _crewWorkers[i];
                if (wr == null) continue;
                wr.State.ApplySleepRecoveryFraction(nightFraction01, wr.PhysicalStaminaMax);
            }
        }

        /// <summary>Game hours from <paramref name="hour"/> until next shift start (08:00).</summary>
        static float HoursUntilMorning(float hour)
        {
            if (hour < ShiftStartHour)
                return ShiftStartHour - hour;
            return (24f - hour) + ShiftStartHour;
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
                Time.unscaledTime);

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
                    line.Text));
        }

        /// <summary>Social Aura speech — WorkerId-authored, priority over ambient banter.</summary>
        bool TryAuthoredSocialBanter(
            int workerId,
            string displayName,
            JobType jobContext,
            string role,
            string line)
        {
            if (workerId <= 0 || string.IsNullOrEmpty(line)) return false;
            var wr = FindCrewWorker(workerId);
            string name = wr != null ? wr.DisplayName : displayName;
            if (string.IsNullOrEmpty(name)) name = $"Worker {workerId}";
            if (jobContext == JobType.Unassigned) jobContext = JobType.Prospecting;
            return _banter.TrySaySocial(
                workerId,
                name,
                JobContextFrom(jobContext),
                $"SocialAura/{role}",
                _absoluteGameHours,
                line);
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
            if (IsOverHud(guiPt)) return;

            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));

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

            if (!SelectedJobIs(JobType.Excavation) || _worker == null) return;

            // RMB / Backspace: undo last pin
            if (mouse.rightButton.wasPressedThisFrame)
            {
                _worker.UndoLastPin();
                return;
            }

            if (!mouse.leftButton.wasPressedThisFrame) return;

            // Shift+LMB: replace route with one pin (go here)
            // LMB: append pin to dig route (plan path through mountain)
            bool replace = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            _worker.AddPin(world, replaceRoute: replace);
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
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
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
        }

        HudPopupKind _hudPopup;

        /// <summary>F3: open sheet target WorkerId (0 = closed). Person-owned, not role-index.</summary>
        int _openStatsWorkerId;
        /// <summary>Baseline snapshots keyed by WorkerId (index 0 unused). Crew ids 1–5.</summary>
        readonly WorkerStats[] _sheetBaselineByWorkerId =
        {
            null, new(), new(), new(), new(), new(),
        };
        readonly bool[] _sheetBaselineCaptured = new bool[6];
        readonly WorkerSheetProfile[] _sheetProfileByWorkerId =
        {
            WorkerSheetProfile.Baseline,
            WorkerSheetProfile.Baseline,
            WorkerSheetProfile.Baseline,
            WorkerSheetProfile.Baseline,
            WorkerSheetProfile.Baseline,
            WorkerSheetProfile.Baseline,
        };

        void OnDrawGizmos()
        {
            if (_socialDevDrawWorld && _crewWorkers != null && _socialAura.IsBootstrapped)
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
            bool canSkip = _crewPhase == CrewPhase.Asleep || _crewPhase == CrewPhase.HeadingHome;
            const float barH = 34f;
            float barW = Mathf.Min(720f, Mathf.Max(520f, Screen.width - 180f));
            var topBar = new Rect(10, 8, barW, barH);
            DrawCyberPanel(topBar, lit: _crewPhase == CrewPhase.OnShift);
            Block(topBar);

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
                CrewPhase.Asleep => "ASLEEP",
                CrewPhase.HeadingOut => "START",
                _ => "",
            };
            float skipW = canSkip ? 110f : 0f;
            float clockClusterW = (canSkip ? 168f : 128f) + skipW;
            float cxClock = topBar.xMax - 12f - clockClusterW;
            if (px < cxClock - 8f)
            {
                DrawVLine(cxClock - 8f, topBar.y + 8f, barH - 16f,
                    new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.22f));
            }
            GUI.Label(new Rect(cxClock, py + 1f, 28f, rowH), $"D{_dayIndex}", barLabel);
            GUI.Label(new Rect(cxClock + 28f, py - 1f, 48f, rowH), FormatClock(),
                LabelStyle(13, UiAmber, bold: true));
            GUI.Label(new Rect(cxClock + 76f, py + 1f, 88f, rowH), phaseShort, barMute);
            if (canSkip)
            {
                var skipR = new Rect(topBar.xMax - 12f - skipW, topBar.y + 5f, skipW, 24f);
                Block(skipR);
                if (DrawCyberButton(skipR, "SKIP // N", selected: false, accent: UiAmber))
                    SkipSleep();
            }

            // Heavy scanner status — under top bar
            bool showScannerHud = _prospector != null && SelectedJobIs(JobType.Prospecting);
            bool scannerExpanded = showScannerHud && (_scannerPlanMode
                || (_fieldScanner != null && _fieldScanner.State == ProspectorScannerState.Scanning));
            float scannerPanelH = !showScannerHud ? 0f
                : _scannerPlaceMode ? 78f
                : scannerExpanded ? 118f
                : 88f;

            float leftColY = topBar.yMax + 8f;
            if (showScannerHud)
                DrawHeavyScannerHud(10f, leftColY, Mathf.Min(400f, barW), scannerPanelH);

            // ——— Left crew roster (F2: person-primary) ———
            float faceW = WorkerFaceMonitor.DefaultWidth;
            float faceGap = 5f;
            float cardW = 168f, cardH = 80f, cardGap = 5f;
            float leftStackH = showScannerHud ? scannerPanelH : 0f;
            float faceX = 10f;
            float cx = faceX + faceW + faceGap;
            float cy = leftColY + leftStackH + (showScannerHud ? 8f : 0f);
            const float statsBtnW = 36f;
            float sheetDockX = cx + cardW + statsBtnW + 14f;
            bool sheetOpen = _openStatsWorkerId > 0;
            float banterX = sheetOpen ? sheetDockX + 300f : cx + cardW + statsBtnW + 14f;
            _rosterMeterTooltip = null;

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
                    if (job != JobType.Unassigned || _banter.GetForWorker(wr.WorkerId) != null)
                        DrawBanterBubble(banterX, rowY, wr);
                }
            }

            if (sheetOpen)
                DrawWorkerStatsSheet(sheetDockX, cy, _openStatsWorkerId);

            float rosterExtraY = cy + (cardH + cardGap) * 5 + 4f;

            if (SelectedJobIs(JobType.Hauling) && _hauler != null)
            {
                var goldBtn = new Rect(cx, rosterExtraY, cardW, 28f);
                Block(goldBtn);
                if (DrawCyberButton(goldBtn, _hauler.PreferGold ? "PRECIOUS FIRST // ON" : "PRECIOUS FIRST // OFF",
                        selected: _hauler.PreferGold, accent: UiAmber))
                    _hauler.TogglePreferGold();
                rosterExtraY += 32f;
                GUI.Label(new Rect(cx, rosterExtraY, cardW, 16f),
                    $"TRACK MUL  {_hauler.DebugTrackSpeedMul:0.00}×",
                    LabelStyle(9, UiDim));
            }

            if (SelectedJobIs(JobType.Engineering) && _engineer != null)
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
                // Eval diagnostics — wrap long lines
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
            }

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

            bool tacOn = _tactical != null && _tactical.Visible;
            var rTac = new Rect(bx, by + bh + 6, bw, bh);
            Block(rTac);
            if (DrawCyberButton(rTac, tacOn ? "TRUTH VIEW // ON" : "TRUTH VIEW", selected: tacOn, accent: UiAmber))
                _tactical?.Toggle();

            // Compact tool strip — non-essentials open as exclusive pop-ups
            by = rTac.yMax + 10f;
            DrawHudPopupToolStrip(bx, by, bw, 22f);

            DrawScanHistoryBrowser();
            DrawHistoricalTacticalBanner();
            DrawScanHistoryDevReadout();

            if (_hudPopup == HudPopupKind.Runtime) DrawWorkerRuntimeDevPanel();
            if (_hudPopup == HudPopupKind.Assign) DrawWorkerAssignmentDevPanel();
            if (_hudPopup == HudPopupKind.Presence) DrawWorkerPresenceDevPanel();
            if (_hudPopup == HudPopupKind.Social) DrawSocialAuraDevPanel();
            if (_hudPopup == HudPopupKind.Control) DrawWorkerControlDevPanel();
            if (_hudPopup == HudPopupKind.Sheet) DrawWorkerSheetDevPanel();
            if (_hudPopup == HudPopupKind.Banter) DrawBanterDevPanel();
            if (_hudPopup == HudPopupKind.Keys) DrawKeybindingsPanel();
            if (_hudPopup == HudPopupKind.Comms) DrawDigHoodLog();
            if (_hudPopup == HudPopupKind.Activity) DrawProspectorDevActivityBox();
            if (_hudPopup == HudPopupKind.Balance || _showBalanceHarness) DrawBalanceHarnessPanel();

            DrawFindingToast();
            DrawProspectorThinkingPanel();
            DrawRosterMeterTooltip();
            DrawAnomalyHoverTooltip();
        }

        void ToggleHudPopup(HudPopupKind kind)
        {
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
            StripBtn("ASSIGN", HudPopupKind.Assign, UiGreen);
            StripBtn("CTRL", HudPopupKind.Control, UiGreen);
            StripBtn("SHEET", HudPopupKind.Sheet, UiAmber);
            StripBtn("BANTER", HudPopupKind.Banter, new Color(0.7f, 0.55f, 1f));
            StripBtn("PRESENCE", HudPopupKind.Presence, UiCyan);
            StripBtn("SOCIAL", HudPopupKind.Social, new Color(0.45f, 0.85f, 1f));
            StripBtn("RUNTIME", HudPopupKind.Runtime, UiDim);
            StripBtn("ACTIVITY", HudPopupKind.Activity, UiAmber);
            StripBtn("BALANCE", HudPopupKind.Balance, UiAmber);
        }

        /// <summary>Right margin reserved for the always-visible panel tool strip.</summary>
        const float HudToolStripReserve = 152f;

        void DrawBalanceHarnessPanel()
        {
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

            var tStyle = new GUIStyle(titleStyle) { normal = { textColor = on ? accent : UiDim } };
            GUI.Label(new Rect(r.x + 14, r.y + 5, r.width - 20, 16),
                wr.DisplayName.ToUpperInvariant(), tStyle);
            GUI.Label(new Rect(r.x + 14, r.y + 22, r.width - 20, 13), jobLine, subStyle);
            GUI.Label(new Rect(r.x + 14, r.y + 35, r.width - 20, 12), providerLine,
                LabelStyle(8, UiMute));
            if (on)
                GUI.Label(new Rect(r.x + 14, r.y + 47, r.width - 20, 10), "// SELECTED",
                    LabelStyle(7, new Color(accent.r, accent.g, accent.b, 0.65f)));

            DrawRosterStateBars(r, wr);

            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                SelectPersonById(wr.WorkerId);
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

            const float barH = 2.2f;
            const float gap = 1.4f;
            float stackH = barH * 4f + gap * 3f;
            float x = card.x + 12f;
            float w = card.width - 20f;
            float y = card.yMax - stackH - 5f;

            DrawRosterMeterBar(new Rect(x, y, w, barH), stam / stamMax, RosterStaminaCol,
                "STAMINA", $"{stam:0}/{stamMax:0}", "Physical reserve.");
            y += barH + gap;
            DrawRosterMeterBar(new Rect(x, y, w, barH), st.FocusState / 100f, RosterFocusCol,
                "FOCUS", $"{st.FocusState:0}", "Current concentration.");
            y += barH + gap;
            DrawRosterMeterBar(new Rect(x, y, w, barH), st.Frustration / 100f, RosterFrustrationCol,
                "FRUSTRATION", $"{st.Frustration:0}", "Pressure and irritation.");
            y += barH + gap;
            DrawRosterMeterBar(new Rect(x, y, w, barH), st.Morale / 100f, RosterMoraleCol,
                "MORALE", $"{st.Morale:0}", "Broader outlook.");
        }

        void DrawRosterMeterBar(Rect r, float fill01, Color accent, string name, string value, string desc)
        {
            fill01 = Mathf.Clamp01(fill01);
            var prev = GUI.color;
            GUI.color = new Color(0.04f, 0.06f, 0.08f, 0.55f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            float fill = fill01 * r.width;
            if (fill > 0.4f)
            {
                GUI.color = new Color(accent.r, accent.g, accent.b, 0.82f);
                GUI.DrawTexture(new Rect(r.x, r.y, fill, r.height), Texture2D.whiteTexture);
            }
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.28f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), Texture2D.whiteTexture);
            GUI.color = prev;

            // Invisible hit — set tooltip without stealing the card click (checked before card button)
            var e = Event.current;
            if (e != null && e.type == EventType.Repaint && r.Contains(e.mousePosition))
            {
                _rosterMeterTooltip = $"{name}  {value}\n{desc}";
                _rosterMeterTooltipGui = e.mousePosition;
            }
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

            const float tw = 168f;
            const float th = 34f;
            float tx = Mathf.Clamp(gui.x + 12f, 8f, Screen.width - tw - 8f);
            float ty = Mathf.Clamp(gui.y + 14f, 8f, Screen.height - th - 8f);
            var panel = new Rect(tx, ty, tw, th);
            DrawCyberPanel(panel, lit: true, accentOverride: UiCyan);
            Block(panel);

            int nl = _rosterMeterTooltip.IndexOf('\n');
            string head = nl >= 0 ? _rosterMeterTooltip.Substring(0, nl) : _rosterMeterTooltip;
            string body = nl >= 0 ? _rosterMeterTooltip.Substring(nl + 1) : "";
            GUI.Label(new Rect(panel.x + 8f, panel.y + 5f, tw - 16f, 12f),
                head, LabelStyle(9, UiWhite, bold: true));
            GUI.Label(new Rect(panel.x + 8f, panel.y + 17f, tw - 16f, 12f),
                body, LabelStyle(8, UiMute));
        }

        WorkerSheetProfile GetSheetProfile(int workerId)
        {
            if ((uint)workerId >= (uint)_sheetProfileByWorkerId.Length)
                return WorkerSheetProfile.Baseline;
            return _sheetProfileByWorkerId[workerId];
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
            if ((uint)workerId >= (uint)_sheetBaselineByWorkerId.Length) return;
            if (_sheetBaselineCaptured[workerId]) return;
            var wr = FindCrewWorker(workerId);
            if (wr?.Stats == null) return;
            if (_sheetBaselineByWorkerId[workerId] == null)
                _sheetBaselineByWorkerId[workerId] = new WorkerStats();
            _sheetBaselineByWorkerId[workerId].CopyFrom(wr.Stats);
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
                wr.Stats.CopyFrom(_sheetBaselineByWorkerId[workerId]);
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
            else
                wr.Stats.CopyFrom(_sheetBaselineByWorkerId[workerId]);
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
            GUI.Label(new Rect(x0, y, innerW, 12f),
                $"id {wr.WorkerId}  ·  {wr.StatsRefLabel}",
                LabelStyle(8, UiMute));
            y += 13f;
            GUI.Label(new Rect(x0, y, innerW, 12f),
                $"Current Job: {JobStatPreview.DisplayName(job)}",
                LabelStyle(9, UiCyan));
            y += 13f;
            if (job != JobType.Unassigned)
            {
                GUI.Label(new Rect(x0, y, innerW, 12f),
                    $"Provider: {ProviderLabelForAssignment(asg)}",
                    LabelStyle(8, UiAmber));
                y += 13f;
            }
            DrawHLine(x0, y, innerW, new Color(accent.r, accent.g, accent.b, 0.28f));
            y += 8f;

            // ——— Person profiles (always) ———
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

            // ——— Job-specific test presets (contextual) ———
            y = DrawJobTestPresetRow(ref y, x0, innerW, btnW, btnH, job, workerId);

            DrawHLine(x0, y, innerW, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.15f));
            y += 8f;

            // ——— Personal conditions (always person-owned) ———
            y = DrawPersonalConditionsBlock(ref y, x0, innerW, wr, mute, sec);

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
                relevant = new HashSet<WorkerStatId>(jobDef.RelevantStats);
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
            GUI.Label(new Rect(x0, y, innerW, 12f), "PERSON STATE · V1.2C", sec);
            y += 14f;

            var st = wr.State;
            var asg = _assignments.GetAssignment(wr.WorkerId);
            var job = asg != null ? asg.JobType : JobType.Unassigned;
            var demand = ResolveDemandFor(wr, job);
            GUI.Label(new Rect(x0, y, innerW, 11f),
                $"ACTIVITY  {demand.ActivityLabel}",
                LabelStyle(8, UiCyan, bold: true));
            y += 12f;
            GUI.Label(new Rect(x0, y, innerW, 11f),
                $"DEMAND  P {demand.Physical:0.00}  M {demand.Mental:0.00}  A {demand.Attention:0.00}",
                mute);
            y += 13f;

            // Debug condition tags
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
            DrawMeterRow(ref y, x0, innerW, "FOCUS ST", st.FocusState, mute,
                st.FocusState >= 70f ? UiGreen :
                st.FocusState >= 40f ? UiCyan : UiAmber);
            DrawMeterRow(ref y, x0, innerW, "FRUST.", st.Frustration, mute,
                st.Frustration >= 70f ? new Color(1f, 0.35f, 0.3f) :
                st.Frustration >= 40f ? UiAmber : UiDim);
            DrawMeterRow(ref y, x0, innerW, "MORALE", st.Morale, mute,
                st.Morale >= 65f ? UiGreen :
                st.Morale >= 40f ? UiCyan : UiAmber);

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

            y += 4f;
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

            DrawHLine(x0, y, innerW, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.15f));
            y += 8f;
            return y;
        }

        float DrawRecentStateEventsBlock(ref float y, float x0, float innerW,
            WorkerRuntime wr, GUIStyle mute, GUIStyle sec)
        {
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
            GUI.Label(new Rect(x0, y, innerW, 12f), "MACHINE CONTEXT", sec);
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

            GUI.Label(new Rect(x0, y, innerW, 12f),
                $"Route {_worker.RouteCount}  ·  Tool {_worker.ToolPower}  ·  {_worker.ProviderId}",
                LabelStyle(8, UiDim));
            y += 14f;

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
            const float panelH = 474f;
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
                ("1 / 2 / 3", "Scan short · med · long"),
                ("U", "Tactical View (anomalies)"),
                ("H", "Scan History (from Tactical)"),
                ("T", "Truth View (debug)"),
                ("Alt", "20× setup / 120× scan / 8× analysis"),
                ("=", "Force READY / finish scan"),
                ("N", "Skip sleep → 08:00"),
                ("G", "Hauler / Refiner priority"),
                ("L", "Place lantern"),
                ("R", "Reset map"),
                ("B", "Balance harness"),
            };

            float y = r.y + 34f;
            for (int i = 0; i < rows.Length; i++)
            {
                GUI.Label(new Rect(r.x + 12, y, 88, 15), rows[i].k, key);
                GUI.Label(new Rect(r.x + 102, y, panelW - 118, 15), rows[i].d, desc);
                y += 16.5f;
            }
        }

        static GUIStyle LabelStyle(int size, Color color, bool bold = false)
        {
            // Floor tiny DEV/body sizes so IMGUI stays readable without redesign.
            int s = size < 9 ? 9 : size;
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = s,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                normal = { textColor = color },
                richText = false,
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 2),
            };
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
            float py = 88f;
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

            const float tw = 340f;
            float th = string.IsNullOrEmpty(question) ? 78f : 112f;
            float px = 12f;
            float py = 100f;
            var r = new Rect(px, py, tw, th);
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
                ProspectorInvestigationState.WalkingToRefiner => $"→ refiner  {dist:0.00}m",
                ProspectorInvestigationState.ReturningToAnalysis => $"→ desk  {dist:0.00}m",
                ProspectorInvestigationState.InspectingRock =>
                    $"dwell  {loop.DwellHours:0.00}/{loop.DwellNeeded:0.00}h  ({dwell01 * 100f:0}%)",
                ProspectorInvestigationState.ConsultingRefiner =>
                    $"discuss  {loop.DwellHours:0.00}/{loop.DwellNeeded:0.00}h" +
                    (_refiner != null && _refiner.IsInConsultation ? "  · paused" : ""),
                ProspectorInvestigationState.Analysing =>
                    cur != null && analyst != null && analyst.IsDeskClockActive
                        ? $"desk clock  {cur.AnalysisElapsedHours:0.00}/{cur.AnalysisDurationHours:0.00}h"
                        : $"at desk  dist {dist:0.00}m",
                _ => dist < 0.6f ? "near desk" : $"dist {dist:0.00}m",
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
            float th = 168f + evidenceLines * 13f + (cur != null && cur.RemainingNeeds.Count > 0 ? 28f : 14f);
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
            const float pw = 258f;
            float ph = 190f;
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
                (_sheetBaselineCaptured[wr.WorkerId] ? "  ✓" : "  (pending)"),
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

            GUI.Label(new Rect(x, y, inner, 12f), "DEV // SOCIAL AURA · PLAYTEST",
                LabelStyle(8, UiMute, bold: true));
            y += 14f;

            var wr = FindCrewWorker(_selectedWorkerId) ?? _crewWorkers[0];
            if (wr == null) return;
            var st = wr.State;
            var kind = MapSocialPresence(wr);
            bool elig = SocialAuraEligibility.IsEligible(wr, kind);
            var actor = _socialAura.World.Get(wr.WorkerId);
            actor?.RefreshExpression();

            GUI.Label(new Rect(x, y, inner, 11f),
                $"{wr.DisplayName.ToUpperInvariant()} [{wr.WorkerId}]  {kind}  elig={(elig ? "Y" : "N")}",
                LabelStyle(8, UiWhite, bold: true));
            y += 13f;

            var tog = new Rect(x, y, inner, 18f);
            Block(tog);
            if (DrawCyberButton(tog,
                    _socialDevDrawWorld ? "WORLD DEBUG · ON" : "WORLD DEBUG · OFF",
                    selected: _socialDevDrawWorld, accent: UiAmber))
                _socialDevDrawWorld = !_socialDevDrawWorld;
            y += 22f;

            // DEV frustration injectors — direct State write, no gameplay events.
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

            float viewH = r.yMax - y - 6f;
            var view = new Rect(x, y, inner, viewH);
            float contentW = inner - 14f;
            // Approximate content height for scroll
            float contentH = 1180f + _socialNearbyScratch.Count * 36f + _socialDevHistory.Count * 12f
                + (_crewWorkers != null ? _crewWorkers.Length * 48f : 0f);
            _socialDevScroll = GUI.BeginScrollView(view, _socialDevScroll,
                new Rect(0f, 0f, contentW, contentH), false, true);
            float sx = 0f;
            float sy = 0f;
            float sw = contentW;

            // ——— MOOD ———
            GUI.Label(new Rect(sx, sy, sw, 10f), "MOOD", LabelStyle(7, UiMute, bold: true));
            sy += 11f;
            if (st != null)
            {
                GUI.Label(new Rect(sx, sy, sw, 10f),
                    $"Morale {st.Morale:0.0}   Frustration {st.Frustration:0.0}   FocusState {st.FocusState:0.0}",
                    LabelStyle(7, UiWhite));
                sy += 11f;
            }
            float pos = actor != null ? actor.Expression.Positive : 0f;
            float neg = actor != null ? actor.Expression.Negative : 0f;
            float reachW = actor != null ? SocialAuraLiveTuning.EffectiveReach(actor.Expression) : 0f;
            float cell = _world != null && _world.CellSize > 0.001f ? _world.CellSize : 1f;
            float reachTiles = reachW / cell;
            GUI.Label(new Rect(sx, sy, sw, 10f),
                $"PosExpr {pos:0.00}   NegExpr {neg:0.00}   class {(actor != null ? actor.Expression.Class.ToString() : "—")}",
                LabelStyle(7, UiCyan));
            sy += 11f;
            GUI.Label(new Rect(sx, sy, sw, 10f),
                $"Aura reach  {reachW:0.00} wu  /  {reachTiles:0.00} tiles   I={(actor != null ? actor.Expression.Intensity : 0f):0.00}",
                LabelStyle(7, UiAmber));
            sy += 13f;

            GUI.Label(new Rect(sx, sy, sw, 10f),
                $"Crew enc {_socialAura.TotalEncounters}  (work {_socialAura.WorkAreaEncounters}  " +
                $"camp {_socialAura.CampEncounters}  commute {_socialAura.CommuteEncounters})  " +
                $"cdSup {_socialAura.CooldownSuppressions}",
                LabelStyle(6, UiDim));
            sy += 13f;

            // ——— NEARBY ———
            _socialAura.FillNearbyDebug(
                wr.WorkerId, _crewWorkers, _presence, MapSocialPresence,
                id =>
                {
                    var asg = _assignments.GetAssignment(id);
                    return asg != null ? asg.JobType : JobType.Unassigned;
                },
                _socialNearbyScratch);

            GUI.Label(new Rect(sx, sy, sw, 10f),
                $"NEARBY WORKERS  ·  P trigger {SocialAuraTuning.PressureTrigger:0.00}",
                LabelStyle(7, UiMute, bold: true));
            sy += 12f;

            if (_socialNearbyScratch.Count == 0)
            {
                GUI.Label(new Rect(sx, sy, sw, 10f), "(no other avatars)", LabelStyle(6, UiDim));
                sy += 12f;
            }

            for (int i = 0; i < _socialNearbyScratch.Count; i++)
            {
                var n = _socialNearbyScratch[i];
                float dTiles = n.Distance / cell;
                Color rowCol = n.InsideReach ? (n.Exposed ? UiAmber : UiCyan) : UiDim;
                GUI.Label(new Rect(sx, sy, sw, 12f),
                    $"{n.OtherName}  d={n.Distance:0.00}wu/{dTiles:0.00}t  reach={(n.InsideReach ? "YES" : "NO")}  elig={(n.Eligible ? "Y" : "N")}",
                    LabelStyle(9, rowCol));
                sy += 12f;
                GUI.Label(new Rect(sx, sy, sw, 12f),
                    $"  P {n.Pressure:0.00}/{SocialAuraTuning.PressureTrigger:0.00}  CD {n.Cooldown:0.00}  ctx {n.Context}",
                    LabelStyle(9, UiDim));
                sy += 12f;
                GUI.Label(new Rect(sx, sy, sw, 12f),
                    $"  Rel T={n.Trust:0.0}  W={n.Warmth:0.0}  H={n.Hostility:0.0}  R={n.Respect:0.0}   theirReach {n.Reach:0.00}",
                    LabelStyle(9, UiCyan));
                sy += 14f;
            }

            // ——— LAST ENCOUNTER ———
            GUI.Label(new Rect(sx, sy, sw, 10f), "LAST ENCOUNTER", LabelStyle(7, UiMute, bold: true));
            sy += 11f;

            SocialDevEncounterStamp? lastStamp = null;
            if (_socialDevHistory.Count > 0)
                lastStamp = _socialDevHistory[_socialDevHistory.Count - 1];
            var last = lastStamp.HasValue ? lastStamp.Value.Log : _socialAura.LastEncounter;

            if (last == null)
            {
                GUI.Label(new Rect(sx, sy, sw, 10f), "(none yet)", LabelStyle(6, UiDim));
                sy += 12f;
            }
            else
            {
                string when = lastStamp.HasValue
                    ? $"D{lastStamp.Value.Day} {FormatHourClock(lastStamp.Value.GameHour)}"
                    : $"shift#{last.ShiftIndex} t={last.TimeInShift:0.00}";
                string initName = FindCrewWorker(last.InitiatorId)?.DisplayName ?? $"#{last.InitiatorId}";
                string tgtName = FindCrewWorker(last.TargetId)?.DisplayName ?? $"#{last.TargetId}";

                GUI.Label(new Rect(sx, sy, sw, 10f), when, LabelStyle(6, UiAmber));
                sy += 10f;
                GUI.Label(new Rect(sx, sy, sw, 10f),
                    $"{initName} → {tgtName}   ctx {last.Context}",
                    LabelStyle(6, UiWhite));
                sy += 10f;
                GUI.Label(new Rect(sx, sy, sw, 10f),
                    $"Act {last.Action}  d20={last.ActionD20} {(last.ActionSuccess ? "OK" : "FAIL")}",
                    LabelStyle(6, last.ActionSuccess ? UiGreen : UiAmber));
                sy += 10f;
                GUI.Label(new Rect(sx, sy, sw, 10f),
                    $"Rsp {last.Response}  d20={last.ResponseD20} {(last.ResponseSuccess ? "OK" : "FAIL")}",
                    LabelStyle(6, last.ResponseSuccess ? UiGreen : UiAmber));
                sy += 10f;
                GUI.Label(new Rect(sx, sy, sw, 10f),
                    $"Rel Δ  I→T T/W/H {last.DeltaTrustIT:+0.0;-0.0}/{last.DeltaWarmthIT:+0.0;-0.0}/{last.DeltaHostilityIT:+0.0;-0.0}",
                    LabelStyle(6, UiDim));
                sy += 10f;
                GUI.Label(new Rect(sx, sy, sw, 10f),
                    $"       T→I T/W/H {last.DeltaTrustTI:+0.0;-0.0}/{last.DeltaWarmthTI:+0.0;-0.0}/{last.DeltaHostilityTI:+0.0;-0.0}",
                    LabelStyle(6, UiDim));
                sy += 10f;
                GUI.Label(new Rect(sx, sy, sw, 10f),
                    $"State Δ  Fr I/T {last.DeltaFrustrationInit:+0.0;-0.0}/{last.DeltaFrustrationTarget:+0.0;-0.0}  " +
                    $"Mo {last.DeltaMoraleInit:+0.0;-0.0}/{last.DeltaMoraleTarget:+0.0;-0.0}",
                    LabelStyle(6, UiDim));
                sy += 11f;

                string dialogueLine;
                Color dialogueCol;
                if (lastStamp.HasValue)
                {
                    var s = lastStamp.Value;
                    if (s.Presented)
                    {
                        dialogueLine = $"Dialogue SHOWN ({s.LinesQueued} lines)";
                        dialogueCol = UiGreen;
                    }
                    else if (s.SuppressedDistance)
                    {
                        dialogueLine = $"Dialogue SUPPRESSED — distance > {SocialAuraPresenter.PresentDistanceMax:0.0}wu";
                        dialogueCol = UiAmber;
                    }
                    else if (s.SuppressedSleep)
                    {
                        dialogueLine = "Dialogue SUPPRESSED — sleep / ineligible presence";
                        dialogueCol = UiAmber;
                    }
                    else
                    {
                        dialogueLine = "Dialogue SUPPRESSED — unknown gate";
                        dialogueCol = UiAmber;
                    }
                }
                else
                {
                    var pres = _socialPresenter.LastPresentation;
                    if (pres.Log == last && pres.Presented)
                    {
                        dialogueLine = $"Dialogue SHOWN ({pres.LinesQueued} lines)";
                        dialogueCol = UiGreen;
                    }
                    else if (pres.Log == last && pres.SuppressedDistance)
                    {
                        dialogueLine = $"Dialogue SUPPRESSED — distance > {SocialAuraPresenter.PresentDistanceMax:0.0}wu";
                        dialogueCol = UiAmber;
                    }
                    else if (pres.Log == last && pres.SuppressedSleep)
                    {
                        dialogueLine = "Dialogue SUPPRESSED — sleep / ineligible presence";
                        dialogueCol = UiAmber;
                    }
                    else
                    {
                        dialogueLine = "Dialogue — (no presentation stamp)";
                        dialogueCol = UiDim;
                    }
                }

                GUI.Label(new Rect(sx, sy, sw, 10f), dialogueLine, LabelStyle(6, dialogueCol));
                sy += 10f;
                if (lastStamp.HasValue && lastStamp.Value.Presented)
                {
                    GUI.Label(new Rect(sx, sy, sw, 10f),
                        $"  I: {TruncateDev(lastStamp.Value.InitiatorLine, 44)}",
                        LabelStyle(6, UiWhite));
                    sy += 10f;
                    GUI.Label(new Rect(sx, sy, sw, 10f),
                        $"  T: {TruncateDev(lastStamp.Value.ResponseLine, 44)}",
                        LabelStyle(6, UiWhite));
                    sy += 10f;
                }
                GUI.Label(new Rect(sx, sy, sw, 10f),
                    TruncateDev(last.OutcomeSummary ?? "", 48),
                    LabelStyle(6, UiMute));
                sy += 13f;
            }

            // ——— RELATIONSHIP-AWARE WORK V1 (Exc↔Eng repair only) ———
            GUI.Label(new Rect(sx, sy, sw, 12f), "COOP WORK V1 · EXC↔ENG REPAIR",
                LabelStyle(9, UiMute, bold: true));
            sy += 13f;
            var coopPanel = EvaluateExcavatorEngineerCoop();
            if (!coopPanel.Active)
            {
                GUI.Label(new Rect(sx, sy, sw, 12f), "(need assigned Excavator + Engineer)",
                    LabelStyle(9, UiDim));
                sy += 12f;
            }
            else
            {
                GUI.Label(new Rect(sx, sy, sw, 12f),
                    $"CooperationQuality  {coopPanel.Quality:0.00}",
                    LabelStyle(9, UiAmber, bold: true));
                sy += 12f;
                GUI.Label(new Rect(sx, sy, sw, 12f),
                    TruncateDev(coopPanel.Factors ?? "", 48),
                    LabelStyle(9, UiWhite));
                sy += 12f;
                GUI.Label(new Rect(sx, sy, sw, 12f),
                    $"Op  dispatch×{coopPanel.DispatchSpeedMul:0.00}  repairDur×{coopPanel.RepairDurationMul:0.00}",
                    LabelStyle(9, UiCyan));
                sy += 12f;
                GUI.Label(new Rect(sx, sy, sw, 12f),
                    $"Chance  benefit {coopPanel.BenefitChance:0.00}  setback {coopPanel.SetbackChance:0.00}",
                    LabelStyle(9, UiDim));
                sy += 12f;
                GUI.Label(new Rect(sx, sy, sw, 12f),
                    $"Last  {ExcavatorEngineerCooperation.LastConsequence}",
                    LabelStyle(9, UiWhite));
                sy += 12f;
                GUI.Label(new Rect(sx, sy, sw, 12f),
                    TruncateDev(ExcavatorEngineerCooperation.LastConsequenceDetail ?? "—", 48),
                    LabelStyle(9, UiDim));
                sy += 12f;
            }
            sy += 4f;

            // ——— RELATIONSHIP V1.1 + MEMORY V1 ———
            var memStore = _socialAura.Memory;
            int memFocus = wr.WorkerId;
            int pairOther = 0;
            if (_socialNearbyScratch.Count > 0)
                pairOther = _socialNearbyScratch[0].OtherId;
            else if (_crewWorkers != null)
            {
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    if (_crewWorkers[i] != null && _crewWorkers[i].WorkerId != memFocus)
                    {
                        pairOther = _crewWorkers[i].WorkerId;
                        break;
                    }
                }
            }

            GUI.Label(new Rect(sx, sy, sw, 12f), "RELATIONSHIP V1.1 (selected pair)",
                LabelStyle(9, UiMute, bold: true));
            sy += 13f;
            if (pairOther > 0)
            {
                string on = FindCrewWorker(pairOther)?.DisplayName ?? $"#{pairOther}";
                var pairRel = _socialAura.Relation(memFocus, pairOther);
                var towardPair = memStore != null
                    ? memStore.GetToward(memFocus, pairOther)
                    : System.Array.Empty<SocialMemoryEntry>();
                var derived = RelationshipClassifier.Classify(pairRel, towardPair, out var derivedWhy);
                GUI.Label(new Rect(sx, sy, sw, 12f),
                    $"{wr.DisplayName}→{on}",
                    LabelStyle(9, UiCyan));
                sy += 12f;
                GUI.Label(new Rect(sx, sy, sw, 12f),
                    RelationshipClassifier.FormatAxes(pairRel),
                    LabelStyle(9, UiWhite));
                sy += 12f;
                GUI.Label(new Rect(sx, sy, sw, 12f),
                    $"Derived  {derived}",
                    LabelStyle(9, UiAmber, bold: true));
                sy += 12f;
                GUI.Label(new Rect(sx, sy, sw, 12f),
                    TruncateDev(derivedWhy ?? "", 48),
                    LabelStyle(9, UiDim));
                sy += 12f;
                RelationshipClassifier.FillSupportingMemories(
                    derived, towardPair, _socialMemoryScratch, 3);
                GUI.Label(new Rect(sx, sy, sw, 12f), "Supporting memories",
                    LabelStyle(9, UiMute));
                sy += 12f;
                if (_socialMemoryScratch.Count == 0)
                {
                    GUI.Label(new Rect(sx, sy, sw, 12f), "  (none)", LabelStyle(9, UiDim));
                    sy += 12f;
                }
                else
                {
                    for (int m = 0; m < _socialMemoryScratch.Count; m++)
                    {
                        var e = _socialMemoryScratch[m];
                        GUI.Label(new Rect(sx, sy, sw, 12f),
                            $"  {e.Type}  {e.Strength:0.00}  {e.Context}",
                            LabelStyle(9, UiWhite));
                        sy += 12f;
                    }
                }
            }
            else
            {
                GUI.Label(new Rect(sx, sy, sw, 12f), "(no pair selected)", LabelStyle(9, UiDim));
                sy += 12f;
            }

            sy += 4f;
            GUI.Label(new Rect(sx, sy, sw, 12f), "MEMORY V1 (observer→target)",
                LabelStyle(9, UiMute, bold: true));
            sy += 13f;

            if (memStore == null || memStore.TotalEntries == 0)
            {
                GUI.Label(new Rect(sx, sy, sw, 12f), "(no memories yet)", LabelStyle(9, UiDim));
                sy += 12f;
            }
            else if (_crewWorkers != null)
            {
                float nowH = _absoluteGameHours;
                for (int i = 0; i < _crewWorkers.Length; i++)
                {
                    var other = _crewWorkers[i];
                    if (other == null || other.WorkerId == memFocus) continue;
                    var toward = memStore.GetToward(memFocus, other.WorkerId);
                    if (toward.Count == 0)
                    {
                        GUI.Label(new Rect(sx, sy, sw, 12f),
                            $"→ {other.DisplayName}: —",
                            LabelStyle(9, UiDim));
                        sy += 12f;
                        continue;
                    }
                    GUI.Label(new Rect(sx, sy, sw, 12f),
                        $"→ {other.DisplayName} ({toward.Count})",
                        LabelStyle(9, UiCyan));
                    sy += 12f;
                    for (int m = 0; m < toward.Count && m < 6; m++)
                    {
                        var e = toward[m];
                        GUI.Label(new Rect(sx, sy, sw, 12f),
                            $"  {e.Type}  str={e.Strength:0.00}  age={e.AgeHours(nowH):0.0}h  {e.Context}",
                            LabelStyle(9, e.Major ? UiAmber : UiWhite));
                        sy += 12f;
                    }
                }

                if (pairOther > 0)
                {
                    string on = FindCrewWorker(pairOther)?.DisplayName ?? $"#{pairOther}";
                    memStore.GetStrongest(memFocus, pairOther, _socialMemoryScratch, 3);
                    GUI.Label(new Rect(sx, sy, sw, 12f),
                        $"TOP3 {wr.DisplayName}→{on}",
                        LabelStyle(9, UiAmber, bold: true));
                    sy += 12f;
                    if (_socialMemoryScratch.Count == 0)
                    {
                        GUI.Label(new Rect(sx, sy, sw, 12f), "  (none)", LabelStyle(9, UiDim));
                        sy += 12f;
                    }
                    else
                    {
                        for (int m = 0; m < _socialMemoryScratch.Count; m++)
                        {
                            var e = _socialMemoryScratch[m];
                            GUI.Label(new Rect(sx, sy, sw, 12f),
                                $"  {e.Type}  {e.Strength:0.00}  {e.Context}",
                                LabelStyle(9, UiWhite));
                            sy += 12f;
                        }
                    }
                }
            }

            sy += 4f;

            // ——— RELATIONSHIP WORK PLAYTEST (5-day, Exc↔Eng repair) ———
            GUI.Label(new Rect(sx, sy, sw, 12f), "RELATIONSHIP WORK PLAYTEST · 5-DAY",
                LabelStyle(9, UiAmber, bold: true));
            sy += 13f;

            int maraId = _workerMara != null ? _workerMara.WorkerId : 2;
            int viktorId = _workerViktor != null ? _workerViktor.WorkerId : 5;
            var mvRel = _socialAura.Relation(maraId, viktorId);
            var mvMem = _socialAura.Memory != null
                ? _socialAura.Memory.GetToward(maraId, viktorId)
                : System.Array.Empty<SocialMemoryEntry>();
            var mvClass = RelationshipClassifier.Classify(mvRel, mvMem, out _);
            GUI.Label(new Rect(sx, sy, sw, 12f),
                $"Mara→Viktor  {RelationshipClassifier.FormatAxes(mvRel)}",
                LabelStyle(9, UiWhite));
            sy += 12f;
            GUI.Label(new Rect(sx, sy, sw, 12f),
                $"Derived  {mvClass}   preset  {_coopWorkPlaytest.LastPresetLabel}",
                LabelStyle(9, UiCyan));
            sy += 12f;

            RelationshipWorkPlaytestTracker.FillTopCoopMemories(
                _socialAura.Memory, maraId, viktorId, _coopMemScratch, 3);
            GUI.Label(new Rect(sx, sy, sw, 12f), "Top coop memories:",
                LabelStyle(9, UiMute));
            sy += 12f;
            if (_coopMemScratch.Count == 0)
            {
                GUI.Label(new Rect(sx, sy, sw, 12f), "  (none)", LabelStyle(9, UiDim));
                sy += 12f;
            }
            else
            {
                for (int m = 0; m < _coopMemScratch.Count; m++)
                {
                    var e = _coopMemScratch[m];
                    string who = e.ObserverId == maraId ? "M→V" : "V→M";
                    GUI.Label(new Rect(sx, sy, sw, 12f),
                        $"  {who} {e.Type}  {e.Strength:0.00}",
                        LabelStyle(9, UiWhite));
                    sy += 12f;
                }
            }

            var setRelBtn = new Rect(sx, sy, sw, 20f);
            if (DrawCyberButton(setRelBtn,
                    $"SET TEST RELATIONSHIP · {_coopWorkPlaytest.LastPresetLabel}",
                    selected: false, accent: UiAmber))
                DevCycleTestRelationship();
            sy += 24f;

            var coopDay = _coopWorkPlaytest.Current;
            if (coopDay != null && coopDay.DayIndex > 0)
            {
                GUI.Label(new Rect(sx, sy, sw, 12f),
                    _coopWorkPlaytest.FormatCompactDay(coopDay),
                    LabelStyle(9, UiWhite));
                sy += 12f;
            }
            else
            {
                GUI.Label(new Rect(sx, sy, sw, 12f), "(no repair collabs yet today)",
                    LabelStyle(9, UiDim));
                sy += 12f;
            }
            var coopHist = _coopWorkPlaytest.Days;
            if (coopHist != null && coopHist.Count > 0)
            {
                GUI.Label(new Rect(sx, sy, sw, 12f), "prior coop days:", LabelStyle(9, UiMute));
                sy += 12f;
                for (int i = coopHist.Count - 1; i >= 0; i--)
                {
                    GUI.Label(new Rect(sx, sy, sw, 12f),
                        _coopWorkPlaytest.FormatCompactDay(coopHist[i]),
                        LabelStyle(9, UiDim));
                    sy += 12f;
                }
            }

            sy += 4f;

            // ——— PLAYTEST SUMMARY (5-day rolling, observation only) ———
            GUI.Label(new Rect(sx, sy, sw, 12f), "PLAYTEST SUMMARY · 5-DAY",
                LabelStyle(9, UiAmber, bold: true));
            sy += 13f;

            var cur = _socialPlaytest.Current;
            if (cur != null && cur.DayIndex > 0)
            {
                GUI.Label(new Rect(sx, sy, sw, 12f),
                    _socialPlaytest.FormatCompactDay(cur),
                    LabelStyle(9, UiWhite));
                sy += 12f;

                // Per-worker encounters + avg frustration
                if (_crewWorkers != null)
                {
                    for (int i = 0; i < _crewWorkers.Length; i++)
                    {
                        var w = _crewWorkers[i];
                        if (w == null) continue;
                        int ec = 0;
                        if (cur.EncountersByWorker != null)
                            cur.EncountersByWorker.TryGetValue(w.WorkerId, out ec);
                        float af = cur.AvgFrustration(w.WorkerId);
                        GUI.Label(new Rect(sx, sy, sw, 12f),
                            $"  {w.DisplayName}  enc={ec}  FrØ={af:0.0}",
                            LabelStyle(9, UiCyan));
                        sy += 12f;
                    }
                }

                // Top pairs today
                if (cur.EncountersByPair != null && cur.EncountersByPair.Count > 0)
                {
                    GUI.Label(new Rect(sx, sy, sw, 12f), "  pairs:", LabelStyle(9, UiMute));
                    sy += 12f;
                    foreach (var kv in cur.EncountersByPair)
                    {
                        int a = (int)(kv.Key >> 32);
                        int b = (int)(uint)kv.Key;
                        string na = FindCrewWorker(a)?.DisplayName ?? $"#{a}";
                        string nb = FindCrewWorker(b)?.DisplayName ?? $"#{b}";
                        GUI.Label(new Rect(sx, sy, sw, 12f),
                            $"  {na}·{nb} ×{kv.Value}",
                            LabelStyle(9, UiDim));
                        sy += 12f;
                    }
                }
            }
            else
            {
                GUI.Label(new Rect(sx, sy, sw, 12f), "(collecting…)", LabelStyle(9, UiDim));
                sy += 12f;
            }

            // Prior archived days (newest first)
            var hist = _socialPlaytest.Days;
            if (hist != null && hist.Count > 0)
            {
                GUI.Label(new Rect(sx, sy, sw, 12f), "prior days:", LabelStyle(9, UiMute));
                sy += 12f;
                for (int i = hist.Count - 1; i >= 0; i--)
                {
                    GUI.Label(new Rect(sx, sy, sw, 12f),
                        _socialPlaytest.FormatCompactDay(hist[i]),
                        LabelStyle(9, UiDim));
                    sy += 12f;
                }
            }

            sy += 6f;

            // ——— LAST 10 CREW ———
            GUI.Label(new Rect(sx, sy, sw, 10f), "LAST 10 ENCOUNTERS (CREW)",
                LabelStyle(7, UiMute, bold: true));
            sy += 11f;

            if (_socialDevHistory.Count == 0)
            {
                GUI.Label(new Rect(sx, sy, sw, 10f), "(none yet)", LabelStyle(6, UiDim));
                sy += 12f;
            }
            else
            {
                for (int i = _socialDevHistory.Count - 1; i >= 0; i--)
                {
                    var s = _socialDevHistory[i];
                    var log = s.Log;
                    if (log == null) continue;
                    string a = FindCrewWorker(log.InitiatorId)?.DisplayName ?? $"#{log.InitiatorId}";
                    string b = FindCrewWorker(log.TargetId)?.DisplayName ?? $"#{log.TargetId}";
                    string dlg = s.Presented ? "say" : (s.SuppressedDistance ? "dist" : (s.SuppressedSleep ? "sleep" : "mute"));
                    GUI.Label(new Rect(sx, sy, sw, 10f),
                        $"D{s.Day} {FormatHourClock(s.GameHour)}  {a}→{b}  {log.Action}/{(log.ActionSuccess ? "OK" : "X")}  {dlg}",
                        LabelStyle(6, s.Presented ? UiGreen : UiDim));
                    sy += 10f;
                }
            }

            GUI.EndScrollView();
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
            float ph = 22f + 5 * rowH + 10f;
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
            // Worker summary + 5 compact providers + stats
            float ph = 72f + 5f * 96f + 18f + nStats * 12f + 20f;
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
            const float bh = 58f;
            var r = new Rect(x, y, bw, bh);

            // Keep speech boxes clear of the right tool strip + open DEV popup.
            float rightLimit = Screen.width - HudToolStripReserve - 16f;
            if (_hudPopup == HudPopupKind.Social)
                rightLimit -= 348f;
            else if (_hudPopup != HudPopupKind.None)
                rightLimit -= 300f;
            if (r.xMax > rightLimit)
                r.x = Mathf.Max(10f, rightLimit - r.width);

            DrawCyberPanel(r, lit: false, accentOverride: accent);
            Block(r);

            var body = LabelStyle(11, new Color(accent.r, accent.g, accent.b, 0.92f));
            body.wordWrap = true;
            GUI.Label(new Rect(r.x + 8f, r.y + 5f, r.width - 16f, 14f),
                speech.DisplayName.ToUpperInvariant(),
                LabelStyle(9, new Color(accent.r, accent.g, accent.b, 0.8f), bold: true));
            GUI.Label(new Rect(r.x + 8f, r.y + 20f, r.width - 16f, 34f), speech.Text, body);
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
            const int s = 24;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, new Color(0, 0, 0, 0));
            for (int y = 2; y < 16; y++)
            for (int x = 10; x <= 13; x++)
                tex.SetPixel(x, y, new Color(0.35f, 1f, 0.55f));
            for (int i = 0; i < 7; i++)
            for (int x = 12 - i; x <= 12 + i; x++)
                if (x >= 0 && x < s) tex.SetPixel(x, 20 - i, new Color(0.35f, 1f, 0.55f));
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
                return;
            }
            Build();
            // Build ends in HeadingOut — audit operating invariants need OnShift
            EnterOnShift();
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
