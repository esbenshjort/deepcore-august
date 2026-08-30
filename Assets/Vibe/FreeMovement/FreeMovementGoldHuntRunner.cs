using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Small vertical gold-hunt: start at bottom, dig upward.
    /// Gold is hidden in unexcavated rock (fog of war) until the wall face is revealed.
    /// </summary>
    public sealed class FreeMovementGoldHuntRunner : MonoBehaviour
    {
        public const int CellsAcrossWorker = 12;

        [SerializeField] float cellSize = 0.1f;
        [SerializeField] float cameraSize = 5.2f;
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
        int _goldFound;
        int _goldTotal;
        int _lanternCount;

        float WorkerRadius => CellsAcrossWorker * 0.5f * cellSize;
        Vector2 BasecampPos => _world.CellCenter(36, 10);

        void Start() => Build();

        void Build()
        {
            EnsureCamera();
            EnsureGlobalLight();
            _pixel = DigVisualKit.Pixel;
            _goalSprite = MakeGoalSprite();

            int tw = 72;
            int th = 110;
            _world = new FineTerrainWorld(tw, th, cellSize);
            BuildHuntMap();
            CountGold();

            _worldRoot = new GameObject("GoldHuntWorld").transform;
            _looseRoot = new GameObject("Loose").transform;
            _looseRoot.SetParent(_worldRoot);
            _lanternRoot = new GameObject("Lanterns").transform;
            _lanternRoot.SetParent(_worldRoot);

            var viewGo = new GameObject("TerrainView");
            viewGo.transform.SetParent(_worldRoot, false);
            viewGo.AddComponent<FreeMovementTerrainView>()
                .Setup(_world, fogOfWarGold: false, strongCliffEdges: true);
            GoldVeinShine.Attach(_worldRoot, _world);

            // No per-cell ShadowCaster2D — too heavy while digging
            DigVisualKit.PlaceLantern(_lanternRoot, new Vector2(tw * cellSize * 0.5f, 9 * cellSize), local: true, intensity: 2.5f);
            _lanternCount = 1;

            _calc = new DeliveryCalculator();
            _yard = BasecampYard.Spawn(_worldRoot, BasecampPos);
            _calc.BindStockpiles(_yard.Rock, _yard.Gold);
            SpawnWorker(_worldRoot);
            _hauler = HaulerPerson.Spawn(_worldRoot, _world, _yard.DropPoint, _calc);
            FrameCamera();

            Debug.Log($"[GoldHunt] Dig up · rock/gold stockpiles · L lantern");
        }

        void BuildHuntMap()
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

            // Bottom staging chamber — only open area at start
            _world.ExcavateRect(18, 6, 36, 18);

            // Narrow upward shaft (player must dig the rest)
            _world.ExcavateRect(30, 24, 12, 8);

            // Diggable bedrock patches — slow the climb in a few places
            GoldVeinPlacer.PlaceBedrockPatch(_world, 0.42f, 0.32f, 0.055f, seed: 711);
            GoldVeinPlacer.PlaceBedrockPatch(_world, 0.58f, 0.48f, 0.06f, seed: 712);
            GoldVeinPlacer.PlaceBedrockPatch(_world, 0.35f, 0.62f, 0.05f, seed: 713);
            GoldVeinPlacer.PlaceBedrockPatch(_world, 0.65f, 0.75f, 0.055f, seed: 714);

            // Gold veins ABOVE — only reachable by digging up / sideways
            GoldVeinPlacer.PlaceVein(_world, 0.22f, 0.42f, 0.48f, 0.55f, 0.022f, seed: 701, minGrade: 3, maxGrade: 5);
            GoldVeinPlacer.PlaceVein(_world, 0.55f, 0.58f, 0.82f, 0.72f, 0.02f, seed: 702, minGrade: 2, maxGrade: 5);
            GoldVeinPlacer.PlaceVein(_world, 0.28f, 0.78f, 0.70f, 0.88f, 0.018f, seed: 703, minGrade: 3, maxGrade: 5);
            GoldVeinPlacer.PlaceCluster(_world, 0.35f, 0.35f, 0.05f, 12, seed: 704);
            GoldVeinPlacer.PlaceCluster(_world, 0.72f, 0.48f, 0.045f, 10, seed: 705);
            GoldVeinPlacer.PlaceCluster(_world, 0.50f, 0.92f, 0.04f, 14, seed: 706); // near top prize
        }

        void CountGold()
        {
            _goldTotal = 0;
            for (int y = 0; y < _world.Height; y++)
            for (int x = 0; x < _world.Width; x++)
            {
                var c = _world.Get(x, y);
                if (c.GoldGrade > 0 && c.Phase != TerrainPhase.Excavated) _goldTotal++;
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
            // Weak headlamp — you need lanterns to feel safe
            DigVisualKit.ConfigurePointLight(light,
                new Color(1f, 0.72f, 0.45f),
                intensity: 0.22f,
                outer: 0.95f,
                inner: 0.04f,
                shadows: false,
                falloff: 0.82f);

            _worker = go.AddComponent<FreeWorkerController>();
            Vector2 start = _world.CellCenter(36, 14);
            _worker.Setup(_world, start, WorkerRadius, _goalMarker, OnBroke);
            DigVisualKit.AttachDrillerVisual(go, _worker);
        }

        void OnBroke(int x, int y)
        {
            var c = _world.Get(x, y);
            if (c.GoldGrade > 0) _goldFound++;
            Vector2 tip = _worker.DrillTip(0.4f);
            // Prefer broken cell near tip so ore pops from the cut face
            Vector2 face = Vector2.Lerp(_world.CellCenter(x, y), tip, 0.75f);
            tip = Vector2.Lerp(tip, face, 0.5f);
            LoosePile.SpawnCellFromDrill(_looseRoot, tip, _worker.Facing, c, _world.CellSize, WorkerRadius, x, y);
        }

        void TryPlaceLantern()
        {
            if (_lanternCount >= 12) return;
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
            // Only on open floor
            var at = _world.WorldToCell(pos);
            if (!_world.InBounds(at.x, at.y) || !_world.IsExcavated(at.x, at.y))
                pos = _worker.Position;

            DigVisualKit.PlaceLantern(_lanternRoot, pos, local: true, intensity: 2.5f);
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
                if (kb.rKey.wasPressedThisFrame) ResetHunt();
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

        void ResetHunt()
        {
            BuildHuntMap();
            CountGold();
            _goldFound = 0;
            for (int i = _looseRoot.childCount - 1; i >= 0; i--)
                Destroy(_looseRoot.GetChild(i).gameObject);
            _worker.Setup(_world, _world.CellCenter(36, 14), WorkerRadius, _goalMarker, OnBroke);
            _calc?.Reset();
            _hauler?.ResetToBase();
            _hoverPile = null;
            var shine = _worldRoot != null ? _worldRoot.GetComponentInChildren<GoldVeinShine>() : null;
            shine?.Rescan();
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
            cam.backgroundColor = new Color(0.008f, 0.008f, 0.012f);
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
                l.intensity = 0.012f; // near-black mine — lanterns only
                l.color = new Color(0.2f, 0.28f, 0.45f);
                return;
            }
            var go = new GameObject("Global Light 2D");
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 0.012f;
            light.color = new Color(0.2f, 0.28f, 0.45f);
        }

        void OnGUI()
        {
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

            GUI.Box(new Rect(10, 8, 400, 72), GUIContent.none);
            if (_calc != null)
            {
                GUI.Label(new Rect(22, 12, 180, 28), $"GOLD  {_calc.GoldValue}", goldStyle);
                GUI.Label(new Rect(190, 16, 200, 22), $"sockets {_calc.GoldSockets}  ·  trips {_calc.Deliveries}", small);
                GUI.Label(new Rect(22, 42, 180, 24), $"ROCK  {_calc.RockMass:0}", rockStyle);
                GUI.Label(new Rect(190, 44, 210, 22),
                    _calc.CarryPiles > 0
                        ? $"hauling R{_calc.CarryRockMass:0} / G{_calc.CarryGoldValue}"
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

            GUI.Label(new Rect(10, 86, 980, 36),
                $"L: lantern ({_lanternCount}/12)  ·  R: reset  ·  hover stockpiles for data  ·  dug {_goldFound}/{_goldTotal}",
                small);
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
