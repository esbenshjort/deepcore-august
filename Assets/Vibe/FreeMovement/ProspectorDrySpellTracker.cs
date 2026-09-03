using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Conservative Prospector dry-spell signal: prolonged lack of Discovery,
    /// not per-scan / per-hour spam. Emits InvestigationFailure via WorkerStateEventHub.
    /// Dry time accumulates OnShift hours only — sleep / off-shift do not advance thresholds.
    /// </summary>
    public sealed class ProspectorDrySpellTracker
    {
        /// <summary>OnShift game-hours without Discovery before the first signal.</summary>
        public const float FirstSignalAfterOnShiftHours = 32f;

        /// <summary>OnShift game-hours between subsequent dry-spell signals while still dry.</summary>
        public const float RepeatGapOnShiftHours = 20f;

        /// <summary>Obsolete name — use <see cref="FirstSignalAfterOnShiftHours"/>.</summary>
        public const float FirstSignalAfterGameHours = FirstSignalAfterOnShiftHours;

        /// <summary>Obsolete name — use <see cref="RepeatGapOnShiftHours"/>.</summary>
        public const float RepeatGapGameHours = RepeatGapOnShiftHours;

        public const float EventMagnitude = 8f;
        public const string EventSource = "ProspectorDrySpell";

        float _dryOnShiftHours;
        float _onShiftSinceLastEmit;
        bool _emittedOnce;
        int _emitCount;

        public int EmitCount => _emitCount;
        public float DryOnShiftHours => _dryOnShiftHours;
        public float OnShiftSinceLastEmit => _onShiftSinceLastEmit;

        /// <summary>Clear dry accumulation and repeat timer (bootstrap / new crew).</summary>
        public void Reset(float unusedCalendarHours = 0f)
        {
            _dryOnShiftHours = 0f;
            _onShiftSinceLastEmit = 0f;
            _emittedOnce = false;
            _emitCount = 0;
        }

        /// <summary>Real Discovery — clears dry time and repeat timer.</summary>
        public void NotifyDiscovery(float unusedCalendarHours = 0f)
        {
            _dryOnShiftHours = 0f;
            _onShiftSinceLastEmit = 0f;
            _emittedOnce = false;
        }

        /// <summary>
        /// Advance only with OnShift hours while actively Prospecting.
        /// Pass the OnShift delta for this frame/step — never calendar jumps across sleep.
        /// </summary>
        public WorkerStateEventRecord Tick(
            int prospectorWorkerId,
            float onShiftHoursDelta,
            string providerId = "")
        {
            if (prospectorWorkerId <= 0 || onShiftHoursDelta <= 0f)
                return null;

            _dryOnShiftHours += onShiftHoursDelta;
            _onShiftSinceLastEmit += onShiftHoursDelta;

            if (!_emittedOnce)
            {
                if (_dryOnShiftHours < FirstSignalAfterOnShiftHours)
                    return null;
            }
            else if (_onShiftSinceLastEmit < RepeatGapOnShiftHours)
            {
                return null;
            }

            var rec = WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                prospectorWorkerId,
                WorkerStateEventType.InvestigationFailure,
                EventMagnitude,
                EventSource,
                JobType.Prospecting,
                providerId ?? ""));
            if (rec != null)
            {
                _onShiftSinceLastEmit = 0f;
                _emittedOnce = true;
                _emitCount++;
            }
            return rec;
        }
    }
}
