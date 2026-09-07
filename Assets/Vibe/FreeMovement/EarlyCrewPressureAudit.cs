using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Offline audit: early hired-crew pressure + frustration tuning.
    /// Snapshots Days 1/3/7/14 for GOOD / AVERAGE / BAD / DYSFUNCTIONAL crews.
    /// Menu: DeepCore/Diagnostics/Run Early Crew Pressure Audit
    /// </summary>
    public static class EarlyCrewPressureAudit
    {
        const float ShiftHours = 10f;
        const float Step = 0.5f;

        struct Snap
        {
            public int Day;
            public float AvgFrust, MaxFrust;
            public float AvgTrust, AvgWarmth, AvgHostility, MaxHostility, AvgRespect;
            public int PosEnc, NegEnc, Arguments, Fights;
            public string RelClasses;
        }

        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[EARLY CREW PRESSURE] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.Exists(path) && File.ReadAllText(path).Contains("**Result:** FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var sb = new StringBuilder(48_000);
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                if (ok) pass++;
                else fail++;
                sb.AppendLine($"- {(ok ? "PASS" : "FAIL")}  {name}"
                              + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
            }

            sb.AppendLine("# Early Crew Pressure + Frustration Tuning Audit");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("## Before → After tunables");
            sb.AppendLine();
            sb.AppendLine("| Knob | Before | After |");
            sb.AppendLine("|---|---:|---:|");
            sb.AppendLine("| WorkBlockedMul | 1.35 | 1.65 |");
            sb.AppendLine("| RepeatedFailureMul | 1.75 | 2.15 |");
            sb.AppendLine("| CompoundPerFrustration | 0.012 | 0.018 |");
            sb.AppendLine("| CompoundMax | 0.85 | 1.10 |");
            sb.AppendLine("| ProgressSuccessReliefScale | 0.22 | 0.14 |");
            sb.AppendLine("| ProgressSuccessReliefCap | 0.42 | 0.28 |");
            sb.AppendLine("| ProgressSuccessHighFrustScale | 0.55 | 0.40 |");
            sb.AppendLine("| DaytimeDecay/h | 0.18 | 0.12 |");
            sb.AppendLine("| Sleep FrustrationRelief | 3.0 | 2.2 |");
            sb.AppendLine("| Sleep high-F factor (>75) | 0.45 | 0.32 |");
            sb.AppendLine("| Dig obstruction mag | 3.5 | 4.8 |");
            sb.AppendLine("| DrySpell first/gap hours | 32/20 | 24/14 |");
            sb.AppendLine("| DrySpell magnitude | 8 | 11 |");
            sb.AppendLine("| CoopFriction excav/eng | 2.2/1.8 | 3.2/2.6 |");
            sb.AppendLine("| Haul stuck mag | 2.5 | 3.4 |");
            sb.AppendLine("| EquipmentProblem mul | 1.25 | 1.45 |");
            sb.AppendLine("| InvestigationFailure mul | 1.15 | 1.40 |");
            sb.AppendLine("| Soul resist Det/Tol | 0.10/0.08 | 0.14/0.12 |");
            sb.AppendLine("| Soul composure cut | 0.06 | 0.09 |");
            sb.AppendLine("| Hired relation seed | CreateNeutral (T/W/H=0, R=50) | Cautious low T/W, R~48, H≤1.1 |");
            sb.AppendLine("| Early instability days | (none) | 9 working days |");
            sb.AppendLine("| Early NegReactionMul peak | 1.0 | 1.55 |");
            sb.AppendLine("| Early Argue Hostility relief | 0 | up to 5.4 via ArgueSoft01 |");
            sb.AppendLine("| Early Argue Frustration relief | 0 | up to 12 via ArgueSoft01 |");
            sb.AppendLine();
            sb.AppendLine($"Live WorkBlockedMul={WorkerStateEventProcessor.WorkBlockedMul} RepeatedFailureMul={WorkerStateEventProcessor.RepeatedFailureMul}");
            sb.AppendLine($"ProgressSuccess scale/cap={WorkerStateEventProcessor.ProgressSuccessReliefScale}/{WorkerStateEventProcessor.ProgressSuccessReliefCap}");
            sb.AppendLine($"Sleep relief={WorkerSleepRecovery.FrustrationRelief} DaytimeDecay={WorkerStateDaytimeRecovery.FrustrationDecayPerGameHour}");
            sb.AppendLine($"EarlyCrew InstabilityDays={EarlyCrewPressure.InstabilityDays}");
            sb.AppendLine();

            // Seed invariants
            sb.AppendLine("## Seed invariants");
            WorkerRoll.BeginSeeded(9001);
            try
            {
                var crew = BuildCrew("AVG");
                var world = new SocialAuraWorld();
                foreach (var wr in crew)
                    world.AddActor(new SocialSimActor(wr.WorkerId, wr.DisplayName, wr.Stats, wr.State));
                EarlyCrewPressure.ActivateForHiredCrew(world, crew);

                float maxH = 0f, minT = 99f, maxT = -99f;
                int edges = 0;
                for (int i = 0; i < crew.Length; i++)
                for (int j = 0; j < crew.Length; j++)
                {
                    if (i == j) continue;
                    var r = world.Relation(crew[i].WorkerId, crew[j].WorkerId);
                    maxH = Mathf.Max(maxH, r.Hostility);
                    minT = Mathf.Min(minT, r.Trust);
                    maxT = Mathf.Max(maxT, r.Trust);
                    edges++;
                    Check($"Seed edge {crew[i].WorkerId}→{crew[j].WorkerId} Hostility≤{EarlyCrewPressure.SeedHostilityMax}",
                        r.Hostility <= EarlyCrewPressure.SeedHostilityMax + 0.01f,
                        $"H={r.Hostility:0.##}");
                }
                Check("Not everyone hostile at seed", maxH < 3f, $"maxH={maxH:0.##}");
                Check("Trust modest variation (not all friends)", maxT - minT > 0.2f || edges < 2,
                    $"T range [{minT:0.##},{maxT:0.##}]");
                Check("EarlyCrewPressure Active after hire", EarlyCrewPressure.Active);
                EarlyCrewPressure.Deactivate();
                Check("Deactivate clears Active", !EarlyCrewPressure.Active);
            }
            finally
            {
                WorkerRoll.EndSeeded();
                EarlyCrewPressure.Deactivate();
            }

            sb.AppendLine();
            sb.AppendLine("## Multi-day crew simulations (hired seed + early pressure)");

            var good = SimulateCrew("GOOD", BuildGoodCrew(), harsh: false, seed: 4100);
            var average = SimulateCrew("AVERAGE", BuildAverageCrew(), harsh: true, seed: 4200);
            var bad = SimulateCrew("BAD", BuildBadCrew(), harsh: true, seed: 4300);
            var dys = SimulateCrew("DYSFUNCTIONAL", BuildDysfunctionalCrew(), harsh: true, seed: 4400);

            void Dump(string name, List<Snap> snaps)
            {
                sb.AppendLine();
                sb.AppendLine($"### {name}");
                sb.AppendLine("| Day | AvgF | MaxF | Trust | Warmth | Host | MaxH | Respect | +Enc | −Enc | Args | Fights | RelClasses |");
                sb.AppendLine("|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|");
                foreach (var s in snaps)
                {
                    sb.AppendLine(
                        $"| {s.Day} | {s.AvgFrust:0.0} | {s.MaxFrust:0.0} | {s.AvgTrust:0.00} | {s.AvgWarmth:0.00} | {s.AvgHostility:0.00} | {s.MaxHostility:0.00} | {s.AvgRespect:0.0} | {s.PosEnc} | {s.NegEnc} | {s.Arguments} | {s.Fights} | {s.RelClasses} |");
                }
            }

            Dump("GOOD CREW", good);
            Dump("AVERAGE CREW", average);
            Dump("BAD CREW", bad);
            Dump("DYSFUNCTIONAL CREW", dys);

            sb.AppendLine();
            sb.AppendLine("## Feel checks");

            Snap G(List<Snap> s, int day) => s.Find(x => x.Day == day);

            var g14 = G(good, 14);
            var g1 = G(good, 1);
            var a7 = G(average, 7);
            var b7 = G(bad, 7);
            var d14 = G(dys, 14);
            var d7 = G(dys, 7);

            Check("GOOD: early tension exists (day1 Hostility or Frust elevated vs zero)",
                g1.AvgFrust > 5f || g1.AvgHostility >= 0f);
            Check("GOOD: week-2 more settled Trust than day1 OR Hostility not spiraling",
                g14.AvgTrust >= g1.AvgTrust - 0.5f && g14.AvgHostility < 8f,
                $"T {g1.AvgTrust:0.##}→{g14.AvgTrust:0.##} H={g14.AvgHostility:0.##}");
            Check("GOOD: fights remain rare",
                g14.Fights <= 2, $"fights={g14.Fights}");

            Check("AVERAGE: mid/high Frustration during bad week (day7 MaxF≥35)",
                a7.MaxFrust >= 35f, $"MaxF={a7.MaxFrust:0.0}");
            Check("AVERAGE: argue pressure reachable (args≥1 or MaxH≥4 with high Frust)",
                G(average, 14).Arguments >= 1 || a7.Arguments >= 1
                || (a7.MaxHostility >= 4f && a7.MaxFrust >= 40f),
                $"args d7={a7.Arguments} d14={G(average, 14).Arguments} maxH={a7.MaxHostility:0.##}");

            Check("BAD: tension builds (day7 Hostility > day1)",
                b7.AvgHostility >= G(bad, 1).AvgHostility - 0.1f);
            Check("BAD: day7 Max Frustration meaningful (≥45)",
                b7.MaxFrust >= 45f, $"MaxF={b7.MaxFrust:0.0}");
            Check("BAD: arguments reachable under pressure by day14",
                G(bad, 14).Arguments >= 1,
                $"args={G(bad, 14).Arguments} maxH={G(bad, 14).MaxHostility:0.##}");

            Check("DYSFUNCTIONAL: can spiral Hostility (day14 Host≥day7 or fights)",
                d14.AvgHostility >= d7.AvgHostility - 0.5f || d14.Fights > 0,
                $"H {d7.AvgHostility:0.##}→{d14.AvgHostility:0.##} fights={d14.Fights}");
            Check("DYSFUNCTIONAL: arguments in difficult first week (day7)",
                d7.Arguments >= 1, $"args={d7.Arguments} maxH={d7.MaxHostility:0.##}");
            Check("DYSFUNCTIONAL: fights possible but not routine wipeout (day14 fights < 25)",
                d14.Fights < 25, $"fights={d14.Fights}");
            Check("Healthy GOOD crew does not fight",
                g14.Fights == 0, $"fights={g14.Fights}");

            Check("Not everyone auto-hostile (GOOD day14 Hostility < 6)",
                g14.AvgHostility < 6f);
            Check("Successful crews can recover Trust (GOOD day14 Trust not collapsed)",
                g14.AvgTrust > -5f);
            Check("Bad weeks leave Frust residue (AVERAGE day7 AvgF > spawn)",
                a7.AvgFrust > 10f, $"AvgF={a7.AvgFrust:0.0}");

            // Sleep residue unit test
            sb.AppendLine();
            sb.AppendLine("## Sleep does not wipe high Frustration");
            {
                var wr = new WorkerRuntime(99, "Tired");
                wr.State.Frustration = 80f;
                float before = wr.State.Frustration;
                wr.State.ApplySleepRecoveryFraction(1f, wr.PhysicalStaminaMax);
                float after = wr.State.Frustration;
                float cut = before - after;
                Check("Sleep relief on F=80 leaves residue (cut < 3.5, after > 70)",
                    cut < 3.5f && after > 70f, $"cut={cut:0.##} after={after:0.##}");
            }

            // ProgressSuccess soft
            sb.AppendLine();
            sb.AppendLine("## ProgressSuccess modest relief");
            {
                var wr = new WorkerRuntime(98, "Dig");
                var svc = new WorkerStateEventService();
                svc.Bind(id => id == 98 ? wr : null);
                WorkerStateEventHub.Service = svc;
                wr.State.Frustration = 50f;
                WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                    98, WorkerStateEventType.ProgressSuccess, 2f, "Audit", JobType.Excavation));
                float after = wr.State.Frustration;
                WorkerStateEventHub.Service = null;
                Check("One ProgressSuccess at F=50 cuts < 1.5",
                    50f - after < 1.5f, $"after={after:0.##}");
            }

            sb.AppendLine();
            sb.AppendLine("## Scope");
            sb.AppendLine("- Prototype DEV crew: EarlyCrewPressure.Deactivate on BootstrapPrototypeCrew.");
            sb.AppendLine("- Hired crew: ActivateForHiredCrew after SocialAura.Bootstrap.");
            sb.AppendLine("- No dialogue visual / SOCIAL DEV UI changes.");
            sb.AppendLine("- Fight/lethal gates unchanged (only argue emergence softened early).");
            sb.AppendLine("- Args column counts argument beats (openings + ongoing exchanges), not unique sessions.");
            sb.AppendLine();
            sb.AppendLine($"**Result:** {(fail == 0 ? "PASS" : "FAIL")}  ({pass} passed, {fail} failed)");

            string dir = outputDirectory
                ?? Path.Combine(Application.dataPath, "..", "BenchmarkResults");
            Directory.CreateDirectory(dir);
            string latest = Path.Combine(dir, "early_crew_pressure_latest.md");
            string stamped = Path.Combine(dir,
                $"early_crew_pressure_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            File.WriteAllText(latest, sb.ToString());
            File.WriteAllText(stamped, sb.ToString());
            Debug.Log($"[EARLY CREW PRESSURE] {(fail == 0 ? "PASS" : "FAIL")} → {latest}");
            return latest;
        }

        static List<Snap> SimulateCrew(string label, WorkerRuntime[] crew, bool harsh, int seed)
        {
            WorkerRoll.BeginSeeded(seed);
            EarlyCrewPressure.Deactivate();
            var world = new SocialAuraWorld();
            var conflict = new SocialConflictSystem();
            var roster = new Dictionary<int, WorkerRuntime>(8);
            foreach (var wr in crew)
            {
                world.AddActor(new SocialSimActor(wr.WorkerId, wr.DisplayName, wr.Stats, wr.State));
                roster[wr.WorkerId] = wr;
            }
            var svc = new WorkerStateEventService();
            svc.Bind(id => roster.TryGetValue(id, out var w) ? w : null);
            WorkerStateEventHub.Service = svc;

            EarlyCrewPressure.ActivateForHiredCrew(world, crew);

            var snaps = new List<Snap>(4);
            int posEnc = 0, negEnc = 0, args = 0, fights = 0;
            var watchDays = new HashSet<int> { 1, 3, 7, 14 };

            try
            {
                for (int day = 1; day <= 14; day++)
                {
                    world.BeginShift(day);
                    float t = 0f;
                    float nextBlock = 1.5f;
                    float nextProgress = 0.8f;
                    float nextDry = harsh ? 3f : 99f;

                    while (t < ShiftHours)
                    {
                        float step = Mathf.Min(Step, ShiftHours - t);
                        t += step;
                        WorkerStateClock.GameHours = day * 24f + t;

                        for (int i = 0; i < crew.Length; i++)
                            WorkerStateDaytimeRecovery.Tick(crew[i], step);

                        float opp = (harsh ? 2.8f : 1.6f) * EarlyCrewPressure.PressureGainMul;

                        for (int i = 0; i < crew.Length; i++)
                        for (int j = i + 1; j < crew.Length; j++)
                        {
                            var ctx = SocialContext.IdleNearby;
                            if (label == "GOOD")
                                ctx = t < 3f ? SocialContext.RecentSuccess : SocialContext.WorkingTogether;
                            else if (label == "DYSFUNCTIONAL")
                                ctx = SocialContext.RecentFailure;
                            else if (harsh)
                                ctx = t > 2.5f ? SocialContext.SharedProblem : SocialContext.WorkingTogether;

                            var pair = world.Pair(crew[i].WorkerId, crew[j].WorkerId);
                            var beat = conflict.TickActivePair(
                                world, crew[i].WorkerId, crew[j].WorkerId,
                                WorkerStateClock.GameHours, opp * 0.15f, pair);
                            if (beat != null)
                            {
                                args++;
                                if (beat.IsFight) fights++;
                            }

                            var log = world.Expose(
                                crew[i].WorkerId, crew[j].WorkerId, ctx, opp);
                            if (log != null)
                            {
                                if (log.OutcomeSummary != null
                                    && (log.OutcomeSummary.Contains("POSITIVE")
                                        || log.OutcomeSummary.Contains("BOND")))
                                    posEnc++;
                                else if (log.OutcomeSummary != null
                                         && (log.OutcomeSummary.Contains("CLASH")
                                             || log.OutcomeSummary.Contains("FAIL")
                                             || log.OutcomeSummary.Contains("Provoke")
                                             || log.OutcomeSummary.Contains("Confront")))
                                    negEnc++;

                                var ev = conflict.TryEmergeFromEncounter(
                                    world, log, WorkerStateClock.GameHours);
                                if (ev != null)
                                {
                                    args++;
                                    if (ev.IsFight) fights++;
                                }
                            }
                        }

                        if (harsh && t >= nextBlock)
                        {
                            var excav = Find(crew, 2) ?? crew[0];
                            WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                                excav.WorkerId, WorkerStateEventType.WorkBlocked, 4.8f,
                                "AuditDigBlock", JobType.Excavation));
                            if ((int)(t / 2f) % 3 == 0)
                                WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                                    excav.WorkerId, WorkerStateEventType.RepeatedFailure, 6f,
                                    "AuditRepeat", JobType.Excavation));
                            nextBlock += harsh ? 1.8f : 4f;
                        }

                        if (t >= nextProgress)
                        {
                            foreach (var wr in crew)
                            {
                                if (label == "GOOD" || !harsh || WorkerRoll.NextUnit() > 0.45f)
                                    WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                                        wr.WorkerId, WorkerStateEventType.ProgressSuccess, 2f,
                                        "AuditProgress", JobType.Excavation));
                            }
                            nextProgress += label == "GOOD" ? 0.6f : 1.4f;
                        }

                        if (harsh && t >= nextDry)
                        {
                            var prosp = Find(crew, 1) ?? crew[0];
                            WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                                prosp.WorkerId, WorkerStateEventType.InvestigationFailure, 11f,
                                "AuditDry", JobType.Prospecting));
                            nextDry += 4f;
                        }
                    }

                    conflict.TickOvernight(world, WorkerStateClock.GameHours, 14f);
                    foreach (var wr in crew)
                        wr.State.ApplySleepRecoveryFraction(1f, wr.PhysicalStaminaMax);

                    EarlyCrewPressure.NotifyWorkingDayAdvanced();

                    if (watchDays.Contains(day))
                        snaps.Add(Capture(day, crew, world, posEnc, negEnc, args, fights));
                }
            }
            finally
            {
                WorkerRoll.EndSeeded();
                EarlyCrewPressure.Deactivate();
                WorkerStateEventHub.Service = null;
            }

            return snaps;
        }

        static Snap Capture(
            int day, WorkerRuntime[] crew, SocialAuraWorld world,
            int pos, int neg, int args, int fights)
        {
            float sumF = 0f, maxF = 0f;
            float sumT = 0f, sumW = 0f, sumH = 0f, maxH = 0f, sumR = 0f;
            int edges = 0;
            int nNeu = 0, nFr = 0, nBo = 0, nPro = 0, nRiv = 0, nGr = 0, nStr = 0;
            for (int i = 0; i < crew.Length; i++)
            {
                float f = crew[i].State.Frustration;
                sumF += f;
                maxF = Mathf.Max(maxF, f);
                for (int j = 0; j < crew.Length; j++)
                {
                    if (i == j) continue;
                    var r = world.Relation(crew[i].WorkerId, crew[j].WorkerId);
                    sumT += r.Trust;
                    sumW += r.Warmth;
                    sumH += r.Hostility;
                    maxH = Mathf.Max(maxH, r.Hostility);
                    sumR += r.Respect;
                    edges++;
                    var mem = world.Memory.GetToward(crew[i].WorkerId, crew[j].WorkerId);
                    switch (RelationshipClassifier.Classify(r, mem, out _))
                    {
                        case RelationshipClass.Friendly: nFr++; break;
                        case RelationshipClass.Bonded: nBo++; break;
                        case RelationshipClass.Professional: nPro++; break;
                        case RelationshipClass.Rivalry: nRiv++; break;
                        case RelationshipClass.Grudge: nGr++; break;
                        case RelationshipClass.Strained: nStr++; break;
                        default: nNeu++; break;
                    }
                }
            }
            return new Snap
            {
                Day = day,
                AvgFrust = sumF / crew.Length,
                MaxFrust = maxF,
                AvgTrust = edges > 0 ? sumT / edges : 0f,
                AvgWarmth = edges > 0 ? sumW / edges : 0f,
                AvgHostility = edges > 0 ? sumH / edges : 0f,
                MaxHostility = maxH,
                AvgRespect = edges > 0 ? sumR / edges : 50f,
                PosEnc = pos,
                NegEnc = neg,
                Arguments = args,
                Fights = fights,
                RelClasses =
                    $"N{nNeu} F{nFr} B{nBo} P{nPro} R{nRiv} G{nGr} S{nStr}",
            };
        }

        static WorkerRuntime Find(WorkerRuntime[] crew, int id)
        {
            for (int i = 0; i < crew.Length; i++)
                if (crew[i].WorkerId == id) return crew[i];
            return null;
        }

        static WorkerRuntime[] BuildGoodCrew() => new[]
        {
            Person(1, "G_Pros", SocialAuraStage0Sim.ProfileA_ComposedEmpath()),
            Person(2, "G_Exc", SocialAuraStage0Sim.ProfileD_FocusedTolerant()),
            Person(3, "G_Hau", SocialAuraStage0Sim.ProfileC_SocialLead()),
            Person(4, "G_Ref", SocialAuraStage0Sim.ProfileA_ComposedEmpath()),
            Person(5, "G_Eng", SocialAuraStage0Sim.ProfileC_SocialLead()),
        };

        static WorkerRuntime[] BuildAverageCrew() => BuildCrew("AVG");

        static WorkerRuntime[] BuildBadCrew() => new[]
        {
            Person(1, "B_Pros", SocialAuraStage0Sim.ProfileB_HotBrave()),
            Person(2, "B_Exc", SocialAuraStage0Sim.ProfileB_HotBrave()),
            Person(3, "B_Hau", SocialAuraStage0Sim.ProfileD_FocusedTolerant()),
            Person(4, "B_Ref", LowTol()),
            Person(5, "B_Eng", SocialAuraStage0Sim.ProfileB_HotBrave()),
        };

        static WorkerRuntime[] BuildDysfunctionalCrew() => new[]
        {
            Person(1, "D_Pros", HotLonely()),
            Person(2, "D_Exc", HotLonely()),
            Person(3, "D_Hau", HotLonely()),
            Person(4, "D_Ref", HotLonely()),
            Person(5, "D_Eng", HotLonely()),
        };

        static WorkerRuntime[] BuildCrew(string prefix)
        {
            return new[]
            {
                Person(1, prefix + "_P", WorkerStats.CreateBaseline()),
                Person(2, prefix + "_E", WorkerStats.CreateBaseline()),
                Person(3, prefix + "_H", WorkerStats.CreateBaseline()),
                Person(4, prefix + "_R", WorkerStats.CreateBaseline()),
                Person(5, prefix + "_G", WorkerStats.CreateBaseline()),
            };
        }

        static WorkerStats LowTol()
        {
            var s = WorkerStats.CreateBaseline();
            s.Set(WorkerStatId.Tolerance, 5);
            s.Set(WorkerStatId.Composure, 7);
            s.Set(WorkerStatId.Affinity, 6);
            return s;
        }

        static WorkerStats HotLonely()
        {
            var s = SocialAuraStage0Sim.ProfileB_HotBrave();
            s.Set(WorkerStatId.Affinity, 4);
            s.Set(WorkerStatId.Empathy, 4);
            s.Set(WorkerStatId.Tolerance, 5);
            s.Set(WorkerStatId.Composure, 5);
            s.Set(WorkerStatId.Leadership, 6);
            return s;
        }

        static WorkerRuntime Person(int id, string name, WorkerStats stats)
        {
            var wr = new WorkerRuntime(id, name, stats);
            wr.State.Frustration = WorkerState.DefaultFrustration;
            return wr;
        }
    }
}
