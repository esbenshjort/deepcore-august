using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Processes one ore cell at a time: cell drops under the hood, then each of 4
    /// sockets is resolved and flies out to refined gold or dirt rock.
    /// </summary>
    public sealed class WashMachine : MonoBehaviour
    {
        public enum Phase : byte { Idle, Intake, UnderHood, SplitSocket, Done }

        struct OutPiece
        {
            public Transform Tr;
            public SpriteRenderer Sr;
            public Vector3 From;
            public Vector3 To;
            public float T;
            public bool Gold;
            public bool Active;
        }

        Stockpile _refinedGold;
        Stockpile _refinedDiamond;
        Stockpile _dirt;
        SpriteRenderer _cellSr;
        SpriteRenderer _hoodSr;
        SpriteRenderer _drumSr;
        readonly SpriteRenderer[] _socketLamp = new SpriteRenderer[4];
        readonly OutPiece[] _out = new OutPiece[4];

        OreCell _current;
        RefinerPriority _priority;
        Phase _phase = Phase.Idle;
        int _socketI;
        float _phaseT;
        int _totalGold;
        int _totalDiamond;
        int _totalDirt;
        float _spin;
        bool[] _socketIsGold = new bool[4];
        IndustrialSteamPlume _steamMain;
        IndustrialSteamPlume _steamSide;
        SpriteRenderer _powerLamp;
        Light2D _powerLampLight;
        OreShimmer _cellShimmer;

        public Phase CurrentPhase => _phase;
        public bool IsBusy => _phase != Phase.Idle;
        public Vector2 WorkPoint => (Vector2)transform.localPosition + new Vector2(0f, -0.4f);

        public event System.Action<int, int, int> BatchComplete;
        public event System.Action FoundGold;
        public event System.Action FoundDiamond;
        public event System.Action FoundDirt;

        public static WashMachine Spawn(Transform parent, Vector2 localPos,
            Stockpile refinedGold, Stockpile dirt, Stockpile refinedDiamond = null)
        {
            var go = new GameObject("WashMachine");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            var bsr = body.AddComponent<SpriteRenderer>();
            bsr.sprite = YardVisualKit.WasherBody;
            bsr.sortingOrder = 20;
            DigVisualKit.ApplyLit(bsr);
            body.transform.localScale = Vector3.one * 0.95f;

            // Process / wet apron under washer — industrial work light identity (amber), not cyan
            DigVisualKit.PlaceGroundingPad(go.transform, new Vector2(0f, -0.28f), 0.85f,
                new Color(0.16f, 0.18f, 0.2f, 0.45f), sortingOrder: 8);
            var processLightGo = new GameObject("ProcessLight");
            processLightGo.transform.SetParent(go.transform, false);
            processLightGo.transform.localPosition = new Vector3(0f, -0.15f, 0f);
            var processLight = processLightGo.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(processLight,
                new Color(1f, 0.55f, 0.22f),
                intensity: 0.38f,
                outer: 0.85f,
                inner: 0.05f,
                shadows: false,
                falloff: 0.8f);

            var drum = new GameObject("Drum");
            drum.transform.SetParent(go.transform, false);
            drum.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            var dsr = drum.AddComponent<SpriteRenderer>();
            dsr.sprite = YardVisualKit.WasherDrum;
            dsr.sortingOrder = 21;
            DigVisualKit.ApplyLit(dsr);
            drum.transform.localScale = Vector3.one * 0.42f;

            // Hood / cover — cell slides under this
            var hood = new GameObject("Hood");
            hood.transform.SetParent(go.transform, false);
            hood.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            var hoodSr = hood.AddComponent<SpriteRenderer>();
            hoodSr.sprite = YardVisualKit.WasherHood;
            hoodSr.sortingOrder = 26; // above cell while under
            DigVisualKit.ApplyLit(hoodSr);
            hood.transform.localScale = Vector3.one * 0.52f;

            // Cyan viewing-slit glow on the washer hood
            var hoodGlow = new GameObject("HoodCyanGlow");
            hoodGlow.transform.SetParent(go.transform, false);
            hoodGlow.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            var hoodLight = hoodGlow.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(hoodLight,
                new Color(0.3f, 0.9f, 1f),
                intensity: 0.42f,
                outer: 0.48f,
                inner: 0.03f,
                shadows: false,
                falloff: 0.82f);
            var unlitHood = Shader.Find("Sprites/Default");
            var hoodHalo = new GameObject("HoodHalo");
            hoodHalo.transform.SetParent(hoodGlow.transform, false);
            hoodHalo.transform.localScale = Vector3.one * 0.35f;
            var hhsr = hoodHalo.AddComponent<SpriteRenderer>();
            hhsr.sprite = DigVisualKit.LanternGlow;
            hhsr.sortingOrder = 25;
            if (unlitHood != null) hhsr.sharedMaterial = new Material(unlitHood);
            hhsr.color = new Color(0.25f, 0.85f, 1f, 0.35f);

            var cellGo = new GameObject("CellInMachine");
            cellGo.transform.SetParent(go.transform, false);
            var csr = cellGo.AddComponent<SpriteRenderer>();
            csr.sortingOrder = 22;
            DigVisualKit.ApplyLit(csr);
            cellGo.SetActive(false);

            var wm = go.AddComponent<WashMachine>();
            wm._refinedGold = refinedGold;
            wm._refinedDiamond = refinedDiamond;
            wm._dirt = dirt;
            wm._cellSr = csr;
            wm._hoodSr = hoodSr;
            wm._drumSr = dsr;

            for (int i = 0; i < 4; i++)
            {
                var lamp = new GameObject($"SocketLamp{i}");
                lamp.transform.SetParent(go.transform, false);
                float lx = -0.24f + i * 0.16f;
                lamp.transform.localPosition = new Vector3(lx, -0.28f, 0f);
                var lsr = lamp.AddComponent<SpriteRenderer>();
                lsr.sprite = DigVisualKit.Pixel;
                lsr.sortingOrder = 27;
                DigVisualKit.ApplyLit(lsr);
                lamp.transform.localScale = Vector3.one * 0.05f;
                lsr.color = new Color(0.2f, 0.25f, 0.3f, 0.5f);
                wm._socketLamp[i] = lsr;

                var fly = new GameObject($"SocketOut{i}");
                fly.transform.SetParent(go.transform, false);
                var fsr = fly.AddComponent<SpriteRenderer>();
                fsr.sprite = DigVisualKit.Pixel;
                fsr.sortingOrder = 28;
                DigVisualKit.ApplyLit(fsr);
                fly.transform.localScale = Vector3.one * 0.08f;
                fly.SetActive(false);
                wm._out[i] = new OutPiece { Tr = fly.transform, Sr = fsr };
            }

            // Exhaust stack on hood — wet heat steam when spinning
            wm._steamMain = IndustrialSteamPlume.Attach(go.transform, new Vector2(0.08f, 0.42f), 32, 32)
                .Configure(
                    riseDir: new Vector2(0.12f, 1f),
                    tint: new Color(0.95f, 0.97f, 1f, 1f),
                    rate: 18f,
                    spread: 0.1f,
                    lift: 0.72f);
            // Side relief valve — thinner jet
            wm._steamSide = IndustrialSteamPlume.Attach(go.transform, new Vector2(-0.28f, 0.32f), 18, 31)
                .Configure(
                    riseDir: new Vector2(-0.35f, 0.9f),
                    tint: new Color(0.9f, 0.94f, 0.98f, 1f),
                    rate: 9f,
                    spread: 0.07f,
                    lift: 0.48f);

            var power = new GameObject("PowerLamp");
            power.transform.SetParent(go.transform, false);
            power.transform.localPosition = new Vector3(0.32f, 0.38f, 0f);
            var psr = power.AddComponent<SpriteRenderer>();
            psr.sprite = DigVisualKit.Pixel;
            psr.sortingOrder = 27;
            var unlit = Shader.Find("Sprites/Default");
            if (unlit != null) psr.sharedMaterial = new Material(unlit);
            power.transform.localScale = Vector3.one * 0.055f;
            psr.color = new Color(0.15f, 0.2f, 0.22f, 0.6f);
            wm._powerLamp = psr;

            var halo = new GameObject("PowerLampHalo");
            halo.transform.SetParent(power.transform, false);
            halo.transform.localScale = Vector3.one * 3.2f;
            var hsr = halo.AddComponent<SpriteRenderer>();
            hsr.sprite = DigVisualKit.LanternGlow;
            hsr.sortingOrder = 26;
            if (unlit != null) hsr.sharedMaterial = new Material(unlit);
            hsr.color = new Color(0.2f, 1f, 0.4f, 0f);

            var pl = power.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(pl,
                new Color(0.3f, 1f, 0.45f),
                intensity: 0f,
                outer: 0.55f,
                inner: 0.02f,
                shadows: false,
                falloff: 0.75f);
            wm._powerLampLight = pl;

            return wm;
        }

        public bool TryBegin(OreCell cell, RefinerPriority priority)
        {
            if (_phase != Phase.Idle) return false;
            _current = cell;
            _priority = priority;
            _socketI = 0;
            _totalGold = 0;
            _totalDiamond = 0;
            _totalDirt = 0;
            _phase = Phase.Intake;
            _phaseT = 0f;

            int bedrock = cell.BedrockCount;
            int seed = cell.Sockets * 7919 + Mathf.RoundToInt(cell.Mass * 100f);
            _cellSr.sprite = DigVisualKit.MakeWallChunk((byte)cell.GoldCount, bedrock, seed,
                (byte)cell.DiamondCount);
            _cellSr.color = Color.white;
            _cellSr.sortingOrder = 25; // above hood while entering
            _cellSr.gameObject.SetActive(true);
            _cellSr.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            _cellSr.transform.localScale = Vector3.one * 0.16f;
            _cellSr.transform.localRotation = Quaternion.identity;

            if (cell.IsDiamondOre || cell.IsGoldOre)
            {
                _cellShimmer = OreShimmer.Attach(_cellSr.transform, cell.IsDiamondOre,
                    (byte)cell.GoldCount, (byte)cell.DiamondCount, 0.16f);
            }

            for (int i = 0; i < 4; i++)
            {
                SetLamp(i, cell.GetSocket(i), lit: false, successPrecious: false);
                _out[i].Active = false;
                if (_out[i].Tr != null) _out[i].Tr.gameObject.SetActive(false);
            }
            return true;
        }

        public void Tick()
        {
            if (_phase == Phase.Idle)
            {
                SetSteam(false, 0f);
                return;
            }

            bool washing = _phase is Phase.UnderHood or Phase.SplitSocket;
            _spin += Time.deltaTime * (washing ? 380f : 80f);
            if (_drumSr != null)
                _drumSr.transform.localRotation = Quaternion.Euler(0f, 0f, _spin);

            // Steam builds as ore hits the wash — peaks while sockets spit
            float steam = _phase switch
            {
                Phase.Intake => Mathf.Clamp01(_phaseT / 0.45f) * 0.35f,
                Phase.UnderHood => 0.55f + 0.35f * Mathf.Sin(_phaseT * 6f) * 0.5f + 0.2f,
                Phase.SplitSocket => 0.85f + 0.15f * Mathf.Sin(_phaseT * 9f),
                Phase.Done => Mathf.Lerp(0.5f, 0f, Mathf.Clamp01(_phaseT / 0.35f)),
                _ => 0f,
            };
            SetSteam(steam > 0.05f, steam);

            _phaseT += Time.deltaTime;

            switch (_phase)
            {
                case Phase.Intake:
                    TickIntake();
                    break;
                case Phase.UnderHood:
                    TickUnderHood();
                    break;
                case Phase.SplitSocket:
                    TickSplit();
                    break;
                case Phase.Done:
                    FinishBatch();
                    break;
            }

            TickFlies();
        }

        void SetSteam(bool on, float burst)
        {
            if (_steamMain != null)
            {
                _steamMain.Emitting = on;
                _steamMain.Burst = burst;
            }
            if (_steamSide != null)
            {
                _steamSide.Emitting = on && burst > 0.4f;
                _steamSide.Burst = burst * 0.7f;
            }
            if (_powerLamp != null)
            {
                if (on)
                {
                    float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 8f);
                    _powerLamp.color = new Color(0.3f, 1f, 0.5f, pulse);
                    _powerLamp.transform.localScale = Vector3.one * (0.055f + 0.02f * burst);
                    var halo = _powerLamp.transform.Find("PowerLampHalo");
                    if (halo != null)
                    {
                        var hsr = halo.GetComponent<SpriteRenderer>();
                        if (hsr != null)
                            hsr.color = new Color(0.2f, 1f, 0.4f, 0.35f + 0.25f * pulse);
                    }
                    if (_powerLampLight != null)
                        _powerLampLight.intensity = 0.45f + 0.25f * pulse;
                }
                else
                {
                    _powerLamp.color = new Color(0.15f, 0.2f, 0.22f, 0.55f);
                    _powerLamp.transform.localScale = Vector3.one * 0.05f;
                    var halo = _powerLamp.transform.Find("PowerLampHalo");
                    if (halo != null)
                    {
                        var hsr = halo.GetComponent<SpriteRenderer>();
                        if (hsr != null) hsr.color = new Color(0.2f, 1f, 0.4f, 0f);
                    }
                    if (_powerLampLight != null)
                        _powerLampLight.intensity = 0f;
                }
            }
        }

        void TickIntake()
        {
            // Slide down into the machine mouth
            float t = Mathf.Clamp01(_phaseT / 0.45f);
            if (_cellSr != null)
            {
                _cellSr.transform.localPosition = Vector3.Lerp(
                    new Vector3(0f, 0.55f, 0f),
                    new Vector3(0f, 0.22f, 0f), t);
                float s = Mathf.Lerp(0.16f, 0.12f, t);
                _cellSr.transform.localScale = Vector3.one * s;
            }
            if (t >= 1f)
            {
                _phase = Phase.UnderHood;
                _phaseT = 0f;
                if (_cellSr != null)
                    _cellSr.sortingOrder = 22; // under hood (hood is 26)
            }
        }

        void TickUnderHood()
        {
            // Tuck under hood / into drum
            float t = Mathf.Clamp01(_phaseT / 0.4f);
            if (_cellSr != null)
            {
                _cellSr.transform.localPosition = Vector3.Lerp(
                    new Vector3(0f, 0.22f, 0f),
                    new Vector3(0f, 0.02f, 0f), t);
                float s = Mathf.Lerp(0.12f, 0.07f, t);
                _cellSr.transform.localScale = Vector3.one * s;
                var c = _cellSr.color;
                c.a = Mathf.Lerp(1f, 0.35f, t);
                _cellSr.color = c;
            }
            if (_hoodSr != null)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(_phaseT * 10f);
                _hoodSr.color = new Color(0.2f, 0.75f, 0.9f, pulse);
            }
            if (t >= 1f)
            {
                _phase = Phase.SplitSocket;
                _phaseT = 0f;
                _socketI = 0;
                if (_cellSr != null) _cellSr.gameObject.SetActive(false);
            }
        }

        void TickSplit()
        {
            // One socket every ~0.42s — resolve + launch piece
            const float perSocket = 0.42f;
            if (_socketI < 4 && _phaseT >= perSocket)
            {
                _phaseT = 0f;
                LaunchSocket(_socketI);
                _socketI++;
            }

            if (_socketI >= 4)
            {
                // Wait until all flies finished
                bool any = false;
                for (int i = 0; i < 4; i++)
                    if (_out[i].Active) { any = true; break; }
                if (!any)
                {
                    _phase = Phase.Done;
                    _phaseT = 0f;
                }
            }
        }

        void LaunchSocket(int i)
        {
            var kind = _current.GetSocket(i);
            bool foundPrecious = RollSocket(kind);
            bool foundGold = foundPrecious && kind == SocketKind.Gold;
            bool foundDia = foundPrecious && kind == SocketKind.Diamond;
            _socketIsGold[i] = foundGold || foundDia;
            SetLamp(i, kind, lit: true, successPrecious: foundPrecious);

            if (foundGold)
            {
                _totalGold++;
                _refinedGold?.DepositRefinedGold(1);
                FoundGold?.Invoke();
            }
            else if (foundDia)
            {
                _totalDiamond++;
                _refinedDiamond?.DepositRefinedDiamond(1);
                FoundDiamond?.Invoke();
            }
            else
            {
                _totalDirt++;
                _dirt?.DepositDirt(1, mass: _current.Mass * 0.25f);
                FoundDirt?.Invoke();
            }

            var piece = _out[i];
            if (piece.Tr == null) return;

            Vector3 localFrom = new(-0.24f + i * 0.16f, -0.05f, 0f);
            piece.From = transform.TransformPoint(localFrom);
            Vector3 dest = foundGold && _refinedGold != null
                ? _refinedGold.transform.position + new Vector3(0f, 0.1f, 0f)
                : foundDia && _refinedDiamond != null
                    ? _refinedDiamond.transform.position + new Vector3(0f, 0.1f, 0f)
                    : _dirt != null
                        ? _dirt.transform.position + new Vector3(0f, 0.1f, 0f)
                        : piece.From + Vector3.down * 0.5f;
            piece.To = dest;
            piece.T = 0f;
            piece.Gold = foundGold || foundDia;
            piece.Active = true;
            piece.Tr.position = piece.From;
            piece.Tr.gameObject.SetActive(true);
            if (piece.Sr != null)
            {
                piece.Sr.color = foundDia
                    ? new Color(0.75f, 0.92f, 1f, 1f)
                    : foundGold
                        ? new Color(1f, 0.88f, 0.3f, 1f)
                        : kind == SocketKind.Bedrock
                            ? new Color(0.35f, 0.7f, 0.9f, 1f)
                            : new Color(0.55f, 0.45f, 0.35f, 1f);
                piece.Tr.localScale = Vector3.one * (foundDia ? 0.1f : foundGold ? 0.09f : 0.075f);
                if (foundDia)
                    OreShimmer.Attach(piece.Tr, true, 0, 1, 0.1f);
                else if (foundGold)
                    OreShimmer.Attach(piece.Tr, false, 1, 0, 0.09f);
            }
            _out[i] = piece;
        }

        bool RollSocket(SocketKind kind)
        {
            if (_priority == RefinerPriority.DiamondOre)
            {
                if (kind != SocketKind.Diamond) return false;
                int n = _current.DiamondCount;
                float chance = 0.55f;
                if (n > 1) chance += (n - 1) * 0.1f;
                return Random.value < chance;
            }

            if (_priority == RefinerPriority.GoldOre)
            {
                if (kind != SocketKind.Gold) return false;
                int n = _current.GoldCount;
                float chance = 0.5f;
                if (n > 1) chance += (n - 1) * 0.1f;
                return Random.value < chance;
            }

            // OreRock / mixed wash — each socket rolls by type
            if (kind == SocketKind.Rock) return Random.value < 0.01f;
            if (kind == SocketKind.Gold) return Random.value < 0.5f;
            if (kind == SocketKind.Diamond) return Random.value < 0.55f;
            return false;
        }

        void TickFlies()
        {
            for (int i = 0; i < 4; i++)
            {
                if (!_out[i].Active) continue;
                var p = _out[i];
                p.T += Time.deltaTime / 0.5f;
                float u = Mathf.Clamp01(p.T);
                // Arc
                Vector3 mid = Vector3.Lerp(p.From, p.To, 0.5f) + Vector3.up * 0.25f;
                Vector3 a = Vector3.Lerp(p.From, mid, u);
                Vector3 b = Vector3.Lerp(mid, p.To, u);
                p.Tr.position = Vector3.Lerp(a, b, u);
                if (u >= 1f)
                {
                    p.Active = false;
                    p.Tr.gameObject.SetActive(false);
                }
                _out[i] = p;
            }
        }

        void FinishBatch()
        {
            int g = _totalGold, d = _totalDirt, dia = _totalDiamond;
            _totalGold = 0;
            _totalDiamond = 0;
            _totalDirt = 0;
            _phase = Phase.Idle;
            _phaseT = 0f;
            SetSteam(false, 0f);
            if (_hoodSr != null)
                _hoodSr.color = Color.white;
            for (int i = 0; i < 4; i++)
                _socketLamp[i].color = new Color(0.2f, 0.25f, 0.3f, 0.45f);
            BatchComplete?.Invoke(g, d, dia);
        }

        void SetLamp(int i, SocketKind kind, bool lit, bool successPrecious)
        {
            if (_socketLamp[i] == null) return;
            Color c = kind switch
            {
                SocketKind.Gold => new Color(1f, 0.82f, 0.25f, 0.95f),
                SocketKind.Diamond => new Color(0.65f, 0.9f, 1f, 0.95f),
                SocketKind.Bedrock => new Color(0.35f, 0.75f, 0.95f, 0.85f),
                _ => new Color(0.55f, 0.48f, 0.4f, 0.85f),
            };
            if (lit && successPrecious) c = kind == SocketKind.Diamond
                ? new Color(0.9f, 0.98f, 1f, 1f)
                : new Color(1f, 0.95f, 0.45f, 1f);
            else if (lit) c *= 0.65f;
            else c *= 0.4f;
            _socketLamp[i].color = c;
            _socketLamp[i].transform.localScale = Vector3.one * (lit ? 0.07f : 0.048f);
            if (lit && kind == SocketKind.Diamond && successPrecious)
                OreShimmer.Attach(_socketLamp[i].transform, true, 0, 1, 0.07f);
            else if (lit && kind == SocketKind.Gold && successPrecious)
                OreShimmer.Attach(_socketLamp[i].transform, false, 1, 0, 0.07f);
        }

        public void ResetMachine()
        {
            _phase = Phase.Idle;
            _phaseT = 0f;
            _socketI = 0;
            _totalGold = _totalDirt = _totalDiamond = 0;
            SetSteam(false, 0f);
            if (_cellSr != null) _cellSr.gameObject.SetActive(false);
            for (int i = 0; i < 4; i++)
            {
                _out[i].Active = false;
                if (_out[i].Tr != null) _out[i].Tr.gameObject.SetActive(false);
                if (_socketLamp[i] != null)
                    _socketLamp[i].color = new Color(0.2f, 0.25f, 0.3f, 0.45f);
            }
            if (_hoodSr != null) _hoodSr.color = Color.white;
        }
    }
}
