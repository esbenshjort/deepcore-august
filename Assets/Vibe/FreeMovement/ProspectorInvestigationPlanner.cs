using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Evidence-gap investigation planner. Needs come from observed signal state —
    /// not analysis progress % and not mandatory physical pacing.
    /// </summary>
    public static class ProspectorInvestigationPlanner
    {
        // ——— Tunables: when gaps become needs ———
        public const float ConductivityAmbiguityBand = 0.22f;
        public const float CoherenceGapBelow = 0.48f;
        public const float FieldContradictionBelow = -0.25f;
        public const float ExcavatorLooseRockCells = 18f;
        public const int MinTraitsForFinding = 3;
        public const float MinAgreementForFinding = 0.48f;

        public static void ApplyAfterEvidence(
            ProspectorAnomaly a,
            AnomalyEvidenceKind recorded,
            float excavatorDistanceCells = -1f)
        {
            if (a == null) return;
            a.RecordEvidence(recorded);
            a.InvestigationPlanBuilt = true;
            Replan(a, excavatorDistanceCells);
        }

        public static void BeginDeskInterpretation(ProspectorAnomaly a)
        {
            if (a == null) return;
            a.RemainingNeeds.Clear();
            a.InvestigationPlanBuilt = false;
            a.SetNeed(
                AnomalyInvestigationNeed.NeedDeskInterpretation,
                ReasonForNeed(AnomalyInvestigationNeed.NeedDeskInterpretation, a));
        }

        public static void MarkComplete(ProspectorAnomaly a)
        {
            if (a == null) return;
            a.RemainingNeeds.Clear();
            a.SetNeed(AnomalyInvestigationNeed.Complete, "investigation closed");
        }

        /// <summary>Rebuild remaining needs from current observed evidence gaps.</summary>
        public static void Replan(ProspectorAnomaly a, float excavatorDistanceCells = -1f)
        {
            if (a == null) return;
            a.RemainingNeeds.Clear();

            if (!a.HasEvidence(AnomalyEvidenceKind.ScanInterpretation))
            {
                Push(a, AnomalyInvestigationNeed.NeedDeskInterpretation);
                AdvanceToHead(a);
                return;
            }

            var o = a.ObservedSignals;

            // Composition / conductivity — only when desk traits leave mineral vs hazard ambiguous
            bool compositionAmbiguous = CompositionAmbiguous(a);
            bool needRefiner =
                (!o.HasConductivity && compositionAmbiguous)
                || (o.HasMineralRelevance && Mathf.Abs(o.MineralRelevance01) < ConductivityAmbiguityBand)
                || (o.HasConductivity && o.HasAttenuation
                    && o.Values.Conductivity > 0.55f && o.Values.Attenuation > 0.65f
                    && !o.HasMineralRelevance);
            if (needRefiner && !a.HasEvidence(AnomalyEvidenceKind.RefinerOpinion))
                Push(a, AnomalyInvestigationNeed.NeedRefinerConsultation);

            // Structural / boundary gap or field mismatch → Field
            bool structureThin = !o.HasCoherence || !o.HasBoundary
                || (o.HasCoherence && o.Values.StructuralCoherence < CoherenceGapBelow);
            bool needField = (structureThin && o.ObservationQuality01 < 0.72f)
                || (o.HasFieldMatch && o.FieldMatch01 < FieldContradictionBelow
                    && !a.HasEvidence(AnomalyEvidenceKind.CrossCheck));
            if (needField && !a.HasEvidence(AnomalyEvidenceKind.FieldEvidence))
                Push(a, AnomalyInvestigationNeed.NeedFieldEvidence);

            // Excavation proximity → loose rock (evidence opportunity, not pacing)
            bool excavNear = excavatorDistanceCells >= 0f
                             && excavatorDistanceCells <= ExcavatorLooseRockCells;
            if (excavNear && !a.HasEvidence(AnomalyEvidenceKind.LooseRockEvidence))
                Push(a, AnomalyInvestigationNeed.NeedLooseRockInspection);

            // Contradictions / tension → Cross-check
            bool needCross = o.HasContradictionFlag
                || ProspectorGeoEvidence.TraitTension(a) > 0.45f
                || (o.HasFieldMatch && o.FieldMatch01 < FieldContradictionBelow)
                || (o.HasLooseRockMatch && o.LooseRockMatch01 < FieldContradictionBelow);
            if (needCross && !a.HasEvidence(AnomalyEvidenceKind.CrossCheck))
                Push(a, AnomalyInvestigationNeed.NeedCrossCheck);

            // Enough coherent evidence → finding (desk-only OK if clear).
            // Skilled Prospectors do not Ready while Mixed would still be premature.
            if (CanReadyForFinding(a, excavatorDistanceCells))
                Push(a, AnomalyInvestigationNeed.ReadyForFinding);
            else if (a.RemainingNeeds.Count == 0)
            {
                // Still thin / contradictory — chase unused evidence before closing
                if (TryPushUsefulFollowUp(a, excavatorDistanceCells))
                { /* follow-up queued */ }
                else
                    Push(a, AnomalyInvestigationNeed.ReadyForFinding);
            }

            AdvanceToHead(a);
        }

        /// <summary>
        /// If a skilled Prospector would still publish Mixed, push one more useful step.
        /// Returns true when a follow-up was queued. Weak Prospectors are not forced here.
        /// </summary>
        public static bool TryDeferMixedWithFollowUp(
            ProspectorAnomaly a,
            float skill01,
            float excavatorDistanceCells = -1f)
        {
            if (a == null) return false;
            if (skill01 < ProspectorGeoEvidence.SkilledInterpretationThreshold) return false;
            if (!ProspectorGeoEvidence.WouldPublishUnresolvedMixed(a, skill01)) return false;
            if (!TryPushUsefulFollowUp(a, excavatorDistanceCells)) return false;
            AdvanceToHead(a);
            return true;
        }

        /// <summary>
        /// Queue one unused evidence step that could resolve contradiction / ambiguity.
        /// Priority: CrossCheck → Field → Refiner → LooseRock (if excavator near).
        /// </summary>
        public static bool TryPushUsefulFollowUp(
            ProspectorAnomaly a, float excavatorDistanceCells = -1f)
        {
            if (a == null) return false;
            var o = a.ObservedSignals;

            if (!a.HasEvidence(AnomalyEvidenceKind.CrossCheck) && o.TraitsKnownCount >= 2)
            {
                Push(a, AnomalyInvestigationNeed.NeedCrossCheck);
                return true;
            }

            if (!a.HasEvidence(AnomalyEvidenceKind.FieldEvidence))
            {
                Push(a, AnomalyInvestigationNeed.NeedFieldEvidence);
                return true;
            }

            if (!a.HasEvidence(AnomalyEvidenceKind.RefinerOpinion))
            {
                Push(a, AnomalyInvestigationNeed.NeedRefinerConsultation);
                return true;
            }

            bool excavNear = excavatorDistanceCells >= 0f
                             && excavatorDistanceCells <= ExcavatorLooseRockCells;
            if (excavNear && !a.HasEvidence(AnomalyEvidenceKind.LooseRockEvidence))
            {
                Push(a, AnomalyInvestigationNeed.NeedLooseRockInspection);
                return true;
            }

            return false;
        }

        static bool CompositionAmbiguous(ProspectorAnomaly a)
        {
            // Approximate mineral vs hazard from desk traits without conductivity
            var o = a.ObservedSignals;
            float mineral = 0.2f;
            float hazard = 0.2f;
            if (o.HasReturn) mineral += o.Values.ReturnStrength * 0.25f;
            if (o.HasAttenuation)
            {
                hazard += o.Values.Attenuation * 0.4f;
                mineral += (1f - o.Values.Attenuation) * 0.1f;
            }
            if (o.HasCoherence)
            {
                mineral += o.Values.StructuralCoherence * 0.2f;
                hazard += (1f - o.Values.StructuralCoherence) * 0.25f;
            }
            if (o.HasBoundary)
            {
                if (o.Values.BoundaryCharacter == AnomalyBoundaryCharacter.Diffuse
                    || o.Values.BoundaryCharacter == AnomalyBoundaryCharacter.FractureBound)
                    hazard += 0.2f;
                if (o.Values.BoundaryCharacter == AnomalyBoundaryCharacter.Irregular
                    || o.Values.BoundaryCharacter == AnomalyBoundaryCharacter.Layered)
                    mineral += 0.15f;
            }
            float margin = Mathf.Abs(mineral - hazard);
            float peak = Mathf.Max(mineral, hazard);
            return peak > 0.35f && margin < 0.2f;
        }

        static bool CanReadyForFinding(ProspectorAnomaly a, float excavatorDistanceCells = -1f)
        {
            var o = a.ObservedSignals;
            if (o.TraitsKnownCount < MinTraitsForFinding) return false;
            if (o.HasContradictionFlag && !a.HasEvidence(AnomalyEvidenceKind.CrossCheck))
                return false;

            float skill = o.ObservationQuality01;
            // Skilled: Mixed means "contradiction needs another step" — not a final call yet
            if (skill >= ProspectorGeoEvidence.SkilledInterpretationThreshold
                && ProspectorGeoEvidence.WouldPublishUnresolvedMixed(a, skill)
                && HasUnusedUsefulEvidence(a, excavatorDistanceCells))
                return false;

            float agree = ProspectorGeoEvidence.AgreementScore(a);
            // Strong observed quality can clear with desk traits alone when coherent
            if (o.ObservationQuality01 > 0.7f && o.TraitsKnownCount >= 4 && agree >= 0.42f)
                return true;
            return agree >= MinAgreementForFinding;
        }

        static bool HasUnusedUsefulEvidence(ProspectorAnomaly a, float excavatorDistanceCells)
        {
            if (!a.HasEvidence(AnomalyEvidenceKind.CrossCheck)) return true;
            if (!a.HasEvidence(AnomalyEvidenceKind.FieldEvidence)) return true;
            if (!a.HasEvidence(AnomalyEvidenceKind.RefinerOpinion)) return true;
            bool excavNear = excavatorDistanceCells >= 0f
                             && excavatorDistanceCells <= ExcavatorLooseRockCells;
            if (excavNear && !a.HasEvidence(AnomalyEvidenceKind.LooseRockEvidence))
                return true;
            return false;
        }

        static void Push(ProspectorAnomaly a, AnomalyInvestigationNeed need)
        {
            for (int i = 0; i < a.RemainingNeeds.Count; i++)
                if (a.RemainingNeeds[i] == need) return;
            a.RemainingNeeds.Add(need);
        }

        static void AdvanceToHead(ProspectorAnomaly a)
        {
            while (a.RemainingNeeds.Count > 0 && NeedAlreadySatisfied(a, a.RemainingNeeds[0]))
                a.RemainingNeeds.RemoveAt(0);

            if (a.RemainingNeeds.Count == 0)
            {
                a.SetNeed(AnomalyInvestigationNeed.ReadyForFinding,
                    ReasonForNeed(AnomalyInvestigationNeed.ReadyForFinding, a));
                return;
            }

            var next = a.RemainingNeeds[0];
            a.SetNeed(next, ReasonForNeed(next, a));
        }

        static bool NeedAlreadySatisfied(ProspectorAnomaly a, AnomalyInvestigationNeed need) => need switch
        {
            AnomalyInvestigationNeed.NeedDeskInterpretation =>
                a.HasEvidence(AnomalyEvidenceKind.ScanInterpretation),
            AnomalyInvestigationNeed.NeedFieldEvidence =>
                a.HasEvidence(AnomalyEvidenceKind.FieldEvidence),
            AnomalyInvestigationNeed.NeedLooseRockInspection =>
                a.HasEvidence(AnomalyEvidenceKind.LooseRockEvidence),
            AnomalyInvestigationNeed.NeedRefinerConsultation =>
                a.HasEvidence(AnomalyEvidenceKind.RefinerOpinion),
            AnomalyInvestigationNeed.NeedCrossCheck =>
                a.HasEvidence(AnomalyEvidenceKind.CrossCheck),
            AnomalyInvestigationNeed.ReadyForFinding => false,
            _ => false,
        };

        public static string ReasonForNeed(AnomalyInvestigationNeed need, ProspectorAnomaly a) =>
            CurrentQuestion(need, a);

        /// <summary>
        /// Player-facing qualitative question / thought for the current investigation step.
        /// No hidden values or percentages — natural language only.
        /// </summary>
        public static string CurrentQuestion(AnomalyInvestigationNeed need, ProspectorAnomaly a)
        {
            var o = a != null ? a.ObservedSignals : default;
            int id = a != null ? a.AnomalyId : 0;
            string name = ProspectorAnomaly.SpokenId(id);

            return need switch
            {
                AnomalyInvestigationNeed.NeedDeskInterpretation =>
                    DeskQuestion(o),
                AnomalyInvestigationNeed.NeedFieldEvidence =>
                    o.HasFieldMatch && o.FieldMatch01 < 0f
                        ? "Field reading may conflict with the scan — need eyes on the rock."
                        : !o.HasBoundary
                            ? "The boundary doesn't read clean. Need a closer look at the formation."
                            : "Checking whether the exposed geology matches the scan response.",
                AnomalyInvestigationNeed.NeedLooseRockInspection =>
                    $"Checking whether the exposed rock matches {name}.",
                AnomalyInvestigationNeed.NeedRefinerConsultation =>
                    !o.HasConductivity
                        ? "Need another opinion on the conductive response."
                        : "Composition still won't settle — asking the Refiner.",
                AnomalyInvestigationNeed.NeedCrossCheck =>
                    o.HasContradictionFlag
                        ? "These readings disagree. Cross-checking before I call it."
                        : "Lining the evidence up at the desk before I commit.",
                AnomalyInvestigationNeed.ReadyForFinding =>
                    "Enough to form a conclusion — preparing the finding.",
                AnomalyInvestigationNeed.Complete =>
                    "Investigation closed.",
                _ => "",
            };
        }

        static string DeskQuestion(AnomalyObservedSignals o)
        {
            if (o.HasReturn && o.HasBoundary
                && o.Values.ReturnStrength > 0.55f
                && o.Values.BoundarySharpness01 < 0.45f)
                return "The return is strong, but the boundary doesn't fit.";
            if (o.HasReturn && o.HasAttenuation
                && o.Values.ReturnStrength > 0.5f && o.Values.Attenuation > 0.55f)
                return "Strong return with heavy attenuation — that pairing is odd.";
            if (o.HasConductivity && o.HasCoherence
                && o.Values.Conductivity > 0.5f && o.Values.StructuralCoherence < 0.4f)
                return "Conductive, but the structure looks broken.";
            if (o.HasContradictionFlag)
                return "Something in this response doesn't line up yet.";
            if (o.HasReturn && o.Values.ReturnStrength > 0.6f)
                return "Solid return — working out what kind of mass this is.";
            if (o.TraitsKnownCount >= 2)
                return "Pulling the scan response apart before I decide.";
            return "Reviewing the raw scan response.";
        }
    }
}
