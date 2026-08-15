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
        readonly WorkerBanter _banter = new();
        enum ControlWorker : byte { Prospector = 0, Excavator = 1, Hauler = 2, Refiner = 3 }
        ControlWorker _control = ControlWorker.Prospector;
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
        Vector2 BasecampPos => _world.CellCenter(StartX, StartY - 10);

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

            DigVisualKit.PlaceLantern(_lanternRoot, _world.CellCenter(StartX, StartY - 2), local: true, intensity: 2.6f);
            _lanternCount = 1;

            _calc = new DeliveryCalculator();
            _yard = BasecampYard.Spawn(_worldRoot, BasecampPos);
            _calc.BindStockpiles(_yard.Rock, _yard.Gold, _yard.RefinedGold, _yard.Dirt);
            SpawnWorker(_worldRoot);
            _hauler = HaulerPerson.Spawn(_worldRoot, _world, _yard.DropPoint, _calc);
            _refiner = RefinerPerson.Spawn(_worldRoot, _world, _yard, _calc);
            _prospector = ProspectorPerson.Spawn(_worldRoot, _world,
                _world.CellCenter(StartX - 6, StartY - 2), _scanView);
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

            Debug.Log($"[SocketMap] SPACE radar→scan · SHIFT radar off · worker cards");
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

            // Larger start chamber + room for wash camp
            int rx = 42, ry = 28;
            int floorY = StartY - 14;
            GoldVeinPlacer.ExcavateHalfOval(_world, StartX, floorY, rx, ry);
            GoldVeinPlacer.ExcavateHalfOval(_world, StartX, floorY + ry - 2, 10, 20);
            // Flat camp pad around base
            GoldVeinPlacer.ExcavateHalfOval(_world, StartX, StartY - 10, 22, 14);

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
                // Drop a pin at excavator feet (keyboard route planning)
                if (_control == ControlWorker.Excavator &&
                    (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                    _worker.AddPin(_worker.Position, replaceRoute: false);
                if (kb.rKey.wasPressedThisFrame) ResetMap();
                if (kb.lKey.wasPressedThisFrame) TryPlaceLantern();
                if (kb.tKey.wasPressedThisFrame) _tactical?.Toggle();
                if (kb.tabKey.wasPressedThisFrame) CycleControl(+1);
                if (kb.vKey.wasPressedThisFrame) _scanView?.Toggle();
                if (kb.digit1Key.wasPressedThisFrame) _prospector?.SetDistance(ScanDistance.Short);
                if (kb.digit2Key.wasPressedThisFrame) _prospector?.SetDistance(ScanDistance.Medium);
                if (kb.digit3Key.wasPressedThisFrame) _prospector?.SetDistance(ScanDistance.Long);
                if (kb.qKey.wasPressedThisFrame) _prospector?.SetWidth(ScanWidth.Narrow);
                if (kb.eKey.wasPressedThisFrame) _prospector?.SetWidth(ScanWidth.Wide);
                if (kb.f1Key.wasPressedThisFrame) _prospector?.SetWorkMode(ProspectorWorkMode.Manual);
                if (kb.f2Key.wasPressedThisFrame) _prospector?.SetWorkMode(ProspectorWorkMode.SurveyNearby);
                if (kb.f3Key.wasPressedThisFrame) _prospector?.SetWorkMode(ProspectorWorkMode.AssistExcavator);
                if (_control == ControlWorker.Prospector && _prospector != null)
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
            }

            _hauler?.Tick();
            DiscoverGasNearCrew();
            UpdateStockpileHover();
            SyncRoutePinsToScanView();
            FollowCamera();
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
            int n = ((int)_control + delta) % 4;
            if (n < 0) n += 4;
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
            Transform follow = _control switch
            {
                ControlWorker.Prospector => _prospector != null ? _prospector.transform : null,
                ControlWorker.Hauler => _hauler != null ? _hauler.transform : null,
                ControlWorker.Refiner => _refiner != null ? _refiner.transform : null,
                _ => _worker != null ? _worker.transform : null,
            };
            if (follow == null) return;
            Vector3 t = follow.position;
            t.z = -10f;
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
                // Bright enough to read gold grades in the wall face
                l.intensity = 0.025f;
                l.color = new Color(0.35f, 0.4f, 0.55f);
                return;
            }
            var go = new GameObject("Global Light 2D");
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 0.025f;
            light.color = new Color(0.35f, 0.4f, 0.55f);
        }

        // Industrial cyberpunk mining HUD palette
        static readonly Color UiBg = new(0.02f, 0.04f, 0.07f, 0.78f);
        static readonly Color UiBgHot = new(0.04f, 0.09f, 0.12f, 0.88f);
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
        static readonly Color CpBtnIdle = new(0.05f, 0.1f, 0.14f, 0.85f);
        static readonly Color CpBtnOn = new(0.08f, 0.28f, 0.36f, 0.92f);

        float _uiPulse;

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

            // ——— Left worker roster ———
            float cardW = 168f, cardH = 70f, cardGap = 6f;
            float cx = 10f, cy = 108f;
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
            DrawBanterBubble(cx + cardW + 8f, cy + cardH + cardGap, WorkerBanter.Voice.Excavator);
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

            if (_control == ControlWorker.Hauler && _hauler != null)
            {
                var goldBtn = new Rect(cx, cy + (cardH + cardGap) * 4 + 4f, cardW, 28f);
                Block(goldBtn);
                if (DrawCyberButton(goldBtn, _hauler.PreferGold ? "GOLD FIRST // ON" : "GOLD FIRST // OFF",
                        selected: _hauler.PreferGold, accent: UiAmber))
                    _hauler.TogglePreferGold();
            }

            if (_control == ControlWorker.Refiner && _refiner != null)
            {
                float byR = cy + (cardH + cardGap) * 4 + 4f;
                var rockBtn = new Rect(cx, byR, cardW, 28f);
                var goldBtn = new Rect(cx, byR + 32f, cardW, 28f);
                Block(rockBtn); Block(goldBtn);
                if (DrawCyberButton(rockBtn, "PRIORITY // ORE ROCK",
                        selected: _refiner.Priority == RefinerPriority.OreRock, accent: UiDim))
                    _refiner.SetPriority(RefinerPriority.OreRock);
                if (DrawCyberButton(goldBtn, "PRIORITY // ORE GOLD",
                        selected: _refiner.Priority == RefinerPriority.GoldOre, accent: UiAmber))
                    _refiner.SetPriority(RefinerPriority.GoldOre);
            }

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
                var workPanel = new Rect(wx, wy, 160, 128);
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
                GUI.Label(new Rect(wx + 12, wy + 112, 140, 12),
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
        }

        void DrawKeybindingsPanel()
        {
            const float panelW = 248f;
            const float panelH = 322f;
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
                ("TAB", "Cycle worker"),
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
                ("G", "Hauler / Refiner priority"),
                ("L", "Place lantern"),
                ("R", "Reset map"),
            };

            float y = r.y + 34f;
            for (int i = 0; i < rows.Length; i++)
            {
                GUI.Label(new Rect(r.x + 12, y, 88, 14), rows[i].k, key);
                GUI.Label(new Rect(r.x + 102, y, panelW - 118, 14), rows[i].d, desc);
                y += 15.5f;
            }
        }

        static GUIStyle LabelStyle(int size, Color color, bool bold = false) =>
            new(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                normal = { textColor = color },
                richText = false,
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
            GUI.color = selected ? UiBgHot : (hover ? new Color(0.05f, 0.1f, 0.14f, 0.9f) : CpBtnIdle);
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
