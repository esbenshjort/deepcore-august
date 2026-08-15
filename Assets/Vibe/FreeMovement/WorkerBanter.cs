using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Sparse, cooldown-gated worker one-liners for the roster HUD.</summary>
    public sealed class WorkerBanter
    {
        public enum Voice : byte { Prospector, Excavator, Hauler, Refiner }

        struct Line
        {
            public string Text;
            public float Until;
        }

        readonly Line[] _active = new Line[4];
        readonly float[] _cooldownUntil = new float[4];
        readonly System.Random _rng = new(9081);

        const float ShowSeconds = 3.6f;
        const float MinGap = 9f;

        public string Get(Voice v)
        {
            int i = (int)v;
            if (i < 0 || i >= _active.Length) return null;
            if (string.IsNullOrEmpty(_active[i].Text)) return null;
            if (Time.unscaledTime > _active[i].Until)
            {
                _active[i] = default;
                return null;
            }
            return _active[i].Text;
        }

        public bool TrySay(Voice v, params string[] options)
        {
            if (options == null || options.Length == 0) return false;
            int i = (int)v;
            if (i < 0 || i >= _active.Length) return false;
            float t = Time.unscaledTime;
            if (t < _cooldownUntil[i]) return false;
            if (!string.IsNullOrEmpty(_active[i].Text) && t < _active[i].Until) return false;
            string line = options[_rng.Next(0, options.Length)];
            _active[i] = new Line { Text = line, Until = t + ShowSeconds };
            _cooldownUntil[i] = t + MinGap;
            return true;
        }

        public void Clear()
        {
            for (int i = 0; i < _active.Length; i++)
            {
                _active[i] = default;
                _cooldownUntil[i] = 0f;
            }
        }
    }
}
