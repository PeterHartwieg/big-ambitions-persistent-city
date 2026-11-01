# E3 pizza performance evidence - 1 November 2026

No new performance win is claimed. The shared read branches are unchanged: one pinned descriptor was added. Review found once-bound metadata, bounded explicit scans, no success-path diagnostic strings and no redundant identity set; no additional cheap safe measurable optimization was identified or taken. All four parked hotspots remain untouched.

Release .NET 10 conditional native doubles. Separate fresh processes for base `5877453` donut/cupcake/salad before and code `acf8081` all four after. Cold is one first call including JIT, descriptor and metadata binding, not a percentile. Warm is 30 warmups plus 1,001 individually timed valid reads per size; fixture construction excluded, one bag counter included. First three harnesses are unchanged; pizza reuses their native doubles and timing method with its own entry point/cases. Foreground sequential runs use the prescribed other-dotnet guard. Host scheduling remains uncontrolled; timing differences are not causal wins.

| Reader / revision | Cold ms / B (one call) | 2 entries warm p50 / p95 / max ms; B/call | 2,000 entries warm p50 / p95 / max ms; B/call |
| --- | --- | --- | --- |
| Donut before (`5877453`) | 2.1720 / 1,040 | 0.0008 / 0.0011 / 0.0143; 200 B | 0.0975 / 0.1653 / 1.4347; 49,104 B |
| Cupcake before (`5877453`) | 3.0193 / 1,040 | 0.0005 / 0.0006 / 0.0068; 200 B | 0.1032 / 0.2266 / 2.0337; 49,104 B |
| Salad before (`5877453`) | 2.0517 / 1,040 | 0.0005 / 0.0009 / 0.2228; 200 B | 0.0851 / 0.1589 / 1.4991; 49,104 B |
| Donut after (`acf8081`) | 2.1335 / 1,064 | 0.0005 / 0.0005 / 0.0082; 200 B | 0.0975 / 0.1589 / 1.3791; 49,104 B |
| Cupcake after (`acf8081`) | 1.7445 / 1,064 | 0.0005 / 0.0007 / 0.0089; 200 B | 0.0956 / 0.1338 / 1.4432; 49,104 B |
| Salad after (`acf8081`) | 2.0674 / 1,064 | 0.0004 / 0.0008 / 0.0150; 200 B | 0.0867 / 0.1476 / 1.6080; 49,104 B |
| Pizza after (`acf8081`) | 1.7546 / 1,064 | 0.0004 / 0.0005 / 0.0075; 200 B | 0.0825 / 0.1122 / 1.5820; 49,104 B |

Warm allocations remain **200 / 49,104 B/read** at 2 / 2,000 entries for every candidate. Cold allocation changes **1,040 -> 1,064 B**, consistent with one additional **24 B first-use descriptor**, with no retained native-data cache. All measured E3 single-reader cold/warm samples are below 30 ms; zero >=30 ms samples. No ordinary-play caller, hooks, polling, per-frame reflection/scans/allocations or automatic initialization exists. **Zero calls means zero reader work/allocations**, by source isolation.

These are offline reader costs, not native combined mod-work or whole-frame results. The normal combined 6 Nov session must report mod-work p50/p95/max, require **p95 <5 ms** and flag every **spike >=30 ms** with count/condition/component. Whole-frame timings/stalls stay separate. No special performance-only run. [Checks and numbered handoff](../checks/e3-pizza.md); [raw measurements, commands and log hashes](../plans/e3-pizza-offline-results.json).

**Separate existing offline flags:** D1's 20,000-place empty-cache aggregate build is **43.1023 ms (>=30 ms)**; its bounded 16-read unit is **0.0023 ms**, and warm refresh **1.5850 ms**. C1's synchronous 1,024-business cold snapshot has **p95 6.4978 ms (>=5 ms)**, max **10.6797 ms**; continuation-unit p95/max is **0.0019 / 2.1303 ms**, zero >=30 ms samples. These unchanged aggregate/stress conditions are not E3 reader samples, native frames or combined mod-work clearance. No optional-stop performance comparison arm was run; its required functional runner passed. Raw diagnostic lines are preserved in the machine receipt.
