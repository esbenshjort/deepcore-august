using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Tiny ring buffer for under-the-hood dig / combat lines (HUD + Console).
    /// </summary>
    public static class DigHoodLog
    {
        public const int Capacity = 8;
        static readonly Queue<string> _lines = new(Capacity);
        static bool _mirrorToConsole = true;

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
            if (_mirrorToConsole)
                Debug.Log($"[DIG] {line}");
        }

        public static void Clear() => _lines.Clear();
    }
}
