using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Offline audit: Social Aura dialogue depth / variety (presentation only).
    /// Does not touch encounter resolution or social math.
    /// </summary>
    public static class SocialDialogueDepthAudit
    {
        static readonly Regex AxisDumpDigits = new(@"\b\d{1,3}([.,]\d+)?\b", RegexOptions.Compiled);

        public static void RunFromEditor()
        {
            string path = Run();
            Debug.Log($"[SOCIAL DIALOGUE DEPTH] Report: {path}");
#if UNITY_EDITOR
            bool fail = File.ReadAllText(path).Contains("INVARIANT: FAIL");
            UnityEditor.EditorApplication.Exit(fail ? 1 : 0);
#endif
        }

        public static string Run(string outputDirectory = null)
        {
            var log = new StringBuilder(24_000);
            int pass = 0, fail = 0;

            void Check(string name, bool ok, string detail = "")
            {
                if (ok) { pass++; log.AppendLine($"PASS | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}")); }
                else { fail++; log.AppendLine($"FAIL | {name}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}")); }
            }

            log.AppendLine("# Social Dialogue Depth Audit");
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine();
            log.AppendLine("Scope: SocialAuraLineBank wording variety via SocialDialogueTone.");
            log.AppendLine("Does not alter Social Aura math / encounter resolution.");
            log.AppendLine();

            // Stable Stage2 strings still present
            Check("FirstInitiator Encourage ok stable",
                SocialAuraLineBank.FirstInitiator(SocialAction.Encourage, true, SocialContext.WorkingTogether)
                == "Nice work. Keep that pace.");
            Check("FirstResponse fail Deflect contains manage",
                SocialAuraLineBank.FirstResponse(SocialResponse.Deflect, SocialAction.Encourage, false, SocialContext.WorkingTogether)
                    .IndexOf("manage", StringComparison.OrdinalIgnoreCase) >= 0);
            Check("FirstResponse SharedProblem Agree contains mess|alone",
                SocialAuraLineBank.FirstResponse(SocialResponse.Agree, SocialAction.Complain, true, SocialContext.SharedProblem)
                    .IndexOf("mess", StringComparison.OrdinalIgnoreCase) >= 0
                || SocialAuraLineBank.FirstResponse(SocialResponse.Agree, SocialAction.Complain, true, SocialContext.SharedProblem)
                    .IndexOf("alone", StringComparison.OrdinalIgnoreCase) >= 0);

            var unique = new HashSet<string>(StringComparer.Ordinal);
            int samples = 0;
            int emptyFails = 0;
            int digitHits = 0;
            var digitExamples = new List<string>(8);

            var actions = (SocialAction[])Enum.GetValues(typeof(SocialAction));
            var responses = (SocialResponse[])Enum.GetValues(typeof(SocialResponse));
            var contexts = new[]
            {
                SocialContext.WorkingTogether,
                SocialContext.SharedProblem,
                SocialContext.Camp,
                SocialContext.IdleNearby,
                SocialContext.RecentSuccess,
                SocialContext.RecentFailure,
            };
            var classes = (RelationshipClass[])Enum.GetValues(typeof(RelationshipClass));
            var emotions = (SocialAuraClass[])Enum.GetValues(typeof(SocialAuraClass));

            // Friend vs grudge Encourage uniqueness sets
            var friendEncourage = new HashSet<string>(StringComparer.Ordinal);
            var grudgeEncourage = new HashSet<string>(StringComparer.Ordinal);

            foreach (var action in actions)
            foreach (bool ok in new[] { true, false })
            foreach (var ctx in contexts)
            foreach (var rel in classes)
            foreach (var emo in emotions)
            {
                var tone = MakeTone(rel, emo, frust: emo == SocialAuraClass.Negative ? 70f : 20f,
                    majorNeg: rel == RelationshipClass.Grudge || rel == RelationshipClass.Strained,
                    majorPos: rel == RelationshipClass.Friendly || rel == RelationshipClass.Bonded);

                for (int n = 0; n < 3; n++)
                {
                    string init = SocialAuraLineBank.PickInitiator(action, ok, ctx, in tone);
                    samples++;
                    if (string.IsNullOrEmpty(init)) emptyFails++;
                    else unique.Add(init);
                    if (LooksLikeAxisDump(init))
                    {
                        digitHits++;
                        if (digitExamples.Count < 6) digitExamples.Add($"INIT '{init}'");
                    }

                    if (action == SocialAction.Encourage && ok)
                    {
                        if (rel == RelationshipClass.Friendly || rel == RelationshipClass.Bonded)
                            friendEncourage.Add(init);
                        if (rel == RelationshipClass.Grudge)
                            grudgeEncourage.Add(init);
                    }
                }

                foreach (var resp in responses)
                {
                    string line = SocialAuraLineBank.PickResponse(resp, action, ok, ctx, in tone);
                    samples++;
                    // "…" is a valid Ignore beat — only null/empty is a failure
                    if (string.IsNullOrEmpty(line)) emptyFails++;
                    else unique.Add(line);
                    if (LooksLikeAxisDump(line))
                    {
                        digitHits++;
                        if (digitExamples.Count < 6) digitExamples.Add($"RESP '{line}'");
                    }
                }
            }

            // Closers
            var closerLogs = new[]
            {
                MakeLog("SHARED_COMPLAINT_BOND"),
                MakeLog("CLASH"),
                MakeLog("POSITIVE_FAIL"),
                MakeLog("IGNORED_AGGRESSION"),
            };
            foreach (var rel in classes)
            {
                var tone = MakeTone(rel, SocialAuraClass.Neutral, 40f, false, false);
                foreach (var cl in closerLogs)
                {
                    string c = SocialAuraLineBank.PickOptionalCloser(cl, in tone);
                    if (string.IsNullOrEmpty(c)) continue;
                    samples++;
                    unique.Add(c);
                    if (LooksLikeAxisDump(c))
                    {
                        digitHits++;
                        if (digitExamples.Count < 6) digitExamples.Add($"CLOSE '{c}'");
                    }
                }
            }

            Check("No empty picks", emptyFails == 0, $"empty={emptyFails}");
            Check("No axis-dump digit sequences in dialogue", digitHits == 0,
                digitHits == 0 ? "" : string.Join("; ", digitExamples));

            bool friendDiffers = false;
            foreach (var f in friendEncourage)
            {
                if (!grudgeEncourage.Contains(f)) { friendDiffers = true; break; }
            }
            // Also require some grudge-only wording
            bool grudgeDiffers = false;
            foreach (var g in grudgeEncourage)
            {
                if (!friendEncourage.Contains(g)) { grudgeDiffers = true; break; }
            }
            Check("Friendly/Bonded Encourage differs from Grudge (some picks)",
                friendDiffers && grudgeDiffers,
                $"friendUnique={friendEncourage.Count} grudgeUnique={grudgeEncourage.Count}");

            // Camp vs work Encourage should show some difference across samples
            var campSet = new HashSet<string>(StringComparer.Ordinal);
            var workSet = new HashSet<string>(StringComparer.Ordinal);
            var neutral = MakeTone(RelationshipClass.Neutral, SocialAuraClass.Neutral, 15f, false, false);
            for (int i = 0; i < 40; i++)
            {
                campSet.Add(SocialAuraLineBank.PickInitiator(SocialAction.Encourage, true, SocialContext.Camp, in neutral));
                workSet.Add(SocialAuraLineBank.PickInitiator(SocialAction.Encourage, true, SocialContext.WorkingTogether, in neutral));
            }
            bool campDiff = false;
            foreach (var c in campSet)
                if (!workSet.Contains(c)) { campDiff = true; break; }
            Check("Camp Encourage shows some lines absent from WorkingTogether",
                campDiff, $"camp={campSet.Count} work={workSet.Count}");

            // Stratified uniqueness: one draw per action×ok×ctx×RelClass (initiators only)
            var stratified = new HashSet<string>(StringComparer.Ordinal);
            int stratifiedN = 0;
            foreach (var action in actions)
            foreach (bool ok in new[] { true, false })
            foreach (var ctx in contexts)
            foreach (var rel in classes)
            {
                var tone = MakeTone(rel, SocialAuraClass.Neutral, 25f,
                    majorNeg: rel == RelationshipClass.Grudge,
                    majorPos: rel == RelationshipClass.Bonded);
                stratified.Add(SocialAuraLineBank.PickInitiator(action, ok, ctx, in tone));
                stratifiedN++;
            }
            float stratifiedRatio = stratifiedN > 0 ? (float)stratified.Count / stratifiedN : 0f;
            float uniqueness = samples > 0 ? (float)unique.Count / samples : 0f;

            Check("Unique line count is high (>= 180)",
                unique.Count >= 180, $"unique={unique.Count} samples={samples} ratio={uniqueness:0.000}");
            Check("Stratified initiator uniqueness (>= 0.22)",
                stratifiedRatio >= 0.22f,
                $"unique={stratified.Count}/{stratifiedN} ratio={stratifiedRatio:0.000}");

            // Tone.From smoke
            var speaker = new WorkerRuntime(1, "Lewis");
            speaker.State.Frustration = 62f;
            var relObj = new SocialDirectedRelation { Trust = 8f, Warmth = 9f, Hostility = 0f, Respect = 58f };
            var mems = new List<SocialMemoryEntry>
            {
                new SocialMemoryEntry
                {
                    ObserverId = 1, TargetId = 2, Type = SocialMemoryType.SupportedMe,
                    Strength = 0.9f, Significance = SocialMemorySignificance.Major, GameTime = 1f,
                },
            };
            var built = SocialDialogueTone.From(speaker, relObj, mems);
            Check("SocialDialogueTone.From sets RelClass",
                built.RelClass == RelationshipClass.Friendly || built.RelClass == RelationshipClass.Bonded
                || built.RelClass == RelationshipClass.Neutral,
                built.RelClass.ToString());
            Check("SocialDialogueTone.From sets frustration",
                Mathf.Abs(built.FrustrationSpeaker - 62f) < 0.01f);
            Check("SocialDialogueTone.From major pos memory", built.HasMajorPosMemory && built.HasMemoryHint);

            log.AppendLine();
            log.AppendLine("## Sample counts");
            log.AppendLine($"- Total picks sampled: {samples}");
            log.AppendLine($"- Unique lines: {unique.Count}");
            log.AppendLine($"- Uniqueness ratio (dense sample): {uniqueness:0.000}");
            log.AppendLine($"- Stratified initiator unique: {stratified.Count}/{stratifiedN} ({stratifiedRatio:0.000})");
            log.AppendLine($"- Friendly/Bonded Encourage uniques: {friendEncourage.Count}");
            log.AppendLine($"- Grudge Encourage uniques: {grudgeEncourage.Count}");
            log.AppendLine($"- Camp Encourage uniques (40 draws): {campSet.Count}");
            log.AppendLine($"- Work Encourage uniques (40 draws): {workSet.Count}");
            log.AppendLine();
            log.AppendLine("## Summary");
            log.AppendLine($"PASS {pass} / FAIL {fail}");
            log.AppendLine(fail == 0 ? "INVARIANT: PASS" : "INVARIANT: FAIL");

            string dir = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(Application.dataPath, "..", "BenchmarkResults")
                : outputDirectory;
            Directory.CreateDirectory(dir);
            string stamped = Path.Combine(dir, $"social_dialogue_depth_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            string latest = Path.Combine(dir, "social_dialogue_depth_latest.md");
            File.WriteAllText(stamped, log.ToString());
            File.WriteAllText(latest, log.ToString());
            return latest;
        }

        static SocialDialogueTone MakeTone(
            RelationshipClass rel,
            SocialAuraClass emo,
            float frust,
            bool majorNeg,
            bool majorPos)
        {
            return new SocialDialogueTone
            {
                RelClass = rel,
                Trust = rel == RelationshipClass.Grudge ? -5f : rel == RelationshipClass.Bonded ? 8f : 2f,
                Warmth = rel == RelationshipClass.Friendly || rel == RelationshipClass.Bonded ? 8f : 0f,
                Hostility = rel == RelationshipClass.Grudge || rel == RelationshipClass.Rivalry ? 10f : 1f,
                Respect = rel == RelationshipClass.Professional || rel == RelationshipClass.Rivalry ? 75f : 50f,
                FrustrationSpeaker = frust,
                EmotionalClass = emo,
                HasMajorNegMemory = majorNeg,
                HasMajorPosMemory = majorPos,
                MemoryHint = majorNeg ? SocialMemoryType.InsultedMe : SocialMemoryType.SupportedMe,
                HasMemoryHint = majorNeg || majorPos,
            };
        }

        static SocialEncounterLog MakeLog(string outcome) =>
            new SocialEncounterLog { OutcomeSummary = outcome };

        /// <summary>
        /// Flag lines that look like axis dumps (Trust 12, T=3.5, etc.) — allow sparse natural numbers.
        /// </summary>
        static bool LooksLikeAxisDump(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            string l = line.ToLowerInvariant();
            if (l.Contains("trust") && AxisDumpDigits.IsMatch(line)) return true;
            if (l.Contains("warmth") && AxisDumpDigits.IsMatch(line)) return true;
            if (l.Contains("hostility") && AxisDumpDigits.IsMatch(line)) return true;
            if (l.Contains("respect") && AxisDumpDigits.IsMatch(line)) return true;
            if (Regex.IsMatch(line, @"\b[TtWwHhRr]\s*=\s*-?\d")) return true;
            if (Regex.IsMatch(line, @"\b\d{1,2}\s*/\s*\d{2,3}\b")) return true; // meter dump
            return false;
        }
    }
}
