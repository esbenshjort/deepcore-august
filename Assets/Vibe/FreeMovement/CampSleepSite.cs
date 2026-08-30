using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Big wall tent + rooftop industrial AC. Crew returns here after shift (18:00)
    /// and emerges at 08:00. AC runs continuously (steam + live power indicators).
    /// </summary>
    public sealed class CampSleepSite : MonoBehaviour
    {
        public Vector2 TentDoor { get; private set; }
        public Vector2 FirePos { get; private set; }
        public Transform Root => transform;

        IndustrialSteamPlume _acSteam;
        SpriteRenderer _powerLed;
        SpriteRenderer _acFan;
        Light2D _powerGlow;
        float _fanSpin;

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
            DigVisualKit.ApplyLit(_powerLed);
            _powerLed.color = new Color(0.3f, 1f, 0.45f, 0.95f);

            // Live electricity glow on the AC / power box
            var lightGo = new GameObject("AcPowerLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = acPos + new Vector2(0.05f, 0.05f);
            _powerGlow = lightGo.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(_powerGlow,
                new Color(0.35f, 0.85f, 1f),
                intensity: 0.85f,
                outer: 1.15f,
                inner: 0.05f,
                shadows: false,
                falloff: 0.7f);

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
            var fireLight = fireLightGo.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(fireLight,
                new Color(1f, 0.42f, 0.14f),
                intensity: 1.65f,
                outer: 2.55f,
                inner: 0.08f,
                shadows: false,
                falloff: 0.66f);
            fireLightGo.AddComponent<CosyLantern>().Init(fireLightGo.transform.position, fireLight, 1.65f);

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

        void Update()
        {
            // Condenser fan spin + living power indicators
            _fanSpin += Time.deltaTime * 220f;
            if (_acFan != null)
                _acFan.transform.localRotation = Quaternion.Euler(0f, 0f, _fanSpin);

            float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 5.5f);
            if (_powerLed != null)
            {
                _powerLed.color = new Color(0.25f, 1f, 0.4f, pulse);
                float s = 0.05f + 0.015f * pulse;
                _powerLed.transform.localScale = Vector3.one * s;
            }

            if (_powerGlow != null)
                _powerGlow.intensity = 0.7f + 0.25f * Mathf.Sin(Time.time * 3.2f);

            // AC load breathes — occasional harder blast of condensate
            if (_acSteam != null)
            {
                float load = 0.55f + 0.3f * Mathf.Sin(Time.time * 0.35f)
                             + 0.12f * Mathf.Sin(Time.time * 1.7f);
                _acSteam.Burst = Mathf.Clamp01(load);
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
