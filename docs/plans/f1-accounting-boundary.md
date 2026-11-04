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

A tick stops at a float-safe fence before the next integer minute. The fence uses the
hour's float spacing (2^-18 minutes), including after adding across 60 and subtracting 60.
At the fence, logical work drains through the preceding double absolute minute. Once ready,
the next tick may use its remaining allowance up to the **next minute's fence**, including
across an hour/midnight. It does not waste a frame on a one-ulp-only crossing. Events exactly
at the new boundary see the new epoch's inputs; historical sales remain forbidden.

Before closures, physical arrivals, retiming or BeforeSave observations, the runtime verifies
the ending epoch and calls `BeginAccountingObservations`. It publishes the current epoch's
integer timestamp in both city and traffic clocks, retaining queued events at that timestamp.
The existing midnight capability fact moves with the clock, so deriving a later midnight
cannot skip it. No citizen, meal, budget, traffic event or native transaction executes here.
Physical progress is rebased without crediting motion. A genuine unfinished prior event is
rejected before any promotion. Save validates again after its physical observations.

Maximum admitted logical backlog is **at most one game minute**. No skipped event, forgiven meal, historical stock image, altered
deadline, receipt replay or late accounting adjustment pays down backlog.

## Overload and recovery

Ordinary frames use elapsed-time allowances with the legacy per-frame floor: 32/256
citizen starts/total units and 4 soft ms at 30 FPS, 16/128/2 ms at 60 FPS, and **16/128/2 ms
at 120 FPS**. Thus 120 FPS retains its old 1,920 citizen starts and 240 soft CPU ms/second;
the pre-review halving is removed. At 30 FPS the 4 ms allowance leaves only 1 ms beneath the
complete 5 ms mod-work p95 gate; it is a capacity allowance, never proof of that gate.
Long-stall caps remain 128 starts, 1,024 units and 4 ms. Hidden skips retain 128 units/16 ms
as a separate latency condition. Individual nonpreemptible work remains F3's responsibility.

If due work remains, native calendar time is held at the current epoch while later rendered
frames drain it. Recovery resumes at the configured pace once the queue is drained; withheld
calendar time is **not** repaid with a fast burst. Wall duration may grow during overload.
There is no claimed upper bound on wall recovery if work stops making progress. Safe refusal
is preferable to discarding demand. Failed/nonterminating planners remain visibly held and
are F3's separate progress contract.

`AccountingDiagnostics` exposes current/maximum lag, ordinary/skip hold ticks, boundary clips,
withheld requested game minutes, current wait/maximum recovered wait seconds, configured
factor and refusal. The prominent notice appears only on workload holds or refusal and remains
for three seconds after the last workload hold, including repeated short holds. Ready fence
clips do not extend that timer. After the first fence cap, a quiet single-line label without
a background box points to Ctrl+F8; it uses constant text without the lag formatter. The panel
retains lag, lifetime holds, boundary caps and withheld requested minutes. Thus material skip
pacing remains visible while routine quantization cannot masquerade as workload backpressure.
Workload status text refreshes at 4 Hz. Constrained native samples cannot change inferred
travel pace. Diagnostic objects/strings are built only on request or for visible UI.

Startup installs the guard before detached construction and holds time through route
preparation. The preparing card explicitly says **calendar held**, including before a
simulation exists. A real lost input epoch refuses Persistent City, releases native time
immediately, and detaches the population/market on Update or save entry. The native game
clock is not held indefinitely by a latched refusal.

The runtime requests withdrawal of all known food scopes through the existing authority
manifest. The partially owned hour stays protected from duplicate native demand. After
ending work drains, the next native hourly boundary releases current/future demand, even
without an attached market and after reload. Original population bytes, receipts, wallets
and completed native orders are retained; no historical purchase or refund is fabricated.
The visible stopped message explicitly reports lost/unprocessed PC demand.

A native save after refusal remains playable **with Persistent City stopped**. Its newer
native clock and preserved older population snapshot do not authorize resuming that snapshot;
the bounded `persistentcity:accounting-stop_v1` marker prevents automatic PC restart on
the next load, including accidental clock equality, and leaves native time running. An
accounting failure stays latched for that save object. **Loading a different save object in
the same session clears only the accounting-specific failure** after scene loading finishes.
Old population, routes, pending initialization and cached UI are discarded before normal
RLO/payload/clock/ownership validation starts for the new save. An unmarked synchronized
checkpoint or another valid PC city may attach normally; it cannot inherit the failed city's
awaiting-population state. A newly loaded object for the marked failed save is refused before
initialization and keeps its archive/stop marker. Null unload gaps and loading scenes do not
clear failure; non-accounting failures retain their existing refusal. No attempt repairs past workload loss or reconstructs prior native
inputs. Unreadable/future authority manifests keep their existing conservative refusal.
`HistoricalClockMisses` remains a commerce-attachment diagnostic, not a persisted loss ledger.

Minute equality at the predecessor never proves draining. Validation checks pending route,
food, budget and traffic roots, including saved food timestamps before market attachment.
New-boundary observations are published after that proof, preventing legitimate arrivals
and reopenings from recreating old-epoch work. Schema 25 remains unchanged.

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
"caught up" flag. Failure intent alone is retained in the separate versioned
`persistentcity:accounting-stop_v1` marker; its presence (including an unknown value) stops PC
without replacing the archived population or receipts. This prevents unsafe automatic restart
after saving/reloading a failed native epoch. A synchronized pre-failure checkpoint has no marker.
No population schema-26 state or migration is needed. Older valid schema payloads
remain codec-readable; an out-of-epoch runtime snapshot is preserved and PC is stopped while native play continues.

## Evidence

[Dated checks and the automated rig handoff](../checks/f1-accounting-backlog.md) and
[measurements and limits](../perf/f1-accounting-backlog.md) separate the implemented contract
from pending native proof. F3's bulk posting, campaign refresh and route progress changes,
and all other city-audit findings, remain outside this slice.
F3 must preserve `IsCaughtUpTo` as a drain predicate for all pending work through its target,
even if it allows other events to progress while routes or postings remain pending.

R1 baseline pacing cost and the remaining minute-granular skip cost are quantified offline
in the [performance evidence](../perf/f1-accounting-backlog.md#r1-review-corrections).
