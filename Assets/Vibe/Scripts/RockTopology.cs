using UnityEngine;

namespace DeepCore.Vibe
{
    /// <summary>
    /// Rock wall autotile based on OPEN sides (floor), not "count of rock neighbours".
    /// That was the bug: straight cliffs were classified as InnerCorner/Tip and all faced the same way.
    /// </summary>
    public static class RockTopology
    {
        public enum Kind : byte
        {
            Full = 0,
            Edge = 1,         // exactly one side open → cliff face
            OuterCorner = 2,  // two adjacent sides open → convex rock corner
            InnerCorner = 3,  // cardinals solid, diagonal open → concave notch
            Tip = 4,          // three sides open → peninsula
        }

        public readonly struct Result
        {
            public readonly Kind Kind;
            public readonly float ZRotation;
            public readonly int OpenMask; // N=1 E=2 S=4 W=8 where neighbor is FLOOR/open

            public Result(Kind kind, float zRotation, int openMask)
            {
                Kind = kind;
                ZRotation = zRotation;
                OpenMask = openMask;
            }
        }

        /// <summary>
        /// Canonical art orientations (sprites authored once, then rotated):
        /// Edge:        open SOUTH (rock fills north)
        /// OuterCorner: open SOUTH+EAST (rock fills NW)
        /// InnerCorner: notch toward SOUTH-EAST
        /// Tip:         points SOUTH
        /// </summary>
        public static Result Resolve(
            bool rockN, bool rockE, bool rockS, bool rockW,
            bool rockNE, bool rockSE, bool rockSW, bool rockNW)
        {
            bool openN = !rockN;
            bool openE = !rockE;
            bool openS = !rockS;
            bool openW = !rockW;

            int openMask = 0;
            if (openN) openMask |= 1;
            if (openE) openMask |= 2;
            if (openS) openMask |= 4;
            if (openW) openMask |= 8;

            int openCount = CountBits(openMask);

            // --- Straight cliff: exactly one open cardinal -----------------
            if (openCount == 1)
            {
                if (openS) return new Result(Kind.Edge, 0f, openMask);
                if (openE) return new Result(Kind.Edge, 90f, openMask);
                if (openN) return new Result(Kind.Edge, 180f, openMask);
                /* openW */ return new Result(Kind.Edge, -90f, openMask);
            }

            // --- Convex corner: two adjacent opens ------------------------
            if (openCount == 2)
            {
                // Opposite opens = thin rock strip — keep solid mass for now
                if (openMask == 5 || openMask == 10)
                    return new Result(Kind.Full, 0f, openMask);

                // Adjacent opens → outer corner (rock sits opposite the opens)
                if (openS && openE) return new Result(Kind.OuterCorner, 0f, openMask);
                if (openE && openN) return new Result(Kind.OuterCorner, 90f, openMask);
                if (openN && openW) return new Result(Kind.OuterCorner, 180f, openMask);
                if (openW && openS) return new Result(Kind.OuterCorner, -90f, openMask);
            }

            // --- Tip / peninsula: three opens -----------------------------
            if (openCount == 3)
            {
                // Connected on the one rock side; tip points into the opens.
                if (!openN) return new Result(Kind.Tip, 0f, openMask);   // rock N → tip S
                if (!openE) return new Result(Kind.Tip, -90f, openMask); // rock E → tip W
                if (!openS) return new Result(Kind.Tip, 180f, openMask); // rock S → tip N
                /* !openW */ return new Result(Kind.Tip, 90f, openMask); // rock W → tip E
            }

            if (openCount == 4)
                return new Result(Kind.Tip, 0f, openMask);

            // --- No cardinal opens: full, or inner notch via diagonal -----
            // Concave corner lives on a "full" cell whose diagonal is excavated.
            if (!rockSE) return new Result(Kind.InnerCorner, 0f, openMask);
            if (!rockNE) return new Result(Kind.InnerCorner, 90f, openMask);
            if (!rockNW) return new Result(Kind.InnerCorner, 180f, openMask);
            if (!rockSW) return new Result(Kind.InnerCorner, -90f, openMask);

            return new Result(Kind.Full, 0f, openMask);
        }

        static int CountBits(int m)
        {
            int c = 0;
            while (m != 0) { c += m & 1; m >>= 1; }
            return c;
        }
    }
}
