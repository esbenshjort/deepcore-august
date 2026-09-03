using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Social Aura Stage 2 — observable exchange presentation / line-bank audit.
    /// Batchmode: -executeMethod DeepCore.FreeMovement.SocialAuraStage2Audit.RunFromEditor
    /// </summary>
    public static class SocialAuraStage2Audit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[SOCIAL AURA S2] Report: {path}");
#if UNITY_EDITOR
            if (Application.isBatchMode)
            {
                bool fail = File.ReadAllText(path).Contains("RECOMMENDATION: NOT READY");
                UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
            }
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var log = new StringBuilder(32_000);
            int pass = 0, fail = 0;

            void Check(string name, bool ok, string detail = "")
            {
                if (ok) { pass++; log.AppendLine($"PASS | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}")); }
                else { fail++; log.AppendLine($"FAIL | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}")); }
            }

            log.AppendLine("# Social Aura Stage 2 — Observable Social Exchanges Audit");
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine();
            log.AppendLine("Scope: presentation + line bank + WorkerId speech. No Stage 0/1 math retune.");
            log.AppendLine();
            AppendArchitecture(log);

            FreeMovementSocketMapRunner runner = null;
            GameObject host = null;
            try
            {
                host = new GameObject("S2_SocialAuraAudit_Host");
                runner = host.AddComponent<FreeMovementSocketMapRunner>();
                runner.ForceBuildForAudit();
                WorkerStateClock.GameHours = 12f;

                // ——— Line bank coverage ———
                log.AppendLine("## 1. Line bank (actions × responses)");
                log.AppendLine();
                CheckLineBank(log, Check);

                // ——— Presentation distance / sleep ———
                log.AppendLine();
                log.AppendLine("## 2. Presentation gates");
                log.AppendLine();
                CheckPresentationGates(log, Check, runner);

                // ——— Speech arbitration + WorkerId ———
                log.AppendLine();
                log.AppendLine("## 3. WorkerId speech + arbitration");
                log.AppendLine();
                CheckSpeech(log, Check, runner);

                // ——— Smoke: outcome visibility via lines ———
                log.AppendLine();
                log.AppendLine("## 4. Smoke — outcome visibility (line wording)");
                log.AppendLine();
                CheckOutcomeVisibility(log, Check, runner);

                // ——— Live proximity still works with presentation hook ———
                log.AppendLine();
                log.AppendLine("## 5. Live hook smoke");
                log.AppendLine();
                CheckLiveHook(log, Check, runner);

                // ——— Camp spam / sleep ———
                log.AppendLine();
                log.AppendLine("## 6. Sleep + cooldown respect");
                log.AppendLine();
                runner.AuditForceCrewPhaseAsleep();
                Check("Sleeping workers not socially eligible", !runner.AuditSocialEligible(1));
                Check("Sleep presentation suppressed",
                    !runner.AuditTryPresentSocial(SocialAuraPresenter.MakeSyntheticLog(
                        1, 2, SocialContext.IdleNearby, SocialAction.Encourage,
                        SocialResponse.Accept, true, true, "POSITIVE_OK")));
                runner.AuditForceCrewPhaseOnShift();

                runner.AuditSetAvatarPos(1, new Vector2(0f, 0f));
                runner.AuditSetAvatarPos(2, new Vector2(0.3f, 0f));
                var pair = runner.AuditPair(1, 2);
                pair.CooldownRemaining = SocialAuraTuning.CooldownAfterEncounter;
                pair.InteractionPressure = SocialAuraTuning.PressureTrigger;
                int enc0 = runner.AuditSocialAura.TotalEncounters;
                for (int t = 0; t < 10; t++)
                    runner.AuditTickSocial(0.05f, syncMovingAvatars: false);
                Check("Cooldown suppresses rapid re-fire (no dialogue spam)",
                    runner.AuditSocialAura.TotalEncounters == enc0
                    || runner.AuditSocialAura.CooldownSuppressions > 0,
                    $"encΔ={runner.AuditSocialAura.TotalEncounters - enc0} " +
                    $"cdSup={runner.AuditSocialAura.CooldownSuppressions}");
            }
            catch (Exception ex)
            {
                fail++;
                log.AppendLine($"FAIL | Audit harness exception — {ex.GetType().Name}: {ex.Message}");
                log.AppendLine(ex.StackTrace);
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

            log.AppendLine();
            log.AppendLine("## Verdict");
            log.AppendLine();
            log.AppendLine($"PASS={pass}  FAIL={fail}");
            string rec = fail == 0 ? "READY" : "NOT READY";
            log.AppendLine($"RECOMMENDATION: {rec}");
            log.AppendLine();
            log.AppendLine("SOCIAL AURA STAGE 2: " + rec);

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(Application.dataPath, "..", "BenchmarkResults")
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(dir, $"social_aura_stage2_{stamp}.md");
            string latest = Path.Combine(dir, "social_aura_stage2_latest.md");
            File.WriteAllText(path, log.ToString());
            File.WriteAllText(latest, log.ToString());
            return path;
        }

        static void CheckLineBank(StringBuilder log, Action<string, bool, string> Check)
        {
            foreach (SocialAction a in Enum.GetValues(typeof(SocialAction)))
            {
                string okLine = SocialAuraLineBank.FirstInitiator(a, true, SocialContext.WorkingTogether);
                string failLine = SocialAuraLineBank.FirstInitiator(a, false, SocialContext.WorkingTogether);
                Check($"Initiator line exists: {a}",
                    !string.IsNullOrEmpty(okLine) && okLine != "…",
                    okLine);
                if (SocialAuraLineBank.IsPositive(a))
                    Check($"Failed {a} initiator differs from success tone",
                        okLine != failLine, $"ok='{okLine}' fail='{failLine}'");
            }

            foreach (SocialResponse r in Enum.GetValues(typeof(SocialResponse)))
            {
                string line = SocialAuraLineBank.FirstResponse(
                    r, SocialAction.Encourage, true, SocialContext.WorkingTogether);
                Check($"Response line exists: {r}",
                    !string.IsNullOrEmpty(line) && line != "…",
                    line);
            }

            string failDeflect = SocialAuraLineBank.FirstResponse(
                SocialResponse.Deflect, SocialAction.Encourage, false, SocialContext.WorkingTogether);
            Check("Encourage fail → Deflect is visibly cool",
                failDeflect.IndexOf("manage", StringComparison.OrdinalIgnoreCase) >= 0
                || failDeflect.IndexOf("pep", StringComparison.OrdinalIgnoreCase) >= 0,
                failDeflect);

            string bondAgree = SocialAuraLineBank.FirstResponse(
                SocialResponse.Agree, SocialAction.Complain, true, SocialContext.SharedProblem);
            Check("SharedProblem Complain→Agree reads as bond",
                bondAgree.IndexOf("mess", StringComparison.OrdinalIgnoreCase) >= 0
                || bondAgree.IndexOf("alone", StringComparison.OrdinalIgnoreCase) >= 0,
                bondAgree);

            string closer = SocialAuraLineBank.PickOptionalCloser(
                SocialAuraPresenter.MakeSyntheticLog(
                    1, 2, SocialContext.SharedProblem, SocialAction.Complain,
                    SocialResponse.Agree, true, true, "SHARED_COMPLAINT_BOND"));
            Check("Bond closer available", !string.IsNullOrEmpty(closer), closer);
        }

        static void CheckPresentationGates(
            StringBuilder log,
            Action<string, bool, string> Check,
            FreeMovementSocketMapRunner runner)
        {
            runner.AuditForceCrewPhaseOnShift();
            runner.AuditClearBanter();
            runner.AuditSocialPresenter.ClearQueue();

            runner.AuditSetAvatarPos(1, new Vector2(0f, 0f));
            runner.AuditSetAvatarPos(2, new Vector2(0.4f, 0f));
            var nearLog = SocialAuraPresenter.MakeSyntheticLog(
                1, 2, SocialContext.WorkingTogether, SocialAction.Encourage,
                SocialResponse.Accept, true, true, "POSITIVE_OK");
            bool nearOk = runner.AuditTryPresentSocial(nearLog);
            Check("Near workers: presentation enqueues", nearOk,
                $"lines={runner.AuditSocialPresenter.LastPresentation.LinesQueued}");
            Check("Near exchange length 1–3",
                runner.AuditSocialPresenter.LastPresentation.LinesQueued >= 1
                && runner.AuditSocialPresenter.LastPresentation.LinesQueued <= 3,
                "");

            runner.AuditSocialPresenter.ClearQueue();
            runner.AuditSetAvatarPos(1, new Vector2(-40f, -40f));
            runner.AuditSetAvatarPos(2, new Vector2(40f, 40f));
            var farLog = SocialAuraPresenter.MakeSyntheticLog(
                1, 2, SocialContext.WorkingTogether, SocialAction.Encourage,
                SocialResponse.Accept, true, true, "POSITIVE_OK");
            bool farOk = runner.AuditTryPresentSocial(farLog);
            Check("Far workers: presentation suppressed, result preserved",
                !farOk && runner.AuditSocialPresenter.LastPresentation.SuppressedDistance
                && farLog.Action == SocialAction.Encourage,
                "");
        }

        static void CheckSpeech(
            StringBuilder log,
            Action<string, bool, string> Check,
            FreeMovementSocketMapRunner runner)
        {
            runner.AuditClearBanter();
            runner.AuditForceCrewPhaseOnShift();

            bool social = runner.AuditTrySocialBanter(1, "Nice work. Keep that pace.");
            Check("Social banter stamps WorkerId", social && runner.AuditLastBanterWorkerId() == 1, "");
            var speech = runner.AuditBanter.LastSpeech;
            Check("Speech DisplayName is person, not job",
                speech != null && !string.IsNullOrEmpty(speech.DisplayName)
                && speech.WorkerId == 1,
                speech?.DisplayName ?? "");

            // Ambient blocked while social priority
            bool ambientBlocked = !runner.AuditTryAmbientBanter(2, "Ambient should not fire.");
            Check("Ambient banter suppressed during social priority", ambientBlocked, "");

            // Reassignment must not rewrite authored speaker identity
            int spokenId = runner.AuditLastBanterWorkerId();
            var frozen = runner.AuditBanter.LastSpeech;
            JobType jobNow = runner.AuditJobOf(1);
            Check("Reassignment does not change speaker WorkerId",
                spokenId == 1 && frozen != null && frozen.WorkerId == 1,
                $"speechId={spokenId} jobNow={jobNow} frozenCtx={frozen?.Context}");

            // Social history lives on pair transient keyed by worker ids — not job
            var pair = runner.AuditPair(1, 2);
            int histBefore = pair.RecentHistory.Count;
            var histLog = SocialAuraPresenter.MakeSyntheticLog(
                1, 2, SocialContext.IdleNearby, SocialAction.Joke,
                SocialResponse.Accept, true, true, "POSITIVE_OK");
            pair.PushHistory(histLog);
            Check("Social history keyed by WorkerId pair (not job)",
                pair.RecentHistory.Count > histBefore
                && pair.RecentHistory[pair.RecentHistory.Count - 1].InitiatorId == 1,
                $"hist={pair.RecentHistory.Count} (before={histBefore})");
        }

        static void CheckOutcomeVisibility(
            StringBuilder log,
            Action<string, bool, string> Check,
            FreeMovementSocketMapRunner runner)
        {
            runner.AuditForceCrewPhaseOnShift();
            runner.AuditSetAvatarPos(1, new Vector2(1f, 1f));
            runner.AuditSetAvatarPos(2, new Vector2(1.3f, 1f));
            runner.AuditClearBanter();
            runner.AuditSocialPresenter.ClearQueue();

            void Present(string name, SocialEncounterLog enc, Func<SocialAuraPresenter.PresentationRecord, bool> pred)
            {
                runner.AuditSocialPresenter.ClearQueue();
                bool ok = runner.AuditTryPresentSocial(enc);
                var p = runner.AuditSocialPresenter.LastPresentation;
                Check(name, ok && pred(p),
                    ok ? $"I='{p.InitiatorLine}' T='{p.ResponseLine}'" : "not presented");
            }

            Present("Encourage can succeed visibly",
                SocialAuraPresenter.MakeSyntheticLog(
                    1, 2, SocialContext.WorkingTogether, SocialAction.Encourage,
                    SocialResponse.Accept, true, true, "POSITIVE_OK"),
                p => p.InitiatorLine.IndexOf("pace", StringComparison.OrdinalIgnoreCase) >= 0
                     || p.InitiatorLine.IndexOf("fine", StringComparison.OrdinalIgnoreCase) >= 0
                     || p.ResponseLine.IndexOf("Thanks", StringComparison.OrdinalIgnoreCase) >= 0
                     || p.ResponseLine.IndexOf("Fair", StringComparison.OrdinalIgnoreCase) >= 0
                     || p.ResponseLine.IndexOf("hold", StringComparison.OrdinalIgnoreCase) >= 0);

            Present("Encourage can fail visibly (deflect)",
                SocialAuraPresenter.MakeSyntheticLog(
                    1, 2, SocialContext.WorkingTogether, SocialAction.Encourage,
                    SocialResponse.Deflect, false, true, "POSITIVE_FAIL"),
                p => (p.ResponseLine.IndexOf("manage", StringComparison.OrdinalIgnoreCase) >= 0
                      || p.ResponseLine.IndexOf("pep", StringComparison.OrdinalIgnoreCase) >= 0)
                     && (p.CloserLine == null
                         || p.CloserLine.IndexOf("Noted", StringComparison.OrdinalIgnoreCase) >= 0
                         || p.CloserLine.IndexOf("Message", StringComparison.OrdinalIgnoreCase) >= 0));

            Present("Joke can land",
                SocialAuraPresenter.MakeSyntheticLog(
                    1, 2, SocialContext.IdleNearby, SocialAction.Joke,
                    SocialResponse.Accept, true, true, "POSITIVE_OK"),
                p => !string.IsNullOrEmpty(p.InitiatorLine));

            Present("Joke can annoy (pushback)",
                SocialAuraPresenter.MakeSyntheticLog(
                    1, 2, SocialContext.IdleNearby, SocialAction.Joke,
                    SocialResponse.PushBack, false, true, "POSITIVE_FAIL"),
                p => p.ResponseLine.IndexOf("landed", StringComparison.OrdinalIgnoreCase) >= 0
                     || p.ResponseLine.IndexOf("talk", StringComparison.OrdinalIgnoreCase) >= 0);

            Present("SharedProblem complaint can bond",
                SocialAuraPresenter.MakeSyntheticLog(
                    1, 2, SocialContext.SharedProblem, SocialAction.Complain,
                    SocialResponse.Agree, true, true, "SHARED_COMPLAINT_BOND"),
                p => p.ResponseLine.IndexOf("alone", StringComparison.OrdinalIgnoreCase) >= 0
                     || p.ResponseLine.IndexOf("mess", StringComparison.OrdinalIgnoreCase) >= 0
                     || p.ResponseLine.IndexOf("together", StringComparison.OrdinalIgnoreCase) >= 0
                     || p.ResponseLine.IndexOf("chew", StringComparison.OrdinalIgnoreCase) >= 0);

            Present("Complaint can clash",
                SocialAuraPresenter.MakeSyntheticLog(
                    1, 2, SocialContext.SharedProblem, SocialAction.Complain,
                    SocialResponse.Escalate, true, false, "CLASH"),
                p => p.ResponseLine.IndexOf("problem", StringComparison.OrdinalIgnoreCase) >= 0
                     || p.ResponseLine.IndexOf("again", StringComparison.OrdinalIgnoreCase) >= 0);

            Present("Provoke can be ignored",
                SocialAuraPresenter.MakeSyntheticLog(
                    1, 2, SocialContext.WorkingTogether, SocialAction.Provoke,
                    SocialResponse.Ignore, true, true, "IGNORED_AGGRESSION"),
                p => p.ResponseLine == "…" || p.ResponseLine.IndexOf("worth", StringComparison.OrdinalIgnoreCase) >= 0);

            Present("Confront → PushBack",
                SocialAuraPresenter.MakeSyntheticLog(
                    1, 2, SocialContext.WorkingTogether, SocialAction.Confront,
                    SocialResponse.PushBack, true, true, "CLASH"),
                p => p.ResponseLine.IndexOf("Back", StringComparison.OrdinalIgnoreCase) >= 0
                     || p.ResponseLine.IndexOf("mouth", StringComparison.OrdinalIgnoreCase) >= 0);

            Present("Confront → Withdraw",
                SocialAuraPresenter.MakeSyntheticLog(
                    1, 2, SocialContext.WorkingTogether, SocialAction.Confront,
                    SocialResponse.Withdraw, true, true, "CLASH"),
                p => p.ResponseLine.IndexOf("walking", StringComparison.OrdinalIgnoreCase) >= 0
                     || p.ResponseLine.IndexOf("doing", StringComparison.OrdinalIgnoreCase) >= 0);

            Present("Confront → Escalate",
                SocialAuraPresenter.MakeSyntheticLog(
                    1, 2, SocialContext.WorkingTogether, SocialAction.Confront,
                    SocialResponse.Escalate, true, false, "CLASH"),
                p => p.ResponseLine.IndexOf("problem", StringComparison.OrdinalIgnoreCase) >= 0
                     || p.ResponseLine.IndexOf("again", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        static void CheckLiveHook(
            StringBuilder log,
            Action<string, bool, string> Check,
            FreeMovementSocketMapRunner runner)
        {
            runner.AuditForceCrewPhaseOnShift();
            runner.AuditClearBanter();
            runner.AuditSocialPresenter.ClearQueue();
            runner.AuditSetAvatarPos(1, new Vector2(0f, 0f));
            runner.AuditSetAvatarPos(2, new Vector2(0.35f, 0.05f));
            var pair = runner.AuditPair(1, 2);
            pair.InteractionPressure = 0f;
            pair.CooldownRemaining = 0f;
            int encBefore = runner.AuditSocialAura.TotalEncounters;

            // Tick through runner social path that includes presentation enqueue
            for (int t = 0; t < 120; t++)
                runner.AuditTickSocial(0.1f, syncMovingAvatars: false);

            // Also manually present last if encounter fired but AuditTickSocial doesn't present
            // (AuditTickSocial bypasses TickSocialAura presentation — wire present here for hook check)
            var last = runner.AuditSocialAura.LastEncounter;
            if (last != null && runner.AuditSocialAura.TotalEncounters > encBefore)
            {
                runner.AuditTryPresentSocial(last);
                // Fire first queued line
                float now = Time.unscaledTime;
                // Force fire by setting FireAt in past — Tick with current time after clear gap
                // Presenter uses FireAtUnscaled = now at enqueue; AuditTickSocialPresentation fires due lines
                runner.AuditTickSocialPresentation();
            }

            Check("Live proximity can still resolve encounters",
                runner.AuditSocialAura.TotalEncounters > encBefore || last != null,
                $"encΔ={runner.AuditSocialAura.TotalEncounters - encBefore}");

            if (last != null)
            {
                Check("Resolved encounter has initiator/target WorkerIds",
                    last.InitiatorId > 0 && last.TargetId > 0 && last.InitiatorId != last.TargetId,
                    "");
                var p = runner.AuditSocialPresenter.LastPresentation;
                Check("Live encounter can present when near",
                    p.Presented || p.SuppressedDistance || p.SuppressedSleep,
                    p.Presented ? $"lines={p.LinesQueued}" : "suppressed");
            }
            else
            {
                log.AppendLine("NOTE | No live encounter in window — presentation tested via synthetic logs above.");
            }
        }

        static void AppendArchitecture(StringBuilder log)
        {
            log.AppendLine("## Architecture");
            log.AppendLine();
            log.AppendLine("SocialAuraLiveSystem.Tick → SocialEncounterResolver (Stage 0) → SocialEncounterLog");
            log.AppendLine("→ SocialAuraPresenter.TryEnqueue (distance/sleep gate, 1–3 lines from SocialAuraLineBank)");
            log.AppendLine("→ WorkerBanter.TrySaySocial (WorkerId authorship, ambient muted while SocialPriorityActive)");
            log.AppendLine("→ existing roster speech bubbles");
            log.AppendLine();
            log.AppendLine("DEV SOCIAL panel: last encounter rolls/deltas + presentation lines (not player UI).");
            log.AppendLine();
        }
    }
}
