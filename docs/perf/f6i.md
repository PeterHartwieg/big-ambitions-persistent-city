Status: implemented (offline); not verified by rig

Refs #93. Base `6fdbc198`; writer 33. Formula and default-strength rationale: [price-anchor decision](../decisions/2026-11-08-price-anchor.md). The initial and first-review measurements use headless macOS arm64 / .NET 10 Release; the round-2 cadence checks below use the required default Debug configuration. All runs are sequential and foreground. No game, rig, ModsLocal or bridge was used.

## Review corrections Opus-r1-1 and Opus-r1-2

The implemented rule now uses a native-scale **$12.50** fixed reference and `min(4, (reference/price)^(2 * priceWeight * sensitivity))`. [The decision](../decisions/2026-11-08-price-anchor.md) links the retained native croissant, burger and salad price records and explains why no new setting or wire field is needed. The `Weight` overloads document allowance as an ignored compatibility argument; hard affordability still lives in selection and checkout.

### Native-price policy checks

Eight new cases cover all four recorded price levels at neutral (band 2) and high (band 4) price sensitivity. All other factors are neutral, with no trip in this table; funded cooking has weight 1. Markups are exactly +50% in the native money unit, including half-cents. Each row also tests ordinary five-minute travel/twenty-minute used-time costs and asserts the marked-up weight remains above the floor. Baseline own/home counts use the same 4,096 deterministic sole-shop versus funded-home draws, not a native market-share measurement.

| Price / sensitivity | Reviewed $5 rule baseline → markup weight | Corrected baseline → markup weight | Before baseline own/home | Corrected baseline → markup own/home |
| --- | --- | --- | --- | --- |
| $9.24 / 2 | 0.183416 → 0.028898 | 1.830105 → 0.813380 | 634 / 3462 | 2647 / 1449 → 1833 / 2263 |
| $10.25 / 2 | 0.122456 → 0.020000 | 1.487210 → 0.660982 | 460 / 3636 | 2445 / 1651 → 1622 / 2474 |
| $12.50 / 2 | 0.049787 → 0.020000 | 1.000000 → 0.444444 | 211 / 3885 | 2036 / 2060 → 1228 / 2868 |
| $16.69 / 2 | 0.020000 → 0.020000 | 0.560928 → 0.249301 | 94 / 4002 | 1447 / 2649 → 800 / 3296 |
| $9.24 / 4 | 0.078552 → 0.020000 | 2.475792 → 0.733568 | 309 / 3787 | 2929 / 1167 → 1733 / 2363 |
| $10.25 / 4 | 0.042852 → 0.020000 | 1.813671 → 0.537384 | 174 / 3922 | 2634 / 1462 → 1403 / 2693 |
| $12.50 / 4 | 0.020000 → 0.020000 | 1.000000 → 0.296296 | 94 / 4002 | 2036 / 2060 → 922 / 3174 |
| $16.69 / 4 | 0.020000 → 0.020000 | 0.420108 → 0.124476 | 94 / 4002 | 1186 / 2910 → 464 / 3632 |

At reference, each sensitivity has weight 1. All five bands retain **0.666667, 0.544331, 0.444444, 0.362887, 0.296296** of their weight at +50%, independent of price level before the existing bounds. Tight affordable wallets receive the same penalty; over-limit prices remain ineligible. A discount-reward ceiling of 4 prevents extremely cheap offers from saturating the final upper weight bound and erasing other signals; it is inactive for every native-price row above at default strength. The added deep-discount regression and the original $1 taste/fit/memory fixtures check that boundary.

### Matched short check at native price levels

The same three-day, seed-3, 128-resident + 128-visitor integrated fixture now reprices its three legacy products to **$9.24/$12.50/$16.69** at the first epoch in both arms and both own/rival menus. The third product is a salad-price-level proxy, not a claim that a native hot dog costs $16.69. The +50% intervention starts on day 2 ($16.69 rounds to $25.04 in this integer-cent fixture). The common 6,000/hour own-service input keeps supply non-binding. No ordinary price, wallet, wage, rival limit, product coefficient or production default is tuned. The following are affected-day own/home totals; baseline day own quantities match within each pair.

| Rule / rival | Own control → markup | Quantity change / arc elasticity | Home control → markup |
| --- | --- | --- | --- |
| Reviewed $5 exponential / absent | 197 → 159 | −19.289% / −0.533708 | 683 → 722 |
| Corrected power / absent | **548 → 395** | **−27.920% / −0.811241** | **332 → 485** |
| Reviewed $5 exponential / present | 145 → 109 | −24.828% / −0.708661 | 602 → 634 |
| Corrected power / present | **398 → 283** | **−28.894% / −0.844347** | **223 → 316** |

Each of the four runs per rule passes 768 daily owner checks, exact receipt/outside-debit reconciliation, zero duplicate orders and zero HistoricalClockMisses. This is a matched price-scale regression check, not a new F6h acceptance/calibration run. The baseline quantities themselves expose the reviewed rule's collapse at ordinary price levels. Actual native baseline shares are explicitly required by rig check 1.

### Matched timing

The same opt-in measurement method below is rerun with the $18.75 offer and the native-level short fixture. Per-policy timings are 2,000 batches × 32 weights after 5,000 warm calls. Full-tool allocation is separate from the policy's zero steady-state allocation.

| 32 weights | p95 / max ms | ≥30 ms | Allocated bytes |
| --- | --- | ---: | ---: |
| Reviewed exponential | 0.006100 / 6.402500 | 0 | 0 |
| Corrected bounded power | 0.006200 / 6.036000 | 0 | 0 |

| Rule / rival / arm | Slice samples | Slice p95 / max ms | ≥30 ms | Tool seconds | Tool allocated bytes |
| --- | ---: | --- | ---: | ---: | ---: |
| Before / absent / control | 14949 | 0.027000 / 6.088000 | 0 | 0.169629 | 42078480 |
| Before / absent / markup | 14770 | 0.026800 / 4.055300 | 0 | 0.176274 | 41653840 |
| After / absent / control | 17404 | 0.027600 / 1.728500 | 0 | 0.174997 | 47567984 |
| After / absent / markup | 16671 | 0.023100 / 3.697300 | 0 | 0.186247 | 45999840 |
| Before / present / control | 17979 | 0.039500 / 21.453600 | 0 | 0.436773 | 70633856 |
| Before / present / markup | 17826 | 0.040500 / 2.137600 | 0 | 0.209145 | 64875832 |
| After / present / control | 20600 | 0.040000 / 19.940000 | 0 | 0.458470 | 78343216 |
| After / present / markup | 20127 | 0.040200 / 1.996600 | 0 | 0.227500 | 72061152 |

No speedup is claimed; changed choices alter the work count. All measured p95 values remain <5 ms and every ≥30 ms count is zero. Native unified mod-work and whole-frame performance remain separate rig evidence.

### Round-2 cadence correction (Sol-r2-1; also Opus-r2-1)

The earlier menu replacement hid a valid failure. Both **$3.50/$7.25/$4.80** (the original/default menu) and **$9.24/$12.50/$16.69** now run as automated evening-rush cases over minutes **3990–4050**. Each keeps the existing exact save-hash comparison against uninterrupted advancement, rush population requirements, p95 <5 ms and unit/frame maximum <30 ms. No timestamp normalization, tolerance or hash rebaseline is used.

Before the scheduler correction, the restored original menu fails: framed hash `1A697AD9A2D107D597C4C9AA6B3DBEC0C15B2C11E8F5F2D00F6A7A7BDE24801D` differs from uninterrupted `BD5863E4F9A241A09812F18B3A62D03B55C847AEBB3384142AA689492CCD78B7`; the native-price case passes. The previously retained save comparison has **17 persisted journey/meal timestamp differences**, each one or two floating-point steps. This is now an automated red regression rather than a retained flag.

The cause is meal retry scheduling reading `CitizenState.NextEventMinute` while its traffic estimate was last published by an arbitrary frame/snapshot. Food events already advance the traffic clock to their own logical minute. Retry scheduling now queries traffic progress at that minute to derive the same journey-completion estimate without mutating the snapshot or kernel. Non-traffic residents retain their scheduled event minute. This applies to own and represented meal waits; traffic movement, snapshot publication, affordability, choice scoring and save wire are unchanged.

The focused **11-test** batch (both rush menus plus all nine traffic adversarial cases) passes. Both menu hashes below match their uninterrupted saves exactly. These detached timings include each replay's bounded advancement interval and snapshot synchronization, with market/route/stop doubles; they exclude warmup, serialization and hashing. They are not Unity mod-work or whole-frame measurements.

| Menu | Before p95 / frame max / unit max ms | After p95 / frame max / unit max ms | Before / after frame ≥30 ms | Before / after unit ≥30 ms | After matched hash |
| --- | --- | --- | --- | --- | --- |
| Original | 0.100459 / 3.444792 / 2.537833 | 0.110000 / 4.412709 / 2.645917 | 0 / 0 | 0 / 0 | `743E286ADEC6CAC648FBF8039BD6697118D93822A5A8D81CFD493E853423EBB8` |
| Native prices | 0.172667 / 4.724792 / 4.486500 | 0.173583 / 3.844792 / 2.990292 | 0 / 0 | 0 / 0 | `E58D0465DA31E831B72151F2A1BD66F91E8F8B8BE8595117725F7A460FAF87AC` |

No speedup is claimed. Reproduce sequentially after `pgrep -f testhost` with the task's Homebrew environment and `dotnet test tests/PersistentCity.Tests.csproj --filter 'FullyQualifiedName~RushSpikeReplayTests|FullyQualifiedName~TrafficAdversarialTests' --logger 'console;verbosity=detailed'`. No fixture payload is committed. Writer 33 and its original golden pins remain unchanged.

The round-2 required foreground build passes, DLL SHA256 `91c9839028c2888da4d6be1eb4cff56e01744bbd9c2b3df8d2336fe5a113574f`. The required full foreground suite passes **3,896 tests**, zero failed/skipped, in **4 min 3 s**, including both original writer-33 golden pins. No persisted format or simulated golden/trace expectation changes are needed for this correction. Documentation links and whitespace checks pass.

### First-review validation

With the initial 23 review cases added, `c6a4796` gives **15 failed / 8 passed**. The final corrected rule plus the added deep-discount case gives **24 passed** in **1.87 s** total test time. The required review-fix build passes, DLL SHA256 `2de5690b4fc4ceb0a730840de1f34d961871326ab0a4ae6ec49047d9c7d30f46`. The final required foreground suite passes **3,895 tests**, zero failed/skipped, in **3 min 53 s**, including both unchanged writer-33 golden pins. The required build and documentation-link/whitespace checks pass. The same older simulated state/trace fixtures are repinned for the corrected choices; all static and original writer-33 pins stay unedited. Reproduction uses the same filtered command below; the temporary pin-capture helper is removed before the final suite and is not committed.

## Initial $5 implementation record (superseded)

The following retains the original cheap-fixture measurements at `c6a4796`. It is historical evidence of the reviewed rule, not the current formula, current test count, or native-price calibration.

### Matched short demand check

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

### Component timing and tool cost

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

### Initial validation and reproduction

First check `pgrep -f testhost`; wait for any running testhost, and run dotnet commands sequentially in the foreground with the task's Homebrew environment:

```sh
env PATH="/opt/homebrew/bin:$PATH" DOTNET_ROOT="$(/opt/homebrew/bin/brew --prefix dotnet)/libexec" PC_F6I_MEASURE=1 dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F6iPriceAnchorTests --logger 'console;verbosity=detailed'
```

The initial base run gives **10 failed / 5 passed** (the price-response assertions turn red; hard affordability and allocation checks already pass). The implemented rule gives **15 passed**, zero failures, in **1.87 s** total test time. The short integrated cases run normally in about one second combined; timing is opt-in. Assertions require at least a 20% quantity reduction per short case; that is an F6i regression bound, not a new F6h acceptance threshold. The required build passed, DLL SHA256 `7063a764553000791e5b39d814f3306b69c0781996aaa556811dd65755ea10f0`.

The final required full foreground unit suite passes **3,886 tests**, zero failed/skipped, in **4 min 38 s**, including the two unchanged writer-33 golden pins. The 33 focused checks of affected expectations, new policy/harness cases and those golden pins also pass. [Checks](../checks/f6i.md) lists every older simulation-generated expectation updated because of changed meals/balances. Documentation links and `git diff --check` pass. No full F6h sweep or tools/*-regression suite is run. The historical five-seed whole-menu dominance at price weights 1 and 3 stays recorded in [F6h calibration](f6h-calibration.md); this slice does not relabel it as passing acceptance.
