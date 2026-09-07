# Prospector Workstation + Faster Analysis Pass Audit

Generated: 2026-09-05 22:03:14

## Exact before → after timing

| Clock | BEFORE | AFTER |
|-------|--------|-------|
| Production analysis clamp | 8–22h | **1.5–4h** |
| Production base (complexity) | Lerp(11,18) | Lerp(2,3.6) × speed |
| Testing analysis scale | ×0.055 | ×0.12 |
| Testing analysis clamp | 0.22–0.85h | **0.14–0.48h** |
| Production cross-check | 3.2–8.8h | **0.55–1.6h** |
| Consult dwell (testing) | 0.07h | **0.035h** |
| Consult dwell (production) | 0.55h | **0.15h** |
| Refiner consult floor | 0.2h | **0.05h** |

Real-time @ 12s/game-hour (testing, 1×):
- Analysis BEFORE ~2.6–10.2s → AFTER ~1.7–5.8s
- Consult BEFORE ~0.84s → AFTER ~0.42s

- PASS  Production band within 1.5–4.0h — weak=4.00 strong=1.62
- PASS  Strong Prospector faster than weak (production) — strong=1.62 weak=4.00
- PASS  Production is fraction of 8h shift (<5h)
- PASS  Production not instant (>1.2h)
- PASS  Testing clamp 0.14–0.48h — weak=0.48 strong=0.19
- PASS  Testing max faster than BEFORE
- PASS  Consult testing shorter
- PASS  Consult production shorter

## Sample durations (formula mirror)
| Mode | Weak/complex | Strong/simple |
|------|--------------|---------------|
| Production | 4.00h | 1.62h |
| Testing | 0.48h | 0.19h |

## Workstation / wiring (source)
- PASS  Source AFTER production clamp 1.5–4.0
- PASS  Source AFTER testing constants
- PASS  ProspectorWorkstationSite exists
- PASS  Runner spawns workstation
- PASS  BindWorkstation wired
- PASS  Desk prefers workstation over scanner
- PASS  Consult routes to analysis table
- PASS  Refiner floor 0.05
- PASS  DEV timing readout present
- PASS  Scan acquisition MinScanHours still 48

**Result:** PASS  (18 passed, 0 failed)
