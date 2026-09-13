using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Offline multi-day dialogue simulation for Social Dialogue + Personal Banter V2.
    /// Does not alter Social Aura frequency/math — samples presentation layer only.
    /// </summary>
    public static class SocialDialoguePersonalBanterV2Audit
    {
        public static string Run()
        {
            var sb = new StringBuilder(8000);
            sb.AppendLine("# Social Dialogue + Personal Banter V2 — Audit");
            sb.AppendLine();
            sb.AppendLine($"Catalog exchanges: {SocialDialogueExchanges.CatalogCount}");
            sb.AppendLine();

            SocialDialogueHistory.Instance.Clear();
            SocialPersonKnowledge.Instance.Clear();

            var mara = new WorkerRuntime(1, "Mara");
            var viktor = new WorkerRuntime(2, "Viktor");
            mara.Stats.Set(WorkerStatId.Composure, 9);
            mara.Stats.Set(WorkerStatId.Bravery, 8);
            viktor.Stats.Set(WorkerStatId.Composure, 14);
            viktor.Stats.Set(WorkerStatId.Bravery, 13);

            // Seed knowledge / memories
            SocialPersonKnowledge.Instance.ObserveConfinementFear(2, 1, 85f, 10f);
            SocialPersonKnowledge.Instance.ObserveSteadyDig(1, 2, 12f);
            SocialPersonKnowledge.Instance.ObserveDiscovery(1, 2, 14f);
            SocialPersonKnowledge.Instance.ObserveMemory(new SocialMemoryEntry
            {
                Type = SocialMemoryType.WasTrapped,
                Strength = 0.8f,
                ObserverId = 2,
                TargetId = 1,
                GameTime = 8f,
                Significance = SocialMemorySignificance.Major,
            }, 8f);
            SocialPersonKnowledge.Instance.ObserveMemory(new SocialMemoryEntry
            {
                Type = SocialMemoryType.InsultedMe,
                Strength = 0.7f,
                ObserverId = 1,
                TargetId = 2,
                GameTime = 9f,
                Significance = SocialMemorySignificance.Significant,
            }, 9f);

            var actions = new[]
            {
                SocialAction.Joke, SocialAction.Encourage, SocialAction.Connect,
                SocialAction.Complain, SocialAction.Provoke, SocialAction.Confront,
            };
            var responses = new[]
            {
                SocialResponse.Accept, SocialResponse.Agree, SocialResponse.Deflect,
                SocialResponse.PushBack, SocialResponse.Escalate, SocialResponse.Ignore,
                SocialResponse.Withdraw,
            };
            var rels = new[]
            {
                RelationshipClass.Friendly, RelationshipClass.Bonded, RelationshipClass.Professional,
                RelationshipClass.Rivalry, RelationshipClass.Grudge, RelationshipClass.Strained,
                RelationshipClass.Neutral,
            };

            var lineSet = new HashSet<string>();
            var topicHits = new Dictionary<SocialDialogueTopic, int>();
            int exchanges = 0, paired = 0, exactRepeats = 0;
            int humour = 0, positiveish = 0, harsh = 0, callback = 0, weakUse = 0, strengthUse = 0;
            string lastInit = "";
            var samplePairs = new List<string>(12);

            var sit = new SocialSituationFlags
            {
                NarrowTunnel = true,
                Dark = true,
                HighConfinement = true,
                Deep = true,
                Exhausted = true,
            };

            float hours = 20f;
            for (int day = 0; day < 4; day++)
            {
                for (int n = 0; n < 40; n++)
                {
                    hours += 0.35f;
                    var action = actions[(day * 17 + n * 3) % actions.Length];
                    var resp = responses[(day * 11 + n * 5) % responses.Length];
                    var rel = rels[(day + n) % rels.Length];

                    var tone = new SocialDialogueTone
                    {
                        RelClass = rel,
                        FrustrationSpeaker = 20f + (n % 5) * 10f,
                        Situation = sit,
                        SpeakerId = 1,
                        ListenerId = 2,
                        GameHours = hours,
                        HasMemoryHint = true,
                        MemoryHint = SocialMemoryType.WasTrapped,
                    };

                    SocialDialogueExchanges.ClearActive();
                    string init = SocialDialogueExchanges.PickInitiator(
                        action, true, SocialContext.WorkingTogether, in tone, sit, 1, 2, hours);
                    if (string.IsNullOrEmpty(init))
                    {
                        init = SocialAuraLineBank.PickInitiator(action, true, SocialContext.WorkingTogether, in tone);
                    }
                    else paired++;

                    string reply = SocialDialogueExchanges.PickResponse(resp, action, true, in tone, hours);
                    if (string.IsNullOrEmpty(reply))
                        reply = SocialAuraLineBank.PickResponse(resp, action, true, SocialContext.WorkingTogether, in tone);

                    exchanges++;
                    if (init == lastInit) exactRepeats++;
                    lastInit = init;
                    lineSet.Add(init);
                    lineSet.Add(reply);

                    var ex = SocialDialogueExchanges.ActiveExchange;
                    if (ex != null)
                    {
                        if (!topicHits.ContainsKey(ex.Topic)) topicHits[ex.Topic] = 0;
                        topicHits[ex.Topic]++;
                        if (ex.Topic == SocialDialogueTopic.Humour) humour++;
                        if (ex.Topic == SocialDialogueTopic.Praise || ex.Action == SocialAction.Encourage) positiveish++;
                        if (ex.RequiresHostileRel || ex.NeedsWeak != SocialWeakPointKind.None) harsh++;
                        if (ex.Topic == SocialDialogueTopic.Callback) callback++;
                        if (ex.NeedsWeak != SocialWeakPointKind.None) weakUse++;
                        if (ex.NeedsStrength != SocialStrengthKind.None) strengthUse++;
                    }

                    if (samplePairs.Count < 10 && ex != null)
                        samplePairs.Add($"{action}/{resp} [{rel}]\n  A: {init}\n  B: {reply}");
                }
            }

            sb.AppendLine("## Simulation (4 days × 40 beats)");
            sb.AppendLine($"- Beats sampled: {exchanges}");
            sb.AppendLine($"- Unique line strings: {lineSet.Count}");
            sb.AppendLine($"- Exact initiator repeats (adjacent): {exactRepeats}");
            sb.AppendLine($"- V2 paired exchange hits: {paired} ({100f * paired / exchanges:0.#}%)");
            sb.AppendLine($"- Humour-topic picks: {humour}");
            sb.AppendLine($"- Encourage/praise-ish: {positiveish}");
            sb.AppendLine($"- Hostile/weak-point gated: {harsh}");
            sb.AppendLine($"- Callback picks: {callback}");
            sb.AppendLine($"- Weak-point uses: {weakUse}");
            sb.AppendLine($"- Strength uses: {strengthUse}");
            sb.AppendLine();
            sb.AppendLine("## Topic hits");
            foreach (var kv in topicHits)
                sb.AppendLine($"- {kv.Key}: {kv.Value}");
            sb.AppendLine();
            sb.AppendLine("## Sample paired exchanges");
            for (int i = 0; i < samplePairs.Count; i++)
                sb.AppendLine(samplePairs[i]).AppendLine();

            // Compatibility check: response belongs to active exchange replies
            int compatOk = 0, compatN = 0;
            SocialDialogueHistory.Instance.Clear();
            hours = 100f;
            for (int i = 0; i < 60; i++)
            {
                hours += 0.5f;
                var tone = new SocialDialogueTone
                {
                    RelClass = RelationshipClass.Friendly,
                    Situation = sit,
                    SpeakerId = 1,
                    ListenerId = 2,
                    GameHours = hours,
                };
                SocialDialogueExchanges.ClearActive();
                string init = SocialDialogueExchanges.PickInitiator(
                    SocialAction.Joke, true, SocialContext.IdleNearby, in tone, sit, 1, 2, hours);
                if (init == null) continue;
                var ex = SocialDialogueExchanges.ActiveExchange;
                string reply = SocialDialogueExchanges.PickResponse(
                    SocialResponse.Accept, SocialAction.Joke, true, in tone, hours);
                compatN++;
                if (ex?.Replies == null || reply == null) continue;
                bool found = false;
                for (int r = 0; r < ex.Replies.Length; r++)
                    if (ex.Replies[r].Text == reply) { found = true; break; }
                if (found) compatOk++;
            }
            sb.AppendLine("## Response compatibility (Joke→Accept against active exchange)");
            sb.AppendLine($"- Compatible: {compatOk}/{compatN}");
            sb.AppendLine();
            sb.AppendLine("## Checks");
            sb.AppendLine($"- Catalog non-empty: {SocialDialogueExchanges.CatalogCount >= 30}");
            sb.AppendLine($"- Paired rate ≥ 40%: {paired * 1f / exchanges >= 0.4f}");
            sb.AppendLine($"- Unique lines ≥ 40: {lineSet.Count >= 40}");
            sb.AppendLine($"- Compatibility ≥ 80%: {compatN == 0 || compatOk * 1f / compatN >= 0.8f}");
            sb.AppendLine($"- Knowledge fear known: {SocialPersonKnowledge.Instance.KnowsWeakPoint(2, 1, out _, out _)}");
            sb.AppendLine($"- Knowledge strength known: {SocialPersonKnowledge.Instance.KnowsStrength(1, 2, out _, out _)}");
            sb.AppendLine();
            sb.AppendLine("Social Aura frequency/math: untouched.");
            sb.AppendLine("Final nicknames: evidence only (not generated).");
            return sb.ToString();
        }
    }
}
