using UnityEngine;

namespace DeepCore.FreeMovement
{
    public enum StockpileKind : byte { Rock = 0, Gold = 1 }

    /// <summary>Visual stockpile at basecamp — grows as hauler deposits. Hover for stats.</summary>
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

        public string Title => Kind == StockpileKind.Gold ? "GOLD STOCKPILE" : "ROCK STOCKPILE";

        public string HoverInfo
        {
            get
            {
                if (Kind == StockpileKind.Gold)
                {
                    return
                        $"{Title}\n" +
                        $"value      {GoldValue}\n" +
                        $"sockets    {GoldSockets}\n" +
                        $"mass       {Mass:0.#}\n" +
                        $"piles      {Piles}\n" +
                        $"deliveries {Deliveries}";
                }

                return
                    $"{Title}\n" +
                    $"mass       {Mass:0.#}\n" +
                    $"piles      {Piles}\n" +
                    $"deliveries {Deliveries}";
            }
        }

        public static Stockpile Spawn(Transform parent, Vector2 localPos, StockpileKind kind)
        {
            var go = new GameObject(kind == StockpileKind.Gold ? "GoldStockpile" : "RockStockpile");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            // Pad
            var pad = new GameObject("Pad");
            pad.transform.SetParent(go.transform, false);
            pad.transform.localPosition = Vector3.zero;
            var psr = pad.AddComponent<SpriteRenderer>();
            psr.sprite = MakePadSprite(kind);
            psr.sortingOrder = 11;
            DigVisualKit.ApplyLit(psr);
            pad.transform.localScale = Vector3.one * 0.7f;

            // Heap
            var heap = new GameObject("Heap");
            heap.transform.SetParent(go.transform, false);
            heap.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            var hsr = heap.AddComponent<SpriteRenderer>();
            hsr.sprite = kind == StockpileKind.Gold ? DigVisualKit.GoldNugget : DigVisualKit.RockPile;
            hsr.sortingOrder = 13;
            DigVisualKit.ApplyLit(hsr);
            heap.transform.localScale = Vector3.one * 0.15f;
            hsr.color = kind == StockpileKind.Gold
                ? new Color(1f, 0.82f, 0.3f)
                : new Color(0.55f, 0.48f, 0.4f);

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

        public void Reset()
        {
            Mass = 0f;
            Piles = 0;
            GoldSockets = 0;
            GoldValue = 0;
            Deliveries = 0;
            RefreshVisual();
        }

        public void DepositRock(float mass, int piles)
        {
            if (mass <= 0f && piles <= 0) return;
            Mass += Mathf.Max(0f, mass);
            Piles += Mathf.Max(0, piles);
            Deliveries++;
            RefreshVisual();
        }

        public void DepositGold(float mass, int sockets, int value, int piles)
        {
            if (piles <= 0 && value <= 0) return;
            Mass += Mathf.Max(0f, mass);
            GoldSockets += Mathf.Max(0, sockets);
            GoldValue += Mathf.Max(0, value);
            Piles += Mathf.Max(0, piles);
            Deliveries++;
            RefreshVisual();
        }

        public bool ContainsWorldPoint(Vector2 worldPoint)
        {
            Vector2 p = transform.position;
            return (worldPoint - p).sqrMagnitude <= HoverRadius * HoverRadius;
        }

        void RefreshVisual()
        {
            if (_heap == null || _sr == null) return;
            float t = Kind == StockpileKind.Gold
                ? Mathf.Clamp01(GoldValue / 40f + Mass / 50f)
                : Mathf.Clamp01(Mass / 60f);
            float scale = Mathf.Lerp(0.12f, 0.55f, Mathf.Sqrt(t));
            _heap.localScale = Vector3.one * scale;
            _heap.gameObject.SetActive(Mass > 0.01f || GoldValue > 0 || Piles > 0);

            if (Kind == StockpileKind.Gold)
            {
                float g = Mathf.Clamp01(GoldSockets / 20f);
                _sr.color = Color.Lerp(new Color(0.85f, 0.65f, 0.22f), new Color(1f, 0.9f, 0.4f), g);
            }
        }

        static Sprite MakePadSprite(StockpileKind kind)
        {
            const int s = 48;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - 23.5f) / 22f;
                float dy = (y - 23.5f) / 18f;
                float d = dx * dx + dy * dy;
                if (d > 1f) { tex.SetPixel(x, y, Color.clear); continue; }
                Color c = kind == StockpileKind.Gold
                    ? new Color(0.35f, 0.28f, 0.12f, 0.85f)
                    : new Color(0.22f, 0.2f, 0.18f, 0.85f);
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

            Color ink = kind == StockpileKind.Gold
                ? new Color(1f, 0.82f, 0.25f)
                : new Color(0.7f, 0.65f, 0.55f);

            // Simple bar marker (G / R stripe)
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

    /// <summary>Base platform + rock/gold stockpiles. Hauler returns to DropPoint.</summary>
    public sealed class BasecampYard
    {
        public Transform Root;
        public Vector2 DropPoint;
        public Stockpile Rock;
        public Stockpile Gold;

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
            platform.transform.localPosition = center;
            var psr = platform.AddComponent<SpriteRenderer>();
            psr.sprite = MakePlatformSprite();
            psr.sortingOrder = 10;
            DigVisualKit.ApplyLit(psr);
            platform.transform.localScale = Vector3.one * 1.1f;

            // Rock left, gold right of drop point
            float spread = 0.85f;
            yard.Rock = Stockpile.Spawn(root, center + new Vector2(-spread, -0.05f), StockpileKind.Rock);
            yard.Gold = Stockpile.Spawn(root, center + new Vector2(spread, -0.05f), StockpileKind.Gold);
            return yard;
        }

        public void Reset()
        {
            Rock?.Reset();
            Gold?.Reset();
        }

        public Stockpile HitTest(Vector2 worldPoint)
        {
            if (Gold != null && Gold.ContainsWorldPoint(worldPoint)) return Gold;
            if (Rock != null && Rock.ContainsWorldPoint(worldPoint)) return Rock;
            return null;
        }

        static Sprite MakePlatformSprite()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - 31.5f) / 30f;
                float dy = (y - 28f) / 16f;
                float d = dx * dx + dy * dy;
                if (d > 1f) { tex.SetPixel(x, y, Color.clear); continue; }
                var c = new Color(0.32f, 0.26f, 0.18f, 0.9f);
                if (d > 0.75f) c = new Color(0.22f, 0.18f, 0.12f, 0.85f);
                tex.SetPixel(x, y, c);
            }
            // center marker
            for (int y = 26; y < 38; y++)
            for (int x = 28; x < 36; x++)
                tex.SetPixel(x, y, new Color(0.2f, 0.42f, 0.32f, 0.9f));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.4f), s);
        }
    }
}
