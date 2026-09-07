using System;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Presentation-only lines for argument / fight / witness beats.
    /// Does not affect Social Conflict math. Escalation wording is intentional.
    /// </summary>
    public static class SocialConflictLineBank
    {
        static readonly System.Random Rng = new(9103);

        public static string PickArgumentLine(
            SocialArgumentPhase phase,
            SocialAction move,
            SocialResponse resp,
            bool moveOk,
            SocialArgumentOutcome outcome)
        {
            if (outcome == SocialArgumentOutcome.Apology)
                return Pick(ApologyLines);

            if (outcome == SocialArgumentOutcome.BacksDown)
                return Pick(BacksDownLines);

            if (outcome == SocialArgumentOutcome.PartialResolution)
                return Pick(PartialResolutionLines);

            if (outcome == SocialArgumentOutcome.MutualDisengage)
                return Pick(MutualDisengageLines);

            if (outcome == SocialArgumentOutcome.CriticalInjury)
                return PickCriticalInjuryLine(victimSpeaks: true);

            if (outcome == SocialArgumentOutcome.Death)
                return PickDeathLine(killerSpeaks: false);

            switch (phase)
            {
                case SocialArgumentPhase.Opening:
                    return PickOpening(move);
                case SocialArgumentPhase.Exchange:
                    return PickExchange(move, resp, moveOk);
                case SocialArgumentPhase.Peak:
                    return PickPeak(move, resp, outcome);
                case SocialArgumentPhase.Resolving:
                    return Pick(ResolvingLines);
                default:
                    return PickOutcomeCloser(outcome);
            }
        }

        public static string PickOutcomeCloser(SocialArgumentOutcome outcome) =>
            outcome switch
            {
                SocialArgumentOutcome.Apology => Pick(new[]
                {
                    "Apology stands. Don't make me repeat it.",
                    "I said it. Leave it buried.",
                    "That's as soft as I get.",
                }),
                SocialArgumentOutcome.BacksDown => Pick(new[]
                {
                    "Noted. Don't push it.",
                    "You got the last word. Use it quiet.",
                    "I'm out. Stay that way.",
                }),
                SocialArgumentOutcome.PartialResolution => Pick(new[]
                {
                    "Truce. Temporary.",
                    "Parked — not forgiven.",
                    "We stop. That's all.",
                }),
                SocialArgumentOutcome.MutualDisengage => Pick(new[]
                {
                    "Distance. Good.",
                    "Walk opposite ways.",
                    "Space. Keep it.",
                }),
                SocialArgumentOutcome.GrudgeStrengthened => Pick(new[]
                {
                    "I'll remember that.",
                    "Logged. Cold.",
                    "This one's staying with me.",
                }),
                SocialArgumentOutcome.RelationshipWorsens => Pick(new[]
                {
                    "We're worse for this.",
                    "Something broke. Not fixing it tonight.",
                    "Don't ask me for cover after that.",
                }),
                SocialArgumentOutcome.EscalateFurther => Pick(new[]
                {
                    "You want worse? Keep talking.",
                    "We're not done. Not even close.",
                    "Next word's a mistake.",
                }),
                SocialArgumentOutcome.FightBreaksOut => Pick(new[]
                {
                    "Hands. Now.",
                    "Talk's over.",
                    "Come on then.",
                }),
                SocialArgumentOutcome.CriticalInjury => Pick(new[]
                {
                    "Stay down. Don't move.",
                    "That's bad. Hold still.",
                    "…Shit. Get help.",
                }),
                SocialArgumentOutcome.Death => Pick(new[]
                {
                    "…They're gone.",
                    "No pulse. Stop.",
                    "It's done. Don't look.",
                }),
                _ => "…",
            };

        public static string PickFightLine(SocialFightBeat beat, bool ok) =>
            beat switch
            {
                SocialFightBeat.Shove => ok
                    ? Pick(new[]
                    {
                        "Back off!",
                        "Get off me!",
                        "Out of my face!",
                        "Move!",
                    })
                    : Pick(new[]
                    {
                        "Missed — still standing.",
                        "That all?",
                        "Nice try.",
                        "Barely felt it.",
                    }),
                SocialFightBeat.Grab => ok
                    ? Pick(new[]
                    {
                        "Hold still—",
                        "I've got you—",
                        "Stay put—",
                        "Don't twist—",
                    })
                    : Pick(new[]
                    {
                        "Don't grab me!",
                        "Hands off!",
                        "Let go!",
                        "Get your hands—",
                    }),
                SocialFightBeat.Punch => ok
                    ? Pick(new[]
                    {
                        "—!",
                        "That one counted.",
                        "Taste that.",
                        "Stay down.",
                    })
                    : Pick(new[]
                    {
                        "Swing and miss.",
                        "Lucky.",
                        "Air. That's all you got.",
                        "Whiffed it.",
                    }),
                SocialFightBeat.Retaliate => ok
                    ? Pick(new[]
                    {
                        "Same to you!",
                        "Take it back!",
                        "You first!",
                        "Paid in kind!",
                    })
                    : Pick(new[]
                    {
                        "Stay down— wait—",
                        "Not finished!",
                        "Come back here—",
                        "Don't you walk—",
                    }),
                SocialFightBeat.BackAway => ok
                    ? Pick(new[]
                    {
                        "I'm out.",
                        "Space. Now.",
                        "Done. Back off.",
                        "Walking. Don't follow.",
                    })
                    : Pick(new[]
                    {
                        "Can't just walk—",
                        "Still on me!",
                        "Path's blocked—",
                        "They won't let go—",
                    }),
                SocialFightBeat.BreakApart => ok
                    ? Pick(new[]
                    {
                        "Enough!",
                        "Break — both of you!",
                        "Split. Now.",
                        "Hands down!",
                    })
                    : Pick(new[]
                    {
                        "Not yet—",
                        "Hold—",
                        "Can't pry them—",
                        "Still locked—",
                    }),
                _ => "…",
            };

        /// <summary>Heavy beat lines when a fight turns ugly — no numbers.</summary>
        public static string PickSevereFightLine() =>
            Pick(new[]
            {
                "Wrong move.",
                "You asked for this.",
                "Feel that?",
                "Stay on the ground.",
                "Don't get up.",
                "That one's lasting.",
                "Harder next time.",
                "You done yet?",
                "Keep coming — I dare you.",
                "Bones remember.",
                "Shut up and bleed.",
                "Last warning was talk.",
            });

        public static string PickCriticalInjuryLine(bool victimSpeaks) =>
            victimSpeaks
                ? Pick(new[]
                {
                    "Can't— breathe—",
                    "Something's wrong—",
                    "Don't… touch it—",
                    "I need— a minute—",
                    "…Hurts bad.",
                    "Legs won't—",
                })
                : Pick(new[]
                {
                    "Hey — look at me.",
                    "Stay with it. Pressure.",
                    "Don't move. Hear me?",
                    "That's not a scratch.",
                    "We need a medic. Now.",
                    "Hold still. You're leaking.",
                });

        public static string PickDeathLine(bool killerSpeaks) =>
            killerSpeaks
                ? Pick(new[]
                {
                    "…I didn't mean—",
                    "Get up. Get up.",
                    "No. No no—",
                    "It was supposed to stop.",
                    "…Christ.",
                })
                : Pick(new[]
                {
                    "They're not breathing.",
                    "Gone.",
                    "Don't shake them. It's over.",
                    "…Quiet now.",
                    "Somebody cover them.",
                });

        public static string PickPostFightLine(bool injured, bool lethal)
        {
            if (lethal)
                return Pick(new[]
                {
                    "Nobody talks about this.",
                    "Clean it. Then we leave.",
                    "Shift just got longer.",
                    "Remember their name. That's all.",
                    "Eyes forward. Walk.",
                });
            if (injured)
                return Pick(new[]
                {
                    "You're walking funny. Sit.",
                    "Wrap that before it stiffens.",
                    "We both look like hell.",
                    "Next time we talk, not hit.",
                    "Ice. Quiet. Don't limp past the boss.",
                    "That bruise'll teach you nothing.",
                });
            return Pick(new[]
            {
                "Dust off. Don't stare.",
                "We're done. Tools up.",
                "Same tunnel tomorrow. Act like it.",
                "Nobody won. Move.",
                "Shake it off. Work's waiting.",
            });
        }

        public static string PickInterventionReaction(bool success, bool duringFight)
        {
            if (duringFight)
            {
                return success
                    ? Pick(new[]
                    {
                        "Break it up!",
                        "Step apart — now.",
                        "Hands off. Both of you.",
                        "Hands off — both!",
                        "That's a write-up if I have to ask twice.",
                        "Enough blood for one shift!",
                        "Split or I write you both!",
                        "Back. Now.",
                    })
                    : Pick(new[]
                    {
                        "Get— off—",
                        "Can't hold them—",
                        "They won't stop—",
                        "Get— off— me—",
                        "Words are dead here!",
                        "Somebody help—",
                    });
            }

            return success
                ? Pick(new[]
                {
                    "Hey — both of you. Cut it.",
                    "That's enough. Tools down, mouths shut.",
                    "Crew first. Squabble later.",
                    "That's enough. Mouths shut.",
                    "Breathe. Both of you. Work's still here.",
                    "We bury this before someone gets hurt.",
                    "We bury this before it gets teeth.",
                    "Look at me — not each other.",
                })
                : Pick(new[]
                {
                    "I said— …forget it.",
                    "Nobody's listening.",
                    "They're past talking.",
                    "Words aren't landing.",
                    "Shouldn't have opened my mouth.",
                    "…Not my fight anymore.",
                });
        }

        public static string PickWitnessLine(SocialWitnessAction action, bool ok, bool fighting) =>
            action switch
            {
                SocialWitnessAction.VerbalIntervene =>
                    PickInterventionReaction(ok, duringFight: fighting),
                SocialWitnessAction.SupportSomeone => ok
                    ? Pick(new[]
                    {
                        "They're not wrong. Back off.",
                        "I've got your back — stand down.",
                        "Leave them alone.",
                        "Pick on someone else.",
                        "You're outnumbered if you keep this up.",
                    })
                    : Pick(new[]
                    {
                        "…Not my fight.",
                        "Shouldn't have opened my mouth.",
                        "Forget I said anything.",
                        "I'm not dying on this hill.",
                    }),
                SocialWitnessAction.DeEscalate =>
                    PickInterventionReaction(ok, duringFight: fighting),
                SocialWitnessAction.BreakUpFight =>
                    PickInterventionReaction(ok, duringFight: true),
                _ => Pick(new[]
                {
                    "…",
                    "Not looking.",
                    "None of my business.",
                    "Keep walking.",
                    "Didn't see it.",
                }),
            };

        static readonly string[] ApologyLines =
        {
            "Alright. I was out of line.",
            "…Fine. I shouldn't have said that.",
            "Okay. That one's on me.",
            "I crossed it. Leaving it here.",
            "You're right. I snapped.",
            "Sorry. Won't dress it up.",
            "I was wrong. That's it.",
        };

        static readonly string[] BacksDownLines =
        {
            "…Whatever. Drop it.",
            "I'm done. You win this round.",
            "Fine. Walking away.",
            "Not worth my teeth.",
            "You want it? Take it. I'm out.",
            "Backing off. Don't gloat.",
            "Alright. Hands down.",
        };

        static readonly string[] PartialResolutionLines =
        {
            "We're not fixed. But we stop here.",
            "Enough heat. Same crew tomorrow.",
            "Park it. Shift still needs us.",
            "Truce till we can stand each other.",
            "Call it even for now.",
            "We pause. Not friends. Functional.",
        };

        static readonly string[] MutualDisengageLines =
        {
            "Space. Now.",
            "We're both walking. That's the deal.",
            "Not worth finishing this underground.",
            "Opposite directions.",
            "Cool off. Separate tunnels.",
            "Neither of us finishes this today.",
        };

        static readonly string[] ResolvingLines =
        {
            "Last word, then we bury it.",
            "Say it clean and we move on.",
            "One more — then tools, not teeth.",
            "Finish the sentence. Then we're done.",
            "Spit it. Then walk.",
        };

        static readonly string[] IrritationLines =
        {
            "You've been chewing on this all shift.",
            "That tone again.",
            "I'm not your punching bag.",
            "Quit needling me.",
            "Every word's a dig with you.",
            "You love picking scabs.",
        };

        static readonly string[] GrudgeLines =
        {
            "Mark this. I won't forget.",
            "Same old poison.",
            "We're past fixing today.",
            "I'll carry that.",
            "You earned this cold.",
            "Don't expect cover from me.",
            "This sits between us now.",
        };

        static readonly string[] ConfrontationLines =
        {
            "We're not burying this. Say it.",
            "You've been on me all shift — enough.",
            "Right here. Right now.",
            "Look at me when you say that.",
            "Stop dancing. Name it.",
            "Say what you mean or shut it.",
        };

        static readonly string[] EscalationLines =
        {
            "You want hands? Keep talking.",
            "One more word and this stops being talk.",
            "I'm done being polite.",
            "You want worse? Keep talking.",
            "Don't talk to me like that.",
            "Say that again.",
            "You want a real problem? Keep going.",
            "Next step's not words.",
        };

        static string PickOpening(SocialAction move) =>
            move switch
            {
                SocialAction.Confront => Pick(ConfrontationLines),
                SocialAction.Provoke => Pick(new[]
                {
                    "Go on. Say it to my face.",
                    "That smirk again? Try me.",
                    "You think I won't?",
                    "Push. See what happens.",
                    "Come on. Finish the insult.",
                    "I've got time. You don't.",
                }),
                SocialAction.Complain => Pick(new[]
                {
                    "I'm sick of carrying your slack.",
                    "Every jam somehow lands on me.",
                    "This isn't a partnership — it's a problem.",
                    "You dump, I clean. Done with it.",
                    "My back's not your shelf.",
                    "Stop sticking me with your mess.",
                }),
                SocialAction.Connect => Pick(new[]
                {
                    "Hold up — before this gets ugly.",
                    "We can still talk this down.",
                    "Listen. One minute. Then we decide.",
                    "Easy. I'm not here to break you.",
                    "Give me a sentence without teeth.",
                }),
                _ => Pick(new[]
                {
                    "We're talking. Whether you like it.",
                    "You've been chewing on this all shift.",
                    "That tone again.",
                    "I'm not your punching bag.",
                }),
            };

        static string PickExchange(SocialAction move, SocialResponse resp, bool moveOk)
        {
            if (resp == SocialResponse.Escalate)
                return Pick(EscalationLines);
            if (resp == SocialResponse.PushBack)
                return Pick(new[]
                {
                    "Push me and I'll push back.",
                    "No. You don't get that free.",
                    "Wrong person to lean on.",
                    "Try that tone elsewhere.",
                    "I push back. Habit.",
                    "Don't test the edge.",
                });
            if (move == SocialAction.Connect && moveOk)
                return Pick(new[]
                {
                    "I'm trying to pull this back.",
                    "We don't have to finish ugly.",
                    "Meet me halfway. One step.",
                    "Talk. Then we decide if we hate each other.",
                });
            if (move == SocialAction.Complain)
                return Pick(IrritationLines);
            return Pick(new[]
            {
                "You heard me.",
                "That's the part you skip every time.",
                "Don't rewrite what happened.",
                "I said what I said.",
                "Own your half.",
                "Still waiting on honesty.",
            });
        }

        static string PickPeak(SocialAction move, SocialResponse resp, SocialArgumentOutcome outcome)
        {
            if (outcome == SocialArgumentOutcome.EscalateFurther)
                return Pick(EscalationLines);
            if (outcome == SocialArgumentOutcome.GrudgeStrengthened)
                return Pick(GrudgeLines);
            if (outcome == SocialArgumentOutcome.FightBreaksOut)
                return Pick(new[]
                {
                    "Hands. Now.",
                    "Talk's dead.",
                    "Come on then.",
                    "You wanted this.",
                });
            if (resp == SocialResponse.Escalate)
                return Pick(EscalationLines);
            return Pick(new[]
            {
                "That's it — the line.",
                "No more soft landings.",
                "You crossed it.",
                "Last chance to walk.",
                "This is the peak. Choose.",
            });
        }

        static string Pick(string[] lines)
        {
            if (lines == null || lines.Length == 0) return "…";
            return lines[Rng.Next(0, lines.Length)];
        }
    }
}
