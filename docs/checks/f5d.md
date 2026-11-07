Status: implemented (offline); not verified by rig

# City audit F5d — independent schedules in writer 27, 7 November 2026

Refs [#92](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/92). Base `30a9de7` on `codex/persistent-city` (writer 26, F5c-lite #127 and C5d #136 merged). Scope: the F5d row of [the F5 plan](../plans/f5-population.md#4-slice-breakdown) as re-scoped by the [no-migration decision](../decisions/2026-11-06-no-migration-rescope.md), with the section 9 defaults Peter accepted ([design defaults](../decisions/2026-11-06-design-defaults.md)): visitors come by subway and walk, own no car, and become commuting visitors with their own money. There is no switch-over day, no `IndependentPolicyStartsDay` and no legacy excursion path. Writer 27 is the next free schema; the final number is allocated at merge. Nothing here ran in the game or on the rig.

Performance: [docs/perf/f5d.md](../perf/f5d.md).

## What is implemented

- **Writer 27** (`CityState.CurrentSchema = 27`, `PersonTable.IndependentSchema`, `Persistence/PersonWire27.cs`).
  - The writer-26 visitor tuple stays (portal, pattern, presence, first day, next stay start, Employment, carried income, `LastArrival`). Every visitor now also has one **routine** object in the new root list `VisitorRoutines`, aligned by index with `Visitors`: `{"J": journey or null, "T": "<routine tuple>"}`.
  - The routine tuple holds phase, activity, current place, destination, event and activity-end minutes, departure, counters, lifecycle clock, shop and leisure venues, commute preparation, meal outing and weather commitment. Places are three base-36 digits of the ordinal place index; minutes are canonical invariant round-trip text and are refused otherwise.
  - Identity, traits and travel preferences are derived from `(seed, PersonId)` and never stored (contract B6). The home is the stay's portal. `DayPlaceId` and its start day come from the tuple's Employment; the carried source and the single arrival come from the tuple. A visitor has no car, recent-visit list or optional stop on the wire.
  - Each visitor tuple charges one record, as in writer 26; a journey charges its legs exactly like a resident's. Streaming and semantic counters agree (`StreamingAndSemanticRecordCountersAgree`).
  - The strict reader refuses: a routine count that differs from the visitors, unknown or missing routine fields, wrong tuple arity or version, out-of-range codes and place indices, noncanonical minutes or counters, a visitor without a portal, a portal that is not a portal place, a `consumer:` visitor, presence that differs from the routine, a dormant visitor with an arrival, a carried source next to an own Employment, a visitor car or car leg, routine stops and excursions in the roster, and a `NextVisitorSequence` that disagrees with the `pv:` IDs.
- **Older cities are refused.** `SaveCodec.LatestDevelopmentSchema = 26`: schemas 1–26 return `SaveReadStatus.DevelopmentSchema` with `DevelopmentSchemaMessage` ("…created by an older development build; start a new city. The saved state is kept unchanged."). The store and session keep the payload and refuse to replace it; the early authority probe refuses with the same message; F5c-lite's food stand-down applies unchanged. The frozen writer-26 reader stays behind the internal `SaveCodec.ReadWriter26`, reached only by its own tests, as the older chain already was.
- **Portals** (`Core/VisitorSchedules.cs` `PortalPlaces`, `Runtime/NativePortalStations.cs`). When a city's first roster is about to be created, Runtime reads the subway stations once on the main thread (same activity and DLC filter as route preparation, finite navmesh entrance) and Core adds each as a zero-capacity Home-kind place with evidence `portal:subway-station`, listed in the existing `Portals` table. Capacity zero keeps portals out of housing, destination and workplace choice. A portal's district is the district of the nearest saved place (ordinal tie-break), so a visitor's budget uses that neighbourhood's mix. Without stations no visitor is created; no portal is invented.
- **Visitors are persons with their own routines from the first day** (`VisitorSchedules.Create`, `CitySimulator`). A fresh roster at factor K creates K−1 visitors per resident as `pv:<n>` commuters, with a portal drawn by `Hash(seed, PersonId, "portal")`. Each visitor gets its own:
  - derived identity and traits;
  - subway-and-walk preferences (no car);
  - shop and leisure venues chosen from its portal;
  - employment seated with today's rule (best-scoring workplace with a vacancy, counted with residents and earlier visitors).

  A working-age worker without a vacancy keeps an outside employer as its **carried income** (`ExternalEmployment`). The ordinary daily job-loss draw ends it; the ordinary job-finding draw can seat it instead.
  - The routines live in `CityState.VisitorRoutines`, outside `Citizens`. Resident-only consumers (RLO identities, bodies, cars, traffic, census) never see them. The one event queue schedules them like residents.
- **Visitor portal arrival and departure in `DailyRoutine`.** A visitor's home slot is its portal, where it is off-map. A stay starts when it leaves the portal and ends when it arrives back. Commuter stays recur on weekdays only: at weekends the routine has no stops and the visitor stays at its portal. A visitor still waiting at its portal for a route has not arrived and stays dormant. The route is retried every 30 minutes inside the slot and never succeeds by itself.
- **Active / Dormant.** Presence is derived from the routine on every commit and before every save (`VisitorSchedules.Sync`). A dormant visitor holds no arrival and no journey, and its next stay start is its next event.
  - **No demand while dormant:** meal opportunities before the next stay end with no intent and no outcome; the first meal still open at the stay start waits for it (`SkipDormantMeals`).
  - Daily postings (income, housing, essentials, home food) continue for every person.
  - An uncertain receipt, held purchase or rival settlement stays with the same person and wallet and reconciles as before.
  - **Deferred departure:** any routine event during a meal outing first finishes the outing (the existing resident rule), so the visitor leaves only after its meal is done.
- **Per-person meal rhythm, routine stops and income source; no anchor read.** Every roster record is its own person's: `HostId == ResidentId`, slot 0, no `FoodRoutineStop`, no `MealExcursion`. `HouseholdFinance` selects a visitor's income source with F5b's `PersonIncome.Select` over the visitor's own Employment and carried source; residents keep today's rule unchanged. `FoodCommerce.AnchorReads` (exposed as `CitySimulator.FoodAnchorReads`) counts any food or finance lookup of a person other than the meal's owner; it is zero in writer 27. The C4/C5 survey reader now reads the meal's scheduling owner, which is the person itself in writer 27.
- **Visitor `LastArrival` admission guard** (plan section 3 *Arrival evidence for visitors*). A visitor keeps only its latest arrival, written by the same arrival commit that appends a resident's visit (`VisitorSchedules.RecordArrival`, used at all four arrival sites). Service at a player shop is admitted only if that arrival is a `Shop` arrival at the meal's place at or after `DesiredAt`, together with the existing at-place and outing conditions. Residents keep the unchanged `RecentVisits` check. A mismatch refuses service; nothing is fabricated.
- **No visitor car, traffic record or body.** Visitors own no car (`PersonPolicy.VisitorsOwnCars`); the codec and ownership validator refuse one. `TryAcquirePhysicalJourney` refuses a visitor. The F5f index files visitors as withheld, so they are never body candidates. Visitors take no optional-stop chance (those are body dwells).
- **Persons created later get meals and wallets.** On attach, writer 27 enrols every resident or visitor without a roster record, for example residents appended by `Population.ExpandToHousing` before `RebuildRoutes`. Existing records are never re-seeded. A populated roster with a factor other than 1 is refused.
- **`pc population person <id>`** (debug console, read-only): prints one person's routine, presence, portal, Employment, carried source, arrival, current meal, wallet source and the anchor-read counter (`VisitorSchedules.DescribePerson`). It is the diagnostic that rig check 3 uses.
- **Diagnostics wording.** The activation message and the food panel header count residents and visitors ("people with meals"), not represented consumers.

### Technical decisions to review

1. **Visitor routines are kept beside `Citizens`, not in it.** One scheduler and one event queue run both (plan section 3). A separate list keeps every resident-only path (RLO matching, physical selection, traffic, census, inspector) unchanged and gives the strict per-kind wire. The scheduler gives a visitor slot the complement of its list index.
2. **Fresh visitors are `pv:<n>`, K−1 per resident.** F5d has no census fill (F5g), so the requested representation factor still sets how many visitors a fresh city gets. They are new persons, so they take the `pv:` format (contract B1); the `consumer:` format appears only in the frozen writer-26 path.
3. **Portals are saved places.** Positions come from native stations once, when the roster is created, then stay with the city like every other place. A station the route planner later refuses (for example near a gate) leaves its visitors waiting at the portal and never relocates them.
4. **Weekday-only stays** follow the Commuter pattern of contract B2. Day visitors are F5g's.
5. **A visitor's employment check runs at its own portal events** (stay start and return), one overdue day per event, as for residents. No extra daily check event is scheduled while it is dormant. A dormant visitor still has commute-preparation events, which keep their 8–14-hour lead as the plan requires.
6. **Unseated workers carry `ExternalEmployment`.** This is "carried income and employment seating at city creation using today's seating rules". It keeps a fresh visitor's income equal to what today's anchor rule would have given a working consumer, without an anchor. `BusinessAllocation = 16` is unchanged; F5e replaces it.
7. **The frozen writer-26 path stays compiled for its tests.** Tests of represented consumers (factor 2/3 excursions, schema 17–26 fixtures) now build their city in schema 26 and round-trip through `tests/DevelopmentChain.cs`. The product path cannot reach it. Removing those codecs is a separate mechanical slice, as for the older chain.
8. **Pre-existing red test fixed in passing.** `ResearchSurveyTests.MissingInputsStayUnknown` was red at the base: C5b's survey read the stored `AnchorId`/`Slot`, which F5c-lite made the record's own. The survey now reads the scheduling owner (`HostId`, `HostSlot`).

## Offline evidence

New tests, all in `tests/Schema27Tests.cs` (32 cases), plus the env-gated measurement harness `tests/F5dMeasure.cs`:

- **Golden writer 27.** Two seeded fixtures (a factor-2 fresh city after 30 hours with visitors, and the minimal no-food city) are pinned in `tests/Writer27/golden.json` by SHA-256 and length, with a determinism check. `Writer26/golden.json` still pins the frozen writer-26 bytes (`Schema26Tests`).
- **Older schema refused and kept.** A writer-26 city with represented visitors, and a writer-25 city, are refused through codec, store, session and the early probe with `DevelopmentSchema` and "start a new city". The payload and every other key are kept byte-identical. The frozen writer-26 reader still decodes the payload, so the refusal is the schema, not damage.
- **Fresh city shape** at factor 1/2/3: visitor count, `pv:` IDs, portals, the portal home, no car, Employment from the routine, one own roster record per person, no routine stops or excursions, no `consumer:` or `RepresentationFactor` on the wire. Byte-identical round trip, and the reloaded city keeps running.
- **Portal routine over a week.** Every working visitor reaches its workplace, every stay starts and ends at its own portal, visitors are off-map at the weekend (days 5–7), and visitors buy meals.
- **No anchor read at all (spy).** Over 72 hours at factor 2 and 3, with reloads, `FoodAnchorReads` is 0, every meal's host is its own ID, and there is never a routine stop or excursion. Also checked in the rival test.
- **Per-person schedule independence across seeds.** Day traces are deterministic per seed. No visitor shares a resident's day, visitor days do not collapse onto each other, and different seeds give different visitor days.
- **Cadence, budgets, skip and reload.** The same 36-hour day at budgets 1, 7 (frame slices), 64 and 100,000 gives identical saves and orders. Two more runs reload whenever a visitor owns an in-flight meal outing, an uncertain receipt (lost acknowledgements injected) or a held decision, or at fixed points: still identical.
- **F1 harness persons variant.** `AccountingScenario.Run(…, persons: true)` (24 residents + 24 visitors in the test; 957 + 957 in the tool) gives the same save and orders at 30/60/120 FPS, 1/16/128 units, a stall, hidden skip and reload, with conservation checked during the run.
- **Conservation every hour from city creation** (factor 3, four days, reloads). Every wallet's `CashConserved` holds, and the city-wide `BigInteger` totals agree (opening + income = balances + essentials + home food + prepared + external + held). The person count is constant, and employment changes only through logged job changes.
- **Unreachable portal.** A planner that refuses every portal route keeps every visitor at its portal, dormant, with zero journeys. Retries happen, and there are no decisions, purchases or orders, while wallets keep posting.
- **No visitor car, traffic record or body.** Visitors have no car legs, are never body candidates and are refused a physical lease; the index matches a full scan. A visitor car is refused at write.
- **Resident arrival guard unchanged.** `FreshCityTraceTests` keeps its base pins for factor 1 and no food (residents only, writer 27). Factors 2/3 run the frozen represented path. On recorded resident traces, the resident `RecentVisits` guard equals the latest-arrival rule at every admission check.
- **Visitor admission across reload.** A visitor at a shop door is admitted through its `LastArrival` with no reload, a reload on the way, a reload at the door, and both; the intent is ordered exactly once.
- **Visitor refusal.** An arrival naming another place, a non-shop activity, or a minute before `DesiredAt` refuses service and places no order.
- **Income continuity across reload.** With no workplace vacancy, working visitors carry `ExternalEmployment`, retirees get a pension and others support. Sources, daily income and income received are equal across a reload each day, and equal to an unreloaded twin.
- **Departure deferral.** A visitor whose stay ends during its meal finishes the meal (ordered) before going dormant. At every step, no dormant visitor has a travelling, serving or held meal.
- **Dormancy with an uncertain receipt.** A lost acknowledgement leaves a visitor's meal uncertain with money held. The visitor goes dormant, is saved and reloaded, and stays uncertain with the same hold for twelve hours. When evidence returns, the receipt settles once: no second order, hold released, conservation kept.
- **Rival settlement across reload.** A visitor's rival meals with reloads after each settlement step give the same city as an unreloaded run, with external spending recorded and conservation kept.
- **Persons created later.** Residents appended by `ExpandToHousing` get a meal record and a conserved wallet after `RebuildRoutes`, and the city saves.
- **Review fixes (round 1).**
  - **Sol-r1-1:** writer-27 validation now adds the visitor tuple charges after `ValidatePersons27` has added travelling visitors' route legs. Before, a travelling-visitor fixture counted 26 streaming records against 25 semantic (`TravellingVisitorRouteLegsAreChargedByBothCounters`: red before the fix, green after).
  - **Sol-r1-2:** a visitor journey with a car leg is refused on read and write, and the payload is kept (`VisitorJourneyWithACarLegIsRefusedAndPreserved`).
- **Strict reader.** The refusal cases listed above, each refused as `Invalid` with every key preserved; future (28) and older (26) labels give `Future` and `DevelopmentSchema`.

Adjusted tests (only where they assumed writer 26 is current or relied on represented consumers):

- Represented-consumer fixtures (`FoodCommerceTests.Ready`, `WeightedFoodChoiceTests.Ready`, `HeldFoodTuningTests`, `RivalServiceLimitsTests`, `ResearchSurveyTests`, `FoodBudgetBatchTests`, `RushSpikeReplay`, the schema-17 economy baseline, `FreshCityTraceTests` factors 2/3) build in schema 26 through `DevelopmentChain.RepresentedSchema`. Their round trips use `DevelopmentChain` (writer 26 ↔ `ReadWriter26`). The economy baseline numbers and the #128 rush replay are unchanged.
- `Schema26Tests` is now the frozen writer-26 suite. Its golden pin is unchanged; the product status for every writer-26 payload is `DevelopmentSchema`.
- Schema-number asserts use 27 where they mean the current writer, and the chain-output constant 25 where they mean the older chain. Probe and corpus fixtures add 26 to the refused development schemas and use 28 as the future label.
- `RepresentativeFixture` (F7a), `EconomyFixture` (F6a) and the bounded-bulk harness add portals, so their factor-2 cities have one visitor per resident.
- The F5b source guard allows writer 27's validator and `HouseholdFinance`'s per-person income selection, and treats `Core/VisitorSchedules.cs` as an identity-owning file.
- Tool fix: `tools/accounting-backlog-regression` had not compiled since F5c-lite (its native stub lacked `EnsureSave`/`StandDown`); the stub gains both as no-ops.

## Validation run

Offline only, on this PC, 7 November 2026. Each `dotnet` command first waited for `wait-other-dotnet.ps1`. Nothing ran in the game or on the rig.

- `python tools/build.py --game-managed "<Steam>/Big Ambitions_Data/Managed"`: exit 0. Runtime compiled against the installed game assemblies.
- `dotnet test tests/PersistentCity.Tests.csproj`: **3,345 passed, 0 failed, 0 skipped** (2 min 14 s, after the round-1 review fixes; one earlier full run had a single wall-clock failure in the #128 rush replay at 31.7 ms, which passed alone twice and in the next full run).
- Named measurements: `tools/bounded-bulk-regression` (both modes), `tools/road-ceiling-regression`, `tools/accounting-backlog-regression` and `tests/F5dMeasure.cs`. Numbers are in the perf file.

## Performance

Summary; tables in [docs/perf/f5d.md](../perf/f5d.md). Offline, Release, synthetic; no game or rig timing.

- **Mod-work proxies at the reference load are far under p95 5 ms.** F1 persons variant (957 + 957): p95 ≤ 0.20 ms, max 21.5 ms, every arm identical. Integrated replay: p95 0.14 ms at 1,914 persons and 0.23 ms at 10,000. Posting units: p95 ≤ 0.05 ms at 1,914/10,000/100,000 persons, F3 bound kept.
- **Flagged:** at the 100,000-person stress size the integrated replay has 132 intervals ≥ 30 ms (max 77.3 ms). The F3 base already had 39–144 there. None at 1,914 or 10,000.
- **Events:** visitors about double daily events at K = 2 (48,814 vs 25,456 at 957 residents). Each person has exactly one queued routine event and one queued meal event, so the peak queue equals persons.
- **Routes:** the doubled commute peak (1,914 requests) drains in 26 s at 30 FPS and 13 s at 60 FPS, ≤ 16 queries per frame, inside the 90 s window.
- **Saved state:** a dormant visitor's tuple plus routine is ≤ 145 characters, an active idle one ≤ 204 (budget 256), and a travelling one ≤ 350 (route legs included). The 150,000-person city uses 51.9 % of the cap, slightly smaller than writer 26 on the same fixture. Retained memory adds about 0.4–0.45 KiB per visitor over writer 26.
- **Latency:** writer 27 saves and loads faster than F5c-lite's writer 26 from 5,000 residents up. All samples are ≥ 30 ms (save criterion, flagged).
- **Initialization (review Opus-r1-1):** creating a fresh city's visitors on the main thread takes 11.4 ms at 957 × 2 and 629 ms at 50,000 × 3, down from 60.4 ms and 1,739 ms after the per-call candidate filtering. **Flagged:** this one-time stall is ≥ 30 ms from 957 × 3 upwards. It is not ordinary-play work and does not occur on loads.
- **Wins taken:** a shared arrival record and no Employment allocation per posting. Allocation drops by 0.25 / 1.05 / 11.1 MB per day at 1,914 / 10,000 / 100,000 persons, with identical save bytes.

## In-game checks for the next batched rig

Every check runs on the headless agent rig ([PeterHartwieg/big-ambitions-agent-rig](https://github.com/PeterHartwieg/big-ambitions-agent-rig)) in a named disposable **FT-F5d** city or a disposable copy of a sealed fixture. The rig records autosave, disables it in the isolated profile, verifies it before every measured window (including after reload) and restores it on cleanup; explicit saves stay outside measured windows. No check is deferred to Peter, a playtest, an unlocked console or a by-hand visual check. They are drawn from plan section 7 (R3, R5, R6, R7, R8, R10 and R9's refusal half), re-scoped for fresh cities without migration.

1. **A fresh city saves and reloads in writer 27 with visitors at portals** (R1/R9 replacement).
   - **Setup:** a fresh FT-F5d city on the head build.
   - **Steps:** let Persistent City start, save natively, reload in the same process, then reload in a fresh process.
   - **Expected:** `persistentcity:state_v1` has `"Schema":27`, `Portals` lists the station IDs logged at start ("Visitor portals: N subway stations"), and `Visitors`/`VisitorRoutines` number (factor − 1) × residents with `pv:` IDs. The save-epoch marker says `schema=27`. Persistent City is active after both reloads with no refusal, and a second save after the fresh-process reload has the same visitor IDs, portals and Employments.
   - **rig-automated:** existing capabilities `rig flow run`, RIG-MODSET, FT `save`/`load`/`state`/`console`; pending capabilities RIG-LIVE, and RIG-PCSAVE (extract PC keys from a saved checkpoint).
2. **A writer-26 city is refused with "start a new city" and kept** (R9, refusal half).
   - **Setup:** a disposable copy of an FT city saved by the base build `30a9de7`, with a `shop_fixture` PC-owned food shop.
   - **Steps:** load it with the head build; run two native hours; save natively.
   - **Expected:** the log and status show "created by an older development build; start a new city". Every `modData` key hash is unchanged after load, after two hours and after the save, and no accounting-stop or epoch marker is written. From the hour after load the shop gets native customers and sales (`shop_state`/`shop_log`), and Persistent City books none.
   - **rig-automated:** existing capabilities RIG-MODSET, FT `load`/`save`/`time`/`state`/`console`, `shop_fixture`, `shop_state`, `shop_log`; pending capabilities RIG-PCFIX (a sealed writer-26 fixture) and RIG-PCSAVE key hashing.
3. **Independent schedules through a working day** (R3, fresh city).
   - **Setup:** check 1's city at 06:00 on a weekday.
   - **Steps:** follow one resident and one employed visitor through the day with `pc population person <id>` at each phase change; `camera follow` and `capture` the resident only (visitors have no body before F5i).
   - **Expected:** different departure times and places. The visitor leaves its portal station, works, eats and returns to the same portal. A visitor meal at a player shop is admitted through its `LastArrival`, also after a reload taken at the shop door. The anchor-read counter (`FoodAnchorReads`) is 0. Identity and wallet facts are identical with and without the resident's body.
   - **rig-automated:** existing capabilities FT `time`/`console`/`save`/`load`, RIG-VIEW `camera`/`screen_pos`/`capture`; pending capability RIG-LIVE. The PC diagnostic `pc population person <id>` is implemented in this slice.
4. **No visitor among bodies or leases** (R5, F5d part).
   - **Setup:** check 1's city at the morning commute.
   - **Steps:** move the camera with `camera look_at` through the street nearest a portal station and a quiet district, sampling `screen_pos list` and the physical-population status line.
   - **Expected:** pedestrians ≤ 48 and cars ≤ 24, no `pv:` ID among bodies or leases, `journeyIndexWithheldVisitors` > 0 during the commute, and no identity or wallet change on acquire/release.
   - **rig-automated:** existing capabilities RIG-VIEW `camera`/`screen_pos`/`capture`, FT `console`; pending capability RIG-LIVE.
5. **Native sale authority and duplicate protection with visitors** (R6).
   - **Setup:** check 1's city with a managed `shop_fixture` food shop near a portal.
   - **Steps:** record `shop_log` purchases and PC receipts across an accounting hour and a midnight settlement; save and reload while a visitor's checkout is pending.
   - **Expected:** one native order per paid intent, including visitor intents; one owner per intent; no duplicate or replayed sale; exact wallet debits; native daily revenue equals PC confirmed receipts.
   - **rig-automated:** existing capabilities `shop_fixture`, `shop_log`, `shop_state`, FT `save`/`load`/`time`/`console`; pending capability RIG-LIVE.
6. **Event budgets and frame caps** (R7).
   - **Setup:** a checkpoint of check 1's city.
   - **Steps:** run one logical day per arm with the PC event budget at its minimum and maximum and frame caps 30/60/120.
   - **Expected:** identical end-of-day state hashes and visitor/order lists, `HistoricalClockMisses == 0` and lag ≤ 1 minute in every arm.
   - **rig-automated:** existing capabilities `rig flow run`, FT `time`/`console`; pending capabilities RIG-PCCONFIG, RIG-FRAMECAP, RIG-LIVE.
7. **Visitor lifecycle over two weeks** (R10, F5d part; F5g adds fill).
   - **Setup:** check 1's city.
   - **Steps:** run two game weeks with hidden skips between measured days. Save and reload at a visitor's portal arrival, during a departure deferral (meal in progress), while dormant with an uncertain receipt (a `shop_fixture` checkout interrupted by the rig), and at the next arrival. Read the person manifest daily.
   - **Expected:** the same `pv:` IDs every day, with no visitor created or retired. Visitors are dormant at weekends, and dormant visitors create no meal intents but receive daily postings. The uncertain receipt resolves exactly once. `BigInteger` ledger totals conserve at every checkpoint.
   - **rig-automated:** existing capabilities `rig flow run`, FT `time`/`save`/`load`/`console`, `shop_fixture`, fixture library; pending capabilities RIG-LIVE, and the PC diagnostic `pc population manifest` (pending implementation work).
8. **Ordinary-play performance with visitors** (R8 subset).
   - **Setup:** the reference fixture as a fresh head city, interleaved with base-build windows.
   - **Steps:** measure windows at idle, morning commute, lunch and evening, with the camera stationary and moving and the Ctrl+F8 panel closed and open.
   - **Expected:** mod-work p95 < 5 ms in every head window; every mod-work spike ≥ 30 ms flagged with its condition; head-vs-base differences reported; whole-frame numbers separate; zero save callbacks inside a window.
   - **rig-automated:** existing capabilities `rig flow run` autosave guard, RIG-MODSET, FT `state`/`time`/`console`, RIG-VIEW `camera`; pending capabilities RIG-LIVE, RIG-PERF-UNION.
