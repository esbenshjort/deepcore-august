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
        Sprite _digConeSprite;
        float _digSmokeTimer;
        float _heatSmokeTimer;
        float _sparkTimer;
        float _rockSparkTimer;
        float _rockChipTimer;
        float _floorDustTimer;
        Vector3 _bodyBase;
        Vector3 _drillBase;
        Light2D[] _statusBulbs;
        float[] _bulbBaseIntensity;
        Sprite _rockChipSprite;

        AudioSource _drillAudio;
        float _drillAudioVol;
        // Far (≈10 ortho) = normal presence; close (≈3.5) = clearly louder
        const float DrillAudioVolFar = 0.2f;
        const float DrillAudioVolClose = 0.72f;
        /// <summary>Very short fade on start/stop (~70ms).</summary>
        const float DrillAudioFadeSeconds = 0.07f;
        const string DrillLoopResource = "Audio/Drill/drill";
        const float DrillZoomOrthoClose = 3.5f;
        const float DrillZoomOrthoFar = 10f;

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
            _digConeSprite = MakeDigConeSprite();
            _sparkSprite = MakeSparkSprite();
            _rockChipSprite = MakeRockChipSprite();
            EnsureDrillAudio();
        }

        void OnDestroy()
        {
            if (_smokeRoot != null)
                Destroy(_smokeRoot.gameObject);
            if (_drillAudio != null && _drillAudio.isPlaying)
                _drillAudio.Stop();
        }

        void EnsureDrillAudio()
        {
            if (_drillAudio != null) return;
            var clip = Resources.Load<AudioClip>(DrillLoopResource);
            if (clip == null) return;

            _drillAudio = gameObject.AddComponent<AudioSource>();
            _drillAudio.clip = clip;
            _drillAudio.loop = true;
            _drillAudio.playOnAwake = false;
            // 2D — zoom owns loudness (not world distance)
            _drillAudio.spatialBlend = 0f;
            _drillAudio.priority = 64;
            _drillAudio.volume = 0f;
            _drillAudio.pitch = 1f;
            _drillAudioVol = 0f;
        }

        void TickDrillAudio(bool digging)
        {
            if (_drillAudio == null) return;

            // Only while actively drilling — false during stamina rest, cooling, overheat.
            float target = 0f;
            if (digging)
            {
                target = DrillAudioVolFar;
                var cam = Camera.main;
                if (cam != null && cam.orthographic)
                {
                    float zoom01 = Mathf.InverseLerp(DrillZoomOrthoFar, DrillZoomOrthoClose,
                        cam.orthographicSize);
                    zoom01 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(zoom01));
                    target = Mathf.Lerp(DrillAudioVolFar, DrillAudioVolClose, zoom01);
                }
                if (_worker != null)
                    target *= Mathf.Lerp(0.92f, 1.06f, _worker.DigFaceHardness);
            }

            float fadeStep = DrillAudioVolClose / Mathf.Max(0.01f, DrillAudioFadeSeconds) * Time.deltaTime;
            _drillAudioVol = Mathf.MoveTowards(_drillAudioVol, target, fadeStep);
            _drillAudio.volume = Mathf.Clamp01(_drillAudioVol);

            if (digging || _drillAudioVol > 0.001f)
            {
                if (!_drillAudio.isPlaying)
                {
                    if (_drillAudioVol <= 0.001f)
                        _drillAudio.volume = 0f;
                    _drillAudio.UnPause();
                    if (!_drillAudio.isPlaying)
                        _drillAudio.Play();
                }
            }
            else if (_drillAudio.isPlaying)
            {
                _drillAudio.Pause();
            }
        }

        void LateUpdate()
        {
            if (_drill == null || _worker == null) return;

            _drill.localRotation = Quaternion.identity;

            bool digging = _worker.IsActivelyDigging;
            var heat = _worker.HeatState;
            // Heat exhaust only while actively working — parked machine cools quietly
            bool heatFx = digging && heat >= ExcavatorHeatZone.Optimal;

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
            TickDrillAudio(digging);

            if (digging)
            {
                _digSmokeTimer -= Time.deltaTime;
                if (_digSmokeTimer <= 0f)
                {
                    _digSmokeTimer = Random.Range(0.022f, 0.042f);
                    // Cone spray + billows (~30% quieter than max heavy pass)
                    int n = Random.Range(2, 4);
                    for (int i = 0; i < n; i++)
                        SpawnDigSmokePuff();
                    if (Random.value < 0.5f)
                        SpawnDigBillowPuff();
                    if (Random.value < 0.28f)
                        SpawnDigBillowPuff();
                }

                // Mine haze — sparse but readable in the tunnel
                _floorDustTimer -= Time.deltaTime;
                if (_floorDustTimer <= 0f)
                {
                    _floorDustTimer = Random.Range(0.22f, 0.48f);
                    if (Random.value < 0.88f)
                        SpawnFloorHazeBehindBit();
                    if (Random.value < 0.4f)
                        SpawnFloorHazeBehindBit();
                }

                // Hot metal / rock chips from the bit — more on hard face
                TickRockSparks();
                TickRockChips();
            }
            else
            {
                _digSmokeTimer = 0f;
                _floorDustTimer = 0f;
                _rockSparkTimer = 0f;
                _rockChipTimer = 0f;
            }

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
            // HOT < CRITICAL < OVERHEATED — denser soot so chassis exhaust reads clearly
            float interval = heat switch
            {
                ExcavatorHeatZone.Overheated => Random.Range(0.018f, 0.034f),
                ExcavatorHeatZone.Extreme => Random.Range(0.028f, 0.05f),
                ExcavatorHeatZone.Danger => Random.Range(0.038f, 0.065f),
                _ => Random.Range(0.05f, 0.085f), // Optimal
            };
            int bursts = heat switch
            {
                ExcavatorHeatZone.Overheated => 4,
                ExcavatorHeatZone.Extreme => 3,
                ExcavatorHeatZone.Danger => 3,
                _ => 2,
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
            // Cone of dust springing from the bit tip along dig facing.
            Vector3 tip = _drill != null
                ? _drill.TransformPoint(Vector3.up * 0.72f)
                : transform.position;
            tip += (Vector3)(Random.insideUnitCircle * 0.02f);
            if (_worker != null)
                tip += (Vector3)(_worker.Facing * Random.Range(0.02f, 0.1f));

            SpawnDigConePuff(tip);
        }

        void SpawnFloorHazeBehindBit()
        {
            if (_smokeRoot == null || _worker == null || _drill == null) return;
            Vector2 face = _worker.Facing.normalized;
            Vector2 behind = (Vector2)_drill.position - face * Random.Range(0.15f, 0.65f);
            behind += new Vector2(-face.y, face.x) * Random.Range(-0.35f, 0.35f);
            float cs = _worker.World != null ? _worker.World.CellSize : 0.14f;
            ExcavationDustFx.SpawnLingering(_smokeRoot, behind, cs * Random.Range(0.9f, 1.2f),
                heavy: Random.value < 0.25f);
        }

        void TickRockSparks()
        {
            float hard = _worker != null ? _worker.DigFaceHardness : 0.5f;
            // Soft rock: rare; bedrock: frequent short bursts
            float interval = Mathf.Lerp(0.18f, 0.04f, hard);
            _rockSparkTimer -= Time.deltaTime;
            if (_rockSparkTimer > 0f) return;
            _rockSparkTimer = interval * Random.Range(0.7f, 1.25f);

            // Chance gate so soft faces stay mostly spark-free
            if (Random.value > 0.28f + hard * 0.68f) return;

            int n = hard > 0.75f ? Random.Range(4, 7) : Random.Range(2, 5);
            for (int i = 0; i < n; i++)
                SpawnRockImpactSpark(hard);
        }

        void TickRockChips()
        {
            float hard = _worker != null ? _worker.DigFaceHardness : 0.5f;
            float interval = Mathf.Lerp(0.09f, 0.035f, hard);
            _rockChipTimer -= Time.deltaTime;
            if (_rockChipTimer > 0f) return;
            _rockChipTimer = interval * Random.Range(0.75f, 1.2f);

            int n = hard > 0.7f ? Random.Range(2, 5) : Random.Range(1, 3);
            for (int i = 0; i < n; i++)
                SpawnRockChip(hard);
        }

        void SpawnRockChip(float hardness)
        {
            if (_smokeRoot == null || _drill == null || _worker == null) return;
            if (_rockChipSprite == null) return;

            Vector2 face = _worker.Facing.normalized;
            Vector2 side = new(-face.y, face.x);
            Vector3 origin = _drill.TransformPoint(Vector3.up * 0.72f);
            origin += (Vector3)(face * Random.Range(0.01f, 0.08f));
            origin += (Vector3)(side * Random.Range(-0.05f, 0.05f));

            // Pop out of the face into the tunnel — tiny debris spray
            Vector2 kick = -face * Random.Range(0.55f, 1.45f) * (0.85f + hardness * 0.4f)
                + side * Random.Range(-0.85f, 0.85f)
                + Vector2.up * Random.Range(0.15f, 0.65f);
            // Occasional side ricochet along the wall
            if (Random.value < 0.2f)
                kick = side * Random.Range(0.6f, 1.3f) * (Random.value < 0.5f ? 1f : -1f)
                    - face * Random.Range(0.15f, 0.45f)
                    + Vector2.up * Random.Range(0.05f, 0.35f);

            float size = Random.Range(0.035f, 0.072f) * (0.9f + hardness * 0.2f);

            var go = new GameObject("RockChip");
            go.transform.SetParent(_smokeRoot, false);
            go.transform.position = origin;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            go.transform.localScale = Vector3.one * size;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _rockChipSprite;
            sr.sortingOrder = SparkSortOrder - 1;
            DigVisualKit.ApplyUnlit(sr);

            float g = Random.Range(0.28f, 0.52f);
            float warm = Random.Range(0.92f, 1.12f);
            sr.color = new Color(g * warm, g * 0.78f, g * 0.52f, Random.Range(0.88f, 1f));

            go.AddComponent<DrillSparkPuff>().Init(
                kick,
                life: Random.Range(0.18f, 0.38f),
                gravity: Random.Range(4.5f, 7.5f),
                stretch: false);
        }

        void SpawnRockImpactSpark(float hardness)
        {
            if (_smokeRoot == null || _drill == null || _worker == null) return;

            Vector2 face = _worker.Facing.normalized;
            Vector2 side = new(-face.y, face.x);
            Vector3 origin = _drill.TransformPoint(Vector3.up * 0.72f);
            origin += (Vector3)(face * Random.Range(0.02f, 0.1f));
            origin += (Vector3)(side * Random.Range(-0.06f, 0.06f));

            // Streak velocity: snappier kick off the face
            Vector2 kick = face * Random.Range(0.85f, 1.95f) * (0.75f + hardness * 0.55f)
                + side * Random.Range(-0.8f, 0.8f)
                + Vector2.down * Random.Range(0.2f, 0.75f);
            // Occasional reverse chip into the tunnel
            if (Random.value < 0.28f)
                kick = -face * Random.Range(0.45f, 1.05f) + side * Random.Range(-0.55f, 0.55f)
                    + Vector2.down * Random.Range(0.1f, 0.45f);

            float len = Random.Range(0.07f, 0.14f) * (0.85f + hardness * 0.4f);
            float thick = len * Random.Range(0.18f, 0.32f);

            var go = new GameObject("RockSpark");
            go.transform.SetParent(_smokeRoot, false);
            go.transform.position = origin;
            float ang = Mathf.Atan2(kick.y, kick.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            go.transform.localScale = new Vector3(len, thick, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sparkSprite;
            sr.sortingOrder = SparkSortOrder;
            DigVisualKit.ApplyUnlit(sr);

            // White-hot core → orange / amber (steel on stone), rare cooler chip
            float roll = Random.value;
            Color col;
            if (roll < 0.35f)
                col = new Color(1f, 0.96f, 0.78f, Random.Range(0.95f, 1f));
            else if (roll < 0.75f)
                col = new Color(1f, 0.58f, 0.2f, Random.Range(0.92f, 1f));
            else if (roll < 0.92f)
                col = new Color(1f, 0.8f, 0.3f, Random.Range(0.88f, 1f));
            else
                col = new Color(0.9f, 0.78f, 0.58f, Random.Range(0.7f, 0.92f));
            sr.color = col;

            go.AddComponent<DrillSparkPuff>().Init(
                kick,
                life: Random.Range(0.09f, 0.2f) * (0.9f + hardness * 0.25f),
                gravity: Random.Range(2.2f, 4.0f),
                stretch: true);
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
            DigVisualKit.ApplyUnlit(sr);
            bool cyan = Random.value > 0.35f;
            sr.color = cyan
                ? new Color(0.55f, 0.9f, 1f, Random.Range(0.75f, 1f))
                : new Color(1f, 0.95f, 0.7f, Random.Range(0.7f, 1f));

            Vector2 kick = Random.insideUnitCircle.normalized * Random.Range(0.35f, 0.9f);
            go.AddComponent<DrillSparkPuff>().Init(kick, life: Random.Range(0.08f, 0.18f));
        }

        void SpawnDigConePuff(Vector3 origin)
        {
            if (_smokeRoot == null || _worker == null) return;

            Vector2 face = _worker.Facing.normalized;
            Vector2 back = -face;
            Vector2 side = new(-face.y, face.x);

            float along = Random.Range(0.22f, 0.58f);
            float flare = Random.Range(-0.85f, 0.85f);
            Vector2 drift = back * along + side * (flare * along * 1.2f)
                + face * Random.Range(-0.05f, 0.1f);

            var go = new GameObject("DigDust");
            go.transform.SetParent(_smokeRoot, false);
            go.transform.position = origin;
            float ang = Mathf.Atan2(back.y, back.x) * Mathf.Rad2Deg - 90f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, ang + flare * 22f);

            float len = Random.Range(0.28f, 0.52f);
            float width = Random.Range(0.13f, 0.26f) * (0.65f + along);
            go.transform.localScale = new Vector3(width, len, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _digConeSprite != null ? _digConeSprite : _smokeSprite;
            sr.sortingOrder = SmokeSortOrder;
            DigVisualKit.ApplyUnlit(sr);
            sr.color = DigSmokeColor();

            go.AddComponent<DrillSmokePuff>().Init(
                _worker.World,
                drift,
                life: Random.Range(0.42f, 0.72f),
                grow: Random.Range(1.0f, 1.65f));
        }

        /// <summary>Thick dirt billow at the bit — volume, not just spray streaks.</summary>
        void SpawnDigBillowPuff()
        {
            if (_smokeRoot == null || _worker == null || _drill == null) return;

            Vector2 face = _worker.Facing.normalized;
            Vector2 back = -face;
            Vector2 side = new(-face.y, face.x);

            Vector3 origin = _drill.TransformPoint(Vector3.up * 0.68f);
            origin += (Vector3)(face * Random.Range(0.02f, 0.08f));
            origin += (Vector3)(side * Random.Range(-0.08f, 0.08f));
            origin += (Vector3)(back * Random.Range(0f, 0.06f));

            var go = new GameObject("DigBillow");
            go.transform.SetParent(_smokeRoot, false);
            go.transform.position = origin;
            float scale = Random.Range(0.24f, 0.42f);
            go.transform.localScale = Vector3.one * scale;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _smokeSprite;
            sr.sortingOrder = SmokeSortOrder - 1;
            DigVisualKit.ApplyUnlit(sr);
            sr.color = DigSmokeColorHeavy();

            Vector2 drift = back * Random.Range(0.1f, 0.32f)
                + side * Random.Range(-0.22f, 0.22f)
                + face * Random.Range(-0.04f, 0.06f);

            go.AddComponent<DrillSmokePuff>().Init(
                _worker.World,
                drift,
                life: Random.Range(0.55f, 0.95f),
                grow: Random.Range(1.25f, 2.0f));
        }

        void SpawnHeatSmokePuff(ExcavatorHeatZone heat)
        {
            float scale = heat switch
            {
                ExcavatorHeatZone.Overheated => Random.Range(0.38f, 0.62f),
                ExcavatorHeatZone.Extreme => Random.Range(0.32f, 0.52f),
                ExcavatorHeatZone.Danger => Random.Range(0.28f, 0.46f),
                _ => Random.Range(0.24f, 0.4f),
            };
            float life = heat switch
            {
                ExcavatorHeatZone.Overheated => Random.Range(0.85f, 1.35f),
                ExcavatorHeatZone.Extreme => Random.Range(0.75f, 1.15f),
                ExcavatorHeatZone.Danger => Random.Range(0.65f, 1.0f),
                _ => Random.Range(0.55f, 0.9f),
            };

            // Heat exhaust from the machine body — separate from tip dig dust.
            Vector3 body = _body != null ? _body.position : transform.position;
            body += (Vector3)(Random.insideUnitCircle * 0.07f);
            if (_worker != null)
                body += (Vector3)(-_worker.Facing * 0.04f); // slightly toward the chassis

            SpawnSmokeInternal(
                origin: body,
                color: HeatSmokeColor(heat),
                scale: scale,
                life: life,
                grow: Random.Range(1.8f, 2.8f),
                driftMul: heat == ExcavatorHeatZone.Overheated ? 1.45f : 1.15f);
        }

        void SpawnSmokeInternal(Vector3 origin, Color color, float scale, float life, float grow,
            float driftMul)
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
            // Unlit so dark exhaust reads as soot against lit tunnel floor
            DigVisualKit.ApplyUnlit(sr);
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

        static Color DigSmokeColor()
        {
            // Dirty brown — readable cloud without blotting the dig face
            float g = Random.Range(0.2f, 0.34f);
            return new Color(g * 1.15f, g * 0.82f, g * 0.58f, Random.Range(0.52f, 0.68f));
        }

        static Color DigSmokeColorHeavy()
        {
            float g = Random.Range(0.14f, 0.26f);
            return new Color(g * 1.1f, g * 0.78f, g * 0.52f, Random.Range(0.56f, 0.72f));
        }

        static Color HeatSmokeColor(ExcavatorHeatZone heat)
        {
            // Dense near-black soot — high alpha so exhaust reads against lit tunnel
            float g = heat switch
            {
                ExcavatorHeatZone.Overheated => Random.Range(0.02f, 0.06f),
                ExcavatorHeatZone.Extreme => Random.Range(0.03f, 0.07f),
                ExcavatorHeatZone.Danger => Random.Range(0.04f, 0.09f),
                _ => Random.Range(0.05f, 0.11f),
            };
            float a = heat switch
            {
                ExcavatorHeatZone.Overheated => Random.Range(0.88f, 0.98f),
                ExcavatorHeatZone.Extreme => Random.Range(0.8f, 0.94f),
                ExcavatorHeatZone.Danger => Random.Range(0.72f, 0.9f),
                _ => Random.Range(0.65f, 0.85f),
            };
            return new Color(g, g * 0.97f, g * 0.94f, a);
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

        /// <summary>
        /// Dense dirt wedge — pivot at the tip (bottom), flares toward +Y for cone spray.
        /// </summary>
        static Sprite MakeDigConeSprite()
        {
            const int s = 40;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            float tipX = (s - 1) * 0.5f;
            float tipY = 2f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float t = Mathf.Clamp01((y - tipY) / (s - 4f - tipY));
                float halfW = Mathf.Lerp(1.1f, s * 0.48f, Mathf.Pow(t, 1.35f));
                float dx = Mathf.Abs(x - tipX);
                if (dx > halfW + 1.5f || y < tipY)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }
                float edge = 1f - Mathf.Clamp01(dx / Mathf.Max(0.5f, halfW));
                float along = Mathf.Sin(Mathf.Clamp01(t * 1.15f) * Mathf.PI);
                float n = Mathf.PerlinNoise(x * 0.32f + 4f, y * 0.26f);
                float a = edge * Mathf.Sqrt(edge) * along * (0.7f + n * 0.35f);
                a *= Mathf.Lerp(1f, 0.55f, t);
                if (a < 0.04f)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, tipY / s), s);
        }

        static Sprite MakeSparkSprite()
        {
            // Elongated hot streak — used for rock impacts (scaled thin) and overheat flecks
            const int w = 16, h = 8;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x - cx) / (w * 0.48f);
                float dy = (y - cy) / (h * 0.32f);
                float d = dx * dx + dy * dy * 2.8f;
                if (d > 1f) { tex.SetPixel(x, y, Color.clear); continue; }
                // Hot tip toward +X
                float tip = Mathf.Clamp01(0.55f + dx * 0.55f);
                float a = Mathf.Clamp01((1f - d) * tip);
                a *= a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
        }

        /// <summary>Tiny angular pebble for drill debris spray.</summary>
        static Sprite MakeRockChipSprite()
        {
            const int s = 10;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            float cx = (s - 1) * 0.5f, cy = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - cx) / 3.4f;
                float dy = (y - cy) / 2.8f;
                // Irregular diamond / chip silhouette
                float d = Mathf.Abs(dx) * 0.85f + Mathf.Abs(dy) * 1.15f
                    + Mathf.Abs(dx * dy) * 0.55f;
                float n = Mathf.PerlinNoise(x * 0.7f + 1.3f, y * 0.7f + 2.1f);
                d -= (n - 0.5f) * 0.35f;
                if (d > 1f) { tex.SetPixel(x, y, Color.clear); continue; }
                float a = Mathf.Clamp01((1f - d) * 1.4f);
                float shade = 0.75f + n * 0.35f;
                tex.SetPixel(x, y, new Color(shade, shade * 0.92f, shade * 0.82f, a));
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

    /// <summary>One-shot spark puff reused by digger + equipment repair FX.</summary>
    public sealed class DrillSparkPuff : MonoBehaviour
    {
        Vector2 _vel;
        float _life;
        float _age;
        float _gravity;
        bool _stretch;
        Vector3 _startScale;
        SpriteRenderer _sr;
        Color _c0;

        public void Init(Vector2 velocity, float life, float gravity = 0f, bool stretch = false)
        {
            _vel = velocity;
            _life = Mathf.Max(0.05f, life);
            _gravity = gravity;
            _stretch = stretch;
            _startScale = transform.localScale;
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _c0 = _sr.color;
        }

        void Update()
        {
            _age += Time.deltaTime;
            float u = Mathf.Clamp01(_age / _life);
            _vel.y -= _gravity * Time.deltaTime;
            transform.position += (Vector3)(_vel * Time.deltaTime);
            _vel *= 1f - (stretchDamp()) * Time.deltaTime;

            if (_stretch && _vel.sqrMagnitude > 0.0001f)
            {
                float ang = Mathf.Atan2(_vel.y, _vel.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, ang);
                float speed = _vel.magnitude;
                float sx = _startScale.x * (1f + Mathf.Clamp01(speed * 0.35f) * (1f - u));
                float sy = _startScale.y * (1f - u * 0.55f);
                transform.localScale = new Vector3(sx, sy, 1f);
            }
            else
            {
                transform.localScale = _startScale * (1f - u * 0.7f);
                transform.Rotate(0f, 0f, 480f * Time.deltaTime);
            }

            if (_sr != null)
            {
                var c = _c0;
                // Cool from white-hot toward amber as it dies
                if (_stretch && u > 0.35f)
                {
                    float cool = (u - 0.35f) / 0.65f;
                    c = Color.Lerp(c, new Color(1f, 0.35f, 0.08f, c.a), cool * 0.55f);
                }
                c.a = _c0.a * (1f - u) * (1f - u * 0.35f);
                _sr.color = c;
            }
            if (u >= 1f) Destroy(gameObject);
        }

            float stretchDamp() => _stretch ? 1.7f : 3.2f;
        }
    }
