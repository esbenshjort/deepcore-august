using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Internal signal profile derived from Ground Truth morphology + hidden tallies.
    /// Player never sees these channel names as material labels.
    /// </summary>
    public struct AnomalySignalProfile
    {
        public float GeologicalMass;   // bedrock-biased channel
        public float MineralResponse;  // gold-biased channel
        public float ChemicalDiffuse;  // gas-biased channel
        public float Density;
        public float BoundaryClarity;
        public float Elongation;
        public float Concentration;
        public float MeanSignal;
    }

    /// <summary>
    /// Stats snapshot used for live analysis (frozen per Scan at start).
    /// Speed and quality are separate — Focus/WorkRate ≠ domain accuracy.
    /// </summary>
    public struct ProspectorAnalysisStats
    {
        public int Mathematics;
        public int Lithology;
        public int Mineralogy;
        public int Chemistry;
        public int Composure;
        public int Intuition;
        /// <summary>ANALYSIS SPEED (primary).</summary>
        public int Focus;
        /// <summary>ANALYSIS SPEED (secondary).</summary>
        public int WorkRate;
        public int Calibration;
        public int Acoustics;
        public int SpatialGeometry;

        public static ProspectorAnalysisStats From(WorkerStats stats) => new()
        {
            Mathematics = stats != null ? stats.Get(WorkerStatId.Mathematics) : WorkerStats.Baseline,
            Lithology = stats != null ? stats.Get(WorkerStatId.Lithology) : WorkerStats.Baseline,
            Mineralogy = stats != null ? stats.Get(WorkerStatId.Mineralogy) : WorkerStats.Baseline,
            Chemistry = stats != null ? stats.Get(WorkerStatId.Chemistry) : WorkerStats.Baseline,
            Composure = stats != null ? stats.Get(WorkerStatId.Composure) : WorkerStats.Baseline,
            Intuition = stats != null ? stats.Get(WorkerStatId.Intuition) : WorkerStats.Baseline,
            Focus = stats != null ? stats.Get(WorkerStatId.Focus) : WorkerStats.Baseline,
            WorkRate = stats != null ? stats.Get(WorkerStatId.WorkRate) : WorkerStats.Baseline,
            Calibration = stats != null ? stats.Get(WorkerStatId.Calibration) : WorkerStats.Baseline,
            Acoustics = stats != null ? stats.Get(WorkerStatId.Acoustics) : WorkerStats.Baseline,
            SpatialGeometry = stats != null ? stats.Get(WorkerStatId.SpatialGeometry) : WorkerStats.Baseline,
        };

        /// <summary>0..1 analysis speed skill. Does not affect interpretation accuracy.</summary>
        public float AnalysisSpeed01
        {
            get
            {
                float f = (WorkerStats.Clamp(Focus) - 1) / 18f;
                float w = (WorkerStats.Clamp(WorkRate) - 1) / 18f;
                return Mathf.Clamp01(f * 0.7f + w * 0.3f);
            }
        }

        /// <summary>0..1 interpretation quality. Does not change analysis duration.</summary>
        public float AnalysisQuality01
        {
            get
            {
                float m = (WorkerStats.Clamp(Mathematics) - 1) / 18f;
                float l = (WorkerStats.Clamp(Lithology) - 1) / 18f;
                float n = (WorkerStats.Clamp(Mineralogy) - 1) / 18f;
                float c = (WorkerStats.Clamp(Chemistry) - 1) / 18f;
                float p = (WorkerStats.Clamp(Composure) - 1) / 18f;
                float i = (WorkerStats.Clamp(Intuition) - 1) / 18f;
                return Mathf.Clamp01(m * 0.22f + l * 0.18f + n * 0.18f + c * 0.18f + p * 0.14f + i * 0.1f);
            }
        }
    }

    /// <summary>
    /// Stage-4 interpretation: Ground Truth → signals → imperfect assessment.
    /// Does not output BEDROCK / GOLD / GAS labels.
    /// </summary>
    public static class ProspectorAnomalyInterpretation
    {
        public const int MinTilesToAnalyse = 4;

        /// <summary>
        /// Production analysis duration (game hours). SPEED stats only + evidence complexity.
        /// Quality stats do not shorten this clock.
        /// </summary>
        public static float AnalysisDurationHours(in ProspectorAnalysisStats stats, ProspectorAnomaly a)
        {
            int tiles = a != null ? a.TileCount : MinTilesToAnalyse;
            float size01 = Mathf.Clamp01((tiles - MinTilesToAnalyse) / 90f);
            float area = a != null ? Mathf.Max(1f, a.BoundsWidth * a.BoundsHeight) : 1f;
            float density = a != null ? Mathf.Clamp01(tiles / area) : 1f;
            // Sparse / sprawling evidence is harder to work through
            float complexity01 = Mathf.Clamp01(size01 * 0.65f + (1f - density) * 0.35f);

            float speed01 = stats.AnalysisSpeed01;
            // Desk clock only — field/refiner trips add more time between published findings.
            // Target band ~8–22h so meaningful discoveries land roughly every 8–24 productive hours.
            float baseHours = Mathf.Lerp(11f, 18f, complexity01);
            float hours = baseHours * Mathf.Lerp(1.35f, 0.72f, speed01);
            hours = Mathf.Clamp(hours, 8f, 22f);

            if (ProspectorScanFormulas.UseTestingScanDurations)
            {
                // Playable but not instant at 1× clock (~few–dozen seconds real)
                hours *= ProspectorScanFormulas.TestingAnalysisDurationScale;
                hours = Mathf.Clamp(hours,
                    ProspectorScanFormulas.TestingMinAnalysisHours,
                    ProspectorScanFormulas.TestingMaxAnalysisHours);
            }
            return hours;
        }

        /// <summary>
        /// Additional desk time after field/refiner evidence — reconsider conflicts.
        /// Shorter than full interpretation; still meaningful game hours.
        /// </summary>
        public static float CrossCheckDurationHours(in ProspectorAnalysisStats stats, ProspectorAnomaly a)
        {
            float full = AnalysisDurationHours(stats, a);
            float hours = full * 0.4f;
            if (ProspectorScanFormulas.UseTestingScanDurations)
            {
                hours = Mathf.Clamp(hours,
                    ProspectorScanFormulas.TestingMinAnalysisHours * 0.5f,
                    ProspectorScanFormulas.TestingMaxAnalysisHours * 0.65f);
            }
            else
                hours = Mathf.Clamp(hours, 3.2f, 8.8f);
            return hours;
        }

        /// <summary>TEMP helper — prefer AnalysisDurationHours(stats, anomaly).</summary>
        public static float AnalysisDurationHours(int mathematics, int tileCount)
        {
            var stub = new ProspectorAnalysisStats
            {
                Mathematics = mathematics,
                Focus = mathematics,
                WorkRate = mathematics,
            };
            // Approximate footprint for complexity when only tile count is known
            int side = Mathf.Max(2, Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, tileCount))));
            var fake = new ProspectorAnomaly
            {
                TileCount = tileCount,
                MinX = 0,
                MinY = 0,
                MaxX = side - 1,
                MaxY = side - 1,
            };
            return AnalysisDurationHours(stub, fake);
        }

        public static AnomalySignalProfile BuildSignals(ProspectorAnomaly a)
        {
            var p = new AnomalySignalProfile();
            if (a == null || a.TileCount <= 0) return p;

            int totalHidden = a.HiddenBedrockTiles + a.HiddenGoldTiles + a.HiddenGasTiles;
            float inv = totalHidden > 0 ? 1f / totalHidden : 0f;
            float bed = a.HiddenBedrockTiles * inv;
            float gold = a.HiddenGoldTiles * inv;
            float gas = a.HiddenGasTiles * inv;

            // Shape descriptors from bounds / tile packing (gas tends diffuse, solids denser)
            float area = Mathf.Max(1f, a.BoundsWidth * a.BoundsHeight);
            p.Density = Mathf.Clamp01(a.TileCount / area);
            p.BoundaryClarity = Mathf.Clamp01(a.GeometryQuality);
            float aspect = a.BoundsWidth > a.BoundsHeight
                ? a.BoundsWidth / (float)Mathf.Max(1, a.BoundsHeight)
                : a.BoundsHeight / (float)Mathf.Max(1, a.BoundsWidth);
            p.Elongation = Mathf.Clamp01((aspect - 1f) / 4f);
            p.Concentration = Mathf.Clamp01(a.MaxSignalStrength);
            p.MeanSignal = Mathf.Clamp01(a.MeanSignalStrength);

            // Channels biased by hidden composition + shape (still not labels)
            p.GeologicalMass = Mathf.Clamp01(bed * 0.75f + p.Density * 0.35f + p.BoundaryClarity * 0.2f);
            p.MineralResponse = Mathf.Clamp01(gold * 0.8f + p.Concentration * 0.35f + (1f - p.Elongation) * 0.1f);
            p.ChemicalDiffuse = Mathf.Clamp01(
                gas * 0.85f + (1f - p.Density) * 0.35f + p.Elongation * 0.15f + (1f - p.BoundaryClarity) * 0.2f);

            return p;
        }

        public static AnomalyAssessment Interpret(
            ProspectorAnomaly a,
            in ProspectorAnalysisStats stats)
        {
            // Geological evidence model — assessment from observed traits
            return ProspectorGeoEvidence.BuildAssessment(a, stats);
        }

        public static string QualitativeFromKind(int kind, float best, float margin)
        {
            return kind switch
            {
                0 => "Hard geological formation likely",
                1 => best > 0.55f && margin > 0.12f
                    ? "Promising mineralised target"
                    : "Mineralised formation possible",
                2 => "Possible hazardous pocket",
                _ => best < 0.35f ? "Unclear" : "Mixed evidence",
            };
        }

        public static string InitialScanFindingText(ProspectorAnomaly a)
        {
            if (a == null) return "Initial scan: notable response registered";
            if (a.MeanSignalStrength > 0.6f && a.GeometryQuality > 0.5f)
                return "Initial scan: strong compact return registered";
            if (a.GeometryQuality < 0.4f || a.TileCount < 10)
                return "Initial scan: diffuse / irregular return registered";
            return "Initial scan: notable response registered";
        }

        public static string RevisedInterpretationFindingText(AnomalyAssessment assessment)
        {
            if (assessment == null) return "Revised interpretation: assessment updated";
            string q = assessment.QualitativeLabel ?? "Unclear";
            return $"Conclusion: {q}";
        }

        /// <summary>
        /// Live in-progress belief — evolves with analysis progress. Not the frozen final answer
        /// until progress reaches 1. Early readings are muddy/unstable; later they converge.
        /// </summary>
        public static void ApplyLiveBelief(
            AnomalyAssessment live,
            ProspectorAnomaly a,
            in ProspectorAnalysisStats stats,
            float progress01)
        {
            if (live == null || a == null) return;
            float p = Mathf.Clamp01(progress01);

            ComputeFinalReads(a, stats,
                out float geoF, out float minF, out float chemF,
                out bool wrong, out float math01, out _, out _, out _,
                out float comp01, out _);

            // Math + quality → clarity arrives earlier; low composure → longer instability
            float quality01 = stats.AnalysisQuality01;
            float clarityExp = Mathf.Lerp(2.2f, 1.1f, Mathf.Max(math01, quality01));
            float clarity = Mathf.Pow(p, clarityExp);
            clarity = Mathf.Clamp01(clarity * (0.45f + quality01 * 0.55f));

            // Step index so beliefs shift in readable jumps, not every frame identically
            int step = Mathf.Clamp(Mathf.FloorToInt(p * 12f), 0, 12);
            float wobbleAmp = (1f - p) * Mathf.Lerp(0.48f, 0.12f, quality01);

            float nG = ProspectorDetection.StableNoise01(a.ScanId, a.AnomalyId * 3 + step, 11);
            float nM = ProspectorDetection.StableNoise01(a.ScanId, a.AnomalyId * 5 + step, 22);
            float nC = ProspectorDetection.StableNoise01(a.ScanId, a.AnomalyId * 7 + step, 33);

            // Early prior: near-equal mud with scan-stable jitter
            float priorG = 0.34f + (nG - 0.5f) * 0.5f;
            float priorM = 0.33f + (nM - 0.5f) * 0.5f;
            float priorC = 0.33f + (nC - 0.5f) * 0.5f;

            // Temporary false leads early — more when quality is low
            float falseLead = ProspectorDetection.StableNoise01(a.AnomalyId * 13, a.ScanId, step + 90);
            if (p < 0.6f && falseLead < (1f - quality01) * 0.65f + (1f - math01) * 0.15f)
            {
                float leadPick = ProspectorDetection.StableNoise01(a.AnomalyId, step, a.ScanId + 4);
                if (leadPick < 0.34f) priorG += 0.28f;
                else if (leadPick < 0.67f) priorM += 0.28f;
                else priorC += 0.28f;
            }

            float geoLive = Mathf.Lerp(priorG, geoF, clarity) + (nG - 0.5f) * wobbleAmp;
            float minLive = Mathf.Lerp(priorM, minF, clarity) + (nM - 0.5f) * wobbleAmp;
            float chemLive = Mathf.Lerp(priorC, chemF, clarity) + (nC - 0.5f) * wobbleAmp;
            geoLive = Mathf.Max(0.02f, geoLive);
            minLive = Mathf.Max(0.02f, minLive);
            chemLive = Mathf.Max(0.02f, chemLive);

            WriteBeliefPercents(live, geoLive, minLive, chemLive);

            float best = geoLive;
            if (minLive > best) best = minLive;
            if (chemLive > best) best = chemLive;
            float margin = best - SecondBest(geoLive, minLive, chemLive);

            float confScore = FinalConfidenceScore(best, margin, math01, comp01, wrong);
            // Confidence develops with progress — quality improves calibration of confidence
            float confLive = confScore * Mathf.Pow(p, Mathf.Lerp(1.55f, 1.05f, quality01));
            confLive *= 0.3f + quality01 * 0.7f;
            if (p < 0.28f) confLive = Mathf.Min(confLive, 0.2f);
            else if (p < 0.55f) confLive = Mathf.Min(confLive, 0.38f);
            else if (p < 0.82f) confLive = Mathf.Min(confLive, 0.62f);

            live.Confidence = BandFromScore(confLive);
            live.AnalysisProgress01 = p;
            live.Title = "ANALYSING…";
            // Player sees qualitative only after published findings — stay Unclear mid-work
            if (string.IsNullOrEmpty(live.QualitativeLabel)
                || live.QualitativeLabel == "ANALYSING…")
                live.QualitativeLabel = "Unclear";
        }

        static void ComputeFinalReads(
            ProspectorAnomaly a,
            in ProspectorAnalysisStats stats,
            out float geoRead, out float minRead, out float chemRead,
            out bool wrong,
            out float math01, out float lith01, out float min01, out float chem01,
            out float comp01,
            out AnomalySignalProfile signals)
        {
            signals = BuildSignals(a);
            math01 = Stat01(stats.Mathematics);
            lith01 = Stat01(stats.Lithology);
            min01 = Stat01(stats.Mineralogy);
            chem01 = Stat01(stats.Chemistry);
            comp01 = Stat01(stats.Composure);
            float intu01 = Stat01(stats.Intuition);

            geoRead = signals.GeologicalMass * (0.45f + lith01 * 0.7f);
            minRead = signals.MineralResponse * (0.45f + min01 * 0.7f);
            chemRead = signals.ChemicalDiffuse * (0.45f + chem01 * 0.7f);

            float sep = 0.55f + math01 * 0.45f;
            geoRead = Mathf.Pow(Mathf.Clamp01(geoRead), sep);
            minRead = Mathf.Pow(Mathf.Clamp01(minRead), sep);
            chemRead = Mathf.Pow(Mathf.Clamp01(chemRead), sep);

            float noise = ProspectorDetection.StableNoise01(a.ScanId, a.AnomalyId, a.TileCount);
            if (noise < intu01 * 0.45f)
                BoostSecond(ref geoRead, ref minRead, ref chemRead, 0.12f + intu01 * 0.18f);

            float err = ProspectorDetection.StableNoise01(a.AnomalyId * 17, a.ScanId, (int)(a.MeanSignalStrength * 100f));
            float errChance = (1f - comp01) * 0.28f + (1f - math01) * 0.12f;
            wrong = err < errChance;

            if (wrong)
            {
                float swap = ProspectorDetection.StableNoise01(a.AnomalyId, a.TileCount * 3, a.ScanId);
                if (swap < 0.34f) (geoRead, minRead) = (minRead, geoRead);
                else if (swap < 0.67f) (geoRead, chemRead) = (chemRead, geoRead);
                else (minRead, chemRead) = (chemRead, minRead);

                if (comp01 < 0.4f)
                {
                    geoRead = Mathf.Lerp(geoRead, minRead, 0.25f);
                    chemRead = Mathf.Lerp(chemRead, geoRead, 0.2f);
                }
            }
        }

        static int ClassifyKind(
            float geoRead, float minRead, float chemRead, float math01,
            out float best, out float margin)
        {
            int kind = 0;
            best = geoRead;
            if (minRead > best) { best = minRead; kind = 1; }
            if (chemRead > best) { best = chemRead; kind = 2; }
            float second = SecondBest(geoRead, minRead, chemRead);
            margin = best - second;
            if (margin < 0.08f + (1f - math01) * 0.1f)
                kind = 3;
            return kind;
        }

        static float FinalConfidenceScore(
            float best, float margin, float math01, float comp01, bool wrong)
        {
            float confScore = margin * 1.4f + comp01 * 0.55f + math01 * 0.25f + best * 0.2f;
            if (wrong)
                confScore = Mathf.Lerp(confScore * 0.55f, 0.55f + comp01 * 0.5f, comp01);
            return confScore;
        }

        /// <summary>
        /// Normalize interpreted channel reads into Bedrock / Gold / Gas belief % (sum 100).
        /// Uses the same post-skill / post-misread values as the assessment — not Ground Truth.
        /// </summary>
        public static void WriteBeliefPercents(
            AnomalyAssessment assessment, float geoRead, float minRead, float chemRead)
        {
            if (assessment == null) return;
            float g = Mathf.Max(0.0001f, geoRead);
            float m = Mathf.Max(0.0001f, minRead);
            float c = Mathf.Max(0.0001f, chemRead);
            float sum = g + m + c;
            float gf = g / sum * 100f;
            float mf = m / sum * 100f;
            float cf = c / sum * 100f;

            int bed = Mathf.FloorToInt(gf);
            int gold = Mathf.FloorToInt(mf);
            int gas = Mathf.FloorToInt(cf);
            int rem = 100 - bed - gold - gas;

            float fb = gf - bed, fm = mf - gold, fc = cf - gas;
            while (rem > 0)
            {
                if (fb >= fm && fb >= fc) { bed++; fb = -1f; }
                else if (fm >= fc) { gold++; fm = -1f; }
                else { gas++; fc = -1f; }
                rem--;
            }

            assessment.BeliefBedrockPct = bed;
            assessment.BeliefGoldPct = gold;
            assessment.BeliefGasPct = gas;
        }

        static void FillAssessment(
            AnomalyAssessment a,
            int kind,
            in AnomalySignalProfile signals,
            float best,
            float margin,
            float math01, float lith01, float min01, float chem01)
        {
            a.EvidenceNotes.Clear();
            switch (kind)
            {
                case 1:
                    a.Title = "MINERAL-LIKE FORMATION";
                    a.EvidenceNotes.Add(signals.Concentration > 0.55f
                        ? "concentrated response"
                        : "localized response peaks");
                    a.EvidenceNotes.Add(signals.Density > 0.45f
                        ? "dense structure"
                        : "compact cluster pattern");
                    a.EvidenceNotes.Add(min01 > 0.5f
                        ? "possible mineral characteristics"
                        : "mineral-like returns (uncertain)");
                    if (math01 > 0.55f && margin > 0.12f)
                        a.EvidenceNotes.Add("pattern separation is relatively clean");
                    break;

                case 2:
                    a.Title = "DIFFUSE ANOMALY";
                    a.EvidenceNotes.Add(signals.Density < 0.4f
                        ? "irregular response"
                        : "spread response field");
                    a.EvidenceNotes.Add(signals.BoundaryClarity < 0.5f
                        ? "weak boundary"
                        : "soft edge definition");
                    a.EvidenceNotes.Add(chem01 > 0.5f
                        ? "possible environmental hazard"
                        : "chemical-like scatter (uncertain)");
                    if (signals.Elongation > 0.35f)
                        a.EvidenceNotes.Add("elongated / non-compact footprint");
                    break;

                case 0:
                    a.Title = "HARD GEOLOGICAL MASS";
                    a.EvidenceNotes.Add(signals.Density > 0.5f
                        ? "coherent mass return"
                        : "broad solid return");
                    a.EvidenceNotes.Add(signals.BoundaryClarity > 0.45f
                        ? "structured boundary"
                        : "heavy body with soft rim");
                    a.EvidenceNotes.Add(lith01 > 0.5f
                        ? "lithologic mass characteristics"
                        : "geological-like density (uncertain)");
                    if (signals.Elongation > 0.4f)
                        a.EvidenceNotes.Add("elongated formation");
                    break;

                default:
                    a.Title = "INDETERMINATE RESPONSE";
                    a.EvidenceNotes.Add("mixed / overlapping signatures");
                    a.EvidenceNotes.Add(margin < 0.1f
                        ? "channels too close to separate"
                        : "ambiguous pattern");
                    a.EvidenceNotes.Add(best < 0.4f
                        ? "weak overall reading"
                        : "no dominant formation class");
                    break;
            }

            while (a.EvidenceNotes.Count > 4)
                a.EvidenceNotes.RemoveAt(a.EvidenceNotes.Count - 1);
        }

        static AssessmentConfidence BandFromScore(float score)
        {
            if (score < 0.22f) return AssessmentConfidence.VeryLow;
            if (score < 0.38f) return AssessmentConfidence.Low;
            if (score < 0.55f) return AssessmentConfidence.Medium;
            if (score < 0.72f) return AssessmentConfidence.High;
            return AssessmentConfidence.VeryHigh;
        }

        static float Stat01(int v) => (WorkerStats.Clamp(v) - 1) / 18f;

        static float SecondBest(float a, float b, float c)
        {
            // median of three
            if (a > b) (a, b) = (b, a);
            if (b > c) (b, c) = (c, b);
            if (a > b) (a, b) = (b, a);
            return b;
        }

        static void BoostSecond(ref float a, ref float b, ref float c, float amount)
        {
            float best = a; int bi = 0;
            if (b > best) { best = b; bi = 1; }
            if (c > best) { best = c; bi = 2; }
            if (bi != 0 && a >= b && a >= c) { /* a is second or tied */ }
            // Find second
            float second = float.MinValue;
            int si = -1;
            if (bi != 0 && a > second) { second = a; si = 0; }
            if (bi != 1 && b > second) { second = b; si = 1; }
            if (bi != 2 && c > second) { second = c; si = 2; }
            if (si == 0) a = Mathf.Clamp01(a + amount);
            else if (si == 1) b = Mathf.Clamp01(b + amount);
            else if (si == 2) c = Mathf.Clamp01(c + amount);
        }
    }
}
