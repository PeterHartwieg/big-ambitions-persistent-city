# C4 market research contract (31 October 2026)

C4 is a pure, inactive engine on B0b (`c0af745`), a sibling of C2a, B1 and B2.
It adds no schema, population fields, charge, order, UI, bridge command, runtime
input reader or Update work. [C3 / PR #20](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/20)
found no safe native fee/evidence path. Paid research remains unavailable.
The only production entitlement provider, `NoPaymentPath`, always denies.
There is no setting to enable research. Only two flat future work settings are
loaded and clamped once: `MarketResearchCatchmentMetres` 500 (50–2,000) and
`MarketResearchConsumersPerUnit` 256 (16–1,024). No tier prices are introduced.

`MarketResearchEngine`, its source interface, cursor and input types are internal
to Core. Only `PersistentCity.Tests` and the standalone offline
`MarketResearchRegression` assembly receive friend access. The game build has no
caller. Engine construction and fixture cell inspection are unavailable through
the public API. `MarketResearchReport` has no public constructor or numeric
serialization property; `TryRead` is its sole public numeric exit. Every call
requires a delivered, paid, unreplaced grant for the exact entitlement and report
identity. A future C5 implementation must own durable proof; this interface does
not invent a payment transaction. Tests use an accepting fake only to prove that
the gate distinguishes the single valid combination from every invalid one.

## Frozen input and continuation

An offline owner supplies an immutable logical source at a held publication
boundary, identified by city, logical day, catalogue revision, place revision and
population/input revision. **Any changed input**, including budget, meals, traits,
assignments or locations, requires a new identity. No live `CityState`, Unity,
RLO, native market or route planner is accepted. A future runtime owner must
establish this freeze and its work-budget scheduling before using the engine;
C4 neither pauses simulation nor installs a scheduler.

The reducer borrows that frozen source. It does not copy the population, retain
rows/IDs in a report, enumerate shops or build a resident-by-shop matrix. The
source must yield unique stable consumer IDs in ordinal order, including distinct
represented slots. Duplicate or unsorted rows fail closed. Each unit examines at
most the clamped consumer cap, including excluded catchment rows. There are 64
fixed integer counters, an index, one previous ID and missing-data flags. No
transient cache or global report collection exists: a job holds one source
identity/accumulator and at most one completed report, replaced on invalidation;
the source has a 150,000-consumer cap. No cache eviction policy is needed.

The transient offline cursor is independently JSON round-tripped by tests. It
contains a version, query identity, total/index, last ID, flags and exactly 64
counters. Restore validates lengths, scalar bounds and group sums before cloning
the fixed accumulator. Source identity, scope, tier or roster-size mismatch starts
again at zero. Revisions are checked before and after each unit; a mid-unit
replacement discards that whole partial image. No numeric report exists until
the final unit. Cursor JSON never enters the population payload. These transient
cursor fields are not a proposed C5 persistence format.

## Provisional projection

Fixed metric/band codes are explicit enums; labels are derived only from those
codes (at most 64 characters), never native text. There are no resident rows.
Reports carry the logical day and coverage. Numbers describe **modelled demand
estimates**, including represented consumers, never guaranteed purchases, native
footfall, route counts or rival finances.

| Tier | Cells | Contents |
| --- | ---: | --- |
| Basic | 13 | Included consumers/represented subset; breakfast/lunch/dinner opportunity counts; funded/unfunded/unknown individual home-food basket; lower/middle/higher/unknown discretionary budget; unknown usual locations |
| Detailed | 62 | Basic plus five-band distributions for A2's nine active taste/lifestyle traits; usual work/study/other groups; unknown-trait count |
| In-depth | 64 | Detailed plus explicit unavailable loyalty distribution and rival market share |

Both city and address catchment coverage are accepted by the pure projection.
Home and usual work/study locations use inclusive 3D straight-line distance to
the frozen address centre. Distance is a walking-time **lower-bound estimate**,
not path reachability or a route query. Home/work overlap counts a consumer once.
Missing both locations never guesses catchment membership. The unknown-location
counter covers rows with either usual location unavailable, including otherwise
excluded rows; it warns that catchment demand can be an undercount.

Opportunity input is a frozen three-bit logical meal window, not a sales forecast.
Household funding is the individual's known basket status for that logical day,
not a family or a new household simulation. Discretionary budget bins are below
1,000 cents, 1,000–2,499 cents and 2,500 cents or more; these are report bands, not
research fees or new spending rules. Missing household/budget inputs have their
own counted unknown buckets; band counts cover known records. Any missing meal
input makes the meal totals unavailable. Any missing A2 traits makes the nine
trait distributions unavailable, with a counted unknown bucket. A known empty
cohort remains numeric zero. A2's unused loyalty predisposition is not substituted
for B1 experience, and D2 share inputs do not exist here: both final cells are null
with `Unknown` codes, even on an empty fixture.

## Offline reproduction

Run `python tools/market-research-regression/validate.py` for the sequential
foreground gate (including the other-worktree dotnet guard before commands).
Run `python tools/market-research-regression/compare_save_baseline.py` to export
tracked B0b Core/Persistence sources into ignored `build/c4`, compile the same
fixture against both heads, and assert literal UTF-8 population/food byte equality.
Neither script switches branches or accesses a rig. Raw gate logs and local
synthetic payloads stay ignored. The committed measurements and the dated
[CHECKS entry](../CHECKS.md#step-c4-market-research-engine--31-october-2026-offline-only)
record offline costs and the separate pending native proof.
