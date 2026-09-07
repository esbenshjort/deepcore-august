using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Player-facing shift schedule (crew-wide V1). Default 08:00–16:00 = 8h work.
    /// </summary>
    public sealed class ShiftPlanner
    {
        public const float DefaultStart = 8f;
        public const float DefaultEnd = 16f; // 8h work (was 18 → now planner-driven)
        public const float MinWorkHours = 4f;
        public const float MaxWorkHours = 14f;
        public const float NominalWorkHours = 8f;
        /// <summary>Target overnight sleep for planner estimates (game hours).</summary>
        public const float PreferredSleepHours = 7.5f;

        public float ShiftStartHour = DefaultStart;
        public float ShiftEndHour = DefaultEnd;

        /// <summary>Typical one-way commute estimate for planner display (game hours).</summary>
        public float ExpectedCommuteOneWayHours = 0.75f;
        /// <summary>Minimum evening camp / meal / care target before sleep (game hours).</summary>
        public float ExpectedCampBufferHours = 1.25f;

        public float PlannedWorkHours
        {
            get
            {
                float w = ShiftEndHour - ShiftStartHour;
                if (w < 0f) w += 24f;
                return w;
            }
        }

        public void SetShift(float startHour, float endHour)
        {
            ShiftStartHour = Mathf.Repeat(startHour, 24f);
            ShiftEndHour = Mathf.Repeat(endHour, 24f);
            float work = PlannedWorkHours;
            if (work < MinWorkHours)
                ShiftEndHour = Mathf.Repeat(ShiftStartHour + MinWorkHours, 24f);
            else if (work > MaxWorkHours)
                ShiftEndHour = Mathf.Repeat(ShiftStartHour + MaxWorkHours, 24f);
        }

        public void NudgeStart(float deltaHours) =>
            SetShift(ShiftStartHour + deltaHours, ShiftEndHour);

        public void NudgeEnd(float deltaHours) =>
            SetShift(ShiftStartHour, ShiftEndHour + deltaHours);

        public void SetWorkLength(float hours)
        {
            hours = Mathf.Clamp(hours, MinWorkHours, MaxWorkHours);
            SetShift(ShiftStartHour, ShiftStartHour + hours);
        }

        float RemainingAfterWorkAndCommute =>
            24f - PlannedWorkHours - ExpectedCommuteOneWayHours * 2f;

        /// <summary>
        /// Prefer ~7.5h sleep; leftover after work+commute becomes camp/free.
        /// Long commute / long shifts squeeze sleep first after a small camp floor.
        /// </summary>
        public float ExpectedSleepHours
        {
            get
            {
                float rem = RemainingAfterWorkAndCommute;
                if (rem <= 0f) return 0f;
                const float minCamp = 0.35f;
                float campFloor = Mathf.Min(ExpectedCampBufferHours, minCamp);
                if (rem >= PreferredSleepHours + ExpectedCampBufferHours)
                    return PreferredSleepHours;
                if (rem >= PreferredSleepHours + campFloor)
                    return PreferredSleepHours;
                return Mathf.Max(0f, rem - campFloor);
            }
        }

        public float ExpectedFreeCampHours =>
            Mathf.Max(0f, RemainingAfterWorkAndCommute - ExpectedSleepHours);

        public string WorkBandLabel
        {
            get
            {
                float h = PlannedWorkHours;
                if (h <= 8.01f) return "NORMAL SHIFT";
                if (h <= 10.01f) return "OVERTIME";
                return "HEAVY OVERTIME";
            }
        }

        public string WorkBandWarning
        {
            get
            {
                float h = PlannedWorkHours;
                if (h <= 8.01f) return "Sustainable for most crews.";
                if (h <= 10.01f) return "Crew fatigue likely.";
                return "High risk of fatigue and resentment.";
            }
        }

        public string FormatHours(float h)
        {
            if (h < 0f) h = 0f;
            int hh = Mathf.FloorToInt(h);
            int mm = Mathf.FloorToInt((h - hh) * 60f);
            return $"{hh}h {mm:00}m";
        }

        public string SleepEstimateLine()
        {
            float sleep = ExpectedSleepHours;
            var sb = new StringBuilder();
            sb.Append("Estimated sleep: ");
            sb.Append(FormatHours(sleep));
            if (ExpectedCommuteOneWayHours * 2f > 1.5f && sleep < 7f)
                sb.Append("\nLong commute is reducing recovery time.");
            else if (sleep < 6f)
                sb.Append("\nShort sleep window — recovery will suffer.");
            return sb.ToString();
        }
    }

    /// <summary>Directional worker → manager relationship (separate from crew↔crew).</summary>
    public sealed class ManagerRelation
    {
        public const float Min = 0f;
        public const float Max = 100f;
        public const float DefaultTrust = 55f;
        public const float DefaultRespect = 50f;
        public const float DefaultResentment = 8f;

        public float Trust = DefaultTrust;
        public float Respect = DefaultRespect;
        public float Resentment = DefaultResentment;

        public void Clamp()
        {
            Trust = Mathf.Clamp(Trust, Min, Max);
            Respect = Mathf.Clamp(Respect, Min, Max);
            Resentment = Mathf.Clamp(Resentment, Min, Max);
        }

        public void Add(float dTrust, float dRespect, float dResentment)
        {
            Trust += dTrust;
            Respect += dRespect;
            Resentment += dResentment;
            Clamp();
        }
    }

    public sealed class ManagerRelationshipStore
    {
        public const int ManagerId = -1; // reserved — never a WorkerId
        readonly Dictionary<int, ManagerRelation> _byWorker = new(16);

        public ManagerRelation Get(int workerId)
        {
            if (workerId <= 0) return null;
            if (!_byWorker.TryGetValue(workerId, out var r) || r == null)
            {
                r = new ManagerRelation();
                _byWorker[workerId] = r;
            }
            return r;
        }

        public void Clear() => _byWorker.Clear();

        public void EnsureCrew(WorkerRuntime[] crew)
        {
            if (crew == null) return;
            for (int i = 0; i < crew.Length; i++)
            {
                var wr = crew[i];
                if (wr == null || !wr.IsAlive) continue;
                Get(wr.WorkerId);
            }
        }

        /// <summary>Slow recovery when schedules are reasonable.</summary>
        public void TickGentleRecovery(WorkerRuntime wr, float gameHours, bool scheduleReasonable)
        {
            if (wr == null || !wr.IsAlive || gameHours <= 0f || !scheduleReasonable) return;
            var r = Get(wr.WorkerId);
            if (r.Resentment <= 5f && r.Trust >= 60f) return;
            float rate = 0.35f * gameHours; // slow
            r.Add(dTrust: rate * 0.15f, dRespect: rate * 0.08f, dResentment: -rate * 0.25f);
        }
    }

    public enum CrewDaySegment : byte
    {
        Work = 0,
        CommuteHome = 1,
        CampFree = 2,
        Sleep = 3,
        CommuteOut = 4,
    }

    /// <summary>Per-worker day ledger for summary + overtime context.</summary>
    public sealed class WorkerDayLedger
    {
        public int WorkerId;
        public float WorkHours;
        public float CommuteHomeHours;
        public float CampFreeHours;
        public float SleepHours;
        public float CommuteOutHours;
        public float BedtimeGameHour = -1f;
        public float WakeGameHour = -1f;
        public bool ArrivedHome;
        public bool ArrivedWork;
        public bool Sleeping;
        public int ConsecutiveOvertimeDays;
        public float SleepDeficit01; // 0 = full rest, 1 = severe shortfall
        public string MorningNote = "";

        public float TotalTracked =>
            WorkHours + CommuteHomeHours + CampFreeHours + SleepHours + CommuteOutHours;

        public void ResetDayKeepStreak()
        {
            WorkHours = 0f;
            CommuteHomeHours = 0f;
            CampFreeHours = 0f;
            SleepHours = 0f;
            CommuteOutHours = 0f;
            BedtimeGameHour = -1f;
            WakeGameHour = -1f;
            ArrivedHome = false;
            ArrivedWork = false;
            Sleeping = false;
            MorningNote = "";
        }
    }

    public sealed class CrewDayTracker
    {
        readonly Dictionary<int, WorkerDayLedger> _byId = new(16);
        public DailySummary LastSummary { get; private set; }
        public bool SummaryPending;

        public WorkerDayLedger Get(int workerId)
        {
            if (!_byId.TryGetValue(workerId, out var L) || L == null)
            {
                L = new WorkerDayLedger { WorkerId = workerId };
                _byId[workerId] = L;
            }
            return L;
        }

        public void EnsureCrew(WorkerRuntime[] crew)
        {
            if (crew == null) return;
            for (int i = 0; i < crew.Length; i++)
                if (crew[i] != null) Get(crew[i].WorkerId);
        }

        public void Accrue(int workerId, CrewDaySegment seg, float gameHours)
        {
            if (gameHours <= 0f || workerId <= 0) return;
            var L = Get(workerId);
            switch (seg)
            {
                case CrewDaySegment.Work: L.WorkHours += gameHours; break;
                case CrewDaySegment.CommuteHome: L.CommuteHomeHours += gameHours; break;
                case CrewDaySegment.CampFree: L.CampFreeHours += gameHours; break;
                case CrewDaySegment.Sleep: L.SleepHours += gameHours; break;
                case CrewDaySegment.CommuteOut: L.CommuteOutHours += gameHours; break;
            }
        }

        public void BeginNewDayKeepStreaks(WorkerRuntime[] crew)
        {
            if (crew == null) return;
            for (int i = 0; i < crew.Length; i++)
            {
                if (crew[i] == null) continue;
                Get(crew[i].WorkerId).ResetDayKeepStreak();
            }
        }

        public DailySummary BuildSummary(
            int dayIndex,
            WorkerRuntime[] crew,
            ManagerRelationshipStore managers,
            ShiftPlanner planner)
        {
            var s = new DailySummary { DayIndex = dayIndex };
            if (crew == null) return s;
            float sumWork = 0f, sumComm = 0f, sumCamp = 0f, sumSleep = 0f;
            int n = 0;
            for (int i = 0; i < crew.Length; i++)
            {
                var wr = crew[i];
                if (wr == null || !wr.IsAlive) continue;
                var L = Get(wr.WorkerId);
                n++;
                sumWork += L.WorkHours;
                sumComm += L.CommuteHomeHours + L.CommuteOutHours;
                sumCamp += L.CampFreeHours;
                sumSleep += L.SleepHours;
                s.Lines.Add(BuildPersonLine(wr, L, managers));
            }
            if (n > 0)
            {
                s.AvgWorkHours = sumWork / n;
                s.AvgCommuteHours = sumComm / n;
                s.AvgCampHours = sumCamp / n;
                s.AvgSleepHours = sumSleep / n;
            }
            s.PlannedWorkHours = planner != null ? planner.PlannedWorkHours : 8f;
            if (planner != null && n > 0 && sumComm > 0.05f)
            {
                // Feed planner display from real round-trip average (half = one-way).
                float oneWay = (sumComm / n) * 0.5f;
                planner.ExpectedCommuteOneWayHours = Mathf.Clamp(
                    Mathf.Lerp(planner.ExpectedCommuteOneWayHours, oneWay, 0.55f),
                    0.25f, 4f);
            }
            LastSummary = s;
            SummaryPending = true;
            return s;
        }

        static string BuildPersonLine(WorkerRuntime wr, WorkerDayLedger L, ManagerRelationshipStore managers)
        {
            var bits = new List<string>(4);
            if (L.SleepDeficit01 >= 0.35f) bits.Add("Poor recovery");
            if (wr.State != null && wr.State.Frustration >= 55f) bits.Add("Frustration increased");
            var mgr = managers?.Get(wr.WorkerId);
            if (mgr != null && mgr.Resentment >= 25f) bits.Add("Resentment toward Manager increased");
            if (wr.State != null && wr.State.ExhaustionLatched) bits.Add("Exhausted");
            if (bits.Count == 0)
            {
                if (L.WorkHours <= 8.2f && L.SleepHours >= 6.5f)
                    bits.Add("Holding steady");
                else
                    bits.Add("Manageable day");
            }
            return $"{wr.DisplayName}:\n{string.Join("\n", bits)}";
        }
    }

    public sealed class DailySummary
    {
        public int DayIndex;
        public float AvgWorkHours;
        public float AvgCommuteHours;
        public float AvgCampHours;
        public float AvgSleepHours;
        public float PlannedWorkHours;
        public readonly List<string> Lines = new(8);
    }

    /// <summary>
    /// Overtime pressure — gradual, trait-aware. Does not auto-trigger violence.
    /// Exact Soul mappings: Composure, Determination, Tolerance, Empathy (forgiveness),
    /// Focus (mental tax), WorkRate (does not reduce OT resentment).
    /// </summary>
    public static class OvertimePressure
    {
        public static float Tolerance01(WorkerRuntime wr)
        {
            if (wr?.Stats == null) return 0.5f;
            float composure = Norm(wr.Stats.Get(WorkerStatId.Composure));
            float determination = Norm(wr.Stats.Get(WorkerStatId.Determination));
            float tolerance = Norm(wr.Stats.Get(WorkerStatId.Tolerance));
            return Mathf.Clamp01(composure * 0.35f + determination * 0.25f + tolerance * 0.40f);
        }

        static float Norm(int s) => Mathf.Clamp01((s - 1) / 19f);

        /// <summary>
        /// Tick while worker is past nominal 8h into their work day, or past planned end.
        /// </summary>
        public static void TickDuringWork(
            WorkerRuntime wr,
            WorkerDayLedger ledger,
            ManagerRelationshipStore managers,
            float plannedWorkHours,
            float gameHours,
            float absoluteGameHours)
        {
            if (wr == null || !wr.IsAlive || wr.State == null || gameHours <= 0f) return;
            float worked = ledger != null ? ledger.WorkHours : 0f;
            float overtime = Mathf.Max(0f, worked - ShiftPlanner.NominalWorkHours);
            bool pastPlan = worked > plannedWorkHours + 0.05f;
            if (overtime < 0.15f && !pastPlan) return;

            float tol = Tolerance01(wr);
            float streak = ledger != null ? ledger.ConsecutiveOvertimeDays : 0;
            float deficit = ledger != null ? ledger.SleepDeficit01 : 0f;
            float fr = wr.State.Frustration / 100f;
            float fatig = wr.State.MentalFatigue / 100f;

            // Intensity grows with OT hours, streak, deficit — tolerance softens
            float intensity = overtime * 0.22f
                              + streak * 0.18f
                              + deficit * 0.35f
                              + (pastPlan ? 0.15f : 0f);
            intensity *= Mathf.Lerp(1.25f, 0.55f, tol);
            intensity *= (0.75f + fr * 0.35f + fatig * 0.25f);

            float mag = intensity * gameHours * 2.2f;
            if (mag < 0.02f) return;

            WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                wr.WorkerId,
                WorkerStateEventType.OvertimePressure,
                mag,
                "ShiftPlanner/Overtime",
                relatedWorkerId: ManagerRelationshipStore.ManagerId));

            var mgr = managers?.Get(wr.WorkerId);
            if (mgr != null)
            {
                float resent = mag * Mathf.Lerp(0.55f, 0.2f, tol);
                float trustHit = mag * 0.08f * (1f - tol);
                mgr.Add(-trustHit, -mag * 0.05f, resent);
            }
        }

        public static void FinalizeDayOvertimeStreak(WorkerDayLedger ledger, float plannedWork)
        {
            if (ledger == null) return;
            if (ledger.WorkHours > ShiftPlanner.NominalWorkHours + 0.35f
                || ledger.WorkHours > plannedWork + 0.25f)
                ledger.ConsecutiveOvertimeDays++;
            else
                ledger.ConsecutiveOvertimeDays = Mathf.Max(0, ledger.ConsecutiveOvertimeDays - 1);
        }
    }
}
