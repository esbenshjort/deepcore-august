# Frustration Tuning Audit (Round 2)
Generated: 2026-09-03 21:17:13

## Tunables
- Daytime decay/h = 0.18
- Sleep relief base = 3 (diminishing when high)
- ProgressSuccess scale=0.22 cap=0.42
- WorkBlockedMul=1.35 RepeatedFailureMul=1.75
- CompoundPerFrustration=0.012 Cap=0.85

## 1. Normal successful shift
PASS | Successful shift does not spike Frustration — start=8 end=0
PASS | Successful shift Frustration stays moderate — F=0

## 2. Difficult shift
PASS | Difficult shift raises Frustration — start=8 end=28
PASS | Difficult shift does not instantly max Frustration — F=28

## 3. Repeated failures
PASS | RepeatedFailure compounds Frustration upward — ΔF=38.7 F=68.7
PASS | Compound path engaged (Frustration higher than raw uncompounded floor) — F=68.7

## 4. Several bad days
FAIL | Several bad days leave persistent Frustration residue — day0=8 after=12
PASS | Sleep diminishing relief — does not fully wipe multi-day residue — F=12

## 5. Recovery after success / rest
PASS | ProgressSuccess relieves Frustration (capped / scaled) — start=55 after=52.6
PASS | ProgressSuccess does not erase high Frustration in one burst — Δ=2.4
PASS | Sleep applies diminishing Frustration relief — before=52.6 after=50.6 expectedRelief≈2
PASS | High-Frust sleep relief < base when Frustration was high
PASS | Diminishing: relief scale < 1.0 above 50 Frust — relief=1.95

## Summary
PASS 12 / FAIL 1
INVARIANT: FAIL
