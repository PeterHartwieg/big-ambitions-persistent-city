Status: implemented (offline); not verified by rig.

# City audit F5g: population fill and visitor transport in writer 33, 8 November 2026

Refs [#92](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/92). Base `3f734cb` on `codex/persistent-city` (writer 33, F6i #150 merged). Scope: the F5g row of [the F5 plan](../plans/f5-population.md#4-slice-breakdown), as re-scoped by the [no-migration decision](../decisions/2026-11-06-no-migration-rescope.md). No schema bump. Nothing here ran in the game or on the rig.

Performance: [docs/perf/f5g.md](../perf/f5g.md).

## What is implemented

- **Fill at day boundaries** (`Core/PopulationFill.cs`, `CitySimulator.AdvanceCore`). Once every event before a day boundary has run and the target reaches the boundary, the scheduler stages the boundary's new persons in units of at most 64 (`PopulationFillPolicy.MaximumPersonsPerUnit`). Each unit counts as one event against the frame budget. Staged persons are built and seated but are not in the city. The clock then crosses the boundary in one publication step: the persons enter the city, the clock moves onto the boundary, and the first midnight's capability activation happens. That step runs before any event at the boundary minute. One boundary publishes at most 1,024 persons (`MaximumPersonsPerBoundary`), and a larger deficit fills over later days (review Sol-r2-2). Runtime advances one slice per step and checks its frame time budget between steps. With the fill attached, the accounting-observation step leaves a day boundary to this publication instead of jumping the clock onto it. There is one scheduler and one event queue. Postings use the existing F3 units and F1 drain.
- **Targets come from the census formula.** `VisitorTargets.Compute` is now the single formula behind both `PopulationCensus.Compute` and the fill. `VisitorCapacity` is `min(2 × Residents, 100,000)`. Commuters are clamped first, then the pool takes the capacity left. Without a portal both targets are zero.
  - The fill reads Runtime's load-time catalogue (`PopulationFillCatalogue`: places and business facts, as F5a's census reads them), the residents' current assignments and the saved portals.
  - The plan for a boundary is latched when its staging starts and is never saved. Whether a boundary is done follows from the clock alone: the clock passes a boundary only by its publication. A save during staging stores the city as it was before the boundary, and the reload stages the same persons again from that state. The save itself does no fill work. A boundary the city was saved, constructed or first attached on is already crossed, so a catalogue change, a first attachment or a reload takes effect at the next boundary (review Sol-r1-1).
  - Runtime attaches the catalogue to every current-schema city after the food market (`PersistentCityMod`). A catalogue the census refuses detaches the fill with a log line and creates nothing.
- **Growth and retention.** A person of pattern P is created only while P's persisted count (active and dormant) is below its target and the aggregate visitor count is below `VisitorCapacity`. Commuters come first, then pool members. Nothing clamps, deletes or retires a persisted person, wallet or obligation. A city seeded above a target keeps everyone and pauses only that pattern. Other patterns fill inside the aggregate capacity that remains. Persons ≤ 150,000 and visitors ≤ 100,000 are checked before each unit seats anyone. If one is ever reached (the clamp makes that unreachable), that boundary's fill ends and `PopulationFillRefusals` counts it, without stopping the simulation (review Opus-r1-4). A city without portals gets no portal and no visitor.
- **Identity and determinism.** Each person gets the next `pv:<n>`. Portal, identity, venues, seat and carried income are drawn by the same keys as F5d's fresh roster: `VisitorSchedules.Create` and the fill share `VisitorFactory`. New persons are merged into the ordinal `Visitors`/`VisitorRoutines` lists in one backward pass (`PersonTable.InsertVisitors`). IDs are never reused.
- **Working-persona commuters** (owner decision, 8 November 2026). The fill creates only working-persona commuters. Each one takes a Work seat when one is free, so meeting the commuter target fills Work places to the census's 80%. Pool members can be any persona. Identities are derived from `(seed, pv:<n>)` by the frozen version-1 policy, so for a commuter the fill skips any sequence number whose identity is a student or retiree. That number is used up and never reused. One commuter looks at most 4,096 numbers ahead, and a city holds at most 400,000 skipped numbers (`PopulationFillPolicy`). Past either bound, commuter creation pauses for that boundary without using up a number. The reader accepts a missing number below `NextVisitorSequence` only if its derived identity is a student or retiree (`VisitorSchedules.OnlyNonWorkingSkipped`). Any other missing number is a lost person and refuses the payload, as the count equality did before. F5d's fresh-roster seeding is unchanged, so its seeded commuters keep any persona and every current-writer golden stays as it was.
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

1. **The fill is published as the clock crosses the boundary, before the boundary minute's own events.** Its plan reads the state the previous day left. No event runs between the plan, the staging units and the publication. The new persons' own events at the boundary minute then run in the ordinary (minute, ID) order. Resident employment checks run from one minute after midnight.
2. **Staging is not saved; publication is one bounded step.** F5g adds no persisted setting. A save during staging stores the pre-boundary city, and the reload stages again from it, so a save never runs fill work (`ASaveWhilePersonsAreStagedStoresThePreBoundaryCityAndTheReloadStagesThemAgain`). The cost is the cap. One publication links at most 1,024 prebuilt persons into the city (perf file, section 1), so a large first fill takes several days: about 7 days to 10,000 persons, and up to 98 days for a city filling 100,000 visitors from empty. A reload during staging with a changed catalogue stages that boundary with the new catalogue, because the boundary has not been crossed.
3. **Fill is attached by Runtime.** Core starts filling once `AttachPopulationFill` is called. Runtime calls it for every current-schema city, so filling is ordinary behaviour. The golden fixtures and older tests do not attach it, so their bytes are unchanged.
4. **Saved portals count as the stations.** The fill uses `CityState.Portals`, the stations saved when the first roster was created. Stations discovered later never add a portal (F5d rule), so a city saved without portals never fills.
5. **No new route cache.** Runtime's `WalkingRoutes` cache is the shared bounded cache for portal legs. It is keyed by endpoint positions, holds 1,024 entries and returns copies. A Core cache keyed by place pair would have shared one person's mode choice with another, because the planner's choice depends on each person's walking tolerance and fare weight. The offline tests use a model of that cache; native hit rates are batched for rig check 8.
6. **Day visitors carry an outside employer.** They follow the rule that unseated commuter workers already follow, so their income is the same kind as any outside worker's.
7. **Commuters are working personas** (owner decision, 8 November 2026, review Opus-r1-1). This replaces the earlier "persons, not seated workers" decision: the published `commuter_target` stays the work gap, and only working-persona commuters are created. The cost is skipped sequence numbers and a relaxed count rule in the reader, limited to numbers whose identity is a student or retiree. That rule is weaker than the old count equality: a lost visitor whose identity is a student or retiree would pass as a skipped number. Changing the frozen identity draw instead would have changed existing cities' identities on load.
8. **Research surveys wait for a pending boundary.** With the fill attached, a survey due after a day boundary runs after that boundary's fill, whatever the cadence.

## Offline evidence

New tests in `tests/F5gPopulationFillTests.cs` (23 cases) and `tests/F5gAccountingTests.cs` (4 theory cases and an opt-in measurement), plus the opt-in harness `tests/F5gMeasure.cs`. The headings follow the task's acceptance list.

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
   - `CreationOrderIdsAndDrawsAreIndependentOfBudgetsCadenceSkipReloadAndOrder`: the same city ends with identical saves and orders at budget 100,000, frame slices of 1 and 7, budget 64, a save and reload while each boundary's persons are staged, a reload each noon, and a shuffled catalogue.
   - `HiddenSkipCrossesEveryBoundaryAndEqualsDayByDay`: one call over eight days equals day-by-day advancing.
   - `ReloadExactlyAtTheBoundaryBeforeAndAfterTheFillGivesTheSameCity`: reloads at the boundary minute after the fill create nobody and end equal to the unreloaded city.
   - `ASaveWhilePersonsAreStagedStoresThePreBoundaryCityAndTheReloadStagesThemAgain`: a save after the first staging unit holds no `pv:` person and a clock before the boundary. The reloaded city equals the uninterrupted one, orders included.
   - `ABoundaryPublishesAtMostItsCapAndALargerDeficitFillsOverLaterDays`: 1,024 persons on the first day and the remaining 110 on the second.
   - `CapabilitiesActivateAtTheFirstMidnightWithTheFillAttached`: with the fill attached, the city's capabilities activate at the first midnight through `AdvanceTo` and through frame slices, as without the fill (review Sol-r2-1; red before the fix: `AdvanceTo 2881`).
   - `F5gAccountingTests.MidnightFillHoldsTheClockAdmitsRoutesAndEqualsTheReferenceAcrossCadencesAndSaves`: the accounting scenario described under PR review round 1 (Astra-r1-1).
4. **Enrolment.**
   - `EveryCreatedPersonIsEnrolledOnceWithItsPortalDistrictMix`: with two districts, each wallet equals `HouseholdFinance.Create` with its portal district's mix, or the default mix. Every record is its own (slot 0, no excursion). A reload never re-seeds, and a later boundary enrols only new persons.
   - `FinancesOffEnrolsMealsWithoutWalletsAndAFoodlessCityCreatesNoVisitor`.
5. **Target timing.**
   - `ACompletedBoundaryStaysClosedAcrossAttachmentAndReload`: after the fill at 2880, a larger catalogue changes nothing at 2880 either in process or after a reload of the 2880 checkpoint. Both cities then grow at 4320 and end equal. An existing city saved at exactly 2880 without the fill and first attached there fills at 4320.
   - `CatalogueChangesMoveTheTargetOnlyAtTheNextBoundaryAndAnExistingCityStartsFillingThere`: a current-schema city that ran two days without fill is reloaded with the catalogue. It starts filling at its next boundary with the same schema. A larger catalogue attached mid-day waits for the next boundary. One attached after the boundary latched leaves that boundary's targets alone.
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
8. **Working-persona commuters and skipped numbers.** `FillConvergesToEachClampedTargetInBoundedUnitsWithoutOvershoot` checks:
   - every fill-created commuter has a working persona, while the pool holds students or retirees;
   - the skip counter equals the gaps.

   `SkippedCommuterNumbersAreOnlyNonWorkingIdentitiesOnRead`: a city with skipped numbers round-trips. Dropping a created commuter is refused at write and at read. A `NextVisitorSequence` raised to open a working identity's number is refused as `Invalid` ("not a student or retiree"). `Invariants` checks at every test checkpoint that every missing number is a non-working identity.
9. **Writer 33.** `DayVisitorsRoundTripInWriter33AndMalformedDayVisitorsAreRefusedAndKept`: a city with day visitors round-trips byte-identically. A day visitor given an Employment is refused at write. A payload whose seated commuter is relabelled `D` is refused at read as `Invalid`, and the store keeps it and every other key. The current-writer goldens (`ChangeJournalTests.CurrentWriterGoldenSnapshotAndReload`, seeds 5150 and 9217) and the older-schema refusal (`OlderAttendancePayloadIsDistinctRefusalAndKept`) pass unchanged. No fixture or pin was edited.

Bug found and fixed on the way: the first version left visitor queue indices stale after a merge, so `TryBeginMealVisit` refused visitor meal outings until a reload rebuilt the queue. Reloaded cities then diverged from unreloaded ones from game minute 3,383. The slot check by ID fixed it (the determinism and four-week tests fail before and pass after).

Review round 1 (an independent review of the head) found:

- **r1-1, fixed.** A pool member whose drawn visit days all fell outside the routine's nine-day window got a Home span up to the clock limit (minute 1e9) and never woke again. That affected roughly a tenth of members at each re-evaluation. `NoPoolMemberStaysDormantForGoodOverTenWeeks` was red before the fix (`pv:24 sleeps until 1000000000`) and is green after it.
- **r1-2, fixed.** A research survey due after a day boundary, with no event in between, could run before that boundary's fill on a long advance but after it on a short one. The survey check now treats the pending boundary as the next work.
- **r1-3, superseded.** The commuter-persona question went to the owner, who decided working-persona commuters only (decision 7 above; PR review Opus-r1-1).
- **r1-4, fixed.** The bounds are now checked before a unit seats anyone, so a refusal could no longer leave seats counted for persons that were never added. The case was unreachable under today's bounds.
- **r1-2 follow-up (round 2), fixed.** With the clock already on a boundary that still had fill work, the survey check looked only at the next boundary. Fill work left at the current boundary now counts as work at the current minute.
- **r1-5, fixed.** Doc wording: the test count, the caught-up wording, and the merge comment ("no copy" instead of "no allocation").

PR review round 1 (Sol, Astra, Opus) found:

- **Sol-r1-1, fixed.** A completed boundary reopened when a city was reloaded or first attached at that boundary minute. A larger catalogue attached after the fill at 2880 then created 106 more visitors on reload, and an existing city first attached at 2880 filled at 2880 instead of 4320. Round 1 opened boundaries only when the clock reached them in-process and had the save complete an open fill. Round 2 replaced that with staging and publication (Sol-r2-2), so the save does no fill work. Regression test: `ACompletedBoundaryStaysClosedAcrossAttachmentAndReload` (both reproduced cases).
- **Astra-r1-1, fixed.** New `F5gAccountingTests` runs the F1 accounting scenario with the fill attached:
  - a native-clock double held by `AccountingClock.Limit` and published by `BeginAccountingObservations`;
  - a market that refuses a checkout outside the current native hour;
  - a resumable planner double that admits at most 16 native paths per frame.

  It covers 30/1/0.01 ms, 60/16/0.5 ms, 120/128/2 ms and a 120-minute skip, each with a save while midnight's persons are staged and a save right after the midnight publication. Every arm equals the unreloaded reference in save and orders, with `HistoricalClockMisses` 0, lag ≤ 1 minute and at most 16 paths per frame. The perf file reports the measured holds, deferrals and lag.
- **Opus-r1-1, fixed.** Owner decision: working-persona commuters only (decision 7).
- **Opus-r1-2, fixed.** Rig checks 2 and 7 are corrected. The manifest shows targets only after a boundary has latched them, and the catalogue is read at load, so a purchase moves the target only after a save, a fresh-process reload and the next midnight.
- **Opus-r1-3, fixed.** The perf file measures a fill boundary at the resident bound (49,996 residents, capacity 99,992 visitors). Round 2 replaced the single 1,563-unit first fill with boundaries of at most 1,024 persons.
- **Opus-r1-4, fixed.** A bounds refusal ends the boundary's fill with a counter instead of throwing.

PR review round 2 (Sol, Astra, Opus) found:

- **Sol-r2-1, fixed.** The round-1 clock stop at a day boundary hid the first midnight's capability activation, so food experience and memory stayed off. Publication now performs it. Covered by `CapabilitiesActivateAtTheFirstMidnightWithTheFillAttached` for `AdvanceTo` and frame slices. The accounting scenario's arms, which use the observation path, end equal to the reference.
- **Sol-r2-2 (also Astra-r2-1, Opus-r2-1), fixed.** The save no longer completes the fill. `CompleteFillForSave` is gone, along with its Runtime call. Staging keeps all creation work on the bounded scheduler, and the per-boundary cap bounds the publication step. Reload continuation after a partial unit sequence is covered by the staging-save test and the accounting scenario. The save path does no fill work, so there is nothing to measure there; the publication step is measured in the perf file.
- **Opus-r2-2, fixed.** The `ValidatePersons27` summary is back above its method.
- **Opus-r2-3, fixed (docs).** The reader's gap rule is weaker than the old count equality: a lost visitor whose identity is a student or retiree cannot be told from a skipped number. That is stated in decision 7 and in the reader's comment. The perf file no longer says the skip draws build no strings; each draw builds one `pv:<n>` string.

Changed existing expectations: none. `Schema27Tests` still refuses a `NextVisitorSequence` raised past the count, because the number it opens derives a working identity.

## Validation run

Offline only, on this Mac (Apple M1 Pro, .NET 10.0.400), 8 November 2026. Each `dotnet` command ran only when no other dotnet test or run process was active.

- `python3 tools/build.py --game-managed "<dll-import-shim>/Big Ambitions_Data/Managed"`: exit 0.
- `dotnet test tests/PersistentCity.Tests.csproj`: **3,929 passed, 0 failed, 0 skipped** (10 min 7 s on a loaded machine), after the PR review round-2 fixes.
- `tests/F5gMeasure.cs` (Release, opt-in): numbers in the perf file.

## Performance

Summary; the tables are in [docs/perf/f5g.md](../perf/f5g.md). Offline, Release, synthetic.

- **Staging.** A unit of 64 persons takes p95 0.10 ms at the reference load (2,532 persisted persons) and 1.32 ms at 10,017 persons. The max is 22.9 ms, on the first unit of a boundary at the resident bound, which latches the plan with a scan of about 100,000 persons.
- **Publication.** A boundary publishes at most 1,024 persons in one step: 4.4 ms at the reference load, at most 23.2 ms at 10,017 persons, and 5.5 ms when merging into about 50,000 visitors. No staging unit or publication reaches 30 ms.
- **Fill time.** The reference city fills in 2 boundaries and the stress city in 7. A city at the resident bound fills from empty in about 98.
- **Mod-work proxy.** On the measured day at the full population, p95 is at most 2.94 ms in every budget arm. Flagged ≥ 30 ms: only the single hidden-skip calls (119 ms and 430 ms), each one call for a whole skipped day, not a frame.
- **Equality.** Saves, orders and events are identical at budgets 1, 16 and 128 and in the skip, the fill days included. `HistoricalClockMisses` is 0.
- **Accounting scenario** (perf section 4). In every arm: lag ≤ 1 minute, zero missed hours, at most 16 paths per frame, no frame at or above 30 ms, and identical results with saves during staging and after publication. The save does no fill work.
- **Portal route model.** 5,558 hits and 32 misses at the reference load, and 32,538 hits and 168 misses at 10,017 persons.

## In-game checks for the next batched rig

Every check runs on the headless agent rig ([PeterHartwieg/big-ambitions-agent-rig](https://github.com/PeterHartwieg/big-ambitions-agent-rig)) in a named disposable **FT-F5g** city or a disposable copy of a sealed fixture. The rig records the autosave setting, disables autosave in the isolated profile, checks it is off before each measured window (also after a reload) and restores it at cleanup. A save inside a window invalidates that sample. None of these checks is left to Peter, a playtest, a manual console session or a visual check by hand. Rig evidence and these offline results are reported separately.

1. **Growth to the clamped targets.**
   - Setup: a fresh FT-F5g city on the head build, saved at 22:00 on its first day.
   - Steps: read `pc population census` and `pc population manifest`. Run past midnight to 00:30, then read `pc population manifest` again.
   - Expected: no visitor is created before midnight. After it, commuters equal `max(seeded, commuter_target)` and the pool equals `min(pool target, visitor_capacity − commuters)`. The manifest's `commuter_target`, `pool_target` and `visitor_capacity` equal the census lines `commuter_target`, `day_visitor_pool_target` and `visitor_capacity` when the census stations equal the saved portals. `next_visitor_sequence − 1` minus the visitor count equals the skipped numbers, and every commuter the fill created has a working persona (`pc population person <id>` for five of them). Before midnight the manifest has no `boundary=` line, because targets appear only after a boundary latches them.
   - Rig automation: existing capabilities `rig flow run`, FT `time`/`save`/`console`; pending capability RIG-LIVE.
2. **Seeded above target and a shrinking census.**
   - Setup: check 1's city after its first fill, plus a copy in which the rig makes the player buy two large businesses (`shop_fixture`), so the census's commuter capacity falls.
   - Steps: on the copy, buy the businesses, save natively and reload in a fresh process, since the catalogue is read at load. Then run to the next midnight and read the manifest before and after it. Save, reload in a fresh process again and read it a third time.
   - Expected: commuter count and visitor IDs are unchanged, with no visitor removed. The manifest has no target line between the reload and midnight. After midnight `commuter_target` is lower than check 1's, and the pool grows only inside `visitor_capacity − visitors`. Wallet balances move only by ordinary postings and purchases.
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
   - Steps: make the player buy a business (`shop_fixture`), save and reload in a fresh process. Read the manifest at 10:30 and 23:50, then at 00:30. Also save while the midnight fill is staging (the clock held just before 00:00), reload in a fresh process, and read the manifest after midnight.
   - Expected: until midnight the counts are unchanged and the manifest has no `boundary=` line, because the reload cleared the in-memory plan and targets appear only when a boundary latches them. At 00:30 the `boundary=` line shows the new catalogue's target, and creation follows check 1's rule. The save during staging holds no new visitors. After the reload the boundary stages again, and the counts after midnight equal those of the uninterrupted fill. A save taken after midnight and reloaded runs no second fill at that midnight.
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
