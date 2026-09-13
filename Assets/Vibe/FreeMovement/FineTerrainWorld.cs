using System;
using System.Collections.Generic;
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
        Diamond = 3,
    }

    public struct TerrainCell
    {
        public TerrainPhase Phase;
        public TerrainMaterial Material;
        public byte Durability;
        public byte MaxDurability;
        public byte Mass;
        public byte DamageState;
        /// <summary>Mining armor from material profile (see FineTerrainWorld.RockArmor / BedrockArmor).</summary>
        public byte Armor;
        /// <summary>Full mining HP for this tile type. Per-tile CurrentHP lives in <see cref="Hp"/>.</summary>
        public byte MaxHp;
        /// <summary>Current mining HP — independent per tile (CurrentHP).</summary>
        public byte Hp;
        /// <summary>True after the excavator has rolled Weak Point for this tile (success or fail).</summary>
        public bool WeakPointChecked;
        /// <summary>True if Weak Point roll succeeded — extra armor pen while this tile lives.</summary>
        public bool HasWeakPoint;
        /// <summary>4 sockets × 2 bits each: Rock=0, Bedrock=1, Gold=2, Diamond=3.</summary>
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
        public int DiamondCount => Count(SocketKind.Diamond);
        public int BedrockCount => Count(SocketKind.Bedrock);
        public int RockCount => Count(SocketKind.Rock);

        /// <summary>Gold richness 0–4 (how many gold sockets).</summary>
        public byte GoldGrade => (byte)GoldCount;
        /// <summary>Diamond richness 0–4.</summary>
        public byte DiamondGrade => (byte)DiamondCount;

        public bool IsGoldOre => GoldCount > 0;
        public bool IsDiamondOre => DiamondCount > 0;
        public bool IsPreciousOre => GoldCount > 0 || DiamondCount > 0;

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

        public byte DiamondSockets
        {
            get
            {
                byte m = 0;
                for (int i = 0; i < 4; i++)
                    if (GetSocket(i) == SocketKind.Diamond)
                        m |= (byte)(1 << i);
                return m;
            }
        }

        public bool IsUndamageableBorder =>
            Material == TerrainMaterial.Bedrock && MaxDurability >= 254;

        /// <summary>True while CurrentHP remains.</summary>
        public bool HasMiningHp => Hp > 0;
    }

    public sealed class FineTerrainWorld
    {
        public const int SocketCount = 4;
        /// <summary>Rock / gold socket hit points. Bedrock is much harder.</summary>
        public const int RockSocketHardness = 3;
        public const int BedrockSocketHardness = 50;

        // Mining combat profiles — single source for rock / bedrock Armor + MaxHp
        public const byte RockArmor = 8;
        public const byte RockMaxHp = 20;
        public const byte BedrockArmor = 18;
        public const byte BedrockMaxHp = 70;

        /// <summary>Weak Point check DC for normal rock (D20 + Finesse).</summary>
        public const int RockWeakPointDC = 18;
        /// <summary>Weak Point check DC for bedrock (D20 + Finesse).</summary>
        public const int BedrockWeakPointDC = 24;

        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }

        TunnelNavGrid _navGrid;

        /// <summary>Shared tunnel walkability / clearance / A* foundation.</summary>
        public TunnelNavGrid Navigation => _navGrid ??= new TunnelNavGrid(this);

        readonly TerrainCell[] _cells;
        bool[] _gas;
        bool[] _gasRevealed;
        /// <summary>Gas cells that may show floor / FX (grows from breach mouth as you enter).</summary>
        bool[] _gasSeen;
        /// <summary>Collapse debris HP — &gt;0 blocks IsTunnelOpen / pathfinding until cleared.</summary>
        byte[] _debrisHp;
        /// <summary>
        /// Monotonic excavate generation per opened cell (0 = never opened).
        /// Early gens = settled tunnels; late gens = freshly cut rock.
        /// Presentation only — does not affect gameplay.
        /// </summary>
        readonly ushort[] _openGen;
        ushort _nextOpenGen = 1;

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
            _gas = new bool[width * height];
            _gasRevealed = new bool[width * height];
            _gasSeen = new bool[width * height];
            _openGen = new ushort[width * height];
            for (int i = 0; i < _cells.Length; i++)
                _cells[i] = MakeRock();
        }

        /// <summary>0 = freshly excavated, 1 = oldest settled open rock. Solid → 1.</summary>
        public float ExcavationAge01(int x, int y)
        {
            if (!InBounds(x, y)) return 1f;
            ushort g = _openGen[y * Width + x];
            if (g == 0) return 1f;
            float newest = Mathf.Max(1f, _nextOpenGen - 1f);
            return 1f - Mathf.Clamp01((g - 1f) / newest);
        }

        /// <summary>True after this cell was ever opened (still excavated or later filled).</summary>
        public bool WasOpened(int x, int y) =>
            InBounds(x, y) && _openGen[y * Width + x] > 0;

        void StampOpenGeneration(int x, int y)
        {
            int i = y * Width + x;
            if (_openGen[i] != 0) return;
            _openGen[i] = _nextOpenGen;
            if (_nextOpenGen < ushort.MaxValue)
                _nextOpenGen++;
        }

        public static byte PackSockets(SocketKind a, SocketKind b, SocketKind c, SocketKind d) =>
            (byte)(((int)a & 3) | (((int)b & 3) << 2) | (((int)c & 3) << 4) | (((int)d & 3) << 6));

        /// <summary>
        /// Build a cell from exactly 4 sockets (any mix of rock / bedrock / gold / diamond).
        /// Hardness = rock&amp;gold&amp;diamond×1 + bedrock×50. Full bedrock ≈ 200 digs.
        /// </summary>
        public static TerrainCell FromSockets(SocketKind s0, SocketKind s1, SocketKind s2, SocketKind s3)
        {
            byte packed = PackSockets(s0, s1, s2, s3);
            int hardness = 0;
            int mass = 0;
            int bedrock = 0;
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
                        break;
                    case SocketKind.Diamond:
                        hardness += RockSocketHardness;
                        mass += 2;
                        break;
                    default:
                        hardness += RockSocketHardness;
                        mass += 1;
                        break;
                }
            }

            // Border cells use 255; diggable bedrock tops out ~200
            hardness = Mathf.Clamp(hardness, 1, 250);
            mass = Mathf.Clamp(mass, 1, 16);

            var cell = new TerrainCell
            {
                Phase = TerrainPhase.Solid,
                Material = bedrock >= 2 ? TerrainMaterial.Bedrock : TerrainMaterial.Rock,
                Durability = (byte)hardness,
                MaxDurability = (byte)hardness,
                Mass = (byte)mass,
                DamageState = 0,
                Sockets = packed,
            };
            ApplyMiningProfile(ref cell);
            return cell;
        }

        /// <summary>
        /// Sets Armor / MaxHp / CurrentHP (Hp) from rock vs bedrock profile.
        /// Clears Weak Point state for a fresh tile.
        /// </summary>
        public static void ApplyMiningProfile(ref TerrainCell cell)
        {
            if (cell.Material == TerrainMaterial.Bedrock)
            {
                cell.Armor = BedrockArmor;
                cell.MaxHp = BedrockMaxHp;
                cell.Hp = BedrockMaxHp;
            }
            else
            {
                cell.Armor = RockArmor;
                cell.MaxHp = RockMaxHp;
                cell.Hp = RockMaxHp;
            }
            cell.WeakPointChecked = false;
            cell.HasWeakPoint = false;
        }

        public static int WeakPointDC(TerrainMaterial material) =>
            material == TerrainMaterial.Bedrock ? BedrockWeakPointDC : RockWeakPointDC;

        /// <summary>
        /// Record Weak Point check result on the tile. Idempotent if already checked.
        /// </summary>
        public bool TrySetWeakPoint(int x, int y, bool success)
        {
            if (!InBounds(x, y)) return false;
            ref var c = ref _cells[y * Width + x];
            if (c.WeakPointChecked) return false;
            if (c.Phase == TerrainPhase.Excavated || c.IsUndamageableBorder) return false;
            c.WeakPointChecked = true;
            c.HasWeakPoint = success;
            return true;
        }

        public static TerrainCell FromCounts(int rock, int bedrock, int gold, int diamond = 0)
        {
            rock = Mathf.Max(0, rock);
            bedrock = Mathf.Max(0, bedrock);
            gold = Mathf.Max(0, gold);
            diamond = Mathf.Max(0, diamond);
            int total = rock + bedrock + gold + diamond;
            if (total != 4)
            {
                if (total <= 0) return MakeRock();
                while (total < 4) { rock++; total++; }
                while (total > 4)
                {
                    if (rock > 0) { rock--; total--; }
                    else if (gold > 0) { gold--; total--; }
                    else if (diamond > 0) { diamond--; total--; }
                    else { bedrock--; total--; }
                }
            }

            var s = new SocketKind[4];
            int i = 0;
            for (int n = 0; n < rock; n++) s[i++] = SocketKind.Rock;
            for (int n = 0; n < bedrock; n++) s[i++] = SocketKind.Bedrock;
            for (int n = 0; n < gold; n++) s[i++] = SocketKind.Gold;
            for (int n = 0; n < diamond; n++) s[i++] = SocketKind.Diamond;
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
            c.Durability = 255;
            c.MaxDurability = 255;
            c.Mass = 12;
            ApplyMiningProfile(ref c);
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
            TryRevealGasNear(x, y);
            _navGrid?.NotifyTileChanged(x, y);
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

        /// <summary>
        /// Walkable tunnel after dig / breach. Sealed gas stays closed until revealed.
        /// Lit floor / gas FX use <see cref="IsFloorOpen"/> so unseen void stays dark.
        /// Damaged rock (CurrentHP &gt; 0) is never walkable — only fully excavated tiles.
        /// </summary>
        public bool IsTunnelOpen(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            ref readonly var c = ref _cells[y * Width + x];
            // Must be excavated AND finished (no remaining mining HP)
            if (c.Phase != TerrainPhase.Excavated || c.Hp > 0) return false;
            int i = y * Width + x;
            if (_gas != null && _gas[i] && (_gasRevealed == null || !_gasRevealed[i]))
                return false;
            if (_debrisHp != null && _debrisHp[i] > 0)
                return false;
            return true;
        }

        public bool HasBlockingDebris(int x, int y)
        {
            if (!InBounds(x, y) || _debrisHp == null) return false;
            return _debrisHp[y * Width + x] > 0;
        }

        public byte GetDebrisHp(int x, int y)
        {
            if (!InBounds(x, y) || _debrisHp == null) return 0;
            return _debrisHp[y * Width + x];
        }

        /// <summary>Place or update blocking collapse debris. hp==0 clears.</summary>
        public void SetBlockingDebris(int x, int y, byte hp)
        {
            if (!InBounds(x, y)) return;
            if (_debrisHp == null)
                _debrisHp = new byte[Width * Height];
            int i = y * Width + x;
            byte prev = _debrisHp[i];
            _debrisHp[i] = hp;
            if (prev != hp)
                Notify(x, y);
        }

        public void ClearBlockingDebris(int x, int y) => SetBlockingDebris(x, y, 0);

        /// <summary>True if this cell still blocks movement (solid, damaged, unfinished, or collapse debris).</summary>
        public bool IsMovementBlocker(int x, int y)
        {
            if (!InBounds(x, y)) return true;
            if (_debrisHp != null && _debrisHp[y * Width + x] > 0) return true;
            ref readonly var c = ref _cells[y * Width + x];
            if (c.Phase != TerrainPhase.Excavated) return true;
            return c.Hp > 0;
        }

        /// <summary>Count solid / unfinished cells overlapping a world-space circle.</summary>
        public int CountSolidInCircle(Vector2 center, float radius)
        {
            int x0 = Mathf.FloorToInt((center.x - radius) / CellSize);
            int x1 = Mathf.FloorToInt((center.x + radius) / CellSize);
            int y0 = Mathf.FloorToInt((center.y - radius) / CellSize);
            int y1 = Mathf.FloorToInt((center.y + radius) / CellSize);
            float r2 = radius * radius;
            int n = 0;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (!IsMovementBlocker(x, y)) continue;
                float cx0 = x * CellSize;
                float cy0 = y * CellSize;
                float closestX = Mathf.Clamp(center.x, cx0, cx0 + CellSize);
                float closestY = Mathf.Clamp(center.y, cy0, cy0 + CellSize);
                float dx = center.x - closestX;
                float dy = center.y - closestY;
                if (dx * dx + dy * dy < r2) n++;
            }
            return n;
        }

        /// <summary>
        /// Floor + lantern-lit cavity. Unseen gas (past the breach mouth) stays dark
        /// until a worker steps near it — stops lights/FX previewing the pocket through rock.
        /// </summary>
        public bool IsFloorOpen(int x, int y)
        {
            if (!IsTunnelOpen(x, y)) return false;
            if (IsGas(x, y) && (_gasSeen == null || !_gasSeen[y * Width + x]))
                return false;
            return true;
        }

        /// <summary>Blocks Light2D when shadows are on — includes walkable-but-unseen gas.</summary>
        public bool IsLightOccluder(int x, int y)
        {
            if (!InBounds(x, y)) return true;
            return !IsFloorOpen(x, y);
        }

        public bool IsSolid(int x, int y)
        {
            if (!InBounds(x, y)) return true;
            return !IsTunnelOpen(x, y);
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
            c.Hp = 0;
            StampOpenGeneration(x, y);
            if (notify) Notify(x, y);
            else { MarkDirtyCell(x, y); _dirty = true; }
        }

        /// <summary>
        /// Apply mining damage to this tile's CurrentHP (<see cref="TerrainCell.Hp"/>).
        /// Excavates when Hp reaches 0. Study soft may double the strike.
        /// </summary>
        public bool Damage(int x, int y, int amount = 1)
        {
            if (!InBounds(x, y)) return false;
            ref var c = ref _cells[y * Width + x];
            if (c.Phase == TerrainPhase.Excavated) return false;
            if (c.IsUndamageableBorder) return false;
            if (amount < 1) amount = 1;

            int strikes = 1;
            if (_studySoft != null)
            {
                int i = y * Width + x;
                if (_studySoft.TryGetValue(i, out byte bonus) && bonus > 0)
                {
                    strikes = 2;
                    bonus--;
                    if (bonus == 0) _studySoft.Remove(i);
                    else _studySoft[i] = bonus;
                }
            }

            for (int s = 0; s < strikes && c.Hp > 0; s++)
                c.Hp = (byte)Mathf.Max(0, c.Hp - amount);

            SyncDurabilityFromHp(ref c);
            c.Phase = c.Hp == 0 ? TerrainPhase.Excavated : TerrainPhase.Damaged;
            if (c.Phase == TerrainPhase.Excavated)
                StampOpenGeneration(x, y);
            Notify(x, y);
            return c.Phase == TerrainPhase.Excavated;
        }

        /// <summary>Keep cracked-rock visuals in sync with mining Hp.</summary>
        static void SyncDurabilityFromHp(ref TerrainCell c)
        {
            if (c.MaxHp <= 0 || c.Hp == 0)
            {
                c.Durability = 0;
                c.DamageState = c.MaxDurability;
                return;
            }

            float t = c.Hp / (float)c.MaxHp;
            c.Durability = (byte)Mathf.Clamp(
                Mathf.CeilToInt(c.MaxDurability * t), 1, c.MaxDurability);
            c.DamageState = (byte)(c.MaxDurability - c.Durability);
        }

        Dictionary<int, byte> _studySoft;

        /// <summary>Mark rock as studied — next digs deal extra damage (uncertain geology).</summary>
        public void MarkRockStudy(int x, int y, byte digBonus = 3)
        {
            if (!InBounds(x, y)) return;
            var c = Get(x, y);
            if (c.Phase == TerrainPhase.Excavated || c.IsUndamageableBorder) return;
            if (_studySoft == null) _studySoft = new Dictionary<int, byte>(64);
            int i = y * Width + x;
            int next = digBonus;
            if (_studySoft.TryGetValue(i, out byte cur)) next = Mathf.Min(8, cur + digBonus);
            _studySoft[i] = (byte)next;
        }

        public bool IsRockStudied(int x, int y) =>
            _studySoft != null && InBounds(x, y) && _studySoft.ContainsKey(y * Width + x);

        public bool IsGas(int x, int y) =>
            InBounds(x, y) && _gas != null && _gas[y * Width + x];

        public bool IsGasRevealed(int x, int y) =>
            IsGas(x, y) && _gasRevealed != null && _gasRevealed[y * Width + x];

        public bool IsGasSeen(int x, int y) =>
            IsGas(x, y) && _gasSeen != null && _gasSeen[y * Width + x];

        public void MarkGas(int x, int y, bool on = true)
        {
            if (!InBounds(x, y) || _gas == null) return;
            _gas[y * Width + x] = on;
            if (!on)
            {
                if (_gasRevealed != null) _gasRevealed[y * Width + x] = false;
                if (_gasSeen != null) _gasSeen[y * Width + x] = false;
            }
        }

        public void ClearGas()
        {
            if (_gas != null) Array.Clear(_gas, 0, _gas.Length);
            if (_gasRevealed != null) Array.Clear(_gasRevealed, 0, _gasRevealed.Length);
            if (_gasSeen != null) Array.Clear(_gasSeen, 0, _gasSeen.Length);
        }

        /// <summary>True when a gas cell touches non-gas open tunnel (pocket opened).</summary>
        public bool IsGasBreached(int x, int y)
        {
            if (!IsGas(x, y) || !IsExcavated(x, y)) return false;
            int[] ox = { 1, -1, 0, 0 };
            int[] oy = { 0, 0, 1, -1 };
            for (int i = 0; i < 4; i++)
            {
                int nx = x + ox[i], ny = y + oy[i];
                if (!InBounds(nx, ny)) continue;
                if (IsGas(nx, ny)) continue;
                // Real dig tunnel — not another sealed void
                if (IsExcavated(nx, ny)) return true;
            }
            return false;
        }

        void TryRevealGasNear(int x, int y)
        {
            if (_gas == null || _gasRevealed == null) return;
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int nx = x + ox, ny = y + oy;
                if (!IsGas(nx, ny) || IsGasRevealed(nx, ny)) continue;
                if (IsGasBreached(nx, ny))
                    RevealGasPocket(nx, ny);
            }
            // Fresh dig next to an already-breached pocket — expose the mouth
            SeedGasSeenNear(x, y);
        }

        public void RevealGasPocket(int sx, int sy)
        {
            if (!IsGas(sx, sy) || _gasRevealed == null) return;
            var q = new Queue<int>();
            int start = sy * Width + sx;
            if (_gasRevealed[start]) return;
            q.Enqueue(start);
            _gasRevealed[start] = true;
            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                int cx = cur % Width, cy = cur / Width;
                MarkDirtyCell(cx, cy);
                for (int i = 0; i < 4; i++)
                {
                    int nx = cx + (i == 0 ? 1 : i == 1 ? -1 : 0);
                    int ny = cy + (i == 2 ? 1 : i == 3 ? -1 : 0);
                    if (!IsGas(nx, ny)) continue;
                    int ni = ny * Width + nx;
                    if (_gasRevealed[ni]) continue;
                    _gasRevealed[ni] = true;
                    q.Enqueue(ni);
                }
            }

            // Only the breach mouth is lit/FX'd — rest of the void stays black until entered
            SeedGasSeenMouth();
            _dirty = true;
        }

        void SeedGasSeenMouth()
        {
            if (_gas == null || _gasRevealed == null || _gasSeen == null) return;
            for (int y = 1; y < Height - 1; y++)
            for (int x = 1; x < Width - 1; x++)
            {
                int i = y * Width + x;
                if (!_gas[i] || !_gasRevealed[i] || _gasSeen[i]) continue;
                if (TouchesNonGasTunnel(x, y))
                    MarkGasSeen(x, y);
            }
        }

        void SeedGasSeenNear(int x, int y)
        {
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int nx = x + ox, ny = y + oy;
                if (!IsGasRevealed(nx, ny) || IsGasSeen(nx, ny)) continue;
                if (TouchesNonGasTunnel(nx, ny))
                    MarkGasSeen(nx, ny);
            }
        }

        bool TouchesNonGasTunnel(int x, int y)
        {
            int[] ox = { 1, -1, 0, 0 };
            int[] oy = { 0, 0, 1, -1 };
            for (int i = 0; i < 4; i++)
            {
                int nx = x + ox[i], ny = y + oy[i];
                if (!InBounds(nx, ny)) continue;
                if (IsGas(nx, ny)) continue;
                if (IsExcavated(nx, ny)) return true;
            }
            return false;
        }

        void MarkGasSeen(int x, int y)
        {
            if (!IsGasRevealed(x, y) || _gasSeen == null) return;
            int i = y * Width + x;
            if (_gasSeen[i]) return;
            _gasSeen[i] = true;
            MarkDirtyCell(x, y);
            _dirty = true;
        }

        /// <summary>
        /// Expand visible gas around a worker/lamp — call while moving through a breached pocket.
        /// Unseen void stays black until you actually enter it.
        /// </summary>
        public void DiscoverGasAround(Vector2 worldPos, float radiusCells = 1.85f)
        {
            if (_gasSeen == null || _gasRevealed == null) return;
            var c = WorldToCell(worldPos);
            if (IsGasRevealed(c.x, c.y))
                MarkGasSeen(c.x, c.y);

            float r2 = radiusCells * radiusCells;
            int r = Mathf.CeilToInt(radiusCells);
            bool any = false;
            // Multi-pass so a step inward unlocks neighbors within the radius
            for (int pass = 0; pass < 3; pass++)
            {
                bool passAny = false;
                for (int oy = -r; oy <= r; oy++)
                for (int ox = -r; ox <= r; ox++)
                {
                    if (ox * ox + oy * oy > r2) continue;
                    int x = c.x + ox, y = c.y + oy;
                    if (!IsGasRevealed(x, y) || IsGasSeen(x, y)) continue;
                    if (!TouchesNonGasTunnel(x, y) && !TouchesSeenGas(x, y) && !TouchesFloorOpen(x, y))
                        continue;
                    MarkGasSeen(x, y);
                    passAny = true;
                    any = true;
                }
                if (!passAny) break;
            }

            if (any && _batch == 0)
            {
                FlushRegion();
                Changed?.Invoke();
            }
        }

        bool TouchesSeenGas(int x, int y)
        {
            int[] ox = { 1, -1, 0, 0 };
            int[] oy = { 0, 0, 1, -1 };
            for (int i = 0; i < 4; i++)
            {
                int nx = x + ox[i], ny = y + oy[i];
                if (IsGasSeen(nx, ny)) return true;
            }
            return false;
        }

        bool TouchesFloorOpen(int x, int y)
        {
            int[] ox = { 1, -1, 0, 0 };
            int[] oy = { 0, 0, 1, -1 };
            for (int i = 0; i < 4; i++)
            {
                int nx = x + ox[i], ny = y + oy[i];
                if (IsFloorOpen(nx, ny)) return true;
            }
            return false;
        }

        public void ClearRockStudy() => _studySoft?.Clear();

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
                if (!IsMovementBlocker(x, y)) continue;
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
            ClearGas();
            MarkAllDirty();
            _dirty = true;
            ClearRockStudy();
            EndBatch();
        }
    }
}
