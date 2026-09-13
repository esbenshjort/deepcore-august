using System;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Comic reaction lines for Manager Communication V1 — no formula text.</summary>
    public static class ManagerCommLineBank
    {
        static readonly string[] PushMotivated =
        {
            "Yeah. Keep it moving.",
            "You got it.",
            "On it.",
            "Let's finish this.",
        };
        static readonly string[] PushAccepted =
        {
            "Understood.",
            "I'll dig in.",
            "Copy that.",
        };
        static readonly string[] PushReluctant =
        {
            "…Fine. Same route. Don't ask again.",
            "I'll do it. Doesn't mean I like it.",
            "Alright. Deeper. Then I'm done.",
            "Against my better judgment.",
        };
        static readonly string[] PushAnnoyed =
        {
            "I've been working eleven hours.",
            "You've got to be kidding me.",
            "They've been saying this all week.",
            "Push harder? With what?",
        };
        static readonly string[] PushAngered =
        {
            "No.",
            "I'm done being yelled at.",
            "Find someone else to break.",
            "Not deeper. Not for you.",
        };
        static readonly string[] PushRefused =
        {
            "I'm fine — but I'm not accelerating.",
            "Not today.",
            "I hear you. Still no.",
            "Not deeper. Not like this.",
        };
        static readonly string[] PushPanicked =
        {
            "I can't — I can't stay down here!",
            "Get me out. Now.",
            "Walls are closing. I'm going.",
        };

        static readonly string[] KeepOk =
        {
            "Steady as she goes.",
            "Keeping pace.",
            "Got it.",
        };
        static readonly string[] KeepHollow =
        {
            "…Sure.",
            "If you say so.",
        };

        static readonly string[] EaseReassured =
        {
            "Thanks. I needed that.",
            "Appreciated.",
            "I'll ease off.",
        };
        static readonly string[] EaseAnnoyed =
        {
            "Ease off? We're bleeding days.",
            "Not while the clock's eating us.",
        };

        static readonly string[] BreakReassured =
        {
            "…Alright. Short break.",
            "I'll sit a minute.",
            "Thanks for noticing.",
        };
        static readonly string[] BreakRefused =
        {
            "I'm fine.",
            "Don't need it.",
            "I'll rest when we're clear.",
        };

        static readonly string[] PraiseMotivated =
        {
            "Means something, coming from you.",
            "Thanks. Won't waste it.",
            "Heard.",
        };
        static readonly string[] PraiseHollow =
        {
            "Uh-huh.",
            "Praise for that? Really?",
            "If you say so.",
        };

        static readonly string[] EncourageOk =
        {
            "Yeah. We can still turn this.",
            "Alright. One more push.",
            "Hearing you.",
        };

        static readonly string[] CheckInOk =
        {
            "…Thanks for asking.",
            "I'm holding. Barely.",
            "Noted. I'll say if it gets worse.",
        };

        static readonly string[] CritAccepted =
        {
            "Fair. I'll tighten up.",
            "Understood. Won't happen again.",
            "You're not wrong.",
        };
        static readonly string[] CritResentful =
        {
            "Criticize me while I'm like this?",
            "That's rich.",
            "Unbelievable.",
        };
        static readonly string[] CritAngered =
        {
            "Don't talk to me like that.",
            "Back off.",
        };

        public static string Pick(
            ManagerTalkAction action,
            ManagerReactionKind reaction,
            WorkerRuntime wr,
            ManagerCommContext ctx,
            ManagerCrewAction? crew = null)
        {
            // Light name flavor — optional overtime line for Viktor-like cases
            if (action == ManagerTalkAction.PushHarder
                && reaction == ManagerReactionKind.Annoyed
                && ctx != null && ctx.WorkedHoursToday >= 10f)
                return PickArr(PushAnnoyed);

            return (action, reaction) switch
            {
                (ManagerTalkAction.PushHarder, ManagerReactionKind.Motivated) => PickArr(PushMotivated),
                (ManagerTalkAction.PushHarder, ManagerReactionKind.Accepted) => PickArr(PushAccepted),
                (ManagerTalkAction.PushHarder, ManagerReactionKind.ReluctantlyAccepted) =>
                    PickArr(PushReluctant),
                (ManagerTalkAction.PushHarder, ManagerReactionKind.Annoyed) => PickArr(PushAnnoyed),
                (ManagerTalkAction.PushHarder, ManagerReactionKind.Angered) => PickArr(PushAngered),
                (ManagerTalkAction.PushHarder, ManagerReactionKind.Refused) => PickArr(PushRefused),
                (ManagerTalkAction.PushHarder, ManagerReactionKind.Panicked) => PickArr(PushPanicked),

                (ManagerTalkAction.KeepItUp, ManagerReactionKind.Hollow) => PickArr(KeepHollow),
                (ManagerTalkAction.KeepItUp, _) => PickArr(KeepOk),

                (ManagerTalkAction.EaseOff, ManagerReactionKind.Annoyed) => PickArr(EaseAnnoyed),
                (ManagerTalkAction.EaseOff, _) => PickArr(EaseReassured),

                (ManagerTalkAction.TakeABreak, ManagerReactionKind.Refused) => PickArr(BreakRefused),
                (ManagerTalkAction.TakeABreak, _) => PickArr(BreakReassured),

                (ManagerTalkAction.Praise, ManagerReactionKind.Hollow) => PickArr(PraiseHollow),
                (ManagerTalkAction.Praise, _) => PickArr(PraiseMotivated),

                (ManagerTalkAction.Encourage, _) => PickArr(EncourageOk),
                (ManagerTalkAction.CheckIn, _) => PickArr(CheckInOk),

                (ManagerTalkAction.Criticize, ManagerReactionKind.Accepted) => PickArr(CritAccepted),
                (ManagerTalkAction.Criticize, ManagerReactionKind.Resentful) => PickArr(CritResentful),
                (ManagerTalkAction.Criticize, ManagerReactionKind.Angered) => PickArr(CritAngered),
                (ManagerTalkAction.Criticize, _) => PickArr(CritAccepted),

                _ => "…",
            };
        }

        public static string PickIntervene(
            ManagerInterveneAction action,
            ManagerReactionKind reaction,
            bool isFight,
            bool isA)
        {
            if (action == ManagerInterveneAction.StayOut)
                return "…Fine. Handle it yourselves.";

            if (reaction == ManagerReactionKind.Complied)
                return isFight ? "Alright. We're done." : "…Okay. Dropping it.";
            if (reaction == ManagerReactionKind.Reassured)
                return isA ? "Appreciate that." : "Finally.";
            if (reaction == ManagerReactionKind.Resentful)
                return "Of course you'd take their side.";
            if (reaction == ManagerReactionKind.Ignored)
                return isFight ? "Not listening." : "Stay out of this.";
            return "…";
        }

        static string PickArr(string[] arr)
        {
            if (arr == null || arr.Length == 0) return "…";
            int i = Mathf.FloorToInt(WorkerRoll.NextUnit() * arr.Length);
            if (i >= arr.Length) i = arr.Length - 1;
            return arr[i];
        }
    }
}
