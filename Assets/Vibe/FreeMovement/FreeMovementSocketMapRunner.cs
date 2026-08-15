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
        [SerializeField] float cameraSize = 8.5f;
        [SerializeField] float cameraFollow = 5f;

        FineTerrainWorld _world;
        FreeWorkerController _worker;
        HaulerPerson _hauler;
        ProspectorPerson _prospector;
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
        enum ControlWorker : byte { Prospector = 0, Excavator = 1, Hauler = 2 }
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
        int StartY => 28;
        Vector2 BasecampPos => _world.CellCenter(StartX, StartY - 4);

        void Start() => Build();

        void Build()
        {
            EnsureCamera();
            EnsureGlobalLight();
            _pixel = DigVisualKit.Pixel;
            _goalSprite = MakeGoalSprite();

            // Larger prospector field
            int tw = 240;
            int th = 200;
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
            GoldVeinShine.Attach(_worldRoot, _world);
            _tactical = TacticalMapOverlay.Attach(_worldRoot, _world);
            _scanView = ScanViewOverlay.Attach(_worldRoot, _world);

            DigVisualKit.PlaceLantern(_lanternRoot, _world.CellCenter(StartX, StartY - 2), local: true, intensity: 2.6f);
            _lanternCount = 1;

            _calc = new DeliveryCalculator();
            _yard = BasecampYard.Spawn(_worldRoot, BasecampPos);
            _calc.BindStockpiles(_yard.Rock, _yard.Gold);
            SpawnWorker(_worldRoot);
            _hauler = HaulerPerson.Spawn(_worldRoot, _world, _yard.DropPoint, _calc);
            _prospector = ProspectorPerson.Spawn(_worldRoot, _world,
                _world.CellCenter(StartX - 6, StartY - 2), _scanView);
            _control = ControlWorker.Prospector;
            FrameCamera();

            Debug.Log($"[SocketMap] worker cards · Tab cycle · F radar · Space scan");
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

            // Half-oval start chamber (flat floor, curved roof)
            int rx = 28, ry = 18;
            int floorY = StartY - 8;
            GoldVeinPlacer.ExcavateHalfOval(_world, StartX, floorY, rx, ry);
            GoldVeinPlacer.ExcavateHalfOval(_world, StartX, floorY + ry - 2, 7, 16);

            // Organic bedrock veins + clumps (soft corridors remain diggable)
            GoldVeinPlacer.PlaceOrganicBedrock(_world, seed: 7701);

            // Organic gold — sparse near start, rich farther out
            GoldVeinPlacer.PlaceOrganicGold(_world, StartX, StartY, seed: 9102);
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
            _goalMarker = new GameObject("Goal").transform;
            _goalMarker.SetParent(root);
            var gsr = _goalMarker.gameObject.AddComponent<SpriteRenderer>();
            gsr.sprite = _goalSprite;
            gsr.sortingOrder = 35;
            DigVisualKit.ApplyLit(gsr);
            _goalMarker.localScale = Vector3.one * 0.5f;
            _goalMarker.gameObject.SetActive(false);

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
            _worker.Setup(_world, _world.CellCenter(StartX, StartY), WorkerRadius, _goalMarker, OnBroke);
            DigVisualKit.AttachDrillerVisual(go, _worker);
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
                if (kb.escapeKey.wasPressedThisFrame) _worker.ClearGoal();
                if (kb.rKey.wasPressedThisFrame) ResetMap();
                if (kb.lKey.wasPressedThisFrame) TryPlaceLantern();
                if (kb.tKey.wasPressedThisFrame) _tactical?.Toggle();
                if (kb.tabKey.wasPressedThisFrame) CycleControl(+1);
                if (kb.vKey.wasPressedThisFrame) _scanView?.Toggle();
                if (kb.fKey.wasPressedThisFrame) _prospector?.ToggleRadar();
                if (kb.digit1Key.wasPressedThisFrame) _prospector?.SetDistance(ScanDistance.Short);
                if (kb.digit2Key.wasPressedThisFrame) _prospector?.SetDistance(ScanDistance.Medium);
                if (kb.digit3Key.wasPressedThisFrame) _prospector?.SetDistance(ScanDistance.Long);
                if (kb.qKey.wasPressedThisFrame) _prospector?.SetWidth(ScanWidth.Narrow);
                if (kb.eKey.wasPressedThisFrame) _prospector?.SetWidth(ScanWidth.Wide);
                if (kb.spaceKey.wasPressedThisFrame && _control == ControlWorker.Prospector) scanPulse = true;
                if (kb.gKey.wasPressedThisFrame && _control == ControlWorker.Hauler)
                    _hauler?.TogglePreferGold();
            }

            switch (_control)
            {
                case ControlWorker.Prospector:
                    _worker.Tick(Vector2.zero);
                    _prospector?.SetHudVisible(true);
                    _prospector?.Tick(wasd, scanPulse);
                    break;
                case ControlWorker.Excavator:
                    _prospector?.SetHudVisible(false);
                    _worker.Tick(wasd);
                    break;
                case ControlWorker.Hauler:
                    _worker.Tick(Vector2.zero);
                    _prospector?.SetHudVisible(false);
                    break;
            }

            _hauler?.Tick();
            UpdateStockpileHover();
            FollowCamera();
        }

        void LateUpdate()
        {
            // After OnGUI so HUD clicks never become dig goals
            HandleMouse();
        }

        void CycleControl(int delta)
        {
            int n = ((int)_control + delta) % 3;
            if (n < 0) n += 3;
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
            if (!mouse.leftButton.wasPressedThisFrame) return;

            Vector2 screen = mouse.position.ReadValue();
            // Unity GUI uses y-down; Input System uses y-up
            Vector2 guiPt = new(screen.x, Screen.height - screen.y);
            if (IsOverHud(guiPt)) return;

            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            if (_control == ControlWorker.Prospector && _prospector != null)
                _prospector.FaceToward(world);
            else if (_control == ControlWorker.Excavator)
                _worker.SetGoal(world);
            // Hauler: no dig goal / aim from LMB
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
            _worker.Setup(_world, _world.CellCenter(StartX, StartY), WorkerRadius, _goalMarker, OnBroke);
            _calc?.Reset();
            _hauler?.ResetToBase();
            _prospector?.ResetTo(_world.CellCenter(StartX - 6, StartY - 2));
            _scanView?.ClearHints();
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

        // Cyberpunk HUD palette (thin neon on dark glass)
        static readonly Color CpCyan = new(0.35f, 0.95f, 1f, 1f);
        static readonly Color CpGold = new(1f, 0.86f, 0.28f, 1f);
        static readonly Color CpDim = new(0.55f, 0.72f, 0.78f, 1f);
        static readonly Color CpPanel = new(0.04f, 0.08f, 0.12f, 0.72f);
        static readonly Color CpBtnIdle = new(0.08f, 0.16f, 0.22f, 0.9f);
        static readonly Color CpBtnOn = new(0.1f, 0.45f, 0.55f, 0.95f);

        void OnGUI()
        {
            _hudBlockers.Clear();

            var goldStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = CpGold }
            };
            var rockStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = CpCyan }
            };
            var small = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = CpDim }
            };
            var hdr = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = CpCyan }
            };
            var cardTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = CpCyan }
            };
            var cardSub = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = CpDim }
            };

            float w = 420f;
            var statsR = new Rect(10, 8, w, 78);
            DrawCpPanel(statsR);
            Block(statsR);
            if (_calc != null)
            {
                GUI.Label(new Rect(22, 12, 200, 28), $"GOLD  {_calc.GoldValue}", goldStyle);
                GUI.Label(new Rect(200, 14, 220, 24), $"SOCKETS {_calc.GoldSockets}  ·  TRIPS {_calc.Deliveries}", small);
                GUI.Label(new Rect(22, 42, 200, 24), $"ROCK  {_calc.RockMass:0}", rockStyle);
                GUI.Label(new Rect(200, 44, 220, 24),
                    _calc.CarryPiles > 0
                        ? $"HAUL {_calc.CarryPiles}  (R{_calc.CarryRockMass:0} / G{_calc.CarryGoldValue})"
                        : "HAULER SEEKING…",
                    small);
            }

            // ——— Left worker roster (Prospector → Excavator → Hauler) ———
            float cardW = 168f, cardH = 72f, cardGap = 8f;
            float cx = 10f, cy = 118f;
            DrawWorkerCard(new Rect(cx, cy, cardW, cardH), ControlWorker.Prospector,
                "PROSPECTOR", "SCAN · RADAR", cardTitle, cardSub);
            DrawWorkerCard(new Rect(cx, cy + cardH + cardGap, cardW, cardH), ControlWorker.Excavator,
                "EXCAVATOR", "DIG · DRIVE", cardTitle, cardSub);
            DrawWorkerCard(new Rect(cx, cy + (cardH + cardGap) * 2, cardW, cardH), ControlWorker.Hauler,
                "HAULER",
                _hauler != null && _hauler.PreferGold ? "PRIORITY // GOLD" : "PRIORITY // MIXED",
                cardTitle, cardSub);

            if (_control == ControlWorker.Hauler && _hauler != null)
            {
                var goldBtn = new Rect(cx, cy + (cardH + cardGap) * 3 + 4f, cardW, 28f);
                Block(goldBtn);
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = _hauler.PreferGold ? new Color(0.55f, 0.4f, 0.05f, 0.95f) : CpBtnIdle;
                if (GUI.Button(goldBtn, _hauler.PreferGold ? "GOLD FIRST // ON" : "GOLD FIRST // OFF"))
                    _hauler.TogglePreferGold();
                GUI.backgroundColor = prevBg;
            }

            if (_hoverPile != null)
            {
                var tip = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 12,
                    alignment = TextAnchor.UpperLeft,
                    normal = { textColor = CpCyan },
                    padding = new RectOffset(10, 10, 8, 8)
                };
                var mouse = Mouse.current;
                float mx = mouse != null ? mouse.position.ReadValue().x : 200f;
                float my = mouse != null ? Screen.height - mouse.position.ReadValue().y : 200f;
                GUI.Box(new Rect(mx + 14, my + 14, 200, 118), _hoverPile.HoverInfo, tip);
            }

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.45f, 0.62f, 0.68f) }
            };
            GUI.Label(new Rect(10, Screen.height - 40, 900, 32),
                "TAB cycle workers  ·  WASD  ·  LMB aim/dig  ·  G gold-first (hauler)  ·  F radar  ·  SPACE scan",
                style);

            // Right tools — overlays only (select workers on the left)
            float bw = 132f, bh = 30f;
            float bx = Screen.width - bw - 14f;
            float by = 12f;
            var prev = GUI.backgroundColor;

            bool scanOn = _scanView != null && _scanView.Visible;
            var rScan = new Rect(bx, by, bw, bh);
            Block(rScan);
            GUI.backgroundColor = scanOn ? CpBtnOn : CpBtnIdle;
            if (GUI.Button(rScan, scanOn ? "SCAN VIEW // ON" : "SCAN VIEW"))
                _scanView?.Toggle();

            bool tacOn = _tactical != null && _tactical.Visible;
            var rTac = new Rect(bx, by + bh + 6, bw, bh);
            Block(rTac);
            GUI.backgroundColor = tacOn ? new Color(0.55f, 0.4f, 0.05f, 0.95f) : CpBtnIdle;
            if (GUI.Button(rTac, tacOn ? "TACTICAL // ON" : "TACTICAL"))
                _tactical?.Toggle();
            GUI.backgroundColor = prev;

            if (_prospector != null && _control == ControlWorker.Prospector)
            {
                float px = bx - 168f;
                var scanPanel = new Rect(px, by, 156, 148);
                DrawCpPanel(scanPanel);
                Block(scanPanel);
                GUI.Label(new Rect(px + 10, by + 6, 140, 18), "SCAN MODE", hdr);

                string[] distLabels = { "SHORT", "MEDIUM", "LONG" };
                for (int i = 0; i < 3; i++)
                {
                    bool sel = (int)_prospector.Distance == i;
                    var br = new Rect(px + 10, by + 28 + i * 26, 136, 22);
                    Block(br);
                    GUI.backgroundColor = sel ? CpBtnOn : CpBtnIdle;
                    if (GUI.Button(br, distLabels[i]))
                        _prospector.SetDistance((ScanDistance)i);
                }

                var nR = new Rect(px + 10, by + 110, 64, 26);
                var wR = new Rect(px + 82, by + 110, 64, 26);
                Block(nR); Block(wR);
                GUI.backgroundColor = _prospector.Width == ScanWidth.Narrow ? CpBtnOn : CpBtnIdle;
                if (GUI.Button(nR, "NARROW"))
                    _prospector.SetWidth(ScanWidth.Narrow);
                GUI.backgroundColor = _prospector.Width == ScanWidth.Wide ? CpBtnOn : CpBtnIdle;
                if (GUI.Button(wR, "WIDE"))
                    _prospector.SetWidth(ScanWidth.Wide);
                GUI.backgroundColor = prev;
            }

            if (scanOn)
            {
                var leg = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    normal = { textColor = CpDim }
                };
                var legR = new Rect(bx - 8, by + (bh + 6) * 2 + 8, bw + 16, 50);
                DrawCpPanel(legR);
                Block(legR);
                GUI.Label(new Rect(bx, by + (bh + 6) * 2 + 14, bw, 42),
                    "YELLOW ≈ gold zone\nCYAN ≈ bedrock zone\n(soft outlines — estimate)",
                    leg);
            }
        }

        void DrawWorkerCard(Rect r, ControlWorker worker, string title, string subtitle,
            GUIStyle titleStyle, GUIStyle subStyle)
        {
            bool on = _control == worker;
            Block(r);

            var prev = GUI.color;
            GUI.color = on
                ? new Color(0.06f, 0.14f, 0.18f, 0.88f)
                : new Color(0.04f, 0.07f, 0.1f, 0.7f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);

            Color frame = on ? CpCyan : new Color(CpCyan.r, CpCyan.g, CpCyan.b, 0.28f);
            float thick = on ? 2f : 1f;
            GUI.color = frame;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - thick, r.width, thick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, thick, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - thick, r.y, thick, r.height), Texture2D.whiteTexture);
            GUI.color = on ? CpCyan : new Color(CpCyan.r, CpCyan.g, CpCyan.b, 0.55f);
            GUI.DrawTexture(new Rect(r.x, r.y, 22f, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, 1f, 14f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 22f, r.yMax - 1f, 22f, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 1f, r.yMax - 14f, 1f, 14f), Texture2D.whiteTexture);

            Color accent = worker switch
            {
                ControlWorker.Prospector => CpCyan,
                ControlWorker.Excavator => new Color(1f, 0.55f, 0.2f),
                _ => new Color(0.45f, 0.75f, 1f),
            };
            GUI.color = on ? accent : new Color(accent.r, accent.g, accent.b, 0.35f);
            GUI.DrawTexture(new Rect(r.x + 8, r.y + 10, 3f, r.height - 20), Texture2D.whiteTexture);
            GUI.color = prev;

            var tStyle = new GUIStyle(titleStyle) { normal = { textColor = on ? accent : CpDim } };
            GUI.Label(new Rect(r.x + 18, r.y + 12, r.width - 28, 22), title, tStyle);
            GUI.Label(new Rect(r.x + 18, r.y + 34, r.width - 28, 18), subtitle, subStyle);
            if (on)
                GUI.Label(new Rect(r.x + 18, r.y + 50, r.width - 28, 16), "// SELECTED", subStyle);

            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                SelectWorker(worker);
        }

        void Block(Rect r) => _hudBlockers.Add(r);

        static void DrawCpPanel(Rect r)
        {
            var prev = GUI.color;
            GUI.color = CpPanel;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(CpCyan.r, CpCyan.g, CpCyan.b, 0.35f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1f, r.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, 1f, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 1f, r.y, 1f, r.height), Texture2D.whiteTexture);
            GUI.color = new Color(CpCyan.r, CpCyan.g, CpCyan.b, 0.7f);
            GUI.DrawTexture(new Rect(r.x, r.y, 18f, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, 1f, 10f), Texture2D.whiteTexture);
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
                tex.SetPixel(x, y, new Color(0.35f, 0.95f, 0.4f));
            for (int i = 0; i < 7; i++)
            for (int x = 12 - i; x <= 12 + i; x++)
                if (x >= 0 && x < s) tex.SetPixel(x, 20 - i, new Color(0.35f, 0.95f, 0.4f));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }
    }
}
