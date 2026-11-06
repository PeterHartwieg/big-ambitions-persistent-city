Status: implemented (offline); not verified by rig

# City audit F5c-lite — person shape in writer 26, no migration, 6 November 2026

Refs [#92](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/92). Base `e9995e5` on `codex/persistent-city` (writer 25). Scope: the F5c-lite task, as fixed by Peter's owner decision of 6 November 2026. The mod is not live and nobody has a save worth keeping, so there is **no save migration before release**. Schema 26 refuses older development cities with a clear message, and the player starts a new city. Where this differs from the F5 plan's F5c row, the owner decision applies. None of this is verified by rig, and offline success is not native proof.

Wire description: [docs/save-format.md](../save-format.md). Performance: [docs/perf/f5c-lite.md](../perf/f5c-lite.md).

## What is implemented

- **Writer 26** (`CityState.CurrentSchema = 26`; `Persistence/PersonWire26.cs`, `Core/PersonTable.cs`):
  - **Residents.** Each `Citizens[]` row carries the resident person tuple `P`: kind, plus Employment start day, schedule code and native-employment code. `CitizenState.EmploymentStartDay` holds the start day; `ResidentLifecycle` sets it at creation and on a job change and clears it on job loss.
  - **Visitors.** `Visitors[]` holds ordinal visitor tuples: stay (portal index, pattern, presence, first day, next stay start), Employment, `CarriedIncomeSource` and the `LastArrival` tuple.
  - **Root.** `NextVisitorSequence`, `IdentityPolicyVersion` and the station table `Portals` are added at the root.
  - **Factor-1 roster rule.** There is one own slot-0 roster record per person (`AnchorId == ResidentId`, `Slot == 0`, stored `RepresentationFactor == 1`), live and decoded. Roster tuple v3 drops `AnchorId` and `Slot`, and `Food` has no `RepresentationFactor`. Today's excursion schedule reads the non-persisted scheduling adapter `ResidentMeal.HostId`/`HostSlot` (and a scheduling factor), so behaviour is unchanged. Every read and write also runs F5b's `OwnershipValidator` over the saved person table (review fix Sol-r1-1).
  - **Records.** Each resident tuple, visitor tuple and station entry charges one embedded record in both the streaming and the write-side counter; a city at exactly 4,000,000 records round-trips and one more station is refused before publication (review fix Sol-r1-2).
  - **No IndependentPolicyStartsDay and no legacy-mode fields.**
- **Fresh cities in the new shape, behaviour unchanged.** A factor-K fresh city enrols its K−1 represented consumers per resident as visitor persons. Their IDs are kept: `consumer:<anchor>:<slot>`, pattern Commuter by the section 9 Q3 constant `PersonPolicy.MigratedConsumerPattern`, Active, first day = the enrolment day. Their meals keep today's excursion schedule through their host resident. The host is not stored: on read it is found by the existing `FoodRepresentation.ConsumerId(host, slot)` formula, evaluated forward for every resident and slot, the same check today's roster initialization makes. Residents keep their routines. F5d changes behaviour, with its own schema bump.
- **Strict reader for 26.** Future (`Future`), unknown, duplicate, missing, malformed, inconsistent and over-bound input is refused (`Invalid`), and the stored payload and every other key are preserved; `modData` is never rewritten. Per-kind shape:
  - A `Citizens` row whose `P` says visitor is refused ("A visitor row cannot carry resident identity, home, car or recent visits").
  - A resident tuple in `Visitors` is refused ("A resident row requires identity…").
  - A visitor tuple has no slot for identity, home, car or `RecentVisits`, so an extra field fails its arity.
  - `LastArrival` is checked for structure only (plan section 3, *Arrival evidence*). `NativeEmployment` and the schedule code are fixed at 0.
- **Named bounds** (`SaveCodec.MaxPersons` 150,000, `MaxResidents` 50,000, `MaxVisitors` 100,000, `MaxTrafficVehicles` 50,000, `MaxPortals` 256). They are checked while streaming, before deserialization allocates, and again at the start of every write. The roster and routine stops use `MaxPersons`. Traffic lists keep the resident bound. Other limits are unchanged.
- **Older schemas (1–25) are refused** with the distinct status `SaveReadStatus.DevelopmentSchema` and `SaveCodec.DevelopmentSchemaMessage` ("This Persistent City save was created by an older development build; start a new city. The saved state is kept unchanged."), with `SourceSchema` set. The status is neither `Invalid` nor `Missing`; the store and session refuse to replace the payload. `SaveCodec.Read` no longer reaches the older codec chain. That chain stays, unchanged, behind the internal `SaveCodec.ReadDevelopmentChain`, which only its own tests call. The old codecs, corpus and tests are not deleted; three frozen resolvers gained one property-exclusion term so the new model properties stay out of their shapes. The early authority probe (`RequireExecutableCapabilities`) accepts only schema 26 and gives older schemas the development message.
- **Refused city with PC-owned food businesses** (`Core/FoodAuthorityStandDown.cs`, `Runtime/NativeFoodOwnership.cs`, `Runtime/AccountingRecovery.cs`):
  - `EnsureSave` recognises a development payload by its header (`SaveCodec.DevelopmentSchemaOf`) before any manifest read or suspension path, and reads the manifest read-only. An unreadable manifest, or one ahead of the loaded clock, protects nothing, so every player business is native at once (review fix Opus-r1-1). Persistent City then takes no authority and persists nothing: no manifest, receipt, accounting-stop or epoch key changes.
  - A shop whose saved manifest owned the loaded hour stays suppressed until that hour ends, so a partly mod-served hour is never replayed natively. At the next native boundary, its queued work drains and its current and future native customer hours are regenerated. Every other player shop is native from the start.
  - Startup refuses before the accounting-stop check (which would otherwise write `accounting-stop_v1`) and before any save-epoch work. Loading a different save object later can start Persistent City again.
- **F7b save-epoch contract unchanged.** `Persistence/SaveEpoch.cs` and its runtime are untouched. The marker records the writer schema, now 26.
- **Inspector.** `ResidentInsight` gains `Kind`, `HouseholdSize` (residents sharing the saved home; `HouseholdId` is derived, never stored) and `PersonWords`, for example "Resident; household of 3; employed since day 1.". The popup shows that line. Words only, no IDs.

### Scope decisions to review

1. **Represented consumers are visitor persons.** This is how the factor-1 roster rule and "behaviour stays what it is today" hold together. The 26 shape stores no anchor or legacy field. The host relation is the existing ID formula, evaluated forward, and no anchor is ever parsed out of an ID. Because of this, writer 26 accepts only represented visitors: a `pv:<n>` visitor, or any visitor with no host, is refused. F5g creates other visitors under its own schema, so `NextVisitorSequence` is 1 in every writer-26 city.
2. **Stored but not yet acted on.** Visitor presence, next stay start, portal, Employment, `CarriedIncomeSource` and `LastArrival` are stored and strictly validated. No routine, choice or money path reads them in writer 26. Fresh cities write Active, no portal, no employment, no carried source and no arrival. A structurally valid dormant visitor loads, but its meals still follow its host. F5d gives these fields behaviour and bumps the schema.
3. **Resident Employment** keeps the workplace in the existing `DayPlaceId` field rather than as a second, index-encoded copy, and its kind follows persona. The tuple adds only what is not already saved: start day, schedule and native code. The plan's index encoding is left to F7d's compact wire.
4. **Not in the visitor tuple:** phase/activity/place/destination (visitors are not scheduled in 26) and a census policy version (no census-driven state exists yet). Both belong to F5d/F5g with their schema bump.
5. **Visitor IDs are limited to 256 characters** (tuple cap 512). This refuses a represented consumer of an anchor ID longer than about 240 characters, which neither RLO (`c<n>`) nor generated residents (`pc:<n>`) produce.

## Offline evidence

New tests:

- `tests/FreshCityTraceTests.cs` (**fresh-city trace equivalence**). It was committed and pinned at the base before any change (`2c43d97`). It runs a fresh seeded city for three days, reloading through the live codec at each day boundary. The trace hashes phases, places, events, meals, purchases, wallets and orders; it reads behaviour only, never the save shape. Factors 1, 2 and 3 with food, and no food. The four hashes are unchanged after the change, with the reloads going through writer 26.
- `tests/Schema26Tests.cs` (19 cases):
  - **Golden writer 26:** two seeded fixtures (a factor-2 fresh city after 30 hours, and the minimal no-food city) pinned in `tests/Writer26/golden.json` by SHA-256 and length, plus determinism.
  - **Fresh cities:** factor 1/2/3 and no-food fresh cities in the new shape. Visitor count = (K−1) × residents, ordinal, Commuter/Active; roster = persons; tuple v3 has 35 slots; no `RepresentationFactor`/`EmploymentStartDay` on the wire; byte-identical round trip; derived factor, anchors and slots equal the runtime values; the reloaded city keeps running.
  - **Employment:** start-day bookkeeping, including a later job.
  - **Refusals:**
    - per-kind: a visitor in the resident table, a resident without identity, extra or missing tuple fields, unknown kind or version;
    - resident tuple vs `DayPlaceId`, schedule/native codes, noncanonical or out-of-range start, duplicate `P`;
    - root: future (`Future`), unknown field, a `RepresentationFactor` field, missing `Visitors`, `NextVisitorSequence`, `IdentityPolicyVersion`, unordered/duplicate/oversized/overlong stations, roster tuple v2, missing/duplicate/unordered visitors, a visitor with no host, a `pv:` visitor;
    - stay pattern/presence/first day/next-stay rules, portal index, employment kind/place/day-visitor/native/schedule, carried income;
    - fabricated `LastArrival`: future minute, negative, NaN, lowercase hex, place index over the codec bound and over the table, unknown activity, short/long/wrong-version token, arrival on a dormant visitor. Historical arrivals (Work, at another place, at minute 0) read and round-trip.
  - **Bounds, at the bound and one over:** 150,000/150,001 persons (50,000 + 100,000/100,001), 50,000/50,001 residents, 100,000/100,001 visitors, 50,000/50,001 traffic vehicles, read side (refused before allocation) and write side.
  - **Schema-25 city with PC-owned food businesses:** the corpus `schema-25/full.json` with a manifest (a managed shop, a never-owned shop and a released shop), a receipt, an accounting-stop key, a save-epoch marker and a foreign key. The store and session report `DevelopmentSchema` with the message and refuse every write. The early probe refuses. The stand-down protects only the managed shop, only up to the loaded hour, and hands back once. Every key is byte-identical (`Assert.Same`) afterwards.
  - **Runtime wiring source guard:** the development check precedes the accounting-stop check and save-epoch work, and the stood-down path returns before any `Persist`.
  - **Inspector words** for every resident.
- `tests/ReaderCorpusTests.cs`: every corpus fixture of schemas 13–25 is refused by the product session as `DevelopmentSchema`, with the message and source schema, and every key is preserved. This is the schema-25 refusal test over the frozen corpus.
- `tests/CurrentSchemaOnlyTests.cs`: schemas 1–13, 24 and 25 give `DevelopmentSchema` through codec, store and session, and the payload is preserved.

Adjusted tests (only those that assumed older schemas still load):

- **Old-codec tests** read through `tests/DevelopmentChain.cs`. For schema-26 payloads and states it is exactly `SaveCodec.Read`/`Write`; for 1–25 it calls the preserved internal chain and the writer-25 path. Tests that loaded an older payload through `SaveSession`/`SaveStore` now assert `DevelopmentSchema` with every key preserved (`DevelopmentChain.RefusedThenRead`) before checking the chain. Former migration publications now assert that the product refuses to write.
- **Corpus pins.** The corpus keeps its writer-25 pin (`current-writer-25.json`); `ReaderCorpus.CorpusWriter = 25`. No corpus file changed.
- **Current-writer tests** that index the roster tuple now use the v3 positions.
- **Schema-number asserts** use 26 where they mean the current writer and 25 where they mean the chain's output.
- **The F5b source guard** now names the files allowed to read person types and still forbids drawing derived identity outside Core's identity files.

## Validation run

Offline only, on this PC, 6 November 2026. Each `dotnet test` first waited for `wait-other-dotnet.ps1`. Nothing ran in game or on the rig, and the regression suites and validation matrices were not run (Peter, 6 November 2026).

- `python tools/build.py --game-managed "<Steam>/Big Ambitions_Data/Managed"`: exit 0. Runtime compiled against the installed game assemblies.
- `dotnet test tests/PersistentCity.Tests.csproj`: **3,200 passed, 0 failed, 0 skipped** (2 min 47 s) at `b816a2e`. The final documentation commit changes no code.
- Measurement: `PC_F5C_LITE_MEASURE=… dotnet test … -c Release --filter FullyQualifiedName~F5cLiteMeasure`; results in the perf file.

## Performance

Fresh cities at 957/5,000/50,000 × K2/K3, with the same state written by writer 25 and writer 26:

- **Bytes:** +39,645 characters at 957 × 2 and +4.58 M at 50,000 × 3, which reaches 52.0 % of the cap.
- **Records:** exactly +1 per person (resident and visitor tuples) and per station entry, after review fix Sol-r1-2.
- **Retained memory:** 95.7–121.5 B per visitor.
- **Tuple sizes:** resident tuple ≤ 15 characters, visitor tuple ≤ 67.
- **Latency:** after the review fixes (the ownership pass on every read and write), writer 26 is slower than writer 25 at ≥ 5,000 residents: +16–37 % write and +10–19 % read p50. At 957 it is mixed (−3 %/+27 % write). Every save/load sample is ≥ 30 ms in both arms. This is the save criterion, flagged as in F7a, and is not ordinary-play mod-work (perf file, *Re-measured after the review fixes*).
- **Frame work:** none added, so mod-work p95 is unchanged by construction; whole-frame timing is separate.
- **Win:** visitor enrolment ordering is 4× faster at 50,000 × 3 (165.6 → 41.1 ms p50). It is still ≥ 30 ms at that stress size, once at fresh-roster activation (flagged).

Full tables and limits: [docs/perf/f5c-lite.md](../perf/f5c-lite.md).

## In-game checks for the next batched rig

Every check runs on the headless agent rig ([PeterHartwieg/big-ambitions-agent-rig](https://github.com/PeterHartwieg/big-ambitions-agent-rig)) in a named disposable **FT-F5c-Lite** city or on a disposable copy of a sealed fixture. The rig records autosave, disables it in the isolated profile, verifies it before every measured window (including after reload) and restores it on cleanup; explicit saves stay outside measured windows. No check is deferred to Peter, a playtest, an unlocked console or a by-hand visual check.

1. **A fresh city at head loads, saves and reloads in schema 26.**
   - **Run:** create a fresh FT city with the head build and let Persistent City start. Save once natively, reload in the same process, then reload in a fresh process.
   - **Asserts:**
     - `persistentcity:state_v1` has `"Schema":26` and visitor tuples numbering (factor − 1) × residents; the default factor is 2.
     - The `persistentcity:save-epoch_v1` success marker says `schema=26` and describes the payload.
     - Persistent City is active after both reloads, with no refusal in the log.
     - A second save after the fresh-process reload is read back as schema 26 with the same visitor IDs and resident Employment start days.
   - **rig-automated** (existing capabilities: `rig flow run`, fixture library `--fixture`, RIG-MODSET, FT `save`/`load`/`state`/`console`; pending rig capabilities: RIG-LIVE, and RIG-PCSAVE extraction of PC keys from a saved checkpoint).
2. **A schema-25 FT city is refused with the DevelopmentSchema message, and its food businesses stay native-owned.**
   - **Run:** load a disposable copy of a writer-25 FT city that has a PC-owned food shop (`pc-schema-25-ref@1`, or an FT city saved by the base build `e9995e5` after `shop_fixture` enrolment). Use the head build.
   - **Asserts:**
     - The log and status show "created by an older development build; start a new city".
     - Every `modData` key hash (state, authority manifest, receipts, accounting stop, epoch marker) is unchanged after load, after two native hours and after a native save of that city.
     - No accounting-stop or epoch marker is written.
     - From the hour after load, the shop gets native customers and native sales (`shop_state`/`shop_log`), and Persistent City books none.
   - **rig-automated** (existing capabilities: RIG-MODSET, FT `load`/`save`/`time`/`state`/`console`, `shop_fixture`, `shop_state`, `shop_log`; pending rig capabilities: RIG-PCFIX (`pc-schema-25-ref@1`) and RIG-PCSAVE key hashing).
3. **Recovery after a refused city.**
   - **Run:** in the same game process as check 2, load or create a fresh head city.
   - **Asserts:** Persistent City starts on the new save object, and the refused save is left as check 2 found it.
   - **rig-automated** (existing capabilities: FT `load`/`state`/`console`; pending rig capability: RIG-LIVE).
4. **Inspector words.**
   - **Run:** in the fresh city of check 1, open the resident inspector for a worker, a student and a retiree.
   - **Asserts:** the popup shows "Resident", the household words ("lives alone" / "household of N") and "employed since day D" / "studying since day D" / no employment, and no person or household ID.
   - **rig-automated** (existing capabilities: FT `console`/`shoot`; pending rig capability: RIG-LIVE and an inspector text read through the existing inspector console).
5. **Unchanged behaviour against the base.**
   - **Run:** the same seeded fresh FT city, run for one game day with the base build `e9995e5` and with the head build.
   - **Asserts:** PC order counts, receipts and wallet totals are equal.
   - **rig-automated** (existing capabilities: RIG-MODSET DLL-set selection, `rig flow run`, FT `time`/`state`; pending rig capability: RIG-PCTRACE, an export of the PC behaviour digest that `FreshCityTraceTests` computes offline).
6. **Ordinary-play performance.**
   - **Run:** interleave base and head windows on the reference fixture at idle and at the lunch peak, with the camera stationary and moving and the Ctrl+F8 panel closed and open.
   - **Asserts:**
     - mod-work p95 < 5 ms in every head window;
     - every mod-work spike ≥ 30 ms is flagged with its condition;
     - head-vs-base mod-work is within the matched noise band;
     - whole-frame numbers are reported separately;
     - zero save callbacks occur inside a window.
   - **rig-automated** (existing capabilities: `rig flow run` autosave guard, RIG-MODSET, FT `state`/`time`/`console`, RIG-VIEW `camera`; pending rig capabilities: RIG-LIVE, RIG-PERF-UNION).
