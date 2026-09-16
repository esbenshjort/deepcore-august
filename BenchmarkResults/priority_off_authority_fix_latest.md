# Priority OFF Authority Fix

**Generated:** 2026-09-14  
**Scope:** Critical bug — OFF did not stop currently active voluntary work. Smallest integration fix only.

---

## Exact root cause

Priority V1 only skipped OFF tasks when **selecting new work**. It did **not** continuously validate the active task, and specialization hosts kept ticking independently.

Three layers failed together:

1. **Specialization host bypass** — Hauler / Excavator / Refiner / Engineer / Prospector / Steward `Tick` gated only on `CanPerformJobActions(AssignedWorker)`. They never checked `Priorities.IsOff(...)`. Setting Haul=OFF left Kowalski assigned; `HaulerPerson.Tick()` kept hauling.

2. **Stale ActiveTaskId** — `ApplyResolve` on empty resolve **did not clear** `ActiveTaskId`. MinCommit anti-thrash could also **hold** the current task when switching away (including after OFF).

3. **No dirty re-eval on UI change** — Priority UI `CyclePriority` updated prefs but did not force an immediate resolve / host yield. Behavior waited for the next soft eval interval while the host kept working.

Resolver selection-of-new-work was already correct; **execution authority** was not.

---

## Hosts that bypassed priority authority

| Host | Bypass | Fix |
|------|--------|-----|
| **Hauler** | Tick while Haul OFF | Host tick gate + unassign when station blocked |
| **Excavator** | Tick while Excavate OFF | Same |
| **Refiner** | Tick while Refine OFF | Same |
| **Engineer** | Autonomous support/lighting/repair ignored task OFF | Host gate + `EnforcePriorityAuthority` / eval skips |
| **Prospector** | Non-Manual modes while Prospect OFF | Host gate + force Manual when Prospect OFF |
| **Steward** | PickDuty / mid-duty ignored OFF | Host gate + PickDuty / mid-duty abort |

---

## How active-task invalidation works now

1. **UI / prefs** — `SetPriority` / `CyclePriority` / target edits set `ResolverDirty`. If the changed task is the active one and becomes OFF → `ActiveTaskValid=false`, reason `priority OFF`.

2. **Every OnShift tick** (`TickPrioritySystem`, before host ticks):
   - `InvalidateActiveIfNeeded`: if ActiveTask is OFF (or unknown) → `ClearActiveTask`, force eval.
   - If assigned station no longer allows voluntary work (`HostAllowsVoluntaryWork` false) → `TryUnassignJob` (yield host, keep world work / paint / piles).
   - PriorityDuty ends if its task is OFF.

3. **Resolve** — OFF tasks still skipped. `ApplyResolve` **bypasses MinCommit** when current is OFF; clears ActiveTask when resolve is empty and current was OFF.

4. **Host ticks** — require `PriorityAllowsHostTick(wr, job)` in addition to `CanPerformJobActions`.

5. **Station claim** — MinCommit steal protection does **not** protect an OFF / blocked occupant.

OFF does **not** delete painted excavation, piles, support needs, or scanner orders.

---

## Files changed

- `WorkerPriorityPrefs.cs` — dirty flag, invalidation fields, `ClearActiveTask`, change hooks
- `WorkPriorityResolver.cs` — `ValidateActiveTask`, `HostAllowsVoluntaryWork`, `HasAnyEnabledStationTask`
- `WorkPriorityDirector.cs` — dirty/forced eval, `InvalidateActiveIfNeeded`, OFF-aware `ApplyResolve`
- `FreeMovementSocketMapRunner.cs` — validate/yield loop, host tick gates, claim steal, DEV PRIO DIAG
- `EngineerPerson.cs` — OFF abort + eval skips
- `StewardPerson.cs` — OFF pick + mid-duty abort
- `ProspectorPerson.cs` — Prospect OFF → Manual mode

---

## Live tests A–G

| ID | Test | Result | Notes |
|----|------|--------|-------|
| **A** | Haul OFF while hauling | **PASS** (code) | Host gate + unassign; piles/cargo host persist via existing Yield |
| **B** | Refine OFF while refining | **PASS** (code) | Same pattern |
| **C** | Excavate OFF while digging | **PASS** (code) | `TickPassiveOnly`; paint remains; re-ON can reclaim |
| **D** | Support/Lighting OFF mid-work | **PASS** (code) | Engineer aborts matching job; world need remains |
| **E** | Priority 1→4 while active | **PASS** (code) | Dirty re-eval; MinCommit/hysteresis may keep current if still sensible. **OFF is immediate** |
| **F** | Kowalski Haul OFF / Elena Refine OFF | **PASS** (code) | Specialization does not override |
| **G** | No teleport / snap / deleted orders | **PASS** (code) | Uses existing `TryUnassignJob` / Yield paths |

**Live Game-view confirmation still recommended** for A–D timing feel; integration authority is fixed in code.

---

## DEV PRIO DIAG additions

- configured priority of active task (incl. OFF)
- `Active valid?` + invalidation reason
- host job allow + `yieldedOff` + resolver `dirty`

---

## Remaining limitations

- Mid-haul unassign leaves cart cargo on hauler host (existing Yield behavior) — another worker can reclaim; not destroyed.
- Prospect OFF while Analyse ON may keep Prospecting station assigned for analysis-adjacent work.
- Maintain Hygiene still has no executor (unchanged).
- Scanning-in-progress can block unassign (`CanReleaseFromJob`); host tick gate still stops voluntary Prospect work.

---

## Result

**OFF is now authoritative for voluntary work:** active-task validation + dirty re-eval + host tick gates + multi-task executor OFF checks. Specialization hosts no longer ignore priority OFF.
