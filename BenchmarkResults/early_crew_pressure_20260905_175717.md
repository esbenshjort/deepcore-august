# Early Crew Pressure + Frustration Tuning Audit
Generated: 2026-09-05 17:57:17

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
| Early instability days | (none) | 9 working days |
| Early NegReactionMul peak | 1.0 | 1.55 |
| Early Argue Hostility relief | 0 | up to 5.4 via ArgueSoft01 |
| Early Argue Frustration relief | 0 | up to 12 via ArgueSoft01 |

Live WorkBlockedMul=1.65 RepeatedFailureMul=2.15
ProgressSuccess scale/cap=0.14/0.28
Sleep relief=2.2 DaytimeDecay=0.12
EarlyCrew InstabilityDays=9

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
| 1 | 3.0 | 7.5 | 0.53 | 0.98 | 0.23 | 1.86 | 49.1 | 2 | 3 | 0 | 0 | N20 F0 B0 P0 R0 G0 S0 |
| 3 | 0.0 | 0.0 | 0.61 | 1.21 | 0.23 | 1.86 | 49.3 | 3 | 3 | 0 | 0 | N20 F0 B0 P0 R0 G0 S0 |
| 7 | 0.0 | 0.0 | 1.14 | 2.26 | 0.17 | 2.26 | 50.8 | 10 | 4 | 0 | 0 | N18 F2 B0 P0 R0 G0 S0 |
| 14 | 0.0 | 0.0 | 2.18 | 4.34 | 0.23 | 2.26 | 53.7 | 27 | 7 | 0 | 0 | N9 F11 B0 P0 R0 G0 S0 |

### AVERAGE CREW
| Day | AvgF | MaxF | Trust | Warmth | Host | MaxH | Respect | +Enc | −Enc | Args | Fights | RelClasses |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | 27.7 | 81.1 | 0.06 | 0.46 | 0.38 | 2.44 | 47.9 | 2 | 1 | 0 | 0 | N20 F0 B0 P0 R0 G0 S0 |
| 3 | 40.1 | 99.1 | 0.14 | 0.63 | 0.42 | 2.31 | 48.1 | 4 | 2 | 0 | 0 | N20 F0 B0 P0 R0 G0 S0 |
| 7 | 39.6 | 99.2 | 0.38 | 1.08 | 0.67 | 4.84 | 48.7 | 11 | 6 | 0 | 0 | N18 F0 B0 P0 R0 G0 S2 |
| 14 | 39.6 | 99.2 | 0.53 | 1.80 | 1.93 | 12.18 | 49.8 | 20 | 17 | 12 | 0 | N14 F4 B0 P0 R0 G1 S1 |

### BAD CREW
| Day | AvgF | MaxF | Trust | Warmth | Host | MaxH | Respect | +Enc | −Enc | Args | Fights | RelClasses |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | 28.0 | 75.8 | -0.16 | 0.20 | 0.30 | 1.43 | 47.7 | 1 | 1 | 0 | 0 | N20 F0 B0 P0 R0 G0 S0 |
| 3 | 43.7 | 99.2 | -0.45 | -0.23 | 1.11 | 2.84 | 47.2 | 4 | 2 | 0 | 0 | N19 F0 B0 P0 R0 G0 S1 |
| 7 | 42.5 | 99.1 | -0.55 | -0.30 | 1.91 | 6.51 | 47.2 | 8 | 9 | 1 | 0 | N16 F0 B0 P0 R0 G0 S4 |
| 14 | 39.6 | 99.2 | -0.98 | -0.49 | 4.01 | 18.72 | 46.1 | 15 | 27 | 16 | 0 | N13 F0 B0 P0 R0 G2 S5 |

### DYSFUNCTIONAL CREW
| Day | AvgF | MaxF | Trust | Warmth | Host | MaxH | Respect | +Enc | −Enc | Args | Fights | RelClasses |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | 33.5 | 97.6 | -0.86 | -0.66 | 1.16 | 4.24 | 46.3 | 1 | 2 | 2 | 0 | N18 F0 B0 P0 R0 G0 S2 |
| 3 | 54.7 | 99.1 | -2.37 | -2.22 | 4.90 | 14.76 | 45.3 | 4 | 7 | 24 | 0 | N10 F0 B0 P0 R0 G6 S4 |
| 7 | 61.6 | 99.2 | -5.84 | -5.62 | 8.03 | 19.40 | 44.4 | 11 | 12 | 72 | 3 | N10 F0 B0 P0 R0 G8 S2 |
| 14 | 75.5 | 99.2 | -8.74 | -8.86 | 11.39 | 20.00 | 41.9 | 16 | 30 | 178 | 8 | N6 F0 B0 P0 R0 G11 S3 |

## Feel checks
- PASS  GOOD: early tension exists (day1 Hostility or Frust elevated vs zero)
- PASS  GOOD: week-2 more settled Trust than day1 OR Hostility not spiraling — T 0.53→2.18 H=0.23
- PASS  GOOD: fights remain rare — fights=0
- PASS  AVERAGE: mid/high Frustration during bad week (day7 MaxF≥35) — MaxF=99.2
- PASS  AVERAGE: argue pressure reachable (args≥1 or MaxH≥4 with high Frust) — args d7=0 d14=12 maxH=4.84
- PASS  BAD: tension builds (day7 Hostility > day1)
- PASS  BAD: day7 Max Frustration meaningful (≥45) — MaxF=99.1
- PASS  BAD: arguments reachable under pressure by day14 — args=16 maxH=18.72
- PASS  DYSFUNCTIONAL: can spiral Hostility (day14 Host≥day7 or fights) — H 8.03→11.39 fights=8
- PASS  DYSFUNCTIONAL: arguments in difficult first week (day7) — args=72 maxH=19.4
- PASS  DYSFUNCTIONAL: fights possible but not routine wipeout (day14 fights < 25) — fights=8
- PASS  Healthy GOOD crew does not fight — fights=0
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
- Args column counts argument beats (openings + ongoing exchanges), not unique sessions.

**Result:** PASS  (41 passed, 0 failed)
