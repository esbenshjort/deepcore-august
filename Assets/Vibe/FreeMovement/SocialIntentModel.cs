using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    public enum SocialIntentKind : byte
    {
        Avoid = 0,
        Neutral = 1,
        Seek = 2,
    }

    /// <summary>Read-only social affinity/avoidance intent — does not move workers.</summary>
    public readonly struct SocialIntentResult
    {
        public readonly SocialIntentKind Kind;
        public readonly float Score; // -1..+1 approx
        public readonly string Reasons;

        public SocialIntentResult(SocialIntentKind kind, float score, string reasons)
        {
            Kind = kind;
            Score = score;
            Reasons = reasons ?? "";
        }
    }

    /// <summary>
    /// Social affinity / avoidance intent prototype (read-only).
    /// Does not alter schedules, reach, or movement.
    /// </summary>
    public static class SocialIntentModel
    {
        public const float SeekThreshold = 0.18f;
        public const float AvoidThreshold = -0.18f;

        public static SocialIntentResult Evaluate(
            int observerId,
            int targetId,
            SocialDirectedRelation rel,
            IReadOnlyList<SocialMemoryEntry> memoriesToward,
            WorkerState observerState,
            WorkerStats observerSoul)
        {
            if (observerId <= 0 || targetId <= 0 || observerId == targetId || rel == null)
                return new SocialIntentResult(SocialIntentKind.Neutral, 0f, "inactive");

            float trust01 = Mathf.Clamp01((rel.Trust - SocialDirectedRelation.Min)
                                          / (SocialDirectedRelation.Max - SocialDirectedRelation.Min));
            float warmth01 = Mathf.Clamp01((rel.Warmth - SocialDirectedRelation.Min)
                                           / (SocialDirectedRelation.Max - SocialDirectedRelation.Min));
            float host01 = Mathf.Clamp01((rel.Hostility - SocialDirectedRelation.Min)
                                         / (SocialDirectedRelation.Max - SocialDirectedRelation.Min));
            float respect01 = Mathf.Clamp01(rel.Respect / SocialDirectedRelation.RespectMax);

            // Warmth/Trust pull toward Seek; Hostility toward Avoid — but high Respect softens Avoid
            float score = 0f;
            score += (warmth01 - 0.5f) * 0.55f;
            score += (trust01 - 0.5f) * 0.40f;
            float hostPull = (host01 - 0.5f) * 0.55f;
            // Professional rivalry: high hostility + high respect → damp avoid
            float respectSoft = 1f - 0.70f * respect01;
            score -= hostPull * respectSoft;

            float memPos = 0f, memNeg = 0f;
            string topMem = "";
            if (memoriesToward != null)
            {
                for (int i = 0; i < memoriesToward.Count; i++)
                {
                    var e = memoriesToward[i];
                    if (e == null || e.Strength < 0.3f) continue;
                    float w = e.Strength * (e.Significance == SocialMemorySignificance.Major ? 1.2f : 1f);
                    switch (e.Type)
                    {
                        case SocialMemoryType.SupportedMe:
                        case SocialMemoryType.HelpedMe:
                        case SocialMemoryType.SharedHardship:
                        case SocialMemoryType.SharedSuccess:
                        case SocialMemoryType.TookMySide:
                        case SocialMemoryType.WorkedWellTogether:
                        case SocialMemoryType.Apologized:
                            memPos += w;
                            if (string.IsNullOrEmpty(topMem) || e.Strength > 0.6f)
                                topMem = e.Type.ToString();
                            break;
                        case SocialMemoryType.InsultedMe:
                        case SocialMemoryType.BlamedMe:
                        case SocialMemoryType.LetMeDown:
                        case SocialMemoryType.FailedTogether:
                        case SocialMemoryType.HurtBy:
                        case SocialMemoryType.WitnessedViolence:
                        case SocialMemoryType.KilledBy:
                        case SocialMemoryType.WitnessedDeath:
                            memNeg += w;
                            if (string.IsNullOrEmpty(topMem) || e.Strength > 0.6f)
                                topMem = e.Type.ToString();
                            break;
                    }
                }
            }
            score += Mathf.Clamp01(memPos / 2.2f) * 0.22f;
            score -= Mathf.Clamp01(memNeg / 2.2f) * 0.28f;

            // Emotional state of observer
            float fr = observerState != null ? observerState.Frustration / 100f : 0f;
            float mo = observerState != null ? observerState.Morale / 100f : 0.5f;
            score += (mo - 0.5f) * 0.12f;
            score -= fr * 0.10f; // frustrated → slightly more avoidant

            // Soul: Affinity/Empathy soft Seek; Composure resists Avoid spikes; Tolerance softens Avoid
            if (observerSoul != null)
            {
                float aff = observerSoul.Get(WorkerStatId.Affinity) / 20f;
                float emp = observerSoul.Get(WorkerStatId.Empathy) / 20f;
                float tol = observerSoul.Get(WorkerStatId.Tolerance) / 20f;
                float comp = observerSoul.Get(WorkerStatId.Composure) / 20f;
                score += (aff - 0.5f) * 0.10f;
                score += (emp - 0.5f) * 0.08f;
                score += (tol - 0.5f) * 0.06f;
                // composure reduces magnitude of extreme avoid when hostility high
                if (score < 0f)
                    score *= Mathf.Lerp(1f, 0.75f, comp);
            }

            score = Mathf.Clamp(score, -1f, 1f);

            SocialIntentKind kind;
            if (score >= SeekThreshold) kind = SocialIntentKind.Seek;
            else if (score <= AvoidThreshold) kind = SocialIntentKind.Avoid;
            else kind = SocialIntentKind.Neutral;

            // Edge: high H + high R stays Neutral/professional unless memories push hard
            if (rel.Hostility >= 6f && rel.Respect >= 62f && kind == SocialIntentKind.Avoid
                && memNeg < 1.0f)
            {
                kind = SocialIntentKind.Neutral;
                score = Mathf.Max(score, AvoidThreshold + 0.02f);
            }

            var sb = new StringBuilder(96);
            sb.Append($"T={rel.Trust:0.0} W={rel.Warmth:0.0} H={rel.Hostility:0.0} R={rel.Respect:0.0}");
            sb.Append($" mem+{memPos:0.00}/-{memNeg:0.00}");
            if (!string.IsNullOrEmpty(topMem)) sb.Append($" [{topMem}]");
            if (rel.Hostility >= 6f && rel.Respect >= 62f) sb.Append(" rivalry→pro");
            sb.Append($" score={score:+0.00;-0.00}");

            return new SocialIntentResult(kind, score, sb.ToString());
        }
    }
}
