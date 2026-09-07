using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// F5: person-authored speech. THE PERSON SPEAKS; THE JOB PROVIDES CONTEXT.
    /// Line banks are keyed by job context — speaker identity is always WorkerId.
    /// </summary>
    public sealed class WorkerBanter
    {
        /// <summary>
        /// Speech authorship label only — never drives physical demand, injury
        /// consequences, work restrictions, or job-stat interpretation.
        /// Formerly named Voice / role personality.
        /// </summary>
        public enum JobContext : byte
        {
            Prospecting = 0,
            Excavation = 1,
            Hauling = 2,
            Refining = 3,
            Engineering = 4,
            /// <summary>Camp / commute / no assignment — speech metadata only.</summary>
            Unassigned = 5,
            Steward = 6,
        }

        /// <summary>Frozen authorship at TrySay — survives later reassignment.</summary>
        public sealed class Speech
        {
            public int WorkerId;
            public string DisplayName = "";
            public JobContext Context;
            public string SourceEvent = "";
            public string Text = "";
            public float Until;
            public float AuthoredUnscaledTime;
            public float AuthoredGameHours;
            /// <summary>True when authored by Social Aura exchange (world bubble eligible).</summary>
            public bool IsSocial;
            /// <summary>Presentation valence from resolved social outcome (None for job banter).</summary>
            public SocialSpeechValence Valence;
        }

        readonly Dictionary<int, Speech> _activeByWorker = new();
        readonly Dictionary<int, float> _cooldownUntil = new();
        readonly System.Random _rng = new(9081);

        const float ShowSeconds = 3.6f;
        const float MinGap = 9f;
        const float SocialShowSeconds = 4.0f;
        const float SocialPrioritySeconds = 5.5f;

        float _socialPriorityUntil;

        /// <summary>True while a Social Aura exchange owns the speech channel.</summary>
        public bool SocialPriorityActive => Time.unscaledTime < _socialPriorityUntil;

        /// <summary>Most recent successful TrySay (any worker). DEV proof.</summary>
        public Speech LastSpeech { get; private set; }

        public Speech GetForWorker(int workerId)
        {
            if (workerId <= 0) return null;
            if (!_activeByWorker.TryGetValue(workerId, out var s) || s == null)
                return null;
            if (string.IsNullOrEmpty(s.Text)) return null;
            if (Time.unscaledTime > s.Until)
            {
                _activeByWorker.Remove(workerId);
                return null;
            }
            return s;
        }

        /// <summary>
        /// Author speech as a specific person. WorkerId is stamped now and never re-resolved.
        /// Ambient banter is suppressed while <see cref="SocialPriorityActive"/>.
        /// </summary>
        public bool TrySay(
            int workerId,
            string displayName,
            JobContext context,
            string sourceEvent,
            float gameHours,
            params string[] options)
        {
            if (SocialPriorityActive) return false;
            return TrySayInternal(
                workerId, displayName, context, sourceEvent, gameHours,
                ShowSeconds, MinGap, clearCooldown: false, isSocial: false, options);
        }

        /// <summary>
        /// Social Aura exchange speech — priority over ambient banter.
        /// Clears that worker's active line/cooldown so the resolved exchange is visible.
        /// </summary>
        public bool TrySaySocial(
            int workerId,
            string displayName,
            JobContext context,
            string sourceEvent,
            float gameHours,
            params string[] options) =>
            TrySaySocial(workerId, displayName, context, sourceEvent, gameHours,
                SocialSpeechValence.Neutral, options);

        /// <summary>
        /// Social Aura / conflict speech with presentation valence from the resolved outcome.
        /// </summary>
        public bool TrySaySocial(
            int workerId,
            string displayName,
            JobContext context,
            string sourceEvent,
            float gameHours,
            SocialSpeechValence valence,
            params string[] options)
        {
            if (workerId <= 0) return false;
            _activeByWorker.Remove(workerId);
            _cooldownUntil.Remove(workerId);
            _socialPriorityUntil = Time.unscaledTime + SocialPrioritySeconds;
            // Short gap so target can speak soon after initiator in a queued exchange
            return TrySayInternal(
                workerId, displayName, context, sourceEvent, gameHours,
                SocialShowSeconds, gapSeconds: 1.6f, clearCooldown: true, isSocial: true,
                valence, options);
        }

        bool TrySayInternal(
            int workerId,
            string displayName,
            JobContext context,
            string sourceEvent,
            float gameHours,
            float showSeconds,
            float gapSeconds,
            bool clearCooldown,
            bool isSocial,
            string[] options) =>
            TrySayInternal(workerId, displayName, context, sourceEvent, gameHours,
                showSeconds, gapSeconds, clearCooldown, isSocial, SocialSpeechValence.None, options);

        bool TrySayInternal(
            int workerId,
            string displayName,
            JobContext context,
            string sourceEvent,
            float gameHours,
            float showSeconds,
            float gapSeconds,
            bool clearCooldown,
            bool isSocial,
            SocialSpeechValence valence,
            string[] options)
        {
            if (workerId <= 0) return false;
            if (options == null || options.Length == 0) return false;

            float t = Time.unscaledTime;
            if (!clearCooldown)
            {
                if (_cooldownUntil.TryGetValue(workerId, out float cd) && t < cd)
                    return false;
                if (_activeByWorker.TryGetValue(workerId, out var cur)
                    && cur != null
                    && !string.IsNullOrEmpty(cur.Text)
                    && t < cur.Until)
                    return false;
            }

            string line = options[_rng.Next(0, options.Length)];
            var speech = new Speech
            {
                WorkerId = workerId,
                DisplayName = string.IsNullOrEmpty(displayName) ? $"Worker {workerId}" : displayName,
                Context = context,
                SourceEvent = sourceEvent ?? "",
                Text = line,
                Until = t + showSeconds,
                AuthoredUnscaledTime = t,
                AuthoredGameHours = gameHours,
                IsSocial = isSocial,
                Valence = isSocial
                    ? (valence == SocialSpeechValence.None ? SocialSpeechValence.Neutral : valence)
                    : SocialSpeechValence.None,
            };
            _activeByWorker[workerId] = speech;
            _cooldownUntil[workerId] = t + gapSeconds;
            LastSpeech = speech;
            return true;
        }

        public void Clear()
        {
            _activeByWorker.Clear();
            _cooldownUntil.Clear();
            LastSpeech = null;
            _socialPriorityUntil = 0f;
        }

        public static string ContextLabel(JobContext ctx) => ctx switch
        {
            JobContext.Prospecting => "Prospecting",
            JobContext.Excavation => "Excavation",
            JobContext.Hauling => "Hauling",
            JobContext.Refining => "Refining",
            JobContext.Engineering => "Engineering",
            JobContext.Unassigned => "Unassigned",
            JobContext.Steward => "Steward",
            _ => "?",
        };
    }
}
