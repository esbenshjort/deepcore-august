using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Temporary balance lab: six excavator profiles dig identical columns side-by-side.
    /// ROCK / BEDROCK = pure mining performance (conditions reset at start).
    /// ENDURANCE = operational rock→bedrock (conditions carry through).
    /// Also hosts automated seeded benchmarks. Does not change mining formulas.
    /// </summary>
    public sealed class ExcavatorBalanceCompareRunner : MonoBehaviour
    {
        static readonly ExcavatorTestProfile[] Profiles =
        {
            ExcavatorTestProfile.Ace,
            ExcavatorTestProfile.Brute,
            ExcavatorTestProfile.Technician,
            ExcavatorTestProfile.Professional,
            ExcavatorTestProfile.Cowboy,
            ExcavatorTestProfile.Green,
        };

        static readonly Color[] ProfileAccent =
        {
            new(1f, 0.92f, 0.35f, 1f),   // ACE
            new(1f, 0.55f, 0.25f, 1f),   // BRUTE
            new(0.25f, 0.92f, 1f, 1f),   // TECH
            new(0.35f, 1f, 0.55f, 1f),   // PRO
            new(1f, 0.35f, 0.4f, 1f),    // COWBOY
            new(0.55f, 0.55f, 0.6f, 1f), // GREEN
        };

        [SerializeField] float cellSize = 0.12f;
        [SerializeField] float cameraSize = 11.5f;
        [SerializeField] float laneGap = 0.45f;

        struct Lane
        {
            public ExcavatorTestProfile Profile;
            public FineTerrainWorld World;
            public FreeWorkerController Worker;
            public ExcavatorBalanceHarness Harness;
            public Transform Root;
            public Transform LooseRoot;
            public Vector2 Origin;
            public Vector2 StartLocal;
            public Vector2 GoalLocal;
            public Color Accent;
        }

        Lane[] _lanes;
        Sprite _goalSprite;
        Light2D _globalLight;
        bool _paused;
        ExcavatorBalanceTestMode _mode = ExcavatorBalanceTestMode.Endurance;

        readonly ExcavatorBalanceBenchmark _benchmark = new();
        bool _showBenchmarkResults;
        bool _autoOpenedResults;
        ExcavatorBalanceTestMode _resultsMode = ExcavatorBalanceTestMode.Rock;
        Vector2 _resultsScroll;

        float WorkerRadius => ExcavatorBalanceLabTerrain.CellsAcross * 0.5f * cellSize;

        void Awake() => _benchmark.Bind(this);

        void Start() => Build();

        void Build()
        {
            DigHoodLog.MirrorToConsole = false;
            DigHoodLog.Clear();
            EnsureCamera();
            EnsureGlobalLight();
            if (_goalSprite == null)
                _goalSprite = MakeGoalSprite();

            var root = new GameObject("BalanceCompare").transform;
            root.SetParent(transform, false);
            _lanes = new Lane[Profiles.Length];
            float cursorX = 0f;
            int layers = ExcavatorBalanceLabTerrain.MaterialLayers(_mode);
            int tw = ExcavatorBalanceLabTerrain.Width;
            int th = ExcavatorBalanceLabTerrain.WorldHeight(_mode);

            for (int i = 0; i < Profiles.Length; i++)
            {
                var laneRoot = new GameObject($"Lane_{ExcavatorBalanceHarness.ProfileLabel(Profiles[i])}").transform;
                laneRoot.SetParent(root, false);
                laneRoot.position = new Vector3(cursorX, 0f, 0f);

                var world = new FineTerrainWorld(tw, th, cellSize);
                ExcavatorBalanceLabTerrain.Carve(world, _mode);
                Vector2 size = world.WorldSize;
                cursorX += size.x + laneGap;

                var viewGo = new GameObject("TerrainView");
                viewGo.transform.SetParent(laneRoot, false);
                viewGo.AddComponent<FreeMovementTerrainView>()
                    .Setup(world, fogOfWarGold: false, strongCliffEdges: true);

                var loose = new GameObject("Loose").transform;
                loose.SetParent(laneRoot, false);

                Vector2 start = ExcavatorBalanceLabTerrain.StartLocal(world);
                Vector2 goal = ExcavatorBalanceLabTerrain.GoalLocal(world, _mode);
                int midX = tw / 2;
                int startY = ExcavatorBalanceLabTerrain.FloorClear - 2;

                var goalMark = new GameObject("Goal").transform;
                goalMark.SetParent(laneRoot, false);
                goalMark.localPosition = goal;
                var gsr = goalMark.gameObject.AddComponent<SpriteRenderer>();
                gsr.sprite = _goalSprite;
                gsr.color = ProfileAccent[i];
                gsr.sortingOrder = 35;
                goalMark.localScale = Vector3.one * 0.4f;

                PlaceLaneWorkLight(laneRoot, world, midX, startY, layers);

                var workerGo = new GameObject("Excavator");
                workerGo.transform.SetParent(laneRoot, false);
                var light = workerGo.AddComponent<Light2D>();
                DigVisualKit.ConfigurePointLight(light,
                    Color.Lerp(ProfileAccent[i], Color.white, 0.35f),
                    intensity: 2.4f,
                    outer: 3.2f,
                    inner: 0.15f,
                    shadows: false,
                    falloff: 0.55f);

                var worker = workerGo.AddComponent<FreeWorkerController>();
                int laneIndex = i;
                worker.Setup(world, start, WorkerRadius, null,
                    onBrokeCell: (x, y) => OnLaneBroke(laneIndex, x, y),
                    onDigImpact: null,
                    pinSprite: _goalSprite);
                DigVisualKit.AttachDrillerVisual(workerGo, worker);

                var harness = new ExcavatorBalanceHarness();
                harness.Bind(worker);
                harness.ApplyProfile(Profiles[i]);

                worker.SetGoal(goal);
                worker.transform.up = Vector2.up;

                _lanes[i] = new Lane
                {
                    Profile = Profiles[i],
                    World = world,
                    Worker = worker,
                    Harness = harness,
                    Root = laneRoot,
                    LooseRoot = loose,
                    Origin = new Vector2(cursorX - size.x - laneGap, 0f),
                    StartLocal = start,
                    GoalLocal = goal,
                    Accent = ProfileAccent[i],
                };
            }

            float totalW = cursorX - laneGap;
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(totalW * 0.5f, thWorldCenter(), -10f);
                cam.orthographicSize = cameraSize;
            }

            DigHoodLog.Push(
                $"{ExcavatorBalanceHarness.LogTag} | MODE {ExcavatorBalanceHarness.ModeLabel(_mode)} | " +
                $"{ExcavatorBalanceHarness.ModeKindLabel(_mode)}");
        }

        float thWorldCenter()
        {
            if (_lanes == null || _lanes.Length == 0) return 3f;
            var w = _lanes[0].World;
            return w != null ? w.WorldSize.y * 0.42f : 3f;
        }

        void OnLaneBroke(int lane, int x, int y)
        {
            if (_lanes == null || lane < 0 || lane >= _lanes.Length) return;
            var L = _lanes[lane];
            if (L.Worker == null || L.World == null || L.LooseRoot == null) return;
            var c = L.World.Get(x, y);
            Vector2 tip = L.Worker.DrillTip(0.35f);
            LoosePile.SpawnCellFromDrill(L.LooseRoot, tip, L.Worker.Facing, c,
                L.World.CellSize, WorkerRadius, x, y);
        }

        void Update()
        {
            if (_benchmark.IsRunning)
            {
                // Manual lanes idle during automated benchmark
                var kbBusy = Keyboard.current;
                if (kbBusy != null && kbBusy.escapeKey.wasPressedThisFrame)
                    _benchmark.Cancel();
                return;
            }

            if (_lanes == null) return;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
                    SetMode(ExcavatorBalanceTestMode.Rock);
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
                    SetMode(ExcavatorBalanceTestMode.Bedrock);
                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
                    SetMode(ExcavatorBalanceTestMode.Endurance);
                if (kb.rKey.wasPressedThisFrame) ResetAll();
                if (kb.pKey.wasPressedThisFrame) _paused = !_paused;
                if (kb.cKey.wasPressedThisFrame) ClearAllOverheats();
                if (kb.spaceKey.wasPressedThisFrame) RestartDigGoals();
                if (kb.bKey.wasPressedThisFrame && _benchmark.Session != null)
                    _showBenchmarkResults = !_showBenchmarkResults;
            }

            if (_paused) return;

            for (int i = 0; i < _lanes.Length; i++)
            {
                var L = _lanes[i];
                if (L.Worker == null) continue;
                if (!L.Worker.HasGoal)
                    L.Worker.SetGoal(L.GoalLocal);
                L.Worker.Tick(Vector2.zero, clearGoalOnWasd: false);
                L.Harness?.Tick(Time.deltaTime);
            }
        }

        void SetMode(ExcavatorBalanceTestMode mode)
        {
            if (_benchmark.IsRunning) return;
            if (_mode == mode && _lanes != null) return;
            _mode = mode;
            ResetAll();
        }

        void RestartDigGoals()
        {
            if (_lanes == null) return;
            for (int i = 0; i < _lanes.Length; i++)
            {
                var L = _lanes[i];
                if (L.Worker == null) continue;
                L.Worker.SetGoal(L.GoalLocal);
                L.Worker.transform.up = Vector2.up;
            }
        }

        void ClearAllOverheats()
        {
            if (_lanes == null) return;
            for (int i = 0; i < _lanes.Length; i++)
                _lanes[i].Worker?.ClearOverheatByEngineer();
        }

        void ResetAll()
        {
            if (_benchmark.IsRunning) return;
            if (transform.childCount > 0)
            {
                for (int i = transform.childCount - 1; i >= 0; i--)
                    DestroyImmediate(transform.GetChild(i).gameObject);
            }
            _lanes = null;
            Build();
        }

        void ResetMetricsOnly()
        {
            if (_lanes == null || _benchmark.IsRunning) return;
            for (int i = 0; i < _lanes.Length; i++)
            {
                var L = _lanes[i];
                L.Harness?.ResetRun();
                if (L.Worker != null)
                {
                    L.Worker.TeleportTo(L.StartLocal);
                    L.Worker.SetGoal(L.GoalLocal);
                    L.Worker.transform.up = Vector2.up;
                }
            }
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
                go.AddComponent<UniversalAdditionalCameraData>();
            }
            cam.orthographic = true;
            cam.orthographicSize = cameraSize;
            cam.backgroundColor = new Color(0.01f, 0.012f, 0.018f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        void EnsureGlobalLight()
        {
            if (_globalLight == null)
            {
                foreach (var l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
                {
                    if (l.lightType != Light2D.LightType.Global) continue;
                    _globalLight = l;
                    break;
                }
            }

            if (_globalLight == null)
            {
                var go = new GameObject("Global Light 2D");
                _globalLight = go.AddComponent<Light2D>();
                _globalLight.lightType = Light2D.LightType.Global;
            }

            _globalLight.intensity = 0.55f;
            _globalLight.color = new Color(0.72f, 0.78f, 0.88f);
        }

        static void PlaceLaneWorkLight(Transform laneRoot, FineTerrainWorld world, int midX, int startY, int layers)
        {
            Vector2 digMid = world.CellCenter(midX, startY + layers / 2);

            void Add(string name, Vector2 local, float intensity, float outer)
            {
                var go = new GameObject(name);
                go.transform.SetParent(laneRoot, false);
                go.transform.localPosition = local;
                var light = go.AddComponent<Light2D>();
                DigVisualKit.ConfigurePointLight(light,
                    new Color(0.85f, 0.92f, 1f),
                    intensity,
                    outer,
                    inner: 0.4f,
                    shadows: false,
                    falloff: 0.45f);
            }

            Add("WorkLight", digMid, 1.8f, 5f);
            if (layers > ExcavatorBalanceLabTerrain.RockLayers + 4)
            {
                Vector2 digTop = world.CellCenter(midX, startY + layers * 3 / 4);
                Add("WorkLight_Upper", digTop, 1.4f, 4.5f);
            }
        }

        static Sprite MakeGoalSprite()
        {
            const int s = 16;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float nx = (x + 0.5f) / s * 2f - 1f;
                float ny = (y + 0.5f) / s * 2f - 1f;
                float d = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));
                bool on = d > 0.55f && d < 0.85f;
                tex.SetPixel(x, y, on ? Color.white : Color.clear);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        // ——— HUD ———
        static readonly Color UiBg = new(0.02f, 0.04f, 0.07f, 0.28f);
        static readonly Color UiCyan = new(0.25f, 0.92f, 1f, 1f);
        static readonly Color UiAmber = new(1f, 0.72f, 0.22f, 1f);
        static readonly Color UiWhite = new(0.92f, 0.96f, 1f, 1f);
        static readonly Color UiDim = new(0.5f, 0.62f, 0.7f, 1f);
        static readonly Color UiMute = new(0.32f, 0.42f, 0.48f, 1f);
        static readonly Color UiGreen = new(0.35f, 1f, 0.55f, 1f);

        void OnGUI()
        {
            if (_benchmark.IsRunning)
            {
                DrawBenchmarkProgress();
                return;
            }

            if (_benchmark.Status == ExcavatorBalanceBenchmark.State.Done &&
                _benchmark.Session != null &&
                !_showBenchmarkResults &&
                Event.current.type == EventType.Layout)
            {
                // Auto-open results once when a session finishes
                if (!_autoOpenedResults)
                {
                    _showBenchmarkResults = true;
                    _autoOpenedResults = true;
                }
            }

            if (_showBenchmarkResults && _benchmark.Session != null)
            {
                DrawBenchmarkResults();
                return;
            }

            if (_lanes == null) return;

            var hdr = LabelStyle(11, UiAmber, bold: true);
            var mute = LabelStyle(9, UiMute);
            var val = LabelStyle(10, UiWhite, bold: true);
            var dim = LabelStyle(9, UiDim);

            string modeLabel = ExcavatorBalanceHarness.ModeLabel(_mode);
            string kind = ExcavatorBalanceHarness.ModeKindLabel(_mode);

            var title = new Rect(10, 8, Screen.width - 20, 52);
            DrawPanel(title);
            GUI.Label(new Rect(18, 12, 520, 18),
                $"BALANCE COMPARE // {modeLabel}  ·  {kind}", hdr);
            GUI.Label(new Rect(18, 32, 720, 16),
                "1 ROCK · 2 BEDROCK · 3 ENDURANCE   ·   dig straight up · lanes never early-stop",
                dim);
            GUI.Label(new Rect(Screen.width - 380, 14, 360, 18),
                _paused ? "PAUSED (P)" : "R reset · P pause · C clear overheat · SPACE re-aim",
                dim);

            float mx = 18f;
            float my = 66f;
            if (ModeButton(new Rect(mx, my, 100, 24), "ROCK (1)", ExcavatorBalanceTestMode.Rock))
                SetMode(ExcavatorBalanceTestMode.Rock);
            if (ModeButton(new Rect(mx + 108, my, 120, 24), "BEDROCK (2)", ExcavatorBalanceTestMode.Bedrock))
                SetMode(ExcavatorBalanceTestMode.Bedrock);
            if (ModeButton(new Rect(mx + 236, my, 130, 24), "ENDURANCE (3)", ExcavatorBalanceTestMode.Endurance))
                SetMode(ExcavatorBalanceTestMode.Endurance);

            // Benchmark controls
            float bx0 = mx + 380f;
            if (GuiButton(new Rect(bx0, my, 150, 24), "QUICK BENCH 3×30s"))
            {
                _autoOpenedResults = false;
                _benchmark.StartQuick();
            }
            if (GuiButton(new Rect(bx0 + 158, my, 170, 24), "FULL BENCH 10×300s"))
            {
                _autoOpenedResults = false;
                _benchmark.StartFull();
            }
            if (_benchmark.Session != null)
            {
                if (GuiButton(new Rect(bx0 + 336, my, 120, 24), "RESULTS (B)"))
                    _showBenchmarkResults = true;
                if (GuiButton(new Rect(bx0 + 464, my, 100, 24), "EXPORT CSV"))
                    _benchmark.ExportAgain();
            }

            float colW = (Screen.width - 20f - 8f * (_lanes.Length - 1)) / _lanes.Length;
            float colH = _mode == ExcavatorBalanceTestMode.Endurance ? 340f : 320f;
            float top = 100f;

            for (int i = 0; i < _lanes.Length; i++)
            {
                var L = _lanes[i];
                float x = 10f + i * (colW + 8f);
                var panel = new Rect(x, top, colW, colH);
                DrawPanel(panel, L.Accent);

                float lx = x + 10f;
                float y = top + 8f;
                float inner = colW - 20f;

                GUI.Label(new Rect(lx, y, inner, 16f),
                    ExcavatorBalanceHarness.ProfileLabel(L.Profile),
                    LabelStyle(12, L.Accent, bold: true));
                y += 18f;

                if (L.Worker != null)
                {
                    GUI.Label(new Rect(lx, y, inner, 14f),
                        $"HEAT {L.Worker.Heat:0.#}/100  {L.Worker.HeatZone}  {L.Worker.MachineActivityLabel}",
                        mute);
                    y += 15f;
                    GUI.Label(new Rect(lx, y, inner, 14f),
                        $"STAM {L.Worker.CurrentStamina:0.#}/{L.Worker.MaxStamina:0.#}  " +
                        $"FRUST {L.Worker.Conditions.Frustration:0.#}  " +
                        $"INJ {L.Worker.Conditions.Injury:0.#}",
                        mute);
                    y += 16f;
                }

                var m = L.Harness?.Metrics;
                if (m == null) continue;

                void Row(string a, string b)
                {
                    GUI.Label(new Rect(lx, y, inner * 0.58f, 14f), a, mute);
                    GUI.Label(new Rect(lx + inner * 0.58f, y, inner * 0.42f, 14f), b, val);
                    y += 14.5f;
                }

                switch (_mode)
                {
                    case ExcavatorBalanceTestMode.Rock:
                        Row("ROCK DESTROYED", $"{m.RockDestroyed}");
                        Row("TOTAL MINING TIME", $"{m.RockMiningSeconds:0.0}s");
                        Row("AVG SEC / ROCK", $"{m.AvgSecondsPerRock:0.00}");
                        Row("ACTIVE TILES/MIN", $"{m.RockActiveTilesPerMinute:0.0}");
                        Row("OPERATIONAL TILES/MIN", $"{m.RockOperationalTilesPerMinute:0.0}");
                        Row("UPTIME %", $"{m.UptimePct:0.#}");
                        break;
                    case ExcavatorBalanceTestMode.Bedrock:
                        Row("BEDROCK DESTROYED", $"{m.BedrockDestroyed}");
                        Row("TOTAL MINING TIME", $"{m.BedrockMiningSeconds:0.0}s");
                        Row("AVG SEC / BEDROCK", $"{m.AvgSecondsPerBedrock:0.00}");
                        Row("ACTIVE TILES/MIN", $"{m.BedrockActiveTilesPerMinute:0.0}");
                        Row("OPERATIONAL TILES/MIN", $"{m.BedrockOperationalTilesPerMinute:0.0}");
                        Row("UPTIME %", $"{m.UptimePct:0.#}");
                        break;
                    default:
                        Row("ROCK DESTROYED", $"{m.RockDestroyed}");
                        Row("BEDROCK DESTROYED", $"{m.BedrockDestroyed}");
                        Row("AVG SEC / ROCK", $"{m.AvgSecondsPerRock:0.00}");
                        Row("AVG SEC / BEDROCK", $"{m.AvgSecondsPerBedrock:0.00}");
                        Row("ACTIVE ROCK T/MIN", $"{m.RockActiveTilesPerMinute:0.0}");
                        Row("ACTIVE BED T/MIN", $"{m.BedrockActiveTilesPerMinute:0.0}");
                        Row("OPS TOTAL T/MIN", $"{m.TotalOperationalTilesPerMinute:0.0}");
                        Row("OPS ROCK T/MIN", $"{m.RockOperationalTilesPerMinute:0.0}");
                        Row("OPS BED T/MIN", $"{m.BedrockOperationalTilesPerMinute:0.0}");
                        Row("UPTIME %", $"{m.UptimePct:0.#}");
                        break;
                }

                Row("WEAK POINT", $"{m.WeakPointSuccesses}/{m.WeakPointAttempts}  ({m.WeakPointSuccessPct:0}%)");
                Row("AVG / MAX HEAT", $"{m.AvgHeat:0.#} / {m.MaxHeat:0.#}");
                Row("ZONE N/O/D/E %",
                    $"{m.ZonePct(m.ZoneNormalSec):0}/{m.ZonePct(m.ZoneOptimalSec):0}/" +
                    $"{m.ZonePct(m.ZoneDangerSec):0}/{m.ZonePct(m.ZoneExtremeSec):0}");
                Row("OVERHEAT / INJURY", $"{m.OverheatEvents} / {m.InjuryEvents}");
                Row("FRUST start→end", $"{m.FrustrationStart:0.#}→{m.FrustrationEnd:0.#}");
                Row("STAM start→end", $"{m.StaminaStart:0.#}→{m.StaminaEnd:0.#}");
            }

            float by = top + colH + 10f;
            if (GuiButton(new Rect(10, by, 120, 26), "RESET ALL (R)"))
                ResetAll();
            if (GuiButton(new Rect(140, by, 140, 26), "RESET METRICS"))
                ResetMetricsOnly();
            if (GuiButton(new Rect(290, by, 150, 26), "CLEAR OVERHEAT (C)"))
                ClearAllOverheats();
            if (GuiButton(new Rect(450, by, 120, 26), _paused ? "RESUME (P)" : "PAUSE (P)"))
                _paused = !_paused;
            if (GuiButton(new Rect(580, by, 160, 26), "GREEN BEDROCK DIAG"))
                ExcavatorGreenBedrockDiagnostic.RunAndLog();
        }

        void DrawBenchmarkProgress()
        {
            var panel = new Rect(Screen.width * 0.2f, Screen.height * 0.35f, Screen.width * 0.6f, 120f);
            DrawPanel(panel, UiAmber);
            GUI.Label(new Rect(panel.x + 16, panel.y + 14, panel.width - 32, 22),
                "AUTOMATED BENCHMARK RUNNING", LabelStyle(14, UiAmber, bold: true));
            GUI.Label(new Rect(panel.x + 16, panel.y + 42, panel.width - 32, 18),
                _benchmark.ProgressLabel, LabelStyle(11, UiWhite));
            float barW = panel.width - 32f;
            var barBg = new Rect(panel.x + 16, panel.y + 70, barW, 14);
            GUI.color = new Color(0.1f, 0.15f, 0.2f, 0.8f);
            GUI.DrawTexture(barBg, Texture2D.whiteTexture);
            GUI.color = UiCyan;
            GUI.DrawTexture(new Rect(barBg.x, barBg.y, barW * Mathf.Clamp01(_benchmark.Progress01), barBg.height),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x + 16, panel.y + 92, panel.width - 32, 16),
                $"{_benchmark.Progress01 * 100f:0.#}%  ·  Esc cancel  ·  headless fast-forward (same systems)",
                LabelStyle(9, UiDim));
        }

        void DrawBenchmarkResults()
        {
            var session = _benchmark.Session;
            var panel = new Rect(12, 12, Screen.width - 24, Screen.height - 24);
            DrawPanel(panel, UiCyan);

            GUI.Label(new Rect(24, 22, 600, 20),
                "BENCHMARK RESULTS", LabelStyle(14, UiAmber, bold: true));
            GUI.Label(new Rect(24, 44, 800, 16),
                $"runs={session.RunsPerExcavator}  duration={session.RunDurationSeconds:0.#}s  seedBase={session.BaseSeed}  step={session.SimStepSeconds:0.###}s",
                LabelStyle(9, UiDim));

            float mx = 24f;
            float my = 68f;
            if (ModeButton(new Rect(mx, my, 90, 22), "ROCK", ExcavatorBalanceTestMode.Rock, resultsSelector: true))
                _resultsMode = ExcavatorBalanceTestMode.Rock;
            if (ModeButton(new Rect(mx + 98, my, 100, 22), "BEDROCK", ExcavatorBalanceTestMode.Bedrock, resultsSelector: true))
                _resultsMode = ExcavatorBalanceTestMode.Bedrock;
            if (ModeButton(new Rect(mx + 206, my, 110, 22), "ENDURANCE", ExcavatorBalanceTestMode.Endurance, resultsSelector: true))
                _resultsMode = ExcavatorBalanceTestMode.Endurance;

            if (GuiButton(new Rect(panel.xMax - 240, 22, 100, 24), "EXPORT"))
                _benchmark.ExportAgain();
            if (GuiButton(new Rect(panel.xMax - 130, 22, 100, 24), "CLOSE"))
                _showBenchmarkResults = false;

            float tableTop = 100f;
            var scrollRect = new Rect(24, tableTop, panel.width - 48, panel.height - tableTop - 80);
            float contentH = 720f;
            _resultsScroll = GUI.BeginScrollView(scrollRect, _resultsScroll,
                new Rect(0, 0, scrollRect.width - 20, contentH));

            float nameW = 170f;
            float colW = (scrollRect.width - 40 - nameW) / Profiles.Length;
            var mute = LabelStyle(9, UiMute);
            var val = LabelStyle(9, UiWhite, bold: true);
            var hdr = LabelStyle(9, UiCyan, bold: true);

            GUI.Label(new Rect(0, 0, nameW, 16), ExcavatorBalanceHarness.ModeLabel(_resultsMode), hdr);
            for (int i = 0; i < Profiles.Length; i++)
                GUI.Label(new Rect(nameW + i * colW, 0, colW, 16),
                    ExcavatorBalanceHarness.ProfileLabel(Profiles[i]), hdr);

            float y = 22f;

            void MetricBlock(string title, System.Func<ExcavatorBenchmarkProfileAggregate, MetricAggregate> pick,
                System.Func<ExcavatorBenchmarkProfileAggregate, string> extra = null)
            {
                GUI.Label(new Rect(0, y, scrollRect.width - 40, 16), title, LabelStyle(10, UiAmber, bold: true));
                y += 18f;
                void StatRow(string label, System.Func<MetricAggregate, float> f)
                {
                    GUI.Label(new Rect(0, y, nameW, 14), label, mute);
                    for (int i = 0; i < Profiles.Length; i++)
                    {
                        var agg = session.FindAggregate(_resultsMode, Profiles[i]);
                        string text = agg == null ? "—" : $"{f(pick(agg)):0.##}";
                        GUI.Label(new Rect(nameW + i * colW, y, colW, 14), text, val);
                    }
                    y += 14f;
                }

                StatRow("MEAN", m => m.Mean);
                StatRow("MEDIAN", m => m.Median);
                StatRow("MIN", m => m.Min);
                StatRow("MAX", m => m.Max);
                StatRow("STD DEV", m => m.StdDev);
                if (extra != null)
                {
                    GUI.Label(new Rect(0, y, nameW, 14), "TOTAL TILES", mute);
                    for (int i = 0; i < Profiles.Length; i++)
                    {
                        var agg = session.FindAggregate(_resultsMode, Profiles[i]);
                        GUI.Label(new Rect(nameW + i * colW, y, colW, 14),
                            agg == null ? "—" : extra(agg), val);
                    }
                    y += 14f;
                }
                y += 8f;
            }

            if (_resultsMode == ExcavatorBalanceTestMode.Rock)
            {
                MetricBlock("ACTIVE TILES/MIN (ROCK)", a => a.RockActiveTilesPerMin,
                    a => $"{a.TotalRockDestroyed}");
                MetricBlock("OPERATIONAL TILES/MIN (ROCK)", a => a.RockOperationalTilesPerMin);
                MetricBlock("SEC / ROCK", a => a.SecPerRock);
            }
            else if (_resultsMode == ExcavatorBalanceTestMode.Bedrock)
            {
                MetricBlock("ACTIVE TILES/MIN (BEDROCK)", a => a.BedrockActiveTilesPerMin,
                    a => $"{a.TotalBedrockDestroyed}");
                MetricBlock("OPERATIONAL TILES/MIN (BEDROCK)", a => a.BedrockOperationalTilesPerMin);
                MetricBlock("SEC / BEDROCK", a => a.SecPerBedrock);
            }
            else
            {
                // ENDURANCE: operational total is the primary throughput readout
                MetricBlock("OPERATIONAL TILES/MIN (TOTAL) ★", a => a.TotalOperationalTilesPerMin,
                    a => $"{a.TotalRockDestroyed + a.TotalBedrockDestroyed}");
                MetricBlock("OPERATIONAL ROCK T/MIN", a => a.RockOperationalTilesPerMin,
                    a => $"{a.TotalRockDestroyed}");
                MetricBlock("OPERATIONAL BEDROCK T/MIN", a => a.BedrockOperationalTilesPerMin,
                    a => $"{a.TotalBedrockDestroyed}");
                MetricBlock("ACTIVE ROCK T/MIN", a => a.RockActiveTilesPerMin);
                MetricBlock("ACTIVE BEDROCK T/MIN", a => a.BedrockActiveTilesPerMin);
            }

            MetricBlock("UPTIME %", a => a.UptimePct);
            MetricBlock("WEAK POINT %", a => a.WeakPointPct);
            MetricBlock("AVG HEAT", a => a.AvgHeat);
            MetricBlock("OVERHEATS / MIN", a => a.OverheatsPerMin);
            MetricBlock("AVG STAMINA", a => a.AvgStamina);
            MetricBlock("INJURIES / MIN", a => a.InjuriesPerMin);
            MetricBlock("OVERHEAT LOCK SEC", a => a.OverheatedLock);

            GUI.EndScrollView();

            if (!string.IsNullOrEmpty(_benchmark.LastRawCsvPath))
            {
                GUI.Label(new Rect(24, panel.yMax - 56, panel.width - 48, 16),
                    $"CSV: {_benchmark.LastRawCsvPath}", LabelStyle(8, UiDim));
                GUI.Label(new Rect(24, panel.yMax - 40, panel.width - 48, 16),
                    $"AGG: {_benchmark.LastAggCsvPath}", LabelStyle(8, UiDim));
            }
        }

        bool ModeButton(Rect r, string label, ExcavatorBalanceTestMode mode, bool resultsSelector = false)
        {
            bool active = resultsSelector ? _resultsMode == mode : _mode == mode;
            var prev = GUI.color;
            GUI.color = active
                ? new Color(0.08f, 0.18f, 0.22f, 0.7f)
                : new Color(0.05f, 0.1f, 0.14f, 0.4f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            Color edge = active ? UiGreen : UiCyan;
            GUI.color = new Color(edge.r, edge.g, edge.b, active ? 0.85f : 0.4f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1f, r.width, 1f), Texture2D.whiteTexture);
            GUI.color = prev;
            var st = LabelStyle(10, active ? UiGreen : UiCyan, bold: true);
            st.alignment = TextAnchor.MiddleCenter;
            GUI.Label(r, label, st);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        static void DrawPanel(Rect r, Color accent = default)
        {
            if (accent.a < 0.01f) accent = UiCyan;
            var prev = GUI.color;
            GUI.color = UiBg;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.55f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1f, r.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, 1f, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 1f, r.y, 1f, r.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        static bool GuiButton(Rect r, string label)
        {
            var prev = GUI.color;
            GUI.color = new Color(0.05f, 0.1f, 0.14f, 0.45f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(UiCyan.r, UiCyan.g, UiCyan.b, 0.5f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), Texture2D.whiteTexture);
            GUI.color = prev;
            var st = LabelStyle(10, UiCyan, bold: true);
            st.alignment = TextAnchor.MiddleCenter;
            GUI.Label(r, label, st);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        static GUIStyle LabelStyle(int size, Color color, bool bold = false) =>
            new(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                normal = { textColor = color },
                clipping = TextClipping.Overflow,
                padding = new RectOffset(0, 0, 0, 2),
            };
    }
}
