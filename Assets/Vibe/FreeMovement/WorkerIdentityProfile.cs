using System;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Optional hiring / biography card for a person. Does not replace WorkerId identity.
    /// </summary>
    [Serializable]
    public sealed class WorkerIdentityProfile
    {
        public int Age = 30;
        public string CareerStanding = "Mid-career";
        public string Background = "";
        /// <summary>Display-only wage ask (no economy spend in V1).</summary>
        public int WageAsk = 100;
        public string[] PersonalityTags = Array.Empty<string>();
        public RecruitmentTraitId[] Traits = Array.Empty<RecruitmentTraitId>();

        public WorkerIdentityProfile Clone()
        {
            var c = new WorkerIdentityProfile
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
            };
            return c;
        }
    }
}
