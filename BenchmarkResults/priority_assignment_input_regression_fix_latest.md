# Priority Assignment / Input Regression Fix

**Generated:** 2026-09-14  
**Scope:** Critical regression after hard-band fix — workers showed `Unassigned · MEALS` / lost specializations; player orders failed. Preserve hard bands; fix yield vs unassign.

---

## Exact root cause — Unassigned state

`SyncHostToActivePriorityTask`, `YieldHostIfVoluntaryTaskBlocked`, and `YieldHostIfAssignedStationBlocked` called **`TryUnassignJob`**, which:

1. Clears host bind  
2. **`_assignments.Unassign(workerId)`** — destroys persistent JobType  

Hard-band HostAllows often returned false when `ActiveTaskId` was a different station (e.g. Meals P4 when Excavate paint unavailable, or Haul P1 while Refiner). Sync then **Unassigned** the worker and tried `TryAssignJob` to the wanted station. If that seat was taken (Kit on Steward, Kowalski on Hauler), assign failed → **`Unassigned · MEALS` / `Unassigned · HAUL`**.

Cascaded across the crew whenever thin/available camp or haul tasks won the band over unavailable home work.

**Hard bands were not wrong.** Treating “not executing this priority task” as “clear persistent assignment” was wrong.

---

## Exact root cause — player orders not working

**Same regression, not a separate input bug.**

Excavation paint / prospect keys gate on `SelectedJobIs(JobType…)` → assignment JobType. With everyone Unassigned, Mara was not Excavation → paint/route commands no-ops.

PRIORITIES panel only `Block`s its own Rect (existing `_hudBlockers`). Outside the panel, world input was fine once assignments exist. No evidence of full-screen Event.Use from priorities.

---

## Temporary yield vs persistent assignment

| Action | Before (broken) | After (fixed) |
|--------|-----------------|---------------|
| Priority forbids home host tick | `TryUnassignJob` | `LeaveHostForPersonTask` / `TemporaryYieldHostForPriority` |
| Assignment JobType | Cleared | **Preserved** |
| Host executor | Stopped | Stopped (`PriorityAllowsHostTick` / HostAllows) |
| Soft claim | Could Unassign then fail | Fills **vacant** seats only (no steal → Unassign) |
| Dead/incap | Unassign (kept) | Unassign (kept) |

Card expectation: `Refiner · HAUL` (assignment + ActiveTask), not `Unassigned · HAUL`.

Hard-band rule preserved: valid P1 still beats P2–P4; continuity does not protect lower bands.

---

## Files changed

- `FreeMovementSocketMapRunner.cs` — `TemporaryYieldHostForPriority`, SyncHost rebind-only, removed priority `TryUnassignJob`, soft claim vacant-only  
- `PrioritySystemV12FullAuthorityAudit.cs` — regression guards  

Hard-band resolver (`WorkPriorityResolver`) unchanged this pass.

---

## Live Game-view results A–K

| ID | Check | Result |
|----|-------|--------|
| A | Six persistent assignments | **NOT TESTED** this session — code restores yield semantics; restart Play Mode to re-bootstrap if already Unassigned mid-session |
| B | Worker selection | **NOT TESTED** |
| C | Excavation paint | **NOT TESTED** (depends on A) |
| D | Prospecting controls | **NOT TESTED** |
| E | Priority panel vs world input | **AUTOMATED** panel Block scoped; live **NOT TESTED** |
| F | Elena Haul1/Refine2 leaves refine | **AUTOMATED** HostAllows + LeaveHost; live **NOT TESTED** |
| G | Elena remains Refiner while Haul ActiveTask | **AUTOMATED** assignment preserved; live **NOT TESTED** |
| H | Return to refine when haul gone | **AUTOMATED** SyncHost rebind when HostAllows; live **NOT TESTED** |
| I–K | No teleport / snap / duplicate host | **NOT TESTED** (uses existing LeaveHost paths) |

**Do not mark LIVE PASS.** Restart Play Mode (or re-assign from DEV) if the session already wiped assignments, then verify A–K.

---

## Remaining limitations

- Exclusive hosts: with vacant-only soft claim, Elena may show `Refiner · HAUL` while Kowalski keeps the Hauler seat until it is free — she will not steal his assignment (avoids Unassign). Cross-job haul on an occupied seat still needs a free seat or player reassignment.
- Mid-session Unassigned crew is not auto-healed; restart Play Mode rebinds Lewis→Kit defaults.
- Soft claim no longer steals living occupants (intentional).

---

## Result

**Root cause fixed:** priority authority now **temporary-yields** hosts and keeps persistent JobType. Hard bands retained. Player-order failure was assignment loss, not a separate input redesign.
