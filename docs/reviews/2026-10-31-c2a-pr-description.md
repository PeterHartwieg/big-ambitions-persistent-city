# C2a: bounded Core food awareness before the existing eight-shop choice

Targets `codex/pc-b0b-held-tuning` (base c0af745). Adds bounded memory (empty seam),
neighbourhood/routine and positive daily distant discovery; dedup/capture feeds
A3's same eight slots, full feasibility and <=16 routes. An optional campaign
provider remains unknown/unused. Generic external metadata can share those slots
later without native enrollment. No B1/B2/C1/D1/F0 dependency, campaign evidence,
research, rival behavior, UI or native write is included.

Schema 19 freezes the small additive held source image and activation header,
strict 18 migration/refusal, captured continuation and next-day capability
activation. New flat tuning applies at the next uncaptured context. The diagnostic
bridge dumps one consumer's slots, union/dedup and proposed/held shortlist.

Validation: **1,902 tests (40 C2a)**, discovery and **all 33 foreground gate commands**
(31 requested plus native-candidate and choice). The same-checkpoint tests cover
capture, partial quotes and selected reload, settings/cadence changes, malformed
source preservation and generation replacement. Historical economy/digests remain
exact. [Command evidence](2026-10-31-c2a-offline-checks.json),
[performance/size manifests](2026-10-31-c2a-measurements.json),
[contract](../population-schema19.md).

| Alternatives / layout | B0b rank p50 / p95 / max ms | C2a source+union+rank p50 / p95 / max ms | B0b / C2a bytes per context | Index build/rebuild p50 / p95 / max ms | Build allocated bytes |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1,024 / dense | 0.1073 / 0.2393 / 5.5627 | 0.0116 / 0.0436 / 0.8222 | 306 / 987 | 2.0451 / 26.4999 / 27.2204 | 275,640 |
| 20,000 / dense | 3.5486 / 4.5085 / 8.5244 | 0.0290 / 0.0486 / 0.8597 | 280 / 885 | 30.7147 / 41.7452 / 72.9799 | 4,254,290 |
| 1,024 / sparse | 0.1249 / 0.1613 / 1.0213 | 0.0057 / 0.0120 / 0.1534 | 280 / 827 | 2.3263 / 9.4777 / 10.4809 | 692,054 |
| 20,000 / sparse | 3.5047 / 4.2679 / 6.2920 | 0.0175 / 0.0322 / 0.7737 | 280 / 827 | 43.2643 / 73.4230 / 78.1916 | 13,475,542 |

Measurements: three fresh Windows .NET 10.0.13 Release processes, 300 warmups /
3,000 measured source+rank contexts per layout/process, alternating B0b-loop/C2a
order; ten warmed builds/process. Median run percentiles and largest max shown.
Seed 77; radius 250, four draws/anchor, discovery .02, four-reference memory double;
dense (10,0,0), sparse 125 m grid. Setup/native reads/routes excluded from context
timers. No measured source/rank sample >=30 ms; routes <=16 in production tests.
**40 rebuild samples >=30 ms flagged: dense20k 16 (max72.9799), sparse20k 24
(max78.1916).** Event-only large-catalogue rebuilding remains a measured limitation;
combined native p95 <5 ms / spike <30 ms proof is pending. Whole-frame is separate.

Retained wire addition with 24 native-format IDs is **3,074 bytes/city**, including
3,038 image +13 field +23 header, at 1,914 and 5,000 consumers; **zero recurring
per-consumer/day bytes**. Managed image graph: 1,048 bytes with shared IDs. Saturated
B0 fixtures add **9,514 bytes and 24 records**, deducted once from existing reserves:

| Full B0 fixture | Schema-19 UTF-8 bytes / characters | Delta from B0b | Streaming / semantic records | Write / read / 17 migration ms |
| --- | ---: | ---: | ---: | ---: |
| reference | 5,022,655 | +9,514 | 54,584 / 54,581 | 129.3 / 371.2 / 559.3 |
| consumers5000 | 9,710,819 | +9,514 | 110,132 / 110,129 | 383.8 / 946.7 / 1944.0 |
| CF32 | 192,029,621 | +9,514 | 1,920,132 / 1,920,129 | 2,757.4 / 9,066.3 / source refused |
| CF64 | 230,429,621 | +9,514 | 3,520,132 / 3,520,129 | 3,512.3 / 10,453.1 / source refused |

All displayed save/read/migration timings >=30 ms are flagged separate startup/
reload/save latency. The complete CF32/CF64 future budgets stay 220,664,366 /
259,064,366 bytes and 2,111,867 /3,711,867 records; CF64's existing >95% warning
remains. Streaming/semantic charge and global caps are unchanged.

Measured low-risk wins: prefix/dedup capture allocation ~4.4?4.6 KB ->0.9?1.0 KB;
20k dense/sparse index build allocation 17.48/32.00 MB ->4.25/13.48 MB. Native
capture full controller/hours sweeps go to zero; exact sampled native terms still
revalidate. The four parked hotspots remain parked. Timing noise and rebuild
spikes are retained. No native gate is inferred from offline measurements.

### In-game checks for 6 Nov rig

Pending: a `rig_test` session must use a named disposable FT fixture and attach
before/red ? after/green bridge dumps/logs or captures to the PR. The locked-PC
classification follows [84be71a's feasibility report](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/84be71a/docs/reviews/rig/2026-10-31-locked-session-feasibility/README.md).
No rig action was performed by this implementation run.

1. **LOCKED-OK ? source/position capture.** On the same seeded disposable checkpoint,
   call `PopulationRuntime.FoodAwareness(consumerId, day)` for a known resident and
   represented slot. Join sampled business IDs to saved scopes/places and native
   shop entrances; verify home/work/current distances, <=9 cells per anchor,
   <=4 local draws each, raw union/dedup flags, <=24 core slots, eight reserved
   unknown campaign slots, <=8 shortlist and <=16 actual decision route requests.
   Repeat without state changes and after reload; attach matching dumps.
2. **LOCKED-OK ? distant opportunity.** Select a supported player shop beyond every
   local anchor radius. Derive seeded logical days through the read-only dump in
   bounded batches until its discovery sample is **eligible**, then show that it
   shares the ordinary shortlist competition. Record consumer/seed/day/revision
   and native position. An admission is neither a committed journey nor a sale.
3. **LOCKED-OK ? refusal after admission.** On matched disposable fixture copies,
   retain a sampled generation while testing unaffordable price, unavailable
   stock, closed hours and an insufficient deadline. Record captured eligibility,
   existing feasibility reasons, zero forbidden purchases and <=16 routes.
   Compare changed taste/household preferences through recorded choices across
   seeds; awareness must not guarantee a purchase or bypass outside choice.
4. **LOCKED-OK ? campaign and memory boundaries.** Verify all eight campaign slots
   say unknown, no campaign source bit/admission appears, and the production memory
   source is empty. Native campaigns may exist but must have no C2a provider call,
   saved mask, discount or capacity effect. Attach the consumer dump and schema.
5. **LOCKED-OK ? migration/replay/size.** Make a disposable copy of a baseline
   schema-18 checkpoint; load with candidate, verify schema 19/next-day activation,
   unchanged IDs/wallets/committed terms, then save/reload at capture, partial quote
   and selected boundaries. Compare IDs, flags, shortlist, quote cursor, money and
   purchase identity against uninterrupted replay under different event budgets.
   Edit tuning only while unloaded: held contexts retain their image and the next
   fresh context uses new values that same day. Record exact saved-size delta;
   preserve refused/malformed source copies and leave ordinary saves untouched.
6. **LOCKED-OK ? matched performance windows.** Interleave candidate and B0b
   `c0af745` at idle and breakfast/lunch/evening rush, matching checkpoint, seed,
   shops, population, clock/body/graphics settings, cache warmth and panel state;
   include a soak and a catalogue-change/rebuild observation. Record mod-work
   p50/p95/max, **every spike >=30 ms**, route/query counts, allocations/GC,
   pending meal work and catch-up latency. Require mod-work p95 <5 ms; record
   whole-frame intervals/stalls **separately**. Record original autosave, disable
   and verify off before **each** window and after reload. Keep manual saves
   outside windows; invalidate any window containing save activity. Restore the
   original autosave/preferences/fixtures during cleanup. No separate save-profile
   timing run is implied.
7. **NEEDS-INPUT ? visible journey sanity.** In the unlocked 6 Nov session, select
   and follow a consumer whose admitted offer survives feasibility through the
   storefront visit/service/return; visually verify continuous travel and the
   existing inspector's individual explanation. Repeat a refusal case. Bridge
   dumps alone do not establish pointer selection or visible continuity.
