using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Side-by-side scale compare: 12 / 10 / 8 / 6 / 4 cells across one excavator.
    /// Same excavator world-size; finer grid = smaller cells. Input is mirrored.
    /// </summary>
    public sealed class FreeMovementScaleCompareRunner : MonoBehaviour
    {
        static readonly int[] Scales = { 12, 10, 8, 6, 4 };

        [SerializeField] float excavatorDiameter = 1.2f; // fixed visual size in all panels
        [SerializeField] float panelWorldW = 18f;
        [SerializeField] float panelWorldH = 14f;
        [SerializeField] float panelGap = 0.7f;
        [SerializeField] float cameraSize = 8.5f;

        struct Panel
        {
            public int Scale;
            public FineTerrainWorld World;
            public FreeWorkerController Worker;
            public Transform Root;
            public Transform GoalMarker;
            public Transform LooseRoot;
            public Vector2 Origin; // bottom-left of panel in scene
            public TextMesh Label;
        }

        Panel[] _panels;
        Sprite _pixel;
        Sprite _excavatorSprite;
        Sprite _goalSprite;

        float Radius => excavatorDiameter * 0.5f;

        void Start() => Build();

        void Build()
        {
            EnsureCamera();
            EnsureGlobalLight();
            _pixel = MakePixel();
            _excavatorSprite = MakeExcavatorSprite();
            _goalSprite = MakeGoalSprite();

            var root = new GameObject("ScaleCompare").transform;
            _panels = new Panel[Scales.Length];
            float cursorX = 0f;

            for (int i = 0; i < Scales.Length; i++)
            {
                int scale = Scales[i];
                float cell = excavatorDiameter / scale;
                int tw = Mathf.RoundToInt(panelWorldW / cell);
                int th = Mathf.RoundToInt(panelWorldH / cell);

                float originX = cursorX;
                var panelRoot = new GameObject($"Panel_{scale}x{scale}").transform;
                panelRoot.SetParent(root);
                panelRoot.position = new Vector3(originX, 0f, 0f);

                var world = new FineTerrainWorld(tw, th, cell);
                CarveCompareMap(world);
                Vector2 size = world.WorldSize;
                cursorX += size.x + panelGap;

                var viewGo = new GameObject("TerrainView");
                viewGo.transform.SetParent(panelRoot, false);
                viewGo.AddComponent<FreeMovementTerrainView>().Setup(world);

                var goal = new GameObject("Goal").transform;
                goal.SetParent(panelRoot, false);
                var gsr = goal.gameObject.AddComponent<SpriteRenderer>();
                gsr.sprite = _goalSprite;
                gsr.sortingOrder = 35;
                goal.localScale = Vector3.one * 0.45f;
                goal.gameObject.SetActive(false);

                var workerGo = new GameObject("Excavator");
                workerGo.transform.SetParent(panelRoot, false);
                var sr = workerGo.AddComponent<SpriteRenderer>();
                sr.sprite = _excavatorSprite;
                sr.sortingOrder = 40;
                workerGo.transform.localScale = Vector3.one * excavatorDiameter;

                var light = workerGo.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.color = new Color(1f, 0.72f, 0.35f);
                light.intensity = 1.05f;
                light.pointLightInnerRadius = 0.08f;
                light.pointLightOuterRadius = 2.0f;

                var worker = workerGo.AddComponent<FreeWorkerController>();
                Vector2 startLocal = new Vector2(size.x * 0.22f, size.y * 0.38f);

                var looseRoot = new GameObject("Loose").transform;
                looseRoot.SetParent(panelRoot, false);

                int panelIndex = i;
                worker.Setup(world, startLocal, Radius, goal, (x, y) =>
                {
                    var p = _panels[panelIndex];
                    var cell = p.World.Get(x, y);
                    Vector2 under = p.Worker.Position + Random.insideUnitCircle * (Radius * 0.4f);
                    LoosePile.Spawn(p.LooseRoot, _pixel, under, cell.Mass, cell.GoldGrade, p.World.CellSize, Radius);
                });

                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(panelRoot, false);
                labelGo.transform.localPosition = new Vector3(size.x * 0.5f, size.y + 0.35f, 0f);
                var tm = labelGo.AddComponent<TextMesh>();
                tm.text = $"{scale}×{scale}";
                tm.characterSize = 0.12f;
                tm.fontSize = 64;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = new Color(1f, 0.88f, 0.55f);
                var mr = labelGo.GetComponent<MeshRenderer>();
                if (mr != null) mr.sortingOrder = 60;

                BuildFootprint(panelRoot, workerGo.transform, Radius);
                PlaceLantern(panelRoot, new Vector2(size.x * 0.25f, size.y * 0.42f));

                _panels[i] = new Panel
                {
                    Scale = scale,
                    World = world,
                    Worker = worker,
                    Root = panelRoot,
                    GoalMarker = goal,
                    LooseRoot = looseRoot,
                    Origin = new Vector2(originX, 0f),
                    Label = tm,
                };
            }

            FrameCamera();
            Debug.Log("[ScaleCompare] 12/10/8/6/4 mirrored · LMB goal · WASD move");
        }

        /// <summary>Same relative carve in every panel (world units 0..W / 0..H).</summary>
        void CarveCompareMap(FineTerrainWorld w)
        {
            w.ResetAllSolidRock();
            int tw = w.Width, th = w.Height;
            // border
            for (int x = 0; x < tw; x++)
            {
                w.Set(x, 0, FineTerrainWorld.MakeBedrock());
                w.Set(x, th - 1, FineTerrainWorld.MakeBedrock());
            }
            for (int y = 0; y < th; y++)
            {
                w.Set(0, y, FineTerrainWorld.MakeBedrock());
                w.Set(tw - 1, y, FineTerrainWorld.MakeBedrock());
            }

            ExcavateNorm(w, 0.06f, 0.14f, 0.32f, 0.42f); // start chamber
            ExcavateNorm(w, 0.32f, 0.24f, 0.72f, 0.40f); // wide east tunnel
            ExcavateNorm(w, 0.32f, 0.48f, 0.55f, 0.58f); // mid pocket
            ExcavateNorm(w, 0.55f, 0.52f, 0.88f, 0.68f); // upper gallery
            ExcavateNorm(w, 0.12f, 0.55f, 0.28f, 0.78f); // north spur
            CarveDiagNorm(w, 0.10f, 0.42f, 0.48f, 0.88f, 0.055f);
            CarvePointNorm(w, 0.70f, 0.30f, 0.22f, 0.07f, 0.015f);
            CarveDiagNorm(w, 0.48f, 0.68f, 0.90f, 0.88f, 0.045f);

            // Gold veins in solid rock (dig into them)
            GoldVeinPlacer.PlaceVein(w, 0.35f, 0.10f, 0.70f, 0.22f, 0.018f, seed: 101);
            GoldVeinPlacer.PlaceVein(w, 0.58f, 0.40f, 0.92f, 0.55f, 0.016f, seed: 202);
            GoldVeinPlacer.PlaceVein(w, 0.15f, 0.80f, 0.55f, 0.92f, 0.02f, seed: 303);
            GoldVeinPlacer.PlaceCluster(w, 0.82f, 0.18f, 0.06f, 18, seed: 404);
            GoldVeinPlacer.PlaceCluster(w, 0.40f, 0.72f, 0.05f, 14, seed: 505);
        }

        void ExcavateNorm(FineTerrainWorld w, float x0, float y0, float x1, float y1)
        {
            int ix0 = Mathf.RoundToInt(x0 * w.Width);
            int iy0 = Mathf.RoundToInt(y0 * w.Height);
            int ix1 = Mathf.RoundToInt(x1 * w.Width);
            int iy1 = Mathf.RoundToInt(y1 * w.Height);
            w.ExcavateRect(ix0, iy0, Mathf.Max(1, ix1 - ix0), Mathf.Max(1, iy1 - iy0));
        }

        void CarveDiagNorm(FineTerrainWorld w, float x0, float y0, float x1, float y1, float halfNorm)
        {
            int half = Mathf.Max(1, Mathf.RoundToInt(halfNorm * w.Height));
            int ax = Mathf.RoundToInt(x0 * w.Width);
            int ay = Mathf.RoundToInt(y0 * w.Height);
            int bx = Mathf.RoundToInt(x1 * w.Width);
            int by = Mathf.RoundToInt(y1 * w.Height);
            w.BeginBatch();
            int steps = Mathf.Max(Mathf.Abs(bx - ax), Mathf.Abs(by - ay)) * 2;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int cx = Mathf.RoundToInt(Mathf.Lerp(ax, bx, t));
                int cy = Mathf.RoundToInt(Mathf.Lerp(ay, by, t));
                for (int oy = -half; oy <= half; oy++)
                for (int ox = -half; ox <= half; ox++)
                {
                    if (ox * ox + oy * oy > half * half) continue;
                    w.InstantExcavate(cx + ox, cy + oy, notify: false);
                }
            }
            w.EndBatch();
        }

        void CarvePointNorm(FineTerrainWorld w, float x0, float yMid, float lengthNorm, float startHalfN, float endHalfN)
        {
            int xStart = Mathf.RoundToInt(x0 * w.Width);
            int len = Mathf.RoundToInt(lengthNorm * w.Width);
            int y = Mathf.RoundToInt(yMid * w.Height);
            int sh = Mathf.Max(1, Mathf.RoundToInt(startHalfN * w.Height));
            int eh = Mathf.Max(1, Mathf.RoundToInt(endHalfN * w.Height));
            w.BeginBatch();
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)Mathf.Max(1, len - 1);
                int half = Mathf.RoundToInt(Mathf.Lerp(sh, eh, t));
                for (int oy = -half; oy <= half; oy++)
                    w.InstantExcavate(xStart + i, y + oy, notify: false);
            }
            w.EndBatch();
        }

        void Update()
        {
            if (_panels == null) return;
            var kb = Keyboard.current;
            Vector2 wasd = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) wasd.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) wasd.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) wasd.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) wasd.x += 1f;
                if (kb.rKey.wasPressedThisFrame) RebuildAllMaps();
                if (kb.escapeKey.wasPressedThisFrame)
                    foreach (var p in _panels) p.Worker.ClearGoal();
            }

            // Mirrored drive
            foreach (var p in _panels)
                p.Worker.Tick(wasd, clearGoalOnWasd: true);

            HandleMouseGoal();
        }

        void HandleMouseGoal()
        {
            var mouse = Mouse.current;
            var cam = Camera.main;
            var kb = Keyboard.current;
            if (mouse == null || cam == null) return;
            // Don't set dig goals while camera-panning
            if (kb != null && (kb.spaceKey.isPressed || kb.leftAltKey.isPressed || kb.rightAltKey.isPressed)) return;
            if (mouse.middleButton.isPressed) return;
            if (!mouse.leftButton.wasPressedThisFrame) return;

            Vector2 screen = mouse.position.ReadValue();
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));

            int hit = -1;
            Vector2 local = default;
            for (int i = 0; i < _panels.Length; i++)
            {
                var size = _panels[i].World.WorldSize;
                var o = _panels[i].Origin;
                if (world.x >= o.x && world.x <= o.x + size.x &&
                    world.y >= o.y && world.y <= o.y + size.y)
                {
                    hit = i;
                    local = new Vector2(world.x - o.x, world.y - o.y);
                    break;
                }
            }
            if (hit < 0) return;

            // Normalized UV → mirrored goal on every panel
            var hitSize = _panels[hit].World.WorldSize;
            float u = Mathf.Clamp01(local.x / hitSize.x);
            float v = Mathf.Clamp01(local.y / hitSize.y);
            foreach (var p in _panels)
            {
                var size = p.World.WorldSize;
                p.Worker.SetGoal(new Vector2(u * size.x, v * size.y));
            }
        }

        void RebuildAllMaps()
        {
            foreach (var p in _panels)
            {
                CarveCompareMap(p.World);
                for (int i = p.LooseRoot.childCount - 1; i >= 0; i--)
                    Destroy(p.LooseRoot.GetChild(i).gameObject);
                var size = p.World.WorldSize;
                p.Worker.Setup(p.World, new Vector2(size.x * 0.22f, size.y * 0.38f), Radius, p.GoalMarker,
                    (x, y) =>
                    {
                        var cell = p.World.Get(x, y);
                        Vector2 under = p.Worker.Position + Random.insideUnitCircle * (Radius * 0.4f);
                        LoosePile.Spawn(p.LooseRoot, _pixel, under, cell.Mass, cell.GoldGrade, p.World.CellSize, Radius);
                    });
            }
        }

        void BuildFootprint(Transform parent, Transform follow, float radius)
        {
            var go = new GameObject("Footprint");
            go.transform.SetParent(parent);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = new Color(0.4f, 0.85f, 1f, 0.35f);
            lr.startWidth = lr.endWidth = 0.025f;
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.sortingOrder = 50;
            int seg = 36;
            lr.positionCount = seg;
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
            var sync = go.AddComponent<FollowLocal>();
            sync.Target = follow;
        }

        void PlaceLantern(Transform parent, Vector2 local)
        {
            var go = new GameObject("Lantern");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _pixel;
            sr.color = new Color(1f, 0.7f, 0.3f);
            sr.sortingOrder = 25;
            go.transform.localScale = Vector3.one * 0.12f;
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(1f, 0.6f, 0.28f);
            light.intensity = 1.2f;
            light.pointLightOuterRadius = 2.4f;
            go.AddComponent<CosyLantern>().Init(go.transform.position, light, 1.2f);
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
            cam.backgroundColor = new Color(0.015f, 0.015f, 0.02f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            if (cam.GetComponent<UniversalAdditionalCameraData>() == null)
                cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            if (cam.GetComponent<TrackpadPan>() == null)
                cam.gameObject.AddComponent<TrackpadPan>();
            var pan = cam.GetComponent<TrackpadPan>();
            pan.MinOrtho = 2f;
            pan.MaxOrtho = 55f;
            pan.ZoomSpeed = 0.12f;
            pan.KeyPanSpeed = 10f;
        }

        void FrameCamera()
        {
            var cam = Camera.main;
            if (cam == null || _panels == null || _panels.Length == 0) return;
            // Start on the first panel so you can pan/zoom across sizes
            var size0 = _panels[0].World.WorldSize;
            var o0 = _panels[0].Origin;
            cam.transform.position = new Vector3(o0.x + size0.x * 0.5f, o0.y + size0.y * 0.45f, -10f);
            cam.orthographicSize = Mathf.Max(3.8f, size0.y * 0.58f);
        }

        void EnsureGlobalLight()
        {
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            {
                if (l.lightType != Light2D.LightType.Global) continue;
                l.intensity = 0.16f;
                l.color = new Color(0.5f, 0.58f, 0.75f);
                return;
            }
            var go = new GameObject("Global Light 2D");
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 0.16f;
            light.color = new Color(0.5f, 0.58f, 0.75f);
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

        static Sprite MakeExcavatorSprite()
        {
            const int s = 48;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, new Color(0, 0, 0, 0));
            void Fill(int x0, int y0, int w, int h, Color c)
            {
                for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    if (x >= 0 && y >= 0 && x < s && y < s) tex.SetPixel(x, y, c);
            }
            Fill(8, 10, 32, 22, new Color(0.88f, 0.55f, 0.12f));
            Fill(14, 20, 12, 10, new Color(0.35f, 0.42f, 0.5f));
            Fill(18, 32, 12, 10, new Color(0.65f, 0.65f, 0.7f));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(1f, 0.9f, 0.7f) }
            };
            GUI.Label(new Rect(10, 8, 1200, 70),
                "SCALE COMPARE  |  bigger panels  ·  gold veins in walls  ·  loose rock on dig\n" +
                "WASD + LMB dig (mirrored)  ·  piles land under excavator  ·  Esc clear  ·  R reset\n" +
                "CAMERA: Arrow keys  ·  Space+LMB / Alt+LMB / MMB drag  ·  scroll zoom",
                style);
        }
    }

    sealed class FollowLocal : MonoBehaviour
    {
        public Transform Target;
        void LateUpdate()
        {
            if (Target != null)
                transform.position = Target.position;
        }
    }
}
