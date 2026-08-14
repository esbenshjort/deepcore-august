using System;
using UnityEngine;

namespace DeepCore.Vibe
{
    /// <summary>
    /// Gameplay source of truth: square rock/floor cells.
    /// Visuals are a separate concern (mesh / tilemap view).
    /// </summary>
    public sealed class CaveGrid : MonoBehaviour
    {
        public int Width { get; private set; }
        public int Height { get; private set; }

        public event Action Changed;

        bool[] _rock;
        GameObject[] _floorVisuals;
        Transform _floorRoot;
        Sprite _floorSprite;

        public void Initialize(string[] map, Transform floorRoot, Sprite floorSprite)
        {
            Height = map.Length;
            Width = map[0].Length;
            _floorRoot = floorRoot;
            _floorSprite = floorSprite;
            _rock = new bool[Width * Height];
            _floorVisuals = new GameObject[Width * Height];

            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                // ASCII row 0 = north = high world Y
                char ch = map[Height - 1 - y][x];
                _rock[Index(x, y)] = ch == '#';
            }

            RebuildFloorVisuals();
            Changed?.Invoke();
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool IsRock(int x, int y) => InBounds(x, y) && _rock[Index(x, y)];
        public bool IsFloor(int x, int y) => InBounds(x, y) && !_rock[Index(x, y)];

        /// <summary>Out of bounds counts as solid rock (sealed mine).</summary>
        public bool IsRockOrEdge(int x, int y) => !InBounds(x, y) || _rock[Index(x, y)];

        public Vector3 CellToWorld(int x, int y)
        {
            float wx = x - (Width - 1) * 0.5f;
            float wy = y - (Height - 1) * 0.5f;
            return new Vector3(wx, wy, 0f);
        }

        public Vector2 CellCornerWorld(int cornerX, int cornerY)
        {
            // Corners sit on the dual grid: (0,0)..(Width,Height)
            float wx = cornerX - Width * 0.5f;
            float wy = cornerY - Height * 0.5f;
            return new Vector2(wx, wy);
        }

        public Vector2Int WorldToCell(Vector3 world)
        {
            int x = Mathf.RoundToInt(world.x + (Width - 1) * 0.5f);
            int y = Mathf.RoundToInt(world.y + (Height - 1) * 0.5f);
            return new Vector2Int(x, y);
        }

        public bool TryExcavate(int x, int y)
        {
            if (!IsRock(x, y)) return false;
            _rock[Index(x, y)] = false;
            RebuildFloorAt(x, y);
            Changed?.Invoke();
            return true;
        }

        public GameObject GetCellVisual(int x, int y)
        {
            if (!InBounds(x, y)) return null;
            return _floorVisuals[Index(x, y)];
        }

        public bool CanFitUnit(int cx, int cy, int footprint = 3)
        {
            int half = footprint / 2;
            for (int dy = -half; dy <= half; dy++)
            for (int dx = -half; dx <= half; dx++)
            {
                if (!IsFloor(cx + dx, cy + dy)) return false;
            }
            return true;
        }

        public bool IsAdjacentToFootprint(int rockX, int rockY, int cx, int cy, int footprint = 3)
        {
            int half = footprint / 2;
            for (int dy = -half; dy <= half; dy++)
            for (int dx = -half; dx <= half; dx++)
            {
                int fx = cx + dx;
                int fy = cy + dy;
                if (Mathf.Abs(fx - rockX) + Mathf.Abs(fy - rockY) == 1)
                    return true;
            }
            return false;
        }

        void RebuildFloorVisuals()
        {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                RebuildFloorAt(x, y);
        }

        void RebuildFloorAt(int x, int y)
        {
            int i = Index(x, y);
            if (_floorVisuals[i] != null)
            {
                Destroy(_floorVisuals[i]);
                _floorVisuals[i] = null;
            }

            if (_rock[i]) return;

            var go = new GameObject("Floor");
            go.transform.SetParent(_floorRoot);
            go.transform.position = CellToWorld(x, y);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _floorSprite;
            sr.sortingOrder = 0;
            _floorVisuals[i] = go;
        }

        int Index(int x, int y) => y * Width + x;
    }
}
