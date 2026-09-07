# Worker Commute / Teleport / Map Bounds Regression

Generated: 2026-09-06

**Result:** PASS (architecture / root-cause fix). Full 6-worker × 3-cycle Play Mode matrix is manual — use MOVE DEBUG + `[WORKER RELOCATION]` log.

---

## 1. Exact teleport root cause(s)

| Cause | Mechanism |
|-------|-----------|
| **Primary** | `BeginHeadingOut` called `SoftArriveAllWork()` then `EnterOnShift()` — never set `CrewPhase.HeadingOut`, so morning walk never ran |
| **Secondary** | `EnterOnShift` → `AlignHostsToMorningPosts()` SoftTeleported excavator/prospector/hauler/refiner/engineer/steward every shift start |
| **Tertiary** | `StepPersonCommute` stuck timer soft-arrived people to destination; emergency timeouts SoftArriveAll* |
| **Quaternary** | Toilet finish snapped avatar onto operate point |

## 2. Exact Prospector locomotion root cause

Shared with crew — not Lewis-specific:

1. Morning SoftArrive + AlignHosts SoftTeleport placed prospector host / avatar inconsistently
2. Auto `TrySnapBodyOut` on every Tick fought pathfinding
3. Jobs gated only on `OnShift`, while SoftArrive skipped physical arrive — state said “working” while body was wrong
4. Camp props / toilet south of excavated floor → `IsTunnelOpen` false → CircleHitsSolid / stranded paths

Fix: same physical `HeadingOut`/`HeadingHome` commute + `ArrivedWork` gate for all six jobs; remove AlignHosts SoftTeleport and auto unstick.

## 3. Exact bottom-map / void root cause

Camp props (toilet ~cell Y9, fire ~Y15) sit **south** of the excavated half-oval floor (`floorY = StartY - 16` → cell Y26). Half-oval only carves **upward**. Unexcavated rock is not terrain-drawn past ~2.6 wall cells → near-black camera clear read as “out of map.”

**Fix:** `floorY = StartY - 34` + `ExcavateCampSouthPad()` rect under southern camp footprint.

## 4. Position-write paths found

Routine teleports (removed from normal flow):

- `BeginHeadingOut` SoftArriveAllWork
- `AlignHostsToMorningPosts` / `EnsureAllHostsOnOpenFloor`
- SoftArriveAllHome / SoftArriveAllWork on commute timeout
- Stuck soft-arrive in `StepPersonCommute`
- Toilet return operate snap

Retained locomotion / sync:

- `PersonAvatarTryStep` / path `Follow`
- Host Tick Step (excavator/prospector/hauler/…)
- `SyncMovingAssignedAvatars` after `ArrivedWork` (follow live host)
- `SeatAvatarAtWork` hide/follow only (no map jump when already at post)
- Rescue `SetCrewWorldPos` walking

Retained DEV/INIT:

- Spawn / ResetMap / `ForceBuildForAudit` SoftArrive (audit-only)
- `SoftArriveAll*` methods (DEV-tagged + relocation log)
- Host `SoftTeleport`/`TeleportTo` APIs (logged; not called by EnterOnShift)

## 5. Position-write paths removed / retained

| Path | Status |
|------|--------|
| SoftArrive on morning start | **Removed** from normal flow |
| AlignHosts SoftTeleport on EnterOnShift | **Removed** |
| Stuck soft-arrive | **Removed** (marks PATH BLOCKED) |
| Emergency SoftArrive | **Removed** (phase advances; late keep walking) |
| Toilet return snap | **Removed** (physical ToiletReturning walk) |
| SoftArriveAll* methods | **Retained** DEV/audit only + log |
| Host SoftTeleport APIs | **Retained** + relocation log |
| SyncMovingAssignedAvatars | **Retained** (post-arrival) |
| Spawn / Reset | **Retained** INIT |

## 6. 6-worker × 3-cycle test results

| Check | Result |
|-------|--------|
| Architecture source gates (`WorkerCommuteRegressionAudit`) | PASS |
| 6h / 8h / 10h / 12h × 6 workers live Play | **Manual** — Editor Play with MOVE DEBUG |
| Prospector physical both ways | Architecture restored (same `StepPersonCommute`) |

Live verification checklist:

1. Restart Play (regenerate map apron)
2. Watch START: crew walk camp → posts; SHIFT LIVE only after arrivals
3. Dig / scanner only after ArrivedWork
4. Knock-off: walk home; no SoftArrive log
5. Next morning: walk out again
6. Confirm camp floor under toilet/fire (no black void)
7. Watch for `[WORKER RELOCATION]` — should be rare (DEV/catastrophic only)

## 7. Files changed

- `FreeMovementSocketMapRunner.cs` — commute lifecycle, camp apron, toilet walk, MOVE DEBUG
- `CampLife.cs` — `ToiletReturning`
- `ProspectorPerson.cs` — SoftTeleport log; remove auto unstick
- `FreeWorkerController.cs` — TeleportTo log
- `WorkerRelocationLog.cs` — **added**
- `WorkerCommuteRegressionAudit.cs` — **added**
- `CrewCycleAudit.cs` — expectations updated

## 8. Remaining known exceptions

- `ForceBuildForAudit` / SoftArriveAll* for batch audits (logged)
- Catastrophic “embedded in solid” snap on wake/shift-end (logged)
- Small arrive settle within `CommuteArriveRadius` (~0.48) — not map teleport
- `SyncMovingAssignedAvatars` copies host transform after arrival (presence sync)
- Toilet leave uses exit offset near host if avatar was already synced
- Providers never commute (by design)
- Full automated Play Mode 6×3 matrix not executed in this batch (Editor lock)

---

STOP.
