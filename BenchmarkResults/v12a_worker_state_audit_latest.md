# V1.1 Worker Architecture Lock Audit

Generated: 2026-09-01 20:59:25
Mode: Unity batch ForceBuild + programmatic regression (not interactive Play Mode UI).

## 1. Bootstrap / person / avatar / assignment
PASS | Crew count 5 — n=5
PASS | Unique WorkerIds 1..5
PASS | Each person one Stats sheet
PASS | Exactly one avatar per worker
PASS | Default jobs Lewis Pros / Mara Exc / Kow Haul / Elena Ref / Vik Eng
PASS | No duplicate job occupancy
PASS | Selected defaults to Lewis

## 2. Selection A — Lewis Prospecting
PASS | Highlight/selection Lewis
PASS | SelectedJobIs Prospecting
PASS | CanPerformJobActions Lewis
PASS | Camera target Prospecting host label
PASS | ST target Lewis stats ref stable
PASS | Banter author Lewis when Prospecting — banter=True id=1

## 3. Reassign B — Lewis Prospecting → Excavation
PASS | TryAssign Lewis Excavation — OK
PASS | Selection remains Lewis
PASS | Lewis now Excavation
PASS | Mara Unassigned after eviction
PASS | No duplicate Excavation
PASS | Lewis stats ref unchanged
PASS | Mara stats ref unchanged
PASS | SelectedJobIs Excavation
PASS | Excavator host position unchanged (no teleport)
PASS | Banter Excavation context still Lewis

## 4. Reassign C — Mara → Prospecting
PASS | TryAssign Mara Prospecting — OK
PASS | Mara Prospecting
PASS | Lewis still Excavation
PASS | Findings author sync Mara — author=2 — author=2
PASS | Banter Prospecting = Mara
PASS | Prospector host position not forced to camp

## 5. Rotate D — cross-job swaps
PASS | Assign Elena Excavation — OK
PASS | Lewis Unassigned after Elena takes Exc
PASS | Assign Lewis Hauling — OK
PASS | Kowalski Unassigned
PASS | Assign Kowalski Refining — OK
PASS | Elena still Excavation after Kowalski→Refining
PASS | Assign Viktor Excavation — OK
PASS | Elena Unassigned after Viktor takes Exc
PASS | No duplicate jobs after rotate
PASS | Hauler provider position stable

## 6. Vacancy E — unassign all
PASS | All jobs vacant
PASS | Five people still in roster
PASS | Five avatars visible when Unassigned — visible=5
PASS | No job banter when vacant
PASS | Selected person survives vacancy

## 7. Rebind F — new configuration
PASS | Lewis Pros
PASS | Mara Exc
PASS | Kowalski Haul
PASS | Elena Ref
PASS | Viktor Eng
PASS | Default map restored
PASS | Assigned avatars hidden — hidden=5
PASS | Avatar sync following set for assigned

## 8. Shift / sleep / wake
PASS | Phase HeadingHome
PASS | Providers stay put on HeadingHome
PASS | Avatars visible commuting
PASS | Job actions disabled while commuting
PASS | Camera host label is WorkerAvatar while commuting
PASS | Phase Asleep
PASS | Providers stay put on Sleep
PASS | Selection survives sleep
PASS | Assignments survive sleep
PASS | After SkipSleep phase HeadingOut or OnShift — HeadingOut
PASS | Providers still unmoved after SkipSleep
PASS | Selection survives SkipSleep
PASS | Assignments survive SkipSleep
PASS | OnShift after wake
PASS | CanPerform again when Operating
PASS | Assigned avatars hidden again

## 9. Provider release / scan block hooks
PASS | CanRelease Prospecting when not mid-scan — OK

## 10. Personal vs machine
PASS | Lewis WorkerState object stable across jobs
PASS | Lewis has WorkerState
PASS | Lewis/Mara State are distinct bags
PASS | Morale not maxed at spawn — morale=55
PASS | FocusState distinct from perfect — focusState=62
PASS | FocusState != static Focus capacity — focusState=62 statFocus=10
PASS | After Mara takes Excavator, Lewis keeps Frustration — lewis=55
PASS | Mara brings her own Frustration — mara=12
PASS | Sleep leaves Frustration residue — frust=37
PASS | Sleep does not max Morale — morale=55
PASS | Sleep Morale barely moves — before=55 after=55
PASS | Excavator Heat is machine field (readable)

## 11. ControlWorker / role debt (classified)
- A: `_control` stub + DEV readout + Bridge bootstrap fallback
- B: `JobToLegacyRoleIndex` / `WorkerStatProfiles.RoleTitle|Build` profile recipes
- C: none found for normal selection/banter/camera/ST
- D: removed obsolete CycleControl / SelectWorker / DrawWorkerCard stubs

## Summary
PASS: 81
FAIL: 0

## Known non-blocking debt (carry-forward)
- Excavator heat hard-resets on sleep (prefer passive cooling)
- WorkerRuntime.State V1.2A — daytime MentalFatigue/FocusState/Morale mostly static until V1.2B/C
- Host sprites can look crewed while vacant / avatar hidden
- Morning provider return is bridge snap, not polished travel
- H/R/E relevant stats provisional
- Job-context line banks ≠ unique personalities
- Engineer provider façade-only
- No Social Aura / final enter-exit visuals

## Play Mode note
This audit ForceBuilds the SocketMap runner and exercises assignment,
avatar presence, selection, banter authorship, and shift phase transitions
programmatically. Interactive WASD/HUD play was not driven.
Prospector geo diagnostic + prior excavator CSV baselines remain separate artifacts.

## VERDICT: A. V1.1 READY TO LOCK
Architecture and live regression are sound.
Proceed to V1.2 Universal Worker State when ready.
