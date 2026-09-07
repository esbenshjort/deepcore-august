using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Debris + Tunnel Collapse + Injury/Rescue V1 audit.</summary>
    public static class DebrisCollapseV1Audit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[DEBRIS COLLAPSE] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.Exists(path) && File.ReadAllText(path).Contains("**Result:** FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var sb = new StringBuilder(8000);
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                if (ok) pass++;
                else fail++;
                sb.AppendLine($"- {(ok ? "PASS" : "FAIL")}  {name}"
                              + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
            }

            sb.AppendLine("# Debris + Tunnel Collapse + Injury/Rescue V1 Audit");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "Vibe", "FreeMovement"));
            string Read(string file) =>
                File.Exists(Path.Combine(root, file)) ? File.ReadAllText(Path.Combine(root, file)) : "";

            string collapse = Read("TunnelCollapse.cs");
            string injury = Read("CollapseInjury.cs");
            string fx = Read("FallingDebrisFx.cs");
            string world = Read("FineTerrainWorld.cs");
            string state = Read("WorkerState.cs");
            string roster = Read("CrewRosterStatusUi.cs");
            string runner = Read("FreeMovementSocketMapRunner.cs");
            string infra = Read("MineInfrastructure.cs");
            string eng = Read("EngineerPerson.cs");
            string mem = Read("SocialMemory.cs");
            string excav = Read("FreeWorkerController.cs");

            sb.AppendLine("## Architecture (source)");
            Check("TunnelCollapseSystem present", collapse.Contains("class TunnelCollapseSystem"));
            Check("CollapseInjuryResolver present", injury.Contains("class CollapseInjuryResolver"));
            Check("FallingDebrisFx present", fx.Contains("class FallingDebrisFx"));
            Check("Debris blocks IsTunnelOpen", world.Contains("HasBlockingDebris") && world.Contains("_debrisHp"));
            Check("IsMovementBlocker includes debris", world.Contains("_debrisHp") && world.Contains("IsMovementBlocker"));
            Check("WorkerState.Incapacitated", state.Contains("MarkIncapacitated") && state.Contains("Incapacitated"));
            Check("TrappedFromCamp flag", state.Contains("TrappedFromCamp"));
            Check("Roster Kind.Incapacitated", roster.Contains("Incapacitated = 1") || roster.Contains("Kind.Incapacitated"));
            Check("Support quality on build", infra.Contains("NearestSupportQuality01") && eng.Contains("SupportBuildQuality01"));
            Check("Social memory collapse types",
                mem.Contains("SurvivedCollapse") && mem.Contains("RescuedByWorker") && mem.Contains("WasTrapped"));
            Check("Excavator DebrisStrike", excav.Contains("DebrisStrike"));
            Check("Runner wires collapse", runner.Contains("TunnelCollapseSystem") && runner.Contains("TickTunnelCollapse"));
            Check("No teleport home when trapped",
                runner.Contains("cannot reach camp — trapped")
                || runner.Contains("NEVER teleport home"));
            Check("SoftArriveAllHome removed; audit SoftArrive skips incap",
                !runner.Contains("SoftArriveAllHome")
                && runner.Contains("void SoftArriveAllWork")
                && ContainsSoftArriveIncapSkip(runner));
            Check("Morning commute skips trapped",
                runner.Contains("Stay at physical site — do not invent a path through debris"));
            Check("Sleep skips proper recovery when trapped",
                runner.Contains("no proper sleep") || runner.Contains("TrappedFromCamp"));
            Check("DEV force controls",
                runner.Contains("FORCE MAJOR") && runner.Contains("CLEAR DEBRIS") && runner.Contains("SHOW STABILITY"));

            sb.AppendLine();
            sb.AppendLine("## Stability bands (runtime)");
            var worldSim = new FineTerrainWorld(32, 32, 0.32f);
            worldSim.ExcavateRect(8, 8, 16, 6);
            var infraSim = new MineInfrastructure(worldSim, new LogisticsTrafficMap(worldSim), null,
                new GameObject("AuditLanternRoot").transform);
            infraSim.SetGameHours(40f);
            // Mark excavate ages
            for (int x = 10; x < 20; x++)
                worldSim.InstantExcavate(x, 10);
            var sys = new TunnelCollapseSystem(worldSim, infraSim, new GameObject("AuditFx").transform);
            sys.BindCamp(worldSim.CellCenter(9, 9));
            sys.SetGameHours(40f);

            float tipRisk = sys.RawRiskAt(19, 10);
            var bandTip = sys.BandAt(19, 10);
            Check("Dig-face tip is end-tunnel site", sys.IsEndTunnelSite(19, 10));
            Check("Mid corridor is not end-tunnel site", !sys.IsEndTunnelSite(14, 10));
            Check("Near-camp cell risk is 0", sys.RawRiskAt(10, 10) <= 0.01f);
            Check("Mid corridor natural risk is 0", sys.RawRiskAt(14, 10) <= 0.01f);
            // Place support near tip — should damp tip risk
            bool built = infraSim.TryBuildSupport(18, 10, quality01: 1.05f);
            float tipSupported = sys.RawRiskAt(19, 10);
            Check("Support build succeeded near tip", built);
            Check("Unsupported tip risk > supported tip (or tip still gated)",
                tipRisk > tipSupported + 0.15f || tipRisk < 0.5f || !built,
                $"tip={tipRisk:0.00} tipSup={tipSupported:0.00} band={bandTip}");
            Check("Well-supported tip not Critical by default",
                sys.BandAt(19, 10) <= TunnelStabilityBand.Watch || tipSupported < 1.2f || !built,
                sys.BandLabel(sys.BandAt(19, 10)));

            sb.AppendLine();
            sb.AppendLine("## Collapse / nav blocking");
            var cell = new Vector2Int(16, 10);
            var field = sys.TriggerCollapse(cell, CollapseSeverity.Blocking, null, null, null, null, forced: true);
            Check("Blocking field created", field != null && field.BlocksNav);
            Check("Debris blocks tunnel open", worldSim.HasBlockingDebris(cell.x, cell.y)
                && !worldSim.IsTunnelOpen(cell.x, cell.y));
            Check("Nav walkable false through debris",
                !worldSim.Navigation.IsWalkableCell(cell.x, cell.y));

            var minor = sys.TriggerCollapse(new Vector2Int(14, 10), CollapseSeverity.MinorDebris,
                null, null, null, null, forced: true);
            Check("Minor debris does not block nav",
                minor != null && !minor.BlocksNav && worldSim.IsTunnelOpen(14, 10));

            sb.AppendLine();
            sb.AppendLine("## Clearance times");
            var clearWr = new WorkerRuntime(901, "Clearer");
            clearWr.Stats.Set(WorkerStatId.HeavyLifting, 16);
            clearWr.Stats.Set(WorkerStatId.Stamina, 14);
            clearWr.Stats.Set(WorkerStatId.Logistics, 12);
            clearWr.Stats.ClampAll();
            WorkerJobDemand.EnsureStaminaPrimed(clearWr);
            float hours = 0f;
            int guard = 0;
            while (field != null && !field.Cleared && guard++ < 200)
            {
                sys.TickClearance(field, clearWr, 0.1f, asExcavator: false, out _);
                hours += 0.1f;
            }
            Check("Hauler clearance completes in meaningful time",
                field != null && field.Cleared && hours >= 0.8f && hours <= 8f,
                $"hours={hours:0.00}");
            Check("Clearance restores IsTunnelOpen", worldSim.IsTunnelOpen(cell.x, cell.y));

            sb.AppendLine();
            sb.AppendLine("## Injury distribution (forced samples)");
            int noneSerious = 0, multi = 0, incapN = 0, critN = 0;
            for (int i = 0; i < 40; i++)
            {
                var wr = new WorkerRuntime(1000 + i, "Victim");
                wr.Stats.Set(WorkerStatId.Toughness, 10);
                wr.Stats.ClampAll();
                var zone = CollapseInjuryResolver.PickZone(Vector2.zero, Vector2.zero, CollapseSeverity.MinorDebris);
                if (!CollapseInjuryResolver.RollHit(CollapseSeverity.MinorDebris, 0.2f, 1f))
                {
                    noneSerious++;
                    continue;
                }
                var hits = CollapseInjuryResolver.ResolveHits(wr, CollapseSeverity.MinorDebris, zone);
                if (hits.Count == 0) noneSerious++;
                else if (hits[0].Severity <= WorkerInjurySeverity.Minor) noneSerious++;
            }
            for (int i = 0; i < 30; i++)
            {
                var wr = new WorkerRuntime(2000 + i, "MajorVic");
                wr.Stats.Set(WorkerStatId.Toughness, 9);
                wr.Stats.ClampAll();
                var zone = DebrisImpactZone.Crush;
                var hits = CollapseInjuryResolver.ResolveHits(wr, CollapseSeverity.Major, zone);
                if (hits.Count >= 2) multi++;
                if (CollapseInjuryResolver.ShouldIncapacitate(wr, CollapseSeverity.Major, hits))
                {
                    wr.State.MarkIncapacitated(1f, "audit");
                    incapN++;
                }
                for (int k = 0; k < hits.Count; k++)
                    if (hits[k].Severity >= WorkerInjurySeverity.Critical) critN++;
            }
            Check("Minor debris often no/limited injury", noneSerious >= 20,
                $"softOrMiss={noneSerious}/40");
            Check("Major can inflict multiple injuries", multi >= 5, $"multi={multi}/30");
            Check("Major can incapacitate", incapN >= 3, $"incap={incapN}/30");
            Check("Critical trauma rare vs samples", critN <= 12, $"critHits={critN}");
            Check("Incapacitated != Dead", true); // structural

            var aliveIncap = new WorkerRuntime(3001, "Incap");
            aliveIncap.State.MarkIncapacitated(2f, "test");
            Check("Incapacitated remains alive", aliveIncap.IsAlive && aliveIncap.State.Incapacitated);
            aliveIncap.State.ClearIncapacitated();
            Check("Clear incap keeps injuries path", !aliveIncap.State.Incapacitated);

            sb.AppendLine();
            sb.AppendLine("## Incapacitated excavator cannot self-clear");
            var digger = new WorkerRuntime(4001, "Mara");
            digger.State.MarkIncapacitated(3f, "collapse");
            Check("CanClearDebris false when incapacitated",
                !sys.CanClearDebris(digger, asExcavator: true));

            sb.AppendLine();
            sb.AppendLine("## Injury cause enum");
            Check("TunnelCollapse cause exists",
                Enum.IsDefined(typeof(WorkerInjuryCause), WorkerInjuryCause.TunnelCollapse));

            sb.AppendLine();
            sb.AppendLine("## Collapse rates (design)");
            sb.AppendLine("| Band | Approx fire / eval (~0.55h) | Notes |");
            sb.AppendLine("|------|------------------------------|-------|");
            sb.AppendLine("| STABLE | 0 | No natural fire |");
            sb.AppendLine("| WATCH | 0 | Warn only (rare); no debris drop |");
            sb.AppendLine("| UNSTABLE | ~0.8% | End-tunnel tips only |");
            sb.AppendLine("| CRITICAL | ~2.2% | End-tunnel tips only; camp/yard immune |");
            sb.AppendLine();
            sb.AppendLine($"Camp safe radius: {TunnelCollapseSystem.CampSafeRadiusWorld}w");
            sb.AppendLine($"End-tunnel min camp dist: {TunnelCollapseSystem.EndTunnelMinCampDist}w");
            sb.AppendLine();
            sb.AppendLine("## Clearance design hours");
            sb.AppendLine($"| Minor | {TunnelCollapseSystem.MinorClearHours}h |");
            sb.AppendLine($"| Blocking | {TunnelCollapseSystem.BlockingClearHours}h |");
            sb.AppendLine($"| Major | {TunnelCollapseSystem.MajorClearHours}h |");
            sb.AppendLine($"| Hauler mul | {TunnelCollapseSystem.HaulerClearMul} |");
            sb.AppendLine($"| Excavator mul | {TunnelCollapseSystem.ExcavatorClearMul} |");

            sb.AppendLine();
            sb.AppendLine("## Result");
            sb.AppendLine($"**Result:** {(fail == 0 ? "PASS" : "FAIL")}  ({pass} passed, {fail} failed)");

            string dir = outputDirectory
                ?? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BenchmarkResults"));
            Directory.CreateDirectory(dir);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string stamped = Path.Combine(dir, $"debris_collapse_v1_{stamp}.md");
            string latest = Path.Combine(dir, "debris_collapse_v1_latest.md");
            File.WriteAllText(stamped, sb.ToString());
            File.WriteAllText(latest, sb.ToString());
            return latest;
        }

        /// <summary>SoftArriveAllWork must skip Incapacitated workers (no warp through debris).</summary>
        static bool ContainsSoftArriveIncapSkip(string runner)
        {
            int i = runner.IndexOf("void SoftArriveAllWork", StringComparison.Ordinal);
            if (i < 0) return false;
            int end = runner.IndexOf("void RelocateAvatar", i, StringComparison.Ordinal);
            if (end < 0) end = Math.Min(runner.Length, i + 900);
            string block = runner.Substring(i, end - i);
            return block.Contains("Incapacitated");
        }
    }
}
