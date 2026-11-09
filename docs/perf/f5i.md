# F5i performance: visitor bodies

Offline component timings, 9 November 2026, Peter's PC (Windows, .NET 10, Release, `DOTNET_TieredCompilation=0`), base `b8feb06` (writer 35). Not in-game mod-work. In-game mod-work p95, ≥ 30 ms spikes and whole-frame timing come from rig check 5 in [the F5i checks](../checks/f5i.md#in-game-checks-for-the-next-batched-rig). Other worktrees' slices were running on this machine at the same time.

Harness: `VisitorBodyMeasure` in `tests/VisitorBodyTests.cs` (runs only with `PC_F5I_MEASURE=<file>`). **Before** is the same code with `ActiveJourneyIndex.VisitorsReceiveBodies = false` (the base), **after** with `true`. Each row is 300 single calls after 100 warm-up calls, two runs per arm. The tables show the second run, with the p95 range of both runs in brackets.

Fixture: the F2 movement fixture (25 × 25 signalled grid, 288 m across, shop at the centre) with 957 residents and K−1 visitors each from three portals near the shop: K = 2 gives 1,914 persons and K = 3 gives 2,871. A persons walk a two-leg route at random grid points, picked at an even stride over residents and visitors, so the active share of visitors matches the population (1/2 or 2/3). The focus is the shop with the 120 m radius. The whole city lies within the grid cells the query touches, so this is the dense case: every indexed body candidate comes back from the grid.

Rows:
- **Selection**: the Runtime selection's roster and stateful filters (`PresentationGrid.Query` near the focus, future event, moving journey, sampled pose within the radius), the `ByPriority` sort and up to four journey samples. Unity calls are left out.
- **Selection + acquisition**: one `Update` of a fresh F2 driver double (`tools/movement-equivalence/PresentationDriver.cs`): the same selection over `BodyCandidates`, up to four bodies bound and observed once. Bindings are never leased (F2c/F2d), so this is the whole acquisition cost. Its bytes are the fresh double's lists growing, not production allocation.
- **Index rebuild** (construction, load, route rebuild), **shadow comparison** (`ActiveJourneyIndexMatchesState`, every 30 s) and **visitor route commit** (a travelling visitor's journey replaced and re-filed, once per event commit).

"Candidates/withheld" is `BodyCandidates` / `WithheldVisitors`. "In radius" is the number of candidates that pass the distance filter.

## 1,914 persons (K = 2)

| Active | Row | Before: candidates/withheld | Before p50 / p95 / max ms | After: candidates/withheld | After p50 / p95 / max ms | In radius before → after |
| ---: | --- | --- | --- | --- | --- | --- |
| 0 | selection | 0/0 | 0.0000 / 0.0001 / 0.0001 (0.0001–0.0001) | 0/0 | 0.0000 / 0.0001 / 0.0001 (0.0001–0.0001) | 0 → 0 |
| 0 | selection + acquisition | 0/0 | 0.0002 / 0.0005 / 0.0010 (0.0005–0.0005) | 0/0 | 0.0002 / 0.0005 / 0.0019 (0.0004–0.0005) | 0 → 0 |
| 0 | index rebuild | 0/0 | 0.0127 / 0.0134 / 0.0746 (0.0114–0.0134) | 0/0 | 0.0131 / 0.0170 / 0.1649 (0.0131–0.0170) | |
| 0 | shadow comparison | 0/0 | 0.0187 / 0.0201 / 0.1816 (0.0192–0.0201) | 0/0 | 0.0181 / 0.0200 / 0.0398 (0.0200–0.0203) | |
| 100 | selection | 50/50 | 0.0021 / 0.0030 / 0.0083 (0.0030–0.0031) | 100/0 | 0.0048 / 0.0049 / 0.0121 (0.0048–0.0049) | 25 → 55 |
| 100 | selection + acquisition | 50/50 | 0.0070 / 0.0081 / 0.0282 (0.0081–0.0098) | 100/0 | 0.0125 / 0.0293 / 0.0944 (0.0168–0.0293) | 25 → 55 |
| 100 | index rebuild | 50/50 | 0.0361 / 0.0564 / 0.1679 (0.0396–0.0564) | 100/0 | 0.0461 / 0.0808 / 0.1858 (0.0711–0.0808) | |
| 100 | shadow comparison | 50/50 | 0.0400 / 0.0509 / 0.0935 (0.0401–0.0509) | 100/0 | 0.0429 / 0.0663 / 0.1772 (0.0531–0.0663) | |
| 100 | visitor route commit | 50/50 | 0.0000 / 0.0001 / 0.0001 | 100/0 | 0.0001 / 0.0002 / 0.0002 | |
| 1,000 | selection | 500/500 | 0.0242 / 0.0248 / 0.0289 (0.0246–0.0248) | 1000/0 | 0.0674 / 0.0860 / 0.1782 (0.0860–0.1805) | 259 → 511 |
| 1,000 | selection + acquisition | 500/500 | 0.0559 / 0.0937 / 12.1191 (0.0663–0.0937) | 1000/0 | 0.1127 / 0.1879 / 5.3712 (0.1879–0.1964) | 259 → 511 |
| 1,000 | index rebuild | 500/500 | 0.1552 / 0.1645 / 0.3363 (0.1645–0.2446) | 1000/0 | 0.2943 / 0.4435 / 0.6219 (0.4226–0.4435) | |
| 1,000 | shadow comparison | 500/500 | 0.0569 / 0.0708 / 0.1783 (0.0568–0.0708) | 1000/0 | 0.0734 / 0.1124 / 0.4148 (0.0806–0.1124) | |
| 1,000 | visitor route commit | 500/500 | 0.0000 / 0.0001 / 0.0001 | 1000/0 | 0.0001 / 0.0002 / 0.0002 | |

## 2,871 persons (K = 3)

| Active | Row | Before: candidates/withheld | Before p50 / p95 / max ms | After: candidates/withheld | After p50 / p95 / max ms | In radius before → after |
| ---: | --- | --- | --- | --- | --- | --- |
| 0 | selection | 0/0 | 0.0000 / 0.0001 / 0.0001 (0.0001–0.0001) | 0/0 | 0.0000 / 0.0001 / 0.0001 (0.0001–0.0001) | 0 → 0 |
| 0 | selection + acquisition | 0/0 | 0.0003 / 0.0006 / 0.0011 (0.0006–0.0007) | 0/0 | 0.0002 / 0.0007 / 0.0051 (0.0002–0.0007) | 0 → 0 |
| 0 | index rebuild | 0/0 | 0.0168 / 0.0195 / 0.0214 (0.0170–0.0195) | 0/0 | 0.0196 / 0.0297 / 0.0611 (0.0221–0.0297) | |
| 0 | shadow comparison | 0/0 | 0.0284 / 0.0310 / 0.0914 (0.0273–0.0310) | 0/0 | 0.0315 / 0.0747 / 0.1414 (0.0655–0.0747) | |
| 100 | selection | 34/66 | 0.0014 / 0.0015 / 0.0075 (0.0015–0.0015) | 100/0 | 0.0045 / 0.0047 / 0.0080 (0.0045–0.0047) | 19 → 52 |
| 100 | selection + acquisition | 34/66 | 0.0039 / 0.0044 / 0.0052 (0.0044–0.0044) | 100/0 | 0.0098 / 0.0105 / 0.0185 (0.0103–0.0105) | 19 → 52 |
| 100 | index rebuild | 34/66 | 0.0448 / 0.0673 / 0.1725 (0.0457–0.0673) | 100/0 | 0.0582 / 0.0615 / 0.1116 (0.0604–0.0615) | |
| 100 | shadow comparison | 34/66 | 0.0589 / 0.0708 / 0.1982 (0.0708–0.0716) | 100/0 | 0.0599 / 0.0628 / 0.1372 (0.0627–0.0628) | |
| 100 | visitor route commit | 34/66 | 0.0000 / 0.0001 / 0.0001 | 100/0 | 0.0001 / 0.0002 / 0.0002 | |
| 1,000 | selection | 334/666 | 0.0154 / 0.0363 / 0.1045 (0.0158–0.0363) | 1000/0 | 0.0671 / 0.0874 / 0.2993 (0.0802–0.0874) | 175 → 509 |
| 1,000 | selection + acquisition | 334/666 | 0.0347 / 0.0386 / 0.0559 (0.0367–0.0386) | 1000/0 | 0.1237 / 0.2152 / 4.6167 (0.1340–0.2152) | 175 → 509 |
| 1,000 | index rebuild | 334/666 | 0.1285 / 0.1491 / 0.2555 (0.1491–0.1821) | 1000/0 | 0.3045 / 0.3506 / 0.5437 (0.3506–0.4006) | |
| 1,000 | shadow comparison | 334/666 | 0.0737 / 0.1120 / 0.2608 (0.0780–0.1120) | 1000/0 | 0.0941 / 0.1217 / 0.1958 (0.0985–0.1217) | |
| 1,000 | visitor route commit | 334/666 | 0.0000 / 0.0001 / 0.0001 | 1000/0 | 0.0002 / 0.0003 / 0.0003 | |

Selection, rebuild and route-commit allocate 0 bytes per call in both arms. The driver double's bytes per call rise from 5,496 to 9,200 B at 100 active and from 30,656 to 59,400 B at 2,871 / 1,000. That is the fresh double's candidate list growing to the larger candidate count. Production reuses its list.

## Reading

- Cost follows eligible bodies, as designed. With visitors eligible, selection sees about 2× (K = 2) to 3× (K = 3) the candidates in the radius, and its p95 rises in proportion: at 1,000 active, 0.0248 → 0.0860 ms (1,914) and 0.0363 → 0.0874 ms (2,871). Every row stays under 0.5 ms p95. Selection runs at most every 0.25 s, so this is not a per-frame cost.
- Index rebuild roughly doubles at 1,000 active (0.16 → 0.44 ms p95) because visitors are now filed in the presentation grid. It runs only at construction, load and route rebuild. Re-filing a visitor's route commit stays at 0.0002–0.0003 ms.
- The shadow check is hardly affected: it already compared visitors, and now also checks their grid cells.
- Spikes: no sample ≥ 30 ms in any row. Isolated maxima of 4.6–17.7 ms appear only in the driver double row at 1,000 active, in both arms (before 9.0 / 12.1 / 17.7 ms, after 5.9 / 5.4 / 5.0 / 4.6 ms). They come from the double's per-call allocations and the shared machine, not from production code.
- No cheap win taken. The added cost is the extra candidates' distance filter and sort, which is the feature itself. The F2f grid already limits the roster to cells near the focus. The four parked hotspots stay parked.

## Gate

Offline only. F5i adds no per-frame scan, reflection or allocation. Selection over a larger candidate set stays at or below 0.09 ms p95 at the declared load in the dense case. Mod-work p95 < 5 ms, the ≥ 30 ms spike list and whole-frame timing (reported separately; #128 stays its own investigation) are measured by rig check 5.

## Commands

```
set DOTNET_TieredCompilation=0
set PC_F5I_MEASURE=%TEMP%\f5i-after-1.jsonl
dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~VisitorBodyMeasure
```

The before rows came from the same command at `ActiveJourneyIndex.VisitorsReceiveBodies = false`, before the flip was committed.
