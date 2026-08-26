using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Optional sim-delta override for headless / fast-forward benchmarks.
    /// When unset, workers use <see cref="Time.deltaTime"/> (normal play).
    /// </summary>
    public static class WorkerSimClock
    {
        static float? _override;

        public static float Delta => _override ?? Time.deltaTime;

        public static bool HasOverride => _override.HasValue;

        public static void SetOverride(float deltaSeconds)
        {
            _override = Mathf.Max(0f, deltaSeconds);
        }

        public static void ClearOverride()
        {
            _override = null;
        }
    }
}
