# Worker Walk Speed Normalization V1

**Status:** Complete. Same `WorkerLocomotion` authority — no new movement system.

Generated: 2026-09-08

---

## Verdict

Off-duty / camp return looked superhuman because **commute used a 1.15× roleBias**, several job hosts pushed absolute speeds ~1.25–1.55 (above a high `ReferenceWalkSpeed` of **1.35**), and Hauler had a **1.8× MoveTowards escape** that bypassed locomotion. Walking is now normalized to a mine-scale human pace with modest stat variation and **no off-duty speed boost**.

---

## Root cause of extreme speeds

| Cause | Effect |
|---|---|
| `ReferenceWalkSpeed = 1.35` | Baseline too fast for person-sized sprites / tunnel visual scale |
| Agility mul **0.78–1.22** | Wide band → “superhuman” brisk walkers |
| Commute `roleBias: 1.15` | **Off-duty/home commute faster than work** |
| Toilet / camp return `roleBias: 1.1` | Same — leisure/camp movement as a boost |
| Engineer/Prospector/Refiner host absolutes **1.25–1.55** | `roleBias = absolute/Reference` stacked on already-scaled MoveSpeed |
| Hauler stuck-escape `MoveTowards(... * 1.8f)` | Direct transform motion **outside** WorkerLocomotion |
| Track `TrackSpeedMul = 1.25` | Noticeable haul sprint on tracks |
| Avatar bob **9.5 Hz** | High-frequency bob on fast motion → “gliding” feel |

Not the primary cause: SoftArrive (DEV/audit only), excavator chassis speed (separate machine), rescue carry (explicit emergency).

---

## Fixes (single locomotion authority)

### `WorkerPhysicalProfile` / `WorkerLocomotion`
- `ReferenceWalkSpeed`: **1.35 → 0.58**
- `MaxNormalWalkSpeed`: **0.72** hard ceiling (normal walk only)
- `RoleBiasMin/Max`: **0.88–1.08** clamp (hosts cannot invent sprint)
- Agility pace band: **0.88–1.12** (was 0.78–1.22)
- Rhythm nudge reduced
- Accel feel near-neutral (no hidden speed bump)

### Call sites
- Commute / toilet / camp evening: **roleBias 1.0** (was 1.15 / 1.1)
- Manual WASD still slightly cautious (0.85 → clamps to 0.88)
- Injury return: 0.95 (slightly careful — within band)
- Engineer / Steward / Refiner / Prospector / Hauler host speeds retuned to ~**0.52–0.60** intent band
- Prospector NavFollow dropped ×1.1–1.2 multipliers
- Hauler escape uses `WalkSpeedAt` (no 1.8× bypass)
- Track mul: **1.25 → 1.06**

### Visual
- Walk bob Hz **~3.6–6.2** scaled from `LastEffectiveSpeed` (was fixed 9.5)

---

## Final speed ranges (world units / sec)

Clear tunnel, unloaded, normal footing:

| Case | Approx speed |
|---|---|
| Baseline (Agi 10, bias 1) | **0.58** |
| Slow worker (low Agi, bias min) | **~0.45–0.51** |
| Fast worker (high Agi, bias max) | **~0.68–0.72** (cap) |
| Exhausted floor | × **0.72–0.82** stamina mul |
| Care / serious injury soft | × **0.78–0.85** |
| Rubble / loose rock | × **~0.32–0.48** terrain |
| Loaded haul | load mul stacks; track ≤ **×1.06** |
| Off-duty / commute / toilet / camp | **same formula, bias 1.0** — **no boost** |

Emergency rescue / collapse carry remains separate and bounded (not normal walk).

---

## Audit checklist

| Check | Result |
|---|---|
| One locomotion source for normal walk | **PASS** — `WorkerLocomotion.EvaluateWalkSpeed` |
| Off-duty does not increase speed | **PASS** — commute/camp bias 1.0 |
| No extreme spikes | **PASS** — cap 0.72 + bias clamp |
| Stat differences modest & visible | **PASS** — Agi ±12% |
| Difficult terrain slows | **PASS** — unchanged TerrainSpeedMul |
| Injuries slow | **PASS** — InjuryMoveMul |
| No teleport/catch-up for lateness | **PASS** — SoftArrive still DEV-only |
| Six jobs share authority | **PASS** — hosts pass clamped bias |

---

## Files

- `WorkerPhysicalProfile.cs`
- `WorkerLocomotion.cs`
- `FreeMovementSocketMapRunner.cs` (commute / toilet / camp biases)
- `EngineerPerson.cs`, `StewardPerson.cs`, `RefinerPerson.cs`, `ProspectorPerson.cs`, `HaulerPerson.cs`
- `MineInfrastructure.cs` (track mul)
- `WorkerAvatar.cs` (bob cadence)

**STOP.**
