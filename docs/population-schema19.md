# Population schema 19: C2a bounded awareness

Contract owner: pc-c2a-core-awareness, 31 October 2026. Base: B0b `c0af745`.
The B?E plan is read from `codex/persistent-city` at `6d43276`; it is not merged
into the original C2a branch. The 1 November sync incorporates the landed B1/B2,
C1, C3, D1, F0 and E2c slices; their contracts remain intact. Schema 18's B and tuning
contracts stay unchanged. `persistentcity:state_v1`, receipt and authority keys,
exact money, the four-offer menu limit and the global input/record caps stay fixed.

## Admission and replay

After activation, the one logical capture samples memory (four direct complete
generation references), home/work-or-study/current origin (up to four draws each),
and eight daily discovery slots. The optional campaign provider is unknown and
is never called: its eight reserved slots admit nothing. In the synced tree, the
default memory provider projects B1's four saved generation references while B is
active, omitting expired entries under B1's existing policy. It never writes memory
or substitutes a replacement generation. B1's existing bounded contribution applies
to the common shortlist rank and is captured per selected shop for offer selection;
held decisions never recompute it. Explicit provider overrides remain supported.
For an existing
shop stop, direct observation occupies Origin slot 0 deterministically (including
when random draws are zero), replacing one of the four origin slots rather than
adding slots. The same eligibility and complete feasibility checks still apply.

`FoodAwarenessCatalogue.Entry` is pure business-generation/place/position/kind
metadata. External metadata can enter the same catalogue without enrollment in
`FoodCommerceState.Shops` or native authority. C2a supplies managed metadata only;
D2 can supply external entries and its own feasibility/settlement contract through
the same Sample/Rank interface. Both kinds compete for the same eight positions.

The immutable index sorts complete generation IDs ordinally and hashes length-prefixed UTF-8 IDs/places, little-endian kind and exact
binary64 positions through a 64 KiB buffer into a canonical SHA-256 revision. Six cell
scales (32, 64, 128, 256, 512, 1,024 metres) select the smallest side at least as
large as the radius. Each anchor examines a fixed 3?3 cell neighbourhood. A draw
selects from the sum of bucket masses, then indexes one slot in that bucket. Dense
buckets are never walked. Distance, invalid evidence, missing generations and
duplicates consume their slot without a refill. No routes run during admission.

Discovery gates and indexed slots use independent policy-v1 hash domains keyed
by city seed, stable consumer, logical day, source, slot and catalogue revision.
They exclude meal/context/attempt and settings. Every supported indexed generation
has positive discovery probability, including far-away businesses. Later contexts
on the same day/revision derive the same discovery; no daily ID list is saved.

The union has at most 24 distinct C2a identities (32 is the shared future envelope).
`Sources` records sampled provenance, including a failed local-radius draw;
`Eligible` is true only if at least one source exposed a currently eligible
identity. The diagnostic retains per-slot acceptance reasons. Deduplication ORs
those flags, then sorts IDs ordinally. A3's weighted insertion rank examines at
most 32?8 positions and returns at most eight. Cheap factual refusal, complete
outward/return paths, price, stock, hours, queue, traits, deadline and weighted
outside choice still apply. No duplicate, missing capture or infeasible offer
adds another slot or route request. Existing continuation remains <=16 logical
route requests, one held context city-wide, four contexts per meal.

Core prepares the index through bounded continuation after first use, geometry/
identity/menu publication, list replacement/count change, or explicit
`InvalidateFoodAwareness()` after a place/in-place catalogue edit. Each work unit
handles at most 64 source entries, merge-sort writes, hash entries or cell entries.
There is no whole-catalogue sort, conversion, copy or hidden missing-place scan
inside a unit. Private six-level data publish only after completion; the next
logical event stays unprocessed at its minute, with no context attempt consumed.
Index work uses the existing total-unit/time caps and food-continuation counter.
Input changes discard/restart the private image before publication; save/reload
restarts that uncaptured event with the same ordering. Held decisions continue
with captured inputs, and stop membership/commit bookkeeping cannot trigger a
geometry build. Before activation, or while the published image is stale, stop
membership retains the live legacy scope lookup, so late authority acquisition
is visible without reload; a diagnostic-built image cannot freeze membership.
Explicit on-demand diagnostics may finish preparation themselves.
Unchanged consumers do O(1) publication checks and bounded draws. Native discovery uses its existing 64-controller ring and initial
startup pass, increments the geometry revision for scope identity/place/menu,
withdrawal or scope-list changes, and rechecks sampled native eligibility directly.
Candidacy add/remove alone never invalidates the geometry index. Capture no longer scans all controllers or refreshes every
shop's routine hours per meal. Detailed supply/hours are still captured live for
only the winning identities. Retired scope slots may be sampled and refused until
the next publication; they never authorize a sale. The existing hourly ownership
and closure work is not redesigned.

## Additive wire fields

- `FoodCommerceState.AwarenessStartsDay`: integer 0?current day+1. New scopes and
  13?18 migration start new awareness contexts on the next logical day. Loaded
  tuning changes affect the next uncaptured context, with no tuning midnight wait.
- `FoodDecisionState.Awareness`: null for an inherited A3 decision, otherwise one
  object containing `CatalogueRevision` (canonical 44-character SHA-256 Base64)
  and `Union` (0?24 records sorted by complete business identity).
- Each union record contains `BusinessId` (1?512 characters), `Sources` (nonzero
  flags Memory=1, Home=2, Routine=4, Origin=8, Discovery=16), and boolean `Eligible`.
  No campaign bit is accepted in schema 19. Each source has at most four distinct
  identities, except discovery's eight. Every saved shortlisted shop must appear
  as eligible in this image. Full shortlisted native terms/routes remain the
  existing A3/B0b image; no awareness matrix or campaign image is stored.

Schema 18 has its own filtered resolver/shape contract and rejects either new
field. It validates its complete old shape before migration, retains existing
held shortlist/quotes/selection/tuning, and sets the activation day once. An
inherited held decision finishes without reranking. New contexts capture once;
continuation and reload never resample or rerank live awareness settings, memory,
catalogue or sources for that decision. Schemas 13?17 still use their frozen reader.
Unknown/future schemas and malformed images refuse without source replacement.
A null held image is accepted only when the owning meal predates
`AwarenessStartsDay`; null after activation refuses on both read and write, so it
cannot bypass shortlist-to-admission validation. Old binaries also refuse schema
19 at the early authority bootstrap.

The existing fixed consumer tuples are unchanged. Only one held image adds
records: one charge per union entry in both streaming and semantic counters,
maximum 24. There are no new per-consumer/day records. The city activation header
uses the existing 64 KiB policy/header allocation; the one image consumes the
existing 1 MiB decision reserve. Worst-case escaping of 24 bounded IDs is well
below that reserve. Capacity tooling subtracts actual consumed bytes and source
records once from those reserves, preserving B0's total future envelope.

For the 24-source native-format-ID fixture: **3,038 image bytes + 13 field bytes +
23 activation-header bytes = 3,074 added UTF-8 bytes**, once per city, at both
1,914 and 5,000 consumers. No held context costs only the 23-byte header (plus
any extra decimal activation-day digits); a legacy held null image adds 17 bytes.
Full capacity measurements and 512-character-ID stress are in CHECKS.

## Settings and bridge diagnostic

The unchanged flat config loader reads `FoodMarketingNeighbourhoodMetres`
(default 250, range 25?1,000), `FoodMarketingNeighbourhoodDrawsPerAnchor` (4,
integer 0?4), and `FoodMarketingDiscoveryDailyProbability` (.02, .001?.20).
Missing keys default; numeric strings and endpoint infinities follow the existing
conventions. Invalid types/NaN/in-range fractional integer keys default, and
clamping/defaulting is logged once. No watcher or file rewrite is added.

`PopulationRuntime.FoodAwareness(consumerId, day?)` delegates to pure
`CitySimulator.InspectFoodAwareness`. It returns raw source slots, their union,
deduplicated flags, catalogue revision and proposed shortlist, plus the separately
labelled held image/shortlist when one exists. Proposed sampling/ranking uses the held
stop/trip mode (or the current stop mode before capture), matching its admission
filter and fixed Origin slot. Join complete IDs to saved scopes
and places for positions. This diagnostic builds text/data only when called,
creates no meal context or route, reads no campaigns/supply, and writes no state.
A day override derives a hypothetical day's sources; it is not a journey or sale.

Offline proof does not certify native accounting, timing or visible journeys.
The numbered [6 November rig checklist](checks/c2a-core-awareness.md#in-game-checks-for-6-nov-rig)
remains pending under the combined-head batched rig rule; its sync amendments
cover B1's populated memory and Peter's input-only playtest checks.
