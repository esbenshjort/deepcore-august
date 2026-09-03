using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>DEV/debug derived relationship label — never stored as independent state.</summary>
    public enum RelationshipClass : byte
    {
        Neutral = 0,
        Friendly = 1,
        Bonded = 2,
        Professional = 3,
        Rivalry = 4,
        Grudge = 5,
        Strained = 6,
    }

    /// <summary>
    /// Relationship V1.1 — Respect updates from meaningful social outcomes only.
    /// Does not alter Trust/Warmth/Hostility math, encounter frequency, or dialogue.
    /// Dislike/Hostility does not automatically reduce Respect.
    /// </summary>
    public static class SocialRespectApplicator
    {
        /// <summary>Hard cap so one encounter cannot swing Respect into extreme classifications alone.</summary>
        public const float MaxAbsPerEncounter = 3.5f;

        public static float LastDeltaIT { get; private set; }
        public static float LastDeltaTI { get; private set; }

        public static void Apply(SocialAuraWorld world, SocialEncounterLog log)
        {
            LastDeltaIT = 0f;
            LastDeltaTI = 0f;
            if (world == null || log == null) return;
            if (!SocialMemoryRecorder.IsMeaningful(log)) return;

            float dIT = 0f; // initiator → target Respect
            float dTI = 0f; // target → initiator Respect

            switch (log.Action)
            {
                case SocialAction.Encourage:
                    if (log.ActionSuccess
                        && (log.Response == SocialResponse.Accept || log.Response == SocialResponse.Agree))
                    {
                        // Reliable support → target respects helper; mild peer respect back
                        dTI += 2.6f;
                        dIT += 0.9f;
                    }
                    else if (!log.ActionSuccess)
                    {
                        dTI -= 1.4f; // failed support reads as less reliable
                    }
                    break;

                case SocialAction.Connect:
                    if (log.ActionSuccess
                        && (log.Response == SocialResponse.Accept || log.Response == SocialResponse.Agree))
                    {
                        if (log.Context == SocialContext.RecentSuccess
                            || log.Context == SocialContext.WorkingTogether)
                        {
                            dIT += 1.6f;
                            dTI += 1.6f;
                        }
                        else
                        {
                            dIT += 0.8f;
                            dTI += 0.8f;
                        }
                    }
                    break;

                case SocialAction.Joke:
                    // Rapport ≠ respect; only mild bump on clean accept
                    if (log.ActionSuccess
                        && (log.Response == SocialResponse.Accept || log.Response == SocialResponse.Agree))
                    {
                        dIT += 0.4f;
                        dTI += 0.4f;
                    }
                    else if (log.Response == SocialResponse.Escalate || log.Response == SocialResponse.PushBack)
                    {
                        dTI -= 1.8f; // poor conduct
                    }
                    break;

                case SocialAction.Complain:
                    if (log.Response == SocialResponse.Agree || log.Response == SocialResponse.Accept)
                    {
                        // Stood with me under pressure — competence/loyalty respect
                        dIT += 1.8f;
                        dTI += 1.4f;
                    }
                    else if (log.Response == SocialResponse.Ignore || log.Response == SocialResponse.Deflect)
                    {
                        if (log.Context == SocialContext.SharedProblem
                            || log.Context == SocialContext.RecentFailure
                            || log.Context == SocialContext.Emergency)
                            dIT -= 2.2f; // let me down when it mattered
                    }
                    else if (log.Response == SocialResponse.Escalate && !log.ActionSuccess)
                    {
                        dIT -= 1.2f;
                        dTI -= 1.0f;
                    }
                    break;

                case SocialAction.Provoke:
                case SocialAction.Confront:
                    if (log.Response == SocialResponse.Escalate || log.Response == SocialResponse.PushBack)
                    {
                        // Poor conduct / blame — Respect drops; Hostility is separate
                        dTI -= 2.8f;
                        dIT -= 1.6f;
                    }
                    else if (log.Response == SocialResponse.Withdraw && !log.ActionSuccess)
                    {
                        dTI += 0.8f; // walked it back — small respect recovery
                    }
                    else if (log.ActionSuccess
                             && (log.Response == SocialResponse.Ignore || log.Response == SocialResponse.Accept))
                    {
                        dTI -= 1.5f;
                    }
                    break;
            }

            // Context reinforcement — still not Hostility-driven
            if (log.Context == SocialContext.RecentSuccess
                && log.ActionSuccess
                && (log.Response == SocialResponse.Accept || log.Response == SocialResponse.Agree))
            {
                dIT += 0.6f;
                dTI += 0.6f;
            }

            dIT = Mathf.Clamp(dIT, -MaxAbsPerEncounter, MaxAbsPerEncounter);
            dTI = Mathf.Clamp(dTI, -MaxAbsPerEncounter, MaxAbsPerEncounter);
            LastDeltaIT = dIT;
            LastDeltaTI = dTI;

            if (Mathf.Abs(dIT) > 0.001f)
                world.Relation(log.InitiatorId, log.TargetId).AddRespect(dIT);
            if (Mathf.Abs(dTI) > 0.001f)
                world.Relation(log.TargetId, log.InitiatorId).AddRespect(dTI);
        }
    }

    /// <summary>
    /// Pure derivation of relationship class from axes + memory evidence.
    /// Conservative thresholds — Bonded / Rivalry / Grudge require multi-encounter-scale evidence.
    /// </summary>
    public static class RelationshipClassifier
    {
        // Axis thresholds (Trust/Warmth/Hostility ∈ −20..20, Respect ∈ 0..100)
        public const float BondedWarmthMin = 8f;
        public const float BondedTrustMin = 5f;
        public const float BondedHostilityMax = 2f;
        public const float BondedRespectMin = 52f;

        public const float RivalryHostilityMin = 6f;
        public const float RivalryRespectMin = 62f;
        public const float RivalryWarmthMax = 5f;

        public const float GrudgeHostilityMin = 7f;
        public const float GrudgeTrustMax = -2f;
        public const float GrudgeWarmthMax = 1f;

        public const float ProfessionalRespectMin = 65f;
        public const float ProfessionalTrustMin = 4f;
        public const float ProfessionalWarmthAbsMax = 6f;
        public const float ProfessionalHostilityMax = 3f;

        public const float FriendlyWarmthMin = 4f;
        public const float FriendlyHostilityMax = 2f;
        public const float FriendlyTrustMin = 1f;

        public const float StrainedHostilityMin = 3.5f;
        public const float StrainedTrustMax = -2f;

        /// <summary>Minimum distinct supporting memory types (or major count) for extreme labels.</summary>
        public const int ExtremeMemoryEvidenceMin = 2;

        public static RelationshipClass Classify(
            SocialDirectedRelation rel,
            IReadOnlyList<SocialMemoryEntry> memoriesToward,
            out string why)
        {
            why = "neutral default";
            if (rel == null)
                return RelationshipClass.Neutral;

            float t = rel.Trust;
            float w = rel.Warmth;
            float h = rel.Hostility;
            float r = rel.Respect;

            int posMaj = 0, negMaj = 0, posTypes = 0, negTypes = 0;
            float posStr = 0f, negStr = 0f;
            TallyEvidence(memoriesToward, ref posMaj, ref negMaj, ref posTypes, ref negTypes, ref posStr, ref negStr);

            bool extremePosMem = posMaj + posTypes >= ExtremeMemoryEvidenceMin || posStr >= 1.15f;
            bool extremeNegMem = negMaj + negTypes >= ExtremeMemoryEvidenceMin || negStr >= 1.15f;

            // Grudge — hostility + betrayal memory; Respect independent (can still be mid/high)
            if (h >= GrudgeHostilityMin && t <= GrudgeTrustMax && w <= GrudgeWarmthMax && extremeNegMem)
            {
                why = $"Grudge: H={h:0.#} T={t:0.#} W={w:0.#} negMem={negMaj}+{negTypes}";
                return RelationshipClass.Grudge;
            }

            // Rivalry — high Hostility AND high Respect (dislike ≠ lose respect)
            if (h >= RivalryHostilityMin && r >= RivalryRespectMin && w <= RivalryWarmthMax)
            {
                why = $"Rivalry: H={h:0.#} R={r:0.#} W={w:0.#}";
                return RelationshipClass.Rivalry;
            }

            // Bonded — deep positive axes + multi-memory evidence
            if (w >= BondedWarmthMin && t >= BondedTrustMin && h <= BondedHostilityMax
                && r >= BondedRespectMin && extremePosMem)
            {
                why = $"Bonded: W={w:0.#} T={t:0.#} R={r:0.#} posMem={posMaj}+{posTypes}";
                return RelationshipClass.Bonded;
            }

            // Professional — competence respect without deep warmth
            if (r >= ProfessionalRespectMin && t >= ProfessionalTrustMin
                && Mathf.Abs(w) <= ProfessionalWarmthAbsMax && h <= ProfessionalHostilityMax)
            {
                why = $"Professional: R={r:0.#} T={t:0.#} |W|={Mathf.Abs(w):0.#}";
                return RelationshipClass.Professional;
            }

            // Friendly — soft positive warmth
            if (w >= FriendlyWarmthMin && h <= FriendlyHostilityMax && t >= FriendlyTrustMin
                && r >= SocialDirectedRelation.RespectBaseline - 8f)
            {
                why = $"Friendly: W={w:0.#} H={h:0.#} T={t:0.#}";
                return RelationshipClass.Friendly;
            }

            // Strained — elevated friction without full grudge/rivalry
            if (h >= StrainedHostilityMin
                || (t <= StrainedTrustMax && w < 3f)
                || (w <= -2f && h >= 2f))
            {
                why = $"Strained: H={h:0.#} T={t:0.#} W={w:0.#}";
                return RelationshipClass.Strained;
            }

            why = $"Neutral: T={t:0.#} W={w:0.#} H={h:0.#} R={r:0.#}";
            return RelationshipClass.Neutral;
        }

        /// <summary>Top memory types that support the current derived class (for DEV UI).</summary>
        public static void FillSupportingMemories(
            RelationshipClass cls,
            IReadOnlyList<SocialMemoryEntry> memoriesToward,
            List<SocialMemoryEntry> into,
            int max)
        {
            into.Clear();
            if (memoriesToward == null || max <= 0) return;

            var ranked = new List<SocialMemoryEntry>(memoriesToward.Count);
            for (int i = 0; i < memoriesToward.Count; i++)
            {
                var e = memoriesToward[i];
                if (e == null) continue;
                if (SupportsClass(cls, e.Type))
                    ranked.Add(e);
            }

            // If none match class filters, fall back to strongest overall
            if (ranked.Count == 0)
            {
                for (int i = 0; i < memoriesToward.Count; i++)
                    if (memoriesToward[i] != null) ranked.Add(memoriesToward[i]);
            }

            ranked.Sort((a, b) =>
            {
                int c = b.Strength.CompareTo(a.Strength);
                return c != 0 ? c : b.GameTime.CompareTo(a.GameTime);
            });
            for (int i = 0; i < ranked.Count && into.Count < max; i++)
                into.Add(ranked[i]);
        }

        public static string FormatAxes(SocialDirectedRelation rel) =>
            rel == null
                ? "T=— W=— H=— R=—"
                : $"T={rel.Trust:0.0} W={rel.Warmth:0.0} H={rel.Hostility:0.0} R={rel.Respect:0.0}";

        static bool SupportsClass(RelationshipClass cls, SocialMemoryType type)
        {
            switch (cls)
            {
                case RelationshipClass.Bonded:
                case RelationshipClass.Friendly:
                    return type == SocialMemoryType.SupportedMe
                           || type == SocialMemoryType.HelpedMe
                           || type == SocialMemoryType.SharedHardship
                           || type == SocialMemoryType.SharedSuccess
                           || type == SocialMemoryType.TookMySide
                           || type == SocialMemoryType.WorkedWellTogether
                           || type == SocialMemoryType.Apologized;
                case RelationshipClass.Professional:
                    return type == SocialMemoryType.HelpedMe
                           || type == SocialMemoryType.WorkedWellTogether
                           || type == SocialMemoryType.SharedSuccess
                           || type == SocialMemoryType.SupportedMe;
                case RelationshipClass.Rivalry:
                    return type == SocialMemoryType.InsultedMe
                           || type == SocialMemoryType.WorkedWellTogether
                           || type == SocialMemoryType.FailedTogether
                           || type == SocialMemoryType.BlamedMe
                           || type == SocialMemoryType.HelpedMe;
                case RelationshipClass.Grudge:
                case RelationshipClass.Strained:
                    return type == SocialMemoryType.InsultedMe
                           || type == SocialMemoryType.BlamedMe
                           || type == SocialMemoryType.LetMeDown
                           || type == SocialMemoryType.FailedTogether;
                default:
                    return true;
            }
        }

        static void TallyEvidence(
            IReadOnlyList<SocialMemoryEntry> mem,
            ref int posMaj, ref int negMaj,
            ref int posTypes, ref int negTypes,
            ref float posStr, ref float negStr)
        {
            if (mem == null) return;
            var seenPos = new HashSet<SocialMemoryType>();
            var seenNeg = new HashSet<SocialMemoryType>();
            for (int i = 0; i < mem.Count; i++)
            {
                var e = mem[i];
                if (e == null || e.Strength < 0.25f) continue;
                if (IsPositive(e.Type))
                {
                    posStr += e.Strength;
                    if (e.Major) posMaj++;
                    if (seenPos.Add(e.Type)) posTypes++;
                }
                else if (IsNegative(e.Type))
                {
                    negStr += e.Strength;
                    if (e.Major) negMaj++;
                    if (seenNeg.Add(e.Type)) negTypes++;
                }
            }
        }

        static bool IsPositive(SocialMemoryType t) =>
            t == SocialMemoryType.HelpedMe
            || t == SocialMemoryType.SupportedMe
            || t == SocialMemoryType.WorkedWellTogether
            || t == SocialMemoryType.SharedSuccess
            || t == SocialMemoryType.SharedHardship
            || t == SocialMemoryType.TookMySide
            || t == SocialMemoryType.Apologized;

        static bool IsNegative(SocialMemoryType t) =>
            t == SocialMemoryType.LetMeDown
            || t == SocialMemoryType.BlamedMe
            || t == SocialMemoryType.FailedTogether
            || t == SocialMemoryType.InsultedMe;
    }
}
