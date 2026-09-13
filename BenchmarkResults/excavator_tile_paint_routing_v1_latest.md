# Excavator Tile-Paint Routing V1

**Status:** Implemented. Directional pin/capsule overlay is no longer authoritative. Painted tile cells are the excavation plan.

Generated: 2026-09-08

---

## Verdict

Excavator planning is now **tile-paint**: hold LMB, drag a grid-snapped stroke, release to commit. Brush width 1–5 paints a corridor of actual map cells. The excavator digs those cells in connected front order. Claustrophobia, hauler access, and supports continue to read **excavated geometry**.

---

## Controls

| Input | Behavior |
|---|---|
| **1–5** | Brush width |
| **Hold LMB + drag** | Paint plan preview (tile-snapped) |
| **Release LMB** | Commit stroke into plan |
| **Shift+LMB** | Clear plan, then start new stroke |
| **RMB** | Erase planned (not excavated) cells under brush / cancel stroke |
| **Esc** | Clear entire plan |
| **Backspace** | Erase near work front (legacy undo path) |
| **Enter** | Short stamp from excavator (legacy helper) |
| **WASD** | Manual dig; clears plan (unchanged) |

---

## Exact brush → cell mapping

Centerline via supercover line traversal (no gaps on fast / diagonal drags). Corridor via perpendicular dilation + disk stamp + interior corner fill.

`PaintDigHalfWorld(w, cs) = (w × 0.5) × cs` while executing a paint plan (envelope matches painted corridor). Stamina / cadence still use `TunnelWidthSpec` tables for the active width (balance unchanged).

| Brush | Paint half (world @ cs=0.1) | Straight 10-cell run (audit) | Intent |
|---|---|---|---|
| **1** | 0.050 | 10 cells, Y-span **1** | 1-tile passage |
| **2** | 0.100 | ~32 cells, Y-span **2–3** | ~2-tile |
| **3** | 0.150 | ~36 cells, Y-span **3** | 3-tile |
| **4** | 0.200 | ~58 cells, Y-span **4–5** | standard ops width |
| **5** | 0.250 | ~66 cells, Y-span **5** | wide corridor |

Active tunnel width on the current work cell drives stamina/cadence multipliers. **Actual excavated cells** drive clearance → claustrophobia / hauler / supports.

---

## Architecture

### New
- `ExcavatorTilePaint.cs` — `ExcavatorTilePaintPlan` (stroke, dilate, validate, queue pick) + `ExcavatorPaintPreview` (batched mesh quads)
- `ExcavatorTilePaintRoutingV1Audit.cs`

### Modified
- `FreeWorkerController.cs` — paint plan is route authority; dig prefers planned cells; pin/LineRenderer suppressed when plan active
- `FreeMovementSocketMapRunner.cs` — LMB paint drag / RMB erase; HUD “BRUSH”
- Editor menu: **DeepCore / Diagnostics / Run Excavator Tile-Paint V1 Audit**

### Unchanged (consumers)
- Geology damage / bedrock border rules
- `TunnelWidthSpec` stamina & cadence tables
- Hauler `MinClearance = 2` from live clearance
- Claustrophobia from live clearance
- Supports / collapse from excavated openness
- Debris, heat, frustration, stamina systems

---

## Plan validation

| Case | Result |
|---|---|
| Diggable solid / debris | **Valid** if connected to open tunnel or existing valid plan |
| Already open / excavated | Not ordered (Completed) |
| Undamageable border / OOB | **Invalid** (shown in preview; not silently rerouted) |
| Disconnected island in solid rock | **Invalid** — no auto-route across unexplored rock |

Multiple strokes append. Excavator picks nearest frontier valid cell (avoids thrashing distant pockets).

Preview colors: cyan preview OK / red preview bad / green valid / amber excavating / red invalid / dim completed.

---

## Audit checklist

| # | Check | Result |
|---|---|---|
| 1 | Width 1–5 map to cells | **PASS** — see mapping table |
| 2 | Fast drag no gaps | **PASS** — supercover AppendLineCells |
| 3 | Diagonal continuous | **PASS** |
| 4 | Curves usable width | **PASS** — dilated centerline |
| 5 | Corners filled | **PASS** — interior corner fill |
| 6 | Disconnected rejected | **PASS** — Invalid state |
| 7 | Invalid geology not silent-excavated | **PASS** — ClassifyCell + dig filter |
| 8 | Excavator follows paint plan | **PASS** — SyncGoalFromPaintPlan |
| 9 | Claustrophobia from geometry | **PASS** — clearance unchanged |
| 10 | Hauler access from geometry | **PASS** |
| 11 | Support/collapse from geometry | **PASS** |
| 12 | Queue/pause/cancel survive | **PASS** — Esc/RMB/ClearRoute |
| 13 | Capsule overlay not authoritative | **PASS** — hidden when paint active |
| 14 | No duplicate excavation system | **PASS** — same FreeWorkerController dig path |

Offline geometry audit: `ExcavatorTilePaintRoutingV1Audit` → `BenchmarkResults/excavator_tile_paint_routing_v1_audit_raw.md` (Editor menu).

---

## Files

- `Assets/Vibe/FreeMovement/ExcavatorTilePaint.cs`
- `Assets/Vibe/FreeMovement/ExcavatorTilePaintRoutingV1Audit.cs`
- `Assets/Vibe/FreeMovement/FreeWorkerController.cs`
- `Assets/Vibe/FreeMovement/FreeMovementSocketMapRunner.cs`
- `Assets/Vibe/FreeMovement/Editor/WorkerV11LockAuditMenu.cs`

**STOP** — Excavator Tile-Paint Routing V1 complete. No excavation balance retune in this pass.
