Status: implemented (offline); not verified by rig

# F5h: integrated acceptance and measurement of the F5 population, 8 November 2026

Refs [#92](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/92). Base `21aea40` on `codex/persistent-city` (writer 34, F5a–F5g, F6i/F6j/F6k and F7d merged). Scope: the F5h row of [the F5 plan](../plans/f5-population.md#4-slice-breakdown), as re-scoped by the [no-migration decision](../decisions/2026-11-06-no-migration-rescope.md). No schema change: writer 34, its wire and its golden hashes are unchanged. Nothing here ran in the game or on the rig.

Performance: [docs/perf/f5h.md](../perf/f5h.md).

## What is implemented

- **Integrated acceptance tests** (`tests/F5hIntegratedAcceptanceTests.cs`, ordinary test project). They run F5a–F5g together on fresh writer-34 cities with the fill attached: the census catalogue, F5d schedules, F5e seating, F5f's journey index, F5g's fill and visitor transport, and the F6 household finances.
- **Measurement harness** (`tests/F5hMeasure.cs`, opt-in). It covers the section 6 matrix at the declared load and at 1,914, 10,000, 50,000 and 100,000 persons, and the full F1 matrix at 10,000 persons.
- **Panel and inspector words** for persons, households, workplaces and visitors (`Core/PopulationWords.cs`, `Runtime/FoodEconomyPanel.cs`, `Core/ResidentInsight.cs`). Words only; see below.
- **Two F5g tests ported to writer 34.** See "Defect found".

### The reference city

The declared load is the F5g fixture at 152 homes of seven beds: 957 residents, two portals, three player food shops, seed 2026. Its census catalogue gives the largest commuter target the even band capacities reach at or below 1,000, which is 999. The fill creates 1,024 persons at the first boundary and the remaining 551 at the second, ending at 957 residents, 999 commuters and 576 pool members: 2,532 persisted persons, inside the plan's 2,230–2,870 range.

### Words for persons, households, workplaces and visitors

The food panel (Ctrl+F8) keeps its headline, now built by `PopulationWords.Headline`: "Day 3 · 957 residents · 1575 visitors · 2532 people with meals". Those are the three counts the panel already printed. Below it, `PopulationWords.Lines` prints fixed sentences:

- "Residents who share a home form one household."
- "Residents and commuters take the seats at the city's workplaces. Residents without a workplace live on a pension or on support income."
- With visitors: "Visitors live outside the city and arrive and leave by subway. Commuters come on weekdays, usually to work; day visitors come to shop and for leisure, mostly at weekends. Visitors are not shown on the street yet."
- With no visitor and no saved portal: "No visitors come to this city: it has no subway station to arrive through."
- "Each person has their own wallet for food at home and prepared meals; residents also pay their bills from it."

These replace "Each consumer has a separate budget for bills, food at home and prepared meals." That sentence was wrong for visitors, who spend the money they arrive with and pay no bills under F6f's ordinary-exchange adapter. "Workplaces and travel patterns shape nearby demand." stays.

The resident inspector's person line now says "Resident; household of 3 sharing one home; employed since day 2." instead of "Resident; household of 3; employed since day 2.". "Lives alone", the study and employment words and the work role are unchanged. The inspector never opens on a visitor (`CitySimulator.Inspect` returns null for one), because visitors have no body before F5i, so there are no visitor words in it.

No new count, scan, counter or saved field. Every sentence states a rule from F5d–F5g or F6f.

### Technical decisions to review

1. **The seven-day run uses two cadences.** The uninterrupted city advances with a budget of 100,000 events per call. The reloaded one advances in frame slices of 7 events, so a boundary's fill of up to 16 units spans several slices and a save can land between units. Equality across cadences is part of what the test proves.
2. **Reloads use a new planner cache.** Each reload in the seven-day run starts a new `CachedPortalPlanner`, so equality also shows the geometry cache does not change outcomes.
3. **Lost acknowledgements.** One intent in 23 (keyed on the intent ID) throws after the native order is placed, so receipts go uncertain and reloads happen while they are open.
4. **The F1 window planner models the cache.** `AdmittedPortalPlanner` admits at most 16 native paths per frame, like F5g's `AdmittedPlanner`, and a request whose endpoint pair is in the 1,024-entry FIFO cache needs no native path. That gives portal hits and misses under admission in the same run.
5. **Runtime's frame shape for the size runs.** The F1 matrix arms count every unit against the event budget, as F5g's accounting test does. The size runs instead use `PopulationFrameBudget(1/60)`: 128 units, 16 citizen events, 2 ms, with only citizen events counted against the 16, as `PersistentCityMod` does.
6. **50,000 and 100,000 persons are factor-2 cities.** Filling a factor-1 city that large takes 33 to 66 game days. These sizes seed R commuters at creation, and the window's first boundary creates 1,024 pool members in 16 units. The perf file says so for each row.

## Offline evidence

### Integrated acceptance (ordinary test project, every run)

1. **Seven days with a save and reload at every day boundary.** `FreshReferenceCityRunsSevenDaysWithASaveAndReloadAtEveryDayBoundaryAndEqualsTheUninterruptedRun`.
   - A fresh reference city runs from 06:00 on day 1 for seven game days, across the boundaries at minutes 2,880 to 11,520.
   - It is saved and reloaded into a new simulator when the clock first stands on each boundary, and between two fill units at the first two.
   - It ends byte-identical to the uninterrupted run, with the same native order list (more than 1,000 orders), the same counts per boundary and at least one reload while a receipt was uncertain.
   - The save is writer 34 (`{"Schema":34,`).
2. **Conservation at every checkpoint.** In both runs, every hour (168 checkpoints): `CashConserved` for every wallet, the city-wide `BigInteger` ledger (opening balance, income and employment income equal balances, essentials, home food, prepared food, external food, discretionary spending and held purchases), and F5g's structural invariants. Those are one roster record and wallet per person, ordinal visitor lists, no car or car leg for a visitor, and only student or retiree identities at skipped sequence numbers. Also `HistoricalClockMisses` 0, `PopulationFillRefusals` 0, and the occupancy index equal to a recount at the end.
3. **Fill bounds.** In both runs and at each boundary:
   - Per-pattern counts never decrease, and every visitor seen stays with its pattern.
   - Commuters plus pool never pass `VisitorCapacity`.
   - A boundary creates at most 1,024 persons.
   - Neither pattern grows past its clamped target. A boundary below its cap reaches both targets exactly.
   - Every commuter has a working persona (the F5g owner decision, unchanged).

   `PopulationFillConvergesToTheCensusTargetsAtTheDeclaredLoadWithoutOvershoot` adds:
   - the first boundary creates exactly 1,024 persons, the second reaches the commuter target and the clamped pool target, and the later five create nobody;
   - the capacity equals the census's (`PopulationCensus.Compute` on the same facts), which is twice the 957 residents;
   - the pool holds students or retirees;
   - no visitor holds a car;
   - the portal cache has more hits than misses.
4. **Older-schema refusal.** `TheIntegratedCityWrittenByTheOlderWriterIsRefusedWithStartANewCityAndKept`: the seven-day city written by the frozen writer 33 is refused as `DevelopmentSchema`, with "start a new city" and without "Invalid". `EconomicLedgerTests.Refused` then checks the store and the session: a fresh city cannot overwrite the payload, and it and every other key stay byte for byte. The writer-34 payload of the same city loads and rewrites identically.
5. **Golden hash unchanged.** `CurrentWriterGoldenHashIsUnchanged`: `CurrentSchema` is 34, and both F7d pins (seeds 5150 and 9217, hashes `a3b70408…` and `cb39d793…`, 73,192 and 72,830 characters) hold. No fixture or pin was edited.
6. **F1 matrix at the declared load.** `DeclaredLoadF1MatrixEqualsTheReferenceAcrossBudgetsCadencesSkipAndReload`, four arms (30 FPS / 32 / 4 ms, 60 / 16 / 2, 120 / 128 / 2, hidden skip 32 / 4).
   - Window: from 06:00 on day 2 (957 residents and the first boundary's 1,024 visitors) to 06:00 on day 3, across the boundary that completes the fill.
   - Each arm runs without saves and with a save at midnight and one between fill units.
   - Every run equals the 60 / 128 / 2 reference in save and orders, with `HistoricalClockMisses` 0, lag within `AccountingClock.MaximumLagMinutes`, at most 16 native paths per frame and the same number of persons created.

The 9 acceptance cases take about 25 s in Debug.

### F1 matrix at 10,000 persons (opt-in)

`F5hMeasure.MeasureStressMatrix`: a 530-home city filled to 9,483 persons, then the day across its seventh boundary (534 more, 10,017 in all). All eight arm and save combinations equal the reference (state hash `6a3b63e2785913ee`), with `HistoricalClockMisses` 0, lag at most 1 minute and at most 16 paths per frame. It is opt-in because it builds a seven-day city first. The two opt-in matrices together take about 30 s in Release. Numbers in the perf file, section 3.

### Wording (ordinary test project)

`tests/F5hWordingTests.cs`, 3 cases:

- `PanelHeadlineShowsOnlyTheNumbersThePanelAlreadyHad`: the headline text, singulars, and that its only digits are the four values passed in.
- `PanelLinesDescribeHouseholdsWorkplacesAndVisitorsInWordsWithoutCounts`: which sentences appear with visitors, without a portal, and with a portal but no visitors yet. No line holds a digit, an ID or a key.
- `InspectorNamesTheSharedHomeAndNeverSelectsAVisitor`: on a filled F5g city, every resident's person line names a shared home ("household of N sharing one home") or "lives alone", plus the employment words, and never a home ID. `Inspect` returns null for every visitor. The panel headline on that city prints the residents, visitors and people with meals the panel printed before.

`Schema26Tests.InspectorShowsKindHouseholdAndEmploymentWordsWithoutIds`, `ResidentInsightTests` and `A5IntegratedAcceptanceTests` pass unchanged.

### Defect found: two F5g tests still on writer 33

The full suite at the base failed two F5g tests. `DayVisitorsRoundTripInWriter33AndMalformedDayVisitorsAreRefusedAndKept` asserted `CurrentSchema == 33`. `SkippedCommuterNumbersAreOnlyNonWorkingIdentitiesOnRead` parsed the save as JSON. F5g (#154) merged after F7d (#158) made writer 34 the compact wire, so both failed (red in this tree before the fix: `Expected: 33, Actual: 34`, and a JSON parse error at line 662). Only the tests were wrong. The fix is local to the tests:

- they assert the current writer;
- they edit the compact payload's body through `SaveWire.Body`/`SaveWire.Payload`, as the other post-F7d tests do;
- they keep every refusal check (a lost working-persona number refused, a forged sequence refused as `Invalid` with "not a student or retiree", a seated commuter relabelled as a day visitor refused as `Invalid` and kept through the store and the session).

The product code is unchanged, and all 27 F5g fill tests pass after the fix. The test name still says writer 33, to match the F5g checks file.

No defect in F5a–F5g product behaviour came out of the integrated run.

## Validation run

Offline only, on this Mac (Apple M1 Pro, .NET 10.0.400), 8 November 2026. Each `dotnet` command ran only when no other dotnet test process was active.

- `python3 tools/build.py --game-managed "<dll-import-shim>/Big Ambitions_Data/Managed"`: exit 0.
- `dotnet test tests/PersistentCity.Tests.csproj`: **4,093 passed, 0 failed, 0 skipped** (5 min 36 s). Before the F5g test port the same suite had 2 failures, both listed under "Defect found".
- `tests/F5hMeasure.cs` (Release, opt-in): numbers in the perf file.

## Performance

Summary; the tables are in [docs/perf/f5h.md](../perf/f5h.md). Offline, Release, synthetic.

- **Mod-work p95** per frame in Runtime's 60 FPS frame shape: 0.437 ms at the declared load (2,532 persons), 0.151 ms at 1,914 → 2,490, 0.144 ms at 10,017, 0.143 ms at 50,000 and 0.220 ms at 99,996. Under 5 ms everywhere.
- **Flagged ≥ 30 ms:** a 35.9 ms citizen unit at 99,996 persons (06:20, a gen-1 .NET GC inside it) and a 33.8 ms citizen unit at 10,017 persons in the 30 / 32 / 4 matrix arm (a gen-2 GC inside it). Every unit of 5 ms or more in every run had a .NET GC inside it.
- **Throughput:** a game day takes 1.29 times the frames of an unheld clock at the declared load and 2.32 times at 10,017 persons. 50,000 and 100,000 persons do not keep pace (13.2 and 26.8 times). Lag stays at most 0.5 minutes and no hour is missed.
- **Fill units:** p50 0.15–0.38 ms, max 7.8 ms (one unit at 99,996 persons). About 300 KB allocated per 64-person unit.
- **Saved state:** 1,083,570 characters and 21,454 records at the declared load, 26,156,631 characters (9.7% of the cap) and 426,674 records at 99,996 persons.
- **No perf change** and no change to the parked hotspots.

## In-game checks for the next batched rig

Every check runs on the headless agent rig ([PeterHartwieg/big-ambitions-agent-rig](https://github.com/PeterHartwieg/big-ambitions-agent-rig)) in a named disposable **FT-F5h** city or a disposable copy of a sealed fixture, never in Peter's ordinary save. The rig records the autosave setting, disables autosave in the isolated profile, checks that it is off before each measured window (also after a reload) and restores it at cleanup. A save inside a measured window invalidates that sample. None of these checks is left to Peter, a playtest, a manual console session or a visual check by hand. Rig evidence and the offline results above are reported separately.

**Locked PC** means the check times frames, so the rig holds the machine exclusively (no other session, no input from anyone else) for the measured windows. **Input only** means the check drives the game through rig input and console commands and reads state, with no timing claim, so it needs no lock.

1. **R8: performance at the declared load (locked PC).**
   - Setup: a fresh FT-F5h city on the head build. Run it to day 3 so the fill has reached its targets, check `pc population manifest`, and save. Use the same fixture on the base build for interleaved baseline windows.
   - Steps: alternate base and head windows at idle, the morning commute (07:00–09:00), lunch and the evening, with the camera stationary and then moving (`camera look_at` along a dense street), with the Ctrl+F8 panel closed and then open. Record mod-work p50/p95/max and its components (events, postings, planning, fill, selection), allocations and GC collections per window, lag and holds. Record whole-frame metrics separately.
   - Expected: head mod-work p95 < 5 ms in every window. Every mod-work spike ≥ 30 ms flagged with count, condition, component and whether a GC ran in it. Bodies ≤ 48 pedestrians and ≤ 24 cars, with no `pv:` ID among them. Whole-frame numbers reported separately and never used for the gate. Zero save callbacks inside a window.
   - Rig automation: existing capabilities `rig flow run` (autosave guard), RIG-MODSET, FT `state`/`time`/`console`/`shoot`, RIG-VIEW `camera`; pending capability RIG-LIVE.
2. **R8: performance at the 10,000-person stress load (locked PC).**
   - Setup: the rig-built stress city of about 10,000 persisted persons on the head build, saved after its fill has stopped growing (a midnight that creates fewer than 1,024 persons), and the same city on the base build.
   - Steps: the windows and camera paths of check 1, plus a window across a midnight with fill work left, if the fixture has any.
   - Expected: as check 1. The midnight window shows at most 16 fill units held on the boundary minute and no unit of 30 ms or more without a flag. The F1 lag stays ≤ 1 game minute. Record how many frames the game day takes against the frames of an unheld clock, so it can be compared with the offline 2.32.
   - Rig automation: as check 1; pending capability RIG-PCFIX (stress fixture).
3. **R7: event budgets and frame caps (locked PC).**
   - Setup: check 1's day-2 checkpoint, saved at 06:00 before the second fill boundary.
   - Steps: run one logical day per arm from the same checkpoint: the PC event-budget override at its minimum and at its maximum (developer config), frame caps 30, 60 and 120, and a hidden skip. In one more arm, save and reload in a fresh process at midnight and again while the fill's units are running.
   - Expected: identical end-of-day manifest hashes (`pc population manifest`, PC state extract) in every arm, `HistoricalClockMisses == 0`, lag ≤ 1 game minute, and at most 16 native path queries per frame.
   - Rig automation: existing capabilities `rig flow run`, FT `time`/`save`/`load`/`console`; pending capabilities RIG-PCCONFIG, RIG-FRAMECAP, RIG-LIVE.
4. **Seven days with a save and reload each day (input only).**
   - Setup: a fresh FT-F5h city on the head build, saved at 06:00 on day 1, and a copy of it.
   - Steps: run the copy for seven game days with hidden skips. Run the original for the same seven days, saving natively and reloading in a fresh process at 00:00 each day (the clock held on the boundary) and once between two fill units on the first night. Read `pc population manifest` after each midnight in both.
   - Expected: both cities end with equal PC state extracts and native order lists. Per-pattern counts never decrease and never pass `visitor_capacity`. After the second midnight both patterns equal their targets, and later midnights create nobody unless the census moved. `BigInteger` ledger totals conserve at every daily checkpoint.
   - Rig automation: existing capabilities `rig flow run`, FT `time`/`save`/`load`/`console`; pending capabilities RIG-LIVE and RIG-PCSAVE (extract PC keys to compare).
5. **Older-schema city refused and kept (input only).**
   - Setup: a named FT city written by a writer-33 build (pending capability: load a named older-schema FT city and read the refusal status).
   - Steps: load it in the head build, read the Persistent City status line and the save's PC keys, save natively, and reload.
   - Expected: the distinct "start a new city" status, not a corruption message. The PC payload and every other PC key stay byte-identical across the native save. PC-owned food businesses are in their defined native state.
   - Rig automation: existing capabilities FT `load`/`save`/`console`; pending capabilities the older-schema FT loader and RIG-PCSAVE.
6. **Panel and inspector words (input only).**
   - Setup: check 4's city on day 3 at 12:00, with visitors present, and a disposable FT city whose saved state has no portal.
   - Steps: open the Ctrl+F8 panel and capture it. Open the resident inspector on a resident in a shared home and on one who lives alone, and capture both.
   - Expected: the headline shows the day, residents, visitors and people with meals, with visitors plus residents equal to people with meals. The household, workplace, visitor and wallet sentences appear as listed under "Words for persons, households, workplaces and visitors". In the no-portal city the no-station sentence appears instead of the visitor one. The inspector says "household of N sharing one home" or "lives alone", and no inspector opens on a visitor.
   - Rig automation: existing capabilities RIG-VIEW `camera`/`capture`, FT `console`/`shoot`; pending capability RIG-LIVE (click a body for the inspector).
