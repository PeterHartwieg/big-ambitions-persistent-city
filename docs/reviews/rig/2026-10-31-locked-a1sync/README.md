# VERDICT: PASS

Role: `rig_test`. Separately authorized locked-session native proof for [PR #15](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/15). Tested current fetched candidate **`aa9bf9c17bb32929b733f45c9ba72b4cf85a36d0`** against requested base **`7a344a7d9dcfedf950f93f34414bb58620303517`**. Evidence branch starts at `origin/codex/persistent-city` **00d1321**. All five gating checks pass within the authorized seam scope. Actual pointer/focus checks are **DEFERRED-INPUT** to Peter. No merge, PR action, issue, release or ordinary-save deployment was performed.

| Check | Result | Evidence and scope |
| --- | --- | --- |
| 1. A1 inspector on merged head | PASS | Five same residents: worker c0, student c102, food traveller c149, meal-line coverage and c1/c10. Addresses equal independently called native formatting; every display field equals the earlier A1-fix proof at the identical clock. No displayed localization key. Map arguments agree with prior proof/native registrations. Cached content and rendered order checked. Complete live PC serialization and body/route/food counters unchanged by each batch. **Seam, not gesture proof.** [Full strings](candidate-inspector-summary.json), [comparisons/order/counters](content-comparison.json), [popup](candidate-popup.jpg). |
| 2. F0 default/off, on and continuity | PASS | Missing keys and explicit off release/hide bodies in the owned cafe and reacquire outside; baseline positive release/re-entry agrees. Schema/fields unchanged. On entry releases bodies during fade and reacquires IDs `c154, c248, pc:53, pc:58`; exit, re-entry, second shop complete. After final exit, inspector remains readable/native-formatted with stable identities. No duplicate owned instance/ID sampled. Initial empty-body observations retained and supplemented at a busier clock. [States/traces/contracts](functional-summary.json), [on image](on-inside.jpg), [second shop](on-second-shop.jpg), [post-interior inspector](post-interior-inspector-summary.json). |
| 3. Candidate manual save/reload | PASS | 957 residents / 957 unique IDs preserved. Saved PC payload matches reloaded source; same five residents' display strings identical. No duplicate owned body sampled. Autosave-off reverified after reload. **Schema 16**, no A1/F0 persisted-field addition. [Reload/digests](save-reload-summary.json), [reloaded strings](candidate-reloaded-inspector-summary.json), [schema contracts](functional-summary.json). |
| 4. Interleaved performance | PASS | Base, candidate, base, candidate, then popup open: five clean >=60 s street windows, identical retained morning FT bytes/settings/clock target. First four: inspector closed, interior view default. All mod-work p95 <5 ms; **0 measured mod spikes >=30 ms**. cand1: 0 formatter calls / 0 picks; cand2: 0 formatter calls / 0 picks. [Timing/raw/load/validity](performance-summary.json), [open popup](candidate-open-perf.jpg). |
| 5. Native logs | PASS | All four logs have zero Persistent City error/exception stacks, stop/adapter failures or inspector guard failures. Native/other exceptions are separated below. [Classifications](log-checks.json), [startup/setup flags](outside-window-spikes.json). |
| Extra rendered glyph observation | PARTIAL | The popup-open performance JPG has incomplete glyphs in two meal-label lines. Cached strings and the first/post-interior popup captures are correct. This image-only observation is PARTIAL (capture/font cause not isolated), separate from the seam content proof; no persistent text mismatch was observed. [Image](candidate-open-perf.jpg). |
| Hover / pointer / click / wheel / focus | DEFERRED-INPUT | Peter's playtest. No gesture pass inferred from bridge or inspector seams. |

Address text is string-for-string native output; business names/link arrows are additional labels. Full JSON includes story, heading/reason and all three meal lines, including c149's existing breakfast journey to **30 3rd Street**. Internal raw Street/PlaceId map arguments remain unchanged and are not displayed as addresses.

| Resident / address | Candidate | Independent native formatter | Earlier A1-fix |
| --- | --- | --- | --- |
| c0 Home | 49 4th Avenue | 49 4th Avenue | 49 4th Avenue |
| c0 Work | 21 5th Avenue | 21 5th Avenue | 21 5th Avenue |
| c102 Home | 18 9th Street | 18 9th Street | 18 9th Street |
| c102 Work | 7 4th Avenue | 7 4th Avenue | 7 4th Avenue |
| c149 Home | 38 3rd Street | 38 3rd Street | 38 3rd Street |
| c149 Work | 22 6th Avenue | 22 6th Avenue | 22 6th Avenue |
| c1 Home | 18 25th Street | 18 25th Street | 18 25th Street |
| c1 Work | 3 9th Avenue | 3 9th Avenue | 3 9th Avenue |
| c10 Home | 14 9th Street | 14 9th Street | 14 9th Street |
| c10 Work | 1 1st Street | 1 1st Street | 1 1st Street |

Candidate and earlier A1-fix captures both occur at exactly **7720.349502563477 native minutes**, day 5 08:40. All cached fields, five identities and native place strings match. Reload strings remain identical at **7720.682834625244**; native load advances about one third of a game minute. [Earlier strings](a1fix-inspector-summary.json), previous accepted A1-fix report commit `d9931f1`, F0 report commit `d308a6e`. Base has no A1 inspector; the merged candidate renders it. The earlier raw-address defect does not reproduce.

Timing values are **p50 / p95 / max milliseconds**. Whole-frame intervals/stalls are informational and separate from the mod-work gate. Load: CPU total; dotnet/testhost/codex counts; available RAM at start. Detailed before/after CPU/top processes/counts/RAM are in JSON. All windows coexist with other system work; no speedup or causal whole-frame claim.

| Window, execution order | Seconds | Mod work ms | Whole frame ms | Mod spikes >=30 ms | Start load | Load wait |
| --- | --- | --- | --- | --- | --- | --- |
| base1 | 60.369 | 0.468 / 0.728 / 29.698 | 15.375 / 21.128 / 71.601 | 0 | 78.0%; 1/0/10; 19.13 GiB | 900.2s |
| cand1 | 60.174 | 0.300 / 0.427 / 8.270 | 11.158 / 13.550 / 53.515 | 0 | 28.9%; 0/0/10; 25.62 GiB | 570.2s |
| base2 | 60.189 | 0.286 / 0.429 / 17.191 | 11.157 / 14.063 / 100.536 | 0 | 17.4%; 0/0/10; 29.71 GiB | 0.0s |
| cand2 | 60.122 | 0.378 / 0.671 / 9.073 | 12.929 / 17.677 / 64.866 | 0 | 26.6%; 1/0/10; 23.02 GiB | 900.2s |
| cand2-open | 60.156 | 0.344 / 0.471 / 24.629 | 11.183 / 12.802 / 40.657 | 0 | 28.7%; 0/0/10; 21.28 GiB | 0.0s |

Mod work = Update/authority union + same-frame whole PopulationRuntime.OnGUI + six disjoint native physics/intersection hook bodies, asserted outside the primary union. Nested components are not added; other small callbacks outside these scopes remain unmeasured. Percentiles use floor(n*q); initial partial wall interval retained, initial/current partial mod frames excluded. All timing/cross-frame/event/drop/active-report errors are zero. Every measured >=30 ms mod sample has window, Unity frame and phase in JSON. Logged startup/load/time-jump component costs >=30 ms are separately flagged; broad summaries without a frame remain explicitly unaligned. This is not continuous whole-session profiling.

Whole-frame stalls (>50 / >100 / >250 ms): **base1: 1 / 0 / 0; cand1: 1 / 0 / 0; base2: 3 / 1 / 0; cand2: 2 / 0 / 0; cand2-open: 0 / 0 / 0**. Exact start clocks in native minutes: base1=7673.516865; cand1=7675.558075; base2=7675.496822; cand2=7675.421246; cand2-open=7675.750340. Setup/transport edges differ slightly; these are near-matched saved-clock windows, not synchronized replays.

Every bracket verifies autosave off, no saving in progress, unchanged save-file size/mtime inventories and no actual save activity. Manual saves/reloads, captures and heavy body/scene/serialization diagnostics stay outside measured windows. Open popup uses normal refresh after seam opening: **67 refreshes / 268 formatter calls**, **0 in Draw**, **0 outside refresh**. Allocation counter unavailable; zero byte field is not an allocation measurement. No extra perf-only rerun. Dotnet/testhost was polled and waited for up to 15 minutes before each window; remaining/newly arriving load is recorded and no other process was controlled.

Native NullReferenceException-group counts: **base1: 4; cand1: 14; base2: 6; cand2: 22**. Outdoor/culling native NullReferenceException groups reproduce in both arms. Native reload/tween/teardown exceptions are retained with their actual stacks/contexts without asserting a PC cause or fix. None has a Persistent City or observer frame. No associated identity loss or unreadable save observed. [Classified logs](log-checks.json).

| Build | Source SHA | Built and actually loaded DLL SHA-256 |
| --- | --- | --- |
| Base | `7a344a7d9dcfedf950f93f34414bb58620303517` | `d442b8cea2754dd0c2e7736653f06791dcdd1a5d8916d0769dfc77386dea97e9` |
| Candidate | `aa9bf9c17bb32929b733f45c9ba72b4cf85a36d0` | `5b71efdd6a70b1100c326a492580cfe06673f8e9fa52e79ef2ddd7a02eb8d074` |

Both untouched detached trees built successfully against read-only agent1 Managed references with `%LOCALAPPDATA%\Microsoft\dotnet` prefixed on PATH. **No offline dotnet test suite ran.** Loaded hashes verified in all four processes. Same passive helper binaries; unchanged original RLO 0.45.0 plus bridge-only host, game Build 3682, D3D11/RTX 4070, Low/1920x1080, target 90 FPS, vSync off, native normal speed and calendar factor one third. Other gameplay mods parked; not full-modpack certification. Only disposable copies of the retained morning FT fixture and exact earlier A1-fix checkpoint were loaded. [Build/load/helper/fixture provenance](tested-builds.json), [tooling notes](tooling-notes.json), [sanitized scripts](scripts/).

**Cleanup: PASS — restored, hash-verified, released.** Four owned games quit normally, exit 0; no game kill. Original profile **4828/4828** hashes and **107/107** install references match. Original ModsLocal, PC config, saves, game preferences/autosave, root receipt/run/log and preference-export hashes match backup. Original autosave enabled, five minutes, three slots. One identified owned Python driver stopped before city load to correct its copied owner assertion; sole forced tool termination. No owned game/build/helper remains. Lease **held=false, pending=0**. [Cleanup/backup/restore/release hashes](cleanup-summary.json), [initial locked session](session-initial.json), [final locked session](session-final.json).

Windows settings/session connections, normal profile, Steam/Workshop, RLO source, other worktrees and unowned processes untouched. Only authorized agent1 game preferences changed/restored. Private backups/token/raw saves/state/full logs remain outside Git; no binaries/secrets published. F0 drawing is a no-op; future visible-city rendering/F1 is not cleared. Functional screenshot FPS includes heavy diagnostic observers and is not a clean performance sample.

Deferred to Peter: walker hover/movement/highlighting, popup-open click, HUD exclusion, map-link gesture/focus, arbitrary-position click/wheel/hold/release/drag and placement safety. All **DEFERRED-INPUT**, with no assumed pass or defect. Native entry/exit and popup operations here are **seam, not gesture proof**.

`python verify_evidence.py` verifies the manifest, recomputes five raw distributions/spikes and checks validity, content/read-only state, schema/continuity and cleanup. Published scripts are sanitized templates requiring stage/profile configuration and existing bridge/toolchain dependencies.
