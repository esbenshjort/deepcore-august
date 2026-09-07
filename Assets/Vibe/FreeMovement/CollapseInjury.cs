using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Body-location-plausible injuries from tunnel debris — not a flat universal table.
    /// </summary>
    public static class CollapseInjuryResolver
    {
        public static DebrisImpactZone PickZone(Vector2 workerPos, Vector2 epicenter, CollapseSeverity sev)
        {
            if (sev == CollapseSeverity.Major && WorkerRoll.NextUnit() < 0.35f)
                return DebrisImpactZone.Crush;

            Vector2 d = workerPos - epicenter;
            float u = WorkerRoll.NextUnit();
            // Overhead fall more common near epicenter
            if (d.sqrMagnitude < 0.55f * 0.55f && u < 0.55f)
                return DebrisImpactZone.Overhead;
            if (u < 0.22f) return DebrisImpactZone.CatchFall;
            if (u < 0.55f) return DebrisImpactZone.Legs;
            if (u < 0.78f) return DebrisImpactZone.Overhead;
            return DebrisImpactZone.Lateral;
        }

        public static bool RollHit(CollapseSeverity sev, float dist, float hitRadius)
        {
            float t = 1f - Mathf.Clamp01(dist / Mathf.Max(0.2f, hitRadius));
            float chance = sev switch
            {
                CollapseSeverity.MinorDebris => 0.12f + t * 0.18f,
                CollapseSeverity.Blocking => 0.38f + t * 0.35f,
                CollapseSeverity.Major => 0.62f + t * 0.32f,
                _ => 0.2f,
            };
            return WorkerRoll.NextUnit() < chance;
        }

        public static List<WorkerInjuryRecord> ResolveHits(
            WorkerRuntime wr,
            CollapseSeverity sev,
            DebrisImpactZone zone)
        {
            var list = new List<WorkerInjuryRecord>(3);
            if (wr?.State == null || !wr.IsAlive) return list;

            int hits = 1;
            if (sev == CollapseSeverity.Major && WorkerRoll.NextUnit() < 0.55f) hits = 2;
            if (sev == CollapseSeverity.Major && WorkerRoll.NextUnit() < 0.22f) hits = 3;
            if (sev == CollapseSeverity.Blocking && WorkerRoll.NextUnit() < 0.28f) hits = 2;

            // Toughness softens severity pick, never voids major collapses
            int tough = wr.Stats != null ? wr.Stats.Get(WorkerStatId.Toughness) : 10;
            int safety = wr.Stats != null ? wr.Stats.Get(WorkerStatId.SafetyProtocol) : 10;

            for (int i = 0; i < hits; i++)
            {
                var type = PickType(sev, zone, tough, safety, i);
                var part = PartFor(type, zone);
                var rec = WorkerAccidentSystem.ApplyTypedInjury(
                    wr,
                    type,
                    WorkerInjuryCause.TunnelCollapse,
                    $"Debris — {zone}",
                    part);
                if (rec != null) list.Add(rec);
            }
            return list;
        }

        public static bool ShouldIncapacitate(
            WorkerRuntime wr,
            CollapseSeverity sev,
            List<WorkerInjuryRecord> justInflicted)
        {
            if (wr?.State == null || !wr.IsAlive) return false;
            if (sev == CollapseSeverity.MinorDebris) return false;

            for (int i = 0; i < justInflicted.Count; i++)
            {
                var r = justInflicted[i];
                if (r == null) continue;
                if (r.Severity >= WorkerInjurySeverity.Critical) return true;
                if (r.Type == WorkerInjuryType.BrokenLeg
                    || r.Type == WorkerInjuryType.BrokenAnkle
                    || r.Type == WorkerInjuryType.BrokenFoot
                    || r.Type == WorkerInjuryType.SevereHeadInjury
                    || r.Type == WorkerInjuryType.MajorCrush
                    || r.Type == WorkerInjuryType.SevereTrauma)
                    return true;
                if (r.Type == WorkerInjuryType.Concussion && sev == CollapseSeverity.Major
                    && justInflicted.Count >= 2)
                    return true;
            }

            // Two serious lower-body or head injuries
            int seriousImmob = 0;
            if (wr.Injuries != null)
            {
                for (int i = 0; i < wr.Injuries.Active.Count; i++)
                {
                    var r = wr.Injuries.Active[i];
                    if (r == null || !r.Active || r.Severity < WorkerInjurySeverity.Serious) continue;
                    if (r.BodyPart is WorkerBodyPart.Leg or WorkerBodyPart.Ankle or WorkerBodyPart.Foot
                        or WorkerBodyPart.Knee or WorkerBodyPart.Head or WorkerBodyPart.Back)
                        seriousImmob++;
                }
            }
            return seriousImmob >= 2 && sev == CollapseSeverity.Major;
        }

        static WorkerInjuryType PickType(
            CollapseSeverity sev,
            DebrisImpactZone zone,
            int toughness,
            int safety,
            int hitIndex)
        {
            float soft = Mathf.Clamp01(((toughness - 8) + (safety - 8) * 0.5f) / 16f);
            float u = WorkerRoll.NextUnit();

            if (sev == CollapseSeverity.MinorDebris)
            {
                // Usually nothing serious — caller already rolled hit (rare)
                if (u < 0.55f) return WorkerInjuryType.Bruising;
                if (u < 0.8f) return WorkerInjuryType.CutAbrasion;
                return zone == DebrisImpactZone.Legs
                    ? WorkerInjuryType.MuscleStrain
                    : WorkerInjuryType.Sprain;
            }

            // Critical — rare even on major
            float critChance = sev == CollapseSeverity.Major ? 0.07f - soft * 0.04f : 0.015f;
            if (hitIndex == 0 && zone == DebrisImpactZone.Crush)
                critChance += 0.08f;
            if (u < critChance)
            {
                if (zone == DebrisImpactZone.Overhead || zone == DebrisImpactZone.Crush)
                    return WorkerRoll.NextUnit() < 0.45f
                        ? WorkerInjuryType.SevereHeadInjury
                        : WorkerInjuryType.MajorCrush;
                return WorkerInjuryType.SevereTrauma;
            }

            float seriousChance = sev == CollapseSeverity.Major
                ? 0.42f - soft * 0.12f
                : 0.22f - soft * 0.08f;
            if (zone == DebrisImpactZone.Crush) seriousChance += 0.12f;
            u = WorkerRoll.NextUnit();
            if (u < seriousChance)
                return PickSerious(zone);

            float modChance = sev == CollapseSeverity.Major ? 0.55f : 0.45f;
            if (WorkerRoll.NextUnit() < modChance)
                return PickModerate(zone);

            return PickMinor(zone);
        }

        static WorkerInjuryType PickSerious(DebrisImpactZone zone) => zone switch
        {
            DebrisImpactZone.Legs => Pick(WorkerInjuryType.BrokenLeg, WorkerInjuryType.BrokenAnkle,
                WorkerInjuryType.BrokenFoot, WorkerInjuryType.SeriousBackInjury),
            DebrisImpactZone.Overhead => Pick(WorkerInjuryType.Concussion, WorkerInjuryType.BrokenArm,
                WorkerInjuryType.BrokenWrist, WorkerInjuryType.ShoulderInjury, WorkerInjuryType.RibFracture),
            DebrisImpactZone.CatchFall => Pick(WorkerInjuryType.BrokenWrist, WorkerInjuryType.BrokenArm,
                WorkerInjuryType.BrokenAnkle),
            DebrisImpactZone.Lateral => Pick(WorkerInjuryType.RibFracture, WorkerInjuryType.BrokenArm,
                WorkerInjuryType.SeriousBackInjury, WorkerInjuryType.BrokenLeg),
            DebrisImpactZone.Crush => Pick(WorkerInjuryType.MajorCrush, WorkerInjuryType.RibFracture,
                WorkerInjuryType.BrokenLeg, WorkerInjuryType.SeriousBackInjury, WorkerInjuryType.Concussion),
            _ => WorkerInjuryType.BrokenArm,
        };

        static WorkerInjuryType PickModerate(DebrisImpactZone zone) => zone switch
        {
            DebrisImpactZone.Legs => Pick(WorkerInjuryType.SevereSprain, WorkerInjuryType.KneeInjury,
                WorkerInjuryType.MinorFracture, WorkerInjuryType.BackStrain),
            DebrisImpactZone.Overhead => Pick(WorkerInjuryType.ShoulderInjury, WorkerInjuryType.DeepCut,
                WorkerInjuryType.MinorFracture),
            DebrisImpactZone.CatchFall => Pick(WorkerInjuryType.MinorFracture, WorkerInjuryType.SevereSprain,
                WorkerInjuryType.DeepCut),
            DebrisImpactZone.Lateral => Pick(WorkerInjuryType.DeepCut, WorkerInjuryType.BackStrain,
                WorkerInjuryType.ShoulderInjury),
            _ => Pick(WorkerInjuryType.BackStrain, WorkerInjuryType.DeepCut, WorkerInjuryType.KneeInjury),
        };

        static WorkerInjuryType PickMinor(DebrisImpactZone zone) => zone switch
        {
            DebrisImpactZone.Legs => Pick(WorkerInjuryType.Bruising, WorkerInjuryType.MuscleStrain,
                WorkerInjuryType.Sprain, WorkerInjuryType.CutAbrasion),
            DebrisImpactZone.Overhead => Pick(WorkerInjuryType.Bruising, WorkerInjuryType.CutAbrasion,
                WorkerInjuryType.MuscleStrain),
            DebrisImpactZone.CatchFall => Pick(WorkerInjuryType.Sprain, WorkerInjuryType.Bruising,
                WorkerInjuryType.CutAbrasion),
            _ => Pick(WorkerInjuryType.Bruising, WorkerInjuryType.CutAbrasion, WorkerInjuryType.MuscleStrain),
        };

        static WorkerBodyPart PartFor(WorkerInjuryType type, DebrisImpactZone zone)
        {
            // Prefer catalog default, but nudge by zone when type is generic
            var def = WorkerInjuryCatalog.DefaultPart(type);
            if (type is WorkerInjuryType.Bruising or WorkerInjuryType.CutAbrasion or WorkerInjuryType.MuscleStrain)
            {
                return zone switch
                {
                    DebrisImpactZone.Legs => WorkerBodyPart.Leg,
                    DebrisImpactZone.Overhead => WorkerBodyPart.Shoulder,
                    DebrisImpactZone.CatchFall => WorkerBodyPart.HandWrist,
                    DebrisImpactZone.Lateral => WorkerBodyPart.Torso,
                    DebrisImpactZone.Crush => WorkerBodyPart.Torso,
                    _ => def,
                };
            }
            return def;
        }

        static WorkerInjuryType Pick(params WorkerInjuryType[] opts)
        {
            if (opts == null || opts.Length == 0) return WorkerInjuryType.Bruising;
            int i = Mathf.Clamp(Mathf.FloorToInt(WorkerRoll.NextUnit() * opts.Length), 0, opts.Length - 1);
            return opts[i];
        }
    }
}
