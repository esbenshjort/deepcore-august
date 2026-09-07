# Injury Response V1.0 Audit
Generated: 2026-09-06 20:23:48

## Exact severity → Frustration event magnitude

| Severity | Base mag | Notes |
|---|---|---|
| Minor | 5 | small but noticeable |
| Moderate | 14 | meaningful |
| Serious | 28 | large |
| Critical | 42 | very large |
| Leg/Ankle/Foot/Head | ×1.12 | body-part weight |
| Abandon OffDuty | +6 | once on care return |
| Abandon Limited | +3.5 | once on care return |
| Abandon Incap | +10 | (incap itself; rarely walked) |
| Repeat (≥2 / ≥3 wounds) | +4 / +7 | once per new wound |
| Steward relief | −(2.5–10) | treatment; does not erase injury |

Processor still applies Composure / compounding via `WorkerStateEventType.Injury`.

## Return-to-work / work-status rules

| Status | Rule |
|---|---|
| FIT | No active meaningful restriction |
| LIMITED | Walk/manual/focus restricted or ForcesCare moderate |
| OFF DUTY | ForcesOutOfWork OR serious arm/wrist/back/concussion/rib stopper |
| INCAPACITATED | Existing flag OR immobilizing injury (broken lower limb serious+, crush, severe head) |
| Care commute | OffDuty always (if mobile); Limited grit-roll (Determination+Composure) |
| Immobile | MarkIncapacitated — no teleport; rescue required |
| Steward | Stabilize serious (rate↑, tiny shave); tend minor/moderate; no instant heal |

## Frustration scaling
- PASS  Minor mag ~5 — m=5
- PASS  Moderate mag ~14 — m=14
- PASS  Serious mag ≥28 — m=28
- PASS  Critical mag ≥42 — m=42
- PASS  Severity ordering Minor < Mod < Ser < Crit
- PASS  Leg/ankle moderate gets ×1.12 weight — m=15.68

## Scenario checks
- PASS  1. Minor usually FIT / continue — Fit
- PASS  1b. Minor does not force OffDuty
- PASS  2. Moderate can trigger return-to-camp — returns=28/40
- PASS  3. Serious arm → OffDuty — OffDuty
- PASS  3b. Serious arm can independent walk
- PASS  3c. Serious arm seeks camp
- PASS  3d. Manual heavily restricted
- PASS  4. Concussion → OffDuty (or Incap only if immobile) — OffDuty
- PASS  4b. Concussion focus restricted
- PASS  4c. Concussion usually walkable
- PASS  5. Serious broken leg → Incapacitated (no walk home)
- PASS  5b. Cannot independent walk
- PASS  5c. ShouldReturn false when immobile
- PASS  6. Multiple → OffDuty from serious wrist
- PASS  6b. Repeated-injury extra Frustration > 0
- PASS  7. Steward tend succeeds / stabilizes — Stabilized serious + tended 1 — still NeedsCare
- PASS  7b. Treatment does not wipe fracture — left 120→115.2
- PASS  7c. HelpedMe memory toward Steward
- PASS  7d. Frustration not erased by treatment — fr=1.3 (pre≈1.3)
- PASS  8. Fracture persists across sleep
- PASS  9. Foot walk << arm walk
- PASS  9b. Arm manual << arm walk
- PASS  10. Yield keeps assignment (architecture) — BeginInjuryReturnToCamp → YieldHost; assignment reserved
- PASS  11. Status labels present — LIMITED / Continuing work.
- PASS  12. Serious Frustration gain >> Minor — Δminor=3.7 Δser=34.9
- PASS  13. WorkerState Injury event path retained
- PASS  14. Social Memory types used (HelpedMe / Witness / SharedHardship)
- PASS  15. No parallel health system added — InjuryResponse derives status from WorkerInjury + WorkerState

## Scope (V1)
- No hospitals, doctors, surgery, med inventory, disease, disability, insurance.
- Incapacitated remains authoritative for rescue.
- Physical care commute; no teleport home.

**Result:** PASS  (34 passed, 0 failed)
