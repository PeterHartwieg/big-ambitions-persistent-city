# E3 cupcake performance evidence

**Reader timing and allocations:** Release .NET 10 native field doubles; separate fresh processes for original donut, shared donut and shared cupcake. Cold is one first call including JIT, metadata and descriptor initialization, not a percentile. Warm is 30 warmups plus 1,001 individually timed calls at each size; fixture construction excluded. Furniture includes the bag counter. No causal timing improvement is claimed.

| Reader | Cold ms / allocated B (one sample) | 2 entries warm p50 / p95 / max ms; B per call | 2,000 entries warm p50 / p95 / max ms; B per call |
| --- | --- | --- | --- |
| Donut before (`462a99d`) | 1.5581 / 968 | 0.0006 / 0.0009 / 0.0120; 200 B | 0.0988 / 0.1786 / 1.8302; 49,104 B |
| Donut shared (`ce612c1`) | 1.7038 / 1,016 | 0.0005 / 0.0006 / 0.0279; 200 B | 0.1006 / 0.1677 / 1.3946; 49,104 B |
| Cupcake shared (`ce612c1`) | 2.6141 / 1,016 | 0.0005 / 0.0005 / 0.0089; 200 B | 0.0959 / 0.1531 / 1.9054; 49,104 B |

Warm allocations are unchanged; the two pinned descriptors add **48 B once** to first-call allocation (968 -> 1,016 B). **Zero single-reader E3 benchmark samples >=30 ms**, including cold. Metadata is shared, bound only on explicit first use; there are no hooks, polls or gameplay callers. Thus zero ordinary-play per-frame reader reflection/scans/allocations, and zero cost when uncalled, by source isolation. These are offline reader-only numbers, not native combined mod-work or whole-frame measurements. Native **mod-work p95 <5 ms**, every spike **>=30 ms** flagged, and whole-frame timings/stalls separate remain pending in the normal combined 6 Nov session.

The instrumented differential output check also passes: original and shared donut outputs are **byte-identical JSON** on every original functional/timed case. Its first instrumented call is **32.0904 ms (>=30 ms)**, explicitly flagged: this includes both readers plus JSON serialization/comparison/JIT, is excluded from single-reader benchmarks and is not a native mod-work frame.

The unchanged regressions retain diagnostic flags: D1 20,000-place aggregate cold build **41.1507 ms (>=30 ms)**; C1 1,024-business synchronous cold p95 **7.0160 ms (>=5 ms)**. They are separate offline aggregate-reader conditions, not E3 samples or native mod-work frames; bounded scheduling requirements remain unchanged. The four parked hotspots are untouched. Review found no additional cheap safe optimization in the moved reader beyond its existing once-bound metadata, bounded on-demand scans, constant refusal strings and already removed identity set; no new measured win is claimed or added to the roadmap's Perf wins log.

[Native checklist and validation](../checks/e3-cupcake.md). No new performance win is claimed by the integration.
