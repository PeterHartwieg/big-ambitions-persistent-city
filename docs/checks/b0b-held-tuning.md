# B0b: Held tuning, base syncs and native checklist

## B0b second base sync with C3 and docs split - 1 November 2026 (offline only)

Merge development `8c24f24` (C3 #20, docs split #41) into B0b `eee1cfb`, with
common ancestor `3b8f7ec`, preserving two-parent ancestry for PR #16 and its
stacks. Resolve four Markdown conflicts into the new per-slice layout. B0b's
Runtime/Core/Persistence, schema amendment, tests and tools are unchanged; C3's
inactive additions match development exactly. No integration fix or additional
behavior change. No incoming schema-18/19 collision.

**2,002/2,002 tests and 2,002 discovered cases; all 50 foreground checks pass**,
including all 27 regression projects, the four Python harnesses, three demos,
six capacity fixtures, exact boundaries/history stress and performance diagnostics.
The other-dotnet guard precedes every invocation; all processes completed without
execution deadlines or background server reuse.

Old-schema migrations, 18 round-trips, held replay, first-continuation capture,
frozen-reader refusal and refused-source preservation pass. All six capacity wire
hashes/sizes/records/reservations and basic save/economy/reload digests match the
previous sync. Eight-candidate overhead stays **405 bytes**, charged once; CF32/
CF64 complete budgets remain **82.204% / 96.509%** (CF64 hard caps only, retaining
its >95% warning). Exact input/record limits and one-over preservation pass;
280,000 history generations retain all totals.

The audit preserves all 488 original B0b-head checks blocks, dated README notices,
23 performance rows, eight native checklists and their qualifications. Every
incoming nonconflicted file is unchanged, and local link targets pass. Recorded
capacity operations >=30 ms are flagged separately as save/load/startup latency;
D1's cold 20k-place **48.8884 ms aggregate** is flagged separately from a native
frame. Native-ID 1,024-scope policy p95 **0.258 ms** remains above the 0.20 ms
engineering target; held eight-shop capture/continuation p95 **24.4/136.8 us**.
No new performance win or native timing clearance. The mod-work and whole-frame
gates and four parked hotspots remain unchanged.

No game, rig/profile/lease, Steam/Workshop mutation, WSL, ordinary save, deployment
or release work. Combined locked development-head proof follows under merge rule
v2; pointer-dependent checks wait for Peter's 6 Nov playtest. No push or PR
publication. [Four conflict resolutions and full validation](../reviews/2026-11-01-b0b-sync2-base-pr-description.md)
and [receipts/hashes/measurements](../reviews/2026-11-01-b0b-sync2-base-results.json).

## B0b base sync after merged B0 and sibling slices - 1 November 2026 (offline only)

Merge approved B0b `c0af745` with development `3b8f7ec` (synced B0 #38, A1/A2/A3/F0/D1/C1/E2a), retaining both parents and common ancestor `7a6153c`. All 21 conflicts preserve both sides' intent. Every production C# file matches one parent exactly; all seven production files changed by B0b, including the entire tuning/held-context/codec implementation, remain byte-identical to approved B0b. No additional runtime/Core integration fix or behavior change. [Per-file resolutions and exact impact](../reviews/2026-11-01-b0b-sync-base-pr-description.md).

**Validation: 1,984/1,984 unit tests**, zero failed/skipped, **1,984 discovered cases**, all 32 requested checks and 17 additional checks pass (**49/49 sequential foreground commands**). This includes all 26 regression projects, all four Python harnesses, three demos, choice/rush/held-tuning and optional-stop performance/findings. The prescribed other-dotnet guard precedes every invocation; all processes completed, without execution deadlines or background build/compiler-server reuse. DLL SHA-256 `5f1173725cf7155fc795d3f43dc086b52bef08affac5b7383e683e9f48e00139`. [Tracked receipts, hashes, preservation and measurements](../reviews/2026-11-01-b0b-sync-base-results.json); full logs and both merge-base diffs are ignored under `build/b0b-sync-base-gate/`.

**Schema and capacity:** no competing schema-18 fields or schema-19 claim; B0b amends unshipped 18 in place. All 13-17 migrations, frozen readers, 18 round-trips, schema-17/pre-image-18 live-once capture, held replay and refused-save preservation pass. The actual eight-candidate image adds exactly **405 bytes**, charged once against the existing policy/header allowance, with unchanged records. Base recovery **115,049,515 characters**; reference **5,013,141 bytes**; 5,000 consumers **9,701,305**; 5,000 residents **17,298,040**; full-memory/table ceiling **141,087,195**. Complete CF32/CF64 reserved budgets stay **220,664,366 (82.204%) / 259,064,366 (96.509%)** and **2,111,867 / 3,711,867 records**. CF32 passes 85%; CF64 passes hard caps and retains the >95% warning, with no 85% claim. Exact input and 4,000,000-record boundaries pass; one-over/oversized old sources preserve original text/reference. The 280,000-generation history stress preserves every history/product total.

Same-schema development/merged comparison retains exact basic save bytes (**119,083 characters**), refusals and economy/reload digests: **849 orders, 797 purchases, two unconfirmed, 107,967,424,757,760 spent units**. Every approved B0b test, both complete CHECKS histories, all seven native checklists/locked/input-only qualifications, roadmap performance rows and test/project input unions survive. The shifted B0/A3/E2a checklist links are corrected. The nonconflicted historical PR #16 retarget sentence remains untouched.

**Offline timing limits:** all recorded capacity write/read/migration operations are >=30 ms and flagged as separate synthetic save/load/startup latency. D1's cold 20,000-place **42.9453 ms aggregate across bounded units** is flagged separately from a native frame. Native-ID 1,024-scope policy p95 **0.1831 ms**, full-decision p95 **0.3061 ms**, maximum full decision/continuation **10.522/3.1037 ms**; held eight-shop capture/continuation p95 **35.5/192.2 microseconds**, no >=30 ms stage sample. These are offline partial stages/batches, not native mod-work or whole-frame clearance; no new performance win. Mod-work p95 <5 ms, every >=30 ms spike flagged, whole-frame cost separate and four parked hotspots parked remain standing practice.

No game, rig/profile/bridge lease, Steam/Workshop mutation, WSL, ordinary save, deployment, release, push or PR publication action. Under merge rule v2, native validation follows on the batched combined locked development head; pointer-only checks wait for Peter's 6 Nov playtest. This ancestry sync targets `codex/pc-b0b-held-tuning` and must land with a merge commit before PR #16's development squash.

## Step B0b held tuning prerequisite — 31 October 2026 (offline only)

Fixes the inherited A3/B0 held-context defect, before B1 resumes on this branch.
A3's twelve effective coefficients are copied before shortlist weighting and
persisted in the one held decision. Evaluating, partial-quote and selected states
finish with that image after live edits/reload; the next uncaptured context uses
current settings on the same logical day. Logical draw keys are unchanged.

Schema **18 is amended in place** before player use; null/missing images in
schema 17/older migration and pre-image 18 capture live once at first continuation.
The frozen 13-17 contracts remain strict and exclude the new fields. Malformed,
oversized, non-finite and out-of-range images refuse without source replacement.
One neutral, bounded binary64 log-contribution slot per shortlisted shop reserves
B1's effective four-entry-memory result. No memory/loyalty/activation, watermark,
UI, extra settings or other feature work is included. [Amended wire contract](../population-schema18.md#b0b-amendment--31-october-2026).

**Offline measurements.** Baseline `192037b`, production source `9c06dd9`,
benchmark source `05517ca`; Windows .NET 10 Release, sequential foreground runs.
[Raw manifests, command order and red evidence](../reviews/2026-10-31-b0b-measurements.json).
Three fresh processes/head, 300 warmups then 3,000 measured decisions per market
size/process, interleaved before/after order. Table p50/p95 are medians of the
three run percentiles; max is the largest measured sample. Setup is outside timers;
capture stops before quotes, continuation includes every quote/selection/commit.
External system load is uncontrolled; these are synthetic Core stage costs.

| Shops / stage | Before p50 / p95 / max µs | After p50 / p95 / max µs |
| --- | ---: | ---: |
| 1 / capture | 3.7 / 8.3 / 112.2 | 3.9 / 6.6 / 90.5 |
| 1 / continuation | 15.6 / 26.5 / 999.5 | 15.7 / 21.6 / 1,017.8 |
| 8 / capture | 11.9 / 23.1 / 528.8 | 12.3 / 24.5 / 450.6 |
| 8 / continuation | 97.2 / 147.7 / 813.0 | 89.1 / 151.9 / 600.6 |

Capture allocation rises **152 bytes at one shop / 208 at eight**. Continuation
allocation remains approximately **11,561 / 61,747 bytes** respectively (sub-byte
per-decision process/JIT accounting noise is retained in raw data); route counts
remain exactly 2/shop and checkouts zero. No decision-stage sample reaches 30 ms
across **36,000 measured before/after decisions**. No native mod-work or whole-frame
result is inferred. The 8-shop p95 continuation change 147.7 → 151.9 µs is retained;
the noisy samples do not establish a continuation speedup or slowdown.

Retained DTO graph allocation is **136 → 280 bytes** with no candidates and
**1,624 → 1,832 bytes** with eight: **144 bytes/held decision + 8/candidate**, a
**208-byte** eight-shop addition. Managed GC retained deltas are approximately
135.6 → 279.2 and 1,562.5 → 1,768.4 bytes; they include GC accounting noise.
There is no per-consumer or per-day tuning snapshot. The allocation-free streaming
shape precheck avoids materializing an image: **136 → 0 bytes/check**, median
**1,375.69 → 187.79 ns** against the materializing prototype (300k calls/run), logged
in the roadmap Perf wins log. No per-frame reflection, scan, polling or new
continuation allocation was added; capture allocations belong to logical decisions.

All size fixtures below include full four-entry memory, the full 20k/2 MiB business
table and the same eight-candidate held image. Canonical characters equal UTF-8 bytes.

| Residents / consumers / visits | B0 bytes | B0b bytes | Streaming / semantic records (unchanged) |
| --- | ---: | ---: | ---: |
| 957 / 1,914 / 32 | 5,012,736 | 5,013,141 | 54,560 / 54,557 |
| 2,500 / **5,000** / 32 | 9,700,900 | 9,701,305 | 110,108 / 110,105 |
| 5,000 / 10,000 / 32 | 17,297,635 | 17,298,040 | 200,108 / 200,105 |
| CF32: 50,000 / 150,000 / 32 | 192,019,702 | 192,020,107 | 1,920,108 / 1,920,105 |
| CF64: 50,000 / 150,000 / 64 | 230,419,702 | 230,420,107 | 3,520,108 / 3,520,105 |

Every row adds **405 bytes**: 205 for tuning plus 25 neutral bytes/candidate.
The capacity ledger deducts those 405 exactly once from the existing policy/header
allowance. Complete future budgets remain **220,664,366 bytes / 2,111,867 records
(CF32, 82.204%)**, **259,064,366 / 3,711,867 (CF64, 96.509%)**. CF32 passes the
85% goal; CF64 passes hard caps, retaining the existing >95% warning. No cap rises.

| Fixture | Write ms before / after | Read ms before / after | 17->18 migration ms before / after |
| --- | ---: | ---: | ---: |
| reference | 110.9 / 121.2 | 329.0 / 363.2 | 439.2 / 452.4 |
| 5,000 consumers | 246.7 / 186.4 | 747.2 / 702.7 | 1,462.8 / 1,631.6 |
| 5,000 residents | 555.4 / 447.3 | 1,097.8 / 963.9 | 1,767.1 / 1,350.5 |
| CF32 | 2,661.9 / 2,738.5 | 8,772.3 / 8,960.2 | oversized 17 source refused |
| CF64 | 3,288.1 / 2,066.3 | 7,477.5 / 6,635.1 | oversized 17 source refused |

Capacity timings are one diagnostic sample/head/case, not p95 or native save timings.
**Every listed save/read/migration sample is >=30 ms and explicitly flagged as
separate save/startup/reload latency.** The reference write/read and 5k-consumer
migration increases remain visible; no universal codec performance win is claimed.
The four parked hotspots remain parked.

**Validation:** **1,862/1,862 tests**, discovery and **all 34 sequential foreground
commands pass** (the 31 requested plus choice, native-candidate and rush).
All **30 B0b tests** pass. Same-seed economy remains **849 orders / 797 purchases /
107,967,424,757,760 spent units**, with exact economy/reload digests unchanged.
[Final command manifest](../reviews/2026-10-31-b0b-offline-checks.json). The original B1 probe on archived B0 independently
reproduces seed 2, taste 0 → 3, burger → croissant, exactly two routes and zero
checkouts. Reintroducing just the live-selection lookup makes **5/7** evaluating/
partial-quote replay tests red; selected controls remain green. The fixed tests
cover actual successive consumers at the same minute, all A3 coefficients,
selected old saves, frame/event budgets, strict refusal/source preservation and
byte/record allocation. Production DLL SHA-256:
`34bbcf02fded476c79b58e84e3a005af38b979694435994e4f9220d2ce73a246`.
The final existing choice harness reports synthetic full-decision max **9.8138 ms**,
with no >=30 ms decision sample. Its 1,024-shop native-ID pure-policy p95 is
**0.2061 ms**, still marginally above the existing 0.20 ms engineering target;
full-decision p95 is 0.3194 ms. These do not certify the combined native gate.
The 957-resident routine harness's largest 16-event batch is **16.4495 ms**;
rush replay retains identical saved-state digests and zero modeled window backlog.
No game, rig, lease, installation, push, PR or merge action occurred.

**Review round 1 - 31 October 2026 (Opus-r1-1, documentation only).** Checks 1
and 2 now name the completed manual save's `persistentcity:state_v1` payload,
extracted through the bridge outside timing windows, as the held-image evidence
source. The schema-18 decision tuple slot and coefficient decoding order are
specified; no Runtime reader was added. The mandatory foreground rerun passed
**1,862/1,862 tests**, discovery and **all 34 commands** (31 requested plus choice,
native-candidate and rush); production DLL hash remains unchanged.
[Round-1 command receipt](../reviews/2026-10-31-b0b-r1-offline-checks.json).
Synthetic choice max was 10.7039 ms and routine 16-event batch max 11.4422 ms; neither
reached 30 ms. The 1,024-shop native-ID pure-policy p95 was 0.2515 ms, above the
existing 0.20 ms engineering target; these are offline diagnostics.

### In-game checks for 6 Nov rig

Pending the combined **rig_test session (Codex Sol, high)**, on a named disposable
FT fixture. Classification follows [locked-session feasibility, 84be71a](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/84be71a67aca97aefc0dcce7055caaf3b0257af8/docs/reviews/rig/2026-10-31-locked-session-feasibility/README.md).
No rig action occurred in this offline slice.

1. **LOCKED-OK — In-flight checkpoints.** Preserve original fixture/profile/config/preferences and record baseline/candidate DLL hashes, dependencies, seed, clock/body/graphics/population/shop setup. On the disposable FT copy, request manual saves through the bridge outside timing windows, then extract the completed save's `persistentcity:state_v1` payload through the bridge. Identify evaluating (including partial quotes) and selected-before-commit contexts from the decoded saved `Food.Residents[*][9]` decision slot. Read its `Tuning` image from that saved payload and decode all twelve coefficients in the documented wire order; record context/draw identifiers, shortlist, quote cursor/routes and selected result alongside checkout/receipt counters. The held-image evidence source is this persisted payload; Runtime exposes no live held-decision/tuning dump.
2. **LOCKED-OK — Config edit replay.** Change an A3 coefficient in the isolated config (use taste 0 → 3 with differing product preferences), reload those checkpoints and compare their saved held image with the image extracted through the bridge from a further manual save at an in-flight continuation boundary, outside timing windows. Compare unchanged-config control against edited-config replay: same held choice, route totals and checkout count; no repeated quote or checkout. On the same logical day, let the next uncaptured context capture, then take another manual save outside timing windows and extract its payload through the bridge to confirm the new held image contains the edited values without a day wait. Keep tuning out of the logical draw key.
3. **LOCKED-OK — Save/reload continuity.** Repeat save/reload at partial quote, selected, committed outward trip, service/payment and return boundaries. Reconcile exact identities, budgets/held funds, prices, stock, orders, receipts and return ownership; a paid/uncertain receipt must never retry checkout. Migrate a disposable schema-17 checkpoint and confirm null-image/neutral-memory defaults capture once on continuation. Refuse a deliberately malformed copied image without replacing its source.
4. **LOCKED-OK — Combined performance windows.** Interleave matched baseline/candidate idle and meal-rush windows plus a soak. Record original autosave; disable and verify off before every window and again after reload. Keep manual saves outside windows; invalidate any window containing save activity. Report **mod-work p50/p95/max**, with p95 <5 ms and every spike **>=30 ms** flagged by count/condition/component where known. Report whole-frame p50/p95/max and stalls separately. Include quote/query counts, allocations/GC if available and backlog/throughput; restore autosave/preferences/config during cleanup.
5. **LOCKED-OK — Saved size.** Read character/UTF-8 payload and native saved-file sizes outside timing windows at the reference population and a 5,000-consumer disposable fixture. Compare to B0 with matched histories, scopes and held state: the eight-candidate B0b image adds 405 canonical payload bytes, consumes the existing policy/header allowance and adds no collection records. Record actual native save sizes separately from the offline fixtures and preserve/restore all preexisting rig files.

## Development notices

> **B0b base sync - 1 November:** approved held tuning is reconciled with synced B0 and merged A1/A2/A3/F0/D1/C1/E2a. **1,984 tests and all 49 foreground checks pass**; B0b production code is unchanged, with no additional integration fix. Exact migrations/replay/refusals and the 405-byte image pass; CF32/CF64 reserved budgets stay **82.204% / 96.509%** (CF64 hard-cap pass). Offline only; combined native checks follow under merge rule v2. [Resolutions and evidence](../reviews/2026-11-01-b0b-sync-base-pr-description.md).

> **Step B0b held tuning — 31 October:** held A3 decisions now persist their captured coefficients across config edits/reload; fresh contexts use current values the same day. Schema 18 is amended in place with a neutral bounded B1 contribution slot. **1,862 tests and all 34 foreground checks pass**. Eight-candidate overhead: 405 saved bytes / 208 managed bytes. Offline only; combined 6 Nov rig checks remain pending. [Evidence](b0b-held-tuning.md#step-b0b-held-tuning-prerequisite--31-october-2026-offline-only) · [Contract](../population-schema18.md#b0b-amendment--31-october-2026).

## Roadmap notices

B0b base sync, 1 November: approved `c0af745` reconciled with development `3b8f7ec`, preserving all tuning/Core/Persistence code and both histories without an additional integration fix. **1,984 tests / 49 foreground checks pass**; the real **405-byte** image and unchanged **82.204% / 96.509%** reserved budgets are verified. All prior checklists/performance rows remain. [Resolutions and evidence](../reviews/2026-11-01-b0b-sync-base-pr-description.md). Native proof belongs to the batched combined locked development head; pointer-only checks wait for 6 Nov.

[Checks index](../../CHECKS.md).
