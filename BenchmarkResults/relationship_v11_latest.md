# Relationship V1.1 Audit
Generated: 2026-09-03 09:09:06

Scope: directional Respect + derived RelationshipClass (DEV only).
No Trust/Warmth/Hostility formula changes. No encounter frequency / dialogue changes.

## 1. Respect is directional
PASS | Baseline Respect is 50 both ways — 1→2=50 2→1=50
PASS | After Encourage, Mara(2)→Lewis(1) Respect rises more (helped) — 2→1 50→52.6 ΔTI=2.6
PASS | Lewis(1)→Mara(2) Respect does not mirror Mara's gain — 1→2 Δ=0.9 2→1 Δ=2.6
PASS | Respect deltas are directional (ΔTI ≠ ΔIT for Encourage) — ΔIT=0.9 ΔTI=2.6

## 2. High Hostility + high Respect is possible
PASS | Can set Hostility=12 and Respect=78 independently — T=1.0 W=-2.0 H=12.0 R=78.0
PASS | Derived Rivalry when H high + R high — Rivalry — Rivalry: H=12 R=78 W=-2
PASS | Non-meaningful / trivial hostility bump does not Apply Respect — R=50

## 3. Classifications derive from underlying state
PASS | Bonded derives from high W/T + memory evidence — Bonded — Bonded: W=10 T=7 R=60 posMem=4+3
PASS | Professional derives from high Respect + Trust, low warmth — Professional — Professional: R=72 T=6 |W|=1
PASS | Friendly derives from moderate Warmth — Friendly

## 4. Classifications do not become permanent stored labels
PASS | SocialDirectedRelation has no RelationshipClass/Derived field
PASS | Changing axes changes derived class (not sticky label) — before=Bonded after=Neutral

## 5. One encounter cannot create extreme relationship
PASS | Single SharedProblem bond ≠ Bonded — A→B=Neutral B→A=Neutral
PASS | Single encounter Respect |Δ| ≤ MaxAbsPerEncounter — ΔIT=1.8 ΔTI=1.4
PASS | Single Provoke/Escalate ≠ Grudge and ≠ Rivalry — Strained

## 6. Reassignment preserves Respect
PASS | Respect keyed by WorkerId (survives job identity) — R=60
PASS | After cosmetic job rename, Respect intact
PASS | Live SocialAura Respect survives shift/re-sync — R=66
PASS | Same World Memory instance after re-sync

## 7. Existing Social Aura and Social Memory invariants remain
PASS | PressureTrigger still 1.05
PASS | CooldownAfterEncounter still 0.55
PASS | ReachWorldScale still 2.35
PASS | MaxEncountersPerWorkerPerShift still 3
PASS | Memory MaxPerTarget still 12
PASS | Trivial encounter still does not flood memory
PASS | T/W/H Add() signature unchanged (Respect separate)
PASS | AddRespect does not mutate Trust

## Summary
PASS 27 / FAIL 0
INVARIANT: PASS

## Files
- Assets/Vibe/FreeMovement/SocialAuraStage0Types.cs (Respect on SocialDirectedRelation)
- Assets/Vibe/FreeMovement/RelationshipV11.cs (Respect applicator + classifier)
- Assets/Vibe/FreeMovement/SocialAuraStage0Encounter.cs (hook Apply)
- Assets/Vibe/FreeMovement/SocialAuraLive.cs (hook Apply + NearbyDebug.Respect)
- Assets/Vibe/FreeMovement/FreeMovementSocketMapRunner.cs (DEV UI)
- Assets/Vibe/FreeMovement/RelationshipV11Audit.cs (this audit)
