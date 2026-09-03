# Prospector Geological Evidence Diagnostic

Generated: 2026-08-29 20:15:50
Samples per case: 40
Cases: 9 · Workers: GREEN / BASE / ACE

Uses real `ProspectorGeoSignalProfiles`, `ProspectorGeoEvidence`, and `ProspectorInvestigationPlanner`.
No profile rebalancing applied.

---

## Worker: GREEN

| Truth case | n | Hard% | Mineral% | Promising% | Hazard% | Mixed% | Unclear% | Rev% | Other% | AvgConf | AvgWork |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Bedrock/Nominal | 40 | 85.0 | 7.5 | 7.5 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 | 3.00 | 2.73 |
| Bedrock/Fractured | 40 | 42.5 | 12.5 | 10.0 | 22.5 | 0.0 | 12.5 | 0.0 | 0.0 | 3.13 | 3.53 |
| Mineral/Nominal | 40 | 35.0 | 5.0 | 37.5 | 17.5 | 0.0 | 5.0 | 0.0 | 0.0 | 3.05 | 3.33 |
| Mineral/Dense | 40 | 60.0 | 7.5 | 22.5 | 0.0 | 0.0 | 5.0 | 5.0 | 0.0 | 2.95 | 3.33 |
| Mineral/Vein | 40 | 17.5 | 25.0 | 20.0 | 20.0 | 0.0 | 17.5 | 0.0 | 0.0 | 3.05 | 3.65 |
| Gas/Nominal | 40 | 5.0 | 7.5 | 2.5 | 67.5 | 0.0 | 15.0 | 2.5 | 0.0 | 2.98 | 4.45 |
| Gas/TrappedPocket | 40 | 7.5 | 7.5 | 2.5 | 72.5 | 0.0 | 7.5 | 2.5 | 0.0 | 2.93 | 4.43 |
| Mixed/Bedrock+Gas | 40 | 2.5 | 2.5 | 2.5 | 85.0 | 0.0 | 7.5 | 0.0 | 0.0 | 3.03 | 4.13 |
| Mixed/Bedrock+Mineral | 40 | 20.0 | 12.5 | 25.0 | 30.0 | 0.0 | 12.5 | 0.0 | 0.0 | 3.03 | 3.60 |

### By dominant truth material (collapsed)

| Material | n | Hard% | Mineral% | Promising% | Hazard% | Mixed% | Unclear% | AvgConf |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Bedrock | 80 | 63.8 | 10.0 | 8.8 | 11.3 | 0.0 | 6.3 | 3.06 |
| Gold | 120 | 37.5 | 12.5 | 26.7 | 12.5 | 0.0 | 9.2 | 3.02 |
| Gas | 80 | 6.3 | 7.5 | 2.5 | 70.0 | 0.0 | 11.3 | 2.95 |

### Confusion: TRUTH MATERIAL → FINAL ASSESSMENT

| Truth \ Assess | Hard | Mineral | Promising | Hazard | Mixed | Unclear | Revision |
|---|---:|---:|---:|---:|---:|---:|---:|
| Bedrock | 63.8% | 10.0% | 8.8% | 11.3% | 0.0% | 6.3% | 0.0% |
| Gold | 37.5% | 12.5% | 26.7% | 12.5% | 0.0% | 9.2% | 1.7% |
| Gas | 6.3% | 7.5% | 2.5% | 70.0% | 0.0% | 11.3% | 2.5% |
| Mixed* | 11.3% | 7.5% | 13.8% | 57.5% | 0.0% | 10.0% | 0.0% |

### Work units / sources

Average work units: 3.68

| Evidence path | count | % |
|---|---:|---:|
| `Desk+LooseRock` | 64 | 17.8 |
| `Desk+Refiner+LooseRock` | 47 | 13.1 |
| `Desk+LooseRock+CrossCheck` | 9 | 2.5 |
| `Desk+Field+LooseRock+CrossCheck` | 13 | 3.6 |
| `Desk+LooseRock+CrossCheck+Field+Refiner` | 7 | 1.9 |
| `Desk+Refiner+Field+LooseRock` | 44 | 12.2 |
| `Desk+Refiner+Field+LooseRock+CrossCheck` | 51 | 14.2 |
| `Desk+Field+Refiner+LooseRock+CrossCheck` | 7 | 1.9 |
| `Desk+Field+LooseRock` | 34 | 9.4 |
| `Desk+Refiner+LooseRock+CrossCheck` | 35 | 9.7 |
| `Desk+Field+LooseRock+CrossCheck+Refiner` | 30 | 8.3 |
| `Desk+Field+Refiner+LooseRock` | 3 | 0.8 |
| `Desk+LooseRock+Refiner+CrossCheck` | 2 | 0.6 |
| `Desk+Refiner+LooseRock+CrossCheck+Field` | 7 | 1.9 |
| `Desk+Field+LooseRock+Refiner+CrossCheck` | 2 | 0.6 |
| `Desk+LooseRock+Refiner` | 1 | 0.3 |
| `Desk+LooseRock+CrossCheck+Refiner` | 2 | 0.6 |
| `Desk+LooseRock+Refiner+CrossCheck+Field` | 1 | 0.3 |
| `Desk+Field+LooseRock+Refiner` | 1 | 0.3 |

---

## Worker: BASE

| Truth case | n | Hard% | Mineral% | Promising% | Hazard% | Mixed% | Unclear% | Rev% | Other% | AvgConf | AvgWork |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Bedrock/Nominal | 40 | 77.5 | 2.5 | 7.5 | 0.0 | 2.5 | 10.0 | 0.0 | 0.0 | 3.45 | 2.55 |
| Bedrock/Fractured | 40 | 40.0 | 2.5 | 2.5 | 35.0 | 5.0 | 15.0 | 0.0 | 0.0 | 3.18 | 3.55 |
| Mineral/Nominal | 40 | 35.0 | 15.0 | 25.0 | 12.5 | 0.0 | 12.5 | 0.0 | 0.0 | 3.55 | 3.08 |
| Mineral/Dense | 40 | 60.0 | 7.5 | 22.5 | 7.5 | 0.0 | 2.5 | 0.0 | 0.0 | 3.33 | 2.98 |
| Mineral/Vein | 40 | 7.5 | 22.5 | 30.0 | 30.0 | 2.5 | 7.5 | 0.0 | 0.0 | 3.48 | 3.55 |
| Gas/Nominal | 40 | 0.0 | 7.5 | 2.5 | 77.5 | 5.0 | 7.5 | 0.0 | 0.0 | 2.58 | 4.10 |
| Gas/TrappedPocket | 40 | 0.0 | 7.5 | 0.0 | 72.5 | 15.0 | 5.0 | 0.0 | 0.0 | 2.43 | 4.38 |
| Mixed/Bedrock+Gas | 40 | 0.0 | 12.5 | 0.0 | 77.5 | 7.5 | 2.5 | 0.0 | 0.0 | 2.63 | 4.10 |
| Mixed/Bedrock+Mineral | 40 | 20.0 | 22.5 | 30.0 | 20.0 | 0.0 | 7.5 | 0.0 | 0.0 | 3.55 | 3.33 |

### By dominant truth material (collapsed)

| Material | n | Hard% | Mineral% | Promising% | Hazard% | Mixed% | Unclear% | AvgConf |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Bedrock | 80 | 58.8 | 2.5 | 5.0 | 17.5 | 3.8 | 12.5 | 3.31 |
| Gold | 120 | 34.2 | 15.0 | 25.8 | 16.7 | 0.8 | 7.5 | 3.45 |
| Gas | 80 | 0.0 | 7.5 | 1.3 | 75.0 | 10.0 | 6.3 | 2.50 |

### Confusion: TRUTH MATERIAL → FINAL ASSESSMENT

| Truth \ Assess | Hard | Mineral | Promising | Hazard | Mixed | Unclear | Revision |
|---|---:|---:|---:|---:|---:|---:|---:|
| Bedrock | 58.8% | 2.5% | 5.0% | 17.5% | 3.8% | 12.5% | 0.0% |
| Gold | 34.2% | 15.0% | 25.8% | 16.7% | 0.8% | 7.5% | 0.0% |
| Gas | 0.0% | 7.5% | 1.3% | 75.0% | 10.0% | 6.3% | 0.0% |
| Mixed* | 10.0% | 17.5% | 15.0% | 48.8% | 3.8% | 5.0% | 0.0% |

### Work units / sources

Average work units: 3.51

| Evidence path | count | % |
|---|---:|---:|
| `Desk+LooseRock` | 61 | 16.9 |
| `Desk+Refiner+LooseRock` | 70 | 19.4 |
| `Desk+LooseRock+CrossCheck` | 15 | 4.2 |
| `Desk+LooseRock+Refiner` | 2 | 0.6 |
| `Desk+Refiner+LooseRock+CrossCheck+Field` | 6 | 1.7 |
| `Desk+LooseRock+Refiner+CrossCheck+Field` | 1 | 0.3 |
| `Desk+Field+LooseRock` | 27 | 7.5 |
| `Desk+Refiner+LooseRock+CrossCheck` | 20 | 5.6 |
| `Desk+Refiner+Field+LooseRock` | 40 | 11.1 |
| `Desk+Field+LooseRock+CrossCheck` | 56 | 15.6 |
| `Desk+LooseRock+CrossCheck+Field` | 3 | 0.8 |
| `Desk+Refiner+Field+LooseRock+CrossCheck` | 48 | 13.3 |
| `Desk+Field+Refiner+LooseRock+CrossCheck` | 2 | 0.6 |
| `Desk+Field+Refiner+LooseRock` | 3 | 0.8 |
| `Desk+LooseRock+Refiner+CrossCheck` | 2 | 0.6 |
| `Desk+Field+LooseRock+Refiner` | 1 | 0.3 |
| `Desk+LooseRock+CrossCheck+Field+Refiner` | 1 | 0.3 |
| `Desk+Field+LooseRock+CrossCheck+Refiner` | 2 | 0.6 |

---

## Worker: ACE

| Truth case | n | Hard% | Mineral% | Promising% | Hazard% | Mixed% | Unclear% | Rev% | Other% | AvgConf | AvgWork |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Bedrock/Nominal | 40 | 100.0 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 | 3.95 | 2.33 |
| Bedrock/Fractured | 40 | 30.0 | 22.5 | 0.0 | 10.0 | 20.0 | 17.5 | 0.0 | 0.0 | 3.65 | 3.30 |
| Mineral/Nominal | 40 | 35.0 | 27.5 | 20.0 | 7.5 | 5.0 | 5.0 | 0.0 | 0.0 | 3.78 | 3.20 |
| Mineral/Dense | 40 | 67.5 | 5.0 | 22.5 | 2.5 | 2.5 | 0.0 | 0.0 | 0.0 | 3.50 | 2.90 |
| Mineral/Vein | 40 | 12.5 | 12.5 | 72.5 | 0.0 | 2.5 | 0.0 | 0.0 | 0.0 | 3.65 | 3.33 |
| Gas/Nominal | 40 | 0.0 | 0.0 | 0.0 | 90.0 | 5.0 | 5.0 | 0.0 | 0.0 | 3.15 | 3.13 |
| Gas/TrappedPocket | 40 | 2.5 | 0.0 | 0.0 | 85.0 | 7.5 | 5.0 | 0.0 | 0.0 | 2.65 | 3.33 |
| Mixed/Bedrock+Gas | 40 | 0.0 | 5.0 | 0.0 | 85.0 | 7.5 | 2.5 | 0.0 | 0.0 | 3.13 | 3.83 |
| Mixed/Bedrock+Mineral | 40 | 10.0 | 20.0 | 70.0 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 | 3.83 | 3.13 |

### By dominant truth material (collapsed)

| Material | n | Hard% | Mineral% | Promising% | Hazard% | Mixed% | Unclear% | AvgConf |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Bedrock | 80 | 65.0 | 11.3 | 0.0 | 5.0 | 10.0 | 8.8 | 3.80 |
| Gold | 120 | 38.3 | 15.0 | 38.3 | 3.3 | 3.3 | 1.7 | 3.64 |
| Gas | 80 | 1.3 | 0.0 | 0.0 | 87.5 | 6.3 | 5.0 | 2.90 |

### Confusion: TRUTH MATERIAL → FINAL ASSESSMENT

| Truth \ Assess | Hard | Mineral | Promising | Hazard | Mixed | Unclear | Revision |
|---|---:|---:|---:|---:|---:|---:|---:|
| Bedrock | 65.0% | 11.3% | 0.0% | 5.0% | 10.0% | 8.8% | 0.0% |
| Gold | 38.3% | 15.0% | 38.3% | 3.3% | 3.3% | 1.7% | 0.0% |
| Gas | 1.3% | 0.0% | 0.0% | 87.5% | 6.3% | 5.0% | 0.0% |
| Mixed* | 5.0% | 12.5% | 35.0% | 42.5% | 3.8% | 1.3% | 0.0% |

### Work units / sources

Average work units: 3.16

| Evidence path | count | % |
|---|---:|---:|
| `Desk+LooseRock` | 75 | 20.8 |
| `Desk+Refiner+LooseRock` | 99 | 27.5 |
| `Desk+Refiner+LooseRock+CrossCheck` | 89 | 24.7 |
| `Desk+LooseRock+CrossCheck` | 70 | 19.4 |
| `Desk+Refiner+LooseRock+CrossCheck+Field` | 19 | 5.3 |
| `Desk+LooseRock+Refiner+CrossCheck+Field` | 2 | 0.6 |
| `Desk+LooseRock+Refiner` | 4 | 1.1 |
| `Desk+LooseRock+Refiner+CrossCheck` | 2 | 0.6 |

---

## Giveaway signal check (HIDDEN traits → truth material)

Nearest profile-centroid classifier on hidden floats (Bedrock / Mineral / Gas only; Mixed cases excluded).
Flag if accuracy ≥ 90% (accidental giveaway).

| Feature set | Accuracy% | Giveaway? | Note |
|---|---:|:---:|---|
| ReturnStrength | 46.4 | no | |
| Attenuation | 66.8 | no | |
| Conductivity | 61.1 | no | |
| StructuralCoherence | 59.6 | no | |
| BoundarySharpness01 | 46.8 | no | |
| ReturnStrength+Attenuation | 70.4 | no |  |
| ReturnStrength+Conductivity | 76.8 | no |  |
| ReturnStrength+StructuralCoherence | 62.9 | no |  |
| ReturnStrength+BoundarySharpness01 | 54.3 | no |  |
| Attenuation+Conductivity | 89.3 | WATCH | WATCH — near giveaway; do not rebalance yet |
| Attenuation+StructuralCoherence | 71.8 | no |  |
| Attenuation+BoundarySharpness01 | 61.8 | no |  |
| Conductivity+StructuralCoherence | 82.5 | no |  |
| Conductivity+BoundarySharpness01 | 69.6 | no |  |
| StructuralCoherence+BoundarySharpness01 | 59.3 | no |  |
| BoundaryCharacter (class) | 56.1 | no | |

---

## Health notes

- Bedrock → Hard: GREEN 63.8% · ACE 65.0%
- Mineral → Mineral/Promising: GREEN 39.2% · ACE 53.3%
- Gas → Hazard: GREEN 70.0% · ACE 87.5%

Healthy if: substantial off-diagonal mass remains; ACE improves vs GREEN without ~100% lock-in.
