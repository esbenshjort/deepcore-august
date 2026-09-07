using System;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Small floor dust kicked while walking — readable in lantern light, still subtle.
    /// </summary>
    public static class FootstepDustFx
    {
        static Sprite _puff;

        public static FootstepDust Attach(Transform mover, Func<bool> canEmit = null)
        {
            if (mover == null) return null;
            var existing = mover.GetComponent<FootstepDust>();
            if (existing != null)
            {
                existing.Configure(canEmit);
                return existing;
            }
            var fx = mover.gameObject.AddComponent<FootstepDust>();
            fx.Configure(canEmit);
            return fx;
        }

        public static void Spawn(Transform parent, Vector2 worldPos, Vector2 moveDir)
        {
            var go = new GameObject("FootDust");
            if (parent != null)
                go.transform.SetParent(parent, true);
            Vector2 side = new(-moveDir.y, moveDir.x);
            if (side.sqrMagnitude < 0.0001f) side = Vector2.right;
            else side.Normalize();
            Vector2 dirN = moveDir.sqrMagnitude > 0.0001f ? moveDir.normalized : Vector2.up;
            Vector2 pos = worldPos
                - dirN * UnityEngine.Random.Range(0.02f, 0.06f)
                + side * UnityEngine.Random.Range(-0.05f, 0.05f)
                + Vector2.down * 0.04f;
            go.transform.position = pos;
            float s = UnityEngine.Random.Range(0.1f, 0.18f);
            go.transform.localScale = new Vector3(
                s * UnityEngine.Random.Range(0.9f, 1.35f), s * 0.65f, 1f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PuffSprite;
            sr.sortingOrder = 18;
            DigVisualKit.ApplyUnlit(sr);
            float g = UnityEngine.Random.Range(0.32f, 0.48f);
            sr.color = new Color(g * 1.08f, g * 0.88f, g * 0.68f,
                UnityEngine.Random.Range(0.28f, 0.42f));

            go.AddComponent<FootstepDustPuff>().Init(
                life: UnityEngine.Random.Range(0.45f, 0.85f),
                drift: -dirN * UnityEngine.Random.Range(0.03f, 0.1f)
                    + side * UnityEngine.Random.Range(-0.05f, 0.05f)
                    + Vector2.up * UnityEngine.Random.Range(0.015f, 0.05f));
        }

        static Sprite PuffSprite
        {
            get
            {
                if (_puff != null) return _puff;
                const int s = 20;
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
                    float dy = (y - cy) / (s * 0.36f);
                    float d = dx * dx + dy * dy;
                    float n = Mathf.PerlinNoise(x * 0.4f + 2f, y * 0.4f);
                    d -= (n - 0.5f) * 0.4f;
                    if (d > 1f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }
                    float a = Mathf.Clamp01((1f - d) * 1.05f);
                    a *= a;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                tex.Apply();
                _puff = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
                return _puff;
            }
        }
    }

    public sealed class FootstepDust : MonoBehaviour
    {
        Func<bool> _canEmit;
        Vector2 _last;
        float _accum;
        bool _hasLast;
        Transform _fxRoot;

        const float StepDistance = 0.11f;
        const float MinSpeed = 0.18f; // world units / sec

        public void Configure(Func<bool> canEmit)
        {
            _canEmit = canEmit;
        }

        void LateUpdate()
        {
            if (_canEmit != null && !_canEmit())
            {
                _hasLast = false;
                _accum = 0f;
                return;
            }

            Vector2 p = transform.position;
            if (!_hasLast)
            {
                _last = p;
                _hasLast = true;
                return;
            }

            Vector2 delta = p - _last;
            float dist = delta.magnitude;
            float speed = Time.deltaTime > 0.0001f ? dist / Time.deltaTime : 0f;
            _last = p;

            if (speed < MinSpeed || dist < 0.0004f)
            {
                _accum *= 0.82f;
                return;
            }

            _accum += dist;
            while (_accum >= StepDistance)
            {
                _accum -= StepDistance;
                if (_fxRoot == null)
                {
                    // Prefer world/root parent so dust doesn't spin with facing
                    _fxRoot = transform.root != null ? transform.root : transform;
                    if (_fxRoot == transform && transform.parent != null)
                        _fxRoot = transform.parent;
                }
                FootstepDustFx.Spawn(_fxRoot, p, delta);
            }
        }
    }

    sealed class FootstepDustPuff : MonoBehaviour
    {
        float _life;
        float _age;
        Vector2 _drift;
        Vector3 _startScale;
        SpriteRenderer _sr;
        Color _c0;

        public void Init(float life, Vector2 drift)
        {
            _life = Mathf.Max(0.1f, life);
            _drift = drift;
            _startScale = transform.localScale;
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _c0 = _sr.color;
        }

        void Update()
        {
            _age += Time.deltaTime;
            float u = Mathf.Clamp01(_age / _life);
            transform.position += (Vector3)(_drift * Time.deltaTime);
            _drift *= 1f - 1.6f * Time.deltaTime;
            transform.localScale = _startScale * (1f + u * 0.9f);
            if (_sr != null)
            {
                var c = _c0;
                c.a = _c0.a * (1f - u) * (1f - u * 0.5f);
                _sr.color = c;
            }
            if (u >= 1f) Destroy(gameObject);
        }
    }
}
