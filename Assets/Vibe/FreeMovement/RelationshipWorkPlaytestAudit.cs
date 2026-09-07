using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Relationship Work Playtest audit — tracker + DEV presets only.
    /// Does not change cooperation formulas or Aura. Batch: RelationshipWorkPlaytestAudit.RunFromEditor
    /// </summary>
    public static class RelationshipWorkPlaytestAudit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[COOP PLAYTEST] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.ReadAllText(path).Contains("INVARIANT: FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var log = new StringBuilder(16_000);
            int pass = 0, fail = 0;

            void Check(string name, bool ok, string detail = "")
            {
                if (ok)
                {
                    pass++;
                    log.AppendLine($"PASS | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
                }
                else
                {
                    fail++;
                    log.AppendLine($"FAIL | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
                }
            }

            log.AppendLine("# Relationship Work Playtest Audit");
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine();
            log.AppendLine("Scope: DEV tracker + SET TEST RELATIONSHIP presets. No sim formula changes.");
            log.AppendLine();

            var tracker = new RelationshipWorkPlaytestTracker();
            tracker.Reset(1);

            // ——— Tracking ———
            log.AppendLine("## 1. Day tracking");
            var coop = ExcavatorEngineerCooperation.AuditEvaluateAxes(9f, 78f, 0.5f);
            tracker.NotifyDispatch(1, coop);
            tracker.NotifyRepairBegin(1, coop);
            tracker.NotifyRepairComplete(1, CooperationWorkConsequence.CoopBenefit);
            Check("Day1 records collaboration + completion + synergy",
                tracker.Current.Collaborations == 1
                && tracker.Current.Completions == 1
                && tracker.Current.CoopSynergy == 1,
                tracker.FormatCompactDay(tracker.Current));

            var strained = ExcavatorEngineerCooperation.AuditEvaluateAxes(-6f, 32f, 8f);
            tracker.NotifyDispatch(1, strained);
            tracker.NotifyRepairInterrupted(1);
            Check("Interruption counted; quality min/max span",
                tracker.Current.Interruptions == 1
                && tracker.Current.MinQuality <= tracker.Current.MaxQuality
                && tracker.Current.QualitySamples >= 2,
                $"min={tracker.Current.MinQuality:0.00} max={tracker.Current.MaxQuality:0.00}");

            tracker.NotifyDispatch(2, coop);
            tracker.NotifyRepairComplete(2, CooperationWorkConsequence.CoopSetback);
            Check("Day rollover archives prior day",
                tracker.Days.Count == 1 && tracker.Days[0].DayIndex == 1
                && tracker.Current.DayIndex == 2
                && tracker.Current.CoopFriction == 1,
                $"archived={tracker.Days.Count} curFric={tracker.Current.CoopFriction}");

            for (int d = 3; d <= 7; d++)
                tracker.NotifyDispatch(d, coop);
            Check("Rolling window keeps ≤5 archived days",
                tracker.Days.Count <= RelationshipWorkPlaytestTracker.RollingDays,
                $"archived={tracker.Days.Count}");

            // ——— Presets ———
            log.AppendLine();
            log.AppendLine("## 2. SET TEST RELATIONSHIP presets");
            var world = new SocialAuraWorld();
            world.AddActor(new SocialSimActor(2, "Mara", WorkerStats.CreateBaseline(), WorkerState.CreateDefault()));
            world.AddActor(new SocialSimActor(5, "Viktor", WorkerStats.CreateBaseline(), WorkerState.CreateDefault()));
            // Pollute with Bonded-ish axes + memories that would confuse Neutral
            world.Relation(2, 5).Warmth = 12f;
            world.Relation(2, 5).Trust = 10f;
            world.Memory.AuditForceAdd(new SocialMemoryEntry
            {
                ObserverId = 2, TargetId = 5, Type = SocialMemoryType.SharedHardship,
                Strength = 0.9f, GameTime = 1f, Major = true,
                Context = SocialContext.SharedProblem
            });

            var t = new RelationshipWorkPlaytestTracker();
            t.ApplyPreset(RelationshipWorkTestPreset.StrongProfessional, world, world.Memory, 2, 5);
            var rel = world.Relation(2, 5);
            var cls = RelationshipClassifier.Classify(rel, world.Memory.GetToward(2, 5), out _);
            Check("Strong Professional → Professional class",
                cls == RelationshipClass.Professional,
                $"{cls} {RelationshipClassifier.FormatAxes(rel)}");

            t.ApplyPreset(RelationshipWorkTestPreset.Rivalry, world, world.Memory, 2, 5);
            cls = RelationshipClassifier.Classify(
                world.Relation(2, 5), world.Memory.GetToward(2, 5), out _);
            Check("Rivalry preset → Rivalry class",
                cls == RelationshipClass.Rivalry, cls.ToString());

            t.ApplyPreset(RelationshipWorkTestPreset.Strained, world, world.Memory, 2, 5);
            cls = RelationshipClassifier.Classify(
                world.Relation(2, 5), world.Memory.GetToward(2, 5), out _);
            Check("Strained preset → Strained (not Grudge)",
                cls == RelationshipClass.Strained, cls.ToString());

            t.ApplyPreset(RelationshipWorkTestPreset.Neutral, world, world.Memory, 2, 5);
            cls = RelationshipClassifier.Classify(
                world.Relation(2, 5), world.Memory.GetToward(2, 5), out _);
            Check("Neutral preset clears pair memories + Neutral class",
                cls == RelationshipClass.Neutral
                && world.Memory.GetToward(2, 5).Count == 0
                && world.Memory.GetToward(5, 2).Count == 0,
                cls.ToString());

            // Cycle order
            t.ApplyPreset(RelationshipWorkTestPreset.Neutral, world, world.Memory, 2, 5);
            var p1 = t.CycleAndApply(world, world.Memory, 2, 5);
            var p2 = t.CycleAndApply(world, world.Memory, 2, 5);
            var p3 = t.CycleAndApply(world, world.Memory, 2, 5);
            var p4 = t.CycleAndApply(world, world.Memory, 2, 5);
            Check("Cycle Neutral→Professional→Rivalry→Strained→Neutral",
                p1 == RelationshipWorkTestPreset.StrongProfessional
                && p2 == RelationshipWorkTestPreset.Rivalry
                && p3 == RelationshipWorkTestPreset.Strained
                && p4 == RelationshipWorkTestPreset.Neutral,
                $"{p1}→{p2}→{p3}→{p4}");

            // Presets change coop Q in expected direction
            t.ApplyPreset(RelationshipWorkTestPreset.StrongProfessional, world, world.Memory, 2, 5);
            var qPro = ExcavatorEngineerCooperation.Evaluate(
                new WorkerRuntime(2, "Mara"), new WorkerRuntime(5, "Viktor"), world);
            t.ApplyPreset(RelationshipWorkTestPreset.Rivalry, world, world.Memory, 2, 5);
            var qRiv = ExcavatorEngineerCooperation.Evaluate(
                new WorkerRuntime(2, "Mara"), new WorkerRuntime(5, "Viktor"), world);
            t.ApplyPreset(RelationshipWorkTestPreset.Neutral, world, world.Memory, 2, 5);
            var qNeu = ExcavatorEngineerCooperation.Evaluate(
                new WorkerRuntime(2, "Mara"), new WorkerRuntime(5, "Viktor"), world);
            t.ApplyPreset(RelationshipWorkTestPreset.Strained, world, world.Memory, 2, 5);
            var qStr = ExcavatorEngineerCooperation.Evaluate(
                new WorkerRuntime(2, "Mara"), new WorkerRuntime(5, "Viktor"), world);
            Check("Preset Strong Professional Q > Strained Q",
                qPro.Quality > qStr.Quality + 0.1f,
                $"pro={qPro.Quality:0.00} strained={qStr.Quality:0.00}");
            Check("All four presets produce distinct CooperationQuality",
                DistinctEnough(qPro.Quality, qRiv.Quality, qNeu.Quality, qStr.Quality),
                $"P={qPro.Quality:0.00} R={qRiv.Quality:0.00} N={qNeu.Quality:0.00} S={qStr.Quality:0.00}");
            Check("Ordering: Professional > Rivalry > Strained",
                qPro.Quality > qRiv.Quality && qRiv.Quality > qStr.Quality,
                $"P={qPro.Quality:0.00} R={qRiv.Quality:0.00} S={qStr.Quality:0.00}");
            Check("Rivalry still competent (Q≥0.45) despite high Hostility",
                qRiv.Quality >= 0.45f, $"Q={qRiv.Quality:0.00}");
            Check("All preset dispatch/repair muls stay in bounds",
                InMulBounds(qPro) && InMulBounds(qRiv) && InMulBounds(qNeu) && InMulBounds(qStr),
                $"P spd={qPro.DispatchSpeedMul:0.00} dur={qPro.RepairDurationMul:0.00}");

            // Contamination: preset only touches named pair
            log.AppendLine();
            log.AppendLine("## 2b. DEV preset isolation");
            world.AddActor(new SocialSimActor(1, "Lewis", WorkerStats.CreateBaseline(), WorkerState.CreateDefault()));
            world.Relation(1, 2).Trust = 7f;
            world.Relation(1, 2).Respect = 66f;
            world.Relation(1, 2).Warmth = 3f;
            float lewisT = world.Relation(1, 2).Trust;
            float lewisR = world.Relation(1, 2).Respect;
            world.Memory.AuditForceAdd(new SocialMemoryEntry
            {
                ObserverId = 1, TargetId = 2, Type = SocialMemoryType.HelpedMe,
                Strength = 0.8f, GameTime = 10f, Major = true,
                Context = SocialContext.WorkingTogether
            });
            int lewisMem = world.Memory.GetToward(1, 2).Count;
            t.ApplyPreset(RelationshipWorkTestPreset.Strained, world, world.Memory, 2, 5);
            Check("Preset Mara↔Viktor does not mutate Lewis→Mara axes",
                Mathf.Abs(world.Relation(1, 2).Trust - lewisT) < 0.001f
                && Mathf.Abs(world.Relation(1, 2).Respect - lewisR) < 0.001f,
                RelationshipClassifier.FormatAxes(world.Relation(1, 2)));
            Check("Preset ClearPair does not wipe unrelated pair memories",
                world.Memory.GetToward(1, 2).Count == lewisMem,
                $"count={world.Memory.GetToward(1, 2).Count}");
            Check("ApplyPreset is DEV-only API (not invoked by Evaluate)",
                typeof(ExcavatorEngineerCooperation).GetMethod("ApplyPreset") == null);

            // ——— Invariants unchanged ———
            log.AppendLine();
            log.AppendLine("## 3. Sim invariants unchanged");
            Check("Dispatch mul bounds unchanged",
                Mathf.Abs(ExcavatorEngineerCooperation.DispatchMulMin - 0.88f) < 0.001f
                && Mathf.Abs(ExcavatorEngineerCooperation.DispatchMulMax - 1.12f) < 0.001f);
            Check("Repair dur mul bounds unchanged",
                Mathf.Abs(ExcavatorEngineerCooperation.RepairDurMulMin - 0.88f) < 0.001f
                && Mathf.Abs(ExcavatorEngineerCooperation.RepairDurMulMax - 1.15f) < 0.001f);
            Check("PressureTrigger still 1.05",
                Mathf.Abs(SocialAuraTuning.PressureTrigger - 1.05f) < 0.001f);
            Check("RepairSeconds still 2.6",
                Mathf.Abs(EngineerPerson.RepairSeconds - 2.6f) < 0.001f);
            Check("Memory MaxPerTarget still 12", SocialMemoryStore.MaxPerTarget == 12);

            log.AppendLine();
            log.AppendLine("## Summary");
            log.AppendLine($"PASS {pass} / FAIL {fail}");
            log.AppendLine(fail == 0 ? "INVARIANT: PASS" : "INVARIANT: FAIL");
            log.AppendLine();
            log.AppendLine("## Files");
            log.AppendLine("- Assets/Vibe/FreeMovement/RelationshipWorkPlaytestTracker.cs");
            log.AppendLine("- Assets/Vibe/FreeMovement/RelationshipWorkPlaytestAudit.cs (this audit)");

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(Application.dataPath, "..", "BenchmarkResults")
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string stamped = Path.Combine(dir, $"coop_playtest_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            string latest = Path.Combine(dir, "coop_playtest_latest.md");
            File.WriteAllText(stamped, log.ToString());
            File.WriteAllText(latest, log.ToString());
            return latest;
        }

        static bool DistinctEnough(float a, float b, float c, float d)
        {
            float[] v = { a, b, c, d };
            for (int i = 0; i < v.Length; i++)
            for (int j = i + 1; j < v.Length; j++)
                if (Mathf.Abs(v[i] - v[j]) < 0.03f) return false;
            return true;
        }

        static bool InMulBounds(CooperationAssessment c) =>
            c.DispatchSpeedMul >= ExcavatorEngineerCooperation.DispatchMulMin - 0.001f
            && c.DispatchSpeedMul <= ExcavatorEngineerCooperation.DispatchMulMax + 0.001f
            && c.RepairDurationMul >= ExcavatorEngineerCooperation.RepairDurMulMin - 0.001f
            && c.RepairDurationMul <= ExcavatorEngineerCooperation.RepairDurMulMax + 0.001f;
    }
}
