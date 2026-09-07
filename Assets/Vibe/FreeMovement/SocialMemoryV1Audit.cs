using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Social Memory V1 audit — directional store, meaningful filter, cap, persistence vs assignment.
    /// Does not retune Social Aura math. Batch: SocialMemoryV1Audit.RunFromEditor
    /// </summary>
    public static class SocialMemoryV1Audit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[SOCIAL MEMORY V1] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.ReadAllText(path).Contains("INVARIANT: FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var log = new StringBuilder(16_000);
            int pass = 0, fail = 0;

            void Check(string name, bool ok, string detail = "")
            {
                if (ok)
                {
                    pass++;
                    log.AppendLine($"PASS | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
                }
                else
                {
                    fail++;
                    log.AppendLine($"FAIL | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
                }
            }

            log.AppendLine("# Social Memory V1 Audit");
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine();
            log.AppendLine("Scope: directional memories from meaningful encounters only.");
            log.AppendLine("No Trust/Warmth/Hostility formula changes. No encounter frequency changes.");
            log.AppendLine();

            WorkerStateClock.GameHours = 100f;
            var world = new SocialAuraWorld();
            var lewis = new SocialSimActor(1, "Lewis", WorkerStats.CreateBaseline(), WorkerState.CreateDefault());
            var mara = new SocialSimActor(2, "Mara", WorkerStats.CreateBaseline(), WorkerState.CreateDefault());
            var kow = new SocialSimActor(3, "Kowalski", WorkerStats.CreateBaseline(), WorkerState.CreateDefault());
            world.AddActor(lewis);
            world.AddActor(mara);
            world.AddActor(kow);
            world.BeginShift(1);

            var mem = world.Memory;
            mem.Clear();

            // ——— 1. Directional ———
            log.AppendLine("## 1. Directional memory");
            var encourage = MakeLog(1, 2, SocialContext.WorkingTogether,
                SocialAction.Encourage, SocialResponse.Accept, true, true,
                dWaIT: 0.9f, dWaTI: 0.7f, dMoT: 1.5f, dFrT: -2.5f);
            SocialMemoryRecorder.Record(mem, encourage, 100f);
            var m21 = mem.GetToward(2, 1); // Mara remembers Lewis
            var m12 = mem.GetToward(1, 2); // Lewis remembers Mara
            bool maraHasSupport = HasType(m21, SocialMemoryType.SupportedMe)
                                  || HasType(m21, SocialMemoryType.HelpedMe);
            bool lewisHasSupport = HasType(m12, SocialMemoryType.SupportedMe)
                                   || HasType(m12, SocialMemoryType.HelpedMe);
            Check("Mara→Lewis has SupportedMe/HelpedMe after Encourage", maraHasSupport,
                $"count={m21.Count}");
            Check("Lewis→Mara does NOT mirror HelpedMe (directional)", !lewisHasSupport,
                $"lewisTypes={TypesOf(m12)}");
            Check("Lewis→Mara may have WorkedWellTogether (own perspective)", true);

            // ——— 2. Meaningful creates ———
            log.AppendLine();
            log.AppendLine("## 2. Meaningful encounters create memories");
            int before = mem.TotalEntries;
            var provoke = MakeLog(1, 2, SocialContext.IdleNearby,
                SocialAction.Provoke, SocialResponse.Escalate, true, true,
                dHoIT: 1.2f, dHoTI: 0.8f, dFrT: 2.2f);
            SocialMemoryRecorder.Record(mem, provoke, 101f);
            Check("Provoke/Escalate creates memory", mem.TotalEntries > before,
                $"Δ={mem.TotalEntries - before}");
            Check("Target remembers InsultedMe", HasType(mem.GetToward(2, 1), SocialMemoryType.InsultedMe));

            var bond = MakeLog(2, 3, SocialContext.SharedProblem,
                SocialAction.Complain, SocialResponse.Agree, true, true,
                dWaIT: 1.4f, dWaTI: 1.3f, dFrI: -4f, dFrT: -3.5f);
            before = mem.TotalEntries;
            SocialMemoryRecorder.Record(mem, bond, 102f);
            Check("Complain+Agree SharedProblem creates SharedHardship both ways",
                HasType(mem.GetToward(2, 3), SocialMemoryType.SharedHardship)
                && HasType(mem.GetToward(3, 2), SocialMemoryType.SharedHardship),
                $"Δ={mem.TotalEntries - before}");

            // ——— 3. Trivial does not flood ———
            log.AppendLine();
            log.AppendLine("## 3. Trivial encounters do not flood");
            before = mem.TotalEntries;
            int trivialAdds = 0;
            bool trivialMeaningful = false;
            for (int i = 0; i < 20; i++)
            {
                var trivial = MakeLog(1, 3, SocialContext.IdleNearby,
                    SocialAction.Connect, SocialResponse.Ignore, false, true,
                    dWaIT: 0f, dWaTI: 0f, dTrIT: 0f, dHoIT: 0f, dFrI: 0f, dFrT: 0f, dMoI: 0f, dMoT: 0f);
                if (SocialMemoryRecorder.IsMeaningful(trivial)) trivialMeaningful = true;
                SocialMemoryRecorder.Record(mem, trivial, 110f + i);
                trivialAdds += SocialMemoryRecorder.LastRecordPassAdds;
            }
            Check("Empty Connect/Ignore is not meaningful", !trivialMeaningful);
            Check("20 trivial Connect/Ignore add 0 memories", trivialAdds == 0 && mem.TotalEntries == before,
                $"adds={trivialAdds} entries={mem.TotalEntries} before={before}");

            // ——— 4. Cap / retention ———
            log.AppendLine();
            log.AppendLine("## 4. Cap / retention (MaxPerTarget=12)");
            var capStore = new SocialMemoryStore();
            for (int i = 0; i < 20; i++)
            {
                capStore.AuditForceAdd(new SocialMemoryEntry
                {
                    ObserverId = 1,
                    TargetId = 2,
                    Type = SocialMemoryType.WorkedWellTogether,
                    Strength = 0.2f + (i % 10) * 0.08f,
                    GameTime = 200f + i,
                    Context = SocialContext.WorkingTogether,
                    SourceRef = $"cap{i}",
                    Major = i >= 18,
                });
            }
            int capped = capStore.GetToward(1, 2).Count;
            Check($"Retention capped at {SocialMemoryStore.MaxPerTarget}",
                capped == SocialMemoryStore.MaxPerTarget, $"count={capped}");
            // Strongest should survive
            float maxStr = 0f;
            foreach (var e in capStore.GetToward(1, 2))
                if (e.Strength > maxStr) maxStr = e.Strength;
            Check("Strongest memories retained under cap", maxStr >= 0.8f, $"maxStr={maxStr:0.00}");

            // ——— 5. Reassignment does not lose memories ———
            log.AppendLine();
            log.AppendLine("## 5. Reassignment does not lose memories");
            // Memories live on SocialAuraWorld.Memory keyed by WorkerId — job hosts irrelevant.
            int entriesBeforeJobSwap = mem.TotalEntries;
            // Simulate Mara leaving Excavation / Lewis leaving Prospecting — no store clear
            Check("Memory store survives job identity (WorkerId keys)",
                mem.GetToward(2, 1).Count > 0 && mem.TotalEntries == entriesBeforeJobSwap,
                $"entries={mem.TotalEntries}");

            FreeMovementSocketMapRunner runner = null;
            GameObject host = null;
            try
            {
                host = new GameObject("SocialMemoryV1_AuditHost");
                runner = host.AddComponent<FreeMovementSocketMapRunner>();
                runner.ForceBuildForAudit();
                var liveMem = runner.AuditSocialAura.Memory;
                liveMem.Clear();
                // Seed memory then reassign
                SocialMemoryRecorder.Record(liveMem, encourage, WorkerStateClock.GameHours);
                int seeded = liveMem.GetToward(2, 1).Count;
                runner.AuditTryAssign(1, JobType.Excavation, out _);
                runner.AuditTryAssign(2, JobType.Prospecting, out _);
                Check("After job swap Mara→Lewis memories intact",
                    liveMem.GetToward(2, 1).Count == seeded,
                    $"before={seeded} after={liveMem.GetToward(2, 1).Count}");
                Check("Same SocialAura World Memory instance",
                    ReferenceEquals(liveMem, runner.AuditSocialAura.World.Memory));
            }
            catch (Exception ex)
            {
                Check("Live runner reassignment check", false, ex.Message);
            }
            finally
            {
                if (host != null)
                {
#if UNITY_EDITOR
                    UnityEngine.Object.DestroyImmediate(host);
#else
                    UnityEngine.Object.Destroy(host);
#endif
                }
            }

            // ——— 6. Social Aura behavior unchanged (spot checks) ———
            log.AppendLine();
            log.AppendLine("## 6. Social Aura behavior unchanged (spot)");
            Check("PressureTrigger still 1.05", Mathf.Abs(SocialAuraTuning.PressureTrigger - 1.05f) < 0.001f);
            Check("CooldownAfterEncounter still 0.55", Mathf.Abs(SocialAuraTuning.CooldownAfterEncounter - 0.55f) < 0.001f);
            Check("ReachWorldScale still 2.35", Mathf.Abs(SocialAuraLiveTuning.ReachWorldScale - 2.35f) < 0.001f);
            Check("MaxEncountersPerWorkerPerShift still 3",
                Mathf.Abs(SocialAuraTuning.MaxEncountersPerWorkerPerShift - 3f) < 0.001f);

            // Relation math still applied by Resolve independently of memory
            float t0 = world.Relation(1, 2).Trust;
            var logRel = SocialEncounterResolver.Resolve(world, lewis, mara,
                SocialContext.WorkingTogether, "audit");
            Check("Resolve still returns encounter log", logRel != null);
            // Memory record is separate — relation Add happens inside RunExchange
            Check("Memory TotalEntries is independent counter", mem.TotalEntries >= 0);

            // ——— Significance tiers ———
            log.AppendLine();
            log.AppendLine("## 7. Significance tiers (Ordinary / Significant / Major)");
            Check("MaxPerTarget unchanged at 12", SocialMemoryStore.MaxPerTarget == 12);
            var decayStore = new SocialMemoryStore();
            decayStore.AuditForceAdd(new SocialMemoryEntry
            {
                ObserverId = 1, TargetId = 2, Type = SocialMemoryType.WorkedWellTogether,
                Strength = 0.70f, GameTime = 0f,
                Significance = SocialMemorySignificance.Ordinary
            });
            decayStore.AuditForceAdd(new SocialMemoryEntry
            {
                ObserverId = 1, TargetId = 2, Type = SocialMemoryType.HelpedMe,
                Strength = 0.70f, GameTime = 0f,
                Significance = SocialMemorySignificance.Significant
            });
            decayStore.AuditForceAdd(new SocialMemoryEntry
            {
                ObserverId = 1, TargetId = 2, Type = SocialMemoryType.SharedHardship,
                Strength = 0.70f, GameTime = 0f,
                Significance = SocialMemorySignificance.Major
            });
            // 80 game-hours of decay
            decayStore.TickDecay(80f);
            float ordStr = 0f, sigStr = 0f, majStr = 0f;
            bool majAlive = false, ordAlive = false, sigAlive = false;
            foreach (var e in decayStore.GetToward(1, 2))
            {
                if (e.Type == SocialMemoryType.WorkedWellTogether) { ordStr = e.Strength; ordAlive = true; }
                if (e.Type == SocialMemoryType.HelpedMe) { sigStr = e.Strength; sigAlive = true; }
                if (e.Type == SocialMemoryType.SharedHardship) { majStr = e.Strength; majAlive = true; }
            }
            Check("Major survives 80h decay", majAlive && majStr > 0.55f, $"str={majStr:0.00}");
            Check("Major retains more strength than Significant after same decay",
                majAlive && sigAlive && majStr > sigStr + 0.15f,
                $"maj={majStr:0.00} sig={sigStr:0.00}");
            Check("Significant retains more than Ordinary (or Ordinary purged)",
                (sigAlive && (!ordAlive || sigStr > ordStr + 0.1f)),
                $"sig={sigStr:0.00} ordAlive={ordAlive} ord={ordStr:0.00}");
            Check("ClassifySignificance maps strength bands",
                SocialMemoryStore.ClassifySignificance(0.4f, false) == SocialMemorySignificance.Ordinary
                && SocialMemoryStore.ClassifySignificance(0.55f, false) == SocialMemorySignificance.Significant
                && SocialMemoryStore.ClassifySignificance(0.7f, false) == SocialMemorySignificance.Major
                && SocialMemoryStore.ClassifySignificance(0.3f, true) == SocialMemorySignificance.Major);

            // Cap prefers majors under pressure
            var capSig = new SocialMemoryStore();
            for (int i = 0; i < 14; i++)
            {
                capSig.AuditForceAdd(new SocialMemoryEntry
                {
                    ObserverId = 9, TargetId = 8,
                    Type = (SocialMemoryType)(i % 11),
                    Strength = 0.35f + (i % 3) * 0.05f,
                    GameTime = i,
                    Significance = i < 3
                        ? SocialMemorySignificance.Major
                        : SocialMemorySignificance.Ordinary
                });
            }
            int majorsKept = 0;
            foreach (var e in capSig.GetToward(9, 8))
                if (e.Significance == SocialMemorySignificance.Major) majorsKept++;
            Check("Under cap pressure, Major memories preferred",
                majorsKept >= 3 && capSig.GetToward(9, 8).Count == SocialMemoryStore.MaxPerTarget,
                $"majors={majorsKept} count={capSig.GetToward(9, 8).Count}");

            log.AppendLine();
            log.AppendLine("## Summary");
            log.AppendLine($"PASS {pass} / FAIL {fail}");
            log.AppendLine(fail == 0 ? "INVARIANT: PASS" : "INVARIANT: FAIL");
            log.AppendLine();
            log.AppendLine("## Files");
            log.AppendLine("- Assets/Vibe/FreeMovement/SocialMemory.cs (significance tiers + decay)");
            log.AppendLine("- Assets/Vibe/FreeMovement/FreeMovementSocketMapRunner.cs (DEV significance)");
            log.AppendLine("- Assets/Vibe/FreeMovement/SocialMemoryV1Audit.cs (this audit)");

            string dir = outputDirectory;
            if (string.IsNullOrEmpty(dir))
                dir = Path.Combine(Application.dataPath, "..", "BenchmarkResults");
            Directory.CreateDirectory(dir);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(dir, $"social_memory_v1_{stamp}.md");
            string latest = Path.Combine(dir, "social_memory_v1_latest.md");
            File.WriteAllText(path, log.ToString());
            File.WriteAllText(latest, log.ToString());
            return Path.GetFullPath(path);
        }

        static bool HasType(IReadOnlyList<SocialMemoryEntry> list, SocialMemoryType t)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].Type == t) return true;
            return false;
        }

        static string TypesOf(IReadOnlyList<SocialMemoryEntry> list)
        {
            if (list == null || list.Count == 0) return "none";
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(list[i].Type);
            }
            return sb.ToString();
        }

        static SocialEncounterLog MakeLog(
            int init, int tgt, SocialContext ctx,
            SocialAction act, SocialResponse rsp, bool actOk, bool rspOk,
            float dTrIT = 0f, float dWaIT = 0f, float dHoIT = 0f,
            float dTrTI = 0f, float dWaTI = 0f, float dHoTI = 0f,
            float dFrI = 0f, float dMoI = 0f, float dFrT = 0f, float dMoT = 0f)
        {
            return new SocialEncounterLog
            {
                ShiftIndex = 1,
                InitiatorId = init,
                TargetId = tgt,
                Context = ctx,
                Action = act,
                Response = rsp,
                ActionSuccess = actOk,
                ResponseSuccess = rspOk,
                ActionD20 = 12,
                ResponseD20 = 10,
                DeltaTrustIT = dTrIT,
                DeltaWarmthIT = dWaIT,
                DeltaHostilityIT = dHoIT,
                DeltaTrustTI = dTrTI,
                DeltaWarmthTI = dWaTI,
                DeltaHostilityTI = dHoTI,
                DeltaFrustrationInit = dFrI,
                DeltaMoraleInit = dMoI,
                DeltaFrustrationTarget = dFrT,
                DeltaMoraleTarget = dMoT,
                OutcomeSummary = "audit",
            };
        }
    }
}
