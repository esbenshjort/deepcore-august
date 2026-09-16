# Priority System V1

**Generated:** 2026-09-13  
**Scope:** Universal RimWorld-style priorities. Jobs = specialization, not permission. Existing systems create work; Priority System selects who/order.

---

## Architecture added

| Layer | Role |
|-------|------|
| `WorkTaskRegistry` | Shared task definitions (15 tasks, 4 categories) |
| `WorkerPriorityPrefs` on `WorkerRuntime` | Per-worker priority 1–4/OFF + hour targets + shift worked hours |
| `WorkAvailabilityContext` | Probes existing hosts/infra/collapse/camp — **does not create work** |
| `WorkPriorityResolver` | Scores candidates (priority ≫ deficit ≫ suit ≫ distance ≫ condition ≫ continuity) |
| `WorkPriorityDirector` | Eval interval, anti-thrash apply, accrual helper |
| Runner `TickPrioritySystem` | Resolve → dispatch side-effects → soft station claim |
| `HudPopupKind.Priorities` | Player PRIORITIES grid UI |

Hard personal overrides (incap, toilet, injury return, seeking care, claustro retreat) remain **outside** the priority list via `WorkPriorityResolver.IsHardBlocked`.

---

## Task registry

**EMERGENCY:** Rescue, Treat Injuries  
**INFRASTRUCTURE:** Clear Debris, Install Supports, Install Lighting, Repair Equipment  
**PRODUCTION:** Excavate, Prospect, Analyse Survey Data, Haul Materials, Refine Ore  
**CAMP:** Prepare Meals, Clean Camp, Maintain Hygiene, Tend Camp Systems  

Each definition: Id, DisplayName, Category, DefaultPriority, EmergencyCapable, SupportsHourTarget, RequiresTool/Station, PlayerOrderDriven, Tooltip, FamiliarJob (**nudge only**).

---

## Task availability integration

| Task | Probe source |
|------|----------------|
| Rescue | `WorkerRescue.NeedsRescue` |
| Treat | NeedsCare / SeekingStewardCare (not incap) |
| Clear Debris | `TunnelCollapseSystem` blocking fields |
| Supports / Lighting | `MineInfrastructure.TryPickNext*` |
| Repair | Excavator overheat / engineer repairing |
| Excavate | Paint plan pending / dig goal (**player-painted only**) |
| Prospect | `ProspectorWorkMode != Manual` |
| Analyse | `ProspectorAnomalyAnalyst.HasWork` |
| Haul | `LoosePile.LiveCount` |
| Refine | `RefinerPerson.CanFetchOre()` |
| Camp duties | `CampLifeState` hygiene / soft availability |

---

## Resolver logic

1. Skip hard-blocked workers  
2. Skip priority **OFF**  
3. Skip unavailable probes / physical can't  
4. Score = player priority weight (dominant) + hour deficit + suitability + distance + condition + continuity  
5. Emergency Rescue/Treat add large boost when urgency high  

**Player priority is authoritative** unless OFF or impossible.

---

## Emergency rules

- Rescue: existing Universal Rescue; priority OFF refuses voluntary accept; ActiveTaskId set on begin; hours accrue  
- Treat: `StewardWoundCare.TryTend` at camp (any worker — not JobType-locked)  
- Minor Fit injuries without NeedsCare do not create treat emergencies  

---

## Hour-target rules

- Optional per task (`SupportsHourTarget`)  
- 0.5h steps; 0 = no target  
- Deficit boosts score; completed target applies mild penalty (still eligible)  
- Reset on `EnterOnShift` via `ResetShiftAccumulation`  
- Targets persist; worked hours reset each shift  

Defaults: Viktor Supports 4h, Lighting 3h, Repair 1h.

---

## Anti-thrashing

- `MinCommitGameHours` = 0.35h hold on current task  
- Marginal same-priority swaps ignored  
- Emergencies interrupt immediately  
- Station claim continuity bonus + min-commit on current operator  

---

## Files changed / added

**Added:**  
`WorkTaskRegistry.cs`, `WorkerPriorityPrefs.cs`, `WorkAvailability.cs`, `WorkPriorityResolver.cs`, `WorkPriorityDirector.cs`, `PrioritySystemV1Audit.cs`

**Modified:**  
`WorkerRuntime.cs`, `CampLife.cs` (`PriorityDutyActive`), `WorkerRescue.cs` (shared task ids), `RefinerPerson.cs` (`CanFetchOre`), `FreeMovementSocketMapRunner.cs` (tick, UI, claim, roster, summary), `Editor/WorkerV11LockAuditMenu.cs`

---

## Existing systems reused

Universal Rescue, StewardWoundCare, MineInfrastructure, Excavator paint plan, LoosePile, Refiner stockpiles, Prospector analyst, CampLife, LeaveHostForPersonTask / PersonTaskAuthority, TryAssignJob, assignment reservation, DigHoodLog.

**No** parallel rescue/treatment/excavation creators.

---

## Profession permission checks found / removed

| Found | Disposition |
|-------|-------------|
| Hauler-only debris clear | Still opportunistic; also any worker via Clear Debris dispatch; respects OFF |
| Hauler/Engineer-only rescue | Already removed in Universal Rescue V1; Priority OFF gate added |
| Task list hidden by JobType | **Never** — registry is universal |
| FamiliarJob on definitions | Efficiency nudge in suitability only |

---

## UI implementation

- Tool strip **PRIORITIES** (player-facing)  
- Grid: rows = tasks (category separators), columns = workers  
- LMB cycle 1→2→3→4→OFF→1; RMB reverse  
- Cell shows number or OFF + optional `Nh` target; active • marker  
- Detail: priority, target ±0.5h, CLEAR TARGET, AVAILABLE/NO WORK/ACTIVE, suitability why  
- Roster: `Job · TASK` short label  
- Yesterday overlay: light per-worker worked/target lines  
- DEV: **PRIO DIAG** strip button  

---

## Current limitations

| Area | Status |
|------|--------|
| Station claim / auto-reassign | Soft; may fail if `TryAssignJob` blocked (e.g. scan in progress) |
| Prospect availability | Only when non-Manual mode — may under-detect idle prospect opportunity |
| Camp meals/clean | Soft-always probes; execution mostly via Steward station claim |
| Multi-worker same task | One exclusive station per JobType |
| PriorityDuty return path | Partial (`RescueReturning` reuse) — some duties rely on station claim instead |
| Save beyond session | In-memory on `WorkerRuntime` (no new save format) |
| Playmode full matrix | Needs manual verification |

---

## Tasks not fully connected (resolve + UI yes; executor thin)

- Maintain Hygiene / Tend Camp Systems — availability soft; little dedicated executor beyond Steward claim  
- Analyse — availability yes; desk work still on prospector host when claimed  
- Repair — probe yes; engineer FSM when Engineering claimed  

Fully stronger paths: Rescue, Clear Debris, Treat, Excavate/Haul/Refine/Supports/Lighting via station claim + existing hosts.

---

## Playmode results

| Check | Result |
|-------|--------|
| Universal task list / no JobType permission | **PASS** (code + offline audit) |
| Priority UI present | **PASS** (source) |
| Hour targets / shift reset | **PASS** (code) |
| Rescue OFF gate | **PASS** (code) |
| Excavate never invents cells | **PASS** (probe = paint only) |
| Live force tests A–K | **UNKNOWN / NEEDS PLAYMODE** |

Menu: `DeepCore/Diagnostics/Run Priority System V1 Audit`

---

## Result

**Priority System V1 shipped as selector layer over existing work creation.**  
Workers share one task list; specialization presets are editable defaults only.
