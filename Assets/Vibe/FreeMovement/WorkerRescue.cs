using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    public enum RescueMissionPhase : byte
    {
        None = 0,
        Approach = 1,
        Assist = 2,
        Carry = 3,
        Complete = 4,
    }

    /// <summary>
    /// Active rescue mission. V1: one primary rescuer; contributor list reserved for cooperation.
    /// </summary>
    public sealed class RescueMission
    {
        public int MissionId;
        public int RescuerId;
        public int CasualtyId;
        public RescueMissionPhase Phase;
        public float AssistHoursLeft;
        public float StartedGameHours;
        public string LastStatus = "";
        /// <summary>Future multi-worker contributions (clear access / assist carry). V1 unused.</summary>
        public readonly List<int> ContributorIds = new(4);
    }

    /// <summary>
    /// Universal rescue — any mobile worker may accept. JobType is never a permission gate.
    /// Performance uses existing WorkerStats only. Path/debris authority stays on FineTerrainWorld.
    /// </summary>
    public static class WorkerRescue
    {
        public static string TaskId => WorkerGenericTaskIds.Rescue;

        /// <summary>Person can leave specialization and take a rescue duty (no JobType check).</summary>
        public static bool CanAcceptRescueDuty(WorkerRuntime wr)
        {
            if (wr == null || !wr.IsAlive || wr.State == null) return false;
            if (wr.State.Incapacitated) return false;
            if (!InjuryResponse.CanIndependentWalk(wr)) return false;
            if (WorkerInjuryConsequences.ForcesOutOfWork(wr.Injuries)) return false;
            var status = InjuryResponse.EvaluateWorkStatus(wr);
            if (status == WorkerInjuryWorkStatus.Incapacitated
                || status == WorkerInjuryWorkStatus.OffDuty)
                return false;
            if (wr.CampBody == null) return false;
            if (wr.CampBody.RescueDutyActive || wr.CampBody.BeingRescued) return false;
            if (wr.CampBody.InjuryReturnActive || wr.CampBody.ToiletTripActive) return false;
            // Steward / care seekers may still leave camp to rescue (Kit must be eligible)
            return true;
        }

        /// <summary>Casualty needs physical recovery by another worker.</summary>
        public static bool NeedsRescue(WorkerRuntime wr)
        {
            if (wr == null || !wr.IsAlive || wr.State == null) return false;
            if (wr.CampBody != null && wr.CampBody.BeingRescued) return true;
            return wr.State.Incapacitated;
        }

        /// <summary>Approach / difficult footing — Agility, Balance, Focus, SafetyProtocol.</summary>
        public static float ApproachSpeedMul(WorkerRuntime wr)
        {
            if (wr?.Stats == null) return 0.85f;
            float agi = wr.Stats.Get(WorkerStatId.Agility) / 20f;
            float bal = wr.Stats.Get(WorkerStatId.Balance) / 20f;
            float foc = wr.Stats.Get(WorkerStatId.Focus) / 20f;
            float saf = wr.Stats.Get(WorkerStatId.SafetyProtocol) / 20f;
            float mul = Mathf.Lerp(0.62f, 1.22f, agi * 0.35f + bal * 0.25f + foc * 0.2f + saf * 0.2f);
            mul *= WorkerInjuryConsequences.WalkSpeedMul(wr.Injuries);
            return mul;
        }

        /// <summary>Carry/drag — HeavyLifting, RawPower, Stamina, Toughness; Empathy/Composure soften handling.</summary>
        public static float CarrySpeedMul(WorkerRuntime wr)
        {
            if (wr?.Stats == null) return TunnelCollapseSystem.RescueCarrySpeedMul;
            float lift = wr.Stats.Get(WorkerStatId.HeavyLifting) / 20f;
            float pow = wr.Stats.Get(WorkerStatId.RawPower) / 20f;
            float stam = wr.Stats.Get(WorkerStatId.Stamina) / 20f;
            float tough = wr.Stats.Get(WorkerStatId.Toughness) / 20f;
            float emp = wr.Stats.Get(WorkerStatId.Empathy) / 20f;
            float com = wr.Stats.Get(WorkerStatId.Composure) / 20f;
            float body = lift * 0.34f + pow * 0.28f + stam * 0.2f + tough * 0.18f;
            float care = emp * 0.55f + com * 0.45f;
            float mul = TunnelCollapseSystem.RescueCarrySpeedMul
                        * Mathf.Lerp(0.55f, 1.35f, body)
                        * Mathf.Lerp(0.9f, 1.08f, care);
            mul *= WorkerInjuryConsequences.LoadCarryMul(wr.Injuries);
            mul *= WorkerInjuryConsequences.ManualWorkMul(wr.Injuries);
            return mul;
        }

        /// <summary>Stabilize/prepare casualty before haul — Empathy, Composure, Focus, Mechanics (equipment/props).</summary>
        public static float AssistDurationHours(WorkerRuntime wr)
        {
            float baseH = TunnelCollapseSystem.RescueAssistHours;
            if (wr?.Stats == null) return baseH;
            float emp = wr.Stats.Get(WorkerStatId.Empathy) / 20f;
            float com = wr.Stats.Get(WorkerStatId.Composure) / 20f;
            float foc = wr.Stats.Get(WorkerStatId.Focus) / 20f;
            float mech = wr.Stats.Get(WorkerStatId.Mechanics) / 20f;
            float skill = emp * 0.35f + com * 0.3f + foc * 0.2f + mech * 0.15f;
            return baseH * Mathf.Lerp(1.25f, 0.65f, skill);
        }

        public static float StaminaTaxPerGameHour(WorkerRuntime wr, bool carrying)
        {
            float baseTax = carrying ? 5.5f : 3.2f;
            if (wr?.Stats == null) return baseTax;
            float stam = wr.Stats.Get(WorkerStatId.Stamina) / 20f;
            float tough = wr.Stats.Get(WorkerStatId.Toughness) / 20f;
            return baseTax * Mathf.Lerp(1.25f, 0.7f, stam * 0.55f + tough * 0.45f);
        }

        /// <summary>
        /// Candidate score for accepting a rescue need. Higher = better.
        /// Distance and stats only — never JobType.
        /// </summary>
        public static float ScoreCandidate(
            WorkerRuntime wr,
            Vector2 rescuerPos,
            Vector2 casualtyPos,
            bool openPathToCasualty)
        {
            if (!CanAcceptRescueDuty(wr)) return -1f;
            float dist = Vector2.Distance(rescuerPos, casualtyPos);
            float distScore = Mathf.Clamp(14f - dist, 0f, 14f);
            float perf = CarrySpeedMul(wr) / Mathf.Max(0.01f, TunnelCollapseSystem.RescueCarrySpeedMul);
            float pathBonus = openPathToCasualty ? 4f : 0.5f;
            // Mild job familiarity nudge — never a gate
            float jobNudge = 0f;
            return distScore + perf * 3f + pathBonus + jobNudge;
        }

        /// <summary>Optional familiarity nudge from current assignment (efficiency only).</summary>
        public static float JobFamiliarityNudge(JobType job) => job switch
        {
            JobType.Hauling => 0.35f,
            JobType.Engineering => 0.25f,
            JobType.Steward => 0.2f,
            JobType.Excavation => 0.1f,
            _ => 0f,
        };

        public static float ScoreCandidate(
            WorkerRuntime wr,
            JobType currentJob,
            Vector2 rescuerPos,
            Vector2 casualtyPos,
            bool openPathToCasualty)
        {
            float s = ScoreCandidate(wr, rescuerPos, casualtyPos, openPathToCasualty);
            if (s < 0f) return s;
            return s + JobFamiliarityNudge(currentJob);
        }
    }
}
