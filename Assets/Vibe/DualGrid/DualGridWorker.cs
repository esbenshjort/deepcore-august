using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.DualGrid
{
    /// <summary>
    /// 1×1 nav unit with smooth move + 8-directional pathfinding.
    /// </summary>
    public sealed class DualGridWorker : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 3.2f;

        DualGridWorld _world;
        Vector2Int _cell;
        readonly List<Vector2Int> _path = new();
        int _pathIndex;
        bool _moving;

        public Vector2Int Cell => _cell;
        public bool IsMoving => _moving;

        public void Setup(DualGridWorld world, Vector2Int start)
        {
            _world = world;
            _cell = start;
            transform.position = _world.NavCellCenter(start.x, start.y);
            _path.Clear();
            _moving = false;
        }

        public void CommandMoveTo(Vector2Int target)
        {
            if (_world == null) return;
            if (!DualGridPathfinder.FindPath(_world, _cell, target, walkableOnly: true, _path)) return;
            _pathIndex = 0;
            _moving = _path.Count > 0;
        }

        public void TryStep(Vector2Int dir)
        {
            if (_moving) return;
            var next = _cell + dir;
            if (!DualGridPathfinder.CanStep(_world, _cell, next, walkableOnly: true)) return;
            _path.Clear();
            _path.Add(next);
            _pathIndex = 0;
            _moving = true;
        }

        void Update()
        {
            if (!_moving || _world == null) return;
            if (_pathIndex >= _path.Count)
            {
                _moving = false;
                return;
            }

            Vector2Int next = _path[_pathIndex];
            Vector3 dest = _world.NavCellCenter(next.x, next.y);
            Vector3 delta = dest - transform.position;
            if (delta.sqrMagnitude > 0.00001f)
            {
                float ang = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, Quaternion.Euler(0, 0, ang), 360f * Time.deltaTime);
            }

            transform.position = Vector3.MoveTowards(transform.position, dest, moveSpeed * Time.deltaTime);
            if ((transform.position - dest).sqrMagnitude < 0.0001f)
            {
                transform.position = dest;
                _cell = next;
                _pathIndex++;
                if (_pathIndex >= _path.Count) _moving = false;
            }
        }
    }
}
