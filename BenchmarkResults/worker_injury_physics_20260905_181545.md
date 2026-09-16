# Universal Worker Physics + Injury V1 Audit
Generated: 2026-09-05 18:15:45

## Exact stat → role mappings

| Stat / state | Role |
|---|---|
| Agility | MoveSpeed, Acceleration, catch-fall mod |
| Balance | Footing D20, FootingResist, Balance01 |
| Toughness | Fall catch D20, FootingResist |
| SpatialGeometry | TerrainAdapt + footing DC mod |
| Stamina | Endurance / exhaustion accident risk |
| Recovery | Footing recovery + injury heal rate |
| HeavyLifting | LoadHandling (carry mul + loaded-fall risk) |
| Focus + Composure (sheet) | StressStability (footing DC) |
| FocusState / MentalFatigue | Hard-terrain accident chance only |
| PhysicalStamina | Walk mul + exhaustion fall risk |
| Injury records (body part) | Contextual walk/manual/load muls |
| Morale / Frustration | Unused for movement |

## 1. Normal terrain stability
- PASS  Normal terrain: near-zero footing events — events=0/200

## 2–4. Difficult terrain / stats / fatigue / load
- PASS  Difficult terrain: weak has more footing fails than strong — weak=16 strong=0
- PASS  Loose/Rubble risk ≥ Rough for weak profile — loose=9 rough=7
- PASS  Exhaustion increases loose-rock accident rate — tired=3 rested=0
- PASS  Heavy load increases accident exposure — load=3 empty=2
- PASS  Exposure produced stumble/fall activity

## 5–6. Stumble vs injury; type matching
- PASS  Stumble usually does not injure (rate < 15%) — inj=0/127 rate=0
- PASS  Falls can produce injuries — fallInj=33 falls=67
- PASS  Loose-rock fall table favors lower-leg/wrist over random broken arm — leg/wrist=46 arm=0 n=80

## 7–9. Body-part consequences
- PASS  Broken foot hurts walking much more than broken arm — footW=0.52 armW=0.96
- PASS  Broken arm hurts manual work more than walking — manual=0.4 walk=0.96
- PASS  Broken arm manual < broken foot manual (arm worse for work) — armM=0.4 footM=0.75
- PASS  Back injury strongly affects load carry

## 10–11. Persist across jobs / leave equipment
- PASS  Find returns same person
- PASS  Injuries survive 'reassignment'
- PASS  Walk penalty follows person

## 12–13. Sleep / NeedsCare
- PASS  One night does not heal a broken leg — left 120→112.3
- PASS  Serious injury forces NeedsCare

## Existing injury compounds risk
- FAIL  Existing walking injury raises further accident rate — hurt=2 fresh=6

## Rarity
- PASS  Broken bones uncommon in typical fall picks (< 25%) — bones=30/200
- PASS  Critical injuries rare (< 8%) — crit=0/200

## Rates (seeded exposure runs)
- AuditStumbleCount=18
- AuditFallCount=5
- AuditInjuryCount=8
- AuditBrokenBoneCount=4
- PASS  Social/WorkerState Injury meter still used — AddInjury + WorkerStateEventType.Injury retained

## Scope
- No ragdolls, hospitals, diseases, prosthetics, or new mine hazards.
- Gas remains non-damaging in V1.
- Excavator chassis movement stays machine-owned.

**Result:** FAIL  (21 passed, 1 failed)
