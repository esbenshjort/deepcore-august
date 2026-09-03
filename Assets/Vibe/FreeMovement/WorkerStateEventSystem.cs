using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Per-person rolling event history (debug + future Social Aura context).
    /// Cap keeps stories short; not a telemetry DB.
    /// </summary>
    [Serializable]
    public sealed class WorkerStateEventHistory
    {
        public const int Cap = 24;

        readonly List<WorkerStateEventRecord> _items = new(Cap);

        public IReadOnlyList<WorkerStateEventRecord> Items => _items;

        public void Add(WorkerStateEventRecord record)
        {
            if (record == null) return;
            _items.Add(record);
            while (_items.Count > Cap)
                _items.RemoveAt(0);
        }

        public void Clear() => _items.Clear();
    }

    /// <summary>
    /// Spam gate: meaningful cadence only. Continuous math may still mutate without events.
    /// </summary>
    public sealed class WorkerStateEventSpamGate
    {
        readonly Dictionary<long, float> _lastHours = new(64);

        static long Key(int workerId, WorkerStateEventType type, string source)
        {
            unchecked
            {
                int s = source != null ? source.GetHashCode() : 0;
                return ((long)workerId << 40) ^ ((long)type << 24) ^ (uint)s;
            }
        }

        /// <summary>Minimum game-hours between identical (worker, type, source) emits.</summary>
        public static float MinGapHours(WorkerStateEventType type) => type switch
        {
            WorkerStateEventType.WorkBlocked => 0.08f,       // ~1 real minute at 12s/h
            WorkerStateEventType.RepeatedFailure => 0.15f,
            WorkerStateEventType.ProgressSuccess => 0.04f,
            WorkerStateEventType.PhysicalExhaustion => 0.5f,
            WorkerStateEventType.EquipmentProblem => 0.25f,
            WorkerStateEventType.EquipmentRecovered => 0.1f,
            WorkerStateEventType.Discovery => 0.05f,
            WorkerStateEventType.Injury => 0.2f,
            WorkerStateEventType.MajorSuccess => 0.1f,
            WorkerStateEventType.InvestigationFailure => 0.2f,
            _ => 0.1f,
        };

        public bool TryAdmit(WorkerStateEvent e)
        {
            if (e == null || e.WorkerId <= 0) return false;
            long k = Key(e.WorkerId, e.EventType, e.Source);
            if (_lastHours.TryGetValue(k, out float last)
                && e.GameHours - last < MinGapHours(e.EventType))
                return false;
            _lastHours[k] = e.GameHours;
            return true;
        }

        public void Clear() => _lastHours.Clear();
    }

    /// <summary>
    /// Universal person-state reactions. Jobs emit; this applies to WorkerRuntime.State.
    /// </summary>
    public static class WorkerStateEventProcessor
    {
        public static WorkerStateEventRecord Apply(WorkerStateEvent e, WorkerRuntime wr)
        {
            if (e == null || wr == null || wr.State == null) return null;
            if (e.WorkerId != wr.WorkerId) return null;

            var st = wr.State;
            var stats = wr.Stats;
            float frBefore = st.Frustration;
            float moBefore = st.Morale;
            float mfBefore = st.MentalFatigue;
            float fsBefore = st.FocusState;

            int determination = stats != null ? stats.Get(WorkerStatId.Determination) : WorkerStats.Baseline;
            int composure = stats != null ? stats.Get(WorkerStatId.Composure) : WorkerStats.Baseline;
            int focus = stats != null ? stats.Get(WorkerStatId.Focus) : WorkerStats.Baseline;

            switch (e.EventType)
            {
                case WorkerStateEventType.WorkBlocked:
                    ApplyFrustrationGain(st, BaseGain(e.Magnitude), determination, composure, acute: false);
                    break;

                case WorkerStateEventType.RepeatedFailure:
                    ApplyFrustrationGain(st, BaseGain(e.Magnitude) * 1.25f, determination, composure, acute: true);
                    if (e.Magnitude >= 6f)
                        st.Morale = st.Morale - Mathf.Clamp(0.4f + e.Magnitude * 0.05f, 0.4f, 2.5f);
                    break;

                case WorkerStateEventType.ProgressSuccess:
                {
                    float relief = BaseGain(e.Magnitude) + focus * 0.05f;
                    // Frequent micro-progress must not erase larger setback residue.
                    relief = Mathf.Min(relief * 0.28f, 0.55f);
                    st.ReduceFrustration(relief);
                    break;
                }

                case WorkerStateEventType.MajorSuccess:
                case WorkerStateEventType.Discovery:
                {
                    float relief = BaseGain(e.Magnitude) + focus * 0.05f;
                    st.ReduceFrustration(relief);
                    float moraleGain = Mathf.Clamp(1.2f + e.Magnitude * 0.15f, 1.2f, 5f);
                    // Determination slightly amplifies positive outlook
                    moraleGain *= 0.85f + determination * 0.015f;
                    st.Morale = st.Morale + moraleGain;
                    st.FocusState = Mathf.Lerp(st.FocusState, 70f, 0.08f);
                    break;
                }

                case WorkerStateEventType.EquipmentProblem:
                    ApplyFrustrationGain(st, BaseGain(e.Magnitude) * 1.1f, determination, composure, acute: true);
                    break;

                case WorkerStateEventType.EquipmentRecovered:
                    st.ReduceFrustration(BaseGain(e.Magnitude) + 1f);
                    st.Morale = st.Morale + Mathf.Clamp(0.8f + e.Magnitude * 0.08f, 0.8f, 3f);
                    break;

                case WorkerStateEventType.Injury:
                    ApplyFrustrationGain(st, BaseGain(e.Magnitude) * 1.15f, determination, composure, acute: true);
                    if (e.Magnitude >= 15f || st.NeedsCare)
                        st.Morale = st.Morale - Mathf.Clamp(1.5f + e.Magnitude * 0.04f, 1.5f, 6f);
                    else
                        st.Morale = st.Morale - Mathf.Clamp(0.6f + e.Magnitude * 0.03f, 0.6f, 3f);
                    break;

                case WorkerStateEventType.PhysicalExhaustion:
                    st.MentalFatigue = st.MentalFatigue + Mathf.Clamp(e.Magnitude * 0.35f, 1f, 6f);
                    st.FocusState = st.FocusState - Mathf.Clamp(e.Magnitude * 0.25f, 1f, 5f);
                    ApplyFrustrationGain(st, Mathf.Max(0.5f, e.Magnitude * 0.2f), determination, composure, acute: false);
                    break;

                case WorkerStateEventType.InvestigationFailure:
                    ApplyFrustrationGain(st, BaseGain(e.Magnitude), determination, composure, acute: false);
                    st.Morale = st.Morale - Mathf.Clamp(0.5f + e.Magnitude * 0.06f, 0.5f, 3f);
                    break;
            }

            var record = new WorkerStateEventRecord
            {
                Event = e,
                DeltaFrustration = st.Frustration - frBefore,
                DeltaMorale = st.Morale - moBefore,
                DeltaMentalFatigue = st.MentalFatigue - mfBefore,
                DeltaFocusState = st.FocusState - fsBefore,
            };
            wr.EventHistory.Add(record);
            return record;
        }

        static float BaseGain(float magnitude) => Mathf.Max(0.25f, magnitude);

        /// <summary>
        /// Determination resists setback gain; Composure dampens acute spikes.
        /// Mirrors Excavator obstruction: Max(0.5, base − Determination×0.10).
        /// </summary>
        static void ApplyFrustrationGain(
            WorkerState st, float baseGain, int determination, int composure, bool acute)
        {
            float resistance = determination * 0.10f;
            float gained = Mathf.Max(0.5f, baseGain - resistance);
            if (acute)
            {
                float composureCut = composure * 0.06f;
                gained = Mathf.Max(0.35f, gained - composureCut);
            }
            st.AddFrustration(gained);
        }
    }

    /// <summary>
    /// Emit → spam gate → resolve WorkerId → process.
    /// Sleeping workers still process (person owns residue); reported in V1.2B audit.
    /// </summary>
    public sealed class WorkerStateEventService
    {
        readonly WorkerStateEventSpamGate _gate = new();
        Func<int, WorkerRuntime> _resolve;

        public WorkerStateEventSpamGate Gate => _gate;

        public void Bind(Func<int, WorkerRuntime> resolveWorker) =>
            _resolve = resolveWorker;

        public WorkerStateEventRecord Emit(WorkerStateEvent e)
        {
            if (e == null || e.WorkerId <= 0) return null;
            if (e.GameHours <= 0f && WorkerStateClock.GameHours > 0f)
                e.GameHours = WorkerStateClock.GameHours;
            if (!_gate.TryAdmit(e)) return null;
            var wr = _resolve?.Invoke(e.WorkerId);
            if (wr == null) return null;
            return WorkerStateEventProcessor.Apply(e, wr);
        }

        /// <summary>Bypass spam gate — audits / forced story beats.</summary>
        public WorkerStateEventRecord EmitForced(WorkerStateEvent e)
        {
            if (e == null || e.WorkerId <= 0) return null;
            if (e.GameHours <= 0f && WorkerStateClock.GameHours > 0f)
                e.GameHours = WorkerStateClock.GameHours;
            var wr = _resolve?.Invoke(e.WorkerId);
            if (wr == null) return null;
            return WorkerStateEventProcessor.Apply(e, wr);
        }

        public void ClearGate() => _gate.Clear();
    }

    /// <summary>Conservative daytime meter drift — not job demand (V1.2C).</summary>
    public static class WorkerStateDaytimeRecovery
    {
        /// <summary>Frustration points removed per game-hour when idle of pressure.</summary>
        public const float FrustrationDecayPerGameHour = 0.18f;

        /// <summary>FocusState lerp toward baseline per game-hour.</summary>
        public const float FocusStateLerpPerGameHour = 0.06f;

        public const float FocusStateBaseline = 62f;

        public static void Tick(WorkerRuntime wr, float gameHoursDelta)
        {
            if (wr?.State == null || gameHoursDelta <= 0f) return;
            var st = wr.State;
            st.ReduceFrustration(FrustrationDecayPerGameHour * gameHoursDelta);
            float t = Mathf.Clamp01(FocusStateLerpPerGameHour * gameHoursDelta);
            st.FocusState = Mathf.Lerp(st.FocusState, FocusStateBaseline, t);
            // Morale: no aggressive continuous drift
        }
    }
}
