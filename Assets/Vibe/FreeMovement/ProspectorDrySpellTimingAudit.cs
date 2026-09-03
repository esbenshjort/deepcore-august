using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Timing-only audit for ProspectorDrySpellTracker (OnShift accumulation).
    /// Batch: ProspectorDrySpellTimingAudit.RunFromEditor
    /// </summary>
    public static class ProspectorDrySpellTimingAudit
    {
        const float ShiftHours = 10f;
        const float NightHours = 14f;
        const float Step = 0.25f;

        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[DRY SPELL TIMING] Report: {path}");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var log = new StringBuilder(6_000);
            log.AppendLine("# ProspectorDrySpellTracker — Timing Audit");
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine();
            log.AppendLine("Rule: accumulate OnShift hours only; first@32, repeat@+20; Discovery resets.");
            log.AppendLine($"Constants: First={ProspectorDrySpellTracker.FirstSignalAfterOnShiftHours} Repeat={ProspectorDrySpellTracker.RepeatGapOnShiftHours}");
            log.AppendLine("Test: 5 × 10h OnShift + 14h night (nights do not Tick); Reset(); no Discovery.");
            log.AppendLine();

            var lewis = new WorkerRuntime(1, "Lewis");
            var svc = new WorkerStateEventService();
            svc.Bind(id => id == 1 ? lewis : null);
            WorkerStateEventHub.Service = svc;

            var dry = new ProspectorDrySpellTracker();
            dry.Reset();
            float calendar = 0f;
            float onShiftAccum = 0f;
            WorkerStateClock.GameHours = 0f;

            var eventOnShift = new List<float>();
            var eventCalendar = new List<float>();

            for (int day = 0; day < 5; day++)
            {
                float shiftEnd = calendar + ShiftHours;
                while (calendar + 1e-4f < shiftEnd)
                {
                    float step = Mathf.Min(Step, shiftEnd - calendar);
                    calendar += step;
                    onShiftAccum += step;
                    WorkerStateClock.GameHours = calendar;

                    var rec = dry.Tick(1, step, "timing.audit");
                    if (rec != null)
                    {
                        eventOnShift.Add(onShiftAccum);
                        eventCalendar.Add(calendar);
                    }
                }

                // Sleep: advance calendar only — do not Tick
                calendar += NightHours;
                WorkerStateClock.GameHours = calendar;
            }

            log.AppendLine("## EXPECTED");
            log.AppendLine("- OnShift total = 50");
            log.AppendLine("- EVENT TIMES (OnShiftAccum): 32");
            log.AppendLine("- Count = 1 (second at 52 is outside window)");
            log.AppendLine();

            log.AppendLine("## ACTUAL");
            log.AppendLine($"- OnShift total = {onShiftAccum:0.##}");
            log.AppendLine($"- EmitCount = {dry.EmitCount}");
            log.AppendLine($"- DryOnShiftHours (end) = {dry.DryOnShiftHours:0.##}");
            for (int i = 0; i < eventOnShift.Count; i++)
                log.AppendLine($"- emit#{i + 1} OnShiftAccum={eventOnShift[i]:0.##} calendar={eventCalendar[i]:0.##}");
            if (eventOnShift.Count == 0)
                log.AppendLine("- (no emits)");
            log.AppendLine();

            bool countOk = dry.EmitCount == 1 && eventOnShift.Count == 1;
            bool timeOk = countOk && Mathf.Abs(eventOnShift[0] - 32f) < 0.05f;
            bool totalOk = Mathf.Abs(onShiftAccum - 50f) < 0.05f;
            bool pass = countOk && timeOk && totalOk;

            log.AppendLine("## RESULT");
            log.AppendLine($"FIX: OnShift-delta accumulation (sleep does not Tick / does not advance thresholds)");
            log.AppendLine($"EVENT TIMES: {(eventOnShift.Count == 0 ? "(none)" : string.Join(", ", eventOnShift.ConvertAll(t => t.ToString("0.##"))))}");
            log.AppendLine(pass ? "AUDIT: PASS" : "AUDIT: FAIL");

            string dir = outputDirectory;
            if (string.IsNullOrEmpty(dir))
                dir = Path.Combine(Application.dataPath, "..", "BenchmarkResults");
            Directory.CreateDirectory(dir);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(dir, $"dry_spell_timing_{stamp}.md");
            string latest = Path.Combine(dir, "dry_spell_timing_latest.md");
            File.WriteAllText(path, log.ToString());
            File.WriteAllText(latest, log.ToString());
            return Path.GetFullPath(path);
        }
    }
}
