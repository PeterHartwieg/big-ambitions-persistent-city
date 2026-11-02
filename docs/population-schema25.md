# Population schema 25: canonical product identity

2 November 2026. E5d owns this contract. The next-free check at base
`034bb7629391ef0fedb61d7122b0d6ee3c451ff9` found writer 24 and no allocation
of 25. This is an inactive persistence contract. Native catalogue eligibility,
complete-menu ownership, handoff and checkout integration belong to E5a/E5b;
neutral metadata resolution/config belongs to E5c. No new product becomes managed.
Q1–Q4 in the E5 plan remain open; the header records the recommended neutral
inputs as a versioned contract, without using them to admit or rank new foods.

## Exact predecessor and migration

`PopulationSchema24*.cs` freezes the base reader, resolver, streaming prechecks,
semantic/count rules, converters, six fixed codes and 21/22/23 neutral-only rival
contribution rules. Its current/default schema is the literal 24. Production
captured/preferred products remain the original three. The earlier frozen
13–20 readers retain their source shapes. The only mechanical adjustment to old
resolvers excludes the new root/offer properties from their reflected DTOs.
Schema-24 codes never consult the schema-25 table. The matched benchmark also
compiles the actual archived base and compares its complete schema-24 bytes and
both counters with the frozen reader/writer.

The chain is the existing valid **13–24 → validated exact 24 → 25**. The entire
source passes its own shape, capability, count, identity and exact-money checks
before migration. The candidate is detached. Migration changes only the schema,
menu capability, new name table, next-day `NamePolicyStartsDay` and legacy held
metadata. Unknown named historical objects retain their object provenance.
Existing identities, assignments, traits, activation dates, exact money, scopes,
quotes, journeys, receipts/uncertainty, returns, outcomes, D3a outflow and D4
memory retain their values. No historical purchase is replayed or inferred.

The new boundary is `floor(Minute / 1440) + 1`, including population-only cities.
It is reserved for later name-policy implementation; E5d executes no new choice
algorithm at that boundary. Existing policy dates and already held terms do not
move. Ordinary config timing remains the next uncaptured-context rule.

The migrated graph and both serialized size limits pass activation preflight
before a successful read is returned. Store/session publication still occurs
only at the existing coordinated save boundary. A valid predecessor requiring
257 table names, or an old scope identity outside the new canonical-name domain,
refuses migration and preserves the original. There is no universal migration
claim, truncation or identity repair. Opaque historical objects do not acquire
the new canonical-name restriction.

## Headers and execution refusal

The normal required `MenuPolicy` is exactly:

`2|name=1|held=1|neutral=2,900,100,450`

The bounded revision-2 metadata storage shape has a distinct, known **cold
contract** header:

`2|name=1|held=2|neutral=2,900,100,450`

The codec can inspect and round-trip that cold shape. Revision-2 held tuples
require its header. This executable's **early authority capability check,
activation preflight and simulator constructor refuse the cold header**; it
cannot silently run those tuples through the old policy. E5c/E5b must implement
the actual consumers and recognize that execution capability together. Normal
runtime captures still use revision 1 and the established original-product
calculation. Codec `Loaded` for a cold contract denotes valid stored data, not
permission to construct a simulator; `ValidateForActivation` remains mandatory.
Unknown headers refuse rather than selecting an older policy.

Schema/capabilities/menu headers precede the population graph on write. Early
bootstrap checks only bounded headers; full decoding is still required for all
fields, duplicates, references and semantics, irrespective of root field order.
Writer-24 binaries report schema 25 as Future and preserve it. Downgrades are
unsupported. Root `ProductNames` and `NamePolicyStartsDay`, and offer `Metadata`,
are foreign fields in every predecessor source.

## Names and references

Required root `ProductNames` is an ordinal-sorted array of at most **256** exact
strings. Names are **1–256 UTF-16 code units**, not all whitespace, without
control characters, `|`, or unmatched surrogates. No trimming, normalization,
case folding, aliases, integer codes, dictionary order or native lookup is an
identity operation. Both raw and escaped Unicode must decode without Json.NET's
replacement-character recovery. Scope/held names remain strings in Core and
Runtime. Compact history cells alone use one-based wire references into this
table; references are decoded to names before a graph is returned.

The table represents encountered identities referenced by scopes, preferred or
held/committed meals, canonical current outcomes and compact retained history.
It is not the loaded catalogue. The writer reconstructs the whole table and all
compact references together at a coherent snapshot. The reader requires exactly
that referenced set and order: duplicate, unused, missing, unordered, invalid,
overbound and dangling entries refuse. Missing mods never retire a reference.
A name is retired only when no retained graph fact references it. Inserting an
earlier ordinal name may change a wire number, never the decoded identity.

The complete table allowance is **524,288 escaped characters and UTF-8 bytes**,
including its array, property name and separator (16 characters of field
overhead). Streaming checks bound each name and entry before collection
materialization, and check both actual source lengths. Write validation uses
the effective JSON escaping settings and both budgets. An original opaque
history object can contain its old up-to-512-character product text, including
text invalid for a new table, and remains an object even when another reference
to the same text is canonical. Such history grants no native authority.

## History and held image

A compact schema-25 product history cell is exactly:

`[nameRef,purchases,revenueUnits,legacyMeals,breakfastMeals,lunchMeals,dinnerMeals]`

All seven slots are canonical nonnegative integers; the reference is 1–256 and
must exist. The remaining six values fit nonnegative Int64, with the unchanged
exact-money and hourly/meal/product/district reconciliation. Maximum numeric
cell size is **125 ASCII characters**, within the unchanged **128** allowance.
The old code mapping is used only by the frozen old reader before migration.
Named opaque objects retain `ProductId`, `PurchaseMeals`, `Purchases`, `Revenue`.
Their in-memory provenance flag is neither a wire field nor a sale permission.

The existing **eight cells/business-generation/day**, **14 × MaxPlaces rows**,
and every count/revenue invariant remain. There is no 1,024-generations/day
validator or migration-time history pruning. E5b must preflight a ninth daily
product or a 257th required name before ownership/new sale; this slice adds no
native sale/admission path.

Each captured offer requires a six-integer tuple:

`Metadata: [revision,mealClass,tasteBand,breakfastFitCents,lunchFitCents,dinnerFitCents]`

- Revision **1** freezes E4's established inputs; revision **2** is the inactive
  name-based input shape.
- Meal class is `0 Unknown`, `1 Food`, `2 Drink`, `3 Bundle`, `4 NonFood`.
  These are captured authored scalars, not a native taxonomy or admission rule.
- Taste is **0–4**; each fit is **0–5,000** generalized-cost cents, never money.
- Revision 1 must exactly match the predecessor policy. The original three
  must always retain their saved taste bands, established Food class and original
  vectors, also in revision 2. Legacy meal execution still has zero fit penalty.

At migration, each legacy offer receives its original effective taste and vector
without changing the shortlist, quote cursor, tuning or routes. Manually built
legacy contexts can be captured at save preparation; ordinary original-product
contexts capture once at the existing capture event so observations and reloads
agree. No native/config read, generated trait, per-resident product table,
health input or daily tuning history is added.

At most **64 tuples** belong to the one held image. Each is one charged record
in both counters. Streaming rejects tuple 65 and any missing/null/bad/duplicate
metadata, unknown revision/class, wrong arity, noncanonical integer or range.
The aggregate raw tuple text, field/separator overhead and image reserve must fit
**8,192 characters and UTF-8 bytes**, checked before typed deserialization.
Generated tuples need at most 24 ASCII characters each plus 12 field/separator
characters and two image brackets, conservatively **2,306** at width 64.

## Capacity and unavailable native evidence

Table entries add at most **256 records**; held tuples at most **64**. History
cells remain one record each; scope additional IDs retain their streaming charge.
Composition uses the larger of streaming and semantic counts. The total new
allocation is **540,672 characters/bytes** (512 Ki table + 8 Ki held + 8 Ki
header reserve); no old reserve is reclaimed in the conservative CF comparison.
The hard caps remain **268,435,456 characters**, **268,435,456 UTF-8 bytes**,
**4,000,000 records**, depth **48**, 50,000 residents, factor ≤3 and 20,000 places.

The codec never consults loaded definitions. `foodtrucks:itemname_dumplings` and
`foodtrucks:itemname_choppedcheese` retain exact names through absent/returned or
reordered catalogues. An absent definition is unavailable native evidence,
not corrupt history or permission to substitute another product. E5a/E5b own
that evidence and whole-business service/handoff behavior; this contract does
not implement or prove them. Conditional offline histories/held images are not
observed in-game purchases.

[Offline checks and numbered rig handoff](checks/e5d-name-identity-save.md).
[Measured sizes, both counters, latency, retained memory and limits](perf/e5d-name-identity-save.md).
