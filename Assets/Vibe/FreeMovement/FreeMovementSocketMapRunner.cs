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
        [SerializeField] float cameraSize = 7.5f;
        [SerializeField] float cameraFollow = 5f;

        FineTerrainWorld _world;
        FreeWorkerController _worker;
        HaulerPerson _hauler;
        DeliveryCalculator _calc;
        BasecampYard _yard;
        Stockpile _hoverPile;
        Transform _worldRoot;
        Transform _looseRoot;
        Transform _lanternRoot;
        Transform _goalMarker;
        Sprite _pixel;
        Sprite _goalSprite;
        int _goldCells;
        int _goldSocketsTotal;
        int _goldFoundCells;
        int _goldFoundSockets;
        int _bedrockCells;
        int _lanternCount;

        float WorkerRadius => CellsAcrossWorker * 0.5f * cellSize;
        int StartX => _world.Width / 2;
        int StartY => 22;
        Vector2 BasecampPos => _world.CellCenter(StartX, StartY - 6);

        void Start() => Build();

        void Build()
        {
            EnsureCamera();
            EnsureGlobalLight();
            _pixel = DigVisualKit.Pixel;
            _goalSprite = MakeGoalSprite();

            // Large test field (~20×16 world units)
            int tw = 200;
            int th = 160;
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

            DigVisualKit.PlaceLantern(_lanternRoot, _world.CellCenter(StartX, StartY - 4), local: true, intensity: 2.6f);
            _lanternCount = 1;

            _calc = new DeliveryCalculator();
            _yard = BasecampYard.Spawn(_worldRoot, BasecampPos);
            _calc.BindStockpiles(_yard.Rock, _yard.Gold);
            SpawnWorker(_worldRoot);
            _hauler = HaulerPerson.Spawn(_worldRoot, _world, _yard.DropPoint, _calc);
            FrameCamera();

            Debug.Log($"[SocketMap] {tw}×{th} · rock/gold stockpiles · L lantern · R reset");
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

            // Soft noise variation across the whole map (mostly rock, some mixed)
            var rng = new System.Random(9081);
            _world.BeginBatch();
            for (int y = 1; y < th - 1; y++)
            for (int x = 1; x < tw - 1; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.07f + 3f, y * 0.07f + 1f);
                float n2 = Mathf.PerlinNoise(x * 0.03f + 9f, y * 0.03f);
                if (n > 0.72f)
                {
                    int bedrock = n > 0.88f ? 4 : (n > 0.8f ? 3 : 2);
                    _world.Set(x, y, FineTerrainWorld.FromCounts(4 - bedrock, bedrock, 0));
                }
                else if (n2 > 0.78f && rng.NextDouble() < 0.35)
                {
                    // Sparse single-gold dust
                    _world.Set(x, y, FineTerrainWorld.FromCounts(3, 0, 1));
                }
            }
            _world.EndBatch();

            // Start chamber
            int sw = 40, sh = 22;
            int sx = StartX - sw / 2;
            int sy = StartY - sh / 2;
            _world.ExcavateRect(sx, sy, sw, sh);

            // Short starter shafts
            _world.ExcavateRect(StartX - 4, sy + sh, 8, 14);
            _world.ExcavateRect(sx - 10, StartY - 3, 10, 6);
            _world.ExcavateRect(sx + sw, StartY - 3, 10, 6);

            // Legend wall just above chamber: 1→4 gold sockets side by side
            PlaceGoldLegend(StartX - 10, sy + sh + 2);

            // Diggable bedrock bands / patches
            GoldVeinPlacer.PlaceBedrockPatch(_world, 0.25f, 0.45f, 0.08f, seed: 11);
            GoldVeinPlacer.PlaceBedrockPatch(_world, 0.70f, 0.40f, 0.09f, seed: 12);
            GoldVeinPlacer.PlaceBedrockPatch(_world, 0.45f, 0.70f, 0.11f, seed: 13);
            GoldVeinPlacer.PlaceBedrockPatch(_world, 0.18f, 0.78f, 0.07f, seed: 14);
            GoldVeinPlacer.PlaceBedrockPatch(_world, 0.82f, 0.75f, 0.08f, seed: 15);
            GoldVeinPlacer.PlaceBedrockPatch(_world, 0.55f, 0.28f, 0.06f, seed: 16);

            // Gold veins — grades 1–4 (socket counts)
            GoldVeinPlacer.PlaceVein(_world, 0.20f, 0.35f, 0.40f, 0.55f, 0.018f, seed: 21, minGrade: 1, maxGrade: 2);
            GoldVeinPlacer.PlaceVein(_world, 0.55f, 0.32f, 0.78f, 0.50f, 0.02f, seed: 22, minGrade: 2, maxGrade: 3);
            GoldVeinPlacer.PlaceVein(_world, 0.30f, 0.60f, 0.55f, 0.82f, 0.022f, seed: 23, minGrade: 2, maxGrade: 4);
            GoldVeinPlacer.PlaceVein(_world, 0.60f, 0.58f, 0.85f, 0.80f, 0.016f, seed: 24, minGrade: 3, maxGrade: 4);
            GoldVeinPlacer.PlaceVein(_world, 0.12f, 0.55f, 0.28f, 0.88f, 0.015f, seed: 25, minGrade: 1, maxGrade: 4);

            GoldVeinPlacer.PlaceCluster(_world, 0.35f, 0.42f, 0.04f, 18, seed: 31);
            GoldVeinPlacer.PlaceCluster(_world, 0.68f, 0.65f, 0.045f, 22, seed: 32);
            GoldVeinPlacer.PlaceCluster(_world, 0.48f, 0.88f, 0.035f, 16, seed: 33);
            GoldVeinPlacer.PlaceCluster(_world, 0.88f, 0.35f, 0.04f, 14, seed: 34);
        }

        void PlaceGoldLegend(int x0, int y0)
        {
            _world.BeginBatch();
            for (int g = 1; g <= 4; g++)
            {
                int bx = x0 + (g - 1) * 5;
                for (int oy = 0; oy < 4; oy++)
                for (int ox = 0; ox < 4; ox++)
                {
                    int x = bx + ox, y = y0 + oy;
                    if (!_world.InBounds(x, y)) continue;
                    _world.Set(x, y, FineTerrainWorld.FromCounts(4 - g, 0, g));
                }
            }
            _world.EndBatch();
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
            LoosePile.SpawnFromDrill(_looseRoot, tip, _worker.Facing, c.Mass, c.GoldGrade, _world.CellSize, WorkerRadius);
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
            if (kb != null)
            {
                if (kb.wKey.isPressed) wasd.y += 1f;
                if (kb.sKey.isPressed) wasd.y -= 1f;
                if (kb.aKey.isPressed) wasd.x -= 1f;
                if (kb.dKey.isPressed) wasd.x += 1f;
                if (kb.escapeKey.wasPressedThisFrame) _worker.ClearGoal();
                if (kb.rKey.wasPressedThisFrame) ResetMap();
                if (kb.lKey.wasPressedThisFrame) TryPlaceLantern();
            }

            _worker.Tick(wasd);
            _hauler?.Tick();
            UpdateStockpileHover();
            HandleMouse();
            FollowCamera();
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
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            _worker.SetGoal(world);
        }

        void FollowCamera()
        {
            var cam = Camera.main;
            if (cam == null || _worker == null) return;
            Vector3 t = _worker.transform.position;
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
            _hoverPile = null;
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

        void OnGUI()
        {
            // Top calculator
            var goldStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.82f, 0.25f) }
            };
            var rockStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                normal = { textColor = new Color(0.85f, 0.75f, 0.6f) }
            };
            var small = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.75f, 0.78f, 0.85f) }
            };

            float w = 420f;
            GUI.Box(new Rect(10, 8, w, 78), GUIContent.none);
            if (_calc != null)
            {
                GUI.Label(new Rect(22, 12, 200, 28), $"GOLD  {_calc.GoldValue}", goldStyle);
                GUI.Label(new Rect(200, 14, 220, 24), $"sockets {_calc.GoldSockets}  ·  trips {_calc.Deliveries}", small);
                GUI.Label(new Rect(22, 42, 200, 24), $"ROCK  {_calc.RockMass:0}", rockStyle);
                GUI.Label(new Rect(200, 44, 220, 24),
                    _calc.CarryPiles > 0
                        ? $"hauling {_calc.CarryPiles}  (R{_calc.CarryRockMass:0} / G{_calc.CarryGoldValue})"
                        : "hauler seeking…",
                    small);
            }

            if (_hoverPile != null)
            {
                var tip = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 13,
                    alignment = TextAnchor.UpperLeft,
                    normal = { textColor = new Color(1f, 0.95f, 0.8f) },
                    padding = new RectOffset(10, 10, 8, 8)
                };
                var mouse = Mouse.current;
                float mx = mouse != null ? mouse.position.ReadValue().x : 200f;
                float my = mouse != null ? Screen.height - mouse.position.ReadValue().y : 200f;
                GUI.Box(new Rect(mx + 14, my + 14, 200, 118), _hoverPile.HoverInfo, tip);
            }

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.7f, 0.72f, 0.78f) }
            };
            GUI.Label(new Rect(10, 92, 1100, 40),
                "Hauler sorts into ROCK (left) and GOLD (right) stockpiles — hover for data.\n" +
                $"L: lantern ({_lanternCount}/16)  ·  R: reset  ·  LMB dig  ·  dug gold cells {_goldFoundCells}/{_goldCells}",
                style);
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
