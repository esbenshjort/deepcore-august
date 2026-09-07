using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Relationship trajectory V1 audit — read-only history, bounded, no effect changes.</summary>
    public static class RelationshipTrajectoryAudit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[TRAJECTORY] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.ReadAllText(path).Contains("INVARIANT: FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var log = new StringBuilder(12_000);
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                if (ok) { pass++; log.AppendLine($"PASS | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}")); }
                else { fail++; log.AppendLine($"FAIL | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}")); }
            }

            log.AppendLine("# Relationship Trajectory Audit");
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine();

            var store = new RelationshipTrajectoryStore();
            store.Record(1, 2, RelationshipAxis.Trust, 5f, 5.1f, 10f, "tiny"); // below threshold
            Check("Sub-threshold delta ignored", store.TotalEntries == 0);

            store.Record(1, 2, RelationshipAxis.Trust, 5f, 6f, 11f, "S1:Encourage");
            store.Record(1, 2, RelationshipAxis.Hostility, 0f, 1.2f, 12f, "S1:Provoke");
            store.Record(2, 1, RelationshipAxis.Respect, 50f, 52.5f, 12f, "S1:Provoke",
                SocialMemoryType.InsultedMe, true);
            Check("Meaningful deltas recorded", store.TotalEntries == 3);
            Check("Directional: 1→2 ≠ 2→1 lists",
                store.GetToward(1, 2).Count == 2 && store.GetToward(2, 1).Count == 1);

            for (int i = 0; i < 20; i++)
                store.Record(1, 2, RelationshipAxis.Warmth, 0f, 1f, 20f + i, $"flood{i}");
            Check($"Bounded to MaxPerPair={RelationshipTrajectoryStore.MaxPerPair}",
                store.GetToward(1, 2).Count == RelationshipTrajectoryStore.MaxPerPair);

            // Live encounter path
            WorkerStateClock.GameHours = 50f;
            var world = new SocialAuraWorld();
            var a = new SocialSimActor(1, "A", WorkerStats.CreateBaseline(), WorkerState.CreateDefault());
            var b = new SocialSimActor(2, "B", WorkerStats.CreateBaseline(), WorkerState.CreateDefault());
            a.State.Frustration = 40f;
            b.State.Frustration = 35f;
            a.State.Morale = 55f;
            b.State.Morale = 50f;
            world.AddActor(a);
            world.AddActor(b);
            world.BeginShift(1);
            int traj0 = world.Trajectory.TotalEntries;
            int enc = 0;
            for (int i = 0; i < 60; i++)
            {
                var e = world.Expose(1, 2, SocialContext.SharedProblem, 1.5f);
                if (e != null) enc++;
            }
            Check("Expose can resolve encounters", enc > 0, $"enc={enc}");
            // Encounter deltas may be below trajectory min — verify CommitDelta path explicitly
            var before = RelationshipTrajectoryStore.Capture(world.Relation(1, 2));
            world.Relation(1, 2).Add(1.2f, 0.8f, 0.5f);
            world.Relation(1, 2).AddRespect(2f);
            world.Trajectory.CommitDelta(1, 2, before, world.Relation(1, 2), 55f, "audit-force", world.Memory);
            Check("CommitDelta records encounter-scale axis changes",
                world.Trajectory.GetToward(1, 2).Count > traj0,
                $"entries={world.Trajectory.GetToward(1, 2).Count}");
            Check("Trajectory does not alter relation math API",
                typeof(SocialDirectedRelation).GetMethod("Add", new[] { typeof(float), typeof(float), typeof(float) }) != null);

            // Invariants
            Check("PressureTrigger still 1.05", Mathf.Abs(SocialAuraTuning.PressureTrigger - 1.05f) < 0.001f);
            Check("Memory MaxPerTarget still 12", SocialMemoryStore.MaxPerTarget == 12);
            Check("Respect baseline still 50",
                Mathf.Abs(SocialDirectedRelation.RespectBaseline - 50f) < 0.001f);

            log.AppendLine();
            log.AppendLine("## Summary");
            log.AppendLine($"PASS {pass} / FAIL {fail}");
            log.AppendLine(fail == 0 ? "INVARIANT: PASS" : "INVARIANT: FAIL");
            log.AppendLine();
            log.AppendLine("## Files");
            log.AppendLine("- Assets/Vibe/FreeMovement/RelationshipTrajectory.cs");
            log.AppendLine("- Assets/Vibe/FreeMovement/SocialAuraStage0Encounter.cs");
            log.AppendLine("- Assets/Vibe/FreeMovement/SocialAuraLive.cs");
            log.AppendLine("- Assets/Vibe/FreeMovement/FreeMovementSocketMapRunner.cs");

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(Application.dataPath, "..", "BenchmarkResults")
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string latest = Path.Combine(dir, "relationship_trajectory_latest.md");
            File.WriteAllText(Path.Combine(dir, $"relationship_trajectory_{DateTime.Now:yyyyMMdd_HHmmss}.md"), log.ToString());
            File.WriteAllText(latest, log.ToString());
            return latest;
        }
    }
}
