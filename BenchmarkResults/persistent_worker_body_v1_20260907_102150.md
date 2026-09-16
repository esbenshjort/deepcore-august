# Persistent Worker Body V1 — Report
Generated: 2026-09-07 10:21:50

## 1. Why duplicate bodies existed
Commute intentionally revealed `WorkerAvatar` while person-shaped
job hosts (`ProspectorPerson`, etc.) kept full `CrewVisualKit` Body
sprites. Result: host "corpse" at post + walking person.

## 2. Old ownership
Host MonoBehaviour owned both job FSM **and** the visible person.
`WorkerAvatar` was an off-duty commute shell (Hide while Operating).

## 3. New ownership
- **WorkerRuntime** — person identity + state
- **JobAssignment** — what they do
- **JobProvider/Equipment** — where/how (Transform + FSM; Body sprites off)
- **WorkerAvatar** — sole persistent visible person
- **Excavator** — machine stays; avatar enters cabin (Hide) / exits (Show beside)

## 4. Avatar spawn/despawn paths removed (behavior)
No second person Spawn added. Host Body sprites disabled at spawn and
kept off on seat/leave/toilet/injury/reassign. SoftArrive still DEV-only.

## 5. Excavator enter/exit
`ExcavatorCabin.Enter` — follow provider + Hide. `Exit` — ParkAt offset + Show.
Wired in SeatAvatarAtWork, BeginHeadingHome, toilet, injury, ParkAvatarLeavingJob.

## 6. Other five jobs
Same WorkerAvatar stays visible at host operate point; host Body hidden;
cart/equipment layers remain on host.

## 7–9. Reassignment / shift / injury
TryAssignJob still Yields + ParkAvatarLeavingJob (cabin Exit if excavator).
Physical commute lifecycle unchanged. Injury return exits cabin then limps.
Incapacitated: avatar stays visible at site; no healthy duplicate.

## 10. Duplicate invariant
ONE living WorkerId → ONE WorkerAvatar; host operator Body never co-visible.

## 11. Files changed
- `PersistentWorkerBody.cs` (new)
- `FreeMovementSocketMapRunner.cs`
- `PersistentWorkerBodyV1Audit.cs` (new)
- `Editor/WorkerV11LockAuditMenu.cs`

## 12. Remaining limitations
- No multi-frame climb animation; OffDuty suit during work.
- Role kit layers on avatar deferred.

# Persistent Worker Body V1 Audit
Generated: 2026-09-07 10:21:50

## Why duplicates existed
F0.5 commute revealed `WorkerAvatar` while person-shaped job hosts
kept their `CrewVisualKit` Body sprites visible — two people per role.

## Ownership (V1)
| Concept | Owner |
|---|---|
| Identity / stats / Soul / injury | WorkerRuntime |
| Assignment | WorkerAssignmentManager |
| Equipment / job FSM | *Person / FreeWorkerController |
| Visible person | WorkerAvatar (one per WorkerId) |
| Excavator cabin | Avatar Hide while Operating; machine stays |

## API / ownership checks
- PASS  IsMachineCabinJob(Excavation)
- PASS  IsMachineCabinJob(Prospecting) false
- PASS  ExcavatorCabin exit offset non-zero

## Enter / exit + operator body visibility
- PASS  SetOperatorBodyVisible(false) disables Body sprite
- PASS  SetOperatorBodyVisible(true) enables Body sprite
- PASS  Enter cabin hides avatar
- PASS  Enter sets FollowingProviderId
- PASS  Exit shows avatar
- PASS  Exit parks beside machine
- PASS  Exit clears following
- PASS  Host Body remains hidden after cabin cycle

## Reassignment / person ownership
- PASS  WorkerRuntime state survives job swap (same object)
- PASS  WorkerId stable for assignment key

## Duplicate-worker invariant (design)
- PASS  SpawnCrewAvatars is sole WorkerAvatar Spawn path (architecture) — Runner SpawnCrewAvatars; hosts no longer dual-show Body
- PASS  Person jobs keep avatar visible while Operating — SeatAvatarAtWork → Show; host Body off
- PASS  Excavation hides avatar while Operating — ExcavatorCabin.Enter
- PASS  Shift end / toilet / injury use cabin Exit or Show avatar — BeginHeadingHome / BeginToiletTrip / BeginInjuryReturnToCamp
- PASS  No SoftArrive on normal commute — prior commute regression lock

## Limitations
- Avatar stays OffDuty suit art while operating (no full role kit swap yet).
- Hauler cart / prospector cone remain on host Transform as equipment.
- Enter/exit is instant Hide/Show (no multi-frame climb animation).

**Result:** PASS  (18 passed, 0 failed)
