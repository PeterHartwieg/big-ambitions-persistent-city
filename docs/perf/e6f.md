# E6f perf: non-original vehicle products, 10 November 2026

Offline measurements on a Mac (Apple M1 Pro), .NET 10 Release test build, three separate processes. They are not in-game mod-work numbers. Mod-work p95 in the game, spikes ≥ 30 ms there and whole-frame timing come from the batched rig ([E6f checks](../checks/e6f.md), check 6). No game, rig, autosave setting or lease was used.

Reproduce (each run writes its lines to `$TMPDIR/e6f-measure.txt`):

`PC_E6F_MEASURE=1 dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~VendorDynamicProductTests.ChoiceCost`

The test is `VendorDynamicProductTests.ChoiceCostWithEightItemVehicleMenusAndDynamicNames`. The baseline arm is the E6d shape: a vehicle with two original items and no product policy. The comparison arm is an eight-item vehicle with six dynamic names under the held policy. Both arms run with the resident choice limit (`FoodMenuMaximumOffersPerShop`) at 8, so the whole eight-item menu enters each decision. The test asserts that (`captured == items.Length`) and that every meal is paid.

## Whole-vehicle admission and capture

`CanServe` checks every menu item against the policy. `CaptureBusiness` is what one decision context reads per vehicle candidate. 1,000 samples after warm-up.

| Operation | Process | 2 original, no policy p50 / p95 / max | 8 items, held policy p50 / p95 / max | B/call 2 / 8 |
| --- | ---: | --- | --- | --- |
| Admission | 1 | 0.69 / 0.76 / 26.2 µs | 0.72 / 3.51 / 46.9 µs | 280 / 245 |
| Admission | 2 | 0.67 / 0.72 / 26.3 µs | 0.47 / 3.38 / 48.2 µs | 280 / 234 |
| Admission | 3 | 0.68 / 0.80 / 26.2 µs | 0.72 / 3.86 / 50.1 µs | 280 / 244 |
| Capture | 1 | 1.12 / 1.16 / 47.3 µs | 0.64 / 0.84 / 44.1 µs | 1,010 / 1,306 |
| Capture | 2 | 1.09 / 1.12 / 41.5 µs | 0.63 / 0.84 / 55.6 µs | 1,010 / 1,306 |
| Capture | 3 | 1.10 / 1.19 / 61.5 µs | 0.65 / 1.33 / 56.3 µs | 1,010 / 1,306 |

Admission p95 rises by about 3 µs for eight policy lookups. That is the whole price of dynamic names at this step. Capture allocates 296 B more per call, which is the six extra offers. Both admission arms allocate about 250 B per call in this loop. `SteadyAdmissionOfAnEightItemDynamicMenuAllocatesNothingBeyondTheOriginalThreePath` gates the steady path in the ordinary test run: the eight-item dynamic vehicle allocates exactly as much as the original-items vehicle.

## One city minute with a vehicle decision

24 seeded cities per arm, two warm-up cities not recorded, up to two days each. Every minute step is timed, including the policy read, the decision, the checkout and the receipt. "Decision step" is the minute in which the resident chose.

| Arm | Process | Every step p50 / p95 / max | Decision step p50 / p95 / max | Steps ≥ 5 ms | Steps ≥ 30 ms |
| --- | ---: | --- | --- | ---: | ---: |
| 2 original items | 1 | 0.46 / 0.83 / 71 µs | 57 / 71 / 71 µs | 0 | 0 |
| 2 original items | 2 | 0.46 / 0.83 / 705 µs | 54 / 91 / 705 µs | 0 | 0 |
| 2 original items | 3 | 0.46 / 0.88 / 152 µs | 61 / 99 / 152 µs | 0 | 0 |
| 8 items (6 dynamic) | 1 | 0.46 / 0.83 / 174 µs | 79 / 96 / 174 µs | 0 | 0 |
| 8 items (6 dynamic) | 2 | 0.46 / 0.83 / 577 µs | 81 / 103 / 153 µs | 0 | 0 |
| 8 items (6 dynamic) | 3 | 0.46 / 0.83 / 615 µs | 77 / 165 / 173 µs | 0 | 0 |

16,734 steps per arm and process. The decision step costs about 25 µs more at p50 with eight items, and its p95 stays under 0.17 ms. The slowest step in any run was 0.71 ms, and it came from the two-item baseline, so I read it as noise rather than menu cost. No step reached 5 ms, so no step reached the 30 ms spike line.

## Cheap wins

None taken. The added cost is a few microseconds per decision and there was nothing safe to remove.

## Gate

Offline: the eight-item dynamic vehicle stays far inside mod-work p95 < 5 ms, and no step comes near 30 ms. This is offline evidence only. The in-game mod-work p95, the spike list and whole-frame timing are rig check 6.
