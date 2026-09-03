using System;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Global job kinds. A worker's identity is not a job — assignment selects the job.
    /// </summary>
    public enum JobType : byte
    {
        Unassigned = 0,
        Excavation = 1,
        Prospecting = 2,
        Hauling = 3,
        Refining = 4,
        Engineering = 5,
    }
}
