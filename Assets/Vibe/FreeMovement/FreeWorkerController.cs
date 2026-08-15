using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Excavator digs a passable tunnel. Supports a queued dig route of pinpoints.
    /// </summary>
    public sealed class FreeWorkerController : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 1.35f;
        [SerializeField] float digMoveSpeed = 0.1f;
        [SerializeField] float rotateSpeed = 100f;
        [SerializeField] float digInterval = 0.95f;
        [SerializeField] int digsPerTick = 1;
        [SerializeField] int digsWhenStalled = 1;
        [SerializeField] float tipReach = 0.2f;

        FineTerrainWorld _world;
        float _radius;
        float _moveRadius;
        float _digHalf;
        Vector2 _goal;
        bool _hasGoal;
        float _digTimer;
        int _stallFrames;
        Transform _pinsRoot;
        LineRenderer _routeLine;
        Sprite _pinSprite;
        readonly List<Vector2> _route = new(16);
        readonly List<Transform> _pinVisuals = new(16);
        int _routeI;
        System.Action<int, int> _onBrokeCell;
        System.Action<TerrainCell, bool> _onDigImpact;

        public float Radius => _radius;
        public bool IsActivelyDigging { get; private set; }
        public Vector2 Facing => transform.up;
        public Vector2 DrillTip(float ahead = 0.42f) => Position + Facing * ahead;
        public Vector2 Position => transform.localPosition;
        public bool HasGoal => _hasGoal || _route.Count > 0;
        public int RouteCount => _route.Count;
        public FineTerrainWorld World => _world;
        public Vector2 Goal => _goal;

        /// <summary>Dig pins / route line — intended for scan view only.</summary>
        public void SetRouteVisible(bool visible)
        {
            if (_pinsRoot != null)
                _pinsRoot.gameObject.SetActive(visible);
        }

        public void Setup(FineTerrainWorld world, Vector2 start, float footprintRadius,
            Transform pinsRoot = null, System.Action<int, int> onBrokeCell = null,
            System.Action<TerrainCell, bool> onDigImpact = null, Sprite pinSprite = null)
        {
            _world = world;
            _radius = footprintRadius;
            _digHalf = footprintRadius * 0.72f;
            _moveRadius = footprintRadius * 0.68f;
            transform.localPosition = start;
            _hasGoal = false;
            _stallFrames = 0;
            _onBrokeCell = onBrokeCell;
            _onDigImpact = onDigImpact;
            _pinSprite = pinSprite;
            _pinsRoot = pinsRoot;
            EnsureRouteLine();
            ClearRoute();
        }

        void EnsureRouteLine()
        {
            if (_pinsRoot == null) return;
            if (_routeLine != null) return;
            var go = new GameObject("RouteLine");
            go.transform.SetParent(_pinsRoot, false);
            _routeLine = go.AddComponent<LineRenderer>();
            _routeLine.useWorldSpace = false;
            _routeLine.loop = false;
            _routeLine.widthMultiplier = 0.035f;
            _routeLine.numCapVertices = 2;
            _routeLine.sortingOrder = 34;
            var sh = Shader.Find("Sprites/Default");
            if (sh != null) _routeLine.material = new Material(sh);
            _routeLine.startColor = _routeLine.endColor = new Color(0.35f, 1f, 0.55f, 0.45f);
            _routeLine.positionCount = 0;
        }

        /// <summary>LMB append pin. Shift+LMB replace route with one pin.</summary>
        public void AddPin(Vector2 terrainPos, bool replaceRoute)
        {
            if (_world == null) return;
            var size = _world.WorldSize;
            var p = new Vector2(
                Mathf.Clamp(terrainPos.x, _moveRadius, size.x - _moveRadius),
                Mathf.Clamp(terrainPos.y, _moveRadius, size.y - _moveRadius));

            // Snap to cell center — feels like placing a dig marker on the grid
            var cell = _world.WorldToCell(p);
            if (_world.InBounds(cell.x, cell.y))
                p = _world.CellCenter(cell.x, cell.y);

            if (replaceRoute)
            {
                _route.Clear();
                _routeI = 0;
            }
            _route.Add(p);
            RefreshVisuals();
            ActivateCurrentPin();
        }

        /// <summary>Legacy single-goal API — replaces route with one pin.</summary>
        public void SetGoal(Vector2 terrainPos) => AddPin(terrainPos, replaceRoute: true);

        public void UndoLastPin()
        {
            if (_route.Count == 0) return;
            // If we're mid-route, prefer popping unvisited pins first
            if (_route.Count > _routeI + 1)
                _route.RemoveAt(_route.Count - 1);
            else
            {
                _route.RemoveAt(_route.Count - 1);
                _routeI = Mathf.Max(0, _route.Count - 1);
            }
            RefreshVisuals();
            if (_route.Count == 0) ClearRoute();
            else ActivateCurrentPin();
        }

        public void ClearGoal() => ClearRoute();

        public void ClearRoute()
        {
            _route.Clear();
            _routeI = 0;
            _hasGoal = false;
            _stallFrames = 0;
            RefreshVisuals();
        }

        void ActivateCurrentPin()
        {
            if (_route.Count == 0)
            {
                _hasGoal = false;
                return;
            }
            _routeI = Mathf.Clamp(_routeI, 0, _route.Count - 1);
            _goal = _route[_routeI];
            _hasGoal = true;
            _stallFrames = 0;
            RefreshVisuals();
        }

        void AdvanceRoute()
        {
            _routeI++;
            if (_routeI >= _route.Count)
            {
                ClearRoute();
                return;
            }
            ActivateCurrentPin();
        }

        void RefreshVisuals()
        {
            EnsureRouteLine();
            while (_pinVisuals.Count < _route.Count)
            {
                var pin = new GameObject($"Pin{_pinVisuals.Count}").transform;
                pin.SetParent(_pinsRoot != null ? _pinsRoot : transform.parent, false);
                var sr = pin.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = _pinSprite;
                sr.sortingOrder = 36;
                DigVisualKit.ApplyLit(sr);
                pin.localScale = Vector3.one * 0.42f;
                _pinVisuals.Add(pin);
            }

            for (int i = 0; i < _pinVisuals.Count; i++)
            {
                bool on = i < _route.Count;
                _pinVisuals[i].gameObject.SetActive(on);
                if (!on) continue;
                _pinVisuals[i].localPosition = _route[i];
                var sr = _pinVisuals[i].GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                // Current = bright green; upcoming = dimmer; past = muted
                if (i < _routeI)
                    sr.color = new Color(0.35f, 0.55f, 0.4f, 0.35f);
                else if (i == _routeI)
                    sr.color = new Color(0.4f, 1f, 0.55f, 1f);
                else
                    sr.color = new Color(0.45f, 0.9f, 0.6f, 0.7f);
                _pinVisuals[i].localScale = Vector3.one * (i == _routeI ? 0.5f : 0.38f);
            }

            if (_routeLine == null) return;
            if (_route.Count == 0)
            {
                _routeLine.positionCount = 0;
                return;
            }

            // Path from excavator through remaining pins
            int remaining = _route.Count - _routeI;
            _routeLine.positionCount = remaining + 1;
            _routeLine.SetPosition(0, Position);
            for (int i = 0; i < remaining; i++)
                _routeLine.SetPosition(i + 1, _route[_routeI + i]);
            _routeLine.startColor = new Color(0.35f, 1f, 0.55f, 0.55f);
            _routeLine.endColor = new Color(0.35f, 1f, 0.55f, 0.2f);
        }

        public void Tick(Vector2 wasd) => Tick(wasd, clearGoalOnWasd: true);

        public void Tick(Vector2 wasd, bool clearGoalOnWasd)
        {
            if (_world == null) return;

            if (wasd.sqrMagnitude > 0.01f)
            {
                if (clearGoalOnWasd) ClearRoute();
                Step(wasd.normalized, moveSpeed, dig: true);
                return;
            }

            if (!_hasGoal) return;

            // Keep route line attached to excavator while moving
            if (_route.Count > 0 && _routeLine != null && _routeLine.positionCount > 0)
                _routeLine.SetPosition(0, Position);

            Vector2 pos = transform.localPosition;
            Vector2 to = _goal - pos;
            float dist = to.magnitude;
            if (dist < 0.1f)
            {
                AdvanceRoute();
                return;
            }

            Vector2 dir = to.normalized;
            // Cruise at full speed on open floor; only crawl when rock is in the way
            bool rockInPath = PathBlockedByRock(pos, dir);
            float speed = rockInPath || _stallFrames > 0 ? digMoveSpeed : moveSpeed;
            Step(dir, speed, dig: rockInPath || _stallFrames > 0);
        }

        bool PathBlockedByRock(Vector2 pos, Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return false;
            // Immediate next step
            if (_world.CircleHitsSolid(pos + dir * Mathf.Max(moveSpeed * Time.deltaTime, _moveRadius * 0.35f), _moveRadius))
                return true;
            // Tip / clearance zone — rock close enough that we should dig, not sprint
            float probe = _moveRadius + tipReach + _world.CellSize * 0.6f;
            if (_world.CircleHitsSolid(pos + dir * probe, _moveRadius * 0.55f))
                return true;
            return false;
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
                    for (int i = 0; i < budget; i++)
                    {
                        if (!DigOneClearanceCell(pos, dir)) break;
                        IsActivelyDigging = true;
                    }
                    if (!canMove)
                    {
                        for (int i = 0; i < budget; i++)
                        {
                            if (DigAnyBlocking(pos, dir))
                                IsActivelyDigging = true;
                            else break;
                        }
                    }
                }
            }

            canMove = !_world.CircleHitsSolid(tryPos, _moveRadius);
            if (canMove)
            {
                transform.localPosition = tryPos;
                _stallFrames = 0;
            }
            else
            {
                _stallFrames++;
                IsActivelyDigging = _stallFrames > 0;
            }
        }

        void Face(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation,
                Quaternion.Euler(0f, 0f, ang),
                rotateSpeed * Time.deltaTime);
        }

        bool DigOneClearanceCell(Vector2 pos, Vector2 dir)
        {
            float cs = _world.CellSize;
            Vector2 perp = new(-dir.y, dir.x);
            float maxAlong = _digHalf + tipReach + cs * 0.5f;
            float maxSide = _digHalf + cs * 0.5f;

            int x0 = Mathf.FloorToInt((pos.x - maxSide) / cs) - 1;
            int x1 = Mathf.FloorToInt((pos.x + maxSide) / cs) + 1;
            int y0 = Mathf.FloorToInt((pos.y - maxAlong) / cs) - 1;
            int y1 = Mathf.FloorToInt((pos.y + maxAlong) / cs) + 1;

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
            var before = _world.Get(bestX, bestY);
            bool broke = _world.Damage(bestX, bestY);
            _onDigImpact?.Invoke(before, broke);
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
            var before = _world.Get(bestX, bestY);
            bool broke = _world.Damage(bestX, bestY);
            _onDigImpact?.Invoke(before, broke);
            if (broke) _onBrokeCell?.Invoke(bestX, bestY);
            return true;
        }

        float AllowedHalfWidth(float along)
        {
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
            return dx * dx + dy * dy <= radius * radius;
        }
    }
}
