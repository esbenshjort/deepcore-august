# Assign / Work Priorities — Management UI visual testbed

**Generated:** 2026-09-17 (rev 2)  
**Scope:** Visual + layout collision only on the Priorities HUD popup.

## Rev 2 (after playtest feedback)

Problems in rev 1 screenshot:
- Panel too transparent → Mission “NOT FOUND” bled through
- Mission HUD still drew under the Priorities panel
- Still read as HUD (ALL CAPS, cyan frame, dense tech copy)

Fixes:
- Panel opacity raised to **α 0.90** (no bleed)
- **Mission HUD hidden** while any exclusive HUD popup is open
- Panel kept clear of the right tool strip (`HudToolStripReserve`)
- Dropped cyan panel border → quiet neutral hairline
- Title case / larger type / more padding / quieter accents
- Selection = soft blue wash only (no outline box)

## Still testbed-only

Roster cards, top bar, and tool strip remain on the old cyber helpers until this panel is approved.
