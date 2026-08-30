using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Debug-only geological evidence model diagnostic.
    /// Uses real ProspectorGeoSignalProfiles + ProspectorGeoEvidence + planner.
    /// Does not rebalance — measurement only.
    /// </summary>
    public static class ProspectorGeoEvidenceDiagnostic
    {
        public const int SamplesPerCell = 40;
        public const int MaxWorkUnits = 14;
        const float ExcavatorNearCells = 12f;

        struct CaseSpec
        {
            public string Key;
            public ProspectorHiddenTruthKind Material;
            public AnomalyMaterialVariant Variant;
            public bool MixedGasBedrock;
        }

        struct Row
        {
            public string Worker;
            public string CaseKey;
            public ProspectorHiddenTruthKind Material;
            public AnomalyMaterialVariant Variant;
            public AnomalyGeoSignals Hidden;
            public AnomalyObservedSignals Observed;
            public string Assessment;
            public AssessmentConfidence Confidence;
            public int WorkUnits;
            public string Sources;
        }

        static readonly CaseSpec[] Cases =
        {
            new() { Key = "Bedrock/Nominal", Material = ProspectorHiddenTruthKind.Bedrock, Variant = AnomalyMaterialVariant.Nominal },
            new() { Key = "Bedrock/Fractured", Material = ProspectorHiddenTruthKind.Bedrock, Variant = AnomalyMaterialVariant.FracturedBedrock },
            new() { Key = "Mineral/Nominal", Material = ProspectorHiddenTruthKind.Gold, Variant = AnomalyMaterialVariant.Nominal },
            new() { Key = "Mineral/Dense", Material = ProspectorHiddenTruthKind.Gold, Variant = AnomalyMaterialVariant.DenseMineralisation },
            new() { Key = "Mineral/Vein", Material = ProspectorHiddenTruthKind.Gold, Variant = AnomalyMaterialVariant.VeinMineralisation },
            new() { Key = "Gas/Nominal", Material = ProspectorHiddenTruthKind.Gas, Variant = AnomalyMaterialVariant.Nominal },
            new() { Key = "Gas/TrappedPocket", Material = ProspectorHiddenTruthKind.Gas, Variant = AnomalyMaterialVariant.TrappedGasPocket },
            new() { Key = "Mixed/Bedrock+Gas", Material = ProspectorHiddenTruthKind.Gas, Variant = AnomalyMaterialVariant.MixedContact, MixedGasBedrock = true },
            new() { Key = "Mixed/Bedrock+Mineral", Material = ProspectorHiddenTruthKind.Gold, Variant = AnomalyMaterialVariant.MixedContact },
        };

        static readonly (string name, WorkerSheetProfile profile)[] Workers =
        {
            ("GREEN", WorkerSheetProfile.Green),
            ("BASE", WorkerSheetProfile.Baseline),
            ("ACE", WorkerSheetProfile.Ace),
        };

        static readonly string[] LabelBuckets =
        {
            "Hard geological formation likely",
            "Mineralised formation possible",
            "Promising mineralised target",
            "Possible hazardous pocket",
            "Mixed evidence",
            "Unclear",
            "Assessment under revision",
            "Other",
        };

        /// <summary>Unity batchmode: -executeMethod DeepCore.FreeMovement.ProspectorGeoEvidenceDiagnostic.RunFromEditor</summary>
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[GEO DIAG] Report written: {path}");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var rows = new List<Row>(SamplesPerCell * Cases.Length * Workers.Length);
            int scanBase = 90001;

            for (int w = 0; w < Workers.Length; w++)
            {
                var statsSheet = WorkerStatProfiles.Build(0, Workers[w].profile);
                var analysisStats = ProspectorAnalysisStats.From(statsSheet);

                for (int c = 0; c < Cases.Length; c++)
                {
                    var spec = Cases[c];
                    for (int i = 0; i < SamplesPerCell; i++)
                    {
                        var a = MakeAnomaly(scanBase + w * 10000 + c * 100 + i, i + 1, spec);
                        ProspectorGeoSignalProfiles.SampleHiddenForced(
                            a, spec.Material, spec.Variant, i + c * 17 + w * 31);

                        var result = SimulateInvestigation(a, analysisStats);
                        rows.Add(new Row
                        {
                            Worker = Workers[w].name,
                            CaseKey = spec.Key,
                            Material = spec.Material,
                            Variant = spec.Variant,
                            Hidden = a.HiddenSignals,
                            Observed = a.ObservedSignals,
                            Assessment = NormalizeLabel(result.assessment?.QualitativeLabel),
                            Confidence = result.assessment?.Confidence ?? AssessmentConfidence.Low,
                            WorkUnits = result.workUnits,
                            Sources = result.sources,
                        });
                    }
                }
            }

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(Application.dataPath, "..", "BenchmarkResults")
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(dir, $"geo_evidence_diagnostic_{stamp}.md");
            File.WriteAllText(path, BuildReport(rows), Encoding.UTF8);

            // Also write a stable latest path for convenience
            string latest = Path.Combine(dir, "geo_evidence_diagnostic_latest.md");
            File.WriteAllText(latest, File.ReadAllText(path), Encoding.UTF8);
            return path;
        }

        static ProspectorAnomaly MakeAnomaly(int scanId, int anomalyId, CaseSpec spec)
        {
            var a = new ProspectorAnomaly
            {
                ScanId = scanId,
                AnomalyId = anomalyId,
                TileCount = 24,
                MinX = 10,
                MinY = 10,
                MaxX = 16,
                MaxY = 15,
                MeanSignalStrength = 0.55f,
                MaxSignalStrength = 0.7f,
                GeometryQuality = 0.55f,
                DistanceFromScannerCells = 12f,
                ApproximateCenterCells = new Vector2(13.5f, 12.5f),
            };

            // Shape hints for variants (affects ApplyShapeBias only; variant is forced)
            switch (spec.Variant)
            {
                case AnomalyMaterialVariant.FracturedBedrock:
                    a.GeometryQuality = 0.32f;
                    a.TileCount = 18;
                    a.MaxX = 18;
                    a.MaxY = 17;
                    break;
                case AnomalyMaterialVariant.DenseMineralisation:
                    a.GeometryQuality = 0.7f;
                    a.MeanSignalStrength = 0.72f;
                    a.TileCount = 28;
                    a.MaxX = 14;
                    a.MaxY = 14;
                    break;
                case AnomalyMaterialVariant.VeinMineralisation:
                    a.MaxX = 22;
                    a.MaxY = 12;
                    a.TileCount = 20;
                    break;
                case AnomalyMaterialVariant.TrappedGasPocket:
                    a.GeometryQuality = 0.65f;
                    a.MaxSignalStrength = 0.82f;
                    break;
                case AnomalyMaterialVariant.Nominal when spec.Material == ProspectorHiddenTruthKind.Gas:
                    a.GeometryQuality = 0.35f;
                    a.TileCount = 22;
                    a.MaxX = 17;
                    a.MaxY = 16;
                    break;
            }

            return a;
        }

        static (AnomalyAssessment assessment, int workUnits, string sources) SimulateInvestigation(
            ProspectorAnomaly a,
            in ProspectorAnalysisStats stats)
        {
            var used = new List<string>(6);
            int work = 0;

            ProspectorInvestigationPlanner.BeginDeskInterpretation(a);
            var deskLines = ProspectorGeoEvidence.RevealDeskInterpretation(a, stats);
            ProspectorGeoEvidence.AppendFindings(a, deskLines);
            ProspectorInvestigationPlanner.ApplyAfterEvidence(
                a, AnomalyEvidenceKind.ScanInterpretation, ExcavatorNearCells);
            used.Add("Desk");
            work++;

            int guard = 0;
            while (a.CurrentNeed != AnomalyInvestigationNeed.ReadyForFinding
                   && a.CurrentNeed != AnomalyInvestigationNeed.Complete
                   && guard++ < MaxWorkUnits)
            {
                switch (a.CurrentNeed)
                {
                    case AnomalyInvestigationNeed.NeedFieldEvidence:
                        ProspectorGeoEvidence.AppendFindings(a,
                            ProspectorGeoEvidence.RevealFieldInspection(a, stats));
                        ProspectorInvestigationPlanner.ApplyAfterEvidence(
                            a, AnomalyEvidenceKind.FieldEvidence, ExcavatorNearCells);
                        used.Add("Field");
                        work++;
                        break;
                    case AnomalyInvestigationNeed.NeedLooseRockInspection:
                        ProspectorGeoEvidence.AppendFindings(a,
                            ProspectorGeoEvidence.RevealLooseRockInspection(a, stats));
                        ProspectorInvestigationPlanner.ApplyAfterEvidence(
                            a, AnomalyEvidenceKind.LooseRockEvidence, ExcavatorNearCells);
                        used.Add("LooseRock");
                        work++;
                        break;
                    case AnomalyInvestigationNeed.NeedRefinerConsultation:
                        ProspectorGeoEvidence.AppendFindings(a,
                            ProspectorGeoEvidence.RevealRefinerOpinion(a, stats));
                        ProspectorInvestigationPlanner.ApplyAfterEvidence(
                            a, AnomalyEvidenceKind.RefinerOpinion, ExcavatorNearCells);
                        used.Add("Refiner");
                        work++;
                        break;
                    case AnomalyInvestigationNeed.NeedCrossCheck:
                        ProspectorGeoEvidence.AppendFindings(a,
                            ProspectorGeoEvidence.RevealCrossCheck(a, stats));
                        ProspectorInvestigationPlanner.ApplyAfterEvidence(
                            a, AnomalyEvidenceKind.CrossCheck, ExcavatorNearCells);
                        used.Add("CrossCheck");
                        work++;
                        break;
                    case AnomalyInvestigationNeed.NeedDeskInterpretation:
                        // Should not loop; force progress
                        ProspectorInvestigationPlanner.ApplyAfterEvidence(
                            a, AnomalyEvidenceKind.ScanInterpretation, ExcavatorNearCells);
                        break;
                    default:
                        // Stuck — publish anyway
                        a.SetNeed(AnomalyInvestigationNeed.ReadyForFinding, "diagnostic force");
                        break;
                }
            }

            var assessment = ProspectorGeoEvidence.BuildAssessment(a, stats);
            a.Assessment = assessment;
            return (assessment, work, string.Join("+", used));
        }

        static string NormalizeLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return "Unclear";
            for (int i = 0; i < LabelBuckets.Length - 1; i++)
                if (string.Equals(label, LabelBuckets[i], StringComparison.OrdinalIgnoreCase))
                    return LabelBuckets[i];
            if (label.IndexOf("Hard", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Hard geological formation likely";
            if (label.IndexOf("Promising", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Promising mineralised target";
            if (label.IndexOf("Mineral", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Mineralised formation possible";
            if (label.IndexOf("hazard", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Possible hazardous pocket";
            if (label.IndexOf("Mixed", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Mixed evidence";
            if (label.IndexOf("revision", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Assessment under revision";
            if (label.IndexOf("Unclear", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Unclear";
            return "Other";
        }

        static string BuildReport(List<Row> rows)
        {
            var sb = new StringBuilder(64_000);
            sb.AppendLine("# Prospector Geological Evidence Diagnostic");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Samples per case: {SamplesPerCell}");
            sb.AppendLine($"Cases: {Cases.Length} · Workers: GREEN / BASE / ACE");
            sb.AppendLine();
            sb.AppendLine("Uses real `ProspectorGeoSignalProfiles`, `ProspectorGeoEvidence`, and `ProspectorInvestigationPlanner`.");
            sb.AppendLine("No profile rebalancing applied.");
            sb.AppendLine();

            foreach (var (name, _) in Workers)
            {
                sb.AppendLine($"---");
                sb.AppendLine();
                sb.AppendLine($"## Worker: {name}");
                sb.AppendLine();
                AppendSummaryTable(sb, rows, name);
                sb.AppendLine();
                AppendConfusion(sb, rows, name);
                sb.AppendLine();
                AppendWorkUnitStats(sb, rows, name);
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Giveaway signal check (HIDDEN traits → truth material)");
            sb.AppendLine();
            sb.AppendLine("Nearest profile-centroid classifier on hidden floats (Bedrock / Mineral / Gas only; Mixed cases excluded).");
            sb.AppendLine("Flag if accuracy ≥ 90% (accidental giveaway).");
            sb.AppendLine();
            AppendGiveaway(sb, rows);
            sb.AppendLine();

            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Health notes");
            sb.AppendLine();
            AppendHealthNotes(sb, rows);
            return sb.ToString();
        }

        static void AppendSummaryTable(StringBuilder sb, List<Row> rows, string worker)
        {
            sb.AppendLine("| Truth case | n | Hard% | Mineral% | Promising% | Hazard% | Mixed% | Unclear% | Rev% | Other% | AvgConf | AvgWork |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

            foreach (var spec in Cases)
            {
                var subset = Filter(rows, worker, spec.Key);
                int n = subset.Count;
                if (n == 0) continue;
                float hard = Pct(subset, "Hard geological formation likely");
                float min = Pct(subset, "Mineralised formation possible");
                float prom = Pct(subset, "Promising mineralised target");
                float haz = Pct(subset, "Possible hazardous pocket");
                float mix = Pct(subset, "Mixed evidence");
                float unc = Pct(subset, "Unclear");
                float rev = Pct(subset, "Assessment under revision");
                float oth = Pct(subset, "Other");
                float conf = AvgConf(subset);
                float work = AvgWork(subset);
                sb.AppendLine(
                    $"| {spec.Key} | {n} | {hard:0.0} | {min:0.0} | {prom:0.0} | {haz:0.0} | {mix:0.0} | {unc:0.0} | {rev:0.0} | {oth:0.0} | {conf:0.00} | {work:0.00} |");
            }

            // Also by pure material (collapse variants)
            sb.AppendLine();
            sb.AppendLine("### By dominant truth material (collapsed)");
            sb.AppendLine();
            sb.AppendLine("| Material | n | Hard% | Mineral% | Promising% | Hazard% | Mixed% | Unclear% | AvgConf |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
            foreach (ProspectorHiddenTruthKind mat in new[]
                     {
                         ProspectorHiddenTruthKind.Bedrock,
                         ProspectorHiddenTruthKind.Gold,
                         ProspectorHiddenTruthKind.Gas,
                     })
            {
                var subset = new List<Row>();
                for (int i = 0; i < rows.Count; i++)
                    if (rows[i].Worker == worker && rows[i].Material == mat
                        && !rows[i].CaseKey.StartsWith("Mixed", StringComparison.Ordinal))
                        subset.Add(rows[i]);
                int n = subset.Count;
                if (n == 0) continue;
                sb.AppendLine(
                    $"| {mat} | {n} | {Pct(subset, "Hard geological formation likely"):0.0} | " +
                    $"{Pct(subset, "Mineralised formation possible"):0.0} | " +
                    $"{Pct(subset, "Promising mineralised target"):0.0} | " +
                    $"{Pct(subset, "Possible hazardous pocket"):0.0} | " +
                    $"{Pct(subset, "Mixed evidence"):0.0} | " +
                    $"{Pct(subset, "Unclear"):0.0} | {AvgConf(subset):0.00} |");
            }
        }

        static void AppendConfusion(StringBuilder sb, List<Row> rows, string worker)
        {
            sb.AppendLine("### Confusion: TRUTH MATERIAL → FINAL ASSESSMENT");
            sb.AppendLine();
            string[] mats = { "Bedrock", "Gold", "Gas", "Mixed*" };
            sb.Append("| Truth \\ Assess |");
            for (int i = 0; i < LabelBuckets.Length - 1; i++)
                sb.Append($" {ShortLabel(LabelBuckets[i])} |");
            sb.AppendLine();
            sb.Append("|---|");
            for (int i = 0; i < LabelBuckets.Length - 1; i++)
                sb.Append("---:|");
            sb.AppendLine();

            foreach (var matName in mats)
            {
                var subset = new List<Row>();
                for (int i = 0; i < rows.Count; i++)
                {
                    if (rows[i].Worker != worker) continue;
                    bool mixed = rows[i].CaseKey.StartsWith("Mixed", StringComparison.Ordinal);
                    if (matName == "Mixed*" && mixed) subset.Add(rows[i]);
                    else if (matName == "Bedrock" && !mixed && rows[i].Material == ProspectorHiddenTruthKind.Bedrock)
                        subset.Add(rows[i]);
                    else if (matName == "Gold" && !mixed && rows[i].Material == ProspectorHiddenTruthKind.Gold)
                        subset.Add(rows[i]);
                    else if (matName == "Gas" && !mixed && rows[i].Material == ProspectorHiddenTruthKind.Gas)
                        subset.Add(rows[i]);
                }
                sb.Append($"| {matName} |");
                int n = Mathf.Max(1, subset.Count);
                for (int i = 0; i < LabelBuckets.Length - 1; i++)
                {
                    int c = 0;
                    for (int r = 0; r < subset.Count; r++)
                        if (subset[r].Assessment == LabelBuckets[i]) c++;
                    sb.Append($" {100f * c / n:0.0}% |");
                }
                sb.AppendLine();
            }
        }

        static void AppendWorkUnitStats(StringBuilder sb, List<Row> rows, string worker)
        {
            sb.AppendLine("### Work units / sources");
            sb.AppendLine();
            float sum = 0f;
            int n = 0;
            var src = new Dictionary<string, int>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Worker != worker) continue;
                sum += rows[i].WorkUnits;
                n++;
                if (!src.ContainsKey(rows[i].Sources)) src[rows[i].Sources] = 0;
                src[rows[i].Sources]++;
            }
            sb.AppendLine($"Average work units: {(n > 0 ? sum / n : 0):0.00}");
            sb.AppendLine();
            sb.AppendLine("| Evidence path | count | % |");
            sb.AppendLine("|---|---:|---:|");
            foreach (var kv in src)
                sb.AppendLine($"| `{kv.Key}` | {kv.Value} | {100f * kv.Value / Mathf.Max(1, n):0.0} |");
        }

        static void AppendGiveaway(StringBuilder sb, List<Row> rows)
        {
            // Use BASE worker rows, non-mixed, hidden traits
            var samples = new List<Row>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Worker != "BASE") continue;
                if (rows[i].CaseKey.StartsWith("Mixed", StringComparison.Ordinal)) continue;
                samples.Add(rows[i]);
            }

            string[] traitNames =
            {
                "ReturnStrength", "Attenuation", "Conductivity", "StructuralCoherence", "BoundarySharpness01",
            };
            sb.AppendLine("| Feature set | Accuracy% | Giveaway? | Note |");
            sb.AppendLine("|---|---:|:---:|---|");

            for (int t = 0; t < traitNames.Length; t++)
            {
                float acc = SingleTraitCentroidAccuracy(samples, t);
                sb.AppendLine(
                    $"| {traitNames[t]} | {acc * 100f:F1} | {(acc >= 0.90f ? "**YES**" : "no")} | |");
            }

            for (int a = 0; a < traitNames.Length; a++)
            for (int b = a + 1; b < traitNames.Length; b++)
            {
                float acc = TwoTraitCentroidAccuracy(samples, a, b);
                bool watch = traitNames[a] == "Attenuation" && traitNames[b] == "Conductivity";
                string note = watch
                    ? "WATCH — near giveaway; do not rebalance yet"
                    : "";
                string flag = acc >= 0.90f ? "**YES**" : (watch && acc >= 0.85f ? "WATCH" : "no");
                sb.AppendLine(
                    $"| {traitNames[a]}+{traitNames[b]} | {acc * 100f:F1} | {flag} | {note} |");
            }

            // Boundary character alone
            float bAcc = BoundaryClassAccuracy(samples);
            sb.AppendLine(
                $"| BoundaryCharacter (class) | {bAcc * 100f:F1} | {(bAcc >= 0.90f ? "**YES**" : "no")} | |");
        }

        static float SingleTraitCentroidAccuracy(List<Row> samples, int traitIndex)
        {
            // Centroid = mid of material profile range
            float Mid(ProspectorGeoSignalProfiles.TraitRange r) => (r.Min + r.Max) * 0.5f;
            float bed = traitIndex switch
            {
                0 => Mid(ProspectorGeoSignalProfiles.Bedrock.ReturnStrength),
                1 => Mid(ProspectorGeoSignalProfiles.Bedrock.Attenuation),
                2 => Mid(ProspectorGeoSignalProfiles.Bedrock.Conductivity),
                3 => Mid(ProspectorGeoSignalProfiles.Bedrock.StructuralCoherence),
                _ => Mid(ProspectorGeoSignalProfiles.Bedrock.BoundarySharpness),
            };
            float gold = traitIndex switch
            {
                0 => Mid(ProspectorGeoSignalProfiles.Mineralisation.ReturnStrength),
                1 => Mid(ProspectorGeoSignalProfiles.Mineralisation.Attenuation),
                2 => Mid(ProspectorGeoSignalProfiles.Mineralisation.Conductivity),
                3 => Mid(ProspectorGeoSignalProfiles.Mineralisation.StructuralCoherence),
                _ => Mid(ProspectorGeoSignalProfiles.Mineralisation.BoundarySharpness),
            };
            float gas = traitIndex switch
            {
                0 => Mid(ProspectorGeoSignalProfiles.Gas.ReturnStrength),
                1 => Mid(ProspectorGeoSignalProfiles.Gas.Attenuation),
                2 => Mid(ProspectorGeoSignalProfiles.Gas.Conductivity),
                3 => Mid(ProspectorGeoSignalProfiles.Gas.StructuralCoherence),
                _ => Mid(ProspectorGeoSignalProfiles.Gas.BoundarySharpness),
            };

            int correct = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                float v = TraitValue(samples[i].Hidden, traitIndex);
                float dB = Mathf.Abs(v - bed);
                float dG = Mathf.Abs(v - gold);
                float dS = Mathf.Abs(v - gas);
                ProspectorHiddenTruthKind pred = ProspectorHiddenTruthKind.Bedrock;
                float best = dB;
                if (dG < best) { best = dG; pred = ProspectorHiddenTruthKind.Gold; }
                if (dS < best) pred = ProspectorHiddenTruthKind.Gas;
                if (pred == samples[i].Material) correct++;
            }
            return samples.Count == 0 ? 0f : correct / (float)samples.Count;
        }

        static float TwoTraitCentroidAccuracy(List<Row> samples, int a, int b)
        {
            int correct = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                float va = TraitValue(samples[i].Hidden, a);
                float vb = TraitValue(samples[i].Hidden, b);
                float dB = Dist2(va, vb, a, b, ProspectorHiddenTruthKind.Bedrock);
                float dG = Dist2(va, vb, a, b, ProspectorHiddenTruthKind.Gold);
                float dS = Dist2(va, vb, a, b, ProspectorHiddenTruthKind.Gas);
                ProspectorHiddenTruthKind pred = ProspectorHiddenTruthKind.Bedrock;
                float best = dB;
                if (dG < best) { best = dG; pred = ProspectorHiddenTruthKind.Gold; }
                if (dS < best) pred = ProspectorHiddenTruthKind.Gas;
                if (pred == samples[i].Material) correct++;
            }
            return samples.Count == 0 ? 0f : correct / (float)samples.Count;
        }

        static float Dist2(float va, float vb, int ia, int ib, ProspectorHiddenTruthKind mat)
        {
            float ma = ProfileMid(mat, ia);
            float mb = ProfileMid(mat, ib);
            float da = va - ma;
            float db = vb - mb;
            return da * da + db * db;
        }

        static float ProfileMid(ProspectorHiddenTruthKind mat, int traitIndex)
        {
            var p = mat switch
            {
                ProspectorHiddenTruthKind.Gold => ProspectorGeoSignalProfiles.Mineralisation,
                ProspectorHiddenTruthKind.Gas => ProspectorGeoSignalProfiles.Gas,
                _ => ProspectorGeoSignalProfiles.Bedrock,
            };
            return traitIndex switch
            {
                0 => (p.ReturnStrength.Min + p.ReturnStrength.Max) * 0.5f,
                1 => (p.Attenuation.Min + p.Attenuation.Max) * 0.5f,
                2 => (p.Conductivity.Min + p.Conductivity.Max) * 0.5f,
                3 => (p.StructuralCoherence.Min + p.StructuralCoherence.Max) * 0.5f,
                _ => (p.BoundarySharpness.Min + p.BoundarySharpness.Max) * 0.5f,
            };
        }

        static float TraitValue(AnomalyGeoSignals h, int traitIndex) => traitIndex switch
        {
            0 => h.ReturnStrength,
            1 => h.Attenuation,
            2 => h.Conductivity,
            3 => h.StructuralCoherence,
            _ => h.BoundarySharpness01,
        };

        static float BoundaryClassAccuracy(List<Row> samples)
        {
            // Mode boundary class per material on training = this sample; predict by class frequency
            // Simpler: for each class, majority material; predict that
            var classCounts = new Dictionary<AnomalyBoundaryCharacter, int[]>();
            for (int i = 0; i < samples.Count; i++)
            {
                var c = samples[i].Hidden.BoundaryCharacter;
                if (!classCounts.TryGetValue(c, out var arr))
                {
                    arr = new int[3];
                    classCounts[c] = arr;
                }
                int mi = samples[i].Material == ProspectorHiddenTruthKind.Bedrock ? 0
                    : samples[i].Material == ProspectorHiddenTruthKind.Gold ? 1 : 2;
                arr[mi]++;
            }
            int correct = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                var arr = classCounts[samples[i].Hidden.BoundaryCharacter];
                int pred = 0;
                if (arr[1] > arr[pred]) pred = 1;
                if (arr[2] > arr[pred]) pred = 2;
                int truth = samples[i].Material == ProspectorHiddenTruthKind.Bedrock ? 0
                    : samples[i].Material == ProspectorHiddenTruthKind.Gold ? 1 : 2;
                if (pred == truth) correct++;
            }
            return samples.Count == 0 ? 0f : correct / (float)samples.Count;
        }

        static void AppendHealthNotes(StringBuilder sb, List<Row> rows)
        {
            // Compare ACE vs GREEN collapsed accuracy toward "expected" qualitative
            float greenHardOnBed = MaterialLabelRate(rows, "GREEN", ProspectorHiddenTruthKind.Bedrock,
                "Hard geological formation likely");
            float aceHardOnBed = MaterialLabelRate(rows, "ACE", ProspectorHiddenTruthKind.Bedrock,
                "Hard geological formation likely");
            float greenMinOnGold = MaterialLabelRate(rows, "GREEN", ProspectorHiddenTruthKind.Gold,
                "Mineralised formation possible")
                + MaterialLabelRate(rows, "GREEN", ProspectorHiddenTruthKind.Gold,
                    "Promising mineralised target");
            float aceMinOnGold = MaterialLabelRate(rows, "ACE", ProspectorHiddenTruthKind.Gold,
                "Mineralised formation possible")
                + MaterialLabelRate(rows, "ACE", ProspectorHiddenTruthKind.Gold,
                    "Promising mineralised target");
            float greenHazOnGas = MaterialLabelRate(rows, "GREEN", ProspectorHiddenTruthKind.Gas,
                "Possible hazardous pocket");
            float aceHazOnGas = MaterialLabelRate(rows, "ACE", ProspectorHiddenTruthKind.Gas,
                "Possible hazardous pocket");

            sb.AppendLine($"- Bedrock → Hard: GREEN {greenHardOnBed * 100f:0.0}% · ACE {aceHardOnBed * 100f:0.0}%");
            sb.AppendLine($"- Mineral → Mineral/Promising: GREEN {greenMinOnGold * 100f:0.0}% · ACE {aceMinOnGold * 100f:0.0}%");
            sb.AppendLine($"- Gas → Hazard: GREEN {greenHazOnGas * 100f:0.0}% · ACE {aceHazOnGas * 100f:0.0}%");
            sb.AppendLine();
            sb.AppendLine("Healthy if: substantial off-diagonal mass remains; ACE improves vs GREEN without ~100% lock-in.");
        }

        static float MaterialLabelRate(
            List<Row> rows, string worker, ProspectorHiddenTruthKind mat, string label)
        {
            int n = 0, hit = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Worker != worker) continue;
                if (rows[i].CaseKey.StartsWith("Mixed", StringComparison.Ordinal)) continue;
                if (rows[i].Material != mat) continue;
                n++;
                if (rows[i].Assessment == label) hit++;
            }
            return n == 0 ? 0f : hit / (float)n;
        }

        static List<Row> Filter(List<Row> rows, string worker, string caseKey)
        {
            var list = new List<Row>();
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Worker == worker && rows[i].CaseKey == caseKey)
                    list.Add(rows[i]);
            return list;
        }

        static float Pct(List<Row> rows, string label)
        {
            if (rows.Count == 0) return 0f;
            int c = 0;
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Assessment == label) c++;
            return 100f * c / rows.Count;
        }

        static float AvgConf(List<Row> rows)
        {
            if (rows.Count == 0) return 0f;
            float s = 0f;
            for (int i = 0; i < rows.Count; i++)
                s += (int)rows[i].Confidence;
            return s / rows.Count; // 0..4
        }

        static float AvgWork(List<Row> rows)
        {
            if (rows.Count == 0) return 0f;
            float s = 0f;
            for (int i = 0; i < rows.Count; i++)
                s += rows[i].WorkUnits;
            return s / rows.Count;
        }

        static string ShortLabel(string label) => label switch
        {
            "Hard geological formation likely" => "Hard",
            "Mineralised formation possible" => "Mineral",
            "Promising mineralised target" => "Promising",
            "Possible hazardous pocket" => "Hazard",
            "Mixed evidence" => "Mixed",
            "Unclear" => "Unclear",
            "Assessment under revision" => "Revision",
            _ => label,
        };
    }
}
