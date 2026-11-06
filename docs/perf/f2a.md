# City audit F2a — movement-equivalence harness: performance

6 November 2026, offline. Refs [#89](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/89). Measurement named in the [F2 plan](../plans/f2-movement.md#6-performance) section 6 for F2a: harness cost per simulated day and per arm; no mod work. [Checks](../checks/f2a.md).

## Mod work

F2a changes no Core, Runtime or Persistence code, so in-game mod-work p95, ≥ 30 ms spikes and whole-frame timing are unchanged and not measured here. No cheap mod-work win applies to a tool-only slice. In-game numbers for F2 come from the plan's rig check 10.

## Tool cost

`dotnet run --project tools/movement-equivalence -c Release -- --out tools/movement-equivalence/baseline.json`, Release, .NET 10, one process, on the development machine. 957 residents, seed 0, 2.125 simulated days per arm (00:00 day 1 to 03:00 day 3), 1/3 game minute per real second (about 552,700 frames per arm at 60 fps). Wall time per arm from `MovementResult.Seconds`; allocation from `GC.GetTotalAllocatedBytes`, including the SHA-256 of three full saves and the journey ledger.

| Arm | Frames | s per arm | s per simulated day | Allocated MB |
| --- | ---: | ---: | ---: | ---: |
| bodies-off/plain-30 | 276,320 | 1.9 | 0.9 | 1,182 |
| bodies-off/plain-60 | 552,689 | 3.0 | 1.4 | 1,182 |
| bodies-off/plain-120 | 1,104,100 | 5.2 | 2.4 | 1,182 |
| caps/1-1 | 553,490 | 3.9 | 1.8 | 1,196 |
| caps/24-12 | 558,119 | 8.6 | 4.0 | 1,279 |
| focus/stationary (48/24) | 561,798 | 12.4 | 5.8 | 1,364 |
| caps/256-64 | 571,741 | 34.5 | 16.2 | 1,709 |
| cadence/30fps | 284,293 | 5.8 | 2.7 | 1,280 |
| cadence/120fps | 1,113,678 | 17.1 | 8.0 | 1,526 |
| transitions/reload-mid-journey | 561,717 | 9.7 | 4.6 | 1,445 |

Whole run (nine self-checks at 96 residents, then 21 arms): **229 s** after the review fixes (199 s before; the per-arm times above are from that first run and moved by up to 30 % between runs on the shared machine). The unit tests run 96 residents for one day, plus one 96-resident two-day and one 200-resident one-day run (about 1.5 minutes in Debug for the eleven F2a tests).

Cost grows with bound bodies (one lease sample, projection and observation per body per frame, plus 0.25 s selection) and with frame count. Plain arms show the simulation's own cost at these frame rates: 0.9–2.4 s per simulated day.

## Tool-side choices that keep it bounded

- Link order is captured once per 5-minute epoch (612 captures per arm), not per frame.
- Saves are serialized and hashed only at the three 03:00 boundaries and at the reload.
- The driver keeps Runtime's 0.25 s selection and four-attempt budget, so selection cost per frame is the same order as in game.
