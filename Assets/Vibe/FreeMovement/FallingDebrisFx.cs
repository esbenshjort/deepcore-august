using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Falling rock / collapse dust — extends excavation dust language, no duplicate fog system.
    /// </summary>
    public static class FallingDebrisFx
    {
        static Sprite _rock;
        static Sprite _rubble;

        public static void SpawnCracking(Vector2 worldPos, float cellSize, Transform parent)
        {
            if (parent == null) return;
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = worldPos + Random.insideUnitCircle * (cellSize * 0.9f);
                SpawnPebble(parent, p, cellSize * Random.Range(0.15f, 0.32f), soft: true);
            }
            ExcavationDustFx.SpawnLingering(parent, worldPos, cellSize, heavy: false);
        }

        public static Transform SpawnCollapse(
            Vector2 epicenter,
            float cellSize,
            CollapseSeverity severity,
            Transform parent,
            bool persistentRubble)
        {
            var root = new GameObject($"CollapseFX_{(int)severity}").transform;
            root.SetParent(parent, false);
            root.position = epicenter;

            int pebbles = severity switch
            {
                CollapseSeverity.Major => 18,
                CollapseSeverity.Blocking => 12,
                _ => 6,
            };
            for (int i = 0; i < pebbles; i++)
            {
                Vector2 p = epicenter + Random.insideUnitCircle * (cellSize * (severity == CollapseSeverity.Major ? 1.6f : 1.1f));
                SpawnPebble(root, p, cellSize * Random.Range(0.2f, 0.55f), soft: false);
            }

            ExcavationDustFx.SpawnLingering(parent, epicenter, cellSize, heavy: true);

            if (persistentRubble)
            {
                var rubble = new GameObject("RubbleBlock").transform;
                rubble.SetParent(root, false);
                rubble.localPosition = Vector3.zero;
                float s = cellSize * (severity == CollapseSeverity.Major ? 2.4f : 1.7f);
                rubble.localScale = new Vector3(s, s * 0.85f, 1f);
                var sr = rubble.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = RubbleSprite;
                sr.sortingOrder = 11;
                DigVisualKit.ApplyLit(sr);
                sr.color = new Color(0.32f, 0.28f, 0.24f, 0.92f);
            }

            return root;
        }

        static void SpawnPebble(Transform parent, Vector2 worldPos, float size, bool soft)
        {
            var go = new GameObject(soft ? "CrackPebble" : "FallPebble");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos + Vector2.up * Random.Range(0.4f, 1.1f);
            go.transform.localScale = Vector3.one * size;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = RockSprite;
            sr.sortingOrder = 18;
            DigVisualKit.ApplyUnlit(sr);
            float g = Random.Range(0.22f, 0.42f);
            sr.color = new Color(g * 1.05f, g * 0.92f, g * 0.78f, soft ? 0.55f : 0.95f);
            go.AddComponent<FallingPebbleMotion>().Init(
                fall: Random.Range(1.6f, 3.2f),
                life: soft ? Random.Range(0.35f, 0.7f) : Random.Range(0.55f, 1.1f),
                drift: Random.insideUnitCircle * 0.4f);
        }

        static Sprite RockSprite
        {
            get
            {
                if (_rock != null) return _rock;
                const int s = 10;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - 4.5f) / 4.2f;
                    float dy = (y - 4.5f) / 4.2f;
                    float d = dx * dx + dy * dy;
                    tex.SetPixel(x, y, d > 1f ? Color.clear : new Color(0.45f, 0.4f, 0.35f, 1f));
                }
                tex.Apply();
                _rock = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
                return _rock;
            }
        }

        static Sprite RubbleSprite
        {
            get
            {
                if (_rubble != null) return _rubble;
                const int s = 32;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.18f, y * 0.18f);
                    float dx = (x - 15.5f) / 15f;
                    float dy = (y - 15.5f) / 14f;
                    float d = dx * dx + dy * dy * 1.1f;
                    if (d > 1f || n < 0.28f) { tex.SetPixel(x, y, Color.clear); continue; }
                    float g = 0.18f + n * 0.35f;
                    tex.SetPixel(x, y, new Color(g * 1.1f, g * 0.95f, g * 0.8f, 0.9f));
                }
                tex.Apply();
                _rubble = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
                return _rubble;
            }
        }
    }

    sealed class FallingPebbleMotion : MonoBehaviour
    {
        float _vy;
        float _life;
        float _age;
        Vector2 _drift;
        SpriteRenderer _sr;
        Color _c0;

        public void Init(float fall, float life, Vector2 drift)
        {
            _vy = -fall;
            _life = life;
            _drift = drift;
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _c0 = _sr.color;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            transform.position += (Vector3)(_drift * dt + new Vector2(0f, _vy * dt));
            _vy -= 6f * dt;
            if (_sr != null)
            {
                float a = 1f - Mathf.Clamp01(_age / _life);
                _sr.color = new Color(_c0.r, _c0.g, _c0.b, _c0.a * a);
            }
            if (_age >= _life)
                Destroy(gameObject);
        }
    }
}
