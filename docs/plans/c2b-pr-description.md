# Add bounded observed campaign evidence and awareness source (schema 22)

C2b records verified enabled native campaign observations at actual accounting-hour
boundaries, retains eight days per business, and supplies up to eight indexed
campaign draws to C2a's shared 32-source / 8-shortlist / 16-route envelope. Schema
22 adds fixed compact images, exact schema-19 migration and strict refusal/source
preservation. No resident awareness arrays or native campaign writes/purchases.
Native fees and controls remain native; paid research remains unavailable.

**Acceptance limitation:** C1 verifies six type identities but every demographic
target row is Unknown. Production therefore contributes zero campaign admissions.
The explicit group/policy interface and commuter/health/overlap behavior pass
synthetic reviewed-policy tests; they do not invent native targeting evidence.
A reviewed type→group policy and fixture proof are still required for campaign
acceptance. No sibling slice or reserved schema 20/21/23 implementation is used.

The on-demand `CampaignAwareness()` bridge dump exposes images, types/groups,
publication/source counters and Unknown-target refusals. Held decisions finish
from their captured source/eligibility and native terms after reload/disable.

Offline validation: production Steam-reference build and **2,357/2,357 tests**
pass (20 new C2b cases), the complete 32-command foreground gate batch, final 23-case
C2b/B1/schema-19 held replay review, and the additional base/C2a comparisons pass.
Archived base `51ce709` and candidate have identical 849 orders / 797 purchases /
107,967,424,757,760 spent units / two uncertain meals and identical order,
economy and reload digests. All four Python adapter checks were repeated with an
explicit other-dotnet wait before each wrapper. No game or rig session was run. Tests cover exact migration/refusal, toggles,
midnight/reload, expiry/retention/decay, overlap, unverified types/targets, atomic
publication, permutations, replay, empty/disabled neutrality and bounds.

Offline Release .NET 10 at 1 / 1,024 images: C1+reviewed publication p95
**0.0164 / 2.3997 ms**, max **0.0669 / 2.8717 ms**, **2,880 / 2,198,552 B/op**;
index rebuild p95 **0.0051 / 0.4096 ms**, **72 / 16,440 B/op**; eight-slot source
p95 **0.0088 / 0.0056 ms**, **0 B/op**. Production Unknown-target adapter p95
**0.0123 / 3.7834 ms**, max **0.0233 / 7.1044 ms**. Absent/disabled allocate zero;
no absent source/index/query work, but bounded hourly native count probes remain
and are explicitly measured. Literal zero detection CPU work is not claimed.
Shared generation references aside, images add **282 / 274,446 bytes** and
**1 / 1,024 records** above 19; empty field adds 15 bytes, zero resident growth.
The existing future save envelope deducts actual campaign bytes/records once.

Cheap wins: private index capacity reuse, one ordinal business sort, and owned
private mask buffers. At 1,024 images publication allocation **4,054,648 →
2,198,552 B/op**, p95 **7.4787 → 3.4398 ms**; rebuild allocation **1,585,816 →
16,440 B/op**, p95 **2.9224 → 0.6484 ms**. **Two earlier hourly spikes ≥30 ms,
max 56.1014 ms, are flagged**; optimized series has none. Offline components do
not certify native combined mod-work p95 <5 ms / spikes <30 ms. Whole-frame cost
is separate and unavailable offline. Four parked hotspots remain untouched.
Additional boundary win: sharing the production reference index removes a measured
**227,440 B / 0.1711 ms p95** catch-up on the next consumer lookup after 1,024
hourly inserts; the shared path takes **0 B / 0.0002 ms p95**. Final C1-bound
empty count probes p95 **0.0002 / 0.0737 ms** at 1 / 1,024 scopes, **0 B/op**
after binding. Standalone C1 cold 1,024 read p95 **5.1061 ms**, max **12.0372 ms**,
is above the component comparison target; zero samples exceed 30 ms.
The additional inherited C2a full-build fixture records six setup samples >=30 ms
at 20,000 places (dense three/max 57.6692 ms; sparse three/max 37.6307 ms), flagged
separately from runtime continuation/C2b. Admission samples have none (max
0.2627 ms). Its implementation is unchanged.
[Command exits and differential](../checks/c2b-offline-gates.json).
[Full method and limits](../perf/c2b-campaign-awareness.md).

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
