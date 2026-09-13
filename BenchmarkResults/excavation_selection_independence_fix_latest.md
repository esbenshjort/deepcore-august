# Excavation Selection Independence Fix

Generated: 2026-09-08

## Root cause

UI selection only routed **WASD input** via `digWasd` (correct). Excavator `Tick` still ran from **AssignedWorker + `CanPerformJobActions`**.

When another worker was selected, `digWasd` became **zero**. Dig continued on the autonomous paint-plan path. That path, when within `0.1` of the paint work-cell center, called `AdvanceRoute()` → `NotifyCellExcavated` **even if the tile was still solid**.

Result: plan cells were false-completed without `world.Damage`, so **tiles stopped excavating** while drill visuals could still show engagement (`IsActivelyDigging` against blockers).

This showed up on selection change because that is when players stop WASD-driving the excavator; the same false-complete could happen whenever unsupervised auto-dig sat on a near-face solid cell.

## Coupling found

| Location | Role | Selection-dependent? |
|----------|------|----------------------|
| `FreeMovementSocketMapRunner` `digWasd = … ctl.JobType == Excavation` | Player drive input only | Yes (intentional) |
| `FreeMovementSocketMapRunner` `_worker.Tick(digWasd)` gated by `CanPerformJobActions(_worker?.AssignedWorker)` | Job simulation | **No** — already correct |
| `FreeWorkerController.Tick` `dist < 0.1` → `AdvanceRoute` → `NotifyCellExcavated` | Plan consumption | **Bug** — false-complete when unsupervised |
| `SelectedJobIs` / `SetRouteVisible` | Paint preview / HUD | UI only (unchanged) |
| `FreeWorkerController` | Dig / Damage | No selection references |

## Fix applied

**File:** `Assets/Vibe/FreeMovement/FreeWorkerController.cs`

1. If near goal and paint work cell still blocks → **dig in place**, do not `AdvanceRoute`.
2. `AdvanceRoute` only `NotifyCellExcavated` when the work cell is actually clear.
3. Unsupervised Tick (WASD zero) always refreshes paint goal when pending cells remain.

**File:** `Assets/Vibe/FreeMovement/FreeMovementSocketMapRunner.cs`

- Clarified comments: all job hosts tick from AssignedWorker eligibility; selection only routes player input.

No balance / routing / width / visual redesign.

## Other jobs

Hauler / Engineer / Prospector / Refiner / Steward already tick from `CanPerformJobActions(AssignedWorker)` — not from `SelectedJobIs`. Same invariant.

## Regression

Audit: `ExcavationSelectionIndependenceAudit.cs`  
Scenario contract:

1. Paint route  
2. Start excavator  
3. Tiles damage via `StrikeCell` / `world.Damage`  
4. Select Lewis / Kowalski → `Tick` still runs (`digWasd=0`)  
5. Near solid work cell → dig, not false complete  
6. SHEET / SOCIAL / CAMP / SHIFT → UI only  
7. Return to Mara → plan reflects real excavation  

Source checks: **GREEN** (Tick not wrapped in `SelectedJobIs`; false-complete guards present).

## RESULT: GREEN
