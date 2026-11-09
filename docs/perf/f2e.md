# F2e perf: player holds as world events, 9 November 2026

Offline measurements on a Mac (Apple M1 Pro), .NET 10 Release test build. These are not in-game mod-work numbers. In-game mod-work p95, ≥ 30 ms spikes and whole-frame timing come from the batched rig (plan section 7 check 10; F2e's checks 8 and 9 are in [the F2e checks](../checks/f2e.md)). Whole-frame timing and #128 stay separate.

Reproduce: `PC_F2E_MEASURE=<file> dotnet test tests/PersistentCity.Tests.csproj -c Release --filter PlayerHoldPerfTests`, and `dotnet run --project tools/movement-equivalence -c Release` for the E2 rows.

## Contact detection per body per frame

`PlayerHoldPerfTests` runs the production `PhysicalPopulation` on the native doubles with 48 walkers converging on one workplace inside the radius, 1/60 s frames, 2,400 timed `Update` calls after a 120-frame warm-up. Three modes: no player (the contact pass is skipped, as before F2e), the player 1 km away, and the player 0.5 m ahead of one walker every frame (that walker is held). Each mode ran twice per process, three processes. The first pass of each process is cold: JIT puts its no-player p50 near 0.069 ms. The table shows the warm second passes.

| Process | Mode | p50 | p95 | max | ≥ 30 ms | Holds |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| 1 | no player | 0.0095 ms | 0.0198 ms | 0.434 ms | 0 | 0 |
| 1 | player 1 km away | 0.0125 ms | 0.0232 ms | 0.817 ms | 0 | 0 |
| 1 | player 0.5 m ahead | 0.0131 ms | 0.0228 ms | 0.086 ms | 0 | 17 |
| 2 | no player | 0.0190 ms | 0.0300 ms | 1.039 ms | 0 | 0 |
| 2 | player 1 km away | 0.0110 ms | 0.0194 ms | 3.383 ms | 0 | 0 |
| 2 | player 0.5 m ahead | 0.0116 ms | 0.0191 ms | 1.488 ms | 0 | 17 |
| 3 | no player | 0.0095 ms | 0.0202 ms | 0.223 ms | 0 | 0 |
| 3 | player 1 km away | 0.0126 ms | 0.0227 ms | 1.145 ms | 0 | 0 |
| 3 | player 0.5 m ahead | 0.0130 ms | 0.0264 ms | 2.193 ms | 0 | 17 |

The modes differ by about 0.003 ms at p50, which is inside the spread between processes (process 2's no-player p50 is 0.019 ms). No sample reached 30 ms. The maxima are single frames that did not repeat across processes; I read them as GC or scheduling, not contact work. Everything is far under the 5 ms gate.

The rule itself, isolated (72 bodies, 48 walkers and 24 cars, 20,000 frames, three processes):

| Path | Per body | Per frame at 72 bodies |
| --- | ---: | ---: |
| full rule (leg sample and `PlayerContact.Observe`), player far | 51.8 / 65.5 / 54.6 ns | 0.0037 / 0.0047 / 0.0039 ms |
| full rule, player in contact | 69.6 / 76.6 / 71.9 ns | 0.0050 / 0.0055 / 0.0052 ms |
| pre-check only (`InReach`, no open track), player far | 4.5 / 9.8 / 4.5 ns | 0.0003 / 0.0007 / 0.0003 ms |

The plan estimated under 0.01 ms per frame at 72 bodies; the measured worst case is 0.0055 ms with every body in contact.

## Hold application cost

Wall time of `CitySimulator.HoldForPlayer` for every applied hold in the harness's E2 contact arm (957 residents, one day, contact window 07:00–10:00), in two runs:

| Kind | Holds | p50 | p95 | max |
| --- | ---: | ---: | ---: | ---: |
| walk | 1,874 | 0.0005 ms | 0.0020 / 0.0032 ms | 0.646 / 0.638 ms |
| car | 1,095 | 0.0007 ms | 0.0024 / 0.0026 ms | 0.678 / 0.674 ms |

A walk hold is one leg sample, one due-set remove and add. A car hold is one kernel queue walk for the follower's position (plus at most a few ulp steps) and one `Reconsider`. Both are O(1) in the city size, as F3's bounded-units rule requires. The maxima are first-call JIT.

Holds per hour in the contact arm: 989.7 applied holds per contact hour, from 3,340 reports, with 149 refused as `AllowanceSpent` and 222 as `Unchanged`. This arm is a stress case: the double blocks every third followed body for the whole window. A player blocks one person at a time.

## Save size and harness cost

- Writer 35 adds 140 characters to each golden city: seed 5150 goes from 73,192 to 73,332 characters, and seed 9217 from 72,830 to 72,970. That is 0.19 %. The compact wire stores the all-zero `PlayerHoldMinutes` column and the one sequence number compactly. Nothing is added at save time beyond the two fields.
- Harness: the self-checks plus the E2 rows (one 957-resident contact run with four checkpoints, three replays, five refusals) took 13.8 s; the full harness, 957 residents and two days with all arms, took 153 s.

## Cheap wins

- **Contact pre-check.** Before: `ObservePlayerContact` sampled each body's own leg and ran the full rule on every frame for every followed body, 52–66 ns per body with the player far. After: a squared-distance test against the player's reach (1 m on foot, the 4 m by 2.5 m lane box for a car) plus one dictionary lookup for an open track skips the rest, 4.5–9.8 ns per body. Over 72 bodies that is 0.0037–0.0047 ms down to 0.0003–0.0007 ms per frame. The behaviour is the same: a body out of reach with no open track can produce neither a contact nor a report.
- **No re-reporting after the allowance is spent.** The first 96-resident E2 run showed 10,727 refused `AllowanceSpent` reports. After the first such refusal for a journey, `PlayerContact` and the harness double stop reporting it. The 957-resident arm now has 149.

The four parked hotspots stay parked. No perf-only rewrite.
