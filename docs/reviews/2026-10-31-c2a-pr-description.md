# C2a: bounded Core awareness — review r1 fixes

Targets `codex/pc-b0b-held-tuning` (B0b c0af745). Memory remains an empty seam,
local/routine plus positive daily discovery feed the same eight-shop shortlist,
and campaigns remain unknown/unused. Full feasibility, trait/offer/outside choice
and <=16 routes remain authoritative. No B1/B2/C1/D1/F0 dependency, research, rival
behavior, UI or native write is introduced.

Review fixes in **19289e3**:

- **Sol-r1-2:** index replacement now yields through private 64-item collection,
  merge-sort, hash and cell phases. It pins the next unconsumed event until atomic
  publication; replacement/reload preserve event order without consuming a context.
- **Sol-r1-3 / Opus-r1-4:** schema 19 refuses null awareness on/after activation;
  pre-activation/schema-18 held decisions retain their captured terms.
- **Opus-r1-1:** the present shop uses Origin slot 0 deterministically inside the
  existing bounds, including random draws 0; feasibility can still refuse it.
- **Opus-r1-2:** candidacy flips no longer invalidate geometry.
- **Opus-r1-3:** proposed diagnostic sampling/ranking uses the held/current stop
  mode and captured coefficients.

**Validation:** **1,911 tests**, including **49 awareness cases**; test discovery
and **33/33 foreground gate commands** pass (31 requested plus native-candidate
and choice). Five red regression cases are now green; additional tests cover
partial publication, 1,024/20k work bounds, restart/reload/cadence and unchanged
eligibility-only revisions. Exact historical economy/reload digests are retained.
[Command evidence](2026-10-31-c2a-r1-offline-checks.json),
[rebuild measurements](2026-10-31-c2a-r1-rebuild.json),
[wire/interface contract](../population-schema19.md).

Six complete builds/layout/process in three fresh sequential .NET 10 Release
processes, including cold/JIT work. Unit timers include builder creation, managed-scope projection and place lookup. Total
CPU is the sum across yielded units, never a frame/spike statistic.

| Alternatives / layout | Work unit p50 / p95 / max ms | Measured units | Max items/unit | Whole build summed CPU p50 / max ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1,024 / dense | 0.0033 / 0.0121 / 5.5345 | 7,362 | 64 | 1.9222 / 16.0049 |
| 20,000 / dense | 0.0027 / 0.0090 / 3.7794 | 169,020 | 64 | 36.3103 / 42.2991 |
| 1,024 / sparse | 0.0035 / 0.0123 / 0.3889 | 8,172 | 64 | 2.1441 / 2.6267 |
| 20,000 / sparse | 0.0021 / 0.0095 / 11.8403 | 187,344 | 64 | 41.1984 / 53.3200 |

Across **371,898 work units**, **0 units >=30 ms**, largest **11.8403 ms**.
The old normal-frame synchronous rebuild path had 40 >=30 ms samples, max78.1916 ms;
those whole-build costs are now split into bounded continuations. Six full builds
per case/process give 409 /9,390 /454 /10,408 units per dense1k/dense20k/sparse1k/
sparse20k build: at the unchanged 128-unit cap, 4 /74 /4 /82 budget-only frames,
with the existing time cap potentially yielding sooner. These totals expose
completion/catch-up cost; yielding does not consume meals or advance event order.
Eligibility-only changes incur **zero geometry rebuilds**, covered by native-source
cursor/reload tests. Source-slot/shortlist/route limits and persisted bytes stay fixed.
The native combined p95 <5 ms / spike <30 ms gate is pending the combined rig;
whole-frame timing stays separate. The four parked hotspots remain parked.

Schema **19** wire and allocation are unchanged by r1. The held addition remains
**3,074 bytes/city** (3,038 image +13 field +23 header) at 1,914 and 5,000 consumers,
with **zero recurring per-consumer/day bytes**. Saturated B0 fixtures add 9,514 bytes
and 24 records, charged once against existing reserves. CF32/CF64 full future
budgets remain 220,664,366 /259,064,366 bytes and 2,111,867 /3,711,867 records;
CF64 retains its existing >95% warning. [Original capacity/admission measurements](2026-10-31-c2a-measurements.json)
retain before/after source sampling/rank and allocation results; r1 replaces their
synchronous runtime rebuild path. Ordinary trip source/rank semantics and the
24/32/8/16 bounds remain unchanged; existing-stop admission is restored.

No game/rig work, push or merge was performed by this fixer.

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
   Repeat without state changes and after reload; attach matching dumps. Include
   at-stop contexts in a dense catalogue with random draws 0 and 4: the present
   eligible shop must occupy Origin slot 0 inside the existing bounds.
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
   include a soak and a catalogue-change/rebuild observation. Verify rebuild
   work yields between units, blocks publication until complete and restarts
   coherently on replacement; eligibility-only flips must perform zero rebuilds. Record mod-work
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
