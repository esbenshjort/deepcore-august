namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Minimal work provider. Heavy Scanner (Prospecting) and Excavator (Excavation) implement this.
    /// Assignment ownership is coordinated by <see cref="WorkerAssignmentManager"/>.
    /// </summary>
    public interface IWorkProvider
    {
        string ProviderId { get; }
        JobType JobType { get; }
        bool IsAvailable { get; }
        /// <summary>−1 if none. Mirror of manager ownership — not a second authority.</summary>
        int AssignedWorkerId { get; }
        bool CanAssign(WorkerRuntime worker, out string reason);
        /// <summary>
        /// Mirror-only: record which worker the manager assigned. Does not mutate the manager.
        /// </summary>
        void NotifyAssigned(WorkerRuntime worker);
        void NotifyUnassigned();
    }
}
