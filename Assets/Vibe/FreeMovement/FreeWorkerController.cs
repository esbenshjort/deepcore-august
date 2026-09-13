using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>Heat band from current Heat value. Separate from WorkerStats.</summary>
    public enum ExcavatorHeatZone : byte
    {
        Normal = 0,     // 0–49
        Optimal = 1,    // 50–79
        Danger = 2,     // 80–94
        Extreme = 3,    // 95–99
        Overheated = 4, // 100
    }

    /// <summary>One excavator dig-route waypoint with planned tunnel width 1–5.</summary>
    public struct DigRoutePin
    {
        public Vector2 Pos;
        public byte Width;

        public DigRoutePin(Vector2 pos, int width)
        {
            Pos = pos;
            Width = (byte)TunnelWidthSpec.Clamp(width);
        }
    }

    /// <summary>
    /// Excavator digs a passable tunnel. Supports a queued dig route of pinpoints.
    /// Stage D: one machine host; assigned <see cref="WorkerRuntime"/> supplies stats + personal conditions.
    /// Implements <see cref="IWorkProvider"/> for Excavation (manager remains assignment authority).
    /// </summary>
    public sealed class FreeWorkerController : MonoBehaviour, IWorkProvider
    {
        static int _nextProviderSerial = 1;

        public const float HeatMin = 0f;
        public const float HeatMax = 100f;
        public const float PassiveCoolPerSecond = 4f;
        public const float CoolResumeHeat = 55f;
        /// <summary>Lower = excavator pushes past preferred max more often (into danger/extreme).</summary>
        public const int PushDecisionDC = 17;
        /// <summary>Higher = composure fails more often at extreme → more overheat lockouts.</summary>
        public const int ExtremeControlDC = 28;
        public const float FrustrationCoolReliefPerSecond = 0.5f;
        public const float FrustrationSleepReliefPerSecond = 2f;
        public const float FrustrationProgressBaseRelief = 2f;
        public const float FrustrationWeakPointRelief = 1f;
        public const float StaminaCostPerDig = 1f;
        public const float StaminaTiredRatio = 0.30f;
        public const float StaminaExhaustedRatio = 0.15f;
        public const float StaminaRestEnterRatio = 0.10f;
        public const float StaminaRestResumeRatio = 0.70f;
        public const float TiredDigIntervalMul = 1.10f;
        public const float ExhaustedDigIntervalMul = 1.25f;
        public const int InjuryOverheatDC = 24;
        public const float InjuryOnFail = 20f;
        public const float InjurySlowThreshold = 20f;
        public const float InjuryCareThreshold = 60f;
        public const float InjuryDigIntervalMul = 1.20f;
        public const float InjuryMoveSpeedMul = 0.85f;


        [SerializeField] float moveSpeed = 1.35f; // MACHINE chassis — not WorkerLocomotion walk

        [SerializeField] float rotateSpeed = 100f;
        [SerializeField] float digInterval = 0.95f;
        [SerializeField] float tipReach = 0.48f;
        [SerializeField] int toolPower = MiningDamage.DefaultExcavatorToolPower;
        [SerializeField] WorkerStats _stats = new WorkerStats();
        /// <summary>Fallback bag when no operator is bound (Setup / unbound). Prefer WorkerRuntime.State.</summary>
        [SerializeField] WorkerState _conditions = WorkerState.CreateDefault();
        WorkerRuntime _assignedWorker;

        FineTerrainWorld _world;
        float _radius;
        float _moveRadius;
        float _digHalf;
        Vector2 _goal;
        bool _hasGoal;
        float _digTimer;
        int _stallFrames;
        bool _lastMoveBlocked = true; // force first move log
        /// <summary>Runtime excavator heat (0–100). Machine state — not a WorkerStat.</summary>
        float _heat;
        ExcavatorHeatZone _heatZone = ExcavatorHeatZone.Normal;
        /// <summary>Worker chose to cool; keeps route/target, digs paused until Heat &lt;= CoolResumeHeat.</summary>
        bool _isCooling;
        /// <summary>True if a dig strike landed this Tick.</summary>
        bool _dugThisTick;
        bool _lastHeatMining; // false so first MINING engagement logs HEAT MODE
        /// <summary>
        /// Consecutive digs since last meaningful progress (tile destroy or physical advance).
        /// Every 3 adds Frustration; continues if obstruction persists.
        /// </summary>
        int _obstructionCounter;
        float _staminaRecoveryLogAcc;
        string _lastStaminaStateLog;
        /// <summary>Batches rate-based relief logs (cooling / sleep) to ~1 unit.</summary>
        float _frustrationReliefLogAcc;

        // Last dig snapshot for temporary STATS debug panel (not a gameplay system).
        bool _hasLastDig;
        TerrainMaterial _lastDigMaterial;
        byte _lastDigHp;
        byte _lastDigMaxHp;
        int _lastDigDamage;
        bool _lastDigWeakPoint;
        float _lastDigThermalMul;
        Transform _pinsRoot;
        LineRenderer _routeLine;
        Sprite _pinSprite;
        readonly List<DigRoutePin> _route = new(16);
        readonly List<Transform> _pinVisuals = new(16);
        int _routeI;
        byte _plannedWidth = TunnelWidthSpec.Standard;
        byte _activeWidth = TunnelWidthSpec.Standard;
        float _baseFootprintRadius = 0.6f;
        System.Action<int, int> _onBrokeCell;
        System.Action<TerrainCell, bool> _onDigImpact;

        /// <summary>Tile-paint excavation plan (authoritative routing V1).</summary>
        readonly ExcavatorTilePaintPlan _paintPlan = new();
        ExcavatorPaintPreview _paintPreview;
        readonly List<(int x, int y, bool valid)> _strokePreviewScratch = new(256);
        int _paintWorkX = -1, _paintWorkY = -1;
        bool _paintDrivenGeometry;
        /// <summary>Pending paint exists but no open-tunnel work front is reachable.</summary>
        bool _awaitingAccess;
        /// <summary>Open-tunnel nav to standing cell beside the work front (existing system).</summary>
        ExcavatedPathfinder _accessNav;
        /// <summary>Worker paused execution (claustro/injury/etc.) — plan cells remain.</summary>
        bool _executionPaused;
        /// <summary>
        /// Manager push willingness: returns stress ceiling for dig refuse.
        /// Default = CriticalAt. Raised temporarily after accepted PUSH HARDER.
        /// </summary>
        public System.Func<WorkerRuntime, float> ResolveClaustroRefuseCeiling;
        /// <summary>Fired when dig pauses for critical confinement (plan retained).</summary>
        public System.Action<WorkerRuntime> OnClaustroRefusePause;
        public event System.Action<int, int> WeakPointFound;
        /// <summary>Collapse debris chip (cell x,y).</summary>
        public event System.Action<int, int> DebrisStrike;

        /// <summary>Balance harness only — dig strike finished (before-cell snapshot, broke).</summary>
        public event System.Action<TerrainCell, bool> BalanceDigImpact;
        /// <summary>Balance harness only — Weak Point roll resolved (success flag).</summary>
        public event System.Action<bool> BalanceWeakPointChecked;
        /// <summary>Balance harness only — machine entered OVERHEATED.</summary>
        public event System.Action BalanceOverheatEvent;
        /// <summary>Balance harness only — Injury applied from overheat check fail.</summary>
        public event System.Action BalanceInjuryEvent;

        public WorkerStats Stats =>
            _assignedWorker != null ? _assignedWorker.Stats : (_stats ??= new WorkerStats());

        /// <summary>Person currently operating this machine (null if unbound).</summary>
        public WorkerRuntime AssignedWorker => _assignedWorker;

        // ——— IWorkProvider (Excavation) ———
        public string ProviderId { get; private set; } = "excavator.0";
        public JobType JobType => DeepCore.FreeMovement.JobType.Excavation;
        /// <summary>−1 if none. Mirror of manager ownership.</summary>
        public int AssignedWorkerId { get; private set; } = -1;
        /// <summary>Stage D: Excavation always accepts ownership transfer (machine state persists).</summary>
        public bool IsAvailable => true;

        public bool CanAssign(WorkerRuntime worker, out string reason)
        {
            reason = "";
            if (worker == null)
            {
                reason = "No worker";
                return false;
            }
            return true;
        }

        public void NotifyAssigned(WorkerRuntime worker)
        {
            AssignedWorkerId = worker != null ? worker.WorkerId : -1;
        }

        public void NotifyUnassigned()
        {
            AssignedWorkerId = -1;
        }

        /// <summary>
        /// Bind this machine to a person. Stats + personal conditions come from the WorkerRuntime.
        /// Machine heat / route / cooling are not transferred from the previous operator.
        /// </summary>
        public void BindWorker(WorkerRuntime worker)
        {
            if (worker == null || worker.Stats == null) return;
            if (string.IsNullOrEmpty(ProviderId) || ProviderId == "excavator.0")
                ProviderId = $"excavator.{_nextProviderSerial++}";

            _assignedWorker = worker;
            _stats = worker.Stats;
            _stats.ClampAll();
            EnsurePersonalStaminaPrimed();
            NotifyAssigned(worker);
        }

        public void ClearWorker()
        {
            _assignedWorker = null;
            _stats = WorkerStats.CreateBaseline();
            AssignedWorkerId = -1;
            // Do not InitStamina on unbound fallback — machine waits for next operator
        }

        /// <summary>
        /// Stop dig engagement and clear active-work visuals. Machine stays put; heat cools passively.
        /// </summary>
        public void ParkMachineIdle()
        {
            IsActivelyDigging = false;
            _dugThisTick = false;
            _digTimer = 0f;
        }

        /// <summary>
        /// Stop this operator's dig engagement. Machine heat, cooling intent, route, and tool power persist.
        /// Personal conditions stay on the WorkerRuntime (not wiped).
        /// </summary>
        public void YieldForReassignment()
        {
            ParkMachineIdle();
            DigHoodLog.Push(
                $"ASSIGN | Excavator yield | Heat {_heat:0.#} | Cooling {_isCooling} | " +
                $"Route {_route.Count} | Overheat {IsOverheated}");
        }

        /// <summary>
        /// Dynamic person state: assigned WorkerRuntime.State, else unbound fallback.
        /// Frustration / stamina / injury are never machine-owned.
        /// </summary>
        public WorkerState Conditions =>
            _assignedWorker != null
                ? _assignedWorker.State
                : (_conditions ??= WorkerState.CreateDefault());

        /// <summary>Max stamina pool from WorkerStats.Stamina: 50 + Stamina×5.</summary>
        public float MaxStamina => 50f + Stats.Get(WorkerStatId.Stamina) * 5f;

        /// <summary>Runtime stamina remaining (Worker Condition).</summary>
        public float CurrentStamina => Conditions.CurrentStamina;

        /// <summary>True while excavator paused digging to recover stamina (route kept).</summary>
        public bool IsStaminaResting => Conditions.IsResting;


        /// <summary>Base tool contribution to DigPower (excavator bit).</summary>
        public int ToolPower => toolPower;

        public float Radius => _radius;
        /// <summary>Current excavator heat 0–100 (machine state).</summary>
        public float Heat => _heat;
        /// <summary>Derived heat zone from Heat.</summary>
        public ExcavatorHeatZone HeatZone => _heatZone;
        /// <summary>Alias for visuals / older callers.</summary>
        public ExcavatorHeatZone HeatState => _heatZone;
        public bool IsOverheated => _heatZone == ExcavatorHeatZone.Overheated;
        public bool IsCooling => _isCooling;
        public bool IsActivelyDigging { get; private set; }
        public Vector2 Facing => transform.up;
        public Vector2 DrillTip(float ahead = 0.42f) => Position + Facing * ahead;
        public Vector2 Position => transform.localPosition;
        public bool HasGoal => _hasGoal || _route.Count > 0 || _paintPlan.PendingDigCount > 0;
        public int RouteCount => Mathf.Max(_route.Count, _paintPlan.PendingDigCount);
        /// <summary>Tile-paint plan (cells). Preferred over legacy pins.</summary>
        public ExcavatorTilePaintPlan PaintPlan => _paintPlan;
        /// <summary>Width used for the next paint stroke (1–5). Hotkeys set this while planning.</summary>
        public int PlannedTunnelWidth
        {
            get => _plannedWidth;
            set
            {
                _plannedWidth = (byte)TunnelWidthSpec.Clamp(value);
                _paintPlan.BrushWidth = _plannedWidth;
                if (_route.Count == 0 && _paintPlan.PendingDigCount == 0)
                    SetActiveTunnelWidth(_plannedWidth);
            }
        }
        /// <summary>Width currently driving dig geometry.</summary>
        public int ActiveTunnelWidth => _activeWidth;
        public float ActiveDigHalfWorld => _digHalf;
        public float ActiveMoveRadiusWorld => _moveRadius;

        public DigRoutePin GetRoutePin(int index) =>
            index >= 0 && index < _route.Count ? _route[index] : default;

        public void SetPlannedTunnelWidth(int width)
        {
            PlannedTunnelWidth = width;
            DigHoodLog.Push(
                $"TUNNEL WIDTH | Brush W{_plannedWidth} · {TunnelWidthSpec.Label(_plannedWidth)} · {TunnelWidthSpec.BrushCells(_plannedWidth)} tiles");
            RefreshPaintPreview();
        }

        /// <summary>
        /// Planning-only brush footprint ring — radius = BrushHalfCells × cellSize
        /// (same as DigHalfCells / paint dig envelope).
        /// </summary>
        public void UpdateBrushWidthRingAt(Vector2 worldPos, bool planningVisible)
        {
            if (_paintPreview == null || _world == null)
                return;
            // Snap to tile under cursor so the ring matches stamped brush cells
            var cell = _world.WorldToCell(worldPos);
            Vector2 center = _world.CellCenter(cell.x, cell.y);
            _paintPreview.UpdateBrushWidthRing(
                center, _world.CellSize, _plannedWidth, planningVisible);
        }

        public void HideBrushWidthRing()
        {
            _paintPreview?.UpdateBrushWidthRing(Vector2.zero, 1f, _plannedWidth, false);
        }

        void SetActiveTunnelWidth(int width)
        {
            _activeWidth = (byte)TunnelWidthSpec.Clamp(width);
            ApplyWidthGeometry(_activeWidth, paintDriven: _paintDrivenGeometry);
        }

        void ApplyWidthGeometry(int width, bool paintDriven = false)
        {
            float cs = _world != null ? Mathf.Max(0.05f, _world.CellSize) : 0.1f;
            _paintDrivenGeometry = paintDriven;
            if (paintDriven)
            {
                _digHalf = ExcavatorTilePaintPlan.PaintDigHalfWorld(width, cs);
                _moveRadius = _digHalf * (TunnelWidthSpec.Clamp(width) <= 2 ? 0.88f : 0.92f);
            }
            else
            {
                _digHalf = TunnelWidthSpec.DigHalfCells(width) * cs;
                _moveRadius = TunnelWidthSpec.MoveRadiusCells(width) * cs;
            }
            // Keep legacy _radius as max(body, dig) for loose-pile / misc callers
            _radius = Mathf.Max(_baseFootprintRadius * 0.68f, _moveRadius);
            if (_accessNav != null)
                _accessNav.SetMachineProfile(_moveRadius, TunnelWidthSpec.WorkerMinClearance);
        }
        public FineTerrainWorld World => _world;

        /// <summary>
        /// 0 = soft/open, 1 = hard bedrock face under the bit — drives impact sparks.
        /// </summary>
        public float DigFaceHardness
        {
            get
            {
                if (_world == null || !IsActivelyDigging) return 0f;
                Vector2 tip = DrillTip(0.38f);
                var cell = _world.WorldToCell(tip);
                if (!_world.InBounds(cell.x, cell.y) || !_world.IsMovementBlocker(cell.x, cell.y))
                {
                    tip = DrillTip(0.55f);
                    cell = _world.WorldToCell(tip);
                    if (!_world.InBounds(cell.x, cell.y) || !_world.IsMovementBlocker(cell.x, cell.y))
                        return 0.35f;
                }
                var c = _world.Get(cell.x, cell.y);
                if (c.Material == TerrainMaterial.Bedrock) return 1f;
                float armorT = Mathf.InverseLerp(
                    FineTerrainWorld.RockArmor, FineTerrainWorld.BedrockArmor, c.Armor);
                return Mathf.Clamp01(0.45f + armorT * 0.4f);
            }
        }
        public Vector2 Goal => _goal;

        /// <summary>Temporary dig readout for the STATS panel.</summary>
        public bool HasLastDig => _hasLastDig;
        public TerrainMaterial LastDigMaterial => _lastDigMaterial;
        public byte LastDigHp => _lastDigHp;
        public byte LastDigMaxHp => _lastDigMaxHp;
        public int LastDigDamage => _lastDigDamage;
        public bool LastDigWeakPoint => _lastDigWeakPoint;
        public float LastDigThermalMul => _lastDigThermalMul;

        /// <summary>Machine activity label — never invents dig work.</summary>
        public string MachineActivityLabel
        {
            get
            {
                if (IsOverheated) return "OVERHEATED";
                if (_isCooling) return "COOLING";
                if (_executionPaused) return "REFUSED";
                if (IsActivelyDigging || _dugThisTick) return "MINING";
                if (_awaitingAccess && _paintPlan.PendingDigCount > 0) return "AWAITING ACCESS";
                if (_paintPlan.PendingDigCount > 0) return "ENROUTE";
                return "AWAITING ORDERS";
            }
        }

        public bool AwaitingExcavationOrders =>
            _paintPlan.PendingDigCount <= 0 && !_paintPlan.IsStroking;

        public bool AwaitingAccess =>
            _awaitingAccess && _paintPlan.PendingDigCount > 0;

        public bool ExecutionPaused => _executionPaused;

        /// <summary>Pause dig execution without clearing player plan (claustro / manager gameplay).</summary>
        public void PauseExecution(string reason)
        {
            _executionPaused = true;
            _hasGoal = false;
            IsActivelyDigging = false;
            DigHoodLog.Push($"PAUSE | {reason} — plan retained ({_paintPlan.PendingDigCount} cells)");
        }

        public void ResumeExecutionIfPossible()
        {
            if (!_executionPaused) return;
            var wr = _assignedWorker;
            if (wr?.State != null && wr.State.ClaustroSeekingExit)
            {
                float ceiling = ResolveRefuseCeiling(wr);
                if (wr.State.ClaustrophobicStress >= ceiling)
                    return;
            }
            // SeekingExit cleared (e.g. returned to open camp) — resume even if stress still high
            if (wr?.State != null)
                wr.State.ClaustroSeekingExit = false;
            _executionPaused = false;
            // Re-acquire the same painted plan after pause/refusal
            SyncGoalFromPaintPlan();
            DigHoodLog.Push("RESUME | Excavation orders still pending");
        }

        float ResolveRefuseCeiling(WorkerRuntime wr)
        {
            if (ResolveClaustroRefuseCeiling != null)
            {
                float c = ResolveClaustroRefuseCeiling(wr);
                if (c > 0f) return c;
            }
            return ClaustrophobiaBands.CriticalAt;
        }

        public void TeleportTo(Vector2 pos)
        {
            Vector2 from = Position;
            if (_world != null && _world.CircleHitsSolid(pos, _moveRadius * 0.85f))
            {
                var c = _world.WorldToCell(pos);
                bool found = false;
                for (int r = 0; r <= 10 && !found; r++)
                {
                    for (int oy = -r; oy <= r && !found; oy++)
                    for (int ox = -r; ox <= r && !found; ox++)
                    {
                        if (r > 0 && Mathf.Abs(ox) != r && Mathf.Abs(oy) != r) continue;
                        int x = c.x + ox, y = c.y + oy;
                        if (!_world.IsTunnelOpen(x, y)) continue;
                        Vector2 cand = _world.CellCenter(x, y);
                        if (_world.CircleHitsSolid(cand, _moveRadius * 0.8f)) continue;
                        pos = cand;
                        found = true;
                    }
                }
            }
            transform.localPosition = pos;
            WorkerRelocationLog.Report(
                AssignedWorker != null ? AssignedWorker.DisplayName : "Excavator",
                from, pos, "TeleportTo", "FreeWorkerController");
            IsActivelyDigging = false;
            // Dig plan / pins are instructions — keep them across sleep & commute snaps.
            if (_paintPlan.PendingDigCount > 0)
                SyncGoalFromPaintPlan();
            else if (_route.Count > 0)
                ActivateCurrentPin();
            else
                RefreshVisuals();
        }

        /// <summary>
        /// End-of-shift bookmark: keep player dig plan only — never invent new excavation cells.
        /// </summary>
        public Vector2 CaptureShiftBreakBookmark()
        {
            Vector2 body = Position;
            if (_paintPlan.PendingDigCount > 0 || _paintPlan.HasPlan)
            {
                DigHoodLog.Push(
                    $"SHIFT BREAK | Keeping dig plan | {_paintPlan.PendingDigCount} pending cell(s)");
            }
            else if (_route.Count > 0)
            {
                DigHoodLog.Push(
                    $"SHIFT BREAK | Keeping dig route | {_route.Count} pin{(_route.Count == 1 ? "" : "s")}");
            }
            else
            {
                DigHoodLog.Push("SHIFT BREAK | No dig plan — awaiting orders next shift");
            }
            return body;
        }

        /// <summary>
        /// Debug/balance harness: clear Heat + worker conditions for a clean comparison run.
        /// Does not clear dig route or rebuild the map.
        /// </summary>
        public void ResetBalanceTestState()
        {
            _heat = HeatMin;
            _heatZone = ExcavatorHeatZone.Normal;
            _isCooling = false;
            _dugThisTick = false;
            IsActivelyDigging = false;
            Conditions.Frustration = 0f;
            Conditions.Injury = 0f;
            Conditions.NeedsCare = false;
            Conditions.MentalFatigue = WorkerState.DefaultMentalFatigue;
            Conditions.FocusState = WorkerState.DefaultFocusState;
            Conditions.Morale = WorkerState.DefaultMorale;
            InitStaminaFromStats();
            DigHoodLog.Push("BALANCE | Conditions reset | Heat 0 | Frust 0 | Injury 0 | Stamina full");
        }

        /// <summary>
        /// Overnight / skipped-sleep cool-down: Heat fully cleared, cooling intent reset.
        /// Machine only — person PhysicalStamina recovers via crew WorkerState sleep recovery.
        /// </summary>
        public void ResetHeatAfterRest()
        {
            _heat = HeatMin;
            _isCooling = false;
            _dugThisTick = false;
            IsActivelyDigging = false;
            RefreshHeatZone(logOverheat: false);
            DigHoodLog.Push("HEAT | Rest cool-down | Total 0/100 | State NORMAL");
        }

        /// <summary>
        /// Field repair by the Engineer: clears OVERHEATED so the drill can run again.
        /// Leaves residual Heat at CoolResumeHeat (not a full overnight cool).
        /// </summary>
        public void ClearOverheatByEngineer()
        {
            _heat = CoolResumeHeat;
            _isCooling = false;
            _dugThisTick = false;
            IsActivelyDigging = false;
            RefreshHeatZone(logOverheat: false);
            DigHoodLog.Push(
                $"REPAIR | Engineer cleared overheat | Total {_heat:0.#}/100 | State {HeatZoneLabel(_heatZone)}");
        }


        public void SetCrewVisible(bool on)
        {
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
                r.enabled = on;
            foreach (var l in GetComponentsInChildren<Light2D>(true))
                l.enabled = on;
        }

        /// <summary>Dig paint preview — scan / excavation job view.</summary>
        public void SetRouteVisible(bool visible)
        {
            if (_pinsRoot != null)
                _pinsRoot.gameObject.SetActive(visible);
            _paintPreview?.SetVisible(visible);
            if (!visible)
                HideBrushWidthRing();
        }

        public void Setup(FineTerrainWorld world, Vector2 start, float footprintRadius,
            Transform pinsRoot = null, System.Action<int, int> onBrokeCell = null,
            System.Action<TerrainCell, bool> onDigImpact = null, Sprite pinSprite = null)
        {
            _world = world;
            _baseFootprintRadius = footprintRadius;
            _radius = footprintRadius;
            _plannedWidth = TunnelWidthSpec.Standard;
            _activeWidth = TunnelWidthSpec.Standard;
            _paintDrivenGeometry = false;
            ApplyWidthGeometry(_activeWidth, paintDriven: false);
            transform.localPosition = start;
            _hasGoal = false;
            _stallFrames = 0;
            _heat = 0f;
            _heatZone = ExcavatorHeatZone.Normal;
            _isCooling = false;
            _dugThisTick = false;
            _obstructionCounter = 0;
            Conditions.ResetAll();
            InitStaminaFromStats();
            _onBrokeCell = onBrokeCell;
            _onDigImpact = onDigImpact;
            _pinSprite = pinSprite;
            _pinsRoot = pinsRoot;
            if (string.IsNullOrEmpty(ProviderId) || ProviderId == "excavator.0")
                ProviderId = $"excavator.{_nextProviderSerial++}";
            _paintPlan.Bind(world);
            _paintPlan.BrushWidth = _plannedWidth;
            EnsureAccessNav();
            if (_paintPreview == null)
            {
                _paintPreview = new ExcavatorPaintPreview();
                _paintPreview.Setup(pinsRoot != null ? pinsRoot : transform.parent);
            }
            EnsureRouteLine();
            ClearRoute();
        }

        void EnsureRouteLine()
        {
            if (_pinsRoot == null) return;
            if (_routeLine != null) return;
            var go = new GameObject("RouteLine");
            go.transform.SetParent(_pinsRoot, false);
            _routeLine = go.AddComponent<LineRenderer>();
            _routeLine.useWorldSpace = false;
            _routeLine.loop = false;
            _routeLine.widthMultiplier = 1f;
            _routeLine.startWidth = 0.012f;
            _routeLine.endWidth = 0.008f;
            _routeLine.numCapVertices = 2;
            _routeLine.sortingOrder = 34;
            var sh = Shader.Find("Sprites/Default");
            if (sh != null) _routeLine.material = new Material(sh);
            _routeLine.startColor = _routeLine.endColor = new Color(0.35f, 1f, 0.55f, 0.22f);
            _routeLine.positionCount = 0;
        }

        /// <summary>Begin LMB paint stroke at world position.</summary>
        public void BeginPaintStroke(Vector2 terrainPos)
        {
            if (_world == null) return;
            _paintPlan.BrushWidth = _plannedWidth;
            _paintPlan.BeginStroke(terrainPos);
            RefreshPaintPreview();
        }

        public void SamplePaintStroke(Vector2 terrainPos)
        {
            if (_world == null || !_paintPlan.IsStroking) return;
            _paintPlan.SampleStroke(terrainPos);
            RefreshPaintPreview();
        }

        public void CommitPaintStroke()
        {
            if (_world == null) return;
            int n = _paintPlan.CommitStroke(Position);
            DigHoodLog.Push(n > 0
                ? $"DIG PLAN | Stroke +{n} cells · W{_plannedWidth}"
                : "DIG PLAN | Stroke empty / invalid");
            _route.Clear();
            _routeI = 0;
            // New player orders: try to leave claustro pause so dig can start again
            if (_executionPaused)
                ResumeExecutionIfPossible();
            SyncGoalFromPaintPlan();
            RefreshPaintPreview();
        }

        public void CancelPaintStroke()
        {
            _paintPlan.CancelStroke();
            RefreshPaintPreview();
        }

        /// <summary>Legacy API — stamps a short paint stroke toward the point (Enter / tests).</summary>
        public void AddPin(Vector2 terrainPos, bool replaceRoute)
        {
            if (_world == null) return;
            if (replaceRoute)
                ClearRoute();
            BeginPaintStroke(Position);
            SamplePaintStroke(terrainPos);
            CommitPaintStroke();
        }

        /// <summary>Legacy single-goal API — replaces plan with one stroke to target.</summary>
        public void SetGoal(Vector2 terrainPos) => AddPin(terrainPos, replaceRoute: true);

        public void UndoLastPin()
        {
            if (_paintPlan.IsStroking)
            {
                CancelPaintStroke();
                return;
            }
            // Erase near excavator facing / last work cell
            Vector2 eraseAt = _paintWorkX >= 0
                ? _world.CellCenter(_paintWorkX, _paintWorkY)
                : Position + Facing * (_world != null ? _world.CellSize * 2f : 0.2f);
            int n = _paintPlan.EraseAt(eraseAt, _plannedWidth);
            if (n == 0 && _route.Count > 0)
            {
                if (_route.Count > _routeI + 1)
                    _route.RemoveAt(_route.Count - 1);
                else
                {
                    _route.RemoveAt(_route.Count - 1);
                    _routeI = Mathf.Max(0, _route.Count - 1);
                }
                if (_route.Count == 0) ClearRoute();
                else ActivateCurrentPin();
                RefreshVisuals();
                return;
            }
            DigHoodLog.Push(n > 0 ? $"DIG PLAN | Erased {n} cells" : "DIG PLAN | Nothing to erase");
            _paintPlan.RevalidateConnectivity(Position);
            SyncGoalFromPaintPlan();
            RefreshPaintPreview();
        }

        public void ErasePaintAt(Vector2 worldPos)
        {
            int n = _paintPlan.EraseAt(worldPos, _plannedWidth);
            if (n > 0)
                DigHoodLog.Push($"DIG PLAN | Erased {n} cells");
            _paintPlan.RevalidateConnectivity(Position);
            SyncGoalFromPaintPlan();
            RefreshPaintPreview();
        }

        public void ClearGoal() => ClearRoute();

        public void ClearRoute()
        {
            _route.Clear();
            _routeI = 0;
            _hasGoal = false;
            _stallFrames = 0;
            _paintWorkX = _paintWorkY = -1;
            _paintPlan.CancelStroke();
            _paintPlan.Clear();
            _paintDrivenGeometry = false;
            _awaitingAccess = false;
            _accessNav?.Invalidate();
            _executionPaused = false;
            SetActiveTunnelWidth(_plannedWidth);
            RefreshVisuals();
            RefreshPaintPreview();
        }

        void EnsureAccessNav()
        {
            if (_world == null) return;
            if (_accessNav == null)
                _accessNav = new ExcavatedPathfinder(_world, _moveRadius);
            else
                _accessNav.Bind(_world);
            // Clearance 1 — excavator must traverse its own narrow painted tunnels
            _accessNav.SetMachineProfile(_moveRadius, minClearance: TunnelWidthSpec.WorkerMinClearance);
        }

        void SyncGoalFromPaintPlan()
        {
            if (_executionPaused)
            {
                _hasGoal = false;
                return;
            }
            // PendingDigCount = Valid + Excavating (not only Valid — MarkExcavating must not empty the queue)
            if (_paintPlan.PendingDigCount <= 0)
            {
                _paintWorkX = _paintWorkY = -1;
                _awaitingAccess = false;
                _accessNav?.Invalidate();
                if (_route.Count == 0)
                {
                    _hasGoal = false;
                    _paintDrivenGeometry = false;
                    ApplyWidthGeometry(_activeWidth, paintDriven: false);
                }
                return;
            }

            EnsureAccessNav();
            int minC = TunnelWidthSpec.WorkerMinClearance;
            // Do NOT RevalidateConnectivity here — that path can mark Valid painted cells
            // Invalid when access BFS fails for loco reasons, wiping player orders.
            if (!_paintPlan.TryPickAccessibleWorkFront(Position, minC,
                    out int x, out int y, out int w, out Vector2 stand))
            {
                _hasGoal = false;
                _paintWorkX = _paintWorkY = -1;
                _awaitingAccess = true;
                _accessNav?.Invalidate();
                DigHoodLog.Push(
                    $"DIG PLAN | AWAITING ACCESS — {_paintPlan.PendingDigCount} pending, no open-tunnel work front");
                return;
            }

            bool retarget = _paintWorkX != x || _paintWorkY != y
                            || (_goal - stand).sqrMagnitude > 0.0025f;
            _paintWorkX = x;
            _paintWorkY = y;
            _paintPlan.MarkExcavating(x, y);
            _goal = stand; // stand on open floor beside the painted face — never the rock cell
            _paintDrivenGeometry = true;
            _awaitingAccess = false;
            SetActiveTunnelWidth(w);
            _hasGoal = true;
            _stallFrames = 0;
            if (retarget)
                _accessNav?.Invalidate();
            // Hide legacy pin line authority
            if (_routeLine != null) _routeLine.positionCount = 0;
            for (int i = 0; i < _pinVisuals.Count; i++)
                _pinVisuals[i].gameObject.SetActive(false);
        }

        void ActivateCurrentPin()
        {
            if (_paintPlan.PendingDigCount > 0)
            {
                SyncGoalFromPaintPlan();
                return;
            }
            if (_route.Count == 0)
            {
                _hasGoal = false;
                return;
            }
            _routeI = Mathf.Clamp(_routeI, 0, _route.Count - 1);
            _goal = _route[_routeI].Pos;
            _paintDrivenGeometry = false;
            SetActiveTunnelWidth(_route[_routeI].Width);
            _hasGoal = true;
            _stallFrames = 0;
            RefreshVisuals();
        }

        void AdvanceRoute()
        {
            if (_paintPlan.PendingDigCount > 0 || _paintWorkX >= 0)
            {
                // Only complete a planned cell once the world tile is actually clear.
                // False-completing here skipped dig when unsupervised (WASD=0 / other worker selected).
                if (_paintWorkX >= 0)
                {
                    if (PaintWorkCellStillBlocks())
                    {
                        SyncGoalFromPaintPlan();
                        return;
                    }
                    _paintPlan.NotifyCellExcavated(_paintWorkX, _paintWorkY);
                }
                _paintPlan.RevalidateConnectivity(Position);
                SyncGoalFromPaintPlan();
                // Never invent new dig cells — idle when plan exhausted
                if (!_hasGoal)
                {
                    _paintWorkX = _paintWorkY = -1;
                    DigHoodLog.Push("DIG PLAN | Complete — awaiting excavation orders");
                }
                RefreshPaintPreview();
                return;
            }

            _routeI++;
            if (_routeI >= _route.Count)
            {
                ClearRoute();
                DigHoodLog.Push("DIG PLAN | Complete — awaiting excavation orders");
                return;
            }
            ActivateCurrentPin();
        }

        /// <summary>
        /// V1.1: Excavator must NEVER invent excavation targets. Kept as dead stub for safety.
        /// </summary>
        bool TryContinueChewingAtFace() => false;

        void RefreshPaintPreview()
        {
            if (_paintPreview == null || _world == null) return;
            _strokePreviewScratch.Clear();
            if (_paintPlan.IsStroking)
                _paintPlan.GetStrokePreview(_strokePreviewScratch, Position);
            _paintPreview.Rebuild(_world, _paintPlan, _strokePreviewScratch);
        }

        void RefreshVisuals()
        {
            // Legacy pin/line kept for debug but not authoritative when paint plan exists
            EnsureRouteLine();
            if (_paintPlan.HasPlan || _paintPlan.IsStroking)
            {
                if (_routeLine != null) _routeLine.positionCount = 0;
                for (int i = 0; i < _pinVisuals.Count; i++)
                    _pinVisuals[i].gameObject.SetActive(false);
                RefreshPaintPreview();
                return;
            }

            while (_pinVisuals.Count < _route.Count)
            {
                var pin = new GameObject($"Pin{_pinVisuals.Count}").transform;
                pin.SetParent(_pinsRoot != null ? _pinsRoot : transform.parent, false);
                var sr = pin.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = _pinSprite;
                sr.sortingOrder = 36;
                DigVisualKit.ApplyLit(sr);
                pin.localScale = Vector3.one * 0.22f;
                _pinVisuals.Add(pin);
            }

            for (int i = 0; i < _pinVisuals.Count; i++)
            {
                bool on = i < _route.Count;
                _pinVisuals[i].gameObject.SetActive(on);
                if (!on) continue;
                byte w = _route[i].Width;
                _pinVisuals[i].localPosition = _route[i].Pos;
                var sr = _pinVisuals[i].GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                Color baseCol = TunnelWidthSpec.PreviewColor(w);
                if (i < _routeI)
                    sr.color = new Color(baseCol.r, baseCol.g, baseCol.b, 0.2f);
                else if (i == _routeI)
                    sr.color = new Color(baseCol.r, baseCol.g, baseCol.b, 0.9f);
                else
                    sr.color = new Color(baseCol.r, baseCol.g, baseCol.b, 0.45f);
                float pinScale = 0.14f + w * 0.028f;
                _pinVisuals[i].localScale = Vector3.one * (i == _routeI ? pinScale * 1.25f : pinScale);
            }

            if (_routeLine == null) return;
            if (_route.Count == 0)
            {
                _routeLine.positionCount = 0;
                return;
            }

            int remaining = _route.Count - _routeI;
            _routeLine.positionCount = remaining + 1;
            _routeLine.SetPosition(0, Position);
            for (int i = 0; i < remaining; i++)
                _routeLine.SetPosition(i + 1, _route[_routeI + i].Pos);

            float half = _digHalf;
            if (_routeI < _route.Count)
            {
                float cs = _world != null ? _world.CellSize : 0.1f;
                half = TunnelWidthSpec.DigHalfCells(_route[_routeI].Width) * cs;
            }
            float visualW = Mathf.Clamp(half * 0.55f, 0.04f, 0.55f);
            _routeLine.startWidth = visualW;
            _routeLine.endWidth = visualW * 0.72f;
            var col = TunnelWidthSpec.PreviewColor(_activeWidth);
            _routeLine.startColor = new Color(col.r, col.g, col.b, 0.38f);
            _routeLine.endColor = new Color(col.r, col.g, col.b, 0.14f);
        }

        public void Tick(Vector2 wasd) => Tick(wasd, clearGoalOnWasd: true);

        /// <summary>Heat/stamina only — never digs. Use when operator is away (commute, toilet, off-shift).</summary>
        public void TickPassiveOnly()
        {
            if (_world == null) return;
            ParkMachineIdle();
            TickPassiveHeat();
            if (_assignedWorker != null)
                TickStaminaRecovery();
        }

        public void Tick(Vector2 wasd, bool clearGoalOnWasd)
        {
            if (_world == null) return;
            _dugThisTick = false;

            // F0.5b vacancy / unmanned: machine stays; passive heat only — no dig
            if (_assignedWorker == null)
            {
                ParkMachineIdle();
                TickPassiveHeat();
                return;
            }

            if (wasd.sqrMagnitude > 0.01f)
            {
                // Player drive: move freely; dig only against player-painted cells
                if (clearGoalOnWasd && _paintPlan.PendingDigCount <= 0)
                {
                    // Don't wipe a committed plan just for steering — only clear empty legacy routes
                    if (_route.Count > 0) ClearRoute();
                }
                bool mayDig = (_paintPlan.PendingDigCount > 0 || _paintWorkX >= 0)
                              && !_executionPaused;
                Step(wasd.normalized, dig: mayDig);
                TickPassiveHeat();
                TickStaminaRecovery();
                return;
            }

            if (_executionPaused)
            {
                ResumeExecutionIfPossible();
                if (_executionPaused)
                {
                    ParkMachineIdle();
                    TickPassiveHeat();
                    TickStaminaRecovery();
                    return;
                }
            }

            // Unsupervised dig: keep a live work front. Do not re-pick every frame while still
            // chewing the current painted face (avoids post-pause target thrash / no Damage).
            if (_paintPlan.PendingDigCount > 0
                && (_paintWorkX < 0 || !PaintWorkCellStillBlocks() || !_hasGoal || _awaitingAccess))
                SyncGoalFromPaintPlan();

            if (!_hasGoal)
            {
                SyncGoalFromPaintPlan();
                if (!_hasGoal)
                {
                    ParkMachineIdle();
                    TickPassiveHeat();
                    TickStaminaRecovery();
                    return;
                }
            }

            // Keep route line attached to excavator while moving
            if (_route.Count > 0 && _routeLine != null && _routeLine.positionCount > 0)
                _routeLine.SetPosition(0, Position);

            Vector2 pos = transform.localPosition;

            // Paint work cell cleared while approaching — retarget
            if (_paintWorkX >= 0 && _world != null
                && !_world.IsMovementBlocker(_paintWorkX, _paintWorkY)
                && !_world.HasBlockingDebris(_paintWorkX, _paintWorkY))
            {
                _paintPlan.NotifyCellExcavated(_paintWorkX, _paintWorkY);
                SyncGoalFromPaintPlan();
                RefreshPaintPreview();
                if (!_hasGoal)
                {
                    TickPassiveHeat();
                    TickStaminaRecovery();
                    return;
                }
                pos = transform.localPosition;
            }

            float standArrive = Mathf.Max(0.1f, _moveRadius * 1.15f);
            bool atStand = (_goal - pos).sqrMagnitude <= standArrive * standArrive;

            // Painted work in dig reach: excavate it. Large chassis often intersects the
            // painted face before standArrive / while dig:false approach is body-blocked —
            // without this, orders stall forever with dig disabled.
            if (_paintWorkX >= 0 && PaintWorkCellStillBlocks())
            {
                Vector2 workCenter = _world.CellCenter(_paintWorkX, _paintWorkY);
                float digReach = _digHalf + _moveRadius + _world.CellSize * 1.25f;
                bool workInReach = (workCenter - pos).sqrMagnitude <= digReach * digReach;
                if (atStand || workInReach)
                {
                    Vector2 digDir = workCenter - pos;
                    if (digDir.sqrMagnitude < 0.0001f)
                        digDir = Facing.sqrMagnitude > 0.0001f ? Facing : Vector2.up;
                    else
                        digDir.Normalize();
                    Step(digDir, dig: true);
                    TickPassiveHeat();
                    TickStaminaRecovery();
                    return;
                }
            }

            // Far from face — walk EXISTING tunnels only (never dig unpainted rock en route)
            if (_paintWorkX >= 0 && !atStand)
            {
                EnsureAccessNav();
                _accessNav.Follow(
                    pos, _goal, moveSpeed, _moveRadius,
                    face: Face,
                    tryStep: (dir, _) =>
                    {
                        Vector2 before = transform.localPosition;
                        Step(dir, dig: false);
                        Vector2 after = transform.localPosition;
                        return (after - before).sqrMagnitude > 1e-8f;
                    });
                TickPassiveHeat();
                TickStaminaRecovery();
                return;
            }

            if (atStand)
            {
                AdvanceRoute();
                TickPassiveHeat();
                TickStaminaRecovery();
                return;
            }

            // Legacy pin route (no paint plan): walk without inventing digs
            Vector2 to = _goal - pos;
            if (to.sqrMagnitude < 0.01f)
            {
                AdvanceRoute();
                TickPassiveHeat();
                TickStaminaRecovery();
                return;
            }
            Step(to.normalized, dig: false);
            TickPassiveHeat();
            TickStaminaRecovery();
        }

        bool PaintWorkCellStillBlocks()
        {
            if (_paintWorkX < 0 || _world == null) return false;
            return _world.IsMovementBlocker(_paintWorkX, _paintWorkY)
                   || _world.HasBlockingDebris(_paintWorkX, _paintWorkY);
        }

        /// <summary>
        /// Solids that must be gone before the body may advance: footprint + dig-face band.
        /// </summary>
        int CountClearanceSolids(Vector2 bodyPos, Vector2 dir) =>
            _world.CountSolidInCircle(bodyPos, _moveRadius) + CountSolidInDigFace(bodyPos, dir);

        bool PathBlockedByRock(Vector2 pos, Vector2 dir) =>
            dir.sqrMagnitude >= 0.0001f && CountClearanceSolids(pos, dir) > 0;

        void Step(Vector2 dir, bool dig)
        {
            if (dir.sqrMagnitude < 0.0001f) return;

            // OVERHEATED: no dig, no move (can still face toward input for readability)
            if (IsOverheated)
            {
                Face(dir);
                IsActivelyDigging = false;
                return;
            }

            Face(dir);
            IsActivelyDigging = false;

            Vector2 pos = transform.localPosition;
            float mul = LoosePile.SpeedMulAt(pos, _moveRadius) * InjuryMoveMul;
            Vector2 tryPos = pos + dir * (moveSpeed * mul * WorkerSimClock.Delta);

            int remainingHere = CountClearanceSolids(pos, dir);
            bool stepBlocked = remainingHere > 0 || _world.CircleHitsSolid(tryPos, _moveRadius);

            // COOLING: keep task/route, but no dig strikes until Heat drops.
            if (_isCooling)
            {
                IsActivelyDigging = false;
                if (stepBlocked)
                {
                    _stallFrames++;
                    LogMoveState(blocked: true, remaining: remainingHere > 0 ? remainingHere : 1);
                    return;
                }
                transform.localPosition = tryPos;
                _stallFrames = 0;
                LogMoveState(blocked: false, remaining: 0);
                NotifyExcavatorAdvanced();
                return;
            }

            

            // STAMINA RESTING: keep task/route, no dig strikes until stamina recovers.
            if (Conditions.IsResting)
            {
                IsActivelyDigging = false;
                if (stepBlocked)
                {
                    _stallFrames++;
                    LogMoveState(blocked: true, remaining: remainingHere > 0 ? remainingHere : 1);
                    return;
                }
                transform.localPosition = tryPos;
                _stallFrames = 0;
                LogMoveState(blocked: false, remaining: 0);
                NotifyExcavatorAdvanced();
                return;
            }

            // Serious Injury: keep task/route, but cannot resume mining until future Care/Triage.
            if (IsTooInjuredToMine)
            {
                IsActivelyDigging = false;
                if (stepBlocked)
                {
                    _stallFrames++;
                    LogMoveState(blocked: true, remaining: remainingHere > 0 ? remainingHere : 1);
                    return;
                }
                transform.localPosition = tryPos;
                _stallFrames = 0;
                LogMoveState(blocked: false, remaining: 0);
                NotifyExcavatorAdvanced();
                return;
            }

            // Dig while blocked — engaged mining for the whole cadence (including digInterval waits).
            if (dig && stepBlocked)
            {
                IsActivelyDigging = true; // engaged with a mining target (not only on damage frames)

                _digTimer -= WorkerSimClock.Delta;
                if (_digTimer <= 0f)
                {
                    if (TryAuthorizedDig(pos, tryPos, dir))
                        _digTimer = EffectiveDigInterval;
                    else
                        _digTimer = EffectiveDigInterval; // decision spent; don't spam rolls every frame
                }

                // Dig may have triggered OVERHEAT — freeze in place
                if (IsOverheated)
                {
                    IsActivelyDigging = false;
                    return;
                }

                remainingHere = CountClearanceSolids(pos, dir);
                stepBlocked = remainingHere > 0 || _world.CircleHitsSolid(tryPos, _moveRadius);
            }

            if (stepBlocked)
            {
                _stallFrames++;
                if (dig)
                    IsActivelyDigging = true;
                LogMoveState(blocked: true, remaining: remainingHere > 0 ? remainingHere : 1);
                return;
            }

            transform.localPosition = tryPos;
            _stallFrames = 0;
            LogMoveState(blocked: false, remaining: 0);
            NotifyExcavatorAdvanced();
        }

        /// <summary>
        /// Solid cells in the immediate dig-face band (one body-step ahead, dig width).
        /// Kept short so clearance matches what TryDigOnce can actually hit.
        /// </summary>
                int CountSolidInDigFace(Vector2 bodyPos, Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return 0;
            float cs = _world.CellSize;
            float along0 = -cs * 0.15f;
            float along1 = DigTipDepth + cs * 0.35f;
            float half = _digHalf + cs; // search bounds; envelope tested per-cell

            int n = 0;
            ForEachDigFaceCell(bodyPos, dir, along0, along1, half, (tx, ty) => { n++; });
            return n;
        }


        void ForEachDigFaceCell(Vector2 bodyPos, Vector2 dir, float along0, float along1, float half,
            System.Action<int, int> fn)
        {
            float cs = _world.CellSize;
            int x0 = Mathf.FloorToInt((bodyPos.x - half - cs) / cs);
            int x1 = Mathf.FloorToInt((bodyPos.x + half + cs) / cs);
            int y0 = Mathf.FloorToInt((bodyPos.y - half - along1) / cs);
            int y1 = Mathf.FloorToInt((bodyPos.y + half + along1) / cs);
            bool paintOnly = _paintPlan.PendingDigCount > 0 || _paintWorkX >= 0;

            for (int ty = y0; ty <= y1; ty++)
            for (int tx = x0; tx <= x1; tx++)
            {
                if (!_world.IsMovementBlocker(tx, ty)) continue;
                if (paintOnly && !_paintPlan.IsPendingDig(tx, ty))
                {
                    // Body footprint blockers still count even if unplanned
                    if (!CellOverlapsCircle(tx, ty, bodyPos, _moveRadius * 1.05f))
                        continue;
                }
                Vector2 c = _world.CellCenter(tx, ty);
                if (!InDigFaceEnvelope(bodyPos, dir, c)) continue;
                fn(tx, ty);
            }
        }

        void LogMoveState(bool blocked, int remaining)
        {
            if (blocked == _lastMoveBlocked && blocked) return; // avoid spam while held
            if (!blocked && !_lastMoveBlocked) return;
            _lastMoveBlocked = blocked;
            if (blocked)
                DigHoodLog.Push($"MOVE BLOCKED | remaining solid tiles: {remaining}");
            else
                DigHoodLog.Push("MOVE ALLOWED | clearance achieved");
        }

        void Face(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation,
                Quaternion.Euler(0f, 0f, ang),
                rotateSpeed * WorkerSimClock.Delta);
        }

        /// <summary>
        /// Heat-aware dig gate: control rolls, then one strike.
        /// </summary>
        bool TryAuthorizedDig(Vector2 pos, Vector2 tryPos, Vector2 dir)
        {
            if (IsOverheated || _isCooling || Conditions.IsResting || IsTooInjuredToMine)
                return false;
            // Absolute authority: no dig without player-painted pending cells
            if (_paintPlan.PendingDigCount <= 0 && _paintWorkX < 0)
                return false;
            if (_executionPaused)
                return false;
            if (ShouldRefuseDeeperDig(dir))
                return false;
            if (!AllowDigByHeatControl())
                return false;
            return TryDigOnce(pos, tryPos, dir);
        }

        /// <summary>
        /// Critical confinement — refuse digging further. Plan is retained (never cleared).
        /// Accepted manager PUSH HARDER temporarily raises the refuse ceiling.
        /// </summary>
        bool ShouldRefuseDeeperDig(Vector2 digDir)
        {
            var wr = _assignedWorker;
            if (wr?.State == null) return false;
            float stress = wr.State.ClaustrophobicStress;
            if (stress < ClaustrophobiaBands.CriticalAt && !wr.State.ClaustroSeekingExit)
                return false;

            float ceiling = ResolveRefuseCeiling(wr);
            // Still willing under an accepted push — stress keeps rising; not "fine"
            if (stress < ceiling)
                return false;

            // Soft grit without a push: very determined/brave can scrape until ~95
            if (ceiling <= ClaustrophobiaBands.CriticalAt + 0.01f)
            {
                int det = wr.Stats != null ? wr.Stats.Get(WorkerStatId.Determination) : 10;
                int brav = wr.Stats != null ? wr.Stats.Get(WorkerStatId.Bravery) : 10;
                if (det + brav >= 30 && stress < 95f)
                    return false;
            }

            PauseExecution("CLAUSTROPHOBIA | REFUSES TO CONTINUE — plan retained");
            EmitOperatorState(WorkerStateEventType.WorkBlocked, 4.5f, "ClaustroRefuse");
            OnClaustroRefusePause?.Invoke(wr);
            return true;
        }

        bool TryDigOnce(Vector2 pos, Vector2 tryPos, Vector2 dir)
        {
            if (_paintPlan.PendingDigCount <= 0 && _paintWorkX < 0)
                return false;
            if (TryPickDigTarget(pos, tryPos, dir, out int tx, out int ty))
                return StrikeCell(tx, ty);

            // Fallback after pause / envelope miss: strike the live paint work cell if still solid
            if (_paintWorkX >= 0 && _world != null && PaintWorkCellStillBlocks())
            {
                float cs = _world.CellSize;
                Vector2 cc = _world.CellCenter(_paintWorkX, _paintWorkY);
                float reach = _digHalf + _moveRadius + cs * 1.25f;
                if ((cc - pos).sqrMagnitude <= reach * reach)
                    return StrikeCell(_paintWorkX, _paintWorkY);
            }
            return false;
        }

        bool TryPickDigTarget(Vector2 pos, Vector2 tryPos, Vector2 dir, out int bestX, out int bestY)
        {
            bestX = -1;
            bestY = -1;
            // Only player-painted cells — never auto-select arbitrary rock
            if (_paintPlan.PendingDigCount <= 0 && _paintWorkX < 0)
                return false;

            float bestScore = float.MaxValue;
            float cs = _world.CellSize;
            Vector2 perp = new(-dir.y, dir.x);

            // Pass A: solids overlapping body / next step (must clear to advance).
            float r = _moveRadius + cs * 0.25f;
            float minX = Mathf.Min(pos.x, tryPos.x) - r;
            float maxX = Mathf.Max(pos.x, tryPos.x) + r;
            float minY = Mathf.Min(pos.y, tryPos.y) - r;
            float maxY = Mathf.Max(pos.y, tryPos.y) + r;
            int bx0 = Mathf.FloorToInt(minX / cs) - 1;
            int bx1 = Mathf.FloorToInt(maxX / cs) + 1;
            int by0 = Mathf.FloorToInt(minY / cs) - 1;
            int by1 = Mathf.FloorToInt(maxY / cs) + 1;
            for (int ty = by0; ty <= by1; ty++)
            for (int tx = bx0; tx <= bx1; tx++)
            {
                if (!_world.InBounds(tx, ty)) continue;
                bool debris = _world.HasBlockingDebris(tx, ty);
                if (!_world.IsMovementBlocker(tx, ty) && !debris) continue;
                if (!debris)
                {
                    var cell = _world.Get(tx, ty);
                    if (cell.IsUndamageableBorder) continue;
                }
                bool pending = _paintPlan.IsPendingDig(tx, ty);
                bool bodyHit = CellOverlapsCircle(tx, ty, pos, _moveRadius * 1.05f)
                               || CellOverlapsCircle(tx, ty, tryPos, _moveRadius * 1.05f);
                // HARD AUTHORITY: only player-painted pending cells (debris may clear body)
                if (!debris && !pending)
                    continue;
                if (debris && !pending && !bodyHit)
                    continue;
                if (!bodyHit)
                    continue;
                Vector2 c = _world.CellCenter(tx, ty);
                Vector2 d = c - pos;
                float along = Vector2.Dot(d, dir);
                float side = Mathf.Abs(Vector2.Dot(d, perp));
                float score = Vector2.Distance(c, pos) + side * 1.1f - along * 0.15f;
                if (pending) score -= 2.5f;
                if (tx == _paintWorkX && ty == _paintWorkY) score -= 2f;
                if (!debris)
                {
                    var cell = _world.Get(tx, ty);
                    score -= (cell.MaxHp - cell.Hp) * 0.05f;
                }
                else
                    score -= 0.8f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestX = tx;
                    bestY = ty;
                }
            }
            if (bestX >= 0) return true;

            Vector2 tip = pos + dir * (_moveRadius * 0.25f);
            float along1 = DigTipDepth + cs * 0.35f;
            float bound = _digHalf + cs * 1.2f;
            int fx0 = Mathf.FloorToInt((pos.x - bound - along1) / cs);
            int fx1 = Mathf.FloorToInt((pos.x + bound + along1) / cs);
            int fy0 = Mathf.FloorToInt((pos.y - bound - along1) / cs);
            int fy1 = Mathf.FloorToInt((pos.y + bound + along1) / cs);
            for (int ty = fy0; ty <= fy1; ty++)
            for (int tx = fx0; tx <= fx1; tx++)
            {
                if (!_world.InBounds(tx, ty)) continue;
                if (!_world.IsMovementBlocker(tx, ty)) continue;
                var cell = _world.Get(tx, ty);
                if (cell.IsUndamageableBorder) continue;
                if (!_paintPlan.IsPendingDig(tx, ty)) continue;
                Vector2 c = _world.CellCenter(tx, ty);
                if (!InDigFaceEnvelope(pos, dir, c)) continue;

                Vector2 dTip = c - tip;
                float along = Vector2.Dot(c - pos, dir);
                float side = Mathf.Abs(Vector2.Dot(c - pos, perp));
                float score = dTip.magnitude + side * 0.55f - along * 0.25f
                    - (cell.MaxHp - cell.Hp) * 0.05f;
                if (tx == _paintWorkX && ty == _paintWorkY) score -= 1.5f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestX = tx;
                    bestY = ty;
                }
            }

            return bestX >= 0;
        }


        bool StrikeCell(int x, int y)
        {
            // Hard simulation guard — unpainted solid rock is never excavated
            if (!_world.HasBlockingDebris(x, y) && !_paintPlan.IsPendingDig(x, y))
            {
                DigHoodLog.Push($"DIG BLOCKED | Unpainted cell ({x},{y}) — no authority");
                return false;
            }

            if (_world.HasBlockingDebris(x, y))
            {
                DebrisStrike?.Invoke(x, y);
                ApplyDigStamina();
                DigHoodLog.Push($"DIG | Debris clearance strike ({x},{y}) hp={_world.GetDebrisHp(x, y)}");
                ExcavationDustFx.SpawnLingering(transform.parent, _world.CellCenter(x, y),
                    _world.CellSize, heavy: true);
                return true;
            }

            EnsureWeakPointRoll(x, y);

            var before = _world.Get(x, y);
            var strike = PreviewMiningDamage(before);
            var zone = ClassifyHeatZone(_heat);
            float mul = ThermalDamageMultiplier(zone);
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(strike.Damage * mul));
            bool broke = _world.Damage(x, y, finalDamage);

            // Snapshot after strike for STATS panel (Hp is post-damage).
            var after = _world.Get(x, y);
            _hasLastDig = true;
            _lastDigMaterial = before.Material;
            _lastDigMaxHp = before.MaxHp;
            _lastDigHp = after.Hp;
            _lastDigDamage = finalDamage;
            _lastDigWeakPoint = before.HasWeakPoint;
            _lastDigThermalMul = mul;

            DigHoodLog.Push(
                $"DIG | Base Penetration {strike.BaseArmorPenetration:0.#} | " +
                $"Weak Point Penetration {strike.WeakPointArmorPenetration:0.#} | " +
                $"Effective Armor {strike.EffectiveArmor:0.#} | Damage {strike.Damage}");
            DigHoodLog.Push(
                $"THERMAL BONUS | Zone {HeatZoneLabel(zone)} | DamageMultiplier {mul:0.00} | FinalDamage {finalDamage}");

            ApplyDigHeat(before);
            ApplyDigFrustration(before, broke);
            ApplyDigStamina();

            _onDigImpact?.Invoke(before, broke);
            BalanceDigImpact?.Invoke(before, broke);
            if (broke)
            {
                _onBrokeCell?.Invoke(x, y);
                _paintPlan.NotifyCellExcavated(x, y);
                if (x == _paintWorkX && y == _paintWorkY)
                    SyncGoalFromPaintPlan();
                RefreshPaintPreview();
            }
            else if (_paintPlan.IsPendingDig(x, y))
            {
                _paintPlan.MarkExcavating(x, y);
                RefreshPaintPreview();
            }
            return true;
        }

        /// <summary>
        /// Obstruction-based Frustration via person-targeted events (V1.2B).
        /// Continuous counter stays local; every 3 obstructed digs emits WorkBlocked / RepeatedFailure.
        /// Determination/Composure applied in <see cref="WorkerStateEventProcessor"/> — do not AddFrustration here.
        /// </summary>
        void ApplyDigFrustration(in TerrainCell target, bool brokeTile)
        {
            if (brokeTile)
            {
                ResetObstructionCounter("Tile destroyed");
                EmitOperatorState(WorkerStateEventType.ProgressSuccess,
                    FrustrationProgressBaseRelief, "TileDestroyed");
                return;
            }

            _obstructionCounter++;
            if (_obstructionCounter % 2 != 0)
                return;

            float mag = 4.8f;
            if (target.Material == TerrainMaterial.Bedrock)
                mag *= 1.55f;
            if (target.HasWeakPoint)
                mag *= 0.75f;
            // Longer stuck streaks hit harder (compound into RepeatedFailure sooner)
            if (_obstructionCounter >= 6)
                mag *= 1.2f;

            var type = _obstructionCounter >= 6
                ? WorkerStateEventType.RepeatedFailure
                : WorkerStateEventType.WorkBlocked;

            EmitOperatorState(type, mag, "RouteObstruction");
            DigHoodLog.Push(
                $"STATE EVENT | {type} | Obstruction {_obstructionCounter} digs | Material {target.Material} | " +
                $"Mag {mag:0.##} | OperatorId {AssignedWorkerId}");
        }

        void NotifyExcavatorAdvanced()
        {
            if (_obstructionCounter <= 0) return;
            ResetObstructionCounter("Excavator advanced");
            EmitOperatorState(WorkerStateEventType.ProgressSuccess,
                FrustrationProgressBaseRelief, "ExcavatorAdvanced");
        }

        void ResetObstructionCounter(string reason)
        {
            _obstructionCounter = 0;
            DigHoodLog.Push($"PROGRESS | {reason} | Obstruction reset");
        }

        /// <summary>Legacy direct relief — prefer events. Kept for any non-migrated callers.</summary>
        void ApplyProgressFrustrationRelief(string reason)
        {
            EmitOperatorState(WorkerStateEventType.ProgressSuccess,
                FrustrationProgressBaseRelief, reason);
        }

        void ApplyFrustrationRelief(string reason, float amount)
        {
            EmitOperatorState(WorkerStateEventType.ProgressSuccess, amount, reason);
        }

        void ApplyFrustrationReliefRate(string reason, float perSecond, float deltaTime)
        {
            // Continuous rate relief (sleep/cooling) retired from excavator; no-op.
        }

        /// <summary>Emit to the operator at this moment — WorkerId frozen on the event.</summary>
        void EmitOperatorState(WorkerStateEventType type, float magnitude, string source)
        {
            int id = AssignedWorkerId;
            if (id <= 0 && _assignedWorker != null)
                id = _assignedWorker.WorkerId;
            if (id <= 0) return;
            WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                id, type, magnitude, source, JobType.Excavation, ProviderId));
        }


        /// <summary>
        /// Heat from one completed dig.
        /// BaseHeat: Rock 3.4, Bedrock 9. Tuned so overheat lockouts (engineer repairs) show up often.
        /// SafetyProtocol × 0.1 reduction (floor 0.5). Weak Point × 0.8.
        /// Rhythm above 10 trims heat slightly (still floored at 0.5).
        /// </summary>
        void ApplyDigHeat(in TerrainCell cell)
        {
            float baseHeat = cell.Material == TerrainMaterial.Bedrock ? 9f : 3.4f;
            int safety = Stats.Get(WorkerStatId.SafetyProtocol);
            int rhythm = Stats.Get(WorkerStatId.Rhythm);
            float added = Mathf.Max(0.5f, baseHeat - safety * 0.1f);
            if (cell.HasWeakPoint)
                added *= 0.8f;
            float rhythmCut = Mathf.Max(0, rhythm - 10) * 0.05f;
            added = Mathf.Max(0.5f, added - rhythmCut);

            _dugThisTick = true;
            _heat = Mathf.Clamp(_heat + added, HeatMin, HeatMax);
            RefreshHeatZone(logOverheat: true);

            LogHeatMode(mining: true);
            DigHoodLog.Push(
                $"HEAT | Added {added:0.##} | Total {_heat:0.#}/100 | State {HeatZoneLabel(_heatZone)}");
        }

        /// <summary>
        /// Preferred max heat from Safety Protocol — where the worker normally considers cooling.
        /// PreferredMaxHeat = clamp(88 − Safety×0.75, 72, 86) — earlier push/cool gate → more extreme climbs.
        /// </summary>
        float PreferredMaxHeat
        {
            get
            {
                int safety = Stats.Get(WorkerStatId.SafetyProtocol);
                return Mathf.Clamp(88f - safety * 0.75f, 72f, 86f);
            }
        }

        /// <summary>
        /// Decision gate before a dig strike. Rolls only at dig attempts (not every frame).
        /// EXTREME (95–99): Composure vs ExtremeControlDC.
        /// At/above PreferredMaxHeat: Determination vs PushDecisionDC.
        /// </summary>
        bool AllowDigByHeatControl()
        {
            if (IsOverheated || _isCooling || Conditions.IsResting || IsTooInjuredToMine)
                return false;

            LogHeatControl();

            // EXTREME: emergency composure check before every additional dig
            if (_heat >= 95f && _heat < HeatMax)
            {
                var control = WorkerRoll.Check(Stats, WorkerStatId.Composure, ExtremeControlDC);
                DigHoodLog.Push(
                    $"EXTREME CONTROL | D20 {control.D20} + Composure {control.StatValue} = Total {control.Total} | " +
                    $"DC {ExtremeControlDC} | {(control.Success ? "COOL" : "RISK")}");
                if (control.Success)
                {
                    EnterCooling("extreme control");
                    return false;
                }
                return true; // accept risk of reaching 100
            }

            // Below preferred max — mine freely
            if (_heat < PreferredMaxHeat)
                return true;

            // PreferredMaxHeat decision point: push or cool
            var push = WorkerRoll.Check(Stats, WorkerStatId.Determination, PushDecisionDC);
            DigHoodLog.Push(
                $"PUSH ROLL | D20 {push.D20} + Determination {push.StatValue} = Total {push.Total} | " +
                $"DC {PushDecisionDC} | {(push.Success ? "PUSH" : "COOL")}");
            if (push.Success)
                return true; // one dig cycle, then reconsider next attempt

            EnterCooling("push declined");
            return false;
        }

        void EnterCooling(string reason)
        {
            if (_isCooling || IsOverheated) return;
            _isCooling = true;
            IsActivelyDigging = false;
            LogHeatMode(mining: false);
            DigHoodLog.Push($"HEAT MODE | COOLING ({reason})");
        }

        void ExitCoolingIfReady()
        {
            if (!_isCooling) return;
            if (_heat > CoolResumeHeat) return;
            _isCooling = false;
            DigHoodLog.Push("HEAT MODE | MINING (cool complete)");
            _lastHeatMining = false; // force mode log on next mining engagement
            LogHeatMode(mining: true);
        }

        /// <summary>
        /// Passive cool when not mining, or always while COOLING.
        /// OVERHEATED never cools (stays at 100 until future recovery).
        /// </summary>
        void TickPassiveHeat()
        {
            if (IsOverheated)
                return;

            // Intentional COOLING: always cool, keep assignment, resume at <= 60
            if (_isCooling)
            {
                LogHeatMode(mining: false);
                float before = _heat;
                _heat = Mathf.Max(HeatMin, _heat - PassiveCoolPerSecond * WorkerSimClock.Delta);
                if (!Mathf.Approximately(before, _heat))
                    RefreshHeatZone(logOverheat: false);
                // V1.2A: machine cooling must not modify person Frustration
                ExitCoolingIfReady();
                return;
            }

            // Engaged mining = blocked dig task (including digInterval waits)
            if (IsActivelyDigging || _dugThisTick)
            {
                LogHeatMode(mining: true);
                return;
            }

            LogHeatMode(mining: false);

            float beforeIdle = _heat;
            _heat = Mathf.Max(HeatMin, _heat - PassiveCoolPerSecond * WorkerSimClock.Delta);
            if (Mathf.Approximately(beforeIdle, _heat))
                return;

            RefreshHeatZone(logOverheat: false);
        }

        void LogHeatMode(bool mining)
        {
            if (mining == _lastHeatMining) return;
            _lastHeatMining = mining;
            DigHoodLog.Push(mining ? "HEAT MODE | MINING" : "HEAT MODE | COOLING");
        }

        void LogHeatControl()
        {
            DigHoodLog.Push(
                $"HEAT CONTROL | Heat {_heat:0.#} | Zone {HeatZoneLabel(_heatZone)} | " +
                $"Safety {Stats.Get(WorkerStatId.SafetyProtocol)} | " +
                $"Rhythm {Stats.Get(WorkerStatId.Rhythm)} | " +
                $"Determination {Stats.Get(WorkerStatId.Determination)} | " +
                $"Composure {Stats.Get(WorkerStatId.Composure)}");
        }

        void RefreshHeatZone(bool logOverheat)
        {
            var next = ClassifyHeatZone(_heat);
            if (next == _heatZone) return;

            var prev = _heatZone;
            _heatZone = next;

            if (logOverheat && next == ExcavatorHeatZone.Overheated && prev != ExcavatorHeatZone.Overheated)
            {
                _isCooling = false;
                IsActivelyDigging = false;
                DigHoodLog.Push("OVERHEAT | Excavator disabled");
                BalanceOverheatEvent?.Invoke();
                EmitOperatorState(WorkerStateEventType.EquipmentProblem, 8f, "Overheat");
                // One injury check per OVERHEAT event (zone edge), not while Heat stays at 100.
                PerformOverheatInjuryCheck();
            }
            else if (next != prev && next != ExcavatorHeatZone.Overheated)
            {
                DigHoodLog.Push(
                    $"HEAT | Added 0 | Total {_heat:0.#}/100 | State {HeatZoneLabel(_heatZone)}");
            }
        }

        static ExcavatorHeatZone ClassifyHeatZone(float heat)
        {
            if (heat >= HeatMax) return ExcavatorHeatZone.Overheated;
            if (heat >= 95f) return ExcavatorHeatZone.Extreme;
            if (heat >= 80f) return ExcavatorHeatZone.Danger;
            if (heat >= 50f) return ExcavatorHeatZone.Optimal;
            return ExcavatorHeatZone.Normal;
        }

        static float ThermalDamageMultiplier(ExcavatorHeatZone zone) => zone switch
        {
            ExcavatorHeatZone.Optimal => 1.10f,
            ExcavatorHeatZone.Danger => 1.05f,
            ExcavatorHeatZone.Extreme => 1.00f,
            ExcavatorHeatZone.Overheated => 0f,
            _ => 1.00f,
        };

        static string HeatZoneLabel(ExcavatorHeatZone zone) => zone switch
        {
            ExcavatorHeatZone.Optimal => "OPTIMAL",
            ExcavatorHeatZone.Danger => "DANGER",
            ExcavatorHeatZone.Extreme => "EXTREME",
            ExcavatorHeatZone.Overheated => "OVERHEATED",
            _ => "NORMAL",
        };


        
        float EffectiveDigInterval
        {
            get
            {
                if (Conditions.IsResting) return digInterval;
                float interval = digInterval;
                float ratio = StaminaRatio;
                if (ratio < StaminaExhaustedRatio) interval *= ExhaustedDigIntervalMul;
                else if (ratio <= StaminaTiredRatio) interval *= TiredDigIntervalMul;
                interval *= InjuryDigMul;
                interval *= TunnelWidthSpec.DigCadenceMul(_activeWidth);
                return interval;
            }
        }

        /// <summary>Injury / incapacitation — cannot resume mining.</summary>
        public bool IsTooInjuredToMine =>
            (Conditions != null && Conditions.Incapacitated)
            || Conditions.Injury >= InjuryCareThreshold
            || (_assignedWorker != null
                && ( _assignedWorker.State != null && _assignedWorker.State.Incapacitated
                    || WorkerInjuryConsequences.ForcesOutOfWork(_assignedWorker.Injuries)));

        public bool NeedsCare => Conditions.NeedsCare || IsTooInjuredToMine;

        float InjuryDigMul =>
            Conditions.Injury >= InjurySlowThreshold ? InjuryDigIntervalMul : 1f;

        float InjuryMoveMul =>
            Conditions.Injury >= InjurySlowThreshold ? InjuryMoveSpeedMul : 1f;

        /// <summary>
        /// Called once when the machine first enters OVERHEATED.
        /// Toughness protects the worker; Heat/machine state is unchanged by the roll.
        /// </summary>
        void PerformOverheatInjuryCheck()
        {
            var roll = WorkerRoll.Check(Stats, WorkerStatId.Toughness, InjuryOverheatDC);
            string verdict = roll.Success ? "SAFE" : "INJURED";
            DigHoodLog.Push(
                $"INJURY CHECK | D20 {roll.D20} + Toughness {roll.StatValue} = Total {roll.Total} | " +
                $"DC {InjuryOverheatDC} | {verdict}");

            if (!roll.Success)
            {
                Conditions.AddInjury(InjuryOnFail);
                if (Conditions.Injury >= InjuryCareThreshold)
                    Conditions.NeedsCare = true;
                BalanceInjuryEvent?.Invoke();
                EmitOperatorState(WorkerStateEventType.Injury, InjuryOnFail, "OverheatInjury");
                if (_assignedWorker != null)
                {
                    var typed = WorkerAccidentSystem.PickOverheatInjury();
                    // Meter already applied — add typed record without double-adding full meter
                    var rec = new WorkerInjuryRecord
                    {
                        Type = typed,
                        BodyPart = WorkerInjuryCatalog.DefaultPart(typed),
                        Severity = WorkerInjuryCatalog.SeverityOf(typed),
                        Cause = WorkerInjuryCause.ExcavationOverheat,
                        CauseLabel = "OverheatInjury",
                        InflictedGameHours = WorkerStateClock.GameHours,
                        RecoveryGameHoursTotal = WorkerInjuryCatalog.DefaultRecoveryHours(typed),
                        RecoveryGameHoursLeft = WorkerInjuryCatalog.DefaultRecoveryHours(typed),
                        MeterContribution = InjuryOnFail,
                    };
                    _assignedWorker.Injuries.Add(rec);
                    _assignedWorker.Injuries.SyncNeedsCare(Conditions);
                    _assignedWorker.Injuries.NoteAccident(
                        $"{_assignedWorker.DisplayName} {rec.DisplayName} — overheat");
                }
            }

            LogInjury();
        }

        void LogInjury()
        {
            DigHoodLog.Push(
                $"INJURY | {Conditions.Injury:0.#}/100 | " +
                $"MovementModifier {InjuryMoveMul:0.##} | DigIntervalModifier {InjuryDigMul:0.##}");
        }

        public float StaminaRatio
        {
            get
            {
                float max = MaxStamina;
                return max > 0.001f ? Conditions.CurrentStamina / max : 1f;
            }
        }

        public string StaminaStateLabel
        {
            get
            {
                if (Conditions.IsResting) return "RESTING";
                float ratio = StaminaRatio;
                if (ratio < StaminaExhaustedRatio) return "EXHAUSTED";
                if (ratio <= StaminaTiredRatio) return "TIRED";
                return "NORMAL";
            }
        }

        void InitStaminaFromStats()
        {
            float max = MaxStamina;
            Conditions.SetStamina(max, max);
            Conditions.IsResting = false;
            Conditions.StaminaPrimed = true;
            _staminaRecoveryLogAcc = 0f;
            _lastStaminaStateLog = null;
            LogStamina();
        }

        /// <summary>
        /// Stage D: first bind fills stamina; returning operators keep fatigue / injury / frustration.
        /// </summary>
        void EnsurePersonalStaminaPrimed()
        {
            float max = MaxStamina;
            var c = Conditions;
            if (!c.StaminaPrimed)
            {
                c.SetStamina(max, max);
                c.IsResting = false;
                c.StaminaPrimed = true;
            }
            else
            {
                c.SetStamina(Mathf.Min(c.CurrentStamina, max), max);
                if (c.IsResting && StaminaRatio >= StaminaRestResumeRatio)
                    c.IsResting = false;
            }
            _staminaRecoveryLogAcc = 0f;
            _lastStaminaStateLog = null;
            LogStamina();
        }

        void ApplyDigStamina()
        {
            float cost = StaminaCostPerDig * TunnelWidthSpec.StaminaPerStrikeMul(_activeWidth);
            Conditions.SpendStamina(cost);
            LogStamina();

            if (!Conditions.IsResting && StaminaRatio <= StaminaRestEnterRatio)
                EnterStaminaRest();
        }

        void EnterStaminaRest()
        {
            if (Conditions.IsResting) return;
            Conditions.IsResting = true;
            Conditions.ExhaustionLatched = true;
            IsActivelyDigging = false;
            LogStamina();
            DigHoodLog.Push("STAMINA | Entered RESTING — mining paused, assignment kept");
            EmitOperatorState(WorkerStateEventType.PhysicalExhaustion, 5f, "StaminaRest");
        }

        void ExitStaminaRest()
        {
            if (!Conditions.IsResting) return;
            Conditions.IsResting = false;
            LogStamina();
            DigHoodLog.Push("STAMINA | Resumed mining — stamina recovered");
        }

        /// <summary>
        /// Recover stamina while RESTING (and only then). Heat/Frustration idle recovery
        /// continue via existing TickPassiveHeat paths because digs are paused.
        /// </summary>
        void TickStaminaRecovery()
        {
            if (!Conditions.IsResting) return;

            int recoveryStat = Stats.Get(WorkerStatId.Recovery);
            float perSec = 1f + recoveryStat * 0.20f;
            float added = Conditions.AddStamina(perSec * WorkerSimClock.Delta, MaxStamina);
            if (added > 0f)
            {
                _staminaRecoveryLogAcc += added;
                if (_staminaRecoveryLogAcc >= 1f)
                {
                    float logged = _staminaRecoveryLogAcc;
                    _staminaRecoveryLogAcc = 0f;
                    DigHoodLog.Push(
                        $"RECOVERY | RecoveryStat {recoveryStat} | Added {logged:0.##} | " +
                        $"Current {Conditions.CurrentStamina:0.#}/{MaxStamina:0.#}");
                }
            }

            if (StaminaRatio >= StaminaRestResumeRatio)
                ExitStaminaRest();
        }

                void LogStamina()
        {
            string state = StaminaStateLabel;
            _lastStaminaStateLog = state;
            DigHoodLog.Push(
                $"STAMINA | Current {Conditions.CurrentStamina:0.#} / Max {MaxStamina:0.#} | State {state}");
        }

/// <summary>
        /// One Weak Point check when first working a tile.
        /// D20 + EffectiveFinesse (runtime: Finesse reduced by Frustration/Focus and Heat Tolerance zone penalty).
        /// Result is stored on the tile until destroyed — never rerolled. Does not mutate WorkerStats.
        /// </summary>
        void EnsureWeakPointRoll(int x, int y)
        {
            var cell = _world.Get(x, y);
            if (cell.WeakPointChecked) return;
            if (cell.Phase == TerrainPhase.Excavated || cell.IsUndamageableBorder) return;

            float frustration = Conditions.Frustration;
            int focus = Stats.Get(WorkerStatId.Focus);
            int heatTolerance = Stats.Get(WorkerStatId.HeatTolerance);
            int baseFinesse = Stats.Get(WorkerStatId.Finesse);
            float frustrationPenalty = ComputeTechnicalPenalty(frustration, focus);
            float heatPenalty = ComputeHeatTolerancePenalty(_heatZone, heatTolerance);
            float focusStatePenalty = WorkerJobDemand.LowFocusFinessePenalty(Conditions);
            int effectiveFinesse = Mathf.Max(1,
                Mathf.RoundToInt(baseFinesse - frustrationPenalty - heatPenalty - focusStatePenalty));

            DigHoodLog.Push(
                $"FOCUS | Frustration {frustration:0.#} | Focus {focus} | BaseFinesse {baseFinesse} | " +
                $"Penalty {frustrationPenalty:0.##} | EffectiveFinesse {effectiveFinesse}");
            DigHoodLog.Push(
                $"HEAT TOLERANCE | Zone {HeatZoneLabel(_heatZone)} | HeatTolerance {heatTolerance} | " +
                $"Penalty {heatPenalty:0.##} | EffectiveFinesse {effectiveFinesse}");

            int dc = FineTerrainWorld.WeakPointDC(cell.Material);
            var roll = WorkerRoll.CheckStatValue(
                effectiveFinesse, dc, modifiers: 0, WorkerStatId.Finesse);
            if (!_world.TrySetWeakPoint(x, y, roll.Success))
                return;

            DigHoodLog.Push(
                $"WEAK POINT | {cell.Material} | D20 {roll.D20} | Finesse {roll.StatValue} | " +
                $"Total {roll.Total} | DC {dc} | {(roll.Success ? "SUCCESS" : "FAIL")}");

            BalanceWeakPointChecked?.Invoke(roll.Success);

            if (roll.Success)
            {
                ApplyFrustrationRelief("Weak Point", FrustrationWeakPointRelief);
                WeakPointFound?.Invoke(x, y);
            }
        }

        /// <summary>
        /// Runtime technical mining pressure from Frustration, resisted by Focus.
        /// Only applies when Frustration &gt; 25. Clamped 0–8. Does not touch WorkerStats.
        /// </summary>
        static float ComputeTechnicalPenalty(float frustration, int focus)
        {
            float frustrationPressure = Mathf.Max(0f, frustration - 25f);
            float focusResistance = focus * 2f;
            float effectivePressure = Mathf.Max(0f, frustrationPressure - focusResistance);
            return Mathf.Clamp(effectivePressure * 0.10f, 0f, 8f);
        }

        /// <summary>
        /// BODY Heat Tolerance resisting technical penalty in DANGER / EXTREME heat zones.
        /// NORMAL / OPTIMAL: 0. Does not affect machine Heat or Raw Power.
        /// </summary>
        static float ComputeHeatTolerancePenalty(ExcavatorHeatZone zone, int heatTolerance)
        {
            float basePenalty;
            float reductionPerPoint;
            switch (zone)
            {
                case ExcavatorHeatZone.Danger:
                    basePenalty = 2f;
                    reductionPerPoint = 0.10f;
                    break;
                case ExcavatorHeatZone.Extreme:
                    basePenalty = 5f;
                    reductionPerPoint = 0.20f;
                    break;
                default:
                    return 0f;
            }

            return Mathf.Max(0f, basePenalty - heatTolerance * reductionPerPoint);
        }

        /// <summary>
        /// Deterministic strike against a tile's Armor (from material profile).
        /// Uses WorkerStats RawPower + Lithology (+ Spatial Geometry if Weak Point active).
        /// </summary>
        public MiningDamage.Result PreviewMiningDamage(in TerrainCell cell) =>
            MiningDamage.Compute(toolPower, Stats, cell);

        public int CalcMiningDamage(byte rockArmor) =>
            MiningDamage.ComputeAmount(toolPower, Stats, rockArmor);

                /// <summary>
        /// Forward dig envelope half-width: a pointy half-circle / half-ellipse.
        /// Full width near the bit, rounding into a tip — never a 1-tile needle shaft.
        /// </summary>
        float DigTipDepth => tipReach + _moveRadius * 0.55f;

        float AllowedHalfWidth(float along)
        {
            float a = _digHalf;          // half-width at the mouth
            float b = DigTipDepth;       // how far the tip sticks out
            float cs = _world.CellSize;

            if (along <= 0f)
                return a;
            if (along >= b)
                return cs * 0.55f; // last cell of the tip (~1 tile half → ~2 tiles wide nose)

            // Ellipse: (side/a)^2 + (along/b)^2 = 1  →  half-circle-ish when a≈b
            float t = along / b;
            float half = a * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
            float minHalf = TunnelWidthSpec.TipMinHalfCells(_activeWidth) * cs;
            return Mathf.Max(half, minHalf * (1f - t * t));
        }

        bool InDigFaceEnvelope(Vector2 bodyPos, Vector2 dir, Vector2 cellCenter)
        {
            Vector2 d = cellCenter - bodyPos;
            float along = Vector2.Dot(d, dir);
            float side = Mathf.Abs(Vector2.Dot(d, new Vector2(-dir.y, dir.x)));
            float cs = _world.CellSize;
            if (along < -cs * 0.25f || along > DigTipDepth + cs * 0.35f)
                return false;
            return side <= AllowedHalfWidth(Mathf.Max(0f, along)) + cs * 0.2f;
        }


        bool CellOverlapsCircle(int tx, int ty, Vector2 center, float radius)
        {
            float cs = _world.CellSize;
            float cx0 = tx * cs;
            float cy0 = ty * cs;
            float closestX = Mathf.Clamp(center.x, cx0, cx0 + cs);
            float closestY = Mathf.Clamp(center.y, cy0, cy0 + cs);
            float dx = center.x - closestX;
            float dy = center.y - closestY;
            return dx * dx + dy * dy <= radius * radius;
        }

        void OnValidate()
        {
            _stats?.ClampAll();
        }
    }
}
