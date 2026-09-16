# Recruitment V1 Audit

Generated: 2026-09-04 08:35:16
Mode: static verification (Unity batch blocked — project already open in Editor)

## Catalog
- PASS  Candidates for Excavation — count=5
- PASS  Candidates for Prospecting — count=5
- PASS  Candidates for Hauling — count=5
- PASS  Candidates for Refining — count=5
- PASS  Candidates for Engineering — count=5
- PASS  All hire jobs have ≥4 candidates
- PASS  Candidate IDs unique (25)

## Materialize / identity
- PASS  Session fills 5 seats (RecruitmentHiringSession.AllJobsFilled)
- PASS  TryBuildCrew materializes unique WorkerIds from base 101+
- PASS  Unique Stats instances per Materialize() / Clone()
- PASS  WorkerState.CreateDefault on WorkerRuntime ctor
- PASS  Identity + traits persist via WorkerIdentityProfile

## Stat tooltips (job-specific)
- PASS  Raw Power Excavation HIGH (live dig power formula)
- PASS  Raw Power Prospecting not HIGH / text differs
- PASS  Mineralogy Excavation LOW
- PASS  Hover tooltips bind to BrowseJob (updates when switching job tabs)

## Traits
- PASS  Modest hire-time ApplyToStats deltas (CalmUnderPressure, etc.)
- PASS  Positive / negative / mixed kinds shown in UI

## Session cancel safety
- PASS  Open hiring does not call ReplaceActiveCrew
- PASS  Cancel / Close leaves active crew untouched
- PASS  Confirm only after all 5 seats filled

## Crew replace architecture
- PASS  ForceClearAllJobHosts → Clear assignments → BindHost per job
- PASS  SpawnCrewAvatars + SocialAura.Bootstrap after hire
- PASS  Named aliases remapped by job (SyncNamedCrewAliasesFromJobs)
- PASS  Sheet baselines Dictionary&lt;int&gt; supports hired ids
- PASS  RESET TO DEFAULT CREW → BootstrapPrototypeCrew

## Runtime notes (play-mode checklist)
- [ ] START GAME opens hiring UI
- [ ] Existing crew untouched before confirmation
- [ ] All 5 jobs can be filled / swapped before confirm
- [ ] Hired crew replaces cleanly (no duplicates)
- [ ] Social Aura + assignments work with hired workers
- [ ] Stat tooltips change per selected job tab
- [ ] SKIP START GAME → default Lewis/Mara/… crew unchanged

Run menu: **DeepCore → Diagnostics → Run Recruitment V1 Audit** (when Editor free).

**Result:** PASS  (static + wiring; play-mode checklist for designer)
