# Priority Hard-Band Fix

**Generated:** 2026-09-14  
**Scope:** Critical — Priority 2 Refine continued while Priority 1 Haul was valid (Elena). Hard priority bands + station subordination.

---

## Exact root cause

Three layers allowed P2 Refine to continue over P1 Haul:

1. **Soft numeric priority** — Priority was a score bonus (`PriorityWeight=120`) mixed with deficit/suit/continuity. Large Refine hour deficit could mathematically beat Haul P1. Even without that, soft scoring treated bands as comparable.

2. **Host ownership bypass (primary live symptom)** — `HostAllowsVoluntaryWork` fell through to `HasAnyEnabledStationTask(Refining)` when `ActiveTaskId` was already **Haul**. Elena could have ActiveTask=Haul while still assigned to the Refiner; the refinery **kept ticking** because Refine was not OFF.

3. **Station claim / MinCommit** — Claim logic could still score Elena for Refining when Resolve wanted Haul (`priority > P2` soft gate). `ApplyResolve` MinCommit could delay ActiveTask switch from Refine→Haul for 0.35h even across bands.

Haul **availability probe** was not the primary bug when piles exist (`ProbeHaul` = loose piles / cargo). If piles exist and Haul is P1, resolver should select Haul; the failure was **execution continuing on the wrong host** and **cross-band hold**.

---

## Previous scoring behavior

- All non-OFF available tasks scored in one pool: `(5-prio)*120 + deficit*28 + suit*18 + …`
- Same-priority margin could hold current
- MinCommit held **any** ActiveTask switch (including P2→P1)
- Host tick allowed station work whenever that job’s tasks weren’t all OFF

---

## New priority-band logic

```
Emergency (Rescue/Treat urgency) → outside bands
else for band in P1, P2, P3, P4:
  collect valid candidates in that band only
  if any → soft-score within band only → return winner
```

Within a band: deficit, suitability, distance, continuity, condition, urgency.  
**No cross-band score comparison.**  
`PriorityWeight` remains diagnostic display only.

Continuity / MinCommit apply **only inside the same band**.  
Higher band always preempts lower (P1 beats P2 immediately).

---

## Active-task preemption

- Resolve picks P1 Haul while ActiveTask is P2 Refine → ApplyResolve **applies immediately** (no MinCommit shield)
- `SyncHostToActivePriorityTask`: unassign Refining; assign Hauling if free
- Host tick: `HostAllows(Refining)` is **false** when ActiveTask is Haul → refine stops even before unassign completes

World refine input/output is not destroyed (existing Yield / host state).

---

## Station ownership findings

| Finding | Fix |
|---------|-----|
| ActiveTask Haul + assigned Refining → host still refined | `HostAllows` requires ActiveTask’s station == host job |
| Claim scored workers whose resolve wanted a different station | Claim requires `StationJobForTask(resolve) == station` |
| MinCommit blocked P1 reclaim of seat | Higher-band challenger bypasses MinCommit steal guard |

---

## Live Elena test

| Case | Expected | Result |
|------|----------|--------|
| Haul=1, Refine=2, valid haul | Elena stops refining → hauls | **NOT TESTED** (no Game view this session) |
| Refine=1, Haul=2, valid both | Prefers refine | **NOT TESTED** |

**Do not mark LIVE PASS.** Re-run in Play Mode with PRIO DIAG: `WinningPriorityBand: 1`, Refine line `LOWER PRIORITY BAND`.

Automated proofs:
- HostAllows Haul ActiveTask denies Refining
- ApplyResolve P1 preempts P2 inside MinCommit window
- Same-band MinCommit still holds

Menu: `DeepCore/Diagnostics/Run Priority System V1.2 Full Authority Audit`

---

## Cross-task regression (code authority)

| Pair | Code expectation | Live |
|------|------------------|------|
| A Haul1 / Refine2 | Haul band wins | NOT TESTED |
| B Refine1 / Haul2 | Refine band wins | NOT TESTED |
| C Excavate1 / Haul2 | Excavate if paint valid | NOT TESTED |
| D Support1 / Lighting2 | Support | NOT TESTED |
| E Meals1 / Clean2 | Meals if available | NOT TESTED |
| F Prospect1 / Haul2 | Prospect if player work | NOT TESTED |
| Same-priority soft factors | Within-band only | AUTOMATED (MinCommit same-band) |

---

## Files changed

- `WorkPriorityResolver.cs` — hard bands, SoftTotal, HostAllows station match
- `WorkPriorityDirector.cs` — cross-band MinCommit bypass
- `WorkerPriorityPrefs.cs` — `LastWinningPriorityBand`, `LastBandRejectHint`
- `FreeMovementSocketMapRunner.cs` — `SyncHostToActivePriorityTask`, claim band rules, DEV diag
- `PrioritySystemV12FullAuthorityAudit.cs` — band automated checks

---

## Remaining limitations

- Live Elena reproduction must be confirmed in Game view
- If Hauler seat occupied by another P1 hauler, Elena leaves refine and waits/unassigned until claim succeeds — may idle briefly (correct: must not keep refining)
- Exclusive hosts still one worker; band logic does not create parallel haul
- Prospect Manual still needs player-created work to enter P1 band

---

## Result

**Hard priority bands + host subordination implemented.** Elena-style P2 refine over P1 haul is fixed in code. **LIVE PLAYMODE PASS not claimed.**
