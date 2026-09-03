using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// F1: resolved gameplay control target for the canonical selected person.
    /// Not a command system — just the answer to "who/what receives job input?"
    /// </summary>
    public readonly struct WorkerControlTarget
    {
        public readonly int WorkerId;
        public readonly WorkerRuntime Worker;
        public readonly JobType JobType;
        public readonly string ProviderId;
        public readonly bool IsAssigned;
        public readonly WorkerAvatar Avatar;
        public readonly string HostLabel;

        public bool HasPerson => WorkerId > 0 && Worker != null;
        public bool IsIdlePerson => HasPerson && !IsAssigned;

        public static WorkerControlTarget None => default;

        public WorkerControlTarget(
            WorkerRuntime worker,
            JobType job,
            string providerId,
            WorkerAvatar avatar,
            string hostLabel)
        {
            Worker = worker;
            WorkerId = worker != null ? worker.WorkerId : 0;
            JobType = job;
            ProviderId = providerId ?? "";
            IsAssigned = worker != null && job != JobType.Unassigned;
            Avatar = avatar;
            HostLabel = hostLabel ?? "";
        }
    }
}
