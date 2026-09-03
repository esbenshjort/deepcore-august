namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Global hook so job hosts can emit without MonoBehaviour references to the runner.
    /// Runner binds <see cref="Service"/> on Build.
    /// </summary>
    public static class WorkerStateEventHub
    {
        public static WorkerStateEventService Service { get; set; }

        public static WorkerStateEventRecord Emit(WorkerStateEvent e) =>
            Service != null ? Service.Emit(e) : null;

        public static WorkerStateEventRecord EmitForced(WorkerStateEvent e) =>
            Service != null ? Service.EmitForced(e) : null;
    }
}
