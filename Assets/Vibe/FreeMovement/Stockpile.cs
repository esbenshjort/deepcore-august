using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    public enum StockpileKind : byte
    {
        Rock = 0,
        Gold = 1,
        RefinedGold = 2,
        Dirt = 3,
        Diamond = 4,
        RefinedDiamond = 5,
    }

    /// <summary>Visual stockpile — input piles keep a queue of intact ore cells for the washer.</summary>
    public sealed class Stockpile : MonoBehaviour
    {
        public StockpileKind Kind;
        public float Mass;
        public int Piles;
        public int GoldSockets;
        public int DiamondSockets;
        public int GoldValue;
        public int Deliveries;
        public float HoverRadius = 0.55f;

        SpriteRenderer _sr;
        Transform _heap;
        readonly Queue<OreCell> _cells = new(128);

        public int CellCount => _cells.Count;
        public bool HasCells => _cells.Count > 0;

        public string Title => Kind switch
        {
            StockpileKind.Gold => "ORE GOLD",
            StockpileKind.Rock => "ORE ROCK",
            StockpileKind.RefinedGold => "REFINED GOLD",
            StockpileKind.Dirt => "DIRT ROCK",
            StockpileKind.Diamond => "ORE DIAMOND",
            StockpileKind.RefinedDiamond => "REFINED DIA",
            _ => "STOCKPILE",
        };

        public string HoverInfo
        {
            get
            {
                if (Kind is StockpileKind.RefinedGold or StockpileKind.RefinedDiamond)
                {
                    return
                        $"{Title}\n" +
                        $"pieces     {GoldValue}\n" +
                        $"deliveries {Deliveries}";
                }
                if (Kind == StockpileKind.Dirt)
                {
                    return
                        $"{Title}\n" +
                        $"pieces     {Piles}\n" +
                        $"mass       {Mass:0.#}\n" +
                        $"deliveries {Deliveries}";
                }
                if (Kind == StockpileKind.Gold)
                {
                    return
                        $"{Title}\n" +
                        $"cells      {CellCount}\n" +
                        $"ore sockets {GoldSockets}\n" +
                        $"mass       {Mass:0.#}\n" +
                        $"deliveries {Deliveries}";
                }
                if (Kind == StockpileKind.Diamond)
                {
                    return
                        $"{Title}\n" +
                        $"cells      {CellCount}\n" +
                        $"ore sockets {DiamondSockets}\n" +
                        $"mass       {Mass:0.#}\n" +
                        $"deliveries {Deliveries}";
                }
                return
                    $"{Title}\n" +
                    $"cells      {CellCount}\n" +
                    $"mass       {Mass:0.#}\n" +
                    $"deliveries {Deliveries}";
            }
        }

        public static Stockpile Spawn(Transform parent, Vector2 localPos, StockpileKind kind)
        {
            var go = new GameObject(kind.ToString() + "Stockpile");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var pad = new GameObject("Pad");
            pad.transform.SetParent(go.transform, false);
            var psr = pad.AddComponent<SpriteRenderer>();
            psr.sprite = YardVisualKit.StockpilePad(kind);
            psr.sortingOrder = 11;
            DigVisualKit.ApplyLit(psr);
            pad.transform.localScale = Vector3.one * 0.55f;

            var heap = new GameObject("Heap");
            heap.transform.SetParent(go.transform, false);
            heap.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            var hsr = heap.AddComponent<SpriteRenderer>();
            hsr.sprite = kind is StockpileKind.Gold or StockpileKind.RefinedGold
                ? DigVisualKit.GoldNugget
                : kind is StockpileKind.Diamond or StockpileKind.RefinedDiamond
                    ? DigVisualKit.DiamondCrystal
                    : DigVisualKit.RockPile;
            hsr.sortingOrder = 13;
            DigVisualKit.ApplyLit(hsr);
            heap.transform.localScale = Vector3.one * 0.15f;
            hsr.color = HeapColor(kind);

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(0f, -0.28f, 0f);
            var lsr = label.AddComponent<SpriteRenderer>();
            lsr.sprite = YardVisualKit.StockpileLabel(kind);
            lsr.sortingOrder = 14;
            DigVisualKit.ApplyLit(lsr);
            label.transform.localScale = Vector3.one * 0.38f;

            var s = go.AddComponent<Stockpile>();
            s.Kind = kind;
            s._sr = hsr;
            s._heap = heap.transform;
            s.RefreshVisual();
            return s;
        }

        static Color HeapColor(StockpileKind kind) => kind switch
        {
            StockpileKind.Gold => new Color(1f, 0.75f, 0.28f),
            StockpileKind.RefinedGold => new Color(1f, 0.92f, 0.45f),
            StockpileKind.Diamond => new Color(0.55f, 0.85f, 1f),
            StockpileKind.RefinedDiamond => new Color(0.85f, 0.95f, 1f),
            StockpileKind.Dirt => new Color(0.42f, 0.36f, 0.28f),
            _ => new Color(0.55f, 0.48f, 0.4f),
        };

        public void Reset()
        {
            Mass = 0f;
            Piles = 0;
            GoldSockets = 0;
            DiamondSockets = 0;
            GoldValue = 0;
            Deliveries = 0;
            _cells.Clear();
            RefreshVisual();
        }

        public void EnqueueOreCell(OreCell cell)
        {
            _cells.Enqueue(cell);
            Mass += Mathf.Max(0f, cell.Mass);
            Piles++;
            if (cell.IsGoldOre) GoldSockets += cell.GoldCount;
            if (cell.IsDiamondOre) DiamondSockets += cell.DiamondCount;
            Deliveries++;
            RefreshVisual();
        }

        public bool TryWithdrawCell(out OreCell cell)
        {
            if (_cells.Count == 0)
            {
                cell = default;
                return false;
            }
            cell = _cells.Dequeue();
            Mass = Mathf.Max(0f, Mass - cell.Mass);
            Piles = Mathf.Max(0, Piles - 1);
            if (cell.IsGoldOre) GoldSockets = Mathf.Max(0, GoldSockets - cell.GoldCount);
            if (cell.IsDiamondOre) DiamondSockets = Mathf.Max(0, DiamondSockets - cell.DiamondCount);
            RefreshVisual();
            return true;
        }

        public void DepositRock(float mass, int piles)
        {
            if (mass <= 0f && piles <= 0) return;
            int n = Mathf.Max(1, piles);
            float per = mass / n;
            for (int i = 0; i < n; i++)
            {
                EnqueueOreCell(new OreCell
                {
                    Sockets = PackAll(SocketKind.Rock),
                    Mass = per,
                });
            }
        }

        public void DepositGold(float mass, int sockets, int value, int piles)
        {
            if (piles <= 0 && value <= 0) return;
            int n = Mathf.Max(1, piles);
            float perMass = mass / n;
            int sockLeft = Mathf.Max(sockets, n);
            for (int i = 0; i < n; i++)
            {
                int g = i == n - 1 ? Mathf.Clamp(sockLeft, 1, 4) : Mathf.Clamp(sockLeft / (n - i), 1, 4);
                sockLeft -= g;
                EnqueueOreCell(new OreCell
                {
                    Sockets = PackKindCount(SocketKind.Gold, g),
                    Mass = perMass,
                });
            }
            _ = value;
        }

        public void DepositDiamond(float mass, int sockets, int value, int piles)
        {
            if (piles <= 0 && value <= 0) return;
            int n = Mathf.Max(1, piles);
            float perMass = mass / n;
            int sockLeft = Mathf.Max(sockets, n);
            for (int i = 0; i < n; i++)
            {
                int d = i == n - 1 ? Mathf.Clamp(sockLeft, 1, 4) : Mathf.Clamp(sockLeft / (n - i), 1, 4);
                sockLeft -= d;
                EnqueueOreCell(new OreCell
                {
                    Sockets = PackKindCount(SocketKind.Diamond, d),
                    Mass = perMass,
                });
            }
            _ = value;
        }

        public void DepositRefinedGold(int pieces)
        {
            if (pieces <= 0) return;
            GoldValue += pieces;
            GoldSockets += pieces;
            Piles += pieces;
            Deliveries++;
            Mass += pieces * 0.5f;
            RefreshVisual();
        }

        public void DepositRefinedDiamond(int pieces)
        {
            if (pieces <= 0) return;
            GoldValue += pieces; // reuse value counter for refined pieces
            DiamondSockets += pieces;
            Piles += pieces;
            Deliveries++;
            Mass += pieces * 0.4f;
            RefreshVisual();
        }

        public void DepositDirt(int pieces, float mass)
        {
            if (pieces <= 0) return;
            Piles += pieces;
            Mass += Mathf.Max(0f, mass);
            Deliveries++;
            RefreshVisual();
        }

        static byte PackAll(SocketKind k)
        {
            byte s = 0;
            for (int i = 0; i < 4; i++)
                s |= (byte)((int)k << (i * 2));
            return s;
        }

        static byte PackKindCount(SocketKind kind, int count)
        {
            count = Mathf.Clamp(count, 0, 4);
            byte s = 0;
            for (int i = 0; i < 4; i++)
            {
                var k = i < count ? kind : SocketKind.Rock;
                s |= (byte)((int)k << (i * 2));
            }
            return s;
        }

        public bool ContainsWorldPoint(Vector2 worldPoint)
        {
            Vector2 p = transform.position;
            return (worldPoint - p).sqrMagnitude <= HoverRadius * HoverRadius;
        }

        void RefreshVisual()
        {
            if (_heap == null || _sr == null) return;
            float t = Kind switch
            {
                StockpileKind.RefinedGold or StockpileKind.RefinedDiamond =>
                    Mathf.Clamp01(GoldValue / 30f),
                StockpileKind.Dirt => Mathf.Clamp01(Piles / 40f + Mass / 50f),
                StockpileKind.Gold or StockpileKind.Diamond =>
                    Mathf.Clamp01(CellCount / 20f + Mass / 40f),
                _ => Mathf.Clamp01(CellCount / 25f + Mass / 50f),
            };
            float scale = Mathf.Lerp(0.12f, 0.55f, Mathf.Sqrt(t));
            _heap.localScale = Vector3.one * scale;
            bool on = Kind is StockpileKind.RefinedGold or StockpileKind.RefinedDiamond
                ? GoldValue > 0
                : Kind == StockpileKind.Dirt
                    ? Piles > 0 || Mass > 0.01f
                    : CellCount > 0 || Mass > 0.01f;
            _heap.gameObject.SetActive(on);
            _sr.color = HeapColor(Kind);
        }
    }

    /// <summary>Expanded base — ore piles, washer, refined gold/diamond + dirt outputs.</summary>
    public sealed class BasecampYard
    {
        public Transform Root;
        public Vector2 DropPoint;
        public Stockpile Rock;
        public Stockpile Gold;
        public Stockpile Diamond;
        public Stockpile RefinedGold;
        public Stockpile RefinedDiamond;
        public Stockpile Dirt;
        public WashMachine Washer;

        public static BasecampYard Spawn(Transform parent, Vector2 center)
        {
            var yard = new BasecampYard();
            var root = new GameObject("Basecamp").transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;
            yard.Root = root;
            yard.DropPoint = center; // overwritten once wash bay is laid out

            var platform = new GameObject("Platform");
            platform.transform.SetParent(root, false);
            // Platform centers on the wash bay rather than empty pad space
            platform.transform.localPosition = center + new Vector2(0.15f, -0.35f);
            var psr = platform.AddComponent<SpriteRenderer>();
            psr.sprite = YardVisualKit.YardPlatform;
            psr.sortingOrder = 10;
            DigVisualKit.ApplyLit(psr);
            platform.transform.localScale = Vector3.one * 2.85f;

            // Compact wash bay — piles hug the washer instead of spanning the whole pad.
            //   [ROCK]  [GOLD]  [DIAMOND]     ← ore inputs (hauler / refiner pick)
            //         [WASHER]
            //   [DIRT]  [R.GOLD] [R.DIA]      ← wash outputs
            Vector2 wash = center + new Vector2(-0.05f, -0.35f);
            float xInL = -0.85f;
            float xInC = 0.15f;
            float xInR = 1.05f;
            float yIn = 0.58f;
            float yOut = -0.62f;

            yard.Rock = Stockpile.Spawn(root, wash + new Vector2(xInL, yIn), StockpileKind.Rock);
            yard.Gold = Stockpile.Spawn(root, wash + new Vector2(xInC, yIn), StockpileKind.Gold);
            yard.Diamond = Stockpile.Spawn(root, wash + new Vector2(xInR, yIn), StockpileKind.Diamond);
            yard.Dirt = Stockpile.Spawn(root, wash + new Vector2(xInL, yOut), StockpileKind.Dirt);
            yard.RefinedGold = Stockpile.Spawn(root, wash + new Vector2(xInC, yOut), StockpileKind.RefinedGold);
            yard.RefinedDiamond = Stockpile.Spawn(root, wash + new Vector2(xInR, yOut),
                StockpileKind.RefinedDiamond);

            yard.Washer = WashMachine.Spawn(root, wash,
                yard.RefinedGold, yard.Dirt, yard.RefinedDiamond);

            // Hauler drop aims at the ore-input bay in front of the washer
            yard.DropPoint = wash + new Vector2(xInC, yIn * 0.35f);

            return yard;
        }

        public void Reset()
        {
            Rock?.Reset();
            Gold?.Reset();
            Diamond?.Reset();
            Dirt?.Reset();
            RefinedGold?.Reset();
            RefinedDiamond?.Reset();
            Washer?.ResetMachine();
        }

        public Stockpile HitTest(Vector2 worldPoint)
        {
            if (RefinedDiamond != null && RefinedDiamond.ContainsWorldPoint(worldPoint)) return RefinedDiamond;
            if (RefinedGold != null && RefinedGold.ContainsWorldPoint(worldPoint)) return RefinedGold;
            if (Diamond != null && Diamond.ContainsWorldPoint(worldPoint)) return Diamond;
            if (Gold != null && Gold.ContainsWorldPoint(worldPoint)) return Gold;
            if (Dirt != null && Dirt.ContainsWorldPoint(worldPoint)) return Dirt;
            if (Rock != null && Rock.ContainsWorldPoint(worldPoint)) return Rock;
            return null;
        }
    }
}
