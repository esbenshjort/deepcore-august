using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Roster biometric face monitor — circular industrial hard-hat portrait.
    /// Mood ranks green (happy) → red (angry), matching reference art direction:
    /// orange weathered helmet, cyan headlamp, green antenna tip, blue visor,
    /// dark metal gauge frame, bottom mood LED strip with mini face.
    /// </summary>
    public static class WorkerFaceMonitor
    {
        public const float DefaultWidth = 72f;

        /// <summary>Six ranks — joyful → enraged (green → deep red).</summary>
        public enum MoodBand : byte
        {
            Joyful = 0,
            Content = 1,
            Neutral = 2,
            Worried = 3,
            Angry = 4,
            Enraged = 5,
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

        static readonly Dictionary<CacheKey, Texture2D> Cache = new(48);

        static readonly Color CyanLamp = new(0.45f, 0.95f, 1f);
        static readonly Color CyanCore = new(0.85f, 0.99f, 1f);
        static readonly Color GreenTip = new(0.4f, 1f, 0.5f);
        static readonly Color GreenCore = new(0.82f, 1f, 0.88f);
        static readonly Color OrangeHat = new(0.92f, 0.48f, 0.12f);
        static readonly Color OrangeDeep = new(0.62f, 0.28f, 0.08f);
        static readonly Color OrangeHi = new(1f, 0.62f, 0.28f);
        static readonly Color Metal = new(0.42f, 0.45f, 0.5f);
        static readonly Color MetalHi = new(0.68f, 0.72f, 0.78f);
        static readonly Color MetalDeep = new(0.1f, 0.11f, 0.13f);
        static readonly Color Charcoal = new(0.05f, 0.055f, 0.06f);
        static readonly Color VisorBlue = new(0.12f, 0.32f, 0.48f, 0.72f);
        static readonly Color Suit = new(0.22f, 0.2f, 0.18f);

        public static MoodSample Sample(WorkerState st)
        {
            if (st == null)
            {
                return new MoodSample
                {
                    Score = 0f,
                    Band = MoodBand.Neutral,
                    Tint = MoodTint(MoodBand.Neutral),
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

            MoodBand band = RankFromScore(score);
            if (st.NeedsCare && band < MoodBand.Worried)
                band = MoodBand.Worried;
            if ((st.MentalFatigue >= 70f || st.IsResting) && (int)band < (int)MoodBand.Angry)
                band = (MoodBand)Mathf.Min((int)band + 1, (int)MoodBand.Enraged);

            return new MoodSample
            {
                Score = score,
                Band = band,
                Tint = MoodTint(band),
            };
        }

        static MoodBand RankFromScore(float score)
        {
            if (score >= 0.55f) return MoodBand.Joyful;
            if (score >= 0.22f) return MoodBand.Content;
            if (score >= -0.12f) return MoodBand.Neutral;
            if (score >= -0.42f) return MoodBand.Worried;
            if (score >= -0.72f) return MoodBand.Angry;
            return MoodBand.Enraged;
        }

        public static Color MoodTint(float score01)
        {
            return MoodTint(RankFromScore(score01));
        }

        public static Color MoodTint(MoodBand band) => band switch
        {
            MoodBand.Joyful => new Color(0.28f, 0.95f, 0.42f, 1f),
            MoodBand.Content => new Color(0.55f, 0.92f, 0.28f, 1f),
            MoodBand.Neutral => new Color(0.95f, 0.82f, 0.22f, 1f),
            MoodBand.Worried => new Color(1f, 0.55f, 0.18f, 1f),
            MoodBand.Angry => new Color(0.95f, 0.22f, 0.18f, 1f),
            _ => new Color(0.72f, 0.08f, 0.1f, 1f),
        };

        /// <summary>Draws a circular industrial hard-hat biometric portrait.</summary>
        public static void Draw(Rect r, WorkerRuntime wr, Color workerAccent, float uiPulse = 0f,
            bool selected = false)
        {
            if (wr == null) return;
            _ = workerAccent; // identity accent kept for callers; ring is mood-driven
            var mood = Sample(wr.State);
            var prev = GUI.color;

            // Square portrait cell centered in roster slot
            float side = Mathf.Min(r.width, r.height);
            var cell = new Rect(
                r.x + (r.width - side) * 0.5f,
                r.y + (r.height - side) * 0.5f,
                side, side);

            bool exhausted = wr.State != null && (wr.State.MentalFatigue >= 70f || wr.State.IsResting);
            bool needsCare = wr.State != null && wr.State.NeedsCare;
            var tex = GetOrBuildFace(wr.WorkerId, mood.Band, needsCare, exhausted);
            if (tex != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(cell, tex, ScaleMode.ScaleToFit);
            }

            // Mood ring around portrait (green → red). Selection gets a cyan outer accent.
            Color ring = mood.Tint;
            float pulse = 0.55f + 0.2f * uiPulse;
            if (selected)
            {
                GUI.color = new Color(CyanLamp.r, CyanLamp.g, CyanLamp.b, 0.45f + 0.25f * uiPulse);
                DrawCircleRing(cell, 2.4f);
                GUI.color = new Color(ring.r, ring.g, ring.b, 0.7f + 0.2f * uiPulse);
                DrawCircleRing(Inset(cell, 2.5f), 2.1f);
            }
            else
            {
                GUI.color = new Color(ring.r, ring.g, ring.b, pulse);
                DrawCircleRing(cell, 2.0f);
            }

            GUI.color = prev;
        }

        static Rect Inset(Rect r, float t) =>
            new Rect(r.x + t, r.y + t, r.width - t * 2f, r.height - t * 2f);

        static void DrawCircleRing(Rect r, float thickness)
        {
            // Approximate ring with short segments (IMGUI has no native circle)
            int segs = 28;
            float cx = r.x + r.width * 0.5f;
            float cy = r.y + r.height * 0.5f;
            float rx = r.width * 0.5f - thickness * 0.5f;
            float ry = r.height * 0.5f - thickness * 0.5f;
            for (int i = 0; i < segs; i++)
            {
                float a0 = (i / (float)segs) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)segs) * Mathf.PI * 2f;
                float x0 = cx + Mathf.Cos(a0) * rx;
                float y0 = cy + Mathf.Sin(a0) * ry;
                float x1 = cx + Mathf.Cos(a1) * rx;
                float y1 = cy + Mathf.Sin(a1) * ry;
                float mx = (x0 + x1) * 0.5f;
                float my = (y0 + y1) * 0.5f;
                float len = Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)) + 0.5f;
                float ang = Mathf.Atan2(y1 - y0, x1 - x0) * Mathf.Rad2Deg;
                var matrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(ang, new Vector2(mx, my));
                GUI.DrawTexture(new Rect(mx - len * 0.5f, my - thickness * 0.5f, len, thickness),
                    Texture2D.whiteTexture);
                GUI.matrix = matrix;
            }
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
            const int s = 112;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"FaceMon_w{workerId}_{band}",
            };

            int seed = workerId * 9176 + 13;
            Color mood = MoodTint(band);
            float cx = (s - 1) * 0.5f, cy = (s - 1) * 0.5f;
            float R = s * 0.48f;

            // Skin variation per worker
            float skinV = 0.42f + Hash01(seed, 1) * 0.28f;
            var skin = new Color(
                skinV * (0.92f + Hash01(seed, 2) * 0.1f),
                skinV * (0.72f + Hash01(seed, 3) * 0.1f),
                skinV * (0.55f + Hash01(seed, 4) * 0.1f), 1f);
            var skinShade = Color.Lerp(skin, Charcoal, 0.35f);

            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = x - cx, dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                // Texture Y: 0 bottom → 1 top (SetPixel y increases up in Unity textures)
                float u = (x + 0.5f) / s;
                float v = (y + 0.5f) / s;

                if (dist > R + 0.8f)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                Color c;

                // ——— Outer gauge rim — mood LED ring (green → red) ———
                float rimOuter = R;
                float rimInner = R * 0.82f;
                if (dist > rimInner)
                {
                    float t = Mathf.InverseLerp(rimOuter, rimInner, dist);
                    float bevel = Mathf.Pow(Mathf.Clamp01(t), 0.65f);
                    c = Color.Lerp(MetalDeep, Metal, bevel);
                    // Specular arc (upper-left)
                    float spec = Mathf.Clamp01(Vector2.Dot(new Vector2(dx, dy).normalized,
                        new Vector2(-0.5f, 0.7f)));
                    if (spec > 0.55f)
                        c = Color.Lerp(c, MetalHi, (spec - 0.55f) * 1.4f);
                    // Weathering under the glow
                    if (Hash01(x + seed, y) > 0.88f)
                        c = Color.Lerp(c, OrangeDeep, 0.2f);

                    // Mood color dominates the ring — brighter toward the outer edge
                    float moodEdge = Mathf.SmoothStep(0.15f, 1f, t);
                    Color moodLit = Color.Lerp(mood, Color.white, 0.22f);
                    c = Color.Lerp(c, mood, 0.45f + moodEdge * 0.5f);
                    if (dist > rimOuter - 2.8f)
                        c = Color.Lerp(c, moodLit, 0.55f + 0.35f * Mathf.Clamp01((rimOuter - dist) / 2.8f));
                    // Soft inner mood wash on the metal bevel
                    if (dist < rimInner + 2.5f)
                        c = Color.Lerp(c, mood, 0.2f);

                    // Soft outer AA
                    if (dist > rimOuter - 1.2f)
                        c.a = Mathf.Clamp01((rimOuter + 0.5f - dist) / 1.2f);
                    tex.SetPixel(x, y, c);
                    continue;
                }

                // ——— Interior well ———
                c = new Color(0.04f, 0.045f, 0.055f, 1f);

                // Collar / suit (lower third inside rim)
                float faceCy = 0.48f;
                float suitTop = 0.28f;
                if (v < suitTop)
                {
                    float su = Mathf.Abs(u - 0.5f);
                    c = Color.Lerp(Suit, MetalDeep, su * 0.4f + Hash01(x, y + seed) * 0.15f);
                    // Strap
                    if (Mathf.Abs(u - 0.5f) < 0.04f && v < 0.26f)
                        c = Color.Lerp(c, Metal, 0.45f);
                }

                // Head / neck skin oval
                float hx = (u - 0.5f) / 0.28f;
                float hy = (v - faceCy) / 0.34f;
                float headR = hx * hx + hy * hy;
                if (headR < 1f && v > 0.18f)
                {
                    float key = Mathf.Clamp01(0.55f - hx * 0.18f + hy * 0.22f);
                    c = Color.Lerp(skinShade, skin, key);
                    if (Hash01(x + seed * 2, y) > 0.9f)
                        c = Color.Lerp(c, Charcoal, 0.12f);
                }

                // ——— Orange hard-hat dome ———
                float hatCx = 0.5f, hatCy = 0.62f;
                float hatRx = 0.36f, hatRy = 0.28f;
                float hdx = (u - hatCx) / hatRx;
                float hdy = (v - hatCy) / hatRy;
                float hatD = hdx * hdx + hdy * hdy;
                if (hatD < 1f && v > 0.42f)
                {
                    float nd = Mathf.Sqrt(Mathf.Max(0f, hatD));
                    float lit = Mathf.Clamp01(0.35f + (-hdx * 0.3f + hdy * 0.55f));
                    c = Color.Lerp(OrangeDeep, OrangeHat, lit);
                    if (lit > 0.7f) c = Color.Lerp(c, OrangeHi, (lit - 0.7f) * 1.2f);
                    // Scratches / weathering
                    float grit = Hash01(x * 3 + seed, y * 2);
                    if (grit > 0.82f) c = Color.Lerp(c, OrangeDeep, 0.4f);
                    if (grit < 0.12f) c = Color.Lerp(c, Charcoal, 0.2f);
                    // Helmet ridge
                    if (Mathf.Abs(u - 0.5f) < 0.035f && v > 0.5f && v < 0.78f)
                        c = Color.Lerp(c, OrangeDeep, 0.35f);
                    // Rim edge
                    if (nd > 0.88f) c = Color.Lerp(c, Charcoal, (nd - 0.88f) / 0.12f);
                }

                // Brim (horizontal bar under dome)
                if (v > 0.44f && v < 0.50f && Mathf.Abs(u - 0.5f) < 0.34f)
                {
                    c = Color.Lerp(OrangeDeep, OrangeHat, 0.45f);
                    if (Hash01(x, y + seed) > 0.85f)
                        c = Color.Lerp(c, Charcoal, 0.3f);
                }

                // ——— Cyan headlamp ———
                float lx = (u - 0.5f) / 0.055f;
                float ly = (v - 0.78f) / 0.05f;
                float lampD = lx * lx + ly * ly;
                if (lampD < 1.3f)
                {
                    c = Color.Lerp(Charcoal, CyanLamp, Mathf.Clamp01(1.1f - lampD));
                    if (lampD < 0.35f) c = CyanCore;
                    // Soft bloom around lamp
                    if (lampD > 0.9f && lampD < 1.3f)
                        c = Color.Lerp(c, CyanLamp, 0.35f);
                }

                // ——— Antenna (right) with green tip ———
                if (u > 0.72f && u < 0.82f && v > 0.55f && v < 0.82f)
                {
                    if (Mathf.Abs(u - 0.76f) < 0.018f)
                        c = Color.Lerp(MetalDeep, Metal, 0.5f);
                }
                float ax = (u - 0.76f) / 0.035f;
                float ay = (v - 0.84f) / 0.035f;
                if (ax * ax + ay * ay < 1f)
                {
                    c = Color.Lerp(GreenTip, GreenCore, 1f - (ax * ax + ay * ay));
                }

                // ——— Blue visor glass over upper face ———
                float vx = (u - 0.5f) / 0.26f;
                float vy = (v - 0.55f) / 0.14f;
                float visorD = vx * vx + vy * vy;
                bool inVisor = visorD < 1f && v > 0.42f && v < 0.68f;
                if (inVisor && headR < 1.05f)
                {
                    // Keep skin under, then tint with glass
                    Color under = c;
                    c = Color.Lerp(under, VisorBlue, 0.55f);
                    // Glass highlight streak
                    if (Mathf.Abs(u - 0.38f) < 0.04f && v > 0.52f && v < 0.62f)
                        c = Color.Lerp(c, CyanCore, 0.25f);
                }

                // ——— Facial expression (under / through visor) ———
                if (headR < 0.95f && v > 0.32f && v < 0.68f)
                    PlotExpression(ref c, u, v, band, inVisor, exhausted || needsCare);

                // ——— Mood LED strip at bottom of circle ———
                float stripY0 = 0.10f, stripY1 = 0.22f;
                float stripHalfW = 0.22f;
                if (v > stripY0 && v < stripY1 && Mathf.Abs(u - 0.5f) < stripHalfW)
                {
                    float edgeU = Mathf.Abs(u - 0.5f) / stripHalfW;
                    float edgeV = Mathf.Min(
                        Mathf.InverseLerp(stripY0, stripY0 + 0.02f, v),
                        Mathf.InverseLerp(stripY1, stripY1 - 0.02f, v));
                    Color bar = Color.Lerp(Color.Lerp(mood, Charcoal, 0.35f), mood, 0.85f);
                    c = Color.Lerp(Charcoal, bar, 0.9f);
                    if (edgeU > 0.92f || edgeV < 0.35f)
                        c = Color.Lerp(c, Charcoal, 0.55f);
                    // Side ticks
                    if ((Mathf.Abs(u - 0.36f) < 0.025f || Mathf.Abs(u - 0.64f) < 0.025f)
                        && v > 0.14f && v < 0.18f)
                        c = Color.Lerp(mood, CyanCore, 0.4f);
                    // Mini face glyph in center of strip
                    PlotMoodGlyph(ref c, u, v, 0.5f, 0.16f, band, mood);
                }

                // Enraged: red sparks around helmet
                if (band == MoodBand.Enraged && dist < rimInner - 2f)
                {
                    float spark = Hash01(x * 7 + seed, y * 11);
                    if (spark > 0.965f && v > 0.45f)
                        c = Color.Lerp(c, new Color(1f, 0.35f, 0.15f), 0.85f);
                }

                // Inner rim shadow
                if (dist > rimInner - 3f && dist <= rimInner)
                    c = Color.Lerp(c, MetalDeep, (dist - (rimInner - 3f)) / 3f * 0.55f);

                tex.SetPixel(x, y, c);
            }

            tex.Apply(false, true);
            return tex;
        }

        static void PlotExpression(ref Color c, float u, float v, MoodBand band, bool underVisor,
            bool strained)
        {
            // Eyes
            float eyeY = 0.56f;
            float open = band switch
            {
                MoodBand.Joyful => 0f,      // closed happy
                MoodBand.Content => 0f,
                MoodBand.Neutral => 1f,
                MoodBand.Worried => 1.1f,
                MoodBand.Angry => 0.85f,
                _ => 0f,                   // squeezed shut scream
            };
            if (strained && open > 0f) open *= 0.85f;

            PlotEye(ref c, u, v, 0.38f, eyeY, open, band, left: true);
            PlotEye(ref c, u, v, 0.62f, eyeY, open, band, left: false);

            // Brows
            float browY = 0.62f;
            float browTilt = band switch
            {
                MoodBand.Joyful => -0.4f,
                MoodBand.Content => -0.25f,
                MoodBand.Neutral => 0f,
                MoodBand.Worried => 0.55f,   // inner up
                MoodBand.Angry => -0.85f,    // angry V
                _ => -1.0f,
            };
            PlotBrow(ref c, u, v, 0.38f, browY, browTilt, left: true);
            PlotBrow(ref c, u, v, 0.62f, browY, browTilt, left: false);

            // Mouth
            PlotMouth(ref c, u, v, band);
        }

        static void PlotEye(ref Color c, float u, float v, float cx, float cy, float open,
            MoodBand band, bool left)
        {
            if (band is MoodBand.Joyful or MoodBand.Content)
            {
                // Closed happy curve
                float my = cy - 0.01f + Mathf.Abs(u - cx) * 0.35f;
                if (Mathf.Abs(u - cx) < 0.055f && Mathf.Abs(v - my) < 0.012f)
                    c = Color.Lerp(c, Charcoal, 0.85f);
                return;
            }

            if (band == MoodBand.Enraged)
            {
                // >< squeezed
                float dx = u - cx;
                float line = left ? cy + dx * 1.2f : cy - dx * 1.2f;
                if (Mathf.Abs(u - cx) < 0.05f && Mathf.Abs(v - line) < 0.014f)
                    c = Color.Lerp(c, Charcoal, 0.9f);
                float line2 = left ? cy - dx * 1.2f : cy + dx * 1.2f;
                if (Mathf.Abs(u - cx) < 0.05f && Mathf.Abs(v - line2) < 0.014f)
                    c = Color.Lerp(c, Charcoal, 0.9f);
                return;
            }

            float ex = (u - cx) / 0.048f;
            float ey = (v - cy) / (0.032f * Mathf.Max(0.35f, open));
            float er = ex * ex + ey * ey;
            if (er < 1f)
            {
                c = new Color(0.92f, 0.93f, 0.9f, 1f);
                float px = (u - cx) / 0.02f;
                float py = (v - cy) / 0.02f;
                if (px * px + py * py < 1f)
                {
                    c = band == MoodBand.Angry
                        ? new Color(0.2f, 0.12f, 0.1f)
                        : new Color(0.18f, 0.2f, 0.22f);
                    if (px * px + py * py < 0.28f)
                        c = Charcoal;
                }
            }
        }

        static void PlotBrow(ref Color c, float u, float v, float cx, float cy, float tilt, bool left)
        {
            float sign = left ? -1f : 1f;
            float by = cy + tilt * 0.02f * sign * (u - cx) / 0.06f;
            if (Mathf.Abs(u - cx) < 0.07f && Mathf.Abs(v - by) < 0.013f)
                c = Color.Lerp(c, Charcoal, 0.88f);
        }

        static void PlotMouth(ref Color c, float u, float v, MoodBand band)
        {
            float mx0 = 0.5f;
            float my = 0.40f;
            Color lip = new(0.28f, 0.14f, 0.12f);

            switch (band)
            {
                case MoodBand.Joyful:
                {
                    // Wide open laugh
                    float dx = (u - mx0) / 0.11f;
                    float dy = (v - (my - 0.01f)) / 0.055f;
                    if (dx * dx + dy * dy < 1f && dy > -0.2f)
                    {
                        c = Color.Lerp(c, lip, 0.9f);
                        if (dy > 0.15f && Mathf.Abs(dx) < 0.7f)
                            c = Color.Lerp(c, new Color(0.45f, 0.18f, 0.16f), 0.7f);
                    }
                    break;
                }
                case MoodBand.Content:
                {
                    float curve = my - 0.02f + Mathf.Abs(u - mx0) * 0.12f;
                    if (Mathf.Abs(u - mx0) < 0.09f && Mathf.Abs(v - curve) < 0.012f)
                        c = Color.Lerp(c, lip, 0.85f);
                    break;
                }
                case MoodBand.Neutral:
                {
                    if (Mathf.Abs(u - mx0) < 0.08f && Mathf.Abs(v - my) < 0.01f)
                        c = Color.Lerp(c, lip, 0.8f);
                    break;
                }
                case MoodBand.Worried:
                {
                    float dx = (u - mx0) / 0.06f;
                    float dy = (v - my) / 0.045f;
                    if (dx * dx + dy * dy < 1f)
                        c = Color.Lerp(c, lip, 0.75f);
                    break;
                }
                case MoodBand.Angry:
                {
                    float curve = my + 0.015f - Mathf.Abs(u - mx0) * 0.1f;
                    if (Mathf.Abs(u - mx0) < 0.085f && Mathf.Abs(v - curve) < 0.012f)
                        c = Color.Lerp(c, lip, 0.88f);
                    break;
                }
                default: // Enraged scream
                {
                    float dx = (u - mx0) / 0.12f;
                    float dy = (v - (my - 0.02f)) / 0.07f;
                    if (dx * dx + dy * dy < 1f && dy > -0.35f)
                    {
                        c = Color.Lerp(c, lip, 0.92f);
                        // Teeth
                        if (dy > -0.05f && dy < 0.25f && Mathf.Abs(dx) < 0.75f
                            && ((int)((u - 0.4f) * 40f) & 1) == 0)
                            c = Color.Lerp(c, new Color(0.9f, 0.88f, 0.82f), 0.7f);
                    }
                    break;
                }
            }
        }

        static void PlotMoodGlyph(ref Color c, float u, float v, float cx, float cy,
            MoodBand band, Color mood)
        {
            float dx = (u - cx) / 0.045f;
            float dy = (v - cy) / 0.035f;
            if (dx * dx + dy * dy > 1f) return;

            // Mini face on LED — white/yellow glyph on mood color
            Color ink = Color.Lerp(Color.white, mood, 0.15f);
            // Eyes
            if ((Mathf.Abs(u - (cx - 0.015f)) < 0.006f || Mathf.Abs(u - (cx + 0.015f)) < 0.006f)
                && Mathf.Abs(v - (cy + 0.008f)) < 0.006f)
                c = ink;
            // Mouth shape by band
            float mouthY = cy - 0.008f;
            switch (band)
            {
                case MoodBand.Joyful:
                case MoodBand.Content:
                    if (Mathf.Abs(u - cx) < 0.02f
                        && Mathf.Abs(v - (mouthY - Mathf.Abs(u - cx) * 0.4f)) < 0.005f)
                        c = ink;
                    break;
                case MoodBand.Neutral:
                    if (Mathf.Abs(u - cx) < 0.018f && Mathf.Abs(v - mouthY) < 0.004f)
                        c = ink;
                    break;
                default:
                    if (Mathf.Abs(u - cx) < 0.02f
                        && Mathf.Abs(v - (mouthY + Mathf.Abs(u - cx) * 0.35f)) < 0.005f)
                        c = ink;
                    break;
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
