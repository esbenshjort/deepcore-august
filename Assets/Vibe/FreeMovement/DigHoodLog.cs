using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Tiny ring buffer for under-the-hood dig / combat lines (HUD).
    /// Console mirroring is off by default — only when DEV MODE is on, or
    /// <see cref="MirrorToConsole"/> is set explicitly (DEV opt-in).
    /// </summary>
    public static class DigHoodLog
    {
        public const int Capacity = 8;
        static readonly Queue<string> _lines = new(Capacity);
        /// <summary>Explicit DEV opt-in for console mirroring (default off).</summary>
        static bool _mirrorToConsole;

        /// <summary>
        /// When true, also mirrors to the Unity Console even if DEV MODE is off.
        /// Default false. Prefer Ø DEV MODE for normal dig diagnostics.
        /// </summary>
        public static bool MirrorToConsole
        {
            get => _mirrorToConsole;
            set => _mirrorToConsole = value;
        }

        public static IReadOnlyCollection<string> Lines => _lines;

        public static void Push(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            while (_lines.Count >= Capacity)
                _lines.Dequeue();
            _lines.Enqueue(line);
            // Ring buffer always; Console only under DEV MODE or explicit flag.
            if (DevMode.Enabled || _mirrorToConsole)
                Debug.Log($"[DIG] {line}");
        }

        public static void Clear() => _lines.Clear();
    }
}
