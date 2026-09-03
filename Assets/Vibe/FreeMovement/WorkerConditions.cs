using System;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Obsolete name for <see cref="WorkerState"/>.
    /// Kept so any lingering SerializeField / type refs compile during V1.2A migration.
    /// Prefer <see cref="WorkerState"/> for all new code.
    /// </summary>
    [Obsolete("V1.2A: use WorkerState")]
    [Serializable]
    public sealed class WorkerConditions : WorkerState
    {
    }
}
