using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    public enum ProspectorInvestigationState : byte
    {
        Idle = 0,
        Analysing = 1,
        WalkingToExcavator = 2,
        InspectingRock = 3,
        WalkingToRefiner = 4,
        ConsultingRefiner = 5,
        ReturningToAnalysis = 6,
        WalkingToLooseRock = 7,
    }

    /// <summary>
    /// Why the Prospector is doing the current physical / desk task.
    /// Driven by anomaly CurrentNeed — not analysis progress %.
    /// </summary>
    public enum ProspectorInvestigationReason : byte
    {
        None = 0,
        DeskAnalysis = 1,
        NeedFieldEvidence = 2,
        NeedRefinerConsultation = 3,
        ReturnToDesk = 4,
        NeedLooseRockInspection = 5,
        NeedCrossCheck = 6,
        ReadyForFinding = 7,
    }

    /// <summary>
    /// Continuous Pathfinder/Investigator loop after a heavy scan freezes.
    /// Desk clocks stay on the analyst; this class owns presence + evidence trips.
    /// </summary>
    public sealed class ProspectorInvestigationLoop
    {
        readonly ProspectorPerson _host;

        ProspectorInvestigationState _state = ProspectorInvestigationState.Idle;
        ProspectorInvestigationReason _reason = ProspectorInvestigationReason.None;
        ProspectorInvestigationReason _activeTripReason = ProspectorInvestigationReason.None;
        int _focusAnomalyId = -1;
        int _focusScanId = -1;
        float _dwellHours;
        float _dwellNeeded;
        float _hoursSinceTrip = 999f;
        Vector2 _deskPos;
        bool _hasDesk;
        Vector2 _tripStand;
        bool _hasTripStand;
        bool _consultStarted;
        /// <summary>PROTOTYPE: nearest loose pile — not provenance-linked to the anomaly.</summary>
        LoosePile _prototypeLoosePile;

        public ProspectorInvestigationState State => _state;
        public ProspectorInvestigationReason Reason => _reason;
        public int FocusAnomalyId => _focusAnomalyId;
        public float DwellHours => _dwellHours;
        public float DwellNeeded => _dwellNeeded;
        public bool HasTripStand => _hasTripStand;
        public Vector2 TripStand => _tripStand;
        /// <summary>Deprecated schedule queue — always None (needs live on the anomaly).</summary>
        public ProspectorInvestigationReason QueuedNeed => ProspectorInvestigationReason.None;

        public string DebugStateLabel => _state switch
        {
            ProspectorInvestigationState.Idle => "IDLE",
            ProspectorInvestigationState.Analysing =>
                _focusAnomalyId > 0 ? $"ANALYSING #{_focusAnomalyId:00}" : "ANALYSING",
            ProspectorInvestigationState.WalkingToExcavator => "WALKING TO EXCAVATOR",
            ProspectorInvestigationState.WalkingToLooseRock => "WALKING TO LOOSE ROCK",
            ProspectorInvestigationState.InspectingRock => "INSPECTING",
            ProspectorInvestigationState.WalkingToRefiner => "WALKING TO REFINER",
            ProspectorInvestigationState.ConsultingRefiner => "CONSULTING REFINER",
            ProspectorInvestigationState.ReturningToAnalysis => "RETURNING TO ANALYSIS",
            _ => "IDLE",
        };

        public string DebugReasonLabel => _reason.ToString();

        public Vector2 ResumeWorldPosition
        {
            get
            {
                switch (_state)
                {
                    case ProspectorInvestigationState.WalkingToExcavator:
                    case ProspectorInvestigationState.WalkingToLooseRock:
                    case ProspectorInvestigationState.InspectingRock:
                    case ProspectorInvestigationState.WalkingToRefiner:
                    case ProspectorInvestigationState.ConsultingRefiner:
                        return _hasTripStand ? _tripStand : ResolveDesk();
                    default:
                        return ResolveDesk();
                }
            }
        }

        /// <summary>Desk analysis clock only advances while the Prospector is at the desk.</summary>
        public bool DeskAnalysisActive =>
            _state == ProspectorInvestigationState.Idle
            || _state == ProspectorInvestigationState.Analysing;

        float MinHoursBetweenTrips =>
            ProspectorScanFormulas.UseTestingScanDurations ? 0.35f : 2.2f;
        float InspectDwellHours =>
            ProspectorScanFormulas.UseTestingScanDurations ? 0.35f : 1.15f;
        float ConsultDwellHours =>
            ProspectorScanFormulas.UseTestingScanDurations ? 0.55f : 1.4f;

        public string PlayerWorkLabel
        {
            get
            {
                var a = ResolveFocusAnomaly();
                return _state switch
                {
                    ProspectorInvestigationState.WalkingToExcavator => "Walking to excavator",
                    ProspectorInvestigationState.WalkingToLooseRock => "Walking to excavation face",
                    ProspectorInvestigationState.InspectingRock =>
                        a != null && a.CurrentNeed == AnomalyInvestigationNeed.NeedLooseRockInspection
                            ? "Inspecting excavation face"
                            : "Inspecting rock face",
                    ProspectorInvestigationState.WalkingToRefiner => "Going to Refiner",
                    ProspectorInvestigationState.ConsultingRefiner => "Consulting Refiner",
                    ProspectorInvestigationState.ReturningToAnalysis => "Returning to desk",
                    ProspectorInvestigationState.Analysing when a != null =>
                        ProspectorAnomaly.NeedWorkLabel(a.CurrentNeed),
                    ProspectorInvestigationState.Idle when a != null =>
                        ProspectorAnomaly.NeedWorkLabel(a.CurrentNeed),
                    _ => "Standby",
                };
            }
        }

        public string PlayerReasonLabel
        {
            get
            {
                var a = ResolveFocusAnomaly();
                if (a != null && !string.IsNullOrEmpty(a.NeedDetail))
                    return a.NeedDetail;
                return "";
            }
        }

        /// <summary>Quoted CURRENT QUESTION for player HUD (strips outer quotes if present).</summary>
        public string PlayerQuestionLabel
        {
            get
            {
                string q = PlayerReasonLabel;
                if (string.IsNullOrEmpty(q)) return "";
                if (q.Length >= 2 && q[0] == '"' && q[q.Length - 1] == '"')
                    return q;
                return q;
            }
        }

        public ProspectorInvestigationLoop(ProspectorPerson host) => _host = host;

        public void Reset()
        {
            _state = ProspectorInvestigationState.Idle;
            _reason = ProspectorInvestigationReason.None;
            _activeTripReason = ProspectorInvestigationReason.None;
            _focusAnomalyId = -1;
            _focusScanId = -1;
            _dwellHours = 0f;
            _hasTripStand = false;
            _consultStarted = false;
            _prototypeLoosePile = null;
            EndRefinerConsultation();
        }

        public void SetDeskPosition(Vector2 desk)
        {
            _deskPos = desk;
            _hasDesk = true;
        }

        public void TickMovement()
        {
            if (_host == null || _host.WorkMode != ProspectorWorkMode.Investigate) return;
            if (_host.HasScannerAssignment) return;

            switch (_state)
            {
                case ProspectorInvestigationState.WalkingToExcavator:
                    TickWalkToExcavator();
                    break;
                case ProspectorInvestigationState.WalkingToLooseRock:
                    TickWalkToLooseRock();
                    break;
                case ProspectorInvestigationState.WalkingToRefiner:
                    TickWalkToRefiner();
                    break;
                case ProspectorInvestigationState.ReturningToAnalysis:
                    TickReturnToDesk();
                    break;
                case ProspectorInvestigationState.Analysing:
                case ProspectorInvestigationState.Idle:
                    HoldNearDesk();
                    break;
            }
        }

        public void TickGameHours(float hoursDelta, float absoluteGameHours)
        {
            if (_host == null || _host.WorkMode != ProspectorWorkMode.Investigate) return;
            if (_host.HasScannerAssignment) return;
            if (hoursDelta <= 0f) return;

            _hoursSinceTrip += hoursDelta;

            switch (_state)
            {
                case ProspectorInvestigationState.Idle:
                case ProspectorInvestigationState.Analysing:
                    TickDeskPresence();
                    break;
                case ProspectorInvestigationState.InspectingRock:
                    TickInspectDwell(hoursDelta);
                    break;
                case ProspectorInvestigationState.ConsultingRefiner:
                    TickConsultDwell(hoursDelta);
                    break;
            }
        }

        public bool HasInvestigationWork()
        {
            var scan = _host?.ScanHistory?.DisplayScan;
            if (scan == null || !scan.FrozenAnomalies) return false;
            for (int i = 0; i < scan.Anomalies.Count; i++)
            {
                var a = scan.Anomalies[i];
                if (a.CurrentNeed == AnomalyInvestigationNeed.Complete) continue;
                if (a.AnalysisStatus == AnomalyAnalysisStatus.Assessed) continue;
                if (a.TileCount < ProspectorAnomalyInterpretation.MinTilesToAnalyse) continue;
                return true;
            }
            return false;
        }

        void TickDeskPresence()
        {
            var history = _host.ScanHistory;
            var analyst = history?.Analyst;
            var scan = history?.DisplayScan;
            if (scan == null || !scan.FrozenAnomalies)
            {
                Enter(ProspectorInvestigationState.Idle, ProspectorInvestigationReason.None, -1, -1);
                return;
            }

            ProspectorAnomaly focus = analyst?.Current;
            if (focus == null)
                focus = FindClosestUnresolved(scan);

            if (focus == null)
            {
                Enter(ProspectorInvestigationState.Idle, ProspectorInvestigationReason.None, -1, scan.ScanId);
                return;
            }

            Enter(ProspectorInvestigationState.Analysing, ReasonFromNeed(focus.CurrentNeed),
                focus.AnomalyId, focus.ScanId);

            analyst?.EnsureDeskClockMatchesNeed();

            switch (focus.CurrentNeed)
            {
                case AnomalyInvestigationNeed.NeedFieldEvidence:
                    if (_hoursSinceTrip >= MinHoursBetweenTrips)
                        BeginFieldTrip(focus);
                    break;
                case AnomalyInvestigationNeed.NeedLooseRockInspection:
                    if (_hoursSinceTrip >= MinHoursBetweenTrips)
                        BeginLooseRockTrip(focus);
                    break;
                case AnomalyInvestigationNeed.NeedRefinerConsultation:
                    if (_hoursSinceTrip >= MinHoursBetweenTrips)
                        BeginRefinerTrip(focus);
                    break;
                case AnomalyInvestigationNeed.ReadyForFinding:
                    analyst?.PublishFinding();
                    break;
                case AnomalyInvestigationNeed.NeedDeskInterpretation:
                case AnomalyInvestigationNeed.NeedCrossCheck:
                case AnomalyInvestigationNeed.Complete:
                case AnomalyInvestigationNeed.None:
                default:
                    break;
            }
        }

        static ProspectorInvestigationReason ReasonFromNeed(AnomalyInvestigationNeed need) => need switch
        {
            AnomalyInvestigationNeed.NeedDeskInterpretation => ProspectorInvestigationReason.DeskAnalysis,
            AnomalyInvestigationNeed.NeedFieldEvidence => ProspectorInvestigationReason.NeedFieldEvidence,
            AnomalyInvestigationNeed.NeedLooseRockInspection =>
                ProspectorInvestigationReason.NeedLooseRockInspection,
            AnomalyInvestigationNeed.NeedRefinerConsultation =>
                ProspectorInvestigationReason.NeedRefinerConsultation,
            AnomalyInvestigationNeed.NeedCrossCheck => ProspectorInvestigationReason.NeedCrossCheck,
            AnomalyInvestigationNeed.ReadyForFinding => ProspectorInvestigationReason.ReadyForFinding,
            _ => ProspectorInvestigationReason.DeskAnalysis,
        };

        void BeginFieldTrip(ProspectorAnomaly a)
        {
            _activeTripReason = ProspectorInvestigationReason.NeedFieldEvidence;
            _hoursSinceTrip = 0f;
            a.SetNeed(
                AnomalyInvestigationNeed.NeedFieldEvidence,
                ProspectorInvestigationPlanner.ReasonForNeed(
                    AnomalyInvestigationNeed.NeedFieldEvidence, a));

            var ex = _host.Excavator;
            if (ex != null)
            {
                _tripStand = _host.FindInvestigationStandNear(ex.Position, preferBehind: true);
                _hasTripStand = true;
            }
            Enter(ProspectorInvestigationState.WalkingToExcavator,
                ProspectorInvestigationReason.NeedFieldEvidence, a.AnomalyId, a.ScanId);
            DigHoodLog.Push($"PROSPECTOR | field evidence → excavator (#{a.AnomalyId:00})");
        }

        void BeginLooseRockTrip(ProspectorAnomaly a)
        {
            _activeTripReason = ProspectorInvestigationReason.NeedLooseRockInspection;
            _hoursSinceTrip = 0f;
            a.SetNeed(
                AnomalyInvestigationNeed.NeedLooseRockInspection,
                ProspectorInvestigationPlanner.ReasonForNeed(
                    AnomalyInvestigationNeed.NeedLooseRockInspection, a));

            // PROTOTYPE: nearest live loose pile — not provenance-linked to this anomaly.
            Vector2 from = ApproximateAnomalyWorld(a);
            _prototypeLoosePile = LoosePile.FindNearest(from, maxDist: 80f, unclaimedOnly: false);
            if (_prototypeLoosePile != null)
            {
                Vector2 pilePos = LoosePile.PileTerrainPos(_prototypeLoosePile, _host.transform.parent);
                _tripStand = _host.FindInvestigationStandNear(pilePos, preferBehind: false);
                _hasTripStand = true;
                Enter(ProspectorInvestigationState.WalkingToLooseRock,
                    ProspectorInvestigationReason.NeedLooseRockInspection, a.AnomalyId, a.ScanId);
                DigHoodLog.Push(
                    $"PROSPECTOR | loose-rock PROTOTYPE → nearest pile (#{a.AnomalyId:00})");
            }
            else
            {
                // Fallback: excavator front if no piles exist yet
                DigHoodLog.Push(
                    $"PROSPECTOR | loose-rock PROTOTYPE — no pile, using excavator (#{a.AnomalyId:00})");
                var ex = _host.Excavator;
                if (ex != null)
                {
                    _tripStand = _host.FindInvestigationStandNear(ex.Position, preferBehind: true);
                    _hasTripStand = true;
                }
                a.SetNeed(
                    AnomalyInvestigationNeed.NeedLooseRockInspection,
                    "no loose pile yet — inspecting excavation front instead");
                Enter(ProspectorInvestigationState.WalkingToExcavator,
                    ProspectorInvestigationReason.NeedLooseRockInspection, a.AnomalyId, a.ScanId);
            }
        }

        void BeginRefinerTrip(ProspectorAnomaly a)
        {
            _activeTripReason = ProspectorInvestigationReason.NeedRefinerConsultation;
            _hoursSinceTrip = 0f;
            _consultStarted = false;
            a.SetNeed(
                AnomalyInvestigationNeed.NeedRefinerConsultation,
                ProspectorInvestigationPlanner.ReasonForNeed(
                    AnomalyInvestigationNeed.NeedRefinerConsultation, a));

            var rf = _host.Refiner;
            if (rf != null)
            {
                _tripStand = _host.FindInvestigationStandNear(rf.ConsultationMeetPoint, preferBehind: false);
                _hasTripStand = true;
                rf.RequestConsultation(rf.WorkPoint, ConsultDwellHours);
            }
            Enter(ProspectorInvestigationState.WalkingToRefiner,
                ProspectorInvestigationReason.NeedRefinerConsultation, a.AnomalyId, a.ScanId);
            DigHoodLog.Push($"PROSPECTOR | refiner consult → washer (#{a.AnomalyId:00})");
        }

        void TickWalkToExcavator()
        {
            var ex = _host.Excavator;
            if (ex == null)
            {
                RecordTripEvidenceSkipped(AnomalyEvidenceKind.FieldEvidence);
                StartReturnToDesk();
                return;
            }

            if (!_hasTripStand)
            {
                _tripStand = _host.FindInvestigationStandNear(ex.Position, preferBehind: true);
                _hasTripStand = true;
            }

            if (_host.InvestigationNavFollow(_tripStand, _host.InvestigationMoveSpeed))
            {
                var reason = _activeTripReason == ProspectorInvestigationReason.NeedLooseRockInspection
                    ? ProspectorInvestigationReason.NeedLooseRockInspection
                    : ProspectorInvestigationReason.NeedFieldEvidence;
                Enter(ProspectorInvestigationState.InspectingRock, reason, _focusAnomalyId, _focusScanId);
                _dwellHours = 0f;
                _dwellNeeded = InspectDwellHours;
                _host.FaceToward(ex.Position);
                Debug.Log($"[PROSPECTOR] Arrived excavator — inspect (#{_focusAnomalyId:00})");
            }
        }

        void TickWalkToLooseRock()
        {
            if (_prototypeLoosePile == null || !_prototypeLoosePile)
            {
                RecordTripEvidenceSkipped(AnomalyEvidenceKind.LooseRockEvidence);
                StartReturnToDesk();
                return;
            }

            Vector2 pilePos = LoosePile.PileTerrainPos(_prototypeLoosePile, _host.transform.parent);
            if (!_hasTripStand)
            {
                _tripStand = _host.FindInvestigationStandNear(pilePos, preferBehind: false);
                _hasTripStand = true;
            }

            if (_host.InvestigationNavFollow(_tripStand, _host.InvestigationMoveSpeed))
            {
                Enter(ProspectorInvestigationState.InspectingRock,
                    ProspectorInvestigationReason.NeedLooseRockInspection, _focusAnomalyId, _focusScanId);
                _dwellHours = 0f;
                _dwellNeeded = InspectDwellHours;
                _host.FaceToward(pilePos);
                Debug.Log($"[PROSPECTOR] Arrived loose pile — inspect (#{_focusAnomalyId:00})");
            }
        }

        void TickWalkToRefiner()
        {
            var rf = _host.Refiner;
            if (rf == null)
            {
                RecordTripEvidenceSkipped(AnomalyEvidenceKind.RefinerOpinion);
                StartReturnToDesk();
                return;
            }

            if (!_hasTripStand)
            {
                _tripStand = _host.FindInvestigationStandNear(rf.ConsultationMeetPoint, preferBehind: false);
                _hasTripStand = true;
                rf.RequestConsultation(rf.WorkPoint, ConsultDwellHours);
            }

            if (!rf.IsInConsultation)
                rf.RequestConsultation(rf.WorkPoint, ConsultDwellHours);

            if (_host.InvestigationNavFollow(_tripStand, _host.InvestigationMoveSpeed))
            {
                Enter(ProspectorInvestigationState.ConsultingRefiner,
                    ProspectorInvestigationReason.NeedRefinerConsultation, _focusAnomalyId, _focusScanId);
                _dwellHours = 0f;
                _dwellNeeded = ConsultDwellHours;
                _consultStarted = true;
                rf.BeginDiscussion(_host.Position);
                _host.FaceToward(rf.Position);
                DigHoodLog.Push("PROSPECTOR | consulting Refiner — both paused");
            }
        }

        void TickInspectDwell(float hoursDelta)
        {
            _dwellHours += hoursDelta;
            if (_reason == ProspectorInvestigationReason.NeedLooseRockInspection
                && _prototypeLoosePile != null && _prototypeLoosePile)
            {
                _host.FaceToward(LoosePile.PileTerrainPos(_prototypeLoosePile, _host.transform.parent));
            }
            else
            {
                var ex = _host.Excavator;
                if (ex != null) _host.FaceToward(ex.Position);
            }

            if (_dwellHours + 0.0001f < _dwellNeeded) return;

            var a = ResolveFocusAnomaly();
            bool loose = _reason == ProspectorInvestigationReason.NeedLooseRockInspection
                         || (a != null && a.CurrentNeed == AnomalyInvestigationNeed.NeedLooseRockInspection);

            if (loose)
            {
                var lines = ProspectorGeoEvidence.RevealLooseRockInspection(a, AnalysisStats());
                ProspectorGeoEvidence.AppendFindings(
                    a, lines, _host.ScanHistory?.Findings, "Loose rock inspection");
                ProspectorInvestigationPlanner.ApplyAfterEvidence(
                    a, AnomalyEvidenceKind.LooseRockEvidence, ExcavatorDistanceCells(a));
                DigHoodLog.Push($"PROSPECTOR | LooseRockEvidence recorded (#{_focusAnomalyId:00})");
            }
            else
            {
                var lines = ProspectorGeoEvidence.RevealFieldInspection(a, AnalysisStats());
                ProspectorGeoEvidence.AppendFindings(
                    a, lines, _host.ScanHistory?.Findings, "Field inspection");
                ProspectorInvestigationPlanner.ApplyAfterEvidence(
                    a, AnomalyEvidenceKind.FieldEvidence, ExcavatorDistanceCells(a));
                DigHoodLog.Push($"PROSPECTOR | FieldEvidence recorded (#{_focusAnomalyId:00})");
            }

            _prototypeLoosePile = null;
            _hasTripStand = false;
            StartReturnToDesk();
        }

        void TickConsultDwell(float hoursDelta)
        {
            _dwellHours += hoursDelta;
            var rf = _host.Refiner;
            if (rf != null)
            {
                _host.FaceToward(rf.Position);
                rf.TickConsultation(hoursDelta, _host.Position);
            }

            if (_dwellHours + 0.0001f < _dwellNeeded) return;

            var a = ResolveFocusAnomaly();
            var lines = ProspectorGeoEvidence.RevealRefinerOpinion(a, AnalysisStats());
            ProspectorGeoEvidence.AppendFindings(
                a, lines, _host.ScanHistory?.Findings, "Refiner consultation");
            ProspectorInvestigationPlanner.ApplyAfterEvidence(
                a, AnomalyEvidenceKind.RefinerOpinion, ExcavatorDistanceCells(a));
            DigHoodLog.Push($"PROSPECTOR | RefinerOpinion recorded (#{_focusAnomalyId:00})");

            EndRefinerConsultation();
            _hasTripStand = false;
            _consultStarted = false;
            DigHoodLog.Push("PROSPECTOR | consult done — both resume work");
            StartReturnToDesk();
        }

        void RecordTripEvidenceSkipped(AnomalyEvidenceKind kind)
        {
            var a = ResolveFocusAnomaly();
            if (a == null) return;
            a.AddFindingEntry($"Skipped {ProspectorAnomaly.EvidenceLabel(kind)} — target unavailable");
            ProspectorInvestigationPlanner.ApplyAfterEvidence(a, kind, ExcavatorDistanceCells(a));
        }

        void EndRefinerConsultation()
        {
            _host?.Refiner?.EndConsultation();
        }

        void StartReturnToDesk()
        {
            EndRefinerConsultation();
            _activeTripReason = ProspectorInvestigationReason.ReturnToDesk;
            Enter(ProspectorInvestigationState.ReturningToAnalysis,
                ProspectorInvestigationReason.ReturnToDesk, _focusAnomalyId, _focusScanId);
        }

        void TickReturnToDesk()
        {
            Vector2 desk = ResolveDesk();
            if (_host.InvestigationNavFollow(desk, _host.InvestigationMoveSpeed))
            {
                Debug.Log($"[PROSPECTOR] Returned to analysis desk (#{_focusAnomalyId:00})");
                Enter(ProspectorInvestigationState.Analysing,
                    ProspectorInvestigationReason.DeskAnalysis, _focusAnomalyId, _focusScanId);
                _activeTripReason = ProspectorInvestigationReason.None;
                _host.ScanHistory?.Analyst?.EnsureDeskClockMatchesNeed();
            }
        }

        void HoldNearDesk()
        {
            if (!_hasDesk && _host.AssignedScanner == null && _host.ScanHistory?.DisplayScan == null)
                return;
            Vector2 desk = ResolveDesk();
            float dist = Vector2.Distance(_host.Position, desk);
            if (dist > 0.55f)
                _host.InvestigationNavFollow(desk, _host.InvestigationMoveSpeed * 0.9f);
        }

        public Vector2 DeskWorldPosition => ResolveDesk();

        Vector2 ResolveDesk()
        {
            if (_host.AssignedScanner != null)
                return _host.AssignedScanner.Position;
            if (_hasDesk) return _deskPos;
            var scan = _host.ScanHistory?.DisplayScan;
            if (scan != null) return scan.ScannerPosition;
            return _host.Position;
        }

        ProspectorAnalysisStats AnalysisStats()
        {
            if (_host?.Stats != null)
                return ProspectorAnalysisStats.From(_host.Stats);
            return default;
        }

        public float ExcavatorDistanceCells(ProspectorAnomaly a)
        {
            if (a == null || _host?.Excavator == null || _host.World == null) return -1f;
            Vector2 center = ApproximateAnomalyWorld(a);
            float worldDist = Vector2.Distance(_host.Excavator.Position, center);
            float cell = Mathf.Max(0.01f, _host.World.CellSize);
            return worldDist / cell;
        }

        Vector2 ApproximateAnomalyWorld(ProspectorAnomaly a)
        {
            if (a == null || _host?.World == null) return _host != null ? _host.Position : Vector2.zero;
            int cx = Mathf.RoundToInt(a.ApproximateCenterCells.x);
            int cy = Mathf.RoundToInt(a.ApproximateCenterCells.y);
            return _host.World.CellCenter(cx, cy);
        }

        ProspectorAnomaly ResolveFocusAnomaly()
        {
            if (_focusAnomalyId <= 0) return null;
            var scan = _host?.ScanHistory?.DisplayScan;
            if (scan == null) return null;
            for (int i = 0; i < scan.Anomalies.Count; i++)
                if (scan.Anomalies[i].AnomalyId == _focusAnomalyId)
                    return scan.Anomalies[i];
            return null;
        }

        static ProspectorAnomaly FindClosestUnresolved(ProspectorScanRecord scan)
        {
            ProspectorAnomaly best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < scan.Anomalies.Count; i++)
            {
                var a = scan.Anomalies[i];
                if (a.CurrentNeed == AnomalyInvestigationNeed.Complete) continue;
                if (a.AnalysisStatus == AnomalyAnalysisStatus.Assessed) continue;
                if (a.TileCount < ProspectorAnomalyInterpretation.MinTilesToAnalyse) continue;
                if (a.DistanceFromScannerCells < bestD)
                {
                    bestD = a.DistanceFromScannerCells;
                    best = a;
                }
            }
            return best;
        }

        void Enter(
            ProspectorInvestigationState state,
            ProspectorInvestigationReason reason,
            int anomalyId,
            int scanId)
        {
            bool changed = state != _state || reason != _reason || anomalyId != _focusAnomalyId;
            _state = state;
            _reason = reason;
            if (anomalyId > 0) _focusAnomalyId = anomalyId;
            if (scanId > 0) _focusScanId = scanId;
            if (changed)
            {
                Debug.Log(
                    $"[PROSPECTOR] State={DebugStateLabel} Reason={DebugReasonLabel} " +
                    $"Work={PlayerWorkLabel}");
            }
        }
    }
}
