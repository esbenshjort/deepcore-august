using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    public enum StockpileKind : byte
    {
        Rock = 0,       // ore rock (input) — unwashed cells
        Gold = 1,       // ore gold (input) — unwashed cells with gold sockets
        RefinedGold = 2,
        Dirt = 3,
    }

    /// <summary>Visual stockpile — input piles keep a queue of intact ore cells for the washer.</summary>
    public sealed class Stockpile : MonoBehaviour
    {
        public StockpileKind Kind;
        public float Mass;
        public int Piles;
        public int GoldSockets;
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
            _ => "STOCKPILE",
        };

        public string HoverInfo
        {
            get
            {
                if (Kind == StockpileKind.RefinedGold)
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
            psr.sprite = MakePadSprite(kind);
            psr.sortingOrder = 11;
            DigVisualKit.ApplyLit(psr);
            pad.transform.localScale = Vector3.one * 0.7f;

            var heap = new GameObject("Heap");
            heap.transform.SetParent(go.transform, false);
            heap.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            var hsr = heap.AddComponent<SpriteRenderer>();
            hsr.sprite = kind is StockpileKind.Gold or StockpileKind.RefinedGold
                ? DigVisualKit.GoldNugget
                : DigVisualKit.RockPile;
            hsr.sortingOrder = 13;
            DigVisualKit.ApplyLit(hsr);
            heap.transform.localScale = Vector3.one * 0.15f;
            hsr.color = HeapColor(kind);

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(0f, -0.28f, 0f);
            var lsr = label.AddComponent<SpriteRenderer>();
            lsr.sprite = MakeLabelSprite(kind);
            lsr.sortingOrder = 14;
            DigVisualKit.ApplyLit(lsr);
            label.transform.localScale = Vector3.one * 0.45f;

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
            StockpileKind.Dirt => new Color(0.42f, 0.36f, 0.28f),
            _ => new Color(0.55f, 0.48f, 0.4f),
        };

        public void Reset()
        {
            Mass = 0f;
            Piles = 0;
            GoldSockets = 0;
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
            RefreshVisual();
            return true;
        }

        /// <summary>Legacy aggregate deposit — synthesizes placeholder cells if sockets unknown.</summary>
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
                    Sockets = PackGoldCount(g),
                    Mass = perMass,
                });
            }
            // value was estimated pre-wash — cleared conceptually; ore waits for washer
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

        static byte PackGoldCount(int gold)
        {
            gold = Mathf.Clamp(gold, 0, 4);
            byte s = 0;
            for (int i = 0; i < 4; i++)
            {
                var k = i < gold ? SocketKind.Gold : SocketKind.Rock;
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
                StockpileKind.RefinedGold => Mathf.Clamp01(GoldValue / 30f),
                StockpileKind.Dirt => Mathf.Clamp01(Piles / 40f + Mass / 50f),
                StockpileKind.Gold => Mathf.Clamp01(CellCount / 20f + Mass / 40f),
                _ => Mathf.Clamp01(CellCount / 25f + Mass / 50f),
            };
            float scale = Mathf.Lerp(0.12f, 0.55f, Mathf.Sqrt(t));
            _heap.localScale = Vector3.one * scale;
            bool on = Kind is StockpileKind.RefinedGold
                ? GoldValue > 0
                : Kind == StockpileKind.Dirt
                    ? Piles > 0 || Mass > 0.01f
                    : CellCount > 0 || Mass > 0.01f;
            _heap.gameObject.SetActive(on);
            _sr.color = HeapColor(Kind);
        }

        static Sprite MakePadSprite(StockpileKind kind)
        {
            const int s = 48;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            Color baseC = kind switch
            {
                StockpileKind.Gold => new Color(0.35f, 0.28f, 0.12f, 0.85f),
                StockpileKind.RefinedGold => new Color(0.4f, 0.32f, 0.08f, 0.9f),
                StockpileKind.Dirt => new Color(0.18f, 0.16f, 0.12f, 0.85f),
                _ => new Color(0.22f, 0.2f, 0.18f, 0.85f),
            };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - 23.5f) / 22f;
                float dy = (y - 23.5f) / 18f;
                float d = dx * dx + dy * dy;
                if (d > 1f) { tex.SetPixel(x, y, Color.clear); continue; }
                var c = baseC;
                if (d > 0.82f) c *= 0.65f;
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.45f), s);
        }

        static Sprite MakeLabelSprite(StockpileKind kind)
        {
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, Color.clear);

            Color ink = kind switch
            {
                StockpileKind.Gold => new Color(1f, 0.82f, 0.25f),
                StockpileKind.RefinedGold => new Color(1f, 0.95f, 0.5f),
                StockpileKind.Dirt => new Color(0.55f, 0.45f, 0.35f),
                _ => new Color(0.7f, 0.65f, 0.55f),
            };

            for (int y = 10; y < 22; y++)
            for (int x = 6; x < 26; x++)
                tex.SetPixel(x, y, ink * 0.35f);
            for (int y = 12; y < 20; y++)
            for (int x = 8; x < 24; x++)
                tex.SetPixel(x, y, ink);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }
    }

    /// <summary>Expanded base — ore piles, washer, refined gold + dirt outputs.</summary>
    public sealed class BasecampYard
    {
        public Transform Root;
        public Vector2 DropPoint;
        public Stockpile Rock;         // ore rock input
        public Stockpile Gold;         // ore gold input
        public Stockpile RefinedGold;
        public Stockpile Dirt;
        public WashMachine Washer;

        public static BasecampYard Spawn(Transform parent, Vector2 center)
        {
            var yard = new BasecampYard();
            var root = new GameObject("Basecamp").transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;
            yard.Root = root;
            yard.DropPoint = center;

            var platform = new GameObject("Platform");
            platform.transform.SetParent(root, false);
            platform.transform.localPosition = center + new Vector2(0f, -0.35f);
            var psr = platform.AddComponent<SpriteRenderer>();
            psr.sprite = MakePlatformSprite();
            psr.sortingOrder = 10;
            DigVisualKit.ApplyLit(psr);
            platform.transform.localScale = Vector3.one * 3.1f;

            // Layout (local offsets from drop) — wider pad for bigger camp
            //   [ORE ROCK]     [WASHER]     [ORE GOLD]
            //   [DIRT]                      [REFINED]
            float x = 1.45f;
            float yOre = 0.25f;
            float yOut = -1.15f;

            yard.Rock = Stockpile.Spawn(root, center + new Vector2(-x, yOre), StockpileKind.Rock);
            yard.Gold = Stockpile.Spawn(root, center + new Vector2(x, yOre), StockpileKind.Gold);
            yard.Dirt = Stockpile.Spawn(root, center + new Vector2(-x, yOut), StockpileKind.Dirt);
            yard.RefinedGold = Stockpile.Spawn(root, center + new Vector2(x, yOut), StockpileKind.RefinedGold);

            yard.Washer = WashMachine.Spawn(root, center + new Vector2(0f, -0.35f),
                yard.RefinedGold, yard.Dirt);

            return yard;
        }

        public void Reset()
        {
            Rock?.Reset();
            Gold?.Reset();
            Dirt?.Reset();
            RefinedGold?.Reset();
            Washer?.ResetMachine();
        }

        public Stockpile HitTest(Vector2 worldPoint)
        {
            if (RefinedGold != null && RefinedGold.ContainsWorldPoint(worldPoint)) return RefinedGold;
            if (Gold != null && Gold.ContainsWorldPoint(worldPoint)) return Gold;
            if (Dirt != null && Dirt.ContainsWorldPoint(worldPoint)) return Dirt;
            if (Rock != null && Rock.ContainsWorldPoint(worldPoint)) return Rock;
            return null;
        }

        static Sprite MakePlatformSprite()
        {
            const int s = 96;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - 47.5f) / 44f;
                float dy = (y - 48f) / 40f;
                float d = dx * dx + dy * dy;
                if (d > 1f) { tex.SetPixel(x, y, Color.clear); continue; }
                var c = new Color(0.28f, 0.24f, 0.18f, 0.92f);
                if (d > 0.78f) c = new Color(0.18f, 0.15f, 0.1f, 0.88f);
                // panel grid
                if ((x + y) % 11 == 0) c *= 0.85f;
                tex.SetPixel(x, y, c);
            }
            // center wash pad
            for (int y = 40; y < 58; y++)
            for (int x = 40; x < 56; x++)
                tex.SetPixel(x, y, new Color(0.15f, 0.35f, 0.4f, 0.9f));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.45f), s);
        }
    }
}
