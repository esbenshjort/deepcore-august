using System.IO;
using UnityEngine;

namespace DeepCore.DualGrid
{
    /// <summary>
    /// Drop real PNGs in Assets/Vibe/Art/DualGrid/ — loaded at runtime.
    /// Missing files fall back to procedural DualGridLook.
    ///
    /// Expected files (any subset):
    ///   Floor.png       — seamless excavated floor
    ///   Rock.png        — seamless solid rock
    ///   RockDamage.png  — optional cracked rock (else Rock darkens)
    ///   Excavator.png   — unit, faces UP (+Y)
    ///   Worker.png
    ///   Goal.png
    ///   Torch.png
    /// </summary>
    public static class DualGridArt
    {
        const string RelDir = "Vibe/Art/DualGrid";

        static Texture2D _floorTex;
        static Texture2D _rockTex;
        static Texture2D _damageTex;
        static Sprite _excavator;
        static Sprite _worker;
        static Sprite _goal;
        static Sprite _torch;
        static bool _loaded;

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            _floorTex = LoadTexture("Floor.png");
            _rockTex = LoadTexture("Rock.png");
            _damageTex = LoadTexture("RockDamage.png");
            _excavator = LoadSprite("Excavator.png", 100f);
            _worker = LoadSprite("Worker.png", 64f);
            _goal = LoadSprite("Goal.png", 64f);
            _torch = LoadSprite("Torch.png", 32f);

            // Sensible defaults from topology Full if Rock missing
            if (_rockTex == null)
                _rockTex = LoadTextureFrom("Vibe/Art/Topology/Full.png")
                           ?? LoadTextureFrom("Vibe/Resources/Topology/Full.png");
        }

        public static void Reload()
        {
            _loaded = false;
            _floorTex = _rockTex = _damageTex = null;
            _excavator = _worker = _goal = _torch = null;
            EnsureLoaded();
        }

        public static Sprite Excavator()
        {
            EnsureLoaded();
            return _excavator ?? DualGridLook.MakeExcavator();
        }

        public static Sprite Worker()
        {
            EnsureLoaded();
            return _worker ?? DualGridLook.MakeWorker();
        }

        public static Sprite Goal()
        {
            EnsureLoaded();
            return _goal ?? DualGridLook.MakeGoalMarker();
        }

        public static Sprite Torch()
        {
            EnsureLoaded();
            return _torch ?? DualGridLook.MakePixel();
        }

        /// <summary>Sample floor albedo in world UV (tile seamlessly).</summary>
        public static Color32 SampleFloor(float worldX, float worldY)
        {
            EnsureLoaded();
            if (_floorTex != null) return SampleWrap(_floorTex, worldX, worldY, 2.5f);
            // procedural warm brown
            float n = Mathf.PerlinNoise(worldX * 3.1f, worldY * 3.1f);
            byte r = (byte)(90 + n * 50);
            byte g = (byte)(55 + n * 30);
            byte b = (byte)(28 + n * 16);
            return new Color32(r, g, b, 255);
        }

        public static Color32 SampleRock(float worldX, float worldY, float damage01)
        {
            EnsureLoaded();
            Color32 c;
            if (_rockTex != null) c = SampleWrap(_rockTex, worldX, worldY, 2.2f);
            else
            {
                float n = Mathf.PerlinNoise(worldX * 2.4f + 9f, worldY * 2.4f);
                byte s = (byte)(22 + n * 36);
                c = new Color32(s, (byte)(s + 2), (byte)(s + 4), 255);
            }

            if (damage01 > 0.01f)
            {
                Color32 dmg;
                if (_damageTex != null) dmg = SampleWrap(_damageTex, worldX, worldY, 2.2f);
                else dmg = new Color32(90, 58, 32, 255);
                c = Lerp(c, dmg, damage01 * 0.75f);
            }
            return c;
        }

        public static bool HasFloorArt
        {
            get { EnsureLoaded(); return _floorTex != null; }
        }

        public static bool HasRockArt
        {
            get { EnsureLoaded(); return _rockTex != null; }
        }

        static Color32 SampleWrap(Texture2D tex, float worldX, float worldY, float tilesPerUnit)
        {
            float u = Fract(worldX * tilesPerUnit);
            float v = Fract(worldY * tilesPerUnit);
            int x = Mathf.Clamp((int)(u * tex.width), 0, tex.width - 1);
            int y = Mathf.Clamp((int)(v * tex.height), 0, tex.height - 1);
            var c = tex.GetPixel(x, y);
            return new Color32(
                (byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), 255);
        }

        static float Fract(float v) => v - Mathf.Floor(v);

        static Color32 Lerp(Color32 a, Color32 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color32(
                (byte)(a.r + (b.r - a.r) * t),
                (byte)(a.g + (b.g - a.g) * t),
                (byte)(a.b + (b.b - a.b) * t),
                255);
        }

        static Texture2D LoadTexture(string fileName) =>
            LoadTextureFrom(Path.Combine(RelDir, fileName));

        static Texture2D LoadTextureFrom(string relativeUnderAssets)
        {
            string path = Path.Combine(Application.dataPath, relativeUnderAssets);
            if (!File.Exists(path)) return null;
            var bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
                name = Path.GetFileNameWithoutExtension(relativeUnderAssets)
            };
            if (!tex.LoadImage(bytes))
            {
                Object.Destroy(tex);
                return null;
            }
            // Readable sampling
            tex.Apply(false, false);
            return tex;
        }

        static Sprite LoadSprite(string fileName, float ppu)
        {
            var tex = LoadTexture(fileName);
            if (tex == null) return null;
            tex.wrapMode = TextureWrapMode.Clamp;
            // Treat near-black as transparent for cutout unit art
            HardCutBlack(tex, 12);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
        }

        static void HardCutBlack(Texture2D tex, int threshold)
        {
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a < 8 || c.r + c.g + c.b < threshold * 3)
                    px[i] = new Color32(0, 0, 0, 0);
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
        }
    }
}
