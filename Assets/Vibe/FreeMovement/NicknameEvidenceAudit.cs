using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    public static class NicknameEvidenceAudit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[NICKNAME EVIDENCE] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.ReadAllText(path).Contains("INVARIANT: FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var log = new StringBuilder(10_000);
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                if (ok) { pass++; log.AppendLine($"PASS | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}")); }
                else { fail++; log.AppendLine($"FAIL | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}")); }
            }

            log.AppendLine("# Nickname Evidence Audit (data only)");
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine();

            NicknameEvidenceStore.Instance.Clear();
            WorkerStateClock.GameHours = 10f;

            // Need a bound service to observe via EmitForced
            var crew = new Dictionary<int, WorkerRuntime>
            {
                [2] = new WorkerRuntime(2, "Mara"),
                [5] = new WorkerRuntime(5, "Viktor"),
            };
            var svc = new WorkerStateEventService();
            svc.Bind(id => crew.TryGetValue(id, out var w) ? w : null);
            WorkerStateEventHub.Service = svc;

            float s0 = NicknameEvidenceStore.Instance.StrengthOf(2, NicknameEvidenceKind.Discovery);
            svc.EmitForced(WorkerStateEvent.Create(2, WorkerStateEventType.Discovery, 5f, "Vein"));
            Check("Discovery accumulates evidence",
                NicknameEvidenceStore.Instance.StrengthOf(2, NicknameEvidenceKind.Discovery) > s0);

            float d1 = NicknameEvidenceStore.Instance.StrengthOf(2, NicknameEvidenceKind.Discovery);
            svc.EmitForced(WorkerStateEvent.Create(2, WorkerStateEventType.Discovery, 5f, "Vein2"));
            float d2 = NicknameEvidenceStore.Instance.StrengthOf(2, NicknameEvidenceKind.Discovery);
            Check("Repetition increases strength (diminishing)",
                d2 > d1 && (d2 - d1) < (d1 - s0) + 0.01f,
                $"Δ1={d1 - s0:0.00} Δ2={d2 - d1:0.00}");

            float beforeTiny = NicknameEvidenceStore.Instance.KindCount(2);
            svc.EmitForced(WorkerStateEvent.Create(2, WorkerStateEventType.ProgressSuccess, 1f, "Micro"));
            Check("Trivial ProgressSuccess does not dominate",
                NicknameEvidenceStore.Instance.StrengthOf(2, NicknameEvidenceKind.DifficultProgress) < 0.01f
                && NicknameEvidenceStore.Instance.KindCount(2) == beforeTiny);

            svc.EmitForced(WorkerStateEvent.Create(
                2, WorkerStateEventType.EquipmentRecovered, 4f, "EngineerRepair",
                JobType.Excavation, "excavator.x", relatedWorkerId: 5));
            Check("Repair rescue marks excavator and engineer",
                NicknameEvidenceStore.Instance.StrengthOf(2, NicknameEvidenceKind.RepairRescue) > 0.5f
                && NicknameEvidenceStore.Instance.StrengthOf(5, NicknameEvidenceKind.RepairRescue) > 0.5f);

            // Reassignment identity: evidence keyed by WorkerId
            float maraRepair = NicknameEvidenceStore.Instance.StrengthOf(2, NicknameEvidenceKind.RepairRescue);
            crew[2] = new WorkerRuntime(2, "Mara"); // new runtime same id
            Check("Evidence survives WorkerRuntime rebuild (person id)",
                Mathf.Abs(NicknameEvidenceStore.Instance.StrengthOf(2, NicknameEvidenceKind.RepairRescue)
                          - maraRepair) < 0.001f);

            var mem = new SocialMemoryStore();
            mem.Add(new SocialMemoryEntry
            {
                ObserverId = 2, TargetId = 5, Type = SocialMemoryType.SharedHardship,
                Strength = 0.8f, GameTime = 12f,
                Significance = SocialMemorySignificance.Major,
                Context = SocialContext.SharedProblem
            });
            Check("Major social memory seeds MajorSocial evidence",
                NicknameEvidenceStore.Instance.StrengthOf(2, NicknameEvidenceKind.MajorSocial) > 0.4f);

            var list = new List<NicknameEvidenceEntry>();
            NicknameEvidenceStore.Instance.GetStrongest(2, list, 5);
            Check("GetStrongest returns ranked evidence", list.Count >= 1);

            Check("PressureTrigger unchanged", Mathf.Abs(SocialAuraTuning.PressureTrigger - 1.05f) < 0.001f);
            Check("No nickname generation API present",
                Type.GetType("DeepCore.FreeMovement.NicknameGenerator") == null);

            WorkerStateEventHub.Service = null;
            NicknameEvidenceStore.Instance.Clear();

            log.AppendLine();
            log.AppendLine("## Summary");
            log.AppendLine($"PASS {pass} / FAIL {fail}");
            log.AppendLine(fail == 0 ? "INVARIANT: PASS" : "INVARIANT: FAIL");

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(Application.dataPath, "..", "BenchmarkResults")
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string latest = Path.Combine(dir, "nickname_evidence_latest.md");
            File.WriteAllText(Path.Combine(dir, $"nickname_evidence_{DateTime.Now:yyyyMMdd_HHmmss}.md"), log.ToString());
            File.WriteAllText(latest, log.ToString());
            return latest;
        }
    }
}
