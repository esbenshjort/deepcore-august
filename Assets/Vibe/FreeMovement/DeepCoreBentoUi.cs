using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Clean management-game IMGUI primitives — transparent charcoal, subtle neon blue,
    /// rounded corners, spacing-led hierarchy. Cached textures/styles only.
    /// Visual testbed language for Assign / Work Priorities; do not propagate yet.
    /// </summary>
    public static class DeepCoreBentoUi
    {
        // ——— Palette (restrained neon blue, high-contrast type) ———
        public static readonly Color OverlayBg = new(0.06f, 0.07f, 0.08f, 0.42f);
        public static readonly Color ModuleBg = new(0.08f, 0.09f, 0.1f, 0.55f);
        public static readonly Color ModuleBgQuiet = new(0.07f, 0.08f, 0.09f, 0.38f);
        public static readonly Color SelectedFill = new(0.18f, 0.42f, 0.58f, 0.28f);
        public static readonly Color HoverFill = new(0.14f, 0.28f, 0.38f, 0.22f);
        public static readonly Color ControlFill = new(0.1f, 0.12f, 0.14f, 0.55f);
        public static readonly Color ControlFillHot = new(0.14f, 0.32f, 0.42f, 0.55f);
        public static readonly Color ControlFillActive = new(0.16f, 0.4f, 0.52f, 0.62f);

        public static readonly Color Accent = new(0.35f, 0.78f, 0.95f, 1f);
        public static readonly Color AccentSoft = new(0.35f, 0.78f, 0.95f, 0.45f);
        public static readonly Color AccentEdge = new(0.35f, 0.78f, 0.95f, 0.55f);
        public static readonly Color BorderSubtle = new(0.35f, 0.78f, 0.95f, 0.22f);

        public static readonly Color TextPrimary = new(0.94f, 0.96f, 0.98f, 1f);
        public static readonly Color TextSecondary = new(0.72f, 0.76f, 0.8f, 1f);
        public static readonly Color TextMuted = new(0.55f, 0.6f, 0.64f, 1f);
        public static readonly Color TextInactive = new(0.48f, 0.52f, 0.56f, 1f);

        public static readonly Color Positive = new(0.4f, 0.92f, 0.58f, 1f);
        public static readonly Color Warning = new(1f, 0.72f, 0.28f, 1f);
        public static readonly Color Danger = new(1f, 0.38f, 0.32f, 1f);

        public const float RadiusMain = 8f;
        public const float RadiusModule = 6f;
        public const float RadiusControl = 5f;
        public const float RadiusChip = 4f;

        const int SliceSize = 32;
        static readonly Dictionary<int, Texture2D> _roundTexByRadius = new(8);
        static readonly Dictionary<long, GUIStyle> _labelCache = new(48);
        static Texture2D _white;

        static Texture2D White => _white != null ? _white : (_white = Texture2D.whiteTexture);

        static Texture2D RoundSource(int radiusPx)
        {
            radiusPx = Mathf.Clamp(radiusPx, 2, 12);
            if (_roundTexByRadius.TryGetValue(radiusPx, out var tex) && tex != null)
                return tex;

            tex = new Texture2D(SliceSize, SliceSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            float r = radiusPx;
            float max = SliceSize - 1;
            for (int y = 0; y < SliceSize; y++)
            for (int x = 0; x < SliceSize; x++)
            {
                // Distance to nearest edge for rounded-rect SDF
                float dx = 0f;
                if (x < r) dx = r - x;
                else if (x > max - r) dx = x - (max - r);
                float dy = 0f;
                if (y < r) dy = r - y;
                else if (y > max - r) dy = y - (max - r);

                float a = 1f;
                if (dx > 0f && dy > 0f)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    a = Mathf.Clamp01(r + 0.5f - d);
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply(false, true);
            _roundTexByRadius[radiusPx] = tex;
            return tex;
        }

        /// <summary>9-slice rounded fill from cached white mask (tinted via GUI.color).</summary>
        public static void FillRound(Rect r, Color color, float radius)
        {
            if (r.width < 2f || r.height < 2f) return;
            int rad = Mathf.Clamp(Mathf.RoundToInt(radius), 2, 12);
            float b = rad;
            if (r.width < b * 2f + 1f || r.height < b * 2f + 1f)
            {
                var prev = GUI.color;
                GUI.color = color;
                GUI.DrawTexture(r, White);
                GUI.color = prev;
                return;
            }

            var tex = RoundSource(rad);
            float u = b / SliceSize;
            float v = b / SliceSize;
            var old = GUI.color;
            GUI.color = color;

            // Corners
            GUI.DrawTextureWithTexCoords(new Rect(r.x, r.y, b, b), tex, new Rect(0f, 1f - v, u, v));
            GUI.DrawTextureWithTexCoords(new Rect(r.xMax - b, r.y, b, b), tex, new Rect(1f - u, 1f - v, u, v));
            GUI.DrawTextureWithTexCoords(new Rect(r.x, r.yMax - b, b, b), tex, new Rect(0f, 0f, u, v));
            GUI.DrawTextureWithTexCoords(new Rect(r.xMax - b, r.yMax - b, b, b), tex, new Rect(1f - u, 0f, u, v));
            // Edges
            GUI.DrawTextureWithTexCoords(new Rect(r.x + b, r.y, r.width - b * 2f, b), tex, new Rect(u, 1f - v, 1f - 2f * u, v));
            GUI.DrawTextureWithTexCoords(new Rect(r.x + b, r.yMax - b, r.width - b * 2f, b), tex, new Rect(u, 0f, 1f - 2f * u, v));
            GUI.DrawTextureWithTexCoords(new Rect(r.x, r.y + b, b, r.height - b * 2f), tex, new Rect(0f, v, u, 1f - 2f * v));
            GUI.DrawTextureWithTexCoords(new Rect(r.xMax - b, r.y + b, b, r.height - b * 2f), tex, new Rect(1f - u, v, u, 1f - 2f * v));
            // Center
            GUI.DrawTextureWithTexCoords(
                new Rect(r.x + b, r.y + b, r.width - b * 2f, r.height - b * 2f),
                tex, new Rect(u, v, 1f - 2f * u, 1f - 2f * v));

            GUI.color = old;
        }

        public static void StrokeRound(Rect r, Color color, float radius, float thickness = 1f)
        {
            if (r.width < 4f || r.height < 4f) return;
            float t = Mathf.Max(1f, thickness);
            float rad = Mathf.Min(radius, r.width * 0.5f, r.height * 0.5f);
            var old = GUI.color;
            GUI.color = color;
            // Straight edges only — corners stay soft from the fill; avoids technical brackets.
            GUI.DrawTexture(new Rect(r.x + rad, r.y, r.width - rad * 2f, t), White);
            GUI.DrawTexture(new Rect(r.x + rad, r.yMax - t, r.width - rad * 2f, t), White);
            GUI.DrawTexture(new Rect(r.x, r.y + rad, t, r.height - rad * 2f), White);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y + rad, t, r.height - rad * 2f), White);
            GUI.color = old;
        }

        public static void DrawPanel(Rect r, bool lit = false)
        {
            FillRound(r, lit ? ModuleBg : OverlayBg, RadiusMain);
            StrokeRound(r, lit ? AccentSoft : BorderSubtle, RadiusMain, 1f);
        }

        public static void DrawModule(Rect r, bool quiet = false)
        {
            FillRound(r, quiet ? ModuleBgQuiet : ModuleBg, RadiusModule);
        }

        public static void DrawSelected(Rect r, bool selected, bool hover = false)
        {
            if (selected)
            {
                FillRound(r, SelectedFill, RadiusModule);
                var old = GUI.color;
                GUI.color = AccentEdge;
                GUI.DrawTexture(new Rect(r.x + 3f, r.y + 5f, 2f, Mathf.Max(4f, r.height - 10f)), White);
                GUI.color = old;
            }
            else if (hover)
            {
                FillRound(r, HoverFill, RadiusModule);
            }
        }

        public static bool DrawControl(Rect r, string label, bool active = false, bool accentText = false)
        {
            Vector2 mouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
            bool hover = r.Contains(mouse);
            Color fill = active ? ControlFillActive : (hover ? ControlFillHot : ControlFill);
            FillRound(r, fill, RadiusControl);
            if (active || hover)
                StrokeRound(r, active ? AccentSoft : BorderSubtle, RadiusControl, 1f);

            Color tc = active || accentText ? Accent
                : hover ? TextPrimary
                : TextSecondary;
            var st = Label(11, tc, bold: active);
            st.alignment = TextAnchor.MiddleCenter;
            GUI.Label(r, label, st);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        public static bool DrawPriorityChip(Rect r, string label, bool active, bool selected)
        {
            Vector2 mouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
            bool hover = r.Contains(mouse);
            Color fill = selected ? ControlFillActive
                : active ? ControlFillHot
                : (hover ? HoverFill : ControlFill);
            FillRound(r, fill, RadiusChip);
            if (selected)
                StrokeRound(r, AccentSoft, RadiusChip, 1f);

            Color tc = label == "OFF" ? TextMuted
                : (selected || active ? Accent : TextPrimary);
            var st = Label(12, tc, bold: true);
            st.alignment = TextAnchor.MiddleCenter;
            GUI.Label(r, label, st);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        public static void DrawHairline(float x, float y, float w, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, 1f), White);
            GUI.color = old;
        }

        public static GUIStyle Label(int size, Color color, bool bold = false)
        {
            long key = ((long)size << 40)
                       ^ ((long)(bold ? 1 : 0) << 32)
                       ^ ((long)(color.r * 255) << 24)
                       ^ ((long)(color.g * 255) << 16)
                       ^ ((long)(color.b * 255) << 8)
                       ^ (long)(color.a * 255);
            if (_labelCache.TryGetValue(key, out var st) && st != null)
                return st;

            st = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                richText = false,
                clipping = TextClipping.Clip,
                wordWrap = false,
                alignment = TextAnchor.UpperLeft,
            };
            st.normal.textColor = color;
            st.hover.textColor = color;
            st.active.textColor = color;
            _labelCache[key] = st;
            return st;
        }

        public static GUIStyle LabelWrap(int size, Color color)
        {
            long key = (1L << 48) ^ ((long)size << 40)
                       ^ ((long)(color.r * 255) << 24)
                       ^ ((long)(color.g * 255) << 16)
                       ^ ((long)(color.b * 255) << 8)
                       ^ (long)(color.a * 255);
            if (_labelCache.TryGetValue(key, out var st) && st != null)
                return st;

            st = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = FontStyle.Normal,
                richText = false,
                wordWrap = true,
                clipping = TextClipping.Clip,
                alignment = TextAnchor.UpperLeft,
            };
            st.normal.textColor = color;
            _labelCache[key] = st;
            return st;
        }
    }
}
