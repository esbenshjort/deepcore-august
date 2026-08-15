using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Purple gas venting from breached pockets — only at <see cref="FineTerrainWorld.IsGasSeen"/>
    /// cells (breach mouth / entered void). Never fills the dark pocket ahead of the dig.
    /// Vents for ~2 minutes after a pocket is opened, then clears.
    /// </summary>
    public sealed class GasPocketFx : MonoBehaviour
    {
        const int SortOrder = 44;
        const float VentDuration = 120f;

        FineTerrainWorld _world;
        Sprite _smokeSprite;
        Transform _root;
        readonly List<Vector2Int> _venting = new(96);
        readonly Dictionary<int, float> _pocketOpenedAt = new();
        readonly HashSet<int> _collectedPockets = new();
        float _emitTimer;
        bool _dirtyScan = true;

        public event System.Action PocketBreached;

        public static GasPocketFx Attach(Transform parent, FineTerrainWorld world)
        {
            var go = new GameObject("GasPocketFx");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<GasPocketFx>();
            fx._world = world;
            fx._root = go.transform;
            fx._smokeSprite = MakeSmokeSprite();
            if (world != null)
                world.Changed += () => fx._dirtyScan = true;
            return fx;
        }

        public void Rescan()
        {
            _dirtyScan = true;
            _pocketOpenedAt.Clear();
            _venting.Clear();
        }

        void Update()
        {
            if (_world == null) return;
            if (_dirtyScan)
            {
                RebuildVentList();
                _dirtyScan = false;
            }

            if (_pocketOpenedAt.Count > 0)
            {
                bool anyExpired = false;
                var keys = ListPoolKeys();
                for (int i = 0; i < keys.Count; i++)
                {
                    int id = keys[i];
                    if (Time.time - _pocketOpenedAt[id] < VentDuration) continue;
                    _pocketOpenedAt.Remove(id);
                    anyExpired = true;
                }
                if (anyExpired)
                {
                    RebuildVentList();
                    return;
                }
            }

            if (_venting.Count == 0) return;

            _emitTimer -= Time.deltaTime;
            if (_emitTimer > 0f) return;
            _emitTimer = Random.Range(0.048f, 0.1f);

            // Prefer mouth cells (touch dig tunnel) so smoke reads at the breakthrough
            int bursts = Mathf.Clamp(1 + _venting.Count / 8, 1, 4);
            for (int b = 0; b < bursts; b++)
            {
                var cell = _venting[Random.Range(0, _venting.Count)];
                SpawnPuff(_world.CellCenter(cell.x, cell.y));
            }
        }

        List<int> ListPoolKeys()
        {
            var list = new List<int>(_pocketOpenedAt.Count);
            foreach (var kv in _pocketOpenedAt)
                list.Add(kv.Key);
            return list;
        }

        void RebuildVentList()
        {
            _venting.Clear();
            _collectedPockets.Clear();
            bool fresh = false;

            for (int y = 1; y < _world.Height - 1; y++)
            for (int x = 1; x < _world.Width - 1; x++)
            {
                if (!_world.IsGasRevealed(x, y)) continue;

                int pocketId = PocketId(x, y);
                if (!_collectedPockets.Add(pocketId)) continue;

                if (!_pocketOpenedAt.ContainsKey(pocketId))
                {
                    _pocketOpenedAt[pocketId] = Time.time;
                    fresh = true;
                }

                if (Time.time - _pocketOpenedAt[pocketId] >= VentDuration)
                    continue;

                CollectSeenVentCells(x, y);
            }

            if (fresh)
                PocketBreached?.Invoke();
        }

        void CollectSeenVentCells(int sx, int sy)
        {
            var q = new Queue<Vector2Int>();
            var seen = new HashSet<int>();
            q.Enqueue(new Vector2Int(sx, sy));
            seen.Add(sy * _world.Width + sx);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                // Only vent where the void is actually visible (mouth / entered)
                if (_world.IsGasSeen(c.x, c.y))
                    _venting.Add(c);

                for (int i = 0; i < 4; i++)
                {
                    int nx = c.x + (i == 0 ? 1 : i == 1 ? -1 : 0);
                    int ny = c.y + (i == 2 ? 1 : i == 3 ? -1 : 0);
                    int ni = ny * _world.Width + nx;
                    if (!seen.Add(ni)) continue;
                    if (!_world.IsGas(nx, ny)) continue;
                    q.Enqueue(new Vector2Int(nx, ny));
                }
            }
        }

        int PocketId(int sx, int sy)
        {
            int min = sy * _world.Width + sx;
            var q = new Queue<Vector2Int>();
            var seen = new HashSet<int>();
            q.Enqueue(new Vector2Int(sx, sy));
            seen.Add(min);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                int idx = c.y * _world.Width + c.x;
                if (idx < min) min = idx;
                for (int i = 0; i < 4; i++)
                {
                    int nx = c.x + (i == 0 ? 1 : i == 1 ? -1 : 0);
                    int ny = c.y + (i == 2 ? 1 : i == 3 ? -1 : 0);
                    int ni = ny * _world.Width + nx;
                    if (!seen.Add(ni)) continue;
                    if (!_world.IsGas(nx, ny)) continue;
                    q.Enqueue(new Vector2Int(nx, ny));
                }
            }
            return min;
        }

        void SpawnPuff(Vector2 center)
        {
            Vector2 pos = center + Random.insideUnitCircle * (_world.CellSize * 0.45f);
            var go = new GameObject("GasPuff");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * Random.Range(0.22f, 0.4f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _smokeSprite;
            sr.sortingOrder = SortOrder;
            DigVisualKit.ApplyLit(sr);

            float v = Random.Range(0.55f, 0.9f);
            sr.color = new Color(0.62f * v, 0.22f * v, 0.95f * v, Random.Range(0.42f, 0.65f));

            Vector2 drift = Random.insideUnitCircle.normalized * Random.Range(0.06f, 0.26f)
                            + Vector2.up * Random.Range(0.04f, 0.16f);

            go.AddComponent<DrillSmokePuff>().Init(
                _world,
                drift,
                life: Random.Range(0.75f, 1.4f),
                grow: Random.Range(1.6f, 2.5f));
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
                float n = Mathf.PerlinNoise(x * 0.3f + 5f, y * 0.3f);
                d -= (n - 0.5f) * 0.4f;
                if (d > 1f) { tex.SetPixel(x, y, Color.clear); continue; }
                float a = Mathf.Clamp01((1f - d) * 0.95f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }
    }
}
