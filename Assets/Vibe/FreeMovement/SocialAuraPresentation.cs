using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Stage 2: minimal authored line bank for resolved Social Aura exchanges.
    /// Wording only — does not change Stage 0/1 outcomes.
    /// </summary>
    public static class SocialAuraLineBank
    {
        static readonly System.Random Rng = new(4217);

        public static string PickInitiator(SocialAction action, bool actionOk, SocialContext ctx)
        {
            string[] lines = action switch
            {
                SocialAction.Encourage => actionOk
                    ? PickCtx(ctx,
                        shared: new[] { "Hey — we get through this. Keep your head.", "Steady. You're still the one who can fix it." },
                        work: new[] { "Nice work. Keep that pace.", "You're doing fine. Don't second-guess it." },
                        idle: new[] { "You look rough. Hang in there.", "We've got this shift. One step." })
                    : new[] {
                        "Come on, you're better than this.",
                        "Chin up. You're slowing us down."
                    },
                SocialAction.Joke => actionOk
                    ? PickCtx(ctx,
                        shared: new[] { "If this vein hates us, at least it's consistent.", "Worst day underground? Give it an hour." },
                        work: new[] { "Machine's moodier than Viktor before coffee.", "If rock could sue, it'd name us." },
                        idle: new[] { "Camp food's a hate crime. Pass it.", "I dreamt the washer thanked me. Bad sign." })
                    : new[] {
                        "Cheer up — could be worse. …Alright, maybe not.",
                        "Lighten up. Or don't. Your face already did."
                    },
                SocialAction.Connect => actionOk
                    ? new[] {
                        "You alright? No performance. Just asking.",
                        "Talk to me a second. What's weighing on you?"
                    }
                    : new[] {
                        "We should talk. Properly.",
                        "Don't shut me out — not today."
                    },
                SocialAction.Complain => ctx == SocialContext.SharedProblem
                    ? new[] {
                        "This whole setup's fighting us. You feel it too?",
                        "Same headache, same dead end. I'm sick of it."
                      }
                    : new[] {
                        "I am done with this nonsense.",
                        "Everything's jammed and nobody's listening."
                      },
                SocialAction.Provoke => new[] {
                    "That your best? Really?",
                    "Maybe the problem's standing right there."
                },
                SocialAction.Confront => new[] {
                    "We're settling this. Now.",
                    "Say it straight — or step aside."
                },
                _ => new[] { "…" },
            };
            return Pick(lines);
        }

        public static string PickResponse(
            SocialResponse response,
            SocialAction action,
            bool actionOk,
            SocialContext ctx)
        {
            // Failed positive actions should feel rejected, not warm
            if (!actionOk
                && IsPositive(action)
                && (response == SocialResponse.Accept || response == SocialResponse.Agree))
            {
                return Pick(new[] { "…Sure. Whatever you say.", "Mm. Fine." });
            }

            string[] lines = response switch
            {
                SocialResponse.Accept => action == SocialAction.Encourage
                    ? new[] { "Yeah. Thanks. Needed that.", "Alright. I'll hold the line." }
                    : new[] { "Fair enough.", "Okay. I hear you." },
                SocialResponse.Agree => action == SocialAction.Complain && ctx == SocialContext.SharedProblem
                    ? new[] {
                        "Yeah. Same mess. At least we're not alone in it.",
                        "Exactly. We'll chew through it together."
                      }
                    : new[] { "You're not wrong.", "Same here." },
                SocialResponse.Deflect => !actionOk && IsPositive(action)
                    ? new[] {
                        "Don't manage me.",
                        "Save the pep talk."
                      }
                    : new[] { "Not now.", "Later. Focus." },
                SocialResponse.Ignore => action == SocialAction.Provoke || action == SocialAction.Confront
                    ? new[] { "…", "Not worth it." }
                    : new[] { "Busy.", "Mhm." },
                SocialResponse.PushBack => !actionOk && IsPositive(action)
                    ? new[] {
                        "That landed wrong.",
                        "Don't talk to me like that."
                      }
                    : new[] {
                        "Back off.",
                        "Watch your mouth."
                      },
                SocialResponse.Escalate => new[] {
                    "You want a problem? You've got one.",
                    "Say that again."
                },
                SocialResponse.Withdraw => !actionOk && IsPositive(action)
                    ? new[] {
                        "I'm done with this chat.",
                        "Leave me alone."
                      }
                    : new[] {
                        "Not doing this.",
                        "I'm walking."
                      },
                _ => new[] { "…" },
            };
            return Pick(lines);
        }

        /// <summary>Optional third beat (initiator reaction) — only for clear bond/clash/fail.</summary>
        public static string PickOptionalCloser(SocialEncounterLog log)
        {
            if (log == null || string.IsNullOrEmpty(log.OutcomeSummary)) return null;
            string o = log.OutcomeSummary;
            if (o.Contains("SHARED_COMPLAINT_BOND"))
                return Pick(new[] { "Good. Then we dig out of it.", "Alright. Pair up. Move." });
            if (o.Contains("CLASH"))
                return Pick(new[] { "Fine. Remember that.", "We're not finished." });
            if (o.Contains("POSITIVE_FAIL"))
                return Pick(new[] { "…Right. Noted.", "Okay. Message received." });
            if (o.Contains("IGNORED_AGGRESSION"))
                return Pick(new[] { "Coward.", "Whatever." });
            return null;
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

        static string[] PickCtx(SocialContext ctx, string[] shared, string[] work, string[] idle)
        {
            if (ctx == SocialContext.SharedProblem || ctx == SocialContext.RecentFailure)
                return shared ?? work;
            if (ctx == SocialContext.WorkingTogether || ctx == SocialContext.RecentSuccess || ctx == SocialContext.Emergency)
                return work ?? idle;
            return idle ?? work;
        }

        static string Pick(string[] lines)
        {
            if (lines == null || lines.Length == 0) return "…";
            return lines[Rng.Next(0, lines.Length)];
        }
    }

    /// <summary>
    /// Stage 2 presentation: queue short WorkerId-authored lines from a resolved SocialEncounterLog.
    /// Does not alter social math — presentation/suppression only.
    /// </summary>
    public sealed class SocialAuraPresenter
    {
        public const float PresentDistanceMax = 4.8f;
        public const float LineGapSeconds = 1.85f;

        public struct PendingLine
        {
            public int WorkerId;
            public string DisplayName;
            public JobType JobContext;
            public string Text;
            public float FireAtUnscaled;
            public string Role; // initiator / response / closer
        }

        public struct PresentationRecord
        {
            public SocialEncounterLog Log;
            public bool Presented;
            public bool SuppressedDistance;
            public bool SuppressedSleep;
            public string InitiatorLine;
            public string ResponseLine;
            public string CloserLine;
            public int LinesQueued;
        }

        readonly List<PendingLine> _queue = new(4);
        PresentationRecord _last;

        public PresentationRecord LastPresentation => _last;
        public int QueuedCount => _queue.Count;

        public void ClearQueue() => _queue.Clear();

        /// <summary>
        /// Build 1–3 line exchange. Returns false if presentation suppressed (result still stands).
        /// </summary>
        public bool TryEnqueue(
            SocialEncounterLog log,
            WorkerRuntime init,
            WorkerRuntime target,
            Vector2 initPos,
            Vector2 targetPos,
            SocialPresenceKind initPresence,
            SocialPresenceKind targetPresence,
            JobType initJob,
            JobType targetJob,
            float nowUnscaled)
        {
            _last = default;
            _last.Log = log;
            if (log == null || init == null || target == null)
                return false;

            if (initPresence == SocialPresenceKind.Sleeping
                || targetPresence == SocialPresenceKind.Sleeping
                || !SocialAuraEligibility.IsEligible(init, initPresence)
                || !SocialAuraEligibility.IsEligible(target, targetPresence))
            {
                _last.SuppressedSleep = true;
                return false;
            }

            float dist = Vector2.Distance(initPos, targetPos);
            if (dist > PresentDistanceMax)
            {
                _last.SuppressedDistance = true;
                return false;
            }

            string initLine = SocialAuraLineBank.PickInitiator(log.Action, log.ActionSuccess, log.Context);
            string respLine = SocialAuraLineBank.PickResponse(log.Response, log.Action, log.ActionSuccess, log.Context);
            string closer = SocialAuraLineBank.PickOptionalCloser(log);

            _queue.Clear();
            _queue.Add(new PendingLine
            {
                WorkerId = log.InitiatorId,
                DisplayName = init.DisplayName,
                JobContext = initJob,
                Text = initLine,
                FireAtUnscaled = nowUnscaled,
                Role = "initiator",
            });
            _queue.Add(new PendingLine
            {
                WorkerId = log.TargetId,
                DisplayName = target.DisplayName,
                JobContext = targetJob,
                Text = respLine,
                FireAtUnscaled = nowUnscaled + LineGapSeconds,
                Role = "response",
            });
            if (!string.IsNullOrEmpty(closer))
            {
                _queue.Add(new PendingLine
                {
                    WorkerId = log.InitiatorId,
                    DisplayName = init.DisplayName,
                    JobContext = initJob,
                    Text = closer,
                    FireAtUnscaled = nowUnscaled + LineGapSeconds * 2f,
                    Role = "closer",
                });
            }

            _last.Presented = true;
            _last.InitiatorLine = initLine;
            _last.ResponseLine = respLine;
            _last.CloserLine = closer;
            _last.LinesQueued = _queue.Count;
            return true;
        }

        /// <summary>Fire due lines through social-priority banter.</summary>
        public int Tick(float nowUnscaled, Func<PendingLine, bool> saySocial)
        {
            if (saySocial == null) return 0;
            int fired = 0;
            for (int i = 0; i < _queue.Count;)
            {
                var line = _queue[i];
                if (nowUnscaled < line.FireAtUnscaled)
                {
                    i++;
                    continue;
                }
                if (saySocial(line))
                    fired++;
                _queue.RemoveAt(i);
            }
            return fired;
        }

        /// <summary>Build a synthetic log for presentation/line-bank audits (no Stage 0 resolve).</summary>
        public static SocialEncounterLog MakeSyntheticLog(
            int initiatorId,
            int targetId,
            SocialContext ctx,
            SocialAction action,
            SocialResponse response,
            bool actionOk,
            bool responseOk,
            string outcome)
        {
            return new SocialEncounterLog
            {
                InitiatorId = initiatorId,
                TargetId = targetId,
                Context = ctx,
                Action = action,
                Response = response,
                ActionSuccess = actionOk,
                ResponseSuccess = responseOk,
                ActionD20 = actionOk ? 14 : 4,
                ResponseD20 = responseOk ? 12 : 6,
                OutcomeSummary = outcome ?? "",
                DeltaFrustrationInit = 0f,
                DeltaMoraleInit = actionOk && SocialAuraLineBank.IsPositive(action) ? 1f : 0f,
                DeltaFrustrationTarget = !actionOk && SocialAuraLineBank.IsPositive(action) ? 2f : 0f,
                DeltaMoraleTarget = actionOk && action == SocialAction.Encourage ? 3f : 0f,
                DeltaTrustIT = actionOk ? 1f : -0.5f,
                DeltaWarmthIT = actionOk ? 1f : -1f,
                DeltaHostilityIT = actionOk ? 0f : 1f,
            };
        }
    }
}
