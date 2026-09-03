# Frustration Loop Diagnostic — 5 dry days
Generated: 2026-09-02 08:15:53

Scope: Frustration persistence only. Social Aura / Morale retune excluded.
Scenario: no Discovery for 5 days; Prospecting dry-spell active; Excavator gets frequent ProgressSuccess.

## Tunables under test
- Daytime FrustrationDecayPerGameHour = 0.18
- Sleep FrustrationRelief = 5
- ProgressSuccess relief = min(BaseGain×0.35, 0.95)
- DrySpell first=32h repeat=20h mag=8

## Per-day Frustration

| Day | Worker | Start shift | End shift | After sleep | Net day | Notes |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| 1 | Lewis | 8.00 | 6.20 | 1.20 | -6.80 | Prospector + dry-spell |
| 1 | Mara | 8.00 | 0.00 | 0.00 | -8.00 | Excavator + ProgressSuccess spam | HARD→0 |
| 1 | Kowalski | 8.00 | 6.20 | 1.20 | -6.80 | passive decay + sleep only |
| 1 | Elena | 8.00 | 6.20 | 1.20 | -6.80 | passive decay + sleep only |
| 1 | Viktor | 8.00 | 6.20 | 1.20 | -6.80 | passive decay + sleep only |
| 1 | _(events)_ |  |  |  |  | 40 logged state events |
| 2 | Lewis | 1.20 | 6.64 | 1.64 | +0.44 | Prospector + dry-spell |
| 2 | Mara | 0.00 | 0.00 | 0.00 | +0.00 | Excavator + ProgressSuccess spam |
| 2 | Kowalski | 1.20 | 0.00 | 0.00 | -1.20 | passive decay + sleep only | HARD→0 |
| 2 | Elena | 1.20 | 0.00 | 0.00 | -1.20 | passive decay + sleep only | HARD→0 |
| 2 | Viktor | 1.20 | 0.00 | 0.00 | -1.20 | passive decay + sleep only | HARD→0 |
| 2 | _(events)_ |  |  |  |  | 41 logged state events |
| 3 | Lewis | 1.64 | 6.84 | 1.84 | +0.20 | Prospector + dry-spell |
| 3 | Mara | 0.00 | 0.00 | 0.00 | +0.00 | Excavator + ProgressSuccess spam |
| 3 | Kowalski | 0.00 | 0.00 | 0.00 | +0.00 | passive decay + sleep only |
| 3 | Elena | 0.00 | 0.00 | 0.00 | +0.00 | passive decay + sleep only |
| 3 | Viktor | 0.00 | 0.00 | 0.00 | +0.00 | passive decay + sleep only |
| 3 | _(events)_ |  |  |  |  | 41 logged state events |
| 4 | Lewis | 1.84 | 7.04 | 2.04 | +0.20 | Prospector + dry-spell |
| 4 | Mara | 0.00 | 0.00 | 0.00 | +0.00 | Excavator + ProgressSuccess spam |
| 4 | Kowalski | 0.00 | 0.00 | 0.00 | +0.00 | passive decay + sleep only |
| 4 | Elena | 0.00 | 0.00 | 0.00 | +0.00 | passive decay + sleep only |
| 4 | Viktor | 0.00 | 0.00 | 0.00 | +0.00 | passive decay + sleep only |
| 4 | _(events)_ |  |  |  |  | 41 logged state events |
| 5 | Lewis | 2.04 | 7.24 | 2.24 | +0.20 | Prospector + dry-spell |
| 5 | Mara | 0.00 | 0.00 | 0.00 | +0.00 | Excavator + ProgressSuccess spam |
| 5 | Kowalski | 0.00 | 0.00 | 0.00 | +0.00 | passive decay + sleep only |
| 5 | Elena | 0.00 | 0.00 | 0.00 | +0.00 | passive decay + sleep only |
| 5 | Viktor | 0.00 | 0.00 | 0.00 | +0.00 | passive decay + sleep only |
| 5 | _(events)_ |  |  |  |  | 41 logged state events |

## Event log (gains / losses)

- D1 @8.25h | Mara ProgressSuccess ΔF=-0.88 → F=7.08
- D1 @8.5h | Mara ProgressSuccess ΔF=-0.88 → F=6.16
- D1 @8.75h | Mara ProgressSuccess ΔF=-0.88 → F=5.24
- D1 @9h | Mara ProgressSuccess ΔF=-0.88 → F=4.32
- D1 @9.25h | Mara ProgressSuccess ΔF=-0.88 → F=3.40
- D1 @9.5h | Mara ProgressSuccess ΔF=-0.88 → F=2.48
- D1 @9.75h | Mara ProgressSuccess ΔF=-0.88 → F=1.56
- D1 @10h | Mara ProgressSuccess ΔF=-0.88 → F=0.64
- D2 @40h | Lewis InvestigationFailure (DrySpell) ΔF=+7.00 → F=7.00
- D3 @60h | Lewis InvestigationFailure (DrySpell) ΔF=+7.00 → F=7.92
- D4 @80.25h | Lewis InvestigationFailure (DrySpell) ΔF=+7.00 → F=8.79
- D5 @104.25h | Lewis InvestigationFailure (DrySpell) ΔF=+7.00 → F=8.99
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
- Lewis Frustration before Discovery: 2.24
- Discovery ΔF: -2.24
- Lewis Frustration after Discovery: 0.00
- RESULT: Frustration relieved by genuine Discovery (success recovery works).

## Collapse / persistence verdict
- Hard-collapse-to-0 events (start>1 → morning≤0.05): 4
- Lewis morning day-5 Frustration: 2.24 (persist meaningful? WEAK)
- Mara morning day-5 Frustration: 0.00 (ProgressSuccess spam wipe? YES wipe)
- Elena morning day-5 Frustration: 0.00 (passive only)
- Discovery recovery works: YES

## RECOMMENDATION
RECOMMENDATION: NEEDS WORK — Frustration still collapses or Discovery recovery failed.
