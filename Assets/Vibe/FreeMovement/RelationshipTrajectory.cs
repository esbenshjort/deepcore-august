using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Which relationship axis changed in a trajectory entry.</summary>
    public enum RelationshipAxis : byte
    {
        Trust = 0,
        Warmth = 1,
        Hostility = 2,
        Respect = 3,
    }

    /// <summary>One bounded directional axis change for DEV history.</summary>
    [Serializable]
    public sealed class RelationshipTrajectoryEntry
    {
        public int ObserverId;
        public int TargetId;
        public RelationshipAxis Axis;
        public float From;
        public float To;
        public float GameHours;
        public string Source;       // encounter fingerprint / reason
        public SocialMemoryType LinkedMemory; // optional; None-ish via Type when Source empty
        public bool HasLinkedMemory;

        public float Delta => To - From;
    }

    /// <summary>
    /// Read-only relationship trajectory — records meaningful axis changes for DEV.
    /// Does not create relationship effects. Bounded per directional pair.
    /// </summary>
    public sealed class RelationshipTrajectoryStore
    {
        public const int MaxPerPair = 16;
        public const float MinAbsDeltaAxes = 0.35f;   // T/W/H
        public const float MinAbsDeltaRespect = 0.8f;

        readonly Dictionary<long, List<RelationshipTrajectoryEntry>> _byPair = new(64);

        static long Key(int from, int to) => ((long)from << 32) | (uint)to;

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

        public IReadOnlyList<RelationshipTrajectoryEntry> GetToward(int observerId, int targetId)
        {
            if (!_byPair.TryGetValue(Key(observerId, targetId), out var list))
                return Array.Empty<RelationshipTrajectoryEntry>();
            return list;
        }

        public void GetRecent(int observerId, int targetId, List<RelationshipTrajectoryEntry> into, int max)
        {
            into.Clear();
            var src = GetToward(observerId, targetId);
            for (int i = 0; i < src.Count; i++)
                into.Add(src[i]);
            into.Sort((a, b) => b.GameHours.CompareTo(a.GameHours));
            while (into.Count > max)
                into.RemoveAt(into.Count - 1);
        }

        public void Record(
            int observerId, int targetId,
            RelationshipAxis axis, float from, float to,
            float gameHours, string source,
            SocialMemoryType linkedMemory = default, bool hasLinkedMemory = false)
        {
            if (observerId <= 0 || targetId <= 0 || observerId == targetId) return;
            float abs = Mathf.Abs(to - from);
            float min = axis == RelationshipAxis.Respect ? MinAbsDeltaRespect : MinAbsDeltaAxes;
            if (abs < min) return;

            long k = Key(observerId, targetId);
            if (!_byPair.TryGetValue(k, out var list))
            {
                list = new List<RelationshipTrajectoryEntry>(MaxPerPair);
                _byPair[k] = list;
            }

            list.Add(new RelationshipTrajectoryEntry
            {
                ObserverId = observerId,
                TargetId = targetId,
                Axis = axis,
                From = from,
                To = to,
                GameHours = gameHours,
                Source = source ?? "",
                LinkedMemory = linkedMemory,
                HasLinkedMemory = hasLinkedMemory,
            });

            while (list.Count > MaxPerPair)
                list.RemoveAt(0); // drop oldest
        }

        /// <summary>
        /// Snapshot axes before an encounter, then call CommitAfter with post values.
        /// </summary>
        public struct AxisSnapshot
        {
            public float Trust, Warmth, Hostility, Respect;
        }

        public static AxisSnapshot Capture(SocialDirectedRelation rel) =>
            rel == null
                ? default
                : new AxisSnapshot
                {
                    Trust = rel.Trust,
                    Warmth = rel.Warmth,
                    Hostility = rel.Hostility,
                    Respect = rel.Respect,
                };

        public void CommitDelta(
            int observerId, int targetId,
            AxisSnapshot before, SocialDirectedRelation after,
            float gameHours, string source,
            SocialMemoryStore memory = null)
        {
            if (after == null) return;
            SocialMemoryType linked = default;
            bool hasLink = false;
            if (memory != null)
            {
                var mems = memory.GetToward(observerId, targetId);
                if (mems.Count > 0)
                {
                    // Strongest/most recent memory as soft link
                    var best = mems[0];
                    for (int i = 1; i < mems.Count; i++)
                        if (mems[i].GameTime >= best.GameTime && mems[i].Strength >= best.Strength * 0.9f)
                            best = mems[i];
                    // Prefer memory written near this game time
                    for (int i = 0; i < mems.Count; i++)
                        if (Mathf.Abs(mems[i].GameTime - gameHours) < 0.05f
                            && mems[i].Strength >= best.Strength - 0.05f)
                        {
                            best = mems[i];
                            break;
                        }
                    linked = best.Type;
                    hasLink = true;
                }
            }

            Record(observerId, targetId, RelationshipAxis.Trust,
                before.Trust, after.Trust, gameHours, source, linked, hasLink);
            Record(observerId, targetId, RelationshipAxis.Warmth,
                before.Warmth, after.Warmth, gameHours, source, linked, hasLink);
            Record(observerId, targetId, RelationshipAxis.Hostility,
                before.Hostility, after.Hostility, gameHours, source, linked, hasLink);
            Record(observerId, targetId, RelationshipAxis.Respect,
                before.Respect, after.Respect, gameHours, source, linked, hasLink);
        }
    }
}
