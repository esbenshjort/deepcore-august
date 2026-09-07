using System;
using System.Collections.Generic;

namespace DeepCore.FreeMovement
{
    /// <summary>In-progress Recruitment V1 hiring choices (not applied until confirm).</summary>
    public sealed class RecruitmentHiringSession
    {
        public bool IsOpen { get; private set; }
        public JobType BrowseJob { get; set; } = JobType.Excavation;
        public int BrowseIndex { get; set; }
        public WorkerStatId? HoverStat { get; set; }
        public string StatusMessage { get; set; } = "";

        readonly Dictionary<JobType, string> _hiredCandidateId = new();

        public void Open()
        {
            IsOpen = true;
            BrowseJob = JobType.Excavation;
            BrowseIndex = 0;
            HoverStat = null;
            StatusMessage = "";
            _hiredCandidateId.Clear();
        }

        public void Close()
        {
            IsOpen = false;
            HoverStat = null;
            StatusMessage = "";
        }

        public RecruitmentCandidate SelectedCandidate
        {
            get
            {
                var list = RecruitmentCatalog.ForJob(BrowseJob);
                if (list == null || list.Count == 0) return null;
                int i = BrowseIndex;
                if (i < 0) i = 0;
                if (i >= list.Count) i = list.Count - 1;
                return list[i];
            }
        }

        public void NextCandidate(int delta)
        {
            var list = RecruitmentCatalog.ForJob(BrowseJob);
            if (list == null || list.Count == 0) return;
            BrowseIndex = (BrowseIndex + delta + list.Count * 8) % list.Count;
        }

        public void SetBrowseJob(JobType job)
        {
            BrowseJob = job;
            BrowseIndex = 0;
            HoverStat = null;
        }

        public void HireSelected()
        {
            var c = SelectedCandidate;
            if (c == null) return;
            _hiredCandidateId[BrowseJob] = c.CandidateId;
            StatusMessage = $"HIRED  {c.DisplayName.ToUpperInvariant()}  →  {JobStatPreview.DisplayName(BrowseJob).ToUpperInvariant()}";
        }

        public void ClearHire(JobType job)
        {
            _hiredCandidateId.Remove(job);
            StatusMessage = $"CLEARED  {JobStatPreview.DisplayName(job).ToUpperInvariant()}";
        }

        public bool TryGetHired(JobType job, out RecruitmentCandidate candidate)
        {
            candidate = null;
            if (!_hiredCandidateId.TryGetValue(job, out string id)) return false;
            candidate = RecruitmentCatalog.Find(id);
            return candidate != null;
        }

        public string HiredName(JobType job) =>
            TryGetHired(job, out var c) ? c.DisplayName : "—";

        public bool AllJobsFilled
        {
            get
            {
                foreach (var job in RecruitmentCatalog.HireJobs)
                    if (!TryGetHired(job, out _)) return false;
                return true;
            }
        }

        public int FilledCount
        {
            get
            {
                int n = 0;
                foreach (var job in RecruitmentCatalog.HireJobs)
                    if (TryGetHired(job, out _)) n++;
                return n;
            }
        }

        /// <summary>
        /// Build crew in HireJobs order with unique WorkerIds starting at baseId.
        /// </summary>
        public bool TryBuildCrew(int baseWorkerId, out WorkerRuntime[] crew, out JobType[] jobs, out string fail)
        {
            crew = null;
            jobs = null;
            fail = "";
            if (!AllJobsFilled)
            {
                fail = $"Fill all {RecruitmentCatalog.HireJobs.Length} job seats before confirming.";
                return false;
            }

            var list = new List<WorkerRuntime>(RecruitmentCatalog.HireJobs.Length);
            var jobList = new List<JobType>(RecruitmentCatalog.HireJobs.Length);
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int id = baseWorkerId;
            foreach (var job in RecruitmentCatalog.HireJobs)
            {
                if (!TryGetHired(job, out var cand))
                {
                    fail = $"Missing hire for {job}";
                    return false;
                }
                if (!usedNames.Add(cand.DisplayName))
                {
                    // Same person catalog id shouldn't appear twice; still guard.
                }
                list.Add(cand.Materialize(id++));
                jobList.Add(job);
            }

            crew = list.ToArray();
            jobs = jobList.ToArray();
            return true;
        }
    }
}
