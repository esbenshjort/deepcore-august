using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Full-map unlit overlay: gold (warm), bedrock (cool), gas (purple) for planning.
    /// Subtle / translucent so the dig face still reads underneath.
    /// Keep in sync when adding map features (minerals / pockets / hazards).
    /// </summary>
    public sealed class TacticalMapOverlay : MonoBehaviour
    {
        const int Ppu = 2;

        FineTerrainWorld _world;
        Texture2D _tex;
        Color32[] _px;
        SpriteRenderer _sr;
        bool _visible;
        bool _dirty = true;

        static readonly Color32 Excavated = new(10, 10, 12, 18);
        static readonly Color32 RockDim = new(48, 44, 40, 22);
        static readonly Color32 BedrockLo = new(70, 105, 140, 70);
        static readonly Color32 BedrockHi = new(95, 135, 175, 100);
        static readonly Color32 Gold1 = new(180, 140, 50, 55);
        static readonly Color32 Gold2 = new(200, 160, 55, 75);
        static readonly Color32 Gold3 = new(220, 180, 65, 95);
        static readonly Color32 Gold4 = new(235, 200, 90, 115);
        static readonly Color32 GasPocket = new(140, 70, 200, 95);
        static readonly Color32 GasCore = new(170, 90, 230, 120);

        public bool Visible
        {
            get => _visible;
            set => SetVisible(value);
        }

        public static TacticalMapOverlay Attach(Transform parent, FineTerrainWorld world)
        {
            var go = new GameObject("TacticalOverlay");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<TacticalMapOverlay>();
            fx.Setup(world);
            return fx;
        }

        public void Setup(FineTerrainWorld world)
        {
            if (_world != null)
                _world.RegionChanged -= OnRegion;
            _world = world;
            if (_world != null)
                _world.RegionChanged += OnRegion;

            int w = world.Width * Ppu;
            int h = world.Height * Ppu;
            float ppuWorld = Ppu / world.CellSize;

            _tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _px = new Color32[w * h];

            if (_sr == null)
            {
                _sr = gameObject.AddComponent<SpriteRenderer>();
                var sh = Shader.Find("Sprites/Default");
                if (sh != null) _sr.sharedMaterial = new Material(sh);
                _sr.sortingOrder = 55;
            }
            _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, w, h), Vector2.zero, ppuWorld);
            _sr.enabled = false;
            _visible = false;
            _dirty = true;
        }

        void OnDestroy()
        {
            if (_world != null)
                _world.RegionChanged -= OnRegion;
        }

        void OnRegion(int x0, int y0, int x1, int y1) => _dirty = true;

        public void SetVisible(bool on)
        {
            _visible = on;
            if (_sr != null) _sr.enabled = on;
            if (on) _dirty = true;
        }

        public void MarkDirty() => _dirty = true;

        public void Toggle() => SetVisible(!_visible);

        void LateUpdate()
        {
            if (!_visible || !_dirty || _world == null) return;
            Rebuild();
            _dirty = false;
        }

        void Rebuild()
        {
            int tw = _world.Width;
            int th = _world.Height;
            int w = tw * Ppu;

            for (int cy = 0; cy < th; cy++)
            for (int cx = 0; cx < tw; cx++)
            {
                var cell = _world.Get(cx, cy);
                Color32 col;
                if (_world.IsGas(cx, cy))
                    col = _world.IsGasRevealed(cx, cy) ? GasCore : GasPocket;
                else if (_world.IsTunnelOpen(cx, cy))
                    col = Excavated;
                else if (cell.IsUndamageableBorder)
                    col = new Color32(24, 28, 36, 90);
                else if (cell.GoldCount > 0)
                {
                    col = cell.GoldCount switch
                    {
                        1 => Gold1,
                        2 => Gold2,
                        3 => Gold3,
                        _ => Gold4,
                    };
                    if (cell.BedrockCount >= 2)
                        col = Lerp(col, BedrockLo, 0.28f);
                }
                else if (cell.BedrockCount >= 2)
                    col = cell.BedrockCount >= 3 ? BedrockHi : BedrockLo;
                else
                    col = RockDim;

                for (int py = 0; py < Ppu; py++)
                for (int px = 0; px < Ppu; px++)
                {
                    int i = (cy * Ppu + py) * w + (cx * Ppu + px);
                    _px[i] = col;
                }
            }

            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        static Color32 Lerp(Color32 a, Color32 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color32(
                (byte)(a.r + (b.r - a.r) * t),
                (byte)(a.g + (b.g - a.g) * t),
                (byte)(a.b + (b.b - a.b) * t),
                (byte)(a.a + (b.a - a.a) * t));
        }
    }
}
