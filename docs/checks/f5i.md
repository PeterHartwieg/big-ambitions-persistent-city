Status: implemented (offline); not verified by rig

# F5i: visitor bodies after F2, 9 November 2026

Refs [#92](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/92), [#89](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/89). Branch `codex/pc-f5i-visitor-bodies` from `codex/persistent-city` at `b8feb06` (F2e #172 merged, writer 35). Scope: the F5i row of the [F5 plan](../plans/f5-population.md#4-slice-breakdown) and B5, with the movement contract of the [F2 plan](../plans/f2-movement.md#equivalence-contract). F2c, F2d and F2e are merged, so a body follows the logical pose and writes nothing. Physical progress follows logical permissions.

## What changed

- **`Core/PhysicalJourneys.cs`**: `ActiveJourneyIndex.VisitorsReceiveBodies = true`. Visitors are filed in `BodyCandidates` and in the F2f presentation grid like residents. `WithheldVisitors` stays 0. Membership (`Member`), caps and the save shape are unchanged.
- **`Runtime/PhysicalPopulation.cs`**:
  - The visitor filter is removed from candidate selection, `PlanQueues` and vendor-waiter tracking. Selection reads `ActiveJourneys.Grid.Query` as before. The filters, the four-attempt budget, `SelectionCandidate.ByPriority` and the caps (48 pedestrians, 24 cars, 120 m) are unchanged.
  - Vendor waiters at construction, the attempt-time pruning and the 30 s `StatefulCandidatesIndexed` shadow check now cover residents and visitor routines (`PersonTable.Schedules`). Without that, visitor attempt times would be pruned as unknown IDs, and a visitor missing from the index would go unnoticed.
  - After a shadow mismatch, the full-scan fallback still scans `State.Citizens`, the residents. Visitors get bodies through the index only, as the plan row says. A session in fallback shows no visitor bodies. That is presentation only.
  - `PickWalker` skips visitor bodies. `CitySimulator.Inspect` returns nothing for a visitor, so a click on a visitor would have opened the resident popup with "This resident has left the city." The visitor inspector view in F5 plan question 5 ("visiting from <station>") is not in the F5i row and is left for a later slice. Until then a visitor body is street presence and is not pickable.
- **Unchanged:** player holds. `CitySimulator.HoldForPlayer` refuses visitors (`PlayerHoldRefusal.Visitor`, F2e, `Core/PlayerHolds.cs`). `ObservePlayerContact` does not track or report visitor bodies (review Opus-r1-1), so no refused report is counted. A player standing in a visitor's way delays only the body: it lags and is corrected like any lagging body, and the visitor's logical journey is not delayed. Holding visitors would mean saving hold minutes on visitor routines, a schema change outside this slice.
- **`tools/movement-equivalence`**: additive visitor arms. `MovementOptions.Visitors` adds three subway portals a few blocks from the own shop, so attaching the food market creates K−1 walking visitors per resident (`RepresentationFactor`, default 2). The visitor arms are:
  - bodies-off `visitors/plain-60`, `plain-30` and `plain-hidden-travel`;
  - body arms `stationary`, `sweep`, `away-return`, `caps-1`, `caps-256`, `lag-ideal`, `failures-50pct`, `cadence-30fps`, `reload-mid-journey` and `hidden-travel`.

  New `visitors` projection per 03:00 boundary: visitor journeys with commit and arrival minutes and place, visitor sales, and per-visitor wallet balance, food spending and purchases. New `visitors.*` and `bodies.visitor*` metrics. The ledger counts visitor journeys. A visitor-arm reload waits until a visitor body is bound. An `AfterFrame` hook lets tests check invariants inside the frame loop. Resident-only arms are unchanged: the `visitors` projection hashes empty input there.
- **`tools/physical-selection-regression/run.py`**: the F2f and F5f baseline reconstructions follow the new roster comment and the removed fallback filter. The rebuilt pre-F5f loop text is the same as before.
- **Existing tests updated to F5i:** `ActiveJourneyIndexTests` (`VisitorsAreIndexedAsBodyCandidates`, the shared index assertion), `PresentationGridTests` (a waiting visitor is filed in the grid), `F5gPopulationFillTests`, `Schema27Tests` (`VisitorsOwnNoCarOrTrafficRecordAndWalkAsBodies`) and `VendorBodyTests`. Each one asserted the pre-F5i switch.
- No schema, save-shape, writer, `Persistence/` or `Core/Model.cs` change.

## Offline evidence (no game, rig, ModsLocal or bridge)

`tests/VisitorBodyTests.cs` uses F2's harness, 300 residents plus 300 visitors over one day (the smallest fixture here in which visitors buy at the own shop):

| Test | What it proves |
| --- | --- |
| `VisitorsAreBodyCandidatesThroughTheActiveJourneyIndex` | Every 600 frames, through a mid-journey reload: index = full scan, `WithheldVisitors = 0`, every travelling visitor routine is a body candidate filed in the grid, and visitor bodies are bound. Visitor appearance seeds are non-zero, which pedestrian acquisition requires. |
| `VisitorBodyArmsEqualBodiesOff` | Bodies on vs off, E1: stationary, sweep, away/return, caps 1/1 and 256/64, ideal lag, 50 % acquisition failures, hidden travel and 30 fps each equal their bodies-off run on every projection: save bytes, sales, journeys, parked cars, wallets, FIFO and visitors. The visitor arrivals, purchases, own sales, wallet units and food-spending units are also checked one by one. The reference run has visitor arrivals, purchases and payments, and the stationary arm has visitor bodies. |
| `CapsAreUnchangedWithVisitorsEligible` | Runtime defaults are 48/24/120 m, and so are the double's. Visitor bodies share the pedestrian cap and add no car. The 1/1 arm holds at most one body. At the declared load (957 residents + 1,914 visitors = 2,871 persons) bodies stay ≤ 48/24 with visitors among them. |
| `VisitorBodyContinuesAcrossReload` | A save and reload while a visitor body is bound: the journey continues from its saved state, the new driver gives that visitor a body again, and the run equals the uninterrupted bodies-off run. The bodies-off reload equals it as well. |
| `SelectionOrderIsStillByPriorityWithoutAKindFilter` | Source check. Selection and `PlanQueues` contain no `ReceivesBody`, `IsVisitor`, `KindOf` or `PersonKind`. There is one `candidates.Sort(SelectionCandidate.ByPriority)`, and the comparer keeps least recently attempted, nearest, ordinal ID and roster order with no kind term. The pick skips visitors. |
| `ResidentsAreUnchanged` | A resident-only run gives the same journeys hash, sales hash, bodies acquired (850), walker frames (1,438,800) and car frames (174,675) as at `b8feb06`, pinned from the base before the flip. Every indexed resident is a body candidate. |
| `CurrentWriterGoldenHashIsUnchanged` | Writer 35 and F2e's two pinned golden hashes (seeds 5150 and 9217). |

F2 harness at the declared size, 957 residents + 957 visitors over two days (`dotnet run --project tools/movement-equivalence -c Release -- --arms visitors/ --no-self-checks`). Every visitor body arm is EQUAL to its bodies-off reference on all seven projections:

| Arm | Bodies acquired (visitors) | Max walkers / cars / visitor bodies |
| --- | --- | --- |
| stationary | 7,124 (2,167) | 48 / 24 / 47 |
| sweep | 5,394 (889) | 48 / 24 / 45 |
| away-return | 6,335 (2,105) | 48 / 24 / 46 |
| caps-1 | 622 (63) | 1 / 1 / 1 |
| caps-256 | 17,199 (5,720) | 256 / 64 / 214 |
| lag-ideal | 7,195 (2,204) | 48 / 24 / 48 |
| failures-50pct | 5,417 (1,477) | 48 / 24 / 48 |
| cadence-30fps | 7,143 (2,175) | 48 / 24 / 47 |
| reload-mid-journey | 7,239 (2,189); one visitor bound at the reload, given a body again | 48 / 24 / 47 |
| hidden-travel | 7,045 (2,155) | 48 / 24 / 47 |

In every arm, bodies-off included, visitors made 5,788 arrivals, 41 purchases and 7,301,444,403,200 food-spending units. Visitors buy rarely in this fixture: most visitor meals end as "chose household food" under the unchanged food model. The equality covers each one.

Commands, 9 November 2026: `python tools/build.py --game-managed …` succeeded (DLL SHA-256 `c8c68f82…`). `dotnet test tests/PersistentCity.Tests.csproj` passed 4,297 of 4,297, with 0 failed and 0 skipped. Performance: [docs/perf/f5i.md](../perf/f5i.md).

Offline success is not native proof. The harness does not run Unity, NavMesh, native bodies or RLO.

## In-game checks for the next batched rig

All checks run in the owned headless rig on named disposable FT cities of the current writer with the F5i build staged through RIG-MODSET. The fixture is a fresh city with portals and the F5g fill, so it has commuters and day visitors. RIG-LIVE is an infrastructure prerequisite, not evidence. The rig records autosave, turns it off in the isolated profile, checks it is off before each measured window (also after reload) and restores it at cleanup. Explicit saves stay outside measured windows. The "physical line" is the 60 s PC diagnostics line (`pedestrians=`, `cars=`, `journeyIndex=`, `journeyIndexWithheldVisitors=`, `journeyIndexMismatches=`, `journeyIndexFallback=`). No check is left to Peter, a playtest or a by-hand look.

1. **Visitors among bodies, caps unchanged (plan R5, after F5i). Locked-PC.**
   - Setup: the visitor fixture at 07:00 on a weekday.
   - Steps: run ordinary speed through the morning commute for 15 minutes. Every 60 s, move the camera with `camera look_at` between a street near a subway portal and a quiet district. Sample `screen_pos list` each move and read every physical line.
   - Expected: on every line `pedestrians` ≤ 48, `cars` ≤ 24, `journeyIndexWithheldVisitors=0`, `journeyIndexMismatches=0` and `journeyIndexFallback=False`. At least one body in `screen_pos list` has a `pv:` ID. `pc population person <that id>` reports `kind=visitor`, and no visitor ID appears among car bodies.
   - Rig-automated. Existing capability: RIG-VIEW `camera`/`screen_pos`/`capture`, FT `time`/`console`, `rig flow run` log checks. Pending: a visitor-bearing PC fixture (RIG-PCFIX), body-ID readback in `screen_pos list`, RIG-LIVE.
2. **Bodies off equals bodies on for visitors (F2 check 2 with visitors). Locked-PC.**
   - Setup: a 07:00 checkpoint of the visitor fixture. The player stands outdoors at a kerb point by the own food shop, off walking lines, inside 120 m.
   - Steps: from the checkpoint, run arms `PhysicalEnabled = false` and default to 22:00 at speed 6, then collect the PC save extraction.
   - Expected: `pc movement holds since <start seq>` is empty in each arm. `pcsave_compare` is equal on `ids`, `money`, `receipts`, `journeys`, `queues` and `meals_campaigns`, including every `pv:` person's receipts, wallet and journeys. Bodies acquired for visitors are > 0 in the default arm.
   - Rig-automated. Existing capability: RIG-CADENCE `arms`, RIG-PCSAVE, FT `time`/`console`. Pending: RIG-PCCONFIG `PhysicalEnabled`, the visitor fixture (RIG-PCFIX), RIG-LIVE.
3. **Body caps do not change visitor outcomes (F2 check 1 with visitors). Locked-PC.**
   - Setup: check 2's checkpoint and player spot.
   - Steps: run arms `bodies-min` (1/1), default (48/24) and `max` (256/64) to 22:00 at speed 6, then extract.
   - Expected: the hold stream is empty in each arm, and the projections are equal across arms, visitor persons included.
   - Rig-automated. Existing capability: RIG-CADENCE `arms` with RIG-PCCONFIG `MaximumPedestrians`/`MaximumVehicles`, RIG-PCSAVE. Pending: the visitor fixture (RIG-PCFIX), RIG-LIVE.
4. **Reload with a visitor body visible (F2 check 7 with visitors). Locked-PC.**
   - Setup: the visitor fixture during the commute.
   - Steps: find a walking `pv:` person with `pc movement person`, move the camera to them with `camera look_at` and confirm their body in `screen_pos list`. Save explicitly, outside any window. Continue 2 game hours and extract. Load the checkpoint and run the same 2 hours, then extract.
   - Expected: both extractions are equal. The visitor's saved `journeys` entry equals the logical pose, with no body offset. Within 60 s after the load the visitor again has a body while inside the radius, and the physical line shows `journeyIndexMismatches=0`.
   - Rig-automated. Existing capability: FT `save`/`load`/`console`, RIG-VIEW `camera`/`screen_pos`, RIG-PCSAVE. Pending: RIG-CHECKPOINTS on the visitor fixture, RIG-LIVE.
5. **Performance with visitors eligible (plan R8 subset; F2 check 10). Locked-PC.**
   - Setup: the visitor fixture at the declared load (1,914 persons, then 2,871 with K = 3) and the pre-F5i build as baseline.
   - Steps: interleave windows of both builds: idle and morning commute, stationary camera and a 30-minute sweep along a dense street and a quiet district, Ctrl+F8 panel closed. Autosave stays off and is checked before each window. No save happens inside a window.
   - Expected: mod-work p95 < 5 ms in every window. Every mod-work sample ≥ 30 ms is listed with its component. The physical-population component p95 of the F5i build is reported against the pre-F5i build (offline expectation: up to about 3× the selection cost, [perf](../perf/f5i.md)). Whole-frame metrics are reported separately and never used as mod-work; #128 stays a separate investigation.
   - Rig-automated. Existing capability: RIG-PERF-UNION `perf_window`, `rig flow run` autosave guard, RIG-MODSET, RIG-VIEW `camera`. Pending: RIG-PCFIX (visitor fixture at both sizes), RIG-LIVE.
6. **A visitor body is not pickable; residents still are. Input-only.**
   - Setup: the visitor fixture during the commute, normal city view.
   - Steps: find a visitor body and a resident body on screen with `screen_pos list`. With the virtual mouse, hover and left-click the visitor body, then the resident body.
   - Expected: the visitor gives no hover highlight and no popup. No popup reads "This resident has left the city." The resident opens the popup with their name.
   - Rig-automated. Existing capability: virtual mouse, `ui geometry`, FT `shoot`. Pending: body-ID readback in `screen_pos list`, the visitor fixture (RIG-PCFIX), RIG-LIVE.

Native evidence from these checks is batched before release and is not a merge gate (merge rule v2).
