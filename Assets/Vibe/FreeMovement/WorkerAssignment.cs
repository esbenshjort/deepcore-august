using System;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Lightweight runtime assignment: person → job → temporary provider identity.
    /// Stage B: provider is a string key for the existing role body (not IWorkProvider yet).
    /// </summary>
    [Serializable]
    public sealed class WorkerAssignment
    {
        public int WorkerId;
        public JobType JobType;
        /// <summary>
        /// Temporary provider identity. Stage B examples: "body.prospector", "body.excavator".
        /// Stage C will replace with real IWorkProvider ids.
        /// </summary>
        public string ProviderId = "";
        public float AssignedAtGameHours;

        public WorkerAssignment() { }

        public WorkerAssignment(int workerId, JobType jobType, string providerId, float assignedAtGameHours)
        {
            WorkerId = workerId;
            JobType = jobType;
            ProviderId = providerId ?? "";
            AssignedAtGameHours = assignedAtGameHours;
        }

        public string ProviderDisplayLabel =>
            !string.IsNullOrEmpty(ProviderId) && ProviderId.StartsWith("scanner.")
                ? $"Heavy Scanner ({ProviderId})"
                : !string.IsNullOrEmpty(ProviderId) && ProviderId.StartsWith("excavator.")
                    ? $"Excavator Machine ({ProviderId})"
                : !string.IsNullOrEmpty(ProviderId) && ProviderId.StartsWith("hauler.")
                    ? $"Hauler Cart ({ProviderId})"
                : !string.IsNullOrEmpty(ProviderId) && ProviderId.StartsWith("washer.")
                    ? $"Wash Station ({ProviderId})"
                : !string.IsNullOrEmpty(ProviderId) && ProviderId.StartsWith("engineer.")
                    ? $"Engineer Kit ({ProviderId})"
                : ProviderId switch
                {
                    "body.prospector" => "Prospector Body / Heavy Scanner (placeholder)",
                    "body.excavator" => "Excavator Body / Machine (placeholder)",
                    "body.hauler" => "Hauler Body / Cart (placeholder)",
                    "body.refiner" => "Refiner Body / Wash Station (placeholder)",
                    "body.engineer" => "Engineer Body / Kit (placeholder)",
                    "" => "—",
                    _ => ProviderId,
                };
    }
}
