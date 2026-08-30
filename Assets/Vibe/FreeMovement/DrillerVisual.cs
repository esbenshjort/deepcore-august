using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Drill points with digger. Dig dust + heat smoke (dark grey) + overheat sparks.
    /// Smoke sits between floor and rock-wall layers, drifts back into open tunnel only.
    /// Subtle green status bulbs pulse on the hazard suit.
    /// </summary>
    public sealed class DrillerVisual : MonoBehaviour
    {
        const int SmokeSortOrder = 43; // above drill (41)
        const int SparkSortOrder = 45;

        FreeWorkerController _worker;
        Transform _drill;
        Transform _body;
        Transform _smokeRoot;
        Sprite _smokeSprite;
        Sprite _sparkSprite;
        float _digSmokeTimer;
        float _heatSmokeTimer;
        float _sparkTimer;
        Vector3 _bodyBase;
        Vector3 _drillBase;
        Light2D[] _statusBulbs;
        float[] _bulbBaseIntensity;

        public void Init(FreeWorkerController worker, Transform drill, Transform body,
            Light2D[] statusBulbs = null)
        {
            _worker = worker;
            _drill = drill;
            _body = body;
            _bodyBase = body != null ? body.localPosition : Vector3.zero;
            _drillBase = drill != null ? drill.localPosition : Vector3.zero;
            if (_drill != null)
                _drill.localRotation = Quaternion.identity;

            _statusBulbs = statusBulbs;
            if (_statusBulbs != null)
            {
                _bulbBaseIntensity = new float[_statusBulbs.Length];
                for (int i = 0; i < _statusBulbs.Length; i++)
                    _bulbBaseIntensity[i] = _statusBulbs[i] != null ? _statusBulbs[i].intensity : 0.14f;
            }

            // World-root smoke so it doesn't rotate with the digger mid-flight
            _smokeRoot = new GameObject("DrillSmoke").transform;
            if (transform.parent != null)
                _smokeRoot.SetParent(transform.parent, false);
            else
                _smokeRoot.SetParent(null, false);

            _smokeSprite = MakeSmokeSprite();
            _sparkSprite = MakeSparkSprite();
        }

        void OnDestroy()
        {
            if (_smokeRoot != null)
                Destroy(_smokeRoot.gameObject);
        }

        void LateUpdate()
        {
            if (_drill == null || _worker == null) return;

            _drill.localRotation = Quaternion.identity;

            bool digging = _worker.IsActivelyDigging;
            var heat = _worker.HeatState;
            bool heatFx = heat >= ExcavatorHeatZone.Optimal;

            if (digging || heat == ExcavatorHeatZone.Overheated)
            {
                float shake = heat == ExcavatorHeatZone.Overheated ? 1.35f : 1f;
                float t = Time.time * 28f;
                Vector2 j = new(Mathf.Sin(t) * 0.007f * shake, Mathf.Cos(t * 1.3f) * 0.01f * shake);
                if (_body != null)
                    _body.localPosition = _bodyBase + (Vector3)(j * 0.4f);
                _drill.localPosition = _drillBase + (Vector3)j + Vector3.up * (Mathf.Sin(t) * 0.01f);
            }
            else
            {
                if (_body != null)
                    _body.localPosition = Vector3.Lerp(_body.localPosition, _bodyBase, 14f * Time.deltaTime);
                _drill.localPosition = Vector3.Lerp(_drill.localPosition, _drillBase, 14f * Time.deltaTime);
            }

            TickStatusBulbs(digging, heat);

            if (digging)
            {
                _digSmokeTimer -= Time.deltaTime;
                if (_digSmokeTimer <= 0f)
                {
                    _digSmokeTimer = Random.Range(0.035f, 0.07f);
                    SpawnDigSmokePuff();
                    if (Random.value < 0.65f)
                        SpawnDigSmokePuff();
                }
            }
            else
                _digSmokeTimer = 0f;

            if (heatFx)
                TickHeatFx(heat);
            else
            {
                _heatSmokeTimer = 0f;
                _sparkTimer = 0f;
            }
        }

        void TickStatusBulbs(bool digging, ExcavatorHeatZone heat)
        {
            if (_statusBulbs == null || _bulbBaseIntensity == null) return;
            float t = Time.time;
            for (int i = 0; i < _statusBulbs.Length; i++)
            {
                var light = _statusBulbs[i];
                if (light == null) continue;
                float baseI = _bulbBaseIntensity[i];
                // Slow independent breath — stay subtle
                float phase = t * (1.1f + i * 0.17f) + i * 1.7f;
                float breath = 0.82f + 0.18f * Mathf.Sin(phase);
                if (digging) breath += 0.06f * Mathf.Sin(t * 9f + i);
                if (heat == ExcavatorHeatZone.Overheated)
                    breath *= 0.7f + 0.3f * Mathf.Sin(t * 14f + i); // nervous flicker
                light.intensity = baseI * breath;
            }
        }

        void TickHeatFx(ExcavatorHeatZone heat)
        {
            // HOT < CRITICAL < OVERHEATED spawn rate / darkness
            float interval = heat switch
            {
                ExcavatorHeatZone.Overheated => Random.Range(0.028f, 0.05f),
                ExcavatorHeatZone.Extreme => Random.Range(0.045f, 0.08f),
                ExcavatorHeatZone.Danger => Random.Range(0.055f, 0.09f),
                _ => Random.Range(0.07f, 0.12f), // Optimal
            };
            int bursts = heat switch
            {
                ExcavatorHeatZone.Overheated => 3,
                ExcavatorHeatZone.Extreme => 2,
                ExcavatorHeatZone.Danger => 2,
                _ => 1,
            };

            _heatSmokeTimer -= Time.deltaTime;
            if (_heatSmokeTimer <= 0f)
            {
                _heatSmokeTimer = interval;
                for (int i = 0; i < bursts; i++)
                    SpawnHeatSmokePuff(heat);
            }

            if (heat == ExcavatorHeatZone.Overheated)
            {
                _sparkTimer -= Time.deltaTime;
                if (_sparkTimer <= 0f)
                {
                    _sparkTimer = Random.Range(0.04f, 0.09f);
                    SpawnElectricSpark();
                    if (Random.value < 0.55f)
                        SpawnElectricSpark();
                }
            }
        }

        void SpawnDigSmokePuff()
        {
            // Classic dig dust at the bit tip (unchanged placement).
            Vector3 tip = _drill != null
                ? _drill.TransformPoint(Vector3.up * 0.52f)
                : transform.position;
            tip += (Vector3)(Random.insideUnitCircle * 0.03f);

            SpawnSmokeInternal(
                origin: tip,
                color: DigSmokeColor(),
                scale: Random.Range(0.16f, 0.28f),
                life: Random.Range(0.45f, 0.75f),
                grow: Random.Range(1.35f, 2.0f),
                driftMul: 1f);
        }

        void SpawnHeatSmokePuff(ExcavatorHeatZone heat)
        {
            float scale = heat switch
            {
                ExcavatorHeatZone.Overheated => Random.Range(0.22f, 0.38f),
                ExcavatorHeatZone.Extreme => Random.Range(0.18f, 0.32f),
                ExcavatorHeatZone.Danger => Random.Range(0.16f, 0.28f),
                _ => Random.Range(0.14f, 0.24f),
            };
            float life = heat switch
            {
                ExcavatorHeatZone.Overheated => Random.Range(0.55f, 0.9f),
                ExcavatorHeatZone.Extreme => Random.Range(0.5f, 0.8f),
                ExcavatorHeatZone.Danger => Random.Range(0.45f, 0.75f),
                _ => Random.Range(0.4f, 0.7f),
            };

            // Heat exhaust from the machine body — separate from tip dig dust.
            Vector3 body = _body != null ? _body.position : transform.position;
            body += (Vector3)(Random.insideUnitCircle * 0.05f);
            if (_worker != null)
                body += (Vector3)(-_worker.Facing * 0.04f); // slightly toward the chassis

            SpawnSmokeInternal(
                origin: body,
                color: HeatSmokeColor(heat),
                scale: scale,
                life: life,
                grow: Random.Range(1.4f, 2.2f),
                driftMul: heat == ExcavatorHeatZone.Overheated ? 1.25f : 1f);
        }

        void SpawnSmokeInternal(Vector3 origin, Color color, float scale, float life, float grow, float driftMul)
        {
            if (_smokeRoot == null || _worker == null) return;

            var go = new GameObject("Smoke");
            go.transform.SetParent(_smokeRoot, false);
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * scale;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _smokeSprite;
            sr.sortingOrder = SmokeSortOrder;
            DigVisualKit.ApplyLit(sr);
            sr.color = color;

            Vector2 back = -_worker.Facing;
            Vector2 side = new(-back.y, back.x);
            Vector2 drift = (back * Random.Range(0.22f, 0.5f)
                + side * Random.Range(-0.25f, 0.25f)) * driftMul;

            go.AddComponent<DrillSmokePuff>().Init(
                _worker.World,
                drift,
                life: life,
                grow: grow);
        }

        void SpawnElectricSpark()
        {
            if (_smokeRoot == null || _drill == null || _worker == null) return;

            Vector3 origin = _body != null ? _body.position : _drill.position;
            origin += (Vector3)(Random.insideUnitCircle * 0.12f);
            origin += (Vector3)(_worker.Facing * Random.Range(-0.05f, 0.15f));

            var go = new GameObject("Spark");
            go.transform.SetParent(_smokeRoot, false);
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * Random.Range(0.06f, 0.12f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sparkSprite;
            sr.sortingOrder = SparkSortOrder;
            DigVisualKit.ApplyLit(sr);
            bool cyan = Random.value > 0.35f;
            sr.color = cyan
                ? new Color(0.55f, 0.9f, 1f, Random.Range(0.75f, 1f))
                : new Color(1f, 0.95f, 0.7f, Random.Range(0.7f, 1f));

            Vector2 kick = Random.insideUnitCircle.normalized * Random.Range(0.35f, 0.9f);
            go.AddComponent<DrillSparkPuff>().Init(kick, life: Random.Range(0.08f, 0.18f));
        }

        static Color DigSmokeColor()
        {
            float g = Random.Range(0.4f, 0.58f);
            return new Color(g, g * 0.92f, g * 0.85f, Random.Range(0.38f, 0.55f));
        }

        static Color HeatSmokeColor(ExcavatorHeatZone heat)
        {
            // Dark grey — hotter = darker / denser
            float g = heat switch
            {
                ExcavatorHeatZone.Overheated => Random.Range(0.12f, 0.22f),
                ExcavatorHeatZone.Extreme => Random.Range(0.16f, 0.28f),
                ExcavatorHeatZone.Danger => Random.Range(0.18f, 0.30f),
                _ => Random.Range(0.22f, 0.34f),
            };
            float a = heat switch
            {
                ExcavatorHeatZone.Overheated => Random.Range(0.5f, 0.72f),
                ExcavatorHeatZone.Extreme => Random.Range(0.42f, 0.6f),
                ExcavatorHeatZone.Danger => Random.Range(0.36f, 0.52f),
                _ => Random.Range(0.32f, 0.48f),
            };
            return new Color(g, g, g * 1.02f, a);
        }

        static Sprite MakeSmokeSprite()
        {
            const int s = 28;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float cx = (s - 1) * 0.5f, cy = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - cx) / (s * 0.42f);
                float dy = (y - cy) / (s * 0.42f);
                float d = dx * dx + dy * dy;
                float n = Mathf.PerlinNoise(x * 0.3f + 2f, y * 0.3f);
                d -= (n - 0.5f) * 0.4f;
                if (d > 1f) { tex.SetPixel(x, y, Color.clear); continue; }
                float a = Mathf.Clamp01((1f - d) * 0.95f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        static Sprite MakeSparkSprite()
        {
            const int s = 12;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float cx = (s - 1) * 0.5f, cy = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = Mathf.Abs(x - cx);
                float dy = Mathf.Abs(y - cy);
                // Thin cross / star
                bool arm = (dx < 1.2f && dy < s * 0.48f) || (dy < 1.2f && dx < s * 0.48f);
                if (!arm) { tex.SetPixel(x, y, Color.clear); continue; }
                float a = 1f - Mathf.Max(dx, dy) / (s * 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }
    }

    sealed class DrillSmokePuff : MonoBehaviour
    {
        FineTerrainWorld _world;
        Vector2 _drift;
        float _life;
        float _age;
        float _grow;
        float _spin;
        Vector3 _startScale;
        SpriteRenderer _sr;
        Color _c0;

        public void Init(FineTerrainWorld world, Vector2 drift, float life, float grow = 1.4f)
        {
            _world = world;
            _drift = drift;
            _life = Mathf.Max(0.1f, life);
            _grow = grow;
            _spin = Random.Range(-60f, 60f);
            _startScale = transform.localScale;
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _c0 = _sr.color;
        }

        void Update()
        {
            _age += Time.deltaTime;
            float u = Mathf.Clamp01(_age / _life);

            Vector3 next = transform.position + (Vector3)(_drift * Time.deltaTime);

            if (_world != null)
            {
                var cell = _world.WorldToCell(next);
                if (!_world.InBounds(cell.x, cell.y) || !_world.IsTunnelOpen(cell.x, cell.y))
                {
                    u = Mathf.Max(u, 0.75f);
                    _age = _life * u;
                    _drift *= 0.15f;
                }
                else
                    transform.position = next;
            }
            else
                transform.position = next;

            _drift *= 1f - 0.7f * Time.deltaTime;
            transform.localScale = _startScale * (1f + u * _grow);
            transform.Rotate(0f, 0f, _spin * Time.deltaTime * (1f - u));

            if (_sr != null)
            {
                var c = _c0;
                c.a = _c0.a * (1f - u) * (1f - u);
                _sr.color = c;
            }
            if (u >= 1f) Destroy(gameObject);
        }
    }

    sealed class DrillSparkPuff : MonoBehaviour
    {
        Vector2 _vel;
        float _life;
        float _age;
        Vector3 _startScale;
        SpriteRenderer _sr;
        Color _c0;

        public void Init(Vector2 velocity, float life)
        {
            _vel = velocity;
            _life = Mathf.Max(0.05f, life);
            _startScale = transform.localScale;
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _c0 = _sr.color;
        }

        void Update()
        {
            _age += Time.deltaTime;
            float u = Mathf.Clamp01(_age / _life);
            transform.position += (Vector3)(_vel * Time.deltaTime);
            _vel *= 1f - 4f * Time.deltaTime;
            transform.localScale = _startScale * (1f - u * 0.7f);
            transform.Rotate(0f, 0f, 720f * Time.deltaTime);

            if (_sr != null)
            {
                var c = _c0;
                c.a = _c0.a * (1f - u);
                _sr.color = c;
            }
            if (u >= 1f) Destroy(gameObject);
        }
    }
}
