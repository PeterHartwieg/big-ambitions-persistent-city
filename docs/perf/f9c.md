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

## Cheap wins

None taken. The scenario is cheap enough that no change was worth its review cost. The largest share is the reload arm's save and read through the compact writer, which is production code outside F9c's scope. The four parked hotspots stay parked; F9c touches no product code.

## Standing gate

F9c adds no mod work: no Core, Runtime or Persistence file changed. The gate (mod-work p95 < 5 ms, every ≥ 30 ms mod-work spike flagged, whole-frame timing separate) is unchanged by this slice. It is measured natively by F4 on the F9 checkpoint, not here.
