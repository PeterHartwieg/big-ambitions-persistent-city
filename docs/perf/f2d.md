# F2d perf: cars follow the traffic kernel, 9 November 2026

Offline measurements on a Mac (Apple M1 Pro, 8 cores), .NET 10 Release test build. These are not in-game mod-work numbers. In-game mod-work p95, ≥ 30 ms spikes and whole-frame timing come from the batched rig (check 7 in [the F2d checks](../checks/f2d.md)).

"Before" is the base build (`c42240f`, traffic legs leased) with only the measurement scaffolding added: the kernel counters, the car pool double and `PhysicalCarPerfTests`, committed first on this branch as `7755c0e`. "After" is this branch. The same opt-in test produced both (`PC_F2D_MEASURE=<file> dotnet test tests/PersistentCity.Tests.csproj -c Release --filter PhysicalCarPerfTests`).

## Kernel work with 24 visible cars

`PhysicalCarPerfTests` runs the production `PhysicalPopulation` on the native doubles with 24 car commuters on 24 straight six-section roads inside the radius. A third drive at 0.6x the kernel's pace and the rest at 1.2x. The city advances 1/180 game minute per 1/60 s frame. After the warm-up until all 24 cars are bound, it times 1,800 frames.

| Build | Kernel `Reconsider` per frame | Kernel events per game hour | Same window without bodies |
| --- | ---: | ---: | ---: |
| before (leases) | 24.05 | 288.0 | not comparable: the leased heads held the kernel |
| after | 0.15 | 864.0 | 864.0 |

Before, every leased car reported its progress every frame, and each report reconsidered its link: about one `Reconsider` per car per frame. Its leased heads also slowed the kernel, so fewer events ran in the window. After, kernel work and events equal the run without bodies.

The movement harness (957 residents, one simulated day plus the 03:00 sample, up to 24 visible cars, 60 fps):

| Arm | Build | `Reconsider` per frame | Kernel events per game hour |
| --- | --- | ---: | ---: |
| bodies-off | either | 0.226 | 1,286.5 |
| focus/stationary 48/24 | before | 4.969 | 1,292.8 |
| focus/stationary 48/24 | after | 0.226 | 1,286.5 |
| lag/slow-head-car | before | 5.022 | 1,292.0 |
| lag/slow-head-car | after | 0.226 | 1,286.5 |

In the full two-day harness run every body arm processes exactly the bodies-off kernel events (68,596 with the review's real car route shape) and reconsiderations. That includes 30 and 120 fps, so background traffic events no longer depend on frame-rate observations.

## `PhysicalPopulation.Update` at 24 cars

Same fixture, 1,800 timed `Update` calls:

| Build | Run | Cars bound | p50 | p95 | max | ≥ 30 ms |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| before (leases) | 1 | 24 | 0.0772 ms | 0.1156 ms | 4.5708 ms | 0 |
| after | 1 | 24 | 0.0692 ms | 0.1104 ms | 11.5394 ms | 0 |
| after | 2 | 24 | 0.0720 ms | 0.0825 ms | 1.0408 ms | 0 |
| after | 3 | 24 | 0.0708 ms | 0.0830 ms | 0.8197 ms | 0 |
| after | 4 | 24 | 0.0712 ms | 0.0822 ms | 0.9127 ms | 0 |

Result: p50 down about 0.006 ms and p95 down 0.005 to 0.03 ms, well under the 5 ms gate. No sample reached 30 ms. The 11.5 ms maximum was a single frame in the first run after a rebuild and did not recur in three more runs. I read it as JIT or GC, not follower work. The double's kernel calls are real, but its native calls are field writes, so the native command cost (`CommandLimit` per physics step, now one kernel snapshot instead of a lease sample plus a snapshot) is not timed here. Rig check 7 measures it.

## Car lag and corrections in the harness

Full run (`dotnet run --project tools/movement-equivalence -c Release`, 957 residents, two days, seed 0). The double has no camera, so every correction is an off-screen release and re-acquisition at the logical pose. Car routes have the Runtime shape since review round 1 (preparation pause, road sections, kerb connector, parking pause). Native lag model: cars drive at nominal pace; in `lag/slow-head-car` every fourth car body drives at 0.5x.

| Arm | E1 | Car frames | Lag p50 | Lag p95 | Lag max | Catch-up frames (> 5 m) | Corrections (> 30 m) | Car handoffs |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| focus/stationary | equal | 2,633,690 | 0.15 m | 0.50 m | 6.32 m | 20,210 | 0 | 2,199 |
| focus/sweep | equal | 2,230,637 | 0.05 m | 0.50 m | 6.32 m | 9,402 | 0 | 977 |
| caps/1-1 | equal | 120,092 | 0.20 m | 2.20 m | 6.20 m | 1,778 | 0 | 339 |
| caps/256-64 | equal | 6,246,672 | 0.15 m | 0.50 m | 6.32 m | 34,994 | 0 | 5,159 |
| lag/ideal | equal | 2,633,866 | 0.15 m | 0.50 m | 6.32 m | 20,189 | 0 | 2,181 |
| lag/slow-head-car | equal | 2,632,905 | 0.15 m | 5.00 m | 30.08 m | 131,713 | 221 | 2,125 |
| failures/50pct | equal | 2,596,111 | 0.15 m | 0.50 m | 6.32 m | 15,197 | 0 | 1,423 |
| cadence/30fps | equal | 1,318,401 | 0.15 m | 0.80 m | 6.48 m | 11,377 | 0 | 2,155 |
| cadence/120fps | equal | 5,262,483 | 0.15 m | 0.30 m | 6.20 m | 40,369 | 0 | 2,195 |
| transitions/reload-mid-journey | equal | 2,629,329 | 0.15 m | 0.50 m | 6.32 m | 19,933 | 0 | 2,183 |

Against the E3 car target (p95 ≤ 8 m): met in every arm, 5.00 m with slow head cars. The 6.3 m maximum in the nominal arms is about one storage spacing (6 m on the harness's 12 m, capacity-2 links). A queued car's kernel coordinate jumps by up to that much when the car ahead leaves its link, and a body at nominal pace then needs a few seconds to catch up. The slow-head arm's 221 corrections are its 0.5x cars crossing 30 m; each is released and re-acquired at the logical pose.

## Phase mismatch

Not measured offline. Neither the harness nor the runtime double has native signals, and the counter reads Gley's waypoint stop flag. The two phase tests in `PhysicalCarFollowTests` only show that the counter records each kind. The count, and the share of car lag caused by phase disagreement, come from rig check 7.

## Cheap wins

None taken. Nothing in the follower showed up as a hotspot. The removed lease reports are the win the plan expected, and the four parked hotspots stay parked.
