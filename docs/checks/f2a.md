Status: implemented (offline); not verified by rig

# City audit F2a — movement-equivalence harness and baseline, 6 November 2026

Refs [#89](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/89). Branch `codex/pc-f2a-movement-harness` from `origin/codex/persistent-city` at `f46b3dd` (writer **25**). Scope: the F2a row of [the F2 plan](../plans/f2-movement.md#4-slice-breakdown), with section 3 (authority table, equivalence contract E1–E3, Decision 2) and section 6 as its spec. Peter's accepted section 9 defaults ([6 Nov decisions](../decisions/2026-11-06-design-defaults.md)) do not change F2a: it measures today's code and adds no behaviour.

F2a adds a tool and tests only. No Core, Runtime, Persistence or save change; writer 25 and the pinned golden hash are untouched. Offline Core results are not native evidence.

Re-read at this base before use: the lease API in `Core/PhysicalJourneys.cs` (`TryAcquirePhysicalJourney`, `ObservePhysicalJourney`, `ReleasePhysicalJourney`, `TrySamplePhysicalJourney`, `IsPhysicalJourneyValid`, `ActiveJourneyIndex`), its Runtime caller `Runtime/PhysicalPopulation.cs` (`Update`, selection, `Observe`, `TryAwaitArrival`, `ObserveForSave`, `Release`, `MotionPermission`/`CommandLimit`), the frame order in `Runtime/PersistentCityMod.cs` (`AdvancePopulation`, then `physical.Update`), `Core/AccountingClock.cs` and the F1/F6a fixtures (`tools/accounting-backlog-regression/AccountingScenario.cs`, `tools/economy-interventions/EconomyFixture.cs`).

## What is implemented

`tools/movement-equivalence/` (usage in `Program.cs` and `README.md`; full output in [`baseline.json`](../../tools/movement-equivalence/baseline.json)):

- **Fixture (`MovementFixture.cs`).** The F1/F6a shape: 957 residents, representation factor 2 (1,914 consumers), every resident `Persona.Foodie` age 30, the current food model attached unchanged (weighted choice, awareness, household budgets), one own food shop with three products (burger 800, croissant 300, hot dog 500 cents; 07:00–22:00; 60 meals/h). Placed on a signalled road grid: 25 × 25 junctions 12 m apart, so every directed link stores two cars; signals at every fourth junction (2-minute cycle, half green, alternating offset). 120 homes, ten workplaces, four leisure places and the shop at the centre, at seeded junctions with doors 3 m off the street. Every second resident owns a car parked at the home corner and drives trips of at least 96 m along the grid (x first, then z, one section per leg); everyone else walks the same streets. The native checkout double enforces the F1 hour fence and one order per intent and keeps the ordered sale list.
- **Clock.** 1/3 game minute per real second (GameManager.MinutesMultiplier 1 times the default clock factor 1/3), walkers 2 m/s (`NativeTravelPace.WalkingMetresPerSecond`), cars 36 km/h. Route minutes, the traffic network and the bodies all use this rate, so a 12 m car section takes 1.2 real seconds and a walking section 6 s.
- **Presentation driver double (`PresentationDriver.cs`).** Calls today's public lease API as `PhysicalPopulation` does for travelling persons: selection every 0.25 real s over `ActiveJourneys.BodyCandidates` with the same filters (event in the future, not bound, 2 s retry, travelling with a journey, sampled position within 120 m) and the same `ByPriority` order; at most four attempts per tick; traffic admission (`TryGetTrafficPermission` with a link) before a car body; `TryAcquirePhysicalJourney`, then body acquisition, whose failure releases the lease; per-frame `Observe` with the 16-corner loop, `PhysicalProgress.TryProject`/`AdvanceWait`, Gley reached evidence before a car crosses a road node, the permission clamp with `PhysicalTrafficCornerPermission`, `PhysicalCornerHandoff`, `PhysicalTrafficPrecision` and the overshoot release; `CommandLimit` (`PhysicalTrafficMotion.MaximumAdvance`, hold at a reached corner); the door-arrival hold (`TryAwaitArrival`, half a game minute); release beyond 1.4 × the radius; release on hidden travel; `ObserveForSave` before every save. A body is a position that moves toward its native target at the lag model's pace; native movement runs before the simulation advances, `Observe` after it.
  - Not modelled, because they take no lease or are F2b's write paths: door queues and `QueueAtOpening`, optional stops, their departure hold and `CancelOptionalStop`, outdoor scenes, ambient/RLO reservations, the interior view, NavMesh path failures.
- **Run (`MovementRun.cs`).** Frame by frame, in Runtime order: native clock step under the F1 fence (`AccountingClock.Limit`), `BeginAccountingObservations`, native body movement, budgeted `AdvanceFrameSlice`, `SynchronizeSnapshot`, then the driver's `Update`. The saved city is hashed at every 03:00 boundary at the moment the native clock enters it (every earlier event drained, none at or after it run, bodies observed for save first); the run asserts `State.Minute` equals the boundary. Link order is sampled at every 5-minute epoch entry. A journey ledger from `ActivityObserved` records each journey's commit and arrival minute and mode.
- **Arms (`MovementArms.cs`).** Each body arm varies one factor from the default (player at the shop, caps 48/24, Native lag, no failures, 60 fps) and is compared with the plain bodies-off simulation at the same frame rate:
  - focus paths: none, stationary (shop), sweep (six street points about 120 m apart, 10 game minutes each, repeated), away/return (about 1.6 km away 09:00–15:00 daily);
  - caps 0/0, 1/1, 24/12, 48/24, 256/64 walkers/cars;
  - native lag: Ideal (nominal pace, no blocking), Native (seeded walker pace 0.8–1.0×, blocking 3 s of every 20 s), SlowHeadCar (Native plus every fourth car body at 0.5×);
  - acquisition failure 0/10/50 %; cadence 30/60/120 fps;
  - mid-journey reload (first frame after 07:30 with at least one walker and one car body: observe for save, save, reload into a new simulator and a new driver, as Runtime does on load);
  - hidden travel (12:00–12:30 on both days, bodies released, native time at 10×).
  - Bodies-off references: plain at 30/60/120 fps, driver with no focus, plain with the reload, plain with hidden travel.
- **Metrics (`persistentcity-f2a-movement-1`).** Per mode (walk-only, any car leg): journey count, mean, p50, p95 and max minutes; arrivals, shop arrivals, admitted at the shop, work arrivals; missed activities; meals bought, own sales, checkout refusals, skipped meals; own revenue and summed wallets (exact units); FIFO samples and queued-link samples; cars parked away from home; body counters (acquired, released, failed acquisitions, blocked samples, overshoots, arrival holds, lease observations, walker/car frames, maxima, hidden-travel frames and releases). Per 03:00 boundary: SHA-256 of the save, the ordered sale list, the journey ledger, parked cars, wallets and the FIFO samples. `Equivalence.Compare` reports, per projection, whether every boundary matches and the first boundary that differs, plus outcome deltas.
- **Self-checks (`SelfChecks.cs`, run first, 96 residents × 1 day).** Same seed byte-identical without and with bodies; another seed differs; the no-focus driver and the zero-cap driver equal the plain simulation; a bodies-off mid-journey reload resumes byte-identical; a body reload resumes; injected faults fail loudly: **duplicate arrival** (a second `CompletedJourneys` for an arrived journey) is caught by the journey ledger, **skipped event** (the native clock moves past an undrained epoch) by the F1 epoch publication check.

Technical decisions taken here: the 1/3 clock rate and the 12 m grid (two-car links, as the row asks), position-based bodies with a 1e-6 m reached tolerance for the Gley marker, a car body spawning at its journey sample and a walker at its selected position (Runtime's road sample and candidate position), sampling at epoch entry so sample minutes do not depend on cadence, and one seed for the baseline (the self-checks show seed sensitivity).

Two double defects found during development and fixed before the baseline: a car body landing on its waypoint by interpolation never raised the reached marker, and car bodies spawned at `PositionOf` instead of the journey sample. Both gridlocked the grid around the shop.

## Offline tests and checks

- `python tools/build.py --game-managed "…\Big Ambitions_Data\Managed"`: passed.
- `dotnet test tests/PersistentCity.Tests.csproj`: **3,188 passed**, 0 failed (9 new in `tests/MovementEquivalenceTests.cs`, which link the harness sources).
- New tests: same seed byte-identical with and without bodies, seed sensitivity; focus-none, zero caps, 30 fps and hidden travel without bodies equal plain (small fixture); both faults fail with their named check; bodies-off reload equals plain with journeys in flight, body reload resumes with bodies bound; boundaries are exactly 03:00 and the grid queues cars; the double takes leases for walkers and cars within caps, records failed acquisitions and hidden-travel frames; **today's body arm differs from bodies-off** (expected red; F2c/F2d flip this to equality); the arm catalogue covers every factor of the row.
- `dotnet run --project tools/movement-equivalence -c Release -- --out tools/movement-equivalence/baseline.json`: all nine self-checks PASS, 21 arms, 199 s. Two runs gave identical output.

## Baseline (957 residents, seed 0, 1,440 → 4,500: two days plus the 03:00 sample)

Plain bodies-off reference: 9,996 journeys (6,208 walking, mean 26.23 / p95 55.41 min; 3,788 with a car leg, mean 30.67 / p95 133.31 min), 2,213 shop arrivals, 691 missed activities, 645 own sales, $3,665.00 revenue, 6,329 queued-link samples in 612 FIFO samples.

| Arm | Equal to bodies-off | Walk mean / p95 | Car mean / p95 | Arrivals | Missed | Own sales | Revenue $ | Bodies | Tool s |
| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| bodies-off/plain-60 | reference | 26.23 / 55.41 | 30.67 / 133.31 | 9,996 | 691 | 645 | 3,665 | 0 | 3.0 |
| bodies-off/plain-30 | fifo, parked, wallets | 26.23 / 55.41 | 30.67 / 133.31 | 9,996 | 691 | 645 | 3,665 | 0 | 1.9 |
| bodies-off/plain-120 | fifo, parked, wallets | 26.23 / 55.41 | 30.67 / 133.31 | 9,996 | 691 | 645 | 3,665 | 0 | 5.2 |
| bodies-off/focus-none | all | same | same | 9,996 | 691 | 645 | 3,665 | 0 | 2.9 |
| bodies-off/plain-reload | all | same | same | 9,996 | 691 | 645 | 3,665 | 0 | 4.6 |
| bodies-off/plain-hidden-travel | all | same | same | 9,996 | 691 | 645 | 3,665 | 0 | 2.9 |
| caps/0-0 | all | same | same | 9,996 | 691 | 645 | 3,665 | 0 | 3.1 |
| focus/stationary (default) | parked | 27.64 / 59.41 | 30.68 / 133.73 | 9,983 | 708 | 625 | 3,606 | 3,934 | 12.4 |
| focus/sweep | parked | 27.13 / 58.66 | 30.70 / 133.31 | 10,038 | 701 | 660 | 3,751 | 4,849 | 10.3 |
| focus/away-return | parked | 27.27 / 59.41 | 30.68 / 133.79 | 9,961 | 711 | 642 | 3,672 | 3,573 | 12.0 |
| caps/1-1 | parked | 26.29 / 55.41 | 30.67 / 133.31 | 9,992 | 691 | 641 | 3,649 | 494 | 3.9 |
| caps/24-12 | parked | 27.15 / 57.41 | 30.66 / 134.31 | 9,989 | 698 | 623 | 3,559 | 2,746 | 8.6 |
| caps/256-64 | parked | 29.33 / 62.77 | 30.76 / 134.29 | 9,964 | 728 | 617 | 3,521 | 11,918 | 34.5 |
| lag/ideal | parked | 25.91 / 55.41 | 30.66 / 133.71 | 9,981 | 692 | 653 | 3,707 | 4,496 | 11.3 |
| lag/slow-head-car | none | 27.59 / 59.41 | 31.02 / 134.69 | 10,008 | 720 | 632 | 3,647 | 3,908 | 9.6 |
| failures/10pct | parked | 27.58 / 59.41 | 30.67 / 133.72 | 9,981 | 710 | 630 | 3,640 | 3,938 | 9.6 |
| failures/50pct | parked | 27.52 / 59.41 | 30.69 / 133.81 | 10,023 | 711 | 648 | 3,737 | 3,696 | 10.4 |
| cadence/30fps (vs plain-30) | parked | 27.42 / 59.01 | 30.71 / 133.74 | 9,997 | 699 | 626 | 3,597 | 4,020 | 5.8 |
| cadence/120fps (vs plain-120) | parked | 27.74 / 59.41 | 30.67 / 133.96 | 9,993 | 707 | 643 | 3,687 | 3,855 | 17.1 |
| transitions/reload-mid-journey | parked | 27.55 / 59.41 | 30.68 / 133.80 | 10,019 | 707 | 638 | 3,678 | 3,958 | 9.7 |
| transitions/hidden-travel (vs plain-hidden) | parked | 27.52 / 59.41 | 30.68 / 134.31 | 10,007 | 710 | 649 | 3,741 | 3,919 | 9.5 |

"Equal to bodies-off" lists the projections (save, sales, journeys, parked, wallets, fifo) equal at both boundaries. The 03:00 boundary of day 1 (minute 1,620) matches in every arm; every difference first appears at 3,060 (03:00 on day 2).

What today's code shows (E1 red, as the plan expects):

- **Every arm with bodies diverges from bodies-off**: save bytes, ordered sales, journeys, wallets and FIFO order differ at the first boundary after bodies appear. Only cars parked at 03:00 stay equal, except under SlowHeadCar.
- **Caps change outcomes monotonically**: from 1/1 to 256/64 the walking mean rises from 26.29 to 29.33 min, missed activities from 691 to 728 and own sales fall from 641 to 617 (revenue −3.9 % at 256/64).
- **Where the player stands changes revenue**: stationary at the shop $3,606, sweeping $3,751, away/return $3,672, bodies off $3,665.
- **Ideal bodies still diverge**: with nominal pace and no blocking, the walking mean falls to 25.91 min and sales rise to 653. The lease freeze, one corner per frame and the half-minute door hold alone change who arrives when.
- **A slow visible head car holds real road storage**: SlowHeadCar is the only arm whose parked cars also differ at the end.
- **Acquisition failures still change outcomes**: each failed body takes and releases a lease (415 failures at 10 %, 3,706 at 50 %).
- **Reload with 16 bodies visible** resumes (31 journeys in flight) and diverges like the default arm. **Hidden travel** released 27 bodies at its window starts.
- No traffic overshoot release in any arm.

**Finding outside F2's body path: bodies-off runs depend on frame cadence.** Plain at 30 and 120 fps differs from 60 fps in save bytes, journey records and sale timestamps; counts, money, wallets, parked cars and FIFO order are equal. With `--days 1 --dump`, 6 of 4,955 journey records (four outings to the shop and their two returns) and 2 of 309 sale timestamps differ, by at most 0.017 game minutes. Meal outings take their departure minute from `State.Minute` at commit (`Core/OptionalVisits.cs:17`, `:36`), so an outing committed at a frame-dependent simulator minute carries that minute into its route and receipt; which call commits it at a frame-dependent minute is not traced here. The small 96-resident fixture does not show it. E1 holds only per cadence until this is fixed, so F2a compares each cadence arm with bodies-off at the same cadence. A cross-cadence E1 assertion in F2g needs a Core fix first; F2a records it and changes no Core code.

Not covered: one seed only; the double is not native evidence (NavMesh, Gley and avoidance are modelled, not run); E3 lag is F2c/F2d's measurement; the E2 hold arm is F2e's.

## In-game checks for the next batched rig

All on a disposable FT city on the owned rig with Persistent City, RLO and one owned food shop, autosave off and verified before every window (also after reload), using today's build (pre-F2b). They record today's in-game red baseline for the F2 plan's checks; no `pc movement` command exists before F2c, so they compare saves and receipts only.

1. **Body caps change outcomes today (plan check 1, red baseline).** Setup: checkpoint at 07:00 on a weekday, player standing outdoors by the shop door off walking lines. Steps: run arms 1/1, 48/24 and 256/64 from the checkpoint to 22:00 at speed 6; extract the PC save. Expected: `pcsave_compare` on `ids`, `money`, `receipts`, `journeys`, `queues`, `meals_campaigns`; `ids` equal, the rest recorded per arm (differences allowed and recorded as today's baseline). **rig-automated (existing capability: RIG-CADENCE `arms` with RIG-PCCONFIG `MaximumPedestrians`/`MaximumVehicles`, RIG-PCSAVE `pcsave_compare`, FT `time`).**
2. **Bodies off against bodies on (plan check 2, red baseline).** Same as check 1 with arms `PhysicalEnabled = false` and 48/24. Expected: same projections recorded; differences allowed. **rig-automated (pending capability: RIG-PCCONFIG row for the existing PC key `PhysicalEnabled`).**
3. **Player paths change outcomes today (plan check 3, red baseline).** Setup: check 1's checkpoint. Steps: arms stationary at the shop, sweep (FT `teleport` across six kerb points 200 m apart, 10 game minutes each) and away/return (1 km away for 6 game hours, then back), each to 22:00. Expected: projections recorded per arm; differences allowed. **rig-automated (pending capability: RIG-ARMS-STEPS per-arm step lists with `collect`; existing: FT `teleport`, RIG-PCSAVE).**
4. **Bodies-off frame-cadence independence (F2a finding).** Setup: check 1's checkpoint with `PhysicalEnabled = false`. Steps: arms at frame caps 30, 60 and 120, each 07:00 to 22:00 at speed 6. Expected: `ids`, `money` and receipt counts equal across caps; record whether `journeys` and receipt timestamps differ (the offline harness finds sub-0.02-minute differences in shop-outing departures). **rig-automated (existing capability: RIG-CADENCE `frame_caps`, RIG-PCSAVE; pending capability: RIG-PCCONFIG `PhysicalEnabled`).**
5. **Reload with visible movers (plan check 7, today's behaviour).** Steps: reach checkpoint `physical_lease_visible` (at least one car and one walker mid-leg), save, continue 2 game hours and extract; load the checkpoint and run the same 2 hours; extract again. Expected: both loads succeed with no refusal; record whether the two extractions are equal (the harness predicts they differ while leases exist). **rig-automated (existing capability: RIG-CHECKPOINTS `physical_lease_visible`, FT `save`/`load`, RIG-PCSAVE).**
6. **Fast-forward releases bodies (plan check 6, today's behaviour).** Steps: from a checkpoint with bodies visible near the shop, run 1 game hour at speed 6, a native `skiptime 120m`, then 1 hour more, at frame caps 30 and 60. Expected: the PC physical diagnostics line shows `pedestrians=0` and `cars=0` during the skip and bodies again afterwards; projections recorded per cap. **rig-automated (existing capability: RIG-CADENCE `frame_caps`, RIG-WEEKS `time_machine` mode, `rig flow run` log checks, RIG-PCSAVE).**

In-game evidence from these checks records today's behaviour; it does not approve a later F2 slice.
