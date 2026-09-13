# UI + Dev Diagnostics Cleanup V1

**Date:** 2026-09-13  
**Scope:** Isolate development/debug telemetry behind one global DEV MODE. Clean normal worker sheet. No gameplay/sim changes. No visual redesign.

---

## Files changed

| File | Change |
|------|--------|
| `Assets/Vibe/FreeMovement/DevMode.cs` | **New** — authoritative `DevMode.Enabled` (default **off**), `SetEnabled` / `Toggle` |
| `Assets/Vibe/FreeMovement/DevMode.cs.meta` | Unity importer meta |
| `Assets/Vibe/FreeMovement/FreeMovementSocketMapRunner.cs` | F12 toggle, DEV badge, early-guard all DEV UI paths, slim normal ST sheet, LabelStyle cache, tool-strip split |
| `BenchmarkResults/ui_dev_diagnostics_cleanup_v1_latest.md` | This report |

---

## Global DEV MODE

- **Authority:** `DeepCore.FreeMovement.DevMode.Enabled` (static, default off).
- **Toggle:** **F12** → `ToggleDevMode()` → `DevMode.Toggle()`.
- **Indicator:** small bottom-right **DEV** badge when enabled (not part of production-facing strip).
- **On OFF cleanup:** closes DEV-only HUD popups, balance harness, Truth View, infra debug overlay, social world gizmos, Mission01 dev validation flag, collapse stability overlay.

Guards are early returns / `if (DevMode.Enabled)` — not alpha-zero or off-screen draw.

---

## Debug surfaces found & classification

### PLAYER-FACING (unchanged availability)

| Surface | Notes |
|---------|--------|
| Top resource / clock bar | Always |
| Crew roster + face monitors + ST sheet button | Always |
| Worker ST sheet (slim) | Always — see hierarchy below |
| Tactical View + History | Always (`U` / `H`) |
| Scanner HUD / placement / plan | Always when prospector selected |
| Mission 01 HUD (gold/dia/days) | Always |
| COMMS, KEYS, CAMP (status), SHIFT, TALK, CREW | Tool strip |
| START GAME / RESET CREW | Tool strip |
| Hiring overlay | Always when open |
| World speech / banter bubbles / escalate FX | Always |
| Collapse event banners | Always |
| Finding toast, tunnel-width HUD, roster tooltips | Always |
| Hauler “PRECIOUS FIRST”, Refiner priority buttons | Always (player controls) |
| Manager relationship + Talk on sheet | Always |

### DEV-ONLY (gated by `DevMode.Enabled`)

| Surface | Notes |
|---------|--------|
| **MOVE DEBUG** (bottom-left) | Entire format+draw skipped when OFF |
| Truth View (`T`) | Button + toggle hidden when OFF |
| ASSIGN / CTRL / SHEET / BANTER / PRESENCE / SOCIAL / RUNTIME / ACTIVITY / BALANCE / MISSION | Tool-strip section “DEV” only |
| Balance harness (`B`) | Key + panel |
| Prospector ACTIVITY box | Early return |
| Scan History DEV readout strip | Early return |
| Camp panel **DEV CONTROLS** (meals, injuries, collapse force, etc.) | Player status still shown |
| Engineer debug dump + INFRA DEBUG + coop Q lines | Hidden when OFF |
| Hauler TRACK MUL readout | Hidden when OFF |
| Social aura world gizmos | Requires DEV + panel toggle |
| ST sheet: WorkerId / StatsRef / Provider / ACE·GREEN / job test presets | DEV only |
| ST sheet: demand coeffs, debug tags, WALK×/LOAD×, Traits dump, StateRef, **RECENT STATE EVENTS** | DEV only |
| ST sheet excavator: Plan / Tool / ProviderId | DEV only |
| Prospector force-complete (`=`) / Alt time boost | DEV only |

---

## Normal worker sheet (DEV OFF) target hierarchy

```
NAME
Job title
Current: <activity>

PERSON STATE
  Physical Stamina
  Mental Fatigue
  Focus
  Frustration
  Morale
  Confinement
  Injury (+ active injuries / toilet when relevant)

YOUR RELATIONSHIP
  Trust / Respect / Resentment + Talk

MACHINE (excavator assigned)
  Heat + zone
  Activity / warning label

BODY / MIND / SOUL
  (job-relevant highlight kept)
```

No raw event log, demand coefficients, plan IDs, or provider IDs in normal mode.

---

## OnGUI allocation notes (addressed / noted)

| Issue | Action |
|-------|--------|
| `LabelStyle` allocated `new GUIStyle` every call | **Cached** by size+bold; color set per use |
| `new HashSet<>(RelevantStats)` every sheet frame | **Reused** `_sheetRelevantScratch` |
| MOVE DEBUG string build every OnGUI | **Skipped** entirely when DEV OFF |
| Event-history traversal on sheet | **Skipped** when DEV OFF |
| Engineer `EvaluateExcavatorEngineerCoop` for UI dump | **Skipped** when DEV OFF |
| Remaining player-facing `$"..."` in OnGUI | Left as-is (not a blind micro-opt pass) |

---

## Intentionally untouched

- Gameplay / balance / worker / excavation / social simulation logic
- Prospecting systems redesign
- Priority System
- Separate audit runners / Editor menu audits
- Non-SocketMap OnGUI runners (`FreeMovementTestRunner`, balance compare, etc.)
- Cyber visual language / layout redesign beyond removing DEV clutter
- Broader performance optimization beyond obvious DEV OnGUI waste

---

## Regression (static / wiring verification)

**DEV MODE OFF (code-path guarantees):**

- MOVE DEBUG, Truth View, DEV tool-strip panels, balance, activity, scan DEV strip, engineer/hauler debug lines, camp DEV controls, sheet telemetry — not entered.
- Player panels (roster, ST sheet core, Talk/Shift/Camp status, Tactical, scanner, hiring, comms) remain callable.
- No simulation fields mutated by this pass.

**DEV MODE ON:**

- MOVE DEBUG, event history, excavation plan/tool lines, Truth View, balance, social/runtime/assign panels, infra debug remain available.

**Compile:** brace balance + symbol presence checked on `FreeMovementSocketMapRunner.cs`. Full Unity Editor play-mode pass is recommended on machine (six sheets, roster, dig, prospector, camp, talk).

---

## How to use

1. Play Socket Map scene with DEV off → clean HUD.
2. Press **F12** → DEV badge + DEV tool strip + MOVE DEBUG + sheet telemetry.
3. Press **F12** again → DEV surfaces close and stop formatting.
