# C2b: merge the schema-20 integration base into campaign awareness

Under **merge rule v2**, these in-game checks are batched into the combined
locked `rig_test` (Codex Sol, high) on the `codex/persistent-city` head; no per-PR
rig proof is required or claimed. Pointer/input-only checks wait for Peter's
6 November playtest. Offline fixtures do not establish native hourly ordering,
noninterference or combined mod-work cost. The combined session must attach
before/red -> after/green dumps/logs/screenshots; this sync performs no game/rig
work and grants no release/upload approval. LOCKED-OK classifications follow
feasibility commit `84be71a`.


## Sync-base integration (1 November 2026)

Target: `codex/pc-c2b-campaign-awareness`; keep the merge commit ancestry when
landing this sync. Merges integration head `0ffb819` into C2b `992291e`, based
on merge base `51ce709`. Both sides' diffs against that base were inspected for
every conflicted file. No rebase, squash, cherry-pick, push or game launch.

All 16 conflicts and resolutions:

| File | Resolution |
| --- | --- |
| `CHECKS.md` | Keep C2b, A5 and E1 entries together; preserve every existing entry including F1. |
| `README.md` | Keep C2b notice plus A5 acceptance and F1 NO-GO notices; move sync evidence into C2b checks. |
| `docs/roadmap.md` | Keep both C2b and A5 perf index entries and all incoming F1 roadmap work. |
| `Core/Model.cs` | Current schema stays reserved 22; retain E1 WeatherStartsDay and citizen weather. |
| `Persistence/SaveCodec.cs` | Exact schema-20 resolver, both converters/filters/validators; older schemas go through E1 20, then isolated 22. |
| `Core/FoodChoicePolicy.cs` | Compose weather distance weight with half distance and fixed 1.25 active preference; preserve both call forms. |
| `Runtime/PersistentCityMod.cs` | Clamp both weather and campaign settings; retain weather attachment and campaign market hook. |
| `tests/FoodAwarenessMemoryIntegrationTests.cs` | Keep 18/19/20 cases, held rain/contributions/replay and exact predecessor field removal. |
| `tests/FoodAwarenessTests.cs` | Remove both campaign and weather additions from legacy-18 fixtures; keep current corruption tests. |
| `tests/MarketResearchTests.cs` | Compare city schema with CurrentSchema while retaining byte-identical research noninterference. |
| `tests/Schema17Tests.cs` | Future refusal uses schemas above current 22; old fixture/refusal contracts retained. |
| `tests/Schema18ReviewTests.cs` | Current header follows CurrentSchema; unsupported 21 still refuses. |
| `tests/Schema18Tests.cs` | Mutate current schema for noncanonical-header test; retain E1's weather-suffixed memory corruption. |
| `tools/awareness-regression/Program.cs` | Project out weather and campaigns only for the frozen awareness-19/legacy-18 byte comparison; label total new wire delta. |
| `tools/save-contract-regression/AwarenessContracts.cs` | Keep weather projection and charge empty campaign field exactly once; preserve record/refusal assertions. |
| `tools/save-contract-regression/CapacityBench.cs` | Deduct both actual weather and campaign bytes/records once from the unchanged future envelope. |

Minimal integration fixes beyond conflict markers: the automatically merged
bootstrap allow-list now accepts 20 while still refusing reserved 21; repoint the
isolated campaign predecessor to 20; keep the E1 migration as a separate 19 -> 20
step. Fixture/tool projections now use exact old shapes, and C2b's migration
case preserves populated weather and a held rainy decision. One regression checks
both factors together; held campaign replay also captures rain. A5's four legacy migration cases also update their current-schema expectation
from 20 to 22; all facts, money, inspector and source-preservation assertions
remain. The C4 exact-byte differential now pins merged schema 22 instead of 20;
its zero-byte-difference assertion stays exact. No cap, native
ownership rule, campaign observation/buffer logic, accounting or A5 behavior changes.

Runtime/Core code did change at the integration seams: `FoodChoicePolicy` combines
both existing factors, `Model` retains both schema/weather additions, and startup
retains both settings/attachments. Other E1/A5 code and all C2b ownership sampling,
one-shot isolation and private-buffer implementations are preserved from their
parents. Persistence and offline fixtures/tools changed as detailed above.
No new feature or slice policy/design change is introduced.

Validation results and source preservation: [C2b owning checks](../checks/c2b-campaign-awareness.md).
All older numbers below are historical C2b measurements against `51ce709` before
E1 and A5, including digests/byte counts; they are not merged-head measurements.

Peter's decision 1 (1 November): "It should simply increase the chance for
residents to pick that shop, also at greater distance." This supersedes the
previous targeting proposal. No demographic/type-to-resident mapping or Unknown
target gate remains; FoodMarketingTargetMatchMultiplier is removed. Verified
observed active campaigns use the same modest fixed 1.25 preference and half soft
distance input for every resident, in both shortlist and final offer weighting.
Price, stock, hours, route time, deadline, traits and outside choice still apply;
no journey or sale is guaranteed. No native campaign or fee write/purchase.
Paid research remains unavailable.

Boundary fixes: sample after ownership transfers / ActivateScope, also on hours
without transfers. Missing reads for a retained scope age the old masks with no
new bits; only absent/replaced generations retire. Optional observation failures
log once and latch the observer off for the session, preserving the previous
complete image and allowing ownership accounting to finish.

Schema 22 still adds only bounded campaign images, no resident arrays or new
held fields. ActiveCampaign=64 reuses the held Sources scalar as a modifier,
separate from at most eight campaign admission draws (bit 32). Existing 32-source /
eight-shortlist / 16-route bounds remain. The modifier is captured once and
survives stop/disable/reload without reranking. Schemas 17-19 remain unchanged;
exact 20->22 migration, after E1's 19->20 step and strict unknown/future/source preservation remain.

Historical pre-E1 offline validation: 2,359 tests and all 33 foreground gate commands pass. The final full-suite recheck after the private cache optimization also passes;
production build and save-contract/order/economy/reload digests remain green.
The actual production adapter harness covers all residents, retained history
with zero candidates, native-state read noninterference and one-shot failure
isolation. The actual ownership-hook harness passes 243 assertions including
acquisition with a running campaign and stopping before the next hour. Core
checks show higher seeded pick probability, weaker distance decay, neutral
absent/disabled behavior, packed-modifier replay and unchanged bounds.

Historical pre-E1 offline Release .NET 10 at 1 / 1,024 images:

| Component | p95 ms | Max ms | Allocation B/op |
| --- | --- | --- | ---: |
| C1 + publication | 0.0151 / 2.6336 | 0.0183 / 3.3697 | 2,880 / 2,198,552 |
| Day/type rebuild | 0.0032 / 0.4893 | 0.1667 / 0.6301 | 72 / 16,440 |
| Eight-slot source | 0.0062 / 0.0065 | 0.1908 / 0.0358 | 0 / 0 |
| Actual all-resident production adapter | 0.0127 / 4.2356 | 0.0322 / 8.9763 | 3,244 / 2,358,090 |
| Absent count probes | 0.0002 / 0.0849 | 0.0150 / 0.7460 | 0 / 0 |
| Disabled | 0.0001 / 0.0001 | 0.0001 / 0.0164 | 0 / 0 |

All final round-1 series above have zero spikes >=30 ms. **Initial round-1 p95
exceeded 5 ms** (publication 10.0878 ms/max 28.8639; production adapter 8.8877
ms/max 14.3554). Those results remain recorded. Reusing private active-hour
buffers removes publication allocation 2,300,768 -> 2,198,552 B/op and rebuild
allocation 118,656 -> 16,440 B/op. A derived neutral marker avoids per-offer
held-image scans: eight lookups p95 0.0028 -> 0.0002 ms, 0 B on both sides.
Host/JIT/GC variability affects wall times; allocation removal has direct proof.
Separate C1 cold 1,024 diagnostic p95 6.3118 ms/max 11.4315 is flagged above 5 ms.
The older initial implementation also recorded two >=30 ms hourly spikes and
six >=30 ms inherited C2a full-build setup samples; they remain explicitly
flagged in the slice history. No parked hotspot is rewritten.

Images remain 265 ASCII characters; field + 1 / 1,024 images add 282 / 274,446
bytes and 1 / 1,024 records over schema 19 with existing shared references.
Packed modifier bit 64 adds at most 32 decimal characters/city and no record or
field. No-campaign economic/order/reload digests remain unchanged. There is no
absent campaign source/index work; bounded native count probes still cost CPU.
These component figures do not certify combined native mod-work p95 <5 ms or
spikes <30 ms. Whole-frame intervals/stalls must be measured separately.

[Checks and review fixes](../checks/c2b-campaign-awareness.md),
[performance evidence](../perf/c2b-campaign-awareness.md).

### In-game checks for 6 Nov rig

Under **merge rule v2**, these in-game checks are batched into the combined
locked `rig_test` (Codex Sol, high) on the `codex/persistent-city` head; no per-PR
rig proof is required or claimed. Pointer/input-only checks wait for Peter's
6 November playtest. Offline fixtures do not establish native hourly ordering,
noninterference or combined mod-work cost. The combined session must attach
before/red -> after/green dumps/logs/screenshots; this sync performs no game/rig
work and grants no release/upload approval. LOCKED-OK classifications follow
feasibility commit `84be71a`.

1. **NEEDS-INPUT - prepare a native campaign and acquisition.** In a named
   disposable FT fixture, prepare a supported player food business with one
   running verified native campaign before its safe authority-acquisition hour.
   Record business generation, campaign type/enabled/agency state, native fee and
   clock. Start/stop campaigns through the native agency UI only; no PC campaign
   write or purchase. Native UI setup and visual confirmation wait for input.
2. **LOCKED-OK - first eligible boundary and stable hours.** Via bridge capture
   authority FromHour, scope/candidacy, `ReadCampaignSnapshot`, and
   `CampaignAwareness()` before/after acquisition. Verify campaign publication
   follows `row.Boundary`/`ActivateScope`, with the first eligible hour bit present.
   Repeat on an hour with no transfers and during permitted hidden-skip boundaries.
   No intermediate guessed hours, partial images, or skipped first observations;
   keep <=1,024 images / eight types / eight days. A failed optional observer must
   report one latched session error without interrupting native ownership.
3. **LOCKED-OK - all residents and active probability.** Compare active versus
   stopped fixture copies across resident/represented-slot traits, including
   near and distant aware shops. Join `FoodAwareness` proposed/held images to
   current observed masks: source bit 64 is the captured active modifier, bit 32
   is bounded campaign awareness. Verify no demographic or type-to-group gate.
   Active shops get the same 1.25 preference and half soft-distance input for
   every trait profile in shortlist and offer weighting; price, stock, hours,
   routes, deadlines, outside choice and traits still decide. Compare seeded
   selections/weights, without demanding a guaranteed purchase. Preserve
   <=eight campaign draws / 32 union / eight shortlisted shops / 16 routes.
4. **NEEDS-INPUT - stop before the next hour.** Stop the acquisition fixture's
   campaign through the agency UI before its next accounting boundary. Record
   exact enabled/list state and clock; UI interaction requires unlocked input.
5. **LOCKED-OK - stop, temporary ineligibility, retirement and fade.** At the next
   boundary show the acquisition-hour evidence survived but no stopped-hour bit
   or active modifier was invented. Exercise one boundary with no current
   candidates while the same generation remains in saved scopes: old masks age
   without new evidence, and candidacy still refuses admission. Restore candidacy
   and verify retained awareness. Advance through midnight/day 1-8 to prove
   retention/decay/expiry. Remove/replace the actual generation and verify only
   that retired history drops. A stopped campaign retains awareness, not its
   active choice modifier; other discovery/memory sources remain independent.
6. **LOCKED-OK - save/reload and native noninterference.** Save/reload disposable
   running/stopped copies outside timing windows; compare masks, complete IDs,
   bit-64 held inputs, shortlist/quotes/selection and money under different event
   budgets. Held decisions must not rerank after live campaign changes or disable.
   With the clock/commerce held, repeat bridge reads and compare native campaign
   fields, MarketingExpenses, fee transactions and cash against an idle control.
   Then compare actual daily native fee settlement separately from legitimate
   food-sale changes. PC must not purchase/edit campaigns, alter fees or book
   marketing as sales. Preserve all refused source copies.
7. **LOCKED-OK - final-head matched performance.** Interleave candidate and base
   `51ce709` on the same fixture/seed/population/shops/clock/body/graphics/cache/
   panel setup at idle and meal rush, with running/retained/absent/disabled
   campaigns. Record mod-work p50/p95/max, allocations/GC, source touches, lag and
   every spike >=30 ms with component; require mod-work p95 <5 ms. Include
   `NativeFoodOwnership.AfterNativeTick` / `ObserveCampaignBoundary` in that
   frame's mod-work total; Advance-only timers are incomplete. Do not double-count
   component spans. Track whole-frame intervals/stalls separately. Record original
   autosave, disable/verify off before every window and after reload; invalidate
   any window with save activity. Keep manual saves outside windows and restore
   original autosave/preferences/fixtures during cleanup. No separate perf-only
   or save-profiling run is implied.

<!-- harness-working-mode -->
Local tests: custom.
Review level: full.
<!-- /harness-working-mode -->
