using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    public enum ScanHintKind : byte { Gold = 0, Bedrock = 1, Gas = 2 }

    /// <summary>
    /// Sparse technical zone outlines for scan estimates. Cheap to draw, hard-capped count.
    /// Keep in sync with map features (gold / bedrock / gas / …).
    /// </summary>
    public sealed class ScanViewOverlay : MonoBehaviour
    {
        const int Ppu = 3;
        const int MaxGoldZones = 5;
        const int MaxBedZones = 4;
        const int MaxGasZones = 3;

        struct Zone
        {
            public float X, Y;
            public ScanHintKind Kind;
            public float Radius;
            public float StretchX, StretchY, Angle;
            public float Strength;
        }

        FineTerrainWorld _world;
        Texture2D _tex;
        Color32[] _px;
        readonly List<Zone> _zones = new(16);
        SpriteRenderer _sr;
        bool _visible;
        bool _dirty;

        static readonly Color32 Clear = new(0, 0, 0, 0);

        public bool Visible
        {
            get => _visible;
            set => SetVisible(value);
        }

        public static ScanViewOverlay Attach(Transform parent, FineTerrainWorld world)
        {
            var go = new GameObject("ScanViewOverlay");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<ScanViewOverlay>();
            fx.Setup(world);
            return fx;
        }

        public void Setup(FineTerrainWorld world)
        {
            _world = world;
            int w = world.Width * Ppu;
            int h = world.Height * Ppu;
            float ppuWorld = Ppu / world.CellSize;
            _tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _px = new Color32[w * h];

            _sr = gameObject.AddComponent<SpriteRenderer>();
            var sh = Shader.Find("Sprites/Default");
            if (sh != null) _sr.sharedMaterial = new Material(sh);
            _sr.sortingOrder = 52;
            _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, w, h), Vector2.zero, ppuWorld);
            _sr.enabled = false;
            _visible = false;
            ClearHints();
        }

        public void SetVisible(bool on)
        {
            // Legacy radar Scan View retired — Heavy Scanner Tactical View replaces it.
            _visible = false;
            if (_sr != null) _sr.enabled = false;
        }

        public void Toggle() => SetVisible(false);

        public void ClearHints()
        {
            _zones.Clear();
            _dirty = true;
            if (_sr != null) _sr.enabled = false;
            _visible = false;
        }

        public void Flash() { }
        public void SoftPing() { }

        public void AddHint(int cx, int cy, float gold, float bedrock, int bleed = 2) { }

        public void AddHint(int cx, int cy, float gold, float bedrock, float gas, int bleed = 2)
        {
            // No-op: legacy round-zone scan marks removed.
        }

        bool RemoveWeakest(ScanHintKind kind)
        {
            int weakest = -1;
            float wStr = float.MaxValue;
            for (int i = 0; i < _zones.Count; i++)
            {
                if (_zones[i].Kind != kind) continue;
                if (_zones[i].Strength < wStr)
                {
                    wStr = _zones[i].Strength;
                    weakest = i;
                }
            }
            if (weakest < 0) return false;
            _zones.RemoveAt(weakest);
            return true;
        }

        void LateUpdate()
        {
            if (!_visible || !_dirty || _world == null) return;
            Rebuild();
            _dirty = false;
        }

        void Rebuild()
        {
            int tw = _world.Width * Ppu;
            int th = _world.Height * Ppu;
            for (int i = 0; i < _px.Length; i++)
                _px[i] = Clear;

            for (int i = 0; i < _zones.Count; i++)
                DrawEllipseOutline(tw, th, _zones[i]);

            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        void DrawEllipseOutline(int texW, int texH, Zone z)
        {
            Color neon = z.Kind switch
            {
                ScanHintKind.Gold => new Color(1f, 0.72f, 0.22f, 1f),
                ScanHintKind.Gas => new Color(0.72f, 0.35f, 1f, 1f),
                _ => new Color(0.25f, 0.92f, 1f, 1f),
            };
            float a = 0.55f * z.Strength;

            float rx = z.Radius * z.StretchX * Ppu;
            float ry = z.Radius * z.StretchY * Ppu;
            float cx = z.X * Ppu;
            float cy = z.Y * Ppu;
            float ca = Mathf.Cos(z.Angle);
            float sa = Mathf.Sin(z.Angle);

            const int steps = 40;
            Vector2 prev = default;
            for (int i = 0; i <= steps; i++)
            {
                float t = (i / (float)steps) * Mathf.PI * 2f;
                float lx = Mathf.Cos(t) * rx;
                float ly = Mathf.Sin(t) * ry;
                Vector2 p = new(
                    cx + lx * ca - ly * sa,
                    cy + lx * sa + ly * ca);
                if (i > 0)
                    DrawSeg(texW, texH, prev, p, neon, a);
                prev = p;
            }

            float arm = Mathf.Clamp(Mathf.Min(rx, ry) * 0.18f, 2.5f, 7f);
            Vector2 c0 = new(
                cx + (-rx * 0.7f) * ca - (-ry * 0.7f) * sa,
                cy + (-rx * 0.7f) * sa + (-ry * 0.7f) * ca);
            DrawSeg(texW, texH, c0, c0 + new Vector2(ca, sa) * arm, neon, a * 0.7f);
            DrawSeg(texW, texH, c0, c0 + new Vector2(-sa, ca) * arm, neon, a * 0.7f);
        }

        void DrawSeg(int texW, int texH, Vector2 a, Vector2 b, Color col, float alpha)
        {
            float len = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 p = Vector2.Lerp(a, b, t);
                Plot(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), texW, col, alpha);
            }
        }

        void Plot(int x, int y, int texW, Color col, float alpha)
        {
            if (x < 0 || y < 0) return;
            int i = y * texW + x;
            if (i < 0 || i >= _px.Length) return;
            byte aa = (byte)Mathf.Clamp(alpha * 255f, 0, 180);
            _px[i] = new Color32(
                (byte)(col.r * 255f),
                (byte)(col.g * 255f),
                (byte)(col.b * 255f),
                aa);
        }
    }
}
