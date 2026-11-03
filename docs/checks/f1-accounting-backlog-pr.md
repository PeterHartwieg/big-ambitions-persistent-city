# Coordinate simulation and native accounting while catching up

Refs #88.

Native accounting could close an hour while rendered-frame budgets left meals unfinished,
turning workload backlog into missed demand. Ordinary play and time-machine skips now share
a minute-epoch drain barrier: due work, held decisions and routes finish before the epoch
closes, including the final native float rounding gap. Admitted logical lag stays below one
game minute; overload holds calendar time and resumes at the configured factor after draining.
There is no fast repayment burst or historical purchase replay.

The guard covers initialization/route preparation and checks the epoch again at advancement
and save entry. Unsynchronized snapshots or bypassing clock writers are visibly refused and
the original payload is retained. Native checkout, receipts, uncertain funds, business
authority and writer **25** are unchanged. Existing saved timestamps/contexts plus native
time reconstruct readiness after load, so no schema 26 is required.

Ordinary advancement caps now scale with elapsed time (30/60/120: 32/16/8 citizen starts,
256/128/64 units and 4/2/1 soft ms), with bounded long-stall caps. Constrained clock samples
cannot retime travel. Diagnostics expose lag, holds, withheld requested time and recovery;
visible pacing text is cached at 4 Hz. F3 retains bulk-unit/route progress work and the four
parked hotspots remain parked.

Offline validation and limits:

- All 62 matrix commands plus the final review build, 2,824 unit cases (20 F1 cases), discovery, identity/save/10,648-check checkout and F1 regression passed; command exits and hashes are recorded in the owning
  [checks](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/codex/pc-f1-accounting-backlog/docs/checks/f1-accounting-backlog.md).
- At 957 residents / 1,914 consumers the 30/60/120 and skip arms have identical final writer-25
  bytes and ordered native-double sales: 969 confirmed purchases, 971 original orders, 164
  missed meals and identical balances/held funds. Two acknowledgements remain uncertain;
  no sale is replayed. In-flight and midnight reload arms match uninterrupted state.
- Detached frame p95 **0.0274/0.2508/0.1894 ms**, max **2.8245/14.8037/4.4055 ms**;
  skip p95/max **0.0084/15.0445 ms**, stress **0.0090/14.0765 ms**. Zero clean >=30 ms
  component samples. Four preliminary save-containing windows with >=30 ms maxima are
  explicitly invalidated. Lag <1 game minute; recovery includes the injected 2 s supported /
  10 s stress stall. The artificial 30-minute/second input pace plus tiny budgets takes
  1,018.1/96.100/62.133 simulated seconds per day: backpressure cost is reported, not a pace pass.
- Taken cap win: 960 same-minute residents at 30 simulated FPS drain in **60 -> 30 frames,
  2 -> 1 simulated seconds**, with identical state. Pure limiter: 100,000 calls / 0.6375 ms /
  0 B. [Full performance evidence and raw output](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/codex/pc-f1-accounting-backlog/docs/perf/f1-accounting-backlog.md).
- Cached visible text: 1,200 requests **499,200 -> 16,640 B**, **1,200 -> 40 builds**, actual formatter with native/UI doubles; no native GUI cost claim.
- Supported **offline** envelope: one always-open finite-stock shop/product, three meals,
  household postings, fixed one-minute walking and integer-minute external inputs; stress
  2,000 residents is separate. Arbitrary subminute mutations/physical motion and wider native
  workloads are not certified. [Explicit input/refusal contract](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/codex/pc-f1-accounting-backlog/docs/plans/f1-accounting-boundary.md).
  Complete native mod-work p95 <5 ms, every >=30 ms mod spike, whole-frame costs/stalls and
  native accounting proof remain pending. The headless fixtures are not in-game proof.

## In-game checks for the next batched rig

All six checks below are executed by `PeterHartwieg/big-ambitions-agent-rig`, owned by Coding
Projects. Existing interfaces are not native proof. Pending capabilities are work for the
rig owner; a missing capability blocks its check. Use disposable FT fixtures, preserve original
files/preferences, disable and verify autosave off before every measured window and after
reload, save outside windows, and restore during cleanup. Any save invalidates a timing window.

1. **One-day cadence/input replay — rig-automated (existing capability: `rig flow run`, FT `state`/`save`/`load`/`time`, `shop_fixture`, `shop_state`, `shop_log`; pending rig capability: fixed 30/60/120 rendered-cadence and scheduler-budget arms, minute-stamped input publication and independent PC/native ledger comparator).** Restore one synchronized FT checkpoint for each arm with unchanged configured calendar factor, resident/consumer/shop/menu/body/graphics setup and seed. Apply identical minute-timestamped prices, hours, staffing and finite-stock deliveries after each prior epoch drains. Run one logical day at 30/60/120 and different event/time budgets. Read full meal outcomes, balances/holds, PC receipts, native completed order IDs/counts, product/bag deltas, hourly and midnight revenue/bank settlement. Assert identical purchases, missed meals, funds and ordered native sales, no duplicate/replayed purchase and zero historical-clock misses. Retain the old-head red reproduction and this-head green trace separately from detached evidence.
2. **Overload and recovery — rig-automated (existing capability: `rig flow run`, FT `state`/`time`; pending rig capability: controlled scheduler stall/budget injection plus `AccountingDiagnostics` sampling and automated pacing-notice capture).** At a meal rush inject a fixed finite stall, then restore the original work allowance. Repeat at the supported candidate load (957 residents, factor two) and a declared stress load. Read native/logical targets, due/pending work, ordinary/skip hold ticks, withheld time, configured factor, refusal and recovered wait duration. Assert native time holds before closing the input epoch, admitted lag <=1 game minute, the player notice appears for short repeated and sustained holds, recovery drains without a fast repayment burst, and post-recovery purchases/outcomes/money/orders equal the matched unstalled logical-day arm. A nonprogressing workload must remain visibly held, never silently drop demand.
3. **Meal/hour/midnight and skip boundaries — rig-automated (existing capability: FT `time`, `state`, `shop_state`, `shop_log`; pending rig capability: exact native float-fence/ending-accounting hook trace with time-machine control and pending-work assertions).** Run ordinary and time-machine arms through meals, each hour and midnight with work due in the last representable float gap and exactly at the boundary. Read final tick arguments, guard readiness, event timestamps, native accounting epoch, ending-hour queues, receipts and daily totals. Assert all strictly earlier events drain under the old epoch, boundary events use the new inputs, the skip destination is unchanged, no historical buy is attempted, and native settlement remains exact with unchanged ownership rules.
4. **In-flight save/reload — rig-automated (existing capability: FT `save`/`load`/`state`, `rig flow run`; pending rig capability: named held-decision/rounding-gap/midnight-backlog checkpoints and receipt/funds comparator).** Outside timing windows save during ordinary lag, a held food decision, an excursion, the final float gap and pending midnight postings; reload each and finish the same logical day. Verify native autosave remains off before resuming timing. Read writer-25 payload, native time, target/readiness, held funds, original uncertain receipts and completed orders. Assert exact state/economic equivalence to uninterrupted arms, no duplicate sale, retained uncertainty and no schema change. Hold startup through route preparation. Load an intentionally out-of-epoch disposable snapshot and assert visible synchronized-input-epoch refusal with the original payload unchanged and no historical checkout.
5. **Complete cost and throughput together — rig-automated (existing capability: `rig flow run`, FT `state`, automated capture; pending rig capability: complete mod-work union plus independent whole-frame/presented-frame intervals correlated with F1 lag, work units and recovery).** With the same autosave-off setup, record supported/stress ordinary meal/accounting peaks and separate skip/reload latency arms. Read p50/p95/max complete mod-work, every >=30 ms spike with count/condition/component when attributable, whole-frame costs/stalls separately, actual body counts, units/second, maximum lag, withheld calendar time and catch-up duration. Require ordinary mod-work p95 <5 ms and individual spikes <30 ms; flag every spike >=30 ms; never use whole-frame p95 as mod-work or a low p95 to excuse nonprogressing demand. Record the native fence's baseline pacing cost explicitly. Preserve unattributed stalls and any failed capacity envelope as unresolved evidence.
6. **Clock/authority interoperability and refusal — rig-automated (existing capability: RIG-MODSET DLL transactions, FT `state`/`load`/`time`, `shop_state`, `shop_log`; pending rig capability: final tick-prefix/Driven Rides observer trace and a disposable bypass-clock fault arm).** Exercise configured factor 1 and 1/3 with the installed ride/run-until policies, ordinary fast-forward, pause, cold startup and teardown. Read the final positive/zero tick argument, inferred travel pace, native current hour, PC authority and saved receipts. Assert earlier caps/zero holds survive the final guard, sustained accounting caps never retime journeys, unsupported businesses remain native, ending-hour transfer remains safe and teardown restores clock ownership. Inject a bypassing clock writer only in the disposable fault arm and assert a latched visible refusal with no new PC checkout or payload overwrite; remove the fault and reload a synchronized checkpoint to recover.
