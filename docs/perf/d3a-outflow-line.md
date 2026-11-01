# D3a outflow history/line: 1 November 2026, offline only

Implementation `7cd4809`, conservative boundary-day follow-up `88551e8`; Windows shared host, .NET 10 Release. Reproduce with
`tools/outflow-line-regression` (included in `python tools/run-d3a-checks.py`).
No game, rig, Unity execution, native mod-work or whole-frame sample is claimed.
Raw output and hashes are retained in the D3a results manifest linked from
[checks](../checks/d3a-outflow-line.md).

Taken cheap win: cache the immutable fourteen-day free projection and its text
until a settlement, rollover or requested day changes. The control repeats the
exact initial D3a projection algorithm and formatting on the same history; it
does not add deserialization/setup to the before arm. An assertion compares the
control and cached text byte for byte. Existing Ctrl+F8 refresh remains three
seconds and only runs while open; the new code adds no frame participant, route,
reflection, catalogue scan, polling or closed-panel allocation. Invalidation and
old-snapshot immutability have a dedicated unit test.

100 warmups; 2,000 measured calls for closed/control/open, 1,000 measured batches
for bursts/rush. Fixture: one exact fractional-price wallet, 365 days of prior
settlements, fourteen retained rows. Timers exclude fixture creation/JIT. Text
for a changed ledger is included in rush; the old intent is deliberately late.
Each burst makes 128 successful settlements and 128 repeated attempts (repeats
refuse). The closed arm is an empty timer control: structurally there is no
new closed-panel function call, rather than a claim about total panel/frame cost.

| Component/control | p50 / p95 / max ms | Allocated B/sample | Samples ≥30 ms |
| --- | ---: | ---: | ---: |
| Closed timer control | 0 / 0.0001 / 0.0020 | 0 | 0 |
| Before: rebuild projection and text every open request | 0.0138 / 0.0198 / 0.4503 | 14,336 | 0 |
| After: unchanged warm open request | 0 / 0.0001 / 0.0001 | 0 | 0 |
| 128 late settlements plus repeated attempts | 0.0494 / 0.0543 / 0.2375 | 32,768 | 0 |
| 128 rush settlements plus refreshed open text | 0.0621 / 0.0687 / 0.1943 | 47,394.8 | 0 |

The earlier standalone pre-gate run measured before/after p95 0.0223/0.0001 ms,
the same 14,336/0 B, burst p95 0.0520 ms and rush p95 0.1081 ms. These are two
shared-host runs, not an additional speedup. No measured D3a component spike
reached 30 ms in either run. The warm cache adds about 3,240 retained bytes in
exchange for eliminating repeated allocations.

Forced-GC heap deltas over 2,000 separately decoded histories: **1,528.656 B per
closed history**, **4,768.656 B after each free view/text is opened**. Arrays for
the measurement are allocated before the baseline; JIT/heap noise is amortized.
These are the named fixture's managed bytes, not an exact Unity heap bound.
Structural bounds are fourteen rows, one rollup, one cached fourteen-cell view
and one cached string, independent of residents and elapsed days. BigInteger
wire cells are capped at 26 digits. The 365-day fixture repeatedly asserts
retention and exact D2 ledger reconciliation; late bursts cannot grow row count.

Saved history: **280 ASCII characters / 293 property bytes / 15 charged records**
in this fixture. Contract maximum **1,024 history characters / 1,037 property
bytes / 15 records per food city**, **zero per consumer**. Population-only state
has zero added fields/records. [Schema contract](../population-schema24.md).
Capacity accounting subtracts actual history bytes/records from its existing
D3a reserve once; it does not raise either global cap or double-reserve history.

The unchanged D2 native-double regression also ran: **19,844 assertions**. Its
20k-alternative whole-day `AdvanceTo(128)` arm flags **one ≥30 ms offline batch,
max 31.5969 ms**. The awareness regression flags a **32.2273 ms whole dense-index
build** (one aggregate sample). These are shared existing operations with no finer
attribution, not single D3a operations, native mod-work or whole-frame evidence.
They remain flagged even though the new component timers are small.
The preceding gate also observed one **30.7708 ms D2 batch** and one
**34.0365 ms awareness whole-build** sample; those earlier flags are retained
separately, rather than relabelled as the final rerun's measurements.
The additional E4 full-history serialization regression flags **21/21 samples
≥30 ms per arm** (four/eight products): p50/p95/max
**143.7052/202.0061/202.1156 ms** and **156.7238/201.2296/205.8757 ms**.
These are whole offline save operations, not frame or ordinary mod-work samples;
no D3a-only cause or serialization speedup is inferred.

Combined native **mod-work p95 <5 ms** and every single mod-work spike **≥30 ms**
still require the ordinary combined 6 Nov session (closed/open panel, ordinary
play and meal rush). Whole-frame costs/stalls are tracked separately. The four
parked hotspots—physical population, closure refresh, optional-stop refresh and
save serialization—remain parked; required additive schema validation is not a
serializer rewrite. No separate performance-only rig run was performed or added.


## 1 November 2026 - D4 base-sync observation

D3a's history/projection code is unchanged in the D4 composition. First complete
merged matrix, source `503b57b`, reproduces **282,335 assertions** with the same
Release protocol. Historical D3a (`88551e8`) and merged shared-host timings are
independent samples; this is not native or matched whole-frame evidence.

| Operation | Historical p50 / p95 / max ms | Merged p50 / p95 / max ms | B/sample before -> after |
| --- | ---: | ---: | ---: |
| Closed | 0.0000 / 0.0001 / 0.0020 | 0.0000 / 0.0001 / 0.0018 | 0 -> 0 |
| Uncached open control | 0.0138 / 0.0198 / 0.4503 | 0.0135 / 0.0191 / 0.4314 | 14336 -> 14336 |
| Warm open | 0.0000 / 0.0001 / 0.0001 | 0.0000 / 0.0001 / 0.0001 | 0 -> 0 |
| 128 late settlements | 0.0494 / 0.0543 / 0.2375 | 0.0509 / 0.0875 / 0.2171 | 32768 -> 32768 |
| 128 settlements plus open | 0.0621 / 0.0687 / 0.1943 | 0.0614 / 0.1045 / 0.2021 | 47394.8 -> 47394.8 |

No component sample reaches 30 ms. Save history remains **280 characters / 293
property UTF-8 bytes / 15 charged records**. Managed retained closed/open
history remains **1,528.656 / 4,768.656 B** in this fixture. The warm open request
still allocates zero bytes. Native acceptance remains pending the combined rig;
whole-frame costs are separate. Full command receipts, repeat measurements and
all positive aggregate spike counters are preserved in the
[D4 sync manifest](../reviews/2026-11-01-d4-sync-base-results.json).


Required repeat observations on unchanged D3a component code:

| Matrix | Warm-open p95 ms | 128-settlement/open p95 / max ms | Warm-open B/request | History characters / property bytes / records |
| --- | ---: | ---: | ---: | ---: |
| d4 | 0.0001 | 0.1045 / 0.2021 | 0 | 280 / 293 / 15 |
| d3a | 0.0001 | 0.1022 / 0.1407 | 0 | 280 / 293 / 15 |
| d2 | 0.0001 | 0.0674 / 0.1217 | 0 | 280 / 293 / 15 |

No D3a component sample reaches 30 ms in any matrix; all repeat samples and
allocation/retention fields remain available in the sync manifest.
