# Excavator Access + Work-Front Fix

**Status:** Patch on Excavation Routing V1.1. No second routing/locomotion system.  
**Generated:** 2026-09-13

---

## Verdict

Excavator no longer drills a straight line through unpainted rock toward a distant painted area. Movement to a new work area uses **existing open tunnels only**. Dig damage is hard-gated to **player-painted pending cells**.

---

## Root cause

Three coupled failures:

1. **`TryPickNextWorkCell` treated plan connectivity as access**  
   `BuildReachableSeeds` + `GrowThroughPlan` flood through *painted* cells as if they were walkable. A distant pending cell behind solid rock scored as “reachable,” then became the dig target.

2. **Goal = rock cell center**  
   `SyncGoalFromPaintPlan` set `_goal = CellCenter(workX, workY)` (inside solid pending rock). Tick then did `Step(to.normalized, dig: true)` — straight-line approach with dig enabled.

3. **Approach dug unpainted rock**  
   While blocked en route, paint-driven body-trap logic in `TryPickDigTarget` excavated unmarked solids in the chassis footprint so the machine could “punch through” to the goal.

Net playtest failure: finish area A → paint area B elsewhere → excavator pointed at B and carved an unauthorized connecting tunnel.

---

## Exact fix

| Area | Change |
|------|--------|
| Work-front pick | New `TryPickAccessibleWorkFront`: pending + adjacent open stand + open-floor BFS reachability (never through paint/solid) |
| Goal | Stand cell on open floor beside the painted face — **not** the rock cell |
| Approach | `ExcavatedPathfinder.Follow` to stand with `Step(dir, dig: false)` |
| Dig | Only after arriving at stand; dig direction toward painted work cell |
| No access | `_awaitingAccess` → machine label **AWAITING ACCESS**; plan retained; no dig / no rock move |
| Authority | `StrikeCell` refuses unpainted solid; `TryPickDigTarget` no longer chips unmarked body traps |

Files:

- `Assets/Vibe/FreeMovement/ExcavatorTilePaint.cs` — `TryPickAccessibleWorkFront` + open BFS
- `Assets/Vibe/FreeMovement/FreeWorkerController.cs` — sync/tick/label/dig guards + `ExcavatedPathfinder` reuse

Unchanged by design: brush widths, dig cadence/balance, claustrophobia, graphics, stats, shift, priority, other jobs.

---

## Pending vs workable front

| Term | Meaning |
|------|---------|
| **Pending excavation cell** | Player-painted Valid/Excavating cell still solid |
| **Workable excavation front** | Pending cell with an open 4-neighbor stand that is reachable from the excavator through **open/excavated** floor only |

Selection scores open BFS distance to the stand (not world-space distance to rock).

---

## Regression checklist

1. Paint/excavate route A  
2. Paint route B reachable via existing tunnel → excavator **walks** to B, digs only painted cells  
3. Zero unpainted rock excavated  
4. Paint isolated order behind solid rock → **AWAITING ACCESS**, no drill toward it, no rock transit, order kept  
5. Selecting another worker / opening UI does not change this (Tick still AssignedWorker-gated)  
6. Continuous excavation along a connected painted face still works  

---

## RESULT: GREEN (source)

Invariants enforced in code paths above. Play Mode verify items 1–6 on SocketMap after compile.
