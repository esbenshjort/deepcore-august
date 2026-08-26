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

    /// <summary>
    /// Excavator digs a passable tunnel. Supports a queued dig route of pinpoints.
    /// </summary>
    public sealed class FreeWorkerController : MonoBehaviour
    {
        public const float HeatMin = 0f;
        public const float HeatMax = 100f;
        public const float PassiveCoolPerSecond = 5f;
        public const float CoolResumeHeat = 60f;
        public const int PushDecisionDC = 22;
        public const int ExtremeControlDC = 25;
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


        [SerializeField] float moveSpeed = 1.35f;
        [SerializeField] float rotateSpeed = 100f;
        [SerializeField] float digInterval = 0.95f;
        [SerializeField] float tipReach = 0.48f;
        [SerializeField] int toolPower = MiningDamage.DefaultExcavatorToolPower;
        [SerializeField] WorkerStats _stats = new WorkerStats();
        [SerializeField] WorkerConditions _conditions = new WorkerConditions();

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
        readonly List<Vector2> _route = new(16);
        readonly List<Transform> _pinVisuals = new(16);
        int _routeI;
        System.Action<int, int> _onBrokeCell;
        System.Action<TerrainCell, bool> _onDigImpact;
        public event System.Action<int, int> WeakPointFound;

        /// <summary>Balance harness only — dig strike finished (before-cell snapshot, broke).</summary>
        public event System.Action<TerrainCell, bool> BalanceDigImpact;
        /// <summary>Balance harness only — Weak Point roll resolved (success flag).</summary>
        public event System.Action<bool> BalanceWeakPointChecked;
        /// <summary>Balance harness only — machine entered OVERHEATED.</summary>
        public event System.Action BalanceOverheatEvent;
        /// <summary>Balance harness only — Injury applied from overheat check fail.</summary>
        public event System.Action BalanceInjuryEvent;

        /// <summary>Body / Mind / Soul sheet. RawPower + Lithology feed dig damage.</summary>
        public WorkerStats Stats => _stats ??= new WorkerStats();

        /// <summary>Dynamic worker conditions (Frustration, …). Separate from Stats and machine Heat.</summary>
        public WorkerConditions Conditions => _conditions ??= new WorkerConditions();

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
        public bool HasGoal => _hasGoal || _route.Count > 0;
        public int RouteCount => _route.Count;
        public FineTerrainWorld World => _world;
        public Vector2 Goal => _goal;

        /// <summary>Temporary dig readout for the STATS panel.</summary>
        public bool HasLastDig => _hasLastDig;
        public TerrainMaterial LastDigMaterial => _lastDigMaterial;
        public byte LastDigHp => _lastDigHp;
        public byte LastDigMaxHp => _lastDigMaxHp;
        public int LastDigDamage => _lastDigDamage;
        public bool LastDigWeakPoint => _lastDigWeakPoint;
        public float LastDigThermalMul => _lastDigThermalMul;

        /// <summary>Simple machine activity label for debug: OVERHEATED / COOLING / MINING / IDLE.</summary>
        public string MachineActivityLabel
        {
            get
            {
                if (IsOverheated) return "OVERHEATED";
                if (_isCooling) return "COOLING";
                if (IsActivelyDigging || _dugThisTick) return "MINING";
                return "IDLE";
            }
        }

        public void TeleportTo(Vector2 pos)
        {
            transform.localPosition = pos;
            IsActivelyDigging = false;
            // Dig route / pins are instructions — keep them across sleep & commute snaps.
            if (_route.Count > 0)
                ActivateCurrentPin();
            else
                RefreshVisuals();
        }

        /// <summary>
        /// End-of-shift bookmark: keep existing dig pins, or drop a small pin just ahead
        /// of the bit so the excavator resumes into the wall tomorrow.
        /// Returns the body resume position (snapped by caller).
        /// </summary>
        public Vector2 CaptureShiftBreakBookmark()
        {
            Vector2 body = Position;
            if (_route.Count == 0)
            {
                // Quiet "left off here" pin — slightly ahead of facing so arrival digs the wall.
                Vector2 tip = DrillTip(Mathf.Max(0.28f, _moveRadius * 0.9f));
                AddPin(tip, replaceRoute: true);
                DigHoodLog.Push("SHIFT BREAK | Left-off pin placed at dig face");
            }
            else
            {
                DigHoodLog.Push(
                    $"SHIFT BREAK | Keeping dig route | {_route.Count} pin{(_route.Count == 1 ? "" : "s")}");
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
            InitStaminaFromStats();
            DigHoodLog.Push("BALANCE | Conditions reset | Heat 0 | Frust 0 | Injury 0 | Stamina full");
        }

        /// <summary>
        /// Overnight / skipped-sleep cool-down: Heat fully cleared, cooling intent reset.
        /// Machine rests with the crew — not the same as mid-shift passive cooling.
        /// Does not wipe Frustration (sleep recovery is rate / duration based).
        /// </summary>
        public void ResetHeatAfterRest()
        {
            _heat = HeatMin;
            _isCooling = false;
            _dugThisTick = false;
            IsActivelyDigging = false;
            RefreshHeatZone(logOverheat: false);
            InitStaminaFromStats();
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


        /// <summary>While asleep: Frustration −2 / real second.</summary>
        public void TickRestFrustrationRelief(float deltaTime) =>
            ApplyFrustrationReliefRate("Sleep", FrustrationSleepReliefPerSecond, deltaTime);

        /// <summary>
        /// Apply sleep Frustration recovery for a block of real time
        /// (time-skip remaining night, using elapsed simulation duration).
        /// </summary>
        public void ApplyRestFrustrationForDuration(float realSeconds)
        {
            if (realSeconds <= 0f) return;
            ApplyFrustrationRelief("Sleep", FrustrationSleepReliefPerSecond * realSeconds);
        }

        public void SetCrewVisible(bool on)
        {
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
                r.enabled = on;
            foreach (var l in GetComponentsInChildren<Light2D>(true))
                l.enabled = on;
        }

        /// <summary>Dig pins / route line — intended for scan view only.</summary>
        public void SetRouteVisible(bool visible)
        {
            if (_pinsRoot != null)
                _pinsRoot.gameObject.SetActive(visible);
        }

        public void Setup(FineTerrainWorld world, Vector2 start, float footprintRadius,
            Transform pinsRoot = null, System.Action<int, int> onBrokeCell = null,
            System.Action<TerrainCell, bool> onDigImpact = null, Sprite pinSprite = null)
        {
            _world = world;
            _radius = footprintRadius;
            _digHalf = footprintRadius * 0.72f;
            _moveRadius = footprintRadius * 0.68f;
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
            _routeLine.widthMultiplier = 0.035f;
            _routeLine.numCapVertices = 2;
            _routeLine.sortingOrder = 34;
            var sh = Shader.Find("Sprites/Default");
            if (sh != null) _routeLine.material = new Material(sh);
            _routeLine.startColor = _routeLine.endColor = new Color(0.35f, 1f, 0.55f, 0.45f);
            _routeLine.positionCount = 0;
        }

        /// <summary>LMB append pin. Shift+LMB replace route with one pin.</summary>
        public void AddPin(Vector2 terrainPos, bool replaceRoute)
        {
            if (_world == null) return;
            var size = _world.WorldSize;
            var p = new Vector2(
                Mathf.Clamp(terrainPos.x, _moveRadius, size.x - _moveRadius),
                Mathf.Clamp(terrainPos.y, _moveRadius, size.y - _moveRadius));

            // Snap to cell center — feels like placing a dig marker on the grid
            var cell = _world.WorldToCell(p);
            if (_world.InBounds(cell.x, cell.y))
                p = _world.CellCenter(cell.x, cell.y);

            if (replaceRoute)
            {
                _route.Clear();
                _routeI = 0;
            }
            _route.Add(p);
            RefreshVisuals();
            ActivateCurrentPin();
        }

        /// <summary>Legacy single-goal API — replaces route with one pin.</summary>
        public void SetGoal(Vector2 terrainPos) => AddPin(terrainPos, replaceRoute: true);

        public void UndoLastPin()
        {
            if (_route.Count == 0) return;
            // If we're mid-route, prefer popping unvisited pins first
            if (_route.Count > _routeI + 1)
                _route.RemoveAt(_route.Count - 1);
            else
            {
                _route.RemoveAt(_route.Count - 1);
                _routeI = Mathf.Max(0, _route.Count - 1);
            }
            RefreshVisuals();
            if (_route.Count == 0) ClearRoute();
            else ActivateCurrentPin();
        }

        public void ClearGoal() => ClearRoute();

        public void ClearRoute()
        {
            _route.Clear();
            _routeI = 0;
            _hasGoal = false;
            _stallFrames = 0;
            RefreshVisuals();
        }

        void ActivateCurrentPin()
        {
            if (_route.Count == 0)
            {
                _hasGoal = false;
                return;
            }
            _routeI = Mathf.Clamp(_routeI, 0, _route.Count - 1);
            _goal = _route[_routeI];
            _hasGoal = true;
            _stallFrames = 0;
            RefreshVisuals();
        }

        void AdvanceRoute()
        {
            _routeI++;
            if (_routeI >= _route.Count)
            {
                ClearRoute();
                return;
            }
            ActivateCurrentPin();
        }

        void RefreshVisuals()
        {
            EnsureRouteLine();
            while (_pinVisuals.Count < _route.Count)
            {
                var pin = new GameObject($"Pin{_pinVisuals.Count}").transform;
                pin.SetParent(_pinsRoot != null ? _pinsRoot : transform.parent, false);
                var sr = pin.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = _pinSprite;
                sr.sortingOrder = 36;
                DigVisualKit.ApplyLit(sr);
                pin.localScale = Vector3.one * 0.42f;
                _pinVisuals.Add(pin);
            }

            for (int i = 0; i < _pinVisuals.Count; i++)
            {
                bool on = i < _route.Count;
                _pinVisuals[i].gameObject.SetActive(on);
                if (!on) continue;
                _pinVisuals[i].localPosition = _route[i];
                var sr = _pinVisuals[i].GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                // Current = bright green; upcoming = dimmer; past = muted
                if (i < _routeI)
                    sr.color = new Color(0.35f, 0.55f, 0.4f, 0.35f);
                else if (i == _routeI)
                    sr.color = new Color(0.4f, 1f, 0.55f, 1f);
                else
                    sr.color = new Color(0.45f, 0.9f, 0.6f, 0.7f);
                _pinVisuals[i].localScale = Vector3.one * (i == _routeI ? 0.5f : 0.38f);
            }

            if (_routeLine == null) return;
            if (_route.Count == 0)
            {
                _routeLine.positionCount = 0;
                return;
            }

            // Path from excavator through remaining pins
            int remaining = _route.Count - _routeI;
            _routeLine.positionCount = remaining + 1;
            _routeLine.SetPosition(0, Position);
            for (int i = 0; i < remaining; i++)
                _routeLine.SetPosition(i + 1, _route[_routeI + i]);
            _routeLine.startColor = new Color(0.35f, 1f, 0.55f, 0.55f);
            _routeLine.endColor = new Color(0.35f, 1f, 0.55f, 0.2f);
        }

        public void Tick(Vector2 wasd) => Tick(wasd, clearGoalOnWasd: true);

        public void Tick(Vector2 wasd, bool clearGoalOnWasd)
        {
            if (_world == null) return;
            _dugThisTick = false;

            if (wasd.sqrMagnitude > 0.01f)
            {
                if (clearGoalOnWasd) ClearRoute();
                Step(wasd.normalized, dig: true);
                TickPassiveHeat();
                TickStaminaRecovery();
                return;
            }

            if (!_hasGoal)
            {
                IsActivelyDigging = false;
                TickPassiveHeat();
                TickStaminaRecovery();
                return;
            }

            // Keep route line attached to excavator while moving
            if (_route.Count > 0 && _routeLine != null && _routeLine.positionCount > 0)
                _routeLine.SetPosition(0, Position);

            Vector2 pos = transform.localPosition;
            Vector2 to = _goal - pos;
            float dist = to.magnitude;
            if (dist < 0.1f)
            {
                AdvanceRoute();
                TickPassiveHeat();
                TickStaminaRecovery();
                return;
            }

            Step(to.normalized, dig: true);
            TickPassiveHeat();
                TickStaminaRecovery();
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
            Vector2 perp = new(-dir.y, dir.x);

            for (int ty = y0; ty <= y1; ty++)
            for (int tx = x0; tx <= x1; tx++)
            {
                if (!_world.IsMovementBlocker(tx, ty)) continue;
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
            if (!AllowDigByHeatControl())
                return false;
            return TryDigOnce(pos, tryPos, dir);
        }

        /// <summary>
        /// One dig action: pick one clearance tile and apply damage once.
        /// </summary>
        bool TryDigOnce(Vector2 pos, Vector2 tryPos, Vector2 dir)
        {
            if (!TryPickDigTarget(pos, tryPos, dir, out int tx, out int ty))
                return false;
            return StrikeCell(tx, ty);
        }

        bool TryPickDigTarget(Vector2 pos, Vector2 tryPos, Vector2 dir, out int bestX, out int bestY)
        {
            bestX = -1;
            bestY = -1;
            float bestScore = float.MaxValue;
            float cs = _world.CellSize;
            Vector2 perp = new(-dir.y, dir.x);
            Vector2 tip = pos + dir * (_moveRadius * 0.25f);

            // Pass A: solids overlapping body / next step (must clear to advance).
            // Mild center bias — still clear shoulders so we don't squeeze into a 1-tile shaft.
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
                if (!_world.IsMovementBlocker(tx, ty)) continue;
                var cell = _world.Get(tx, ty);
                if (cell.IsUndamageableBorder) continue;
                if (!CellOverlapsCircle(tx, ty, pos, _moveRadius * 1.05f) &&
                    !CellOverlapsCircle(tx, ty, tryPos, _moveRadius * 1.05f))
                    continue;
                Vector2 c = _world.CellCenter(tx, ty);
                Vector2 d = c - pos;
                float along = Vector2.Dot(d, dir);
                float side = Mathf.Abs(Vector2.Dot(d, perp));
                float score = Vector2.Distance(c, pos) + side * 1.1f - along * 0.15f
                    - (cell.MaxHp - cell.Hp) * 0.05f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestX = tx;
                    bestY = ty;
                }
            }
            if (bestX >= 0) return true;

            // Pass B: fill the pointy half-circle dig face (ellipse), nearest tip first.
            float along0 = -cs * 0.15f;
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
                Vector2 c = _world.CellCenter(tx, ty);
                if (!InDigFaceEnvelope(pos, dir, c)) continue;

                Vector2 dTip = c - tip;
                float along = Vector2.Dot(c - pos, dir);
                float side = Mathf.Abs(Vector2.Dot(c - pos, perp));
                // Carve the rounded tip as a blob: closest to tip center, slight center bias.
                float score = dTip.magnitude + side * 0.55f - along * 0.25f
                    - (cell.MaxHp - cell.Hp) * 0.05f;
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
            if (broke) _onBrokeCell?.Invoke(x, y);
            return true;
        }

        /// <summary>
        /// Obstruction-based Frustration: consecutive digs without meaningful progress.
        /// Meaningful progress = tile destroyed (handled here) or physical advance (Step).
        /// Every 3 obstructed digs adds Frustration; counter keeps rising until progress.
        /// </summary>
        void ApplyDigFrustration(in TerrainCell target, bool brokeTile)
        {
            if (brokeTile)
            {
                ResetObstructionCounter("Tile destroyed");
                ApplyProgressFrustrationRelief("Tile destroyed");
                return;
            }

            _obstructionCounter++;
            if (_obstructionCounter % 3 != 0)
                return;

            int determination = Stats.Get(WorkerStatId.Determination);
            float baseFrustration = 3f;
            float resistance = determination * 0.10f;
            float gained = Mathf.Max(0.5f, baseFrustration - resistance);

            if (target.Material == TerrainMaterial.Bedrock)
                gained *= 1.5f;
            if (target.HasWeakPoint)
                gained *= 0.75f;

            Conditions.AddFrustration(gained);

            DigHoodLog.Push(
                $"FRUSTRATION | Obstruction {_obstructionCounter} digs | Material {target.Material} | " +
                $"Determination {determination} | Added {gained:0.##} | Total {Conditions.Frustration:0.#}/100");
        }

        void NotifyExcavatorAdvanced()
        {
            if (_obstructionCounter <= 0) return;
            ResetObstructionCounter("Excavator advanced");
            ApplyProgressFrustrationRelief("Excavator advanced");
        }

        void ResetObstructionCounter(string reason)
        {
            _obstructionCounter = 0;
            DigHoodLog.Push($"PROGRESS | {reason} | Obstruction reset");
        }

        /// <summary>
        /// Meaningful progress relief: Base 2 + Focus×0.05. Does not change gain rules.
        /// </summary>
        void ApplyProgressFrustrationRelief(string reason)
        {
            int focus = Stats.Get(WorkerStatId.Focus);
            float relief = FrustrationProgressBaseRelief + focus * 0.05f;
            float reduced = Conditions.ReduceFrustration(relief);
            if (reduced <= 0f) return;
            DigHoodLog.Push(
                $"FRUSTRATION RELIEF | Reason {reason} | Focus {focus} | " +
                $"Reduced {reduced:0.##} | Total {Conditions.Frustration:0.#}/100");
        }

        void ApplyFrustrationRelief(string reason, float amount)
        {
            float reduced = Conditions.ReduceFrustration(amount);
            if (reduced <= 0f) return;
            DigHoodLog.Push(
                $"FRUSTRATION RELIEF | Reason {reason} | " +
                $"Reduced {reduced:0.##} | Total {Conditions.Frustration:0.#}/100");
        }

        void ApplyFrustrationReliefRate(string reason, float perSecond, float deltaTime)
        {
            if (perSecond <= 0f || deltaTime <= 0f) return;
            float reduced = Conditions.ReduceFrustration(perSecond * deltaTime);
            if (reduced <= 0f) return;

            _frustrationReliefLogAcc += reduced;
            if (_frustrationReliefLogAcc < 1f) return;

            float logged = _frustrationReliefLogAcc;
            _frustrationReliefLogAcc = 0f;
            DigHoodLog.Push(
                $"FRUSTRATION RELIEF | Reason {reason} | " +
                $"Reduced {logged:0.##} | Total {Conditions.Frustration:0.#}/100");
        }


        /// <summary>
        /// Heat from one completed dig.
        /// BaseHeat: Rock 2, Bedrock 6. SafetyProtocol × 0.1 reduction (floor 0.5).
        /// Weak Point × 0.8. Rhythm above 10 trims heat slightly (still floored at 0.5).
        /// </summary>
        void ApplyDigHeat(in TerrainCell cell)
        {
            float baseHeat = cell.Material == TerrainMaterial.Bedrock ? 6f : 2f;
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
        /// PreferredMaxHeat = clamp(92 − Safety×0.8, 76, 91).
        /// </summary>
        float PreferredMaxHeat
        {
            get
            {
                int safety = Stats.Get(WorkerStatId.SafetyProtocol);
                return Mathf.Clamp(92f - safety * 0.8f, 76f, 91f);
            }
        }

        /// <summary>
        /// Decision gate before a dig strike. Rolls only at dig attempts (not every frame).
        /// EXTREME (95–99): Composure vs DC 25.
        /// At/above PreferredMaxHeat: Determination vs DC 22.
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
                ApplyFrustrationReliefRate("Cooling", FrustrationCoolReliefPerSecond, WorkerSimClock.Delta);
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
                return interval;
            }
        }

        /// <summary>Injury ≥ Care threshold — cannot resume mining; Steward/Triage later.</summary>
        public bool IsTooInjuredToMine => Conditions.Injury >= InjuryCareThreshold;

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
            _staminaRecoveryLogAcc = 0f;
            _lastStaminaStateLog = null;
            LogStamina();
        }

        void ApplyDigStamina()
        {
            Conditions.SpendStamina(StaminaCostPerDig);
            LogStamina();

            if (!Conditions.IsResting && StaminaRatio <= StaminaRestEnterRatio)
                EnterStaminaRest();
        }

        void EnterStaminaRest()
        {
            if (Conditions.IsResting) return;
            Conditions.IsResting = true;
            IsActivelyDigging = false;
            LogStamina();
            DigHoodLog.Push("STAMINA | Entered RESTING — mining paused, assignment kept");
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
            int effectiveFinesse = Mathf.Max(1,
                Mathf.RoundToInt(baseFinesse - frustrationPenalty - heatPenalty));

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
            // Floor width so the tip stays a blunt point (≥ ~2 cells), not a 1-tile line
            float minHalf = cs * 0.85f;
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
