using System;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Recruitment V1 hiring screen — cyberpunk industrial IMGUI overlay.
    /// Drawn by FreeMovementSocketMapRunner while session.IsOpen.
    /// </summary>
    public static class RecruitmentHiringUi
    {
        static readonly Color Bg = new(0.02f, 0.03f, 0.045f, 0.94f);
        static readonly Color Panel = new(0.04f, 0.06f, 0.09f, 0.88f);
        static readonly Color Cyan = new(0.35f, 0.9f, 1f, 1f);
        static readonly Color Green = new(0.35f, 0.95f, 0.55f, 1f);
        static readonly Color Amber = new(1f, 0.75f, 0.25f, 1f);
        static readonly Color White = new(0.9f, 0.93f, 0.96f, 1f);
        static readonly Color Mute = new(0.45f, 0.55f, 0.62f, 1f);
        static readonly Color Dim = new(0.3f, 0.38f, 0.44f, 1f);
        static readonly Color Red = new(1f, 0.35f, 0.35f, 1f);
        static readonly Color Pos = new(0.4f, 0.95f, 0.55f, 1f);
        static readonly Color Neg = new(1f, 0.45f, 0.4f, 1f);
        static readonly Color Mix = new(1f, 0.8f, 0.35f, 1f);

        static GUIStyle _label;
        static float _pulse;
        static WorkerRuntime _previewCache;
        static string _previewKey;

        public static void TickPulse(float t) => _pulse = t;

        static GUIStyle L(int size, Color c, bool bold = false)
        {
            _label ??= new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false };
            var s = new GUIStyle(_label)
            {
                fontSize = size,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                normal = { textColor = c },
                alignment = TextAnchor.UpperLeft,
            };
            return s;
        }

        static void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        static void Border(Rect r, Color c, float t = 1f)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        static bool Btn(Rect r, string label, bool selected, Color accent, Func<Rect, string, bool, Color, bool> drawCyber)
        {
            return drawCyber(r, label, selected, accent);
        }

        /// <summary>
        /// Full-screen hiring overlay. Returns: cancel, confirm, or none.
        /// </summary>
        public static HiringUiResult Draw(
            RecruitmentHiringSession session,
            float uiPulse,
            Func<Rect, string, bool, Color, bool> drawCyberButton,
            Action<Rect> blockInput)
        {
            if (session == null || !session.IsOpen)
                return HiringUiResult.None;

            TickPulse(uiPulse);
            var result = HiringUiResult.None;

            // Dim world
            Fill(new Rect(0, 0, Screen.width, Screen.height), Bg);

            float margin = 18f;
            var root = new Rect(margin, margin, Screen.width - margin * 2f, Screen.height - margin * 2f);
            Fill(root, Panel);
            Border(root, new Color(Cyan.r, Cyan.g, Cyan.b, 0.45f + 0.2f * _pulse), 2f);
            // Corner brackets
            float br = 14f;
            Fill(new Rect(root.x, root.y, br, 2f), Cyan);
            Fill(new Rect(root.x, root.y, 2f, br), Cyan);
            Fill(new Rect(root.xMax - br, root.y, br, 2f), Cyan);
            Fill(new Rect(root.xMax - 2f, root.y, 2f, br), Cyan);

            blockInput?.Invoke(root);

            float x = root.x + 16f;
            float y = root.y + 12f;
            float w = root.width - 32f;

            GUI.Label(new Rect(x, y, w * 0.6f, 22f), "RECRUITMENT V1  //  HIRING",
                L(16, Cyan, bold: true));
            GUI.Label(new Rect(x + w * 0.55f, y + 4f, w * 0.45f, 16f),
                $"CREW  {session.FilledCount}/5",
                L(12, session.AllJobsFilled ? Green : Amber, bold: true));
            y += 28f;
            GUI.Label(new Rect(x, y, w, 16f),
                "Hire one worker per job. Confirm replaces the active crew. Cancel leaves the current crew untouched.",
                L(10, Mute));
            y += 22f;

            // Job tabs
            float tabW = (w - 8f * 4f) / 5f;
            float tabH = 26f;
            for (int i = 0; i < RecruitmentCatalog.HireJobs.Length; i++)
            {
                var job = RecruitmentCatalog.HireJobs[i];
                var tr = new Rect(x + i * (tabW + 8f), y, tabW, tabH);
                bool filled = session.TryGetHired(job, out _);
                bool sel = session.BrowseJob == job;
                Color acc = filled ? Green : Cyan;
                string label = JobStatPreview.DisplayName(job).ToUpperInvariant();
                if (filled) label = "● " + label;
                if (Btn(tr, label, sel, acc, drawCyberButton))
                    session.SetBrowseJob(job);
            }
            y += tabH + 12f;

            // Selected crew strip
            float stripH = 52f;
            var strip = new Rect(x, y, w, stripH);
            Fill(strip, new Color(0.02f, 0.04f, 0.06f, 0.7f));
            Border(strip, new Color(Cyan.r, Cyan.g, Cyan.b, 0.25f));
            float sx = strip.x + 10f;
            GUI.Label(new Rect(sx, strip.y + 6f, 80f, 14f), "SELECTED", L(9, Mute, bold: true));
            sx += 78f;
            for (int i = 0; i < RecruitmentCatalog.HireJobs.Length; i++)
            {
                var job = RecruitmentCatalog.HireJobs[i];
                string name = session.HiredName(job);
                bool has = session.TryGetHired(job, out _);
                GUI.Label(new Rect(sx, strip.y + 6f, 150f, 12f),
                    JobStatPreview.DisplayName(job).ToUpperInvariant(),
                    L(8, Dim, bold: true));
                GUI.Label(new Rect(sx, strip.y + 20f, 150f, 16f),
                    name.ToUpperInvariant(),
                    L(11, has ? White : Mute, bold: has));
                if (has)
                {
                    var clr = new Rect(sx + 110f, strip.y + 28f, 36f, 16f);
                    if (Btn(clr, "CLR", false, Dim, drawCyberButton))
                        session.ClearHire(job);
                }
                sx += 168f;
            }
            y += stripH + 10f;

            // Main columns
            float leftW = 220f;
            float midW = Mathf.Min(340f, (w - leftW - 16f) * 0.42f);
            float rightW = w - leftW - midW - 24f;
            float bodyH = root.yMax - y - 56f;

            DrawCandidateList(session, new Rect(x, y, leftW, bodyH), drawCyberButton);
            DrawIdentity(session, new Rect(x + leftW + 12f, y, midW, bodyH), drawCyberButton);
            DrawStats(session, new Rect(x + leftW + midW + 24f, y, rightW, bodyH));

            // Footer
            float fy = root.yMax - 44f;
            if (!string.IsNullOrEmpty(session.StatusMessage))
                GUI.Label(new Rect(x, fy - 18f, w * 0.55f, 16f), session.StatusMessage, L(10, Amber));

            var cancelR = new Rect(x, fy, 120f, 28f);
            var confirmR = new Rect(root.xMax - 200f, fy, 184f, 28f);
            if (Btn(cancelR, "CANCEL", false, Dim, drawCyberButton))
                result = HiringUiResult.Cancel;

            bool canConfirm = session.AllJobsFilled;
            if (Btn(confirmR, canConfirm ? "CONFIRM CREW" : $"CONFIRM (NEED {RecruitmentCatalog.HireJobs.Length})", canConfirm, canConfirm ? Green : Dim, drawCyberButton))
            {
                if (canConfirm) result = HiringUiResult.Confirm;
                else session.StatusMessage = "Fill all 5 job seats before confirming.";
            }

            return result;
        }

        static void DrawCandidateList(
            RecruitmentHiringSession session,
            Rect r,
            Func<Rect, string, bool, Color, bool> drawCyber)
        {
            Fill(r, new Color(0.02f, 0.035f, 0.05f, 0.75f));
            Border(r, new Color(Cyan.r, Cyan.g, Cyan.b, 0.22f));
            float y = r.y + 8f;
            GUI.Label(new Rect(r.x + 10f, y, r.width - 20f, 14f), "CANDIDATES", L(10, Cyan, bold: true));
            y += 20f;

            var list = RecruitmentCatalog.ForJob(session.BrowseJob);
            float rowH = 36f;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                var row = new Rect(r.x + 8f, y, r.width - 16f, rowH - 4f);
                bool sel = i == session.BrowseIndex;
                bool hiredHere = session.TryGetHired(session.BrowseJob, out var h)
                                 && h != null && h.CandidateId == c.CandidateId;
                Fill(row, sel
                    ? new Color(0.06f, 0.12f, 0.16f, 0.85f)
                    : new Color(0.03f, 0.05f, 0.07f, 0.5f));
                if (sel) Border(row, new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f));
                GUI.Label(new Rect(row.x + 8f, row.y + 4f, row.width - 16f, 14f),
                    c.DisplayName.ToUpperInvariant(),
                    L(11, sel ? White : Mute, bold: sel));
                GUI.Label(new Rect(row.x + 8f, row.y + 18f, row.width - 16f, 12f),
                    $"WAGE {c.WageAsk}   {(hiredHere ? "● SEAT" : c.CareerStanding)}",
                    L(8, hiredHere ? Green : Dim));
                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                {
                    session.BrowseIndex = i;
                    session.HoverStat = null;
                }
                y += rowH;
            }

            var prev = new Rect(r.x + 8f, r.yMax - 32f, (r.width - 24f) * 0.5f, 24f);
            var next = new Rect(prev.xMax + 8f, prev.y, prev.width, 24f);
            if (drawCyber(prev, "◀ PREV", false, Dim))
                session.NextCandidate(-1);
            if (drawCyber(next, "NEXT ▶", false, Dim))
                session.NextCandidate(1);
        }

        static void DrawIdentity(
            RecruitmentHiringSession session,
            Rect r,
            Func<Rect, string, bool, Color, bool> drawCyber)
        {
            Fill(r, new Color(0.02f, 0.035f, 0.05f, 0.75f));
            Border(r, new Color(Cyan.r, Cyan.g, Cyan.b, 0.22f));
            var c = session.SelectedCandidate;
            if (c == null) return;

            float y = r.y + 8f;
            float lx = r.x + 12f;
            float tw = r.width - 24f;

            GUI.Label(new Rect(lx, y, tw, 14f), "IDENTITY", L(10, Cyan, bold: true));
            y += 18f;

            // Face
            string key = c.CandidateId;
            if (_previewKey != key || _previewCache == null)
            {
                _previewCache = c.PreviewRuntime(9100 + (c.CandidateId?.GetHashCode() ?? 0) & 0x3ff);
                _previewKey = key;
            }
            var faceR = new Rect(lx, y, 64f, 64f);
            WorkerFaceMonitor.Draw(faceR, _previewCache, Cyan, _pulse, selected: true);
            GUI.Label(new Rect(lx + 74f, y, tw - 74f, 20f),
                c.DisplayName.ToUpperInvariant(), L(14, White, bold: true));
            GUI.Label(new Rect(lx + 74f, y + 22f, tw - 74f, 14f),
                $"AGE {c.Age}   //   {c.CareerStanding.ToUpperInvariant()}", L(10, Mute));
            GUI.Label(new Rect(lx + 74f, y + 40f, tw - 74f, 14f),
                $"WAGE ASK  {c.WageAsk}", L(11, Amber, bold: true));
            y += 72f;

            GUI.Label(new Rect(lx, y, tw, 12f), "BACKGROUND", L(8, Mute, bold: true));
            y += 14f;
            GUI.Label(new Rect(lx, y, tw, 48f), c.Background, L(10, White));
            y += 52f;

            GUI.Label(new Rect(lx, y, tw, 12f), "JOB", L(8, Mute, bold: true));
            y += 14f;
            GUI.Label(new Rect(lx, y, tw, 14f),
                JobStatPreview.DisplayName(c.TargetJob).ToUpperInvariant(),
                L(12, Cyan, bold: true));
            y += 18f;
            GUI.Label(new Rect(lx, y, tw, 40f), c.SuitabilitySummary, L(10, White));
            y += 44f;

            GUI.Label(new Rect(lx, y, tw, 12f), "TRAITS", L(8, Mute, bold: true));
            y += 14f;
            if (c.Traits != null)
            {
                for (int i = 0; i < c.Traits.Length; i++)
                {
                    var def = RecruitmentTraits.Get(c.Traits[i]);
                    Color tc = def.Kind switch
                    {
                        RecruitmentTraitKind.Positive => Pos,
                        RecruitmentTraitKind.Negative => Neg,
                        _ => Mix,
                    };
                    GUI.Label(new Rect(lx, y, tw, 14f), def.Name.ToUpperInvariant(), L(11, tc, bold: true));
                    y += 14f;
                    GUI.Label(new Rect(lx, y, tw, 28f), $"{def.Summary}  [{def.EffectLabel}]", L(9, Mute));
                    y += 30f;
                }
            }

            GUI.Label(new Rect(lx, y, tw, 12f), "PERSONALITY  (incomplete)", L(8, Mute, bold: true));
            y += 14f;
            if (c.PersonalityTags != null && c.PersonalityTags.Length > 0)
                GUI.Label(new Rect(lx, y, tw, 20f),
                    string.Join("  ·  ", c.PersonalityTags).ToUpperInvariant(),
                    L(10, White));
            y += 28f;

            var hireR = new Rect(lx, Mathf.Min(y, r.yMax - 36f), tw, 28f);
            if (drawCyber(hireR, "HIRE FOR SEAT", true, Green))
                session.HireSelected();
        }

        static void DrawStats(RecruitmentHiringSession session, Rect r)
        {
            Fill(r, new Color(0.02f, 0.035f, 0.05f, 0.75f));
            Border(r, new Color(Cyan.r, Cyan.g, Cyan.b, 0.22f));
            var c = session.SelectedCandidate;
            if (c == null) return;

            // Stats with traits applied for truthful preview
            var preview = c.PreviewRuntime(9200);
            var stats = preview.Stats;
            float y = r.y + 8f;
            float lx = r.x + 10f;
            float tw = r.width - 20f;

            GUI.Label(new Rect(lx, y, tw, 14f), "STATS  (with traits)", L(10, Cyan, bold: true));
            y += 18f;

            session.HoverStat = null;
            y = DrawPillar(session, stats, "PHYSICAL  //  BODY", WorkerStatPillar.Body, lx, y, tw);
            y = DrawPillar(session, stats, "TECHNICAL / COGNITIVE  //  MIND", WorkerStatPillar.Mind, lx, y, tw);
            y = DrawPillar(session, stats, "WORK / SOUL  //  SOCIAL", WorkerStatPillar.Soul, lx, y, tw);

            // Tooltip panel at bottom of column
            if (session.HoverStat.HasValue)
            {
                var id = session.HoverStat.Value;
                var tip = new Rect(lx, Mathf.Max(y + 4f, r.yMax - 150f), tw, 142f);
                Fill(tip, new Color(0.03f, 0.06f, 0.09f, 0.95f));
                Border(tip, Amber, 1.5f);
                float ty = tip.y + 6f;
                GUI.Label(new Rect(tip.x + 8f, ty, tip.width - 16f, 14f),
                    JobStatPreview.StatDisplayName(id).ToUpperInvariant(),
                    L(11, Amber, bold: true));
                ty += 16f;
                GUI.Label(new Rect(tip.x + 8f, ty, tip.width - 16f, 12f), "GENERAL", L(8, Mute, bold: true));
                ty += 12f;
                GUI.Label(new Rect(tip.x + 8f, ty, tip.width - 16f, 28f),
                    RecruitmentStatTooltips.General(id), L(9, White));
                ty += 30f;
                var (forJob, imp, prov) = RecruitmentStatTooltips.ForJob(id, session.BrowseJob);
                GUI.Label(new Rect(tip.x + 8f, ty, tip.width - 16f, 12f),
                    $"FOR {JobStatPreview.DisplayName(session.BrowseJob).ToUpperInvariant()}"
                    + (prov ? "  (PROVISIONAL)" : ""),
                    L(8, Mute, bold: true));
                ty += 12f;
                GUI.Label(new Rect(tip.x + 8f, ty, tip.width - 16f, 40f), forJob, L(9, White));
                ty += 42f;
                Color ic = imp switch
                {
                    StatJobImportance.High => Green,
                    StatJobImportance.Useful => Cyan,
                    StatJobImportance.Situational => Amber,
                    StatJobImportance.Low => Mute,
                    _ => Dim,
                };
                GUI.Label(new Rect(tip.x + 8f, ty, tip.width - 16f, 14f),
                    $"IMPORTANCE  {RecruitmentStatTooltips.ImportanceLabel(imp)}",
                    L(11, ic, bold: true));
            }
            else
            {
                GUI.Label(new Rect(lx, r.yMax - 24f, tw, 16f),
                    "HOVER A STAT FOR JOB-SPECIFIC TOOLTIP", L(9, Dim));
            }
        }

        static float DrawPillar(
            RecruitmentHiringSession session,
            WorkerStats stats,
            string title,
            WorkerStatPillar pillar,
            float x,
            float y,
            float w)
        {
            GUI.Label(new Rect(x, y, w, 12f), title, L(8, Mute, bold: true));
            y += 14f;
            int start = pillar == WorkerStatPillar.Body ? 0 : pillar == WorkerStatPillar.Mind ? 10 : 20;
            float colW = (w - 8f) * 0.5f;
            for (int i = 0; i < 10; i++)
            {
                var id = (WorkerStatId)(start + i);
                int col = i % 2;
                int row = i / 2;
                float rx = x + col * (colW + 8f);
                float ry = y + row * 18f;
                var cell = new Rect(rx, ry, colW, 16f);
                bool hover = cell.Contains(Event.current != null
                    ? Event.current.mousePosition
                    : Vector2.zero);
                if (hover) session.HoverStat = id;

                bool relevant = false;
                var rel = JobStatPreview.RelevantStats(session.BrowseJob);
                for (int k = 0; k < rel.Count; k++)
                    if (rel[k] == id) { relevant = true; break; }

                int v = stats.Get(id);
                Color vc = v >= 16 ? Green : v <= 6 ? Red : White;
                if (hover) Fill(cell, new Color(0.08f, 0.14f, 0.18f, 0.9f));
                GUI.Label(new Rect(cell.x, cell.y, cell.width * 0.72f, 16f),
                    JobStatPreview.StatDisplayName(id),
                    L(9, relevant ? Cyan : Mute));
                GUI.Label(new Rect(cell.x + cell.width * 0.72f, cell.y, cell.width * 0.28f, 16f),
                    v.ToString(), L(10, vc, bold: true));
            }
            return y + 5 * 18f + 8f;
        }
    }

    public enum HiringUiResult : byte
    {
        None = 0,
        Cancel = 1,
        Confirm = 2,
    }
}
