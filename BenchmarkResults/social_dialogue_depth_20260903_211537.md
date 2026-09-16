# Social Dialogue Depth Audit
Generated: 2026-09-03 21.15.37

Scope: SocialAuraLineBank wording variety via SocialDialogueTone.
Does not alter Social Aura math / encounter resolution.

PASS | FirstInitiator Encourage ok stable
PASS | FirstResponse fail Deflect contains manage
PASS | FirstResponse SharedProblem Agree contains mess|alone
PASS | No empty picks — empty=0
PASS | No axis-dump digit sequences in dialogue
PASS | Friendly/Bonded Encourage differs from Grudge (some picks) — friendUnique=20 grudgeUnique=13
PASS | Camp Encourage shows some lines absent from WorkingTogether — camp=5 work=6
PASS | Unique line count is high (>= 180) — unique=298 samples=20188 ratio=0,015
FAIL | Stratified initiator uniqueness (>= 0.35) — unique=123/504 ratio=0,244
PASS | SocialDialogueTone.From sets RelClass — Bonded
PASS | SocialDialogueTone.From sets frustration
PASS | SocialDialogueTone.From major pos memory

## Sample counts
- Total picks sampled: 20188
- Unique lines: 298
- Uniqueness ratio (dense sample): 0,015
- Stratified initiator unique: 123/504 (0,244)
- Friendly/Bonded Encourage uniques: 20
- Grudge Encourage uniques: 13
- Camp Encourage uniques (40 draws): 5
- Work Encourage uniques (40 draws): 6

## Summary
PASS 11 / FAIL 1
INVARIANT: FAIL
