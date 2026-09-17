# Assign / Work Priorities — Management UI visual testbed

**Generated:** 2026-09-17  
**Scope:** Visual language only on the Priorities HUD popup. No gameplay, assignment, or resolver changes.

## Direction

Leave cyberpunk / chamfered HUD. Move to **clean + readable + modern + transparent + subtle neon blue** — premium management overlay over the mine.

## What changed

| Area | Change |
|------|--------|
| `DeepCoreBentoUi.cs` | New cached IMGUI primitives: rounded fills (9-slice), transparent charcoal panels, selected-row fill, priority chips, RESPOND controls, label cache |
| `DrawPrioritiesPanel` | Restyled Assign / Work Priorities testbed using BentoUi only (not `DrawCyberPanel`) |
| `WorkTaskRegistry.CollectForJob` | Presentation helper — profession-scoped task list for the detail section |

## Visual rules applied (this panel only)

1. **Background** — near-black / charcoal transparency (`OverlayBg` α≈0.42); mine stays visible  
2. **Corners** — consistent rounded radii (main 8 / module 6 / control 5)  
3. **Borders** — single subtle panel stroke; subsections use spacing + type, not nested boxes  
4. **Color** — one cyan-blue accent for selection / active controls; primary text off-white; secondary muted grey  
5. **Selected** — translucent blue fill + thin left accent edge (no double outlines)  
6. **Controls** — rounded translucent chips / RESPOND buttons  
7. **Perf** — rounded masks + label styles cached; no per-frame texture create; no blur  

## Explicitly not changed

- Worker roster cards / top bar / other HUD (still cyber helpers)  
- Priority resolver, hosts, TemporaryYield, claims  
- Assignment manager / JobType authority  

## Evaluate in Game view

Open **PRIORITIES** and judge:

- overall transparency  
- readability / hierarchy  
- rounded panels  
- selected states  
- priority chips + RESPOND  
- information density  
- neon accent strength  

**Stop here** — do not propagate style globally until this panel is approved.
