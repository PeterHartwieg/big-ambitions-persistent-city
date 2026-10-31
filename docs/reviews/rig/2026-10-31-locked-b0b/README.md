VERDICT: PASS

# Combined B0 + B0b locked-session rig proof

31 October 2026. Role: `rig_test`. This is the combined merge-gating proof for PR #9 B0 and stacked PR #16 B0b. B0 was not tested alone. Candidate `c0af745` remained the current remote head when fetched and verified. The requested locked-capable gates are covered below. This evidence grants no ordinary-save deployment or release approval; no merge or PR action was performed.

| Check | Result | Evidence |
|---|---|---|
| 1 Setup / provenance | PASS | Fresh coordinated A3 schema-17 disposable checkpoint, native/payload hashes before and after migration; original retained source untouched. [Builds](tested-builds.json), [fresh-source preservation](supplement-source-preservation.json), [source integrity](source-integrity.json). |
| 2 Migration / rejection - gating | PASS | Exact decoded graph across 17->18 and both stored reloads, including identities, traits, assignments, sequence, optional stops, exact budgets, visits, Today and latches. B inactive; references/memory empty. Native receipt/authority formats unchanged. Future/active-B/malformed-tuning copies rejected and preserved. [Migration](migration-summary.json), [contracts](native-save-contract.json), [rejections](refusal-summary.json). |
| 3 Accounting - gating | PASS | Same 42 day-6 purchases and 11,559,303,168,000 exact units; all native observations and every consumer money match, including cash bits and native revenue rounded once. Reload causes no settlement replay. [Initial boundaries](native-migration-accounting.json), [midnight](settlement-summary.json). |
| 4 Interrupted decisions / journeys | PARTIAL | Actual evaluating, partial-quote, selected, outward, serving, paid and returning saves/reloads. Target purchases exactly once and returns home. Naturally uncertain/late receipts across midnight were unavailable. [Held contexts](held-config-summary.json), [journeys](journey-summary.json). |
| 5 Ordinary 32 retained visits | PASS, bounded coverage | Two of 957 residents reached 32; all 1,914 consumers retained. Stored save/reload graph is exact. Four native days advanced without injected history; no claim that every resident reached the limit. [History](history32-summary.json). |
| 6 Save profiling / capacity | PARTIAL | Three in-game samples per reference operation, native file sizes, UTF-8/character/record counts and external memory samples. Native 5,000-consumer/ceiling/64-visit and actual 95%-warning fixtures were unavailable. [Profiles](save-profile.json), [latency flags](operation-latency.json), [size](history32-summary.json). |
| 7 Normal-play frame work - gating | PASS | Interleaved control/candidate/control/candidate, >=60 s each, matched reference fixture/settings and clock phases. Autosave off and verified; no save in any window. [Raw-derived distributions, spikes and system load](performance-summary.json). |
| 8 Config-edit replay - gating | PASS | Taste 0->3 and household weight 0.1->5 changes the A3 held choice from croissant to household food. Candidate retains old exact tuning/croissant; a new context the same day captures 3/5. Cursor 1->2, exactly one remaining walking request, no checkout during either decision continuation. [Decoded saved images and counters](held-config-summary.json). |
| 9 B0b continuity boundaries | PARTIAL | Captured tuning, quotes, terms, payment, return ownership and budgets preserved at reachable boundaries. Paid/return reload does not checkout again. No naturally uncertain receipt was available; no payment failure was injected. [Journeys](journey-summary.json). |

B0/B0b add no UI. There is no new input-only check to clear. All actions use bridge/native seams; no pointer, hover, focus or arbitrary-position gesture proof is claimed. The console remained locked: [initial](session-initial.json), [final](session-final.json).

## Performance

Mod work is the same-frame Update/native-authority union **plus whole PopulationRuntime.OnGUI**. Nested component times are not added. Other hook bodies outside those scopes remain outside this measurement. Percentiles use floor(n*q). Whole-frame timing includes the game, rendering, RLO and other work and is informational.

| Order / condition | Seconds / mod frames | Mod work p50 / p95 / max ms | >=30 ms mod spikes | Whole frame p50 / p95 / max ms | Whole stalls >50 / >100 / >250 ms |
|---|---:|---:|---:|---:|---:|
| base1 / idle / early morning | 60.114 / 5034 | 0.370 / 0.577 / 18.486 | 0 | 11.758 / 16.846 / 93.382 | 1 / 0 / 0 |
| cand1 / idle / early morning | 60.249 / 5352 | 0.493 / 0.636 / 4.109 | 0 | 11.180 / 12.762 / 64.666 | 1 / 0 / 0 |
| base2 / breakfast rush | 60.244 / 5325 | 0.577 / 0.747 / 19.570 | 0 | 11.287 / 13.232 / 46.327 | 0 / 0 / 0 |
| cand2 / breakfast rush | 60.241 / 5298 | 0.661 / 0.855 / 22.481 | 0 | 11.179 / 13.234 / 63.929 | 2 / 0 / 0 |

Every mod spike >=30 ms is flagged with window, Unity frame, event branch/minute and available inclusive components in the JSON. No such spike occurred.

| Window | Start CPU % | dotnet / testhost / codex | Available RAM GiB | Under test/build load |
|---|---:|---:|---:|---|
| base1 | 60.1 | 4 / 1 / 6 | 22.47 | True |
| cand1 | 33.8 | 6 / 1 / 12 | 27.11 | True |
| base2 | 35.6 | 5 / 1 / 12 | 22.02 | True |
| cand2 | 27.9 | 1 / 0 / 8 | 29.91 | True |

Before every window the rig waited up to 15 minutes for external dotnet/testhost processes. Remaining/restarted load is recorded, including total CPU, top processes and RAM before/after each window. Other processes were inspected only. These samples establish the stated absolute frame-work gate; they do not isolate a causal FPS change between builds.

## Save size and latency

| In-game operation, three samples | Median write ms | Median read / migration ms | Payload characters / UTF-8 bytes |
|---|---:|---:|---:|
| base-reference-profile | 353.003 | 687.175 | 6,999,361 / 6,999,362 |
| candidate-migration-profile | 291.252 | 987.523 | 5,322,114 / 5,322,115 |
| candidate18-profile | 305.992 | 607.724 | 5,322,114 / 5,322,115 |

Actual compressed native files are 1,271,599 bytes for the A3 reference and 1,197,682 bytes for its first schema-18 save. Later checkpoint sizes and native hashes are in [the native-size ledger](native-save-sizes.json).

The source schema-17 payload read for migration is 6,999,368 UTF-8 bytes. The A3 live post-reload encode in the table is six bytes shorter because its clock value advanced slightly; source decode comparisons remain exact.

Every listed codec sample is >=30 ms and is flagged as separate save/startup/reload latency. Native coordinated save/load wall times and diagnostic encodes are separately listed in [the operation ledger](operation-latency.json). No save occurred within a primary performance bracket.

The native one-shop held image adds **230 structural bytes** to the B0 shape: 205 for tuning and 25 for its neutral per-shop contribution. The documented eight-shop offline reference adds 405. This run does not claim a native B0-only binary or eight-shop/5,000-consumer size comparison. No collection record is added by these scalar fields.

The history checkpoint is **5,626,030 UTF-8 bytes**, 48,078 streaming/semantic records, **2.096%** of the 268,435,456-byte cap. The 95% byte threshold is 255,013,683.2 and record threshold 3,800,000. Neither was reached; warning presentation and ceiling refusal remain NOT-RUN here.

External Windows memory was sampled every 50 ms for all three samples. The managed process API returned zero working-set values, so those are marked unavailable. Sampled RSS and observed lifetime Windows peak values, plus managed heap/GC counts, are in the profile JSON; these are not forced-GC retained-graph allocations.

## Tested builds and scope

- Control: `754c2b67610e69a42aee65a342b1e2a403530a25`; DLL SHA-256 `29ef76b33deb22f499cd1aff02b8507d26a0f19cf7a16fa38568eb75706e5cdd`.
- Candidate: `c0af745b044d9c53d6e0b59f9fd12da0b61fc4fd`; DLL SHA-256 `34bbcf02fded476c79b58e84e3a005af38b979694435994e4f9220d2ce73a246`.

Both were built from unchanged source against `C:/BigAmbitions-AgentTests/Big Ambitions Agent Tests_Data/Managed`, with the per-user dotnet directory prefixed to PATH. No offline dotnet suite ran. Loaded assembly hashes and installation receipts are included in provenance. RLO is unchanged, version `0.45.0+b12d3581860ce380397223a2790fcce8b72b3482`. Unity 2022.3.62f2, D3D11/RTX 4070, Low/1920x1080, target 90 FPS, vSync 0, background execution enabled; 957 residents, 1,914 consumers, 1,059 places, one owned food shop, seed -1903163055, one-third calendar. Normal visible windows were used.

The native live continuation is not bitwise deterministic across independently loaded scenes. The paired midnight financial observations match exactly, while nonfinancial history counters and native geometry/timing values differ. Stored-source migration/reload comparisons are exact. [Native replay comparison](native-variation-summary.json) retains the counter drift; no stronger live determinism claim is made.

Tooling-only corrections and excluded attempts are recorded in [tooling notes](tooling-notes.json). In particular, accelerated bridge calendar speed is distinct from a normal-clock checkpoint; the gating replay uses normal clock speed throughout. Diagnostic mailbox IDs and serialization were corrected in the stage only. Production code and mod behavior were not patched.

## Logs and cleanup

[Complete-log review](log-review.json) distinguishes intentional refusal messages from unexpected mod faults and native game exceptions. The observed native outdoor music/hangout/lamp exception stacks contain no Persistent City frame and remain disclosed.

Cleanup: restored **4828 / 4828 profile files**, **107 / 107 original install references**, original configuration, preference export/autosave and root files; SHA-256 verification passed. All owned games quit normally; no game kill was needed. Lease released with held=false/pending=0; no owned game/build/measurement/sampler command remains. [Restore, hashes and release](cleanup-summary.json).

No Windows setting, session connection, Steam/Workshop, normal profile/save, RLO source, other process, other harness worktree or production source was changed. Only this evidence folder is committed on `codex/pc-rig-locked-b0b`. Raw saves, DLLs, full private logs, backups and lease material remain private.
