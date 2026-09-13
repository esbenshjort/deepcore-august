using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    public enum WorkerBodyPart : byte
    {
        Head = 0,
        Torso = 1,
        Back = 2,
        Shoulder = 3,
        Arm = 4,
        HandWrist = 5,
        Leg = 6,
        Knee = 7,
        Ankle = 8,
        Foot = 9,
    }

    public enum WorkerInjurySeverity : byte
    {
        Minor = 0,
        Moderate = 1,
        Serious = 2,
        Critical = 3,
    }

    public enum WorkerInjuryCause : byte
    {
        TerrainFall = 0,
        ExhaustionFall = 1,
        LoadedFall = 2,
        ExcavationOverheat = 3,
        SocialFight = 4,
        Other = 5,
        TunnelCollapse = 6,
        DebrisImpact = 7,
    }

    /// <summary>Mine-sensible injury types only — no disease/medical tree.</summary>
    public enum WorkerInjuryType : byte
    {
        // Minor
        Bruising = 0,
        CutAbrasion = 1,
        Sprain = 2,
        MuscleStrain = 3,
        // Moderate
        SevereSprain = 10,
        DeepCut = 11,
        ShoulderInjury = 12,
        KneeInjury = 13,
        BackStrain = 14,
        MinorFracture = 15,
        // Serious
        BrokenArm = 20,
        BrokenWrist = 21,
        BrokenLeg = 22,
        BrokenAnkle = 23,
        BrokenFoot = 24,
        RibFracture = 25,
        SeriousBackInjury = 26,
        Concussion = 27,
        // Critical
        SevereHeadInjury = 30,
        MajorCrush = 31,
        SevereTrauma = 32,
    }

    [Serializable]
    public sealed class WorkerInjuryRecord
    {
        public WorkerInjuryType Type;
        public WorkerBodyPart BodyPart;
        public WorkerInjurySeverity Severity;
        public WorkerInjuryCause Cause;
        public float InflictedGameHours;
        public float RecoveryGameHoursTotal;
        public float RecoveryGameHoursLeft;
        public float MeterContribution;
        public string CauseLabel = "";
        /// <summary>Steward stabilized (Serious+) — recovery rate improved, not healed.</summary>
        public bool StabilizedBySteward;
        public bool Active => RecoveryGameHoursLeft > 0.01f;

        public string DisplayName => WorkerInjuryCatalog.DisplayName(Type);
        public string SeverityLabel => Severity.ToString().ToUpperInvariant();

        public string HoverSummary()
        {
            var c = WorkerInjuryConsequences.Describe(this);
            int days = Mathf.Max(1, Mathf.CeilToInt(RecoveryGameHoursLeft / 24f));
            return $"{DisplayName}\n{SeverityLabel}\n{c}\nEstimated recovery: {days} day{(days == 1 ? "" : "s")}.";
        }
    }

    public static class WorkerInjuryCatalog
    {
        public static string DisplayName(WorkerInjuryType t) => t switch
        {
            WorkerInjuryType.Bruising => "BRUISING",
            WorkerInjuryType.CutAbrasion => "CUT / ABRASION",
            WorkerInjuryType.Sprain => "SPRAIN",
            WorkerInjuryType.MuscleStrain => "MUSCLE STRAIN",
            WorkerInjuryType.SevereSprain => "SEVERE SPRAIN",
            WorkerInjuryType.DeepCut => "DEEP CUT",
            WorkerInjuryType.ShoulderInjury => "SHOULDER INJURY",
            WorkerInjuryType.KneeInjury => "KNEE INJURY",
            WorkerInjuryType.BackStrain => "BACK STRAIN",
            WorkerInjuryType.MinorFracture => "MINOR FRACTURE",
            WorkerInjuryType.BrokenArm => "BROKEN ARM",
            WorkerInjuryType.BrokenWrist => "BROKEN WRIST",
            WorkerInjuryType.BrokenLeg => "BROKEN LEG",
            WorkerInjuryType.BrokenAnkle => "BROKEN ANKLE",
            WorkerInjuryType.BrokenFoot => "BROKEN FOOT",
            WorkerInjuryType.RibFracture => "RIB FRACTURE",
            WorkerInjuryType.SeriousBackInjury => "SERIOUS BACK INJURY",
            WorkerInjuryType.Concussion => "CONCUSSION",
            WorkerInjuryType.SevereHeadInjury => "SEVERE HEAD INJURY",
            WorkerInjuryType.MajorCrush => "MAJOR CRUSH INJURY",
            WorkerInjuryType.SevereTrauma => "SEVERE TRAUMA",
            _ => t.ToString().ToUpperInvariant(),
        };

        public static WorkerInjurySeverity SeverityOf(WorkerInjuryType t)
        {
            int v = (int)t;
            if (v >= 30) return WorkerInjurySeverity.Critical;
            if (v >= 20) return WorkerInjurySeverity.Serious;
            if (v >= 10) return WorkerInjurySeverity.Moderate;
            return WorkerInjurySeverity.Minor;
        }

        public static float DefaultRecoveryHours(WorkerInjuryType t) => SeverityOf(t) switch
        {
            WorkerInjurySeverity.Minor => 18f,       // ~¾ day
            WorkerInjurySeverity.Moderate => 48f,    // ~2 days
            WorkerInjurySeverity.Serious => 120f,    // ~5 days
            WorkerInjurySeverity.Critical => 240f,   // ~10 days
            _ => 24f,
        };

        public static float MeterAmount(WorkerInjuryType t) => SeverityOf(t) switch
        {
            WorkerInjurySeverity.Minor => 6f,
            WorkerInjurySeverity.Moderate => 18f,
            WorkerInjurySeverity.Serious => 32f,
            WorkerInjurySeverity.Critical => 55f,
            _ => 8f,
        };

        public static WorkerBodyPart DefaultPart(WorkerInjuryType t) => t switch
        {
            WorkerInjuryType.Bruising => WorkerBodyPart.Torso,
            WorkerInjuryType.CutAbrasion => WorkerBodyPart.HandWrist,
            WorkerInjuryType.Sprain => WorkerBodyPart.Ankle,
            WorkerInjuryType.MuscleStrain => WorkerBodyPart.Leg,
            WorkerInjuryType.SevereSprain => WorkerBodyPart.Ankle,
            WorkerInjuryType.DeepCut => WorkerBodyPart.Arm,
            WorkerInjuryType.ShoulderInjury => WorkerBodyPart.Shoulder,
            WorkerInjuryType.KneeInjury => WorkerBodyPart.Knee,
            WorkerInjuryType.BackStrain => WorkerBodyPart.Back,
            WorkerInjuryType.MinorFracture => WorkerBodyPart.HandWrist,
            WorkerInjuryType.BrokenArm => WorkerBodyPart.Arm,
            WorkerInjuryType.BrokenWrist => WorkerBodyPart.HandWrist,
            WorkerInjuryType.BrokenLeg => WorkerBodyPart.Leg,
            WorkerInjuryType.BrokenAnkle => WorkerBodyPart.Ankle,
            WorkerInjuryType.BrokenFoot => WorkerBodyPart.Foot,
            WorkerInjuryType.RibFracture => WorkerBodyPart.Torso,
            WorkerInjuryType.SeriousBackInjury => WorkerBodyPart.Back,
            WorkerInjuryType.Concussion => WorkerBodyPart.Head,
            WorkerInjuryType.SevereHeadInjury => WorkerBodyPart.Head,
            WorkerInjuryType.MajorCrush => WorkerBodyPart.Torso,
            WorkerInjuryType.SevereTrauma => WorkerBodyPart.Torso,
            _ => WorkerBodyPart.Torso,
        };
    }

    /// <summary>Body-part + type consequences — never a single Injury speed mul.</summary>
    public static class WorkerInjuryConsequences
    {
        public static float WalkSpeedMul(WorkerInjuryStore store)
        {
            if (store == null || store.Count == 0) return 1f;
            float mul = 1f;
            for (int i = 0; i < store.Active.Count; i++)
            {
                var inj = store.Active[i];
                if (!inj.Active) continue;
                mul *= WalkMulOne(inj);
            }
            return Mathf.Clamp(mul, 0.35f, 1f);
        }

        public static float LoadCarryMul(WorkerInjuryStore store)
        {
            if (store == null || store.Count == 0) return 1f;
            float mul = 1f;
            for (int i = 0; i < store.Active.Count; i++)
            {
                var inj = store.Active[i];
                if (!inj.Active) continue;
                mul *= LoadMulOne(inj);
            }
            return Mathf.Clamp(mul, 0.25f, 1f);
        }

        public static float ManualWorkMul(WorkerInjuryStore store)
        {
            if (store == null || store.Count == 0) return 1f;
            float mul = 1f;
            for (int i = 0; i < store.Active.Count; i++)
            {
                var inj = store.Active[i];
                if (!inj.Active) continue;
                mul *= ManualMulOne(inj);
            }
            return Mathf.Clamp(mul, 0.25f, 1f);
        }

        public static float FocusWorkMul(WorkerInjuryStore store)
        {
            if (store == null || store.Count == 0) return 1f;
            float mul = 1f;
            for (int i = 0; i < store.Active.Count; i++)
            {
                var inj = store.Active[i];
                if (!inj.Active) continue;
                if (inj.BodyPart == WorkerBodyPart.Head || inj.Type == WorkerInjuryType.Concussion)
                    mul *= inj.Severity switch
                    {
                        WorkerInjurySeverity.Critical => 0.55f,
                        WorkerInjurySeverity.Serious => 0.72f,
                        WorkerInjurySeverity.Moderate => 0.88f,
                        _ => 0.96f,
                    };
            }
            return Mathf.Clamp(mul, 0.45f, 1f);
        }

        public static bool RestrictsWalking(WorkerInjuryStore store) =>
            WalkSpeedMul(store) < 0.85f;

        public static bool RestrictsManualWork(WorkerInjuryStore store) =>
            ManualWorkMul(store) < 0.85f;

        public static bool ForcesOutOfWork(WorkerInjuryStore store)
        {
            if (store == null) return false;
            for (int i = 0; i < store.Active.Count; i++)
            {
                var inj = store.Active[i];
                if (!inj.Active) continue;
                if (inj.Severity >= WorkerInjurySeverity.Critical) return true;
                if (inj.Severity >= WorkerInjurySeverity.Serious
                    && (inj.BodyPart == WorkerBodyPart.Leg
                        || inj.BodyPart == WorkerBodyPart.Ankle
                        || inj.BodyPart == WorkerBodyPart.Foot
                        || inj.BodyPart == WorkerBodyPart.Head
                        || inj.Type == WorkerInjuryType.SeriousBackInjury))
                    return true;
            }
            return false;
        }

        public static string Describe(WorkerInjuryRecord inj)
        {
            if (inj == null) return "";
            return inj.Type switch
            {
                WorkerInjuryType.BrokenFoot or WorkerInjuryType.BrokenAnkle or WorkerInjuryType.BrokenLeg =>
                    "Walking heavily impaired.\nExcavation/manual work restricted.",
                WorkerInjuryType.BrokenArm or WorkerInjuryType.BrokenWrist =>
                    "Ordinary walking mostly fine.\nPhysical/manual work heavily restricted.",
                WorkerInjuryType.SeriousBackInjury or WorkerInjuryType.BackStrain =>
                    "Load carrying strongly affected.\nBending/lifting painful.",
                WorkerInjuryType.Concussion or WorkerInjuryType.SevereHeadInjury =>
                    "Focus/attention impaired.\nWork restrictions apply.",
                WorkerInjuryType.Bruising or WorkerInjuryType.CutAbrasion =>
                    "Small operational effect.",
                WorkerInjuryType.Sprain or WorkerInjuryType.SevereSprain or WorkerInjuryType.KneeInjury =>
                    "Uneven ground is risky.\nWalking slowed.",
                WorkerInjuryType.ShoulderInjury =>
                    "Lifting and overhead work impaired.",
                _ => inj.Severity >= WorkerInjurySeverity.Serious
                    ? "Significant physical restriction."
                    : "Moderate discomfort under strain.",
            };
        }

        static float WalkMulOne(WorkerInjuryRecord inj)
        {
            // Typed fractures of the lower limb: independent walk usually impossible
            if (inj.Type is WorkerInjuryType.BrokenLeg or WorkerInjuryType.BrokenAnkle
                or WorkerInjuryType.BrokenFoot)
            {
                return inj.Severity switch
                {
                    WorkerInjurySeverity.Critical => 0.38f,
                    WorkerInjurySeverity.Serious => 0.46f,
                    WorkerInjurySeverity.Moderate => 0.72f,
                    _ => 0.88f,
                };
            }
            switch (inj.BodyPart)
            {
                case WorkerBodyPart.Foot:
                case WorkerBodyPart.Ankle:
                    return inj.Severity switch
                    {
                        WorkerInjurySeverity.Critical => 0.40f,
                        WorkerInjurySeverity.Serious => 0.52f,
                        WorkerInjurySeverity.Moderate => 0.78f,
                        _ => 0.92f,
                    };
                case WorkerBodyPart.Leg:
                case WorkerBodyPart.Knee:
                    return inj.Severity switch
                    {
                        WorkerInjurySeverity.Critical => 0.45f,
                        WorkerInjurySeverity.Serious => 0.58f,
                        WorkerInjurySeverity.Moderate => 0.82f,
                        _ => 0.94f,
                    };
                case WorkerBodyPart.Back:
                    return inj.Severity >= WorkerInjurySeverity.Serious ? 0.80f : 0.94f;
                case WorkerBodyPart.Arm:
                case WorkerBodyPart.HandWrist:
                case WorkerBodyPart.Shoulder:
                    // Arms barely affect ordinary walking
                    return inj.Severity >= WorkerInjurySeverity.Serious ? 0.96f : 1f;
                case WorkerBodyPart.Head:
                    return inj.Severity >= WorkerInjurySeverity.Serious ? 0.88f : 0.97f;
                default:
                    return inj.Severity >= WorkerInjurySeverity.Serious ? 0.90f : 0.98f;
            }
        }

        static float LoadMulOne(WorkerInjuryRecord inj)
        {
            if (inj.BodyPart == WorkerBodyPart.Back
                || inj.Type == WorkerInjuryType.BackStrain
                || inj.Type == WorkerInjuryType.SeriousBackInjury)
            {
                return inj.Severity switch
                {
                    WorkerInjurySeverity.Critical => 0.30f,
                    WorkerInjurySeverity.Serious => 0.42f,
                    WorkerInjurySeverity.Moderate => 0.65f,
                    _ => 0.85f,
                };
            }
            if (inj.BodyPart == WorkerBodyPart.Shoulder)
                return inj.Severity >= WorkerInjurySeverity.Moderate ? 0.70f : 0.90f;
            if (inj.BodyPart is WorkerBodyPart.Arm or WorkerBodyPart.HandWrist)
                return inj.Severity >= WorkerInjurySeverity.Serious ? 0.75f : 0.92f;
            if (inj.BodyPart is WorkerBodyPart.Foot or WorkerBodyPart.Ankle or WorkerBodyPart.Leg)
                return inj.Severity >= WorkerInjurySeverity.Serious ? 0.70f : 0.95f;
            return 1f;
        }

        static float ManualMulOne(WorkerInjuryRecord inj)
        {
            if (inj.BodyPart is WorkerBodyPart.Arm or WorkerBodyPart.HandWrist or WorkerBodyPart.Shoulder)
            {
                return inj.Severity switch
                {
                    WorkerInjurySeverity.Critical => 0.30f,
                    WorkerInjurySeverity.Serious => 0.40f,
                    WorkerInjurySeverity.Moderate => 0.70f,
                    _ => 0.90f,
                };
            }
            if (inj.BodyPart == WorkerBodyPart.Back)
                return inj.Severity >= WorkerInjurySeverity.Serious ? 0.55f : 0.85f;
            if (inj.BodyPart is WorkerBodyPart.Foot or WorkerBodyPart.Ankle or WorkerBodyPart.Leg or WorkerBodyPart.Knee)
                return inj.Severity >= WorkerInjurySeverity.Serious ? 0.75f : 0.95f;
            if (inj.BodyPart == WorkerBodyPart.Head)
                return inj.Severity >= WorkerInjurySeverity.Serious ? 0.65f : 0.92f;
            return 1f;
        }
    }

    /// <summary>Person-owned active injuries — follows WorkerRuntime across jobs.</summary>
    public sealed class WorkerInjuryStore
    {
        public const int MaxActive = 8;
        readonly List<WorkerInjuryRecord> _active = new(MaxActive);
        readonly List<string> _recentAccidents = new(12);

        public IReadOnlyList<WorkerInjuryRecord> Active => _active;
        public IReadOnlyList<string> RecentAccidents => _recentAccidents;
        public int Count => _active.Count;
        public WorkerInjuryRecord LastInflicted { get; private set; }

        public WorkerInjuryRecord Add(WorkerInjuryRecord record)
        {
            if (record == null) return null;
            // Stack same type+part: refresh recovery if worse/equal
            for (int i = 0; i < _active.Count; i++)
            {
                var a = _active[i];
                if (a.Type == record.Type && a.BodyPart == record.BodyPart)
                {
                    if ((int)record.Severity >= (int)a.Severity)
                    {
                        a.Severity = record.Severity;
                        a.RecoveryGameHoursTotal = Mathf.Max(a.RecoveryGameHoursTotal, record.RecoveryGameHoursTotal);
                        a.RecoveryGameHoursLeft = Mathf.Max(a.RecoveryGameHoursLeft, record.RecoveryGameHoursLeft);
                        a.MeterContribution = Mathf.Max(a.MeterContribution, record.MeterContribution);
                        a.Cause = record.Cause;
                        a.CauseLabel = record.CauseLabel;
                        LastInflicted = a;
                        return a;
                    }
                    return a;
                }
            }
            if (_active.Count >= MaxActive)
                _active.RemoveAt(0);
            _active.Add(record);
            LastInflicted = record;
            return record;
        }

        public void NoteAccident(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            _recentAccidents.Insert(0, line);
            while (_recentAccidents.Count > 10)
                _recentAccidents.RemoveAt(_recentAccidents.Count - 1);
        }

        /// <summary>
        /// Advance recovery. Sleep multiplies rate slightly but never wipes serious injuries overnight.
        /// </summary>
        public void TickRecovery(float gameHours, float recoveryStat01, bool sleeping)
        {
            if (gameHours <= 0f || _active.Count == 0) return;
            float rate = Mathf.Lerp(0.75f, 1.25f, Mathf.Clamp01(recoveryStat01));
            if (sleeping)
                rate *= 1.35f; // rest helps — still far short of healing a fracture overnight
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var a = _active[i];
                if (a == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }
                // Fractures resist sleep magic
                float local = rate;
                if (a.Severity >= WorkerInjurySeverity.Serious && sleeping)
                    local = Mathf.Min(local, 0.55f);
                if (a.StabilizedBySteward)
                    local *= 1.22f; // care improves recovery rate — still multi-day for fractures
                a.RecoveryGameHoursLeft -= gameHours * local;
                if (a.RecoveryGameHoursLeft <= 0f)
                    _active.RemoveAt(i);
            }
            // Soft-sync Injury meter toward contribution floor (never wipe below active wounds)
            // Caller may pass State via SyncNeedsCare after tick
        }

        /// <summary>Pull WorkerState.Injury meter toward active contributions (down as wounds heal).</summary>
        public void SyncInjuryMeter(WorkerState st)
        {
            if (st == null) return;
            float floor = TotalMeterContribution();
            if (st.Injury > floor)
                st.Injury = Mathf.MoveTowards(st.Injury, floor, 8f);
            else if (st.Injury < floor * 0.85f)
                st.Injury = floor * 0.85f;
        }

        public float TotalMeterContribution()
        {
            float s = 0f;
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Active) s += _active[i].MeterContribution;
            return Mathf.Min(100f, s);
        }

        public void SyncNeedsCare(WorkerState st)
        {
            if (st == null) return;
            if (st.Incapacitated)
            {
                st.NeedsCare = true;
                return;
            }
            // Clear when no longer forced — Steward/recovery can drop the flag
            st.NeedsCare = ForcesCare();
        }

        public bool ForcesCare()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var a = _active[i];
                if (!a.Active) continue;
                if (a.Severity >= WorkerInjurySeverity.Serious) return true;
                if (a.Severity >= WorkerInjurySeverity.Moderate && a.MeterContribution >= 18f) return true;
            }
            return false;
        }
    }
}
