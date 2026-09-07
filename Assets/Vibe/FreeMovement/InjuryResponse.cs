using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Work-function status derived from existing typed injuries + WorkerState.</summary>
    public enum WorkerInjuryWorkStatus : byte
    {
        Fit = 0,
        Limited = 1,
        OffDuty = 2,
        Incapacitated = 3,
    }

    /// <summary>
    /// Injury Response V1 — person-owned decisions on existing WorkerInjury / WorkerState /
    /// locomotion / Steward care. Not a parallel health system.
    /// </summary>
    public static class InjuryResponse
    {
        /// <summary>Extra Frustration when already carrying injuries (once per new wound).</summary>
        public static float RepeatedInjuryExtraFrustration(WorkerInjuryStore store)
        {
            if (store == null) return 0f;
            int n = store.Count;
            if (n >= 3) return 7f;
            if (n >= 2) return 4f;
            return 0f;
        }

        /// <summary>Frustration magnitude fed into Injury state events (before processor mul).</summary>
        public static float FrustrationEventMagnitude(WorkerInjuryRecord rec)
        {
            if (rec == null) return 6f;
            float baseMag = rec.Severity switch
            {
                WorkerInjurySeverity.Minor => 5f,       // small but noticeable
                WorkerInjurySeverity.Moderate => 14f,   // meaningful
                WorkerInjurySeverity.Serious => 28f,    // large
                WorkerInjurySeverity.Critical => 42f,   // very large
                _ => 8f,
            };
            // Body-part weight: legs/head sting more emotionally
            if (rec.BodyPart is WorkerBodyPart.Leg or WorkerBodyPart.Ankle or WorkerBodyPart.Foot
                or WorkerBodyPart.Head)
                baseMag *= 1.12f;
            return baseMag;
        }

        /// <summary>Extra Frustration when abandoning a post to seek care (once per return).</summary>
        public static float AbandonWorkFrustration(WorkerInjuryWorkStatus status) => status switch
        {
            WorkerInjuryWorkStatus.OffDuty => 6f,
            WorkerInjuryWorkStatus.Limited => 3.5f,
            WorkerInjuryWorkStatus.Incapacitated => 10f,
            _ => 0f,
        };

        /// <summary>Frustration relief on successful Steward tend (does not erase injury).</summary>
        public static float TreatmentReliefFrustration(int woundsTended, bool stabilizedSerious) =>
            Mathf.Clamp(2.5f + woundsTended * 1.2f + (stabilizedSerious ? 3f : 0f), 2f, 10f);

        public static float TreatmentReliefMorale(int woundsTended, bool stabilizedSerious) =>
            Mathf.Clamp(1.2f + woundsTended * 0.4f + (stabilizedSerious ? 1.5f : 0f), 1f, 4f);

        public static WorkerInjuryWorkStatus EvaluateWorkStatus(WorkerRuntime wr)
        {
            if (wr?.State == null || !wr.IsAlive)
                return WorkerInjuryWorkStatus.Fit;
            if (wr.State.Incapacitated)
                return WorkerInjuryWorkStatus.Incapacitated;

            var store = wr.Injuries;
            if (store == null || store.Count == 0)
                return WorkerInjuryWorkStatus.Fit;

            // Mobility-impossible serious injuries → Incapacitated (existing flag may lag)
            if (!CanIndependentWalk(wr) && HasImmobilizingInjury(store))
                return WorkerInjuryWorkStatus.Incapacitated;

            if (WorkerInjuryConsequences.ForcesOutOfWork(store))
                return WorkerInjuryWorkStatus.OffDuty;

            // Serious arm/wrist/back/concussion: off duty for heavy work, but walking OK
            if (HasSeriousWorkStopper(store))
                return WorkerInjuryWorkStatus.OffDuty;

            if (WorkerInjuryConsequences.RestrictsWalking(store)
                || WorkerInjuryConsequences.RestrictsManualWork(store)
                || WorkerInjuryConsequences.FocusWorkMul(store) < 0.9f
                || store.ForcesCare())
                return WorkerInjuryWorkStatus.Limited;

            return WorkerInjuryWorkStatus.Fit;
        }

        public static bool HasImmobilizingInjury(WorkerInjuryStore store)
        {
            if (store == null) return false;
            for (int i = 0; i < store.Active.Count; i++)
            {
                var a = store.Active[i];
                if (a == null || !a.Active) continue;
                if (a.Severity >= WorkerInjurySeverity.Critical
                    && (a.BodyPart is WorkerBodyPart.Leg or WorkerBodyPart.Ankle
                        or WorkerBodyPart.Foot or WorkerBodyPart.Head
                        or WorkerBodyPart.Torso))
                    return true;
                if (a.Type is WorkerInjuryType.BrokenLeg or WorkerInjuryType.BrokenAnkle
                        or WorkerInjuryType.BrokenFoot
                    && a.Severity >= WorkerInjurySeverity.Serious
                    && WorkerInjuryConsequences.WalkSpeedMul(store) < 0.50f)
                    return true;
                if (a.Type is WorkerInjuryType.MajorCrush or WorkerInjuryType.SevereTrauma
                        or WorkerInjuryType.SevereHeadInjury)
                    return true;
            }
            return false;
        }

        public static bool HasSeriousWorkStopper(WorkerInjuryStore store)
        {
            if (store == null) return false;
            for (int i = 0; i < store.Active.Count; i++)
            {
                var a = store.Active[i];
                if (a == null || !a.Active) continue;
                if (a.Severity < WorkerInjurySeverity.Serious) continue;
                if (a.Type is WorkerInjuryType.BrokenArm or WorkerInjuryType.BrokenWrist
                    or WorkerInjuryType.SeriousBackInjury or WorkerInjuryType.Concussion
                    or WorkerInjuryType.RibFracture)
                    return true;
            }
            return false;
        }

        /// <summary>True if the person can limp/walk under their own power (not rescue).</summary>
        public static bool CanIndependentWalk(WorkerRuntime wr)
        {
            if (wr?.State == null || !wr.IsAlive) return false;
            if (wr.State.Incapacitated) return false;
            if (HasImmobilizingInjury(wr.Injuries)
                && WorkerInjuryConsequences.WalkSpeedMul(wr.Injuries) < 0.50f)
                return false;
            float mul = WorkerLocomotion.InjuryMoveMul(wr);
            return mul >= 0.48f;
        }

        /// <summary>
        /// Should leave post and seek Steward at camp (mobile only).
        /// Moderate: often (Determination/Composure may keep them working).
        /// Serious OffDuty: always when mobile.
        /// </summary>
        public static bool ShouldReturnToCampForCare(WorkerRuntime wr)
        {
            if (wr?.State == null || !wr.IsAlive) return false;
            if (wr.State.Incapacitated) return false;
            if (!CanIndependentWalk(wr)) return false;
            var status = EvaluateWorkStatus(wr);
            if (status == WorkerInjuryWorkStatus.OffDuty)
                return true;
            if (status != WorkerInjuryWorkStatus.Limited)
                return false;
            // Moderate / limited: roll once using Determination + Composure
            float det = wr.Stats != null ? wr.Stats.Get(WorkerStatId.Determination) : 10f;
            float com = wr.Stats != null ? wr.Stats.Get(WorkerStatId.Composure) : 10f;
            float grit = (det + com) * 0.5f;
            // High grit → more likely to gut it out (return chance lower)
            float returnChance = Mathf.Lerp(0.72f, 0.28f, Mathf.Clamp01((grit - 5f) / 15f));
            return WorkerRoll.NextUnit() < returnChance;
        }

        public static string StatusLabel(WorkerInjuryWorkStatus s) => s switch
        {
            WorkerInjuryWorkStatus.Fit => "FIT",
            WorkerInjuryWorkStatus.Limited => "LIMITED",
            WorkerInjuryWorkStatus.OffDuty => "OFF DUTY",
            WorkerInjuryWorkStatus.Incapacitated => "INCAPACITATED",
            _ => "—",
        };

        public static string StatusActionLine(WorkerRuntime wr, WorkerInjuryWorkStatus status)
        {
            if (wr?.CampBody != null && wr.CampBody.InjuryReturnActive)
                return "Returning to camp for treatment.";
            if (wr?.CampBody != null && wr.CampBody.SeekingStewardCare)
                return "Awaiting Steward treatment at camp.";
            return status switch
            {
                WorkerInjuryWorkStatus.Fit => "Continuing work.",
                WorkerInjuryWorkStatus.Limited => "Continuing work.",
                WorkerInjuryWorkStatus.OffDuty => "Should remain at camp / recover.",
                WorkerInjuryWorkStatus.Incapacitated => "RESCUE REQUIRED",
                _ => "",
            };
        }

        public static string MovementImpactSummary(WorkerRuntime wr)
        {
            if (wr == null) return "—";
            if (wr.State != null && wr.State.Incapacitated)
                return "Cannot walk — rescue required.";
            float walk = WorkerLocomotion.InjuryMoveMul(wr);
            if (walk < 0.55f) return $"Walking heavily impaired (×{walk:0.00}).";
            if (walk < 0.85f) return $"Walking slowed (×{walk:0.00}).";
            float man = WorkerInjuryConsequences.ManualWorkMul(wr.Injuries);
            if (man < 0.7f) return $"Walking OK; manual work restricted (×{man:0.00}).";
            float foc = WorkerInjuryConsequences.FocusWorkMul(wr.Injuries);
            if (foc < 0.85f) return $"Walking OK; focus work restricted (×{foc:0.00}).";
            return $"Walking mostly normal (×{walk:0.00}).";
        }

        public static string FrustrationImpactSummary(WorkerInjuryRecord worst)
        {
            if (worst == null) return "—";
            return worst.Severity switch
            {
                WorkerInjurySeverity.Minor => "Frustration: small but noticeable.",
                WorkerInjurySeverity.Moderate => "Frustration: meaningful.",
                WorkerInjurySeverity.Serious => "Frustration: large.",
                WorkerInjurySeverity.Critical => "Frustration: very large.",
                _ => "Frustration: elevated.",
            };
        }

        /// <summary>
        /// After typed injury: mark NeedsCare, optionally Incapacitated if immobile,
        /// and return whether runner should start a physical care commute.
        /// </summary>
        public static bool AfterInjuryApplied(WorkerRuntime wr, WorkerInjuryRecord rec, out string log)
        {
            log = "";
            if (wr?.State == null || rec == null) return false;
            wr.Injuries?.SyncNeedsCare(wr.State);

            if (!CanIndependentWalk(wr) && HasImmobilizingInjury(wr.Injuries))
            {
                if (!wr.State.Incapacitated)
                {
                    wr.State.MarkIncapacitated(WorkerStateClock.GameHours,
                        $"{rec.DisplayName} — cannot walk");
                    log = $"{wr.DisplayName} INCAPACITATED — {rec.DisplayName} (cannot walk)";
                }
                return false;
            }

            var status = EvaluateWorkStatus(wr);
            log = $"{wr.DisplayName} {rec.DisplayName} ({rec.SeverityLabel}) → {StatusLabel(status)}";
            if (status == WorkerInjuryWorkStatus.OffDuty)
                return true;
            if (status == WorkerInjuryWorkStatus.Limited && ShouldReturnToCampForCare(wr))
            {
                log += " — returning to camp";
                return true;
            }
            return false;
        }
    }

    /// <summary>Runner registers to start physical care commute after injury.</summary>
    public static class InjuryResponseHub
    {
        public static System.Action<WorkerRuntime, WorkerInjuryRecord> OnInjuryApplied;

        public static void NotifyApplied(WorkerRuntime wr, WorkerInjuryRecord rec) =>
            OnInjuryApplied?.Invoke(wr, rec);
    }
}
