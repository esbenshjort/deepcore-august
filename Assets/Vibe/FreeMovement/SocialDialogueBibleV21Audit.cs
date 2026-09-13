using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Dialogue Bible V2.1 offline audit — presentation catalog + 10 social-day sim.
    /// Does not alter Social Aura frequency/math.
    /// </summary>
    public static class SocialDialogueBibleV21Audit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[DIALOGUE BIBLE V2.1] Report: {path}");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var sb = new StringBuilder(16000);
            sb.AppendLine("# Deep Core Dialogue Bible V2.1 — Audit");
            sb.AppendLine();
            sb.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine($"Exchange families: {SocialDialogueExchanges.CatalogCount}");
            sb.AppendLine($"Utterance variants: {SocialDialogueExchanges.UtteranceVariantCount}");
            sb.AppendLine();

            SocialDialogueHistory.Instance.Clear();
            SocialPersonKnowledge.Instance.Clear();
            NicknameEvidenceStore.Instance.Clear();

            SocialPersonKnowledge.Instance.ObserveConfinementFear(2, 1, 85f, 10f);
            SocialPersonKnowledge.Instance.ObserveConfinementFear(1, 2, 82f, 10.5f);
            SocialPersonKnowledge.Instance.ObserveSteadyDig(1, 2, 12f);
            SocialPersonKnowledge.Instance.ObserveSteadyDig(2, 1, 12.5f);
            SocialPersonKnowledge.Instance.ObserveDiscovery(1, 2, 14f);
            SocialPersonKnowledge.Instance.ObservePhysicalWork(1, 2, 15f);
            SocialPersonKnowledge.Instance.ObservePhysicalWork(2, 1, 15.5f);
            SocialPersonKnowledge.Instance.ObserveMemory(new SocialMemoryEntry
            {
                Type = SocialMemoryType.WasTrapped,
                Strength = 0.85f,
                ObserverId = 2,
                TargetId = 1,
                GameTime = 8f,
                Significance = SocialMemorySignificance.Major,
            }, 8f);
            SocialPersonKnowledge.Instance.ObserveMemory(new SocialMemoryEntry
            {
                Type = SocialMemoryType.RescuedByWorker,
                Strength = 0.8f,
                ObserverId = 1,
                TargetId = 2,
                GameTime = 11f,
                Significance = SocialMemorySignificance.Major,
            }, 11f);
            SocialPersonKnowledge.Instance.ObserveMemory(new SocialMemoryEntry
            {
                Type = SocialMemoryType.SurvivedCollapse,
                Strength = 0.75f,
                ObserverId = 1,
                TargetId = 2,
                GameTime = 12f,
                Significance = SocialMemorySignificance.Major,
            }, 12f);

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
                RelationshipClass.Rivalry, RelationshipClass.Strained, RelationshipClass.Grudge,
            };

            var sitVariants = new[]
            {
                new SocialSituationFlags
                {
                    NarrowTunnel = true, Dark = true, HighConfinement = true, Deep = true,
                    Exhausted = true, LongShift = true,
                },
                new SocialSituationFlags
                {
                    Injured = true, Exhausted = true, BadFood = true, StomachTrouble = true,
                },
                new SocialSituationFlags
                {
                    RecentDiscovery = true, ManagerPressure = true, CollapseMemory = true,
                },
                new SocialSituationFlags
                {
                    Trapped = true, Dark = true, HighConfinement = true, Injured = true,
                },
                new SocialSituationFlags
                {
                    GoodFood = true, Deep = true, NarrowTunnel = false,
                },
            };

            var lineSet = new HashSet<string>();
            var topicHits = new Dictionary<SocialDialogueTopic, int>();
            var initCounts = new Dictionary<string, int>();
            var topicDayHits = new Dictionary<SocialDialogueTopic, int>();
            int exchanges = 0, paired = 0, exactRepeats = 0, topicRepeats = 0;
            int humour = 0, positiveish = 0, hostile = 0, severeCruel = 0;
            int silence = 0, callback = 0, weakUse = 0, strengthUse = 0;
            int incompatible = 0, impossibleContext = 0;
            string lastInit = "";
            SocialDialogueTopic lastTopic = SocialDialogueTopic.General;
            var samplePairs = new List<string>(120);
            var manualReview = new List<string>(110);

            float hours = 20f;
            for (int day = 0; day < 10; day++)
            {
                var rel = rels[day % rels.Length];
                var sit = sitVariants[day % sitVariants.Length];
                SocialDialogueHistory.Instance.Clear(); // per-day soft reset for variety
                topicDayHits.Clear();

                for (int n = 0; n < 36; n++)
                {
                    hours += 0.4f;
                    var action = actions[(day * 13 + n * 3) % actions.Length];
                    var resp = responses[(day * 7 + n * 5) % responses.Length];

                    SocialMemoryType memHint = SocialMemoryType.WasTrapped;
                    if (n % 5 == 1) memHint = SocialMemoryType.RescuedByWorker;
                    if (n % 5 == 2) memHint = SocialMemoryType.SurvivedCollapse;

                    var tone = new SocialDialogueTone
                    {
                        RelClass = rel,
                        FrustrationSpeaker = 15f + (n % 6) * 12f,
                        Situation = sit,
                        SpeakerId = day % 2 == 0 ? 1 : 2,
                        ListenerId = day % 2 == 0 ? 2 : 1,
                        GameHours = hours,
                        HasMemoryHint = true,
                        MemoryHint = memHint,
                    };

                    int sp = tone.SpeakerId;
                    int li = tone.ListenerId;

                    SocialDialogueExchanges.ClearActive();
                    string init = SocialDialogueExchanges.PickInitiator(
                        action, true, SocialContext.WorkingTogether, in tone, sit, sp, li, hours);
                    if (string.IsNullOrEmpty(init))
                    {
                        impossibleContext++;
                        init = SocialAuraLineBank.PickInitiator(action, true, SocialContext.WorkingTogether, in tone);
                    }
                    else paired++;

                    string reply = SocialDialogueExchanges.PickResponse(resp, action, true, in tone, hours);
                    if (string.IsNullOrEmpty(reply))
                        reply = SocialAuraLineBank.PickResponse(resp, action, true, SocialContext.WorkingTogether, in tone);

                    exchanges++;
                    if (init == lastInit) exactRepeats++;
                    lastInit = init ?? "";
                    if (!string.IsNullOrEmpty(init))
                    {
                        lineSet.Add(init);
                        if (!initCounts.ContainsKey(init)) initCounts[init] = 0;
                        initCounts[init]++;
                    }
                    if (!string.IsNullOrEmpty(reply)) lineSet.Add(reply);

                    var ex = SocialDialogueExchanges.ActiveExchange;
                    if (ex != null)
                    {
                        if (!topicHits.ContainsKey(ex.Topic)) topicHits[ex.Topic] = 0;
                        topicHits[ex.Topic]++;
                        if (!topicDayHits.ContainsKey(ex.Topic)) topicDayHits[ex.Topic] = 0;
                        topicDayHits[ex.Topic]++;
                        if (ex.Topic == lastTopic && ex.Topic != SocialDialogueTopic.General)
                            topicRepeats++;
                        lastTopic = ex.Topic;

                        if (ex.Topic == SocialDialogueTopic.Humour || ex.Action == SocialAction.Joke)
                            humour++;
                        if (ex.Topic == SocialDialogueTopic.Praise || ex.Action == SocialAction.Encourage
                            || (ex.RequiresBondOrFriend && ex.Action == SocialAction.Connect))
                            positiveish++;
                        if (ex.RequiresHostileRel) hostile++;
                        if (ex.RequiresHostileRel && ex.NeedsWeak != SocialWeakPointKind.None)
                            severeCruel++;
                        if (ex.Topic == SocialDialogueTopic.Callback) callback++;
                        if (ex.NeedsWeak != SocialWeakPointKind.None) weakUse++;
                        if (ex.NeedsStrength != SocialStrengthKind.None) strengthUse++;

                        if (ex.Replies != null && !string.IsNullOrEmpty(reply))
                        {
                            bool found = false;
                            for (int r = 0; r < ex.Replies.Length; r++)
                                if (ex.Replies[r].Text == reply) { found = true; break; }
                            if (!found) incompatible++;
                        }

                        if (IsSilence(reply)) silence++;
                    }
                    else if (IsSilence(reply)) silence++;

                    if (samplePairs.Count < 24 && ex != null)
                        samplePairs.Add($"D{day + 1} {rel} {action}/{resp} [{ex.Topic}]\n  A: {init}\n  B: {reply}");

                    if (manualReview.Count < 100 && ex != null && n % 3 == 0)
                        manualReview.Add($"[{rel}|{ex.Topic}] A: {init} / B: {reply}");
                }
            }

            float ExactRepeatRate = exchanges > 0 ? 100f * exactRepeats / exchanges : 0f;
            float TopicRepeatRate = exchanges > 0 ? 100f * topicRepeats / exchanges : 0f;

            sb.AppendLine("## Simulation (10 social days × 36 beats)");
            sb.AppendLine($"- RelClasses cycled: Friendly, Bonded, Professional, Rivalry, Strained, Grudge");
            sb.AppendLine($"- Beats sampled: {exchanges}");
            sb.AppendLine($"- V2 paired hits: {paired} ({100f * paired / Mathf.Max(1, exchanges):0.#}%)");
            sb.AppendLine($"- Unique line strings: {lineSet.Count}");
            sb.AppendLine($"- Exact initiator adjacent-repeat rate: {ExactRepeatRate:0.#}% ({exactRepeats})");
            sb.AppendLine($"- Repeated-topic adjacent rate: {TopicRepeatRate:0.#}% ({topicRepeats})");
            sb.AppendLine($"- Incompatible response count: {incompatible}");
            sb.AppendLine($"- Impossible-context / null-exchange fallbacks: {impossibleContext}");
            sb.AppendLine($"- Humour / joke-action rate: {100f * humour / Mathf.Max(1, exchanges):0.#}% ({humour})");
            sb.AppendLine($"- Positive/warm rate: {100f * positiveish / Mathf.Max(1, exchanges):0.#}% ({positiveish})");
            sb.AppendLine($"- Hostile-gated rate: {100f * hostile / Mathf.Max(1, exchanges):0.#}% ({hostile})");
            sb.AppendLine($"- Severe cruelty rate (hostile+weak): {100f * severeCruel / Mathf.Max(1, exchanges):0.#}% ({severeCruel})");
            sb.AppendLine($"- Silence / non-witty reply rate: {100f * silence / Mathf.Max(1, exchanges):0.#}% ({silence})");
            sb.AppendLine($"- Callback count: {callback}");
            sb.AppendLine($"- Strength recognition count: {strengthUse}");
            sb.AppendLine($"- Weakness callback count: {weakUse}");
            sb.AppendLine();
            sb.AppendLine("## Topic hits");
            foreach (var kv in topicHits)
                sb.AppendLine($"- {kv.Key}: {kv.Value}");
            sb.AppendLine();
            sb.AppendLine("## Sample paired exchanges");
            for (int i = 0; i < samplePairs.Count; i++)
            {
                sb.AppendLine(samplePairs[i]);
                sb.AppendLine();
            }
            sb.AppendLine("## Manual coherence sample (≤100)");
            for (int i = 0; i < manualReview.Count; i++)
                sb.AppendLine($"{i + 1}. {manualReview[i]}");
            sb.AppendLine();
            sb.AppendLine("## Architecture checks");
            sb.AppendLine($"- Catalog ≥ 150 families: {SocialDialogueExchanges.CatalogCount >= 150}");
            sb.AppendLine($"- Utterances ≥ 500: {SocialDialogueExchanges.UtteranceVariantCount >= 500}");
            sb.AppendLine($"- Paired rate ≥ 50%: {paired * 1f / Mathf.Max(1, exchanges) >= 0.5f}");
            sb.AppendLine($"- Exact adjacent repeat ≤ 8%: {ExactRepeatRate <= 8f}");
            sb.AppendLine($"- Incompatible responses = 0 preferred: {incompatible}");
            sb.AppendLine($"- Fear knowledge: {SocialPersonKnowledge.Instance.KnowsWeakPoint(2, 1, out _, out _)}");
            sb.AppendLine($"- Strength knowledge: {SocialPersonKnowledge.Instance.KnowsStrength(1, 2, out _, out _)}");
            sb.AppendLine($"- Nickname RunningJoke evidence > 0: {NicknameEvidenceStore.Instance.StrengthOf(1, NicknameEvidenceKind.RunningJoke) > 0f || NicknameEvidenceStore.Instance.StrengthOf(2, NicknameEvidenceKind.RunningJoke) > 0f}");
            sb.AppendLine($"- Nickname PhysicalReputation > 0: {NicknameEvidenceStore.Instance.StrengthOf(2, NicknameEvidenceKind.PhysicalReputation) > 0f}");
            sb.AppendLine();
            sb.AppendLine("Social Aura frequency/math: untouched.");
            sb.AppendLine("Final nicknames: evidence only (not generated).");

            string dir = outputDirectory;
            if (string.IsNullOrEmpty(dir))
                dir = Path.Combine(Application.dataPath, "..", "BenchmarkResults");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "deep_core_dialogue_bible_v21_audit_raw.md");
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        static bool IsSilence(string t)
        {
            if (string.IsNullOrEmpty(t)) return false;
            t = t.Trim();
            return t == "…" || t == "..." || t == "No." || t == "Mm." || t == "Mhm."
                   || t == "Fuck off." || t == "—" || t == "Right.";
        }
    }
}
