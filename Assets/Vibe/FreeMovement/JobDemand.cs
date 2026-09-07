using System;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// V1.2C: activity demand of the JOB right now — not worker suitability.
    /// Normalized 0–1 intensity axes. Idle = zeros.
    /// </summary>
    public readonly struct JobDemandProfile
    {
        public readonly float Physical;
        public readonly float Mental;
        public readonly float Attention;
        public readonly string ActivityLabel;

        public JobDemandProfile(float physical, float mental, float attention, string activityLabel = "")
        {
            Physical = Mathf.Clamp01(physical);
            Mental = Mathf.Clamp01(mental);
            Attention = Mathf.Clamp01(attention);
            ActivityLabel = activityLabel ?? "";
        }

        public static JobDemandProfile Idle => new(0f, 0f, 0f, "IDLE");

        public bool IsIdle => Physical <= 0.001f && Mental <= 0.001f && Attention <= 0.001f;
    }

    /// <summary>Centralized V1.2C demand / recovery / threshold constants.</summary>
    public static class JobDemandTuning
    {
        // ——— Continuous rates (per game-hour at demand axis = 1.0) ———
        /// <summary>PhysicalStamina points drained per game-hour at Physical=1 (non-Excavator).</summary>
        public const float PhysicalSpendPerGameHour = 14f;
        public const float MentalFatigueGainPerGameHour = 9f;
        public const float FocusDrainPerGameHour = 7f;

        // Idle / rest recovery per game-hour
        public const float IdlePhysicalRecoverPerGameHour = 11f;
        public const float RestPhysicalRecoverPerGameHour = 22f;
        public const float IdleMentalRecoverPerGameHour = 4.5f;
        public const float RestMentalRecoverPerGameHour = 8f;
        public const float IdleFocusLerpPerGameHour = 0.10f;
        public const float RestFocusLerpPerGameHour = 0.18f;
        public const float FocusBaseline = 62f;

        // Exhaustion latch (PhysicalStamina ratio)
        public const float ExhaustionEnterRatio = 0.10f;
        public const float ExhaustionClearRatio = 0.70f;

        // Debug / soft consequence thresholds
        public const float HighMentalFatigue = 70f;
        public const float LowFocusState = 40f;
        public const float TiredPhysicalRatio = 0.30f;

        // Soft FocusState influence on attention-sensitive dig finesse (not a global DPS mul)
        public const float LowFocusFinessePenaltyMax = 2.5f;
    }

    /// <summary>
    /// Resolve current activity demand from live host FSM — assignment alone is not enough.
    /// </summary>
    public static class JobDemandCatalog
    {
        public static JobDemandProfile ForExcavation(FreeWorkerController host)
        {
            if (host == null || host.AssignedWorker == null) return JobDemandProfile.Idle;
            if (host.IsStaminaResting) return new JobDemandProfile(0f, 0f, 0f, "RESTING");
            if (host.IsOverheated) return new JobDemandProfile(0f, 0.1f, 0.25f, "OVERHEAT_WAIT");
            if (host.IsCooling) return new JobDemandProfile(0f, 0.08f, 0.22f, "COOLING");
            // Physical handled by dig SpendStamina — continuous Physical demand = 0 here
            if (host.IsActivelyDigging)
                return new JobDemandProfile(0f, 0.12f, 0.40f, "MINING");
            return new JobDemandProfile(0f, 0.04f, 0.12f, "EXCAV_IDLE");
        }

        public static JobDemandProfile ForProspecting(ProspectorPerson host)
        {
            if (host == null || host.AssignedWorker == null) return JobDemandProfile.Idle;
            if (host.WorkMode == ProspectorWorkMode.Investigate)
            {
                if (host.Investigation == null)
                    return new JobDemandProfile(0.1f, 0.25f, 0.30f, "INVESTIGATE");
                var st = host.Investigation.State;
                switch (st)
                {
                    case ProspectorInvestigationState.Analysing:
                        return new JobDemandProfile(0.05f, 0.95f, 0.90f, "ANALYSING");
                    case ProspectorInvestigationState.InspectingRock:
                    case ProspectorInvestigationState.ConsultingRefiner:
                        return new JobDemandProfile(0.15f, 0.55f, 0.65f, "FIELD_INSPECT");
                    case ProspectorInvestigationState.WalkingToExcavator:
                    case ProspectorInvestigationState.WalkingToLooseRock:
                    case ProspectorInvestigationState.WalkingToRefiner:
                    case ProspectorInvestigationState.ReturningToAnalysis:
                        return new JobDemandProfile(0.35f, 0.20f, 0.30f, "FIELD_TRAVEL");
                    default:
                        return new JobDemandProfile(0.1f, 0.25f, 0.30f, "INVESTIGATE");
                }
            }

            if (host.HasScannerAssignment)
            {
                // Actively placing/operating scanner — light physical + attention
                return new JobDemandProfile(0.25f, 0.20f, 0.45f, "SCANNER_OPS");
            }

            if (host.WorkMode == ProspectorWorkMode.SurveyNearby
                || host.WorkMode == ProspectorWorkMode.AssistExcavator)
                return new JobDemandProfile(0.30f, 0.25f, 0.40f, "SURVEY");

            return new JobDemandProfile(0.05f, 0.08f, 0.10f, "PROSPECT_IDLE");
        }

        public static JobDemandProfile ForHauling(HaulerPerson host)
        {
            if (host == null || host.AssignedWorker == null) return JobDemandProfile.Idle;
            string act = host.ActivityLabel ?? "";
            if (act.IndexOf("LOAD", StringComparison.OrdinalIgnoreCase) >= 0
                || act.IndexOf("HAUL", StringComparison.OrdinalIgnoreCase) >= 0
                || act.IndexOf("UNLOAD", StringComparison.OrdinalIgnoreCase) >= 0)
                return new JobDemandProfile(0.85f, 0.15f, 0.35f, act);
            if (act.IndexOf("SEEK", StringComparison.OrdinalIgnoreCase) >= 0)
                return new JobDemandProfile(0.45f, 0.20f, 0.45f, act);
            return new JobDemandProfile(0.1f, 0.05f, 0.1f, act);
        }

        public static JobDemandProfile ForRefining(RefinerPerson host)
        {
            if (host == null || host.AssignedWorker == null) return JobDemandProfile.Idle;
            if (host.IsInConsultation)
                return new JobDemandProfile(0.05f, 0.50f, 0.55f, "CONSULT");
            string act = host.ActivityLabel ?? "";
            if (act.IndexOf("FETCH", StringComparison.OrdinalIgnoreCase) >= 0
                || act.IndexOf("CARRY", StringComparison.OrdinalIgnoreCase) >= 0)
                return new JobDemandProfile(0.35f, 0.35f, 0.40f, act);
            if (act.IndexOf("WAIT", StringComparison.OrdinalIgnoreCase) >= 0)
                // Washer runs mechanically — light attention only while operator waits
                return new JobDemandProfile(0.05f, 0.15f, 0.30f, act);
            return new JobDemandProfile(0.05f, 0.08f, 0.08f, "REFINE_IDLE");
        }

        public static JobDemandProfile ForEngineering(EngineerPerson host)
        {
            if (host == null || host.AssignedWorker == null) return JobDemandProfile.Idle;
            if (host.IsRepairing)
                return new JobDemandProfile(0.55f, 0.70f, 0.65f, "REPAIRING");
            if (host.IsInfrastructureWork)
                return new JobDemandProfile(0.50f, 0.45f, 0.50f, "INFRA");
            if (host.IsEnRoute)
                return new JobDemandProfile(0.40f, 0.20f, 0.30f, "EN_ROUTE");
            return new JobDemandProfile(0.05f, 0.08f, 0.08f, "ENG_IDLE");
        }

        public static JobDemandProfile ForSteward(StewardPerson host)
        {
            if (host == null || host.AssignedWorker == null) return JobDemandProfile.Idle;
            return host.WorkKind switch
            {
                StewardWorkKind.PreparingMeal => new JobDemandProfile(0.25f, 0.35f, 0.45f, "PREP_MEAL"),
                StewardWorkKind.TendingWounds => new JobDemandProfile(0.20f, 0.40f, 0.50f, "TEND"),
                StewardWorkKind.CleaningCamp => new JobDemandProfile(0.40f, 0.15f, 0.25f, "CLEAN"),
                StewardWorkKind.AfterMealCleanup => new JobDemandProfile(0.35f, 0.12f, 0.20f, "CLEANUP"),
                StewardWorkKind.KitchenDuty => new JobDemandProfile(0.30f, 0.25f, 0.35f, "KITCHEN"),
                _ => new JobDemandProfile(0.08f, 0.08f, 0.10f, "STEWARD_IDLE"),
            };
        }
    }

    /// <summary>
    /// Applies job demand / idle recovery to person WorkerState.
    /// Excavator dig stamina remains dig-cost based — do not double-spend Physical here while mining.
    /// </summary>
    public static class WorkerJobDemand
    {
        public static void EnsureStaminaPrimed(WorkerRuntime wr)
        {
            if (wr?.State == null || wr.Stats == null) return;
            if (wr.State.StaminaPrimed) return;
            float max = wr.PhysicalStaminaMax;
            wr.State.SetStamina(max, max);
            wr.State.IsResting = false;
            wr.State.StaminaPrimed = true;
            wr.State.ExhaustionLatched = false;
        }

        public static float StaminaRatio(WorkerRuntime wr)
        {
            if (wr?.State == null) return 1f;
            float max = wr.PhysicalStaminaMax;
            if (max <= 0.001f) return 1f;
            if (!wr.State.StaminaPrimed) return 1f; // uninitialized ≠ exhausted
            return Mathf.Clamp01(wr.State.PhysicalStamina / max);
        }

        public static void TickActive(
            WorkerRuntime wr,
            JobDemandProfile demand,
            float gameHours,
            JobType job,
            string providerId)
        {
            if (wr?.State == null || gameHours <= 0f) return;
            EnsureStaminaPrimed(wr);
            var st = wr.State;
            var stats = wr.Stats;

            int focusStat = stats != null ? stats.Get(WorkerStatId.Focus) : WorkerStats.Baseline;
            int recovery = stats != null ? stats.Get(WorkerStatId.Recovery) : WorkerStats.Baseline;

            // Physical (non-zero only when catalog asks — Excavation mining uses 0)
            if (demand.Physical > 0.001f)
            {
                float spend = JobDemandTuning.PhysicalSpendPerGameHour * demand.Physical * gameHours;
                // Recovery trait slightly reduces continuous drain
                spend *= Mathf.Clamp(1.05f - recovery * 0.01f, 0.75f, 1.05f);
                st.SpendStamina(spend);
                MaybeFireExhaustion(wr, job, providerId);
            }

            // Mental fatigue — Focus capacity resists slightly (attention endurance), not WorkRate blanket
            if (demand.Mental > 0.001f)
            {
                float resist = 1f - (focusStat - 10) * 0.012f;
                resist = Mathf.Clamp(resist, 0.75f, 1.15f);
                float gain = JobDemandTuning.MentalFatigueGainPerGameHour * demand.Mental * gameHours * resist;
                st.MentalFatigue = st.MentalFatigue + gain;
            }

            // FocusState: attention demand drains; high MentalFatigue accelerates; static Focus resists
            if (demand.Attention > 0.001f || st.MentalFatigue > 50f)
            {
                float drain = JobDemandTuning.FocusDrainPerGameHour * demand.Attention * gameHours;
                float fatiguePressure = Mathf.Max(0f, st.MentalFatigue - 40f) * 0.02f * gameHours;
                float resist = 1f - (focusStat - 10) * 0.015f;
                resist = Mathf.Clamp(resist, 0.7f, 1.2f);
                st.FocusState = st.FocusState - (drain + fatiguePressure) * resist;
            }
        }

        public static void TickIdleOrRest(WorkerRuntime wr, float gameHours, bool resting)
        {
            if (wr?.State == null || gameHours <= 0f) return;
            EnsureStaminaPrimed(wr);
            var st = wr.State;
            float max = wr.PhysicalStaminaMax;

            float physRate = resting
                ? JobDemandTuning.RestPhysicalRecoverPerGameHour
                : JobDemandTuning.IdlePhysicalRecoverPerGameHour;
            st.AddStamina(physRate * gameHours, max);

            float mentRate = resting
                ? JobDemandTuning.RestMentalRecoverPerGameHour
                : JobDemandTuning.IdleMentalRecoverPerGameHour;
            st.MentalFatigue = st.MentalFatigue - mentRate * gameHours;

            float focusLerp = resting
                ? JobDemandTuning.RestFocusLerpPerGameHour
                : JobDemandTuning.IdleFocusLerpPerGameHour;
            st.FocusState = Mathf.Lerp(st.FocusState, JobDemandTuning.FocusBaseline,
                Mathf.Clamp01(focusLerp * gameHours));

            // Clear exhaustion latch when recovered
            if (StaminaRatio(wr) >= JobDemandTuning.ExhaustionClearRatio)
            {
                st.ExhaustionLatched = false;
                if (resting && StaminaRatio(wr) >= FreeWorkerController.StaminaRestResumeRatio)
                    st.IsResting = false;
            }
        }

        static void MaybeFireExhaustion(WorkerRuntime wr, JobType job, string providerId)
        {
            var st = wr.State;
            float ratio = StaminaRatio(wr);
            if (ratio > JobDemandTuning.ExhaustionEnterRatio)
                return;
            if (st.ExhaustionLatched) return;
            st.ExhaustionLatched = true;
            st.IsResting = true;
            WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                wr.WorkerId,
                WorkerStateEventType.PhysicalExhaustion,
                5f,
                "StaminaThreshold",
                job,
                providerId));
        }

        /// <summary>
        /// Soft finesse penalty from low FocusState (0–LowFocusFinessePenaltyMax).
        /// Does not replace Frustration/Focus-stat excavator formulas.
        /// </summary>
        public static float LowFocusFinessePenalty(WorkerState st)
        {
            if (st == null) return 0f;
            if (st.FocusState >= JobDemandTuning.LowFocusState) return 0f;
            float t = (JobDemandTuning.LowFocusState - st.FocusState) / JobDemandTuning.LowFocusState;
            return Mathf.Clamp01(t) * JobDemandTuning.LowFocusFinessePenaltyMax;
        }

        /// <summary>Hauler move mul when tired/exhausted (behavioural, not stacked DPS).</summary>
        public static float PhysicalMoveMul(WorkerRuntime wr)
        {
            float r = StaminaRatio(wr);
            if (r >= JobDemandTuning.TiredPhysicalRatio) return 1f;
            if (r <= JobDemandTuning.ExhaustionEnterRatio) return 0.82f;
            float t = (JobDemandTuning.TiredPhysicalRatio - r)
                      / (JobDemandTuning.TiredPhysicalRatio - JobDemandTuning.ExhaustionEnterRatio);
            return Mathf.Lerp(1f, 0.88f, Mathf.Clamp01(t));
        }
    }
}
