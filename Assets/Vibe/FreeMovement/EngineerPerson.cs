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
    /// </summary>
    public sealed class EngineerPerson : MonoBehaviour
    {
        public const float MoveSpeed = 1.45f;
        public const float BodyRadius = 0.12f;
        public const float RepairSeconds = 2.6f;
        public const float ArriveRadius = 0.38f;
        public float EvaluateIntervalGameHours = 0.35f;

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
        Transform _facing;
        bool _announcedDispatch;
        Vector2Int _jobCell;
        Vector2 _jobWorld;
        bool _hasJobTarget;
        bool _jobIsLantern;
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

        /// <summary>Body / Mind / Soul sheet. Data only — unused by repair logic yet.</summary>
        public WorkerStats Stats => _stats ??= new WorkerStats();

        [SerializeField] WorkerStats _stats = new WorkerStats();

        public static EngineerPerson Spawn(Transform parent, FineTerrainWorld world, Vector2 post)
        {
            var go = new GameObject("Engineer");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = post;

            var facing = new GameObject("Facing");
            facing.transform.SetParent(go.transform, false);
            CrewVisualKit.AttachEngineer(facing.transform, out _);

            var e = go.AddComponent<EngineerPerson>();
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
        public void SetPost(Vector2 post) => _post = post;

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
                        _state = State.ReturnToPost;
                        _nav?.Invalidate();
                        break;
                    }
                    if (FollowTo(RepairStandPoint()))
                        BeginRepair();
                    break;

                case State.Repairing:
                    SetStatus("REPAIRING");
                    _repairTimer -= Time.deltaTime;
                    FaceDir((RepairStandPoint() - Position).normalized);
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

            DigHoodLog.Push(_evalSupportDebug);
            DigHoodLog.Push(_evalLanternDebug);

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
                _jobIsLantern = true;
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
                DigHoodLog.Push(_evalSupportDebug);
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
            _jobIsLantern = false;
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

            if (_infra != null && _infra.TryBuildSupport(_jobCell.x, _jobCell.y))
                DigHoodLog.Push($"ENGINEER | support ({_jobCell.x},{_jobCell.y})"
                    + (_jobIsCriticalSupport ? " CRITICAL" : ""));

            FinishInfraJob();
        }

        void FinishInfraJob()
        {
            _state = State.IdleAtPost;
            _workKind = EngineerWorkKind.Idle;
            _hasJobTarget = false;
            _jobIsLantern = false;
            SetStatus("NO WORK NEEDED");
            _evaluateCooldown = 0.2f;
        }

        void AbortInfraJob()
        {
            _buildHoursLeft = 0f;
            _jobIsLantern = false;
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

        void BeginDispatch()
        {
            AbortInfraJob();
            _state = State.ToExcavator;
            _workKind = EngineerWorkKind.RepairEnRoute;
            SetStatus("REPAIRING");
            _nav?.Invalidate();
            if (!_announcedDispatch)
            {
                _announcedDispatch = true;
                DigHoodLog.Push("REPAIR | Engineer dispatched — excavator OVERHEATED");
            }
        }

        void BeginRepair()
        {
            _state = State.Repairing;
            _repairTimer = RepairSeconds;
            _workKind = EngineerWorkKind.Repairing;
            SetStatus("REPAIRING");
            DigHoodLog.Push("REPAIR | Engineer on site — fixing drill…");
        }

        void FinishRepair()
        {
            if (_excavator != null && _excavator.IsOverheated)
                _excavator.ClearOverheatByEngineer();
            _state = State.ReturnToPost;
            _workKind = EngineerWorkKind.Returning;
            SetStatus("NO WORK NEEDED");
            _nav?.Invalidate();
            DigHoodLog.Push("REPAIR | Fix complete — excavator online");
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

        bool FollowTo(Vector2 goal)
        {
            Vector2 from = Position;
            if ((goal - from).sqrMagnitude <= ArriveRadius * ArriveRadius * 0.55f)
            {
                _nav?.Invalidate();
                return true;
            }

            return _nav.Follow(
                from,
                goal,
                MoveSpeed,
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
