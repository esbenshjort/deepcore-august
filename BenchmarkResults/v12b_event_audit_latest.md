# V1.2B Person-Targeted Worker State Events Audit

Generated: 2026-09-01 21:50:32

## A. Person targeting
PASS | Emit to Lewis applied — dFr=4
PASS | Only Lewis Frustration rose

## B. Frozen WorkerId across swap
PASS | Lewis still owns the Frustration delta after Mara takes Excavator
PASS | History event still WorkerId=Lewis

## C. Five independent workers
PASS | WorkerId 1 received ProgressSuccess
PASS | WorkerId 1 history has AuditC
PASS | WorkerId 2 received ProgressSuccess
PASS | WorkerId 2 history has AuditC
PASS | WorkerId 3 received ProgressSuccess
PASS | WorkerId 3 history has AuditC
PASS | WorkerId 4 received ProgressSuccess
PASS | WorkerId 4 history has AuditC
PASS | WorkerId 5 received ProgressSuccess
PASS | WorkerId 5 history has AuditC

## D. Unassigned receives events
PASS | Elena Unassigned
PASS | Unassigned Elena received Discovery
PASS | Unassigned Elena Morale rose

## E. Sleeping worker — PROCESS (not reject)
PASS | Phase Asleep
PASS | Sleeping Kowalski still processed WorkBlocked

## F. History follows WorkerId
PASS | History count grew and persists across jobs

## G. No machine state from events
PASS | Excavator Heat unchanged by EquipmentProblem event — heat=0

## H. No double-apply (processor-only Frustration)
PASS | Single WorkBlocked → one history row
PASS | Frustration rose once (positive delta recorded)

## I. MajorSuccess effects
PASS | MajorSuccess reduces Frustration
PASS | MajorSuccess increases Morale

## J. RepeatedFailure vs WorkBlocked + spam gate
PASS | RepeatedFailure gains ≥ WorkBlocked at same magnitude — wb=2 rf=2.15
PASS | Spam gate blocks rapid identical WorkBlocked — admitted=1

## K. Sleep recovery after events
PASS | Sleep still partially relieves Frustration

## L. No Social Aura
PASS | No SocialAura / InteractionPressure types

## Daytime decay
PASS | Daytime Frustration decay

## Emitters connected (V1.2B subset)
- Excavator: WorkBlocked / RepeatedFailure (obstruction), ProgressSuccess (tile/advance/WP),
  EquipmentProblem (overheat), Injury, PhysicalExhaustion (stamina rest)
- Engineer: EquipmentRecovered (operator) + ProgressSuccess (engineer)
- Prospector: Discovery (assessed finding, frozen WorkerId)
- Hauler / Refiner: not wired (no reliable semantic signal yet)

## Sleeping event policy
PROCESS — person still owns emotional residue while asleep.

## Summary
PASS: 30
FAIL: 0

VERDICT: PASS — V1.2B READY TO LOCK
