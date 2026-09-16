# Universal Rescue Integration V1

**Generated:** 2026-09-13  
**Scope:** Integrate existing rescue into person-authority worker tasks. No parallel rescue system. No Priority UI. No collapse rebalance.

---

## Existing rescue code reused

| Piece | Role after V1 |
|-------|----------------|
| `TunnelCollapseSystem.FindIncapacitatedNeedingRescue` | Detects rescue need |
| `TunnelCollapseSystem.HasOpenTunnelPath` / `IsReachableFromCamp` | Access authority (debris blocks) |
| `TunnelCollapseSystem.FinalizeRescueAtCamp` | Camp handoff: clear incap, `NeedsCare`, `SeekingStewardCare`, banners, SocialMemory |
| `TunnelCollapseSystem.TryRescueTick` | Legacy/DEV fallback; now uses `WorkerRescue` stats + `FinalizeRescueAtCamp` |
| `TunnelCollapseSystem.RescueAssistHours` / `RescueCarrySpeedMul` | Base timing constants |
| `StewardWoundCare` / Steward `NeedsCare` pick | Unchanged treatment after handoff |
| Hauler opportunistic `TickClearance` | Still separate from rescue |

**New (thin, not parallel):** `WorkerRescue.cs` — eligibility, scoring, stat multipliers, `RescueMission`, `WorkerGenericTaskIds.Rescue` for future Priority System.

---

## Files changed

| File | Change |
|------|--------|
| `Assets/Vibe/FreeMovement/WorkerRescue.cs` | **Added** — universal task id, mission, stats, CanAccept (no JobType gate) |
| `Assets/Vibe/FreeMovement/CampLife.cs` | `RescueDutyActive`, `BeingRescued`, `RescueReturning` |
| `Assets/Vibe/FreeMovement/TunnelCollapse.cs` | `HasOpenTunnelPath`, `FinalizeRescueAtCamp`; `TryRescueTick` updated |
| `Assets/Vibe/FreeMovement/FreeMovementSocketMapRunner.cs` | Universal pick/yield/path/carry/return; host snap fixes |
| `Assets/Vibe/FreeMovement/UniversalRescueV1Audit.cs` | **Added** — offline eligibility audit |
| `Assets/Vibe/FreeMovement/Editor/WorkerV11LockAuditMenu.cs` | Menu: Run Universal Rescue V1 Audit |

---

## Previous JobType assumptions found (removed)

In `TickDebrisClearanceAndRescue` (pre-V1):

1. Rescuer **hard-locked** to Hauler assigned worker, else Engineer.
2. **8 world-unit** proximity gate (implicitly favored nearby Hauler/Engineer).
3. `TryRescueTick` + `SetCrewWorldPos` moved **avatars only** while Hauler/Engineer **hosts kept working**.
4. `CrewWorldPos` preferred excavator **machine** after `ClearIncapacitated` → snap-back risk.

**V1:** `PickUniversalRescuer` scores **any** living mobile crew member via `WorkerRescue.CanAcceptRescueDuty` / `ScoreCandidate`. JobType only adds a **familiarity nudge** (efficiency), never `return false`.

---

## How rescue task authority works now

```
incap / NeedsRescue
  → PickUniversalRescuer (any eligible worker; no JobType permission)
  → LeaveHostForPersonTask(casualty) + BeingRescued
  → LeaveHostForPersonTask(rescuer) + RescueDutyActive
  → Approach (person nav / IsTunnelOpen — no debris walkthrough)
  → Assist (stat-scaled hours)
  → Carry both avatars toward camp (person nav)
  → FinalizeRescueAtCamp → SeekingStewardCare
  → rescuer RescueReturning → BindHost + SeatAvatarAtWork
```

**Authority rules:**

- While `RescueDutyActive` / `BeingRescued` / `RescueReturning` / `SeekingStewardCare` / incap: **avatar position is sole physical authority**.
- `SyncMovingAssignedAvatars` **skips** those flags (and incap) — **no host snap**.
- `CanPerformJobActions` false during rescue duty — specialization yielded, assignment reserved.
- UI selection does not drive who rescues (`PickUniversalRescuer` uses crew list + scores).

**Future Priority System:** expose `WorkerGenericTaskIds.Rescue` (`"task.rescue"`). Same list for every worker; selection must not use JobType permission.

**Cooperation:** `RescueMission.ContributorIds` reserved; V1 uses one primary rescuer.

---

## Stats affecting rescue performance

| Stat | Effect |
|------|--------|
| HeavyLifting, RawPower, Stamina, Toughness | Carry speed |
| Empathy, Composure | Carry handling nudge; assist frustration relief on casualty |
| Empathy, Composure, Focus, Mechanics | Assist duration (skill ↓ time) |
| Agility, Balance, Focus, SafetyProtocol | Approach speed |
| Stamina, Toughness | Stamina tax while approaching/carrying |
| Injury consequences | Walk / load / manual muls (existing) |

Job familiarity nudge (Hauling > Engineering > Steward > Excavation > other): **score only**.

---

## Remaining limitations

- Still **one** active rescue mission at a time.
- No multi-worker coordinated clear+carry yet (architecture allows ContributorIds later).
- Debris clearance remains **separate** (excavator dig / Hauler proximity) — rescue waits on `ACCESS BLOCKED`.
- Does **not** invent rock excavation (paint authority unchanged).
- Natural collapse still rare (unchanged).
- Playmode force-collapse → full crew rescue matrix not auto-run in editor batch here — use DEV force + observe DigHoodLog, or menu audit for eligibility.

---

## Regression results

### Offline / source (this change)

| Check | Result |
|-------|--------|
| JobType permission removed from rescue pick | **PASS** (source) |
| Lewis/Mara/Kowalski/Elena/Viktor/Kit CanAccept API | **PASS** (audit script; run via DeepCore/Diagnostics/Run Universal Rescue V1 Audit) |
| Strong vs weak carry mul differs | **PASS** (audit script) |
| Incap cannot accept duty / NeedsRescue | **PASS** (audit script) |
| Finalize sets SeekingStewardCare | **PASS** (source) |
| Host snap skip for SeekingStewardCare / rescue flags | **PASS** (source) |

### Playmode (manual — expected checklist)

Use DEV: FORCE BLOCK / FORCE INCAP near dig face, then observe:

| Check | Expected |
|-------|----------|
| Any of Lewis…Kit can be selected as rescuer | Not JobType-rejected |
| Rescuer stops normal job | YieldHost + RescueDutyActive |
| Victim stays until carried | BeingRescued / avatar authority |
| No walk through debris | ACCESS BLOCKED + nav |
| No unauthorized excavation | StrikeCell paint rule intact |
| No teleport / no snap-back | PersonTaskAuthority |
| Steward care after handoff | SeekingStewardCare |
| Rescuer resumes assignment | RescueReturning → BindHost |

**Playmode matrix not executed in this agent session** (Unity Editor not batch-driven here). Mark: **UNKNOWN / NEEDS PLAYMODE TEST** for live force-collapse timing; architecture checks above are code-verified.

---

## Result

**Universal rescue integrated into existing collapse/injury/Steward stack.**  
Workers are generalists for rescue; jobs are specialization with yield, not permission.
