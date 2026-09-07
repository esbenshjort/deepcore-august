using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Last cooperation work consequence (DEV + audit).</summary>
    public enum CooperationWorkConsequence : byte
    {
        None = 0,
        NeutralComplete = 1,
        CoopBenefit = 2,
        CoopSetback = 3,
        Inactive = 4,
    }

    /// <summary>
    /// Snapshot of Excavator↔Engineer cooperation for repair situations only.
    /// Derived — not stored as lasting relationship state.
    /// </summary>
    public struct CooperationAssessment
    {
        public bool Active;
        public float Quality;              // 0.15..0.90 (0.5 ≈ competent baseline)
        public float DispatchSpeedMul;     // en-route only
        public float RepairDurationMul;    // on-site repair timer
        public float SetbackChance;
        public float BenefitChance;
        public string Factors;
        public float AvgTrust;
        public float AvgRespect;
        public float AvgHostility;

        public static CooperationAssessment Inactive() => new CooperationAssessment
        {
            Active = false,
            Quality = 0.5f,
            DispatchSpeedMul = 1f,
            RepairDurationMul = 1f,
            Factors = "inactive (no Exc/Eng pair)",
            SetbackChance = 0f,
            BenefitChance = 0f,
        };
    }

    /// <summary>
    /// Relationship-Aware Work V1 — Excavator operator ↔ Engineer during equipment repair only.
    /// Warmth is not a major work-efficiency input. High Respect + high Hostility can still cooperate.
    /// Does not mutate Trust/Warmth/Hostility/Respect.
    /// </summary>
    public static class ExcavatorEngineerCooperation
    {
        public const float QualityFloor = 0.18f;
        public const float QualityCeil = 0.88f;
        public const float DispatchMulMin = 0.88f;
        public const float DispatchMulMax = 1.12f;
        public const float RepairDurMulMin = 0.88f; // faster when quality high
        public const float RepairDurMulMax = 1.15f; // slower when strained
        public const float MaxSetbackChance = 0.22f;
        public const float MaxBenefitChance = 0.20f;

        public static CooperationAssessment LastAssessment { get; private set; } =
            CooperationAssessment.Inactive();
        public static CooperationWorkConsequence LastConsequence { get; private set; } =
            CooperationWorkConsequence.None;
        public static string LastConsequenceDetail { get; private set; } = "—";
        public static float LastAppliedDispatchMul { get; private set; } = 1f;
        public static float LastAppliedRepairDurMul { get; private set; } = 1f;

        /// <summary>Audit/DEV: reset consequence stamp.</summary>
        public static void AuditResetConsequence()
        {
            LastConsequence = CooperationWorkConsequence.None;
            LastConsequenceDetail = "—";
        }

        public static CooperationAssessment Evaluate(
            WorkerRuntime excavOp,
            WorkerRuntime engOp,
            SocialAuraWorld world)
        {
            if (excavOp == null || engOp == null || world == null || excavOp.WorkerId == engOp.WorkerId)
            {
                LastAssessment = CooperationAssessment.Inactive();
                return LastAssessment;
            }

            int a = excavOp.WorkerId;
            int b = engOp.WorkerId;
            var ab = world.Relation(a, b);
            var ba = world.Relation(b, a);

            float avgT = 0.5f * (ab.Trust + ba.Trust);
            float avgR = 0.5f * (ab.Respect + ba.Respect);
            float avgH = 0.5f * (ab.Hostility + ba.Hostility);
            // Warmth intentionally NOT used as a major work-efficiency term.

            float trust01 = Mathf.Clamp01((avgT - SocialDirectedRelation.Min)
                                          / (SocialDirectedRelation.Max - SocialDirectedRelation.Min));
            float respect01 = Mathf.Clamp01(avgR / SocialDirectedRelation.RespectMax);
            float host01 = Mathf.Clamp01((avgH - SocialDirectedRelation.Min)
                                         / (SocialDirectedRelation.Max - SocialDirectedRelation.Min));

            // Hostility hurts coordination, but high Respect softens it (professional rivalry OK)
            float hostPenalty = host01 * (1f - 0.65f * respect01);

            float score = 0.5f;
            score += (trust01 - 0.5f) * 0.28f;
            score += (respect01 - 0.5f) * 0.34f;
            score -= hostPenalty * 0.24f;

            float memPos, memNeg;
            TallyMemories(world.Memory, a, b, out memPos, out memNeg);
            score += memPos * 0.09f;
            score -= memNeg * 0.11f;

            float frExc = excavOp.State != null ? excavOp.State.Frustration : 0f;
            float frEng = engOp.State != null ? engOp.State.Frustration : 0f;
            float foExc = excavOp.State != null ? excavOp.State.FocusState : 50f;
            float foEng = engOp.State != null ? engOp.State.FocusState : 50f;
            float frAvg01 = 0.5f * (frExc + frEng) / 100f;
            float foAvg01 = 0.5f * (foExc + foEng) / 100f;
            score -= frAvg01 * 0.12f;
            score += (foAvg01 - 0.5f) * 0.08f;

            float soul = SoulBlend(excavOp.Stats, engOp.Stats);
            score += (soul - 0.5f) * 0.10f;

            float quality = Mathf.Clamp(score, QualityFloor, QualityCeil);

            float dispatchMul = Mathf.Lerp(DispatchMulMin, DispatchMulMax, InverseQuality(quality));
            float repairDurMul = Mathf.Lerp(RepairDurMulMax, RepairDurMulMin, InverseQuality(quality));

            float setbackChance = quality < 0.40f
                ? Mathf.Lerp(0f, MaxSetbackChance, Mathf.InverseLerp(0.40f, QualityFloor, quality))
                : 0f;
            float benefitChance = quality > 0.62f
                ? Mathf.Lerp(0f, MaxBenefitChance, Mathf.InverseLerp(0.62f, QualityCeil, quality))
                : 0f;

            var factors = new StringBuilder(96);
            factors.Append($"T={avgT:0.0} R={avgR:0.0} H={avgH:0.0}");
            factors.Append($" mem+{memPos:0.00}/-{memNeg:0.00}");
            factors.Append($" Fr={frAvg01:0.00} Fo={foAvg01:0.00}");
            factors.Append($" Soul={soul:0.00}");
            if (avgH >= 6f && avgR >= 62f)
                factors.Append(" [rivalry→pro]");
            if (avgT <= -2f && avgR <= 40f && avgH >= 6f)
                factors.Append(" [strained]");

            var result = new CooperationAssessment
            {
                Active = true,
                Quality = quality,
                DispatchSpeedMul = dispatchMul,
                RepairDurationMul = repairDurMul,
                SetbackChance = setbackChance,
                BenefitChance = benefitChance,
                Factors = factors.ToString(),
                AvgTrust = avgT,
                AvgRespect = avgR,
                AvgHostility = avgH,
            };
            LastAssessment = result;
            return result;
        }

        /// <summary>
        /// Resolve modest benefit/setback after a completed repair. Does not mutate relations.
        /// Returns consequence for DEV; emits existing WorkerStateEvent types when rolled.
        /// </summary>
        public static CooperationWorkConsequence ResolveRepairOutcome(
            CooperationAssessment coop,
            int excavOpId,
            int engOpId,
            string excavProviderId,
            string engProviderId,
            float baseRecoverMag,
            out float recoverMagMul,
            bool forceBenefit = false,
            bool forceSetback = false,
            float? auditRoll = null)
        {
            recoverMagMul = 1f;
            if (!coop.Active || excavOpId <= 0 || engOpId <= 0)
            {
                LastConsequence = CooperationWorkConsequence.Inactive;
                LastConsequenceDetail = "inactive";
                return LastConsequence;
            }

            float roll = auditRoll ?? WorkerRoll.NextUnit();
            bool benefit = false;
            bool setback = false;
            if (forceBenefit) benefit = true;
            else if (forceSetback) setback = true;
            else if (coop.BenefitChance > 0f && roll < coop.BenefitChance)
                benefit = true;
            else if (coop.SetbackChance > 0f
                     && roll < coop.BenefitChance + coop.SetbackChance)
                setback = true;

            if (benefit)
            {
                recoverMagMul = 1.18f; // modest EquipmentRecovered boost only
                WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                    excavOpId,
                    WorkerStateEventType.ProgressSuccess,
                    1.8f,
                    "CoopSynergy",
                    JobType.Excavation,
                    excavProviderId,
                    relatedWorkerId: engOpId));
                WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                    engOpId,
                    WorkerStateEventType.ProgressSuccess,
                    1.6f,
                    "CoopSynergy",
                    JobType.Engineering,
                    engProviderId,
                    relatedWorkerId: excavOpId));
                LastConsequence = CooperationWorkConsequence.CoopBenefit;
                LastConsequenceDetail =
                    $"benefit Q={coop.Quality:0.00} recover×{recoverMagMul:0.00} (base {baseRecoverMag:0.0})";
                return LastConsequence;
            }

            if (setback)
            {
                // Repair still completes — friction setback via WorkBlocked (small)
                WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                    excavOpId,
                    WorkerStateEventType.WorkBlocked,
                    3.2f,
                    "CoopFriction",
                    JobType.Excavation,
                    excavProviderId,
                    relatedWorkerId: engOpId));
                WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                    engOpId,
                    WorkerStateEventType.WorkBlocked,
                    2.6f,
                    "CoopFriction",
                    JobType.Engineering,
                    engProviderId,
                    relatedWorkerId: excavOpId));
                recoverMagMul = 0.92f;
                LastConsequence = CooperationWorkConsequence.CoopSetback;
                LastConsequenceDetail =
                    $"setback Q={coop.Quality:0.00} recover×{recoverMagMul:0.00}";
                return LastConsequence;
            }

            LastConsequence = CooperationWorkConsequence.NeutralComplete;
            LastConsequenceDetail = $"neutral Q={coop.Quality:0.00}";
            return LastConsequence;
        }

        public static void StampAppliedModifiers(float dispatchMul, float repairDurMul)
        {
            LastAppliedDispatchMul = dispatchMul;
            LastAppliedRepairDurMul = repairDurMul;
        }

        /// <summary>Map quality floor..ceil → 0..1 for mul lerps.</summary>
        static float InverseQuality(float q) =>
            Mathf.InverseLerp(QualityFloor, QualityCeil, q);

        static float SoulBlend(WorkerStats a, WorkerStats b)
        {
            float sa = SoulOne(a);
            float sb = SoulOne(b);
            return 0.5f * (sa + sb);
        }

        static float SoulOne(WorkerStats s)
        {
            if (s == null) return 0.5f;
            // Composure / Focus / Tolerance — coordination under pressure
            float v = (s.Get(WorkerStatId.Composure)
                       + s.Get(WorkerStatId.Focus)
                       + s.Get(WorkerStatId.Tolerance)) / 3f;
            return Mathf.Clamp01(v / 20f);
        }

        static void TallyMemories(
            SocialMemoryStore mem, int excavId, int engId,
            out float pos, out float neg)
        {
            pos = 0f;
            neg = 0f;
            if (mem == null) return;
            Accumulate(mem.GetToward(excavId, engId), ref pos, ref neg);
            Accumulate(mem.GetToward(engId, excavId), ref pos, ref neg);
            pos = Mathf.Clamp01(pos / 2.5f);
            neg = Mathf.Clamp01(neg / 2.5f);
        }

        static void Accumulate(IReadOnlyList<SocialMemoryEntry> list, ref float pos, ref float neg)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null || e.Strength < 0.25f) continue;
                switch (e.Type)
                {
                    case SocialMemoryType.HelpedMe:
                    case SocialMemoryType.SupportedMe:
                    case SocialMemoryType.WorkedWellTogether:
                    case SocialMemoryType.SharedSuccess:
                    case SocialMemoryType.TookMySide:
                        pos += e.Strength * (e.Major ? 1.15f : 1f);
                        break;
                    case SocialMemoryType.LetMeDown:
                    case SocialMemoryType.BlamedMe:
                    case SocialMemoryType.InsultedMe:
                    case SocialMemoryType.FailedTogether:
                    case SocialMemoryType.HurtBy:
                    case SocialMemoryType.WitnessedViolence:
                    case SocialMemoryType.KilledBy:
                    case SocialMemoryType.WitnessedDeath:
                        neg += e.Strength * (e.Major ? 1.15f : 1f);
                        break;
                }
            }
        }

        /// <summary>Audit helper: evaluate from raw axes without live workers.</summary>
        public static CooperationAssessment AuditEvaluateAxes(
            float trust, float respect, float hostility,
            float frustrationAvg = 20f, float focusAvg = 70f,
            float soul01 = 0.5f,
            float memPos = 0f, float memNeg = 0f)
        {
            float trust01 = Mathf.Clamp01((trust - SocialDirectedRelation.Min)
                                          / (SocialDirectedRelation.Max - SocialDirectedRelation.Min));
            float respect01 = Mathf.Clamp01(respect / SocialDirectedRelation.RespectMax);
            float host01 = Mathf.Clamp01((hostility - SocialDirectedRelation.Min)
                                         / (SocialDirectedRelation.Max - SocialDirectedRelation.Min));
            float hostPenalty = host01 * (1f - 0.65f * respect01);

            float score = 0.5f;
            score += (trust01 - 0.5f) * 0.28f;
            score += (respect01 - 0.5f) * 0.34f;
            score -= hostPenalty * 0.24f;
            score += Mathf.Clamp01(memPos) * 0.09f;
            score -= Mathf.Clamp01(memNeg) * 0.11f;
            score -= (frustrationAvg / 100f) * 0.12f;
            score += (focusAvg / 100f - 0.5f) * 0.08f;
            score += (soul01 - 0.5f) * 0.10f;

            float quality = Mathf.Clamp(score, QualityFloor, QualityCeil);
            return new CooperationAssessment
            {
                Active = true,
                Quality = quality,
                DispatchSpeedMul = Mathf.Lerp(DispatchMulMin, DispatchMulMax, InverseQuality(quality)),
                RepairDurationMul = Mathf.Lerp(RepairDurMulMax, RepairDurMulMin, InverseQuality(quality)),
                SetbackChance = quality < 0.40f
                    ? Mathf.Lerp(0f, MaxSetbackChance, Mathf.InverseLerp(0.40f, QualityFloor, quality))
                    : 0f,
                BenefitChance = quality > 0.62f
                    ? Mathf.Lerp(0f, MaxBenefitChance, Mathf.InverseLerp(0.62f, QualityCeil, quality))
                    : 0f,
                Factors = $"audit T={trust:0.0} R={respect:0.0} H={hostility:0.0}",
                AvgTrust = trust,
                AvgRespect = respect,
                AvgHostility = hostility,
            };
        }
    }
}
