# C2b: observed campaigns and soft distance preference

## Round-1 review fixes: latest E4 base - 1 November 2026

Addresses **Sol-r1-1 / Opus-r1-1 / Opus-r1-2**. E4 landed at `2bd9006`
after the ice cream-only sync's final fetch. Real two-parent merge `beb3eae`
integrates that head into `d2ba6d2`, based on `75c956c`; no rebase, squash or
cherry-pick. Both parents' diffs were inspected. All **20 textual conflicts**
and their resolutions are listed in the [current PR description](../plans/c2b-sync2-base-pr-description.md).

The next final fetch found **`8c6b3f3` (E3 pizza)**. Two-parent merge `84348e1`
then integrates it into `4dc81d1` from base `2bd9006`; only README conflicted,
and both C2b/pizza notices remain. All incoming pizza checks/plan/perf/status
entries survive. Pizza adds no schema or gameplay behavior. Its production
reader/facade are byte-exact to the incoming head.

The imported pizza descriptor exposed an existing frozen-baseline reconstruction
failure in `icecream-feasibility-regression/measure_baseline.py`. A one-line
harness fix removes that descriptor when reconstructing the exact old three-
candidate reader. Ice cream's guard normalizes precisely that added line, and
pizza's guard normalizes precisely that guard edit; **all original hash values
are retained**, and the original reconstructed-reader hash still executes.
No production reader branch or Runtime code was changed for this test fix.

The executable writes **schema 23**, following E4's documented composition:
**19 -> 20 (E1) -> 22 (C2b) -> 23 (E4)**. Schema 21 stays reserved and refused;
>23 is future. An exact schema-22 resolver includes campaign images, 32 source
slots and held ActiveCampaign bits while retaining four menu/history/offer
limits and revision-1 tuning. E4's menu header, revision-2 tuning and compact
history cells are excluded from that predecessor. Migration validates the
complete predecessor and preserves campaign masks, weather, held decisions,
money and identities before applying the menu header. A new interrupted
schema-22 decision test covers rain/campaign captures, exact source retention,
reload and different live settings; E4 predecessor/old-limit cases now include 22.

Core/Runtime/persistence integration seams changed as documented; no policy
redesign was needed. Campaign preference remains 1.25 / half soft distance for
all residents in shortlist and final weighting; E4's taste input and native
effective four-offer limit remain. The E4 merge retained the E3 reader unchanged; the subsequent pizza import
and exact baseline/guard normalization are described above. The merge audit verifies **1,341 exact inherited paths** outside
the documented seams, every checks-index entry and all existing performance
evidence files. C2b/E4/A5 perf entries and ice cream status/checklist remain.

### Final latest-head offline validation

**65/65 foreground command gates pass on `8378efa`**, including the production
build, **2,435/2,435 tests** with zero failed/skipped, **2,435 discovered tests**,
all **40 top-level regression projects**, four Python adapters, three simulations,
continuity pickup, optional-stop performance/findings, all reader/source guards,
frozen baselines, C4 exact bytes, five A5 combined modes and E4's exact default-
economy comparison against `51ce709`. That comparison finds identical legacy and weighted economy outputs and exact
digests. Every gate used the prescribed
other-dotnet guard; all tests were foreground with no command time limit.

The first E4-only unit run had one new-test fixture failure (2,434 tests passed):
awareness preparation was missing before requesting its held decision. Adding
`InspectFoodAwareness` as in the existing replay test fixes only that setup.
The focused case and full suite then pass. The intermediate E4-only run passed
63 gates on `4dc81d1`; the final run above supersedes it after the pizza merge.
The separate pizza baseline assertion/fix described above is also retained.
No assertion was weakened or skipped. [Commands, all run exits/log hashes,
preservation audits, failures and current measurements](c2b-e4-sync-r1-results.json).
Raw logs remain ignored under `build/c2b-e4-sync/`.

Current population fixture: **schema 23, 120,257 characters**, digest
`E9ACB14208DFFB98500039FE30CDDA3545CFAE3DAA2EE8CAA45A08FEC810F974`.
The weather/campaign/menu-stripped schema-19 projection remains **119,083
characters**, digest `D7FF8294F56C3B3D5AF736540274E68E9848319B34658859FC15E0D4CDB1F3A2`.
Economy/order/reload digests remain unchanged, including **849 orders / 797
purchases / two uncertain meals**; all twelve F0 transitions pass. C4 fixtures
with/without its engine are exactly equal at **41,860 / 66,348 bytes**.
E4 passes **25,165 assertions**; its raw eight-product history fixture is
**10,135,880 UTF-8 bytes**, with **14,336 rows / 114,688 product cells**. That
raw sizing fixture is separate from its timed serialization variant. Pizza's
reader harness passes **2,423 assertions**. Production DLL SHA-256:
`3a1c07f87ba7c9a524d72a513e308bfc86cee2418cde84171e337dd3abb091a1`.

Campaign charges remain **282 / 274,446 bytes** and **1 / 1,024 records** above
schema 20, with 265-character images. At 1,024 images, publication p95/max is
**2.4388 / 3.921 ms**, production adapter **2.7404 / 5.0597 ms**; both have zero
>=30 ms samples, and eight-slot queries remain **0 B**. Cold C1 1,024-business
p95 **6.0568 ms** remains flagged above 5 ms. C2a's 20,000-place setup has **one
dense / one sparse** sample >=30 ms, max **40.9454 / 36.1882 ms**; sparse 1,024
setup p95 is **12.1316 ms**. Candidate admission has zero >=30 ms samples.
E4's separate four/eight-product timed serialization variants each have **21
of 21 operations >=30 ms**, max **200.1293 / 226.4955 ms**; these are save
operations, not ordinary frames or the raw sizing fixture. All aggregate rush,
save/read/migration and component timing flags are retained in the machine
record, with whole-frame/native mod-work evidence explicitly absent.

The final audit covers **1,388 tracked paths**, with **1,352 exact inherited
paths** outside documented seams. Both parents' index/performance entries and
all E3 production files are preserved. Final fetch reports **`8c6b3f3`**, an
ancestor of this branch. Foreground runners exited; inspection found no owned
test/run/compiler left behind. No push or PR merge was performed.

All older measurements below retain their original head/schema scope. In particular,
the old schema-22/23-refusal and `75c956c` final-fetch statements describe the
ice cream-only sync, not this current composition. No new performance win,
native mod-work or whole-frame result is claimed; the four parked hotspots
remain untouched. No game, test rig/profile, bridge lease, Steam/Workshop or
WSL operations. Native proof remains batched under merge rule v2; input-only
checks wait for Peter's 6 Nov playtest. No release or deployment approval.

## Second base sync: E3 ice cream - 1 November 2026 (historical, base 75c956c)

Merged `75c956c` into C2b `ffe85a0` from merge base `0ffb819` as two-parent
commit `033ba81`. Both sides' diffs against that base were inspected. The sole
textual conflict was the adjacent README status paragraphs: retain both C2b
and E3 ice cream, followed by A5/F1. Automatic CHECKS/roadmap/plan compositions
retain every existing index/status entry, C2b/A5 perf entries and incoming ice
cream plans, tools, checks and evidence. [Prepared sync PR description](../plans/c2b-sync2-base-pr-description.md).

The merge-tree audit covers all **1,351 tracked paths**: **1,347 exact parent
blobs** outside four composed documents; both parents' checks-index lines and
all added documentation lines survive. No integration code fix was needed.
C2b Core/Runtime/persistence/tests/tools are unchanged; the only Runtime
additions from its parent are E3's unchanged ice cream facade and shared-reader
descriptor. Schema remains **22**, with E1 **19 -> 20** then C2b **20 -> 22**;
At that head 21/23 remained refused. Incoming ice cream's schema-20 results and all earlier
measurements below remain historical, with their original limitations.

**60/60 foreground command gates pass** on `033ba81`: production build against
local Managed assemblies, **2,416/2,416 unit tests**, zero failed/skipped,
**2,416 discovered tests**, all **38 top-level regression projects**, four
Python regressions, continuity pickup, optional-stop performance/findings,
three simulations, reader isolation/frozen baselines, C4 exact-byte comparison,
and A5's rush/integrated/combined/5,000-resident/decision-save modes. Every
dotnet test/run, including Python children, used the prescribed other-dotnet
guard. Shared-PC waits were allowed to finish; no command deadline or detached
test was used. Both runners exited; process inspection found no task-owned
test/run/compiler process remaining. [Commands, log hashes, preservation audit,
save fingerprints and offline measurements](c2b-sync2-base-results.json).
Ignored raw logs are under `build/e3-icecream/gates/` and `build/c2b-sync2/`.

Current schema-22 save contract repeats **120,184 characters**, digest
`D520285710B5BBCBF7715CF430D75AC6733B23BD7B6EB71ED9D6E7CD0DD72945`;
weather-stripped projection **119,083 characters**, with the prior digest.
Economy/order/reload digests, **849 orders / 797 purchases / two uncertain
meals**, and all twelve F0 transitions remain unchanged. C4's schema-22
population/food fixtures remain byte-identical at **41,787 / 66,275 bytes**.
Production DLL SHA-256:
`94fa2718ecd943656983d686968eb588e7ffa8a6af64f52cf8b7981cef38ace4`.

Campaign wire charges reverify **282 / 274,446 bytes** and **1 / 1,024 records**
above schema 20, with 265-character images. At 1,024 images, publication
p95/max is **2.7971 / 3.6038 ms**, production adapter **4.4494 / 10.4813 ms**,
with zero >=30 ms samples in those series; eight-slot queries remain **0 B**.
Donut/cupcake/salad/ice cream pass **2,082 / 2,098 / 2,119 / 2,127 assertions**;
all seven reader baseline/candidate sets report zero samples >=30 ms, with
unchanged warm **200 / 49,104 B/call** allocations at 2 / 2,000 entries.

Offline flags remain explicit: C1 cold 1,024-business p95 **6.713 ms** exceeds
5 ms; C2a 20,000-place setup has **one dense / five sparse** samples >=30 ms,
max **39.4224 / 64.348 ms**. Its sparse 1,024-place setup p95 is **16.2639 ms**;
candidate admission has zero >=30 ms samples. Aggregate rush CPU costs and
separate save/read/migration operations >=30 ms are retained separately in
the machine record. These shared-host component/double/aggregate measurements
include JIT/GC/scheduling variation; they are neither native frame samples nor
a new performance win. Combined native mod-work p95 <5 ms, every spike >=30 ms
flagged and separate whole-frame evidence remain pending; four parked hotspots
are untouched.

That sync's final fetch reported base `75c956c`; E4 landed afterwards at `2bd9006` and is handled in the review-fix record above.
No game, rig/test profile, bridge lease, Steam/Workshop or WSL operations were
performed. Native checks remain batched under merge rule v2; pointer/input
proof waits for Peter's 6 Nov playtest. No release/upload or ordinary-save
deployment approval is claimed.

## Schema-20 base sync - 1 November 2026

Merged integration base `0ffb819` (F1, E1 and A5) into C2b `992291e` using a
real two-parent merge from `51ce709`. All 16 conflict resolutions are listed in
the [prepared PR description](../plans/c2b-pr-description.md#sync-base-integration-1-november-2026).
All preexisting checks/perf index lines and both parents' slice files are retained.
The 53 incoming-only files outside integration seams were compared with their
parent and match exactly after newline normalization. A5's test-only current-schema
expectation changes from 20 to 22, preserving all its invariant assertions; the
C4 byte differential likewise pins schema 22 and retains exact byte equality.

Schema 20 now has an exact filtered reader and bootstrap allow-list entry.
The isolated 20 -> 22 campaign step validates the predecessor, adds only empty
campaign history and preserves E1 weather. Older saves use E1's neutral 19 -> 20
step before 22; reserved 21 and future 23 remain refused. Shortlist and final
weights compose E1 rain with C2b's fixed 1.25 preference and half soft distance.
Tests cover weather commitments/held inputs and changed-source reload.

All numbers, byte counts, fingerprints and command counts in the earlier rounds
below were measured on C2b based on `51ce709` before E1/A5. They are preserved as
historical evidence and do not describe this merged tree. No new perf win is
claimed. In-game checks are batched into the combined locked rig under merge
rule v2; no rig proof is claimed, and input-only checks wait for Peter's 6 Nov
playtest. This sync does not authorize release/upload or normal-save deployment.

### Merged-tree offline validation

Validated merge commit `c455851` (parents `992291e`, `0ffb819`): **2,416/2,416
unit tests pass**, zero failures/skips; final `--no-build --list-tests` passes.
All **48 final distinct command gates pass**: Steam-reference build, unit suite,
discovery, all 37 top-level regression projects, four Python adapters, three
simulations and the extra C4 exact-byte differential. The first suite run had
only four A5 current-schema assertions fail (expected 20, actual 22); changing
that expectation to CurrentSchema preserved every subsequent invariant. The
complete suite was rerun, with no weakened or skipped assertion.

Every test/run and Python wrapper was preceded by the required other-dotnet wait.
All runs were foreground, with no command time limit or process termination.
All owned test/run processes ended; no owned dotnet/testhost/runner remained.
[Command exits, source hashes, preservation audit and full offline metrics](c2b-sync-base-results.json).
Ignored logs: `build/c2b-sync/`. DLL SHA-256:
`afeb5a246049a7d0e5679b751b3efe5324431783a2f00340d51b3c73b9a96447`.

The schema-22 population fixture is **120,184 characters**, digest
`D520285710B5BBCBF7715CF430D75AC6733B23BD7B6EB71ED9D6E7CD0DD72945`.
Its weather-stripped schema-19 projection is **119,083 characters**, digest
`D7FF8294F56C3B3D5AF736540274E68E9848319B34658859FC15E0D4CDB1F3A2`,
matching the original base. The no-campaign economy still produces **849 orders,
797 purchases, 107,967,424,757,760 spent units and two uncertain meals**; order,
economy and reload digests match the historical base exactly. The ownership-hook
regression retains **243 assertions**. C4's schema-22 fixtures with/without its
engine are byte-identical: population **41,787 bytes**, food **66,275 bytes**.

The merged campaign harness re-verifies **282 / 274,446 bytes** and **1 / 1,024
records** above schema 20 with existing references; images remain 265 characters.
At 1,024 images, publication p95/max is **2.5747 / 3.1028 ms** and the production
adapter **4.3486 / 8.7057 ms**, with zero >=30 ms samples in those component
series. **Cold C1-reader p95 6.4337 ms remains above 5 ms** (max 11.3982 ms).
Inherited C2a setup flags **one dense / eight sparse** 20,000-place full-build
samples >=30 ms (max **38.8319 / 75.8547 ms**). Its original baseline scan flags
**one sparse 1,024-place / 41 sparse 20,000-place** samples >=30 ms (max
**32.824 / 589.1993 ms**); candidate admission has zero >=30 ms samples. These
shared-host offline component results include JIT/GC/scheduling variability;
no new performance win or native mod-work/whole-frame proof is claimed. The four
parked hotspots and native combined performance gate remain unchanged.

No game launch, rig files/lease or normal-profile access, Steam/Workshop
operations or WSL use occurred. The build referenced local Managed assemblies
read-only.
In-game checks remain batched under merge rule v2; input-only checks wait for
Peter's 6 Nov playtest. No native proof, release or deployment approval is claimed.

## Round-2 fix - Sol-r2-2, 1 November 2026

Active preference now checks the saved mask at the meal's captured logical hour,
using a generation-keyed image lookup and at most eight type bits. A later native
publication, including a stopped/missing read, cannot remove an earlier observed
hour's modifier while that hour remains in the eight-day image. Stopped,
unobserved, future and expired hours remain inactive. Both derived index buffers
are reused and published with the complete image; there is no per-resident
business scan, new saved field, schema change or change to the 32/8/16 envelope.

Implementation: `0a58a8b`. Before the fix the new regression reproduced
`Sources=66 -> Sources=2` for an identical delayed meal after another publication.
After the fix all 23 campaign tests pass. The full suite passes **2,360/2,360**,
zero failures/skips. The replay compares complete timely/delayed captures across
midnight, continuous campaigns, a stop, an unobserved gap, restart, encoded-image
reload and explicit index rebuild. It also checks no active-lookup allocation.

All **33/33** foreground command gates pass, including the Steam-reference build,
test discovery, all requested regressions and three simulation commands, plus the
C2b production-adapter/performance harness. The other-dotnet wait preceded every
test/run and Python wrapper. Ownership-hook fixtures pass 243 assertions.
Schema, save-contract digest/length/refusals and order/economy/reload results are
unchanged from round 1. DLL SHA-256:
`711f314146c759e348151e69f8079fde2682bd3f36333a5dea739395ea4bc0d4`.
[Command exits, save differential and fingerprints](c2b-r2-offline-gates.json).
Local foreground logs are in ignored `build/c2b-r2/`.
Coordinated MSBuild/compiler-server shutdown succeeded; the final inventory found
no C2b dotnet/testhost processes. All foreground test and simulation runs ended.

At 1 / 1,024 images, eight delayed-hour active lookups have p95
**0.0004 / 0.0002 ms**, with **0 B** allocated. The maximum 32-lookup,
eight-type unobserved-hour workload has p95 **0.0047 / 0.0009 ms**, also **0 B**.
C1/publication p95 is **0.0142 / 3.1701 ms**, production-adapter p95
**0.0167 / 4.5993 ms** (max **0.0636 / 8.2773 ms**). All round-2 component
series have zero spikes >=30 ms. **Separate cold C1-reader p95 6.3476 ms is
above 5 ms** and remains flagged. Absent/disabled paths still allocate zero;
saved campaign bytes/records are unchanged. [Method and full results](../perf/c2b-campaign-awareness.md).

In-game checks are batched into the combined locked rig under merge rule v2;
no rig proof is claimed. This offline replay does not certify native clock
ordering or combined mod-work. Whole-frame
timing remains separate. No game/rig work, native campaign mutation, push or merge.

## Round-1 fixes - owner decision 1, 1 November 2026

Peter: "It should simply increase the chance for residents to pick that shop,
also at greater distance." This supersedes the initial demographic-targeting
proposal below and in the older B-E plan. Production is active for **all residents**;
there is no target policy, no Unknown target gate, and no target-match tuning.
Only C1-verified observed campaign evidence is accepted. Native controls/fees stay
native; paid research remains unavailable.

For a shop with positive evidence in the meal's captured logical hour, shortlist
and final offer preference use half the normal distance input and a fixed 1.25
preference factor. This modest fixed gameplay prior is the same for every resident
and campaign type; it neither changes measured journey times nor bypasses existing
feasibility/traits/outside choice. The held awareness image stores modifier bit
64, separately from campaign admission bit 32. No new saved field or resident
array is added. Without a campaign or with the feature disabled, the original
arithmetic and serialized source flags are unchanged.

Sol-r1-1: sample after ownership transfers and scope activation, including hours
without transfers. Opus-r1-1: retain and age masks for generations still in saved
scopes when no current read exists; add no new bit and drop only absent/replaced
scope generations. Opus-r1-3: catch observer failures, log once, latch that
observer off for the session, and retain the last complete image. Campaign
observation cannot skip or abort the hour's ownership transfer.

Sol-r1-2 / Opus-r1-2: the new owner instruction replaces demographic mapping.
The production adapter uses the universal rule; no target-match boost remains.
The same trait-independent modifier applies to already-known shops even when the
eight campaign discovery draws admit nothing. At a stopped or unobserved meal
hour, retained awareness has no active modifier; held decisions keep the modifier
they actually captured.

**Validation:** implementation `6de85c6`; final full suite **2,359/2,359**, zero
failures/skips; all **33/33** foreground gate commands pass, with the required
other-dotnet wait before every test/run and Python wrapper. The full suite was
repeated after the private-cache changes and passes. Actual ownership hooks:
**243 assertions**. Production-adapter fixtures verify all residents, retained
history with zero current candidates, first-hour evidence and a single latched
failure without publication. Policy tests prove higher seeded pick probability,
softer distance decay, unchanged absence, and held-bit replay after stop/disable.

At 1 / 1,024 images: publication p95 **0.0151 / 2.6336 ms**, production adapter
p95 **0.0127 / 4.2356 ms** (max **0.0322 / 8.9763 ms**), day/type rebuild p95
**0.0032 / 0.4893 ms**, eight-slot source p95 **0.0062 / 0.0065 ms**, **0 B/query**.
Absent/disabled steady-state paths allocate zero. Final series have zero >=30 ms
spikes. Initial round-1 1,024-image publication/adapter p95 **10.0878 / 8.8877 ms**
remains explicitly above target; private active-buffer reuse removes **102,216
B/publication** and **102,216 B/rebuild**. The neutral marker cache reduces eight-
lookup p95 **0.0028 -> 0.0002 ms**, **0 B on both sides**. Fixed index/work arrays
shrink **512 -> 64** slots. Wall times include host/JIT/GC variability; this is no
native gate claim. Separate C1 cold diagnostic p95 **6.3118 ms** remains flagged
above 5 ms. Four parked hotspots are unchanged.
[Round-1 measurements](../perf/c2b-r1-offline-results.json),
[method / before-after evidence](../perf/c2b-campaign-awareness.md).

Final save-contract/order/economy/reload digests match the previous candidate
and its schema-19 base comparison below when campaigns are absent. Population-only
contract digest remains `70698CB7D21E1ECA6C8D91A3FD3C4835A4EE7BA8EAB2D005592F868DDD308B2C`.
Final DLL SHA-256: `163a0abab7df999d9542c5eeed44d22813052aa884d706866715af833f046d88`.
[Command exits and final differential](c2b-r1-offline-gates.json). Local logs:
ignored `build/c2b-r1/`. Coordinated compiler shutdown succeeded; the final process
inventory found no owned test/run/testhost processes. No game/rig, ordinary save,
native campaign/fee mutation, push or merge occurred in this fixer.

Earlier measurements below belong to the initial implementation and remain
historical evidence, not final-head rig proof.

## Initial 1 November implementation (historical; superseded by owner decision 1)

Branch `codex/pc-c2b-campaign-awareness`, base `51ce709`. No sibling slice,
schema 20/21/23 implementation or rig files were used. No game launch, native
campaign write/purchase, RLO change, push, PR creation or merge.

Implemented C1-only hourly evidence, eight-type/eight-day masks, 1,024-image
atomic publication, fixed group/day/type indexes and at most eight source draws
through C2a's unchanged 32/8/16 envelope. Overlap deduplicates once and shares a
consumer/business/exposure-day reach draw, never a sales multiplier. Held source
and eligibility finish unchanged after reload or feature disable. Native campaign
fees and controls remain native; paid research remains unavailable.

**Target evidence limitation:** all six C1 target rows remain Unknown on this
base. Production deliberately contributes **zero** campaign admissions until an
explicit reviewed target policy exists. The implementation exposes commuter
routine / saved health-trait group membership and tested policy injection, but
does not infer internet use, billboard passing, native employment or demographics
from channel names. An optional targeting clarification was requested; absent a
decision/evidence it is not treated as approval. Campaign acceptance is pending,
not claimed complete merely because detached reviewed-policy fixtures pass.

Native observations run at the existing actual accounting-hour hook, all C1 units
of 16 synchronously before native/UI writers can interleave. No partial read is
published, no previous hour is reconstructed, and the existing hidden-skip clock
allows each observed boundary to finish before the next. Ordinary clock jumps
sample only the hour actually reached. No new per-frame reflection/polling/scan
or allocation is added. Publication is synchronous and bounded at 1,024; native
combined cost still needs the rig gate. With no campaigns only allocation-free
hourly count probes remain; source/index/decision work is zero. Literal zero
native detection work cannot be claimed because UI starts must be discovered.

The original pre-E1 [schema 22 contract](../population-schema22.md) used exact
19-to-22 migration (now 20-to-22 after E1), strict
old/new shape separation and source preservation. Only campaign images are added;
held awareness reuses its existing fields. Schemas 17–19 contracts are untouched.
Campaign publication and food memory share one current reference index, so
reference insertion finishes at the hour boundary and cannot move a 1,024-reference
catch-up onto the next consumer. Independent diagnostic stores retain bounded
append synchronization. The empty preflight uses C1's verified fields-only binding,
so missing or changed list fields cannot bypass its compatibility boundary. Empty evidence is byte-identical to C2a sampling/choices;
schema/empty campaign field are the documented serialization differences.

`PopulationRuntime.CampaignAwareness()` builds its images/verified-type/group,
publication and admitted/refused counters only when called. It also labels
verified observed type-days without reviewed targets. Join its complete business
IDs to `FoodAwareness(consumerId, day)` for the individual raw eight slots and held
capture. Counters are transient observations, not customers or sales.

**Unit validation:** final production Steam-reference build and **2,357/2,357
tests** pass, zero failures/skips (20 new C2b cases). Initial targeted review:
107/107; final C2b/B1/schema-19 held replay review: **23/23**.
The first full run identified eight stale schema-current/old-fixture assumptions;
those fixtures now remove only schema-22 additions when constructing older shapes,
and still prove their original contracts. Campaign-source/unknown/migration cases
pass after private buffer reuse. **32/32** foreground batch commands pass (build,
list, requested regression/demo commands and the additional C2b harness), plus
the full suite, exact base comparison and inherited C2a harness. The four Python
adapter checks were repeated with an explicit other-dotnet wait before each
wrapper's inner `dotnet run`; all four pass. Final targeted checks also cover
public-mask immutability and populated schema-19 B1/held-context migration.

Implementation commits: `f013687`, `3d93ad0`, `3bbb36d`. Local command logs are under ignored
`build/c2b/`; no raw native save or game DLL is committed.

**Base differential:** archived pure source from `51ce709` was compiled/run in
ignored `build/c2b/base19`, without changing branches. Both schema-19 base and
schema-22 candidate produce **849 identical orders, 797 purchases, exactly
107,967,424,757,760 spent units and two uncertain meals**, with identical order,
economy and reload digests. Order digest `1B94987B3158044594B9C4D6A5473BA22BE0D4F3488D769CBBEE23318BB33B91`;
economy `74D0FE8081EA795D4D56A11B4B955DC6E777E3822C702D3C09BC9477E6679AF9`;
reload `59B6F14D44CA0F996C5A828E2E538B52C5426347FCFA40767527B72D77F1D4FA`.
Population-only contract length stays **119,083 characters**; its schema-bearing
digest changes from `D7FF8294F56C3B3D5AF736540274E68E9848319B34658859FC15E0D4CDB1F3A2`
to `70698CB7D21E1ECA6C8D91A3FD3C4835A4EE7BA8EAB2D005592F868DDD308B2C`.
Candidate DLL SHA-256: `42f851401a11e2f71971223e6c82aef539de943cc21f6bd64d61cf5773686d96`.
[Command exits and base differential](c2b-offline-gates.json).
The final package's config hash matches the source template. Coordinated MSBuild
and VB/C# compiler-server shutdown succeeded; the final inventory found no owned
dotnet/testhost or gate-runner processes. Other worktrees were allowed to finish;
none was terminated. [Prepared PR text](../plans/c2b-pr-description.md) contains
the identical numbered rig list below.

### Offline performance and save budget

[Method and before/after evidence](../perf/c2b-campaign-awareness.md). At 1 / 1,024
images, C1+reviewed-policy hourly publication p95 **0.0164 / 2.3997 ms**, max
**0.0669 / 2.8717 ms**, **2,880 / 2,198,552 B/op**. Fixed eight-slot query p95
**0.0088 / 0.0056 ms**, **0 B/op**. Index rebuild p95 **0.0051 / 0.4096 ms**,
**72 / 16,440 B/op**. Production Unknown-target adapter p95 **0.0123 / 3.7834 ms**,
max **0.0233 / 7.1044 ms**. No-campaign detection probes and disabled hours
allocate **0 B/op**; absent source/index/query work is zero. Earlier prototype
hourly p95 **7.4787 ms**, **two spikes ≥30 ms, max 56.1014 ms**, explicitly flagged;
optimized repeat has zero ≥30 ms samples. These are offline component results,
never whole-frame timing or native mod-work clearance.

Final C1-bound empty preflight p95 is **0.0002 / 0.0737 ms** at 1 / 1,024
scopes, with zero allocations after binding. Sharing the reference index removes
the next lookup's **227,440 B / 0.1711 ms p95** catch-up after 1,024 inserts:
the shared path takes **0 B / 0.0002 ms p95**. Standalone C1 cold 1,024 read p95
is **5.1061 ms**, max **12.0372 ms**, explicitly above the 5 ms component
comparison target; zero ≥30 ms samples. [Machine-readable final results](../perf/c2b-offline-results.json).

The additional inherited C2a harness flags **six synchronous 20,000-place fixture
build samples ≥30 ms**: three dense (max **57.6692 ms**) and three sparse (max
**37.6307 ms**). These are its explicit complete-build setup condition, not normal
runtime bounded continuation or C2b work. No admission sample reaches 30 ms; max
**0.2627 ms**. The inherited build/parked-hotspot implementations are unchanged.

Campaign images add **282 / 274,446 bytes** and **1 / 1,024 records** over 19
with shared references already registered; new reference bytes/records are separate.
Each fixed record is 265 ASCII characters, no per-resident state. Empty field:
15 bytes, zero records. Existing global caps and future envelope stay fixed.

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

[Checks index](../../CHECKS.md).
