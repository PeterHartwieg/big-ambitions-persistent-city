# A5 integrated acceptance performance

1 November 2026, offline .NET 10 Release. [Integrated report, historical comparisons and remaining work](../checks/a5-integrated-acceptance.md#integrated-offline-performance). No Unity/Mono frame-time or native formatter measurement is claimed.

One cheap win in the touched popup code: reuse each place's address image within `ResidentInsight.Capture`. The image is local to that one open-popup refresh; it adds no closed/hover/per-frame work and caches neither success nor failure into the next refresh. Raw map keys and place state remain unchanged. The existing one-second Runtime refresh cadence and input hooks are unchanged. Tests check reformatting on the next capture and byte-identical inspector/simulation replay.

| Same no-food popup fixture, before → after | 957 residents | 5,000 residents |
|---|---:|---:|
| Address formatter calls per refresh | 4 → 2 | 4 → 2 |
| Allocated bytes per capture + text | 6,432 → 5,856 | 6,432 → 5,856 (rounded) |
| p50 ms per capture + text | 0.003144 → 0.003395 | 0.003417 → 0.003505 |
| p95 ms of batch means | 0.006279 → 0.007290 | 0.007809 → 0.007362 |

80 batches of 100 captures after eight warmups, same fixture and .NET runtime; baseline production `51ce709`, then the A5 cache. Managed stub formatter, native-format address IDs. Raw baseline/after aggregates in the A5 measurement JSON. This is **576 bytes (9%) less allocation and 50% fewer formatter calls**, not a demonstrated latency gain: timing includes changing desktop load/GC and is not an interleaved native comparison. No batch reaches 30 ms in these two measurements. The 5k after-run maximum batch mean is 0.174444 ms (17.4444 ms batch), not a mod-work frame.

The address cache is bounded by the selected resident's displayed places and three meal records, never the roster. The four parked hotspots—physical population, closure refresh, optional-stop refresh and save serialization—are unchanged. No other cheap safe measured improvement was identified in this small display-only diff. Legacy parser/object reuse and broader frame work stay outside A5; current decision, rush and save measurements report costs without rewriting them.
