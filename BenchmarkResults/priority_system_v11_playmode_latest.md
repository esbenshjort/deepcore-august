# Priority System V1.1 — Playmode Validation + Integration Fixes

**Generated:** 2026-09-13  
**Scope:** Prove Priority V1 end-to-end; fix only verified integration failures. No new tasks/UI redesign/rebalance.

**Read:** `priority_system_v1_latest.md`, `universal_rescue_integration_v1_latest.md`

---

## A–K validation table

| ID | Check | Result | Notes |
|----|-------|--------|-------|
| **A** | Universal task list (6 workers × 15 tasks) | **PASS** | Registry shared; prefs register all ids; no JobType hide |
| **B** | Priority authority / cross-specialization | **PASS** (code) / **PARTIAL** (live) | Lewis may set Refine P1; Viktor Haul P1; station claim uses score not JobType. Live Game-view swap still operator-confirm |
| **C** | OFF | **PASS** | OFF skipped in resolver; Hauler clear respects Clear Debris OFF; Rescue OFF blocks accept |
| **D** | Hour targets | **PASS** (fixed) | Accrual now only while `IsActuallyPerformingPriorityTask`; deficit/reset/persist verified offline |
| **E** | Emergency interruption | **PASS** (code) / **PARTIAL** (live) | Universal rescue yield + person authority unchanged; ActiveTaskId=Rescue; host `CanPerform` false during duty |
| **F** | Universal rescue regression | **PASS** | Lewis→Kit `CanAcceptRescueDuty`; no JobType reintroduction; Priority OFF gate only |
| **G** | Anti-thrashing | **PASS** (fixed) | MinCommit 0.35h + same-priority score margin 35; removed broken `score < 40` filter |
| **H** | Excavation authority | **PASS** | Probe = paint pending / dig goal only; `PlayerOrderDriven`; never invents cells |
| **I** | Selection independence | **PASS** | `TickPrioritySystem` iterates full crew; pick/rescue ignore `_selectedWorkerId` |
| **J** | Movement / host authority | **PASS** (code) / **PARTIAL** (live) | LeaveHost + PriorityDuty lifecycle return; CrewWorldPos person-authority; force-test in Game view recommended |
| **K** | UI | **PASS** | PRIORITIES player strip; LMB/RMB; ±0.5h; CLEAR; active •; no redesign this pass |

**Overall:** Integration fixes applied. Full Game-view force matrix still recommended for E/J live timing; offline + source invariants **PASS**.

Menu: `DeepCore/Diagnostics/Run Priority System V1.1 Playmode Audit`

---

## Thin executor results

| Task | Opportunity exposed? | Resolver sees? | Any worker accept? | Physical execute? | Completes? | Hours accrue? | Resume? |
|------|----------------------|----------------|--------------------|-------------------|------------|---------------|---------|
| **Prepare Meals** | Yes if meal not served tonight | Yes | Via Steward station claim | Steward PickDuty / PreparingMeal | Yes (existing meal flow) | Only while Steward non-idle meal/kitchen | Yes |
| **Clean Camp** | Yes if Hygiene01 &lt; 0.55 or cleaning active | Yes | Steward claim | Steward CleaningCamp | Yes | While cleaning | Yes |
| **Maintain Hygiene** | **No** (honest) | No / unavailable | N/A | None — toilet is hard override | N/A | No | N/A |
| **Tend Camp Systems** | Only if Steward AfterMealCleanup / KitchenDuty active | Yes when active | Steward claim | Existing steward duty | Yes when duty runs | While duty active | Yes |
| **Prospect** | Only player-created work | Yes when available | Prospecting station claim | Existing Prospector modes / scanner setup | Yes | While mode≠Manual or scanner setup | Yes |

**Not faked:** Hygiene and idle Manual Prospect stay **NO WORK** rather than inventing busywork.

---

## Prospect Manual-mode behavior

**Meaning of Prospect priority in Manual mode:**

- **Manual + no scanner assignment + no non-Manual mode** → availability = **None** (`Manual — no player prospect order`).
- Priority System does **not** place scanners, start surveys, or invent dig/scan orders.
- When the player places a scanner / sets SurveyNearby / AssistExcavator / Investigate / scanner travel-setup → Prospect becomes **AVAILABLE** and the resolver may claim the Prospecting station for the highest Prospect-priority eligible worker.
- Analyse Survey Data remains a **separate** task driven by `Analyst.HasWork`.

This preserves player-authoritative Prospector workflow.

---

## Bugs found → fixes made

| Bug | Fix |
|-----|-----|
| Camp meals/hygiene/systems always `Yes` → fake work + thrash + false hours | Honest probes: meal only if not served; clean if dirty; hygiene = None; camp sys only on real steward duty |
| Hours accrued while idle on station with ActiveTask set | `IsActuallyPerformingPriorityTask` gate before Accrue |
| Same-priority anti-thrash used meaningless `score < 40` | Resolver same-priority **margin 35** vs current task score |
| PriorityDuty could stick after debris gone | `TickPriorityDutyLifecycle` releases + `RescueReturning` |
| Prospect Manual under-documented / scanner setup missed | Scanner assignment/setup counts as Prospect; tooltip + report |
| DEV PRIO DIAG too thin | Selected-worker score breakdown (Prio/Def/Suit/Dist/Cond/Cont/Emg), performing flags, crew list |

---

## Files changed

- `WorkAvailability.cs` — honest camp + Prospect probes; `MealServedTonight`
- `WorkTaskRegistry.cs` — Prospect / Hygiene / Camp Sys tooltips
- `WorkerPriorityPrefs.cs` — last score breakdown fields
- `WorkPriorityResolver.cs` — `WorkScoreBreakdown`, same-priority margin
- `WorkPriorityDirector.cs` — store breakdown; meal flag bind; simplify thrash
- `FreeMovementSocketMapRunner.cs` — performing gate, duty lifecycle, DEV diag
- `PrioritySystemV11PlaymodeAudit.cs` — **added**
- `Editor/WorkerV11LockAuditMenu.cs` — menu entry

---

## Unresolved limitations

- Live Game-view A–K force tests (collapse, paint, UI at scale) not executed in this agent session
- Maintain Hygiene remains a registry row with **no executor** (by design until real work exists)
- Soft station claim can still fail if `TryAssignJob` blocked (e.g. scan in progress)
- One exclusive host per JobType — concurrency unchanged
- Session-only prefs (no new save format)

---

## Architecture risks

- Station claim + resolver both drive ActiveTaskId — keep MinCommit / margin to avoid fight
- Accrual honesty depends on host activity labels (`IDLE` string checks) — brittle if labels rename
- Claiming Steward for meals all day until `MealServedTonight` is intentional but strong for Kit P1 Meals

---

## Result

**V1.1 integration fixes landed.** Thin executors made honest; hour accrual and anti-thrash corrected; Prospect Manual semantics documented; DEV diagnostics expanded. Remaining live Game-view force confirmation for E/J timing.
