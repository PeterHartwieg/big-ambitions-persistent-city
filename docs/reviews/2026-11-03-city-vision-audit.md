# Persistent City architecture and city simulation audit

3 November 2026. Audited source: [`00506edf14bdfc35262ed5c54cdcd2b5032ce30c`](https://github.com/PeterHartwieg/big-ambitions-persistent-city/tree/00506edf14bdfc35262ed5c54cdcd2b5032ce30c). Prepared for Peter Hartwieg.

Persistent City has a credible foundation for persistent people, traffic and food demand. Reaching a realistic full-city simulation with meaningful gameplay at 60+ FPS requires changes to simulation ownership, scheduling and economic integration. Keep C#, preserve the tested identity, traffic and accounting foundations, and revise the architecture and feature order in stages.

The largest risk is that processing performance can affect city outcomes. Simulation backlog can cancel purchases, and visible bodies take control of logical journey progress. Those problems deserve priority over additional products, preference dimensions and rendering features.

The review evaluates the code against Peter's requested city vision, independently of previous scope decisions. The recommendations below do not change runtime behavior or adopt a new implementation plan.

## Evidence and scope

The audit covers Core, Runtime, Compatibility, Persistence, tests, build tooling, roadmap and retained performance evidence. Small implementation defects are outside its focus. Severity describes impact on the requested city vision, rather than a security classification or a demonstrated failure in every current workload.

- The production build passed against the locally installed game assemblies using `python3 tools/build.py --game-managed <local Managed directory>`.
- `dotnet test tests/PersistentCity.Tests.csproj -c Release --no-restore --verbosity minimal` passed **2,783 tests**, with zero failures and zero skips, in approximately 92 seconds.
- A separate synthetic benchmark measured the current budget-posting operation. Its source, method and observed results are retained with this report.
- Native performance numbers are historical, with their original workload and measurement limits. No new game session, save-specific profile, ordinary-save deployment or release occurred during the audit.

Offline tests establish valuable semantic properties. They do not establish native frame rate, Mono allocation behavior, compatibility of every game hook or gameplay quality.

## Foundations worth preserving

Stable identities, deterministic random draws, interrupted-journey handling, explicit business ownership and cautious transaction reconciliation are useful foundations. The sparse traffic kernel is especially appropriate for a city simulation: finite road capacity, FIFO queues, spillback and scheduled events provide meaningful behavior without ticking every car everywhere.

The tests contain substantial interruption, conservation, identity and ordering coverage. A complete rewrite would discard that knowledge. The strongest case for change concerns how the existing systems connect and execute.

## GitHub follow up

Each issue contains source evidence, scope qualifications and acceptance criteria. These track the architectural findings; they do not treat a proposed implementation as already selected.

| Finding | Priority for the city vision | Issue |
| --- | --- | --- |
| F1 Accounting and simulation lag | Critical | [#88](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/88) |
| F2 Movement authority across detail levels | High | [#89](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/89) |
| F3 Bounded work and route planning | High | [#90](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/90) |
| F4 Integrated frame rate and throughput gate | High | [#91](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/91) |
| F5 Independent logical population | High | [#92](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/92) |
| F6 Connected economic feedback | High | [#93](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/93) |
| F7 Save scaling and coherent snapshots | High at larger scale | [#94](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/94) |
| F8 Dependency and native capability contracts | Medium to high | [#95](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/95) |
| F9 Integrated acceptance workflow | Medium to high | [#96](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/96) |

## Findings

### F1 Simulation backlog can change economic outcomes

**Severity: Critical. Evidence: observed source behavior; the frequency under a particular native workload remains to be measured.**

When simulation time falls behind the native accounting hour, an active meal is explicitly marked missed because native inventory cannot safely accept a historical purchase. This protects accounting, but insufficient simulation throughput can consequently reduce business demand. See [FoodCommerce clock reconciliation](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Core/FoodCommerce.cs#L328-L334).

Ordinary advancement is constrained by rendered frames, including a limit of sixteen citizen events per frame. Lower frame rate therefore also lowers the maximum rate at which those events can begin. Additional clock backpressure exists for economy skips; ordinary ticking principally applies the configured calendar multiplier. See [AdvancePopulation](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Runtime/PersistentCityMod.cs#L391-L444) and [NativeCityClock](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Runtime/NativeCityClock.cs#L62-L72).

A realistic city should not produce worse commercial outcomes merely because the computer struggled to process it. Smooth rendering while the logical city falls behind is also an unsuccessful performance result.

Establish a shared simulation/accounting boundary. Transactions must commit against the appropriate world epoch, with controlled backpressure before that epoch closes. Preserve the existing protection against retroactive or duplicate sales. Measure throughput and workload-caused missed transactions alongside FPS; do not mask insufficient capacity by silently accumulating backlog or slowing the calendar beyond the declared gameplay pace.

Acceptance should compare the same logical day under different frame cadences and processing budgets, using identical timestamped external inputs. Any intentional overload behavior must be explicit, bounded and visible, with no unreported demand loss.

### F2 Presentation detail changes journey authority

**Severity: High. Evidence: observed authority transfer; the size of camera-only economic divergence is unmeasured.**

Visible bodies acquire leases that freeze ordinary journey progression until physical progress is reported. Traffic vehicles switch into physical control, allowing a physically delayed head vehicle to retain road occupancy. See [physical journey acquisition](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Core/PhysicalJourneys.cs#L59-L87) and [physical traffic control](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Core/TrafficKernel.cs#L166-L198).

This correctly avoids showing somebody blocked while simultaneously admitting them at their destination. However, camera-dependent body acquisition selects which citizens encounter native physical movement constraints. Identity continuity alone does not establish outcome independence across detail levels.

Define one authoritative movement contract across distances. Native bodies should follow its permissions, while actual player interactions can feed persistent events back into the world model. Signals, queues, capacity and access restrictions that materially affect arrival should have corresponding logical behavior outside the camera.

Exact equivalence for every physical detail may be impractical. If so, define acceptable divergence and test it. Camera changes without player interaction should not materially change missed shifts, completed meals or business revenue. Tests must include detail transitions and save/reload, not only retention of identity and measured position.

### F3 Scheduler budgets do not bound the largest operations

**Severity: High. Evidence: observed control flow and a synthetic component measurement.**

The two-millisecond advancement budget is checked between units. Several units still contain whole workloads:

- [StepBudget](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Core/FoodFinances.cs#L37-L57) posts every budget due at the same minute in one call.
- [Campaign observation](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Runtime/NativeCampaignAwareness.cs#L59-L63) drains repeated sixteen-item steps synchronously at the native boundary.
- [Route restart fallback](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Core/CitySimulator.cs#L275-L303) eventually runs planning inline with an unlimited budget.

One pending citizen route also holds the simulation clock and subsequent logical events until its answer completes. Resuming planning across frames protects many frame intervals, but does not remove this global dependency. See [pending route semantics](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Core/CitySimulator.cs#L20-L23).

The separate audit benchmark measured the current `StepBudget` implementation through reflection, using two consumers per resident:

| Residents | Consumers | Median call ms | Maximum call ms | Median allocated bytes |
| ---: | ---: | ---: | ---: | ---: |
| 957 | 1,914 | 2.261 | 4.117 | 91,896 |
| 5,000 | 10,000 | 13.729 | 15.741 | 480,024 |
| 50,000 | 100,000 | 54.212 | 65.826 | 4,800,024 |

These are seven measured warm calls per size after one discarded call, on .NET 10/macOS arm64. Each call posts the next day's budgets. Timing excludes construction and the subsequent completeness assertion, and includes reflection invocation. The minimal fixture performs no Unity, market or route work and is not a native or whole-frame benchmark. Sizes ran in increasing order in one process; tiered compilation, runtime warmup and host noise limit cross-size scaling comparisons. The result demonstrates an indivisible operation with substantial measured cost, not a measured native FPS failure.

Make bulk work resumable while retaining logical ordering. All midnight postings can remain logically before meals without executing in one native frame. Prepare routes ahead of deadlines against versioned snapshots and commit results deterministically. A restart policy must make progress without falling back to unbounded synchronous work. Native queries that cannot be interrupted require explicit admission limits and an observed tail-cost budget.

### F4 The performance gate does not establish the requested experience

**Severity: High. Evidence: historical native measurements and the current acceptance definition.**

The [30 October Windows report](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/1273abd9a8ec23017deb12cbfe181f9f891b2682/docs/reviews/perf/2026-10-30-performance-review.md) contains encouraging dependency-only results:

| Condition | Frame wall p95 ms | Mod work p95 ms | Maximum frame wall interval ms |
| --- | ---: | ---: | ---: |
| Morning | 14.005 | 1.259 | 25.766 |
| Noon | 13.185 | 0.411 | 36.636 |
| Evening | 13.807 | 0.815 | 174.875 |

The campaign used 957 residents, a stationary camera, 1920x1080 Low graphics, one-third calendar pace, a target of 90 and unsaturated caps of 48 pedestrians/24 cars. Endpoint observations showed zero to three cars; these were sparse observations, not maxima. Allocation attribution was unavailable, some fixed-step hooks were outside the primary mod-work union, and the report expressly does not establish presented-frame FPS or a matched whole-frame improvement. Its source predates the audited head.

The current [roadmap gate](../roadmap.md#performance-gate-peter-2026-10-30) focuses on mod-work p95 below five milliseconds and flags spikes of thirty milliseconds or more. That cannot independently establish a total frame within the 16.67 ms budget for 60 FPS. It also permits individual mod calls that exceed that entire frame budget.

Replace the headline gate with a combined contract covering presented-frame pacing, complete mod cost, native/GPU pressure, simulation lag and recovery. Declare supported hardware, settings, logical population, business load, actual body counts and calendar pace. Test commute, meal and accounting peaks, moving cameras, dense streets and ordinary management UI. Use a matched dependency-only reference and distinguish component CPU savings from whole-frame improvement.

Keep clean gameplay timing windows autosave-off under existing rig rules. Startup, reload, time skips and saving need separate latency criteria; a save-specific campaign remains a separately requested run. Unexplained whole-frame stalls should remain unresolved evidence until attributed, rather than being assigned to this mod or dismissed by its narrow timing union.

### F5 Extra consumers are not independent logical people

**Severity: High. Evidence: observed population and representation model.**

The default representation factor creates two consumers per resident. Extra consumers own wallets, preferences and meal excursions, but share an anchor resident's underlying routine and opportunities. They are explicitly not citizen, body, car or traffic owners. See [CityBalance](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Core/CityBalance.cs#L6-L12) and [FoodRepresentation](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Core/FoodRepresentation.cs#L28-L62).

This is a practical demand multiplier. It becomes a structural obstacle when consumers need independent jobs, households, transport or daily lives: extending it risks creating a second citizen simulator.

Other population assumptions are provisional. Residential capacity follows exposed interiors and occupancy bands, business workplace allocation is a constant sixteen, work windows largely follow persona templates, and employment changes use daily probabilities. See [GamePlaces](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Runtime/GamePlaces.cs#L15-L16), [HousingPolicy](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Core/HousingPolicy.cs#L10-L32), [work windows](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Core/DailyRoutine.cs#L148-L152) and [employment checks](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Core/ResidentLifecycle.cs#L46-L64).

Consolidate consumers into independent logical residents or explicitly modeled visitors. Most require no native body. Connect people through household, workplace and business relationships rather than duplicated consumer slots. Migrate existing identities, balances and outstanding obligations deliberately.

Choose a population target from a defensible interpretation of the game world. Neither 957 nor 50,000 is inherently realistic. Consistency among homes, jobs, services, trips and spending matters more than an arbitrary count or the codec's ceiling.

### F6 Economic detail exceeds economic connectedness

**Severity: High. Evidence: observed economic boundaries; the assessment of gameplay depth is architectural judgment.**

Food preferences, memory, awareness, weather and probabilistic choice have substantial detail. Less connects attendance, employment, household resources, services and business performance. Employment status affects outside income, but actual attendance does not determine the external wage. Ordinary shopping and leisure largely create activity without corresponding economic exchanges. See [HouseholdFinance](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Core/HouseholdFinance.cs#L62-L113) and the [population description](../../README.md).

Rival food uses advertised prices/hours, unknown stock and assumed service time, whereas owned shops face live stock and staffing constraints. See [rival offer capture](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Core/RivalFood.cs#L75-L86). The resulting competitive asymmetry needs calibration, even if native rival finances remain read-only.

Prioritize causal loops: employment and services influence resources and opportunities; resources, needs and schedules influence purchases and travel; shortages and accessibility influence businesses; experiences influence subsequent behavior. External income and economic outflows can remain legitimate abstractions without simulating every upstream dollar.

Prove gameplay depth with controlled interventions in price, staffing, opening hours, shortages, employment and access. Measure demand response, recovery, market share and outcome stability over multiple weeks. Give players enough feedback to predict, observe and respond. Additional coefficients and hidden statistics should follow those demonstrated loops.

Native rival accounts need not be written to model bounded competing availability. Any such model must be labeled as mod-owned estimates, rather than claims about inaccessible native stock or finances.

### F7 Persistence needs a different scaling strategy

**Severity: High at larger scale. Evidence: synchronous write path and retained native/offline measurements.**

Saving performs full validation and JSON serialization synchronously. Runtime state and wire state are closely connected, including historical records and committed journey structures. See [SaveCodec.WriteForSchema](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Persistence/SaveCodec.cs#L56-L69) and [SaveSession.WriteTo](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Persistence/SaveSession.cs#L75-L102).

The historical Windows report measured two individual serialization calls at approximately 190 and 333 ms, outside gameplay timing windows. The newer [E5d capacity evidence](../perf/e5d-name-identity-save.md) includes a saturated 50,000-resident/factor-two fixture of 162,346,836 JSON bytes and 326.560 MiB retained decoded memory, with offline maximum write/read times of 2,081.7804/5,439.7251 ms across three measured samples. These are synthetic stress histories on .NET 10, not typical saves or native latency forecasts.

The 256 MiB input ceiling is a compatibility limit, not a desirable operating budget. Historical codecs also account for considerable maintenance surface; preserving every development writer inside production should not dictate the future runtime model.

Separate runtime storage from the save representation. Consider compact internal references, shared immutable route data, bounded histories and immutable snapshots. Encode away from the main thread only where the native save contract allows it. Native finances and mod state must represent the same save epoch; an older background snapshot or concurrent serialization of mutable live state is not an acceptable shortcut.

Preserve strict readers, original payloads and exact money/identity migration. Storage redesign should begin with measured typical and stressed bytes, allocation, retained memory and pause targets.

### F8 Dependencies and native patches need capability boundaries

**Severity: Medium to high. Evidence: observed dependency checks and patch surfaces; decoupling is a proposed design investigation.**

RLO supplies donor identities and appearances, and the running city periodically requires that catalogue to remain available and match the preserved source. An unavailable or changed donor source can stop the city. See [RloReadResult.MatchesPopulation](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Compatibility/RloIdentityAdapter.cs#L30-L46) and the [periodic check](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Runtime/PersistentCityMod.cs#L253-L263).

Make Persistent City's logical population independently authoritative in the long-term design. Assess RLO as an optional identity/appearance provider only after proving a replacement appearance path and a migration that retains identities. Removing the dependency without those proofs would be premature. Keep RLO access behind Compatibility and do not modify its source.

Retain native transaction adapters and cautious refusals. Express business support through capabilities such as direct stock, equipment, recipes, service and checkout semantics. Loaded-catalogue discovery is a stronger direction than accumulating isolated product probes, but metadata cannot establish transactional support. Each materially different transaction mechanism still needs native evidence.

Inventory Harmony hooks by owned behavior, native contract, conflicting behavior and fallback. Physical movement and inspector input have substantial native surfaces; see [PhysicalTrafficHooks](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Runtime/PhysicalTrafficHooks.cs#L30-L81) and [inspector input guards](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Runtime/ResidentInspector.cs#L63-L108). A capability must report whether it is active; an inactive optimization or fail-closed adapter cannot establish behavioral compatibility.

### F9 Validation needs an integrated city acceptance path

**Severity: Medium to high. Evidence: source structure and validation tooling.**

Core and Runtime are usefully separated, but conceptual boundaries remain inside large partial classes. At the audited source, CitySimulator spans eleven partial files and approximately 2,800 lines; FoodCommerce spans nine and approximately 2,100. Publicly mutable model objects serve simulation, persistence and inspection. File splitting alone will not resolve ownership or execution coupling.

The tests provide valuable coverage, while acceptance is fragmented across unit tests, extracted source harnesses, native doubles, historical comparisons and platform-specific runners. A central [D3a runner](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/tools/run-d3a-checks.py#L14-L24) hardcodes Windows paths and an external PowerShell coordination script.

Create one portable offline acceptance entry point and a small integrated city scenario shared by correctness and performance work. Compile production against supported game assembly fingerprints. Keep separate results for semantic correctness, native compatibility, performance and gameplay acceptance. Source-preservation checks remain useful, but cannot substitute for behavior under changing native inputs.

Batching some game validation is reasonable. Changes to native assumptions should receive timely integrated evidence before many dependent systems accumulate. Record the exact source, artifact, fixture, settings and active capabilities for every result.

## Recommended architecture

| Boundary | Responsibility |
| --- | --- |
| World model | Independent persistent people, households, businesses, services and transport with no native object references |
| Simulation coordinator | Deterministic event order, bounded continuations, accounting epochs and explicit backlog policy |
| Navigation | Versioned geometry/cost snapshots, reusable routes, advance planning and bounded native queries |
| Presentation | Spatial selection of nearby bodies, animation, interpolation and explicit detail transitions |
| Native economy gateway | Typed commands, exclusive ownership, current native validation and durable reconciliation |
| Persistence | Compact wire representation and coherent immutable snapshots |
| Observation | Read-only player feedback and complete timing, lag and outcome measurements |

Retain a single authoritative simulation writer initially. Parallel calculations should read immutable inputs and return results for deterministic commitment. Multithreading every subsystem would introduce ordering problems without first establishing a measured need.

Replace repeated city-wide presentation scans with maintained spatial or active-journey indexes when scaling warrants it. The current [selector](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/00506edf14bdfc35262ed5c54cdcd2b5032ce30c/Runtime/PhysicalPopulation.cs#L186-L222) scans eligible residents and sorts nearby candidates every quarter-second when capacity is available. Keep body caps and presentation quality adjustable independently of identity and economic semantics.

Retain verified navigation connectivity and public-access rules. Reduce expensive native queries through shared geometry, hierarchical routes and revision-aware invalidation. Unity documents that [NavMesh.CalculatePath](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AI.NavMesh.CalculatePath.html) is synchronous and can affect frame rate. A language change does not remove that call's cost.

## Language and rewrite assessment

| Option | Assessment |
| --- | --- |
| C# with clearer ownership and better data layout | Preferred |
| Selectively replace scheduler, consumer representation and save paths | Justified investigation from the findings |
| Rewrite everything in Rust or C++ | Insufficient evidence of benefit relative to integration and migration cost |
| ECS-style storage | Useful where profiles show scan, allocation or locality costs; no framework required initially |
| Unity Entities or Burst throughout | Requires proof of compatibility and deployment feasibility in the actual game |

Burst excludes managed objects and reference types from its supported subset, so the present object-heavy model would need restructuring. It is not a compiler switch for the existing simulation. See [Burst type support](https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/csharp-type-support.html).

A staged architectural refactor with selective subsystem replacement has the strongest justification. Preserve identity contracts, traffic algorithms, financial invariants and native reconciliation tests throughout.

## Proposed milestone order

1. **Define the city contract.** Specify logical population, visitors, households, supported economic activity, outside boundaries and acceptable detail-dependent divergence. Declare the hardware and settings for the 60+ FPS target.
2. **Prove execution capacity.** Bound atomic operations, address global planning dependencies, coordinate accounting and measure throughput at intended calendar speeds. Require no unreported workload-caused demand loss.
3. **Consolidate population.** Replace anchored extra consumers with independent logical people. Connect homes, work, resources and needs without requiring additional native bodies.
4. **Unify movement across detail levels.** Preserve the traffic kernel, strengthen off-camera constraints, introduce spatial presentation indexes and test camera-only outcome differences.
5. **Prove a connected gameplay loop.** Demonstrate that location, prices, staffing, shortages, access and employment create understandable choices across multiple simulated weeks.
6. **Broaden businesses through capability adapters.** Expand economic coverage by transaction mechanism, with native evidence and explicit unsupported categories.
7. **Prove integrated scale and usability.** Test dense streets, moving cameras, UI use, reloads, fast-forward and long runs on the actual candidate. Assess saving in a separately specified campaign.

Defer further interior rendering, isolated product probes and preference dimensions until the first two milestones pass. That sequence directs effort toward coherent outcomes and responsive gameplay before further surface detail.

## Reproducing the budget measurement

The [fixture source](assets/2026-11-03-city-vision-audit/budget-posting-benchmark.cs.txt), [runner](assets/2026-11-03-city-vision-audit/run-budget-benchmark.py) and [recorded observations](assets/2026-11-03-city-vision-audit/budget-posting-results.json) preserve the audit's small component experiment. The runner builds in a temporary directory against the local Core project and does not launch the game or access saves.

From the repository root, run:

```sh
python3 docs/reviews/assets/2026-11-03-city-vision-audit/run-budget-benchmark.py
```

The recorded values belong to the audited source and the original audit process. Reruns depend on the checkout, host, JIT state and runtime. The fixture directly constructs minimal state and invokes the posting operation; it is not a complete validated city, a general scale harness or a new release gate.
