using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>DEV presets for Mara↔Viktor relationship playtest (axes + minimal memories).</summary>
    public enum RelationshipWorkTestPreset : byte
    {
        StrongProfessional = 0,
        Rivalry = 1,
        Strained = 2,
        Neutral = 3,
    }

    /// <summary>
    /// DEV-only tracker for Excavator↔Engineer repair cooperation over rolling 5 days.
    /// Observation only — does not change simulation formulas.
    /// </summary>
    public sealed class RelationshipWorkPlaytestTracker
    {
        public const int RollingDays = 5;
        public const int DefaultExcavId = 2; // Mara
        public const int DefaultEngId = 5;   // Viktor

        public sealed class DayBucket
        {
            public int DayIndex;
            public int Collaborations;      // dispatch starts
            public int Completions;
            public int Interruptions;       // en-route cancelled (overheat cleared early)
            public int CoopFriction;
            public int CoopSynergy;

            public float QualitySum;
            public int QualitySamples;
            public float QualityMin = float.MaxValue;
            public float QualityMax = float.MinValue;

            public float DispatchMulSum;
            public float RepairDurMulSum;
            public int ModifierSamples;

            public float AvgQuality => QualitySamples > 0 ? QualitySum / QualitySamples : 0f;
            public float AvgDispatchMul => ModifierSamples > 0 ? DispatchMulSum / ModifierSamples : 1f;
            public float AvgRepairDurMul => ModifierSamples > 0 ? RepairDurMulSum / ModifierSamples : 1f;
            public float MinQuality => QualitySamples > 0 ? QualityMin : 0f;
            public float MaxQuality => QualitySamples > 0 ? QualityMax : 0f;

            public void SampleCoop(CooperationAssessment coop)
            {
                if (!coop.Active) return;
                QualitySum += coop.Quality;
                QualitySamples++;
                if (coop.Quality < QualityMin) QualityMin = coop.Quality;
                if (coop.Quality > QualityMax) QualityMax = coop.Quality;
                DispatchMulSum += coop.DispatchSpeedMul;
                RepairDurMulSum += coop.RepairDurationMul;
                ModifierSamples++;
            }

            public void Clear()
            {
                DayIndex = 0;
                Collaborations = Completions = Interruptions = 0;
                CoopFriction = CoopSynergy = 0;
                QualitySum = 0f;
                QualitySamples = 0;
                QualityMin = float.MaxValue;
                QualityMax = float.MinValue;
                DispatchMulSum = RepairDurMulSum = 0f;
                ModifierSamples = 0;
            }
        }

        readonly List<DayBucket> _days = new(RollingDays);
        DayBucket _current = new();
        int _activeDay = -1;
        RelationshipWorkTestPreset _preset = RelationshipWorkTestPreset.Neutral;
        string _lastPresetLabel = "Neutral";

        public DayBucket Current => _current;
        public IReadOnlyList<DayBucket> Days => _days;
        public RelationshipWorkTestPreset ActivePreset => _preset;
        public string LastPresetLabel => _lastPresetLabel;

        public void Reset(int dayIndex)
        {
            _days.Clear();
            _current = new DayBucket();
            _activeDay = -1;
            EnsureDay(dayIndex);
        }

        public void EnsureDay(int dayIndex)
        {
            if (dayIndex == _activeDay) return;
            if (_activeDay > 0 && _current.DayIndex > 0)
                ArchiveCurrent();
            _activeDay = dayIndex;
            _current = new DayBucket { DayIndex = dayIndex };
        }

        void ArchiveCurrent()
        {
            _days.Add(_current);
            while (_days.Count > RollingDays)
                _days.RemoveAt(0);
        }

        public void NotifyDispatch(int dayIndex, CooperationAssessment coop)
        {
            EnsureDay(dayIndex);
            _current.Collaborations++;
            _current.SampleCoop(coop);
        }

        public void NotifyRepairBegin(int dayIndex, CooperationAssessment coop)
        {
            EnsureDay(dayIndex);
            _current.SampleCoop(coop);
        }

        public void NotifyRepairComplete(int dayIndex, CooperationWorkConsequence consequence)
        {
            EnsureDay(dayIndex);
            _current.Completions++;
            if (consequence == CooperationWorkConsequence.CoopBenefit)
                _current.CoopSynergy++;
            else if (consequence == CooperationWorkConsequence.CoopSetback)
                _current.CoopFriction++;
        }

        public void NotifyRepairInterrupted(int dayIndex)
        {
            EnsureDay(dayIndex);
            _current.Interruptions++;
        }

        public string FormatCompactDay(DayBucket d)
        {
            if (d == null) return "";
            var sb = new StringBuilder(192);
            sb.Append($"D{d.DayIndex} collab={d.Collaborations} done={d.Completions} int={d.Interruptions} ");
            sb.Append($"QØ={d.AvgQuality:0.00} [{d.MinQuality:0.00}..{d.MaxQuality:0.00}] ");
            sb.Append($"spd×{d.AvgDispatchMul:0.00} dur×{d.AvgRepairDurMul:0.00} ");
            sb.Append($"fric={d.CoopFriction} syn={d.CoopSynergy}");
            return sb.ToString();
        }

        /// <summary>
        /// Cycle Strong Professional → Rivalry → Strained → Neutral.
        /// Sets Mara↔Viktor T/W/H/R both ways; clears pair memories then seeds only if needed for class.
        /// </summary>
        public RelationshipWorkTestPreset CycleAndApply(
            SocialAuraWorld world,
            SocialMemoryStore memory,
            int excavId = DefaultExcavId,
            int engId = DefaultEngId,
            float gameHours = 0f)
        {
            _preset = (RelationshipWorkTestPreset)(((int)_preset + 1) % 4);
            ApplyPreset(_preset, world, memory, excavId, engId, gameHours);
            return _preset;
        }

        public void ApplyPreset(
            RelationshipWorkTestPreset preset,
            SocialAuraWorld world,
            SocialMemoryStore memory,
            int excavId = DefaultExcavId,
            int engId = DefaultEngId,
            float gameHours = 0f)
        {
            _preset = preset;
            if (world == null) return;

            // Clear pair memories so leftover evidence cannot force wrong class
            memory?.ClearPair(excavId, engId);

            void SetBoth(float t, float w, float h, float r)
            {
                SetOne(world.Relation(excavId, engId), t, w, h, r);
                SetOne(world.Relation(engId, excavId), t, w, h, r);
            }

            switch (preset)
            {
                case RelationshipWorkTestPreset.StrongProfessional:
                    // High Trust+Respect, low Warmth/Hostility → Professional
                    SetBoth(t: 9f, w: 1.5f, h: 0.5f, r: 78f);
                    _lastPresetLabel = "Strong Professional";
                    break;

                case RelationshipWorkTestPreset.Rivalry:
                    // High Hostility + high Respect, cool Warmth → Rivalry
                    SetBoth(t: 2f, w: -2f, h: 12f, r: 78f);
                    _lastPresetLabel = "Rivalry";
                    break;

                case RelationshipWorkTestPreset.Strained:
                    // Low Trust/Respect, elevated Hostility — Strained (no extremeNegMem → not Grudge)
                    SetBoth(t: -6f, w: -2f, h: 8f, r: 32f);
                    _lastPresetLabel = "Strained";
                    break;

                default:
                    SetBoth(t: 0f, w: 0f, h: 0f, r: SocialDirectedRelation.RespectBaseline);
                    _lastPresetLabel = "Neutral";
                    break;
            }
        }

        static void SetOne(SocialDirectedRelation rel, float t, float w, float h, float r)
        {
            if (rel == null) return;
            rel.Trust = t;
            rel.Warmth = w;
            rel.Hostility = h;
            rel.Respect = r;
            rel.Clamp();
        }

        /// <summary>Top memories (both directions) that feed CooperationQuality scoring.</summary>
        public static void FillTopCoopMemories(
            SocialMemoryStore mem, int excavId, int engId,
            List<SocialMemoryEntry> into, int max)
        {
            into.Clear();
            if (mem == null || max <= 0) return;
            var ranked = new List<SocialMemoryEntry>(16);
            Collect(mem.GetToward(excavId, engId), ranked);
            Collect(mem.GetToward(engId, excavId), ranked);
            ranked.Sort((a, b) =>
            {
                int c = b.Strength.CompareTo(a.Strength);
                return c != 0 ? c : b.GameTime.CompareTo(a.GameTime);
            });
            for (int i = 0; i < ranked.Count && into.Count < max; i++)
                into.Add(ranked[i]);
        }

        static void Collect(IReadOnlyList<SocialMemoryEntry> src, List<SocialMemoryEntry> dst)
        {
            if (src == null) return;
            for (int i = 0; i < src.Count; i++)
            {
                var e = src[i];
                if (e == null || e.Strength < 0.25f) continue;
                if (!AffectsCoop(e.Type)) continue;
                dst.Add(e);
            }
        }

        static bool AffectsCoop(SocialMemoryType t) =>
            t == SocialMemoryType.HelpedMe
            || t == SocialMemoryType.SupportedMe
            || t == SocialMemoryType.WorkedWellTogether
            || t == SocialMemoryType.SharedSuccess
            || t == SocialMemoryType.TookMySide
            || t == SocialMemoryType.LetMeDown
            || t == SocialMemoryType.BlamedMe
            || t == SocialMemoryType.InsultedMe
            || t == SocialMemoryType.FailedTogether
            || t == SocialMemoryType.HurtBy
            || t == SocialMemoryType.WitnessedViolence
            || t == SocialMemoryType.KilledBy
            || t == SocialMemoryType.WitnessedDeath;
    }
}
