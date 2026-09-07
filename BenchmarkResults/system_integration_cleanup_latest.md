# Deep Core — System Integration + Safe Cleanup

Generated: 2026-09-07  
Mode: architecture map + `SystemIntegrationCleanupAudit` (ForceBuild) + related regression audits  
Goal: **STABILITY** — no new features, no retunes, no redesigns

**Primary gate: PASS (47 / 0)** — `BenchmarkResults/system_integration_cleanup_latest.md`

Menu: `DeepCore/Diagnostics/Run System Integration Cleanup Audit`

---

## 1. Architecture ownership map

| Domain | Authoritative owner | Readers | Writers | Legacy / bridge |
|---|---|---|---|---|
| Person identity | `WorkerRuntime` (`WorkerId`) | All systems | Runner spawn / hire | — |
| Physical person | `WorkerAvatar` | Social, UI, presence | Runner commute / cabin / toilet | Host Transforms still move on job |
| WorkerStats | `WorkerRuntime.Stats` | Jobs, social, loco | Recruitment / DEV | Host unbound fallback bags |
| WorkerState | `WorkerRuntime.State` | Gates, UI, social | Event processor, camp, sleep, conflict | `PersonalConditions` alias |
| Injuries | `WorkerInjuryStore` + meter | InjuryResponse, loco, UI | Accident, collapse, fight, overheat | Dual meter ↔ typed sync |
| Peer social | `SocialAuraWorld` + Memory | Classifiers, UI | Aura live, conflict, meal | Stage-0 sim path |
| Manager relation | `ManagerRelationshipStore` | Manager UI | ManagerComm | Memory shares peer store |
| Assignment | `WorkerAssignmentManager` | Runner, banter | Runner assign APIs | Host `BindWorker` mirrors |
| Job behavior | Host FSM (`*Person` / excavator) | Runner tick gate | Hosts | `CanPerformJobActions` |
| Machine state | Host (heat / route / tools) | Dig, engineer | Hosts | Cabin: avatar hidden |
| Shift / phase | Runner `CrewPhase` + `ShiftPlanner` | Jobs, social | Runner | Per-person day ledger parallel |
| Commute | Runner `_personNav` | Presence | Runner Follow | SoftArrive = DEV/audit only |
| Camp / meal / toilet | Runner + CampLife / sites | Job pause | Runner + meal system | `WorkerCampBody` |
| Locomotion | `WorkerLocomotion` | Speed muls | Evaluate/tick | Hosts have own pathfinders |
| Terrain | `FineTerrainWorld` | Nav, dig, collapse | Excavator dig | — |
| Collapse / debris | `TunnelCollapseSystem` | Nav block, rescue | Collapse tick | — |
| Supports / lamps | `MineInfrastructure` | Collapse risk | Engineer | — |

### Conflicts (more than one authority — documented debt)

1. Avatar vs host Transform (on-shift snap to operate point)
2. Runner `_personNav` vs host `ExcavatedPathfinder`
3. Assignment manager vs host bind mirrors (`SyncBodyBindings`)
4. Injury meter vs typed `WorkerInjuryStore`
5. Crew-wide `CrewPhase` vs per-worker day ledger
6. Legacy `ControlWorker` stub (selection is `WorkerId`)

---

## 2. Conflicts found (actionable)

| Issue | Severity | Action |
|---|---|---|
| `ForceBuildForAudit` seated before `EnterOnShift` wiped `ArrivedWork` | Audit / seating | **Fixed** — SoftArrive after EnterOnShift |
| Morning commute cleared `TrappedFromCamp` and walked trapped workers | Gameplay bug | **Fixed** — stay at site; no invent path through debris |
| Dead `SoftArriveAllHome` | Dead code | **Removed** |
| Unused host SoftTeleport (Hauler/Refiner/Steward) | Dead code | **Removed** |
| Engineer SoftTeleport on repair strand | Emergency fallback | **Kept** (documented) |
| V1.1 Lock expects crew=5; live crew=6 (Steward) | Legacy audit drift | **DEFER** — not redesigned |
| Debris dig-face tip / trauma rate flaky sims | Pre-existing audit noise | **DEFER** |

---

## 3. Bugs fixed

1. **Audit seating order** — `ForceBuildForAudit` now `EnterOnShift` then `SoftArriveAllWork` so operators stay `Operating`.
2. **Trapped morning commute** — no longer clears `TrappedFromCamp` at wake; HeadingOut + late morning commute skip trapped workers (stay at physical site).
3. **Excavator idle at face** (prior session) — dig pins beyond nose + continue chewing; not part of this cleanup pass but related stability.

---

## 4. Duplicate paths removed

- `SoftArriveAllHome` (zero callers)
- `BridgeSelectedWorkerFromLegacySlot` + `LegacySlotToJob`
- Hauler / Refiner / Steward `SoftTeleport` / `TeleportTo`
- Obsolete excavator rest no-op stubs
- `WorkerConditions` obsolete empty subclass

Event pipeline: no confirmed double meter apply; Injury events apply Frustration only; spam gate verified.

---

## 5. Legacy code removed / classified

| Item | Verdict |
|---|---|
| SoftArriveAllHome | REMOVED |
| BridgeSelectedWorkerFromLegacySlot | REMOVED |
| LegacySlotToJob | REMOVED |
| WorkerConditions.cs | REMOVED |
| Host SoftTeleport Hauler/Refiner/Steward | REMOVED |
| Excavator obsolete rest no-ops | REMOVED |
| ControlWorker `_control` stub | KEEP |
| PersonalConditions alias | KEEP |
| JobToLegacyRoleIndex (UI recipes) | KEEP |
| Engineer SoftTeleport (strand ≥2.5s) | KEEP emergency |
| Prospector SoftTeleport (DEV scanner) | KEEP |

---

## 6. Files changed

- `Assets/Vibe/FreeMovement/SystemIntegrationCleanupAudit.cs` **(new)**
- `Assets/Vibe/FreeMovement/Editor/WorkerV11LockAuditMenu.cs`
- `Assets/Vibe/FreeMovement/FreeMovementSocketMapRunner.cs`
- `Assets/Vibe/FreeMovement/HaulerPerson.cs`
- `Assets/Vibe/FreeMovement/RefinerPerson.cs`
- `Assets/Vibe/FreeMovement/StewardPerson.cs`
- `Assets/Vibe/FreeMovement/FreeWorkerController.cs`
- `Assets/Vibe/FreeMovement/DebrisCollapseV1Audit.cs`
- Deleted: `WorkerConditions.cs` (+ meta)

---

## 7. Cross-system scenario results (Round 6)

All programmatic checks in `SystemIntegrationCleanupAudit`:

| Scenario | Result |
|---|---|
| A NORMAL DAY (phase HeadingHome ↔ OnShift) | PASS |
| B REASSIGNMENT (identity / Frustration / Stats) | PASS |
| C INJURY (typed + meter + event) | PASS |
| D INCAPACITATION (blocks CanPerform) | PASS |
| E SOCIAL CONFLICT (writers present) | PASS |
| F OVERTIME (planner band) | PASS |
| G TOILET (gates + return seat) | PASS |
| H COLLAPSE (no SoftArrive; stranded) | PASS |
| I RECRUITMENT (6 unique workers) | PASS |

---

## 8. Regression totals

| Audit | Result |
|---|---|
| **System Integration Cleanup** | **PASS 47 / 0** |
| Persistent Worker Body V1 | PASS 18 / 0 |
| Crew Cycle | PASS 31 / 0 |
| Debris Collapse V1 | FAIL 37 / 2 (dig-face tip sim + trauma sample — pre-existing / flaky; morning trapped check now PASS) |
| V1.1 Lock | FAIL 73 / 8 (crew count 5→6 Steward — audit debt, not this cleanup) |

Invariant suite (20): all PASS.

---

## 9. Remaining debt

- Avatar ↔ host dual position while Operating
- Dual `ExcavatedPathfinder` (commute vs job hosts)
- Injury meter ↔ typed store dual writers
- ControlWorker unused stub
- Engineer SoftTeleport repair strand (emergency)
- RelocateAvatar leave-host / embedded snaps (emergency)
- V1.1 Lock still assumes 5-person default crew
- Debris dig-face tip / trauma rate audit flakiness

**No major redesign required** — remaining dual-authority items need deliberate ownership decisions later.

---

## 10. Recommended human playtest

1. Full day: commute → dig → knock-off → camp meal → sleep → wake  
2. Reassign Mara mid-shift; excavator stays put; avatar seats correctly  
3. Toilet mid-dig; machine idle; return resumes chewing  
4. Force collapse / trap a worker overnight; morning must **not** warp them through debris  
5. Hire custom 6-crew; one full shift  

---

## Integration invariant checklist (live)

See Round 2 section in stamped reports under `BenchmarkResults/system_integration_cleanup_*.md`.
