using UnityEngine;

namespace DeepCore.FreeMovement
{
    public readonly struct WorkResolveResult
    {
        public readonly string TaskId;
        public readonly float Score;
        public readonly string Reason;
        public readonly float Suitability01;
        public readonly WorkAvailabilityResult Availability;
        public readonly WorkScoreBreakdown Breakdown;

        public WorkResolveResult(
            string taskId, float score, string reason, float suit, WorkAvailabilityResult avail,
            WorkScoreBreakdown breakdown = default)
        {
            TaskId = taskId ?? "";
            Score = score;
            Reason = reason ?? "";
            Suitability01 = suit;
            Availability = avail;
            Breakdown = breakdown;
        }

        public static WorkResolveResult None(string reason) =>
            new WorkResolveResult("", -1f, reason, 0f, WorkAvailabilityResult.None(reason));
    }

    public struct WorkScoreBreakdown
    {
        public float Priority;
        public float Deficit;
        public float Suitability;
        public float Distance;
        public float Condition;
        public float Continuity;
        public float Emergency;
        /// <summary>Soft factors only (within-band competition).</summary>
        public float SoftTotal;
        public float Total;
    }

    /// <summary>
    /// RimWorld-style priority resolver with HARD priority bands.
    /// P1 candidates never compete on score with P2+. Soft factors only within a band.
    /// Does not create work — scores existing opportunities.
    /// </summary>
    public static class WorkPriorityResolver
    {
        public const float EmergencyScoreBoost = 1000f;
        /// <summary>Display/diagnostics only — not used for cross-band competition.</summary>
        public const float PriorityWeight = 120f;
        public const float DeficitWeight = 28f;
        public const float SuitWeight = 18f;
        public const float DistanceWeight = 12f;
        public const float ContinuityBonus = 22f;
        public const float CompletedTargetPenalty = 14f;
        /// <summary>Same-band switch requires this soft-score edge over the current task.</summary>
        public const float SamePrioritySwitchMargin = 35f;

        public static bool IsHardBlocked(WorkerRuntime wr, out string reason)
        {
            reason = "";
            if (wr == null || !wr.IsAlive) { reason = "dead/null"; return true; }
            if (wr.State != null && wr.State.Incapacitated) { reason = "incapacitated"; return true; }
            if (wr.CampBody != null && wr.CampBody.BeingRescued) { reason = "being rescued"; return true; }
            if (wr.CampBody != null && wr.CampBody.ToiletTripActive) { reason = "toilet"; return true; }
            if (wr.CampBody != null && wr.CampBody.InjuryReturnActive) { reason = "injury return"; return true; }
            if (wr.CampBody != null && wr.CampBody.SeekingStewardCare
                && wr.State != null && wr.State.NeedsCare)
            {
                reason = "seeking care";
                return true;
            }
            if (wr.State != null && wr.State.ClaustroSeekingExit)
            {
                reason = "claustrophobia retreat";
                return true;
            }
            var status = InjuryResponse.EvaluateWorkStatus(wr);
            if (status == WorkerInjuryWorkStatus.Incapacitated)
            {
                reason = "injury incapacitated";
                return true;
            }
            return false;
        }

        /// <summary>True if <paramref name="a"/> is a strictly higher player priority band than <paramref name="b"/> (P1 &gt; P2 &gt; …). OFF is lowest.</summary>
        public static bool IsHigherPriorityBand(WorkPriorityLevel a, WorkPriorityLevel b)
        {
            if (a == WorkPriorityLevel.Off) return false;
            if (b == WorkPriorityLevel.Off) return true;
            return (int)a < (int)b;
        }

        public static WorkResolveResult Resolve(
            WorkerRuntime wr,
            JobType currentJob,
            Vector2 workerPos,
            WorkAvailabilityContext ctx,
            float gameHours,
            bool allowEmergencyInterrupt)
        {
            if (IsHardBlocked(wr, out string block))
                return WorkResolveResult.None(block);
            if (wr.Priorities == null)
            {
                wr.Priorities = new WorkerPriorityPrefs();
                wr.Priorities.EnsureAllTasksRegistered();
            }

            var prefs = wr.Priorities;
            string current = prefs.ActiveTaskId ?? "";
            prefs.LastWinningPriorityBand = 0;
            prefs.LastBandRejectHint = "";

            // Emergency override — outside voluntary bands
            if (allowEmergencyInterrupt)
            {
                var emerg = TryResolveEmergency(wr, currentJob, workerPos, ctx, gameHours, current);
                if (!string.IsNullOrEmpty(emerg.TaskId))
                {
                    prefs.LastWinningPriorityBand = prefs.GetPriority(emerg.TaskId) == WorkPriorityLevel.Off
                        ? 0 : (int)prefs.GetPriority(emerg.TaskId);
                    return emerg;
                }
            }

            // Hard bands: P1 → P2 → P3 → P4. First non-empty band wins entirely.
            WorkPriorityLevel[] bands =
            {
                WorkPriorityLevel.P1, WorkPriorityLevel.P2,
                WorkPriorityLevel.P3, WorkPriorityLevel.P4,
            };
            for (int b = 0; b < bands.Length; b++)
            {
                var band = bands[b];
                var bandResult = ResolveWithinBand(
                    wr, currentJob, workerPos, ctx, gameHours, current, band, out int candCount);
                if (candCount <= 0) continue;

                prefs.LastWinningPriorityBand = (int)band;
                if (!string.IsNullOrEmpty(current))
                {
                    var curP = prefs.GetPriority(current);
                    if (IsHigherPriorityBand(band, curP))
                        prefs.LastBandRejectHint =
                            $"{WorkTaskRegistry.Get(current)?.ShortName ?? current} P{(int)curP} rejected: LOWER PRIORITY BAND";
                }
                return bandResult;
            }

            return WorkResolveResult.None("no eligible work");
        }

        static WorkResolveResult TryResolveEmergency(
            WorkerRuntime wr, JobType currentJob, Vector2 workerPos,
            WorkAvailabilityContext ctx, float gameHours, string current)
        {
            WorkResolveResult best = WorkResolveResult.None("");
            float bestScore = float.MinValue;
            var all = WorkTaskRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                if (!def.EmergencyCapable) continue;
                var prio = wr.Priorities.GetPriority(def.Id);
                if (prio == WorkPriorityLevel.Off) continue;
                var avail = ctx.Probe(def.Id);
                if (!avail.Available) continue;
                if (!CanPhysicallyPerform(wr, def, avail, out _)) continue;

                float urg = avail.Urgency01;
                float boost = 0f;
                if (def.Id == WorkerGenericTaskIds.Rescue && urg >= 0.9f)
                    boost = EmergencyScoreBoost;
                else if (def.Id == WorkerGenericTaskIds.TreatInjuries && urg >= 0.85f)
                    boost = EmergencyScoreBoost * 0.85f;
                if (boost < 1f) continue;

                float suit = Suitability01(wr, def, currentJob);
                var br = ScoreSoftBreakdown(wr, def, prio, avail, suit, workerPos, current, gameHours);
                br.Emergency = boost;
                br.Total = br.SoftTotal + boost;
                if (br.Total > bestScore)
                {
                    bestScore = br.Total;
                    best = new WorkResolveResult(def.Id, br.Total,
                        "EMERGENCY · " + BuildReason(def, prio, avail, suit), suit, avail, br);
                }
            }
            return best;
        }

        static WorkResolveResult ResolveWithinBand(
            WorkerRuntime wr,
            JobType currentJob,
            Vector2 workerPos,
            WorkAvailabilityContext ctx,
            float gameHours,
            string current,
            WorkPriorityLevel band,
            out int candidateCount)
        {
            candidateCount = 0;
            float bestScore = float.MinValue;
            WorkResolveResult best = WorkResolveResult.None("no eligible work");
            float currentTaskScore = float.MinValue;
            bool currentInBand = !string.IsNullOrEmpty(current)
                                 && wr.Priorities.GetPriority(current) == band;

            var all = WorkTaskRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                var prio = wr.Priorities.GetPriority(def.Id);
                if (prio != band) continue;

                var avail = ctx.Probe(def.Id);
                if (!avail.Available) continue;
                if (!CanPhysicallyPerform(wr, def, avail, out _)) continue;

                candidateCount++;
                float suit = Suitability01(wr, def, currentJob);
                var br = ScoreSoftBreakdown(wr, def, prio, avail, suit, workerPos, current, gameHours);
                float score = br.SoftTotal;
                br.Total = score;

                if (currentInBand && def.Id == current)
                    currentTaskScore = score;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = new WorkResolveResult(def.Id, score,
                        $"band P{(int)band} · " + BuildReason(def, prio, avail, suit),
                        suit, avail, br);
                }
            }

            if (candidateCount == 0) return best;

            // Same-band hysteresis only — never across bands
            if (currentInBand && !string.IsNullOrEmpty(current) && best.TaskId != current
                && currentTaskScore > float.MinValue / 2f
                && best.Score < currentTaskScore + SamePrioritySwitchMargin)
            {
                return new WorkResolveResult(current, currentTaskScore,
                    $"band P{(int)band} · hold current (score margin)",
                    wr.Priorities.Suitability01, ctx.Probe(current), best.Breakdown);
            }

            return best;
        }

        /// <summary>Whether specialization host may continue voluntary work for this JobType.</summary>
        public static bool HostAllowsVoluntaryWork(WorkerRuntime wr, JobType job)
        {
            if (wr?.Priorities == null) return true;
            string active = wr.Priorities.ActiveTaskId ?? "";
            if (string.IsNullOrEmpty(active))
                return HasAnyEnabledStationTask(wr, job);

            var station = StationJobForTask(active);
            // Person-authority / non-station active task → host must yield
            if (station == JobType.Unassigned)
                return false;
            // Active task belongs to a different exclusive station → stop this host
            if (station != job)
                return false;
            return !wr.Priorities.IsOff(active);
        }

        public static bool HasAnyEnabledStationTask(WorkerRuntime wr, JobType job)
        {
            if (wr?.Priorities == null) return true;
            switch (job)
            {
                case JobType.Excavation:
                    return !wr.Priorities.IsOff(WorkerGenericTaskIds.Excavate);
                case JobType.Hauling:
                    return !wr.Priorities.IsOff(WorkerGenericTaskIds.HaulMaterials);
                case JobType.Refining:
                    return !wr.Priorities.IsOff(WorkerGenericTaskIds.RefineOre);
                case JobType.Prospecting:
                    return !wr.Priorities.IsOff(WorkerGenericTaskIds.Prospect)
                           || !wr.Priorities.IsOff(WorkerGenericTaskIds.AnalyseSurvey);
                case JobType.Engineering:
                    return !wr.Priorities.IsOff(WorkerGenericTaskIds.InstallSupports)
                           || !wr.Priorities.IsOff(WorkerGenericTaskIds.InstallLighting)
                           || !wr.Priorities.IsOff(WorkerGenericTaskIds.RepairEquipment);
                case JobType.Steward:
                    return !wr.Priorities.IsOff(WorkerGenericTaskIds.TreatInjuries)
                           || !wr.Priorities.IsOff(WorkerGenericTaskIds.PrepareMeals)
                           || !wr.Priorities.IsOff(WorkerGenericTaskIds.CleanCamp)
                           || !wr.Priorities.IsOff(WorkerGenericTaskIds.TendCampSystems);
                default:
                    return true;
            }
        }

        /// <summary>Soft-factor score within a priority band. PriorityWeight is display-only.</summary>
        static WorkScoreBreakdown ScoreSoftBreakdown(
            WorkerRuntime wr,
            WorkTaskDefinition def,
            WorkPriorityLevel prio,
            WorkAvailabilityResult avail,
            float suit,
            Vector2 workerPos,
            string currentTaskId,
            float gameHours)
        {
            var br = new WorkScoreBreakdown
            {
                Priority = (5 - (int)prio) * PriorityWeight, // diagnostic only
            };

            float deficit = wr.Priorities.TargetDeficit(def.Id);
            br.Deficit = deficit * DeficitWeight;
            float target = wr.Priorities.GetTargetHours(def.Id);
            float worked = wr.Priorities.GetWorkedHours(def.Id);
            if (target > 0.01f && worked >= target)
                br.Deficit -= CompletedTargetPenalty;

            br.Suitability = suit * SuitWeight;

            float dist = Vector2.Distance(workerPos, avail.HintWorld);
            br.Distance = -Mathf.Clamp(dist, 0f, 40f) / 40f * DistanceWeight;

            if (wr.State != null)
            {
                if (wr.State.ExhaustionLatched) br.Condition -= 8f;
                br.Condition -= wr.State.MentalFatigue * 0.04f;
                br.Condition -= (100f - wr.State.FocusState) * 0.03f;
                br.Condition -= wr.State.Injury * 0.05f;
            }

            if (!string.IsNullOrEmpty(currentTaskId) && currentTaskId == def.Id)
            {
                float held = gameHours - wr.Priorities.ActiveTaskStartedGameHours;
                if (held >= 0f && held < WorkerPriorityPrefs.MinCommitGameHours)
                    br.Continuity = ContinuityBonus * 1.5f;
                else
                    br.Continuity = ContinuityBonus;
            }

            br.SoftTotal = br.Deficit + br.Suitability + br.Distance
                           + br.Condition + br.Continuity + avail.Urgency01 * 6f;
            br.Total = br.SoftTotal;
            return br;
        }

        static WorkScoreBreakdown ScoreTaskBreakdown(
            WorkerRuntime wr,
            WorkTaskDefinition def,
            WorkPriorityLevel prio,
            WorkAvailabilityResult avail,
            float suit,
            Vector2 workerPos,
            string currentTaskId,
            float gameHours) =>
            ScoreSoftBreakdown(wr, def, prio, avail, suit, workerPos, currentTaskId, gameHours);

        static float ScoreTask(
            WorkerRuntime wr,
            WorkTaskDefinition def,
            WorkPriorityLevel prio,
            WorkAvailabilityResult avail,
            float suit,
            Vector2 workerPos,
            string currentTaskId,
            float gameHours) =>
            ScoreSoftBreakdown(wr, def, prio, avail, suit, workerPos, currentTaskId, gameHours).SoftTotal;

        public static float Suitability01(WorkerRuntime wr, WorkTaskDefinition def, JobType currentJob)
        {
            if (wr?.Stats == null || def == null) return 0.4f;
            float s = 0.35f;
            // Familiarity nudge from specialization / current job — not permission / not cross-band
            if (def.FamiliarJob != JobType.Unassigned && currentJob == def.FamiliarJob)
                s += 0.18f;

            switch (def.Id)
            {
                case WorkerGenericTaskIds.Rescue:
                    s += Stat01(wr, WorkerStatId.HeavyLifting) * 0.2f
                         + Stat01(wr, WorkerStatId.Empathy) * 0.15f
                         + Stat01(wr, WorkerStatId.Composure) * 0.1f;
                    break;
                case WorkerGenericTaskIds.TreatInjuries:
                    s += Stat01(wr, WorkerStatId.Empathy) * 0.25f
                         + Stat01(wr, WorkerStatId.Recovery) * 0.2f
                         + Stat01(wr, WorkerStatId.Focus) * 0.1f;
                    break;
                case WorkerGenericTaskIds.ClearDebris:
                    s += Stat01(wr, WorkerStatId.HeavyLifting) * 0.25f
                         + Stat01(wr, WorkerStatId.Stamina) * 0.15f
                         + Stat01(wr, WorkerStatId.RawPower) * 0.1f;
                    break;
                case WorkerGenericTaskIds.InstallSupports:
                case WorkerGenericTaskIds.InstallLighting:
                case WorkerGenericTaskIds.RepairEquipment:
                    s += Stat01(wr, WorkerStatId.Mechanics) * 0.25f
                         + Stat01(wr, WorkerStatId.SafetyProtocol) * 0.2f
                         + Stat01(wr, WorkerStatId.Focus) * 0.1f;
                    break;
                case WorkerGenericTaskIds.Excavate:
                    s += Stat01(wr, WorkerStatId.RawPower) * 0.15f
                         + Stat01(wr, WorkerStatId.Mechanics) * 0.15f
                         + Stat01(wr, WorkerStatId.Toughness) * 0.1f;
                    break;
                case WorkerGenericTaskIds.Prospect:
                case WorkerGenericTaskIds.AnalyseSurvey:
                    s += Stat01(wr, WorkerStatId.Focus) * 0.2f
                         + Stat01(wr, WorkerStatId.Lithology) * 0.15f
                         + Stat01(wr, WorkerStatId.Mineralogy) * 0.15f;
                    break;
                case WorkerGenericTaskIds.HaulMaterials:
                    s += Stat01(wr, WorkerStatId.HeavyLifting) * 0.2f
                         + Stat01(wr, WorkerStatId.Logistics) * 0.2f
                         + Stat01(wr, WorkerStatId.Stamina) * 0.1f;
                    break;
                case WorkerGenericTaskIds.RefineOre:
                    s += Stat01(wr, WorkerStatId.Chemistry) * 0.25f
                         + Stat01(wr, WorkerStatId.Focus) * 0.15f
                         + Stat01(wr, WorkerStatId.Finesse) * 0.1f;
                    break;
                case WorkerGenericTaskIds.PrepareMeals:
                case WorkerGenericTaskIds.CleanCamp:
                case WorkerGenericTaskIds.MaintainHygiene:
                case WorkerGenericTaskIds.TendCampSystems:
                    s += Stat01(wr, WorkerStatId.Empathy) * 0.15f
                         + Stat01(wr, WorkerStatId.Logistics) * 0.15f
                         + Stat01(wr, WorkerStatId.Composure) * 0.1f;
                    break;
            }

            s *= WorkerInjuryConsequences.ManualWorkMul(wr.Injuries);
            return Mathf.Clamp01(s);
        }

        /// <summary>
        /// Voluntary active task must stay valid. OFF always invalidates.
        /// Hard-state is reported but does not clear ActiveTaskId (host/CanPerform already stop work).
        /// </summary>
        public static bool ValidateActiveTask(WorkerRuntime wr, out string reason)
        {
            reason = "";
            if (wr?.Priorities == null)
            {
                reason = "no prefs";
                return true;
            }
            string active = wr.Priorities.ActiveTaskId ?? "";
            if (string.IsNullOrEmpty(active))
            {
                reason = "none";
                return true;
            }
            if (WorkTaskRegistry.Get(active) == null)
            {
                reason = "unknown task";
                return false;
            }
            if (wr.Priorities.IsOff(active))
            {
                reason = "priority OFF";
                return false;
            }
            if (IsHardBlocked(wr, out string block))
            {
                reason = block;
                return true;
            }
            reason = "ok";
            return true;
        }

        static bool CanPhysicallyPerform(
            WorkerRuntime wr, WorkTaskDefinition def, WorkAvailabilityResult avail, out string reason)
        {
            reason = "";
            if (def.Id == WorkerGenericTaskIds.Rescue)
            {
                if (!WorkerRescue.CanAcceptRescueDuty(wr))
                {
                    reason = "cannot accept rescue";
                    return false;
                }
                return true;
            }
            if (InjuryResponse.EvaluateWorkStatus(wr) == WorkerInjuryWorkStatus.OffDuty
                && def.Category == WorkTaskCategory.Production)
            {
                reason = "off duty — production";
                return false;
            }
            // Tool/station presence is probed via availability (excavator/host exists)
            if (def.RequiresTool && def.Id == WorkerGenericTaskIds.Excavate && !avail.Available)
            {
                reason = "excavator/work unavailable";
                return false;
            }
            return true;
        }

        static float Stat01(WorkerRuntime wr, WorkerStatId id) =>
            wr.Stats != null ? wr.Stats.Get(id) / 20f : 0.5f;

        static string BuildReason(
            WorkTaskDefinition def, WorkPriorityLevel prio, WorkAvailabilityResult avail, float suit)
        {
            string suitLab = suit >= 0.72f ? "HIGH" : suit >= 0.48f ? "MED" : "LOW";
            return $"P{(int)prio} · {avail.Reason} · suit {suitLab}";
        }

        /// <summary>Map task → exclusive station JobType when applicable (claim/swap).</summary>
        public static JobType StationJobForTask(string taskId) => taskId switch
        {
            WorkerGenericTaskIds.Excavate => JobType.Excavation,
            WorkerGenericTaskIds.Prospect => JobType.Prospecting,
            WorkerGenericTaskIds.AnalyseSurvey => JobType.Prospecting,
            WorkerGenericTaskIds.HaulMaterials => JobType.Hauling,
            WorkerGenericTaskIds.RefineOre => JobType.Refining,
            WorkerGenericTaskIds.InstallSupports => JobType.Engineering,
            WorkerGenericTaskIds.InstallLighting => JobType.Engineering,
            WorkerGenericTaskIds.RepairEquipment => JobType.Engineering,
            WorkerGenericTaskIds.PrepareMeals => JobType.Steward,
            WorkerGenericTaskIds.CleanCamp => JobType.Steward,
            WorkerGenericTaskIds.TendCampSystems => JobType.Steward,
            WorkerGenericTaskIds.TreatInjuries => JobType.Steward,
            _ => JobType.Unassigned,
        };

        public static string SuitabilityLabel(float s01) =>
            s01 >= 0.72f ? "HIGH" : s01 >= 0.48f ? "MED" : "LOW";

        public static string WhySuitable(WorkerRuntime wr, WorkTaskDefinition def, JobType job)
        {
            if (wr == null || def == null) return "—";
            var sb = new System.Text.StringBuilder(128);
            float suit = Suitability01(wr, def, job);
            sb.Append("Suitability: ").Append(SuitabilityLabel(suit));
            if (def.FamiliarJob != JobType.Unassigned && job == def.FamiliarJob)
                sb.Append("\n- Familiar with ").Append(def.FamiliarJob).Append(" work");
            switch (def.Id)
            {
                case WorkerGenericTaskIds.InstallSupports:
                case WorkerGenericTaskIds.InstallLighting:
                case WorkerGenericTaskIds.RepairEquipment:
                    sb.Append("\n- Mechanics ").Append(wr.Stats.Get(WorkerStatId.Mechanics));
                    sb.Append("\n- Safety Protocol ").Append(wr.Stats.Get(WorkerStatId.SafetyProtocol));
                    break;
                case WorkerGenericTaskIds.Rescue:
                case WorkerGenericTaskIds.ClearDebris:
                case WorkerGenericTaskIds.HaulMaterials:
                    sb.Append("\n- Heavy Lifting ").Append(wr.Stats.Get(WorkerStatId.HeavyLifting));
                    break;
                case WorkerGenericTaskIds.TreatInjuries:
                    sb.Append("\n- Empathy ").Append(wr.Stats.Get(WorkerStatId.Empathy));
                    sb.Append("\n- Recovery ").Append(wr.Stats.Get(WorkerStatId.Recovery));
                    break;
                case WorkerGenericTaskIds.Prospect:
                case WorkerGenericTaskIds.AnalyseSurvey:
                    sb.Append("\n- Lithology ").Append(wr.Stats.Get(WorkerStatId.Lithology));
                    sb.Append("\n- Focus ").Append(wr.Stats.Get(WorkerStatId.Focus));
                    break;
                case WorkerGenericTaskIds.RefineOre:
                    sb.Append("\n- Chemistry ").Append(wr.Stats.Get(WorkerStatId.Chemistry));
                    break;
                case WorkerGenericTaskIds.Excavate:
                    sb.Append("\n- Raw Power ").Append(wr.Stats.Get(WorkerStatId.RawPower));
                    sb.Append("\n- Mechanics ").Append(wr.Stats.Get(WorkerStatId.Mechanics));
                    break;
            }
            return sb.ToString();
        }
    }
}
