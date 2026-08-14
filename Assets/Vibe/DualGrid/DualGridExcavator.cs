using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.DualGrid
{
    /// <summary>
    /// Continuous drill: set any world goal, moves straight toward it.
    /// Tip wedge damages fine terrain (5 rock stages) — no nav-tile pathfinding.
    /// </summary>
    public sealed class DualGridExcavator : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 1.35f;
        [SerializeField] float digInterval = 0.045f;
        [SerializeField] int digsPerTick = 3;
        [SerializeField] float bodyRadius = 0.38f;
        [SerializeField] float tipLength = 0.55f;
        [SerializeField] float tipHalfWidth = 0.28f;

        DualGridWorld _world;
        Vector3 _goal;
        bool _hasGoal;
        float _digTimer;
        GameObject _goalMarker;

        public bool HasGoal => _hasGoal;
        public Vector3 Goal => _goal;

        public void Setup(DualGridWorld world, Vector2Int startNav)
        {
            _world = world;
            transform.position = _world.NavCellCenter(startNav.x, startNav.y);
            _hasGoal = false;

            var light = GetComponent<Light2D>();
            if (light == null) light = gameObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(1f, 0.72f, 0.38f);
            light.intensity = 1.2f;
            light.pointLightInnerRadius = 0.12f;
            light.pointLightOuterRadius = 1.8f;
            light.falloffIntensity = 0.55f;
        }

        public void SetGoalMarker(GameObject marker) => _goalMarker = marker;

        public void SetGoalWorld(Vector3 world)
        {
            if (_world == null) return;
            _goal = world;
            _goal.z = 0f;
            // clamp into map
            _goal.x = Mathf.Clamp(_goal.x, 0.15f, _world.NavWidth - 0.15f);
            _goal.y = Mathf.Clamp(_goal.y, 0.15f, _world.NavHeight - 0.15f);
            _hasGoal = true;
            if (_goalMarker != null)
            {
                _goalMarker.SetActive(true);
                _goalMarker.transform.position = _goal;
            }
        }

        public void ClearGoal()
        {
            _hasGoal = false;
            if (_goalMarker != null) _goalMarker.SetActive(false);
        }

        void Update()
        {
            if (_world == null || !_hasGoal) return;

            Vector3 pos = transform.position;
            Vector3 toGoal = _goal - pos;
            toGoal.z = 0f;
            float dist = toGoal.magnitude;
            if (dist < 0.12f)
            {
                ClearGoal();
                return;
            }

            Vector3 dir = toGoal / dist;
            Face(dir);

            // Always work the tip — pointed wedge ahead of the drill
            bool blocked = DigTipWedge(pos, dir);

            if (!blocked && ClearForBody(pos + dir * 0.08f))
            {
                transform.position = Vector3.MoveTowards(pos, _goal, moveSpeed * Time.deltaTime);
            }
            else
            {
                // cozy chatter while chewing rock
                transform.position = pos + (Vector3)(Random.insideUnitCircle * 0.012f);
            }
        }

        void Face(Vector3 dir)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.Euler(0, 0, ang), 280f * Time.deltaTime);
        }

        /// <summary>
        /// Damage rock in a triangular tip ahead. Returns true if still chewing solid rock.
        /// </summary>
        bool DigTipWedge(Vector3 pos, Vector3 dir)
        {
            Vector3 tipBase = pos + dir * (bodyRadius * 0.35f);
            Vector3 tipEnd = pos + dir * (bodyRadius + tipLength);
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f);

            _digTimer -= Time.deltaTime;
            bool anyRock = false;
            var best = new Vector2Int(-1, -1);
            float bestScore = float.MaxValue;

            // Scan fine cells near the tip wedge
            var min = _world.WorldToTerrain(tipBase - perp * tipHalfWidth - dir * 0.1f);
            var max = _world.WorldToTerrain(tipEnd + perp * tipHalfWidth + dir * 0.1f);
            // Also expand search box
            int x0 = Mathf.Min(min.x, max.x) - 1;
            int x1 = Mathf.Max(min.x, max.x) + 1;
            int y0 = Mathf.Min(min.y, max.y) - 1;
            int y1 = Mathf.Max(min.y, max.y) + 1;

            for (int ty = y0; ty <= y1; ty++)
            for (int tx = x0; tx <= x1; tx++)
            {
                if (!_world.InTerrainBounds(tx, ty)) continue;
                var state = _world.GetTerrain(tx, ty);
                if (state >= TerrainState.Excavated) continue;

                Vector3 c = _world.TerrainCellCenter(tx, ty);
                if (!PointInWedge(c, tipBase, tipEnd, perp, tipHalfWidth)) continue;

                anyRock = true;
                // Prefer cells closest to tip centerline & nearest tip end
                float along = Vector3.Dot(c - tipBase, dir);
                float side = Mathf.Abs(Vector3.Dot(c - tipBase, perp));
                float score = along * 0.35f + side * 2f + (int)state * 0.01f; // chew damaged first slightly? prefer front: lower along first
                // Actually prefer nearest to tip (highest along toward tipEnd) that's blocking
                score = -along + side * 1.5f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = new Vector2Int(tx, ty);
                }
            }

            if (!anyRock) return false;

            if (_digTimer <= 0f && best.x >= 0)
            {
                _digTimer = digInterval;
                _world.BeginBatch();
                // Damage several cells in tip — progressive 5-stage breakdown
                int left = digsPerTick;
                // re-scan priority order: damage best, then nearby in wedge
                DamageInWedgePriority(tipBase, tipEnd, perp, dir, ref left);
                _world.EndBatch();
            }

            return true;
        }

        void DamageInWedgePriority(Vector3 tipBase, Vector3 tipEnd, Vector3 perp, Vector3 dir, ref int left)
        {
            // Collect candidates
            var min = _world.WorldToTerrain(tipBase - perp * tipHalfWidth);
            var max = _world.WorldToTerrain(tipEnd + perp * tipHalfWidth);
            int x0 = Mathf.Min(min.x, max.x) - 1;
            int x1 = Mathf.Max(min.x, max.x) + 1;
            int y0 = Mathf.Min(min.y, max.y) - 1;
            int y1 = Mathf.Max(min.y, max.y) + 1;

            // Simple multi-pass: each dig damages the current best cell one stage
            while (left > 0)
            {
                Vector2Int best = default;
                float bestScore = float.MaxValue;
                bool found = false;

                for (int ty = y0; ty <= y1; ty++)
                for (int tx = x0; tx <= x1; tx++)
                {
                    if (!_world.InTerrainBounds(tx, ty)) continue;
                    var state = _world.GetTerrain(tx, ty);
                    if (state >= TerrainState.Excavated) continue;
                    Vector3 c = _world.TerrainCellCenter(tx, ty);
                    if (!PointInWedge(c, tipBase, tipEnd, perp, tipHalfWidth)) continue;

                    float along = Vector3.Dot(c - tipBase, dir);
                    float side = Mathf.Abs(Vector3.Dot(c - tipBase, perp));
                    // Front-center first; already-damaged slightly preferred so tip clears
                    float score = -along * 2f + side * 2.2f - (int)state * 0.15f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = new Vector2Int(tx, ty);
                        found = true;
                    }
                }

                if (!found) break;
                _world.DamageTerrain(best.x, best.y);
                left--;
            }
        }

        static bool PointInWedge(Vector3 p, Vector3 tipBase, Vector3 tipEnd, Vector3 perp, float halfWidthAtBase)
        {
            Vector3 axis = tipEnd - tipBase;
            float len = axis.magnitude;
            if (len < 0.001f) return false;
            Vector3 dir = axis / len;
            float along = Vector3.Dot(p - tipBase, dir);
            if (along < -0.02f || along > len + 0.02f) return false;
            // Width tapers to a point at tipEnd (spids)
            float t = Mathf.Clamp01(along / len);
            float half = Mathf.Lerp(halfWidthAtBase, 0.04f, t);
            float side = Mathf.Abs(Vector3.Dot(p - tipBase, perp));
            return side <= half;
        }

        bool ClearForBody(Vector3 pos)
        {
            // Body needs excavated cells under a small disk
            float r = bodyRadius * 0.85f;
            var c = _world.WorldToTerrain(pos);
            int rad = 2;
            for (int dy = -rad; dy <= rad; dy++)
            for (int dx = -rad; dx <= rad; dx++)
            {
                int tx = c.x + dx, ty = c.y + dy;
                if (!_world.InTerrainBounds(tx, ty)) return false;
                Vector3 tc = _world.TerrainCellCenter(tx, ty);
                if ((tc - pos).sqrMagnitude > r * r) continue;
                if (_world.GetTerrain(tx, ty) != TerrainState.Excavated) return false;
            }
            return true;
        }
    }
}
