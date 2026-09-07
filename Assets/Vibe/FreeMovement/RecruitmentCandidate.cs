using System;

namespace DeepCore.FreeMovement
{
    /// <summary>Stable hiring candidate definition (catalog entry).</summary>
    [Serializable]
    public sealed class RecruitmentCandidate
    {
        public string CandidateId;
        public string DisplayName;
        public JobType TargetJob;
        public int Age;
        public string CareerStanding;
        public string Background;
        public int WageAsk;
        public string SuitabilitySummary;
        public string[] PersonalityTags;
        public RecruitmentTraitId[] Traits;
        /// <summary>Base sheet before trait hire deltas.</summary>
        public WorkerStats BaseStats;

        public RecruitmentCandidate Clone()
        {
            return new RecruitmentCandidate
            {
                CandidateId = CandidateId,
                DisplayName = DisplayName,
                TargetJob = TargetJob,
                Age = Age,
                CareerStanding = CareerStanding,
                Background = Background,
                WageAsk = WageAsk,
                SuitabilitySummary = SuitabilitySummary,
                PersonalityTags = PersonalityTags != null
                    ? (string[])PersonalityTags.Clone()
                    : Array.Empty<string>(),
                Traits = Traits != null
                    ? (RecruitmentTraitId[])Traits.Clone()
                    : Array.Empty<RecruitmentTraitId>(),
                BaseStats = BaseStats != null ? BaseStats.Clone() : WorkerStats.CreateBaseline(),
            };
        }

        /// <summary>Materialize a person for the active crew. WorkerId must be unique and &gt; 0.</summary>
        public WorkerRuntime Materialize(int workerId)
        {
            var stats = BaseStats != null ? BaseStats.Clone() : WorkerStats.CreateBaseline();
            RecruitmentTraits.ApplyToStats(stats, Traits);
            var wr = new WorkerRuntime(workerId, DisplayName, stats)
            {
                Identity = new WorkerIdentityProfile
                {
                    Age = Age,
                    CareerStanding = CareerStanding,
                    Background = Background,
                    WageAsk = WageAsk,
                    PersonalityTags = PersonalityTags != null
                        ? (string[])PersonalityTags.Clone()
                        : Array.Empty<string>(),
                    Traits = Traits != null
                        ? (RecruitmentTraitId[])Traits.Clone()
                        : Array.Empty<RecruitmentTraitId>(),
                },
            };
            return wr;
        }

        /// <summary>Preview runtime (temp id) for face / sheet UI without hiring.</summary>
        public WorkerRuntime PreviewRuntime(int tempId = 9000) => Materialize(tempId);
    }
}
