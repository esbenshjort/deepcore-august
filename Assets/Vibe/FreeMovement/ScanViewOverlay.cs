using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Soft vector outline zones — estimated gold / bedrock regions (not icon stamps).
    /// Nearby hints merge into quiet neon contours with faint interior wash.
    /// </summary>
    public sealed class ScanViewOverlay : MonoBehaviour
    {
        const int Ppu = 5;

        struct Zone
        {
            public float X;
            public float Y;
            public float Gold;
            public float Bed;
            public float Radius; // cells
            public float TargetRadius;
            public float Age;
            public float StretchX;
            public float StretchY;
            public float Angle; // radians — soft ellipse tilt
        }

        FineTerrainWorld _world;
        Texture2D _tex;
        Color32[] _px;
        readonly List<Zone> _zones = new(32);
        SpriteRenderer _sr;
        bool _visible;
        bool _dirty;
        float _flash;

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
            _visible = on;
            if (_sr != null) _sr.enabled = on;
            if (on) _dirty = true;
        }

        public void Toggle() => SetVisible(!_visible);

        public void ClearHints()
        {
            _zones.Clear();
            _dirty = true;
        }

        /// <summary>Add / grow a soft estimate region around a cell.</summary>
        public void AddHint(int cx, int cy, float gold, float bedrock, int bleed = 2)
        {
            if (_world == null || !_world.InBounds(cx, cy)) return;

            bool isGold = gold >= bedrock && gold > 0.01f;
            float radius = Mathf.Lerp(2.4f, 4.2f, Mathf.Clamp01(bleed / 3f));
            radius *= 0.9f + Random.value * 0.25f;

            float jx = cx + 0.5f + Random.Range(-0.45f, 0.45f);
            float jy = cy + 0.5f + Random.Range(-0.45f, 0.45f);

            // Merge same-type zones into soft area outlines
            const float mergeDist = 5.5f;
            for (int i = 0; i < _zones.Count; i++)
            {
                var z = _zones[i];
                bool zGold = z.Gold >= z.Bed && z.Gold > 0.01f;
                if (zGold != isGold) continue;
                float dx = z.X - jx;
                float dy = z.Y - jy;
                float mergeR = (z.TargetRadius + radius) * 0.65f;
                if (dx * dx + dy * dy > mergeR * mergeR && dx * dx + dy * dy > mergeDist * mergeDist)
                    continue;

                // Weighted center toward richer / newer ping
                float wNew = 0.35f + Mathf.Max(gold, bedrock) * 0.25f;
                z.X = Mathf.Lerp(z.X, jx, wNew);
                z.Y = Mathf.Lerp(z.Y, jy, wNew);
                z.Gold = Mathf.Max(z.Gold, gold);
                z.Bed = Mathf.Max(z.Bed, bedrock);
                z.TargetRadius = Mathf.Min(7.5f, z.TargetRadius + radius * 0.28f);
                z.Age = Mathf.Min(z.Age, 0.12f);
                z.StretchX = Mathf.Clamp(z.StretchX + Random.Range(-0.04f, 0.08f), 0.7f, 1.45f);
                z.StretchY = Mathf.Clamp(z.StretchY + Random.Range(-0.04f, 0.08f), 0.7f, 1.45f);
                _zones[i] = z;
                _dirty = true;
                return;
            }

            _zones.Add(new Zone
            {
                X = jx,
                Y = jy,
                Gold = Mathf.Clamp01(gold),
                Bed = Mathf.Clamp01(bedrock),
                Radius = radius * 0.2f,
                TargetRadius = radius,
                Age = 0f,
                StretchX = 0.85f + Random.value * 0.45f,
                StretchY = 0.85f + Random.value * 0.45f,
                Angle = Random.Range(-0.5f, 0.5f),
            });
            _dirty = true;
        }

        public void Flash() => _flash = Mathf.Max(_flash, 0.14f);
        public void SoftPing() => _flash = Mathf.Max(_flash, 0.08f);

        void LateUpdate()
        {
            if (_flash > 0f)
            {
                _flash -= Time.deltaTime * 1.2f;
                _dirty = true;
            }

            bool aging = false;
            for (int i = 0; i < _zones.Count; i++)
            {
                var z = _zones[i];
                if (z.Age >= 0.7f && Mathf.Abs(z.Radius - z.TargetRadius) < 0.05f) continue;
                z.Age += Time.deltaTime;
                float t = Mathf.Clamp01(z.Age / 0.55f);
                t = t * t * (3f - 2f * t);
                z.Radius = Mathf.Lerp(z.TargetRadius * 0.15f, z.TargetRadius, t);
                _zones[i] = z;
                aging = true;
            }
            if (aging) _dirty = true;

            if (!_visible || !_dirty || _world == null) return;
            Rebuild();
            _dirty = false;
        }

        void Rebuild()
        {
            int w = _world.Width * Ppu;
            int h = _world.Height * Ppu;
            float flash = Mathf.Clamp01(_flash / 0.3f);

            for (int i = 0; i < _px.Length; i++)
                _px[i] = Clear;

            // Soft metaball field → outline + whisper fill (vector HUD zones)
            DrawZoneField(w, h, goldLayer: true, flash);
            DrawZoneField(w, h, goldLayer: false, flash);

            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        void DrawZoneField(int texW, int texH, bool goldLayer, float flash)
        {
            // Bounds of relevant zones
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            int count = 0;
            for (int i = 0; i < _zones.Count; i++)
            {
                var z = _zones[i];
                bool isGold = z.Gold >= z.Bed && z.Gold > 0.01f;
                if (isGold != goldLayer) continue;
                float pad = z.Radius * 1.35f;
                minX = Mathf.Min(minX, z.X - pad);
                maxX = Mathf.Max(maxX, z.X + pad);
                minY = Mathf.Min(minY, z.Y - pad);
                maxY = Mathf.Max(maxY, z.Y + pad);
                count++;
            }
            if (count == 0) return;

            int x0 = Mathf.Max(0, Mathf.FloorToInt(minX * Ppu) - 2);
            int x1 = Mathf.Min(texW - 1, Mathf.CeilToInt(maxX * Ppu) + 2);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY * Ppu) - 2);
            int y1 = Mathf.Min(texH - 1, Mathf.CeilToInt(maxY * Ppu) + 2);

            Color neon = goldLayer
                ? new Color(1f, 0.84f, 0.28f, 1f)
                : new Color(0.35f, 0.92f, 1f, 1f);

            // Contour band around field threshold — soft vector outline
            const float threshold = 1f;
            const float outlineHalf = 0.14f;

            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float cellX = (x + 0.5f) / Ppu;
                float cellY = (y + 0.5f) / Ppu;
                float field = SampleField(cellX, cellY, goldLayer);
                if (field < 0.01f) continue;

                float appear = 1f; // zones already age their radius
                float boost = 1f + flash * 0.15f;

                // Soft interior wash
                if (field >= threshold)
                {
                    float depth = Mathf.Clamp01((field - threshold) / 1.2f);
                    float fillA = (0.035f + 0.04f * depth) * boost;
                    Blend(x, y, texW, neon.r, neon.g, neon.b, fillA);
                }

                // Soft outline (distance-to-threshold band)
                float edgeDist = Mathf.Abs(field - threshold);
                if (edgeDist < outlineHalf * 2.2f)
                {
                    float e = 1f - edgeDist / (outlineHalf * 2.2f);
                    e = e * e * (3f - 2f * e);
                    // Sharper core of the stroke, soft bloom outside
                    float stroke = e * e;
                    float a = (0.22f + 0.28f * stroke) * boost * appear;
                    // Outer glow slightly wider / quieter
                    float glow = e * 0.1f * boost;
                    Blend(x, y, texW, neon.r, neon.g, neon.b, Mathf.Max(a, glow));
                }
            }
        }

        float SampleField(float cellX, float cellY, bool goldLayer)
        {
            float sum = 0f;
            for (int i = 0; i < _zones.Count; i++)
            {
                var z = _zones[i];
                bool isGold = z.Gold >= z.Bed && z.Gold > 0.01f;
                if (isGold != goldLayer) continue;

                float appear = Mathf.Clamp01(z.Age / 0.4f);
                appear = appear * appear * (3f - 2f * appear);
                float strength = Mathf.Max(z.Gold, z.Bed) * Mathf.Lerp(0.55f, 1f, appear);
                if (strength < 0.05f) continue;

                float dx = cellX - z.X;
                float dy = cellY - z.Y;
                // Soft ellipse with slight rotation — organic vector shape
                float ca = Mathf.Cos(z.Angle);
                float sa = Mathf.Sin(z.Angle);
                float rx = (dx * ca + dy * sa) / (z.Radius * z.StretchX + 0.001f);
                float ry = (-dx * sa + dy * ca) / (z.Radius * z.StretchY + 0.001f);
                float d2 = rx * rx + ry * ry;
                if (d2 > 2.8f) continue;

                // Metaball falloff + tiny noise for soft non-perfect edge
                float n = Hash(cellX * 0.7f + z.X, cellY * 0.7f + z.Y);
                float warp = 1f + (n - 0.5f) * 0.12f;
                float d = Mathf.Sqrt(d2) * warp;
                if (d >= 1.55f) continue;

                // Smooth contribution — peaks near center
                float contrib = 1f - d / 1.55f;
                contrib = contrib * contrib;
                sum += contrib * strength * 1.65f;
            }
            return sum;
        }

        void Blend(int x, int y, int texW, float r, float g, float b, float srcA)
        {
            if (x < 0 || y < 0) return;
            int i = y * texW + x;
            if (i < 0 || i >= _px.Length) return;
            srcA = Mathf.Clamp01(srcA);
            if (srcA < 0.002f) return;

            var prev = _px[i];
            float dstA = prev.a / 255f;
            float outA = srcA + dstA * (1f - srcA);
            if (outA < 0.001f) return;

            float wSrc = srcA / outA;
            float wDst = 1f - wSrc;
            _px[i] = new Color32(
                (byte)Mathf.Clamp(r * 255f * wSrc + prev.r * wDst, 0, 255),
                (byte)Mathf.Clamp(g * 255f * wSrc + prev.g * wDst, 0, 255),
                (byte)Mathf.Clamp(b * 255f * wSrc + prev.b * wDst, 0, 255),
                (byte)Mathf.Clamp(outA * 255f, 0, 110));
        }

        static float Hash(float x, float y)
        {
            float n = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
            return n - Mathf.Floor(n);
        }
    }
}
