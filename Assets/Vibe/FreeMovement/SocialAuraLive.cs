using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Stage 1: live presence kinds for social eligibility (mirrors runner physical state).
    /// </summary>
    public enum SocialPresenceKind : byte
    {
        Operating = 0,
        Idle = 1,
        CommutingHome = 2,
        Sleeping = 3,
        CommutingToWork = 4,
    }

    /// <summary>Centralized social eligibility — not CanPerformJobActions.</summary>
    public static class SocialAuraEligibility
    {
        /// <summary>Injury at/above this suppresses normal social encounters.</summary>
        public const float InjurySuppressThreshold = 70f;

        public static bool IsEligible(WorkerRuntime wr, SocialPresenceKind presence)
        {
            if (wr?.State == null) return false;
            if (!wr.State.IsAlive) return false;
            if (wr.State.Incapacitated) return false;
            if (presence == SocialPresenceKind.Sleeping) return false;
            if (wr.State.NeedsCare) return false;
            if (wr.State.Injury >= InjurySuppressThreshold) return false;
            return presence == SocialPresenceKind.Operating
                   || presence == SocialPresenceKind.Idle
                   || presence == SocialPresenceKind.CommutingHome
                   || presence == SocialPresenceKind.CommutingToWork;
        }
    }

    /// <summary>Stage 1 live reach / distance falloff (Stage 0 reach × world scale).</summary>
    public static class SocialAuraLiveTuning
    {
        public const float ReachWorldScale = 2.35f;
        public const float PressureGainPerGameHour = 0.92f;
        public const float OvernightPressureDecayMul = 3.2f;
        public const float ContextEventWindowHours = 2.0f;

        public static float EffectiveReach(SocialExpression expr) =>
            Mathf.Max(0.15f, expr.Reach * ReachWorldScale);

        /// <summary>Smooth falloff 0–1. Strong near, weak at edge, zero beyond max reach.</summary>
        public static float DistanceFalloff(float distance, float reachA, float reachB)
        {
            float maxReach = Mathf.Max(reachA, reachB);
            if (maxReach <= 0.001f || distance >= maxReach) return 0f;
            float t = Mathf.Clamp01(distance / maxReach);
            return (1f - t) * (1f - t);
        }

        public static bool Reaches(float distance, float reach) => distance <= reach;
    }

    /// <summary>Derive live SocialContext from assignment + recent WorkerStateEvent history.</summary>
    public static class SocialContextLive
    {
        public static SocialContext Derive(
            WorkerRuntime a,
            WorkerRuntime b,
            JobType jobA,
            JobType jobB,
            SocialPresenceKind presenceA,
            SocialPresenceKind presenceB,
            float nowGameHours,
            bool bothInCamp = false)
        {
            if (TrySharedProblem(a, b, nowGameHours))
                return SocialContext.SharedProblem;

            if (TryRecent(a, b, nowGameHours,
                    WorkerStateEventType.MajorSuccess,
                    WorkerStateEventType.Discovery,
                    WorkerStateEventType.ProgressSuccess)
                && !TryRecent(a, b, nowGameHours,
                    WorkerStateEventType.WorkBlocked,
                    WorkerStateEventType.EquipmentProblem,
                    WorkerStateEventType.RepeatedFailure))
                return SocialContext.RecentSuccess;

            if (TryRecent(a, b, nowGameHours,
                    WorkerStateEventType.WorkBlocked,
                    WorkerStateEventType.EquipmentProblem,
                    WorkerStateEventType.InvestigationFailure,
                    WorkerStateEventType.RepeatedFailure))
                return SocialContext.RecentFailure;

            if (presenceA == SocialPresenceKind.Operating && presenceB == SocialPresenceKind.Operating)
            {
                if (jobA != JobType.Unassigned && jobA == jobB)
                    return SocialContext.WorkingTogether;
                if (IsCollaborationPair(jobA, jobB))
                    return SocialContext.WorkingTogether;
            }

            // Camp foundation: off-shift (not Operating) + both inside camp radius.
            // Still resolved by normal Social Aura — may help or harm.
            if (bothInCamp
                && presenceA != SocialPresenceKind.Operating
                && presenceB != SocialPresenceKind.Operating
                && presenceA != SocialPresenceKind.Sleeping
                && presenceB != SocialPresenceKind.Sleeping)
                return SocialContext.Camp;

            return SocialContext.IdleNearby;
        }

        static bool IsCollaborationPair(JobType a, JobType b)
        {
            bool Pair(JobType x, JobType y) => (a == x && b == y) || (a == y && b == x);
            return Pair(JobType.Prospecting, JobType.Refining)
                   || Pair(JobType.Engineering, JobType.Excavation)
                   || Pair(JobType.Prospecting, JobType.Excavation);
        }

        static bool TrySharedProblem(WorkerRuntime a, WorkerRuntime b, float now)
        {
            if (!TryFindProblem(a, now, out var ea)) return false;
            if (!TryFindProblem(b, now, out var eb)) return false;
            if (!string.IsNullOrEmpty(ea.ProviderId) && ea.ProviderId == eb.ProviderId)
                return true;
            if (ea.RelatedWorkerId == b.WorkerId || eb.RelatedWorkerId == a.WorkerId)
                return true;
            if (ea.JobType != JobType.Unassigned && ea.JobType == eb.JobType)
                return true;
            return false;
        }

        static bool TryFindProblem(WorkerRuntime wr, float now, out WorkerStateEvent ev)
        {
            ev = null;
            if (wr?.EventHistory == null) return false;
            var items = wr.EventHistory.Items;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var e = items[i]?.Event;
                if (e == null) continue;
                if (now - e.GameHours > SocialAuraLiveTuning.ContextEventWindowHours) break;
                if (e.EventType == WorkerStateEventType.WorkBlocked
                    || e.EventType == WorkerStateEventType.EquipmentProblem
                    || e.EventType == WorkerStateEventType.PhysicalExhaustion)
                {
                    ev = e;
                    return true;
                }
            }
            return false;
        }

        static bool TryRecent(WorkerRuntime a, WorkerRuntime b, float now, params WorkerStateEventType[] types) =>
            HasType(a, now, types) || HasType(b, now, types);

        static bool HasType(WorkerRuntime wr, float now, WorkerStateEventType[] types)
        {
            if (wr?.EventHistory == null) return false;
            var items = wr.EventHistory.Items;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var e = items[i]?.Event;
                if (e == null) continue;
                if (now - e.GameHours > SocialAuraLiveTuning.ContextEventWindowHours) break;
                for (int t = 0; t < types.Length; t++)
                    if (e.EventType == types[t]) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Stage 1 live Social Aura — avatar proximity → pressure → Stage 0 resolver.
    /// Person identity: WorkerId / WorkerAvatar.PresencePosition (not provider Transform).
    /// Relationship memory lives on SocialAuraWorld directed store (person-keyed).
    /// </summary>
    public sealed class SocialAuraLiveSystem
    {
        readonly SocialAuraWorld _world = new();
        readonly SocialConflictSystem _conflict = new();
        readonly Dictionary<int, int> _encountersThisShift = new();
        readonly HashSet<long> _knownPairs = new();
        readonly List<SocialEncounterLog> _liveLogs = new(128);
        SocialEncounterLog _lastEncounter;
        SocialConflictEvent _lastConflict;
        int _shiftIndex;
        bool _bootstrapped;

        public int TotalEncounters { get; private set; }
        public int CampEncounters { get; private set; }
        public int CommuteEncounters { get; private set; }
        public int WorkAreaEncounters { get; private set; }
        public int CooldownSuppressions { get; private set; }
        public readonly Dictionary<SocialContext, int> ContextCounts = new();
        public readonly Dictionary<long, int> PairEncounterCounts = new();

        public IReadOnlyList<SocialEncounterLog> LiveLogs => _liveLogs;
        public SocialEncounterLog LastEncounter => _lastEncounter;
        public SocialConflictEvent LastConflict => _lastConflict;
        public SocialAuraWorld World => _world;
        public SocialConflictSystem Conflict => _conflict;
        public SocialMemoryStore Memory => _world.Memory;
        public RelationshipTrajectoryStore Trajectory => _world.Trajectory;
        public bool IsBootstrapped => _bootstrapped;

        public void Bootstrap(IReadOnlyList<WorkerRuntime> crew)
        {
            _bootstrapped = true;
            SyncActors(crew);
            _shiftIndex = 0;
            _world.BeginShift(_shiftIndex);
            _encountersThisShift.Clear();
        }

        public void NotifyShiftStart(IReadOnlyList<WorkerRuntime> crew)
        {
            if (!_bootstrapped) Bootstrap(crew);
            else SyncActors(crew);
            _shiftIndex++;
            _world.BeginShift(_shiftIndex);
            _encountersThisShift.Clear();
        }

        void SyncActors(IReadOnlyList<WorkerRuntime> crew)
        {
            if (crew == null) return;
            for (int i = 0; i < crew.Count; i++)
            {
                var wr = crew[i];
                if (wr == null) continue;
                var existing = _world.Get(wr.WorkerId);
                if (existing == null)
                {
                    // Live State/Stats refs — Stage 0 consequences mutate real WorkerState
                    _world.AddActor(new SocialSimActor(wr.WorkerId, wr.DisplayName, wr.Stats, wr.State));
                }
                else
                {
                    existing.Stats = wr.Stats;
                    existing.State = wr.State;
                    existing.Name = wr.DisplayName;
                    existing.RefreshExpression();
                }
            }
        }

        public SocialDirectedRelation Relation(int from, int to) => _world.Relation(from, to);
        public SocialPairTransient Pair(int a, int b) => _world.Pair(a, b);

        /// <summary>Game-time tick. Call after avatar position sync.</summary>
        public void Tick(
            IReadOnlyList<WorkerRuntime> crew,
            WorkerPresenceRegistry presence,
            float gameHoursDelta,
            Func<WorkerRuntime, SocialPresenceKind> presenceOf,
            Func<int, JobType> jobOf,
            bool isAsleep,
            Vector2 campCenter,
            float campRadius = 2.2f)
        {
            if (!_bootstrapped || crew == null || presence == null || gameHoursDelta <= 0f)
                return;

            SyncActors(crew);

            if (isAsleep)
            {
                TickOvernightDecay(gameHoursDelta);
                _conflict.TickOvernight(_world, WorkerStateClock.GameHours, gameHoursDelta);
                return;
            }

            _conflict.ClearPendingPresent();

            int n = crew.Count;
            var eligible = new bool[n];
            var kinds = new SocialPresenceKind[n];
            var pos = new Vector2[n];
            var expr = new SocialExpression[n];
            var jobs = new JobType[n];

            for (int i = 0; i < n; i++)
            {
                var wr = crew[i];
                if (wr == null) continue;
                kinds[i] = presenceOf(wr);
                eligible[i] = SocialAuraEligibility.IsEligible(wr, kinds[i]);
                jobs[i] = jobOf != null ? jobOf(wr.WorkerId) : JobType.Unassigned;
                var av = presence.Get(wr.WorkerId);
                pos[i] = av != null ? av.PresencePosition : Vector2.zero;
                var actor = _world.Get(wr.WorkerId);
                if (actor != null)
                {
                    actor.RefreshExpression();
                    expr[i] = actor.Expression;
                }
            }

            var exposedKeys = new HashSet<long>();

            for (int i = 0; i < n; i++)
            {
                if (crew[i] == null || !eligible[i]) continue;
                for (int j = i + 1; j < n; j++)
                {
                    if (crew[j] == null || !eligible[j]) continue;

                    int idA = crew[i].WorkerId;
                    int idB = crew[j].WorkerId;
                    long pk = PairKey(idA, idB);
                    _knownPairs.Add(pk);

                    float dist = Vector2.Distance(pos[i], pos[j]);
                    float reachA = SocialAuraLiveTuning.EffectiveReach(expr[i]);
                    float reachB = SocialAuraLiveTuning.EffectiveReach(expr[j]);
                    if (!SocialAuraLiveTuning.Reaches(dist, reachA)
                        && !SocialAuraLiveTuning.Reaches(dist, reachB))
                        continue;

                    float falloff = SocialAuraLiveTuning.DistanceFalloff(dist, reachA, reachB);
                    if (falloff <= 0.001f) continue;

                    exposedKeys.Add(pk);

                    bool nearCamp = Vector2.Distance(pos[i], campCenter) <= campRadius
                                    && Vector2.Distance(pos[j], campCenter) <= campRadius;
                    var ctx = SocialContextLive.Derive(
                        crew[i], crew[j], jobs[i], jobs[j], kinds[i], kinds[j],
                        WorkerStateClock.GameHours, bothInCamp: nearCamp);

                    // Active argument: divert pressure into conflict beats (game-hour gated)
                    if (_conflict.HasActiveArgument(idA, idB))
                    {
                        float intensity = 0.5f * (
                            (_world.Get(idA)?.Expression.Intensity ?? 0.5f)
                            + (_world.Get(idB)?.Expression.Intensity ?? 0.5f));
                        float gainHint = gameHoursDelta
                                         * SocialAuraLiveTuning.PressureGainPerGameHour
                                         * (0.35f + 0.65f * intensity)
                                         * falloff;
                        var pair = _world.Pair(idA, idB);
                        var conflictEv = _conflict.TickActivePair(
                            _world, idA, idB, WorkerStateClock.GameHours, gainHint, pair);
                        if (conflictEv != null)
                            _lastConflict = conflictEv;
                        continue;
                    }

                    var log = ExposeLive(idA, idB, ctx, gameHoursDelta, falloff);
                    if (log == null) continue;

                    TotalEncounters++;
                    _liveLogs.Add(log);
                    _lastEncounter = log;
                    while (_liveLogs.Count > 64) _liveLogs.RemoveAt(0);

                    if (!ContextCounts.ContainsKey(ctx)) ContextCounts[ctx] = 0;
                    ContextCounts[ctx]++;
                    if (!PairEncounterCounts.ContainsKey(pk)) PairEncounterCounts[pk] = 0;
                    PairEncounterCounts[pk]++;

                    bool commuting = IsCommute(kinds[i]) || IsCommute(kinds[j]);
                    if (ctx == SocialContext.Camp || nearCamp) CampEncounters++;
                    else if (commuting) CommuteEncounters++;
                    else WorkAreaEncounters++;

                    // Round 3: hostile resolve may open a multi-stage argument
                    var emerged = _conflict.TryEmergeFromEncounter(
                        _world, log, WorkerStateClock.GameHours);
                    if (emerged != null)
                        _lastConflict = emerged;
                }
            }

            foreach (var k in _knownPairs)
            {
                if (exposedKeys.Contains(k)) continue;
                int a = (int)(k >> 32);
                int b = (int)(uint)k;
                var pair = _world.Pair(a, b);
                pair.InteractionPressure = Mathf.Max(0f,
                    pair.InteractionPressure - SocialAuraTuning.PressureDecayPerShiftUnit * gameHoursDelta);
                pair.CooldownRemaining = Mathf.Max(0f, pair.CooldownRemaining - gameHoursDelta);
            }
        }

        SocialEncounterLog ExposeLive(
            int idA, int idB, SocialContext context, float gameHoursDelta, float falloff)
        {
            var a = _world.Get(idA);
            var b = _world.Get(idB);
            if (a == null || b == null) return null;

            a.RefreshExpression();
            b.RefreshExpression();

            var pair = _world.Pair(idA, idB);
            if (pair.CooldownRemaining > 0f)
            {
                pair.CooldownRemaining = Mathf.Max(0f, pair.CooldownRemaining - gameHoursDelta);
                CooldownSuppressions++;
                return null;
            }

            int encA = _encountersThisShift.TryGetValue(idA, out var ea) ? ea : 0;
            int encB = _encountersThisShift.TryGetValue(idB, out var eb) ? eb : 0;
            if (encA >= SocialAuraTuning.MaxEncountersPerWorkerPerShift
                || encB >= SocialAuraTuning.MaxEncountersPerWorkerPerShift)
                return null;

            float intensity = 0.5f * (a.Expression.Intensity + b.Expression.Intensity);
            float contextMul = SocialAuraTuning.ContextMul(context);
            var ab = _world.Relation(idA, idB);
            var ba = _world.Relation(idB, idA);
            float warmthAvg = 0.5f * (ab.Warmth + ba.Warmth);
            float hostAvg = 0.5f * (ab.Hostility + ba.Hostility);
            float focusAvg = 0.5f * (a.Stats.Get(WorkerStatId.Focus) + b.Stats.Get(WorkerStatId.Focus));
            float focusDamp = Mathf.Clamp(1.1f - focusAvg / 40f, 0.55f, 1.15f);
            float relationMul = 1f + warmthAvg * 0.015f + hostAvg * 0.012f;
            if (context == SocialContext.SharedProblem)
                relationMul += 0.18f + Mathf.Max(0f, hostAvg) * 0.01f;

            float exprWeight = 0.35f + 0.65f * intensity;

            float gain = gameHoursDelta
                         * SocialAuraLiveTuning.PressureGainPerGameHour
                         * exprWeight
                         * falloff
                         * contextMul
                         * relationMul
                         * focusDamp
                         * EarlyCrewPressure.PressureGainMul;

            pair.InteractionPressure += gain;
            pair.ExposureThisShift += gameHoursDelta;

            string why =
                $"LIVE falloff={falloff:0.##} intens={intensity:0.##} ctx={context}×{contextMul:0.##} " +
                $"relMul={relationMul:0.##} focusDamp={focusDamp:0.##} Δh={gameHoursDelta:0.###} " +
                $"P={pair.InteractionPressure:0.##}/{SocialAuraTuning.PressureTrigger:0.##}";

            if (pair.InteractionPressure < SocialAuraTuning.PressureTrigger)
                return null;

            pair.InteractionPressure = 0f;
            pair.CooldownRemaining = SocialAuraTuning.CooldownAfterEncounter;
            pair.LastEncounterShift = _shiftIndex;

            var snapAB = RelationshipTrajectoryStore.Capture(_world.Relation(idA, idB));
            var snapBA = RelationshipTrajectoryStore.Capture(_world.Relation(idB, idA));
            var log = SocialEncounterResolver.Resolve(_world, a, b, context, why);
            if (log != null)
            {
                pair.PushHistory(log);
                _encountersThisShift[idA] = encA + 1;
                _encountersThisShift[idB] = encB + 1;
                float t = WorkerStateClock.GameHours;
                SocialMemoryRecorder.Record(_world.Memory, log, t);
                SocialRespectApplicator.Apply(_world, log);
                string src = $"S{log.ShiftIndex}:{log.Action}/{log.Response}";
                _world.Trajectory.CommitDelta(log.InitiatorId, log.TargetId,
                    log.InitiatorId == idA ? snapAB : snapBA,
                    _world.Relation(log.InitiatorId, log.TargetId), t, src, _world.Memory);
                _world.Trajectory.CommitDelta(log.TargetId, log.InitiatorId,
                    log.InitiatorId == idA ? snapBA : snapAB,
                    _world.Relation(log.TargetId, log.InitiatorId), t, src, _world.Memory);
            }
            return log;
        }

        void TickOvernightDecay(float gameHoursDelta)
        {
            foreach (var k in _knownPairs)
            {
                int a = (int)(k >> 32);
                int b = (int)(uint)k;
                var pair = _world.Pair(a, b);
                pair.InteractionPressure = Mathf.Max(0f,
                    pair.InteractionPressure
                    - SocialAuraTuning.PressureDecayPerShiftUnit
                      * SocialAuraLiveTuning.OvernightPressureDecayMul
                      * gameHoursDelta);
                pair.CooldownRemaining = Mathf.Max(0f, pair.CooldownRemaining - gameHoursDelta);
            }
            _world.Memory.TickDecay(gameHoursDelta);
        }

        static bool IsCommute(SocialPresenceKind k) =>
            k == SocialPresenceKind.CommutingHome || k == SocialPresenceKind.CommutingToWork;

        public static long PairKey(int a, int b)
        {
            if (a > b) { int t = a; a = b; b = t; }
            return ((long)a << 32) | (uint)b;
        }

        public struct NearbyDebug
        {
            public int OtherId;
            public string OtherName;
            public float Distance;
            public bool Eligible;
            /// <summary>Distance within mutual effective reach (falloff &gt; 0). Independent of eligibility.</summary>
            public bool InsideReach;
            public float Positive;
            public float Negative;
            public float Reach;
            public float Pressure;
            public float Cooldown;
            public SocialContext Context;
            public float Trust;
            public float Warmth;
            public float Hostility;
            public float Respect;
            public bool Exposed;
        }

        public void FillNearbyDebug(
            int focusId,
            IReadOnlyList<WorkerRuntime> crew,
            WorkerPresenceRegistry presence,
            Func<WorkerRuntime, SocialPresenceKind> presenceOf,
            Func<int, JobType> jobOf,
            List<NearbyDebug> into)
        {
            into.Clear();
            if (crew == null || presence == null) return;
            var focus = _world.Get(focusId);
            var focusAv = presence.Get(focusId);
            if (focus == null || focusAv == null) return;
            focus.RefreshExpression();
            Vector2 fp = focusAv.PresencePosition;
            var focusWr = Find(crew, focusId);
            if (focusWr == null) return;

            for (int i = 0; i < crew.Count; i++)
            {
                var wr = crew[i];
                if (wr == null || wr.WorkerId == focusId) continue;
                var av = presence.Get(wr.WorkerId);
                if (av == null) continue;
                var other = _world.Get(wr.WorkerId);
                other?.RefreshExpression();
                var kind = presenceOf(wr);
                bool elig = SocialAuraEligibility.IsEligible(wr, kind);
                float dist = Vector2.Distance(fp, av.PresencePosition);
                float reach = other != null
                    ? SocialAuraLiveTuning.EffectiveReach(other.Expression)
                    : 0f;
                float myReach = SocialAuraLiveTuning.EffectiveReach(focus.Expression);
                float falloff = SocialAuraLiveTuning.DistanceFalloff(dist, myReach, reach);
                var pair = _world.Pair(focusId, wr.WorkerId);
                var rel = _world.Relation(focusId, wr.WorkerId);
                var ctx = SocialContextLive.Derive(
                    focusWr, wr, jobOf(focusId), jobOf(wr.WorkerId),
                    presenceOf(focusWr), kind, WorkerStateClock.GameHours,
                    bothInCamp: false);

                into.Add(new NearbyDebug
                {
                    OtherId = wr.WorkerId,
                    OtherName = wr.DisplayName,
                    Distance = dist,
                    Eligible = elig,
                    InsideReach = falloff > 0.001f,
                    Positive = other?.Expression.Positive ?? 0f,
                    Negative = other?.Expression.Negative ?? 0f,
                    Reach = reach,
                    Pressure = pair.InteractionPressure,
                    Cooldown = pair.CooldownRemaining,
                    Context = ctx,
                    Trust = rel.Trust,
                    Warmth = rel.Warmth,
                    Hostility = rel.Hostility,
                    Respect = rel.Respect,
                    Exposed = falloff > 0.001f && elig,
                });
            }
        }

        static WorkerRuntime Find(IReadOnlyList<WorkerRuntime> crew, int id)
        {
            for (int i = 0; i < crew.Count; i++)
                if (crew[i] != null && crew[i].WorkerId == id) return crew[i];
            return null;
        }
    }
}
