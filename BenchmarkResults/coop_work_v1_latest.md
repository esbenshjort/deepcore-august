# Relationship-Aware Work V1 Audit
Generated: 2026-09-03 09:09:20

Scope: Excavator↔Engineer repair CooperationQuality only.
No Warmth as major efficiency. No flat global productivity. No Aura frequency change.

## 1. Strong professional pair can outperform strained pair
PASS | Professional Quality > Strained Quality — pro=0.70 strained=0.18
PASS | Professional dispatch faster than strained — pro×1.06 strained×0.88
PASS | Professional repair duration shorter than strained — pro×0.95 strained×1.15
PASS | Strained has setback chance; professional has benefit chance — setback=0.22 benefit=0.06

## 2. High Hostility + high Respect can still cooperate reasonably
PASS | Rivalry Quality near competent baseline (≥0.45) — Q=0.50 factors=audit T=2.0 R=78.0 H=12.0
PASS | Rivalry dispatch not crippled (≥0.95) — spd×0.99
PASS | Rivalry Quality >> deeply strained — rivalry=0.50 strained=0.18

## 3. Relationship does not affect unrelated jobs
PASS | Lantern/support durations are fixed constants (not coop-scaled)
PASS | Cooperation muls only applied on repair path fields
PASS | Engineer base MoveSpeed unchanged (1.45)
PASS | Engineer base RepairSeconds unchanged (2.6)

## 4. Effects remain bounded/modest
PASS | Quality floor ≥ 0.18
PASS | Quality ceil ≤ 0.88
PASS | Dispatch mul in [0.88,1.12] — strained=0.88 pro=1.06
PASS | Repair dur mul in [0.88,1.15]
PASS | Setback chance ≤ 0.22
PASS | Benefit chance ≤ 0.2
PASS | Worst repair still finishes in modest time (<4s realtime) — worst=2.99s

## 5. No recursive relationship feedback from the modifier itself
PASS | After CoopBenefit, Trust unchanged
PASS | After CoopBenefit, Respect unchanged
PASS | After CoopBenefit, Hostility unchanged
PASS | After CoopBenefit, Warmth unchanged
PASS | After CoopSetback, Respect still unchanged
PASS | Consequence stamps Benefit then Setback
PASS | Warmth not an EvaluateAxes input (same axes → same Q)

## 6. Existing Aura/Memory/Relationship invariants pass
PASS | PressureTrigger still 1.05
PASS | CooldownAfterEncounter still 0.55
PASS | ReachWorldScale still 2.35
PASS | MaxEncountersPerWorkerPerShift still 3
PASS | Memory MaxPerTarget still 12
PASS | Respect baseline still 50
PASS | RelationshipClass remains derived-only (no field on relation)
PASS | Missing operators → Inactive assessment

## Summary
PASS 33 / FAIL 0
INVARIANT: PASS

## Files
- Assets/Vibe/FreeMovement/RelationshipAwareWorkV1.cs (new)
- Assets/Vibe/FreeMovement/EngineerPerson.cs (repair coop muls + outcome)
- Assets/Vibe/FreeMovement/FreeMovementSocketMapRunner.cs (bind + DEV UI)
- Assets/Vibe/FreeMovement/RelationshipAwareWorkV1Audit.cs (this audit)
