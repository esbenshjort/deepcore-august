# Tunnel Width + Darkness + Claustrophobia V1

Generated: 2026-09-08  

Built on existing Excavator route/pins, WorkerState, Soul/stats, MineInfrastructure lanterns, Frustration events, ManagerComm, Social banter — **no parallel routing or lighting systems**.

---

## Width geometry / workload mapping

| Width | Dig half (cells) | Move radius (cells) | Tip min half | Stamina × / strike | Dig cadence × | Est. clearance | Access |
|------:|-----------------:|--------------------:|-------------:|-------------------:|--------------:|---------------:|--------|
| 1 | 0.7 | ~0.62 | 0.42 | 0.72 | 0.82 | 1 | Person only |
| 2 | 1.45 | ~1.28 | 0.55 | 0.85 | 0.90 | 1 | Person only |
| 3 | 2.5 | ~2.35 | 0.70 | 0.95 | 0.96 | 2 | Hauler OK |
| 4 | **4.32** (pre-V1 standard) | ~4.06 | 0.85 | 1.00 | 1.00 | 3 | Operational |
| 5 | 6.2 | ~5.83 | 0.85 | 1.18 | 1.12 | 4 | Wide corridor |

**Workload:** Primary cost is geometric — wider `_digHalf` / `_moveRadius` means more solid cells in the dig envelope before advance. Stamina/cadence multipliers are secondary.

**Route:** Each pin stores `DigRoutePin { Pos, Width }`. Keys **1–5** set `PlannedTunnelWidth`; LMB appends with that width. Preview line thickness + pin tint show corridor width.

---

## Access rules

- Workers: `PathAgentProfile.Worker` MinClearance **1** (unchanged).
- Hauler: `ExcavatedPathfinder.SetMachineProfile(..., MinClearance 2)` — cannot path through clearance-1 shafts (width 1–2 digs).
- Excavator dig follow still uses solid-circle clearance (not A*), driven by active width geometry.

---

## Stress accumulation / recovery

Person-owned `WorkerState.ClaustrophobicStress` (0–100), game-hour based (`ClaustrophobiaSystem.TickPerson`).

**Gain / hour (before resistance & exposure ramp):**
- Width band from live `TunnelNavGrid.GetClearance`
- Light: Lit 0 · Dim 3.5 · Dark 11 · Pitch black 20
- Depth: +0.28 × cells below camp (cap 14)
- Isolation (narrow + alone): +4.5
- Trapped: +38
- Exposure mul: 0.55 → 1.25 over ~2.5h continuous

**Resistance:** Composure, Bravery, Tolerance, Determination, Focus (Soul) lower gain; high Frustration / MentalFatigue / Injury / low Morale raise it.

**Recovery:** Camp / wide+lit spaces (−12…−28/h); sleep (−38…−52 overnight fraction); exposure hours decay at camp.

**Bands:** Elevated 32 · High 52 · Severe 72 · Critical 88

---

## Darkness thresholds

`TunnelIllumination.Sample01` = max of:
- `MineInfrastructure.EvaluateIllumination01` (engineer lanterns)
- Excavator work-light pool (~1.25 outer)
- Camp pool (~3.6 outer)

| Band | Illum |
|------|------:|
| Lit | ≥ 0.52 |
| Dim | ≥ 0.26 |
| Dark | ≥ 0.07 |
| Pitch black | &lt; 0.07 |

---

## Refusal / manager / social

- **Critical + SeekingExit:** excavator dig strikes refused (Determination+Bravery≥30 can push until ~95); route cleared on seek-exit pulse.
- **Forced deeper pin** while Severe+: Manager Trust↓ Resentment↑ + `ManagerCommunication` event.
- **Ease Off / Take A Break:** existing ManagerComm trust recovery paths unchanged.
- Banter via existing `TryWorkerBanter` / `TryAuthoredBanter` (`Claustrophobia` source).
- Collapse: `ClaustrophobiaSystem.SpikeCollapse` (+16 / +28).

---

## Files changed

| File | Role |
|------|------|
| `TunnelWidthSpec.cs` | **New** — width/light/band tables |
| `ClaustrophobiaSystem.cs` | **New** — sample, tick, tooltip, spikes |
| `WorkerState.cs` | ClaustrophobicStress + exposure + seeking exit; sleep relief |
| `FreeWorkerController.cs` | Per-pin width, dig geometry, stamina/cadence, refuse dig |
| `ExcavatedPathfinder.cs` | Machine profile API |
| `HaulerPerson.cs` | MinClearance 2 |
| `TunnelCollapse.cs` | Confinement spike on hit |
| `FreeMovementSocketMapRunner.cs` | 1–5 keys, tick, roster bar, width HUD, forced-deeper friction |

---

## Audit checklist (implementer)

| Check | Status |
|-------|--------|
| 1–5 hotkeys switch width (excavation selected) | Implemented |
| LMB uses planned width; segments can differ | Implemented |
| Dig geometry changes with active pin width | Implemented |
| Workload from cells + stamina/cadence | Implemented |
| Hauler blocked in clearance-1 tunnels | Implemented |
| Stress by width / depth / light / time | Implemented |
| Lanterns reduce darkness/stress | Via Sample01 |
| Camp/wide/light recover | Implemented |
| High stress → Focus + Frustration events | Implemented |
| Critical refuse / clear route | Implemented |
| Manager forced-deeper friction | Implemented |
| Collapse spike | Implemented |
| Game-hour accumulation (not frame×) | Implemented |
| Existing route workflow intact | Pins/LMB/RMB/Esc/Enter kept |

---

## Remaining limitations

- Darkness does not yet sample every worker headlamp (excavator + lanterns + camp only).
- Width→clearance is approximate after dig; organic faces vary.
- No oxygen / horror / permanent phobias (by design).
- Seek-exit does not auto-path home (clears dig; commute systems unchanged).
- Prospecting still uses 1/2/3 for scan range when prospecting selected (excavation uses 1–5).

**STOP.**
