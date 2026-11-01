# Sync D2 with persistent-city through C2b and E3 frenchfries

Target: `codex/pc-d2-rival-outflow`. Harness handoff only: merge this PR with a
**merge commit**, preserving its ancestry; PR #58 can then squash-merge into
`codex/persistent-city`. No push, PR creation, game, rig or release action is
part of this task. Merge rule v2 puts native checks in the later combined locked
rig on the integration head; pointer/input checks wait for Peter's 6 Nov playtest.

Verified `origin/codex/pc-d2-rival-outflow` (`ef2d72e`) is an ancestor of draft
`03960b1`, then fast-forwarded this branch to that commit. Reused its real merge
ancestry without touching its old worktree. New two-parent merge `d901057` merges
`03960b1` and `04b502c`; actual merge base `8c6b3f3`. Both sides' diffs against
that base were inspected for all 20 conflicted files. Prior sync resolution and
measurements remain in the [archived draft](2026-11-01-d2-sync-base-pr-description.md),
qualified by their original source commits. Fresh validation is recorded in
[D2 checks](../checks/d2-rival-outflow.md).

## Schema composition

D2 keeps **21**, below C2b; no renumbering to 24. The repo's schema-22/23 docs and
CampaignWire22 integration seam explicitly allow this exact predecessor change.
The full chain is **13-17 frozen -> 18 -> 19 -> 20 (E1 weather) -> 21 (D2) ->
22 (C2b) -> 23 (E4)**; the writer remains **23**.

`RivalWire21.Migrate` uses the frozen E1 reader to validate exact 20 (older sources
first reach 20), preserves every weather capture, and initializes D2's next-day
activation/account/target state. Exact 21 includes weather plus D2 state, excludes
campaign/E4 fields and retains four-offer limits. Only C2b's exact predecessor is
repointed from 20 to 21 (`MigrateCampaigns21To22`); its complete predecessor is
validated before schema mutation. Exact 22 now includes D2 and C2b state and
excludes E4's menu header/tuning/history cells. E4 stays at predecessor **22**.
Every step is isolated and complete validation precedes replacement of the
original dictionary. Schema 21 is supported; unknown and >23 shapes still refuse
(>23 reports Future), and strict malformed/foreign-field refusal stays intact.

Followed C2b's existing strict required-field policy at `6909beb`: it required
`Campaigns` in an already-numbered schema-23 payload, rather than backfilling
pre-C2b E4 development saves. Pre-D2 development schema-22/23 saves missing D2
fields or carrying tuple revision 1 similarly refuse with the source preserved.
There is no new compatibility scheme. Re-checking the prior draft also found that its frozen-20 wrapper stamped older payloads as 20 before executing the complete schema-19 validator. It now delegates older sources to the frozen-19 reader first, then applies the isolated, unchanged E1 weather stamp and validates 20. This is a required migration-order integration repair, with no weather policy change. Frozen 19/20 resolvers now explicitly
exclude the new `Campaigns` DTO property so their original shape stays frozen.

## Conflict resolutions

| Conflicted file | Resolution |
| --- | --- |
| `Core/FoodAwareness.cs` | Keep rival source kinds/weight and combine ActiveCampaign's 1.25 preference and half soft-distance input with E1 rain in the shared eight-slot rank. |
| `Core/FoodChoicePolicy.cs` | Retain D2 alternative weight, E4 taste and C2b campaign modifier; named campaign argument avoids positional ambiguity. Final selection applies all captured terms. |
| `Core/FoodCommerce.cs` | Keep RivalStartsDay and Campaigns, next-day rival initialization and C2b shared generation-index reuse. |
| `Core/FoodWeightedCommerce.cs` | Keep bounded rival capture and E4 whole-menu/missing-price guards; prepare campaign evidence and pass captured campaign, rival, taste and rain terms through final weighting. |
| `Persistence/MenuWire23.cs` | Keep incoming exact predecessor 22. |
| `Persistence/PopulationWire18.cs` | Support 21 and 22 in bootstrap; retain D2 tuple revision/account/outcome converters and E1 weather. Schema-specific counting uses exact 21/22 resolvers. |
| `Persistence/SaveCodec.cs` | Compose exact 20 -> 21 -> 22 -> 23; retain all D2/C2b/E4 strict bounds and semantic validators with dedicated 21/22 resolvers. |
| `README.md` | Retain all D2, C2b and other notices; current writer 23, supported predecessors 13-22. |
| `Runtime/PersistentCityMod.cs` | Retain rival settings/warnings, E4 live-four limit and campaign attachment at ordinary startup/resume. The diagnostic enrollment helper retains D2's existing argument set. |
| `docs/population-schema23.md` | Document composed chain and strict predecessor/dev-save treatment; preserve the E4 envelope and all remaining sections. |
| `docs/roadmap.md` | Keep C2b status and D2 status plus both perf-log entries; retain every other slice entry. |
| `tests/FoodAwarenessMemoryIntegrationTests.cs` | Use genuine frozen 19/20 fixtures and exact 21/22 projections; preserve all memory/weather/source checks. |
| `tests/Menu23Tests.cs` | Cover both 21 and 22; keep fifth-offer/history/scope rejection; unknown/future list now 24/25/99. |
| `tests/Schema18Tests.cs` | Keep canonical-token attacks aimed at D2's actual tuple revision 2 and current schema 23. |
| `tests/WeatherTests.cs` | Preserve genuine schema-19 projection through its frozen writer, not relabelled D2 tuples; retain all weather tests. |
| `tools/awareness-regression/Program.cs` | Keep exact frozen-19 byte projection; separate weather/campaign/menu bytes from rival bytes. |
| `tools/pizza-feasibility-regression/source_checks.py` | Keep incoming frenchfries/combined-reader pins; use full exact hashes for the repaired historical baseline/guard files; no reader or facade behavior changed. |
| `tools/save-contract-regression/AwarenessContracts.cs` | Keep exact frozen predecessor fixture and awareness-only byte accounting, excluding later extensions. |
| `tools/save-contract-regression/CapacityBench.cs` | Charge D2, weather, campaign and menu additions once against existing reserves; no cap increase. |
| `tools/save-contract-regression/WeatherContracts.cs` | Separate exact weather, campaign and menu byte deltas, retaining genuine pre-rival schema-20 projection. |

Auto-merged CHECKS retains exactly one D2 line and every incoming line. All other
slices' checks and numbered in-game sections remain. C2b, E4, E3 icecream/pizza/
frenchfries, A5, E1, F1 and earlier slices' tools/tests/docs remain present.
The campaign-awareness regression now uses the genuine frozen-20 writer instead of relabelling current D2 tuples; its campaign-only byte delta is labelled above predecessor 21. Schema 21/22 docs, refusal tests and historical draft claims were deliberately
updated or explicitly qualified with the source where the old statement applied.

## Runtime/Core scope and integration fixes

Core **did change** at the four conflict seams above to compose the already-approved
D2 rival choice, C2b campaign preference, E1 rain and E4 menu inputs. Runtime
**did change** at `PersistentCityMod` attachment/constructor seams; the reused
prior sync also combines native menu-limit and rival arguments in
`NativeFoodMarket`. No new behavior beyond composition, demographic targeting,
product permission, journey/settlement policy or native finance write was added.
D2's four review fixes and native-double harness remain intact. E1 weather policy,
E4 menu policy/supply/missing-price fix and C2b campaign source/adapter are retained.
Icecream's historical baseline reconstruction strips all later pizza/frenchfries descriptors; duplicate stripping from the draft is removed. Its reconstructed-reader hash remains unchanged, and icecream/pizza/frenchfries guards now pin full exact tool contents instead of normalizing edits. The compile-only nullable annotation and genuine predecessor fixtures are minimal
integration fixes; no checks were relaxed to fit new numbers.

Added focused tests for exact 21/22/23 strict refusal (including pre-D2 tuples),
held/paid rival replay through 21 and 22, and mixed rain/campaign/rival capture and
reload under disabled live settings. The gate discovers every root regression
project, including weather/campaign-awareness/E4/all six E3 probes, and now runs
frenchfries isolation and the documented salad/frenchfries baselines as well.

## Offline validation

**73/73 foreground gates passed** on the final source fingerprint (`246c24e`):
production build, **2,492 passing and discovered tests** (zero failed/skipped),
all 42 root regression projects including weather/campaign-awareness/campaign-reader/
E4/rival/awareness/food-candidate/all six E3 probes, four Python adapters, three
demos, four capacities, pickup continuity, combined rush/integrated/save modes,
C4 exact bytes, E4 default-economy comparison, five E3 isolation guards and the
salad/icecream/frenchfries historical comparisons. Eight focused integration cases
also passed. Every test/run used the required dotnet guard, foreground execution
and no command deadline. [Commands, hashes, full measurements and initial fixes](2026-11-01-d2-sync2-base-results.json).

Fresh exported-`03960b1` / final-source D2 benchmarks passed separately. Source/rank
p95 before -> after is **0.0185 -> 0.0279 ms** (1,024) and **0.0236 -> 0.0236 ms**
(20k); allocation increases **8 B/context** with C2b's held-image cache. The paired
20k offline batch has **1 -> 2** >=30 ms samples, max **41.7655 -> 36.5147 ms**;
the full gate's separate D2 run has one, **32.9809 ms**. No speedup is claimed.
Whole-build/serialization/save/aggregate costs remain separately flagged, including
cold C1 p95 **6.0459 ms**. [Detailed before/after and limits](../perf/d2-rival-outflow.md#sync2-fresh-beforeafter-comparison).

Population fixture **120,257 B** and schema-19 projection **119,083 B** retain
exact digests and economy (**849 orders / 797 purchases / two uncertain meals**).
Current reference/5k/CF32/CF64 capacity bytes are **5,058,429 / 9,802,141 /
194,180,943 / 232,580,943**, +15 for empty campaigns versus the named prior E4/D2
table, unchanged record counts and full future envelopes. E4's raw eight-product
history is **10,135,900 B**, separate from timed serialization. D2/E4 assertions:
**19,844 / 25,165**. All measurements and limitations are in [D2 checks](../checks/d2-rival-outflow.md).

Audit: **1,401 incoming paths**, **1,347 exact inherited blobs**, no missing paths
or exact checks-index lines; no changed native-check sections or E3 production
readers. Historical perf/byte claims remain explicitly tied to their source commits.
No native mod-work/whole-frame gate or release approval is claimed. The four parked
hotspots stay parked. Game/rig/profile/lease/Steam/Workshop/WSL were not used.

## In-game checks for 6 Nov rig

Pending combined-head proof on an existing named disposable FT fixture with an
advertised rival and eligible player food shop. Classifications follow
[the locked-bridge feasibility report at 84be71a](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/84be71a/docs/reviews/rig/2026-10-31-locked-session-feasibility/README.md).
No rig action was performed here. Preserve fixture/profile copies and preferences;
use independent native observations as proof of native non-mutation.

1. **LOCKED-OK — rival choice over a player shop.** From one disposable checkpoint, bridge-dump both alternatives and an admitted resident's held source/shortlist. Advance normal events to an actual rival commitment; record `RivalFoodDiagnostics.Core.RivalChoices`, target kind, exact advertised price and `EstimatedFromPriceAndHours`. Show the player competed in the same eight-shop shortlist, <=32 source slots and <=16 logical route requests. Sampling is not a purchase; there is no minimum rival share.
2. **LOCKED-OK — timed outing and return.** Dump slot zero before departure, at the rival door, during estimated service, after payment and back at its saved origin. Record stable resident/body ownership, times, deadline and one outing/return. Repeat a represented slot's existing excursion with no second body. State proves timing/ownership; visual continuity is item 8.
3. **LOCKED-OK — one exact external debit.** Capture wallet/allowance, external account, spend/count, watermark and native receipt keys immediately before/after service. Verify one exact price-unit transfer to `ExternalFoodSpent` (or the legacy account), conservation, `RivalSettlements +1`, no native held/uncertain receipt and no replay debit. Use prepared disposable checkpoints for arrears/hold/future-essentials refusal.
4. **NEEDS-INPUT — changed terms and ownership.** Use normal fixture UI to change rival price/hours after commitment and before arrival; D2 must not make that write. Show missed service, zero debit, retained return and no substitute. Repeat closure/withdrawal and, if normal UI can induce it, both ownership-transfer directions: old authority refuses. Record scenarios the native fixture cannot induce rather than treating doubles as proof.
5. **LOCKED-OK — interrupted reload.** Save/reload outside timing windows at capture, partial quotes, selected result, outward travel, service, paid state and return. Compare IDs, price/service/deadline, account units, watermark and count with uninterrupted replay. Repeat with weight zero loaded while unloaded: held/saved payments/returns continue; new choices do zero rival work. Copied malformed/foreign/future schemas refuse while preserving source/hash. Changed-terms checkpoints prepared in item 4 can then replay without input.
6. **LOCKED-OK — native non-mutation and player control.** Independently capture rival registration/owner/prices/hours, stock, orders, customer-entry and finance state before/after a paused or explainably matched modeled-purchase window. Compare exact fields; attribute unrelated native clock effects separately. Verify zero rival checkout/customer-entry/supply/demand calls and zero native rival change. A player control keeps its existing receipt namespace/content and exact money/stock/order/accounting path, with no extra body.
7. **LOCKED-OK — matched performance windows.** Freeze base/candidate commits, checkpoint/seed, effective settings, dependencies, clock/body/graphics/population/shop setup and cache warmth. Interleave idle and breakfast/lunch/evening rush windows plus a soak; record pending work/catch-up, source/detail/route counts, refresh units, allocations/GC and retained memory. Record original autosave, disable it in the isolated profile and verify off before every window and after reload. Manual saves stay outside windows; save activity invalidates a clean sample. Report combined mod-work p50/p95/max, require p95 <5 ms and flag every spike >=30 ms with count/condition/component. Report whole-frame timing/stalls separately. Bridge-accessible closed/open diagnostic states can run locked; UI-only variants wait for item 8. Restore autosave/preferences/fixture changes during cleanup.
8. **NEEDS-INPUT — visible continuity and fixture UI.** In the unlocked session, select/follow a rival customer through outward walk, door/service and return; confirm continuous appearance, no second body and agreement with bridge state. Visually check price/hour edits, the player control and UI-only diagnostic/performance variants. Establishing a missing shop fixture through normal UI also waits for input.

[Checks index](../../CHECKS.md).
