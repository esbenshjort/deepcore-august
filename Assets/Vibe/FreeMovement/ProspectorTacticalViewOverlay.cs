using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Player-facing Tactical View: anomaly silhouettes from Prospector evidence.
    /// Neutral uncertain style — never Gold / Bedrock / Gas labels or material colors.
    /// Current mode = live DisplayScan. Historical mode = frozen SpatialSnapshots only.
    /// </summary>
    public sealed class ProspectorTacticalViewOverlay : MonoBehaviour
    {
        public const string ViewName = "TACTICAL VIEW";

        const int Ppu = 3;

        FineTerrainWorld _world;
        ProspectorScanHistory _history;
        Texture2D _tex;
        Color32[] _px;
        SpriteRenderer _sr;
        bool _visible;
        bool _dirty = true;
        readonly List<ProspectorAnomaly> _drawList = new(32);
        readonly List<AnomalySpatialSnapshot> _snapList = new(32);

        static readonly Color32 Clear = new(0, 0, 0, 0);
        // Single neutral evidence language — cold cyan-gray
        static readonly Color FillCore = new(0.22f, 0.42f, 0.52f, 1f);
        static readonly Color FillEdge = new(0.35f, 0.48f, 0.58f, 1f);
        static readonly Color Outline = new(0.7f, 0.82f, 0.9f, 1f);
        static readonly Color Uncertain = new(0.5f, 0.58f, 0.66f, 1f);
        // Historical — cooler / amber-tinted so old intel is visually distinct
        static readonly Color HistFill = new(0.18f, 0.32f, 0.42f, 1f);
        static readonly Color HistOutline = new(0.55f, 0.72f, 0.78f, 1f);
        static readonly Color HistUncertain = new(0.42f, 0.48f, 0.52f, 1f);
        static readonly Color HistRaw = new(0.25f, 0.38f, 0.48f, 1f);
        static readonly Color HistCone = new(0.35f, 0.55f, 0.62f, 1f);

        public bool Visible
        {
            get => _visible;
            set => SetVisible(value);
        }

        public bool IsHistorical => _history != null && _history.IsHistoricalMode;

        public static ProspectorTacticalViewOverlay Attach(
            Transform parent, FineTerrainWorld world, ProspectorScanHistory history)
        {
            var go = new GameObject("ProspectorTacticalView");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<ProspectorTacticalViewOverlay>();
            fx.Setup(world, history);
            return fx;
        }

        public void Setup(FineTerrainWorld world, ProspectorScanHistory history)
        {
            if (_history != null)
            {
                _history.EvidenceChanged -= OnChanged;
                _history.AnomaliesChanged -= OnChanged;
                _history.ViewModeChanged -= OnChanged;
            }
            _world = world;
            _history = history;
            if (_history != null)
            {
                _history.EvidenceChanged += OnChanged;
                _history.AnomaliesChanged += OnChanged;
                _history.ViewModeChanged += OnChanged;
            }

            int w = world.Width * Ppu;
            int h = world.Height * Ppu;
            float ppuWorld = Ppu / world.CellSize;

            _tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _px = new Color32[w * h];

            if (_sr == null)
            {
                _sr = gameObject.AddComponent<SpriteRenderer>();
                var sh = Shader.Find("Sprites/Default");
                if (sh != null) _sr.sharedMaterial = new Material(sh);
                _sr.sortingOrder = 53;
            }
            _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, w, h), Vector2.zero, ppuWorld);
            _sr.enabled = false;
            _visible = false;
            _dirty = true;
        }

        void OnDestroy()
        {
            if (_history == null) return;
            _history.EvidenceChanged -= OnChanged;
            _history.AnomaliesChanged -= OnChanged;
            _history.ViewModeChanged -= OnChanged;
        }

        void OnChanged() => _dirty = true;

        public void SetVisible(bool on)
        {
            _visible = on;
            if (_sr != null) _sr.enabled = on;
            if (on) _dirty = true;
            if (!on) _history?.ReturnToCurrentTactical();
        }

        public void Toggle() => SetVisible(!_visible);

        public void MarkDirty() => _dirty = true;

        public bool TryPickAnomaly(Vector2 worldLocalPos, out ProspectorAnomaly anomaly)
        {
            anomaly = null;
            if (_world == null || _history == null || _history.IsHistoricalMode) return false;
            var cell = _world.WorldToCell(worldLocalPos);
            return _history.TryGetAnomalyAt(cell.x, cell.y, out anomaly);
        }

        public bool TryPickHistorical(
            Vector2 worldLocalPos, out AnomalySpatialSnapshot snap)
        {
            snap = null;
            if (_world == null || _history == null || !_history.IsHistoricalMode) return false;
            var cell = _world.WorldToCell(worldLocalPos);
            return _history.TryGetHistoricalSpatialAt(cell.x, cell.y, out snap);
        }

        void LateUpdate()
        {
            if (_visible && _history != null && !_history.IsHistoricalMode)
            {
                // Keep pulsing while any unread finding or timed highlight is active
                if (HasUnreadAnomaly()
                    || (_history.Findings != null
                        && _history.Findings.IsHighlightActive(out _, out _)))
                    _dirty = true;
            }

            if (!_visible || !_dirty || _world == null) return;
            Rebuild();
            _dirty = false;
        }

        bool HasUnreadAnomaly()
        {
            var scan = _history?.DisplayScan;
            if (scan == null) return false;
            for (int i = 0; i < scan.Anomalies.Count; i++)
                if (scan.Anomalies[i].HasUnreadFinding) return true;
            return false;
        }

        void Rebuild()
        {
            for (int i = 0; i < _px.Length; i++)
                _px[i] = Clear;

            if (_history == null)
            {
                ApplyTex();
                return;
            }

            int tw = _world.Width * Ppu;
            bool historical = _history.IsHistoricalMode;
            var view = _history.ViewScan;

            if (historical && view != null)
            {
                DrawRawEvidenceFaint(tw, view);
                DrawScanConeHint(tw, view);
                _history.CopyViewSpatialSnapshotsSorted(_snapList);
                for (int i = 0; i < _snapList.Count; i++)
                    DrawSnapshotSilhouette(tw, _snapList[i]);
            }
            else
            {
                _history.CopyDisplayAnomaliesSorted(_drawList);
                for (int i = 0; i < _drawList.Count; i++)
                    DrawAnomalySilhouette(tw, _drawList[i]);

                // Unread NEW finding — amber pulse until noticed (live only)
                for (int i = 0; i < _drawList.Count; i++)
                {
                    if (!_drawList[i].HasUnreadFinding) continue;
                    DrawNewFindingMark(tw, _drawList[i]);
                }

                if (_history.Findings != null
                    && _history.Findings.IsHighlightActive(out int hs, out int ha))
                {
                    var scan = _history.DisplayScan;
                    if (scan != null && scan.ScanId == hs)
                    {
                        for (int i = 0; i < scan.Anomalies.Count; i++)
                        {
                            if (scan.Anomalies[i].AnomalyId != ha) continue;
                            DrawHighlight(tw, scan.Anomalies[i]);
                            break;
                        }
                    }
                }
            }

            ApplyTex();
        }

        void DrawRawEvidenceFaint(int texW, ProspectorScanRecord scan)
        {
            var obs = scan.Observations;
            for (int i = 0; i < obs.Count; i++)
                FillCell(texW, obs[i].CellX, obs[i].CellY, HistRaw, 0.08f);
        }

        void DrawScanConeHint(int texW, ProspectorScanRecord scan)
        {
            if (_world == null) return;
            Vector2 origin = scan.ScannerPosition;
            Vector2 facing = scan.ScannerFacing.sqrMagnitude > 0.0001f
                ? scan.ScannerFacing.normalized
                : Vector2.up;
            float cs = _world.CellSize;
            float range = Mathf.Max(1f, scan.PlannedRangeCells);
            float half = Mathf.Clamp(scan.PlannedHalfAngleDeg, 1f, 89f);
            int steps = Mathf.Clamp(Mathf.CeilToInt(range * 0.35f), 8, 28);

            // Origin tick
            var oc = _world.WorldToCell(origin);
            OutlineCell(texW, oc.x, oc.y, HistCone, 0.55f);

            // Arc samples at full range
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float ang = Mathf.Lerp(-half, half, t) * Mathf.Deg2Rad;
                float cos = Mathf.Cos(ang);
                float sin = Mathf.Sin(ang);
                Vector2 dir = new(
                    facing.x * cos - facing.y * sin,
                    facing.x * sin + facing.y * cos);
                Vector2 tip = origin + dir * (range * cs);
                var cell = _world.WorldToCell(tip);
                FillCell(texW, cell.x, cell.y, HistCone, 0.18f);
            }

            // Left / right rays
            for (int ray = 0; ray < 2; ray++)
            {
                float ang = (ray == 0 ? -half : half) * Mathf.Deg2Rad;
                float cos = Mathf.Cos(ang);
                float sin = Mathf.Sin(ang);
                Vector2 dir = new(
                    facing.x * cos - facing.y * sin,
                    facing.x * sin + facing.y * cos);
                int n = Mathf.Clamp(Mathf.CeilToInt(range * 0.5f), 6, 40);
                for (int s = 1; s <= n; s++)
                {
                    float u = s / (float)n;
                    Vector2 p = origin + dir * (u * range * cs);
                    var cell = _world.WorldToCell(p);
                    FillCell(texW, cell.x, cell.y, HistCone, 0.12f);
                }
            }
        }

        void DrawNewFindingMark(int texW, ProspectorAnomaly a)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            Color hi = new(1f, 0.72f, 0.28f, 1f); // amber — NEW
            for (int i = 0; i < a.SilhouetteTiles.Count; i++)
            {
                var t = a.SilhouetteTiles[i];
                OutlineCell(texW, t.x, t.y, hi, 0.72f * pulse);
            }
        }

        void DrawHighlight(int texW, ProspectorAnomaly a)
        {
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 8f);
            Color hi = new(0.85f, 0.95f, 1f, 1f);
            for (int i = 0; i < a.SilhouetteTiles.Count; i++)
            {
                var t = a.SilhouetteTiles[i];
                OutlineCell(texW, t.x, t.y, hi, 0.55f * pulse);
            }
        }

        void DrawSnapshotSilhouette(int texW, AnomalySpatialSnapshot a)
        {
            float str = Mathf.Clamp01(a.MaxSignalStrength);
            float q = Mathf.Clamp01(a.GeometryQuality);
            float fillA = Mathf.Lerp(0.22f, 0.40f, str) * Mathf.Lerp(0.75f, 1f, q);
            float outlineA = Mathf.Lerp(0.32f, 0.58f, q);
            float uncertainA = Mathf.Lerp(0.18f, 0.08f, q) * Mathf.Lerp(0.85f, 1f, str);

            var coreSet = new HashSet<long>(a.SilhouetteTiles.Count * 2);
            for (int i = 0; i < a.SilhouetteTiles.Count; i++)
            {
                var t = a.SilhouetteTiles[i];
                coreSet.Add(((long)t.x << 32) ^ (uint)t.y);
                FillCell(texW, t.x, t.y, HistFill, fillA);
            }

            for (int i = 0; i < a.UncertainEdgeTiles.Count; i++)
            {
                var t = a.UncertainEdgeTiles[i];
                long k = ((long)t.x << 32) ^ (uint)t.y;
                if (coreSet.Contains(k)) continue;
                FillCell(texW, t.x, t.y, HistUncertain, uncertainA);
            }

            for (int i = 0; i < a.SilhouetteTiles.Count; i++)
            {
                var t = a.SilhouetteTiles[i];
                if (IsBoundary(coreSet, t.x, t.y))
                    OutlineCell(texW, t.x, t.y, HistOutline, outlineA);
            }
        }

        void DrawAnomalySilhouette(int texW, ProspectorAnomaly a)
        {
            float str = Mathf.Clamp01(a.MaxSignalStrength);
            float q = Mathf.Clamp01(a.GeometryQuality);
            // Interior: relatively solid. Low geometry quality slightly softens fill.
            float fillA = Mathf.Lerp(0.28f, 0.48f, str) * Mathf.Lerp(0.82f, 1f, q);
            float outlineA = Mathf.Lerp(0.4f, 0.72f, q);
            if (a.AnalysisStatus == AnomalyAnalysisStatus.Assessed)
                outlineA = Mathf.Min(0.85f, outlineA + 0.12f);
            else if (a.AnalysisStatus == AnomalyAnalysisStatus.Analysing)
                outlineA = Mathf.Min(0.8f, outlineA + 0.08f);

            // Faded perimeter — more visible when precision is low
            float uncertainA = Mathf.Lerp(0.22f, 0.10f, q) * Mathf.Lerp(0.85f, 1f, str);

            var coreSet = new HashSet<long>(a.SilhouetteTiles.Count * 2);
            for (int i = 0; i < a.SilhouetteTiles.Count; i++)
            {
                var t = a.SilhouetteTiles[i];
                coreSet.Add(((long)t.x << 32) ^ (uint)t.y);
                FillCell(texW, t.x, t.y, FillCore, fillA);
            }

            for (int i = 0; i < a.UncertainEdgeTiles.Count; i++)
            {
                var t = a.UncertainEdgeTiles[i];
                long k = ((long)t.x << 32) ^ (uint)t.y;
                if (coreSet.Contains(k)) continue;
                FillCell(texW, t.x, t.y, Uncertain, uncertainA);
            }

            for (int i = 0; i < a.SilhouetteTiles.Count; i++)
            {
                var t = a.SilhouetteTiles[i];
                if (IsBoundary(coreSet, t.x, t.y))
                    OutlineCell(texW, t.x, t.y, Outline, outlineA);
            }
        }

        static bool IsBoundary(HashSet<long> core, int x, int y) =>
            !core.Contains(((long)(x + 1) << 32) ^ (uint)y)
            || !core.Contains(((long)(x - 1) << 32) ^ (uint)y)
            || !core.Contains(((long)x << 32) ^ (uint)(y + 1))
            || !core.Contains(((long)x << 32) ^ (uint)(y - 1));

        void FillCell(int texW, int cx, int cy, Color col, float alpha)
        {
            if (_world == null || !_world.InBounds(cx, cy)) return;
            int px0 = cx * Ppu;
            int py0 = cy * Ppu;
            for (int oy = 0; oy < Ppu; oy++)
            for (int ox = 0; ox < Ppu; ox++)
                Plot(px0 + ox, py0 + oy, texW, col, alpha);
        }

        void OutlineCell(int texW, int cx, int cy, Color col, float alpha)
        {
            if (_world == null || !_world.InBounds(cx, cy)) return;
            int px0 = cx * Ppu;
            int py0 = cy * Ppu;
            for (int i = 0; i < Ppu; i++)
            {
                Plot(px0 + i, py0, texW, col, alpha);
                Plot(px0 + i, py0 + Ppu - 1, texW, col, alpha);
                Plot(px0, py0 + i, texW, col, alpha);
                Plot(px0 + Ppu - 1, py0 + i, texW, col, alpha);
            }
        }

        void Plot(int x, int y, int texW, Color col, float alpha)
        {
            if (x < 0 || y < 0) return;
            int i = y * texW + x;
            if (i < 0 || i >= _px.Length) return;
            byte aa = (byte)Mathf.Clamp(alpha * 255f, 0, 200);
            // Additive-ish keep max alpha for overlapping silhouettes
            if (_px[i].a >= aa) return;
            _px[i] = new Color32(
                (byte)(col.r * 255f),
                (byte)(col.g * 255f),
                (byte)(col.b * 255f),
                aa);
        }

        void ApplyTex()
        {
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }
    }
}
