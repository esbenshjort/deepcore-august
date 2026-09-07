using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Manager Communication V1 offline audit.</summary>
    public static class ManagerCommAudit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[MANAGER COMM] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.Exists(path) && File.ReadAllText(path).Contains("**Result:** FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var sb = new StringBuilder();
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                if (ok) pass++;
                else fail++;
                sb.AppendLine($"- {(ok ? "PASS" : "FAIL")}  {name}"
                              + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
            }

            sb.AppendLine("# Manager Communication V1 Audit");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("## Exact Soul / relationship mappings");
            sb.AppendLine("| Input | Role |");
            sb.AppendLine("|-------|------|");
            sb.AppendLine("| Manager Trust / Respect | Raise receptivity to instructions |");
            sb.AppendLine("| Manager Resentment | Lower receptivity; harder compliance |");
            sb.AppendLine("| Tolerance / Composure / Determination | Shape push/criticize/break reactions |");
            sb.AppendLine("| Frustration / MentalFatigue / Injury | Context gates + harsh-push risk |");
            sb.AppendLine("| WorkerStateEventType.ManagerCommunication | Modest Focus/Morale/Frustration |");
            sb.AppendLine();

            WorkerRoll.BeginSeeded(42);
            try
            {
                sb.AppendLine("## Same instruction, different workers");
                var soft = MakeWorker(601, "Soft", composure: 5, tolerance: 4, determination: 6);
                soft.State.Frustration = 55f;
                soft.State.MentalFatigue = 60f;
                var hard = MakeWorker(602, "Hard", composure: 16, tolerance: 15, determination: 14);
                hard.State.Frustration = 15f;
                hard.State.MentalFatigue = 20f;
                var mgrStore = new ManagerRelationshipStore();
                mgrStore.Get(soft.WorkerId).Add(-10f, -5f, 20f); // lower trust, some resent
                mgrStore.Get(hard.WorkerId).Add(15f, 20f, -5f);

                var spam = new ManagerCommAntiSpam();
                var ctxSoft = Ctx(soft, mgrStore, worked: 11f);
                var ctxHard = Ctx(hard, mgrStore, worked: 6f);
                var rSoft = ManagerCommSystem.EvaluateTalk(ManagerTalkAction.PushHarder, ctxSoft, spam);
                var spam2 = new ManagerCommAntiSpam();
                var rHard = ManagerCommSystem.EvaluateTalk(ManagerTalkAction.PushHarder, ctxHard, spam2);
                Check("Soft/OT push ≠ Hard fresh push",
                    rSoft.Reaction != rHard.Reaction
                    || rSoft.AcceptedIntent != rHard.AcceptedIntent,
                    $"soft={rSoft.ReactionLabel} hard={rHard.ReactionLabel}");
                Check("High receptivity can accept push",
                    rHard.AcceptedIntent || rHard.Reaction == ManagerReactionKind.Motivated
                    || rHard.Reaction == ManagerReactionKind.Accepted,
                    rHard.ReactionLabel);

                sb.AppendLine();
                sb.AppendLine("## Context: criticize injured");
                var hurt = MakeWorker(603, "Hurt", 10, 10, 10);
                hurt.State.NeedsCare = true;
                hurt.State.Injury = 70f;
                var rCrit = ManagerCommSystem.EvaluateTalk(
                    ManagerTalkAction.Criticize,
                    Ctx(hurt, mgrStore, onShift: true),
                    new ManagerCommAntiSpam());
                Check("Criticize injured → resentful",
                    rCrit.Reaction == ManagerReactionKind.Resentful, rCrit.ReactionLabel);

                sb.AppendLine();
                sb.AppendLine("## Recovery builds trust");
                var tired = MakeWorker(604, "Tired", 12, 11, 10);
                tired.State.ExhaustionLatched = true;
                tired.State.MentalFatigue = 70f;
                float t0 = mgrStore.Get(tired.WorkerId).Trust;
                var rBreak = ManagerCommSystem.EvaluateTalk(
                    ManagerTalkAction.TakeABreak,
                    Ctx(tired, mgrStore, worked: 10f),
                    new ManagerCommAntiSpam());
                ManagerCommSystem.ApplyResult(
                    rBreak, mgrStore, null, new ManagerIntentStore(), 10f,
                    ManagerTalkAction.TakeABreak);
                Check("Take a Break when exhausted accepted",
                    rBreak.AcceptedIntent && rBreak.Reaction == ManagerReactionKind.Reassured);
                Check("Trust rises after recovery break",
                    mgrStore.Get(tired.WorkerId).Trust > t0);

                sb.AppendLine();
                sb.AppendLine("## Anti-spam praise");
                var ok = MakeWorker(605, "Ok", 12, 12, 12);
                ok.State.FocusState = 70f;
                ok.State.Morale = 60f;
                var spamP = new ManagerCommAntiSpam();
                var ctxOk = Ctx(ok, mgrStore);
                ctxOk.Performance01 = 0.7f;
                ctxOk.RecentSuccess = true;
                var p1 = ManagerCommSystem.EvaluateTalk(ManagerTalkAction.Praise, ctxOk, spamP);
                Check("First praise can motivate",
                    p1.Reaction == ManagerReactionKind.Motivated
                    || p1.Reaction == ManagerReactionKind.Accepted, p1.ReactionLabel);
                // Force repeat history
                for (int i = 0; i < 6; i++)
                    spamP.Record(ok.WorkerId, (int)ManagerTalkAction.Praise, 30f + i * 0.5f, 0.01f);
                var pSpam = ManagerCommSystem.EvaluateTalk(
                    ManagerTalkAction.Praise, Ctx(ok, mgrStore, gameHours: 40f), spamP);
                Check("Praise spam blocked or hollow",
                    pSpam.SpamBlocked || pSpam.Reaction == ManagerReactionKind.Hollow,
                    pSpam.ReactionLabel + (pSpam.SpamBlocked ? " blocked" : ""));

                sb.AppendLine();
                sb.AppendLine("## Driven worker may refuse break");
                var driven = MakeWorker(606, "Driven", 14, 12, determination: 18);
                driven.State.MentalFatigue = 30f;
                var rNo = ManagerCommSystem.EvaluateTalk(
                    ManagerTalkAction.TakeABreak,
                    Ctx(driven, mgrStore),
                    new ManagerCommAntiSpam());
                Check("Healthy driven can refuse break",
                    rNo.Reaction == ManagerReactionKind.Refused && !rNo.AcceptedIntent,
                    rNo.ReactionLabel);

                sb.AppendLine();
                sb.AppendLine("## Intervene take sides");
                var a = MakeWorker(607, "Mara", 12, 12, 12);
                var b = MakeWorker(608, "Viktor", 10, 8, 11);
                mgrStore.Get(a.WorkerId);
                mgrStore.Get(b.WorkerId);
                float resB0 = mgrStore.Get(b.WorkerId).Resentment;
                float trustA0 = mgrStore.Get(a.WorkerId).Trust;
                var session = new SocialArgumentSession { IdA = a.WorkerId, IdB = b.WorkerId };
                session.Phase = SocialArgumentPhase.Peak;
                var side = ManagerCommSystem.EvaluateIntervene(
                    ManagerInterveneAction.SideWithA, session, a, b, mgrStore,
                    new ManagerCommAntiSpam(), 50f, isFight: false);
                Check("Side with A raises A's trust",
                    mgrStore.Get(a.WorkerId).Trust > trustA0);
                Check("Side with A raises B's resentment",
                    mgrStore.Get(b.WorkerId).Resentment > resB0);
                Check("Side outcome notes both workers",
                    side.ResultA != null && side.ResultB != null);

                sb.AppendLine();
                sb.AppendLine("## Memory + event type");
                Check("Manager memory types exist",
                    Enum.IsDefined(typeof(SocialMemoryType), SocialMemoryType.ManagerPraisedMe)
                    && Enum.IsDefined(typeof(SocialMemoryType), SocialMemoryType.ManagerTookTheirSide));
                Check("ManagerCommunication event exists",
                    Enum.IsDefined(typeof(WorkerStateEventType),
                        WorkerStateEventType.ManagerCommunication));
                var mem = new SocialMemoryStore();
                mem.Add(new SocialMemoryEntry
                {
                    Type = SocialMemoryType.ManagerPraisedMe,
                    Strength = 0.5f,
                    GameTime = 1f,
                    ObserverId = ok.WorkerId,
                    TargetId = ManagerRelationshipStore.ManagerId,
                    Significance = SocialMemorySignificance.Ordinary,
                });
                Check("Memory accepts ManagerId target",
                    mem.GetToward(ok.WorkerId, ManagerRelationshipStore.ManagerId).Count == 1);

                sb.AppendLine();
                sb.AppendLine("## Relation labels");
                var hostile = new ManagerRelation { Trust = 20f, Respect = 25f, Resentment = 75f };
                Check("Hostile label",
                    ManagerRelationUi.Classify(hostile) == ManagerRelationLabel.HostileTowardYou);
                var trusts = new ManagerRelation { Trust = 75f, Respect = 60f, Resentment = 5f };
                Check("Trusts you label",
                    ManagerRelationUi.Classify(trusts) == ManagerRelationLabel.TrustsYou);

                sb.AppendLine();
                sb.AppendLine("## Runner wiring (source)");
                string runnerPath = Path.GetFullPath(Path.Combine(
                    Application.dataPath, "Vibe", "FreeMovement", "FreeMovementSocketMapRunner.cs"));
                string runner = File.Exists(runnerPath) ? File.ReadAllText(runnerPath) : "";
                Check("TALK HUD present", runner.Contains("DrawManagerTalkPanel"));
                Check("CREW broadcast present", runner.Contains("DrawManagerCrewTalkPanel"));
                Check("Intervene overlay present", runner.Contains("DrawManagerInterveneOverlay"));
                Check("YOUR RELATIONSHIP on sheet", runner.Contains("DrawManagerRelationOnSheet"));
                Check("No dialogue tree / salary APIs",
                    !runner.Contains("SalaryNegotiation") && !runner.Contains("DialogueTree"));
            }
            finally
            {
                WorkerRoll.EndSeeded();
            }

            sb.AppendLine();
            sb.AppendLine($"**Result:** {(fail == 0 ? "PASS" : "FAIL")}  ({pass} passed, {fail} failed)");

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BenchmarkResults"))
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string stamp = Path.Combine(dir, $"manager_comm_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            string latest = Path.Combine(dir, "manager_comm_latest.md");
            File.WriteAllText(stamp, sb.ToString());
            File.WriteAllText(latest, sb.ToString());
            Debug.Log($"[MANAGER COMM] {(fail == 0 ? "PASS" : "FAIL")} → {latest}");
            return latest;
        }

        static WorkerRuntime MakeWorker(int id, string name, int composure, int tolerance, int determination)
        {
            var wr = new WorkerRuntime(id, name);
            wr.Stats.Set(WorkerStatId.Composure, composure);
            wr.Stats.Set(WorkerStatId.Tolerance, tolerance);
            wr.Stats.Set(WorkerStatId.Determination, determination);
            wr.Stats.ClampAll();
            return wr;
        }

        static ManagerCommContext Ctx(
            WorkerRuntime wr,
            ManagerRelationshipStore store,
            float worked = 5f,
            bool onShift = true,
            float gameHours = 10f)
        {
            return new ManagerCommContext
            {
                Worker = wr,
                Mgr = store.Get(wr.WorkerId),
                GameHours = gameHours,
                OnShift = onShift,
                WorkedHoursToday = worked,
                OvertimeHours = Mathf.Max(0f, worked - 8f),
                Performance01 = 0.55f,
            };
        }
    }
}
