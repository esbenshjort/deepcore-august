using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace DeepCore.Vibe
{
    /// <summary>
    /// 8-topology showcase + dig maps. Preset 1 = all 8 kinds labeled.
    /// </summary>
    public sealed class VisualTopologyTest : MonoBehaviour
    {
        public const int UnitFootprint = 3;

        [SerializeField] float cameraSize = 11f;
        [SerializeField] float globalLightIntensity = 0.14f;
        [SerializeField] float torchIntensity = 1.5f;

        /// <summary>
        /// Showcase: every topology kind appears in a clear context.
        /// ASCII top = north. # rock  . floor  T torch
        /// </summary>
        static readonly string[] ShowcaseAll8 =
        {
            "################################",
            "################################",
            "####......................######",
            "####......................######",
            "####......########........######",
            "####......##....##........######",
            "####......##..T.##........######",
            "####......##....##........######",
            "####......########........######",
            "####......................######",
            "####.........####.........######",
            "####..........#...........######",
            "####..........#...........######",
            "####.........###..........######",
            "####......................######",
            "####......##..............######",
            "####.....##...............######",
            "####....##................######",
            "####...##.................######",
            "####......................######",
            "####......##..............######",
            "####......##..............######",
            "####........#.............######",
            "####...........#..........######",
            "####......................######",
            "####.........#.#..........######",
            "####..........#...........######",
            "################################",
        };

        static readonly string[] WideTunnel =
        {
            "##############################",
            "##############################",
            "#######................#######",
            "#######................#######",
            "#######................#######",
            "#######........T.......#######",
            "#######................#######",
            "#######................#######",
            "#######................#######",
            "##############################",
            "##############################",
        };

        static readonly string[] PointedTunnel =
        {
            "##############################",
            "##############################",
            "#######.................######",
            "#######.................######",
            "#######.................######",
            "#######.................######",
            "#######.................######",
            "########...............#######",
            "#########.............########",
            "##########...........#########",
            "###########.........##########",
            "############.......###########",
            "#############.....############",
            "##############...T############",
            "##############################",
            "##############################",
        };

        CaveGrid _grid;
        Transform _world;
        Transform _floorRoot;
        Transform _rockRoot;
        Transform _propsRoot;
        Transform _gridOverlay;
        GameObject _excavator;
        TopologyRockView _rockView;
        bool _showGrid;
        bool _debugKinds;
        int _preset = 1;

        void Start() => BuildPreset(1);

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) BuildPreset(1);
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) BuildPreset(2);
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) BuildPreset(3);
            if (kb.gKey.wasPressedThisFrame)
            {
                _showGrid = !_showGrid;
                if (_gridOverlay != null) _gridOverlay.gameObject.SetActive(_showGrid);
            }
            if (kb.tabKey.wasPressedThisFrame)
            {
                _debugKinds = !_debugKinds;
                _rockView?.SetDebugColors(_debugKinds);
            }
        }

        void BuildPreset(int preset)
        {
            _preset = preset;
            string[] map = preset switch
            {
                2 => WideTunnel,
                3 => PointedTunnel,
                _ => ShowcaseAll8,
            };
            bool labels = preset == 1;

            if (_world != null) Destroy(_world.gameObject);

            _world = new GameObject("VisualTopologyWorld").transform;
            _floorRoot = new GameObject("Floor").transform;
            _floorRoot.SetParent(_world);
            _rockRoot = new GameObject("Rock").transform;
            _rockRoot.SetParent(_world);
            _propsRoot = new GameObject("Props").transform;
            _propsRoot.SetParent(_world);
            _gridOverlay = new GameObject("GridOverlay").transform;
            _gridOverlay.SetParent(_world);
            _gridOverlay.gameObject.SetActive(_showGrid);

            // Slightly oversized floor kills grey hairline seams
            var floorSprite = ProceduralSprites.Floor(32);

            var gridGo = new GameObject("CaveGrid");
            gridGo.transform.SetParent(_world);
            _grid = gridGo.AddComponent<CaveGrid>();
            _grid.Initialize(map, _floorRoot, floorSprite);
            ScaleFloors(1.04f);

            var viewGo = new GameObject("TopologyRockView");
            viewGo.transform.SetParent(_world);
            _rockView = viewGo.AddComponent<TopologyRockView>();
            _rockView.Bind(_grid, _rockRoot, labels);
            _rockView.SetDebugColors(_debugKinds);

            BuildGridOverlay();
            PlaceTorches(map);
            PlaceExcavatorAndWorker(preset);
            SetupCameraAndLight();

            Debug.Log($"[Vibe] topology showcase preset {preset} · Tab=kind colors · labels={labels}");
        }

        void ScaleFloors(float scale)
        {
            for (int i = 0; i < _floorRoot.childCount; i++)
                _floorRoot.GetChild(i).localScale = Vector3.one * scale;
        }

        void PlaceTorches(string[] map)
        {
            int h = map.Length;
            var torchSprite = ProceduralSprites.Torch();
            for (int my = 0; my < h; my++)
            for (int x = 0; x < map[0].Length; x++)
            {
                if (map[my][x] != 'T') continue;
                int y = h - 1 - my; // ASCII → world
                var go = new GameObject("Torch");
                go.transform.SetParent(_propsRoot);
                go.transform.position = _grid.CellToWorld(x, y);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = torchSprite;
                sr.sortingOrder = 20;
                var light = go.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.color = new Color(1f, 0.72f, 0.35f);
                light.intensity = torchIntensity;
                light.pointLightInnerRadius = 0.5f;
                light.pointLightOuterRadius = 7f;
                light.falloffIntensity = 0.4f;
            }
        }

        void PlaceExcavatorAndWorker(int preset)
        {
            Vector2Int cell = FindSpawn();

            var excavatorSprite = ProceduralSprites.Excavator(96, UnitFootprint);
            _excavator = new GameObject("Excavator_3x3");
            _excavator.transform.SetParent(_propsRoot);
            _excavator.transform.position = _grid.CellToWorld(cell.x, cell.y);
            var sr = _excavator.AddComponent<SpriteRenderer>();
            sr.sprite = excavatorSprite;
            sr.sortingOrder = 30;

            var footprint = new GameObject("Footprint").transform;
            footprint.SetParent(_propsRoot);
            DrawFootprint(footprint);

            var cosy = _excavator.AddComponent<CosyExcavator>();
            cosy.ConfigureFeel(1.35f, 3.0f, UnitFootprint);
            cosy.Setup(_grid, cell, footprint);

            var worker = new GameObject("Worker");
            worker.transform.SetParent(_propsRoot);
            worker.transform.position = _grid.CellToWorld(cell.x - 3, cell.y - 1);
            var wsr = worker.AddComponent<SpriteRenderer>();
            wsr.sprite = ProceduralSprites.Worker(64);
            wsr.sortingOrder = 31;
            worker.transform.localScale = Vector3.one * 0.45f;
        }

        Vector2Int FindSpawn()
        {
            // Prefer center-ish floor that fits 3×3
            int cx = _grid.Width / 2;
            int cy = _grid.Height / 2;
            for (int r = 0; r < 20; r++)
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (_grid.CanFitUnit(x, y, UnitFootprint))
                    return new Vector2Int(x, y);
            }
            return new Vector2Int(cx, cy);
        }

        void DrawFootprint(Transform parent)
        {
            var ghost = ProceduralSprites.Floor(12);
            int half = UnitFootprint / 2;
            for (int dy = -half; dy <= half; dy++)
            for (int dx = -half; dx <= half; dx++)
            {
                var go = new GameObject("fp");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(dx, dy, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ghost;
                sr.sortingOrder = 5;
                sr.color = new Color(1f, 0.75f, 0.2f, 0.18f);
            }
        }

        void BuildGridOverlay()
        {
            var line = ProceduralSprites.Floor(8);
            for (int y = 0; y < _grid.Height; y++)
            for (int x = 0; x < _grid.Width; x++)
            {
                var go = new GameObject("g");
                go.transform.SetParent(_gridOverlay, false);
                go.transform.position = _grid.CellToWorld(x, y);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = line;
                sr.sortingOrder = 50;
                sr.color = new Color(1f, 1f, 1f, 0.1f);
                go.transform.localScale = Vector3.one * 0.95f;
            }
        }

        void SetupCameraAndLight()
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
            // Dark void — transparent rock cutouts must show brown floor, not blend into bg
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.025f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            if (cam.GetComponent<TrackpadCameraController>() == null)
                cam.gameObject.AddComponent<TrackpadCameraController>();

            foreach (var existing in FindObjectsByType<Light2D>())
            {
                if (existing.lightType != Light2D.LightType.Global) continue;
                existing.intensity = globalLightIntensity;
                existing.color = new Color(0.55f, 0.65f, 0.85f);
                return;
            }

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = globalLightIntensity;
            light.color = new Color(0.55f, 0.65f, 0.85f);
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(1f, 0.92f, 0.75f, 0.95f) }
            };
            string presetName = _preset switch
            {
                2 => "wide tunnel",
                3 => "pointed / diagonal",
                _ => "SHOWCASE all 8",
            };
            GUI.Label(new Rect(12, 10, 1100, 70),
                $"8-TOPOLOGY  |  {presetName}  |  excavator 3×3\n" +
                "1 = showcase (labels) · 2 = tunnel · 3 = pointed · G grid · Tab kind colors\n" +
                "Fritlagt alpha from your art · floor shows through cutouts",
                style);
        }
    }
}
