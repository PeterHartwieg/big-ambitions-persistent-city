# C2b: campaign awareness and targeting

## 1 November 2026 — offline implementation

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

[Schema 22 contract](../population-schema22.md): exact 19→22 migration, strict
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

Pending, normal combined session only. Use a named disposable FT fixture and
attach before/red → after/green dumps/logs/captures. LOCKED-OK classification
follows [feasibility commit 84be71a](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/84be71a/docs/reviews/rig/2026-10-31-locked-session-feasibility/README.md).

1. **NEEDS-INPUT — prepare one native campaign.** In the agency UI of the named
   disposable fixture, start one available verified type on one supported player
   food business. Record type, enabled flag, agency/business generation, native
   fee and clock. Starting/stopping and visual UI confirmation need the unlocked
   6 November session. Do not have PC purchase or mutate it.
2. **LOCKED-OK — hourly complete publication.** From the prepared checkpoint call
   `ReadCampaignSnapshot` and `CampaignAwareness()`, advance through the next
   accounting hour using the bridge, then dump again. Verify a single complete
   image, exact first-observed hour bit, no earlier exposure, ≤8 types/8 days and
   ≤1,024 images. Repeat across permitted hidden-skip accounting boundaries;
   show each actual observation, never reconstructed activity or partial images.
3. **LOCKED-OK — targeted versus untargeted evidence.** Dump known resident and
   represented-slot traits/routine facts plus `FoodAwareness` raw/held slots and
   campaign counters. With the current Unknown table both cohorts must admit
   zero; attach that refusal. Campaign acceptance additionally requires a
   reviewed type→group policy/evidence, then matched targeted/untargeted dumps,
   overlapping groups counted once, ≤8 campaign draws, ≤32 union/8 shortlist/16
   routes, and trait/price/stock/deadline rejection after exposure. Synthetic
   policy injection alone does not clear this native check.
4. **NEEDS-INPUT — stop through the native agency UI.** Stop the prepared campaign,
   record enabled/list state and logical hour. UI action and visual confirmation
   require input; PC must not issue a campaign write.
5. **LOCKED-OK — stop/fade over days.** From the stopped checkpoint advance through
   hours/midnight/day 1–8 via bridge and dump masks/sources/counters. No new stopped
   bits; retained prior awareness follows saved masks, configured decay and stable
   reach draws; expired campaign admission is zero. Other awareness sources may
   still admit the shop. Unknown targets remain zero throughout.
6. **LOCKED-OK — mid-campaign save/reload and fees.** Save a disposable running-
   campaign copy outside timing windows, reload via bridge, and compare exact
   image masks, source flags, held shortlist/quotes/selection and native campaign
   state. Repeat after stopping and with different event budgets. Compare raw
   campaign fields, `MarketingExpenses`, native fee/transaction/cash evidence
   against matched native controls before/after repeated bridge reads and actual
   daily fee settlement; PC contributes zero native changes. Preserve refused
   source copies and keep manual saves outside measured windows.
7. **LOCKED-OK — matched candidate/base performance.** Interleave candidate and
   base `51ce709` on the same prepared fixture/seed/population/shops/clock/body/
   graphics/cache/panel state at idle and meal rush, including campaign sampling,
   retention/rebuild and absent/disabled conditions. Record **mod-work p50/p95/max**,
   allocations/GC, source touches, lag, and **every spike ≥30 ms** with component;
   require mod-work p95 <5 ms. Track **whole-frame intervals/stalls separately**.
   Include `NativeFoodOwnership.AfterNativeTick` / `ObserveCampaignBoundary`
   sampling/publication cost in that frame's mod-work total; Advance-only timers
   are incomplete, and component spans must not be double-counted.
   Record original autosave, disable and verify off before every measured window
   and after reload; invalidate windows with any save activity. Restore original
   autosave/preferences/fixtures at cleanup. No special performance-only or save
   profiling run is implied.

[Checks index](../../CHECKS.md).
