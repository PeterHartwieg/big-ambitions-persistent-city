# C2b campaign evidence performance — 1 November 2026

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
