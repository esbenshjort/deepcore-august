# Steward + Camp Life V1 Audit

Generated: 2026-09-05 21:26:34

## Exact steward stat mappings (no new permanent stats)
| Stat | Role |
|------|------|
| Chemistry | Meal safety / prep quality (primary) |
| Finesse | Kitchen handling precision |
| Focus (sheet) + FocusState | Prep attention; fatigue penalty |
| WorkRate | Kitchen / cleanup throughput |
| Composure | Steadies quality under MentalFatigue |
| Empathy | Wound-care quality |
| Recovery | Care skill + personal recovery |
| Logistics | Camp hygiene maintenance while cleaning |
| SafetyProtocol | Contamination discipline |
| Toughness (eater) | Stomach upset resist |

## Recruitment / job seat
- PASS  JobType.Steward exists
- PASS  Steward in OccupiedJobs
- PASS  Steward in HireJobs
- PASS  HireJobs length 6 — n=6
- PASS  Steward candidates ≥4 — count=5
- PASS  Session fills 6 seats — filled=6
- PASS  TryBuildCrew
- PASS  Crew length 6
- PASS  All living with CampBody
- PASS  Unique WorkerIds
- PASS  Steward job present

## Meal / hygiene / care
- PASS  Skilled steward + clean camp not Unsafe — q=Normal
- PASS  Weak steward + filthy camp tends Poor/Unsafe — q=Unsafe
- PASS  Good meal mul within soft band
- PASS  Bad meal mul within soft band
- PASS  Serious injury not wiped by Steward care — left 120→120
- PASS  Minor injury recovery shortened modestly — before=18

## Toilet / stomach
- PASS  Normal toilet build ~once/day scale — rate=0.05
- PASS  Severe stomach ~hourly toilet rate — rate=1.05
- PASS  Dead workers skipped by meal apply

## Scope
- PASS  No hunger/thirst/recipe systems added
- PASS  JobContext.Steward speech label exists

**Result:** PASS  (22 passed, 0 failed)
