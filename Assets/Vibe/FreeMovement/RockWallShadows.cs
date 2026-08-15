using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Builds ShadowCaster2D blocks for solid rock near open tunnels so Light2D
    /// casts real cliff shadows into excavated space.
    /// </summary>
    public sealed class RockWallShadows : MonoBehaviour
    {
        const int Depth = 5; // solid cells near open that cast (covers soft wall band)

        FineTerrainWorld _world;
        Transform _root;
        readonly Dictionary<long, GameObject> _casters = new();
        bool _pending;
        int _qx0, _qy0, _qx1, _qy1;
        float _cooldown;

        static FieldInfo _shapePathField;
        static FieldInfo _shapePathHashField;
        static FieldInfo _castingSourceField;
        static FieldInfo _forceRebuildField;
        static bool _reflected;

        public void Setup(FineTerrainWorld world)
        {
            _world = world;
            _root = new GameObject("RockWallShadows").transform;
            _root.SetParent(transform, false);
            _world.RegionChanged += OnRegion;
            CacheReflection();
            RebuildAll();
        }

        void OnDestroy()
        {
            if (_world != null) _world.RegionChanged -= OnRegion;
        }

        void OnRegion(int x0, int y0, int x1, int y1)
        {
            x0 = Mathf.Max(0, x0 - Depth - 1);
            y0 = Mathf.Max(0, y0 - Depth - 1);
            x1 = Mathf.Min(_world.Width - 1, x1 + Depth + 1);
            y1 = Mathf.Min(_world.Height - 1, y1 + Depth + 1);
            if (!_pending)
            {
                _qx0 = x0; _qy0 = y0; _qx1 = x1; _qy1 = y1;
                _pending = true;
            }
            else
            {
                _qx0 = Mathf.Min(_qx0, x0);
                _qy0 = Mathf.Min(_qy0, y0);
                _qx1 = Mathf.Max(_qx1, x1);
                _qy1 = Mathf.Max(_qy1, y1);
            }
        }

        void LateUpdate()
        {
            if (!_pending) return;
            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f) return;
            _cooldown = 0.05f;
            _pending = false;
            RebuildRect(_qx0, _qy0, _qx1, _qy1);
        }

        public void RebuildAll() => RebuildRect(0, 0, _world.Width - 1, _world.Height - 1);

        void RebuildRect(int x0, int y0, int x1, int y1)
        {
            float cs = _world.CellSize;
            var keep = new HashSet<long>();

            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (!_world.IsLightOccluder(x, y))
                {
                    Remove(Key(x, y));
                    continue;
                }
                if (!NearOpen(x, y, Depth))
                {
                    Remove(Key(x, y));
                    continue;
                }

                long key = Key(x, y);
                keep.Add(key);
                if (!_casters.ContainsKey(key))
                    _casters[key] = CreateCaster(x, y, cs);
            }

            // Cleanup casters in rect that are no longer needed
            var toRemove = new List<long>();
            foreach (var kv in _casters)
            {
                Decode(kv.Key, out int cx, out int cy);
                if (cx < x0 || cy < y0 || cx > x1 || cy > y1) continue;
                if (!keep.Contains(kv.Key)) toRemove.Add(kv.Key);
            }
            foreach (var k in toRemove) Remove(k);
        }

        bool NearOpen(int x, int y, int depth)
        {
            for (int oy = -depth; oy <= depth; oy++)
            for (int ox = -depth; ox <= depth; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                int nx = x + ox, ny = y + oy;
                if (!_world.InBounds(nx, ny)) continue;
                if (_world.IsFloorOpen(nx, ny)) return true;
            }
            return false;
        }

        GameObject CreateCaster(int x, int y, float cs)
        {
            var go = new GameObject($"SC_{x}_{y}");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3((x + 0.5f) * cs, (y + 0.5f) * cs, 0f);

            var sc = go.AddComponent<ShadowCaster2D>();
            sc.castsShadows = true;
            // Cast only — don't self-shadow (avoids dark FoW-looking ring on dig face)
            sc.selfShadows = false;
            sc.castingOption = ShadowCaster2D.ShadowCastingOptions.CastShadow;

            // Overlap so light can't leak between neighboring rock cells
            float h = cs * 0.58f;
            var path = new Vector3[]
            {
                new(-h, -h, 0f),
                new(-h,  h, 0f),
                new( h,  h, 0f),
                new( h, -h, 0f),
            };
            ApplyShape(sc, path);
            return go;
        }

        void Remove(long key)
        {
            if (!_casters.TryGetValue(key, out var go)) return;
            _casters.Remove(key);
            if (go != null) Destroy(go);
        }

        static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
        static void Decode(long key, out int x, out int y)
        {
            x = (int)(key >> 32);
            y = (int)(key & 0xffffffff);
        }

        static void CacheReflection()
        {
            if (_reflected) return;
            var t = typeof(ShadowCaster2D);
            _shapePathField = t.GetField("m_ShapePath", BindingFlags.Instance | BindingFlags.NonPublic);
            _shapePathHashField = t.GetField("m_ShapePathHash", BindingFlags.Instance | BindingFlags.NonPublic);
            _castingSourceField = t.GetField("m_ShadowCastingSource", BindingFlags.Instance | BindingFlags.NonPublic);
            _forceRebuildField = t.GetField("m_ForceShadowMeshRebuild", BindingFlags.Instance | BindingFlags.NonPublic);
            _reflected = true;
        }

        static void ApplyShape(ShadowCaster2D sc, Vector3[] path)
        {
            CacheReflection();
            if (_castingSourceField != null)
                _castingSourceField.SetValue(sc, 1); // ShapeEditor
            if (_shapePathField != null)
                _shapePathField.SetValue(sc, path);
            if (_shapePathHashField != null)
                _shapePathHashField.SetValue(sc, Random.Range(int.MinValue, int.MaxValue));
            if (_forceRebuildField != null)
                _forceRebuildField.SetValue(sc, true);
        }
    }
}
