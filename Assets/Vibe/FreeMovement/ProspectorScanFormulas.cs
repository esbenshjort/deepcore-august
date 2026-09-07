using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Centralized scan duration + size helpers.
    /// Calibration = primary speed · Focus = secondary · Work Rate unused.
    /// Production target band: ~2–4 in-game days (48–96 game hours).
    /// Testing currently scales that down for playable iteration.
    /// </summary>
    public static class ProspectorScanFormulas
    {
        public const float MinScanHours = 48f;  // 2 days (production)
        public const float MaxScanHours = 96f;  // 4 days (production)
        public const float HoursPerDay = 24f;

        /// <summary>
        /// TEMP: shrink production durations for SocketMap testing.
        /// Flip to false when balancing real scan length again.
        /// </summary>
        public static bool UseTestingScanDurations = true;

        /// <summary>
        /// Scan testing scale — short enough to feel like active prospecting, long enough to read.
        /// ~2.5–7 game hours ≈ ~30–84s real at 12s/game-hour.
        /// </summary>
        public const float TestingDurationScale = 0.075f;

        /// <summary>
        /// Analysis testing scale — conclusions land in a short but readable desk sit.
        /// Applied after production 1.5–4.0h band.
        /// </summary>
        public const float TestingAnalysisDurationScale = 0.12f;
        public const float TestingMinAnalysisHours = 0.14f;
        public const float TestingMaxAnalysisHours = 0.48f;

        /// <summary>Refiner consult dwell (game hours) — short focused check.</summary>
        public const float TestingConsultDwellHours = 0.035f;
        public const float ProductionConsultDwellHours = 0.15f;

        public const float TestingMinScanHours = 2.5f;
        public const float TestingMaxScanHours = 7f;

        /// <summary>
        /// Area-ish fraction of equipment max cone (0 = tiny · 1 = full equipment limit).
        /// Stats cannot push past equipment — caller must clamp plan to Spec first.
        /// </summary>
        public static float Size01(
            float plannedRangeCells,
            float plannedHalfAngleDeg,
            in ProspectorScannerEquipmentSpec max)
        {
            float rMax = Mathf.Max(1f, max.MaxRangeCells);
            float aMax = Mathf.Max(1f, max.ConeHalfAngleDeg);
            float r = Mathf.Clamp01(plannedRangeCells / rMax);
            float a = Mathf.Clamp01(plannedHalfAngleDeg / aMax);
            // Sector area ∝ r² · angle
            return Mathf.Clamp01(r * r * a);
        }

        /// <summary>
        /// Total scan duration in game hours.
        /// size01 from <see cref="Size01"/>; Calibration primary, Focus secondary.
        /// </summary>
        public static float ScanDurationHours(WorkerStats stats, float size01)
        {
            int calib = stats != null ? stats.Get(WorkerStatId.Calibration) : WorkerStats.Baseline;
            int focus = stats != null ? stats.Get(WorkerStatId.Focus) : WorkerStats.Baseline;
            return ScanDurationHours(calib, focus, size01);
        }

        public static float ScanDurationHours(int calibration, int focus, float size01)
        {
            calibration = WorkerStats.Clamp(calibration);
            focus = WorkerStats.Clamp(focus);
            size01 = Mathf.Clamp01(size01);

            // Full-size production examples: ~3.8d / ~3.0d / ~2.1d for 3 / 10 / 19
            float skill = calibration + focus * 0.4f;
            float baseAtFull = 100f - skill * 2.05f;
            float hours = baseAtFull * Mathf.Lerp(0.58f, 1f, size01);
            hours = Mathf.Clamp(hours, MinScanHours, MaxScanHours);

            if (UseTestingScanDurations)
            {
                hours *= TestingDurationScale;
                hours = Mathf.Clamp(hours, TestingMinScanHours, TestingMaxScanHours);
            }

            return hours;
        }

        public static float DaysFromHours(float hours) => hours / HoursPerDay;

        /// <summary>Player clock: Day 1 starts at hour 0. Minutes from fractional hour.</summary>
        public static void FormatDayClock(float absoluteGameHours, out int day, out int hour, out int minute)
        {
            float h = Mathf.Max(0f, absoluteGameHours);
            day = Mathf.FloorToInt(h / HoursPerDay) + 1;
            float tod = h - (day - 1) * HoursPerDay;
            hour = Mathf.Clamp(Mathf.FloorToInt(tod), 0, 23);
            minute = Mathf.Clamp(Mathf.FloorToInt((tod - hour) * 60f), 0, 59);
        }

        public static string FormatDayClockLabel(float absoluteGameHours)
        {
            FormatDayClock(absoluteGameHours, out int day, out int hour, out int minute);
            return $"DAY {day} // {hour:00}:{minute:00}";
        }

        public static string FormatFacingLabel(Vector2 facing)
        {
            if (facing.sqrMagnitude < 0.0001f) return "—";
            float ang = Mathf.Atan2(facing.x, facing.y) * Mathf.Rad2Deg; // 0 = up/N
            if (ang < 0f) ang += 360f;
            int oct = Mathf.RoundToInt(ang / 45f) % 8;
            return oct switch
            {
                0 => "N",
                1 => "NE",
                2 => "E",
                3 => "SE",
                4 => "S",
                5 => "SW",
                6 => "W",
                7 => "NW",
                _ => "N",
            };
        }
    }
}
