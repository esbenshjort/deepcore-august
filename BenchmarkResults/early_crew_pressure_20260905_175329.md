# Early Crew Pressure + Frustration Tuning Audit
Generated: 2026-09-05 17:53:28

## Before → After tunables

| Knob | Before | After |
|---|---:|---:|
| WorkBlockedMul | 1.35 | 1.65 |
| RepeatedFailureMul | 1.75 | 2.15 |
| CompoundPerFrustration | 0.012 | 0.018 |
| CompoundMax | 0.85 | 1.10 |
| ProgressSuccessReliefScale | 0.22 | 0.14 |
| ProgressSuccessReliefCap | 0.42 | 0.28 |
| ProgressSuccessHighFrustScale | 0.55 | 0.40 |
| DaytimeDecay/h | 0.18 | 0.12 |
| Sleep FrustrationRelief | 3.0 | 2.2 |
| Sleep high-F factor (>75) | 0.45 | 0.32 |
| Dig obstruction mag | 3.5 | 4.8 |
| DrySpell first/gap hours | 32/20 | 24/14 |
| DrySpell magnitude | 8 | 11 |
| CoopFriction excav/eng | 2.2/1.8 | 3.2/2.6 |
| Haul stuck mag | 2.5 | 3.4 |
| EquipmentProblem mul | 1.25 | 1.45 |
| InvestigationFailure mul | 1.15 | 1.40 |
| Soul resist Det/Tol | 0.10/0.08 | 0.14/0.12 |
| Soul composure cut | 0.06 | 0.09 |
| Hired relation seed | CreateNeutral (T/W/H=0, R=50) | Cautious low T/W, R~48, H≤1.1 |
| Early instability days | (none) | 7 working days |

Live WorkBlockedMul=1.65 RepeatedFailureMul=2.15
ProgressSuccess scale/cap=0.14/0.28
Sleep relief=2.2 DaytimeDecay=0.12
EarlyCrew InstabilityDays=7

## Seed invariants
- PASS  Seed edge 1→2 Hostility≤1.1 — H=0
- PASS  Seed edge 1→3 Hostility≤1.1 — H=0
- PASS  Seed edge 1→4 Hostility≤1.1 — H=0
- PASS  Seed edge 1→5 Hostility≤1.1 — H=0
- PASS  Seed edge 2→1 Hostility≤1.1 — H=0
- PASS  Seed edge 2→3 Hostility≤1.1 — H=0
- PASS  Seed edge 2→4 Hostility≤1.1 — H=0
- PASS  Seed edge 2→5 Hostility≤1.1 — H=0
- PASS  Seed edge 3→1 Hostility≤1.1 — H=0
- PASS  Seed edge 3→2 Hostility≤1.1 — H=0
- PASS  Seed edge 3→4 Hostility≤1.1 — H=0.32
- PASS  Seed edge 3→5 Hostility≤1.1 — H=0.39
- PASS  Seed edge 4→1 Hostility≤1.1 — H=0
- PASS  Seed edge 4→2 Hostility≤1.1 — H=0
- PASS  Seed edge 4→3 Hostility≤1.1 — H=0
- PASS  Seed edge 4→5 Hostility≤1.1 — H=0
- PASS  Seed edge 5→1 Hostility≤1.1 — H=0
- PASS  Seed edge 5→2 Hostility≤1.1 — H=0
- PASS  Seed edge 5→3 Hostility≤1.1 — H=0
- PASS  Seed edge 5→4 Hostility≤1.1 — H=0
- PASS  Not everyone hostile at seed — maxH=0.39
- PASS  Trust modest variation (not all friends) — T range [-0.52,0.82]
- PASS  EarlyCrewPressure Active after hire
- PASS  Deactivate clears Active

## Multi-day crew simulations (hired seed + early pressure)

### GOOD CREW
| Day | AvgF | MaxF | Trust | Warmth | Host | Respect | +Enc | −Enc | Args | Fights | RelClasses |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | 3.0 | 7.5 | 0.53 | 0.99 | 0.22 | 49.1 | 2 | 3 | 0 | 0 | N20 F0 B0 P0 R0 G0 S0 |
| 3 | 0.0 | 0.0 | 0.62 | 1.23 | 0.21 | 49.3 | 3 | 3 | 0 | 0 | N20 F0 B0 P0 R0 G0 S0 |
| 7 | 0.0 | 0.0 | 1.02 | 2.03 | 0.15 | 50.4 | 8 | 3 | 0 | 0 | N20 F0 B0 P0 R0 G0 S0 |
| 14 | 0.0 | 0.0 | 1.71 | 3.77 | 0.22 | 52.1 | 19 | 3 | 0 | 0 | N8 F12 B0 P0 R0 G0 S0 |

### AVERAGE CREW
| Day | AvgF | MaxF | Trust | Warmth | Host | Respect | +Enc | −Enc | Args | Fights | RelClasses |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | 28.6 | 85.2 | 0.02 | 0.38 | 0.37 | 47.8 | 1 | 1 | 0 | 0 | N20 F0 B0 P0 R0 G0 S0 |
| 3 | 41.4 | 99.1 | 0.08 | 0.51 | 0.58 | 48.0 | 4 | 2 | 0 | 0 | N20 F0 B0 P0 R0 G0 S0 |
| 7 | 39.6 | 99.1 | 0.50 | 1.21 | 0.78 | 48.5 | 14 | 6 | 0 | 0 | N20 F0 B0 P0 R0 G0 S0 |
| 14 | 39.6 | 99.2 | 0.67 | 1.62 | 1.35 | 49.3 | 21 | 15 | 0 | 0 | N15 F2 B0 P0 R0 G0 S3 |

### BAD CREW
| Day | AvgF | MaxF | Trust | Warmth | Host | Respect | +Enc | −Enc | Args | Fights | RelClasses |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | 24.0 | 69.4 | -0.15 | 0.24 | 0.35 | 48.1 | 2 | 0 | 0 | 0 | N20 F0 B0 P0 R0 G0 S0 |
| 3 | 39.6 | 99.1 | -0.19 | 0.28 | 0.97 | 48.6 | 7 | 3 | 0 | 0 | N18 F0 B0 P0 R0 G0 S2 |
| 7 | 39.6 | 99.2 | -0.09 | 0.60 | 1.34 | 49.1 | 13 | 9 | 0 | 0 | N16 F0 B0 P0 R0 G0 S4 |
| 14 | 40.1 | 99.2 | -0.19 | 0.57 | 2.52 | 49.1 | 22 | 19 | 0 | 0 | N12 F0 B0 P0 R0 G0 S8 |

### DYSFUNCTIONAL CREW
| Day | AvgF | MaxF | Trust | Warmth | Host | Respect | +Enc | −Enc | Args | Fights | RelClasses |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | 29.1 | 87.2 | -0.68 | -0.47 | 0.69 | 46.6 | 1 | 0 | 0 | 0 | N20 F0 B0 P0 R0 G0 S0 |
| 3 | 42.2 | 99.1 | -0.90 | -0.76 | 1.51 | 46.5 | 4 | 3 | 0 | 0 | N18 F0 B0 P0 R0 G0 S2 |
| 7 | 42.2 | 99.2 | -1.44 | -1.45 | 3.33 | 45.6 | 10 | 10 | 0 | 0 | N12 F0 B0 P0 R0 G0 S8 |
| 14 | 45.0 | 99.1 | -2.92 | -3.45 | 7.15 | 43.7 | 16 | 24 | 13 | 0 | N3 F0 B0 P0 R0 G6 S11 |

## Feel checks
- PASS  GOOD: early tension exists (day1 Hostility or Frust elevated vs zero)
- PASS  GOOD: week-2 more settled Trust than day1 OR Hostility not spiraling — T 0.53→1.71 H=0.22
- PASS  GOOD: fights remain rare — fights=0
- PASS  AVERAGE: mid/high Frustration during bad week (day7 MaxF≥35) — MaxF=99.1
- PASS  AVERAGE: occasional arguments reachable
- PASS  BAD: tension builds (day7 Hostility > day1)
- PASS  BAD: day7 Max Frustration meaningful (≥45) — MaxF=99.2
- PASS  DYSFUNCTIONAL: can spiral Hostility (day14 Host≥day7 or fights) — H 3.33→7.15 fights=0
- PASS  Not everyone auto-hostile (GOOD day14 Hostility < 6)
- PASS  Successful crews can recover Trust (GOOD day14 Trust not collapsed)
- PASS  Bad weeks leave Frust residue (AVERAGE day7 AvgF > spawn) — AvgF=39.6

## Sleep does not wipe high Frustration
- PASS  Sleep relief on F=80 leaves residue (cut < 3.5, after > 70) — cut=0.7 after=79.3

## ProgressSuccess modest relief
- PASS  One ProgressSuccess at F=50 cuts < 1.5 — after=49.89

## Scope
- Prototype DEV crew: EarlyCrewPressure.Deactivate on BootstrapPrototypeCrew.
- Hired crew: ActivateForHiredCrew after SocialAura.Bootstrap.
- No dialogue visual / SOCIAL DEV UI changes.
- Fight/lethal gates unchanged (only argue emergence softened early).

**Result:** PASS  (37 passed, 0 failed)
