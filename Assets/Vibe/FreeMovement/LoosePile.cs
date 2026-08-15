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
        public byte Sockets;
        public bool IsGold => GoldGrade > 0;
        public bool Claimed { get; set; }

        /// <summary>Gold payout — more sockets = more value (1→1, 2→3, 3→6, 4→10).</summary>
        public int GoldValue => GoldGrade <= 0 ? 0 : GoldGrade * (GoldGrade + 1) / 2;

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
            Vector2? ejectFrom = null, byte sockets = 0)
        {
            mass = Mathf.Max(1f, mass);
            bool gold = goldGrade > 0;
            int bedrock = 0;
            if (sockets != 0)
            {
                for (int i = 0; i < 4; i++)
                    if (((sockets >> (i * 2)) & 0b11) == (int)SocketKind.Bedrock) bedrock++;
            }

            var go = new GameObject(gold ? "LooseGold" : "LooseRock");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            // ~one wall cell on the floor — slightly smaller so it reads as loose
            float size = cellSize * Random.Range(0.82f, 0.98f);
            if (mass > 6f) size *= 1.06f;
            go.transform.localScale = Vector3.one * (size * 0.55f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-18f, 18f));

            int seed = (Mathf.RoundToInt(localPos.x * 100f) * 73856093)
                       ^ (Mathf.RoundToInt(localPos.y * 100f) * 19349663)
                       ^ (goldGrade * 83492791)
                       ^ Random.Range(0, 99991);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = DigVisualKit.MakeWallChunk(goldGrade, bedrock, seed);
            sr.sortingOrder = 18;
            DigVisualKit.ApplyLit(sr);
            sr.color = Color.white;

            var pile = go.AddComponent<LoosePile>();
            pile.Mass = mass;
            pile.GoldGrade = goldGrade;
            pile.Sockets = sockets;
            go.AddComponent<LooseSettle>().Init(localPos, size, ejectFrom);
            return pile;
        }

        /// <summary>Spawn rock/gold ejected from a drill tip toward a landing spot.</summary>
        public static LoosePile SpawnFromDrill(Transform parent, Vector2 drillTip, Vector2 facing,
            float mass, byte goldGrade, float cellSize, float excavatorRadius = 0.6f, byte sockets = 0)
        {
            Vector2 perp = new(-facing.y, facing.x);
            Vector2 land = drillTip
                - facing * Random.Range(0.12f, 0.38f)
                + perp * Random.Range(-0.22f, 0.22f);
            return Spawn(parent, null, land, mass, goldGrade, cellSize, excavatorRadius,
                ejectFrom: drillTip, sockets: sockets);
        }

        /// <summary>
        /// One loose piece per excavated cell — same mass / sockets / gold grade as the wall cell.
        /// Hauler picks up one cell-worth at a time.
        /// </summary>
        public static LoosePile SpawnCellFromDrill(Transform parent, Vector2 drillTip, Vector2 facing,
            TerrainCell cell, float cellSize, float excavatorRadius = 0.6f)
        {
            return SpawnFromDrill(parent, drillTip, facing, cell.Mass, cell.GoldGrade,
                cellSize, excavatorRadius, sockets: cell.Sockets);
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
                    // Gold always outranks rock; higher grade outranks lower within gold
                    if (p.IsGold) score = d - p.GoldGrade * 50_000f;
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
        float _endScale;

        public void Init(Vector2 targetLocal, float size, Vector2? ejectFrom = null)
        {
            _target = targetLocal;
            _endScale = size;
            if (ejectFrom.HasValue)
            {
                // Burst out from drill tip toward landing
                _start = ejectFrom.Value + Random.insideUnitCircle * (size * 0.15f);
            }
            else
            {
                _start = targetLocal + Random.insideUnitCircle * (size * 0.7f) + Vector2.up * (size * 0.45f);
            }
            transform.localPosition = _start;
            transform.localScale = Vector3.one * (_endScale * 0.45f);
            _spin = Random.Range(-260f, 260f);
            _t = 0f;
        }

        void Update()
        {
            _t += Time.deltaTime * 5.2f;
            float u = Mathf.Clamp01(_t);
            // Fast out of tip, ease into ground
            float ease = 1f - (1f - u) * (1f - u);
            Vector2 p = Vector2.Lerp(_start, _target, ease);
            // Small arc
            float lift = Mathf.Sin(u * Mathf.PI) * (_endScale * 0.35f);
            p += (_target - _start).normalized * 0f;
            // Perpendicular-ish lift in world-up of map
            p.y += lift * 0.55f;
            transform.localPosition = p;
            // Pop scale: small → slightly overshoot → settle
            float s = _endScale * Mathf.Lerp(0.45f, 1f, ease);
            if (u < 0.55f) s *= Mathf.Lerp(0.9f, 1.08f, u / 0.55f);
            else s *= Mathf.Lerp(1.08f, 1f, (u - 0.55f) / 0.45f);
            transform.localScale = Vector3.one * s;
            transform.Rotate(0f, 0f, _spin * Time.deltaTime * (1f - u));
            if (u >= 1f)
            {
                transform.localPosition = _target;
                transform.localScale = Vector3.one * _endScale;
                Destroy(this);
            }
        }
    }
}
