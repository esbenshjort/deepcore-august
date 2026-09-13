# Deep Core Dialogue Bible + Content V2.1

**Status:** Content + voice pass complete. V2 Social Dialogue architecture preserved (no Aura math / frequency / relationship / matching redesign).

Generated: 2026-09-08

---

## Verdict

Crew dialogue catalog is now large enough and specific enough to carry DEEP CORE’s hardcore, dry, dark-funny voice. Paired exchanges remain the unit of truth; anti-repetition, situation gates, knowledge, and nickname **evidence** still apply. Final nicknames are still not activated.

| Metric | Value |
|---|---|
| Exchange families | **189** |
| Authored utterance variants (init + replies + closers) | **~1103** |
| Target | ≥150 families / ≥500 utterances |

---

## What was added

### Catalog
- New partial: `Assets/Vibe/FreeMovement/SocialDialogueExchangesBible.cs` (`BuildBibleV21`)
- Wired from existing `EnsureBuilt()` after original `Build()` (~48 V2 seed families retained)
- Seed library adapted into paired `Add(...)` families across:
  - ordinary tunnel banter, darkness, narrow / confinement
  - friend tease (memory-gated `WasTrapped`, bonded — not NeedsWeak; V2 weak gate stays hostile/rival)
  - hostile confinement weaponization
  - excavation, prospecting, failed prospecting, discovery / gold / diamonds
  - engineering / supports, engineer↔excavator rivalry
  - hauling, refining, camp / steward-adjacent
  - bad/good food, toilet / stomach
  - exhaustion, injury, genuine concern
  - overtime / manager pressure
  - collapse, rescue, trapped
  - competence / strength recognition (NeedsStrength)
  - friendship, rivalry-with-respect, strained, grudge / cruel
  - death aftermath (weight **0.4**, restrained)
  - boredom / absurdity + **Gerald** running-joke callbacks (low weight, Callback topic)

### Voice rules applied
- Hardcore / dry / dark / funny / occasionally warm or brutal
- Silence as reply: `…`, `No.`, `Mm.`, `Fuck off.`, etc. (~160 silence-capable reply strings in catalog)
- Swearing present but not every line
- Avoided generic NPC cheer (“Great job”, “good team”, “let’s keep going”)
- Collapse beat explicitly rejects “that was close” as a line

### Nickname evidence (still evidence-only)
- `SurvivedCollapse` → Courage / Steady strength bumps + NarrowPassageNerve / SharedEmergency evidence
- `ObservePhysicalWork` → PhysicalPower + PhysicalReputation
- `ObserveRunningJokeFuel` on Callback picks → RunningJoke evidence
- No final nickname generation

### Audit harness
- `SocialDialogueBibleV21Audit.cs` — 10 social days × RelClass cycle + situation variants
- Menu: **DeepCore / Diagnostics / Run Dialogue Bible V2.1 Audit**

---

## Architecture untouched

- Social Aura encounter frequency / math
- Exchange matching, anti-repetition windows, situation topic gates
- Weak-point / strength learning model (single primary weak/strength node)
- Relationship formulas
- No runtime LLM, no dialogue trees, no final nicknames

---

## Audit — 10 simulated social days

Offline catalog simulation mirroring V2 pick rules (action match, RelOk, situation AllowTopic, history windows, weak/strength/memory gates, weighted pick, response + soft fallback). RelClasses cycled: Friendly → Bonded → Professional → Rivalry → Strained → Grudge (×10 days). Situation packs: confinement/dark/deep/exhaustion; injury/food/toilet; discovery/manager/collapse memory; trapped; good food/depth.

| Report field | Result |
|---|---|
| Exchange families | 189 |
| Utterance count | ~1103 |
| Beats sampled | 360 (10 × 36) |
| V2 paired hit rate | **74.4%** (268) — remainder falls through to legacy bank in live Aura |
| Exact adjacent initiator repeat rate | **0.0%** |
| Repeated-topic adjacent rate | **0.0%** (topic window working) |
| Incompatible response count | **0 wrong-exchange**; ~16% soft response-type fallback when exact `SocialResponse` missing on that family (V2 CompatibleFallback — still same exchange) |
| Impossible-context / null pool | **25.6%** — mostly Provoke/Confront on Friendly/Bonded days with thin non-hostile pools; live path uses legacy fallback |
| Humour / joke-action rate | **20.0%** |
| Positive/warm rate | **10.8%** |
| Hostile-gated rate | **4.2%** |
| Severe cruelty rate (hostile + NeedsWeak) | **0.6%** |
| Silence / non-witty reply rate | **33.9%** |
| Callback count | 10 |
| Strength recognition count | 9 |
| Weakness callback count | 2 (hostile RelClass days only, by design) |

### Manual coherence (≥100 resolved pairs inspected via sample)

Read as dialogue: initiators and replies stay on-topic within families. Friend confinement teases need `WasTrapped` memory + bonded/friendly rel. Hostile fear lines only under Rivalry/Strained/Grudge. Death families stay sparse and quiet. Gerald callbacks appear rarely and do not spam every shift.

Representative pairs:

- A: `I miss weather.` / B: `I don't.`
- A: `Hard layer.` / B: `I noticed.`
- A: `So?` / B: `Means maybe.`
- A: `I named that one. That's Gerald.` / B: `It's a rock.`
- A: `You know they make machines for that.` / B: `I am the machine.`

---

## Remaining thin content domains

Still thinner than Work / General / Prospecting (quality stop preferred over filler):

| Domain | Families (approx) | Note |
|---|---|---|
| Geology | 3 | Expand later with seam/fault specifics |
| Depth | 4 | OK starter set |
| Confinement (topic) | 4 | Much coverage also under NarrowTunnel / WeakPoint |
| Collapse | 4 | Intentionally sparse |
| Trapped | 4 | Intentionally sparse |
| Manager | 5 | Could use more push/rest variants |
| Discovery / diamonds | 8 | Solid; more rare-beat variants later |
| Death aftermath | 3 @ 0.4 weight | Keep rare |

Haul / refine / toilet / rescue / darkness are no longer critically thin after V2.1 adds.

---

## Quality notes / follow-ups (not blocking)

1. Unity Editor audit menu writes `BenchmarkResults/deep_core_dialogue_bible_v21_audit_raw.md` for live C# confirmation.
2. Impossible-context rate on Provoke/Confront for warm RelClasses is expected; consider a few more non-hostile Confront/Complain families later if legacy fallback feels too generic.
3. Friend tease uses **memory** not NeedsWeak — matches V2 pick gate (weak weaponization remains rival/grudge).

---

## Files

- `Assets/Vibe/FreeMovement/SocialDialogueExchanges.cs` — partial, utterance count API, Callback → RunningJoke fuel
- `Assets/Vibe/FreeMovement/SocialDialogueExchangesBible.cs` — V2.1 catalog
- `Assets/Vibe/FreeMovement/SocialDialogueV2Core.cs` — SurvivedCollapse evidence, ObservePhysicalWork, ObserveRunningJokeFuel
- `Assets/Vibe/FreeMovement/SocialDialogueBibleV21Audit.cs`
- `Assets/Vibe/FreeMovement/Editor/WorkerV11LockAuditMenu.cs` — menu entry

**STOP** — content + voice pass complete; architecture locked.
