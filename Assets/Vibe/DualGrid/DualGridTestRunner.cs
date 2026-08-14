using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace DeepCore.DualGrid
{
    /// <summary>
    /// DualGridTest — fluid cave silhouette over square dig data.
    /// </summary>
    public sealed class DualGridTestRunner : MonoBehaviour
    {
        const int NavW = 10;
        const int NavH = 8;

        [SerializeField] float cameraSize = 5.5f;

        DualGridWorld _world;
        DualGridWorker _worker;
        DualGridExcavator _excavator;
        Transform _fineGridRoot;
        Transform _navGridRoot;
        bool _showFineGrid;
        bool _showNavGrid;

        void Start() => Build();

        void Build()
        {
            EnsureCamera();
            EnsureGlobalLight();

            DualGridArt.Reload();

            var root = new GameObject("DualGridWorld").transform;

            _world = new DualGridWorld(NavW, NavH);
            SeedStartingArea();

            var fluidGo = new GameObject("FluidView");
            fluidGo.transform.SetParent(root, false);
            fluidGo.AddComponent<DualGridFluidView>().Setup(_world);

            _fineGridRoot = BuildGridOverlay(
                root, "FineGrid",
                _world.TerrainWidth, _world.TerrainHeight,
                1f / DualGridWorld.TerrainPerNav,
                new Color(1f, 0.9f, 0.7f, 0.1f),
                _showFineGrid);

            _navGridRoot = BuildGridOverlay(
                root, "NavGrid",
                _world.NavWidth, _world.NavHeight,
                1f,
                new Color(0.4f, 0.85f, 1f, 0.28f),
                _showNavGrid);

            SpawnTorches(root);
            SpawnWorker(root);
            SpawnExcavator(root);

            FrameCamera();
            Debug.Log("[DualGrid] LMB = straight drill goal (any angle) · tip wedge · 5 rock stages");
        }

        void SpawnTorches(Transform root)
        {
            PlaceTorch(root, new Vector3(1.5f, 1.5f, 0f));
            PlaceTorch(root, new Vector3(0.5f, 2.5f, 0f));
            PlaceTorch(root, new Vector3(2.5f, 0.5f, 0f));
        }

        static void PlaceTorch(Transform root, Vector3 pos)
        {
            var go = new GameObject("Torch");
            go.transform.SetParent(root);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = DualGridArt.Torch();
            sr.color = Color.white;
            sr.sortingOrder = 25;
            go.transform.localScale = Vector3.one * 0.35f;

            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(1f, 0.62f, 0.28f);
            light.intensity = 1.45f;
            light.pointLightInnerRadius = 0.2f;
            light.pointLightOuterRadius = 2.6f;
            light.falloffIntensity = 0.4f;
        }

        void SpawnWorker(Transform root)
        {
            var workerGo = new GameObject("Worker");
            workerGo.transform.SetParent(root);
            var sr = workerGo.AddComponent<SpriteRenderer>();
            sr.sprite = DualGridArt.Worker();
            sr.color = Color.white;
            sr.sortingOrder = 40;
            workerGo.transform.localScale = Vector3.one * 0.55f;
            _worker = workerGo.AddComponent<DualGridWorker>();
            _worker.Setup(_world, new Vector2Int(1, 1));

            var light = workerGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(0.7f, 0.9f, 1f);
            light.intensity = 0.55f;
            light.pointLightInnerRadius = 0.05f;
            light.pointLightOuterRadius = 0.9f;
        }

        void SpawnExcavator(Transform root)
        {
            var go = new GameObject("Excavator");
            go.transform.SetParent(root);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = DualGridArt.Excavator();
            sr.color = Color.white;
            sr.sortingOrder = 45;
            go.transform.localScale = Vector3.one * 0.75f;

            _excavator = go.AddComponent<DualGridExcavator>();
            _excavator.Setup(_world, new Vector2Int(2, 1));

            var marker = new GameObject("GoalMarker");
            marker.transform.SetParent(root);
            var msr = marker.AddComponent<SpriteRenderer>();
            msr.sprite = DualGridArt.Goal();
            msr.sortingOrder = 35;
            marker.transform.localScale = Vector3.one * 0.7f;
            marker.SetActive(false);
            _excavator.SetGoalMarker(marker);
        }

        void SeedStartingArea()
        {
            for (int ny = 0; ny < 3; ny++)
            for (int nx = 0; nx < 3; nx++)
                _world.ExcavateNavCell(nx, ny);
        }

        void ResetWorld()
        {
            _world.ResetAllSolid();
            SeedStartingArea();
            _worker.Setup(_world, new Vector2Int(1, 1));
            _excavator.Setup(_world, new Vector2Int(2, 1));
            _excavator.ClearGoal();
        }

        void Update()
        {
            if (_world == null) return;
            HandleKeyboard();
            HandleMouse();
        }

        void HandleKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.gKey.wasPressedThisFrame)
            {
                _showFineGrid = !_showFineGrid;
                if (_fineGridRoot != null) _fineGridRoot.gameObject.SetActive(_showFineGrid);
            }
            if (kb.nKey.wasPressedThisFrame)
            {
                _showNavGrid = !_showNavGrid;
                if (_navGridRoot != null) _navGridRoot.gameObject.SetActive(_showNavGrid);
            }
            if (kb.rKey.wasPressedThisFrame) ResetWorld();
            if (kb.escapeKey.wasPressedThisFrame) _excavator.ClearGoal();

            bool anyStep =
                kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame ||
                kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame ||
                kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame ||
                kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame;

            if (anyStep)
            {
                int x = 0, y = 0;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y++;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y--;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x--;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x++;
                x = Mathf.Clamp(x, -1, 1);
                y = Mathf.Clamp(y, -1, 1);
                if (x != 0 || y != 0)
                    _worker.TryStep(new Vector2Int(x, y));
            }
        }

        void HandleMouse()
        {
            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse == null || cam == null) return;

            Vector2 screen = mouse.position.ReadValue();
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            world.z = 0f;

            bool alt = Keyboard.current != null &&
                       (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (!alt)
                {
                    // Straight drill toward click — any angle, no tile pathfinding
                    _excavator.SetGoalWorld(world);
                    return;
                }

                var nav = _world.WorldToNav(world);
                if (!_world.InNavBounds(nav.x, nav.y)) return;
                var t = _world.WorldToTerrain(world);
                if (!_world.InTerrainBounds(t.x, t.y)) return;
                if (_world.GetTerrain(t.x, t.y) == TerrainState.Excavated)
                    _worker.CommandMoveTo(nav);
                else
                    _world.DamageTerrain(t.x, t.y);
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                var t = _world.WorldToTerrain(world);
                _world.ExcavateTerrain(t.x, t.y);
            }
        }

        Transform BuildGridOverlay(Transform parent, string name, int cellsX, int cellsY, float cellSize, Color color, bool active)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent);
            float w = cellsX * cellSize;
            float h = cellsY * cellSize;
            float width = Mathf.Clamp(0.01f * cellSize * 5f, 0.006f, 0.025f);

            for (int x = 0; x <= cellsX; x++)
            {
                var lr = MakeLine(root, color, width);
                float xf = x * cellSize;
                lr.positionCount = 2;
                lr.SetPosition(0, new Vector3(xf, 0f, 0f));
                lr.SetPosition(1, new Vector3(xf, h, 0f));
            }
            for (int y = 0; y <= cellsY; y++)
            {
                var lr = MakeLine(root, color, width);
                float yf = y * cellSize;
                lr.positionCount = 2;
                lr.SetPosition(0, new Vector3(0f, yf, 0f));
                lr.SetPosition(1, new Vector3(w, yf, 0f));
            }

            root.gameObject.SetActive(active);
            return root;
        }

        static LineRenderer MakeLine(Transform parent, Color color, float width)
        {
            var go = new GameObject("line");
            go.transform.SetParent(parent);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = color;
            lr.startWidth = lr.endWidth = width;
            lr.useWorldSpace = true;
            lr.sortingOrder = 50;
            return lr;
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
            cam.backgroundColor = new Color(0.012f, 0.012f, 0.016f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            if (cam.GetComponent<UniversalAdditionalCameraData>() == null)
                cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }

        void FrameCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.transform.position = new Vector3(NavW * 0.45f, NavH * 0.4f, -10f);
            cam.orthographicSize = cameraSize;
        }

        void EnsureGlobalLight()
        {
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            {
                if (l.lightType != Light2D.LightType.Global) continue;
                l.intensity = 0.18f;
                l.color = new Color(0.5f, 0.58f, 0.72f);
                return;
            }
            var go = new GameObject("Global Light 2D");
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 0.18f;
            light.color = new Color(0.5f, 0.58f, 0.72f);
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(1f, 0.92f, 0.75f) }
            };
            GUI.Label(new Rect(10, 8, 980, 90),
                "DEEP CORE · continuous drill  |  art: Assets/Vibe/Art/DualGrid/\n" +
                "LMB: drill goal  ·  Alt+LMB: worker/damage  ·  RMB: excavate  ·  Esc/R/G/N\n" +
                "Drop Floor.png · Rock.png · RockDamage.png · Excavator.png · Worker.png · Goal.png · Torch.png",
                style);
        }
    }
}
