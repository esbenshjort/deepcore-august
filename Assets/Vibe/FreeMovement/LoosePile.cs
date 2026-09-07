using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Loose rock/gold that lands under the excavator for a hauler.</summary>
    public sealed class LoosePile : MonoBehaviour
    {
        static readonly List<LoosePile> Active = new(128);

        public float Mass;
        public byte GoldGrade;
        public byte DiamondGrade;
        public byte Sockets;
        public bool IsGold => GoldGrade > 0;
        public bool IsDiamond => DiamondGrade > 0;
        public bool IsPrecious => GoldGrade > 0 || DiamondGrade > 0;
        public bool Claimed { get; set; }
        /// <summary>World-radius of the rubble heap for walk slowdown / overlap.</summary>
        public float FootprintRadius { get; private set; } = 0.12f;

        /// <summary>Gold payout — more sockets = more value (1→1, 2→3, 3→6, 4→10).</summary>
        public int GoldValue => GoldGrade <= 0 ? 0 : GoldGrade * (GoldGrade + 1) / 2;
        /// <summary>Diamond payout — higher than gold (1→3, 2→7, 3→12, 4→18).</summary>
        public int DiamondValue => DiamondGrade <= 0 ? 0 : DiamondGrade * (DiamondGrade + 5) / 2;

        public static IReadOnlyList<LoosePile> All => Active;

        void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        void OnDisable()
        {
            Active.Remove(this);
            Claimed = false;
        }

        public static LoosePile Spawn(Transform parent, Sprite ignoredSprite, Vector2 localPos,
            float mass, byte goldGrade, float cellSize, float excavatorRadius = 0.6f,
            Vector2? ejectFrom = null, byte sockets = 0, byte diamondGrade = 0)
        {
            mass = Mathf.Max(1f, mass);
            int bedrock = 0;
            if (sockets != 0)
            {
                int g = 0, d = 0;
                for (int i = 0; i < 4; i++)
                {
                    int k = (sockets >> (i * 2)) & 0b11;
                    if (k == (int)SocketKind.Bedrock) bedrock++;
                    else if (k == (int)SocketKind.Gold) g++;
                    else if (k == (int)SocketKind.Diamond) d++;
                }
                if (goldGrade == 0) goldGrade = (byte)g;
                if (diamondGrade == 0) diamondGrade = (byte)d;
            }

            string name = diamondGrade > 0 ? "LooseDiamond"
                : goldGrade > 0 ? "LooseGold" : "LooseRock";
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one;
            go.transform.localRotation = Quaternion.identity;

            float size = Mathf.Max(0.01f, cellSize);
            int seed = (Mathf.RoundToInt(localPos.x * 100f) * 73856093)
                       ^ (Mathf.RoundToInt(localPos.y * 100f) * 19349663)
                       ^ (goldGrade * 83492791)
                       ^ (diamondGrade * 19349663)
                       ^ (sockets * 9973)
                       ^ (Random.Range(0, 1 << 20));

            // 3–6 bigger chips — reads as a rubble heap, not a sparse drop
            int chips = Random.Range(3, 7);
            float spread = size * Random.Range(0.42f, 0.72f);
            float maxChipReach = 0f;
            for (int i = 0; i < chips; i++)
            {
                var chip = new GameObject($"Chip{i}");
                chip.transform.SetParent(go.transform, false);
                Vector2 off = Random.insideUnitCircle;
                if (off.sqrMagnitude < 0.0001f) off = Vector2.right;
                off = off.normalized * (spread * Random.Range(0.2f, 1f));
                // Keep a dense core so the pile still looks solid
                if (i == 0) off *= 0.25f;
                chip.transform.localPosition = off;

                float sm = size * Random.Range(0.58f, 0.98f);
                float aspect = Random.Range(0.7f, 1.35f);
                chip.transform.localScale = new Vector3(sm * aspect, sm / Mathf.Max(0.55f, aspect), 1f);
                chip.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-55f, 55f));
                maxChipReach = Mathf.Max(maxChipReach, off.magnitude + sm * 0.45f);

                var sr = chip.AddComponent<SpriteRenderer>();
                // Rock chips rarely full-gold wash — flecks only unless rich
                byte chipGold = goldGrade;
                byte chipDia = diamondGrade;
                if (goldGrade > 0 && i > 0 && Random.value > 0.45f)
                    chipGold = (byte)Mathf.Max(0, goldGrade - 1);
                if (diamondGrade > 0 && i > 0 && Random.value > 0.5f)
                    chipDia = 0;
                sr.sprite = DigVisualKit.MakeLooseRockChunk(chipGold, bedrock, seed + i * 9173, chipDia);
                sr.sortingOrder = 18 + (i & 1);
                DigVisualKit.ApplyLit(sr);
                sr.color = Color.white;
            }

            var pile = go.AddComponent<LoosePile>();
            pile.Mass = mass;
            pile.GoldGrade = goldGrade;
            pile.DiamondGrade = diamondGrade;
            pile.Sockets = sockets;
            pile.FootprintRadius = Mathf.Max(size * 0.55f, maxChipReach);
            go.AddComponent<LooseSettle>().Init(localPos, ejectFrom);

            if (diamondGrade > 0 || goldGrade > 0)
                OreShimmer.Attach(go.transform, diamondGrade > 0, goldGrade, diamondGrade,
                    size * 0.85f);

            return pile;
        }

        /// <summary>Spawn rock/gold ejected from a drill tip toward a landing spot.</summary>
        public static LoosePile SpawnFromDrill(Transform parent, Vector2 drillTip, Vector2 facing,
            float mass, byte goldGrade, float cellSize, float excavatorRadius = 0.6f, byte sockets = 0)
        {
            Vector2 land = ChooseFloorLandPos(drillTip, facing, cellSize);
            return Spawn(parent, null, land, mass, goldGrade, cellSize, excavatorRadius,
                ejectFrom: drillTip, sockets: sockets);
        }

        /// <summary>
        /// Snap behind the dig face onto a floor cell (legacy pack path for non-cell spawns).
        /// </summary>
        static Vector2 ChooseFloorLandPos(Vector2 drillTip, Vector2 facing, float cellSize)
        {
            float cs = Mathf.Max(0.01f, cellSize);
            Vector2 behind = drillTip - facing.normalized * (cs * 1.35f);
            int cx = Mathf.FloorToInt(behind.x / cs);
            int cy = Mathf.FloorToInt(behind.y / cs);
            return CellCenter(cx, cy, cs) + Random.insideUnitCircle * (cs * Random.Range(0.16f, 0.34f));
        }

        static Vector2 CellCenter(int cx, int cy, float cs) =>
            new((cx + 0.5f) * cs, (cy + 0.5f) * cs);

        /// <summary>
        /// One loose piece per excavated cell — same size / colour / sockets as the wall cell,
        /// placed on that cell's floor for the hauler to pick up.
        /// </summary>
        public static LoosePile SpawnCellFromDrill(Transform parent, Vector2 drillTip, Vector2 facing,
            TerrainCell cell, float cellSize, float excavatorRadius = 0.6f,
            int cellX = int.MinValue, int cellY = int.MinValue)
        {
            if (cellX != int.MinValue && cellY != int.MinValue)
                return SpawnAtExcavatedCell(parent, drillTip, cellX, cellY, cell, cellSize);

            // Fallback when cell coords unknown: land on floor behind dig face
            return SpawnFromDrill(parent, drillTip, facing, cell.Mass, cell.GoldGrade,
                cellSize, excavatorRadius, sockets: cell.Sockets);
        }

        /// <summary>
        /// Place dug material as a scattered floor pile — not snapped to the cell grid.
        /// </summary>
        public static LoosePile SpawnAtExcavatedCell(Transform parent, Vector2 drillTip,
            int cellX, int cellY, TerrainCell cell, float cellSize)
        {
            float cs = Mathf.Max(0.01f, cellSize);
            Vector2 land = CellCenter(cellX, cellY, cs);
            // Broad scatter so piles don't telegraph the tile lattice
            land += Random.insideUnitCircle * (cs * Random.Range(0.12f, 0.32f));
            Vector2 away = land - drillTip;
            if (away.sqrMagnitude > 0.0001f)
                land += away.normalized * (cs * Random.Range(0.02f, 0.12f));
            return Spawn(parent, null, land, cell.Mass, cell.GoldGrade, cs,
                ejectFrom: drillTip, sockets: cell.Sockets, diamondGrade: cell.DiamondGrade);
        }

        public static LoosePile FindNearest(Vector2 fromTerrain, float maxDist, bool unclaimedOnly = true,
            Transform spaceRoot = null)
        {
            return FindBest(fromTerrain, maxDist, unclaimedOnly, spaceRoot, preferGold: false);
        }

        /// <summary>
        /// Nearest pile, optionally preferring gold (any gold beats rock; richer gold wins ties).
        /// </summary>
        public static LoosePile FindBest(Vector2 fromTerrain, float maxDist, bool unclaimedOnly,
            Transform spaceRoot, bool preferGold)
        {
            LoosePile best = null;
            float bestScore = float.MaxValue;
            float maxSqr = maxDist * maxDist;
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var p = Active[i];
                if (p == null)
                {
                    Active.RemoveAt(i);
                    continue;
                }
                if (unclaimedOnly && p.Claimed) continue;

                Vector2 terrainPos = PileTerrainPos(p, spaceRoot);
                float d = (terrainPos - fromTerrain).sqrMagnitude;
                if (d > maxSqr) continue;

                float score = d;
                if (preferGold)
                {
                    // Diamonds outrank gold; richer grades win ties
                    if (p.IsDiamond) score = d - 2_000_000f - p.DiamondGrade * 80_000f;
                    else if (p.IsGold) score = d - p.GoldGrade * 50_000f;
                    else score = d + 1_000_000f;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            return best;
        }

        public static Vector2 PileTerrainPos(LoosePile p, Transform spaceRoot = null)
        {
            if (p == null) return Vector2.zero;
            if (spaceRoot != null)
                return spaceRoot.InverseTransformPoint(p.transform.position);
            // Loose → world root: prefer local under Loose (same as terrain if Loose is at origin)
            if (p.transform.parent != null)
                return p.transform.parent.localPosition + (Vector3)p.transform.localPosition;
            return p.transform.localPosition;
        }

        /// <summary>True if standing on / brushing a loose rock heap on the floor.</summary>
        public static bool IsOnLoose(Vector2 terrainPos, float bodyRadius = 0.12f)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                var p = Active[i];
                if (p == null) continue;
                Vector2 pp = PileTerrainPos(p);
                float hit = bodyRadius + Mathf.Max(0.1f, p.FootprintRadius * 0.85f);
                if ((pp - terrainPos).sqrMagnitude <= hit * hit)
                    return true;
            }
            return false;
        }

        /// <summary>1 on clear floor, ~0.45 when walking through loose rubble.</summary>
        public static float SpeedMulAt(Vector2 terrainPos, float bodyRadius = 0.12f) =>
            IsOnLoose(terrainPos, bodyRadius) ? 0.45f : 1f;

        public static void ClearAllClaims()
        {
            for (int i = 0; i < Active.Count; i++)
            {
                var p = Active[i];
                if (p != null) p.Claimed = false;
            }
        }

        public static int LiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Active.Count; i++)
                    if (Active[i] != null) n++;
                return n;
            }
        }
    }

    /// <summary>
    /// Legacy stub — gold no longer self-lights. Keeps old play-mode instances from
    /// throwing "missing script" after the real component was removed.
    /// </summary>
    public sealed class Light2DProxy : MonoBehaviour
    {
        void Awake() => Destroy(this);
    }

    sealed class LooseSettle : MonoBehaviour
    {
        Vector2 _target;
        float _t;
        Vector2 _start;
        float _spin;
        float _sizeHint;

        public void Init(Vector2 targetLocal, Vector2? ejectFrom = null)
        {
            _target = targetLocal;
            _sizeHint = 0.12f;
            if (ejectFrom.HasValue)
            {
                _start = ejectFrom.Value + Random.insideUnitCircle * 0.04f;
            }
            else
            {
                _start = targetLocal + Random.insideUnitCircle * 0.08f + Vector2.up * 0.05f;
            }
            transform.localPosition = _start;
            _spin = Random.Range(-160f, 160f);
            _t = 0f;
        }

        void Update()
        {
            _t += Time.deltaTime * 5.2f;
            float u = Mathf.Clamp01(_t);
            float ease = 1f - (1f - u) * (1f - u);
            Vector2 p = Vector2.Lerp(_start, _target, ease);
            float lift = Mathf.Sin(u * Mathf.PI) * (_sizeHint * 1.4f);
            p.y += lift * 0.55f;
            transform.localPosition = p;
            transform.Rotate(0f, 0f, _spin * Time.deltaTime * (1f - u));
            if (u >= 1f)
            {
                transform.localPosition = _target;
                Destroy(this);
            }
        }
    }
}
