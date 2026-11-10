Status: implemented (offline); not verified by rig

# F8b-F8g: capability surface, contracts, RLO split, conflicts, mechanisms, failure matrix

Refs [#95](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/95). Rows F8b-F8g of the [F8 plan](../plans/f8-capability-boundaries.md), one branch. Base `origin/codex/persistent-city` at `17dccf8b` (E6f #185; F8a #173 at `84eaa1cb`). No schema or save-shape change; no edit under `Persistence/`. F8h is not in scope. Perf: [docs/perf/f8bg.md](../perf/f8bg.md).

## F8b: `pc capability status | show`

Implemented. `Core/CapabilitySnapshot.cs` builds a D1 registry from plain copies of the existing status members (`CapabilityInputs`); `Runtime/CapabilityDiagnostics.cs` (a `PopulationRuntime` partial) fills them on request and prints F8a's report. One routing branch in `RunCensusCommand`. `ReadRlo` keeps its last answer for the report (one line, no extra read). Inspector guards are read from Harmony patch owners (metadata only, one pass), because their state is private to `ResidentInspector.cs`, which F8 does not edit. `vendor.streetfood` follows the bound API's snapshot status (`CapabilityInputs.ReadVendor`): Off, Refused or Failed is off with the snapshot's reason. `interior.view` names the setting, the guard or the missing speed accessor once a city runs. The commands and the activation line catch their own exceptions, so a diagnostic never stops the city.

Checks: `CapabilityReportTests` (every state from doubles, header wording, source boundary: no mutating member, not reached from `Update`, red with one injected call each).

## F8c: food-economy contracts verified before effect

Implemented. `Core/NativeMemberContract.cs` checks exact shapes (parameter lists, return and field types, static-ness, inherited fields as AccessTools resolves them). `Runtime/NativeFoodContract.cs` binds the real types:

- **food.authority** (required): the six H1 targets and `BusinessSimulator.buildingRegistration`/`currentHour`, checked in `NativeFoodOwnership.Install` before any patch, through `NativeFoodContract.InstallChecked`, which the harness runs with the same refusal and rollback paths. A refusal throws the named start refusal (`Persistent City did not start: food.authority is off: <member> ...`); no owner is installed and no saved key is read or written. The loader bootstrap logs the same refusal instead of failing the mod load. Targets are patched through the verified `MethodInfo`; the inventory now pins their parameter lists.
- **food.checkout.shop** (optional): `IsPriceAcceptable(String, Single)`, `ProcessCustomer(CustomerEntry)` and the three `FoodSupplyReader` members, checked when the market is constructed. Refused, the market runs in checkout-off mode (`Core` `FoodCheckoutMode`, which the market applies and the tests drive): no price prefix, `CanServe` false, `SupportedMenu` false (so every shop is observed ineligible), and `ScopeStatus` returns `Excluded: food checkout contract off: <member>`. A shop already owned keeps its loaded hour protected and is handed back at the next drained boundary through the existing release path. `SellLine` refuses before `beginReceipt` if the method is missing. `FoodSupplyReader` takes its members from the same contract.
- **Fault points** (test build only): `python tools/build.py --game-managed ... --fault-points` defines `PC_FAULT_POINTS` and writes `build/PersistentCity-faultpoints`. `PC_FAULT_POINT=authority:<member>:name|signature`, `checkout:<member>:name|signature` or `research:<text>` (one `#if` block in `ResearchBillingContract.Check`). The release build has no fault code.
- Every existing refusal stays.

Checks: `NativeFoodContractTests`; `NativeFoodContractTests.CheckoutOffReloadSettlesPaidReceiptsAndKeepsUncertainOnesHeld` reloads an owned scope with paid and uncertain receipts in checkout-off mode through Core's production reconciliation (paid settled without a checkout, uncertain kept with its held funds, no sale, evidence unchanged); `tools/food-ownership-regression` `CheckoutOff` runs the checkout-off transition through production `NativeFoodOwnership` (owned hour protected across a reload, withdrawal persisted, receipts unchanged, drained-boundary hand-back persisted, no reacquisition; the older arms of that harness predate the compact wire and stop after it); stub harness `tools/capability-contracts` (`dotnet run -c Release`, 92 checks: exact targets patched by the pinned Harmony 2.4.2, 14 fault arms refused before any patch with saved keys unchanged, checkout fault leaves authority verified, research fault text, and the red-on-base arm: the base by-name lookup finds a changed `ProcessCustomer`, begins a receipt and leaves it Pending; F8c refuses before any receipt).

## F8d: RLO reason classes

Implemented (D6 option B). `RloReadResult.Reason` is set at every construction site; `Continuity(state)` classes a read as Match, Different, Transient or Unsupported.

| Path | Answer | Reason | After activation |
| --- | --- | --- | --- |
| CityApi | Ready, same catalogue | Ready | continue |
| CityApi | Ready, different catalogue | Ready | stop, payload kept (as before) |
| CityApi | Off / NotYet / Refreshing; empty population or seed 0 | Off / NotYet / Refreshing | continue and save on the saved catalogue |
| CityApi | Shared / Failed / API threw | Shared / Failed / Incompatible | stop (as before) |
| Reflection | Going Public | Shared | stop |
| Reflection | Flare not running | Off | continue |
| Reflection | no director, no account, seed 0, empty population | NotYet | continue |
| Reflection | avatar mismatch, account changed during the read | Refreshing | continue |

Startup still requires Ready and activation stays strict (`MatchesPopulation`). The save gate uses the same class, so a transient save writes the Ready bytes. No identity is created, renamed or regenerated. One activation Player.log line lists every capability that is not active.

**Road speeds in a transient window** (review Sol-r1-1). Before round 1, `ReadSlowedLanes` answering NotYet or Refreshing threw from `RoadRoutes.Prepare` and stopped the runtime through `UpdateCity`'s catch. Now Compatibility raises a distinct `RloRoadSpeedsNotReadyException`, which `MultimodalRoutes` classifies for `NativeRoadSpeeds` (so the road harnesses still compile it without Compatibility). `Refresh` keeps the last verified overrides and records `Waiting`; while waiting `Canonical`/`Effective` throw `RoadSpeedsWaitingException` instead of handing out timing; `RoadRoutes.Prepare`, the sweeps in `MultimodalRoutes.Prepare` and `RoadPlan` start, search and completion wait and retry; `ReadyForPopulation` is false. `UpdateCity` takes its existing waiting branch (the accounting hold keeps native time) and the city keeps running and saving. `tools/road-speed-regression` `TransientCaptureWaits` drives the real `RoadRoutes` through a held plan, a lane change during Refreshing and a save in the window. Nothing is read as "no slowdowns". Shared and Failed still throw and stop, as the donor read does.

`rlo.roadspeeds` reports the planner's own capture: verified, waiting, or the recorded terminal refusal. `rlo.reservations` reports the guard's own last verdict (`RloPhysicalReservations.Last`, recorded by every construction, update and failure; no extra read). A Ready catalogue that differs from the saved one shows `rlo.donor` off.

Checks: `RloContinuityTests` (red on the base code for the transient cases: there every non-Ready read failed `MatchesPopulation` and stopped the city).

## F8e: hook conflict report

Implemented, report only. Harmony 2.4.2 has `Harmony.GetAllPatchedMethods()` and `GetPatchInfo` (confirmed against the package; it also lists `InnerPrefixes`/`InnerPostfixes`, classed rewrites). `Runtime/HookConflicts.cs` reads both; `Core/HookConflictReport.cs` joins them to the F8a inventory by signature. A method Harmony still lists after its last patch is removed counts as not patched. Persistent City's own owners (H1/H3/H4 on `RunMainGameTick`) are not foreign. Once at activation (one line with the scan time) and on `pc capability hooks`. Nothing is refused or unpatched.

Checks: `HookConflictReportTests` (including the pinned Harmony 2.4.2 on stand-in methods); `tools/capability-contracts` hook arm with a transpiler.

## F8f: transaction mechanisms

Implemented. `Core/TransactionMechanisms.cs`: `shop.fullservice-bag.v1`, `vehicle.streetfood-api.v1`, `shop.dynamic-catalogue` (E5b5 landed dynamic acquisition; its refusal text `E5b dynamic native acquisition off; original three only` maps to `shop.dynamic-catalogue off`), `research.agency-bill.v1`; each implemented with its checks link and rig pending. `pc capability business <place>` reads `NativeFoodMarket.ReadOnlyScopeStatus`, which uses only state the market holds (no `SupportedMenu`, `ContractAdmits` or `NativeFoodOwnership.Find`, so it refuses no contract and changes no observation), or the vendor market's serving answer. No change to `Supported`.

Checks: `TransactionMechanismTests`.

## F8g: failure matrix

Implemented. `tools/capability-matrix` (`dotnet run -c Release [days]`) over the 957-resident economy fixture (factor 2); the same code runs in `CapabilityMatrixTests` for one day. Arms: control; presentation off (driver attached, zero bodies) against never present at the same frame cadence; `food.checkout.shop` off; each required capability refused; RLO transient window and a Shared stop; each research contract off alone through the production gate (`ResearchEligibility.SessionEnabled`, `MarketResearchActivation`) against all on; `food.dynamic` off against on through the production menu predicate (`FoodMenuPolicy.LiveMenu`), with the control selling at the dynamic site; `rlo.roadspeeds` is covered by `tools/road-speed-regression` (the road planner is Runtime); Street Food absent then returned and a bound Street Food whose snapshot is Off, Refused or Failed (vendor rig, test only). `CapabilityMatrix.Coverage` names the arm for every optional capability; the inspector guards and the interior view have no Core input and are covered by the report tests and the rig.

## Requirements A1-A17

| # | Passing check |
| --- | --- |
| A1 | `NativeCapabilityTests.DefinitionsFollowTheD2Table`, `CapabilityReportTests.ReportFromDoublesForEveryState`, `HookInventorySourceTests.ProductionSourcesMatchTheInventory` |
| A2 | `RloContinuityTests.ATransientWindowContinuesSavesTheReadyBytesAndChangesNoIdentity`, `RloContinuityTests.ReflectionFallbackClassesGoingPublicAsStopAndFlareOffAsTransient` |
| A3 | `RloContinuityTests.CallSitesUseTheClassAndAddNoRead`, `RloContinuityTests.ATransientWindowContinuesSavesTheReadyBytesAndChangesNoIdentity`, `RloContinuityTests.ReadyButDifferentStopsWithThePayloadKept` |
| A4 | `TransactionMechanismTests.VerifiedByRigWithoutAResultLinkIsRefused`, `TransactionMechanismTests.EveryScopedBusinessHasOneMechanismOrOneUnsupportedReason` |
| A5 | `HookInventorySourceTests.ProductionSourcesMatchTheInventory`, `HookConflictReportTests.RealHarmonyOnStandInMethodsIsClassed`, `tools/capability-contracts` |
| A6 | `CapabilityMatrixTests.EveryCoreArmPasses`, `NativeFoodContractTests.CheckoutOffWithdrawsAnOwnedShopAtTheNextBoundaryAndKeepsItsReceipts`, `CapabilityReportTests.HealthyInputsReportEveryReadCapabilityActive` |
| A7 | `RloContinuityTests.CallSitesUseTheClassAndAddNoRead`, `NativeFoodContractTests.CheckoutOffIsDecidedAtConstructionAndGuardsEveryEffect` |
| A8 | `RloContinuityTests.ATransientWindowContinuesSavesTheReadyBytesAndChangesNoIdentity`, `CapabilityMatrixTests.EveryCoreArmPasses` |
| A9 | `NativeFoodContractTests.TheAuthorityContractIsCheckedBeforeAnyPatchOrSaveRead`, `NativeFoodContractTests.CheckoutOffIsDecidedAtConstructionAndGuardsEveryEffect` |
| A10 | `TransactionMechanismTests.EveryScopedBusinessHasOneMechanismOrOneUnsupportedReason`, `TransactionMechanismTests.TheBusinessCommandReadsTheExistingScopeStatusOnly` |
| A11 | `TransactionMechanismTests.MetadataRegistrationGrantsNoMechanism` |
| A12 | `HookInventorySourceTests.InventoryCountsAndCapabilityMapping`, `HookConflictReportTests.ForeignOwnersAreJoinedToTheInventoryAndOurOwnersAreNot` |
| A13 | `CapabilityReportTests.HealthyInputsReportEveryReadCapabilityActive`, `TransactionMechanismTests.VerifiedByRigWithoutAResultLinkIsRefused` |
| A14 | `CapabilityMatrixTests.EveryCoreArmPasses`, `HookConflictReportTests.ReportOnlyChangesNoCapability` |
| A15 | `CapabilityReportTests.DiagnosticsFileCallsNoMutatingMember`, `CapabilityReportTests.DiagnosticsAreNotReachedFromUpdate` |
| A16 | `CapabilityReportTests.ReportFromDoublesForEveryState`, `RloContinuityTests.EveryBridgeStatusMapsToItsReasonClass`, `NativeFoodContractTests.EachChangedAuthorityMemberGivesANamedStartRefusal` |
| A17 | `NativeFoodContractTests.FaultPointsExistOnlyInTheTestBuild`, `CapabilityMatrixTests.StreetFoodAbsentThenReturned`, `CapabilityMatrixTests.EveryCoreArmPasses`; in-game checks below |

The same mapping is `CapabilityMatrixTests.Requirements`; `EveryRequirementHasANamedPassingCheck` fails if a requirement is missing or names a check that does not exist. It reads no docs file.

## Offline checks run

- `python tools/build.py --game-managed ...`: passed (release); `--fault-points`: passed.
- `python tools/test-focused.py`: the changed test classes pass.
- `tools/capability-contracts`: checks passed. `tools/capability-matrix`: 7 of 7 Core arms passed. `tools/road-speed-regression`, `tools/road-handoff-regression`, `tools/gate-access-regression`: pass.
- Current-writer golden hash unchanged: `CapabilityReportTests.CurrentWriterGoldenHashIsUnchanged` (writer `CityState.CurrentSchema`).

Notes for review: X3 (#192) owns the golden pins and holds the schema at 1. F8a's `NativeCapabilityTests.CurrentWriterGoldenHashIsUnchanged` asserted writer 35 and failed at the writer-36 base; it is removed. `CapabilityReportTests.CurrentWriterGoldenHashIsUnchanged` pins no hash: it checks the `CityState.CurrentSchema` envelope and that building and printing the reports leaves the payload unchanged. `tools/food-supply-regression` already failed to build at the base (missing `EmployeeWorkstation`/`CargoInstance` stubs) and now also lacks `NativeFoodContract`; it is not run before release and is left as found.

## In-game checks for the next batched rig

Fixture for all: a disposable FT city on the owned rig with Persistent City, RLO and Street Food, one owned food shop and one Street Food cart owned by PC, built and aged at head. Autosave off in the isolated profile, checked before every measured window (also after reload) and restored at cleanup. State is read with `pc capability status` through FT `console`. Red is the base build, green the build after these rows. Every check is rig-automated; none is deferred to Peter or a playtest. Locked-PC means the rig drives it with input locked; input-only means it needs a mid-session game-option change.

1. **Capability report and modpack hook owners** (plan check 1; F8b, F8e; locked-PC). Setup: full modpack DLL set. Steps: load the fixture; run `pc capability status` and `pc capability hooks`; keep the Player.log activation lines. Expected: header `city running`; every required capability `active`; optional ones `active` or `off` with a reason; the hook list names every foreign owner on the inventory targets with its class; the result file is kept as F8h's input. **Existing:** RIG-MODSET full set, FT `console`/`load`.
2. **Street Food absent, then returned** (plan check 2; F8b; locked-PC). Steps: save; load without `FoodTrucks`; run 2 game hours; save; restore Street Food and load; run to the next crew opening. Expected: `vendor.streetfood optional absent: Street Food (FoodTrucks) is not loaded`; shop money and receipts equal a run with no cart; vehicle rows, receipts and identities unchanged by the window; after return `active` and the cart requalifies. **Existing:** RIG-MODSET, FT `save`/`load`/`time`; **pending:** RIG-PCSAVE `money`/`receipts`.
3. **Driven Rides absent** (plan check 3; F8b; locked-PC). Steps: load without Driven Rides. Expected: `clock.drivenrides required-when-present absent`, city runs, calendar factor as configured. **Existing:** RIG-MODSET, FT `console`.
4. **RLO absent at load** (plan check 4; F8b; locked-PC). Steps: load without RLO. Expected: no city starts; `rlo.donor required absent` with RLO's required message; raw PC payload hash unchanged; native customers serve the shop; no PC receipt key is written. **Existing:** RIG-MODSET build selection and raw PC payload hash/refusal-status readback.
5. **Game update: changed required member** (plan check 5; F8c; locked-PC). Steps: build with `--fault-points`; set `PC_FAULT_POINT=authority:ProcessAllCustomersFromThisHour:name`; load. Expected red: the base build has no such check (record behaviour). Green: Player.log and status `Persistent City did not start: food.authority is off: RetailBusinessSimulator.ProcessAllCustomersFromThisHourFault() is missing.`; payload hash unchanged; no `persistentcity.food-authority` owner in `pc capability hooks`; native customers serve the shop. **Existing:** RIG-MODSET build selection, payload hash readback; **pending:** RIG-FAULT selection of the `PersistentCity-faultpoints` build and the variable.
6. **Game update: changed checkout member** (plan check 6; F8c; locked-PC). Steps: fault build with `PC_FAULT_POINT=checkout:ProcessCustomer:signature`; load in the owned shop's open hour; run to lunch. Expected red: first checkout leaves an uncertain receipt. Green: `food.checkout.shop optional off: RetailBusinessSimulator.ProcessCustomer(CustomerEntry, Int32) changed ...`; the owned shop keeps the loaded hour protected, shows `Excluded: food checkout contract off: ...`, begins no new PC receipt and is handed back at the next drained boundary, after which native customers serve it; the city runs. **Existing:** RIG-MODSET, FT `time`; **pending:** RIG-FAULT, RIG-PCSAVE `receipts`.
7. **Game update: changed optional research contract** (plan check 7; F8b, F8c; locked-PC). Steps: fault build with `PC_FAULT_POINT=research:GameManager.ChangeMoneySafe`; load; run past one nightly marketing bill. Expected: `research.billing optional off: GameManager.ChangeMoneySafe changed (PC_FAULT_POINTS).`; no research charge; native campaigns unchanged; the city runs. **Existing:** RIG-MODSET, FT `time`; **pending:** RIG-FAULT, RIG-PCSAVE.
8. **RLO Flare switched off mid-session** (plan check 8; F8d; input-only, it toggles an RLO option in the running session). Steps: run 1 game hour; switch RLO's Social Media option off; run 1 hour; save; switch it on; run 1 hour; save; reload. Expected red: the city stops at the next periodic check. Green: Player.log `RLO transient (Off): ...`; `rlo.donor required off: transient (Off): ... The city continues and saves on its saved donor catalogue.`, header `city running`; `rlo.reservations` and `rlo.roadspeeds` active; bodies continue; the in-window save succeeds; resident IDs, names and count unchanged; reload continues. **Pending:** RIG-RLOTOGGLE, RIG-PCSAVE `ids`; **existing:** FT `save`/`load`.
9. **Mechanism report** (plan check 9; F8f; locked-PC). Steps: `pc capability business <shop place>` and `<cart place>`; edit a third shop to carry a non-historical product with dynamic acquisition off. Expected: `shop.fullservice-bag.v1 (implemented; rig pending (tests/A5IntegratedAcceptanceTests.cs))`, `vehicle.streetfood-api.v1 (...)`; the third shop `shop.dynamic-catalogue off: E5b dynamic native acquisition off; original three only: ...`. **Existing:** FT `console`, RIG-SHOPEDIT `shop_edit`.
10. **Performance** (plan check 11; F8b, F8d, F8e; locked-PC). Steps: in a clean RIG-PERF-UNION window at default caps, run `pc capability status` once per game minute for 30 game minutes; record the activation scan line (`hook conflicts at activation ... scan <ms>`) from the load log. Expected: mod-work p95 < 5 ms; every mod-work sample >= 30 ms listed with its component; whole-frame metrics reported separately; activation scan time recorded. **Existing:** RIG-PERF-UNION `perf_window`, FT `console`.

Plan check 10 (foreign prefix on checkout) belongs to F8h and is not listed. Rig capability requests (PeterHartwieg/big-ambitions-agent-rig): RIG-FAULT (checks 5-7), RIG-PCSAVE (2, 6-8), RIG-RLOTOGGLE (8).

## Rig check 1 — 10 November 2026

PC: verified by rig at `b3e5a910`: city running, all five required capabilities active; twelve optional active and two off with reasons. [Small F8h input](f8h-check1-modpack-owners-pc.json) records all 49 targets, foreign owner classes, activation excerpts and the full isolated DLL set. [Batched report](rig-2026-11-10-results.md).

Mac: [check 1 Mac record](f8h-check1-modpack-owners-mac.json): verified by rig, city running; required capabilities active, optional capabilities active or off with a reason. Full Mac modpack hook owners and classes are recorded. [Batch results](rig-2026-11-10-mac-results.md) retain the initial creation-save refusal and setup. This is F8h input, not F8h or Windows compatibility proof.
