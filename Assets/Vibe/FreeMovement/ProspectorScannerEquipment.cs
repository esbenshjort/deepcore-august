using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Heavy Prospector field scanner.
    /// Stage 1: place → travel → game-time setup → Ready.
    /// Stage 2: plan cone → Scanning (game-time) → Ready; raw evidence via ScanHistory.
    /// </summary>
    public sealed class ProspectorScannerEquipment : MonoBehaviour
    {
        FineTerrainWorld _world;
        Transform _facingRoot;
        LineRenderer _coneCore;
        LineRenderer _coneGlow;
        LineRenderer _radialL;
        LineRenderer _radialR;
        LineRenderer _ringMid;
        LineRenderer _sweepRing;
        MeshFilter _fillMf;
        MeshRenderer _fillMr;
        Mesh _fillMesh;
        SpriteRenderer _body;
        SpriteRenderer _status;
        Transform _progressRoot;
        SpriteRenderer _progressBg;
        SpriteRenderer _progressFill;
        const float ProgressBarWidth = 0.28f;
        static readonly Color CyanCore = new(0.25f, 0.92f, 1f, 0.85f);
        static readonly Color CyanGlow = new(0.2f, 0.75f, 1f, 0.28f);
        static readonly Color CyanFill = new(0.08f, 0.22f, 0.32f, 0.42f);
        static readonly Color CyanRing = new(0.35f, 0.9f, 1f, 0.35f);
        static readonly Color ScanFill = new(0.1f, 0.28f, 0.38f, 0.38f);
        static readonly Color InvalidCore = new(1f, 0.35f, 0.35f, 0.8f);
        static readonly Color InvalidGlow = new(1f, 0.25f, 0.25f, 0.3f);
        static readonly Color InvalidFill = new(0.28f, 0.06f, 0.08f, 0.45f);

        public ProspectorScannerState State { get; private set; } = ProspectorScannerState.Packed;
        public ProspectorScannerEquipmentSpec Spec { get; private set; } = ProspectorScannerEquipmentSpec.Default;
        public Vector2 Position => transform.localPosition;
        public Vector2 Facing => _facingRoot != null ? (Vector2)_facingRoot.up : Vector2.up;
        public float SetupDurationHours { get; private set; }
        public float SetupElapsedHours { get; private set; }
        public float SetupRemainingHours =>
            State == ProspectorScannerState.SettingUp
                ? Mathf.Max(0f, SetupDurationHours - SetupElapsedHours)
                : 0f;
        public int SetupMechanics { get; private set; }
        public int SetupHeavyLifting { get; private set; }
        public string SetupByName { get; private set; } = "Prospector";

        /// <summary>Planned survey cone — always ≤ equipment Spec.</summary>
        public float PlannedRangeCells { get; private set; }
        public float PlannedHalfAngleDeg { get; private set; }
        public ProspectorScanSession ActiveSession { get; private set; }
        public bool IsScanning => State == ProspectorScannerState.Scanning && ActiveSession != null;

        public static ProspectorScannerEquipment Spawn(
            Transform parent,
            FineTerrainWorld world,
            Vector2 localPos,
            Vector2 facing,
            ProspectorScannerEquipmentSpec? spec = null)
        {
            var go = new GameObject("ProspectorScanner");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var eq = go.AddComponent<ProspectorScannerEquipment>();
            eq.Build(world, facing, spec ?? ProspectorScannerEquipmentSpec.Default);
            return eq;
        }

        void Build(FineTerrainWorld world, Vector2 facing, ProspectorScannerEquipmentSpec spec)
        {
            _world = world;
            Spec = spec;
            State = ProspectorScannerState.Packed;
            PlannedRangeCells = spec.MaxRangeCells;
            PlannedHalfAngleDeg = spec.ConeHalfAngleDeg;

            _facingRoot = new GameObject("Facing").transform;
            _facingRoot.SetParent(transform, false);
            SetFacing(facing);

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(_facingRoot, false);
            _body = bodyGo.AddComponent<SpriteRenderer>();
            _body.sprite = MakeBodySprite();
            _body.sortingOrder = 40;
            DigVisualKit.ApplyLit(_body);
            // Compact field kit — slightly larger to show detail on 64px sprite
            bodyGo.transform.localScale = Vector3.one * 0.34f;
            _body.color = Color.white;

            var statusGo = new GameObject("StatusPip");
            statusGo.transform.SetParent(_facingRoot, false);
            // LED sits on the rear housing / pack of the sprite
            statusGo.transform.localPosition = new Vector3(0.04f, -0.1f, 0f);
            _status = statusGo.AddComponent<SpriteRenderer>();
            _status.sprite = MakePipSprite();
            _status.sortingOrder = 41;
            DigVisualKit.ApplyLit(_status);
            statusGo.transform.localScale = Vector3.one * 0.085f;

            BuildSurveyPreview();
            BuildProgressBar();
            RebuildConePreview();
            SetPreviewVisible(false);
            RefreshStatusVisual();
        }

        void BuildSurveyPreview()
        {
            // Soft sector fill — transparent glass panel language
            var fillGo = new GameObject("SurveyFill");
            fillGo.transform.SetParent(_facingRoot, false);
            _fillMf = fillGo.AddComponent<MeshFilter>();
            _fillMr = fillGo.AddComponent<MeshRenderer>();
            _fillMesh = new Mesh { name = "ScannerSurveyFill" };
            _fillMf.sharedMesh = _fillMesh;
            var sh = Shader.Find("Sprites/Default");
            if (sh != null)
            {
                _fillMr.sharedMaterial = new Material(sh);
                _fillMr.sharedMaterial.color = CyanFill;
            }
            _fillMr.sortingOrder = 26;

            _coneGlow = MakeConeLine("ArcGlow", 0.09f, CyanGlow, 27);
            _coneCore = MakeConeLine("ArcCore", 0.028f, CyanCore, 30);
            _radialL = MakeConeLine("RadialL", 0.022f, CyanCore, 29);
            _radialR = MakeConeLine("RadialR", 0.022f, CyanCore, 29);
            _ringMid = MakeConeLine("RangeRing", 0.018f, CyanRing, 28);
            _sweepRing = MakeConeLine("SweepReach", 0.03f, CyanCore, 31);
            if (_sweepRing != null) _sweepRing.enabled = false;
        }

        void BuildProgressBar()
        {
            _progressRoot = new GameObject("SetupProgress").transform;
            _progressRoot.SetParent(transform, false);
            _progressRoot.localPosition = new Vector3(0f, -0.16f, 0f);

            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(_progressRoot, false);
            _progressBg = bgGo.AddComponent<SpriteRenderer>();
            _progressBg.sprite = MakeBarSprite();
            _progressBg.sortingOrder = 43;
            DigVisualKit.ApplyLit(_progressBg);
            _progressBg.color = new Color(0.04f, 0.07f, 0.1f, 0.9f);
            bgGo.transform.localScale = new Vector3(ProgressBarWidth, 0.032f, 1f);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(_progressRoot, false);
            _progressFill = fillGo.AddComponent<SpriteRenderer>();
            _progressFill.sprite = MakeBarSprite(pivotLeft: true);
            _progressFill.sortingOrder = 44;
            DigVisualKit.ApplyLit(_progressFill);
            _progressFill.color = new Color(0.3f, 0.92f, 1f, 0.95f);
            fillGo.transform.localPosition = new Vector3(-ProgressBarWidth * 0.5f, 0f, 0f);
            fillGo.transform.localScale = new Vector3(0.001f, 0.022f, 1f);

            SetProgressVisible(false);
        }

        void SetProgressVisible(bool on)
        {
            if (_progressRoot != null)
                _progressRoot.gameObject.SetActive(on);
        }

        void UpdateProgressBar()
        {
            if (_progressFill == null || _progressRoot == null) return;

            if (State == ProspectorScannerState.SettingUp)
            {
                SetProgressVisible(true);
                float t = SetupDurationHours > 0.001f
                    ? Mathf.Clamp01(SetupElapsedHours / SetupDurationHours)
                    : 0f;
                _progressFill.color = new Color(0.3f, 0.92f, 1f, 0.95f);
                _progressFill.transform.localScale = new Vector3(
                    Mathf.Max(0.001f, ProgressBarWidth * t), 0.022f, 1f);
            }
            else if (State == ProspectorScannerState.Scanning && ActiveSession != null)
            {
                SetProgressVisible(true);
                float t = ActiveSession.Progress01;
                _progressFill.color = new Color(0.95f, 0.78f, 0.25f, 0.95f);
                _progressFill.transform.localScale = new Vector3(
                    Mathf.Max(0.001f, ProgressBarWidth * t), 0.022f, 1f);
            }
            else if (State == ProspectorScannerState.Ready)
            {
                SetProgressVisible(true);
                _progressFill.color = new Color(0.35f, 1f, 0.55f, 0.95f);
                _progressFill.transform.localScale = new Vector3(ProgressBarWidth, 0.022f, 1f);
            }
            else
            {
                SetProgressVisible(false);
            }
        }

        LineRenderer MakeConeLine(string name, float width, Color col, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_facingRoot, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = false;
            lr.widthMultiplier = width;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.sortingOrder = order;
            var sh = Shader.Find("Sprites/Default");
            if (sh != null) lr.material = new Material(sh);
            lr.startColor = lr.endColor = col;
            return lr;
        }

        public void SetFacing(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            dir.Normalize();
            if (_facingRoot == null) return;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            _facingRoot.localRotation = Quaternion.Euler(0f, 0f, ang);
            RebuildConePreview();
        }

        public void SetPreviewVisible(bool on)
        {
            if (_coneCore != null) _coneCore.enabled = on;
            if (_coneGlow != null) _coneGlow.enabled = on;
            if (_radialL != null) _radialL.enabled = on;
            if (_radialR != null) _radialR.enabled = on;
            if (_ringMid != null) _ringMid.enabled = on;
            if (_fillMr != null) _fillMr.enabled = on;
            if (_sweepRing != null && !on) _sweepRing.enabled = false;
        }

        /// <summary>
        /// Clamp planned survey to equipment Spec. Worker stats never exceed these limits.
        /// </summary>
        public void SetPlannedSurvey(float rangeCells, float halfAngleDeg)
        {
            PlannedRangeCells = Mathf.Clamp(rangeCells, 1f, Spec.MaxRangeCells);
            PlannedHalfAngleDeg = Mathf.Clamp(halfAngleDeg, 1f, Spec.ConeHalfAngleDeg);
            RebuildConePreview();
        }

        public void ApplyDistancePreset(ScanDistance distance)
        {
            float frac = distance switch
            {
                ScanDistance.Short => 1f / 3f,
                ScanDistance.Medium => 2f / 3f,
                _ => 1f,
            };
            SetPlannedSurvey(Spec.MaxRangeCells * frac, PlannedHalfAngleDeg);
        }

        public void ApplyWidthPreset(ScanWidth width)
        {
            float frac = width == ScanWidth.Narrow ? 0.55f : 1f;
            SetPlannedSurvey(PlannedRangeCells, Spec.ConeHalfAngleDeg * frac);
        }

        public void ShowPlanningPreview(bool valid)
        {
            SetPreviewVisible(true);
            ApplyPreviewColors(valid);
            if (_body != null)
                _body.color = valid
                    ? new Color(0.55f, 0.72f, 0.85f, 1f)
                    : new Color(0.85f, 0.35f, 0.35f, 0.85f);
        }

        void ApplyPreviewColors(bool valid)
        {
            Color core = valid ? CyanCore : InvalidCore;
            Color glow = valid ? CyanGlow : InvalidGlow;
            Color fill = valid ? CyanFill : InvalidFill;
            Color ring = valid ? CyanRing : new Color(1f, 0.4f, 0.4f, 0.4f);
            SetLineColor(_coneCore, core);
            SetLineColor(_coneGlow, glow);
            SetLineColor(_radialL, core);
            SetLineColor(_radialR, core);
            SetLineColor(_ringMid, ring);
            if (_fillMr != null && _fillMr.sharedMaterial != null)
                _fillMr.sharedMaterial.color = fill;
        }

        static void SetLineColor(LineRenderer lr, Color c)
        {
            if (lr == null) return;
            lr.startColor = lr.endColor = c;
        }

        public void BeginSetup(WorkerStats stats, string workerName, float durationHours)
        {
            if (State != ProspectorScannerState.Packed) return;
            SetupByName = string.IsNullOrEmpty(workerName) ? "Prospector" : workerName;
            SetupMechanics = stats != null ? stats.Get(WorkerStatId.Mechanics) : WorkerStats.Baseline;
            SetupHeavyLifting = stats != null ? stats.Get(WorkerStatId.HeavyLifting) : WorkerStats.Baseline;
            SetupDurationHours = Mathf.Max(ProspectorScannerSetup.MinHours, durationHours);
            SetupElapsedHours = 0f;
            State = ProspectorScannerState.SettingUp;
            SetPreviewVisible(false);
            RefreshStatusVisual();
            DigHoodLog.Push(
                $"SCANNER | SETTING UP | by {SetupByName} | " +
                $"Mechanics {SetupMechanics} | HeavyLifting {SetupHeavyLifting} | " +
                $"duration {SetupDurationHours:0.##}h | pos {Position.x:0.00},{Position.y:0.00}");
            Debug.Log(
                $"[SCANNER] SETTING UP — {SetupByName} | {SetupDurationHours:0.##} game hours | " +
                $"Mechanics={SetupMechanics} HeavyLifting={SetupHeavyLifting}");
        }

        /// <summary>Advance setup using game-hour delta (not raw Time.deltaTime).</summary>
        public bool TickSetupGameHours(float gameHoursDelta)
        {
            if (State != ProspectorScannerState.SettingUp || gameHoursDelta <= 0f)
                return false;

            SetupElapsedHours += gameHoursDelta;
            RefreshStatusVisual();
            if (SetupElapsedHours + 0.0001f < SetupDurationHours)
                return false;

            CompleteSetup();
            return true;
        }

        /// <summary>Debug: finish setup immediately without changing the global clock.</summary>
        public void DebugForceCompleteSetup()
        {
            if (State != ProspectorScannerState.SettingUp) return;
            SetupElapsedHours = SetupDurationHours;
            CompleteSetup();
        }

        void CompleteSetup()
        {
            State = ProspectorScannerState.Ready;
            SetupElapsedHours = SetupDurationHours;
            PlannedRangeCells = Spec.MaxRangeCells;
            PlannedHalfAngleDeg = Spec.ConeHalfAngleDeg;
            RebuildConePreview();
            SetPreviewVisible(false);
            RefreshStatusVisual();
            DigHoodLog.Push(
                $"SCANNER READY | {SetupByName} | duration {SetupDurationHours:0.##}h | " +
                $"Mechanics {SetupMechanics} | HeavyLifting {SetupHeavyLifting} | " +
                $"pos {Position.x:0.00},{Position.y:0.00} | facing {Facing.x:0.00},{Facing.y:0.00}");
            Debug.Log(
                $"[SCANNER READY] Prospector={SetupByName} duration={SetupDurationHours:0.##}h " +
                $"Mechanics={SetupMechanics} HeavyLifting={SetupHeavyLifting} " +
                $"pos=({Position.x:0.00},{Position.y:0.00}) facing=({Facing.x:0.00},{Facing.y:0.00})");
        }

        /// <summary>Start Stage-2 scan. Scanner stays fixed. Returns false if rejected.</summary>
        public bool TryBeginScan(
            ProspectorScanHistory history,
            WorkerStats stats,
            string prospectorName,
            float absoluteGameHours,
            string prospectorId = null,
            WorkerSheetProfile prospectorProfile = WorkerSheetProfile.Baseline,
            string prospectorProfileLabel = null)
        {
            if (State != ProspectorScannerState.Ready || _world == null) return false;
            ActiveSession ??= new ProspectorScanSession();
            if (!ActiveSession.TryBegin(
                    _world, history, this, stats, prospectorName,
                    absoluteGameHours, PlannedRangeCells, PlannedHalfAngleDeg,
                    prospectorId, prospectorProfile, prospectorProfileLabel))
                return false;

            State = ProspectorScannerState.Scanning;
            // Cone outline is planning-only (C). During scan keep the kit quiet — sweep ring still runs.
            SetPreviewVisible(false);
            if (_sweepRing != null) _sweepRing.enabled = true;
            RefreshStatusVisual();
            UpdateSweepReach(0f);
            return true;
        }

        /// <summary>Advance scan on game hours. Returns true when scan finishes → Ready.</summary>
        public bool TickScanGameHours(float gameHoursDelta, float absoluteGameHours)
        {
            if (State != ProspectorScannerState.Scanning || ActiveSession == null)
                return false;

            bool done = ActiveSession.TickGameHours(gameHoursDelta, absoluteGameHours);
            UpdateSweepReach(ActiveSession.IsActive ? ActiveSession.CurrentReachCells : PlannedRangeCells);
            RefreshStatusVisual();
            if (!done) return false;

            FinishScanToReady();
            return true;
        }

        public void DebugForceCompleteScan(float absoluteGameHours)
        {
            if (State != ProspectorScannerState.Scanning || ActiveSession == null) return;
            ActiveSession.DebugForceComplete(absoluteGameHours);
            FinishScanToReady();
        }

        void FinishScanToReady()
        {
            State = ProspectorScannerState.Ready;
            if (_sweepRing != null) _sweepRing.enabled = false;
            SetPreviewVisible(false);
            RefreshStatusVisual();
            DigHoodLog.Push("SCANNER READY | scan complete — evidence retained");
        }

        void UpdateSweepReach(float reachCells)
        {
            if (_sweepRing == null || _world == null) return;
            float half = PlannedHalfAngleDeg;
            float r = Mathf.Max(0.01f, reachCells) * _world.CellSize;
            const int steps = 24;
            _sweepRing.enabled = State == ProspectorScannerState.Scanning;
            _sweepRing.positionCount = steps + 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float ang = Mathf.Lerp(-half, half, t) * Mathf.Deg2Rad;
                _sweepRing.SetPosition(i, new Vector3(Mathf.Sin(ang) * r, Mathf.Cos(ang) * r, 0f));
            }
            SetLineColor(_sweepRing, new Color(1f, 0.85f, 0.35f, 0.75f));
        }

        /// <summary>Future redeploy entry: return to Packed without destroying the object.</summary>
        public void BeginPackUp()
        {
            if (ActiveSession != null && ActiveSession.IsActive)
                ActiveSession.Abort();
            ActiveSession = null;
            State = ProspectorScannerState.Packed;
            SetupElapsedHours = 0f;
            SetupDurationHours = 0f;
            if (_sweepRing != null) _sweepRing.enabled = false;
            SetPreviewVisible(false);
            RefreshStatusVisual();
            DigHoodLog.Push("SCANNER | PACKED (redeploy)");
        }

        void RebuildConePreview()
        {
            if (_world == null || _coneCore == null || _facingRoot == null) return;
            float range = PlannedRangeCells * _world.CellSize;
            float half = PlannedHalfAngleDeg;
            const int steps = 28;

            // Outer arc
            _coneCore.positionCount = steps + 1;
            _coneGlow.positionCount = steps + 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float ang = Mathf.Lerp(-half, half, t) * Mathf.Deg2Rad;
                Vector3 p = new(Mathf.Sin(ang) * range, Mathf.Cos(ang) * range, 0f);
                _coneCore.SetPosition(i, p);
                _coneGlow.SetPosition(i, p);
            }

            // Radial edges (apex → arc ends)
            Vector3 left = new(Mathf.Sin(-half * Mathf.Deg2Rad) * range, Mathf.Cos(-half * Mathf.Deg2Rad) * range, 0f);
            Vector3 right = new(Mathf.Sin(half * Mathf.Deg2Rad) * range, Mathf.Cos(half * Mathf.Deg2Rad) * range, 0f);
            if (_radialL != null)
            {
                _radialL.positionCount = 2;
                _radialL.SetPosition(0, Vector3.zero);
                _radialL.SetPosition(1, left);
            }
            if (_radialR != null)
            {
                _radialR.positionCount = 2;
                _radialR.SetPosition(0, Vector3.zero);
                _radialR.SetPosition(1, right);
            }

            // Mid-range tick ring (technical depth cue)
            float midR = range * 0.55f;
            if (_ringMid != null)
            {
                _ringMid.positionCount = steps + 1;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float ang = Mathf.Lerp(-half, half, t) * Mathf.Deg2Rad;
                    _ringMid.SetPosition(i, new Vector3(Mathf.Sin(ang) * midR, Mathf.Cos(ang) * midR, 0f));
                }
            }

            RebuildFillMesh(range, half, steps);
            ApplyPreviewColors(valid: true);
        }

        void RebuildFillMesh(float range, float halfDeg, int steps)
        {
            if (_fillMesh == null) return;
            int vertCount = steps + 2; // apex + arc
            var verts = new Vector3[vertCount];
            var cols = new Color[vertCount];
            var tris = new int[steps * 3];
            verts[0] = Vector3.zero;
            cols[0] = new Color(CyanFill.r, CyanFill.g, CyanFill.b, CyanFill.a * 0.55f);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float ang = Mathf.Lerp(-halfDeg, halfDeg, t) * Mathf.Deg2Rad;
                verts[i + 1] = new Vector3(Mathf.Sin(ang) * range, Mathf.Cos(ang) * range, 0f);
                // Fade toward rim — glass panel, not solid blot
                float rim = 0.35f + 0.65f * (1f - t * (1f - t));
                cols[i + 1] = new Color(CyanFill.r, CyanFill.g, CyanFill.b, CyanFill.a * rim);
            }
            for (int i = 0; i < steps; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }
            _fillMesh.Clear();
            _fillMesh.vertices = verts;
            _fillMesh.colors = cols;
            _fillMesh.triangles = tris;
            _fillMesh.RecalculateBounds();
        }

        void RefreshStatusVisual()
        {
            if (_status == null) return;
            _status.color = State switch
            {
                ProspectorScannerState.Packed => new Color(0.45f, 0.5f, 0.55f, 1f),
                ProspectorScannerState.SettingUp => new Color(1f, 0.72f, 0.22f, 1f),
                ProspectorScannerState.Ready => new Color(0.35f, 1f, 0.55f, 1f),
                ProspectorScannerState.Scanning => new Color(0.35f, 0.95f, 1f, 1f),
                _ => Color.white,
            };
            // Body keeps its authored palette — only slight live tint when active
            if (_body != null)
            {
                _body.color = State == ProspectorScannerState.Packed
                    ? new Color(0.88f, 0.9f, 0.92f, 1f)
                    : Color.white;
            }
            UpdateProgressBar();
        }

        public string StateLabel => State switch
        {
            ProspectorScannerState.Packed => "PACKED",
            ProspectorScannerState.SettingUp => "SETTING UP",
            ProspectorScannerState.Ready => "READY",
            ProspectorScannerState.Scanning => "SCANNING",
            _ => "?",
        };

        static Sprite MakeBarSprite(bool pivotLeft = false)
        {
            const int s = 8;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, Color.white);
            tex.Apply();
            var pivot = pivotLeft ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
            return Sprite.Create(tex, new Rect(0, 0, s, s), pivot, s);
        }

        /// <summary>
        /// Top-down industrial survey kit: orange/gunmetal chassis, forward dish, cyan aperture.
        /// Sprite "up" = facing / emit direction. Matches Deep Core crew art direction.
        /// </summary>
        static Sprite MakeBodySprite()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, Color.clear);

            Color orange = new(0.92f, 0.48f, 0.12f);
            Color orangeHi = new(1f, 0.62f, 0.22f);
            Color orangeDeep = new(0.62f, 0.28f, 0.08f);
            Color rust = new(0.45f, 0.22f, 0.1f);
            Color hull = new(0.14f, 0.16f, 0.18f);
            Color hullHi = new(0.28f, 0.32f, 0.36f);
            Color charcoal = new(0.1f, 0.11f, 0.12f);
            Color metal = new(0.48f, 0.5f, 0.54f);
            Color metalHi = new(0.72f, 0.74f, 0.78f);
            Color metalDeep = new(0.28f, 0.3f, 0.33f);
            Color outline = new(0.02f, 0.02f, 0.03f);
            Color cyan = new(0.3f, 0.9f, 1f);
            Color cyanCore = new(0.75f, 0.98f, 1f);
            Color cyanDeep = new(0.1f, 0.35f, 0.42f);
            Color green = new(0.3f, 0.95f, 0.4f);
            Color greenCore = new(0.75f, 1f, 0.8f);
            Color dark = new(0.05f, 0.06f, 0.07f);
            Color hazardY = new(0.95f, 0.82f, 0.12f);

            void P(int x, int y, Color c)
            {
                if ((uint)x < s && (uint)y < s) tex.SetPixel(x, y, c);
            }

            void Fill(int x0, int y0, int w, int h, Color c)
            {
                for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    P(x, y, c);
            }

            void Disc(float cx, float cy, float rx, float ry, Color fill, Color? edge = null)
            {
                int x0 = Mathf.FloorToInt(cx - rx - 1);
                int x1 = Mathf.CeilToInt(cx + rx + 1);
                int y0 = Mathf.FloorToInt(cy - ry - 1);
                int y1 = Mathf.CeilToInt(cy + ry + 1);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x - cx) / Mathf.Max(0.01f, rx);
                    float dy = (y - cy) / Mathf.Max(0.01f, ry);
                    float d = dx * dx + dy * dy;
                    if (d > 1.02f) continue;
                    if (edge.HasValue && d > 0.78f) P(x, y, edge.Value);
                    else P(x, y, fill);
                }
            }

            // Main chassis (chamfered industrial box)
            Fill(16, 12, 32, 28, outline);
            Fill(17, 13, 30, 26, charcoal);
            Fill(19, 15, 26, 22, hull);
            Fill(22, 18, 20, 16, hullHi);
            // Orange shoulder rails
            Fill(16, 12, 32, 4, orangeDeep);
            Fill(17, 13, 30, 2, orange);
            Fill(16, 36, 32, 4, orangeDeep);
            Fill(17, 37, 30, 2, orange);
            // Hazard corner ticks
            Fill(17, 14, 6, 2, hazardY);
            Fill(41, 14, 6, 2, hazardY);
            Fill(17, 14, 6, 2, dark);
            for (int i = 0; i < 3; i++)
            {
                P(18 + i * 2, 14, ((i % 2) == 0) ? hazardY : dark);
                P(42 + i * 2, 14, ((i % 2) == 0) ? hazardY : dark);
            }

            // Rivets / bolts
            void Bolt(int x, int y)
            {
                Fill(x - 1, y - 1, 3, 3, charcoal);
                P(x, y, metalHi);
            }
            Bolt(20, 16);
            Bolt(43, 16);
            Bolt(20, 35);
            Bolt(43, 35);

            // Grille vents
            for (int i = 0; i < 4; i++)
                Fill(24, 20 + i * 3, 16, 1, dark);

            // Side sensor pods
            Fill(10, 22, 6, 8, metalDeep);
            Fill(11, 23, 4, 6, metal);
            Fill(48, 22, 6, 8, metalDeep);
            Fill(49, 23, 4, 6, metal);
            P(12, 25, cyan);
            P(50, 25, green);
            P(50, 25, greenCore);

            // Forward dish / emitter head (+Y)
            Disc(32, 44, 12, 10, metalDeep, outline);
            Disc(32, 44, 10, 8, charcoal, null);
            Disc(32, 44, 7.5f, 6, cyanDeep, null);
            // Dish grid ticks
            for (int a = 0; a < 8; a++)
            {
                float ang = a * Mathf.PI * 0.25f;
                int tx = Mathf.RoundToInt(32 + Mathf.Cos(ang) * 6f);
                int ty = Mathf.RoundToInt(44 + Mathf.Sin(ang) * 5f);
                P(tx, ty, cyan);
            }
            Disc(32, 44, 3.2f, 2.8f, cyan, null);
            P(32, 44, cyanCore);
            P(32, 45, Color.white);
            // Dish mount collar
            Fill(28, 38, 8, 4, orangeDeep);
            Fill(29, 39, 6, 2, orange);
            Fill(30, 40, 4, 1, orangeHi);

            // Antenna stub (rear-right)
            Fill(42, 28, 2, 14, metalDeep);
            Fill(41, 40, 4, 3, charcoal);
            Fill(42, 41, 2, 2, green);
            P(43, 42, greenCore);

            // Rear battery / pack
            Fill(24, 6, 16, 8, charcoal);
            Fill(25, 7, 14, 6, metalDeep);
            Fill(27, 8, 10, 4, hull);
            Fill(28, 9, 3, 2, orange);
            P(36, 10, green);
            P(36, 10, greenCore);

            // Feet / pads
            Fill(18, 4, 8, 4, metalDeep);
            Fill(38, 4, 8, 4, metalDeep);
            Fill(19, 5, 6, 2, metal);

            // Weather / grit
            for (int y = 12; y < 40; y++)
            for (int x = 16; x < 48; x++)
            {
                var c = tex.GetPixel(x, y);
                if (c.a < 0.1f) continue;
                float n = Mathf.PerlinNoise(x * 0.35f + 2f, y * 0.35f);
                if (n > 0.78f) tex.SetPixel(x, y, Color.Lerp(c, rust, 0.35f));
                else if (n < 0.18f) tex.SetPixel(x, y, Color.Lerp(c, dark, 0.25f));
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.38f), s);
        }

        static Sprite MakePipSprite()
        {
            const int s = 8;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = x + 0.5f - s * 0.5f;
                float dy = y + 0.5f - s * 0.5f;
                float r2 = dx * dx + dy * dy;
                Color c = Color.clear;
                if (r2 <= 2.2f * 2.2f) c = Color.white;
                if (r2 > 2.0f * 2.0f && r2 <= 2.6f * 2.6f)
                    c = new Color(1f, 1f, 1f, 0.35f);
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }
    }
}
