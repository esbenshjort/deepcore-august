using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    public enum ScanDistance : byte { Short = 0, Medium = 1, Long = 2 }
    public enum ScanWidth : byte { Narrow = 0, Wide = 1 }
    public enum ProspectorWorkMode : byte
    {
        Manual = 0,
        SurveyNearby = 1,
        AssistExcavator = 2,
        Investigate = 3,
    }

    /// <summary>
    /// Prospector with a soft quarter-circle radar (3 rows × 3 cols).
    /// Space starts a slow sweep into the rock; hints pop as the wave reaches them.
    /// </summary>
    public sealed class ProspectorPerson : MonoBehaviour
    {
        const float FullHalfAngle = 45f;
        const int RowCount = 3;
        const int ColCount = 3;

        struct PendingHit
        {
            public float Dist01; // 0 near → 1 far (within selected range)
            public int X, Y;
            public ScanHintKind Kind;
            public float Strength;
        }

        FineTerrainWorld _world;
        ScanViewOverlay _scanView;
        FreeWorkerController _excavator;
        RefinerPerson _refiner;
        ExcavatedPathfinder _nav;
        ProspectorInvestigationLoop _investigation;
        float _radius = 0.1f;
        float _moveSpeed = 1.55f;
        float _autoMoveSpeed = 1.15f;
        Transform _facingRoot;
        Transform _coneRoot;
        readonly List<LineRenderer> _arcCore = new(3);
        readonly List<LineRenderer> _arcGlow = new(3);
        readonly List<LineRenderer> _radialCore = new(4);
        readonly List<LineRenderer> _radialGlow = new(4);
        LineRenderer _sweepCore;
        LineRenderer _sweepGlow;
        MeshFilter _sweepFillMf;
        MeshRenderer _sweepFillMr;
        Mesh _sweepFillMesh;
        Light2D _lamp;

        bool _hudVisible = true;
        bool _scanning;
        float _scanT;
        float _scanDuration = 2f;
        float _scanCooldown;
        float _pulse; // idle radar breath
        readonly List<PendingHit> _pending = new(48);
        Vector2 _scanOrigin;
        Vector2 _scanFwd;
        float _scanRangeWorld;
        float _reliability;

        // Auto work modes
        float _workRetargetT;
        float _workStudyT;
        float _workBanterT;
        float _stuckT;
        Vector2 _workGoal;
        Vector2 _lastPos;
        bool _hasWorkGoal;
        int _focusX = -1, _focusY = -1;
        int _surveyRing;

        // Stage 1–2: heavy scanner setup + scan assignment
        ProspectorScannerEquipment _assignedScanner;
        bool _scannerTravelActive;
        bool _scannerSetupActive;
        float _debugSetupSpeedMul = 1f;
        float _debugScanSpeedMul = 1f;
        ProspectorScanHistory _scanHistory;

        public ScanDistance Distance { get; private set; } = ScanDistance.Medium;
        public ScanWidth Width { get; private set; } = ScanWidth.Narrow;
        public ProspectorWorkMode WorkMode { get; private set; } = ProspectorWorkMode.Manual;
        public bool RadarOn { get; private set; }
        public bool IsScanning => _scanning;
        public bool HasScannerAssignment => _assignedScanner != null;
        public ProspectorScannerEquipment AssignedScanner => _assignedScanner;
        public bool IsSettingUpScanner => _scannerSetupActive;
        public ProspectorScanHistory ScanHistory => _scanHistory ??= new ProspectorScanHistory();

        public void BindScanHistory(ProspectorScanHistory history)
        {
            if (history != null) _scanHistory = history;
        }

        public Vector2 Position => transform.localPosition;
        public Vector2 Facing => _facingRoot != null ? (Vector2)_facingRoot.up : Vector2.up;
        public FineTerrainWorld World => _world;

        /// <summary>Body / Mind / Soul sheet. Mechanics / HeavyLifting drive Stage-1 scanner setup.</summary>
        public WorkerStats Stats =>
            _assignedWorker != null ? _assignedWorker.Stats : (_stats ??= new WorkerStats());

        /// <summary>Stage A fixed mapping — person identity backing this body.</summary>
        public WorkerRuntime AssignedWorker => _assignedWorker;

        [SerializeField] WorkerStats _stats = new WorkerStats();
        WorkerRuntime _assignedWorker;

        /// <summary>
        /// Bind this role body to a person. Stats become the Worker's shared sheet (same reference).
        /// </summary>
        public void BindWorker(WorkerRuntime worker)
        {
            if (worker == null || worker.Stats == null) return;
            _assignedWorker = worker;
            _stats = worker.Stats;
            _stats.ClampAll();
            WorkerJobDemand.EnsureStaminaPrimed(worker);
        }

        /// <summary>Clear person binding (Stage C: worker left this body).</summary>
        public void ClearWorker()
        {
            _assignedWorker = null;
            // Fresh private sheet — must not keep referencing another person's Stats.
            _stats = WorkerStats.CreateBaseline();
        }

        /// <summary>
        /// Stage C: stop personal Prospecting actions before another worker takes the body.
        /// Does not delete scan history / findings.
        /// </summary>
        public void YieldForReassignment()
        {
            CancelScannerAssignment(clearEquipment: false);
            if (WorkMode == ProspectorWorkMode.Investigate)
                SetWorkMode(ProspectorWorkMode.Manual);
            else
            {
                _hasWorkGoal = false;
                _nav?.Invalidate();
            }
            _scanning = false;
            SetRadar(false);
        }

        public int ActiveRows => Distance switch
        {
            ScanDistance.Short => 1,
            ScanDistance.Medium => 2,
            _ => 3,
        };

        // Long range — keep in sync with heavy scanner Spec (√2 of prior 52.5 ≈ 2× area)
        public float MaxRangeCells => 52.5f * 1.41421356f;
        public float ScanRangeCells => MaxRangeCells * (ActiveRows / (float)RowCount);

        public static ProspectorPerson Spawn(Transform parent, FineTerrainWorld world,
            Vector2 start, ScanViewOverlay scanView)
        {
            var go = new GameObject("Prospector");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = start;

            var facing = new GameObject("Facing");
            facing.transform.SetParent(go.transform, false);
            CrewVisualKit.AttachProspector(facing.transform, out _);

            var cone = new GameObject("RadarCone");
            cone.transform.SetParent(facing.transform, false);

            // Tiny unlit fill only — forward light is the helmet cone
            var light = go.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(light,
                new Color(0.7f, 0.88f, 1f),
                intensity: 0.04f,
                outer: 0.22f,
                inner: 0.02f,
                shadows: false,
                falloff: 0.9f);

            var p = go.AddComponent<ProspectorPerson>();
            p._world = world;
            p._scanView = scanView;
            p._nav = new ExcavatedPathfinder(world);
            p._facingRoot = facing.transform;
            p._coneRoot = cone.transform;
            p._lamp = light;
            p.BuildConeLines();
            p.RebuildConeMesh();
            return p;
        }

        public void BindExcavator(FreeWorkerController excavator) => _excavator = excavator;
        public void BindRefiner(RefinerPerson refiner) => _refiner = refiner;

        public FreeWorkerController Excavator => _excavator;
        public RefinerPerson Refiner => _refiner;
        public float BodyRadius => _radius;
        public float InvestigationMoveSpeed => _autoMoveSpeed * 1.05f;

        public ProspectorInvestigationLoop Investigation =>
            _investigation ??= new ProspectorInvestigationLoop(this);

        public string InvestigationDebugLine
        {
            get
            {
                if (WorkMode != ProspectorWorkMode.Investigate) return "";
                var loop = Investigation;
                string work = loop.PlayerWorkLabel;
                string why = loop.PlayerReasonLabel;
                if (loop.FocusAnomalyId > 0)
                {
                    return string.IsNullOrEmpty(why)
                        ? $"#{loop.FocusAnomalyId:00} · {work}"
                        : $"#{loop.FocusAnomalyId:00} · {work} · {why}";
                }
                return work;
            }
        }

        public void SetInvestigationDesk(Vector2 deskWorld) => Investigation.SetDeskPosition(deskWorld);

        public void NotifyInvestigationFinding() => InvestigationFinding?.Invoke();

        public event System.Action InvestigationFinding;

        /// <summary>Used by investigation loop — excavated-path follow.</summary>
        public bool InvestigationNavFollow(Vector2 goal, float speed)
        {
            _nav ??= _world != null ? new ExcavatedPathfinder(_world) : null;
            return NavFollow(goal, speed);
        }

        public Vector2 FindInvestigationStandNear(Vector2 target, bool preferBehind)
        {
            if (_world == null) return target;
            float cs = _world.CellSize;
            Vector2 away = (Position - target);
            if (away.sqrMagnitude < 0.0001f) away = Vector2.left;
            away.Normalize();
            if (!preferBehind) away = -away;

            float[] dist = { 0.55f, 0.85f, 1.15f, 0.4f };
            float[] side = { 0f, 0.45f, -0.45f, 0.75f, -0.75f };
            Vector2 perp = new(-away.y, away.x);
            Vector2 best = target + away * 0.7f;
            float bestScore = float.MaxValue;

            for (int di = 0; di < dist.Length; di++)
            for (int si = 0; si < side.Length; si++)
            {
                Vector2 cand = target + away * dist[di] + perp * (side[si] * cs * 3f);
                var cell = _world.WorldToCell(cand);
                if (!_world.InBounds(cell.x, cell.y) || !_world.IsTunnelOpen(cell.x, cell.y)) continue;
                Vector2 center = _world.CellCenter(cell.x, cell.y);
                if (_world.CircleHitsSolid(center, _radius)) continue;
                float score = Vector2.Distance(center, target) + Vector2.Distance(center, Position) * 0.2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = center;
                }
            }
            return best;
        }

        public void SetWorkMode(ProspectorWorkMode mode)
        {
            if (_assignedScanner != null && mode != ProspectorWorkMode.Manual)
            {
                DigHoodLog.Push("SCANNER | Work mode blocked — setup assignment active");
                return;
            }
            WorkMode = mode;
            _hasWorkGoal = false;
            _focusX = _focusY = -1;
            _workRetargetT = 0f;
            _workStudyT = 0f;
            _nav?.Invalidate();
            if (mode != ProspectorWorkMode.Investigate)
                Investigation.Reset();
        }

        /// <summary>
        /// Assign travel + setup for a PACKED scanner. Prospector walks there (no teleport).
        /// </summary>
        public void AssignScannerSetup(ProspectorScannerEquipment scanner)
        {
            if (scanner == null || _world == null) return;
            if (scanner.State != ProspectorScannerState.Packed)
            {
                DigHoodLog.Push($"SCANNER | Cannot assign — state {scanner.StateLabel}");
                return;
            }

            CancelScannerAssignment(clearEquipment: false);
            _assignedScanner = scanner;
            _scannerTravelActive = true;
            _scannerSetupActive = false;
            _debugSetupSpeedMul = 1f;
            SetWorkMode(ProspectorWorkMode.Manual);
            SetRadar(false);
            _nav ??= new ExcavatedPathfinder(_world);
            _nav.Invalidate();
            DigHoodLog.Push(
                $"SCANNER | ASSIGN SETUP | travel to {scanner.Position.x:0.00},{scanner.Position.y:0.00}");
        }

        public void CancelScannerAssignment(bool clearEquipment = true)
        {
            if (_scannerSetupActive && _assignedScanner != null &&
                _assignedScanner.State == ProspectorScannerState.SettingUp)
            {
                // Leave equipment SettingUp only if we were mid-setup — Stage 1 cancels back to Packed
                _assignedScanner.BeginPackUp();
            }
            _assignedScanner = null;
            _scannerTravelActive = false;
            _scannerSetupActive = false;
            _debugSetupSpeedMul = 1f;
            if (clearEquipment) { /* reserved for future redeploy ownership */ }
        }

        /// <summary>Local setup-time multiplier (does not change global game clock).</summary>
        public void SetDebugSetupSpeedMul(float mul) =>
            _debugSetupSpeedMul = Mathf.Clamp(mul, 0.25f, 60f);

        /// <summary>Local scan-time multiplier (does not change global game clock).</summary>
        public void SetDebugScanSpeedMul(float mul) =>
            _debugScanSpeedMul = Mathf.Clamp(mul, 0.25f, 120f);

        public float DebugScanSpeedMul => _debugScanSpeedMul;

        public void DebugForceFinishScannerSetup()
        {
            if (_assignedScanner == null) return;
            if (_assignedScanner.State == ProspectorScannerState.Packed && _scannerTravelActive)
            {
                // Snap only for debug travel skip — normal path never teleports
                TeleportTo(_assignedScanner.Position);
                BeginAssignedScannerSetup();
            }
            _assignedScanner.DebugForceCompleteSetup();
            if (_assignedScanner.State == ProspectorScannerState.Ready)
            {
                _scannerTravelActive = false;
                _scannerSetupActive = false;
                _assignedScanner = null;
            }
        }

        void BeginAssignedScannerSetup()
        {
            if (_assignedScanner == null) return;
            float hours = ProspectorScannerSetup.SetupDurationHours(Stats);
            string by = AssignedWorker != null ? AssignedWorker.DisplayName : "Prospector";
            _assignedScanner.BeginSetup(Stats, by, hours);
            _scannerTravelActive = false;
            _scannerSetupActive = true;
            Face(_assignedScanner.Facing);
        }

        /// <summary>
        /// Advance scanner setup from the authoritative game-hour clock.
        /// Call with hours advanced this frame (Time.deltaTime / SecondsPerGameHour).
        /// Stage-2 scan progression is ticked by the runner on the field scanner.
        /// </summary>
        public void TickScannerGameTime(float gameHoursDelta)
        {
            if (!_scannerSetupActive || _assignedScanner == null) return;
            if (_assignedScanner.State != ProspectorScannerState.SettingUp) return;
            float dt = gameHoursDelta * _debugSetupSpeedMul;
            if (_assignedScanner.TickSetupGameHours(dt))
            {
                _scannerSetupActive = false;
                _scannerTravelActive = false;
                // Kit stays in world as field scanner; release travel lock so radar Space works
                _assignedScanner = null;
            }
        }

        public void ResetTo(Vector2 pos)
        {
            transform.localPosition = pos;
            if (_facingRoot != null) _facingRoot.localRotation = Quaternion.identity;
            _scanCooldown = 0f;
            _scanning = false;
            _pending.Clear();
            _hasWorkGoal = false;
            _focusX = _focusY = -1;
            CancelScannerAssignment(clearEquipment: false);
            _nav?.Invalidate();
            if (_sweepCore != null) _sweepCore.gameObject.SetActive(false);
            if (_sweepGlow != null) _sweepGlow.gameObject.SetActive(false);
            if (_sweepFillMr != null) _sweepFillMr.enabled = false;
            RebuildConeMesh();
        }

        /// <summary>Move only — keep WorkMode / investigation / scanner assignment across sleep.</summary>
        public void SoftTeleport(Vector2 pos)
        {
            transform.localPosition = pos;
            _nav?.Invalidate();
            _hasWorkGoal = false;
            _workRetargetT = 0f;
        }

        public bool CanReachPoint(Vector2 worldPos)
        {
            if (_world == null) return false;
            _nav ??= new ExcavatedPathfinder(_world);
            return _nav.TryFindPath(Position, worldPos, null);
        }

        public void TeleportTo(Vector2 pos) => SoftTeleport(pos);

        public void SetCrewVisible(bool on)
        {
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
                r.enabled = on;
            foreach (var l in GetComponentsInChildren<Light2D>(true))
                l.enabled = on;
            if (_coneRoot != null && !on)
                _coneRoot.gameObject.SetActive(false);
        }

        public void SetDistance(ScanDistance d)
        {
            if (_scanning) return;
            Distance = d;
            RebuildConeMesh();
        }

        public void SetWidth(ScanWidth w)
        {
            if (_scanning) return;
            Width = w;
            RebuildConeMesh();
        }

        public void SetRadar(bool on)
        {
            RadarOn = on;
            ApplyHudVisibility();
        }

        public void ToggleRadar() => SetRadar(!RadarOn);

        public void SetHudVisible(bool visible)
        {
            _hudVisible = visible;
            ApplyHudVisibility();
        }

        void ApplyHudVisibility()
        {
            if (_coneRoot != null)
                _coneRoot.gameObject.SetActive(_hudVisible && RadarOn);
        }

        public event System.Action ScanStarted;
        public event System.Action GoldHintFound;
        public event System.Action BedrockHintFound;
        public event System.Action GasHintFound;
        public event System.Action SurveyWhisper;
        public event System.Action AssistNote;

        public void Tick(Vector2 wasd, bool scanPulse)
        {
            if (_world == null) return;
            if (_scanCooldown > 0f) _scanCooldown -= Time.deltaTime;
            _pulse += Time.deltaTime;

            // F0.5b vacancy: equipment stays; no setup / investigation / new scans
            // F1: vacant host must not receive WASD / operator input
            if (_assignedWorker == null)
            {
                if (_hudVisible && RadarOn)
                    RebuildConeMesh();
                return;
            }

            // Scanner assignment: travel then wait while Setting Up (game-time driven elsewhere)
            if (_assignedScanner != null && (_scannerTravelActive || _scannerSetupActive))
            {
                TickScannerAssignment(wasd);
                if (_hudVisible && RadarOn)
                    RebuildConeMesh();
                return;
            }

            bool playerDriving = !_scanning && wasd.sqrMagnitude > 0.01f;
            if (playerDriving)
            {
                Vector2 dir = wasd.normalized;
                Face(dir);
                Step(dir, _moveSpeed);
                _nav?.Invalidate();
            }

            if (_hudVisible && RadarOn)
                RebuildConeMesh();

            if (_scanning)
                TickScanSweep();
            else if (scanPulse && RadarOn && _scanCooldown <= 0f)
                BeginScan();
            else if (!playerDriving && WorkMode != ProspectorWorkMode.Manual)
                TickWorkMode();
        }

        void TickScannerAssignment(Vector2 wasd)
        {
            // Allow player to walk away only if not mid-setup; otherwise hold position and face kit
            if (_scannerSetupActive)
            {
                if (_assignedScanner != null)
                    Face((_assignedScanner.Position - Position).normalized);
                return;
            }

            if (!_scannerTravelActive || _assignedScanner == null) return;

            // WASD can still nudge, but assignment keeps pulling toward scanner
            if (wasd.sqrMagnitude > 0.01f)
            {
                Face(wasd.normalized);
                Step(wasd.normalized, _moveSpeed * 0.85f);
            }

            _nav ??= new ExcavatedPathfinder(_world);
            bool arrived = NavFollow(_assignedScanner.Position, _autoMoveSpeed * 1.1f);
            if (!arrived)
            {
                float moved = Vector2.Distance(Position, _lastPos);
                _lastPos = Position;
                if (moved < 0.0015f) _stuckT += Time.deltaTime;
                else _stuckT = 0f;
                return;
            }

            _stuckT = 0f;
            BeginAssignedScannerSetup();
        }

        void TickWorkMode()
        {
            _workRetargetT -= Time.deltaTime;
            _workStudyT -= Time.deltaTime;
            _workBanterT -= Time.deltaTime;

            switch (WorkMode)
            {
                case ProspectorWorkMode.SurveyNearby:
                    TickSurveyNearby();
                    break;
                case ProspectorWorkMode.AssistExcavator:
                    TickAssistExcavator();
                    break;
                case ProspectorWorkMode.Investigate:
                    Investigation.TickMovement();
                    break;
            }
        }

        /// <summary>Game-hour tick for investigation dwells + auto-enter after scan freeze.</summary>
        public void TickInvestigationGameTime(float gameHoursDelta, float absoluteGameHours)
        {
            TryAutoStartInvestigation();
            if (WorkMode == ProspectorWorkMode.Investigate)
                Investigation.TickGameHours(gameHoursDelta, absoluteGameHours);
        }

        void TryAutoStartInvestigation()
        {
            if (HasScannerAssignment) return;
            if (WorkMode != ProspectorWorkMode.Manual && WorkMode != ProspectorWorkMode.Investigate)
                return;
            if (!Investigation.HasInvestigationWork()) return;
            if (WorkMode == ProspectorWorkMode.Investigate) return;

            // After heavy scan freezes with unresolved anomalies — continuous investigator
            var scan = ScanHistory?.DisplayScan;
            if (scan != null)
                SetInvestigationDesk(scan.ScannerPosition);
            SetWorkMode(ProspectorWorkMode.Investigate);
            DigHoodLog.Push("PROSPECTOR | Investigation loop started (closest anomalies first)");
            Debug.Log("[PROSPECTOR] Auto-entered Investigate mode after scan freeze");
        }

        void TickSurveyNearby()
        {
            if (_nav == null && _world != null)
                _nav = new ExcavatedPathfinder(_world);

            // Hunt gold in rock near the excavator (not random wandering alone)
            if (!_hasWorkGoal || _workRetargetT <= 0f)
                PickSurveyGoalNearExcavator();

            if (!_hasWorkGoal)
            {
                // Path to excavator while hunting a wall sample
                if (_excavator != null)
                    NavFollow(_excavator.Position, _autoMoveSpeed);
                return;
            }

            bool arrived = NavFollow(_workGoal, _autoMoveSpeed * 1.15f);
            if (!arrived)
            {
                float moved = Vector2.Distance(Position, _lastPos);
                _lastPos = Position;
                if (moved < 0.0015f) _stuckT += Time.deltaTime;
                else _stuckT = 0f;
                if (_stuckT > 2.5f)
                {
                    _hasWorkGoal = false;
                    _workRetargetT = 0f;
                    _stuckT = 0f;
                    _nav?.Invalidate();
                }
                return;
            }
            _lastPos = Position;
            if (_focusX < 0)
            {
                _hasWorkGoal = false;
                return;
            }

            Face((_world.CellCenter(_focusX, _focusY) - Position).normalized);

            if (_workStudyT > 0f) return;
            _workStudyT = Random.Range(0.7f, 1.25f);
            PerformSurveyStudy(_focusX, _focusY);
            _hasWorkGoal = false;
            _workRetargetT = Random.Range(0.15f, 0.45f);
            _nav?.Invalidate();
        }

        void PickSurveyGoalNearExcavator()
        {
            _hasWorkGoal = false;
            _focusX = _focusY = -1;
            _workRetargetT = 0.8f;

            Vector2 hub = _excavator != null ? _excavator.Position : Position;
            var hubCell = _world.WorldToCell(hub);
            float cs = _world.CellSize;

            int bestX = -1, bestY = -1;
            float bestScore = float.MaxValue;
            Vector2 bestStand = hub;

            // Sweep a ring of solid cells around the excavator — prioritize gold-adjacent walls
            int radius = 3 + (_surveyRing % 6); // 3..8 cells out, rotating
            _surveyRing++;

            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int x = hubCell.x + dx;
                int y = hubCell.y + dy;
                if (!_world.InBounds(x, y)) continue;
                bool sealedGas = _world.IsGas(x, y) && !_world.IsTunnelOpen(x, y);
                if (_world.IsTunnelOpen(x, y)) continue;
                if (_world.IsExcavated(x, y) && !sealedGas) continue;

                var t = _world.Get(x, y);
                if (t.IsUndamageableBorder) continue;
                if (!TryFindStandBeside(x, y, out Vector2 standPos)) continue;

                // Must be reachable-ish from open tunnel near hub
                float toHub = Vector2.Distance(standPos, hub);
                if (toHub > 11f * cs) continue;

                float score = toHub;
                score += Vector2.Distance(standPos, Position) * 0.35f;

                // Strong bias: diamonds, gold, then sealed gas hazards
                if (t.DiamondCount > 0)
                    score *= 0.18f - t.DiamondCount * 0.03f;
                else if (t.GoldCount > 0)
                    score *= 0.25f - t.GoldCount * 0.04f;
                else if (sealedGas)
                    score *= 0.4f;
                else
                {
                    int nearGold = CountNeighborGold(x, y);
                    if (nearGold > 0) score *= 0.55f;
                    else score += 2.5f + Random.Range(0f, 3f);
                }

                // Skip cells we just studied if we can
                if (x == _focusX && y == _focusY) score += 4f;

                score += Random.Range(0f, 1.2f);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestX = x;
                    bestY = y;
                    bestStand = standPos;
                }
            }

            // Fallback: any wall / sealed gas next to excavator within 5 cells
            if (bestX < 0)
            {
                for (int dy = -5; dy <= 5 && bestX < 0; dy++)
                for (int dx = -5; dx <= 5; dx++)
                {
                    int x = hubCell.x + dx, y = hubCell.y + dy;
                    if (!_world.InBounds(x, y)) continue;
                    bool sealedGas = _world.IsGas(x, y) && !_world.IsTunnelOpen(x, y);
                    if (_world.IsTunnelOpen(x, y)) continue;
                    if (_world.IsExcavated(x, y) && !sealedGas) continue;
                    if (_world.Get(x, y).IsUndamageableBorder) continue;
                    if (!TryFindStandBeside(x, y, out Vector2 standPos)) continue;
                    bestX = x;
                    bestY = y;
                    bestStand = standPos;
                    break;
                }
            }

            if (bestX < 0) return;

            _focusX = bestX;
            _focusY = bestY;
            _workGoal = bestStand;
            _hasWorkGoal = true;
            _workRetargetT = 6f;
            _stuckT = 0f;
            _lastPos = Position;
            _nav?.Invalidate();
        }

        int CountNeighborGold(int cx, int cy)
        {
            int n = 0;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int x = cx + dx, y = cy + dy;
                if (!_world.InBounds(x, y)) continue;
                if (_world.Get(x, y).IsPreciousOre) n++;
            }
            return n;
        }

        bool TryFindStandBeside(int sx, int sy, out Vector2 stand)
        {
            stand = default;
            int[] ox = { 1, -1, 0, 0, 1, 1, -1, -1 };
            int[] oy = { 0, 0, 1, -1, 1, -1, 1, -1 };
            float best = float.MaxValue;
            bool found = false;
            Vector2 bestPos = default;
            Vector2 prefer = _excavator != null ? _excavator.Position : Position;
            for (int i = 0; i < 8; i++)
            {
                int x = sx + ox[i], y = sy + oy[i];
                if (!_world.InBounds(x, y) || !_world.IsTunnelOpen(x, y)) continue;
                Vector2 c = _world.CellCenter(x, y);
                if (_world.CircleHitsSolid(c, _radius * 0.9f)) continue;
                float d = Vector2.Distance(prefer, c) + Vector2.Distance(Position, c) * 0.25f;
                if (d < best)
                {
                    best = d;
                    bestPos = c;
                    found = true;
                }
            }
            stand = bestPos;
            return found;
        }

        void PerformSurveyStudy(int x, int y)
        {
            if (_world == null || !_world.InBounds(x, y)) return;
            bool sealedGas = _world.IsGas(x, y) && !_world.IsTunnelOpen(x, y);
            var terrain = _world.Get(x, y);
            if (terrain.Phase == TerrainPhase.Excavated && !sealedGas) return;

            // Close-range geology — better than radar, still never certain
            const float reliability = 0.62f;
            bool trueGold = !sealedGas && (terrain.GoldCount > 0 || terrain.DiamondCount > 0);
            bool nearGold = !trueGold && !sealedGas && CountNeighborGold(x, y) > 0;
            bool trueBed = !sealedGas && terrain.BedrockCount >= 2;
            bool trueGas = sealedGas;

            bool reportGold = false;
            bool reportBed = false;
            bool reportGas = false;

            if (trueGas)
            {
                reportGas = Random.value < 0.72f;
            }
            else if (Random.value < reliability)
            {
                if (trueGold)
                {
                    int grade = Mathf.Max(terrain.GoldCount, terrain.DiamondCount);
                    reportGold = Random.value < (0.55f + grade * 0.12f);
                }
                else if (nearGold)
                    reportGold = Random.value < 0.28f;
            }

            if (!reportGold && !trueGold && !trueGas && Random.value < 0.1f)
                reportGold = true;

            if (trueGold && Random.value < 0.28f)
                reportGold = false;

            // Bedrock reads are more precise up close
            if (!reportGold && !reportGas && trueBed && Random.value < 0.78f)
                reportBed = true;
            if (!reportGold && !reportGas && !reportBed && !trueBed && Random.value < 0.04f)
                reportBed = true;

            if (!reportGold && !reportBed && !reportGas)
            {
                if (_workBanterT <= 0f)
                {
                    SurveyWhisper?.Invoke();
                    _workBanterT = Random.Range(7f, 12f);
                }
                return;
            }

            if (reportGas) GasHintFound?.Invoke();
            else if (reportGold) GoldHintFound?.Invoke();
            else BedrockHintFound?.Invoke();
            SurveyWhisper?.Invoke();
            _workBanterT = Random.Range(6f, 11f);
        }

        void TickAssistExcavator()
        {
            if (_excavator == null) return;
            if (_nav == null && _world != null)
                _nav = new ExcavatedPathfinder(_world);

            Vector2 digger = _excavator.Position;
            Vector2 fwd = _excavator.Facing.normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector2.up;

            // Prefer standing in open tunnel behind the bit (pathfind there)
            Vector2 behind = FindAssistStand(digger, fwd);
            bool arrived = NavFollow(behind, _autoMoveSpeed * 1.2f);

            if (arrived)
                Face(fwd);
            else
                return; // still navigating tunnels — don't study from a pocket

            if (_workStudyT > 0f) return;
            _workStudyT = Random.Range(0.55f, 1.05f);
            StudyDigFace(digger, fwd);
        }

        Vector2 FindAssistStand(Vector2 digger, Vector2 fwd)
        {
            float cs = _world.CellSize;
            // Try several offsets behind excavator; pick first excavated free spot
            float[] back = { 2.4f, 1.8f, 1.2f, 0.8f, 3.2f };
            float[] side = { 0f, 0.6f, -0.6f, 1.1f, -1.1f };
            Vector2 perp = new(-fwd.y, fwd.x);
            Vector2 best = digger - fwd * (_excavator.Radius * 2f);
            float bestScore = float.MaxValue;

            for (int bi = 0; bi < back.Length; bi++)
            for (int si = 0; si < side.Length; si++)
            {
                Vector2 cand = digger - fwd * (_excavator.Radius * back[bi] + 0.1f) + perp * (side[si] * cs * 4f);
                var cell = _world.WorldToCell(cand);
                if (!_world.InBounds(cell.x, cell.y) || !_world.IsTunnelOpen(cell.x, cell.y)) continue;
                Vector2 center = _world.CellCenter(cell.x, cell.y);
                if (_world.CircleHitsSolid(center, _radius)) continue;
                float score = Vector2.Distance(center, digger) + Vector2.Distance(center, Position) * 0.15f;
                // Prefer true behind
                float along = Vector2.Dot(center - digger, -fwd);
                if (along < 0f) score += 2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = center;
                }
            }
            return best;
        }

        bool NavFollow(Vector2 goal, float speed)
        {
            if (_nav == null) return (goal - Position).sqrMagnitude < 0.04f;
            float spd = speed * LoosePile.SpeedMulAt(Position, _radius);
            return _nav.Follow(Position, goal, spd, _radius, Face, TryNavStep);
        }

        bool TryNavStep(Vector2 dir, float step)
        {
            if (dir.sqrMagnitude < 0.0001f) return false;
            dir.Normalize();
            Vector2 pos = Position;
            Vector2 next = pos + dir * step;
            if (!_world.CircleHitsSolid(next, _radius))
            {
                transform.localPosition = next;
                return true;
            }
            return false;
        }

        void StudyDigFace(Vector2 digger, Vector2 fwd)
        {
            float cs = _world.CellSize;
            Vector2 tip = digger + fwd * (_excavator.Radius + tipReachAssist);
            int marked = 0;

            for (int i = 0; i < 10; i++)
            {
                float along = cs * (0.4f + i * 0.55f);
                float side = (i % 2 == 0 ? 1f : -1f) * cs * (0.15f + (i / 2) * 0.2f);
                Vector2 perp = new(-fwd.y, fwd.x);
                Vector2 p = tip + fwd * along + perp * side;
                var cell = _world.WorldToCell(p);
                if (!_world.InBounds(cell.x, cell.y)) continue;
                var t = _world.Get(cell.x, cell.y);
                if (t.Phase == TerrainPhase.Excavated || t.IsUndamageableBorder) continue;

                // Reading rock is imperfect — sometimes mark nothing useful
                if (Random.value > 0.62f) continue;

                _world.MarkRockStudy(cell.x, cell.y, digBonus: (byte)Random.Range(2, 5));

                // Occasional soft chip when a seam is correctly felt
                if (t.BedrockCount < 3 && Random.value < 0.18f)
                    _world.Damage(cell.x, cell.y);

                marked++;
                if (marked >= 3) break;
            }

            if (marked > 0 && _workBanterT <= 0f)
            {
                AssistNote?.Invoke();
                _workBanterT = Random.Range(9f, 16f);
            }
        }

        const float tipReachAssist = 0.35f;

        public void FaceToward(Vector2 terrainPoint)
        {
            if (_scanning) return;
            Vector2 dir = terrainPoint - Position;
            if (dir.sqrMagnitude < 0.0001f || _facingRoot == null) return;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            _facingRoot.localRotation = Quaternion.Euler(0f, 0f, ang);
            RebuildConeMesh();
        }

        void Face(Vector2 dir)
        {
            if (_facingRoot == null || dir.sqrMagnitude < 0.0001f) return;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            _facingRoot.localRotation = Quaternion.RotateTowards(
                _facingRoot.localRotation,
                Quaternion.Euler(0f, 0f, ang),
                360f * Time.deltaTime);
        }

        void Step(Vector2 dir) => Step(dir, _moveSpeed);

        void Step(Vector2 dir, float speed)
        {
            float step = speed * LoosePile.SpeedMulAt(Position, _radius) * Time.deltaTime;
            Vector2 pos = Position;
            Vector2 next = pos + dir * step;
            if (!_world.CircleHitsSolid(next, _radius))
            {
                transform.localPosition = next;
                return;
            }
            Vector2 nx = pos + new Vector2(dir.x, 0f) * step;
            if (!_world.CircleHitsSolid(nx, _radius))
            {
                transform.localPosition = nx;
                return;
            }
            Vector2 ny = pos + new Vector2(0f, dir.y) * step;
            if (!_world.CircleHitsSolid(ny, _radius))
                transform.localPosition = ny;
        }

        bool _saidGoldThisScan;
        bool _saidBedThisScan;
        bool _saidGasThisScan;

        void BeginScan()
        {
            // Legacy handheld radar sweep + round Scan View zones retired.
            // Heavy Scanner (Y place → C plan → Enter) is the only scan path.
        }

        void CollectPendingHits()
        {
            _pending.Clear();
            float range = _scanRangeWorld;
            float colWidth = (FullHalfAngle * 2f) / ColCount;
            int colStart = Width == ScanWidth.Narrow ? 1 : 0;
            int colEnd = Width == ScanWidth.Narrow ? 1 : ColCount - 1;

            int samplesPerCol = 4;
            int distSteps = Mathf.Max(ActiveRows * 12, Mathf.CeilToInt(ScanRangeCells));

            for (int col = colStart; col <= colEnd; col++)
            {
                float a0 = -FullHalfAngle + col * colWidth;
                float a1 = a0 + colWidth;
                for (int s = 0; s < samplesPerCol; s++)
                {
                    float ta = samplesPerCol == 1 ? 0.5f : s / (float)(samplesPerCol - 1);
                    float ang = Mathf.Lerp(a0 + colWidth * 0.18f, a1 - colWidth * 0.18f, ta);
                    Vector2 dir = Rotate(_scanFwd, ang);

                    for (int di = 1; di <= distSteps; di++)
                    {
                        float u = di / (float)distSteps;
                        Vector2 hit = _scanOrigin + dir * (u * range);
                        var cell = _world.WorldToCell(hit);
                        if (!_world.InBounds(cell.x, cell.y)) break;
                        // Skip open tunnel; sealed gas is excavated but still scannable
                        if (_world.IsTunnelOpen(cell.x, cell.y)) continue;

                        bool trueGas = _world.IsGas(cell.x, cell.y);
                        if (_world.IsExcavated(cell.x, cell.y) && !trueGas) continue;

                        var terrain = _world.Get(cell.x, cell.y);
                        bool trueGold = !trueGas && terrain.IsPreciousOre;
                        bool trueBed = !trueGas && terrain.BedrockCount >= 2;

                        float rowPos = u * ActiveRows;
                        float frac = rowPos - Mathf.Floor(rowPos);
                        if (frac < 0.28f || frac > 0.72f) continue;

                        bool reportGold = false;
                        bool reportBed = false;
                        bool reportGas = false;
                        if (Random.value < _reliability)
                        {
                            int grade = Mathf.Max(terrain.GoldCount, terrain.DiamondCount);
                            reportGold = trueGold && Random.value < (0.18f + grade * 0.06f);
                            // Precise bedrock: high true-positive, low noise
                            reportBed = trueBed && Random.value < (0.86f + (terrain.BedrockCount >= 3 ? 0.08f : 0f));
                            reportGas = trueGas && Random.value < 0.78f;
                        }
                        float falseRate = 1f - _reliability;
                        if (!reportGold && !trueGas && Random.value < falseRate * 0.03f) reportGold = true;
                        if (!reportBed && !trueGas && Random.value < falseRate * 0.05f) reportBed = true;
                        if (!reportGas && Random.value < falseRate * 0.04f) reportGas = true;

                        float strength = Mathf.Lerp(0.72f, 1f, _reliability);
                        if (reportGas)
                            _pending.Add(new PendingHit { Dist01 = u, X = cell.x, Y = cell.y, Kind = ScanHintKind.Gas, Strength = strength });
                        if (reportGold)
                            _pending.Add(new PendingHit { Dist01 = u, X = cell.x, Y = cell.y, Kind = ScanHintKind.Gold, Strength = strength });
                        if (reportBed)
                            _pending.Add(new PendingHit { Dist01 = u, X = cell.x, Y = cell.y, Kind = ScanHintKind.Bedrock, Strength = strength });
                    }
                }
            }

            ThinPending();
        }

        void ThinPending()
        {
            if (_pending.Count == 0) return;
            const int minSepGold = 22;
            const int minSepBed = 10;
            const int minSepGas = 14;
            var kept = new List<PendingHit>(10);

            void Take(ScanHintKind kind, int max, int minSep)
            {
                for (int i = 0; i < _pending.Count && kept.Count < 10; i++)
                {
                    var h = _pending[i];
                    if (h.Kind != kind) continue;
                    int same = 0;
                    bool near = false;
                    for (int k = 0; k < kept.Count; k++)
                    {
                        if (kept[k].Kind != h.Kind) continue;
                        same++;
                        if (Mathf.Abs(kept[k].X - h.X) + Mathf.Abs(kept[k].Y - h.Y) < minSep)
                        {
                            near = true;
                            break;
                        }
                    }
                    if (near || same >= max) continue;
                    kept.Add(h);
                }
            }

            Take(ScanHintKind.Gold, max: 1, minSep: minSepGold);
            Take(ScanHintKind.Gas, max: 2, minSep: minSepGas);
            Take(ScanHintKind.Bedrock, max: 4, minSep: minSepBed);
            _pending.Clear();
            _pending.AddRange(kept);
            _pending.Sort((a, b) => a.Dist01.CompareTo(b.Dist01));
        }

        void EmitPendingHit(PendingHit h)
        {
            switch (h.Kind)
            {
                case ScanHintKind.Gold:
                    _scanView.AddHint(h.X, h.Y, gold: h.Strength, bedrock: 0f, gas: 0f, bleed: 3);
                    if (!_saidGoldThisScan)
                    {
                        _saidGoldThisScan = true;
                        GoldHintFound?.Invoke();
                    }
                    break;
                case ScanHintKind.Gas:
                    _scanView.AddHint(h.X, h.Y, gold: 0f, bedrock: 0f, gas: h.Strength, bleed: 3);
                    if (!_saidGasThisScan)
                    {
                        _saidGasThisScan = true;
                        GasHintFound?.Invoke();
                    }
                    break;
                default:
                    _scanView.AddHint(h.X, h.Y, gold: 0f, bedrock: h.Strength, gas: 0f, bleed: 3);
                    if (!_saidBedThisScan)
                    {
                        _saidBedThisScan = true;
                        BedrockHintFound?.Invoke();
                    }
                    break;
            }
        }

        void TickScanSweep()
        {
            // Ease-out so it lingers a bit as it reaches far bands
            _scanT += Time.deltaTime / _scanDuration;
            float u = Mathf.Clamp01(_scanT);
            float sweep = 1f - Mathf.Pow(1f - u, 1.55f);

            UpdateSweepArc(Mathf.Max(0.04f, sweep));

            while (_pending.Count > 0 && _pending[0].Dist01 <= sweep + 0.02f)
            {
                var h = _pending[0];
                _pending.RemoveAt(0);
                EmitPendingHit(h);
            }

            if (u >= 1f)
            {
                for (int i = 0; i < _pending.Count; i++)
                    EmitPendingHit(_pending[i]);
                _pending.Clear();
                _scanning = false;
                if (_sweepCore != null) _sweepCore.gameObject.SetActive(false);
                if (_sweepGlow != null) _sweepGlow.gameObject.SetActive(false);
                if (_sweepFillMr != null) _sweepFillMr.enabled = false;
            }
        }

        void UpdateSweepArc(float sweep01)
        {
            if (_sweepCore == null || _world == null) return;
            float r = _scanRangeWorld * sweep01;
            float half = Width == ScanWidth.Narrow
                ? FullHalfAngle / ColCount
                : FullHalfAngle;
            const int res = 32;
            SetArcPoints(_sweepGlow, r, -half, half, res);
            SetArcPoints(_sweepCore, r, -half, half, res);
            BuildSweepFill(r, -half, half, res);

            float pulse = 0.7f + 0.3f * Mathf.Sin(_pulse * 9f);
            // Cyberpunk leading edge: hairline core + soft bloom
            Color glow = new(0.25f, 0.95f, 1f, 0.08f * pulse);
            Color core = new(0.85f, 1f, 1f, 0.55f * pulse);
            _sweepGlow.startColor = _sweepGlow.endColor = glow;
            _sweepCore.startColor = _sweepCore.endColor = core;
            _sweepGlow.widthMultiplier = 0.055f;
            _sweepCore.widthMultiplier = 0.012f;

            if (_sweepFillMr != null && _sweepFillMr.material != null)
            {
                float fillA = 0.028f + 0.022f * pulse * (1f - sweep01 * 0.4f);
                _sweepFillMr.material.color = new Color(0.2f, 0.95f, 1f, fillA);
            }
        }

        void BuildSweepFill(float r, float a0, float a1, int res)
        {
            if (_sweepFillMesh == null || r < 0.01f) return;
            int vCount = res + 2;
            var verts = new Vector3[vCount];
            var cols = new Color[vCount];
            var tris = new int[res * 3];
            verts[0] = Vector3.zero;
            cols[0] = new Color(0.4f, 0.9f, 1f, 0.12f);
            for (int i = 0; i <= res; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)res);
                verts[i + 1] = (Vector3)(Rotate(Vector2.up, a) * r);
                // Fade toward leading edge
                cols[i + 1] = new Color(0.45f, 0.92f, 1f, 0.02f);
            }
            for (int i = 0; i < res; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }
            _sweepFillMesh.Clear();
            _sweepFillMesh.vertices = verts;
            _sweepFillMesh.colors = cols;
            _sweepFillMesh.triangles = tris;
            var uvs = new Vector2[vCount];
            for (int i = 0; i < vCount; i++) uvs[i] = Vector2.zero;
            _sweepFillMesh.uv = uvs;
            _sweepFillMesh.RecalculateBounds();
        }

        void BuildConeLines()
        {
            _arcCore.Clear();
            _arcGlow.Clear();
            _radialCore.Clear();
            _radialGlow.Clear();

            for (int i = 0; i < RowCount; i++)
            {
                _arcGlow.Add(MakeLr($"ArcGlow{i}", 43));
                _arcCore.Add(MakeLr($"Arc{i}", 45));
            }
            for (int i = 0; i < ColCount + 1; i++)
            {
                _radialGlow.Add(MakeLr($"RadialGlow{i}", 43));
                _radialCore.Add(MakeLr($"Radial{i}", 45));
            }
            _sweepGlow = MakeLr("SweepGlow", 46);
            _sweepCore = MakeLr("Sweep", 47);
            _sweepGlow.gameObject.SetActive(false);
            _sweepCore.gameObject.SetActive(false);

            var fillGo = new GameObject("SweepFill");
            fillGo.transform.SetParent(_coneRoot, false);
            _sweepFillMf = fillGo.AddComponent<MeshFilter>();
            _sweepFillMr = fillGo.AddComponent<MeshRenderer>();
            _sweepFillMesh = new Mesh { name = "ScanSweepFill" };
            _sweepFillMf.sharedMesh = _sweepFillMesh;
            var sh = Shader.Find("Sprites/Default")
                     ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh != null)
            {
                var mat = new Material(sh) { name = "ScanSweepFill_Mat", color = new Color(0.2f, 0.95f, 1f, 0.03f) };
                _sweepFillMr.sharedMaterial = mat;
            }
            _sweepFillMr.sortingOrder = 44;
            _sweepFillMr.enabled = false;
        }

        LineRenderer MakeLr(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_coneRoot, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = false;
            lr.widthMultiplier = 0.012f;
            var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            lr.material = sh != null ? new Material(sh) : null;
            lr.sortingOrder = order;
            lr.numCapVertices = 1;
            lr.numCornerVertices = 1;
            return lr;
        }

        void RebuildConeMesh()
        {
            if (_coneRoot == null || _world == null) return;
            if (_arcCore.Count < RowCount || _radialCore.Count < ColCount + 1) return;

            float fullRange = MaxRangeCells * _world.CellSize;
            float activeRange = fullRange * (ActiveRows / (float)RowCount);
            float half = FullHalfAngle;
            const int arcRes = 36;
            float breath = 0.9f + 0.1f * Mathf.Sin(_pulse * 2.4f);

            // Chosen size = clear outline; other range rings barely perceptible
            Color activeCore = new(0.55f, 1f, 1f, 0.48f * breath);
            Color activeGlow = new(0.15f, 0.9f, 1f, 0.08f * breath);
            Color ghostCore = new(0.35f, 0.75f, 0.9f, 0.008f);
            Color ghostGlow = new(0.2f, 0.55f, 0.75f, 0.003f);

            const float coreW = 0.012f;
            const float glowW = 0.05f;

            for (int row = 0; row < RowCount; row++)
            {
                float r = fullRange * ((row + 1) / (float)RowCount);
                // Only the outer arc of the selected distance — no ghost range rings
                bool on = (row + 1) == ActiveRows;
                SetArcPoints(_arcGlow[row], r, -half, half, arcRes);
                SetArcPoints(_arcCore[row], r, -half, half, arcRes);
                _arcGlow[row].startColor = _arcGlow[row].endColor = on ? activeGlow : ghostGlow;
                _arcCore[row].startColor = _arcCore[row].endColor = on ? activeCore : ghostCore;
                _arcGlow[row].widthMultiplier = on ? glowW : 0.02f;
                _arcCore[row].widthMultiplier = on ? coreW : 0.006f;
                _arcGlow[row].enabled = on;
                _arcCore[row].enabled = on;
            }

            for (int i = 0; i < ColCount + 1; i++)
            {
                float a = Mathf.Lerp(-half, half, i / (float)ColCount);
                bool centerish = i == 1 || i == 2;
                bool outer = i == 0 || i == ColCount;
                bool lit = Width == ScanWidth.Wide || centerish;
                if (outer && Width == ScanWidth.Narrow) lit = false;

                Color core = lit ? activeCore : ghostCore;
                Color glow = lit ? activeGlow : ghostGlow;
                // Radials stop at chosen range — ghost stubs for unused long range
                float len = lit ? activeRange : activeRange;
                Vector2 d = Rotate(Vector2.up, a);
                SetLine(_radialGlow[i], Vector3.zero, d * len);
                SetLine(_radialCore[i], Vector3.zero, d * len);
                _radialGlow[i].startColor = _radialGlow[i].endColor = glow;
                _radialCore[i].startColor = _radialCore[i].endColor = core;
                _radialGlow[i].widthMultiplier = lit ? glowW * 0.85f : 0.025f;
                _radialCore[i].widthMultiplier = lit ? coreW : 0.007f;
            }
        }

        static void SetArcPoints(LineRenderer lr, float r, float a0, float a1, int res)
        {
            lr.positionCount = res + 1;
            for (int i = 0; i <= res; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)res);
                lr.SetPosition(i, (Vector3)(Rotate(Vector2.up, a) * r));
            }
        }

        static void SetLine(LineRenderer lr, Vector3 a, Vector3 b)
        {
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
        }

        static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            float ca = Mathf.Cos(r), sa = Mathf.Sin(r);
            return new Vector2(v.x * ca - v.y * sa, v.x * sa + v.y * ca);
        }

        void OnValidate()
        {
            _stats?.ClampAll();
        }
    }
}
