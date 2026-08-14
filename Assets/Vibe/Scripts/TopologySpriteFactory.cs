using System.IO;
using UnityEngine;

namespace DeepCore.Vibe
{
    /// <summary>
    /// Uses fritlagt topology art as ALPHA MASK + Full rock color.
    /// Hard binary alpha so cutouts read as transparent over floor.
    /// </summary>
    public static class TopologySpriteFactory
    {
        const int Size = 100;
        const float Ppu = 100f;

        static Sprite[] _cache;
        static Color32[] _fullPixels;
        static readonly string[] MaskFiles =
        {
            "Full",
            "Edge",
            "OuterCorner",
            "InnerCorner",
            "Diagonal",
            "Tip",
            "TJunction",
            "Cross",
        };

        public static Sprite Get(RockTopology8.Kind kind)
        {
            _cache ??= new Sprite[8];
            int i = (int)kind;
            if (_cache[i] == null)
                _cache[i] = Bake(kind);
            return _cache[i];
        }

        public static void ClearCache()
        {
            _cache = null;
            _fullPixels = null;
        }

        public static float EdgeWave(float t) =>
            0.28f + 0.05f * Mathf.Sin(t * Mathf.PI * 2f) + 0.02f * Mathf.Sin(t * Mathf.PI * 4f);

        static Sprite Bake(RockTopology8.Kind kind)
        {
            EnsureFullPixels();
            var mask = LoadMaskPixels(MaskFiles[(int)kind]);

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, // no dark bilinear fringe
                wrapMode = TextureWrapMode.Clamp,
                name = $"TopoMask_{kind}"
            };

            var pixels = new Color32[Size * Size];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte ma = mask != null ? mask[i].a : (byte)255;
                // Hard fritlæg: only keep clearly opaque rock
                if (ma < 40)
                {
                    pixels[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                // Prefer Full texture color; if mask has visible rock shading keep a blend
                var full = _fullPixels[i];
                var m = mask[i];
                bool maskHasRockColor = m.r + m.g + m.b > 40;
                Color32 c = maskHasRockColor
                    ? new Color32(
                        (byte)((full.r * 2 + m.r) / 3),
                        (byte)((full.g * 2 + m.g) / 3),
                        (byte)((full.b * 2 + m.b) / 3),
                        255)
                    : new Color32(full.r, full.g, full.b, 255);
                pixels[i] = c;
            }

            // If mask file missing, fall back to welded procedural coverage
            if (mask == null)
            {
                for (int y = 0; y < Size; y++)
                {
                    float v = y / (Size - 1f);
                    for (int x = 0; x < Size; x++)
                    {
                        float u = x / (Size - 1f);
                        int i = y * Size + x;
                        if (!ProcCoverage(kind, u, v))
                            pixels[i] = new Color32(0, 0, 0, 0);
                        else
                        {
                            var f = _fullPixels[i];
                            pixels[i] = new Color32(f.r, f.g, f.b, 255);
                        }
                    }
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Ppu);
        }

        static bool ProcCoverage(RockTopology8.Kind kind, float u, float v) => kind switch
        {
            RockTopology8.Kind.Full => true,
            RockTopology8.Kind.Edge => v >= EdgeWave(u),
            RockTopology8.Kind.OuterCorner => v >= EdgeWave(u) && (1f - u) >= EdgeWave(v),
            RockTopology8.Kind.InnerCorner => !(u > 0.42f && v < 0.58f && (1f - u) * (1f - u) + v * v < 0.42f),
            RockTopology8.Kind.Diagonal => u + v < 1.02f + 0.04f * Mathf.Sin((u - v) * 6f),
            RockTopology8.Kind.Tip => Mathf.Abs(u - 0.5f) <= 0.16f + 0.34f * v && v >= 0.08f,
            RockTopology8.Kind.TJunction => v >= 0.52f || Mathf.Abs(u - 0.5f) <= 0.26f,
            RockTopology8.Kind.Cross => Mathf.Abs(u - 0.5f) <= 0.26f || Mathf.Abs(v - 0.5f) <= 0.26f,
            _ => true,
        };

        static Color32[] LoadMaskPixels(string name)
        {
            string[] candidates =
            {
                Path.Combine(Application.dataPath, "Vibe/Resources/Topology", name + ".png"),
                Path.Combine(Application.dataPath, "Vibe/Art/Topology", name + ".png"),
            };

            foreach (var path in candidates)
            {
                if (!File.Exists(path)) continue;
                var bytes = File.ReadAllBytes(path);
                var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!src.LoadImage(bytes)) { Object.Destroy(src); continue; }

                var srcPx = src.GetPixels32();
                int sw = src.width, sh = src.height;
                var dst = new Color32[Size * Size];

                // Orient art to canonical openings (matches RockTopology8 comments)
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int sx = Mathf.Clamp(x * sw / Size, 0, sw - 1);
                    int sy = Mathf.Clamp(y * sh / Size, 0, sh - 1);
                    dst[y * Size + x] = srcPx[sy * sw + sx];
                }

                Object.Destroy(src);
                return dst;
            }

            return null;
        }

        static void EnsureFullPixels()
        {
            if (_fullPixels != null) return;
            _fullPixels = new Color32[Size * Size];

            string[] candidates =
            {
                Path.Combine(Application.dataPath, "Vibe/Resources/Topology", "Full.png"),
                Path.Combine(Application.dataPath, "Vibe/Art/Topology", "Full.png"),
            };

            Texture2D src = null;
            foreach (var path in candidates)
            {
                if (!File.Exists(path)) continue;
                var bytes = File.ReadAllBytes(path);
                src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!src.LoadImage(bytes)) { Object.Destroy(src); src = null; continue; }
                break;
            }

            if (src == null)
            {
                for (int i = 0; i < _fullPixels.Length; i++)
                {
                    int x = i % Size, y = i / Size;
                    float n = Hash(x, y);
                    byte s = (byte)(30 + n * 45);
                    _fullPixels[i] = new Color32(s, (byte)(s - 2), (byte)(s - 4), 255);
                }
                return;
            }

            var sp = src.GetPixels32();
            int sw = src.width, sh = src.height;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var c = sp[(y * sh / Size) * sw + (x * sw / Size)];
                if (c.a < 200 || c.r + c.g + c.b < 12)
                    c = new Color32(40, 40, 42, 255);
                c.a = 255;
                _fullPixels[y * Size + x] = c;
            }
            Object.Destroy(src);
        }

        static float Hash(int x, int y)
        {
            uint n = (uint)(x * 374761393 + y * 668265263);
            n = (n ^ (n >> 13)) * 1274126177u;
            return (n & 0xFFFF) / 65535f;
        }
    }
}
