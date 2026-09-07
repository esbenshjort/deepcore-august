using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Compact roster status badges — presentation only.
    /// Surfaces act-on-this conditions; detail lives in hover tooltips.
    /// </summary>
    public static class CrewRosterStatusUi
    {
        public const float IconSize = 10f;
        public const float IconGap = 2f;

        public enum Kind : byte
        {
            Dead = 0,
            Incapacitated = 1,
            Injury = 2,
            NeedsCare = 3,
            Stomach = 4,
            Exhaustion = 5,
            Grudge = 6,
        }

        public readonly struct Badge
        {
            public readonly Kind Kind;
            public readonly Color Fill;
            public readonly Color Border;
            public readonly string Glyph;
            public readonly string Tooltip;
            public readonly int Count;

            public Badge(Kind kind, Color fill, Color border, string glyph, string tooltip, int count = 0)
            {
                Kind = kind;
                Fill = fill;
                Border = border;
                Glyph = glyph ?? "";
                Tooltip = tooltip ?? "";
                Count = count;
            }
        }

        static readonly Color DeadFill = new(0.12f, 0.12f, 0.14f, 0.92f);
        static readonly Color DeadBorder = new(0.55f, 0.55f, 0.58f, 1f);
        static readonly Color IncapFill = new(0.72f, 0.08f, 0.12f, 0.98f);
        static readonly Color IncapBorder = new(1f, 0.35f, 0.28f, 1f);
        static readonly Color InjMinor = new(0.55f, 0.18f, 0.16f, 0.88f);
        static readonly Color InjModerate = new(0.85f, 0.18f, 0.16f, 0.95f);
        static readonly Color InjSerious = new(1f, 0.12f, 0.1f, 1f);
        static readonly Color CareFill = new(0.9f, 0.45f, 0.12f, 0.95f);
        static readonly Color CareBorder = new(1f, 0.72f, 0.25f, 1f);
        static readonly Color StomachFill = new(0.35f, 0.55f, 0.18f, 0.92f);
        static readonly Color StomachBorder = new(0.7f, 0.95f, 0.35f, 1f);
        static readonly Color ExhFill = new(0.55f, 0.28f, 0.08f, 0.95f);
        static readonly Color ExhBorder = new(1f, 0.62f, 0.2f, 1f);
        static readonly Color GrudgeFill = new(0.45f, 0.12f, 0.22f, 0.95f);
        static readonly Color GrudgeBorder = new(1f, 0.35f, 0.45f, 1f);

        public static void Collect(
            WorkerRuntime wr,
            List<Badge> into,
            System.Func<WorkerRuntime, string> grudgeTooltipOrNull = null)
        {
            into.Clear();
            if (wr == null) return;

            if (!wr.IsAlive)
            {
                into.Add(new Badge(Kind.Dead, DeadFill, DeadBorder, "X",
                    "DEAD\nNo longer in the crew rotation.\nIdentity and memory remain."));
                return;
            }

            if (wr.State != null && wr.State.Incapacitated)
            {
                into.Add(new Badge(Kind.Incapacitated, IncapFill, IncapBorder, "↓",
                    BuildIncapacitatedTooltip(wr)));
            }

            // Injury — most severe; count if multiple
            if (wr.Injuries != null && wr.Injuries.Count > 0)
            {
                WorkerInjuryRecord worst = null;
                int n = 0;
                for (int i = 0; i < wr.Injuries.Active.Count; i++)
                {
                    var inj = wr.Injuries.Active[i];
                    if (inj == null || !inj.Active) continue;
                    n++;
                    if (worst == null || (int)inj.Severity > (int)worst.Severity)
                        worst = inj;
                }
                if (worst != null)
                {
                    Color fill = worst.Severity switch
                    {
                        WorkerInjurySeverity.Critical => InjSerious,
                        WorkerInjurySeverity.Serious => InjSerious,
                        WorkerInjurySeverity.Moderate => InjModerate,
                        _ => InjMinor,
                    };
                    Color border = worst.Severity >= WorkerInjurySeverity.Serious
                        ? new Color(1f, 0.55f, 0.5f, 1f)
                        : new Color(0.9f, 0.4f, 0.35f, 0.85f);
                    string tip = BuildInjuryTooltip(worst, wr, n);
                    string glyph = n > 1 ? Mathf.Min(n, 9).ToString() : "!";
                    into.Add(new Badge(Kind.Injury, fill, border, glyph, tip, n));
                }
            }

            if (wr.State != null && wr.State.NeedsCare)
            {
                into.Add(new Badge(Kind.NeedsCare, CareFill, CareBorder, "+",
                    "NEEDS CARE\nInjury requires camp attention.\nSteward can tend minor/moderate wounds.\nSerious injuries stay flagged."));
            }

            if (wr.CampBody != null && wr.CampBody.HasStomachUpset)
            {
                var sev = wr.CampBody.StomachUpset;
                string tip =
                    $"STOMACH UPSET\n{sev}\n~{wr.CampBody.StomachUpsetHoursLeft:0.#}h remaining\n"
                    + (sev >= StomachUpsetSeverity.Severe
                        ? "Toilet urgency ~hourly.\nSleep and focus disrupted."
                        : sev >= StomachUpsetSeverity.Moderate
                            ? "Frequent toilet trips.\nFocus under pressure."
                            : "Mild discomfort.\nToilet need rises faster.");
                into.Add(new Badge(Kind.Stomach, StomachFill, StomachBorder, "S", tip));
            }

            if (wr.State != null && wr.State.ExhaustionLatched)
            {
                float ratio = WorkerJobDemand.StaminaRatio(wr);
                into.Add(new Badge(Kind.Exhaustion, ExhFill, ExhBorder, "E",
                    $"SEVERE EXHAUSTION\nPhysical reserve critical ({ratio * 100f:0}%).\nWork output and footing suffer.\nRest or sleep to recover."));
            }

            if (grudgeTooltipOrNull != null)
            {
                string g = grudgeTooltipOrNull(wr);
                if (!string.IsNullOrEmpty(g))
                    into.Add(new Badge(Kind.Grudge, GrudgeFill, GrudgeBorder, "G", g));
            }
        }

        static string BuildIncapacitatedTooltip(WorkerRuntime wr)
        {
            var sb = new StringBuilder(280);
            sb.Append("INCAPACITATED\nCannot work, walk, or self-rescue.\nRemains at accident site.");
            if (wr.State != null && !string.IsNullOrEmpty(wr.State.IncapacitatedCause))
            {
                sb.Append("\nCause: ");
                sb.Append(wr.State.IncapacitatedCause);
            }
            if (wr.State != null && wr.State.TrappedFromCamp)
                sb.Append("\nTRAPPED — route to camp blocked.\nCrew must clear debris and reach them.");
            else
                sb.Append("\nRequires crew assistance to return.");
            if (wr.Injuries != null && wr.Injuries.Count > 0)
            {
                sb.Append("\n—");
                int n = 0;
                for (int i = 0; i < wr.Injuries.Active.Count && n < 4; i++)
                {
                    var inj = wr.Injuries.Active[i];
                    if (inj == null || !inj.Active) continue;
                    sb.Append('\n');
                    sb.Append(inj.DisplayName);
                    n++;
                }
            }
            if (wr.State != null && wr.State.NeedsCare)
                sb.Append("\nNeeds Care after rescue.");
            return sb.ToString();
        }

        static string BuildInjuryTooltip(WorkerInjuryRecord inj, WorkerRuntime wr, int totalCount)
        {
            var sb = new StringBuilder(280);
            sb.Append(inj.DisplayName);
            sb.Append('\n');
            sb.Append(inj.SeverityLabel);
            sb.Append('\n');
            sb.Append(inj.BodyPart.ToString().ToUpperInvariant());
            string cause = !string.IsNullOrEmpty(inj.CauseLabel)
                ? inj.CauseLabel
                : CauseLabel(inj.Cause);
            if (!string.IsNullOrEmpty(cause))
            {
                sb.Append('\n');
                sb.Append(cause);
            }

            var status = InjuryResponse.EvaluateWorkStatus(wr);
            sb.Append("\n\nSTATUS: ");
            sb.Append(InjuryResponse.StatusLabel(status));
            sb.Append('\n');
            sb.Append(InjuryResponse.StatusActionLine(wr, status));

            sb.Append("\n\n");
            sb.Append(InjuryResponse.MovementImpactSummary(wr));
            sb.Append('\n');
            sb.Append(InjuryResponse.FrustrationImpactSummary(inj));

            string affect = WorkerInjuryConsequences.Describe(inj);
            if (!string.IsNullOrEmpty(affect))
            {
                sb.Append('\n');
                sb.Append(affect);
            }

            if (inj.StabilizedBySteward)
                sb.Append("\nTreatment: stabilized by Steward (recovery improved).");
            else if (wr.CampBody != null && wr.CampBody.SeekingStewardCare)
                sb.Append("\nTreatment: awaiting Steward at camp.");
            else if (wr.CampBody != null && wr.CampBody.InjuryReturnActive)
                sb.Append("\nTreatment: en route to camp.");
            else
                sb.Append("\nTreatment: none yet.");

            int days = Mathf.Max(1, Mathf.CeilToInt(inj.RecoveryGameHoursLeft / 24f));
            sb.Append("\nEstimated recovery: ");
            sb.Append(days);
            sb.Append(days == 1 ? " day." : " days.");
            if (wr.State != null && wr.State.NeedsCare)
                sb.Append("\nNeeds Care.");
            if (totalCount > 1)
            {
                sb.Append('\n');
                sb.Append(totalCount);
                sb.Append(" active injuries.");
            }
            return sb.ToString();
        }

        static string CauseLabel(WorkerInjuryCause c) => c switch
        {
            WorkerInjuryCause.TerrainFall => "Terrain fall",
            WorkerInjuryCause.ExhaustionFall => "Exhaustion fall",
            WorkerInjuryCause.LoadedFall => "Loaded fall",
            WorkerInjuryCause.ExcavationOverheat => "Excavation overheat",
            WorkerInjuryCause.SocialFight => "Fight",
            WorkerInjuryCause.TunnelCollapse => "Tunnel collapse",
            WorkerInjuryCause.DebrisImpact => "Debris impact",
            _ => "",
        };

        /// <summary>
        /// Draw icons top-right inside card. Sets tooltip via out when hovered.
        /// Returns width used.
        /// </summary>
        public static float Draw(
            Rect card,
            List<Badge> badges,
            GUIStyle glyphStyle,
            out string hoverTooltip,
            out Vector2 hoverGui)
        {
            hoverTooltip = null;
            hoverGui = default;
            if (badges == null || badges.Count == 0) return 0f;

            float totalW = badges.Count * IconSize + (badges.Count - 1) * IconGap;
            float x = card.xMax - 8f - totalW;
            float y = card.y + 6f;
            var e = Event.current;

            for (int i = 0; i < badges.Count; i++)
            {
                var b = badges[i];
                var r = new Rect(x + i * (IconSize + IconGap), y, IconSize, IconSize);
                DrawIcon(r, b, glyphStyle);

                // Stronger outer frame for serious/critical injury or dead
                bool emphasize = b.Kind == Kind.Dead
                    || b.Kind == Kind.Incapacitated
                    || (b.Kind == Kind.Injury && b.Fill.r >= 0.98f);
                if (emphasize)
                {
                    var prev = GUI.color;
                    GUI.color = new Color(b.Border.r, b.Border.g, b.Border.b, 0.7f);
                    GUI.DrawTexture(new Rect(r.x - 1f, r.y - 1f, r.width + 2f, 1f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.x - 1f, r.yMax, r.width + 2f, 1f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.x - 1f, r.y, 1f, r.height), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.xMax, r.y, 1f, r.height), Texture2D.whiteTexture);
                    GUI.color = prev;
                }

                if (e != null && e.type == EventType.Repaint && r.Contains(e.mousePosition)
                    && !string.IsNullOrEmpty(b.Tooltip))
                {
                    hoverTooltip = b.Tooltip;
                    hoverGui = e.mousePosition;
                }
            }

            return totalW;
        }

        static void DrawIcon(Rect r, Badge b, GUIStyle glyphStyle)
        {
            var prev = GUI.color;
            GUI.color = b.Fill;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = b.Border;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1f, r.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, 1f, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 1f, r.y, 1f, r.height), Texture2D.whiteTexture);
            GUI.color = prev;

            if (!string.IsNullOrEmpty(b.Glyph) && glyphStyle != null)
            {
                var gs = new GUIStyle(glyphStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 8,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white },
                };
                GUI.Label(r, b.Glyph, gs);
            }
        }
    }
}
