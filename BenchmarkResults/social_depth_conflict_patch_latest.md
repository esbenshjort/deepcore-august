# Social Depth + Conflict Patch

Generated: 2026-09-03 (updated with offline audit results)  
Project: Tilemap_graphics_test / FreeMovement  
Scope: Dialogue depth, Frustration tuning, Arguments, Fights V1, Witnesses — **no redesign** of Social Aura / Memory / Relationship math cores.

## Status

| Round | Deliverable | Checkpoint | Result |
| --- | --- | --- | --- |
| 1 Dialogue depth | `SocialAuraLineBank.cs` + `SocialDialogueTone` | `social_dialogue_depth_latest.md` | **PASS** 12/0 |
| 2 Frustration | Compound setbacks, Tolerance, sleep diminishing, `FrustrationDevLog` | `frustration_tuning_latest.md` | **PASS** 13/0 |
| 3–5 Conflict | Arguments / fights / witnesses | `social_conflict_latest.md` | **PASS** 21/0 |

Offline runners from [Expand SocialAuraLineBank R1](a90f09d9-be79-4910-9690-97c599bdfe07) and [Build conflict R3-R5 systems](936fce3b-1629-44e3-b010-910eec1f7134) produced the latest green reports.

---

## Observed rates (seeded offline audits)

### Dialogue (`social_dialogue_depth_latest.md`)
| Metric | Value |
| --- | --- |
| Picks sampled | 20,188 |
| Unique lines | **298** |
| Stratified initiator unique | **123 / 504** (ratio 0.244) |
| Friendly/Bonded Encourage uniques | 20 |
| Grudge Encourage uniques | 13 (distinct set) |
| Camp vs Work Encourage | diverge (5 vs 6 in 40 draws) |
| First* Stage2 strings | stable (`manage`, `mess\|alone`) |

### Frustration (`frustration_tuning_latest.md`)
| Scenario | Observed |
| --- | --- |
| Successful shift | start≈8 → end **0** |
| Difficult WorkBlocked chain | start≈8 → end **28** |
| RepeatedFailure compound | ΔF **+38.7** → F **68.7** |
| Several bad days + sleep | residue **98.7** (not wiped) |
| ProgressSuccess at high Frust | Δ **−2.4** (capped) |
| Sleep diminishing @ ~53 Frust | relief **≈1.95** (base 3) |

### Conflict (`social_conflict_latest.md`)
| Metric | Healthy (40 trials) | Dysfunctional (seeded) | Rivalry (30 trials) |
| --- | --- | --- | --- |
| Arguments started | **0** | reachable (argTrials=1) | — |
| Fights | **0** | **1** (non-lethal injB=3.2) | **0** |
| Frust-alone emergence | **blocked** | — | — |
| Composure/Tolerance block | — | emergence blocked; fight gate 0/40 | — |
| Sub-interval beat spam | **0** extra beats | — | — |
| Witness acts (this seed) | probabilistic — 0 OK | path available | — |

### Tunables (live)
```
Argue: Frust≥52 Host≥7.5 Trust≤-1.5 + (neg mem / hostile trigger)
Fight: Host≥12 Frust≥68 Trust≤-4 + Soul hot-head + FailedDeEscalation + 14% Peak
ExchangeIntervalHours=0.55 MaxExchanges=6
WorkBlockedMul=1.35 RepeatedFailureMul=1.75
ProgressSuccess scale/cap=0.22/0.42 CompoundPerFrustration=0.012
Sleep FrustrationRelief base=3 (diminishing when high)
```

---

## Remaining regression (Editor / menu)

New audits are green offline. When convenient, also re-run:

- V1.2A / V1.2B / V1.2C  
- Social Aura S0–S2 + Stage2 Logic  
- Social Memory / Relationship V1.1 / Coop Work / Camp / Intent / Nickname / Frustration Loop  

Menus: **DeepCore/Diagnostics/Run Social Dialogue Depth Audit** · **Frustration Tuning** · **Social Conflict**.

## STOP

Round 1–5 implementation + dedicated audits are green. Full suite re-run is optional hygiene, not a blocker for this patch.
