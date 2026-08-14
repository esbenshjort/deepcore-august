using System;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    public enum TerrainMaterial : byte { Rock = 0, Bedrock = 1 }

    public enum TerrainPhase : byte
    {
        Solid = 0,
        Damaged = 1,
        Excavated = 2,
    }

    /// <summary>Contents of one of the four sockets in a terrain cell.</summary>
    public enum SocketKind : byte
    {
        Rock = 0,
        Bedrock = 1,
        Gold = 2,
    }

    public struct TerrainCell
    {
        public TerrainPhase Phase;
        public TerrainMaterial Material;
        public byte Durability;
        public byte MaxDurability;
        public byte Mass;
        public byte DamageState;
        /// <summary>4 sockets × 2 bits each: Rock=0, Bedrock=1, Gold=2.</summary>
        public byte Sockets;

        public SocketKind GetSocket(int index) =>
            (SocketKind)((Sockets >> (index * 2)) & 0b11);

        public int Count(SocketKind kind)
        {
            int n = 0;
            for (int i = 0; i < 4; i++)
                if (GetSocket(i) == kind) n++;
            return n;
        }

        public int GoldCount => Count(SocketKind.Gold);
        public int BedrockCount => Count(SocketKind.Bedrock);
        public int RockCount => Count(SocketKind.Rock);

        /// <summary>Gold richness 0–4 (how many gold sockets).</summary>
        public byte GoldGrade => (byte)GoldCount;

        /// <summary>Bitmask of which sockets hold gold (debug / UI).</summary>
        public byte GoldSockets
        {
            get
            {
                byte m = 0;
                for (int i = 0; i < 4; i++)
                    if (GetSocket(i) == SocketKind.Gold)
                        m |= (byte)(1 << i);
                return m;
            }
        }

        public bool IsUndamageableBorder =>
            Material == TerrainMaterial.Bedrock && MaxDurability > 12;
    }

    public sealed class FineTerrainWorld
    {
        public const int SocketCount = 4;
        /// <summary>Rock / gold socket hit points. Bedrock sockets are 3× this.</summary>
        public const int RockSocketHardness = 1;
        public const int BedrockSocketHardness = 3;

        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }

        readonly TerrainCell[] _cells;
        public event Action Changed;
        /// <summary>Inclusive dirty cell rect after a change batch.</summary>
        public event Action<int, int, int, int> RegionChanged;

        int _batch;
        bool _dirty;
        int _dx0, _dy0, _dx1, _dy1;
        bool _hasRegion;

        public FineTerrainWorld(int width, int height, float cellSize)
        {
            Width = width;
            Height = height;
            CellSize = cellSize;
            _cells = new TerrainCell[width * height];
            for (int i = 0; i < _cells.Length; i++)
                _cells[i] = MakeRock();
        }

        public static byte PackSockets(SocketKind a, SocketKind b, SocketKind c, SocketKind d) =>
            (byte)(((int)a & 3) | (((int)b & 3) << 2) | (((int)c & 3) << 4) | (((int)d & 3) << 6));

        /// <summary>
        /// Build a cell from exactly 4 sockets (any mix of rock / bedrock / gold).
        /// Hardness = rock&amp;gold×1 + bedrock×3. Mass follows the same mix.
        /// </summary>
        public static TerrainCell FromSockets(SocketKind s0, SocketKind s1, SocketKind s2, SocketKind s3)
        {
            byte packed = PackSockets(s0, s1, s2, s3);
            int hardness = 0;
            int mass = 0;
            int bedrock = 0;
            int gold = 0;
            Span<SocketKind> sockets = stackalloc SocketKind[4] { s0, s1, s2, s3 };
            for (int i = 0; i < 4; i++)
            {
                switch (sockets[i])
                {
                    case SocketKind.Bedrock:
                        hardness += BedrockSocketHardness;
                        mass += 3;
                        bedrock++;
                        break;
                    case SocketKind.Gold:
                        hardness += RockSocketHardness;
                        mass += 2;
                        gold++;
                        break;
                    default:
                        hardness += RockSocketHardness;
                        mass += 1;
                        break;
                }
            }

            hardness = Mathf.Clamp(hardness, 1, 12);
            mass = Mathf.Clamp(mass, 1, 16);

            return new TerrainCell
            {
                Phase = TerrainPhase.Solid,
                Material = bedrock >= 2 ? TerrainMaterial.Bedrock : TerrainMaterial.Rock,
                Durability = (byte)hardness,
                MaxDurability = (byte)hardness,
                Mass = (byte)mass,
                DamageState = 0,
                Sockets = packed,
            };
        }

        public static TerrainCell FromCounts(int rock, int bedrock, int gold)
        {
            rock = Mathf.Max(0, rock);
            bedrock = Mathf.Max(0, bedrock);
            gold = Mathf.Max(0, gold);
            int total = rock + bedrock + gold;
            if (total != 4)
            {
                // Normalize / pad with rock
                if (total <= 0) return MakeRock();
                while (total < 4) { rock++; total++; }
                while (total > 4)
                {
                    if (rock > 0) { rock--; total--; }
                    else if (gold > 0) { gold--; total--; }
                    else { bedrock--; total--; }
                }
            }

            var s = new SocketKind[4];
            int i = 0;
            for (int n = 0; n < rock; n++) s[i++] = SocketKind.Rock;
            for (int n = 0; n < bedrock; n++) s[i++] = SocketKind.Bedrock;
            for (int n = 0; n < gold; n++) s[i++] = SocketKind.Gold;
            return FromSockets(s[0], s[1], s[2], s[3]);
        }

        public static TerrainCell MakeRock(byte _durability = 3, byte _mass = 4, byte _legacySockets = 0, byte goldCount = 0)
        {
            int g = Mathf.Clamp(goldCount, 0, 4);
            if (g == 0 && _legacySockets > 0)
            {
                // Old bitmask callers: treat set bits as gold sockets
                g = 0;
                for (int i = 0; i < 4; i++)
                    if ((_legacySockets & (1 << i)) != 0) g++;
                g = Mathf.Clamp(g, 1, 4);
            }
            return FromCounts(4 - g, 0, g);
        }

        /// <summary>Indestructible map border — 4× bedrock, undamageable.</summary>
        public static TerrainCell MakeBedrock(byte _ = 8)
        {
            var c = FromSockets(SocketKind.Bedrock, SocketKind.Bedrock, SocketKind.Bedrock, SocketKind.Bedrock);
            c.Material = TerrainMaterial.Bedrock;
            c.Durability = 24;
            c.MaxDurability = 24;
            c.Mass = 12;
            return c;
        }

        /// <summary>Diggable hard rock — mostly bedrock sockets (3× hardness each).</summary>
        public static TerrainCell MakeHardRock(int bedrockSockets = 3, int goldSockets = 0)
        {
            bedrockSockets = Mathf.Clamp(bedrockSockets, 1, 4);
            goldSockets = Mathf.Clamp(goldSockets, 0, 4 - bedrockSockets);
            return FromCounts(4 - bedrockSockets - goldSockets, bedrockSockets, goldSockets);
        }

        public void BeginBatch() => _batch++;

        public void EndBatch()
        {
            _batch = Mathf.Max(0, _batch - 1);
            if (_batch == 0 && _dirty)
            {
                _dirty = false;
                FlushRegion();
                Changed?.Invoke();
            }
        }

        void MarkDirtyCell(int x, int y)
        {
            if (!_hasRegion)
            {
                _dx0 = _dx1 = x;
                _dy0 = _dy1 = y;
                _hasRegion = true;
                return;
            }
            if (x < _dx0) _dx0 = x;
            if (y < _dy0) _dy0 = y;
            if (x > _dx1) _dx1 = x;
            if (y > _dy1) _dy1 = y;
        }

        void MarkAllDirty()
        {
            _dx0 = 0;
            _dy0 = 0;
            _dx1 = Width - 1;
            _dy1 = Height - 1;
            _hasRegion = true;
        }

        void FlushRegion()
        {
            if (!_hasRegion) return;
            RegionChanged?.Invoke(_dx0, _dy0, _dx1, _dy1);
            _hasRegion = false;
        }

        void Notify(int x, int y)
        {
            MarkDirtyCell(x, y);
            if (_batch > 0) { _dirty = true; return; }
            FlushRegion();
            Changed?.Invoke();
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public TerrainCell Get(int x, int y) =>
            InBounds(x, y) ? _cells[y * Width + x] : MakeBedrock();

        public void Set(int x, int y, TerrainCell cell)
        {
            if (!InBounds(x, y)) return;
            _cells[y * Width + x] = cell;
            Notify(x, y);
        }

        public bool IsSolid(int x, int y)
        {
            if (!InBounds(x, y)) return true;
            return _cells[y * Width + x].Phase != TerrainPhase.Excavated;
        }

        public bool IsExcavated(int x, int y) =>
            InBounds(x, y) && _cells[y * Width + x].Phase == TerrainPhase.Excavated;

        public Vector2 CellCenter(int x, int y) =>
            new((x + 0.5f) * CellSize, (y + 0.5f) * CellSize);

        public Vector2Int WorldToCell(Vector2 world) =>
            new(Mathf.FloorToInt(world.x / CellSize), Mathf.FloorToInt(world.y / CellSize));

        public Vector2 WorldSize => new(Width * CellSize, Height * CellSize);

        public void FillRect(int x0, int y0, int w, int h, TerrainCell cell)
        {
            BeginBatch();
            for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                if (!InBounds(x, y)) continue;
                _cells[y * Width + x] = cell;
                MarkDirtyCell(x, y);
            }
            _dirty = true;
            EndBatch();
        }

        public void ExcavateRect(int x0, int y0, int w, int h)
        {
            BeginBatch();
            for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                InstantExcavate(x, y, notify: false);
            EndBatch();
        }

        public void InstantExcavate(int x, int y, bool notify = true)
        {
            if (!InBounds(x, y)) return;
            ref var c = ref _cells[y * Width + x];
            if (c.Phase == TerrainPhase.Excavated) return;
            c.Phase = TerrainPhase.Excavated;
            c.Durability = 0;
            c.DamageState = c.MaxDurability;
            if (notify) Notify(x, y);
            else { MarkDirtyCell(x, y); _dirty = true; }
        }

        public bool Damage(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            ref var c = ref _cells[y * Width + x];
            if (c.Phase == TerrainPhase.Excavated) return false;
            if (c.IsUndamageableBorder) return false;

            if (c.Durability > 0) c.Durability--;
            c.DamageState = (byte)(c.MaxDurability - c.Durability);
            c.Phase = c.Durability == 0 ? TerrainPhase.Excavated : TerrainPhase.Damaged;
            Notify(x, y);
            return c.Phase == TerrainPhase.Excavated;
        }

        public bool CircleHitsSolid(Vector2 center, float radius)
        {
            int x0 = Mathf.FloorToInt((center.x - radius) / CellSize);
            int x1 = Mathf.FloorToInt((center.x + radius) / CellSize);
            int y0 = Mathf.FloorToInt((center.y - radius) / CellSize);
            int y1 = Mathf.FloorToInt((center.y + radius) / CellSize);
            float r2 = radius * radius;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (!IsSolid(x, y)) continue;
                float cx0 = x * CellSize;
                float cy0 = y * CellSize;
                float closestX = Mathf.Clamp(center.x, cx0, cx0 + CellSize);
                float closestY = Mathf.Clamp(center.y, cy0, cy0 + CellSize);
                float dx = center.x - closestX;
                float dy = center.y - closestY;
                if (dx * dx + dy * dy < r2) return true;
            }
            return false;
        }

        public void ResetAllSolidRock()
        {
            BeginBatch();
            for (int i = 0; i < _cells.Length; i++)
                _cells[i] = MakeRock();
            MarkAllDirty();
            _dirty = true;
            EndBatch();
        }
    }
}
