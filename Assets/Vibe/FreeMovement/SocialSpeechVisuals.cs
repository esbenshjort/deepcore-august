using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Presentation-only emotional tone for social dialogue bubbles.
    /// Classified from resolved encounter/conflict outcomes — never from intent alone.
    /// </summary>
    public enum SocialSpeechValence : byte
    {
        None = 0,
        Positive = 1,
        Neutral = 2,
        Negative = 3,
        Severe = 4,
    }

    /// <summary>
    /// Maps resolved social outcomes → bubble chrome. No simulation side effects.
    /// </summary>
    public static class SocialSpeechVisuals
    {
        public static readonly Color PositiveAccent = new(0.35f, 0.95f, 0.55f, 1f);
        public static readonly Color NeutralAccent = new(0.35f, 0.9f, 1f, 1f);
        public static readonly Color NegativeAccent = new(1f, 0.72f, 0.28f, 1f);
        public static readonly Color SevereAccent = new(1f, 0.28f, 0.32f, 1f);

        public static Color Accent(SocialSpeechValence v) => v switch
        {
            SocialSpeechValence.Positive => PositiveAccent,
            SocialSpeechValence.Negative => NegativeAccent,
            SocialSpeechValence.Severe => SevereAccent,
            SocialSpeechValence.Neutral => NeutralAccent,
            _ => NeutralAccent,
        };

        public static Color FillTint(SocialSpeechValence v) => v switch
        {
            SocialSpeechValence.Positive => new Color(0.08f, 0.22f, 0.12f, 0.42f),
            SocialSpeechValence.Negative => new Color(0.28f, 0.14f, 0.04f, 0.4f),
            SocialSpeechValence.Severe => new Color(0.32f, 0.04f, 0.06f, 0.55f),
            SocialSpeechValence.Neutral => new Color(0.04f, 0.1f, 0.14f, 0.32f),
            _ => new Color(0f, 0f, 0f, 0.2f),
        };

        public static string Glyph(SocialSpeechValence v) => v switch
        {
            SocialSpeechValence.Positive => "+",
            SocialSpeechValence.Neutral => "·",
            SocialSpeechValence.Negative => "!",
            SocialSpeechValence.Severe => "×",
            _ => "",
        };

        public static string Label(SocialSpeechValence v) => v switch
        {
            SocialSpeechValence.Positive => "POSITIVE",
            SocialSpeechValence.Neutral => "NEUTRAL",
            SocialSpeechValence.Negative => "NEGATIVE",
            SocialSpeechValence.Severe => "SEVERE",
            _ => "NONE",
        };

        /// <summary>
        /// Per-line valence from a resolved aura encounter.
        /// Failed jokes / encouragement resolve Negative (result, not intention).
        /// </summary>
        public static SocialSpeechValence FromAuraLine(SocialEncounterLog log, string role)
        {
            if (log == null) return SocialSpeechValence.Neutral;
            role = role ?? "";

            if (role == "response")
                return FromAuraResponse(log);
            if (role == "closer")
                return FromOutcomeSummary(log.OutcomeSummary);

            return FromAuraInitiator(log);
        }

        static SocialSpeechValence FromAuraInitiator(SocialEncounterLog log)
        {
            string s = log.OutcomeSummary ?? "";
            if (s == "POSITIVE_FAIL")
                return SocialSpeechValence.Negative;
            if (s == "POSITIVE_CONNECT" || s == "SHARED_COMPLAINT_BOND")
                return SocialSpeechValence.Positive;
            if (s == "CLASH")
                return SocialSpeechValence.Negative;
            if (s == "IGNORED_AGGRESSION")
                return SocialSpeechValence.Negative;

            switch (log.Action)
            {
                case SocialAction.Encourage:
                case SocialAction.Joke:
                case SocialAction.Connect:
                    return log.ActionSuccess
                        ? SocialSpeechValence.Positive
                        : SocialSpeechValence.Negative;
                case SocialAction.Provoke:
                case SocialAction.Confront:
                    return SocialSpeechValence.Negative;
                case SocialAction.Complain:
                    return SocialSpeechValence.Negative;
                default:
                    return SocialSpeechValence.Neutral;
            }
        }

        static SocialSpeechValence FromAuraResponse(SocialEncounterLog log)
        {
            string s = log.OutcomeSummary ?? "";
            if (s == "POSITIVE_CONNECT" || s == "SHARED_COMPLAINT_BOND")
                return SocialSpeechValence.Positive;
            if (s == "CLASH")
                return SocialSpeechValence.Negative;
            if (s == "POSITIVE_FAIL")
            {
                // Target annoyed / rejects — visual is the social result
                return log.Response is SocialResponse.PushBack or SocialResponse.Escalate
                    ? SocialSpeechValence.Negative
                    : SocialSpeechValence.Neutral;
            }

            return log.Response switch
            {
                SocialResponse.Accept or SocialResponse.Agree =>
                    log.Action is SocialAction.Provoke or SocialAction.Confront
                        ? SocialSpeechValence.Negative
                        : SocialSpeechValence.Positive,
                SocialResponse.PushBack or SocialResponse.Escalate =>
                    SocialSpeechValence.Negative,
                SocialResponse.Ignore or SocialResponse.Withdraw or SocialResponse.Deflect =>
                    SocialSpeechValence.Neutral,
                _ => SocialSpeechValence.Neutral,
            };
        }

        public static SocialSpeechValence FromOutcomeSummary(string summary)
        {
            if (string.IsNullOrEmpty(summary)) return SocialSpeechValence.Neutral;
            if (summary == "POSITIVE_CONNECT" || summary == "SHARED_COMPLAINT_BOND")
                return SocialSpeechValence.Positive;
            if (summary == "POSITIVE_FAIL" || summary == "CLASH" || summary == "IGNORED_AGGRESSION")
                return SocialSpeechValence.Negative;
            if (summary.StartsWith("EXCHANGE_"))
            {
                if (summary.Contains("Escalate") || summary.Contains("PushBack")
                    || summary.Contains("Provoke") || summary.Contains("Confront"))
                    return SocialSpeechValence.Negative;
                if (summary.Contains("Accept") || summary.Contains("Agree"))
                    return SocialSpeechValence.Positive;
            }
            return SocialSpeechValence.Neutral;
        }

        /// <summary>Per-beat valence from a resolved conflict event (arguments / fights / witnesses).</summary>
        public static SocialSpeechValence FromConflict(SocialConflictEvent ev)
        {
            if (ev == null) return SocialSpeechValence.Neutral;

            if (ev.IsLethal || ev.Outcome == SocialArgumentOutcome.Death
                || ev.Outcome == SocialArgumentOutcome.CriticalInjury
                || ev.FightSeverity >= SocialFightSeverity.Severe
                || ev.IsSevereFight)
                return SocialSpeechValence.Severe;

            if (ev.IsFight || ev.Outcome == SocialArgumentOutcome.FightBreaksOut)
                return SocialSpeechValence.Severe;

            if (ev.IsWitness)
            {
                return ev.WitnessAction switch
                {
                    SocialWitnessAction.DeEscalate or SocialWitnessAction.BreakUpFight =>
                        SocialSpeechValence.Positive,
                    SocialWitnessAction.SupportSomeone or SocialWitnessAction.VerbalIntervene =>
                        SocialSpeechValence.Neutral,
                    _ => SocialSpeechValence.Neutral,
                };
            }

            return ev.Outcome switch
            {
                SocialArgumentOutcome.Apology or SocialArgumentOutcome.PartialResolution =>
                    SocialSpeechValence.Positive,
                SocialArgumentOutcome.BacksDown or SocialArgumentOutcome.MutualDisengage =>
                    SocialSpeechValence.Neutral,
                SocialArgumentOutcome.GrudgeStrengthened
                    or SocialArgumentOutcome.RelationshipWorsens
                    or SocialArgumentOutcome.EscalateFurther =>
                    SocialSpeechValence.Negative,
                _ => SocialSpeechValence.Negative, // active argument beat default
            };
        }

        public static bool WantsEscalationPulse(SocialSpeechValence v) =>
            v == SocialSpeechValence.Severe || v == SocialSpeechValence.Negative;
    }
}
