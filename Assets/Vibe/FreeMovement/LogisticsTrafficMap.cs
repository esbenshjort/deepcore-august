using System;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Hidden Hauler traffic evidence. Actual traversal history — not geometric direction to camp.
    /// Loaded moves weigh more for infrastructure value.
    /// </summary>
    public sealed class LogisticsTrafficMap
    {
        /// <summary>Fired when a cell is visited — terrain presentation can dirty wear.</summary>
        public event Action<int, int> CellVisited;
        /// <summary>Score added per unloaded cell visit.</summary>
        public float VisitGainUnloaded = 1f;
        /// <summary>Score added per loaded cell visit.</summary>
        public float VisitGainLoaded = 2.6f;
        /// <summary>How much loaded score counts vs unloaded when ranking cells.</summary>
        public float LoadedValueWeight = 2.2f;
        public float UnloadedValueWeight = 1f;
        /// <summary>Exponential decay applied per game-hour to both score channels.</summary>
        public float DecayPerGameHour = 0.045f;
        /// <summary>Hours after last use at which freshness ≈ 0.5.</summary>
        public float RecencyHalfLifeHours = 6f;
        /// <summary>Ignore cells with weighted value below this for corridor candidates.</summary>
        public float MinWeightedValue = 2.5f;

        readonly FineTerrainWorld _world;
        readonly float[] _loaded;
        readonly float[] _unloaded;
        readonly float[] _lastUsedHours;
        readonly int _w;
        readonly int _h;

        float _gameHours;

        public int Width => _w;
        public int Height => _h;
        public float GameHours => _gameHours;

        public LogisticsTrafficMap(FineTerrainWorld world)
        {
            _world = world;
            _w = world.Width;
            _h = world.Height;
            int n = _w * _h;
            _loaded = new float[n];
            _unloaded = new float[n];
            _lastUsedHours = new float[n];
            for (int i = 0; i < n; i++)
                _lastUsedHours[i] = -9999f;
        }

        public void SetGameHours(float absoluteGameHours) => _gameHours = absoluteGameHours;

        public void TickDecay(float hoursDelta)
        {
            if (hoursDelta <= 0f || DecayPerGameHour <= 0f) return;
            float keep = Mathf.Exp(-DecayPerGameHour * hoursDelta);
            int n = _loaded.Length;
            for (int i = 0; i < n; i++)
            {
                if (_loaded[i] <= 0.0001f && _unloaded[i] <= 0.0001f) continue;
                _loaded[i] *= keep;
                _unloaded[i] *= keep;
                if (_loaded[i] < 0.02f) _loaded[i] = 0f;
                if (_unloaded[i] < 0.02f) _unloaded[i] = 0f;
            }
        }

        public void RecordVisit(int x, int y, bool loaded)
        {
            if (_world == null || !InBounds(x, y)) return;
            if (!_world.IsTunnelOpen(x, y)) return;
            int i = Idx(x, y);
            if (loaded) _loaded[i] += VisitGainLoaded;
            else _unloaded[i] += VisitGainUnloaded;
            _lastUsedHours[i] = _gameHours;
            CellVisited?.Invoke(x, y);
        }

        public void RecordVisitWorld(Vector2 worldPos, bool loaded)
        {
            if (_world == null) return;
            var c = _world.WorldToCell(worldPos);
            RecordVisit(c.x, c.y, loaded);
        }

        public float LoadedScore(int x, int y) => InBounds(x, y) ? _loaded[Idx(x, y)] : 0f;
        public float UnloadedScore(int x, int y) => InBounds(x, y) ? _unloaded[Idx(x, y)] : 0f;
        public float TotalScore(int x, int y) => LoadedScore(x, y) + UnloadedScore(x, y);
        public float LastUsedHours(int x, int y) => InBounds(x, y) ? _lastUsedHours[Idx(x, y)] : -9999f;

        /// <summary>Infrastructure ranking base from actual Hauler history (loaded-weighted).</summary>
        public float WeightedTrafficValue(int x, int y)
        {
            if (!InBounds(x, y)) return 0f;
            int i = Idx(x, y);
            return _loaded[i] * LoadedValueWeight + _unloaded[i] * UnloadedValueWeight;
        }

        public float Freshness01(int x, int y)
        {
            if (!InBounds(x, y)) return 0f;
            float age = Mathf.Max(0f, _gameHours - _lastUsedHours[Idx(x, y)]);
            float half = Mathf.Max(0.25f, RecencyHalfLifeHours);
            return Mathf.Exp(-age * 0.693147f / half);
        }

        public float Intensity01(int x, int y, float refMax = 40f)
        {
            float v = WeightedTrafficValue(x, y);
            return Mathf.Clamp01(v / Mathf.Max(1f, refMax));
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < _w && y < _h;

        int Idx(int x, int y) => y * _w + x;
    }
}
