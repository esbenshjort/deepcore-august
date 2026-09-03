# ProspectorDrySpellTracker — Timing Audit
Generated: 2026-09-02 08:25:51

Rule: accumulate OnShift hours only; first@32, repeat@+20; Discovery resets.
Constants: First=32 Repeat=20
Test: 5 × 10h OnShift + 14h night (nights do not Tick); Reset(); no Discovery.

## EXPECTED
- OnShift total = 50
- EVENT TIMES (OnShiftAccum): 32
- Count = 1 (second at 52 is outside window)

## ACTUAL
- OnShift total = 50
- EmitCount = 1
- DryOnShiftHours (end) = 50
- emit#1 OnShiftAccum=32 calendar=74

## RESULT
FIX: OnShift-delta accumulation (sleep does not Tick / does not advance thresholds)
EVENT TIMES: 32
AUDIT: PASS
