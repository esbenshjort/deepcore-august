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
        readonly ExcavatedPathfinder[] _crewNav = new ExcavatedPathfinder[5];
        readonly float[] _crewBodyR = { 0.22f, 0.12f, 0.14f, 0.12f, 0.12f };
        readonly WorkerBanter _banter = new();
        enum ControlWorker : byte { Prospector = 0, Excavator = 1, Hauler = 2, Refiner = 3, Engineer = 4 }
        ControlWorker _control = ControlWorker.Prospector;

        // ——— Day / shift cycle (24h clock, shift 08:00–18:00) ———
        enum CrewPhase : byte { OnShift = 0, HeadingHome = 1, Asleep = 2, HeadingOut = 3 }
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
        Vector2 _workExcavator;
        Vector2 _workProspector;
        Vector2 _workHauler;
        Vector2 _workRefiner;
        Vector2 _workEngineer;
        /// <summary>Where the excavator left the dig face at whistle — next shift walks back here.</summary>
        Vector2 _excavatorDigResume;
        bool _hasExcavatorDigResume;
        bool[] _arrived = { false, false, false, false, false };
        bool[] _crewStranded = { false, false, false, false, false };
        /// <summary>Subtle per-crew lateral path stagger (world units).</summary>
        readonly float[] _crewLateral = { -0.035f, 0.04f, -0.02f, 0.03f, 0.015f };
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
                return SnapPostToTunnel(door, _crewBodyR[0]);
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
            _gasFx.PocketBreached += () => _banter.TrySay(WorkerBanter.Voice.Excavator,
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

            RefreshWorkPosts();

            // First whistle — crew emerges from the tent
            BeginHeadingOut(announce: false);
            _prospector.BindExcavator(_worker);
            _prospector.BindRefiner(_refiner);
            _prospector.ScanStarted += () => _banter.TrySay(WorkerBanter.Voice.Prospector,
                "Radar live. Sweeping the dark.",
                "Listening to the rock…",
                "Cone up. Let's read the mountain.");
            _prospector.GoldHintFound += () => _banter.TrySay(WorkerBanter.Voice.Prospector,
                "Soft amber return — possible vein.",
                "Gold whisper on the scope. Mark it.",
                "That's not noise. That's money.",
                "Faint yellow. Don't lose the bearing.");
            _prospector.BedrockHintFound += () => _banter.TrySay(WorkerBanter.Voice.Prospector,
                "Hard mass ahead — route around if you can.",
                "Bedrock lobe on scan. Plan the cut.",
                "Solid wall signature. Find the seam.",
                "Cyan plate. Excavator's going to hate that.");
            _prospector.GasHintFound += () => _banter.TrySay(WorkerBanter.Voice.Prospector,
                "Purple void on the scope — sealed pocket.",
                "Gas signature. Don't punch that blind.",
                "Hollow return. Air's wrong in there.",
                "Pressure pocket. Mark it and dig careful.");
            _prospector.SurveyWhisper += () => _banter.TrySay(WorkerBanter.Voice.Prospector,
                "Studying the wall… maybe something.",
                "Could be gold. Could be wishful thinking.",
                "Grain looks promising. No guarantees.",
                "Quiet return. Don't bet the trip on it.",
                "Nothing clean — still poking around.");
            _prospector.AssistNote += () => _banter.TrySay(WorkerBanter.Voice.Prospector,
                "Reading the face for you.",
                "Seam mapped — dig should bite cleaner.",
                "Rock grain noted. Don't thank me yet.",
                "Behind you. Softening the bite.");
            _prospector.InvestigationFinding += () =>
            {
                _banter.TrySay(WorkerBanter.Voice.Prospector,
                    "Boss. I've got something. Check the Tactical View.",
                    "New findings, boss. Tactical View — look.",
                    "Got something. Worth a look on Tactical.");
            };

            _hauler.PickedGold += () => _banter.TrySay(WorkerBanter.Voice.Hauler,
                "Gold in the cart. Easy does it.",
                "Yellow load — this trip pays.",
                "Careful on the corners. Precious cargo.");
            _hauler.PickedRock += () => _banter.TrySay(WorkerBanter.Voice.Hauler,
                "Another rock. Cart's gettin' heavy.",
                "Fillin' up on stone. Base wants it anyway.");
            _hauler.Deposited += () => _banter.TrySay(WorkerBanter.Voice.Hauler,
                "Dropped at base. Back into the hole.",
                "Stockpile fed. Round trip done.",
                "Unload complete. Seekin' the next pile.");

            _refiner.StartedWash += () => _banter.TrySay(WorkerBanter.Voice.Refiner,
                "Cell in the drum. Splitting sockets…",
                "Washer spinning. Let's see what she holds.",
                "One cell at a time — no shortcuts.");
            _refiner.FoundGold += () => _banter.TrySay(WorkerBanter.Voice.Refiner,
                "Yellow in the rinse!",
                "Socket paid out. Into the clean pile.",
                "That's a keeper.");
            _refiner.FoundDiamond += () => _banter.TrySay(WorkerBanter.Voice.Refiner,
                "Ice! Diamond socket — clean as glass.",
                "Crystal in the wash. That's the money.",
                "Hard sparkle. Refined diamond out.");
            _refiner.BatchDone += () => _banter.TrySay(WorkerBanter.Voice.Refiner,
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
                _banter.TrySay(WorkerBanter.Voice.Excavator,
                    "Nailed it.",
                    "Weak Point!",
                    "Got this.",
                    "There — soft spot.",
                    "Crack opens. Push it.");
            };
            _balance.Bind(_worker);
        }

        void OnDigImpact(TerrainCell before, bool broke)
        {
            if (before.DiamondCount > 0 && broke)
            {
                _banter.TrySay(WorkerBanter.Voice.Excavator,
                    "Diamonds! Catch that shimmer — haul it!",
                    "Ice in the rock. Real stones!",
                    "Crystal flash — don't lose that pile.",
                    "Hard glitter. That's a diamond pocket.");
                return;
            }
            if (before.GoldCount > 0 && broke)
            {
                _banter.TrySay(WorkerBanter.Voice.Excavator,
                    "Paydirt! That's the real stuff.",
                    "Gold in the teeth of the bit — haul it!",
                    "There she is. Yellow as sin.",
                    "Struck a pocket. Don't blink.");
                return;
            }
            if (before.BedrockCount >= 2)
            {
                if (broke)
                    _banter.TrySay(WorkerBanter.Voice.Excavator,
                        "Finally chewed through that plate.",
                        "Bedrock's done. Took its sweet time.",
                        "Hard stuff cracked. Moving on.");
                else
                    _banter.TrySay(WorkerBanter.Voice.Excavator,
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
                    else if (_control == ControlWorker.Excavator)
                        _worker.ClearRoute();
                }
                if (kb.backspaceKey.wasPressedThisFrame && _control == ControlWorker.Excavator)
                    _worker.UndoLastPin();
                if (_control == ControlWorker.Excavator &&
                    (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                    _worker.AddPin(_worker.Position, replaceRoute: false);
                if (kb.rKey.wasPressedThisFrame) ResetMap();
                if (kb.bKey.wasPressedThisFrame) _showBalanceHarness = !_showBalanceHarness;
                if (kb.lKey.wasPressedThisFrame) TryPlaceLantern();
                if (kb.tKey.wasPressedThisFrame) _tactical?.Toggle();
                if (kb.tabKey.wasPressedThisFrame) CycleControl(+1);
                if (kb.uKey.wasPressedThisFrame) _playerTactical?.Toggle();
                if (kb.hKey.wasPressedThisFrame
                    && _playerTactical != null && _playerTactical.Visible)
                    _scanHistoryBrowserOpen = !_scanHistoryBrowserOpen;
                if (kb.nKey.wasPressedThisFrame) SkipSleep();
                if (kb.yKey.wasPressedThisFrame && _control == ControlWorker.Prospector)
                {
                    if (_scannerPlanMode) ExitScannerPlanMode();
                    ToggleScannerPlacementMode();
                }
                if (kb.cKey.wasPressedThisFrame && _control == ControlWorker.Prospector)
                    ToggleScannerPlanMode();
                if (kb.equalsKey.wasPressedThisFrame)
                {
                    if (_fieldScanner != null && _fieldScanner.State == ProspectorScannerState.Scanning)
                        _fieldScanner.DebugForceCompleteScan(_absoluteGameHours);
                    else
                        _prospector?.DebugForceFinishScannerSetup();
                }
                if (_prospector != null)
                {
                    bool boost = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
                    if (_prospector.IsSettingUpScanner)
                        _prospector.SetDebugSetupSpeedMul(boost ? 20f : 1f);
                    if (_fieldScanner != null && _fieldScanner.State == ProspectorScannerState.Scanning)
                        _prospector.SetDebugScanSpeedMul(boost ? 120f : 12f);
                    else
                        _prospector.SetDebugScanSpeedMul(1f);
                }
                if (kb.digit1Key.wasPressedThisFrame && !_scannerPlaceMode)
                {
                    _prospector?.SetDistance(ScanDistance.Short);
                    if (_scannerPlanMode) ApplyPlanPresetsFromProspector();
                }
                if (kb.digit2Key.wasPressedThisFrame && !_scannerPlaceMode)
                {
                    _prospector?.SetDistance(ScanDistance.Medium);
                    if (_scannerPlanMode) ApplyPlanPresetsFromProspector();
                }
                if (kb.digit3Key.wasPressedThisFrame && !_scannerPlaceMode)
                {
                    _prospector?.SetDistance(ScanDistance.Long);
                    if (_scannerPlanMode) ApplyPlanPresetsFromProspector();
                }
                if (kb.qKey.wasPressedThisFrame)
                {
                    if (_scannerPlaceMode) RotateScannerPlacement(-15f);
                    else
                    {
                        _prospector?.SetWidth(ScanWidth.Narrow);
                        if (_scannerPlanMode) ApplyPlanPresetsFromProspector();
                    }
                }
                if (kb.eKey.wasPressedThisFrame)
                {
                    if (_scannerPlaceMode) RotateScannerPlacement(+15f);
                    else
                    {
                        _prospector?.SetWidth(ScanWidth.Wide);
                        if (_scannerPlanMode) ApplyPlanPresetsFromProspector();
                    }
                }
                if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                    && _scannerPlanMode && _control == ControlWorker.Prospector)
                    ConfirmScannerPlan();
                if (_control == ControlWorker.Prospector && _prospector != null && _crewPhase == CrewPhase.OnShift
                    && !_scannerPlaceMode && !_scannerPlanMode)
                {
                    if (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame)
                        _prospector.SetRadar(false);
                    else if (kb.spaceKey.wasPressedThisFrame && !_prospector.HasScannerAssignment)
                    {
                        // Radar cone preview only — legacy SPACE sweep scan removed
                        if (!_prospector.RadarOn)
                            _prospector.SetRadar(true);
                    }
                }
                if (kb.gKey.wasPressedThisFrame && _control == ControlWorker.Hauler)
                    _hauler?.TogglePreferGold();
                if (kb.gKey.wasPressedThisFrame && _control == ControlWorker.Refiner)
                    _refiner?.TogglePriority();
            }

            // Only run normal worker AI while on shift
            if (_crewPhase == CrewPhase.OnShift)
            {
                switch (_control)
                {
                    case ControlWorker.Prospector:
                        _worker.Tick(Vector2.zero);
                        _prospector?.SetHudVisible(true);
                        _prospector?.Tick(wasd, scanPulse);
                        _refiner?.Tick(Vector2.zero);
                        break;
                    case ControlWorker.Excavator:
                        _prospector?.SetHudVisible(false);
                        _worker.Tick(wasd);
                        _prospector?.Tick(Vector2.zero, false);
                        _refiner?.Tick(Vector2.zero);
                        break;
                    case ControlWorker.Hauler:
                        _worker.Tick(Vector2.zero);
                        _prospector?.SetHudVisible(false);
                        _prospector?.Tick(Vector2.zero, false);
                        _refiner?.Tick(Vector2.zero);
                        break;
                    case ControlWorker.Refiner:
                        _worker.Tick(Vector2.zero);
                        _prospector?.SetHudVisible(false);
                        _prospector?.Tick(Vector2.zero, false);
                        _refiner?.Tick(wasd);
                        break;
                    case ControlWorker.Engineer:
                        _worker.Tick(Vector2.zero);
                        _prospector?.SetHudVisible(false);
                        _prospector?.Tick(Vector2.zero, false);
                        _refiner?.Tick(Vector2.zero);
                        break;
                }
                _hauler?.Tick();
                float hoursNow = Time.deltaTime / SecondsPerGameHour;
                _hauler?.SetGameHours(_absoluteGameHours);
                _engineer?.Tick(hoursNow, _absoluteGameHours);
                if (_engineer != null)
                {
                    if (_engineer.IsEnRoute && !_engineerWasEnRoute)
                        _banter.TrySay(WorkerBanter.Voice.Engineer,
                            "She's redlined. I'm moving.",
                            "Heat spike — engineer en route.",
                            "Don't touch the bit. I've got it.");
                    if (_engineer.IsRepairing && !_engineerWasRepairing)
                        _banter.TrySay(WorkerBanter.Voice.Engineer,
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
            UpdateScannerPlacementGhost();
            FollowCamera();
        }

        void TickClock()
        {
            float prev = _gameHour;
            float hoursDelta = Time.deltaTime / SecondsPerGameHour;
            _gameHour += hoursDelta;
            _absoluteGameHours += hoursDelta;
            if (_gameHour >= 24f)
            {
                _gameHour -= 24f;
                _dayIndex++;
            }
            ApplyDayNightLight();

            _scanHistory?.Findings.SetGameHours(_absoluteGameHours);

            _logisticsTraffic?.SetGameHours(_absoluteGameHours);
            _logisticsTraffic?.TickDecay(hoursDelta);
            _hauler?.SetGameHours(_absoluteGameHours);

            // Prospector scanner setup advances on game time (not a separate real-time timer)
            _prospector?.TickScannerGameTime(hoursDelta);
            _prospector?.TickInvestigationGameTime(hoursDelta, _absoluteGameHours);

            // Stage-2 heavy scan: game-time progression (local Alt multiplier does not change clock)
            if (_fieldScanner != null && _fieldScanner.State == ProspectorScannerState.Scanning)
            {
                float mul = _prospector != null ? _prospector.DebugScanSpeedMul : 1f;
                _fieldScanner.TickScanGameHours(hoursDelta * mul, _absoluteGameHours);
            }

            // Stage-4 timed analysis — 1× game clock by default.
            // Pauses while Prospector is away on field/refiner trips (desk work only).
            // Alt only: optional debug accel (does not apply scan's 12× always-on mul).
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
                BeginHeadingOut(announce: true);

            // Sleep recovery: Frustration −2 / real second while crew is asleep
            if (_crewPhase == CrewPhase.Asleep)
                _worker?.TickRestFrustrationRelief(Time.deltaTime);
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
            if (!_fieldScanner.TryBeginScan(
                    _scanHistory, _prospector.Stats, "Prospector", _absoluteGameHours,
                    prospectorId: "prospector",
                    prospectorProfile: _sheetProfile[(int)ControlWorker.Prospector],
                    prospectorProfileLabel: WorkerStatProfiles.Label(
                        (byte)ControlWorker.Prospector,
                        _sheetProfile[(int)ControlWorker.Prospector])))
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
            _prospector.AssignScannerSetup(_fieldScanner);
            DigHoodLog.Push(
                $"SCANNER | PLACED PACKED | pos {pos.x:0.00},{pos.y:0.00} | facing {facing.x:0.00},{facing.y:0.00}");
            Debug.Log($"[SCANNER] Placed PACKED at ({pos.x:0.00},{pos.y:0.00}) — Prospector traveling to set up");
        }

        void TickCrewPhase()
        {
            if (_sleepCamp == null || _world == null) return;
            EnsureCrewNav();
            if (_crewPhase == CrewPhase.HeadingHome)
            {
                _commuteTimer += Time.deltaTime;
                Vector2 door = CampNavDestination;
                // Walk radii only — excavator dig footprint is too fat for corridors
                bool all = true;
                all &= StepCrewHomeOrOut(0, _worker != null ? _worker.transform : null, door, _crewBodyR[0]);
                all &= StepCrewHomeOrOut(1, _prospector != null ? _prospector.transform : null, door + new Vector2(-0.15f, 0.12f), _crewBodyR[1]);
                all &= StepCrewHomeOrOut(2, _hauler != null ? _hauler.transform : null, door + new Vector2(0.2f, -0.1f), _crewBodyR[2]);
                all &= StepCrewHomeOrOut(3, _refiner != null ? _refiner.transform : null, door + new Vector2(-0.05f, -0.2f), _crewBodyR[3]);
                all &= StepCrewHomeOrOut(4, _engineer != null ? _engineer.transform : null, door + new Vector2(0.15f, 0.2f), _crewBodyR[4]);
                bool anyStranded = false;
                for (int i = 0; i < _crewStranded.Length; i++)
                    if (_crewStranded[i]) anyStranded = true;
                // Never teleport stranded workers through rock; timeout only sleeps if everyone can arrive.
                if (all) EnterSleep();
                else if (_commuteTimer >= CommuteTimeoutSec && !anyStranded) EnterSleep();
            }
            else if (_crewPhase == CrewPhase.HeadingOut)
            {
                _commuteTimer += Time.deltaTime;
                bool all = true;
                all &= StepCrewHomeOrOut(0, _worker != null ? _worker.transform : null, _workExcavator, _crewBodyR[0]);
                all &= StepCrewHomeOrOut(1, _prospector != null ? _prospector.transform : null, _workProspector, _crewBodyR[1]);
                all &= StepCrewHomeOrOut(2, _hauler != null ? _hauler.transform : null, _workHauler, _crewBodyR[2]);
                all &= StepCrewHomeOrOut(3, _refiner != null ? _refiner.transform : null, _workRefiner, _crewBodyR[3]);
                all &= StepCrewHomeOrOut(4, _engineer != null ? _engineer.transform : null, _workEngineer, _crewBodyR[4]);
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

        void RefreshWorkPosts()
        {
            // Excavator returns to last dig face when one was saved; otherwise camp start pad.
            if (_hasExcavatorDigResume)
                _workExcavator = SnapPostToTunnel(_excavatorDigResume, _crewBodyR[0]);
            else
                _workExcavator = SnapPostToTunnel(_world.CellCenter(StartX, StartY), _crewBodyR[0]);

            // Prospector: resume investigation desk / trip stand if still investigating
            if (_prospector != null && _prospector.WorkMode == ProspectorWorkMode.Investigate)
            {
                _workProspector = SnapPostToTunnel(
                    _prospector.Investigation.ResumeWorldPosition, _crewBodyR[1]);
            }
            else
                _workProspector = SnapPostToTunnel(_world.CellCenter(StartX - 4, StartY - 1), _crewBodyR[1]);

            _workHauler = SnapPostToTunnel(_yard != null ? _yard.DropPoint : BasecampPos, _crewBodyR[2]);
            _workRefiner = SnapPostToTunnel(_refiner != null ? _refiner.WorkPoint : BasecampPos, _crewBodyR[3]);
            _workEngineer = SnapPostToTunnel(_yard != null ? _yard.DropPoint + new Vector2(0.55f, 0.35f) : BasecampPos, _crewBodyR[4]);
        }

        void EnsureCrewNav()
        {
            TunnelNavGrid.DebugLog = navDebugLog;
            TunnelPathfinder.DebugLog = navDebugLog;
            TunnelPathfinder.DebugDraw = navDebugDraw;
            for (int i = 0; i < _crewNav.Length; i++)
            {
                if (_crewNav[i] == null)
                {
                    _crewNav[i] = new ExcavatedPathfinder(_world, _crewBodyR[i]);
                    _crewNav[i].LateralOffset = _crewLateral[i];
                }
                _crewNav[i].SetAgentRadius(_crewBodyR[i]);
            }
        }

        bool StepCrewHomeOrOut(int idx, Transform t, Vector2 target, float bodyR)
        {
            if (t == null) { _arrived[idx] = true; return true; }
            if (_arrived[idx]) return true;
            if (_crewStranded[idx]) return false;

            Vector2 p = t.localPosition;
            float arrive = Mathf.Max(0.1f, bodyR * 0.85f);
            if ((target - p).sqrMagnitude <= arrive * arrive)
            {
                t.localPosition = target;
                _arrived[idx] = true;
                _crewNav[idx]?.Invalidate();
                return true;
            }

            // Snap onto nearest open tunnel if somehow stuck in rock
            var cell = _world.WorldToCell(p);
            if (!_world.IsTunnelOpen(cell.x, cell.y))
            {
                if (TrySnapToNearestTunnel(ref p, bodyR))
                    t.localPosition = p;
            }

            float speed = TravelSpeed * LoosePile.SpeedMulAt(p, bodyR);
            bool done = _crewNav[idx].Follow(
                p,
                target,
                speed,
                bodyR,
                face: dir =>
                {
                    if (t == _worker?.transform && dir.sqrMagnitude > 0.0001f)
                        t.up = dir;
                },
                tryStep: (dir, step) => CrewTryStep(t, dir, step, bodyR));

            if (_crewNav[idx] != null && _crewNav[idx].Stranded)
            {
                _crewStranded[idx] = true;
                DigHoodLog.Push($"CREW {idx} | STRANDED — no path to destination");
                return false;
            }

            if (done)
            {
                t.localPosition = target;
                _arrived[idx] = true;
                _crewNav[idx]?.Invalidate();
            }
            return _arrived[idx];
        }

        bool CrewTryStep(Transform t, Vector2 dir, float step, float bodyR)
        {
            if (t == null || dir.sqrMagnitude < 0.00001f) return false;
            Vector2 next = (Vector2)t.localPosition + dir.normalized * step;
            if (_world.CircleHitsSolid(next, bodyR * 0.85f)) return false;
            t.localPosition = next;
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
            for (int i = 0; i < _arrived.Length; i++)
            {
                _arrived[i] = false;
                _crewStranded[i] = false;
            }
            EnsureCrewNav();
            for (int i = 0; i < _crewNav.Length; i++)
            {
                if (_crewNav[i] == null) continue;
                _crewNav[i].CampReturnMode = true;
                _crewNav[i].Invalidate();
            }
            SetAllCrewVisible(true);
            // Remember dig face + keep route pins overnight (do not ClearRoute).
            if (_worker != null)
            {
                _excavatorDigResume = SnapPostToTunnel(
                    _worker.CaptureShiftBreakBookmark(), _crewBodyR[0]);
                _hasExcavatorDigResume = true;
                DigHoodLog.Push(
                    $"SHIFT BREAK | Resume @ {_excavatorDigResume.x:0.0},{_excavatorDigResume.y:0.0}");
            }
            _prospector?.SetRadar(false);
            _banter.TrySay(WorkerBanter.Voice.Excavator,
                "Whistle's blown. Back to the tent.",
                "Shift's done. Firepit's calling.",
                "Knock off — see you at 08.");
            _banter.TrySay(WorkerBanter.Voice.Hauler,
                "Cart parked. Heading to camp.",
                "That's a wrap. Boots off soon.");
        }

        void EnterSleep()
        {
            _crewPhase = CrewPhase.Asleep;
            SetAllCrewVisible(false);
            if (_sleepCamp != null)
            {
                _worker?.TeleportTo(_sleepCamp.TentDoor);
                _prospector?.TeleportTo(_sleepCamp.TentDoor);
                _hauler?.TeleportTo(_sleepCamp.TentDoor);
                _refiner?.TeleportTo(_sleepCamp.TentDoor);
                _engineer?.TeleportTo(_sleepCamp.TentDoor);
            }
            // Overnight rest fully cools the excavator machine.
            _worker?.ResetHeatAfterRest();
            _banter.TrySay(WorkerBanter.Voice.Prospector,
                "Lights out. Dreaming of veins.",
                "Tent's warm. See you at dawn.");
        }

        void BeginHeadingOut(bool announce)
        {
            _crewPhase = CrewPhase.HeadingOut;
            _commuteTimer = 0f;
            RefreshWorkPosts();
            for (int i = 0; i < _arrived.Length; i++)
            {
                _arrived[i] = false;
                _crewStranded[i] = false;
            }
            EnsureCrewNav();
            for (int i = 0; i < _crewNav.Length; i++)
            {
                if (_crewNav[i] == null) continue;
                _crewNav[i].CampReturnMode = false;
                _crewNav[i].Invalidate();
            }
            Vector2 door = _sleepCamp != null ? _sleepCamp.TentDoor : BasecampPos;
            // Door may sit near pad edge — snap onto open floor before walking
            door = SnapPostToTunnel(door, _crewBodyR[1]);
            // Stagger slightly outside the flap
            _worker?.TeleportTo(door + new Vector2(0.1f, -0.15f));
            _prospector?.TeleportTo(door + new Vector2(-0.2f, 0.05f));
            _hauler?.TeleportTo(door + new Vector2(0.25f, 0.1f));
            _refiner?.TeleportTo(door + new Vector2(-0.05f, -0.25f));
            _engineer?.TeleportTo(door + new Vector2(0.3f, 0.15f));
            SetAllCrewVisible(true);
            if (announce)
            {
                _banter.TrySay(WorkerBanter.Voice.Excavator,
                    "08:00. Bits warm — let's dig.",
                    "Morning whistle. Out of the tent.",
                    "New shift. Mountain's waiting.");
            }
        }

        void EnterOnShift()
        {
            _crewPhase = CrewPhase.OnShift;
            _commuteTimer = 0f;
            RefreshWorkPosts();
            SetAllCrewVisible(true);
            // Snap exactly onto posts (excavator → saved dig face when available)
            _worker?.TeleportTo(_workExcavator);
            _prospector?.TeleportTo(_workProspector);
            _hauler?.TeleportTo(_workHauler);
            _refiner?.TeleportTo(_workRefiner);
            _engineer?.TeleportTo(_workEngineer);
            if (_hasExcavatorDigResume && _worker != null && _worker.RouteCount > 0)
                DigHoodLog.Push(
                    $"SHIFT START | Back at dig face | Route {_worker.RouteCount} pin{(_worker.RouteCount == 1 ? "" : "s")}");
            if (_prospector != null && _prospector.WorkMode == ProspectorWorkMode.Investigate)
                DigHoodLog.Push($"SHIFT START | Prospector resumes {_prospector.Investigation.PlayerWorkLabel}");
            if (_refiner != null && _refiner.IsInConsultation)
                DigHoodLog.Push("SHIFT START | Refiner resumes consult");
            if (_engineer != null && _engineer.IsInfrastructureWork)
                DigHoodLog.Push($"SHIFT START | Engineer resumes {_engineer.WorkLabel}");
        }

        /// <summary>Fast-forward night — jump to next 08:00 and walk out (or skip walk).</summary>
        public void SkipSleep()
        {
            if (_crewPhase == CrewPhase.OnShift || _crewPhase == CrewPhase.HeadingOut)
                return;

            // Remaining night as real seconds → Frustration sleep recovery (does not wipe instantly).
            float hoursLeft = HoursUntilMorning(_gameHour);
            _worker?.ApplyRestFrustrationForDuration(hoursLeft * SecondsPerGameHour);

            if (_gameHour >= ShiftStartHour)
                _dayIndex++;
            _gameHour = ShiftStartHour;
            // Skipped night still counts as a full rest for machine heat.
            _worker?.ResetHeatAfterRest();
            BeginHeadingOut(announce: true);
            _banter.TrySay(WorkerBanter.Voice.Hauler,
                "Skipped the snore. Coffee and cart.",
                "Fast-forward. Boots back on.");
            ApplyDayNightLight();
        }

        /// <summary>Game hours from <paramref name="hour"/> until next shift start (08:00).</summary>
        static float HoursUntilMorning(float hour)
        {
            if (hour < ShiftStartHour)
                return ShiftStartHour - hour;
            return (24f - hour) + ShiftStartHour;
        }

        void SetAllCrewVisible(bool on)
        {
            _worker?.SetCrewVisible(on);
            _prospector?.SetCrewVisible(on);
            _hauler?.SetCrewVisible(on);
            _refiner?.SetCrewVisible(on);
            _engineer?.SetCrewVisible(on);
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

        void DiscoverGasNearCrew()
        {
            if (_world == null) return;
            if (_worker != null) _world.DiscoverGasAround(_worker.Position);
            if (_prospector != null) _world.DiscoverGasAround(_prospector.Position);
            if (_hauler != null) _world.DiscoverGasAround(_hauler.Position);
            if (_refiner != null) _world.DiscoverGasAround(_refiner.Position);
        }

        void SyncRoutePinsToScanView()
        {
            // Dig route no longer gated on retired Scan View
            _worker?.SetRouteVisible(_control == ControlWorker.Excavator);
        }

        void LateUpdate()
        {
            // After OnGUI so HUD clicks never become dig goals
            HandleMouse();
        }

        void CycleControl(int delta)
        {
            int n = ((int)_control + delta) % 5;
            if (n < 0) n += 5;
            SelectWorker((ControlWorker)n);
        }

        void SelectWorker(ControlWorker w)
        {
            // Keep excavator dig goal / pinpoint when switching workers
            _control = w;
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
                if (mouse.leftButton.wasPressedThisFrame)
                    ConfirmScannerPlacement();
                return;
            }

            if (_scannerPlanMode)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                    ConfirmScannerPlan();
                return;
            }

            if (_control == ControlWorker.Prospector && _prospector != null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                    _prospector.FaceToward(world);
                return;
            }

            if (_control != ControlWorker.Excavator || _worker == null) return;

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
                Transform follow = _control switch
                {
                    ControlWorker.Prospector => _prospector != null ? _prospector.transform : null,
                    ControlWorker.Hauler => _hauler != null ? _hauler.transform : null,
                    ControlWorker.Refiner => _refiner != null ? _refiner.transform : null,
                    ControlWorker.Engineer => _engineer != null ? _engineer.transform : null,
                    _ => _worker != null ? _worker.transform : null,
                };
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
            _hoverPile = null;
            RefreshWorkPosts();
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

        /// <summary>Which crew sheet is open (−1 = none).</summary>
        int _openStatsSheet = -1;
        readonly WorkerStats[] _sheetBaseline = { new(), new(), new(), new(), new() };
        readonly bool[] _sheetBaselineCaptured = new bool[5];
        readonly WorkerSheetProfile[] _sheetProfile =
        {
            WorkerSheetProfile.Baseline,
            WorkerSheetProfile.Baseline,
            WorkerSheetProfile.Baseline,
            WorkerSheetProfile.Baseline,
            WorkerSheetProfile.Baseline,
        };

        void OnDrawGizmos()
        {
            if (!navDebugDraw || _world == null) return;
            for (int i = 0; i < _crewNav.Length; i++)
            {
                var eng = _crewNav[i]?.Engine;
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
            bool showScannerHud = _prospector != null && _control == ControlWorker.Prospector;
            bool scannerExpanded = showScannerHud && (_scannerPlanMode
                || (_fieldScanner != null && _fieldScanner.State == ProspectorScannerState.Scanning));
            float scannerPanelH = !showScannerHud ? 0f
                : _scannerPlaceMode ? 78f
                : scannerExpanded ? 118f
                : 88f;

            float leftColY = topBar.yMax + 8f;
            if (showScannerHud)
                DrawHeavyScannerHud(10f, leftColY, Mathf.Min(400f, barW), scannerPanelH);

            // ——— Left worker roster ———
            float cardW = 168f, cardH = 70f, cardGap = 6f;
            float leftStackH = showScannerHud ? scannerPanelH : 0f;
            float cx = 10f, cy = leftColY + leftStackH + (showScannerHud ? 8f : 0f);
            const float statsBtnW = 36f;
            float sheetDockX = cx + cardW + statsBtnW + 14f;
            bool sheetOpen = _openStatsSheet >= 0;
            float banterX = sheetOpen ? sheetDockX + 300f : cx + cardW + statsBtnW + 14f;

            string prosSub = _prospector == null
                ? "SCAN · RADAR"
                : _prospector.WorkMode == ProspectorWorkMode.Investigate
                    ? ShortInvestigationSub(_prospector)
                    : "SCAN · RADAR";
            DrawWorkerCard(new Rect(cx, cy, cardW, cardH), ControlWorker.Prospector,
                "PROSPECTOR", prosSub, cardTitle, cardSub);
            DrawRosterStatsButton(cx + cardW + 6f, cy, cardH, (int)ControlWorker.Prospector, UiCyan);
            DrawBanterBubble(banterX, cy, WorkerBanter.Voice.Prospector);

            string digSub = _worker != null && _worker.RouteCount > 0
                    ? $"ROUTE · {_worker.RouteCount} PIN{(_worker.RouteCount == 1 ? "" : "S")}"
                    : "LMB PIN · SHIFT GO";
            DrawWorkerCard(new Rect(cx, cy + cardH + cardGap, cardW, cardH), ControlWorker.Excavator,
                "EXCAVATOR", digSub, cardTitle, cardSub);
            DrawRosterStatsButton(cx + cardW + 6f, cy + cardH + cardGap, cardH, (int)ControlWorker.Excavator, UiAmber);
            DrawBanterBubble(banterX, cy + cardH + cardGap, WorkerBanter.Voice.Excavator);

            DrawWorkerCard(new Rect(cx, cy + (cardH + cardGap) * 2, cardW, cardH), ControlWorker.Hauler,
                "HAULER",
                _hauler != null && _hauler.PreferGold ? "PRIORITY // PRECIOUS" : "PRIORITY // MIXED",
                cardTitle, cardSub);
            DrawRosterStatsButton(cx + cardW + 6f, cy + (cardH + cardGap) * 2, cardH,
                (int)ControlWorker.Hauler, UiGreen);
            DrawBanterBubble(banterX, cy + (cardH + cardGap) * 2, WorkerBanter.Voice.Hauler);

            string refSub = _refiner == null ? "WASHER"
                : _refiner.IsInConsultation
                    ? (_refiner.IsDiscussing ? "CONSULT // TALKING" : "CONSULT // MEETING")
                : _refiner.Priority == RefinerPriority.DiamondOre ? "WASH // ORE DIA"
                : _refiner.Priority == RefinerPriority.GoldOre ? "WASH // ORE GOLD"
                : "WASH // ORE ROCK";
            Color refAccent = new(0.7f, 0.55f, 1f);
            DrawWorkerCard(new Rect(cx, cy + (cardH + cardGap) * 3, cardW, cardH), ControlWorker.Refiner,
                "REFINER", refSub, cardTitle, cardSub);
            DrawRosterStatsButton(cx + cardW + 6f, cy + (cardH + cardGap) * 3, cardH,
                (int)ControlWorker.Refiner, refAccent);
            DrawBanterBubble(banterX, cy + (cardH + cardGap) * 3, WorkerBanter.Voice.Refiner);

            string engSub = _engineer == null ? "STANDBY" : _engineer.WorkLabel;
            Color engAccent = new(1f, 0.55f, 0.22f);
            DrawWorkerCard(new Rect(cx, cy + (cardH + cardGap) * 4, cardW, cardH), ControlWorker.Engineer,
                "ENGINEER", engSub, cardTitle, cardSub);
            DrawRosterStatsButton(cx + cardW + 6f, cy + (cardH + cardGap) * 4, cardH,
                (int)ControlWorker.Engineer, engAccent);
            DrawBanterBubble(banterX, cy + (cardH + cardGap) * 4, WorkerBanter.Voice.Engineer);

            if (sheetOpen)
                DrawWorkerStatsSheet(sheetDockX, cy, _openStatsSheet);

            float rosterExtraY = cy + (cardH + cardGap) * 5 + 4f;

            if (_control == ControlWorker.Hauler && _hauler != null)
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

            if (_control == ControlWorker.Engineer && _engineer != null)
            {
                GUI.Label(new Rect(cx, rosterExtraY, cardW, 18f),
                    _engineer.DebugStatus,
                    LabelStyle(11, UiAmber));
                rosterExtraY += 18f;
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

            if (_control == ControlWorker.Refiner && _refiner != null)
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
            // (drawn above via DrawWorkerStatsSheet when _openStatsSheet is set)

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

            DrawScanHistoryBrowser();
            DrawHistoricalTacticalBanner();
            DrawScanHistoryDevReadout();
            DrawKeybindingsPanel();
            DrawDigHoodLog();
            DrawFindingToast();
            DrawProspectorThinkingPanel();
            DrawProspectorDevActivityBox();
            DrawBalanceHarnessPanel();
            DrawAnomalyHoverTooltip();
        }

        void DrawBalanceHarnessPanel()
        {
            if (!_showBalanceHarness || _worker == null) return;

            const float panelW = 320f;
            const float panelH = 520f;
            float top = 96f;
            float maxH = Mathf.Max(280f, Screen.height - top - 14f);
            float useH = Mathf.Min(panelH, maxH);
            var panel = new Rect(Screen.width - panelW - 12f, top, panelW, useH);
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
                _showBalanceHarness = false;
        }

        void DrawRosterStatsButton(float x, float y, float cardH, int roleIndex, Color accent)
        {
            var r = new Rect(x, y + (cardH - 28f) * 0.5f, 36f, 28f);
            Block(r);
            bool on = _openStatsSheet == roleIndex;
            if (DrawCyberButton(r, "ST", selected: on, accent: accent))
                _openStatsSheet = on ? -1 : roleIndex;
        }

        WorkerStats GetWorkerStats(int roleIndex) => roleIndex switch
        {
            0 => _prospector != null ? _prospector.Stats : null,
            1 => _worker != null ? _worker.Stats : null,
            2 => _hauler != null ? _hauler.Stats : null,
            3 => _refiner != null ? _refiner.Stats : null,
            4 => _engineer != null ? _engineer.Stats : null,
            _ => null,
        };

        void EnsureSheetBaseline(int roleIndex)
        {
            if ((uint)roleIndex >= 5) return;
            if (_sheetBaselineCaptured[roleIndex]) return;
            var live = GetWorkerStats(roleIndex);
            if (live == null) return;
            _sheetBaseline[roleIndex].CopyFrom(live);
            _sheetBaselineCaptured[roleIndex] = true;
        }

        void ApplySheetProfile(int roleIndex, WorkerSheetProfile profile)
        {
            if ((uint)roleIndex >= 5) return;
            EnsureSheetBaseline(roleIndex);
            _sheetProfile[roleIndex] = profile;

            if (roleIndex == 1 && _worker != null)
            {
                _balance.ApplyProfile(WorkerStatProfiles.ToExcavator(profile));
                return;
            }

            var live = GetWorkerStats(roleIndex);
            if (live == null) return;
            if (profile == WorkerSheetProfile.Baseline)
                live.CopyFrom(_sheetBaseline[roleIndex]);
            else
                live.CopyFrom(WorkerStatProfiles.Build((byte)roleIndex, profile));
        }

        void DrawWorkerStatsSheet(float px, float py, int roleIndex)
        {
            var stats = GetWorkerStats(roleIndex);
            if (stats == null) return;
            EnsureSheetBaseline(roleIndex);

            // Keep excavator sheet profile in sync with balance harness when open
            if (roleIndex == 1)
                _sheetProfile[roleIndex] = WorkerStatProfiles.FromExcavator(_balance.Profile);

            Color accent = roleIndex switch
            {
                0 => UiCyan,
                1 => UiAmber,
                2 => UiGreen,
                3 => new Color(0.7f, 0.55f, 1f),
                4 => new Color(1f, 0.55f, 0.22f),
                _ => UiCyan,
            };

            const float panelW = 300f;
            float maxBottom = Screen.height - 12f;
            float digHoodTop = Screen.height - 160f;
            float panelH = Mathf.Clamp(digHoodTop - py - 8f, 440f, 680f);
            if (py + panelH > maxBottom)
                panelH = Mathf.Max(360f, maxBottom - py);

            var panel = new Rect(px, py, panelW, panelH);
            DrawCyberPanel(panel, lit: true, accentOverride: accent);
            Block(panel);

            var hdr = LabelStyle(10, accent, bold: true);
            var dim = LabelStyle(9, UiDim);
            var val = LabelStyle(11, UiWhite, bold: true);
            var pillar = LabelStyle(9, UiCyan, bold: true);
            var mute = LabelStyle(9, UiMute);
            var sec = LabelStyle(9, UiCyan, bold: true);

            float x0 = panel.x + 12f;
            float y = panel.y + 8f;
            float innerW = panelW - 24f;

            GUI.Label(new Rect(x0, y, innerW - 48f, 14f),
                $"{WorkerStatProfiles.RoleTitle((byte)roleIndex)} // STATS", hdr);
            y += 18f;
            DrawHLine(x0, y, innerW, new Color(accent.r, accent.g, accent.b, 0.28f));
            y += 8f;

            // ——— Profile picker ———
            GUI.Label(new Rect(x0, y, innerW, 12f), "PROFILE", sec);
            y += 14f;
            float btnW = (innerW - 10f) / 3f;
            float btnH = 22f;
            for (int i = 0; i < WorkerStatProfiles.ProfileCount; i++)
            {
                var p = (WorkerSheetProfile)i;
                int col = i % 3;
                int row = i / 3;
                var br = new Rect(x0 + col * (btnW + 5f), y + row * (btnH + 4f), btnW, btnH);
                Block(br);
                bool sel = _sheetProfile[roleIndex] == p;
                if (DrawCyberButton(br, WorkerStatProfiles.Label((byte)roleIndex, p),
                        selected: sel, accent: WorkerStatProfiles.Accent(p)))
                    ApplySheetProfile(roleIndex, p);
            }
            y += (btnH + 4f) * 2f + 6f;

            DrawHLine(x0, y, innerW, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.15f));
            y += 8f;

            // Excavator-only live machine / condition block
            if (roleIndex == 1 && _worker != null)
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

                DrawHLine(x0, y, innerW, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.15f));
                y += 8f;

                GUI.Label(new Rect(x0, y, innerW, 12f), "WORKER CONDITIONS", sec);
                y += 14f;

                float stamMax = Mathf.Max(1f, _worker.MaxStamina);
                float stam = _worker.CurrentStamina;
                string stamState = _worker.StaminaStateLabel;
                Color stamCol = stamState switch
                {
                    "RESTING" => UiCyan,
                    "EXHAUSTED" => new Color(1f, 0.3f, 0.28f, 1f),
                    "TIRED" => UiAmber,
                    _ => UiGreen,
                };
                DrawConditionRow(ref y, x0, innerW, "STAMINA",
                    $"{stam:0.#}/{stamMax:0.#}", stamState, stam / stamMax, stamCol, mute);

                float fr = _worker.Conditions.Frustration;
                Color frCol = fr >= 70f ? new Color(1f, 0.35f, 0.3f, 1f)
                    : fr >= 40f ? UiAmber
                    : UiDim;
                DrawConditionRow(ref y, x0, innerW, "FRUST.",
                    $"{fr:0.#}/100", fr >= 70f ? "HIGH" : fr >= 40f ? "RISING" : "LOW",
                    fr / 100f, frCol, mute);

                float inj = _worker.Conditions.Injury;
                Color injCol = inj >= FreeWorkerController.InjuryCareThreshold
                    ? new Color(1f, 0.28f, 0.28f, 1f)
                    : inj >= FreeWorkerController.InjurySlowThreshold
                        ? UiAmber
                        : UiDim;
                string injState = inj >= FreeWorkerController.InjuryCareThreshold
                    ? "NEEDS CARE"
                    : inj >= FreeWorkerController.InjurySlowThreshold
                        ? "HURT"
                        : "OK";
                DrawConditionRow(ref y, x0, innerW, "INJURY",
                    $"{inj:0.#}/100", injState, inj / 100f, injCol, mute);

                y += 4f;
                DrawHLine(x0, y, innerW, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.15f));
                y += 8f;
            }

            DrawStatPillar(ref y, x0, innerW, "BODY", WorkerStatId.RawPower, 10, stats, pillar, dim, val);
            y += 4f;
            DrawStatPillar(ref y, x0, innerW, "MIND", WorkerStatId.Calibration, 10, stats, pillar, dim, val);
            y += 4f;
            DrawStatPillar(ref y, x0, innerW, "SOUL", WorkerStatId.Composure, 10, stats, pillar, dim, val);

            var close = new Rect(panel.xMax - 54f, panel.y + 6f, 42f, 18f);
            if (DrawCyberButton(close, "×", selected: false, accent: accent))
                _openStatsSheet = -1;
        }

        void DrawConditionRow(ref float y, float x, float w, string label, string value,
            string state, float fill01, Color accent, GUIStyle mute)
        {
            GUI.Label(new Rect(x, y, 58f, 14f), label, mute);
            GUI.Label(new Rect(x + 58f, y, 88f, 14f), value, LabelStyle(10, UiWhite, bold: true));
            GUI.Label(new Rect(x + 148f, y, w - 148f, 14f), state,
                LabelStyle(9, accent, bold: true));
            y += 14f;

            // Thin technical bar
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
            GUIStyle pillar, GUIStyle dim, GUIStyle val)
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

                string name = StatShortName(id);
                int v = stats.Get(id);
                GUI.Label(new Rect(cx, rowY, colW - 36f, 14f), name, dim);
                GUI.Label(new Rect(cx + colW - 34f, rowY, 30f, 14f), v.ToString(), val);
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
            var r = new Rect(10f, Screen.height - panelH - 12f, panelW, panelH);
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
            const float panelH = 458f;
            var r = new Rect(Screen.width - panelW - 12f, Screen.height - panelH - 12f, panelW, panelH);
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

        static GUIStyle LabelStyle(int size, Color color, bool bold = false) =>
            new(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                normal = { textColor = color },
                richText = false,
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 2),
            };

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
            ShowFindingToast(string.IsNullOrEmpty(f.Message)
                ? $"PROSPECTOR: Boss, I've got something on {spoken}. Check Tactical."
                : f.Message);

            _banter.TrySay(WorkerBanter.Voice.Prospector,
                $"Boss, I've got something on {spoken}. Check Tactical.",
                $"Finding on {spoken}. Open Tactical.",
                $"Conclusion ready on {spoken} — look at Tactical.");
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
                "PROSPECTOR // FINDING", LabelStyle(10, UiAmber, bold: true));
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
                && _control != ControlWorker.Prospector)
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
            float px = Screen.width - tw - 12f;
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

        void DrawScanHistoryBrowser()
        {
            if (!_scanHistoryBrowserOpen) return;
            if (_playerTactical == null || !_playerTactical.Visible) return;
            if (_scanHistory == null) return;

            // Dock mid-left, clear of worker roster (~220px) and Dig Hood / CONTROLS.
            const float panelW = 300f;
            float panelX = 220f;
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
                    $"LOC ({rec.ScannerPosition.x:0.0},{rec.ScannerPosition.y:0.0})  ·  {facing}",
                    LabelStyle(8, UiDim));
                ty += 13f;
                GUI.Label(new Rect(lx, ty, tw, 12f),
                    $"RNG {rec.PlannedRangeCells:0.#}  ·  ±{rec.PlannedHalfAngleDeg:0.#}°  ·  " +
                    $"{rec.ProspectorName}/{rec.ProspectorProfileLabel}  ·  " +
                    $"ANOM {rec.SpatialSnapshots.Count}",
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

        void DrawBanterBubble(float x, float y, WorkerBanter.Voice voice)
        {
            string line = _banter.Get(voice);
            if (string.IsNullOrEmpty(line)) return;

            Color accent = voice switch
            {
                WorkerBanter.Voice.Prospector => UiCyan,
                WorkerBanter.Voice.Excavator => UiAmber,
                WorkerBanter.Voice.Refiner => new Color(0.7f, 0.55f, 1f),
                WorkerBanter.Voice.Engineer => new Color(1f, 0.55f, 0.22f),
                _ => UiGreen,
            };
            // Soft floating line — no panel / box
            var st = LabelStyle(11, new Color(accent.r, accent.g, accent.b, 0.72f));
            st.wordWrap = true;
            GUI.Label(new Rect(x + 4f, y + 18f, 260f, 40f), line, st);
        }

        void DrawWorkerCard(Rect r, ControlWorker worker, string title, string subtitle,
            GUIStyle titleStyle, GUIStyle subStyle)
        {
            bool on = _control == worker;
            Block(r);

            Color accent = worker switch
            {
                ControlWorker.Prospector => UiCyan,
                ControlWorker.Excavator => UiAmber,
                ControlWorker.Refiner => new Color(0.7f, 0.55f, 1f),
                ControlWorker.Engineer => new Color(1f, 0.55f, 0.22f),
                _ => UiGreen,
            };

            DrawCyberPanel(r, lit: on, accentOverride: on ? accent : default);
            // Selected marker — thin left rail
            var prev = GUI.color;
            GUI.color = on ? new Color(accent.r, accent.g, accent.b, 0.55f + 0.35f * _uiPulse) : new Color(accent.r, accent.g, accent.b, 0.2f);
            GUI.DrawTexture(new Rect(r.x + 6, r.y + 10, 2f, r.height - 20), Texture2D.whiteTexture);
            GUI.color = prev;

            var tStyle = new GUIStyle(titleStyle) { normal = { textColor = on ? accent : UiDim } };
            GUI.Label(new Rect(r.x + 16, r.y + 12, r.width - 24, 20), title, tStyle);
            GUI.Label(new Rect(r.x + 16, r.y + 34, r.width - 24, 16), subtitle, subStyle);
            if (on)
                GUI.Label(new Rect(r.x + 16, r.y + 50, r.width - 24, 14), "// SELECTED",
                    LabelStyle(9, new Color(accent.r, accent.g, accent.b, 0.65f)));

            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                SelectWorker(worker);
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
    }
}
