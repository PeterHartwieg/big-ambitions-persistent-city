Status: implemented (offline); not verified by rig

# City audit F2g: integrated equivalence evidence, 9 November 2026

Refs [#89](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/89). Branch `codex/pc-f2g-equivalence-evidence` from `codex/persistent-city` at `baa775d` (F5i #179 merged; current writer 36 from E5b4 #180), then merged with `codex/persistent-city` at `b85731d` (E5b5 #181, X1–X3, F4e #176; writer still 36). Scope: the F2g row of the [F2 plan](../plans/f2-movement.md#4-slice-breakdown): the final movement-equivalence run across every arm and the acceptance table with numbers. Docs and tool arms only. No Core, Runtime or Persistence change, no schema or save change.

## What changed

- **`tools/movement-equivalence/MovementArms.cs`**, additive arms:
  - Visitor bodies on, for the plan factors F5i had not run: `visitors/plain-120` (bodies-off reference), `focus-none`, `caps-0` (0/0), `caps-24` (24/12), `failures-10pct`, `lag-slow-head-car` and `cadence-120fps`. With F5i's ten arms, every factor level of the plan row now also runs with visitors eligible for bodies.
  - A `combined` family. Each arm varies several factors at once and is compared with bodies-off at its own cadence and transition:
    - `sweep-256-10pct-120fps`
    - `away-return-1-50pct-30fps-reload`
    - `stationary-24-50pct-slow-head-hidden`
    - `visitors-sweep-256-50pct-30fps-reload`
    - `visitors-away-return-24-10pct-slow-head-hidden`
    - `visitors-stationary-1-10pct-120fps`
- A `pairwise` family (review round 1): a deterministic greedy covering array over the full factor product: residents or visitors (2) × focus path (4) × caps (5) × failures (3) × cadence (3) × lag model (3) × transition none/reload/hidden/reload+hidden (4). The 24 arms hold every valid pair of factor levels at least once. A reload with focus none or caps 0/0 binds no body, so it would be the bodies-off reload (self-check and `bodies-off/plain-reload`); those four pairs are excluded. Each arm is compared with bodies-off with the same city, cadence and hidden travel; four new references (`bodies-off/plain-hidden-30`, `-120`, `visitors/plain-hidden-30`, `-120`) give hidden travel at 30 and 120 fps. The full product would be 4,320 tuples (about 6 hours per run at 957 residents), so it is not run.
- **`HoldReplay.Check(..., visitors: true)`**: the F2e E2 arm with visitor bodies on. Holds refuse visitors (F2e/F5i), so the row "visitor bodies bound and only residents held" checks that the logged stream has no `pv:` person. The replays and the five segment refusals then run as in F2e. `Program.cs` runs both E2 arms.
- **`MovementRun.cs`**: a reload arm that never reloads now throws `HarnessViolation` ("mid-journey reload never happened"). Before, it would have passed E1 without testing a reload.
- **`Program.cs`**: the exit code is 1 when an arm DIFFERS, not only when a self-check fails. Wall time per stage (self-checks, E2, arms, total) is printed and written to the JSON.
- **`baseline.json`**: replaced F2a's expected-red output with the green result below (957 residents, two days, seed 0).
- **`tests/MovementEquivalenceTests.cs`** (new tests):
  - `CombinedArmsEqualBodiesOff`
  - `ReloadArmThatNeverReloadsFailsLoudly`
  - `E2ReplayIsGreenWithVisitorBodies`
  - `RecordedBaselineIsTheGreenMatrix`, which asserts that every self-check and E2 row in `baseline.json` passes, that every catalogue arm was recorded and that every compared arm is identical on all seven projections.
  - `ArmCatalogueCoversThePlanRow` now also covers the visitor and combined factor levels.
  - `PairwiseArmsCoverEveryPairOfTheFullProduct`: reads the factor levels back from the pairwise arms' options, asserts every valid pair of the full product is covered, that exactly the four reload pairs without bodies are excluded, that reload+hidden runs with and without visitors, and that each arm's reference matches its city, cadence and hidden travel.
  - `PairwiseArmsEqualBodiesOff`: runs the 24 pairwise arms on the 96-resident fixture; each equals its reference, every reload arm reloads with bodies bound and every hidden arm travels hidden.
- `README.md` of the harness updated.

## Offline evidence

Command, 9 November 2026, on the Windows PC (other worktrees' dotnet jobs were running at times): `dotnet run --project tools/movement-equivalence -c Release -- --out tools/movement-equivalence/baseline.json`. Exit code 0.

- **Self-checks: 9 of 9 pass.**
  - The same seed is byte-identical, plain (`3b104857…`) and with bodies (850 acquired).
  - Another seed differs.
  - The no-focus and zero-cap drivers equal plain.
  - A bodies-off mid-journey reload resumes byte-identical (9 in flight). A reload with bodies bound resumes (8 bodies, 13 in flight).
  - Injected faults fail loudly. `DuplicateArrival`: "journey ledger: completed journeys differ from observed arrivals (557 vs 556)". `SkippedEvent`: "observation epoch not published".
- **E2: 19 of 19 rows pass** (one simulated day, contact window 07:00–10:00, checkpoints at minutes 1441/1920/1980/2160).

  | E2 arm | Holds walk / car | Absorbed (input > charge) | Refused | Replays equal (holds applied) | Refuses gap, duplicate, reorder, wrong start, wrong end |
  | --- | ---: | ---: | --- | --- | --- |
  | residents | 1,876 / 1,117 | 266 | AllowanceSpent 148, Unchanged 209 | from 1441 (2,993), 1920 (2,685), 1980 (661) | 5 of 5 |
  | visitor bodies on | 1,816 / 1,116 | 263 | AllowanceSpent 145, Unchanged 208, Visitor 1,235 | from 1441 (2,932), 1920 (2,624), 1980 (640) | 5 of 5 |

  In the visitor arm, 1,180 visitor bodies were bound and all 2,932 logged holds are for residents.
- **E1: 76 of 76 compared arms are equal to their bodies-off reference on all seven projections**: save bytes, ordered sales, journeys, parked cars, wallets, per-link FIFO order and visitors, at each drained 03:00 boundary (minutes 1620, 3060, 4500). Every outcome delta (`Equivalence.Outcomes`) is 0 in every arm. The 24 pairwise arms are part of the 76 (10 November run). The full arm table, with bodies and lag, is in [the F2g perf file](../perf/f2g.md#per-arm-cost-and-e3-lag).
- **Cross-cadence.** `bodies-off/plain-30` and `plain-120` equal `plain-60`. F2a recorded them as different ([F2a finding](f2a.md)); at this base they are equal. So every cadence arm equals the 60 fps bodies-off run as well as its own-cadence reference.

Reference outcomes (bodies-off 60 fps, end of day 2; equal in every arm):

| Metric | Value |
| --- | ---: |
| Arrivals (walk / car journeys) | 10,745 (6,973 / 3,772) |
| Walk journey mean / p95 | 25.04 / 55.41 min |
| Car journey mean / p95 | 34.31 / 137.31 min |
| Shop arrivals / admitted at shop / work arrivals | 2,599 / 2,035 / 2,347 |
| Missed activities | 724 |
| Meals bought (own sales) / skipped | 651 / 181 |
| Own revenue units | 94,891,933,696,000 |
| Resident wallet units | 47,953,725,923,611,980 |
| Cars parked away from home | 473 |
| Kernel traffic events | 68,596 |
| Queued link entries in the 612 FIFO samples | 6,335 |
| Visitors (957): arrivals / purchases / food-spending units | 5,788 / 41 / 7,301,444,403,200 |

Transitions and faults inside the green arms:

| Arm | Evidence that the factor was exercised |
| --- | --- |
| transitions/reload-mid-journey | 1 reload with 40 bodies bound, 96 in flight |
| visitors/reload-mid-journey | 1 reload with 72 bodies bound, 250 in flight; the bound visitor got a body again (1 of 1) |
| combined/visitors-sweep-256-50pct-30fps-reload | 1 reload with 137 bodies bound, 597 in flight; 7,742 failed acquisitions |
| combined/away-return-1-50pct-30fps-reload | 1 reload with 2 bodies bound (caps 1/1), 96 in flight |
| transitions/hidden-travel | 1,134 hidden frames, 36 releases before the hidden slice |
| combined/stationary-24-50pct-slow-head-hidden | 1,134 hidden frames, 35 releases, 2,695 failed acquisitions, 133 car corrections |
| failures/10pct, failures/50pct | 534 and 4,451 failed acquisitions |
| lag/slow-head-car | 221 car corrections (> 30 m), car lag p95 5.00 m |
| pairwise, 5 reload+hidden arms | each 1 reload then a hidden slice: 2 to 72 bodies bound at reload, 586–2,190 hidden frames; `visitors-away-return-48-24-0pct-60fps-slow-head-reload-hidden` reloads with visitor `pv:11` bound |
| pairwise, 5 reload-only arms | 1 reload each, 2 to 33 bodies bound; `visitors-sweep-24-12-0pct-30fps-ideal-reload` with visitor `pv:6` bound |
| pairwise, failures | 72 to 6,611 failed acquisitions in the 10 % and 50 % arms with bodies |

Every frame of every arm asserts that no traffic vehicle is under physical control, and every hidden slice asserts that no body is bound.

## Acceptance table

Each row of plan section 1, mapped to passing checks. E1 is exact equality, E2 is replay equality, and E3 is visual tolerance only.

| # | Acceptance item | Contract | Passing check and numbers |
| --- | --- | --- | --- |
| A1 | Authority for progress, occupancy, arrival and admission at each detail level and during transfer | E1 | `PresentationBoundaryTests.PhysicalRuntimeCallsOnlyTheReviewedSimulationMembers` (F2b, shrunk in F2c/F2d to read-only plus `HoldForPlayer`). The harness requires, in every arm and frame, that no vehicle is under physical control (0 violations in 60 arms, up to 1,104,100 frames per arm) and that no body is bound during a hidden slice. Transfers in and out (acquire, release, cap, failure, reload, hidden travel) are covered by the 76 equal arms. |
| A2 | Stationary, sweep and away/return camera paths without player interaction, with different body caps | E1 | `focus/stationary`, `sweep`, `away-return` and `bodies-off/focus-none`, plus `caps/0-0`, `1-1`, `24-12`, (48/24 default) and `256-64`, each equal to `bodies-off/plain-60`. The same with visitor bodies (`visitors/*`) and walking-only (`walking/*`). Bodies acquired: 0 to 11,958 (residents), 0 to 17,199 (with visitors). Tests `MixedCityBodyArmsEqualBodiesOff`, `VisitorBodyArmsEqualBodiesOff`, `RecordedBaselineIsTheGreenMatrix`. |
| A3 | Journey times, admitted visits, missed routines, meals and revenue under a declared contract | E1 | Exact equality of the save bytes, sales and journey projections, and every `Equivalence.Outcomes` delta is 0 in all 76 arms: walk 25.04/55.41 min, car 34.31/137.31 min, 2,035 admitted, 724 missed, 651 meals, 94,891,933,696,000 revenue units. |
| A4 | Detail transitions, acquisition failures, interrupted journeys, fast-forward and reload | E1 | `transitions/reload-mid-journey` and `hidden-travel`, `failures/10pct` and `50pct`, the visitor and walking versions, the six `combined/*` arms and the 24 `pairwise/*` arms, which cover every pair of focus path, caps, failures, cadence, lag and none/reload/hidden/reload+hidden (table above), all equal. `PairwiseArmsCoverEveryPairOfTheFullProduct`, `PairwiseArmsEqualBodiesOff`. Self-checks: bodies-off reload byte-identical, reload with bodies bound resumes. `ReloadArmThatNeverReloadsFailsLoudly`, `HiddenTravelReleasesBodiesBeforeTheHiddenSliceAdvances`. |
| A5 | Player collision and blockage as explicit external interactions | E2 | The E2 arms: 2,993 holds (residents) and 2,932 holds (visitor bodies on) replayed from three checkpoints, each equal to the contact run's saved bytes. A gap, duplicate, reorder, wrong start or wrong end is refused (5 of 5, both arms). `PlayerHoldTests`, `E2ReplayIsGreenWithVisitorBodies`. |
| A6 | Identity, queue order, car ownership and saved position preserved | E1 | The save-byte projection (ids, journeys, cars) plus separate `fifo`, `parked` and `wallets` projections are equal in all 76 arms. 6,335 queued link entries in 612 FIFO samples and 473 cars parked away are identical across arms. The same seed is byte-identical. |
| A7 | One movement contract across distances; native bodies follow its permissions | E1 + E3 | Walkers (F2c) and cars (F2d) follow the logical pose. `walking/*` (10 arms), mixed (15 body arms) and visitor arms are equal. Lag is bounded: see A9. |
| A8 | Signals, queues, capacity and access restrictions that affect arrival are logical outside the camera | E1 | The kernel's FIFO, capacity-2 links and signals produce 68,596 traffic events in every arm, bodies on or off, at 30/60/120 fps. `lag/slow-head-car` and the slow-head combined arms (221, 133 and 112 car corrections) leave FIFO order and arrivals unchanged. Visible-only crossing waits are presentation lag, measured by rig check 10. |
| A9 | If exact equivalence is impractical, define and test the divergence | E3 | Outcomes are exact (E1/E2). The only divergence is visual body lag. Walker lag p50/p95/max: 2.10/6.90/8.03 m (Native model), 0.10/0.15/0.16 m (ideal). Car lag: 0.15/0.50/6.32 m, and 0.15/5.00/30.08 m with slow head cars. The car target (p95 ≤ 8 m) is met in every arm but one: `pairwise/visitors-away-return-48-24-0pct-60fps-slow-head-reload-hidden` has car p95 8.35 m under the slow-head stand-in (every fourth car at 0.5×). The walker target (p95 ≤ 2 m) is met with ideal bodies only; F2c records the harness's pessimistic Native model. Offline, the walker target and the car target in that slow-head arm are accepted only as harness stand-in figures, not as native lag; rig check 4 is the check for them. Details: [perf](../perf/f2g.md#e3-lag-summary). |
| A10 | Camera changes without player interaction do not change missed shifts, completed meals or revenue, including detail transitions and save/reload | E1 | `routines.missedActivities`, `meals.bought` and `revenue.ownUnits` deltas are 0, and the save bytes are equal, in every focus, cap, reload, hidden-travel and combined arm (76 of 76). |

Rig checks 1–11 below add the native side; they are batched and do not gate merge.

## Commands

- Harness, 9 November 2026: as above, exit 0. 360 s wall time in total.
- Harness re-run on 10 November 2026 after merging `b85731d`: exit 0, 9/9 self-checks, 19/19 E2 rows, 52/52 arms equal. The JSON differed from the committed `baseline.json` only in wall times, allocation counts and hold timings, 343 s wall time in total.
- Harness with the pairwise family, 10 November 2026: exit 0, 9/9 self-checks, 19/19 E2 rows, 76/76 arms equal, 696 s wall time in total (other worktrees' dotnet jobs were running). This output is the committed `baseline.json`.
- `python tools/build.py --game-managed "…\Big Ambitions_Data\Managed"`: succeeded.
- `dotnet test tests/PersistentCity.Tests.csproj` (10 November 2026, with the pairwise family): 4,076 passed, 0 failed, 0 skipped.
- Current-writer golden: writer 36 unchanged (`HeldPolicyExecutionTests.TheHeldPolicyWriterGoldenIsPinned` passes; no Core/Persistence edit).

Offline success is not native proof. The harness does not run Unity, NavMesh, native bodies, Gley or RLO.

## In-game checks for the next batched rig

All checks run on the owned rig in named disposable FT cities of the current writer (36), with Persistent City and RLO and the build of the merged F2 slices staged through RIG-MODSET. They are the plan's section 7 checks; F2c–F2e, F2h and F5i already list them per slice, and F2g batches the full set. Fixture: at least one owned food shop with a signalled junction and a door within 120 m of a reachable kerb point. Autosave is recorded, turned off in the isolated profile, checked off before each window (also after reload) and restored at cleanup. Every equality check without intended contact places the player off walking lines and asserts that `pc movement holds since <start seq>` is empty at the end of each arm. **Locked-PC**: runs entirely through FT console/teleport/time and RIG tooling with the PC locked. **Input-only**: needs synthesized game input. None is left to Peter, a playtest or a by-hand check.

1. **Body caps do not change outcomes. Locked-PC.**
   - Setup: checkpoint at 07:00 on a weekday; player at a kerb point beside the shop door, inside 120 m.
   - Steps: run arms `bodies-min` (1/1), `default` (48/24) and `max` (256/64) from the checkpoint to 22:00 at speed 6; extract the PC save.
   - Expected: hold stream empty in each arm; `pcsave_compare` equal on `ids`, `money`, `receipts`, `journeys`, `queues`, `meals_campaigns`.
   - Capability: existing RIG-CADENCE `arms`, RIG-PCCONFIG `MaximumPedestrians`/`MaximumVehicles`, RIG-PCSAVE `pcsave_compare`, FT `time`/`console`. Pending: RIG-LEDGER comparison.
2. **Bodies off equals bodies on, visitors included. Locked-PC.**
   - Setup: check 1's checkpoint, in a city with portals and visitors.
   - Steps: arms `PhysicalEnabled = false` and `default` to 22:00 at speed 6; extract.
   - Expected: hold stream empty; identical projections, including every `pv:` person's receipts, wallet and journeys; visitor bodies acquired > 0 in the default arm.
   - Capability: existing RIG-CADENCE `arms`, RIG-PCSAVE. Pending: RIG-PCCONFIG `PhysicalEnabled`, a visitor fixture (RIG-PCFIX).
3. **Player paths do not change outcomes. Locked-PC.**
   - Setup: check 1's checkpoint.
   - Steps: arms *stationary* (player at the shop), *sweep* (FT `teleport` across six kerb points 200 m apart, 10 game minutes each, repeated) and *away/return* (1 km away for 6 game hours, then back), to 22:00.
   - Expected: hold stream empty in each arm; equal projections across the three arms.
   - Capability: existing FT `teleport`/`console`, RIG-PCSAVE. Pending: RIG-ARMS-STEPS.
4. **Detail transitions and visual lag. Locked-PC.**
   - Setup: a resident about to walk to a parked car and drive to work (`pc movement person <id>`); record the logical arrival minute and `last_sequence` from `pc movement status`.
   - Steps: teleport beside them, `camera follow` with `capture` every 30 s, teleport away mid-route, return before arrival; read `pc movement corrections since <last_sequence>` and `pc movement status`.
   - Expected: arrival minute unchanged; walker lag p95 and car lag p95 recorded against the E3 targets (2 m / 8 m); `gap=false`; no `kind=Move on_screen=true` entry; every entry has before and after poses. Captures are supporting evidence only.
   - Capability: existing FT `teleport`/`console`/`time`, RIG-VIEW `camera follow`/`capture`.
5. **Acquisition failures. Locked-PC.**
   - Setup: check 1's checkpoint.
   - Steps: arms `MovementFaultAcquirePercent` 0 and 50 to 22:00; extract.
   - Expected: hold stream empty; identical projections; failure counter > 0 in the 50 arm.
   - Capability: existing RIG-CADENCE `arms`, RIG-PCSAVE. Pending: RIG-PCCONFIG `MovementFaultAcquirePercent`.
6. **Fast-forward and frame cadence. Locked-PC.**
   - Setup: a checkpoint with bodies visible near the shop.
   - Steps: arms at frame caps 30/60/120; each runs 4 game hours at speed 6, a native `skiptime 120m`, then 4 more hours; extract.
   - Expected: hold stream empty; identical projections across caps; `pc movement status` shows zero followers during the skip.
   - Capability: existing RIG-CADENCE `frame_caps`, RIG-WEEKS `time_machine`, RIG-PCSAVE.
7. **Reload with visible movers, visitors included. Locked-PC.**
   - Setup: checkpoint state `physical_lease_visible` (at least one car, one walker and one visitor body mid-leg).
   - Steps: save; continue 2 game hours and extract; load and run the same 2 hours; repeat from the checkpoint with `PhysicalEnabled = false`.
   - Expected: hold stream empty; all three extractions equal; the saved `journeys` projection equals the logical pose; no `PhysicalControl` true in the save.
   - Capability: existing RIG-CHECKPOINTS `physical_lease_visible`, FT `save`/`load`, RIG-PCSAVE. Pending: RIG-PCCONFIG `PhysicalEnabled`, RIG-PCFIX for the visitor body.
8. **Player blocks a walker; in-game E2 replay. Locked-PC.**
   - Setup: checkpoint A before a walker's next leg point (`pc movement person <id>`); read `city_sequence` from `pc movement holds since 0`.
   - Steps: teleport onto the leg point 5 s before the walker; stand 2 game minutes; checkpoint B 10 game minutes later. Extract the Player.log hold lines between A and B. Load A with `PhysicalEnabled = false`, run `pc movement replay-holds <file>`, run to B's minute. Repeat with one middle line removed and with the last line removed.
   - Expected: contiguous `player-foot` holds totalling ≤ 1 game minute for the journey; `journeys`, `money`, `receipts` at B equal B's; both edited files refused before any hold is applied.
   - Capability: existing RIG-CHECKPOINTS, FT `teleport`/`console`/`load`/`save`, `log_check`, RIG-PCSAVE. Pending: RIG-PCCONFIG `PhysicalEnabled`, writing the extracted lines to a file the game can read.
9. **Player car blocks a resident car. Input-only.**
   - Setup: a visible resident car on a signalled link; checkpoint A.
   - Steps: place the player's car stopped in the lane ahead for 2 game minutes, release; checkpoint B; replay as in check 8.
   - Expected: `player-vehicle` holds, ≤ 1 game minute per journey; followers stay in FIFO order behind the held car; replay equality.
   - Capability: pending RIG-DRIVE (place and hold the player's owned car at a lane pose, release, read the pose back), plus check 8's capabilities.
10. **Performance and crossing waits. Locked-PC.**
    - Setup: a clean RIG-PERF-UNION window at default caps, autosave verified off.
    - Steps: run check 3's sweep along a dense street and a quiet district for 30 game minutes, reading `pc movement status` each minute.
    - Expected: mod-work p95 < 5 ms; every ≥ 30 ms mod-work sample listed with its component; whole-frame metrics reported separately; walker and car lag per minute and the walker lag share from native crossing waits recorded (plan Decision 6).
    - Capability: existing RIG-PERF-UNION `perf_window`, FT `teleport`/`console`.
11. **Native objects near the player do not change stops or routes. Locked-PC.**
    - Setup: checkpoint at 10:00 with a park, a street-performer spot and a subway station in the fixture district.
    - Steps: arms *stationary* (player 1 km away) and *sweep/away-return* (performer spot, park and station for 15 game minutes each, then away) to 18:00; read `pc population census` and the optional-stop counters.
    - Expected: hold stream empty; equal `journeys` and `meals_campaigns`, including saved optional stops and cancellation flags; same stop counts and station set.
    - Capability: existing FT `teleport`/`console`, RIG-PCSAVE. Pending: RIG-ARMS-STEPS.
