# C2b campaign evidence performance — 1 November 2026


All measurements below were made on C2b based on `51ce709`, before E1 weather
and A5 integration. They remain historical evidence; this sync claims no new
performance win or native proof. Merged-tree offline results are recorded in
[the owning checks file](../checks/c2b-campaign-awareness.md).

## Round-2 captured-hour fix / Sol-r2-2

The generation index now holds immutable saved images instead of only their
latest active hour. Each candidate needs one dictionary lookup and at most eight
type-mask checks at the meal's hour, including after a newer publication or
reload. Both private buffers still reuse capacity. No new performance-only
optimization is claimed; the change fixes replay while keeping the existing
source/shortlist/route bounds. Saved bytes and records are unchanged.

[Raw results, reader timings and source fingerprints](c2b-r2-offline-results.json).
Same offline Release .NET 10 fixture/method as round 1: 30 warmups, 301 hourly
and rebuild samples, 10,001 query samples; 1 then 1,024 images in one foreground
run. Sequential tiered JIT, host and GC effects limit cross-size/time comparisons.
The 32-lookup workload uses distinct businesses at 1,024 images and repeats the
single ID at one image to exercise the full union's maximum mask-probe count.

| Operation, 1 / 1,024 images | p95 ms | Max ms | B/op |
| --- | --- | --- | ---: |
| Eight current-hour active lookups | 0.0004 / 0.0003 | 0.0292 / 0.0094 | 0 / 0 |
| Eight delayed-hour active lookups | 0.0004 / 0.0002 | 0.0153 / 0.0146 | 0 / 0 |
| Eight unobserved-hour lookups, all eight types checked | 0.0013 / 0.0003 | 0.1741 / 0.0172 | 0 / 0 |
| 32 unobserved-hour lookups (union bound) | 0.0047 / 0.0009 | 0.2338 / 0.0199 | 0 / 0 |
| C1 + publication | 0.0142 / 3.1701 | 0.0241 / 4.6240 | 2,880 / 2,198,552 |
| Day/type index rebuild | 0.0031 / 0.5375 | 0.0141 / 0.6897 | 72 / 16,440 |
| Eight-slot source | 0.0074 / 0.0032 | 0.2362 / 0.1695 | 0 / 0 |
| Production adapter | 0.0167 / 4.5993 | 0.0636 / 8.2773 | 3,244 / 2,358,090 |

Absent/disabled paths still allocate zero. All round-2 component series have
zero spikes >=30 ms. **Separate cold C1 reader p95 6.3476 ms remains above
5 ms**, max 10.1882 ms; warm p95 2.1562 ms, max 2.7177 ms. These component
numbers do not certify combined native mod-work p95 <5 ms. Whole-frame timing
was not measured and remains separate. Final-head rig proof remains pending.

## Round-1 owner decision 1 / review fixes

All residents now use the same observed active-campaign preference (fixed 1.25
weight factor and half soft-distance input). Group policy and target-match tuning
are removed. Native sampling follows ownership activation; missing candidate reads
age retained history without new bits. This run is offline only; final-head
checks are batched into the combined locked rig under merge rule v2; no rig
proof is claimed. The tables after this round-1
section are historical initial-implementation evidence.

[Raw round-1 results and source fingerprints](c2b-r1-offline-results.json).
Release .NET 10; same 1/1,024-business C1 doubles, eight saved days, 30 warmups,
301 hourly/rebuild samples and 10,001 eight-slot queries. No native state changes.

| Final operation, 1 / 1,024 images | p95 ms | Max ms | B/op |
| --- | --- | --- | ---: |
| C1 + publication | 0.0151 / 2.6336 | 0.0183 / 3.3697 | 2,880 / 2,198,552 |
| Day/type index rebuild | 0.0032 / 0.4893 | 0.1667 / 0.6301 | 72 / 16,440 |
| Eight-slot source | 0.0062 / 0.0065 | 0.1908 / 0.0358 | 0 / 0 |
| Actual all-resident production adapter | 0.0127 / 4.2356 | 0.0322 / 8.9763 | 3,244 / 2,358,090 |
| No-campaign count probes | 0.0002 / 0.0849 | 0.0150 / 0.7460 | 0 / 0 |
| Feature disabled | 0.0001 / 0.0001 | 0.0001 / 0.0164 | 0 / 0 |

**Initial round-1 maximum-size p95 exceeded 5 ms:** C1/publication 10.0878 ms
(max 28.8639 ms), production adapter 8.8877 ms (max 14.3554 ms); zero >=30 ms
samples in those series. These results are retained, not pooled into a better
number. **Taken win:** reuse two private active-hour dictionaries with atomic
publication, cutting publication allocation **2,300,768 -> 2,198,552 B/op** and
rebuild allocation **118,656 -> 16,440 B/op**. The repeat gives the table above;
wall-time differences include host/JIT/GC variability, so only allocation removal
and eliminated dictionary resizing have direct causal proof.

**Taken neutral-path win:** derive/cache the captured active marker at capture or
strict decode. Eight known-shop checks over a 24-ID held image previously walked
the image per offer: p50/p95/max **0.0019/0.0028/0.4650 ms**. Cached checks take
**0.0001/0.0002/0.0220 ms**, with **0 B/op on both sides** (30 warmups, 10,001
samples). No campaign-source/index work occurs when absent; no per-offer held-ID
scan is added to the ordinary path. Day/type rather than group/day/type indexes
also reduce the four fixed index/work arrays from 512 to 64 slots each, without
adding an identity or increasing any 32/8/16 cap.

All final round-1 component series above have **zero spikes >=30 ms**. C1's
separate cold 1,024-business diagnostic still has p95 **6.3118 ms** (max 11.4315
ms), explicitly above the comparison target; warm p95 2.2145 ms, zero >=30 ms
samples. Component numbers are not native mod-work or whole-frame gate proof.
Shared reference lookup still allocates zero (1,024-ref p95 0.0002 ms, max
0.0007 ms); the duplicate-index control allocates 227,440 B and p95 0.2219 ms.
Saved images remain 265 ASCII characters and **282 / 274,446 bytes** for field
plus 1 / 1,024 images with existing shared references, charged once each. Active
modifier bit 64 reuses the one held Sources scalar, costing at most one extra
decimal character per union record (<=32/city), with no new field or record.

## Initial measurements (historical)

Offline Release .NET 10, local Windows CPU. These are component measurements,
not Unity/Mono mod-work or whole-frame results. Foreground only, other-dotnet
wait before each run. Harness: `tools/campaign-awareness-regression`.

Each business double has all six verified C1 types; eight days are filled before
301 measured hourly observations after 30 warmups. Each observation completes C1
units of 16 in one synchronous main-thread call and publishes a complete image.
The fixed eight-slot query uses 10,001 samples; source construction is outside
those query timings. No native state changes in the adapter-double comparison.

Taken cheap wins: reuse two private group-index buffers/capacities with atomic
swaps; sort business identities once so every group/day/type bucket inherits
ordinal order; transfer private mask-array ownership instead of cloning it again.
The public constructor still copies caller arrays. No shared hotspot is rewritten.

| Operation at 1,024 businesses | Before p50 / p95 / max ms | After p50 / p95 / max ms | Before → after B/op |
| --- | --- | --- | ---: |
| C1 read + reviewed-policy publication | 3.8722 / 7.4787 / 56.1014 | 2.2701 / 3.4398 / 4.3782 | 4,054,648 → 2,198,552 |
| Index rebuild | 2.1511 / 2.9224 / 3.4135 | 0.5398 / 0.6484 / 1.0122 | 1,585,816 → 16,440 |
| Eight-slot query | 0.0015 / 0.0078 / 0.1583 | 0.0009 / 0.0052 / 0.0796 | 0 → 0 |
| Production Unknown-target adapter, complete hour | 3.0805 / 4.6412 / 7.7446 | 2.8417 / 4.2805 / 7.4704 | 2,624,608 → 2,350,208 |

**Before: two hourly samples ≥30 ms, max 56.1014 ms, explicitly flagged.** The
first series' p95 also exceeded 5 ms. After: zero ≥30 ms samples in these rows.
Do not discard the earlier spikes or use the improved component p95 as native
gate proof. JIT/GC/cache/host scheduling affect these small series; no statistical
confidence interval or whole-frame measurement is asserted.

| After at 1 business | p50 / p95 / max ms | B/op |
| --- | --- | ---: |
| C1 + reviewed publication | 0.0133 / 0.0265 / 0.2472 | 2,880 |
| Index rebuild | 0.0035 / 0.0051 / 0.0299 | 72 |
| Eight-slot query | 0.0059 / 0.0110 / 0.3530 | 0 |
| Production Unknown-target hour | 0.0127 / 0.0287 / 0.3221 | 3,216 |

No-campaign **source/index/query work is zero**, with zero steady-state
allocations. Detecting a newly started native campaign still requires bounded
native list-count probes at the existing hour hook; it is not literally zero CPU
work. At 1 / 1,024 businesses those probes measured p95 **0.0002 / 0.0737 ms**,
max **0.0007 / 3.3573 ms**, **0 B/op**. Feature-disabled hours return immediately:
p95 **0.0001 / 0.0001 ms**, max **0.0001 / 0.0025 ms**, **0 B/op**. No full C1 snapshot reads,
publication, index rebuild or per-consumer campaign scan occurs in those paths.
The final bounded preflight checks only saved eligible food scopes (≤1,024),
never the whole registration catalogue.

### Final boundary review repeat

The empty preflight now goes through C1's verified field binding, including
missing/changed-field refusal. The count-probe figures above are from that final
path, after metadata binding, and include its reflection cost at the hour hook.
No per-frame reflection is added. Machine-readable final results and source
hashes: [offline results](c2b-offline-results.json).

| Final operation, 1 / 1,024 images | p95 ms | Max ms | B/op |
| --- | --- | --- | ---: |
| C1 + reviewed-policy publication | 0.0164 / 2.3997 | 0.0669 / 2.8717 | 2,880 / 2,198,552 |
| Group index rebuild | 0.0051 / 0.4096 | 0.0198 / 0.5800 | 72 / 16,440 |
| Eight-slot source | 0.0088 / 0.0056 | 0.1187 / 0.0143 | 0 / 0 |
| Production Unknown-target hour | 0.0123 / 3.7834 | 0.0233 / 7.1044 | 3,216 / 2,350,208 |

**Additional taken win: share the production reference index.** Publication and
food memory now use the same already-current index, so the next consumer cannot
inherit a 1,024-reference synchronization. The explicit duplicate-index control
performs the original deferred catch-up after the same hourly insertion. At 1 /
1,024 newly inserted references, its first lookup allocates **272 / 227,440 B**
and takes p95 **0.0006 / 0.1711 ms**. The shared production index takes **0 B**,
p95 **0.0001 / 0.0002 ms**, max **0.0001 / 0.0008 ms**. Each comparison has ten
warmups and 101 fresh fixtures; publication, fixture construction and the one
reflection lookup selecting the store are outside both measured lookups. The
unit invariant additionally checks the first actual `FoodCommerce` memory lookup
allocates zero after 1,024 hourly inserts. There are no ≥30 ms samples here.

The final C1 standalone **cold** 1,024-business diagnostic p95 is **5.1061 ms**,
max **12.0372 ms**, explicitly above the 5 ms component comparison target; warm
p95 **2.1035 ms**, max **3.2725 ms**. Both series have zero ≥30 ms samples. This
does not replace C2b's actual scheduler series or certify the native combined gate.

The inherited C2a regression's separate complete-build fixtures also recorded
**six ≥30 ms samples** at 20,000 places: dense three/max **57.6692 ms**, sparse
three/max **37.6307 ms**. These are synchronous setup measurements, not the runtime
64-item continuation or C2b sampling. Its admission samples have no ≥30 ms spikes
(max **0.2627 ms**). The inherited index is unchanged; these costs remain flagged.

Saved image wire is 265 ASCII characters. Campaign field plus images adds
**282 / 274,446 bytes** and **1 / 1,024 records** above 19 with existing shared
references, independently of resident count. An empty field costs 15 bytes.
New generation-reference bytes/records remain separately charged. No resident
allocation or reserve increase is introduced.

Native combined gate remains **mod-work p95 <5 ms; every spike ≥30 ms flagged**.
Whole-frame intervals/stalls must be tracked separately during the normal combined
6 November rig session. The four parked hotspots remain parked. Full checklist:
[C2b checks](../checks/c2b-campaign-awareness.md#in-game-checks-for-6-nov-rig).
