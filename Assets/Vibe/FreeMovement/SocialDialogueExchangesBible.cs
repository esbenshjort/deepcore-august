namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Dialogue Bible V2.1 — curated exchange families (presentation only).
    /// Continues SocialDialogueExchanges; called from EnsureBuilt after Build().
    /// </summary>
    public static partial class SocialDialogueExchanges
    {
        static void BuildBibleV21()
        {
            // ——— ORDINARY TUNNEL BANTER ———
            Add(SocialDialogueTopic.General, SocialAction.Joke,
                "Anyone know what day it is?",
                new[]
                {
                    R(SocialResponse.Accept, "Underground."),
                    R(SocialResponse.Agree, "Payday-adjacent."),
                    R(SocialResponse.Deflect, "Does it matter?"),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.PushBack, "Dig day. Same as yesterday.", true),
                },
                new[] { "Helpful.", "Fair." });

            Add(SocialDialogueTopic.General, SocialAction.Connect,
                "You hear that?",
                new[]
                {
                    R(SocialResponse.Accept, "Hear what?"),
                    R(SocialResponse.Agree, "…Yeah. That."),
                    R(SocialResponse.Deflect, "Rock settling."),
                    R(SocialResponse.Ignore, "Mm."),
                    R(SocialResponse.Escalate, "Don't start."),
                });

            Add(SocialDialogueTopic.General, SocialAction.Joke,
                "Smells different down here.",
                new[]
                {
                    R(SocialResponse.Accept, "That's Kowalski."),
                    R(SocialResponse.Agree, "Wet rock and bad decisions."),
                    R(SocialResponse.Deflect, "Breathe through your mouth."),
                    R(SocialResponse.PushBack, "Fuck off."),
                    R(SocialResponse.Ignore, "…"),
                },
                new[] { "Charming.", "Noted." });

            Add(SocialDialogueTopic.General, SocialAction.Connect,
                "You ever think about quitting?",
                new[]
                {
                    R(SocialResponse.Accept, "Every morning."),
                    R(SocialResponse.Agree, "Then payday arrives."),
                    R(SocialResponse.Deflect, "Ask me after this cut."),
                    R(SocialResponse.Withdraw, "…Sometimes."),
                    R(SocialResponse.PushBack, "Don't tempt me."),
                });

            Add(SocialDialogueTopic.General, SocialAction.Connect,
                "Quiet today.",
                new[]
                {
                    R(SocialResponse.Accept, "You complaining?"),
                    R(SocialResponse.Agree, "Don't ruin it."),
                    R(SocialResponse.Deflect, "Enjoy it."),
                    R(SocialResponse.Ignore, "Mm."),
                    R(SocialResponse.PushBack, "No."),
                });

            Add(SocialDialogueTopic.General, SocialAction.Joke,
                "I miss weather.",
                new[]
                {
                    R(SocialResponse.Accept, "I don't."),
                    R(SocialResponse.Agree, "Rain. Sky. Options."),
                    R(SocialResponse.Deflect, "We've got drip. Close enough."),
                    R(SocialResponse.PushBack, "Miss people less.", true),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Work, SocialAction.Connect,
                "Bit's biting clean for once.",
                new[]
                {
                    R(SocialResponse.Accept, "Don't say it out loud."),
                    R(SocialResponse.Agree, "Jinx and we eat dust."),
                    R(SocialResponse.Deflect, "Enjoy the miracle."),
                    R(SocialResponse.Ignore, "Mm."),
                },
                new[] { "Luxury." });

            Add(SocialDialogueTopic.Work, SocialAction.Complain,
                "This face is arguing with me.",
                new[]
                {
                    R(SocialResponse.Agree, "I heard the screaming."),
                    R(SocialResponse.Accept, "Hard layer. Mark it."),
                    R(SocialResponse.Deflect, "Thought that was enthusiasm."),
                    R(SocialResponse.PushBack, "Then dig quieter."),
                    R(SocialResponse.Ignore, "…"),
                });

            // ——— DARKNESS ———
            Add(SocialDialogueTopic.Darkness, SocialAction.Complain,
                "Light's getting thin.",
                new[]
                {
                    R(SocialResponse.Accept, "Still enough."),
                    R(SocialResponse.Agree, "That's not comforting."),
                    R(SocialResponse.Deflect, "Helmet first. Panic later."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.Withdraw, "I hate this stretch."),
                });

            Add(SocialDialogueTopic.Darkness, SocialAction.Joke,
                "Can't see shit.",
                new[]
                {
                    R(SocialResponse.Accept, "There's rock in front of you."),
                    R(SocialResponse.Agree, "Thanks, Lewis."),
                    R(SocialResponse.Deflect, "Feel for the wall."),
                    R(SocialResponse.PushBack, "Lamp up.", true),
                    R(SocialResponse.Ignore, "Mhm."),
                });

            Add(SocialDialogueTopic.Darkness, SocialAction.Connect,
                "Something moved.",
                new[]
                {
                    R(SocialResponse.Accept, "Probably you."),
                    R(SocialResponse.Agree, "I saw it too."),
                    R(SocialResponse.Deflect, "Then stop looking."),
                    R(SocialResponse.Escalate, "Don't."),
                    R(SocialResponse.Withdraw, "…"),
                });

            Add(SocialDialogueTopic.Darkness, SocialAction.Complain,
                "I fucking hate the dark.",
                new[]
                {
                    R(SocialResponse.Agree, "Good career choice."),
                    R(SocialResponse.Accept, "Same."),
                    R(SocialResponse.Deflect, "Lamp. Then feelings."),
                    R(SocialResponse.PushBack, "Then leave."),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Darkness, SocialAction.Joke,
                "Lamp dies, I'm going home.",
                new[]
                {
                    R(SocialResponse.Accept, "You know the way?"),
                    R(SocialResponse.Agree, "Fuck."),
                    R(SocialResponse.Deflect, "Spare battery. Maybe."),
                    R(SocialResponse.PushBack, "Cute threat.", true),
                    R(SocialResponse.Ignore, "Mm."),
                },
                new[] { "Fuck.", "Fair." });

            // ——— NARROW / CLAUSTROPHOBIA ———
            Add(SocialDialogueTopic.NarrowTunnel, SocialAction.Complain,
                "This gets any tighter, I'm charging it rent.",
                new[]
                {
                    R(SocialResponse.Accept, "You'd have to live here first."),
                    R(SocialResponse.Agree, "Feels like I fucking do."),
                    R(SocialResponse.Deflect, "Width-1. Character building."),
                    R(SocialResponse.PushBack, "Then back out."),
                    R(SocialResponse.Escalate, "Scared already?"),
                },
                new[] { "Luxury." });

            Add(SocialDialogueTopic.NarrowTunnel, SocialAction.Complain,
                "Who planned this?",
                new[]
                {
                    R(SocialResponse.Accept, "Manager."),
                    R(SocialResponse.Agree, "Of course."),
                    R(SocialResponse.Deflect, "Geology. Blame the rock."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.PushBack, "You volunteered."),
                });

            Add(SocialDialogueTopic.NarrowTunnel, SocialAction.Connect,
                "Can't turn around.",
                new[]
                {
                    R(SocialResponse.Accept, "Then don't."),
                    R(SocialResponse.Agree, "Forward only. Fun."),
                    R(SocialResponse.Deflect, "Breathe sideways."),
                    R(SocialResponse.Withdraw, "I need out."),
                    R(SocialResponse.PushBack, "Keep moving."),
                });

            Add(SocialDialogueTopic.Confinement, SocialAction.Connect,
                "I don't like this.",
                new[]
                {
                    R(SocialResponse.Accept, "I know."),
                    R(SocialResponse.Agree, "Same."),
                    R(SocialResponse.Deflect, "Don't say it like that."),
                    R(SocialResponse.Withdraw, "…"),
                    R(SocialResponse.PushBack, "Then dig faster."),
                }, bonded: true);

            Add(SocialDialogueTopic.Confinement, SocialAction.Connect,
                "How are you fine in here?",
                new[]
                {
                    R(SocialResponse.Accept, "I don't think about it."),
                    R(SocialResponse.Agree, "Practice. Bad practice."),
                    R(SocialResponse.Deflect, "Ask me at camp."),
                    R(SocialResponse.PushBack, "I'm not fine. I'm busy."),
                    R(SocialResponse.Ignore, "Mm."),
                });

            Add(SocialDialogueTopic.NarrowTunnel, SocialAction.Joke,
                "Left a shoulder at the last bend.",
                new[]
                {
                    R(SocialResponse.Accept, "Pick it up on the way back."),
                    R(SocialResponse.Agree, "Dignity's cheaper."),
                    R(SocialResponse.Deflect, "Keep your elbows in."),
                    R(SocialResponse.PushBack, "Not funny.", true),
                    R(SocialResponse.Ignore, "…"),
                });

            // ——— FRIEND TEASING CONFINEMENT WEAKNESS ———
            Add(SocialDialogueTopic.WeakPoint, SocialAction.Joke,
                "Bit narrow ahead.",
                new[]
                {
                    R(SocialResponse.PushBack, "Don't."),
                    R(SocialResponse.Accept, "I noticed."),
                    R(SocialResponse.Agree, "Your face did the talking."),
                    R(SocialResponse.Escalate, "Fuck off."),
                    R(SocialResponse.Deflect, "Lamp. Then talk."),
                    R(SocialResponse.Withdraw, "…"),
                },
                // Friend tease: memory-gated (WasTrapped), not NeedsWeak — V2 weak gate is hostile-only.
                bonded: true,
                mem: SocialMemoryType.WasTrapped, needsMem: true, weight: 0.85f);

            Add(SocialDialogueTopic.Callback, SocialAction.Joke,
                "Need me to hold your hand?",
                new[]
                {
                    R(SocialResponse.PushBack, "Need me to break yours?"),
                    R(SocialResponse.Accept, "Funny. Almost."),
                    R(SocialResponse.Escalate, "Say that again."),
                    R(SocialResponse.Deflect, "Not today."),
                    R(SocialResponse.Ignore, "…"),
                },
                bonded: true,
                mem: SocialMemoryType.WasTrapped, needsMem: true, weight: 0.75f);

            Add(SocialDialogueTopic.WeakPoint, SocialAction.Joke,
                "Width one.",
                new[]
                {
                    R(SocialResponse.Escalate, "Fuck off."),
                    R(SocialResponse.PushBack, "That's not a no."),
                    R(SocialResponse.Accept, "I heard."),
                    R(SocialResponse.Deflect, "Keep walking."),
                    R(SocialResponse.Withdraw, "Mm."),
                },
                bonded: true,
                mem: SocialMemoryType.WasTrapped, needsMem: true, weight: 0.8f);

            Add(SocialDialogueTopic.Callback, SocialAction.Connect,
                "Send the mole.",
                new[]
                {
                    R(SocialResponse.Accept, "I heard that."),
                    R(SocialResponse.Agree, "Hilarious."),
                    R(SocialResponse.PushBack, "Don't."),
                    R(SocialResponse.Escalate, "One more."),
                    R(SocialResponse.Deflect, "After you."),
                },
                bonded: true,
                mem: SocialMemoryType.WasTrapped, needsMem: true, weight: 0.55f);

            // ——— HOSTILE CONFINEMENT ———
            Add(SocialDialogueTopic.WeakPoint, SocialAction.Provoke,
                "Careful. Walls are getting close.",
                new[]
                {
                    R(SocialResponse.Escalate, "Shut your mouth."),
                    R(SocialResponse.PushBack, "Keep checking."),
                    R(SocialResponse.Deflect, "Not worth it."),
                    R(SocialResponse.Withdraw, "…"),
                    R(SocialResponse.Agree, "Hilarious. Really.", true),
                },
                hostile: true, weak: SocialWeakPointKind.Fear, weight: 1.15f);

            Add(SocialDialogueTopic.WeakPoint, SocialAction.Provoke,
                "Want me to call someone when you freeze again?",
                new[]
                {
                    R(SocialResponse.Escalate, "Say it again."),
                    R(SocialResponse.PushBack, "Fuck off."),
                    R(SocialResponse.Deflect, "Walk."),
                    R(SocialResponse.Withdraw, "…"),
                    R(SocialResponse.Accept, "There she is."),
                },
                hostile: true, weak: SocialWeakPointKind.Fear,
                mem: SocialMemoryType.WasTrapped, needsMem: true, weight: 1.2f);

            Add(SocialDialogueTopic.Confinement, SocialAction.Provoke,
                "Just checking you're still breathing.",
                new[]
                {
                    R(SocialResponse.Escalate, "You looking for a fight?"),
                    R(SocialResponse.PushBack, "One more word."),
                    R(SocialResponse.Deflect, "Cute."),
                    R(SocialResponse.Withdraw, "…"),
                    R(SocialResponse.Ignore, "No."),
                },
                hostile: true, weak: SocialWeakPointKind.Fear, weight: 1.1f);

            // ——— EXCAVATION ———
            Add(SocialDialogueTopic.Work, SocialAction.Joke,
                "How much rock you planning to murder today?",
                new[]
                {
                    R(SocialResponse.Accept, "All of it."),
                    R(SocialResponse.Agree, "Good talk."),
                    R(SocialResponse.Deflect, "Ask the bit."),
                    R(SocialResponse.PushBack, "Less poetry. More cut.", true),
                    R(SocialResponse.Ignore, "Mm."),
                });

            Add(SocialDialogueTopic.Work, SocialAction.Complain,
                "Hard layer.",
                new[]
                {
                    R(SocialResponse.Agree, "I noticed."),
                    R(SocialResponse.Accept, "Thought the screaming was enthusiasm."),
                    R(SocialResponse.Deflect, "Cool the bit."),
                    R(SocialResponse.PushBack, "Then switch faces."),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Work, SocialAction.Connect,
                "Machine sounds wrong.",
                new[]
                {
                    R(SocialResponse.Accept, "Machine always sounds wrong."),
                    R(SocialResponse.Agree, "That sounded more wrong."),
                    R(SocialResponse.Deflect, "Kill it. Check bearings."),
                    R(SocialResponse.PushBack, "It's fine until it isn't."),
                    R(SocialResponse.Escalate, "Don't ignore that."),
                });

            Add(SocialDialogueTopic.Work, SocialAction.Joke,
                "You ever get tired of digging?",
                new[]
                {
                    R(SocialResponse.Accept, "You ever get tired of talking?"),
                    R(SocialResponse.Agree, "No. There's your answer."),
                    R(SocialResponse.Deflect, "Ask me after shift."),
                    R(SocialResponse.PushBack, "Dig.", true),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Work, SocialAction.Encourage,
                "Clean cut. Don't get cocky.",
                new[]
                {
                    R(SocialResponse.Accept, "Wasn't planning to."),
                    R(SocialResponse.Agree, "I'll take clean."),
                    R(SocialResponse.Deflect, "Don't jinx it."),
                    R(SocialResponse.Ignore, "Mm."),
                });

            // ——— PROSPECTOR ———
            Add(SocialDialogueTopic.Prospecting, SocialAction.Connect,
                "Anything?",
                new[]
                {
                    R(SocialResponse.Accept, "Something."),
                    R(SocialResponse.Agree, "Rock something."),
                    R(SocialResponse.Deflect, "Ask again in an hour."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.PushBack, "I'm working."),
                });

            Add(SocialDialogueTopic.Prospecting, SocialAction.Connect,
                "How confident are you?",
                new[]
                {
                    R(SocialResponse.Accept, "Enough to dig."),
                    R(SocialResponse.Agree, "That's not a number."),
                    R(SocialResponse.Deflect, "Exactly."),
                    R(SocialResponse.PushBack, "More than you."),
                    R(SocialResponse.Ignore, "Mm."),
                });

            Add(SocialDialogueTopic.Prospecting, SocialAction.Confront,
                "You said east yesterday.",
                new[]
                {
                    R(SocialResponse.Accept, "The evidence changed."),
                    R(SocialResponse.Agree, "My opinion did."),
                    R(SocialResponse.Deflect, "Rock moved. Sort of."),
                    R(SocialResponse.PushBack, "Write your own map."),
                    R(SocialResponse.Escalate, "You dig where you want."),
                });

            Add(SocialDialogueTopic.Prospecting, SocialAction.Joke,
                "So?",
                new[]
                {
                    R(SocialResponse.Accept, "Interesting."),
                    R(SocialResponse.Agree, "I fucking hate when you say that."),
                    R(SocialResponse.Deflect, "Means maybe."),
                    R(SocialResponse.PushBack, "Means dig.", true),
                    R(SocialResponse.Ignore, "…"),
                });

            // ——— FAILED PROSPECTING ———
            Add(SocialDialogueTopic.Prospecting, SocialAction.Complain,
                "Still nothing?",
                new[]
                {
                    R(SocialResponse.Accept, "No."),
                    R(SocialResponse.Agree, "Expensive machine."),
                    R(SocialResponse.Deflect, "Excellent observation."),
                    R(SocialResponse.PushBack, "Another data point."),
                    R(SocialResponse.Ignore, "…"),
                }, weight: 0.9f);

            Add(SocialDialogueTopic.Prospecting, SocialAction.Complain,
                "Another dead end?",
                new[]
                {
                    R(SocialResponse.Accept, "Another data point."),
                    R(SocialResponse.Agree, "That's Prospector for 'yes.'"),
                    R(SocialResponse.Deflect, "Mark it. Move."),
                    R(SocialResponse.PushBack, "You dig empty tunnels too."),
                    R(SocialResponse.Withdraw, "…"),
                });

            Add(SocialDialogueTopic.Prospecting, SocialAction.Confront,
                "You sure there's gold down here?",
                new[]
                {
                    R(SocialResponse.Accept, "No."),
                    R(SocialResponse.Agree, "Finally, some confidence."),
                    R(SocialResponse.Deflect, "Sure enough to keep looking."),
                    R(SocialResponse.PushBack, "Want surface work?"),
                    R(SocialResponse.Escalate, "Ask Manager."),
                });

            Add(SocialDialogueTopic.Prospecting, SocialAction.Joke,
                "Scan hummed. Rock stayed boring.",
                new[]
                {
                    R(SocialResponse.Accept, "Honest rock."),
                    R(SocialResponse.Agree, "I hate honest rock."),
                    R(SocialResponse.Deflect, "Next pocket."),
                    R(SocialResponse.PushBack, "Then stop humming.", true),
                    R(SocialResponse.Ignore, "Mm."),
                });

            // ——— DISCOVERY / GOLD / DIAMONDS ———
            Add(SocialDialogueTopic.Discovery, SocialAction.Connect,
                "That's gold.",
                new[]
                {
                    R(SocialResponse.Accept, "I know."),
                    R(SocialResponse.Agree, "Just wanted to hear somebody say it."),
                    R(SocialResponse.Deflect, "Bag it. Don't lose it."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.PushBack, "Don't celebrate yet."),
                },
                new[] { "Luxury." });

            Add(SocialDialogueTopic.Discovery, SocialAction.Joke,
                "Well.",
                new[]
                {
                    R(SocialResponse.Accept, "Well what?"),
                    R(SocialResponse.Agree, "We're not completely fucked."),
                    R(SocialResponse.Deflect, "Don't jinx the vein."),
                    R(SocialResponse.Ignore, "Mm."),
                    R(SocialResponse.PushBack, "Keep digging.", true),
                });

            Add(SocialDialogueTopic.Discovery, SocialAction.Encourage,
                "You beautiful boring bastard.",
                new[]
                {
                    R(SocialResponse.Accept, "I'll take boring."),
                    R(SocialResponse.Agree, "Don't tell anyone I smiled."),
                    R(SocialResponse.Deflect, "Save it for payday."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.PushBack, "Weird compliment."),
                },
                strength: SocialStrengthKind.Insight, weight: 1.15f);

            Add(SocialDialogueTopic.Discovery, SocialAction.Connect,
                "Is that…",
                new[]
                {
                    R(SocialResponse.Accept, "Yes."),
                    R(SocialResponse.Agree, "Diamonds."),
                    R(SocialResponse.Deflect, "Don't touch it."),
                    R(SocialResponse.Escalate, "Back up."),
                    R(SocialResponse.Ignore, "…"),
                },
                new[] { "Fuck me.", "Fair." });

            Add(SocialDialogueTopic.Discovery, SocialAction.Confront,
                "Don't touch it.",
                new[]
                {
                    R(SocialResponse.Accept, "I wasn't."),
                    R(SocialResponse.Agree, "You were thinking about it."),
                    R(SocialResponse.Deflect, "Hands off. Mark it."),
                    R(SocialResponse.PushBack, "Relax."),
                    R(SocialResponse.Escalate, "I said don't."),
                });

            Add(SocialDialogueTopic.Discovery, SocialAction.Joke,
                "Worthless rock with a sparkle complex.",
                new[]
                {
                    R(SocialResponse.Accept, "Still bag it."),
                    R(SocialResponse.Agree, "Sparkle pays."),
                    R(SocialResponse.Deflect, "Refiner's problem."),
                    R(SocialResponse.PushBack, "Stop naming rocks.", true),
                    R(SocialResponse.Ignore, "…"),
                });

            // ——— ENGINEERING / SUPPORTS ———
            Add(SocialDialogueTopic.Supports, SocialAction.Connect,
                "Another pillar?",
                new[]
                {
                    R(SocialResponse.Accept, "Another ceiling."),
                    R(SocialResponse.Agree, "Ceiling was already there."),
                    R(SocialResponse.Deflect, "Exactly."),
                    R(SocialResponse.PushBack, "Mind your own span."),
                    R(SocialResponse.Ignore, "Mm."),
                },
                new[] { "Fair." });

            Add(SocialDialogueTopic.Supports, SocialAction.Confront,
                "Do we need that?",
                new[]
                {
                    R(SocialResponse.Accept, "No."),
                    R(SocialResponse.Agree, "We need two."),
                    R(SocialResponse.Deflect, "Ask the creak."),
                    R(SocialResponse.PushBack, "You're paranoid."),
                    R(SocialResponse.Escalate, "And you're alive."),
                },
                new[] { "Fair.", "Luxury." });

            Add(SocialDialogueTopic.Supports, SocialAction.Complain,
                "That timber's complaining.",
                new[]
                {
                    R(SocialResponse.Agree, "I hear it. Brace."),
                    R(SocialResponse.Accept, "Don't like that note."),
                    R(SocialResponse.Deflect, "Wood always complains."),
                    R(SocialResponse.PushBack, "Replace it then."),
                    R(SocialResponse.Withdraw, "…"),
                });

            Add(SocialDialogueTopic.Supports, SocialAction.Encourage,
                "That brace held when it shouldn't have.",
                new[]
                {
                    R(SocialResponse.Accept, "Don't say that."),
                    R(SocialResponse.Agree, "I'll take held."),
                    R(SocialResponse.Deflect, "Luck and bolts."),
                    R(SocialResponse.Ignore, "Mm."),
                },
                strength: SocialStrengthKind.TechnicalSkill, weight: 1.2f);

            // ——— RIVALRY ENGINEER VS EXCAVATOR ———
            Add(SocialDialogueTopic.Work, SocialAction.Provoke,
                "Stop digging like the mountain owes you money.",
                new[]
                {
                    R(SocialResponse.PushBack, "Stop building like you're expecting it to sue."),
                    R(SocialResponse.Escalate, "Touch that support and I'll bury you myself."),
                    R(SocialResponse.Accept, "Noted."),
                    R(SocialResponse.Deflect, "Both still employed. Miracle."),
                    R(SocialResponse.Agree, "Fair."),
                }, hostile: true);

            Add(SocialDialogueTopic.Supports, SocialAction.Confront,
                "That support isn't decorative.",
                new[]
                {
                    R(SocialResponse.PushBack, "Could've fooled me."),
                    R(SocialResponse.Accept, "Hands off."),
                    R(SocialResponse.Escalate, "Dig around it."),
                    R(SocialResponse.Agree, "I know."),
                    R(SocialResponse.Deflect, "Calm down."),
                }, hostile: true);

            Add(SocialDialogueTopic.Work, SocialAction.Provoke,
                "You dig too fast.",
                new[]
                {
                    R(SocialResponse.PushBack, "You build too slow."),
                    R(SocialResponse.Escalate, "Clock me then."),
                    R(SocialResponse.Accept, "And somehow we're both still employed."),
                    R(SocialResponse.Deflect, "Save it for Manager."),
                    R(SocialResponse.Agree, "Maybe."),
                }, hostile: true);

            // ——— HAULER ———
            Add(SocialDialogueTopic.Work, SocialAction.Connect,
                "How much does that weigh?",
                new[]
                {
                    R(SocialResponse.Accept, "Enough."),
                    R(SocialResponse.Agree, "Useful unit."),
                    R(SocialResponse.Deflect, "Ask my spine."),
                    R(SocialResponse.PushBack, "Less than your questions."),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Work, SocialAction.Encourage,
                "Need help?",
                new[]
                {
                    R(SocialResponse.Accept, "No."),
                    R(SocialResponse.Agree, "That sounded painful."),
                    R(SocialResponse.Deflect, "Still no."),
                    R(SocialResponse.PushBack, "I said no."),
                    R(SocialResponse.Ignore, "Mm."),
                });

            Add(SocialDialogueTopic.Work, SocialAction.Joke,
                "You know they make machines for that.",
                new[]
                {
                    R(SocialResponse.Accept, "I am the machine."),
                    R(SocialResponse.Agree, "Broken one."),
                    R(SocialResponse.Deflect, "Budget said no."),
                    R(SocialResponse.PushBack, "Then haul.", true),
                    R(SocialResponse.Ignore, "…"),
                },
                new[] { "Luxury." });

            Add(SocialDialogueTopic.Strength, SocialAction.Encourage,
                "Kowalski carried that alone?",
                new[]
                {
                    R(SocialResponse.Accept, "Apparently he's too stupid for gravity."),
                    R(SocialResponse.Agree, "Don't give him ideas."),
                    R(SocialResponse.Deflect, "Spine's filing a complaint."),
                    R(SocialResponse.Ignore, "Mm."),
                    R(SocialResponse.PushBack, "Ask him."),
                },
                strength: SocialStrengthKind.PhysicalPower, weight: 1.25f);

            // ——— REFINER ———
            Add(SocialDialogueTopic.Work, SocialAction.Connect,
                "Anything useful?",
                new[]
                {
                    R(SocialResponse.Accept, "Mostly dirt."),
                    R(SocialResponse.Agree, "We've got plenty of that."),
                    R(SocialResponse.Deflect, "Ask after the wash."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.PushBack, "Useful's relative."),
                });

            Add(SocialDialogueTopic.Work, SocialAction.Joke,
                "Can you make it gold?",
                new[]
                {
                    R(SocialResponse.Accept, "No."),
                    R(SocialResponse.Agree, "Then what exactly do you do?"),
                    R(SocialResponse.Deflect, "Separate. Not invent."),
                    R(SocialResponse.PushBack, "Alchemy's upstairs.", true),
                    R(SocialResponse.Ignore, "Mm."),
                });

            Add(SocialDialogueTopic.Discovery, SocialAction.Connect,
                "That good?",
                new[]
                {
                    R(SocialResponse.Accept, "Depends."),
                    R(SocialResponse.Agree, "Whether you like money."),
                    R(SocialResponse.Deflect, "Grade's decent."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.PushBack, "Don't get greedy."),
                });

            // ——— FOOD ———
            Add(SocialDialogueTopic.Food, SocialAction.Complain,
                "What is this?",
                new[]
                {
                    R(SocialResponse.Accept, "Dinner."),
                    R(SocialResponse.Agree, "Legally?"),
                    R(SocialResponse.Deflect, "Eat it."),
                    R(SocialResponse.PushBack, "That's not reassuring."),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Food, SocialAction.Joke,
                "Mine tried to move.",
                new[]
                {
                    R(SocialResponse.Accept, "That's steam."),
                    R(SocialResponse.Agree, "It had intent."),
                    R(SocialResponse.Deflect, "Don't insult Kit where Kit can hear."),
                    R(SocialResponse.PushBack, "Eat and shut up.", true),
                    R(SocialResponse.Ignore, "Mhm."),
                },
                new[] { "Hope died in the pot." });

            Add(SocialDialogueTopic.Food, SocialAction.Encourage,
                "That's actually good.",
                new[]
                {
                    R(SocialResponse.Accept, "Sound more surprised."),
                    R(SocialResponse.Agree, "I physically can't."),
                    R(SocialResponse.Deflect, "Don't scare Kit."),
                    R(SocialResponse.Ignore, "Mm."),
                    R(SocialResponse.PushBack, "Seconds before you jinx it."),
                },
                strength: SocialStrengthKind.Care, weight: 1.1f);

            Add(SocialDialogueTopic.Food, SocialAction.Joke,
                "Kit, marry me.",
                new[]
                {
                    R(SocialResponse.Accept, "Eat your food."),
                    R(SocialResponse.Agree, "That's basically yes."),
                    R(SocialResponse.Deflect, "After you do dishes."),
                    R(SocialResponse.PushBack, "No.", true),
                    R(SocialResponse.Ignore, "…"),
                }, bonded: true);

            // ——— STOMACH / TOILET ———
            Add(SocialDialogueTopic.Toilet, SocialAction.Joke,
                "Again?",
                new[]
                {
                    R(SocialResponse.PushBack, "Don't."),
                    R(SocialResponse.Accept, "That's four."),
                    R(SocialResponse.Agree, "I can count."),
                    R(SocialResponse.Deflect, "Camp plumbing's a suggestion."),
                    R(SocialResponse.Escalate, "Fuck off."),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Toilet, SocialAction.Complain,
                "Who destroyed the toilet?",
                new[]
                {
                    R(SocialResponse.Accept, "We agreed not to investigate."),
                    R(SocialResponse.Agree, "Wise."),
                    R(SocialResponse.Deflect, "Kit's problem now."),
                    R(SocialResponse.PushBack, "Not me."),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Toilet, SocialAction.Joke,
                "If I die in there…",
                new[]
                {
                    R(SocialResponse.Accept, "We're leaving you."),
                    R(SocialResponse.Agree, "Fair."),
                    R(SocialResponse.Deflect, "Take a lamp."),
                    R(SocialResponse.PushBack, "Don't.", true),
                    R(SocialResponse.Ignore, "Mm."),
                },
                new[] { "Luxury." });

            Add(SocialDialogueTopic.Toilet, SocialAction.Connect,
                "Kit.",
                new[]
                {
                    R(SocialResponse.Accept, "No."),
                    R(SocialResponse.Agree, "I haven't said anything."),
                    R(SocialResponse.Deflect, "I know."),
                    R(SocialResponse.PushBack, "Not my stomach."),
                    R(SocialResponse.Ignore, "…"),
                });

            // ——— EXHAUSTION ———
            Add(SocialDialogueTopic.Exhaustion, SocialAction.Connect,
                "You look like shit.",
                new[]
                {
                    R(SocialResponse.Accept, "Feel worse."),
                    R(SocialResponse.Agree, "Good. Consistency."),
                    R(SocialResponse.Deflect, "Mirror's worse."),
                    R(SocialResponse.PushBack, "Speak for yourself."),
                    R(SocialResponse.Withdraw, "…I need five."),
                });

            Add(SocialDialogueTopic.Exhaustion, SocialAction.Complain,
                "How long have we been working?",
                new[]
                {
                    R(SocialResponse.Accept, "Too long."),
                    R(SocialResponse.Agree, "Actual number? Still too long."),
                    R(SocialResponse.Deflect, "Whistle's coming. Allegedly."),
                    R(SocialResponse.PushBack, "Count your own hours."),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Exhaustion, SocialAction.Encourage,
                "Sit down.",
                new[]
                {
                    R(SocialResponse.Accept, "I'm fine."),
                    R(SocialResponse.Agree, "You walked into a wall."),
                    R(SocialResponse.Deflect, "Wall moved."),
                    R(SocialResponse.PushBack, "Don't mother me."),
                    R(SocialResponse.Withdraw, "…Yeah."),
                }, bonded: true);

            Add(SocialDialogueTopic.Exhaustion, SocialAction.Joke,
                "My bones filed overtime without me.",
                new[]
                {
                    R(SocialResponse.Accept, "Tell them we don't pay dreams."),
                    R(SocialResponse.Agree, "Mine are unionizing."),
                    R(SocialResponse.Deflect, "Sleep on your break."),
                    R(SocialResponse.PushBack, "Then stop talking.", true),
                    R(SocialResponse.Ignore, "Mm."),
                });

            // ——— INJURY ———
            Add(SocialDialogueTopic.Injury, SocialAction.Connect,
                "You're bleeding.",
                new[]
                {
                    R(SocialResponse.Accept, "I noticed."),
                    R(SocialResponse.Agree, "Just checking."),
                    R(SocialResponse.Deflect, "I've had worse."),
                    R(SocialResponse.PushBack, "Don't stare."),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Injury, SocialAction.Connect,
                "How's the leg?",
                new[]
                {
                    R(SocialResponse.Accept, "Attached."),
                    R(SocialResponse.Agree, "High standards."),
                    R(SocialResponse.Deflect, "Ask after the cut."),
                    R(SocialResponse.PushBack, "Still walking."),
                    R(SocialResponse.Withdraw, "…Sore."),
                });

            Add(SocialDialogueTopic.Injury, SocialAction.Joke,
                "That hurt?",
                new[]
                {
                    R(SocialResponse.Accept, "No, I always scream like that."),
                    R(SocialResponse.Agree, "Comedy gold."),
                    R(SocialResponse.Deflect, "Kit's tent."),
                    R(SocialResponse.PushBack, "Fuck off.", true),
                    R(SocialResponse.Ignore, "…"),
                });

            // ——— GENUINE CONCERN (BONDED) ———
            Add(SocialDialogueTopic.Injury, SocialAction.Encourage,
                "Seriously. You alright?",
                new[]
                {
                    R(SocialResponse.Accept, "I'll manage."),
                    R(SocialResponse.Agree, "That's not what I asked."),
                    R(SocialResponse.Deflect, "Ask me at camp."),
                    R(SocialResponse.PushBack, "I'm fine. Stop staring."),
                    R(SocialResponse.Withdraw, "…No."),
                }, bonded: true);

            Add(SocialDialogueTopic.Injury, SocialAction.Encourage,
                "Let Kit look at it.",
                new[]
                {
                    R(SocialResponse.Accept, "It's fine."),
                    R(SocialResponse.Agree, "You're limping."),
                    R(SocialResponse.Deflect, "Observant."),
                    R(SocialResponse.PushBack, "After this span."),
                    R(SocialResponse.Withdraw, "…Alright."),
                }, bonded: true);

            Add(SocialDialogueTopic.Exhaustion, SocialAction.Encourage,
                "You're done. Don't argue.",
                new[]
                {
                    R(SocialResponse.Accept, "…Fine."),
                    R(SocialResponse.Agree, "Heard."),
                    R(SocialResponse.Deflect, "One more cut."),
                    R(SocialResponse.PushBack, "Don't mother me."),
                    R(SocialResponse.Withdraw, "Yeah."),
                }, bonded: true);

            Add(SocialDialogueTopic.General, SocialAction.Connect,
                "You alright?",
                new[]
                {
                    R(SocialResponse.Accept, "Yeah."),
                    R(SocialResponse.Agree, "Liar."),
                    R(SocialResponse.Deflect, "Define alright."),
                    R(SocialResponse.Ignore, "Mm."),
                    R(SocialResponse.Withdraw, "…"),
                }, bonded: true);

            Add(SocialDialogueTopic.General, SocialAction.Connect,
                "If this goes wrong…",
                new[]
                {
                    R(SocialResponse.Accept, "I know."),
                    R(SocialResponse.Agree, "Good."),
                    R(SocialResponse.Deflect, "It won't."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.PushBack, "Don't."),
                }, bonded: true);

            Add(SocialDialogueTopic.Camp, SocialAction.Connect,
                "Drink?",
                new[]
                {
                    R(SocialResponse.Accept, "After shift."),
                    R(SocialResponse.Agree, "That's what I meant."),
                    R(SocialResponse.Deflect, "No it wasn't."),
                    R(SocialResponse.Ignore, "Mm."),
                    R(SocialResponse.PushBack, "Water. Not whatever Kit found."),
                }, bonded: true);

            // ——— OVERTIME / MANAGER ———
            Add(SocialDialogueTopic.Manager, SocialAction.Complain,
                "Another two hours.",
                new[]
                {
                    R(SocialResponse.Agree, "Manager say that?"),
                    R(SocialResponse.Accept, "Yeah. Manager can come dig it."),
                    R(SocialResponse.Deflect, "Orders are orders."),
                    R(SocialResponse.PushBack, "Don't start."),
                    R(SocialResponse.Escalate, "I'm going to push someone."),
                });

            Add(SocialDialogueTopic.Manager, SocialAction.Complain,
                "We're staying late.",
                new[]
                {
                    R(SocialResponse.Agree, "Again?"),
                    R(SocialResponse.Accept, "Again. Fantastic."),
                    R(SocialResponse.Deflect, "Paper doesn't choke."),
                    R(SocialResponse.PushBack, "Keep that talk soft."),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Manager, SocialAction.Complain,
                "Push harder, apparently.",
                new[]
                {
                    R(SocialResponse.Agree, "I'm going to push someone."),
                    R(SocialResponse.Accept, "Heard."),
                    R(SocialResponse.Deflect, "Save it for the fire."),
                    R(SocialResponse.PushBack, "Don't."),
                    R(SocialResponse.Escalate, "Fuck that."),
                });

            Add(SocialDialogueTopic.Manager, SocialAction.Confront,
                "Deeper, they said. From a chair with air.",
                new[]
                {
                    R(SocialResponse.Agree, "Paper doesn't choke."),
                    R(SocialResponse.Accept, "Keep that soft."),
                    R(SocialResponse.Deflect, "Orders."),
                    R(SocialResponse.PushBack, "Not now."),
                    R(SocialResponse.Escalate, "Say it louder."),
                });

            // ——— COLLAPSE ———
            Add(SocialDialogueTopic.Collapse, SocialAction.Confront,
                "Move!",
                new[]
                {
                    R(SocialResponse.Accept, "I'm moving!"),
                    R(SocialResponse.Agree, "Go!"),
                    R(SocialResponse.Escalate, "Left!"),
                    R(SocialResponse.Withdraw, "—"),
                    R(SocialResponse.Ignore, "…"),
                }, weight: 1.3f);

            Add(SocialDialogueTopic.Collapse, SocialAction.Connect,
                "Everyone alive?",
                new[]
                {
                    R(SocialResponse.Accept, "Define alive."),
                    R(SocialResponse.Agree, "Counting."),
                    R(SocialResponse.Deflect, "Don't fucking say that was close."),
                    R(SocialResponse.Withdraw, "…"),
                    R(SocialResponse.PushBack, "Check the span."),
                },
                new[] { "Fair." },
                needsMem: true, mem: SocialMemoryType.SurvivedCollapse, weight: 0.85f);

            Add(SocialDialogueTopic.Collapse, SocialAction.Confront,
                "That creak again. Same note.",
                new[]
                {
                    R(SocialResponse.Agree, "I remember."),
                    R(SocialResponse.Accept, "Supports. Now."),
                    R(SocialResponse.Deflect, "Don't."),
                    R(SocialResponse.Withdraw, "…"),
                    R(SocialResponse.Escalate, "Out. Now."),
                },
                needsMem: true, mem: SocialMemoryType.SurvivedCollapse, weight: 0.95f);

            // ——— RESCUE / TRAPPED ———
            Add(SocialDialogueTopic.Rescue, SocialAction.Connect,
                "You came back.",
                new[]
                {
                    R(SocialResponse.Accept, "Obviously."),
                    R(SocialResponse.Agree, "Could've left me."),
                    R(SocialResponse.Deflect, "Still considering it."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.PushBack, "Don't make it a speech."),
                },
                needsMem: true, mem: SocialMemoryType.RescuedByWorker, bonded: true, weight: 0.9f);

            Add(SocialDialogueTopic.Rescue, SocialAction.Encourage,
                "Can you walk?",
                new[]
                {
                    R(SocialResponse.Accept, "Probably."),
                    R(SocialResponse.Agree, "That's not convincing."),
                    R(SocialResponse.Deflect, "Neither is standing here."),
                    R(SocialResponse.PushBack, "Watch me."),
                    R(SocialResponse.Withdraw, "…Help."),
                },
                needsMem: true, mem: SocialMemoryType.RescuedByWorker, bonded: true);

            Add(SocialDialogueTopic.Trapped, SocialAction.Connect,
                "Stay with me. Breathing.",
                new[]
                {
                    R(SocialResponse.Accept, "Trying."),
                    R(SocialResponse.Agree, "Don't leave me."),
                    R(SocialResponse.Withdraw, "I can't—"),
                    R(SocialResponse.PushBack, "I know."),
                    R(SocialResponse.Ignore, "…"),
                }, bonded: true, weight: 1.1f);

            Add(SocialDialogueTopic.Trapped, SocialAction.Encourage,
                "We're cutting through. Hold.",
                new[]
                {
                    R(SocialResponse.Accept, "Hurry."),
                    R(SocialResponse.Agree, "I hear you."),
                    R(SocialResponse.Withdraw, "…"),
                    R(SocialResponse.PushBack, "Talk less. Dig."),
                    R(SocialResponse.Deflect, "Just get me out."),
                });

            // ——— STRENGTH RECOGNITION ———
            Add(SocialDialogueTopic.Strength, SocialAction.Encourage,
                "Say what you want about them — they don't freeze.",
                new[]
                {
                    R(SocialResponse.Accept, "Not today, anyway."),
                    R(SocialResponse.Agree, "Steady's underrated."),
                    R(SocialResponse.Deflect, "Flattery's slippery."),
                    R(SocialResponse.Ignore, "Mm."),
                },
                strength: SocialStrengthKind.SteadyUnderPressure, weight: 1.25f);

            Add(SocialDialogueTopic.Strength, SocialAction.Encourage,
                "Get Viktor. I want it fixed.",
                new[]
                {
                    R(SocialResponse.Accept, "Already thinking it."),
                    R(SocialResponse.Agree, "Because you want it fixed. Fair."),
                    R(SocialResponse.Deflect, "If they want the headache."),
                    R(SocialResponse.Ignore, "…"),
                },
                strength: SocialStrengthKind.TechnicalSkill, weight: 1.2f);

            Add(SocialDialogueTopic.Strength, SocialAction.Encourage,
                "Lewis was right.",
                new[]
                {
                    R(SocialResponse.Accept, "Don't tell him."),
                    R(SocialResponse.Agree, "Wasn't planning to."),
                    R(SocialResponse.Deflect, "Rare day."),
                    R(SocialResponse.PushBack, "Once."),
                    R(SocialResponse.Ignore, "Mm."),
                },
                strength: SocialStrengthKind.Insight, weight: 1.15f);

            Add(SocialDialogueTopic.Strength, SocialAction.Encourage,
                "They went back in. Didn't hesitate.",
                new[]
                {
                    R(SocialResponse.Accept, "Stupid. Useful stupid."),
                    R(SocialResponse.Agree, "Courage or debt."),
                    R(SocialResponse.Deflect, "Don't make a speech."),
                    R(SocialResponse.Ignore, "…"),
                },
                strength: SocialStrengthKind.Courage, weight: 1.2f);

            Add(SocialDialogueTopic.Strength, SocialAction.Encourage,
                "Quiet work. Shows up. Doesn't break.",
                new[]
                {
                    R(SocialResponse.Accept, "I'll take quiet."),
                    R(SocialResponse.Agree, "Don't make it weird."),
                    R(SocialResponse.Deflect, "Barely."),
                    R(SocialResponse.Ignore, "Mm."),
                },
                strength: SocialStrengthKind.Reliability, weight: 1.2f);

            Add(SocialDialogueTopic.Praise, SocialAction.Encourage,
                "Not bad.",
                new[]
                {
                    R(SocialResponse.Accept, "From you?"),
                    R(SocialResponse.Agree, "Don't make me take it back."),
                    R(SocialResponse.Deflect, "Feeling alright?"),
                    R(SocialResponse.PushBack, "Fuck off."),
                    R(SocialResponse.Ignore, "…"),
                }, hostile: true);

            Add(SocialDialogueTopic.Praise, SocialAction.Joke,
                "I could've done that faster.",
                new[]
                {
                    R(SocialResponse.Accept, "You could've done it worse faster."),
                    R(SocialResponse.Agree, "Nice work. Feeling alright?"),
                    R(SocialResponse.Deflect, "Clock me next time."),
                    R(SocialResponse.PushBack, "Try."),
                    R(SocialResponse.Escalate, "Fuck off."),
                }, hostile: true);

            // ——— RIVALRY / STRAINED / GRUDGE ———
            Add(SocialDialogueTopic.General, SocialAction.Connect,
                "Need anything?",
                new[]
                {
                    R(SocialResponse.PushBack, "From you?"),
                    R(SocialResponse.Accept, "Never mind."),
                    R(SocialResponse.Deflect, "I'm fine."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.Escalate, "Walk away."),
                }, hostile: true);

            Add(SocialDialogueTopic.Work, SocialAction.Confront,
                "You could've warned me.",
                new[]
                {
                    R(SocialResponse.PushBack, "You could've listened."),
                    R(SocialResponse.Escalate, "Don't."),
                    R(SocialResponse.Accept, "…Fine."),
                    R(SocialResponse.Deflect, "Write it down next time."),
                    R(SocialResponse.Withdraw, "…"),
                }, hostile: true);

            Add(SocialDialogueTopic.WeakPoint, SocialAction.Provoke,
                "Don't freeze this time.",
                new[]
                {
                    R(SocialResponse.Escalate, "Keep talking."),
                    R(SocialResponse.PushBack, "Or what? Find out."),
                    R(SocialResponse.Deflect, "Cute."),
                    R(SocialResponse.Withdraw, "…"),
                    R(SocialResponse.Agree, "Hilarious.", true),
                },
                hostile: true, weak: SocialWeakPointKind.Fear, weight: 1.2f);

            Add(SocialDialogueTopic.WeakPoint, SocialAction.Provoke,
                "Everybody else manages.",
                new[]
                {
                    R(SocialResponse.Escalate, "Everybody else doesn't have to listen to you."),
                    R(SocialResponse.PushBack, "Fuck off."),
                    R(SocialResponse.Deflect, "Keep walking."),
                    R(SocialResponse.Withdraw, "…"),
                    R(SocialResponse.Ignore, "No."),
                },
                hostile: true, weak: SocialWeakPointKind.Competence, weight: 1.1f);

            Add(SocialDialogueTopic.General, SocialAction.Confront,
                "You always have an excuse.",
                new[]
                {
                    R(SocialResponse.Escalate, "And you always need someone to blame."),
                    R(SocialResponse.PushBack, "Say it properly."),
                    R(SocialResponse.Deflect, "Not today."),
                    R(SocialResponse.Withdraw, "…"),
                    R(SocialResponse.Accept, "Maybe."),
                }, hostile: true);

            Add(SocialDialogueTopic.WeakPoint, SocialAction.Provoke,
                "Late to the face. Again.",
                new[]
                {
                    R(SocialResponse.PushBack, "Say that when you're on the bit."),
                    R(SocialResponse.Escalate, "Fuck off."),
                    R(SocialResponse.Deflect, "Keep walking."),
                    R(SocialResponse.Withdraw, "…"),
                    R(SocialResponse.Ignore, "No."),
                },
                hostile: true, weak: SocialWeakPointKind.Speed, weight: 1.1f);

            Add(SocialDialogueTopic.WeakPoint, SocialAction.Confront,
                "Missed the tell. Don't miss the next one.",
                new[]
                {
                    R(SocialResponse.PushBack, "I saw it. Different call."),
                    R(SocialResponse.Escalate, "Back off."),
                    R(SocialResponse.Accept, "…Fine. Watched."),
                    R(SocialResponse.Deflect, "Write it in your little book."),
                    R(SocialResponse.Withdraw, "…"),
                },
                weak: SocialWeakPointKind.Competence, weight: 1.05f);

            // ——— DEATH / AFTERMATH (RARE, RESTRAINED) ———
            Add(SocialDialogueTopic.Callback, SocialAction.Connect,
                "Where's Mara?",
                new[]
                {
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.Accept, "Right."),
                    R(SocialResponse.Withdraw, "Don't."),
                    R(SocialResponse.Deflect, "Not now."),
                    R(SocialResponse.Agree, "I know."),
                },
                needsMem: true, mem: SocialMemoryType.WitnessedDeath, weight: 0.4f);

            Add(SocialDialogueTopic.Callback, SocialAction.Connect,
                "Her stuff's still there.",
                new[]
                {
                    R(SocialResponse.Accept, "I know."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.Agree, "Leave it."),
                    R(SocialResponse.Withdraw, "Can't look."),
                    R(SocialResponse.Deflect, "Later."),
                },
                needsMem: true, mem: SocialMemoryType.WitnessedDeath, weight: 0.4f);

            Add(SocialDialogueTopic.Camp, SocialAction.Connect,
                "You sleeping?",
                new[]
                {
                    R(SocialResponse.Accept, "No."),
                    R(SocialResponse.Agree, "Me neither."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.Deflect, "Don't ask."),
                    R(SocialResponse.Withdraw, "Mm."),
                },
                bonded: true, needsMem: true, mem: SocialMemoryType.WitnessedSeriousAccident, weight: 0.4f);

            // ——— BOREDOM / ABSURDITY / GERALD ———
            Add(SocialDialogueTopic.Humour, SocialAction.Joke,
                "If you had to eat one of us…",
                new[]
                {
                    R(SocialResponse.PushBack, "No."),
                    R(SocialResponse.Accept, "You didn't even let me finish."),
                    R(SocialResponse.Deflect, "Save it for the fire."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.Escalate, "Fuck off."),
                }, weight: 0.7f);

            Add(SocialDialogueTopic.Humour, SocialAction.Joke,
                "Think rocks get bored?",
                new[]
                {
                    R(SocialResponse.Accept, "I think you do."),
                    R(SocialResponse.Agree, "We're the entertainment."),
                    R(SocialResponse.Deflect, "Dig."),
                    R(SocialResponse.PushBack, "Please stop.", true),
                    R(SocialResponse.Ignore, "Mm."),
                });

            Add(SocialDialogueTopic.Callback, SocialAction.Joke,
                "I named that one. That's Gerald.",
                new[]
                {
                    R(SocialResponse.Accept, "I hate Gerald."),
                    R(SocialResponse.Agree, "It's a rock."),
                    R(SocialResponse.Deflect, "Don't."),
                    R(SocialResponse.PushBack, "Stop naming rocks.", true),
                    R(SocialResponse.Ignore, "…"),
                },
                weight: 0.5f);

            Add(SocialDialogueTopic.Callback, SocialAction.Joke,
                "Gerald's gone.",
                new[]
                {
                    R(SocialResponse.Accept, "Who?"),
                    R(SocialResponse.Agree, "Forget it."),
                    R(SocialResponse.Deflect, "Good riddance."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.PushBack, "Not this again.", true),
                },
                weight: 0.4f);

            Add(SocialDialogueTopic.Callback, SocialAction.Joke,
                "This one's Gerald Junior.",
                new[]
                {
                    R(SocialResponse.Accept, "I hate the whole family."),
                    R(SocialResponse.Agree, "Murder it quietly."),
                    R(SocialResponse.Deflect, "No."),
                    R(SocialResponse.PushBack, "Fuck off.", true),
                    R(SocialResponse.Ignore, "Mm."),
                },
                weight: 0.35f);

            Add(SocialDialogueTopic.Callback, SocialAction.Connect,
                "Gerald's still there.",
                new[]
                {
                    R(SocialResponse.Accept, "Leave him."),
                    R(SocialResponse.Agree, "Masochist rock."),
                    R(SocialResponse.Deflect, "Dig around."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.PushBack, "It's a rock."),
                },
                weight: 0.45f);

            Add(SocialDialogueTopic.Humour, SocialAction.Joke,
                "Twelve hours in. Inventing dialects of fuck.",
                new[]
                {
                    R(SocialResponse.Accept, "Patent them."),
                    R(SocialResponse.Agree, "Teach me one."),
                    R(SocialResponse.Deflect, "Save some for bedrock."),
                    R(SocialResponse.Ignore, "Mhm."),
                    R(SocialResponse.PushBack, "Dig.", true),
                });

            Add(SocialDialogueTopic.Depth, SocialAction.Complain,
                "Ears popped. Or the mountain did.",
                new[]
                {
                    R(SocialResponse.Agree, "Depth taxes the skull."),
                    R(SocialResponse.Accept, "Lamp helps. Barely."),
                    R(SocialResponse.Deflect, "Keep moving."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.Withdraw, "I need up soon."),
                });

            Add(SocialDialogueTopic.Geology, SocialAction.Complain,
                "Rock's lying to the bit. Feel that?",
                new[]
                {
                    R(SocialResponse.Agree, "I felt it too."),
                    R(SocialResponse.Accept, "Mark it. Go slow."),
                    R(SocialResponse.Deflect, "Everything feels wrong after ten hours."),
                    R(SocialResponse.PushBack, "Or you're tired."),
                    R(SocialResponse.Ignore, "Mm."),
                });

            Add(SocialDialogueTopic.Camp, SocialAction.Joke,
                "Queue for the bucket. Peak civilization.",
                new[]
                {
                    R(SocialResponse.Accept, "Civil engineering at its finest."),
                    R(SocialResponse.Agree, "Kit would cry."),
                    R(SocialResponse.Deflect, "Wait your turn like a human."),
                    R(SocialResponse.PushBack, "Charming.", true),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Camp, SocialAction.Connect,
                "Embers held. Small win.",
                new[]
                {
                    R(SocialResponse.Accept, "Small mercies."),
                    R(SocialResponse.Agree, "Warm's not nothing."),
                    R(SocialResponse.Deflect, "Until the fuel runs."),
                    R(SocialResponse.Ignore, "Mm."),
                    R(SocialResponse.PushBack, "Don't stare at it."),
                });

            Add(SocialDialogueTopic.General, SocialAction.Encourage,
                "Ugly truth: you're still standing.",
                new[]
                {
                    R(SocialResponse.Accept, "Ugly truth."),
                    R(SocialResponse.Agree, "I'll take ugly."),
                    R(SocialResponse.Deflect, "Poetry later."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.PushBack, "Barely."),
                });

            Add(SocialDialogueTopic.Work, SocialAction.Complain,
                "Can't tell if the bit's screaming or I am.",
                new[]
                {
                    R(SocialResponse.Agree, "Both, probably."),
                    R(SocialResponse.Accept, "Cool it before it cooks you."),
                    R(SocialResponse.Deflect, "Music to some ears."),
                    R(SocialResponse.PushBack, "Then stop."),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Humour, SocialAction.Joke,
                "If the rock wanted guests, it'd leave a handle.",
                new[]
                {
                    R(SocialResponse.Accept, "Then why's it charging rent in dust?"),
                    R(SocialResponse.Agree, "Worst landlord I ever had."),
                    R(SocialResponse.Deflect, "Save it for the fire."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.PushBack, "Not funny. Dig.", true),
                },
                new[] { "Luxury.", "Ha. Ow." });

            Add(SocialDialogueTopic.Darkness, SocialAction.Joke,
                "I waved at the dark. Rude bastard didn't wave back.",
                new[]
                {
                    R(SocialResponse.Accept, "It waved. You just couldn't see it."),
                    R(SocialResponse.Agree, "Pitch black has no manners."),
                    R(SocialResponse.Deflect, "Lamp first. Comedy later."),
                    R(SocialResponse.Ignore, "Mhm."),
                    R(SocialResponse.PushBack, "Stop waving.", true),
                });

            Add(SocialDialogueTopic.General, SocialAction.Provoke,
                "Wipe that look. You earned nothing yet.",
                new[]
                {
                    R(SocialResponse.Deflect, "Do I?"),
                    R(SocialResponse.PushBack, "Problem?"),
                    R(SocialResponse.Accept, "Rare day."),
                    R(SocialResponse.Escalate, "Wipe that look off."),
                    R(SocialResponse.Ignore, "…"),
                }, hostile: true);

            Add(SocialDialogueTopic.Work, SocialAction.Confront,
                "Sloppy cut. Fix it before it kills someone.",
                new[]
                {
                    R(SocialResponse.Accept, "On it."),
                    R(SocialResponse.PushBack, "It's holding."),
                    R(SocialResponse.Escalate, "You dig it then."),
                    R(SocialResponse.Agree, "…Yeah. You're right."),
                    R(SocialResponse.Deflect, "After this load."),
                });

            Add(SocialDialogueTopic.Praise, SocialAction.Encourage,
                "No noise. No drama. Keep that.",
                new[]
                {
                    R(SocialResponse.Accept, "Thanks."),
                    R(SocialResponse.Agree, "I'll take quiet."),
                    R(SocialResponse.Deflect, "Don't make it weird."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.PushBack, "Barely."),
                }, bonded: true);

            Add(SocialDialogueTopic.Rescue, SocialAction.Encourage,
                "You went back. Don't pretend that was nothing.",
                new[]
                {
                    R(SocialResponse.Accept, "Had to."),
                    R(SocialResponse.Agree, "Couldn't leave them."),
                    R(SocialResponse.Deflect, "Anyone would've."),
                    R(SocialResponse.Ignore, "Mm."),
                    R(SocialResponse.PushBack, "Don't."),
                },
                needsMem: true, mem: SocialMemoryType.RescuedByWorker, bonded: true,
                strength: SocialStrengthKind.Courage, weight: 1.15f);

            Add(SocialDialogueTopic.General, SocialAction.Connect,
                "Eat something?",
                new[]
                {
                    R(SocialResponse.Accept, "Something that claimed to be stew."),
                    R(SocialResponse.Agree, "Enough to keep standing."),
                    R(SocialResponse.Deflect, "Later."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.PushBack, "Not hungry."),
                });

            Add(SocialDialogueTopic.Humour, SocialAction.Joke,
                "Helmet's brighter than my prospects.",
                new[]
                {
                    R(SocialResponse.Accept, "At least the helmet's paid for."),
                    R(SocialResponse.Agree, "Speak for yourself. Mine's rented."),
                    R(SocialResponse.Deflect, "Dark humour, dark tunnel."),
                    R(SocialResponse.Escalate, "Keep that talk away from me."),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Callback, SocialAction.Joke,
                "Remember when Gerald was taller?",
                new[]
                {
                    R(SocialResponse.Accept, "Before you murdered him."),
                    R(SocialResponse.Agree, "Junior's shorter. Progress."),
                    R(SocialResponse.Deflect, "Stop."),
                    R(SocialResponse.PushBack, "It's a rock.", true),
                    R(SocialResponse.Ignore, "Mm."),
                },
                weight: 0.4f);

            Add(SocialDialogueTopic.Strength, SocialAction.Encourage,
                "When shit went sideways, they stayed put.",
                new[]
                {
                    R(SocialResponse.Accept, "Nowhere to go."),
                    R(SocialResponse.Agree, "Steady's underrated."),
                    R(SocialResponse.Deflect, "Don't make a speech."),
                    R(SocialResponse.Ignore, "…"),
                    R(SocialResponse.PushBack, "Luck."),
                },
                strength: SocialStrengthKind.SteadyUnderPressure, weight: 1.2f);

            Add(SocialDialogueTopic.WeakPoint, SocialAction.Provoke,
                "All muscle. Judgment optional, apparently.",
                new[]
                {
                    R(SocialResponse.Escalate, "Say that to my face properly."),
                    R(SocialResponse.PushBack, "Keep talking."),
                    R(SocialResponse.Deflect, "Cute."),
                    R(SocialResponse.Withdraw, "…"),
                    R(SocialResponse.Ignore, "No."),
                },
                hostile: true, weak: SocialWeakPointKind.StrengthPride, weight: 0.95f);

            Add(SocialDialogueTopic.Work, SocialAction.Joke,
                "Bit dies and I'm writing the mountain a letter.",
                new[]
                {
                    R(SocialResponse.Accept, "Good luck getting a receipt."),
                    R(SocialResponse.Agree, "CC the bedrock. They're listening."),
                    R(SocialResponse.Deflect, "Less filing. More drilling."),
                    R(SocialResponse.PushBack, "Then don't break it.", true),
                    R(SocialResponse.Ignore, "Mm."),
                });

            // ——— THIN DOMAINS (Depth / Geology / Haul / Rescue / Toilet) ———
            Add(SocialDialogueTopic.Depth, SocialAction.Complain,
                "How far down are we?",
                new[]
                {
                    R(SocialResponse.Accept, "Far enough the surface forgot us."),
                    R(SocialResponse.Agree, "Don't ask numbers."),
                    R(SocialResponse.Deflect, "Still rock under your boots."),
                    R(SocialResponse.PushBack, "Want me to measure with your spine?"),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Depth, SocialAction.Connect,
                "Pressure feels different this deep.",
                new[]
                {
                    R(SocialResponse.Accept, "Ears know before the gauge does."),
                    R(SocialResponse.Agree, "Yeah. Heavier air."),
                    R(SocialResponse.Deflect, "Or you're tired."),
                    R(SocialResponse.Ignore, "Mm."),
                });

            Add(SocialDialogueTopic.Geology, SocialAction.Connect,
                "This seam's wrong.",
                new[]
                {
                    R(SocialResponse.Accept, "Wrong how?"),
                    R(SocialResponse.Agree, "Colour's off. Grain's lying."),
                    R(SocialResponse.Deflect, "Mark it. Dig later."),
                    R(SocialResponse.PushBack, "Everything's wrong down here."),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Work, SocialAction.Connect,
                "How much does that weigh?",
                new[]
                {
                    R(SocialResponse.Accept, "Enough."),
                    R(SocialResponse.Agree, "Useful unit."),
                    R(SocialResponse.Deflect, "Ask the cart."),
                    R(SocialResponse.PushBack, "Less than complaining about it."),
                    R(SocialResponse.Ignore, "Mm."),
                });

            Add(SocialDialogueTopic.Work, SocialAction.Encourage,
                "Need help?",
                new[]
                {
                    R(SocialResponse.PushBack, "No."),
                    R(SocialResponse.Accept, "That sounded painful."),
                    R(SocialResponse.Agree, "Still no."),
                    R(SocialResponse.Deflect, "I've got it."),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Work, SocialAction.Joke,
                "You know they make machines for that.",
                new[]
                {
                    R(SocialResponse.Accept, "I am the machine."),
                    R(SocialResponse.Agree, "Machine's busy. I'm free."),
                    R(SocialResponse.Deflect, "Machine doesn't swear as well."),
                    R(SocialResponse.PushBack, "Machine breaks. I don't.", true),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Rescue, SocialAction.Connect,
                "You came back.",
                new[]
                {
                    R(SocialResponse.Accept, "Obviously."),
                    R(SocialResponse.Agree, "Could've left me."),
                    R(SocialResponse.Deflect, "Still considering it."),
                    R(SocialResponse.PushBack, "Don't make it weird."),
                    R(SocialResponse.Ignore, "…"),
                },
                needsMem: true, mem: SocialMemoryType.RescuedByWorker, bonded: true, weight: 0.85f);

            Add(SocialDialogueTopic.Rescue, SocialAction.Connect,
                "Can you walk?",
                new[]
                {
                    R(SocialResponse.Accept, "Probably."),
                    R(SocialResponse.Agree, "That's not convincing."),
                    R(SocialResponse.Deflect, "Neither is standing here."),
                    R(SocialResponse.PushBack, "Watch me."),
                    R(SocialResponse.Withdraw, "…Help."),
                },
                needsMem: true, mem: SocialMemoryType.RescuedByWorker, weight: 0.9f);

            Add(SocialDialogueTopic.Trapped, SocialAction.Complain,
                "We're sealed.",
                new[]
                {
                    R(SocialResponse.Accept, "I noticed."),
                    R(SocialResponse.Agree, "Air's still moving. Barely."),
                    R(SocialResponse.Deflect, "Pick a wall. Dig."),
                    R(SocialResponse.Escalate, "Shut up and push."),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Toilet, SocialAction.Complain,
                "Again?",
                new[]
                {
                    R(SocialResponse.PushBack, "Don't."),
                    R(SocialResponse.Accept, "That's four."),
                    R(SocialResponse.Agree, "I can count."),
                    R(SocialResponse.Deflect, "Kit's problem."),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.Toilet, SocialAction.Joke,
                "Who destroyed the toilet?",
                new[]
                {
                    R(SocialResponse.Accept, "We agreed not to investigate."),
                    R(SocialResponse.Agree, "Statute of limitations."),
                    R(SocialResponse.Deflect, "Natural causes."),
                    R(SocialResponse.PushBack, "Ask quietly."),
                    R(SocialResponse.Ignore, "Mm."),
                });

            Add(SocialDialogueTopic.Darkness, SocialAction.Complain,
                "Lamp dies, I'm going home.",
                new[]
                {
                    R(SocialResponse.Accept, "You know the way?"),
                    R(SocialResponse.Agree, "Fuck."),
                    R(SocialResponse.Deflect, "Spare's in the pack. Maybe."),
                    R(SocialResponse.PushBack, "Then don't let it die."),
                    R(SocialResponse.Ignore, "…"),
                });

            Add(SocialDialogueTopic.NarrowTunnel, SocialAction.Complain,
                "Can't turn around.",
                new[]
                {
                    R(SocialResponse.Accept, "Then don't."),
                    R(SocialResponse.Agree, "Forward's cheaper."),
                    R(SocialResponse.Deflect, "Elbows in."),
                    R(SocialResponse.PushBack, "Complaining won't widen it."),
                    R(SocialResponse.Ignore, "Mm."),
                });
        }
    }
}
