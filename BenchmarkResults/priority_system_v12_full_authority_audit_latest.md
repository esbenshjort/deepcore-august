# Priority System V1.2 — Full Authority + Parameter Validation

**Generated:** 2026-09-14  
**Scope:** Prove Priority System is behaviorally authoritative across all current parameters and executors. No new features/tasks/UI redesign.

**Read:** `priority_system_v1_latest.md`, `priority_system_v11_playmode_latest.md`, `priority_off_authority_fix_latest.md`

**Verdict:** Core authority paths are **substantially fixed and automated-proven**. System is **not declared LOCKED** — live Game-view matrix remains required for stop-feel, thrash, claims, and emergency timing.

Menu: `DeepCore/Diagnostics/Run Priority System V1.2 Full Authority Audit`

---

## Evidence classes

| Label | Meaning |
|-------|---------|
| **AUTOMATED PASS** | Deterministic code/API check passed |
| **LIVE PLAYMODE PASS** | Observed in Game view this session |
| **NOT TESTED** | Requires live observation; not run here |
| **PASS** | Automated + prior live evidence adequate for that cell |
| **PARTIAL** | Code present; live confirmation missing or residual edge |
| **FAIL** | Broken (none remaining after this fix pass for known bugs) |
| **NOT APPLICABLE** | No executor / honest unavailable |

This agent session: **no Game-view play**. Live cells = **NOT TESTED**.

---

## Score weights (authoritative)

| Component | Constant | Effect |
|-----------|----------|--------|
| Player priority | `PriorityWeight = 120` | P1=480, P2=360, P3=240, P4=120 |
| Hour deficit | `DeficitWeight = 28` | `deficitHours * 28` |
| Suitability | `SuitWeight = 18` | max ~18 |
| Distance | `DistanceWeight = 12` | max −12 |
| Continuity | `ContinuityBonus = 22` (×1.5 under MinCommit 0.35h) | |
| Completed target | `CompletedTargetPenalty = 14` | soft |
| Same-priority hysteresis | `SamePrioritySwitchMargin = 35` | |
| Emergency | `EmergencyScoreBoost = 1000` (Treat ×0.85) | |

**Player priority dominates Suit+Distance** (one priority step = 120 ≫ ~30 soft).  
**Large unmet hour targets can compete with priority** (by design).  
**OFF** is not scored (skipped). **Emergency** overwhelms soft scores.

### Switching behavior (documented)

| Change | Behavior |
|--------|----------|
| A=1, B=2 → A wins | Normal |
| A=2, B=1 → B wins | Normal |
| A=1, B=1 | Suit / distance / continuity / deficit break ties; margin 35 to leave current |
| A=4, B=1 | B strongly wins unless MinCommit/continuity holds A briefly |
| Priority 1→4 while active | Dirty re-eval; may hold via MinCommit/hysteresis if still valid |
| **OFF while active** | Immediate invalidate — **no** MinCommit hold |

---

## Task × parameter matrix

| Task | Priority order | OFF | Active invalidate | Hour targets | Availability | Requirements | Emergency | Host auth | Movement | Claims | Work-hours | Cross-spec | Selection indep |
|------|----------------|-----|-------------------|--------------|--------------|--------------|-----------|-----------|----------|--------|------------|------------|-----------------|
| Rescue | AUTOMATED | AUTOMATED* | AUTOMATED* | N/A (emerg) | AUTOMATED | AUTOMATED | AUTOMATED | PASS (person) | NOT TESTED | exclusive mission | PARTIAL | AUTOMATED | PASS |
| Treat Injuries | AUTOMATED | AUTOMATED | AUTOMATED | soft | AUTOMATED | AUTOMATED | AUTOMATED | Steward/duty | NOT TESTED | soft | PARTIAL | AUTOMATED | PASS |
| Clear Debris | AUTOMATED | AUTOMATED | AUTOMATED | soft | AUTOMATED | AUTOMATED | N/A | duty/hauler | NOT TESTED | field | AUTOMATED† | AUTOMATED | PASS |
| Supports | AUTOMATED | AUTOMATED | AUTOMATED | AUTOMATED | AUTOMATED | AUTOMATED | N/A | AUTOMATED | NOT TESTED | exclusive eng | AUTOMATED† | AUTOMATED | PASS |
| Lighting | AUTOMATED | AUTOMATED | AUTOMATED | AUTOMATED | AUTOMATED | AUTOMATED | N/A | AUTOMATED | NOT TESTED | exclusive eng | AUTOMATED† | AUTOMATED | PASS |
| Repair | AUTOMATED | AUTOMATED | AUTOMATED | soft | AUTOMATED | AUTOMATED | soft | AUTOMATED | NOT TESTED | exclusive eng | AUTOMATED† | AUTOMATED | PASS |
| Excavate | AUTOMATED | AUTOMATED | AUTOMATED | soft | AUTOMATED | AUTOMATED (paint) | N/A | AUTOMATED | NOT TESTED | exclusive excav | AUTOMATED† | AUTOMATED | PASS |
| Prospect | AUTOMATED | AUTOMATED | AUTOMATED | soft | AUTOMATED (Manual honest) | AUTOMATED (player order) | N/A | AUTOMATED | NOT TESTED | exclusive pros | PARTIAL | AUTOMATED | PASS |
| Analyse | AUTOMATED | AUTOMATED | AUTOMATED | soft | AUTOMATED | AUTOMATED | N/A | via Prospect host | NOT TESTED | queue | PARTIAL | AUTOMATED | PASS |
| Haul | AUTOMATED | AUTOMATED | AUTOMATED | soft | AUTOMATED | AUTOMATED | N/A | AUTOMATED | NOT TESTED | pile claim | AUTOMATED† | AUTOMATED | PASS |
| Refine | AUTOMATED | AUTOMATED | AUTOMATED | soft | AUTOMATED | AUTOMATED | N/A | AUTOMATED | NOT TESTED | exclusive | AUTOMATED† | AUTOMATED | PASS |
| Prepare Meals | AUTOMATED | AUTOMATED | AUTOMATED | soft | AUTOMATED | AUTOMATED | N/A | Steward | NOT TESTED | exclusive stew | PARTIAL | AUTOMATED | PASS |
| Clean Camp | AUTOMATED | AUTOMATED | AUTOMATED | soft | AUTOMATED | AUTOMATED | N/A | Steward | NOT TESTED | exclusive stew | PARTIAL | AUTOMATED | PASS |
| Maintain Hygiene | **N/A** | N/A | N/A | N/A | **honest None** | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| Tend Camp Systems | PARTIAL | AUTOMATED | AUTOMATED | soft | thin (steward-tied) | thin | N/A | Steward | NOT TESTED | exclusive stew | PARTIAL | AUTOMATED | PASS |

\* Rescue mid-mission OFF abort **fixed this pass** (automated source + API). Live carry abort = **NOT TESTED**.  
† Accrual honesty tightened this pass (dig/load/build only) — live meter check = **NOT TESTED**.

---

## Thin / unimplemented executors (honest)

| Task | Status |
|------|--------|
| **Maintain Hygiene** | No distinct work target. Toilet = hard override outside priorities. **Unavailable.** |
| **Tend Camp Systems** | Only when Steward already on kitchen/after-meal. Soft claim does not invent it. |
| **Prospect Manual** | No work until player creates mode/scanner order. Priority never invents scans. |

---

## Bugs found → fixes (this pass)

| Bug | Root cause | Fix |
|-----|------------|-----|
| Rescue OFF mid-mission continued | `TickUniversalRescue` never re-checked OFF after accept | Abort mission when rescuer Rescue is OFF |
| Work gone left stale ActiveTask | Invalidate only checked OFF/unknown | Probe availability; clear when unavailable (Rescue duty excluded) |
| Haul/Refine mid-task falsely unavailable | Probe ignored cargo / mid-wash | ProbeHaul cargo; ProbeRefine in-progress |
| Empty resolve left ActiveTask | ApplyResolve early-return | Clear ActiveTask on empty resolve |
| Dead/incap orphaned station claim | MinCommit protected dead; MARK DEAD no unassign | `ReleaseUnavailableWorkerClaims`; claim steal skips dead |
| Hours accrued while idle/travel | Excavate pending paint; ClearDebris walking; SEEK haul; shared eng kinds | Accrue only active dig / load-haul-unload / wash-carry / build-install-repair at site |
| DEV diag missing TaskAvailable | Incomplete §22 | Show Avail + performing + host/dirty |

---

## Files changed

- `WorkAvailability.cs` — haul cargo / refine in-progress probes
- `WorkPriorityDirector.cs` — availability invalidate; empty-resolve clear
- `FreeMovementSocketMapRunner.cs` — Rescue OFF abort; dead/incap release; accrual honesty; DEV diag
- `PrioritySystemV12FullAuthorityAudit.cs` — **added**
- `Editor/WorkerV11LockAuditMenu.cs` — menu entry

---

## Six-worker checklist (code authority)

| Worker | Spec P1 | Spec OFF | Cross-task ON | Cross OFF | Prio while active | Selection while active |
|--------|---------|----------|---------------|-----------|-------------------|------------------------|
| Lewis | AUTOMATED | AUTOMATED | AUTOMATED | AUTOMATED | AUTOMATED (dirty) | PASS (iterates all crew) |
| Mara | AUTOMATED | AUTOMATED | AUTOMATED | AUTOMATED | AUTOMATED | PASS |
| Kowalski | AUTOMATED | AUTOMATED | AUTOMATED | AUTOMATED | AUTOMATED | PASS |
| Elena | AUTOMATED | AUTOMATED | AUTOMATED | AUTOMATED | AUTOMATED | PASS |
| Viktor | AUTOMATED | AUTOMATED | AUTOMATED | AUTOMATED | AUTOMATED | PASS |
| Kit | AUTOMATED | AUTOMATED | AUTOMATED | AUTOMATED | AUTOMATED | PASS |

Live confirmation of each cell: **NOT TESTED** this session.

---

## Shift boundary

| Check | Result |
|-------|--------|
| Worked hours reset on EnterOnShift | AUTOMATED (API + prior source) |
| Targets persist | AUTOMATED |
| Priorities persist | AUTOMATED |
| Commute not accrued | AUTOMATED (`TickPrioritySystem` OnShift-only) |
| Overtime / rescue counter corruption | NOT TESTED live |

---

## Still requiring manual Game-view verification

1. OFF stop feel on Haul / Excavate / Refine / Supports (≤1–2 ticks)  
2. Rescue interrupt during P1 + under-target work; resume after  
3. Rescue OFF mid-carry abort + casualty release  
4. Hour meters: dig-only vs idle excavator; no SEEK haul accrual  
5. Same-priority multi-task thrash over several game hours  
6. Exclusive claim: two workers, one excavator/hauler; loser re-resolves  
7. Distance soft vs Priority 1 far target  
8. UI LMB/RMB/±0.5h/CLEAR → WorkerRuntime + next Update behavior  
9. Selection spam while crew works  
10. Death/incap mid-station → immediate reclaim by another worker  

---

## Architecture risks (unchanged policy)

- Soft station claim + resolver both write ActiveTaskId — MinCommit/margin required  
- Accrual still depends on host activity labels / work kinds — rename risk  
- Deficit can outweigh one priority step with large targets — intentional  
- Analyse ON can keep Prospecting host allowed while Prospect OFF  

---

## Lock decision

**NOT LOCKED.**

Authority integration for OFF, host gates, availability invalidate, dead claims, Rescue OFF, and hour-honesty is in place and automated-checked. Full behavioral proof still needs the live matrix above.
