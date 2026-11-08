Status: implemented (offline); not verified by rig

Refs #93. Base `6fdbc198`; writer 33. Formula and default-strength rationale: [price-anchor decision](../decisions/2026-11-08-price-anchor.md). These are headless macOS arm64 / .NET 10 Release measurements, sequential foreground runs. No game, rig, ModsLocal or bridge was used.

## Matched short demand check

`F6iPriceAnchorTests.ShortWholeMenuMarkupReducesQuantity` reuses the ordinary F6 integrated harness, with seed 3, 128 residents + 128 independent visitors, three days, one baseline day then two days of +50% whole-menu prices versus a matched control. The menu stays $3/$5/$8 before markup. Own capacity is an identical fixture input of 6,000 meals/hour in both arms, isolating demand from the stopped F6h fixture's peak supply saturation. It is not a production default change. All wages, person wallets, normal C9 prices, household funding, memory, rival limits and journal remain active. The second own site stays closed. The two cases differ only in whether the existing rival is listed.

Before-values run the new checks against the unmodified base formula; after-values use the anchored rule. Each row totals the two affected days, excluding the baseline day. This short one-seed experiment is outside the complete F6h protocol.

| Policy / rival | Control own meals | +50% own meals | Quantity change | Arc elasticity | Control → markup home meals |
| --- | ---: | ---: | ---: | ---: | --- |
| Before / absent (monopoly) | 521 | 519 | −0.384% | −0.009615 | 359 → 361 |
| After / absent (monopoly) | 531 | 374 | **−29.567%** | −0.867403 | 349 → 506 |
| Before / present | 382 | 382 | 0.000% | 0.000000 | 247 → 247 |
| After / present | 386 | 269 | **−30.311%** | −0.893130 | 243 → 343 |

Arc elasticity is `(q1-q0)/((q1+q0)/2) / 0.4`, using the +50% menu-price arc. Baseline own quantities match within every control/markup pair. Each run passes **768 daily money-owner conservation checks**, exact double receipt and outside-debit reconciliation, zero duplicate orders and zero HistoricalClockMisses. Across four runs per policy that is 3,072 owner-day checks. No increased C9 charge or altered wallet fixture produces this result.

The policy-only monopoly experiment (4,096 stable draws, neutral traits, $10,000 allowance) changes $5 → $7.50 shop selections from **2,036 → 2,036 before**, to **2,036 → 1,098 after** with funded home cooking. Its weight ratio changes from **0.999750187** to **0.367879441**. The five sensitivity-band weight ratios after are **0.606531, 0.472367, 0.367879, 0.286505, 0.223130**. Affordable tight-wallet weights equal cap-wallet weights; the hard gates are checked separately.

## Component timing and tool cost

`PC_F6I_MEASURE=1` enables timing in the same tests. The policy microcheck warms 5,000 calls, then measures 2,000 batches of 32 offer weights (eight shortlisted shops × four default live offers; the pure policy's hard bound is 64). Samples include timer overhead. It measures the actual old/new `Weight` implementation, not a repeated formula.

| 32 offer weights | p95 ms | Max ms | ≥30 ms | Allocated bytes across 64,000 calls |
| --- | ---: | ---: | ---: | ---: |
| Before | 0.006000 | 4.768000 | 0 | 0 |
| After | 0.005800 | 6.396200 | 0 | 0 |

The tiny p95 difference is noise; no speedup is claimed. The moved affordability guard skips weight/hash work for ineligible offers, without changing eligibility. No separate timing win is claimed for that safe change.

Bounded advancement timings include Core scheduler and market/route doubles in each `AdvanceFrameSlice(..., 1)`, excluding fixture construction, daily all-owner reconciliation, final serialization/hashing, output and sample-array insertion. These are detached bounded units, not a Unity mod-work union or whole-frame measurement.

| Policy / rival / arm | Samples | p95 / max ms | ≥30 ms | Whole tool seconds | Whole tool allocated bytes |
| --- | ---: | --- | ---: | ---: | ---: |
| Before / absent / control | 17,192 | 0.027500 / 5.938200 | 0 | 0.188617 | 47,197,000 |
| Before / absent / markup | 17,182 | 0.020000 / 1.705500 | 0 | 0.167527 | 47,106,864 |
| After / absent / control | 17,307 | 0.027900 / 5.617900 | 0 | 0.196611 | 47,370,200 |
| After / absent / markup | 16,565 | 0.021700 / 4.259100 | 0 | 0.169972 | 46,725,640 |
| Before / present / control | 20,414 | 0.039800 / 19.972400 | 0 | 0.443964 | 78,126,424 |
| Before / present / markup | 20,414 | 0.039700 / 4.183700 | 0 | 0.227477 | 72,387,024 |
| After / present / control | 20,488 | 0.041500 / 21.334300 | 0 | 0.494094 | 77,929,328 |
| After / present / markup | 20,002 | 0.041300 / 2.993000 | 0 | 0.237302 | 71,464,416 |

Tool totals include fixture construction, diagnostic sampling, doubles, daily accounting and final save/hash. They are not steady-state allocation claims. The rival-present control runs first and includes colder initialization. Changed choices also change event counts, so these are not isolated price-term speed comparisons. All measured units remain below 5 ms at p95; no sample reaches 30 ms. Native unified mod-work p95 <5 ms and whole-frame time remain separate rig measurements. No save timing or autosave setting is involved in the headless windows.

## Validation and reproduction

First check `pgrep -f testhost`; wait for any running testhost, and run dotnet commands sequentially in the foreground with the task's Homebrew environment:

```sh
env PATH="/opt/homebrew/bin:$PATH" DOTNET_ROOT="$(/opt/homebrew/bin/brew --prefix dotnet)/libexec" PC_F6I_MEASURE=1 dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F6iPriceAnchorTests --logger 'console;verbosity=detailed'
```

The initial base run gives **10 failed / 5 passed** (the price-response assertions turn red; hard affordability and allocation checks already pass). The implemented rule gives **15 passed**, zero failures, in **1.87 s** total test time. The short integrated cases run normally in about one second combined; timing is opt-in. Assertions require at least a 20% quantity reduction per short case; that is an F6i regression bound, not a new F6h acceptance threshold. The required build passed, DLL SHA256 `7063a764553000791e5b39d814f3306b69c0781996aaa556811dd65755ea10f0`.

The final required full foreground unit suite passes **3,886 tests**, zero failed/skipped, in **4 min 38 s**, including the two unchanged writer-33 golden pins. The 33 focused checks of affected expectations, new policy/harness cases and those golden pins also pass. [Checks](../checks/f6i.md) lists every older simulation-generated expectation updated because of changed meals/balances. Documentation links and `git diff --check` pass. No full F6h sweep or tools/*-regression suite is run. The historical five-seed whole-menu dominance at price weights 1 and 3 stays recorded in [F6h calibration](f6h-calibration.md); this slice does not relabel it as passing acceptance.
