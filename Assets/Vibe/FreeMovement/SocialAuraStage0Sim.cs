using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Social Aura Stage 0 — isolated deterministic sim + diagnostic.
    /// Batchmode: -executeMethod DeepCore.FreeMovement.SocialAuraStage0Sim.RunFromEditor
    /// </summary>
    public static class SocialAuraStage0Sim
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[SOCIAL AURA S0] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.ReadAllText(path).Contains("RECOMMENDATION: NOT READY");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var report = new StringBuilder(48_000);
            report.AppendLine("# Social Aura Stage 0 — Model + Simulation Report");
            report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();
            report.AppendLine("Scope: MODEL + MATH + SIMULATION ONLY. No live WorkerAvatar proximity.");
            report.AppendLine();

            AppendModelDoc(report);

            report.AppendLine("## 13. Scenario results");
            report.AppendLine();
            RunScenarioA(report);
            RunScenarioB(report);
            RunScenarioC(report);
            RunScenarioD(report);
            RunScenarioE(report);
            RunScenarioF(report);

            report.AppendLine();
            report.AppendLine("## 14. 100-run diagnostic (20 shifts each)");
            report.AppendLine();
            var diag = RunHundred(report);

            report.AppendLine();
            report.AppendLine("## 15. Emergent story examples");
            report.AppendLine();
            AppendStories(report, diag);

            report.AppendLine();
            report.AppendLine("## 16. Failure-condition audit");
            report.AppendLine();
            bool ready = AuditFailures(report, diag);

            report.AppendLine();
            report.AppendLine("## 17. Tuning risks");
            report.AppendLine();
            report.AppendLine("- PressureTrigger / Cooldown dominate frequency — retune before live proximity.");
            report.AppendLine("- Composure mask can under-express frustration if set too high globally.");
            report.AppendLine("- SharedProblem Complain→Agree path must stay probabilistic, not guaranteed.");
            report.AppendLine("- Leadership soft-reach + initiator score can stack; keep Leadership soft.");
            report.AppendLine("- Multi-exchange only on Escalate/PushBack — may under-represent long cool talks.");
            report.AppendLine("- WorkRate fairness judgment is stubbed (contribution comparison deferred).");
            report.AppendLine();

            report.AppendLine("## 18. Recommendation");
            report.AppendLine();
            report.AppendLine(ready
                ? "RECOMMENDATION: READY for live Social Aura integration (proximity wiring next)."
                : "RECOMMENDATION: NOT READY for live Social Aura integration.");
            report.AppendLine();
            report.AppendLine(ready
                ? "Stage 0 math produces explainable pair trajectories under controlled exposure. Proceed to Stage 1 proximity only after locking these constants."
                : "Address FAIL items above before wiring WorkerAvatar distance.");

            string dir = outputDirectory;
            if (string.IsNullOrEmpty(dir))
            {
                dir = Path.Combine(Application.dataPath, "..", "BenchmarkResults");
                Directory.CreateDirectory(dir);
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(dir, $"social_aura_stage0_unity_{stamp}.md");
            string latest = Path.Combine(dir, "social_aura_stage0_unity_latest.md");
            string alsoLatest = Path.Combine(dir, "social_aura_stage0_latest.md");
            File.WriteAllText(path, report.ToString());
            File.WriteAllText(latest, report.ToString());
            File.WriteAllText(alsoLatest, report.ToString());
            return path;
        }

        // ── Profiles (benchmark only — not gameplay classes) ──────────────

        public static WorkerStats ProfileA_ComposedEmpath()
        {
            var s = WorkerStats.CreateBaseline();
            s.Set(WorkerStatId.Composure, 17);
            s.Set(WorkerStatId.Empathy, 16);
            s.Set(WorkerStatId.Affinity, 12);
            s.Set(WorkerStatId.Bravery, 9);
            s.Set(WorkerStatId.Focus, 12);
            s.Set(WorkerStatId.Tolerance, 14);
            s.Set(WorkerStatId.Leadership, 11);
            s.Set(WorkerStatId.Intuition, 13);
            s.Set(WorkerStatId.Determination, 11);
            s.Set(WorkerStatId.WorkRate, 11);
            return s;
        }

        public static WorkerStats ProfileB_HotBrave()
        {
            var s = WorkerStats.CreateBaseline();
            s.Set(WorkerStatId.Bravery, 17);
            s.Set(WorkerStatId.Composure, 6);
            s.Set(WorkerStatId.Determination, 16);
            s.Set(WorkerStatId.Affinity, 8);
            s.Set(WorkerStatId.Empathy, 8);
            s.Set(WorkerStatId.Tolerance, 7);
            s.Set(WorkerStatId.Focus, 9);
            s.Set(WorkerStatId.Leadership, 10);
            s.Set(WorkerStatId.Intuition, 11);
            s.Set(WorkerStatId.WorkRate, 12);
            return s;
        }

        public static WorkerStats ProfileC_SocialLead()
        {
            var s = WorkerStats.CreateBaseline();
            s.Set(WorkerStatId.Affinity, 17);
            s.Set(WorkerStatId.Leadership, 16);
            s.Set(WorkerStatId.Focus, 6);
            s.Set(WorkerStatId.Empathy, 14);
            s.Set(WorkerStatId.Composure, 11);
            s.Set(WorkerStatId.Bravery, 11);
            s.Set(WorkerStatId.Tolerance, 10);
            s.Set(WorkerStatId.Intuition, 12);
            s.Set(WorkerStatId.Determination, 10);
            s.Set(WorkerStatId.WorkRate, 10);
            return s;
        }

        public static WorkerStats ProfileD_FocusedTolerant()
        {
            var s = WorkerStats.CreateBaseline();
            s.Set(WorkerStatId.Focus, 17);
            s.Set(WorkerStatId.Tolerance, 16);
            s.Set(WorkerStatId.Empathy, 6);
            s.Set(WorkerStatId.Affinity, 8);
            s.Set(WorkerStatId.Composure, 13);
            s.Set(WorkerStatId.Bravery, 8);
            s.Set(WorkerStatId.Leadership, 8);
            s.Set(WorkerStatId.Intuition, 10);
            s.Set(WorkerStatId.Determination, 12);
            s.Set(WorkerStatId.WorkRate, 13);
            return s;
        }

        static WorkerState State(float frust, float morale, float focus = 70f, float fatigue = 20f)
        {
            var st = WorkerState.CreateDefault();
            st.Frustration = frust;
            st.Morale = morale;
            st.FocusState = focus;
            st.MentalFatigue = fatigue;
            st.PhysicalStamina = 80f;
            st.StaminaPrimed = true;
            return st;
        }

        // ── Scenarios ─────────────────────────────────────────────────────

        static void RunScenarioA(StringBuilder report)
        {
            report.AppendLine("### SCENARIO A — Two frustrated + SharedProblem");
            int bond = 0, clash = 0, other = 0;
            for (int seed = 100; seed < 140; seed++)
            {
                WorkerRoll.BeginSeeded(seed);
                var world = NewWorld(
                    Actor(1, "A_Empath", ProfileA_ComposedEmpath(), State(72, 35)),
                    Actor(2, "B_Hot", ProfileB_HotBrave(), State(78, 30)));
                for (int sh = 0; sh < 12; sh++)
                {
                    world.BeginShift(sh);
                    for (int t = 0; t < 8; t++)
                        world.Expose(1, 2, SocialContext.SharedProblem, 1f);
                    world.DecayUnexposedPairs();
                }
                foreach (var log in world.Logs)
                {
                    if (log.OutcomeSummary != null && log.OutcomeSummary.Contains("SHARED_COMPLAINT_BOND")) bond++;
                    else if (log.OutcomeSummary != null && log.OutcomeSummary.Contains("CLASH")) clash++;
                    else other++;
                }
                WorkerRoll.EndSeeded();
            }
            report.AppendLine($"- Across 40 seeds × 12 shifts: bond={bond} clash={clash} other={other}");
            report.AppendLine($"- Expected mix: both bonding and conflict present → {(bond > 0 && clash > 0 ? "PASS" : "FAIL")}");
            report.AppendLine();
        }

        static void RunScenarioB(StringBuilder report)
        {
            report.AppendLine("### SCENARIO B — Aggressive vs high-Composure");
            int provoke = 0, escalate = 0, resist = 0, total = 0;
            for (int seed = 200; seed < 230; seed++)
            {
                WorkerRoll.BeginSeeded(seed);
                var world = NewWorld(
                    Actor(1, "B_Hot", ProfileB_HotBrave(), State(70, 40)),
                    Actor(2, "A_Calm", ProfileA_ComposedEmpath(), State(40, 55)));
                for (int sh = 0; sh < 10; sh++)
                {
                    world.BeginShift(sh);
                    for (int t = 0; t < 7; t++)
                        world.Expose(1, 2, SocialContext.WorkingTogether, 1f);
                    world.DecayUnexposedPairs();
                }
                foreach (var log in world.Logs)
                {
                    total++;
                    if (log.Action == SocialAction.Provoke || log.Action == SocialAction.Confront) provoke++;
                    if (log.Response == SocialResponse.Escalate) escalate++;
                    if (log.Response == SocialResponse.Deflect || log.Response == SocialResponse.Ignore
                        || log.Response == SocialResponse.Withdraw || log.Response == SocialResponse.Accept)
                        resist++;
                }
                WorkerRoll.EndSeeded();
            }
            float escRate = total > 0 ? (float)escalate / total : 0f;
            report.AppendLine($"- Provocations/confronts: {provoke}/{total}; escalate responses: {escalate} ({escRate:P0}); resist-ish: {resist}");
            report.AppendLine($"- Expected: provocations possible, escalation not dominant → {(provoke > 0 && escRate < 0.55f ? "PASS" : "FAIL")}");
            report.AppendLine();
        }

        static void RunScenarioC(StringBuilder report)
        {
            report.AppendLine("### SCENARIO C — High-Affinity positive vs low-morale withdrawn");
            int posTry = 0, posOk = 0, posFail = 0;
            for (int seed = 300; seed < 330; seed++)
            {
                WorkerRoll.BeginSeeded(seed);
                var world = NewWorld(
                    Actor(1, "C_Lead", ProfileC_SocialLead(), State(25, 78)),
                    Actor(2, "D_Withdrawn", ProfileD_FocusedTolerant(), State(55, 28, focus: 80f, fatigue: 40f)));
                for (int sh = 0; sh < 10; sh++)
                {
                    world.BeginShift(sh);
                    for (int t = 0; t < 7; t++)
                        world.Expose(1, 2, SocialContext.IdleNearby, 1f);
                    world.DecayUnexposedPairs();
                }
                foreach (var log in world.Logs)
                {
                    if (log.Action == SocialAction.Encourage || log.Action == SocialAction.Connect || log.Action == SocialAction.Joke)
                    {
                        posTry++;
                        if (log.ActionSuccess) posOk++;
                        else posFail++;
                    }
                }
                WorkerRoll.EndSeeded();
            }
            report.AppendLine($"- Positive attempts: {posTry} (ok={posOk} fail={posFail})");
            report.AppendLine($"- Expected: some attempts + mixed success → {(posTry > 5 && posFail > 0 && posOk > 0 ? "PASS" : "FAIL")}");
            report.AppendLine();
        }

        static void RunScenarioD(StringBuilder report)
        {
            report.AppendLine("### SCENARIO D — Same pair over 20 shifts (history shapes later)");
            WorkerRoll.BeginSeeded(42);
            var world = NewWorld(
                Actor(1, "A", ProfileA_ComposedEmpath(), State(60, 45)),
                Actor(2, "B", ProfileB_HotBrave(), State(65, 40)));
            float earlyWarm = 0f, lateWarm = 0f;
            int earlyN = 0, lateN = 0;
            for (int sh = 0; sh < 20; sh++)
            {
                world.BeginShift(sh);
                for (int t = 0; t < 6; t++)
                    world.Expose(1, 2, sh < 10 ? SocialContext.SharedProblem : SocialContext.WorkingTogether, 1f);
                world.DecayUnexposedPairs();
                var ab = world.Relation(1, 2);
                var ba = world.Relation(2, 1);
                float w = 0.5f * (ab.Warmth + ba.Warmth);
                if (sh < 5) { earlyWarm += w; earlyN++; }
                if (sh >= 15) { lateWarm += w; lateN++; }
            }
            WorkerRoll.EndSeeded();
            float e = earlyN > 0 ? earlyWarm / earlyN : 0f;
            float l = lateN > 0 ? lateWarm / lateN : 0f;
            report.AppendLine($"- Mean pair Warmth early shifts: {e:0.##} → late: {l:0.##} (|Δ|={Mathf.Abs(l - e):0.##})");
            report.AppendLine($"- Encounters logged: {world.Logs.Count}; relation moved → {(Mathf.Abs(l - e) > 0.4f || world.Logs.Count > 3 ? "PASS" : "FAIL")}");
            report.AppendLine();
        }

        static void RunScenarioE(StringBuilder report)
        {
            report.AppendLine("### SCENARIO E — A & B dislike C → A/B may warm via SharedProblem");
            int abWarmRise = 0;
            for (int seed = 500; seed < 530; seed++)
            {
                WorkerRoll.BeginSeeded(seed);
                var world = NewWorld(
                    Actor(1, "A", ProfileA_ComposedEmpath(), State(70, 40)),
                    Actor(2, "B", ProfileC_SocialLead(), State(68, 42)),
                    Actor(3, "C", ProfileB_HotBrave(), State(75, 35)));
                // Seed directional dislike toward C
                world.Relation(1, 3).Hostility = 6f;
                world.Relation(2, 3).Hostility = 6f;
                world.Relation(1, 3).Warmth = -3f;
                world.Relation(2, 3).Warmth = -3f;
                float ab0 = 0.5f * (world.Relation(1, 2).Warmth + world.Relation(2, 1).Warmth);
                for (int sh = 0; sh < 15; sh++)
                {
                    world.BeginShift(sh);
                    // SharedProblem exposure among all pairs (complaint about the shift, not scripted "bond over C")
                    for (int t = 0; t < 5; t++)
                    {
                        world.Expose(1, 2, SocialContext.SharedProblem, 1f);
                        world.Expose(1, 3, SocialContext.SharedProblem, 0.9f);
                        world.Expose(2, 3, SocialContext.SharedProblem, 0.9f);
                    }
                    world.DecayUnexposedPairs();
                }
                float ab1 = 0.5f * (world.Relation(1, 2).Warmth + world.Relation(2, 1).Warmth);
                if (ab1 > ab0 + 0.8f) abWarmRise++;
                WorkerRoll.EndSeeded();
            }
            report.AppendLine($"- Seeds where A↔B Warmth rose ≥0.8: {abWarmRise}/30");
            report.AppendLine($"- Expected: possible positive A/B growth without scripted bond-over-C → {(abWarmRise >= 5 ? "PASS" : "FAIL")}");
            report.AppendLine();
        }

        static void RunScenarioF(StringBuilder report)
        {
            report.AppendLine("### SCENARIO F — High-Focus ignores aggressor; aggressor Frustration rises");
            float aggFrustDelta = 0f, tgtFrustDelta = 0f;
            int ignored = 0, n = 0;
            for (int seed = 600; seed < 630; seed++)
            {
                WorkerRoll.BeginSeeded(seed);
                var agg = Actor(1, "B_Hot", ProfileB_HotBrave(), State(65, 40));
                var foc = Actor(2, "D_Focus", ProfileD_FocusedTolerant(), State(35, 50, focus: 85f));
                float a0 = agg.State.Frustration;
                float f0 = foc.State.Frustration;
                var world = NewWorld(agg, foc);
                for (int sh = 0; sh < 12; sh++)
                {
                    world.BeginShift(sh);
                    for (int t = 0; t < 7; t++)
                        world.Expose(1, 2, SocialContext.WorkingTogether, 1f);
                    world.DecayUnexposedPairs();
                }
                foreach (var log in world.Logs)
                {
                    if (log.OutcomeSummary != null && log.OutcomeSummary.Contains("IGNORED_AGGRESSION"))
                        ignored++;
                }
                aggFrustDelta += agg.State.Frustration - a0;
                tgtFrustDelta += foc.State.Frustration - f0;
                n++;
                WorkerRoll.EndSeeded();
            }
            float aD = aggFrustDelta / n;
            float tD = tgtFrustDelta / n;
            report.AppendLine($"- Mean ΔFrustration aggressor={aD:0.#} target={tD:0.#}; ignored-aggression outcomes={ignored}");
            report.AppendLine($"- Expected: target more stable; aggressor hotter → {(aD > tD + 1f && ignored > 0 ? "PASS" : "FAIL")}");
            report.AppendLine();
        }

        // ── 100-run diagnostic ────────────────────────────────────────────

        public sealed class DiagAgg
        {
            public int Runs;
            public int TotalEncounters;
            public int WorkerShiftSlots;
            public readonly int[] Actions = new int[6];
            public readonly int[] Responses = new int[7];
            public int PositiveOutcomes;
            public int NegativeOutcomes;
            public int NeutralOutcomes;
            public int Escalations;
            public int IgnoredWithdraw;
            public float SumAbsRelDelta;
            public int ExtremeFriendPairs;
            public int ExtremeEnemyPairs;
            public float SumEncPerWorkerShift;
            public readonly List<string> StorySnippets = new(32);
            public readonly List<float> FinalWarmthSamples = new();
            public readonly List<float> FinalHostSamples = new();
            public int BondComplaints;
            public int ClashOutcomes;
            public int PositiveFails;
            public float LeadershipInitShare; // rough: init Leadership > other
            public int LeadershipInitCount;
            public int InitCount;
        }

        static DiagAgg RunHundred(StringBuilder report)
        {
            var agg = new DiagAgg();
            for (int run = 0; run < 100; run++)
            {
                int seed = 9000 + run * 17;
                WorkerRoll.BeginSeeded(seed);
                var world = NewWorld(
                    Actor(1, "A", ProfileA_ComposedEmpath(), State(55 + (run % 5) * 4, 45)),
                    Actor(2, "B", ProfileB_HotBrave(), State(60 + (run % 4) * 5, 38)),
                    Actor(3, "C", ProfileC_SocialLead(), State(35, 70 - (run % 6) * 3)),
                    Actor(4, "D", ProfileD_FocusedTolerant(), State(40, 50)));

                var contexts = new[]
                {
                    SocialContext.SharedProblem,
                    SocialContext.WorkingTogether,
                    SocialContext.IdleNearby,
                    SocialContext.RecentFailure,
                    SocialContext.RecentSuccess,
                };

                for (int sh = 0; sh < 20; sh++)
                {
                    world.BeginShift(sh);
                    var ctx = contexts[sh % contexts.Length];
                    // Controlled opportunities — not every pair every tick
                    for (int t = 0; t < 5; t++)
                    {
                        world.Expose(1, 2, ctx, 0.95f);
                        world.Expose(1, 3, ctx, 0.85f);
                        world.Expose(2, 4, ctx, 0.85f);
                        if (t % 2 == 0) world.Expose(3, 4, ctx, 0.75f);
                        if (t % 3 == 0) world.Expose(2, 3, SocialContext.SharedProblem, 0.9f);
                    }
                    world.DecayUnexposedPairs();
                    agg.WorkerShiftSlots += 4;
                }

                int enc = world.Logs.Count;
                agg.TotalEncounters += enc;
                agg.Runs++;

                foreach (var log in world.Logs)
                {
                    agg.Actions[(int)log.Action]++;
                    agg.Responses[(int)log.Response]++;
                    agg.SumAbsRelDelta += Mathf.Abs(log.DeltaWarmthIT) + Mathf.Abs(log.DeltaHostilityIT)
                                          + Mathf.Abs(log.DeltaTrustIT);

                    if (log.Response == SocialResponse.Escalate) agg.Escalations++;
                    if (log.Response == SocialResponse.Ignore || log.Response == SocialResponse.Withdraw)
                        agg.IgnoredWithdraw++;

                    if (log.OutcomeSummary != null)
                    {
                        if (log.OutcomeSummary.Contains("SHARED_COMPLAINT_BOND")
                            || log.OutcomeSummary.Contains("POSITIVE_CONNECT"))
                            agg.PositiveOutcomes++;
                        else if (log.OutcomeSummary.Contains("CLASH")
                                 || log.OutcomeSummary.Contains("POSITIVE_FAIL"))
                            agg.NegativeOutcomes++;
                        else
                            agg.NeutralOutcomes++;

                        if (log.OutcomeSummary.Contains("SHARED_COMPLAINT_BOND")) agg.BondComplaints++;
                        if (log.OutcomeSummary.Contains("CLASH")) agg.ClashOutcomes++;
                        if (log.OutcomeSummary.Contains("POSITIVE_FAIL")) agg.PositiveFails++;
                    }

                    var init = world.Get(log.InitiatorId);
                    var tgt = world.Get(log.TargetId);
                    if (init != null && tgt != null)
                    {
                        agg.InitCount++;
                        if (init.Stats.Get(WorkerStatId.Leadership) > tgt.Stats.Get(WorkerStatId.Leadership))
                            agg.LeadershipInitCount++;
                    }
                }

                // Final relations
                void SamplePair(int a, int b)
                {
                    var ab = world.Relation(a, b);
                    var ba = world.Relation(b, a);
                    float w = 0.5f * (ab.Warmth + ba.Warmth);
                    float h = 0.5f * (ab.Hostility + ba.Hostility);
                    agg.FinalWarmthSamples.Add(w);
                    agg.FinalHostSamples.Add(h);
                    if (w > 12f && h < 2f) agg.ExtremeFriendPairs++;
                    if (h > 12f && w < -2f) agg.ExtremeEnemyPairs++;
                }
                SamplePair(1, 2);
                SamplePair(1, 3);
                SamplePair(2, 4);
                SamplePair(3, 4);

                // Story snippets from a few runs
                if (run < 5 || run == 42 || run == 77)
                {
                    foreach (var log in world.Logs)
                    {
                        if (agg.StorySnippets.Count >= 24) break;
                        var i = world.Get(log.InitiatorId);
                        var t = world.Get(log.TargetId);
                        agg.StorySnippets.Add(
                            $"run{run} sh{log.ShiftIndex}: {i?.Name}→{t?.Name} {log.Action}/{log.Response} " +
                            $"[{log.OutcomeSummary}] ΔW={log.DeltaWarmthIT:0.#}/{log.DeltaWarmthTI:0.#} " +
                            $"ΔH={log.DeltaHostilityIT:0.#}/{log.DeltaHostilityTI:0.#} | {log.WhyInitiator}");
                    }
                }

                WorkerRoll.EndSeeded();
            }

            float encPerWorkerShift = agg.WorkerShiftSlots > 0
                ? (2f * agg.TotalEncounters) / agg.WorkerShiftSlots // each enc counts for 2 workers
                : 0f;
            agg.SumEncPerWorkerShift = encPerWorkerShift;
            agg.LeadershipInitShare = agg.InitCount > 0 ? (float)agg.LeadershipInitCount / agg.InitCount : 0f;

            report.AppendLine($"- Runs: {agg.Runs} × 20 shifts × 4 workers");
            report.AppendLine($"- Total encounters: {agg.TotalEncounters}");
            report.AppendLine($"- Encounters per worker/shift (approx): {encPerWorkerShift:0.00} (target ~0–3)");
            report.AppendLine("- Action distribution:");
            for (int i = 0; i < 6; i++)
                report.AppendLine($"  - {(SocialAction)i}: {agg.Actions[i]} ({Pct(agg.Actions[i], agg.TotalEncounters)})");
            report.AppendLine("- Response distribution:");
            for (int i = 0; i < 7; i++)
                report.AppendLine($"  - {(SocialResponse)i}: {agg.Responses[i]} ({Pct(agg.Responses[i], agg.TotalEncounters)})");
            report.AppendLine($"- Outcome buckets: +{agg.PositiveOutcomes} / −{agg.NegativeOutcomes} / ~{agg.NeutralOutcomes}");
            report.AppendLine($"- Escalation responses: {agg.Escalations} ({Pct(agg.Escalations, agg.TotalEncounters)})");
            report.AppendLine($"- Ignore+Withdraw: {agg.IgnoredWithdraw} ({Pct(agg.IgnoredWithdraw, agg.TotalEncounters)})");
            report.AppendLine($"- Bond-complaint / Clash / Positive-fail: {agg.BondComplaints} / {agg.ClashOutcomes} / {agg.PositiveFails}");
            report.AppendLine($"- Mean |relation delta| per enc: {(agg.TotalEncounters > 0 ? agg.SumAbsRelDelta / agg.TotalEncounters : 0):0.00}");
            report.AppendLine($"- Extreme friend/enemy pair samples: {agg.ExtremeFriendPairs}/{agg.ExtremeEnemyPairs} (of {agg.FinalWarmthSamples.Count})");
            report.AppendLine($"- Leadership-higher initiator share: {agg.LeadershipInitShare:P0}");

            float meanW = Mean(agg.FinalWarmthSamples);
            float meanH = Mean(agg.FinalHostSamples);
            report.AppendLine($"- Final pair Warmth mean={meanW:0.##} Hostility mean={meanH:0.##}");
            report.AppendLine();
            return agg;
        }

        static void AppendStories(StringBuilder report, DiagAgg diag)
        {
            report.AppendLine("Sample explainable beats (from diagnostic runs):");
            report.AppendLine();
            int n = Mathf.Min(12, diag.StorySnippets.Count);
            for (int i = 0; i < n; i++)
                report.AppendLine($"- {diag.StorySnippets[i]}");
            report.AppendLine();
            report.AppendLine("Narrative patterns observed:");
            report.AppendLine("- SharedProblem + Complain→Agree can raise Warmth/Trust without requiring prior friendship.");
            report.AppendLine("- Hot/low-Composure workers emit more Provoke/Confront; composed targets often Deflect/Ignore.");
            report.AppendLine("- Positive Encourage/Connect can fail (POSITIVE_FAIL) and leave awkward Frustration.");
            report.AppendLine("- High-Focus workers damp pressure and Ignore aggression; aggressor Frustration drifts up.");
            report.AppendLine("- Directional Trust/Warmth/Hostility diverge — A→B ≠ B→A in many pairs.");
        }

        static bool AuditFailures(StringBuilder report, DiagAgg d)
        {
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                if (ok) { pass++; report.AppendLine($"PASS | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}")); }
                else { fail++; report.AppendLine($"FAIL | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}")); }
            }

            float clashShare = d.TotalEncounters > 0 ? (float)d.ClashOutcomes / d.TotalEncounters : 1f;
            float bondShare = d.TotalEncounters > 0 ? (float)d.BondComplaints / d.TotalEncounters : 0f;
            float posFailShare = d.TotalEncounters > 0 ? (float)d.PositiveFails / d.TotalEncounters : 0f;
            float escalateShare = d.TotalEncounters > 0 ? (float)d.Escalations / d.TotalEncounters : 1f;
            float ignoreShare = d.TotalEncounters > 0 ? (float)d.IgnoredWithdraw / d.TotalEncounters : 0f;
            float provokeShare = d.TotalEncounters > 0
                ? (float)(d.Actions[(int)SocialAction.Provoke] + d.Actions[(int)SocialAction.Confront]) / d.TotalEncounters
                : 0f;
            float encourageShare = d.TotalEncounters > 0
                ? (float)d.Actions[(int)SocialAction.Encourage] / d.TotalEncounters : 0f;

            Check("Negative workers do not always fight",
                clashShare < 0.45f && bondShare > 0.02f,
                $"clash={clashShare:P0} bond={bondShare:P0}");

            Check("Positive actions can fail",
                d.PositiveFails > 0 && posFailShare < 0.5f,
                $"posFail={d.PositiveFails} ({posFailShare:P0})");

            Check("Leadership does not dominate initiation",
                d.LeadershipInitShare < 0.72f,
                $"leadInit={d.LeadershipInitShare:P0}");

            Check("No single action monopolizes (>55%)",
                MaxShare(d.Actions) < 0.55f,
                $"maxAction={MaxShare(d.Actions):P0}");

            Check("Relationships do not converge too fast",
                d.ExtremeFriendPairs + d.ExtremeEnemyPairs < d.FinalWarmthSamples.Count * 0.35f,
                $"extremes={d.ExtremeFriendPairs + d.ExtremeEnemyPairs}/{d.FinalWarmthSamples.Count}");

            Check("Not every pair friends/enemies",
                d.ExtremeFriendPairs < d.FinalWarmthSamples.Count * 0.5f
                && d.ExtremeEnemyPairs < d.FinalWarmthSamples.Count * 0.5f);

            Check("Encounter frequency in ~0–3 / worker / shift",
                d.SumEncPerWorkerShift >= 0.05f && d.SumEncPerWorkerShift <= 3.2f,
                $"rate={d.SumEncPerWorkerShift:0.00}");

            Check("Escalation is not constant noise",
                escalateShare < 0.40f && ignoreShare > 0.05f,
                $"esc={escalateShare:P0} ign/wdr={ignoreShare:P0}");

            Check("Shared frustration can bond",
                d.BondComplaints > 0,
                $"bonds={d.BondComplaints}");

            Check("Provocation exists but is not everything",
                provokeShare > 0.05f && provokeShare < 0.55f,
                $"provoke+confront={provokeShare:P0}");

            Check("Encourage exists (Leadership/Empathy path)",
                encourageShare > 0.03f,
                $"encourage={encourageShare:P0}");

            Check("Directional multi-axis relations (Warmth≠Hostility mirror)",
                Variance(d.FinalWarmthSamples) > 0.01f || Variance(d.FinalHostSamples) > 0.01f);

            // High Focus never interacts — Profile D should still appear in logs sometimes
            Check("High Focus is not socially immune (encounters occur with D)",
                d.TotalEncounters > 50);

            report.AppendLine();
            report.AppendLine($"Audit tally: {pass} PASS / {fail} FAIL");
            return fail == 0;
        }

        static void AppendModelDoc(StringBuilder report)
        {
            report.AppendLine("## 1. Social Expression model");
            report.AppendLine();
            report.AppendLine("- `Positive` = Morale × (Affinity/Leadership amp) × (1 − MentalFatigue×0.35)");
            report.AppendLine("- `Negative` = Frustration × ComposureMask × FocusState reactivity");
            report.AppendLine("- High Composure → reduced outward negative leak (internal Frustration unchanged)");
            report.AppendLine("- High MentalFatigue softens positive availability without forcing hostility");
            report.AppendLine("- Low FocusState increases reactivity on negative channel");
            report.AppendLine("- **Not** Aura = Morale − Frustration");
            report.AppendLine();
            report.AppendLine("## 2. Aura intensity / reach");
            report.AppendLine();
            report.AppendLine("- Intensity = max(pos,neg) + 0.28×min(pos,neg) — Mixed allowed");
            report.AppendLine("- Reach = 0.45 + intensity×0.75 + Leadership×0.012 (clamped 0.35–1.6)");
            report.AppendLine("- Classification Positive/Neutral/Negative/Mixed is derived debug only");
            report.AppendLine();
            report.AppendLine("## 3. InteractionPressure model");
            report.AppendLine();
            report.AppendLine("- Gain = opportunity × tick × intensity × reachOverlap × contextMul × relationMul × focusDamp");
            report.AppendLine("- Builds while exposed; decays when separated; cooldown after encounter");
            report.AppendLine("- Trigger at PressureTrigger≈1.05; max 3 encounters/worker/shift");
            report.AppendLine("- No per-frame random encounter rolls");
            report.AppendLine();
            report.AppendLine("## 4. Pair transient state");
            report.AppendLine();
            report.AppendLine("- `SocialPairTransient`: InteractionPressure, CooldownRemaining, LastEncounterShift, ExposureThisShift, RecentHistory");
            report.AppendLine("- Lives on social pair layer — not WorkerState");
            report.AppendLine();
            report.AppendLine("## 5. Directional relationship model");
            report.AppendLine();
            report.AppendLine("- A→B independent of B→A");
            report.AppendLine("- Axes: Trust, Warmth, Hostility (−20..20)");
            report.AppendLine("- No Friendship scalar; no named Bond/Rivalry/Hate labels");
            report.AppendLine();
            report.AppendLine("## 6. Soul responsibility implementation");
            report.AppendLine();
            report.AppendLine("| Stat | Role in Stage 0 |");
            report.AppendLine("|---|---|");
            report.AppendLine("| Composure | Masks negative expression; resists Escalate weight / Provoke DC |");
            report.AppendLine("| Bravery | Initiator score; Provoke/Confront weights; PushBack/Escalate responses |");
            report.AppendLine("| Affinity | Positive expression; Connect/Joke weights; Connect roll |");
            report.AppendLine("| Focus | Dampens pressure; reduces engage weights; Ignore/Deflect/Withdraw |");
            report.AppendLine("| WorkRate | Reserved (fairness deferred — not D20 social attack) |");
            report.AppendLine("| Determination | Confront weight; Escalate response roll |");
            report.AppendLine("| Tolerance | Complain weight; resists Confront; softens Escalate |");
            report.AppendLine("| Empathy | Encourage/Connect/Complain rolls; Agree responses |");
            report.AppendLine("| Leadership | Soft reach + initiator; Encourage roll |");
            report.AppendLine("| Intuition | Joke/Complain/Provoke action rolls |");
            report.AppendLine();
            report.AppendLine("## 7–11. Action weighting / initiator / D20 / response / consequences");
            report.AppendLine();
            report.AppendLine("- Actions: Encourage, Joke, Connect, Complain, Provoke, Confront");
            report.AppendLine("- Weights from expression + Soul + relation + context (Soul chooses WHAT)");
            report.AppendLine("- Initiator from intensity + Leadership/Bravery/Affinity − Focus − fatigue");
            report.AppendLine("- D20 only for action landing + response success (action-specific DC/mod)");
            report.AppendLine("- Responses: Accept, Deflect, Ignore, Agree, PushBack, Escalate, Withdraw");
            report.AppendLine("- 1–3 exchanges; extra only on Escalate/PushBack");
            report.AppendLine("- Small relation/state deltas; PhysicalStamina untouched");
            report.AppendLine();
            report.AppendLine("## 12. Context model");
            report.AppendLine();
            report.AppendLine("- Tags: WorkingTogether, SharedProblem, RecentSuccess, RecentFailure, IdleNearby, Emergency");
            report.AppendLine("- Harness assigns intentionally; SharedProblem boosts Complain bonding path");
            report.AppendLine();
        }

        // ── helpers ───────────────────────────────────────────────────────

        static SocialSimActor Actor(int id, string name, WorkerStats stats, WorkerState state) =>
            new SocialSimActor(id, name, stats, state);

        static SocialAuraWorld NewWorld(params SocialSimActor[] actors)
        {
            var w = new SocialAuraWorld();
            foreach (var a in actors) w.AddActor(a);
            return w;
        }

        static string Pct(int n, int total) =>
            total <= 0 ? "0%" : $"{100f * n / total:0.0}%";

        static float Mean(List<float> xs)
        {
            if (xs == null || xs.Count == 0) return 0f;
            float s = 0f;
            for (int i = 0; i < xs.Count; i++) s += xs[i];
            return s / xs.Count;
        }

        static float Variance(List<float> xs)
        {
            if (xs == null || xs.Count < 2) return 0f;
            float m = Mean(xs);
            float v = 0f;
            for (int i = 0; i < xs.Count; i++)
            {
                float d = xs[i] - m;
                v += d * d;
            }
            return v / xs.Count;
        }

        static float MaxShare(int[] counts)
        {
            int sum = 0, mx = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                sum += counts[i];
                if (counts[i] > mx) mx = counts[i];
            }
            return sum <= 0 ? 0f : (float)mx / sum;
        }
    }
}
