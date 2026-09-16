using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    public enum StewardWorkKind : byte
    {
        Idle = 0,
        PreparingMeal = 1,
        CleaningCamp = 2,
        TendingWounds = 3,
        KitchenDuty = 4,
        AfterMealCleanup = 5,
    }

    /// <summary>
    /// Camp support provider — kitchen / hygiene / wound care. Person-first via WorkerRuntime.
    /// Station stays at camp; assigned worker operates from this post.
    /// </summary>
    public sealed class StewardPerson : MonoBehaviour, IWorkProvider
    {
        public const float MoveSpeed = 0.58f;
        public const float BodyRadius = 0.12f;
        public const float ArriveRadius = 0.32f;
        static int _nextProviderSerial = 1;

        enum State : byte
        {
            IdleAtPost,
            WalkingDuty,
            Working,
        }

        FineTerrainWorld _world;
        ExcavatedPathfinder _nav;
        Vector2 _post;
        State _state = State.IdleAtPost;
        StewardWorkKind _workKind = StewardWorkKind.Idle;
        string _debugStatus = "CAMP DUTY";
        float _dutyHoursLeft;
        float _evaluateCooldown;
        Vector2 _dutyTarget;
        bool _hasDutyTarget;
        Transform _facing;
        CampLifeState _camp;
        Func<WorkerRuntime[]> _crewLookup;
        Func<WorkerRuntime, Vector2> _patientWorldPos;
        Func<SocialMemoryStore> _memoryLookup;
        Func<float> _gameHoursLookup;
        System.Random _rng = new(77);

        public Vector2 Position => transform.localPosition;
        public StewardWorkKind WorkKind => _workKind;
        public string WorkLabel => _debugStatus;
        public string DebugStatus => _debugStatus;
        public WorkerStats Stats =>
            _assignedWorker != null ? _assignedWorker.Stats : (_stats ??= new WorkerStats());
        public WorkerRuntime AssignedWorker => _assignedWorker;

        public string ProviderId { get; private set; } = "steward.0";
        public JobType JobType => DeepCore.FreeMovement.JobType.Steward;
        public int AssignedWorkerId { get; private set; } = -1;
        public bool IsAvailable => true;

        [SerializeField] WorkerStats _stats = new WorkerStats();
        WorkerRuntime _assignedWorker;

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

        public void YieldForReassignment()
        {
            _nav?.Invalidate();
            DigHoodLog.Push($"ASSIGN | Steward yield | {_debugStatus}");
        }

        public void BindCamp(CampLifeState camp) => _camp = camp;

        public void BindCrewLookup(Func<WorkerRuntime[]> lookup) => _crewLookup = lookup;

        public void BindPatientWorldPos(Func<WorkerRuntime, Vector2> worldPos) =>
            _patientWorldPos = worldPos;

        public void BindSocialMemory(Func<SocialMemoryStore> lookup, Func<float> gameHours) =>
            (_memoryLookup, _gameHoursLookup) = (lookup, gameHours);

        void EnsureProviderId()
        {
            if (string.IsNullOrEmpty(ProviderId) || ProviderId == "steward.0")
                ProviderId = $"steward.{_nextProviderSerial++}";
        }

        public static StewardPerson Spawn(Transform parent, FineTerrainWorld world, Vector2 post)
        {
            var go = new GameObject("Steward");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = post;

            var facing = new GameObject("Facing");
            facing.transform.SetParent(go.transform, false);
            CrewVisualKit.AttachSteward(facing.transform, out _);

            var s = go.AddComponent<StewardPerson>();
            s.EnsureProviderId();
            s._facing = facing.transform;
            s.Setup(world, post);
            return s;
        }

        public void Setup(FineTerrainWorld world, Vector2 post)
        {
            _world = world;
            _post = post;
            _nav = new ExcavatedPathfinder(world);
            _state = State.IdleAtPost;
            _workKind = StewardWorkKind.Idle;
            _debugStatus = "CAMP DUTY";
            transform.localPosition = post;
        }

        public void Tick(float gameHours, bool onShift)
        {
            if (_camp != null)
                _camp.StewardActivity = _debugStatus;

            if (!onShift || _assignedWorker == null || !_assignedWorker.IsAlive)
            {
                _workKind = StewardWorkKind.Idle;
                _debugStatus = "OFF SHIFT";
                if (_camp != null) _camp.StewardActivity = _debugStatus;
                return;
            }

            if (!EnforcePriorityAuthority())
            {
                if (_camp != null) _camp.StewardActivity = _debugStatus;
                return;
            }

            float cleanPush = 0f;
            if (_workKind == StewardWorkKind.CleaningCamp
                || _workKind == StewardWorkKind.AfterMealCleanup
                || _workKind == StewardWorkKind.KitchenDuty)
            {
                float logistics = (_assignedWorker.Stats.Get(WorkerStatId.Logistics) - 1) / 19f;
                float work = (_assignedWorker.Stats.Get(WorkerStatId.WorkRate) - 1) / 19f;
                cleanPush = 0.35f + logistics * 0.4f + work * 0.25f;
            }
            _camp?.DriftHygiene(gameHours, cleanPush);

            _evaluateCooldown -= gameHours;
            if (_state == State.IdleAtPost && _evaluateCooldown <= 0f)
            {
                PickDuty();
                _evaluateCooldown = 0.45f;
            }

            if (_state == State.WalkingDuty && _hasDutyTarget)
            {
                Vector2 p = transform.localPosition;
                float speed = WorkerLocomotion.WalkSpeedAt(
                    _assignedWorker, _world, p, BodyRadius, 1f, 0f, true);
                bool done = _nav.Follow(
                    p, _dutyTarget, speed, BodyRadius,
                    face: dir => Face(dir),
                    tryStep: (dir, step) => TryStep(dir, step));
                if (done || (p - _dutyTarget).sqrMagnitude <= ArriveRadius * ArriveRadius)
                {
                    transform.localPosition = _dutyTarget;
                    _state = State.Working;
                    _dutyHoursLeft = _workKind switch
                    {
                        StewardWorkKind.TendingWounds => 0.55f,
                        StewardWorkKind.PreparingMeal => 0.8f,
                        StewardWorkKind.CleaningCamp => 0.7f,
                        StewardWorkKind.AfterMealCleanup => 0.5f,
                        _ => 0.4f,
                    };
                }
            }
            else if (_state == State.Working)
            {
                _dutyHoursLeft -= gameHours;
                if (_workKind == StewardWorkKind.TendingWounds && _dutyHoursLeft <= 0.15f)
                    TendNearestInjured();
                if (_dutyHoursLeft <= 0f)
                {
                    _state = State.IdleAtPost;
                    _workKind = StewardWorkKind.Idle;
                    _debugStatus = "CAMP DUTY";
                    _hasDutyTarget = false;
                    _nav.Invalidate();
                }
            }
            else
            {
                // Idle drift toward post
                Vector2 p = transform.localPosition;
                if ((p - _post).sqrMagnitude > 0.04f)
                {
                    float speed = WorkerLocomotion.WalkSpeedAt(
                        _assignedWorker, _world, p, BodyRadius, 1.0f, 0f, true) * 0.85f;
                    _nav.Follow(p, _post, speed, BodyRadius, Face, TryStep);
                }
            }

            if (_camp != null) _camp.StewardActivity = _debugStatus;
        }

        void PickDuty()
        {
            var crew = _crewLookup?.Invoke();
            WorkerRuntime injured = null;
            if (crew != null)
            {
                for (int i = 0; i < crew.Length; i++)
                {
                    var wr = crew[i];
                    if (wr == null || !wr.IsAlive) continue;
                    if (wr.State != null && wr.State.Incapacitated) continue;
                    bool needs = wr.State != null && wr.State.NeedsCare
                        || (wr.CampBody != null && wr.CampBody.SeekingStewardCare)
                        || (wr.Injuries != null && wr.Injuries.Count > 0);
                    if (!needs) continue;
                    if (!PatientAtCamp(wr)) continue;
                    injured = wr;
                    if (wr.CampBody != null && wr.CampBody.SeekingStewardCare) break;
                }
            }

            int roll = _rng.Next(100);
            if (injured != null && roll < 55 && TaskAllowed(WorkerGenericTaskIds.TreatInjuries))
            {
                _workKind = StewardWorkKind.TendingWounds;
                _debugStatus = $"TEND {injured.DisplayName}";
                _dutyTarget = _post + new Vector2(0.35f, -0.2f);
            }
            else if (_camp != null && _camp.Hygiene01 < 0.55f && roll < 70
                     && TaskAllowed(WorkerGenericTaskIds.CleanCamp))
            {
                _workKind = StewardWorkKind.CleaningCamp;
                _debugStatus = "CLEAN CAMP";
                _dutyTarget = _post + new Vector2(-0.4f, 0.15f);
            }
            else if (roll < 55 && TaskAllowed(WorkerGenericTaskIds.TendCampSystems))
            {
                _workKind = StewardWorkKind.KitchenDuty;
                _debugStatus = "KITCHEN";
                _dutyTarget = _post + new Vector2(0.15f, 0.25f);
            }
            else if (roll < 75 && TaskAllowed(WorkerGenericTaskIds.PrepareMeals))
            {
                _workKind = StewardWorkKind.PreparingMeal;
                _debugStatus = "PREP MEAL";
                _dutyTarget = _post + new Vector2(0.2f, 0.1f);
            }
            else if (TaskAllowed(WorkerGenericTaskIds.TendCampSystems)
                     || TaskAllowed(WorkerGenericTaskIds.CleanCamp))
            {
                _workKind = StewardWorkKind.AfterMealCleanup;
                _debugStatus = "CLEANUP";
                _dutyTarget = _post + new Vector2(-0.2f, -0.15f);
            }
            else
            {
                // All camp tasks OFF — stay idle; do not invent duty
                _workKind = StewardWorkKind.Idle;
                _debugStatus = "IDLE (PRIORITY OFF)";
                _hasDutyTarget = false;
                _state = State.IdleAtPost;
                if (_camp != null) _camp.StewardActivity = _debugStatus;
                return;
            }

            _dutyHoursLeft = _workKind switch
            {
                StewardWorkKind.TendingWounds => 0.55f,
                StewardWorkKind.CleaningCamp => 0.7f,
                StewardWorkKind.KitchenDuty => 0.45f,
                StewardWorkKind.PreparingMeal => 0.9f,
                StewardWorkKind.AfterMealCleanup => 0.4f,
                _ => 0.35f,
            };
            _hasDutyTarget = true;
            _state = State.WalkingDuty;
            _nav.Invalidate();
            if (_camp != null) _camp.StewardActivity = _debugStatus;
        }

        bool TaskAllowed(string taskId) =>
            _assignedWorker?.Priorities == null
            || !_assignedWorker.Priorities.IsOff(taskId);

        /// <summary>Returns false when current duty was aborted due to priority OFF.</summary>
        bool EnforcePriorityAuthority()
        {
            string taskId = _workKind switch
            {
                StewardWorkKind.TendingWounds => WorkerGenericTaskIds.TreatInjuries,
                StewardWorkKind.CleaningCamp => WorkerGenericTaskIds.CleanCamp,
                StewardWorkKind.PreparingMeal => WorkerGenericTaskIds.PrepareMeals,
                StewardWorkKind.KitchenDuty => WorkerGenericTaskIds.TendCampSystems,
                StewardWorkKind.AfterMealCleanup => WorkerGenericTaskIds.TendCampSystems,
                _ => null,
            };
            if (taskId == null || TaskAllowed(taskId)) return true;
            _workKind = StewardWorkKind.Idle;
            _debugStatus = "IDLE (PRIORITY OFF)";
            _hasDutyTarget = false;
            _state = State.IdleAtPost;
            _nav?.Invalidate();
            return false;
        }

        bool PatientAtCamp(WorkerRuntime wr)
        {
            if (wr?.CampBody != null && wr.CampBody.SeekingStewardCare) return true;
            Vector2 p = _patientWorldPos != null ? _patientWorldPos(wr) : Position;
            return Vector2.Distance(p, _post) <= 4.2f;
        }

        void TendNearestInjured()
        {
            var crew = _crewLookup?.Invoke();
            if (crew == null || _assignedWorker == null) return;
            WorkerRuntime best = null;
            for (int i = 0; i < crew.Length; i++)
            {
                var wr = crew[i];
                if (wr == null || !wr.IsAlive) continue;
                if (wr.State != null && wr.State.Incapacitated) continue;
                if (wr.Injuries == null || wr.Injuries.Count == 0) continue;
                if (!PatientAtCamp(wr)) continue;
                best = wr;
                if (wr.CampBody != null && wr.CampBody.SeekingStewardCare) break;
            }
            if (best == null) return;
            SocialMemoryStore mem = _memoryLookup?.Invoke();
            float gh = _gameHoursLookup != null ? _gameHoursLookup() : 0f;
            if (StewardWoundCare.TryTend(_assignedWorker, best, out string result, mem, gh))
            {
                DigHoodLog.Push($"STEWARD | {result} → {best.DisplayName}");
                _debugStatus = result;
            }
        }

        void Face(Vector2 dir)
        {
            if (_facing == null || dir.sqrMagnitude < 0.0001f) return;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            _facing.localRotation = Quaternion.Euler(0f, 0f, ang);
        }

        bool TryStep(Vector2 dir, float step)
        {
            if (dir.sqrMagnitude < 0.00001f) return false;
            Vector2 next = (Vector2)transform.localPosition + dir.normalized * step;
            if (_world != null && _world.CircleHitsSolid(next, BodyRadius * 0.85f))
                return false;
            transform.localPosition = next;
            return true;
        }
    }
}
