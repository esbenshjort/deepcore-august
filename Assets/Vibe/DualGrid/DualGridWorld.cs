using System;
using UnityEngine;

namespace DeepCore.DualGrid
{
    public enum TerrainState : byte
    {
        Solid = 0,
        Damage1 = 1,
        Damage2 = 2,
        Damage3 = 3,
        Damage4 = 4,
        Excavated = 5,
    }

    /// <summary>
    /// Dual grid: coarse navigation (1×1 unit) + fine terrain (5×5 per nav cell).
    /// </summary>
    public sealed class DualGridWorld
    {
        public const int TerrainPerNav = 5;

        public int NavWidth { get; }
        public int NavHeight { get; }
        public int TerrainWidth => NavWidth * TerrainPerNav;
        public int TerrainHeight => NavHeight * TerrainPerNav;

        readonly TerrainState[] _terrain;
        int _batchDepth;
        bool _batchDirty;
        public event Action Changed;

        public DualGridWorld(int navWidth, int navHeight)
        {
            NavWidth = navWidth;
            NavHeight = navHeight;
            _terrain = new TerrainState[TerrainWidth * TerrainHeight];
            for (int i = 0; i < _terrain.Length; i++)
                _terrain[i] = TerrainState.Solid;
        }

        public void BeginBatch() => _batchDepth++;

        public void EndBatch()
        {
            _batchDepth = Mathf.Max(0, _batchDepth - 1);
            if (_batchDepth == 0 && _batchDirty)
            {
                _batchDirty = false;
                Changed?.Invoke();
            }
        }

        void Notify()
        {
            if (_batchDepth > 0) { _batchDirty = true; return; }
            Changed?.Invoke();
        }

        public void ResetAllSolid()
        {
            for (int i = 0; i < _terrain.Length; i++)
                _terrain[i] = TerrainState.Solid;
            Notify();
        }

        public bool InNavBounds(int nx, int ny) =>
            nx >= 0 && ny >= 0 && nx < NavWidth && ny < NavHeight;

        public bool InTerrainBounds(int tx, int ty) =>
            tx >= 0 && ty >= 0 && tx < TerrainWidth && ty < TerrainHeight;

        public TerrainState GetTerrain(int tx, int ty) =>
            InTerrainBounds(tx, ty) ? _terrain[Index(tx, ty)] : TerrainState.Solid;

        public void DamageTerrain(int tx, int ty)
        {
            if (!InTerrainBounds(tx, ty)) return;
            var s = _terrain[Index(tx, ty)];
            if (s >= TerrainState.Excavated) return;
            _terrain[Index(tx, ty)] = (TerrainState)(s + 1);
            Notify();
        }

        public void ExcavateTerrain(int tx, int ty)
        {
            if (!InTerrainBounds(tx, ty)) return;
            if (_terrain[Index(tx, ty)] == TerrainState.Excavated) return;
            _terrain[Index(tx, ty)] = TerrainState.Excavated;
            Notify();
        }

        public void ExcavateNavCell(int nx, int ny)
        {
            if (!InNavBounds(nx, ny)) return;
            BeginBatch();
            int x0 = nx * TerrainPerNav;
            int y0 = ny * TerrainPerNav;
            for (int dy = 0; dy < TerrainPerNav; dy++)
            for (int dx = 0; dx < TerrainPerNav; dx++)
                _terrain[Index(x0 + dx, y0 + dy)] = TerrainState.Excavated;
            EndBatch();
        }

        /// <summary>Excavate one remaining solid/damaged cell in this nav cell. Returns false if already clear.</summary>
        public bool ExcavateNextInNavCell(int nx, int ny)
        {
            if (!InNavBounds(nx, ny)) return false;
            int x0 = nx * TerrainPerNav;
            int y0 = ny * TerrainPerNav;
            for (int dy = 0; dy < TerrainPerNav; dy++)
            for (int dx = 0; dx < TerrainPerNav; dx++)
            {
                int i = Index(x0 + dx, y0 + dy);
                if (_terrain[i] == TerrainState.Excavated) continue;
                _terrain[i] = TerrainState.Excavated;
                Notify();
                return true;
            }
            return false;
        }

        public int CountUnexcavatedInNav(int nx, int ny)
        {
            if (!InNavBounds(nx, ny)) return 25;
            int x0 = nx * TerrainPerNav;
            int y0 = ny * TerrainPerNav;
            int c = 0;
            for (int dy = 0; dy < TerrainPerNav; dy++)
            for (int dx = 0; dx < TerrainPerNav; dx++)
            {
                if (_terrain[Index(x0 + dx, y0 + dy)] != TerrainState.Excavated) c++;
            }
            return c;
        }

        public bool IsNavWalkable(int nx, int ny)
        {
            if (!InNavBounds(nx, ny)) return false;
            return CountUnexcavatedInNav(nx, ny) == 0;
        }

        public Vector3 NavCellCenter(int nx, int ny) =>
            new(nx + 0.5f, ny + 0.5f, 0f);

        public Vector3 TerrainCellCenter(int tx, int ty)
        {
            float s = 1f / TerrainPerNav;
            return new Vector3((tx + 0.5f) * s, (ty + 0.5f) * s, 0f);
        }

        public Vector2Int WorldToTerrain(Vector3 world)
        {
            int tx = Mathf.FloorToInt(world.x * TerrainPerNav);
            int ty = Mathf.FloorToInt(world.y * TerrainPerNav);
            return new Vector2Int(tx, ty);
        }

        public Vector2Int WorldToNav(Vector3 world) =>
            new(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));

        int Index(int tx, int ty) => ty * TerrainWidth + tx;
    }
}
