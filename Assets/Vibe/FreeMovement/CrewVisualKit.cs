using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Top-down industrial mining crew sprites + Light2D attach helpers.
    /// True helmet-centered footprint; +Y = facing / dig direction.
    /// </summary>
    public static class CrewVisualKit
    {
        // ——— Shared palette ———
        static readonly Color Orange = new(0.92f, 0.48f, 0.12f);
        static readonly Color OrangeHi = new(1f, 0.62f, 0.22f);
        static readonly Color OrangeDeep = new(0.62f, 0.28f, 0.08f);
        static readonly Color Rust = new(0.45f, 0.22f, 0.1f);
        static readonly Color Suit = new(0.42f, 0.34f, 0.26f);
        static readonly Color SuitHi = new(0.55f, 0.45f, 0.34f);
        static readonly Color SuitDeep = new(0.28f, 0.22f, 0.17f);
        static readonly Color Black = new(0.06f, 0.06f, 0.07f);
        static readonly Color Charcoal = new(0.14f, 0.15f, 0.16f);
        static readonly Color Metal = new(0.48f, 0.5f, 0.54f);
        static readonly Color MetalHi = new(0.72f, 0.74f, 0.78f);
        static readonly Color MetalDeep = new(0.28f, 0.3f, 0.33f);
        static readonly Color HazardY = new(0.95f, 0.82f, 0.12f);
        static readonly Color Outline = new(0.02f, 0.02f, 0.03f);
        static readonly Color Green = new(0.3f, 0.95f, 0.4f);
        static readonly Color GreenCore = new(0.75f, 1f, 0.8f);
        static readonly Color Blue = new(0.35f, 0.85f, 1f);
        static readonly Color BlueCore = new(0.75f, 0.95f, 1f);
        static readonly Color Cyan = new(0.3f, 0.95f, 1f);
        static readonly Color CyanCore = new(0.7f, 1f, 1f);
        static readonly Color Gold = new(0.95f, 0.72f, 0.18f);
        static readonly Color GoldHi = new(1f, 0.9f, 0.4f);
        static readonly Color GoldDeep = new(0.65f, 0.4f, 0.08f);
        static readonly Color Bronze = new(0.7f, 0.42f, 0.22f);
        static readonly Color BronzeHi = new(0.88f, 0.58f, 0.32f);
        static readonly Color RedTank = new(0.72f, 0.14f, 0.1f);
        static readonly Color RedHi = new(0.95f, 0.28f, 0.18f);
        static readonly Color Molten = new(1f, 0.45f, 0.08f);
        static readonly Color MoltenCore = new(1f, 0.85f, 0.35f);
        static readonly Color MoltenDeep = new(0.7f, 0.2f, 0.05f);
        static readonly Color Glove = new(0.1f, 0.1f, 0.11f);
        static readonly Color Boot = new(0.12f, 0.1f, 0.09f);

        static Sprite _excavatorBody;
        static Sprite _excavatorDrill;
        static Sprite _prospector;
        static Sprite _engineer;
        static Sprite _hauler;
        static Sprite _refiner;
        static Sprite _statusBulb;
        static Sprite _headlampBeam;
        static Sprite _headlampBead;
        static Sprite[] _offDutyWorkers;

        public static Sprite ExcavatorBody
        {
            get
            {
                if (_excavatorBody != null) return _excavatorBody;
                _excavatorBody = MakeExcavatorBody();
                return _excavatorBody;
            }
        }

        public static Sprite ExcavatorDrill
        {
            get
            {
                if (_excavatorDrill != null) return _excavatorDrill;
                _excavatorDrill = MakeExcavatorDrill();
                return _excavatorDrill;
            }
        }

        public static Sprite Prospector
        {
            get
            {
                if (_prospector != null) return _prospector;
                _prospector = MakeProspector();
                return _prospector;
            }
        }

        public static Sprite Engineer
        {
            get
            {
                if (_engineer != null) return _engineer;
                _engineer = MakeEngineer();
                return _engineer;
            }
        }

        public static Sprite Hauler
        {
            get
            {
                if (_hauler != null) return _hauler;
                _hauler = MakeHauler();
                return _hauler;
            }
        }

        public static Sprite Refiner
        {
            get
            {
                if (_refiner != null) return _refiner;
                _refiner = MakeRefiner();
                return _refiner;
            }
        }

        /// <summary>
        /// Off-duty person sprite (WorkerId 1–5 → variants 0–4). Same suit language as on-duty crew.
        /// </summary>
        public static Sprite OffDutyForWorker(int workerId)
        {
            EnsureOffDutyWorkers();
            int v = Mathf.Clamp(workerId - 1, 0, _offDutyWorkers.Length - 1);
            return _offDutyWorkers[v];
        }

        static void EnsureOffDutyWorkers()
        {
            if (_offDutyWorkers != null) return;
            _offDutyWorkers = new Sprite[5];
            for (int i = 0; i < 5; i++)
                _offDutyWorkers[i] = MakeOffDutyWorker(i);
        }

        public static Sprite StatusBulb
        {
            get
            {
                if (_statusBulb != null) return _statusBulb;
                _statusBulb = MakeStatusBulbSprite();
                return _statusBulb;
            }
        }

        static Sprite HeadlampBeam
        {
            get
            {
                if (_headlampBeam != null) return _headlampBeam;
                _headlampBeam = MakeHeadlampBeamSprite();
                return _headlampBeam;
            }
        }

        static Sprite HeadlampBead
        {
            get
            {
                if (_headlampBead != null) return _headlampBead;
                _headlampBead = MakeHeadlampBeadSprite();
                return _headlampBead;
            }
        }

        // Green status bulbs — body local, facing +Y (helmet toward +Y).
        static readonly Vector2[] ExcavatorBulbLocals =
        {
            new(-0.07f, -0.02f),
            new(0.07f, -0.02f),
            new(-0.11f, 0.04f),
            new(0.11f, 0.04f),
        };

        static readonly Vector2[] ProspectorBulbLocals =
        {
            new(-0.12f, 0.02f), // radar dish center
            new(0.12f, 0.08f),  // antenna tip
            new(0.1f, 0.06f),   // tablet
        };

        static readonly Vector2[] EngineerBulbLocals =
        {
            new(0.1f, 0.16f),   // arm tool tip
            new(-0.12f, 0.06f), // tablet
            new(0.08f, 0.08f),  // antenna
        };

        static readonly Vector2[] HaulerBulbLocals =
        {
            new(-0.08f, -0.06f),
            new(0.08f, -0.06f),
            new(0f, -0.1f),
        };

        static readonly Vector2[] RefinerBulbLocals =
        {
            new(0f, -0.06f),    // globe
            new(0.1f, 0f),      // cylinder tank
            new(-0.08f, -0.02f),
            new(0.06f, -0.1f),
        };

        // ——— Attach ———

        public static void AttachExcavator(GameObject root, FreeWorkerController worker)
        {
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, -0.03f, 0f);
            body.transform.localScale = Vector3.one * 0.58f;
            var bsr = body.AddComponent<SpriteRenderer>();
            bsr.sprite = ExcavatorBody;
            bsr.sortingOrder = 40;
            DigVisualKit.ApplyLit(bsr);

            var drill = new GameObject("DrillBit");
            drill.transform.SetParent(root.transform, false);
            drill.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            drill.transform.localRotation = Quaternion.identity;
            drill.transform.localScale = Vector3.one * 0.5f;
            var dsr = drill.AddComponent<SpriteRenderer>();
            dsr.sprite = ExcavatorDrill;
            dsr.sortingOrder = 41;
            DigVisualKit.ApplyLit(dsr);

            AttachHeadlamp(body.transform, new Vector2(0f, 0.11f));

            // Harsh directional work light — distinct from warm lanterns / campfire
            var headlampTf = root.transform.Find("Headlamp");
            if (headlampTf != null)
            {
                var hl = headlampTf.GetComponent<Light2D>();
                if (hl != null)
                {
                    hl.color = new Color(1f, 0.82f, 0.55f);
                    hl.intensity = 1.18f;
                    hl.pointLightOuterRadius = 1.18f;
                    hl.pointLightInnerAngle = 20f;
                    hl.pointLightOuterAngle = 48f;
                    hl.falloffIntensity = 0.58f;
                    hl.shadowIntensity = 0.85f;
                    var flick = headlampTf.GetComponent<HelmetFlashlight>();
                    flick?.RetuneFromLight(1.18f);
                }
            }

            var bulbs = new Light2D[ExcavatorBulbLocals.Length];
            for (int i = 0; i < ExcavatorBulbLocals.Length; i++)
                bulbs[i] = AttachStatusBulb(body.transform, ExcavatorBulbLocals[i], i, green: true);

            var anim = root.GetComponent<DrillerVisual>();
            if (anim == null) anim = root.AddComponent<DrillerVisual>();
            anim.Init(worker, drill.transform, body.transform, bulbs);
            FootstepDustFx.Attach(root.transform);
        }

        public static void AttachProspector(Transform facingRoot, out Light2D[] bulbs)
        {
            bulbs = AttachCrewBody(facingRoot, Prospector, 0.54f, ProspectorBulbLocals);
            // Tablet + dish get a touch of green spill
            if (bulbs.Length > 2 && bulbs[2] != null)
            {
                bulbs[2].color = new Color(0.35f, 1f, 0.45f);
                bulbs[2].intensity = 0.32f;
                bulbs[2].pointLightOuterRadius = 0.28f;
            }
        }

        public static void AttachEngineer(Transform facingRoot, out Light2D[] bulbs)
        {
            bulbs = AttachCrewBody(facingRoot, Engineer, 0.54f, EngineerBulbLocals);
            // Arm tool tip = cyan
            if (bulbs.Length > 0 && bulbs[0] != null)
            {
                bulbs[0].color = new Color(0.3f, 0.95f, 1f);
                bulbs[0].intensity = 0.38f;
                bulbs[0].pointLightOuterRadius = 0.32f;
                var sr = bulbs[0].GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(0.55f, 0.98f, 1f, 1f);
            }
            // Tablet cyan spill
            if (bulbs.Length > 1 && bulbs[1] != null)
            {
                bulbs[1].color = new Color(0.3f, 0.9f, 1f);
                bulbs[1].intensity = 0.3f;
                bulbs[1].pointLightOuterRadius = 0.28f;
                var sr = bulbs[1].GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(0.5f, 0.95f, 1f, 1f);
            }
        }

        public static void AttachHauler(Transform facingRoot, out Light2D[] bulbs)
        {
            bulbs = AttachCrewBody(facingRoot, Hauler, 0.56f, HaulerBulbLocals);
            // Soft gold spill from ore crate
            var body = facingRoot.Find("Body");
            if (body != null)
            {
                var go = new GameObject("OreGlow");
                go.transform.SetParent(body, false);
                go.transform.localPosition = new Vector2(0f, -0.08f);
                var light = go.AddComponent<Light2D>();
                DigVisualKit.ConfigurePointLight(light,
                    new Color(1f, 0.75f, 0.25f),
                    intensity: 0.12f,
                    outer: 0.2f,
                    inner: 0.02f,
                    shadows: false,
                    falloff: 0.85f);
            }
        }

        public static void AttachRefiner(Transform facingRoot, out Light2D[] bulbs)
        {
            bulbs = AttachCrewBody(facingRoot, Refiner, 0.55f, RefinerBulbLocals);
            // Molten vat / tank glow
            var body = facingRoot.Find("Body");
            if (body != null)
            {
                void Molten(string name, Vector2 local, float intensity, float outer)
                {
                    var go = new GameObject(name);
                    go.transform.SetParent(body, false);
                    go.transform.localPosition = local;
                    var light = go.AddComponent<Light2D>();
                    DigVisualKit.ConfigurePointLight(light,
                        new Color(1f, 0.45f, 0.12f),
                        intensity: intensity,
                        outer: outer,
                        inner: 0.02f,
                        shadows: false,
                        falloff: 0.75f);
                }
                Molten("MoltenGlobe", new Vector2(0f, -0.05f), 0.28f, 0.32f);
                Molten("MoltenTank", new Vector2(0.1f, 0f), 0.18f, 0.22f);
            }
        }

        /// <summary>Camp steward — soft green care / kitchen lamp language.</summary>
        public static void AttachSteward(Transform facingRoot, out Light2D[] bulbs)
        {
            bulbs = AttachCrewBody(facingRoot, OffDutyForWorker(6), 0.54f, EngineerBulbLocals);
            if (bulbs.Length > 0 && bulbs[0] != null)
            {
                bulbs[0].color = new Color(0.45f, 1f, 0.55f);
                bulbs[0].intensity = 0.32f;
                bulbs[0].pointLightOuterRadius = 0.28f;
            }
            var body = facingRoot.Find("Body");
            if (body != null)
            {
                var go = new GameObject("StewardLamp");
                go.transform.SetParent(body, false);
                go.transform.localPosition = new Vector2(0.06f, -0.04f);
                var light = go.AddComponent<Light2D>();
                DigVisualKit.ConfigurePointLight(light,
                    new Color(0.55f, 1f, 0.65f),
                    intensity: 0.16f,
                    outer: 0.24f,
                    inner: 0.02f,
                    shadows: false,
                    falloff: 0.8f);
            }
        }

        /// <summary>
        /// Off-duty walking presence — hard-hat crew without role tools.
        /// </summary>
        public static SpriteRenderer AttachOffDuty(Transform root, int workerId)
        {
            var body = new GameObject("Body");
            body.transform.SetParent(root, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = Vector3.one * 0.54f;
            var sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = OffDutyForWorker(workerId);
            sr.sortingOrder = 38;
            DigVisualKit.ApplyLit(sr);

            AttachHeadlamp(body.transform, new Vector2(0f, 0.11f));
            AttachStatusBulb(body.transform, new Vector2(-0.08f, -0.02f), 0, green: true);
            AttachStatusBulb(body.transform, new Vector2(0.08f, -0.02f), 1, green: true);
            return sr;
        }

        static Light2D[] AttachCrewBody(Transform facingRoot, Sprite sprite, float scale, Vector2[] bulbLocals)
        {
            var body = new GameObject("Body");
            body.transform.SetParent(facingRoot, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = Vector3.one * scale;
            var sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 40;
            DigVisualKit.ApplyLit(sr);

            var lamp = AttachHeadlamp(body.transform, new Vector2(0f, 0.11f));
            var bulbs = new Light2D[bulbLocals.Length + 1];
            for (int i = 0; i < bulbLocals.Length; i++)
                bulbs[i] = AttachStatusBulb(body.transform, bulbLocals[i], i, green: true);
            bulbs[bulbLocals.Length] = lamp;

            // Walking people kick a whisper of floor dust
            FootstepDustFx.Attach(facingRoot);
            return bulbs;
        }

        /// <summary>
        /// Helmet flashlight — short warm cone ahead (+Y), occluded by rock walls.
        /// Not an omni body glow.
        /// </summary>
        static Light2D AttachHeadlamp(Transform body, Vector2 local)
        {
            Transform host = body.parent != null ? body.parent : body;
            float bodyScale = Mathf.Max(0.01f, body.localScale.x);
            // Origin at forehead, slightly forward so the cone clears the body
            Vector3 lampLocal = body.localPosition
                + (Vector3)(local + new Vector2(0f, 0.04f)) * bodyScale;

            var go = new GameObject("Headlamp");
            go.transform.SetParent(host, false);
            go.transform.localPosition = lampLocal;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            // Tiny LED on the hard-hat
            var beadGo = new GameObject("LedBead");
            beadGo.transform.SetParent(go.transform, false);
            beadGo.transform.localPosition = Vector3.zero;
            beadGo.transform.localScale = Vector3.one * 0.042f;
            var beadSr = beadGo.AddComponent<SpriteRenderer>();
            beadSr.sprite = HeadlampBead;
            beadSr.sortingOrder = 44;
            var beadSh = Shader.Find("Sprites/Default");
            if (beadSh != null) beadSr.sharedMaterial = new Material(beadSh);
            beadSr.color = new Color(1f, 0.88f, 0.65f, 0.95f);

            // Short warm dust wedge — sells the cone without a long grey beam
            var beamGo = new GameObject("DustBeam");
            beamGo.transform.SetParent(go.transform, false);
            beamGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            beamGo.transform.localScale = new Vector3(0.55f, 0.72f, 1f); // a little farther
            var beamSr = beamGo.AddComponent<SpriteRenderer>();
            beamSr.sprite = HeadlampBeam;
            beamSr.sortingOrder = 12;
            var beamSh = Shader.Find("Sprites/Default");
            if (beamSh != null) beamSr.sharedMaterial = new Material(beamSh);
            beamSr.color = new Color(1f, 0.72f, 0.4f, 0.1f);

            // Warm spot cone (+Y) — little helmet flashlight, not a lantern
            const float intensity = 0.95f;
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(1f, 0.68f, 0.36f);
            light.intensity = intensity;
            light.pointLightInnerRadius = 0.03f;
            light.pointLightOuterRadius = 0.95f;
            light.pointLightInnerAngle = 24f;
            light.pointLightOuterAngle = 52f;
            light.falloffIntensity = 0.72f;
            light.shadowsEnabled = true;
            light.shadowIntensity = 0.78f;
            light.shadowSoftness = 0.7f;
            light.overlapOperation = Light2D.OverlapOperation.AlphaBlend;

            go.AddComponent<HelmetFlashlight>().Init(light, beamSr, beadSr, intensity);
            return light;
        }

        static Sprite MakeHeadlampBeamSprite()
        {
            // Apex at bottom-center, fans upward — soft dust volume in the cone
            const int w = 64;
            const int h = 96;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            float cx = (w - 1) * 0.5f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = y / (float)(h - 1);          // 0 at lamp → 1 at tip
                float half = Mathf.Lerp(1.2f, w * 0.48f, v * v);
                float dx = Mathf.Abs(x - cx);
                float edge = 1f - Mathf.Clamp01(dx / Mathf.Max(0.5f, half));
                edge = edge * edge * (3f - 2f * edge); // smoothstep
                // Brighter near lamp, soft fade with distance + haze noise
                float along = (1f - v);
                along = along * along;
                float n = Mathf.PerlinNoise(x * 0.09f + 2.1f, y * 0.07f + 0.4f);
                float a = edge * along * (0.55f + 0.45f * n) * 0.85f;
                // Hot core along centerline
                float core = Mathf.Exp(-(dx * dx) / (half * half * 0.35f + 0.01f)) * along * 0.55f;
                a = Mathf.Clamp01(a + core * 0.35f);
                if (a < 0.01f)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), h);
        }

        static Sprite MakeHeadlampBeadSprite()
        {
            const int s = 16;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            float c = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - c) / (s * 0.42f);
                float dy = (y - c) / (s * 0.42f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }
                float core = Mathf.Clamp01(1f - d * 1.35f);
                float glow = Mathf.Clamp01(1f - d);
                float a = core * core * 0.95f + glow * 0.35f;
                Color col = Color.Lerp(
                    new Color(0.55f, 0.7f, 1f, a),
                    new Color(1f, 1f, 1f, a),
                    core);
                tex.SetPixel(x, y, col);
            }
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        static Light2D AttachStatusBulb(Transform body, Vector2 local, int index, bool green)
        {
            var go = new GameObject($"StatusBulb_{index}");
            go.transform.SetParent(body, false);
            go.transform.localPosition = local;
            go.transform.localScale = Vector3.one * 0.048f;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = StatusBulb;
            sr.sortingOrder = 42;
            var sh = Shader.Find("Sprites/Default");
            if (sh != null) sr.sharedMaterial = new Material(sh);
            sr.color = green
                ? new Color(0.45f, 1f, 0.55f, 1f)
                : new Color(0.5f, 0.95f, 1f, 1f);

            // Soft bloom halo behind the LED (reads as neon in the dark).
            var halo = new GameObject("Halo");
            halo.transform.SetParent(go.transform, false);
            halo.transform.localPosition = Vector3.zero;
            halo.transform.localScale = Vector3.one * 2.4f;
            var hsr = halo.AddComponent<SpriteRenderer>();
            hsr.sprite = DigVisualKit.LanternGlow;
            hsr.sortingOrder = 41;
            if (sh != null) hsr.sharedMaterial = new Material(sh);
            hsr.color = green
                ? new Color(0.25f, 1f, 0.4f, 0.42f)
                : new Color(0.25f, 0.85f, 1f, 0.4f);

            var light = go.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(light,
                green ? new Color(0.35f, 1f, 0.45f) : new Color(0.4f, 0.9f, 1f),
                intensity: green ? 0.28f : 0.4f,
                outer: green ? 0.24f : 0.36f,
                inner: 0.012f,
                shadows: false,
                falloff: 0.82f);
            return light;
        }

        // ——— Draw helpers ———

        static void Clear(Texture2D tex, int s)
        {
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, new Color(0, 0, 0, 0));
        }

        static void Dot(Texture2D tex, int s, int x, int y, Color c)
        {
            if ((uint)x < s && (uint)y < s) tex.SetPixel(x, y, c);
        }

        static void Box(Texture2D tex, int s, int x0, int y0, int w, int h, Color c)
        {
            for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                Dot(tex, s, x, y, c);
        }

        static void Disc(Texture2D tex, int s, float cx, float cy, float rx, float ry, Color fill, Color? edge = null)
        {
            int x0 = Mathf.FloorToInt(cx - rx - 1);
            int x1 = Mathf.CeilToInt(cx + rx + 1);
            int y0 = Mathf.FloorToInt(cy - ry - 1);
            int y1 = Mathf.CeilToInt(cy + ry + 1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = (x - cx) / Mathf.Max(0.01f, rx);
                float dy = (y - cy) / Mathf.Max(0.01f, ry);
                float d = dx * dx + dy * dy;
                if (d > 1.02f) continue;
                if (edge.HasValue && d > 0.78f) Dot(tex, s, x, y, edge.Value);
                else Dot(tex, s, x, y, fill);
            }
        }

        static void Weather(Texture2D tex, int s, int x0, int y0, int w, int h, int seed)
        {
            for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                if ((uint)x >= s || (uint)y >= s) continue;
                var c = tex.GetPixel(x, y);
                if (c.a < 0.1f) continue;
                float n = Mathf.PerlinNoise(x * 0.35f + seed, y * 0.35f);
                if (n > 0.72f) tex.SetPixel(x, y, Color.Lerp(c, Rust, 0.35f));
                else if (n < 0.22f) tex.SetPixel(x, y, Color.Lerp(c, Black, 0.2f));
                else if (n > 0.55f && n < 0.58f) tex.SetPixel(x, y, Color.Lerp(c, SuitDeep, 0.25f));
            }
        }

        static void HazardDiag(Texture2D tex, int s, int x0, int y0, int w, int h)
        {
            for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                bool dark = ((x + y) / 3) % 2 == 0;
                Dot(tex, s, x, y, dark ? Black : HazardY);
            }
        }

        static void Hose(Texture2D tex, int s, int x0, int y0, int x1, int y1)
        {
            int steps = Mathf.Max(8, Mathf.RoundToInt(Vector2.Distance(
                new Vector2(x0, y0), new Vector2(x1, y1))));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                Color ring = (i % 2 == 0) ? Charcoal : Black;
                Box(tex, s, x - 1, y - 1, 3, 3, ring);
                Dot(tex, s, x, y, MetalDeep);
            }
        }

        static void SoftShade(Texture2D tex, int s, float cx, float cy, float rx, float ry, Color shade, float strength)
        {
            int x0 = Mathf.FloorToInt(cx - rx - 1);
            int x1 = Mathf.CeilToInt(cx + rx + 1);
            int y0 = Mathf.FloorToInt(cy - ry - 1);
            int y1 = Mathf.CeilToInt(cy + ry + 1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if ((uint)x >= s || (uint)y >= s) continue;
                float dx = (x - cx) / Mathf.Max(0.01f, rx);
                float dy = (y - cy) / Mathf.Max(0.01f, ry);
                float d = dx * dx + dy * dy;
                if (d > 1f) continue;
                var c = tex.GetPixel(x, y);
                if (c.a < 0.1f) continue;
                float t = (1f - d) * strength;
                tex.SetPixel(x, y, Color.Lerp(c, shade, t));
            }
        }

        // ——— Shared crew base ———

        /// <summary>
        /// Boots, quilted suit, belt/pouches, orange hard-hat + blue headlamp at leading (+Y) edge.
        /// </summary>
        static void PaintCrewBase(Texture2D tex, int s, bool includeBeltYellowCans)
        {
            // Boots at rear
            Disc(tex, s, 34, 18, 8, 5.5f, Boot, Outline);
            Disc(tex, s, 62, 18, 8, 5.5f, Boot, Outline);
            Disc(tex, s, 34, 18, 4.5f, 3f, Charcoal, null);
            Disc(tex, s, 62, 18, 4.5f, 3f, Charcoal, null);
            Box(tex, s, 30, 16, 8, 2, Black);
            Box(tex, s, 58, 16, 8, 2, Black);

            // Legs / lower suit
            Disc(tex, s, 38, 28, 7, 8, SuitDeep, Outline);
            Disc(tex, s, 58, 28, 7, 8, SuitDeep, Outline);
            Disc(tex, s, 38, 28, 5, 6, Suit, null);
            Disc(tex, s, 58, 28, 5, 6, Suit, null);

            // Torso oval
            Disc(tex, s, 48, 42, 18, 14, SuitDeep, Outline);
            Disc(tex, s, 48, 42, 15.5f, 11.5f, Suit, null);
            Disc(tex, s, 48, 44, 10, 7.5f, SuitHi, null);
            // Quilt seams
            Box(tex, s, 40, 36, 1, 14, SuitDeep);
            Box(tex, s, 48, 35, 1, 16, SuitDeep);
            Box(tex, s, 56, 36, 1, 14, SuitDeep);
            Box(tex, s, 36, 42, 24, 1, SuitDeep);
            Weather(tex, s, 30, 30, 36, 28, 11);

            // Shoulders (slim default)
            Disc(tex, s, 28, 46, 9, 8, SuitDeep, Outline);
            Disc(tex, s, 28, 46, 7, 6.5f, Suit, null);
            Disc(tex, s, 68, 46, 9, 8, SuitDeep, Outline);
            Disc(tex, s, 68, 46, 7, 6.5f, Suit, null);

            // Dark gloves
            Disc(tex, s, 26, 54, 5.5f, 5f, Glove, Outline);
            Disc(tex, s, 70, 54, 5.5f, 5f, Glove, Outline);
            Dot(tex, s, 26, 55, MetalDeep);
            Dot(tex, s, 70, 55, MetalDeep);

            // Utility belt
            Box(tex, s, 32, 38, 32, 5, Charcoal);
            Box(tex, s, 33, 39, 30, 3, MetalDeep);
            Box(tex, s, 46, 38, 4, 5, Metal);
            Dot(tex, s, 47, 40, MetalHi);
            // Pouches
            Box(tex, s, 34, 36, 6, 5, SuitDeep);
            Box(tex, s, 35, 37, 4, 3, Black);
            Box(tex, s, 56, 36, 6, 5, SuitDeep);
            Box(tex, s, 57, 37, 4, 3, Black);
            Box(tex, s, 42, 35, 5, 4, Charcoal);
            Box(tex, s, 49, 35, 5, 4, Charcoal);

            if (includeBeltYellowCans)
            {
                Disc(tex, s, 30, 40, 3.5f, 4.5f, HazardY, Outline);
                Disc(tex, s, 30, 40, 2f, 3f, OrangeDeep, null);
                Disc(tex, s, 66, 40, 3.5f, 4.5f, HazardY, Outline);
                Disc(tex, s, 66, 40, 2f, 3f, OrangeDeep, null);
            }

            // Orange hard-hat dome (center identity ~48,52)
            Disc(tex, s, 48, 52, 14, 13, Outline, null);
            Disc(tex, s, 48, 52, 12.5f, 11.5f, OrangeDeep, Outline);
            Disc(tex, s, 48, 52, 10.5f, 9.5f, Orange, null);
            Disc(tex, s, 48, 53, 7, 6.5f, OrangeHi, null);
            // Helmet ridge
            Box(tex, s, 46, 48, 4, 10, OrangeDeep);
            Box(tex, s, 47, 49, 2, 8, OrangeHi);
            // Brim
            Box(tex, s, 38, 57, 20, 2, OrangeDeep);
            Weather(tex, s, 38, 42, 20, 22, 19);
            // Blue headlamp on leading (+Y) edge
            Box(tex, s, 46, 60, 5, 4, Charcoal);
            Box(tex, s, 47, 61, 3, 2, Blue);
            Dot(tex, s, 48, 61, BlueCore);
            Dot(tex, s, 48, 62, BlueCore);
        }

        static Sprite MakeStatusBulbSprite()
        {
            const int s = 8;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Clear(tex, s);
            float cx = (s - 1) * 0.5f, cy = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = x - cx, dy = y - cy;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r > 3.2f) continue;
                float t = 1f - r / 3.2f;
                Color c = Color.Lerp(new Color(0.2f, 0.2f, 0.2f, 0.4f),
                    new Color(1f, 1f, 1f, 1f), t * t);
                if (r < 1.15f) c = Color.white;
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        // ——— Role sprites ———

        static Sprite MakeExcavatorBody()
        {
            const int s = 96;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Clear(tex, s);

            PaintCrewBase(tex, s, includeBeltYellowCans: false);

            void LocalWeather(int x0, int y0, int w, int h, int seed) => Weather(tex, s, x0, y0, w, h, seed);
            void LocalBox(int x0, int y0, int w, int h, Color c) => Box(tex, s, x0, y0, w, h, c);
            void LocalDot(int x, int y, Color c) => Dot(tex, s, x, y, c);

            // Hazard stripes on boots
            HazardDiag(tex, s, 28, 15, 12, 6);
            HazardDiag(tex, s, 56, 15, 12, 6);
            Disc(tex, s, 34, 18, 5, 3, MetalDeep, null);
            Disc(tex, s, 62, 18, 5, 3, MetalDeep, null);

            // Back pack / power unit — heavier industrial block
            LocalBox(32, 20, 32, 18, Outline);
            LocalBox(33, 21, 30, 16, Charcoal);
            LocalBox(35, 23, 26, 12, Black);
            LocalBox(45, 24, 6, 10, MetalDeep);
            LocalBox(46, 25, 4, 8, Metal);
            LocalDot(47, 27, MetalHi);
            LocalDot(48, 29, Green);
            LocalDot(49, 29, GreenCore);
            LocalBox(36, 26, 4, 4, MetalDeep);
            LocalBox(56, 26, 4, 4, MetalDeep);
            LocalDot(37, 27, Rust);
            LocalDot(57, 28, Rust);
            Weather(tex, s, 32, 20, 32, 18, 14);

            // Yellow side tanks
            Disc(tex, s, 28, 30, 5.5f, 8, HazardY, Outline);
            Disc(tex, s, 28, 30, 3.5f, 5.5f, OrangeDeep, null);
            Disc(tex, s, 28, 32, 2f, 3f, Orange, null);
            Disc(tex, s, 68, 30, 5.5f, 8, HazardY, Outline);
            Disc(tex, s, 68, 30, 3.5f, 5.5f, OrangeDeep, null);
            Disc(tex, s, 68, 32, 2f, 3f, Orange, null);
            Box(tex, s, 26, 24, 4, 2, Metal);
            Box(tex, s, 66, 24, 4, 2, Metal);
            Dot(tex, s, 28, 26, MetalHi);
            Dot(tex, s, 68, 26, MetalHi);

            // Bulky orange armored shoulders (over base)
            Disc(tex, s, 22, 46, 13, 11, OrangeDeep, Outline);
            Disc(tex, s, 22, 46, 10.5f, 9, Orange, null);
            Disc(tex, s, 22, 47, 6, 5, OrangeHi, null);
            Disc(tex, s, 74, 46, 13, 11, OrangeDeep, Outline);
            Disc(tex, s, 74, 46, 10.5f, 9, Orange, null);
            Disc(tex, s, 74, 47, 6, 5, OrangeHi, null);
            Box(tex, s, 18, 44, 5, 5, Charcoal);
            Box(tex, s, 73, 44, 5, 5, Charcoal);
            Dot(tex, s, 19, 45, MetalHi);
            Dot(tex, s, 20, 47, MetalHi);
            Dot(tex, s, 74, 45, MetalHi);
            Dot(tex, s, 75, 47, MetalHi);
            // Shoulder plate seams
            Box(tex, s, 18, 48, 8, 1, OrangeDeep);
            Box(tex, s, 70, 48, 8, 1, OrangeDeep);
            LocalWeather(12, 36, 22, 20, 3);
            LocalWeather(62, 36, 22, 20, 7);

            // Arms / gauntlets gripping forward
            Disc(tex, s, 26, 58, 8, 7, SuitDeep, Outline);
            Disc(tex, s, 26, 58, 5.5f, 5, Metal, null);
            Disc(tex, s, 70, 58, 8, 7, SuitDeep, Outline);
            Disc(tex, s, 70, 58, 5.5f, 5, Metal, null);
            Dot(tex, s, 26, 59, MetalHi);
            Dot(tex, s, 70, 59, MetalHi);
            Box(tex, s, 24, 56, 4, 2, Charcoal);
            Box(tex, s, 68, 56, 4, 2, Charcoal);

            // Corrugated hoses pack → forward drill mount
            Hose(tex, s, 36, 34, 42, 62);
            Hose(tex, s, 60, 34, 54, 62);
            Hose(tex, s, 32, 38, 40, 58);
            Hose(tex, s, 64, 38, 56, 58);
            Hose(tex, s, 40, 30, 44, 50);
            Hose(tex, s, 56, 30, 52, 50);

            // Drill mount plate at y~64 — heavier collar
            Disc(tex, s, 48, 64, 12, 8, OrangeDeep, Outline);
            Disc(tex, s, 48, 64, 9.5f, 6, Orange, null);
            Disc(tex, s, 48, 65, 5, 3.5f, OrangeHi, null);
            Box(tex, s, 40, 60, 16, 5, Charcoal);
            Box(tex, s, 42, 61, 12, 3, Metal);
            Box(tex, s, 44, 62, 8, 1, MetalHi);
            Dot(tex, s, 43, 63, MetalHi);
            Dot(tex, s, 52, 63, MetalHi);
            Dot(tex, s, 45, 61, Rust);
            Dot(tex, s, 50, 61, Black);

            // Re-paint helmet on top of mount/hoses overlap cleanup
            Disc(tex, s, 48, 52, 12.5f, 11.5f, OrangeDeep, Outline);
            Disc(tex, s, 48, 52, 10.5f, 9.5f, Orange, null);
            Disc(tex, s, 48, 53, 7, 6.5f, OrangeHi, null);
            Box(tex, s, 46, 48, 4, 10, OrangeDeep);
            Box(tex, s, 47, 49, 2, 8, OrangeHi);
            LocalWeather(38, 42, 20, 20, 19);
            Box(tex, s, 46, 60, 5, 4, Charcoal);
            Box(tex, s, 47, 61, 3, 2, Blue);
            Dot(tex, s, 48, 61, BlueCore);

            // Green status bulbs on shoulders / pack
            Box(tex, s, 22, 48, 3, 3, Green);
            Dot(tex, s, 23, 49, GreenCore);
            Box(tex, s, 71, 48, 3, 3, Green);
            Dot(tex, s, 72, 49, GreenCore);
            Box(tex, s, 38, 34, 2, 2, Green);
            Box(tex, s, 56, 34, 2, 2, Green);
            Dot(tex, s, 38, 34, GreenCore);
            Dot(tex, s, 56, 34, GreenCore);

            SoftShade(tex, s, 48, 36, 16, 10, Black, 0.18f);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.45f), s);
        }

        static Sprite MakeExcavatorDrill()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Clear(tex, s);

            void D(int x, int y, Color c) => Dot(tex, s, x, y, c);
            void B(int x0, int y0, int w, int h, Color c) => Box(tex, s, x0, y0, w, h, c);

            // Orange mount collar at pivot (bottom)
            B(18, 2, 28, 12, Outline);
            B(19, 3, 26, 10, OrangeDeep);
            B(21, 4, 22, 8, Orange);
            B(24, 5, 16, 5, OrangeHi);
            B(26, 6, 12, 3, Charcoal);
            // Rivets
            D(22, 5, MetalHi);
            D(41, 5, MetalHi);
            D(22, 11, MetalHi);
            D(41, 11, MetalHi);
            Weather(tex, s, 18, 2, 28, 12, 5);

            // Spiral gunmetal auger
            B(26, 12, 12, 28, Outline);
            B(27, 13, 10, 26, MetalDeep);
            B(28, 14, 8, 24, Metal);
            B(30, 16, 4, 20, MetalHi);

            // Helix grooves
            for (int i = 0; i < 7; i++)
            {
                int y = 14 + i * 4;
                int shift = (i % 2 == 0) ? 0 : 3;
                B(26 + shift, y, 7, 2, Charcoal);
                B(27 + shift, y, 5, 1, Black);
                B(33 - shift, y + 1, 4, 1, MetalHi);
            }

            // Grit / rust flecks on shaft
            for (int i = 0; i < 8; i++)
            {
                int x = 28 + (i * 3) % 8;
                int y = 15 + i * 3;
                if ((i % 2) == 0) D(x, y, Rust);
                else D(x + 1, y, Black);
            }

            // Sharp tip toward +Y
            for (int i = 0; i < 12; i++)
            {
                int w = Mathf.Max(2, 12 - i);
                int x0 = 32 - w / 2;
                Color tip = i < 4 ? Metal : (i < 8 ? MetalHi : new Color(0.9f, 0.91f, 0.93f));
                B(x0, 40 + i, w, 1, tip);
                D(x0, 40 + i, Outline);
                D(x0 + w - 1, 40 + i, Outline);
            }
            D(31, 52, MetalHi);
            D(32, 53, MetalHi);
            D(32, 54, new Color(0.95f, 0.96f, 0.98f));

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.08f), s);
        }

        static Sprite MakeProspector()
        {
            const int s = 96;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Clear(tex, s);

            PaintCrewBase(tex, s, includeBeltYellowCans: false);

            void LocalDisc(float cx, float cy, float rx, float ry, Color fill, Color? edge = null) =>
                Disc(tex, s, cx, cy, rx, ry, fill, edge);
            void LocalBox(int x0, int y0, int w, int h, Color c) => Box(tex, s, x0, y0, w, h, c);
            void LocalDot(int x, int y, Color c) => Dot(tex, s, x, y, c);

            // Slightly slimmer shoulders already from base — add scout padding
            LocalDisc(26, 46, 8, 7, SuitDeep, Outline);
            LocalDisc(26, 46, 6, 5.5f, SuitHi, null);
            LocalDisc(70, 46, 8, 7, SuitDeep, Outline);
            LocalDisc(70, 46, 6, 5.5f, SuitHi, null);
            Weather(tex, s, 20, 40, 16, 16, 4);
            Weather(tex, s, 62, 40, 16, 16, 8);

            // Backpack (left/rear)
            LocalDisc(36, 26, 12, 10, Charcoal, Outline);
            LocalDisc(36, 26, 10, 8, MetalDeep, null);
            LocalBox(32, 22, 8, 4, Metal);
            LocalDot(34, 24, MetalHi);
            LocalDot(38, 23, Rust);
            LocalBox(30, 28, 4, 3, Charcoal);
            LocalDot(31, 29, Green);
            LocalDot(31, 29, GreenCore);

            // Articulated dish arm from pack → left
            Hose(tex, s, 40, 28, 30, 34);
            LocalDisc(28, 36, 3, 3, Metal, Outline);
            LocalBox(26, 34, 4, 2, Charcoal);

            // Circular radar dish (large, left)
            LocalDisc(30, 38, 11, 11, MetalDeep, Outline);
            LocalDisc(30, 38, 9.5f, 9.5f, Charcoal, null);
            LocalDisc(30, 38, 7.5f, 7.5f, MetalDeep, null);
            LocalDisc(30, 38, 5.5f, 5.5f, Metal, null);
            // Dish ticks / grid
            for (int a = 0; a < 12; a++)
            {
                float ang = a * Mathf.PI / 6f;
                int tx = Mathf.RoundToInt(30 + Mathf.Cos(ang) * 7.5f);
                int ty = Mathf.RoundToInt(38 + Mathf.Sin(ang) * 7.5f);
                LocalDot(tx, ty, a % 2 == 0 ? Green : MetalHi);
            }
            LocalDisc(30, 38, 2.8f, 2.8f, Green, null);
            LocalDot(30, 38, GreenCore);
            LocalDot(30, 39, Color.white);
            LocalBox(29, 28, 2, 6, Metal); // dish mast

            // Antenna with green tip (right)
            LocalBox(68, 42, 2, 22, MetalDeep);
            LocalBox(68, 42, 2, 22, Charcoal);
            LocalBox(67, 62, 4, 4, Charcoal);
            LocalBox(68, 63, 2, 2, Green);
            LocalDot(69, 64, GreenCore);
            LocalBox(66, 44, 6, 3, Metal); // base bracket
            LocalDot(67, 45, Green);
            Hose(tex, s, 58, 30, 68, 46);

            // Handheld tablet on right-front (green radar UI)
            LocalBox(62, 54, 14, 12, Outline);
            LocalBox(63, 55, 12, 10, Charcoal);
            LocalBox(64, 56, 10, 8, Black);
            LocalDisc(69, 60, 4f, 4f, new Color(0.04f, 0.18f, 0.07f), null);
            LocalDisc(69, 60, 3.2f, 3.2f, Green, null);
            LocalDot(69, 60, GreenCore);
            for (int t = 0; t < 6; t++)
            {
                float ang = t * Mathf.PI / 3f + 0.15f;
                int gx = Mathf.RoundToInt(69 + Mathf.Cos(ang) * 2.8f);
                int gy = Mathf.RoundToInt(60 + Mathf.Sin(ang) * 2.8f);
                LocalDot(gx, gy, GreenCore);
            }
            LocalBox(66, 57, 6, 1, new Color(0.2f, 0.65f, 0.28f));
            LocalBox(67, 61, 1, 3, new Color(0.2f, 0.65f, 0.28f));
            LocalBox(72, 58, 1, 1, Green);
            LocalBox(72, 62, 1, 1, Green);
            // Hand grip
            LocalDisc(60, 56, 3.5f, 3.5f, Glove, Outline);
            LocalDot(60, 57, MetalDeep);

            // Small green bulbs painted
            LocalBox(38, 30, 2, 2, Green);
            LocalDot(38, 30, GreenCore);
            LocalBox(72, 46, 2, 2, Green);
            LocalDot(72, 46, GreenCore);

            SoftShade(tex, s, 48, 38, 14, 10, Black, 0.15f);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.45f), s);
        }

        static Sprite MakeEngineer()
        {
            const int s = 96;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Clear(tex, s);

            PaintCrewBase(tex, s, includeBeltYellowCans: false);

            void LocalDisc(float cx, float cy, float rx, float ry, Color fill, Color? edge = null) =>
                Disc(tex, s, cx, cy, rx, ry, fill, edge);
            void LocalBox(int x0, int y0, int w, int h, Color c) => Box(tex, s, x0, y0, w, h, c);
            void LocalDot(int x, int y, Color c) => Dot(tex, s, x, y, c);

            // Dark metal backpack
            LocalBox(36, 16, 24, 18, Outline);
            LocalBox(37, 17, 22, 16, Charcoal);
            LocalBox(39, 19, 18, 12, MetalDeep);
            LocalBox(41, 21, 14, 8, Black);
            LocalBox(42, 22, 12, 6, Metal);
            LocalDot(43, 23, MetalHi);
            LocalDot(50, 24, Rust);
            LocalBox(38, 28, 4, 4, Charcoal);
            LocalBox(54, 28, 4, 4, Charcoal);
            Weather(tex, s, 36, 16, 24, 18, 6);
            // Pack green status bulb (top-left of pack)
            LocalBox(40, 28, 3, 3, Green);
            LocalDot(41, 29, GreenCore);

            // Yellow canister on belt (right hip)
            LocalDisc(68, 40, 3.5f, 4.5f, HazardY, Outline);
            LocalDisc(68, 40, 2f, 3f, OrangeDeep, null);
            LocalBox(67, 36, 3, 2, Metal);

            // Segmented robotic arm arcing over right shoulder (orange / charcoal)
            LocalDisc(64, 46, 6, 5.5f, OrangeDeep, Outline);
            LocalDisc(64, 46, 4.5f, 4f, Orange, null);
            LocalDot(64, 47, OrangeHi);
            Color[] segCols = { Charcoal, Orange, Black, Orange, Charcoal, OrangeDeep, Orange };
            Vector2[] segPts =
            {
                new(68, 48), new(74, 52), new(78, 56),
                new(80, 62), new(78, 68), new(72, 72), new(68, 74),
            };
            for (int i = 0; i < segPts.Length; i++)
            {
                var p = segPts[i];
                LocalDisc(p.x, p.y, 3.6f, 3.2f, segCols[i], Outline);
                LocalDisc(p.x, p.y, 2.2f, 1.8f, i % 2 == 0 ? Metal : OrangeHi, null);
                if (i > 0)
                {
                    var prev = segPts[i - 1];
                    int mx = Mathf.RoundToInt((prev.x + p.x) * 0.5f);
                    int my = Mathf.RoundToInt((prev.y + p.y) * 0.5f);
                    LocalBox(mx - 1, my - 1, 3, 3, MetalDeep);
                }
            }
            // Cyan tool tip at arm end
            LocalDisc(66, 76, 4.5f, 4.5f, Cyan, Outline);
            LocalDisc(66, 76, 2.8f, 2.8f, CyanCore, null);
            LocalDot(66, 76, Color.white);
            LocalBox(64, 74, 4, 2, Metal);

            // Cyan glowing tablet in left hand
            LocalBox(16, 52, 13, 11, Outline);
            LocalBox(17, 53, 11, 9, Charcoal);
            LocalBox(18, 54, 9, 7, new Color(0.04f, 0.12f, 0.16f));
            LocalBox(19, 55, 7, 5, Cyan);
            LocalDot(22, 57, CyanCore);
            LocalDot(23, 58, Color.white);
            LocalBox(19, 55, 7, 1, new Color(0.15f, 0.45f, 0.5f));
            LocalBox(20, 58, 5, 1, new Color(0.2f, 0.55f, 0.6f));
            LocalBox(26, 56, 1, 1, Green);
            LocalBox(26, 60, 1, 1, Green);
            LocalDisc(20, 50, 3.5f, 3.5f, Glove, Outline);

            // Antenna with green tip
            LocalBox(56, 48, 2, 18, MetalDeep);
            LocalBox(55, 64, 4, 3, Charcoal);
            LocalBox(56, 65, 2, 2, Green);
            LocalDot(57, 66, GreenCore);

            // Extra status bulbs
            LocalBox(70, 48, 2, 2, Green);
            LocalDot(70, 48, GreenCore);

            SoftShade(tex, s, 48, 36, 14, 10, Black, 0.15f);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.45f), s);
        }

        static Sprite MakeHauler()
        {
            const int s = 96;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Clear(tex, s);

            PaintCrewBase(tex, s, includeBeltYellowCans: true);

            void LocalDisc(float cx, float cy, float rx, float ry, Color fill, Color? edge = null) =>
                Disc(tex, s, cx, cy, rx, ry, fill, edge);
            void LocalBox(int x0, int y0, int w, int h, Color c) => Box(tex, s, x0, y0, w, h, c);
            void LocalDot(int x, int y, Color c) => Dot(tex, s, x, y, c);

            // Large rectangular metal hopper on back (rear of helmet)
            LocalBox(28, 10, 40, 24, Outline);
            LocalBox(29, 11, 38, 22, MetalDeep);
            LocalBox(31, 13, 34, 18, Charcoal);
            LocalBox(33, 15, 30, 14, Black);
            // Empty hopper bed — cargo slots fill this at runtime
            LocalBox(34, 16, 28, 12, new Color(0.05f, 0.05f, 0.055f));
            SoftShade(tex, s, 48, 22, 12, 6, MetalDeep, 0.25f);
            // Hopper rim / lips
            LocalBox(28, 10, 40, 3, Metal);
            LocalBox(28, 31, 40, 3, MetalDeep);
            LocalBox(28, 10, 3, 24, Metal);
            LocalBox(65, 10, 3, 24, Metal);
            LocalDot(32, 12, MetalHi);
            LocalDot(60, 12, MetalHi);
            LocalDot(32, 30, MetalHi);
            LocalDot(60, 30, MetalHi);
            Weather(tex, s, 28, 10, 40, 24, 9);

            // Yellow-black hazard stripe on hopper rear face
            HazardDiag(tex, s, 32, 12, 32, 5);

            // Bronze/copper cylinders on hopper sides
            LocalDisc(26, 22, 5, 8, Bronze, Outline);
            LocalDisc(26, 22, 3.2f, 6, BronzeHi, null);
            LocalBox(24, 16, 4, 2, Metal);
            LocalDisc(26, 18, 1.5f, 1.5f, Orange, null);
            LocalDot(26, 18, OrangeHi);
            LocalDisc(70, 22, 5, 8, Bronze, Outline);
            LocalDisc(70, 22, 3.2f, 6, BronzeHi, null);
            LocalBox(68, 16, 4, 2, Metal);
            LocalDisc(70, 18, 1.5f, 1.5f, Orange, null);
            LocalDot(70, 18, OrangeHi);
            LocalDot(26, 20, Rust);
            LocalDot(70, 24, Rust);

            // Hopper straps to suit
            LocalBox(36, 32, 3, 8, Charcoal);
            LocalBox(57, 32, 3, 8, Charcoal);
            LocalBox(37, 34, 1, 5, MetalDeep);
            LocalBox(58, 34, 1, 5, MetalDeep);

            // Status bulbs
            LocalBox(34, 30, 2, 2, Green);
            LocalBox(60, 30, 2, 2, Green);
            LocalDot(34, 30, GreenCore);
            LocalDot(60, 30, GreenCore);

            SoftShade(tex, s, 48, 38, 14, 10, Black, 0.12f);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.45f), s);
        }

        static Sprite MakeRefiner()
        {
            const int s = 96;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Clear(tex, s);

            PaintCrewBase(tex, s, includeBeltYellowCans: false);

            void LocalDisc(float cx, float cy, float rx, float ry, Color fill, Color? edge = null) =>
                Disc(tex, s, cx, cy, rx, ry, fill, edge);
            void LocalBox(int x0, int y0, int w, int h, Color c) => Box(tex, s, x0, y0, w, h, c);
            void LocalDot(int x, int y, Color c) => Dot(tex, s, x, y, c);

            // Large spherical molten-orange energy globe in metal cage (back)
            LocalDisc(48, 24, 14, 14, Outline, null);
            LocalDisc(48, 24, 12.5f, 12.5f, MoltenDeep, Outline);
            LocalDisc(48, 24, 10, 10, Molten, null);
            LocalDisc(48, 25, 6.5f, 6.5f, OrangeHi, null);
            LocalDisc(47, 26, 3.5f, 3.5f, MoltenCore, null);
            LocalDot(46, 27, Color.white);
            LocalDot(48, 24, MoltenCore);
            // Bright glow pixels
            LocalDot(44, 22, MoltenCore);
            LocalDot(52, 28, OrangeHi);
            LocalDot(50, 20, MoltenCore);
            LocalDot(45, 28, OrangeHi);

            // Metal cage bars
            for (int i = -2; i <= 2; i++)
            {
                int x = 48 + i * 5;
                LocalBox(x, 12, 2, 24, MetalDeep);
                LocalBox(x, 12, 1, 24, Metal);
            }
            LocalBox(36, 18, 24, 2, Metal);
            LocalBox(36, 28, 24, 2, MetalDeep);
            LocalBox(36, 14, 24, 2, Charcoal);
            // Cage rivets
            LocalDot(38, 18, MetalHi);
            LocalDot(58, 18, MetalHi);
            LocalDot(38, 28, MetalHi);
            LocalDot(58, 28, MetalHi);
            Weather(tex, s, 36, 12, 24, 26, 12);

            // Vertical translucent orange cylinder tank on right
            LocalBox(66, 18, 10, 28, Outline);
            LocalBox(67, 19, 8, 26, OrangeDeep);
            LocalBox(68, 20, 6, 24, Molten);
            LocalBox(69, 22, 4, 20, new Color(1f, 0.55f, 0.15f, 0.9f));
            LocalBox(70, 26, 2, 12, MoltenCore);
            LocalDot(71, 30, Color.white);
            LocalBox(66, 18, 10, 3, Metal);
            LocalBox(66, 43, 10, 3, MetalDeep);
            LocalBox(68, 16, 6, 3, Charcoal);
            LocalDot(70, 17, Green);
            LocalDot(70, 17, GreenCore);

            // Small red pressurized tank
            LocalDisc(34, 30, 5, 7, RedTank, Outline);
            LocalDisc(34, 30, 3.2f, 5, RedHi, null);
            LocalBox(32, 24, 4, 2, Metal);
            LocalDot(34, 28, new Color(1f, 0.5f, 0.4f));
            LocalBox(33, 36, 2, 3, Charcoal);
            LocalDot(33, 38, MetalHi);

            // Black corrugated hoses
            Hose(tex, s, 40, 28, 44, 40);
            Hose(tex, s, 56, 28, 62, 36);
            Hose(tex, s, 58, 32, 68, 30);
            Hose(tex, s, 38, 32, 34, 34);

            // Pack mount plate under globe
            LocalBox(42, 34, 12, 4, Charcoal);
            LocalBox(44, 35, 8, 2, Metal);

            // Status bulbs
            LocalBox(42, 32, 2, 2, Green);
            LocalBox(52, 32, 2, 2, Green);
            LocalDot(42, 32, GreenCore);
            LocalDot(52, 32, GreenCore);
            LocalBox(70, 40, 2, 2, Green);

            // Re-assert helmet over hose overlaps
            LocalDisc(48, 52, 12.5f, 11.5f, OrangeDeep, Outline);
            LocalDisc(48, 52, 10.5f, 9.5f, Orange, null);
            LocalDisc(48, 53, 7, 6.5f, OrangeHi, null);
            LocalBox(46, 48, 4, 10, OrangeDeep);
            LocalBox(47, 49, 2, 8, OrangeHi);
            Weather(tex, s, 38, 42, 20, 20, 19);
            LocalBox(46, 60, 5, 4, Charcoal);
            LocalBox(47, 61, 3, 2, Blue);
            LocalDot(48, 61, BlueCore);

            SoftShade(tex, s, 48, 24, 10, 10, MoltenCore, 0.08f);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.45f), s);
        }

        // ——— Off-duty person variants (same base suit as role sprites) ———

        static Sprite MakeOffDutyWorker(int variant)
        {
            const int s = 96;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Clear(tex, s);

            PaintCrewBase(tex, s, includeBeltYellowCans: variant == 2 || variant == 4);

            void LocalDisc(float cx, float cy, float rx, float ry, Color fill, Color? edge = null) =>
                Disc(tex, s, cx, cy, rx, ry, fill, edge);
            void LocalBox(int x0, int y0, int w, int h, Color c) => Box(tex, s, x0, y0, w, h, c);
            void LocalDot(int x, int y, Color c) => Dot(tex, s, x, y, c);

            Color accent = variant switch
            {
                0 => Cyan,      // Lewis
                1 => OrangeHi,  // Mara
                2 => Green,     // Kowalski
                3 => new Color(0.72f, 0.52f, 1f), // Elena
                _ => new Color(1f, 0.42f, 0.28f), // Viktor
            };
            Color accentCore = Color.Lerp(accent, Color.white, 0.45f);

            // Helmet identity stripe
            LocalBox(40, 50, 16, 2, accent);
            LocalDot(48, 51, accentCore);

            // Soft shoulder pads (off-duty still looks equipped)
            LocalDisc(26, 46, 8, 7, SuitDeep, Outline);
            LocalDisc(26, 46, 6, 5.5f, SuitHi, null);
            LocalDisc(70, 46, 8, 7, SuitDeep, Outline);
            LocalDisc(70, 46, 6, 5.5f, SuitHi, null);
            Weather(tex, s, 20, 40, 16, 16, 3 + variant);
            Weather(tex, s, 62, 40, 16, 16, 7 + variant);

            switch (variant)
            {
                case 0: // radio / headset
                    LocalDisc(34, 30, 9, 8, Charcoal, Outline);
                    LocalDisc(34, 30, 7, 6, MetalDeep, null);
                    LocalBox(31, 27, 6, 3, Metal);
                    LocalDot(33, 28, accent);
                    LocalDot(35, 29, GreenCore);
                    LocalBox(66, 44, 3, 10, MetalDeep);
                    LocalDisc(67, 55, 3.5f, 3.5f, accent, Outline);
                    LocalDot(67, 55, accentCore);
                    Hose(tex, s, 40, 32, 48, 40);
                    break;
                case 1: // thermos + clipboard
                    LocalDisc(32, 32, 4.5f, 8, MetalDeep, Outline);
                    LocalDisc(32, 32, 3f, 6, Metal, null);
                    LocalBox(30, 38, 4, 2, OrangeDeep);
                    LocalDot(32, 30, accent);
                    LocalBox(60, 34, 12, 16, Charcoal);
                    LocalBox(61, 35, 10, 14, SuitDeep);
                    LocalBox(62, 36, 8, 12, new Color(0.12f, 0.14f, 0.16f));
                    LocalBox(63, 38, 6, 1, accent);
                    LocalBox(63, 41, 6, 1, Metal);
                    LocalBox(63, 44, 5, 1, MetalDeep);
                    break;
                case 2: // hanging wrench + spare pouch
                    LocalBox(64, 28, 4, 14, MetalDeep);
                    LocalBox(62, 26, 8, 4, Metal);
                    LocalDisc(66, 42, 4, 3, MetalHi, Outline);
                    LocalDot(66, 42, accent);
                    LocalDisc(30, 34, 6, 5, SuitDeep, Outline);
                    LocalBox(28, 32, 4, 5, Charcoal);
                    LocalDot(29, 34, Green);
                    LocalDot(29, 34, GreenCore);
                    Hose(tex, s, 58, 36, 64, 32);
                    break;
                case 3: // datapad
                    LocalBox(58, 40, 16, 14, Outline);
                    LocalBox(59, 41, 14, 12, Charcoal);
                    LocalBox(60, 42, 12, 10, Black);
                    LocalBox(61, 44, 10, 2, accent);
                    LocalBox(61, 47, 8, 1, Metal);
                    LocalBox(61, 49, 6, 1, MetalDeep);
                    LocalDot(70, 44, accentCore);
                    LocalDisc(32, 34, 5, 6, Charcoal, Outline);
                    LocalDot(32, 35, accent);
                    break;
                default: // canteen + gloves tuck
                    LocalDisc(66, 32, 6, 8, RedTank, Outline);
                    LocalDisc(66, 32, 4, 6, RedHi, null);
                    LocalBox(64, 26, 4, 2, Metal);
                    LocalDot(66, 30, accent);
                    LocalDisc(30, 36, 5.5f, 4.5f, Glove, Outline);
                    LocalDisc(30, 36, 3.5f, 2.5f, Charcoal, null);
                    LocalBox(42, 30, 12, 3, Charcoal);
                    LocalBox(44, 31, 8, 1, HazardY);
                    break;
            }

            // Re-assert helmet over gear overlaps
            LocalDisc(48, 52, 12.5f, 11.5f, OrangeDeep, Outline);
            LocalDisc(48, 52, 10.5f, 9.5f, Orange, null);
            LocalDisc(48, 53, 7, 6.5f, OrangeHi, null);
            LocalBox(46, 48, 4, 10, OrangeDeep);
            LocalBox(47, 49, 2, 8, OrangeHi);
            LocalBox(40, 50, 16, 2, accent);
            Weather(tex, s, 38, 42, 20, 20, 19 + variant);
            LocalBox(46, 60, 5, 4, Charcoal);
            LocalBox(47, 61, 3, 2, Blue);
            LocalDot(48, 61, BlueCore);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.45f), s);
        }
    }
}
