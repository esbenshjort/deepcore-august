using UnityEngine;

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
        int _totalDirt;
        float _spin;
        bool[] _socketIsGold = new bool[4];

        public Phase CurrentPhase => _phase;
        public bool IsBusy => _phase != Phase.Idle;
        public Vector2 WorkPoint => (Vector2)transform.localPosition + new Vector2(0f, -0.4f);

        public event System.Action<int, int> BatchComplete;
        public event System.Action FoundGold;
        public event System.Action FoundDirt;

        public static WashMachine Spawn(Transform parent, Vector2 localPos,
            Stockpile refinedGold, Stockpile dirt)
        {
            var go = new GameObject("WashMachine");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            var bsr = body.AddComponent<SpriteRenderer>();
            bsr.sprite = MakeBodySprite();
            bsr.sortingOrder = 20;
            DigVisualKit.ApplyLit(bsr);
            body.transform.localScale = Vector3.one * 1.05f;

            var drum = new GameObject("Drum");
            drum.transform.SetParent(go.transform, false);
            drum.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            var dsr = drum.AddComponent<SpriteRenderer>();
            dsr.sprite = MakeDrumSprite();
            dsr.sortingOrder = 21;
            DigVisualKit.ApplyLit(dsr);
            drum.transform.localScale = Vector3.one * 0.38f;

            // Hood / cover — cell slides under this
            var hood = new GameObject("Hood");
            hood.transform.SetParent(go.transform, false);
            hood.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            var hoodSr = hood.AddComponent<SpriteRenderer>();
            hoodSr.sprite = MakeHoodSprite();
            hoodSr.sortingOrder = 26; // above cell while under
            DigVisualKit.ApplyLit(hoodSr);
            hood.transform.localScale = Vector3.one * 0.55f;

            var cellGo = new GameObject("CellInMachine");
            cellGo.transform.SetParent(go.transform, false);
            var csr = cellGo.AddComponent<SpriteRenderer>();
            csr.sortingOrder = 22;
            DigVisualKit.ApplyLit(csr);
            cellGo.SetActive(false);

            var wm = go.AddComponent<WashMachine>();
            wm._refinedGold = refinedGold;
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

            return wm;
        }

        public bool TryBegin(OreCell cell, RefinerPriority priority)
        {
            if (_phase != Phase.Idle) return false;
            _current = cell;
            _priority = priority;
            _socketI = 0;
            _totalGold = 0;
            _totalDirt = 0;
            _phase = Phase.Intake;
            _phaseT = 0f;

            int bedrock = cell.BedrockCount;
            int seed = cell.Sockets * 7919 + Mathf.RoundToInt(cell.Mass * 100f);
            _cellSr.sprite = DigVisualKit.MakeWallChunk((byte)cell.GoldCount, bedrock, seed);
            _cellSr.color = Color.white;
            _cellSr.sortingOrder = 25; // above hood while entering
            _cellSr.gameObject.SetActive(true);
            _cellSr.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            _cellSr.transform.localScale = Vector3.one * 0.16f;
            _cellSr.transform.localRotation = Quaternion.identity;

            for (int i = 0; i < 4; i++)
            {
                SetLamp(i, cell.GetSocket(i), lit: false, successGold: false);
                _out[i].Active = false;
                if (_out[i].Tr != null) _out[i].Tr.gameObject.SetActive(false);
            }
            return true;
        }

        public void Tick()
        {
            if (_phase == Phase.Idle) return;

            _spin += Time.deltaTime * (_phase is Phase.UnderHood or Phase.SplitSocket ? 380f : 80f);
            if (_drumSr != null)
                _drumSr.transform.localRotation = Quaternion.Euler(0f, 0f, _spin);

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
            bool foundGold = RollSocket(kind);
            _socketIsGold[i] = foundGold;
            SetLamp(i, kind, lit: true, successGold: foundGold);

            if (foundGold)
            {
                _totalGold++;
                _refinedGold?.DepositRefinedGold(1);
                FoundGold?.Invoke();
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
                : _dirt != null
                    ? _dirt.transform.position + new Vector3(0f, 0.1f, 0f)
                    : piece.From + Vector3.down * 0.5f;
            piece.To = dest;
            piece.T = 0f;
            piece.Gold = foundGold;
            piece.Active = true;
            piece.Tr.position = piece.From;
            piece.Tr.gameObject.SetActive(true);
            if (piece.Sr != null)
            {
                piece.Sr.color = foundGold
                    ? new Color(1f, 0.88f, 0.3f, 1f)
                    : kind == SocketKind.Bedrock
                        ? new Color(0.35f, 0.7f, 0.9f, 1f)
                        : new Color(0.55f, 0.45f, 0.35f, 1f);
                piece.Tr.localScale = Vector3.one * (foundGold ? 0.09f : 0.075f);
            }
            _out[i] = piece;
        }

        bool RollSocket(SocketKind kind)
        {
            if (_priority == RefinerPriority.GoldOre)
            {
                if (kind != SocketKind.Gold) return false;
                int n = _current.GoldCount;
                float chance = 0.5f;
                if (n > 1) chance += (n - 1) * 0.1f;
                return Random.value < chance;
            }

            if (kind == SocketKind.Rock) return Random.value < 0.01f;
            if (kind == SocketKind.Gold) return Random.value < 0.5f;
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
            int g = _totalGold, d = _totalDirt;
            _totalGold = 0;
            _totalDirt = 0;
            _phase = Phase.Idle;
            _phaseT = 0f;
            if (_hoodSr != null)
                _hoodSr.color = Color.white;
            for (int i = 0; i < 4; i++)
                _socketLamp[i].color = new Color(0.2f, 0.25f, 0.3f, 0.45f);
            BatchComplete?.Invoke(g, d);
        }

        void SetLamp(int i, SocketKind kind, bool lit, bool successGold)
        {
            if (_socketLamp[i] == null) return;
            Color c = kind switch
            {
                SocketKind.Gold => new Color(1f, 0.82f, 0.25f, 0.95f),
                SocketKind.Bedrock => new Color(0.35f, 0.75f, 0.95f, 0.85f),
                _ => new Color(0.55f, 0.48f, 0.4f, 0.85f),
            };
            if (lit && successGold) c = new Color(1f, 0.95f, 0.45f, 1f);
            else if (lit) c *= 0.65f;
            else c *= 0.4f;
            _socketLamp[i].color = c;
            _socketLamp[i].transform.localScale = Vector3.one * (lit ? 0.07f : 0.048f);
        }

        public void ResetMachine()
        {
            _phase = Phase.Idle;
            _phaseT = 0f;
            _socketI = 0;
            _totalGold = _totalDirt = 0;
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

        static Sprite MakeBodySprite()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                bool frame = x < 3 || x > s - 4 || y < 3 || y > s - 4;
                bool tank = x > 12 && x < 52 && y > 14 && y < 48;
                bool chuteL = x > 4 && x < 14 && y < 18;
                bool chuteR = x > 50 && x < 60 && y < 18;
                if (frame) tex.SetPixel(x, y, new Color(0.25f, 0.55f, 0.65f, 0.95f));
                else if (tank) tex.SetPixel(x, y, new Color(0.07f, 0.11f, 0.15f, 0.94f));
                else if (chuteL) tex.SetPixel(x, y, new Color(0.4f, 0.35f, 0.28f, 0.9f));
                else if (chuteR) tex.SetPixel(x, y, new Color(0.7f, 0.55f, 0.2f, 0.9f));
                else tex.SetPixel(x, y, Color.clear);
            }
            for (int x = 16; x < 48; x++)
                tex.SetPixel(x, 28, new Color(0.2f, 0.7f, 0.85f, 0.5f));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.35f), s);
        }

        static Sprite MakeHoodSprite()
        {
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - 15.5f) / 14f;
                float dy = (y - 10f) / 9f;
                if (dy < 0f || dx * dx + dy * dy > 1f) { tex.SetPixel(x, y, Color.clear); continue; }
                tex.SetPixel(x, y, new Color(0.18f, 0.55f, 0.65f, 0.88f));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.2f), s);
        }

        static Sprite MakeDrumSprite()
        {
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - 15.5f) / 12f;
                float dy = (y - 15.5f) / 12f;
                float d = dx * dx + dy * dy;
                if (d > 1f) { tex.SetPixel(x, y, Color.clear); continue; }
                var c = new Color(0.3f, 0.38f, 0.42f, 0.9f);
                if (d > 0.72f) c = new Color(0.2f, 0.55f, 0.65f, 0.95f);
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }
    }
}
