# Code map

Where things are, as of 11 November 2026 (X7). Behaviour is in [behaviour](behaviour.md), terms in the [glossary](glossary.md), the save string in [save format](save-format.md). Paths are from the repository root; line numbers drift, so search for the member name.

## Folders

| Folder | Holds |
| --- | --- |
| `Core/` | Pure simulation (netstandard2.1, no Unity): `CitySimulator`, routines, traffic kernel, food commerce, money, research, capabilities |
| `Persistence/` | `SaveCodec` and its wire partials, `SaveSnapshot`, `SaveSession`, `SaveStore`, `SaveEpoch`, receipts and authority stores |
| `Runtime/` | Unity and game calls: `PopulationRuntime` (the mod host), native food market, physical bodies, routes, panels, diagnostics |
| `Compatibility/` | The only RLO access (`RloBridge`, `RloIdentityAdapter`) and the Street Food bridge |
| `config/` | `persistent-city.json`, the shipped settings template |
| `tests/` | xUnit project `PersistentCity.Tests.csproj`; `tests/Kit` holds the shared builders and doubles; `tests/economy-interventions`, `tests/movement-equivalence` and `tests/capability-matrix` are tool projects whose shared sources the tests compile |
| `tools/` | `build.py`, `test-focused.py`, `docs-links`, the acceptance runner and the F4 perf-gate tools. Older harnesses are in the tag `tools-history-2026-11-11` |

## Tick path

One game frame, in order. The entry is `PopulationRuntime.Update()` in `Runtime/SaveEpochRuntime.cs`, which wraps `UpdateCity()` in a save-epoch unit so a native save inside it is refused as re-entrant.

1. **Native time.** Harmony prefixes on `GameManager.RunMainGameTick` (`Runtime/NativeCityClock.cs`): `TickPrefix` applies the calendar factor; `AccountingPrefix` holds native time until the population has caught up (`AccountingClock.Limit` in `Core/AccountingClock.cs`). This is the backpressure point.
2. **`UpdateCity()`** (`Runtime/PersistentCityMod.cs`):
   1. Accounting save-change checks (`Runtime/AccountingRecovery.cs`), startup clock and initialization (`CompleteInitialization`, `TryStart`) until a city exists.
   2. `BeginAccountingObservations`: publishes the new native-minute epoch to the simulator.
   3. Shop discovery (`NativeFoodCandidates.RefreshDiscovery`), route-object steps, periodic RLO continuity and station rescans.
   4. One Street Food vendor unit (`StreetFoodVendorMarket.Prepare`), food opening hours (`NativeFoodHours`), optional-stop environment (`CitySimulator.RefreshOptionalStops`).
   5. `physical.BeforeAdvance()` (`Runtime/PhysicalPopulation.Selection.cs`); clock-rate retiming through `TravelTiming.Apply` when the rate changed.
   6. `routes.Prepare()` (road network slices, `Runtime/TrafficRoadNetwork.cs`) and traffic closure refresh (`Core/TrafficJourneys.cs`).
   7. **`AdvancePopulation(target)`**: the frame budget loop. `PopulationFrameBudget` (`Core/AccountingClock.cs`) allows 128–1,024 units, 16–128 person events and 2–4 ms. It alternates `CitySimulator.ContinuePlanning` (resumable route planning) and `AdvanceFrameSlice`, then `SynchronizeSnapshot`.
   8. Player-hold replay, then `physical.Update()`: ambient pedestrians, vehicles, binding observation, and body selection every 0.25 s.
   9. Inspector update and the 60-second summary log.
3. **Inside the simulator** (`CitySimulator.AdvanceCore` in `Core/CitySimulator.cs`), one event at a time under the event budget: ordinary-ownership and campaign continuations, research survey, population fill, day boundary, capability activation, awareness preparation, `FoodCommerce.Step`, `TrafficKernel.AdvanceTo` and person events (`RunEvent`).
4. **Panels.** `OnGUI` in `Runtime/FoodEconomyPanel.cs`: status card, Ctrl+F8 economy panel, research window, inspector.

## Save path

1. `GlobalEvents.onSaveGame += Save` (`Runtime/SaveEpochRuntime.cs`). `Save()` runs `SaveEpochCallback.Run` (`Persistence/SaveEpoch.cs`), which refuses a nested save, a replaced dictionary or a poisoned epoch.
2. `CaptureForSave` / `CaptureSteps` (`Runtime/PersistentCityMod.cs`): accounting observation, RLO check, `physical.BeforeSave`, campaign completion, ownership refresh, input-epoch check, `vendorMarket.CompleteForSave`, then `SaveSession.WriteTo`.
3. `SaveSession.WriteTo` (`Persistence/SaveSession.cs`) calls `SaveCodec.Capture` (`Persistence/SaveCodec.cs`): `CityState.PrepareForSave` runs the snapshot synchronizer (traffic capture in `Core/TrafficJourneys.cs`), then `SaveSnapshot.Capture` (`Persistence/SaveSnapshot.cs`) copies nine sections.
4. `SaveCodec.Encode`: `Validate`, then `EncodeCompact` (`Persistence/CompactWire34.cs`) writes the `pcc-c` string. The session writes `persistentcity:state_v1`; `SaveEpoch` then writes the epoch marker. Receipts and the authority manifest are written at sale and ownership time, not here.

**Load.** `TryStart` (`Runtime/PersistentCityMod.cs`) checks the accounting-stop key and the epoch marker (`SaveEpochStore.CheckForStart`), then reads off the main thread: `SaveSession.Open` → `SaveStore.ReadFrom` → `SaveCodec.Read` → `ReadCompact` → `ReadExact` (`CheckShape`, deserialize, derive persons, `Validate`). `Missing` creates a city from housing; anything but `Loaded` is refused with the payload kept. `CompleteInitialization` refuses again if the payload, marker, RLO or clock changed meanwhile.

## Settings

The file is `ModsLocal/PersistentCity/persistent-city.json`; the template is `config/persistent-city.json`; parsing is `Runtime/PrototypeSettings.cs` (with `InteriorGuardSettings.cs` and `FoodProductOverrideSettings.cs`), clamping happens in `PopulationRuntime.Initialize`. Gameplay keys clamp to their range; developer keys outside their range refuse to start.

| Keys | Default | Range | Kind |
| --- | --- | --- | --- |
| `OptionalStopChoicePercentage` | 25 | 0–100 | Gameplay |
| `StreetLifeBreak/Chat/WindowShopping/Audience/RestingMaximumMinutes` | 10 / 30 / 3 / 8 / 40 | 5–10 / 10–30 / 1–3 / 2–8 / 10–40 | Gameplay |
| `FoodChoiceTemperature`, `…WeightFloor`, `…Taste`, `…Price`, `…Distance`, `…DistanceScaleMinutes`, `…Queue`, `…Quality`, `…TimePressure`, `…Health`, `…Household`, `FoodShortlistTemperature` | 1, .02, 1, 1.2, 1, 10, 1, .5, 1, 0, 1, 1 | clamped per key (`Core/FoodChoiceSettings.cs`) | Gameplay |
| `FoodMenuMaximumOffersPerShop`, `FoodMenuNewTasteWeight`, `FoodMenuHealthWeight` | 4, 1, 0 | 1–8, 0–2, 0–2 | Gameplay |
| `FoodMemory…` (weight, loyal return, curious novelty, penalties, decay, capacity 4, forget after 28 days, regulars) | see `Core/FoodMemorySettings.cs` | clamped per key | Gameplay |
| `FoodMarketing…` (campaign reach .25, retention 7 days, daily decay .5, neighbourhood 250 m, 4 draws, discovery .02) | as listed | clamped per key (`Core/FoodAwareness.cs`, `Core/FoodCampaignSettings.cs`) | Gameplay |
| `Rival…` (choice weight 1, service minutes 5, refresh 60 min, 16 reads per unit, capacity 100%, 2 lanes, 60 units per meal period, profiles) | as listed | clamped per key (`Core/RivalServiceLimits.cs`) | Gameplay |
| `Weather…` (enabled, rain outing ×.7, rain travel ×1.3, queue lead 5 min) | as listed | clamped (`Core/WeatherPolicy.cs`) | Gameplay |
| `MarketResearchCatchmentMetres`, `MarketResearchConsumersPerUnit` | 500, 256 | 50–2,000, 16–1,024 | Gameplay |
| `OrdinaryRetailVisitCents`, `OrdinaryLeisureVisitCents` (not in the template) | 500, 1,200 | 1–10,000 | Gameplay |
| `FoodProductOverrides` | `{}` | bounded table | Gameplay metadata |
| `GameClockFactor` | 1/3 | 0.1–1, else refuse | Developer |
| `PhysicalEnabled`, `MaximumPedestrians`, `MaximumVehicles`, `PhysicalRadiusMetres` | true, 48, 24, 120 | 1–256, 1–64, 30–300, else refuse | Developer |
| `Enabled`, `SaveCostDiagnostics`, `MovementFaultAcquirePercent` | true, false, 0 | — | Developer |
| `InteriorCityView`, `InteriorViewRadiusMetres`, `InteriorMaximumPedestrians`, `InteriorMaximumVehicles` | "off", 60, 24, 12 | "on"/"off", 30–120, 0–48, 0–24 | Developer (F0) |
| `ThirdPartyInteriorGuard`, `…AssemblyPatterns`, `…HarmonyOwnerIds` | "auto" | "auto"/"off", at most 16 strings | Developer (F0 guard) |

## Partial classes

**`CitySimulator`** (Core): `CitySimulator.cs` (event queue, `AdvanceCore`, accounting epochs, planning resume), `TrafficJourneys.cs` (kernel ownership, closures, snapshot sync), `PhysicalJourneys.cs` (journey samplers for bodies), `OptionalDestinations.cs`, `OptionalStopScheduling.cs`, `OptionalVisits.cs` (optional and meal visits), `OrdinaryExchanges.cs` (non-food visit spending), `PopulationFill.cs` (day-boundary fill), `PlayerHolds.cs` (player holds), `VendorStands.cs` (vendor places), `WorkplaceAvailability.cs`, `WeatherPolicy.cs`, `FoodFeedback.cs`, `FoodFinances.cs`, `FoodMemoryCommerce.cs`, `RivalFood.cs`, `RivalOutflowHistory.cs`.

**`FoodCommerce`** (Core): `FoodCommerce.cs` (meal queue and per-meal state machine), `FoodWeightedCommerce.cs` (weighted choice), `FoodFinances.cs` (budget postings), `FoodFeedback.cs`, `FoodMarketHistory.cs` (14-day aggregates), `FoodMealTickets.cs` (same-visit extras), `FoodMemoryCommerce.cs`, `FoodRepresentation.cs` (roster setup and walking quotes), `FoodStops.cs`, `RivalFood.cs`, `RivalFoodMemory.cs`.

**`SaveCodec`** (Persistence): `SaveCodec.cs` (read, write, capture, encode, shape check, validation), `CompactWire34.cs` (the `pcc-c` wire), `PopulationWire18.cs` (capabilities pre-check, record counting), `PersonWire26.cs` and `PersonWire27.cs` (person, visitor and routine tuples), `AwarenessWire19.cs`, `WeatherWire20.cs`, `CampaignWire22.cs`, `MenuWire23.cs`, `OutflowWire24.cs`, `NameWire25.cs` (product names), `ResearchWire29.cs`, `VendorWire30.cs`, `EconomicLedgerWire.cs`, `HeldTuningWire.cs`. The numbers in the file names are the old development writers that introduced each section; they are not schema numbers.

**`PopulationRuntime`** (Runtime, the mod host): `PersistentCityMod.cs` (initialize, `UpdateCity`, `AdvancePopulation`, start, save capture), `SaveEpochRuntime.cs` (frame guard, save callback), `AccountingRecovery.cs`, `AccountingStatus.cs`, `PopulationLifecycle.cs` (fail and reset), `FoodEconomyPanel.cs` (`OnGUI`, Ctrl+F8), `VendorDiagnostics.cs` (the `pc` command router), `PopulationCensusDiagnostics.cs`, `MovementDiagnostics.cs`, `CapabilityDiagnostics.cs`, `FoodContractDiagnostics.cs`, `ResearchDiagnostics.cs` (`pcresearch`), `WorkplaceDiagnostics.cs`, `OrdinaryExchangeDiagnostics.cs`, `NativeCampaignAwareness.cs`, `NativeCampaignDiagnostics.cs`, `FoodMemoryDiagnostics.cs`, `FoodProductMetadataApi.cs`, `InteriorGuardDiagnostics.cs`, `WeatherDiagnostics.cs`, `SaveCostStatus.cs`, `PhysicalPopulation.cs`.

**`NativeFoodMarket`** (Runtime): `NativeFoodMarket.cs` (shop authority, checkout, receipts, patches), `NativeFoodCandidates.cs` (discovery, ownership refresh), `NativeFoodHours.cs`, `NativeFoodAccounts.cs`, `NativeRivalFood.cs`, `NativeCampaignAwareness.cs`, `FoodContractDiagnostics.cs` (loaded-catalogue contract), `FoodProductPolicyNames.cs`, `FoodMemoryDiagnostics.cs`, `CapabilityDiagnostics.cs`.

**`PhysicalPopulation`** (Runtime): `PhysicalPopulation.cs` (bindings, construction, disposal), `.Selection.cs` (update, selection, pools), `.Following.cs` (walker and car follow and caps), `.Queues.cs` (arrival, door and vendor queues).

**`PrototypeSettings`** (Runtime): `PrototypeSettings.cs`, `InteriorGuardSettings.cs`, `FoodProductOverrideSettings.cs`.

## Recipes

### Add a setting

1. Add the key and default to `config/persistent-city.json`.
2. Add a property to `PrototypeSettings` (`Runtime/PrototypeSettings.cs`): either a clamping setter on a backing field (like `OptionalStopChoicePercentage`) or a forwarding property into a Core settings object (like `FoodChoice`). Use a tolerant converter so one bad token does not break the whole file.
3. Clamp in the Core settings class and, for a new group, call its `Clamp` from `PopulationRuntime.Initialize`.
4. Pass it to its consumer in `CompleteInitialization` (an `Attach…` call or a constructor argument).
5. Test the compiled default, the shipped template and the clamp range; `tests/F6kPriceWeightTests.cs` is the pattern. Add the key to the table above.

### Change the save format (schema stays 1)

1. Add or remove the property on the Core model class. The contract resolver writes every writable property as required, so older payloads are refused: that is the in-place change ([decision](decisions/2026-11-09-schema-one.md)). Never touch `CityState.CurrentSchema`; `SchemaHeldAtOneTests` fails if you do.
2. Copy the field in `Persistence/SaveSnapshot.cs`. If a live object owns it, sync it in `PrepareForSave` or the snapshot synchronizer. `SaveSnapshotTests` fails on an uncopied member.
3. Add bounds (`SaveCodec.CollectionLimit`) and semantic checks in `Validate` or the section's wire partial.
4. Regenerate the reader corpus (`tests/ReaderCorpus/README.md`, `PC_READER_CORPUS_WRITE`) and update each failing golden pin once; a failing pin prints the new hash and length. Each fixture has one pin.
5. Add a test that a payload with the old shape is refused, like `TrafficJourneyTests.SavedTrafficVehiclesHaveNoPhysicalControlFieldAndOlderPayloadsAreRefused`.

### Add a test

1. Start from the kit: `TestCity.Minimal()` for a one-resident city, `TestCity.Banded(seed, runTo)` for the banded food city, `NoRoutes` or `FixedWalk(minutes)` as the planner (`tests/Kit`). Larger fixtures (`CityFixture` in `tests/Acceptance`, `EconomyScenario`, `RepresentativeFixture`, the movement fixture) stay with their owners.
2. Drive the city with `CitySimulator.AdvanceTo(minute, budget)` until `CaughtUp`, save with `SaveCodec.Write`, reload with `SaveWire.Read`. Assert invariants (conservation, identity, no duplicate arrival), not repeated formulas. Do not read source files.
3. Put any wall-clock limit through `PerfGate.Under`, and gate a measurement on `PerfGate.Variable("PC_…")`; both run only with `PC_PERF_ASSERTS=1`.
4. Run the build and `python tools/test-focused.py`.
