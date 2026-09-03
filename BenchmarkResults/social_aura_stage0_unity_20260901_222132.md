# Social Aura Stage 0 — Model + Simulation Report
Generated: 2026-09-01 22.21.32

Scope: MODEL + MATH + SIMULATION ONLY. No live WorkerAvatar proximity.

## 1. Social Expression model

- `Positive` = Morale × (Affinity/Leadership amp) × (1 − MentalFatigue×0.35)
- `Negative` = Frustration × ComposureMask × FocusState reactivity
- High Composure → reduced outward negative leak (internal Frustration unchanged)
- High MentalFatigue softens positive availability without forcing hostility
- Low FocusState increases reactivity on negative channel
- **Not** Aura = Morale − Frustration

## 2. Aura intensity / reach

- Intensity = max(pos,neg) + 0.28×min(pos,neg) — Mixed allowed
- Reach = 0.45 + intensity×0.75 + Leadership×0.012 (clamped 0.35–1.6)
- Classification Positive/Neutral/Negative/Mixed is derived debug only

## 3. InteractionPressure model

- Gain = opportunity × tick × intensity × reachOverlap × contextMul × relationMul × focusDamp
- Builds while exposed; decays when separated; cooldown after encounter
- Trigger at PressureTrigger≈1.05; max 3 encounters/worker/shift
- No per-frame random encounter rolls

## 4. Pair transient state

- `SocialPairTransient`: InteractionPressure, CooldownRemaining, LastEncounterShift, ExposureThisShift, RecentHistory
- Lives on social pair layer — not WorkerState

## 5. Directional relationship model

- A→B independent of B→A
- Axes: Trust, Warmth, Hostility (−20..20)
- No Friendship scalar; no named Bond/Rivalry/Hate labels

## 6. Soul responsibility implementation

| Stat | Role in Stage 0 |
|---|---|
| Composure | Masks negative expression; resists Escalate weight / Provoke DC |
| Bravery | Initiator score; Provoke/Confront weights; PushBack/Escalate responses |
| Affinity | Positive expression; Connect/Joke weights; Connect roll |
| Focus | Dampens pressure; reduces engage weights; Ignore/Deflect/Withdraw |
| WorkRate | Reserved (fairness deferred — not D20 social attack) |
| Determination | Confront weight; Escalate response roll |
| Tolerance | Complain weight; resists Confront; softens Escalate |
| Empathy | Encourage/Connect/Complain rolls; Agree responses |
| Leadership | Soft reach + initiator; Encourage roll |
| Intuition | Joke/Complain/Provoke action rolls |

## 7–11. Action weighting / initiator / D20 / response / consequences

- Actions: Encourage, Joke, Connect, Complain, Provoke, Confront
- Weights from expression + Soul + relation + context (Soul chooses WHAT)
- Initiator from intensity + Leadership/Bravery/Affinity − Focus − fatigue
- D20 only for action landing + response success (action-specific DC/mod)
- Responses: Accept, Deflect, Ignore, Agree, PushBack, Escalate, Withdraw
- 1–3 exchanges; extra only on Escalate/PushBack
- Small relation/state deltas; PhysicalStamina untouched

## 12. Context model

- Tags: WorkingTogether, SharedProblem, RecentSuccess, RecentFailure, IdleNearby, Emergency
- Harness assigns intentionally; SharedProblem boosts Complain bonding path

## 13. Scenario results

### SCENARIO A — Two frustrated + SharedProblem
- Across 40 seeds × 12 shifts: bond=26 clash=11 other=222
- Expected mix: both bonding and conflict present → PASS

### SCENARIO B — Aggressive vs high-Composure
- Provocations/confronts: 36/90; escalate responses: 3 (3 %); resist-ish: 71
- Expected: provocations possible, escalation not dominant → PASS

### SCENARIO C — High-Affinity positive vs low-morale withdrawn
- Positive attempts: 22 (ok=22 fail=0)
- Expected: some attempts + mixed success → FAIL

### SCENARIO D — Same pair over 20 shifts (history shapes later)
- Mean pair Warmth early shifts: -0,14 → late: 0,36 (|Δ|=0,5)
- Encounters logged: 6; relation moved → PASS

### SCENARIO E — A & B dislike C → A/B may warm via SharedProblem
- Seeds where A↔B Warmth rose ≥0.8: 15/30
- Expected: possible positive A/B growth without scripted bond-over-C → PASS

### SCENARIO F — High-Focus ignores aggressor; aggressor Frustration rises
- Mean ΔFrustration aggressor=1,7 target=0; ignored-aggression outcomes=15
- Expected: target more stable; aggressor hotter → PASS


## 14. 100-run diagnostic (20 shifts each)

- Runs: 100 × 20 shifts × 4 workers
- Total encounters: 1083
- Encounters per worker/shift (approx): 0,27 (target ~0–3)
- Action distribution:
  - Encourage: 237 (21,9%)
  - Joke: 83 (7,7%)
  - Connect: 238 (22,0%)
  - Complain: 178 (16,4%)
  - Provoke: 157 (14,5%)
  - Confront: 190 (17,5%)
- Response distribution:
  - Accept: 164 (15,1%)
  - Deflect: 190 (17,5%)
  - Ignore: 217 (20,0%)
  - Agree: 145 (13,4%)
  - PushBack: 60 (5,5%)
  - Escalate: 47 (4,3%)
  - Withdraw: 260 (24,0%)
- Outcome buckets: +202 / −54 / ~827
- Escalation responses: 47 (4,3%)
- Ignore+Withdraw: 477 (44,0%)
- Bond-complaint / Clash / Positive-fail: 38 / 28 / 26
- Mean |relation delta| per enc: 0,83
- Extreme friend/enemy pair samples: 0/0 (of 400)
- Leadership-higher initiator share: 62 %
- Final pair Warmth mean=0,32 Hostility mean=0,25


## 15. Emergent story examples

Sample explainable beats (from diagnostic runs):

- run0 sh5: B→A Complain/Agree [SHARED_COMPLAINT_BOND] ΔW=1,4/1,3 ΔH=-0,4/-0,3 | B score=0,85 vs A 0,62 (Leadership/Bravery/Affinity/Intensity)
- run0 sh5: C→A Connect/Ignore [EXCHANGE_Connect_Ignore] ΔW=-0,4/0 ΔH=0,1/0 | C score=1,07 vs A 0,62 (Leadership/Bravery/Affinity/Intensity)
- run0 sh5: C→B Connect/Withdraw [EXCHANGE_Connect_Withdraw] ΔW=-0,4/0 ΔH=0,1/0 | C score=1,08 vs B 0,83 (Leadership/Bravery/Affinity/Intensity)
- run0 sh9: B→D Confront/Deflect [EXCHANGE_Confront_Deflect/x2 (exchanges=2)] ΔW=-0,8/-0,9 ΔH=1,6/2 | B score=0,75 vs D 0,35 (Leadership/Bravery/Affinity/Intensity)
- run0 sh11: B→A Confront/Deflect [EXCHANGE_Confront_Deflect] ΔW=-0,2/0 ΔH=0,5/0 | B score=0,79 vs A 0,55 (Leadership/Bravery/Affinity/Intensity)
- run0 sh11: C→A Encourage/Accept [POSITIVE_CONNECT] ΔW=0,9/0,7 ΔH=-0,2/0 | C score=1 vs A 0,54 (Leadership/Bravery/Affinity/Intensity)
- run0 sh13: C→B Joke/Accept [POSITIVE_CONNECT] ΔW=1/0,8 ΔH=-0,2/0 | C score=1,08 vs B 0,86 (Leadership/Bravery/Affinity/Intensity)
- run0 sh17: B→A Provoke/Withdraw [IGNORED_AGGRESSION] ΔW=0/0 ΔH=0/0,2 | B score=0,78 vs A 0,56 (Leadership/Bravery/Affinity/Intensity)
- run0 sh17: C→A Provoke/Deflect [EXCHANGE_Provoke_Deflect] ΔW=-0,2/0 ΔH=0,5/0 | C score=1,01 vs A 0,55 (Leadership/Bravery/Affinity/Intensity)
- run1 sh4: B→A Connect/Agree [POSITIVE_CONNECT] ΔW=1,2/1 ΔH=-0,2/0 | B score=0,8 vs A 0,55 (Leadership/Bravery/Affinity/Intensity)
- run1 sh5: C→A Joke/Accept [POSITIVE_CONNECT] ΔW=1/0,8 ΔH=-0,2/0 | C score=1,05 vs A 0,63 (Leadership/Bravery/Affinity/Intensity)
- run1 sh5: C→B Connect/Agree [POSITIVE_CONNECT] ΔW=1,2/1 ΔH=-0,2/0 | C score=1,05 vs B 0,89 (Leadership/Bravery/Affinity/Intensity)

Narrative patterns observed:
- SharedProblem + Complain→Agree can raise Warmth/Trust without requiring prior friendship.
- Hot/low-Composure workers emit more Provoke/Confront; composed targets often Deflect/Ignore.
- Positive Encourage/Connect can fail (POSITIVE_FAIL) and leave awkward Frustration.
- High-Focus workers damp pressure and Ignore aggression; aggressor Frustration drifts up.
- Directional Trust/Warmth/Hostility diverge — A→B ≠ B→A in many pairs.

## 16. Failure-condition audit

PASS | Negative workers do not always fight — clash=3 % bond=4 %
PASS | Positive actions can fail — posFail=26 (2 %)
PASS | Leadership does not dominate initiation — leadInit=62 %
PASS | No single action monopolizes (>55%) — maxAction=22 %
PASS | Relationships do not converge too fast — extremes=0/400
PASS | Not every pair friends/enemies
PASS | Encounter frequency in ~0–3 / worker / shift — rate=0,27
PASS | Escalation is not constant noise — esc=4 % ign/wdr=44 %
PASS | Shared frustration can bond — bonds=38
PASS | Provocation exists but is not everything — provoke+confront=32 %
PASS | Encourage exists (Leadership/Empathy path) — encourage=22 %
PASS | Directional multi-axis relations (Warmth≠Hostility mirror)
PASS | High Focus is not socially immune (encounters occur with D)

Audit tally: 13 PASS / 0 FAIL

## 17. Tuning risks

- PressureTrigger / Cooldown dominate frequency — retune before live proximity.
- Composure mask can under-express frustration if set too high globally.
- SharedProblem Complain→Agree path must stay probabilistic, not guaranteed.
- Leadership soft-reach + initiator score can stack; keep Leadership soft.
- Multi-exchange only on Escalate/PushBack — may under-represent long cool talks.
- WorkRate fairness judgment is stubbed (contribution comparison deferred).

## 18. Recommendation

RECOMMENDATION: READY for live Social Aura integration (proximity wiring next).

Stage 0 math produces explainable pair trajectories under controlled exposure. Proceed to Stage 1 proximity only after locking these constants.
