using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.Vibe
{
    /// <summary>
    /// Seamless rock silhouettes: each rock cell is a square canvas, but alpha comes from
    /// a continuous 4-corner field shared with neighbours — so tile borders disappear.
    /// </summary>
    public static class SeamlessRockSprites
    {
        const int Size = 48;

        public static Sprite Build(float sw, float se, float nw, float ne, int salt = 0)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "RockTopo"
            };

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = x / (Size - 1f);
                float v = y / (Size - 1f);

                // Bilinear field from shared dual-grid corners (guarantees edge continuity)
                float bottom = Mathf.Lerp(sw, se, u);
                float top = Mathf.Lerp(nw, ne, u);
                float field = Mathf.Lerp(bottom, top, v);

                // Slight geological noise that fades near edges so seams stay stable
                float edgeDist = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                float noiseAmp = Mathf.SmoothStep(0f, 0.15f, edgeDist) * 0.06f;
                float n = Hash(x + salt * 13, y + salt * 7);
                field += (n - 0.5f) * noiseAmp;

                float a = Mathf.SmoothStep(0.42f, 0.58f, field);
                if (a <= 0.001f)
                {
                    tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                    continue;
                }

                float shade = 0.06f + n * 0.07f;
                // Soft lit lip where field crosses the threshold (the organic edge)
                float lip = 1f - Mathf.Abs(field - 0.5f) * 2f;
                lip = Mathf.Clamp01(lip);
                shade += lip * 0.12f;
                if (((x * 3 + y * 5 + salt) % 23) == 0) shade *= 0.55f;

                tex.SetPixel(x, y, new Color(shade * 1.05f, shade, shade * 0.92f, a));
            }

            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }

        static float Hash(int x, int y)
        {
            int n = x * 374761393 + y * 668265263;
            n = (n ^ (n >> 13)) * 1274126177;
            return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
        }
    }

    /// <summary>
    /// Classifies 8-neighbour patterns into the 8 topology kinds (for debug / art mapping).
    /// Visuals use SeamlessRockSprites; this labels what a painted tileset would pick.
    /// </summary>
    public static class Topology8
    {
        public enum Kind : byte
        {
            Full = 0,
            Edge = 1,
            OuterCorner = 2,
            InnerCorner = 3,
            Diagonal = 4,
            Tip = 5,
            TJunction = 6,
            Cross = 7,
        }

        public static Kind Classify(bool n, bool ne, bool e, bool se, bool s, bool sw, bool w, bool nw)
        {
            int card = (n ? 1 : 0) + (e ? 1 : 0) + (s ? 1 : 0) + (w ? 1 : 0);
            int openCard = 4 - card;

            if (openCard == 0)
            {
                // Concave notch if a diagonal is excavated
                if (!ne || !se || !sw || !nw) return Kind.InnerCorner;
                return Kind.Full;
            }

            if (openCard == 1) return Kind.Edge;

            if (openCard == 2)
            {
                bool opp = (n && s && !e && !w) || (e && w && !n && !s);
                if (opp) return Kind.Edge;
                // two adjacent opens
                bool adj =
                    (!n && !e && s && w) || (!e && !s && n && w) ||
                    (!s && !w && n && e) || (!w && !n && e && s) ||
                    (!n && !w) || (!n && !e) || (!s && !e) || (!s && !w);
                // simpler: if the two opens are adjacent cardinals
                if ((!n && !e && (s || w)) || (!e && !s && (n || w)) ||
                    (!s && !w && (n || e)) || (!w && !n && (e || s)))
                {
                    // Check diagonal between the two opens for diagonal tile
                    if ((!n && !e && !ne) || (!e && !s && !se) || (!s && !w && !sw) || (!w && !n && !nw))
                        return Kind.Diagonal;
                    return Kind.OuterCorner;
                }
                return Kind.OuterCorner;
            }

            if (openCard == 3) return Kind.Tip;

            // 4 open cardinals
            if (card == 0)
            {
                int diag = (ne ? 1 : 0) + (se ? 1 : 0) + (sw ? 1 : 0) + (nw ? 1 : 0);
                if (diag >= 3) return Kind.Cross;
                if (diag == 2) return Kind.TJunction;
                return Kind.Tip;
            }

            return Kind.Edge;
        }

        public static readonly Color[] DebugColors =
        {
            new(0.25f, 0.25f, 0.28f), // Full
            new(0.55f, 0.4f, 0.2f),   // Edge
            new(0.8f, 0.35f, 0.2f),   // Outer
            new(0.25f, 0.55f, 0.75f), // Inner
            new(0.45f, 0.7f, 0.35f),  // Diagonal
            new(0.75f, 0.3f, 0.75f),  // Tip
            new(0.7f, 0.7f, 0.25f),   // T
            new(0.9f, 0.9f, 0.9f),    // Cross
        };
    }
}
