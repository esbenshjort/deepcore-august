# Debris + Tunnel Collapse + Injury/Rescue V1 Audit

Generated: 2026-09-07 10:25:00

## Architecture (source)
- PASS  TunnelCollapseSystem present
- PASS  CollapseInjuryResolver present
- PASS  FallingDebrisFx present
- PASS  Debris blocks IsTunnelOpen
- PASS  IsMovementBlocker includes debris
- PASS  WorkerState.Incapacitated
- PASS  TrappedFromCamp flag
- PASS  Roster Kind.Incapacitated
- PASS  Support quality on build
- PASS  Social memory collapse types
- PASS  Excavator DebrisStrike
- PASS  Runner wires collapse
- PASS  No teleport home when trapped
- PASS  SoftArriveAllHome removed; audit SoftArrive skips incap
- PASS  Morning commute skips trapped
- PASS  Sleep skips proper recovery when trapped
- PASS  DEV force controls

## Stability bands (runtime)
- FAIL  Dig-face tip is end-tunnel site
- PASS  Mid corridor is not end-tunnel site
- PASS  Near-camp cell risk is 0
- PASS  Mid corridor natural risk is 0
- PASS  Support build succeeded near tip
- PASS  Unsupported tip risk > supported tip (or tip still gated) — tip=0.00 tipSup=0.00 band=Stable
- PASS  Well-supported tip not Critical by default — STABLE

## Collapse / nav blocking
- PASS  Blocking field created
- PASS  Debris blocks tunnel open
- PASS  Nav walkable false through debris
- PASS  Minor debris does not block nav

## Clearance times
- PASS  Hauler clearance completes in meaningful time — hours=2.80
- PASS  Clearance restores IsTunnelOpen

## Injury distribution (forced samples)
- PASS  Minor debris often no/limited injury — softOrMiss=40/40
- PASS  Major can inflict multiple injuries — multi=21/30
- PASS  Major can incapacitate — incap=17/30
- FAIL  Critical trauma rare vs samples — critHits=14
- PASS  Incapacitated != Dead
- PASS  Incapacitated remains alive
- PASS  Clear incap keeps injuries path

## Incapacitated excavator cannot self-clear
- PASS  CanClearDebris false when incapacitated

## Injury cause enum
- PASS  TunnelCollapse cause exists

## Collapse rates (design)
| Band | Approx fire / eval (~0.55h) | Notes |
|------|------------------------------|-------|
| STABLE | 0 | No natural fire |
| WATCH | 0 | Warn only (rare); no debris drop |
| UNSTABLE | ~0.8% | End-tunnel tips only |
| CRITICAL | ~2.2% | End-tunnel tips only; camp/yard immune |

Camp safe radius: 9.5w
End-tunnel min camp dist: 7.5w

## Clearance design hours
| Minor | 0.35h |
| Blocking | 1.6h |
| Major | 3.4h |
| Hauler mul | 0.55 |
| Excavator mul | 1 |

## Result
**Result:** FAIL  (37 passed, 2 failed)
