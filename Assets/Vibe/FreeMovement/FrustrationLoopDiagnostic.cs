using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Offline 5-day Frustration loop diagnostic — no Social Aura retune.
    /// Models OnShift daytime decay, sleep relief, Prospector dry-spell,
    /// and frequent Excavator ProgressSuccess under the softened relief rule.
    /// Batch / menu: FrustrationLoopDiagnostic.RunFromEditor
    /// </summary>
    public static class FrustrationLoopDiagnostic
    {
        const float ShiftGameHours = 10f;
        const float DayStepHours = 0.25f;
        /// <summary>Excavator micro-progress cadence (game-hours), matching spam-gate scale.</summary>
        const float ExcavatorProgressIntervalHours = 0.25f;
        const float ExcavatorProgressMagnitude = 2f; // FreeWorkerController.FrustrationProgressBaseRelief

        static readonly string[] Names =
            { "Lewis", "Mara", "Kowalski", "Elena", "Viktor" };

        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[FRUSTRATION LOOP] Report: {path}");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var log = new StringBuilder(24_000);
            log.AppendLine("# Frustration Loop Diagnostic — 5 dry days");
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine();
            log.AppendLine("Scope: Frustration persistence only. Social Aura / Morale retune excluded.");
            log.AppendLine("Scenario: no Discovery for 5 days; Prospecting dry-spell active; Excavator gets frequent ProgressSuccess.");
            log.AppendLine();
            log.AppendLine("## Tunables under test");
            log.AppendLine($"- Daytime FrustrationDecayPerGameHour = {WorkerStateDaytimeRecovery.FrustrationDecayPerGameHour}");
            log.AppendLine($"- Sleep FrustrationRelief = {WorkerSleepRecovery.FrustrationRelief}");
            log.AppendLine($"- ProgressSuccess relief = min(BaseGain×{WorkerStateEventProcessor.ProgressSuccessReliefScale}, {WorkerStateEventProcessor.ProgressSuccessReliefCap})");
            log.AppendLine($"- WorkBlockedMul={WorkerStateEventProcessor.WorkBlockedMul} RepeatedFailureMul={WorkerStateEventProcessor.RepeatedFailureMul}");
            log.AppendLine($"- CompoundPerFrustration={WorkerStateEventProcessor.CompoundPerFrustration} Cap={WorkerStateEventProcessor.CompoundMax}");
            log.AppendLine($"- Sleep FrustrationRelief base={WorkerSleepRecovery.FrustrationRelief} (diminishing when high)");
            log.AppendLine($"- DrySpell first={ProspectorDrySpellTracker.FirstSignalAfterGameHours}h repeat={ProspectorDrySpellTracker.RepeatGapGameHours}h mag={ProspectorDrySpellTracker.EventMagnitude}");
            log.AppendLine();

            var crew = new WorkerRuntime[5];
            for (int i = 0; i < 5; i++)
                crew[i] = new WorkerRuntime(i + 1, Names[i]);

            var svc = new WorkerStateEventService();
            svc.Bind(id =>
            {
                for (int i = 0; i < crew.Length; i++)
                    if (crew[i].WorkerId == id) return crew[i];
                return null;
            });
            WorkerStateEventHub.Service = svc;

            var dry = new ProspectorDrySpellTracker();
            float gameHours = 8f; // day-1 shift start
            float onShiftAccum = 0f;
            dry.Reset();
            WorkerStateClock.GameHours = gameHours;

            var dayEndFrust = new float[5, 5]; // [day, workerIndex]
            var dayStartFrust = new float[5, 5];
            var morningFrust = new float[5, 5]; // after sleep of prior night / start
            var eventLines = new List<string>(256);
            int hardCollapseCount = 0;

            log.AppendLine("## Per-day Frustration");
            log.AppendLine();
            log.AppendLine("| Day | Worker | Start shift | End shift | After sleep | Net day | Notes |");
            log.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | --- |");

            for (int day = 0; day < 5; day++)
            {
                for (int i = 0; i < 5; i++)
                    dayStartFrust[day, i] = crew[i].State.Frustration;

                float shiftEnd = gameHours + ShiftGameHours;
                float nextProgressAt = gameHours + ExcavatorProgressIntervalHours;
                int dayEventsBefore = eventLines.Count;

                while (gameHours + 1e-4f < shiftEnd)
                {
                    float step = Mathf.Min(DayStepHours, shiftEnd - gameHours);
                    gameHours += step;
                    onShiftAccum += step;
                    WorkerStateClock.GameHours = gameHours;

                    for (int i = 0; i < 5; i++)
                        WorkerStateDaytimeRecovery.Tick(crew[i], step);

                    // Lewis (id1) Prospecting — dry spell (OnShift delta only)
                    var dryRec = dry.Tick(1, step, "body.prospector");
                    if (dryRec != null)
                    {
                        eventLines.Add(
                            $"D{day + 1} @{gameHours:0.##}h OnShift={onShiftAccum:0.##}h | Lewis InvestigationFailure (DrySpell) ΔF={dryRec.DeltaFrustration:+0.00;-0.00} → F={crew[0].State.Frustration:0.00}");
                    }

                    // Mara (id2) Excavation — frequent small ProgressSuccess
                    if (gameHours + 1e-4f >= nextProgressAt)
                    {
                        var rec = WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                            2,
                            WorkerStateEventType.ProgressSuccess,
                            ExcavatorProgressMagnitude,
                            "TileDestroyed",
                            JobType.Excavation,
                            "body.excavator"));
                        if (rec != null)
                        {
                            eventLines.Add(
                                $"D{day + 1} @{gameHours:0.##}h | Mara ProgressSuccess ΔF={rec.DeltaFrustration:+0.00;-0.00} → F={crew[1].State.Frustration:0.00}");
                        }
                        nextProgressAt += ExcavatorProgressIntervalHours;
                    }
                }

                for (int i = 0; i < 5; i++)
                    dayEndFrust[day, i] = crew[i].State.Frustration;

                // Full overnight sleep
                for (int i = 0; i < 5; i++)
                {
                    float maxStam = crew[i].PhysicalStaminaMax;
                    crew[i].State.StaminaPrimed = true;
                    crew[i].State.ApplySleepRecoveryFraction(1f, maxStam);
                    morningFrust[day, i] = crew[i].State.Frustration;
                }

                // Advance clock through night (~14h) — does not Tick dry-spell
                gameHours += 14f;
                WorkerStateClock.GameHours = gameHours;

                for (int i = 0; i < 5; i++)
                {
                    float net = morningFrust[day, i] - dayStartFrust[day, i];
                    string note = "";
                    if (i == 0) note = "Prospector + dry-spell";
                    else if (i == 1) note = "Excavator + ProgressSuccess spam";
                    else note = "passive decay + sleep only";

                    if (morningFrust[day, i] <= 0.05f && dayStartFrust[day, i] > 1f
                        && i != 1) // Mara's ProgressSuccess-only path to 0 is productive, not a setback collapse
                    {
                        hardCollapseCount++;
                        note += " | HARD→0";
                    }

                    log.AppendLine(
                        $"| {day + 1} | {Names[i]} | {dayStartFrust[day, i]:0.00} | {dayEndFrust[day, i]:0.00} | {morningFrust[day, i]:0.00} | {net:+0.00;-0.00} | {note} |");
                }

                // Summarize event delta count for the day
                int dayEv = eventLines.Count - dayEventsBefore;
                log.AppendLine($"| {day + 1} | _(events)_ |  |  |  |  | {dayEv} logged state events |");
            }

            log.AppendLine();
            log.AppendLine("## Event log (gains / losses)");
            log.AppendLine();
            if (eventLines.Count == 0)
                log.AppendLine("_No state events emitted._");
            else
            {
                // Cap dump: show all dry-spell + sample ProgressSuccess
                int shownProgress = 0;
                foreach (var line in eventLines)
                {
                    if (line.Contains("ProgressSuccess"))
                    {
                        if (shownProgress < 8 || line.Contains("D5 "))
                        {
                            log.AppendLine("- " + line);
                            shownProgress++;
                        }
                        continue;
                    }
                    log.AppendLine("- " + line);
                }
                int progressTotal = 0;
                for (int i = 0; i < eventLines.Count; i++)
                    if (eventLines[i].Contains("ProgressSuccess")) progressTotal++;
                log.AppendLine($"- _(ProgressSuccess total emits: {progressTotal}; sample above)_");
            }

            log.AppendLine();
            log.AppendLine("## Dry-spell emit count");
            log.AppendLine($"- ProspectorDrySpellTracker.EmitCount = {dry.EmitCount}");
            log.AppendLine($"- Expected OnShift: first@{ProspectorDrySpellTracker.FirstSignalAfterOnShiftHours:0.#}h, repeat +{ProspectorDrySpellTracker.RepeatGapOnShiftHours:0.#}h (nights excluded).");
            log.AppendLine($"- OnShiftAccum end = {onShiftAccum:0.##}");

            // ——— Discovery recovery check ———
            log.AppendLine();
            log.AppendLine("## Genuine success recovery (Discovery after dry streak)");
            float lewisBeforeDiscovery = crew[0].State.Frustration;
            dry.NotifyDiscovery();
            var disc = WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                1,
                WorkerStateEventType.Discovery,
                5f,
                "FindingAssessed#99",
                JobType.Prospecting,
                "body.prospector"));
            float lewisAfterDiscovery = crew[0].State.Frustration;
            log.AppendLine($"- Lewis Frustration before Discovery: {lewisBeforeDiscovery:0.00}");
            log.AppendLine($"- Discovery ΔF: {(disc != null ? disc.DeltaFrustration : 0f):+0.00;-0.00}");
            log.AppendLine($"- Lewis Frustration after Discovery: {lewisAfterDiscovery:0.00}");
            bool recovered = disc != null && lewisAfterDiscovery < lewisBeforeDiscovery - 1f;
            log.AppendLine(recovered
                ? "- RESULT: Frustration relieved by genuine Discovery (success recovery works)."
                : "- RESULT: Discovery did not meaningfully relieve Frustration.");

            // Mara after many ProgressSuccess — did she hard-collapse?
            float lewisMorningD5 = morningFrust[4, 0];
            float maraMorningD5 = morningFrust[4, 1];
            float elenaMorningD5 = morningFrust[4, 3];

            // Inject mid-streak setback residue on Mara, then spam ProgressSuccess — must not fully erase.
            log.AppendLine();
            log.AppendLine("## ProgressSuccess vs elevated Frustration (Mara stress test)");
            crew[1].State.Frustration = 28f;
            float maraHigh = crew[1].State.Frustration;
            for (int n = 0; n < 40; n++)
            {
                WorkerStateClock.GameHours = gameHours + n * 0.05f;
                WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                    2, WorkerStateEventType.ProgressSuccess, ExcavatorProgressMagnitude,
                    "TileDestroyed", JobType.Excavation, "body.excavator"));
            }
            float maraAfterSpam = crew[1].State.Frustration;
            log.AppendLine($"- Start elevated F={maraHigh:0.00}; after 40× ProgressSuccess F={maraAfterSpam:0.00}");
            bool spamLeavesResidue = maraAfterSpam >= 5f;
            log.AppendLine(spamLeavesResidue
                ? "- RESULT: Frequent ProgressSuccess leaves setback residue (does not fully erase)."
                : "- RESULT: ProgressSuccess spam still wiped elevated Frustration.");

            bool lewisPersists = lewisMorningD5 >= 4f;
            bool anyoneHardCollapse = hardCollapseCount > 0;

            log.AppendLine();
            log.AppendLine("## Collapse / persistence verdict");
            log.AppendLine($"- Hard-collapse-to-0 events (passive/prospector start>1 → morning≤0.05): {hardCollapseCount}");
            log.AppendLine($"- Lewis morning day-5 Frustration: {lewisMorningD5:0.00} (persist meaningful? {(lewisPersists ? "YES" : "WEAK")})");
            log.AppendLine($"- Lewis end-shift day-5 Frustration: {dayEndFrust[4, 0]:0.00}");
            log.AppendLine($"- Mara morning day-5 Frustration: {maraMorningD5:0.00} (productive digger floor — expected near 0 without setbacks)");
            log.AppendLine($"- Elena morning day-5 Frustration: {elenaMorningD5:0.00} (passive only — expected near 0 without setbacks)");
            log.AppendLine($"- ProgressSuccess leaves elevated residue: {(spamLeavesResidue ? "YES" : "NO")}");
            log.AppendLine($"- Discovery recovery works: {(recovered ? "YES" : "NO")}");

            log.AppendLine();
            log.AppendLine("## RECOMMENDATION");
            if (lewisPersists && recovered && spamLeavesResidue)
                log.AppendLine(anyoneHardCollapse
                    ? "RECOMMENDATION: MOSTLY READY — dry-streak Frustration persists; passive floor-to-0 without setbacks is expected."
                    : "RECOMMENDATION: READY — Frustration persists across dry days; sleep/decay no longer erase residue; Discovery still relieves.");
            else
                log.AppendLine("RECOMMENDATION: NEEDS WORK — Frustration still collapses or Discovery recovery failed.");

            string dir = outputDirectory;
            if (string.IsNullOrEmpty(dir))
            {
                dir = Application.dataPath != null
                    ? Path.Combine(Application.dataPath, "..", "BenchmarkResults")
                    : "BenchmarkResults";
            }
            Directory.CreateDirectory(dir);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(dir, $"frustration_loop_{stamp}.md");
            string latest = Path.Combine(dir, "frustration_loop_latest.md");
            File.WriteAllText(path, log.ToString());
            File.WriteAllText(latest, log.ToString());
            return Path.GetFullPath(path);
        }
    }
}
