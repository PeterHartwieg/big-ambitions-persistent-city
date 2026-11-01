# Sync B0b with C3 and the per-slice documentation layout

Target: `codex/pc-b0b-held-tuning`. Land this ancestry sync with a **merge commit**;
PR #16 subsequently squash-merges into `codex/persistent-city`. Preserve this
ancestry for B1 #17, C2a #24 and C4 #30 to sync afterwards.

Merge development `8c24f24` (C3 #20 at `c6d7535`, docs split #41) into B0b
`eee1cfb` (first sync #40), with common ancestor `3b8f7ec`. Both merge-base diffs
were read and retained in ignored `build/b0b-sync2-base-gate/`. There are exactly
four conflicts, all documentation; code, test project, save contract, migrations
and regression tools merge without conflicts.

| Conflicted file | Resolution |
|---|---|
| `CHECKS.md` | Keep the development index and every existing slice file unchanged. Move both B0b checks sections, including first-sync evidence and the complete five-item native checklist, into `docs/checks/b0b-held-tuning.md`; add exactly one B0b index line. Rebase local links. Preserve all locked/input qualifications across all eight native checklists. |
| `README.md` | Keep the new concise development README. Move both B0b dated opening notices into the owning checks file, alongside its evidence. Use the development split's corrected B0/A3 checklist destinations, retaining the first sync's intended targets without obsolete numbered root anchors. |
| `docs/roadmap.md` | Keep the B0b prerequisite/status in normal planning prose. Move the B0b first-sync notice into its checks file and its complete measured performance row into `docs/perf/b0b-held-tuning.md`, with one performance index line. Keep every development planning item, shared performance gate, existing slice index and moved B0 notice/row. |
| `docs/plans/e2-street-life.md` | Use the split's E2a checklist link in place of the first sync's old root `-3` anchor; both sides intend the same preserved E2a checklist. Keep the other split link repair and all plan text. |

**No B0b Runtime/Core/Persistence code changed. No integration fix was required.**
Every nonconflicted B0b file is unchanged from `eee1cfb`; every nonconflicted
incoming file is unchanged from `8c24f24`. The only executable additions relative
to B0b are C3's three production files, unit-test file and three regression files,
all identical to development. C3 remains inactive/refusing, with no gameplay
construction or persisted agency order/entitlement. No new behavior is introduced
by conflict resolution. The tests project and all existing tests/tools are intact.
The historical PR #16 retarget sentence is in a nonconflicted file and remains
untouched.

Schema remains **18, amended in place while unshipped**. Development adds no
Persistence/DTO/schema changes after `3b8f7ec`, and claims no competing schema-18
field or schema 19. Preserve the twelve-coefficient tuning image, strict shape
and semantic validation, refused-source preservation, schema-17/pre-image-18
live-once capture, frozen 13–17 readers, B1 neutral log-contribution reservation,
one-held-context bound and the **405-byte** eight-candidate ledger charge.

**Validation: 2,002/2,002 unit tests**, zero failed/skipped, **2,002 discovered
cases**, including all 30 B0b tuning cases. **All 50 sequential foreground checks
pass**: production Steam-reference build; tests/discovery; all 27 regression
projects, including C3, held tuning, native candidates and pickup; all four
required Python harnesses; three demos; six capacity fixtures; base recovery,
input/record boundaries and history stress; choice rush and optional-stop
performance/findings. The prescribed other-dotnet guard precedes every command.
No execution deadlines or background build/compiler-server reuse.

All 13–17 migrations, schema-18 round-trips, frozen-reader refusals, legacy
live-once capture, held replay and refused-source preservation pass. The actual
eight-candidate image still adds **405 bytes**, charged exactly once against the
existing aggregate header allowance without new collection records. All six
capacity wire hashes, character/UTF-8 sizes, record counts and future reservations
match the previous B0b sync. Reference **5,013,141 bytes**; 5,000 consumers
**9,701,305**; 5,000 residents **17,298,040**; full-memory/table ceiling
**141,087,195**. Complete CF32/CF64 budgets remain **220,664,366 (82.204%) /
259,064,366 (96.509%) bytes**, **2,111,867 / 3,711,867 records**. CF32 passes 85%;
CF64 passes hard caps, retaining its >95% warning and no 85% claim. Exact
268,435,456-character/UTF-8 and 4,000,000-record boundaries pass; oversized sources
and one-over cases preserve original text/reference. The 280,000-generation
history stress retains all history/product totals. Base recovery remains
**115,049,515 characters**. Basic save bytes (**119,083 characters**), refusals and
economy/reload digests match the previous sync: **849 orders, 797 purchases, two
unconfirmed, 107,967,424,757,760 spent units**. Twelve F0 off/on transitions retain
identical saved bytes.

The preservation audit checks all **488 historical B0b-head checks blocks** and
**464 merge-base checks blocks**, all dated README notices and all performance
rows, with unchanged non-link text. Both B0b checks sections and moved notices/row
occur exactly once; every nonconflicted incoming slice file matches development.
All eight native checklists and their qualifications remain. Local Markdown
files/headings pass the link audit.

**Offline timing limits:** recorded capacity write/read/migration operations of
at least 30 ms are flagged individually in the results as separate synthetic
save/load/startup latency. D1's **48.8884 ms cold 20,000-place aggregate across
bounded units** is flagged separately from a native frame. Native-ID 1,024-scope
pure-policy p95 is **0.258 ms**, above the existing 0.20 ms engineering target;
full-decision/continuation maxima are **9.8594/3.3498 ms** across that harness.
Held eight-shop capture/continuation p95 is **24.4/136.8 microseconds**, without a
stage sample of at least 30 ms. These are noisy offline stages/batches, not native mod-work
or whole-frame clearance; no performance change or new win is claimed.

[Command receipts, hashes, preservation audit and measurements](2026-11-01-b0b-sync2-base-results.json).
Raw logs, guard logs, audit scripts and both merge-base diffs remain ignored under
`build/b0b-sync2-base-gate/`. Production DLL SHA-256:
`ebd0720e6f367e4289caa9f4c41210606a7b1db5cb82f18431679fa70234b385`.

This is offline integration only. No game, disposable rig/profile, bridge lease,
Steam/Workshop mutation, ordinary save, WSL, deployment or release action.
Under merge rule v2, in-game proof belongs to the batched combined locked
`codex/persistent-city` head; pointer-dependent checks wait for Peter's 6 Nov
playtest. No per-PR rig proof is requested for this sync. Standing performance
practice remains mod-work p95 below 5 ms, every spike of at least 30 ms flagged,
whole-frame cost tracked separately and the four parked hotspots parked. No new
performance win or native timing clearance is claimed.
