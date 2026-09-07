using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Mission 01 geology overlay on the SocketMap organic mountain.
    /// Deep Gold + Diamond chambers with ≥2 soft (no-bedrock / no-gas) approaches,
    /// plus tempting wrong turns into bedrock or gas. Prospecting stays interpretive.
    /// </summary>
    public static class Mission01Geology
    {
        public const string MissionId = "MISSION 01";
        public const string MissionTitle = "MISSION 01 — 14 DAY PROSPECTING TEST";
        public const int MissionDays = 14;

        public struct Layout
        {
            public RectInt GoldChamber;
            public RectInt DiamondChamber;
            public Vector2Int Spawn;
            public Vector2Int GoldCenter;
            public Vector2Int DiamondCenter;
            public Vector2Int DirectBedrockPlug;
            public Vector2Int GoldTemptGas;
            public Vector2Int DiamondTemptGas;
            public Vector2Int GoldTemptBedrock;
            public Vector2Int DiamondTemptBedrock;
            public Vector2Int JunctionWest;
            public Vector2Int JunctionEast;
            public Vector2Int JunctionMid;
        }

        public sealed class ValidationResult
        {
            public bool Passed;
            public Layout Layout;
            public int GoldCells;
            public int DiamondCells;
            public int BedrockCells;
            public int GasCells;
            public int SoftReachableCells;
            public int GoldSoftDist = -1;
            public int DiamondSoftDist = -1;
            public int GoldApproaches;
            public int DiamondApproaches;
            public bool GoldReachableSoft;
            public bool DiamondReachableSoft;
            public bool DirectNorthBlockedByHazard;
            public bool GoldTemptBlocked;
            public bool DiamondTemptBlocked;
            public bool TrivialFromSpawn;
            public List<Vector2Int> GoldPathA = new();
            public List<Vector2Int> GoldPathB = new();
            public List<Vector2Int> DiamondPathA = new();
            public List<Vector2Int> DiamondPathB = new();
            public readonly List<string> Notes = new();
            public readonly List<string> Failures = new();

            public string ToReport()
            {
                var sb = new StringBuilder(2048);
                sb.AppendLine("# Mission 01 — Map Validation");
                sb.AppendLine();
                sb.AppendLine($"**Result:** {(Passed ? "PASS" : "FAIL")}");
                sb.AppendLine($"**Title:** {MissionTitle}");
                sb.AppendLine();
                sb.AppendLine("## Counts");
                sb.AppendLine($"- Gold cells: {GoldCells}");
                sb.AppendLine($"- Diamond cells: {DiamondCells}");
                sb.AppendLine($"- Diggable bedrock cells (≥2 sockets): {BedrockCells}");
                sb.AppendLine($"- Gas cells: {GasCells}");
                sb.AppendLine($"- Soft-reachable cells from spawn: {SoftReachableCells}");
                sb.AppendLine();
                sb.AppendLine("## Targets");
                sb.AppendLine($"- Gold chamber: {Layout.GoldChamber} center {Layout.GoldCenter} softDist={GoldSoftDist}");
                sb.AppendLine($"- Diamond chamber: {Layout.DiamondChamber} center {Layout.DiamondCenter} softDist={DiamondSoftDist}");
                sb.AppendLine($"- Gold soft approaches: {GoldApproaches} (need ≥2)");
                sb.AppendLine($"- Diamond soft approaches: {DiamondApproaches} (need ≥2)");
                sb.AppendLine();
                sb.AppendLine("## Hazards / Wrong turns");
                sb.AppendLine($"- Direct north blocked by hazard: {DirectNorthBlockedByHazard}");
                sb.AppendLine($"- Gold tempting approach blocked: {GoldTemptBlocked}");
                sb.AppendLine($"- Diamond tempting approach blocked: {DiamondTemptBlocked}");
                sb.AppendLine($"- Trivial from spawn (softDist < 70): {TrivialFromSpawn}");
                sb.AppendLine();
                if (Notes.Count > 0)
                {
                    sb.AppendLine("## Notes");
                    foreach (var n in Notes) sb.AppendLine($"- {n}");
                    sb.AppendLine();
                }
                if (Failures.Count > 0)
                {
                    sb.AppendLine("## Failures");
                    foreach (var f in Failures) sb.AppendLine($"- {f}");
                    sb.AppendLine();
                }
                sb.AppendLine("## Design intent");
                sb.AppendLine("- Organic SocketMap style retained (bedrock lobes, early teases, gas field).");
                sb.AppendLine("- Mission overlay carves soft trunks + junctions; plugs tempting shortcuts.");
                sb.AppendLine("- Prospecting must interpret mineral / hazard clues — safe path is not labeled.");
                sb.AppendLine("- Win = refine ≥1 GOLD and ≥1 DIA within 14 shift days.");
                return sb.ToString();
            }
        }

        public static Layout Apply(FineTerrainWorld w, int sx, int sy)
        {
            var layout = MakeLayout(sx, sy);

            SoftCorridor(w, sx, sy + 6, layout.JunctionWest.x, layout.JunctionWest.y, 3);
            SoftCorridor(w, sx, sy + 6, layout.JunctionEast.x, layout.JunctionEast.y, 3);

            SoftCorridor(w, layout.JunctionWest.x, layout.JunctionWest.y,
                layout.DiamondChamber.xMin - 2, layout.DiamondCenter.y, 3);
            SoftCorridor(w, layout.JunctionWest.x - 8, layout.JunctionWest.y + 30,
                layout.DiamondChamber.xMin - 1, layout.DiamondCenter.y - 4, 2);

            SoftCorridor(w, layout.JunctionEast.x, layout.JunctionEast.y,
                layout.GoldChamber.xMax + 2, layout.GoldCenter.y, 3);
            SoftCorridor(w, layout.JunctionEast.x + 8, layout.JunctionEast.y + 30,
                layout.GoldChamber.xMax + 1, layout.GoldCenter.y - 4, 2);

            GoldVeinPlacer.FillSoftRockRect(w, layout.JunctionWest.x - 5, layout.JunctionWest.y - 5, 11, 11);
            GoldVeinPlacer.FillSoftRockRect(w, layout.JunctionEast.x - 5, layout.JunctionEast.y - 5, 11, 11);
            GoldVeinPlacer.FillSoftRockRect(w, layout.JunctionMid.x - 6, layout.JunctionMid.y - 6, 13, 13);

            // Tempting direct north — bedrock ridge
            GoldVeinPlacer.FillBedrockRect(w, sx - 14, sy + 38, 29, 10);
            GoldVeinPlacer.FillBedrockRect(w, layout.DirectBedrockPlug.x - 10,
                layout.DirectBedrockPlug.y - 3, 21, 8);

            GoldVeinPlacer.FillBedrockRect(w, layout.GoldTemptBedrock.x - 5,
                layout.GoldTemptBedrock.y - 4, 12, 10);
            GoldVeinPlacer.FillBedrockRect(w, layout.DiamondTemptBedrock.x - 5,
                layout.DiamondTemptBedrock.y - 4, 12, 10);

            // Soft links around the ridge (not through it)
            SoftCorridor(w, layout.JunctionWest.x, layout.JunctionWest.y, sx - 24, sy + 68, 3);
            SoftCorridor(w, sx - 24, sy + 68, layout.JunctionMid.x - 8, layout.JunctionMid.y, 3);
            SoftCorridor(w, layout.JunctionEast.x, layout.JunctionEast.y, sx + 24, sy + 68, 3);
            SoftCorridor(w, sx + 24, sy + 68, layout.JunctionMid.x + 8, layout.JunctionMid.y, 3);
            GoldVeinPlacer.FillSoftRockRect(w, layout.JunctionMid.x - 6, layout.JunctionMid.y - 6, 13, 13);

            SoftCorridor(w, layout.JunctionEast.x, layout.JunctionEast.y,
                layout.GoldChamber.xMax + 2, layout.GoldCenter.y, 3);
            SoftCorridor(w, layout.JunctionMid.x + 22, layout.JunctionMid.y + 12,
                layout.GoldCenter.x + 2, layout.GoldChamber.yMin - 2, 3);
            SoftCorridor(w, layout.JunctionWest.x, layout.JunctionWest.y,
                layout.DiamondChamber.xMin - 2, layout.DiamondCenter.y, 3);
            SoftCorridor(w, layout.JunctionMid.x - 22, layout.JunctionMid.y + 12,
                layout.DiamondCenter.x - 2, layout.DiamondChamber.yMin - 2, 3);

            GoldVeinPlacer.TryPlaceGasPocket(w, layout.GoldTemptGas.x, layout.GoldTemptGas.y,
                rx: 4, ry: 3, stretch: 0.9f, rockBuffer: 2);
            GoldVeinPlacer.TryPlaceGasPocket(w, layout.DiamondTemptGas.x, layout.DiamondTemptGas.y,
                rx: 4, ry: 3, stretch: 0.9f, rockBuffer: 2);
            GoldVeinPlacer.TryPlaceGasPocket(w, sx + 6, sy + 72, rx: 3, ry: 3, stretch: 1f, rockBuffer: 2);

            GoldVeinPlacer.FillSoftRockRect(w,
                layout.GoldChamber.xMin - 3, layout.GoldChamber.yMin - 3,
                layout.GoldChamber.width + 6, layout.GoldChamber.height + 6);
            GoldVeinPlacer.FillSoftRockRect(w,
                layout.DiamondChamber.xMin - 3, layout.DiamondChamber.yMin - 3,
                layout.DiamondChamber.width + 6, layout.DiamondChamber.height + 6);

            GoldVeinPlacer.FillGoldRect(w,
                layout.GoldChamber.xMin, layout.GoldChamber.yMin,
                layout.GoldChamber.width, layout.GoldChamber.height, 3, 4);
            GoldVeinPlacer.FillDiamondRect(w,
                layout.DiamondChamber.xMin, layout.DiamondChamber.yMin,
                layout.DiamondChamber.width, layout.DiamondChamber.height, 2, 4);

            GoldVeinPlacer.FillSoftRockRect(w, sx - 10, sy + 62, 20, 8);
            GoldVeinPlacer.FillSoftRockRect(w, sx + 14, sy + 58, 22, 8);

            EnsureSoftApproach(w, layout, gold: true);
            EnsureSoftApproach(w, layout, gold: false);
            return layout;
        }

        static void EnsureSoftApproach(FineTerrainWorld w, Layout layout, bool gold)
        {
            var chamber = gold ? layout.GoldChamber : layout.DiamondChamber;
            int approaches = CountDisjointApproaches(w, layout.Spawn.x, layout.Spawn.y, chamber, out _, out _);
            if (approaches >= 2) return;

            if (gold)
            {
                SoftCorridor(w, layout.JunctionEast.x + 14, layout.JunctionEast.y + 10,
                    layout.GoldChamber.xMax + 4, layout.GoldCenter.y + 2, 4);
                SoftCorridor(w, layout.JunctionMid.x + 28, layout.JunctionMid.y + 18,
                    layout.GoldCenter.x, layout.GoldChamber.yMin - 3, 3);
                GoldVeinPlacer.FillGoldRect(w, chamber.xMin, chamber.yMin, chamber.width, chamber.height, 3, 4);
            }
            else
            {
                SoftCorridor(w, layout.JunctionWest.x - 14, layout.JunctionWest.y + 10,
                    layout.DiamondChamber.xMin - 4, layout.DiamondCenter.y + 2, 4);
                SoftCorridor(w, layout.JunctionMid.x - 28, layout.JunctionMid.y + 18,
                    layout.DiamondCenter.x, layout.DiamondChamber.yMin - 3, 3);
                GoldVeinPlacer.FillDiamondRect(w, chamber.xMin, chamber.yMin, chamber.width, chamber.height, 2, 4);
            }
        }

        public static Layout MakeLayout(int sx, int sy)
        {
            var gold = new RectInt(sx + 38, sy + 102, 16, 12);
            var dia = new RectInt(sx - 58, sy + 106, 14, 12);
            return new Layout
            {
                Spawn = new Vector2Int(sx, sy),
                GoldChamber = gold,
                DiamondChamber = dia,
                GoldCenter = new Vector2Int(gold.xMin + gold.width / 2, gold.yMin + gold.height / 2),
                DiamondCenter = new Vector2Int(dia.xMin + dia.width / 2, dia.yMin + dia.height / 2),
                JunctionWest = new Vector2Int(sx - 42, sy + 48),
                JunctionEast = new Vector2Int(sx + 42, sy + 48),
                JunctionMid = new Vector2Int(sx, sy + 78),
                DirectBedrockPlug = new Vector2Int(sx, sy + 52),
                GoldTemptGas = new Vector2Int(sx + 22, sy + 88),
                DiamondTemptGas = new Vector2Int(sx - 22, sy + 90),
                GoldTemptBedrock = new Vector2Int(sx + 18, sy + 98),
                DiamondTemptBedrock = new Vector2Int(sx - 18, sy + 100),
            };
        }

        public static void SoftCorridor(FineTerrainWorld w, int x0, int y0, int x1, int y1, int half)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * 2 + 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)Mathf.Max(1, steps);
                int cx = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int cy = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (int oy = -half; oy <= half; oy++)
                for (int ox = -half; ox <= half; ox++)
                {
                    if (ox * ox + oy * oy > half * half + half) continue;
                    int x = cx + ox, y = cy + oy;
                    if (!w.InBounds(x, y) || w.IsExcavated(x, y)) continue;
                    var c = w.Get(x, y);
                    if (c.IsUndamageableBorder) continue;
                    if (w.IsGas(x, y)) continue;
                    w.Set(x, y, FineTerrainWorld.FromCounts(4, 0, 0, 0));
                }
            }
        }

        public static bool IsSoftPassable(FineTerrainWorld w, int x, int y)
        {
            if (!w.InBounds(x, y)) return false;
            if (w.IsGas(x, y)) return false;
            if (w.IsTunnelOpen(x, y)) return true;
            var c = w.Get(x, y);
            if (c.IsUndamageableBorder) return false;
            if (c.Phase == TerrainPhase.Excavated) return false;
            return c.BedrockCount < 2;
        }

        public static ValidationResult Validate(FineTerrainWorld w, Layout layout)
        {
            var r = new ValidationResult { Layout = layout };
            int sx = layout.Spawn.x, sy = layout.Spawn.y;

            for (int y = 1; y < w.Height - 1; y++)
            for (int x = 1; x < w.Width - 1; x++)
            {
                if (w.IsGas(x, y)) { r.GasCells++; continue; }
                var c = w.Get(x, y);
                if (c.IsUndamageableBorder || c.Phase == TerrainPhase.Excavated) continue;
                if (c.GoldCount > 0) r.GoldCells++;
                if (c.DiamondCount > 0) r.DiamondCells++;
                if (c.BedrockCount >= 2) r.BedrockCells++;
            }

            r.SoftReachableCells = BfsReachable(w, sx, sy, null).Count;
            r.GoldReachableSoft = ChamberReachable(w, sx, sy, layout.GoldChamber, out r.GoldSoftDist);
            r.DiamondReachableSoft = ChamberReachable(w, sx, sy, layout.DiamondChamber, out r.DiamondSoftDist);
            r.GoldApproaches = CountDisjointApproaches(w, sx, sy, layout.GoldChamber, out r.GoldPathA, out r.GoldPathB);
            r.DiamondApproaches = CountDisjointApproaches(w, sx, sy, layout.DiamondChamber, out r.DiamondPathA, out r.DiamondPathB);

            r.DirectNorthBlockedByHazard = !PathExistsThroughBand(w, sx, sy, sx - 4, sx + 4, sy + 40, sy + 58);
            r.GoldTemptBlocked = !IsSoftPassable(w, layout.GoldTemptBedrock.x, layout.GoldTemptBedrock.y)
                || w.IsGas(layout.GoldTemptGas.x, layout.GoldTemptGas.y);
            r.DiamondTemptBlocked = !IsSoftPassable(w, layout.DiamondTemptBedrock.x, layout.DiamondTemptBedrock.y)
                || w.IsGas(layout.DiamondTemptGas.x, layout.DiamondTemptGas.y);
            r.TrivialFromSpawn = (r.GoldSoftDist >= 0 && r.GoldSoftDist < 70)
                || (r.DiamondSoftDist >= 0 && r.DiamondSoftDist < 70);

            if (r.GoldCells < 8) r.Failures.Add("Too few gold cells.");
            if (r.DiamondCells < 6) r.Failures.Add("Too few diamond cells.");
            if (!r.GoldReachableSoft) r.Failures.Add("Gold chamber not soft-reachable.");
            if (!r.DiamondReachableSoft) r.Failures.Add("Diamond chamber not soft-reachable.");
            if (r.GoldApproaches < 2) r.Failures.Add($"Gold soft approaches = {r.GoldApproaches} (need ≥2).");
            if (r.DiamondApproaches < 2) r.Failures.Add($"Diamond soft approaches = {r.DiamondApproaches} (need ≥2).");
            if (!r.DirectNorthBlockedByHazard) r.Failures.Add("Direct north not blocked.");
            if (!r.GoldTemptBlocked) r.Failures.Add("Gold tempting shortcut not hazardous.");
            if (!r.DiamondTemptBlocked) r.Failures.Add("Diamond tempting shortcut not hazardous.");
            if (r.TrivialFromSpawn) r.Failures.Add("Target soft distance too short.");

            if (r.GoldSoftDist >= 90) r.Notes.Add("Gold soft distance supports Day 9–12 pacing.");
            if (r.DiamondSoftDist >= 90) r.Notes.Add("Diamond soft distance supports Day 9–12 pacing.");
            r.Notes.Add("Early organic teases retained for prospecting practice.");
            r.Notes.Add("Truth View shows geology; MISSION VAL paints soft viable routes.");
            r.Passed = r.Failures.Count == 0;
            return r;
        }

        static bool ChamberReachable(FineTerrainWorld w, int sx, int sy, RectInt chamber, out int dist)
        {
            dist = -1;
            var distMap = BfsDistances(w, sx, sy, null);
            int best = int.MaxValue;
            for (int y = chamber.yMin; y < chamber.yMax; y++)
            for (int x = chamber.xMin; x < chamber.xMax; x++)
            {
                if (!w.InBounds(x, y)) continue;
                if (!distMap.TryGetValue(y * w.Width + x, out int d)) continue;
                if (d < best) best = d;
            }
            if (best == int.MaxValue) return false;
            dist = best;
            return true;
        }

        static int CountDisjointApproaches(FineTerrainWorld w, int sx, int sy, RectInt chamber,
            out List<Vector2Int> pathA, out List<Vector2Int> pathB)
        {
            pathA = FindSoftPath(w, sx, sy, chamber, null);
            if (pathA == null || pathA.Count == 0)
            {
                pathB = new List<Vector2Int>();
                return 0;
            }

            var blocked = new HashSet<int>();
            int keepTail = Mathf.Min(8, pathA.Count);
            int keepHead = Mathf.Min(10, pathA.Count);
            for (int i = keepHead; i < pathA.Count - keepTail; i++)
            {
                var p = pathA[i];
                for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int x = p.x + ox, y = p.y + oy;
                    if (w.InBounds(x, y)) blocked.Add(y * w.Width + x);
                }
            }

            pathB = FindSoftPath(w, sx, sy, chamber, blocked);
            return pathB == null || pathB.Count == 0 ? 1 : 2;
        }

        static List<Vector2Int> FindSoftPath(FineTerrainWorld w, int sx, int sy, RectInt chamber,
            HashSet<int> blocked)
        {
            int wdt = w.Width;
            var prev = new Dictionary<int, int>(4096);
            var q = new Queue<int>();
            int start = sy * wdt + sx;
            if (!IsSoftPassable(w, sx, sy)) return null;
            q.Enqueue(start);
            prev[start] = -1;
            int found = -1;

            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                int cx = cur % wdt, cy = cur / wdt;
                if (chamber.Contains(new Vector2Int(cx, cy)))
                {
                    found = cur;
                    break;
                }
                TryEnqueue(w, q, prev, blocked, cx + 1, cy, cur);
                TryEnqueue(w, q, prev, blocked, cx - 1, cy, cur);
                TryEnqueue(w, q, prev, blocked, cx, cy + 1, cur);
                TryEnqueue(w, q, prev, blocked, cx, cy - 1, cur);
            }

            if (found < 0) return null;
            var path = new List<Vector2Int>(128);
            for (int at = found; at >= 0; at = prev[at])
                path.Add(new Vector2Int(at % wdt, at / wdt));
            path.Reverse();
            return path;
        }

        static void TryEnqueue(FineTerrainWorld w, Queue<int> q, Dictionary<int, int> prev,
            HashSet<int> blocked, int x, int y, int from)
        {
            if (!IsSoftPassable(w, x, y)) return;
            int i = y * w.Width + x;
            if (blocked != null && blocked.Contains(i)) return;
            if (prev.ContainsKey(i)) return;
            prev[i] = from;
            q.Enqueue(i);
        }

        static HashSet<int> BfsReachable(FineTerrainWorld w, int sx, int sy, HashSet<int> blocked)
        {
            var seen = new HashSet<int>();
            var q = new Queue<int>();
            if (!IsSoftPassable(w, sx, sy)) return seen;
            int start = sy * w.Width + sx;
            q.Enqueue(start);
            seen.Add(start);
            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                int cx = cur % w.Width, cy = cur / w.Width;
                void Step(int x, int y)
                {
                    if (!IsSoftPassable(w, x, y)) return;
                    int i = y * w.Width + x;
                    if (blocked != null && blocked.Contains(i)) return;
                    if (!seen.Add(i)) return;
                    q.Enqueue(i);
                }
                Step(cx + 1, cy); Step(cx - 1, cy); Step(cx, cy + 1); Step(cx, cy - 1);
            }
            return seen;
        }

        static Dictionary<int, int> BfsDistances(FineTerrainWorld w, int sx, int sy, HashSet<int> blocked)
        {
            var dist = new Dictionary<int, int>(4096);
            var q = new Queue<int>();
            if (!IsSoftPassable(w, sx, sy)) return dist;
            int start = sy * w.Width + sx;
            q.Enqueue(start);
            dist[start] = 0;
            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                int cx = cur % w.Width, cy = cur / w.Width;
                int d0 = dist[cur];
                void Step(int x, int y)
                {
                    if (!IsSoftPassable(w, x, y)) return;
                    int i = y * w.Width + x;
                    if (blocked != null && blocked.Contains(i)) return;
                    if (dist.ContainsKey(i)) return;
                    dist[i] = d0 + 1;
                    q.Enqueue(i);
                }
                Step(cx + 1, cy); Step(cx - 1, cy); Step(cx, cy + 1); Step(cx, cy - 1);
            }
            return dist;
        }

        static bool PathExistsThroughBand(FineTerrainWorld w, int sx, int sy,
            int x0, int x1, int y0, int y1)
        {
            var reach = BfsReachable(w, sx, sy, null);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (!w.InBounds(x, y)) continue;
                if (reach.Contains(y * w.Width + x)) return true;
            }
            return false;
        }
    }

    public enum Mission01Outcome
    {
        Active = 0,
        Complete = 1,
        Failed = 2,
    }

    /// <summary>
    /// Mission 01 runtime: refine Gold + Diamonds within 14 in-game days.
    /// FOUND = refined stockpile (≥1), matching HUD GOLD / DIA.
    /// </summary>
    public sealed class Mission01Runtime
    {
        public Mission01Geology.Layout Layout;
        public Mission01Geology.ValidationResult LastValidation;
        public Mission01Outcome Outcome { get; private set; } = Mission01Outcome.Active;
        public bool GoldFound { get; private set; }
        public bool DiamondFound { get; private set; }
        public int DaysRemaining { get; private set; } = Mission01Geology.MissionDays;
        public bool DevValidationVisible;

        public void Reset(Mission01Geology.Layout layout, Mission01Geology.ValidationResult validation)
        {
            Layout = layout;
            LastValidation = validation;
            Outcome = Mission01Outcome.Active;
            GoldFound = false;
            DiamondFound = false;
            DaysRemaining = Mission01Geology.MissionDays;
        }

        public void Sync(int dayIndex, int refinedGold, int refinedDiamond, float gameHour,
            float prevGameHour, float shiftEndHour)
        {
            if (Outcome != Mission01Outcome.Active) return;

            if (!GoldFound && refinedGold > 0)
            {
                GoldFound = true;
                DigHoodLog.Push("MISSION 01 | GOLD — FOUND");
            }
            if (!DiamondFound && refinedDiamond > 0)
            {
                DiamondFound = true;
                DigHoodLog.Push("MISSION 01 | DIAMONDS — FOUND");
            }

            DaysRemaining = Mathf.Clamp(Mission01Geology.MissionDays - dayIndex + 1,
                0, Mission01Geology.MissionDays);

            if (GoldFound && DiamondFound)
            {
                Outcome = Mission01Outcome.Complete;
                DigHoodLog.Push("MISSION 01 | MISSION COMPLETE");
                return;
            }

            bool day14ShiftEnd = dayIndex == Mission01Geology.MissionDays
                && prevGameHour < shiftEndHour && gameHour >= shiftEndHour;
            bool pastDeadline = dayIndex > Mission01Geology.MissionDays;
            if (day14ShiftEnd || pastDeadline)
            {
                Outcome = Mission01Outcome.Failed;
                DaysRemaining = 0;
                DigHoodLog.Push("MISSION 01 | MISSION FAILED — day 14 ended without both finds");
            }
        }
    }
}
