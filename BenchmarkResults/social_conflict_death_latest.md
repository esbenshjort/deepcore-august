# Social Conflict + Death Patch

Generated: 2026-09-03  
Project: Tilemap_graphics_test / FreeMovement  
Scope: Arguments → Fights → Witnesses → Severe → Critical Injury → rare Death; results-only SOCIAL DEV; Force* DEV; balance matrix.  
**No redesign** of Social Aura / Memory / Relationship cores.

## Status

| Round | Deliverable | Status |
| --- | --- | --- |
| 1 Arguments | Escalating sessions, Frust≠violence, Soul+D20, meaningful memories | **Done** (prior + retained) |
| 2 Fights | Peak-only escalate; shove/grab/punch/retaliate/break; Injury | **Done** (prior + retained) |
| 3 Witnesses | Ignore / intervene / side / de-escalate / break-up | **Done** (prior + FailedIntervention hook) |
| 4 Lethal escalation | Scuffle → Severe → Critical → Lethal gates | **Done** |
| 5 Death | `WorkerState.IsAlive` / `MarkDead`; exclude assign/commute/social | **Done** |
| 6 Conflict dialogue | Expanded LineBank (+ severe/critical/death/post-fight) | **Done** (~163 new lines) |
| 7 SOCIAL DEV | Results-only panel (relationship / happening / history / conflict) | **Done** |
| 8 DEV testing | Healthy / Rivalry / Grudge / Critical + Force Argument/Fight/Lethal | **Done** |
| 9 Balance + audits | `SocialConflictAudit` + death report dual-write | **Code ready** — Unity Editor held the project lock for batchmode |

**Menus:** DeepCore/Diagnostics/Run Social Conflict Audit · Run Social Conflict Death Audit

Re-run those menus (or close Editor and batch) to refresh PASS counts below.

---

## Architecture

```
Encounter (hostile) → TryEmergeFromEncounter
  → Argument phases (Opening→Exchange→Peak→…)
  → CanStartFight (FailedDeEscalation + hot soul + axes + history + chance)
  → RunFight (scuffle, injury ≤ moderate)
  → TryEscalateFightSeverity
       Severe (axes + chance/force)
       → Critical (injury/punches + NeedsCare)
       → Lethal (CanLethalCombination + chance/force) → MarkDead + Major memories
  → RunWitnesses (FailedIntervention if BreakUp fails)
```

**Death rules encoded**
- Frustration alone never emerges / never kills  
- Hostility alone never kills (`HostilityAloneInsufficientForDeath`)  
- Ordinary scuffles stay non-lethal (injury cap = `FightInjuryModerate`)  
- High Respect + Hostility &lt; 18 blocks murder (rivalry ≠ murderous hatred)  
- Lethal needs Critical severity + murderous Major history + failed de-escalation/intervention + low Empathy + high Bravery + chance (or ForceLethal)  
- Dead: no social eligibility, no job actions, no commute, no conflict speech; identity/memory/relations persist; `ResetToSpawnDefaults` does **not** revive  

---

## Files changed (primary)

| Area | Files |
| --- | --- |
| Conflict sim | `SocialConflict.cs`, `SocialConflictLineBank.cs` |
| Vitality | `WorkerState.cs`, `WorkerRuntime.cs` |
| Memory | `SocialMemory.cs` (+ negative helpers in Intent / Rel / LineBank / Conflict) |
| Live gates | `SocialAuraLive.cs`, `FreeMovementSocketMapRunner.cs` (physical Dead, assign, commute, roster `// DEAD`, SOCIAL DEV rewrite) |
| Audits | `SocialConflictAudit.cs`, `SocialConflictDeathAudit.cs`, `Editor/WorkerV11LockAuditMenu.cs` |

---

## Balance matrix (expected soft targets)

| Bucket | Fights | Deaths | Notes |
| --- | --- | --- | --- |
| Healthy | very rare | **0** | soft ≤2 fights / 40 |
| Normal | rare | **0** | |
| Rivalry | possible conflict | **0** | high Respect blocks murder |
| Grudge | fights reachable | rare | ≤1/40 soft OK |
| Extreme | severe reachable | ForceLethal proves path | chance death not default |

Exact seeded counts: fill from menu run into `social_conflict_death_latest.md` / `social_conflict_latest.md` (audit dual-writes both).

Prior R3–R5 offline audit (2026-09-03 21:16): **PASS 21 / 0** (non-lethal scuffle invariant).

---

## Audit results

**Batchmode blocked** this session: another Unity instance has the project open.

Run:

1. DeepCore/Diagnostics/Run Social Conflict Death Audit  
2. Optionally: Dialogue Depth, Frustration Tuning, V1.2A/B/C, Social Aura S0–S2, Memory, Relationship V1.1, Camp, Intent, Nickname, Relationship-Aware Work  

Expected new checks: ForceLethal kills + KilledBy memory; dead ineligible; ResetToSpawnDefaults no revive; HostilityAloneInsufficientForDeath; Respect rivalry blocks lethal; balance soft matrix.

---

## Limitations

- No police / legal / weapons / corpses / funerals / replacement hires / player violence  
- Witness proximity still soft (actor list; `WitnessReachMul` not fully spatial)  
- Offline audit approximates de-escalation / intervention counts from history tags  
- ForceLethal bypasses chance only — does not change live tuning constants  

---

## Recommended human playtest

1. Healthy crew long shift — quiet SOCIAL · RESULTS, no fights  
2. DEV → Grudge / Critical Conflict → Force Argument → Force Fight  
3. Force Lethal Pipeline — victim `// DEAD`, silent, unassignable; killer/witnesses keep Major memories  
4. Rivalry preset — tension without murder  
5. Confirm SOCIAL DEV shows plain-language relationship / conflict / history — no D20/math  

---

## STOP

Rounds 1–9 implemented. No further social features in this patch. Refresh audits from the Editor menus when the project is free.
