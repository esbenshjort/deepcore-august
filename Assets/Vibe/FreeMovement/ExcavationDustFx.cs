using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Dirty haze in excavated cells — subtle mine atmosphere that fades slowly.
    /// Tuned to read in a busy dig without becoming a fog bank.
    /// </summary>
    public static class ExcavationDustFx
    {
        static Sprite _haze;

        public static void SpawnLingering(Transform parent, Vector2 worldPos, float cellSize,
            bool heavy = false)
        {
            if (parent == null) return;
            float baseSize = Mathf.Max(0.26f, cellSize * 3.1f);
            float mul = heavy ? Random.Range(1.05f, 1.35f) : Random.Range(0.9f, 1.15f);
            SpawnOne(parent, worldPos, baseSize, mul, heavy);
            // Extra soft puff so busy digs feel dusty without stacking opacity hard
            if (heavy && Random.value < 0.55f)
            {
                Vector2 offset = worldPos + Random.insideUnitCircle * (cellSize * 0.85f);
                SpawnOne(parent, offset, baseSize * 0.75f, mul * 0.85f, heavy: false);
            }
        }

        static void SpawnOne(Transform parent, Vector2 worldPos, float baseSize, float mul, bool heavy)
        {
            var go = new GameObject(heavy ? "ExcavDustHeavy" : "ExcavDust");
            go.transform.SetParent(parent, false);
            Vector3 pos = (Vector3)worldPos
                + (Vector3)(Random.insideUnitCircle * (baseSize * (heavy ? 0.28f : 0.18f)));
            pos.y -= Random.Range(0.01f, 0.06f);
            go.transform.position = pos;
            float sx = baseSize * Random.Range(0.65f, 1.05f) * mul;
            float sy = baseSize * Random.Range(0.45f, 0.8f) * mul;
            go.transform.localScale = new Vector3(sx, sy, 1f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-22f, 22f));

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = HazeSprite;
            sr.sortingOrder = heavy ? 16 : 14;
            DigVisualKit.ApplyUnlit(sr);
            float g = heavy ? Random.Range(0.24f, 0.36f) : Random.Range(0.28f, 0.4f);
            // Readable air — still restrained
            float a = heavy ? Random.Range(0.18f, 0.28f) : Random.Range(0.12f, 0.2f);
            sr.color = new Color(g * 1.1f, g * 0.88f, g * 0.66f, a);

            go.AddComponent<ExcavationDustHaze>().Init(
                life: heavy ? Random.Range(8f, 14f) : Random.Range(7f, 12f),
                grow: heavy ? Random.Range(0.3f, 0.55f) : Random.Range(0.22f, 0.42f),
                drift: Random.insideUnitCircle * Random.Range(0.005f, 0.02f)
                    + Vector2.up * Random.Range(0.003f, 0.01f));
        }

        static Sprite HazeSprite
        {
            get
            {
                if (_haze != null) return _haze;
                const int s = 40;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                float cx = (s - 1) * 0.5f, cy = (s - 1) * 0.5f;
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - cx) / (s * 0.48f);
                    float dy = (y - cy) / (s * 0.4f);
                    float d = dx * dx + dy * dy;
                    float n = Mathf.PerlinNoise(x * 0.2f + 1.7f, y * 0.2f + 3.1f);
                    d -= (n - 0.5f) * 0.5f;
                    if (d > 1f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }
                    float a = Mathf.Clamp01((1f - d) * 0.78f);
                    a *= a * (0.45f + n * 0.4f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                tex.Apply();
                _haze = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
                return _haze;
            }
        }
    }

    sealed class ExcavationDustHaze : MonoBehaviour
    {
        float _life;
        float _age;
        float _grow;
        Vector2 _drift;
        Vector3 _startScale;
        SpriteRenderer _sr;
        Color _c0;
        float _spin;

        public void Init(float life, float grow, Vector2 drift)
        {
            _life = Mathf.Max(1f, life);
            _grow = grow;
            _drift = drift;
            _startScale = transform.localScale;
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _c0 = _sr.color;
            _spin = Random.Range(-3f, 3f);
        }

        void Update()
        {
            _age += Time.deltaTime;
            float u = Mathf.Clamp01(_age / _life);
            float fade = u < 0.3f ? 1f : 1f - ((u - 0.3f) / 0.7f);
            fade = fade * fade * (3f - 2f * fade);
            transform.position += (Vector3)(_drift * Time.deltaTime);
            _drift *= 1f - 0.32f * Time.deltaTime;
            transform.localScale = _startScale * (1f + u * _grow);
            transform.Rotate(0f, 0f, _spin * Time.deltaTime);
            if (_sr != null)
            {
                var c = _c0;
                c.a = _c0.a * fade;
                _sr.color = c;
            }
            if (u >= 1f) Destroy(gameObject);
        }
    }
}
