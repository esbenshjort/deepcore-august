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
            Vector2? ejectFrom = null)
        {
            mass = Mathf.Max(1f, mass);
            bool gold = goldGrade > 0;
            var go = new GameObject(gold ? "LooseGold" : "LooseRock");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            float baseScale = Mathf.Clamp(excavatorRadius * 0.28f, 0.12f, 0.32f);
            float size = baseScale * (0.75f + Mathf.Clamp(mass, 1f, 10f) * 0.05f);
            if (gold) size *= 1.06f;
            if (goldGrade >= 3) size *= 1.05f + (goldGrade - 2) * 0.04f;
            go.transform.localScale = Vector3.one * (size * 0.55f); // start small — settle grows
            go.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-25f, 25f));

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = gold ? DigVisualKit.GoldNugget : DigVisualKit.RockPile;
            sr.sortingOrder = 18;
            DigVisualKit.ApplyLit(sr);
            float goldWarm = goldGrade / 4f;
            sr.color = gold
                ? Color.Lerp(new Color(0.75f, 0.55f, 0.18f), new Color(1f, 0.88f, 0.4f), goldWarm)
                : Color.Lerp(Color.white, new Color(0.85f, 0.8f, 0.75f), Random.value * 0.25f);

            int chips = gold ? Random.Range(1 + goldGrade, 2 + goldGrade) : Random.Range(2, 4);
            for (int i = 0; i < chips; i++)
            {
                var chip = new GameObject("Chip");
                chip.transform.SetParent(go.transform, false);
                chip.transform.localPosition = (Vector3)(Random.insideUnitCircle * 0.4f);
                chip.transform.localScale = Vector3.one * Random.Range(0.22f, 0.42f);
                chip.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                var csr = chip.AddComponent<SpriteRenderer>();
                csr.sprite = gold ? DigVisualKit.GoldNugget : DigVisualKit.RockPile;
                csr.sortingOrder = 17;
                DigVisualKit.ApplyLit(csr);
                csr.color = sr.color;
            }

            var pile = go.AddComponent<LoosePile>();
            pile.Mass = mass;
            pile.GoldGrade = goldGrade;
            go.AddComponent<LooseSettle>().Init(localPos, size, ejectFrom);
            return pile;
        }

        /// <summary>Spawn rock/gold ejected from a drill tip toward a landing spot.</summary>
        public static LoosePile SpawnFromDrill(Transform parent, Vector2 drillTip, Vector2 facing,
            float mass, byte goldGrade, float cellSize, float excavatorRadius = 0.6f)
        {
            Vector2 perp = new(-facing.y, facing.x);
            // Land slightly behind/beside the tip so it feels blasted out of the cut
            Vector2 land = drillTip
                - facing * Random.Range(0.12f, 0.38f)
                + perp * Random.Range(-0.22f, 0.22f);
            return Spawn(parent, null, land, mass, goldGrade, cellSize, excavatorRadius, ejectFrom: drillTip);
        }

        public static LoosePile FindNearest(Vector2 fromTerrain, float maxDist, bool unclaimedOnly = true)
        {
            LoosePile best = null;
            float bestD = maxDist * maxDist;
            for (int i = 0; i < Active.Count; i++)
            {
                var p = Active[i];
                if (p == null) continue;
                if (unclaimedOnly && p.Claimed) continue;

                // Piles are under Loose/ → world root; resolve to terrain/local space
                Transform root = p.transform.parent != null ? p.transform.parent.parent : null;
                Vector2 terrainPos = root != null
                    ? (Vector2)root.InverseTransformPoint(p.transform.position)
                    : (Vector2)p.transform.localPosition;

                float d = (terrainPos - fromTerrain).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = p;
                }
            }
            return best;
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
