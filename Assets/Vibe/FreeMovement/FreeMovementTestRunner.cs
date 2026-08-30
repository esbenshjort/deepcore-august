using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Cosy free-movement dig test — big map, click-to-drive excavator, swaying lanterns.
    /// </summary>
    public sealed class FreeMovementTestRunner : MonoBehaviour
    {
        public const int CellsAcrossWorker = 12;

        [SerializeField] float cameraSize = 6.2f;
        [SerializeField] float cellSize = 0.1f;
        [SerializeField] float cameraFollow = 4f;

        FineTerrainWorld _world;
        FreeWorkerController _worker;
        Transform _worldRoot;
        Transform _gridRoot;
        Transform _footprintRing;
        Transform _looseRoot;
        Transform _goalMarker;
        Sprite _pixel;
        bool _showGrid;
        bool _showFootprint = true;
        bool _showMouseData = true;
        string _mouseDebug = "";
        Vector2Int _hover = new(-1, -1);

        float WorkerRadius => CellsAcrossWorker * 0.5f * cellSize;

        void Start() => Build();

        void Build()
        {
            EnsureCamera();
            EnsureGlobalLight(0.12f); // dark & cozy
            _pixel = MakePixel();

            // Bigger map
            int tw = 260;
            int th = 190;
            _world = new FineTerrainWorld(tw, th, cellSize);
            BuildTestMap();

            _worldRoot = new GameObject("FreeMovementWorld").transform;
            _looseRoot = new GameObject("Loose").transform;
            _looseRoot.SetParent(_worldRoot);

            var viewGo = new GameObject("TerrainView");
            viewGo.transform.SetParent(_worldRoot, false);
            viewGo.AddComponent<FreeMovementTerrainView>().Setup(_world);

            _gridRoot = BuildGrid(_worldRoot);
            _gridRoot.gameObject.SetActive(_showGrid);

            SpawnLanterns(_worldRoot);
            SpawnWorker(_worldRoot);
            _footprintRing = BuildFootprintRing(_worldRoot);
            _footprintRing.gameObject.SetActive(_showFootprint);

            FrameCamera();
            Debug.Log("[FreeMovement] LMB=drive+dig goal · WASD manual · cosy lanterns");
        }

        void BuildTestMap()
        {
            _world.ResetAllSolidRock();
            for (int x = 0; x < _world.Width; x++)
            {
                _world.Set(x, 0, FineTerrainWorld.MakeBedrock());
                _world.Set(x, _world.Height - 1, FineTerrainWorld.MakeBedrock());
            }
            for (int y = 0; y < _world.Height; y++)
            {
                _world.Set(0, y, FineTerrainWorld.MakeBedrock());
                _world.Set(_world.Width - 1, y, FineTerrainWorld.MakeBedrock());
            }

            // A) Large cosy chamber
            _world.ExcavateRect(20, 20, 55, 40);
            // B) Wide tunnel east
            _world.ExcavateRect(75, 32, 90, 20);
            // C) Narrow blocked tunnel
            _world.ExcavateRect(75, 70, 55, 9);
            // D) Diagonal
            CarveDiagonal(24, 65, 90, 140, halfWidth: 10);
            // E) Pointed
            CarvePointed(75, 100, length: 70, startHalf: 12, endHalf: 2);
            // F) Irregular caverns
            CarveIrregular(180, 50, 22);
            CarveIrregular(150, 130, 18);
            CarveIrregular(210, 150, 16);
            CarveIrregular(220, 70, 14);
            // Connecting pockets
            _world.ExcavateRect(140, 32, 28, 16);
            _world.ExcavateRect(40, 120, 35, 22);
            _world.ExcavateRect(180, 100, 40, 14);

            // Gold veins / clusters in solid rock
            GoldVeinPlacer.PlaceVein(_world, 0.28f, 0.08f, 0.55f, 0.18f, 0.012f, seed: 11);
            GoldVeinPlacer.PlaceVein(_world, 0.55f, 0.28f, 0.88f, 0.42f, 0.01f, seed: 22);
            GoldVeinPlacer.PlaceVein(_world, 0.12f, 0.55f, 0.40f, 0.75f, 0.014f, seed: 33);
            GoldVeinPlacer.PlaceVein(_world, 0.60f, 0.70f, 0.92f, 0.88f, 0.012f, seed: 44);
            GoldVeinPlacer.PlaceCluster(_world, 0.78f, 0.15f, 0.04f, 22, seed: 55);
            GoldVeinPlacer.PlaceCluster(_world, 0.35f, 0.85f, 0.045f, 20, seed: 66);
            GoldVeinPlacer.PlaceCluster(_world, 0.90f, 0.55f, 0.035f, 16, seed: 77);
            SprinkleGold(90, 22, 40, 30);
        }

        void SprinkleGold(int x0, int y0, int w, int h)
        {
            var rng = new System.Random(42);
            for (int i = 0; i < 28; i++)
            {
                int x = x0 + rng.Next(w);
                int y = y0 + rng.Next(h);
                if (!_world.InBounds(x, y) || _world.IsExcavated(x, y)) continue;
                _world.Set(x, y, FineTerrainWorld.MakeRock(4, 5, (byte)rng.Next(1, 16), (byte)rng.Next(1, 5)));
            }
        }

        void CarveDiagonal(int x0, int y0, int x1, int y1, int halfWidth)
        {
            _world.BeginBatch();
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * 2;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int cx = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int cy = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (int oy = -halfWidth; oy <= halfWidth; oy++)
                for (int ox = -halfWidth; ox <= halfWidth; ox++)
                {
                    if (ox * ox + oy * oy > halfWidth * halfWidth) continue;
                    _world.InstantExcavate(cx + ox, cy + oy, notify: false);
                }
            }
            _world.EndBatch();
        }

        void CarvePointed(int x0, int yMid, int length, int startHalf, int endHalf)
        {
            _world.BeginBatch();
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)Mathf.Max(1, length - 1);
                int half = Mathf.RoundToInt(Mathf.Lerp(startHalf, endHalf, t));
                for (int oy = -half; oy <= half; oy++)
                    _world.InstantExcavate(x0 + i, yMid + oy, notify: false);
            }
            _world.EndBatch();
        }

        void CarveIrregular(int cx, int cy, int radius)
        {
            _world.BeginBatch();
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.18f + cx * 0.01f, y * 0.18f + cy * 0.01f);
                float r = radius * (0.5f + n * 0.6f);
                if (x * x + y * y <= r * r)
                    _world.InstantExcavate(cx + x, cy + y, notify: false);
            }
            _world.EndBatch();
        }

        void SpawnLanterns(Transform root)
        {
            // Chamber cluster
            PlaceLantern(root, _world.CellCenter(24, 24));
            PlaceLantern(root, _world.CellCenter(40, 30));
            PlaceLantern(root, _world.CellCenter(30, 38));
            // Wide tunnel
            PlaceLantern(root, _world.CellCenter(70, 34));
            PlaceLantern(root, _world.CellCenter(95, 34));
            // Side caverns
            PlaceLantern(root, _world.CellCenter(118, 40));
            PlaceLantern(root, _world.CellCenter(100, 85));
            PlaceLantern(root, _world.CellCenter(50, 70));
        }

        void PlaceLantern(Transform root, Vector2 pos)
        {
            DigVisualKit.PlaceLantern(root, pos, local: false, intensity: 1.55f);
        }

        void SpawnWorker(Transform root)
        {
            _goalMarker = new GameObject("Goal").transform;
            _goalMarker.SetParent(root);
            var gsr = _goalMarker.gameObject.AddComponent<SpriteRenderer>();
            gsr.sprite = MakeGoalSprite();
            gsr.sortingOrder = 35;
            _goalMarker.localScale = Vector3.one * 0.55f;
            _goalMarker.gameObject.SetActive(false);

            var go = new GameObject("Excavator");
            go.transform.SetParent(root);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeExcavatorSprite();
            sr.sortingOrder = 40;
            float diameter = CellsAcrossWorker * cellSize;
            go.transform.localScale = Vector3.one * diameter;

            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(1f, 0.75f, 0.4f);
            light.intensity = 1.1f;
            light.pointLightInnerRadius = 0.1f;
            light.pointLightOuterRadius = 2.2f;
            light.falloffIntensity = 0.5f;

            _worker = go.AddComponent<FreeWorkerController>();
            Vector2 start = _world.CellCenter(28, 28);
            _worker.Setup(_world, start, WorkerRadius, _goalMarker, SpawnLooseMarker);
        }

        void SpawnLooseMarker(int x, int y)
        {
            if (_worker == null || _looseRoot == null) return;
            var c = _world.Get(x, y);
            // Land under the excavator body (slight scatter) — not at the dug cell
            Vector2 under = _worker.Position + Random.insideUnitCircle * (_worker.Radius * 0.4f);
            LoosePile.SpawnCellFromDrill(_looseRoot, under, _worker.Facing, c, _world.CellSize, _worker.Radius, x, y);
        }

        Transform BuildFootprintRing(Transform parent)
        {
            var go = new GameObject("Footprint");
            go.transform.SetParent(parent);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = new Color(0.4f, 0.85f, 1f, 0.55f);
            lr.startWidth = lr.endWidth = 0.03f;
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.sortingOrder = 50;
            int seg = 48;
            lr.positionCount = seg;
            float r = WorkerRadius;
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
            return go.transform;
        }

        Transform BuildGrid(Transform parent)
        {
            var root = new GameObject("FineGrid").transform;
            root.SetParent(parent);
            var color = new Color(1f, 0.9f, 0.7f, 0.08f);
            float cs = _world.CellSize;
            float w = _world.Width * cs;
            float h = _world.Height * cs;
            for (int x = 0; x <= _world.Width; x += 6)
            {
                var lr = MakeLine(root, color, x % 12 == 0 ? 0.018f : 0.01f);
                float xf = x * cs;
                lr.positionCount = 2;
                lr.SetPosition(0, new Vector3(xf, 0, 0));
                lr.SetPosition(1, new Vector3(xf, h, 0));
            }
            for (int y = 0; y <= _world.Height; y += 6)
            {
                var lr = MakeLine(root, color, y % 12 == 0 ? 0.018f : 0.01f);
                float yf = y * cs;
                lr.positionCount = 2;
                lr.SetPosition(0, new Vector3(0, yf, 0));
                lr.SetPosition(1, new Vector3(w, yf, 0));
            }
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
            lr.sortingOrder = 45;
            return lr;
        }

        void Update()
        {
            if (_world == null || _worker == null) return;
            HandleKeyboard();
            HandleMovementAndGoal();
            HandleMouseDig();
            UpdateFootprintVisual();
            UpdateMouseDebug();
            FollowCamera();
        }

        void HandleMovementAndGoal()
        {
            var kb = Keyboard.current;
            Vector2 input = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
            }
            _worker.Tick(input);
        }

        void HandleKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.gKey.wasPressedThisFrame)
            {
                _showGrid = !_showGrid;
                if (_gridRoot != null) _gridRoot.gameObject.SetActive(_showGrid);
            }
            if (kb.cKey.wasPressedThisFrame)
            {
                _showFootprint = !_showFootprint;
                if (_footprintRing != null) _footprintRing.gameObject.SetActive(_showFootprint);
            }
            if (kb.mKey.wasPressedThisFrame) _showMouseData = !_showMouseData;
            if (kb.rKey.wasPressedThisFrame) ResetTest();
            if (kb.escapeKey.wasPressedThisFrame) _worker.ClearGoal();
        }

        void HandleMouseDig()
        {
            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse == null || cam == null) return;

            Vector2 screen = mouse.position.ReadValue();
            Vector3 world3 = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            Vector2 world = world3;
            _hover = _world.WorldToCell(world);

            bool alt = kbAlt();
            bool shift = kbShift();

            // LMB: set excavator goal (drive + dig tip). Shift+LMB = manual damage.
            if (mouse.leftButton.wasPressedThisFrame && !alt)
            {
                if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) return;
                if (shift)
                {
                    if (_world.InBounds(_hover.x, _hover.y) && _world.Damage(_hover.x, _hover.y))
                        SpawnLooseMarker(_hover.x, _hover.y);
                }
                else
                {
                    _worker.SetGoal(world);
                }
            }

            if (shift && mouse.leftButton.isPressed && Time.frameCount % 3 == 0)
            {
                if (_world.InBounds(_hover.x, _hover.y) && _world.Damage(_hover.x, _hover.y))
                    SpawnLooseMarker(_hover.x, _hover.y);
            }

            if (mouse.rightButton.wasPressedThisFrame && _world.InBounds(_hover.x, _hover.y))
            {
                if (_world.Get(_hover.x, _hover.y).Phase != TerrainPhase.Excavated)
                {
                    _world.InstantExcavate(_hover.x, _hover.y);
                    SpawnLooseMarker(_hover.x, _hover.y);
                }
            }
        }

        static bool kbAlt() =>
            Keyboard.current != null &&
            (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed);

        static bool kbShift() =>
            Keyboard.current != null &&
            (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);

        void UpdateFootprintVisual()
        {
            if (_footprintRing != null && _worker != null)
                _footprintRing.position = _worker.transform.position;
        }

        void FollowCamera()
        {
            var cam = Camera.main;
            if (cam == null || _worker == null) return;
            Vector3 target = _worker.transform.position;
            target.z = -10f;
            cam.transform.position = Vector3.Lerp(cam.transform.position, target, 1f - Mathf.Exp(-cameraFollow * Time.deltaTime));
        }

        void UpdateMouseDebug()
        {
            if (!_showMouseData) { _mouseDebug = ""; return; }
            if (!_world.InBounds(_hover.x, _hover.y))
            {
                _mouseDebug = $"cell {_hover} (oob)";
                return;
            }
            var c = _world.Get(_hover.x, _hover.y);
            _mouseDebug =
                $"cell ({_hover.x},{_hover.y})  {c.Phase}  {c.Material}\n" +
                $"dur {c.Durability}/{c.MaxDurability}  dmg {c.DamageState}  mass {c.Mass}\n" +
                $"sockets R{c.RockCount} B{c.BedrockCount} G{c.GoldCount}  goldBits 0b{System.Convert.ToString(c.GoldSockets, 2).PadLeft(4, '0')}";
        }

        void ResetTest()
        {
            for (int i = _looseRoot.childCount - 1; i >= 0; i--)
                Destroy(_looseRoot.GetChild(i).gameObject);
            BuildTestMap();
            _worker.Setup(_world, _world.CellCenter(28, 28), WorkerRadius, _goalMarker, SpawnLooseMarker);
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
            cam.backgroundColor = new Color(0.01f, 0.01f, 0.015f);
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

        void EnsureGlobalLight(float intensity)
        {
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            {
                if (l.lightType != Light2D.LightType.Global) continue;
                l.intensity = intensity;
                l.color = new Color(0.45f, 0.55f, 0.75f);
                return;
            }
            var go = new GameObject("Global Light 2D");
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = intensity;
            light.color = new Color(0.45f, 0.55f, 0.75f);
        }

        static Sprite MakePixel()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                tex.SetPixel(x, y, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }

        static Sprite MakeGoalSprite()
        {
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, new Color(0, 0, 0, 0));
            for (int y = 4; y < 22; y++)
            for (int x = 14; x <= 17; x++)
                tex.SetPixel(x, y, new Color(0.35f, 0.95f, 0.45f, 0.95f));
            for (int i = 0; i < 9; i++)
            for (int x = 16 - i; x <= 16 + i; x++)
                if (x >= 0 && x < s) tex.SetPixel(x, 28 - i, new Color(0.35f, 0.95f, 0.45f, 0.95f));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        static Sprite MakeExcavatorSprite()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, new Color(0, 0, 0, 0));
            Fill(tex, 10, 14, 44, 30, new Color(0.88f, 0.58f, 0.12f));
            Fill(tex, 14, 18, 36, 18, new Color(0.72f, 0.45f, 0.08f));
            Fill(tex, 20, 28, 16, 12, new Color(0.3f, 0.38f, 0.45f));
            Fill(tex, 22, 30, 12, 7, new Color(0.55f, 0.78f, 0.9f, 0.85f));
            Fill(tex, 8, 10, 48, 6, new Color(0.18f, 0.18f, 0.2f));
            Fill(tex, 8, 42, 48, 6, new Color(0.18f, 0.18f, 0.2f));
            Fill(tex, 26, 44, 12, 12, new Color(0.6f, 0.6f, 0.65f));
            Fill(tex, 29, 54, 6, 6, new Color(0.8f, 0.8f, 0.85f));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        static void Fill(Texture2D tex, int x0, int y0, int w, int h, Color c)
        {
            for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                if (x >= 0 && y >= 0 && x < tex.width && y < tex.height)
                    tex.SetPixel(x, y, c);
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(1f, 0.9f, 0.7f) }
            };
            GUI.Label(new Rect(10, 8, 1100, 72),
                "FreeMovement · cosy dig  |  map 260×190  |  excavator ≈ 12 cells  |  gold veins in walls\n" +
                "LMB: dig goal  ·  WASD: manual  ·  loose rock drops under excavator\n" +
                "Esc: clear  ·  G grid  ·  C footprint  ·  M data  ·  R reset  ·  scroll zoom",
                style);
            if (_showMouseData && !string.IsNullOrEmpty(_mouseDebug))
            {
                var box = new GUIStyle(GUI.skin.box) { fontSize = 12 };
                box.normal.textColor = new Color(0.9f, 0.95f, 1f);
                GUI.Box(new Rect(10, 82, 400, 58), _mouseDebug, box);
            }
        }
    }

    /// <summary>
    /// Camera pan/zoom for dig tests.
    /// MMB drag, Space+LMB / Alt+LMB drag, arrow keys · scroll zoom.
    /// </summary>
    public sealed class TrackpadPan : MonoBehaviour
    {
        public float MinOrtho = 2f;
        public float MaxOrtho = 45f;
        public float ZoomSpeed = 0.1f;
        public float KeyPanSpeed = 8f;

        void Update()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            var cam = GetComponent<Camera>();
            if (cam == null) return;

            if (mouse != null)
            {
                var scroll = mouse.scroll.ReadValue();
                if (Mathf.Abs(scroll.y) > 0.01f)
                    cam.orthographicSize = Mathf.Clamp(
                        cam.orthographicSize - scroll.y * ZoomSpeed, MinOrtho, MaxOrtho);

                bool space = kb != null && kb.spaceKey.isPressed;
                bool alt = kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);
                bool panDrag = mouse.middleButton.isPressed
                               || (space && mouse.leftButton.isPressed)
                               || (alt && mouse.leftButton.isPressed);
                if (panDrag)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    float scale = cam.orthographicSize * 0.0022f;
                    transform.position -= new Vector3(delta.x, delta.y, 0f) * scale;
                }
            }

            if (kb != null)
            {
                Vector2 key = Vector2.zero;
                if (kb.leftArrowKey.isPressed) key.x -= 1f;
                if (kb.rightArrowKey.isPressed) key.x += 1f;
                if (kb.upArrowKey.isPressed) key.y += 1f;
                if (kb.downArrowKey.isPressed) key.y -= 1f;
                if (key.sqrMagnitude > 0.01f)
                    transform.position += (Vector3)(key.normalized * KeyPanSpeed * cam.orthographicSize * 0.15f * Time.deltaTime);
            }
        }
    }
}
