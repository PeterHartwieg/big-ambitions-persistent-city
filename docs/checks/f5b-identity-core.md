# City audit F5b — identity and ownership core, 4 November 2026, offline

Refs [#92](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/92). Base `dc59049` on `codex/persistent-city` (writer **25**; F1 #99, F3 #98 and the F5/F6/F7 plans merged). Scope: the F5b row of [the F5 plan](../plans/f5-population.md) section 4, under section 3's *Identity and ownership*, *Guards against duplication*, *Conservation and determinism* and *Contracts for F6 and F7*. Persistent City is not live or released. This slice is **implemented** (inactive, offline); none of it is verified by rig. Offline success is not native proof.

## Rechecked base citations

Re-read at `dc59049` before relying on them: `FoodCommerce.cs:166-167` (stale "sharing one resident wallet" summary, now corrected); `HouseholdFinance.cs:41-60` (budget seeded per consumer ID) and the private anchor rule `IncomeSource` (now L106-109); `FoodRepresentation.cs:30-31` (`ConsumerId`); `MealRhythm.cs:16-18` and `FoodDemand.cs:455` (intent IDs `food:<escaped id>:…`); `ResidentLifecycle.cs:74-117` (`pc:` remix draws and age bands); `Population.cs:9-55` (`StableRandom` FNV-1a with `Begin/Append/Finish`); `RloIdentityAdapter.cs:258,292` (`c<index>`); `Model.cs:256` (`CurrentSchema = 25`). Every cited behaviour matched; only line offsets moved.

## Implemented contract (inactive)

- **Types** (`Core/PersonIdentity.cs`): `PersonKind {Resident, Visitor}`. `VisitorStay {OriginPortalId, Pattern ∈ {Commuter, DayVisit}, Presence ∈ {Active, Dormant}, FirstDay, NextStayStart}`, with no money field. `Employment {PersonId, WorkplaceId, Kind ∈ {Work, Study}, StartDay, EndDay (Open = -1), ScheduleCode, ShiftDraw, NativeEmployment}`, where `NativeEmployment` has only `None` and stores no wage field.
- **IDs:** `PersonIds` classifies the B1 formats (`c<n>`, `pc:<n>`, legacy `consumer:<escaped anchor>:<slot ≥ 1>`, `pv:<n>`) strictly, checking shape only and never extracting an anchor. Order is ordinal. `HouseholdIds.For(home)` = `h:<homePlaceId>`, derived and never stored.
- **Derived identity** (`Core/IdentityPolicy.cs`, contract B6): `IdentityPolicy.V1(donors)` orders the preserved donor catalogue by ordinal ID and refuses an empty or duplicate catalogue. `Draw(seed, PersonId)` and `Derive(seed, PersonId)` use `Hash(seed, PersonId + ":identity:v1:<purpose>")` with the `pc:` remix's donor/surname/age-compatible persona rule. Its age bands are a frozen copy, so a later `pc:` change cannot move version 1. Visitors get an empty District (no home). Every draw carries `PolicyVersion = 1`. Nothing is persisted.
- **Income source** (`PersonIncome.Select`): an eligible active Work employment gives `ExternalEmployment`. Otherwise a carried source applies until the person's own employment outcome clears it. Otherwise retirees get `ExternalPension` and everyone else `ExternalSupport`. For residents this equals today's anchor rule (exhaustive test over persona × age × DayPlaceId).
- **Ownership validator** (`Core/OwnershipValidator.cs`): this is a pure fact-based validator; F5c will project saved state into `OwnershipInput`. It checks:
  - rule 1 (person ↔ wallet bijection; households, workplaces and visitor stays hold no money);
  - rule 2 (one own slot-0 roster record per person);
  - rule 3 (one existing owner per demand unit, with the intent ID derived from that owner);
  - rule 4 (strict ordinal person/catalogue order, which also refuses duplicate persons);
  - rule 5 (homes, portals and workplaces resolve to saved IDs);
  - per-kind shape (resident: home and no stay; visitor: stay, no home and no car under the Q1 default);
  - employment guards: one active employment per person, `NativeEmployment = None`, Work/Study kind matches the place, no day-visitor employment, and **no employment at a player-owned workplace** (F6 request C1);
  - F6 wage-key guards: at most one wage per `(PersonId, day)`, sourced from a covering Work employment at that workplace, and never a wage on Study.

  Every refusal names a rule and subject; at most 64 are stored, all are counted. Reused buffers give 0 B per warm call.
- **Peter's questions (plan section 9), defaults in named constants:** `PersonPolicy.VisitorsOwnCars = false` (Q1, subway and walking) and `PersonPolicy.MigratedConsumerPattern = Commuter` (Q3, today's extra customers become commuting visitors).
- **Inactive:** no simulator, codec, Runtime or Compatibility file references any F5b type; a unit test enforces this by scanning the sources. The save shape and writer 25 are unchanged, and the build and DLL contain only additional unused types.

### Contracts B1-B6 as implemented

| Contract | What F5b provides |
| --- | --- |
| B1 | Opaque ordinal `PersonId` with the four formats; legacy consumer IDs classify verbatim; `PersonIds.Visitor(n)` never reuses a sequence. |
| B2 | `PersonKind`, `VisitorStay` (Commuter recurs, DayVisit pool returns, Active/Dormant). Commuter visitors may hold one `Employment`; day visitors are refused. `HouseholdId = h:<homePlaceId>`. Bounds belong to F5c. |
| B3 | `Employment` with at most one active per person, Study never wage-bearing, `NativeEmployment = None`, and wages keyed `(PersonId, day)` into the person's single wallet (validator `WagePosting`). |
| B4 | Person wallets only: `MoneyOwnerKind.Household/Workplace/VisitorStay` is always refused. Household flows must be exact transfers between member wallets; F5b provides no pooled-balance type. |
| B5 | No body dependence in any F5b type; selection and lease authority stay unchanged. |
| B6 | Ordinal lists, integer FNV draws, and a derived, versioned, never-stored visitor identity. RLO and `MatchesPopulation` are untouched. |

## Offline evidence

- `tests/PersonIdentityTests.cs`: 78 cases. The red cases each refuse exactly one named rule:
  - second budget, orphan budget, missing budget, budget with finances off;
  - household, workplace and visitor-stay money;
  - duplicate, orphan, missing, anchored and food-off roster records;
  - intent with two owners (the former anchor claiming a migrated consumer's intent), duplicate intent, ownerless intent, underived intent;
  - two active employments, wage-bearing Study, duplicate wage, wage without a covering employment, `NativeEmployment ≠ None`, employment at a player-owned business, unknown workplace, kind mismatch, employment without a person, reversed employment dates, day-visitor employment;
  - resident/visitor shape, visitor car, invalid stay (undefined pattern, NaN), ID-kind mismatch, unknown home/portal, unordered or duplicate persons, unordered catalogue.

  Further cases check that results do not depend on fact-list order, that a reused validator carries no state between calls, that stored violations are bounded, and that intent derivation equals `Uri.EscapeDataString` for every printable ASCII owner and for non-ASCII owners.
- **Identity:** 12 frozen golden vectors (three seeds × four IDs), identical under reversed and rotated donor order and reversed query order. Draws equal an independent in-test FNV-1a reference across seeds including `int.MinValue`/`int.MaxValue`. Persona keeps the appearance donor's age band. Empty and duplicate catalogues are refused.
- **PersonId:** 26 classification cases, legacy `ConsumerId` round-trip for slots 1–2, the 2,000-sequence uniqueness of `pv:<n>`, and ordinal-vs-numeric order.
- Full matrix results: see *Validation run* below.

## Validation run

RESULTS_PLACEHOLDER

## Performance

Validator (0 B/call warm) p50/p95 at 1,914 / 10,000 / 100,000 persons: **0.6485/0.7140**, **3.6938/5.1875**, **49.2912/56.3153 ms**. `Draw` takes 53–64 ns and 0 B/lookup; `Derive` takes 132–233 ns and about 200 B/lookup. **Flagged:** a whole-graph validation at 100,000 persons is ≥ 30 ms (max 56.6–97.0 ms). It is not frame work in F5b, and F5c must run it detached or in bounded units. F5b adds no frame work, so in-game mod-work p95 and spikes are unchanged by construction; whole-frame timing is tracked separately and nothing native is claimed. Four perf wins in new code are logged with before/after numbers; the four parked hotspots are untouched. Full numbers and limits: [docs/perf/f5b-identity-core.md](../perf/f5b-identity-core.md).

## In-game checks for the next batched rig

F5b has no in-game surface of its own: nothing calls the new types until F5c wires the validator into the codec and F5c/F5e create persons and employments. Its section 7 checks therefore become live in R1, R2, R4 and R9 under those slices. For F5b, the next batched rig proves the slice is inactive and establishes the baselines that R4 and R8 later compare against. Every check is executed by the headless agent rig ([PeterHartwieg/big-ambitions-agent-rig](https://github.com/PeterHartwieg/big-ambitions-agent-rig)) in a named disposable **FT-F5b-Identity** city or a sealed fixture. The rig records autosave, disables it in the isolated profile, verifies it before every measured window (including after reload) and restores it on cleanup; explicit saves stay outside measured windows. No check is deferred to Peter, a playtest, an unlocked console or a by-hand visual check.

1. **Save text unchanged (inactive, writer 25; from R1/R9).** The rig loads the same writer-25 fixture three times: with the base build (`dc59049`), with the F5b build, and again with the base build as a determinism control. Each time it saves once immediately after load, with zero game minutes advanced, and reads the `persistentcity:state_v1` text from the isolated save. **Asserts:** all three texts are byte-identical, the schema is 25, the mod log has no refusal and the native money and `shop_state` reads match. **rig-automated** (existing capabilities: fixture library `--fixture`, RIG-MODSET, `rig flow run`, FT `save`/`load`/`state`, `shop_state`; pending rig capabilities: RIG-LIVE, RIG-PCFIX (`pc-schema-25-ref@1`), and RIG-SAVEKEY: extract and hash the `persistentcity:state_v1` text from an isolated save file without copying it out of the rig).
2. **No native employment (native part of R4).** In a fresh FT city with one `shop_fixture` business with real staff, the rig runs the F5b build for three game days at ordinary pace. It reads the business's native staff list and payroll through `shop_state` at the start and after each midnight. **Asserts:** the staff list and wage settings are unchanged, and the PC log shows no F5 person or employment diagnostic (nothing is wired). This becomes R4's baseline for F5e. **rig-automated** (existing capabilities: `shop_fixture`, `shop_state`, FT `time`/`console`; pending rig capability: RIG-LIVE).
3. **RLO identity contract and resident identities unchanged (B6).** On the same fixture under base and F5b builds, the rig reads the PC log's RLO contract verdict (`MatchesPopulation` periodic check) and, through FT `console`, the resident inspector identity of three `c<n>` and three `pc:<n>` residents. **Asserts:** the verdict passes in both builds, and names, ages and personas are equal between builds. Derived visitor identities exist nowhere in game until F5c. **rig-automated** (existing capabilities: RIG-MODSET, fixture library, FT `console`/`state`; pending rig capability: RIG-LIVE).
4. **Performance subset (R8).** The rig interleaves base and F5b windows on the reference fixture at idle and the lunch peak, with the camera stationary and moving (RIG-VIEW `camera`) and the Ctrl+F8 panel closed and open. It records mod-work p50/p95/max, allocations and lag, and records whole-frame metrics separately. **Asserts:** mod-work p95 < 5 ms in every F5b window, every single mod-work spike ≥ 30 ms flagged with its condition and component, and F5b-vs-base mod-work within the matched noise band, since F5b adds no frame work. Whole-frame numbers are reported separately and never used as mod-work p95. **rig-automated** (existing capabilities: `rig flow run` autosave guard, RIG-MODSET, FT `state`/`time`/`console`/`shoot`, RIG-VIEW `camera`; pending rig capabilities: RIG-LIVE, RIG-PCFIX).
