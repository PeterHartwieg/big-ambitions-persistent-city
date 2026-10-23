# Public-gate lookup performance — 21 October 2026

Implements finding 2 from the [frame-cost review](2026-10-20-frame-cost-review.md). Finding 1, unnecessary periodic road-snapshot rebuilds, was already implemented in the current source. Gate scanning is the highest-ranked remaining opportunity in that review: it sits inside road A*, route validation, walking access checks and occupied-road closure checks. Its share of current native frame time has not been isolated.

## Change

`Runtime/RoadRoutes.cs` builds a 50-metre X/Z cell index for each immutable `PublicRoadAccess` snapshot. Short segment queries visit only intersecting cells. Every candidate still passes the existing exact three-dimensional, boundary-inclusive slab intersection. The index changes candidate lookup only; it does not cache routes, skip native road-contract checks, or change route costs, population policy, timing or saved state.

- Both ends of each gate's and query's cell range are included, including negative coordinates and exact cell boundaries.
- Up to eight gates use the original linear scan and do not build the index.
- A gate occupying more than 64 cells, or with unindexable coordinates, stays in a fallback list checked on every indexed query. It cannot silently disappear.
- Long queries fall back to the original full scan before exceeding 64 cells or the gate count. Cell arithmetic is checked before integer conversion and iteration.
- Overlapping boxes can appear in multiple cells. A candidate budget falls back before repeated candidate tests reach one full scan, bounding the total below twice the original gate count.
- The index belongs to its captured gate list. Changed gates create a new snapshot and index through the existing refresh path. An unchanged refresh captures the comparison boxes and door-identity records but does not build a replacement grid. Moving-leaf envelope retention and `NearGate` are unchanged.

The implementation adds no explicit per-query allocations or mutable query scratch; zero measured query allocation is verified on .NET 10 only, not Unity/Mono. Index storage is bounded by 64 bucket entries per indexed gate. This adds retained memory in exchange for fewer geometric tests.

## Offline evidence

- All **869 existing tests** pass before and after the change.
- The revised actual-Runtime harness passes **213,884 checks**, including differential gate decisions and 80 full road-search comparisons of feasibility, exact cost and chosen node sequences. The searches now explicitly assert that gates block edges; a separate fixture verifies a known detour, complete gate blockage and reopening, and a difference from the ungated route. Mid-scan budget fallback cases check blocked and clear answers individually. The benchmark adds 30,150 per-request/result/allocation assertions. Cases also include refreshes, leaf growth across cells, malformed/extreme inputs, unreachable routes and expansion limits. The oracle scans the captured boxes directly using the original predicate; it does not use the index.
- The existing actual-Runtime road handoff/sweep/gate harness passes. The road-speed and persistence harness passes all **602 assertions**.
- Full current-source packages compile against both the local Mac game and the retained Windows rig reference assemblies with C# 9 and warnings as errors. The revised builds are kept under `build/gate-access-review-20261021/validation-r2/`; the existing `build/PersistentCity` package is preserved.

Both revised builds produce the same DLL SHA-256: `3f87e7f927be074ca649f7e1403bf803ac146cb62c35528655bc36877be8f0a9`.

This is source, synthetic correctness and compilation evidence. Astra, Sol, Opus 5.5 and Grok 4.7 approve the final source after [two review rounds](2026-10-21-gate-access-panel.md); in-game acceptance remains pending.

## Synthetic timing

Command: `DOTNET_TieredCompilation=0 dotnet run --project tools/gate-access-regression/GateAccessRegression.csproj --configuration Release -- --benchmark`.

Each row is the median of five alternating original/current pairs, after warmup, for 200,000 identical queries on .NET 10 on this Mac. The reference is the harness's implementation of the original linear scan, not the original mod binary. Every individual request is compared before timing. No competing build or benchmark ran during measurement. Both versions returned identical results; the indexed query path allocated zero bytes in every measured run on .NET 10.

| Gates | Layout | Original scan | Current lookup | Speedup |
| ---: | --- | ---: | ---: | ---: |
| 8 | Spread | 9.489 ms | 10.017 ms | 0.95× |
| 32 | Spread | 39.067 ms | 17.583 ms | 2.22× |
| 32 | Clustered | 44.687 ms | 21.974 ms | 2.03× |
| 32 | Fully overlapping | 33.431 ms | 39.174 ms | 0.85× |
| 128 | Spread | 160.818 ms | 17.747 ms | 9.06× |
| 128 | Clustered | 147.072 ms | 45.328 ms | 3.24× |
| 512 | Spread | 692.590 ms | 21.137 ms | 32.77× |
| 512 | Clustered | 628.424 ms | 168.139 ms | 3.74× |
| 128 | Fully overlapping | 157.098 ms | 143.139 ms | 1.10× |
| 512 | Fully overlapping | 559.635 ms | 579.426 ms | 0.97× |

These counts and distributions are synthetic, not an inventory of native gates. This is not a universal speedup: the worst measured row, 32 fully overlapping gates, takes about 17% more query time (0.85× speedup). The deliberately overlapping case exercises the repeated-candidate fallback. Native route time, frame maxima, backlog and FPS gains remain unmeasured. The earlier approximately 60 ms route events are not claimed fixed.

## Delivery and remaining validation

The implementation and repeatable harness are in the local repository. Ignored `build/gate-access-20261021/` retains the initial implementation and baseline evidence; `build/gate-access-review-20261021/` retains the review rounds and revised source/build/benchmark evidence. Packages remain disabled by default and include the current development tree's existing stationary-citizen and frame-cost changes. At review completion, no game installation, live save, remote PC or RLO source had changed. Peter subsequently authorized [installation of the reviewed DLL in both PC profiles](2026-10-21-gate-access-pc-install.md); neither game was launched.

A native follow-up should compare matched route requests and frame/advance maxima on the same checkpoint, record the actual gate count and distribution, and check gate refreshes during physical movement. It must separate gate-query timing from navigation and native contract validation. Synthetic improvements alone do not establish the reduction in visible stutter.
