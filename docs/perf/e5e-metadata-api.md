# E5e metadata API — 2 November 2026, offline

## Roadmap perf-log entry (moved 6 Nov 2026)

- E5e - [Skip all-refused claim walks and reuse images across empty-provider lifecycles](e5e-metadata-api.md): publication p95 0.0590 -> 0.0066 ms; empty lifecycle 512 -> 128 B/burst; host copy 673104 -> 350496 B; offline components only.

Final production code: `545f589`. .NET 10 Release component timings on this Windows host, detached inputs only; no game/rig execution. These are not combined in-game mod-work or whole-frame timings. The next batched rig must check **combined mod-work p95 <5 ms**, flag every **>=30 ms** spike and record whole-frame costs/stalls separately. Physical population, closure refresh, optional-stop refresh and save serialization remain parked.

[Primary data/log hash](e5e-metadata-api-results.json); [independent complete replay](e5e-metadata-api-provider-count-results.json); [full checks manifest](../checks/e5e-metadata-api-results.json). Reproduce through the guarded foreground metadata API regression program or full validator. These final-source data supersede pre-isolation-repair timings.

501 samples after 100 warmups; resolve/clean publication use 1,001 windows of 100 calls, and loaded-name copies use 101 calls. Batched p50/p95/max are **window averages, not individual maxima**; raw maxWindowMs/windowsAtLeast30ms retain undivided windows so averaging cannot hide a 30 ms stall. Individual mutation timers exclude fixture setup/cleanup (GC includes setup); bursts include the entire named operation. Instrumentation Actions never enter production APIs. Raw native-setup/route/provider-callback/frame-participant zero fields describe structural absence, not measured game counters. Retained managed heap deltas use full GC and are estimates.

Empty/256-entry/budget resolve p95 **0.000518/0.000047/0.000726 ms**, **0 B/call**. Full-width register/unregister/conflict-burst p95 **0.1084/0.0414/0.1539 ms**. Maximum-budget publication p95 **0.0117 ms**, retained approximately **422,128 B** excluding shared loaded-name input strings/image. At 256 providers publication/burst p95 is **0.0348/0.4989 ms**, max burst **3.1737 ms**. First uses **6.8386/6.7279 ms** are separate startup measurements. Neither process recorded a measured E5e window >=30 ms.

| Condition | p50 ms | p95 ms | max ms | B/call |
| --- | ---: | ---: | ---: | ---: |
| 0 products: resolve | 0.000323 | 0.000518 | 0.040762 | 0 |
| 0 products: cleanPublication | 0.000028 | 0.000043 | 0.000182 | 0 |
| 0 products: idempotent | 0.000300 | 0.000400 | 0.006900 | 0 |
| 0 products: replaceAndPublish | 0.000600 | 0.002400 | 0.015600 | 424 |
| 256 products: resolve | 0.000038 | 0.000047 | 0.000127 | 0 |
| 256 products: cleanPublication | 0.000027 | 0.000028 | 0.000069 | 0 |
| 256 products: idempotent | 0.000300 | 0.000300 | 0.003900 | 0 |
| 256 products: replaceAndPublish | 0.026900 | 0.041900 | 0.527100 | 47416 |
| 0 products: register | 0.000100 | 0.000100 | 0.000200 | 0 |
| 0 products: unregister | 0.000200 | 0.000200 | 0.000400 | 0 |
| 0 products: conflictRegister | 0.000100 | 0.000200 | 0.000900 | 192 |
| 256 products: register | 0.103100 | 0.108400 | 0.196500 | 69504 |
| 256 products: unregister | 0.037300 | 0.041400 | 0.125000 | 0 |
| 256 products: conflictRegister | 0.104300 | 0.127200 | 0.233300 | 69696 |
| 0-product lifecycle/publication burst | 0.000400 | 0.000500 | 0.006500 | 128 |
| 1-product lifecycle/publication burst | 0.001400 | 0.001800 | 0.005200 | 1024 |
| 256-product lifecycle/publication burst | 0.167700 | 0.186200 | 0.282900 | 95008 |
| 256-product conflict/removal/publication burst | 0.135500 | 0.153900 | 0.347800 | 117192 |
| Maximum budget: resolve | 0.000437 | 0.000726 | 0.001362 | 0 |
| Maximum budget: boundedPublication | 0.007800 | 0.011700 | 0.181200 | 22424 |
| Maximum budget: cleanPublication | 0.000029 | 0.000029 | 0.000085 | 0 |
| 256 providers: resolve | 0.000108 | 0.000114 | 0.000165 | 0 |
| 256 providers: publication | 0.031900 | 0.034800 | 0.181500 | 47416 |
| 256 providers: allProviderUnregisterRegisterBurst | 0.260700 | 0.498900 | 3.173700 | 139768 |
| allRefusedWin before | 0.054500 | 0.059000 | 0.255700 | 22424 |
| allRefusedWin after | 0.005600 | 0.006600 | 0.157400 | 22424 |
| emptyProviderWin before | 0.000500 | 0.000800 | 0.008500 | 512 |
| emptyProviderWin after | 0.000200 | 0.000200 | 0.002100 | 128 |
| 0 loaded names: before capacity reuse | 0.000100 | 0.000200 | 0.000300 | 88 |
| 0 loaded names: lifecycle-only copy | 0.000100 | 0.000200 | 0.000300 | 88 |
| 256 loaded names: before capacity reuse | 0.020000 | 0.022200 | 0.029400 | 16104 |
| 256 loaded names: lifecycle-only copy | 0.014600 | 0.016400 | 0.017000 | 6032 |
| 16384 loaded names: before capacity reuse | 0.674800 | 1.243700 | 1.459700 | 673104 |
| 16384 loaded names: lifecycle-only copy | 0.435200 | 0.949500 | 1.220000 | 350496 |

Retained registry + image estimates at empty/256 entries/maximum claim budget: **448 / 64,776 / 422,128 B**, excluding shared names/host image. Lifecycle-only loaded-name image estimates at 0/256/16,384 names: **64 / 5,976 / 350,496 B**, excluding shared input strings. Copy this immutable evidence once per catalogue publication and reuse it; registry publication performs at most 256 lookups and never enumerates the whole catalogue.

Maximum claim budget: **524,200/524,288 units**, **256 names/16 providers**; a separate fixture saturates 256 providers and 256 names. There is no impossible over-budget 256-by-256 matrix. Width-0 mutation uses an empty handle; the 0-product idempotence/replacement fixture registers its first entry during warmup, while its resolve/clean measurements are genuinely empty. Unit tests separately prove escaped/UTF-8 refusal, copied input, and 16,384/16,385 host-image bounds with one-extra-step overflow detection. E5e adds no save fields.

## Taken low-risk wins

All-refused publication p95 **0.0590 -> 0.0066 ms**, allocation unchanged at **22,424 B/publication**. Empty-provider lifecycle + publications **512 -> 128 B/burst**, p95 **0.0008 -> 0.0002 ms**.

The generated policy-matched before arm changes only the all-refused claim walk and empty-provider image invalidation; both arms share the same immutable host input. Skipping claims already proven conflicted/unloaded cannot contribute a value; empty providers alter bookkeeping without changing effective metadata. Tests assert image reuse for empty lifecycles with empty/populated registries. Both final processes retain matched controls. Clean registry publication, resolution and identical re-registration allocate 0 B; constructing a resolver wrapper at a context boundary is separate. There is no new frame participant, reflection/polling/scanning, provider callback, per-consumer product array or extra route slot.

Known collection counts are validated before capacity allocation; pre-sizing removes growth allocations while the streaming 16,384-entry guard still bounds unknown/misreported counts. Host-image copy at 16,384 names: **673104 -> 350496 B/copy**, p95 **1.2437 -> 0.9495 ms**.
Loaded-name copy timings use short synthetic names, not maximum-length names or native loaded-game evidence.
