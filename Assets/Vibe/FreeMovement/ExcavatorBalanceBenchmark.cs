using System.Collections;
using System.IO;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Headless / fast-forward excavator benchmark using the same FreeWorkerController + terrain
    /// systems as the live compare scene. Measurement only — no balance changes.
    /// </summary>
    public sealed class ExcavatorBalanceBenchmark
    {
        public const int DefaultRuns = 10;
        public const float DefaultDuration = 300f;
        public const int DefaultBaseSeed = 42001;
        public const float DefaultSimStep = 1f / 20f;
        /// <summary>Simulated seconds advanced per Unity frame while benchmarking.</summary>
        public const float DefaultSimSecondsPerFrame = 12f;

        public const int QuickRuns = 3;
        public const float QuickDuration = 30f;

        static readonly ExcavatorTestProfile[] Profiles =
        {
            ExcavatorTestProfile.Ace,
            ExcavatorTestProfile.Brute,
            ExcavatorTestProfile.Technician,
            ExcavatorTestProfile.Professional,
            ExcavatorTestProfile.Cowboy,
            ExcavatorTestProfile.Green,
        };

        static readonly ExcavatorBalanceTestMode[] Modes =
        {
            ExcavatorBalanceTestMode.Rock,
            ExcavatorBalanceTestMode.Bedrock,
            ExcavatorBalanceTestMode.Endurance,
        };

        public enum State : byte
        {
            Idle = 0,
            Running = 1,
            Done = 2,
            Cancelled = 3,
        }

        public State Status { get; private set; }
        public string ProgressLabel { get; private set; } = "";
        public float Progress01 { get; private set; }
        public ExcavatorBenchmarkSession Session { get; private set; }
        public string LastRawCsvPath { get; private set; }
        public string LastAggCsvPath { get; private set; }

        MonoBehaviour _host;
        Coroutine _routine;
        bool _cancel;

        public bool IsRunning => Status == State.Running;

        public void Bind(MonoBehaviour host) => _host = host;

        public void Cancel() => _cancel = true;

        public void StartQuick() =>
            Start(QuickRuns, QuickDuration, DefaultBaseSeed, DefaultSimStep, DefaultSimSecondsPerFrame);

        public void StartFull() =>
            Start(DefaultRuns, DefaultDuration, DefaultBaseSeed, DefaultSimStep, DefaultSimSecondsPerFrame);

        public void Start(
            int runsPerExcavator,
            float durationSeconds,
            int baseSeed,
            float simStep,
            float simSecondsPerFrame)
        {
            if (_host == null)
            {
                Debug.LogError("ExcavatorBalanceBenchmark: host MonoBehaviour not bound.");
                return;
            }
            if (Status == State.Running)
            {
                Debug.LogWarning("Benchmark already running.");
                return;
            }

            _cancel = false;
            if (_routine != null)
                _host.StopCoroutine(_routine);
            _routine = _host.StartCoroutine(
                RunSession(runsPerExcavator, durationSeconds, baseSeed, simStep, simSecondsPerFrame));
        }

        IEnumerator RunSession(
            int runsPer,
            float duration,
            int baseSeed,
            float simStep,
            float simPerFrame)
        {
            Status = State.Running;
            Progress01 = 0f;
            ProgressLabel = "Starting…";
            DigHoodLog.MirrorToConsole = false;

            Session = new ExcavatorBenchmarkSession
            {
                BaseSeed = baseSeed,
                RunDurationSeconds = duration,
                RunsPerExcavator = runsPer,
                SimStepSeconds = simStep,
            };

            int totalJobs = Modes.Length * Profiles.Length * runsPer;
            int doneJobs = 0;
            ExcavatorBalanceMetrics greenBedrockSample = null;

            var root = new GameObject("BenchmarkSimRoot");
            root.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                foreach (var mode in Modes)
                {
                    for (int run = 1; run <= runsPer; run++)
                    {
                        int seed = baseSeed + run * 9973 + (int)mode * 131;

                        foreach (var profile in Profiles)
                        {
                            if (_cancel)
                            {
                                Status = State.Cancelled;
                                ProgressLabel = "Cancelled";
                                yield break;
                            }

                            ProgressLabel =
                                $"{ExcavatorBalanceHarness.ModeLabel(mode)} · " +
                                $"{ExcavatorBalanceHarness.ProfileLabel(profile)} · run {run}/{runsPer}";
                            Progress01 = totalJobs > 0 ? doneJobs / (float)totalJobs : 0f;

                            ExcavatorBenchmarkRunRecord record = null;
                            yield return SimulateOneCoroutine(
                                root.transform, mode, profile, run, seed, duration, simStep, simPerFrame,
                                r => record = r);

                            if (record != null)
                            {
                                Session.Runs.Add(record);
                                if (mode == ExcavatorBalanceTestMode.Bedrock &&
                                    profile == ExcavatorTestProfile.Green &&
                                    greenBedrockSample == null)
                                {
                                    greenBedrockSample = record.Metrics;
                                }
                            }

                            doneJobs++;
                            Progress01 = totalJobs > 0 ? doneJobs / (float)totalJobs : 1f;
                            yield return null;
                        }
                    }
                }

                Session.RebuildAggregates(Profiles, Modes);
                Session.GreenBedrockDiagnostic =
                    ExcavatorGreenBedrockDiagnostic.RunAndLog(greenBedrockSample);

                var paths = Session.ExportCsv(DefaultExportDirectory());
                LastRawCsvPath = paths.rawPath;
                LastAggCsvPath = paths.aggPath;
                Debug.Log($"[BENCHMARK] Raw CSV: {LastRawCsvPath}");
                Debug.Log($"[BENCHMARK] Aggregate CSV: {LastAggCsvPath}");

                yield return VerifyDeterminismSample(root.transform, duration, simStep);

                Status = State.Done;
                ProgressLabel = "Complete";
                Progress01 = 1f;
                Debug.Log("[BENCHMARK] Session complete. Open RESULTS in the Balance Compare HUD or press B.");
            }
            finally
            {
                WorkerRoll.EndSeeded();
                WorkerSimClock.ClearOverride();
                if (root != null)
                    Object.DestroyImmediate(root);
                _routine = null;
            }
        }

        IEnumerator VerifyDeterminismSample(Transform parent, float duration, float simStep)
        {
            float probeDur = Mathf.Min(5f, duration);
            int seed = DefaultBaseSeed + 1 * 9973 + (int)ExcavatorBalanceTestMode.Rock * 131;

            ExcavatorBenchmarkRunRecord a = null;
            ExcavatorBenchmarkRunRecord b = null;
            yield return SimulateOneCoroutine(parent, ExcavatorBalanceTestMode.Rock,
                ExcavatorTestProfile.Ace, 1, seed, probeDur, simStep, probeDur, r => a = r);
            yield return SimulateOneCoroutine(parent, ExcavatorBalanceTestMode.Rock,
                ExcavatorTestProfile.Ace, 1, seed, probeDur, simStep, probeDur, r => b = r);

            if (a == null || b == null)
            {
                Debug.LogWarning("[BENCHMARK] Determinism sample skipped (null records).");
                yield break;
            }

            bool match =
                a.Metrics.RockDestroyed == b.Metrics.RockDestroyed &&
                a.Metrics.WeakPointAttempts == b.Metrics.WeakPointAttempts &&
                a.Metrics.WeakPointSuccesses == b.Metrics.WeakPointSuccesses &&
                Mathf.Abs(a.Metrics.MaxHeat - b.Metrics.MaxHeat) < 0.01f;

            Debug.Log(match
                ? $"[BENCHMARK] Determinism OK (ACE ROCK seed {seed}, {probeDur:0.#}s replay matched)."
                : $"[BENCHMARK] Determinism FAIL seed {seed}: " +
                  $"A rock={a.Metrics.RockDestroyed} wp={a.Metrics.WeakPointSuccesses}/{a.Metrics.WeakPointAttempts} maxH={a.Metrics.MaxHeat:0.##} | " +
                  $"B rock={b.Metrics.RockDestroyed} wp={b.Metrics.WeakPointSuccesses}/{b.Metrics.WeakPointAttempts} maxH={b.Metrics.MaxHeat:0.##}");
        }

        IEnumerator SimulateOneCoroutine(
            Transform parent,
            ExcavatorBalanceTestMode mode,
            ExcavatorTestProfile profile,
            int runIndex,
            int seed,
            float duration,
            float simStep,
            float simPerFrame,
            System.Action<ExcavatorBenchmarkRunRecord> onDone)
        {
            WorkerRoll.BeginSeeded(seed);

            float cellSize = 0.12f;
            float radius = ExcavatorBalanceLabTerrain.CellsAcross * 0.5f * cellSize;
            int th = ExcavatorBalanceLabTerrain.WorldHeight(mode);

            var laneGo = new GameObject($"Sim_{ExcavatorBalanceHarness.ProfileLabel(profile)}_{runIndex}");
            laneGo.transform.SetParent(parent, false);

            var world = new FineTerrainWorld(ExcavatorBalanceLabTerrain.Width, th, cellSize);
            ExcavatorBalanceLabTerrain.Carve(world, mode);
            Vector2 start = ExcavatorBalanceLabTerrain.StartLocal(world);
            Vector2 goal = ExcavatorBalanceLabTerrain.GoalLocal(world, mode);

            var workerGo = new GameObject("Excavator");
            workerGo.transform.SetParent(laneGo.transform, false);
            var worker = workerGo.AddComponent<FreeWorkerController>();
            worker.Setup(world, start, radius, null, onBrokeCell: null, onDigImpact: null, pinSprite: null);

            var harness = new ExcavatorBalanceHarness();
            harness.Bind(worker);
            harness.ApplyProfile(profile);
            worker.SetGoal(goal);
            worker.transform.up = Vector2.up;

            float simT = 0f;
            float sinceYield = 0f;
            while (simT < duration - 0.0001f)
            {
                if (_cancel) break;

                float step = Mathf.Min(simStep, duration - simT);
                WorkerSimClock.SetOverride(step);
                if (!worker.HasGoal)
                    worker.SetGoal(goal);
                worker.Tick(Vector2.zero, clearGoalOnWasd: false);
                harness.Tick(step);
                simT += step;
                sinceYield += step;

                if (sinceYield >= simPerFrame)
                {
                    sinceYield = 0f;
                    yield return null;
                }
            }

            harness.Metrics.SimulationSeconds = simT;
            harness.Tick(0f);

            var record = new ExcavatorBenchmarkRunRecord
            {
                Mode = mode,
                Profile = profile,
                RunIndex = runIndex,
                Seed = seed,
            };
            CopyMetrics(harness.Metrics, record.Metrics);

            WorkerRoll.EndSeeded();
            WorkerSimClock.ClearOverride();
            Object.DestroyImmediate(laneGo);
            onDone?.Invoke(record);
        }

        static void CopyMetrics(ExcavatorBalanceMetrics src, ExcavatorBalanceMetrics dst)
        {
            dst.Clear();
            dst.RockDestroyed = src.RockDestroyed;
            dst.BedrockDestroyed = src.BedrockDestroyed;
            dst.RockMiningSeconds = src.RockMiningSeconds;
            dst.BedrockMiningSeconds = src.BedrockMiningSeconds;
            dst.WeakPointAttempts = src.WeakPointAttempts;
            dst.WeakPointSuccesses = src.WeakPointSuccesses;
            dst.HeatSum = src.HeatSum;
            dst.HeatSamples = src.HeatSamples;
            dst.MaxHeat = src.MaxHeat;
            dst.ZoneNormalSec = src.ZoneNormalSec;
            dst.ZoneOptimalSec = src.ZoneOptimalSec;
            dst.ZoneDangerSec = src.ZoneDangerSec;
            dst.ZoneExtremeSec = src.ZoneExtremeSec;
            dst.ZoneOverheatedSec = src.ZoneOverheatedSec;
            dst.OverheatEvents = src.OverheatEvents;
            dst.InjuryEvents = src.InjuryEvents;
            dst.FrustrationStart = src.FrustrationStart;
            dst.FrustrationEnd = src.FrustrationEnd;
            dst.FrustrationGained = src.FrustrationGained;
            dst.StaminaStart = src.StaminaStart;
            dst.StaminaEnd = src.StaminaEnd;
            dst.StaminaMaxAtStart = src.StaminaMaxAtStart;
            dst.StaminaSum = src.StaminaSum;
            dst.StaminaSamples = src.StaminaSamples;
            dst.MinStamina = src.MinStamina;
            dst.IdleTimeSec = src.IdleTimeSec;
            dst.MiningTimeSec = src.MiningTimeSec;
            dst.CoolingTimeSec = src.CoolingTimeSec;
            dst.OverheatedLockSec = src.OverheatedLockSec;
            dst.SimulationSeconds = src.SimulationSeconds;
        }

        public static string DefaultExportDirectory()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BenchmarkResults"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public bool ExportAgain()
        {
            if (Session == null || Session.Runs.Count == 0) return false;
            var paths = Session.ExportCsv(DefaultExportDirectory());
            LastRawCsvPath = paths.rawPath;
            LastAggCsvPath = paths.aggPath;
            Debug.Log($"[BENCHMARK] Raw CSV: {LastRawCsvPath}");
            Debug.Log($"[BENCHMARK] Aggregate CSV: {LastAggCsvPath}");
            return true;
        }
    }
}
