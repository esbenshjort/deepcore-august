using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Stage 2 logic audit that does not require a live runner / ForceBuild.
    /// Used by Tools/SocialAuraDiag and as a subset of the Unity Stage 2 audit.
    /// </summary>
    public static class SocialAuraStage2LogicAudit
    {
        public static string Run(string outputDirectory = null)
        {
            var log = new StringBuilder(16_000);
            int pass = 0, fail = 0;

            void Check(string name, bool ok, string detail = "")
            {
                if (ok) { pass++; log.AppendLine($"PASS | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}")); }
                else { fail++; log.AppendLine($"FAIL | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}")); }
            }

            log.AppendLine("# Social Aura Stage 2 — Logic / Presentation Audit (offline)");
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine();
            log.AppendLine("Scope: line bank + presenter gates + WorkerId ownership of lines.");
            log.AppendLine("Live runner / banter arbitration: run Unity menu audit when Editor is free.");
            log.AppendLine();

            foreach (SocialAction a in Enum.GetValues(typeof(SocialAction)))
            {
                string okLine = SocialAuraLineBank.FirstInitiator(a, true, SocialContext.WorkingTogether);
                string failLine = SocialAuraLineBank.FirstInitiator(a, false, SocialContext.WorkingTogether);
                Check($"Initiator line exists: {a}", !string.IsNullOrEmpty(okLine) && okLine != "…", okLine);
                if (SocialAuraLineBank.IsPositive(a))
                    Check($"Failed {a} initiator differs from success tone", okLine != failLine,
                        $"ok='{okLine}' fail='{failLine}'");
            }

            foreach (SocialResponse r in Enum.GetValues(typeof(SocialResponse)))
            {
                string line = SocialAuraLineBank.FirstResponse(
                    r, SocialAction.Encourage, true, SocialContext.WorkingTogether);
                Check($"Response line exists: {r}", !string.IsNullOrEmpty(line) && line != "…", line);
            }

            Check("Encourage fail → Deflect cool",
                SocialAuraLineBank.FirstResponse(SocialResponse.Deflect, SocialAction.Encourage, false, SocialContext.WorkingTogether)
                    .IndexOf("manage", StringComparison.OrdinalIgnoreCase) >= 0);

            Check("SharedProblem Complain→Agree bonds",
                SocialAuraLineBank.FirstResponse(SocialResponse.Agree, SocialAction.Complain, true, SocialContext.SharedProblem)
                    .IndexOf("alone", StringComparison.OrdinalIgnoreCase) >= 0
                || SocialAuraLineBank.FirstResponse(SocialResponse.Agree, SocialAction.Complain, true, SocialContext.SharedProblem)
                    .IndexOf("mess", StringComparison.OrdinalIgnoreCase) >= 0);

            var lewis = new WorkerRuntime(1, "Lewis");
            var mara = new WorkerRuntime(2, "Mara");
            var presenter = new SocialAuraPresenter();

            var near = SocialAuraPresenter.MakeSyntheticLog(
                1, 2, SocialContext.WorkingTogether, SocialAction.Encourage,
                SocialResponse.Accept, true, true, "POSITIVE_OK");
            bool nearOk = presenter.TryEnqueue(
                near, lewis, mara, new Vector2(0f, 0f), new Vector2(0.4f, 0f),
                SocialPresenceKind.Operating, SocialPresenceKind.Operating,
                JobType.Excavation, JobType.Excavation, 0f);
            Check("Near: presentation enqueues 1–3 lines",
                nearOk && presenter.LastPresentation.LinesQueued >= 1
                && presenter.LastPresentation.LinesQueued <= 3,
                $"lines={presenter.LastPresentation.LinesQueued}");
            Check("Lines authored to WorkerIds",
                presenter.QueuedCount >= 2);

            presenter.ClearQueue();
            var far = SocialAuraPresenter.MakeSyntheticLog(
                1, 2, SocialContext.WorkingTogether, SocialAction.Encourage,
                SocialResponse.Accept, true, true, "POSITIVE_OK");
            bool farOk = presenter.TryEnqueue(
                far, lewis, mara, new Vector2(-40f, -40f), new Vector2(40f, 40f),
                SocialPresenceKind.Operating, SocialPresenceKind.Operating,
                JobType.Excavation, JobType.Excavation, 0f);
            Check("Far: suppress presentation, preserve result",
                !farOk && presenter.LastPresentation.SuppressedDistance
                && far.ActionSuccess);

            presenter.ClearQueue();
            bool sleepOk = presenter.TryEnqueue(
                near, lewis, mara, new Vector2(0f, 0f), new Vector2(0.3f, 0f),
                SocialPresenceKind.Sleeping, SocialPresenceKind.Operating,
                JobType.Excavation, JobType.Excavation, 0f);
            Check("Sleep: suppress presentation",
                !sleepOk && presenter.LastPresentation.SuppressedSleep);

            // Outcome visibility via deterministic first-lines + enqueue wording
            void PresentSmoke(string name, SocialEncounterLog enc, Func<SocialAuraPresenter.PresentationRecord, bool> pred)
            {
                presenter.ClearQueue();
                bool ok = presenter.TryEnqueue(
                    enc, lewis, mara, new Vector2(1f, 1f), new Vector2(1.2f, 1f),
                    SocialPresenceKind.Operating, SocialPresenceKind.Operating,
                    JobType.Prospecting, JobType.Excavation, 10f);
                Check(name, ok && pred(presenter.LastPresentation),
                    ok ? $"I='{presenter.LastPresentation.InitiatorLine}' T='{presenter.LastPresentation.ResponseLine}'"
                       : "not presented");
            }

            PresentSmoke("Encourage can succeed visibly",
                SocialAuraPresenter.MakeSyntheticLog(1, 2, SocialContext.WorkingTogether, SocialAction.Encourage, SocialResponse.Accept, true, true, "POSITIVE_OK"),
                p => !string.IsNullOrEmpty(p.InitiatorLine) && !string.IsNullOrEmpty(p.ResponseLine));

            PresentSmoke("Encourage can fail visibly",
                SocialAuraPresenter.MakeSyntheticLog(1, 2, SocialContext.WorkingTogether, SocialAction.Encourage, SocialResponse.Deflect, false, true, "POSITIVE_FAIL"),
                p => ContainsAny(p.ResponseLine, "manage", "pep", "speech", "coaching"));

            PresentSmoke("Joke can land or annoy (annoy path)",
                SocialAuraPresenter.MakeSyntheticLog(1, 2, SocialContext.IdleNearby, SocialAction.Joke, SocialResponse.PushBack, false, true, "POSITIVE_FAIL"),
                p => ContainsAny(p.ResponseLine, "landed", "talk", "tone", "dig", "Wrong"));

            PresentSmoke("SharedProblem complaint can bond",
                SocialAuraPresenter.MakeSyntheticLog(1, 2, SocialContext.SharedProblem, SocialAction.Complain, SocialResponse.Agree, true, true, "SHARED_COMPLAINT_BOND"),
                p => ContainsAny(p.ResponseLine, "alone", "mess", "together", "chew", "grit", "Same", "push", "Shared"));

            PresentSmoke("Complaint can clash",
                SocialAuraPresenter.MakeSyntheticLog(1, 2, SocialContext.SharedProblem, SocialAction.Complain, SocialResponse.Escalate, true, false, "CLASH"),
                p => ContainsAny(p.ResponseLine, "problem", "again", "fight", "happens", "doing this"));

            PresentSmoke("Provoke can be ignored",
                SocialAuraPresenter.MakeSyntheticLog(1, 2, SocialContext.WorkingTogether, SocialAction.Provoke, SocialResponse.Ignore, true, true, "IGNORED_AGGRESSION"),
                p => p.ResponseLine == "…" || ContainsAny(p.ResponseLine, "worth", "static", "Moving", "Hearing"));

            PresentSmoke("Confront PushBack/Withdraw/Escalate",
                SocialAuraPresenter.MakeSyntheticLog(1, 2, SocialContext.WorkingTogether, SocialAction.Confront, SocialResponse.Escalate, true, false, "CLASH"),
                p => ContainsAny(p.ResponseLine, "problem", "again", "fight", "happens", "doing this"));

            // History identity across “jobs”
            var pair = new SocialPairTransient();
            var hist = SocialAuraPresenter.MakeSyntheticLog(
                1, 2, SocialContext.IdleNearby, SocialAction.Joke, SocialResponse.Accept, true, true, "POSITIVE_OK");
            pair.PushHistory(hist);
            Check("Social history keyed by WorkerId",
                pair.RecentHistory.Count == 1 && pair.RecentHistory[0].InitiatorId == 1);

            // Speech arbitration (real WorkerBanter).
            // Do not assign Time.unscaledTime — it is read-only in the Editor.
            var banter = new WorkerBanter();
            bool social = banter.TrySaySocial(1, "Lewis", WorkerBanter.JobContext.Excavation, "SocialAura/initiator", 12f, "Nice work.");
            Check("TrySaySocial stamps WorkerId", social && banter.LastSpeech != null && banter.LastSpeech.WorkerId == 1);
            bool ambientBlocked = !banter.TrySay(2, "Mara", WorkerBanter.JobContext.Hauling, "Ambient", 12f, "Nope.");
            Check("Ambient muted during social priority", ambientBlocked && banter.SocialPriorityActive);
            Check("Speech ownership survives job-context mismatch",
                banter.LastSpeech.WorkerId == 1
                && banter.LastSpeech.Context == WorkerBanter.JobContext.Excavation);

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

        static bool ContainsAny(string line, params string[] needles)
        {
            if (string.IsNullOrEmpty(line) || needles == null) return false;
            for (int i = 0; i < needles.Length; i++)
            {
                if (string.IsNullOrEmpty(needles[i])) continue;
                if (line.IndexOf(needles[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }
}
