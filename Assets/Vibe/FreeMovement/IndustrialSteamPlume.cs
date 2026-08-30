using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Soft billowing steam from industrial vents — stacked translucent puffs that
    /// rise, drift, expand, and dissolve. No ParticleSystem dependency.
    /// </summary>
    public sealed class IndustrialSteamPlume : MonoBehaviour
    {
        struct Puff
        {
            public Transform Tr;
            public SpriteRenderer Sr;
            public float Age;
            public float Life;
            public float Speed;
            public float Drift;
            public float WobblePhase;
            public float StartScale;
            public float EndScale;
            public float StartAlpha;
            public Vector2 Origin;
            public bool Alive;
        }

        Puff[] _puffs;
        float _emitAcc;
        float _rate = 14f;
        float _burst = 1f;
        bool _emitting;
        Vector2 _dir = Vector2.up;
        Color _tint = new(0.92f, 0.96f, 1f, 1f);
        float _spread = 0.12f;
        float _lift = 0.55f;
        int _sort = 30;
        static Sprite _puffSprite;
        static Material _unlit;

        public bool Emitting
        {
            get => _emitting;
            set => _emitting = value;
        }

        public float Rate
        {
            get => _rate;
            set => _rate = Mathf.Max(0.5f, value);
        }

        /// <summary>0–1 intensity multiplier (volume + opacity).</summary>
        public float Burst
        {
            get => _burst;
            set => _burst = Mathf.Clamp01(value);
        }

        public static IndustrialSteamPlume Attach(Transform parent, Vector2 localPos,
            int poolSize = 28, int sortingOrder = 30)
        {
            var go = new GameObject("SteamPlume");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var plume = go.AddComponent<IndustrialSteamPlume>();
            plume._sort = sortingOrder;
            plume.BuildPool(poolSize);
            return plume;
        }

        public IndustrialSteamPlume Configure(
            Vector2 riseDir,
            Color tint,
            float rate = 14f,
            float spread = 0.12f,
            float lift = 0.55f)
        {
            _dir = riseDir.sqrMagnitude > 0.01f ? riseDir.normalized : Vector2.up;
            _tint = tint;
            _rate = rate;
            _spread = spread;
            _lift = lift;
            return this;
        }

        void BuildPool(int n)
        {
            EnsureSprites();
            _puffs = new Puff[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject($"Puff{i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _puffSprite;
                sr.sharedMaterial = _unlit;
                sr.sortingOrder = _sort;
                sr.color = Color.clear;
                go.SetActive(false);
                _puffs[i] = new Puff { Tr = go.transform, Sr = sr };
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_emitting && _burst > 0.02f)
            {
                _emitAcc += dt * _rate * _burst;
                while (_emitAcc >= 1f)
                {
                    _emitAcc -= 1f;
                    EmitOne();
                }
            }
            else
            {
                _emitAcc = 0f;
            }

            for (int i = 0; i < _puffs.Length; i++)
            {
                if (!_puffs[i].Alive) continue;
                ref var p = ref _puffs[i];
                p.Age += dt;
                float u = Mathf.Clamp01(p.Age / p.Life);
                if (u >= 1f)
                {
                    p.Alive = false;
                    p.Tr.gameObject.SetActive(false);
                    continue;
                }

                // Ease out rise — denser near vent, thins as it climbs
                float rise = (1f - (1f - u) * (1f - u)) * _lift * p.Speed;
                float side = Mathf.Sin(p.Age * 2.1f + p.WobblePhase) * p.Drift
                             + Mathf.Sin(p.Age * 0.7f + p.WobblePhase * 1.7f) * p.Drift * 0.45f;
                Vector2 perp = new(-_dir.y, _dir.x);
                Vector2 pos = p.Origin + _dir * rise + perp * side;
                // Soft lateral bloom as steam cools
                pos += perp * (u * u * p.Drift * 0.6f);
                p.Tr.localPosition = pos;

                float s = Mathf.Lerp(p.StartScale, p.EndScale, u * u);
                p.Tr.localScale = Vector3.one * s;

                // Opacity: quick fade-in, long dissolve
                float aIn = Mathf.Clamp01(u / 0.12f);
                float aOut = 1f - Mathf.SmoothStep(0.25f, 1f, u);
                float a = p.StartAlpha * aIn * aOut * _burst;
                // Cooler / thinner toward top
                Color c = Color.Lerp(_tint, new Color(0.85f, 0.9f, 0.95f, 1f), u * 0.55f);
                c.a = a;
                p.Sr.color = c;
                p.Tr.Rotate(0f, 0f, dt * (18f + p.WobblePhase * 4f) * (1f - u * 0.5f));
            }
        }

        void EmitOne()
        {
            int slot = -1;
            for (int i = 0; i < _puffs.Length; i++)
            {
                if (!_puffs[i].Alive) { slot = i; break; }
            }
            if (slot < 0) return;

            ref var p = ref _puffs[slot];
            float jitter = Random.Range(-_spread, _spread);
            Vector2 perp = new(-_dir.y, _dir.x);
            p.Origin = perp * jitter + _dir * Random.Range(-0.02f, 0.02f);
            p.Age = 0f;
            p.Life = Random.Range(0.85f, 1.65f);
            p.Speed = Random.Range(0.85f, 1.25f);
            p.Drift = Random.Range(_spread * 0.55f, _spread * 1.35f);
            p.WobblePhase = Random.Range(0f, Mathf.PI * 2f);
            p.StartScale = Random.Range(0.08f, 0.14f) * (0.7f + _burst * 0.5f);
            p.EndScale = Random.Range(0.32f, 0.55f) * (0.75f + _burst * 0.45f);
            p.StartAlpha = Random.Range(0.22f, 0.42f) * (0.55f + _burst * 0.55f);
            p.Alive = true;
            p.Tr.gameObject.SetActive(true);
            p.Tr.localPosition = p.Origin;
            p.Tr.localScale = Vector3.one * p.StartScale;
            p.Tr.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            var c = _tint;
            c.a = 0f;
            p.Sr.color = c;
        }

        static void EnsureSprites()
        {
            if (_puffSprite != null) return;
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            float cx = (s - 1) * 0.5f, cy = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - cx) / (s * 0.42f);
                float dy = (y - cy) / (s * 0.42f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                // Soft irregular cloud — two overlapping lobes
                float n = Mathf.PerlinNoise(x * 0.18f + 2.1f, y * 0.18f + 1.4f);
                float lobe = d - (n - 0.5f) * 0.35f;
                float a = Mathf.Clamp01(1f - lobe);
                a = a * a * (3f - 2f * a);
                a *= 0.55f + n * 0.45f;
                if (a < 0.02f) { tex.SetPixel(x, y, Color.clear); continue; }
                // Warm core → cool rim for volumetric read
                Color c = Color.Lerp(
                    new Color(1f, 1f, 1f, 1f),
                    new Color(0.82f, 0.9f, 0.98f, 1f),
                    lobe);
                c.a = a;
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            _puffSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);

            var sh = Shader.Find("Sprites/Default");
            _unlit = sh != null ? new Material(sh) : null;
        }
    }
}
