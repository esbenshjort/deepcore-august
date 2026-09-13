using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Presentation-only dialogue tone snapshot. Does not affect Social Aura math.
    /// </summary>
    public struct SocialDialogueTone
    {
        public RelationshipClass RelClass;
        public float Trust, Warmth, Hostility, Respect;
        public float FrustrationSpeaker;
        public SocialAuraClass EmotionalClass;
        public bool HasMajorNegMemory;
        public bool HasMajorPosMemory;
        public SocialMemoryType MemoryHint;
        public bool HasMemoryHint;
        public SocialSituationFlags Situation;
        public int SpeakerId;
        public int ListenerId;
        public float GameHours;

        /// <summary>
        /// Build tone from speaker state + directed relation/memories (speaker → other).
        /// </summary>
        public static SocialDialogueTone From(
            WorkerRuntime speaker,
            SocialDirectedRelation relToward,
            IReadOnlyList<SocialMemoryEntry> memoriesToward,
            SocialSituationFlags situation = default,
            int listenerId = 0,
            float gameHours = 0f)
        {
            var tone = new SocialDialogueTone
            {
                RelClass = RelationshipClass.Neutral,
                Trust = 0f,
                Warmth = 0f,
                Hostility = 0f,
                Respect = SocialDirectedRelation.RespectBaseline,
                FrustrationSpeaker = speaker != null && speaker.State != null
                    ? speaker.State.Frustration
                    : 0f,
                EmotionalClass = SocialAuraClass.Neutral,
                MemoryHint = SocialMemoryType.HelpedMe,
                HasMemoryHint = false,
                Situation = situation,
                SpeakerId = speaker != null ? speaker.WorkerId : 0,
                ListenerId = listenerId,
                GameHours = gameHours,
            };

            if (speaker != null)
            {
                var expr = SocialExpressionModel.Derive(speaker.State, speaker.Stats);
                tone.EmotionalClass = expr.Class;
            }

            if (relToward != null)
            {
                tone.Trust = relToward.Trust;
                tone.Warmth = relToward.Warmth;
                tone.Hostility = relToward.Hostility;
                tone.Respect = relToward.Respect;
            }

            tone.RelClass = RelationshipClassifier.Classify(relToward, memoriesToward, out _);

            float bestStr = -1f;
            SocialMemoryType bestType = SocialMemoryType.HelpedMe;
            bool found = false;
            if (memoriesToward != null)
            {
                for (int i = 0; i < memoriesToward.Count; i++)
                {
                    var e = memoriesToward[i];
                    if (e == null || e.Strength < 0.2f) continue;
                    if (IsPositiveMem(e.Type) && e.Major) tone.HasMajorPosMemory = true;
                    if (IsNegativeMem(e.Type) && e.Major) tone.HasMajorNegMemory = true;
                    if (e.Strength > bestStr)
                    {
                        bestStr = e.Strength;
                        bestType = e.Type;
                        found = true;
                    }
                }
            }

            if (found)
            {
                tone.MemoryHint = bestType;
                tone.HasMemoryHint = true;
            }

            return tone;
        }

        static bool IsPositiveMem(SocialMemoryType t) =>
            t == SocialMemoryType.HelpedMe
            || t == SocialMemoryType.SupportedMe
            || t == SocialMemoryType.WorkedWellTogether
            || t == SocialMemoryType.SharedSuccess
            || t == SocialMemoryType.SharedHardship
            || t == SocialMemoryType.TookMySide
            || t == SocialMemoryType.Apologized;

        static bool IsNegativeMem(SocialMemoryType t) =>
            t == SocialMemoryType.LetMeDown
            || t == SocialMemoryType.BlamedMe
            || t == SocialMemoryType.FailedTogether
            || t == SocialMemoryType.InsultedMe
            || t == SocialMemoryType.HurtBy
            || t == SocialMemoryType.WitnessedViolence
            || t == SocialMemoryType.KilledBy
            || t == SocialMemoryType.WitnessedDeath;
    }

    /// <summary>
    /// Authored line bank for resolved Social Aura exchanges.
    /// Wording only — does not change Stage 0/1 outcomes.
    /// </summary>
    public static class SocialAuraLineBank
    {
        static readonly System.Random Rng = new(4217);
        static readonly List<string> Scratch = new(64);

        public static string PickInitiator(SocialAction action, bool actionOk, SocialContext ctx) =>
            PickInitiator(action, actionOk, ctx, default);

        public static string PickInitiator(
            SocialAction action, bool actionOk, SocialContext ctx, in SocialDialogueTone tone)
        {
            SocialDialogueExchanges.ClearActive();
            string v2 = SocialDialogueExchanges.PickInitiator(
                action, actionOk, ctx, in tone, tone.Situation,
                tone.SpeakerId, tone.ListenerId, tone.GameHours);
            if (!string.IsNullOrEmpty(v2))
                return v2;

            Scratch.Clear();
            AddInitiatorBase(Scratch, action, actionOk, ctx);
            int baseCount = Scratch.Count;
            AddInitiatorTone(Scratch, action, actionOk, ctx, in tone);
            FilterRecent(Scratch, tone.GameHours);
            return PickWeighted(Scratch, baseCount, in tone);
        }

        public static string PickResponse(
            SocialResponse response, SocialAction action, bool actionOk, SocialContext ctx) =>
            PickResponse(response, action, actionOk, ctx, default);

        public static string PickResponse(
            SocialResponse response,
            SocialAction action,
            bool actionOk,
            SocialContext ctx,
            in SocialDialogueTone tone)
        {
            string paired = SocialDialogueExchanges.PickResponse(
                response, action, actionOk, in tone, tone.GameHours);
            if (!string.IsNullOrEmpty(paired))
                return paired;

            // Failed positive actions should feel rejected, not warm
            if (!actionOk
                && IsPositive(action)
                && (response == SocialResponse.Accept || response == SocialResponse.Agree))
            {
                Scratch.Clear();
                Scratch.Add("…Sure. Whatever you say.");
                Scratch.Add("Mm. Fine.");
                Scratch.Add("Yeah. Okay.");
                Scratch.Add("…Right.");
                if (tone.FrustrationSpeaker >= 55f)
                {
                    Scratch.Add("Fine.");
                    Scratch.Add("Whatever.");
                }
                FilterRecent(Scratch, tone.GameHours);
                return Pick(Scratch);
            }

            Scratch.Clear();
            AddResponseBase(Scratch, response, action, actionOk, ctx);
            int baseCount = Scratch.Count;
            AddResponseTone(Scratch, response, action, actionOk, ctx, in tone);
            FilterRecent(Scratch, tone.GameHours);
            return PickWeighted(Scratch, baseCount, in tone);
        }

        public static string PickOptionalCloser(SocialEncounterLog log) =>
            PickOptionalCloser(log, default);

        public static string PickOptionalCloser(SocialEncounterLog log, in SocialDialogueTone tone)
        {
            string v2Closer = SocialDialogueExchanges.PickCloser(log, tone.GameHours);
            if (!string.IsNullOrEmpty(v2Closer))
                return v2Closer;

            if (log == null || string.IsNullOrEmpty(log.OutcomeSummary)) return null;
            string o = log.OutcomeSummary;
            Scratch.Clear();
            if (o.Contains("SHARED_COMPLAINT_BOND"))
            {
                Scratch.Add("Good. Then we dig out of it.");
                Scratch.Add("Alright. Pair up. Move.");
                Scratch.Add("Okay. Back to it — together.");
                Scratch.Add("Then we fix it. Shift's not done.");
                if (tone.RelClass == RelationshipClass.Bonded || tone.RelClass == RelationshipClass.Friendly)
                {
                    Scratch.Add("Knew you'd get it. Let's move.");
                    Scratch.Add("That's my partner. Dig.");
                }
                if (tone.RelClass == RelationshipClass.Professional)
                    Scratch.Add("Agreed. Execute.");
            }
            else if (o.Contains("CLASH"))
            {
                Scratch.Add("Fine. Remember that.");
                Scratch.Add("We're not finished.");
                Scratch.Add("Noted. Don't forget I heard you.");
                Scratch.Add("Yeah. File that away.");
                if (tone.RelClass == RelationshipClass.Grudge || tone.RelClass == RelationshipClass.Rivalry)
                {
                    Scratch.Add("Same as always.");
                    Scratch.Add("Keep talking. I'll remember.");
                }
                if (tone.FrustrationSpeaker >= 55f)
                {
                    Scratch.Add("Done.");
                    Scratch.Add("Enough.");
                }
            }
            else if (o.Contains("POSITIVE_FAIL"))
            {
                Scratch.Add("…Right. Noted.");
                Scratch.Add("Okay. Message received.");
                Scratch.Add("…Got it.");
                Scratch.Add("Fine. Won't push.");
                if (tone.RelClass == RelationshipClass.Strained || tone.RelClass == RelationshipClass.Grudge)
                    Scratch.Add("Figured. Still tried.");
            }
            else if (o.Contains("IGNORED_AGGRESSION"))
            {
                Scratch.Add("Coward.");
                Scratch.Add("Whatever.");
                Scratch.Add("Walk away then.");
                Scratch.Add("Sure. Hide.");
                if (tone.FrustrationSpeaker >= 55f)
                    Scratch.Add("Tch.");
            }
            else return null;

            return Pick(Scratch);
        }

        public static bool IsPositive(SocialAction a) =>
            a == SocialAction.Encourage || a == SocialAction.Joke || a == SocialAction.Connect;

        /// <summary>Stable first-line picks for audits (no RNG).</summary>
        public static string FirstInitiator(SocialAction action, bool actionOk, SocialContext ctx)
        {
            return action switch
            {
                SocialAction.Encourage => actionOk ? "Nice work. Keep that pace." : "Come on, you're better than this.",
                SocialAction.Joke => actionOk ? "If rock could sue, it'd name us." : "Cheer up — could be worse. …Alright, maybe not.",
                SocialAction.Connect => actionOk ? "You alright? No performance. Just asking." : "We should talk. Properly.",
                SocialAction.Complain => ctx == SocialContext.SharedProblem
                    ? "This whole setup's fighting us. You feel it too?"
                    : "I am done with this nonsense.",
                SocialAction.Provoke => "That your best? Really?",
                SocialAction.Confront => "We're settling this. Now.",
                _ => "…",
            };
        }

        public static string FirstResponse(SocialResponse response, SocialAction action, bool actionOk, SocialContext ctx)
        {
            if (!actionOk && IsPositive(action) && response == SocialResponse.Deflect)
                return "Don't manage me.";
            if (!actionOk && IsPositive(action) && response == SocialResponse.PushBack)
                return "That landed wrong.";
            if (!actionOk && IsPositive(action) && response == SocialResponse.Withdraw)
                return "I'm done with this chat.";
            return response switch
            {
                SocialResponse.Accept => "Fair enough.",
                SocialResponse.Agree => action == SocialAction.Complain && ctx == SocialContext.SharedProblem
                    ? "Yeah. Same mess. At least we're not alone in it."
                    : "You're not wrong.",
                SocialResponse.Deflect => "Not now.",
                SocialResponse.Ignore => "Not worth it.",
                SocialResponse.PushBack => "Back off.",
                SocialResponse.Escalate => "You want a problem? You've got one.",
                SocialResponse.Withdraw => "Not doing this.",
                _ => "…",
            };
        }

        // ─── Base initiator pools ───────────────────────────────────────────

        static void AddInitiatorBase(List<string> pool, SocialAction action, bool actionOk, SocialContext ctx)
        {
            switch (action)
            {
                case SocialAction.Encourage:
                    if (actionOk)
                    {
                        AddCtx(pool, ctx,
                            shared: new[]
                            {
                                "Hey — we get through this. Keep your head.",
                                "Steady. You're still the one who can fix it.",
                                "Don't flinch. We've pulled worse out of rock.",
                                "Breathe. Then hit it again.",
                                "I've seen you dig out of worse. Do it.",
                            },
                            work: new[]
                            {
                                "Nice work. Keep that pace.",
                                "You're doing fine. Don't second-guess it.",
                                "Solid. Hold that rhythm.",
                                "That's the cut. Stay on it.",
                                "Good hands. Don't overthink the next pass.",
                                "Keep the line. You're earning this shift.",
                            },
                            camp: new[]
                            {
                                "You look rough. Hang in there.",
                                "We've got this shift. One step.",
                                "Eat something. Then we talk jobs.",
                                "Camp's loud. You're quieter — that's fine.",
                                "Rest while you can. Tomorrow's worse.",
                            },
                            idle: new[]
                            {
                                "You look rough. Hang in there.",
                                "We've got this shift. One step.",
                                "Still standing. That's already something.",
                            });
                    }
                    else
                    {
                        pool.Add("Come on, you're better than this.");
                        pool.Add("Chin up. You're slowing us down.");
                        pool.Add("Hey. Focus up.");
                        pool.Add("That pace won't cut it.");
                        pool.Add("Snap out of it.");
                    }
                    break;

                case SocialAction.Joke:
                    if (actionOk)
                    {
                        AddCtx(pool, ctx,
                            shared: new[]
                            {
                                "If this vein hates us, at least it's consistent.",
                                "Worst day underground? Give it an hour.",
                                "Rock's got jokes. We're the punchline.",
                                "At least the cave-in would be faster.",
                            },
                            work: new[]
                            {
                                "Machine's moodier than Viktor before coffee.",
                                "If rock could sue, it'd name us.",
                                "Bit's singing. Or screaming. Same difference.",
                                "Another meter, another insult from geology.",
                                "I rate this seam: two stars, would not recommend.",
                            },
                            camp: new[]
                            {
                                "Camp food's a hate crime. Pass it.",
                                "I dreamt the washer thanked me. Bad sign.",
                                "Beds are harder than the ore. Sleep tight.",
                                "If the stew moves, it's still stew.",
                            },
                            idle: new[]
                            {
                                "Camp food's a hate crime. Pass it.",
                                "I dreamt the washer thanked me. Bad sign.",
                                "Quiet shift means the rock's planning something.",
                            });
                    }
                    else
                    {
                        pool.Add("Cheer up — could be worse. …Alright, maybe not.");
                        pool.Add("Lighten up. Or don't. Your face already did.");
                        pool.Add("Smile. Or grimace. Close enough.");
                        pool.Add("That was a joke. Mostly.");
                    }
                    break;

                case SocialAction.Connect:
                    if (actionOk)
                    {
                        AddCtx(pool, ctx,
                            shared: new[]
                            {
                                "You alright? No performance. Just asking.",
                                "Talk to me a second. What's weighing on you?",
                                "This mess — you holding?",
                                "Check in. Honest answer only.",
                            },
                            work: new[]
                            {
                                "You alright? No performance. Just asking.",
                                "Talk to me a second. What's weighing on you?",
                                "How's the load from your side?",
                                "Need a second pair of eyes? Say it.",
                                "You gone quiet on the line. Talk.",
                            },
                            camp: new[]
                            {
                                "You alright? No performance. Just asking.",
                                "Talk to me a second. What's weighing on you?",
                                "Off shift. Real talk — you okay?",
                                "Sit a minute. No clipboard.",
                                "Camp's the only place we can say it.",
                            },
                            idle: new[]
                            {
                                "You alright? No performance. Just asking.",
                                "Talk to me a second. What's weighing on you?",
                                "Got a minute? Not work talk.",
                            });
                    }
                    else
                    {
                        pool.Add("We should talk. Properly.");
                        pool.Add("Don't shut me out — not today.");
                        pool.Add("Hey. Eyes up. Talk to me.");
                        pool.Add("This silence isn't helping.");
                    }
                    break;

                case SocialAction.Complain:
                    if (ctx == SocialContext.SharedProblem || ctx == SocialContext.RecentFailure)
                    {
                        pool.Add("This whole setup's fighting us. You feel it too?");
                        pool.Add("Same headache, same dead end. I'm sick of it.");
                        pool.Add("Everything's jammed and the clock doesn't care.");
                        pool.Add("Same wall. Same bad options.");
                        pool.Add("I hate this seam. You hate it. Good.");
                    }
                    else if (ctx == SocialContext.Camp)
                    {
                        pool.Add("Even camp can't rinse this day off.");
                        pool.Add("I am done with this nonsense.");
                        pool.Add("They want miracles on scrap rations.");
                        pool.Add("Off shift and still chewing on the mess.");
                    }
                    else
                    {
                        pool.Add("I am done with this nonsense.");
                        pool.Add("Everything's jammed and nobody's listening.");
                        pool.Add("This job's eating hours for nothing.");
                        pool.Add("I'm tired of babysitting broken gear.");
                        pool.Add("Somebody sold us a bad plan.");
                    }
                    break;

                case SocialAction.Provoke:
                    pool.Add("That your best? Really?");
                    pool.Add("Maybe the problem's standing right there.");
                    pool.Add("Impressive. In the wrong direction.");
                    pool.Add("You always this slow, or is today special?");
                    pool.Add("Keep that up — we'll be here next week.");
                    break;

                case SocialAction.Confront:
                    pool.Add("We're settling this. Now.");
                    pool.Add("Say it straight — or step aside.");
                    pool.Add("No more soft talk. Out with it.");
                    pool.Add("You and me. Clear the air.");
                    pool.Add("I've bitten my tongue long enough.");
                    break;
            }
        }

        static void AddInitiatorTone(
            List<string> pool, SocialAction action, bool actionOk, SocialContext ctx, in SocialDialogueTone tone)
        {
            bool highFrust = tone.FrustrationSpeaker >= 55f;
            bool camp = ctx == SocialContext.Camp || ctx == SocialContext.IdleNearby;
            bool workish = ctx == SocialContext.WorkingTogether
                           || ctx == SocialContext.RecentSuccess
                           || ctx == SocialContext.Emergency;

            switch (tone.RelClass)
            {
                case RelationshipClass.Friendly:
                case RelationshipClass.Bonded:
                    AddFriendlyInitiator(pool, action, actionOk, camp, workish, in tone);
                    break;
                case RelationshipClass.Professional:
                    AddProfessionalInitiator(pool, action, actionOk, workish);
                    break;
                case RelationshipClass.Rivalry:
                    AddRivalryInitiator(pool, action, actionOk);
                    break;
                case RelationshipClass.Grudge:
                    AddGrudgeInitiator(pool, action, actionOk, in tone);
                    break;
                case RelationshipClass.Strained:
                    AddStrainedInitiator(pool, action, actionOk);
                    break;
            }

            if (highFrust)
                AddFrustratedInitiator(pool, action, actionOk);

            if (tone.EmotionalClass == SocialAuraClass.Negative)
            {
                if (action == SocialAction.Complain || action == SocialAction.Confront)
                    pool.Add("I'm already raw. Don't make me spell it out.");
                if (action == SocialAction.Joke && actionOk)
                    pool.Add("Dark humor's all I've got left. Take it.");
            }
            else if (tone.EmotionalClass == SocialAuraClass.Positive && IsPositive(action) && actionOk)
            {
                pool.Add("Mood's decent. Sharing while it lasts.");
            }

            if (tone.HasMajorNegMemory
                && (action == SocialAction.Confront || action == SocialAction.Provoke || action == SocialAction.Complain))
            {
                AddNegMemoryInitiator(pool, tone.MemoryHint, tone.HasMemoryHint);
            }

            if (tone.HasMajorPosMemory
                && actionOk
                && (action == SocialAction.Encourage || action == SocialAction.Connect)
                && (tone.RelClass == RelationshipClass.Friendly || tone.RelClass == RelationshipClass.Bonded))
            {
                AddPosMemoryInitiator(pool, tone.MemoryHint, tone.HasMemoryHint);
            }

            if (tone.Hostility >= 6f && (action == SocialAction.Provoke || action == SocialAction.Confront))
                pool.Add("Don't pretend we're fine.");
            if (tone.Warmth >= 6f && action == SocialAction.Encourage && actionOk)
                pool.Add("I mean it. You're solid.");
            if (tone.Respect >= 70f && actionOk && (action == SocialAction.Encourage || action == SocialAction.Connect))
                pool.Add("Respect the craft. Keep going.");
            if (tone.Trust <= -3f && action == SocialAction.Connect)
                pool.Add("I don't trust easy. Still asking.");
        }

        static void AddFriendlyInitiator(
            List<string> pool, SocialAction action, bool actionOk, bool camp, bool workish, in SocialDialogueTone tone)
        {
            switch (action)
            {
                case SocialAction.Encourage:
                    if (actionOk)
                    {
                        pool.Add("Hey partner — you've got this.");
                        pool.Add("I trust your hands. Keep them moving.");
                        pool.Add("We cover each other. Same as always.");
                        if (camp) pool.Add("Rest up. I've got your back tomorrow.");
                        if (workish) pool.Add("Side by side. Don't drop the rhythm.");
                        if (tone.RelClass == RelationshipClass.Bonded)
                        {
                            pool.Add("You and me through worse rock than this.");
                            pool.Add("I'd dig next to you in a collapse. Keep going.");
                        }
                    }
                    else
                    {
                        pool.Add("C'mon — I know you. Shake it off.");
                        pool.Add("Not like you. Fix it.");
                    }
                    break;
                case SocialAction.Joke:
                    if (actionOk)
                    {
                        pool.Add("Remember that washer story? Still funny.");
                        pool.Add("If we die down here, at least the jokes were good.");
                    }
                    break;
                case SocialAction.Connect:
                    if (actionOk)
                    {
                        pool.Add("Between us — you holding?");
                        pool.Add("No crew speech. Just you and me.");
                        if (camp) pool.Add("Camp check. How's the head?");
                    }
                    break;
                case SocialAction.Complain:
                    pool.Add("Tell me I'm not crazy — this shift's cursed.");
                    pool.Add("Venting to you 'cause you'll get it.");
                    break;
                case SocialAction.Provoke:
                    pool.Add("Friendly fire: that was sloppy.");
                    pool.Add("I can say this — do better.");
                    break;
                case SocialAction.Confront:
                    pool.Add("We're too close to leave this rotten.");
                    pool.Add("Friends settle it. Talk.");
                    break;
            }
        }

        static void AddProfessionalInitiator(List<string> pool, SocialAction action, bool actionOk, bool workish)
        {
            switch (action)
            {
                case SocialAction.Encourage:
                    if (actionOk)
                    {
                        pool.Add("Competent work. Maintain.");
                        pool.Add("Standards look good. Hold them.");
                        if (workish) pool.Add("Procedure's sound. Continue.");
                    }
                    else pool.Add("Below standard. Correct it.");
                    break;
                case SocialAction.Joke:
                    if (actionOk) pool.Add("Brief levity. Then back to meters.");
                    else pool.Add("Humor later. Output now.");
                    break;
                case SocialAction.Connect:
                    if (actionOk) pool.Add("Status check — anything blocking you?");
                    else pool.Add("Need a straight status. Now.");
                    break;
                case SocialAction.Complain:
                    pool.Add("Process is failing. Flagging it.");
                    pool.Add("This workflow's inefficient and it's costing us.");
                    break;
                case SocialAction.Provoke:
                    pool.Add("That performance wouldn't pass review.");
                    break;
                case SocialAction.Confront:
                    pool.Add("Clarify the issue. No drama — facts.");
                    break;
            }
        }

        static void AddRivalryInitiator(List<string> pool, SocialAction action, bool actionOk)
        {
            switch (action)
            {
                case SocialAction.Encourage:
                    if (actionOk)
                    {
                        pool.Add("Not bad. Still not better than me.");
                        pool.Add("Keep pace — I hate waiting on you.");
                        pool.Add("Alright. Prove you belong on this line.");
                    }
                    else pool.Add("Falling behind already?");
                    break;
                case SocialAction.Joke:
                    if (actionOk) pool.Add("Even the rock prefers my cuts.");
                    else pool.Add("Tough crowd. Or just you.");
                    break;
                case SocialAction.Connect:
                    if (actionOk) pool.Add("Don't make this soft. What's the holdup?");
                    break;
                case SocialAction.Complain:
                    pool.Add("If you'd done your part, we wouldn't be here.");
                    break;
                case SocialAction.Provoke:
                    pool.Add("Still chasing my numbers?");
                    pool.Add("Come on — make me respect it.");
                    break;
                case SocialAction.Confront:
                    pool.Add("Scoreboard time. Talk.");
                    pool.Add("You want the argument? Let's have it.");
                    break;
            }
        }

        static void AddGrudgeInitiator(List<string> pool, SocialAction action, bool actionOk, in SocialDialogueTone tone)
        {
            switch (action)
            {
                case SocialAction.Encourage:
                    if (actionOk)
                    {
                        pool.Add("Do the job. Don't need a speech from me.");
                        pool.Add("Keep working. That's all I'm asking.");
                        pool.Add("Useful. For once.");
                    }
                    else pool.Add("Figures. Same old.");
                    break;
                case SocialAction.Joke:
                    if (actionOk) pool.Add("Funny how some people never change.");
                    else pool.Add("Don't smile at me.");
                    break;
                case SocialAction.Connect:
                    if (actionOk) pool.Add("Not friends. Still need a word.");
                    else pool.Add("You owe me a straight answer.");
                    break;
                case SocialAction.Complain:
                    pool.Add("Thanks to last time, this is worse.");
                    pool.Add("I'm not forgetting who left me hanging.");
                    break;
                case SocialAction.Provoke:
                    pool.Add("Still the same problem you always were.");
                    pool.Add("You think I forgot?");
                    break;
                case SocialAction.Confront:
                    pool.Add("This isn't new. You know what you did.");
                    pool.Add("We're past polite. Own it.");
                    break;
            }

            if (tone.HasMajorNegMemory)
            {
                pool.Add("I remember. Don't act surprised.");
                pool.Add("That debt's still open.");
            }
        }

        static void AddStrainedInitiator(List<string> pool, SocialAction action, bool actionOk)
        {
            switch (action)
            {
                case SocialAction.Encourage:
                    if (actionOk)
                    {
                        pool.Add("Look — just… keep going.");
                        pool.Add("We're awkward. Job still needs doing.");
                    }
                    else pool.Add("Don't make this weirder.");
                    break;
                case SocialAction.Connect:
                    if (actionOk) pool.Add("This is strained. I'm still asking.");
                    else pool.Add("We can't keep skating past this.");
                    break;
                case SocialAction.Complain:
                    pool.Add("Everything's off between us and the work.");
                    break;
                case SocialAction.Confront:
                    pool.Add("The air's bad. Clear it.");
                    break;
                case SocialAction.Provoke:
                    pool.Add("Tension's thick enough. Don't add to it — wait, do.");
                    break;
            }
        }

        static void AddFrustratedInitiator(List<string> pool, SocialAction action, bool actionOk)
        {
            switch (action)
            {
                case SocialAction.Encourage:
                    pool.Add(actionOk ? "Move. Now." : "Fix it.");
                    pool.Add(actionOk ? "Don't stall." : "Wake up.");
                    break;
                case SocialAction.Joke:
                    pool.Add("Ha. Whatever.");
                    pool.Add("Not funny. Still saying it.");
                    break;
                case SocialAction.Connect:
                    pool.Add("Talk. Short.");
                    pool.Add("Spill it.");
                    break;
                case SocialAction.Complain:
                    pool.Add("I'm done.");
                    pool.Add("This is garbage.");
                    break;
                case SocialAction.Provoke:
                    pool.Add("Useless.");
                    pool.Add("Try harder.");
                    break;
                case SocialAction.Confront:
                    pool.Add("Now.");
                    pool.Add("Out with it.");
                    break;
            }
        }

        static void AddNegMemoryInitiator(List<string> pool, SocialMemoryType hint, bool hasHint)
        {
            pool.Add("Last time still sits wrong.");
            if (!hasHint) return;
            switch (hint)
            {
                case SocialMemoryType.InsultedMe:
                    pool.Add("You mouthed off before. Don't again.");
                    break;
                case SocialMemoryType.LetMeDown:
                    pool.Add("You left me hanging once. Not again.");
                    break;
                case SocialMemoryType.BlamedMe:
                    pool.Add("You pinned it on me. Remember that?");
                    break;
                case SocialMemoryType.FailedTogether:
                    pool.Add("We burned that job together. Still tastes bad.");
                    break;
                default:
                    pool.Add("I've got reasons to stay sharp with you.");
                    break;
            }
        }

        static void AddPosMemoryInitiator(List<string> pool, SocialMemoryType hint, bool hasHint)
        {
            pool.Add("You've pulled me out before. Paying it forward.");
            if (!hasHint) return;
            switch (hint)
            {
                case SocialMemoryType.HelpedMe:
                case SocialMemoryType.SupportedMe:
                    pool.Add("You had my back. I've got yours.");
                    break;
                case SocialMemoryType.SharedHardship:
                    pool.Add("We survived that mess. This is nothing.");
                    break;
                case SocialMemoryType.SharedSuccess:
                    pool.Add("That win still counts. Keep the streak.");
                    break;
                case SocialMemoryType.TookMySide:
                    pool.Add("You stood with me. I don't forget.");
                    break;
                case SocialMemoryType.WorkedWellTogether:
                    pool.Add("We click on the line. Stay with it.");
                    break;
                default:
                    pool.Add("Good history. Lean on it.");
                    break;
            }
        }

        // ─── Base response pools ────────────────────────────────────────────

        static void AddResponseBase(
            List<string> pool, SocialResponse response, SocialAction action, bool actionOk, SocialContext ctx)
        {
            switch (response)
            {
                case SocialResponse.Accept:
                    if (action == SocialAction.Encourage)
                    {
                        pool.Add("Yeah. Thanks. Needed that.");
                        pool.Add("Alright. I'll hold the line.");
                        pool.Add("Heard. Back at it.");
                        pool.Add("…Appreciate it.");
                    }
                    else
                    {
                        pool.Add("Fair enough.");
                        pool.Add("Okay. I hear you.");
                        pool.Add("Copy that.");
                        pool.Add("Alright.");
                    }
                    break;

                case SocialResponse.Agree:
                    if (action == SocialAction.Complain && ctx == SocialContext.SharedProblem)
                    {
                        pool.Add("Yeah. Same mess. At least we're not alone in it.");
                        pool.Add("Exactly. We'll chew through it together.");
                        pool.Add("Same grit in my teeth. Let's push.");
                        pool.Add("You're right. Shared problem, shared fix.");
                    }
                    else if (ctx == SocialContext.Camp)
                    {
                        pool.Add("Yeah. Even off shift it sticks.");
                        pool.Add("You're not wrong.");
                        pool.Add("Camp agrees with you, somehow.");
                    }
                    else
                    {
                        pool.Add("You're not wrong.");
                        pool.Add("Same here.");
                        pool.Add("Can't argue that.");
                        pool.Add("True enough.");
                    }
                    break;

                case SocialResponse.Deflect:
                    if (!actionOk && IsPositive(action))
                    {
                        pool.Add("Don't manage me.");
                        pool.Add("Save the pep talk.");
                        pool.Add("Not the time for coaching.");
                        pool.Add("Keep the speech.");
                    }
                    else
                    {
                        pool.Add("Not now.");
                        pool.Add("Later. Focus.");
                        pool.Add("Busy.");
                        pool.Add("Park it.");
                    }
                    break;

                case SocialResponse.Ignore:
                    if (action == SocialAction.Provoke || action == SocialAction.Confront)
                    {
                        pool.Add("…");
                        pool.Add("Not worth it.");
                        pool.Add("Hearing static.");
                        pool.Add("Moving on.");
                    }
                    else
                    {
                        pool.Add("Busy.");
                        pool.Add("Mhm.");
                        pool.Add("…");
                        pool.Add("Working.");
                    }
                    break;

                case SocialResponse.PushBack:
                    if (!actionOk && IsPositive(action))
                    {
                        pool.Add("That landed wrong.");
                        pool.Add("Don't talk to me like that.");
                        pool.Add("Wrong tone.");
                        pool.Add("Try again without the dig.");
                    }
                    else
                    {
                        pool.Add("Back off.");
                        pool.Add("Watch your mouth.");
                        pool.Add("Ease up.");
                        pool.Add("Don't.");
                    }
                    break;

                case SocialResponse.Escalate:
                    pool.Add("You want a problem? You've got one.");
                    pool.Add("Say that again.");
                    pool.Add("Keep going. See what happens.");
                    pool.Add("You picked a fight. Fine.");
                    pool.Add("Oh we're doing this.");
                    break;

                case SocialResponse.Withdraw:
                    if (!actionOk && IsPositive(action))
                    {
                        pool.Add("I'm done with this chat.");
                        pool.Add("Leave me alone.");
                        pool.Add("Enough. Walking.");
                        pool.Add("Nope. Out.");
                    }
                    else
                    {
                        pool.Add("Not doing this.");
                        pool.Add("I'm walking.");
                        pool.Add("Later. Or never.");
                        pool.Add("Done talking.");
                    }
                    break;
            }
        }

        static void AddResponseTone(
            List<string> pool,
            SocialResponse response,
            SocialAction action,
            bool actionOk,
            SocialContext ctx,
            in SocialDialogueTone tone)
        {
            bool highFrust = tone.FrustrationSpeaker >= 55f;

            switch (tone.RelClass)
            {
                case RelationshipClass.Friendly:
                case RelationshipClass.Bonded:
                    AddFriendlyResponse(pool, response, action, actionOk, in tone);
                    break;
                case RelationshipClass.Professional:
                    AddProfessionalResponse(pool, response, action);
                    break;
                case RelationshipClass.Rivalry:
                    AddRivalryResponse(pool, response, action);
                    break;
                case RelationshipClass.Grudge:
                    AddGrudgeResponse(pool, response, action, in tone);
                    break;
                case RelationshipClass.Strained:
                    AddStrainedResponse(pool, response);
                    break;
            }

            if (highFrust)
            {
                switch (response)
                {
                    case SocialResponse.Accept: pool.Add("Fine."); break;
                    case SocialResponse.Agree: pool.Add("Yeah. Whatever."); break;
                    case SocialResponse.Deflect: pool.Add("Not now."); break;
                    case SocialResponse.Ignore: pool.Add("…"); break;
                    case SocialResponse.PushBack: pool.Add("Stop."); break;
                    case SocialResponse.Escalate: pool.Add("Try me."); break;
                    case SocialResponse.Withdraw: pool.Add("Out."); break;
                }
            }

            if (tone.HasMajorNegMemory
                && (response == SocialResponse.PushBack
                    || response == SocialResponse.Escalate
                    || response == SocialResponse.Deflect))
            {
                pool.Add("After what you pulled? Soft chance.");
                if (tone.HasMemoryHint && tone.MemoryHint == SocialMemoryType.InsultedMe)
                    pool.Add("You already took a shot. Don't.");
                if (tone.HasMemoryHint && tone.MemoryHint == SocialMemoryType.LetMeDown)
                    pool.Add("You left me once. Ears closed.");
            }

            if (tone.HasMajorPosMemory
                && (response == SocialResponse.Accept || response == SocialResponse.Agree)
                && (tone.RelClass == RelationshipClass.Friendly || tone.RelClass == RelationshipClass.Bonded))
            {
                pool.Add("From you? I'll take it.");
                pool.Add("You earned the right to say that.");
            }

            if (ctx == SocialContext.Camp && response == SocialResponse.Agree)
                pool.Add("Even the bunkhouse would nod.");
        }

        static void AddFriendlyResponse(
            List<string> pool, SocialResponse response, SocialAction action, bool actionOk, in SocialDialogueTone tone)
        {
            switch (response)
            {
                case SocialResponse.Accept:
                    pool.Add("Thanks, partner.");
                    pool.Add("Yeah. Needed a friend on that.");
                    if (tone.RelClass == RelationshipClass.Bonded)
                        pool.Add("You always know when to say it.");
                    break;
                case SocialResponse.Agree:
                    pool.Add("With you. Always.");
                    pool.Add("Same page. Let's dig.");
                    break;
                case SocialResponse.Deflect:
                    pool.Add("Love you, but not now.");
                    pool.Add("Hold that thought, friend.");
                    break;
                case SocialResponse.PushBack:
                    pool.Add("Hey — soft. That stung.");
                    pool.Add("We're close enough I can say: ease off.");
                    break;
                case SocialResponse.Escalate:
                    pool.Add("Don't make me fight my own.");
                    break;
                case SocialResponse.Withdraw:
                    pool.Add("Need space. Not done with you — later.");
                    break;
                case SocialResponse.Ignore:
                    if (action == SocialAction.Joke) pool.Add("Heard. Filing under later.");
                    break;
            }
        }

        static void AddProfessionalResponse(List<string> pool, SocialResponse response, SocialAction action)
        {
            switch (response)
            {
                case SocialResponse.Accept:
                    pool.Add("Acknowledged.");
                    pool.Add("Understood. Proceeding.");
                    break;
                case SocialResponse.Agree:
                    pool.Add("Assessment matches.");
                    pool.Add("Concur.");
                    break;
                case SocialResponse.Deflect:
                    pool.Add("Noted for later. Current task first.");
                    break;
                case SocialResponse.PushBack:
                    pool.Add("Disagree with the framing.");
                    break;
                case SocialResponse.Escalate:
                    pool.Add("Escalate then. On record.");
                    break;
                case SocialResponse.Withdraw:
                    pool.Add("Disengaging. Resume when useful.");
                    break;
            }
        }

        static void AddRivalryResponse(List<string> pool, SocialResponse response, SocialAction action)
        {
            switch (response)
            {
                case SocialResponse.Accept:
                    pool.Add("Don't get used to agreeing.");
                    pool.Add("Fine. Point to you.");
                    break;
                case SocialResponse.Agree:
                    pool.Add("Hate that you're right.");
                    break;
                case SocialResponse.Deflect:
                    pool.Add("Save it for the scoreboard.");
                    break;
                case SocialResponse.PushBack:
                    pool.Add("Cute. Still wrong.");
                    pool.Add("You wish that landed.");
                    break;
                case SocialResponse.Escalate:
                    pool.Add("Finally. Bring it.");
                    pool.Add("Been waiting for this.");
                    break;
                case SocialResponse.Ignore:
                    pool.Add("Not giving you the win.");
                    break;
                case SocialResponse.Withdraw:
                    pool.Add("Round later. Count on it.");
                    break;
            }
        }

        static void AddGrudgeResponse(
            List<string> pool, SocialResponse response, SocialAction action, in SocialDialogueTone tone)
        {
            switch (response)
            {
                case SocialResponse.Accept:
                    pool.Add("…Fine. Don't make it a habit.");
                    pool.Add("I'll take the work talk. Not the friendship.");
                    break;
                case SocialResponse.Agree:
                    pool.Add("On the problem. Not on you.");
                    break;
                case SocialResponse.Deflect:
                    pool.Add("Don't act like we're good.");
                    pool.Add("Save the concern.");
                    break;
                case SocialResponse.PushBack:
                    pool.Add("After everything? No.");
                    pool.Add("You don't get to talk soft now.");
                    break;
                case SocialResponse.Escalate:
                    pool.Add("Been storing this. Here it is.");
                    pool.Add("Good. Say it so I can answer.");
                    break;
                case SocialResponse.Ignore:
                    pool.Add("…");
                    pool.Add("Nothing for you.");
                    break;
                case SocialResponse.Withdraw:
                    pool.Add("Walking. Still remembering.");
                    break;
            }

            if (action == SocialAction.Encourage && response == SocialResponse.Deflect)
                pool.Add("Your pep doesn't erase it.");
        }

        static void AddStrainedResponse(List<string> pool, SocialResponse response)
        {
            switch (response)
            {
                case SocialResponse.Accept:
                    pool.Add("Okay. Awkward, but okay.");
                    break;
                case SocialResponse.Agree:
                    pool.Add("Yeah. Even if this is weird.");
                    break;
                case SocialResponse.Deflect:
                    pool.Add("Not ready for that conversation.");
                    break;
                case SocialResponse.PushBack:
                    pool.Add("Careful — we're already thin ice.");
                    break;
                case SocialResponse.Withdraw:
                    pool.Add("Need distance.");
                    break;
            }
        }

        // ─── Helpers ────────────────────────────────────────────────────────

        static void AddCtx(
            List<string> pool,
            SocialContext ctx,
            string[] shared,
            string[] work,
            string[] camp,
            string[] idle)
        {
            string[] pick;
            if (ctx == SocialContext.SharedProblem || ctx == SocialContext.RecentFailure)
                pick = shared ?? work;
            else if (ctx == SocialContext.WorkingTogether || ctx == SocialContext.RecentSuccess || ctx == SocialContext.Emergency)
                pick = work ?? idle;
            else if (ctx == SocialContext.Camp)
                pick = camp ?? idle ?? work;
            else
                pick = idle ?? camp ?? work;

            if (pick == null) return;
            for (int i = 0; i < pick.Length; i++)
                pool.Add(pick[i]);
        }

        static void FilterRecent(List<string> pool, float gameHours)
        {
            if (pool == null || pool.Count <= 1) return;
            var hist = SocialDialogueHistory.Instance;
            for (int i = pool.Count - 1; i >= 0; i--)
            {
                if (!hist.IsLineFresh(pool[i], gameHours) && pool.Count > 2)
                    pool.RemoveAt(i);
            }
        }

        /// <summary>
        /// When relationship class is colored, prefer tone-flavored lines so friends ≠ grudges.
        /// </summary>
        static string PickWeighted(List<string> pool, int baseCount, in SocialDialogueTone tone)
        {
            if (pool == null || pool.Count == 0) return "…";
            bool colored = tone.RelClass != RelationshipClass.Neutral
                           || tone.HasMajorNegMemory
                           || tone.HasMajorPosMemory
                           || tone.FrustrationSpeaker >= 55f;
            if (colored && pool.Count > baseCount && Rng.NextDouble() < 0.78)
            {
                int flavorStart = Mathf.Clamp(baseCount, 0, pool.Count - 1);
                if (flavorStart < pool.Count)
                {
                    string line = pool[Rng.Next(flavorStart, pool.Count)];
                    SocialDialogueHistory.Instance.Remember(line, 0, SocialDialogueTopic.General, tone.GameHours);
                    return line;
                }
            }
            string pick = pool[Rng.Next(0, pool.Count)];
            SocialDialogueHistory.Instance.Remember(pick, 0, SocialDialogueTopic.General, tone.GameHours);
            return pick;
        }

        static string Pick(List<string> lines)
        {
            if (lines == null || lines.Count == 0) return "…";
            return lines[Rng.Next(0, lines.Count)];
        }

        static string Pick(string[] lines)
        {
            if (lines == null || lines.Length == 0) return "…";
            return lines[Rng.Next(0, lines.Length)];
        }
    }
}
