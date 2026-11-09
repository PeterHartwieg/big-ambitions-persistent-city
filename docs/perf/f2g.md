# F2g perf: integrated equivalence evidence, 9 November 2026

Offline, on the Windows PC, .NET 10 Release (`dotnet run --project tools/movement-equivalence -c Release`), 957 residents (plus 957 visitors in visitor arms), two simulated days plus the 03:00 sample, seed 0. Other worktrees' dotnet jobs ran on the machine at times, so wall times are indicative. [Checks](../checks/f2g.md).

## Mod work

F2g changes no Core, Runtime or Persistence code, so in-game mod-work p95, ≥ 30 ms spikes and whole-frame timing are unchanged by this slice and not measured here. The standing gate (mod-work p95 < 5 ms, every ≥ 30 ms mod-work spike flagged, whole-frame timing reported separately; #128 stays a separate investigation) is measured by rig check 10. No cheap mod-work win applies to a tool-and-docs slice. The four parked hotspots stay parked.

Offline component totals from the F2 slices, for reference (not in-game mod-work):

| Slice | Measure | Result |
| --- | --- | --- |
| [F2b](f2b.md) | `PhysicalPopulation.Update` on the optional-stop doubles | neutral within noise; 0 samples ≥ 30 ms |
| [F2c](f2c.md) | `Update` with 48 walkers, p50 / p95 / max | 0.059–0.067 / 0.071–0.079 / 0.28–2.69 ms (base 0.051–0.057 / 0.062–0.067 ms); 0 samples ≥ 30 ms |
| [F2d](f2d.md) | Kernel `Reconsider` per frame with 24 cars | 24.05 → 0.15 (double); 4.969 → 0.226 (harness) |
| [F2d](f2d.md) | `Update` at 24 cars, p50 / p95 / max | 0.072 / 0.083 / 0.82–1.04 ms (runs 2–4), 0 samples ≥ 30 ms |
| [F2e](f2e.md) | Hold application (harness, this run) | walk p50/p95/max 0.0006/0.0026/0.74 ms; car 0.0011/0.0045/0.86 ms |
| [F2f](f2f.md) | Selection p95 at 5,000 active journeys | 0.0073 ms (10,000 persons), 0.0120 ms (50,000); target ≤ 0.05 ms |
| [F2h](f2h.md) | `RefreshOptionalStops`, 40 audience stops, p95 | 0.0184 → 0.0115 ms; 0 samples ≥ 30 ms |

## Whole-harness cost

| Stage | Wall time |
| --- | ---: |
| Self-checks (96 residents, one day, 11 runs) | 7.1 s |
| E2: two contact arms with three replays each (957 residents, one day) | 11.3 s |
| 60 arms (120 simulated days) | 341.1 s |
| Total | 359.6 s |

Per family (sum of arm wall time; seconds per simulated day):

| Family | Arms | Seconds | s per simulated day |
| --- | ---: | ---: | ---: |
| bodies-off | 6 | 13.4 | 1.12 |
| focus | 3 | 20.9 | 3.49 |
| caps | 4 | 28.7 | 3.59 |
| lag | 2 | 14.2 | 3.56 |
| failures | 2 | 15.7 | 3.93 |
| cadence | 2 | 18.6 | 4.65 |
| transitions | 2 | 18.0 | 4.51 |
| walking | 13 | 36.5 | 1.40 |
| visitors | 20 | 133.9 | 3.35 |
| combined | 6 | 41.1 | 3.42 |

F2g's added arms (seven visitor arms, six combined arms, the visitor E2 arm) cost about 90 s of the 360 s. Cost grows with bound bodies and frames: 256/64 caps cost about 9–10 s per simulated day, 120 fps about 7 s, plain bodies-off 0.7–1.8 s.

## E3 lag summary

Harness double, no camera, so every correction is an off-screen move. Native lag model: walkers at a seeded 0.8–1.0× pace with a 3 s block every 20 s of real time; cars at nominal pace, and in slow-head arms every fourth car at 0.5×. These are the harness's stand-ins, not native measurements.

| Body | Arms | p50 | p95 | max | Target (plan E3) |
| --- | --- | ---: | ---: | ---: | --- |
| Walker, Native model | all non-ideal body arms | 1.95–2.10 m | 6.60–6.95 m | 8.02–8.07 m | p95 ≤ 2 m: not met under this model (as F2c recorded) |
| Walker, ideal | `lag/ideal`, `walking/lag-ideal`, `visitors/lag-ideal` | 0.10 m | 0.15 m | 0.15–0.16 m | met |
| Car, nominal | all non-slow-head arms | 0.05–0.80 m | 0.30–2.20 m | 6.15–6.48 m | p95 ≤ 8 m: met |
| Car, slow head cars | `lag/slow-head-car`, `visitors/lag-slow-head-car`, two combined arms | 0.15–0.20 m | 4.85–5.50 m | 30.08–34.50 m | met; 221/222/133/112 corrections (> 30 m) |

Walker moves (off-screen moves to the logical pose, > 8 m) per two-day arm: 0 with ideal bodies; 825–1,366 at cap 48 (focus paths and other factors), 441–757 at cap 24, 27–39 at cap 1 and 4,291–5,052 at cap 256. The harness's lag-release counter is non-zero only in the slow-head arms (221, 222, 133, 112), where it equals the car corrections. The E3 numbers match F2c and F2d. Native lag comes from rig checks 4 and 10.

## Per-arm cost and E3 lag

| Arm | Reference | E1 | Bodies acquired | Walker lag p50/p95/max m | Car lag p50/p95/max m | Tool s | s per day |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: |
| bodies-off/plain-30 | bodies-off/plain-60 | equal | 0 | 0.00 / 0.00 / 0.00 | 0.00 / 0.00 / 0.00 | 1.4 | 0.69 |
| bodies-off/plain-60 | - | - | 0 | 0.00 / 0.00 / 0.00 | 0.00 / 0.00 / 0.00 | 2.1 | 1.07 |
| bodies-off/plain-120 | bodies-off/plain-60 | equal | 0 | 0.00 / 0.00 / 0.00 | 0.00 / 0.00 / 0.00 | 3.4 | 1.68 |
| bodies-off/focus-none | bodies-off/plain-60 | equal | 0 | 0.00 / 0.00 / 0.00 | 0.00 / 0.00 / 0.00 | 2.1 | 1.04 |
| bodies-off/plain-reload | bodies-off/plain-60 | equal | 0 | 0.00 / 0.00 / 0.00 | 0.00 / 0.00 / 0.00 | 2.4 | 1.19 |
| bodies-off/plain-hidden-travel | bodies-off/plain-60 | equal | 0 | 0.00 / 0.00 / 0.00 | 0.00 / 0.00 / 0.00 | 2.1 | 1.04 |
| focus/stationary | bodies-off/plain-60 | equal | 5,236 | 2.10 / 6.90 / 8.03 | 0.15 / 0.50 / 6.32 | 7.6 | 3.78 |
| focus/sweep | bodies-off/plain-60 | equal | 5,038 | 2.05 / 6.80 / 8.03 | 0.05 / 0.50 / 6.32 | 6.4 | 3.18 |
| focus/away-return | bodies-off/plain-60 | equal | 4,524 | 2.10 / 6.95 / 8.03 | 0.15 / 0.50 / 6.32 | 7.0 | 3.50 |
| caps/0-0 | bodies-off/plain-60 | equal | 0 | 0.00 / 0.00 / 0.00 | 0.00 / 0.00 / 0.00 | 2.2 | 1.08 |
| caps/1-1 | bodies-off/plain-60 | equal | 589 | 2.00 / 6.75 / 8.03 | 0.20 / 2.20 / 6.20 | 2.5 | 1.23 |
| caps/24-12 | bodies-off/plain-60 | equal | 3,820 | 2.05 / 6.85 / 8.03 | 0.15 / 0.85 / 6.32 | 5.3 | 2.63 |
| caps/256-64 | bodies-off/plain-60 | equal | 11,958 | 2.10 / 6.95 / 8.03 | 0.15 / 0.50 / 6.32 | 18.9 | 9.43 |
| lag/ideal | bodies-off/plain-60 | equal | 5,256 | 0.10 / 0.15 / 0.16 | 0.15 / 0.50 / 6.32 | 6.5 | 3.25 |
| lag/slow-head-car | bodies-off/plain-60 | equal | 5,384 | 2.10 / 6.90 / 8.03 | 0.15 / 5.00 / 30.08 | 7.7 | 3.87 |
| failures/10pct | bodies-off/plain-60 | equal | 4,970 | 2.10 / 6.90 / 8.03 | 0.15 / 0.50 / 6.32 | 8.0 | 4.00 |
| failures/50pct | bodies-off/plain-60 | equal | 4,417 | 2.10 / 6.90 / 8.03 | 0.15 / 0.50 / 6.32 | 7.7 | 3.86 |
| cadence/30fps | bodies-off/plain-30 | equal | 5,192 | 2.10 / 6.90 / 8.07 | 0.15 / 0.80 / 6.48 | 4.5 | 2.26 |
| cadence/120fps | bodies-off/plain-120 | equal | 5,231 | 2.10 / 6.90 / 8.02 | 0.15 / 0.30 / 6.20 | 14.1 | 7.03 |
| transitions/reload-mid-journey | bodies-off/plain-60 | equal | 5,260 | 2.10 / 6.90 / 8.03 | 0.15 / 0.50 / 6.32 | 9.6 | 4.78 |
| transitions/hidden-travel | bodies-off/plain-hidden-travel | equal | 5,115 | 2.10 / 6.90 / 8.03 | 0.15 / 0.50 / 6.32 | 8.5 | 4.24 |
| walking/plain-60 | - | - | 0 | 0.00 / 0.00 / 0.00 | 0.00 / 0.00 / 0.00 | 0.6 | 0.29 |
| walking/plain-30 | - | - | 0 | 0.00 / 0.00 / 0.00 | 0.00 / 0.00 / 0.00 | 0.6 | 0.28 |
| walking/plain-hidden-travel | - | - | 0 | 0.00 / 0.00 / 0.00 | 0.00 / 0.00 / 0.00 | 0.6 | 0.29 |
| walking/stationary | walking/plain-60 | equal | 3,332 | 2.10 / 6.90 / 8.03 | 0.00 / 0.00 / 0.00 | 3.9 | 1.95 |
| walking/sweep | walking/plain-60 | equal | 3,547 | 2.05 / 6.85 / 8.03 | 0.00 / 0.00 / 0.00 | 2.6 | 1.31 |
| walking/away-return | walking/plain-60 | equal | 2,659 | 2.05 / 6.85 / 8.03 | 0.00 / 0.00 / 0.00 | 2.8 | 1.41 |
| walking/caps-1 | walking/plain-60 | equal | 259 | 2.00 / 6.85 / 8.03 | 0.00 / 0.00 / 0.00 | 1.5 | 0.76 |
| walking/caps-256 | walking/plain-60 | equal | 8,750 | 2.10 / 6.95 / 8.03 | 0.00 / 0.00 / 0.00 | 8.5 | 4.27 |
| walking/lag-ideal | walking/plain-60 | equal | 3,367 | 0.10 / 0.15 / 0.15 | 0.00 / 0.00 / 0.00 | 3.2 | 1.59 |
| walking/failures-50pct | walking/plain-60 | equal | 2,916 | 2.10 / 6.90 / 8.03 | 0.00 / 0.00 / 0.00 | 3.3 | 1.64 |
| walking/cadence-30fps | walking/plain-30 | equal | 3,340 | 2.05 / 6.90 / 8.07 | 0.00 / 0.00 / 0.00 | 2.2 | 1.10 |
| walking/reload-mid-journey | walking/plain-60 | equal | 3,350 | 2.05 / 6.90 / 8.03 | 0.00 / 0.00 / 0.00 | 3.5 | 1.74 |
| walking/hidden-travel | walking/plain-hidden-travel | equal | 3,250 | 2.05 / 6.90 / 8.03 | 0.00 / 0.00 / 0.00 | 3.2 | 1.62 |
| visitors/plain-60 | - | - | 0 | 0.00 / 0.00 / 0.00 | 0.00 / 0.00 / 0.00 | 2.4 | 1.20 |
| visitors/plain-30 | - | - | 0 | 0.00 / 0.00 / 0.00 | 0.00 / 0.00 / 0.00 | 1.6 | 0.79 |
| visitors/plain-hidden-travel | - | - | 0 | 0.00 / 0.00 / 0.00 | 0.00 / 0.00 / 0.00 | 2.2 | 1.10 |
| visitors/stationary | visitors/plain-60 | equal | 7,124 | 2.05 / 6.80 / 8.03 | 0.15 / 0.50 / 6.32 | 8.2 | 4.08 |
| visitors/sweep | visitors/plain-60 | equal | 5,394 | 2.05 / 6.80 / 8.03 | 0.05 / 0.50 / 6.32 | 6.9 | 3.46 |
| visitors/away-return | visitors/plain-60 | equal | 6,335 | 2.05 / 6.85 / 8.03 | 0.15 / 0.50 / 6.32 | 7.6 | 3.80 |
| visitors/caps-1 | visitors/plain-60 | equal | 622 | 2.00 / 6.75 / 8.03 | 0.15 / 2.20 / 6.20 | 2.7 | 1.34 |
| visitors/caps-256 | visitors/plain-60 | equal | 17,199 | 2.05 / 6.85 / 8.03 | 0.15 / 0.50 / 6.32 | 20.7 | 10.33 |
| visitors/lag-ideal | visitors/plain-60 | equal | 7,195 | 0.10 / 0.15 / 0.16 | 0.15 / 0.50 / 6.32 | 7.3 | 3.64 |
| visitors/failures-50pct | visitors/plain-60 | equal | 5,417 | 2.05 / 6.85 / 8.03 | 0.15 / 0.50 / 6.32 | 8.3 | 4.16 |
| visitors/cadence-30fps | visitors/plain-30 | equal | 7,143 | 2.05 / 6.85 / 8.07 | 0.15 / 0.80 / 6.48 | 5.2 | 2.61 |
| visitors/reload-mid-journey | visitors/plain-60 | equal | 7,239 | 2.05 / 6.85 / 8.03 | 0.46 / 0.50 / 6.32 | 8.5 | 4.25 |
| visitors/hidden-travel | visitors/plain-hidden-travel | equal | 7,045 | 2.05 / 6.80 / 8.03 | 0.15 / 0.50 / 6.32 | 7.9 | 3.97 |
| visitors/plain-120 | - | - | 0 | 0.00 / 0.00 / 0.00 | 0.00 / 0.00 / 0.00 | 3.6 | 1.78 |
| visitors/focus-none | visitors/plain-60 | equal | 0 | 0.00 / 0.00 / 0.00 | 0.00 / 0.00 / 0.00 | 2.3 | 1.17 |
| visitors/caps-0 | visitors/plain-60 | equal | 0 | 0.00 / 0.00 / 0.00 | 0.00 / 0.00 / 0.00 | 2.4 | 1.19 |
| visitors/caps-24 | visitors/plain-60 | equal | 5,031 | 2.00 / 6.75 / 8.03 | 0.15 / 0.65 / 6.32 | 5.5 | 2.74 |
| visitors/failures-10pct | visitors/plain-60 | equal | 6,641 | 2.05 / 6.85 / 8.03 | 0.15 / 0.50 / 6.32 | 8.4 | 4.19 |
| visitors/lag-slow-head-car | visitors/plain-60 | equal | 7,284 | 2.05 / 6.80 / 8.03 | 0.15 / 4.85 / 31.00 | 8.1 | 4.05 |
| visitors/cadence-120fps | visitors/plain-120 | equal | 7,120 | 2.05 / 6.85 / 8.02 | 0.15 / 0.30 / 6.20 | 14.2 | 7.09 |
| combined/sweep-256-10pct-120fps | bodies-off/plain-120 | equal | 7,483 | 2.05 / 6.85 / 8.02 | 0.05 / 0.30 / 6.20 | 17.9 | 8.97 |
| combined/away-return-1-50pct-30fps-reload | bodies-off/plain-30 | equal | 361 | 2.05 / 6.60 / 8.07 | 0.20 / 0.80 / 6.15 | 1.9 | 0.97 |
| combined/stationary-24-50pct-slow-head-hidden | bodies-off/plain-hidden-travel | equal | 2,716 | 2.05 / 6.85 / 8.03 | 0.20 / 5.35 / 34.50 | 5.0 | 2.50 |
| combined/visitors-sweep-256-50pct-30fps-reload | visitors/plain-30 | equal | 7,853 | 2.05 / 6.80 / 8.07 | 0.80 / 0.80 / 6.48 | 6.6 | 3.29 |
| combined/visitors-away-return-24-10pct-slow-head-hidden | visitors/plain-hidden-travel | equal | 4,103 | 2.00 / 6.80 / 8.03 | 0.20 / 5.50 / 31.00 | 5.3 | 2.67 |
| combined/visitors-stationary-1-10pct-120fps | visitors/plain-120 | equal | 635 | 1.95 / 6.65 / 8.02 | 0.15 / 1.95 / 6.20 | 4.3 | 2.14 |
