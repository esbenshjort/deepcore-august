# Social Memory V1 Audit
Generated: 2026-09-03 08:02:53

Scope: directional memories from meaningful encounters only.
No Trust/Warmth/Hostility formula changes. No encounter frequency changes.

## 1. Directional memory
PASS | Mara→Lewis has SupportedMe/HelpedMe after Encourage — count=2
PASS | Lewis→Mara does NOT mirror HelpedMe (directional) — lewisTypes=WorkedWellTogether
PASS | Lewis→Mara may have WorkedWellTogether (own perspective)

## 2. Meaningful encounters create memories
PASS | Provoke/Escalate creates memory — Δ=2
PASS | Target remembers InsultedMe
PASS | Complain+Agree SharedProblem creates SharedHardship both ways — Δ=4

## 3. Trivial encounters do not flood
PASS | Empty Connect/Ignore is not meaningful
PASS | 20 trivial Connect/Ignore add 0 memories — adds=0 entries=9 before=9

## 4. Cap / retention (MaxPerTarget=12)
PASS | Retention capped at 12 — count=12
PASS | Strongest memories retained under cap — maxStr=0.92

## 5. Reassignment does not lose memories
PASS | Memory store survives job identity (WorkerId keys) — entries=9
PASS | After job swap Mara→Lewis memories intact — before=2 after=2
PASS | Same SocialAura World Memory instance

## 6. Social Aura behavior unchanged (spot)
PASS | PressureTrigger still 1.05
PASS | CooldownAfterEncounter still 0.55
PASS | ReachWorldScale still 2.35
PASS | MaxEncountersPerWorkerPerShift still 3
PASS | Resolve still returns encounter log
PASS | Memory TotalEntries is independent counter

## Summary
PASS 19 / FAIL 0
INVARIANT: PASS

## Files
- Assets/Vibe/FreeMovement/SocialMemory.cs (new)
- Assets/Vibe/FreeMovement/SocialAuraStage0Encounter.cs (hook Record)
- Assets/Vibe/FreeMovement/SocialAuraLive.cs (hook Record + overnight decay)
- Assets/Vibe/FreeMovement/FreeMovementSocketMapRunner.cs (DEV UI)
- Assets/Vibe/FreeMovement/SocialMemoryV1Audit.cs (this audit)
