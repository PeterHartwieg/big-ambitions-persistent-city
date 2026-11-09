# F9c perf: integrated city fixture and scenario, 9 November 2026

Offline tool cost on a Mac (Apple M1 Pro), .NET 10 Debug test build (the default `dotnet test` configuration). These numbers are **tool cost** of the acceptance scenario: harness, oracles, saves and the simulated city together. They are not mod work. F9 never times frames or computes p95 (plan section 3.8). In-game mod-work p95, ≥ 30 ms spikes and whole-frame timing come from F4's windows on the `f9-city-ready` checkpoint in the batched rig. #128 stays a separate investigation.

Reproduce: `dotnet test tests/PersistentCity.Tests.csproj --filter "FullyQualifiedName~F9IntegratedCityTests&Size=city-s"` (or `Size=city-m`) with `--logger "console;verbosity=detailed"`; each arm fact prints its line.

## Test time

| Run | Tests | Wall time |
| --- | ---: | ---: |
| `F9IntegratedCityTests`, both sizes | 28 | 23.5 s |
| `Size=city-s` only | 16 | 5.0 s |
| `Size=city-m` only | 10 | 20.9 s |
| Whole suite with F9c | 4,316 | 7 min 5 s |
| Whole suite after review round 1 | 4,317 | 7 min 17 s |

`city-m` stays in the default suite: about 21 s is within a unit-test budget next to F5h's acceptance class. F9a's `full` stage can still select either size by trait.

## Tool cost per simulated day

Each arm runs three game days (06:00 day 1 to 06:00 day 4). Two runs in separate processes; seconds for the whole arm, then milliseconds per simulated day.

| Arm | city-s run 1 | city-s run 2 | city-m run 1 | city-m run 2 |
| --- | ---: | ---: | ---: | ---: |
| reference (synchronous drain) | 0.57 s / 191 ms | 0.57 s / 189 ms | 2.47 s / 823 ms | 3.56 s / 1,186 ms |
| fps30 (30 FPS, 32 units, 4 ms) | 0.40 s / 133 ms | 0.39 s / 130 ms | 2.40 s / 801 ms | 2.42 s / 805 ms |
| fps60 (60 FPS, 16 units, 2 ms) | 0.37 s / 124 ms | 0.38 s / 127 ms | 2.44 s / 813 ms | 2.43 s / 809 ms |
| fps120 (120 FPS, 128 units, 2 ms) | 0.35 s / 118 ms | 0.38 s / 126 ms | 2.44 s / 815 ms | 2.63 s / 876 ms |
| hidden skip (60 FPS, 2 h skip) | 0.40 s / 134 ms | 0.40 s / 133 ms | 2.47 s / 822 ms | 2.49 s / 831 ms |
| reload (60 FPS, 9 reloads) | 1.28 s / 425 ms | 1.31 s / 436 ms | 5.56 s / 1,852 ms | 5.56 s / 1,853 ms |

Sizes: city-s has 126 residents and 202 persons by day 4, with 9,399 ledger events. city-m has 957 residents and 2,552 persons, with 91,511 events. The reference is the first arm each process runs, so its time includes JIT warm-up and varies between processes. The reload arm's extra time is its nine save, read and rebuild cycles.

## After review round 1: postings in the event ledger

The ledger now also records every wallet posting (a scan of all wallets at each drained native minute) and the buyer's wallet at each sale. One run, same machine:

| Arm | city-s | city-m |
| --- | ---: | ---: |
| reference | 0.94 s / 312 ms | 4.75 s / 1,583 ms |
| fps30 | 0.54 s / 178 ms | 4.87 s / 1,623 ms |
| fps60 | 0.51 s / 170 ms | 4.85 s / 1,616 ms |
| fps120 | 0.49 s / 165 ms | 4.90 s / 1,633 ms |
| hidden skip | 0.71 s / 238 ms | 4.84 s / 1,614 ms |
| reload | 0.87 s / 291 ms | 8.01 s / 2,669 ms |

`F9IntegratedCityTests` (29 tests) now takes 38.6 s; city-m about doubled, from the per-minute wallet scan over 2,552 wallets. That is still within the unit-test budget, so city-m stays in the default suite. Ledgers: 10,235 events (city-s), 102,088 (city-m).

## After review round 2: postings captured where they execute

The per-minute wallet scan is gone. Each simulator unit now reads the posting queue's minimum before it runs and records a posting only when the unit made one (one wallet per posting unit). One run:

| Arm | city-s | city-m |
| --- | ---: | ---: |
| reference | 0.66 s / 220 ms | 2.49 s / 831 ms |
| fps30 | 0.44 s / 148 ms | 2.57 s / 858 ms |
| fps60 | 0.43 s / 144 ms | 2.60 s / 865 ms |
| fps120 | 0.43 s / 142 ms | 2.54 s / 847 ms |
| hidden skip | 0.44 s / 148 ms | 2.55 s / 849 ms |
| reload | 1.07 s / 356 ms | 5.95 s / 1,983 ms |

`F9IntegratedCityTests` (29 tests) takes 24.8 s, back near the original 23.5 s. Ledgers: 10,539 events (city-s), 107,862 (city-m).

## Cheap wins

None taken. The scenario is cheap enough that no change was worth its review cost. The largest share is the reload arm's save and read through the compact writer, which is production code outside F9c's scope. The four parked hotspots stay parked; F9c touches no product code.

## Standing gate

F9c adds no mod work: no Core, Runtime or Persistence file changed. The gate (mod-work p95 < 5 ms, every ≥ 30 ms mod-work spike flagged, whole-frame timing separate) is unchanged by this slice. It is measured natively by F4 on the F9 checkpoint, not here.
