Status: implemented (offline); not verified by rig

# City audit F5e: workplace capacity and employment in writer 28, 7 November 2026

Refs [#92](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/92). Base `b1dbc82` on `codex/persistent-city` (writer 27, F5d #138 merged). Scope is the F5e row of [the F5 plan](../plans/f5-population.md#4-slice-breakdown) as re-scoped by the [no-migration decision](../decisions/2026-11-06-no-migration-rescope.md). Writer 28 is the next free schema, and the final number is allocated at merge. Nothing here ran in the game or on the rig.

Performance: [docs/perf/f5e.md](../perf/f5e.md).

## What is implemented

- **Area bands replace `BusinessAllocation = 16` for Work and Study** (`Core/WorkplaceCapacity.cs`, `WorkplaceCapacityPolicy`). A Work place seats F5a's band for its native `BuildingSizeData.squareMeters`: < 40 m² 2, < 75 4, < 150 6, < 300 10, < 600 16, otherwise 24. A school's Study place seats twice the band. `Runtime/GamePlaces.cs` reads the area from the catalogue dictionary it already used for homes and the census, and tags those places with evidence `policy:workplace-bands-v1`. Shop and leisure allocations stay at 16, because they are customer attraction weights.
  - A business without a positive catalogue area seats nobody. No capacity is invented.
  - A player-owned business seats nobody (`PopulationCensusPolicy.ExcludePlayerOwnedWorkplaces`, F6 request C1). F5a reported this exclusion and F5e applies it. Ownership is read once, from the catalogue when the city is created, and saved capacities are never refreshed on load. A business the player rents later keeps its band and the persons already seated there, and nobody is evicted. A business the player gives up keeps capacity 0. No later ownership change reseats anyone, and any reconciliation with native staffing (C1) belongs to F6.
  - The census's assignable capacity now equals the city's seated capacity for the same business facts.
- **Writer 28** (`CityState.CurrentSchema = 28`, `PersonTable.WorkplaceCapacitySchema`). It keeps writer 27's shape. The number marks a city whose Work and Study places were seated under the bands from creation. Fresh cities are created and seated under the bands.
- **Older cities are refused.** `SaveCodec.LatestDevelopmentSchema = 27`. Schemas 1–27 return `SaveReadStatus.DevelopmentSchema` with "…start a new city. The saved state is kept unchanged." A writer-27 city can hold more workers at a small business than its band allows, so it cannot be read as a writer-28 city. The store, session and early authority probe refuse it the same way and keep every key. F5c-lite's food stand-down applies unchanged. The frozen writer-27 reader `SaveCodec.ReadWriter27` is reachable only from tests, like `ReadWriter26`.
- **Employment by the existing daily draws.** The loss and finding chances, their key `<id>:employment:<day>` and the one-overdue-day-per-event rule are unchanged. Residents are seated at creation in ordinal order, then visitors in `pv:` order compete for what is left (F5d's seating, now against the bands). A worker without a vacancy stays unemployed, and a working visitor keeps its carried outside employer until its own draw ends or replaces it. A loss frees the seat; a later successful roll anyone makes can take it.
- **Capacity bookkeeping** (`WorkplaceOccupancy`). The simulator keeps persons per Work/Study place and moves the count only when an event commits a changed `DayPlaceId`. A finding roll checks vacancies against it instead of recounting every person. The index is derived state and never saved. It is rebuilt when a simulator is built or visitors are scheduled, and it recounts itself if a commit ever finds it out of step. The strict reader still refuses any payload that seats more persons than a place's capacity.
- **One active Employment per person.** A resident's Employment is its `DayPlaceId` (single field). A visitor's tuple Employment is synced from its routine. The F5b ownership validator refuses two active Employments on every read and write.
- **Study carries no wage; income composes with F5d.** Residents now select income through `PersonIncome.ForDayPlace`, the same per-person rule visitors use through `PersonIncome.Select`, without allocating an Employment per posting. It equals the old resident rule. A student's Study enrolment never gives employment income. F5d's schedules and carried income are unchanged.
- **No native staff member.** No product code calls a native hiring, staffing or employee API. The only native staff read is F5a's informational rival `aiEmployees` list length inside GamePlaces' census block, and capacity never uses it. `NativeEmployment` has only `None`.

### Technical decisions to review

1. **Area bands, not the native rival staff count.** F5a verified `aiEmployees` as a rival headcount. F5e does not use it as capacity. It exists only for rival businesses, poaching and replacement edit it, and it counts native staff, which Persistent City persons must never become. The bands are the one input for every business.
2. **The schema number is the only capacity mark.** Area is not saved for workplaces, so the reader cannot re-derive a band from a payload. The writer-27 refusal is what keeps 16-seated cities out. Saved capacities are the city's own after creation, as for homes.
3. **Vanished places.** A saved city keeps its own place catalogue, and no product path removes a place from it. A payload whose Employment names a missing place, a place of the wrong kind, or a zero-capacity place is refused and kept. It is never re-seated.
4. **Tight capacity reduces visitor trips.** When the bands leave fewer seats than workers, unseated visitors keep carried income but make no commute. The perf file shows this with a deliberately tight catalogue. At the reference shape the bands still exceed the workers.

## Offline evidence

New tests in `tests/Schema28Tests.cs` (28 cases), plus the opt-in harness `tests/F5eMeasure.cs`:

- **Golden writer 28.** Two seeded fixtures (a banded factor-2 city after 30 hours, and the minimal city) are pinned in `tests/Writer28/golden.json` by SHA-256 and length, with a determinism check. Writer 28 differs from frozen writer 27 only in the schema label. `tests/Writer27/golden.json` is unchanged and now pins the frozen writer-27 bytes, which `ReadWriter27` round-trips.
- **Older schema refused and kept.** A writer-27 city seated under 16 holds more workers at a 30 m² business than its band of 2. Codec, store, session and the early probe refuse it with `DevelopmentSchema` and "start a new city", and every key is byte-identical afterwards. The frozen reader still decodes it, so the refusal is the schema, not damage. Writer 26 is refused the same way, and 29 is refused as future.
- **Band edges** for Work, Study, unknown, non-finite and player-owned areas, equal to F5a's census function.
- **Census agreement.** A fresh banded city's Work and Study capacity equals the census's, with no over-capacity workplace. Unknown-area and player-owned businesses seat nobody.
- **Deterministic fill order.** Residents fill first and leave vacancies, then visitors are seated as a prefix in `pv:` order. Every unseated working visitor carries outside income, and seated workers equal the capacity exactly. A shuffled catalogue and donor order give byte-identical saves.
- **Capacity never exceeded under churn and reload.** Two years of daily draws keep every workplace within capacity, every visitor's Employment matching its routine and every job change counted. A 90-day simulation with a reload every ten days (jobs lost and refilled) stays within capacity, the occupancy index equals a full recount every day and after every reload, and every reload keeps its bytes. A payload seating one more person than a place's capacity is refused and kept.
- **Loss and find rates unchanged against the base.** Fresh seeds 5150 and 77 run 1,500 days under both the bands and the base rule (every workplace 16). Where a person is employed in both arms, its loss outcome is identical on every shared day (over 10,000 person-days). In both arms the loss rate per employed day and the finding-roll rate per unemployed day are within four standard deviations of 0.001 and 0.10.
- **Occupancy index equals the recount.** Two identical cities churned for two years, one through the index and one recounting, end with identical assignments and job-change counts.
- **Study carries no wage.** In a running city every student's budget source is not `ExternalEmployment`, every seated adult worker's is, and unseated visitors keep their carried source. `ForDayPlace` equals `Select` over every persona, age and assignment.
- **Vanished or wrong places.** A missing workplace, a shop as a workplace, a workplace removed from the catalogue, and an Employment at an unknown-area or player-owned business are each refused and kept.
- **Native-employment source guard.** No Core, Persistence, Runtime or Compatibility file names a native staff or hiring member. The only `Employee` identifiers are the food readers' `EmployeeWorkstation` flag and GamePlaces' single `aiEmployees` read inside the census block. GamePlaces' Work and Study capacities come from `WorkplaceCapacityPolicy`, never `BusinessAllocation` or the staff count.

Adjusted tests (only where they assumed writer 27 is current):

- Schema-number asserts use 28 where they mean the current writer. Probe and refusal fixtures add 27 to the refused development schemas and use 29 as the future label. `CurrentSchemaOnlyTests` covers schema 27.
- `Schema27Tests` pins the frozen writer-27 bytes through `DevelopmentChain.Write27` and `ReadWriter27`. `DevelopmentChain` routes schema-27 payloads to that reader.

## Validation run

Offline only, on this Mac (Apple M1 Pro, .NET 10.0.400), 7 November 2026, with no other dotnet test process running. Nothing ran in the game or on the rig.

- `python3 tools/build.py --game-managed "<dll-import-shim>/Big Ambitions_Data/Managed"`: exit 0.
- `dotnet test tests/PersistentCity.Tests.csproj`: **3,440 passed, 0 failed, 0 skipped** (3 min 42 s).
- `tests/F5eMeasure.cs` (Release, opt-in): shortlist cost, day gate and save cost. The numbers are in the perf file.

## Performance

Summary; tables in [docs/perf/f5e.md](../perf/f5e.md).

- **Mod-work proxy:** one natural day at 1,914 and 10,000 persons has slice p95 ≤ 0.61 ms in every arm. No slice reaches 30 ms.
- **Job-finding roll at 10,000 persons:** p50 0.18 ms and 573 B with the occupancy index, down from 0.53 ms and 102,768 B. At 100,000 persons it went from 10.9 ms (2 of 400 rolls ≥ 30 ms, flagged) to 1.6 ms with none.
- **Saved state:** within 0.3% of the base rule. The 150,000-person city uses 54.4% of the character cap. Save and load samples are ≥ 30 ms (save criterion, flagged).

## In-game checks for the next batched rig

Every check runs on the headless agent rig ([PeterHartwieg/big-ambitions-agent-rig](https://github.com/PeterHartwieg/big-ambitions-agent-rig)) in a named disposable **FT-F5e** city or a disposable copy of a sealed fixture. The rig records autosave, disables it in the isolated profile, verifies it before every measured window (including after reload) and restores it on cleanup. Explicit saves stay outside measured windows. No check is deferred to Peter, a playtest, an unlocked console or a by-hand visual check. The checks come from plan section 7 (R4 and R8, and R9's refusal half), re-scoped for fresh cities without migration.

1. **A fresh city saves and reloads in writer 28 with banded workplaces.**
   - **Setup:** a fresh FT-F5e city on the head build.
   - **Steps:** let Persistent City start, save natively, reload in the same process, then reload in a fresh process.
   - **Expected:** `persistentcity:state_v1` has `"Schema":28` and the save-epoch marker says `schema=28`. Every Work and Study place has evidence ending `policy:workplace-bands-v1` and a capacity in {0, 2, 4, 6, 10, 16, 24} (Study: twice those). Persistent City is active after both reloads with no refusal, and the second save has the same `DayPlaceId`s and visitor Employments.
   - **rig-automated:** existing capabilities `rig flow run`, RIG-MODSET, FT `save`/`load`/`state`/`console`; pending capabilities RIG-LIVE and RIG-PCSAVE (extract PC keys from a saved checkpoint).
2. **A writer-27 city is refused with "start a new city" and kept** (R9, refusal half).
   - **Setup:** a disposable copy of an FT city saved by the base build `b1dbc82`, with a `shop_fixture` PC-owned food shop.
   - **Steps:** load it with the head build, wait one accounting hour, save natively, then reload it with the base build.
   - **Expected:** the head build logs the development-schema refusal with "start a new city", takes no food ownership and lets the shop return to native customers after the current hour. The `persistentcity:state_v1` value after the head build's save is byte-identical to the original. The base build loads it as before.
   - **rig-automated:** existing capabilities RIG-MODSET, `shop_fixture`, `shop_log`, FT `load`/`save`/`time`/`console`; pending capabilities RIG-LIVE and RIG-PCSAVE.
3. **Workplace capacity against native areas** (R4, workplace half).
   - **Setup:** a fresh FT-F5e city with one `shop_fixture` player business and the existing rival businesses.
   - **Steps:** save and reload the checkpoint (business facts are read at load), then read `pc population census workplaces` and the saved place list.
   - **Expected:** each established business's saved Work capacity equals the census band `capacity=` for its `area=`, which equals `shop_state.location` square metres for the fixture. The fixture business (`owner=player`) has capacity 0 and nobody seated. Unknown-area lines have capacity 0. `check_jobs_within_capacity=ok`, and no workplace line seats more than its capacity.
   - **rig-automated:** existing capabilities `shop_fixture`, `shop_state`, FT `save`/`load`/`console`; pending capabilities RIG-LIVE and RIG-PCSAVE.
4. **No native employment over three days** (R4, staff half).
   - **Setup:** the city from check 3, the fixture shop with real native staff hired by the rig's fixture step.
   - **Steps:** record the fixture's native staff list and every rival's `aiEmployees` count, advance three game days at ordinary pace, then read them again together with `pc population person` for three seated residents and one seated visitor.
   - **Expected:** the native staff list and every rival `aiEmployees` count are unchanged by Persistent City. Each sampled person has exactly one workplace, at a business other than the player's, and no person appears in any native staff list.
   - **rig-automated:** existing capabilities `shop_fixture`, `shop_state`, FT `time`/`console`; pending capabilities RIG-LIVE and rival `aiEmployees` count readback (extension of `rival list`).
5. **Students earn no wage and workers keep theirs across a reload.**
   - **Setup:** the city from check 1.
   - **Steps:** advance past the next midnight posting, read `pc population person` for one seated student, one seated adult worker and one unseated working visitor, save, reload and repeat after the next posting.
   - **Expected:** the student's `income=` is `ExternalSupport`, the worker's is `ExternalEmployment`, and the unseated visitor shows `carried=ExternalEmployment` with income `ExternalEmployment`. The values are equal before and after the reload.
   - **rig-automated:** existing capabilities FT `time`/`save`/`load`/`console`; pending capability RIG-LIVE.
6. **Performance subset** (R8).
   - **Setup:** the reference fixture, the base build `b1dbc82` and the head build, autosave off and verified before every window.
   - **Steps:** interleaved idle and morning-commute windows on each build, stationary and moving camera, plus one window across the first in-game daily employment check after midnight.
   - **Expected:** mod-work p95 < 5 ms in every window with no F5e-attributable change against the base. Every mod-work sample ≥ 30 ms is flagged with count, condition and component. Whole-frame metrics are recorded separately and never used as mod-work.
   - **rig-automated:** existing capabilities `rig flow run` autosave guard, RIG-MODSET, FT `state`/`time`/`console`, RIG-VIEW `camera`; pending capabilities RIG-PCFIX and RIG-LIVE.
