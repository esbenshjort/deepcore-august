using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Sparse ambient grit near fresh dig faces — reuses ExcavationDustFx.
    /// No per-decoration Update storms; one timer, occasional burst.
    /// </summary>
    public sealed class TunnelAmbientFx : MonoBehaviour
    {
        FineTerrainWorld _world;
        Transform _fxRoot;
        float _timer;
        int _cursor;

        public static TunnelAmbientFx Attach(Transform parent, FineTerrainWorld world, Transform fxRoot)
        {
            var go = new GameObject("TunnelAmbientFx");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<TunnelAmbientFx>();
            fx._world = world;
            fx._fxRoot = fxRoot != null ? fxRoot : parent;
            fx._timer = 1.8f;
            return fx;
        }

        void Update()
        {
            if (_world == null) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = Random.Range(2.4f, 5.2f);

            // Scan a small rotating band for a fresh dig-face cell
            int w = _world.Width;
            int h = _world.Height;
            int attempts = 48;
            for (int a = 0; a < attempts; a++)
            {
                _cursor = (_cursor + 7919) % (w * h);
                int x = _cursor % w;
                int y = _cursor / w;
                if (!_world.IsExcavated(x, y)) continue;
                if (_world.ExcavationAge01(x, y) > 0.35f) continue; // only fairly fresh
                if (!TouchesSolid(x, y)) continue;

                Vector2 pos = _world.CellCenter(x, y);
                ExcavationDustFx.SpawnLingering(_fxRoot, pos, _world.CellSize * Random.Range(0.55f, 0.9f),
                    heavy: false);
                return;
            }
        }

        bool TouchesSolid(int x, int y)
        {
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                int nx = x + ox, ny = y + oy;
                if (!_world.InBounds(nx, ny)) continue;
                if (!_world.IsExcavated(nx, ny)) return true;
            }
            return false;
        }
    }
}
