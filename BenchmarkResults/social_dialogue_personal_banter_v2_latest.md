# Social Dialogue + Personal Banter V2

Generated: 2026-09-08  

Built on existing Social Aura presentation path (`SocialAuraLineBank` → `SocialAuraPresenter` → `WorkerBanter.TrySaySocial`).  
**No second dialogue system. No Social Aura frequency/math retune. No final nicknames.**

---

## Problems found (pre-V2)

| Issue | Cause |
|-------|--------|
| Unrelated replies | Initiator and response picked from **independent** pools |
| High repetition | No recent-line / topic / exchange suppression; 78% flavor bias amplified repeats |
| Weak context | Only `SocialContext` + RelClass; no live tunnel/dark/confinement gating |
| Interchangeable voices | Thin Soul hooks in wording; conflict banks personality-blind |
| No weak-point / strength language | Knowledge unused; nickname evidence unused by dialogue |
| Callbacks rare/generic | Few memory-type-specific lines |

---

## Systems added / changed

| File | Role |
|------|------|
| `SocialDialogueV2Core.cs` | Topics, situation flags, dialogue history, person knowledge (weak/strength) |
| `SocialDialogueExchanges.cs` | **~43 curated exchanges** with compatible reply sets + optional closers |
| `SocialAuraLineBank.cs` | Prefer V2 paired pick; filter recent on legacy fallback; tone carries situation/ids/hours |
| `SocialMemory.cs` | Feeds `SocialPersonKnowledge` on memory add/merge |
| `NicknameEvidence.cs` | New evidence kinds + `ObserveBehaviour`; richer major-memory hooks |
| `FreeMovementSocketMapRunner.cs` | Situation sampling into `SocialDialogueTone.From` |
| `SocialDialoguePersonalBanterV2Audit.cs` | Offline multi-day presentation simulation |

**Unchanged:** Social Aura encounter rate, intent model, relationship deltas, conflict fight frequency, valence chrome (GREEN/CYAN/AMBER/RED).

---

## Exchange structure

```
INITIATOR (topic + action + rel/memory/knowledge gates)
  → REPLY filtered by SocialResponse (Accept/Agree/Deflect/PushBack/Escalate/…)
  → optional CLOSER (~42% when authored)
```

Example (catalog):

- Mara: `"You're putting another support there?"`
- Viktor: `"I enjoy ceilings staying above my head."`
- Closer: `"Luxury."`

Unrelated pairings (food reply to supports) are structurally impossible on V2 hits.

---

## Anti-repetition

`SocialDialogueHistory` suppresses:

- exact line hash (window 28 + soft 2.5h)
- exchange id (window 16)
- topic overuse (≤1 repeat in last 10 topics)

Legacy bank fallback also filters recent lines before weighted pick.

---

## Context gating

`SocialSituationFlags` from live state (clearance, illumination, confinement stress, injury, fatigue, trap, depth).  
Topics like NarrowTunnel / Darkness / Confinement / Injury only selected when flags allow.

---

## Weak points / strengths

`SocialPersonKnowledge` learns from **memories and observed behaviour** (not invented biography):

| Source | Learns |
|--------|--------|
| InsultedMe / BlamedMe | Competence / job criticism sensitivity |
| LetMeDown / FailedTogether | Mistake-joke sensitivity |
| WasTrapped / SurvivedCollapse / high confinement | Fear |
| Helped / Rescued / SharedHardship | Care, reliability, steady |
| Discovery observe | Insight |

Hostile RelClass can weaponize known weak points; friends/bonded can voice known strengths.

---

## Nickname evidence (foundation only)

New kinds: `NarrowPassageNerve`, `FearShown`, `TechnicalReputation`, `PhysicalReputation`, `RunningJoke`.  
Still **observation only** — no nickname strings assigned.

---

## Humour / harsh / positive balance

Catalog deliberately mixes:

- gallows / mining / camp / stupid jokes (including failed-joke replies)
- warm praise & practical concern
- relationship-gated cruelty (Grudge/Rivalry/Strained)

Severe weak-point lines require both knowledge **and** hostile/strained class.

---

## Audit simulation (presentation layer)

`SocialDialoguePersonalBanterV2Audit.Run()` — 4×40 beats with seeded knowledge/memories.

**Expected / design targets:**

| Metric | Target |
|--------|--------|
| Catalog size | ≥ 30 exchanges (shipped ~43) |
| V2 paired hit rate | ≥ 40% when situation flags rich |
| Unique strings | ≫ legacy adjacent repeats |
| Response ∈ active exchange replies | ≥ 80% on paired Joke→Accept |
| Humour + praise present | Non-zero both |
| Weak/strength gated uses | Non-zero when knowledge seeded |
| Aura math | Untouched |

Run in Unity via existing audit console patterns or temporary call to `SocialDialoguePersonalBanterV2Audit.Run()`.

---

## Remaining limitations

- Legacy pool still used when no exchange matches action/flags (by design).
- Conflict line bank only lightly benefits (knowledge/memory still flow into future aura tone).
- Worker job banter (`TryWorkerBanter` inline strings) not rewritten this pass.
- Callbacks require matching memory hint — still relatively rare (intended).
- No LLM / dialogue trees / voice.

---

## Do-not checklist (honored)

- No second dialogue system  
- No Social Aura frequency change  
- No relationship math retune  
- No final nicknames  
- No invented backstories for insults  
- No cutscene dialogue boxes  

**STOP.**
