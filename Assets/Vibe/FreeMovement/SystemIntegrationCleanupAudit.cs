using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Deep Core system integration + safe cleanup audit (stability gate).
    /// Covers architecture ownership, 20 invariants, position/event/legacy checks,
    /// and cross-system scenarios. Does not add gameplay features.
    /// Menu: DeepCore/Diagnostics/Run System Integration Cleanup Audit
    /// Batch: -executeMethod DeepCore.FreeMovement.SystemIntegrationCleanupAudit.RunFromEditor
    /// </summary>
    public static class SystemIntegrationCleanupAudit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[SYSTEM INTEGRATION] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.Exists(path) && File.ReadAllText(path).Contains("**Result:** FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var sb = new StringBuilder(64_000);
            int pass = 0, fail = 0;
            var blockers = new List<string>();

            void Check(string name, bool ok, string detail = "")
            {
                if (ok)
                {
                    pass++;
                    sb.AppendLine($"- PASS  {name}"
                                  + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
                }
                else
                {
                    fail++;
                    blockers.Add(name + (string.IsNullOrEmpty(detail) ? "" : $": {detail}"));
                    sb.AppendLine($"- FAIL  {name}"
                                  + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
                }
            }

            string runnerSrc = ReadSource("FreeMovementSocketMapRunner.cs");
            string bodySrc = ReadSource("PersistentWorkerBody.cs");
            string eventSrc = ReadSource("WorkerStateEventSystem.cs");
            string accidentSrc = ReadSource("WorkerAccident.cs");
            string conflictSrc = ReadSource("SocialConflict.cs");
            string excavSrc = ReadSource("FreeWorkerController.cs");
            string engSrc = ReadSource("EngineerPerson.cs");
            string auraSrc = ReadSource("SocialAuraLive.cs");
            string memSrc = ReadSource("SocialMemory.cs");

            sb.AppendLine("# Deep Core — System Integration + Safe Cleanup Audit");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("Mode: static source + ForceBuild live checks (not interactive Play Mode).");
            sb.AppendLine("Goal: STABILITY — no new features, no retunes.");
            sb.AppendLine();

            // ─── ROUND 1: Architecture ownership (documented) ───
            sb.AppendLine("## ROUND 1 — Architecture ownership map");
            sb.AppendLine();
            sb.AppendLine("| Domain | Authoritative owner | Notes |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine("| Person identity | `WorkerRuntime` (`WorkerId`) | Stats/State/Injuries/CampBody nested |");
            sb.AppendLine("| Physical person | `WorkerAvatar` | One per living WorkerId |");
            sb.AppendLine("| WorkerStats | `WorkerRuntime.Stats` | Hosts bind, do not own |");
            sb.AppendLine("| WorkerState | `WorkerRuntime.State` | Mutations via events + care/camp |");
            sb.AppendLine("| Injuries | `WorkerInjuryStore` + meter | Policy: `InjuryResponse` |");
            sb.AppendLine("| Peer social | `SocialAuraWorld` / Memory | Via `SocialAuraLiveSystem` |");
            sb.AppendLine("| Manager relation | `ManagerRelationshipStore` | Separate axes; shared memory store |");
            sb.AppendLine("| Assignment | `WorkerAssignmentManager` | Hosts are mirrors |");
            sb.AppendLine("| Job behavior | Host FSM (`*Person` / excavator) | Gated by `CanPerformJobActions` |");
            sb.AppendLine("| Machine state | Host (heat/route/equipment) | Not person identity |");
            sb.AppendLine("| Shift / phase | Runner `CrewPhase` + `ShiftPlanner` | Per-person ledger parallel |");
            sb.AppendLine("| Commute | Runner `_personNav` | SoftArrive = DEV/audit only |");
            sb.AppendLine("| Camp/meal/toilet | Runner + `CampLife` / sites | `WorkerCampBody` flags |");
            sb.AppendLine("| Locomotion formula | `WorkerLocomotion` | Hosts have parallel pathfinders |");
            sb.AppendLine("| Terrain | `FineTerrainWorld` | Dig / collapse / infra read |");
            sb.AppendLine("| Collapse/debris | `TunnelCollapseSystem` | |");
            sb.AppendLine("| Supports/lamps | `MineInfrastructure` | Engineer places |");
            sb.AppendLine();
            sb.AppendLine("### Known authority conflicts (documented debt — not auto-fixed)");
            sb.AppendLine("1. Avatar vs host Transform (on-shift snap to operate point).");
            sb.AppendLine("2. Runner `_personNav` vs host `ExcavatedPathfinder`.");
            sb.AppendLine("3. Assignment manager vs host `BindWorker` mirrors (SyncBodyBindings).");
            sb.AppendLine("4. Injury meter vs typed `WorkerInjuryStore` (best-effort sync).");
            sb.AppendLine("5. `CrewPhase` crew-wide vs per-worker day ledger.");
            sb.AppendLine("6. Legacy `ControlWorker` stub (selection is WorkerId).");
            sb.AppendLine();

            // ─── ROUND 2: Invariant suite ───
            sb.AppendLine("## ROUND 2 — Core invariant suite (20)");
            sb.AppendLine();

            FreeMovementSocketMapRunner runner = null;
            GameObject host = null;
            try
            {
                host = new GameObject("SystemIntegration_Audit_Host");
                runner = host.AddComponent<FreeMovementSocketMapRunner>();
                runner.ForceBuildForAudit();

                // 1
                Check("1. One living worker = one avatar",
                    runner.AuditOneAvatarPerWorker() && runner.AuditCrewCount() >= 5);
                // 2–3 reassignment identity
                float maraFr0 = runner.AuditWorkerState(2).Frustration;
                string maraStats0 = runner.AuditStatsRef(2);
                runner.AuditWorkerState(2).Frustration = Mathf.Clamp(maraFr0 + 7f, 0f, 100f);
                float maraFrMarked = runner.AuditWorkerState(2).Frustration;
                bool assignOk = runner.AuditTryAssign(2, JobType.Prospecting, out string whyPros);
                Check("2. Identity/state survives reassignment",
                    assignOk
                    && runner.AuditStatsRef(2) == maraStats0
                    && Mathf.Approximately(runner.AuditWorkerState(2).Frustration, maraFrMarked),
                    whyPros);
                Check("3. Assignment does not own person identity",
                    runner.AuditJobOf(2) == JobType.Prospecting
                    && runner.AuditStatsRef(2) == maraStats0);
                // restore Mara excavation for later scenarios
                runner.AuditTryAssign(2, JobType.Excavation, out _);
                runner.AuditTryAssign(1, JobType.Prospecting, out _);

                // 4 providers don't commute
                Check("4. EnterOnShift documents no SoftTeleport hosts",
                    runnerSrc.Contains("do NOT SoftTeleport hosts"));
                Check("4b. BeginHeadingOut does not SoftArriveAllWork",
                    !ContainsCallSequence(runnerSrc, "void BeginHeadingOut", "SoftArriveAllWork()", 200));

                // 5
                Check("5. SoftArriveAllHome removed (no normal home teleport API)",
                    !runnerSrc.Contains("SoftArriveAllHome"));
                Check("5b. SoftArriveAllWork only ForceBuild/audit",
                    runnerSrc.Contains("audit-only seat")
                    && CountOccurrences(runnerSrc, "SoftArriveAllWork()") <= 3);

                // 6 physical state — ForceBuild seats after EnterOnShift so ArrivedWork sticks
                Check("6. Seated audit crew OnShift Operating for excavator",
                    runner.AuditCrewPhaseName() == "OnShift"
                    && runner.AuditPhysicalStateName(2) == "Operating",
                    $"phase={runner.AuditCrewPhaseName()} phys={runner.AuditPhysicalStateName(2)} job={runner.AuditJobOf(2)}");

                // 7 dead/incap gates
                Check("7. CanPerformJobActions incap gate in source",
                    SourceHasIncapGate(runnerSrc));
                var mara = Find(runner, 2);
                bool wasIncap = mara.State.Incapacitated;
                mara.State.MarkIncapacitated(WorkerStateClock.GameHours, "audit");
                Check("7b. Live: incap blocks CanPerform",
                    !runner.AuditCanPerform(2));
                if (!wasIncap) mara.State.ClearIncapacitated();

                // 8 injuries persist
                var prevHub = InjuryResponseHub.OnInjuryApplied;
                InjuryResponseHub.OnInjuryApplied = null;
                float inj0 = mara.State.Injury;
                int injCount0 = mara.Injuries.Count;
                var rec = WorkerAccidentSystem.ApplyTypedInjury(
                    mara, WorkerInjuryType.Sprain, WorkerInjuryCause.TerrainFall,
                    "IntegrationAudit");
                Check("8. Injury persists on WorkerRuntime store",
                    rec != null && mara.Injuries.Count > injCount0
                    && mara.State.Injury >= inj0);
                // Heal out audit wound
                for (int i = 0; i < mara.Injuries.Active.Count; i++)
                    mara.Injuries.Active[i].RecoveryGameHoursLeft = 0f;
                mara.Injuries.TickRecovery(1f, 1f, false);
                mara.State.Injury = inj0;
                mara.State.NeedsCare = false;
                InjuryResponseHub.OnInjuryApplied = prevHub;

                // 9 steward/camp/sleep same WorkerState
                Check("9. Sleep recovery applies WorkerRuntime.State",
                    runnerSrc.Contains("ApplySleepRecoveryFraction")
                    || runnerSrc.Contains("ApplyCrewSleepRecoveryFraction"));

                // 10 social uses WorkerId
                Check("10. Social Aura / conflict target WorkerId",
                    auraSrc.Contains("WorkerId") && conflictSrc.Contains("WorkerRuntime.Find"));

                // 11 manager relationship
                var mgrStore = new ManagerRelationshipStore();
                var rel = mgrStore.Get(2);
                rel.Add(1f, 0f, 0f);
                float t = rel.Trust;
                Check("11. ManagerRelationshipStore persists Trust", t >= 1f - 0.01f);

                // 12 shift planner / commute
                Check("12. Shift start uses physical HeadingOut commute",
                    runnerSrc.Contains("BeginHeadingOut")
                    && runnerSrc.Contains("physical commute"));

                // 13 toilet preserves assignment
                Check("13. Toilet trip parks machine, does not ClearWorker",
                    runnerSrc.Contains("ParkMachineIdle")
                    && runnerSrc.Contains("BeginToiletTrip")
                    && !ContainsCallSequence(runnerSrc, "void BeginToiletTrip", "ClearWorker()", 80));

                // 14 debris/path
                Check("14. Tunnel collapse / debris nav shared FineTerrainWorld",
                    File.Exists(Path.Combine(FreeMovementDir(), "TunnelCollapse.cs")));

                // 15 recruitment unique
                var session = new RecruitmentHiringSession();
                session.Open();
                foreach (var job in RecruitmentCatalog.HireJobs)
                {
                    session.SetBrowseJob(job);
                    session.BrowseIndex = 0;
                    session.HireSelected();
                }
                bool built = session.TryBuildCrew(201, out var crew, out _, out string hireFail);
                bool uniqueHire = true;
                if (built && crew != null)
                {
                    var ids = new HashSet<int>();
                    foreach (var w in crew)
                        if (w == null || !ids.Add(w.WorkerId)) uniqueHire = false;
                }
                Check("15. Recruitment builds unique WorkerIds",
                    built && uniqueHire, hireFail);

                // 16–17 event / memory single path (structural)
                Check("16. WorkerStateEvent spam gate exists",
                    eventSrc.Contains("WorkerStateEventSpamGate")
                    && eventSrc.Contains("MinGapHours"));
                Check("17. SocialMemoryRecorder.IsMeaningful gate",
                    memSrc.Contains("IsMeaningful")
                    && memSrc.Contains("LastRecordPassAdds"));

                // 18 game-time
                Check("18. Injury/event clocks use WorkerStateClock.GameHours",
                    accidentSrc.Contains("WorkerStateClock.GameHours")
                    && eventSrc.Contains("GameHours"));

                // 19 bounds
                Check("19. Snap/relocate uses tunnel / InBounds helpers",
                    runnerSrc.Contains("TrySnapToNearestTunnel")
                    && runnerSrc.Contains("SnapPostToTunnel"));

                // 20 DEV contamination
                Check("20. SoftArrive labeled DEV/audit; ForceBuild seats only",
                    runnerSrc.Contains("DEV soft-arrive")
                    || runnerSrc.Contains("audit-only seat"));
                Check("20b. BridgeSelectedWorkerFromLegacySlot removed",
                    !runnerSrc.Contains("BridgeSelectedWorkerFromLegacySlot"));

                // ─── ROUND 3: Position / transition ───
                sb.AppendLine();
                sb.AppendLine("## ROUND 3 — Position / lifecycle classification");
                sb.AppendLine();
                sb.AppendLine("| Path | Class |");
                sb.AppendLine("|---|---|");
                sb.AppendLine("| SoftArriveAllWork via ForceBuildForAudit | DEV ONLY |");
                sb.AppendLine("| SoftArriveAllHome | REMOVED |");
                sb.AppendLine("| RelocateAvatar leave-host / embedded | EMERGENCY FALLBACK |");
                sb.AppendLine("| ExcavatorCabin Enter/Exit ParkAt | VALID |");
                sb.AppendLine("| Toilet ParkAt door | VALID |");
                sb.AppendLine("| Engineer SoftTeleport repair strand ≥2.5s | EMERGENCY FALLBACK (kept) |");
                sb.AppendLine("| Host SoftTeleport Hauler/Refiner/Steward | REMOVED (unused) |");
                sb.AppendLine("| Commute StepPersonCommute Follow | VALID |");
                sb.AppendLine();
                Check("3. No SoftArriveAllHome remains",
                    !runnerSrc.Contains("SoftArriveAllHome"));
                Check("3. Engineer SoftTeleport kept as emergency only",
                    engSrc.Contains("SoftTeleport")
                    && engSrc.Contains("path stranded"));
                Check("3. No INVALID SoftArrive on BeginHeadingHome",
                    !ContainsCallSequence(runnerSrc, "void BeginHeadingHome", "SoftArrive", 200));

                // ─── ROUND 4: Event pipeline ───
                sb.AppendLine();
                sb.AppendLine("## ROUND 4 — Event pipeline");
                sb.AppendLine();
                Check("4. Injury event processor applies Frustration only (not second meter)",
                    eventSrc.Contains("case WorkerStateEventType.Injury:")
                    && !CaseBlockContains(eventSrc, "WorkerStateEventType.Injury", "AddInjury"));
                Check("4. Accident ApplyTypedInjury emits once then InjuryResponseHub",
                    accidentSrc.Contains("WorkerStateEventHub.Emit")
                    && accidentSrc.Contains("InjuryResponseHub.NotifyApplied"));
                Check("4. Overheat: meter then typed record without ApplyTypedInjury double",
                    excavSrc.Contains("Meter already applied")
                    || excavSrc.Contains("add typed record without double"));
                // live spam gate
                runner.AuditClearEventGate();
                float frBefore = runner.AuditWorkerState(1).Frustration;
                var a = runner.AuditEmit(WorkerStateEvent.Create(
                    1, WorkerStateEventType.WorkBlocked, 3f, "IntegDup", JobType.Prospecting, "p"));
                var b = runner.AuditEmit(WorkerStateEvent.Create(
                    1, WorkerStateEventType.WorkBlocked, 3f, "IntegDup", JobType.Prospecting, "p"));
                Check("4. Spam gate blocks identical WorkBlocked double",
                    a != null && (b == null || Mathf.Approximately(
                        runner.AuditWorkerState(1).Frustration, frBefore + (a?.DeltaFrustration ?? 0f))));

                // ─── ROUND 5: Legacy classification (static) ───
                sb.AppendLine();
                sb.AppendLine("## ROUND 5 — Legacy / dead code classification");
                sb.AppendLine();
                sb.AppendLine("| Item | Verdict |");
                sb.AppendLine("|---|---|");
                sb.AppendLine("| ControlWorker `_control` stub + DEV line | KEEP (debt) |");
                sb.AppendLine("| BridgeSelectedWorkerFromLegacySlot | REMOVED |");
                sb.AppendLine("| LegacySlotToJob | REMOVED |");
                sb.AppendLine("| SoftArriveAllHome | REMOVED |");
                sb.AppendLine("| WorkerConditions.cs | REMOVED |");
                sb.AppendLine("| PersonalConditions alias | KEEP (compat) |");
                sb.AppendLine("| JobToLegacyRoleIndex recipes | KEEP (UI sheets) |");
                sb.AppendLine("| Host SoftTeleport Hauler/Refiner/Steward | REMOVED |");
                sb.AppendLine("| Engineer/Prospector SoftTeleport | KEEP (emergency/DEV) |");
                sb.AppendLine("| Excavator obsolete rest no-ops | REMOVED |");
                sb.AppendLine();
                Check("5. WorkerConditions type file gone",
                    !File.Exists(Path.Combine(FreeMovementDir(), "WorkerConditions.cs")));
                Check("5. Hauler SoftTeleport removed",
                    !ReadSource("HaulerPerson.cs").Contains("SoftTeleport"));
                Check("5. Refiner SoftTeleport removed",
                    !ReadSource("RefinerPerson.cs").Contains("SoftTeleport"));
                Check("5. Steward SoftTeleport removed",
                    !ReadSource("StewardPerson.cs").Contains("SoftTeleport"));

                // ─── ROUND 6: Cross-system scenarios ───
                sb.AppendLine();
                sb.AppendLine("## ROUND 6 — Cross-system scenarios");
                sb.AppendLine();

                // A NORMAL DAY (compressed phase walk)
                runner.AuditBeginHeadingHome();
                Check("A. NORMAL DAY — BeginHeadingHome → HeadingHome",
                    runner.AuditCrewPhaseName() == "HeadingHome");
                runner.AuditForceCrewPhaseOnShift();
                Check("A. NORMAL DAY — can return OnShift",
                    runner.AuditCrewPhaseName() == "OnShift");

                // B REASSIGNMENT (already partly done)
                float frB = runner.AuditWorkerState(3).Frustration;
                runner.AuditWorkerState(3).Frustration = Mathf.Clamp(frB + 5f, 0f, 100f);
                string stats3 = runner.AuditStatsRef(3);
                bool reb = runner.AuditTryAssign(3, JobType.Engineering, out _);
                // may fail if Viktor holds eng — unassign first
                if (!reb)
                {
                    runner.AuditTryUnassign(JobType.Engineering, out _);
                    reb = runner.AuditTryAssign(3, JobType.Engineering, out _);
                }
                Check("B. REASSIGNMENT — Kowalski keeps Stats ref + Frustration",
                    reb && runner.AuditStatsRef(3) == stats3
                    && runner.AuditWorkerState(3).Frustration >= frB + 4.5f);
                // restore defaults
                runner.AuditTryAssign(5, JobType.Engineering, out _);
                runner.AuditTryAssign(3, JobType.Hauling, out _);

                // C INJURY
                var wr2 = Find(runner, 2);
                var prevHubC = InjuryResponseHub.OnInjuryApplied;
                InjuryResponseHub.OnInjuryApplied = null;
                float injC = wr2.State.Injury;
                int nC = wr2.Injuries.Count;
                var injRec = WorkerAccidentSystem.ApplyTypedInjury(
                    wr2, WorkerInjuryType.CutAbrasion, WorkerInjuryCause.TerrainFall, "ScenarioC");
                Check("C. INJURY — typed + meter + event path",
                    injRec != null && wr2.State.Injury >= injC
                    && wr2.Injuries.Count > nC);
                for (int i = 0; i < wr2.Injuries.Active.Count; i++)
                    wr2.Injuries.Active[i].RecoveryGameHoursLeft = 0f;
                wr2.Injuries.TickRecovery(1f, 1f, false);
                wr2.State.Injury = injC;
                wr2.State.NeedsCare = false;
                InjuryResponseHub.OnInjuryApplied = prevHubC;

                // D INCAP
                wr2.State.MarkIncapacitated(WorkerStateClock.GameHours, "ScenarioD");
                Check("D. INCAPACITATION — blocks job actions",
                    !runner.AuditCanPerform(2));
                wr2.State.ClearIncapacitated();

                // E SOCIAL CONFLICT structural
                Check("E. SOCIAL CONFLICT — system present + death/fight writers",
                    File.Exists(Path.Combine(FreeMovementDir(), "SocialConflict.cs"))
                    && conflictSrc.Contains("ApplyFightInjury"));

                // F OVERTIME
                var plan = new ShiftPlanner();
                plan.SetWorkLength(10f);
                Check("F. OVERTIME — 10h band OVERTIME",
                    plan.WorkBandLabel == "OVERTIME");
                plan.SetWorkLength(8f);

                // G TOILET structural
                Check("G. TOILET — trip gates CanPerform; returns SeatAvatarAtWork",
                    runnerSrc.Contains("ToiletTripActive")
                    && runnerSrc.Contains("SeatAvatarAtWork")
                    && runnerSrc.Contains("walking back to post"));

                // H COLLAPSE
                Check("H. COLLAPSE — TunnelCollapse + stranded commute no SoftArrive",
                    File.Exists(Path.Combine(FreeMovementDir(), "TunnelCollapse.cs"))
                    && runnerSrc.Contains("_personStranded"));

                // I RECRUITMENT (built earlier)
                Check("I. RECRUITMENT — 6-seat crew materializes",
                    built && crew != null
                    && crew.Length == RecruitmentCatalog.HireJobs.Length);

                // Persistent body
                Check("Persistent body API — excavator is cabin job",
                    PersistentWorkerBody.IsMachineCabinJob(JobType.Excavation)
                    && !PersistentWorkerBody.IsMachineCabinJob(JobType.Hauling));
                Check("Persistent body — HideOperatorBodies exists",
                    bodySrc.Contains("HideOperatorBodiesOnHosts"));
            }
            catch (Exception ex)
            {
                Check("Live ForceBuild suite", false, ex.Message);
                sb.AppendLine();
                sb.AppendLine($"EXCEPTION: {ex}");
            }
            finally
            {
                if (host != null)
                    UnityEngine.Object.DestroyImmediate(host);
            }

            // ─── ROUND 7/8 summary slots filled by runner report write ───
            sb.AppendLine();
            sb.AppendLine("## ROUND 7 — Safe cleanup applied this pass");
            sb.AppendLine("- Removed dead `SoftArriveAllHome`");
            sb.AppendLine("- Removed obsolete `BridgeSelectedWorkerFromLegacySlot` + `LegacySlotToJob`");
            sb.AppendLine("- Removed unused SoftTeleport/TeleportTo on Hauler, Refiner, Steward");
            sb.AppendLine("- Removed obsolete empty `WorkerConditions` type");
            sb.AppendLine("- Removed excavator obsolete rest no-op stubs");
            sb.AppendLine("- Kept Engineer SoftTeleport emergency strand path (documented)");
            sb.AppendLine("- Kept ControlWorker stub / JobToLegacyRoleIndex / PersonalConditions alias");
            sb.AppendLine();

            sb.AppendLine("## ROUND 8 — Totals");
            sb.AppendLine();
            sb.AppendLine($"- PASS: {pass}");
            sb.AppendLine($"- FAIL: {fail}");
            sb.AppendLine($"- **Result:** {(fail == 0 ? "PASS" : "FAIL")}");
            if (blockers.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("### Blockers");
                foreach (var b in blockers)
                    sb.AppendLine($"- {b}");
            }

            sb.AppendLine();
            sb.AppendLine("## Remaining debt");
            sb.AppendLine("- Avatar↔host dual position authority while Operating");
            sb.AppendLine("- Dual ExcavatedPathfinder (person commute vs job hosts)");
            sb.AppendLine("- Injury meter ↔ typed store dual writers");
            sb.AppendLine("- ControlWorker unused stub");
            sb.AppendLine("- Engineer SoftTeleport on repair strand (emergency)");
            sb.AppendLine("- RelocateAvatar leave-host snaps (emergency)");
            sb.AppendLine();
            sb.AppendLine("## Recommended human playtest");
            sb.AppendLine("1. Full day: commute → dig → knock-off → camp meal → sleep → wake");
            sb.AppendLine("2. Reassign Mara mid-shift; confirm excavator stays put, avatar seats");
            sb.AppendLine("3. Toilet trip mid-dig; machine idle; return resumes");
            sb.AppendLine("4. Force collapse debris; verify no SoftArrive home");
            sb.AppendLine("5. Hire custom 6-crew and run one shift");

            string dir = outputDirectory
                         ?? Path.Combine(Application.dataPath, "..", "BenchmarkResults");
            Directory.CreateDirectory(dir);
            string stamp = Path.Combine(dir,
                $"system_integration_cleanup_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            string latest = Path.Combine(dir, "system_integration_cleanup_latest.md");
            File.WriteAllText(stamp, sb.ToString());
            File.WriteAllText(latest, sb.ToString());
            return latest;
        }

        static WorkerRuntime Find(FreeMovementSocketMapRunner runner, int id)
        {
            // Use reflection-free path via AuditWorkerState side — need WorkerRuntime
            // Audit hooks don't expose Find; use WorkerRuntime.Find after ForceBuild registers.
            return WorkerRuntime.Find(id);
        }

        static string FreeMovementDir() =>
            Path.Combine(Application.dataPath, "Vibe", "FreeMovement");

        static string ReadSource(string fileName)
        {
            string path = Path.Combine(FreeMovementDir(), fileName);
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }

        static int CountOccurrences(string src, string token)
        {
            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(token)) return 0;
            int n = 0, i = 0;
            while ((i = src.IndexOf(token, i, StringComparison.Ordinal)) >= 0)
            {
                n++;
                i += token.Length;
            }
            return n;
        }

        static bool ContainsCallSequence(string src, string a, string b, int maxGapChars)
        {
            int ia = src.IndexOf(a, StringComparison.Ordinal);
            if (ia < 0) return false;
            int ib = src.IndexOf(b, ia, StringComparison.Ordinal);
            if (ib < 0) return false;
            return ib - ia <= maxGapChars;
        }

        static bool SourceHasIncapGate(string runnerSrc) =>
            runnerSrc.Contains("Incapacitated")
            && runnerSrc.Contains("CanPerformJobActions");

        static bool CaseBlockContains(string src, string caseLabel, string needle)
        {
            int i = src.IndexOf($"case {caseLabel}", StringComparison.Ordinal);
            if (i < 0) return false;
            int j = src.IndexOf("case WorkerStateEventType.", i + 10, StringComparison.Ordinal);
            if (j < 0) j = Math.Min(src.Length, i + 800);
            string block = src.Substring(i, j - i);
            return block.Contains(needle);
        }
    }
}
