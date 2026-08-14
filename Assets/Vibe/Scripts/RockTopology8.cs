using UnityEngine;

namespace DeepCore.Vibe
{
    /// <summary>
    /// 8-neighbour rock topology → one of 8 base sprites + rotation/mirror.
    /// </summary>
    public static class RockTopology8
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

        public readonly struct Result
        {
            public readonly Kind Kind;
            public readonly float ZRotation;
            public readonly bool FlipX;

            public Result(Kind kind, float zRotation, bool flipX = false)
            {
                Kind = kind;
                ZRotation = zRotation;
                FlipX = flipX;
            }
        }

        /// <summary>
        /// Canonical sprites: Edge opens SOUTH, Outer/Diagonal open SE,
        /// Inner notch SE, Tip points SOUTH, T stem SOUTH.
        /// </summary>
        public static Result Resolve(
            bool n, bool ne, bool e, bool se, bool s, bool sw, bool w, bool nw)
        {
            bool oN = !n, oE = !e, oS = !s, oW = !w;
            int open = (oN ? 1 : 0) + (oE ? 1 : 0) + (oS ? 1 : 0) + (oW ? 1 : 0);

            if (open == 0)
            {
                if (!se) return new Result(Kind.InnerCorner, 0f);
                if (!ne) return new Result(Kind.InnerCorner, 90f);
                if (!nw) return new Result(Kind.InnerCorner, 180f);
                if (!sw) return new Result(Kind.InnerCorner, -90f);
                return new Result(Kind.Full, 0f);
            }

            if (open == 1)
            {
                if (oS) return new Result(Kind.Edge, 0f);
                if (oE) return new Result(Kind.Edge, 90f);
                if (oN) return new Result(Kind.Edge, 180f);
                return new Result(Kind.Edge, -90f);
            }

            if (open == 2)
            {
                if (oN && oS) return new Result(Kind.Edge, 90f);
                if (oE && oW) return new Result(Kind.Edge, 0f);

                // Adjacent opens:
                // OuterCorner = convex cave corner (far diagonal is floor)
                // Diagonal   = bevel where far diagonal stays rock
                bool farDiagRock =
                    (oS && oE && se) ||
                    (oE && oN && ne) ||
                    (oN && oW && nw) ||
                    (oW && oS && sw);

                if (oS && oE) return new Result(farDiagRock ? Kind.Diagonal : Kind.OuterCorner, 0f);
                if (oE && oN) return new Result(farDiagRock ? Kind.Diagonal : Kind.OuterCorner, 90f);
                if (oN && oW) return new Result(farDiagRock ? Kind.Diagonal : Kind.OuterCorner, 180f);
                if (oW && oS) return new Result(farDiagRock ? Kind.Diagonal : Kind.OuterCorner, -90f);
            }

            if (open == 3)
            {
                if (n && !e && !s && !w) return new Result(Kind.Tip, 0f);
                if (e && !n && !s && !w) return new Result(Kind.Tip, -90f);
                if (s && !n && !e && !w) return new Result(Kind.Tip, 180f);
                if (w && !n && !e && !s) return new Result(Kind.Tip, 90f);
                if (!oN) return new Result(Kind.Tip, 0f);
                if (!oE) return new Result(Kind.Tip, -90f);
                if (!oS) return new Result(Kind.Tip, 180f);
                return new Result(Kind.Tip, 90f);
            }

            // 4 cardinal opens
            int diagRock = (ne ? 1 : 0) + (se ? 1 : 0) + (sw ? 1 : 0) + (nw ? 1 : 0);
            if (diagRock >= 3) return new Result(Kind.Cross, 0f);
            if (diagRock == 2)
            {
                if (ne && se) return new Result(Kind.TJunction, 90f);
                if (se && sw) return new Result(Kind.TJunction, 180f);
                if (sw && nw) return new Result(Kind.TJunction, -90f);
                if (nw && ne) return new Result(Kind.TJunction, 0f);
                return new Result(Kind.Cross, 0f);
            }
            if (diagRock == 1)
            {
                if (ne) return new Result(Kind.TJunction, 90f);
                if (se) return new Result(Kind.TJunction, 180f);
                if (sw) return new Result(Kind.TJunction, -90f);
                return new Result(Kind.TJunction, 0f);
            }
            return new Result(Kind.Cross, 0f);
        }

        public static readonly Color[] DebugColors =
        {
            new(0.25f, 0.25f, 0.28f),
            new(0.75f, 0.45f, 0.15f),
            new(0.9f, 0.35f, 0.15f),
            new(0.2f, 0.55f, 0.85f),
            new(0.3f, 0.8f, 0.35f),
            new(0.85f, 0.3f, 0.85f),
            new(0.9f, 0.85f, 0.2f),
            new(0.95f, 0.95f, 0.95f),
        };

        public static string Label(Kind k) => k.ToString();
    }
}
