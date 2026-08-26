using System;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Temporary excavator balance profiles for A/B comparison.
    /// Unspecified stats stay at baseline 10. Debug/dev only — not a permanent sheet.
    /// </summary>
    public enum ExcavatorTestProfile : byte
    {
        /// <summary>Inspector / session default (restored when leaving test profiles).</summary>
        Baseline = 0,
        Brute = 1,
        Technician = 2,
        Professional = 3,
        Cowboy = 4,
        /// <summary>Extremely strong across excavator-relevant stats.</summary>
        Ace = 5,
        /// <summary>Extremely weak across excavator-relevant stats.</summary>
        Green = 6,
    }

    /// <summary>
    /// Balance-lab modes. ROCK / BEDROCK = pure mining performance (conditions reset at start).
    /// ENDURANCE = operational performance (rock→bedrock continuous, conditions carry over).
    /// </summary>
    public enum ExcavatorBalanceTestMode : byte
    {
        Rock = 0,
        Bedrock = 1,
        Endurance = 2,
    }

    /// <summary>
    /// Session metrics for one excavator profile run. No gameplay effects.
    /// </summary>
    [Serializable]
    public sealed class ExcavatorBalanceMetrics
    {
        public int RockDestroyed;
        public int BedrockDestroyed;
        public float RockMiningSeconds;
        public float BedrockMiningSeconds;
        public int WeakPointAttempts;
        public int WeakPointSuccesses;
        public float HeatSum;
        public int HeatSamples;
        public float MaxHeat;
        public float ZoneNormalSec;
        public float ZoneOptimalSec;
        public float ZoneDangerSec;
        public float ZoneExtremeSec;
        public float ZoneOverheatedSec;
        public int OverheatEvents;
        public int InjuryEvents;
        public float FrustrationStart;
        public float FrustrationEnd;
        public float FrustrationGained;
        public float StaminaStart;
        public float StaminaEnd;
        public float StaminaMaxAtStart;
        public float StaminaSum;
        public int StaminaSamples;
        public float MinStamina = float.MaxValue;
        public float IdleTimeSec;
        public float MiningTimeSec;
        public float CoolingTimeSec;
        public float OverheatedLockSec;
        public float SimulationSeconds;

        public float AvgSecondsPerRock =>
            RockDestroyed > 0 ? RockMiningSeconds / RockDestroyed : 0f;

        public float AvgSecondsPerBedrock =>
            BedrockDestroyed > 0 ? BedrockMiningSeconds / BedrockDestroyed : 0f;

        /// <summary>
        /// ACTIVE TILES/MIN — pure mining pace from engaged dig time only
        /// (excludes cooling / rest / idle / overheat lock).
        /// </summary>
        public float RockActiveTilesPerMinute =>
            RockMiningSeconds > 0.001f ? RockDestroyed * 60f / RockMiningSeconds : 0f;

        /// <summary>ACTIVE TILES/MIN for bedrock (engaged dig time only).</summary>
        public float BedrockActiveTilesPerMinute =>
            BedrockMiningSeconds > 0.001f ? BedrockDestroyed * 60f / BedrockMiningSeconds : 0f;

        /// <summary>
        /// OPERATIONAL TILES/MIN — destroyed / total elapsed sim minutes
        /// (includes mining, cooling, resting, idle, overheated, injury downtime).
        /// </summary>
        public float RockOperationalTilesPerMinute =>
            SimulationSeconds > 0.001f ? RockDestroyed * 60f / SimulationSeconds : 0f;

        /// <summary>OPERATIONAL TILES/MIN for bedrock (full elapsed sim time).</summary>
        public float BedrockOperationalTilesPerMinute =>
            SimulationSeconds > 0.001f ? BedrockDestroyed * 60f / SimulationSeconds : 0f;

        /// <summary>Combined operational throughput (rock + bedrock) over elapsed sim time.</summary>
        public float TotalOperationalTilesPerMinute =>
            SimulationSeconds > 0.001f
                ? (RockDestroyed + BedrockDestroyed) * 60f / SimulationSeconds
                : 0f;

        /// <summary>UPTIME % = MiningTime / TotalElapsedSimulationTime × 100.</summary>
        public float UptimePct =>
            SimulationSeconds > 0.001f ? 100f * MiningTimeSec / SimulationSeconds : 0f;

        // Legacy aliases — same values as Active / Operational (kept for older call sites).
        public float RockTilesPerMinute => RockActiveTilesPerMinute;
        public float BedrockTilesPerMinute => BedrockActiveTilesPerMinute;
        public float RockTilesPerMinuteWall => RockOperationalTilesPerMinute;
        public float BedrockTilesPerMinuteWall => BedrockOperationalTilesPerMinute;

        public float WeakPointSuccessPct =>
            WeakPointAttempts > 0 ? 100f * WeakPointSuccesses / WeakPointAttempts : 0f;

        public float AvgHeat =>
            HeatSamples > 0 ? HeatSum / HeatSamples : 0f;

        public float AvgStamina =>
            StaminaSamples > 0 ? StaminaSum / StaminaSamples : 0f;

        public float OverheatsPerMinute =>
            SimulationSeconds > 0.001f ? OverheatEvents * 60f / SimulationSeconds : 0f;

        public float InjuriesPerMinute =>
            SimulationSeconds > 0.001f ? InjuryEvents * 60f / SimulationSeconds : 0f;

        /// <summary>Active dig time attributed to rock or bedrock strikes.</summary>
        public float MaterialMiningSeconds => RockMiningSeconds + BedrockMiningSeconds;

        public float MiningSecondsTotal =>
            ZoneNormalSec + ZoneOptimalSec + ZoneDangerSec + ZoneExtremeSec + ZoneOverheatedSec;

        public float ZonePct(float seconds)
        {
            float t = MiningSecondsTotal;
            return t > 0.001f ? 100f * seconds / t : 0f;
        }

        public void Clear()
        {
            RockDestroyed = 0;
            BedrockDestroyed = 0;
            RockMiningSeconds = 0f;
            BedrockMiningSeconds = 0f;
            WeakPointAttempts = 0;
            WeakPointSuccesses = 0;
            HeatSum = 0f;
            HeatSamples = 0;
            MaxHeat = 0f;
            ZoneNormalSec = 0f;
            ZoneOptimalSec = 0f;
            ZoneDangerSec = 0f;
            ZoneExtremeSec = 0f;
            ZoneOverheatedSec = 0f;
            OverheatEvents = 0;
            InjuryEvents = 0;
            FrustrationStart = 0f;
            FrustrationEnd = 0f;
            FrustrationGained = 0f;
            StaminaStart = 0f;
            StaminaEnd = 0f;
            StaminaMaxAtStart = 0f;
            StaminaSum = 0f;
            StaminaSamples = 0;
            MinStamina = float.MaxValue;
            IdleTimeSec = 0f;
            MiningTimeSec = 0f;
            CoolingTimeSec = 0f;
            OverheatedLockSec = 0f;
            SimulationSeconds = 0f;
        }
    }

    /// <summary>
    /// Temporary debug harness: swap excavator profiles and collect identical-run metrics.
    /// Does not change mining formulas. Restores Baseline sheet when leaving test profiles.
    /// </summary>
    public sealed class ExcavatorBalanceHarness
    {
        public const string LogTag = "BALANCE";

        readonly ExcavatorBalanceMetrics _metrics = new();
        readonly WorkerStats _baselineSheet = new();
        bool _baselineCaptured;
        ExcavatorTestProfile _profile = ExcavatorTestProfile.Baseline;
        FreeWorkerController _worker;
        bool _enabled = true;

        public ExcavatorBalanceMetrics Metrics => _metrics;
        public ExcavatorTestProfile Profile => _profile;
        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public void Bind(FreeWorkerController worker)
        {
            if (_worker != null)
                Unhook(_worker);
            _worker = worker;
            if (_worker == null) return;
            Hook(_worker);
            if (!_baselineCaptured)
            {
                _baselineSheet.CopyFrom(_worker.Stats);
                _baselineCaptured = true;
            }
            SnapshotConditionEnds();
            SnapshotConditionStarts();
        }

        void Hook(FreeWorkerController w)
        {
            w.BalanceDigImpact += OnDigImpact;
            w.BalanceWeakPointChecked += OnWeakPoint;
            w.BalanceOverheatEvent += OnOverheat;
            w.BalanceInjuryEvent += OnInjury;
        }

        void Unhook(FreeWorkerController w)
        {
            w.BalanceDigImpact -= OnDigImpact;
            w.BalanceWeakPointChecked -= OnWeakPoint;
            w.BalanceOverheatEvent -= OnOverheat;
            w.BalanceInjuryEvent -= OnInjury;
        }

        /// <summary>Apply a temporary profile. Baseline restores the captured session sheet.</summary>
        public void ApplyProfile(ExcavatorTestProfile profile)
        {
            if (_worker == null) return;
            if (!_baselineCaptured)
            {
                _baselineSheet.CopyFrom(_worker.Stats);
                _baselineCaptured = true;
            }

            _profile = profile;
            if (profile == ExcavatorTestProfile.Baseline)
            {
                _worker.Stats.CopyFrom(_baselineSheet);
            }
            else
            {
                var sheet = BuildProfile(profile);
                _worker.Stats.CopyFrom(sheet);
            }

            ResetRun(keepProfile: true);
            DigHoodLog.Push($"{LogTag} | PROFILE {ProfileLabel(profile)}");
        }

        /// <summary>
        /// Clear metrics and set Heat / Stamina / Frustration / Injury to clean test values.
        /// Does not rebuild the map.
        /// </summary>
        public void ResetRun(bool keepProfile = true)
        {
            if (_worker == null) return;
            _worker.ResetBalanceTestState();
            _metrics.Clear();
            SnapshotConditionStarts();
            SnapshotConditionEnds();
            DigHoodLog.Push(
                keepProfile
                    ? $"{LogTag} | RESET metrics + conditions | {ProfileLabel(_profile)}"
                    : $"{LogTag} | RESET metrics + conditions");
        }

        public void Tick(float dt)
        {
            if (!_enabled || _worker == null) return;

            SnapshotConditionEnds();
            if (dt <= 0f) return;

            _metrics.SimulationSeconds += dt;

            float heat = _worker.Heat;
            _metrics.HeatSum += heat;
            _metrics.HeatSamples++;
            if (heat > _metrics.MaxHeat)
                _metrics.MaxHeat = heat;

            float stam = _worker.CurrentStamina;
            _metrics.StaminaSum += stam;
            _metrics.StaminaSamples++;
            if (stam < _metrics.MinStamina)
                _metrics.MinStamina = stam;

            float frustGain = _metrics.FrustrationEnd - _metrics.FrustrationStart;
            if (frustGain > _metrics.FrustrationGained)
                _metrics.FrustrationGained = frustGain;

            if (_worker.IsOverheated)
                _metrics.OverheatedLockSec += dt;
            else if (_worker.IsCooling)
                _metrics.CoolingTimeSec += dt;
            else if (_worker.IsActivelyDigging)
                _metrics.MiningTimeSec += dt;
            else
                _metrics.IdleTimeSec += dt;

            if (!_worker.IsActivelyDigging && !_worker.IsCooling)
                return;

            // Mining-time zone mix (includes engaged dig waits + intentional cooling pauses)
            if (_worker.IsActivelyDigging)
            {
                switch (_worker.HeatZone)
                {
                    case ExcavatorHeatZone.Optimal:
                        _metrics.ZoneOptimalSec += dt;
                        break;
                    case ExcavatorHeatZone.Danger:
                        _metrics.ZoneDangerSec += dt;
                        break;
                    case ExcavatorHeatZone.Extreme:
                        _metrics.ZoneExtremeSec += dt;
                        break;
                    case ExcavatorHeatZone.Overheated:
                        _metrics.ZoneOverheatedSec += dt;
                        break;
                    default:
                        _metrics.ZoneNormalSec += dt;
                        break;
                }

                // Attribute active dig time to last strike material for avg seconds / tile
                if (_worker.HasLastDig)
                {
                    if (_worker.LastDigMaterial == TerrainMaterial.Bedrock)
                        _metrics.BedrockMiningSeconds += dt;
                    else
                        _metrics.RockMiningSeconds += dt;
                }
            }
        }

        void OnDigImpact(TerrainCell before, bool broke)
        {
            if (!broke) return;
            if (before.Material == TerrainMaterial.Bedrock || before.BedrockCount >= 2)
                _metrics.BedrockDestroyed++;
            else
                _metrics.RockDestroyed++;
        }

        void OnWeakPoint(bool success)
        {
            _metrics.WeakPointAttempts++;
            if (success) _metrics.WeakPointSuccesses++;
        }

        void OnOverheat() => _metrics.OverheatEvents++;

        void OnInjury() => _metrics.InjuryEvents++;

        void SnapshotConditionStarts()
        {
            if (_worker == null) return;
            _metrics.FrustrationStart = _worker.Conditions.Frustration;
            _metrics.StaminaStart = _worker.CurrentStamina;
            _metrics.StaminaMaxAtStart = _worker.MaxStamina;
        }

        void SnapshotConditionEnds()
        {
            if (_worker == null) return;
            _metrics.FrustrationEnd = _worker.Conditions.Frustration;
            _metrics.StaminaEnd = _worker.CurrentStamina;
        }

        public static string ProfileLabel(ExcavatorTestProfile p) => p switch
        {
            ExcavatorTestProfile.Brute => "BRUTE",
            ExcavatorTestProfile.Technician => "TECHNICIAN",
            ExcavatorTestProfile.Professional => "PROFESSIONAL",
            ExcavatorTestProfile.Cowboy => "COWBOY",
            ExcavatorTestProfile.Ace => "ACE",
            ExcavatorTestProfile.Green => "GREEN",
            _ => "BASELINE",
        };

        public static string ModeLabel(ExcavatorBalanceTestMode mode) => mode switch
        {
            ExcavatorBalanceTestMode.Rock => "ROCK",
            ExcavatorBalanceTestMode.Bedrock => "BEDROCK",
            ExcavatorBalanceTestMode.Endurance => "ENDURANCE",
            _ => "?",
        };

        public static string ModeKindLabel(ExcavatorBalanceTestMode mode) =>
            mode == ExcavatorBalanceTestMode.Endurance
                ? "OPERATIONAL PERFORMANCE"
                : "PURE MINING PERFORMANCE";

        public static WorkerStats BuildProfile(ExcavatorTestProfile profile)
        {
            var s = WorkerStats.CreateBaseline();
            switch (profile)
            {
                case ExcavatorTestProfile.Brute:
                    s.Set(WorkerStatId.RawPower, 18);
                    s.Set(WorkerStatId.Lithology, 6);
                    s.Set(WorkerStatId.Finesse, 6);
                    s.Set(WorkerStatId.SpatialGeometry, 6);
                    s.Set(WorkerStatId.Rhythm, 12);
                    break;
                case ExcavatorTestProfile.Technician:
                    s.Set(WorkerStatId.RawPower, 8);
                    s.Set(WorkerStatId.Lithology, 18);
                    s.Set(WorkerStatId.Finesse, 18);
                    s.Set(WorkerStatId.SpatialGeometry, 18);
                    s.Set(WorkerStatId.Focus, 16);
                    break;
                case ExcavatorTestProfile.Professional:
                    s.Set(WorkerStatId.SafetyProtocol, 18);
                    s.Set(WorkerStatId.Rhythm, 17);
                    s.Set(WorkerStatId.Composure, 16);
                    s.Set(WorkerStatId.Determination, 12);
                    break;
                case ExcavatorTestProfile.Cowboy:
                    s.Set(WorkerStatId.SafetyProtocol, 5);
                    s.Set(WorkerStatId.Rhythm, 15);
                    s.Set(WorkerStatId.Determination, 19);
                    s.Set(WorkerStatId.Composure, 17);
                    break;
                case ExcavatorTestProfile.Ace:
                    // Diagnostic ceiling — every excavator-relevant stat = 19
                    s.Set(WorkerStatId.RawPower, 19);
                    s.Set(WorkerStatId.Stamina, 19);
                    s.Set(WorkerStatId.Recovery, 19);
                    s.Set(WorkerStatId.Finesse, 19);
                    s.Set(WorkerStatId.Rhythm, 19);
                    s.Set(WorkerStatId.HeatTolerance, 19);
                    s.Set(WorkerStatId.Toughness, 19);
                    s.Set(WorkerStatId.Lithology, 19);
                    s.Set(WorkerStatId.SpatialGeometry, 19);
                    s.Set(WorkerStatId.SafetyProtocol, 19);
                    s.Set(WorkerStatId.Composure, 19);
                    s.Set(WorkerStatId.Focus, 19);
                    s.Set(WorkerStatId.Determination, 19);
                    break;
                case ExcavatorTestProfile.Green:
                    // Diagnostic floor — every excavator-relevant stat = 3
                    s.Set(WorkerStatId.RawPower, 3);
                    s.Set(WorkerStatId.Stamina, 3);
                    s.Set(WorkerStatId.Recovery, 3);
                    s.Set(WorkerStatId.Finesse, 3);
                    s.Set(WorkerStatId.Rhythm, 3);
                    s.Set(WorkerStatId.HeatTolerance, 3);
                    s.Set(WorkerStatId.Toughness, 3);
                    s.Set(WorkerStatId.Lithology, 3);
                    s.Set(WorkerStatId.SpatialGeometry, 3);
                    s.Set(WorkerStatId.SafetyProtocol, 3);
                    s.Set(WorkerStatId.Composure, 3);
                    s.Set(WorkerStatId.Focus, 3);
                    s.Set(WorkerStatId.Determination, 3);
                    break;
            }
            return s;
        }
    }
}
