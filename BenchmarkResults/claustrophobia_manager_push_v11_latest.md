# Claustrophobia Refusal + Manager Push V1.1

Generated: 2026-09-08  

Patch on existing Tunnel Width + Claustrophobia + Manager Communication. No redesign. Claustrophobia retained. Width 1 still costly long-term.

---

## Exact before → after (stress buildup)

### Width base gain / game-hour

| Width | V1 (before) | V1.1 (after) |
|------:|------------:|-------------:|
| 1 | 26 | **11** |
| 2 | 16 | **7** |
| 3 | 8 | **4** |
| 4 | 2.8 | **1.6** |
| 5 | 0.4 | **0.25** |

### Light gain / game-hour (catalog unchanged; Tick applies ×0.85)

| Band | Catalog | Effective in Tick |
|------|--------:|------------------:|
| Lit | 0 | 0 |
| Dim | 3.5 | ~3.0 |
| Dark | 11 | ~9.4 |
| Pitch black | 20 | ~17.0 |

### Depth / isolation / trap

| Term | V1 | V1.1 |
|------|----|------|
| Depth | 0.28 × cells, cap 14 | **0.16 × cells**, cap **12**; compounds after **1.2h** continuous exposure |
| Isolation (narrow+alone) | +4.5 | **+2.2** |
| Trapped | +38 | **+28** |
| Exposure ramp | 0.55 → 1.25 over ~2.5h | **0.42 → 1.20 over ~5h** (short trips manageable) |
| Camp / wide+lit recover | ~12–28/h | **16–36/h** (stronger early relief) |
| Overnight residue | −38 / −52 if stress &gt;70 | **−28 / −48** (more multi-day residue) |

### Band thresholds (refusal gate)

| Band | Label | V1 | V1.1 |
|------|-------|---:|-----:|
| Low | CALM | &lt;32 | **&lt;28** |
| Elevated | UNEASY | 32 | **28** |
| High | STRESSED | 52 | **48** |
| Severe | SEVERE | 72 | **68** (hesitation / complaints — not refuse) |
| Critical | CRITICAL | 88 | **90** (can refuse deeper dig) |

### Simulated hours to CRITICAL (rested resist≈1.0, width-only, lit, no depth)

Approximate continuous exposure integration of the ramps above:

| Width | V1 → Critical (88) | V1.1 → Critical (90) |
|------:|-------------------:|---------------------:|
| 1 | ~3.4h | **~8.4h** |
| 2 | ~5.1h | **~12.4h** |
| 3 | ~9.5h | **~20.4h** |
| 4 | ~25.9h | ~48h+ |
| 5 | effectively never | effectively never |

Other audit points (same integrator):

| Scenario | Approx to Critical |
|----------|-------------------:|
| W1 lit short trip **0.5h** stress Δ | V1 **~8.1** → V1.1 **~2.5** |
| W1 dark + depth≈20 | V1 **~2.3h** → V1.1 **~4.8h** |
| W1 lit tough Soul (resist~0.7) | **~11.4h** |
| W1 lit fragile (resist~1.35) | **~6.7h** |

**Read:** a rested worker can generally attempt a narrow stretch before hard refusal; sustained deep/dark exposure is still the serious path.

---

## Gradual states (behavior)

| State | Stress | Behavior |
|-------|--------|----------|
| CALM | &lt;28 | Normal |
| UNEASY | 28+ | Soft comments; light Focus drain |
| STRESSED | 48+ | Frustration events; Focus suffers more |
| SEVERE | 68+ | Hesitation / wants wider or light / break asks; **no dig refuse** |
| CRITICAL | 90+ | May refuse deeper excavation |

Seek-exit / dig refuse pulses only at **CRITICAL** (was Severe+ seeking in earlier drafts).

---

## Refusal is not permanent

- Dig refuse / seek-exit → **`PauseExecution`** — painted plan **retained**, never cleared/replaced.
- Banner: `NAME` / **REFUSES TO CONTINUE** / `"Not deeper. Not like this."`
- Status reads **REFUSED** while paused; cells stay pending.
- Recovery paths: wider/lit area, break, camp, sleep, manager Ease Off / Check In / Take A Break.
- Accepted **PUSH HARDER** resumes the **same** pending paint route with a temporary willingness ceiling (stress continues rising; claustrophobia not wiped).

---

## Manager PUSH HARDER (confinement)

Uses existing Trust / Respect / Resentment + Composure / Determination / Tolerance / Bravery + Frustration / MentalFatigue + confinement stress (+ light/depth sample). **No percentages shown.**

| Outcome label | Meaning |
|---------------|---------|
| ACCEPTS | Resume route; ceiling ~99–102; Frustration↑; Resentment may↑ |
| RELUCTANTLY ACCEPTS | Resume route; lower ceiling (~96.5); larger Frustration / Resentment / Trust hit |
| REFUSES | Stay paused; plan pending |
| ANGERED | Stay paused; larger relationship damage |
| PANICS | Stay paused; stress spike; panic memory; future pushes harder |

Repeated confinement pushes increment `ConfinementPushCount` — compliance falls; anger/panic/hard refuse become likelier. Anti-spam cooldowns remain. Infinite button-spam cannot brute-force every worker.

**Hard limit examples:** critical + pitch black, extreme fatigue + frustration, high resentment + prior pushes, panic memory + still critical. Player may need width, light, recovery, or another worker.

**EASE OFF / TAKE A BREAK / CHECK IN:** available under confinement pressure; apply soft `ApplyManagerRelief` + Trust where appropriate. Good management stabilizes; bad pushes buy progress at relationship cost.

---

## Willingness ceilings (accepted push)

| Reaction | Temp refuse ceiling | Duration (game hours) |
|----------|--------------------:|----------------------:|
| ACCEPTS (motivated) | 102 | ~1.45 |
| ACCEPTS | 99 | ~1.45 |
| RELUCTANTLY ACCEPTS | 96.5 | ~0.85 |

When expired, ceiling returns to CriticalAt (90). Stress is never erased by the push.

---

## Balance checklist

| Check | Result |
|-------|--------|
| Refusal not too early in normal play | Pass — W1 lit ~8h rested vs ~3.4h before |
| Short narrow exposure manageable | Pass — 0.5h W1 ~2.5 stress vs ~8 |
| Sustained deep/dark serious | Pass — ~4.8h W1 dark/deep |
| PUSH HARDER can work | Pass — AcceptedIntent resumes same plan |
| PUSH HARDER not guaranteed | Pass — refuse/anger/panic/hard limit |
| Accepted push resumes exact pending route | Pass — ResumeExecutionIfPossible; no plan rewrite |
| Route never deleted on refuse | Pass — PauseExecution only; seek-exit no longer ClearRoute |
| Repeated pushing has consequences | Pass — ConfinementPushCount + spamTax escalation |
| Good management can recover | Pass — Ease/Break/CheckIn relief + Trust |
| Extreme hard refusal remains | Pass — pitch black / fatigue / resentment / panic memory |
| Social / Manager / Excavation systems | Pass — existing hooks only; no parallel systems |

---

## Files touched

- `TunnelWidthSpec.cs` — gain rates + band thresholds + CALM…CRITICAL labels
- `ClaustrophobiaSystem.cs` — slower early ramp; Critical-only seek; manager relief; gradual banter
- `WorkerState.cs` — stronger overnight confinement residue
- `FreeWorkerController.cs` — pause retain plan; willingness ceiling; refuse banner hook
- `FreeMovementSocketMapRunner.cs` — seek-exit PauseExecution; refuse banner; push resume; talk intents
- `ManagerComm.cs` — confinement EvalPush; ReluctantlyAccepted / Panicked; intent flags; relief apply
- `ManagerCommLineBank.cs` — reluctant / panic / confinement refuse lines

---

## STOP

V1.1 patch complete. No further work in this pass.
