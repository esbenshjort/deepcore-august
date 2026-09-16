# Deep Core — Worker / Job / Task Architecture Audit

**Generated:** 2026-09-14  
**Mode:** AUDIT ONLY — no code changes, no refactor, no TaskDefinition implementation.

**Scope:** `Assets/Vibe/FreeMovement/` (+ Priority / assignment regressions documented in BenchmarkResults).

**Purpose:** Map how Deep Core works today so a future model  
`Worker → Task → Stats → Priority → Tool/Station` (JobType = specialization, not permission)  
can be evaluated against real ownership — not invented architecture.

---

## Executive verdict

Deep Core already has **three parallel authority layers**:

1. **Persistent specialization** — `WorkerAssignmentManager.JobType` (who “is” the Excavator / Refiner / …).
2. **Execution bind** — host `AssignedWorker` / `IWorkProvider` (who may run that machine *right now*; yieldable).
3. **Voluntary / emergency intent** — `WorkerPriorityPrefs.ActiveTaskId` (what Priority V1 says they should do).

Person-physical authority (`CampBody` duty flags + avatar) is a **fourth** override for rescue, toilet, PriorityDuty, care.

Recent Priority bugs (OFF ignored, P2 beating P1, refine under Haul ActiveTask, mass Unassigned, orders dead) were not “missing TaskDefinition.” They were **failures to keep these layers distinct** — especially treating “must not run this host now” as “Unassign JobType.”

A future `TaskDefinition` can **wrap** existing probes/gates/duty dispatch as adapters. Replacing hosts with definitions would be high-risk and unnecessary for a first seam.

---

## 1. Current worker authority map

### Overlap diagram (today)

```
WorkerRuntime (person bag)
├── Stats / State / Injuries / Locomotion     ← always person-owned
├── CampBody (toilet, meal, *Duty flags)     ← person-task physical authority
└── Priorities
    ├── Priority 1–4 / OFF / hour targets     ← player config
    ├── ActiveTaskId                          ← current task INTENT
    └── ResolverDirty                         ← force re-eval

WorkerAssignmentManager
└── JobType + ProviderId                      ← PERSISTENT specialization seat

IWorkProvider host (*Person / FreeWorkerController)
└── AssignedWorker                            ← EXECUTION bind (may be null while assigned)

WorkerAvatar / WorkerPresenceRegistry
└── Transform presence                        ← person body in world

CrewWorldPos                                  ← arbitrator: avatar vs host Position
```

### Per-object ownership

| Object | Owns | When authoritative | Can override | Depends on it |
|--------|------|--------------------|--------------|---------------|
| **WorkerRuntime** | Identity, Stats, State, CampBody, Priorities, Injuries | Always the person record | DEV sheet edits; death | Everything person-keyed |
| **WorkerState** | Stamina, focus, incap, NeedsCare, death | Live condition | Sleep, JobDemand, injury, collapse, care | CanPerform, hard-block, avatar tint |
| **WorkerCampBody** | Toilet/stomach + Rescue/PriorityDuty/care flags | Flags during person tasks | Runner lifecycle clearers | PersonTaskAuthority, CanPerform |
| **WorkerPriorityPrefs** | Bands, targets, worked hours, ActiveTaskId, dirty | Config always; ActiveTask after resolve/duty | Invalidate OFF/unavailable; duty/rescue writers | TickPrioritySystem, HostAllows, UI |
| **WorkerAssignmentManager** | `WorkerId ↔ JobType ↔ ProviderId` (1:1 job) | After bootstrap / TryAssign / soft claim / incap Unassign | Only explicit AssignInternal/Unassign | Operating state, soft claim, SelectedJobIs, familiarity |
| **Host AssignedWorker** | Who ticks the machine | While bound | Yield/Clear/LeaveHost without Unassign | Host Tick, formulas via Stats |
| **WorkerAvatar** | Transform presence, follow, hide | Person-task / commute / idle; synced from host when Operating | RelocateAvatar, SeatAvatarAtWork, cabin | CrewWorldPos person branch, camera, social |
| **CrewWorldPos** | Canonical world position query | Always the API others should use | Nothing — it *is* the override | Collapse, priority, rescue, care |
| **PersonTaskAuthority** | Predicate: avatar sole physical authority | Any CampBody duty/toilet/care/rescue flag | Flag clear | CrewWorldPos, GetPhysicalState, SyncMovingAssignedAvatars |
| **ActiveTaskId** | Current task intent string | After ApplyResolve / BeginPriorityDuty / rescue latch | ClearActiveTask paths | HostAllows, accrue, Dispatch, claim “want” |
| **ResolverDirty** | Force next resolve | After UI priority/target change | Cleared on ApplyResolve | ShouldEvaluate |
| **Soft station claim** | Fill **vacant** exclusive seats | After priority eval | Will not steal living occupant | TryAssignJob when vacant |

### Critical overlaps

| Conflict | Current rule |
|----------|--------------|
| Assignment vs host bind | Assignment can outlive bind (priority yield / person duty) — intentional |
| ActiveTask vs home JobType | Host may tick only if ActiveTask’s station matches host (or empty ActiveTask + enabled station tasks) |
| Soft claim vs specialist | Vacant seats only; no Unassign-to-steal |
| Dead/incap | `ReleaseUnavailableWorkerClaims` **does** Unassign (exception) |
| Multiple ActiveTask writers | Director, BeginPriorityDuty, rescue — can race |
| Stats | Person Stats win when bound; hosts keep fallback `_stats` when vacant |

---

## 2. Six job end-to-end traces

### Excavator (`FreeWorkerController`, JobType.Excavation)

| Stage | Path |
|-------|------|
| Assigned | `TryAssignJob` → `BindHost` → `BindWorker` |
| Available | Player paint → `PaintPlan.PendingDigCount` (`ProbeExcavate`) |
| Target | `SyncGoalFromPaintPlan` / accessible work front — never invents cells |
| Select | Resolver `task.excavate` + soft claim Excavation |
| Move | Machine `transform`; dig/access via `ExcavatedPathfinder`; cabin avatar |
| Execute | `Tick` dig strikes, `MiningDamage`, heat/stamina |
| Stats | RawPower, Lithology, SpatialGeometry, Finesse, Focus, SafetyProtocol, Rhythm, Determination, Composure, Toughness, Stamina |
| Complete | Paint pending empty |
| Interrupt | PriorityAllowsHostTick; `TickPassiveOnly`; LeaveHost; toilet/rescue |

### Prospector (`ProspectorPerson`, JobType.Prospecting)

| Stage | Path |
|-------|------|
| Assigned | BindWorker + field scanner NotifyAssigned |
| Available | Non-Manual WorkMode or scanner setup (`ProbeProspect`); Analyse via Analyst queue |
| Target | Survey/Assist/Investigate planners; scanner travel; desk analysis |
| Select | Soft claim Prospecting |
| Move | Host transform + pathfinder; avatar follows when Operating |
| Execute | Modes, radar/scan, scanner setup hours, analysis hours |
| Stats | Calibration/Focus (scan); Mechanics/HeavyLifting (setup); large analysis set |
| Complete | Mode idle / setup done / analysis conclude |
| Interrupt | Yield cancels travel; mid-`Scanning` blocks CanReleaseFromJob |

### Hauler (`HaulerPerson`, JobType.Hauling)

| Stage | Path |
|-------|------|
| Assigned | BindWorker (cart host) |
| Available | `LoosePile.LiveCount` or mid-haul `CargoCount` |
| Target | `FindTarget` over piles; PreferGold bias |
| Claim | `LoosePile.Claimed = true` (boolean, not WorkerId) |
| Move | Cart transform; `WorkerLocomotion` × track |
| Execute | Seek → PickUp → Return → Deposit |
| Stats | Performance: mostly locomotion/stamina; **haul math barely uses HeavyLifting** |
| Suitability | HeavyLifting, Logistics, Stamina (selection only) |
| Complete | Deposit → Seek |
| Interrupt | Yield drops pickup claim; **cargo persists on cart** |

### Refiner (`RefinerPerson`, JobType.Refining)

| Stage | Path |
|-------|------|
| Assigned | BindWorker (washer) |
| Available | `CanFetchOre` or mid wash/carry ActivityLabel |
| Target | Ore pick by RefinerPriority |
| Execute | Fetch → Carry → WashMachine → WaitWash |
| Stats | Suitability Chemistry/Focus/Finesse; **wash outcome does not read Chemistry** |
| Interrupt | Yield keeps washer; HostAllows gate |

### Engineer (`EngineerPerson`, JobType.Engineering)

| Stage | Path |
|-------|------|
| Assigned | BindWorker |
| Available | Infra support/lantern pickers; excavator overheat |
| Select | Critical support → lantern → preventative; repair preempts; TaskAllowed vs OFF |
| Move | Host nav; **SoftTeleport** if repair approach stuck |
| Execute | Build/install hours; repair timer |
| Stats | SpatialGeometry, Mechanics, Focus for support quality |
| Interrupt | EnforcePriorityAuthority aborts OFF kinds |

### Steward (`StewardPerson`, JobType.Steward)

| Stage | Path |
|-------|------|
| Assigned | BindWorker |
| Available | Hygiene, meal-not-served, NeedsCare, WorkKind |
| Select | Host `PickDuty` RNG among allowed; Treat also via PriorityDuty |
| Execute | Duty hours; `StewardWoundCare.TryTend`; hygiene drift; meals via CampMealQuality |
| Stats | Logistics/WorkRate clean; Empathy/Recovery tend; meal suite |
| Note | JobDemand catalog has **no ForSteward** (falls through Idle) |

---

## 3. What JobType actually controls

### Dependency matrix (A–I)

| Category | Meaning | Evidence (representative) |
|----------|---------|---------------------------|
| **A Permission / hard gate** | Blocks action if wrong/missing job | `CanPerformJobActions` (HeadingOut needs assignment); `SelectedJobIs` for paint/scan keys; `HostAllowsVoluntaryWork` (ActiveTask↔station); `CanReleaseFromJob` (scan in progress); `IWorkProvider.CanAssign` |
| **B Executor selection** | Which MonoBehaviour ticks | Host Tick gated on `AssignedWorker` by job; `GetBodyAssignedWorker`; cabin vs person body |
| **C Provider / station binding** | Seat reservation + bind txn | `WorkerAssignmentManager`; `TryAssignJob` / Bind / Clear / Yield; soft claim → vacant Assign |
| **D Stat / formula selection** | Which stats matter | `JobStatPreview` / JobDefinition relevant stats; `Suitability01` FamiliarJob nudge; JobDemand catalogs by job; **host formulas use AssignedWorker.Stats**, not JobType enum switch once bound |
| **E Work discovery** | Finding work | Mostly **not** JobType — probes on world/hosts; `StationJobForTask` maps task→station; FamiliarJob metadata |
| **F UI / presentation** | Labels, routing chrome | Roster `JobStatPreview.DisplayName`; SelectedJobIs; speech JobContext |
| **G Recruitment / defaults** | Starting seats | `BootstrapPrototypeDefaults`; hire `TargetJob`; ApplyCrewDefaults by **name** (soft P1, not JobType lock) |
| **H Debug / audit** | Diagnostics | Priority/rescue/assignment audits |
| **I Other** | Social pairing, events | Relationship job pairs; WorkerStateEvent JobType tag |

### Where removing JobType authority would break gameplay today

1. **Exclusive station seats** — one Hauler/Excavator/… body; assignment is how exclusivity is stored.
2. **Player order routing** — paint/scanner/gold keys require `SelectedJobIs(JobType)`.
3. **CrewWorldPos Operating branch** — host Position lookup by JobType.
4. **Soft claim + HostAllows** — station mapping assumes JobType seats.
5. **Cabin / provider visibility** — Excavation-specific.
6. **Bootstrap / hire** — prototype six seats.

JobType is **not** permission for Rescue / ClearDebris / PriorityDuty Treat — those already bypass JobType gates.

---

## 4. Task creation vs execution (15 tasks)

| Task | Creates | Exists | Discovers | Claims | Executes | Completes | Class |
|------|---------|--------|-----------|--------|----------|-----------|-------|
| Rescue | Collapse / incap | Incapacitated / BeingRescued | ProbeRescue / NeedsRescue | Mission pick | Any eligible person | Camp recover | **GENERIC** |
| Treat | Injuries / NeedsCare | Patient flags | ProbeTreat | Steward seat or PriorityDuty | Steward host or Duty+WoundCare | NeedsCare clear | **PARTIAL** |
| Clear Debris | TunnelCollapse fields | Fields | ProbeDebris | No exclusive seat | PriorityDuty ± Hauler auto-clear | Field gone | **PARTIAL** |
| Supports | Infra need | Candidate cells | ProbeSupports | Eng seat | EngineerPerson | TryBuildSupport | **JOB-SPECIFIC** |
| Lighting | Infra dark cells | Lantern candidates | ProbeLighting | Eng seat | EngineerPerson | TryPlaceLantern | **JOB-SPECIFIC** |
| Repair | Excavator overheat | IsOverheated | ProbeRepair | Eng seat | Engineer repair FSM | FinishRepair | **JOB-SPECIFIC** |
| Excavate | **Player paint** | PaintPlan | ProbeExcavate | Excavation seat | FreeWorkerController | Queue empty | **JOB-SPECIFIC** |
| Prospect | Player mode/scanner | WorkMode / scanner | ProbeProspect | Prospecting seat | ProspectorPerson | Mode/setup done | **JOB-SPECIFIC** |
| Analyse | Scan enqueue | Analyst queue | ProbeAnalyse | Prospecting seat | Analyst desk | Analysis done | **JOB-SPECIFIC** |
| Haul | Dig → LoosePile | Piles / cargo | ProbeHaul | Hauling seat + pile.Claimed | HaulerPerson | Deposit | **JOB-SPECIFIC** |
| Refine | Stockpile ore | Ore / wash mid | ProbeRefine | Refining seat | Refiner+WashMachine | Batch done | **JOB-SPECIFIC** |
| Meals | Evening need | !MealServedTonight | Probe meals | Steward seat | Steward + CampMeal | MealServed | **JOB-SPECIFIC** |
| Clean | Hygiene drift | Hygiene01 low | Probe clean | Steward seat | Steward clean | Hygiene up | **JOB-SPECIFIC** |
| Maintain Hygiene | — | — | Always None | — | Toilet = hard personal override | — | **NOT IMPLEMENTED** |
| Tend Camp Systems | Steward kitchen/cleanup | WorkKind | Probe systems | Steward seat | Steward duty | Duty end | **PARTIAL / thin** |

---

## 5. Stat ownership

| Work | Who decides relevant stats | Performance vs suitability |
|------|----------------------------|------------------------------|
| Excavate | MiningDamage + excavator FSMs | Performance on dig/heat; suitability also RawPower/Mechanics/Toughness |
| Prospect/Analyse | ScanFormulas, ScannerSetup, AnomalyInterpretation | Strong performance coupling |
| Haul | Almost none in FSM; Locomotion uses person meters | Suitability HeavyLifting/Logistics — **not** pickup math |
| Refine | WashMachine yield **ignores** Chemistry | Suitability only today |
| Engineering | SupportBuildQuality01 + coop | Mechanics/SpatialGeometry/Focus on build |
| Steward | Clean push, WoundCare, CampMealQuality | Split across host + camp systems |
| Rescue | WorkerRescue scoring/taxes | Performance-ish; JobType familiarity nudge only |
| WorkerRuntime | Does **not** generically score tasks | Holds Stats sheet; hosts/resolver read it |

**Implication for “TASK DEFINES RELEVANT STATS”:**  
Suitability can wrap `WorkPriorityResolver.Suitability01` / registry metadata easily.  
True performance formulas live **inside hosts** (and sometimes don’t match suitability). Wrapping = call existing functions; moving formulas out of hosts is a separate, larger project.

---

## 6. Movement authority

| Mode | Authority | Notes / risks |
|------|-----------|----------------|
| Commute | Avatar + person nav | Stranded: no emergency teleport (DEV soft-arrive exists) |
| Operating (most jobs) | Host `transform`; avatar follows / seated | SeatAvatarAtWork snaps to operate point |
| Excavator cabin | Machine + cabin hide | Exit RelocateAvatar if far |
| Toilet | Avatar; LeaveHost | Stall snap; return SeatAvatarAtWork |
| Camp / sleep | Avatar hide in house | Interior offsets |
| Rescue / PriorityDuty | Avatar (`PersonTaskAuthority`) | LeaveHost Relocate if >~0.85 from exit |
| Return to provider | Walk then SeatAvatarAtWork | Snap on rebind |
| Engineer repair stuck | **SoftTeleport** to stand | Known teleport |
| Hauler stuck | ForceNudgeTowardOpen | Soft |
| Idle WASD | Avatar direct | No pathfind |

**Historical risks still relevant:** dual host/avatar presence; RelocateAvatar; SeatAvatarAtWork; Engineer SoftTeleport; provider Tick continuing if CanPerform/PriorityAllows not gated (fixed for priority, still architecture-dependent).

---

## 7. Priority System integration (and why failures happened)

### Live path (current)

```
PRIORITIES UI → CyclePriority / hour edit → ResolverDirty
       ↓
TickPrioritySystem (before host ticks)
  InvalidateActiveIfNeeded
  TemporaryYieldHostForPriority (LeaveHost if !HostAllows)
  Resolve (hard bands) → ApplyResolve → ActiveTaskId
  SyncHostToActivePriorityTask (yield or rebind vacant home)
  DispatchPriorityTask (ClearDebris / Treat)
  TryClaimStations (vacant only)
       ↓
Host Tick: CanPerform ∧ PriorityAllowsHostTick
```

### Why architecture made the regressions possible

| Failure | Architectural reason |
|---------|----------------------|
| **OFF not stopping work** | Priority was a **selector**; hosts executed from AssignedWorker without reading OFF/ActiveTask |
| **P2 over P1** | Priority was a **soft score weight**, competing with deficit/continuity; MinCommit crossed bands |
| **Refine while ActiveTask Haul** | **Host ownership ≠ ActiveTask**; HostAllows fell through to “any refine task not OFF” |
| **Mass Unassigned** | “Cannot run host” was wired to **`TryUnassignJob`** (clears JobType), then soft claim failed |
| **Orders unavailable** | Same Unassign wipe — `SelectedJobIs` needs persistent JobType |

These are **dual-authority** failures, not missing catalog entries. Hard bands + TemporaryYield address selection vs execution without deleting specialization — still fragile if any path Unassigns for voluntary priority again.

---

## 8. Existing generic building blocks (reuse, don’t duplicate)

| Building block | Location |
|----------------|----------|
| Task catalog / ids | `WorkTaskRegistry`, `WorkerGenericTaskIds`, `WorkTaskDefinition` |
| Availability probes | `WorkAvailabilityContext.Probe` |
| Resolve / bands / suitability | `WorkPriorityResolver`, `WorkPriorityDirector` |
| Prefs / hours / ActiveTask | `WorkerPriorityPrefs` |
| Station mapping | `StationJobForTask` |
| Host subordination | `HostAllowsVoluntaryWork`, `PriorityAllowsHostTick` |
| Temporary yield | `LeaveHostForPersonTask`, `TemporaryYieldHostForPriority`, `YieldForReassignment` |
| Soft vacant claim | `TryClaimStationsByPriority` |
| Assignment txn | `TryAssignJob`, `WorkerAssignmentManager` |
| Person duty | `BeginPriorityDuty`, `DispatchPriorityTask`, `PriorityDutyActive` |
| Locomotion | `WorkerLocomotion`, `ExcavatedPathfinder` |
| Hard blocks | `IsHardBlocked`, CampBody flags, InjuryResponse |
| Rescue | `WorkerRescue`, TickUniversalRescue |
| Accrual honesty | `IsActuallyPerformingPriorityTask` |
| World pile claim | `LoosePile.Claimed` (Haul-specific) |

---

## 9. Safest future architecture assessment

### Proposed model

`Worker → chooses TaskInstance → TaskDefinition (requirements/stats/rules) → existing executor`

### Can TaskDefinition WRAP existing systems?

**Yes, as a façade / adapter layer. No, as host replacement.**

Today `WorkTaskDefinition` is already catalog metadata. Execution is hosts + PriorityDuty + Rescue. Wrapping a host *inside* a Definition object would duplicate AssignedWorker, Yield, cabin/cart state, and fight AssignmentManager.

### Smallest safe seam

1. Treat **`ActiveTaskId` (+ started time / phase)** as a proto-`TaskInstance` on the person.  
2. Keep **`WorkTaskDefinition`** as static registry; optionally add adapter methods: Probe / StationJob / AccruePredicate / BeginDuty / HostAllow.  
3. Keep subordination seam: **HostAllows + LeaveHost + vacant claim** — never Unassign for voluntary priority.  
4. Do **not** move MiningDamage / WashMachine / Hauler FSM into Definition in v1.

**Do not** introduce a second claim system or second position authority.

---

## 10. Haul as first migration candidate

| Question | Finding |
|----------|---------|
| Find targets | `HaulerPerson.FindTarget` over `LoosePile.All` |
| Claim | Boolean `LoosePile.Claimed` — not WorkerId-safe for multi-agent |
| Movement | Cart host transform + pathfinder + locomotion |
| Pickup/dropoff | Host FSM; cargo on cart |
| JobType checks | None as permission; Hauling is seat identity |
| Other WorkerRuntime on same executor? | Yes if BindWorker after vacant assign; soft claim won’t steal living occupant |
| Must not change casually | Cargo continuity on yield; pile claim semantics; exclusive cart; ActivityLabel accrual |

**Risk: HIGH**

Reasons: host-owned cargo identity; boolean pile claims; exclusive seat vs “everyone haul”; yield/reclaim edge cases; ClearDebris auto-coupled to hauler host; suitability ≠ performance.

**Not the safest first universal task** despite earlier assumptions. Prefer thin Steward/camp or Supports/Lighting after yield semantics are trusted.

---

## 11. Migration risk by task + recommended order

| Task | Risk | Order suggestion |
|------|------|------------------|
| Maintain Hygiene | **LOW** (noop contract) | 1 |
| Clean Camp | **LOW–MED** | 2 |
| Tend Camp Systems | **LOW–MED** | 3 |
| Supports | **MED** | 4 |
| Lighting | **MED** | 5 |
| Prepare Meals | **MED** | 6 |
| Treat Injuries | **MED** | 7 |
| Clear Debris | **MED** | 8 |
| Analyse | **MED** | 9 |
| Repair | **MED–HIGH** | 10 |
| Refine | **MED–HIGH** | 11 |
| Rescue | **HIGH** | 12 |
| Prospect | **HIGH** | 13 |
| Excavate | **HIGH** | 14 |
| Haul | **HIGH** | 15 (last) |

Order driven by: exclusive host coupling, player-order authority, cargo/machine state, person-duty maturity, and dual-writer ActiveTask risk — **not** by “haul feels generic.”

---

## 12. Architecture diagrams

### Current state (real)

```
WorkerRuntime (person)
    │
    ├── Priorities.ActiveTaskId ──────── intent (Priority V1)
    │         │
    │         ▼
    │   WorkPriorityResolver (hard bands)
    │         │
    │         ├─► HostAllows / PriorityAllowsHostTick
    │         ├─► TemporaryYield / LeaveHost  (keep JobType)
    │         ├─► DispatchPriorityDuty / Rescue
    │         └─► Soft claim vacant station
    │
    ├── AssignmentManager.JobType ────── persistent specialization
    │         │
    │         ▼
    │   IWorkProvider host (*Person / Excavator)
    │         │
    │         └─► Work target (paint / pile / ore / infra / camp)
    │                   │
    │                   └─► Host FSM + Stats formulas
    │
    └── WorkerAvatar ◄──► CrewWorldPos ◄──► Host.Position
              (PersonTaskAuthority forces avatar)
```

### Smallest safe evolution (proposed — not implemented)

```
WorkerRuntime
  └─ TaskInstance (promote ActiveTaskId + phase)     ← NEW thin field only
         │
         ▼
  WorkTaskDefinition + adapters                      ← WRAP probes/gates/duty
         │
         ▼
  Existing executors unchanged                       ← hosts / duty / rescue
         │
         └─ Subordination seam unchanged:
              HostAllows + LeaveHost + vacant claim
              JobType remains specialization seat
```

---

## 13. Architectural conflicts (do not “fix” in this doc)

1. **Three brains** — Assignment, host bind, ActiveTask — easy to conflate.  
2. **ActiveTask multiple writers** — director vs duty vs rescue.  
3. **Exclusive seats vs cross-task P1** — vacant-only claim → `Refiner · HAUL` may wait while Hauler seat occupied.  
4. **Suitability ≠ performance** — especially Haul/Refine.  
5. **False-available camp probes** historically caused thrash / Unassign cascades.  
6. **JobType still hard-gates player orders** — correct for tools today; conflicts with “JobType is never permission” if taken absolutely.  
7. **Steward JobDemand missing** — demand system incomplete for camp job.

---

## 14. Safest seam for TaskDefinition (summary)

| Do | Don’t |
|----|-------|
| Promote ActiveTaskId → TaskInstance metadata | Replace HaulerPerson / Excavator with Definition-owned FSM |
| Adapter table: Probe / AllowHost / Accrue / BeginDuty | Second position authority or second assignment manager |
| Keep LeaveHost for voluntary priority | Call TryUnassignJob for voluntary priority |
| Migrate thin Steward/infra tasks first | Start with Haul or Excavate |
| Wrap existing formulas behind adapters | Move MiningDamage/WashMachine in the first PR |

---

## Result

Accurate map of **today’s** Deep Core worker/job/task stack. Future universal Task architecture is feasible as a **thin intent + adapter seam** over existing hosts — not as a host rewrite. **Haul is HIGH risk / late migration.** No code was changed.
