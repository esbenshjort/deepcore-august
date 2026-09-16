# Debris + Collapse + Injury + Rescue — Architecture / Status Audit

**Generated:** 2026-09-13 20:17  
**Scope:** Read-only codebase audit. No implementation, balance, or refactors.  
**Prior related:** `debris_collapse_v1_latest.md` (2026-09-07 offline sim; 37 pass / 2 fail), `injury_response_v1_latest.md`.

---

## Status matrix

| SYSTEM | STATUS | CURRENT IMPLEMENTATION | MISSING PIECES | RELEVANT FILES |
|--------|--------|------------------------|----------------|----------------|
| **1. DEBRIS** | **IMPLEMENTED BUT INCOMPLETE** | Blocking/Major: `DebrisField` + per-cell `_debrisHp` in world; blocks `IsTunnelOpen` / `IsMovementBlocker` / nav. Minor: FX + optional hit only — **no** world HP / nav block. | Minor debris as world state; dedicated debris AI “go clear”; orphan-cell sync beyond camp clear | `TunnelCollapse.cs`, `FineTerrainWorld.cs`, `FallingDebrisFx.cs` |
| **2. TUNNEL COLLAPSE** | **IMPLEMENTED BUT INCOMPLETE** | `TunnelCollapseSystem.TickNatural` ~0.55h eval; end-tunnel tips only; camp/yard/mid-corridor risk=0; severity Minor/Blocking/Major; warnings + banners; DEV force | Natural rate very rare; prior audit FAIL on dig-face tip as end-tunnel site; overnight no natural tick | `TunnelCollapse.cs`, runner `TickTunnelCollapse` |
| **3. STRUCTURAL SUPPORT / ENGINEER** | **IMPLEMENTED + WORKING** (prevention path) | Engineer builds supports via `MineInfrastructure`; quality-aware; `EvaluateInstability` + `RawRiskAt` reduced by support dist/quality; collapse soft-damages support condition | Collapse does not remove pillars; no explicit “stabilize after collapse” job beyond normal support pick | `MineInfrastructure.cs`, `EngineerPerson.cs`, `TunnelCollapse.RawRiskAt` |
| **4. WORKER INJURY (rock/collapse/falls)** | **IMPLEMENTED + WORKING** (typed path) | `CollapseInjuryResolver` zone/severity → `WorkerAccidentSystem.ApplyTypedInjury` (`TunnelCollapse` cause); falls via `WorkerAccident` / locomotion; Injury meter + Frustration events | Natural collapse injury rare in play (rate gated); critical rate disputed by V1 audit | `CollapseInjury.cs`, `WorkerInjury.cs`, `WorkerAccident.cs`, `WorkerLocomotion` |
| **5. INCAPACITATION** | **IMPLEMENTED + WORKING** | `WorkerState.Incapacitated`; set by collapse resolver + `InjuryResponse.AfterInjuryApplied`; blocks work (`CanPerform` / excavator mine); no soft-teleport home | Dual host/avatar position after rescue (see D) | `WorkerState.cs`, `InjuryResponse.cs`, runner commute/sleep |
| **6. TRAPPED WORKERS** | **IMPLEMENTED + WORKING** | `TrappedFromCamp` via camp↔pos BFS on `IsTunnelOpen`; commute/sleep skip; banners; claustrophobia env flag | False-positive edge cases near camp mitigated (≤3.25w); excavator “trapped” uses machine pos | `TunnelCollapse.RefreshTrappedFlags`, runner commute |
| **7. RESCUE** | **IMPLEMENTED BUT NOT INTEGRATED** | `TryRescueTick`: walk toward → carry toward camp → clear incap, keep injuries, `NeedsCare`, social memory | Does **not** yield Hauler/Engineer job hosts; moves **presence avatar only**; 8w proximity gate; incomplete access check stub; no post-rescue `SeekingStewardCare` | `TunnelCollapse.TryRescueTick`, runner `TickDebrisClearanceAndRescue` / `SetCrewWorldPos` |
| **8. DEBRIS CLEARING** | **IMPLEMENTED BUT INCOMPLETE** | Excavator: dig debris cells without paint (`DebrisStrike` → `TickClearance`); Hauler: **opportunistic** clear if within 2.4w + reachable from camp | No “path to debris” job; Hauler does not abandon haul to seek rubble; Engineer not a clearer | `TunnelCollapse.TickClearance`, `FreeWorkerController.StrikeCell`, runner hauler tick |
| **9. BLOCKED ACCESS / PATHFINDING** | **IMPLEMENTED + WORKING** | `_debrisHp>0` ⇒ not tunnel-open; `TunnelPathfinder` / Hauler / Prospector / Engineer corridor BFS all use `IsTunnelOpen`; paint open-BFS skips debris | Excavator escape through **unpainted rock** still blocked (by design) | `FineTerrainWorld`, `TunnelNavigation.cs`, `ExcavatorTilePaint.cs` |
| **10. STEWARD TREATMENT (serious)** | **IMPLEMENTED + WORKING** (camp path) | `StewardWoundCare.TryTend`: stabilize Serious+ (`StabilizedBySteward`), shave minor/moderate; Frustration/Morale relief; HelpedMe memory; skips still-incap patients | Needs patient **at camp** after rescue; no Steward travel into tunnel; rescue→Steward handoff not explicit | `CampLife.StewardWoundCare`, `StewardPerson.cs`, `InjuryResponse.cs` |

---

## Intended loop: does it exist?

| Step | Exists? | Notes |
|------|---------|-------|
| Poor/insufficient support → collapse risk | **Yes** | End-tunnel + instability − supports |
| → collapse / debris risk | **Yes** | Rare natural; DEV force solid |
| → debris physically blocks tunnel | **Yes** | Blocking/Major only |
| → worker may be hit / injured | **Yes** | Typed injuries + Injury events |
| → trapped or incapacitated | **Yes** | Flags + banners |
| → others cannot path through | **Yes** | Nav / jobs via `IsTunnelOpen` |
| → clear debris / create access | **Partial** | Excavator if chassis hits debris; Hauler only if already near |
| → rescue trapped/incap | **Partial** | Logic exists; **not** job-integrated |
| → serious injured to camp / assist | **Partial** | Mobile: `BeginInjuryReturnToCamp`; incap: rescue carry (avatar) |
| → Steward treats | **Yes** if at camp + not incap | Stabilize, not full heal |
| → persists in WorkerState / injuries | **Yes** | Meter, records, NeedsCare, recovery ticks |

**Verdict:** Core sim pieces of the loop are **implemented**. The **full player-facing end-to-end loop is not reliably playable** because clearance/rescue AI and host/avatar integration are incomplete. Natural collapse is also hard to observe without DEV force.

---

## Checklist answers

| Question | Answer |
|----------|--------|
| Excavator trapped behind collapse? | **Yes** — `TrappedFromCamp` if no camp path; machine stays. |
| Excavator excavate escape where appropriate? | **Debris: yes** (no paint required). **Rock bypass: only if player-painted.** Cannot invent dig routes. |
| Hauler clear debris? | **Yes, proximity-only** — no dedicated seek job. |
| Engineer prevent/reduce collapse? | **Yes** — supports lower risk / instability. |
| Serious debris → meaningful injuries? | **Yes** when hits resolve (Blocking/Major); wired to typed injury + Frustration. |
| Incap cannot walk home? | **Yes** — commute / soft-arrive skip. |
| Another worker physically assist/rescue? | **Logic yes; integration weak** — avatar MoveTowards, hosts keep jobs. |
| Rescue competes with normal work (no teleport)? | **Intent yes; practice no** — no yield/priority; parallel haul/clear; position dualism ≈ soft teleport of presence only. |
| Collapse blocks Hauler/Prospector/etc.? | **Yes** — shared `IsTunnelOpen`. |
| Debris in world state (not just visual)? | **Blocking/Major: yes** (`_debrisHp`). **Minor: visual/injury only.** |
| Pathfinding updates correctly? | **Yes** on set/clear debris (Notify). |
| Collapse × painted excavation routes? | **Mostly yes** — debris diggable; open BFS treats debris as non-open; pending paint still required for rock. |
| Injuries → Injury / NeedsCare / Frustration? | **Yes** via `ApplyTypedInjury` + hub. |
| Treatment → Steward? | **Yes** at camp (`StewardWoundCare`). |
| Events → WorkerStateEvent / Social Memory? | **Injury WSE yes**; collapse → SocialMemory (`SurvivedCollapse`, `WasTrapped`, `RescuedByWorker`); **no dedicated Collapse WSE type**. |
| Shift/camp cycle trapped/injured? | **Mostly yes** — no teleport home; reduced sleep recovery; trapped flags refresh overnight; natural collapse **OnShift only**. |

---

## A. What already works end-to-end

- **World debris blocking:** `SetBlockingDebris` ↔ nav / jobs / excavator paint open-BFS.
- **Engineer supports → lower end-tunnel risk** (when tip classification + instability fire).
- **Forced collapse (DEV):** debris field, injuries, incap rolls, trapped flags, banners.
- **Excavator debris chip** when dig body hits debris cells (`DebrisStrike`).
- **Typed collapse injuries** → Injury meter, `NeedsCare`, Frustration via `WorkerStateEventType.Injury`, InjuryResponse hub.
- **Incapacitated / trapped stay at site** through commute, soft-arrive, morning dispatch.
- **Steward stabilize Serious+** without wiping fractures (Injury Response V1).
- **Social memory** for collapse / trap / rescue; dialogue topics exist.
- **Camp-zone debris auto-clear** on boot / shift start (yard soft-lock prevention).

## B. What exists but is disconnected

- **Rescue tick** vs **Hauler/Engineer hosts** — `SetCrewWorldPos` updates presence only; haul/dig AI continues.
- **Hauler clearance** vs haul AI — opportunistic `TickClearance` while host may be elsewhere on a path.
- **Post-rescue Steward handoff** — sets `NeedsCare` but not `SeekingStewardCare` / injury-return; excavator `CrewWorldPos` after `ClearIncapacitated` may snap to **machine** underground → Steward `PatientAtCamp` can miss.
- **`TryRescueTick` access stub** (lines ~911–914): `IsReachableFromCamp` check commented incomplete; reliance on `HasOpenPath(a,b)` only.
- **Minor debris** — field/FX without nav authority.
- **Claustrophobia / dialogue** can see trapped/collapse flags without rescue competing for labour.

## C. What is completely missing

- Dedicated **“seek and clear debris”** / **“mount rescue”** job states (priority vs haul/support/dig).
- **Rescuer host locomotion** synced to rescue (or explicit yield + avatar-as-authority for duration).
- **Engineer as clearer** (only Hauler opportunistic + Excavator dig).
- **Player order** to clear / rescue (all automatic proximity).
- **WorkerStateEvent** type for collapse/trap/rescue (Injury + SocialMemory only).
- Guaranteed **natural** collapse observability in short play sessions (by design rare + end-tunnel gate).
- Escape **around** blockage through rock without paint (correctly absent — do not invent).

## D. Bugs / architectural conflicts discovered

1. **Rescue dual-body:** `TryRescueTick` + `SetCrewWorldPos` move avatars; Hauler/Engineer/Excavator transforms keep working → rescue is not labour competition.
2. **Post-incap excavator position:** `CrewWorldPos` prefers host when not incapacitated → clearing incap can re-bind casualty to machine still behind debris while avatar was hauled to camp.
3. **Hauler clear without pathing to rubble:** only clears if already within 2.4w of epicenter.
4. **Rescue 8w gate:** farther casualties never start rescue even if path open.
5. **Natural risk tip classification:** prior `DebrisCollapseV1Audit` FAIL — dig-face tip not always `IsEndTunnelSite` → risk stays 0 → natural loop silent.
6. **Critical trauma sample rate:** prior audit FAIL vs design intent (too many crit in forced samples — balance/audit conflict, not wiring absence).
7. **Overnight:** trapped refresh only; no natural collapse / clearance / rescue while off-shift.

## E. Smallest implementation sequence (to complete the loop)

Do **not** add parallel systems — extend existing `TunnelCollapseSystem` + runner job hosts.

1. **Verify natural tip risk** (fix `IsEndTunnelSite` / worker-consider ring) so unsupported dig-face can leave Stable — else loop never fires without DEV.
2. **Hauler: yield haul → path to nearest clearable field** (camp-side) → `TickClearance` until open; then resume haul.
3. **Rescue: yield Hauler (else Engineer) job** → path with real host/`TunnelPathfinder` to casualty → carry with host+avatar synced → on camp arrival set `SeekingStewardCare` + keep injuries.
4. **Fix post-rescue position authority** for excavator casualty (avatar/camp until fit; don’t snap to machine while NeedsCare).
5. **Optional thin:** Engineer secondary clearer; raise rescue range or queue; DEV already covers force tests.
6. **Playmode prove** forced Blocking → trap → Hauler clear → rescue → Steward stabilize → shift cycle.

---

## Classification of the “full intended loop”

| Overall | **IMPLEMENTED BUT NOT INTEGRATED** (sim + Steward solid; clearance/rescue AI + host sync incomplete; natural fire fragile) |

**Do not duplicate** `TunnelCollapseSystem`, `CollapseInjuryResolver`, `StewardWoundCare`, or debris `_debrisHp`. Integrate jobs into what already exists.
