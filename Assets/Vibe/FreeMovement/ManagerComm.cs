using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Player → single worker talk actions (contextual).</summary>
    public enum ManagerTalkAction : byte
    {
        PushHarder = 0,
        KeepItUp = 1,
        EaseOff = 2,
        TakeABreak = 3,
        Praise = 4,
        Encourage = 5,
        CheckIn = 6,
        Criticize = 7,
    }

    /// <summary>Crew-wide broadcast — each worker evaluates independently.</summary>
    public enum ManagerCrewAction : byte
    {
        PushForTheDay = 0,
        SteadyDay = 1,
        TakeItEasy = 2,
        GoodWork = 3,
        TurnThisAround = 4,
    }

    public enum ManagerInterveneAction : byte
    {
        CalmDown = 0,
        BackToWork = 1,
        HearThemOut = 2,
        SideWithA = 3,
        SideWithB = 4,
        StayOut = 5,
        OrderStop = 6,
        BreakItUp = 7,
        CallHelp = 8,
    }

    /// <summary>Player-facing reaction label — never expose rolls.</summary>
    public enum ManagerReactionKind : byte
    {
        Motivated = 0,
        Accepted = 1,
        Annoyed = 2,
        Angered = 3,
        Reassured = 4,
        Resentful = 5,
        Refused = 6,
        Hollow = 7,
        Complied = 8,
        Ignored = 9,
    }

    public enum ManagerRelationLabel : byte
    {
        TrustsYou = 0,
        RespectsYou = 1,
        Uncertain = 2,
        FrustratedWithYou = 3,
        ResentsYou = 4,
        HostileTowardYou = 5,
    }

    /// <summary>Snapshot of why a worker might listen (or not).</summary>
    public sealed class ManagerCommContext
    {
        public WorkerRuntime Worker;
        public ManagerRelation Mgr;
        public float GameHours;
        public bool OnShift;
        public bool Asleep;
        public bool AtCamp;
        public bool Commuting;
        public float WorkedHoursToday;
        public float OvertimeHours;
        public int ConsecutiveOvertimeDays;
        public float SleepDeficit01;
        public bool MissionTight; // few days left / unfinished goals
        public bool MissionOk;
        public bool RecentSuccess;
        public bool RecentFailure;
        public float Performance01; // rough 0..1 from focus/morale/frustration
    }

    public sealed class ManagerCommResult
    {
        public int WorkerId;
        public string DisplayName;
        public ManagerReactionKind Reaction;
        public string ReactionLabel;
        public string Line;
        public SocialSpeechValence Valence;
        public bool AcceptedIntent; // true = they try to follow the ask
        public float DTrust;
        public float DRespect;
        public float DResentment;
        public float EventMagnitude; // for WorkerStateEvent
        public SocialMemoryType? MemoryType;
        public float MemoryStrength;
        public string WhyPlain; // for history UI, not shown as formula
        public bool SpamBlocked;
        public string SpamNote;
    }

    /// <summary>Anti-spam + repetition awareness for manager actions.</summary>
    public sealed class ManagerCommAntiSpam
    {
        public const float WorkerActionCooldownHours = 0.85f;
        public const float CrewActionCooldownHours = 1.4f;
        public const float InterveneCooldownHours = 0.55f;
        public const float RepeatWindowHours = 18f;

        readonly Dictionary<long, float> _nextAllowed = new(64);
        readonly Dictionary<long, List<float>> _history = new(64);

        static long Key(int workerId, int actionCode) =>
            ((long)workerId << 32) | (uint)actionCode;

        public bool CanDo(int workerId, int actionCode, float now, out string note)
        {
            note = null;
            long k = Key(workerId, actionCode);
            if (_nextAllowed.TryGetValue(k, out float until) && now < until)
            {
                note = "They're still digesting the last order.";
                return false;
            }
            return true;
        }

        public int CountRecent(int workerId, int actionCode, float now)
        {
            long k = Key(workerId, actionCode);
            if (!_history.TryGetValue(k, out var list) || list == null) return 0;
            int n = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (now - list[i] > RepeatWindowHours) break;
                n++;
            }
            return n;
        }

        public void Record(int workerId, int actionCode, float now, float cooldownHours)
        {
            long k = Key(workerId, actionCode);
            _nextAllowed[k] = now + Mathf.Max(0.2f, cooldownHours);
            if (!_history.TryGetValue(k, out var list) || list == null)
            {
                list = new List<float>(8);
                _history[k] = list;
            }
            list.Add(now);
            while (list.Count > 12) list.RemoveAt(0);
        }

        public void Clear()
        {
            _nextAllowed.Clear();
            _history.Clear();
        }
    }

    /// <summary>Short-lived accepted intents — not puppeteering.</summary>
    public sealed class ManagerIntentStore
    {
        public sealed class Flags
        {
            public float PushUntil;
            public float BreakUntil;
            public bool AcceptedPush;
            public bool AcceptedBreak;
        }

        readonly Dictionary<int, Flags> _byId = new(16);

        public Flags Get(int workerId)
        {
            if (!_byId.TryGetValue(workerId, out var f) || f == null)
            {
                f = new Flags();
                _byId[workerId] = f;
            }
            return f;
        }

        public void Tick(float now)
        {
            foreach (var kv in _byId)
            {
                var f = kv.Value;
                if (f == null) continue;
                if (now >= f.PushUntil) f.AcceptedPush = false;
                if (now >= f.BreakUntil) f.AcceptedBreak = false;
            }
        }
    }

    /// <summary>Plain-language manager relationship readout.</summary>
    public static class ManagerRelationUi
    {
        public static ManagerRelationLabel Classify(ManagerRelation r)
        {
            if (r == null) return ManagerRelationLabel.Uncertain;
            if (r.Resentment >= 70f || (r.Resentment >= 55f && r.Trust < 30f))
                return ManagerRelationLabel.HostileTowardYou;
            if (r.Resentment >= 40f) return ManagerRelationLabel.ResentsYou;
            if (r.Resentment >= 25f || (r.Trust < 40f && r.Respect < 40f))
                return ManagerRelationLabel.FrustratedWithYou;
            if (r.Trust >= 68f && r.Respect >= 55f && r.Resentment < 18f)
                return ManagerRelationLabel.TrustsYou;
            if (r.Respect >= 65f && r.Resentment < 22f)
                return ManagerRelationLabel.RespectsYou;
            return ManagerRelationLabel.Uncertain;
        }

        public static string LabelText(ManagerRelationLabel lab) => lab switch
        {
            ManagerRelationLabel.TrustsYou => "TRUSTS YOU",
            ManagerRelationLabel.RespectsYou => "RESPECTS YOU",
            ManagerRelationLabel.Uncertain => "UNCERTAIN",
            ManagerRelationLabel.FrustratedWithYou => "FRUSTRATED WITH YOU",
            ManagerRelationLabel.ResentsYou => "RESENTS YOU",
            ManagerRelationLabel.HostileTowardYou => "HOSTILE TOWARD YOU",
            _ => "UNCERTAIN",
        };

        public static string BuildWhyHover(
            ManagerRelation r,
            SocialMemoryStore mem,
            int workerId,
            WorkerDayLedger ledger)
        {
            var bits = new List<string>(4);
            if (ledger != null && ledger.ConsecutiveOvertimeDays >= 2)
                bits.Add("Repeated overtime");
            if (ledger != null && ledger.SleepDeficit01 >= 0.35f)
                bits.Add("Insufficient sleep under your schedule");
            if (mem != null)
            {
                var toward = mem.GetToward(workerId, ManagerRelationshipStore.ManagerId);
                for (int i = 0; i < toward.Count && bits.Count < 4; i++)
                {
                    string line = toward[i].Type switch
                    {
                        SocialMemoryType.ManagerGaveMeRecovery =>
                            "You gave them recovery after hardship",
                        SocialMemoryType.ManagerPraisedMe => "You praised their work",
                        SocialMemoryType.ManagerPushedMeTooHard => "You pushed them too hard",
                        SocialMemoryType.ManagerCriticizedMe => "You criticized them",
                        SocialMemoryType.ManagerSupportedMe => "You supported them under pressure",
                        SocialMemoryType.ManagerTookTheirSide =>
                            "You supported them during an argument",
                        SocialMemoryType.ManagerStoppedFight => "You stepped into a fight",
                        SocialMemoryType.ManagerIgnoredConflict =>
                            "You stayed out of a conflict",
                        _ => null,
                    };
                    if (!string.IsNullOrEmpty(line) && !bits.Contains(line))
                        bits.Add(line);
                }
            }
            if (r != null && r.Trust >= 65f && r.Resentment < 15f && bits.Count == 0)
                bits.Add("Steady management so far");
            if (bits.Count == 0) bits.Add("Still forming an opinion of you");
            return string.Join("\n", bits);
        }
    }

    /// <summary>
    /// Manager Communication V1 — intent only; workers decide how to feel / whether to comply.
    /// </summary>
    public static class ManagerCommSystem
    {
        public static string ReactionLabel(ManagerReactionKind k) => k switch
        {
            ManagerReactionKind.Motivated => "MOTIVATED",
            ManagerReactionKind.Accepted => "ACCEPTED",
            ManagerReactionKind.Annoyed => "ANNOYED",
            ManagerReactionKind.Angered => "ANGERED",
            ManagerReactionKind.Reassured => "REASSURED",
            ManagerReactionKind.Resentful => "RESENTFUL",
            ManagerReactionKind.Refused => "REFUSED",
            ManagerReactionKind.Hollow => "HOLLOW",
            ManagerReactionKind.Complied => "COMPLIED",
            ManagerReactionKind.Ignored => "IGNORED",
            _ => "ACCEPTED",
        };

        public static bool TalkActionAvailable(ManagerTalkAction a, ManagerCommContext ctx)
        {
            if (ctx?.Worker == null || !ctx.Worker.IsAlive || ctx.Asleep) return false;
            var st = ctx.Worker.State;
            switch (a)
            {
                case ManagerTalkAction.PushHarder:
                    return ctx.OnShift || ctx.Commuting;
                case ManagerTalkAction.KeepItUp:
                    return ctx.OnShift && st != null && st.FocusState >= 45f
                           && !st.ExhaustionLatched;
                case ManagerTalkAction.EaseOff:
                    return st != null && (st.MentalFatigue >= 45f || st.ExhaustionLatched
                                          || ctx.OvertimeHours >= 1f || st.Frustration >= 40f);
                case ManagerTalkAction.TakeABreak:
                    return st != null && (st.ExhaustionLatched || st.NeedsCare || st.Injury >= 35f
                                          || st.MentalFatigue >= 55f || ctx.OvertimeHours >= 2f
                                          || WorkerJobDemand.StaminaRatio(ctx.Worker) <= 0.35f);
                case ManagerTalkAction.Praise:
                    return true;
                case ManagerTalkAction.Encourage:
                    return st != null && (st.Morale <= 50f || st.Frustration >= 35f
                                          || ctx.RecentFailure);
                case ManagerTalkAction.CheckIn:
                    return st != null && (st.NeedsCare || st.Injury >= 25f
                                          || st.MentalFatigue >= 50f
                                          || (ctx.Mgr != null && ctx.Mgr.Resentment >= 20f)
                                          || ctx.SleepDeficit01 >= 0.3f);
                case ManagerTalkAction.Criticize:
                    return ctx.OnShift || ctx.RecentFailure || (st != null && st.FocusState < 40f);
                default:
                    return false;
            }
        }

        public static ManagerCommResult EvaluateTalk(
            ManagerTalkAction action,
            ManagerCommContext ctx,
            ManagerCommAntiSpam spam)
        {
            var wr = ctx?.Worker;
            if (wr == null || !wr.IsAlive)
                return Blocked(0, "?", "They're gone.");

            int code = (int)action;
            if (spam != null && !spam.CanDo(wr.WorkerId, code, ctx.GameHours, out string note))
            {
                var blocked = Blocked(wr.WorkerId, wr.DisplayName, note);
                blocked.SpamBlocked = true;
                blocked.SpamNote = note;
                return blocked;
            }

            int repeats = spam?.CountRecent(wr.WorkerId, code, ctx.GameHours) ?? 0;
            var result = EvaluateTalkCore(action, ctx, repeats);
            spam?.Record(wr.WorkerId, code, ctx.GameHours,
                ManagerCommAntiSpam.WorkerActionCooldownHours * (1f + repeats * 0.25f));
            return result;
        }

        public static ManagerCommResult EvaluateCrewMember(
            ManagerCrewAction action,
            ManagerCommContext ctx,
            ManagerCommAntiSpam spam,
            int crewActionCode)
        {
            var wr = ctx?.Worker;
            if (wr == null || !wr.IsAlive) return null;

            // Soft per-worker digest for same broadcast family
            int perWorkerCode = 1000 + (int)action;
            if (spam != null && !spam.CanDo(wr.WorkerId, perWorkerCode, ctx.GameHours, out _))
            {
                // Still evaluate but dampen — broadcast reaches them, reaction muted
            }

            int repeats = spam?.CountRecent(0, crewActionCode, ctx.GameHours) ?? 0;
            var talk = MapCrewToTalk(action);
            var result = EvaluateTalkCore(talk, ctx, repeats, crewFlavor: action);
            spam?.Record(wr.WorkerId, perWorkerCode, ctx.GameHours,
                ManagerCommAntiSpam.WorkerActionCooldownHours * 0.7f);
            return result;
        }

        static ManagerTalkAction MapCrewToTalk(ManagerCrewAction a) => a switch
        {
            ManagerCrewAction.PushForTheDay => ManagerTalkAction.PushHarder,
            ManagerCrewAction.SteadyDay => ManagerTalkAction.KeepItUp,
            ManagerCrewAction.TakeItEasy => ManagerTalkAction.EaseOff,
            ManagerCrewAction.GoodWork => ManagerTalkAction.Praise,
            ManagerCrewAction.TurnThisAround => ManagerTalkAction.Encourage,
            _ => ManagerTalkAction.KeepItUp,
        };

        public static ManagerTalkAction TalkFromCrew(ManagerCrewAction a) => MapCrewToTalk(a);

        static ManagerCommResult EvaluateTalkCore(
            ManagerTalkAction action,
            ManagerCommContext ctx,
            int repeats,
            ManagerCrewAction? crewFlavor = null)
        {
            var wr = ctx.Worker;
            var st = wr.State;
            var mgr = ctx.Mgr ?? new ManagerRelation();
            float recept = Receptivity01(wr, mgr);
            float determ = Norm(wr.Stats.Get(WorkerStatId.Determination));
            float composure = Norm(wr.Stats.Get(WorkerStatId.Composure));
            float tolerance = Norm(wr.Stats.Get(WorkerStatId.Tolerance));
            float empathy = Norm(wr.Stats.Get(WorkerStatId.Empathy));

            var r = new ManagerCommResult
            {
                WorkerId = wr.WorkerId,
                DisplayName = wr.DisplayName,
            };

            // Diminishing / irritation from repeats
            float spamTax = Mathf.Clamp01(repeats * 0.22f);

            switch (action)
            {
                case ManagerTalkAction.PushHarder:
                    EvalPush(r, ctx, recept, determ, composure, spamTax);
                    break;
                case ManagerTalkAction.KeepItUp:
                    EvalKeep(r, ctx, recept, spamTax);
                    break;
                case ManagerTalkAction.EaseOff:
                    EvalEase(r, ctx, recept, determ, spamTax);
                    break;
                case ManagerTalkAction.TakeABreak:
                    EvalBreak(r, ctx, recept, determ, spamTax);
                    break;
                case ManagerTalkAction.Praise:
                    EvalPraise(r, ctx, recept, spamTax);
                    break;
                case ManagerTalkAction.Encourage:
                    EvalEncourage(r, ctx, recept, composure, spamTax);
                    break;
                case ManagerTalkAction.CheckIn:
                    EvalCheckIn(r, ctx, recept, empathy, spamTax);
                    break;
                case ManagerTalkAction.Criticize:
                    EvalCriticize(r, ctx, recept, tolerance, composure, spamTax);
                    break;
            }

            if (crewFlavor == ManagerCrewAction.TurnThisAround && ctx.MissionTight && recept > 0.45f
                && r.Reaction == ManagerReactionKind.Annoyed)
            {
                r.Reaction = ManagerReactionKind.Accepted;
                r.AcceptedIntent = true;
                r.WhyPlain = "Mission pressure made the turnaround ask land";
            }

            r.ReactionLabel = ReactionLabel(r.Reaction);
            r.Valence = ValenceFor(r.Reaction);
            r.Line = ManagerCommLineBank.Pick(action, r.Reaction, wr, ctx, crewFlavor);
            ScaleDeltas(r, spamTax);
            return r;
        }

        static void EvalPush(ManagerCommResult r, ManagerCommContext ctx,
            float recept, float determ, float composure, float spamTax)
        {
            var st = ctx.Worker.State;
            bool brutal = ctx.OvertimeHours >= 2.5f || ctx.WorkedHoursToday >= 11f
                          || (st != null && (st.ExhaustionLatched || st.NeedsCare));
            bool criticalOk = ctx.MissionTight && recept >= 0.4f && !brutal;

            if (brutal && recept < 0.7f)
            {
                r.Reaction = spamTax > 0.4f || st.Frustration >= 55f
                    ? ManagerReactionKind.Angered
                    : ManagerReactionKind.Annoyed;
                r.AcceptedIntent = false;
                r.DResentment = 2.2f + spamTax * 3f + (1f - recept) * 2f;
                r.DTrust = -0.6f - spamTax;
                r.DRespect = -0.3f;
                r.EventMagnitude = 1.4f + spamTax;
                r.MemoryType = SocialMemoryType.ManagerPushedMeTooHard;
                r.MemoryStrength = 0.55f + spamTax * 0.2f;
                r.WhyPlain = "Pushed while exhausted / overtime";
                return;
            }

            if (spamTax >= 0.45f)
            {
                r.Reaction = ManagerReactionKind.Annoyed;
                r.AcceptedIntent = false;
                r.DResentment = 1.5f + spamTax * 2f;
                r.DRespect = -0.4f;
                r.EventMagnitude = 1.1f;
                r.MemoryType = SocialMemoryType.ManagerPushedMeTooHard;
                r.MemoryStrength = 0.45f;
                r.WhyPlain = "Push Harder is getting old";
                return;
            }

            float acceptChance = recept * 0.55f + determ * 0.25f + composure * 0.1f
                                 + (criticalOk ? 0.15f : 0f) - spamTax * 0.35f;
            if (acceptChance >= 0.42f)
            {
                r.Reaction = acceptChance >= 0.62f
                    ? ManagerReactionKind.Motivated
                    : ManagerReactionKind.Accepted;
                r.AcceptedIntent = true;
                r.DTrust = recept >= 0.55f ? 0.35f : 0.1f;
                r.DRespect = 0.25f;
                r.DResentment = criticalOk ? -0.15f : 0.4f + (1f - recept) * 0.8f;
                r.EventMagnitude = 0.7f;
                if (r.DResentment >= 1f)
                {
                    r.MemoryType = SocialMemoryType.ManagerPushedMeTooHard;
                    r.MemoryStrength = 0.35f;
                }
                r.WhyPlain = criticalOk ? "Accepted under mission pressure" : "Willing to dig deeper";
            }
            else
            {
                r.Reaction = ManagerReactionKind.Refused;
                r.AcceptedIntent = false;
                r.DResentment = 1.2f + (1f - recept) * 1.5f;
                r.DTrust = -0.4f;
                r.EventMagnitude = 1.0f;
                r.MemoryType = SocialMemoryType.ManagerPushedMeTooHard;
                r.MemoryStrength = 0.4f;
                r.WhyPlain = "Refused the push";
            }
        }

        static void EvalKeep(ManagerCommResult r, ManagerCommContext ctx, float recept, float spamTax)
        {
            if (spamTax > 0.5f)
            {
                r.Reaction = ManagerReactionKind.Hollow;
                r.AcceptedIntent = true;
                r.EventMagnitude = 0.2f;
                r.WhyPlain = "Keep-it-up is worn thin";
                return;
            }
            r.Reaction = recept >= 0.45f ? ManagerReactionKind.Accepted : ManagerReactionKind.Hollow;
            r.AcceptedIntent = true;
            r.DTrust = 0.15f * recept;
            r.DRespect = 0.1f;
            r.EventMagnitude = 0.45f;
            r.WhyPlain = "Steady acknowledgment";
        }

        static void EvalEase(ManagerCommResult r, ManagerCommContext ctx,
            float recept, float determ, float spamTax)
        {
            var st = ctx.Worker.State;
            bool needsEase = st != null && (st.ExhaustionLatched || st.MentalFatigue >= 50f
                                            || ctx.OvertimeHours >= 1.5f);
            if (ctx.MissionTight && determ >= 0.65f && !needsEase)
            {
                r.Reaction = ManagerReactionKind.Annoyed;
                r.AcceptedIntent = false;
                r.DRespect = -0.5f;
                r.DTrust = -0.2f;
                r.EventMagnitude = 0.6f;
                r.WhyPlain = "Driven worker disliked easing off mid-crisis";
                return;
            }
            r.Reaction = needsEase ? ManagerReactionKind.Reassured : ManagerReactionKind.Accepted;
            r.AcceptedIntent = true;
            r.DTrust = needsEase ? 0.8f : 0.25f;
            r.DResentment = needsEase ? -0.6f : 0f;
            r.EventMagnitude = 0.5f;
            if (needsEase)
            {
                r.MemoryType = SocialMemoryType.ManagerGaveMeRecovery;
                r.MemoryStrength = 0.5f;
            }
            r.WhyPlain = needsEase ? "Recovery after hard work" : "Took the ease-off";
        }

        static void EvalBreak(ManagerCommResult r, ManagerCommContext ctx,
            float recept, float determ, float spamTax)
        {
            var st = ctx.Worker.State;
            bool needs = st != null && (st.ExhaustionLatched || st.NeedsCare || st.Injury >= 40f
                                        || WorkerJobDemand.StaminaRatio(ctx.Worker) <= 0.3f);
            if (!needs && determ >= 0.7f && recept < 0.75f)
            {
                r.Reaction = ManagerReactionKind.Refused;
                r.AcceptedIntent = false;
                r.DTrust = 0.15f; // still notices care
                r.EventMagnitude = 0.3f;
                r.WhyPlain = "Driven — insists they're fine";
                return;
            }
            if (needs)
            {
                r.Reaction = ManagerReactionKind.Reassured;
                r.AcceptedIntent = true;
                r.DTrust = 1.1f + recept * 0.4f;
                r.DRespect = 0.35f;
                r.DResentment = -0.8f;
                r.EventMagnitude = 0.55f;
                r.MemoryType = SocialMemoryType.ManagerGaveMeRecovery;
                r.MemoryStrength = 0.6f;
                r.WhyPlain = "Break after brutal work builds trust";
            }
            else
            {
                r.Reaction = ManagerReactionKind.Accepted;
                r.AcceptedIntent = true;
                r.DTrust = 0.4f;
                r.EventMagnitude = 0.35f;
                r.WhyPlain = "Accepted a short break";
            }
        }

        static void EvalPraise(ManagerCommResult r, ManagerCommContext ctx, float recept, float spamTax)
        {
            float perf = ctx.Performance01;
            if (spamTax >= 0.4f)
            {
                r.Reaction = ManagerReactionKind.Hollow;
                r.AcceptedIntent = false;
                r.DRespect = -0.3f;
                r.EventMagnitude = 0.15f;
                r.WhyPlain = "Praise feels empty from overuse";
                return;
            }
            if (perf < 0.35f && ctx.RecentFailure)
            {
                r.Reaction = ManagerReactionKind.Hollow;
                r.AcceptedIntent = false;
                r.DTrust = -0.25f;
                r.DRespect = -0.2f;
                r.EventMagnitude = 0.25f;
                r.WhyPlain = "Praise after poor work felt hollow";
                return;
            }
            r.Reaction = perf >= 0.55f || ctx.RecentSuccess
                ? ManagerReactionKind.Motivated
                : ManagerReactionKind.Accepted;
            r.AcceptedIntent = true;
            r.DTrust = 0.45f * recept;
            r.DRespect = 0.3f;
            r.DResentment = -0.25f;
            r.EventMagnitude = 0.65f;
            r.MemoryType = SocialMemoryType.ManagerPraisedMe;
            r.MemoryStrength = 0.45f + (perf >= 0.55f ? 0.15f : 0f);
            r.WhyPlain = "Sincere praise";
        }

        static void EvalEncourage(ManagerCommResult r, ManagerCommContext ctx,
            float recept, float composure, float spamTax)
        {
            if (spamTax > 0.55f)
            {
                r.Reaction = ManagerReactionKind.Annoyed;
                r.AcceptedIntent = false;
                r.EventMagnitude = 0.4f;
                r.WhyPlain = "Encouragement looping";
                return;
            }
            r.Reaction = recept + composure * 0.2f >= 0.4f
                ? ManagerReactionKind.Reassured
                : ManagerReactionKind.Accepted;
            r.AcceptedIntent = true;
            r.DTrust = 0.35f;
            r.DResentment = -0.2f;
            r.EventMagnitude = 0.55f;
            r.MemoryType = SocialMemoryType.ManagerSupportedMe;
            r.MemoryStrength = 0.4f;
            r.WhyPlain = "Encouragement landed";
        }

        static void EvalCheckIn(ManagerCommResult r, ManagerCommContext ctx,
            float recept, float empathy, float spamTax)
        {
            r.Reaction = ManagerReactionKind.Reassured;
            r.AcceptedIntent = true;
            r.DTrust = 0.55f + empathy * 0.3f;
            r.DResentment = -0.35f;
            r.EventMagnitude = 0.4f;
            r.MemoryType = SocialMemoryType.ManagerSupportedMe;
            r.MemoryStrength = 0.4f;
            r.WhyPlain = "Checked in on their state";
        }

        static void EvalCriticize(ManagerCommResult r, ManagerCommContext ctx,
            float recept, float tolerance, float composure, float spamTax)
        {
            var st = ctx.Worker.State;
            bool vulnerable = st != null && (st.NeedsCare || st.ExhaustionLatched
                                             || st.Injury >= 45f || ctx.SleepDeficit01 >= 0.4f);
            bool deserved = ctx.RecentFailure || (st != null && st.FocusState < 35f);

            if (vulnerable)
            {
                r.Reaction = ManagerReactionKind.Resentful;
                r.AcceptedIntent = false;
                r.DResentment = 3f + spamTax * 2f;
                r.DTrust = -1.2f;
                r.DRespect = -0.8f;
                r.EventMagnitude = 1.6f;
                r.MemoryType = SocialMemoryType.ManagerCriticizedMe;
                r.MemoryStrength = 0.65f;
                r.WhyPlain = "Criticized while injured/exhausted";
                return;
            }

            if (spamTax >= 0.45f)
            {
                r.Reaction = ManagerReactionKind.Annoyed;
                r.AcceptedIntent = false;
                r.DResentment = 2f;
                r.DTrust = -0.6f;
                r.EventMagnitude = 1.1f;
                r.MemoryType = SocialMemoryType.ManagerCriticizedMe;
                r.MemoryStrength = 0.5f;
                r.WhyPlain = "Criticism is piling up";
                return;
            }

            float accept = recept * 0.4f + tolerance * 0.35f + composure * 0.2f
                           + (deserved ? 0.2f : -0.15f);
            if (accept >= 0.48f)
            {
                r.Reaction = ManagerReactionKind.Accepted;
                r.AcceptedIntent = true;
                r.DRespect = deserved ? 0.35f : -0.15f;
                r.DTrust = deserved ? 0.1f : -0.3f;
                r.DResentment = deserved ? 0.3f : 1.2f;
                r.EventMagnitude = 0.85f;
                r.MemoryType = SocialMemoryType.ManagerCriticizedMe;
                r.MemoryStrength = 0.4f;
                r.WhyPlain = deserved
                    ? "Disciplined worker accepted fair criticism"
                    : "Took the criticism";
            }
            else
            {
                r.Reaction = ManagerReactionKind.Angered;
                r.AcceptedIntent = false;
                r.DResentment = 2.4f;
                r.DTrust = -0.9f;
                r.DRespect = -0.5f;
                r.EventMagnitude = 1.3f;
                r.MemoryType = SocialMemoryType.ManagerCriticizedMe;
                r.MemoryStrength = 0.55f;
                r.WhyPlain = "Rejected the criticism";
            }
        }

        /// <summary>Manager intervention into an active argument/fight.</summary>
        public static ManagerInterveneOutcome EvaluateIntervene(
            ManagerInterveneAction action,
            SocialArgumentSession session,
            WorkerRuntime a,
            WorkerRuntime b,
            ManagerRelationshipStore managers,
            ManagerCommAntiSpam spam,
            float gameHours,
            bool isFight,
            System.Random rng = null)
        {
            var outcome = new ManagerInterveneOutcome
            {
                Action = action,
                Session = session,
            };
            if (session == null || !session.IsActive || a == null || b == null)
            {
                outcome.Failed = true;
                outcome.Note = "No active conflict.";
                return outcome;
            }

            int code = 2000 + (int)action;
            if (spam != null && !spam.CanDo(0, code, gameHours, out string note))
            {
                outcome.Failed = true;
                outcome.Note = note ?? "Already intervened.";
                outcome.SpamBlocked = true;
                return outcome;
            }

            var mgrA = managers?.Get(a.WorkerId);
            var mgrB = managers?.Get(b.WorkerId);
            float respectAvg = ((mgrA?.Respect ?? 50f) + (mgrB?.Respect ?? 50f)) * 0.5f;
            float resentAvg = ((mgrA?.Resentment ?? 8f) + (mgrB?.Resentment ?? 8f)) * 0.5f;
            float listen = Mathf.Clamp01((respectAvg - 25f) / 60f) * (1f - resentAvg / 140f);

            outcome.ResultA = new ManagerCommResult
            {
                WorkerId = a.WorkerId,
                DisplayName = a.DisplayName,
            };
            outcome.ResultB = new ManagerCommResult
            {
                WorkerId = b.WorkerId,
                DisplayName = b.DisplayName,
            };

            switch (action)
            {
                case ManagerInterveneAction.StayOut:
                    ApplyStayOut(outcome, a, b, mgrA, mgrB);
                    outcome.ResolveConflict = false;
                    break;

                case ManagerInterveneAction.SideWithA:
                case ManagerInterveneAction.SideWithB:
                    ApplySide(outcome, action == ManagerInterveneAction.SideWithA, a, b, mgrA, mgrB, listen);
                    outcome.ResolveConflict = listen >= 0.35f;
                    outcome.Success = outcome.ResolveConflict;
                    break;

                case ManagerInterveneAction.CalmDown:
                case ManagerInterveneAction.HearThemOut:
                case ManagerInterveneAction.BackToWork:
                case ManagerInterveneAction.OrderStop:
                case ManagerInterveneAction.BreakItUp:
                case ManagerInterveneAction.CallHelp:
                    float need = isFight
                        ? (action == ManagerInterveneAction.BreakItUp
                           || action == ManagerInterveneAction.OrderStop
                           || action == ManagerInterveneAction.CallHelp ? 0.32f : 0.55f)
                        : (action == ManagerInterveneAction.CalmDown
                           || action == ManagerInterveneAction.HearThemOut ? 0.28f : 0.4f);
                    // Deterministic-ish from listen + composure — no % shown to player
                    float roll = listen * 0.7f
                                 + (Norm(a.Stats.Get(WorkerStatId.Composure))
                                    + Norm(b.Stats.Get(WorkerStatId.Composure))) * 0.075f
                                 + (rng != null ? (float)rng.NextDouble() * 0.25f : 0.12f);
                    if (resentAvg >= 50f && isFight) roll *= 0.65f;
                    outcome.Success = roll >= need;
                    outcome.ResolveConflict = outcome.Success;
                    ApplyAuthority(outcome, mgrA, mgrB, outcome.Success, isFight, action);
                    break;
            }

            spam?.Record(0, code, gameHours, ManagerCommAntiSpam.InterveneCooldownHours);
            FinalizeInterveneSpeech(outcome, action, isFight);
            return outcome;
        }

        static void ApplyStayOut(ManagerInterveneOutcome o, WorkerRuntime a, WorkerRuntime b,
            ManagerRelation mgrA, ManagerRelation mgrB)
        {
            SetReaction(o.ResultA, ManagerReactionKind.Ignored, 0, 0, 0.4f,
                SocialMemoryType.ManagerIgnoredConflict, 0.35f, "Manager stayed out");
            SetReaction(o.ResultB, ManagerReactionKind.Ignored, 0, 0, 0.4f,
                SocialMemoryType.ManagerIgnoredConflict, 0.35f, "Manager stayed out");
            mgrA?.Add(0, -0.2f, 0.5f);
            mgrB?.Add(0, -0.2f, 0.5f);
            o.Success = true; // choice succeeded as inaction
            o.Note = "You stayed out of it.";
        }

        static void ApplySide(ManagerInterveneOutcome o, bool sideA,
            WorkerRuntime a, WorkerRuntime b,
            ManagerRelation mgrA, ManagerRelation mgrB, float listen)
        {
            var favored = sideA ? o.ResultA : o.ResultB;
            var other = sideA ? o.ResultB : o.ResultA;
            var mgrF = sideA ? mgrA : mgrB;
            var mgrO = sideA ? mgrB : mgrA;

            SetReaction(favored, ManagerReactionKind.Reassured, 1.4f, 0.6f, -0.5f,
                SocialMemoryType.ManagerTookTheirSide, 0.55f, "Manager took their side");
            SetReaction(other, ManagerReactionKind.Resentful, -1.2f, -0.8f, 2.8f,
                SocialMemoryType.ManagerTookTheirSide, 0.6f, "Manager sided against them");
            mgrF?.Add(1.4f, 0.6f, -0.5f);
            mgrO?.Add(-1.2f, -0.8f, 2.8f);
            o.Success = listen >= 0.35f;
            o.Note = o.Success
                ? (sideA ? $"Sided with {a.DisplayName}." : $"Sided with {b.DisplayName}.")
                : "They barely listen — sides were taken anyway.";
            // Worker-worker: slight relation hit for the other via SocialAura applied by caller
            o.SideFavorId = sideA ? a.WorkerId : b.WorkerId;
            o.SideAgainstId = sideA ? b.WorkerId : a.WorkerId;
        }

        static void ApplyAuthority(ManagerInterveneOutcome o,
            ManagerRelation mgrA, ManagerRelation mgrB,
            bool success, bool isFight, ManagerInterveneAction action)
        {
            if (success)
            {
                SetReaction(o.ResultA, ManagerReactionKind.Complied, 0.5f, 0.9f, -0.3f,
                    isFight ? SocialMemoryType.ManagerStoppedFight
                            : SocialMemoryType.ManagerSupportedMe,
                    0.5f, "Complied with manager");
                SetReaction(o.ResultB, ManagerReactionKind.Complied, 0.5f, 0.9f, -0.3f,
                    isFight ? SocialMemoryType.ManagerStoppedFight
                            : SocialMemoryType.ManagerSupportedMe,
                    0.5f, "Complied with manager");
                mgrA?.Add(0.5f, 0.9f, -0.3f);
                mgrB?.Add(0.5f, 0.9f, -0.3f);
                o.Note = isFight ? "They stopped." : "They cooled off.";
            }
            else
            {
                SetReaction(o.ResultA, ManagerReactionKind.Ignored, -0.4f, -0.7f, 1.2f,
                    SocialMemoryType.ManagerIgnoredConflict, 0.3f, "Ignored the order");
                SetReaction(o.ResultB, ManagerReactionKind.Ignored, -0.4f, -0.7f, 1.2f,
                    SocialMemoryType.ManagerIgnoredConflict, 0.3f, "Ignored the order");
                // Fix: ignored intervention shouldn't use IgnoredConflict memory as "you ignored"
                o.ResultA.MemoryType = null;
                o.ResultB.MemoryType = null;
                mgrA?.Add(-0.4f, -0.7f, 1.2f);
                mgrB?.Add(-0.4f, -0.7f, 1.2f);
                o.Note = "They ignored you.";
                o.Failed = true;
            }
        }

        static void SetReaction(ManagerCommResult r, ManagerReactionKind kind,
            float dT, float dR, float dRes, SocialMemoryType? mem, float str, string why)
        {
            r.Reaction = kind;
            r.ReactionLabel = ReactionLabel(kind);
            r.Valence = ValenceFor(kind);
            r.DTrust = dT;
            r.DRespect = dR;
            r.DResentment = dRes;
            r.MemoryType = mem;
            r.MemoryStrength = str;
            r.WhyPlain = why;
            r.EventMagnitude = kind == ManagerReactionKind.Complied ? 0.4f
                : kind == ManagerReactionKind.Resentful ? 1.2f : 0.6f;
        }

        static void FinalizeInterveneSpeech(ManagerInterveneOutcome o,
            ManagerInterveneAction action, bool isFight)
        {
            if (o.ResultA != null)
                o.ResultA.Line = ManagerCommLineBank.PickIntervene(action, o.ResultA.Reaction, isFight, true);
            if (o.ResultB != null)
                o.ResultB.Line = ManagerCommLineBank.PickIntervene(action, o.ResultB.Reaction, isFight, false);
        }

        public static float Receptivity01(WorkerRuntime wr, ManagerRelation mgr)
        {
            if (wr?.Stats == null) return 0.4f;
            float trust = mgr != null ? mgr.Trust / 100f : 0.55f;
            float respect = mgr != null ? mgr.Respect / 100f : 0.5f;
            float resent = mgr != null ? mgr.Resentment / 100f : 0.08f;
            float fr = wr.State != null ? wr.State.Frustration / 100f : 0f;
            float fatig = wr.State != null ? wr.State.MentalFatigue / 100f : 0f;
            float tol = Norm(wr.Stats.Get(WorkerStatId.Tolerance));
            float det = Norm(wr.Stats.Get(WorkerStatId.Determination));
            return Mathf.Clamp01(
                trust * 0.35f + respect * 0.35f + tol * 0.15f + det * 0.05f
                - resent * 0.45f - fr * 0.2f - fatig * 0.12f);
        }

        static float Norm(int s) => Mathf.Clamp01((s - 1) / 19f);
        static float Norm(float s) => Mathf.Clamp01((s - 1f) / 19f);

        static SocialSpeechValence ValenceFor(ManagerReactionKind k) => k switch
        {
            ManagerReactionKind.Motivated => SocialSpeechValence.Positive,
            ManagerReactionKind.Reassured => SocialSpeechValence.Positive,
            ManagerReactionKind.Complied => SocialSpeechValence.Positive,
            ManagerReactionKind.Accepted => SocialSpeechValence.Neutral,
            ManagerReactionKind.Hollow => SocialSpeechValence.Neutral,
            ManagerReactionKind.Annoyed => SocialSpeechValence.Negative,
            ManagerReactionKind.Refused => SocialSpeechValence.Negative,
            ManagerReactionKind.Ignored => SocialSpeechValence.Negative,
            ManagerReactionKind.Resentful => SocialSpeechValence.Severe,
            ManagerReactionKind.Angered => SocialSpeechValence.Severe,
            _ => SocialSpeechValence.Neutral,
        };

        static void ScaleDeltas(ManagerCommResult r, float spamTax)
        {
            // Keep effects modest
            r.DTrust = Mathf.Clamp(r.DTrust, -3f, 2.5f);
            r.DRespect = Mathf.Clamp(r.DRespect, -2.5f, 2f);
            r.DResentment = Mathf.Clamp(r.DResentment, -2f, 4f);
            if (spamTax > 0f && r.DTrust > 0f) r.DTrust *= (1f - spamTax * 0.5f);
        }

        static ManagerCommResult Blocked(int id, string name, string note) => new()
        {
            WorkerId = id,
            DisplayName = name,
            Reaction = ManagerReactionKind.Ignored,
            ReactionLabel = "IGNORED",
            Line = note,
            Valence = SocialSpeechValence.Neutral,
            SpamBlocked = true,
            SpamNote = note,
            WhyPlain = note,
        };

        public static void ApplyResult(
            ManagerCommResult result,
            ManagerRelationshipStore managers,
            SocialMemoryStore memory,
            ManagerIntentStore intents,
            float gameHours,
            ManagerTalkAction? talkAction = null)
        {
            if (result == null || result.WorkerId <= 0 || result.SpamBlocked) return;
            var mgr = managers?.Get(result.WorkerId);
            mgr?.Add(result.DTrust, result.DRespect, result.DResentment);

            if (result.EventMagnitude > 0.05f)
            {
                WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                    result.WorkerId,
                    WorkerStateEventType.ManagerCommunication,
                    result.EventMagnitude,
                    "ManagerComm/" + result.ReactionLabel,
                    relatedWorkerId: ManagerRelationshipStore.ManagerId));
            }

            if (result.MemoryType.HasValue && memory != null && result.MemoryStrength >= 0.35f)
            {
                memory.Add(new SocialMemoryEntry
                {
                    Type = result.MemoryType.Value,
                    Strength = result.MemoryStrength,
                    GameTime = gameHours,
                    Context = SocialContext.None,
                    ObserverId = result.WorkerId,
                    TargetId = ManagerRelationshipStore.ManagerId,
                    SourceRef = "ManagerComm/" + result.ReactionLabel,
                    Significance = result.MemoryStrength >= 0.6f
                        ? SocialMemorySignificance.Significant
                        : SocialMemorySignificance.Ordinary,
                });
            }

            if (intents != null && talkAction.HasValue && result.AcceptedIntent)
            {
                var f = intents.Get(result.WorkerId);
                if (talkAction == ManagerTalkAction.PushHarder
                    || talkAction == ManagerTalkAction.KeepItUp)
                {
                    f.AcceptedPush = true;
                    f.PushUntil = gameHours + 1.2f;
                }
                if (talkAction == ManagerTalkAction.TakeABreak
                    || talkAction == ManagerTalkAction.EaseOff)
                {
                    f.AcceptedBreak = true;
                    f.BreakUntil = gameHours + 1.5f;
                    var wr = WorkerRuntime.Find(result.WorkerId);
                    if (wr?.State != null && talkAction == ManagerTalkAction.TakeABreak)
                        wr.State.IsResting = true;
                }
            }
        }
    }

    public sealed class ManagerInterveneOutcome
    {
        public ManagerInterveneAction Action;
        public SocialArgumentSession Session;
        public ManagerCommResult ResultA;
        public ManagerCommResult ResultB;
        public bool Success;
        public bool Failed;
        public bool ResolveConflict;
        public bool SpamBlocked;
        public string Note;
        public int SideFavorId;
        public int SideAgainstId;
    }
}
