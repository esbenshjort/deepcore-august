# 24-Hour Crew Cycle + Shift Planner V1 Audit

Generated: 2026-09-07 10:22:04

## Exact overtime Soul mappings
| Stat | Role |
|------|------|
| Composure | Softens overtime intensity (0.35) |
| Determination | Softens intensity (0.25) |
| Tolerance | Primary OT tolerance (0.40) |
| Focus / MentalFatigue / Frustration | Amplify OT pressure at tick |
| Manager Resentment/Trust/Respect | Separate worker→manager axes |

## Shift planner
- PASS  Default 8h work — h=8
- PASS  Default band NORMAL
- PASS  8h sleep estimate near preferred — sleep=7.5
- PASS  8h has camp/free surplus — camp=7
- PASS  10h OVERTIME band
- PASS  12h HEAVY band
- PASS  Long commute shrinks sleep — sleep=5.7
- PASS  Sleep estimate positive at 8h — sleep=7.5

## Manager relationship
- PASS  Resentment accumulates
- PASS  Reasonable schedule recovers resentment slowly

## Overtime pressure (differential tolerance)
- PASS  High Soul tolerance > low — hard=0.79 soft=0.20
- PASS  OT event type exists
- PASS  OT streak increments after long day

## Day ledger / summary
- PASS  Summary averages tracked
- PASS  Summary pending flag

## Commute invariants (runner source)
- PASS  Runner source readable — /private/tmp/Tilemap_graphics_test_integ_audit2/Assets/Vibe/FreeMovement/FreeMovementSocketMapRunner.cs
- PASS  EnterCampEvening before sleep
- PASS  CommuteEmergencyTimeoutSec hard-unlocks camp return
- PASS  Stranded unlocks commute early
- PASS  No snappy CommuteTimeoutSec = 5.5
- PASS  EnterSleep does not teleport to tent
- PASS  BeginHeadingOut physical commute
- PASS  EnterOnShift does not AlignHosts SoftTeleport
- PASS  No SoftArrive on normal commute timeout
- PASS  Morning wake does not invent leftover recovery
- PASS  SHIFT planner HUD strip present
- PASS  YESTERDAY summary overlay present
- PASS  OvertimePressure tick while OnShift
- PASS  WorkerLocomotion used on commute

## Scope guard (V1)
- PASS  No weekly calendar API
- PASS  No strike system API

**Result:** PASS  (31 passed, 0 failed)
