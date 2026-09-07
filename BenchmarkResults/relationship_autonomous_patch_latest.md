# Autonomous Relationship Patch — Final Report

Generated: 2026-09-03

**Overall verdict: GREEN** — all listed regression audits PASS after small audit/threshold fixes.

---

## 1. Rounds

### Round 1 — Relationship Work Playtest validation
- **Status:** PASS (no sim tuning)
- Verified presets Strong Professional / Rivalry / Strained / Neutral produce distinct CooperationQuality (P=0.60 R=0.51 N=0.42 S=0.26) with bounded dispatch/repair muls.
- Rivalry remains competent (Q≥0.45) despite high Hostility.
- DEV `ApplyPreset` only mutates named Mara↔Viktor pair; Lewis axes/memories untouched.
- **Checkpoint audits:** coop_playtest, coop_work_v1, relationship_v11, social_memory_v1 — PASS

### Round 2 — Social Memory significance
- **Status:** PASS
- Added `SocialMemorySignificance`: Ordinary / Significant / Major.
- Decay rates: Ordinary 0.022/h, Significant 0.006/h, Major 0.0012/h (majors never purge from decay alone).
- Cap still 12; trim prefers higher significance.
- DEV memory rows show significance.
- **Checkpoint:** social_memory_v1 (+ significance section), relationship/coop/playtest — PASS

### Round 3 — Relationship trajectory
- **Status:** PASS
- Read-only `RelationshipTrajectoryStore` (max 16/pair) records meaningful T/W/H/R deltas with optional memory link.
- Hooked after resolve + memory + respect on Stage0 Expose and Live ExposeLive.
- DEV: compact recent trajectory for selected pair.
- **Checkpoint:** relationship_trajectory + mem/rel/coop — PASS

### Round 4 — Camp social opportunity V1 (foundation)
- **Status:** PASS
- Added `SocialContext.Camp` (ContextMul 0.90 — distinct, not a positivity buff).
- Off-shift + both in camp radius → Camp context via existing Social Aura resolver.
- Operating pairs still WorkingTogether; sleeping remains ineligible.
- Camp encounter counts in DEV playtest summary.
- **Not implemented:** seeking/avoiding movement, scripted camp talk, forced bonding.
- **Checkpoint:** camp_social_v1 + mem/traj — PASS

### Round 5 — Social affinity / avoidance intent (read-only)
- **Status:** PASS (after threshold tweak ±0.18)
- `SocialIntentModel` → Seek / Neutral / Avoid from T/W/H/R + memories + Fr/Morale + Soul.
- High H + high R stays Neutral/professional.
- Directional; no movement/reach/schedule changes.
- DEV: intent + reasons per other worker.
- **Checkpoint:** social_intent — PASS

### Round 6 — Nickname evidence (data only)
- **Status:** PASS
- `NicknameEvidenceStore` observes real WorkerStateEvents + major social memories.
- Person-keyed; repetition with diminishing returns; trivial ProgressSuccess ignored.
- DEV: strongest evidence kinds for selected worker.
- **Not implemented:** nickname generation, UI names, player naming, stat effects.
- **Checkpoint:** nickname_evidence — PASS

### Final regression
| Audit | Result |
|-------|--------|
| WorkerV12A State Verify | PASS (audit updated for person stamina priming on any job bind) |
| WorkerV12B Event Audit | PASS (SocialAura types now expected) |
| Social Memory V1 | PASS |
| Relationship V1.1 | PASS |
| Relationship-Aware Work V1 | PASS |
| Relationship Work Playtest | PASS |
| Relationship Trajectory | PASS |
| Camp Social V1 | PASS |
| Social Intent | PASS |
| Nickname Evidence | PASS |

---

## 2. Files changed (this patch)

**New**
- `RelationshipTrajectory.cs` / `RelationshipTrajectoryAudit.cs`
- `CampSocialV1Audit.cs`
- `SocialIntentModel.cs` / `SocialIntentAudit.cs`
- `NicknameEvidence.cs` / `NicknameEvidenceAudit.cs`
- (plus prior session: RelationshipAwareWork, RelationshipV11, SocialMemory, playtest tracker)

**Modified**
- `SocialMemory.cs` — significance tiers, decay, major→nickname hook
- `SocialAuraStage0Types.cs` — `SocialContext.Camp`, ContextMul
- `SocialAuraStage0Encounter.cs` / `SocialAuraLive.cs` — trajectory + Camp derive/counts
- `WorkerStateEventSystem.cs` — nickname evidence observe on emit
- `FreeMovementSocketMapRunner.cs` — DEV: significance, trajectory, intent, nickname evidence, camp counts, playtest
- `Editor/WorkerV11LockAuditMenu.cs` — new audit menus
- `RelationshipWorkPlaytestAudit.cs` — stronger preset isolation checks
- `WorkerV12AStateVerify.cs` / `WorkerV12BEventAudit.cs` — align locks with intentional current design

---

## 3. Design decisions

1. **Warmth is not a work-efficiency lever** (coop still Trust/Respect/Hostility-led).
2. **Camp is a context, not a mood buff** — same resolver; outcomes can clash.
3. **Intent is observational only** — thresholds tuned so clear cases classify; rivalry softens Avoid.
4. **Nickname evidence is ledger-only** — no generation; accumulates from existing events.
5. **Trajectory filters noise** — sub-threshold axis jitter is ignored (min Δ 0.35 axes / 0.8 Respect).
6. **DEV presets clear only the target pair’s memories** so classification stays clean without wiping the crew graph.

---

## 4. Intentionally NOT implemented

- Camp seeking/avoiding locomotion
- Scripted camp conversations / forced relationship repair
- Nickname generation, display names, player naming
- Romance, fights, death, player dialogue choices, story events
- Social Aura frequency rebalance
- Job redesign / dig productivity relationship buffs
- Respect/Warmth/Trust formula retunes (beyond existing V1.1 Respect applicator)

---

## 5. Risks / uncertainties

- **Trajectory vs tiny deltas:** many soft encounters won’t leave history; that’s intentional but may look “empty” early game.
- **Camp ContextMul 0.90** slightly above IdleNearby 0.85 — may slightly increase off-shift pressure vs pure IdleNearby; not a frequency constant change, but watch playtest.
- **Nearby DEV debug** does not pass camp radius into Derive (Tick path is authoritative) — nearby ctx may show IdleNearby while standing at camp.
- **NicknameEvidenceStore** is a process singleton — survives scene reload in editor until Clear; fine for playtest, not a save format yet.
- **Intent thresholds** are prototype; live feel needs human judgment with SET TEST RELATIONSHIP.

---

## 6. Recommended next human playtest

1. SOCIAL panel → **SET TEST RELATIONSHIP** cycle Professional → Rivalry → Strained → Neutral.
2. Force excavator overheats; compare repair dispatch feel + COOP WORK PLAYTEST day lines.
3. End shift at camp with 2+ idle avatars; confirm Camp ctx count rises and outcomes aren’t always positive.
4. Inspect trajectory + memory significance after a few charged encounters.
5. Confirm nickname evidence accumulates (Discovery / RepairRescue / Injury) without any nickname text appearing.

---

## 7. Recommended next development step

1. **Camp presence accuracy** — pass real camp radius into nearby Derive for DEV honesty.
2. **Persist trajectory + nickname evidence** with WorkerId save keys (still no nicknames).
3. Only after playtest: optional **soft idle drift** toward Seek / away from Avoid at camp (movement), still no scripts.
4. Defer nickname generation until evidence kinds feel earned in live digs.

---

## 8. Regression watch list (checked)

- Duplicate event processing: nickname observe runs once after successful Apply (not on spam-gated rejects).
- Frame-rate accumulation: evidence/trajectory keyed by game events/hours, not per-frame.
- Memory spam: trivial encounters still filtered by `IsMeaningful`.
- DEV presets: only via button; Evaluate/coop paths do not call ApplyPreset.
- Job assignment / person identity: evidence and relations remain WorkerId-keyed.

**STOP.**
