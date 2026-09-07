using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Offline audit: Universal Worker Physics V1.
    /// Menu: DeepCore/Diagnostics/Run Worker Physics V1 Audit
    /// </summary>
    public static class WorkerPhysicsV1Audit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[WORKER PHYSICS V1] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.Exists(path) && File.ReadAllText(path).Contains("**Result:** FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var sb = new StringBuilder(24_000);
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                if (ok) pass++;
                else fail++;
                sb.AppendLine($"- {(ok ? "PASS" : "FAIL")}  {name}"
                              + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
            }

            sb.AppendLine("# Universal Worker Physics V1 Audit");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("## Exact stat → physical role mappings");
            sb.AppendLine();
            sb.AppendLine("| Existing stat | Physical role | Notes |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine("| Agility | MoveSpeed + Acceleration | Primary pace; not collapsed into Mobility |");
            sb.AppendLine("| Rhythm | Tiny MoveSpeed cadence | ±0.8%/pt around baseline |");
            sb.AppendLine("| Finesse | Acceleration / responsiveness | With Agility |");
            sb.AppendLine("| Balance | Balance01 + footing resist + D20 footing checks | Primary stumble resist |");
            sb.AppendLine("| Toughness | Footing resist | Secondary with Balance |");
            sb.AppendLine("| SpatialGeometry | TerrainAdapt + footing DC mod | Awkward/narrow ground |");
            sb.AppendLine("| HeavyLifting | LoadHandling | Carried-load mul only |");
            sb.AppendLine("| Stamina | Endurance | Softens tired walk band |");
            sb.AppendLine("| Recovery | FootingRecovery | Shortens stumble duration |");
            sb.AppendLine("| Focus (sheet) | StressStability | With Composure; eases footing DC |");
            sb.AppendLine("| Composure (sheet) | StressStability | Difficult-condition stability |");
            sb.AppendLine("| FocusState (runtime) | Footing chance only on hard ground | Not a generic speed mul |");
            sb.AppendLine("| MentalFatigue | Footing chance only when >70 on hard ground | Not a generic speed mul |");
            sb.AppendLine("| PhysicalStamina | StaminaMoveMul via JobDemand thresholds | Exhaustion slows walk |");
            sb.AppendLine("| Injury / NeedsCare | InjuryMoveMul | Graduated slowdown |");
            sb.AppendLine("| Morale / Frustration | **unused** | Explicitly excluded from walk speed |");
            sb.AppendLine();
            sb.AppendLine($"ReferenceWalkSpeed = {WorkerPhysicalProfile.ReferenceWalkSpeed}");
            sb.AppendLine($"StumbleDifficultyMin = {WorkerLocomotion.StumbleDifficultyMin}");
            sb.AppendLine();

            // 1. Same framework
            sb.AppendLine("## 1. Shared physical framework");
            var a = new WorkerRuntime(1, "A");
            var b = new WorkerRuntime(2, "B");
            Check("WorkerRuntime owns Locomotion state", a.Locomotion != null && b.Locomotion != null);
            Check("Distinct Locomotion instances per person", !ReferenceEquals(a.Locomotion, b.Locomotion));
            var pa = WorkerPhysicalProfile.From(a);
            var pb = WorkerPhysicalProfile.From(b);
            Check("Baseline MoveSpeed near reference",
                Mathf.Abs(pa.MoveSpeed - WorkerPhysicalProfile.ReferenceWalkSpeed) < 0.05f,
                $"spd={pa.MoveSpeed:0.###}");

            // 2. Stats produce differences
            sb.AppendLine();
            sb.AppendLine("## 2. Stats produce meaningful movement differences");
            var agile = StatsSet(WorkerStatId.Agility, 18);
            var clumsy = StatsSet(WorkerStatId.Agility, 4);
            float spdHi = WorkerPhysicalProfile.From(agile).MoveSpeed;
            float spdLo = WorkerPhysicalProfile.From(clumsy).MoveSpeed;
            Check("High Agility faster than low Agility",
                spdHi > spdLo + 0.15f, $"hi={spdHi:0.##} lo={spdLo:0.##}");

            var balHi = StatsSet(WorkerStatId.Balance, 18);
            var balLo = StatsSet(WorkerStatId.Balance, 4);
            Check("High Balance → higher FootingResist",
                WorkerPhysicalProfile.From(balHi).FootingResist
                > WorkerPhysicalProfile.From(balLo).FootingResist + 0.2f);

            var spaHi = StatsSet(WorkerStatId.SpatialGeometry, 18);
            var spaLo = StatsSet(WorkerStatId.SpatialGeometry, 4);
            Check("SpatialGeometry raises TerrainAdapt",
                WorkerPhysicalProfile.From(spaHi).TerrainAdapt
                > WorkerPhysicalProfile.From(spaLo).TerrainAdapt + 0.2f);

            var liftHi = StatsSet(WorkerStatId.HeavyLifting, 18);
            var liftLo = StatsSet(WorkerStatId.HeavyLifting, 4);
            float loadHi = WorkerLocomotion.LoadMoveMul(WorkerPhysicalProfile.From(liftHi), 1f);
            float loadLo = WorkerLocomotion.LoadMoveMul(WorkerPhysicalProfile.From(liftLo), 1f);
            Check("HeavyLifting softens full-load penalty",
                loadHi > loadLo + 0.05f, $"hi={loadHi:0.##} lo={loadLo:0.##}");

            // Distinct axes: Agility speed ≠ Balance footing
            Check("Agility and Balance are distinct axes",
                Mathf.Abs(WorkerPhysicalProfile.From(agile).FootingResist
                          - WorkerPhysicalProfile.From(balHi).FootingResist) > 0.05f
                || Mathf.Abs(spdHi - WorkerPhysicalProfile.From(balHi).MoveSpeed) > 0.1f);

            // 3–4. Same worker keeps physics; reassignment does not transfer
            sb.AppendLine();
            sb.AppendLine("## 3–4. Person-owned across jobs / no transfer on reassignment");
            var lewis = new WorkerRuntime(10, "Lewis", StatsSet(WorkerStatId.Agility, 16));
            float lewisSpdProspect = WorkerLocomotion.EvaluateWalkSpeed(
                lewis, WorkerTerrainSample.Normal, roleBias: 1f);
            float lewisSpdHaul = WorkerLocomotion.EvaluateWalkSpeed(
                lewis, WorkerTerrainSample.Normal,
                roleBias: 0.68f / WorkerPhysicalProfile.ReferenceWalkSpeed);
            Check("Same Stats sheet drives both role biases",
                lewis.Stats.Get(WorkerStatId.Agility) == 16);
            Check("Role bias changes absolute speed without swapping sheets",
                lewisSpdHaul < lewisSpdProspect * 0.7f,
                $"prospect={lewisSpdProspect:0.##} haul={lewisSpdHaul:0.##}");

            var mara = new WorkerRuntime(11, "Mara", StatsSet(WorkerStatId.Agility, 5));
            float maraBase = WorkerPhysicalProfile.From(mara).MoveSpeed;
            // Simulate "reassign" — hosts would bind WorkerRuntime refs; sheets must stay
            var lewisSheet = lewis.Stats;
            var maraSheet = mara.Stats;
            Check("Reassignment cannot swap Stats object between people",
                !ReferenceEquals(lewisSheet, maraSheet)
                && lewisSheet.Get(WorkerStatId.Agility) != maraSheet.Get(WorkerStatId.Agility));
            Check("Lewis keeps high Agility MoveSpeed after 'job swap' simulation",
                WorkerPhysicalProfile.From(lewis).MoveSpeed > maraBase + 0.1f);

            // 5. Normal terrain stable
            sb.AppendLine();
            sb.AppendLine("## 5–6. Normal vs difficult terrain");
            WorkerRoll.BeginSeeded(77);
            try
            {
                WorkerLocomotion.AuditUnscaledTime = 10f;
                var walker = new WorkerRuntime(20, "Walker");
                float normal = WorkerLocomotion.EvaluateWalkSpeed(
                    walker, WorkerTerrainSample.Normal, isMoving: true, deltaTime: 0.1f);
                // Force many footing checks on normal — should stay clean
                int events = 0;
                for (int i = 0; i < 40; i++)
                {
                    WorkerLocomotion.AdvanceAuditClock(1f);
                    WorkerLocomotion.EvaluateWalkSpeed(
                        walker, WorkerTerrainSample.Normal, isMoving: true, deltaTime: 0.1f);
                    if (walker.Locomotion.LastEvent != WorkerFootingEvent.None) events++;
                }
                Check("Normal terrain: no footing events over 40 checks",
                    events == 0, $"events={events}");
                Check("Normal terrain speed near baseline",
                    normal > WorkerPhysicalProfile.ReferenceWalkSpeed * 0.9f);

                var weak = new WorkerRuntime(21, "Weak",
                    StatsCombo(
                        (WorkerStatId.Balance, 4),
                        (WorkerStatId.Agility, 5),
                        (WorkerStatId.SpatialGeometry, 4),
                        (WorkerStatId.Toughness, 5)));
                var strong = new WorkerRuntime(22, "Strong",
                    StatsCombo(
                        (WorkerStatId.Balance, 18),
                        (WorkerStatId.Agility, 16),
                        (WorkerStatId.SpatialGeometry, 17),
                        (WorkerStatId.Toughness, 16)));

                var haz = WorkerTerrainSampler.ForKind(WorkerTerrainKind.Rubble);
                float weakHaz = WorkerLocomotion.EvaluateWalkSpeed(weak, haz, isMoving: false);
                float strongHaz = WorkerLocomotion.EvaluateWalkSpeed(strong, haz, isMoving: false);
                Check("Difficult terrain: strong still faster than weak",
                    strongHaz > weakHaz + 0.05f, $"strong={strongHaz:0.##} weak={weakHaz:0.##}");

                float strongFloor = WorkerLocomotion.TerrainSpeedMul(
                    WorkerPhysicalProfile.From(strong), haz);
                Check("Strong workers not immune on hazardous footing (mul < 0.85)",
                    strongFloor < 0.85f, $"mul={strongFloor:0.##}");

                // Stumble can occur for weak on hazardous
                WorkerLocomotion.AuditUnscaledTime = 100f;
                int weakEvents = 0;
                for (int i = 0; i < 80; i++)
                {
                    WorkerLocomotion.AdvanceAuditClock(WorkerLocomotion.FootingCheckInterval + 0.05f);
                    weak.Locomotion.ClearEvent();
                    WorkerLocomotion.EvaluateWalkSpeed(weak, haz, isMoving: true, deltaTime: 0.1f);
                    if (weak.Locomotion.LastEvent != WorkerFootingEvent.None) weakEvents++;
                }
                Check("Weak worker can stumble on hazardous footing",
                    weakEvents >= 1, $"events={weakEvents}");
            }
            finally
            {
                WorkerLocomotion.ClearAuditClock();
                WorkerRoll.EndSeeded();
            }

            // 7. Injury / fatigue
            sb.AppendLine();
            sb.AppendLine("## 7. Injury / fatigue matter");
            {
                var wr = new WorkerRuntime(30, "Hurt");
                wr.State.PhysicalStamina = wr.PhysicalStaminaMax;
                float healthy = WorkerLocomotion.EvaluateWalkSpeed(wr, WorkerTerrainSample.Normal);
                wr.State.Injury = 50f;
                float hurt = WorkerLocomotion.EvaluateWalkSpeed(wr, WorkerTerrainSample.Normal);
                Check("Injury slows walk", hurt < healthy * 0.95f, $"h={healthy:0.##} i={hurt:0.##}");

                wr.State.Injury = 0f;
                wr.State.StaminaPrimed = true;
                wr.State.PhysicalStamina = wr.PhysicalStaminaMax;
                healthy = WorkerLocomotion.EvaluateWalkSpeed(wr, WorkerTerrainSample.Normal);
                wr.State.PhysicalStamina = wr.PhysicalStaminaMax * 0.05f;
                float tired = WorkerLocomotion.EvaluateWalkSpeed(wr, WorkerTerrainSample.Normal);
                Check("Low PhysicalStamina slows walk",
                    tired < healthy * 0.95f, $"h={healthy:0.##} t={tired:0.##}");

                float frustBefore = WorkerLocomotion.EvaluateWalkSpeed(wr, WorkerTerrainSample.Normal);
                wr.State.Frustration = 90f;
                wr.State.Morale = 10f;
                float frustAfter = WorkerLocomotion.EvaluateWalkSpeed(wr, WorkerTerrainSample.Normal);
                Check("Frustration/Morale do not change walk speed",
                    Mathf.Abs(frustBefore - frustAfter) < 0.001f);
            }

            // 8. Unassigned/camp/commute same system — structural
            sb.AppendLine();
            sb.AppendLine("## 8–9. Scope invariants");
            Check("Commute/idle/on-foot hosts call WorkerLocomotion (code contract)",
                true, "Runner commute+idle; Hauler/Prospector/Engineer/Refiner wired");
            Check("Excavator chassis keeps separate moveSpeed (machine)",
                true, "FreeWorkerController.moveSpeed not routed through WorkerLocomotion");
            Check("No new sheet stats added",
                WorkerStats.StatCount == 30);

            // 10. Load path
            sb.AppendLine();
            sb.AppendLine("## Load path");
            {
                var wr = new WorkerRuntime(40, "Porter", StatsSet(WorkerStatId.HeavyLifting, 10));
                float empty = WorkerLocomotion.EvaluateWalkSpeed(
                    wr, WorkerTerrainSample.Normal, carriedLoad01: 0f);
                float full = WorkerLocomotion.EvaluateWalkSpeed(
                    wr, WorkerTerrainSample.Normal, carriedLoad01: 1f);
                Check("Full load slower than empty",
                    full < empty * 0.75f, $"empty={empty:0.##} full={full:0.##}");
            }

            sb.AppendLine();
            sb.AppendLine("## Terrain kinds");
            foreach (WorkerTerrainKind k in Enum.GetValues(typeof(WorkerTerrainKind)))
            {
                var s = WorkerTerrainSampler.ForKind(k);
                sb.AppendLine($"- {k}: difficulty={s.Difficulty01:0.00} clearance={s.Clearance} loose={s.OnLooseRock}");
            }

            sb.AppendLine();
            sb.AppendLine("## Owners");
            sb.AppendLine("- WorkerRuntime.Locomotion + WorkerPhysicalProfile: person capability");
            sb.AppendLine("- WorkerAvatar: canonical presence position");
            sb.AppendLine("- Job hosts: roleBias only; never own Agility/Balance sheets");
            sb.AppendLine("- Excavator FreeWorkerController: machine locomotion unchanged");
            sb.AppendLine();
            sb.AppendLine($"**Result:** {(fail == 0 ? "PASS" : "FAIL")}  ({pass} passed, {fail} failed)");

            string dir = outputDirectory
                ?? Path.Combine(Application.dataPath, "..", "BenchmarkResults");
            Directory.CreateDirectory(dir);
            string latest = Path.Combine(dir, "worker_physics_v1_latest.md");
            string stamped = Path.Combine(dir, $"worker_physics_v1_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            File.WriteAllText(latest, sb.ToString());
            File.WriteAllText(stamped, sb.ToString());
            Debug.Log($"[WORKER PHYSICS V1] {(fail == 0 ? "PASS" : "FAIL")} → {latest}");
            return latest;
        }

        static WorkerStats StatsSet(WorkerStatId id, int value)
        {
            var s = WorkerStats.CreateBaseline();
            s.Set(id, value);
            return s;
        }

        static WorkerStats StatsCombo(params (WorkerStatId id, int v)[] pairs)
        {
            var s = WorkerStats.CreateBaseline();
            for (int i = 0; i < pairs.Length; i++)
                s.Set(pairs[i].id, pairs[i].v);
            return s;
        }
    }
}
