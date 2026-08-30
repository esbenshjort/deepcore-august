using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Observe hidden geo signals through investigation work units.
    /// Competence = deterministic bias / uncertainty / wording strength — not trait-miss RNG.
    /// All error tunables live here.
    /// </summary>
    public static class ProspectorGeoEvidence
    {
        // ——— Tunables: observation error ———
        public const float BaseErrorWide = 0.38f;
        public const float BaseErrorNarrow = 0.08f;
        public const float GeometryQualityWeight = 0.35f;
        public const float BiasTowardMid = 0.12f; // poor workers pull readings toward muddy mid

        /// <summary>
        /// Apply desk/scan interpretation — Return, Attenuation, Coherence, Boundary.
        /// </summary>
        public static string[] RevealDeskInterpretation(ProspectorAnomaly a, in ProspectorAnalysisStats stats)
        {
            if (a == null) return System.Array.Empty<string>();
            float skill = DeskSkill01(stats);
            float geom = Mathf.Clamp01(a.GeometryQuality);
            int salt = a.ScanId * 13 + a.AnomalyId;
            ObserveTrait(a, ref a.ObservedSignals.Values.ReturnStrength, a.HiddenSignals.ReturnStrength,
                skill, geom, salt, 1, out _, a.ObservedSignals.HasReturn);
            ObserveTrait(a, ref a.ObservedSignals.Values.Attenuation, a.HiddenSignals.Attenuation,
                skill, geom, salt, 2, out _, a.ObservedSignals.HasAttenuation);
            ObserveTrait(a, ref a.ObservedSignals.Values.StructuralCoherence, a.HiddenSignals.StructuralCoherence,
                skill, geom, salt, 3, out _, a.ObservedSignals.HasCoherence);
            ObserveBoundary(a, skill, geom, salt, out _);

            a.ObservedSignals.HasReturn = true;
            a.ObservedSignals.HasAttenuation = true;
            a.ObservedSignals.HasCoherence = true;
            a.ObservedSignals.HasBoundary = true;
            a.ObservedSignals.ObservationQuality01 = Mathf.Max(a.ObservedSignals.ObservationQuality01, skill);
            a.ObservedSignals.Values.ClampAll();

            return BuildFindingLines(a, ret: true, att: true, cond: false, coh: true, bound: true,
                field: false, loose: false, refiner: false, cross: false);
        }

        /// <summary>Refiner — Conductivity + mineralisation relevance.</summary>
        public static string[] RevealRefinerOpinion(ProspectorAnomaly a, in ProspectorAnalysisStats stats)
        {
            if (a == null) return System.Array.Empty<string>();
            float skill = RefinerSkill01(stats);
            int salt = a.ScanId * 13 + a.AnomalyId;
            ObserveTrait(a, ref a.ObservedSignals.Values.Conductivity, a.HiddenSignals.Conductivity,
                skill, a.GeometryQuality, salt, 11, out bool condNew, a.ObservedSignals.HasConductivity);
            if (condNew || !a.ObservedSignals.HasConductivity) a.ObservedSignals.HasConductivity = true;
            // ensure flag set
            a.ObservedSignals.HasConductivity = true;

            float truthLean = MineralLeanFromHidden(a.HiddenSignals);
            float obsLean = ObserveScalar(truthLean, skill, salt, 12);
            // Poor composure → slightly more extreme / poorly calibrated lean
            float composure01 = Stat01(stats.Composure);
            if (composure01 < 0.4f)
                obsLean = Mathf.Clamp(obsLean + (obsLean >= 0f ? 0.12f : -0.12f) * (1f - composure01), -1f, 1f);
            a.ObservedSignals.MineralRelevance01 = obsLean;
            a.ObservedSignals.HasMineralRelevance = true;
            a.ObservedSignals.ObservationQuality01 = Mathf.Max(a.ObservedSignals.ObservationQuality01, skill * 0.9f);
            a.ObservedSignals.Values.ClampAll();

            bool contradiction = DetectSubtleContradiction(a, skill);
            if (contradiction) a.ObservedSignals.HasContradictionFlag = true;

            return BuildFindingLines(a, false, false, condNew, false, false,
                field: false, loose: false, refiner: true, cross: false, contradiction);
        }

        /// <summary>Field — coherence, boundary, match vs desk.</summary>
        public static string[] RevealFieldInspection(ProspectorAnomaly a, in ProspectorAnalysisStats stats)
        {
            if (a == null) return System.Array.Empty<string>();
            float skill = FieldSkill01(stats);
            float geom = Mathf.Clamp01(a.GeometryQuality);
            int salt = a.ScanId * 13 + a.AnomalyId;

            ObserveTrait(a, ref a.ObservedSignals.Values.StructuralCoherence, a.HiddenSignals.StructuralCoherence,
                skill, geom, salt, 21, out _, a.ObservedSignals.HasCoherence);
            ObserveBoundary(a, skill, geom, salt, out _);
            a.ObservedSignals.HasCoherence = true;
            a.ObservedSignals.HasBoundary = true;

            float matchTruth = FieldMatchTruth(a);
            float matchObs = ObserveScalar(matchTruth, skill, salt, 22);
            a.ObservedSignals.FieldMatch01 = matchObs;
            a.ObservedSignals.HasFieldMatch = true;

            if (a.ObservedSignals.HasReturn && matchObs < -0.25f && skill > 0.35f)
                a.ObservedSignals.HasContradictionFlag = true;

            a.ObservedSignals.ObservationQuality01 = Mathf.Max(a.ObservedSignals.ObservationQuality01, skill);
            a.ObservedSignals.Values.ClampAll();

            return BuildFindingLines(a, false, false, false, true, true,
                field: true, loose: false, refiner: false, cross: false);
        }

        /// <summary>Loose rock — local support/contradict.</summary>
        public static string[] RevealLooseRockInspection(ProspectorAnomaly a, in ProspectorAnalysisStats stats)
        {
            if (a == null) return System.Array.Empty<string>();
            float skill = LooseRockSkill01(stats);
            int salt = a.ScanId * 13 + a.AnomalyId;
            float matchTruth = LooseRockMatchTruth(a);
            float matchObs = ObserveScalar(matchTruth, skill, salt, 31);
            a.ObservedSignals.LooseRockMatch01 = matchObs;
            a.ObservedSignals.HasLooseRockMatch = true;

            if (a.ObservedSignals.HasReturn)
                ObserveTrait(a, ref a.ObservedSignals.Values.ReturnStrength, a.HiddenSignals.ReturnStrength,
                    skill * 0.85f, a.GeometryQuality, salt, 32, out _, alreadyKnown: true);
            if (a.ObservedSignals.HasConductivity)
                ObserveTrait(a, ref a.ObservedSignals.Values.Conductivity, a.HiddenSignals.Conductivity,
                    skill * 0.85f, a.GeometryQuality, salt, 33, out _, alreadyKnown: true);

            if (matchObs < -0.3f && skill > 0.4f)
                a.ObservedSignals.HasContradictionFlag = true;

            a.ObservedSignals.ObservationQuality01 = Mathf.Max(a.ObservedSignals.ObservationQuality01, skill * 0.85f);
            a.ObservedSignals.Values.ClampAll();

            return BuildFindingLines(a, false, false, false, false, false,
                field: false, loose: true, refiner: false, cross: false);
        }

        /// <summary>Cross-check — shrink error, resolve/confirm contradictions.</summary>
        public static string[] RevealCrossCheck(ProspectorAnomaly a, in ProspectorAnalysisStats stats)
        {
            if (a == null) return System.Array.Empty<string>();
            float skill = CrossCheckSkill01(stats);

            // Pull observed toward hidden for known traits (better math/composure → stronger pull)
            float pull = Mathf.Lerp(0.15f, 0.55f, skill);
            RefineTowardHidden(a, pull);

            bool subtle = DetectSubtleContradiction(a, skill);
            if (subtle)
                a.ObservedSignals.HasContradictionFlag = true;
            else if (skill > 0.55f && a.ObservedSignals.HasContradictionFlag)
            {
                // Strong workers can resolve soft contradictions after cross-check
                if (AgreementScore(a) > 0.55f)
                    a.ObservedSignals.HasContradictionFlag = false;
            }

            a.ObservedSignals.ObservationQuality01 = Mathf.Clamp01(
                a.ObservedSignals.ObservationQuality01 * 0.7f + skill * 0.45f);
            a.ObservedSignals.Values.ClampAll();

            return BuildFindingLines(a, false, false, false, false, false,
                field: false, loose: false, refiner: false, cross: true, subtle);
        }

        // ——— Skills ———

        public static float DeskSkill01(in ProspectorAnalysisStats s) =>
            Mathf.Clamp01(Stat01(s.Focus) * 0.1f + Stat01(s.Calibration) * 0.3f
                + Stat01(s.Acoustics) * 0.2f + Stat01(s.SpatialGeometry) * 0.2f
                + Stat01(s.Mathematics) * 0.2f);

        public static float RefinerSkill01(in ProspectorAnalysisStats s) =>
            Mathf.Clamp01(Stat01(s.Chemistry) * 0.4f + Stat01(s.Mineralogy) * 0.35f
                + Stat01(s.Intuition) * 0.15f + Stat01(s.Composure) * 0.1f);

        public static float FieldSkill01(in ProspectorAnalysisStats s) =>
            Mathf.Clamp01(Stat01(s.Lithology) * 0.4f + Stat01(s.SpatialGeometry) * 0.25f
                + Stat01(s.Focus) * 0.2f + Stat01(s.Intuition) * 0.15f);

        public static float LooseRockSkill01(in ProspectorAnalysisStats s) =>
            Mathf.Clamp01(Stat01(s.Mineralogy) * 0.4f + Stat01(s.Lithology) * 0.4f
                + Stat01(s.Intuition) * 0.2f);

        public static float CrossCheckSkill01(in ProspectorAnalysisStats s) =>
            Mathf.Clamp01(Stat01(s.Mathematics) * 0.35f + Stat01(s.Composure) * 0.25f
                + Stat01(s.Intuition) * 0.25f + Stat01(s.Calibration) * 0.15f);

        static float Stat01(int v) => (WorkerStats.Clamp(v) - 1) / 18f;

        // ——— Observation core ———

        static void ObserveTrait(
            ProspectorAnomaly a,
            ref float observedSlot,
            float hidden,
            float skill01,
            float geom01,
            int saltBase,
            int traitSalt,
            out bool newlySet,
            bool alreadyKnown = false)
        {
            newlySet = !alreadyKnown;
            ObserveTraitRefine(a, ref observedSlot, hidden, skill01, geom01,
                saltBase + traitSalt, alreadyKnown);
        }

        static void ObserveTraitRefine(
            ProspectorAnomaly a,
            ref float slot,
            float hidden,
            float skill01,
            float geom01,
            int salt,
            bool alreadyKnown)
        {
            float err = ErrorMagnitude(skill01, geom01);
            float u = ProspectorDetection.StableNoise01(a.ScanId, a.AnomalyId, salt);
            float midBias = (0.5f - hidden) * BiasTowardMid * (1f - skill01);
            float noise = (u * 2f - 1f) * err;
            float raw = hidden + noise + midBias;
            float maxDev = Mathf.Lerp(0.42f, 0.1f, skill01);
            raw = Mathf.Clamp(raw, hidden - maxDev, hidden + maxDev);
            raw = Mathf.Clamp01(raw);
            if (alreadyKnown)
                slot = Mathf.Lerp(slot, raw, Mathf.Lerp(0.35f, 0.7f, skill01));
            else
                slot = raw;
        }

        static void ObserveBoundary(
            ProspectorAnomaly a,
            float skill01,
            float geom01,
            int saltBase,
            out bool newlySet)
        {
            newlySet = !a.ObservedSignals.HasBoundary;
            var hidden = a.HiddenSignals;
            ObserveTraitRefine(a, ref a.ObservedSignals.Values.BoundarySharpness01,
                hidden.BoundarySharpness01, skill01, geom01, saltBase + 40,
                a.ObservedSignals.HasBoundary);

            var truth = hidden.BoundaryCharacter;
            if (skill01 >= 0.55f)
                a.ObservedSignals.Values.BoundaryCharacter = truth;
            else
            {
                float u = ProspectorDetection.StableNoise01(a.ScanId, a.AnomalyId, 44);
                float confuse = (1f - skill01) * 0.85f;
                if (u < confuse)
                    a.ObservedSignals.Values.BoundaryCharacter = NeighborClass(truth, u);
                else
                    a.ObservedSignals.Values.BoundaryCharacter = truth;
            }
        }

        static AnomalyBoundaryCharacter NeighborClass(AnomalyBoundaryCharacter c, float u)
        {
            // Adjacent confusion — not arbitrary
            return c switch
            {
                AnomalyBoundaryCharacter.Diffuse =>
                    u > 0.5f ? AnomalyBoundaryCharacter.FractureBound : AnomalyBoundaryCharacter.Irregular,
                AnomalyBoundaryCharacter.FractureBound =>
                    u > 0.5f ? AnomalyBoundaryCharacter.Diffuse : AnomalyBoundaryCharacter.Irregular,
                AnomalyBoundaryCharacter.Irregular =>
                    u > 0.5f ? AnomalyBoundaryCharacter.Layered : AnomalyBoundaryCharacter.FractureBound,
                AnomalyBoundaryCharacter.Layered =>
                    u > 0.5f ? AnomalyBoundaryCharacter.Sharp : AnomalyBoundaryCharacter.Irregular,
                AnomalyBoundaryCharacter.Sharp =>
                    u > 0.5f ? AnomalyBoundaryCharacter.Layered : AnomalyBoundaryCharacter.Irregular,
                _ => AnomalyBoundaryCharacter.Irregular,
            };
        }

        static float ObserveScalar(float truthNeg1To1, float skill01, int saltBase, int salt)
        {
            float err = Mathf.Lerp(0.55f, 0.12f, skill01);
            float u = ProspectorDetection.StableNoise01(saltBase, salt, salt * 3);
            float noise = (u * 2f - 1f) * err;
            return Mathf.Clamp(truthNeg1To1 + noise, -1f, 1f);
        }

        static float ErrorMagnitude(float skill01, float geom01)
        {
            float g = Mathf.Lerp(1f, 1f - GeometryQualityWeight, Mathf.Clamp01(geom01));
            return Mathf.Lerp(BaseErrorWide, BaseErrorNarrow, skill01) * g;
        }

        static void RefineTowardHidden(ProspectorAnomaly a, float pull)
        {
            var h = a.HiddenSignals;
            ref var o = ref a.ObservedSignals.Values;
            if (a.ObservedSignals.HasReturn)
                o.ReturnStrength = Mathf.Lerp(o.ReturnStrength, h.ReturnStrength, pull);
            if (a.ObservedSignals.HasAttenuation)
                o.Attenuation = Mathf.Lerp(o.Attenuation, h.Attenuation, pull);
            if (a.ObservedSignals.HasConductivity)
                o.Conductivity = Mathf.Lerp(o.Conductivity, h.Conductivity, pull);
            if (a.ObservedSignals.HasCoherence)
                o.StructuralCoherence = Mathf.Lerp(o.StructuralCoherence, h.StructuralCoherence, pull);
            if (a.ObservedSignals.HasBoundary)
            {
                o.BoundarySharpness01 = Mathf.Lerp(o.BoundarySharpness01, h.BoundarySharpness01, pull);
                if (pull > 0.4f)
                    o.BoundaryCharacter = h.BoundaryCharacter;
            }
        }

        static float MineralLeanFromHidden(in AnomalyGeoSignals h)
        {
            // + mineral, − hazard/gas-like
            float mineral = h.Conductivity * 0.55f + h.ReturnStrength * 0.25f - h.Attenuation * 0.2f;
            float hazard = h.Attenuation * 0.45f + (1f - h.StructuralCoherence) * 0.4f - h.Conductivity * 0.15f;
            return Mathf.Clamp(mineral - hazard, -1f, 1f);
        }

        static float FieldMatchTruth(ProspectorAnomaly a)
        {
            // Does exposed geology match desk reading of coherence/return?
            if (!a.ObservedSignals.HasReturn && !a.ObservedSignals.HasCoherence)
                return 0.2f;
            float dRet = a.ObservedSignals.HasReturn
                ? 1f - Mathf.Abs(a.ObservedSignals.Values.ReturnStrength - a.HiddenSignals.ReturnStrength)
                : 0.5f;
            float dCoh = a.ObservedSignals.HasCoherence
                ? 1f - Mathf.Abs(a.ObservedSignals.Values.StructuralCoherence - a.HiddenSignals.StructuralCoherence)
                : 0.5f;
            return Mathf.Clamp((dRet + dCoh) * 1.2f - 0.6f, -1f, 1f);
        }

        static float LooseRockMatchTruth(ProspectorAnomaly a)
        {
            // Ore zone / mineralisation leaves conductive rock; gas/bedrock less so
            float lean = MineralLeanFromHidden(a.HiddenSignals);
            return Mathf.Clamp(lean * 0.85f, -1f, 1f);
        }

        /// <summary>
        /// Subtle contradiction: observed traits disagree with each other.
        /// Low skill → often miss it (return false even when present).
        /// </summary>
        static bool DetectSubtleContradiction(ProspectorAnomaly a, float skill01)
        {
            float tension = TraitTension(a);
            float threshold = Mathf.Lerp(0.72f, 0.38f, skill01); // poor workers need louder conflict
            if (tension < threshold) return false;
            // Competence gate: below skill floor, fail to notice
            float notice = Mathf.Lerp(0.25f, 0.9f, skill01);
            float u = ProspectorDetection.StableNoise01(a.ScanId, a.AnomalyId, 88);
            // Deterministic: notice if u < notice (higher skill → almost always notice when tension high)
            return u < notice;
        }

        public static float TraitTension(ProspectorAnomaly a)
        {
            var o = a.ObservedSignals;
            float t = 0f;
            if (o.HasReturn && o.HasAttenuation)
            {
                // High return + high attenuation is odd
                t = Mathf.Max(t, o.Values.ReturnStrength * o.Values.Attenuation);
            }
            if (o.HasCoherence && o.HasBoundary)
            {
                bool diffuse = o.Values.BoundaryCharacter == AnomalyBoundaryCharacter.Diffuse;
                if (diffuse && o.Values.StructuralCoherence > 0.7f)
                    t = Mathf.Max(t, 0.55f);
                if (!diffuse && o.Values.BoundarySharpness01 > 0.7f && o.Values.StructuralCoherence < 0.3f)
                    t = Mathf.Max(t, 0.5f);
            }
            if (o.HasConductivity && o.HasAttenuation)
            {
                // High conductivity + very high attenuation
                t = Mathf.Max(t, o.Values.Conductivity * o.Values.Attenuation * 0.9f);
            }
            if (o.HasFieldMatch && o.FieldMatch01 < -0.35f) t = Mathf.Max(t, 0.6f);
            if (o.HasLooseRockMatch && o.LooseRockMatch01 < -0.35f) t = Mathf.Max(t, 0.55f);
            if (o.HasMineralRelevance && o.HasAttenuation
                && o.MineralRelevance01 > 0.4f && o.Values.Attenuation > 0.7f)
                t = Mathf.Max(t, 0.5f);
            return t;
        }

        public static float AgreementScore(ProspectorAnomaly a)
        {
            float tension = TraitTension(a);
            float coverage = a.ObservedSignals.TraitsKnownCount / 5f;
            float q = a.ObservedSignals.ObservationQuality01;
            return Mathf.Clamp01((1f - tension) * 0.55f + coverage * 0.25f + q * 0.2f);
        }

        // ——— Findings ———

        static string[] BuildFindingLines(
            ProspectorAnomaly a,
            bool ret, bool att, bool cond, bool coh, bool bound,
            bool field, bool loose, bool refiner, bool cross,
            bool contradictionNote = false)
        {
            var tmp = new System.Collections.Generic.List<string>(6);
            float skill = a.ObservedSignals.ObservationQuality01;
            bool weakWording = skill < 0.4f;
            var v = a.ObservedSignals.Values;

            if (ret && a.ObservedSignals.HasReturn)
                tmp.Add(WordingReturn(v.ReturnStrength, weakWording));
            if (att && a.ObservedSignals.HasAttenuation)
                tmp.Add(WordingAttenuation(v.Attenuation, weakWording));
            if (coh && a.ObservedSignals.HasCoherence)
                tmp.Add(WordingCoherence(v.StructuralCoherence, weakWording));
            if (bound && a.ObservedSignals.HasBoundary)
                tmp.Add(WordingBoundary(v.BoundaryCharacter, v.BoundarySharpness01, weakWording));
            if (cond && a.ObservedSignals.HasConductivity)
                tmp.Add(WordingConductivity(v.Conductivity, weakWording));

            if (refiner && a.ObservedSignals.HasMineralRelevance)
                tmp.Add(WordingMineralRelevance(a.ObservedSignals.MineralRelevance01, weakWording));
            if (field && a.ObservedSignals.HasFieldMatch)
                tmp.Add(WordingFieldMatch(a.ObservedSignals.FieldMatch01, weakWording));
            if (loose && a.ObservedSignals.HasLooseRockMatch)
                tmp.Add(WordingLooseMatch(a.ObservedSignals.LooseRockMatch01, weakWording));
            if (cross)
                tmp.Add(a.ObservedSignals.HasContradictionFlag
                    ? (weakWording
                        ? "Cross-check: readings still look inconsistent."
                        : "Cross-check: conflicting evidence remains after reconsideration.")
                    : (weakWording
                        ? "Cross-check: evidence looks roughly consistent."
                        : "Cross-check: collected evidence aligns under reconsideration."));
            if (contradictionNote && a.ObservedSignals.HasContradictionFlag && !cross)
                tmp.Add(weakWording
                    ? "Some readings may not fit together."
                    : "Field evidence conflicts with the original interpretation.");

            return tmp.ToArray();
        }

        static string WordingReturn(float r, bool weak)
        {
            if (r > 0.72f) return weak ? "Return seems fairly strong." : "Strong compact return.";
            if (r > 0.45f) return weak ? "Return is moderate." : "Moderate return strength.";
            return weak ? "Return looks weak or soft." : "Weak or soft return.";
        }

        static string WordingAttenuation(float a, bool weak)
        {
            if (a > 0.7f)
                return weak
                    ? "Signal seems to fade quickly behind the front."
                    : "Signal weakens sharply behind the upper boundary.";
            if (a > 0.4f) return weak ? "Some fade with depth." : "Moderate attenuation with depth.";
            return weak ? "Signal holds up reasonably." : "Low attenuation — signal persists behind the front.";
        }

        static string WordingCoherence(float c, bool weak)
        {
            if (c > 0.7f)
                return weak ? "Structure looks fairly ordered." : "Structure appears internally coherent.";
            if (c > 0.4f) return weak ? "Structure is mixed." : "Structure is only partially coherent.";
            return weak ? "Structure looks messy." : "Low structural coherence — fractured or diffuse interior.";
        }

        static string WordingBoundary(AnomalyBoundaryCharacter c, float sharp, bool weak)
        {
            string edge = sharp > 0.65f ? "sharp" : sharp < 0.35f ? "soft" : "moderate";
            string cls = c switch
            {
                AnomalyBoundaryCharacter.Diffuse => "diffuse",
                AnomalyBoundaryCharacter.FractureBound => "fracture-bound",
                AnomalyBoundaryCharacter.Irregular => "irregular",
                AnomalyBoundaryCharacter.Layered => "layered",
                AnomalyBoundaryCharacter.Sharp => "abrupt",
                _ => "unclear",
            };
            if (weak) return $"Boundary looks {cls}, edge {edge}.";
            return $"Boundary character {cls} with {edge} leading edge.";
        }

        static string WordingConductivity(float c, bool weak)
        {
            if (c > 0.7f)
                return weak
                    ? "Conductive response seems high."
                    : "Conductive response is elevated but may be inconsistent.";
            if (c > 0.4f) return weak ? "Conductivity is middling." : "Moderate conductive response.";
            return weak ? "Conductivity looks low." : "Low conductive response.";
        }

        static string WordingMineralRelevance(float lean, bool weak)
        {
            if (lean > 0.35f)
                return weak
                    ? "Refiner: might relate to mineralisation."
                    : "Refiner: conductive pattern is consistent with mineralised rock.";
            if (lean < -0.35f)
                return weak
                    ? "Refiner: not a clean mineral read."
                    : "Refiner: response leans away from clean mineralisation — possible hazard context.";
            return weak
                ? "Refiner: composition still fuzzy."
                : "Refiner: composition remains ambiguous.";
        }

        static string WordingFieldMatch(float m, bool weak)
        {
            if (m > 0.3f)
                return weak
                    ? "Field look roughly matches the scan."
                    : "Field evidence supports the scan interpretation.";
            if (m < -0.3f)
                return weak
                    ? "Field look doesn't match the scan well."
                    : "Field evidence conflicts with the original interpretation.";
            return weak
                ? "Field look is inconclusive."
                : "Field geology neither confirms nor rules out the scan reading.";
        }

        static string WordingLooseMatch(float m, bool weak)
        {
            if (m > 0.3f)
                return weak
                    ? "Loose rock seems related."
                    : "Loose material from the face matches the anomaly response.";
            if (m < -0.3f)
                return weak
                    ? "Loose rock seems off."
                    : "Loose material contradicts the current anomaly reading.";
            return weak
                ? "Loose rock is hard to read."
                : "Loose material is inconclusive relative to the anomaly.";
        }

        // ——— Assessment ———

        /// <summary>
        /// Skill threshold above which Prospectors notice contradictions and refuse to
        /// publish Mixed until remaining useful evidence is exhausted (ACE territory).
        /// Weak Prospectors stay below this and may conclude early.
        /// </summary>
        public const float SkilledInterpretationThreshold = 0.55f;

        public static float PublishSkill01(ProspectorAnomaly a, in ProspectorAnalysisStats stats)
        {
            if (a == null) return 0f;
            return Mathf.Max(a.ObservedSignals.ObservationQuality01, CrossCheckSkill01(stats));
        }

        /// <summary>Preview publish label from current observed evidence (no mutation).</summary>
        public static string PreviewQualitativeLabel(ProspectorAnomaly a, float skill) =>
            a == null ? "Unclear" : ClassifyObserved(a, skill);

        /// <summary>
        /// True when the current evidence set would publish as Mixed / under revision —
        /// i.e. a contradiction the Prospector can still see.
        /// </summary>
        public static bool WouldPublishUnresolvedMixed(ProspectorAnomaly a, float skill)
        {
            if (a == null) return false;
            string label = ClassifyObserved(a, skill);
            return label == "Mixed evidence" || label == "Assessment under revision";
        }

        public static AnomalyAssessment BuildAssessment(ProspectorAnomaly a, in ProspectorAnalysisStats stats)
        {
            var assessment = new AnomalyAssessment();
            if (a == null)
            {
                assessment.QualitativeLabel = "Unclear";
                return assessment;
            }

            var o = a.ObservedSignals;
            float skill = PublishSkill01(a, stats);
            string label = ClassifyObserved(a, skill);
            assessment.QualitativeLabel = label;
            assessment.Title = TitleFromLabel(label);
            assessment.Confidence = ConfidenceFromEvidence(a, stats);
            assessment.AnalysisProgress01 = 1f;

            if (o.HasReturn)
                assessment.EvidenceNotes.Add(WordingReturn(o.Values.ReturnStrength, skill < 0.4f));
            if (o.HasConductivity)
                assessment.EvidenceNotes.Add(WordingConductivity(o.Values.Conductivity, skill < 0.4f));
            if (o.HasContradictionFlag)
                assessment.EvidenceNotes.Add("Evidence set contains unresolved tension.");

            // Internal belief % — debug only, from observed lean (not truth)
            float mineral = ClusterMineral(a);
            float hard = ClusterHard(a);
            float hazard = ClusterHazard(a);
            float sum = mineral + hard + hazard + 0.001f;
            assessment.BeliefGoldPct = Mathf.RoundToInt(100f * mineral / sum);
            assessment.BeliefBedrockPct = Mathf.RoundToInt(100f * hard / sum);
            assessment.BeliefGasPct = Mathf.Clamp(100 - assessment.BeliefGoldPct - assessment.BeliefBedrockPct, 0, 100);
            return assessment;
        }

        static string ClassifyObserved(ProspectorAnomaly a, float skill)
        {
            var o = a.ObservedSignals;
            if (o.TraitsKnownCount < 2)
                return "Unclear";

            float hard = ClusterHard(a);
            float mineral = ClusterMineral(a);
            float hazard = ClusterHazard(a);
            float best = Mathf.Max(hard, Mathf.Max(mineral, hazard));
            float second = hard + mineral + hazard - best;
            float margin = best - second * 0.5f;
            bool decisiveLean = best >= 0.45f && margin >= 0.12f;

            // Weak Prospectors: contradiction → muddy "under revision" (or they never noticed)
            if (o.HasContradictionFlag && skill < 0.5f)
                return "Assessment under revision";

            // Skilled: contradiction only stays Mixed when lean is still genuinely split.
            // A clear cluster lean after investigation is allowed to resolve past Mixed.
            if (o.HasContradictionFlag && !decisiveLean)
                return "Mixed evidence";

            if (best < 0.38f || margin < 0.08f)
                return "Unclear";

            if (Mathf.Approximately(best, hard))
                return "Hard geological formation likely";
            if (Mathf.Approximately(best, mineral))
                return margin > 0.18f && mineral > 0.55f
                    ? "Promising mineralised target"
                    : "Mineralised formation possible";
            if (Mathf.Approximately(best, hazard))
                return "Possible hazardous pocket";
            return "Mixed evidence";
        }

        static float ClusterHard(ProspectorAnomaly a)
        {
            var o = a.ObservedSignals;
            float s = 0.2f;
            if (o.HasReturn) s += o.Values.ReturnStrength * 0.35f;
            if (o.HasCoherence) s += o.Values.StructuralCoherence * 0.35f;
            if (o.HasAttenuation) s += (1f - o.Values.Attenuation) * 0.2f;
            if (o.HasBoundary && (o.Values.BoundaryCharacter == AnomalyBoundaryCharacter.Sharp
                                  || o.Values.BoundaryCharacter == AnomalyBoundaryCharacter.Layered))
                s += 0.15f * o.Values.BoundarySharpness01;
            return Mathf.Clamp01(s);
        }

        static float ClusterMineral(ProspectorAnomaly a)
        {
            var o = a.ObservedSignals;
            float s = 0.15f;
            if (o.HasConductivity) s += o.Values.Conductivity * 0.4f;
            if (o.HasReturn) s += o.Values.ReturnStrength * 0.2f;
            if (o.HasCoherence) s += o.Values.StructuralCoherence * 0.15f;
            if (o.HasMineralRelevance) s += (o.MineralRelevance01 + 1f) * 0.15f;
            if (o.HasBoundary && o.Values.BoundaryCharacter == AnomalyBoundaryCharacter.Irregular)
                s += 0.1f;
            return Mathf.Clamp01(s);
        }

        static float ClusterHazard(ProspectorAnomaly a)
        {
            var o = a.ObservedSignals;
            float s = 0.15f;
            if (o.HasAttenuation) s += o.Values.Attenuation * 0.4f;
            if (o.HasCoherence) s += (1f - o.Values.StructuralCoherence) * 0.3f;
            if (o.HasBoundary && (o.Values.BoundaryCharacter == AnomalyBoundaryCharacter.Diffuse
                                  || o.Values.BoundaryCharacter == AnomalyBoundaryCharacter.FractureBound))
                s += 0.2f;
            if (o.HasMineralRelevance) s += (1f - (o.MineralRelevance01 + 1f) * 0.5f) * 0.15f;
            return Mathf.Clamp01(s);
        }

        static string TitleFromLabel(string label)
        {
            if (label.Contains("Hard")) return "HARD GEOLOGICAL MASS";
            if (label.Contains("Promising") || label.Contains("Mineral")) return "MINERAL-LIKE FORMATION";
            if (label.Contains("hazard")) return "DIFFUSE ANOMALY";
            if (label.Contains("Mixed") || label.Contains("revision")) return "INDETERMINATE RESPONSE";
            return "INDETERMINATE RESPONSE";
        }

        static AssessmentConfidence ConfidenceFromEvidence(ProspectorAnomaly a, in ProspectorAnalysisStats stats)
        {
            float agree = AgreementScore(a);
            float cover = a.ObservedSignals.TraitsKnownCount / 5f;
            float calib = Stat01(stats.Calibration);
            float composure = Stat01(stats.Composure);
            // Poor calibration → over/under confidence (biased)
            float raw = agree * 0.45f + cover * 0.3f + calib * 0.15f + composure * 0.1f;
            if (calib < 0.35f)
                raw = Mathf.Lerp(raw, raw > 0.5f ? 0.85f : 0.2f, 0.35f); // miscalibration
            if (a.ObservedSignals.HasContradictionFlag) raw *= 0.7f;
            return raw switch
            {
                < 0.22f => AssessmentConfidence.VeryLow,
                < 0.38f => AssessmentConfidence.Low,
                < 0.55f => AssessmentConfidence.Medium,
                < 0.72f => AssessmentConfidence.High,
                _ => AssessmentConfidence.VeryHigh,
            };
        }

        public static void AppendFindings(
            ProspectorAnomaly a,
            string[] lines,
            ProspectorFindingsLog log = null,
            string sourceLabel = null)
        {
            if (a == null || lines == null) return;
            for (int i = 0; i < lines.Length; i++)
                a.AddFindingEntry(lines[i]);
            log?.PushEvidenceNotes(a, lines, sourceLabel);
        }
    }
}
