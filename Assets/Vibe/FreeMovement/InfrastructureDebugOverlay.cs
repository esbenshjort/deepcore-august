using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Debug visualization: lantern illumination, proposed sites, tunnel instability.
    /// </summary>
    public sealed class InfrastructureDebugOverlay : MonoBehaviour
    {
        const int Ppu = 2;

        FineTerrainWorld _world;
        MineInfrastructure _infra;
        LogisticsTrafficMap _traffic;
        Texture2D _tex;
        Color32[] _px;
        SpriteRenderer _sr;
        bool _visible;
        float _repaintTimer;

        static readonly Color32 Clear = new(0, 0, 0, 0);

        public bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                if (_sr != null) _sr.enabled = value;
                if (value) MarkDirty();
            }
        }

        public static InfrastructureDebugOverlay Attach(
            Transform parent,
            FineTerrainWorld world,
            MineInfrastructure infra)
        {
            var go = new GameObject("InfrastructureDebugOverlay");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<InfrastructureDebugOverlay>();
            fx.Setup(world, infra);
            return fx;
        }

        public void Setup(FineTerrainWorld world, MineInfrastructure infra)
        {
            _world = world;
            _infra = infra;
            _traffic = infra?.Traffic;

            int w = world.Width * Ppu;
            int h = world.Height * Ppu;
            float ppuWorld = Ppu / world.CellSize;

            _tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            _px = new Color32[w * h];

            if (_sr == null)
            {
                _sr = gameObject.AddComponent<SpriteRenderer>();
                var sh = Shader.Find("Sprites/Default");
                if (sh != null) _sr.sharedMaterial = new Material(sh);
                _sr.sortingOrder = 54;
            }

            _sr.sprite = Sprite.Create(
                _tex,
                new Rect(0, 0, w, h),
                new Vector2(0f, 0f),
                ppuWorld);
            transform.localPosition = Vector3.zero;
            _sr.enabled = _visible;
            MarkDirty();
        }

        public void MarkDirty() => _repaintTimer = 0f;

        void LateUpdate()
        {
            if (!_visible || _world == null || _infra == null) return;
            _repaintTimer -= Time.unscaledDeltaTime;
            if (_repaintTimer > 0f) return;
            _repaintTimer = 0.35f;
            _infra.RefreshProposedDebug();
            Repaint();
        }

        void Repaint()
        {
            int tw = _world.Width * Ppu;
            for (int i = 0; i < _px.Length; i++)
                _px[i] = Clear;

            float spacing = _infra.LanternMinSpacingWorld;
            float cell = _world.CellSize;
            _ = _traffic;

            int step = 2;
            for (int y = 0; y < _world.Height; y += step)
            for (int x = 0; x < _world.Width; x += step)
            {
                if (!_world.IsTunnelOpen(x, y)) continue;
                float inst = _infra.EvaluateInstability(x, y);
                if (inst >= _infra.SupportPreventScore)
                {
                    byte a = (byte)Mathf.Clamp(inst * 18f, 20, 160);
                    bool crit = inst >= _infra.SupportCriticalScore;
                    FillCell(tw, x, y, crit
                        ? new Color32(255, 60, 40, a)
                        : new Color32(255, 160, 40, (byte)(a * 0.7f)));
                }
            }

            var proposed = _infra.ProposedDebug;
            for (int i = 0; i < proposed.Count; i++)
            {
                var c = proposed[i];
                OutlineCell(tw, c.x, c.y, new Color32(90, 255, 140, 200));
            }

            float ringCells = spacing / Mathf.Max(0.001f, cell);
            DrawLanternCoverage(tw, ringCells);

            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        void DrawLanternCoverage(int texW, float ringCells)
        {
            int step = Mathf.Max(1, Mathf.FloorToInt(ringCells * 0.28f));
            for (int y = 0; y < _world.Height; y += step)
            for (int x = 0; x < _world.Width; x += step)
            {
                if (!_world.IsTunnelOpen(x, y)) continue;
                Vector2 w = _world.CellCenter(x, y);
                float illum = _infra.EvaluateIllumination01(w);
                if (illum < 0.08f) continue;
                byte a = (byte)Mathf.Clamp(illum * 110f, 18, 110);
                FillCell(texW, x, y, new Color32(255, 150, 60, a));
            }
        }

        void FillCell(int texW, int cx, int cy, Color32 c)
        {
            int x0 = cx * Ppu;
            int y0 = cy * Ppu;
            for (int py = 0; py < Ppu; py++)
            for (int px = 0; px < Ppu; px++)
            {
                int i = (y0 + py) * texW + (x0 + px);
                if ((uint)i >= (uint)_px.Length) continue;
                _px[i] = Blend(_px[i], c);
            }
        }

        void OutlineCell(int texW, int cx, int cy, Color32 c)
        {
            int x0 = cx * Ppu;
            int y0 = cy * Ppu;
            for (int i = 0; i < Ppu; i++)
            {
                Set(texW, x0 + i, y0, c);
                Set(texW, x0 + i, y0 + Ppu - 1, c);
                Set(texW, x0, y0 + i, c);
                Set(texW, x0 + Ppu - 1, y0 + i, c);
            }
        }

        void Set(int texW, int x, int y, Color32 c)
        {
            int i = y * texW + x;
            if ((uint)i >= (uint)_px.Length) return;
            _px[i] = Blend(_px[i], c);
        }

        static Color32 Blend(Color32 a, Color32 b)
        {
            if (a.a == 0) return b;
            float t = b.a / 255f;
            return new Color32(
                (byte)(a.r + (b.r - a.r) * t),
                (byte)(a.g + (b.g - a.g) * t),
                (byte)(a.b + (b.b - a.b) * t),
                (byte)Mathf.Max(a.a, b.a));
        }
    }
}
