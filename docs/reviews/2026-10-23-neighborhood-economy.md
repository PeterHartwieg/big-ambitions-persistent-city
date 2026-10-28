# Neighborhood food economy — 23 October 2026

Status: neighborhood implementation and the native validation campaign are complete. The private candidate remains disabled by default. Frame spikes, substantial loading times and human playtesting remain limits on playable-milestone clearance. Nothing has been installed into Peter's normal game.

The [agreed strategy](2026-10-23-economy-strategy.md) is implemented as a narrow candidate: existing shopping stops can satisfy the same prepared-food intention as a dedicated outing, residents retain individual accounts across days, and the selected shops retain native stock, staffing and financial accounting. The candidate is 0.7.0, save schema 10. See [usage and budget assumptions](../neighborhood-economy.md).

## What changed

- A suitable existing shopping visit can reconsider an unfulfilled food intention. Service and eating must fit that visit; buying creates no additional appetite, trip or protected demand.
- Persistent accounts receive declared outside wages, pensions or support, pay essential bills and a household food basket, and retain savings or arrears. Purchase holds, refusals and native receipts conserve money across reloads. Co-residence does not imply a family, and routine work destinations do not imply native employment.
- Ctrl+F8 provides explicit saved-city enrollment and a bounded management view, with localized addresses to distinguish branches: purchase patterns, customer home districts, observed offer blockers and native operating statements. The latest recorded demand day remains visible after midnight. Native hours changes refresh the enrolled shops' routine access.
- Hidden time skips wait for the existing bounded event queue before crossing native accounting hours. A published simulation minute alone is insufficient: all events at that minute, including daily budget postings, must finish. Historical purchases are never injected into a later native hour.

## Ordinary PC baseline and time-skip check

The same 957-resident pre-food city, one 15-capacity shop at 30 Third Street, one employee with initial skill 75, opening hours 08:00–18:00, and a $10.25 pastry price were used. Each fixture received 300 pastries and 300 bags. Stock was not freely replenished.

| Native accounting day | Ordinary purchases | Skipped-day purchases | Sales | Native operating result | Skipped day, real seconds |
| --- | ---: | ---: | ---: | ---: | ---: |
| 2 | 42 | 42 | $430.50 | −$106.12 | 119.35 |
| 3 | 44 | 44 | $451.00 | −$91.88 | 112.29 |
| 4 | 39 | 39 | $399.75 | −$127.49 | 113.05 |

The native costs and results match between the two run styles on every day. Ordinary six-times fast-forward at the one-third calendar took approximately 762 real seconds per day. Skip timings include diagnostic polling; they are background-rig measurements, not a normal rendered frame benchmark. The disposable test player's energy drain was suspended during skips and restored afterward; resident demand and accounts were unchanged.

Rent was $189/day and wages $216.24/day. Native goods consumption was $131.38, $137.64 and $122.00. Equipment was valued at $2,645 and opening stock at $938.46 using native prices. These were explicit supplied test assets: the operating results do not establish cash return on investment, and deposits, acquisition/licensing costs, borrowing and personal taxes are outside this measurement.

The skip campaign also passed settled reload and paid-receipt replay checks. Every resident account reconciled, prepared-food spending matched purchases and native revenue, no resident bought twice in one day, and no uncertain purchase hold remained. All preexisting PC rig files, preferences and its installation manifest were restored and verified before release. Peter then reserved Windows for other work; further tests moved to the Mac.

## Existing-stop experiment: measured limitation

No additional existing-stop purchases occurred in the ordinary three-day baseline. On the first day, the selected shop recorded 62 visits: all 59 visits within the resident's meal window matched a dedicated food outing's chosen arrival. The three ordinary shopping visits occurred around 17:47–17:53, outside those residents' meal windows. The earlier retrospective count of visits during meal windows did not separate dedicated food outings from ordinary shopping and was not evidence of 59 unused attached-buying opportunities.

The first day's offer observations were 930 evaluations: 797 travel/time rejections, 49 service/queue rejections, 25 preferred alternatives and 59 chosen offers. Forty-two purchases completed; some chosen trips later missed service/arrival constraints. These are evaluated offers and first observed blockers, not unique citywide unmet customers. The mechanism works in ownership/reload tests, but no native attached purchase occurred in any of the eight market cases. This is the strategy's measured blocking-assumption outcome, not a reason to multiply demand.

## Mac business choices

Eight cases completed across seventeen game days: three baseline days and two days for each variation. All cases started from the same imported 957-resident pre-food city. Location cases used the same $10.25 price, initial staff skill 75 and 08:00–18:00 hours. Each shop received the same valued equipment and 300 pastries/bags. Daily income, expenses, purchases, native product-stock consumption and operating statements reconciled; all settled reloads and paid-receipt replays passed. No uncertain money hold remained and no resident bought twice in one day.

| Case | Equipped capacity/hour | Purchases, first two days | Average native operating result/day |
| --- | ---: | --- | ---: |
| One small shop, 08–18 | 15 | 42 / 44 | −$99.00 |
| Two nearby small shops | 30 | 65 / 67 | −$232.44 |
| Two separated small shops | 30 | 70 / 68 | −$232.07 |
| Larger premises | 20 | 44 / 50 | −$625.51 |
| Small shop, price $15.38 | 15 | 38 / 42 | +$84.83 |
| Small shop, staff skill 25 | 15 | 42 / 43 | −$31.21 |
| Small shop, 06–22 | 15 | 42 / 43 | −$232.31 |
| Small shop, 10–14 | 15 | 39 / 40 | +$5.82 |

The corrected Mac baseline exactly reproduced the PC's 42 / 44 / 39 purchases, individual budgets and native statements. The table compares the first two days consistently; the third baseline day is retained as an additional check. Equipment and opening stock were supplied test assets valued at $2,645 and $938.46 per shop. No free restocking occurred. These are short observed operating results, not measured cash return on investment or sustainable profitability forecasts.

The nearby second branch gained 23 additional buyers on each day, while 11 and 10 baseline buyers chose that branch. The original shop averaged 37 sales instead of 43. The separated pair averaged 69 purchases, with the original shop retaining 42 and 43 sales; no baseline buyer switched to the distant branch. One baseline buyer did not buy on the second day, while 25 additional residents did. Expansion therefore earns local reach without an ownership bonus, while nearby stores share customers. The separated outlet's higher rent almost exactly offset its extra reach: either pair lost about $232/day at these hours and prices.

A higher price lost some customers but produced the best measured operating result. Lower-skilled staff cut wages; their quality effect was delayed by native reputation reporting. Both fixtures were observed at service score 50 on day one, then 70 for skill-75 staff and 23 for skill-25 staff on day two. Sales and quality are not assumed to move instantly or proportionally.

The 06–22 schedule added wages without additional sales. Opening 10–14 retained most sales and made a small operating profit. This finding belongs to the current one-prepared-food-intention model, whose observed purchases concentrate around lunch; it does not establish that full restaurant menus have no breakfast or evening market.

The larger premises cost $744/day in rent versus $189 at the baseline shop. Its single-register fit-out was limited to 20/hour inside a 30-capacity building. Its higher sales did not cover the rent. This compares actual premises and fit-outs, not two equal-capacity layouts or capacity in isolation.

### Test integrity

The original Mac two-shop fixture was excluded: rival shops shared the same native schedule list, and the first shop's shifts pointed to the second register. The diagnostic helper now gives each fixture independent schedules and checks employee/station ownership and midday staffing before advancing time. Location cases also use a common price instead of inherited rival prices. A stale load-health snapshot and a partially written diagnostic response were corrected in the runner; responses now publish atomically. The baseline was repeated afterward and matched every resident's cumulative purchases, budgets and native statements on all three days. These repairs changed the test setup, not the demand model.

Completed results combine the verified nearby-pair case from `build/neighborhood-mac-r4-20261023/` with the seven fresh cases in `build/neighborhood-mac-r5-20261023/`; provenance is recorded alongside the summary. Both campaigns restored and verified all preexisting rig files/preferences/manifests and released ownership. Failed or interrupted attempts remain separate evidence.

## Time skipping and rendered gate

The ordinary and initially gated PC comparisons above establish correct current-hour accounting. Background Mac skips with the original normal-play processing budget took about two minutes per day. The final candidate allows up to 128 events under a soft 16 ms advancement budget during an enrolled economy's hidden time skip. Ordinary play retains 128 total events, at most 16 resident events and a soft 2 ms budget. One native route call can exceed a soft limit. All 1,073 offline tests pass, including identical purchases and final snapshots across 1-, 16- and 128-event gated batches. The three-day native Mac comparison also matches every resident’s purchases, spending and budget, native product stock and all native financial statements. The same background rig fell from 116.13 / 110.25 / 111.89 seconds per day to 33.87 / 31.94 / 29.00: about 72% less waiting. Advancement p95 during hidden skips was 24–32 ms, with 53–60 ms maxima, including diagnostic trace capture; these are soft-budget batches and are not normal-play frame timings.

Three valid 180-second rendered samples used the same saved checkpoint and camera, a one-third calendar, 1920×1080 Low settings, 30 FPS target, vSync off and the M1 Pro Metal player. The window stayed focused at every observation. The control retains the installed modpack and native population, with Persistent City disabled except for the matching calendar policy. It is not an otherwise unmodded game.

| Rendered sample | Mean whole frame | p95 whole frame | p99 whole frame | Maximum | Frames over 100 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| Candidate A | 44.85 ms | 80.39 ms | 222.56 ms | 2,029.54 ms | 161 |
| Native-population control | 44.19 ms | 89.51 ms | 246.65 ms | 877.64 ms | 183 |
| Candidate B | 37.76 ms | 46.39 ms | 174.14 ms | 952.40 ms | 102 |

The candidate's complete population update averaged 1.39 / 0.91 ms, with p95 3.47 / 2.53 ms and maxima 70.77 / 65.90 ms. That includes route preparation and physical work. Both samples ended with all 957 residents caught up. Configured limits were 48 pedestrians and 24 cars; observed peaks were 18 / 15 pedestrians and zero Persistent City cars. This is ordinary local activity, not maximum-body or rush-hour saturation. Candidate A's whole-frame mean was slightly above the control and its worst spike was larger; candidate B was faster. The evidence shows no clear aggregate regression in these short samples, **not** uniformly smooth frame pacing or an isolated cost estimate for the new finance code.

The Unity allocation recorder was unavailable. Whole-game managed heap changes and collection counts were retained, but heap growth is not an allocation rate and cannot be attributed solely to this mod. CPU frame timing was available; GPU timing was not. Broader modpack startup logs contained missing asset bundles and two culling null-reference errors with Persistent City both enabled and disabled. This is not a clean, unmodified-game performance baseline.

One earlier control lost window focus after its first 15-second observation and is excluded. The valid comparison combines candidate A from `neighborhood-render-mac-r2-20261023`, candidate B from `r3`, and the replacement control from `r4`, with explicit checkpoint/build provenance. All three use DLL `65ac5d95…`; environment, camera and every sampled focus state are checked by the verifier. Startup serialization and port-reuse failures were repaired in the harness and never counted as completed samples.

A nine-hour hidden skip finished in 17.74 seconds with zero remaining lag. Two game hours at fast-forward took 70.68 seconds including diagnostic polling; ordinary physical activity resumed afterward, with three observed pedestrians and zero lag. Native opening-hour edits reached the enrolled saved place and restored exactly before time advanced. Saving took 4.06 seconds. The subsequent same-process reload took 369.17 seconds but preserved resident accounts, including held money and paid-food spending. These full-modpack timings include game loading and route readiness; the slow reload is a usability concern, not a passing latency target.

The reload trace identified an initial route-discovery pass taking 210.20 wall seconds but 1.51 seconds of recorded work across low-frame-rate preparation. The final candidate gives only the first discovery pass while paused a soft 16 ms budget, retaining its hard 2,048-step bound. Normal and recurring discovery keep the 1 ms budget. A focused regression harness passed 566 assertions, including identical complete route inventories at both budgets, atomic publication and bounded branch work. The final Mac check completed both a fresh load (147.40 seconds) and a same-process save/reload (3.88 / 102.84 seconds), with accounts reconciled and no catch-up lag. Initial discovery took 32.13 seconds on the fresh load and 1.90 seconds on reload; its observed maximum native slice was 104.39 / 20.33 ms, so the soft budget still cannot preempt a single native call. The earlier 369-second reload and this 103-second reload are observations under varying full-game conditions, not a controlled percentage-speedup claim. Loading remains substantial. The management panel was visually checked with an opaque background above other overlays and the correct native address, “30 3rd Street.”

## Final build and evidence ownership

The final private DLL is `61b8538dfde2474b94f609527dcff1e4755c7266196d06cf84549aac5e65e4c6`, version 0.7.0 / schema 10. All 76 production source files match the final native snapshot. Market cases used `b3369052…`; faster-skip and steady-state frame measurements used `65ac5d95…`. The final delta from the latter is limited to four runtime files: opaque management display and native address formatting, plus the larger first paused discovery budget. Economy code, native purchase ownership, ordinary simulation budgets and recurring route discovery are unchanged. The final source compiles with warnings treated as errors and passed the separate native panel/load/reload check.

Combined market evidence is in `build/neighborhood-mac-r5-20261023/`, skip equivalence in `build/neighborhood-skip-fast-mac-20261023/`, verified rendering in `build/neighborhood-render-mac-r4-20261023/`, and final display/loading evidence in `build/neighborhood-panel-mac-r2-20261023/`. Each result retains the exact build, source and relevant provenance. The final campaign restored and verified preexisting rig files, preferences and installation hashes, removed the probe and released the stopped Mac rig. Windows remained free throughout the Mac continuation. The packaged candidate includes a source/evidence manifest and stays disabled; it contains no diagnostic probe, game assemblies or raw saves.

## Evidence and remaining boundary

All 1,073 offline tests pass. Native compilation uses warnings as errors. The separate live-hours harness checks extensions, temporary closures, reopening and state preservation. Native fixture results and source snapshots are retained privately under `build/neighborhood-finance-20261023/`, `build/neighborhood-skip-20261023/` and the Mac campaign directories. Game binaries, raw saves and decompiled code remain outside Git.

This is an open, single-product local economy with a fixed enrollment scope. It does not implement general shopping detours, physical grocery provisioning/cooking, actual resident-to-native-employer payroll, broader categories or rival investment and closure. The income/expense policy is provisional. Human playtesting still has to establish whether the choices and feedback are enjoyable; reviewer consensus and automated tests cannot establish fun.

The next decision is a short human management playtest of this narrow slice, alongside investigation of the shared frame spikes and remaining load time before broadening the economy. The tests establish explainable local reach and offer/cost tradeoffs; they do not establish that a full city economy is finished or that its pacing is enjoyable.
