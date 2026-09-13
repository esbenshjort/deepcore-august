using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Data-only nickname evidence kinds — no nickname generation yet.</summary>
    public enum NicknameEvidenceKind : byte
    {
        Discovery = 0,
        RepairRescue = 1,
        Injury = 2,
        ObstructionPersistence = 3,
        DifficultProgress = 4,
        MajorWorkSuccess = 5,
        SharedEmergency = 6,
        MajorSocial = 7,
        /// <summary>V2: repeated narrow-tunnel / confined courage.</summary>
        NarrowPassageNerve = 8,
        /// <summary>V2: visible fear / refusal under confinement.</summary>
        FearShown = 9,
        /// <summary>V2: technical / prospecting insight reputation.</summary>
        TechnicalReputation = 10,
        /// <summary>V2: physical/haul reputation.</summary>
        PhysicalReputation = 11,
        /// <summary>V2: running joke / embarrassing callback fuel.</summary>
        RunningJoke = 12,
    }

    [Serializable]
    public sealed class NicknameEvidenceEntry
    {
        public NicknameEvidenceKind Kind;
        public int Count;
        public float Strength; // accumulated, soft-capped
        public float LastGameHours;
        public string LastSource;

        public void Add(float weight, float gameHours, string source)
        {
            Count++;
            // Repetition matters with diminishing returns
            float gain = weight * (1f / (1f + Count * 0.12f));
            Strength = Mathf.Min(12f, Strength + gain);
            LastGameHours = gameHours;
            LastSource = source ?? LastSource;
        }
    }

    /// <summary>
    /// Person-keyed nickname evidence from real gameplay events.
    /// Observation only — no nicknames, no gameplay effects.
    /// </summary>
    public sealed class NicknameEvidenceStore
    {
        public const int MaxKindsShown = 5;
        public const float MinEventMagnitude = 2.5f;

        readonly Dictionary<int, Dictionary<NicknameEvidenceKind, NicknameEvidenceEntry>> _byWorker
            = new(16);

        public static NicknameEvidenceStore Instance { get; } = new();

        public void Clear() => _byWorker.Clear();

        public void ObserveEvent(WorkerStateEvent e)
        {
            if (e == null || e.WorkerId <= 0) return;
            if (e.Magnitude < MinEventMagnitude
                && e.EventType != WorkerStateEventType.Discovery
                && e.EventType != WorkerStateEventType.Injury)
                return;

            switch (e.EventType)
            {
                case WorkerStateEventType.Discovery:
                    Add(e.WorkerId, NicknameEvidenceKind.Discovery, 2.2f, e.GameHours, e.Source);
                    break;
                case WorkerStateEventType.EquipmentRecovered:
                    Add(e.WorkerId, NicknameEvidenceKind.RepairRescue, 1.8f, e.GameHours, e.Source);
                    if (e.RelatedWorkerId > 0)
                        Add(e.RelatedWorkerId, NicknameEvidenceKind.RepairRescue, 1.4f, e.GameHours, e.Source);
                    break;
                case WorkerStateEventType.Injury:
                    Add(e.WorkerId, NicknameEvidenceKind.Injury, Mathf.Clamp(e.Magnitude / 12f, 0.8f, 2.5f),
                        e.GameHours, e.Source);
                    break;
                case WorkerStateEventType.WorkBlocked:
                case WorkerStateEventType.RepeatedFailure:
                    if (e.Magnitude >= 4f)
                        Add(e.WorkerId, NicknameEvidenceKind.ObstructionPersistence,
                            Mathf.Clamp(e.Magnitude / 10f, 0.6f, 1.8f), e.GameHours, e.Source);
                    break;
                case WorkerStateEventType.ProgressSuccess:
                    if (e.Magnitude >= 5f || (e.Source != null && e.Source.IndexOf("Bedrock", StringComparison.OrdinalIgnoreCase) >= 0))
                        Add(e.WorkerId, NicknameEvidenceKind.DifficultProgress, 1.2f, e.GameHours, e.Source);
                    break;
                case WorkerStateEventType.MajorSuccess:
                    Add(e.WorkerId, NicknameEvidenceKind.MajorWorkSuccess, 2.0f, e.GameHours, e.Source);
                    break;
                case WorkerStateEventType.EquipmentProblem:
                    // Shared emergency flavor when related worker present
                    if (e.RelatedWorkerId > 0)
                    {
                        Add(e.WorkerId, NicknameEvidenceKind.SharedEmergency, 0.9f, e.GameHours, e.Source);
                        Add(e.RelatedWorkerId, NicknameEvidenceKind.SharedEmergency, 0.7f, e.GameHours, e.Source);
                    }
                    break;
            }
        }

        public void ObserveMajorSocialMemory(SocialMemoryEntry entry)
        {
            if (entry == null || entry.Significance != SocialMemorySignificance.Major) return;
            if (entry.Strength < 0.55f) return;
            Add(entry.ObserverId, NicknameEvidenceKind.MajorSocial, 1.1f, entry.GameTime,
                entry.Type.ToString());
            // Shared hardship / success also marks the target
            if (entry.Type == SocialMemoryType.SharedHardship
                || entry.Type == SocialMemoryType.SharedSuccess
                || entry.Type == SocialMemoryType.FailedTogether)
                Add(entry.TargetId, NicknameEvidenceKind.SharedEmergency, 0.8f, entry.GameTime,
                    entry.Type.ToString());
            if (entry.Type == SocialMemoryType.RescuedByWorker)
                Add(entry.TargetId, NicknameEvidenceKind.RepairRescue, 1.3f, entry.GameTime, "Rescue");
            if (entry.Type == SocialMemoryType.WasTrapped || entry.Type == SocialMemoryType.SurvivedCollapse)
                Add(entry.TargetId, NicknameEvidenceKind.FearShown, 0.9f, entry.GameTime, entry.Type.ToString());
        }

        /// <summary>V2: explicit evidence from observed behaviour (still no nickname generation).</summary>
        public void ObserveBehaviour(int workerId, NicknameEvidenceKind kind, float weight,
            float gameHours, string source)
        {
            Add(workerId, kind, weight, gameHours, source);
        }

        void Add(int workerId, NicknameEvidenceKind kind, float weight, float gameHours, string source)
        {
            if (workerId <= 0 || weight <= 0f) return;
            if (!_byWorker.TryGetValue(workerId, out var map))
            {
                map = new Dictionary<NicknameEvidenceKind, NicknameEvidenceEntry>(8);
                _byWorker[workerId] = map;
            }
            if (!map.TryGetValue(kind, out var e))
            {
                e = new NicknameEvidenceEntry { Kind = kind };
                map[kind] = e;
            }
            e.Add(weight, gameHours, source);
        }

        public void GetStrongest(int workerId, List<NicknameEvidenceEntry> into, int max)
        {
            into.Clear();
            if (!_byWorker.TryGetValue(workerId, out var map)) return;
            foreach (var kv in map)
                if (kv.Value.Strength >= 0.5f)
                    into.Add(kv.Value);
            into.Sort((a, b) =>
            {
                int c = b.Strength.CompareTo(a.Strength);
                return c != 0 ? c : b.Count.CompareTo(a.Count);
            });
            while (into.Count > max)
                into.RemoveAt(into.Count - 1);
        }

        public float StrengthOf(int workerId, NicknameEvidenceKind kind)
        {
            if (!_byWorker.TryGetValue(workerId, out var map)) return 0f;
            return map.TryGetValue(kind, out var e) ? e.Strength : 0f;
        }

        public int KindCount(int workerId) =>
            _byWorker.TryGetValue(workerId, out var map) ? map.Count : 0;
    }
}
