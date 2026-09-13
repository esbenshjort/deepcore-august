using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Authoritative global DEV MODE for diagnostics UI.
    /// Always starts OFF. Manual toggle only (Ø). Never auto-enables on Play.
    /// When disabled, debug panels must not draw, format, or iterate for display.
    /// Does not change simulation behavior.
    /// </summary>
    public static class DevMode
    {
        /// <summary>Default off for a clean player-facing runtime.</summary>
        public static bool Enabled { get; private set; } = false;

        /// <summary>
        /// Domain-reload-safe reset — Play Mode must never inherit a prior ON state.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Enabled = false;
        }

        public static void SetEnabled(bool on)
        {
            if (Enabled == on) return;
            Enabled = on;
            DigHoodLog.Push(on ? "DEV MODE // ON" : "DEV MODE // OFF");
        }

        public static void Toggle() => SetEnabled(!Enabled);
    }
}
