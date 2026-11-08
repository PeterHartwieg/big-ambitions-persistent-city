Status: implemented (offline); not verified by rig.

# City audit F5g: population fill and visitor transport in writer 33, 8 November 2026

Refs [#92](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/92). Base `3f734cb` on `codex/persistent-city` (writer 33, F6i #150 merged). Scope: the F5g row of [the F5 plan](../plans/f5-population.md#4-slice-breakdown), as re-scoped by the [no-migration decision](../decisions/2026-11-06-no-migration-rescope.md). No schema bump. Nothing here ran in the game or on the rig.

Performance: [docs/perf/f5g.md](../perf/f5g.md).

## What is implemented

- **Fill at day boundaries** (`Core/PopulationFill.cs`, `CitySimulator.AdvanceCore`). When the clock reaches a day boundary, every event at that minute runs first. Then fill units run, before any later event. Each unit creates at most 64 persons (`PopulationFillPolicy.MaximumPersonsPerUnit`) and counts as one event against the frame budget. A boundary with no event of its own still stops the clock, so its fill runs. The simulator does not report itself caught up while a boundary inside the target is unreached or still has fill work. `IsCaughtUpTo` and `IsAccountingEpochDrained` also hold while the boundary they stand on has fill work. There is one scheduler and one event queue. Postings use the existing F3 units and F1 drain.
- **Targets come from the census formula.** `VisitorTargets.Compute` is now the single formula behind both `PopulationCensus.Compute` and the fill. `VisitorCapacity` is `min(2 × Residents, 100,000)`. Commuters are clamped first, then the pool takes the capacity left. Without a portal both targets are zero.
  - The fill reads Runtime's load-time catalogue (`PopulationFillCatalogue`: places and business facts, as F5a's census reads them), the residents' current assignments and the saved portals.
  - The plan for a boundary is latched when that boundary's own events have finished. A catalogue attached later in the day applies at the next boundary. One attached at a boundary that has already latched keeps the latched targets.
  - Runtime attaches the catalogue to every current-schema city after the food market (`PersistentCityMod`). A catalogue the census refuses detaches the fill with a log line and creates nothing.
- **Growth and retention.** A person of pattern P is created only while P's persisted count (active and dormant) is below its target and the aggregate visitor count is below `VisitorCapacity`. Commuters come first, then pool members. Nothing clamps, deletes or retires a persisted person, wallet or obligation. A city seeded above a target keeps everyone and pauses only that pattern. Other patterns fill inside the aggregate capacity that remains. Persons ≤ 150,000 and visitors ≤ 100,000 are checked before each creation. A city without portals gets no portal and no visitor.
- **Identity and determinism.** Each person gets the next `pv:<n>`. Portal, identity, venues, seat and carried income are drawn by the same keys as F5d's fresh roster: `VisitorSchedules.Create` and the fill share `VisitorFactory`. New persons are merged into the ordinal `Visitors`/`VisitorRoutines` lists in one backward pass (`PersonTable.InsertVisitors`). IDs are never reused.
- **Enrolment exactly once** (`FoodCommerce.EnrolCreated`). Each new person gets one own meal record and, with finances on, one wallet from `HouseholdFinance.Create`. The wallet uses the mix of the portal's district (the district of the nearest saved place, ordinal tie-break, from F5d) or the default mix when no district mix is known. A person who already has a record is refused, never re-seeded. With finances off the record has no wallet. A city without a food roster gets no visitor, because writer 33 refuses visitors without a roster. The wallet joins the posting queue in its ordinary position (`BudgetQueue.Insert`).
- **Employment.** Commuters are seated with F5e's rule against the simulator's occupancy index. An unseated working adult carries an outside employer (`ExternalEmployment`). Day visitors never hold a city workplace. They carry an outside employer by the same rule, which only the ordinary job-loss draw ends (`ResidentLifecycle.BeginEmploymentCheck`). The wage and income rules are unchanged.
- **Day-visitor stays** (`DailyRoutine`, `VisitorSchedules.VisitsOn`).
  - A pool member returns on the days drawn by `Hash(seed, PersonId, "visit", day)`. The weighting is 13.75% on each of the four weekdays and 40% on each of the three weekend days, which averages the census's 25%.
  - On a visit day it goes from its portal to its shop and its leisure venue and back. On other days it stays dormant at its portal. When no visit day falls in the routine's nine-day window, it wakes at the window's end and looks again, so no member stays dormant for good.
  - Commuters keep F5d's weekday stays. Dormancy, meal skipping while dormant, deferred departure and the `LastArrival` guard are F5d's and apply to both patterns.
- **Transport.** Visitors walk and take the subway through their portal with the existing planner and F3 admission. No visitor gets a car, a traffic record, a body or a lease. Routes are committed per person (`route.Commit()`). The shared Runtime geometry cache returns copies, so no route, queue position or choice is shared.
- **Writer 33 unchanged.** The visitor tuple already had pattern code `D`. The strict reader and writer now accept it for a visitor with no Employment and no workplace, and refuse it otherwise ("A day visitor holds no employment.").
- **Visitor slot check by ID** (`CitySimulator.CurrentCitizenSlot`). A merge shifts the positions of later visitors. The meal-outing, optional-visit and lease checks now find a visitor by binary search on its ID instead of its queued index. Before F5g both gave the same answer.
- **`pc population manifest`** (debug console, read-only, `CitySimulator.PopulationManifest`) prints:
  - residents, visitors, portals and the next visitor sequence;
  - commuters and pool members, active and in total;
  - fill units and persons created;
  - the latched boundary, both targets, `VisitorCapacity` and any clamp.

### Technical decisions to review

1. **The fill runs after the boundary minute's events, not before.** After a reload taken at the boundary minute, the saved state then gives the same plan. Between the plan and the last unit, only fill units and the new persons' own first events run, and none of them changes the residents the census reads. A reload before, during or after the fill gives the same city (tests 3 and 4 below).
2. **The plan is never saved.** Since F5g adds no persisted setting, the latched targets live only in memory. A reload exactly at a boundary minute recomputes them from the saved state with the catalogue of that load. With an unchanged world this is the same plan. A world that changed between sessions applies at that boundary.
3. **Fill is attached by Runtime.** Core starts filling once `AttachPopulationFill` is called. Runtime calls it for every current-schema city, so filling is ordinary behaviour. The golden fixtures and older tests do not attach it, so their bytes are unchanged.
4. **Saved portals count as the stations.** The fill uses `CityState.Portals`, the stations saved when the first roster was created. Stations discovered later never add a portal (F5d rule), so a city saved without portals never fills.
5. **No new route cache.** Runtime's `WalkingRoutes` cache is the shared bounded cache for portal legs. It is keyed by endpoint positions, holds 1,024 entries and returns copies. A Core cache keyed by place pair would have shared one person's mode choice with another, because the planner's choice depends on each person's walking tolerance and fare weight. The offline tests use a model of that cache; native hit rates are batched for rig check 8.
6. **Day visitors carry an outside employer.** They follow the rule that unseated commuter workers already follow, so their income is the same kind as any outside worker's.
7. **The commuter target counts persons, not seated workers.** The plan counts each pattern's persisted persons against its target. A created commuter counts whatever its persona: a student is seated at a Study place, and a retiree or an unseated worker takes no seat. So Work places can stay below the census's 80% fill when commuters equal their target. Counting seated workers instead would be a different target and needs an owner decision.
8. **Research surveys wait for a pending boundary.** With the fill attached, a survey due after a day boundary runs after that boundary's fill, whatever the cadence.

## Offline evidence

New tests in `tests/F5gPopulationFillTests.cs` (20 cases), plus the opt-in harness `tests/F5gMeasure.cs`. The headings follow the task's acceptance list.

1. **Convergence and capacity.**
   - `FillConvergesToEachClampedTargetInBoundedUnitsWithoutOvershoot`: nothing is created before the boundary. At each of five boundaries each pattern equals `max(previous, target)` within the capacity. Units are at most 64 persons, and the occupancy index matches a recount.
   - `FillStopsExactlyAtTheAggregateCapacity`: demand above `2R` fills exactly `2R` commuters and no pool. A commuter target one or two below the capacity leaves exactly that many pool places.
   - `TargetClampsAtExactBoundariesAndWithoutStations`: demand at, one below and above the capacity, the pool formula, the 100,000 cap, no portal, and the weekend-weighted mean.
   - `NoDiscoveredPortalCreatesNoVisitor` and `WithoutAnAttachedCatalogueNothingIsFilled`, including a refused catalogue.
   - `FillTargetsEqualTheCensusOnTheSameFacts`: three seeds and three catalogues, in original and reversed input order.
2. **Above target and shrinking census.**
   - `FreshCitySeededAboveTargetCreatesNoCommutersAndRetiresNone`: a factor-2 city keeps all its seeded commuters and their wallets, creates no commuter, and its pool fills inside the remaining capacity. A factor-3 city at full capacity creates nobody.
   - `ShrinkingCensusKeepsAThousandCommutersAndPausesOnlyThatPattern`: 567 residents fill exactly 1,000 commuters plus the pool to the 1,134 capacity. The census then falls to 700. The new catalogue is attached at noon and changes nothing until the next boundary. After it, all 1,000 identities and wallets remain. The pool's own target (above its count) is held back by the aggregate capacity. A reload keeps everything.
3. **Determinism.**
   - `CreationOrderIdsAndDrawsAreIndependentOfBudgetsCadenceSkipReloadAndOrder`: the same city ends with identical saves and orders at budget 100,000, frame slices of 1 and 7, budget 64, a reload after every odd unit inside a boundary's fill, a reload each noon, and a shuffled catalogue.
   - `HiddenSkipCrossesEveryBoundaryAndEqualsDayByDay`: one call over eight days equals day-by-day advancing.
   - `ReloadExactlyAtTheBoundaryBeforeAndAfterTheFillGivesTheSameCity`: reloads at the boundary minute after the fill create nobody and end equal to the unreloaded city.
4. **Enrolment.**
   - `EveryCreatedPersonIsEnrolledOnceWithItsPortalDistrictMix`: with two districts, each wallet equals `HouseholdFinance.Create` with its portal district's mix, or the default mix. Every record is its own (slot 0, no excursion). A reload never re-seeds, and a later boundary enrols only new persons.
   - `FinancesOffEnrolsMealsWithoutWalletsAndAFoodlessCityCreatesNoVisitor`.
5. **Target timing.** `CatalogueChangesMoveTheTargetOnlyAtTheNextBoundaryAndAnExistingCityStartsFillingThere`: a current-schema city that ran two days without fill is reloaded with the catalogue. It starts filling at its next boundary with the same schema. A larger catalogue attached mid-day waits for the next boundary. One attached after the boundary latched leaves that boundary's targets alone.
6. **Four weeks of stays.** `FourWeeksOfStaysReturnTheSamePersonsAndConserveEverythingThroughReloads`: 29 days, checked every hour.
   - Reloads are taken at day-visitor arrivals, at meal outings (departure deferral), while a dormant visitor holds an uncertain receipt (lost acknowledgements, evidence withheld for two days of three), and each week at noon.
   - The run ends byte-identical to an unreloaded twin, with the same order list. The market double refuses any second order.
   - Pattern counts never decrease and grow only below their targets. Every person seen stays, with an unchanged pattern.
   - Dormant persons have no travelling, serving or decided meal, and their wallets post every day.
   - `CashConserved` holds for every wallet and the city-wide `BigInteger` ledger at every hour.
   - Most pool members return on two or more days, and pool presence per weekend day exceeds that per weekday.
   - `DayVisitDrawsFollowTheirSeededDays`: a pool member is present only on its drawn day, or finishing the previous day's stay.
   - `NoPoolMemberStaysDormantForGoodOverTenWeeks`: over 70 days with reloads, no pool member's next event is more than ten days ahead, and every member is active at least once.
7. **Transport and arrival.**
   - `PortalRoutesShareCachedGeometryButNeverRoutesAndUnreachablePortalsNeverArrive`: with the cache model, portal requests hit and miss, and no two persons share a route or leg list. The journey index matches a scan, and no visitor gets a lease. When every portal route is refused, the visitors stay dormant at their portals with no arrival, journey, purchase or order through a reload, and the wallets stay conserved.
   - `DayVisitorShopArrivalAdmitsServiceThroughReloadAndNeverFabricatesIt`: every day visitor served at a player shop holds a `Shop` arrival at that place at or after the meal's desired minute, across reloads.
8. **Writer 33.** `DayVisitorsRoundTripInWriter33AndMalformedDayVisitorsAreRefusedAndKept`: a city with day visitors round-trips byte-identically. A day visitor given an Employment is refused at write. A payload whose seated commuter is relabelled `D` is refused at read as `Invalid`, and the store keeps it and every other key. The current-writer goldens (`ChangeJournalTests.CurrentWriterGoldenSnapshotAndReload`, seeds 5150 and 9217) and the older-schema refusal (`OlderAttendancePayloadIsDistinctRefusalAndKept`) pass unchanged. No fixture or pin was edited.

Bug found and fixed on the way: the first version left visitor queue indices stale after a merge, so `TryBeginMealVisit` refused visitor meal outings until a reload rebuilt the queue. Reloaded cities then diverged from unreloaded ones from game minute 3,383. The slot check by ID fixed it (the determinism and four-week tests fail before and pass after).

Review round 1 (an independent review of the head) found:

- **r1-1, fixed.** A pool member whose drawn visit days all fell outside the routine's nine-day window got a Home span up to the clock limit (minute 1e9) and never woke again. That affected roughly a tenth of members at each re-evaluation. `NoPoolMemberStaysDormantForGoodOverTenWeeks` was red before the fix (`pv:24 sleeps until 1000000000`) and is green after it.
- **r1-2, fixed.** A research survey due after a day boundary, with no event in between, could run before that boundary's fill on a long advance but after it on a short one. The survey check now treats the pending boundary as the next work.
- **r1-3, documented, not changed.** The commuter target counts persons, as the plan states (decision 7 above).
- **r1-4, fixed.** The bounds are now checked before a unit seats anyone, so a refusal could no longer leave seats counted for persons that were never added. The case was unreachable under today's bounds.
- **r1-2 follow-up (round 2), fixed.** With the clock already on a boundary that still had fill work, the survey check looked only at the next boundary. Fill work left at the current boundary now counts as work at the current minute.
- **r1-5, fixed.** Doc wording: the test count, the caught-up wording, and the merge comment ("no copy" instead of "no allocation").

No existing test needed a changed expectation.

## Validation run

Offline only, on this Mac (Apple M1 Pro, .NET 10.0.400), 8 November 2026. Each `dotnet` command ran only when no other dotnet test or run process was active.

- `python3 tools/build.py --game-managed "<dll-import-shim>/Big Ambitions_Data/Managed"`: exit 0.
- `dotnet test tests/PersistentCity.Tests.csproj`: **3,917 passed, 0 failed, 0 skipped** (3 min 53 s), after the review round-2 fix.
- `tests/F5gMeasure.cs` (Release, opt-in): numbers in the perf file.

## Performance

Summary; the tables are in [docs/perf/f5g.md](../perf/f5g.md). Offline, Release, synthetic.

- A creation unit of 64 persons takes p95 3.87 ms at the reference load (2,532 persisted persons) and 1.46 ms at 10,017 persisted persons. The max is 7.52 ms, on the first unit of a boundary, which latches the plan. A unit allocates about 5 KB per person.
- The mod-work proxy p95 is at most 2.87 ms in every budget arm. No frame slice reached 30 ms. Flagged: the single hidden-skip call of 48 game hours (201 ms and 651 ms), which is one call for the whole skip, not a frame.
- Saves, orders and events are identical at budgets 1, 16 and 128 and in the skip. `HistoricalClockMisses` is 0.
- Portal route model: 2,792 hits and 16 misses at the reference load, and 13,068 hits and 28 misses at 10,017 persons. The busiest game minute needs 12 and 15 frames at 16 paths per frame, if each request is one path.

## In-game checks for the next batched rig

Every check runs on the headless agent rig ([PeterHartwieg/big-ambitions-agent-rig](https://github.com/PeterHartwieg/big-ambitions-agent-rig)) in a named disposable **FT-F5g** city or a disposable copy of a sealed fixture. The rig records the autosave setting, disables autosave in the isolated profile, checks it is off before each measured window (also after a reload) and restores it at cleanup. A save inside a window invalidates that sample. None of these checks is left to Peter, a playtest, a manual console session or a visual check by hand. Rig evidence and these offline results are reported separately.

1. **Growth to the clamped targets.**
   - Setup: a fresh FT-F5g city on the head build, saved at 22:00 on its first day.
   - Steps: read `pc population census` and `pc population manifest`. Run past midnight to 00:30, then read `pc population manifest` again.
   - Expected: no visitor is created before midnight. After it, commuters equal `max(seeded, commuter_target)` and the pool equals `min(pool target, visitor_capacity − commuters)`. The manifest's `commuter_target`, `pool_target` and `visitor_capacity` equal the census lines `commuter_target`, `day_visitor_pool_target` and `visitor_capacity` when the census stations equal the saved portals. `next_visitor_sequence − 1` equals the visitor count.
   - Rig automation: existing capabilities `rig flow run`, FT `time`/`save`/`console`; pending capability RIG-LIVE.
2. **Seeded above target and a shrinking census.**
   - Setup: check 1's city after its first fill, plus a copy in which the rig makes the player buy two large businesses (`shop_fixture`), so the census's commuter capacity falls.
   - Steps: on the copy, run to the next midnight and read the manifest before and after. Save, reload in a fresh process and read it again.
   - Expected: commuter count and visitor IDs are unchanged, with no visitor removed. `commuter_target` falls only at that midnight, and the pool grows only inside `visitor_capacity − visitors`. Wallet balances move only by ordinary postings and purchases.
   - Rig automation: existing capabilities `shop_fixture`, FT `time`/`save`/`load`/`console`; pending capabilities RIG-LIVE and RIG-PCSAVE (extract PC keys to compare visitor IDs and wallets).
3. **Reusable pools over four weeks.**
   - Setup: check 1's city.
   - Steps: run four game weeks with hidden skips between measured days. Save and reload at a day visitor's portal arrival, during a meal outing at its stay end, while a dormant visitor holds an uncertain receipt (a `shop_fixture` checkout the rig interrupts), and at the next arrival. Read `pc population manifest` and `pc population person <id>` for five pool members daily.
   - Expected: the same `pv:` IDs every day, and per-pattern counts never decrease. Each followed pool member is active only on some days, mostly at weekends, and the same members return in later weeks. Dormant visitors create no meal intents but post daily. The uncertain receipt resolves exactly once. `BigInteger` ledger totals conserve at every checkpoint.
   - Rig automation: existing capabilities `rig flow run`, FT `time`/`save`/`load`/`console`, `shop_fixture`, fixture library; pending capabilities RIG-LIVE and RIG-PCSAVE.
4. **No visitor bodies, cars or leases.**
   - Setup: check 1's city during a weekend afternoon with day visitors active.
   - Steps: move the camera with `camera look_at` along the street nearest a portal station and through a quiet district, sampling `screen_pos list` and the physical-population status line.
   - Expected: pedestrians ≤ 48 and cars ≤ 24, no `pv:` ID among bodies, leases or traffic records, and `journeyIndexWithheldVisitors` > 0 while pool members travel.
   - Rig automation: existing capabilities RIG-VIEW `camera`/`screen_pos`/`capture`, FT `console`; pending capability RIG-LIVE.
5. **Native sale authority with day visitors.**
   - Setup: check 1's city with a managed `shop_fixture` food shop near a portal, on a weekend day.
   - Steps: record `shop_log` purchases and PC receipts across an accounting hour and a midnight settlement. Save and reload while a day visitor's checkout is pending.
   - Expected: one native order per paid intent, day-visitor intents included, with one owner per intent. No duplicate or replayed sale. Wallet debits are exact, and native daily revenue equals PC confirmed receipts.
   - Rig automation: existing capabilities `shop_fixture`, `shop_log`, `shop_state`, FT `save`/`load`/`time`/`console`; pending capability RIG-LIVE.
6. **Arrival evidence and obligations through reload.**
   - Setup: check 5's city.
   - Steps: save at a day visitor's shop door (manifest shows it active, `pc population person` shows its arrival), reload in a fresh process, and follow the meal to its receipt.
   - Expected: service is admitted through the visitor's `Shop` arrival at that place, and the intent is ordered once. A visitor whose arrival names another place or an earlier minute is refused service, with no order.
   - Rig automation: existing capabilities FT `save`/`load`/`time`/`console`, `shop_log`; pending capability RIG-LIVE.
7. **Targets move only at day boundaries.**
   - Setup: check 1's city at 10:00 on a weekday.
   - Steps: make the player buy a business (`shop_fixture`), save and reload in a fresh process. Read the manifest at 10:30 and 23:50, then at 00:30.
   - Expected: `commuter_target` and the counts are unchanged until midnight. At 00:30 the boundary shows the new target, and creation follows check 1's rule.
   - Rig automation: existing capabilities `shop_fixture`, FT `time`/`save`/`load`/`console`; pending capability RIG-LIVE.
8. **Fill and portal route throughput.**
   - Setup: the reference fixture as a fresh head city, saved at 23:55 before its first fill, interleaved with base-build windows.
   - Steps: measure windows across midnight (the fill), the morning commute and a weekend afternoon, with the camera stationary and moving.
   - Expected: mod-work p95 < 5 ms in every head window, and every mod-work spike ≥ 30 ms flagged with its condition and component. The midnight window shows the fill's units and the held clock. Walking-cache hits and misses (`WalkingCacheHits`, `SubwayConnectorCacheHits`) and native path queries per frame stay ≤ 16. Whole-frame numbers are reported separately, with zero save callbacks inside a window.
   - Rig automation: existing capabilities `rig flow run` autosave guard, RIG-MODSET, FT `state`/`time`/`console`, RIG-VIEW `camera`; pending capabilities RIG-LIVE and RIG-PERF-UNION.
9. **Writer 33 stays and older cities are refused.**
   - Setup: check 3's city after four weeks, and a disposable FT city saved by a writer-32 build.
   - Steps: save and reload the first in a fresh process. Load the second with the head build and save natively.
   - Expected: the first has `"Schema":33` with `D` visitor tuples and loads without refusal. The second shows "created by an older development build; start a new city", and every `modData` key hash is unchanged after load and save.
   - Rig automation: existing capabilities RIG-MODSET, FT `load`/`save`/`state`/`console`; pending capabilities RIG-PCFIX (a sealed writer-32 fixture) and RIG-PCSAVE.
