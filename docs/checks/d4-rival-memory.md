# D4: optional modeled rival-experience memory

Native acceptance checks are deferred to the next batched combined rig / Peter's
playtest per merge rule v2, are recorded in `rig-2026-11-06-checklist.md`, and do
not gate merge. Pending evidence still includes native memory replay, rival
nonmutation, mod-work p50/p95/max (p95 <5 ms), every spike >=30 ms with
count/condition/component, and separate whole-frame timing/stalls. Offline
checks and microbenchmarks do not establish native acceptance. No rig work was
performed in this integration task.

## 1 November 2026 — offline implementation

Base `c85dc19`; branch `codex/pc-d4-rival-memory`. D4 composes the existing B1
and D2 contracts. A committed rival outing now carries B1's existing watermark;
successful modeled settlement or a generation/place/price-matched known closure
updates the same four-entry consumer memory atomically. The existing terminal
watermark rejects repeats and older contexts even after decay, eviction or reload.
Pre-integration commitments without that watermark finish neutrally. B1 inactive
keeps entries and watermark empty. No D2/D3a prerequisite is added.

Rival choice captures B1's existing contribution before shortlisting and holds
that scalar across reload/tuning edits, just as player choices do. The current
rival validator permits that already bounded scalar in current schema 24;
schema-21/22/23 source validation retains its neutral-only rival rule. Tests exercise
those genuine predecessor writers and refusal paths, plus finite endpoints and
nonfinite/out-of-range refusal at 24. The malformed-scalar test now uses 9 rather
than valid 1.
Pre-D4 binaries, including the uncomposed D3a writer, refuse D4-written saves
holding a nonzero rival `MemoryContribution`, so shared dev/rig fixtures must be
created and read on the combined head (Opus-r1-1).
D4 adds no DTO, tuple slot, collection, ledger, schema, migration, capability
revision, route request, native rival write or outcome history. The merged
writer is **24**, owned by D3a's outflow shape; the complete chain remains
18 -> 19 -> 20 -> 21 -> 22 -> 23 -> 24. Frozen 13-20 readers are untouched;
21/22/23 retain their original neutral-only rival scalar gate. D4's unmerged
semantic widening is re-anchored on 24, not backported into genuine sources.
The save contracts allocate numbers for new shapes and require exact predecessor
validation; this existing finite [-8,8] slot adds no shape requiring schema 25.

Known advertised closure is the only rival failure cause: no native stockout or
queue is invented. Missing/unreadable reads, ownership/generation/place changes,
changed prices, changed hours while still open, lost funds, route/schedule loss
and household declines stay neutral. The same detail read D2 already performs
supplies closure evidence. A personal deadline failure does not become a closure.
Modeled settlement remains exclusively own-ledger spending.

B1's protected-insertion exception is preserved unchanged: when a new failure
would evict a competing positive habit/admission, only the terminal failed
watermark is saved. The failed shop keeps its previous novelty/return odds;
there is no negative entry or persisted failure diary in that exceptional case.
Mixed own/rival entries share the existing decay, expiry, capacity and ordering.

Inspector words explicitly say **modeled** and **native service unobserved**.
They recognize D1's complete six-field escaped rival signature; player authority
adds `:generation:N`, so a retired player scope is never inferred to be a rival.
Words use retained evidence, including historical rival generations, without
native reads. Rival memories never become free own-shop regular counts. Formatting
and identity parsing run only for the open inspector/explicit diagnostic.

### Offline validation

Initial focused integration checks: **74 passed**, including D2's retained independent
checks, with no failures/skips. The final full suite includes **27 D4 cases**
(the original 24 plus three current/predecessor scalar-validation cases). New
cases cover all continuation/service/return reload boundaries, represented consumers, arrival/completion closures, closure
from advertised hours, household declines, neutral refusals, old commitments,
identical full snapshots under budgets 1/128, captured nonzero contributions,
mixed eviction/decay, late replay, protected insertion and honest words.

Historical pre-sync source **`eee4dff`** passed **59/59 foreground acceptance commands**:
Steam-reference build, **2,540/2,540 unit tests** (zero failed/skipped), **2,540
discovered cases**, all **44 root regression projects**, four Python adapters,
three demos and all five product-reader isolation guards. E4's existing frozen
baseline preparation ran first; its initial missing-generated-file failure was
a runner prerequisite error, resolved without changing E4 or gameplay code.
The complete final batch reran after the schema-source safeguard and UI allocation
win, so earlier mixed-head receipts are not used as final evidence.

[Commands, output hashes, source-preservation audit and raw metrics](../reviews/2026-11-01-d4-rival-memory-results.json)
identify every final run. Reproduce the 56-command main matrix with
`python tools/rival-memory-regression/validate.py`; the three extra isolation
commands are listed in that manifest. Raw logs remain in ignored
`build/d4-validation/`. Production DLL SHA-256:
`96826062e2ec7c0805a93c6fad0f1e63461085d2b736f87d54116ee1db0c4f52`.
All owned foreground commands exited; no process command line points at this
worktree. Other worktrees' processes were left untouched.
No game, rig, lease, bridge profile or ordinary save was accessed. Game DLLs were
read-only compiler inputs. All dotnet test/run commands use the shared wait guard,
run in the foreground and have no execution deadline.

### Performance and retained format

The reproducible Release harness is `tools/rival-memory-regression`. It times
10,000 warmed individual operations after 2,000 warmups; setup, serialization,
first insertion and index construction are outside those timers. The mixed
fixture contains two own and two rival entries, not another memory collection.
The empty lookup has no retained entries; the active lookup includes generation
index lookup and the four-entry policy. Measurements are synthetic .NET 10,
separate from native mod-work and whole-frame timing.

Historical pre-sync warmed results at `eee4dff` (milliseconds per call):

| Operation | p50 | p95 | Max | Allocation |
| --- | ---: | ---: | ---: | ---: |
| Empty-memory lookup | 0.0001 | 0.0001 | 0.0033 | 0 B |
| Mixed four-entry lookup, including index | 0.0001 | 0.0002 | 0.0070 | 0 B |
| Mixed four-entry repeat update | 0.0002 | 0.0002 | 0.0058 | 0 B |

No D4 micro-sample is >=30 ms. Timer resolution and shared-host/JIT noise limit
interpretation at this scale. Historical pre-sync saturated fixture bytes were **2,422 / 2,422** for
neutral/active contributors and **2,422** for the all-own equal-width-ID control;
streaming/semantic records are **7/7** in every arm. Four retained entries and
four shared references stay fixed through 12,000 repeat updates.

Full four-entry memory remains 48 ASCII characters (51 inclusive JSON bytes),
40 more than empty memory, within B1's existing allocation. References and held
nonzero scalar text consume their existing bounds; there is zero new format
overhead, not a claim that recording an experience changes no saved content.
The same saturated fixture with the contributor neutral/active has identical
bytes and record counts; an all-own control with equal-length generation IDs has
the same size/counts as mixed own/rival memory. No retained provenance field or
replay-ID set is added.

The low-risk completion win removes the temporary Place/opening-cache tree:
**1,464 -> 0 B/check**, p95 **0.0030 -> 0.0001 ms**; arrival still uses the general
union's next-close boundary. Player-generation rejection for open-inspector words
also avoids splitting fields: **352 -> 0 B/check**, p95 **0.0002 -> 0.0002 ms**.
These are isolated allocation wins, not matched native frame speedups. [Before/after measurements and limits](../perf/d4-rival-memory.md).
No per-frame reflection, polling, catalogue scan or new frame participant; the
four parked hotspots remain parked.

The native combined gate remains pending: **mod-work p95 <5 ms**, flag every
mod-work spike **>=30 ms** with count/condition/component. Whole-frame costs and
stalls are recorded separately and cannot substitute for mod-work p95. No
special performance-only rig run is proposed.


Broader offline timing flags, separate from D4's zero >=30 ms samples: the
20,000-alternative awareness **whole-catalogue construction** has **one dense
sample, max 33.0878 ms**, and **five sparse samples, max 56.8085 ms**. E4's
standalone four/eight-product **history serialization** has **21/21 operations
>=30 ms** in each arm, maxima **204.2245/148.8218 ms**. These are aggregate
construction/save operations, not measured native frame samples. The manifest
retains each counter and its p50/p95/max; no native attribution or new gate pass
is inferred. The four parked hotspots are unchanged.

### In-game checks for 6 Nov rig

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

LOCKED-OK denotes bridge-only feasibility, not completed native proof. These
checks require the combined fixture/session; this offline task performs none.
Classifications follow the [locked-session feasibility report at 84be71a](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/84be71a/docs/reviews/rig/2026-10-31-locked-session-feasibility/README.md),
read through Git; no UI capability or fixture mutation helper is presumed.


## 1 November 2026 - round-one review follow-up (offline only)

Sol-r1-1: native acceptance is deferred to the next batched combined rig /
Peter's playtest per merge rule v2, recorded in `rig-2026-11-06-checklist.md`,
and does not gate merge. The before/red and after/green proof remains pending.
Opus-r1-1: the fixture compatibility note requires shared fixtures to be created
and read on the combined head. Neither change alters code, schemas or downgrade
behavior; no native evidence is claimed and no rig work was performed.

Fresh review-fix validation: **40/40 required foreground checks pass**, including
the game-reference build, **2,540/2,540 tests**, **2,540 discovered cases**, all
requested regression tools, Python guards and demos. The prescribed host guard
preceded every dotnet test/run, with no execution deadline. Production sources
are unchanged from `9106790`; the built DLL retains the SHA-256 recorded above.
Raw command receipts/log hashes are in ignored `build/d4-review-r1/results.json`:
`df3862f809c320f2657b263a6de901468b69e0a0c8ea88a9d18e63c4c4893042`.

Fresh offline aggregate flags: 20k whole-catalogue awareness construction has
**two dense samples >=30 ms, max 34.4564 ms**, and **one sparse sample >=30 ms,
max 30.9823 ms**. These are whole catalogue builds, not native mod-work frames;
they do not replace the historical measurements or satisfy the native gate.


## 1 November 2026 - D4/D3a base sync (offline)

Two-parent merge `d6f883f` retains D4 `1be75e5` and D3a `fb958ae`.
Schema 24 remains D3a's writer; D4 widens only its existing bounded scalar.
Genuine 21/22/23 remain neutral-only, including refusal without replacing the
source; every predecessor migration and future-schema refusal remain intact.
The paired source audit retains all checks/performance index entries and D4's
numbered in-game checks, including those in the PR description. D3a history,
wire, UI, tests and regression runner are retained unchanged. The only Core
combination is D4's memory outcomes around D3a's history-aware payment call.
No other runtime/Core behavior changes were needed.

First merged foreground matrix on source `503b57b`: **57/57 commands passed**,
including Steam-reference build, **2,556/2,556 unit cases** (no failures/skips),
**2,494 listed test entries** (theory discovery and executed cases differ), all
**45 root regression projects**, four Python adapters, three demos and the
specified source guards. There are now **28 D4 cases**, including schema 23's
original neutral-only rule and combined three-consumer memory/outflow replay.
The D3a/D2 wrapper receipts and complete command/log hashes are recorded in
[the sync manifest](../reviews/2026-11-01-d4-sync-base-results.json).

The first merged D4 fixture is **2,446 B / 8 streaming / 8 semantic records**
for all three equal-width-ID controls, versus historical **2,422 B / 7 / 7**.
The **+24 B / +1 record** is D3a's empty `Outflow` field, present identically in
neutral/active/all-own arms. D4 adds no format overhead; four entries/references
and the 48-character memory tuple remain fixed. Mixed lookup/update p95 is
**0.0003/0.0002 ms**, still **0 B/call**. Historical and merged measurements,
including every component max/spike counter, are kept separately in the
[owning performance note](../perf/d4-rival-memory.md) and sync manifest.
Native acceptance remains deferred under merge rule v2 as stated above.
