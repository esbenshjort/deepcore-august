using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Excavator digs a passable tunnel: full body-width clearance + sharp tip past the nose.
    /// Keeps chewing while blocked so it never stalls vibrating in a V-notch.
    /// </summary>
    public sealed class FreeWorkerController : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 1.35f;
        [SerializeField] float digMoveSpeed = 0.28f;
        [SerializeField] float rotateSpeed = 130f;
        [SerializeField] float digInterval = 0.28f;
        [SerializeField] int digsPerTick = 1;
        [SerializeField] int digsWhenStalled = 1;
        [SerializeField] float tipReach = 0.2f; // past the nose

        FineTerrainWorld _world;
        float _radius;       // visual / setup footprint
        float _moveRadius;   // collision — slightly tighter than dig so it fits
        float _digHalf;      // tunnel half-width (narrower than full footprint)
        Vector2 _goal;
        bool _hasGoal;
        float _digTimer;
        int _stallFrames;
        Transform _goalMarker;
        System.Action<int, int> _onBrokeCell;

        public float Radius => _radius;
        public bool IsActivelyDigging { get; private set; }
        /// <summary>Terrain-space forward (dig facing).</summary>
        public Vector2 Facing => transform.up;
        /// <summary>Approx visual drill tip in terrain space (for FX / eject).</summary>
        public Vector2 DrillTip(float ahead = 0.42f) =>
            Position + Facing * ahead;
        /// <summary>Terrain-space position (local to parent). Matches FineTerrainWorld coords.</summary>
        public Vector2 Position => transform.localPosition;
        public bool HasGoal => _hasGoal;

        public void Setup(FineTerrainWorld world, Vector2 start, float footprintRadius,
            Transform goalMarker = null, System.Action<int, int> onBrokeCell = null)
        {
            _world = world;
            _radius = footprintRadius;
            // Tunnel is deliberately tighter than the sprite footprint
            _digHalf = footprintRadius * 0.72f;
            _moveRadius = footprintRadius * 0.68f;
            transform.localPosition = start;
            _hasGoal = false;
            _stallFrames = 0;
            _goalMarker = goalMarker;
            _onBrokeCell = onBrokeCell;
            if (_goalMarker != null) _goalMarker.gameObject.SetActive(false);
        }

        public void SetGoal(Vector2 terrainPos)
        {
            if (_world == null) return;
            var size = _world.WorldSize;
            _goal = new Vector2(
                Mathf.Clamp(terrainPos.x, _moveRadius, size.x - _moveRadius),
                Mathf.Clamp(terrainPos.y, _moveRadius, size.y - _moveRadius));
            _hasGoal = true;
            _stallFrames = 0;
            if (_goalMarker != null)
            {
                _goalMarker.gameObject.SetActive(true);
                _goalMarker.localPosition = _goal;
            }
        }

        public void ClearGoal()
        {
            _hasGoal = false;
            _stallFrames = 0;
            if (_goalMarker != null) _goalMarker.gameObject.SetActive(false);
        }

        public FineTerrainWorld World => _world;
        public Vector2 Goal => _goal;

        public void Tick(Vector2 wasd) => Tick(wasd, clearGoalOnWasd: true);

        public void Tick(Vector2 wasd, bool clearGoalOnWasd)
        {
            if (_world == null) return;

            if (wasd.sqrMagnitude > 0.01f)
            {
                if (clearGoalOnWasd) ClearGoal();
                Step(wasd.normalized, moveSpeed, dig: true);
                return;
            }

            if (!_hasGoal) return;

            Vector2 pos = transform.localPosition;
            Vector2 to = _goal - pos;
            float dist = to.magnitude;
            if (dist < 0.08f)
            {
                ClearGoal();
                return;
            }

            // Keep chewing toward pinpoint even when almost there but clipped by rock
            Step(to.normalized, digMoveSpeed, dig: true);
        }

        void Step(Vector2 dir, float speed, bool dig)
        {
            Face(dir);
            IsActivelyDigging = false;

            Vector2 pos = transform.localPosition;
            Vector2 tryPos = pos + dir * (speed * Time.deltaTime);
            bool canMove = !_world.CircleHitsSolid(tryPos, _moveRadius);

            if (dig)
            {
                bool urgent = _hasGoal && _stallFrames > 8;
                _digTimer -= Time.deltaTime;
                if (_digTimer <= 0f || urgent)
                {
                    _digTimer = urgent ? digInterval * 0.65f : digInterval;
                    int budget = _stallFrames > 12 ? digsWhenStalled : digsPerTick;
                    _world.BeginBatch();
                    int dug = 0;
                    while (budget-- > 0)
                    {
                        if (!DigOneClearanceCell(pos, dir)) break;
                        dug++;
                        IsActivelyDigging = true;
                    }
                    // Single unstick chew — slow and deliberate
                    if (_stallFrames > 10 && dug == 0)
                    {
                        if (DigAnyBlocking(pos, dir))
                            IsActivelyDigging = true;
                    }
                    _world.EndBatch();

                    tryPos = pos + dir * (speed * Time.deltaTime);
                    canMove = !_world.CircleHitsSolid(tryPos, _moveRadius);
                }
                else if (!canMove)
                {
                    // Pressed against rock waiting for next dig tick
                    IsActivelyDigging = _stallFrames > 0;
                }
            }

            if (canMove)
            {
                _stallFrames = 0;
                Vector2 next = pos;
                Vector2 delta = tryPos - pos;
                TryMove(ref next, new Vector2(delta.x, 0f));
                TryMove(ref next, new Vector2(0f, delta.y));
                if (!_world.CircleHitsSolid(tryPos, _moveRadius))
                    next = tryPos;
                transform.localPosition = Vector2.Lerp(pos, next, 0.92f);
            }
            else
            {
                _stallFrames++;
            }
        }

        void Face(Vector2 dir)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.Euler(0, 0, ang), rotateSpeed * Time.deltaTime);
        }

        void TryMove(ref Vector2 pos, Vector2 delta)
        {
            Vector2 next = pos + delta;
            if (!_world.CircleHitsSolid(next, _moveRadius))
                pos = next;
        }

        /// <summary>
        /// Narrow clearance: dig half-width ≈ 72% of footprint, sharp tip.
        /// </summary>
        bool DigOneClearanceCell(Vector2 pos, Vector2 dir)
        {
            float cs = _world.CellSize;
            Vector2 perp = new(-dir.y, dir.x);
            float maxAlong = _digHalf + tipReach + cs * 0.5f;
            float maxSide = _digHalf + cs * 0.5f;

            int x0 = Mathf.FloorToInt((pos.x - maxSide) / cs) - 1;
            int x1 = Mathf.FloorToInt((pos.x + maxSide) / cs) + 1;
            int y0 = Mathf.FloorToInt((pos.y - maxSide) / cs) - 1;
            int y1 = Mathf.FloorToInt((pos.y + maxSide) / cs) + 1;

            int bestX = -1, bestY = -1;
            float bestScore = float.MaxValue;

            for (int ty = y0; ty <= y1; ty++)
            for (int tx = x0; tx <= x1; tx++)
            {
                if (!_world.InBounds(tx, ty)) continue;
                var cell = _world.Get(tx, ty);
                if (cell.Phase == TerrainPhase.Excavated) continue;
                if (cell.IsUndamageableBorder) continue;

                Vector2 c = _world.CellCenter(tx, ty);
                Vector2 d = c - pos;
                float along = Vector2.Dot(d, dir);
                if (along < -cs * 0.35f) continue;
                if (along > maxAlong) continue;

                float side = Mathf.Abs(Vector2.Dot(d, perp));
                float allowedHalf = AllowedHalfWidth(along);
                if (side > allowedHalf + cs * 0.12f) continue;

                bool overlapsBody = CellOverlapsCircle(tx, ty, pos, _moveRadius);
                bool overlapsNext = CellOverlapsCircle(tx, ty, pos + dir * (cs * 0.7f), _moveRadius);

                float score;
                if (overlapsBody || overlapsNext)
                    score = side * 2.4f + along * 0.12f - cell.DamageState * 0.2f;
                else
                    score = 100f + (-along * 2.2f) + side * 4f;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestX = tx;
                    bestY = ty;
                }
            }

            if (bestX < 0) return false;
            bool broke = _world.Damage(bestX, bestY);
            if (broke) _onBrokeCell?.Invoke(bestX, bestY);
            return true;
        }

        bool DigAnyBlocking(Vector2 pos, Vector2 dir)
        {
            float cs = _world.CellSize;
            float r = _moveRadius + cs * 0.25f;
            int x0 = Mathf.FloorToInt((pos.x - r) / cs) - 1;
            int x1 = Mathf.FloorToInt((pos.x + r) / cs) + 1;
            int y0 = Mathf.FloorToInt((pos.y - r) / cs) - 1;
            int y1 = Mathf.FloorToInt((pos.y + r) / cs) + 1;

            int bestX = -1, bestY = -1;
            float bestScore = float.MaxValue;
            for (int ty = y0; ty <= y1; ty++)
            for (int tx = x0; tx <= x1; tx++)
            {
                if (!_world.InBounds(tx, ty)) continue;
                var cell = _world.Get(tx, ty);
                if (cell.Phase == TerrainPhase.Excavated) continue;
                if (cell.IsUndamageableBorder) continue;
                if (!CellOverlapsCircle(tx, ty, pos, _moveRadius * 1.02f) &&
                    !CellOverlapsCircle(tx, ty, pos + dir * (cs * 0.6f), _moveRadius))
                    continue;

                Vector2 c = _world.CellCenter(tx, ty);
                float along = Vector2.Dot(c - pos, dir);
                float score = along * 0.2f + Vector2.Distance(c, pos) - cell.DamageState * 0.25f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestX = tx;
                    bestY = ty;
                }
            }

            if (bestX < 0) return false;
            bool broke = _world.Damage(bestX, bestY);
            if (broke) _onBrokeCell?.Invoke(bestX, bestY);
            return true;
        }

        float AllowedHalfWidth(float along)
        {
            // Narrow body corridor; sharp tip past the nose
            if (along <= _digHalf)
                return _digHalf;

            float t = Mathf.Clamp01((along - _digHalf) / Mathf.Max(0.001f, tipReach));
            t = t * t;
            return Mathf.Lerp(_digHalf, _world.CellSize * 0.28f, t);
        }

        bool CellOverlapsCircle(int tx, int ty, Vector2 center, float radius)
        {
            float cs = _world.CellSize;
            float cx0 = tx * cs;
            float cy0 = ty * cs;
            float closestX = Mathf.Clamp(center.x, cx0, cx0 + cs);
            float closestY = Mathf.Clamp(center.y, cy0, cy0 + cs);
            float dx = center.x - closestX;
            float dy = center.y - closestY;
            return dx * dx + dy * dy < radius * radius;
        }
    }
}
