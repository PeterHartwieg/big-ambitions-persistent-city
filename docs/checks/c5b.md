Status: implemented (offline); not verified by rig

# C5b: survey runner and time hold (6 November 2026)

Plan: [C5 section 4, row C5b](../plans/c5-agency-research.md#c5b--survey-runner-and-time-hold). The design is in section 3.4; the defaults are in [design defaults](../decisions/2026-11-06-design-defaults.md). Base `cfb568a` (C5a merged, writer 25). Perf: [docs/perf/c5b.md](../perf/c5b.md).

## Scope

- **Implemented:** `Core/ResearchSurvey.cs` (`ResearchSurveyRunner`, `OrdinalRosterIndex`, `CityResearchSnapshot : IMarketResearchSnapshot`, `ResearchSurveyRefusal`); `tests/ResearchSurveyTests.cs`. In `Core/CitySimulator.cs`: the `AttachResearch(ResearchLedger, consumersPerUnit = 256, businessPlace = null)` seam and `Research` property, one `NoEventsThrough` clause, one survey unit in the `AdvanceCore` loop next to the campaign publication, an `AdvanceResult.ResearchUnits` count, and one `RebuildRoutes` line that carries the attachment to the replacement.
- **Unchanged:** `ContinuePlanning`, `Core/Model.cs`, `Persistence/`, the codec and the schema (writer 25). No Runtime change: no dialog, billing or UI, and no caller in the game build. `MarketResearchSettings.Available` stays false. The ledger reaches the simulator only through the seam.

## What the code does

- **Order.** `ResearchSurveyRunner.TryOrder` refuses before the ledger sees the order when the city has no food roster (`NoFoodRoster`), the roster is over C4's 150,000 cap (`RosterTooLarge`), a catchment product's business has no city place (`UnknownBusinessPlace`), or the native minute is not compatible with the city clock (`ClockMismatch`). Otherwise it calls `ResearchLedger.TryOrder`, which records `SurveyAtMinute = AccountingClock.Target(orderMinute)`. A ledger refusal is returned as `Ledger` with the ledger's own reason.
- **Hold.** While an Ordered order with `SurveyAtMinute <= target` is unsurveyed, `NoEventsThrough(target)` is false, so `IsCaughtUpTo` is false and native time is held. With `inspectSavedFood` (the epoch-drain check) the clause is skipped, as for the campaign publication: research is never unfinished previous-epoch work.
- **Drain point.** In `AdvanceCore`, after the campaign publication and after `next` is computed, a survey unit runs when `next > SurveyAtMinute`. Every event at or before that minute has then run, no route job is pending (a frame slice returns early while one is, and `ContinuePlanning` is unchanged), and no campaign publication is queued. The unit runs before the later event and before the clock moves. The loop counts it as one bounded unit (`AdvanceResult.ResearchUnits`, excluded from `CitizenEvents`).
- **Units.** First the ordinal index: a bottom-up merge sort over `int[]` positions of `FoodCommerceState.Residents` by `ResidentId`, at most 4,096 element moves per unit (copying the keys counts as moves). Then C4's `MarketResearchEngine.Continue`, at most `ConsumersPerUnit` (clamped 16–1,024, default 256) consumers per unit. On completion the report is sealed with `ResearchLedger.TrySeal` (Ordered → Surveyed) and the next Ordered order is picked (earliest `SurveyAtMinute`, then sequence).
- **Restart.** Before and after each unit the runner checks the clock minute, the roster count and the roster list itself. If any changed, the survey starts again from zero. The survey is transient: a save writes the order as Ordered, and after a reload or a route rebuild the survey restarts from zero on the replayed state. No cursor is saved.
- **Snapshot.** Each roster row maps to C4's `MarketResearchConsumer`:
  - ID: `ResidentMeal.ResidentId`; represented: `Slot > 0`.
  - Home: the anchor's `HomeId` place position. Day place: the `DayPlaceId` place position for work or study, otherwise home.
  - Day activity: Study for students with a day place, Work for other non-retirees with a day place, otherwise Home (C4's "other").
  - Traits: the anchor's `Traits` for slot 0, `RepresentedTraits` for slots above 0.
  - Meals: the open breakfast, lunch and dinner opportunities of the logical day (see decision 3).
  - Household food: `HomeFoodFundedDay == day` once that day's budget has posted.
  - Budget: `HouseholdFinance.SpendingLimit` in whole cents.
  - A missing anchor, place, trait record, meal rhythm (legacy single meals), finance policy, budget or day posting stays `null`.
- **Identity.** City key `"<order ID>|<population key>"` (the population key is hashed if the key would pass 256 characters), logical day `floor(SurveyAtMinute / 1440)`, and the bits of `SurveyAtMinute` for all three revisions. Two orders in the same minute have different identities and report IDs.

## Technical decisions

1. **Business place.** The ledger stores a business address and the catchment needs a centre. `AttachResearch` takes an optional `businessPlace` map from address to Place ID; without it the address is the Place ID. C5e supplies the map.
2. **Unsurveyable orders do not hold time.** An Ordered order this session cannot survey (no roster, roster over the cap, unknown place; possible only through a direct `ResearchLedger.TryOrder` or an attached ledger) is listed in `UnsurveyableOrders`, holds nothing and stays Ordered. The same happens when the roster is removed, emptied or grows past the cap after the order was picked: the next unit releases the hold instead of throwing inside the frame. A reload checks it again. Holding time for it would stop the city for good.
3. **Meal opportunities are the ones still open.** A kind counts when its meal on the logical day is not resolved yet (Pending, Travelling or Serving), or a later kind when the record is still on an earlier kind. A record already on a later day counts 0; a record still on an earlier day counts all three. Under rhythm version 1 every consumer has three meals a day, so a whole-day count would just repeat the consumer count.
4. **Earliest `SurveyAtMinute` first** among Ordered orders, then sequence. With up to 8 open orders this keeps each survey at its own drain point.
5. **Bounded sort kept.** The single synchronous sort's p95 at 100,000 consumers is 46.6 ms (max 60.8 ms, a flagged spike), far above the plan's 1 ms rule, so the bounded index is kept. Its units stay under 0.7 ms ([perf](../perf/c5b.md)).
6. **`RebuildRoutes` carries the attachment.** The replacement simulator gets the same ledger, unit size and place map; the running survey restarts from zero, as after a reload.
7. **No new ledger state or refusal.** `ResearchLedger.cs` (C5a) is unchanged. The runner's refusals have their own enum.

## Offline checks run (6 November 2026)

Every command ran in the foreground after the shared other-dotnet wait. No background process was left running.

| Check | Result |
| --- | --- |
| `python tools/build.py --game-managed "...\Big Ambitions_Data\Managed"` | passed |
| `dotnet test tests/PersistentCity.Tests.csproj` | **3,220 / 3,220 passed** (28 new C5b cases) |
| `dotnet test ... --filter FullyQualifiedName~ResearchSurveyTests` | 28 / 28 passed |
| `PC_C5B_PERF=1 dotnet test ... --filter FullyQualifiedName~ResearchSurveyTests.Perf --logger "console;verbosity=detailed"` | passed; numbers in [docs/perf/c5b.md](../perf/c5b.md) |

New cases in `tests/ResearchSurveyTests.cs`:

- **Hold, then old order.** Two identical cities caught up at noon on day 1; one orders `detailed-city`. `IsCaughtUpTo` is false at noon and at noon + 120 until the seal, and each frame slice runs exactly one survey unit and no citizen event while the clock stays at noon. The epoch-drain check is unchanged by the order. After the seal both cities run to noon + 120: the same activity trace, the same native sale calls and byte-equal writer-25 payloads.
- **Cadence.** Unit sizes 16, 256 and 1,024 with frame budgets 1, 7 and 10,000 give the same cells and report ID. Each equals a one-shot `Array.Sort` plus single engine reference.
- **Reload mid-survey.** After 1, 4 or 7 units the city is saved with the writer-25 codec, read back, and given an in-memory clone of the ledger (`TryRestore` from order images) through the seam. The order is still Ordered, the new runner starts at zero units and holds time, and the result has the same cells, report ID, trace and payload as the uninterrupted run.
- **Route rebuild mid-survey.** `RebuildRoutes` carries the same ledger and unit size; the survey restarts on the replacement and gives the same cells.
- **Pending route probe.** An order is placed while an employment workplace probe (deferred route job) is pending. The survey does not start before the probe commits; the resident's committed `DayPlaceId` is in the result. Reading the same state with the uncommitted empty day place gives different cells. A second run is saved and reloaded before the probe commits: the replayed probe commits the same `DayPlaceId`, and the cells and payload are the same.
- **Same minute.** Two `detailed-city` orders in the same minute: both sealed, different report IDs, equal cells.
- **Missing inputs.** Legacy meal rhythm, no household finances, one anchor and one represented slot without traits, and one worker whose workplace is not a place: meal cells null; household food and budget all Unknown; TraitsUnknown 2 with null trait bands; UsualLocationUnknown 2. A complete city has numeric meal cells and zero unknowns.
- **Roster change.** Removing one roster row after three engine units restarts the survey once; the result counts the new roster and equals the reference.
- **Refusals.** A city without a food roster refuses with `NoFoodRoster` and uses no sequence. A direct ledger order in that city is listed as unsurveyable and never holds time. Unknown business place, unknown product (`Ledger` + `UnknownProduct`) and an incompatible minute (`ClockMismatch`) are refused.
- **Roster removed after the pick.** Replacing the roster with an empty list before the survey starts, or after two engine units, makes the next unit list the order as unsurveyable and release the hold; nothing throws and the order stays Ordered.
- **No city change.** Two orders (catchment and city) surveyed across several frames: the writer-25 payload digest (sales, receipts, budgets, routes, traffic, everything saved) is unchanged, there is no native read or sale call and no citizen event.
- **Idle.** An attached runner with no order runs zero research units over ten hours of frames and gives the same trace and payload as a city without one.
- **Bounded index.** For 0 to 25,000 rows: every unit moves at most 4,096 elements, and the result is a permutation in strict ordinal order.

## In-game checks for the next batched rig

C5b has no caller in the game build, so it has no in-game behavior of its own. These checks cover the survey once C5e places orders. They are the C5b parts of plan section 7 (check 3, the survey half of check 7, and the survey window of check 10). Each runs on a named disposable FT city at the development head with a native marketing agency contact and one PC-owned food business, during agency hours with the player outside the agency.

1. **Order and survey hold (plan check 3).** Setup: C5e or later build, fixture loaded, autosave off. Steps: order `basic-local` for the fixture business; read `pcresearch orders` and the accounting status line every frame batch until the order leaves Ordered; read money, native campaigns and the native transaction count before and after. Expected: the order is Ordered, then Surveyed; the accounting status shows the calendar held only between the order and the seal (offline: 2 frames at 1,914 consumers, 41 to 79 frames at 100,000); no change in money, campaigns or native transactions. Rig-automated (pending capability: R1 `research` op, `order` action; existing capability: `call: console` for `pcresearch` and accounting lines).
2. **Save during the hold restarts the survey (survey half of plan check 7).** Setup: as check 1, with a roster large enough that the survey spans several frames (10,000 consumers or more). Steps: order `detailed-city`; trigger a save while `pcresearch orders` shows Ordered; reload that save; let the survey finish; then on a second copy of the fixture, order the same product at the same minute without the save and let it finish; compare `pcresearch report` cells after both are Paid (or the C5c `pcsave` research section while Surveyed). Expected: the saved order is Ordered with no survey cursor in `pcsave`; after reload the survey starts from zero, holds the calendar again and gives the same cells as the run without the save. Rig-automated (existing capability: `save` trigger, `call: load`, RIG-PCSAVE extraction and compare; pending capability: R1 `order` action).
3. **Order while a route job is pending.** Setup: as check 1, at a minute when `pc` census shows a pending route plan. Steps: order `detailed-city` while the plan is pending; read `pcresearch orders` and the accounting line. Expected: the survey starts only after the pending plan finishes; the order reaches Surveyed; no Persistent City event runs between the plan's commit and the seal. Rig-automated (pending capability: R1 `order` action; existing capability: `call: console` for `pc` census and accounting lines).
4. **Survey window performance (survey window of plan check 10).** Setup: autosave recorded, turned off and checked before the window and after any reload. Steps: a matched idle window and an order-plus-survey window at the same city size. Expected: mod-work p95 under 5 ms in both; every mod-work spike of 30 ms or more flagged with condition and component; whole-frame timing reported separately; research work zero in the idle window; autosave restored at cleanup. Rig-automated (existing capability: RIG-PERF-UNION clean windows; pending capability: R1 `order` action for the order window).
