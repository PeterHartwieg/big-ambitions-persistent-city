# E3 salad performance evidence

**Reader timings and allocations:** Release .NET 10, conditional native field doubles. Separate fresh processes for frozen cupcake-head donut/cupcake before and all three readers after. Cold is one first call including JIT, descriptor and metadata binding, not a percentile. Warm is 30 warmups plus 1,001 individually timed valid reads at each size; fixture construction excluded. Furniture includes the bag counter. No causal timing improvement is claimed.

| Reader | Cold ms / allocated B (one sample) | 2 entries warm p50 / p95 / max ms; B per call | 2,000 entries warm p50 / p95 / max ms; B per call |
| --- | --- | --- | --- |
| Donut before (`682aa77`) | 2.6150 / 1,016 | 0.0005 / 0.0006 / 0.0074; 200 B | 0.0994 / 0.1798 / 2.1501; 49,104 B |
| Cupcake before (`682aa77`) | 2.4321 / 1,016 | 0.0007 / 0.0008 / 0.0123; 200 B | 0.0962 / 0.1651 / 1.7164; 49,104 B |
| Donut after (`817b215`) | 1.7729 / 1,040 | 0.0005 / 0.0006 / 0.0089; 200 B | 0.0978 / 0.1697 / 1.3974; 49,104 B |
| Cupcake after (`817b215`) | 2.0320 / 1,040 | 0.0005 / 0.0006 / 0.6467; 200 B | 0.0993 / 0.1876 / 1.8019; 49,104 B |
| Salad (`817b215`) | 2.2668 / 1,040 | 0.0005 / 0.0006 / 0.0046; 200 B | 0.0967 / 0.1670 / 1.3336; 49,104 B |

Warm allocation is unchanged at **200 / 49,104 B per call** for 2 / 2,000 entries; the third pinned descriptor adds **24 B once** on first use (**1,016 -> 1,040 B** cold allocation). **Zero recorded single-reader E3 samples >=30 ms**, including cold. No normal-play caller, hooks, polls or scheduled scans exist: **zero ordinary-play per-frame reader reflection/scans/allocations and zero reader cost when uncalled**, by source isolation. These are offline reader costs, not combined native mod-work or whole-frame timings. Combined native **mod-work p95 <5 ms**, p50/p95/max and all spikes **>=30 ms flagged**, with whole-frame timings/stalls separate, remain pending in the normal combined 6 Nov session.

**Separate diagnostic flags:** instrumented cupcake differential first call **31.0202 ms (>=30 ms)**, explicitly flagged; donut differential first call 29.3038 ms. Both include two readers, JSON serialization/comparison and JIT, and are excluded from single-reader benchmarks. Unchanged D1 20,000-place aggregate cold build **40.7823 ms (>=30 ms)** and C1 1,024-business synchronous cold p95 **7.0623 ms (>=5 ms)** remain flagged offline aggregate-reader conditions, not E3 samples or native frames; bounded scheduling requirements stay unchanged. Review of the touched reader found no additional cheap safe measurable win beyond its existing once-bound metadata, bounded on-demand scans, no success-path diagnostic strings and removed redundant set. No new performance win is claimed or added to the roadmap log. All four parked hotspots remain untouched.

[Native checklist and validation](../checks/e3-salad.md). No new performance win is claimed by this integration.

## Base sync - 1 November 2026

The merged C2a tree retains the approved E3 reader implementation and no normal-play caller. Final Release conditional-double measurements:

| Reader | Cold ms / allocated B (one sample) | 2,000 entries warm p50 / p95 / max ms; B per call |
| --- | --- | --- |
| Donut | 1.7620 / 1,040 | 0.0832 / 0.1261 / 1.4673; 49,104 B |
| Cupcake | 1.7166 / 1,040 | 0.0817 / 0.1360 / 1.9460; 49,104 B |
| Salad | 1.7224 / 1,040 | 0.0816 / 0.1074 / 1.3732; 49,104 B |

Small reads retain **200 B/read**. **Zero >=30 ms single-reader samples**, including cold, in either sync run. The same method applies: 30 warmups, 1,001 individually timed valid reads, fixture construction excluded, cold including JIT/metadata/descriptors. No causal timing improvement or new performance win is claimed. Ordinary play has zero reader work by source isolation.

**Explicit offline flags:** final D1 20,000-place aggregate cold **32.8855 ms (>=30 ms)**; awareness dense 20,000-alternative synchronous complete build **32.3121 ms (one >=30 ms sample)**. Awareness sparse 1,024/20,000 complete-build p95 is **7.6906 / 23.7681 ms**. Final optional-stop 1,000-venue model-1 batch p95 is **6.2435 / 7.4516 ms** for reuse/rebind; existing untuned 957-resident 25/100% p95 is **8.3535 / 9.5997 ms**, tuned 100% p95 **9.7017 ms**; all optional-stop arms have zero >=30 ms samples. First-base flags: D1 cold **36.6597 ms (>=30 ms)**, C1 1,024-business cold p95 **6.3751 ms**, instrumented donut/cupcake-baseline differential's donut first call **40.3132 ms (>=30 ms)**. The latter includes two readers, JSON serialization/comparison and JIT, and is excluded from reader benchmarks. Final instrumented donut/cupcake first calls are **26.7543 / 26.0189 ms**; original/shared donut differential cold is **25.8548 ms**.

[Both runs' complete measurements, threshold flags, conditions and log hashes](../reviews/2026-11-01-e3-salad-sync-base-results.json). These existing aggregate/stress/comparison costs do not measure native frames or bounded production continuation cost. Native combined mod-work p95 <5 ms, every >=30 ms spike flagged, allocations/GC, separate whole-frame timings and the four parked hotspots retain their original requirements. Native proof remains pending in the combined session; no special rig run or new optimization was performed.
