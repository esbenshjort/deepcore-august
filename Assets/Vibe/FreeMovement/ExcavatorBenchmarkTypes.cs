using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>One excavator run inside an automated benchmark.</summary>
    [Serializable]
    public sealed class ExcavatorBenchmarkRunRecord
    {
        public ExcavatorBalanceTestMode Mode;
        public ExcavatorTestProfile Profile;
        public int RunIndex;
        public int Seed;
        public ExcavatorBalanceMetrics Metrics = new();
    }

    /// <summary>Mean / median / min / max / stdev for one scalar across runs.</summary>
    [Serializable]
    public struct MetricAggregate
    {
        public float Mean;
        public float Median;
        public float Min;
        public float Max;
        public float StdDev;
        public int Samples;

        public static MetricAggregate From(List<float> values)
        {
            var a = new MetricAggregate();
            if (values == null || values.Count == 0)
            {
                a.Min = 0f;
                a.Max = 0f;
                return a;
            }

            values.Sort();
            a.Samples = values.Count;
            a.Min = values[0];
            a.Max = values[values.Count - 1];
            double sum = 0;
            for (int i = 0; i < values.Count; i++)
                sum += values[i];
            a.Mean = (float)(sum / values.Count);

            if (values.Count % 2 == 1)
                a.Median = values[values.Count / 2];
            else
                a.Median = 0.5f * (values[values.Count / 2 - 1] + values[values.Count / 2]);

            if (values.Count > 1)
            {
                double var = 0;
                for (int i = 0; i < values.Count; i++)
                {
                    double d = values[i] - a.Mean;
                    var += d * d;
                }
                a.StdDev = (float)Math.Sqrt(var / (values.Count - 1));
            }

            return a;
        }
    }

    /// <summary>Aggregates for one profile × one test mode.</summary>
    [Serializable]
    public sealed class ExcavatorBenchmarkProfileAggregate
    {
        public ExcavatorTestProfile Profile;
        public ExcavatorBalanceTestMode Mode;
        public int Runs;
        public int TotalRockDestroyed;
        public int TotalBedrockDestroyed;

        public MetricAggregate RockTilesPerMin;
        public MetricAggregate BedrockTilesPerMin;
        public MetricAggregate RockActiveTilesPerMin;
        public MetricAggregate BedrockActiveTilesPerMin;
        public MetricAggregate RockOperationalTilesPerMin;
        public MetricAggregate BedrockOperationalTilesPerMin;
        public MetricAggregate TotalOperationalTilesPerMin;
        public MetricAggregate UptimePct;
        public MetricAggregate SecPerRock;
        public MetricAggregate SecPerBedrock;
        public MetricAggregate WeakPointPct;
        public MetricAggregate AvgHeat;
        public MetricAggregate MaxHeat;
        public MetricAggregate OverheatsPerMin;
        public MetricAggregate AvgStamina;
        public MetricAggregate MinStamina;
        public MetricAggregate InjuriesPerMin;
        public MetricAggregate FrustrationGained;
        public MetricAggregate MiningTime;
        public MetricAggregate CoolingTime;
        public MetricAggregate OverheatedLock;
        public MetricAggregate IdleTime;
    }

    /// <summary>Full benchmark session results + CSV export.</summary>
    [Serializable]
    public sealed class ExcavatorBenchmarkSession
    {
        public int BaseSeed;
        public float RunDurationSeconds;
        public int RunsPerExcavator;
        public float SimStepSeconds;
        public string GreenBedrockDiagnostic;
        public readonly List<ExcavatorBenchmarkRunRecord> Runs = new();
        public readonly List<ExcavatorBenchmarkProfileAggregate> Aggregates = new();

        public void RebuildAggregates(ExcavatorTestProfile[] profiles, ExcavatorBalanceTestMode[] modes)
        {
            Aggregates.Clear();
            foreach (var mode in modes)
            foreach (var profile in profiles)
            {
                var subset = new List<ExcavatorBenchmarkRunRecord>();
                for (int i = 0; i < Runs.Count; i++)
                {
                    var r = Runs[i];
                    if (r.Mode == mode && r.Profile == profile)
                        subset.Add(r);
                }
                if (subset.Count == 0) continue;
                Aggregates.Add(BuildAggregate(profile, mode, subset));
            }
        }

        static ExcavatorBenchmarkProfileAggregate BuildAggregate(
            ExcavatorTestProfile profile,
            ExcavatorBalanceTestMode mode,
            List<ExcavatorBenchmarkRunRecord> subset)
        {
            var agg = new ExcavatorBenchmarkProfileAggregate
            {
                Profile = profile,
                Mode = mode,
                Runs = subset.Count,
            };

            var rockTpm = new List<float>(subset.Count);
            var bedTpm = new List<float>(subset.Count);
            var rockActive = new List<float>(subset.Count);
            var bedActive = new List<float>(subset.Count);
            var rockOps = new List<float>(subset.Count);
            var bedOps = new List<float>(subset.Count);
            var totalOps = new List<float>(subset.Count);
            var uptime = new List<float>(subset.Count);
            var secRock = new List<float>(subset.Count);
            var secBed = new List<float>(subset.Count);
            var wp = new List<float>(subset.Count);
            var avgHeat = new List<float>(subset.Count);
            var maxHeat = new List<float>(subset.Count);
            var ohPm = new List<float>(subset.Count);
            var avgStam = new List<float>(subset.Count);
            var minStam = new List<float>(subset.Count);
            var injPm = new List<float>(subset.Count);
            var frust = new List<float>(subset.Count);
            var mine = new List<float>(subset.Count);
            var cool = new List<float>(subset.Count);
            var ohLock = new List<float>(subset.Count);
            var idle = new List<float>(subset.Count);

            for (int i = 0; i < subset.Count; i++)
            {
                var m = subset[i].Metrics;
                agg.TotalRockDestroyed += m.RockDestroyed;
                agg.TotalBedrockDestroyed += m.BedrockDestroyed;
                rockTpm.Add(m.RockActiveTilesPerMinute);
                bedTpm.Add(m.BedrockActiveTilesPerMinute);
                rockActive.Add(m.RockActiveTilesPerMinute);
                bedActive.Add(m.BedrockActiveTilesPerMinute);
                rockOps.Add(m.RockOperationalTilesPerMinute);
                bedOps.Add(m.BedrockOperationalTilesPerMinute);
                totalOps.Add(m.TotalOperationalTilesPerMinute);
                uptime.Add(m.UptimePct);
                secRock.Add(m.AvgSecondsPerRock);
                secBed.Add(m.AvgSecondsPerBedrock);
                wp.Add(m.WeakPointSuccessPct);
                avgHeat.Add(m.AvgHeat);
                maxHeat.Add(m.MaxHeat);
                ohPm.Add(m.OverheatsPerMinute);
                avgStam.Add(m.AvgStamina);
                minStam.Add(m.MinStamina >= float.MaxValue * 0.5f ? m.StaminaEnd : m.MinStamina);
                injPm.Add(m.InjuriesPerMinute);
                frust.Add(Mathf.Max(0f, m.FrustrationEnd - m.FrustrationStart));
                mine.Add(m.MiningTimeSec);
                cool.Add(m.CoolingTimeSec);
                ohLock.Add(m.OverheatedLockSec);
                idle.Add(m.IdleTimeSec);
            }

            agg.RockTilesPerMin = MetricAggregate.From(rockTpm);
            agg.BedrockTilesPerMin = MetricAggregate.From(bedTpm);
            agg.RockActiveTilesPerMin = MetricAggregate.From(rockActive);
            agg.BedrockActiveTilesPerMin = MetricAggregate.From(bedActive);
            agg.RockOperationalTilesPerMin = MetricAggregate.From(rockOps);
            agg.BedrockOperationalTilesPerMin = MetricAggregate.From(bedOps);
            agg.TotalOperationalTilesPerMin = MetricAggregate.From(totalOps);
            agg.UptimePct = MetricAggregate.From(uptime);
            agg.SecPerRock = MetricAggregate.From(secRock);
            agg.SecPerBedrock = MetricAggregate.From(secBed);
            agg.WeakPointPct = MetricAggregate.From(wp);
            agg.AvgHeat = MetricAggregate.From(avgHeat);
            agg.MaxHeat = MetricAggregate.From(maxHeat);
            agg.OverheatsPerMin = MetricAggregate.From(ohPm);
            agg.AvgStamina = MetricAggregate.From(avgStam);
            agg.MinStamina = MetricAggregate.From(minStam);
            agg.InjuriesPerMin = MetricAggregate.From(injPm);
            agg.FrustrationGained = MetricAggregate.From(frust);
            agg.MiningTime = MetricAggregate.From(mine);
            agg.CoolingTime = MetricAggregate.From(cool);
            agg.OverheatedLock = MetricAggregate.From(ohLock);
            agg.IdleTime = MetricAggregate.From(idle);
            return agg;
        }

        public ExcavatorBenchmarkProfileAggregate FindAggregate(
            ExcavatorBalanceTestMode mode, ExcavatorTestProfile profile)
        {
            for (int i = 0; i < Aggregates.Count; i++)
            {
                var a = Aggregates[i];
                if (a.Mode == mode && a.Profile == profile)
                    return a;
            }
            return null;
        }

        /// <summary>Writes raw runs CSV + aggregates CSV. Returns paths.</summary>
        public (string rawPath, string aggPath) ExportCsv(string directory)
        {
            Directory.CreateDirectory(directory);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string rawPath = Path.Combine(directory, $"excavator_benchmark_runs_{stamp}.csv");
            string aggPath = Path.Combine(directory, $"excavator_benchmark_aggregates_{stamp}.csv");

            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(64 * 1024);
            sb.AppendLine(
                "test_type,excavator,run_number,seed,sim_seconds," +
                "rock_destroyed,bedrock_destroyed," +
                "rock_active_tiles_per_min,bedrock_active_tiles_per_min," +
                "rock_operational_tiles_per_min,bedrock_operational_tiles_per_min,total_operational_tiles_per_min," +
                "uptime_pct," +
                "avg_sec_per_rock,avg_sec_per_bedrock," +
                "wp_attempts,wp_successes,wp_success_pct," +
                "avg_heat,max_heat," +
                "zone_normal_sec,zone_optimal_sec,zone_danger_sec,zone_extreme_sec,zone_overheated_sec," +
                "overheats,overheats_per_min," +
                "avg_stamina,min_stamina,final_stamina,stamina_start," +
                "frustration_start,frustration_end,frustration_gained," +
                "injuries,injuries_per_min," +
                "idle_sec,mining_sec,cooling_sec,overheated_lock_sec");

            for (int i = 0; i < Runs.Count; i++)
            {
                var r = Runs[i];
                var m = r.Metrics;
                float frustGain = Mathf.Max(0f, m.FrustrationEnd - m.FrustrationStart);
                float minStam = m.MinStamina >= float.MaxValue * 0.5f ? m.StaminaEnd : m.MinStamina;
                sb.Append(ExcavatorBalanceHarness.ModeLabel(r.Mode)).Append(',');
                sb.Append(ExcavatorBalanceHarness.ProfileLabel(r.Profile)).Append(',');
                sb.Append(r.RunIndex.ToString(inv)).Append(',');
                sb.Append(r.Seed.ToString(inv)).Append(',');
                sb.Append(m.SimulationSeconds.ToString("0.###", inv)).Append(',');
                sb.Append(m.RockDestroyed).Append(',');
                sb.Append(m.BedrockDestroyed).Append(',');
                sb.Append(m.RockActiveTilesPerMinute.ToString("0.####", inv)).Append(',');
                sb.Append(m.BedrockActiveTilesPerMinute.ToString("0.####", inv)).Append(',');
                sb.Append(m.RockOperationalTilesPerMinute.ToString("0.####", inv)).Append(',');
                sb.Append(m.BedrockOperationalTilesPerMinute.ToString("0.####", inv)).Append(',');
                sb.Append(m.TotalOperationalTilesPerMinute.ToString("0.####", inv)).Append(',');
                sb.Append(m.UptimePct.ToString("0.####", inv)).Append(',');
                sb.Append(m.AvgSecondsPerRock.ToString("0.####", inv)).Append(',');
                sb.Append(m.AvgSecondsPerBedrock.ToString("0.####", inv)).Append(',');
                sb.Append(m.WeakPointAttempts).Append(',');
                sb.Append(m.WeakPointSuccesses).Append(',');
                sb.Append(m.WeakPointSuccessPct.ToString("0.####", inv)).Append(',');
                sb.Append(m.AvgHeat.ToString("0.####", inv)).Append(',');
                sb.Append(m.MaxHeat.ToString("0.####", inv)).Append(',');
                sb.Append(m.ZoneNormalSec.ToString("0.####", inv)).Append(',');
                sb.Append(m.ZoneOptimalSec.ToString("0.####", inv)).Append(',');
                sb.Append(m.ZoneDangerSec.ToString("0.####", inv)).Append(',');
                sb.Append(m.ZoneExtremeSec.ToString("0.####", inv)).Append(',');
                sb.Append(m.ZoneOverheatedSec.ToString("0.####", inv)).Append(',');
                sb.Append(m.OverheatEvents).Append(',');
                sb.Append(m.OverheatsPerMinute.ToString("0.####", inv)).Append(',');
                sb.Append(m.AvgStamina.ToString("0.####", inv)).Append(',');
                sb.Append(minStam.ToString("0.####", inv)).Append(',');
                sb.Append(m.StaminaEnd.ToString("0.####", inv)).Append(',');
                sb.Append(m.StaminaStart.ToString("0.####", inv)).Append(',');
                sb.Append(m.FrustrationStart.ToString("0.####", inv)).Append(',');
                sb.Append(m.FrustrationEnd.ToString("0.####", inv)).Append(',');
                sb.Append(frustGain.ToString("0.####", inv)).Append(',');
                sb.Append(m.InjuryEvents).Append(',');
                sb.Append(m.InjuriesPerMinute.ToString("0.####", inv)).Append(',');
                sb.Append(m.IdleTimeSec.ToString("0.####", inv)).Append(',');
                sb.Append(m.MiningTimeSec.ToString("0.####", inv)).Append(',');
                sb.Append(m.CoolingTimeSec.ToString("0.####", inv)).Append(',');
                sb.Append(m.OverheatedLockSec.ToString("0.####", inv));
                sb.AppendLine();
            }
            File.WriteAllText(rawPath, sb.ToString());

            sb.Clear();
            sb.AppendLine(
                "test_type,excavator,runs,total_rock,total_bedrock," +
                "metric,mean,median,min,max,std_dev");

            void AggRows(ExcavatorBenchmarkProfileAggregate a)
            {
                void Row(string name, MetricAggregate m)
                {
                    sb.Append(ExcavatorBalanceHarness.ModeLabel(a.Mode)).Append(',');
                    sb.Append(ExcavatorBalanceHarness.ProfileLabel(a.Profile)).Append(',');
                    sb.Append(a.Runs).Append(',');
                    sb.Append(a.TotalRockDestroyed).Append(',');
                    sb.Append(a.TotalBedrockDestroyed).Append(',');
                    sb.Append(name).Append(',');
                    sb.Append(m.Mean.ToString("0.####", inv)).Append(',');
                    sb.Append(m.Median.ToString("0.####", inv)).Append(',');
                    sb.Append(m.Min.ToString("0.####", inv)).Append(',');
                    sb.Append(m.Max.ToString("0.####", inv)).Append(',');
                    sb.Append(m.StdDev.ToString("0.####", inv));
                    sb.AppendLine();
                }

                // Legacy names kept as active aliases for older sheets
                Row("rock_active_tiles_per_min", a.RockActiveTilesPerMin);
                Row("bedrock_active_tiles_per_min", a.BedrockActiveTilesPerMin);
                Row("rock_operational_tiles_per_min", a.RockOperationalTilesPerMin);
                Row("bedrock_operational_tiles_per_min", a.BedrockOperationalTilesPerMin);
                Row("total_operational_tiles_per_min", a.TotalOperationalTilesPerMin);
                Row("uptime_pct", a.UptimePct);
                Row("rock_tiles_per_min", a.RockTilesPerMin); // = active
                Row("bedrock_tiles_per_min", a.BedrockTilesPerMin); // = active
                Row("sec_per_rock", a.SecPerRock);
                Row("sec_per_bedrock", a.SecPerBedrock);
                Row("weak_point_pct", a.WeakPointPct);
                Row("avg_heat", a.AvgHeat);
                Row("max_heat", a.MaxHeat);
                Row("overheats_per_min", a.OverheatsPerMin);
                Row("avg_stamina", a.AvgStamina);
                Row("min_stamina", a.MinStamina);
                Row("injuries_per_min", a.InjuriesPerMin);
                Row("frustration_gained", a.FrustrationGained);
                Row("mining_sec", a.MiningTime);
                Row("cooling_sec", a.CoolingTime);
                Row("overheated_lock_sec", a.OverheatedLock);
                Row("idle_sec", a.IdleTime);
            }

            for (int i = 0; i < Aggregates.Count; i++)
                AggRows(Aggregates[i]);

            File.WriteAllText(aggPath, sb.ToString());
            return (rawPath, aggPath);
        }
    }
}
