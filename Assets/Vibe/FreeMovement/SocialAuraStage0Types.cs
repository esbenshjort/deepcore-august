using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    // ═══════════════════════════════════════════════════════════════
    // SOCIAL AURA STAGE 0 — types (simulation only, no live proximity)
    // ═══════════════════════════════════════════════════════════════

    public enum SocialAuraClass : byte { Neutral = 0, Positive = 1, Negative = 2, Mixed = 3 }

    public enum SocialContext : byte
    {
        None = 0,
        WorkingTogether = 1,
        SharedProblem = 2,
        RecentSuccess = 3,
        RecentFailure = 4,
        IdleNearby = 5,
        Emergency = 6,
        /// <summary>Off-shift camp proximity — uses existing encounter resolver; not universally positive.</summary>
        Camp = 7,
    }

    public enum SocialAction : byte
    {
        Encourage = 0,
        Joke = 1,
        Connect = 2,
        Complain = 3,
        Provoke = 4,
        Confront = 5,
    }

    public enum SocialResponse : byte
    {
        Accept = 0,
        Deflect = 1,
        Ignore = 2,
        Agree = 3,
        PushBack = 4,
        Escalate = 5,
        Withdraw = 6,
    }

    /// <summary>Derived outward signal — not stored as primary WorkerState.</summary>
    public readonly struct SocialExpression
    {
        public readonly float Positive;   // 0–1
        public readonly float Negative;   // 0–1
        public readonly float Intensity;  // 0–1
        public readonly float Reach;      // relative units (sim)
        public readonly SocialAuraClass Class;

        public SocialExpression(float positive, float negative, float intensity, float reach, SocialAuraClass cls)
        {
            Positive = positive;
            Negative = negative;
            Intensity = intensity;
            Reach = reach;
            Class = cls;
        }
    }

    /// <summary>Directional A→B lasting memory. Independent of B→A.</summary>
    [Serializable]
    public sealed class SocialDirectedRelation
    {
        public const float Min = -20f;
        public const float Max = 20f;
        public const float RespectMin = 0f;
        public const float RespectMax = 100f;
        public const float RespectBaseline = 50f;

        public float Trust;
        public float Warmth;
        public float Hostility;
        /// <summary>Competence / reliability respect. Independent of Warmth/Hostility. 0..100.</summary>
        public float Respect = RespectBaseline;

        public static SocialDirectedRelation CreateNeutral() =>
            new SocialDirectedRelation { Respect = RespectBaseline };

        public void Clamp()
        {
            Trust = Mathf.Clamp(Trust, Min, Max);
            Warmth = Mathf.Clamp(Warmth, Min, Max);
            Hostility = Mathf.Clamp(Hostility, Min, Max);
            Respect = Mathf.Clamp(Respect, RespectMin, RespectMax);
        }

        public void Add(float dTrust, float dWarmth, float dHostility)
        {
            Trust += dTrust;
            Warmth += dWarmth;
            Hostility += dHostility;
            Clamp();
        }

        /// <summary>Respect-only delta. Does not touch Trust/Warmth/Hostility.</summary>
        public void AddRespect(float delta)
        {
            Respect += delta;
            Respect = Mathf.Clamp(Respect, RespectMin, RespectMax);
        }
    }

    /// <summary>Transient pair layer — not WorkerState.</summary>
    [Serializable]
    public sealed class SocialPairTransient
    {
        public float InteractionPressure;
        public float CooldownRemaining; // shift-units
        public float LastEncounterShift;
        public float ExposureThisShift;
        public readonly List<SocialEncounterLog> RecentHistory = new(8);

        public void PushHistory(SocialEncounterLog log, int max = 8)
        {
            if (log == null) return;
            RecentHistory.Add(log);
            while (RecentHistory.Count > max)
                RecentHistory.RemoveAt(0);
        }
    }

    [Serializable]
    public sealed class SocialEncounterLog
    {
        public int ShiftIndex;
        public float TimeInShift;
        public int InitiatorId;
        public int TargetId;
        public SocialContext Context;
        public SocialAction Action;
        public SocialResponse Response;
        public bool ActionSuccess;
        public bool ResponseSuccess;
        public int ActionD20;
        public int ResponseD20;
        public float DeltaTrustIT;   // initiator→target
        public float DeltaWarmthIT;
        public float DeltaHostilityIT;
        public float DeltaTrustTI;
        public float DeltaWarmthTI;
        public float DeltaHostilityTI;
        public float DeltaFrustrationInit;
        public float DeltaMoraleInit;
        public float DeltaFrustrationTarget;
        public float DeltaMoraleTarget;
        public string WhyPressure;
        public string WhyInitiator;
        public string WhyAction;
        public string WhyResponse;
        public string OutcomeSummary;
    }

    /// <summary>Sim actor: soul + mutable state snapshot (not live hosts).</summary>
    public sealed class SocialSimActor
    {
        public int Id;
        public string Name;
        public WorkerStats Stats;
        public WorkerState State;
        public SocialExpression Expression;

        public SocialSimActor(int id, string name, WorkerStats stats, WorkerState state)
        {
            Id = id;
            Name = name;
            Stats = stats ?? WorkerStats.CreateBaseline();
            State = state ?? WorkerState.CreateDefault();
        }

        public void RefreshExpression() =>
            Expression = SocialExpressionModel.Derive(State, Stats);
    }

    public static class SocialExpressionModel
    {
        public static SocialExpression Derive(WorkerState st, WorkerStats stats)
        {
            if (st == null) st = WorkerState.CreateDefault();
            int composure = S(stats, WorkerStatId.Composure);
            int affinity = S(stats, WorkerStatId.Affinity);
            int leadership = S(stats, WorkerStatId.Leadership);
            int focusStat = S(stats, WorkerStatId.Focus);

            float morale01 = st.Morale / 100f;
            float frust01 = st.Frustration / 100f;
            float fatigue01 = st.MentalFatigue / 100f;
            float focusState01 = st.FocusState / 100f;

            // Positive expression: morale amplified by Affinity/Leadership; fatigue softens availability
            float posAmp = 0.35f + affinity / 40f + leadership / 55f;
            float pos = morale01 * posAmp * (1f - fatigue01 * 0.35f);
            pos = Mathf.Clamp01(pos);

            // Negative expression: frustration leak resisted by Composure; low FocusState increases reactivity
            float composureMask = Mathf.Clamp01(1.15f - composure / 28f); // high composure → less leak
            float reactivity = 1f + (1f - focusState01) * 0.45f;
            float neg = frust01 * composureMask * reactivity;
            neg = Mathf.Clamp01(neg);

            // Intensity from both channels (allows Mixed)
            float intensity = Mathf.Clamp01(Mathf.Max(pos, neg) + 0.28f * Mathf.Min(pos, neg));

            // Reach: intensity primary; Leadership soft only
            float reach = 0.45f + intensity * 0.75f + leadership * 0.012f;
            reach = Mathf.Clamp(reach, 0.35f, 1.6f);

            SocialAuraClass cls;
            bool hiPos = pos >= 0.42f;
            bool hiNeg = neg >= 0.42f;
            if (hiPos && hiNeg) cls = SocialAuraClass.Mixed;
            else if (pos >= neg + 0.12f && pos >= 0.32f) cls = SocialAuraClass.Positive;
            else if (neg >= pos + 0.12f && neg >= 0.32f) cls = SocialAuraClass.Negative;
            else cls = SocialAuraClass.Neutral;

            return new SocialExpression(pos, neg, intensity, reach, cls);
        }

        static int S(WorkerStats stats, WorkerStatId id) =>
            stats != null ? stats.Get(id) : WorkerStats.Baseline;
    }

    public static class SocialAuraTuning
    {
        public const float PressureTrigger = 1.05f;
        public const float PressureDecayPerShiftUnit = 0.55f;
        public const float CooldownAfterEncounter = 0.55f; // fraction of a shift
        public const float MaxEncountersPerWorkerPerShift = 3f;
        public const float ExposureTick = 0.12f; // sim opportunity chunk

        public static float ContextMul(SocialContext ctx) => ctx switch
        {
            SocialContext.SharedProblem => 1.55f,
            SocialContext.WorkingTogether => 1.25f,
            SocialContext.Emergency => 1.40f,
            SocialContext.RecentFailure => 1.20f,
            SocialContext.RecentSuccess => 1.10f,
            SocialContext.Camp => 0.90f, // distinct from IdleNearby; not a positivity buff
            SocialContext.IdleNearby => 0.85f,
            _ => 1f,
        };
    }
}
