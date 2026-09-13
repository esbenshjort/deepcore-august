# Mission 01 — 14 Day Prospecting Test

**Implemented** on SocketMap.  
**DEV audit:** DeepCore → Diagnostics → Run Mission 01 Prospecting Audit  
**Reports:** `BenchmarkResults/mission01_prospecting_latest.md` (from audit) · this design note

> Unity Editor currently has the project open, so batch audit could not auto-run. Use the menu item above after compile to refresh PASS/FAIL.

---

## Objective

Find **Gold** and **Diamonds** (refined ≥1 each) within **14 in-game days**.

| Result | Condition |
|--------|-----------|
| MISSION COMPLETE | Both FOUND on or before Day 14 |
| MISSION FAILED | Day 14 shift end (18:00) without both |

Pacing: excellent Day 9–10 · good Day 11–12 · wrong turns Day 13–14 · random dig can fail.

FOUND = refined stockpile (HUD GOLD / DIA), not raw ore.

---

## Map design

**Base:** existing SocketMap organic mountain (bedrock lobes, soft corridors, early gold/diamond teases, gas field).

**Mission overlay** (`Mission01Geology.Apply`):

| Feature | Approx. (spawn `sx,sy`) | Role |
|---------|-------------------------|------|
| West / East / Mid junctions | `sx±42,sy+48` · `sx,sy+78` | Soft decision network |
| Direct-north bedrock ridge | `sx±14 @ sy+38` + plug `@ sy+52` | Tempting straight dig fails |
| Deep Gold chamber | `sx+38, sy+102` (16×12) | Mission GOLD |
| Deep Diamond chamber | `sx-58, sy+106` (14×12) | Mission DIA |
| Diagonal gas pockets | `sx±22 @ sy+88/90` | Tempting shortcuts |
| Near-chamber bedrock plugs | approach flanks | Wrong last-mile cuts |

### Soft approaches (≥2 each, validated by disjoint BFS)

- **Gold:** east trunk flank · mid→NE link (extra soft widen if needed)
- **Diamonds:** west trunk flank · mid→NW link

Junction reconnects **around** the north ridge (`sx±24, sy+68`) — soft paths never require punching the plug.

Prospecting stays interpretive: mineralisation / continuity / hazard-leaning clues only — no painted safe path.

---

## UI

Mission strip:

- GOLD: FOUND / NOT FOUND  
- DIAMONDS: FOUND / NOT FOUND  
- DAYS REMAINING: X  
- COMPLETE / FAILED banners  

DEV panel **MISSION**: validation summary · ROUTE OVERLAY · REVALIDATE  

---

## Files

- `Mission01Geology.cs` — layout + soft-path validation  
- `Mission01Runtime.cs` — day / found / win-lose  
- `Mission01DevOverlay.cs` — DEV routes  
- `Mission01Audit.cs` — geology audit → BenchmarkResults  
- `GoldVeinPlacer.cs` — public fill / gas helpers  
- `FreeMovementSocketMapRunner.cs` — build, clock sync, HUD  
- Editor menu entry under DeepCore/Diagnostics  

---

## Verify (audit checklist)

- [ ] Both targets soft-reachable without bedrock/gas  
- [ ] ≥2 soft approaches each  
- [ ] Direct north blocked  
- [ ] Tempt shortcuts hazardous  
- [ ] Soft distance not trivial (≥70)  
- [ ] Dig / prospect / haul / wash / shift systems unchanged  

---

## Loop

**PROSPECT → INTERPRET → CHOOSE ROUTE → EXCAVATE → REASSESS**

No new mechanics — layout + objective only.
