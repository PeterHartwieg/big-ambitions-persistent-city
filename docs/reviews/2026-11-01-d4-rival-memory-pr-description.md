# D4 base sync: preserve rival memory and D3a outflow history

Sync branch `codex/pc-d4-sync-base` merges D4 head `1be75e5` with
`origin/codex/persistent-city` (`fb958ae`, D3a). The original D4 implementation
and its historical measurements used source `eee4dff` on `c85dc19`. The free
fourteen-day outflow line/history and exact ledger reconciliation remain intact
alongside D4's existing B1 memory integration.


Schema plan: retain writer **24**, owned by D3a. The save-contract rules in
`docs/plans/steps-b-e.md` ("One owner per additive schema") allocate numbers at
shape freeze, forbid different formats under one number, and preserve published
version-specific readers. D4 adds no shape and was never merged on its old base;
its existing bounded scalar can therefore widen the current schema-24 contract
without schema 25. Genuine 21/22/23 still reject nonzero rival contributions.
The full 18 -> 19 -> 20 -> 21 -> 22 -> 23 -> 24 migration chain, frozen 13-17
readers, unsupported/foreign refusal and future 25+ refusal remain intact.

Fixture compatibility (Opus-r1-1): pre-D4 binaries, including the uncomposed
D3a writer, refuse saves with a nonzero rival `MemoryContribution`. Shared
dev/rig fixtures must be created and read on the combined head; no genuine
predecessor reader is widened to accept a D4 fixture.

Conflicts reviewed against merge base `c85dc19` on both sides:

| File | Resolution |
| --- | --- |
| `CHECKS.md` | Keep D3a, D4 and every existing index line; D4 stays a single link to its slice file. |
| `README.md` | Keep both D3a's free outflow line and D4's memory notice; use current writer 24 and merge rule v2. |
| `docs/roadmap.md` | Keep D2/D3a/D4 status and both D3a/D4 performance links, with all existing entries. |
| `Core/RivalFood.cs` | Keep D4 closure/served memory calls and D3a `TryPay(meal, state.Outflow, now)` together. One exact payment records its debit day before served memory; failure paths retain D4 neutrality. |

Clean automatic merges were also inspected: `SaveCodec.cs` keeps D3a's isolated
readers/converters/migrations plus D4's schema-aware validator call;
`RivalWire21.cs` needed a manual integration adjustment to move the nonzero gate
from 23 to 24. `RivalOutflowTests.cs` retains both sides' assertions and the
out-of-range scalar case; the plan table and regression tools retain both slices.
D4 tests now cover neutral-only schema 23, atomic refusal/preservation, and
combined memory/history under interrupted versus uninterrupted event budgets.

Runtime/Core scope: only `Core/RivalFood.cs` combines D3a and D4 behavior in their
shared settlement call; no other new runtime/Core behavior is introduced by
conflict resolution. D3a's other Core/Runtime changes are retained as approved.
Persistence only re-anchors D4's existing semantic widening on 24. The D4 runner
also guards Python wrappers that invoke dotnet and disables compiler-server reuse.
No performance algorithm changed during sync; historical measurements below are
kept as historical, with merged measurements recorded in the owning slice files.

Committed rival outings now use B1's existing watermark, record a modeled purchase
or known closure once, and apply the same trait-weighted return contribution as
own-shop visits. Captured nonzero rival contributions survive held-decision reload.
Unknown/mixed refusals and declines remain neutral; B inactive and pre-integration
commitments remain empty. Protected insertion keeps the documented equal-return
exception when a new failure would remove a competitor's positive memory.

Schema 24 permits the existing bounded rival scalar; genuine 21/22/23 sources retain
their neutral-only rule. D4 adds no DTO/tuple/collection/ledger/migration/schema growth, no native rival transaction,
extra route or history. Writer is 24 (D3a). Existing generation references and scalar
budgets are reused; actual experience content can fill the preallocated memory.
Rival inspector words explicitly identify modeled experience and unobserved native
service, without counting rivals as own-shop regulars. D2/D3a retain independent
acceptance and acquire no backward prerequisite.

Merged offline validation: **196/196 required foreground matrix steps pass**:

- `python tools/rival-memory-regression/validate.py`: **57/57**.
- `python tools/run-d3a-checks.py`: **63/63**.
- `python tools/run-d2-checks.py`: **76/76**.

Each passed the Steam-reference build, **2,556 unit cases** (zero failed/skipped)
and **2,494 listed test entries**; executed theory cases and listed names differ.
The matrices include every root regression project, all requested adapters,
demos, feasibility guards/comparisons, weather, nested continuity, capacity and
5,000-consumer choice variants. No integration failure required further code
changes. [Exact commands, log hashes, source audit and raw metrics](2026-11-01-d4-sync-base-results.json)
record all results. Production/test content is unchanged since merge `d6f883f`;
runner-only guard changes are `4b7ec96`. Every build produced DLL SHA-256
`82edbf8fe0c70c95736364109a278fb2cd54fc9f36c70d1f58052b82584e330f`.

Merged save controls are **2,446 B / 8 streaming / 8 semantic records** in all
three matrices, versus historical **2,422 B / 7 / 7**. The **+24 B / +1 record**
is D3a's empty `Outflow` property, shared by neutral/active/all-own controls;
D4's tuple remains 48 characters with four entries/references. Lookup/update p95
is **0.0003/0.0002 ms** in the first merged matrix, **0.0002/0.0002 ms** in each
repeat, with **0 B/call** and no D4 sample >=30 ms. Owning slice performance
notes preserve historical and current numbers separately; no performance
algorithm changed during sync.

Fresh positive aggregate counters include whole-20k awareness builds, E4 history
serialization, and two 20-call B2 panel-text batches >=30 ms in D3a's matrix
(2,500 residents /5,000 consumers; normalized per-call p95/max
0.918645/1.833810 ms; derived maximum batch 36.6762 ms). Counts, fixture
conditions and units are retained in the manifest. These are offline aggregate
operations, separate from native mod-work and whole-frame evidence. No parked
hotspot changed. All owned foreground runners exited; no worktree dotnet,
testhost or MSBuild process remains. Final fetch retains base `fb958ae` as an
ancestor, so the result includes the latest target base. No push or PR action.

Validation and measurements: [D4 checks](../checks/d4-rival-memory.md),
[performance](../perf/d4-rival-memory.md), [command receipts/raw metrics](2026-11-01-d4-rival-memory-results.json).
Historical pre-sync **59/59 foreground checks passed**, including production build, **2,540/2,540 unit
tests**, all 2,540 discovered cases, 44 root regression projects, the Python
adapters/demos and all five product-reader isolation guards. The runner prepares
E4's existing generated baseline inputs. Every dotnet test/run waits on the
shared host guard; no execution deadline or background worker. No owned test
process remains.

| Historical pre-sync D4 warmed operation | p50 / p95 / max ms | B/call |
| --- | ---: | ---: |
| Empty-memory lookup | 0.0001 / 0.0001 / 0.0033 | 0 |
| Active mixed four-entry lookup, with index | 0.0001 / 0.0002 / 0.0070 | 0 |
| Active mixed four-entry repeat update | 0.0002 / 0.0002 / 0.0058 | 0 |

10,000 warmed calls after 2,000 warmups; first insertion/setup excluded. No D4
sample >=30 ms. Retained entries/references remain four/four. Neutral/active
contributors and the all-own equal-width-ID control each serialize **2,422 B**,
**7 streaming / 7 semantic records** in the historical pre-sync fixture. The saturated memory tuple
stays 48 characters/51 inclusive JSON bytes; new experiences use its existing
allocation rather than adding a format or history. No retained provenance field.

Taken wins: completion-hours checks **1,464 -> 0 B**, p95 **0.0030 -> 0.0001 ms**;
player-generation rejection for inspector words **352 -> 0 B**, p95
**0.0002 -> 0.0002 ms**. Both before/after arms and limits are in the perf log.
These microbenchmarks are not a native mod-work or whole-frame result.

Historical pre-sync offline flags: whole 20k awareness builds have **1 dense/5 sparse >=30 ms**
operations, maxima **33.0878/56.8085 ms**; E4 four/eight-product history
serialization has **21/21 >=30 ms operations per arm**, maxima
**204.2245/148.8218 ms**. These aggregate construction/save operations are
separate from native frame samples; raw counters and units are retained.

The low-risk completion-hours improvement skips a temporary Place/opening-cache
tree, with equivalent advertised-window membership; arrival's next-close rules
remain. No new frame work. Four parked hotspots remain parked. Native combined
mod-work p95 <5 ms and all >=30 ms spike evidence remain pending the normal
combined session; whole-frame stalls are separate.

Native acceptance checks are deferred to the next batched combined rig / Peter's
playtest per merge rule v2, are recorded in `rig-2026-11-06-checklist.md`, and do
not gate merge. All locked-PC versus input-only markers below remain intact.

## In-game checks for 6 Nov rig

1. **LOCKED-OK** — In a disposable combined B1+D2 fixture with B active, retain
   two own and two rival memories for physical and represented consumers. Use
   bridge state dumps before/after repeated rival service; each modeled purchase
   increments the existing served count once and changes the captured return
   contribution. Reload at commitment, service, settlement and return boundaries;
   compare watermarks, generations, counts and exact outflow against uninterrupted
   replay. With B inactive, repeat and verify empty memory.
2. **NEEDS-INPUT** — Use normal fixture UI to induce a known rival closure after
   commitment and restore it for recovery; D4 must never write native rival terms.
   Prepare before-closure/closed/recovered checkpoints for the same generation.
   Verify the closure lowers its later return contribution and subsequent modeled
   service recovers it. If the native fixture cannot induce this sequence, record
   the unproved scenario explicitly rather than substituting offline evidence.
3. **LOCKED-OK** — Replay the prepared closure/recovery checkpoints from item 2.
   Dump memory before/after each outcome, save/reload at each boundary and compare
   with uninterrupted replay: one penalty or served increment per context. Check
   a full-memory protected insertion keeps competing boosts and commits only a
   failed watermark; changed-price/unreadable observations remain neutral.
4. **LOCKED-OK** — Compare bridge reads of rival native stock, cash, sales,
   customer/receipt state and advertised terms before/after both service and
   failure arms. Assert no native rival write or native rival receipt, unchanged
   own-shop accounting authority, exact resident-money conservation, four entries
   maximum and the existing 32-source/8-shop/16-route-slot envelope.
5. **LOCKED-OK** — During the normal combined session, record original autosave,
   disable and verify it before every measured idle/rush/soak window and again
   after reload. Keep manual saves outside windows; discard any window containing
   save activity. Compare active/empty rival contributors with matched setup:
   mod-work p50/p95/max, all >=30 ms spikes, allocations/retained records,
   route counts and catch-up lag. Require p95 <5 ms; track whole-frame stalls
   separately. Restore autosave and fixture state during cleanup.
6. **NEEDS-INPUT** — Open the resident inspector for a repeatedly served rival
   visitor and a closure visitor in the unlocked session. Visually confirm the
   modeled/native-unobserved wording is readable, own-shop words still describe
   native evidence, represented stories are not attributed to the visible anchor,
   and rival visits are not displayed as own-shop regulars.


LOCKED-OK is bridge-only feasibility, not completed native evidence. No game/rig,
bridge profile, ordinary save, lease, deployment, push or PR action occurred here.
