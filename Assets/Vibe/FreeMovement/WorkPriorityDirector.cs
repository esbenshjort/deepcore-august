using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Runner-facing Priority System director: resolve, accrue, station claim hints.
    /// Does not create work — consumes WorkAvailabilityContext probes.
    /// </summary>
    public sealed class WorkPriorityDirector
    {
        public readonly WorkAvailabilityContext Context = new();
        float _evalAccum;
        public const float EvalIntervalGameHours = 0.12f;
        bool _forceEval;

        public void BindHosts(
            FineTerrainWorld world,
            TunnelCollapseSystem collapse,
            MineInfrastructure infra,
            FreeWorkerController excavator,
            HaulerPerson hauler,
            RefinerPerson refiner,
            EngineerPerson engineer,
            ProspectorPerson prospector,
            ProspectorAnomalyAnalyst analyst,
            StewardPerson steward,
            CampLifeState camp,
            WorkerRuntime[] crew,
            System.Func<WorkerRuntime, Vector2> worldPos,
            Vector2 campWorld)
        {
            Context.World = world;
            Context.Collapse = collapse;
            Context.Infra = infra;
            Context.Excavator = excavator;
            Context.Hauler = hauler;
            Context.Refiner = refiner;
            Context.Engineer = engineer;
            Context.Prospector = prospector;
            Context.Analyst = analyst;
            Context.Steward = steward;
            Context.Camp = camp;
            Context.Crew = crew;
            Context.WorldPosOf = worldPos;
            Context.CampWorld = campWorld;
            Context.MealServedTonight = camp != null && camp.MealServedTonight;
        }

        public void RequestImmediateEval() => _forceEval = true;

        public bool ShouldEvaluate(float hoursDelta, WorkerRuntime[] crew = null)
        {
            if (_forceEval)
            {
                _forceEval = false;
                _evalAccum = 0f;
                return true;
            }
            if (crew != null)
            {
                for (int i = 0; i < crew.Length; i++)
                {
                    var p = crew[i]?.Priorities;
                    if (p != null && p.ResolverDirty)
                    {
                        _evalAccum = 0f;
                        return true;
                    }
                }
            }
            _evalAccum += hoursDelta;
            if (_evalAccum < EvalIntervalGameHours) return false;
            _evalAccum = 0f;
            return true;
        }

        public WorkResolveResult ResolveFor(
            WorkerRuntime wr, JobType job, Vector2 pos, float gameHours, bool emergencyOk)
        {
            return WorkPriorityResolver.Resolve(wr, job, pos, Context, gameHours, emergencyOk);
        }

        /// <summary>
        /// Clear voluntary ActiveTask when OFF / unknown. Does not delete world work.
        /// Returns true if active task was cleared due to priority authority.
        /// </summary>
        public bool InvalidateActiveIfNeeded(WorkerRuntime wr)
        {
            if (wr?.Priorities == null) return false;
            var p = wr.Priorities;
            p.LastHostYieldedForOff = false;
            if (string.IsNullOrEmpty(p.ActiveTaskId))
            {
                p.ActiveTaskValid = true;
                return false;
            }

            if (WorkTaskRegistry.Get(p.ActiveTaskId) == null)
            {
                p.ActiveTaskValid = false;
                p.ActiveInvalidationReason = "unknown task";
                p.ClearActiveTask("unknown task");
                p.ResolverDirty = true;
                _forceEval = true;
                return true;
            }

            if (p.IsOff(p.ActiveTaskId))
            {
                p.ActiveTaskValid = false;
                p.ActiveInvalidationReason = "priority OFF";
                p.ClearActiveTask("priority OFF");
                p.ResolverDirty = true;
                _forceEval = true;
                return true;
            }

            if (WorkPriorityResolver.IsHardBlocked(wr, out string block))
            {
                // Pause signal for DEV — do not clear ActiveTaskId
                p.ActiveTaskValid = true;
                p.ActiveInvalidationReason = block;
                return false;
            }

            // Work disappeared while active — invalidate (probes include mid-task cargo/wash)
            // Skip emergency Rescue while duty is active — TickUniversalRescue owns lifecycle
            if (p.ActiveTaskId != WorkerGenericTaskIds.Rescue
                && Context.World != null)
            {
                var avail = Context.Probe(p.ActiveTaskId);
                if (!avail.Available)
                {
                    p.ActiveTaskValid = false;
                    p.ActiveInvalidationReason = "work unavailable: " + (avail.Reason ?? "");
                    p.ClearActiveTask(p.ActiveInvalidationReason);
                    p.ResolverDirty = true;
                    _forceEval = true;
                    return true;
                }
            }

            p.ActiveTaskValid = true;
            p.ActiveInvalidationReason = "";
            return false;
        }

        public void ApplyResolve(WorkerRuntime wr, WorkResolveResult result, float gameHours)
        {
            if (wr?.Priorities == null) return;
            var p = wr.Priorities;
            p.ResolverDirty = false;

            bool currentOff = !string.IsNullOrEmpty(p.ActiveTaskId) && p.IsOff(p.ActiveTaskId);

            if (string.IsNullOrEmpty(result.TaskId))
            {
                p.LastUnavailableReason = result.Reason;
                // Clear stale ActiveTask when resolve finds nothing (or OFF/invalid)
                if (currentOff || !p.ActiveTaskValid || !string.IsNullOrEmpty(p.ActiveTaskId))
                {
                    string why = currentOff ? "priority OFF"
                        : !p.ActiveTaskValid ? p.ActiveInvalidationReason
                        : "no eligible work";
                    p.ClearActiveTask(why);
                    p.LastUnavailableReason = result.Reason;
                }
                return;
            }

            bool changed = p.ActiveTaskId != result.TaskId;
            // Anti-thrash MinCommit only within the SAME priority band.
            // Higher-band work always preempts (P1 vs P2, etc.). OFF already handled above.
            if (changed && !string.IsNullOrEmpty(p.ActiveTaskId) && !currentOff)
            {
                var curP = p.GetPriority(p.ActiveTaskId);
                var nextP = p.GetPriority(result.TaskId);
                bool higherBand = WorkPriorityResolver.IsHigherPriorityBand(nextP, curP);
                var nextDef = WorkTaskRegistry.Get(result.TaskId);
                bool emerg = nextDef != null && nextDef.EmergencyCapable
                             && result.Availability.Urgency01 >= 0.85f;
                float held = gameHours - p.ActiveTaskStartedGameHours;
                if (!higherBand && !emerg
                    && held >= 0f && held < WorkerPriorityPrefs.MinCommitGameHours)
                {
                    p.LastResolveReason = "hold current (anti-thrash same band)";
                    return;
                }
            }

            if (changed || p.ActiveTaskStartedGameHours < 0f)
                p.ActiveTaskStartedGameHours = gameHours;
            p.ActiveTaskId = result.TaskId;
            p.ActiveTaskValid = true;
            p.ActiveInvalidationReason = "";
            p.LastResolveReason = result.Reason;
            p.Suitability01 = result.Suitability01;
            p.LastUnavailableReason = "";
            var br = result.Breakdown;
            p.LastScoreTotal = br.Total;
            p.LastScorePriority = br.Priority;
            p.LastScoreDeficit = br.Deficit;
            p.LastScoreSuit = br.Suitability;
            p.LastScoreDistance = br.Distance;
            p.LastScoreCondition = br.Condition;
            p.LastScoreContinuity = br.Continuity;
            p.LastScoreEmergency = br.Emergency;
            p.LastEmergencyOverride = br.Emergency >= 1f;
            p.LastTargetHint = result.Availability.Reason ?? "";
            if (p.LastWinningPriorityBand <= 0)
                p.LastWinningPriorityBand = (int)p.GetPriority(result.TaskId);
        }

        public void AccrueActive(WorkerRuntime wr, float hoursDelta)
        {
            if (wr?.Priorities == null || hoursDelta <= 0f) return;
            if (string.IsNullOrEmpty(wr.Priorities.ActiveTaskId)) return;
            if (wr.Priorities.IsOff(wr.Priorities.ActiveTaskId)) return;
            wr.Priorities.AccrueWork(wr.Priorities.ActiveTaskId, hoursDelta);
        }

        public static string ActiveTaskShortLabel(WorkerRuntime wr)
        {
            if (wr?.Priorities == null || string.IsNullOrEmpty(wr.Priorities.ActiveTaskId))
                return "";
            var def = WorkTaskRegistry.Get(wr.Priorities.ActiveTaskId);
            return def != null ? def.ShortName : wr.Priorities.ActiveTaskId;
        }
    }
}
