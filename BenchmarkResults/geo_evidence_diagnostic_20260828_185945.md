# Prospector Geological Evidence Diagnostic

Generated: 2026-08-28 18:59:45
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
| Bedrock/Nominal | 40 | 77.5 | 2.5 | 7.5 | 0.0 | 2.5 | 10.0 | 0.0 | 0.0 | 3.45 | 2.53 |
| Bedrock/Fractured | 40 | 37.5 | 0.0 | 2.5 | 25.0 | 20.0 | 15.0 | 0.0 | 0.0 | 3.18 | 3.48 |
| Mineral/Nominal | 40 | 35.0 | 12.5 | 25.0 | 12.5 | 2.5 | 12.5 | 0.0 | 0.0 | 3.55 | 3.08 |
| Mineral/Dense | 40 | 57.5 | 7.5 | 22.5 | 7.5 | 2.5 | 2.5 | 0.0 | 0.0 | 3.33 | 2.98 |
| Mineral/Vein | 40 | 7.5 | 20.0 | 30.0 | 30.0 | 5.0 | 7.5 | 0.0 | 0.0 | 3.48 | 3.55 |
| Gas/Nominal | 40 | 0.0 | 5.0 | 2.5 | 22.5 | 62.5 | 7.5 | 0.0 | 0.0 | 2.58 | 4.10 |
| Gas/TrappedPocket | 40 | 0.0 | 5.0 | 0.0 | 27.5 | 62.5 | 5.0 | 0.0 | 0.0 | 2.43 | 4.38 |
| Mixed/Bedrock+Gas | 40 | 0.0 | 10.0 | 0.0 | 30.0 | 57.5 | 2.5 | 0.0 | 0.0 | 2.63 | 4.10 |
| Mixed/Bedrock+Mineral | 40 | 20.0 | 22.5 | 25.0 | 20.0 | 5.0 | 7.5 | 0.0 | 0.0 | 3.55 | 3.33 |

### By dominant truth material (collapsed)

| Material | n | Hard% | Mineral% | Promising% | Hazard% | Mixed% | Unclear% | AvgConf |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Bedrock | 80 | 57.5 | 1.3 | 5.0 | 12.5 | 11.3 | 12.5 | 3.31 |
| Gold | 120 | 33.3 | 13.3 | 25.8 | 16.7 | 3.3 | 7.5 | 3.45 |
| Gas | 80 | 0.0 | 5.0 | 1.3 | 25.0 | 62.5 | 6.3 | 2.50 |

### Confusion: TRUTH MATERIAL → FINAL ASSESSMENT

| Truth \ Assess | Hard | Mineral | Promising | Hazard | Mixed | Unclear | Revision |
|---|---:|---:|---:|---:|---:|---:|---:|
| Bedrock | 57.5% | 1.3% | 5.0% | 12.5% | 11.3% | 12.5% | 0.0% |
| Gold | 33.3% | 13.3% | 25.8% | 16.7% | 3.3% | 7.5% | 0.0% |
| Gas | 0.0% | 5.0% | 1.3% | 25.0% | 62.5% | 6.3% | 0.0% |
| Mixed* | 10.0% | 16.3% | 12.5% | 25.0% | 31.3% | 5.0% | 0.0% |

### Work units / sources

Average work units: 3.50

| Evidence path | count | % |
|---|---:|---:|
| `Desk+LooseRock` | 61 | 16.9 |
| `Desk+Refiner+LooseRock` | 70 | 19.4 |
| `Desk+LooseRock+CrossCheck` | 15 | 4.2 |
| `Desk+LooseRock+Refiner` | 2 | 0.6 |
| `Desk+Refiner+LooseRock+CrossCheck` | 23 | 6.4 |
| `Desk+LooseRock+Refiner+CrossCheck` | 3 | 0.8 |
| `Desk+Field+LooseRock` | 27 | 7.5 |
| `Desk+Refiner+Field+LooseRock` | 40 | 11.1 |
| `Desk+Field+LooseRock+CrossCheck` | 56 | 15.6 |
| `Desk+LooseRock+CrossCheck+Field` | 3 | 0.8 |
| `Desk+Refiner+Field+LooseRock+CrossCheck` | 48 | 13.3 |
| `Desk+Field+Refiner+LooseRock+CrossCheck` | 2 | 0.6 |
| `Desk+Field+Refiner+LooseRock` | 3 | 0.8 |
| `Desk+Refiner+LooseRock+CrossCheck+Field` | 3 | 0.8 |
| `Desk+Field+LooseRock+Refiner` | 1 | 0.3 |
| `Desk+LooseRock+CrossCheck+Field+Refiner` | 1 | 0.3 |
| `Desk+Field+LooseRock+CrossCheck+Refiner` | 2 | 0.6 |

---

## Worker: ACE

| Truth case | n | Hard% | Mineral% | Promising% | Hazard% | Mixed% | Unclear% | Rev% | Other% | AvgConf | AvgWork |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Bedrock/Nominal | 40 | 95.0 | 0.0 | 0.0 | 0.0 | 5.0 | 0.0 | 0.0 | 0.0 | 3.95 | 2.33 |
| Bedrock/Fractured | 40 | 27.5 | 17.5 | 0.0 | 2.5 | 35.0 | 17.5 | 0.0 | 0.0 | 3.65 | 3.10 |
| Mineral/Nominal | 40 | 35.0 | 22.5 | 12.5 | 2.5 | 22.5 | 5.0 | 0.0 | 0.0 | 3.78 | 3.13 |
| Mineral/Dense | 40 | 45.0 | 2.5 | 7.5 | 2.5 | 42.5 | 0.0 | 0.0 | 0.0 | 3.50 | 2.88 |
| Mineral/Vein | 40 | 12.5 | 10.0 | 42.5 | 0.0 | 35.0 | 0.0 | 0.0 | 0.0 | 3.65 | 3.30 |
| Gas/Nominal | 40 | 0.0 | 0.0 | 0.0 | 35.0 | 60.0 | 5.0 | 0.0 | 0.0 | 3.15 | 3.08 |
| Gas/TrappedPocket | 40 | 0.0 | 0.0 | 0.0 | 10.0 | 85.0 | 5.0 | 0.0 | 0.0 | 2.65 | 3.25 |
| Mixed/Bedrock+Gas | 40 | 0.0 | 2.5 | 0.0 | 22.5 | 72.5 | 2.5 | 0.0 | 0.0 | 3.13 | 3.75 |
| Mixed/Bedrock+Mineral | 40 | 10.0 | 17.5 | 55.0 | 0.0 | 17.5 | 0.0 | 0.0 | 0.0 | 3.83 | 3.13 |

### By dominant truth material (collapsed)

| Material | n | Hard% | Mineral% | Promising% | Hazard% | Mixed% | Unclear% | AvgConf |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Bedrock | 80 | 61.3 | 8.8 | 0.0 | 1.3 | 20.0 | 8.8 | 3.80 |
| Gold | 120 | 30.8 | 11.7 | 20.8 | 1.7 | 33.3 | 1.7 | 3.64 |
| Gas | 80 | 0.0 | 0.0 | 0.0 | 22.5 | 72.5 | 5.0 | 2.90 |

### Confusion: TRUTH MATERIAL → FINAL ASSESSMENT

| Truth \ Assess | Hard | Mineral | Promising | Hazard | Mixed | Unclear | Revision |
|---|---:|---:|---:|---:|---:|---:|---:|
| Bedrock | 61.3% | 8.8% | 0.0% | 1.3% | 20.0% | 8.8% | 0.0% |
| Gold | 30.8% | 11.7% | 20.8% | 1.7% | 33.3% | 1.7% | 0.0% |
| Gas | 0.0% | 0.0% | 0.0% | 22.5% | 72.5% | 5.0% | 0.0% |
| Mixed* | 5.0% | 10.0% | 27.5% | 11.3% | 45.0% | 1.3% | 0.0% |

### Work units / sources

Average work units: 3.10

| Evidence path | count | % |
|---|---:|---:|
| `Desk+LooseRock` | 75 | 20.8 |
| `Desk+Refiner+LooseRock` | 99 | 27.5 |
| `Desk+Refiner+LooseRock+CrossCheck` | 108 | 30.0 |
| `Desk+LooseRock+CrossCheck` | 70 | 19.4 |
| `Desk+LooseRock+Refiner+CrossCheck` | 4 | 1.1 |
| `Desk+LooseRock+Refiner` | 4 | 1.1 |

---

## Giveaway signal check (HIDDEN traits → truth material)

Nearest profile-centroid classifier on hidden floats (Bedrock / Mineral / Gas only; Mixed cases excluded).
Flag if accuracy ≥ 90% (accidental giveaway).

| Feature set | Accuracy% | Giveaway? |
|---|---:|:---:|
| ReturnStrength | 46.4 | no |
| Attenuation | 66.8 | no |
| Conductivity | 61.1 | no |
| StructuralCoherence | 59.6 | no |
| BoundarySharpness01 | 46.8 | no |
| ReturnStrength+Attenuation | 70.4 | no |
| ReturnStrength+Conductivity | 76.8 | no |
| ReturnStrength+StructuralCoherence | 62.9 | no |
| ReturnStrength+BoundarySharpness01 | 54.3 | no |
| Attenuation+Conductivity | 89.3 | no |
| Attenuation+StructuralCoherence | 71.8 | no |
| Attenuation+BoundarySharpness01 | 61.8 | no |
| Conductivity+StructuralCoherence | 82.5 | no |
| Conductivity+BoundarySharpness01 | 69.6 | no |
| StructuralCoherence+BoundarySharpness01 | 59.3 | no |
| BoundaryCharacter (class) | 56.1 | no |

---

## Health notes

- Bedrock → Hard: GREEN 63.8% · ACE 61.3%
- Mineral → Mineral/Promising: GREEN 39.2% · ACE 32.5%
- Gas → Hazard: GREEN 70.0% · ACE 22.5%

Healthy if: substantial off-diagonal mass remains; ACE improves vs GREEN without ~100% lock-in.
