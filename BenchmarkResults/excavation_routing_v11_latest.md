# Excavation Routing V1.1 — Player Authority + Width Fix

**Status:** Patch on Tile-Paint Routing V1. No second routing system.

Generated: 2026-09-08

---

## Verdict

Player-painted cells are the **only** excavation-order authority. Legacy autonomous face-chewing / shift auto-pins / claustro ClearRoute are disabled. Brush footprints are now 3/5/8/12/16 cells by width **class**. Plan overlay is quieter cyan blueprint.

---

## Root cause of autonomous digging

Three legacy paths created dig work without player paint:

1. **`TryContinueChewingAtFace`** — when the route finished against rock, invented a new paint stroke / face pin so the machine kept digging.
2. **`CaptureShiftBreakBookmark`** — end-of-shift dropped an auto pin ahead of the bit if the route was empty.
3. **`ShouldRefuseDeeperDig` → `ClearRoute()`** — critical claustrophobia deleted the player's plan (not just paused execution).

Additionally, dig target picking could still strike unplanned rock when no paint plan was active (WASD free dig / dig-face envelope).

---

## Legacy logic disabled / removed from authority

| Behavior | Change |
|---|---|
| `TryContinueChewingAtFace` | Stub `=> false` — never creates cells |
| Shift bookmark auto-pin | Removed — keeps existing plan only; logs “awaiting orders” if empty |
| Claustro `ClearRoute()` | Replaced with `PauseExecution` — **plan retained** |
| Dig without pending plan | `TryAuthorizedDig` / `TryPickDigTarget` refuse — no arbitrary rock |
| WASD dig | Only digs if player plan has pending cells |
| Plan complete | Stops; status **AWAITING ORDERS** — no fallback dig |

**Still allowed (execution, not orders):** path to work front, order among connected planned cells, pause/break/refuse/retreat/injury/shift end, resume remaining plan later.

**Player-only order creators:** LMB paint commit, RMB erase, Esc clear, Enter stamp (`AddPin` → paint), Shift+LMB replace.

---

## Authoritative order data structure

```
ExcavatorTilePaintPlan
  Dictionary<long, ExcavationPlanCell>  // key = (y<<32)|x
    ExcavationPlanCell { X, Y, Width=CLASS 1–5, State }
      State: Valid | Invalid | Excavating | Completed
```

- Width field stores **width class** (1–5), not raw cell count.
- Corridor footprint = `TunnelWidthSpec.BrushCells(class)`.
- Dig envelope while executing = `PaintDigHalfWorld(class)` = `BrushCells(class)/2 * cellSize`.

---

## New brush mappings

| Key / Class | Corridor cells | Feel |
|---|---|---|
| 1 | **3** | Very narrow / single-person |
| 2 | **5** | Narrow |
| 3 | **8** | Medium |
| 4 | **12** | Standard industrial |
| 5 | **16** | Major corridor |

Even widths (8/12/16): deterministic perp sign (+X preference) and asymmetric `halfLo/halfHi` so the corridor does not wobble left/right while dragging.

**Gameplay width class** remains 1–5 for stamina, cadence, claustrophobia bands, hauler clearance estimates — never treat “3 tiles” as class 3.

---

## Overlay opacity / presentation

Committed plan fills: ~α 28 (valid), quieter than before (~72).  
Live brush preview: ~α 42.  
Invalid: ~α 55 (stronger for readability).  
Thin cyan edge frames; inset fill so geology stays visible. No opaque turquoise slabs.

---

## Persistence / refusal

| Event | Plan |
|---|---|
| Critical claustrophobia refuse | **Kept** — `PauseExecution` / status REFUSED |
| Injury / break / cooling / overheat | Plan kept (execution gates only) |
| Shift change | Plan kept; no auto-pin |
| Next shift | `SyncGoalFromPaintPlan` resumes remaining Valid cells |
| Plan complete | Idle — **AWAITING ORDERS** |
| Player Esc / Shift+LMB clear | Plan cleared (player authority) |

---

## Audit results

| Check | Result |
|---|---|
| No paint → never digs | **PASS** (dig gates) |
| Only painted cells excavated | **PASS** (TryPickDigTarget paint-only) |
| Complete → stop | **PASS** |
| No autonomous new excavation | **PASS** (chew/shift pin removed) |
| Shift persistence | **PASS** |
| Claustro refuse keeps order | **PASS** |
| Class 1–5 → 3/5/8/12/16 | **PASS** |
| Straight span == BrushCells | **PASS** (perp dilation) |
| Overlay quieter | **PASS** |
| Hauler/claustro/supports via geometry | **PASS** (unchanged consumers) |

Editor: **DeepCore / Diagnostics / Run Excavator Tile-Paint V1 Audit**

---

## Files touched

- `TunnelWidthSpec.cs` — `BrushCells` / `BrushHalfCells`; DigHalfCells aligned to brush half
- `ExcavatorTilePaint.cs` — dilation + quiet overlay
- `FreeWorkerController.cs` — authority, pause, dig gates, labels
- `FreeMovementSocketMapRunner.cs` — HUD brush cells + activity
- `ExcavatorTilePaintRoutingV1Audit.cs` — V1.1 checks

**STOP.**
