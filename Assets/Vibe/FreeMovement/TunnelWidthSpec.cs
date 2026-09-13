using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Tunnel Width V1 — geometry + access + stress rate tables.
    /// Width 4 matches pre-V1 excavator dig envelope (standard operational tunnel).
    /// </summary>
    public static class TunnelWidthSpec
    {
        public const int Min = 1;
        public const int Max = 5;
        public const int Standard = 4;

        public static int Clamp(int w) => Mathf.Clamp(w, Min, Max);

        /// <summary>
        /// Painted corridor cell count for width CLASS 1–5 (Tile-Paint V1.1).
        /// Class stays 1–5 for stamina/claustro/access semantics — do not treat cell count as class.
        /// </summary>
        public static int BrushCells(int widthClass) => Clamp(widthClass) switch
        {
            1 => 3,
            2 => 5,
            3 => 8,
            4 => 12,
            5 => 16,
            _ => 12,
        };

        /// <summary>Half-width in cells for paint-driven dig envelope (= BrushCells/2).</summary>
        public static float BrushHalfCells(int widthClass) => BrushCells(widthClass) * 0.5f;

        /// <summary>Dig-face half-width in cells at the mouth (legacy free-dig / WASD envelope).</summary>
        public static float DigHalfCells(int width) => BrushHalfCells(width);

        /// <summary>Body clearance radius in cells — excavator advances only after this clears.</summary>
        public static float MoveRadiusCells(int width) =>
            DigHalfCells(width) * (Clamp(width) <= 2 ? 0.88f : 0.94f);

        /// <summary>Tip floor half-width (cells) — narrow tips allowed for width 1–2.</summary>
        public static float TipMinHalfCells(int width) => Clamp(width) switch
        {
            1 => 0.42f,
            2 => 0.55f,
            3 => 0.7f,
            _ => 0.85f,
        };

        /// <summary>
        /// Extra stamina per strike — wider face still costs more per action;
        /// primary workload is geometric (more cells to clear).
        /// </summary>
        public static float StaminaPerStrikeMul(int width) => Clamp(width) switch
        {
            1 => 0.72f,
            2 => 0.85f,
            3 => 0.95f,
            4 => 1f,
            5 => 1.18f,
            _ => 1f,
        };

        /// <summary>Dig cadence mul — narrow slightly faster strikes; wide slightly slower.</summary>
        public static float DigCadenceMul(int width) => Clamp(width) switch
        {
            1 => 0.82f,
            2 => 0.9f,
            3 => 0.96f,
            4 => 1f,
            5 => 1.12f,
            _ => 1f,
        };

        /// <summary>Min nav clearance for an agent to use a corridor of this dig width.</summary>
        public static int ResultingClearanceEstimate(int digWidth) => Clamp(digWidth) switch
        {
            1 => 1,
            2 => 1,
            3 => 2,
            4 => 3,
            5 => 4,
            _ => 2,
        };

        /// <summary>Hauler / machine requires clearance ≥ 2 → needs dig width ≥ 3.</summary>
        public const int HaulerMinClearance = 2;
        public const int HaulerMinTunnelWidth = 3;
        public const int WorkerMinClearance = 1;

        public static bool HaulerCanUseClearance(int clearance) =>
            clearance >= HaulerMinClearance;

        public static bool WorkerCanUseClearance(int clearance) =>
            clearance >= WorkerMinClearance;

        /// <summary>Map live clearance → approximate confinement width band for stress.</summary>
        public static int WidthBandFromClearance(int clearance)
        {
            if (clearance <= 0) return 1;
            if (clearance == 1) return 1;
            if (clearance == 2) return 2;
            if (clearance == 3) return 3;
            if (clearance == 4) return 4;
            return 5;
        }

        /// <summary>Base claustrophobic stress gain per game-hour by confinement width (V1.1 — slow early).</summary>
        public static float StressGainPerHour(int widthBand) => Clamp(widthBand) switch
        {
            1 => 11f,   // was 26 — Width 1 dangerous long-term, not instant
            2 => 7f,    // was 16
            3 => 4f,    // was 8
            4 => 1.6f,  // was 2.8
            5 => 0.25f,
            _ => 1.6f,
        };

        public static string Label(int width) => Clamp(width) switch
        {
            1 => "EXTREME CONFINEMENT",
            2 => "TIGHT",
            3 => "MODERATE",
            4 => "STANDARD",
            5 => "WIDE",
            _ => "STANDARD",
        };

        public static string ShortHint(int width) => Clamp(width) switch
        {
            1 => $"Very narrow · {BrushCells(1)} tiles · single-person",
            2 => $"Narrow · {BrushCells(2)} tiles",
            3 => $"Medium · {BrushCells(3)} tiles",
            4 => $"Standard industrial · {BrushCells(4)} tiles",
            5 => $"Major corridor · {BrushCells(5)} tiles",
            _ => "",
        };

        public static Color PreviewColor(int width)
        {
            return Clamp(width) switch
            {
                1 => new Color(1f, 0.35f, 0.25f, 0.55f),
                2 => new Color(1f, 0.55f, 0.22f, 0.5f),
                3 => new Color(1f, 0.82f, 0.28f, 0.45f),
                4 => new Color(0.35f, 1f, 0.55f, 0.4f),
                5 => new Color(0.35f, 0.85f, 1f, 0.45f),
                _ => new Color(0.35f, 1f, 0.55f, 0.4f),
            };
        }
    }

    public enum TunnelLightBand : byte
    {
        Lit = 0,
        Dim = 1,
        Dark = 2,
        PitchBlack = 3,
    }

    /// <summary>Reusable illumination sample — lanterns + excavator work light + camp pool.</summary>
    public static class TunnelIllumination
    {
        public static float Sample01(
            Vector2 worldPos,
            MineInfrastructure infra,
            Vector2? excavatorPos,
            Vector2 campPos)
        {
            float best = infra != null ? infra.EvaluateIllumination01(worldPos) : 0f;

            if (excavatorPos.HasValue)
            {
                float d = Vector2.Distance(worldPos, excavatorPos.Value);
                const float outer = 1.25f;
                if (d < outer)
                {
                    float t = 1f - d / outer;
                    best = Mathf.Max(best, t * t * 0.88f);
                }
            }

            float cd = Vector2.Distance(worldPos, campPos);
            const float campOuter = 3.6f;
            if (cd < campOuter)
            {
                float t = 1f - cd / campOuter;
                best = Mathf.Max(best, t * t * 0.72f);
            }

            return Mathf.Clamp01(best);
        }

        public static TunnelLightBand Classify(float illum01)
        {
            if (illum01 >= 0.52f) return TunnelLightBand.Lit;
            if (illum01 >= 0.26f) return TunnelLightBand.Dim;
            if (illum01 >= 0.07f) return TunnelLightBand.Dark;
            return TunnelLightBand.PitchBlack;
        }

        public static float StressGainPerHour(TunnelLightBand band) => band switch
        {
            TunnelLightBand.Lit => 0f,
            TunnelLightBand.Dim => 3.5f,
            TunnelLightBand.Dark => 11f,
            TunnelLightBand.PitchBlack => 20f,
            _ => 0f,
        };

        public static string Label(TunnelLightBand band) => band switch
        {
            TunnelLightBand.Lit => "Lit",
            TunnelLightBand.Dim => "Dim",
            TunnelLightBand.Dark => "Dark",
            TunnelLightBand.PitchBlack => "Pitch black",
            _ => "Dim",
        };
    }

    public enum ClaustrophobiaBand : byte
    {
        Low = 0,
        Elevated = 1,
        High = 2,
        Severe = 3,
        Critical = 4,
    }

    public static class ClaustrophobiaBands
    {
        // V1.1: wider calm band; critical refusal later
        public const float ElevatedAt = 28f;  // UNEASY
        public const float HighAt = 48f;      // STRESSED
        public const float SevereAt = 68f;    // SEVERE — hesitation
        public const float CriticalAt = 90f;  // CRITICAL — can refuse (was 88)

        public static ClaustrophobiaBand Classify(float stress01to100)
        {
            if (stress01to100 >= CriticalAt) return ClaustrophobiaBand.Critical;
            if (stress01to100 >= SevereAt) return ClaustrophobiaBand.Severe;
            if (stress01to100 >= HighAt) return ClaustrophobiaBand.High;
            if (stress01to100 >= ElevatedAt) return ClaustrophobiaBand.Elevated;
            return ClaustrophobiaBand.Low;
        }

        public static string Label(ClaustrophobiaBand b) => b switch
        {
            ClaustrophobiaBand.Low => "CALM",
            ClaustrophobiaBand.Elevated => "UNEASY",
            ClaustrophobiaBand.High => "STRESSED",
            ClaustrophobiaBand.Severe => "SEVERE",
            ClaustrophobiaBand.Critical => "CRITICAL",
            _ => "CALM",
        };

        public static Color BarColor(float stress)
        {
            float t = Mathf.Clamp01(stress / 100f);
            if (t < 0.32f) return Color.Lerp(new Color(0.45f, 0.7f, 0.85f), new Color(0.7f, 0.78f, 0.55f), t / 0.32f);
            if (t < 0.52f) return Color.Lerp(new Color(0.7f, 0.78f, 0.55f), new Color(1f, 0.75f, 0.2f), (t - 0.32f) / 0.2f);
            if (t < 0.72f) return Color.Lerp(new Color(1f, 0.75f, 0.2f), new Color(1f, 0.48f, 0.12f), (t - 0.52f) / 0.2f);
            return Color.Lerp(new Color(1f, 0.48f, 0.12f), new Color(1f, 0.22f, 0.18f), (t - 0.72f) / 0.28f);
        }
    }
}
