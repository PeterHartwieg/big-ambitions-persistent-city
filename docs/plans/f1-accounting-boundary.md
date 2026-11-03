# City-audit F1 accounting boundary

3 November 2026. Refs #88. Implementation base `bd99eb3a45d6b008911e1a5225ce1ced590cec51`.
This is the city-audit finding, separate from the older Track F shop-view experiment.

## Contract

Native inventory, customer orders, receipts and hourly/daily settlement retain sale authority.
An event must finish before native time closes its input epoch. The epoch is one **native
game minute**, nested inside the existing accounting hour. The configured calendar factor
is unchanged. The time machine retains its requested destination and does not apply the
ordinary calendar factor.

Before every native tick, the scheduler must have drained all work through the current
native observation, including same-minute events, held food decisions and pending routes.
Minute equality alone is insufficient. The final clock prefix caps positive time after
calendar/ride/run-until policies. It never runs the simulation inside a native tick.

A tick stops at the preceding representable **float minute-of-hour** before the next
integer minute. At that fence the logical target becomes the preceding representable
**double absolute minute**. All strictly earlier events therefore drain in the old epoch,
including events in the float rounding gap. Only then may a tiny tick cross exactly to the
integer boundary. Events exactly at that minute run with the new epoch's inputs. This also
protects meal/hour/midnight ordering without an arbitrary decimal epsilon or historical sale.
The logical clock may lead native time only across that final float rounding gap.

Maximum admitted logical backlog is **less than one game minute** (reported conservatively
as a one-minute bound). No skipped event, forgiven meal, historical stock image, altered
deadline, receipt replay or late accounting adjustment pays down backlog.

## Overload and recovery

Ordinary frames receive 960 citizen starts and 7,680 total units per unscaled wall second:
32/256 at 30 FPS, 16/128 at 60 and 8/64 at 120. Their soft advancement budget is 4/2/1 ms
respectively. Long stalls cannot accrue work debt: caps are 128 citizen starts, 1,024 units
and 4 ms per ordinary frame. Hidden economy skips retain 128 units/16 ms as a separate
latency condition. Budgets check between units and cannot preempt a native call; making
individual units bounded remains F3's work.

If due work remains, native calendar time is held at the current epoch while later rendered
frames drain it. Recovery resumes at the configured pace once the queue is drained; withheld
calendar time is **not** repaid with a fast burst. Wall duration may grow during overload.
There is no claimed upper bound on wall recovery if work stops making progress. Safe refusal
is preferable to discarding demand. Failed/nonterminating planners remain visibly held and
are F3's separate progress contract.

`AccountingDiagnostics` exposes current/maximum lag, ordinary/skip hold ticks, boundary clips,
withheld requested game minutes, current wait/maximum recovered wait seconds, configured
factor and refusal. A persistent screen notice appears on a workload hold and remains for
three seconds after the last hold, including repeated short holds; Ctrl+F8 also shows lag
and lifetime hold counts. Fence clips are separately counted, so baseline quantization cost
cannot be confused with workload holds; the panel also reports those caps and withheld
requested minutes. Visible text refreshes at 4 Hz. Constrained native samples cannot change inferred
travel pace. Diagnostic objects/strings are built only on request or for visible UI.

Startup installs the guard before detached decoding/population construction and holds time
through route preparation. A clock writer bypassing the guarded tick, or a loaded snapshot
outside the native input epoch, breaks the **synchronized input epoch** contract. Startup
refuses before binding/writing the payload; a running city latches a visible accounting hold
and stops advancement. Reloading a synchronized save is required. Minute equality at the
exact predecessor never proves draining.
Validation checks pending route/food/budget/event roots; before market attachment it also
checks saved food timestamps. New-boundary work is allowed after the earlier epoch drains.
Past workload loss and prior native worlds are not reconstructed. `HistoricalClockMisses` additionally
counts the existing Core stale-market refusal on its current commerce attachment; it is a
session diagnostic, not persisted history or a substitute for the runtime refusal.

## Timestamped world inputs and limits

An immutable city-wide historical inventory/input journal was evaluated and rejected: a past
snapshot cannot authorize a current native sale, and the native adapters do not provide a
transactional history for every stock/staff/price/weather/topology writer. Existing held
food contexts still capture their bounded replay inputs. Checkout always revalidates live
authority, prices, supply and receipts, and preserves uncertain acknowledgements.

The reproducible input contract is native-minute epochs: publish timestamped external inputs
at the boundary after the prior epoch drains and before events at the boundary run. Within
an epoch the external world must remain unchanged except for the simulator's ordered native
transactions. Offline replay changes the advertised price at fixed integer timestamps and
decrements finite stock in order. All cadence arms read the same timestamped inputs.

Arbitrary native/player writes while a continuation is held are still live interventions;
they may refuse a commitment at checkout under the existing rules. Exact replay for arbitrary
subminute writes, changing physical-motion inputs or a writer bypassing the guarded tick is
**not established** by this implementation. These are not invented historical input images.
Native hook order and input publication remain pending rig verification. If the rig cannot
establish that contract, F1 remains refused for that workload; offline replay is not a pass.

The offline supported envelope is the reproducible 957-resident / 1,914-consumer fixture:
one always-open owned shop, one finite-stock product, three meals, household postings,
minute-stamped price changes, one-minute walking routes, no physical bodies or native
campaign/route queries. A 2,000-resident / 4,000-consumer case is stress evidence only.
This specifies the tested workload, not a native hardware capacity claim. Wider native
shop/menu/route/body capacity must be measured by the combined rig with F3 as applicable.

## Save contract

Writer **25** is unchanged. The native clock plus existing `State.Minute`, event timestamps,
held contexts, excursions, wallets and receipts fully determine the drain target after load.
Save/reload may interrupt the rounding-gap drain, a decision, an excursion or midnight work.
Transient hold counters/timers restart; readiness is recomputed, never restored from a stale
"caught up" flag. No schema-26 state or migration is needed. Older valid schema payloads
remain codec-readable; an out-of-epoch runtime snapshot is preserved and refused.

## Evidence

[Dated checks and the automated rig handoff](../checks/f1-accounting-backlog.md) and
[measurements and limits](../perf/f1-accounting-backlog.md) separate the implemented contract
from pending native proof. F3's bulk posting, campaign refresh and route progress changes,
and all other city-audit findings, remain outside this slice.
F3 must preserve `IsCaughtUpTo` as a drain predicate for all pending work through its target,
even if it allows other events to progress while routes or postings remain pending.
