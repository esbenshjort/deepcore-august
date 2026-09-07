using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Stage 0 social graph: directional relations + transient pair pressure + encounter resolution.
    /// </summary>
    public sealed class SocialAuraWorld
    {
        readonly Dictionary<int, SocialSimActor> _actors = new();
        readonly Dictionary<long, SocialDirectedRelation> _directed = new(); // key: from<<32|to
        readonly Dictionary<long, SocialPairTransient> _pairs = new(); // unordered pair key
        readonly List<SocialEncounterLog> _logs = new(512);
        readonly Dictionary<int, int> _encountersThisShift = new();
        readonly SocialMemoryStore _memory = new();
        readonly RelationshipTrajectoryStore _trajectory = new();

        public IReadOnlyList<SocialEncounterLog> Logs => _logs;
        public int ShiftIndex { get; private set; }
        public SocialMemoryStore Memory => _memory;
        public RelationshipTrajectoryStore Trajectory => _trajectory;

        public void ClearLogs() => _logs.Clear();

        /// <summary>Drop directed edges so hired crews can re-seed cautious impressions.</summary>
        public void ClearDirectedRelations() => _directed.Clear();

        public void AddActor(SocialSimActor actor)
        {
            if (actor == null) return;
            _actors[actor.Id] = actor;
            actor.RefreshExpression();
        }

        public SocialSimActor Get(int id) =>
            _actors.TryGetValue(id, out var a) ? a : null;

        public IEnumerable<SocialSimActor> Actors => _actors.Values;

        public void BeginShift(int shiftIndex)
        {
            ShiftIndex = shiftIndex;
            _encountersThisShift.Clear();
            foreach (var a in _actors.Values)
                a.RefreshExpression();
        }

        static long DirKey(int from, int to) => ((long)from << 32) | (uint)to;

        static long PairKey(int a, int b)
        {
            if (a > b) { int t = a; a = b; b = t; }
            return ((long)a << 32) | (uint)b;
        }

        public SocialDirectedRelation Relation(int from, int to)
        {
            long k = DirKey(from, to);
            if (!_directed.TryGetValue(k, out var r))
            {
                r = SocialDirectedRelation.CreateNeutral();
                _directed[k] = r;
            }
            return r;
        }

        public SocialPairTransient Pair(int a, int b)
        {
            long k = PairKey(a, b);
            if (!_pairs.TryGetValue(k, out var p))
            {
                p = new SocialPairTransient();
                _pairs[k] = p;
            }
            return p;
        }

        /// <summary>
        /// Controlled exposure opportunity (Stage 0 — not live avatar distance).
        /// Returns encounter log if pressure crossed threshold.
        /// </summary>
        public SocialEncounterLog Expose(
            int idA, int idB, SocialContext context, float opportunity = 1f)
        {
            var a = Get(idA);
            var b = Get(idB);
            if (a == null || b == null || idA == idB) return null;

            a.RefreshExpression();
            b.RefreshExpression();

            var pair = Pair(idA, idB);
            if (pair.CooldownRemaining > 0f)
            {
                pair.CooldownRemaining = Mathf.Max(0f, pair.CooldownRemaining - SocialAuraTuning.ExposureTick);
                return null;
            }

            int encA = _encountersThisShift.TryGetValue(idA, out var ea) ? ea : 0;
            int encB = _encountersThisShift.TryGetValue(idB, out var eb) ? eb : 0;
            if (encA >= SocialAuraTuning.MaxEncountersPerWorkerPerShift
                || encB >= SocialAuraTuning.MaxEncountersPerWorkerPerShift)
                return null;

            float reachOverlap = Mathf.Min(a.Expression.Reach, b.Expression.Reach);
            float intensity = 0.5f * (a.Expression.Intensity + b.Expression.Intensity);
            float contextMul = SocialAuraTuning.ContextMul(context);

            // Relationship tendency: warmth helps contact; hostility increases pressure under SharedProblem;
            // Focus on either side softens social noise absorption of pressure
            var ab = Relation(idA, idB);
            var ba = Relation(idB, idA);
            float warmthAvg = 0.5f * (ab.Warmth + ba.Warmth);
            float hostAvg = 0.5f * (ab.Hostility + ba.Hostility);
            float focusAvg = 0.5f * (a.Stats.Get(WorkerStatId.Focus) + b.Stats.Get(WorkerStatId.Focus));
            float focusDamp = Mathf.Clamp(1.1f - focusAvg / 40f, 0.55f, 1.15f);

            float relationMul = 1f + warmthAvg * 0.015f + hostAvg * 0.012f;
            if (context == SocialContext.SharedProblem)
                relationMul += 0.18f + Mathf.Max(0f, hostAvg) * 0.01f;

            float gain = opportunity * SocialAuraTuning.ExposureTick
                         * intensity * reachOverlap * contextMul * relationMul * focusDamp;

            pair.InteractionPressure += gain;
            pair.ExposureThisShift += SocialAuraTuning.ExposureTick;

            string whyPressure =
                $"exposure={opportunity:0.##} intens={intensity:0.##} reach={reachOverlap:0.##} " +
                $"ctx={context}×{contextMul:0.##} relMul={relationMul:0.##} focusDamp={focusDamp:0.##} " +
                $"P={pair.InteractionPressure:0.##}/{SocialAuraTuning.PressureTrigger:0.##}";

            if (pair.InteractionPressure < SocialAuraTuning.PressureTrigger)
                return null;

            // Trigger encounter
            pair.InteractionPressure = 0f;
            pair.CooldownRemaining = SocialAuraTuning.CooldownAfterEncounter;
            pair.LastEncounterShift = ShiftIndex;

            var snapAB = RelationshipTrajectoryStore.Capture(Relation(idA, idB));
            var snapBA = RelationshipTrajectoryStore.Capture(Relation(idB, idA));

            var log = SocialEncounterResolver.Resolve(this, a, b, context, whyPressure);
            if (log != null)
            {
                _logs.Add(log);
                pair.PushHistory(log);
                _encountersThisShift[idA] = encA + 1;
                _encountersThisShift[idB] = encB + 1;
                float t = WorkerStateClock.GameHours > 0f
                    ? WorkerStateClock.GameHours
                    : ShiftIndex * 10f;
                SocialMemoryRecorder.Record(_memory, log, t);
                SocialRespectApplicator.Apply(this, log);
                string src = $"S{log.ShiftIndex}:{log.Action}/{log.Response}";
                _trajectory.CommitDelta(log.InitiatorId, log.TargetId,
                    log.InitiatorId == idA ? snapAB : snapBA,
                    Relation(log.InitiatorId, log.TargetId), t, src, _memory);
                _trajectory.CommitDelta(log.TargetId, log.InitiatorId,
                    log.InitiatorId == idA ? snapBA : snapAB,
                    Relation(log.TargetId, log.InitiatorId), t, src, _memory);
            }
            return log;
        }

        public void DecayUnexposedPairs()
        {
            foreach (var kv in _pairs)
            {
                var p = kv.Value;
                p.InteractionPressure = Mathf.Max(0f,
                    p.InteractionPressure - SocialAuraTuning.PressureDecayPerShiftUnit * 0.25f);
                p.CooldownRemaining = Mathf.Max(0f, p.CooldownRemaining - 0.15f);
                p.ExposureThisShift = 0f;
            }
        }
    }

    public static class SocialEncounterResolver
    {
        public static SocialEncounterLog Resolve(
            SocialAuraWorld world,
            SocialSimActor a,
            SocialSimActor b,
            SocialContext context,
            string whyPressure)
        {
            // Initiator
            float scoreA = InitiatorScore(a, b, world.Relation(a.Id, b.Id), context);
            float scoreB = InitiatorScore(b, a, world.Relation(b.Id, a.Id), context);
            bool aInit = scoreA >= scoreB;
            // tiny continuous noise only when nearly tied (not D20 personality)
            if (Mathf.Abs(scoreA - scoreB) < 0.08f)
                aInit = WorkerRoll.NextUnit() >= 0.5f;

            var initiator = aInit ? a : b;
            var target = aInit ? b : a;
            var relIT = world.Relation(initiator.Id, target.Id);
            var relTI = world.Relation(target.Id, initiator.Id);

            string whyInit =
                $"{initiator.Name} score={ (aInit ? scoreA : scoreB):0.##} vs {target.Name} " +
                $"{(aInit ? scoreB : scoreA):0.##} (Leadership/Bravery/Affinity/Intensity)";

            // Exchange 1
            RunExchange(
                initiator, target, relIT, relTI, context,
                out var action, out var response, out var actionOk, out var respOk,
                out var actionRoll, out var respRoll,
                out float dTrIT, out float dWaIT, out float dHoIT,
                out float dTrTI, out float dWaTI, out float dHoTI,
                out float dFrI, out float dMoI, out float dFrT, out float dMoT,
                out string whyAction, out string whyResp);

            int exchanges = 1;
            string outcome = Summarize(action, response, actionOk, respOk, context);
            var sbAction = new StringBuilder(whyAction);
            var sbResp = new StringBuilder(whyResp);

            // Optional exchanges 2–3 on escalation only
            while (exchanges < 3
                   && (response == SocialResponse.Escalate || response == SocialResponse.PushBack)
                   && WorkerRoll.NextUnit() < 0.55f)
            {
                // Target often becomes initiator of the next beat
                var nextInit = target;
                var nextTgt = initiator;
                var rNextIT = world.Relation(nextInit.Id, nextTgt.Id);
                var rNextTI = world.Relation(nextTgt.Id, nextInit.Id);

                RunExchange(
                    nextInit, nextTgt, rNextIT, rNextTI, context,
                    out action, out response, out actionOk, out respOk,
                    out actionRoll, out respRoll,
                    out float eTrIT, out float eWaIT, out float eHoIT,
                    out float eTrTI, out float eWaTI, out float eHoTI,
                    out float eFrI, out float eMoI, out float eFrT, out float eMoT,
                    out string wA, out string wR);

                // Accumulate from original initiator/target frame of reference where possible
                if (nextInit.Id == initiator.Id)
                {
                    dTrIT += eTrIT; dWaIT += eWaIT; dHoIT += eHoIT;
                    dTrTI += eTrTI; dWaTI += eWaTI; dHoTI += eHoTI;
                    dFrI += eFrI; dMoI += eMoI; dFrT += eFrT; dMoT += eMoT;
                }
                else
                {
                    dTrIT += eTrTI; dWaIT += eWaTI; dHoIT += eHoTI;
                    dTrTI += eTrIT; dWaTI += eWaIT; dHoTI += eHoIT;
                    dFrI += eFrT; dMoI += eMoT; dFrT += eFrI; dMoT += eMoI;
                }

                exchanges++;
                sbAction.Append($" | x{exchanges}: {wA}");
                sbResp.Append($" | x{exchanges}: {wR}");
                outcome = Summarize(action, response, actionOk, respOk, context) + $"/x{exchanges}";

                if (response == SocialResponse.Ignore
                    || response == SocialResponse.Withdraw
                    || response == SocialResponse.Agree
                    || response == SocialResponse.Accept)
                    break;
            }

            initiator.RefreshExpression();
            target.RefreshExpression();

            if (outcome != null
                && (outcome.StartsWith("POSITIVE_CONNECT") || outcome.StartsWith("SHARED_COMPLAINT_BOND")))
                EarlyCrewPressure.RegisterPositiveBond();

            return new SocialEncounterLog
            {
                ShiftIndex = world.ShiftIndex,
                TimeInShift = 0f,
                InitiatorId = initiator.Id,
                TargetId = target.Id,
                Context = context,
                Action = action,
                Response = response,
                ActionSuccess = actionOk,
                ResponseSuccess = respOk,
                ActionD20 = actionRoll.D20,
                ResponseD20 = respRoll.D20,
                DeltaTrustIT = dTrIT,
                DeltaWarmthIT = dWaIT,
                DeltaHostilityIT = dHoIT,
                DeltaTrustTI = dTrTI,
                DeltaWarmthTI = dWaTI,
                DeltaHostilityTI = dHoTI,
                DeltaFrustrationInit = dFrI,
                DeltaMoraleInit = dMoI,
                DeltaFrustrationTarget = dFrT,
                DeltaMoraleTarget = dMoT,
                WhyPressure = whyPressure,
                WhyInitiator = whyInit,
                WhyAction = sbAction.ToString(),
                WhyResponse = sbResp.ToString(),
                OutcomeSummary = outcome + (exchanges > 1 ? $" (exchanges={exchanges})" : ""),
            };
        }

        static void RunExchange(
            SocialSimActor initiator, SocialSimActor target,
            SocialDirectedRelation relIT, SocialDirectedRelation relTI,
            SocialContext context,
            out SocialAction action, out SocialResponse response,
            out bool actionOk, out bool respOk,
            out WorkerRollResult actionRoll, out WorkerRollResult respRoll,
            out float dTrIT, out float dWaIT, out float dHoIT,
            out float dTrTI, out float dWaTI, out float dHoTI,
            out float dFrI, out float dMoI, out float dFrT, out float dMoT,
            out string whyAction, out string whyResp)
        {
            var weights = new float[6];
            whyAction = BuildActionWeights(initiator, target, relIT, context, weights);
            action = (SocialAction)WeightedPick(weights);

            actionRoll = ResolveActionRoll(initiator, target, relIT, action, context);
            actionOk = actionRoll.Success;

            var respWeights = new float[7];
            whyResp = BuildResponseWeights(
                target, initiator, relTI, action, actionOk, context, respWeights);
            response = (SocialResponse)WeightedPick(respWeights);
            respRoll = ResolveResponseRoll(target, initiator, response, action, actionOk);
            respOk = respRoll.Success;

            ApplyConsequences(
                initiator, target, relIT, relTI,
                action, response, actionOk, respOk, context,
                out dTrIT, out dWaIT, out dHoIT,
                out dTrTI, out dWaTI, out dHoTI,
                out dFrI, out dMoI, out dFrT, out dMoT);

            whyAction += $" → {action} (d20={actionRoll.D20} vs DC {actionRoll.DC} {(actionOk ? "OK" : "FAIL")})";
            whyResp += $" → {response} (d20={respRoll.D20} vs DC {respRoll.DC} {(respOk ? "OK" : "FAIL")})";
        }

        static float InitiatorScore(
            SocialSimActor self, SocialSimActor other,
            SocialDirectedRelation towardOther, SocialContext ctx)
        {
            float lead = self.Stats.Get(WorkerStatId.Leadership) / 20f;
            float brave = self.Stats.Get(WorkerStatId.Bravery) / 20f;
            float aff = self.Stats.Get(WorkerStatId.Affinity) / 20f;
            float focus = self.Stats.Get(WorkerStatId.Focus) / 20f;

            float score = self.Expression.Intensity * 0.55f
                          + lead * 0.35f
                          + brave * 0.25f
                          + aff * 0.20f
                          + self.Expression.Positive * 0.15f
                          + self.Expression.Negative * 0.10f;

            // High Focus reduces urge to engage social noise
            score -= focus * 0.22f;

            // Warmth toward other encourages initiation; hostility can too under SharedProblem
            score += towardOther.Warmth * 0.012f;
            if (ctx == SocialContext.SharedProblem)
                score += Mathf.Max(0f, towardOther.Hostility) * 0.01f + 0.08f;

            // Fatigue reduces social availability
            score -= self.State.MentalFatigue / 220f;
            return score;
        }

        static string BuildActionWeights(
            SocialSimActor init, SocialSimActor target,
            SocialDirectedRelation rel, SocialContext ctx, float[] w)
        {
            // Encourage Joke Connect Complain Provoke Confront
            float pos = init.Expression.Positive;
            float neg = init.Expression.Negative;
            int composure = init.Stats.Get(WorkerStatId.Composure);
            int bravery = init.Stats.Get(WorkerStatId.Bravery);
            int affinity = init.Stats.Get(WorkerStatId.Affinity);
            int empathy = init.Stats.Get(WorkerStatId.Empathy);
            int leadership = init.Stats.Get(WorkerStatId.Leadership);
            int focus = init.Stats.Get(WorkerStatId.Focus);
            int tolerance = init.Stats.Get(WorkerStatId.Tolerance);
            int determination = init.Stats.Get(WorkerStatId.Determination);

            w[0] = 0.15f + pos * 0.9f + leadership / 25f + empathy / 30f; // Encourage
            w[1] = 0.12f + pos * 0.5f + affinity / 28f - init.State.Frustration / 200f; // Joke
            w[2] = 0.18f + pos * 0.7f + affinity / 22f + empathy / 28f + rel.Warmth * 0.02f; // Connect
            w[3] = 0.12f + neg * 0.85f + tolerance / 35f; // Complain
            w[4] = 0.05f + neg * 0.9f + bravery / 22f - composure / 30f + rel.Hostility * 0.03f; // Provoke
            w[5] = 0.05f + neg * 0.55f + bravery / 20f + determination / 28f - composure / 35f; // Confront

            if (ctx == SocialContext.SharedProblem)
            {
                w[3] *= 1.85f; // Complain bonding path
                w[4] *= 1.15f;
                w[0] *= 0.85f;
            }
            if (ctx == SocialContext.RecentSuccess)
            {
                w[0] *= 1.35f;
                w[1] *= 1.25f;
                w[2] *= 1.20f;
            }
            if (ctx == SocialContext.Emergency)
            {
                w[5] *= 1.4f;
                w[0] *= 1.2f;
            }

            // High Focus: prefer lower-engagement social (not zero)
            float focusMulEngage = Mathf.Clamp(1.15f - focus / 35f, 0.55f, 1.15f);
            w[4] *= focusMulEngage;
            w[5] *= focusMulEngage;

            // Soft floor
            for (int i = 0; i < w.Length; i++)
                w[i] = Mathf.Max(0.02f, w[i]);

            return $"weights Enc={w[0]:0.##} Joke={w[1]:0.##} Conn={w[2]:0.##} Comp={w[3]:0.##} Prov={w[4]:0.##} Conf={w[5]:0.##}";
        }

        static string BuildResponseWeights(
            SocialSimActor target, SocialSimActor init,
            SocialDirectedRelation towardInit, SocialAction action, bool actionOk,
            SocialContext ctx, float[] w)
        {
            // Accept Deflect Ignore Agree PushBack Escalate Withdraw
            int composure = target.Stats.Get(WorkerStatId.Composure);
            int tolerance = target.Stats.Get(WorkerStatId.Tolerance);
            int focus = target.Stats.Get(WorkerStatId.Focus);
            int bravery = target.Stats.Get(WorkerStatId.Bravery);
            int empathy = target.Stats.Get(WorkerStatId.Empathy);
            int determination = target.Stats.Get(WorkerStatId.Determination);
            float neg = target.Expression.Negative;
            float pos = target.Expression.Positive;

            w[0] = 0.2f + composure / 40f + (actionOk ? 0.15f : 0f) + pos * 0.2f; // Accept
            w[1] = 0.18f + focus / 35f + composure / 45f; // Deflect
            w[2] = 0.15f + focus / 28f + tolerance / 40f; // Ignore
            w[3] = 0.1f + empathy / 30f; // Agree
            w[4] = 0.08f + bravery / 35f + neg * 0.35f; // PushBack
            w[5] = 0.05f + bravery / 30f + determination / 40f + neg * 0.45f - composure / 40f; // Escalate
            w[6] = 0.12f + focus / 40f + (1f - bravery / 40f); // Withdraw

            // Early unfamiliar crew: misunderstandings / escalate slightly more likely;
            // Composure + Tolerance resist harder under pressure; Determination can refuse to back down.
            float early = EarlyCrewPressure.Instability01;
            if (early > 0.01f)
            {
                w[4] *= EarlyCrewPressure.EscalateWeightMul;
                w[5] *= EarlyCrewPressure.EscalateWeightMul;
                float composureMask = Mathf.Clamp(1.25f - composure / 22f - tolerance / 38f, 0.28f, 1.25f);
                w[5] *= composureMask;
                if (target.State != null && target.State.Frustration >= 40f)
                    w[5] *= 1f + determination * 0.01f * early;
                // Empathy softens escalate toward bonded actions
                w[5] *= Mathf.Clamp(1.1f - empathy / 45f, 0.7f, 1.1f);
            }

            switch (action)
            {
                case SocialAction.Encourage:
                case SocialAction.Joke:
                case SocialAction.Connect:
                    if (actionOk) { w[0] *= 1.4f; w[3] *= 1.2f; }
                    else { w[1] *= 1.3f; w[2] *= 1.2f; w[4] *= 1.15f; }
                    break;
                case SocialAction.Complain:
                    if (ctx == SocialContext.SharedProblem)
                    { w[3] *= 2.2f; w[0] *= 1.2f; w[5] *= 0.7f; } // bonding path
                    else
                    { w[3] *= 1.2f; w[1] *= 1.1f; w[4] *= 1.1f; }
                    break;
                case SocialAction.Provoke:
                case SocialAction.Confront:
                    w[4] *= 1.35f;
                    w[5] *= 1.25f;
                    w[2] *= 1.15f + focus / 50f; // Focus can ignore
                    w[0] *= 0.7f;
                    w[6] *= 1.1f;
                    // High composure/tolerance resist escalation
                    w[5] *= Mathf.Clamp(1.2f - composure / 28f - tolerance / 45f, 0.35f, 1.2f);
                    break;
            }

            if (towardInit.Hostility > 8f) { w[4] *= 1.3f; w[5] *= 1.25f; }
            if (towardInit.Warmth > 8f) { w[0] *= 1.25f; w[3] *= 1.2f; }

            for (int i = 0; i < w.Length; i++)
                w[i] = Mathf.Max(0.02f, w[i]);

            return $"resp Acc={w[0]:0.##} Def={w[1]:0.##} Ign={w[2]:0.##} Agr={w[3]:0.##} Push={w[4]:0.##} Esc={w[5]:0.##} Wdr={w[6]:0.##}";
        }

        static WorkerRollResult ResolveActionRoll(
            SocialSimActor init, SocialSimActor target,
            SocialDirectedRelation rel, SocialAction action, SocialContext ctx)
        {
            WorkerStatId stat;
            int dc;
            int mod = 0;
            switch (action)
            {
                case SocialAction.Encourage:
                    stat = WorkerStatId.Leadership;
                    dc = 11;
                    mod += (init.Stats.Get(WorkerStatId.Empathy) - 10) / 4;
                    mod -= Mathf.RoundToInt(target.State.Frustration / 40f);
                    break;
                case SocialAction.Joke:
                    stat = WorkerStatId.Affinity;
                    dc = 12;
                    mod += (init.Stats.Get(WorkerStatId.Intuition) - 10) / 5;
                    mod -= Mathf.RoundToInt(target.Expression.Negative * 3f);
                    break;
                case SocialAction.Connect:
                    stat = WorkerStatId.Affinity;
                    dc = 11;
                    mod += (init.Stats.Get(WorkerStatId.Empathy) - 10) / 4;
                    mod += Mathf.RoundToInt(rel.Warmth / 6f);
                    mod -= Mathf.RoundToInt(rel.Hostility / 5f);
                    break;
                case SocialAction.Complain:
                    stat = WorkerStatId.Empathy;
                    dc = ctx == SocialContext.SharedProblem ? 9 : 12;
                    mod += (init.Stats.Get(WorkerStatId.Intuition) - 10) / 5;
                    break;
                case SocialAction.Provoke:
                    stat = WorkerStatId.Intuition;
                    dc = 12;
                    mod += (init.Stats.Get(WorkerStatId.Bravery) - 10) / 5;
                    mod -= (target.Stats.Get(WorkerStatId.Composure) - 10) / 4;
                    break;
                default: // Confront
                    stat = WorkerStatId.Bravery;
                    dc = 13;
                    mod += (init.Stats.Get(WorkerStatId.Determination) - 10) / 5;
                    mod -= (target.Stats.Get(WorkerStatId.Tolerance) - 10) / 5;
                    break;
            }

            return WorkerRoll.Check(init.Stats, stat, dc, mod);
        }

        static WorkerRollResult ResolveResponseRoll(
            SocialSimActor target, SocialSimActor init,
            SocialResponse response, SocialAction action, bool actionOk)
        {
            WorkerStatId stat;
            int dc = 10;
            int mod = actionOk ? 1 : -1;
            switch (response)
            {
                case SocialResponse.Accept:
                case SocialResponse.Agree:
                    stat = WorkerStatId.Empathy;
                    dc = 10;
                    break;
                case SocialResponse.Deflect:
                case SocialResponse.Ignore:
                    stat = WorkerStatId.Focus;
                    dc = 11;
                    break;
                case SocialResponse.PushBack:
                    stat = WorkerStatId.Bravery;
                    dc = 11;
                    break;
                case SocialResponse.Escalate:
                    stat = WorkerStatId.Determination;
                    dc = 12;
                    mod -= (target.Stats.Get(WorkerStatId.Composure) - 10) / 5;
                    break;
                default: // Withdraw
                    stat = WorkerStatId.Focus;
                    dc = 10;
                    break;
            }
            return WorkerRoll.Check(target.Stats, stat, dc, mod);
        }

        static void ApplyConsequences(
            SocialSimActor init, SocialSimActor target,
            SocialDirectedRelation relIT, SocialDirectedRelation relTI,
            SocialAction action, SocialResponse response,
            bool actionOk, bool respOk, SocialContext ctx,
            out float dTrIT, out float dWaIT, out float dHoIT,
            out float dTrTI, out float dWaTI, out float dHoTI,
            out float dFrI, out float dMoI, out float dFrT, out float dMoT)
        {
            float trIT = 0f, waIT = 0f, hoIT = 0f;
            float trTI = 0f, waTI = 0f, hoTI = 0f;
            dFrI = dMoI = dFrT = dMoT = 0f;

            void AddIT(float t, float w, float h) { trIT += t; waIT += w; hoIT += h; }
            void AddTI(float t, float w, float h) { trTI += t; waTI += w; hoTI += h; }

            // Shared complaint bonding
            bool bondComplain = action == SocialAction.Complain
                                && (response == SocialResponse.Agree || response == SocialResponse.Accept)
                                && (actionOk || ctx == SocialContext.SharedProblem);

            if (bondComplain)
            {
                AddIT(0.6f, 1.4f, -0.4f);
                AddTI(0.5f, 1.3f, -0.3f);
                dFrI -= 4f;
                dFrT -= 3.5f;
                dMoI += 1.2f;
                dMoT += 1.0f;
            }
            else
            {
                switch (action)
                {
                    case SocialAction.Encourage:
                        if (actionOk && (response == SocialResponse.Accept || response == SocialResponse.Agree))
                        {
                            AddIT(0.8f, 0.9f, -0.2f);
                            AddTI(0.5f, 0.7f, 0f);
                            dFrT -= 2.5f;
                            dMoT += 1.5f;
                            dMoI += 0.6f;
                        }
                        else if (!actionOk)
                        {
                            AddIT(-0.3f, -0.5f, 0.2f);
                            dFrI += 1.5f;
                            dFrT += 1.0f; // felt patronizing
                        }
                        break;

                    case SocialAction.Joke:
                        if (actionOk && response != SocialResponse.PushBack && response != SocialResponse.Escalate)
                        {
                            AddIT(0.3f, 1.0f, -0.2f);
                            AddTI(0.2f, 0.8f, 0f);
                            dFrI -= 1.5f;
                            dFrT -= 1.0f;
                        }
                        else
                        {
                            AddIT(-0.2f, -0.6f, 0.4f);
                            dFrI += 2f;
                            dFrT += 1.2f;
                        }
                        break;

                    case SocialAction.Connect:
                        if (actionOk && (response == SocialResponse.Accept || response == SocialResponse.Agree))
                        {
                            AddIT(0.7f, 1.2f, -0.2f);
                            AddTI(0.6f, 1.0f, 0f);
                            dMoI += 0.8f;
                            dMoT += 0.8f;
                        }
                        else if (response == SocialResponse.Ignore || response == SocialResponse.Withdraw)
                        {
                            AddIT(-0.2f, -0.4f, 0.1f);
                            dFrI += 2.2f;
                        }
                        break;

                    case SocialAction.Complain:
                        if (response == SocialResponse.PushBack || response == SocialResponse.Escalate)
                        {
                            AddIT(-0.3f, -0.4f, 0.8f);
                            AddTI(-0.2f, -0.3f, 0.7f);
                            dFrI += 2f;
                            dFrT += 2.5f;
                        }
                        else if (!bondComplain)
                        {
                            dFrI -= 1.0f; // venting alone still helps a little
                        }
                        break;

                    case SocialAction.Provoke:
                    case SocialAction.Confront:
                        if (response == SocialResponse.Escalate || response == SocialResponse.PushBack)
                        {
                            AddIT(-0.6f, -0.8f, 1.6f);
                            AddTI(-0.5f, -0.7f, 1.5f);
                            dFrI += 3f;
                            dFrT += 3.5f;
                            dMoI -= 0.8f;
                            dMoT -= 1.0f;
                        }
                        else if (response == SocialResponse.Ignore || response == SocialResponse.Withdraw)
                        {
                            // Target relatively stable; initiator may heat up
                            AddTI(0f, 0f, 0.2f);
                            dFrI += 2.8f;
                            if (!actionOk) dFrI += 1f;
                        }
                        else if (response == SocialResponse.Accept || response == SocialResponse.Deflect)
                        {
                            AddIT(-0.2f, -0.2f, 0.5f);
                            dFrT += 1.5f;
                        }
                        break;
                }
            }

            // Cap single-encounter deltas (slow relationship change)
            dTrIT = Mathf.Clamp(trIT, -2.5f, 2.5f);
            dWaIT = Mathf.Clamp(waIT, -2.5f, 2.5f);
            dHoIT = Mathf.Clamp(hoIT, -2.5f, 2.5f);
            dTrTI = Mathf.Clamp(trTI, -2.5f, 2.5f);
            dWaTI = Mathf.Clamp(waTI, -2.5f, 2.5f);
            dHoTI = Mathf.Clamp(hoTI, -2.5f, 2.5f);

            // Early hired-crew uncertainty: slower Trust gains, sharper negatives, weaker recovery
            EarlyCrewPressure.ScaleEncounterDeltas(
                ref dTrIT, ref dWaIT, ref dHoIT,
                ref dTrTI, ref dWaTI, ref dHoTI,
                ref dFrI, ref dFrT,
                init, target);

            relIT.Add(dTrIT, dWaIT, dHoIT);
            relTI.Add(dTrTI, dWaTI, dHoTI);

            init.State.Frustration = Mathf.Clamp(init.State.Frustration + dFrI, 0f, 100f);
            init.State.Morale = Mathf.Clamp(init.State.Morale + dMoI, 0f, 100f);
            target.State.Frustration = Mathf.Clamp(target.State.Frustration + dFrT, 0f, 100f);
            target.State.Morale = Mathf.Clamp(target.State.Morale + dMoT, 0f, 100f);

            // Mild focus jitter from heated exchanges only
            if (response == SocialResponse.Escalate)
            {
                init.State.FocusState = Mathf.Clamp(init.State.FocusState - 1.5f, 0f, 100f);
                target.State.FocusState = Mathf.Clamp(target.State.FocusState - 1.5f, 0f, 100f);
            }
        }

        static string Summarize(
            SocialAction action, SocialResponse response, bool aOk, bool rOk, SocialContext ctx)
        {
            if (action == SocialAction.Complain
                && (response == SocialResponse.Agree || response == SocialResponse.Accept)
                && ctx == SocialContext.SharedProblem)
                return "SHARED_COMPLAINT_BOND";
            if ((action == SocialAction.Provoke || action == SocialAction.Confront)
                && (response == SocialResponse.Escalate || response == SocialResponse.PushBack))
                return "CLASH";
            if ((action == SocialAction.Provoke || action == SocialAction.Confront)
                && (response == SocialResponse.Ignore || response == SocialResponse.Withdraw))
                return "IGNORED_AGGRESSION";
            if ((action == SocialAction.Encourage || action == SocialAction.Connect || action == SocialAction.Joke)
                && aOk
                && (response == SocialResponse.Accept || response == SocialResponse.Agree))
                return "POSITIVE_CONNECT";
            if ((action == SocialAction.Encourage || action == SocialAction.Joke || action == SocialAction.Connect)
                && !aOk)
                return "POSITIVE_FAIL";
            return $"EXCHANGE_{action}_{response}";
        }

        static int WeightedPick(float[] w)
        {
            float sum = 0f;
            for (int i = 0; i < w.Length; i++) sum += w[i];
            if (sum <= 0f) return 0;
            // Continuous sample of Soul-built weights — D20 is reserved for outcome quality.
            float pick = WorkerRoll.NextUnit() * sum;
            float acc = 0f;
            for (int i = 0; i < w.Length; i++)
            {
                acc += w[i];
                if (pick <= acc) return i;
            }
            return w.Length - 1;
        }
    }
}
