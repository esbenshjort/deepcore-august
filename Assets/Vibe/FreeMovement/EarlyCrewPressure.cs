using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Temporary early-hire social uncertainty + cautious relationship seeding.
    /// Does not redesign Social Aura; presentation/math hooks only amplify existing paths.
    /// Prototype DEV crew leaves this inactive.
    /// </summary>
    public static class EarlyCrewPressure
    {
        /// <summary>Working days of elevated unfamiliarity after hire.</summary>
        public const float InstabilityDays = 9f;

        /// <summary>How strongly positive shared experiences burn down uncertainty.</summary>
        public const float PositiveBondInstabilityCut = 0.08f;

        // —— BEFORE (CreateNeutral) vs AFTER (hired seed) documented in audit ——
        public const float SeedTrustCenter = 0.15f;
        public const float SeedTrustSpread = 1.4f;
        public const float SeedWarmthCenter = 0.35f;
        public const float SeedWarmthSpread = 1.6f;
        public const float SeedRespectCenter = 48f;
        public const float SeedRespectSpread = 4f;
        public const float SeedHostilityMax = 1.1f;

        public static bool Active { get; private set; }
        public static int WorkingDaysElapsed { get; private set; }
        public static float PositiveProgress { get; private set; }

        /// <summary>1 at hire → 0 after ~week / positive bonds. No Hostility injection.</summary>
        public static float Instability01
        {
            get
            {
                if (!Active) return 0f;
                float dayFade = Mathf.Clamp01(1f - WorkingDaysElapsed / InstabilityDays);
                float bondFade = 1f - Mathf.Clamp01(PositiveProgress) * 0.55f;
                return Mathf.Clamp01(dayFade * bondFade);
            }
        }

        public static float TrustGainMul =>
            Active ? Mathf.Lerp(1f, 0.52f, Instability01) : 1f;

        public static float NegativeReactionMul =>
            Active ? Mathf.Lerp(1f, 1.55f, Instability01) : 1f;

        public static float ConflictRecoveryMul =>
            Active ? Mathf.Lerp(1f, 0.55f, Instability01) : 1f;

        public static float PressureGainMul =>
            Active ? Mathf.Lerp(1f, 1.3f, Instability01) : 1f;

        public static float EscalateWeightMul =>
            Active ? Mathf.Lerp(1f, 1.45f, Instability01) : 1f;

        /// <summary>Lowers argue Frustration gate during early instability (not fight/lethal).</summary>
        public static float ArgueFrustrationRelief =>
            Active ? 12f * ArgueSoft01 : 0f;

        /// <summary>
        /// Soft argue Hostility gate through ~week 1–early week 2.
        /// Uses the longer of instability vs calendar soft-window so mid-week stress
        /// can still spark arguments without fighting/lethal gate changes.
        /// </summary>
        public static float ArgueHostilityRelief =>
            Active ? 5.4f * ArgueSoft01 : 0f;

        /// <summary>Calendar soft window for argue emergence (independent of bond settle rate).</summary>
        static float ArgueSoft01
        {
            get
            {
                if (!Active) return 0f;
                float daySoft = Mathf.Clamp01(1f - WorkingDaysElapsed / 11f);
                return Mathf.Max(Instability01, daySoft * 0.92f);
            }
        }

        public static float SocialFrustrationMul =>
            Active ? Mathf.Lerp(1f, 1.4f, Instability01) : 1f;

        public static void Deactivate()
        {
            Active = false;
            WorkingDaysElapsed = 0;
            PositiveProgress = 0f;
        }

        public static void ActivateForHiredCrew(
            SocialAuraWorld world,
            IReadOnlyList<WorkerRuntime> crew)
        {
            Active = true;
            WorkingDaysElapsed = 0;
            PositiveProgress = 0f;
            if (world != null && crew != null)
                SeedCautiousRelations(world, crew);
        }

        public static void NotifyWorkingDayAdvanced()
        {
            if (!Active) return;
            WorkingDaysElapsed++;
            if (WorkingDaysElapsed >= InstabilityDays && PositiveProgress >= 0.35f)
            {
                // Settled enough — drop flag but keep Active until fully faded via Instability01
            }
            if (WorkingDaysElapsed > InstabilityDays + 3 && Instability01 <= 0.02f)
                Deactivate();
        }

        public static void RegisterPositiveBond()
        {
            if (!Active) return;
            PositiveProgress = Mathf.Clamp01(PositiveProgress + PositiveBondInstabilityCut);
        }

        /// <summary>
        /// Modest cautious impressions — low Trust/Warmth, near-baseline Respect, tiny Hostility.
        /// Soul/Affinity/Empathy/Tolerance nudge starting impressions slightly.
        /// </summary>
        public static void SeedCautiousRelations(
            SocialAuraWorld world,
            IReadOnlyList<WorkerRuntime> crew)
        {
            if (world == null || crew == null) return;
            world.ClearDirectedRelations();

            for (int i = 0; i < crew.Count; i++)
            {
                var a = crew[i];
                if (a == null) continue;
                for (int j = 0; j < crew.Count; j++)
                {
                    if (i == j) continue;
                    var b = crew[j];
                    if (b == null) continue;
                    SeedPairDirected(world, a, b);
                }
            }
        }

        static void SeedPairDirected(SocialAuraWorld world, WorkerRuntime from, WorkerRuntime to)
        {
            int aff = S(from, WorkerStatId.Affinity);
            int emp = S(from, WorkerStatId.Empathy);
            int tol = S(from, WorkerStatId.Tolerance);
            int lead = S(from, WorkerStatId.Leadership);
            int toAff = S(to, WorkerStatId.Affinity);
            int toTol = S(to, WorkerStatId.Tolerance);

            // Deterministic variation from ids (stable across runs)
            float h = Hash01(from.WorkerId, to.WorkerId);

            float trust = SeedTrustCenter
                         + (aff - 10) * 0.08f
                         + (toAff - 10) * 0.04f
                         + (h - 0.5f) * SeedTrustSpread;
            float warmth = SeedWarmthCenter
                           + (emp - 10) * 0.09f
                           + (aff - 10) * 0.05f
                           + (h - 0.35f) * SeedWarmthSpread * 0.7f;
            float respect = SeedRespectCenter
                            + (lead - 10) * 0.35f
                            + (h - 0.5f) * SeedRespectSpread;
            // Tiny edge only when both low tolerance — never predetermined enemies
            float hostility = 0f;
            if (tol <= 8 && toTol <= 9)
                hostility = Mathf.Clamp((9 - tol) * 0.12f + (h - 0.6f) * 0.5f, 0f, SeedHostilityMax);
            else if (h > 0.88f)
                hostility = Mathf.Clamp((h - 0.88f) * 4f, 0f, 0.45f);

            trust = Mathf.Clamp(trust, -2.5f, 2.5f);
            warmth = Mathf.Clamp(warmth, -2f, 3f);
            respect = Mathf.Clamp(respect, 42f, 56f);
            hostility = Mathf.Clamp(hostility, 0f, SeedHostilityMax);

            var rel = world.Relation(from.WorkerId, to.WorkerId);
            rel.Trust = trust;
            rel.Warmth = warmth;
            rel.Hostility = hostility;
            rel.Respect = respect;
            rel.Clamp();
        }

        static int S(WorkerRuntime wr, WorkerStatId id) =>
            wr?.Stats != null ? wr.Stats.Get(id) : WorkerStats.Baseline;

        static float Hash01(int a, int b)
        {
            unchecked
            {
                int x = a * 73856093 ^ b * 19349663;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                return (x & 0x7fffffff) / (float)int.MaxValue;
            }
        }

        /// <summary>
        /// Scale resolved encounter deltas: slower Trust gains, stronger negative reactions,
        /// weaker Hostility recovery. Does not invent Hostility from nothing.
        /// </summary>
        public static void ScaleEncounterDeltas(
            ref float dTrIT, ref float dWaIT, ref float dHoIT,
            ref float dTrTI, ref float dWaTI, ref float dHoTI,
            ref float dFrI, ref float dFrT,
            SocialSimActor init, SocialSimActor target)
        {
            if (!Active || Instability01 <= 0.001f) return;

            float trustMul = TrustGainMul;
            float negMul = NegativeReactionMul;
            float recoverMul = ConflictRecoveryMul;
            float frMul = SocialFrustrationMul;

            // Empathy / Leadership slightly improve positive repair even early
            float empathyBoost = 1f;
            if (init?.Stats != null)
                empathyBoost += (init.Stats.Get(WorkerStatId.Empathy) - 10) * 0.015f;
            if (target?.Stats != null)
                empathyBoost += (target.Stats.Get(WorkerStatId.Empathy) - 10) * 0.01f;
            empathyBoost = Mathf.Clamp(empathyBoost, 0.85f, 1.25f);

            ScaleAxis(ref dTrIT, trustMul, negMul, empathyBoost);
            ScaleAxis(ref dWaIT, trustMul, negMul, empathyBoost);
            ScaleAxis(ref dTrTI, trustMul, negMul, empathyBoost);
            ScaleAxis(ref dWaTI, trustMul, negMul, empathyBoost);
            ScaleHostility(ref dHoIT, negMul, recoverMul);
            ScaleHostility(ref dHoTI, negMul, recoverMul);

            if (dFrI > 0f) dFrI *= frMul;
            if (dFrT > 0f) dFrT *= frMul;
        }

        static void ScaleAxis(ref float d, float gainMul, float negMul, float empathyBoost)
        {
            if (d > 0f) d *= gainMul * empathyBoost;
            else if (d < 0f) d *= negMul;
        }

        static void ScaleHostility(ref float d, float negMul, float recoverMul)
        {
            if (d > 0f) d *= negMul;
            else if (d < 0f) d *= recoverMul;
        }

        public static void ScaleConflictRecoveryDeltas(
            ref float dTrSL, ref float dTrLS,
            ref float dWaSL, ref float dWaLS,
            ref float dHoSL, ref float dHoLS,
            ref float dFrS, ref float dFrL)
        {
            if (!Active || Instability01 <= 0.001f) return;
            float trustMul = TrustGainMul;
            float negMul = NegativeReactionMul;
            float recoverMul = ConflictRecoveryMul;
            float frMul = SocialFrustrationMul;

            ScaleAxis(ref dTrSL, trustMul, negMul, 1f);
            ScaleAxis(ref dTrLS, trustMul, negMul, 1f);
            ScaleAxis(ref dWaSL, trustMul, negMul, 1f);
            ScaleAxis(ref dWaLS, trustMul, negMul, 1f);
            ScaleHostility(ref dHoSL, negMul, recoverMul);
            ScaleHostility(ref dHoLS, negMul, recoverMul);
            if (dFrS > 0f) dFrS *= frMul;
            if (dFrL > 0f) dFrL *= frMul;
            // Soften overnight / de-escalate frustration relief while unfamiliar
            if (dFrS < 0f) dFrS *= recoverMul;
            if (dFrL < 0f) dFrL *= recoverMul;
        }
    }
}
