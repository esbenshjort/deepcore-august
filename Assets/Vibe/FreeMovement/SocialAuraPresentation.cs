using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
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
            /// <summary>Presentation valence from resolved encounter (result, not intent).</summary>
            public SocialSpeechValence Valence;
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
        /// Optional tones color wording only — no simulation impact.
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
            float nowUnscaled,
            SocialDialogueTone initiatorTone = default,
            SocialDialogueTone responseTone = default)
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

            string initLine = SocialAuraLineBank.PickInitiator(
                log.Action, log.ActionSuccess, log.Context, in initiatorTone);
            string respLine = SocialAuraLineBank.PickResponse(
                log.Response, log.Action, log.ActionSuccess, log.Context, in responseTone);
            string closer = SocialAuraLineBank.PickOptionalCloser(log, in initiatorTone);

            _queue.Clear();
            _queue.Add(new PendingLine
            {
                WorkerId = log.InitiatorId,
                DisplayName = init.DisplayName,
                JobContext = initJob,
                Text = initLine,
                FireAtUnscaled = nowUnscaled,
                Role = "initiator",
                Valence = SocialSpeechVisuals.FromAuraLine(log, "initiator"),
            });
            _queue.Add(new PendingLine
            {
                WorkerId = log.TargetId,
                DisplayName = target.DisplayName,
                JobContext = targetJob,
                Text = respLine,
                FireAtUnscaled = nowUnscaled + LineGapSeconds,
                Role = "response",
                Valence = SocialSpeechVisuals.FromAuraLine(log, "response"),
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
                    Valence = SocialSpeechVisuals.FromAuraLine(log, "closer"),
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
