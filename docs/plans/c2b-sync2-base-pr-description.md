# C2b: compose the latest base (E4 schema 23 and E3 pizza)

Historical handoff for `8378efa`. Its schema/refusal and measurement claims apply only to that source. The current supported composition is **20 -> 21 -> 22 -> 23**, with writer 23; see [schema 23](../population-schema23.md) and [current D2 sync handoff](../reviews/2026-11-01-d2-sync2-base-pr-description.md).

Fixes Sol-r1-1, Opus-r1-1 and Opus-r1-2. Merge development head `2bd9006`
into prior sync `d2ba6d2` from merge base `75c956c`, retaining two-parent merge
ancestry. Target remains `codex/pc-c2b-campaign-awareness`; no push or remote PR
merge is performed here. Both parents' diffs against the base were inspected.

The base advanced again during validation to **`8c6b3f3` (E3 pizza)**. A second
real merge, `84348e1` (parents `4dc81d1`, `8c6b3f3`), includes that latest head.
Its sole conflict was README's adjacent C2b/pizza notices; keep both plus all
E4/E3/A5/F1 notices and all checks/perf/plan lines. Both sides' diffs against
`2bd9006` were inspected. The pizza import is read-only and adds no schema.

Pizza's descriptor exposed a frozen ice cream baseline reconstruction assertion.
The harness now strips only the pizza descriptor before applying the original
three-candidate hash. Ice cream/pizza guards normalize only that exact harness
and guard edit; original hash values remain and all other code is still pinned.
This is a test-tool integration fix, with no production reader changes. All
Runtime E3 files match `8c6b3f3`; the E4/Core/persistence composition below is
unchanged by the second merge.

At this historical C2b handoff, the E4 predecessor rule gave **20 -> 22 -> 23**. Schema 21 was then
reserved (now supported below C2b); >23 is future. Exact 22 includes bounded campaign images/held source
bits but excludes the menu header, revision-2 tuning and compact product cells.
Its menu/history/offer limits remain four. The menu migration validates this
predecessor before adding E4's policy header; no facts, identities, weather,
money, campaign masks or held decisions are regenerated.

The E4 merge's 20 textual conflicts and resolutions (plus the subsequent README conflict above):

| File | Resolution |
| --- | --- |
| `CHECKS.md` | Keep C2b and E4 entries plus every existing slice index line, including ice cream. |
| `Core/FoodChoicePolicy.cs` | Keep CampaignActive and NewTasteBand; campaign distance/preference and E4 taste weighting both apply. |
| `Core/FoodWeightedCommerce.cs` | Pass both captured campaign activity and E4 new-taste projection; keep menu capture limits/refusals. |
| `Core/Model.cs` | Write schema 23 and retain E4 MenuPolicy and E1 weather. |
| `Persistence/PopulationWire18.cs` | Bootstrap accepts exact 22 and 23; 23 requires its menu policy; 21 refused at that historical head (now supported). |
| `Persistence/SaveCodec.cs` | Add exact schema-22 resolver; preserve old limits and campaign checks; migrate 20 -> 22 -> 23; keep E4 cells/tuning/counts. |
| `Persistence/WeatherWire20.cs` | Retain one equivalent exact 19 -> 20 migration under C2b's existing method name. |
| `README.md` | Keep C2b/E4/E3/A5/F1 notices; current source writes 23 and accepts predecessors 13-20 and 22. |
| `Runtime/PersistentCityMod.cs` | Keep E4 maximum-offer constructor input and C2b campaign attachment/clamping. |
| `docs/roadmap.md` | Keep C2b and E4 perf entries, all status lines and existing performance gates. |
| `tests/A5IntegratedAcceptanceTests.cs` | Use CurrentSchema while preserving all migration/source/weather invariants. |
| `tests/FoodAwarenessMemoryIntegrationTests.cs` | Keep 18/19/20/23 and add 22; remove only fields absent in each exact predecessor. |
| `tests/MarketResearchTests.cs` | Retain exact byte noninterference and CurrentSchema assertion. |
| `tests/Schema17Tests.cs` | Future refusals remain above CurrentSchema, now 24/25/1000. |
| `tests/Schema18ReviewTests.cs` | Header assertion follows CurrentSchema; historical reserved-21 refusal; current tests instead refuse 24. |
| `tests/Schema18Tests.cs` | Mutate the actual current header for noncanonical refusal; keep all corruption cases. |
| `tests/WeatherTests.cs` | Preserve current-schema assertions and both menu/campaign removal from old source shapes. |
| `tools/awareness-regression/Program.cs` | Project out weather, campaigns and menu header for the frozen awareness comparison; label total extension bytes. |
| `tools/market-research-regression/compare_save_baseline.py` | Use exact schema-23 saved-byte equality without weakening the differential. |
| `tools/save-contract-regression/WeatherContracts.cs` | Separate menu/campaign/weather projections and ledgers; verify exact 20 migration and unchanged predecessor fields. |

Integration seams do change Core/Runtime/persistence code: the evaluated offer
carries both existing inputs; runtime construction keeps both settings; exact
22 validation/conversion sits below E4. `MenuPredecessorSchema` advances to 22.
No campaign probability, distance factor, menu policy or native supported-product
rule is redesigned. C2b remains 1.25 preference / half soft distance for all
residents; E4's effective live maximum remains four. E3 reader/source pins and
all other slices remain intact.

Fixture-only integration also updates C2b's old-20 shapes, adds 22 to E4's
migration/old-limit tests, keeps awareness/weather/campaign/menu byte charges
separate, and names E4's new-taste constructor argument to preserve both inputs.
A new interrupted-decision regression verifies exact schema-22 campaign/rain
captures through migration, reload and changed live settings.

Final validation on **`8378efa`**: **65/65 foreground gates pass**, **2,435 tests
passing and discoverable**, zero failed/skipped; production build, all **40
top-level regression projects**, four Python adapters, three simulations,
continuity/optional-stop checks, frozen reader baselines/source guards, C4 exact
saved bytes, five A5 combined modes and E4's exact default-economy comparison
against `51ce709`. All gates used the prescribed other-dotnet guard; no command
time limit or detached tests. The initial new-test setup failure, its focused
retest, the successful E4-only 63-gate intermediate run and the later frozen-
baseline assertion/fix are all retained in the evidence.

Current schema-23 population fixture: **120,257 characters**, digest
`E9ACB14208DFFB98500039FE30CDDA3545CFAE3DAA2EE8CAA45A08FEC810F974`;
stripped projection **119,083 characters**, with unchanged prior digest.
Economy/order/reload digests and twelve F0 transitions are unchanged. C4's
**41,860 / 66,348-byte** fixtures are equal with/without its engine. E4's raw
8-product history fixture measures **10,135,880 B** (14,336 rows / 114,688 cells).
Campaign byte/record charges remain unchanged. Current C1 cold p95 **6.0568 ms**
above 5 ms, C2a setup >=30 ms samples, E4 timed serialization >=30 ms and separate
aggregate/save timings remain qualified in the [owning checks](../checks/c2b-campaign-awareness.md)
and [machine record](../checks/c2b-e4-sync-r1-results.json). None is a native
mod-work/whole-frame result or a new performance win; parked hotspots remain.

Final fetch still reports **`8c6b3f3`**, already included; the working tree's only
post-validation edits are evidence/documentation. Foreground runners exited and
no owned test process remains. No push or remote PR merge. Earlier results
below are historical ice cream-only measurements at `75c956c`, not this current
schema-23 composition. All prior evidence and its limitations are retained.
Native proof remains batched under merge rule v2 on the combined locked rig;
input-only proof waits for Peter's 6 Nov playtest. No game/rig/bridge/profile,
Steam/Workshop or WSL operations, release/upload or normal-save deployment.

## Historical ice cream-only sync (base 75c956c; superseded by the E4 integration above)

# C2b: sync the E3 ice cream integration base

Merge `75c956c` (`codex/persistent-city`) into C2b head `ffe85a0` with a real
two-parent merge, `033ba81`, from merge base `0ffb819`. Target this PR at
`codex/pc-c2b-campaign-awareness` and retain its merge ancestry when landing.
Both sides' diffs against the merge base were inspected before resolution.

The sole textual conflict was `README.md`: keep both the C2b campaign status
paragraph and E3 ice cream's independent feasibility STOP/conditional-reader
paragraph, followed by the existing A5 and F1 notices. The automatic compositions
in `CHECKS.md`, `docs/roadmap.md` and `docs/plans/steps-b-e.md` retain all incoming
ice cream entries alongside C2b and every existing slice. Both C2b and A5 perf
index entries and their evidence files remain unchanged. Historical measurements
and schema-20 ice cream evidence retain their original scope; they do not become
schema-22 or current-head measurements through this merge.

No integration fix or design change was needed. No C2b Core, Runtime,
persistence, test or harness code changed. The only Runtime differences from
the C2b parent are the incoming, unchanged E3 ice cream facade and its single
shared-reader descriptor; every E3 source/tool file matches `75c956c`.
No other slice code changed. Schema remains 22 with the approved E1 19 -> 20
then C2b 20 -> 22 chain at that historical pre-E4 head; 21 and 23 were then refused (both now supported). Campaign
preference remains 1.25 with half soft distance for all residents in shortlist
and final weighting under Peter's decision, with the existing observation,
failure-isolation, bounds and held-input contracts preserved.

The merge-tree preservation audit checks all 1,351 tracked paths: 1,347 exact
parent blobs outside the four composed documents, all checks-index lines from
both parents, and every added line from both sides of those documents. No
source hash pins were changed.

All **60 foreground gates pass** on `033ba81`: production build, **2,416 tests**
passing and discoverable (zero failed/skipped), all **38 top-level regression
projects**, four Python regressions, three simulations, continuity pickup,
optional-stop performance/findings, reader source guards/frozen baselines,
C4's exact saved-byte comparison and five combined A5 acceptance/benchmark
modes. Every dotnet test/run, including Python children, used the prescribed
other-dotnet guard. No command time limit, detached tests or remaining owned
test process. The final fetch still reports `75c956c`, already included.

The schema-22 save fixture remains 120,184 characters with the same prior C2b
digest, economy/order/reload fingerprints and twelve F0 transitions. C4's
41,787 / 66,275-byte fixtures are identical with/without its engine. Campaign
wire charges and reader assertion counts/allocations reverify unchanged.
The current offline C1 cold p95 **6.713 ms** is flagged above 5 ms; C2a setup
>=30 ms samples, aggregate rush CPU and separate save/read/migration timings
remain explicitly qualified in the
[owning C2b checks](../checks/c2b-campaign-awareness.md) and
[machine record](../checks/c2b-sync2-base-results.json). Older measurements and
byte records remain historical, not silently relabeled as current.

All local validation is offline. Under merge rule v2, native checks remain batched into the combined
locked `rig_test` (Codex Sol, high) on the development branch head; pointer/input
checks wait for Peter's 6 Nov playtest. No per-PR rig proof, release/upload or
ordinary-save deployment approval is claimed. No game, test rig, bridge lease,
Steam/Workshop, normal-profile or WSL operations were performed. No new
performance win or combined native/whole-frame performance result is claimed;
the four parked hotspots stay parked.
