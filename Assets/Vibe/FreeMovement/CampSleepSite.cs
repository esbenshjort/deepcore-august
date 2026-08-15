using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Tent + firepit sleep site. Crew returns here after shift (18:00) and emerges at 08:00.
    /// </summary>
    public sealed class CampSleepSite : MonoBehaviour
    {
        public Vector2 TentDoor { get; private set; }
        public Vector2 FirePos { get; private set; }
        public Transform Root => transform;

        public static CampSleepSite Spawn(Transform parent, Vector2 campCenter)
        {
            var go = new GameObject("TentCamp");
            go.transform.SetParent(parent, false);
            var camp = go.AddComponent<CampSleepSite>();
            camp.Build(campCenter);
            return camp;
        }

        void Build(Vector2 campCenter)
        {
            // West of wash pad — keep inside excavated camp (cellSize 0.1)
            TentDoor = campCenter + new Vector2(-1.85f, -0.15f);
            FirePos = campCenter + new Vector2(-1.15f, -1.05f);
            transform.localPosition = Vector3.zero;

            // Packed dirt pad under camp
            var pad = MakeSpriteGo("SleepPad", TentDoor + new Vector2(0.35f, -0.35f),
                MakePadSprite(), 9, 1.55f);
            DigVisualKit.ApplyLit(pad);

            // A-frame tent
            var tent = MakeSpriteGo("Tent", TentDoor + new Vector2(0.15f, 0.05f),
                MakeTentSprite(), 16, 0.95f);
            DigVisualKit.ApplyLit(tent);

            // Door flap darker
            var flap = MakeSpriteGo("TentFlap", TentDoor + new Vector2(-0.02f, -0.12f),
                MakeFlapSprite(), 17, 0.42f);
            DigVisualKit.ApplyLit(flap);

            // Bedroll hints outside
            var bed = MakeSpriteGo("Bedrolls", TentDoor + new Vector2(0.55f, -0.35f),
                MakeBedrollSprite(), 15, 0.55f);
            DigVisualKit.ApplyLit(bed);

            // Firepit ring + logs
            var pit = MakeSpriteGo("FirePit", FirePos, MakeFirePitSprite(), 15, 0.48f);
            DigVisualKit.ApplyLit(pit);

            var flame = MakeSpriteGo("Flame", FirePos + new Vector2(0f, 0.06f),
                MakeFlameSprite(), 18, 0.32f);
            DigVisualKit.ApplyLit(flame);
            flame.gameObject.AddComponent<CampFireFlicker>().Bind(flame);

            // Warm fire light (no 2D shadows — keep lanterns cheap)
            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = FirePos + new Vector2(0f, 0.05f);
            var light = lightGo.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(light,
                new Color(1f, 0.45f, 0.18f),
                intensity: 1.55f,
                outer: 2.4f,
                inner: 0.08f,
                shadows: false,
                falloff: 0.68f);
            lightGo.AddComponent<CosyLantern>().Init(lightGo.transform.position, light, 1.55f);

            // Small supply crate by tent
            var crate = MakeSpriteGo("Crate", TentDoor + new Vector2(0.85f, -0.55f),
                MakeCrateSprite(), 14, 0.28f);
            DigVisualKit.ApplyLit(crate);
        }

        SpriteRenderer MakeSpriteGo(string name, Vector2 local, Sprite sprite, int order, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        static Sprite MakePadSprite()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - 31.5f) / 30f;
                float dy = (y - 28f) / 22f;
                float d = dx * dx + dy * dy;
                if (d > 1f) { tex.SetPixel(x, y, Color.clear); continue; }
                float n = Mathf.PerlinNoise(x * 0.18f, y * 0.18f);
                var c = Color.Lerp(new Color(0.32f, 0.24f, 0.14f, 0.9f),
                    new Color(0.42f, 0.32f, 0.18f, 0.88f), n);
                if (d > 0.82f) c.a *= 0.55f;
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.4f), s);
        }

        static Sprite MakeTentSprite()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, Color.clear);

            // Canvas triangle
            for (int y = 8; y < 56; y++)
            {
                float t = (y - 8) / 48f;
                int half = Mathf.RoundToInt(Mathf.Lerp(2, 26, t));
                int mid = 32;
                for (int x = mid - half; x <= mid + half; x++)
                {
                    float edge = Mathf.Abs(x - mid) / (float)Mathf.Max(1, half);
                    Color c;
                    if (edge > 0.88f)
                        c = new Color(0.22f, 0.18f, 0.12f, 0.95f); // seam
                    else if (y < 18)
                        c = new Color(0.45f, 0.28f, 0.12f, 0.95f); // peak
                    else
                        c = Color.Lerp(new Color(0.55f, 0.38f, 0.18f, 0.94f),
                            new Color(0.4f, 0.26f, 0.12f, 0.94f), edge);
                    // stripe
                    if ((y + x) % 9 == 0) c *= 0.9f;
                    tex.SetPixel(x, y, c);
                }
            }
            // Pole
            for (int y = 6; y < 58; y++)
            for (int x = 31; x <= 33; x++)
                tex.SetPixel(x, y, new Color(0.25f, 0.18f, 0.1f, 1f));

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.15f), s);
        }

        static Sprite MakeFlapSprite()
        {
            const int s = 24;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                if (x < 4 || x > 19 || y < 2 || y > 20) { tex.SetPixel(x, y, Color.clear); continue; }
                tex.SetPixel(x, y, new Color(0.18f, 0.12f, 0.08f, 0.92f));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.2f), s);
        }

        static Sprite MakeBedrollSprite()
        {
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, Color.clear);
            for (int i = 0; i < 2; i++)
            {
                int y0 = 8 + i * 10;
                for (int y = y0; y < y0 + 7; y++)
                for (int x = 4; x < 28; x++)
                {
                    var c = i == 0
                        ? new Color(0.25f, 0.35f, 0.4f, 0.9f)
                        : new Color(0.35f, 0.28f, 0.18f, 0.9f);
                    if (x < 6 || x > 25) c *= 0.7f;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        static Sprite MakeFirePitSprite()
        {
            const int s = 40;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - 19.5f) / 16f;
                float dy = (y - 19.5f) / 16f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f || d < 0.45f) { tex.SetPixel(x, y, Color.clear); continue; }
                // Stone ring
                float a = Mathf.Atan2(dy, dx);
                float bump = 0.5f + 0.5f * Mathf.Sin(a * 5f);
                var c = Color.Lerp(new Color(0.35f, 0.32f, 0.28f), new Color(0.5f, 0.45f, 0.38f), bump);
                tex.SetPixel(x, y, new Color(c.r, c.g, c.b, 0.95f));
            }
            // Ash center
            for (int y = 14; y < 26; y++)
            for (int x = 14; x < 26; x++)
            {
                float dx = (x - 19.5f) / 6f, dy = (y - 19.5f) / 6f;
                if (dx * dx + dy * dy > 1f) continue;
                tex.SetPixel(x, y, new Color(0.12f, 0.1f, 0.08f, 0.85f));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        static Sprite MakeFlameSprite()
        {
            const int s = 24;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - 11.5f) / 6f;
                float dy = (y - 6f) / 14f;
                float d = dx * dx + dy * dy * 0.55f;
                if (d > 1f || y < 2) { tex.SetPixel(x, y, Color.clear); continue; }
                float t = 1f - d;
                var c = Color.Lerp(new Color(1f, 0.25f, 0.05f, 0.7f),
                    new Color(1f, 0.85f, 0.35f, 0.95f), t);
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.15f), s);
        }

        static Sprite MakeCrateSprite()
        {
            const int s = 20;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                if (x < 2 || y < 2 || x > 17 || y > 17)
                    tex.SetPixel(x, y, new Color(0.2f, 0.14f, 0.08f, 1f));
                else
                    tex.SetPixel(x, y, new Color(0.42f, 0.3f, 0.16f, 1f));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }
    }

    /// <summary>Subtle flame scale pulse.</summary>
    public sealed class CampFireFlicker : MonoBehaviour
    {
        SpriteRenderer _sr;
        Vector3 _baseScale;
        float _t;

        public void Bind(SpriteRenderer sr)
        {
            _sr = sr;
            _baseScale = sr.transform.localScale;
            _t = Random.value * 10f;
        }

        void Update()
        {
            if (_sr == null) return;
            _t += Time.deltaTime * 7f;
            float s = 1f + 0.12f * Mathf.Sin(_t) + 0.06f * Mathf.Sin(_t * 2.3f);
            _sr.transform.localScale = _baseScale * s;
            float a = 0.75f + 0.2f * Mathf.Sin(_t * 1.7f);
            var c = _sr.color;
            c.a = a;
            _sr.color = c;
        }
    }
}
