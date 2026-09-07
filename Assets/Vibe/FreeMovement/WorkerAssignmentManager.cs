using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Authoritative worker ↔ job assignment store.
    /// Stage B: bootstrap + query APIs. Live gameplay reassignment not exposed yet.
    /// </summary>
    public sealed class WorkerAssignmentManager
    {
        readonly Dictionary<int, WorkerAssignment> _byWorker = new(8);
        readonly Dictionary<JobType, int> _workerByJob = new(8);

        public IReadOnlyDictionary<int, WorkerAssignment> AllByWorker => _byWorker;

        public void Clear()
        {
            _byWorker.Clear();
            _workerByJob.Clear();
        }

        public bool TryGetAssignment(int workerId, out WorkerAssignment assignment) =>
            _byWorker.TryGetValue(workerId, out assignment);

        public WorkerAssignment GetAssignment(int workerId) =>
            _byWorker.TryGetValue(workerId, out var a) ? a : null;

        public WorkerAssignment GetAssignment(WorkerRuntime worker) =>
            worker != null ? GetAssignment(worker.WorkerId) : null;

        /// <summary>WorkerId for a job, or −1 if none.</summary>
        public int GetWorkerIdForJob(JobType job)
        {
            if (job == JobType.Unassigned) return -1;
            return _workerByJob.TryGetValue(job, out int id) ? id : -1;
        }

        public bool TryGetWorkerIdForJob(JobType job, out int workerId)
        {
            workerId = GetWorkerIdForJob(job);
            return workerId > 0;
        }

        /// <summary>
        /// Internal bootstrap / future API. Stage B gameplay must not call this for live swaps.
        /// One worker → one primary job; one occupied job → one worker.
        /// </summary>
        public bool AssignInternal(
            int workerId,
            JobType job,
            string providerId,
            float absoluteGameHours,
            bool replaceExisting = true)
        {
            if (workerId <= 0) return false;
            if (job == JobType.Unassigned)
                return Unassign(workerId);

            if (_byWorker.TryGetValue(workerId, out var existing)
                && existing.JobType == job
                && existing.ProviderId == providerId)
            {
                existing.AssignedAtGameHours = absoluteGameHours;
                return true;
            }

            if (!replaceExisting && _byWorker.ContainsKey(workerId))
                return false;

            // Release previous job held by this worker
            if (_byWorker.TryGetValue(workerId, out var prev)
                && prev.JobType != JobType.Unassigned
                && _workerByJob.TryGetValue(prev.JobType, out int mapped)
                && mapped == workerId)
            {
                _workerByJob.Remove(prev.JobType);
            }

            // Evict other worker currently on this job (single-operator V1)
            if (_workerByJob.TryGetValue(job, out int otherId) && otherId != workerId)
            {
                if (_byWorker.TryGetValue(otherId, out var otherAsg))
                {
                    otherAsg.JobType = JobType.Unassigned;
                    otherAsg.ProviderId = "";
                }
                _workerByJob.Remove(job);
            }

            var asg = new WorkerAssignment(
                workerId,
                job,
                string.IsNullOrEmpty(providerId) ? JobStatPreview.BehaviourKey(job) : providerId,
                absoluteGameHours);
            _byWorker[workerId] = asg;
            _workerByJob[job] = workerId;
            return true;
        }

        public bool Unassign(int workerId)
        {
            if (!_byWorker.TryGetValue(workerId, out var asg)) return false;
            if (asg.JobType != JobType.Unassigned
                && _workerByJob.TryGetValue(asg.JobType, out int mapped)
                && mapped == workerId)
            {
                _workerByJob.Remove(asg.JobType);
            }
            asg.JobType = JobType.Unassigned;
            asg.ProviderId = "";
            return true;
        }

        /// <summary>API surface for Stage C+.</summary>
        public bool Reassign(
            int workerId,
            JobType newJob,
            string providerId,
            float absoluteGameHours) =>
            AssignInternal(workerId, newJob, providerId, absoluteGameHours, replaceExisting: true);

        /// <summary>True if Prospecting (or any job) has more than one holder.</summary>
        public bool HasDuplicateJobViolation(IReadOnlyList<WorkerRuntime> crew)
        {
            _ = crew;
            foreach (var job in JobStatPreview.OccupiedJobs)
            {
                if (CountWorkersOnJob(job) > 1) return true;
            }
            return false;
        }

        public int CountWorkersOnJob(JobType job)
        {
            int n = 0;
            foreach (var kv in _byWorker)
                if (kv.Value.JobType == job) n++;
            return n;
        }

        /// <summary>
        /// Stage B fixed defaults matching Stage A body binds.
        /// </summary>
        public void BootstrapPrototypeDefaults(
            IReadOnlyList<WorkerRuntime> crew,
            float absoluteGameHours)
        {
            Clear();
            if (crew == null) return;

            // Expected order: Lewis, Mara, Kowalski, Elena, Viktor, [optional Steward]
            JobType[] jobs =
            {
                JobType.Prospecting,
                JobType.Excavation,
                JobType.Hauling,
                JobType.Refining,
                JobType.Engineering,
                JobType.Steward,
            };

            for (int i = 0; i < crew.Count && i < jobs.Length; i++)
            {
                var w = crew[i];
                if (w == null) continue;
                AssignInternal(
                    w.WorkerId,
                    jobs[i],
                    JobStatPreview.BehaviourKey(jobs[i]),
                    absoluteGameHours);
            }
        }

        /// <summary>Stage B integrity checks — call after bootstrap.</summary>
        public void AssertPrototypeIntegrity(IReadOnlyList<WorkerRuntime> crew)
        {
            if (crew == null) return;

            for (int i = 0; i < crew.Count; i++)
            {
                var w = crew[i];
                if (w == null) continue;
                Debug.Assert(
                    TryGetAssignment(w.WorkerId, out var a) && a.JobType != JobType.Unassigned,
                    $"Worker {w.WorkerId} missing assignment");
            }

            // Prototype may be 5 (legacy) or 6 (with Steward) — only assert jobs that have workers
            for (int i = 0; i < crew.Count; i++)
            {
                var w = crew[i];
                if (w == null) continue;
                var a = GetAssignment(w.WorkerId);
                if (a == null) continue;
                Debug.Assert(
                    TryGetWorkerIdForJob(a.JobType, out int wid) && wid == w.WorkerId,
                    $"Job {a.JobType} mapping mismatch");
            }

            // Unique stats per person
            for (int i = 0; i < crew.Count; i++)
            for (int j = i + 1; j < crew.Count; j++)
            {
                if (crew[i] == null || crew[j] == null) continue;
                Debug.Assert(
                    !ReferenceEquals(crew[i].Stats, crew[j].Stats),
                    "Workers must not share WorkerStats sheets");
            }

            // Default mapping: worker index → expected job (extend when Steward present)
            JobType[] expected =
            {
                JobType.Prospecting,
                JobType.Excavation,
                JobType.Hauling,
                JobType.Refining,
                JobType.Engineering,
                JobType.Steward,
            };
            for (int i = 0; i < crew.Count && i < expected.Length; i++)
            {
                var w = crew[i];
                if (w == null) continue;
                var a = GetAssignment(w.WorkerId);
                Debug.Assert(a != null && a.JobType == expected[i],
                    $"Worker {w.WorkerId} job {a?.JobType} != expected {expected[i]}");
                string expectedKey = JobStatPreview.BehaviourKey(expected[i]);
                bool providerOk = a.ProviderId == expectedKey
                    || (expected[i] == JobType.Prospecting && a.ProviderId.StartsWith("scanner."))
                    || (expected[i] == JobType.Excavation && a.ProviderId.StartsWith("excavator."))
                    || (expected[i] == JobType.Hauling && a.ProviderId.StartsWith("hauler."))
                    || (expected[i] == JobType.Refining && a.ProviderId.StartsWith("washer."))
                    || (expected[i] == JobType.Engineering && a.ProviderId.StartsWith("engineer."));
                Debug.Assert(providerOk,
                    $"Worker {w.WorkerId} provider mismatch: {a.ProviderId}");
            }
        }
    }
}
