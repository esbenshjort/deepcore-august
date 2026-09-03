# V1.2C Universal Job Demand Audit
Generated: 2026-09-01 21:59:20

## N. Stamina priming
PASS | Unprimed ratio reads as full (not exhausted)
PASS | EnsureStaminaPrimed fills pool

## A–G. Job demand differentiation
PASS | D. Hauling creates physical fatigue — stam 100→77.4
PASS | E. Prospecting creates mental fatigue — MF 18→35.1
PASS | E. Prospecting attention drains FocusState — FS 62→49.4
PASS | F. Refining creates mental demand
PASS | F. Refining creates attention demand
PASS | G. Engineering mixed physical+mental — stam 100→85.4 MF 18→30.6
PASS | H. Excavation catalog idle when unbound
PASS | H. Excavation continuous tick does not spend PhysicalStamina — stam 100→100
PASS | H. Excavation mining adds light MentalFatigue
PASS | A. Jobs affect state differently (haul≠pros MF paths)

## B. State follows person
PASS | Same WorkerState object across jobs
PASS | MentalFatigue retained across reassignment

## C. No universal stacked efficiency
PASS | No WorkerEfficiency / stacked mul type

## I. Idle / rest recovery
PASS | Idle recovers MentalFatigue

## J. Exhaustion latch
PASS | Exhaustion event fires once per threshold

## K. Event spam control
PASS | Spam gate type present

## L. Sleep recovery
PASS | Sleep still partially relieves Frustration

## M. Morale slow-moving
PASS | Normal work demand does not move Morale

## Profile sheets (BASE / Focus / Det / Comp)
PASS | Profile sheets applied without crash

## Social Aura readiness matrix
| Job | Verdict | Notes |
|-----|---------|-------|
| Prospecting | PASS | Mental/Focus demand + Discovery events |
| Excavation | PASS | Dig stamina + Frustration events + light MF/FS |
| Hauling | PASS | Physical demand + Delivered/Stuck events |
| Refining | PASS | Mental/Attention + WashComplete events |
| Engineering | PASS | Mixed demand + Repair events |

## Summary
PASS: 21
FAIL: 0

VERDICT: PASS — V1.2C READY TO LOCK

InvestigationFailure: UNUSED — no reliable contradiction/theory-fail signal in investigation loop.
