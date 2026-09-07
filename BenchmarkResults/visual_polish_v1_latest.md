# Visual Polish V1

Generated: 2026-09-05

## KEEP (preserved — extended, not replaced)

| System | Role |
|--------|------|
| DigVisualKit / CrewVisualKit / YardVisualKit | Art language |
| ExcavationDustFx / FootstepDustFx / FallingDebrisFx | Dust family |
| IndustrialSteamPlume | Wash + camp AC (+ new meal steam) |
| DrillerVisual + DrillSparkPuff | Dig / heat / sparks |
| ApplyDayNightLight + CosyLantern + headlamps | Lighting hierarchy |
| UiCyan/Green/Amber/Red + roster badges + social speech | UI language |
| Prospector cone / scanner / tactical | Prospecting FX |
| CampSleepSite / CampToiletSite | Camp props |

## IMPROVED

1. **WorkerAvatar** — walk bob; injury/exhaust/incap tint; IdBead status color; stumble tilt + footstep dust burst (reads `WorkerLocomotion` + `WorkerState`)
2. **CampSleepSite** — night quiet (steam/fan/fire/power dim when asleep); meal cook-steam + fire punch on real `ApplyMealToCrew`
3. **Night ambience** — `_mineAmbience` volume/pitch ducks when asleep / evening
4. **Engineer repair** — cyan/amber sparks while repairing; restart spark burst on overheat clear

## NEW (thin extensions only)

- `EquipmentSparkFx.cs` — shared repair/restart sparks using existing `DrillSparkPuff` + `DigVisualKit.Pixel` (no second particle stack)

## DUPLICATES AVOIDED

- No new ParticleSystem / bloom volume / lantern type
- No parallel injury FX system
- No second camp meal simulation
- No UI redesign — palette unchanged

## FILES CHANGED

- `WorkerAvatar.cs` — presence polish + `BindWorker`
- `CampSleepSite.cs` — night quiet + meal pulse
- `EngineerPerson.cs` — repair/restart sparks
- `FreeMovementSocketMapRunner.cs` — bind avatars, meal notify, day/night ambience
- `DrillerVisual.cs` — `DrillSparkPuff` public for shared use
- `EquipmentSparkFx.cs` — **new**

## REMAINING WEAKNESSES (not V1)

- Host roles (excavator/hauler bodies) lack the same bob/limp as off-duty avatars while Operating (avatars hidden)
- No dedicated limp sprite set
- Toilet / wound-care still mostly logic + HUD
- Prospector anomaly world ping still optional
- Geology decoration (cracks/glints) left alone to protect prospecting readability

## STOP

Visual Polish V1 complete — major gaps addressed without art-direction change or simulation balance edits.
