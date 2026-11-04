# Coordinate simulation and native accounting while catching up

Refs #88.

**Merge blocked: Sol-r3-1 / Opus-r3-2 native proof is still outstanding.** The corrected HEAD requires
a separate authorized `rig_test` (Codex Sol, high) with native before-red/after-green artifacts,
hook-order/settlement checks, complete mod-work measurements and separate whole-frame costs.
This offline fixer did not launch the game or access the rig. Doubles do not satisfy that rule.

R1 accounting corrections are `6df891d` and `8e5c8a9`. R2 notice/restart guidance is
`a8c6cfc`. R3 save-change recovery is `18cd631`, committed locally for the harness push:

- Publish a verified new epoch in city and traffic clocks before closures, physical arrivals,
  retiming and BeforeSave; retain queued boundary work and reject genuinely unfinished prior
  work. Five new boundary-observation cases cover link reopening, arrival and save/reload.
  The midnight capability boundary is preserved without running transactions in publication.
- Drain fences, then use the remaining tick allowance up to the next minute fence. Hour-safe
  float spacing works across 60. No historical native purchase or receipt replay is introduced.
- A lost epoch now releases native time immediately and stops/detaches PC on Update/save.
  Existing authority records request withdrawal: protect the partial owned hour, drain ending
  work, then restore current/future native customers even after a no-market reload. A separate versioned
  failure-intent marker prevents automatic restart on native save/reload, including accidental
  clock equality. Native saves remain playable **with PC stopped**; original PC bytes/receipts/held funds are retained.
  Lost unprocessed demand is reported. Load a different valid save or unmarked synchronized pre-failure checkpoint in the same
  session to resume normal validation/attachment. The marked failed save stays refused.
- Retain the legacy 120 FPS floor (16 starts / 128 units / 2 ms), preserving 1,920 starts and
  240 soft ms/second. At 30 FPS 32/256/4 ms retains the throughput gain but leaves only 1 ms
  under the complete mod-work p95 gate. No native performance pass is claimed.
- R2: Boundary clips retain counters and a quiet single-line status without the large box.
  Only workload holds/refusal raise the prominent notice; startup/routes say **calendar held**
  even before a simulator exists. Remove unused `EconomySkipClock` and test the production limiter.

Writer **25** is unchanged. Native checkout and duplicate-sale receipts retain authority.
The offline envelope remains 957 residents / 1,914 consumers, one finite-stock shop/product,
three meals, household posting, fixed one-minute walking, integer-stamped inputs and no native
bodies/queries. Wider native workloads and arbitrary subminute changes are not certified.

R3 implements the coordinator-selected save-change recovery. Only an accounting-specific
failure may clear when a different non-null save object finishes loading. Old population,
routes, initialization, save binding and UI are discarded before startup for the new save;
marked stopped saves are refused again before initialization. Other failures stay latched.
The regression compiles actual Fail/reset/transition methods with native cleanup doubles and
covers both unmarked and marked loads, another PC city, pre-attachment stops, loading gaps,
old-state cleanup and retained payloads. All **62 required matrix commands** pass, including both nested wrappers: **2,832 tests**,
zero failures/skips; **2,740 discovery entries**. Build, identity, save contract, checkout,
ownership and all regression/source checks pass.
[R3 exits/hashes](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/codex/pc-f1-accounting-backlog/docs/checks/f1-accounting-backlog-r3-results.json).
Final detached 30/60/120 p95 **0.0370/0.2919/0.3453 ms**, max **4.8222/4.6583/3.8542 ms**;
skip p95/max **0.0204/2.5089 ms**, stress **0.0128/13.1749 ms**.
Across six R3 runs: **0 samples >=30 ms**, maximum **19.3869 ms**. The R2 66.3905 ms sample
remains retained and flagged. No native performance gate is established. The unchanged
standalone saturated-history bulk arm separately flags **305.4715/649.4827/286.7959 ms** write/read/activation
maxima (3/3 >=30 ms each); parked serialization is unchanged.
No native attachment or settlement proof is claimed. R2 restart-only guidance is superseded.

R2 offline evidence (retained): the pre-R2 clock fails the new zero-work notice regression; the
corrected clock passes 180 empty ordinary minutes at 1/3, 1 and 2 game minutes/second without
the workload notice, then shows it on a real hold. Actual AccountingRecovery source verifies
restart-first guidance and retained failure intent (native/Fail doubles). Warm quiet text
uses **0 B / 0 builds**, versus **16,640 B / 40 builds** per 1,200 prior cached-status requests;
GUI cost is excluded. The first nested repeat flags **one 66.3905 ms detached F1 frame**
at 120 simulated FPS (p95 0.4610 ms). The save-free sample is retained and unattributed;
that run does not pass the detached spike condition. Native performance remains unverified.
The required foreground matrix passed **62/62 commands**, including both full wrappers: **2,832 tests**,
zero failures/skips; **2,740 discovery entries**. Build and every required regression/source
check passed. Both pre-R2 isolated-source negatives fail as expected; current source passes.
[Command exits/hashes](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/codex/pc-f1-accounting-backlog/docs/checks/f1-accounting-backlog-r2-results.json).

Final detached ordinary 30/60/120 p95 **0.0188/0.1338/0.2736 ms**, max **4.7865/4.6588/4.2513 ms**;
skip p95/max **0.0173/3.1596 ms**, stress **0.0158/14.7591 ms**.
Across six R2 runs: **one sample >=30 ms**, maximum **66.3905 ms**; the detached spike condition
is not passed. Native mod-work and separately tracked whole-frame costs remain unverified.
The inherited saturated-history bulk arm separately flags write/read/activation maxima
**310.6081/609.7626/255.2312 ms** (3/3 >=30 ms each); parked serialization is unchanged.

R1 offline evidence (retained):

- All required foreground commands passed across the resumed sessions, including both broad
  wrappers. Current suite: **2,832 tests**, zero failures/skips; **2,740 discovery entries**.
  Ownership: **249 assertions**. Build, identity, save contract, checkout and all required
  regression/source tools passed. The first segment lost its aggregate exit JSON at harness
  resumption; preserved output and independently repeated zero-exit nested matrices are
  qualified in the [R1 manifest](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/codex/pc-f1-accounting-backlog/docs/checks/f1-accounting-backlog-r1-results.json).
- Every cadence/budget and skip arm has identical final save bytes and ordered native-double
  sales: 969 confirmed purchases, 971 original orders, 164 recorded missed meals and identical
  balances/held funds. Two acknowledgements remain uncertain and are not replayed.
- Zero-work baseline at one-third pace retains **0.336/0.333/0.210%** slowdown at 30/60/120 FPS
  versus identical uncapped float arithmetic. At 6x requests it retains **4.773/2.886/1.553%**.
  These remaining phase/float costs are counted and shown through pacing status.
- An eight-hour zero-work skip at 60 FPS takes **481 ticks / 8.017 simulated seconds**, versus
  **960 / 16.000 s** before review and **16 / 0.267 s** with the historical hourly limiter.
  The remaining approximately one-tick/minute floor is still **30.06x** the old hourly minimum;
  this is a documented minute-input contract cost, not a skip-latency pass.
- Final detached frame p95 (30/60/120) **0.0118/0.0839/0.2280 ms**, max
  **4.0703/2.6435/2.5934 ms**; skip p95/max **0.0169/3.9963 ms**, stress **0.0092/5.8933 ms**.
  Final >=30 ms F1 samples: **0**. Lag <1 game minute; injected supported/stress recovery
  2/10 s. These complete detached frame costs exclude native/Unity work; full native
  mod-work/whole-frame costs remain pending, and the native proof blocker remains open.
  [All initial/R1 measurements and limits](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/codex/pc-f1-accounting-backlog/docs/perf/f1-accounting-backlog.md).

- The inherited standalone saturated-history bulk arm flags write/read/activation maxima
  **289.8408/583.4523/340.8855 ms** (3/3 >=30 ms each), separate from F1 frame windows.
  Parked serialization remains unchanged; no native frame-gate pass is implied.

## In-game checks for the next batched rig

These six checks are executed by `PeterHartwieg/big-ambitions-agent-rig`, owned by Coding
Projects. Missing capabilities are work for the rig owner and block their check; no manual
substitution. Use named disposable FT cities. Preserve/restore rig files/preferences; disable
and verify autosave off before every timing window and after reload. Save outside measured
windows; any save invalidates a clean timing window. Attach proof for the corrected HEAD.

1. **One-day cadence/input replay — rig-automated (existing capability: `rig flow run`, FT `state`/`save`/`load`/`time`, `shop_fixture`, `shop_state`, `shop_log`; pending rig capability: fixed 30/60/120 rendered-cadence and scheduler-budget arms, minute-stamped input publication and independent PC/native ledger comparator).** Restore one synchronized FT checkpoint for each arm with unchanged configured calendar factor, resident/consumer/shop/menu/body/graphics setup and seed. Apply identical minute-timestamped prices, hours, staffing and finite-stock deliveries after each prior epoch drains. Run one logical day at 30/60/120 and different event/time budgets. Read full meal outcomes, balances/holds, PC receipts, native completed order IDs/counts, product/bag deltas, hourly and midnight revenue/bank settlement. Assert identical purchases, missed meals, funds and ordered native sales, no duplicate/replayed purchase and zero historical-clock misses. Retain the old-head red reproduction and this-head green trace separately from detached evidence.
2. **Overload and recovery — rig-automated (existing capability: `rig flow run`, FT `state`/`time`; pending rig capability: ready/no-due-work control, controlled scheduler stall/budget injection plus `AccountingDiagnostics` sampling and automated pacing-notice capture).** First run a fresh ready/no-due-work disposable control for 180 native minutes at 1/3, 1 and 2 game minutes/second to distinguish quiet fence status from workload backpressure. Then at a meal rush inject a fixed finite stall, then restore the original work allowance. Repeat at the supported candidate load (957 residents, factor two) and a declared stress load. Read native/logical targets, due/pending work, ordinary/skip hold ticks, withheld time, configured factor, refusal and recovered wait duration. Assert native time holds before closing the input epoch, admitted lag <=1 game minute, zero-work minute fences leave `pacingAffected=false` with only the quiet baseline line and increasing cap counters, while the prominent notice appears for short repeated and sustained workload holds, recovery drains without a fast repayment burst, and post-recovery purchases/outcomes/money/orders equal the matched unstalled logical-day arm. A nonprogressing workload must remain visibly held, never silently drop demand.
3. **Meal/hour/midnight and skip boundaries — rig-automated (existing capability: FT `time`, `state`, `shop_state`, `shop_log`; pending rig capability: exact native float-fence/ending-accounting hook trace with time-machine control and pending-work assertions).** Run ordinary and time-machine arms through meals, each hour and midnight with work due in the final float gap and exactly at the boundary; reopen a blocked link and finish a physical journey in BeforeAdvance at that crossing. Read final tick arguments, guard readiness, event timestamps, native accounting epoch, ending-hour queues, receipts and daily totals. Assert all strictly earlier events drain under the old epoch, boundary events use the new inputs, the skip destination is unchanged, no historical buy is attempted, and native settlement remains exact with unchanged ownership rules.
4. **In-flight save/reload — rig-automated (existing capability: FT `save`/`load`/`state`, `rig flow run`; pending rig capability: named held-decision/rounding-gap/midnight-backlog checkpoints and receipt/funds comparator).** Outside timing windows save during ordinary lag, a held food decision, an excursion, the final float gap, a completed physical journey in BeforeSave and pending midnight postings; reload each and finish the same logical day. Verify native autosave remains off before resuming timing. Read writer-25 payload, native time, target/readiness, held funds, original uncertain receipts and completed orders. Assert exact state/economic equivalence to uninterrupted arms, no duplicate sale, retained uncertainty and no schema change. Hold startup through route preparation. Load an intentionally out-of-epoch disposable snapshot and assert visible synchronized-input-epoch refusal with the original payload unchanged and no historical checkout.
5. **Complete cost and throughput together — rig-automated (existing capability: `rig flow run`, FT `state`, automated capture; pending rig capability: complete mod-work union plus independent whole-frame/presented-frame intervals correlated with F1 lag, work units and recovery).** With the same autosave-off setup, record supported/stress ordinary meal/accounting peaks and separate skip/reload latency arms. Read p50/p95/max complete mod-work, every >=30 ms spike with count/condition/component when attributable, whole-frame costs/stalls separately, actual body counts, units/second, maximum lag, withheld calendar time and catch-up duration. Require ordinary mod-work p95 <5 ms and individual spikes <30 ms; flag every spike >=30 ms; never use whole-frame p95 as mod-work or a low p95 to excuse nonprogressing demand. Compare zero-work ordinary/fast-forward/8-hour skip pacing against the offline old/new controls, including the remaining one-tick-per-minute skip floor; record native baseline pacing cost explicitly. Preserve unattributed stalls and any failed capacity envelope as unresolved evidence.
6. **Clock/authority interoperability and refusal — rig-automated (existing capability: RIG-MODSET DLL transactions, FT `state`/`load`/`time`, `shop_state`, `shop_log`; pending rig capability: final tick-prefix/Driven Rides observer trace, disposable bypass-clock fault arm and same-session save-identity/attachment/stop-marker lifecycle assertions).** Exercise configured factor 1 and 1/3 with the installed ride/run-until policies, ordinary fast-forward, pause, cold startup and teardown. Read the final positive/zero tick argument, inferred travel pace, native current hour, PC authority and saved receipts. Assert earlier caps/zero holds survive the final guard, sustained accounting caps never retime journeys, unsupported businesses remain native, ending-hour transfer remains safe and teardown restores clock ownership. Inject a bypassing clock writer only in the disposable fault arm and assert visible PC stop, immediate native-clock release, no new PC checkout, preserved population/receipts and protected partial-hour authority. Save/reload after refusal and assert the native city remains playable with PC stopped and next-boundary native demand resumes without duplicate orders. Remove the fault and, without restarting the game, load an unmarked synchronized pre-failure checkpoint and then another valid PC-owned city. After normal startup/routes finish, assert accounting failure cleared, the runtime matches each new save, old pending work/caches are absent, population ownership validation completed (not stuck awaiting population), PC checkout resumes and no duplicate native demand/order occurs. In the same session reload the marked failed save as a new save object; assert PC remains refused before initialization, the original payload/marker remain unchanged and native current/future demand follows the withdrawal manifest. Include a refusal before initial attachment (no saveIdentity), null unload/loading gaps and a non-accounting refusal that must not auto-clear; verify cold population/route preparation explicitly reports calendar held.
