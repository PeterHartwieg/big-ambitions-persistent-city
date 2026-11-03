# City-audit F1 — 3 November 2026 — offline only

Refs #88. Base `bd99eb3a45d6b008911e1a5225ce1ced590cec51`; audit source inspected from
`origin/codex/city-vision-audit-2026-11-03` without importing or editing the report.
This is separate from Track F's historical shop-view F1.

Implemented the [simulation/accounting contract](../plans/f1-accounting-boundary.md):
one-minute input epochs, float-safe end draining, ordinary/skip backpressure, a less-than-one
minute admitted lag bound, explicit recovery/refusal, wall-time frame caps and visible lag/hold
status. Writer **25** and native sale/receipt authority remain unchanged. Native proof and
complete native mod-work/whole-frame measurement are pending the batched rig.

## Initial evidence and acceptance (before R1)

The original evidence below is retained. Corrected behavior and current results are in the
R1 section; native proof remains a merge blocker.

- Re-verified the audit against this base: `FoodCommerce.StepMeal` still misses active meals
  when `now < market.EarliestMinute`; `AdvancePopulation` still had fixed 128-unit/16-citizen
  ordinary caps and 2 ms; `NativeCityClock.TickPrefix` held skips but only multiplied ordinary
  ticks. E5 code movement did not remove the finding. The sale adapter's `BuyLive` rejects
  outside the current native hour and checks saved receipts; neither authority check changed.
- Red: the foreground test advanced the native market hour with active meals and failed its
  no-`Native clock passed the purchase window` assertion (60-resident fixture). Green: the
  same in-flight boundary fixture uses the production guard and one-unit slices, with zero
  historical-clock misses. This is source/offline evidence only.
- Twenty focused cases cover 30/60/120 cadence with 1/16/128 units and 0.01/0.5/2 ms budgets,
  identical timestamped price inputs, skip parity, held/travelling and midnight writer-25
  reloads, float predecessors through large absolute days, stalled frames, recovery and
  constrained-clock travel-pace inference, Unity float-duration caps, a Core checkout into the market double in the
  final float gap, a writer-25 gap round trip and unfinished exact-predecessor refusal before/
  after market attachment. Entire final save bytes and native-double ordered
  sales must match, covering purchases, missed meals, wallets, held funds and native orders.
- The foreground `accounting-backlog-regression` additionally runs the 957-resident /
  1,914-consumer day at every cadence, compares whole save bytes and ordered sales, and runs
  a 2,000-resident stress day. Actual `NativeCityClock` source is compiled with native/Harmony
  doubles to verify registered hook priority, ordinary/skip holds, fence crossing, visible
  pacing, recovery counters, bypass refusal and teardown. The doubles do not establish native
  Harmony order, native inventory or in-game UI correctness.
- Supported offline envelope and timestamped-input limits are explicit in the contract. No
  native capacity claim is made. An out-of-epoch save or bypassing native clock writer is
  refused rather than repaired with lost meals or historical purchases. Arbitrary subminute
  external mutations/physical motion are not proven cadence-independent by this fixture.
- [Full measurements and raw output](../perf/f1-accounting-backlog.md): detached supported
  p95s 0.0274/0.2508/0.1894 ms (30/60/120), maxima 2.8245/14.8037/4.4055 ms; skip p95/max
  0.0084/15.0445 ms; stress 0.0090/14.0765 ms. Zero clean >=30 ms component samples; four
  preliminary save-containing >=30 ms windows are flagged and invalidated. Lag <1 minute;
  recovery includes the injected 2 s supported / 10 s stress stall. No native mod-work or
  whole-frame measurement is available; their gates remain pending. Frame-cap win at 30 FPS:
  960 same-minute residents drain in 60 -> 30 frames (2 -> 1 simulated seconds), equal state.

## Initial foreground validation (before R1)

All **62 matrix commands passed**, covering the required commands, both nested validation wrappers and
the additional F1 regression. The final review pass also passed build, **2,824 unit cases /
zero failures or skips**, discovery (**2,732 listed entries**), identity, save-contract, **10,648** checkout assertions
and the F1 source/cadence/recovery benchmark. The last game-reference build passed with DLL
SHA-256 `7d82b931f795603310e91d16f9c9848e5573d35f66c28f59ea8fed77150067b1`.
[Command exits and log hashes](f1-accounting-backlog-results.json) retain the matrix and final
checks separately; earlier matrix test runs contained fewer cases while review regressions
were added. Core identity and save-contract digests remain equal to the initial matrix.

The shared `wait-other-dotnet.ps1` gate ran before each foreground command. Build servers
were gracefully shut down; final process inspection found zero repository dotnet/testhost
processes. Generated E5c validation manifests were restored to keep this slice local.
No game or rig run is part of this evidence. The source fixture, clock contract checks and
measurements are reproducible with
`tools/accounting-backlog-regression/AccountingBacklogRegression.csproj`.

## R1 review corrections — 3 November 2026 (offline)

Code: `6df891d`, `8e5c8a9`. Sol-r1-1 and Opus-r1-1/2/3/4/5 are implemented and covered offline:

- A verified new epoch is published in city **and traffic** clocks before closure, physical,
  retiming and BeforeSave observations. Queued boundary events remain queued. The five new
  regression cases reproduce the old drained-to-undrained transition for a reopened link and
  physical arrival, then verify the published epoch and a single arrival after save/reload.
  Genuine prior work remains rejected. The midnight capability fact is published with its
  clock, preserving the original multi-day purchase/ledger comparison.
- Ready fences use the remaining tick allowance up to the next minute fence, with uniform
  hour-safe float spacing across 60. The unused `EconomySkipClock` is removed; its existing
  food regression now runs the production limiter. The before-review limiter is retained
  only as a local benchmark control, alongside the historical hour-granular control.
- Calendar caps as well as workload holds trigger the pacing notice. Startup/routes show
  “calendar held” before a simulator exists. The legacy 120 FPS floor is restored:
  16 citizen starts / 128 units / 2 ms, 1,920 starts and 240 soft ms per second. The 30 FPS
  4 ms allowance leaves only 1 ms below the complete 5 ms mod-work gate and requires native proof.
- On a genuinely lost epoch, native ticks immediately run without the accounting cap. Update
  or save entry stops/detaches PC and requests withdrawal using the existing food-authority
  manifest. Native time continues; the partially owned hour remains protected and the next
  drained boundary restores current/future native customers, including after reload without
  an attached market. The ownership source regression passes 249 assertions including this
  lifecycle. Original population bytes, receipts, held money and native orders are retained.
  Unprocessed PC demand is visibly reported as lost, never replayed. A subsequent native save
  remains playable **with PC stopped**; quit and relaunch the game, then load a synchronized pre-failure checkpoint to resume PC.

Three additional stop-marker cases cover native-save/reload intent, idempotence, unknown
marker preservation and unchanged archived population/receipt bytes. The marker is separate
from population writer 25; it prevents unsafe automatic restart after a failed epoch.

The required foreground matrix passed across two sessions: the first 41 commands reported
success before harness resumption; commands 42-62 then completed with recorded zero exits.
The two required nested wrappers independently reran build, current unit/discovery and broad
regressions successfully. The first segment output survives, but its aggregate exit JSON was
lost with the parent session; this limitation and command/log hashes are explicit in the
[R1 results manifest](f1-accounting-backlog-r1-results.json). The current full suite passed
**2,832 cases**, zero failures/skips, and discovery listed **2,740 entries**. The earlier focused
run passed 26 cases before the three marker cases were added. Ownership passed 249 assertions.
Writer 25 is unchanged. The final nested game-reference build DLL SHA-256 is
`eaf0f9c395fa341c4c9336a5350072860620f57d2cf7a473e6719ac84cb2f9cc`.

Final detached ordinary 30/60/120 p95 **0.0118/0.0839/0.2280 ms**, max **4.0703/2.6435/2.5934 ms**;
skip p95/max **0.0169/3.9963 ms**; stress **0.0092/5.8933 ms**.
Final >=30 ms F1 samples: **0**. Lag stayed <1 game minute; stall/recovery remained 2 s supported /
10 s stress. These are detached costs, never a native mod-work or whole-frame gate pass.
The shared dotnet gate ran before foreground work; all processes started by this fixer
finished, and final inspection found no repository test/dotnet processes left. Generated
unrelated E5c evidence was restored. No game or rig work occurred.
See [R1 performance and baseline pacing costs](../perf/f1-accounting-backlog.md#r1-review-corrections).

**Sol-r1-2 remains unresolved and blocks merge.** No authorized separate `rig_test` session,
native red/green artifacts, actual hook-order/settlement proof or complete native mod-work /
whole-frame measurements were produced by this offline fixer. The original no-game/no-rig
restriction was retained. Source doubles and the prepared rig handoff do not satisfy that
project rule. The authorized rig owner must validate the corrected HEAD and attach proof to
PR #99. Do not mark this finding passed from the offline results.

Sol-r1-3 is handled by updating PR #99's actual body from the matching prepared description;
that metadata action is separate from repository commits and is verified by readback.

## R2 review corrections - 3 November 2026 (offline)

- **Opus-r2-1:** Routine ready fence clips no longer update the workload-notice timer.
  They retain diagnostics and Ctrl+F8 totals and use a quiet 22-pixel-high single-line label
  without a background box. Actual workload holds/refusal retain the prominent card and
  three-second workload-hold tail. No percentage threshold is used: this implements the
  review's small-status-line alternative. The native clock contract runs 180 zero-work
  minutes at each of 1/3, 1 and 2 requested game minutes/second, requiring `PacingAffected`
  false throughout, nonzero fence diagnostics, then a real hold with the notice true.
- **Opus-r2-2:** The chosen policy retains the session-sticky failure state. Both refusal
  messages now lead with **quit and relaunch**, then load a synchronized pre-failure
  checkpoint. Loading another save in the failed session does not restart PC; restart before
  loading another city, since its PC-owned businesses otherwise have no attached simulator.
  A fresh runtime still honors the persisted stop marker on the failed save. The regression
  compiles the actual AccountingRecovery source and checks restart-first guidance, authority
  withdrawal and preserved stop intent/population bytes; native authority and Fail are doubles.
- **Sol-r2-1 remains unresolved and blocks merge.** No native rig_test session or native
  red/green artifacts were produced here. Actual hook/input ordering, settlement, complete
  mod-work and separately measured whole-frame costs still require the authorized Codex Sol
  high rig session. The original no-game/no-rig restriction remains in force for this fixer.

The first nested R2 repeat flags one **66.3905 ms** detached F1 frame at 120 simulated
FPS (p95 0.4610 ms). The save-free raw sample is retained, unattributed and not discarded;
that repeat does not pass the detached spike condition. Native timing remains unverified.

R2 foreground matrix results will be recorded here after completion. R1 evidence above is
retained; its statement that fence clips raise the prominent notice is superseded by R2.
The numbered rig checks below use the corrected notice and restart policy.

## In-game checks for the next batched rig

All checks are executed by the headless agent rig in
[`PeterHartwieg/big-ambitions-agent-rig`](https://github.com/PeterHartwieg/big-ambitions-agent-rig),
owned by Coding Projects. Read-only README/capability inventory: `25aecb0f7fe7b130a8a290df8e613abfebf35501`.
No rig files, preferences, lease, ModsLocal, bridge profile or game process were touched in
this task. Existing capabilities below are available interfaces, not claims of native proof.
Pending capabilities must be implemented by the rig owner; a missing capability blocks that
check instead of substituting manual verification. Only named disposable FT fixtures may be
used. Preserve original preferences/rig files; disable and verify autosave off before **each**
timing window, including after reload, and restore during cleanup. Manual saves belong outside
timing windows; any save in a window invalidates the window.

1. **One-day cadence/input replay — rig-automated (existing capability: `rig flow run`, FT `state`/`save`/`load`/`time`, `shop_fixture`, `shop_state`, `shop_log`; pending rig capability: fixed 30/60/120 rendered-cadence and scheduler-budget arms, minute-stamped input publication and independent PC/native ledger comparator).** Restore one synchronized FT checkpoint for each arm with unchanged configured calendar factor, resident/consumer/shop/menu/body/graphics setup and seed. Apply identical minute-timestamped prices, hours, staffing and finite-stock deliveries after each prior epoch drains. Run one logical day at 30/60/120 and different event/time budgets. Read full meal outcomes, balances/holds, PC receipts, native completed order IDs/counts, product/bag deltas, hourly and midnight revenue/bank settlement. Assert identical purchases, missed meals, funds and ordered native sales, no duplicate/replayed purchase and zero historical-clock misses. Retain the old-head red reproduction and this-head green trace separately from detached evidence.
2. **Overload and recovery — rig-automated (existing capability: `rig flow run`, FT `state`/`time`; pending rig capability: ready/no-due-work control, controlled scheduler stall/budget injection plus `AccountingDiagnostics` sampling and automated pacing-notice capture).** First run a fresh ready/no-due-work disposable control for 180 native minutes at 1/3, 1 and 2 game minutes/second to distinguish quiet fence status from workload backpressure. Then at a meal rush inject a fixed finite stall, then restore the original work allowance. Repeat at the supported candidate load (957 residents, factor two) and a declared stress load. Read native/logical targets, due/pending work, ordinary/skip hold ticks, withheld time, configured factor, refusal and recovered wait duration. Assert native time holds before closing the input epoch, admitted lag <=1 game minute, zero-work minute fences leave `pacingAffected=false` with only the quiet baseline line and increasing cap counters, while the prominent notice appears for short repeated and sustained workload holds, recovery drains without a fast repayment burst, and post-recovery purchases/outcomes/money/orders equal the matched unstalled logical-day arm. A nonprogressing workload must remain visibly held, never silently drop demand.
3. **Meal/hour/midnight and skip boundaries — rig-automated (existing capability: FT `time`, `state`, `shop_state`, `shop_log`; pending rig capability: exact native float-fence/ending-accounting hook trace with time-machine control and pending-work assertions).** Run ordinary and time-machine arms through meals, each hour and midnight with work due in the final float gap and exactly at the boundary; reopen a blocked link and finish a physical journey in BeforeAdvance at that crossing. Read final tick arguments, guard readiness, event timestamps, native accounting epoch, ending-hour queues, receipts and daily totals. Assert all strictly earlier events drain under the old epoch, boundary events use the new inputs, the skip destination is unchanged, no historical buy is attempted, and native settlement remains exact with unchanged ownership rules.
4. **In-flight save/reload — rig-automated (existing capability: FT `save`/`load`/`state`, `rig flow run`; pending rig capability: named held-decision/rounding-gap/midnight-backlog checkpoints and receipt/funds comparator).** Outside timing windows save during ordinary lag, a held food decision, an excursion, the final float gap, a completed physical journey in BeforeSave and pending midnight postings; reload each and finish the same logical day. Verify native autosave remains off before resuming timing. Read writer-25 payload, native time, target/readiness, held funds, original uncertain receipts and completed orders. Assert exact state/economic equivalence to uninterrupted arms, no duplicate sale, retained uncertainty and no schema change. Hold startup through route preparation. Load an intentionally out-of-epoch disposable snapshot and assert visible synchronized-input-epoch refusal with the original payload unchanged and no historical checkout.
5. **Complete cost and throughput together — rig-automated (existing capability: `rig flow run`, FT `state`, automated capture; pending rig capability: complete mod-work union plus independent whole-frame/presented-frame intervals correlated with F1 lag, work units and recovery).** With the same autosave-off setup, record supported/stress ordinary meal/accounting peaks and separate skip/reload latency arms. Read p50/p95/max complete mod-work, every >=30 ms spike with count/condition/component when attributable, whole-frame costs/stalls separately, actual body counts, units/second, maximum lag, withheld calendar time and catch-up duration. Require ordinary mod-work p95 <5 ms and individual spikes <30 ms; flag every spike >=30 ms; never use whole-frame p95 as mod-work or a low p95 to excuse nonprogressing demand. Compare zero-work ordinary/fast-forward/8-hour skip pacing against the offline old/new controls, including the remaining one-tick-per-minute skip floor; record native baseline pacing cost explicitly. Preserve unattributed stalls and any failed capacity envelope as unresolved evidence.
6. **Clock/authority interoperability and refusal — rig-automated (existing capability: RIG-MODSET DLL transactions, FT `state`/`load`/`time`, `shop_state`, `shop_log`; pending rig capability: final tick-prefix/Driven Rides observer trace, disposable bypass-clock fault arm and cold-restart/first-load stop-marker assertions).** Exercise configured factor 1 and 1/3 with the installed ride/run-until policies, ordinary fast-forward, pause, cold startup and teardown. Read the final positive/zero tick argument, inferred travel pace, native current hour, PC authority and saved receipts. Assert earlier caps/zero holds survive the final guard, sustained accounting caps never retime journeys, unsupported businesses remain native, ending-hour transfer remains safe and teardown restores clock ownership. Inject a bypassing clock writer only in the disposable fault arm and assert visible PC stop, immediate native-clock release, no new PC checkout, preserved population/receipts and protected partial-hour authority. Save/reload after refusal and assert the native city remains playable with PC stopped and next-boundary native demand resumes without duplicate orders. Before advancing time in any other city, verify a same-session checkpoint load does not reattach PC and the visible failure still requires restart. Quit the game, remove the fault, relaunch and load a synchronized unmarked pre-failure checkpoint to resume PC; separately cold-load the marked failed save and assert PC remains stopped while native play continues; verify cold population/route preparation explicitly reports calendar held.
