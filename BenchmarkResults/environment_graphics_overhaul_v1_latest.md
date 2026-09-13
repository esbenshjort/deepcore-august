# Deep Core — Environment Graphics Overhaul V1

Generated: 2026-09-07  
Goal: **One coherent industrial pixel-art visual language** — not brighter, not redesigned gameplay.

---

## 1. Visual problems found (pre-change)

From gameplay screenshot + pipeline audit:

| Issue | Cause |
|---|---|
| Soft / blurry excavated rock walls | Terrain atlases: **4 texels/cell** + **`FilterMode.Bilinear`** |
| Sharper loose rocks & machines | Point-filtered procedural sprites (32–96 px) |
| Noisy floor carpet | High-frequency dither on low-res bilinear floor |
| Soft dig-face “airbrush” edges | Large SDF jitter + smoothstep alpha fade |
| Inconsistent ore glints | `GoldVeinShine` Point vs `OreShimmer` Bilinear |
| Machines “on” dirt, not “in” world | Strong Point props on soft terrain; weak contact grounding |
| Lighting amplifying mismatch | Soft walls + warm lanterns → mushy lit cliffs |

**Reference fidelity (kept):** `YardVisualKit` machinery, `MakeLooseRockChunk` / `LoosePile`, `CrewVisualKit` workers.

---

## 2. Rendering / import inconsistencies

| Layer | Was | Target |
|---|---|---|
| Floor / wall atlases | Bilinear, Ppu=4 | **Point, Ppu=8** |
| Loose rock chips | Point 32 | Unchanged (reference) |
| Machines / crew | Point | Unchanged |
| OreShimmer | Bilinear 10 | **Point** |
| DigVisualKit.Pixel / blobs | Bilinear | **Point** |
| Dust / steam / lantern glow | Bilinear soft | Kept soft (FX, not structure) |
| Camera | Ortho 10, no PixelPerfect | Unchanged (gameplay zoom) |
| Assets | 0 imported PNGs — all runtime | Still runtime |

No Tilemap / custom terrain shader — CPU-painted `Texture2D` atlases remain the architecture.

---

## 3. Art standard established

**DEEP CORE environment standard (V1)**

- **Pixel scale:** Apparent density matched to Point loose-rock / machinery at normal ortho 10.
- **Edge language:** Fractured hard silhouettes with controlled irregularity (tight SDF jitter).
- **Texture:** Geological variation without equal-noise carpet.
- **Light:** Dark mine preserved; richer local industrial pools (slightly tighter lantern falloff).
- **Color:** Earth brown/black geology; cyan/amber/green accents on interactables only.
- **Detail hierarchy:** Workers/machines/hazards/resources → loose rock / wall lips → ordinary floor / far rock.

---

## 4. Systems / assets changed

| File | Change |
|---|---|
| `FreeMovementTerrainView.cs` | Full terrain overhaul (Ppu, Point, lip/depth, floor, wall chips) |
| `DigVisualKit.cs` | Pixel + blob sprites → Point; lantern outer/falloff tuned |
| `OreShimmer.cs` | Spark filter → Point |
| `Stockpile.cs` / `BasecampYard` | Contact dirt pads under wash platform |

**Not changed:** Crew art, washer/platform sprites, loose-pile chip generator, gameplay, map layout, worker systems.

---

## 5. New assets created

None imported. All improvements are **procedural / runtime** so quality applies throughout the mine, not only the camp screenshot.

Reusable stamps:
- Existing `StampRubbleChip` (now on Point atlas)
- New wall-lip chip pass (`StampWallLipChips`)

---

## 6. Terrain changes (Rounds 3–6)

1. **Ppu 4 → 8** — 2× linear density (4× texels); matches chip detail at gameplay zoom.
2. **FilterMode.Point** — kills soft bilinear mush vs machinery.
3. **Harder dig-face SDF** — jitter 0.22 → 0.12; less Perlin mush.
4. **Wall depth layers:** fractured lip → dark contact → rock mass → outer fade.
5. **Cracks / protruding stones** on lip; sparse wall-lip rubble stamps.
6. **Floor:** lower-frequency compact dirt; disturbed near-wall band; harder floor edge alpha.
7. **Border rubble** retained (palette-aligned with loose rock).

---

## 7. Geology / resources (Round 7)

- In-wall gold/diamond paint retained (embedded, not stickers).
- Ore shimmer Point-aligned with vein shine.
- No discovery spoiler changes.

---

## 8. Machines + camp (Round 8)

- Machinery sprites **not remade**.
- Added **contact dirt** under wash pad (sorting 9) so washer reads installed.
- Terrain now supports machine fidelity instead of competing with it.

---

## 9. Lighting (Round 9)

- Lantern: outer **3.15 → 2.95**, falloff **0.7 → 0.82**, slightly larger inner.
- Darker outside pools; lit dig faces richer (opaque lip).
- No bloom, no global brighten, headlamp shadows unchanged.

---

## 10. Pixel / rendering consistency (Round 10)

| Setting | Decision |
|---|---|
| Terrain filter | Point |
| Terrain PPU density | 8 / cell |
| Soft FX (dust/steam/glow) | Bilinear kept |
| PixelPerfectCamera | Not added (would fight ortho follow) |

---

## 11. Environmental polish (Round 11)

- Wall-lip chips + existing dig haze / foot dust / steam left as subtle motion.
- No new particle storm.

---

## 12. Performance impact

| Item | Impact |
|---|---|
| Atlas size | ~4× pixels (360×300×8 → 2880×2400 ×2) ≈ **~55 MB** RGBA vs ~14 MB |
| Dig rebuild | Region-based; cooldown **0.1s** (was 0.08) |
| GameObjects | No per-cell wall objects; still 2 atlases |
| Risk | First full rebuild slightly slower; incremental dig OK |

If memory becomes tight on low-end, fall back to Ppu=6 is a one-constant change.

---

## 13. Before / after assessment

| Criterion | Expected after |
|---|---|
| 1 Wall sharpness | **Much improved** (Point + lip) |
| 2 Apparent pixel density | **Aligned** with loose rock / machines |
| 3 Floor/wall separation | **Clearer** hard edge + contact dark |
| 4 Loose-rock integration | **Better** (shared Point + palette) |
| 5 Machinery grounding | **Improved** (contact dirt + coherent floor) |
| 6 Lighting cohesion | **Slightly richer** pools, dark preserved |
| 7 Visual noise | **Reduced** on floor |
| 8 Worker readability | **Preserved** (workers unchanged, terrain quieter) |
| 9 Geological richness | **Improved** lip/cracks/strata |
| 10 Overall consistency | **Primary goal addressed** |

Validate in Play at close / normal / tactical zoom after map rebuild.

---

## 14. Remaining visual weaknesses

- Shadow casters still **cell-square** vs organic painted cliffs (light occlusion ≠ paint).
- Soft dig haze / steam still Bilinear (intentional FX softness).
- No true parallax / multi-layer wall mesh — depth is painted, not geometry.
- Tactical far zoom still shows large cells; Point may look blockier than Bilinear (acceptable trade for coherence).
- Camp toilet / tent could get the same contact-dirt treatment later.
- Full-mine atlas memory at Ppu=8 — monitor low-end.

---

## 15. Do-not checklist (honored)

- No gameplay / map redesign  
- No worker system changes  
- No global brighten / bloom  
- No outlines everywhere  
- No screenshot-only decoration  
- No duplicate rendering system  

**STOP.**
