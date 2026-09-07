using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    public enum EngineerWorkKind : byte
    {
        Idle = 0,
        Evaluating = 1,
        WalkingToLanternSite = 2,
        InstallingLantern = 3,
        WalkingToSupport = 4,
        BuildingSupport = 5,
        RepairEnRoute = 6,
        Repairing = 7,
        Returning = 8,
        NoWorkNeeded = 9,
        DarkAreaFound = 10,
        SupportNeeded = 11,
    }

    /// <summary>
    /// Field mechanic: repair → tunnel support → sparse lanterns.
    /// No track/road work.
    /// Stage E: light Engineering provider façade (kit/workbench is implicit on this host).
    /// </summary>
    public sealed class EngineerPerson : MonoBehaviour, IWorkProvider
    {
        public const float MoveSpeed = 1.45f;
        public const float BodyRadius = 0.12f;
        public const float RepairSeconds = 2.6f;
        public const float ArriveRadius = 0.38f;
        public float EvaluateIntervalGameHours = 0.35f;
        static int _nextProviderSerial = 1;

        enum State : byte
        {
            IdleAtPost,
            ToExcavator,
            Repairing,
            ReturnToPost,
            Evaluating,
            WalkingToLantern,
            InstallingLantern,
            WalkingToSupport,
            BuildingSupport,
        }

        FineTerrainWorld _world;
        FreeWorkerController _excavator;
        ExcavatedPathfinder _nav;
        MineInfrastructure _infra;
        Vector2 _post;
        State _state = State.IdleAtPost;
        float _repairTimer;
        float _buildHoursLeft;
        float _evaluateCooldown;
        float _repairDispatchSpeedMul = 1f;
        CooperationAssessment _activeRepairCoop = default;
        Func<CooperationAssessment> _coopEval;
        /// <summary>DEV observation hooks — playtest tracker only; no sim effect.</summary>
        public Action<CooperationAssessment> OnRepairDispatched;
        public Action<CooperationAssessment> OnRepairBegun;
        public Action<CooperationWorkConsequence> OnRepairCompleted;
        public Action OnRepairInterrupted;
        Transform _facing;
        bool _announcedDispatch;
        Vector2Int _jobCell;
        Vector2 _jobWorld;
        bool _hasJobTarget;
        bool _jobIsCriticalSupport;
        EngineerWorkKind _workKind = EngineerWorkKind.NoWorkNeeded;
        string _debugStatus = "NO WORK NEEDED";

        string _evalLanternDebug = "";
        string _evalSupportDebug = "";

        public Vector2 Position => transform.localPosition;
        public bool IsRepairing => _state == State.Repairing;
        public bool IsEnRoute => _state == State.ToExcavator;
        public bool IsReturning => _state == State.ReturnToPost;
        public bool IsInfrastructureWork =>
            _state == State.Evaluating
            || _state == State.WalkingToLantern
            || _state == State.InstallingLantern
            || _state == State.WalkingToSupport
            || _state == State.BuildingSupport;

        public EngineerWorkKind WorkKind => _workKind;
        public string WorkLabel => _debugStatus;
        public string DebugStatus => _debugStatus;
        public string EvalLanternDebug => _evalLanternDebug;
        public string EvalSupportDebug => _evalSupportDebug;
        public bool HasDebugTarget => _hasJobTarget
            && (_state == State.WalkingToLantern
                || _state == State.InstallingLantern
                || _state == State.WalkingToSupport
                || _state == State.BuildingSupport
                || _state == State.Evaluating
                || _workKind == EngineerWorkKind.DarkAreaFound
                || _workKind == EngineerWorkKind.SupportNeeded);
        public Vector2 DebugTarget => _jobWorld;
        public Vector2Int DebugTargetCell => _jobCell;

        /// <summary>Body / Mind / Soul sheet. Provisional — unused by live repair formulas yet.</summary>
        public WorkerStats Stats =>
            _assignedWorker != null ? _assignedWorker.Stats : (_stats ??= new WorkerStats());

        public WorkerRuntime AssignedWorker => _assignedWorker;

        // ——— IWorkProvider (Engineering kit façade) ———
        public string ProviderId { get; private set; } = "engineer.0";
        public JobType JobType => DeepCore.FreeMovement.JobType.Engineering;
        public int AssignedWorkerId { get; private set; } = -1;
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

        public void NotifyAssigned(WorkerRuntime worker) =>
            AssignedWorkerId = worker != null ? worker.WorkerId : -1;

        public void NotifyUnassigned() => AssignedWorkerId = -1;

        [SerializeField] WorkerStats _stats = new WorkerStats();
        WorkerRuntime _assignedWorker;

        public void BindWorker(WorkerRuntime worker)
        {
            if (worker == null || worker.Stats == null) return;
            EnsureProviderId();
            _assignedWorker = worker;
            _stats = worker.Stats;
            _stats.ClampAll();
            WorkerJobDemand.EnsureStaminaPrimed(worker);
            NotifyAssigned(worker);
        }

        public void ClearWorker()
        {
            _assignedWorker = null;
            _stats = WorkerStats.CreateBaseline();
            AssignedWorkerId = -1;
        }

        /// <summary>
        /// Cancel personal path only. Repair timer, infra job, lantern/support targets persist.
        /// </summary>
        public void YieldForReassignment()
        {
            _nav?.Invalidate();
            DigHoodLog.Push(
                $"ASSIGN | Engineer yield | {_debugStatus} | repairT={_repairTimer:0.##}");
        }

        void EnsureProviderId()
        {
            if (string.IsNullOrEmpty(ProviderId) || ProviderId == "engineer.0")
                ProviderId = $"engineer.{_nextProviderSerial++}";
        }

        public static EngineerPerson Spawn(Transform parent, FineTerrainWorld world, Vector2 post)
        {
            var go = new GameObject("Engineer");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = post;

            var facing = new GameObject("Facing");
            facing.transform.SetParent(go.transform, false);
            CrewVisualKit.AttachEngineer(facing.transform, out _);

            var e = go.AddComponent<EngineerPerson>();
            e.EnsureProviderId();
            e._facing = facing.transform;
            e.Setup(world, post);
            return e;
        }

        public void Setup(FineTerrainWorld world, Vector2 post)
        {
            _world = world;
            _post = post;
            _nav = new ExcavatedPathfinder(world);
            _state = State.IdleAtPost;
            _repairTimer = 0f;
            _announcedDispatch = false;
            _evaluateCooldown = 0f;
            _workKind = EngineerWorkKind.Idle;
            _debugStatus = "NO WORK NEEDED";
            _hasJobTarget = false;
            transform.localPosition = post;
        }

        public void BindExcavator(FreeWorkerController excavator) => _excavator = excavator;
        public void BindInfrastructure(MineInfrastructure infra) => _infra = infra;
        /// <summary>Optional: Excavator↔Engineer CooperationQuality for repair only.</summary>
        public void BindCooperation(Func<CooperationAssessment> eval) => _coopEval = eval;
        public void SetPost(Vector2 post) => _post = post;

        /// <summary>SpatialGeometry + Mechanics + Focus; fatigue softens — never random failure.</summary>
        public float SupportBuildQuality01()
        {
            var wr = _assignedWorker;
            if (wr?.Stats == null) return 0.75f;
            float spatial = wr.Stats.Get(WorkerStatId.SpatialGeometry) / 20f;
            float mech = wr.Stats.Get(WorkerStatId.Mechanics) / 20f;
            float focus = wr.Stats.Get(WorkerStatId.Focus) / 20f;
            float q = spatial * 0.45f + mech * 0.35f + focus * 0.20f;
            if (wr.State != null)
            {
                if (wr.State.ExhaustionLatched) q *= 0.82f;
                if (wr.State.MentalFatigue > 70f) q *= 0.90f;
            }
            return Mathf.Clamp(q * 1.05f, 0.40f, 1.12f);
        }

        public CooperationAssessment ActiveRepairCoop => _activeRepairCoop;
        public float RepairDispatchSpeedMul => _repairDispatchSpeedMul;

        public void SoftTeleport(Vector2 pos)
        {
            transform.localPosition = pos;
            _nav?.Invalidate();
        }

        public void TeleportTo(Vector2 pos) => SoftTeleport(pos);

        public void ResetToPost()
        {
            AbortInfraJob();
            _state = State.IdleAtPost;
            _repairTimer = 0f;
            _announcedDispatch = false;
            _workKind = EngineerWorkKind.Idle;
            SetStatus("NO WORK NEEDED");
            _hasJobTarget = false;
            transform.localPosition = _post;
            _nav?.Invalidate();
        }

        public void SetCrewVisible(bool on)
        {
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
                r.enabled = on;
            foreach (var l in GetComponentsInChildren<Light2D>(true))
                l.enabled = on;
        }

        public void Tick(float gameHoursDelta, float absoluteGameHours)
        {
            if (_world == null) return;
            _infra?.SetGameHours(absoluteGameHours);

            // F0.5b vacancy: no repair / infra / lantern labour
            if (_assignedWorker == null) return;

            bool needsRepair = _excavator != null && _excavator.IsOverheated;

            if (needsRepair && IsInfrastructureWork)
            {
                AbortInfraJob();
                BeginDispatch();
            }

            switch (_state)
            {
                case State.IdleAtPost:
                    if (needsRepair)
                        BeginDispatch();
                    else
                        TickIdleInfrastructure(gameHoursDelta);
                    break;

                case State.Evaluating:
                    if (needsRepair) { BeginDispatch(); break; }
                    ResolveEvaluation();
                    break;

                case State.WalkingToLantern:
                    SetStatus("MOVING TO LANTERN SITE");
                    if (FollowTo(_jobWorld))
                        BeginInstallLantern();
                    break;

                case State.InstallingLantern:
                    SetStatus("INSTALLING LANTERN");
                    TickInstallLantern(gameHoursDelta);
                    break;

                case State.WalkingToSupport:
                    SetStatus("MOVING TO SUPPORT");
                    if (FollowTo(_jobWorld))
                        BeginBuildSupport();
                    break;

                case State.BuildingSupport:
                    SetStatus("BUILDING SUPPORT");
                    TickBuildSupport(gameHoursDelta);
                    break;

                case State.ToExcavator:
                    SetStatus("REPAIRING");
                    if (!needsRepair)
                    {
                        OnRepairInterrupted?.Invoke();
                        _state = State.ReturnToPost;
                        _nav?.Invalidate();
                        _repairStrandTimer = 0f;
                        break;
                    }
                    if (FollowTo(RepairStandPoint(), MoveSpeed * _repairDispatchSpeedMul))
                    {
                        _repairStrandTimer = 0f;
                        BeginRepair();
                    }
                    else if (_nav != null && _nav.Stranded)
                    {
                        _repairStrandTimer += Time.deltaTime;
                        // Path blocked (debris / jam) — still reach the machine for V1 repair
                        if (_repairStrandTimer >= 2.5f)
                        {
                            SoftTeleport(RepairStandPoint());
                            _nav.Invalidate();
                            _repairStrandTimer = 0f;
                            DigHoodLog.Push("REPAIR | Engineer soft-arrived at overheated excavator (path stranded)");
                            BeginRepair();
                        }
                    }
                    else
                        _repairStrandTimer = 0f;
                    break;

                case State.Repairing:
                    SetStatus("REPAIRING");
                    _repairTimer -= Time.deltaTime;
                    FaceDir((RepairStandPoint() - Position).normalized);
                    // Brief tool sparks while wrenching — reuse equipment spark language
                    if (Time.frameCount % 7 == 0)
                    {
                        Vector2 tip = Position + (RepairStandPoint() - Position).normalized * 0.2f;
                        EquipmentSparkFx.SpawnRepair(
                            transform.parent != null ? transform.parent : transform, tip, count: 2);
                    }
                    if (_repairTimer > 0f) break;
                    FinishRepair();
                    break;

                case State.ReturnToPost:
                    SetStatus("NO WORK NEEDED");
                    if (needsRepair)
                    {
                        BeginDispatch();
                        break;
                    }
                    if (FollowTo(_post))
                    {
                        _state = State.IdleAtPost;
                        _announcedDispatch = false;
                        _workKind = EngineerWorkKind.Idle;
                        SetStatus("NO WORK NEEDED");
                        _hasJobTarget = false;
                        DigHoodLog.Push("REPAIR | Engineer back at post");
                    }
                    break;
            }
        }

        void TickIdleInfrastructure(float gameHoursDelta)
        {
            if (_infra == null)
            {
                SetStatus("NO WORK NEEDED");
                return;
            }

            _evaluateCooldown -= gameHoursDelta;
            if (_evaluateCooldown > 0f)
            {
                if (_workKind != EngineerWorkKind.NoWorkNeeded && _workKind != EngineerWorkKind.Idle)
                    return;
                SetStatus("NO WORK NEEDED");
                return;
            }

            _state = State.Evaluating;
            _workKind = EngineerWorkKind.Evaluating;
        }

        void ResolveEvaluation()
        {
            _evaluateCooldown = EvaluateIntervalGameHours;
            Vector2 from = Position;

            Vector2Int crit = default;
            float critScore = 0f;
            bool critOk = false;
            Vector2Int lan = default;
            float lanScore = 0f;
            bool lanOk = false;
            Vector2Int prev = default;
            float prevScore = 0f;
            bool prevOk = false;

            if (_infra != null)
            {
                critOk = _infra.TryPickNextSupportCell(from, criticalOnly: true, out crit, out critScore);
                _evalSupportDebug = _infra.LastSupportEvalDebug;

                lanOk = _infra.TryPickNextLanternCell(from, out lan, out lanScore);
                _evalLanternDebug = _infra.LastLanternEvalDebug;
            }
            else
            {
                _evalSupportDebug = "SUPPORT | no infra";
                _evalLanternDebug = "LANTERN | no infra";
            }

            // 1) Critical tunnel support
            if (critOk)
            {
                BeginSupportJob(crit, critical: true);
                DigHoodLog.Push($"ENGINEER | CRITICAL support ({crit.x},{crit.y}) score {critScore:0.0}");
                return;
            }

            // 2) Needed lantern on dark corridor
            if (lanOk)
            {
                _jobCell = lan;
                _jobWorld = _world.CellCenter(lan.x, lan.y);
                _hasJobTarget = true;
                _workKind = EngineerWorkKind.DarkAreaFound;
                SetStatus("DARK AREA FOUND");
                _state = State.WalkingToLantern;
                _nav?.Invalidate();
                DigHoodLog.Push($"ENGINEER | dark area / lantern ({lan.x},{lan.y}) score {lanScore:0.0}");
                return;
            }

            // 3) Preventative support
            if (_infra != null)
            {
                prevOk = _infra.TryPickNextSupportCell(from, criticalOnly: false, out prev, out prevScore);
                _evalSupportDebug = _infra.LastSupportEvalDebug;
            }

            if (prevOk)
            {
                BeginSupportJob(prev, critical: false);
                DigHoodLog.Push($"ENGINEER | preventative support ({prev.x},{prev.y}) score {prevScore:0.0}");
                return;
            }

            _state = State.IdleAtPost;
            _hasJobTarget = false;
            _workKind = EngineerWorkKind.NoWorkNeeded;
            SetStatus("NO WORK NEEDED");
        }

        void BeginSupportJob(Vector2Int cell, bool critical)
        {
            _jobCell = cell;
            _jobWorld = _world.CellCenter(cell.x, cell.y);
            _hasJobTarget = true;
            _jobIsCriticalSupport = critical;
            _workKind = EngineerWorkKind.SupportNeeded;
            SetStatus("SUPPORT NEEDED");
            _state = State.WalkingToSupport;
            _nav?.Invalidate();
        }

        void BeginInstallLantern()
        {
            _state = State.InstallingLantern;
            _buildHoursLeft = _infra != null ? _infra.LanternInstallGameHours : 0.2f;
            _workKind = EngineerWorkKind.InstallingLantern;
            SetStatus("INSTALLING LANTERN");
            FaceDir((_jobWorld - Position).normalized);
        }

        void TickInstallLantern(float gameHoursDelta)
        {
            FaceDir((_jobWorld - Position).normalized);
            _buildHoursLeft -= gameHoursDelta;
            if (_buildHoursLeft > 0f) return;

            if (_infra != null && _infra.TryPlaceLantern(_jobWorld))
                DigHoodLog.Push($"ENGINEER | lantern ({_jobCell.x},{_jobCell.y})");

            FinishInfraJob();
        }

        void BeginBuildSupport()
        {
            _state = State.BuildingSupport;
            _buildHoursLeft = _infra != null ? _infra.SupportBuildGameHours : 0.28f;
            _workKind = EngineerWorkKind.BuildingSupport;
            SetStatus("BUILDING SUPPORT");
            FaceDir((_jobWorld - Position).normalized);
        }

        void TickBuildSupport(float gameHoursDelta)
        {
            FaceDir((_jobWorld - Position).normalized);
            _buildHoursLeft -= gameHoursDelta;
            if (_buildHoursLeft > 0f) return;

            if (_infra != null && _infra.TryBuildSupport(_jobCell.x, _jobCell.y, SupportBuildQuality01()))
                DigHoodLog.Push($"ENGINEER | support ({_jobCell.x},{_jobCell.y})"
                    + (_jobIsCriticalSupport ? " CRITICAL" : "")
                    + $" Q={SupportBuildQuality01():0.00}");

            FinishInfraJob();
        }

        void FinishInfraJob()
        {
            _state = State.IdleAtPost;
            _workKind = EngineerWorkKind.Idle;
            _hasJobTarget = false;
            SetStatus("NO WORK NEEDED");
            _evaluateCooldown = 0.2f;
        }

        void AbortInfraJob()
        {
            _buildHoursLeft = 0f;
            _hasJobTarget = false;
            if (_state == State.Evaluating
                || _state == State.WalkingToLantern
                || _state == State.InstallingLantern
                || _state == State.WalkingToSupport
                || _state == State.BuildingSupport)
            {
                _nav?.Invalidate();
            }
        }

        float _repairStrandTimer;

        void BeginDispatch()
        {
            AbortInfraJob();
            _state = State.ToExcavator;
            _workKind = EngineerWorkKind.RepairEnRoute;
            SetStatus("REPAIRING");
            _nav?.Invalidate();
            _repairStrandTimer = 0f;
            RefreshRepairCoop();
            OnRepairDispatched?.Invoke(_activeRepairCoop);
            if (!_announcedDispatch)
            {
                _announcedDispatch = true;
                DigHoodLog.Push(
                    $"REPAIR | Engineer dispatched — excavator OVERHEATED | coop Q={_activeRepairCoop.Quality:0.00} spd×{_repairDispatchSpeedMul:0.00}");
            }
        }

        void BeginRepair()
        {
            RefreshRepairCoop();
            _state = State.Repairing;
            float durMul = _activeRepairCoop.Active ? _activeRepairCoop.RepairDurationMul : 1f;
            _repairTimer = RepairSeconds * durMul;
            ExcavatorEngineerCooperation.StampAppliedModifiers(_repairDispatchSpeedMul, durMul);
            OnRepairBegun?.Invoke(_activeRepairCoop);
            _workKind = EngineerWorkKind.Repairing;
            SetStatus("REPAIRING");
            DigHoodLog.Push(
                $"REPAIR | Engineer on site — fixing drill… | dur×{durMul:0.00} ({_repairTimer:0.00}s)");
        }

        void FinishRepair()
        {
            int excavOpId = _excavator != null ? _excavator.AssignedWorkerId : 0;
            if (_excavator != null && _excavator.IsOverheated)
            {
                _excavator.ClearOverheatByEngineer();
                EquipmentSparkFx.SpawnRestart(
                    transform.parent != null ? transform.parent : transform,
                    _excavator.Position);
            }

            const float baseRecoverMag = 4f;
            float recoverMul = 1f;
            var consequence = CooperationWorkConsequence.NeutralComplete;
            if (_activeRepairCoop.Active && excavOpId > 0 && AssignedWorkerId > 0)
            {
                consequence = ExcavatorEngineerCooperation.ResolveRepairOutcome(
                    _activeRepairCoop,
                    excavOpId,
                    AssignedWorkerId,
                    _excavator != null ? _excavator.ProviderId : "",
                    ProviderId,
                    baseRecoverMag,
                    out recoverMul);
            }
            OnRepairCompleted?.Invoke(consequence);

            _state = State.ReturnToPost;
            _workKind = EngineerWorkKind.Returning;
            _repairDispatchSpeedMul = 1f;
            SetStatus("NO WORK NEEDED");
            _nav?.Invalidate();
            DigHoodLog.Push(
                $"REPAIR | Fix complete — excavator online | {ExcavatorEngineerCooperation.LastConsequenceDetail}");

            // EquipmentRecovered targets the operator who benefits (frozen id at repair complete)
            if (excavOpId > 0)
            {
                WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                    excavOpId,
                    WorkerStateEventType.EquipmentRecovered,
                    baseRecoverMag * recoverMul,
                    "EngineerRepair",
                    JobType.Excavation,
                    _excavator != null ? _excavator.ProviderId : "",
                    relatedWorkerId: AssignedWorkerId > 0 ? AssignedWorkerId : 0));
            }
            if (AssignedWorkerId > 0)
            {
                WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                    AssignedWorkerId,
                    WorkerStateEventType.ProgressSuccess,
                    2.5f,
                    "RepairComplete",
                    JobType.Engineering,
                    ProviderId,
                    relatedWorkerId: excavOpId));
            }
        }

        void RefreshRepairCoop()
        {
            if (_coopEval != null)
            {
                _activeRepairCoop = _coopEval();
                _repairDispatchSpeedMul = _activeRepairCoop.Active
                    ? _activeRepairCoop.DispatchSpeedMul
                    : 1f;
            }
            else
            {
                _activeRepairCoop = CooperationAssessment.Inactive();
                _repairDispatchSpeedMul = 1f;
            }
            ExcavatorEngineerCooperation.StampAppliedModifiers(
                _repairDispatchSpeedMul,
                _activeRepairCoop.Active ? _activeRepairCoop.RepairDurationMul : 1f);
        }

        void SetStatus(string status)
        {
            _debugStatus = status;
        }

        Vector2 RepairStandPoint()
        {
            if (_excavator == null) return _post;
            Vector2 dig = _excavator.Position;
            Vector2 delta = Position - dig;
            if (delta.sqrMagnitude < 0.0001f)
                delta = Vector2.down;
            return dig + delta.normalized * ArriveRadius;
        }

        bool FollowTo(Vector2 goal, float speed = -1f)
        {
            float moveSpeed = speed > 0f ? speed : MoveSpeed;
            Vector2 from = Position;
            if ((goal - from).sqrMagnitude <= ArriveRadius * ArriveRadius * 0.55f)
            {
                _nav?.Invalidate();
                return true;
            }

            float bias = moveSpeed / WorkerPhysicalProfile.ReferenceWalkSpeed;
            float personSpeed = WorkerLocomotion.WalkSpeedAt(
                _assignedWorker, _world, from, BodyRadius,
                roleBias: bias, carriedLoad01: 0f, isMoving: true);

            return _nav.Follow(
                from,
                goal,
                personSpeed,
                BodyRadius,
                face: FaceDir,
                tryStep: TryStep);
        }

        bool TryStep(Vector2 dir, float step)
        {
            if (dir.sqrMagnitude < 0.00001f) return false;
            Vector2 next = Position + dir.normalized * step;
            if (_world.CircleHitsSolid(next, BodyRadius * 0.85f)) return false;
            transform.localPosition = next;
            return true;
        }

        void FaceDir(Vector2 dir)
        {
            if (_facing == null || dir.sqrMagnitude < 0.0001f) return;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            _facing.localRotation = Quaternion.RotateTowards(
                _facing.localRotation,
                Quaternion.Euler(0f, 0f, ang),
                260f * Time.deltaTime);
        }

        void OnValidate() => _stats?.ClampAll();
    }
}
