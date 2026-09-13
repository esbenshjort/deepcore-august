using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Dialogue topic tags — gates contextual validity (presentation only).</summary>
    public enum SocialDialogueTopic : byte
    {
        General = 0,
        Work = 1,
        NarrowTunnel = 2,
        Darkness = 3,
        Confinement = 4,
        Depth = 5,
        Injury = 6,
        Exhaustion = 7,
        Food = 8,
        Camp = 9,
        Collapse = 10,
        Discovery = 11,
        Prospecting = 12,
        Supports = 13,
        Manager = 14,
        Rescue = 15,
        Trapped = 16,
        Humour = 17,
        Praise = 18,
        WeakPoint = 19,
        Strength = 20,
        Callback = 21,
        Toilet = 22,
        Geology = 23,
    }

    public enum SocialWeakPointKind : byte
    {
        None = 0,
        Competence = 1,
        Speed = 2,
        Fear = 3,
        StrengthPride = 4,
        TechnicalPride = 5,
        JobCriticism = 6,
        MistakeJokes = 7,
        Protectiveness = 8,
    }

    public enum SocialStrengthKind : byte
    {
        None = 0,
        SteadyUnderPressure = 1,
        TechnicalSkill = 2,
        PhysicalPower = 3,
        Insight = 4,
        Courage = 5,
        Care = 6,
        Reliability = 7,
    }

    /// <summary>Live situation flags — only comment on what is actually true.</summary>
    public struct SocialSituationFlags
    {
        public bool NarrowTunnel;
        public bool Dark;
        public bool HighConfinement;
        public bool Deep;
        public bool Injured;
        public bool Exhausted;
        public bool LongShift;
        public bool Trapped;
        public bool CollapseMemory;
        public bool RecentDiscovery;
        public bool BadFood;
        public bool GoodFood;
        public bool StomachTrouble;
        public bool ManagerPressure;

        public static SocialSituationFlags FromWorkers(
            WorkerRuntime a, WorkerRuntime b, Vector2 posA, FineTerrainWorld world,
            Vector2 campPos, MineInfrastructure infra, Vector2? excavPos)
        {
            var f = new SocialSituationFlags();
            if (a?.State != null)
            {
                f.Injured = a.State.Injury >= 20f;
                f.Exhausted = a.State.MentalFatigue >= 55f || a.State.IsResting;
                f.Trapped = a.State.TrappedFromCamp;
                f.HighConfinement = a.State.ClaustrophobicStress >= 45f;
                f.StomachTrouble = a.CampBody != null && a.CampBody.HasStomachUpset;
            }
            if (b?.State != null)
            {
                f.Injured = f.Injured || b.State.Injury >= 20f;
                f.Exhausted = f.Exhausted || b.State.MentalFatigue >= 55f;
                f.Trapped = f.Trapped || b.State.TrappedFromCamp;
                f.HighConfinement = f.HighConfinement || b.State.ClaustrophobicStress >= 45f;
            }

            if (world != null)
            {
                var cell = world.WorldToCell(posA);
                int clear = world.InBounds(cell.x, cell.y) && world.IsTunnelOpen(cell.x, cell.y)
                    ? world.Navigation.GetClearance(cell.x, cell.y) : 0;
                f.NarrowTunnel = clear > 0 && clear <= 2;
                var campCell = world.WorldToCell(campPos);
                f.Deep = campCell.y - cell.y >= 12;
            }

            float illum = TunnelIllumination.Sample01(posA, infra, excavPos, campPos);
            var band = TunnelIllumination.Classify(illum);
            f.Dark = band >= TunnelLightBand.Dark;

            if (ClaustrophobiaSystem.TryGetLastCauses(a != null ? a.WorkerId : 0, out var c))
            {
                if (c.WidthBand <= 2) f.NarrowTunnel = true;
                if (c.Light >= TunnelLightBand.Dark) f.Dark = true;
                if (c.DepthCells >= 10f) f.Deep = true;
            }

            return f;
        }

        public bool AllowsTopic(SocialDialogueTopic topic) => topic switch
        {
            SocialDialogueTopic.General or SocialDialogueTopic.Work or SocialDialogueTopic.Humour
                or SocialDialogueTopic.Praise or SocialDialogueTopic.Callback
                or SocialDialogueTopic.WeakPoint or SocialDialogueTopic.Strength => true,
            SocialDialogueTopic.NarrowTunnel or SocialDialogueTopic.Confinement => NarrowTunnel || HighConfinement,
            SocialDialogueTopic.Darkness => Dark,
            SocialDialogueTopic.Depth => Deep,
            SocialDialogueTopic.Injury => Injured,
            SocialDialogueTopic.Exhaustion => Exhausted || LongShift,
            SocialDialogueTopic.Trapped => Trapped,
            SocialDialogueTopic.Collapse => CollapseMemory,
            SocialDialogueTopic.Discovery => RecentDiscovery,
            SocialDialogueTopic.Food => BadFood || GoodFood,
            SocialDialogueTopic.Toilet => StomachTrouble,
            SocialDialogueTopic.Manager => ManagerPressure,
            SocialDialogueTopic.Camp => true,
            SocialDialogueTopic.Supports or SocialDialogueTopic.Geology or SocialDialogueTopic.Prospecting
                or SocialDialogueTopic.Rescue => true, // soft — gated elsewhere by memory/action
            _ => true,
        };
    }

    /// <summary>Recent dialogue suppression — exact lines, topics, exchange ids.</summary>
    public sealed class SocialDialogueHistory
    {
        public const int LineWindow = 28;
        public const int TopicWindow = 10;
        public const int ExchangeWindow = 16;
        public const float SoftSuppressHours = 2.5f;

        readonly Queue<int> _lineHashes = new(LineWindow + 2);
        readonly Queue<int> _exchangeIds = new(ExchangeWindow + 2);
        readonly Queue<SocialDialogueTopic> _topics = new(TopicWindow + 2);
        readonly HashSet<int> _lineSet = new(LineWindow + 2);
        readonly HashSet<int> _exSet = new(ExchangeWindow + 2);
        readonly Dictionary<int, float> _lineLastHours = new(64);

        public static SocialDialogueHistory Instance { get; } = new();

        public void Clear()
        {
            _lineHashes.Clear();
            _exchangeIds.Clear();
            _topics.Clear();
            _lineSet.Clear();
            _exSet.Clear();
            _lineLastHours.Clear();
        }

        public bool IsLineFresh(string line, float gameHours)
        {
            if (string.IsNullOrEmpty(line)) return false;
            int h = Hash(line);
            if (_lineSet.Contains(h)) return false;
            if (_lineLastHours.TryGetValue(h, out float last)
                && gameHours - last < SoftSuppressHours)
                return false;
            return true;
        }

        public bool IsExchangeFresh(int exchangeId) =>
            exchangeId <= 0 || !_exSet.Contains(exchangeId);

        public bool IsTopicFresh(SocialDialogueTopic topic)
        {
            int n = 0;
            foreach (var t in _topics)
                if (t == topic && ++n >= 2) return false;
            return true;
        }

        public void Remember(string line, int exchangeId, SocialDialogueTopic topic, float gameHours)
        {
            if (!string.IsNullOrEmpty(line))
            {
                int h = Hash(line);
                Push(_lineHashes, _lineSet, h, LineWindow);
                _lineLastHours[h] = gameHours;
            }
            if (exchangeId > 0)
                Push(_exchangeIds, _exSet, exchangeId, ExchangeWindow);
            _topics.Enqueue(topic);
            while (_topics.Count > TopicWindow) _topics.Dequeue();
        }

        static void Push(Queue<int> q, HashSet<int> set, int v, int max)
        {
            if (set.Contains(v)) return;
            q.Enqueue(v);
            set.Add(v);
            while (q.Count > max)
            {
                int old = q.Dequeue();
                set.Remove(old);
            }
        }

        public static int Hash(string s)
        {
            unchecked
            {
                int h = 23;
                for (int i = 0; i < s.Length; i++)
                    h = h * 31 + s[i];
                return h;
            }
        }
    }

    /// <summary>
    /// Person→person social knowledge from observed behaviour/memories — not invented biography.
    /// </summary>
    public sealed class SocialPersonKnowledge
    {
        public struct Node
        {
            public SocialWeakPointKind Weak;
            public float WeakStrength;
            public SocialStrengthKind Strength;
            public float StrengthScore;
            public float LastHours;
        }

        readonly Dictionary<long, Node> _map = new(64);
        public static SocialPersonKnowledge Instance { get; } = new();

        public void Clear() => _map.Clear();

        static long Key(int observer, int subject) => ((long)observer << 32) ^ (uint)subject;

        public void ObserveMemory(SocialMemoryEntry e, float gameHours)
        {
            if (e == null || e.ObserverId <= 0 || e.TargetId <= 0) return;
            long k = Key(e.ObserverId, e.TargetId);
            _map.TryGetValue(k, out var n);
            n.LastHours = gameHours;

            switch (e.Type)
            {
                case SocialMemoryType.InsultedMe:
                case SocialMemoryType.BlamedMe:
                    BumpWeak(ref n, SocialWeakPointKind.Competence, e.Strength * 0.55f);
                    BumpWeak(ref n, SocialWeakPointKind.JobCriticism, e.Strength * 0.35f);
                    break;
                case SocialMemoryType.LetMeDown:
                case SocialMemoryType.FailedTogether:
                    BumpWeak(ref n, SocialWeakPointKind.MistakeJokes, e.Strength * 0.5f);
                    BumpWeak(ref n, SocialWeakPointKind.Speed, e.Strength * 0.25f);
                    break;
                case SocialMemoryType.WasTrapped:
                case SocialMemoryType.SurvivedCollapse:
                    BumpWeak(ref n, SocialWeakPointKind.Fear, e.Strength * 0.6f);
                    if (e.Type == SocialMemoryType.SurvivedCollapse)
                    {
                        BumpStrength(ref n, SocialStrengthKind.Courage, e.Strength * 0.45f);
                        BumpStrength(ref n, SocialStrengthKind.SteadyUnderPressure, e.Strength * 0.3f);
                        NicknameEvidenceStore.Instance.ObserveBehaviour(e.TargetId,
                            NicknameEvidenceKind.NarrowPassageNerve, 0.55f, gameHours, "SurvivedCollapse");
                        NicknameEvidenceStore.Instance.ObserveBehaviour(e.TargetId,
                            NicknameEvidenceKind.SharedEmergency, 0.4f, gameHours, "SurvivedCollapse");
                    }
                    break;
                case SocialMemoryType.HelpedMe:
                case SocialMemoryType.SupportedMe:
                case SocialMemoryType.RescuedByWorker:
                    BumpStrength(ref n, SocialStrengthKind.Care, e.Strength * 0.7f);
                    BumpStrength(ref n, SocialStrengthKind.Reliability, e.Strength * 0.4f);
                    break;
                case SocialMemoryType.SharedSuccess:
                case SocialMemoryType.WorkedWellTogether:
                    BumpStrength(ref n, SocialStrengthKind.Reliability, e.Strength * 0.55f);
                    break;
                case SocialMemoryType.SharedHardship:
                    BumpStrength(ref n, SocialStrengthKind.SteadyUnderPressure, e.Strength * 0.65f);
                    break;
                case SocialMemoryType.TookMySide:
                    BumpStrength(ref n, SocialStrengthKind.Care, e.Strength * 0.5f);
                    break;
            }

            _map[k] = n;
        }

        public void ObserveConfinementFear(int observerId, int subjectId, float stress, float gameHours)
        {
            if (observerId <= 0 || subjectId <= 0 || stress < 70f) return;
            long k = Key(observerId, subjectId);
            _map.TryGetValue(k, out var n);
            BumpWeak(ref n, SocialWeakPointKind.Fear, 0.22f);
            n.LastHours = gameHours;
            _map[k] = n;
            NicknameEvidenceStore.Instance.ObserveBehaviour(subjectId, NicknameEvidenceKind.FearShown,
                0.55f, gameHours, "ConfinementFear");
        }

        public void ObserveSteadyDig(int observerId, int subjectId, float gameHours)
        {
            if (observerId <= 0 || subjectId <= 0) return;
            long k = Key(observerId, subjectId);
            _map.TryGetValue(k, out var n);
            BumpStrength(ref n, SocialStrengthKind.SteadyUnderPressure, 0.18f);
            BumpStrength(ref n, SocialStrengthKind.Courage, 0.12f);
            n.LastHours = gameHours;
            _map[k] = n;
            NicknameEvidenceStore.Instance.ObserveBehaviour(subjectId, NicknameEvidenceKind.NarrowPassageNerve,
                0.45f, gameHours, "SteadyDig");
        }

        public void ObserveDiscovery(int observerId, int subjectId, float gameHours)
        {
            long k = Key(observerId, subjectId);
            _map.TryGetValue(k, out var n);
            BumpStrength(ref n, SocialStrengthKind.Insight, 0.35f);
            n.LastHours = gameHours;
            _map[k] = n;
            NicknameEvidenceStore.Instance.ObserveBehaviour(subjectId, NicknameEvidenceKind.TechnicalReputation,
                0.7f, gameHours, "Discovery");
        }

        /// <summary>Repeated heavy haul / physical reliability — evidence only.</summary>
        public void ObservePhysicalWork(int observerId, int subjectId, float gameHours)
        {
            if (observerId <= 0 || subjectId <= 0) return;
            long k = Key(observerId, subjectId);
            _map.TryGetValue(k, out var n);
            BumpStrength(ref n, SocialStrengthKind.PhysicalPower, 0.22f);
            BumpStrength(ref n, SocialStrengthKind.Reliability, 0.1f);
            n.LastHours = gameHours;
            _map[k] = n;
            NicknameEvidenceStore.Instance.ObserveBehaviour(subjectId, NicknameEvidenceKind.PhysicalReputation,
                0.5f, gameHours, "HeavyHaul");
        }

        /// <summary>Running-joke / callback fuel from dialogue — evidence only, no nickname.</summary>
        public void ObserveRunningJokeFuel(int subjectId, float gameHours, string source = "Callback")
        {
            if (subjectId <= 0) return;
            NicknameEvidenceStore.Instance.ObserveBehaviour(subjectId, NicknameEvidenceKind.RunningJoke,
                0.35f, gameHours, source);
        }

        static void BumpWeak(ref Node n, SocialWeakPointKind kind, float amt)
        {
            if (n.Weak == kind || n.Weak == SocialWeakPointKind.None || amt >= n.WeakStrength * 0.85f)
            {
                if (n.Weak != kind) { n.Weak = kind; n.WeakStrength = 0f; }
                n.WeakStrength = Mathf.Min(3f, n.WeakStrength + amt);
            }
        }

        static void BumpStrength(ref Node n, SocialStrengthKind kind, float amt)
        {
            if (n.Strength == kind || n.Strength == SocialStrengthKind.None || amt >= n.StrengthScore * 0.85f)
            {
                if (n.Strength != kind) { n.Strength = kind; n.StrengthScore = 0f; }
                n.StrengthScore = Mathf.Min(3f, n.StrengthScore + amt);
            }
        }

        public bool TryGet(int observerId, int subjectId, out Node node) =>
            _map.TryGetValue(Key(observerId, subjectId), out node);

        public bool KnowsWeakPoint(int observerId, int subjectId, out SocialWeakPointKind kind, out float str)
        {
            kind = SocialWeakPointKind.None;
            str = 0f;
            if (!TryGet(observerId, subjectId, out var n) || n.WeakStrength < 0.55f) return false;
            kind = n.Weak;
            str = n.WeakStrength;
            return kind != SocialWeakPointKind.None;
        }

        public bool KnowsStrength(int observerId, int subjectId, out SocialStrengthKind kind, out float str)
        {
            kind = SocialStrengthKind.None;
            str = 0f;
            if (!TryGet(observerId, subjectId, out var n) || n.StrengthScore < 0.55f) return false;
            kind = n.Strength;
            str = n.StrengthScore;
            return kind != SocialStrengthKind.None;
        }
    }
}
