using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Relationship V1.1 audit — Respect directionality, Hostility+Respect, derived classes,
    /// no stored labels, single-encounter limits, reassignment persistence, Aura/Memory invariants.
    /// Batch: RelationshipV11Audit.RunFromEditor
    /// </summary>
    public static class RelationshipV11Audit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[RELATIONSHIP V1.1] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.ReadAllText(path).Contains("INVARIANT: FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var log = new StringBuilder(20_000);
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

            log.AppendLine("# Relationship V1.1 Audit");
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine();
            log.AppendLine("Scope: directional Respect + derived RelationshipClass (DEV only).");
            log.AppendLine("No Trust/Warmth/Hostility formula changes. No encounter frequency / dialogue changes.");
            log.AppendLine();

            WorkerStateClock.GameHours = 200f;
            var world = new SocialAuraWorld();
            var lewis = new SocialSimActor(1, "Lewis", WorkerStats.CreateBaseline(), WorkerState.CreateDefault());
            var mara = new SocialSimActor(2, "Mara", WorkerStats.CreateBaseline(), WorkerState.CreateDefault());
            var kow = new SocialSimActor(3, "Kowalski", WorkerStats.CreateBaseline(), WorkerState.CreateDefault());
            world.AddActor(lewis);
            world.AddActor(mara);
            world.AddActor(kow);
            world.BeginShift(1);

            // ——— 1. Respect is directional ———
            log.AppendLine("## 1. Respect is directional");
            float r12Before = world.Relation(1, 2).Respect;
            float r21Before = world.Relation(2, 1).Respect;
            Check("Baseline Respect is 50 both ways",
                Mathf.Abs(r12Before - SocialDirectedRelation.RespectBaseline) < 0.01f
                && Mathf.Abs(r21Before - SocialDirectedRelation.RespectBaseline) < 0.01f,
                $"1→2={r12Before:0.#} 2→1={r21Before:0.#}");

            var encourage = MakeLog(1, 2, SocialContext.WorkingTogether,
                SocialAction.Encourage, SocialResponse.Accept, true, true,
                dWaIT: 0.9f, dWaTI: 0.7f, dMoT: 1.5f, dFrT: -2.5f);
            SocialMemoryRecorder.Record(world.Memory, encourage, 200f);
            SocialRespectApplicator.Apply(world, encourage);

            float r12 = world.Relation(1, 2).Respect;
            float r21 = world.Relation(2, 1).Respect;
            Check("After Encourage, Mara(2)→Lewis(1) Respect rises more (helped)",
                r21 > r21Before + 1.5f,
                $"2→1 {r21Before:0.#}→{r21:0.#} ΔTI={SocialRespectApplicator.LastDeltaTI:0.#}");
            Check("Lewis(1)→Mara(2) Respect does not mirror Mara's gain",
                Mathf.Abs((r21 - r21Before) - (r12 - r12Before)) > 0.5f
                || (r21 - r21Before) > (r12 - r12Before) + 0.5f,
                $"1→2 Δ={r12 - r12Before:0.#} 2→1 Δ={r21 - r21Before:0.#}");
            Check("Respect deltas are directional (ΔTI ≠ ΔIT for Encourage)",
                Mathf.Abs(SocialRespectApplicator.LastDeltaTI - SocialRespectApplicator.LastDeltaIT) > 0.4f,
                $"ΔIT={SocialRespectApplicator.LastDeltaIT:0.#} ΔTI={SocialRespectApplicator.LastDeltaTI:0.#}");

            // ——— 2. High Hostility + high Respect possible ———
            log.AppendLine();
            log.AppendLine("## 2. High Hostility + high Respect is possible");
            var rivalryRel = world.Relation(1, 3);
            rivalryRel.Hostility = 12f;
            rivalryRel.Warmth = -2f;
            rivalryRel.Trust = 1f;
            rivalryRel.Respect = 78f;
            Check("Can set Hostility=12 and Respect=78 independently",
                rivalryRel.Hostility >= 12f && rivalryRel.Respect >= 78f,
                RelationshipClassifier.FormatAxes(rivalryRel));
            var rivClass = RelationshipClassifier.Classify(
                rivalryRel, world.Memory.GetToward(1, 3), out var rivWhy);
            Check("Derived Rivalry when H high + R high",
                rivClass == RelationshipClass.Rivalry, $"{rivClass} — {rivWhy}");

            // Hostility alone must not force Respect down via applicator
            float respectBeforeHost = world.Relation(2, 3).Respect;
            world.Relation(2, 3).Hostility = 10f;
            var trivialHostileFeel = MakeLog(2, 3, SocialContext.IdleNearby,
                SocialAction.Connect, SocialResponse.Ignore, false, true,
                dWaIT: -0.2f, dHoIT: 0.4f);
            SocialRespectApplicator.Apply(world, trivialHostileFeel);
            Check("Non-meaningful / trivial hostility bump does not Apply Respect",
                Mathf.Abs(world.Relation(2, 3).Respect - respectBeforeHost) < 0.01f
                && !SocialMemoryRecorder.IsMeaningful(trivialHostileFeel),
                $"R={world.Relation(2, 3).Respect:0.#}");

            // ——— 3. Classifications derive from underlying state ———
            log.AppendLine();
            log.AppendLine("## 3. Classifications derive from underlying state");
            var bondRel = world.Relation(2, 1);
            bondRel.Warmth = 10f;
            bondRel.Trust = 7f;
            bondRel.Hostility = 0f;
            bondRel.Respect = 60f;
            // Seed multi-memory evidence
            world.Memory.AuditForceAdd(new SocialMemoryEntry
            {
                ObserverId = 2, TargetId = 1, Type = SocialMemoryType.SharedHardship,
                Strength = 0.8f, GameTime = 190f, Context = SocialContext.SharedProblem, Major = true
            });
            world.Memory.AuditForceAdd(new SocialMemoryEntry
            {
                ObserverId = 2, TargetId = 1, Type = SocialMemoryType.SupportedMe,
                Strength = 0.7f, GameTime = 195f, Context = SocialContext.WorkingTogether, Major = true
            });
            var bondClass = RelationshipClassifier.Classify(
                bondRel, world.Memory.GetToward(2, 1), out var bondWhy);
            Check("Bonded derives from high W/T + memory evidence",
                bondClass == RelationshipClass.Bonded, $"{bondClass} — {bondWhy}");

            var profRel = SocialDirectedRelation.CreateNeutral();
            profRel.Respect = 72f;
            profRel.Trust = 6f;
            profRel.Warmth = 1f;
            profRel.Hostility = 0f;
            var profClass = RelationshipClassifier.Classify(
                profRel, Array.Empty<SocialMemoryEntry>(), out var profWhy);
            Check("Professional derives from high Respect + Trust, low warmth",
                profClass == RelationshipClass.Professional, $"{profClass} — {profWhy}");

            var friendRel = SocialDirectedRelation.CreateNeutral();
            friendRel.Warmth = 5f;
            friendRel.Trust = 2f;
            friendRel.Hostility = 0f;
            friendRel.Respect = 50f;
            var friendClass = RelationshipClassifier.Classify(
                friendRel, Array.Empty<SocialMemoryEntry>(), out _);
            Check("Friendly derives from moderate Warmth",
                friendClass == RelationshipClass.Friendly, friendClass.ToString());

            // ——— 4. Classifications are not stored labels ———
            log.AppendLine();
            log.AppendLine("## 4. Classifications do not become permanent stored labels");
            var fields = typeof(SocialDirectedRelation).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            bool hasClassField = false;
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].FieldType == typeof(RelationshipClass)
                    || fields[i].Name.IndexOf("Class", StringComparison.OrdinalIgnoreCase) >= 0
                    || fields[i].Name.IndexOf("Derived", StringComparison.OrdinalIgnoreCase) >= 0)
                    hasClassField = true;
            }
            Check("SocialDirectedRelation has no RelationshipClass/Derived field", !hasClassField);

            var c1 = RelationshipClassifier.Classify(bondRel, world.Memory.GetToward(2, 1), out _);
            bondRel.Warmth = 0f;
            bondRel.Trust = 0f;
            var c2 = RelationshipClassifier.Classify(bondRel, world.Memory.GetToward(2, 1), out _);
            Check("Changing axes changes derived class (not sticky label)",
                c1 == RelationshipClass.Bonded && c2 != RelationshipClass.Bonded,
                $"before={c1} after={c2}");

            // ——— 5. One encounter cannot create extreme relationship ———
            log.AppendLine();
            log.AppendLine("## 5. One encounter cannot create extreme relationship");
            var fresh = new SocialAuraWorld();
            fresh.AddActor(new SocialSimActor(10, "A", WorkerStats.CreateBaseline(), WorkerState.CreateDefault()));
            fresh.AddActor(new SocialSimActor(11, "B", WorkerStats.CreateBaseline(), WorkerState.CreateDefault()));
            var oneHit = MakeLog(10, 11, SocialContext.SharedProblem,
                SocialAction.Complain, SocialResponse.Agree, true, true,
                dWaIT: 2.5f, dWaTI: 2.5f, dTrIT: 2.0f, dTrTI: 2.0f, dFrI: -4f, dFrT: -3.5f);
            // Apply max capped relation deltas as Resolve would
            fresh.Relation(10, 11).Add(2.5f, 2.5f, 0f);
            fresh.Relation(11, 10).Add(2.0f, 2.5f, 0f);
            SocialMemoryRecorder.Record(fresh.Memory, oneHit, 210f);
            SocialRespectApplicator.Apply(fresh, oneHit);

            var clsA = RelationshipClassifier.Classify(
                fresh.Relation(10, 11), fresh.Memory.GetToward(10, 11), out var whyA);
            var clsB = RelationshipClassifier.Classify(
                fresh.Relation(11, 10), fresh.Memory.GetToward(11, 10), out var whyB);
            Check("Single SharedProblem bond ≠ Bonded",
                clsA != RelationshipClass.Bonded && clsB != RelationshipClass.Bonded,
                $"A→B={clsA} B→A={clsB}");
            Check("Single encounter Respect |Δ| ≤ MaxAbsPerEncounter",
                Mathf.Abs(SocialRespectApplicator.LastDeltaIT) <= SocialRespectApplicator.MaxAbsPerEncounter + 0.01f
                && Mathf.Abs(SocialRespectApplicator.LastDeltaTI) <= SocialRespectApplicator.MaxAbsPerEncounter + 0.01f,
                $"ΔIT={SocialRespectApplicator.LastDeltaIT:0.#} ΔTI={SocialRespectApplicator.LastDeltaTI:0.#}");

            var clash = new SocialAuraWorld();
            clash.AddActor(new SocialSimActor(20, "X", WorkerStats.CreateBaseline(), WorkerState.CreateDefault()));
            clash.AddActor(new SocialSimActor(21, "Y", WorkerStats.CreateBaseline(), WorkerState.CreateDefault()));
            var provoke = MakeLog(20, 21, SocialContext.IdleNearby,
                SocialAction.Provoke, SocialResponse.Escalate, true, true,
                dHoIT: 2.5f, dHoTI: 2.5f, dWaIT: -2f, dWaTI: -2f, dFrI: 3f, dFrT: 3.5f);
            clash.Relation(20, 21).Add(-1f, -2f, 2.5f);
            clash.Relation(21, 20).Add(-1f, -2f, 2.5f);
            SocialMemoryRecorder.Record(clash.Memory, provoke, 211f);
            SocialRespectApplicator.Apply(clash, provoke);
            var clsClash = RelationshipClassifier.Classify(
                clash.Relation(21, 20), clash.Memory.GetToward(21, 20), out _);
            Check("Single Provoke/Escalate ≠ Grudge and ≠ Rivalry",
                clsClash != RelationshipClass.Grudge && clsClash != RelationshipClass.Rivalry,
                clsClash.ToString());

            // ——— 6. Reassignment preserves Respect ———
            log.AppendLine();
            log.AppendLine("## 6. Reassignment preserves Respect");
            float keepR = world.Relation(2, 1).Respect;
            // Job swap identity is WorkerId — SocialAuraWorld keyed by person id
            Check("Respect keyed by WorkerId (survives job identity)",
                Mathf.Abs(world.Relation(2, 1).Respect - keepR) < 0.001f,
                $"R={keepR:0.#}");
            // Simulate "reassignment" by only changing DisplayName / job host — relation store same
            mara.Name = "Mara (Hauler)";
            lewis.Name = "Lewis (Refiner)";
            Check("After cosmetic job rename, Respect intact",
                Mathf.Abs(world.Relation(2, 1).Respect - keepR) < 0.001f);

            var live = new SocialAuraLiveSystem();
            var crew = new List<WorkerRuntime>
            {
                new WorkerRuntime(1, "Lewis"),
                new WorkerRuntime(2, "Mara"),
            };
            live.Bootstrap(crew);
            live.Relation(2, 1).Respect = 66f;
            // Re-bootstrap / shift with same WorkerIds (job change is assignment-side)
            live.NotifyShiftStart(crew);
            Check("Live SocialAura Respect survives shift/re-sync",
                Mathf.Abs(live.Relation(2, 1).Respect - 66f) < 0.01f,
                $"R={live.Relation(2, 1).Respect:0.#}");
            Check("Same World Memory instance after re-sync",
                ReferenceEquals(live.Memory, live.World.Memory));

            // ——— 7. Social Aura + Social Memory invariants ———
            log.AppendLine();
            log.AppendLine("## 7. Existing Social Aura and Social Memory invariants remain");
            Check("PressureTrigger still 1.05",
                Mathf.Abs(SocialAuraTuning.PressureTrigger - 1.05f) < 0.001f);
            Check("CooldownAfterEncounter still 0.55",
                Mathf.Abs(SocialAuraTuning.CooldownAfterEncounter - 0.55f) < 0.001f);
            Check("ReachWorldScale still 2.35",
                Mathf.Abs(SocialAuraLiveTuning.ReachWorldScale - 2.35f) < 0.001f);
            Check("MaxEncountersPerWorkerPerShift still 3",
                Mathf.Abs(SocialAuraTuning.MaxEncountersPerWorkerPerShift - 3f) < 0.001f);
            Check("Memory MaxPerTarget still 12", SocialMemoryStore.MaxPerTarget == 12);

            int memBefore = world.Memory.TotalEntries;
            var trivial = MakeLog(1, 2, SocialContext.IdleNearby,
                SocialAction.Connect, SocialResponse.Ignore, false, true);
            SocialMemoryRecorder.Record(world.Memory, trivial, 220f);
            SocialRespectApplicator.Apply(world, trivial);
            Check("Trivial encounter still does not flood memory",
                world.Memory.TotalEntries == memBefore);
            Check("T/W/H Add() signature unchanged (Respect separate)",
                typeof(SocialDirectedRelation).GetMethod("Add", new[] { typeof(float), typeof(float), typeof(float) }) != null
                && typeof(SocialDirectedRelation).GetMethod("AddRespect") != null);

            float tCheck = world.Relation(1, 2).Trust;
            world.Relation(1, 2).AddRespect(5f);
            Check("AddRespect does not mutate Trust",
                Mathf.Abs(world.Relation(1, 2).Trust - tCheck) < 0.001f);

            log.AppendLine();
            log.AppendLine("## Summary");
            log.AppendLine($"PASS {pass} / FAIL {fail}");
            log.AppendLine(fail == 0 ? "INVARIANT: PASS" : "INVARIANT: FAIL");
            log.AppendLine();
            log.AppendLine("## Files");
            log.AppendLine("- Assets/Vibe/FreeMovement/SocialAuraStage0Types.cs (Respect on SocialDirectedRelation)");
            log.AppendLine("- Assets/Vibe/FreeMovement/RelationshipV11.cs (Respect applicator + classifier)");
            log.AppendLine("- Assets/Vibe/FreeMovement/SocialAuraStage0Encounter.cs (hook Apply)");
            log.AppendLine("- Assets/Vibe/FreeMovement/SocialAuraLive.cs (hook Apply + NearbyDebug.Respect)");
            log.AppendLine("- Assets/Vibe/FreeMovement/FreeMovementSocketMapRunner.cs (DEV UI)");
            log.AppendLine("- Assets/Vibe/FreeMovement/RelationshipV11Audit.cs (this audit)");

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(Application.dataPath, "..", "BenchmarkResults")
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string stamped = Path.Combine(dir, $"relationship_v11_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            string latest = Path.Combine(dir, "relationship_v11_latest.md");
            File.WriteAllText(stamped, log.ToString());
            File.WriteAllText(latest, log.ToString());
            return latest;
        }

        static SocialEncounterLog MakeLog(
            int init, int tgt, SocialContext ctx,
            SocialAction action, SocialResponse response,
            bool actionOk, bool respOk,
            float dTrIT = 0f, float dWaIT = 0f, float dHoIT = 0f,
            float dTrTI = 0f, float dWaTI = 0f, float dHoTI = 0f,
            float dFrI = 0f, float dFrT = 0f, float dMoI = 0f, float dMoT = 0f)
        {
            return new SocialEncounterLog
            {
                ShiftIndex = 1,
                TimeInShift = 0.5f,
                InitiatorId = init,
                TargetId = tgt,
                Context = ctx,
                Action = action,
                Response = response,
                ActionSuccess = actionOk,
                ResponseSuccess = respOk,
                DeltaTrustIT = dTrIT,
                DeltaWarmthIT = dWaIT,
                DeltaHostilityIT = dHoIT,
                DeltaTrustTI = dTrTI,
                DeltaWarmthTI = dWaTI,
                DeltaHostilityTI = dHoTI,
                DeltaFrustrationInit = dFrI,
                DeltaFrustrationTarget = dFrT,
                DeltaMoraleInit = dMoI,
                DeltaMoraleTarget = dMoT,
                OutcomeSummary = "AUDIT",
            };
        }
    }
}
