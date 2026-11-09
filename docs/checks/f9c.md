Status: implemented (offline); not verified by rig

# City acceptance F9c: integrated city fixture and scenario, 9 November 2026

Refs [#96](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/96). Branch `codex/pc-f9c-city-fixture` from `codex/persistent-city` at `b8feb06` (F2e #172 merged, current writer 35). Scope: the F9c row and Decision 3 of [the F9 plan](../plans/f9-city-acceptance.md#decision-3-one-integrated-city-fixture-in-two-sizes), without body or follower arms (F9f) and without read-only views (F9e). Test code only: no product file, no schema, no save shape and no `Persistence/` change.

Re-read at this base: `tests/F5hIntegratedAcceptanceTests.cs` (`Shape`, `ReferenceShape`, `AdmittedPortalPlanner`, the F1 frame loop), `tests/F5gPopulationFillTests.cs` (`Places`, `Catalogue`, `Conserved`), `tools/economy-interventions/InterventionInputs.cs` and `EconomyFixture.cs` (`InputSchedule`, the rival double), `tests/StreetFoodDoubles.cs`, `tests/VehicleMarketTests.cs`, `Runtime/StreetFoodVendorMarket.cs`, `Runtime/FoodMarkets.cs`, `Core/ResearchLedger.cs`, `Core/ResearchSurvey.cs`, `Core/ResearchCharge.cs`, `Runtime/ResearchReportText.cs`, `Core/AccountingClock.cs`.

## What is implemented

- **`tests/Acceptance/CityFixture.cs`.** One city in two sizes, both built from F5h's `Shape` (F5g housing of seven beds, the census catalogue, two portals, seed 2026):
  - `city-m`: F5h's reference shape, 957 residents and three player food shops. The fill grows it to 2,552 persons by day 4.
  - `city-s`: F5g's base businesses on 20 homes, 126 residents and one player shop. The plan asked for 128; 20 homes of this housing give 126, the nearest size.
  - Both add one rival (`b-rival`) under the F6b service limits, the F6a three-product menu, weighted choice, awareness and rivals from day 1, household finances, C5 research on the saved ledger, and one crew-run Street Food cart through the real `StreetFoodBridge` and `StreetFoodVendorMarket` over the contract stand-in. The shops and the cart reach the city through Runtime's `FoodMarkets` composite.
  - The checkpoint is the fresh city at 06:00 on day 1, written by the current writer.
  - Native doubles live outside the saved city and survive a reload: the shop and rival market, the stand-in, the receipt dictionary and the agency bill. The shop double loses the acknowledgement of every 23rd intent by hash, as F5h's does, so some receipts stay uncertain.
- **`tests/Acceptance/CityScenario.cs`.** Each arm runs 06:00 on day 1 to 06:00 on day 4 from the same checkpoint.
  - The **reference** drains every native minute synchronously with a synchronous walking planner.
  - **Cadence arms** run F1's frame loop with F5h's admitted planner (16 native paths per frame): 30 FPS / 32 units / 4 ms, 60 / 16 / 2 and 120 / 128 / 2.
  - The **hidden skip** arm runs 60 / 32 / 4 and skips 13:00–15:00 on day 2 at 120 minutes requested per frame, 128 units, 16 ms.
  - The **reload** arm runs 60 / 16 / 2, and saves and reloads into a new simulator, vendor market, research session and composite market at each state of `CityReload`: every day boundary, between two fill units, with a receipt uncertain, with a research order surveyed but not billed, with the vendor lease open, with a held meal decision, and mid-journey. Each state is checked again on the loaded city.
  - At every whole native minute, after the ending epoch drained: the hourly oracles; the save at each day boundary; the inputs stamped at that minute; the stand-in's clock, openings and lease, then `Prepare`; research orders; and at native midnight the agency bill with its notices.
- **Timestamped native changes.** F6a's `InputSchedule` with `ProductPrice` +50% on every product, `Stock` to zero for one product, `MealsPerHour` halved, `Hours` closing at 18:00, `RivalLanes` 50% and `WorkplaceClosed` for the largest workplace on day 2, and the reverts on day 3.
- **C5 and E6 in the same city.**
  - Research: one order of each product (`basic-local`, `detailed-local`, `detailed-city`) for the first shop, on days 1, 2 and 3 at 10:05, through `ResearchSurveyRunner.TryOrder`. They are billed through `ResearchChargeSession` and noticed through `ResearchNoticeTracker`.
  - The cart opens 11:05–14:05 daily. PC withdraws it at 12:30 on day 3.
- **`tests/Acceptance/CityOracles.cs`**, checked at every drained hour. Every failure throws `CityViolation`.
  - **Identity:** person, household (home and its residents), place, vendor-row and research-order ID sets, written into the ledger as a digest. A seen ID that names another entity fails.
  - **Conservation:** per-wallet `CashConserved` and F5g's BigInteger city ledger. Native money: confirmed prepared-food spending plus the price of every still-uncertain confirmed meal equals what the shop double and the stand-in booked. No duplicate order and no historical checkout.
  - **Research charges:** every tagged charge belongs to one Paid order with its report, at its product's price, and native money moved by exactly the charges.
  - **Vendor sales:** every sale happened under the stand-in's lease and PC's ownership, with unique sequences.
  - **Schedule:** every input stamped before the minute was published once, at its own minute.
- **Event order and equality** (`CityOracles.Equal`). One ordered ledger per arm:
  - journey events (`ActivityObserved`), routine misses, meal decisions, native orders (each with the buyer's wallet as checkout found it), vendor sales and vendor script events;
  - every wallet posting (daily bill, income, shift wage), one row per wallet at the minute it drained;
  - published inputs, research orders, state moves, bills and notices;
  - the hourly identity and wallet totals.
  - Each arm must equal the reference in the ledger, the save bytes at every drained day boundary, the native orders, the vendor sales and the final save. A vendor sale is compared without the stand-in's lease end, because a reload renews the transient lease at other minutes.
  - The scenario compares runs with each other and pins none of its own save hashes.
- **`tests/F9IntegratedCityTests.cs`**, 29 facts. Traits `Size=city-s` and `Size=city-m` select one size; both run in the default suite (perf below).

### Technical choices

- **The reference drains each native minute.** It shares no frame logic with the arms, so an arm that equals it is independent of slicing, cadence and planner admission.
- **Inputs publish at the minute boundary.** Native time crosses one whole minute per frame at most (`AccountingClock.Limit`). The scenario publishes the inputs stamped at that minute when the frame crosses it, after the ending epoch drained and before `BeginAccountingObservations`. That is the condition `InputSchedule.PublishAt` checks. `PublishAt` takes F6a's `EconomyMarket`, so the scenario reuses the schedule's validation and order and applies each input to its own double.
- **The fill-unit reload ends the frame.** In the reload arm a frame ends after a fill unit while the fill is pending, so the save falls between two units in both sizes (in city-s the whole fill otherwise runs inside one frame). After the load the fill must still be pending; the simulator's fill-unit counter is not saved.
- **The vendor market is not disposed on reload.** Disposing hands every vehicle back, which a save and load in the same native session does not do. The reload calls `CompleteForSave` before writing, as `PersistentCityMod` does, and `Prepare` at the current minute after loading.
- **Older-writer refusal reads the constant.** The fact uses `CityState.CurrentSchema - 1` and `SaveCodec.LatestDevelopmentSchema`. It refuses the scenario city with its header stamped as the previous writer, and also through `DevelopmentChain.Write<N>` where that frozen writer exists (`Write34` at this base).
- **Golden pins.** `CurrentWriterGoldenHashIsUnchanged` checks the writer-35 pins of `PlayerHoldTests` on this branch. If a later writer merges first, the same pins apply to `DevelopmentChain.Write35`, and the fact fails if that writer was not frozen.
- **No new link line** in `tests/PersistentCity.Tests.csproj`. Every harness and Runtime source the fixture uses is already linked.

## Offline checks

Mac (Apple M1 Pro), at this branch's head:

- Build: `python3 tools/build.py --game-managed "…/dll-import-shim/Big Ambitions_Data/Managed"` passed.
- `dotnet test tests/PersistentCity.Tests.csproj`: 4,317 passed, 0 failed, 7 min 17 s (after review round 1; 4,316 in 7 min 5 s before).
- `dotnet test … --filter "FullyQualifiedName~F9IntegratedCityTests"`: 29 passed, 38.6 s after review round 1 (23.5 s before postings joined the ledger). Size filters before round 1: `&Size=city-s` 16 passed in 5.0 s, `&Size=city-m` 10 passed in 20.9 s.

| Required test | Facts |
| --- | --- |
| Every arm equals the reference (ledger, saves at day boundaries, native orders) | `SmallArmEqualsTheReference`, `MediumArmEqualsTheReference` × fps30, fps60, fps120, hidden-skip, reload |
| Each reload state is reached and asserted | `SmallReloadArmReachesEveryInFlightState`, `MediumReloadArmReachesEveryInFlightState` |
| Fault injection fails loudly | `AnInjectedFaultFailsTheScenario` × duplicate sale, skipped input, research charge without a report, vendor sale outside its lease; `AnArmThatSkipsAnInputDiffersFromTheReferenceEvenWithoutTheScheduleOracle`, `APostingMovedAcrossASaleFailsTheEventOrderOracle` |
| One research order of each product billed once, reported the next morning | `SmallResearchOrderOfEachProductIsBilledOnceAndReportedTheNextMorning`, `Medium…` (reference and reload arms) |
| A vendor scope sells to residents and hands back on release | `SmallVendorScopeSellsToResidentsAndHandsBackOnRelease`, `Medium…` |
| Timestamped native changes take effect at their minute | `SmallNativeChangesTakeEffectAtTheirMinute`, `Medium…` |
| Older-writer refusal of the scenario city | `TheScenarioCityWrittenAsTheOlderWriterIsRefusedWithStartANewCityAndKept` |
| Current-writer golden pins unchanged | `CurrentWriterGoldenHashIsUnchanged` (seeds 5150, 9217) |
| Fixture composition | `SmallFixtureIsTheDeclaredComposition`, `MediumFixtureIsTheDeclaredComposition` |

The faults fail with these messages:

- Duplicate sale: `duplicate native order <intent>`, found in the booked orders, at 13:00 on day 1.
- Skipped input: `scheduled input … WorkplaceClosed … skipped` at 05:00 on day 2.
- Charge without a report: `research charge pcr-999 without a delivered report` at the day-2 bill.
- Sale outside the lease: `vendor sale outside its lease at 2206 (lease until 2205)` once the stand-in's lease lapsed.

Fixture hashes of the creation-time saves (for F9a's provenance, not pinned by any test):

- `city-s`: `bca4a8f0a9c7f0d1cd081f769b5642b47612480943b92c9873dab37619cc1f87`
- `city-m`: `f5cd0bd9b4923e630354c8498b2590b21921d72279078e786f4d763c5b6d697c`

For F9a's stage rows: `city-s` is `--filter "FullyQualifiedName~F9IntegratedCityTests&Size=city-s"`, `city-m` the same with `Size=city-m`.

This is offline evidence only. No game, rig, ModsLocal or bridge was used.

## In-game checks for the next batched rig

All run on the headless agent rig in the combined native/perf batch, in the disposable FT-F9 city from the F9 plan's section 7, never in Peter's ordinary save. The rig records the autosave setting, turns autosave off in the isolated profile, checks it is off before each measured window (also after reload) and restores it at cleanup. Functional steps that save run outside F4's timing windows. None is left to Peter, a playtest or a by-hand check. "Locked-PC" means console and file steps only. "Input-only" means the step needs synthetic UI input. These are the F9 plan's section 7 checks 2, 4, 5, 6 (bodies-off half; the body half is F9f), 7 and 8 at this head. A check that repeats another slice's check runs once and is recorded under both IDs.

1. **FT-F9 fixture with a player food business** (plan check 2). Locked-PC.
   - Setup: fresh head city.
   - Steps: create the city; rent and fit one food shop with staff, stock and prices through the shop fixture; age to day 3; save as checkpoint `f9-city-ready`.
   - Expected: PC runs at the current writer with no refusal; `pc population manifest` is at its targets; the shop is owned by PC; `pc food decisions` shows decisions at that shop.
   - Rig capability: existing `produce_fixture.py`, `shop_fixture`, `shop_state`, FT `save`/`console`; pending RIG-SHOPRENT (the 6 Nov run refused native rental).
2. **Seven days with daily save/reload against an uninterrupted copy** (plan check 4). Locked-PC.
   - Setup: `f9-city-ready` and a copy.
   - Steps: as F5h check 4, with the player business open: run both copies seven game days; save and reload one at each midnight in a fresh process. (The offline scenario covers three days; the rig keeps the plan's seven.)
   - Expected: equal PC state extracts and native order lists at each daily checkpoint; ledger conservation at each.
   - Rig capability: existing `rig flow run`, FT `time`/`save`/`load`/`console`; pending RIG-PCSAVE, RIG-LEDGER, RIG-LIVE.
3. **Timestamped native changes across cadences** (plan check 5). Locked-PC.
   - Setup: `f9-city-ready` at 06:00.
   - Steps: at fixed game minutes, raise the shop's prices by 50%, set one product's stock to zero, remove one staff member and shorten its hours. Run the same day at frame caps 30, 60 and 120, and once with a hidden two-hour skip.
   - Expected: identical PC extracts and native order lists across arms; each change takes effect at the same simulated minute (orders at the raised price only after the raise, no sold-out product after the stock change, none after the new close); `HistoricalClockMisses` 0; lag ≤ 1 game minute.
   - Rig capability: existing RIG-SHOPEDIT `shop_edit`, RIG-CHECKPOINTS, FT `time`; pending RIG-FRAMECAP, RIG-PCSAVE.
4. **In-flight reload with bodies off** (plan check 6, bodies-off half). Locked-PC.
   - Setup: `f9-city-ready` at 07:30 with `PhysicalEnabled = false`.
   - Steps: skip 2 hours (hidden skip), then save and reload in a fresh process at these points:
     - mid-journey (a resident travelling);
     - with a meal decision held;
     - at midnight while the fill runs;
     - with a receipt pending (the PC receipt dictionary holds a `pending|` entry).
   - Expected: equal PC extracts and native order lists against an uninterrupted copy.
   - Rig capability: existing FT `save`/`load`/`console`, RIG-CHECKPOINTS; pending RIG-PCCONFIG `PhysicalEnabled`, RIG-PCSAVE.
5. **Research and a food truck in the same city** (plan check 7). Input-only (agency phone dialog).
   - Setup: `f9-city-ready` with Street Food installed and one crew-run truck opening.
   - Steps: order a neighbourhood snapshot through the agency phone dialog. Run past the night's marketing bill to the next morning, with one save/reload while the order is pending. Read `pcresearch orders`, `pc vendor status` and `pc vendor sales`. Withdraw the truck with the existing `pc vendor withdraw <vehicle>` command while it is open.
   - Expected:
     - one charge on that night's marketing bill and the report the next morning;
     - at most one pending order for the business;
     - residents buy at the truck, and the opening hands back on release while Street Food keeps it open;
     - no truck sale after the handback; receipts unique across the reload.
   - Rig capability: existing RIG-MODSET (Street Food present), FT `console`/`save`/`load`; pending RIG-LIVE (agency phone dialog input).
6. **Older-schema city refused and kept** (plan check 8). Locked-PC.
   - Setup and steps: F5h check 5 at this head, with a city written by the writer before the current one.
   - Expected: the "start a new city" status; payload and other PC keys byte-identical across a native save.
   - Rig capability: as F5h check 5 (pending older-schema FT loader, RIG-PCSAVE).
