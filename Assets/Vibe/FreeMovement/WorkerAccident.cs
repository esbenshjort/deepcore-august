using UnityEngine;

namespace DeepCore.FreeMovement
{
    public enum WorkerAccidentOutcome : byte
    {
        None = 0,
        Stumble = 1,
        RecoveredFall = 2,
        Fell = 3,
        FellInjured = 4,
    }

    /// <summary>Last accident presentation payload for world speech / DEV.</summary>
    public sealed class WorkerAccidentReport
    {
        public int WorkerId;
        public string DisplayName = "";
        public WorkerAccidentOutcome Outcome;
        public WorkerFootingEvent Footing;
        public WorkerTerrainKind Terrain;
        public WorkerInjuryRecord Injury;
        public string Headline = "";
        public string Detail = "";
        public float GameHours;
        public bool IsMeaningfulInjury;
    }

    /// <summary>
    /// Terrain exposure → stumble/slip → recover OR fall → optional injury.
    /// Stumble usually does NOT injure. Broken bones uncommon. Critical rare.
    /// </summary>
    public static class WorkerAccidentSystem
    {
        public static WorkerAccidentReport LastReport { get; private set; }
        public static int AuditStumbleCount;
        public static int AuditFallCount;
        public static int AuditInjuryCount;
        public static int AuditBrokenBoneCount;

        public static void ResetAuditCounters()
        {
            AuditStumbleCount = AuditFallCount = AuditInjuryCount = AuditBrokenBoneCount = 0;
        }

        /// <summary>
        /// Resolve a footing fail into stumble / fall / injury. Called from locomotion.
        /// </summary>
        public static WorkerAccidentReport ResolveFootingFail(
            WorkerRuntime wr,
            WorkerFootingEvent footing,
            in WorkerTerrainSample terrain,
            float carriedLoad01,
            int balanceMargin)
        {
            if (wr?.State == null || !wr.State.IsAlive) return null;
            var profile = WorkerPhysicalProfile.From(wr);
            var report = new WorkerAccidentReport
            {
                WorkerId = wr.WorkerId,
                DisplayName = wr.DisplayName,
                Footing = footing,
                Terrain = terrain.Kind,
                GameHours = WorkerStateClock.GameHours,
            };

            // BriefSlow / Stumble: almost never injury
            if (footing == WorkerFootingEvent.BriefSlow || footing == WorkerFootingEvent.Stumble)
            {
                AuditStumbleCount++;
                report.Outcome = WorkerAccidentOutcome.Stumble;
                report.Headline = "STUMBLED";
                report.Detail = $"on {terrain.Kind}";
                wr.Injuries.NoteAccident($"{wr.DisplayName} STUMBLED ({terrain.Kind})");

                // Rare scrape only on rubble + bad margin
                if (footing == WorkerFootingEvent.Stumble
                    && terrain.Difficulty01 >= 0.7f
                    && balanceMargin >= 5
                    && WorkerRoll.NextUnit() < 0.06f)
                {
                    ApplyInjury(wr, report, PickMinorTerrainInjury(terrain),
                        WorkerInjuryCause.TerrainFall, "Stumble scrape");
                }

                LastReport = report;
                return report;
            }

            // LossOfFooting → Toughness (+ Recovery hint) to catch the fall
            int toughDc = 9 + Mathf.RoundToInt(terrain.Difficulty01 * 8f);
            if (carriedLoad01 > 0.45f) toughDc += 2;
            if (IsExhausted(wr)) toughDc += 2;
            if (wr.Injuries.Count > 0) toughDc += 1;
            toughDc = Mathf.Clamp(toughDc, 8, 18);

            int mod = (wr.Stats.Get(WorkerStatId.Agility) - 10) / 5;
            var catchRoll = WorkerRoll.Check(wr.Stats, WorkerStatId.Toughness, toughDc, mod);
            if (catchRoll.Success)
            {
                AuditStumbleCount++;
                report.Outcome = WorkerAccidentOutcome.RecoveredFall;
                report.Headline = "STUMBLED";
                report.Detail = $"caught fall on {terrain.Kind}";
                wr.Injuries.NoteAccident($"{wr.DisplayName} STUMBLED — caught fall ({terrain.Kind})");
                LastReport = report;
                return report;
            }

            // Fell
            AuditFallCount++;
            report.Outcome = WorkerAccidentOutcome.Fell;
            report.Headline = "FELL";
            report.Detail = $"on {terrain.Kind}";

            float injuryChance = 0.55f + terrain.Difficulty01 * 0.25f;
            injuryChance *= Mathf.Lerp(1.15f, 0.75f, Mathf.Clamp01((profile.FootingResist - 0.4f) / 0.95f));
            if (IsExhausted(wr)) injuryChance += 0.12f;
            if (carriedLoad01 > 0.5f) injuryChance += 0.10f;
            if (wr.State.Injury >= 25f) injuryChance += 0.08f;
            injuryChance = Mathf.Clamp(injuryChance, 0.25f, 0.88f);

            if (WorkerRoll.NextUnit() > injuryChance)
            {
                wr.Injuries.NoteAccident($"{wr.DisplayName} FELL ({terrain.Kind}) — uninjured");
                LastReport = report;
                return report;
            }

            var cause = WorkerInjuryCause.TerrainFall;
            if (IsExhausted(wr) && carriedLoad01 < 0.35f) cause = WorkerInjuryCause.ExhaustionFall;
            else if (carriedLoad01 >= 0.45f) cause = WorkerInjuryCause.LoadedFall;

            var type = PickFallInjury(terrain, cause, balanceMargin + (toughDc - catchRoll.Total));
            ApplyInjury(wr, report, type, cause, $"{cause} on {terrain.Kind}");
            report.Outcome = WorkerAccidentOutcome.FellInjured;
            report.Headline = report.Injury != null && report.Injury.Severity >= WorkerInjurySeverity.Serious
                ? "SERIOUS FALL"
                : "FELL";
            if (report.Injury != null)
                report.Detail = WorkerInjuryCatalog.DisplayName(report.Injury.Type);
            LastReport = report;
            return report;
        }

        public static WorkerInjuryRecord ApplyTypedInjury(
            WorkerRuntime wr,
            WorkerInjuryType type,
            WorkerInjuryCause cause,
            string causeLabel,
            WorkerBodyPart? partOverride = null)
        {
            if (wr?.State == null || !wr.State.IsAlive) return null;
            var rec = BuildRecord(type, cause, causeLabel, partOverride);
            wr.Injuries.Add(rec);
            float applied = wr.State.AddInjury(rec.MeterContribution);
            float floor = wr.Injuries.TotalMeterContribution() * 0.85f;
            if (wr.State.Injury < floor)
                wr.State.Injury = floor;
            wr.Injuries.SyncNeedsCare(wr.State);
            if (WorkerInjuryConsequences.ForcesOutOfWork(wr.Injuries))
                wr.State.NeedsCare = true;

            // Severity-scaled Frustration magnitude (processor still applies Composure/compounding)
            float frustMag = InjuryResponse.FrustrationEventMagnitude(rec);
            WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                wr.WorkerId,
                WorkerStateEventType.Injury,
                Mathf.Max(applied, frustMag),
                causeLabel ?? cause.ToString()));

            AuditInjuryCount++;
            if (rec.Severity >= WorkerInjurySeverity.Serious
                && (rec.Type == WorkerInjuryType.BrokenArm
                    || rec.Type == WorkerInjuryType.BrokenWrist
                    || rec.Type == WorkerInjuryType.BrokenLeg
                    || rec.Type == WorkerInjuryType.BrokenAnkle
                    || rec.Type == WorkerInjuryType.BrokenFoot
                    || rec.Type == WorkerInjuryType.RibFracture
                    || rec.Type == WorkerInjuryType.MinorFracture))
                AuditBrokenBoneCount++;

            wr.Injuries.NoteAccident(
                $"{wr.DisplayName} {rec.DisplayName} ({rec.SeverityLabel}) — {causeLabel}");

            // Injury Response V1 — yield/return decision (runner listens)
            InjuryResponseHub.NotifyApplied(wr, rec);
            return rec;
        }

        static void ApplyInjury(
            WorkerRuntime wr,
            WorkerAccidentReport report,
            WorkerInjuryType type,
            WorkerInjuryCause cause,
            string label)
        {
            var rec = ApplyTypedInjury(wr, type, cause, label);
            report.Injury = rec;
            report.IsMeaningfulInjury = rec != null && rec.Severity >= WorkerInjurySeverity.Moderate;
        }

        static WorkerInjuryRecord BuildRecord(
            WorkerInjuryType type,
            WorkerInjuryCause cause,
            string causeLabel,
            WorkerBodyPart? partOverride)
        {
            var sev = WorkerInjuryCatalog.SeverityOf(type);
            float hours = WorkerInjuryCatalog.DefaultRecoveryHours(type);
            return new WorkerInjuryRecord
            {
                Type = type,
                BodyPart = partOverride ?? WorkerInjuryCatalog.DefaultPart(type),
                Severity = sev,
                Cause = cause,
                CauseLabel = causeLabel ?? "",
                InflictedGameHours = WorkerStateClock.GameHours,
                RecoveryGameHoursTotal = hours,
                RecoveryGameHoursLeft = hours,
                MeterContribution = WorkerInjuryCatalog.MeterAmount(type),
            };
        }

        static bool IsExhausted(WorkerRuntime wr)
        {
            if (wr?.State == null || !wr.State.StaminaPrimed) return false;
            float max = wr.PhysicalStaminaMax;
            if (max < 0.001f) return false;
            return wr.State.PhysicalStamina / max <= 0.12f;
        }

        static WorkerInjuryType PickMinorTerrainInjury(in WorkerTerrainSample terrain)
        {
            float u = WorkerRoll.NextUnit();
            if (terrain.OnLooseRock && u < 0.45f) return WorkerInjuryType.CutAbrasion;
            if (u < 0.55f) return WorkerInjuryType.Bruising;
            return WorkerInjuryType.Sprain;
        }

        /// <summary>Cause/terrain-weighted injury pick — not every accident can cause every injury.</summary>
        public static WorkerInjuryType PickFallInjury(
            in WorkerTerrainSample terrain,
            WorkerInjuryCause cause,
            int severityMargin)
        {
            // Critical only on extreme margin + rubble
            if (severityMargin >= 12 && terrain.Difficulty01 >= 0.85f && WorkerRoll.NextUnit() < 0.04f)
                return WorkerRoll.NextUnit() < 0.5f
                    ? WorkerInjuryType.SevereTrauma
                    : WorkerInjuryType.MajorCrush;

            // Serious bones — uncommon
            float boneChance = 0.08f + Mathf.Clamp01(severityMargin / 20f) * 0.10f;
            if (terrain.Difficulty01 >= 0.7f) boneChance += 0.05f;
            if (cause == WorkerInjuryCause.LoadedFall) boneChance += 0.04f;
            if (WorkerRoll.NextUnit() < boneChance)
                return PickBone(terrain, cause);

            // Moderate band
            float modChance = 0.35f + terrain.Difficulty01 * 0.2f;
            if (WorkerRoll.NextUnit() < modChance)
                return PickModerate(terrain, cause);

            return PickMinorTerrainInjury(terrain);
        }

        static WorkerInjuryType PickBone(in WorkerTerrainSample terrain, WorkerInjuryCause cause)
        {
            float u = WorkerRoll.NextUnit();
            // Loose rock / rubble → lower extremity & catch-fall wrist
            if (terrain.OnLooseRock || terrain.Kind == WorkerTerrainKind.Rubble)
            {
                if (u < 0.28f) return WorkerInjuryType.BrokenAnkle;
                if (u < 0.48f) return WorkerInjuryType.BrokenFoot;
                if (u < 0.68f) return WorkerInjuryType.BrokenWrist; // catching the fall
                if (u < 0.82f) return WorkerInjuryType.BrokenLeg;
                return WorkerInjuryType.RibFracture;
            }
            if (cause == WorkerInjuryCause.LoadedFall)
            {
                if (u < 0.35f) return WorkerInjuryType.SeriousBackInjury;
                if (u < 0.55f) return WorkerInjuryType.BrokenAnkle;
                if (u < 0.75f) return WorkerInjuryType.BrokenWrist;
                return WorkerInjuryType.BrokenArm;
            }
            if (u < 0.25f) return WorkerInjuryType.BrokenWrist;
            if (u < 0.45f) return WorkerInjuryType.BrokenArm;
            if (u < 0.65f) return WorkerInjuryType.BrokenAnkle;
            if (u < 0.80f) return WorkerInjuryType.Concussion;
            return WorkerInjuryType.RibFracture;
        }

        static WorkerInjuryType PickModerate(in WorkerTerrainSample terrain, WorkerInjuryCause cause)
        {
            float u = WorkerRoll.NextUnit();
            if (cause == WorkerInjuryCause.LoadedFall && u < 0.4f)
                return WorkerInjuryType.BackStrain;
            if (terrain.OnLooseRock)
            {
                if (u < 0.35f) return WorkerInjuryType.SevereSprain;
                if (u < 0.55f) return WorkerInjuryType.KneeInjury;
                if (u < 0.75f) return WorkerInjuryType.DeepCut;
                return WorkerInjuryType.MinorFracture;
            }
            if (u < 0.3f) return WorkerInjuryType.SevereSprain;
            if (u < 0.5f) return WorkerInjuryType.ShoulderInjury;
            if (u < 0.7f) return WorkerInjuryType.KneeInjury;
            if (u < 0.85f) return WorkerInjuryType.BackStrain;
            return WorkerInjuryType.DeepCut;
        }

        /// <summary>Overheat → burns/strain distribution (Toughness fail already decided).</summary>
        public static WorkerInjuryType PickOverheatInjury()
        {
            float u = WorkerRoll.NextUnit();
            if (u < 0.35f) return WorkerInjuryType.DeepCut; // hot metal / steam — treat as deep burn-cut
            if (u < 0.55f) return WorkerInjuryType.ShoulderInjury;
            if (u < 0.75f) return WorkerInjuryType.MuscleStrain;
            if (u < 0.90f) return WorkerInjuryType.BackStrain;
            return WorkerInjuryType.MinorFracture;
        }

        /// <summary>Fight injuries — punches → face/torso/arm, not random broken feet.</summary>
        public static WorkerInjuryType PickFightInjury(float amount)
        {
            float u = WorkerRoll.NextUnit();
            if (amount >= 50f)
                return u < 0.5f ? WorkerInjuryType.SevereHeadInjury : WorkerInjuryType.SevereTrauma;
            if (amount >= 28f)
            {
                if (u < 0.35f) return WorkerInjuryType.Concussion;
                if (u < 0.6f) return WorkerInjuryType.BrokenArm;
                if (u < 0.8f) return WorkerInjuryType.RibFracture;
                return WorkerInjuryType.DeepCut;
            }
            if (amount >= 14f)
            {
                if (u < 0.4f) return WorkerInjuryType.DeepCut;
                if (u < 0.7f) return WorkerInjuryType.ShoulderInjury;
                return WorkerInjuryType.Bruising;
            }
            if (u < 0.5f) return WorkerInjuryType.Bruising;
            if (u < 0.8f) return WorkerInjuryType.CutAbrasion;
            return WorkerInjuryType.MuscleStrain;
        }
    }
}
