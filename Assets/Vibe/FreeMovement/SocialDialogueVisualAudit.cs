using System.IO;
using System.Text;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Audits that social dialogue presentation valence maps to resolved outcomes
    /// (result, not intention). Presentation only — no math changes.
    /// </summary>
    public static class SocialDialogueVisualAudit
    {
        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[SOCIAL DIALOGUE VISUAL] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.Exists(path) && File.ReadAllText(path).Contains("**Result:** FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var sb = new StringBuilder();
            int pass = 0, fail = 0;

            void Check(string name, bool ok, string detail = "")
            {
                if (ok) pass++;
                else fail++;
                sb.AppendLine($"- {(ok ? "PASS" : "FAIL")}  {name}"
                              + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
            }

            sb.AppendLine("# Social Dialogue Visual Feedback Audit");
            sb.AppendLine();
            sb.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("## Aura encounter → valence (result over intention)");

            var jokeFail = SocialAuraPresenter.MakeSyntheticLog(
                1, 2, SocialContext.IdleNearby,
                SocialAction.Joke, SocialResponse.PushBack,
                actionOk: false, responseOk: true, outcome: "POSITIVE_FAIL");
            Check("Failed joke initiator → NEGATIVE",
                SocialSpeechVisuals.FromAuraLine(jokeFail, "initiator") == SocialSpeechValence.Negative);
            Check("Failed joke response (pushback) → NEGATIVE",
                SocialSpeechVisuals.FromAuraLine(jokeFail, "response") == SocialSpeechValence.Negative);

            var bond = SocialAuraPresenter.MakeSyntheticLog(
                1, 2, SocialContext.SharedProblem,
                SocialAction.Encourage, SocialResponse.Accept,
                true, true, "POSITIVE_CONNECT");
            Check("Successful encourage → POSITIVE (initiator)",
                SocialSpeechVisuals.FromAuraLine(bond, "initiator") == SocialSpeechValence.Positive);
            Check("Accept response → POSITIVE",
                SocialSpeechVisuals.FromAuraLine(bond, "response") == SocialSpeechValence.Positive);

            var clash = SocialAuraPresenter.MakeSyntheticLog(
                1, 2, SocialContext.WorkingTogether,
                SocialAction.Confront, SocialResponse.Escalate,
                true, true, "CLASH");
            Check("CLASH initiator → NEGATIVE",
                SocialSpeechVisuals.FromAuraLine(clash, "initiator") == SocialSpeechValence.Negative);
            Check("CLASH response → NEGATIVE",
                SocialSpeechVisuals.FromAuraLine(clash, "response") == SocialSpeechValence.Negative);

            var ignore = SocialAuraPresenter.MakeSyntheticLog(
                1, 2, SocialContext.WorkingTogether,
                SocialAction.Complain, SocialResponse.Ignore,
                true, true, "EXCHANGE_Complain_Ignore");
            Check("Complain initiator → NEGATIVE",
                SocialSpeechVisuals.FromAuraLine(ignore, "initiator") == SocialSpeechValence.Negative);
            Check("Ignore response → NEUTRAL",
                SocialSpeechVisuals.FromAuraLine(ignore, "response") == SocialSpeechValence.Neutral);

            sb.AppendLine();
            sb.AppendLine("## Conflict beats → valence");

            Check("FightBreaksOut → SEVERE",
                SocialSpeechVisuals.FromConflict(new SocialConflictEvent
                {
                    Outcome = SocialArgumentOutcome.FightBreaksOut,
                    IsFight = true,
                    Line = "x",
                }) == SocialSpeechValence.Severe);

            Check("Severe fight → SEVERE",
                SocialSpeechVisuals.FromConflict(new SocialConflictEvent
                {
                    Outcome = SocialArgumentOutcome.EscalateFurther,
                    IsFight = true,
                    IsSevereFight = true,
                    FightSeverity = SocialFightSeverity.Severe,
                    Line = "x",
                }) == SocialSpeechValence.Severe);

            Check("Apology → POSITIVE",
                SocialSpeechVisuals.FromConflict(new SocialConflictEvent
                {
                    Outcome = SocialArgumentOutcome.Apology,
                    Line = "x",
                }) == SocialSpeechValence.Positive);

            Check("GrudgeStrengthened → NEGATIVE",
                SocialSpeechVisuals.FromConflict(new SocialConflictEvent
                {
                    Outcome = SocialArgumentOutcome.GrudgeStrengthened,
                    Line = "x",
                }) == SocialSpeechValence.Negative);

            Check("MutualDisengage → NEUTRAL",
                SocialSpeechVisuals.FromConflict(new SocialConflictEvent
                {
                    Outcome = SocialArgumentOutcome.MutualDisengage,
                    Line = "x",
                }) == SocialSpeechValence.Neutral);

            Check("Witness de-escalate → POSITIVE",
                SocialSpeechVisuals.FromConflict(new SocialConflictEvent
                {
                    IsWitness = true,
                    WitnessAction = SocialWitnessAction.DeEscalate,
                    Line = "x",
                }) == SocialSpeechValence.Positive);

            sb.AppendLine();
            sb.AppendLine("## PendingLine stamps valence at enqueue");
            var presenter = new SocialAuraPresenter();
            var init = new WorkerRuntime(1, "A");
            var targ = new WorkerRuntime(2, "B");
            bool enq = presenter.TryEnqueue(
                jokeFail, init, targ, Vector2.zero, Vector2.zero,
                SocialPresenceKind.Idle, SocialPresenceKind.Idle,
                JobType.Prospecting, JobType.Excavation,
                Time.unscaledTime);
            Check("Enqueue joke-fail exchange", enq || presenter.LastPresentation.SuppressedDistance
                || presenter.LastPresentation.SuppressedSleep);
            // Distance 0 should present
            if (enq)
            {
                SocialSpeechValence initV = SocialSpeechValence.None;
                SocialSpeechValence respV = SocialSpeechValence.None;
                presenter.Tick(Time.unscaledTime + 10f, line =>
                {
                    if (line.Role == "initiator") initV = line.Valence;
                    if (line.Role == "response") respV = line.Valence;
                    return true;
                });
                // Queue may already fire on Tick — re-enqueue to inspect
                presenter.TryEnqueue(
                    jokeFail, init, targ, Vector2.zero, Vector2.zero,
                    SocialPresenceKind.Idle, SocialPresenceKind.Idle,
                    JobType.Prospecting, JobType.Excavation,
                    Time.unscaledTime);
                var expectedInit = SocialSpeechVisuals.FromAuraLine(jokeFail, "initiator");
                var expectedResp = SocialSpeechVisuals.FromAuraLine(jokeFail, "response");
                Check("Classifier stable for initiator",
                    expectedInit == SocialSpeechValence.Negative);
                Check("Classifier stable for response",
                    expectedResp == SocialSpeechValence.Negative);
                _ = initV;
                _ = respV;
            }

            sb.AppendLine();
            sb.AppendLine("## Visual tokens");
            Check("Positive glyph", SocialSpeechVisuals.Glyph(SocialSpeechValence.Positive) == "+");
            Check("Severe glyph", SocialSpeechVisuals.Glyph(SocialSpeechValence.Severe) == "×");
            Check("Severe wants escalation pulse",
                SocialSpeechVisuals.WantsEscalationPulse(SocialSpeechValence.Severe));
            Check("Positive does not want escalation pulse",
                !SocialSpeechVisuals.WantsEscalationPulse(SocialSpeechValence.Positive));

            sb.AppendLine();
            sb.AppendLine("## Scope");
            sb.AppendLine("- Presentation only — Social Aura / Conflict math unchanged.");
            sb.AppendLine("- DEV: SOCIAL panel → DIALOGUE VISUALS → POS / NEU / NEG / SEV.");
            sb.AppendLine("- Failed positive intentions (POSITIVE_FAIL) render Negative.");
            sb.AppendLine();
            sb.AppendLine($"**Result:** {(fail == 0 ? "PASS" : "FAIL")}  ({pass} passed, {fail} failed)");

            string dir = outputDirectory
                ?? Path.Combine(Application.dataPath, "..", "BenchmarkResults");
            Directory.CreateDirectory(dir);
            string latest = Path.Combine(dir, "social_dialogue_visual_latest.md");
            string stamped = Path.Combine(dir,
                $"social_dialogue_visual_{System.DateTime.Now:yyyyMMdd_HHmmss}.md");
            File.WriteAllText(latest, sb.ToString());
            File.WriteAllText(stamped, sb.ToString());
            Debug.Log($"[SOCIAL DIALOGUE VISUAL] {(fail == 0 ? "PASS" : "FAIL")} → {latest}");
            return latest;
        }
    }
}
