using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Drill points with digger. Smoke sits between floor and rock-wall layers,
    /// drifts back into open tunnel only, and dies if it enters solid rock.
    /// </summary>
    public sealed class DrillerVisual : MonoBehaviour
    {
        const int SmokeSortOrder = 43; // above drill (41), reads as dust from the tip

        FreeWorkerController _worker;
        Transform _drill;
        Transform _body;
        Transform _smokeRoot;
        Sprite _smokeSprite;
        float _smokeTimer;
        Vector3 _bodyBase;
        Vector3 _drillBase;

        public void Init(FreeWorkerController worker, Transform drill, Transform body)
        {
            _worker = worker;
            _drill = drill;
            _body = body;
            _bodyBase = body != null ? body.localPosition : Vector3.zero;
            _drillBase = drill != null ? drill.localPosition : Vector3.zero;
            if (_drill != null)
                _drill.localRotation = Quaternion.identity;

            // World-root smoke so it doesn't rotate with the digger mid-flight
            _smokeRoot = new GameObject("DrillSmoke").transform;
            if (transform.parent != null)
                _smokeRoot.SetParent(transform.parent, false);
            else
                _smokeRoot.SetParent(null, false);

            _smokeSprite = MakeSmokeSprite();
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
            if (digging)
            {
                float t = Time.time * 28f;
                Vector2 j = new(Mathf.Sin(t) * 0.007f, Mathf.Cos(t * 1.3f) * 0.01f);
                if (_body != null)
                    _body.localPosition = _bodyBase + (Vector3)(j * 0.4f);
                _drill.localPosition = _drillBase + (Vector3)j + Vector3.up * (Mathf.Sin(t) * 0.01f);

                _smokeTimer -= Time.deltaTime;
                if (_smokeTimer <= 0f)
                {
                    _smokeTimer = Random.Range(0.035f, 0.07f);
                    SpawnSmokePuff();
                    if (Random.value < 0.65f)
                        SpawnSmokePuff();
                }
            }
            else
            {
                if (_body != null)
                    _body.localPosition = Vector3.Lerp(_body.localPosition, _bodyBase, 14f * Time.deltaTime);
                _drill.localPosition = Vector3.Lerp(_drill.localPosition, _drillBase, 14f * Time.deltaTime);
                _smokeTimer = 0f;
            }
        }

        void SpawnSmokePuff()
        {
            if (_smokeRoot == null || _drill == null || _worker == null) return;

            // Tip of the drill bit (sprite pivot is near the motor / base)
            Vector3 tipWorld = _drill.TransformPoint(Vector3.up * 0.52f);
            tipWorld += (Vector3)(Random.insideUnitCircle * 0.03f);

            var go = new GameObject("Smoke");
            go.transform.SetParent(_smokeRoot, false);
            go.transform.position = tipWorld;
            go.transform.localScale = Vector3.one * Random.Range(0.16f, 0.28f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _smokeSprite;
            sr.sortingOrder = SmokeSortOrder;
            DigVisualKit.ApplyLit(sr);

            float g = Random.Range(0.4f, 0.58f);
            sr.color = new Color(g, g * 0.92f, g * 0.85f, Random.Range(0.38f, 0.55f));

            // Drift BACK into open tunnel (−facing), not into the rock face
            Vector2 back = -_worker.Facing;
            Vector2 side = new(-back.y, back.x);
            Vector2 drift = back * Random.Range(0.22f, 0.5f)
                + side * Random.Range(-0.25f, 0.25f);

            go.AddComponent<DrillSmokePuff>().Init(
                _worker.World,
                drift,
                life: Random.Range(0.45f, 0.75f),
                grow: Random.Range(1.35f, 2.0f));
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

            // Only travel in excavated tunnel — stop at rock / unopened space
            if (_world != null)
            {
                var cell = _world.WorldToCell(next);
                if (!_world.InBounds(cell.x, cell.y) || !_world.IsExcavated(cell.x, cell.y))
                {
                    // Soft kill at the dig face / wall
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
}
