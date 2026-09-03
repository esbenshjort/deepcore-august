using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// DEV-only Social Aura playtest counters. Observes live systems; never changes tuning/math.
    /// Rolling 5 calendar days for human playtest review.
    /// </summary>
    public sealed class SocialAuraPlaytestTracker
    {
        public const int RollingDays = 5;

        public sealed class DayBucket
        {
            public int DayIndex;
            public int TotalEncounters;
            public int WorkEncounters;
            public int CampEncounters;
            public int CommuteEncounters;
            public int CooldownSuppressions;
            public int DialogueShown;
            public int DialogueSuppressed;

            public float PressureSum;
            public int PressureSamples;
            public float PressureMax;

            /// <summary>Pair-hours: each eligible pair inside mutual reach contributes Δh.</summary>
            public float ReachPairHours;

            readonly Dictionary<int, int> _encByWorker = new(8);
            readonly Dictionary<long, int> _encByPair = new(16);
            readonly Dictionary<int, float> _frustSum = new(8);
            readonly Dictionary<int, int> _frustSamples = new(8);

            public IReadOnlyDictionary<int, int> EncountersByWorker => _encByWorker;
            public IReadOnlyDictionary<long, int> EncountersByPair => _encByPair;

            public void AddEncounter(int initId, int targetId)
            {
                AddWorker(initId);
                AddWorker(targetId);
                long pk = SocialAuraLiveSystem.PairKey(initId, targetId);
                if (!_encByPair.ContainsKey(pk)) _encByPair[pk] = 0;
                _encByPair[pk]++;
            }

            void AddWorker(int id)
            {
                if (id <= 0) return;
                if (!_encByWorker.ContainsKey(id)) _encByWorker[id] = 0;
                _encByWorker[id]++;
            }

            public void SamplePressure(float p)
            {
                PressureSum += p;
                PressureSamples++;
                if (p > PressureMax) PressureMax = p;
            }

            public void SampleFrustration(int workerId, float frustration)
            {
                if (workerId <= 0) return;
                if (!_frustSum.ContainsKey(workerId))
                {
                    _frustSum[workerId] = 0f;
                    _frustSamples[workerId] = 0;
                }
                _frustSum[workerId] += frustration;
                _frustSamples[workerId]++;
            }

            public float AvgPressure =>
                PressureSamples > 0 ? PressureSum / PressureSamples : 0f;

            public float AvgFrustration(int workerId)
            {
                if (!_frustSamples.TryGetValue(workerId, out int n) || n <= 0) return 0f;
                return _frustSum[workerId] / n;
            }

            public void Clear()
            {
                DayIndex = 0;
                TotalEncounters = 0;
                WorkEncounters = CampEncounters = CommuteEncounters = 0;
                CooldownSuppressions = 0;
                DialogueShown = DialogueSuppressed = 0;
                PressureSum = 0f;
                PressureSamples = 0;
                PressureMax = 0f;
                ReachPairHours = 0f;
                _encByWorker.Clear();
                _encByPair.Clear();
                _frustSum.Clear();
                _frustSamples.Clear();
            }
        }

        readonly List<DayBucket> _days = new(RollingDays);
        DayBucket _current = new();
        int _activeDay = -1;

        int _cdBaseline;
        int _workBaseline;
        int _campBaseline;
        int _commuteBaseline;

        public DayBucket Current => _current;
        public IReadOnlyList<DayBucket> Days => _days;

        public void Reset(int dayIndex, SocialAuraLiveSystem aura)
        {
            _days.Clear();
            _current = new DayBucket();
            _activeDay = -1;
            BeginDay(dayIndex, aura);
        }

        public void EnsureDay(int dayIndex, SocialAuraLiveSystem aura)
        {
            if (dayIndex == _activeDay) return;
            if (_activeDay > 0 && _current.DayIndex > 0)
                ArchiveCurrent();
            BeginDay(dayIndex, aura);
        }

        void BeginDay(int dayIndex, SocialAuraLiveSystem aura)
        {
            _activeDay = dayIndex;
            _current = new DayBucket { DayIndex = dayIndex };
            _cdBaseline = aura != null ? aura.CooldownSuppressions : 0;
            _workBaseline = aura != null ? aura.WorkAreaEncounters : 0;
            _campBaseline = aura != null ? aura.CampEncounters : 0;
            _commuteBaseline = aura != null ? aura.CommuteEncounters : 0;
        }

        void ArchiveCurrent()
        {
            _days.Add(_current);
            while (_days.Count > RollingDays)
                _days.RemoveAt(0);
        }

        /// <summary>Call each social tick after SocialAuraLiveSystem.Tick (observation only).</summary>
        public void ObserveTick(
            int dayIndex,
            SocialAuraLiveSystem aura,
            IReadOnlyList<WorkerRuntime> crew,
            WorkerPresenceRegistry presence,
            Func<WorkerRuntime, SocialPresenceKind> presenceOf,
            float gameHoursDelta,
            int encountersBefore)
        {
            if (aura == null || crew == null || gameHoursDelta <= 0f) return;
            EnsureDay(dayIndex, aura);

            _current.CooldownSuppressions =
                Mathf.Max(0, aura.CooldownSuppressions - _cdBaseline);
            _current.WorkEncounters =
                Mathf.Max(0, aura.WorkAreaEncounters - _workBaseline);
            _current.CampEncounters =
                Mathf.Max(0, aura.CampEncounters - _campBaseline);
            _current.CommuteEncounters =
                Mathf.Max(0, aura.CommuteEncounters - _commuteBaseline);
            _current.TotalEncounters =
                _current.WorkEncounters + _current.CampEncounters + _current.CommuteEncounters;

            // Attribute new encounters from LiveLogs tail (supports multi-pair ticks)
            int newCount = aura.TotalEncounters - encountersBefore;
            if (newCount > 0)
            {
                var logs = aura.LiveLogs;
                int start = Mathf.Max(0, logs.Count - newCount);
                for (int i = start; i < logs.Count; i++)
                {
                    var log = logs[i];
                    if (log != null)
                        _current.AddEncounter(log.InitiatorId, log.TargetId);
                }
            }

            // Frustration + pressure + reach samples
            int n = crew.Count;
            var pos = new Vector2[n];
            var reach = new float[n];
            var elig = new bool[n];
            var ids = new int[n];

            for (int i = 0; i < n; i++)
            {
                var wr = crew[i];
                if (wr == null) continue;
                ids[i] = wr.WorkerId;
                if (wr.State != null)
                    _current.SampleFrustration(wr.WorkerId, wr.State.Frustration);

                var kind = presenceOf != null ? presenceOf(wr) : SocialPresenceKind.Idle;
                elig[i] = SocialAuraEligibility.IsEligible(wr, kind);
                var av = presence != null ? presence.Get(wr.WorkerId) : null;
                pos[i] = av != null ? av.PresencePosition : Vector2.zero;
                var actor = aura.World.Get(wr.WorkerId);
                if (actor != null)
                {
                    actor.RefreshExpression();
                    reach[i] = SocialAuraLiveTuning.EffectiveReach(actor.Expression);
                }
            }

            for (int i = 0; i < n; i++)
            {
                if (crew[i] == null || !elig[i]) continue;
                for (int j = i + 1; j < n; j++)
                {
                    if (crew[j] == null || !elig[j]) continue;
                    long pk = SocialAuraLiveSystem.PairKey(ids[i], ids[j]);
                    var pair = aura.Pair(ids[i], ids[j]);
                    if (pair != null)
                        _current.SamplePressure(pair.InteractionPressure);

                    float dist = Vector2.Distance(pos[i], pos[j]);
                    float falloff = SocialAuraLiveTuning.DistanceFalloff(dist, reach[i], reach[j]);
                    if (falloff > 0.001f)
                        _current.ReachPairHours += gameHoursDelta;
                }
            }
        }

        public void ObservePresentation(int dayIndex, SocialAuraLiveSystem aura, bool presented)
        {
            EnsureDay(dayIndex, aura);
            if (_current == null || _current.DayIndex <= 0) return;
            if (presented) _current.DialogueShown++;
            else _current.DialogueSuppressed++;
        }

        public string FormatCompactDay(DayBucket d)
        {
            if (d == null) return "";
            var sb = new StringBuilder(256);
            sb.Append($"D{d.DayIndex} enc={d.TotalEncounters} ");
            sb.Append($"W/C/M {d.WorkEncounters}/{d.CampEncounters}/{d.CommuteEncounters} ");
            sb.Append($"cdSup={d.CooldownSuppressions} ");
            sb.Append($"dlg {d.DialogueShown}/{d.DialogueSuppressed} ");
            sb.Append($"PØ={d.AvgPressure:0.00} Pmax={d.PressureMax:0.00} ");
            sb.Append($"reachH={d.ReachPairHours:0.00}");
            return sb.ToString();
        }
    }
}
