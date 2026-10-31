# VERDICT: PASS

Role: `rig_test`. PR #12 / C1 locked-session merge-gating proof. Candidate **`7d7ee2a11710988bb60d245eb386f03676adff03`**, unchanged PR head at the initial fetch. Actual merge base/baseline **`6d43276198eba5d0dd016b51bc7fc670bc651762`** (the expected `6d43276`). The evidence branch was created from the then-current `origin/codex/persistent-city` at `7a344a7`. Production source was unchanged. No offline dotnet test suite was run.

**Spike flag:** 1 matched-frame interval(s) >=30 ms. Largest **205.330 ms**, `cand1-quiet-scope8`, Unity frame **46181**, `warm-facade-8` / `traffic-advance`. The PhysicalPopulation.Update interval is **204.332 ms** and its reader cost is **0.157 ms**; component intervals and all flags are retained. This does not establish the spike's cause. The specified p95 gate is evaluated separately.

| Required check | Result | Evidence |
| --- | --- | --- |
| 1. Explicit detached dumps, repeat reads and reload | PASS | [Native proof and raw DTO/native-field links](native-read-proof.json), [coverage/limits](native-coverage.json) |
| 2. Six Unknown mappings and unsupported/changed inputs | PASS | [Loaded native fields/enums and reviewed rows](cand1-metadata.json), [detached diagnostics](detached-diagnostics.json), [source integration](source-integration.json) |
| 3. Native customer/account/campaign noninterference | PASS | [Exact before/idle/after hashes, twice](native-read-proof.json), [populated/empty data coverage](native-coverage.json) |
| 4. Matched idle/1/8 performance, interleaved builds | PASS | [Every window, phase, load, GC and raw samples](performance-summary.json), [separate full-scope flags](full-scope-diagnostic.json) |
| 5. Load, save/reload, unchanged contract, PC log errors | PASS | [Save byte/contract comparison](save-contract.json), [Player-log review](log-review.json), [loaded DLL provenance](cand1-metadata.json) |
| Agency UI options/on/off/payment lifecycle (PR input item 1) | DEFERRED-INPUT | Peter's own playtest; no agency gesture or campaign control was driven. |
| Native allocated bytes | NOT-RUN (counter unavailable) | Mono's thread counter failed a known-allocation canary; Unity's recorder is unavailable. GC and retained-heap estimates are reported; zero allocation is not claimed. |

The copied disposable FT morning and quiet fixtures have **957 residents / 1,059 simulation places**, **885 saved registrations**, and **zero native campaign entries**. Actual scopes are **1 / 8 / 885**, with explicit current-save indices recorded in every dump. No existing campaign-control seam was available in the reused tooling; native on/off states were not fabricated. These fixture observations and detached doubles do not prove agency options or live campaign lifecycle.

Both paused checkpoints passed an idle control and **100 repeated facade reads at each of the three scopes**, before and after ordinary save/reload: **600 repeated reads total**, plus the exact dumps. Each comparison held the native minute fixed; the idle bracket was two wall seconds. Native fields, all captured campaign registrations, customer demand/cache, cash, transaction lists and financial summaries were byte/value-identical before/idle/after. Raw captures have no depth truncation. There is 1 nonempty fulfilled-customer-demand registration, 348 registrations with daily-income entries, 8 transactions and 4 financial summaries. The customer-entry cache is observed empty, and `MarketingExpenses` is observed zero. That limitation is explicit.

Assembly reflection confirms only `agencyAddress`, `marketingTypeName`, and Boolean `enabled` on the native campaign shape, plus the exact Int32 enum codes 0–5 and `None=6`. All six reviewed rows remain Unknown. Detached enabled-type/disabled/None/999/changed-enum inputs keep resident group, independent active state, start/end/duration and target Unknown; duplicates are refused. Changed minute/revision discards partial publication, and a one-business step does not publish a partial image. No exposure, choice, startup, scheduler or save integration was added.

## Performance

All times below are **ms**. Windows run in build order **base1 → cand1 → base2 → cand2**, quiet then breakfast within each arm, restoring the same checkpoint before each condition and warming for 15 seconds at native normal speed. Every matched window is at least 60 seconds. Settings: Low, 1920×1080, 90 target FPS, vSync 0, default one-third calendar factor. Saved fixture/clock/camera/population setup is restored; exact observed starting minutes and environments are in the JSON, rather than claiming bit-identical engine pacing.

**Combined mod work = same-frame Update/authority union + whole PopulationRuntime.OnGUI + synchronous C1 read wall cost.** Nested components are not added. Calls run on the main thread; reflection dispatch is included. Reader bookkeeping and other hooks outside those scopes remain unmeasured. Every raw read row is retained; initial/current partial mod frames are excluded from the completed-frame distribution. Percentiles use floor(n×q).

Each 1/8-business condition alternates **cold direct Runtime reader** (new binding cache over selected existing registrations) and **warm production facade** (`PopulationRuntime.ReadCampaignSnapshot`, including canonical business identity resolution). The direct reader uses caller-supplied diagnostic source IDs; the facade uses canonical native business IDs. These are different entry paths, reported separately; no cold/warm speed ratio or fresh-process JIT claim is made.

| Window | Seconds | Combined mod p50/p95/max | Whole frame p50/p95/max | Mod spikes ≥30 | CPU before/after % | dotnet/testhost load |
| --- | ---: | --- | --- | ---: | --- | --- |
| base1-quiet-idle | 60.23 | 0.562/0.796/8.336 | 11.180/12.792/80.058 | 0 | 27.1/26.9 | yes |
| base1-breakfast-idle | 60.07 | 0.281/0.398/16.585 | 11.261/13.119/81.549 | 0 | 27.7/39.6 | yes |
| cand1-quiet-idle | 60.24 | 0.522/0.747/24.891 | 11.235/13.025/74.676 | 0 | 38.5/24.8 | yes |
| cand1-quiet-scope1 | 60.52 | 0.598/0.813/19.659 | 11.181/12.832/249.272 | 0 | 22.4/19.9 | no |
| cand1-quiet-scope8 | 60.40 | 0.594/0.869/205.330 | 11.384/14.512/225.651 | 1 | 28.6/41.6 | yes |
| cand1-breakfast-idle | 60.10 | 0.322/0.486/8.258 | 11.162/13.421/36.864 | 0 | 19.5/36.4 | yes |
| cand1-breakfast-scope1 | 60.46 | 0.372/0.629/28.816 | 11.616/15.951/64.848 | 0 | 83.6/36.4 | yes |
| cand1-breakfast-scope8 | 60.52 | 0.388/0.478/8.460 | 11.133/12.716/31.353 | 0 | 18.7/19.9 | no |
| base2-quiet-idle | 60.13 | 0.439/0.661/9.132 | 11.136/12.724/42.880 | 0 | 20.0/31.6 | no |
| base2-breakfast-idle | 60.14 | 0.303/0.420/8.992 | 11.142/12.750/32.091 | 0 | 32.6/30.0 | no |
| cand2-quiet-idle | 60.33 | 0.477/0.689/7.105 | 11.145/12.890/51.313 | 0 | 48.2/32.2 | yes |
| cand2-quiet-scope1 | 60.43 | 0.551/0.736/7.862 | 11.133/12.482/29.761 | 0 | 18.5/30.8 | no |
| cand2-quiet-scope8 | 60.29 | 0.674/0.902/7.691 | 11.187/12.803/38.303 | 0 | 20.2/22.5 | no |
| cand2-breakfast-idle | 60.37 | 0.272/0.366/9.313 | 11.191/12.752/67.312 | 0 | 20.2/31.7 | yes |
| cand2-breakfast-scope1 | 60.41 | 0.352/0.513/11.036 | 11.296/14.513/122.299 | 0 | 18.1/41.8 | yes |
| cand2-breakfast-scope8 | 60.46 | 0.446/0.561/8.503 | 11.120/12.662/33.149 | 0 | 81.8/21.6 | yes |

| Read condition | Combined mod p50/p95/max on its frames | Reader-only p50/p95/max |
| --- | --- | --- |
| cand1-quiet-scope1: cold-new-reader-1 | 0.620/0.835/7.974 | 0.098/0.123/0.382 |
| cand1-quiet-scope1: warm-facade-1 | 0.571/0.779/19.659 | 0.043/0.054/19.124 |
| cand1-quiet-scope8: cold-new-reader-8 | 0.618/0.882/12.970 | 0.133/0.197/6.205 |
| cand1-quiet-scope8: warm-facade-8 | 0.571/0.843/205.330 | 0.083/0.117/3.829 |
| cand1-breakfast-scope1: cold-new-reader-1 | 0.395/0.690/28.816 | 0.103/0.187/28.426 |
| cand1-breakfast-scope1: warm-facade-1 | 0.339/0.582/7.826 | 0.047/0.083/7.363 |
| cand1-breakfast-scope8: cold-new-reader-8 | 0.403/0.498/8.460 | 0.130/0.148/0.718 |
| cand1-breakfast-scope8: warm-facade-8 | 0.359/0.448/3.459 | 0.081/0.096/0.891 |
| cand2-quiet-scope1: cold-new-reader-1 | 0.579/0.758/7.862 | 0.095/0.108/5.795 |
| cand2-quiet-scope1: warm-facade-1 | 0.531/0.709/7.005 | 0.042/0.050/1.383 |
| cand2-quiet-scope8: cold-new-reader-8 | 0.692/0.914/4.469 | 0.131/0.173/0.782 |
| cand2-quiet-scope8: warm-facade-8 | 0.647/0.876/7.691 | 0.081/0.110/0.402 |
| cand2-breakfast-scope1: cold-new-reader-1 | 0.378/0.542/11.036 | 0.099/0.137/4.454 |
| cand2-breakfast-scope1: warm-facade-1 | 0.327/0.467/8.075 | 0.044/0.059/0.285 |
| cand2-breakfast-scope8: cold-new-reader-8 | 0.460/0.581/4.865 | 0.129/0.154/0.684 |
| cand2-breakfast-scope8: warm-facade-8 | 0.416/0.535/8.503 | 0.080/0.098/0.836 |

**Flagged diagnostic, not a gate:** synchronous full scope is **885 registrations / 102 calls** while paused. Combined work on all dump frames is **3.325/5.410/20.836 ms**; **0 spikes ≥30 ms**, all recorded with Unity frame, phase and components. No dump frame is hidden or pooled into the normal 1/8-scope p95. Every reader frame has a matching primary mod frame. [Full distributions, every flagged frame and raw samples](full-scope-diagnostic.json).

| Full-scope path | Combined p50/p95/max | Reader-only p50/p95/max |
| --- | --- | --- |
| cold-new-reader-885 | 3.446/5.410/20.836 | 3.165/4.926/20.567 |
| warm-facade-885 | 2.850/6.179/8.198 | 2.565/5.839/7.890 |

Any full-scope path with p95 ≥5 ms is flagged in its JSON distribution. Full-scope whole-frame p50/p95/max: **11.142/13.792/28.343 ms**; stalls >50/>100/>250 ms: 0/0/0. This is informational; the future C2b continuation scheduler is not certified by a synchronous dump.

Matched windows contain **1 mod-work spike ≥30 ms**; every one is enumerated in [performance-summary.json](performance-summary.json). Whole-frame stalls are reported separately per window (>50/>100/>250 ms). Whole-frame timing is informational under the locked desktop and shared load.

System snapshots span **18.1–83.6% CPU** and **21.43–34.02 GiB available RAM** of 63.84 GiB. Every window records top process CPU deltas/RAM, dotnet/testhost/codex/python counts, pre/post CPU and RAM, and all load-wait polls. Per-process CPU deltas sum across cores, not machine percentages; the exported ranking can include the Windows idle process. External dotnet/testhost work was waited for up to 15 minutes before each window, then measured/labelled under load when still present. No other process was controlled.

GC collection deltas and whole-process managed bytes before/after are retained per window. The unsupported allocation counters are not interpreted as zero. [Retained detached-image heap estimates](memory.json) average 32 simultaneously held images per scope and are noisy GC.GetTotalMemory observations, not an allocation or leak bound.

GC.CollectionCount deltas summed across matched windows (reported API generations 0/1/2): **29/29/29**; these are whole-game counters, not solely C1 allocations. Retained-image estimates: 1 registrations = 0 B/image, 8 registrations = 0 B/image, 885 registrations = 28544 B/image. The zero estimates at scopes 1/8 reflect noisy/granular heap accounting and do not prove zero retention.

## Save, log and build evidence

The baseline and candidate read the same schema-13 payload and produce the same migration result. Their ordinary manual saves produce **byte-identical schema-16 Persistent City payloads: 5,690,810 characters**, SHA-256 **`b41cfffb13e035474a7c4a8245a889936aa5304c060eb16c397d327e45a2d309`**, each exactly round-trippable. Different named native save containers are not claimed byte-identical. The candidate reload succeeds, returns the same detached business images and repeats the zero-native-change proof. All Persistence source hashes are identical to baseline.

All completed Player logs are reviewed in [log-review.json](log-review.json), with full-log SHA-256 and sanitised error/exception contexts. Native outside-building culling null-reference errors and native DOTween reload warnings occur in this fixture; they are retained rather than described as an error-free game. Persistent City errors/exceptions are separately checked.

| Build | Source SHA | PersistentCity.dll SHA-256 |
| --- | --- | --- |
| Baseline | `6d43276198eba5d0dd016b51bc7fc670bc651762` | `f7f0c36e42dcd2b66067245602f27622a2020c2df5670ecf636e9b9525ca471c` |
| Candidate | `7d7ee2a11710988bb60d245eb386f03676adff03` | `7dc7074408e93c88394ef8c9f56e5f9af4df935d2d0ee8b5b086ee15db722e8f` |

Both DLLs were built unchanged with `python tools/build.py --game-managed` against agent1 Managed references, with `%LOCALAPPDATA%\Microsoft\dotnet` prefixed to PATH. [Source/build provenance](source-integration.json) and per-arm `*-metadata.json` record the loaded PC DLL path/hash/MVID and the actual agent1 native assembly hashes/field/enum contract. The earlier Steam inspection hash in the C1 plan is not substituted for the loaded rig assembly hash. RLO is unchanged; the bridge-only host does not invoke Food Trucks gameplay entry points. No A1–A3 stack was added.

A first stage-only packaging attempt placed a diagnostic fixture DLL beside the bridge host DLL; the mod loader did not start the bridge. No measurement began. The owned game closed normally through WM_CLOSE (exit 0); the waiting owned Python driver was stopped because its bridge could not exist. Packaging was corrected to a non-discoverable `.fixture` extension. Subsequent games use the corrected host and quit through the bridge. This was a test-tooling issue, not a C1 defect. [Correction receipt](tooling-packaging-correction.json).

The first scope-1 and full-scope instrumentation preflight included Unity runtime discovery inside the timer. Separated calibration confirms roughly 90 ms in that scene lookup; the cached facade itself is fast. Those raw samples and all >=30 ms events remain in [lookup-preflight.json](lookup-preflight.json), classified NOT-RUN for C1 attribution, not a mod defect. The corrected helper resolves the current runtime before timing. Only the affected invalid conditions were replaced; the valid candidate idle window and native/save/reload proof are retained. Candidate arm 1 therefore spans two owned game processes with the same production DLL.

## Autosave, cleanup and lease

Original agent1 autosave: enabled (`preventAutoSave=false`), five minutes, three slots. It was disabled after loading and verified off before/after each matched window and after every reload. Save-file inventory and Player-log brackets show zero save activity in every accepted matched window; manual saves are outside measured windows. Full-scope diagnostic autosave checks are also retained.

**Restore/hash/lease: PASS.** All owned games quit normally; no game kill was required. Full original profile **4828/4828 file hashes**, original install references **107/107**, ModsLocal/config, original preferences/autosave, root install receipt/run metadata/log hashes match the backup. Lease is **held=false / pending=0**; the owned rig is stopped. [Backup](backup-receipt.json), [restoration](restore-receipt.json), [release](lock-released.json), [owned-process cleanup](cleanup-summary.json).

[Read-only WTS observations](session-initial.json) and [final session observation](session-final.json) record session 11 locked. No Windows/session connection change, Steam/Workshop/RLO change, ordinary-save deployment, merge or PR action was performed. Only this report folder is committed on the dedicated evidence branch.

## Deferred to Peter

- Agency UI options, actual per-type on/off/stop/removal lifecycle and native daily-payment disable behaviour.
- Native enabled/disabled campaign fixture confirmation for all available types; the tested fixtures are campaign-less.
- Any gesture/focus/pointer-driven check; no such proof is inferred from bridge seams.

Raw public CSVs are exact column projections with every sample retained; [projection manifest](raw-projections.json) records original export hashes and columns. Full original exports, native save containers, Player logs, backups and lease material remain private in the stage. The helper sources are stage-only test tooling. Run `python verify_evidence.py` here to recompute published timing and native invariants; it does not access the game or run a dotnet suite.
