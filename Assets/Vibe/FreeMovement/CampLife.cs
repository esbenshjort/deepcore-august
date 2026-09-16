using System;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    public enum CampMealQuality : byte
    {
        Good = 0,
        Normal = 1,
        Poor = 2,
        Unsafe = 3,
    }

    public enum CampHygieneBand : byte
    {
        Clean = 0,
        Acceptable = 1,
        Poor = 2,
        Filthy = 3,
    }

    public enum StomachUpsetSeverity : byte
    {
        None = 0,
        Mild = 1,
        Moderate = 2,
        Severe = 3,
    }

    /// <summary>
    /// Person-owned toilet urgency + stomach condition. Follows WorkerRuntime.
    /// </summary>
    public sealed class WorkerCampBody
    {
        /// <summary>0–1 bathroom urgency. Builds gradually; toilet clears it.</summary>
        public float ToiletNeed01;
        public StomachUpsetSeverity StomachUpset;
        public float StomachUpsetHoursLeft;
        /// <summary>Tonight's meal sleep recovery multiplier (0.82–1.12). Reset after night.</summary>
        public float MealSleepRecoveryMul = 1f;
        public float MealMoraleDelta;
        public float MealFrustrationDelta;
        public bool AteTonight;
        public bool ToiletTripActive;
        /// <summary>True after WC use while walking back to workstation (physical return).</summary>
        public bool ToiletReturning;
        /// <summary>Injury Response V1: physically walking to camp for Steward care.</summary>
        public bool InjuryReturnActive;
        /// <summary>At camp awaiting / receiving Steward treatment.</summary>
        public bool SeekingStewardCare;
        /// <summary>Universal rescue: this person yielded specialization and is on rescue duty.</summary>
        public bool RescueDutyActive;
        /// <summary>Universal rescue: casualty — person avatar is sole physical authority.</summary>
        public bool BeingRescued;
        /// <summary>After rescue: walking back to reserved assignment post (no snap).</summary>
        public bool RescueReturning;
        /// <summary>Priority System: yielded specialization for a non-host or cross-task duty.</summary>
        public bool PriorityDutyActive;
        public float ToiletUseSecondsLeft;
        public float LastToiletGameHours = -99f;

        public bool HasStomachUpset => StomachUpset != StomachUpsetSeverity.None
                                       && StomachUpsetHoursLeft > 0.01f;

        public void ClearMealTonight()
        {
            AteTonight = false;
            MealSleepRecoveryMul = 1f;
            MealMoraleDelta = 0f;
            MealFrustrationDelta = 0f;
        }

        public void ClearStomach()
        {
            StomachUpset = StomachUpsetSeverity.None;
            StomachUpsetHoursLeft = 0f;
        }

        public void ApplyStomach(StomachUpsetSeverity sev, float hours)
        {
            if (sev == StomachUpsetSeverity.None) return;
            if ((int)sev >= (int)StomachUpset)
            {
                StomachUpset = sev;
                StomachUpsetHoursLeft = Mathf.Max(StomachUpsetHoursLeft, hours);
            }
            else
                StomachUpsetHoursLeft = Mathf.Max(StomachUpsetHoursLeft, hours * 0.5f);
        }

        public void TickStomach(float gameHours)
        {
            if (StomachUpsetHoursLeft <= 0f)
            {
                ClearStomach();
                return;
            }
            StomachUpsetHoursLeft -= gameHours;
            if (StomachUpsetHoursLeft <= 0f)
                ClearStomach();
        }

        /// <summary>Apply stomach pressure to meters (person condition, not comedy).</summary>
        public void ApplyStomachMeterPressure(WorkerState st, float gameHours)
        {
            if (st == null || !HasStomachUpset || gameHours <= 0f) return;
            float focusHit = StomachUpset switch
            {
                StomachUpsetSeverity.Mild => 1.5f,
                StomachUpsetSeverity.Moderate => 3.5f,
                StomachUpsetSeverity.Severe => 6f,
                _ => 0f,
            };
            float fatigue = StomachUpset switch
            {
                StomachUpsetSeverity.Mild => 1.2f,
                StomachUpsetSeverity.Moderate => 2.5f,
                StomachUpsetSeverity.Severe => 4f,
                _ => 0f,
            };
            st.FocusState = Mathf.Max(5f, st.FocusState - focusHit * gameHours);
            st.MentalFatigue = Mathf.Min(100f, st.MentalFatigue + fatigue * gameHours);
            if (StomachUpset >= StomachUpsetSeverity.Moderate)
                st.AddFrustration(0.35f * (int)StomachUpset * gameHours);
            if (StomachUpset == StomachUpsetSeverity.Severe)
                st.Morale = Mathf.Max(0f, st.Morale - 0.4f * gameHours);
        }

        public float ToiletBuildPerGameHour()
        {
            float baseRate = 1f / 20f; // ~once per day at shift+night (~20h awake-ish)
            if (!HasStomachUpset) return baseRate;
            return StomachUpset switch
            {
                StomachUpsetSeverity.Mild => 1f / 8f,
                StomachUpsetSeverity.Moderate => 1f / 3.5f,
                StomachUpsetSeverity.Severe => 1.05f, // ~hourly
                _ => baseRate,
            };
        }

        /// <summary>Per-person mul so the crew does not all stampede the WC together.</summary>
        public static float ToiletPersonalRateMul(int workerId)
        {
            // Stable 0.72–1.28 spread from id
            int h = Mathf.Abs(workerId * 7919 + 104729);
            return 0.72f + (h % 57) / 100f;
        }

        /// <summary>Individual urgency threshold so trips don't sync.</summary>
        public static float ToiletTripThreshold(int workerId)
        {
            int h = Mathf.Abs(workerId * 40503 + 17);
            return 0.78f + (h % 18) / 100f; // 0.78–0.95
        }

        public bool ToiletUrgencyHigh => ToiletNeed01 >= 0.82f;
        public bool ToiletUrgencyCritical => ToiletNeed01 >= 0.95f;
    }

    /// <summary>Camp-level hygiene + last meal readout (shared, not per-person).</summary>
    public sealed class CampLifeState
    {
        /// <summary>0 = filthy, 1 = clean.</summary>
        public float Hygiene01 = 0.72f;
        public CampMealQuality TonightMeal = CampMealQuality.Normal;
        public bool MealServedTonight;
        public string StewardActivity = "IDLE";
        public int LastSickCount;
        public int MealStreakBad;
        public int MealStreakGood;

        public CampHygieneBand HygieneBand => Hygiene01 switch
        {
            >= 0.78f => CampHygieneBand.Clean,
            >= 0.55f => CampHygieneBand.Acceptable,
            >= 0.32f => CampHygieneBand.Poor,
            _ => CampHygieneBand.Filthy,
        };

        public string HygieneLabel => HygieneBand switch
        {
            CampHygieneBand.Clean => "Clean",
            CampHygieneBand.Acceptable => "Acceptable",
            CampHygieneBand.Poor => "Poor",
            _ => "Filthy",
        };

        public string MealLabel => TonightMeal switch
        {
            CampMealQuality.Good => "Good",
            CampMealQuality.Normal => "Normal",
            CampMealQuality.Poor => "Poor",
            _ => "Unsafe",
        };

        public void DriftHygiene(float gameHours, float stewardCleanPush01)
        {
            // Slow natural dirt from camp use
            Hygiene01 -= gameHours * 0.012f;
            if (stewardCleanPush01 > 0f)
                Hygiene01 += gameHours * stewardCleanPush01 * 0.08f;
            Hygiene01 = Mathf.Clamp01(Hygiene01);
        }

        public void ForceHygiene(CampHygieneBand band)
        {
            Hygiene01 = band switch
            {
                CampHygieneBand.Clean => 0.90f,
                CampHygieneBand.Acceptable => 0.62f,
                CampHygieneBand.Poor => 0.38f,
                _ => 0.15f,
            };
        }
    }

    /// <summary>
    /// Evening meal resolution + sparse social memory. Uses existing WorkerStats only.
    /// Exact mappings: Chemistry (food safety), Finesse (prep), Focus sheet + FocusState,
    /// WorkRate (throughput), Empathy (comfort), Composure (steady under fatigue),
    /// Logistics (camp organization / hygiene discipline), SafetyProtocol (contamination),
    /// Recovery (wound-care aid elsewhere).
    /// </summary>
    public static class CampMealSystem
    {
        public static CampMealQuality ResolveQuality(
            WorkerRuntime steward,
            CampLifeState camp,
            System.Random rng)
        {
            if (steward == null || !steward.IsAlive)
                return CampMealQuality.Poor;

            var s = steward.Stats;
            var st = steward.State;
            float chem = Norm(s.Get(WorkerStatId.Chemistry));
            float finesse = Norm(s.Get(WorkerStatId.Finesse));
            float focusSheet = Norm(s.Get(WorkerStatId.Focus));
            float work = Norm(s.Get(WorkerStatId.WorkRate));
            float composure = Norm(s.Get(WorkerStatId.Composure));
            float logistics = Norm(s.Get(WorkerStatId.Logistics));
            float safety = Norm(s.Get(WorkerStatId.SafetyProtocol));

            float focusState01 = st != null ? st.FocusState / 100f : 0.55f;
            float fatiguePenalty = st != null
                ? Mathf.Clamp01((st.MentalFatigue - 35f) / 65f) * 0.22f
                : 0f;

            float skill = chem * 0.28f + finesse * 0.22f + focusSheet * 0.12f
                          + work * 0.12f + composure * 0.10f + logistics * 0.08f
                          + safety * 0.08f;
            skill = skill * (0.72f + 0.28f * focusState01) - fatiguePenalty;

            float hygiene = camp != null ? camp.Hygiene01 : 0.6f;
            float safetyScore = skill * 0.65f + hygiene * 0.35f;

            // Meaningful D20 — not pure random: skill+hygiene set DC band
            int roll = 1 + (rng?.Next(20) ?? UnityEngine.Random.Range(0, 20));
            float roll01 = (roll - 1) / 19f;
            float score = safetyScore * 0.78f + roll01 * 0.22f;

            if (hygiene < 0.28f && score < 0.55f)
                return CampMealQuality.Unsafe;
            if (score >= 0.72f) return CampMealQuality.Good;
            if (score >= 0.48f) return CampMealQuality.Normal;
            if (score >= 0.32f || hygiene >= 0.40f) return CampMealQuality.Poor;
            return CampMealQuality.Unsafe;
        }

        static float Norm(int stat) => Mathf.Clamp01((stat - 1) / 19f);

        public static void ApplyMealToCrew(
            WorkerRuntime[] crew,
            WorkerRuntime steward,
            CampLifeState camp,
            CampMealQuality quality,
            SocialMemoryStore memory,
            float gameHours,
            System.Random rng)
        {
            if (camp == null) return;
            camp.TonightMeal = quality;
            camp.MealServedTonight = true;

            if (quality == CampMealQuality.Good)
                camp.MealStreakGood++;
            else
                camp.MealStreakGood = 0;

            if (quality >= CampMealQuality.Poor)
                camp.MealStreakBad++;
            else
                camp.MealStreakBad = 0;

            float sleepMul = quality switch
            {
                CampMealQuality.Good => 1.10f,
                CampMealQuality.Normal => 1f,
                CampMealQuality.Poor => 0.90f,
                _ => 0.82f,
            };
            float morale = quality switch
            {
                CampMealQuality.Good => 2.5f,
                CampMealQuality.Normal => 0.4f,
                CampMealQuality.Poor => -1.5f,
                _ => -4f,
            };
            float frustr = quality switch
            {
                CampMealQuality.Good => -1.2f,
                CampMealQuality.Normal => 0f,
                CampMealQuality.Poor => 1.0f,
                _ => 2.5f,
            };

            int sick = 0;
            if (crew != null)
            {
                for (int i = 0; i < crew.Length; i++)
                {
                    var wr = crew[i];
                    if (wr == null || !wr.IsAlive) continue;
                    var body = wr.CampBody;
                    body.AteTonight = true;
                    body.MealSleepRecoveryMul = sleepMul;
                    body.MealMoraleDelta = morale;
                    body.MealFrustrationDelta = frustr;
                    if (wr.State != null)
                    {
                        wr.State.Morale = Mathf.Clamp(wr.State.Morale + morale, 0f, 100f);
                        if (frustr > 0f)
                            wr.State.AddFrustration(frustr);
                        else if (frustr < 0f)
                            wr.State.ReduceFrustration(-frustr);
                    }

                    if (quality == CampMealQuality.Unsafe
                        || (quality == CampMealQuality.Poor && camp.Hygiene01 < 0.40f))
                    {
                        float resist = Norm(wr.Stats.Get(WorkerStatId.Toughness)) * 0.35f
                                       + Norm(wr.Stats.Get(WorkerStatId.Recovery)) * 0.25f;
                        int d20 = 1 + (rng?.Next(20) ?? UnityEngine.Random.Range(0, 20));
                        float risk = (quality == CampMealQuality.Unsafe ? 0.55f : 0.22f)
                                     + (1f - camp.Hygiene01) * 0.25f - resist;
                        if (d20 <= Mathf.Clamp(Mathf.RoundToInt(risk * 20f), 2, 16))
                        {
                            var sev = quality == CampMealQuality.Unsafe
                                ? (d20 <= 4 ? StomachUpsetSeverity.Severe
                                    : d20 <= 9 ? StomachUpsetSeverity.Moderate
                                    : StomachUpsetSeverity.Mild)
                                : StomachUpsetSeverity.Mild;
                            float hrs = sev switch
                            {
                                StomachUpsetSeverity.Mild => 6f,
                                StomachUpsetSeverity.Moderate => 10f,
                                _ => 14f,
                            };
                            body.ApplyStomach(sev, hrs);
                            sick++;
                        }
                    }
                }
            }

            camp.LastSickCount = sick;
            if (quality == CampMealQuality.Unsafe)
                camp.Hygiene01 = Mathf.Max(0f, camp.Hygiene01 - 0.06f);
            else if (quality == CampMealQuality.Good)
                camp.Hygiene01 = Mathf.Min(1f, camp.Hygiene01 + 0.02f);

            MaybeRecordMealMemories(crew, steward, quality, sick, camp, memory, gameHours);
        }

        static void MaybeRecordMealMemories(
            WorkerRuntime[] crew,
            WorkerRuntime steward,
            CampMealQuality quality,
            int sick,
            CampLifeState camp,
            SocialMemoryStore memory,
            float gameHours)
        {
            if (memory == null || steward == null || crew == null) return;
            bool meaningful = quality == CampMealQuality.Good && camp.MealStreakGood >= 1
                              || quality == CampMealQuality.Unsafe
                              || sick >= 2
                              || camp.MealStreakBad >= 3;
            if (!meaningful) return;

            for (int i = 0; i < crew.Length; i++)
            {
                var wr = crew[i];
                if (wr == null || !wr.IsAlive || wr.WorkerId == steward.WorkerId) continue;

                if (quality == CampMealQuality.Good)
                {
                    memory.Add(new SocialMemoryEntry
                    {
                        ObserverId = wr.WorkerId,
                        TargetId = steward.WorkerId,
                        Type = SocialMemoryType.HelpedMe,
                        Strength = 0.42f,
                        Significance = SocialMemorySignificance.Ordinary,
                        Context = SocialContext.Camp,
                        GameTime = gameHours,
                        SourceRef = "camp.meal.good",
                    });
                }
                else if (quality == CampMealQuality.Unsafe || sick >= 2)
                {
                    memory.Add(new SocialMemoryEntry
                    {
                        ObserverId = wr.WorkerId,
                        TargetId = steward.WorkerId,
                        Type = SocialMemoryType.LetMeDown,
                        Strength = sick >= 2 ? 0.55f : 0.45f,
                        Significance = sick >= 2
                            ? SocialMemorySignificance.Significant
                            : SocialMemorySignificance.Ordinary,
                        Context = SocialContext.Camp,
                        GameTime = gameHours,
                        SourceRef = sick >= 2 ? "camp.meal.sickness" : "camp.meal.unsafe",
                    });
                }
                else if (camp.MealStreakBad >= 3)
                {
                    memory.Add(new SocialMemoryEntry
                    {
                        ObserverId = wr.WorkerId,
                        TargetId = steward.WorkerId,
                        Type = SocialMemoryType.LetMeDown,
                        Strength = 0.40f,
                        Significance = SocialMemorySignificance.Ordinary,
                        Context = SocialContext.Camp,
                        GameTime = gameHours,
                        SourceRef = "camp.meal.streak.bad",
                    });
                }
            }

            if (sick >= 2)
            {
                // Shared hardship among sick — sparse pair sample
                int a = -1, b = -1;
                for (int i = 0; i < crew.Length; i++)
                {
                    var wr = crew[i];
                    if (wr == null || !wr.IsAlive || !wr.CampBody.HasStomachUpset) continue;
                    if (a < 0) a = wr.WorkerId;
                    else { b = wr.WorkerId; break; }
                }
                if (a > 0 && b > 0)
                {
                    memory.Add(new SocialMemoryEntry
                    {
                        ObserverId = a,
                        TargetId = b,
                        Type = SocialMemoryType.SharedHardship,
                        Strength = 0.38f,
                        Significance = SocialMemorySignificance.Ordinary,
                        Context = SocialContext.Camp,
                        GameTime = gameHours,
                        SourceRef = "camp.meal.shared.sickness",
                    });
                }
            }
        }
    }

    /// <summary>Steward wound care — modest recovery aid; never wipes serious injuries.</summary>
    public static class StewardWoundCare
    {
        public static bool TryTend(
            WorkerRuntime steward,
            WorkerRuntime patient,
            out string result,
            SocialMemoryStore memory = null,
            float gameHours = 0f)
        {
            result = "No patient";
            if (steward == null || patient == null || !steward.IsAlive || !patient.IsAlive)
                return false;
            if (patient.Injuries == null || patient.Injuries.Count == 0)
            {
                result = "No active injuries";
                return false;
            }

            float empathy = (steward.Stats.Get(WorkerStatId.Empathy) - 1) / 19f;
            float recovery = (steward.Stats.Get(WorkerStatId.Recovery) - 1) / 19f;
            float focus = (steward.Stats.Get(WorkerStatId.Focus) - 1) / 19f;
            float composure = (steward.Stats.Get(WorkerStatId.Composure) - 1) / 19f;
            float skill = empathy * 0.35f + recovery * 0.30f + focus * 0.20f + composure * 0.15f;

            int tended = 0;
            bool stabilizedSerious = false;
            bool stillNeeds = false;
            for (int i = 0; i < patient.Injuries.Active.Count; i++)
            {
                var inj = patient.Injuries.Active[i];
                if (inj == null || !inj.Active) continue;
                if (inj.Severity >= WorkerInjurySeverity.Serious)
                {
                    stillNeeds = true;
                    // V1 stabilize: improve recovery rate + slight meter ease — NOT heal fracture
                    if (!inj.StabilizedBySteward)
                    {
                        inj.StabilizedBySteward = true;
                        inj.MeterContribution = Mathf.Max(
                            inj.MeterContribution * 0.88f, WorkerInjuryCatalog.MeterAmount(inj.Type) * 0.7f);
                        // Tiny shave only — multi-day recovery remains
                        float shave = Mathf.Lerp(0.02f, 0.05f, skill);
                        inj.RecoveryGameHoursLeft *= (1f - shave);
                        stabilizedSerious = true;
                        tended++;
                    }
                    continue;
                }
                // Modest: shave 8–18% of remaining recovery hours on minor/moderate
                float shaveMod = Mathf.Lerp(0.08f, 0.18f, skill);
                inj.RecoveryGameHoursLeft *= (1f - shaveMod);
                tended++;
            }

            patient.Injuries.SyncInjuryMeter(patient.State);
            patient.Injuries.SyncNeedsCare(patient.State);
            if (patient.CampBody != null && tended > 0)
                patient.CampBody.SeekingStewardCare = patient.State != null && patient.State.NeedsCare;

            if (tended > 0 && patient.State != null)
            {
                float fr = InjuryResponse.TreatmentReliefFrustration(tended, stabilizedSerious);
                float mo = InjuryResponse.TreatmentReliefMorale(tended, stabilizedSerious);
                patient.State.ReduceFrustration(fr);
                patient.State.Morale = patient.State.Morale + mo;
            }

            if (tended > 0 && memory != null && steward.WorkerId > 0)
            {
                memory.Add(new SocialMemoryEntry
                {
                    Type = SocialMemoryType.HelpedMe,
                    Strength = stabilizedSerious ? 0.72f : 0.55f,
                    GameTime = gameHours,
                    ObserverId = patient.WorkerId,
                    TargetId = steward.WorkerId,
                    Context = SocialContext.Camp,
                    SourceRef = "steward.woundcare",
                    Significance = stabilizedSerious
                        ? SocialMemorySignificance.Significant
                        : SocialMemorySignificance.Ordinary,
                });
            }

            if (patient.State != null && patient.State.NeedsCare && stillNeeds)
                result = stabilizedSerious
                    ? $"Stabilized serious + tended {tended} — still NeedsCare"
                    : $"Tended {tended} minor/moderate — still NeedsCare";
            else if (tended > 0)
                result = $"Tended {tended} wound(s)";
            else
                result = stillNeeds ? "Beyond Steward V1 — NeedsCare" : "Nothing to tend";
            return tended > 0 || stillNeeds;
        }
    }
}
