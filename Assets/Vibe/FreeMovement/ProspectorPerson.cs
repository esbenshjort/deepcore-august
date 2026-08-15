using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    public enum ScanDistance : byte { Short = 0, Medium = 1, Long = 2 }
    public enum ScanWidth : byte { Narrow = 0, Wide = 1 }

    /// <summary>
    /// Prospector with a soft quarter-circle radar (3 rows × 3 cols).
    /// Space starts a slow sweep into the rock; hints pop as the wave reaches them.
    /// </summary>
    public sealed class ProspectorPerson : MonoBehaviour
    {
        const float FullHalfAngle = 45f;
        const int RowCount = 3;
        const int ColCount = 3;

        struct PendingHit
        {
            public float Dist01; // 0 near → 1 far (within selected range)
            public int X, Y;
            public bool Gold;
            public float Strength;
        }

        FineTerrainWorld _world;
        ScanViewOverlay _scanView;
        float _radius = 0.1f;
        float _moveSpeed = 1.55f;
        Transform _facingRoot;
        Transform _coneRoot;
        readonly List<LineRenderer> _arcCore = new(3);
        readonly List<LineRenderer> _arcGlow = new(3);
        readonly List<LineRenderer> _radialCore = new(4);
        readonly List<LineRenderer> _radialGlow = new(4);
        LineRenderer _sweepCore;
        LineRenderer _sweepGlow;
        MeshFilter _sweepFillMf;
        MeshRenderer _sweepFillMr;
        Mesh _sweepFillMesh;
        Light2D _lamp;

        bool _hudVisible = true;
        bool _scanning;
        float _scanT;
        float _scanDuration = 2f;
        float _scanCooldown;
        float _pulse; // idle radar breath
        readonly List<PendingHit> _pending = new(48);
        Vector2 _scanOrigin;
        Vector2 _scanFwd;
        float _scanRangeWorld;
        float _reliability;

        public ScanDistance Distance { get; private set; } = ScanDistance.Medium;
        public ScanWidth Width { get; private set; } = ScanWidth.Narrow;
        public bool RadarOn { get; private set; } = true;
        public bool IsScanning => _scanning;

        public Vector2 Position => transform.localPosition;
        public Vector2 Facing => _facingRoot != null ? (Vector2)_facingRoot.up : Vector2.up;

        public int ActiveRows => Distance switch
        {
            ScanDistance.Short => 1,
            ScanDistance.Medium => 2,
            _ => 3,
        };

        // Keep full Long grid on-screen (~3 world units)
        public float MaxRangeCells => 30f;
        public float ScanRangeCells => MaxRangeCells * (ActiveRows / (float)RowCount);

        public static ProspectorPerson Spawn(Transform parent, FineTerrainWorld world,
            Vector2 start, ScanViewOverlay scanView)
        {
            var go = new GameObject("Prospector");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = start;

            var facing = new GameObject("Facing");
            facing.transform.SetParent(go.transform, false);

            var body = new GameObject("Body");
            body.transform.SetParent(facing.transform, false);
            var sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = MakePersonSprite();
            sr.sortingOrder = 42;
            DigVisualKit.ApplyLit(sr);
            body.transform.localScale = Vector3.one * 0.38f;

            var cone = new GameObject("RadarCone");
            cone.transform.SetParent(facing.transform, false);

            var light = go.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(light,
                new Color(0.55f, 0.85f, 1f),
                intensity: 0.35f,
                outer: 1.1f,
                inner: 0.05f,
                shadows: false,
                falloff: 0.7f);

            var p = go.AddComponent<ProspectorPerson>();
            p._world = world;
            p._scanView = scanView;
            p._facingRoot = facing.transform;
            p._coneRoot = cone.transform;
            p._lamp = light;
            p.BuildConeLines();
            p.RebuildConeMesh();
            return p;
        }

        public void ResetTo(Vector2 pos)
        {
            transform.localPosition = pos;
            if (_facingRoot != null) _facingRoot.localRotation = Quaternion.identity;
            _scanCooldown = 0f;
            _scanning = false;
            _pending.Clear();
            if (_sweepCore != null) _sweepCore.gameObject.SetActive(false);
            if (_sweepGlow != null) _sweepGlow.gameObject.SetActive(false);
            if (_sweepFillMr != null) _sweepFillMr.enabled = false;
            RebuildConeMesh();
        }

        public void SetDistance(ScanDistance d)
        {
            if (_scanning) return;
            Distance = d;
            RebuildConeMesh();
        }

        public void SetWidth(ScanWidth w)
        {
            if (_scanning) return;
            Width = w;
            RebuildConeMesh();
        }

        public void SetRadar(bool on)
        {
            RadarOn = on;
            ApplyHudVisibility();
        }

        public void ToggleRadar() => SetRadar(!RadarOn);

        public void SetHudVisible(bool visible)
        {
            _hudVisible = visible;
            ApplyHudVisibility();
        }

        void ApplyHudVisibility()
        {
            if (_coneRoot != null)
                _coneRoot.gameObject.SetActive(_hudVisible && RadarOn);
        }

        public void Tick(Vector2 wasd, bool scanPulse)
        {
            if (_world == null) return;
            if (_scanCooldown > 0f) _scanCooldown -= Time.deltaTime;
            _pulse += Time.deltaTime;

            if (!_scanning && wasd.sqrMagnitude > 0.01f)
            {
                Vector2 dir = wasd.normalized;
                Face(dir);
                Step(dir);
            }

            if (_hudVisible && RadarOn)
                RebuildConeMesh();

            if (_scanning)
                TickScanSweep();
            else if (scanPulse && _scanCooldown <= 0f)
            {
                if (!RadarOn) SetRadar(true);
                BeginScan();
            }
        }

        public void FaceToward(Vector2 terrainPoint)
        {
            if (_scanning) return;
            Vector2 dir = terrainPoint - Position;
            if (dir.sqrMagnitude < 0.0001f || _facingRoot == null) return;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            _facingRoot.localRotation = Quaternion.Euler(0f, 0f, ang);
            RebuildConeMesh();
        }

        void Face(Vector2 dir)
        {
            if (_facingRoot == null) return;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            _facingRoot.localRotation = Quaternion.RotateTowards(
                _facingRoot.localRotation,
                Quaternion.Euler(0f, 0f, ang),
                360f * Time.deltaTime);
        }

        void Step(Vector2 dir)
        {
            float step = _moveSpeed * Time.deltaTime;
            Vector2 pos = Position;
            Vector2 next = pos + dir * step;
            if (!_world.CircleHitsSolid(next, _radius))
            {
                transform.localPosition = next;
                return;
            }
            Vector2 nx = pos + new Vector2(dir.x, 0f) * step;
            if (!_world.CircleHitsSolid(nx, _radius))
            {
                transform.localPosition = nx;
                return;
            }
            Vector2 ny = pos + new Vector2(0f, dir.y) * step;
            if (!_world.CircleHitsSolid(ny, _radius))
                transform.localPosition = ny;
        }

        void BeginScan()
        {
            if (_scanView == null || _world == null) return;
            if (!_scanView.Visible)
                _scanView.SetVisible(true);

            _scanOrigin = Position;
            _scanFwd = Facing.normalized;
            if (_scanFwd.sqrMagnitude < 0.01f) _scanFwd = Vector2.up;
            _scanRangeWorld = ScanRangeCells * _world.CellSize;
            _reliability = Distance switch
            {
                ScanDistance.Short => 0.82f,
                ScanDistance.Medium => 0.68f,
                _ => 0.52f,
            };
            _scanDuration = Distance switch
            {
                ScanDistance.Short => 1.35f,
                ScanDistance.Medium => 2.25f,
                _ => 3.4f,
            };

            CollectPendingHits();
            _pending.Sort((a, b) => a.Dist01.CompareTo(b.Dist01));

            _scanT = 0f;
            _scanning = true;
            _scanCooldown = _scanDuration + 0.4f;
            if (_sweepCore != null) _sweepCore.gameObject.SetActive(true);
            if (_sweepGlow != null) _sweepGlow.gameObject.SetActive(true);
            if (_sweepFillMr != null) _sweepFillMr.enabled = true;
            UpdateSweepArc(0.02f);
        }

        void CollectPendingHits()
        {
            _pending.Clear();
            float cs = _world.CellSize;
            float range = _scanRangeWorld;
            float colWidth = (FullHalfAngle * 2f) / ColCount;
            int colStart = Width == ScanWidth.Narrow ? 1 : 0;
            int colEnd = Width == ScanWidth.Narrow ? 1 : ColCount - 1;

            int samplesPerCol = 4;
            int distSteps = Mathf.Max(ActiveRows * 10, Mathf.CeilToInt(ScanRangeCells));

            for (int col = colStart; col <= colEnd; col++)
            {
                float a0 = -FullHalfAngle + col * colWidth;
                float a1 = a0 + colWidth;
                for (int s = 0; s < samplesPerCol; s++)
                {
                    float ta = samplesPerCol == 1 ? 0.5f : s / (float)(samplesPerCol - 1);
                    float ang = Mathf.Lerp(a0 + colWidth * 0.18f, a1 - colWidth * 0.18f, ta);
                    Vector2 dir = Rotate(_scanFwd, ang);

                    for (int di = 1; di <= distSteps; di++)
                    {
                        float u = di / (float)distSteps;
                        Vector2 hit = _scanOrigin + dir * (u * range);
                        var cell = _world.WorldToCell(hit);
                        if (!_world.InBounds(cell.x, cell.y)) break;
                        if (_world.IsExcavated(cell.x, cell.y)) continue;

                        var terrain = _world.Get(cell.x, cell.y);
                        bool trueGold = terrain.GoldCount > 0;
                        bool trueBed = terrain.BedrockCount >= 2;

                        float rowPos = u * ActiveRows;
                        float frac = rowPos - Mathf.Floor(rowPos);
                        if (frac < 0.28f || frac > 0.72f) continue;

                        bool reportGold = false;
                        bool reportBed = false;
                        if (Random.value < _reliability)
                        {
                            reportGold = trueGold && Random.value < (0.5f + terrain.GoldCount * 0.1f);
                            reportBed = trueBed && Random.value < 0.7f;
                        }
                        float falseRate = (1f - _reliability) * 0.16f;
                        if (!reportGold && Random.value < falseRate * 0.22f) reportGold = true;
                        if (!reportBed && Random.value < falseRate * 0.2f) reportBed = true;

                        float strength = Mathf.Lerp(0.7f, 1f, _reliability);
                        if (reportGold)
                            _pending.Add(new PendingHit { Dist01 = u, X = cell.x, Y = cell.y, Gold = true, Strength = strength });
                        if (reportBed)
                            _pending.Add(new PendingHit { Dist01 = u, X = cell.x, Y = cell.y, Gold = false, Strength = strength });
                    }
                }
            }

            // Thin to sparse clusters ahead of time
            ThinPending();
        }

        void ThinPending()
        {
            if (_pending.Count == 0) return;
            // Keep seed points close enough to merge into soft outline zones
            const int minSep = 4;
            var kept = new List<PendingHit>(16);
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < _pending.Count; i++)
                {
                    var h = _pending[i];
                    if (pass == 0 && !h.Gold) continue;
                    if (pass == 1 && h.Gold) continue;
                    bool near = false;
                    for (int k = 0; k < kept.Count; k++)
                    {
                        if (kept[k].Gold != h.Gold) continue;
                        if (Mathf.Abs(kept[k].X - h.X) + Mathf.Abs(kept[k].Y - h.Y) < minSep)
                        {
                            near = true;
                            break;
                        }
                    }
                    if (!near) kept.Add(h);
                    if (kept.Count >= 12) break;
                }
                if (kept.Count >= 12) break;
            }
            _pending.Clear();
            _pending.AddRange(kept);
            _pending.Sort((a, b) => a.Dist01.CompareTo(b.Dist01));
        }

        void TickScanSweep()
        {
            // Ease-out so it lingers a bit as it reaches far bands
            _scanT += Time.deltaTime / _scanDuration;
            float u = Mathf.Clamp01(_scanT);
            float sweep = 1f - Mathf.Pow(1f - u, 1.55f); // smooth pull outward

            UpdateSweepArc(Mathf.Max(0.04f, sweep));

            // Reveal hits the wave has passed
            while (_pending.Count > 0 && _pending[0].Dist01 <= sweep + 0.02f)
            {
                var h = _pending[0];
                _pending.RemoveAt(0);
                if (h.Gold)
                    _scanView.AddHint(h.X, h.Y, gold: h.Strength, bedrock: 0f, bleed: 3);
                else
                    _scanView.AddHint(h.X, h.Y, gold: 0f, bedrock: h.Strength, bleed: 3);
                _scanView.SoftPing();
            }

            if (u >= 1f)
            {
                // Flush leftovers
                for (int i = 0; i < _pending.Count; i++)
                {
                    var h = _pending[i];
                    if (h.Gold)
                        _scanView.AddHint(h.X, h.Y, gold: h.Strength, bedrock: 0f, bleed: 3);
                    else
                        _scanView.AddHint(h.X, h.Y, gold: 0f, bedrock: h.Strength, bleed: 3);
                }
                _pending.Clear();
                _scanning = false;
                if (_sweepCore != null) _sweepCore.gameObject.SetActive(false);
                if (_sweepGlow != null) _sweepGlow.gameObject.SetActive(false);
                if (_sweepFillMr != null) _sweepFillMr.enabled = false;
                _scanView.Flash();
            }
        }

        void UpdateSweepArc(float sweep01)
        {
            if (_sweepCore == null || _world == null) return;
            float r = _scanRangeWorld * sweep01;
            float half = Width == ScanWidth.Narrow
                ? FullHalfAngle / ColCount
                : FullHalfAngle;
            const int res = 32;
            SetArcPoints(_sweepGlow, r, -half, half, res);
            SetArcPoints(_sweepCore, r, -half, half, res);
            BuildSweepFill(r, -half, half, res);

            float pulse = 0.7f + 0.3f * Mathf.Sin(_pulse * 9f);
            // Cyberpunk leading edge: hairline core + soft bloom
            Color glow = new(0.25f, 0.95f, 1f, 0.08f * pulse);
            Color core = new(0.85f, 1f, 1f, 0.55f * pulse);
            _sweepGlow.startColor = _sweepGlow.endColor = glow;
            _sweepCore.startColor = _sweepCore.endColor = core;
            _sweepGlow.widthMultiplier = 0.055f;
            _sweepCore.widthMultiplier = 0.012f;

            if (_sweepFillMr != null && _sweepFillMr.material != null)
            {
                float fillA = 0.028f + 0.022f * pulse * (1f - sweep01 * 0.4f);
                _sweepFillMr.material.color = new Color(0.2f, 0.95f, 1f, fillA);
            }
        }

        void BuildSweepFill(float r, float a0, float a1, int res)
        {
            if (_sweepFillMesh == null || r < 0.01f) return;
            int vCount = res + 2;
            var verts = new Vector3[vCount];
            var cols = new Color[vCount];
            var tris = new int[res * 3];
            verts[0] = Vector3.zero;
            cols[0] = new Color(0.4f, 0.9f, 1f, 0.12f);
            for (int i = 0; i <= res; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)res);
                verts[i + 1] = (Vector3)(Rotate(Vector2.up, a) * r);
                // Fade toward leading edge
                cols[i + 1] = new Color(0.45f, 0.92f, 1f, 0.02f);
            }
            for (int i = 0; i < res; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }
            _sweepFillMesh.Clear();
            _sweepFillMesh.vertices = verts;
            _sweepFillMesh.colors = cols;
            _sweepFillMesh.triangles = tris;
            var uvs = new Vector2[vCount];
            for (int i = 0; i < vCount; i++) uvs[i] = Vector2.zero;
            _sweepFillMesh.uv = uvs;
            _sweepFillMesh.RecalculateBounds();
        }

        void BuildConeLines()
        {
            _arcCore.Clear();
            _arcGlow.Clear();
            _radialCore.Clear();
            _radialGlow.Clear();

            for (int i = 0; i < RowCount; i++)
            {
                _arcGlow.Add(MakeLr($"ArcGlow{i}", 43));
                _arcCore.Add(MakeLr($"Arc{i}", 45));
            }
            for (int i = 0; i < ColCount + 1; i++)
            {
                _radialGlow.Add(MakeLr($"RadialGlow{i}", 43));
                _radialCore.Add(MakeLr($"Radial{i}", 45));
            }
            _sweepGlow = MakeLr("SweepGlow", 46);
            _sweepCore = MakeLr("Sweep", 47);
            _sweepGlow.gameObject.SetActive(false);
            _sweepCore.gameObject.SetActive(false);

            var fillGo = new GameObject("SweepFill");
            fillGo.transform.SetParent(_coneRoot, false);
            _sweepFillMf = fillGo.AddComponent<MeshFilter>();
            _sweepFillMr = fillGo.AddComponent<MeshRenderer>();
            _sweepFillMesh = new Mesh { name = "ScanSweepFill" };
            _sweepFillMf.sharedMesh = _sweepFillMesh;
            var sh = Shader.Find("Sprites/Default")
                     ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh != null)
            {
                var mat = new Material(sh) { name = "ScanSweepFill_Mat", color = new Color(0.2f, 0.95f, 1f, 0.03f) };
                _sweepFillMr.sharedMaterial = mat;
            }
            _sweepFillMr.sortingOrder = 44;
            _sweepFillMr.enabled = false;
        }

        LineRenderer MakeLr(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_coneRoot, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = false;
            lr.widthMultiplier = 0.012f;
            var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            lr.material = sh != null ? new Material(sh) : null;
            lr.sortingOrder = order;
            lr.numCapVertices = 1;
            lr.numCornerVertices = 1;
            return lr;
        }

        void RebuildConeMesh()
        {
            if (_coneRoot == null || _world == null) return;
            if (_arcCore.Count < RowCount || _radialCore.Count < ColCount + 1) return;

            float fullRange = MaxRangeCells * _world.CellSize;
            float half = FullHalfAngle;
            const int arcRes = 36;
            float breath = 0.88f + 0.12f * Mathf.Sin(_pulse * 2.4f);

            // Cyberpunk HUD: hairline neon + faint bloom (not chunky CAD lines)
            Color activeCore = new(0.55f, 1f, 1f, 0.38f * breath);
            Color idleCore = new(0.35f, 0.75f, 0.9f, 0.12f);
            Color activeGlow = new(0.15f, 0.9f, 1f, 0.055f * breath);
            Color idleGlow = new(0.2f, 0.55f, 0.75f, 0.02f);

            const float coreW = 0.011f;
            const float glowW = 0.048f;

            for (int row = 0; row < RowCount; row++)
            {
                float r = fullRange * ((row + 1) / (float)RowCount);
                bool on = (row + 1) <= ActiveRows;
                SetArcPoints(_arcGlow[row], r, -half, half, arcRes);
                SetArcPoints(_arcCore[row], r, -half, half, arcRes);
                _arcGlow[row].startColor = _arcGlow[row].endColor = on ? activeGlow : idleGlow;
                _arcCore[row].startColor = _arcCore[row].endColor = on ? activeCore : idleCore;
                _arcGlow[row].widthMultiplier = on ? glowW : glowW * 0.7f;
                _arcCore[row].widthMultiplier = on ? coreW : coreW * 0.75f;
            }

            for (int i = 0; i < ColCount + 1; i++)
            {
                float a = Mathf.Lerp(-half, half, i / (float)ColCount);
                bool centerish = i == 1 || i == 2;
                bool outer = i == 0 || i == ColCount;
                bool lit = Width == ScanWidth.Wide || centerish;
                Color core = lit ? activeCore : idleCore;
                Color glow = lit ? activeGlow : idleGlow;
                if (outer && Width == ScanWidth.Narrow)
                {
                    core = new Color(idleCore.r, idleCore.g, idleCore.b, 0.06f);
                    glow = new Color(idleGlow.r, idleGlow.g, idleGlow.b, 0.015f);
                }

                Vector2 d = Rotate(Vector2.up, a);
                SetLine(_radialGlow[i], Vector3.zero, d * fullRange);
                SetLine(_radialCore[i], Vector3.zero, d * fullRange);
                _radialGlow[i].startColor = _radialGlow[i].endColor = glow;
                _radialCore[i].startColor = _radialCore[i].endColor = core;
                _radialGlow[i].widthMultiplier = glowW;
                _radialCore[i].widthMultiplier = coreW;
            }
        }

        static void SetArcPoints(LineRenderer lr, float r, float a0, float a1, int res)
        {
            lr.positionCount = res + 1;
            for (int i = 0; i <= res; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)res);
                lr.SetPosition(i, (Vector3)(Rotate(Vector2.up, a) * r));
            }
        }

        static void SetLine(LineRenderer lr, Vector3 a, Vector3 b)
        {
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
        }

        static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            float ca = Mathf.Cos(r), sa = Mathf.Sin(r);
            return new Vector2(v.x * ca - v.y * sa, v.x * sa + v.y * ca);
        }

        static Sprite MakePersonSprite()
        {
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, new Color(0, 0, 0, 0));

            void Fill(int x0, int y0, int w, int h, Color c)
            {
                for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    if (x >= 0 && y >= 0 && x < s && y < s) tex.SetPixel(x, y, c);
            }

            Fill(11, 2, 4, 8, new Color(0.12f, 0.18f, 0.2f));
            Fill(17, 2, 4, 8, new Color(0.12f, 0.18f, 0.2f));
            Fill(10, 9, 12, 12, new Color(0.2f, 0.55f, 0.55f));
            Fill(6, 11, 4, 7, new Color(0.2f, 0.55f, 0.55f));
            Fill(22, 11, 4, 7, new Color(0.2f, 0.55f, 0.55f));
            Fill(12, 21, 8, 8, new Color(0.9f, 0.74f, 0.58f));
            Fill(11, 26, 10, 5, new Color(0.85f, 0.9f, 0.95f));
            Fill(14, 14, 4, 5, new Color(0.35f, 0.75f, 0.9f));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.15f), s);
        }
    }
}
