using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Big wall tent + rooftop industrial AC. Crew returns here after shift end
    /// (planner default 16:00) and emerges at shift start (default 08:00).
    /// </summary>
    public sealed class CampSleepSite : MonoBehaviour
    {
        public Vector2 TentDoor { get; private set; }
        public Vector2 FirePos { get; private set; }
        public Transform Root => transform;

        IndustrialSteamPlume _acSteam;
        IndustrialSteamPlume _cookSteam;
        SpriteRenderer _powerLed;
        SpriteRenderer _acFan;
        Light2D _powerGlow;
        Light2D _fireLight;
        CosyLantern _fireLantern;
        float _fanSpin;
        float _nightBlend; // 0 day/on-shift camp · 1 asleep quiet
        float _mealPulse;
        float _fireBaseIntensity = 1.65f;
        float _powerBaseIntensity = 1.35f;

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
            // Farther west of wash pad — separate sleep cluster (cellSize 0.1)
            TentDoor = campCenter + new Vector2(-3.85f, -0.35f);
            FirePos = campCenter + new Vector2(-2.95f, -1.55f);
            transform.localPosition = Vector3.zero;

            // Packed dirt pad under camp
            var pad = MakeSpriteGo("SleepPad", TentDoor + new Vector2(0.55f, -0.35f),
                YardVisualKit.SleepPad, 9, 2.35f);
            DigVisualKit.ApplyLit(pad);

            // Large marquee / wall tent
            var tent = MakeSpriteGo("Tent", TentDoor + new Vector2(0.45f, 0.1f),
                YardVisualKit.Tent, 16, 1.55f);
            DigVisualKit.ApplyLit(tent);

            // Door flap (west entry)
            var flap = MakeSpriteGo("TentFlap", TentDoor + new Vector2(-0.15f, -0.05f),
                YardVisualKit.TentFlap, 17, 0.55f);
            DigVisualKit.ApplyLit(flap);

            // Soft warm interior spill from doorway
            var doorGlow = MakeSpriteGo("DoorGlow", TentDoor + new Vector2(-0.05f, -0.02f),
                DigVisualKit.LanternGlow, 15, 0.55f);
            var unlit = Shader.Find("Sprites/Default");
            if (unlit != null) doorGlow.sharedMaterial = new Material(unlit);
            doorGlow.color = new Color(1f, 0.55f, 0.2f, 0.35f);

            // Big rooftop AC / condenser on the east end of the tent
            Vector2 acPos = TentDoor + new Vector2(1.15f, 0.35f);
            var ac = MakeSpriteGo("Aircon", acPos, YardVisualKit.Aircon, 19, 0.72f);
            DigVisualKit.ApplyLit(ac);

            // Spinning fan disc (reads as powered condenser)
            _acFan = MakeSpriteGo("AcFan", acPos + new Vector2(-0.06f, 0.02f),
                DigVisualKit.Pixel, 20, 0.14f);
            DigVisualKit.ApplyLit(_acFan);
            _acFan.color = new Color(0.35f, 0.38f, 0.42f, 0.55f);
            _acFan.transform.localScale = new Vector3(0.22f, 0.22f, 1f);

            // Exhaust steam — continuous condensate plume
            _acSteam = IndustrialSteamPlume.Attach(transform, acPos + new Vector2(0.18f, 0.22f), 36, 33)
                .Configure(
                    riseDir: new Vector2(0.25f, 1f),
                    tint: new Color(0.88f, 0.94f, 1f, 1f),
                    rate: 12f,
                    spread: 0.11f,
                    lift: 0.85f);
            _acSteam.Emitting = true;
            _acSteam.Burst = 0.72f;

            // Power cable from AC toward tent / junction
            Vector2 boxPos = TentDoor + new Vector2(0.85f, -0.55f);
            var cable = MakeSpriteGo("PowerCable",
                Vector2.Lerp(acPos, boxPos, 0.5f) + new Vector2(-0.05f, -0.15f),
                YardVisualKit.PowerCable, 14, 0.55f);
            DigVisualKit.ApplyLit(cable);
            cable.transform.localRotation = Quaternion.Euler(0f, 0f, -38f);

            var box = MakeSpriteGo("PowerBox", boxPos, YardVisualKit.PowerBox, 18, 0.28f);
            DigVisualKit.ApplyLit(box);

            _powerLed = MakeSpriteGo("PowerLed", boxPos + new Vector2(-0.04f, 0.02f),
                DigVisualKit.Pixel, 21, 0.06f);
            var unlitLed = Shader.Find("Sprites/Default");
            if (unlitLed != null) _powerLed.sharedMaterial = new Material(unlitLed);
            _powerLed.color = new Color(0.35f, 1f, 0.5f, 1f);

            // Green power LED spill
            var ledLightGo = new GameObject("PowerLedLight");
            ledLightGo.transform.SetParent(transform, false);
            ledLightGo.transform.localPosition = boxPos + new Vector2(-0.04f, 0.02f);
            var ledLight = ledLightGo.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(ledLight,
                new Color(0.3f, 1f, 0.4f),
                intensity: 0.55f,
                outer: 0.55f,
                inner: 0.02f,
                shadows: false,
                falloff: 0.75f);

            // Soft unlit bloom on the LED itself
            var ledHalo = MakeSpriteGo("PowerLedHalo", boxPos + new Vector2(-0.04f, 0.02f),
                DigVisualKit.LanternGlow, 20, 0.22f);
            if (unlitLed != null) ledHalo.sharedMaterial = new Material(unlitLed);
            ledHalo.color = new Color(0.25f, 1f, 0.4f, 0.45f);

            // Live electricity glow on the AC / power box
            var lightGo = new GameObject("AcPowerLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = acPos + new Vector2(0.05f, 0.05f);
            _powerGlow = lightGo.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(_powerGlow,
                new Color(0.35f, 0.9f, 1f),
                intensity: 1.35f,
                outer: 1.45f,
                inner: 0.06f,
                shadows: false,
                falloff: 0.65f);

            // Cyan panel glow on tent face (matches neon slit on sprite)
            var tentCyanGo = new GameObject("TentCyanLight");
            tentCyanGo.transform.SetParent(transform, false);
            tentCyanGo.transform.localPosition = TentDoor + new Vector2(0.55f, 0.25f);
            var tentCyan = tentCyanGo.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(tentCyan,
                new Color(0.3f, 0.9f, 1f),
                intensity: 0.45f,
                outer: 0.7f,
                inner: 0.04f,
                shadows: false,
                falloff: 0.8f);

            // Bedrolls near door
            var bed = MakeSpriteGo("Bedrolls", TentDoor + new Vector2(0.35f, -0.7f),
                YardVisualKit.Bedrolls, 15, 0.48f);
            DigVisualKit.ApplyLit(bed);

            // Cut-barrel firepit + logs
            var pit = MakeSpriteGo("FirePit", FirePos, YardVisualKit.FirePit, 15, 0.52f);
            DigVisualKit.ApplyLit(pit);

            var flame = MakeSpriteGo("Flame", FirePos + new Vector2(0f, 0.07f),
                YardVisualKit.Flame, 18, 0.36f);
            if (unlit != null) flame.sharedMaterial = new Material(unlit);
            flame.color = new Color(1f, 0.85f, 0.45f, 0.95f);
            flame.gameObject.AddComponent<CampFireFlicker>().Bind(flame);

            var ember = MakeSpriteGo("EmberGlow", FirePos + new Vector2(0f, 0.04f),
                DigVisualKit.LanternGlow, 17, 0.45f);
            if (unlit != null) ember.sharedMaterial = new Material(unlit);
            ember.color = new Color(1f, 0.45f, 0.12f, 0.55f);

            var fireLightGo = new GameObject("FireLight");
            fireLightGo.transform.SetParent(transform, false);
            fireLightGo.transform.localPosition = FirePos + new Vector2(0f, 0.05f);
            _fireLight = fireLightGo.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(_fireLight,
                new Color(1f, 0.42f, 0.14f),
                intensity: _fireBaseIntensity,
                outer: 2.55f,
                inner: 0.08f,
                shadows: false,
                falloff: 0.66f);
            _fireLantern = fireLightGo.AddComponent<CosyLantern>();
            _fireLantern.Init(fireLightGo.transform.position, _fireLight, _fireBaseIntensity);

            // Quiet cook steam near fire — only bursts when a meal is served
            _cookSteam = IndustrialSteamPlume.Attach(transform, FirePos + new Vector2(0.08f, 0.22f), 22, 34)
                .Configure(
                    riseDir: new Vector2(0.1f, 1f),
                    tint: new Color(0.95f, 0.9f, 0.78f, 1f),
                    rate: 6f,
                    spread: 0.09f,
                    lift: 0.55f);
            _cookSteam.Emitting = false;
            _cookSteam.Burst = 0f;

            // Supply crates
            var crate = MakeSpriteGo("Crate", TentDoor + new Vector2(1.35f, -0.75f),
                YardVisualKit.Crate, 14, 0.32f);
            DigVisualKit.ApplyLit(crate);
            var crate2 = MakeSpriteGo("Crate2", TentDoor + new Vector2(1.55f, -0.45f),
                YardVisualKit.Crate, 14, 0.26f);
            DigVisualKit.ApplyLit(crate2);
            crate2.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);

            DigVisualKit.PlaceLantern(transform,
                TentDoor + new Vector2(1.75f, -0.95f), local: true, intensity: 1.85f);
        }

        /// <summary>0 = active camp evening · 1 = asleep / deep night quiet.</summary>
        public void SetNightQuiet01(float night01)
        {
            _nightBlend = Mathf.Clamp01(night01);
        }

        /// <summary>Real meal event — brief cook steam + fire punch (simulation-driven).</summary>
        public void NotifyMealServed(CampMealQuality quality)
        {
            _mealPulse = quality == CampMealQuality.Good ? 1f
                : quality == CampMealQuality.Unsafe ? 0.55f
                : 0.8f;
            if (_cookSteam != null)
            {
                _cookSteam.Emitting = true;
                _cookSteam.Burst = 0.35f + _mealPulse * 0.65f;
            }
        }

        void Update()
        {
            // Condenser fan spin + living power indicators
            float fanSpeed = Mathf.Lerp(220f, 70f, _nightBlend);
            _fanSpin += Time.deltaTime * fanSpeed;
            if (_acFan != null)
                _acFan.transform.localRotation = Quaternion.Euler(0f, 0f, _fanSpin);

            float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 5.5f);
            if (_powerLed != null)
            {
                float a = Mathf.Lerp(pulse, 0.35f, _nightBlend);
                _powerLed.color = new Color(0.25f, 1f, 0.4f, a);
                float s = 0.05f + 0.015f * a;
                _powerLed.transform.localScale = Vector3.one * s;
            }

            if (_powerGlow != null)
            {
                float glow = _powerBaseIntensity + 0.35f * Mathf.Sin(Time.time * 3.2f);
                _powerGlow.intensity = Mathf.Lerp(glow, glow * 0.35f, _nightBlend);
            }

            // AC load breathes — quieter when asleep
            if (_acSteam != null)
            {
                float load = 0.55f + 0.3f * Mathf.Sin(Time.time * 0.35f)
                             + 0.12f * Mathf.Sin(Time.time * 1.7f);
                load = Mathf.Lerp(load, load * 0.28f, _nightBlend);
                _acSteam.Burst = Mathf.Clamp01(load);
            }

            // Meal cook steam fade
            if (_mealPulse > 0f)
            {
                _mealPulse = Mathf.MoveTowards(_mealPulse, 0f, Time.deltaTime * 0.22f);
                if (_cookSteam != null)
                {
                    _cookSteam.Burst = Mathf.Max(_cookSteam.Burst * 0.96f, _mealPulse * 0.5f);
                    if (_mealPulse <= 0.02f)
                    {
                        _cookSteam.Emitting = false;
                        _cookSteam.Burst = 0f;
                    }
                }
                if (_fireLight != null)
                    _fireLight.intensity = _fireBaseIntensity * (1f + _mealPulse * 0.45f);
            }
            else if (_fireLight != null)
            {
                float nightFire = Mathf.Lerp(_fireBaseIntensity, _fireBaseIntensity * 0.55f, _nightBlend);
                _fireLight.intensity = Mathf.MoveTowards(_fireLight.intensity, nightFire, Time.deltaTime * 0.8f);
            }
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
            float s = 1f + 0.14f * Mathf.Sin(_t) + 0.07f * Mathf.Sin(_t * 2.3f);
            _sr.transform.localScale = new Vector3(
                _baseScale.x * (0.92f + 0.08f * Mathf.Sin(_t * 1.4f)),
                _baseScale.y * s,
                _baseScale.z);
            float a = 0.78f + 0.2f * Mathf.Sin(_t * 1.7f);
            var c = _sr.color;
            c.a = a;
            _sr.color = c;
        }
    }
}
