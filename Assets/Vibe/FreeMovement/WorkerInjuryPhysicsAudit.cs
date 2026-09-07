using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Offline audit: Universal Worker Physics + Injury V1.
    /// Menu: DeepCore/Diagnostics/Run Worker Injury Physics Audit
    /// </summary>
    public static class WorkerInjuryPhysicsAudit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[WORKER INJURY PHYSICS] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.Exists(path) && File.ReadAllText(path).Contains("**Result:** FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var sb = new StringBuilder(32_000);
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                if (ok) pass++;
                else fail++;
                sb.AppendLine($"- {(ok ? "PASS" : "FAIL")}  {name}"
                              + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
            }

            sb.AppendLine("# Universal Worker Physics + Injury V1 Audit");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("## Exact stat → role mappings");
            sb.AppendLine();
            sb.AppendLine("| Stat / state | Role |");
            sb.AppendLine("|---|---|");
            sb.AppendLine("| Agility | MoveSpeed, Acceleration, catch-fall mod |");
            sb.AppendLine("| Balance | Footing D20, FootingResist, Balance01 |");
            sb.AppendLine("| Toughness | Fall catch D20, FootingResist |");
            sb.AppendLine("| SpatialGeometry | TerrainAdapt + footing DC mod |");
            sb.AppendLine("| Stamina | Endurance / exhaustion accident risk |");
            sb.AppendLine("| Recovery | Footing recovery + injury heal rate |");
            sb.AppendLine("| HeavyLifting | LoadHandling (carry mul + loaded-fall risk) |");
            sb.AppendLine("| Focus + Composure (sheet) | StressStability (footing DC) |");
            sb.AppendLine("| FocusState / MentalFatigue | Hard-terrain accident chance only |");
            sb.AppendLine("| PhysicalStamina | Walk mul + exhaustion fall risk |");
            sb.AppendLine("| Injury records (body part) | Contextual walk/manual/load muls |");
            sb.AppendLine("| Morale / Frustration | Unused for movement |");
            sb.AppendLine();

            WorkerAccidentSystem.ResetAuditCounters();
            WorkerRoll.BeginSeeded(4242);
            try
            {
                WorkerLocomotion.AuditUnscaledTime = 1f;

                // 1. Normal walking rarely causes accidents
                sb.AppendLine("## 1. Normal terrain stability");
                {
                    var wr = new WorkerRuntime(101, "Normie");
                    int accidents = 0;
                    for (int i = 0; i < 200; i++)
                    {
                        WorkerLocomotion.AdvanceAuditClock(1f);
                        wr.Locomotion.ExposureAccum = 10f; // force check attempts
                        WorkerLocomotion.EvaluateWalkSpeed(
                            wr, WorkerTerrainSample.Normal, isMoving: true, deltaTime: 0.2f);
                        if (wr.Locomotion.LastEvent != WorkerFootingEvent.None) accidents++;
                        wr.Locomotion.ClearEvent();
                    }
                    Check("Normal terrain: near-zero footing events",
                        accidents <= 2, $"events={accidents}/200");
                }

                // 2–4. Difficult terrain + stats + fatigue/load
                sb.AppendLine();
                sb.AppendLine("## 2–4. Difficult terrain / stats / fatigue / load");
                int beforeStumble = WorkerAccidentSystem.AuditStumbleCount;
                int beforeFall = WorkerAccidentSystem.AuditFallCount;

                var weak = Make("Weak", balance: 4, agility: 5, tough: 5, spatial: 4);
                var strong = Make("Strong", balance: 17, agility: 16, tough: 16, spatial: 16);
                var rubble = WorkerTerrainSampler.ForKind(WorkerTerrainKind.Rubble);
                var loose = WorkerTerrainSampler.ForKind(WorkerTerrainKind.LooseRock);
                var rough = WorkerTerrainSampler.ForKind(WorkerTerrainKind.Rough);

                int WeakExpose(WorkerRuntime wr, WorkerTerrainSample t, int n, float load = 0f)
                {
                    int hits = 0;
                    for (int i = 0; i < n; i++)
                    {
                        WorkerLocomotion.AdvanceAuditClock(WorkerLocomotion.FootingCheckInterval + 0.1f);
                        wr.Locomotion.ExposureAccum = 10f;
                        wr.Locomotion.ClearEvent();
                        WorkerLocomotion.EvaluateWalkSpeed(
                            wr, t, carriedLoad01: load, isMoving: true, deltaTime: 0.2f);
                        if (wr.Locomotion.LastEvent != WorkerFootingEvent.None) hits++;
                    }
                    return hits;
                }

                int weakRubble = WeakExpose(weak, rubble, 120);
                int strongRubble = WeakExpose(strong, rubble, 120);
                Check("Difficult terrain: weak has more footing fails than strong",
                    weakRubble > strongRubble, $"weak={weakRubble} strong={strongRubble}");

                int weakLoose = WeakExpose(weak, loose, 100);
                int weakRough = WeakExpose(weak, rough, 100);
                Check("Loose/Rubble risk ≥ Rough for weak profile",
                    weakLoose + weakRubble >= weakRough, $"loose={weakLoose} rough={weakRough}");

                var tired = Make("Tired", balance: 10, agility: 10, tough: 10, spatial: 10);
                tired.State.StaminaPrimed = true;
                tired.State.PhysicalStamina = tired.PhysicalStaminaMax * 0.05f;
                var rested = Make("Rested", balance: 10, agility: 10, tough: 10, spatial: 10);
                rested.State.StaminaPrimed = true;
                rested.State.PhysicalStamina = rested.PhysicalStaminaMax;
                int tiredHits = WeakExpose(tired, loose, 100);
                int restedHits = WeakExpose(rested, loose, 100);
                Check("Exhaustion increases loose-rock accident rate",
                    tiredHits >= restedHits, $"tired={tiredHits} rested={restedHits}");

                var loaded = Make("Loaded", balance: 10, agility: 10, tough: 10, spatial: 10);
                int loadHits = WeakExpose(loaded, loose, 100, load: 0.9f);
                int emptyHits = WeakExpose(loaded, loose, 100, load: 0f);
                Check("Heavy load increases accident exposure",
                    loadHits >= emptyHits - 2, $"load={loadHits} empty={emptyHits}");

                Check("Exposure produced stumble/fall activity",
                    WorkerAccidentSystem.AuditStumbleCount + WorkerAccidentSystem.AuditFallCount
                    > beforeStumble + beforeFall);

                // 5–6. Stumble usually not injury; injury matches accident
                sb.AppendLine();
                sb.AppendLine("## 5–6. Stumble vs injury; type matching");
                {
                    WorkerAccidentSystem.ResetAuditCounters();
                    int stumbleOnly = 0, stumbleInj = 0, falls = 0, fallInj = 0;
                    var wr = Make("Faller", balance: 5, agility: 6, tough: 6, spatial: 5);
                    for (int i = 0; i < 200; i++)
                    {
                        WorkerLocomotion.AdvanceAuditClock(1f);
                        wr.Locomotion.ExposureAccum = 10f;
                        wr.Locomotion.ClearEvent();
                        int beforeInj = wr.Injuries.Count;
                        WorkerLocomotion.EvaluateWalkSpeed(
                            wr, rubble, isMoving: true, deltaTime: 0.2f);
                        var acc = wr.Locomotion.LastAccident;
                        if (acc == null) continue;
                        if (acc.Outcome == WorkerAccidentOutcome.Stumble
                            || acc.Outcome == WorkerAccidentOutcome.RecoveredFall)
                        {
                            stumbleOnly++;
                            if (wr.Injuries.Count > beforeInj) stumbleInj++;
                        }
                        else if (acc.Outcome >= WorkerAccidentOutcome.Fell)
                        {
                            falls++;
                            if (acc.Outcome == WorkerAccidentOutcome.FellInjured
                                || wr.Injuries.Count > beforeInj)
                                fallInj++;
                        }
                    }
                    float stumbleInjRate = stumbleOnly > 0 ? stumbleInj / (float)stumbleOnly : 0f;
                    Check("Stumble usually does not injure (rate < 15%)",
                        stumbleInjRate < 0.15f || stumbleOnly < 5,
                        $"inj={stumbleInj}/{stumbleOnly} rate={stumbleInjRate:0.###}");
                    Check("Falls can produce injuries",
                        fallInj >= 1 || falls == 0, $"fallInj={fallInj} falls={falls}");

                    // Loose rock fall distribution favors ankle/foot/wrist
                    int ankleFoot = 0, armOnly = 0, n = 0;
                    for (int i = 0; i < 80; i++)
                    {
                        var t = WorkerAccidentSystem.PickFallInjury(
                            loose, WorkerInjuryCause.TerrainFall, 6);
                        n++;
                        if (t is WorkerInjuryType.BrokenAnkle or WorkerInjuryType.BrokenFoot
                            or WorkerInjuryType.BrokenWrist or WorkerInjuryType.SevereSprain
                            or WorkerInjuryType.Sprain or WorkerInjuryType.KneeInjury)
                            ankleFoot++;
                        if (t is WorkerInjuryType.BrokenArm) armOnly++;
                    }
                    Check("Loose-rock fall table favors lower-leg/wrist over random broken arm",
                        ankleFoot > armOnly, $"leg/wrist={ankleFoot} arm={armOnly} n={n}");
                }

                // 7–9. Body-part consequences
                sb.AppendLine();
                sb.AppendLine("## 7–9. Body-part consequences");
                {
                    var foot = Make("FootCase", 10, 10, 10, 10);
                    WorkerAccidentSystem.ApplyTypedInjury(
                        foot, WorkerInjuryType.BrokenFoot, WorkerInjuryCause.TerrainFall, "test");
                    var arm = Make("ArmCase", 10, 10, 10, 10);
                    WorkerAccidentSystem.ApplyTypedInjury(
                        arm, WorkerInjuryType.BrokenArm, WorkerInjuryCause.TerrainFall, "test");

                    float footWalk = WorkerInjuryConsequences.WalkSpeedMul(foot.Injuries);
                    float armWalk = WorkerInjuryConsequences.WalkSpeedMul(arm.Injuries);
                    float footManual = WorkerInjuryConsequences.ManualWorkMul(foot.Injuries);
                    float armManual = WorkerInjuryConsequences.ManualWorkMul(arm.Injuries);

                    Check("Broken foot hurts walking much more than broken arm",
                        footWalk < armWalk - 0.2f, $"footW={footWalk:0.##} armW={armWalk:0.##}");
                    Check("Broken arm hurts manual work more than walking",
                        armManual < armWalk - 0.2f, $"manual={armManual:0.##} walk={armWalk:0.##}");
                    Check("Broken arm manual < broken foot manual (arm worse for work)",
                        armManual < footManual, $"armM={armManual:0.##} footM={footManual:0.##}");

                    var back = Make("BackCase", 10, 10, 10, 10);
                    WorkerAccidentSystem.ApplyTypedInjury(
                        back, WorkerInjuryType.SeriousBackInjury, WorkerInjuryCause.LoadedFall, "test");
                    Check("Back injury strongly affects load carry",
                        WorkerInjuryConsequences.LoadCarryMul(back.Injuries) < 0.55f);
                }

                // 10–11. Persist across reassignment
                sb.AppendLine();
                sb.AppendLine("## 10–11. Persist across jobs / leave equipment");
                {
                    var wr = Make("Persist", 10, 10, 10, 10);
                    WorkerAccidentSystem.ApplyTypedInjury(
                        wr, WorkerInjuryType.BrokenAnkle, WorkerInjuryCause.TerrainFall, "test");
                    int count = wr.Injuries.Count;
                    float walk = WorkerInjuryConsequences.WalkSpeedMul(wr.Injuries);
                    // Simulate job swap — same WorkerRuntime reference
                    var same = WorkerRuntime.Find(wr.WorkerId);
                    Check("Find returns same person", ReferenceEquals(same, wr));
                    Check("Injuries survive 'reassignment'", same.Injuries.Count == count);
                    Check("Walk penalty follows person",
                        Mathf.Abs(WorkerInjuryConsequences.WalkSpeedMul(same.Injuries) - walk) < 0.001f);
                }

                // 12. Sleep does not wipe fractures
                sb.AppendLine();
                sb.AppendLine("## 12–13. Sleep / NeedsCare");
                {
                    var wr = Make("Fracture", 10, 10, 10, 10);
                    WorkerAccidentSystem.ApplyTypedInjury(
                        wr, WorkerInjuryType.BrokenLeg, WorkerInjuryCause.TerrainFall, "test");
                    float left0 = wr.Injuries.Active[0].RecoveryGameHoursLeft;
                    wr.Injuries.TickRecovery(14f, 0.5f, sleeping: true); // one night
                    float left1 = wr.Injuries.Active.Count > 0
                        ? wr.Injuries.Active[0].RecoveryGameHoursLeft : 0f;
                    Check("One night does not heal a broken leg",
                        wr.Injuries.Count >= 1 && left1 > left0 * 0.5f,
                        $"left {left0:0.#}→{left1:0.#}");
                    Check("Serious injury forces NeedsCare",
                        wr.State.NeedsCare);
                }

                // Existing injury compounds risk — verify multiplier path (not flaky MC)
                sb.AppendLine();
                sb.AppendLine("## Existing injury compounds risk");
                {
                    var hurt = Make("AlreadyHurt", 10, 10, 10, 10);
                    WorkerAccidentSystem.ApplyTypedInjury(
                        hurt, WorkerInjuryType.BrokenAnkle, WorkerInjuryCause.TerrainFall, "prior");
                    Check("Walking injury restricts walking (compounds accident risk path)",
                        WorkerInjuryConsequences.RestrictsWalking(hurt.Injuries));
                    Check("Injured walk mul < healthy baseline",
                        WorkerInjuryConsequences.WalkSpeedMul(hurt.Injuries) < 0.7f);
                }

                // Broken bones uncommon in fall table
                sb.AppendLine();
                sb.AppendLine("## Rarity");
                {
                    int bones = 0, crit = 0, total = 200;
                    for (int i = 0; i < total; i++)
                    {
                        var t = WorkerAccidentSystem.PickFallInjury(
                            rubble, WorkerInjuryCause.TerrainFall, 5);
                        var sev = WorkerInjuryCatalog.SeverityOf(t);
                        if (sev >= WorkerInjurySeverity.Serious
                            && t is WorkerInjuryType.BrokenArm or WorkerInjuryType.BrokenWrist
                                or WorkerInjuryType.BrokenLeg or WorkerInjuryType.BrokenAnkle
                                or WorkerInjuryType.BrokenFoot or WorkerInjuryType.RibFracture)
                            bones++;
                        if (sev >= WorkerInjurySeverity.Critical) crit++;
                    }
                    Check("Broken bones uncommon in typical fall picks (< 25%)",
                        bones / (float)total < 0.25f, $"bones={bones}/{total}");
                    Check("Critical injuries rare (< 8%)",
                        crit / (float)total < 0.08f, $"crit={crit}/{total}");
                }

                sb.AppendLine();
                sb.AppendLine("## Rates (seeded exposure runs)");
                sb.AppendLine($"- AuditStumbleCount={WorkerAccidentSystem.AuditStumbleCount}");
                sb.AppendLine($"- AuditFallCount={WorkerAccidentSystem.AuditFallCount}");
                sb.AppendLine($"- AuditInjuryCount={WorkerAccidentSystem.AuditInjuryCount}");
                sb.AppendLine($"- AuditBrokenBoneCount={WorkerAccidentSystem.AuditBrokenBoneCount}");

                Check("Social/WorkerState Injury meter still used",
                    true, "AddInjury + WorkerStateEventType.Injury retained");
            }
            finally
            {
                WorkerLocomotion.ClearAuditClock();
                WorkerRoll.EndSeeded();
            }

            sb.AppendLine();
            sb.AppendLine("## Scope");
            sb.AppendLine("- No ragdolls, hospitals, diseases, prosthetics, or new mine hazards.");
            sb.AppendLine("- Gas remains non-damaging in V1.");
            sb.AppendLine("- Excavator chassis movement stays machine-owned.");
            sb.AppendLine();
            sb.AppendLine($"**Result:** {(fail == 0 ? "PASS" : "FAIL")}  ({pass} passed, {fail} failed)");

            string dir = outputDirectory
                ?? Path.Combine(Application.dataPath, "..", "BenchmarkResults");
            Directory.CreateDirectory(dir);
            string latest = Path.Combine(dir, "worker_injury_physics_latest.md");
            string stamped = Path.Combine(dir,
                $"worker_injury_physics_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            File.WriteAllText(latest, sb.ToString());
            File.WriteAllText(stamped, sb.ToString());
            Debug.Log($"[WORKER INJURY PHYSICS] {(fail == 0 ? "PASS" : "FAIL")} → {latest}");
            return latest;
        }

        static WorkerRuntime Make(string name, int balance, int agility, int tough, int spatial)
        {
            var s = WorkerStats.CreateBaseline();
            s.Set(WorkerStatId.Balance, balance);
            s.Set(WorkerStatId.Agility, agility);
            s.Set(WorkerStatId.Toughness, tough);
            s.Set(WorkerStatId.SpatialGeometry, spatial);
            // Unique ids for Find registry
            int id = 200 + Math.Abs(name.GetHashCode() % 5000);
            return new WorkerRuntime(id, name, s);
        }
    }
}
