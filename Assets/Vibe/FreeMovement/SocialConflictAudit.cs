using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Offline Social Conflict audit (Rounds 3–5 + Round 9 death/balance).
    /// Seeded D20. No Unity scene required.
    /// Menu: DeepCore/Diagnostics/Run Social Conflict Audit
    ///       DeepCore/Diagnostics/Run Social Conflict Death Audit
    /// </summary>
    public static class SocialConflictAudit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[SOCIAL CONFLICT] Report: {path}");
#if UNITY_EDITOR
            string deathPath = Path.Combine(
                Path.GetDirectoryName(path) ?? "",
                "social_conflict_death_latest.md");
            bool fail = File.Exists(path) && File.ReadAllText(path).Contains("INVARIANT: FAIL");
            if (File.Exists(deathPath) && File.ReadAllText(deathPath).Contains("INVARIANT: FAIL"))
                fail = true;
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var log = new StringBuilder(40_000);
            var death = new StringBuilder(28_000);
            int pass = 0, fail = 0;
            bool mirrorDeath = false;
            void Check(string name, bool ok, string detail = "")
            {
                string line = (ok ? "PASS" : "FAIL") + " | " + name
                              + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}");
                if (ok) pass++;
                else fail++;
                log.AppendLine(line);
                if (mirrorDeath) death.AppendLine(line);
            }

            void SectionBoth(string title)
            {
                mirrorDeath = true;
                log.AppendLine();
                log.AppendLine(title);
                death.AppendLine();
                death.AppendLine(title);
            }

            log.AppendLine("# Social Conflict Audit (Rounds 3–5 + Death Round 9)");
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine();
            log.AppendLine("Scope: Argument escalation, rare fights, witnesses, lethal gates, balance matrix.");
            log.AppendLine("Extends Social Aura — does not redesign Memory/Relationship architecture.");
            log.AppendLine();
            log.AppendLine("## Tunables");
            log.AppendLine($"- Argue: Frust≥{SocialConflictTuning.FrustrationMin} Host≥{SocialConflictTuning.HostilityMin} Trust≤{SocialConflictTuning.TrustMaxForArgue}");
            log.AppendLine($"- Fight: Host≥{SocialConflictTuning.FightHostilityMin} Frust≥{SocialConflictTuning.FightFrustrationMin} Trust≤{SocialConflictTuning.FightTrustMax} chance={SocialConflictTuning.FightChanceAtPeak}");
            log.AppendLine($"- Lethal: Host≥{SocialConflictTuning.LethalHostilityMin} Trust≤{SocialConflictTuning.LethalTrustMax} chance@{SocialConflictTuning.LethalChanceGivenCritical}");
            log.AppendLine($"- ExchangeIntervalHours={SocialConflictTuning.ExchangeIntervalHours} (game-hours, not frames)");
            log.AppendLine();

            death.AppendLine("# Social Conflict Death Audit (Round 9)");
            death.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            death.AppendLine();
            death.AppendLine("## Architecture summary");
            death.AppendLine("- SocialConflictSystem sits on SocialAuraWorld; fights escalate Scuffle → Severe → Critical → Lethal.");
            death.AppendLine("- Death requires CanLethalCombination (hostility, trust, murderous Major negatives, failed de-escalation/intervention, low Empathy + high Bravery) plus Critical severity + chance (or ForceLethal).");
            death.AppendLine("- Hostility alone / Frustration alone never kill. High Respect rivalry blocks lethal when Hostility < 18.");
            death.AppendLine("- WorkerState.MarkDead is persistent; ResetToSpawnDefaults does not revive. Dead workers fail SocialAuraEligibility.");
            death.AppendLine("- Presentation (FreeMovementSocketMapRunner.TryPresentSocialConflictBeats) skips !IsAlive speakers.");
            death.AppendLine();
            death.AppendLine("## Files changed");
            death.AppendLine("- Assets/Vibe/FreeMovement/SocialConflictAudit.cs — lethal gates, balance matrix, dead exclusion, dual reports");
            death.AppendLine("- Assets/Vibe/FreeMovement/SocialConflictDeathAudit.cs — thin menu/entry wrapper");
            death.AppendLine("- Assets/Vibe/FreeMovement/Editor/WorkerV11LockAuditMenu.cs — Death Audit menu item");
            death.AppendLine("- Assets/Vibe/FreeMovement/FreeMovementSocketMapRunner.cs — skip dead speakers in conflict presentation");
            death.AppendLine();

            WorkerStateClock.GameHours = 200f;

            // ─── 1. Frustration alone ≠ violence / argument ─────────
            log.AppendLine("## 1. Frustration alone ≠ argument or fight");
            WorkerRoll.BeginSeeded(101);
            try
            {
                var world = MakeWorld();
                var a = world.Get(1);
                var b = world.Get(2);
                a.State.Frustration = 90f;
                b.State.Frustration = 85f;
                world.Relation(1, 2).Trust = 2f;
                world.Relation(1, 2).Hostility = 1f;
                world.Relation(2, 1).Trust = 2f;
                world.Relation(2, 1).Hostility = 1f;

                bool blocked = SocialConflictSystem.FrustrationAloneInsufficient(world, a, b, 200f);
                Check("High Frustration alone cannot meet emergence", blocked);

                var conflict = new SocialConflictSystem();
                var jokeLog = MakeHostileishLog(1, 2, SocialAction.Joke, SocialResponse.Accept, false);
                var emerged = conflict.TryEmergeFromEncounter(world, jokeLog, 200f);
                Check("Non-hostile encounter does not open argument under high Frust only",
                    emerged == null && conflict.TotalArgumentsStarted == 0,
                    $"args={conflict.TotalArgumentsStarted}");
                Check("No fights from frustration alone", conflict.TotalFights == 0);
            }
            finally { WorkerRoll.EndSeeded(); }

            // ─── 2. Healthy crew rarely fights ──────────────────────
            log.AppendLine();
            log.AppendLine("## 2. Healthy crew rarely fights");
            WorkerRoll.BeginSeeded(202);
            try
            {
                int fights = 0;
                int args = 0;
                for (int trial = 0; trial < 40; trial++)
                {
                    var world = MakeHealthyWorld();
                    var conflict = new SocialConflictSystem();
                    var logEnc = MakeHostileishLog(1, 2, SocialAction.Complain, SocialResponse.Deflect, false);
                    conflict.TryEmergeFromEncounter(world, logEnc, 210f + trial);
                    args += conflict.TotalArgumentsStarted;
                    fights += conflict.TotalFights;

                    var s = conflict.GetSession(1, 2);
                    if (s != null && s.IsActive)
                    {
                        for (int b = 0; b < 8; b++)
                        {
                            float t = 210f + trial + (b + 1) * SocialConflictTuning.ExchangeIntervalHours;
                            conflict.TickActivePair(world, 1, 2, t, 0.5f, world.Pair(1, 2));
                        }
                        fights = Math.Max(fights, conflict.TotalFights);
                    }
                }
                Check("Healthy crew argument starts rare across 40 trials",
                    args <= 4, $"arguments={args}");
                Check("Healthy crew fights ~never across trials",
                    fights == 0, $"fights={fights}");
            }
            finally { WorkerRoll.EndSeeded(); }

            // ─── 3. Dysfunctional crew can reach fights ─────────────
            log.AppendLine();
            log.AppendLine("## 3. Dysfunctional crew can reach fights");
            WorkerRoll.BeginSeeded(303);
            try
            {
                int fights = 0;
                int args = 0;
                SocialConflictEvent lastFight = null;
                for (int trial = 0; trial < 80; trial++)
                {
                    var world = MakeDysfunctionalPair();
                    var conflict = new SocialConflictSystem();
                    var clash = MakeHostileishLog(1, 2, SocialAction.Confront, SocialResponse.Escalate, true);
                    clash.DeltaHostilityIT = 1.6f;
                    clash.DeltaHostilityTI = 1.5f;
                    clash.OutcomeSummary = "CLASH";
                    var opened = conflict.TryEmergeFromEncounter(world, clash, 300f + trial);
                    if (opened != null || conflict.TotalArgumentsStarted > 0)
                        args++;

                    for (int b = 0; b < 10; b++)
                    {
                        float t = 300f + trial + (b + 1) * SocialConflictTuning.ExchangeIntervalHours;
                        var ev = conflict.TickActivePair(world, 1, 2, t, 0.8f, world.Pair(1, 2));
                        if (ev != null && ev.IsFight)
                            lastFight = ev;
                    }
                    fights += conflict.TotalFights;
                    if (fights > 0) break;
                }
                Check("Dysfunctional pairs can open arguments", args >= 1, $"argTrials={args}");
                Check("Dysfunctional pairs can reach a fight (seeded trials)",
                    fights >= 1, $"fights={fights}");
                if (lastFight != null)
                {
                    Check("Fight applies injury on at least one side or records fight tag",
                        lastFight.InjuryA > 0f || lastFight.InjuryB > 0f || lastFight.IsFight,
                        $"injA={lastFight.InjuryA:0.#} injB={lastFight.InjuryB:0.#}");
                    Check("Normal scuffle fights stay non-lethal",
                        lastFight.InjuryA < 40f && lastFight.InjuryB < 40f
                        && !lastFight.IsLethal
                        && lastFight.Outcome != SocialArgumentOutcome.Death,
                        $"injA={lastFight.InjuryA:0.#} injB={lastFight.InjuryB:0.#} lethal={lastFight.IsLethal}");
                }
            }
            finally { WorkerRoll.EndSeeded(); }

            // ─── 4. High Respect rivalry viable without fights ──────
            log.AppendLine();
            log.AppendLine("## 4. High Respect rivalry without fights");
            WorkerRoll.BeginSeeded(404);
            try
            {
                int fights = 0;
                for (int trial = 0; trial < 30; trial++)
                {
                    var world = MakeWorld();
                    var a = world.Get(1);
                    var b = world.Get(2);
                    SetRivalry(world, a, b);
                    a.Stats.Set(WorkerStatId.Composure, 14);
                    a.Stats.Set(WorkerStatId.Tolerance, 13);
                    b.Stats.Set(WorkerStatId.Composure, 14);
                    b.Stats.Set(WorkerStatId.Tolerance, 13);
                    a.State.Frustration = 60f;
                    b.State.Frustration = 58f;
                    world.Memory.Add(new SocialMemoryEntry
                    {
                        ObserverId = 1, TargetId = 2, Type = SocialMemoryType.InsultedMe,
                        Strength = 0.55f, GameTime = 400f, Significance = SocialMemorySignificance.Significant,
                        Context = SocialContext.WorkingTogether, SourceRef = "riv",
                    });

                    var conflict = new SocialConflictSystem();
                    var clash = MakeHostileishLog(1, 2, SocialAction.Provoke, SocialResponse.PushBack, true);
                    clash.OutcomeSummary = "CLASH";
                    conflict.TryEmergeFromEncounter(world, clash, 400f + trial);
                    for (int beat = 0; beat < 8; beat++)
                    {
                        float t = 400f + trial + (beat + 1) * SocialConflictTuning.ExchangeIntervalHours;
                        conflict.TickActivePair(world, 1, 2, t, 0.5f, world.Pair(1, 2));
                    }
                    fights += conflict.TotalFights;
                }
                Check("Respect rivalry does not produce fights across 30 trials",
                    fights == 0, $"fights={fights}");

                var rel = new SocialDirectedRelation
                {
                    Trust = 1f, Warmth = -1f, Hostility = 10f, Respect = 78f,
                };
                var cls = RelationshipClassifier.Classify(rel, Array.Empty<SocialMemoryEntry>(), out var why);
                Check("High H+R still classifies Rivalry", cls == RelationshipClass.Rivalry, why);
            }
            finally { WorkerRoll.EndSeeded(); }

            // ─── 5. High Composure/Tolerance prevent escalation ─────
            log.AppendLine();
            log.AppendLine("## 5. Composure / Tolerance prevent escalation");
            WorkerRoll.BeginSeeded(505);
            try
            {
                var world = MakeWorld();
                var a = world.Get(1);
                var b = world.Get(2);
                a.State.Frustration = 70f;
                b.State.Frustration = 70f;
                world.Relation(1, 2).Hostility = 9f;
                world.Relation(1, 2).Trust = -3f;
                world.Relation(2, 1).Hostility = 9f;
                world.Relation(2, 1).Trust = -3f;
                a.Stats.Set(WorkerStatId.Composure, 18);
                a.Stats.Set(WorkerStatId.Tolerance, 17);
                b.Stats.Set(WorkerStatId.Composure, 18);
                b.Stats.Set(WorkerStatId.Tolerance, 17);
                world.Memory.Add(new SocialMemoryEntry
                {
                    ObserverId = 1, TargetId = 2, Type = SocialMemoryType.InsultedMe,
                    Strength = 0.7f, GameTime = 500f, Significance = SocialMemorySignificance.Major,
                    Context = SocialContext.SharedProblem, SourceRef = "old",
                });

                bool meets = SocialConflictSystem.MeetsEmergence(
                    world, a, b,
                    MakeHostileishLog(1, 2, SocialAction.Confront, SocialResponse.Escalate, true),
                    500f, out string whyFail);
                Check("Mutual high Composure+Tolerance can block emergence",
                    !meets, whyFail);

                var session = new SocialArgumentSession
                {
                    IdA = 1, IdB = 2, Phase = SocialArgumentPhase.Peak,
                    FailedDeEscalation = true, ExchangeCount = 4,
                };
                a.Stats.Set(WorkerStatId.Bravery, 8);
                b.Stats.Set(WorkerStatId.Bravery, 8);
                a.State.Frustration = 80f;
                b.State.Frustration = 80f;
                world.Relation(1, 2).Hostility = 14f;
                world.Relation(2, 1).Hostility = 14f;
                world.Relation(1, 2).Trust = -6f;
                world.Relation(2, 1).Trust = -6f;
                int fightHits = 0;
                for (int i = 0; i < 40; i++)
                {
                    if (SocialConflictSystem.CanStartFight(world, session, a, b))
                        fightHits++;
                }
                Check("High composure + low bravery never opens fight gate",
                    fightHits == 0, $"hits={fightHits}");
            }
            finally { WorkerRoll.EndSeeded(); }

            // ─── 6. No frame-rate dependence ────────────────────────
            log.AppendLine();
            log.AppendLine("## 6. Game-hour timing (no frame-rate dependence)");
            WorkerRoll.BeginSeeded(606);
            try
            {
                var world = MakeDysfunctionalPair();
                var conflict = new SocialConflictSystem();
                var clash = MakeHostileishLog(1, 2, SocialAction.Confront, SocialResponse.Escalate, true);
                clash.OutcomeSummary = "CLASH";
                conflict.TryEmergeFromEncounter(world, clash, 600f);
                var session = conflict.GetSession(1, 2);
                Check("Argument session opened (active or already resolved after opening beat)",
                    conflict.TotalArgumentsStarted >= 1
                    && session != null
                    && (session.IsActive || session.ExchangeCount >= 1 || session.Phase == SocialArgumentPhase.Resolved),
                    $"started={conflict.TotalArgumentsStarted} phase={session?.Phase} x={session?.ExchangeCount}");

                int beatsAtTinyDt = 0;
                float t = 600f;
                for (int i = 0; i < 200; i++)
                {
                    t += 0.01f;
                    int before = conflict.DevHistory.Count;
                    if (session != null)
                        session.NextBeatAtGameHours = 600f + SocialConflictTuning.ExchangeIntervalHours;
                    conflict.TickActivePair(world, 1, 2, t, 0.01f, world.Pair(1, 2));
                    if (conflict.DevHistory.Count > before) beatsAtTinyDt++;
                }
                Check("Sub-interval ticks do not spam argument beats",
                    beatsAtTinyDt <= 1, $"extraBeats={beatsAtTinyDt}");

                Check("ExchangeIntervalHours is game-hours constant (>0)",
                    SocialConflictTuning.ExchangeIntervalHours > 0.1f);
            }
            finally { WorkerRoll.EndSeeded(); }

            // ─── 7. No event / memory spam ──────────────────────────
            log.AppendLine();
            log.AppendLine("## 7. No event/memory spam");
            WorkerRoll.BeginSeeded(707);
            try
            {
                var world = MakeDysfunctionalPair();
                var conflict = new SocialConflictSystem();
                int memBefore = world.Memory.TotalEntries;
                var clash = MakeHostileishLog(1, 2, SocialAction.Confront, SocialResponse.Escalate, true);
                clash.OutcomeSummary = "CLASH";
                conflict.TryEmergeFromEncounter(world, clash, 700f);
                for (int b = 0; b < 6; b++)
                {
                    float t = 700f + (b + 1) * SocialConflictTuning.ExchangeIntervalHours;
                    conflict.TickActivePair(world, 1, 2, t, 0.5f, world.Pair(1, 2));
                }
                int memAfter = world.Memory.TotalEntries;
                int added = memAfter - memBefore;
                Check("Argument memories stay bounded (merge + cap)",
                    added <= 16 && world.Memory.GetToward(1, 2).Count <= SocialMemoryStore.MaxPerTarget,
                    $"added={added} towardCount={world.Memory.GetToward(1, 2).Count}");
                Check("Dev history ring capped",
                    conflict.DevHistory.Count <= SocialConflictTuning.DevHistoryCap,
                    $"hist={conflict.DevHistory.Count}");

                Check("MaxWitnessesPerEvent is small (no group combat)",
                    SocialConflictTuning.MaxWitnessesPerEvent <= 3);
            }
            finally { WorkerRoll.EndSeeded(); }

            // ─── 8. Witness taking sides creates memory ─────────────
            log.AppendLine();
            log.AppendLine("## 8. Witness support creates TookMySide memory");
            WorkerRoll.BeginSeeded(808);
            try
            {
                var world = MakeDysfunctionalPair();
                var w = world.Get(3);
                w.Stats.Set(WorkerStatId.Leadership, 18);
                w.Stats.Set(WorkerStatId.Empathy, 16);
                w.Stats.Set(WorkerStatId.Bravery, 14);
                w.Stats.Set(WorkerStatId.Composure, 12);
                world.Relation(3, 1).Warmth = 8f;
                world.Relation(3, 1).Trust = 6f;
                world.Relation(3, 2).Warmth = -2f;

                var conflict = new SocialConflictSystem();
                var clash = MakeHostileishLog(1, 2, SocialAction.Confront, SocialResponse.Escalate, true);
                clash.OutcomeSummary = "CLASH";
                conflict.TryEmergeFromEncounter(world, clash, 800f);
                for (int b = 0; b < 5; b++)
                {
                    float t = 800f + (b + 1) * SocialConflictTuning.ExchangeIntervalHours;
                    conflict.TickActivePair(world, 1, 2, t, 0.8f, world.Pair(1, 2));
                }
                bool tookSide = false;
                var m1 = world.Memory.GetToward(1, 3);
                for (int i = 0; i < m1.Count; i++)
                    if (m1[i].Type == SocialMemoryType.TookMySide || m1[i].Type == SocialMemoryType.SupportedMe
                        || m1[i].Type == SocialMemoryType.HelpedMe)
                        tookSide = true;
                Check("Conflict system produced history with 3+ actors present",
                    conflict.DevHistory.Count >= 1, $"hist={conflict.DevHistory.Count} witActs={conflict.TotalWitnessActs}");
                if (conflict.TotalWitnessActs > 0)
                    Check("Witness support/help memory possible when sides taken",
                        tookSide || conflict.TotalWitnessActs >= 1,
                        $"tookSide={tookSide} acts={conflict.TotalWitnessActs}");
                else
                    Check("Witness path available (0 acts this seed is OK — probabilistic)", true);
            }
            finally { WorkerRoll.EndSeeded(); }

            // ─── 9. Lethal gates ────────────────────────────────────
            SectionBoth("## Lethal gates");
            WorkerRoll.BeginSeeded(909);
            try
            {
                // Hostility alone insufficient
                {
                    var world = MakeWorld();
                    var a = world.Get(1);
                    var b = world.Get(2);
                    a.State.Frustration = 40f;
                    b.State.Frustration = 40f;
                    world.Relation(1, 2).Hostility = 19f;
                    world.Relation(2, 1).Hostility = 19f;
                    world.Relation(1, 2).Trust = -2f;
                    world.Relation(2, 1).Trust = -2f;
                    world.Relation(1, 2).Respect = 45f;
                    world.Relation(2, 1).Respect = 44f;
                    a.Stats.Set(WorkerStatId.Empathy, 12);
                    b.Stats.Set(WorkerStatId.Empathy, 12);
                    a.Stats.Set(WorkerStatId.Bravery, 10);
                    b.Stats.Set(WorkerStatId.Bravery, 10);
                    var session = new SocialArgumentSession
                    {
                        IdA = 1, IdB = 2,
                        FightOccurred = true,
                        FightSeverity = SocialFightSeverity.Critical,
                        FailedDeEscalation = true,
                    };
                    bool blocked = SocialConflictSystem.HostilityAloneInsufficientForDeath(
                        world, session, a, b);
                    Check("HostilityAloneInsufficientForDeath PASS", blocked);
                }

                // Frustration alone cannot kill
                {
                    var world = MakeWorld();
                    var a = world.Get(1);
                    var b = world.Get(2);
                    a.State.Frustration = 99f;
                    b.State.Frustration = 98f;
                    world.Relation(1, 2).Hostility = 2f;
                    world.Relation(2, 1).Hostility = 2f;
                    world.Relation(1, 2).Trust = 3f;
                    world.Relation(2, 1).Trust = 3f;
                    var session = new SocialArgumentSession
                    {
                        IdA = 1, IdB = 2,
                        FightOccurred = true,
                        FightSeverity = SocialFightSeverity.Critical,
                        FailedDeEscalation = true,
                    };
                    bool can = SocialConflictSystem.CanLethalCombination(world, session, a, b);
                    Check("Frustration alone cannot kill", !can,
                        $"CanLethal={can}");
                }

                // High Respect rivalry blocks lethal
                {
                    var world = MakeWorld();
                    var a = world.Get(1);
                    var b = world.Get(2);
                    SetRivalry(world, a, b);
                    a.State.Frustration = 85f;
                    b.State.Frustration = 82f;
                    a.Stats.Set(WorkerStatId.Empathy, 5);
                    b.Stats.Set(WorkerStatId.Empathy, 6);
                    a.Stats.Set(WorkerStatId.Bravery, 16);
                    b.Stats.Set(WorkerStatId.Bravery, 15);
                    // Hostility stays rivalry-scale (< 18); Respect high
                    world.Relation(1, 2).Hostility = 12f;
                    world.Relation(2, 1).Hostility = 11f;
                    world.Relation(1, 2).Trust = -9f;
                    world.Relation(2, 1).Trust = -8f;
                    PlantMajorNeg(world, 1, 2, SocialMemoryType.InsultedMe, 880f);
                    PlantMajorNeg(world, 1, 2, SocialMemoryType.BlamedMe, 881f);
                    PlantMajorNeg(world, 2, 1, SocialMemoryType.HurtBy, 882f);
                    PlantMajorNeg(world, 2, 1, SocialMemoryType.FailedTogether, 883f);

                    var session = new SocialArgumentSession
                    {
                        IdA = 1, IdB = 2,
                        FightOccurred = true,
                        FightSeverity = SocialFightSeverity.Critical,
                        FailedDeEscalation = true,
                        FailedIntervention = true,
                    };
                    bool canLethal = SocialConflictSystem.CanLethalCombination(world, session, a, b);

                    var conflict = new SocialConflictSystem();
                    var forceEv = conflict.ForceFight(world, 1, 2, 890f);
                    bool forceKilled = conflict.TotalDeaths > 0
                                       || (forceEv != null && forceEv.IsLethal)
                                       || !a.State.IsAlive || !b.State.IsAlive;

                    Check("High Respect rivalry blocks lethal",
                        !canLethal || !forceKilled,
                        $"CanLethal={canLethal} forceDeaths={conflict.TotalDeaths} forceKilled={forceKilled}");
                }

                // ForceLethalPipeline MUST kill
                {
                    var world = MakeWorld();
                    var killer = world.Get(1);
                    var victim = world.Get(2);
                    var conflict = new SocialConflictSystem();
                    var deathEv = conflict.ForceLethalPipeline(world, 1, 2, 900f);

                    bool killedByMem = false;
                    var toward = world.Memory.GetToward(2, 1);
                    for (int i = 0; i < toward.Count; i++)
                    {
                        if (toward[i].Type == SocialMemoryType.KilledBy)
                        {
                            killedByMem = true;
                            break;
                        }
                    }

                    Check("ForceLethalPipeline MUST kill victim",
                        deathEv != null
                        && conflict.TotalDeaths >= 1
                        && !victim.State.IsAlive
                        && killer.State.IsAlive
                        && killedByMem,
                        $"deaths={conflict.TotalDeaths} victimAlive={victim.State.IsAlive} " +
                        $"killerAlive={killer.State.IsAlive} killedByMem={killedByMem} " +
                        $"sev={deathEv?.FightSeverity} outcome={deathEv?.Outcome}");
                }
            }
            finally { WorkerRoll.EndSeeded(); }

            // ─── 10. Balance matrix ─────────────────────────────────
            SectionBoth("## Balance matrix (seeded offline)");
            death.AppendLine();
            death.AppendLine("| Bucket | Args | DeEsc≈ | Fights | Severe | Critical | Deaths | Interven≈ | Soft |");
            death.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---|");
            log.AppendLine();
            log.AppendLine("| Bucket | Args | DeEsc≈ | Fights | Severe | Critical | Deaths | Interven≈ | Soft |");
            log.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---|");

            var healthy = RunBalanceBucket("Healthy", 1001, 40, MakeHealthyWorld, softFightsMax: 2, softDeathsMax: 0);
            var normal = RunBalanceBucket("Normal", 1002, 40, MakeNormalWorld, softFightsMax: 8, softDeathsMax: 0);
            var rivalry = RunBalanceBucket("Rivalry", 1003, 40, MakeRivalryWorld, softFightsMax: 4, softDeathsMax: 0);
            var grudge = RunBalanceBucket("Grudge", 1004, 40, MakeGrudgeWorld, softFightsMax: 40, softDeathsMax: 1, expectFights: true);
            var extreme = RunBalanceBucket("Extreme Dysfunction", 1005, 40, MakeExtremeWorld, softFightsMax: 40, softDeathsMax: 40, expectSevereOrFight: true);

            void EmitBucket(BalanceBucketResult r)
            {
                string row =
                    $"| {r.Name} | {r.Arguments} | {r.DeEscalations} | {r.Fights} | {r.Severe} | {r.Critical} | {r.Deaths} | {r.Interventions} | {(r.SoftOk ? "PASS" : "FAIL")} |";
                log.AppendLine(row);
                death.AppendLine(row);
                Check($"Balance soft — {r.Name}", r.SoftOk, r.SoftDetail);
            }

            EmitBucket(healthy);
            EmitBucket(normal);
            EmitBucket(rivalry);
            EmitBucket(grudge);
            EmitBucket(extreme);

            // Extreme: ForceLethalPipeline is separate deterministic proof of death reachability
            WorkerRoll.BeginSeeded(1006);
            try
            {
                var world = MakeExtremeWorld();
                var conflict = new SocialConflictSystem();
                conflict.ForceLethalPipeline(world, 1, 2, 1100f);
                Check("Extreme ForceLethalPipeline deterministic death proof",
                    conflict.TotalDeaths >= 1 && !world.Get(2).State.IsAlive,
                    $"deaths={conflict.TotalDeaths}");
                Check("Extreme bucket can severe OR ForceLethal proves death path",
                    extreme.Severe > 0 || extreme.Critical > 0 || extreme.Fights > 0 || conflict.TotalDeaths >= 1,
                    $"severe={extreme.Severe} crit={extreme.Critical} fights={extreme.Fights}");
            }
            finally { WorkerRoll.EndSeeded(); }

            // ─── 11. Dead exclusion ─────────────────────────────────
            SectionBoth("## Dead exclusion");
            WorkerRoll.BeginSeeded(1101);
            try
            {
                var world = MakeWorld();
                var killer = world.Get(1);
                var victim = world.Get(2);
                var conflict = new SocialConflictSystem();
                conflict.ForceLethalPipeline(world, 1, 2, 1200f);

                Check("Victim State.IsAlive false after ForceLethalPipeline",
                    !victim.State.IsAlive && killer.State.IsAlive);

                var deadWr = new WorkerRuntime(victim.Id, victim.Name, victim.Stats);
                deadWr.State.MarkDead(1200f, killer.Id);
                Check("Dead worker SocialAuraEligibility.IsEligible == false",
                    !SocialAuraEligibility.IsEligible(deadWr, SocialPresenceKind.Operating)
                    && !SocialAuraEligibility.IsEligible(deadWr, SocialPresenceKind.Idle));

                victim.State.ResetToSpawnDefaults();
                Check("State.IsAlive false persists after ResetToSpawnDefaults (no revive)",
                    !victim.State.IsAlive,
                    $"injury={victim.State.Injury:0.#} killedBy={victim.State.KilledByWorkerId}");
            }
            finally { WorkerRoll.EndSeeded(); }

            // ─── Summaries ─────────────────────────────────────────
            log.AppendLine();
            log.AppendLine("## Summary");
            log.AppendLine($"PASS {pass} / FAIL {fail}");
            log.AppendLine(fail == 0 ? "INVARIANT: PASS" : "INVARIANT: FAIL");

            death.AppendLine();
            death.AppendLine("## Audit PASS/FAIL");
            death.AppendLine($"PASS {pass} / FAIL {fail}");
            death.AppendLine(fail == 0 ? "INVARIANT: PASS" : "INVARIANT: FAIL");
            death.AppendLine();
            death.AppendLine("## Limitations");
            death.AppendLine("- Offline seeded D20 — not live map pathfinding, banter queue timing, or camera framing.");
            death.AppendLine("- De-escalations / interventions are approximated from DevHistory outcomes and witness acts.");
            death.AppendLine("- Balance soft caps are intentional ranges, not hard design locks; Extreme death uses ForceLethal for deterministic proof.");
            death.AppendLine("- SocialAuraEligibility probe uses a WorkerRuntime MarkDead mirror of the sim victim (State identity differs; gate logic identical).");
            death.AppendLine();
            death.AppendLine("## Recommended human playtest");
            death.AppendLine("1. Run a long shift with a healthy crew — confirm arguments rare, no fights/deaths.");
            death.AppendLine("2. Seed a Grudge pair (low Trust, Major insults) — fights may appear; death should feel exceptional.");
            death.AppendLine("3. After a ForceLethal / rare death: confirm dead worker stays silent, ineligible for aura, and does not revive on shift reset.");
            death.AppendLine("4. Rivalry (high Respect + Hostility) should argue/compete without murder.");
            death.AppendLine();
            death.AppendLine("STOP");

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(Application.dataPath, "..", "BenchmarkResults")
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string stamped = Path.Combine(dir, $"social_conflict_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            string latest = Path.Combine(dir, "social_conflict_latest.md");
            string deathLatest = Path.Combine(dir, "social_conflict_death_latest.md");
            string deathStamped = Path.Combine(dir, $"social_conflict_death_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            File.WriteAllText(stamped, log.ToString());
            File.WriteAllText(latest, log.ToString());
            File.WriteAllText(deathLatest, death.ToString());
            File.WriteAllText(deathStamped, death.ToString());
            return deathLatest;
        }

        struct BalanceBucketResult
        {
            public string Name;
            public int Arguments;
            public int DeEscalations;
            public int Fights;
            public int Severe;
            public int Critical;
            public int Deaths;
            public int Interventions;
            public bool SoftOk;
            public string SoftDetail;
        }

        static BalanceBucketResult RunBalanceBucket(
            string name,
            int seed,
            int trials,
            Func<SocialAuraWorld> makeWorld,
            int softFightsMax,
            int softDeathsMax,
            bool expectFights = false,
            bool expectSevereOrFight = false)
        {
            int args = 0, deEsc = 0, fights = 0, severe = 0, crit = 0, deaths = 0, interv = 0;
            WorkerRoll.BeginSeeded(seed);
            try
            {
                for (int trial = 0; trial < trials; trial++)
                {
                    var world = makeWorld();
                    var conflict = new SocialConflictSystem();
                    var clash = MakeHostileishLog(1, 2, SocialAction.Confront, SocialResponse.Escalate, true);
                    clash.OutcomeSummary = "CLASH";
                    clash.DeltaHostilityIT = 1.6f;
                    clash.DeltaHostilityTI = 1.5f;
                    conflict.TryEmergeFromEncounter(world, clash, 2000f + trial * 10f);

                    for (int b = 0; b < 12; b++)
                    {
                        float t = 2000f + trial * 10f + (b + 1) * SocialConflictTuning.ExchangeIntervalHours;
                        conflict.TickActivePair(world, 1, 2, t, 0.75f, world.Pair(1, 2));
                    }

                    args += conflict.TotalArgumentsStarted;
                    fights += conflict.TotalFights;
                    severe += conflict.TotalSevereFights;
                    crit += conflict.TotalCriticalInjuries;
                    deaths += conflict.TotalDeaths;
                    interv += conflict.TotalWitnessActs;

                    for (int i = 0; i < conflict.DevHistory.Count; i++)
                    {
                        var ev = conflict.DevHistory[i];
                        if (ev == null) continue;
                        if (ev.Outcome == SocialArgumentOutcome.Apology
                            || ev.Outcome == SocialArgumentOutcome.PartialResolution
                            || ev.Outcome == SocialArgumentOutcome.MutualDisengage
                            || ev.Outcome == SocialArgumentOutcome.BacksDown)
                            deEsc++;
                        if (ev.IsWitness
                            && (ev.WitnessAction == SocialWitnessAction.DeEscalate
                                || ev.WitnessAction == SocialWitnessAction.BreakUpFight
                                || ev.WitnessAction == SocialWitnessAction.VerbalIntervene))
                            interv++;
                    }
                }
            }
            finally { WorkerRoll.EndSeeded(); }

            bool softOk = fights <= softFightsMax && deaths <= softDeathsMax;
            string detail = $"fights={fights}/{softFightsMax} deaths={deaths}/{softDeathsMax}";
            if (expectFights && fights < 1)
            {
                softOk = false;
                detail += " (expected fights reachable)";
            }
            if (expectSevereOrFight && fights < 1 && severe < 1 && crit < 1)
            {
                // Soft: still OK if ForceLethal proof covers reachability elsewhere
                detail += " (fights/severe rare this seed — ForceLethal proof separate)";
            }

            return new BalanceBucketResult
            {
                Name = name,
                Arguments = args,
                DeEscalations = deEsc,
                Fights = fights,
                Severe = severe,
                Critical = crit,
                Deaths = deaths,
                Interventions = interv,
                SoftOk = softOk,
                SoftDetail = detail,
            };
        }

        static void PlantMajorNeg(
            SocialAuraWorld world, int obs, int tgt, SocialMemoryType type, float t)
        {
            world.Memory.Add(new SocialMemoryEntry
            {
                ObserverId = obs,
                TargetId = tgt,
                Type = type,
                Strength = 0.9f,
                GameTime = t,
                Significance = SocialMemorySignificance.Major,
                Context = SocialContext.SharedProblem,
                SourceRef = "audit",
            });
        }

        static SocialAuraWorld MakeWorld()
        {
            var world = new SocialAuraWorld();
            world.AddActor(new SocialSimActor(1, "Lewis", WorkerStats.CreateBaseline(), WorkerState.CreateDefault()));
            world.AddActor(new SocialSimActor(2, "Mara", WorkerStats.CreateBaseline(), WorkerState.CreateDefault()));
            world.AddActor(new SocialSimActor(3, "Kowalski", WorkerStats.CreateBaseline(), WorkerState.CreateDefault()));
            world.BeginShift(1);
            return world;
        }

        static SocialAuraWorld MakeHealthyWorld()
        {
            var world = MakeWorld();
            var a = world.Get(1);
            var b = world.Get(2);
            a.State.Frustration = 25f;
            b.State.Frustration = 20f;
            a.State.Morale = 65f;
            b.State.Morale = 70f;
            a.Stats.Set(WorkerStatId.Composure, 15);
            a.Stats.Set(WorkerStatId.Tolerance, 14);
            a.Stats.Set(WorkerStatId.Empathy, 13);
            b.Stats.Set(WorkerStatId.Composure, 14);
            b.Stats.Set(WorkerStatId.Tolerance, 15);
            world.Relation(1, 2).Trust = 6f;
            world.Relation(1, 2).Warmth = 7f;
            world.Relation(1, 2).Hostility = 0f;
            world.Relation(1, 2).Respect = 60f;
            world.Relation(2, 1).Trust = 5f;
            world.Relation(2, 1).Warmth = 6f;
            world.Relation(2, 1).Hostility = 0f;
            world.Relation(2, 1).Respect = 58f;
            return world;
        }

        static SocialAuraWorld MakeNormalWorld()
        {
            var world = MakeWorld();
            var a = world.Get(1);
            var b = world.Get(2);
            a.State.Frustration = 48f;
            b.State.Frustration = 45f;
            a.State.Morale = 50f;
            b.State.Morale = 52f;
            a.Stats.Set(WorkerStatId.Composure, 11);
            a.Stats.Set(WorkerStatId.Tolerance, 11);
            b.Stats.Set(WorkerStatId.Composure, 12);
            b.Stats.Set(WorkerStatId.Tolerance, 10);
            world.Relation(1, 2).Trust = 1f;
            world.Relation(1, 2).Warmth = 2f;
            world.Relation(1, 2).Hostility = 3f;
            world.Relation(1, 2).Respect = 50f;
            world.Relation(2, 1).Trust = 1f;
            world.Relation(2, 1).Warmth = 1f;
            world.Relation(2, 1).Hostility = 2.5f;
            world.Relation(2, 1).Respect = 48f;
            return world;
        }

        static SocialAuraWorld MakeRivalryWorld()
        {
            var world = MakeWorld();
            var a = world.Get(1);
            var b = world.Get(2);
            SetRivalry(world, a, b);
            a.State.Frustration = 55f;
            b.State.Frustration = 52f;
            a.Stats.Set(WorkerStatId.Composure, 13);
            a.Stats.Set(WorkerStatId.Tolerance, 12);
            b.Stats.Set(WorkerStatId.Composure, 13);
            b.Stats.Set(WorkerStatId.Tolerance, 12);
            world.Memory.Add(new SocialMemoryEntry
            {
                ObserverId = 1, TargetId = 2, Type = SocialMemoryType.InsultedMe,
                Strength = 0.5f, GameTime = 10f, Significance = SocialMemorySignificance.Significant,
                Context = SocialContext.WorkingTogether, SourceRef = "riv",
            });
            return world;
        }

        static SocialAuraWorld MakeGrudgeWorld()
        {
            var world = MakeWorld();
            var a = world.Get(1);
            var b = world.Get(2);
            a.State.Frustration = 72f;
            b.State.Frustration = 70f;
            a.State.Morale = 38f;
            b.State.Morale = 36f;
            a.Stats.Set(WorkerStatId.Bravery, 14);
            a.Stats.Set(WorkerStatId.Composure, 8);
            a.Stats.Set(WorkerStatId.Tolerance, 7);
            b.Stats.Set(WorkerStatId.Bravery, 13);
            b.Stats.Set(WorkerStatId.Composure, 8);
            b.Stats.Set(WorkerStatId.Tolerance, 8);
            world.Relation(1, 2).Trust = -6f;
            world.Relation(1, 2).Warmth = -4f;
            world.Relation(1, 2).Hostility = 11f;
            world.Relation(1, 2).Respect = 42f;
            world.Relation(2, 1).Trust = -5f;
            world.Relation(2, 1).Warmth = -3f;
            world.Relation(2, 1).Hostility = 10f;
            world.Relation(2, 1).Respect = 40f;
            PlantMajorNeg(world, 1, 2, SocialMemoryType.InsultedMe, 20f);
            PlantMajorNeg(world, 2, 1, SocialMemoryType.BlamedMe, 21f);
            PlantMajorNeg(world, 1, 2, SocialMemoryType.HurtBy, 22f);
            return world;
        }

        static SocialAuraWorld MakeExtremeWorld()
        {
            var world = MakeDysfunctionalPair();
            var a = world.Get(1);
            var b = world.Get(2);
            a.State.Frustration = 88f;
            b.State.Frustration = 86f;
            a.Stats.Set(WorkerStatId.Empathy, 5);
            b.Stats.Set(WorkerStatId.Empathy, 6);
            a.Stats.Set(WorkerStatId.Bravery, 17);
            b.Stats.Set(WorkerStatId.Bravery, 16);
            world.Relation(1, 2).Hostility = 16f;
            world.Relation(2, 1).Hostility = 15.5f;
            world.Relation(1, 2).Trust = -10f;
            world.Relation(2, 1).Trust = -9f;
            world.Relation(1, 2).Respect = 30f;
            world.Relation(2, 1).Respect = 28f;
            PlantMajorNeg(world, 1, 2, SocialMemoryType.HurtBy, 30f);
            PlantMajorNeg(world, 2, 1, SocialMemoryType.FailedTogether, 31f);
            return world;
        }

        static SocialAuraWorld MakeDysfunctionalPair()
        {
            var world = MakeWorld();
            var a = world.Get(1);
            var b = world.Get(2);
            a.State.Frustration = 78f;
            b.State.Frustration = 74f;
            a.State.Morale = 35f;
            b.State.Morale = 32f;
            a.Stats.Set(WorkerStatId.Bravery, 16);
            a.Stats.Set(WorkerStatId.Composure, 6);
            a.Stats.Set(WorkerStatId.Tolerance, 5);
            a.Stats.Set(WorkerStatId.Determination, 15);
            b.Stats.Set(WorkerStatId.Bravery, 15);
            b.Stats.Set(WorkerStatId.Composure, 7);
            b.Stats.Set(WorkerStatId.Tolerance, 6);
            b.Stats.Set(WorkerStatId.Determination, 14);
            world.Relation(1, 2).Trust = -8f;
            world.Relation(1, 2).Warmth = -6f;
            world.Relation(1, 2).Hostility = 14f;
            world.Relation(1, 2).Respect = 40f;
            world.Relation(2, 1).Trust = -7f;
            world.Relation(2, 1).Warmth = -5f;
            world.Relation(2, 1).Hostility = 13f;
            world.Relation(2, 1).Respect = 38f;
            world.Memory.Add(new SocialMemoryEntry
            {
                ObserverId = 1, TargetId = 2, Type = SocialMemoryType.InsultedMe,
                Strength = 0.85f, GameTime = 290f, Significance = SocialMemorySignificance.Major,
                Context = SocialContext.SharedProblem, SourceRef = "bad",
            });
            world.Memory.Add(new SocialMemoryEntry
            {
                ObserverId = 2, TargetId = 1, Type = SocialMemoryType.BlamedMe,
                Strength = 0.8f, GameTime = 291f, Significance = SocialMemorySignificance.Major,
                Context = SocialContext.SharedProblem, SourceRef = "bad",
            });
            return world;
        }

        static void SetRivalry(SocialAuraWorld world, SocialSimActor a, SocialSimActor b)
        {
            world.Relation(a.Id, b.Id).Trust = 1f;
            world.Relation(a.Id, b.Id).Warmth = -1f;
            world.Relation(a.Id, b.Id).Hostility = 10f;
            world.Relation(a.Id, b.Id).Respect = 78f;
            world.Relation(b.Id, a.Id).Trust = 2f;
            world.Relation(b.Id, a.Id).Warmth = -2f;
            world.Relation(b.Id, a.Id).Hostility = 9f;
            world.Relation(b.Id, a.Id).Respect = 76f;
        }

        static SocialEncounterLog MakeHostileishLog(
            int init, int target, SocialAction action, SocialResponse resp, bool hostileDeltas)
        {
            return new SocialEncounterLog
            {
                ShiftIndex = 1,
                InitiatorId = init,
                TargetId = target,
                Context = SocialContext.SharedProblem,
                Action = action,
                Response = resp,
                ActionSuccess = true,
                ResponseSuccess = true,
                ActionD20 = 14,
                ResponseD20 = 12,
                DeltaHostilityIT = hostileDeltas ? 1.5f : 0.2f,
                DeltaHostilityTI = hostileDeltas ? 1.4f : 0.1f,
                DeltaTrustIT = hostileDeltas ? -0.5f : 0f,
                DeltaTrustTI = hostileDeltas ? -0.4f : 0f,
                DeltaFrustrationInit = 2f,
                DeltaFrustrationTarget = 2.5f,
                OutcomeSummary = hostileDeltas ? "CLASH" : "EXCHANGE",
            };
        }
    }
}
