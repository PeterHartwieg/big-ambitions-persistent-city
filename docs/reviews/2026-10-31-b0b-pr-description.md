# Persist captured tuning for held food decisions (B0b)

Target: `codex/pc-b0-schema18` (retarget to `codex/persistent-city` after B0 merges).
B1 resumes on top of this prerequisite.

A decision saved while evaluating could choose burger with taste weight 0, then
croissant after reloading with taste weight 3. Capture all twelve effective A3
coefficients before shortlist weighting and persist their exact bounded image.
Every held stage uses that image; new contexts take current values the same day.
The logical draw key remains tuning-independent.

Amend unshipped population schema 18 in place. Strictly validate the image and
preserve refused saves; schema 17/older and pre-image 18 capture live once on first
continuation. Frozen older readers retain their allow-lists and reject new fields.
Reserve a neutral per-shop binary64 log contribution for B1's bounded four-entry
memory, without memory policy, activation, watermark, UI or new settings. Bound all
held contexts, including empty shortlists, to the scheduler's one-decision limit.

The complete eight-candidate addition is **405 saved bytes**, charged once against
the existing 65,536-byte aggregate policy/header allowance, with no new collection
records. B1's full-precision future scalar allowance is at most 589 bytes including
A3 tuning. Details: [schema amendment](../population-schema18.md#b0b-amendment--31-october-2026).

**Offline measurements.** Baseline `192037b`, production source `9c06dd9`,
benchmark source `05517ca`; Windows .NET 10 Release, sequential foreground runs.
[Raw manifests, command order and red evidence](2026-10-31-b0b-measurements.json).
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
[Final command manifest](2026-10-31-b0b-offline-checks.json). The original B1 probe on archived B0 independently
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

Native **mod-work p95 <5 ms / single spikes <30 ms** and whole-frame measurements
remain pending the checks below. Whole-frame timing is tracked separately.

### In-game checks for 6 Nov rig

Pending the combined **rig_test session (Codex Sol, high)**, on a named disposable
FT fixture. Classification follows [locked-session feasibility, 84be71a](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/84be71a67aca97aefc0dcce7055caaf3b0257af8/docs/reviews/rig/2026-10-31-locked-session-feasibility/README.md).
No rig action occurred in this offline slice.

1. **LOCKED-OK — In-flight checkpoints.** Preserve original fixture/profile/config/preferences and record baseline/candidate DLL hashes, dependencies, seed, clock/body/graphics/population/shop setup. On the disposable FT copy, request manual saves through the bridge outside timing windows, then extract the completed save's `persistentcity:state_v1` payload through the bridge. Identify evaluating (including partial quotes) and selected-before-commit contexts from the decoded saved `Food.Residents[*][9]` decision slot. Read its `Tuning` image from that saved payload and decode all twelve coefficients in the documented wire order; record context/draw identifiers, shortlist, quote cursor/routes and selected result alongside checkout/receipt counters. The held-image evidence source is this persisted payload; Runtime exposes no live held-decision/tuning dump.
2. **LOCKED-OK — Config edit replay.** Change an A3 coefficient in the isolated config (use taste 0 → 3 with differing product preferences), reload those checkpoints and compare their saved held image with the image extracted through the bridge from a further manual save at an in-flight continuation boundary, outside timing windows. Compare unchanged-config control against edited-config replay: same held choice, route totals and checkout count; no repeated quote or checkout. On the same logical day, let the next uncaptured context capture, then take another manual save outside timing windows and extract its payload through the bridge to confirm the new held image contains the edited values without a day wait. Keep tuning out of the logical draw key.
3. **LOCKED-OK — Save/reload continuity.** Repeat save/reload at partial quote, selected, committed outward trip, service/payment and return boundaries. Reconcile exact identities, budgets/held funds, prices, stock, orders, receipts and return ownership; a paid/uncertain receipt must never retry checkout. Migrate a disposable schema-17 checkpoint and confirm null-image/neutral-memory defaults capture once on continuation. Refuse a deliberately malformed copied image without replacing its source.
4. **LOCKED-OK — Combined performance windows.** Interleave matched baseline/candidate idle and meal-rush windows plus a soak. Record original autosave; disable and verify off before every window and again after reload. Keep manual saves outside windows; invalidate any window containing save activity. Report **mod-work p50/p95/max**, with p95 <5 ms and every spike **>=30 ms** flagged by count/condition/component where known. Report whole-frame p50/p95/max and stalls separately. Include quote/query counts, allocations/GC if available and backlog/throughput; restore autosave/preferences/config during cleanup.
5. **LOCKED-OK — Saved size.** Read character/UTF-8 payload and native saved-file sizes outside timing windows at the reference population and a 5,000-consumer disposable fixture. Compare to B0 with matched histories, scopes and held state: the eight-candidate B0b image adds 405 canonical payload bytes, consumes the existing policy/header allowance and adds no collection records. Record actual native save sizes separately from the offline fixtures and preserve/restore all preexisting rig files.
