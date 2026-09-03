# Frustration Loop Diagnostic — 5 dry days
Generated: 2026-09-02 08:17:20

Scope: Frustration persistence only. Social Aura / Morale retune excluded.
Scenario: no Discovery for 5 days; Prospecting dry-spell active; Excavator gets frequent ProgressSuccess.

## Tunables under test
- Daytime FrustrationDecayPerGameHour = 0.18
- Sleep FrustrationRelief = 3
- ProgressSuccess relief = min(BaseGain×0.28, 0.55)
- DrySpell first=32h repeat=20h mag=8

## Per-day Frustration

| Day | Worker | Start shift | End shift | After sleep | Net day | Notes |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| 1 | Lewis | 8.00 | 6.20 | 3.20 | -4.80 | Prospector + dry-spell |
| 1 | Mara | 8.00 | 0.00 | 0.00 | -8.00 | Excavator + ProgressSuccess spam |
| 1 | Kowalski | 8.00 | 6.20 | 3.20 | -4.80 | passive decay + sleep only |
| 1 | Elena | 8.00 | 6.20 | 3.20 | -4.80 | passive decay + sleep only |
| 1 | Viktor | 8.00 | 6.20 | 3.20 | -4.80 | passive decay + sleep only |
| 1 | _(events)_ |  |  |  |  | 40 logged state events |
| 2 | Lewis | 3.20 | 8.40 | 5.40 | +2.20 | Prospector + dry-spell |
| 2 | Mara | 0.00 | 0.00 | 0.00 | +0.00 | Excavator + ProgressSuccess spam |
| 2 | Kowalski | 3.20 | 1.40 | 0.00 | -3.20 | passive decay + sleep only | HARD→0 |
| 2 | Elena | 3.20 | 1.40 | 0.00 | -3.20 | passive decay + sleep only | HARD→0 |
| 2 | Viktor | 3.20 | 1.40 | 0.00 | -3.20 | passive decay + sleep only | HARD→0 |
| 2 | _(events)_ |  |  |  |  | 41 logged state events |
| 3 | Lewis | 5.40 | 10.60 | 7.60 | +2.20 | Prospector + dry-spell |
| 3 | Mara | 0.00 | 0.00 | 0.00 | +0.00 | Excavator + ProgressSuccess spam |
| 3 | Kowalski | 0.00 | 0.00 | 0.00 | +0.00 | passive decay + sleep only |
| 3 | Elena | 0.00 | 0.00 | 0.00 | +0.00 | passive decay + sleep only |
| 3 | Viktor | 0.00 | 0.00 | 0.00 | +0.00 | passive decay + sleep only |
| 3 | _(events)_ |  |  |  |  | 41 logged state events |
| 4 | Lewis | 7.60 | 12.80 | 9.80 | +2.20 | Prospector + dry-spell |
| 4 | Mara | 0.00 | 0.00 | 0.00 | +0.00 | Excavator + ProgressSuccess spam |
| 4 | Kowalski | 0.00 | 0.00 | 0.00 | +0.00 | passive decay + sleep only |
| 4 | Elena | 0.00 | 0.00 | 0.00 | +0.00 | passive decay + sleep only |
| 4 | Viktor | 0.00 | 0.00 | 0.00 | +0.00 | passive decay + sleep only |
| 4 | _(events)_ |  |  |  |  | 41 logged state events |
| 5 | Lewis | 9.80 | 15.00 | 12.00 | +2.20 | Prospector + dry-spell |
| 5 | Mara | 0.00 | 0.00 | 0.00 | +0.00 | Excavator + ProgressSuccess spam |
| 5 | Kowalski | 0.00 | 0.00 | 0.00 | +0.00 | passive decay + sleep only |
| 5 | Elena | 0.00 | 0.00 | 0.00 | +0.00 | passive decay + sleep only |
| 5 | Viktor | 0.00 | 0.00 | 0.00 | +0.00 | passive decay + sleep only |
| 5 | _(events)_ |  |  |  |  | 41 logged state events |

## Event log (gains / losses)

- D1 @8.25h | Mara ProgressSuccess ΔF=-0.55 → F=7.41
- D1 @8.5h | Mara ProgressSuccess ΔF=-0.55 → F=6.81
- D1 @8.75h | Mara ProgressSuccess ΔF=-0.55 → F=6.21
- D1 @9h | Mara ProgressSuccess ΔF=-0.55 → F=5.62
- D1 @9.25h | Mara ProgressSuccess ΔF=-0.55 → F=5.02
- D1 @9.5h | Mara ProgressSuccess ΔF=-0.55 → F=4.43
- D1 @9.75h | Mara ProgressSuccess ΔF=-0.55 → F=3.83
- D1 @10h | Mara ProgressSuccess ΔF=-0.55 → F=3.24
- D2 @40h | Lewis InvestigationFailure (DrySpell) ΔF=+7.00 → F=8.76
- D3 @60h | Lewis InvestigationFailure (DrySpell) ΔF=+7.00 → F=11.68
- D4 @80.25h | Lewis InvestigationFailure (DrySpell) ΔF=+7.00 → F=14.55
- D5 @104.25h | Lewis InvestigationFailure (DrySpell) ΔF=+7.00 → F=16.75
- D5 @104.25h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @104.5h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @104.75h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @105h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @105.25h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @105.5h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @105.75h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @106h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @106.25h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @106.5h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @106.75h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @107h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @107.25h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @107.5h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @107.75h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @108h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @108.25h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @108.5h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @108.75h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @109h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @109.25h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @109.5h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @109.75h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @110h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @110.25h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @110.5h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @110.75h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @111h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @111.25h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @111.5h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @111.75h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @112h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @112.25h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @112.5h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @112.75h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @113h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @113.25h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @113.5h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @113.75h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- D5 @114h | Mara ProgressSuccess ΔF=+0.00 → F=0.00
- _(ProgressSuccess total emits: 200; sample above)_

## Dry-spell emit count
- ProspectorDrySpellTracker.EmitCount = 4
- Expected over 5×24h ≈ 120 calendar hours from reset: first at +36h, then ~every 22h while dry.

## Genuine success recovery (Discovery after dry streak)
- Lewis Frustration before Discovery: 12.00
- Discovery ΔF: -5.50
- Lewis Frustration after Discovery: 6.50
- RESULT: Frustration relieved by genuine Discovery (success recovery works).

## ProgressSuccess vs elevated Frustration (Mara stress test)
- Start elevated F=28.00; after 40× ProgressSuccess F=6.00
- RESULT: Frequent ProgressSuccess leaves setback residue (does not fully erase).

## Collapse / persistence verdict
- Hard-collapse-to-0 events (passive/prospector start>1 → morning≤0.05): 3
- Lewis morning day-5 Frustration: 12.00 (persist meaningful? YES)
- Lewis end-shift day-5 Frustration: 15.00
- Mara morning day-5 Frustration: 0.00 (productive digger floor — expected near 0 without setbacks)
- Elena morning day-5 Frustration: 0.00 (passive only — expected near 0 without setbacks)
- ProgressSuccess leaves elevated residue: YES
- Discovery recovery works: YES

## RECOMMENDATION
RECOMMENDATION: MOSTLY READY — dry-streak Frustration persists; passive floor-to-0 without setbacks is expected.
