# Relationship Trajectory Audit
Generated: 2026-09-03 09:04:53

PASS | Sub-threshold delta ignored
PASS | Meaningful deltas recorded
PASS | Directional: 1→2 ≠ 2→1 lists
PASS | Bounded to MaxPerPair=16
FAIL | Expose encounters can write trajectory — Δ=0
PASS | Trajectory does not alter relation math API
PASS | PressureTrigger still 1.05
PASS | Memory MaxPerTarget still 12
PASS | Respect baseline still 50

## Summary
PASS 8 / FAIL 1
INVARIANT: FAIL

## Files
- Assets/Vibe/FreeMovement/RelationshipTrajectory.cs
- Assets/Vibe/FreeMovement/SocialAuraStage0Encounter.cs
- Assets/Vibe/FreeMovement/SocialAuraLive.cs
- Assets/Vibe/FreeMovement/FreeMovementSocketMapRunner.cs
