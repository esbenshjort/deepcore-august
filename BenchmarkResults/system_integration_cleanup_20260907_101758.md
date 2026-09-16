# Deep Core — System Integration + Safe Cleanup Audit

Generated: 2026-09-07 10:17:55
Mode: static source + ForceBuild live checks (not interactive Play Mode).
Goal: STABILITY — no new features, no retunes.

## ROUND 1 — Architecture ownership map

| Domain | Authoritative owner | Notes |
|---|---|---|
| Person identity | `WorkerRuntime` (`WorkerId`) | Stats/State/Injuries/CampBody nested |
| Physical person | `WorkerAvatar` | One per living WorkerId |
| WorkerStats | `WorkerRuntime.Stats` | Hosts bind, do not own |
| WorkerState | `WorkerRuntime.State` | Mutations via events + care/camp |
| Injuries | `WorkerInjuryStore` + meter | Policy: `InjuryResponse` |
| Peer social | `SocialAuraWorld` / Memory | Via `SocialAuraLiveSystem` |
| Manager relation | `ManagerRelationshipStore` | Separate axes; shared memory store |
| Assignment | `WorkerAssignmentManager` | Hosts are mirrors |
| Job behavior | Host FSM (`*Person` / excavator) | Gated by `CanPerformJobActions` |
| Machine state | Host (heat/route/equipment) | Not person identity |
| Shift / phase | Runner `CrewPhase` + `ShiftPlanner` | Per-person ledger parallel |
| Commute | Runner `_personNav` | SoftArrive = DEV/audit only |
| Camp/meal/toilet | Runner + `CampLife` / sites | `WorkerCampBody` flags |
| Locomotion formula | `WorkerLocomotion` | Hosts have parallel pathfinders |
| Terrain | `FineTerrainWorld` | Dig / collapse / infra read |
| Collapse/debris | `TunnelCollapseSystem` | |
| Supports/lamps | `MineInfrastructure` | Engineer places |

### Known authority conflicts (documented debt — not auto-fixed)
1. Avatar vs host Transform (on-shift snap to operate point).
2. Runner `_personNav` vs host `ExcavatedPathfinder`.
3. Assignment manager vs host `BindWorker` mirrors (SyncBodyBindings).
4. Injury meter vs typed `WorkerInjuryStore` (best-effort sync).
5. `CrewPhase` crew-wide vs per-worker day ledger.
6. Legacy `ControlWorker` stub (selection is WorkerId).

## ROUND 2 — Core invariant suite (20)

- PASS  1. One living worker = one avatar
- PASS  2. Identity/state survives reassignment — OK
- PASS  3. Assignment does not own person identity
- PASS  4. EnterOnShift documents no SoftTeleport hosts
- PASS  4b. BeginHeadingOut does not SoftArriveAllWork
- PASS  5. SoftArriveAllHome removed (no normal home teleport API)
- PASS  5b. SoftArriveAllWork only ForceBuild/audit
- FAIL  6. Seated audit crew OnShift Operating for excavator
- PASS  7. CanPerformJobActions incap gate in source
- PASS  7b. Live: incap blocks CanPerform
- PASS  8. Injury persists on WorkerRuntime store
- PASS  9. Sleep recovery applies WorkerRuntime.State
- PASS  10. Social Aura / conflict target WorkerId
- PASS  11. ManagerRelationshipStore persists Trust
- PASS  12. Shift start uses physical HeadingOut commute
- PASS  13. Toilet trip parks machine, does not ClearWorker
- PASS  14. Tunnel collapse / debris nav shared FineTerrainWorld
- PASS  15. Recruitment builds unique WorkerIds
- PASS  16. WorkerStateEvent spam gate exists
- PASS  17. SocialMemoryRecorder.IsMeaningful gate
- PASS  18. Injury/event clocks use WorkerStateClock.GameHours
- PASS  19. Snap/relocate uses tunnel / InBounds helpers
- PASS  20. SoftArrive labeled DEV/audit; ForceBuild seats only
- PASS  20b. BridgeSelectedWorkerFromLegacySlot removed

## ROUND 3 — Position / lifecycle classification

| Path | Class |
|---|---|
| SoftArriveAllWork via ForceBuildForAudit | DEV ONLY |
| SoftArriveAllHome | REMOVED |
| RelocateAvatar leave-host / embedded | EMERGENCY FALLBACK |
| ExcavatorCabin Enter/Exit ParkAt | VALID |
| Toilet ParkAt door | VALID |
| Engineer SoftTeleport repair strand ≥2.5s | EMERGENCY FALLBACK (kept) |
| Host SoftTeleport Hauler/Refiner/Steward | REMOVED (unused) |
| Commute StepPersonCommute Follow | VALID |

- PASS  3. No SoftArriveAllHome remains
- PASS  3. Engineer SoftTeleport kept as emergency only
- PASS  3. No INVALID SoftArrive on BeginHeadingHome

## ROUND 4 — Event pipeline

- PASS  4. Injury event processor applies Frustration only (not second meter)
- PASS  4. Accident ApplyTypedInjury emits once then InjuryResponseHub
- PASS  4. Overheat: meter then typed record without ApplyTypedInjury double
- PASS  4. Spam gate blocks identical WorkBlocked double

## ROUND 5 — Legacy / dead code classification

| Item | Verdict |
|---|---|
| ControlWorker `_control` stub + DEV line | KEEP (debt) |
| BridgeSelectedWorkerFromLegacySlot | REMOVED |
| LegacySlotToJob | REMOVED |
| SoftArriveAllHome | REMOVED |
| WorkerConditions.cs | REMOVED |
| PersonalConditions alias | KEEP (compat) |
| JobToLegacyRoleIndex recipes | KEEP (UI sheets) |
| Host SoftTeleport Hauler/Refiner/Steward | REMOVED |
| Engineer/Prospector SoftTeleport | KEEP (emergency/DEV) |
| Excavator obsolete rest no-ops | REMOVED |

- PASS  5. WorkerConditions type file gone
- PASS  5. Hauler SoftTeleport removed
- PASS  5. Refiner SoftTeleport removed
- PASS  5. Steward SoftTeleport removed

## ROUND 6 — Cross-system scenarios

- PASS  A. NORMAL DAY — BeginHeadingHome → HeadingHome
- PASS  A. NORMAL DAY — can return OnShift
- PASS  B. REASSIGNMENT — Kowalski keeps Stats ref + Frustration
- PASS  C. INJURY — typed + meter + event path
- PASS  D. INCAPACITATION — blocks job actions
- PASS  E. SOCIAL CONFLICT — system present + death/fight writers
- PASS  F. OVERTIME — 10h band OVERTIME
- PASS  G. TOILET — trip gates CanPerform; returns SeatAvatarAtWork
- PASS  H. COLLAPSE — TunnelCollapse + stranded commute no SoftArrive
- PASS  I. RECRUITMENT — 6-seat crew materializes
- PASS  Persistent body API — excavator is cabin job
- PASS  Persistent body — HideOperatorBodies exists

## ROUND 7 — Safe cleanup applied this pass
- Removed dead `SoftArriveAllHome`
- Removed obsolete `BridgeSelectedWorkerFromLegacySlot` + `LegacySlotToJob`
- Removed unused SoftTeleport/TeleportTo on Hauler, Refiner, Steward
- Removed obsolete empty `WorkerConditions` type
- Removed excavator obsolete rest no-op stubs
- Kept Engineer SoftTeleport emergency strand path (documented)
- Kept ControlWorker stub / JobToLegacyRoleIndex / PersonalConditions alias

## ROUND 8 — Totals

- PASS: 46
- FAIL: 1
- **Result:** FAIL

### Blockers
- 6. Seated audit crew OnShift Operating for excavator

## Remaining debt
- Avatar↔host dual position authority while Operating
- Dual ExcavatedPathfinder (person commute vs job hosts)
- Injury meter ↔ typed store dual writers
- ControlWorker unused stub
- Engineer SoftTeleport on repair strand (emergency)
- RelocateAvatar leave-host snaps (emergency)

## Recommended human playtest
1. Full day: commute → dig → knock-off → camp meal → sleep → wake
2. Reassign Mara mid-shift; confirm excavator stays put, avatar seats
3. Toilet trip mid-dig; machine idle; return resumes
4. Force collapse debris; verify no SoftArrive home
5. Hire custom 6-crew and run one shift
