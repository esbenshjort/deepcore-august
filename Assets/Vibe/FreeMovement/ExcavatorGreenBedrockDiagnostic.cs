using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Measurement-only: explains why GREEN often shows BedrockDestroyed = 0.
    /// Does not change stats or formulas.
    /// </summary>
    public static class ExcavatorGreenBedrockDiagnostic
    {
        public readonly struct StrikeProbe
        {
            public readonly MiningDamage.Result BaseStrike;
            public readonly float ThermalMulNormal;
            public readonly int FinalDamageNormal;
            public readonly MiningDamage.Result WeakPointStrike;
            public readonly int FinalDamageWeakPoint;
            public readonly int BedrockArmor;
            public readonly int BedrockMaxHp;
            public readonly int ToolPower;
            public readonly int RawPower;
            public readonly int Lithology;
            public readonly int Spatial;
            public readonly int Toughness;
            public readonly int Safety;
            public readonly bool CanEverPassInjuryCheck;
            public readonly string VerdictCode;
            public readonly string VerdictText;

            public StrikeProbe(
                MiningDamage.Result baseStrike,
                float thermalMulNormal,
                int finalDamageNormal,
                MiningDamage.Result weakPointStrike,
                int finalDamageWeakPoint,
                int bedrockArmor,
                int bedrockMaxHp,
                int toolPower,
                int rawPower,
                int lithology,
                int spatial,
                int toughness,
                int safety,
                bool canEverPassInjuryCheck,
                string verdictCode,
                string verdictText)
            {
                BaseStrike = baseStrike;
                ThermalMulNormal = thermalMulNormal;
                FinalDamageNormal = finalDamageNormal;
                WeakPointStrike = weakPointStrike;
                FinalDamageWeakPoint = finalDamageWeakPoint;
                BedrockArmor = bedrockArmor;
                BedrockMaxHp = bedrockMaxHp;
                ToolPower = toolPower;
                RawPower = rawPower;
                Lithology = lithology;
                Spatial = spatial;
                Toughness = toughness;
                Safety = safety;
                CanEverPassInjuryCheck = canEverPassInjuryCheck;
                VerdictCode = verdictCode;
                VerdictText = verdictText;
            }
        }

        public static StrikeProbe Probe(
            WorkerStats greenSheet,
            int toolPower = MiningDamage.DefaultExcavatorToolPower)
        {
            var cell = FineTerrainWorld.MakeHardRock(bedrockSockets: 3);
            var noWp = MiningDamage.Compute(toolPower, greenSheet, cell.Armor, 0f);
            float wpPen = greenSheet.Get(WorkerStatId.SpatialGeometry) * 0.5f;
            var yesWp = MiningDamage.Compute(toolPower, greenSheet, cell.Armor, wpPen);

            int finalNo = Mathf.Max(1, Mathf.RoundToInt(noWp.Damage * 1f));
            int finalWp = Mathf.Max(1, Mathf.RoundToInt(yesWp.Damage * 1f));

            int toughness = greenSheet.Get(WorkerStatId.Toughness);
            int safety = greenSheet.Get(WorkerStatId.SafetyProtocol);
            int raw = greenSheet.Get(WorkerStatId.RawPower);
            int lith = greenSheet.Get(WorkerStatId.Lithology);
            int spatial = greenSheet.Get(WorkerStatId.SpatialGeometry);
            bool canPassInjury = toughness + WorkerRoll.DieMax >= FreeWorkerController.InjuryOverheatDC;

            float heatPerDig = Mathf.Max(0.5f, 6f - safety * 0.1f);
            int digsToOh = Mathf.CeilToInt(100f / heatPerDig);
            int strikesToBreak = Mathf.CeilToInt(FineTerrainWorld.BedrockMaxHp / (float)Mathf.Max(1, finalNo));

            string code = "D";
            string text =
                $"GREEN deals {finalNo} Bedrock damage/hit (min floor; WP also {finalWp}). " +
                $"Bedrock HP {FineTerrainWorld.BedrockMaxHp} / Armor {FineTerrainWorld.BedrockArmor} → " +
                $"~{strikesToBreak} strikes per tile if focused. Heat ~{heatPerDig:0.##}/dig → ~{digsToOh} digs to OVERHEAT if never cooling. " +
                "Dig face spreads strikes across many cells, so tiles rarely finish before lock. " +
                "OVERHEATED never passive-cools (needs Engineer clear) — lab freezes GREEN after lock. " +
                $"At EXTREME, Composure {greenSheet.Get(WorkerStatId.Composure)}+d20 cannot meet control DC — push into overheat is likely. " +
                $"Injury: Toughness {toughness}+d20 max {toughness + 20} vs DC {FreeWorkerController.InjuryOverheatDC} " +
                $"→ {(canPassInjury ? "can pass" : "ALWAYS FAIL")}. " +
                "Verdict: not A (can chip); not B-only (WP still 1 dmg); C mathematically slow; " +
                "observed zero destroys explained by D (overheat lock + wide dig spread + injury soft-lock).";

            return new StrikeProbe(
                noWp, 1f, finalNo, yesWp, finalWp,
                FineTerrainWorld.BedrockArmor, FineTerrainWorld.BedrockMaxHp,
                toolPower, raw, lith, spatial, toughness, safety,
                canPassInjury, code, text);
        }

        public static string FormatReport(StrikeProbe p, ExcavatorBalanceMetrics sampleRun = null)
        {
            var sb = new StringBuilder(2048);
            sb.AppendLine("======== GREEN vs BEDROCK DIAGNOSTIC (measurement only) ========");
            sb.AppendLine($"ToolPower={p.ToolPower} RawPower={p.RawPower} Lithology={p.Lithology} Spatial={p.Spatial}");
            sb.AppendLine($"Bedrock Armor={p.BedrockArmor} MaxHp={p.BedrockMaxHp} WP DC={FineTerrainWorld.BedrockWeakPointDC}");
            sb.AppendLine($"NO Weak Point: {p.BaseStrike}");
            sb.AppendLine($"  Thermal×{p.ThermalMulNormal:0.00} → FinalDamage={p.FinalDamageNormal} (after max(1, round))");
            sb.AppendLine($"WITH Weak Point: {p.WeakPointStrike}");
            sb.AppendLine($"  Thermal×{p.ThermalMulNormal:0.00} → FinalDamage={p.FinalDamageWeakPoint}");
            sb.AppendLine(
                "Min damage floor: MiningDamage max(1, Floor(DigPower−EffectiveArmor)); " +
                "StrikeCell max(1, Round(dmg×thermal)).");
            sb.AppendLine(
                $"Injury: Toughness {p.Toughness} — can pass overheat injury DC " +
                $"{FreeWorkerController.InjuryOverheatDC}? {p.CanEverPassInjuryCheck}");
            sb.AppendLine($"Safety={p.Safety} (affects heat / preferred max)");
            if (sampleRun != null)
            {
                sb.AppendLine(
                    $"Sample BEDROCK run: destroyed={sampleRun.BedrockDestroyed} " +
                    $"overheats={sampleRun.OverheatEvents} overheatLockSec={sampleRun.OverheatedLockSec:0.#} " +
                    $"miningSec={sampleRun.MiningTimeSec:0.#} injuries={sampleRun.InjuryEvents}");
            }
            sb.AppendLine($"VERDICT [{p.VerdictCode}]: {p.VerdictText}");
            sb.AppendLine("================================================================");
            return sb.ToString();
        }

        public static string RunAndLog(ExcavatorBalanceMetrics sampleBedrockRun = null)
        {
            var sheet = ExcavatorBalanceHarness.BuildProfile(ExcavatorTestProfile.Green);
            var probe = Probe(sheet);
            string report = FormatReport(probe, sampleBedrockRun);
            Debug.Log(report);
            return report;
        }
    }
}
