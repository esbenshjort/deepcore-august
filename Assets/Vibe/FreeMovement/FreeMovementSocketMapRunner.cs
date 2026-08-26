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
            // NOTE: RockWallShadows + multi-lantern Light2D shadows is too heavy in URP 2D
            // (per-cell casters × N lights). Revisit with merged casters / mask later.
            GoldVeinShine.Attach(_worldRoot, _world);
            _tactical = TacticalMapOverlay.Attach(_worldRoot, _world);
            _scanView = ScanViewOverlay.Attach(_worldRoot, _world);
            _gasFx = GasPocketFx.Attach(_worldRoot, _world);
            _gasFx.PocketBreached += () => _banter.TrySay(WorkerBanter.Voice.Excavator,
                "Gas pocket! Purple haze — vent it.",
                "Hollow chamber. Air's wrong in here.",
                "Broke into a gas void. Watch the bit.",
                "Purple fog. Pocket's open.");

            DigVisualKit.PlaceLantern(_lanternRoot, _world.CellCenter(StartX, StartY - 4), local: true, intensity: 2.6f);
            _lanternCount = 1;

            _calc = new DeliveryCalculator();
            _yard = BasecampYard.Spawn(_worldRoot, BasecampPos);
            _sleepCamp = CampSleepSite.Spawn(_worldRoot, BasecampPos);
            _calc.BindStockpiles(_yard.Rock, _yard.Gold, _yard.RefinedGold, _yard.Dirt);
            SpawnWorker(_worldRoot);
            _hauler = HaulerPerson.Spawn(_worldRoot, _world, _yard.DropPoint, _calc);
            _refiner = RefinerPerson.Spawn(_worldRoot, _world, _yard, _calc);
            _engineer = EngineerPerson.Spawn(_worldRoot, _world,
                _yard != null ? _yard.DropPoint + new Vector2(0.55f, 0.35f) : BasecampPos);
            _engineer.BindExcavator(_worker);
            _prospector = ProspectorPerson.Spawn(_worldRoot, _world,
                _world.CellCenter(StartX - 6, StartY - 2), _scanView);

            RefreshWorkPosts();

            // First whistle — crew emerges from the tent
            BeginHeadingOut(announce: false);
            _prospector.BindExcavator(_worker);
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
            _refiner.BatchDone += () => _banter.TrySay(WorkerBanter.Voice.Refiner,
                "Batch clear. Next cell.",
                "Dirt one way, gold the other.",
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

            // Larger start chamber + room for wash camp + tent
            int rx = 52, ry = 34;
            int floorY = StartY - 16;
            GoldVeinPlacer.ExcavateHalfOval(_world, StartX, floorY, rx, ry);
            GoldVeinPlacer.ExcavateHalfOval(_world, StartX, floorY + ry - 2, 12, 22);
            // Flat camp pad — washer yard + tent firepit
            GoldVeinPlacer.ExcavateHalfOval(_world, StartX, StartY - 12, 34, 18);

            // Organic bedrock veins + clumps (soft corridors remain diggable)
            GoldVeinPlacer.PlaceOrganicBedrock(_world, seed: 7701);

            // Organic gold — sparse near start, rich farther out
            GoldVeinPlacer.PlaceOrganicGold(_world, StartX, StartY, seed: 9102);

            // Sealed gas pockets — empty voids that vent purple when breached
            GoldVeinPlacer.PlaceGasPockets(_world, StartX, StartY, seed: 4404);
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

            var light = go.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(light,
                new Color(1f, 0.78f, 0.5f),
                intensity: 0.28f,
                outer: 1.25f,
                inner: 0.05f,
                shadows: false,
                falloff: 0.78f);

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
            // One loose piece per cell — same sockets/mass/gold as the wall cell
            LoosePile.SpawnCellFromDrill(_looseRoot, tip, _worker.Facing, c, _world.CellSize, WorkerRadius);
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
                if (kb.escapeKey.wasPressedThisFrame && _control == ControlWorker.Excavator)
                    _worker.ClearRoute();
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
                if (kb.vKey.wasPressedThisFrame) _scanView?.Toggle();
                if (kb.nKey.wasPressedThisFrame) SkipSleep();
                if (kb.digit1Key.wasPressedThisFrame) _prospector?.SetDistance(ScanDistance.Short);
                if (kb.digit2Key.wasPressedThisFrame) _prospector?.SetDistance(ScanDistance.Medium);
                if (kb.digit3Key.wasPressedThisFrame) _prospector?.SetDistance(ScanDistance.Long);
                if (kb.qKey.wasPressedThisFrame) _prospector?.SetWidth(ScanWidth.Narrow);
                if (kb.eKey.wasPressedThisFrame) _prospector?.SetWidth(ScanWidth.Wide);
                if (kb.f1Key.wasPressedThisFrame) _prospector?.SetWorkMode(ProspectorWorkMode.Manual);
                if (kb.f2Key.wasPressedThisFrame) _prospector?.SetWorkMode(ProspectorWorkMode.SurveyNearby);
                if (kb.f3Key.wasPressedThisFrame) _prospector?.SetWorkMode(ProspectorWorkMode.AssistExcavator);
                if (_control == ControlWorker.Prospector && _prospector != null && _crewPhase == CrewPhase.OnShift)
                {
                    if (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame)
                        _prospector.SetRadar(false);
                    else if (kb.spaceKey.wasPressedThisFrame)
                    {
                        if (!_prospector.RadarOn)
                            _prospector.SetRadar(true);
                        else
                            scanPulse = true;
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
                _engineer?.Tick();
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
            FollowCamera();
        }

        void TickClock()
        {
            float prev = _gameHour;
            _gameHour += Time.deltaTime / SecondsPerGameHour;
            if (_gameHour >= 24f)
            {
                _gameHour -= 24f;
                _dayIndex++;
            }
            ApplyDayNightLight();

            // Phase transitions driven by clock edges
            if (_crewPhase == CrewPhase.OnShift && prev < ShiftEndHour && _gameHour >= ShiftEndHour)
                BeginHeadingHome();
            if (_crewPhase == CrewPhase.Asleep && prev < ShiftStartHour && _gameHour >= ShiftStartHour)
                BeginHeadingOut(announce: true);

            // Sleep recovery: Frustration −2 / real second while crew is asleep
            if (_crewPhase == CrewPhase.Asleep)
                _worker?.TickRestFrustrationRelief(Time.deltaTime);
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
            bool show = _scanView != null && _scanView.Visible;
            _worker?.SetRouteVisible(show);
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
            if (kb != null && (kb.spaceKey.isPressed || kb.leftAltKey.isPressed || kb.rightAltKey.isPressed)) return;
            if (mouse.middleButton.isPressed) return;

            Vector2 screen = mouse.position.ReadValue();
            Vector2 guiPt = new(screen.x, Screen.height - screen.y);
            if (IsOverHud(guiPt)) return;

            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));

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
            _calc?.Reset();
            _hauler?.ResetToBase();
            _refiner?.ResetToBase();
            _prospector?.ResetTo(_world.CellCenter(StartX - 6, StartY - 2));
            _world.ClearRockStudy();
            _yard?.Reset();
            _scanView?.ClearHints();
            _gasFx?.Rescan();
            _banter.Clear();
            _hoverPile = null;
            RefreshWorkPosts();
            _dayIndex = 1;
            _gameHour = 7.7f;
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
        bool _showExcavatorStats;
        bool _showBalanceHarness;
        readonly ExcavatorBalanceHarness _balance = new();

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

            var labelSm = LabelStyle(10, UiDim);
            var labelMd = LabelStyle(11, UiDim);
            var labelHdr = LabelStyle(10, UiCyan, bold: true);
            var valueGreen = LabelStyle(22, UiGreen, bold: true);
            var valueCyan = LabelStyle(18, UiCyan, bold: true);
            var cardTitle = LabelStyle(12, UiCyan, bold: true);
            var cardSub = LabelStyle(10, UiMute);

            // ——— Top-left resource instrumentation ———
            float w = 400f;
            var statsR = new Rect(10, 8, w, 86);
            DrawCyberPanel(statsR, lit: true);
            Block(statsR);
            if (_calc != null)
            {
                GUI.Label(new Rect(22, 12, 80, 14), "GOLD", labelHdr);
                GUI.Label(new Rect(22, 26, 120, 28), $"{_calc.RefinedGold}", valueGreen);
                GUI.Label(new Rect(150, 12, 100, 14), "DIRT", labelHdr);
                GUI.Label(new Rect(150, 26, 120, 28), $"{_calc.DirtPieces}", valueCyan);

                GUI.Label(new Rect(280, 14, 110, 14), "ORE IN", labelSm);
                GUI.Label(new Rect(280, 28, 110, 18), $"{_calc.GoldSockets}", LabelStyle(14, UiWhite));
                GUI.Label(new Rect(280, 48, 110, 14), "WASHED", labelSm);
                GUI.Label(new Rect(280, 62, 110, 18), $"{_calc.CellsWashed}", LabelStyle(14, UiWhite));

                DrawHLine(statsR.x + 12, statsR.y + 58, statsR.width - 24, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.18f));
                GUI.Label(new Rect(22, 62, 250, 18),
                    _calc.CarryPiles > 0
                        ? $"HAUL  {_calc.CarryPiles}   R{_calc.CarryRockMass:0} / G{_calc.CarryGoldSockets}"
                        : "HAULER  SEEKING…",
                    labelMd);
            }

            // ——— Day clock / shift ———
            bool canSkip = _crewPhase == CrewPhase.Asleep || _crewPhase == CrewPhase.HeadingHome;
            float clockH = canSkip ? 92f : 64f;
            var clockR = new Rect(10, 100, 168f, clockH);
            DrawCyberPanel(clockR, lit: _crewPhase == CrewPhase.OnShift);
            Block(clockR);
            GUI.Label(new Rect(clockR.x + 12, clockR.y + 8, 140, 14), $"DAY {_dayIndex}", labelHdr);
            GUI.Label(new Rect(clockR.x + 12, clockR.y + 24, 140, 22), FormatClock(),
                LabelStyle(20, UiAmber, bold: true));
            string phaseLabel = _crewPhase switch
            {
                CrewPhase.OnShift => "SHIFT // 08–18",
                CrewPhase.HeadingHome => "KNOCKING OFF",
                CrewPhase.Asleep => "CREW ASLEEP",
                CrewPhase.HeadingOut => "SHIFT STARTING",
                _ => "",
            };
            GUI.Label(new Rect(clockR.x + 12, clockR.y + 46, 150, 14), phaseLabel, labelSm);
            if (canSkip)
            {
                var skipR = new Rect(clockR.x + 12, clockR.y + 64, 144f, 22);
                Block(skipR);
                if (DrawCyberButton(skipR, "SKIP SLEEP // N", selected: false, accent: UiAmber))
                    SkipSleep();
            }

            // ——— Left worker roster ———
            float cardW = 168f, cardH = 70f, cardGap = 6f;
            float cx = 10f, cy = 100f + clockH + 8f;
            string prosSub = _prospector == null ? "SCAN · RADAR" : _prospector.WorkMode switch
            {
                ProspectorWorkMode.SurveyNearby => "AUTO // SURVEY",
                ProspectorWorkMode.AssistExcavator => "AUTO // ASSIST",
                _ => "MANUAL · RADAR",
            };
            DrawWorkerCard(new Rect(cx, cy, cardW, cardH), ControlWorker.Prospector,
                "PROSPECTOR", prosSub, cardTitle, cardSub);
            DrawBanterBubble(cx + cardW + 8f, cy, WorkerBanter.Voice.Prospector);
            string digSub = _worker != null && _worker.RouteCount > 0
                    ? $"ROUTE · {_worker.RouteCount} PIN{(_worker.RouteCount == 1 ? "" : "S")}"
                    : "LMB PIN · SHIFT GO";
            DrawWorkerCard(new Rect(cx, cy + cardH + cardGap, cardW, cardH), ControlWorker.Excavator,
                "EXCAVATOR", digSub, cardTitle, cardSub);
            float excavBanterX = (_showExcavatorStats && _worker != null)
                ? cx + cardW + 8f + 300f
                : cx + cardW + 8f;
            DrawBanterBubble(excavBanterX, cy + cardH + cardGap, WorkerBanter.Voice.Excavator);
            DrawWorkerCard(new Rect(cx, cy + (cardH + cardGap) * 2, cardW, cardH), ControlWorker.Hauler,
                "HAULER",
                _hauler != null && _hauler.PreferGold ? "PRIORITY // GOLD" : "PRIORITY // MIXED",
                cardTitle, cardSub);
            DrawBanterBubble(cx + cardW + 8f, cy + (cardH + cardGap) * 2, WorkerBanter.Voice.Hauler);

            string refSub = _refiner == null ? "WASHER"
                : _refiner.Priority == RefinerPriority.GoldOre ? "WASH // ORE GOLD" : "WASH // ORE ROCK";
            DrawWorkerCard(new Rect(cx, cy + (cardH + cardGap) * 3, cardW, cardH), ControlWorker.Refiner,
                "REFINER", refSub, cardTitle, cardSub);
            DrawBanterBubble(cx + cardW + 8f, cy + (cardH + cardGap) * 3, WorkerBanter.Voice.Refiner);
            string engSub = _engineer == null ? "STANDBY"
                : _engineer.IsRepairing ? "REPAIR // ON SITE"
                : _engineer.IsEnRoute ? "EN ROUTE // DRILL"
                : _engineer.IsReturning ? "RETURNING // CAMP"
                : "STANDBY // CAMP";
            DrawWorkerCard(new Rect(cx, cy + (cardH + cardGap) * 4, cardW, cardH), ControlWorker.Engineer,
                "ENGINEER", engSub, cardTitle, cardSub);
            DrawBanterBubble(cx + cardW + 8f, cy + (cardH + cardGap) * 4, WorkerBanter.Voice.Engineer);

            float rosterExtraY = cy + (cardH + cardGap) * 5 + 4f;

            if (_control == ControlWorker.Excavator && _worker != null)
            {
                var statsBtn = new Rect(cx, rosterExtraY, cardW, 28f);
                Block(statsBtn);
                if (DrawCyberButton(statsBtn, _showExcavatorStats ? "STATS // ON" : "STATS // SHEET",
                        selected: _showExcavatorStats, accent: UiAmber))
                    _showExcavatorStats = !_showExcavatorStats;
                rosterExtraY += 32f;
            }

            if (_control == ControlWorker.Hauler && _hauler != null)
            {
                var goldBtn = new Rect(cx, rosterExtraY, cardW, 28f);
                Block(goldBtn);
                if (DrawCyberButton(goldBtn, _hauler.PreferGold ? "GOLD FIRST // ON" : "GOLD FIRST // OFF",
                        selected: _hauler.PreferGold, accent: UiAmber))
                    _hauler.TogglePreferGold();
            }

            if (_control == ControlWorker.Refiner && _refiner != null)
            {
                var rockBtn = new Rect(cx, rosterExtraY, cardW, 28f);
                var goldBtn = new Rect(cx, rosterExtraY + 32f, cardW, 28f);
                Block(rockBtn); Block(goldBtn);
                if (DrawCyberButton(rockBtn, "PRIORITY // ORE ROCK",
                        selected: _refiner.Priority == RefinerPriority.OreRock, accent: UiDim))
                    _refiner.SetPriority(RefinerPriority.OreRock);
                if (DrawCyberButton(goldBtn, "PRIORITY // ORE GOLD",
                        selected: _refiner.Priority == RefinerPriority.GoldOre, accent: UiAmber))
                    _refiner.SetPriority(RefinerPriority.GoldOre);
            }

            // Excavator sheet docks beside the roster (not world-anchored)
            if (_showExcavatorStats && _worker != null)
                DrawExcavatorStatsRoster(cx + cardW + 8f, cy);

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

            bool scanOn = _scanView != null && _scanView.Visible;
            var rScan = new Rect(bx, by, bw, bh);
            Block(rScan);
            if (DrawCyberButton(rScan, scanOn ? "SCAN VIEW // ON" : "SCAN VIEW", selected: scanOn))
            {
                _scanView?.Toggle();
                SyncRoutePinsToScanView();
            }

            bool tacOn = _tactical != null && _tactical.Visible;
            var rTac = new Rect(bx, by + bh + 6, bw, bh);
            Block(rTac);
            if (DrawCyberButton(rTac, tacOn ? "TACTICAL // ON" : "TACTICAL", selected: tacOn, accent: UiAmber))
                _tactical?.Toggle();

            if (_prospector != null && _control == ControlWorker.Prospector)
            {
                float px = bx - 172f;
                var scanPanel = new Rect(px, by, 160, 152);
                DrawCyberPanel(scanPanel, lit: true);
                Block(scanPanel);
                GUI.Label(new Rect(px + 12, by + 8, 140, 16), "SCAN MODE", labelHdr);
                DrawHLine(px + 12, by + 26, 136, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.2f));

                string[] distLabels = { "SHORT", "MEDIUM", "LONG" };
                for (int i = 0; i < 3; i++)
                {
                    bool sel = (int)_prospector.Distance == i;
                    var br = new Rect(px + 12, by + 34 + i * 26, 136, 22);
                    Block(br);
                    if (DrawCyberButton(br, distLabels[i], selected: sel))
                        _prospector.SetDistance((ScanDistance)i);
                }

                var nR = new Rect(px + 12, by + 116, 64, 24);
                var wR = new Rect(px + 84, by + 116, 64, 24);
                Block(nR); Block(wR);
                if (DrawCyberButton(nR, "NARROW", selected: _prospector.Width == ScanWidth.Narrow))
                    _prospector.SetWidth(ScanWidth.Narrow);
                if (DrawCyberButton(wR, "WIDE", selected: _prospector.Width == ScanWidth.Wide))
                    _prospector.SetWidth(ScanWidth.Wide);

                // Auto job modes — under scan panel
                float wx = px;
                float wy = by + 160f;
                var workPanel = new Rect(wx, wy, 160, 142);
                DrawCyberPanel(workPanel, lit: true);
                Block(workPanel);
                GUI.Label(new Rect(wx + 12, wy + 8, 140, 16), "AUTO JOB", labelHdr);
                DrawHLine(wx + 12, wy + 26, 136, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.2f));

                (string label, ProspectorWorkMode mode)[] jobs =
                {
                    ("MANUAL", ProspectorWorkMode.Manual),
                    ("SURVEY", ProspectorWorkMode.SurveyNearby),
                    ("ASSIST DIG", ProspectorWorkMode.AssistExcavator),
                };
                for (int i = 0; i < jobs.Length; i++)
                {
                    bool sel = _prospector.WorkMode == jobs[i].mode;
                    var br = new Rect(wx + 12, wy + 34 + i * 26, 136, 22);
                    Block(br);
                    if (DrawCyberButton(br, jobs[i].label, selected: sel))
                        _prospector.SetWorkMode(jobs[i].mode);
                }
                GUI.Label(new Rect(wx + 12, wy + 116, 140, 20),
                    _prospector.WorkMode == ProspectorWorkMode.SurveyNearby
                        ? "gold near excavator"
                        : _prospector.WorkMode == ProspectorWorkMode.AssistExcavator
                            ? "study dig face"
                            : "player control",
                    LabelStyle(9, UiMute));
            }

            if (scanOn)
            {
                var legR = new Rect(bx - 8, by + (bh + 6) * 2 + 8, bw + 16, 70);
                DrawCyberPanel(legR, lit: false);
                Block(legR);
                GUI.Label(new Rect(bx, by + (bh + 6) * 2 + 14, bw, 58),
                    "AMBER ≈ gold zone\nCYAN ≈ bedrock zone\nPURPLE ≈ gas pocket\nPINS = dig route",
                    LabelStyle(10, UiDim));
            }

            DrawKeybindingsPanel();
            DrawDigHoodLog();
            DrawBalanceHarnessPanel();
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

        void DrawExcavatorStatsRoster(float px, float py)
        {
            if (_worker == null) return;

            const float panelW = 292f;
            float maxBottom = Screen.height - 12f;
            // Leave room above Dig Hood
            float digHoodTop = Screen.height - 160f;
            float panelH = Mathf.Clamp(digHoodTop - py - 8f, 420f, 640f);
            if (py + panelH > maxBottom)
                panelH = Mathf.Max(360f, maxBottom - py);

            var panel = new Rect(px, py, panelW, panelH);
            DrawCyberPanel(panel, lit: true, accentOverride: UiAmber);
            Block(panel);

            var hdr = LabelStyle(10, UiAmber, bold: true);
            var dim = LabelStyle(9, UiDim);
            var val = LabelStyle(11, UiWhite, bold: true);
            var pillar = LabelStyle(9, UiCyan, bold: true);
            var mute = LabelStyle(9, UiMute);
            var sec = LabelStyle(9, UiCyan, bold: true);

            float x0 = panel.x + 12f;
            float y = panel.y + 8f;
            float innerW = panelW - 24f;
            GUI.Label(new Rect(x0, y, innerW - 48f, 14f), "EXCAVATOR // STATS", hdr);
            y += 18f;
            DrawHLine(x0, y, innerW, new Color(UiAmber.r, UiAmber.g, UiAmber.b, 0.25f));
            y += 8f;

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

            GUI.Label(new Rect(x0, y, innerW, 12f), "CURRENT MINING", sec);
            y += 14f;
            if (_worker.HasLastDig)
            {
                GUI.Label(new Rect(x0, y, 70f, 14f), "MATERIAL", mute);
                GUI.Label(new Rect(x0 + 70f, y, innerW - 70f, 14f),
                    _worker.LastDigMaterial.ToString().ToUpperInvariant(), val);
                y += 15f;
                GUI.Label(new Rect(x0, y, 70f, 14f), "HP", mute);
                GUI.Label(new Rect(x0 + 70f, y, innerW - 70f, 14f),
                    $"{_worker.LastDigHp}/{_worker.LastDigMaxHp}", val);
                y += 15f;
                GUI.Label(new Rect(x0, y, 70f, 14f), "DAMAGE", mute);
                GUI.Label(new Rect(x0 + 70f, y, innerW - 70f, 14f),
                    $"{_worker.LastDigDamage}  (x{_worker.LastDigThermalMul:0.00})", val);
                y += 15f;
                GUI.Label(new Rect(x0, y, 70f, 14f), "WEAK PT", mute);
                GUI.Label(new Rect(x0 + 70f, y, innerW - 70f, 14f),
                    _worker.LastDigWeakPoint ? "YES" : "NO",
                    LabelStyle(11, _worker.LastDigWeakPoint ? UiGreen : UiDim, bold: true));
                y += 16f;
            }
            else
            {
                GUI.Label(new Rect(x0, y, innerW, 14f), "NO STRIKE YET", mute);
                y += 16f;
            }

            DrawHLine(x0, y, innerW, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.15f));
            y += 8f;

            var stats = _worker.Stats;
            DrawStatPillar(ref y, x0, innerW, "BODY", WorkerStatId.RawPower, 10, stats, pillar, dim, val);
            y += 4f;
            DrawStatPillar(ref y, x0, innerW, "MIND", WorkerStatId.Calibration, 10, stats, pillar, dim, val);
            y += 4f;
            DrawStatPillar(ref y, x0, innerW, "SOUL", WorkerStatId.Composure, 10, stats, pillar, dim, val);

            var close = new Rect(panel.xMax - 54f, panel.y + 6f, 42f, 18f);
            if (DrawCyberButton(close, "×", selected: false, accent: UiAmber))
                _showExcavatorStats = false;
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
            int n = DigHoodLog.Lines.Count;
            float panelH = 32f + Mathf.Max(1, n) * lineH + 14f;
            var r = new Rect(10f, Screen.height - panelH - 12f, panelW, panelH);
            DrawCyberPanel(r, lit: false);
            Block(r);

            GUI.Label(new Rect(r.x + 12, r.y + 8, panelW - 24, 14),
                "DIG HOOD // CurrentHP", LabelStyle(10, UiCyan, bold: true));
            DrawHLine(r.x + 12, r.y + 24, panelW - 24, new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.2f));

            var lineStyle = LabelStyle(9, UiDim);
            float y = r.y + 30f;
            if (n == 0)
            {
                GUI.Label(new Rect(r.x + 12, y, panelW - 24, lineH + 2f),
                    "waiting for a dig strike…", lineStyle);
                return;
            }

            foreach (var line in DigHoodLog.Lines)
            {
                GUI.Label(new Rect(r.x + 12, y, panelW - 24, lineH + 2f), line, lineStyle);
                y += lineH;
            }
        }

        void DrawKeybindingsPanel()
        {
            const float panelW = 248f;
            const float panelH = 392f;
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
                ("Enter", "Pin at excavator"),
                ("RMB / ⌫", "Undo last pin"),
                ("Esc", "Clear dig route"),
                ("SPACE", "Radar on · then scan"),
                ("SHIFT", "Radar off"),
                ("1 / 2 / 3", "Scan short · med · long"),
                ("Q / E", "Scan narrow · wide"),
                ("F1 / F2 / F3", "Manual · Survey · Assist"),
                ("V", "Scan view overlay"),
                ("T", "Tactical map"),
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
