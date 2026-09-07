using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    // ═══════════════════════════════════════════════════════════════
    // SOCIAL CONFLICT — Arguments (R3) / Fights (R4) / Witnesses (R5)
    // Extends Social Aura; does not replace encounter resolver.
    // Timing uses game-hours only (never frame rate).
    // ═══════════════════════════════════════════════════════════════

    public enum SocialArgumentPhase : byte
    {
        None = 0,
        Opening = 1,
        Exchange = 2,
        Peak = 3,
        Resolving = 4,
        Resolved = 5,
    }

    public enum SocialArgumentOutcome : byte
    {
        None = 0,
        BacksDown = 1,
        MutualDisengage = 2,
        PartialResolution = 3,
        Apology = 4,
        GrudgeStrengthened = 5,
        RelationshipWorsens = 6,
        EscalateFurther = 7,
        FightBreaksOut = 8,
        CriticalInjury = 9,
        Death = 10,
    }

    public enum SocialFightSeverity : byte
    {
        None = 0,
        Scuffle = 1,
        Severe = 2,
        Critical = 3,
        Lethal = 4,
    }

    public enum SocialFightBeat : byte
    {
        Shove = 0,
        Grab = 1,
        Punch = 2,
        Retaliate = 3,
        BackAway = 4,
        BreakApart = 5,
    }

    public enum SocialWitnessAction : byte
    {
        Ignore = 0,
        VerbalIntervene = 1,
        SupportSomeone = 2,
        DeEscalate = 3,
        BreakUpFight = 4,
    }

    /// <summary>Central tunables — keep fights rare for healthy crews.</summary>
    public static class SocialConflictTuning
    {
        // Emergence (all substantially required; Frustration alone never enough)
        public const float FrustrationMin = 52f;
        public const float HostilityMin = 7.5f;
        public const float TrustMaxForArgue = -1.5f;
        public const float NegMemoryStrengthMin = 0.48f;
        public const float RecentHostileLookbackHours = 14f;

        // Argument cadence (game-hours)
        public const float ExchangeIntervalHours = 0.55f;
        public const float MaxArgumentDurationHours = 4.5f;
        public const int MaxExchanges = 6;
        public const float ArgumentCooldownAfterHours = 6f;
        public const float PairPressureWhileArguingMul = 0.15f;

        // Fight gate (severe unresolved Peak only)
        public const float FightHostilityMin = 12f;
        public const float FightFrustrationMin = 68f;
        public const float FightTrustMax = -4f;
        public const int FightBraveryMin = 13;
        public const int FightComposureMax = 9;
        public const int FightToleranceMax = 10;
        public const float FightChanceAtPeak = 0.14f;
        public const int FightMaxBeats = 5;
        public const float FightInjuryMinor = 8f;
        public const float FightInjuryModerate = 16f;

        // Severe → Critical → Lethal (rare; ForceLethal bypasses chance rolls only)
        public const float SevereHostilityMin = 14.5f;
        public const float SevereFrustrationMin = 78f;
        public const float SevereTrustMax = -6.5f;
        public const float SevereChanceGivenFight = 0.20f;
        public const float CriticalChanceGivenSevere = 0.24f;
        public const float LethalChanceGivenCritical = 0.09f;
        public const float LethalHostilityMin = 16.5f;
        public const float LethalTrustMax = -8f;
        public const float LethalRespectMaxBlock = 52f; // Max(Respect) ≥ this AND Hostility < 18 → block lethal
        public const float FightInjurySevereCap = 32f;
        public const float FightInjuryCriticalAmount = 58f;
        public const float FightInjuryLethalAmount = 100f;
        public const int LethalEmpathyMax = 8;
        public const int LethalBraveryMin = 14;
        public const int MurderousMajorNegMin = 3;

        // Witness
        public const float WitnessReachMul = 1.35f;
        public const int MaxWitnessesPerEvent = 3;
        public const float WitnessCooldownHours = 2.5f;

        // History ring
        public const int DevHistoryCap = 48;
    }

    [Serializable]
    public sealed class SocialArgumentSession
    {
        public int IdA;
        public int IdB;
        public SocialArgumentPhase Phase = SocialArgumentPhase.None;
        public int ExchangeCount;
        public float StartGameHours;
        public float LastBeatGameHours;
        public float NextBeatAtGameHours;
        public SocialArgumentOutcome LastOutcome;
        public string LastOutcomeTag = "";
        public bool FailedDeEscalation;
        public bool FightOccurred;
        public SocialFightSeverity FightSeverity;
        public bool LethalOccurred;
        public int KillerId;
        public int VictimId;
        public bool FailedIntervention;
        public int AggressorId; // who pushed hardest recently
        public readonly List<string> Tags = new(8);

        public bool IsActive =>
            Phase != SocialArgumentPhase.None && Phase != SocialArgumentPhase.Resolved;

        public bool Involves(int id) => id == IdA || id == IdB;

        public int Other(int id) => id == IdA ? IdB : IdA;

        public void PushTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return;
            Tags.Add(tag);
            while (Tags.Count > 8) Tags.RemoveAt(0);
            LastOutcomeTag = tag;
        }
    }

    /// <summary>One resolved conflict beat (argument exchange, fight beat, or witness act).</summary>
    [Serializable]
    public sealed class SocialConflictEvent
    {
        public float GameHours;
        public int WorkerA;
        public int WorkerB;
        public int SpeakerId;
        public int ListenerId;
        public SocialArgumentPhase Phase;
        public SocialArgumentOutcome Outcome;
        public SocialFightBeat FightBeat;
        public bool IsFight;
        public bool IsWitness;
        public int WitnessId;
        public SocialWitnessAction WitnessAction;
        public int WitnessSupportTargetId;
        public bool RollSuccess;
        public int D20;
        public int DC;
        public string Line = "";
        public string Why = "";
        public float DeltaFrustrationA;
        public float DeltaFrustrationB;
        public float DeltaMoraleA;
        public float DeltaMoraleB;
        public float DeltaHostilityAB;
        public float DeltaHostilityBA;
        public float DeltaTrustAB;
        public float DeltaTrustBA;
        public float InjuryA;
        public float InjuryB;
        public SocialFightSeverity FightSeverity;
        public bool IsLethal;
        public int KillerId;
        public int VictimId;
        public bool IsSevereFight;
    }

    /// <summary>
    /// Pair-scoped conflict layer sitting on SocialAuraWorld.
    /// Call from live tick: divert active pairs; TryEmerge after hostile encounters.
    /// </summary>
    public sealed class SocialConflictSystem
    {
        readonly Dictionary<long, SocialArgumentSession> _sessions = new(16);
        readonly Dictionary<long, float> _pairCooldownUntil = new(16);
        readonly Dictionary<int, float> _witnessCooldownUntil = new(16);
        readonly List<SocialConflictEvent> _history = new(SocialConflictTuning.DevHistoryCap);
        readonly List<SocialConflictEvent> _pendingPresent = new(8);

        public IReadOnlyList<SocialConflictEvent> DevHistory => _history;
        public IReadOnlyList<SocialConflictEvent> PendingPresent => _pendingPresent;
        public int ActiveArgumentCount
        {
            get
            {
                int n = 0;
                foreach (var kv in _sessions)
                    if (kv.Value != null && kv.Value.IsActive) n++;
                return n;
            }
        }
        public int TotalArgumentsStarted { get; private set; }
        public int TotalFights { get; private set; }
        public int TotalSevereFights { get; private set; }
        public int TotalCriticalInjuries { get; private set; }
        public int TotalDeaths { get; private set; }
        public int TotalWitnessActs { get; private set; }

        public void ClearPendingPresent() => _pendingPresent.Clear();

        public SocialArgumentSession GetSession(int a, int b)
        {
            long k = PairKey(a, b);
            return _sessions.TryGetValue(k, out var s) ? s : null;
        }

        public bool HasActiveArgument(int a, int b)
        {
            var s = GetSession(a, b);
            return s != null && s.IsActive;
        }

        public bool WorkerInActiveArgument(int id)
        {
            foreach (var kv in _sessions)
            {
                var s = kv.Value;
                if (s != null && s.IsActive && s.Involves(id)) return true;
            }
            return false;
        }

        /// <summary>First active argument/fight session, or null.</summary>
        public SocialArgumentSession FindAnyActiveSession()
        {
            foreach (var kv in _sessions)
            {
                var s = kv.Value;
                if (s != null && s.IsActive) return s;
            }
            return null;
        }

        public SocialArgumentSession FindActiveSessionForWorker(int id)
        {
            if (id <= 0) return null;
            foreach (var kv in _sessions)
            {
                var s = kv.Value;
                if (s != null && s.IsActive && s.Involves(id)) return s;
            }
            return null;
        }

        /// <summary>
        /// Manager Communication V1 — player intervene. Does not guarantee success.
        /// On success with ResolveConflict, forces MutualDisengage (same as witness success).
        /// </summary>
        public SocialConflictEvent TryManagerIntervene(
            SocialAuraWorld world,
            SocialArgumentSession session,
            float gameHours,
            bool successResolve,
            string whyTag)
        {
            if (world == null || session == null || !session.IsActive) return null;
            session.PushTag(whyTag ?? "MANAGER_INTERVENE");
            if (!successResolve)
            {
                session.FailedIntervention = true;
                var failEv = new SocialConflictEvent
                {
                    GameHours = gameHours,
                    WorkerA = session.IdA,
                    WorkerB = session.IdB,
                    SpeakerId = session.IdA,
                    ListenerId = session.IdB,
                    Phase = session.Phase,
                    Outcome = session.LastOutcome,
                    IsFight = session.FightOccurred,
                    Line = "…",
                    Why = whyTag ?? "manager-intervene-failed",
                };
                PushHistory(failEv);
                _pendingPresent.Add(failEv);
                return failEv;
            }
            return ForceResolve(world, session, gameHours, SocialArgumentOutcome.MutualDisengage);
        }

        /// <summary>
        /// After a normal Social Aura encounter resolves: maybe open an argument.
        /// Frustration alone is never sufficient.
        /// </summary>
        public SocialConflictEvent TryEmergeFromEncounter(
            SocialAuraWorld world,
            SocialEncounterLog log,
            float gameHours)
        {
            if (world == null || log == null) return null;
            if (HasActiveArgument(log.InitiatorId, log.TargetId)) return null;

            long pk = PairKey(log.InitiatorId, log.TargetId);
            if (_pairCooldownUntil.TryGetValue(pk, out float until) && gameHours < until)
                return null;

            var a = world.Get(log.InitiatorId);
            var b = world.Get(log.TargetId);
            if (a == null || b == null) return null;
            if (a.State == null || b.State == null || !a.State.IsAlive || !b.State.IsAlive)
                return null;

            if (!MeetsEmergence(world, a, b, log, gameHours, out string whyNot))
                return null;

            var session = new SocialArgumentSession
            {
                IdA = Math.Min(a.Id, b.Id),
                IdB = Math.Max(a.Id, b.Id),
                Phase = SocialArgumentPhase.Opening,
                ExchangeCount = 0,
                StartGameHours = gameHours,
                LastBeatGameHours = gameHours,
                NextBeatAtGameHours = gameHours + SocialConflictTuning.ExchangeIntervalHours * 0.35f,
                AggressorId = log.InitiatorId,
            };
            session.PushTag("OPEN");
            _sessions[pk] = session;
            TotalArgumentsStarted++;

            // Opening beat immediately so the quarrel is visible
            return ResolveArgumentBeat(world, session, gameHours, openingForce: true);
        }

        /// <summary>
        /// While an argument is active, consume pair pressure into argument beats
        /// instead of normal encounters. Returns event if a beat fired.
        /// </summary>
        public SocialConflictEvent TickActivePair(
            SocialAuraWorld world,
            int idA,
            int idB,
            float gameHours,
            float pressureGain,
            SocialPairTransient pair)
        {
            var session = GetSession(idA, idB);
            if (session == null || !session.IsActive || world == null) return null;

            var aliveA = world.Get(session.IdA);
            var aliveB = world.Get(session.IdB);
            if (aliveA?.State == null || aliveB?.State == null
                || !aliveA.State.IsAlive || !aliveB.State.IsAlive)
            {
                ForceResolve(world, session, gameHours, SocialArgumentOutcome.MutualDisengage);
                return null;
            }

            // Soften normal pressure while arguing — beats are gated by game-hours.
            if (pair != null && pressureGain > 0f)
                pair.InteractionPressure += pressureGain * SocialConflictTuning.PairPressureWhileArguingMul;

            if (gameHours < session.NextBeatAtGameHours)
                return null;

            // Timeout → force resolve without fight
            if (gameHours - session.StartGameHours >= SocialConflictTuning.MaxArgumentDurationHours
                || session.ExchangeCount >= SocialConflictTuning.MaxExchanges)
            {
                return ForceResolve(world, session, gameHours, SocialArgumentOutcome.MutualDisengage);
            }

            return ResolveArgumentBeat(world, session, gameHours, openingForce: false);
        }

        /// <summary>Overnight / sleep: arguments cool off toward MutualDisengage.</summary>
        public void TickOvernight(SocialAuraWorld world, float gameHours, float gameHoursDelta)
        {
            if (world == null || gameHoursDelta <= 0f) return;
            var keys = new List<long>(_sessions.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                var s = _sessions[keys[i]];
                if (s == null || !s.IsActive) continue;
                // Sleep pulls toward disengage
                s.NextBeatAtGameHours = Mathf.Min(s.NextBeatAtGameHours, gameHours);
                ForceResolve(world, s, gameHours, SocialArgumentOutcome.MutualDisengage);
            }
        }

        public static bool MeetsEmergence(
            SocialAuraWorld world,
            SocialSimActor a,
            SocialSimActor b,
            SocialEncounterLog trigger,
            float gameHours,
            out string whyFail)
        {
            whyFail = "";
            if (a?.State == null || b?.State == null)
            {
                whyFail = "missing actors";
                return false;
            }
            if (!a.State.IsAlive || !b.State.IsAlive)
            {
                whyFail = "actor not alive";
                return false;
            }

            float frustGate = SocialConflictTuning.FrustrationMin - EarlyCrewPressure.ArgueFrustrationRelief;
            bool highFrust = a.State.Frustration >= frustGate
                             || b.State.Frustration >= frustGate;
            if (!highFrust)
            {
                whyFail = "frustration below threshold";
                return false;
            }

            var ab = world.Relation(a.Id, b.Id);
            var ba = world.Relation(b.Id, a.Id);
            float hostMax = Mathf.Max(ab.Hostility, ba.Hostility);
            float trustMin = Mathf.Min(ab.Trust, ba.Trust);
            float hostGate = SocialConflictTuning.HostilityMin - EarlyCrewPressure.ArgueHostilityRelief;
            if (hostMax < hostGate)
            {
                whyFail = $"hostility {hostMax:0.#} < {hostGate:0.#}";
                return false;
            }
            if (trustMin > SocialConflictTuning.TrustMaxForArgue)
            {
                whyFail = $"trust {trustMin:0.#} > {SocialConflictTuning.TrustMaxForArgue}";
                return false;
            }

            bool negMem = HasNegativeHistory(world.Memory, a.Id, b.Id)
                          || HasNegativeHistory(world.Memory, b.Id, a.Id);
            bool recentHostile = IsHostileTrigger(trigger)
                                 || HasRecentHostileInPair(world.Pair(a.Id, b.Id), gameHours);
            if (!negMem && !recentHostile)
            {
                whyFail = "no negative memory / recent hostile trigger";
                return false;
            }

            // High Composure + Tolerance on BOTH sides can still refuse to open
            int compA = a.Stats.Get(WorkerStatId.Composure);
            int tolA = a.Stats.Get(WorkerStatId.Tolerance);
            int compB = b.Stats.Get(WorkerStatId.Composure);
            int tolB = b.Stats.Get(WorkerStatId.Tolerance);
            if (compA >= 15 && tolA >= 14 && compB >= 15 && tolB >= 14
                && hostMax < SocialConflictTuning.HostilityMin + 3f)
            {
                whyFail = "mutual high composure/tolerance blocked";
                return false;
            }

            return true;
        }

        /// <summary>Audit helper: frustration alone must never be enough.</summary>
        public static bool FrustrationAloneInsufficient(
            SocialAuraWorld world, SocialSimActor a, SocialSimActor b, float gameHours)
        {
            // Synthetic "only frustration" — no hostility, trust ok, no memories, no trigger
            var fake = new SocialEncounterLog
            {
                InitiatorId = a.Id,
                TargetId = b.Id,
                Action = SocialAction.Joke,
                Response = SocialResponse.Accept,
                ActionSuccess = true,
            };
            return !MeetsEmergence(world, a, b, fake, gameHours, out _);
        }

        SocialConflictEvent ResolveArgumentBeat(
            SocialAuraWorld world,
            SocialArgumentSession session,
            float gameHours,
            bool openingForce)
        {
            var a = world.Get(session.IdA);
            var b = world.Get(session.IdB);
            if (a == null || b == null) return null;
            if (a.State == null || b.State == null || !a.State.IsAlive || !b.State.IsAlive)
                return null;

            a.RefreshExpression();
            b.RefreshExpression();

            // Advance phase
            if (session.Phase == SocialArgumentPhase.Opening && session.ExchangeCount > 0)
                session.Phase = SocialArgumentPhase.Exchange;
            if (session.ExchangeCount >= 2 && session.Phase == SocialArgumentPhase.Exchange)
                session.Phase = SocialArgumentPhase.Peak;
            if (session.ExchangeCount >= 4 && session.Phase == SocialArgumentPhase.Peak)
                session.Phase = SocialArgumentPhase.Resolving;

            if (openingForce && session.Phase == SocialArgumentPhase.None)
                session.Phase = SocialArgumentPhase.Opening;

            // Speaker: higher Bravery + Frustration + Hostility toward other
            float scoreA = ArgueInitScore(a, world.Relation(a.Id, b.Id));
            float scoreB = ArgueInitScore(b, world.Relation(b.Id, a.Id));
            bool aSpeaks = scoreA >= scoreB;
            if (Mathf.Abs(scoreA - scoreB) < 0.05f)
                aSpeaks = WorkerRoll.NextUnit() >= 0.5f;

            var speaker = aSpeaks ? a : b;
            var listener = aSpeaks ? b : a;
            var relSL = world.Relation(speaker.Id, listener.Id);
            var relLS = world.Relation(listener.Id, speaker.Id);

            // Move choice: Confront / Provoke / Complain (hostile) vs Connect (de-escalate attempt)
            var move = PickArgumentMove(speaker, listener, relSL, session);
            var moveRoll = ResolveArgueMoveRoll(speaker, listener, move, session);
            var resp = PickArgumentResponse(listener, speaker, relLS, move, moveRoll.Success, session);
            var respRoll = ResolveArgueResponseRoll(listener, speaker, resp, move, moveRoll.Success);

            ApplyArgumentConsequences(
                speaker, listener, relSL, relLS, session, move, resp,
                moveRoll.Success, respRoll.Success,
                out float dFrS, out float dFrL, out float dMoS, out float dMoL,
                out float dHoSL, out float dHoLS, out float dTrSL, out float dTrLS);

            session.ExchangeCount++;
            session.LastBeatGameHours = gameHours;
            session.NextBeatAtGameHours = gameHours + SocialConflictTuning.ExchangeIntervalHours;
            session.AggressorId = move == SocialAction.Connect ? session.AggressorId : speaker.Id;

            if (resp == SocialResponse.Escalate || resp == SocialResponse.PushBack)
                session.FailedDeEscalation = true;
            if (move == SocialAction.Connect && !moveRoll.Success)
                session.FailedDeEscalation = true;

            var outcome = ClassifyBeatOutcome(session, move, resp, moveRoll.Success, respRoll.Success);
            session.LastOutcome = outcome;
            session.PushTag(outcome.ToString());

            // Memories (reuse existing types)
            RecordArgumentMemories(world.Memory, speaker.Id, listener.Id, move, resp,
                moveRoll.Success, respRoll.Success, outcome, gameHours);

            string line = SocialConflictLineBank.PickArgumentLine(
                session.Phase, move, resp, moveRoll.Success, outcome);

            var ev = new SocialConflictEvent
            {
                GameHours = gameHours,
                WorkerA = session.IdA,
                WorkerB = session.IdB,
                SpeakerId = speaker.Id,
                ListenerId = listener.Id,
                Phase = session.Phase,
                Outcome = outcome,
                RollSuccess = moveRoll.Success,
                D20 = moveRoll.D20,
                DC = moveRoll.DC,
                Line = line,
                Why =
                    $"{session.Phase} x{session.ExchangeCount} {speaker.Name}->{listener.Name} " +
                    $"{move}/{resp} d20={moveRoll.D20}/{moveRoll.DC} → {outcome}",
                DeltaFrustrationA = speaker.Id == session.IdA ? dFrS : dFrL,
                DeltaFrustrationB = speaker.Id == session.IdB ? dFrS : dFrL,
                DeltaMoraleA = speaker.Id == session.IdA ? dMoS : dMoL,
                DeltaMoraleB = speaker.Id == session.IdB ? dMoS : dMoL,
                DeltaHostilityAB = speaker.Id == session.IdA ? dHoSL : dHoLS,
                DeltaHostilityBA = speaker.Id == session.IdA ? dHoLS : dHoSL,
                DeltaTrustAB = speaker.Id == session.IdA ? dTrSL : dTrLS,
                DeltaTrustBA = speaker.Id == session.IdA ? dTrLS : dTrSL,
            };

            // Peak: maybe escalate to fight (rare)
            if (session.Phase == SocialArgumentPhase.Peak
                && outcome == SocialArgumentOutcome.EscalateFurther
                && CanStartFight(world, session, a, b))
            {
                var fightEv = RunFight(world, session, a, b, gameHours);
                if (fightEv != null)
                {
                    PushHistory(ev);
                    _pendingPresent.Add(ev);
                    // Witnesses on fight
                    RunWitnesses(world, session, gameHours, fighting: true);
                    PushHistory(fightEv);
                    _pendingPresent.Add(fightEv);
                    return fightEv;
                }
            }

            // Terminal outcomes
            if (IsTerminal(outcome) || session.Phase == SocialArgumentPhase.Resolving
                && (outcome == SocialArgumentOutcome.Apology
                    || outcome == SocialArgumentOutcome.PartialResolution
                    || outcome == SocialArgumentOutcome.BacksDown
                    || outcome == SocialArgumentOutcome.MutualDisengage))
            {
                CloseSession(session, gameHours, outcome);
            }

            PushHistory(ev);
            _pendingPresent.Add(ev);

            // Witnesses may react to heated argument (not every beat)
            if (session.Phase == SocialArgumentPhase.Peak
                || outcome == SocialArgumentOutcome.EscalateFurther
                || outcome == SocialArgumentOutcome.GrudgeStrengthened)
            {
                RunWitnesses(world, session, gameHours, fighting: false);
            }

            return ev;
        }

        SocialConflictEvent ForceResolve(
            SocialAuraWorld world,
            SocialArgumentSession session,
            float gameHours,
            SocialArgumentOutcome outcome)
        {
            session.Phase = SocialArgumentPhase.Resolving;
            session.LastOutcome = outcome;
            session.PushTag("FORCE_" + outcome);
            CloseSession(session, gameHours, outcome);

            var a = world.Get(session.IdA);
            var b = world.Get(session.IdB);
            if (a != null && b != null && outcome == SocialArgumentOutcome.MutualDisengage)
            {
                var ab = world.Relation(a.Id, b.Id);
                var ba = world.Relation(b.Id, a.Id);
                float dTr = 0.2f, dWa = 0.1f, dHo = -0.4f;
                float dFr = -2f;
                // Weaker overnight recovery while unfamiliar
                float recover = EarlyCrewPressure.ConflictRecoveryMul;
                dTr *= EarlyCrewPressure.TrustGainMul;
                dWa *= EarlyCrewPressure.TrustGainMul;
                dHo *= recover; // less Hostility reduction when early
                dFr *= recover;
                ab.Add(dTr, dWa, dHo);
                ba.Add(dTr, dWa, dHo);
                a.State.Frustration = Mathf.Clamp(a.State.Frustration + dFr, 0f, 100f);
                b.State.Frustration = Mathf.Clamp(b.State.Frustration + dFr, 0f, 100f);
            }

            var ev = new SocialConflictEvent
            {
                GameHours = gameHours,
                WorkerA = session.IdA,
                WorkerB = session.IdB,
                SpeakerId = session.IdA,
                ListenerId = session.IdB,
                Phase = SocialArgumentPhase.Resolved,
                Outcome = outcome,
                Line = SocialConflictLineBank.PickOutcomeCloser(outcome),
                Why = $"force-resolve {outcome}",
            };
            PushHistory(ev);
            _pendingPresent.Add(ev);
            return ev;
        }

        void CloseSession(SocialArgumentSession session, float gameHours, SocialArgumentOutcome outcome)
        {
            session.Phase = SocialArgumentPhase.Resolved;
            session.LastOutcome = outcome;
            long pk = PairKey(session.IdA, session.IdB);
            _pairCooldownUntil[pk] = gameHours + SocialConflictTuning.ArgumentCooldownAfterHours;
        }

        public static long PairKey(int a, int b)
        {
            if (a > b) { int t = a; a = b; b = t; }
            return ((long)a << 32) | (uint)b;
        }

        static bool IsTerminal(SocialArgumentOutcome o) =>
            o == SocialArgumentOutcome.BacksDown
            || o == SocialArgumentOutcome.MutualDisengage
            || o == SocialArgumentOutcome.PartialResolution
            || o == SocialArgumentOutcome.Apology
            || o == SocialArgumentOutcome.FightBreaksOut
            || o == SocialArgumentOutcome.CriticalInjury
            || o == SocialArgumentOutcome.Death;

        // ─── Fight V1 ───────────────────────────────────────────────

        public static bool CanStartFight(
            SocialAuraWorld world,
            SocialArgumentSession session,
            SocialSimActor a,
            SocialSimActor b,
            bool forceFight = false)
        {
            if (session == null || !session.FailedDeEscalation) return false;
            if (a?.State == null || b?.State == null) return false;
            if (!a.State.IsAlive || !b.State.IsAlive) return false;

            var ab = world.Relation(a.Id, b.Id);
            var ba = world.Relation(b.Id, a.Id);
            float host = Mathf.Max(ab.Hostility, ba.Hostility);
            float trust = Mathf.Min(ab.Trust, ba.Trust);
            float fr = Mathf.Max(a.State.Frustration, b.State.Frustration);
            if (host < SocialConflictTuning.FightHostilityMin) return false;
            if (fr < SocialConflictTuning.FightFrustrationMin) return false;
            if (trust > SocialConflictTuning.FightTrustMax) return false;

            // Soul tendencies: at least one side hot-headed
            if (!IsHotSoul(a) && !IsHotSoul(b)) return false;

            // Meaningful negative history required
            if (!HasNegativeHistory(world.Memory, a.Id, b.Id)
                && !HasNegativeHistory(world.Memory, b.Id, a.Id))
                return false;

            // High Respect rivalry alone should not auto-fight — need trust/hostility already gated.
            // Force path / additional roll keeps fights rare in normal play.
            if (forceFight) return true;
            return WorkerRoll.NextUnit() < SocialConflictTuning.FightChanceAtPeak;
        }

        static bool IsHotSoul(SocialSimActor actor) =>
            actor != null
            && actor.Stats.Get(WorkerStatId.Bravery) >= SocialConflictTuning.FightBraveryMin
            && actor.Stats.Get(WorkerStatId.Composure) <= SocialConflictTuning.FightComposureMax
            && actor.Stats.Get(WorkerStatId.Tolerance) <= SocialConflictTuning.FightToleranceMax;

        SocialConflictEvent RunFight(
            SocialAuraWorld world,
            SocialArgumentSession session,
            SocialSimActor a,
            SocialSimActor b,
            float gameHours,
            bool forceFight = false,
            bool forceLethal = false)
        {
            if (a?.State == null || b?.State == null || !a.State.IsAlive || !b.State.IsAlive)
                return null;

            TotalFights++;
            session.FightOccurred = true;
            session.FightSeverity = SocialFightSeverity.Scuffle;
            session.Phase = SocialArgumentPhase.Peak;
            session.LastOutcome = SocialArgumentOutcome.FightBreaksOut;
            session.PushTag("FIGHT");

            int beats = 2 + (int)(WorkerRoll.NextUnit() * (SocialConflictTuning.FightMaxBeats - 1));
            beats = Mathf.Clamp(beats, 2, SocialConflictTuning.FightMaxBeats);
            if (forceLethal) beats = SocialConflictTuning.FightMaxBeats;

            float injA = 0f, injB = 0f;
            float dFrA = 0f, dFrB = 0f, dMoA = 0f, dMoB = 0f;
            float dHoAB = 0f, dHoBA = 0f, dTrAB = 0f, dTrBA = 0f;
            var sb = new StringBuilder(128);
            SocialFightBeat lastBeat = SocialFightBeat.Shove;
            bool lastOk = false;
            int lastD20 = 0, lastDc = 0;
            int actorId = session.AggressorId > 0 ? session.AggressorId : a.Id;
            int punchHits = 0;

            for (int i = 0; i < beats; i++)
            {
                var actor = world.Get(actorId) ?? a;
                var other = actor.Id == a.Id ? b : a;
                if (actor.State == null || !actor.State.IsAlive
                    || other.State == null || !other.State.IsAlive)
                    break;

                var beat = PickFightBeat(i, beats, actor, other);
                if (forceLethal && i < beats - 1 && beat != SocialFightBeat.Punch)
                    beat = SocialFightBeat.Punch;

                var roll = ResolveFightBeat(actor, other, beat);
                lastBeat = beat;
                lastOk = roll.Success;
                lastD20 = roll.D20;
                lastDc = roll.DC;
                sb.Append($"{beat}:{(roll.Success ? "OK" : "FAIL")} ");

                if (beat == SocialFightBeat.Punch && roll.Success)
                    punchHits++;

                ApplyFightBeat(actor, other, beat, roll.Success,
                    ref injA, ref injB, ref dFrA, ref dFrB, ref dMoA, ref dMoB,
                    a.Id, b.Id);

                // Swap unless BreakApart / BackAway succeeded
                if (beat == SocialFightBeat.BreakApart && roll.Success)
                    break;
                if (beat == SocialFightBeat.BackAway && roll.Success)
                    break;
                actorId = other.Id;
            }

            // Apply relation damage
            var ab = world.Relation(a.Id, b.Id);
            var ba = world.Relation(b.Id, a.Id);
            dHoAB += 2.2f;
            dHoBA += 2.2f;
            dTrAB -= 1.8f;
            dTrBA -= 1.8f;
            ab.Add(dTrAB, -1.2f, dHoAB);
            ba.Add(dTrBA, -1.2f, dHoBA);
            a.State.Frustration = Mathf.Clamp(a.State.Frustration + dFrA + 6f, 0f, 100f);
            b.State.Frustration = Mathf.Clamp(b.State.Frustration + dFrB + 6f, 0f, 100f);
            a.State.Morale = Mathf.Clamp(a.State.Morale + dMoA - 4f, 0f, 100f);
            b.State.Morale = Mathf.Clamp(b.State.Morale + dMoB - 4f, 0f, 100f);

            // Scuffle injury: still capped at FightInjuryModerate (audit: well below 100)
            ApplyFightInjury(a, injA, b.Id);
            ApplyFightInjury(b, injB, a.Id);

            // Major memories both ways
            RecordFightMemories(world.Memory, a.Id, b.Id, gameHours);

            var outcome = SocialArgumentOutcome.FightBreaksOut;
            TryEscalateFightSeverity(
                world, session, a, b, gameHours,
                ref injA, ref injB, punchHits, forceLethal, out outcome);

            CloseSession(session, gameHours, outcome);

            string line = outcome == SocialArgumentOutcome.Death
                ? SocialConflictLineBank.PickDeathLine(killerSpeaks: false)
                : outcome == SocialArgumentOutcome.CriticalInjury
                    ? SocialConflictLineBank.PickCriticalInjuryLine(victimSpeaks: true)
                    : SocialConflictLineBank.PickFightLine(lastBeat, lastOk);

            return new SocialConflictEvent
            {
                GameHours = gameHours,
                WorkerA = a.Id,
                WorkerB = b.Id,
                SpeakerId = actorId,
                ListenerId = actorId == a.Id ? b.Id : a.Id,
                Phase = SocialArgumentPhase.Resolved,
                Outcome = outcome,
                FightBeat = lastBeat,
                IsFight = true,
                RollSuccess = lastOk,
                D20 = lastD20,
                DC = lastDc,
                Line = line,
                Why =
                    $"{SeverityTag(session.FightSeverity)} beats={beats} [{sb}] " +
                    $"injA={injA:0.#} injB={injB:0.#}" +
                    (session.LethalOccurred
                        ? $" kill={session.KillerId}->{session.VictimId}"
                        : ""),
                DeltaFrustrationA = dFrA + 6f,
                DeltaFrustrationB = dFrB + 6f,
                DeltaMoraleA = dMoA - 4f,
                DeltaMoraleB = dMoB - 4f,
                DeltaHostilityAB = dHoAB,
                DeltaHostilityBA = dHoBA,
                DeltaTrustAB = dTrAB,
                DeltaTrustBA = dTrBA,
                InjuryA = injA,
                InjuryB = injB,
                FightSeverity = session.FightSeverity,
                IsLethal = session.LethalOccurred,
                KillerId = session.KillerId,
                VictimId = session.VictimId,
                IsSevereFight = session.FightSeverity >= SocialFightSeverity.Severe,
            };
        }

        void TryEscalateFightSeverity(
            SocialAuraWorld world,
            SocialArgumentSession session,
            SocialSimActor a,
            SocialSimActor b,
            float gameHours,
            ref float injA,
            ref float injB,
            int punchHits,
            bool forceLethal,
            out SocialArgumentOutcome outcome)
        {
            outcome = SocialArgumentOutcome.FightBreaksOut;
            if (world == null || session == null || a?.State == null || b?.State == null)
                return;

            // CanSevere: fight already happening + gates + hot soul + neg history + FailedDeEsc + chance/force
            bool canSevere = CanEscalateToSevere(world, session, a, b, forceLethal);
            if (!canSevere) return;

            session.FightSeverity = SocialFightSeverity.Severe;
            session.PushTag("SEVERE");
            TotalSevereFights++;

            // Additional injury toward severe cap (scuffle already applied at moderate)
            float addA = Mathf.Max(0f, SocialConflictTuning.FightInjurySevereCap * 0.55f);
            float addB = Mathf.Max(0f, SocialConflictTuning.FightInjurySevereCap * 0.45f);
            ApplyFightInjury(a, addA, b.Id, SocialConflictTuning.FightInjurySevereCap);
            ApplyFightInjury(b, addB, a.Id, SocialConflictTuning.FightInjurySevereCap);
            injA = Mathf.Min(SocialConflictTuning.FightInjurySevereCap, injA + addA);
            injB = Mathf.Min(SocialConflictTuning.FightInjurySevereCap, injB + addB);

            // CanCritical: severe + (high injury already or punch-heavy) + chance/force
            bool punchHeavy = punchHits >= 2 || forceLethal;
            bool highInj = Mathf.Max(a.State.Injury, b.State.Injury)
                           >= SocialConflictTuning.FightInjurySevereCap * 0.7f
                           || Mathf.Max(injA, injB) >= SocialConflictTuning.FightInjurySevereCap * 0.7f;
            bool canCritical = (highInj || punchHeavy)
                               && (forceLethal
                                   || WorkerRoll.NextUnit() < SocialConflictTuning.CriticalChanceGivenSevere);
            if (!canCritical) return;

            session.FightSeverity = SocialFightSeverity.Critical;
            session.PushTag("CRITICAL");
            TotalCriticalInjuries++;
            outcome = SocialArgumentOutcome.CriticalInjury;

            // Ensure NeedsCare: bump the worse-injured side (or both on force) to CareInjuryThreshold+
            SocialSimActor critVictim = a.State.Injury >= b.State.Injury ? a : b;
            SocialSimActor critOther = critVictim.Id == a.Id ? b : a;
            float need = WorkerState.CareInjuryThreshold + 2f - critVictim.State.Injury;
            if (need < SocialConflictTuning.FightInjuryCriticalAmount * 0.35f)
                need = SocialConflictTuning.FightInjuryCriticalAmount * 0.35f;
            ApplyFightInjury(critVictim, need, critOther.Id,
                SocialConflictTuning.FightInjuryCriticalAmount);
            if (critVictim.Id == a.Id) injA = Mathf.Max(injA, critVictim.State.Injury);
            else injB = Mathf.Max(injB, critVictim.State.Injury);

            // CanLethal: critical + combination gates + chance/force
            bool canLethal = CanLethalCombination(world, session, a, b)
                             && (forceLethal
                                 || WorkerRoll.NextUnit() < SocialConflictTuning.LethalChanceGivenCritical);
            if (!canLethal) return;

            // Victim = worse injured (or Forced VictimId if already set); Killer = other
            int victimId = session.VictimId > 0
                ? session.VictimId
                : (a.State.Injury >= b.State.Injury ? a.Id : b.Id);
            if (victimId != a.Id && victimId != b.Id)
                victimId = a.State.Injury >= b.State.Injury ? a.Id : b.Id;
            int killerId = session.KillerId > 0 && session.KillerId != victimId
                ? session.KillerId
                : (victimId == a.Id ? b.Id : a.Id);

            var victim = victimId == a.Id ? a : b;
            var killer = killerId == a.Id ? a : b;

            session.FightSeverity = SocialFightSeverity.Lethal;
            session.LethalOccurred = true;
            session.KillerId = killerId;
            session.VictimId = victimId;
            session.PushTag("LETHAL");
            session.PushTag("DEATH");
            TotalDeaths++;
            outcome = SocialArgumentOutcome.Death;

            ApplyFightInjury(victim, SocialConflictTuning.FightInjuryLethalAmount, killerId,
                SocialConflictTuning.FightInjuryLethalAmount);
            if (victim.Id == a.Id) injA = SocialConflictTuning.FightInjuryLethalAmount;
            else injB = SocialConflictTuning.FightInjuryLethalAmount;

            victim.State.MarkDead(gameHours, killerId);
            RecordDeathMemories(world, killerId, victimId, a, b, gameHours);
        }

        static bool CanEscalateToSevere(
            SocialAuraWorld world,
            SocialArgumentSession session,
            SocialSimActor a,
            SocialSimActor b,
            bool forceLethal)
        {
            if (!session.FightOccurred || !session.FailedDeEscalation) return false;
            if (a?.State == null || b?.State == null) return false;
            if (!a.State.IsAlive || !b.State.IsAlive) return false;

            var ab = world.Relation(a.Id, b.Id);
            var ba = world.Relation(b.Id, a.Id);
            float host = Mathf.Max(ab.Hostility, ba.Hostility);
            float trust = Mathf.Min(ab.Trust, ba.Trust);
            float fr = Mathf.Max(a.State.Frustration, b.State.Frustration);
            if (host < SocialConflictTuning.SevereHostilityMin) return false;
            if (fr < SocialConflictTuning.SevereFrustrationMin) return false;
            if (trust > SocialConflictTuning.SevereTrustMax) return false;
            if (!IsHotSoul(a) && !IsHotSoul(b)) return false;
            if (!HasNegativeHistory(world.Memory, a.Id, b.Id)
                && !HasNegativeHistory(world.Memory, b.Id, a.Id))
                return false;

            if (forceLethal) return true;
            return WorkerRoll.NextUnit() < SocialConflictTuning.SevereChanceGivenFight;
        }

        /// <summary>
        /// Full lethal gate (no chance roll). Frustration alone / Hostility alone never enough.
        /// High Respect rivalry blocks murder when Hostility &lt; 18.
        /// </summary>
        public static bool CanLethalCombination(
            SocialAuraWorld world,
            SocialArgumentSession session,
            SocialSimActor a,
            SocialSimActor b)
        {
            if (world == null || session == null || a?.State == null || b?.State == null)
                return false;
            if (!session.FightOccurred) return false;
            // Require Critical severity (progression Argument→Fight→Severe→Critical→Death)
            if (session.FightSeverity < SocialFightSeverity.Critical)
                return false;

            var ab = world.Relation(a.Id, b.Id);
            var ba = world.Relation(b.Id, a.Id);
            float host = Mathf.Max(ab.Hostility, ba.Hostility);
            float trust = Mathf.Min(ab.Trust, ba.Trust);
            float maxRespect = Mathf.Max(ab.Respect, ba.Respect);

            if (host < SocialConflictTuning.LethalHostilityMin) return false;
            if (trust > SocialConflictTuning.LethalTrustMax) return false;

            // Ordinary rivalry with high Respect must not kill
            if (maxRespect >= SocialConflictTuning.LethalRespectMaxBlock && host < 18f)
                return false;

            if (!HasMurderousHistory(world.Memory, a.Id, b.Id))
                return false;

            if (!(session.FailedIntervention || session.FailedDeEscalation))
                return false;

            bool lowEmp =
                a.Stats.Get(WorkerStatId.Empathy) <= SocialConflictTuning.LethalEmpathyMax
                || b.Stats.Get(WorkerStatId.Empathy) <= SocialConflictTuning.LethalEmpathyMax;
            bool highBrave =
                a.Stats.Get(WorkerStatId.Bravery) >= SocialConflictTuning.LethalBraveryMin
                || b.Stats.Get(WorkerStatId.Bravery) >= SocialConflictTuning.LethalBraveryMin;
            if (!lowEmp || !highBrave) return false;

            return true;
        }

        /// <summary>Audit: Hostility alone (even extreme) is never enough for death.</summary>
        public static bool HostilityAloneInsufficientForDeath(
            SocialAuraWorld world,
            SocialArgumentSession session,
            SocialSimActor a,
            SocialSimActor b)
        {
            if (a?.State == null || b?.State == null || world == null) return true;

            // Probe CanLethalCombination under Critical severity; if other gates are missing,
            // hostility alone cannot produce death (returns true = insufficient / blocked).
            bool prevFight = session != null && session.FightOccurred;
            var prevSev = session?.FightSeverity ?? SocialFightSeverity.None;
            if (session != null)
            {
                session.FightOccurred = true;
                session.FightSeverity = SocialFightSeverity.Critical;
            }

            bool can = session != null && CanLethalCombination(world, session, a, b);

            if (session != null)
            {
                session.FightOccurred = prevFight;
                session.FightSeverity = prevSev;
            }

            return !can;
        }

        static bool HasMurderousHistory(SocialMemoryStore store, int a, int b)
        {
            if (store == null) return false;
            int count = CountMajorNegPair(store, a, b) + CountMajorNegPair(store, b, a);
            return count >= SocialConflictTuning.MurderousMajorNegMin;
        }

        static int CountMajorNegPair(SocialMemoryStore store, int observer, int target)
        {
            int n = 0;
            var list = store.GetToward(observer, target);
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null || e.Significance != SocialMemorySignificance.Major) continue;
                if (e.Type == SocialMemoryType.InsultedMe
                    || e.Type == SocialMemoryType.BlamedMe
                    || e.Type == SocialMemoryType.HurtBy
                    || e.Type == SocialMemoryType.FailedTogether)
                    n++;
            }
            return n;
        }

        static void RecordDeathMemories(
            SocialAuraWorld world, int killerId, int victimId,
            SocialSimActor a, SocialSimActor b, float t)
        {
            if (world?.Memory == null) return;
            var store = world.Memory;

            void Maj(int obs, int tgt, SocialMemoryType type, float str, string src)
            {
                if (obs <= 0 || tgt <= 0 || obs == tgt) return;
                store.Add(new SocialMemoryEntry
                {
                    ObserverId = obs,
                    TargetId = tgt,
                    Type = type,
                    Strength = str,
                    GameTime = t,
                    Context = SocialContext.Emergency,
                    SourceRef = src,
                    Significance = SocialMemorySignificance.Major,
                });
            }

            string src = $"DEATH:{killerId}->{victimId}";
            Maj(victimId, killerId, SocialMemoryType.KilledBy, 1f, src);
            // Killer carries blame / failed-together Major
            Maj(killerId, victimId,
                WorkerRoll.NextUnit() < 0.5f
                    ? SocialMemoryType.BlamedMe
                    : SocialMemoryType.FailedTogether,
                0.95f, src);

            // HurtBy both ways if either was injured before death
            if (a?.State != null && a.State.Injury >= 0.5f)
                Maj(a.Id, b.Id, SocialMemoryType.HurtBy, 0.9f, src);
            if (b?.State != null && b.State.Injury >= 0.5f)
                Maj(b.Id, a.Id, SocialMemoryType.HurtBy, 0.9f, src);

            // Witnesses in world (not the pair)
            foreach (var actor in world.Actors)
            {
                if (actor == null || actor.Id == killerId || actor.Id == victimId) continue;
                if (actor.State != null && !actor.State.IsAlive) continue;
                Maj(actor.Id, victimId, SocialMemoryType.WitnessedDeath, 0.85f, src);
            }
        }

        static string SeverityTag(SocialFightSeverity s) =>
            s switch
            {
                SocialFightSeverity.Lethal => "LETHAL/DEATH",
                SocialFightSeverity.Critical => "CRITICAL",
                SocialFightSeverity.Severe => "SEVERE",
                SocialFightSeverity.Scuffle => "FIGHT",
                _ => "FIGHT",
            };

        static SocialFightBeat PickFightBeat(int index, int total, SocialSimActor actor, SocialSimActor other)
        {
            if (index == total - 1)
                return WorkerRoll.NextUnit() < 0.55f
                    ? SocialFightBeat.BreakApart
                    : SocialFightBeat.BackAway;

            float wShove = 0.25f + actor.Stats.Get(WorkerStatId.Bravery) / 40f;
            float wGrab = 0.18f + actor.Stats.Get(WorkerStatId.Determination) / 50f;
            float wPunch = 0.12f + actor.Stats.Get(WorkerStatId.Bravery) / 35f
                           - actor.Stats.Get(WorkerStatId.Composure) / 45f;
            float wRet = 0.2f;
            float wBack = 0.1f + actor.Stats.Get(WorkerStatId.Composure) / 40f;
            float sum = wShove + wGrab + wPunch + wRet + wBack;
            float pick = WorkerRoll.NextUnit() * sum;
            if ((pick -= wShove) <= 0f) return SocialFightBeat.Shove;
            if ((pick -= wGrab) <= 0f) return SocialFightBeat.Grab;
            if ((pick -= wPunch) <= 0f) return SocialFightBeat.Punch;
            if ((pick -= wRet) <= 0f) return SocialFightBeat.Retaliate;
            return SocialFightBeat.BackAway;
        }

        static WorkerRollResult ResolveFightBeat(
            SocialSimActor actor, SocialSimActor other, SocialFightBeat beat)
        {
            WorkerStatId stat;
            int dc;
            int mod = 0;
            switch (beat)
            {
                case SocialFightBeat.Shove:
                    stat = WorkerStatId.Bravery;
                    dc = 11;
                    mod -= (other.Stats.Get(WorkerStatId.Balance) - 10) / 5;
                    break;
                case SocialFightBeat.Grab:
                    stat = WorkerStatId.Determination;
                    dc = 12;
                    break;
                case SocialFightBeat.Punch:
                    stat = WorkerStatId.Bravery;
                    dc = 13;
                    mod -= (other.Stats.Get(WorkerStatId.Composure) - 10) / 5;
                    break;
                case SocialFightBeat.Retaliate:
                    stat = WorkerStatId.Bravery;
                    dc = 12;
                    mod += (actor.Stats.Get(WorkerStatId.Determination) - 10) / 5;
                    break;
                case SocialFightBeat.BackAway:
                    stat = WorkerStatId.Composure;
                    dc = 11;
                    break;
                default: // BreakApart
                    stat = WorkerStatId.Composure;
                    dc = 10;
                    mod += (actor.Stats.Get(WorkerStatId.Tolerance) - 10) / 5;
                    break;
            }
            return WorkerRoll.Check(actor.Stats, stat, dc, mod);
        }

        static void ApplyFightBeat(
            SocialSimActor actor, SocialSimActor other, SocialFightBeat beat, bool ok,
            ref float injA, ref float injB,
            ref float dFrA, ref float dFrB,
            ref float dMoA, ref float dMoB,
            int idA, int idB)
        {
            switch (beat)
            {
                case SocialFightBeat.Shove:
                    if (ok)
                    {
                        if (other.Id == idA) { injA += SocialConflictTuning.FightInjuryMinor * 0.4f; dFrA += 3f; }
                        else { injB += SocialConflictTuning.FightInjuryMinor * 0.4f; dFrB += 3f; }
                    }
                    else
                    {
                        if (actor.Id == idA) dFrA += 2f; else dFrB += 2f;
                    }
                    break;
                case SocialFightBeat.Grab:
                    if (ok)
                    {
                        if (other.Id == idA) { dFrA += 4f; dMoA -= 1f; }
                        else { dFrB += 4f; dMoB -= 1f; }
                    }
                    break;
                case SocialFightBeat.Punch:
                    if (ok)
                    {
                        if (other.Id == idA)
                        {
                            injA += SocialConflictTuning.FightInjuryModerate * 0.65f;
                            dFrA += 5f; dMoA -= 2f;
                        }
                        else
                        {
                            injB += SocialConflictTuning.FightInjuryModerate * 0.65f;
                            dFrB += 5f; dMoB -= 2f;
                        }
                    }
                    else
                    {
                        if (actor.Id == idA)
                        {
                            dFrA += 3f;
                            injA += SocialConflictTuning.FightInjuryMinor * 0.25f;
                        }
                        else
                        {
                            dFrB += 3f;
                            injB += SocialConflictTuning.FightInjuryMinor * 0.25f;
                        }
                    }
                    break;
                case SocialFightBeat.Retaliate:
                    if (ok)
                    {
                        if (other.Id == idA)
                        {
                            injA += SocialConflictTuning.FightInjuryMinor * 0.7f;
                            dFrA += 4f;
                        }
                        else
                        {
                            injB += SocialConflictTuning.FightInjuryMinor * 0.7f;
                            dFrB += 4f;
                        }
                    }
                    break;
                case SocialFightBeat.BackAway:
                case SocialFightBeat.BreakApart:
                    if (ok)
                    {
                        if (actor.Id == idA) { dFrA -= 2f; dFrB -= 1f; }
                        else { dFrB -= 2f; dFrA -= 1f; }
                    }
                    break;
            }
        }

        static void ApplyFightInjury(
            SocialSimActor victim, float amount, int relatedId,
            float maxCap = -1f)
        {
            if (victim?.State == null || !victim.State.IsAlive || amount < 0.5f) return;
            if (maxCap < 0f) maxCap = SocialConflictTuning.FightInjuryModerate;
            amount = Mathf.Clamp(amount, 0f, maxCap);
            victim.State.AddInjury(amount);
            WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                victim.Id,
                WorkerStateEventType.Injury,
                amount,
                "SocialFight",
                relatedWorkerId: relatedId));

            var wr = WorkerRuntime.Find(victim.Id);
            if (wr != null)
            {
                var typed = WorkerAccidentSystem.PickFightInjury(amount);
                var rec = new WorkerInjuryRecord
                {
                    Type = typed,
                    BodyPart = WorkerInjuryCatalog.DefaultPart(typed),
                    Severity = WorkerInjuryCatalog.SeverityOf(typed),
                    Cause = WorkerInjuryCause.SocialFight,
                    CauseLabel = "SocialFight",
                    InflictedGameHours = WorkerStateClock.GameHours,
                    RecoveryGameHoursTotal = WorkerInjuryCatalog.DefaultRecoveryHours(typed),
                    RecoveryGameHoursLeft = WorkerInjuryCatalog.DefaultRecoveryHours(typed),
                    MeterContribution = amount,
                };
                wr.Injuries.Add(rec);
                wr.Injuries.SyncNeedsCare(victim.State);
                wr.Injuries.NoteAccident($"{wr.DisplayName} {rec.DisplayName} — fight");
            }
        }

        static void RecordFightMemories(SocialMemoryStore store, int a, int b, float t)
        {
            if (store == null) return;
            void Maj(int obs, int tgt, SocialMemoryType type, float str)
            {
                store.Add(new SocialMemoryEntry
                {
                    ObserverId = obs,
                    TargetId = tgt,
                    Type = type,
                    Strength = str,
                    GameTime = t,
                    Context = SocialContext.Emergency,
                    SourceRef = $"FIGHT:{a}-{b}",
                    Significance = SocialMemorySignificance.Major,
                });
            }
            Maj(a, b, SocialMemoryType.InsultedMe, 0.9f);
            Maj(b, a, SocialMemoryType.InsultedMe, 0.9f);
            Maj(a, b, SocialMemoryType.BlamedMe, 0.75f);
            Maj(b, a, SocialMemoryType.BlamedMe, 0.75f);
            Maj(a, b, SocialMemoryType.FailedTogether, 0.7f);
            Maj(b, a, SocialMemoryType.FailedTogether, 0.7f);
        }

        // ─── Witnesses ──────────────────────────────────────────────

        void RunWitnesses(
            SocialAuraWorld world,
            SocialArgumentSession session,
            float gameHours,
            bool fighting)
        {
            if (world == null || session == null) return;
            int acted = 0;
            foreach (var actor in world.Actors)
            {
                if (acted >= SocialConflictTuning.MaxWitnessesPerEvent) break;
                if (actor == null || session.Involves(actor.Id)) continue;
                // Skip dead; SocialSimActor has no world position — WitnessReachMul unused without coords
                if (actor.State == null || !actor.State.IsAlive) continue;
                if (_witnessCooldownUntil.TryGetValue(actor.Id, out float until) && gameHours < until)
                    continue;
                if (actor.State.Injury >= SocialAuraEligibility.InjurySuppressThreshold)
                    continue;

                var action = ChooseWitnessAction(world, actor, session, fighting);
                if (action == SocialWitnessAction.Ignore)
                {
                    // Still spend a soft cooldown so we don't re-check every beat spam
                    if (WorkerRoll.NextUnit() < 0.7f) continue;
                    _witnessCooldownUntil[actor.Id] = gameHours + SocialConflictTuning.WitnessCooldownHours * 0.5f;
                    continue;
                }

                var roll = ResolveWitnessRoll(actor, action, fighting);
                int supportTarget = 0;
                if (action == SocialWitnessAction.SupportSomeone)
                    supportTarget = PickSupportTarget(world, actor, session);

                ApplyWitnessConsequences(world, actor, session, action, supportTarget, roll.Success, gameHours);

                // Failed BreakUpFight during a live fight marks intervention failure (lethal gate)
                if (fighting
                    && action == SocialWitnessAction.BreakUpFight
                    && !roll.Success)
                {
                    session.FailedIntervention = true;
                }

                TotalWitnessActs++;
                acted++;
                _witnessCooldownUntil[actor.Id] = gameHours + SocialConflictTuning.WitnessCooldownHours;

                var ev = new SocialConflictEvent
                {
                    GameHours = gameHours,
                    WorkerA = session.IdA,
                    WorkerB = session.IdB,
                    SpeakerId = actor.Id,
                    ListenerId = supportTarget > 0 ? supportTarget : session.IdA,
                    Phase = session.Phase,
                    Outcome = session.LastOutcome,
                    IsWitness = true,
                    WitnessId = actor.Id,
                    WitnessAction = action,
                    WitnessSupportTargetId = supportTarget,
                    IsFight = fighting,
                    RollSuccess = roll.Success,
                    D20 = roll.D20,
                    DC = roll.DC,
                    Line = SocialConflictLineBank.PickWitnessLine(action, roll.Success, fighting),
                    Why = $"witness {actor.Name} {action} {(roll.Success ? "OK" : "FAIL")}",
                };
                PushHistory(ev);
                _pendingPresent.Add(ev);

                // Successful break-up / de-escalate can end argument without fight
                if (roll.Success
                    && (action == SocialWitnessAction.BreakUpFight || action == SocialWitnessAction.DeEscalate)
                    && session.IsActive)
                {
                    ForceResolve(world, session, gameHours, SocialArgumentOutcome.MutualDisengage);
                    break;
                }
            }
        }

        static SocialWitnessAction ChooseWitnessAction(
            SocialAuraWorld world,
            SocialSimActor witness,
            SocialArgumentSession session,
            bool fighting)
        {
            var towardA = world.Relation(witness.Id, session.IdA);
            var towardB = world.Relation(witness.Id, session.IdB);
            int lead = witness.Stats.Get(WorkerStatId.Leadership);
            int brave = witness.Stats.Get(WorkerStatId.Bravery);
            int emp = witness.Stats.Get(WorkerStatId.Empathy);
            int comp = witness.Stats.Get(WorkerStatId.Composure);
            float fr = witness.State?.Frustration ?? 0f;

            // Exhausted / checked-out → ignore
            if (fr >= 80f || (witness.State != null && witness.State.MentalFatigue >= 75f))
                return SocialWitnessAction.Ignore;

            float wIgnore = 0.35f + comp / 50f;
            float wVerbal = 0.12f + lead / 35f + emp / 40f;
            float wSupport = 0.1f + emp / 30f
                             + Mathf.Max(towardA.Warmth, towardB.Warmth) * 0.02f;
            float wDeEsc = 0.15f + lead / 28f + comp / 35f + emp / 40f;
            float wBreak = fighting
                ? 0.2f + lead / 25f + brave / 30f
                : 0.02f;

            // Low bravery shy away from fights
            if (fighting && brave <= 8) { wBreak *= 0.2f; wIgnore *= 1.4f; }

            float sum = wIgnore + wVerbal + wSupport + wDeEsc + wBreak;
            float pick = WorkerRoll.NextUnit() * sum;
            if ((pick -= wIgnore) <= 0f) return SocialWitnessAction.Ignore;
            if ((pick -= wVerbal) <= 0f) return SocialWitnessAction.VerbalIntervene;
            if ((pick -= wSupport) <= 0f) return SocialWitnessAction.SupportSomeone;
            if ((pick -= wDeEsc) <= 0f) return SocialWitnessAction.DeEscalate;
            return SocialWitnessAction.BreakUpFight;
        }

        static int PickSupportTarget(
            SocialAuraWorld world, SocialSimActor witness, SocialArgumentSession session)
        {
            var towardA = world.Relation(witness.Id, session.IdA);
            var towardB = world.Relation(witness.Id, session.IdB);
            float scoreA = towardA.Warmth + towardA.Trust * 0.5f - towardA.Hostility;
            float scoreB = towardB.Warmth + towardB.Trust * 0.5f - towardB.Hostility;
            return scoreA >= scoreB ? session.IdA : session.IdB;
        }

        static WorkerRollResult ResolveWitnessRoll(
            SocialSimActor witness, SocialWitnessAction action, bool fighting)
        {
            WorkerStatId stat;
            int dc;
            int mod = fighting ? -1 : 0;
            switch (action)
            {
                case SocialWitnessAction.VerbalIntervene:
                    stat = WorkerStatId.Leadership;
                    dc = 12;
                    break;
                case SocialWitnessAction.SupportSomeone:
                    stat = WorkerStatId.Empathy;
                    dc = 11;
                    break;
                case SocialWitnessAction.DeEscalate:
                    stat = WorkerStatId.Composure;
                    dc = 12;
                    mod += (witness.Stats.Get(WorkerStatId.Empathy) - 10) / 5;
                    break;
                case SocialWitnessAction.BreakUpFight:
                    stat = WorkerStatId.Bravery;
                    dc = 13;
                    mod += (witness.Stats.Get(WorkerStatId.Leadership) - 10) / 5;
                    break;
                default:
                    stat = WorkerStatId.Focus;
                    dc = 8;
                    break;
            }
            return WorkerRoll.Check(witness.Stats, stat, dc, mod);
        }

        static void ApplyWitnessConsequences(
            SocialAuraWorld world,
            SocialSimActor witness,
            SocialArgumentSession session,
            SocialWitnessAction action,
            int supportTarget,
            bool success,
            float gameHours)
        {
            var a = world.Get(session.IdA);
            var b = world.Get(session.IdB);
            if (a == null || b == null) return;

            switch (action)
            {
                case SocialWitnessAction.VerbalIntervene:
                case SocialWitnessAction.DeEscalate:
                    if (success)
                    {
                        world.Relation(a.Id, b.Id).Add(0.15f, 0.1f, -0.6f);
                        world.Relation(b.Id, a.Id).Add(0.15f, 0.1f, -0.6f);
                        a.State.Frustration = Mathf.Clamp(a.State.Frustration - 3f, 0f, 100f);
                        b.State.Frustration = Mathf.Clamp(b.State.Frustration - 3f, 0f, 100f);
                        // Both remember witness as supportive
                        AddMem(world.Memory, a.Id, witness.Id, SocialMemoryType.SupportedMe, 0.55f, gameHours);
                        AddMem(world.Memory, b.Id, witness.Id, SocialMemoryType.SupportedMe, 0.55f, gameHours);
                    }
                    else
                    {
                        world.Relation(a.Id, witness.Id).Add(0f, -0.2f, 0.4f);
                        world.Relation(b.Id, witness.Id).Add(0f, -0.2f, 0.4f);
                    }
                    break;

                case SocialWitnessAction.SupportSomeone:
                    if (supportTarget <= 0) break;
                    int opposed = session.Other(supportTarget);
                    AddMem(world.Memory, supportTarget, witness.Id, SocialMemoryType.TookMySide,
                        success ? 0.7f : 0.4f, gameHours, major: success);
                    AddMem(world.Memory, opposed, witness.Id, SocialMemoryType.BlamedMe,
                        success ? 0.55f : 0.35f, gameHours);
                    world.Relation(supportTarget, witness.Id).Add(success ? 0.6f : 0.2f, 0.5f, -0.2f);
                    world.Relation(opposed, witness.Id).Add(success ? -0.5f : -0.2f, -0.4f, 0.8f);
                    world.Relation(witness.Id, opposed).Add(-0.3f, -0.3f, 0.5f);
                    break;

                case SocialWitnessAction.BreakUpFight:
                    if (success)
                    {
                        world.Relation(a.Id, witness.Id).Add(0.4f, 0.2f, -0.3f);
                        world.Relation(b.Id, witness.Id).Add(0.4f, 0.2f, -0.3f);
                        AddMem(world.Memory, a.Id, witness.Id, SocialMemoryType.HelpedMe, 0.65f, gameHours, major: true);
                        AddMem(world.Memory, b.Id, witness.Id, SocialMemoryType.HelpedMe, 0.65f, gameHours, major: true);
                        a.State.Frustration = Mathf.Clamp(a.State.Frustration - 5f, 0f, 100f);
                        b.State.Frustration = Mathf.Clamp(b.State.Frustration - 5f, 0f, 100f);
                    }
                    else
                    {
                        // Got shoved aside — minor injury possible
                        if (WorkerRoll.NextUnit() < 0.35f)
                            ApplyFightInjury(witness, SocialConflictTuning.FightInjuryMinor * 0.5f, session.IdA);
                    }
                    break;
            }
        }

        static void AddMem(
            SocialMemoryStore store, int obs, int tgt, SocialMemoryType type,
            float str, float t, bool major = false)
        {
            if (store == null || obs <= 0 || tgt <= 0 || obs == tgt) return;
            store.Add(new SocialMemoryEntry
            {
                ObserverId = obs,
                TargetId = tgt,
                Type = type,
                Strength = Mathf.Clamp01(str),
                GameTime = t,
                Context = SocialContext.Emergency,
                SourceRef = "WITNESS",
                Significance = SocialMemoryStore.ClassifySignificance(str, major),
            });
        }

        // ─── Argument helpers ───────────────────────────────────────

        static float ArgueInitScore(SocialSimActor self, SocialDirectedRelation toward)
        {
            return self.State.Frustration / 40f
                   + self.Stats.Get(WorkerStatId.Bravery) / 18f
                   + self.Stats.Get(WorkerStatId.Determination) / 22f
                   + toward.Hostility * 0.04f
                   - self.Stats.Get(WorkerStatId.Composure) / 28f
                   - self.Stats.Get(WorkerStatId.Tolerance) / 30f;
        }

        static SocialAction PickArgumentMove(
            SocialSimActor speaker, SocialSimActor listener,
            SocialDirectedRelation rel, SocialArgumentSession session)
        {
            // Weights: Confront, Provoke, Complain, Connect(de-esc)
            float wConf = 0.2f + speaker.Stats.Get(WorkerStatId.Bravery) / 25f
                          + speaker.Stats.Get(WorkerStatId.Determination) / 30f
                          + rel.Hostility * 0.03f
                          - speaker.Stats.Get(WorkerStatId.Composure) / 35f;
            float wProv = 0.18f + speaker.Expression.Negative * 0.6f
                          + speaker.Stats.Get(WorkerStatId.Bravery) / 28f;
            float wComp = 0.15f + speaker.State.Frustration / 80f;
            float wConn = 0.12f + speaker.Stats.Get(WorkerStatId.Empathy) / 28f
                          + speaker.Stats.Get(WorkerStatId.Composure) / 30f
                          + speaker.Stats.Get(WorkerStatId.Leadership) / 35f
                          - rel.Hostility * 0.02f;

            if (session.Phase == SocialArgumentPhase.Resolving)
                wConn *= 1.6f;
            if (session.Phase == SocialArgumentPhase.Peak)
            {
                wConf *= 1.35f;
                wProv *= 1.25f;
                wConn *= 0.7f;
            }
            if (session.Phase == SocialArgumentPhase.Opening)
                wComp *= 1.3f;

            float sum = Mathf.Max(0.01f, wConf + wProv + wComp + wConn);
            float pick = WorkerRoll.NextUnit() * sum;
            if ((pick -= wConf) <= 0f) return SocialAction.Confront;
            if ((pick -= wProv) <= 0f) return SocialAction.Provoke;
            if ((pick -= wComp) <= 0f) return SocialAction.Complain;
            return SocialAction.Connect;
        }

        static SocialResponse PickArgumentResponse(
            SocialSimActor listener, SocialSimActor speaker,
            SocialDirectedRelation towardSpeaker,
            SocialAction move, bool moveOk, SocialArgumentSession session)
        {
            float wAcc = 0.08f + listener.Stats.Get(WorkerStatId.Tolerance) / 40f;
            float wDef = 0.12f + listener.Stats.Get(WorkerStatId.Focus) / 35f;
            float wIgn = 0.1f + listener.Stats.Get(WorkerStatId.Composure) / 40f;
            float wAgr = 0.08f + listener.Stats.Get(WorkerStatId.Empathy) / 35f;
            float wPush = 0.18f + listener.Stats.Get(WorkerStatId.Bravery) / 30f
                          + towardSpeaker.Hostility * 0.03f;
            float wEsc = 0.15f + listener.Stats.Get(WorkerStatId.Determination) / 28f
                         + listener.Expression.Negative * 0.4f
                         - listener.Stats.Get(WorkerStatId.Composure) / 32f
                         - listener.Stats.Get(WorkerStatId.Tolerance) / 40f;
            float wWdr = 0.1f + listener.Stats.Get(WorkerStatId.Composure) / 45f;

            if (move == SocialAction.Connect && moveOk)
            {
                wAcc *= 1.8f;
                wAgr *= 1.5f;
                wEsc *= 0.55f;
                wPush *= 0.7f;
            }
            if (move == SocialAction.Confront || move == SocialAction.Provoke)
            {
                wPush *= 1.4f;
                wEsc *= 1.35f;
                wAcc *= 0.5f;
            }
            if (session.Phase == SocialArgumentPhase.Peak)
            {
                wEsc *= 1.3f;
                wPush *= 1.2f;
            }

            float sum = wAcc + wDef + wIgn + wAgr + wPush + wEsc + wWdr;
            float pick = WorkerRoll.NextUnit() * sum;
            if ((pick -= wAcc) <= 0f) return SocialResponse.Accept;
            if ((pick -= wDef) <= 0f) return SocialResponse.Deflect;
            if ((pick -= wIgn) <= 0f) return SocialResponse.Ignore;
            if ((pick -= wAgr) <= 0f) return SocialResponse.Agree;
            if ((pick -= wPush) <= 0f) return SocialResponse.PushBack;
            if ((pick -= wEsc) <= 0f) return SocialResponse.Escalate;
            return SocialResponse.Withdraw;
        }

        static WorkerRollResult ResolveArgueMoveRoll(
            SocialSimActor speaker, SocialSimActor listener,
            SocialAction move, SocialArgumentSession session)
        {
            WorkerStatId stat;
            int dc;
            int mod = 0;
            switch (move)
            {
                case SocialAction.Connect:
                    stat = WorkerStatId.Empathy;
                    dc = 12;
                    mod += (speaker.Stats.Get(WorkerStatId.Composure) - 10) / 4;
                    mod += (speaker.Stats.Get(WorkerStatId.Leadership) - 10) / 5;
                    mod -= Mathf.RoundToInt(listener.State.Frustration / 45f);
                    break;
                case SocialAction.Complain:
                    stat = WorkerStatId.Determination;
                    dc = 11;
                    break;
                case SocialAction.Provoke:
                    stat = WorkerStatId.Bravery;
                    dc = 12;
                    mod -= (listener.Stats.Get(WorkerStatId.Tolerance) - 10) / 5;
                    break;
                default: // Confront
                    stat = WorkerStatId.Bravery;
                    dc = 13;
                    mod += (speaker.Stats.Get(WorkerStatId.Determination) - 10) / 5;
                    mod -= (listener.Stats.Get(WorkerStatId.Composure) - 10) / 5;
                    break;
            }
            if (session.Phase == SocialArgumentPhase.Peak) dc += 1;
            return WorkerRoll.Check(speaker.Stats, stat, dc, mod);
        }

        static WorkerRollResult ResolveArgueResponseRoll(
            SocialSimActor listener, SocialSimActor speaker,
            SocialResponse resp, SocialAction move, bool moveOk)
        {
            WorkerStatId stat;
            int dc = 11;
            int mod = moveOk ? 0 : 1;
            switch (resp)
            {
                case SocialResponse.Accept:
                case SocialResponse.Agree:
                    stat = WorkerStatId.Empathy;
                    break;
                case SocialResponse.Deflect:
                case SocialResponse.Ignore:
                    stat = WorkerStatId.Focus;
                    break;
                case SocialResponse.PushBack:
                    stat = WorkerStatId.Bravery;
                    break;
                case SocialResponse.Escalate:
                    stat = WorkerStatId.Determination;
                    mod -= (listener.Stats.Get(WorkerStatId.Composure) - 10) / 5;
                    break;
                default:
                    stat = WorkerStatId.Composure;
                    break;
            }
            return WorkerRoll.Check(listener.Stats, stat, dc, mod);
        }

        static void ApplyArgumentConsequences(
            SocialSimActor speaker, SocialSimActor listener,
            SocialDirectedRelation relSL, SocialDirectedRelation relLS,
            SocialArgumentSession session,
            SocialAction move, SocialResponse resp,
            bool moveOk, bool respOk,
            out float dFrS, out float dFrL, out float dMoS, out float dMoL,
            out float dHoSL, out float dHoLS, out float dTrSL, out float dTrLS)
        {
            dFrS = dFrL = dMoS = dMoL = 0f;
            dHoSL = dHoLS = dTrSL = dTrLS = 0f;
            float dWaSL = 0f, dWaLS = 0f;

            bool deEscOk = move == SocialAction.Connect && moveOk
                           && (resp == SocialResponse.Accept || resp == SocialResponse.Agree
                               || resp == SocialResponse.Withdraw);
            bool clash = (move == SocialAction.Confront || move == SocialAction.Provoke)
                         && (resp == SocialResponse.Escalate || resp == SocialResponse.PushBack);

            if (deEscOk)
            {
                dTrSL += 0.7f; dTrLS += 0.6f;
                dWaSL += 0.5f; dWaLS += 0.4f;
                dHoSL -= 1.0f; dHoLS -= 0.9f;
                dFrS -= 4f; dFrL -= 3.5f;
                dMoS += 1.2f; dMoL += 1.0f;
            }
            else if (clash)
            {
                dTrSL -= 0.7f; dTrLS -= 0.6f;
                dWaSL -= 0.8f; dWaLS -= 0.7f;
                dHoSL += 1.5f; dHoLS += 1.4f;
                dFrS += 3.5f; dFrL += 4f;
                dMoS -= 1.0f; dMoL -= 1.2f;
            }
            else if (resp == SocialResponse.Withdraw || resp == SocialResponse.Ignore)
            {
                dHoSL += 0.3f;
                dFrS += 2f;
                dFrL -= 1f;
            }
            else if (move == SocialAction.Connect && !moveOk)
            {
                dFrS += 2.5f;
                dHoLS += 0.4f;
            }
            else
            {
                dHoSL += 0.6f; dHoLS += 0.5f;
                dTrSL -= 0.3f; dTrLS -= 0.25f;
                dFrS += 1.5f; dFrL += 1.8f;
            }

            dTrSL = Mathf.Clamp(dTrSL, -2f, 2f);
            dTrLS = Mathf.Clamp(dTrLS, -2f, 2f);
            dHoSL = Mathf.Clamp(dHoSL, -2.5f, 2.5f);
            dHoLS = Mathf.Clamp(dHoLS, -2.5f, 2.5f);
            dWaSL = Mathf.Clamp(dWaSL, -2f, 2f);
            dWaLS = Mathf.Clamp(dWaLS, -2f, 2f);

            EarlyCrewPressure.ScaleConflictRecoveryDeltas(
                ref dTrSL, ref dTrLS,
                ref dWaSL, ref dWaLS,
                ref dHoSL, ref dHoLS,
                ref dFrS, ref dFrL);

            relSL.Add(dTrSL, dWaSL, dHoSL);
            relLS.Add(dTrLS, dWaLS, dHoLS);
            speaker.State.Frustration = Mathf.Clamp(speaker.State.Frustration + dFrS, 0f, 100f);
            listener.State.Frustration = Mathf.Clamp(listener.State.Frustration + dFrL, 0f, 100f);
            speaker.State.Morale = Mathf.Clamp(speaker.State.Morale + dMoS, 0f, 100f);
            listener.State.Morale = Mathf.Clamp(listener.State.Morale + dMoL, 0f, 100f);
        }

        static SocialArgumentOutcome ClassifyBeatOutcome(
            SocialArgumentSession session,
            SocialAction move, SocialResponse resp,
            bool moveOk, bool respOk)
        {
            if (move == SocialAction.Connect && moveOk
                && (resp == SocialResponse.Accept || resp == SocialResponse.Agree))
            {
                if (session.ExchangeCount >= 2)
                    return SocialArgumentOutcome.Apology;
                return SocialArgumentOutcome.PartialResolution;
            }

            if (resp == SocialResponse.Withdraw
                || (resp == SocialResponse.Accept && move != SocialAction.Connect))
                return SocialArgumentOutcome.BacksDown;

            if (resp == SocialResponse.Ignore && moveOk == false)
                return SocialArgumentOutcome.MutualDisengage;

            if ((move == SocialAction.Confront || move == SocialAction.Provoke)
                && (resp == SocialResponse.Escalate || resp == SocialResponse.PushBack))
            {
                if (session.Phase == SocialArgumentPhase.Peak)
                    return SocialArgumentOutcome.EscalateFurther;
                return SocialArgumentOutcome.GrudgeStrengthened;
            }

            if (session.Phase == SocialArgumentPhase.Resolving && move == SocialAction.Connect)
                return moveOk
                    ? SocialArgumentOutcome.PartialResolution
                    : SocialArgumentOutcome.RelationshipWorsens;

            return SocialArgumentOutcome.RelationshipWorsens;
        }

        static void RecordArgumentMemories(
            SocialMemoryStore store,
            int speakerId, int listenerId,
            SocialAction move, SocialResponse resp,
            bool moveOk, bool respOk,
            SocialArgumentOutcome outcome,
            float gameHours)
        {
            if (store == null) return;
            string src = $"ARG:{speakerId}>{listenerId}:{move}/{resp}/{outcome}";

            void Add(int obs, int tgt, SocialMemoryType type, float str, bool major)
            {
                store.Add(new SocialMemoryEntry
                {
                    ObserverId = obs,
                    TargetId = tgt,
                    Type = type,
                    Strength = Mathf.Clamp01(str),
                    GameTime = gameHours,
                    Context = SocialContext.Emergency,
                    SourceRef = src,
                    Significance = SocialMemoryStore.ClassifySignificance(str, major),
                });
            }

            if (outcome == SocialArgumentOutcome.Apology)
            {
                Add(listenerId, speakerId, SocialMemoryType.Apologized, 0.7f, true);
                Add(speakerId, listenerId, SocialMemoryType.Apologized, 0.55f, false);
                return;
            }

            if (move == SocialAction.Confront || move == SocialAction.Provoke)
            {
                Add(listenerId, speakerId, SocialMemoryType.InsultedMe, 0.7f, true);
                if (resp == SocialResponse.Escalate || resp == SocialResponse.PushBack)
                    Add(speakerId, listenerId, SocialMemoryType.InsultedMe, 0.6f, false);
            }

            if (outcome == SocialArgumentOutcome.GrudgeStrengthened
                || outcome == SocialArgumentOutcome.RelationshipWorsens)
            {
                Add(listenerId, speakerId, SocialMemoryType.BlamedMe, 0.55f, false);
            }
        }

        static bool HasNegativeHistory(SocialMemoryStore store, int observer, int target)
        {
            if (store == null) return false;
            var list = store.GetToward(observer, target);
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null) continue;
                if (e.Strength < SocialConflictTuning.NegMemoryStrengthMin) continue;
                if (e.Significance == SocialMemorySignificance.Ordinary
                    && e.Strength < 0.6f) continue;
                if (e.Type == SocialMemoryType.InsultedMe
                    || e.Type == SocialMemoryType.BlamedMe
                    || e.Type == SocialMemoryType.LetMeDown
                    || e.Type == SocialMemoryType.FailedTogether
                    || e.Type == SocialMemoryType.HurtBy
                    || e.Type == SocialMemoryType.WitnessedViolence
                    || e.Type == SocialMemoryType.KilledBy
                    || e.Type == SocialMemoryType.WitnessedDeath)
                    return true;
            }
            return false;
        }

        static bool IsHostileTrigger(SocialEncounterLog log)
        {
            if (log == null) return false;
            if (log.Action == SocialAction.Provoke || log.Action == SocialAction.Confront)
                return log.Response == SocialResponse.Escalate
                       || log.Response == SocialResponse.PushBack
                       || log.ActionSuccess;
            if (log.Action == SocialAction.Complain
                && (log.Response == SocialResponse.Escalate || log.Response == SocialResponse.PushBack))
                return true;
            return log.OutcomeSummary != null && log.OutcomeSummary.Contains("CLASH");
        }

        static bool HasRecentHostileInPair(SocialPairTransient pair, float gameHours)
        {
            if (pair?.RecentHistory == null) return false;
            // RecentHistory has no timestamps — treat last few clash-like logs as recent
            int n = pair.RecentHistory.Count;
            int look = Mathf.Min(3, n);
            for (int i = n - look; i < n; i++)
            {
                if (IsHostileTrigger(pair.RecentHistory[i]))
                    return true;
            }
            return false;
        }

        void PushHistory(SocialConflictEvent ev)
        {
            if (ev == null) return;
            _history.Add(ev);
            while (_history.Count > SocialConflictTuning.DevHistoryCap)
                _history.RemoveAt(0);
        }

        // ─── Force DEV APIs ──────────────────────────────────────────

        /// <summary>DEV: hostile seed + open session + one Peak beat toward escalate.</summary>
        public SocialConflictEvent ForceArgument(SocialAuraWorld world, int idA, int idB, float gameHours)
        {
            if (world == null) return null;
            var a = world.Get(idA);
            var b = world.Get(idB);
            if (a?.State == null || b?.State == null || !a.State.IsAlive || !b.State.IsAlive)
                return null;

            EnsureHostileSeed(world, a, b, forFight: false);

            long pk = PairKey(idA, idB);
            var session = GetSession(idA, idB);
            if (session == null || !session.IsActive)
            {
                session = new SocialArgumentSession
                {
                    IdA = Math.Min(idA, idB),
                    IdB = Math.Max(idA, idB),
                    Phase = SocialArgumentPhase.Peak,
                    ExchangeCount = 2,
                    StartGameHours = gameHours,
                    LastBeatGameHours = gameHours,
                    NextBeatAtGameHours = gameHours,
                    AggressorId = idA,
                    FailedDeEscalation = true,
                };
                session.PushTag("FORCE_ARG");
                _sessions[pk] = session;
                TotalArgumentsStarted++;
            }
            else
            {
                session.Phase = SocialArgumentPhase.Peak;
                session.FailedDeEscalation = true;
                session.PushTag("FORCE_ARG");
            }

            return ResolveArgumentBeat(world, session, gameHours, openingForce: false);
        }

        /// <summary>DEV: FailedDeEscalation + force CanStartFight path → RunFight.</summary>
        public SocialConflictEvent ForceFight(SocialAuraWorld world, int idA, int idB, float gameHours)
        {
            if (world == null) return null;
            var a = world.Get(idA);
            var b = world.Get(idB);
            if (a?.State == null || b?.State == null || !a.State.IsAlive || !b.State.IsAlive)
                return null;

            EnsureHostileSeed(world, a, b, forFight: true);

            long pk = PairKey(idA, idB);
            var session = GetSession(idA, idB);
            if (session == null || !session.IsActive)
            {
                session = new SocialArgumentSession
                {
                    IdA = Math.Min(idA, idB),
                    IdB = Math.Max(idA, idB),
                    Phase = SocialArgumentPhase.Peak,
                    ExchangeCount = 3,
                    StartGameHours = gameHours,
                    LastBeatGameHours = gameHours,
                    NextBeatAtGameHours = gameHours,
                    AggressorId = idA,
                    FailedDeEscalation = true,
                };
                session.PushTag("FORCE_FIGHT");
                _sessions[pk] = session;
                TotalArgumentsStarted++;
            }
            else
            {
                session.FailedDeEscalation = true;
                session.Phase = SocialArgumentPhase.Peak;
                session.PushTag("FORCE_FIGHT");
            }

            if (!CanStartFight(world, session, a, b, forceFight: true))
                return null;

            var fightEv = RunFight(world, session, a, b, gameHours, forceFight: true);
            if (fightEv != null)
            {
                RunWitnesses(world, session, gameHours, fighting: true);
                PushHistory(fightEv);
                _pendingPresent.Add(fightEv);
            }
            return fightEv;
        }

        /// <summary>
        /// DEV: Deterministic Death. Sets extreme axes, Major negatives, FailedDeEscalation +
        /// FailedIntervention, hot souls; RunFight(forceLethal) MUST produce Death.
        /// Does not change SocialConflictTuning chance constants — only bypasses rolls.
        /// </summary>
        public SocialConflictEvent ForceLethalPipeline(
            SocialAuraWorld world, int killerId, int victimId, float gameHours)
        {
            if (world == null || killerId <= 0 || victimId <= 0 || killerId == victimId)
                return null;
            var killer = world.Get(killerId);
            var victim = world.Get(victimId);
            if (killer?.State == null || victim?.State == null
                || !killer.State.IsAlive || !victim.State.IsAlive)
                return null;

            // Extreme hostility / trust / frustration
            killer.State.Frustration = 95f;
            victim.State.Frustration = 92f;
            var kv = world.Relation(killerId, victimId);
            var vk = world.Relation(victimId, killerId);
            kv.Hostility = 19f;
            vk.Hostility = 18.5f;
            kv.Trust = -12f;
            vk.Trust = -11f;
            kv.Warmth = -10f;
            vk.Warmth = -9f;
            // Keep Respect below murder-block so rivalry ≠ murder does not apply
            kv.Respect = 28f;
            vk.Respect = 26f;

            // Hot + murderous souls
            killer.Stats.Set(WorkerStatId.Bravery, 16);
            killer.Stats.Set(WorkerStatId.Composure, 6);
            killer.Stats.Set(WorkerStatId.Tolerance, 6);
            killer.Stats.Set(WorkerStatId.Empathy, 5);
            victim.Stats.Set(WorkerStatId.Bravery, 14);
            victim.Stats.Set(WorkerStatId.Composure, 7);
            victim.Stats.Set(WorkerStatId.Tolerance, 7);
            victim.Stats.Set(WorkerStatId.Empathy, 7);

            // Plant Major negative memories (murderous history)
            void Plant(int obs, int tgt, SocialMemoryType type)
            {
                world.Memory.Add(new SocialMemoryEntry
                {
                    ObserverId = obs,
                    TargetId = tgt,
                    Type = type,
                    Strength = 0.95f,
                    GameTime = gameHours - 1f,
                    Context = SocialContext.Emergency,
                    SourceRef = "FORCE_LETHAL",
                    Significance = SocialMemorySignificance.Major,
                });
            }
            Plant(victimId, killerId, SocialMemoryType.InsultedMe);
            Plant(victimId, killerId, SocialMemoryType.BlamedMe);
            Plant(victimId, killerId, SocialMemoryType.HurtBy);
            Plant(killerId, victimId, SocialMemoryType.InsultedMe);
            Plant(killerId, victimId, SocialMemoryType.FailedTogether);
            Plant(killerId, victimId, SocialMemoryType.BlamedMe);

            long pk = PairKey(killerId, victimId);
            var session = new SocialArgumentSession
            {
                IdA = Math.Min(killerId, victimId),
                IdB = Math.Max(killerId, victimId),
                Phase = SocialArgumentPhase.Peak,
                ExchangeCount = 4,
                StartGameHours = gameHours,
                LastBeatGameHours = gameHours,
                NextBeatAtGameHours = gameHours,
                AggressorId = killerId,
                FailedDeEscalation = true,
                FailedIntervention = true,
                KillerId = killerId,
                VictimId = victimId,
            };
            session.PushTag("FORCE_LETHAL");
            _sessions[pk] = session;
            TotalArgumentsStarted++;

            var a = world.Get(session.IdA);
            var b = world.Get(session.IdB);
            var fightEv = RunFight(world, session, a, b, gameHours,
                forceFight: true, forceLethal: true);
            if (fightEv != null)
            {
                PushHistory(fightEv);
                _pendingPresent.Add(fightEv);
            }
            return fightEv;
        }

        static void EnsureHostileSeed(
            SocialAuraWorld world, SocialSimActor a, SocialSimActor b, bool forFight)
        {
            float hostMin = forFight
                ? SocialConflictTuning.FightHostilityMin + 1f
                : SocialConflictTuning.HostilityMin + 1f;
            float frMin = forFight
                ? SocialConflictTuning.FightFrustrationMin + 2f
                : SocialConflictTuning.FrustrationMin + 2f;
            float trustMax = forFight
                ? SocialConflictTuning.FightTrustMax - 0.5f
                : SocialConflictTuning.TrustMaxForArgue - 0.5f;

            if (a.State.Frustration < frMin) a.State.Frustration = frMin;
            if (b.State.Frustration < frMin) b.State.Frustration = frMin;

            var ab = world.Relation(a.Id, b.Id);
            var ba = world.Relation(b.Id, a.Id);
            if (ab.Hostility < hostMin) ab.Hostility = hostMin;
            if (ba.Hostility < hostMin) ba.Hostility = hostMin;
            if (ab.Trust > trustMax) ab.Trust = trustMax;
            if (ba.Trust > trustMax) ba.Trust = trustMax;

            if (forFight)
            {
                // Ensure at least one hot soul
                if (!IsHotSoul(a) && !IsHotSoul(b))
                {
                    a.Stats.Set(WorkerStatId.Bravery, SocialConflictTuning.FightBraveryMin);
                    a.Stats.Set(WorkerStatId.Composure, SocialConflictTuning.FightComposureMax);
                    a.Stats.Set(WorkerStatId.Tolerance, SocialConflictTuning.FightToleranceMax);
                }
            }

            if (!HasNegativeHistory(world.Memory, a.Id, b.Id)
                && !HasNegativeHistory(world.Memory, b.Id, a.Id))
            {
                world.Memory.Add(new SocialMemoryEntry
                {
                    ObserverId = a.Id,
                    TargetId = b.Id,
                    Type = SocialMemoryType.InsultedMe,
                    Strength = 0.75f,
                    GameTime = 0f,
                    Context = SocialContext.Emergency,
                    SourceRef = "FORCE_SEED",
                    Significance = SocialMemorySignificance.Major,
                });
            }
        }

        /// <summary>DEV / audit: format recent conflict ring buffer.</summary>
        public string FormatHistory(int max = 12)
        {
            var sb = new StringBuilder(512);
            int start = Mathf.Max(0, _history.Count - max);
            for (int i = start; i < _history.Count; i++)
            {
                var e = _history[i];
                string kind;
                if (e.Outcome == SocialArgumentOutcome.Death || e.IsLethal
                    || e.FightSeverity == SocialFightSeverity.Lethal)
                    kind = "DEATH";
                else if (e.Outcome == SocialArgumentOutcome.CriticalInjury
                         || e.FightSeverity == SocialFightSeverity.Critical)
                    kind = "CRITICAL";
                else if (e.IsSevereFight || e.FightSeverity == SocialFightSeverity.Severe)
                    kind = "SEVERE";
                else if (e.IsFight)
                    kind = "FIGHT";
                else if (e.IsWitness)
                    kind = "WIT";
                else
                    kind = "ARG";

                sb.AppendLine(
                    $"@{e.GameHours:0.##}h {kind} " +
                    $"{e.WorkerA}-{e.WorkerB} {e.Phase}/{e.Outcome} | {e.Line}");
            }
            return sb.ToString();
        }
    }
}
