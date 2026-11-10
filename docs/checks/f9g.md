Status: implemented (offline); not verified by rig

# City acceptance F9g: presentation reads through the view, 9 November 2026

Refs [#96](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/96). Branch `codex/pc-f9g-presentation-view` from `codex/persistent-city` at `649467b` (X1 #187; X2 #184 split `Runtime/PhysicalPopulation.cs` into four partial files; F9e #182 added the read-only views). Synced on 10 November with `codex/persistent-city` at `f3a5210` (X3 #192 holds the save schema at 1; F2g #191; F9a #177); the base tip `5b56bda` (#195) adds docs only. Scope: the F9g row and [Decision 8, *Presentation*](../plans/f9-city-acceptance.md#decision-8-read-only-city-views-for-observers) of the F9 plan. No schema, save shape or `Persistence/` change. No product behaviour change.

Re-read at this base: `Core/CityView.cs` (`IPersonView` and `ICityView` members), `Core/PhysicalJourneys.cs` (`ActiveJourneyIndex`), `Core/PresentationGrid.cs` (`Query`), the four `Runtime/PhysicalPopulation*.cs` partial files, `Runtime/PhysicalVehicles.cs` (`TryAcquire`), `tests/OwnershipBoundaryTests.cs`, and the F2 driver `tools/movement-equivalence/PresentationDriver.cs` (read only).

## What is implemented

- **Candidate query returns views.** `PresentationGrid.Query(Position, double, List<IPersonView>)` returns `IReadOnlyList<IPersonView>`. The grid still files entities; the result is the same objects seen through the interface, so there is no copy or wrapper.
- **`ActiveJourneyIndex.Contains` takes `IPersonView`**, so a grid result can be checked against the index without the entity.
- **Presentation reads go through `simulation.View`**, in all four `PhysicalPopulation` partial files:
  - every logical-minute read (`View.Minute`; 19 reads);
  - the full-scan fallback roster in selection and in `PlanQueues` (`View.ResidentList`, the same list as `State.Citizens`);
  - the vendor-waiter seed at construction and the journey-index shadow check (`View.ResidentList`, then `View.VisitorRoutineList`, which is `PersonTable.Schedules`' order);
  - the retry-table bound and prune (`ResidentList.Count + VisitorCount`, which equals `PersonTable.PersonCount`; ids from both lists);
  - the body preflight's residents.
- **View-typed helpers and state.** `TrackVendorWaiter` and `VendorWaiting` take `IPersonView`. The vendor-waiter table, the grid result buffer (`nearby`), and the person locals in `ObserveQueue` and `AwaitVendorDeparture` are `IPersonView`. The selection loop reads id, next event, phase and place through the view.
- **One named step from view to entity.** `PhysicalPopulation.Entity(IPersonView)` is a cast. It is used only where the code needs a member the view does not have (journey, optional stop, car, appearance identity) or calls a simulator method that takes the entity (`PositionOf`, `QueueAtOpening`).
- **`tests/OwnershipBoundaryTests.cs`:**
  - The blanket `PhysicalPopulation.cs` allowance is removed. The five presentation files (`PhysicalPopulation.cs`, `.Following.cs`, `.Queues.cs`, `.Selection.cs`, `PhysicalVehicles.cs`) have no file allowance. A simulator `.State` read in them is allowed only on a line that holds a named exception.
  - New rule for the presentation files: any entity type in type position (a signature, field, local type, generic argument or cast; the F9e `EntityTypes` list) must be on a named line.
  - The cited `row` receiver moved from `PhysicalPopulation.cs` to `PhysicalPopulation.Queues.cs`, where X2 put it.
  - Three new facts (table below).

### Named exceptions

Each is listed in `OwnershipBoundaryTests.PresentationExceptions` with its reason. The test fails if one no longer occurs. Most come from one gap: `IPersonView` (F9e, `Core/CityView.cs`, outside F9g's Owns) has no committed journey, optional stop, car or appearance identity, and the simulator's samplers (`SampleJourney`, `PositionOf`, `QueueAtOpening`, `TryGetTrafficSignalGreen`, `MultimodalRoutes.TryPhysicalRoadSample`) take `CitizenState`.

| File | Signature or read | Why it still needs mutable state |
| --- | --- | --- |
| `Core/PhysicalJourneys.cs` | `ActiveJourneyIndex.BodyCandidates` stays `IReadOnlyList<CitizenState>` | The F2 movement-equivalence driver (`tools/movement-equivalence/PresentationDriver.cs:198-230`) reads each candidate's `Identity`, `Journey` and passes it to `PositionOf`/`SampleJourney`. That file belongs to F2g, which is in flight; F9g may not edit it, and a view-typed `BodyCandidates` does not compile there. Runtime does not read `BodyCandidates` (`PresentationGridTests.RuntimeReadsTheGridWithTheFullScanFallback`). |
| `PhysicalPopulation.cs` | `Binding.Citizen` (`CitizenState`) | A bound body follows the entity's journey and optional stop and passes it to the samplers. |
| `PhysicalPopulation.cs` | `Entity(IPersonView)` | The one view-to-entity step (a cast). |
| `PhysicalPopulation.cs` | `fadedUntilHidden` values `(CitizenState Citizen, Route? Journey, string? ArrivedAt)` | The fade restriction compares the entity and its journey by reference. |
| `PhysicalPopulation.cs` | `Dictionary<string, Place> places` and `simulation.State.Places.ToDictionary(` | `DwellSpots` and `CitySimulator.QueueAtOpening` take `Place`. |
| `PhysicalPopulation.cs` | `FoodCommerceState? vendorFood` | Vendor meal rows (`ResidentMeal`) are not on `IFoodView`. |
| `PhysicalPopulation.cs` | `simulation.State.SourceIdentities` | The source identity pool for ambient pedestrians is not on the view. |
| `PhysicalPopulation.Following.cs` | `LogicalCarMinutes(CitizenState, Route)` | Reads the journey; `SampleJourney` takes the entity. |
| `PhysicalPopulation.Following.cs` | `contact.Settle(…, simulation.HoldForPlayer(…), simulation.State)` | The F2e player hold, a simulator write; `PlayerContact.Settle` takes the city. |
| `PhysicalPopulation.Queues.cs` | `OnOptionalDeparture(string, CitizenState, double)` | The simulator's `ActivityObserved` event signature. |
| `PhysicalPopulation.Queues.cs` | `OptionalVisible(CitizenState)` | Reads the optional stop. |
| `PhysicalPopulation.Queues.cs` | `TryBodyPlace(string, out Place)` | `DwellSpots` take `Place`. |
| `PhysicalPopulation.Queues.cs` | `simulation.State.Vendors.FindPlace(` (2) | The stand (publication, closing time, position, opening) is not on `IVendorRowView`. |
| `PhysicalPopulation.Queues.cs` | `simulation.State.Food` | Vendor meal rows are not on `IFoodView`. |
| `PhysicalPopulation.Queues.cs` | `VendorWaiting(IPersonView, out VendorRow, out VendorVehicle)` | Stand times and position; `DwellSpots.TryVendorQueue` takes `VendorRow`. |
| `PhysicalPopulation.Selection.cs` | `FadeRestricted(CitizenState)` | Compares the entity and its journey by reference. |
| `PhysicalPopulation.Selection.cs` | `SelectionCandidate(CitizenState, …)` and its `Citizen` | The bind step follows the candidate's journey (`Binding.Citizen`). |
| `PhysicalPopulation.Selection.cs` | `Outdoor(CitizenState)` | Reads the optional stop; `OptionalStopStatus` takes it. |
| `PhysicalPopulation.Selection.cs` | `(Place Place, List<Dwell> Dwells)`, `QueueDwell(CitizenState, Place, double)`, `TryLine(Place, …)` | `QueueAtOpening` and `DwellSpots` take the entity and `Place`. |
| `PhysicalVehicles.cs` | `TryAcquire(CitizenState, PhysicalRoadSample, out …)` | Reads the saved car (`CarState`), which has no view. |

The plan asks for the read-only route and pose helpers to take `IPersonView`. In these files every route or pose helper reads the journey or calls a sampler that takes the entity, so each is a named exception above. The helpers that read only view members (`TrackVendorWaiter`, `VendorWaiting`) take `IPersonView`. `PhysicalVehicles.cs` has no other person-typed helper. I kept `TryAcquire` named rather than splitting it into `(IPersonView, CarState)`: the car is mutable too, and the split would rewrite the source fragment that `tools/physical-selection-regression` extracts.

### Where the boundary is closed

Closed for the paths `OwnershipBoundaryTests` covers. In the five presentation files, no line reads `CitySimulator.State` and no line names an entity type, except the named lines above. The grid query hands out `IPersonView`s (reflection test).

Still exposes mutable state, named and not closed:

- Every named exception above, in particular `Binding.Citizen`. Following, fades, queues and optional stops still work on the entity, through `Entity()` and the fields above.
- Entity members reached through a named field without naming the type (`binding.Citizen.Identity.Id`, `var citizen = binding.Citizen`) are not policed. The rule polices entity types in type position, `.State` reads and casts.
- `ActiveJourneyIndex.BodyCandidates`, `Member`, `KindOf`, `ReceivesBody`, `MatchesScan`, `PresentationGrid.Contains` and `PresentationGrid.Matches` keep entity parameters or results. They are Core-side checks used by Core, tests and the F2 driver; Runtime presentation calls none of them except `ActiveJourneys.Contains`, which now takes the view.
- Outside the presentation files: `MultimodalRoutes.TryPhysicalRoadSample(CitizenState, …)`, `PlayerContact.Settle(…, CityState)`, `DwellSpots` (`Place`, `VendorRow`) and the F9e remaining limits (`WeatherDiagnostics.cs`, the save owner's record count, the F9c harness).
- A view is a contract, not a copy (Decision 8). Code in the same assembly can still cast; in the presentation files the only cast is `Entity()`.

Closing the rest needs view members for the journey (a route view), the optional stop, the car and the identity, plus samplers that accept the view. That is a change to `Core/CityView.cs`, `Core/Model.cs` and the simulator, outside F9g's Owns. `BodyCandidates` can become view-typed once F2g's driver reads it through the view.

### Base finding

At base `649467b`, `OwnershipBoundaryTests.RuntimeStateReadsAreOnTheAllowedListAndObserversUseTheView` fails. X2 (#184) split `PhysicalPopulation.cs` after F9e, and the test allowed only the file name `PhysicalPopulation.cs`. So the `.State` reads in `.Following.cs`, `.Queues.cs` and `.Selection.cs` were reported, and the cited `row` receiver no longer resolved. This branch replaces the file allowance with the named lines, and that test passes again.

## Offline checks

PC (20 logical cores), at this branch's head:

- Build: `python tools/build.py --game-managed "…\Big Ambitions_Data\Managed"` passed (`DLL SHA256: 4c4482660dc9f22d8ec67c6d088782a81fc382258bc063a6ccfb7375f3fd67f2`).
- Focused run, 10 November 2026: `dotnet test … --filter` over `MovementEquivalenceTests` (12), `OwnershipBoundaryTests` (13), `PresentationGridTests` (7), `PresentationBoundaryTests` (9) and `VisitorBodyTests` (6): 47 passed, 0 failed, 3 min 3 s.
- Full suite, 10 November 2026, before the sync with `f3a5210` and the base repair below: `dotnet test tests/PersistentCity.Tests.csproj`: 4,011 passed, 0 failed, 8 min 22 s. The `wait-other-dotnet.ps1` wait before it timed out at the tool's 10-minute limit while other sessions' dotnet runs were still active, so this run shared the machine.
- The first full run (local-test round 1) failed one test: `PresentationBoundaryTests.PhysicalRuntimeCallsOnlyTheReviewedSimulationMembers`, which keeps an exact list of the `CitySimulator` members presentation calls. The F9g reads add `View`. The list now names it; nothing else changed.

| Required test (F9g row) | Fact |
| --- | --- |
| Source test with no blanket `PhysicalPopulation.cs` exception, only named signatures | `PresentationReadsThroughTheViewWithOnlyNamedExceptions` (no presentation file has a file allowance; every named exception has a reason and still occurs; the minute, rosters and person count are read through the view; exactly the six named `.State` reads remain) and `RuntimeStateReadsAreOnTheAllowedListAndObserversUseTheView` (no violation over all Runtime files) |
| A planted entity-typed read in a presentation fixture is caught | `APlantedEntityReadInAPresentationFixtureIsCaught`. It plants into `PhysicalPopulation.Selection.cs`: a `CitizenState` read helper, a `List<CitizenState>` field, a `(CitizenState)` cast, an `as CitizenState` cast, `VendorRow`, `Place` and `CityState` signatures, `simulation.State.Minute`, and `var current = simulation; current.State`. Each is caught as exactly one violation. A `CitizenState` helper planted in each of the other four presentation files is caught. A named fragment copied into another file is caught. View reads, comments and strings are not reported. |
| Reflection test that `BodyCandidates` and `Query` return view types | `CandidateQueriesReturnViewTypes`: `Query` returns `IReadOnlyList<IPersonView>` and fills a `List<IPersonView>`; `ActiveJourneyIndex.Contains` takes `IPersonView`. The only entity-typed public result on `ActiveJourneyIndex` and `PresentationGrid` is the named `BodyCandidates`, so adding another one, or making `BodyCandidates` view-typed without removing the exception, fails. The query returns the filed entities themselves (reference-equal), with the expected set. |
| Selected set and order unchanged on the F2f grid tests | `PresentationGridTests` (11 facts) and `VisitorBodyTests`, unchanged apart from the type change: the grid-against-full-scan comparisons (`Within`, `Waiting`, ordered by distance and ordinal id) run on `Query`'s results cast back to entities in the test. |
| F2 movement-equivalence harness arms unchanged | `MovementEquivalenceTests` (12 tests: walker and mixed-city body arms equal bodies-off, the arm catalogue, mid-journey reload, injected faults) passed unchanged at this head in the focused and full runs above; `tools/movement-equivalence/` is not edited. |
| Current-writer golden pins unchanged | `NativeCapabilityTests.CurrentWriterGoldenHashIsUnchanged` and `ChangeJournalTests.CurrentWriterGoldenSnapshotAndReload` (schema held at 1, seeds 5150 and 9217) |

Test files changed for the type change only (listed for the PR):

- `tests/PresentationGridTests.cs`: query buffers `List<IPersonView>`; `Query(...).Cast<CitizenState>()` before the entity-based `Within`/`Waiting` comparisons; `c.Id` for `c.Identity.Id` on a query result; the runtime-source literal now reads `simulation.View.ResidentList`.
- `tests/PresentationBoundaryTests.cs`: `View` added to the reviewed `CitySimulator` member list (`ReadOnlyMembers`).
- `tests/VisitorBodyTests.cs`: the selection replica's query buffer is `List<IPersonView>`, and it casts the query result to the entity before reading the journey, as Runtime does through `Entity()`.

Not run: `tools/physical-selection-regression` (the `tools/*-regression` suites are not run before release). Its selection fragment changed (view reads, `Entity(c)`), so its extraction may need a stub update when it is next run.

### Base repair after the sync

After the merge of X3, `NativeCapabilityTests.CurrentWriterGoldenHashIsUnchanged` no longer compiled. The base version asserted `CurrentSchema == 35` and pinned writer-35 hashes. An earlier F9g commit (`1e13cd7`) had pointed it at `CityState.PlayerHoldSchema` and `DevelopmentChain.Write35`, and X3 removed both. Commit `707d479` deleted the theory. The fix round restored it: it now asserts `CityState.CurrentSchema == 1` with the message from `SchemaHeldAtOneTests`, writes with `SaveCodec.Write`, and pins the schema-1 goldens:

| Seed | SHA-256 | Characters |
| --- | --- | --- |
| 5150 (reader corpus `schema-01/full.json`) | `ffa821dea8fe057959a91135db75342a3b69c2bdec8487af7485a35dd5d0889c` | 73,330 |
| 9217 | `a9651c15e44c67fa8e05ac084b638ac1bfaf0c48805a8d1142a873c423a6422c` | 72,968 |

The restored theory passes at this head and also on `codex/persistent-city` at `5b56bda` without the F9g diff, so F9g changes no save bytes. The post-sync full suite is the harness's pre-merge run; this branch records no full run after the sync.

This is offline evidence only. No game, rig, ModsLocal or bridge was used.

## In-game checks

In-game checks for the next batched rig. All run on the headless agent rig in the combined native/perf batch, in the disposable FT-F9 city from the F9 plan's section 7 at checkpoint `f9-city-ready`, never in Peter's ordinary save. The rig records the autosave setting, turns autosave off in the isolated profile, checks it is off before each measured window (also after reload) and restores it at cleanup. None is left to Peter, a playtest or a by-hand check. "Locked-PC" means console, bridge-read and file steps only; "input-only" means the step needs synthetic UI input. "Base build" means `codex/persistent-city` at `5b56bda` installed on a copy of the same checkpoint. Check 1 is the plan's check 6 at this head; checks 2 to 4 are F9g's own.

1. **Detail transitions and in-flight reload** (plan check 6). Locked-PC.
   - Setup: `f9-city-ready` at 07:30.
   - Steps: teleport beside the shop, away 1 km and back; enter and leave a building interior; skip 2 hours; save with followers mid-leg and reload in a fresh process. Repeat with bodies off.
   - Expected: equal PC extracts and native order lists with and without bodies.
   - Rig capability: existing FT `teleport`/`save`/`load`, RIG-CHECKPOINTS `physical_lease_visible`; pending RIG-PCCONFIG `PhysicalEnabled`, RIG-PCSAVE.
2. **Body selection matches the base build.** Locked-PC.
   - Setup: two copies of `f9-city-ready` at 07:30 with the focus beside the shop, one with this head and one with the base build.
   - Steps: on each, run 10 game minutes at speed 1 and read `pc movement status` and the physical population diagnostics line every game minute.
   - Expected: on both builds `journeyIndexMismatches=0` and `journeyIndexFallback=False`, `followers` and `cars` above 0 at some sample, no exception in the mod log; the same persons are bound at the first sample after load on both builds (selection is deterministic before native movement differs).
   - Rig capability: existing FT `console`, `teleport`, `time`; pending RIG-ACCEPT.
3. **Door and vendor queue bodies.** Locked-PC.
   - Setup: `f9-city-ready` 10 minutes before the shop opens, with residents waiting for the opening; a second copy with a published Street Food truck stand and a resident walking to it.
   - Steps: teleport beside the shop door, run until the opening, read the diagnostics line (`queueing`, `queued`) every game minute and take a screenshot at the opening; repeat beside the truck.
   - Expected: `queueing` above 0 before the opening and back to 0 after the queue walks in; the vendor queue forms and clears; equal counts to the base build on the same copy.
   - Rig capability: existing FT `teleport`, `time`, `shoot`, `console`; pending RIG-ACCEPT.
4. **Presentation cost in the perf window** (part of plan check 10). Locked-PC.
   - Setup: autosave recorded, off and checked before each window (also after reload).
   - Steps: matched windows at the same city size beside the shop at 07:30 (walkers and cars bound) on this head and the base build.
   - Expected: mod-work p95 under 5 ms; every mod-work spike of 30 ms or more flagged with condition and component; whole-frame timing reported separately; `PhysicalPopulation.Update` within noise of the base build; autosave restored at cleanup.
   - Rig capability: existing `rig flow run` autosave guard, RIG-PERF-UNION clean windows; pending RIG-PERF-UNION PC component counters.
