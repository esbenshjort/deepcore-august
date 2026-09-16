# Early Crew Pressure + Frustration Tuning Audit
Generated: 2026-09-05 17:55:12

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
| Early NegReactionMul peak | 1.0 | 1.58 |
| Early Argue Hostility relief | 0 | up to 5.8 (gate ~1.7 at hire) |
| Early Argue Frustration relief | 0 | up to 12 (gate ~40 at hire) |

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
| Day | AvgF | MaxF | Trust | Warmth | Host | MaxH | Respect | +Enc | −Enc | Args | Fights | RelClasses |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | 3.0 | 7.5 | 0.52 | 0.98 | 0.24 | 1.90 | 49.1 | 2 | 3 | 0 | 0 | N20 F0 B0 P0 R0 G0 S0 |
| 3 | 0.0 | 0.0 | 0.61 | 1.22 | 0.23 | 1.90 | 49.3 | 3 | 3 | 0 | 0 | N20 F0 B0 P0 R0 G0 S0 |
| 7 | 0.0 | 0.0 | 0.97 | 2.02 | 0.22 | 2.25 | 50.1 | 9 | 4 | 0 | 0 | N19 F1 B0 P0 R0 G0 S0 |
| 14 | 0.0 | 0.0 | 1.68 | 3.76 | 0.37 | 2.55 | 51.3 | 19 | 7 | 0 | 0 | N14 F6 B0 P0 R0 G0 S0 |

### AVERAGE CREW
| Day | AvgF | MaxF | Trust | Warmth | Host | MaxH | Respect | +Enc | −Enc | Args | Fights | RelClasses |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | 27.7 | 81.1 | 0.06 | 0.46 | 0.38 | 2.49 | 47.9 | 2 | 1 | 0 | 0 | N20 F0 B0 P0 R0 G0 S0 |
| 3 | 40.1 | 99.1 | 0.14 | 0.63 | 0.42 | 2.36 | 48.1 | 4 | 2 | 0 | 0 | N20 F0 B0 P0 R0 G0 S0 |
| 7 | 41.2 | 99.1 | 0.20 | 0.85 | 1.06 | 4.67 | 48.3 | 10 | 6 | 0 | 0 | N18 F0 B0 P0 R0 G0 S2 |
| 14 | 39.6 | 99.1 | 0.57 | 1.72 | 1.71 | 4.57 | 49.1 | 18 | 14 | 0 | 0 | N16 F1 B0 P0 R0 G0 S3 |

### BAD CREW
| Day | AvgF | MaxF | Trust | Warmth | Host | MaxH | Respect | +Enc | −Enc | Args | Fights | RelClasses |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | 29.0 | 80.8 | -0.23 | 0.14 | 0.54 | 2.45 | 47.7 | 2 | 0 | 0 | 0 | N20 F0 B0 P0 R0 G0 S0 |
| 3 | 43.2 | 99.1 | -0.37 | -0.05 | 1.27 | 4.28 | 48.0 | 5 | 3 | 0 | 0 | N17 F0 B0 P0 R0 G0 S3 |
| 7 | 39.9 | 99.2 | -0.63 | -0.26 | 2.12 | 10.24 | 47.8 | 7 | 8 | 6 | 0 | N17 F0 B0 P0 R0 G2 S1 |
| 14 | 39.6 | 99.1 | -0.73 | 0.12 | 3.73 | 19.00 | 48.3 | 19 | 12 | 22 | 0 | N15 F0 B0 P0 R0 G2 S3 |

### DYSFUNCTIONAL CREW
| Day | AvgF | MaxF | Trust | Warmth | Host | MaxH | Respect | +Enc | −Enc | Args | Fights | RelClasses |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | 33.5 | 97.6 | -0.87 | -0.67 | 1.17 | 4.32 | 46.3 | 1 | 2 | 2 | 0 | N18 F0 B0 P0 R0 G0 S2 |
| 3 | 61.8 | 99.1 | -3.10 | -3.12 | 6.43 | 18.32 | 44.9 | 4 | 8 | 25 | 0 | N11 F0 B0 P0 R0 G6 S3 |
| 7 | 73.4 | 99.2 | -7.11 | -7.35 | 10.05 | 20.00 | 43.5 | 12 | 14 | 88 | 2 | N5 F0 B0 P0 R0 G8 S7 |
| 14 | 79.4 | 99.2 | -10.54 | -10.75 | 13.52 | 20.00 | 40.6 | 16 | 32 | 231 | 5 | N3 F0 B0 P0 R0 G15 S2 |

## Feel checks
- PASS  GOOD: early tension exists (day1 Hostility or Frust elevated vs zero)
- PASS  GOOD: week-2 more settled Trust than day1 OR Hostility not spiraling — T 0.52→1.68 H=0.37
- PASS  GOOD: fights remain rare — fights=0
- PASS  AVERAGE: mid/high Frustration during bad week (day7 MaxF≥35) — MaxF=99.1
- FAIL  AVERAGE: occasional arguments reachable by day14 — args d7=0 d14=0
- PASS  BAD: tension builds (day7 Hostility > day1)
- PASS  BAD: day7 Max Frustration meaningful (≥45) — MaxF=99.2
- PASS  BAD: arguments reachable under pressure by day14 — args=22 maxH=19
- PASS  DYSFUNCTIONAL: can spiral Hostility (day14 Host≥day7 or fights) — H 10.05→13.52 fights=5
- PASS  DYSFUNCTIONAL: arguments in difficult first week (day7) — args=88 maxH=20
- PASS  Not everyone auto-hostile (GOOD day14 Hostility < 6)
- PASS  Successful crews can recover Trust (GOOD day14 Trust not collapsed)
- PASS  Bad weeks leave Frust residue (AVERAGE day7 AvgF > spawn) — AvgF=41.2

## Sleep does not wipe high Frustration
- PASS  Sleep relief on F=80 leaves residue (cut < 3.5, after > 70) — cut=0.7 after=79.3

## ProgressSuccess modest relief
- PASS  One ProgressSuccess at F=50 cuts < 1.5 — after=49.89

## Scope
- Prototype DEV crew: EarlyCrewPressure.Deactivate on BootstrapPrototypeCrew.
- Hired crew: ActivateForHiredCrew after SocialAura.Bootstrap.
- No dialogue visual / SOCIAL DEV UI changes.
- Fight/lethal gates unchanged (only argue emergence softened early).

**Result:** FAIL  (38 passed, 1 failed)
