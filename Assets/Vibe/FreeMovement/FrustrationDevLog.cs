using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// DEV ring buffer for major Frustration swings — wording for under-hood / audits.
    /// Does not alter simulation.
    /// </summary>
    public static class FrustrationDevLog
    {
        public const float MajorAbsDelta = 2.5f;
        public const int Cap = 32;

        public struct Entry
        {
            public float GameHours;
            public int WorkerId;
            public string DisplayName;
            public WorkerStateEventType EventType;
            public string Source;
            public float Delta;
            public float After;
            public string Explain;
        }

        static readonly List<Entry> _items = new(Cap);
        public static IReadOnlyList<Entry> Items => _items;

        public static void Clear() => _items.Clear();

        public static void Observe(WorkerRuntime wr, WorkerStateEvent e, WorkerStateEventRecord rec)
        {
            if (wr?.State == null || e == null || rec == null) return;
            float d = rec.DeltaFrustration;
            if (Mathf.Abs(d) < MajorAbsDelta) return;

            string why;
            if (d > 0f)
            {
                why = e.EventType switch
                {
                    WorkerStateEventType.RepeatedFailure =>
                        "Repeated setback compounded — already under pressure.",
                    WorkerStateEventType.WorkBlocked =>
                        "Work blocked again — Determination/Tolerance resisted some of it.",
                    WorkerStateEventType.InvestigationFailure =>
                        "Dry spell / investigation miss — patience thinning.",
                    WorkerStateEventType.EquipmentProblem =>
                        "Gear fight — acute spike (Composure helped if high).",
                    WorkerStateEventType.Injury =>
                        "Injury sting — body and temper both take the hit.",
                    _ => "Setback pressure rose.",
                };
            }
            else
            {
                why = e.EventType switch
                {
                    WorkerStateEventType.ProgressSuccess =>
                        "Small win — soft relief only; won't wipe a bad stretch.",
                    WorkerStateEventType.MajorSuccess =>
                        "Major success — real pressure drop.",
                    WorkerStateEventType.Discovery =>
                        "Discovery — meaningful relief + morale lift.",
                    WorkerStateEventType.EquipmentRecovered =>
                        "Gear recovered — tension eased.",
                    _ => "Pressure eased.",
                };
            }

            DigHoodLog.Push(
                $"FRUST {(d >= 0f ? "+" : "")}{d:0.#} | {wr.DisplayName} | {e.EventType} | {why} → {wr.State.Frustration:0.#}");

            _items.Add(new Entry
            {
                GameHours = e.GameHours > 0f ? e.GameHours : WorkerStateClock.GameHours,
                WorkerId = wr.WorkerId,
                DisplayName = wr.DisplayName,
                EventType = e.EventType,
                Source = e.Source ?? "",
                Delta = d,
                After = wr.State.Frustration,
                Explain = why,
            });
            while (_items.Count > Cap)
                _items.RemoveAt(0);
        }
    }
}
