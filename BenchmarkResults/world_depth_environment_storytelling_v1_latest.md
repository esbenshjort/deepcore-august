# Deep Core — World Depth + Environmental Storytelling V1

Generated: 2026-09-07  
Baseline: Environment Graphics Overhaul V1 (Point atlases, Ppu=8, fractured lips) — **preserved, not redone**.

Goal: physical depth, geological variation, excavation history, machinery grounding, lighting hierarchy, lived-in camp — systemic, not screenshot polish.

---

## 1. Existing systems reused

| System | Role |
|---|---|
| `FreeMovementTerrainView` | Extended (not replaced) Point floor/wall atlases |
| `FineTerrainWorld` | Excavate + RegionChanged; added open-generation stamps |
| `LogisticsTrafficMap` | Hauler visit scores → floor wear |
| `DigVisualKit.StampRubbleChip` | Lip chips, border rubble, large formations |
| `YardVisualKit.SleepPad` / `PowerCable` | Grounding pads + utility cables |
| `ExcavationDustFx` | Ambient grit near fresh dig faces |
| `IndustrialSteamPlume` / camp fire / washer steam | Existing life (unchanged rates) |
| `RockWallShadows` | Headlamp occlusion (unchanged) |
| `CampSleepSite` / toilet / prospector desk / washer | Grounding + light identity only |

---

## 2. Graphical systems changed

| File | Change |
|---|---|
| `FineTerrainWorld.cs` | `_openGen` stamps; `ExcavationAge01` (0=fresh … 1=settled) |
| `FreeMovementTerrainView.cs` | Wall volume layers, geo patches, history, traffic, camp wear, large formations |
| `LogisticsTrafficMap.cs` | `CellVisited` → terrain dirty for wear |
| `DigVisualKit.cs` | `PlaceGroundingPad`, `PlaceUtilityCable`; lantern identity tightened |
| `CrewVisualKit.cs` | Excavator headlamp = harsher work light |
| `HelmetFlashlight.cs` | `RetuneFromLight` so identity sticks |
| `WashMachine.cs` | Wet apron + amber process light; cyan hood slightly quieter |
| `Stockpile.cs` / `BasecampYard` | Extra grounding + cables at wash bay |
| `CampSleepSite.cs` | Contact wear; warmer/softer fire falloff |
| `CampToiletSite.cs` | Grounding under stall |
| `ProspectorWorkstationSite.cs` | Cyan tech spill + pad/cable |
| `FreeMovementSocketMapRunner.cs` | `BindPresentation` + `TunnelAmbientFx` |
| `TunnelAmbientFx.cs` | **New** — sparse grit on fresh dig faces |

---

## 3. Assets created

No imported PNGs. All presentation remains runtime/procedural so dig-forward mine inherits quality.

Reusable helpers: grounding pads, utility cables, geo patch sampler, large-formation stamps, open-generation age.

---

## 4. Rock depth solution

Painted wall structure (still no second mesh pipeline):

1. **Fractured lip** (depth &lt; 0.32) — lit facets, cracks, shelf hints, overhang bias in SDF  
2. **Wall face** (0.32–0.85) — darker volume + strata bands  
3. **Contact/occlusion** (0.85–1.55) — strong darkness at floor join  
4. **Silhouette** (&gt;1.55) — whisper of rock into darkness (`WallRevealCells` 2.85 → **3.25**)

Lip chips + sparse large fault/slab stamps on dig-adjacent solid.

---

## 5. Geological variation solution

Macro `SampleGeo` patches (not per-tile noise):

- Compact earth · Fractured · Hard/darker stone · Clay · Damp · Mineral stain  

Applied to floor + wall with restrained palette shifts. Hidden ore/diamond paint rules unchanged (no discovery spoilers).

---

## 6. Excavation-history solution

`FineTerrainWorld` stamps monotonic open generation on excavate.

Terrain reads `ExcavationAge01`:

| Age | Floor | Border rubble |
|---|---|---|
| Fresh | Warmer scars, more rock fragments | More chips |
| Settled | Darker compacted dirt | Fewer chips |
| High traffic | Wear strip / compaction | Debris reduced |
| Camp radius | Grimy lived-in apron | — |

Hauler `CellVisited` dirties cells; camp band soft-refreshes ~5.5s when traffic bound.

---

## 7. Machinery grounding

- Wash platform: contact dirt + wet apron + utility cables  
- Washer: grounding pad + amber **process** light (cyan hood remains tech accent, quieter)  
- Camp fire / tent apron / crates / toilet / prospector table: contact pads  
- Machines/sprites not remade

---

## 8. Lighting hierarchy

| Source | Identity |
|---|---|
| Excavator | Harsher cooler-amber cone, harder falloff, stronger shadows |
| Industrial lantern | Controlled amber, tighter outer/falloff |
| Campfire | Warmer orange, softer organic falloff + existing flicker |
| Scanner / prospector table | Restrained cyan |
| Washer process | Amber work pool under machine |
| Washer hood / tent cyan / power LED | Tech accents (kept, not amplified) |

Global darkness preserved (~0.025). No bloom pass.

---

## 9. Camp changes

- Terrain camp influence darkens/wears apron  
- Extra grounding under fire, tent path, crates, toilet  
- Fire light identity separated from lanterns  
- Prospector desk grounded + cable  
- Steward/sleep/steam systems reused as-is  

Camp should read as **lived-in industrial pad** vs fresh cut tunnel.

---

## 10. Performance impact

| Item | Notes |
|---|---|
| Atlas size | Unchanged (still Ppu=8 Point) |
| Open-gen array | +2 bytes/cell |
| Large formations | Sparse hash (&gt;0.965) stamp pass in rebuild rect |
| Traffic dirty | Local OnRegion only |
| Camp refresh | ~5.5s, only if traffic bound |
| Ambient grit | 1 timer, ≤1 dust burst / 2.4–5.2s |
| Extra GOs | Few grounding pads/cables at camp (fixed, not per-tile) |

No per-decoration Update farms; no second rendering pipeline.

---

## 11. Before / after assessment (expected)

| Check | Result |
|---|---|
| 1 Physical tunnel depth | **Improved** — lip/face/contact/silhouette |
| 2 Geological richness | **Improved** — coherent patches + landmarks |
| 3 Material separation | **Improved** — floor history vs wall volume |
| 4 Machinery grounding | **Improved** — pads/cables/wet apron |
| 5 Lighting hierarchy | **Improved** — excavator ≠ lantern ≠ fire ≠ cyan |
| 6 Environmental history | **New** — fresh vs settled vs traffic vs camp |
| 7 Worker readability | **Preserved** — workers untouched; terrain quieter hierarchy |
| 8 Visual noise | Controlled — large forms sparse; no prop scatter |
| 9 Pixel consistency | **Preserved** — Point / Ppu 8 baseline |
| 10 Overall cohesion | Toward “one workplace being excavated and inhabited” |

Validate in Play: camp · washer · excavator face · fresh dig · older corridor · darkness edge — at close / normal / tactical zoom.

---

## 12. Remaining visual weaknesses

- Shadow casters still cell-square vs organic paint  
- Excavation age is generation-relative (not clock hours) — fine for session storytelling, not calendar aging  
- Engineer supports/cables not dynamically painted into tunnels (infra sprites may exist separately)  
- No true multi-layer wall mesh / parallax  
- Far tactical zoom still shows large cells; Point may look blockier  
- Job traces beyond excavator/hauler/refiner/camp are light-touch only  

---

## Do-not checklist (honored)

- No gameplay redesign / map layout change  
- No graphics-overhaul redo / machine remakes  
- No global brighten / bloom / outline-everything  
- No hidden-resource exposure  
- No screenshot-only decoration / duplicate render pipeline  

**STOP.**
