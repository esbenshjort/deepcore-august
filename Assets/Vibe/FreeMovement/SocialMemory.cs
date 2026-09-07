using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>V1 social memory kinds — directional observer→target.</summary>
    public enum SocialMemoryType : byte
    {
        HelpedMe = 0,
        LetMeDown = 1,
        SupportedMe = 2,
        BlamedMe = 3,
        WorkedWellTogether = 4,
        FailedTogether = 5,
        SharedSuccess = 6,
        SharedHardship = 7,
        InsultedMe = 8,
        Apologized = 9,
        TookMySide = 10,
        /// <summary>Observer was hurt by Target in a fight.</summary>
        HurtBy = 11,
        /// <summary>Observer saw Target in a serious fight.</summary>
        WitnessedViolence = 12,
        /// <summary>Observer was killed by Target (victim→killer).</summary>
        KilledBy = 13,
        /// <summary>Observer saw Target kill someone.</summary>
        WitnessedDeath = 14,
        // ——— Manager Communication V1 (observer = worker, target = ManagerId) ———
        ManagerPraisedMe = 15,
        ManagerPushedMeTooHard = 16,
        ManagerGaveMeRecovery = 17,
        ManagerCriticizedMe = 18,
        ManagerSupportedMe = 19,
        ManagerTookTheirSide = 20,
        ManagerStoppedFight = 21,
        ManagerIgnoredConflict = 22,
        // ——— Debris / Collapse V1 ———
        SurvivedCollapse = 23,
        WasTrapped = 24,
        RescuedByWorker = 25,
        FailedToReachWorker = 26,
        WitnessedSeriousAccident = 27,
    }

    /// <summary>Persistence weight — Major survives longest; Ordinary decays fastest.</summary>
    public enum SocialMemorySignificance : byte
    {
        Ordinary = 0,
        Significant = 1,
        Major = 2,
    }

    /// <summary>One directional memory entry (Observer remembers Target).</summary>
    [Serializable]
    public sealed class SocialMemoryEntry
    {
        public SocialMemoryType Type;
        public float Strength; // 0..1
        public float GameTime;
        public SocialContext Context;
        public int ObserverId;
        public int TargetId;
        /// <summary>Loose encounter fingerprint (shift + ids + action) — not a live object ref.</summary>
        public string SourceRef;
        public SocialMemorySignificance Significance;

        /// <summary>Compat: true when Significance is Major.</summary>
        public bool Major
        {
            get => Significance == SocialMemorySignificance.Major;
            set
            {
                if (value) Significance = SocialMemorySignificance.Major;
                else if (Significance == SocialMemorySignificance.Major)
                    Significance = SocialMemorySignificance.Significant;
            }
        }

        public float AgeHours(float nowGameHours) =>
            Mathf.Max(0f, nowGameHours - GameTime);
    }

    /// <summary>
    /// Persistent directional social memory store keyed ObserverWorkerId → TargetWorkerId.
    /// Does not alter Trust/Warmth/Hostility math.
    /// </summary>
    public sealed class SocialMemoryStore
    {
        public const int MaxPerTarget = 12;
        public const float MeaningfulRelAbs = 1.0f;
        public const float MeaningfulFrAbs = 2.0f;
        public const float MeaningfulMoAbs = 1.4f;
        public const float SignificantStrengthMin = 0.50f;
        public const float MajorStrengthMin = 0.65f;

        /// <summary>Decay / game-hour by significance (ordinary decays fastest).</summary>
        public const float DecayOrdinary = 0.022f;
        public const float DecaySignificant = 0.006f;
        public const float DecayMajor = 0.0012f;

        /// <summary>key: observer&lt;&lt;32|target → list newest/strongest retained.</summary>
        readonly Dictionary<long, List<SocialMemoryEntry>> _byPair = new(64);

        static long Key(int observer, int target) =>
            ((long)observer << 32) | (uint)target;

        public int PairCount => _byPair.Count;

        public int TotalEntries
        {
            get
            {
                int n = 0;
                foreach (var kv in _byPair) n += kv.Value.Count;
                return n;
            }
        }

        public void Clear() => _byPair.Clear();

        /// <summary>DEV: clear both directions for a person pair (preset testing).</summary>
        public void ClearPair(int workerA, int workerB)
        {
            if (workerA <= 0 || workerB <= 0 || workerA == workerB) return;
            _byPair.Remove(Key(workerA, workerB));
            _byPair.Remove(Key(workerB, workerA));
        }

        public IReadOnlyList<SocialMemoryEntry> GetToward(int observerId, int targetId)
        {
            if (!_byPair.TryGetValue(Key(observerId, targetId), out var list))
                return Array.Empty<SocialMemoryEntry>();
            return list;
        }

        public void Add(SocialMemoryEntry entry)
        {
            if (entry == null || entry.ObserverId <= 0) return;
            bool mgrTarget = entry.TargetId == ManagerRelationshipStore.ManagerId;
            if (!mgrTarget && entry.TargetId <= 0) return;
            if (!mgrTarget && entry.ObserverId == entry.TargetId) return;
            entry.Strength = Mathf.Clamp01(entry.Strength);
            long k = Key(entry.ObserverId, entry.TargetId);
            if (!_byPair.TryGetValue(k, out var list))
            {
                list = new List<SocialMemoryEntry>(MaxPerTarget);
                _byPair[k] = list;
            }

            // Merge same type: keep stronger / refresh time; promote significance
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Type != entry.Type) continue;
                if (entry.Strength >= list[i].Strength)
                {
                    list[i].Strength = entry.Strength;
                    list[i].GameTime = entry.GameTime;
                    list[i].Context = entry.Context;
                    list[i].SourceRef = entry.SourceRef;
                    if (entry.Significance > list[i].Significance)
                        list[i].Significance = entry.Significance;
                }
                else if (entry.GameTime > list[i].GameTime)
                {
                    // Weaker but newer: slight bump toward new
                    list[i].Strength = Mathf.Clamp01(list[i].Strength * 0.85f + entry.Strength * 0.25f);
                    list[i].GameTime = entry.GameTime;
                    list[i].Context = entry.Context;
                    list[i].SourceRef = entry.SourceRef;
                    if (entry.Significance > list[i].Significance)
                        list[i].Significance = entry.Significance;
                }
                Trim(list);
                return;
            }

            list.Add(entry);
            Trim(list);
            if (entry.Significance == SocialMemorySignificance.Major)
                NicknameEvidenceStore.Instance.ObserveMajorSocialMemory(entry);
        }

        /// <summary>Audit/DEV: append without type-merge so retention cap can be exercised.</summary>
        public void AuditForceAdd(SocialMemoryEntry entry)
        {
            if (entry == null || entry.ObserverId <= 0 || entry.TargetId <= 0) return;
            entry.Strength = Mathf.Clamp01(entry.Strength);
            long k = Key(entry.ObserverId, entry.TargetId);
            if (!_byPair.TryGetValue(k, out var list))
            {
                list = new List<SocialMemoryEntry>(MaxPerTarget + 4);
                _byPair[k] = list;
            }
            list.Add(entry);
            Trim(list);
        }

        static void Trim(List<SocialMemoryEntry> list)
        {
            if (list.Count <= MaxPerTarget) return;
            // Prefer higher significance, then strength, then recency
            list.Sort((a, b) =>
            {
                int s = ((byte)b.Significance).CompareTo((byte)a.Significance);
                if (s != 0) return s;
                int c = b.Strength.CompareTo(a.Strength);
                if (c != 0) return c;
                return b.GameTime.CompareTo(a.GameTime);
            });
            while (list.Count > MaxPerTarget)
                list.RemoveAt(list.Count - 1);
        }

        /// <summary>Significance-weighted decay. Major persists much longer; ordinary fades.</summary>
        public void TickDecay(float gameHoursDelta)
        {
            if (gameHoursDelta <= 0f) return;
            foreach (var kv in _byPair)
            {
                var list = kv.Value;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var e = list[i];
                    float rate = e.Significance switch
                    {
                        SocialMemorySignificance.Major => DecayMajor,
                        SocialMemorySignificance.Significant => DecaySignificant,
                        _ => DecayOrdinary,
                    };
                    e.Strength = Mathf.Max(0f, e.Strength - rate * gameHoursDelta);
                    // Majors never purge from decay alone; significant/ordinary may.
                    if (e.Strength < 0.04f && e.Significance != SocialMemorySignificance.Major)
                        list.RemoveAt(i);
                }
            }
        }

        public void GetStrongest(int observerId, int targetId, List<SocialMemoryEntry> into, int max)
        {
            into.Clear();
            var src = GetToward(observerId, targetId);
            for (int i = 0; i < src.Count; i++)
                into.Add(src[i]);
            into.Sort((a, b) =>
            {
                int s = ((byte)b.Significance).CompareTo((byte)a.Significance);
                if (s != 0) return s;
                int c = b.Strength.CompareTo(a.Strength);
                return c != 0 ? c : b.GameTime.CompareTo(a.GameTime);
            });
            while (into.Count > max)
                into.RemoveAt(into.Count - 1);
        }

        public static SocialMemorySignificance ClassifySignificance(float strength, bool forceMajor)
        {
            if (forceMajor || strength >= MajorStrengthMin)
                return SocialMemorySignificance.Major;
            if (strength >= SignificantStrengthMin)
                return SocialMemorySignificance.Significant;
            return SocialMemorySignificance.Ordinary;
        }
    }

    /// <summary>
    /// Maps meaningful SocialEncounterLog outcomes → directional memories.
    /// Skips trivial encounters. Does not mutate relation math.
    /// </summary>
    public static class SocialMemoryRecorder
    {
        public static int LastRecordPassAdds { get; private set; }

        public static void Record(SocialMemoryStore store, SocialEncounterLog log, float gameHours)
        {
            LastRecordPassAdds = 0;
            if (store == null || log == null) return;
            if (!IsMeaningful(log)) return;

            string src = $"S{log.ShiftIndex}:{log.InitiatorId}>{log.TargetId}:{log.Action}/{log.Response}";
            int before = store.TotalEntries;

            // Bonded complain → shared hardship both ways
            bool bondComplain = log.Action == SocialAction.Complain
                && (log.Response == SocialResponse.Agree || log.Response == SocialResponse.Accept)
                && (log.ActionSuccess || log.Context == SocialContext.SharedProblem);

            if (bondComplain)
            {
                Add(store, log.InitiatorId, log.TargetId, SocialMemoryType.SharedHardship,
                    StrengthFrom(log, 0.75f), gameHours, log.Context, src, major: true);
                Add(store, log.TargetId, log.InitiatorId, SocialMemoryType.SharedHardship,
                    StrengthFrom(log, 0.7f), gameHours, log.Context, src, major: true);
                if (log.Response == SocialResponse.Agree || log.Response == SocialResponse.Accept)
                {
                    Add(store, log.InitiatorId, log.TargetId, SocialMemoryType.TookMySide,
                        StrengthFrom(log, 0.55f), gameHours, log.Context, src, major: false);
                    Add(store, log.TargetId, log.InitiatorId, SocialMemoryType.SupportedMe,
                        StrengthFrom(log, 0.55f), gameHours, log.Context, src, major: false);
                }
                LastRecordPassAdds = store.TotalEntries - before;
                return;
            }

            switch (log.Action)
            {
                case SocialAction.Encourage:
                    if (log.ActionSuccess
                        && (log.Response == SocialResponse.Accept || log.Response == SocialResponse.Agree))
                    {
                        Add(store, log.TargetId, log.InitiatorId, SocialMemoryType.SupportedMe,
                            StrengthFrom(log, 0.7f), gameHours, log.Context, src, major: true);
                        Add(store, log.TargetId, log.InitiatorId, SocialMemoryType.HelpedMe,
                            StrengthFrom(log, 0.55f), gameHours, log.Context, src, major: false);
                        Add(store, log.InitiatorId, log.TargetId, SocialMemoryType.WorkedWellTogether,
                            StrengthFrom(log, 0.45f), gameHours, log.Context, src, major: false);
                    }
                    else if (!log.ActionSuccess)
                    {
                        Add(store, log.TargetId, log.InitiatorId, SocialMemoryType.LetMeDown,
                            StrengthFrom(log, 0.5f), gameHours, log.Context, src, major: false);
                    }
                    break;

                case SocialAction.Joke:
                    if (log.ActionSuccess
                        && (log.Response == SocialResponse.Accept || log.Response == SocialResponse.Agree))
                    {
                        Add(store, log.InitiatorId, log.TargetId, SocialMemoryType.WorkedWellTogether,
                            StrengthFrom(log, 0.4f), gameHours, log.Context, src, major: false);
                        Add(store, log.TargetId, log.InitiatorId, SocialMemoryType.WorkedWellTogether,
                            StrengthFrom(log, 0.4f), gameHours, log.Context, src, major: false);
                    }
                    else if (log.Response == SocialResponse.Escalate || log.Response == SocialResponse.PushBack)
                    {
                        Add(store, log.TargetId, log.InitiatorId, SocialMemoryType.InsultedMe,
                            StrengthFrom(log, 0.55f), gameHours, log.Context, src, major: false);
                    }
                    break;

                case SocialAction.Connect:
                    if (log.ActionSuccess
                        && (log.Response == SocialResponse.Accept || log.Response == SocialResponse.Agree))
                    {
                        if (log.Context == SocialContext.RecentSuccess)
                        {
                            Add(store, log.InitiatorId, log.TargetId, SocialMemoryType.SharedSuccess,
                                StrengthFrom(log, 0.6f), gameHours, log.Context, src, major: false);
                            Add(store, log.TargetId, log.InitiatorId, SocialMemoryType.SharedSuccess,
                                StrengthFrom(log, 0.6f), gameHours, log.Context, src, major: false);
                        }
                        else
                        {
                            Add(store, log.InitiatorId, log.TargetId, SocialMemoryType.WorkedWellTogether,
                                StrengthFrom(log, 0.45f), gameHours, log.Context, src, major: false);
                            Add(store, log.TargetId, log.InitiatorId, SocialMemoryType.WorkedWellTogether,
                                StrengthFrom(log, 0.45f), gameHours, log.Context, src, major: false);
                        }
                    }
                    else if (log.Response == SocialResponse.Ignore && log.DeltaWarmthIT < -0.3f)
                    {
                        Add(store, log.InitiatorId, log.TargetId, SocialMemoryType.LetMeDown,
                            StrengthFrom(log, 0.4f), gameHours, log.Context, src, major: false);
                    }
                    break;

                case SocialAction.Complain:
                    if (log.Context == SocialContext.RecentFailure || log.Context == SocialContext.SharedProblem)
                    {
                        if (log.Response == SocialResponse.Ignore || log.Response == SocialResponse.Deflect)
                        {
                            Add(store, log.InitiatorId, log.TargetId, SocialMemoryType.LetMeDown,
                                StrengthFrom(log, 0.55f), gameHours, log.Context, src, major: false);
                        }
                        else if (!log.ActionSuccess && log.Response == SocialResponse.Escalate)
                        {
                            Add(store, log.InitiatorId, log.TargetId, SocialMemoryType.FailedTogether,
                                StrengthFrom(log, 0.5f), gameHours, log.Context, src, major: false);
                            Add(store, log.TargetId, log.InitiatorId, SocialMemoryType.FailedTogether,
                                StrengthFrom(log, 0.45f), gameHours, log.Context, src, major: false);
                        }
                    }
                    break;

                case SocialAction.Provoke:
                case SocialAction.Confront:
                    if (log.ActionSuccess
                        || log.Response == SocialResponse.Escalate
                        || log.Response == SocialResponse.PushBack)
                    {
                        Add(store, log.TargetId, log.InitiatorId, SocialMemoryType.InsultedMe,
                            StrengthFrom(log, 0.75f), gameHours, log.Context, src, major: true);
                        if (log.DeltaHostilityIT > 0.5f || log.DeltaHostilityTI > 0.5f)
                        {
                            Add(store, log.TargetId, log.InitiatorId, SocialMemoryType.BlamedMe,
                                StrengthFrom(log, 0.5f), gameHours, log.Context, src, major: false);
                        }
                    }
                    else if (log.Response == SocialResponse.Withdraw && !log.ActionSuccess)
                    {
                        // Soft walk-back reads as apology-ish from initiator's side
                        Add(store, log.TargetId, log.InitiatorId, SocialMemoryType.Apologized,
                            StrengthFrom(log, 0.4f), gameHours, log.Context, src, major: false);
                    }
                    break;
            }

            // Context-weighted extras when deltas are strong
                if (log.Context == SocialContext.RecentSuccess
                && RelAbs(log) >= SocialMemoryStore.MeaningfulRelAbs
                && log.DeltaWarmthIT + log.DeltaWarmthTI > 0.8f)
            {
                Add(store, log.InitiatorId, log.TargetId, SocialMemoryType.SharedSuccess,
                    StrengthFrom(log, 0.5f), gameHours, log.Context, src, major: false);
                Add(store, log.TargetId, log.InitiatorId, SocialMemoryType.SharedSuccess,
                    StrengthFrom(log, 0.5f), gameHours, log.Context, src, major: false);
            }

            if (log.Context == SocialContext.RecentFailure
                && RelAbs(log) >= SocialMemoryStore.MeaningfulRelAbs
                && (log.DeltaFrustrationInit + log.DeltaFrustrationTarget) > 1.5f)
            {
                Add(store, log.InitiatorId, log.TargetId, SocialMemoryType.FailedTogether,
                    StrengthFrom(log, 0.5f), gameHours, log.Context, src, major: false);
                Add(store, log.TargetId, log.InitiatorId, SocialMemoryType.FailedTogether,
                    StrengthFrom(log, 0.5f), gameHours, log.Context, src, major: false);
            }

            LastRecordPassAdds = Mathf.Max(0, store.TotalEntries - before);
        }

        public static bool IsMeaningful(SocialEncounterLog log)
        {
            if (log == null) return false;
            if (bondComplain(log)) return true;
            if (RelAbs(log) >= SocialMemoryStore.MeaningfulRelAbs) return true;
            if (Mathf.Abs(log.DeltaFrustrationInit) + Mathf.Abs(log.DeltaFrustrationTarget)
                >= SocialMemoryStore.MeaningfulFrAbs) return true;
            if (Mathf.Abs(log.DeltaMoraleInit) + Mathf.Abs(log.DeltaMoraleTarget)
                >= SocialMemoryStore.MeaningfulMoAbs) return true;
            // Strong hostile / support actions even with modest deltas
            if (log.Action == SocialAction.Provoke || log.Action == SocialAction.Confront)
                return true;
            if (log.Action == SocialAction.Encourage && log.ActionSuccess
                && (log.Response == SocialResponse.Accept || log.Response == SocialResponse.Agree))
                return true;
            return false;
        }

        static bool bondComplain(SocialEncounterLog log) =>
            log.Action == SocialAction.Complain
            && (log.Response == SocialResponse.Agree || log.Response == SocialResponse.Accept)
            && (log.ActionSuccess || log.Context == SocialContext.SharedProblem);

        static float RelAbs(SocialEncounterLog log) =>
            Mathf.Abs(log.DeltaTrustIT) + Mathf.Abs(log.DeltaWarmthIT) + Mathf.Abs(log.DeltaHostilityIT)
            + Mathf.Abs(log.DeltaTrustTI) + Mathf.Abs(log.DeltaWarmthTI) + Mathf.Abs(log.DeltaHostilityTI);

        static float StrengthFrom(SocialEncounterLog log, float baseStr)
        {
            float boost = Mathf.Clamp01(RelAbs(log) / 6f) * 0.25f;
            float fr = Mathf.Clamp01(
                (Mathf.Abs(log.DeltaFrustrationInit) + Mathf.Abs(log.DeltaFrustrationTarget)) / 8f) * 0.15f;
            return Mathf.Clamp01(baseStr + boost + fr);
        }

        static void Add(
            SocialMemoryStore store,
            int observer, int target,
            SocialMemoryType type, float strength,
            float gameHours, SocialContext ctx, string src, bool major)
        {
            if (strength < 0.2f) return;
            store.Add(new SocialMemoryEntry
            {
                ObserverId = observer,
                TargetId = target,
                Type = type,
                Strength = strength,
                GameTime = gameHours,
                Context = ctx,
                SourceRef = src,
                Significance = SocialMemoryStore.ClassifySignificance(strength, major),
            });
        }
    }
}
