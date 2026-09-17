using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Clean management-game IMGUI primitives for the Assign / Work Priorities testbed.
    /// Opaque enough for readability, rounded, minimal chrome, quiet accent.
    /// Cached textures/styles only — no per-OnGUI alloc, no blur.
    /// </summary>
    public static class DeepCoreBentoUi
    {
        // Solid enough that world / other HUD never bleeds through text.
        public static readonly Color OverlayBg = new(0.07f, 0.08f, 0.09f, 0.90f);
        public static readonly Color ModuleBg = new(0.09f, 0.1f, 0.11f, 0.55f);
        public static readonly Color RowIdle = new(1f, 1f, 1f, 0.03f);
        public static readonly Color SelectedFill = new(0.22f, 0.48f, 0.68f, 0.32f);
        public static readonly Color HoverFill = new(1f, 1f, 1f, 0.06f);
        public static readonly Color ControlFill = new(1f, 1f, 1f, 0.08f);
        public static readonly Color ControlFillHot = new(1f, 1f, 1f, 0.14f);
        public static readonly Color ControlFillActive = new(0.22f, 0.48f, 0.68f, 0.45f);

        // Quiet accent — used sparingly (selection wash / active control), never all text.
        public static readonly Color Accent = new(0.45f, 0.78f, 0.95f, 1f);
        public static readonly Color BorderQuiet = new(1f, 1f, 1f, 0.08f);

        public static readonly Color TextPrimary = new(0.96f, 0.97f, 0.98f, 1f);
        public static readonly Color TextSecondary = new(0.78f, 0.81f, 0.84f, 1f);
        public static readonly Color TextMuted = new(0.58f, 0.62f, 0.66f, 1f);

        public static readonly Color Positive = new(0.45f, 0.88f, 0.58f, 1f);
        public static readonly Color Warning = new(1f, 0.72f, 0.28f, 1f);
        public static readonly Color Danger = new(1f, 0.42f, 0.36f, 1f);

        public const float RadiusMain = 10f;
        public const float RadiusRow = 6f;
        public const float RadiusControl = 6f;

        const int SliceSize = 32;
        static readonly Dictionary<int, Texture2D> _roundTexByRadius = new(8);
        static readonly Dictionary<long, GUIStyle> _labelCache = new(64);
        static Texture2D _white;

        static Texture2D White => _white != null ? _white : (_white = Texture2D.whiteTexture);

        static Texture2D RoundSource(int radiusPx)
        {
            radiusPx = Mathf.Clamp(radiusPx, 2, 14);
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

        public static void FillRound(Rect r, Color color, float radius)
        {
            if (r.width < 2f || r.height < 2f) return;
            int rad = Mathf.Clamp(Mathf.RoundToInt(radius), 2, 14);
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

            GUI.DrawTextureWithTexCoords(new Rect(r.x, r.y, b, b), tex, new Rect(0f, 1f - v, u, v));
            GUI.DrawTextureWithTexCoords(new Rect(r.xMax - b, r.y, b, b), tex, new Rect(1f - u, 1f - v, u, v));
            GUI.DrawTextureWithTexCoords(new Rect(r.x, r.yMax - b, b, b), tex, new Rect(0f, 0f, u, v));
            GUI.DrawTextureWithTexCoords(new Rect(r.xMax - b, r.yMax - b, b, b), tex, new Rect(1f - u, 0f, u, v));
            GUI.DrawTextureWithTexCoords(new Rect(r.x + b, r.y, r.width - b * 2f, b), tex, new Rect(u, 1f - v, 1f - 2f * u, v));
            GUI.DrawTextureWithTexCoords(new Rect(r.x + b, r.yMax - b, r.width - b * 2f, b), tex, new Rect(u, 0f, 1f - 2f * u, v));
            GUI.DrawTextureWithTexCoords(new Rect(r.x, r.y + b, b, r.height - b * 2f), tex, new Rect(0f, v, u, 1f - 2f * v));
            GUI.DrawTextureWithTexCoords(new Rect(r.xMax - b, r.y + b, b, r.height - b * 2f), tex, new Rect(1f - u, v, u, 1f - 2f * v));
            GUI.DrawTextureWithTexCoords(
                new Rect(r.x + b, r.y + b, r.width - b * 2f, r.height - b * 2f),
                tex, new Rect(u, v, 1f - 2f * u, 1f - 2f * v));

            GUI.color = old;
        }

        /// <summary>Main panel — opaque charcoal, hairline neutral edge only.</summary>
        public static void DrawPanel(Rect r)
        {
            FillRound(r, OverlayBg, RadiusMain);
            // Extremely quiet edge — no cyan frame.
            var old = GUI.color;
            GUI.color = BorderQuiet;
            float t = 1f;
            float rad = RadiusMain;
            GUI.DrawTexture(new Rect(r.x + rad, r.y, r.width - rad * 2f, t), White);
            GUI.DrawTexture(new Rect(r.x + rad, r.yMax - t, r.width - rad * 2f, t), White);
            GUI.DrawTexture(new Rect(r.x, r.y + rad, t, r.height - rad * 2f), White);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y + rad, t, r.height - rad * 2f), White);
            GUI.color = old;
        }

        public static void DrawSelected(Rect r, bool selected, bool hover = false)
        {
            if (selected)
                FillRound(r, SelectedFill, RadiusRow);
            else if (hover)
                FillRound(r, HoverFill, RadiusRow);
        }

        public static bool DrawControl(Rect r, string label, bool active = false)
        {
            Vector2 mouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
            bool hover = r.Contains(mouse);
            Color fill = active ? ControlFillActive : (hover ? ControlFillHot : ControlFill);
            FillRound(r, fill, RadiusControl);

            Color tc = active ? TextPrimary : (hover ? TextPrimary : TextSecondary);
            var st = Label(11, tc, bold: active);
            st.alignment = TextAnchor.MiddleCenter;
            GUI.Label(r, label, st);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        public static bool DrawPriorityChip(Rect r, string label, bool active, bool selected)
        {
            Vector2 mouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
            bool hover = r.Contains(mouse);
            Color fill = selected || active ? ControlFillActive
                : (hover ? ControlFillHot : ControlFill);
            FillRound(r, fill, RadiusControl);

            Color tc = label == "Off" || label == "OFF"
                ? TextMuted
                : TextPrimary;
            var st = Label(13, tc, bold: true);
            st.alignment = TextAnchor.MiddleCenter;
            GUI.Label(r, label, st);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        public static void DrawDivider(float x, float y, float w)
        {
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.07f);
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
