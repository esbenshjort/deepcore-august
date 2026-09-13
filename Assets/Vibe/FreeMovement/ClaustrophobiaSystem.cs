using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Claustrophobic Stress V1 — game-hour exposure accumulation + recovery.
    /// Person-owned via WorkerState. Uses existing stats/Soul resistance and WorkerState events.
    /// </summary>
    public static class ClaustrophobiaSystem
    {
        public struct Sample
        {
            public int WidthBand;
            public TunnelLightBand Light;
            public float Illum01;
            public float DepthCells;
            public bool Isolated;
            public bool Trapped;
            public bool AtCamp;
            public bool InWideLit;
        }

        public struct LastCauses
        {
            public int WidthBand;
            public TunnelLightBand Light;
            public float DepthCells;
            public float ExposureHours;
            public bool Isolated;
            public bool Trapped;
            public ClaustrophobiaBand Band;
        }

        static readonly Dictionary<int, LastCauses> _last = new(8);
        static readonly Dictionary<int, float> _banterCd = new(8);
        static readonly Dictionary<int, float> _effectAcc = new(8);
        static readonly Dictionary<int, float> _seekExitCd = new(8);

        public static bool TryGetLastCauses(int workerId, out LastCauses c) =>
            _last.TryGetValue(workerId, out c);

        public static Sample SampleEnvironment(
            FineTerrainWorld world,
            Vector2 worldPos,
            Vector2 campPos,
            MineInfrastructure infra,
            Vector2? excavatorPos,
            IReadOnlyList<Vector2> coworkerPositions,
            float isolationRadius = 2.4f)
        {
            var s = new Sample { AtCamp = false, InWideLit = false };
            if (world == null) return s;

            var cell = world.WorldToCell(worldPos);
            int clear = 0;
            if (world.InBounds(cell.x, cell.y) && world.IsTunnelOpen(cell.x, cell.y))
                clear = world.Navigation.GetClearance(cell.x, cell.y);
            else
                clear = 0;

            s.WidthBand = TunnelWidthSpec.WidthBandFromClearance(clear);
            s.Illum01 = TunnelIllumination.Sample01(worldPos, infra, excavatorPos, campPos);
            s.Light = TunnelIllumination.Classify(s.Illum01);

            var campCell = world.WorldToCell(campPos);
            // Depth into the mountain (north / +Y). South yard must NOT count as deep.
            s.DepthCells = Mathf.Max(0f, cell.y - campCell.y);

            float campDist = Vector2.Distance(worldPos, campPos);
            // Open camp/yard is open — do NOT require WidthBand≥4 (perimeter beds/toilet
            // sit near rock and report clearance 1–2, which falsely spiked confinement).
            const float campReliefRadius = 5.2f;
            s.AtCamp = campDist < campReliefRadius;
            if (s.AtCamp)
                s.WidthBand = Mathf.Max(s.WidthBand, 5);

            s.InWideLit = s.WidthBand >= 4 && s.Light <= TunnelLightBand.Dim;

            s.Isolated = true;
            if (coworkerPositions != null)
            {
                for (int i = 0; i < coworkerPositions.Count; i++)
                {
                    if (Vector2.Distance(worldPos, coworkerPositions[i]) <= isolationRadius)
                    {
                        s.Isolated = false;
                        break;
                    }
                }
            }

            return s;
        }

        /// <summary>Resistance 0.55–1.45 — lower = tougher under confinement.</summary>
        public static float ResistanceMul(WorkerStats stats, WorkerState st)
        {
            int composure = stats != null ? stats.Get(WorkerStatId.Composure) : WorkerStats.Baseline;
            int bravery = stats != null ? stats.Get(WorkerStatId.Bravery) : WorkerStats.Baseline;
            int tolerance = stats != null ? stats.Get(WorkerStatId.Tolerance) : WorkerStats.Baseline;
            int determination = stats != null ? stats.Get(WorkerStatId.Determination) : WorkerStats.Baseline;
            int focus = stats != null ? stats.Get(WorkerStatId.Focus) : WorkerStats.Baseline;

            float soul = (composure + bravery + tolerance + determination * 0.7f + focus * 0.4f) / 5.1f;
            // Baseline 10 → ~1.0; high Soul lowers gain
            float mul = Mathf.Lerp(1.35f, 0.58f, Mathf.InverseLerp(6f, 16f, soul));

            if (st != null)
            {
                if (st.Frustration >= 55f) mul *= 1.12f;
                if (st.Frustration >= 75f) mul *= 1.1f;
                if (st.MentalFatigue >= 50f) mul *= 1.1f;
                if (st.MentalFatigue >= 75f) mul *= 1.12f;
                if (st.Morale <= 35f) mul *= 1.08f;
                if (st.Injury >= 25f) mul *= 1.15f;
                if (st.Injury >= 50f) mul *= 1.2f;
            }
            return Mathf.Clamp(mul, 0.5f, 1.55f);
        }

        public static void TickPerson(
            WorkerRuntime wr,
            float hoursDelta,
            Sample env,
            bool trappedFromCamp,
            JobType job,
            string providerId,
            System.Action<WorkerRuntime, string> tryBanter,
            System.Action<WorkerRuntime> onSeekExit)
        {
            if (wr?.State == null || hoursDelta <= 0f || !wr.IsAlive) return;
            if (wr.State.Incapacitated) return;

            var st = wr.State;
            env.Trapped = trappedFromCamp || st.TrappedFromCamp;

            float resist = ResistanceMul(wr.Stats, st);
            float gain = 0f;
            float recover = 0f;

            if (env.AtCamp || env.InWideLit)
            {
                recover = env.AtCamp ? 36f : 16f;
                if (env.Light == TunnelLightBand.Lit) recover += 8f;
                st.ClaustroExposureHours = Mathf.Max(0f, st.ClaustroExposureHours - hoursDelta * 2.4f);
            }
            else
            {
                gain += TunnelWidthSpec.StressGainPerHour(env.WidthBand);
                gain += TunnelIllumination.StressGainPerHour(env.Light) * 0.85f;
                // Depth compounds later — soft early
                float depthGain = env.DepthCells * 0.16f;
                if (st.ClaustroExposureHours >= 1.2f)
                    depthGain *= 1f + Mathf.Min(1.2f, (st.ClaustroExposureHours - 1.2f) * 0.35f);
                gain += Mathf.Min(12f, depthGain);
                if (env.Isolated && env.WidthBand <= 3) gain += 2.2f;
                if (env.Trapped) gain += 28f;

                // Continuous exposure: short trips manageable; sustained is the problem
                st.ClaustroExposureHours += hoursDelta;
                // 0–1.5h: ~0.42–0.75× · full ramp by ~5h
                float exposureMul = 0.42f + Mathf.Clamp01(st.ClaustroExposureHours / 5f) * 0.78f;
                gain *= exposureMul;
            }

            float delta;
            if (recover > 0f && gain < recover * 0.4f)
                delta = -recover * hoursDelta;
            else
                delta = (gain * resist - recover) * hoursDelta;

            st.ClaustrophobicStress = WorkerState.ClampMeter(st.ClaustrophobicStress + delta);

            var band = ClaustrophobiaBands.Classify(st.ClaustrophobicStress);
            // Open camp never triggers seek-exit / dig refuse — even while stress is still bleeding off
            st.ClaustroSeekingExit = !env.AtCamp && band >= ClaustrophobiaBand.Critical;

            _last[wr.WorkerId] = new LastCauses
            {
                WidthBand = env.WidthBand,
                Light = env.Light,
                DepthCells = env.DepthCells,
                ExposureHours = st.ClaustroExposureHours,
                Isolated = env.Isolated,
                Trapped = env.Trapped,
                Band = band,
            };

            ApplyThresholdEffects(wr, hoursDelta, band, env.AtCamp, job, providerId, tryBanter, onSeekExit);
        }

        static void ApplyThresholdEffects(
            WorkerRuntime wr,
            float hoursDelta,
            ClaustrophobiaBand band,
            bool atCamp,
            JobType job,
            string providerId,
            System.Action<WorkerRuntime, string> tryBanter,
            System.Action<WorkerRuntime> onSeekExit)
        {
            var st = wr.State;
            if (!_effectAcc.TryGetValue(wr.WorkerId, out float acc)) acc = 0f;
            acc += hoursDelta;
            _effectAcc[wr.WorkerId] = acc;

            // Soft continuous focus pressure while UNEASY+ (not in open camp)
            if (!atCamp && band >= ClaustrophobiaBand.Elevated)
            {
                float focusDrain = band switch
                {
                    ClaustrophobiaBand.Elevated => 1.2f,
                    ClaustrophobiaBand.High => 3.2f,
                    ClaustrophobiaBand.Severe => 5.5f,
                    ClaustrophobiaBand.Critical => 9f,
                    _ => 0f,
                };
                st.FocusState = Mathf.Max(8f, st.FocusState - focusDrain * hoursDelta);
            }

            // Emit frustration via existing event pipeline on a slow cadence (not every frame)
            if (acc < 0.4f) return;
            _effectAcc[wr.WorkerId] = 0f;

            if (!atCamp && band >= ClaustrophobiaBand.High)
            {
                float mag = band switch
                {
                    ClaustrophobiaBand.High => 1.8f,
                    ClaustrophobiaBand.Severe => 3.0f,
                    ClaustrophobiaBand.Critical => 4.5f,
                    _ => 1.2f,
                };
                WorkerStateEventHub.Emit(WorkerStateEvent.Create(
                    wr.WorkerId,
                    WorkerStateEventType.WorkBlocked,
                    mag,
                    "Claustrophobia",
                    job,
                    providerId ?? ""));
            }

            // Banter from UNEASY up — gradual voice, not sudden panic
            if (!atCamp && band >= ClaustrophobiaBand.Elevated && tryBanter != null)
            {
                if (!_banterCd.TryGetValue(wr.WorkerId, out float cd)) cd = 0f;
                cd -= 0.4f;
                if (cd <= 0f)
                {
                    _banterCd[wr.WorkerId] = band >= ClaustrophobiaBand.Severe
                        ? Random.Range(2.5f, 4.5f)
                        : Random.Range(4.5f, 8f);
                    string line = PickLine(band, wr);
                    if (!string.IsNullOrEmpty(line))
                        tryBanter(wr, line);
                }
                else _banterCd[wr.WorkerId] = cd;
            }

            if (!atCamp && band >= ClaustrophobiaBand.Critical)
            {
                if (!_seekExitCd.TryGetValue(wr.WorkerId, out float scd)) scd = 0f;
                scd -= 0.4f;
                if (scd <= 0f)
                {
                    _seekExitCd[wr.WorkerId] = 2.8f;
                    onSeekExit?.Invoke(wr);
                }
                else _seekExitCd[wr.WorkerId] = scd;
            }
        }

        static string PickLine(ClaustrophobiaBand band, WorkerRuntime wr)
        {
            int h = Mathf.Abs(wr.WorkerId * 7919 + (int)(wr.State.ClaustrophobicStress * 10f));
            if (band >= ClaustrophobiaBand.Critical)
            {
                string[] lines =
                {
                    "I can't stay down here. Get me out.",
                    "Not deeper. Not like this.",
                    "I need out. Now.",
                };
                return lines[h % lines.Length];
            }
            if (band >= ClaustrophobiaBand.Severe)
            {
                string[] lines =
                {
                    "This shaft is crushing me.",
                    "Need light. Or width. Or both.",
                    "Feels like the rock's leaning in.",
                    "I want a break. Wider stretch.",
                };
                return lines[h % lines.Length];
            }
            if (band >= ClaustrophobiaBand.High)
            {
                string[] lines =
                {
                    "Getting tight.",
                    "Hate these narrow cuts.",
                    "Could use another lamp on this stretch.",
                };
                return lines[h % lines.Length];
            }
            // UNEASY
            string[] mild =
            {
                "Bit close in here.",
                "Walls are fine. For now.",
                "Keep moving.",
            };
            return mild[h % mild.Length];
        }

        /// <summary>Manager Ease Off / Check In / Break — soft stress relief (not a cure).</summary>
        public static void ApplyManagerRelief(WorkerRuntime wr, float stressRelief, float exposureRelief)
        {
            if (wr?.State == null) return;
            var st = wr.State;
            st.ClaustrophobicStress = WorkerState.ClampMeter(st.ClaustrophobicStress - stressRelief);
            st.ClaustroExposureHours = Mathf.Max(0f, st.ClaustroExposureHours - exposureRelief);
            if (st.ClaustrophobicStress < ClaustrophobiaBands.CriticalAt)
                st.ClaustroSeekingExit = false;
        }

        /// <summary>Collapse / debris entrapment spike — call from existing collapse hooks.</summary>
        public static void SpikeCollapse(WorkerRuntime wr, float amount = 18f)
        {
            if (wr?.State == null || !wr.IsAlive) return;
            wr.State.ClaustrophobicStress =
                WorkerState.ClampMeter(wr.State.ClaustrophobicStress + amount);
            if (wr.State.ClaustrophobicStress >= ClaustrophobiaBands.CriticalAt)
                wr.State.ClaustroSeekingExit = true;
        }

        public static string BuildTooltip(WorkerRuntime wr)
        {
            if (wr?.State == null) return "CONFINEMENT STRESS\nNo data.";
            var st = wr.State;
            var band = ClaustrophobiaBands.Classify(st.ClaustrophobicStress);
            var sb = new System.Text.StringBuilder(160);
            sb.Append("CONFINEMENT STRESS\n");
            sb.Append("Current: ").Append(ClaustrophobiaBands.Label(band)).Append('\n');
            sb.Append('\n').Append("Causes:\n");
            if (TryGetLastCauses(wr.WorkerId, out var c))
            {
                if (c.WidthBand <= 2)
                    sb.Append("- Narrow tunnel\n");
                else if (c.WidthBand == 3)
                    sb.Append("- Moderate passage\n");
                if (c.Light >= TunnelLightBand.Dark)
                    sb.Append("- Low light\n");
                else if (c.Light == TunnelLightBand.Dim)
                    sb.Append("- Dim light\n");
                if (c.DepthCells >= 8f)
                    sb.Append("- Deep underground\n");
                if (c.ExposureHours >= 0.4f)
                    sb.Append("- ").Append(c.ExposureHours.ToString("0.0")).Append("h continuous exposure\n");
                if (c.Isolated)
                    sb.Append("- Working alone\n");
                if (c.Trapped)
                    sb.Append("- Path blocked / trapped\n");
                if (st.Injury >= 25f)
                    sb.Append("- Injured in confinement\n");
            }
            else
                sb.Append("- Environment sampling…\n");

            sb.Append('\n').Append("Effects:\n");
            if (band >= ClaustrophobiaBand.Elevated)
                sb.Append("- Focus reduced\n");
            if (band >= ClaustrophobiaBand.High)
                sb.Append("- Frustration increasing\n");
            if (band >= ClaustrophobiaBand.Severe)
                sb.Append("- Hesitating / wants wider or lighter route\n");
            if (band >= ClaustrophobiaBand.Critical)
                sb.Append("- May refuse deeper work — talk to push or ease\n");
            if (band == ClaustrophobiaBand.Low)
                sb.Append("- Handling it\n");
            return sb.ToString().TrimEnd();
        }
    }
}
