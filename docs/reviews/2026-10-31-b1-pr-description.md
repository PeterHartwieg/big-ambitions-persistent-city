# B1 prerequisite: reproduce missing schema-18 replay state

**B1 is stopped, not implemented.** Scope item 1 requires stopping when needed
state is missing from B0's published contract. At base `192037b`, schema 18 has
the memory/watermark/reference allocation but no held decision memory/config
image or independent B migration activation day. This branch preserves that
contract and records a reproducible prerequisite report.

The synthetic `tools/b1-contract-probe` uses the actual codec/session: seed 2's
saved evaluating context chooses burger with captured taste weight 0, but
croissant when reloaded with taste weight 3. Unchanged-config replay matches;
both make two routes and no checkout calls. Proposed held `MemoryInputs` and B
`StartsDay` properties are rejected as unknown fields, preserving the source
string reference. A 64-character size reservation is not an extension grammar.

See [contract gap report](2026-10-31-b1-contract-gaps.md) and the dated CHECKS
entry for commands/results. No production code or save-contract regression is
changed. No B2 UI, settings, new behavior, schema number or rig work is included.

Validation: **1,832/1,832 tests**, **1,832 discovered cases**, **all 31 requested foreground commands plus the contract probe pass**. Production DLL SHA-256 `b91e6029e59c56b2031bdbab3bff9d32ee2fe690097cf84b3c53cee2ece47a1e` matches B0 exactly. [Raw command manifest/probe output](2026-10-31-b1-offline-checks.json).
B1 performance (lookup/update, saturated reference/5,000 consumers, rush/lag,
retained memory/allocations and saved bytes) remains unmeasured. No native gate
clearance or performance win is claimed; the four parked hotspots are untouched.

### In-game checks for 6 Nov rig

**Pending until B1 is implemented.** Use a `rig_test` session (Codex Sol, high),
named disposable FT copies, frozen baseline/candidate hashes, dependency versions,
effective settings/revision, seed, checkpoint and matched clock/body/graphics/
population/shop setup. The [locked feasibility report at 84be71a](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/84be71a/docs/reviews/rig/2026-10-31-locked-session-feasibility/README.md)
supports bridge reads, reload/accounting and timing while locked; it does not
prove arbitrary pointer input. No rig activity was performed in this task.

1. **LOCKED-OK:** On a prepared disposable FT fixture, select matched loyal/curious cohorts by saved trait bands, including represented consumer slots. Observe several served days with the same eligible shops/prices. Dump each stable consumer's generation references, last experience day, served count, effective/raw affinity, actual cause and watermark; compare return/novelty rates over multiple seeded checkpoint trials, without forcing winners or extra route requests.
2. **NEEDS-INPUT:** Prepare/visually verify an induced stockout, closure and excessive queue at a player shop using the unlocked 6 Nov session. Record actual commitment/admission and the named failure evidence. Verify that distant candidate rejection, unaffordability, unknown supply, lost routes and schedule interruption do not create bad-service memory. This item waits for unlocked input unless a separately verified bridge fixture action becomes available.
3. **LOCKED-OK:** From those prepared failure checkpoints, dump committed memory and run recovery over days 3–6. Compare matched loyal/curious and patient/impatient/time-pressed consumers; stockout must never produce a positive gain, bad visits must lower return weight relative to the same pre-visit image, confirmed recovery applies once, and logical-day decay/expiry is lazy.
4. **LOCKED-OK:** Dump before/after coordinated save/reload at captured/evaluating, selected, committed, reached and uncertain-receipt boundaries, including a late confirmation after midnight. Replay the same checkpoint with different event budgets and unchanged config; require identical choices/memory, original business generation, watermark progression and no duplicate served count. Repeat with edited config: held contexts retain captured inputs; the next uncaptured decision uses the new values that same day. Migration activation remains on the saved next-day boundary after repeated reloads.
5. **LOCKED-OK:** Reconcile a full native day, including refusal/uncertain/late receipt cases: native orders, stock/bags, resident exact spending/holds, business cash/accounts, wages/expenses and receipt counts. Require no extra or missing native transactions, no retry of an uncertain checkout, unchanged ownership boundaries and no B1-added route requests.
6. **LOCKED-OK:** Load untouched pre-B1 disposable saves through normal migration, preserving their originals; confirm empty memory, IDs/traits/accounts/journeys unchanged, migration activation on the next logical day, old commitments finishing their captured terms and reload continuity. Lower capacity on a saved four-entry consumer and verify loading does not trim it; normal insertion/expiry handles replacement with the documented tie order.
7. **LOCKED-OK:** Interleave matched ordinary-play idle and breakfast/lunch/evening rush windows with memory active versus `FoodMemoryWeight=0`, including warm/cold inputs and a soak. Record original autosave, disable it and verify off before every window and after each reload; keep all saves outside measured windows and invalidate any window with save activity. Report combined mod-work p50/p95/max, sample count, every spike >=30 ms with condition/component, route/query counts, allocations/GC, pending work, throughput and catch-up lag. Require mod-work p95 <5 ms and single spikes <30 ms; report whole-frame p50/p95/max/stalls separately. Record matched saved characters/UTF-8 bytes and B0 allocation headroom outside the timing windows. Restore original autosave/profile files/preferences and fixture changes during cleanup; attach before/red -> after/green logs/dumps/captures.
