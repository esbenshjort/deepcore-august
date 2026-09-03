using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Roster helmet/visor biometric monitor — visual only.
    /// Mood from Morale + Frustration + FocusState (not Morale alone).
    /// Industrial cyber HUD: near-black glass, cyan edge light, angular brackets.
    /// </summary>
    public static class WorkerFaceMonitor
    {
        public const float DefaultWidth = 68f;

        public enum MoodBand : byte
        {
            Bad = 0,
            Neutral = 1,
            Good = 2,
        }

        public struct MoodSample
        {
            /// <summary>-1 bad … +1 good.</summary>
            public float Score;
            public MoodBand Band;
            public Color Tint;
        }

        struct CacheKey : System.IEquatable<CacheKey>
        {
            public int WorkerId;
            public MoodBand Band;
            public bool NeedsCare;
            public bool Exhausted;

            public bool Equals(CacheKey o) =>
                WorkerId == o.WorkerId && Band == o.Band
                && NeedsCare == o.NeedsCare && Exhausted == o.Exhausted;

            public override bool Equals(object obj) => obj is CacheKey k && Equals(k);
            public override int GetHashCode() =>
                WorkerId * 397 ^ ((int)Band << 3) ^ (NeedsCare ? 16 : 0) ^ (Exhausted ? 32 : 0);
        }

        static readonly Dictionary<CacheKey, Texture2D> Cache = new(24);

        // Art direction — near-black industrial + cyan / amber / critical
        static readonly Color PanelFill = new(0.04f, 0.05f, 0.07f, 0.88f);
        static readonly Color PanelInner = new(0.025f, 0.03f, 0.04f, 1f);
        static readonly Color CyanEdge = new(0.25f, 0.78f, 0.95f, 1f);
        static readonly Color CyanDim = new(0.18f, 0.45f, 0.55f, 1f);
        static readonly Color Metal = new(0.38f, 0.42f, 0.48f);
        static readonly Color MetalHi = new(0.58f, 0.64f, 0.72f);
        static readonly Color MetalDeep = new(0.12f, 0.14f, 0.17f);
        static readonly Color Orange = new(0.86f, 0.48f, 0.14f);
        static readonly Color Charcoal = new(0.07f, 0.08f, 0.09f);
        static readonly Color VisorGlass = new(0.04f, 0.07f, 0.09f, 0.95f);

        static GUIStyle _idStyle;

        public static MoodSample Sample(WorkerState st)
        {
            if (st == null)
            {
                return new MoodSample
                {
                    Score = 0f,
                    Band = MoodBand.Neutral,
                    Tint = MoodTint(0f),
                };
            }

            float moraleN = (st.Morale - 50f) / 50f;
            float focusN = (st.FocusState - 50f) / 50f;
            float frustN = -(st.Frustration - 15f) / 70f;
            float fatigueN = -(st.MentalFatigue - 25f) / 75f;
            float injuryPull = st.Injury >= 40f ? -0.25f - (st.Injury - 40f) / 120f : 0f;
            if (st.NeedsCare) injuryPull -= 0.35f;

            float score = moraleN * 0.38f
                          + focusN * 0.27f
                          + frustN * 0.28f
                          + fatigueN * 0.12f
                          + injuryPull;
            score = Mathf.Clamp(score, -1f, 1f);

            MoodBand band = score <= -0.22f ? MoodBand.Bad
                : score >= 0.22f ? MoodBand.Good
                : MoodBand.Neutral;

            return new MoodSample
            {
                Score = score,
                Band = band,
                Tint = MoodTint(score),
            };
        }

        public static Color MoodTint(float score01)
        {
            // Critical red ← amber → neon green (restrained)
            Color bad = new(0.88f, 0.22f, 0.2f, 1f);
            Color mid = new(0.9f, 0.72f, 0.22f, 1f);
            Color good = new(0.28f, 0.88f, 0.48f, 1f);
            if (score01 < 0f)
                return Color.Lerp(mid, bad, Mathf.Clamp01(-score01));
            return Color.Lerp(mid, good, Mathf.Clamp01(score01));
        }

        /// <summary>Draws a compact biometric visor portrait left of a roster card.</summary>
        public static void Draw(Rect r, WorkerRuntime wr, Color workerAccent, float uiPulse = 0f, bool selected = false)
        {
            if (wr == null) return;
            var mood = Sample(wr.State);
            var prev = GUI.color;

            // Outer housing — transparent near-black panel
            GUI.color = PanelFill;
            GUI.DrawTexture(r, Texture2D.whiteTexture);

            // Selected: persistent cyan accent line on left (machine-control language)
            if (selected)
            {
                GUI.color = new Color(CyanEdge.r, CyanEdge.g, CyanEdge.b, 0.85f + 0.1f * uiPulse);
                GUI.DrawTexture(new Rect(r.x, r.y, 2f, r.height), Texture2D.whiteTexture);
            }

            // Thin border — cyan when selected, otherwise quiet metal/cyan dim
            Color border = selected
                ? new Color(CyanEdge.r, CyanEdge.g, CyanEdge.b, 0.55f + 0.2f * uiPulse)
                : new Color(CyanDim.r, CyanDim.g, CyanDim.b, 0.35f);
            GUI.color = border;
            DrawBorder(r, 1f);

            // Subtle inner fill
            var well = Inset(r, 3f);
            GUI.color = PanelInner;
            GUI.DrawTexture(well, Texture2D.whiteTexture);

            // Hazard lip (top) — thin amber industrial stripe
            GUI.color = new Color(Orange.r, Orange.g, Orange.b, selected ? 0.9f : 0.55f);
            GUI.DrawTexture(new Rect(well.x + 1f, well.y + 1f, well.width - 2f, 2f), Texture2D.whiteTexture);

            // Worker accent marker (right edge, quiet)
            GUI.color = new Color(workerAccent.r, workerAccent.g, workerAccent.b, 0.4f + 0.2f * uiPulse);
            GUI.DrawTexture(new Rect(well.xMax - 2f, well.y + 6f, 1.5f, well.height - 18f), Texture2D.whiteTexture);

            // Visor viewport
            float footerH = 11f;
            var viewport = new Rect(well.x + 3f, well.y + 5f, well.width - 6f, well.height - 8f - footerH);
            GUI.color = new Color(0.015f, 0.02f, 0.028f, 1f);
            GUI.DrawTexture(viewport, Texture2D.whiteTexture);

            // Soft mood wash — restrained, glass-behind only
            float washA = 0.1f + 0.08f * Mathf.Abs(mood.Score);
            if (selected) washA += 0.04f;
            GUI.color = new Color(mood.Tint.r, mood.Tint.g, mood.Tint.b, washA);
            GUI.DrawTexture(Inset(viewport, 1f), Texture2D.whiteTexture);

            bool exhausted = wr.State != null && (wr.State.MentalFatigue >= 70f || wr.State.IsResting);
            bool needsCare = wr.State != null && wr.State.NeedsCare;
            var tex = GetOrBuildFace(wr.WorkerId, mood.Band, needsCare, exhausted);
            if (tex != null)
            {
                GUI.color = Color.white;
                var faceRect = Inset(viewport, 2f);
                GUI.DrawTexture(faceRect, tex, ScaleMode.ScaleAndCrop);
            }

            // Cyan glass film
            GUI.color = new Color(0.2f, 0.65f, 0.78f, 0.045f);
            GUI.DrawTexture(viewport, Texture2D.whiteTexture);

            // Hairline scan (ambient, slow)
            float scanY = viewport.y + Mathf.Repeat(Time.unscaledTime * 11f + wr.WorkerId * 9.3f, viewport.height - 1f);
            GUI.color = new Color(CyanEdge.r, CyanEdge.g, CyanEdge.b, 0.055f + 0.025f * uiPulse);
            GUI.DrawTexture(new Rect(viewport.x + 1f, scanY, viewport.width - 2f, 1f), Texture2D.whiteTexture);

            // Faint grid lines (technical readout, not decorative noise)
            GUI.color = new Color(CyanDim.r, CyanDim.g, CyanDim.b, 0.04f);
            float midX = viewport.x + viewport.width * 0.5f;
            GUI.DrawTexture(new Rect(midX, viewport.y + 2f, 1f, viewport.height - 4f), Texture2D.whiteTexture);
            float midY = viewport.y + viewport.height * 0.42f;
            GUI.DrawTexture(new Rect(viewport.x + 2f, midY, viewport.width - 4f, 1f), Texture2D.whiteTexture);

            // Viewport frame
            GUI.color = new Color(Metal.r, Metal.g, Metal.b, 0.55f);
            DrawBorder(viewport, 1f);
            GUI.color = new Color(MetalHi.r, MetalHi.g, MetalHi.b, 0.22f);
            GUI.DrawTexture(new Rect(viewport.x + 1f, viewport.y + 1f, viewport.width - 2f, 1f), Texture2D.whiteTexture);

            // Corner brackets on outer housing
            DrawCornerBrackets(r, selected
                ? new Color(CyanEdge.r, CyanEdge.g, CyanEdge.b, 0.7f)
                : new Color(mood.Tint.r, mood.Tint.g, mood.Tint.b, 0.4f));

            // Footer strip — ID + mood LED
            var footer = new Rect(well.x + 2f, well.yMax - footerH - 1f, well.width - 4f, footerH);
            GUI.color = new Color(0.03f, 0.04f, 0.05f, 0.95f);
            GUI.DrawTexture(footer, Texture2D.whiteTexture);
            GUI.color = new Color(CyanDim.r, CyanDim.g, CyanDim.b, 0.25f);
            GUI.DrawTexture(new Rect(footer.x, footer.y, footer.width, 1f), Texture2D.whiteTexture);

            EnsureIdStyle();
            var prevContent = GUI.contentColor;
            GUI.contentColor = new Color(0.72f, 0.82f, 0.9f, 0.85f);
            GUI.Label(new Rect(footer.x + 3f, footer.y - 1f, footer.width - 14f, footer.height + 2f),
                $"ID-{wr.WorkerId:00}", _idStyle);
            GUI.contentColor = prevContent;

            // Mood status LED (bottom-right) — precise, not bloomy
            float led = 3f;
            var ledR = new Rect(footer.xMax - led - 3f, footer.y + (footer.height - led) * 0.5f, led, led);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(ledR.x - 1f, ledR.y - 1f, ledR.width + 2f, ledR.height + 2f), Texture2D.whiteTexture);
            GUI.color = new Color(mood.Tint.r, mood.Tint.g, mood.Tint.b, 0.7f + 0.25f * uiPulse);
            GUI.DrawTexture(ledR, Texture2D.whiteTexture);
            // Hairline glow ring
            GUI.color = new Color(mood.Tint.r, mood.Tint.g, mood.Tint.b, 0.2f);
            DrawBorder(new Rect(ledR.x - 1.5f, ledR.y - 1.5f, ledR.width + 3f, ledR.height + 3f), 1f);

            GUI.color = prev;
        }

        static void EnsureIdStyle()
        {
            if (_idStyle != null) return;
            _idStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 8,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0),
            };
            _idStyle.normal.textColor = Color.white;
        }

        static Rect Inset(Rect r, float t) =>
            new Rect(r.x + t, r.y + t, r.width - t * 2f, r.height - t * 2f);

        static void DrawBorder(Rect r, float t)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);
        }

        static void DrawCornerBrackets(Rect r, Color col)
        {
            GUI.color = col;
            const float L = 6f;
            const float T = 1.2f;
            // TL
            GUI.DrawTexture(new Rect(r.x, r.y, L, T), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, T, L), Texture2D.whiteTexture);
            // TR
            GUI.DrawTexture(new Rect(r.xMax - L, r.y, L, T), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - T, r.y, T, L), Texture2D.whiteTexture);
            // BL
            GUI.DrawTexture(new Rect(r.x, r.yMax - T, L, T), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - L, T, L), Texture2D.whiteTexture);
            // BR
            GUI.DrawTexture(new Rect(r.xMax - L, r.yMax - T, L, T), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - T, r.yMax - L, T, L), Texture2D.whiteTexture);
        }

        static Texture2D GetOrBuildFace(int workerId, MoodBand band, bool needsCare, bool exhausted)
        {
            var key = new CacheKey
            {
                WorkerId = workerId,
                Band = band,
                NeedsCare = needsCare,
                Exhausted = exhausted,
            };
            if (Cache.TryGetValue(key, out var tex) && tex != null)
                return tex;
            tex = BuildFace(workerId, band, needsCare, exhausted);
            Cache[key] = tex;
            return tex;
        }

        static Texture2D BuildFace(int workerId, MoodBand band, bool needsCare, bool exhausted)
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"FaceMon_w{workerId}_{band}",
            };

            int seed = workerId * 9176 + 13;
            float skinV = 0.38f + Hash01(seed, 1) * 0.32f;
            float skinR = skinV * (0.9f + Hash01(seed, 2) * 0.14f);
            float skinG = skinV * (0.7f + Hash01(seed, 3) * 0.12f);
            float skinB = skinV * (0.55f + Hash01(seed, 4) * 0.12f);
            var skin = new Color(skinR, skinG, skinB, 1f);
            var skinShade = Color.Lerp(skin, Charcoal, 0.4f);
            var skinHi = Color.Lerp(skin, MetalHi, 0.12f);
            var helmet = Color.Lerp(MetalDeep, Orange, 0.15f + Hash01(seed, 5) * 0.2f);
            bool beard = workerId == 3 || workerId == 5 || Hash01(seed, 6) > 0.72f;
            float browHeavy = 0.75f + Hash01(seed, 7) * 0.55f;
            float eyeOpen = band == MoodBand.Good ? 1f : band == MoodBand.Bad ? 0.7f : 0.88f;
            if (exhausted) eyeOpen *= 0.68f;
            if (needsCare) eyeOpen *= 0.85f;

            float mouthCurve = band switch
            {
                MoodBand.Good => 1.5f,
                MoodBand.Bad => -1.7f,
                _ => 0.05f,
            };
            if (needsCare) mouthCurve = Mathf.Min(mouthCurve, -0.7f);
            if (exhausted && band != MoodBand.Good) mouthCurve -= 0.45f;

            float browTilt = band switch
            {
                MoodBand.Good => -0.35f,
                MoodBand.Bad => 1.05f,
                _ => 0.12f,
            };

            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s;
                float v = (y + 0.5f) / s; // y=0 bottom → forehead at high v

                Color c = new Color(0, 0, 0, 0);

                // Angular helmet silhouette (squircle / chamfered oval — less soft blob)
                float dx = (u - 0.5f) / 0.40f;
                float dy = (v - 0.47f) / 0.48f;
                float faceR = dx * dx + dy * dy;
                // Mild chamfer: pull corners in
                float chamfer = Mathf.Max(Mathf.Abs(dx) + Mathf.Abs(dy) * 0.15f - 1.05f, 0f);
                faceR += chamfer * 0.35f;

                float hx = (u - 0.5f) / 0.46f;
                float hy = (v - 0.49f) / 0.52f;
                float helmR = hx * hx + hy * hy;

                if (helmR < 1.05f && faceR > 0.88f)
                {
                    float edge = Mathf.Clamp01((faceR - 0.88f) / 0.25f);
                    float grit = Hash01(x + seed, y) * 0.12f;
                    c = Color.Lerp(helmet, Metal, edge * 0.45f + grit);
                    // Top hazard band on helmet
                    if (v > 0.86f && faceR < 1.15f)
                        c = Color.Lerp(c, Orange, 0.55f);
                    // Side rivet dots
                    if (Mathf.Abs(Mathf.Abs(u - 0.5f) - 0.38f) < 0.02f && v > 0.35f && v < 0.7f
                        && Hash01(x * 3, y + seed) > 0.55f)
                        c = Color.Lerp(c, MetalHi, 0.35f);
                }
                else if (faceR <= 0.88f)
                {
                    // Soft lighting — top-left key
                    float key = Mathf.Clamp01(0.55f - dx * 0.2f + dy * 0.25f);
                    c = Color.Lerp(skinShade, skinHi, key * 0.55f + 0.2f);
                    c = Color.Lerp(c, skin, 0.35f);

                    // Controlled grease (sparse)
                    float smudge = Hash01(x + seed * 2, y + 9);
                    if (smudge > 0.88f)
                        c = Color.Lerp(c, Charcoal, 0.14f);

                    PlotEyes(ref c, u, v, eyeOpen, browTilt, browHeavy, band, seed);

                    // Nose bridge (subtle)
                    if (Mathf.Abs(u - 0.5f) < 0.025f && v > 0.44f && v < 0.56f)
                        c = Color.Lerp(c, skinShade, 0.28f);

                    PlotMouth(ref c, u, v, mouthCurve, band);

                    if (beard && v < 0.40f && v > 0.20f && Mathf.Abs(u - 0.5f) < 0.20f)
                    {
                        float b = Hash01(x + 4, y + seed);
                        if (b > 0.5f)
                            c = Color.Lerp(c, new Color(0.11f, 0.09f, 0.08f), 0.28f + b * 0.22f);
                    }

                    if (needsCare && v > 0.58f && v < 0.74f && Mathf.Abs(u - 0.5f) < 0.18f)
                        c = Color.Lerp(c, new Color(0.5f, 0.16f, 0.14f), 0.1f);

                    // Inner rim shadow at face edge
                    if (faceR > 0.72f)
                        c = Color.Lerp(c, MetalDeep, (faceR - 0.72f) / 0.16f * 0.45f);
                }
                else if (helmR < 1.2f)
                {
                    c = Color.Lerp(MetalDeep, Metal, Hash01(x, y + seed) * 0.3f);
                    if (v > 0.9f)
                        c = Color.Lerp(c, Orange, 0.4f);
                    c.a = Mathf.Clamp01(1.2f - helmR) ;
                }

                // Sparse specular on metal only
                if (c.a > 0.5f && faceR > 0.88f && Hash01(x + 70, y + seed) > 0.96f)
                    c = Color.Lerp(c, MetalHi, 0.4f);

                tex.SetPixel(x, y, c);
            }

            tex.Apply(false, true);
            return tex;
        }

        static void PlotEyes(
            ref Color c, float u, float v,
            float open, float browTilt, float browHeavy,
            MoodBand band, int seed)
        {
            PlotOneEye(ref c, u, v, 0.36f, open, browTilt, browHeavy, band, seed);
            PlotOneEye(ref c, u, v, 0.64f, open, browTilt, browHeavy, band, seed);
        }

        static void PlotOneEye(
            ref Color c, float u, float v, float cx,
            float open, float browTilt, float browHeavy,
            MoodBand band, int seed)
        {
            float eyeY = 0.63f;
            float ex = (u - cx) / 0.065f;
            float ey = (v - eyeY) / (0.04f * open);
            float er = ex * ex + ey * ey;
            if (er < 1f)
            {
                c = new Color(0.9f, 0.91f, 0.88f, 1f);
                float px = (u - cx) / 0.026f;
                float py = (v - eyeY) / 0.026f;
                if (px * px + py * py < 1f)
                {
                    float iris = band == MoodBand.Bad ? 0.22f : 0.32f;
                    float hue = Hash01(seed, 20);
                    c = new Color(iris + hue * 0.12f, iris * 0.9f, iris * 0.7f + hue * 0.08f, 1f);
                    if (px * px + py * py < 0.25f)
                        c = Charcoal;
                    // Specular catchlight
                    if ((u - (cx - 0.012f)) * (u - (cx - 0.012f)) + (v - (eyeY + 0.01f)) * (v - (eyeY + 0.01f)) < 0.00012f)
                        c = new Color(0.95f, 0.97f, 1f, 1f);
                }
            }

            float by = 0.71f + browTilt * 0.018f * Mathf.Sign(cx - 0.5f);
            if (Mathf.Abs(u - cx) < 0.075f * browHeavy
                && Mathf.Abs(v - by) < 0.015f * browHeavy)
                c = Color.Lerp(Charcoal, c, 0.12f);

            if (open < 0.8f && er < 1.15f && v > eyeY)
                c = Color.Lerp(c, Charcoal, (1f - open) * 0.4f);
        }

        static void PlotMouth(ref Color c, float u, float v, float curve, MoodBand band)
        {
            float my = 0.33f - curve * 0.011f * (1f - Mathf.Abs(u - 0.5f) * 4f);
            if (Mathf.Abs(u - 0.5f) < 0.1f && Mathf.Abs(v - my) < 0.014f)
            {
                Color lip = band == MoodBand.Bad
                    ? new Color(0.32f, 0.14f, 0.13f)
                    : new Color(0.36f, 0.2f, 0.18f);
                c = Color.Lerp(c, lip, 0.72f);
            }
        }

        static float Hash01(int x, int y)
        {
            int n = x * 374761393 + y * 668265263;
            n = (n ^ (n >> 13)) * 1274126177;
            n ^= n >> 16;
            return (n & 0x7fffffff) / (float)int.MaxValue;
        }
    }
}
