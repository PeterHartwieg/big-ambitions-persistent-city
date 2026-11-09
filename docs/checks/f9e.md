Status: implemented (offline); not verified by rig

# City acceptance F9e: read-only city views, 9 November 2026

Refs [#96](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/96). Branch `codex/pc-f9e-read-only-views` from `codex/persistent-city` at `9cb1490` (E5b4 #180 merged, current writer 36; F9c #174 at `12f0b32`). Scope: the F9e row and [Decision 8](../plans/f9-city-acceptance.md#decision-8-read-only-city-views-for-observers) of the F9 plan, for observers only. Presentation (`Runtime/PhysicalPopulation.cs`, the candidate queries) is F9g. No schema, save shape or `Persistence/` change.

Re-read at this base: `Core/CitySimulator.cs` (`State` at `:54`, `Inspect`), `Core/Model.cs` (`Place`, `CitizenState`, `CityState`), `Core/FoodCommerce.cs` (`FoodScope`, `FoodCommerceState`), `Core/ResearchLedger.cs`, `Core/VendorState.cs`, `Core/EconomicLedger.cs`, `Core/ResidentInsight.cs`, the six observer files in Runtime, `tests/Acceptance/*`, `tests/PresentationBoundaryTests.cs` and `Persistence/CompactWire34.cs` (`BuildFieldLimits`).

## What is implemented

- **`Core/CityView.cs`.**
  - `ICityView`: `Minute`, `ResidentList`, `VisitorRoutineList`, `VisitorCount`, `PortalCount`, `PlaceList`, `Food` (null until the city owns a food scope), `Economy`, `Research`, `VendorRowList` and `FindVendor`.
  - Entity views: `IPersonView`, `IPlaceView`, `IFoodView`, `IFoodScopeView`, `IEconomicLedgerView`, `IResearchView`, `IResearchOrderView`, `IVendorRowView`. Every member returns a view, an enum, a primitive, a string, `Position`, `Money` or an `IReadOnlyList` of those.
  - `CityView`, the one internal implementation. It reads the state's current lists and values on every access, so a replaced list or changed value shows at once.
  - `CityViewReports`, observer reports that run an existing Core helper on the state behind a view and return text, values or detached copies: the economy overview (history rows copied), the money rescan, the food history report and its recording days, the city economy lines, the person line, the census capture, a research ledger copy and copies of the vendor-place meals.
  - `FoodHistoryReport`: the history indexed once per panel refresh. The saved day rows stay inside it; the panel gets the weeks, periods, journal and latest-day text.
- **`CitySimulator.View`** (next to `State`). It creates the view once; later reads allocate nothing.
- **Entity classes** (`CitizenState`, `Place`, `FoodScope`, `ResearchOrder`, `VendorRow`, `EconomicLedger`) implement their views with explicit, getter-only members. No field, public member, wire or save change.
- **Observers read through the view:**
  - `Runtime/FoodEconomyPanel.cs`: the panel text, the page count, the F6c overview and the economy overview.
  - `Runtime/AccountingStatus.cs`: the logical minute and lag.
  - `Runtime/ResearchDiagnostics.cs`: the `pcresearch status` counts through the view; `pcresearch orders` and `pcresearch report` on a fresh detached ledger copy; the research window on a detached copy refreshed at most every 0.25 s while it draws.
  - `Runtime/VendorDiagnostics.cs`: `pc vendor sales` on detached copies of the vendor-place meals and their wallets.
  - `Runtime/SaveCostStatus.cs`: holds no state. The save owner passes the record-count function (see below).
  - `Runtime/PopulationCensusDiagnostics.cs`: `pc population person`, the census capture and its minute.
  - The resident inspector: `CitySimulator.Inspect` passes the view to `ResidentInsight.Capture`, which reads the minute, the household count and whether food is active through it.
  - The F9c oracles: persons, households, places, vendor rows and research orders in `CityOracles.Checkpoint`, the research-charge oracle, and the reload-state and minute reads in `CityScenario`.
- **`tests/OwnershipBoundaryTests.cs`**: 8 facts, 9 test cases (table below).

### Technical choices

- **No observer file reads `State`** (review round 1 removed the three named sinks of the first version). Where a sink outside F9e's files takes an entity, the observer gets a detached copy:
  - Research: `ResearchReportText` and the research window take a `ResearchLedger`. `CityViewReports.ResearchSnapshot` returns the F7c save copy (`ResearchLedger.CopyForSave`: same orders, states and order; reports are immutable and shared). Ordering on the copy leaves the city's ledger unchanged.
  - Vendor sales: `FoodSaleReceipts.ReadStatus` takes a `ResidentMeal`. `CityViewReports.VendorMeals` returns deep copies of the vendor-place meals and their wallets.
  - Economy overview: `FoodEconomyInsight.Capture` returns the saved history rows. `CityViewReports.EconomyOverview` returns its fields in the same order with the rows copied, so the bridge JSON is unchanged.
  - The copies come from one reflection copier in `CityView.cs` (public settable properties, lists and arrays element by element; anything outside Core is refused). It runs only for these on-demand diagnostics.
- **Save-cost counts come from the save owner.** `SaveCodec.CountRecords` validates the state it wrote. Decoding the published payload instead would cost 221 ms and 72 MB on the main thread at 957 residents ([perf](../perf/f9e.md)), a spike inside an explicitly requested save-profiling run. So `PersistentCityMod.cs`, the load and save owner already on the allowed list, passes `payload => SaveCodec.CountRecords(simulation!.State, payload)` into `CountSavedRecords`, which only times and records it. This is the one edit outside F9e's Owns list in Runtime: one call line in an allowed owner file.
- **Reports instead of new helper signatures.** `FoodEconomyInsight`, `FoodMoneyCounters`, `FoodFeedback`, `FoodFeedbackV2`, `VisitorSchedules` and `PopulationCensusInput` keep their concrete parameters, because their files are outside F9e. `CityViewReports` unwraps the view inside Core and returns the helper's result, so the observer files hold only `ICityView`. A foreign `ICityView` is refused.
- **Allowed files.** The plan names `PersistentCityMod.cs`, `AccountingRecovery.cs`, `StreetFoodVendorMarket.cs` and `PhysicalPopulation.cs` (until F9g). Two files that read `simulation.State` at this base are not in the plan's list and not in F9e's Owns, so they are allowed by name:
  - `MovementDiagnostics.cs` (F2e). It publishes and replays player holds into the state, which is a write.
  - `WeatherDiagnostics.cs`. It is an observer and still reads the state; a later slice can move it.
- **Type-based receivers without Roslyn.** The scan blanks comments and string literals (interpolation holes stay code). It reads each file's declarations and its partial-class siblings', and `var x = y` or `var x = y ?? …` copies y's type. An untyped lambda parameter makes its name unresolved unless a candidate is `CitySimulator`. Declarations decide first: a name with a `CitySimulator` candidate is a simulator read. Only a name they leave unresolved falls back to a cited entry (`row`, `order`, `open`, `r`, `stored`, `done`, `prepared`), so `private void Probe(CitySimulator r) { r.State }` in `VendorDiagnostics.cs` is caught although its lambda `r` is a vendor row (review Sol-r1-3). Otherwise an unresolved receiver fails.
- **View collections are named apart from saved fields.** `CompactWire34.BuildFieldLimits` takes each saved collection's read bound as the largest over every public property of that name in Core, interfaces included. A view member named `AdditionalProductIds` raised the saved bound from 7 to 50,000; `F7dCompactWriterTests` caught it. Hence `ResidentList`, `PlaceList`, `ScopeList`, `ExtraProductIds`, `OpenOrders` and so on. The reflection test refuses any shared name and pins the specialized bounds.
- **The inspector keeps the person entity.** `ResidentInsight` reads a person's preferences, traits, journey legs, optional stop and meal outing, which are not view members. The inspector's Runtime file (`ResidentInspector.cs`) only ever sees the `ResidentInsight` result. Moving the person to `IPersonView` belongs with F9g's presentation work. The `CityState` overload of `Capture` stays for tests and `tools/a5-inspector-regression`, and forwards to the view.
- **One assertion outside the Owns list.** `tests/OrdinaryExchangeTests.cs` counted the panel's direct `FoodFeedbackV2.AppendCity(` call. It now counts `CityViewReports.AppendCityEconomy(` in the panel and the one `FoodFeedbackV2.AppendCity(` in `Core/CityView.cs`, keeping its intent (the city projection renders once).

### Remaining limits

These are named, not closed:

- A view is a contract, not a copy (plan Decision 8). Code in the same assembly can cast a view back to its entity. The source test forbids that in the six observer files only.
- The detached copies are mutable objects. Changing one changes nothing in the city, and the next call returns a new copy.
- Still on `State`: `WeatherDiagnostics.cs`, the save owner's record count, and in the F9c harness the visitor persons (`VisitorPerson` stays), wallets, meals and save writes.
- `PhysicalPopulation.cs` reads the state until F9g.

## Offline checks

Mac (Apple M1 Pro), at this branch's head:

- Build: `python3 tools/build.py --game-managed "…/dll-import-shim/Big Ambitions_Data/Managed"` passed.
- `dotnet test tests/PersistentCity.Tests.csproj`: 4,352 passed ([validation run](#validation-run)).
- `dotnet test … --filter "FullyQualifiedName~OwnershipBoundaryTests"`: 9 passed.
- Before and after on the same cities: the observer outputs (table below) hashed at base `9cb1490` with the base code's `State` reads, and at this head through `View`. After review round 1 the projection also covers `pcresearch orders` and `report` text, `pc vendor sales`, the save-cost record counts and the panel's whole per-shop history block. The base run used a scratch copy of `ThroughState` and its helpers on an export of `9cb1490`; the scratch file is not committed.

| City | Base `9cb1490` (State reads) | This head (View reads) | Characters |
| --- | --- | --- | --- |
| F5h reference, day 2 13:00 (957 residents) | `d34a62f1b4130212d02f7f64ed455b5b5692267508c1fe208188f49fee9476ca` | same | 1,581,352 |
| F9c city-s reference run, final (126 residents, 3 reports, 1 vendor row) | `98cc1a334019164a0960f233fd90634705ad11ffa99b7d836cd5f90b61a795df` | same | 176,389 |

| Required test (F9e row) | Fact |
| --- | --- |
| Type-based source test lists the allowed `.State` users and refuses unresolved receivers | `RuntimeStateReadsAreOnTheAllowedListAndObserversUseTheView` |
| A planted `current.State` read in a diagnostics fixture is caught | `APlantedStateReadInADiagnosticsFixtureIsCaught`. Plants into `PopulationCensusDiagnostics.cs`: `var current = simulation; current.State`, `simulation!.State`, `this.simulation?.State`, an interpolation hole, a `CitySimulator` parameter, an undeclared receiver, a call receiver, an unknown declared type, two casts back to entities. Comments and string text are not reads. `RuntimeStateReadsAreOnTheAllowedListAndObserversUseTheView` also plants the old save-cost read and a `CitySimulator r` parameter that shares the vendor lambda's name. |
| Panel, inspector and diagnostic outputs identical before and after on the F5h and F9c cities | `PanelInspectorAndDiagnosticOutputsAreIdenticalThroughTheViewOnTheF5hAndF9cCities` (State reads against View reads in one run, and every view member against its field), plus the hash table above (base code against this head) |
| No type reachable from `ICityView` exposes a setter, an entity class or a mutable collection | `NoTypeReachableFromTheCityViewExposesASetterAnEntityOrAMutableCollection` (also: explicit implementations only, no view collection name shared with a saved field, `CityView` not public) |
| Current-writer golden pins unchanged | `CurrentWriterGoldenHashIsUnchanged` (writer 36, E5b4's pins for seeds 5150 and 9217) |
| No allocation per read in the panel refresh | `PanelRefreshReadsThroughTheViewAllocateNothing`: 0 bytes over 1,000 refreshes of the panel's view reads (255 places, 3 shops) |
| The live view | `TheViewIsLiveAndAStateWithoutFoodHasNoFoodView` |
| Observer reports share no saved object (review round 1) | `ObserverReportsShareNoSavedObjectWithTheCity`: every public report is classified (a new one fails until listed); `FoodHistoryReport` returns only text; the overview's JSON is unchanged and editing its rows leaves the city's overview JSON unchanged; all 202 F9c meals and wallets detach to the same JSON and editing the copies changes nothing; ordering on the research copy leaves the city's ledger unchanged |

### Validation run

Mac (Apple M1 Pro), after review round 1:

- Build: passed (`DLL SHA256: 363626de361dca8290edf2796c14c1d336952cb8cda73918d76911333c848492`).
- `dotnet test tests/PersistentCity.Tests.csproj`: 4,352 passed, 0 failed, 8 min 23 s (another session's tests ran on the machine just before). Before review: 4,351 passed, 6 min 55 s at `54029ab`.
- An earlier full run, before the collection renames, failed two tests: `F7dCompactWriterTests.SpecializedBodyBoundsApplyToMenuIdsProductNamesAndPortals`, which found the loosened saved bound (see *View collections are named apart from saved fields*), and `FoodContractCoordinatorTests.RecheckBoundaryRebuildAndCommitGateCosts`, a timing test that passed alone and in the run above.

This is offline evidence only. No game, rig, ModsLocal or bridge was used.

## In-game checks

In-game checks for the next batched rig. All run on the headless agent rig in the combined native/perf batch, in the disposable FT-F9 city from the F9 plan's section 7 at checkpoint `f9-city-ready`, never in Peter's ordinary save. The rig records the autosave setting, turns autosave off in the isolated profile, checks it is off before each measured window (also after reload) and restores it at cleanup. None is left to Peter, a playtest or a by-hand check. "Locked-PC" means console, bridge-read and file steps only; "input-only" means the step needs synthetic UI input. "Base build" means `codex/persistent-city` at `9cb1490` installed on a copy of the same checkpoint. Check 1 is the plan's check 3 at this head; checks 2 to 6 are F9e's own.

1. **Active capabilities and status lines** (plan check 3). Locked-PC.
   - Setup: `f9-city-ready`.
   - Steps: read `pc population manifest`, `pc vendor status`, `pcresearch status` and the `AccountingDiagnostics` status object.
   - Expected: each capability recorded as active or inactive with its reason; an inactive one is recorded as inactive, never as a pass.
   - Rig capability: existing FT `console`; pending F8's capability report (until then the status lines only) and RIG-ACCEPT.
2. **Diagnostic commands equal the base build.** Locked-PC.
   - Setup: two copies of `f9-city-ready` at the same game minute with time paused, one with this head and one with the base build.
   - Steps: on each run `pc population person <id>` for five residents and one visitor, `pc population manifest`, `pcresearch status`, `pcresearch orders`, `pcresearch report <id>` for one kept order, `pc vendor status` and `pc vendor sales`; read `AccountingDiagnostics`, `EconomyOverview` and `FoodFeedbackOverview` through the bridge.
   - Expected: every output is byte-identical between the two builds, apart from timing fields (`WorkSpikes`, `MaximumWorkMilliseconds`, `panelBuilds`).
   - Rig capability: existing FT `console`, `state`, `time`; pending RIG-ACCEPT, RIG-SHOPRENT (for the fixture).
3. **Food panel text equals the base build.** Input-only.
   - Setup: as check 2.
   - Steps: press Ctrl+F8 on each copy, wait one refresh (3 s), capture the panel and read `FoodFeedbackOverview`; page through every shop page.
   - Expected: identical panel text on both builds for every page; the panel shows each managed shop with its name, weeks and accounts lines.
   - Rig capability: existing FT `shoot`; pending RIG-LIVE (Ctrl+F8 key input).
4. **Resident inspector equals the base build.** Input-only.
   - Setup: as check 2, with own walkers visible near the shop.
   - Steps: click three residents on each copy and capture the popup; read `InspectorTiming`.
   - Expected: identical popup text (story, heading, meals, memory and budget words, addresses) for the same resident IDs on both builds.
   - Rig capability: existing FT `shoot`, `state`; pending RIG-LIVE (pointer input on a walker).
5. **Population census and save-cost status after a save.** Locked-PC.
   - Setup: `f9-city-ready`; set `SaveCostDiagnosticsRequested` through the bridge.
   - Steps: run `pc population census` and wait for the result; save through FT and read `SaveCostDiagnostics`.
   - Expected: census and record counts equal the base build on the same checkpoint; the census minute equals the logical minute; no save failure.
   - Rig capability: existing FT `console`, `save`; pending RIG-ACCEPT.
6. **Observer cost in the perf window** (part of plan check 10). Locked-PC for the closed-panel window, input-only for the open one.
   - Setup: autosave recorded, off and checked before each window (also after reload).
   - Steps: matched windows at the same city size: panel closed with no command; panel closed with 20 `pc population person` commands; panel open (Ctrl+F8).
   - Expected: mod-work p95 under 5 ms in each; every mod-work spike of 30 ms or more flagged with condition and component; whole-frame timing reported separately; the open-panel window within noise of the base build; autosave restored at cleanup.
   - Rig capability: existing `rig flow run` autosave guard, RIG-PERF-UNION clean windows, FT `console`; pending RIG-LIVE (Ctrl+F8) for the open-panel window and RIG-PERF-UNION PC component counters.
