# Batched rig results — 9 November 2026 (intermediate)

Intermediate background session authorised by Peter on 9 November. Tested exactly `c2fc1b094767` (F5h #163, writer 34), with `f8a7f5d` (writer 33) only for the save comparison and refusal arms. This is input to the next batch; it does not replace the final combined native/performance gate and gates no merge. No release, version, changelog, merge or campaign purchase. `<stage>` is `%USERPROFILE%\HART-Builds\persistent-city-rig-2026-11-09`.

**Origin moved during testing** to `ece487837da2faba1e52448939f58fb51b5bd404` (F2d #169), after F2c #161, E5b2 #168 and the F4/F9/E5b remainder plans. Native testing remained at `c2fc1b0`; the evidence branch is based on the refreshed origin. Sixteen newly merged checks are catalogued as NOT RUN (outside the tested candidate).

**Summary.** Of 510 catalogue rows, **6 passed**, **64 partial**, **5 failed**, and **435 not run**. The current-writer fresh FT city starts, saves and reloads. Natural fill reaches 957 residents plus 1,914 commuters (2,871 persons). A player food business was acquired and saved, but remains a non-serving fixture: the PC-absent control also returned unavailable. 50 profiled native saves completed. Timing is **provisional (PC shared)** throughout.

## Build and rig

| Item | Value |
|---|---|
| Host / role | `HOMEPCHARTWIEG` / `rig_test`; Windows, background isolated agent1. |
| Head / origin | `c2fc1b094767cf2ed08e9cfa3eb688e315a7b4ef`; origin/codex/persistent-city matched at the initial fetch. Detached build worktree; main working tree untouched. |
| Base | `f8a7f5d`, parent of F7d #158; fresh writer-33 city, not a migrated head city. |
| Build command | `python tools/build.py --game-managed "<Steam>\Big Ambitions_Data\Managed"`; each build followed `wait-other-dotnet.ps1 -Repo <its-worktree>`. PATH prefixed with `%LOCALAPPDATA%\Microsoft\dotnet`. No offline test suite run. |
| Head DLL SHA-256 | `bf3bc839b5161495141ff562a4a0837d3090777da2026fca1e15050a63a80d2f` |
| Base DLL SHA-256 | `63c357383056cbb1582fcffd9c878f22e924f6d61833708b953b09dbb8235afa` |
| Rig / installed overlay | Read-only rig source `cfcc5fde3f652356a5aa3585250f4a92604557fb` (PR #67). During this run the documented updater installed AgentRigOps SHA-256 `e9ae1a640fad85d93e30c2afb1383221c012d90a7771691d68bd4567b30ba961`; runtime 7, background `-rig-test`. Cleanup restores the original overlay (Ops SHA-256 `4b16c613eb2d8bcb90da0f5665ea5f2d6704d21177a01bda35195316c69d2f24`). |
| Install path / mods | `C:\BigAmbitions-AgentTests`; per-flow hashed 31-DLL RIG-MODSET transaction, including pinned RLO 0.40.4 and FoodTrucks 0.10.2. Only the owned isolated player was started, raised to High and closed. No foreground activation or real input. |
| PC config | Original five keys: Enabled=true, PhysicalEnabled=true, MaximumPedestrians=48, MaximumVehicles=24, PhysicalRadiusMetres=120. Other values compiled defaults; clock factor 1/3. RIG-PCCONFIG staged SaveCostDiagnostics=true for save/armed windows, restored afterward. Default-off diagnostic arm used the original config. |
| Price-weight readback | FoodChoicePriceWeight compiled default is 1.2; original config omits it. No effective live value is exposed by the installed projection/allowlist. F6k-1 is NOT RUN, rather than inferred from source. |
| Autosave | Original native preferences: 5 minutes, maximum 3, prevent_autosave=false. Rig-test guard disabled automatic saves; health/save read verified the guard before each measured window and after loads. Manual/declared save-type requests occurred outside gameplay windows. The original preferences/config are restored byte-for-byte; no Windows settings changed. |
| Load sampling | About every 15 seconds: total CPU/RAM, top process usage and dotnet/testhost/MSBuild/assistant counts. Before union windows, the stage runner checks other dotnet build/test activity and waits up to 15 minutes if needed. Only the owned player priority changed. Conditions per run/window below; shared PC activity is not controlled. |
| Evidence | Sanitised summaries below and in [rig-2026-11-09/](rig-2026-11-09/). Full reports, requests/responses, logs, load samples, backups and fixture archive remain in `<stage>` outside Git. No raw saves, game assemblies or personal save data committed. |

## Runs

| Run | Fixture / purpose | Result |
|---|---|---|
| `head-producer` | Native new_game → fresh head Ready, 957 residents/957 visitors | PASS |
| `base-producer` | Native new_game → fresh base Ready, same map bounds; seed differs | PASS |
| `head-save` | Five Default saves at 1,914 persons; initial post-load readback was too early | PASS |
| `base-save` | Five Default saves succeeded; later load wait exceeded the flow polling allowance | INFRA after all five save samples; no observed PC defect |
| `head-reload` | Corrected settled fresh-process and same-process menu/reload readbacks | PASS |
| `head-refusal` | Writer 33 loaded by current head, then native save; PC key equality | PASS |
| `base-refusal` | Writer 34 loaded by base, then native save; PC key equality | PASS |
| `head-types` | Input validation rejected uppercase label before acquisition | INPUT_ERROR; no game or lease acquired |
| `head-types-r2` | Five MidnightSave + five RecoverSave at 1,914 persons | PASS |
| `base-types` | Five MidnightSave + five RecoverSave at 1,914 persons | PASS |
| `head-declared` | Natural fill to 2,871 persons, five Default saves | PASS |
| `base-declared` | Attempted declared-load ageing; base stayed at 1,914 persons, five day-2 Default saves | PASS |
| `head-diag` | Input validation rejected empty pc_config before acquisition | INPUT_ERROR; no game or lease acquired |
| `head-diag-r2` | Default-off diagnostics, read-only projections/catalogue, 120-second union sanity | PASS |
| `head-shop-1` | 225 sqm Fourth Avenue 23 rental refused | NOT RUN for PC assertions: ops_shop_native_rental_refused (flow FAIL is fixture setup) |
| `head-shop-2` | Retry at the same supported rental selector, refused | NOT RUN for PC assertions: ops_shop_native_rental_refused (flow FAIL is fixture setup) |
| `head-shop-3` | Different supported area selector: 75 sqm Third Street 54 acquired/setup/saved | PASS |
| `head-business-age` | Owned shop through day 1 → day 2, native ledger, food/attendance/census readbacks | PASS |
| `native-control` | Same business checkpoint with PC absent; native croissant attempts unavailable | PASS |
| `head-business-perf` | Day-2 day cycle and day-3 morning/lunch/evening, speed 1; transitions speed 6 | FAIL |
| `head-declared-types` | Five MidnightSave + five RecoverSave at head actual load 2,871 | PASS |

The native control was read-only apart from ordinary game advancement and did not save over its source. Input/setup failures are not PC FAILs. Every completed guarded run restored its original lifecycle/config/modset files. Raw evidence retains the unsuccessful attempts.

## Smoke

**Fresh current city.** PC became active with 957 residents and 957 visitors. Compact payload begins `{"Schema":34,"f":"pcc-c","v":1,`. The newline-v1 epoch marker names schema 34, the exact UTF-16 character length and an increasing save sequence. A settled fresh-process load and a native return-to-menu/reload in the same process reached `SaveEpochDiagnostics.attached=true` with no accounting-stop key. The post-unload file hash/mtime sequence was not captured; that assertion remains partial.

**Population.** Natural fill at midnight created 957 visitors in 15 units and reached 1,914 commuters, 957 residents, 20 portals and a clamped pool target of zero. Census: 335 homes, 1,064 beds, 330 households, 386 workplaces, intrinsic work capacity 4,324, 764 resident workers and 20 students. Household/job/student bounds reported ok. A positive day-visitor-pool fixture and exact compact identity/assignment comparisons were not available.

**Refusals.** Current head loading writer 33 displayed “created by an older development build; start a new city”. Base loading writer 34 displayed “Unsupported population schema; preserve the payload.” Every one of the three PC namespace values was byte-identical before/after a native save in each arm. This proves refusal/preservation, not the unexposed literal `Future` enum or the historical/corruption matrix. The older rig-only probe reports `future_header` even on an active current city because its header parser predates writers 33/34; it is not used as the codec verdict.

**Player business.** The first two native rental attempts returned `ops_shop_native_rental_refused` at Fourth Avenue 23 (225 sqm). A different documented selector acquired Third Street 54, Garment District (75 sqm): register, bakery showcase, storage/cleaning, 100 croissants, 300 bags, price 10.25, hours and one employee assigned to Default 09:00–18:00 shifts on all seven days. Ownership was read back after reload. PC feedback refused offers as “No staffed counter”. With PC absent, four native croissant attempts also returned unavailable; no sale/order/revenue or stock change, no lost log entries or hook faults. The setup is reusable for ownership; it is not a paid-service fixture. No workaround or native rival write was attempted.

**Economy/research/vendor.** Native first-day bank reconciled exactly: 10,000 − 4,110 setup/deposit − 225 payroll − 66 rent = 5,599. Day 2: 5,599 − 225 payroll − 66 rent = 5,308; sales/completed orders remained zero. PC aggregate income/resources and feedback projections were retained; no exact paid-sale reconciliation is claimed. Idle research contract/ledger attachment passed, with zero orders/messages/billing. Vendor bridge returned Absent, “Street Food vendor API missing”, in the pinned mod set. No campaign/research purchase, vehicle setup or Street Food asset edit.

**Read-only evidence.** Repeated paused diagnostic reads left PC key hashes unchanged. Filtered catalogue count was 807; dumplings/chopped cheese and the griddle whitelist were captured. Building-placeability/consuming-holder identity and the independent tag cache were not exposed, so no completed positive/negative holder result is inferred. Reviewed fresh-city logs contain no PC stop/exception/save failure; expected unload warnings and deliberate older-schema refusals are separate.

## Save phase (F7d)

Save-specific requests were explicit, outside all gameplay windows. Both builds: five Default + five MidnightSave + five RecoverSave at 957 residents/957 visitors (1,914 persons). Head repeated all three types, five each, at 957/1,914 (2,871 persons). The base has five additional day-2 Default saves, still at 957/957 (1,914 persons). Total **50 profiled samples**, with completed groups below. All returned complete/successful saves; PC phase sums match total within 0.01 ms; UTF-16 bytes = 2 × characters. Five-sample quantiles use nearest rank, so p95=max.

The documented native new_game producer has no common-seed parameter. These are independently fresh cities, with different population keys; save warm-up also changes payload size. **The base predates F5g and does not fill to 2,871**: its writer-33 day-2 payload still contains Citizens=957 and Visitors=957; its manifest command is unavailable. The census implied load must not be confused with this saved count. A base 2,871-person arm is NOT RUN (needs a supported fixture builder). They are **unmatched**, and no writer-only causal speed/size claim is made. OS working/peak/private memory readbacks were zero/unavailable, so peak transient is **not measured**; before/after GC heaps and sampled process RSS are not a substitute.

### Shared-PC save conditions

| Condition | Run | Sampled ambient load (whole flow, including load/cleanup) |
|---|---|---|
| S1 | `head-save` | CPU 14.1–25.8%; RAM 64.9–75.3%; dotnet/testhost/MSBuild max 0/0/0; assistant CLI count 6–10 |
| S2 | `head-types-r2` | CPU 12.2–41.4%; RAM 65.3–77.0%; dotnet/testhost/MSBuild max 1/0/0; assistant CLI count 6–6 |
| S3 | `head-declared` | CPU 9.8–28.8%; RAM 66.5–76.2%; dotnet/testhost/MSBuild max 0/0/0; assistant CLI count 6–6 |
| S4 | `head-declared-types` | CPU 10.6–18.5%; RAM 59.3–68.9%; dotnet/testhost/MSBuild max 0/0/0; assistant CLI count 6–6 |
| S5 | `base-save` | CPU 14.3–27.5%; RAM 63.0–74.4%; dotnet/testhost/MSBuild max 1/0/0; assistant CLI count 6–8 |
| S6 | `base-types` | CPU 12.3–22.0%; RAM 66.6–75.6%; dotnet/testhost/MSBuild max 1/0/0; assistant CLI count 6–6 |
| S7 | `base-declared` | CPU 13.1–69.1%; RAM 65.6–82.7%; dotnet/testhost/MSBuild max 1/0/0; assistant CLI count 6–6 |

Counts are process-name samples, not proof that every dotnet process was compiling during a save. All numbers below are **provisional (PC shared: the indicated condition)**. No subtraction for observer overhead. `recordCountMs` is diagnostic reparsing after the timed PC phases but inside the native callback; it is shown separately and subtracted per sample from native Save before computing the adjusted quantiles.

| Build / persons / type | Phase | p50 / p95 / max (ms) | Samples ≥30 ms |
|---|---|---|---:|
| head / 1,914 / Default (S1) | observeMs | provisional (PC shared: S1) 1.940 / 2.994 / 2.994 | 0/5 |
| head / 1,914 / Default (S1) | synchronizeMs | provisional (PC shared: S1) 5.939 / 7.191 / 7.191 | 0/5 |
| head / 1,914 / Default (S1) | captureMs | provisional (PC shared: S1) 9.004 / 14.016 / 14.016 | 0/5 |
| head / 1,914 / Default (S1) | validateMs | provisional (PC shared: S1) 30.063 / 65.655 / 65.655 | 3/5 |
| head / 1,914 / Default (S1) | encodeMs | provisional (PC shared: S1) 374.440 / 400.655 / 400.655 | 5/5 |
| head / 1,914 / Default (S1) | publishMs | provisional (PC shared: S1) 0.009 / 0.328 / 0.328 | 0/5 |
| head / 1,914 / Default (S1) | totalMs | provisional (PC shared: S1) 442.356 / 454.901 / 454.901 | 5/5 |
| head / 1,914 / Default (S1) | recordCountMs | provisional (PC shared: S1) 402.755 / 444.717 / 444.717 | 5/5 |
| head / 1,914 / Default (S1) | PC callback incl. record counting | provisional (PC shared: S1) 832.396 / 860.112 / 860.112 | 5/5 |
| head / 1,914 / Default (S1) | Save | provisional (PC shared: S1) 893.830 / 972.549 / 972.549 | 5/5 |
| head / 1,914 / Default (S1) | Save minus recordCountMs | provisional (PC shared: S1) 483.670 / 605.476 / 605.476 | 5/5 |
| head / 1,914 / Default (S1) | PortraitGenerator.Create | provisional (PC shared: S1) 9.321 / 15.853 / 15.853 | 0/5 |
| head / 1,914 / Default (S1) | GenerateSaveGameTexture | provisional (PC shared: S1) 0.810 / 2.160 / 2.160 | 0/5 |
| head / 1,914 / Default (S1) | SerializeSaveGame | provisional (PC shared: S1) 26.468 / 98.710 / 98.710 | 1/5 |
| head / 1,914 / Default (S1) | CompressSaveGame | provisional (PC shared: S1) 139.235 / 154.056 / 154.056 | 5/5 |
| head / 1,914 / Default (S1) | derived remaining onSaveGame subscribers after ms | provisional (PC shared: S1) 0.975 / 24.454 / 24.454 | 0/5 |
| head / 1,914 / Default (S1) | derived portrait plus screenshot dispatch ms | provisional (PC shared: S1) 10.131 / 18.013 / 18.013 | 0/5 |
| head / 1,914 / Default (S1) | derived remaining subscribers plus portrait dispatch ms | provisional (PC shared: S1) 11.079 / 42.467 / 42.467 | 1/5 |
| head / 1,914 / MidnightSave (S2) | observeMs | provisional (PC shared: S2) 3.358 / 4.436 / 4.436 | 0/5 |
| head / 1,914 / MidnightSave (S2) | synchronizeMs | provisional (PC shared: S2) 6.072 / 7.006 / 7.006 | 0/5 |
| head / 1,914 / MidnightSave (S2) | captureMs | provisional (PC shared: S2) 8.805 / 14.798 / 14.798 | 0/5 |
| head / 1,914 / MidnightSave (S2) | validateMs | provisional (PC shared: S2) 44.593 / 67.772 / 67.772 | 5/5 |
| head / 1,914 / MidnightSave (S2) | encodeMs | provisional (PC shared: S2) 986.096 / 1,218.092 / 1,218.092 | 5/5 |
| head / 1,914 / MidnightSave (S2) | publishMs | provisional (PC shared: S2) 0.009 / 0.437 / 0.437 | 0/5 |
| head / 1,914 / MidnightSave (S2) | totalMs | provisional (PC shared: S2) 1,059.671 / 1,280.442 / 1,280.442 | 5/5 |
| head / 1,914 / MidnightSave (S2) | recordCountMs | provisional (PC shared: S2) 1,119.725 / 1,320.089 / 1,320.089 | 5/5 |
| head / 1,914 / MidnightSave (S2) | PC callback incl. record counting | provisional (PC shared: S2) 2,350.824 / 2,400.191 / 2,400.191 | 5/5 |
| head / 1,914 / MidnightSave (S2) | Save | provisional (PC shared: S2) 2,405.684 / 2,573.179 / 2,573.179 | 5/5 |
| head / 1,914 / MidnightSave (S2) | Save minus recordCountMs | provisional (PC shared: S2) 1,123.730 / 1,326.590 / 1,326.590 | 5/5 |
| head / 1,914 / MidnightSave (S2) | PortraitGenerator.Create | provisional (PC shared: S2) 10.147 / 20.907 / 20.907 | 0/5 |
| head / 1,914 / MidnightSave (S2) | GenerateSaveGameTexture | provisional (PC shared: S2) 0.529 / 2.177 / 2.177 | 0/5 |
| head / 1,914 / MidnightSave (S2) | SerializeSaveGame | provisional (PC shared: S2) 32.272 / 112.083 / 112.083 | 4/5 |
| head / 1,914 / MidnightSave (S2) | CompressSaveGame | provisional (PC shared: S2) 398.805 / 425.324 / 425.324 | 5/5 |
| head / 1,914 / MidnightSave (S2) | derived remaining onSaveGame subscribers after ms | provisional (PC shared: S2) 1.536 / 24.736 / 24.736 | 0/5 |
| head / 1,914 / MidnightSave (S2) | derived portrait plus screenshot dispatch ms | provisional (PC shared: S2) 11.415 / 23.084 / 23.084 | 0/5 |
| head / 1,914 / MidnightSave (S2) | derived remaining subscribers plus portrait dispatch ms | provisional (PC shared: S2) 12.430 / 47.820 / 47.820 | 1/5 |
| head / 1,914 / RecoverSave (S2) | observeMs | provisional (PC shared: S2) 3.405 / 4.437 / 4.437 | 0/5 |
| head / 1,914 / RecoverSave (S2) | synchronizeMs | provisional (PC shared: S2) 5.657 / 6.456 / 6.456 | 0/5 |
| head / 1,914 / RecoverSave (S2) | captureMs | provisional (PC shared: S2) 8.817 / 9.427 / 9.427 | 0/5 |
| head / 1,914 / RecoverSave (S2) | validateMs | provisional (PC shared: S2) 45.296 / 52.954 / 52.954 | 5/5 |
| head / 1,914 / RecoverSave (S2) | encodeMs | provisional (PC shared: S2) 984.942 / 1,245.549 / 1,245.549 | 5/5 |
| head / 1,914 / RecoverSave (S2) | publishMs | provisional (PC shared: S2) 0.008 / 0.009 / 0.009 | 0/5 |
| head / 1,914 / RecoverSave (S2) | totalMs | provisional (PC shared: S2) 1,047.642 / 1,315.989 / 1,315.989 | 5/5 |
| head / 1,914 / RecoverSave (S2) | recordCountMs | provisional (PC shared: S2) 1,073.611 / 1,312.917 / 1,312.917 | 5/5 |
| head / 1,914 / RecoverSave (S2) | PC callback incl. record counting | provisional (PC shared: S2) 2,340.555 / 2,387.303 / 2,387.303 | 5/5 |
| head / 1,914 / RecoverSave (S2) | Save | provisional (PC shared: S2) 2,383.273 / 2,427.896 / 2,427.896 | 5/5 |
| head / 1,914 / RecoverSave (S2) | Save minus recordCountMs | provisional (PC shared: S2) 1,088.283 / 1,356.607 / 1,356.607 | 5/5 |
| head / 1,914 / RecoverSave (S2) | PortraitGenerator.Create | provisional (PC shared: S2) 7.655 / 10.162 / 10.162 | 0/5 |
| head / 1,914 / RecoverSave (S2) | GenerateSaveGameTexture | provisional (PC shared: S2) 0.460 / 0.674 / 0.674 | 0/5 |
| head / 1,914 / RecoverSave (S2) | SerializeSaveGame | provisional (PC shared: S2) 28.402 / 30.680 / 30.680 | 1/5 |
| head / 1,914 / RecoverSave (S2) | CompressSaveGame | provisional (PC shared: S2) 414.434 / 420.794 / 420.794 | 5/5 |
| head / 1,914 / RecoverSave (S2) | derived remaining onSaveGame subscribers after ms | provisional (PC shared: S2) 0.995 / 1.243 / 1.243 | 0/5 |
| head / 1,914 / RecoverSave (S2) | derived portrait plus screenshot dispatch ms | provisional (PC shared: S2) 8.192 / 10.836 / 10.836 | 0/5 |
| head / 1,914 / RecoverSave (S2) | derived remaining subscribers plus portrait dispatch ms | provisional (PC shared: S2) 9.127 / 12.051 / 12.051 | 0/5 |
| head / 2,871 / Default (day 2) (S3) | observeMs | provisional (PC shared: S3) 2.018 / 3.135 / 3.135 | 0/5 |
| head / 2,871 / Default (day 2) (S3) | synchronizeMs | provisional (PC shared: S3) 7.756 / 9.671 / 9.671 | 0/5 |
| head / 2,871 / Default (day 2) (S3) | captureMs | provisional (PC shared: S3) 12.225 / 21.427 / 21.427 | 0/5 |
| head / 2,871 / Default (day 2) (S3) | validateMs | provisional (PC shared: S3) 50.858 / 56.974 / 56.974 | 5/5 |
| head / 2,871 / Default (day 2) (S3) | encodeMs | provisional (PC shared: S3) 670.891 / 732.097 / 732.097 | 5/5 |
| head / 2,871 / Default (day 2) (S3) | publishMs | provisional (PC shared: S3) 0.009 / 0.417 / 0.417 | 0/5 |
| head / 2,871 / Default (day 2) (S3) | totalMs | provisional (PC shared: S3) 749.430 / 819.642 / 819.642 | 5/5 |
| head / 2,871 / Default (day 2) (S3) | recordCountMs | provisional (PC shared: S3) 788.081 / 1,003.112 / 1,003.112 | 5/5 |
| head / 2,871 / Default (day 2) (S3) | PC callback incl. record counting | provisional (PC shared: S3) 1,595.518 / 1,752.577 / 1,752.577 | 5/5 |
| head / 2,871 / Default (day 2) (S3) | Save | provisional (PC shared: S3) 1,668.875 / 1,819.857 / 1,819.857 | 5/5 |
| head / 2,871 / Default (day 2) (S3) | Save minus recordCountMs | provisional (PC shared: S3) 788.639 / 1,045.599 / 1,045.599 | 5/5 |
| head / 2,871 / Default (day 2) (S3) | PortraitGenerator.Create | provisional (PC shared: S3) 8.317 / 17.962 / 17.962 | 0/5 |
| head / 2,871 / Default (day 2) (S3) | GenerateSaveGameTexture | provisional (PC shared: S3) 0.538 / 5.682 / 5.682 | 0/5 |
| head / 2,871 / Default (day 2) (S3) | SerializeSaveGame | provisional (PC shared: S3) 25.814 / 143.957 / 143.957 | 1/5 |
| head / 2,871 / Default (day 2) (S3) | CompressSaveGame | provisional (PC shared: S3) 272.244 / 281.428 / 281.428 | 5/5 |
| head / 2,871 / Default (day 2) (S3) | derived remaining onSaveGame subscribers after ms | provisional (PC shared: S3) 1.703 / 33.481 / 33.481 | 1/5 |
| head / 2,871 / Default (day 2) (S3) | derived portrait plus screenshot dispatch ms | provisional (PC shared: S3) 8.771 / 23.644 / 23.644 | 0/5 |
| head / 2,871 / Default (day 2) (S3) | derived remaining subscribers plus portrait dispatch ms | provisional (PC shared: S3) 10.474 / 57.125 / 57.125 | 1/5 |
| head / 2,871 / Default (day 2) (S3) | subscriber RealLifeOverhaul.SaveHook.Run | provisional (PC shared: S3) 1.703 / 33.481 / 33.481 | 1/5 |
| head / 2,871 / MidnightSave (S4) | observeMs | provisional (PC shared: S4) 1.915 / 2.774 / 2.774 | 0/5 |
| head / 2,871 / MidnightSave (S4) | synchronizeMs | provisional (PC shared: S4) 7.200 / 7.336 / 7.336 | 0/5 |
| head / 2,871 / MidnightSave (S4) | captureMs | provisional (PC shared: S4) 11.377 / 18.468 / 18.468 | 0/5 |
| head / 2,871 / MidnightSave (S4) | validateMs | provisional (PC shared: S4) 47.811 / 54.933 / 54.933 | 5/5 |
| head / 2,871 / MidnightSave (S4) | encodeMs | provisional (PC shared: S4) 674.914 / 690.740 / 690.740 | 5/5 |
| head / 2,871 / MidnightSave (S4) | publishMs | provisional (PC shared: S4) 0.006 / 0.354 / 0.354 | 0/5 |
| head / 2,871 / MidnightSave (S4) | totalMs | provisional (PC shared: S4) 749.230 / 767.483 / 767.483 | 5/5 |
| head / 2,871 / MidnightSave (S4) | recordCountMs | provisional (PC shared: S4) 773.620 / 981.885 / 981.885 | 5/5 |
| head / 2,871 / MidnightSave (S4) | PC callback incl. record counting | provisional (PC shared: S4) 1,522.876 / 1,750.745 / 1,750.745 | 5/5 |
| head / 2,871 / MidnightSave (S4) | Save | provisional (PC shared: S4) 1,564.241 / 1,938.457 / 1,938.457 | 5/5 |
| head / 2,871 / MidnightSave (S4) | Save minus recordCountMs | provisional (PC shared: S4) 790.620 / 956.572 / 956.572 | 5/5 |
| head / 2,871 / MidnightSave (S4) | PortraitGenerator.Create | provisional (PC shared: S4) 8.019 / 19.375 / 19.375 | 0/5 |
| head / 2,871 / MidnightSave (S4) | GenerateSaveGameTexture | provisional (PC shared: S4) 0.598 / 2.368 / 2.368 | 0/5 |
| head / 2,871 / MidnightSave (S4) | SerializeSaveGame | provisional (PC shared: S4) 26.196 / 105.367 / 105.367 | 1/5 |
| head / 2,871 / MidnightSave (S4) | CompressSaveGame | provisional (PC shared: S4) 276.632 / 278.540 / 278.540 | 5/5 |
| head / 2,871 / MidnightSave (S4) | derived remaining onSaveGame subscribers after ms | provisional (PC shared: S4) 1.246 / 27.133 / 27.133 | 0/5 |
| head / 2,871 / MidnightSave (S4) | derived portrait plus screenshot dispatch ms | provisional (PC shared: S4) 8.617 / 21.743 / 21.743 | 0/5 |
| head / 2,871 / MidnightSave (S4) | derived remaining subscribers plus portrait dispatch ms | provisional (PC shared: S4) 10.388 / 48.876 / 48.876 | 1/5 |
| head / 2,871 / RecoverSave (S4) | observeMs | provisional (PC shared: S4) 1.869 / 1.982 / 1.982 | 0/5 |
| head / 2,871 / RecoverSave (S4) | synchronizeMs | provisional (PC shared: S4) 7.152 / 8.054 / 8.054 | 0/5 |
| head / 2,871 / RecoverSave (S4) | captureMs | provisional (PC shared: S4) 11.826 / 29.890 / 29.890 | 0/5 |
| head / 2,871 / RecoverSave (S4) | validateMs | provisional (PC shared: S4) 48.790 / 78.481 / 78.481 | 5/5 |
| head / 2,871 / RecoverSave (S4) | encodeMs | provisional (PC shared: S4) 667.726 / 690.794 / 690.794 | 5/5 |
| head / 2,871 / RecoverSave (S4) | publishMs | provisional (PC shared: S4) 0.007 / 0.008 / 0.008 | 0/5 |
| head / 2,871 / RecoverSave (S4) | totalMs | provisional (PC shared: S4) 744.004 / 761.693 / 761.693 | 5/5 |
| head / 2,871 / RecoverSave (S4) | recordCountMs | provisional (PC shared: S4) 775.751 / 974.715 / 974.715 | 5/5 |
| head / 2,871 / RecoverSave (S4) | PC callback incl. record counting | provisional (PC shared: S4) 1,523.942 / 1,734.010 / 1,734.010 | 5/5 |
| head / 2,871 / RecoverSave (S4) | Save | provisional (PC shared: S4) 1,565.497 / 1,772.230 / 1,772.230 | 5/5 |
| head / 2,871 / RecoverSave (S4) | Save minus recordCountMs | provisional (PC shared: S4) 781.669 / 799.934 / 799.934 | 5/5 |
| head / 2,871 / RecoverSave (S4) | PortraitGenerator.Create | provisional (PC shared: S4) 7.589 / 8.617 / 8.617 | 0/5 |
| head / 2,871 / RecoverSave (S4) | GenerateSaveGameTexture | provisional (PC shared: S4) 0.473 / 0.536 / 0.536 | 0/5 |
| head / 2,871 / RecoverSave (S4) | SerializeSaveGame | provisional (PC shared: S4) 25.164 / 27.278 / 27.278 | 0/5 |
| head / 2,871 / RecoverSave (S4) | CompressSaveGame | provisional (PC shared: S4) 273.738 / 275.168 / 275.168 | 5/5 |
| head / 2,871 / RecoverSave (S4) | derived remaining onSaveGame subscribers after ms | provisional (PC shared: S4) 1.090 / 1.186 / 1.186 | 0/5 |
| head / 2,871 / RecoverSave (S4) | derived portrait plus screenshot dispatch ms | provisional (PC shared: S4) 8.043 / 9.153 / 9.153 | 0/5 |
| head / 2,871 / RecoverSave (S4) | derived remaining subscribers plus portrait dispatch ms | provisional (PC shared: S4) 9.118 / 10.339 / 10.339 | 0/5 |
| base / 1,914 / Default (S5) | observeMs | provisional (PC shared: S5) 1.976 / 2.420 / 2.420 | 0/5 |
| base / 1,914 / Default (S5) | synchronizeMs | provisional (PC shared: S5) 5.991 / 7.138 / 7.138 | 0/5 |
| base / 1,914 / Default (S5) | captureMs | provisional (PC shared: S5) 8.459 / 18.586 / 18.586 | 0/5 |
| base / 1,914 / Default (S5) | validateMs | provisional (PC shared: S5) 31.387 / 46.804 / 46.804 | 5/5 |
| base / 1,914 / Default (S5) | encodeMs | provisional (PC shared: S5) 262.641 / 410.103 / 410.103 | 5/5 |
| base / 1,914 / Default (S5) | publishMs | provisional (PC shared: S5) 0.007 / 0.534 / 0.534 | 0/5 |
| base / 1,914 / Default (S5) | totalMs | provisional (PC shared: S5) 311.592 / 482.642 / 482.642 | 5/5 |
| base / 1,914 / Default (S5) | recordCountMs | provisional (PC shared: S5) 282.027 / 361.422 / 361.422 | 5/5 |
| base / 1,914 / Default (S5) | PC callback incl. record counting | provisional (PC shared: S5) 588.514 / 844.086 / 844.086 | 5/5 |
| base / 1,914 / Default (S5) | Save | provisional (PC shared: S5) 665.582 / 889.166 / 889.166 | 5/5 |
| base / 1,914 / Default (S5) | Save minus recordCountMs | provisional (PC shared: S5) 372.439 / 527.744 / 527.744 | 5/5 |
| base / 1,914 / Default (S5) | PortraitGenerator.Create | provisional (PC shared: S5) 7.416 / 16.234 / 16.234 | 0/5 |
| base / 1,914 / Default (S5) | GenerateSaveGameTexture | provisional (PC shared: S5) 0.623 / 2.550 / 2.550 | 0/5 |
| base / 1,914 / Default (S5) | SerializeSaveGame | provisional (PC shared: S5) 31.114 / 109.395 / 109.395 | 3/5 |
| base / 1,914 / Default (S5) | CompressSaveGame | provisional (PC shared: S5) 176.217 / 210.399 / 210.399 | 5/5 |
| base / 1,914 / Default (S5) | derived remaining onSaveGame subscribers after ms | provisional (PC shared: S5) 1.021 / 25.507 / 25.507 | 0/5 |
| base / 1,914 / Default (S5) | derived portrait plus screenshot dispatch ms | provisional (PC shared: S5) 8.176 / 18.784 / 18.784 | 0/5 |
| base / 1,914 / Default (S5) | derived remaining subscribers plus portrait dispatch ms | provisional (PC shared: S5) 9.107 / 44.291 / 44.291 | 1/5 |
| base / 1,914 / MidnightSave (S6) | observeMs | provisional (PC shared: S6) 3.772 / 5.936 / 5.936 | 0/5 |
| base / 1,914 / MidnightSave (S6) | synchronizeMs | provisional (PC shared: S6) 6.433 / 54.394 / 54.394 | 1/5 |
| base / 1,914 / MidnightSave (S6) | captureMs | provisional (PC shared: S6) 10.256 / 16.613 / 16.613 | 0/5 |
| base / 1,914 / MidnightSave (S6) | validateMs | provisional (PC shared: S6) 46.069 / 48.597 / 48.597 | 5/5 |
| base / 1,914 / MidnightSave (S6) | encodeMs | provisional (PC shared: S6) 601.364 / 623.640 / 623.640 | 5/5 |
| base / 1,914 / MidnightSave (S6) | publishMs | provisional (PC shared: S6) 0.008 / 0.403 / 0.403 | 0/5 |
| base / 1,914 / MidnightSave (S6) | totalMs | provisional (PC shared: S6) 674.934 / 685.794 / 685.794 | 5/5 |
| base / 1,914 / MidnightSave (S6) | recordCountMs | provisional (PC shared: S6) 577.630 / 653.988 / 653.988 | 5/5 |
| base / 1,914 / MidnightSave (S6) | PC callback incl. record counting | provisional (PC shared: S6) 1,251.183 / 1,292.493 / 1,292.493 | 5/5 |
| base / 1,914 / MidnightSave (S6) | Save | provisional (PC shared: S6) 1,350.347 / 1,455.920 / 1,455.920 | 5/5 |
| base / 1,914 / MidnightSave (S6) | Save minus recordCountMs | provisional (PC shared: S6) 741.923 / 893.625 / 893.625 | 5/5 |
| base / 1,914 / MidnightSave (S6) | PortraitGenerator.Create | provisional (PC shared: S6) 7.745 / 19.687 / 19.687 | 0/5 |
| base / 1,914 / MidnightSave (S6) | GenerateSaveGameTexture | provisional (PC shared: S6) 0.675 / 2.387 / 2.387 | 0/5 |
| base / 1,914 / MidnightSave (S6) | SerializeSaveGame | provisional (PC shared: S6) 42.857 / 128.475 / 128.475 | 5/5 |
| base / 1,914 / MidnightSave (S6) | CompressSaveGame | provisional (PC shared: S6) 475.010 / 559.723 / 559.723 | 5/5 |
| base / 1,914 / MidnightSave (S6) | derived remaining onSaveGame subscribers after ms | provisional (PC shared: S6) 0.953 / 24.171 / 24.171 | 0/5 |
| base / 1,914 / MidnightSave (S6) | derived portrait plus screenshot dispatch ms | provisional (PC shared: S6) 8.397 / 22.074 / 22.074 | 0/5 |
| base / 1,914 / MidnightSave (S6) | derived remaining subscribers plus portrait dispatch ms | provisional (PC shared: S6) 9.328 / 46.245 / 46.245 | 1/5 |
| base / 1,914 / RecoverSave (S6) | observeMs | provisional (PC shared: S6) 2.976 / 3.057 / 3.057 | 0/5 |
| base / 1,914 / RecoverSave (S6) | synchronizeMs | provisional (PC shared: S6) 5.896 / 6.285 / 6.285 | 0/5 |
| base / 1,914 / RecoverSave (S6) | captureMs | provisional (PC shared: S6) 9.534 / 9.617 / 9.617 | 0/5 |
| base / 1,914 / RecoverSave (S6) | validateMs | provisional (PC shared: S6) 45.190 / 54.267 / 54.267 | 5/5 |
| base / 1,914 / RecoverSave (S6) | encodeMs | provisional (PC shared: S6) 593.455 / 614.126 / 614.126 | 5/5 |
| base / 1,914 / RecoverSave (S6) | publishMs | provisional (PC shared: S6) 0.007 / 0.014 / 0.014 | 0/5 |
| base / 1,914 / RecoverSave (S6) | totalMs | provisional (PC shared: S6) 661.323 / 677.842 / 677.842 | 5/5 |
| base / 1,914 / RecoverSave (S6) | recordCountMs | provisional (PC shared: S6) 592.651 / 772.306 / 772.306 | 5/5 |
| base / 1,914 / RecoverSave (S6) | PC callback incl. record counting | provisional (PC shared: S6) 1,230.141 / 1,430.307 / 1,430.307 | 5/5 |
| base / 1,914 / RecoverSave (S6) | Save | provisional (PC shared: S6) 1,285.930 / 1,494.445 / 1,494.445 | 5/5 |
| base / 1,914 / RecoverSave (S6) | Save minus recordCountMs | provisional (PC shared: S6) 722.139 / 731.227 / 731.227 | 5/5 |
| base / 1,914 / RecoverSave (S6) | PortraitGenerator.Create | provisional (PC shared: S6) 7.686 / 8.765 / 8.765 | 0/5 |
| base / 1,914 / RecoverSave (S6) | GenerateSaveGameTexture | provisional (PC shared: S6) 0.515 / 0.875 / 0.875 | 0/5 |
| base / 1,914 / RecoverSave (S6) | SerializeSaveGame | provisional (PC shared: S6) 41.876 / 47.535 / 47.535 | 5/5 |
| base / 1,914 / RecoverSave (S6) | CompressSaveGame | provisional (PC shared: S6) 487.382 / 505.233 / 505.233 | 5/5 |
| base / 1,914 / RecoverSave (S6) | derived remaining onSaveGame subscribers after ms | provisional (PC shared: S6) 0.985 / 1.375 / 1.375 | 0/5 |
| base / 1,914 / RecoverSave (S6) | derived portrait plus screenshot dispatch ms | provisional (PC shared: S6) 8.174 / 9.640 / 9.640 | 0/5 |
| base / 1,914 / RecoverSave (S6) | derived remaining subscribers plus portrait dispatch ms | provisional (PC shared: S6) 9.122 / 11.015 / 11.015 | 0/5 |
| base / 1,914 / Default (day 2) (S7) | observeMs | provisional (PC shared: S7) 2.013 / 3.702 / 3.702 | 0/5 |
| base / 1,914 / Default (day 2) (S7) | synchronizeMs | provisional (PC shared: S7) 7.546 / 17.720 / 17.720 | 0/5 |
| base / 1,914 / Default (day 2) (S7) | captureMs | provisional (PC shared: S7) 10.948 / 35.887 / 35.887 | 1/5 |
| base / 1,914 / Default (day 2) (S7) | validateMs | provisional (PC shared: S7) 38.487 / 67.815 / 67.815 | 5/5 |
| base / 1,914 / Default (day 2) (S7) | encodeMs | provisional (PC shared: S7) 360.802 / 485.340 / 485.340 | 5/5 |
| base / 1,914 / Default (day 2) (S7) | publishMs | provisional (PC shared: S7) 0.006 / 0.436 / 0.436 | 0/5 |
| base / 1,914 / Default (day 2) (S7) | totalMs | provisional (PC shared: S7) 417.833 / 581.544 / 581.544 | 5/5 |
| base / 1,914 / Default (day 2) (S7) | recordCountMs | provisional (PC shared: S7) 347.542 / 541.856 / 541.856 | 5/5 |
| base / 1,914 / Default (day 2) (S7) | PC callback incl. record counting | provisional (PC shared: S7) 902.366 / 960.660 / 960.660 | 5/5 |
| base / 1,914 / Default (day 2) (S7) | Save | provisional (PC shared: S7) 947.085 / 1,151.558 / 1,151.558 | 5/5 |
| base / 1,914 / Default (day 2) (S7) | Save minus recordCountMs | provisional (PC shared: S7) 472.278 / 773.880 / 773.880 | 5/5 |
| base / 1,914 / Default (day 2) (S7) | PortraitGenerator.Create | provisional (PC shared: S7) 7.556 / 13.713 / 13.713 | 0/5 |
| base / 1,914 / Default (day 2) (S7) | GenerateSaveGameTexture | provisional (PC shared: S7) 0.748 / 2.582 / 2.582 | 0/5 |
| base / 1,914 / Default (day 2) (S7) | SerializeSaveGame | provisional (PC shared: S7) 31.079 / 125.725 / 125.725 | 4/5 |
| base / 1,914 / Default (day 2) (S7) | CompressSaveGame | provisional (PC shared: S7) 271.671 / 300.714 / 300.714 | 5/5 |
| base / 1,914 / Default (day 2) (S7) | derived remaining onSaveGame subscribers after ms | provisional (PC shared: S7) 1.939 / 28.663 / 28.663 | 0/5 |
| base / 1,914 / Default (day 2) (S7) | derived portrait plus screenshot dispatch ms | provisional (PC shared: S7) 8.084 / 16.295 / 16.295 | 0/5 |
| base / 1,914 / Default (day 2) (S7) | derived remaining subscribers plus portrait dispatch ms | provisional (PC shared: S7) 10.023 / 44.958 / 44.958 | 1/5 |

Every ≥30 ms phase/total above is a **save latency flag**, not gameplay mod-work. Exact sample indices, other onSaveGame subscribers, before/after GC heaps and raw values are in [save-phases.json](rig-2026-11-09/save-phases.json). Totals/contained phases are nested observations, not additional independent stalls. All adjusted native-save samples exceed the 60 ms typical native pause target; this is save-budget evidence, not a clean-play gate failure.

| Build / persons / type | Characters p50 / p95 / max | UTF-8 bytes p50 / p95 / max | UTF-16 bytes p50 / p95 / max | PC UTF-16 bytes / native uncompressed bytes, p50 / p95 / max |
|---|---|---|---|---|
| head / 1,914 / Default  | 1,213,459 / 1,503,604 / 1,503,604 | 1,213,460 / 1,503,605 / 1,503,605 | 2,426,918 / 3,007,208 / 3,007,208 | 29.04% / 33.65% / 33.65% |
| head / 1,914 / MidnightSave  | 6,439,569 / 6,439,569 / 6,439,569 | 6,439,570 / 6,439,570 / 6,439,570 | 12,879,138 / 12,879,138 / 12,879,138 | 68.47% / 68.47% / 68.47% |
| head / 1,914 / RecoverSave  | 6,439,569 / 6,439,569 / 6,439,569 | 6,439,570 / 6,439,570 / 6,439,570 | 12,879,138 / 12,879,138 / 12,879,138 | 68.47% / 68.47% / 68.47% |
| head / 2,871 / Default (day 2) | 3,964,095 / 3,964,095 / 3,964,095 | 3,964,096 / 3,964,096 / 3,964,096 | 7,928,190 / 7,928,190 / 7,928,190 | 56.99% / 56.99% / 56.99% |
| head / 2,871 / MidnightSave  | 4,057,539 / 4,057,539 / 4,057,539 | 4,057,540 / 4,057,540 / 4,057,540 | 8,115,078 / 8,115,078 / 8,115,078 | 57.55% / 57.55% / 57.55% |
| head / 2,871 / RecoverSave  | 4,057,539 / 4,057,539 / 4,057,539 | 4,057,540 / 4,057,540 / 4,057,540 | 8,115,078 / 8,115,078 / 8,115,078 | 57.55% / 57.55% / 57.55% |
| base / 1,914 / Default  | 5,282,752 / 6,025,619 / 6,025,619 | 5,282,753 / 6,025,620 / 6,025,620 | 10,565,504 / 12,051,238 / 12,051,238 | 64.15% / 67.11% / 67.11% |
| base / 1,914 / MidnightSave  | 13,395,430 / 13,599,833 / 13,599,833 | 13,395,431 / 13,599,834 / 13,599,834 | 26,790,860 / 27,199,666 / 27,199,666 | 81.94% / 82.16% / 82.16% |
| base / 1,914 / RecoverSave  | 13,599,833 / 13,599,833 / 13,599,833 | 13,599,834 / 13,599,834 / 13,599,834 | 27,199,666 / 27,199,666 / 27,199,666 | 82.16% / 82.16% / 82.16% |
| base / 1,914 / Default (day 2) | 7,616,103 / 7,616,103 / 7,616,103 | 7,616,104 / 7,616,104 / 7,616,104 | 15,232,206 / 15,232,206 / 15,232,206 | 71.92% / 71.92% / 71.92% |

### Captured load activations

These are the distinct armed activation records captured by the exercised flows, not a five-load matched campaign. Repeated reads of one record are counted once. Every ≥30 ms field is a load-latency flag. Main-thread values over the 50 ms typical native target are stated explicitly; background fields are not main-thread pauses.

| Run / first captured label | Had payload / chars | Background read / create / validate / main-thread ms | ≥30 ms fields / main >50 ms |
|---|---|---|---|
| base-declared / save-1 | True / 5,014,732 | provisional (PC shared: base-declared) 874.034 / 0.658 / 348.564 / 60.261 | backgroundReadMs, backgroundValidateMs, mainThreadMs; main >50=True |
| base-save / activation | True / 5,014,732 | provisional (PC shared: base-save) 1007.858 / 0.758 / 415.388 / 68.300 | backgroundReadMs, backgroundValidateMs, mainThreadMs; main >50=True |
| base-types / midnightsave-1 | True / 5,014,732 | provisional (PC shared: base-types) 879.632 / 0.791 / 365.083 / 66.993 | backgroundReadMs, backgroundValidateMs, mainThreadMs; main >50=True |
| head-business-perf / d2-06-10-cost | True / 3,397,680 | provisional (PC shared: head-business-perf) 1478.601 / 0.978 / 508.671 / 78.470 | backgroundReadMs, backgroundValidateMs, mainThreadMs; main >50=True |
| head-declared / save-1 | True / 1,171,321 | provisional (PC shared: head-declared) 1020.192 / 0.720 / 318.296 / 63.841 | backgroundReadMs, backgroundValidateMs, mainThreadMs; main >50=True |
| head-declared-types / midnightsave-1 | True / 3,964,095 | provisional (PC shared: head-declared-types) 1535.919 / 0.925 / 502.692 / 69.309 | backgroundReadMs, backgroundValidateMs, mainThreadMs; main >50=True |
| head-reload / activation | True / 1,503,604 | provisional (PC shared: head-reload) 796.404 / 0.122 / 291.726 / 28.718 | backgroundReadMs, backgroundValidateMs; main >50=False |
| head-save / activation | True / 1,171,321 | provisional (PC shared: head-save) 954.439 / 0.702 / 295.273 / 70.380 | backgroundReadMs, backgroundValidateMs, mainThreadMs; main >50=True |
| head-types-r2 / midnightsave-1 | True / 1,171,321 | provisional (PC shared: head-types-r2) 1277.270 / 1.015 / 313.047 / 69.240 | backgroundReadMs, backgroundValidateMs, mainThreadMs; main >50=True |

Ambient load for these runs is retained in [timing.json](rig-2026-11-09/timing.json), `runs`; this is provisional shared-PC load evidence, with no release load-gate claim.


### Input for the F7e decision

Current PC encode sits **outside and before** the portrait/screenshot request: PC runs synchronously in onSaveGame, followed by those native calls, as documented in [F7 §2.2/§3.5](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/plans/f7-save-snapshots.md#22-native-save-contract-read-only-ilspy-inspection-installed-bigambitionsdll). In the representative head Default samples, encode is provisional (PC shared: S1) **374.440 / 400.655 / 400.655 ms**, while portrait plus screenshot dispatch is provisional (PC shared: S1) **10.131 / 18.013 / 18.013 ms**; capture is 2.435 / 3.743 / 3.743% of encode. The observed subscriber order also places RLO’s save hook after PC: remaining subscribers plus portrait/dispatch total provisional (PC shared: S1) **11.079 / 42.467 / 42.467 ms**. Capture satisfies the ≤25% premise, but this measured overlap opportunity is small beside encode. Inference: portrait-only F7e would recover a small part of the pause; even the broader measured post-PC interval would not approach the typical pause budget. The profiler measures screenshot dispatch, not asynchronous capture completion or a timestamped worker join. This is input against relying on portrait overlap for the target pause, not proof that a worker has no benefit. The final matched batch remains necessary; no F7e implementation or frozen-plan change is made here.

## Performance

Every timing in this section is **provisional (PC shared)**: Peter was using the PC and other sessions were present. Only the owned background player ran at High. Fixed camera, default 48/24 body caps, speed 1 and PC clock factor 1/3; speed-6 positioning is outside named windows. No foreground input or controlled camera/cadence comparison. A non-serving player business exercises ownership/suppression, not successful checkout.

RIG #67 armed **55 bindings, complete=true**, no missing components/patch failures. The union covers mod Unity messages, Harmony prefixes/postfixes/finalizers and injected calls, registered GlobalEvents/scene subscribers, lifecycle and component scopes; nested intervals are counted once. **Coroutines, worker threads and callbacks registered with other native events are excluded**, and observer overhead stays in the union. Thus it is the rig’s full supported main-thread union, with that scope. Whole-frame time is separate.

| Window / day-part | Frames | Shared-PC condition | PC mod-work p50 / p95 / max ms | ≥30 ms intervals | Strict observed gate | Whole-frame p50 / p95 / max ms (separate) |
|---|---:|---|---|---:|---|---|
| `sanity` / speed=1 | 3,486 | CPU 13.7–17.8%; RAM 75.5–75.7%; dotnet/testhost/MSBuild max 0/0/0; assistant CLI count 6–8 | provisional (PC shared: sanity) 2.0986 / 4.4982 / 14.1751 | 0 | PASS | provisional (PC shared: sanity) 33.3370 / 33.5748 / 120.0855 |
| `d2-06-10` / d2-06-10.speed=1.high.shared | 23,266 | CPU 9.9–71.8%; RAM 66.1–84.0%; dotnet/testhost/MSBuild max 0/0/0; assistant CLI count 6–10 | provisional (PC shared: d2-06-10) 1.2918 / 4.5500 / 419.4360 | 3 | FAIL: 3 mod-work intervals >= 30 ms (allowed 0): {"PopulationRuntime.Update": 2, "advancement": 1} | provisional (PC shared: d2-06-10) 33.3361 / 33.7142 / 494.0376 |
| `d2-10-14` / d2-10-14.speed=1.high.shared | 22,789 | CPU 10.4–71.3%; RAM 65.7–82.7%; dotnet/testhost/MSBuild max 0/0/0; assistant CLI count 6–10 | provisional (PC shared: d2-10-14) 1.1160 / 4.0232 / 19.6775 | 0 | PASS | provisional (PC shared: d2-10-14) 33.3362 / 33.6998 / 221.3541 |
| `d2-14-18` / d2-14-18.speed=1.high.shared | 23,964 | CPU 8.4–71.7%; RAM 63.6–79.7%; dotnet/testhost/MSBuild max 0/0/0; assistant CLI count 6–10 | provisional (PC shared: d2-14-18) 0.9842 / 5.0849 / 130.8769 | 1 | FAIL: mod-work p95 5.0849 ms is not below 5 ms; 1 mod-work intervals >= 30 ms (allowed 0): {"advancement": 1} | provisional (PC shared: d2-14-18) 33.3358 / 33.6910 / 185.3519 |
| `d2-18-22` / d2-18-22.speed=1.high.shared | 25,052 | CPU 8.1–69.5%; RAM 61.9–78.1%; dotnet/testhost/MSBuild max 0/0/0; assistant CLI count 6–10 | provisional (PC shared: d2-18-22) 1.3446 / 5.0698 / 98.8913 | 1 | FAIL: mod-work p95 5.0698 ms is not below 5 ms; 1 mod-work intervals >= 30 ms (allowed 0): {"advancement": 1} | provisional (PC shared: d2-18-22) 33.3352 / 33.5623 / 170.2631 |
| `d2-22-d3-02` / d2-22-d3-02.speed=1.high.shared | 22,320 | CPU 6.5–11.0%; RAM 61.8–63.2%; dotnet/testhost/MSBuild max 0/0/0; assistant CLI count 6–6 | provisional (PC shared: d2-22-d3-02) 0.6563 / 1.1944 / 15.8712 | 0 | PASS | provisional (PC shared: d2-22-d3-02) 33.3353 / 33.6206 / 1057.8075 |
| `d3-02-07` / d3-02-07.speed=1.high.shared | 27,228 | CPU 7.3–13.0%; RAM 61.7–62.8%; dotnet/testhost/MSBuild max 0/0/0; assistant CLI count 6–6 | provisional (PC shared: d3-02-07) 0.5222 / 0.9423 / 13.8593 | 0 | PASS | provisional (PC shared: d3-02-07) 33.3352 / 33.5355 / 89.1484 |
| `morning` / day3.morning.speed=1.high.shared | 15,066 | CPU 7.5–14.7%; RAM 62.6–63.3%; dotnet/testhost/MSBuild max 0/0/0; assistant CLI count 6–6 | provisional (PC shared: morning) 1.4380 / 4.9854 / 33.2674 | 1 | FAIL: 1 mod-work intervals >= 30 ms (allowed 0): {"advancement": 1} | provisional (PC shared: morning) 33.3353 / 33.5374 / 4365.2646 |
| `lunch` / day3.lunch.speed=1.high.shared | 17,003 | CPU 9.5–17.1%; RAM 62.6–67.2%; dotnet/testhost/MSBuild max 0/0/0; assistant CLI count 6–6 | provisional (PC shared: lunch) 0.6880 / 2.2003 / 118.7278 | 1 | FAIL: 1 mod-work intervals >= 30 ms (allowed 0): {"PopulationRuntime.Update": 1} | provisional (PC shared: lunch) 33.3362 / 33.5531 / 796.2370 |
| `evening` / day3.evening.speed=1.high.shared | 18,918 | CPU 10.3–21.6%; RAM 62.8–64.4%; dotnet/testhost/MSBuild max 0/0/0; assistant CLI count 6–6 | provisional (PC shared: evening) 1.2853 / 4.8478 / 429.2408 | 3 | FAIL: 3 mod-work intervals >= 30 ms (allowed 0): {"PopulationRuntime.Update": 2, "physical_population": 1} | provisional (PC shared: evening) 33.3357 / 33.5558 / 479.6478 |

Strict observed gate: mod-work p95 <5 ms, zero single intervals ≥30 ms, complete/valid timing and zero save callbacks. The rig default end-step checks p95 and flags spikes; the independent existing judge was also applied with `max_spikes=0`. A whole frame near 33.3 ms is the normal 30 FPS cadence and is not a PC interval. Maxima/tails above cadence are retained separately; no cause is inferred from whole-frame duration alone.

### Auxiliary hourly p95 flags

The recorder also labels native hours inside each window. Full-hour conditions below reached p95 ≥5 ms even where a longer-window p95 was lower; they must not be hidden by the longer aggregation. Three-frame end-boundary fragments are retained in JSON but are not treated as full-hour windows. Conditions share the parent window’s sampled load.

| Parent window / native-hour condition | Frames | PC p95 ms |
|---|---:|---|
| d2-06-10 / d2-06-10.speed=1.high.shared; hour=06 | 6,192 | provisional (PC shared: d2-06-10) 5.2084 |
| d2-06-10 / d2-06-10.speed=1.high.shared; hour=08 | 5,812 | provisional (PC shared: d2-06-10) 5.0631 |
| d2-10-14 / d2-10-14.speed=1.high.shared; hour=12 | 5,813 | provisional (PC shared: d2-10-14) 5.0369 |
| d2-14-18 / d2-14-18.speed=1.high.shared; hour=16 | 5,979 | provisional (PC shared: d2-14-18) 5.3077 |
| d2-14-18 / d2-14-18.speed=1.high.shared; hour=17 | 7,011 | provisional (PC shared: d2-14-18) 5.6814 |
| d2-18-22 / d2-18-22.speed=1.high.shared; hour=18 | 6,624 | provisional (PC shared: d2-18-22) 5.6056 |
| d2-18-22 / d2-18-22.speed=1.high.shared; hour=19 | 6,273 | provisional (PC shared: d2-18-22) 5.1552 |
| morning / day3.morning.speed=1.high.shared; hour=08 | 6,341 | provisional (PC shared: morning) 5.3480 |
| evening / day3.evening.speed=1.high.shared; hour=17 | 3,401 | provisional (PC shared: evening) 5.4371 |

### Every ≥30 ms union interval

| Window / condition | Frame | PC interval ms | Dominant self component / group | Attribution |
|---|---:|---:|---|---|
| d2-06-10 / d2-06-10.speed=1.high.shared; hour=06 | 4 | provisional (PC shared: d2-06-10) 419.4360 | PopulationRuntime.Update (416.7575 ms self) / frame_update | First speed-6→1 resume/clock-retiming frames; Update self dominates. Exact native-callee/GC cause unresolved. |
| d2-06-10 / d2-06-10.speed=1.high.shared; hour=06 | 51 | provisional (PC shared: d2-06-10) 139.4938 | PopulationRuntime.Update (125.1369 ms self) / frame_update | First speed-6→1 resume/clock-retiming frames; Update self dominates. Exact native-callee/GC cause unresolved. |
| d2-06-10 / d2-06-10.speed=1.high.shared; hour=08 | 16147 | provisional (PC shared: d2-06-10) 44.8271 | advancement (42.494 ms self) / advancement | Matches production max 42.483 ms, unit route-planning 39.688 ms; optional reads 0.0 ms; GC completed during advance=1; park scans in observation=0. Exact native callee unresolved. |
| d2-14-18 / d2-14-18.speed=1.high.shared; hour=17 | 19177 | provisional (PC shared: d2-14-18) 130.8769 | advancement (129.3333 ms self) / advancement | Matches production max 129.328 ms, unit citizen 129.271 ms; optional reads 128.755 ms; GC completed during advance=0; park scans in observation=0. Exact native callee unresolved. |
| d2-18-22 / d2-18-22.speed=1.high.shared; hour=19 | 6816 | provisional (PC shared: d2-18-22) 98.8913 | advancement (97.3305 ms self) / advancement | Matches production max 97.324 ms, unit citizen 97.232 ms; optional reads 97.089 ms; GC completed during advance=0; park scans in observation=1. Exact native callee unresolved. |
| morning / day3.morning.speed=1.high.shared; hour=07 | 181 | provisional (PC shared: morning) 33.2674 | advancement (32.444 ms self) / advancement | Matches production max 32.438 ms, unit route-planning 29.688 ms; optional reads 0.0 ms; GC completed during advance=1; park scans in observation=0. Exact native callee unresolved. |
| lunch / day3.lunch.speed=1.high.shared; hour=11 | 44 | provisional (PC shared: lunch) 118.7278 | PopulationRuntime.Update (110.0432 ms self) / frame_update | First speed-6→1 resume/clock-retiming frames; Update self dominates. Exact native-callee/GC cause unresolved. |
| evening / day3.evening.speed=1.high.shared; hour=17 | 27 | provisional (PC shared: evening) 429.2408 | PopulationRuntime.Update (426.5985 ms self) / frame_update | First speed-6→1 resume/clock-retiming frames; Update self dominates. Exact native-callee/GC cause unresolved. |
| evening / day3.evening.speed=1.high.shared; hour=17 | 75 | provisional (PC shared: evening) 139.9181 | PopulationRuntime.Update (114.5592 ms self) / frame_update | First speed-6→1 resume/clock-retiming frames; Update self dominates. Exact native-callee/GC cause unresolved. |
| evening / day3.evening.speed=1.high.shared; hour=17 | 79 | provisional (PC shared: evening) 48.6576 | physical_population (47.1899 ms self) / physical_population | Timed PC callback including native callees/observer overhead; exact native-callee/GC cause is not exposed. |
| None observed in completed union windows | — | — | — | — |

All listed spikes and per-component counts/totals are in [timing.json](rig-2026-11-09/timing.json). Recorder validity, dropped/cross-frame/unbalanced counters and no-save assertions are retained. Population summaries supply coarse advancement attribution where possible; an observation is not a count of every individual interval.

### Supplemental whole-frame stall traces (separate)

RLO also emits throttled native whole-frame diagnostics. The counts below are **reported traces ≥100 ms**, not all stalls; clock-command segments can include between-window readbacks. They do not enter the PC gate. Full separate frame quantiles remain in the main performance table.

| Clock-command segment | Reported traces ≥100 ms | Largest reported whole frame ms |
|---|---:|---|
| speed=6 until day 2 06:00 | 13 | provisional (PC shared: head-business-perf) 487 |
| speed=1 until day 2 10:00 | 31 | provisional (PC shared: head-business-perf) 473 |
| speed=1 until day 2 14:00 | 31 | provisional (PC shared: head-business-perf) 221 |
| speed=1 until day 2 18:00 | 29 | provisional (PC shared: head-business-perf) 172 |
| speed=1 until day 2 22:00 | 6 | provisional (PC shared: head-business-perf) 170 |
| speed=1 until day 3 02:00 | 2 | provisional (PC shared: head-business-perf) 794 |
| speed=1 until day 3 09:30 | 2 | provisional (PC shared: head-business-perf) 4365 |
| speed=6 until day 3 11:30 | 2 | provisional (PC shared: head-business-perf) 467 |
| speed=1 until day 3 14:30 | 2 | provisional (PC shared: head-business-perf) 796 |
| speed=6 until day 3 17:30 | 2 | provisional (PC shared: head-business-perf) 352 |
| speed=1 until day 3 20:30 | 3 | provisional (PC shared: head-business-perf) 460 |

A corroborating trace near the day-2 optional-stop tail reports a provisional (PC shared: hour17, native trace machine30%/other25%) 140 ms whole frame, 131 ms scripts’ Update and coarse 141 ms main-thread CPU, no GC and 2,871 page faults. Its phase/time agreement supports active work in the PC/native read path; it is not an exact per-call CPU profile, and page faults do not establish hard paging. Other large frames are dominated by LateUpdate and remain separate. Structured traces and the matching excerpt are in [wholeframe-traces.json](rig-2026-11-09/wholeframe-traces.json).

The largest supplemental trace is provisional (PC shared: speed=1 until day 3 09:30) **4365 ms** whole frame, with coarse main-thread CPU **31.0 ms**, GC work **0.0 ms**, not-saving=True and no-scene-change=True. It remains outside the measured PC attribution; the blocking point is unresolved.

### Outside-window flags and #128 attribution

Per-run ambient CPU/RAM/process-count ranges, including the startup/preparation runs below, are retained in [timing.json](rig-2026-11-09/timing.json), `runs`. A run name in a provisional qualifier refers to that recorded shared-PC condition.

| Run / condition | Coarse observation count | Max advance ms | Slowest unit | Optional-stop reads ms | GC / park scans |
|---|---:|---|---|---|---|
| base-declared; speed=6 until day 2 00:30; logical minute 2537.9 | 1 | provisional (PC shared: base-declared) 120.131 | citizen 119.937 ms | 119.645 | 0 / 0 |
| head-business-age; speed=6 until day 2 00:30; logical minute 2500.8 | 1 | provisional (PC shared: head-business-age) 109.736 | citizen 109.583 ms | 109.271 | 0 / 0 |
| head-business-age; speed=6 until day 2 00:30; logical minute 2843.7 | 1 | provisional (PC shared: head-business-age) 40.304 | route-planning 40.03 ms | 0.0 | 1 / 0 |
| head-business-perf; speed=6 until day 2 06:00; logical minute 2975.7 | 1 | provisional (PC shared: head-business-perf) 40.651 | route-planning 39.003 ms | 0.0 | 1 / 0 |
| head-declared; speed=6 until day 2 00:30; logical minute 2554.3 | 1 | provisional (PC shared: head-declared) 99.452 | citizen 99.349 ms | 99.085 | 0 / 0 |

These are prep/accelerated-play observations, outside union gate windows. At least one interval reached each reported maximum; the block counters do not identify every interval. The head/base evening optional-stop tails occurred with zero completed GC and zero park-trigger scans, pointing to the optional-stop read path rather than repeated park scans; native-callee/OS attribution is unresolved. The day-1 late route-planning tail completed one GC. Loading/scene-discovery work is outside normal-play windows and not promoted to a clean-window defect. This extends #128’s attribution; it does not show that its cause is fixed.

Other reported PC component maxima ≥30 ms are retained below. Counts are coarse observation maxima, or distinct reported lifetime maxima where labelled; they overlap the advancement/union flags and must not be summed as independent intervals. Exact condition/line/logical minute is in [component-flags.json](rig-2026-11-09/component-flags.json). Native-callee/GC attribution for these counters is unavailable.

| Run / condition | Component | Reported maxima count | Maximum ms |
|---|---|---:|---|
| base-declared; startup | preparation_frame | 1 | provisional (PC shared: base-declared) 97.501 |
| base-declared; startup | planner_scene_search_lifetime | 1 | provisional (PC shared: base-declared) 89.776 |
| base-declared; speed=6 until day 2 00:30 | optional_stop_read | 1 | provisional (PC shared: base-declared) 118.440 |
| base-producer; startup | preparation_frame | 1 | provisional (PC shared: base-producer) 106.245 |
| base-producer; startup | planner_scene_search_lifetime | 1 | provisional (PC shared: base-producer) 98.733 |
| base-save; startup | preparation_frame | 1 | provisional (PC shared: base-save) 113.843 |
| base-save; startup | planner_scene_search_lifetime | 1 | provisional (PC shared: base-save) 105.635 |
| base-types; startup | preparation_frame | 1 | provisional (PC shared: base-types) 95.512 |
| base-types; startup | planner_scene_search_lifetime | 1 | provisional (PC shared: base-types) 88.247 |
| head-business-age; startup | preparation_frame | 1 | provisional (PC shared: head-business-age) 91.291 |
| head-business-age; startup | planner_scene_search_lifetime | 1 | provisional (PC shared: head-business-age) 84.069 |
| head-business-age; speed=6 until day 2 00:30 | optional_stop_read | 1 | provisional (PC shared: head-business-age) 108.480 |
| head-business-perf; startup | preparation_frame | 1 | provisional (PC shared: head-business-perf) 111.122 |
| head-business-perf; startup | planner_scene_search_lifetime | 1 | provisional (PC shared: head-business-perf) 101.110 |
| head-business-perf; speed=1 until day 2 10:00 | closure_refresh | 1 | provisional (PC shared: head-business-perf) 52.633 |
| head-business-perf; speed=1 until day 2 18:00 | optional_stop_read | 1 | provisional (PC shared: head-business-perf) 127.616 |
| head-business-perf; speed=1 until day 2 22:00 | optional_stop_read | 1 | provisional (PC shared: head-business-perf) 97.080 |
| head-business-perf; speed=1 until day 3 20:30 | closure_refresh | 1 | provisional (PC shared: head-business-perf) 31.011 |
| head-declared; startup | preparation_frame | 1 | provisional (PC shared: head-declared) 120.440 |
| head-declared; startup | planner_scene_search_lifetime | 1 | provisional (PC shared: head-declared) 110.428 |
| head-declared; speed=6 until day 2 00:30 | optional_stop_read | 1 | provisional (PC shared: head-declared) 98.364 |
| head-declared-types; startup | preparation_frame | 1 | provisional (PC shared: head-declared-types) 101.160 |
| head-declared-types; startup | planner_scene_search_lifetime | 1 | provisional (PC shared: head-declared-types) 91.811 |
| head-diag-r2; startup | preparation_frame | 1 | provisional (PC shared: head-diag-r2) 94.585 |
| head-diag-r2; startup | planner_scene_search_lifetime | 1 | provisional (PC shared: head-diag-r2) 87.557 |
| head-producer; startup | preparation_frame | 1 | provisional (PC shared: head-producer) 98.109 |
| head-producer; startup | planner_scene_search_lifetime | 1 | provisional (PC shared: head-producer) 90.656 |
| head-reload; startup | preparation_frame | 2 | provisional (PC shared: head-reload) 103.535 |
| head-reload; startup | planner_scene_search_lifetime | 2 | provisional (PC shared: head-reload) 101.137 |
| head-save; startup | preparation_frame | 1 | provisional (PC shared: head-save) 97.986 |
| head-save; startup | planner_scene_search_lifetime | 1 | provisional (PC shared: head-save) 89.925 |
| head-shop-2; startup | preparation_frame | 1 | provisional (PC shared: head-shop-2) 96.198 |
| head-shop-2; startup | planner_scene_search_lifetime | 1 | provisional (PC shared: head-shop-2) 88.726 |
| head-shop-3; startup | preparation_frame | 1 | provisional (PC shared: head-shop-3) 107.036 |
| head-shop-3; startup | planner_scene_search_lifetime | 1 | provisional (PC shared: head-shop-3) 99.413 |
| head-types-r2; startup | preparation_frame | 1 | provisional (PC shared: head-types-r2) 114.359 |
| head-types-r2; startup | planner_scene_search_lifetime | 1 | provisional (PC shared: head-types-r2) 106.743 |

## Findings

1. **FAIL — Provisional combined-work gate breach and optional-stop tail.** 2 main window(s) have p95 >=5 ms; 10 union intervals reach >=30 ms. All are provisional (PC shared), not a final release verdict. The day-2 afternoon 130.8769 ms union interval contains 129.3333 ms advancement; the production maximum 129.328 ms is a citizen unit with 128.755 ms optional-stop reads, zero completed GC and zero park scans. This identifies the timed path; individual native-callee/OS attribution is unavailable. A separate morning route-planning tail completed one GC; the first speed-6→1 frames are dominated by Update/retiming and are labelled as transitions. Likely follow-up owners: F3/PR #98 for combined-frame budget/route work; F2h/PR #159 (current optional-stop provider) with #128/PR #133 attribution; F2b/PR #151 for the inherited clock-retiming boundary. No matched-build run isolates one PR as a regression. **Minimal repro:** load the retained head Business Day2 Ready checkpoint at c2fc1b0, autosave guarded off, caps48/24, clock factor1/3, player High, fixed camera; position to D2 06:00 at speed6, then run speed1 through 18:00 with the supported union. Inspect d2-06-10 and d2-14-18 plus the population summary near logical minute3931.2 (D2 evening rush). For the final combined batch, repeat the named D3 morning/lunch/evening windows and compare the retained evidence.

2. **PARTIAL — Save measurements inform F7e but are unmatched.** Synchronous encode precedes the short portrait/screenshot-dispatch CPU window. Both representative builds completed all requested save types. Seeds differ, baseline cannot naturally reach the head declared load, peak transient is unavailable and async screenshot completion is not timed. Large save phases are flagged as save latency.

3. **NOT RUN — Serving fixture still unavailable.** Native setup succeeded on the third rental approach. PC feedback and a PC-absent control both refused croissant service; the control had four unavailable attempts, no paid sale and no lost/hook-fault log evidence. Keep the business as an ownership/setup asset; receipt/checkout assertions need a serving fixture. This is not assigned as a PC defect.

The generic RIG-PCSAVE JSON projection/marker comparator predates compact writer 34 and the current newline-v1 marker. Raw key extraction works; its `unknown` marker/empty projections are a tooling gap, not evidence of corrupt saves. The raw prefix/epoch lines and runtime acceptance were checked independently without introducing a new decoder or editing payloads.

## NOT RUN grouped by missing capability or fixture

- **arms (34 rows):** Needs the check-specific matched baseline/config/cadence/camera arms. Ordinary stationary head windows do not establish arm equivalence. Check IDs: `a5-12`, `b0b-4`, `c1-2`, `c1-5`, `c2a-s2-6`, `c2b-7`, `d2-7`, `e1-1`, `e1-2`, `e1-3`, `e1-5`, `e1-6`, `e3-cupcake-6`, `e3-donut-5`, `e3-frenchfries-8`, `e3-icecream-8`, `e3-pizza-4`, `e3-pizza-7`, `e3-remaining-9`, `e3-remaining-11`, `e3-salad-7`, `e4-6`, `e5a-5`, `f0-6`, `f1b-5`, `f2a-1`, `f2a-2`, `f2a-3`, `f2a-4`, `f2a-5`, `f2a-6`, `f5f-4`, `f5h-4`, `c2a-6`.
- **campaign (17 rows):** Needs a prepared campaign/order/payment fixture; campaign purchases were explicitly prohibited and none were made. Check IDs: `c1-1`, `c1-3`, `c1-4`, `c2b-1`, `c2b-2`, `c2b-3`, `c2b-4`, `c2b-5`, `c2b-6`, `c3-1`, `c3-2`, `c3-3`, `c3-4`, `c3-5`, `c3-6`, `c5e-agency-dialog-2`, `c5f-delivery-reports-2`.
- **config (5 rows):** Needs effective price-weight readback and explicit config/edit arms. The compiled default is 1.2, but the installed live projection does not expose it. Check IDs: `f6k-1`, `f6k-2`, `f6k-3`, `f6k-4`, `f6k-5`.
- **dynamic (6 rows):** Dynamic acquisition, dirty recovery and held-policy execution are not implemented on this head; no real mixed-product fixture or notification/publication trace was available. Check IDs: `e5a-2`, `e5a-3`, `e5a-4`, `e5b-4`, `e5b-5`, `e5b-6`.
- **fault (6 rows):** Needs the specified RIG-FAULT/relocation/modData-mutation arm. None was invoked; readbacks were read-only. Check IDs: `c5d-7`, `c5e-agency-dialog-5`, `f7b-3`, `f7b-4`, `f7b-5`, `f7b-6`.
- **historical (34 rows):** Needs the named historical fixture/build. This run created only fresh writer-33/34 cities; current saves do not verify the historical migration or inactive-writer assertion. Check IDs: `a3-8`, `a5-8`, `b0-1`, `b0-2`, `b0-3`, `b0-4`, `b0-5`, `b0-6`, `b0-7`, `b0b-3`, `c2a-s2-5`, `c4-1`, `c4-2`, `c4-3`, `c4-4`, `c5c-2`, `e1-4`, `e3-remaining-4`, `e5d-1`, `e5d-6`, `e6c-5`, `f5b-1`, `f5b-3`, `f5c-lite-2`, `f5c-lite-3`, `f5c-lite-5`, `f6d-2`, `f6d-3`, `f7f-1`, `f7f-2`, `f7f-3`, `f7f-4`, `f7p-1`, `c2a-5`.
- **ledger (49 rows):** Needs a serving fixture, exact per-person/receipt ledger export and matched intervention/week arms. Aggregate projections/native totals alone cannot prove this assertion. Check IDs: `f6a-2`, `f6a-3`, `f6a-4`, `f6a-5`, `f6a-6`, `f6a-7`, `f6a-8`, `f6a-9`, `f6b-1`, `f6b-2`, `f6b-4`, `f6b-5`, `f6b-6`, `f6b-7`, `f6b-8`, `f6c-1`, `f6c-4`, `f6c-5`, `f6c-7`, `f6c-8`, `f6e-2`, `f6e-3`, `f6e-4`, `f6e-5`, `f6e-6`, `f6g-2`, `f6g-3`, `f6g-4`, `f6g-5`, `f6g-6`, `f6g-7`, `f6g-8`, `f6h-2`, `f6h-3`, `f6h-4`, `f6h-5`, `f6h-6`, `f6h-7`, `f6h-8`, `f6h-9`, `f6h-10`, `f6h-12`, `f6h-13`, `f6h-14`, `f6h-15`, `f6h-17`, `f6h-18`, `f6h-19`, `f6h-20`.
- **menu (47 rows):** Needs the specified genuine product/holder/menu fixture and independent field/transaction evidence; the only acquired shop advertised croissants. Check IDs: `e3-cupcake-1`, `e3-cupcake-2`, `e3-cupcake-3`, `e3-cupcake-4`, `e3-cupcake-5`, `e3-donut-1`, `e3-donut-2`, `e3-donut-3`, `e3-donut-4`, `e3-frenchfries-1`, `e3-frenchfries-2`, `e3-frenchfries-3`, `e3-frenchfries-4`, `e3-frenchfries-5`, `e3-frenchfries-6`, `e3-frenchfries-7`, `e3-icecream-1`, `e3-icecream-2`, `e3-icecream-3`, `e3-icecream-4`, `e3-icecream-5`, `e3-icecream-6`, `e3-icecream-7`, `e3-pizza-1`, `e3-pizza-2`, `e3-pizza-3`, `e3-pizza-5`, `e3-pizza-6`, `e3-remaining-1`, `e3-remaining-2`, `e3-remaining-3`, `e3-remaining-5`, `e3-remaining-6`, `e3-remaining-7`, `e3-remaining-8`, `e3-remaining-10`, `e3-salad-1`, `e3-salad-2`, `e3-salad-3`, `e3-salad-4`, `e3-salad-5`, `e3-salad-6`, `e4-1`, `e4-2`, `e4-3`, `e4-4`, `e4-5`.
- **metadata (5 rows):** Needs metadata override/bad-config arms and named likes/held-state fixtures; available metadata staging was not exercised in this intermediate batch. Check IDs: `e5c-1`, `e5c-2`, `e5c-3`, `e5c-4`, `e5c-5`.
- **ordinary (5 rows):** Needs a selected ordinary venue/admission/transfer fixture, exact debit join and controlled access/endowment arms. Check IDs: `f6f-1`, `f6f-2`, `f6f-3`, `f6f-5`, `f6f-6`.
- **outside_candidate (16 rows):** Merged after c2fc1b0 while this run was active. Not in the tested candidate; no native proof for this later implementation is claimed. Check IDs: `e5b2-1`, `e5b2-2`, `e5b2-3`, `e5b2-4`, `f2c-1`, `f2c-2`, `f2c-3`, `f2c-4`, `f2c-5`, `f2d-1`, `f2d-2`, `f2d-3`, `f2d-4`, `f2d-5`, `f2d-6`, `f2d-7`.
- **physical (44 rows):** Needs the specified body/camera/selection or input replay arms and compact semantic comparisons; no real input or foreground was used. Check IDs: `e2a-1`, `e2a-2`, `e2a-3`, `e2a-4`, `e2a-5`, `e2a-6`, `e2a-7`, `e2a-8`, `e2b-1`, `e2b-2`, `e2b-3`, `e2b-4`, `e2b-5`, `e2b-6`, `e2b-7`, `e2c-1`, `e2c-2`, `e2c-3`, `e2c-4`, `f0-1`, `f0-2`, `f0-3`, `f0-4`, `f0-5`, `f0-7`, `f1-shop-view-1`, `f1-shop-view-2`, `f1-shop-view-3`, `f1-shop-view-4`, `f1-shop-view-5`, `f1-shop-view-6`, `f1-shop-view-7`, `f1b-1`, `f1b-2`, `f1b-3`, `f1b-4`, `f1b-6`, `f1b-7`, `f1b-8`, `f2b-1`, `f2b-2`, `f2b-3`, `f2f-1`, `f2f-3`.
- **provider (3 rows):** Needs an actual metadata provider/collision/lifecycle fixture and held-state trace; no provider variant was installed. Check IDs: `e5e-1`, `e5e-2`, `e5e-3`.
- **replay (39 rows):** Needs named interrupted-state checkpoints and a writer-34 semantic comparator. RIG-PCSAVE extracts exact keys, but its generic projections do not decode the compact wire. Check IDs: `a5-1`, `a5-2`, `a5-3`, `a5-4`, `a5-5`, `a5-6`, `a5-7`, `a5-9`, `a5-10`, `a5-11`, `b0b-1`, `b0b-2`, `b0b-5`, `e5d-2`, `e5d-3`, `e5d-4`, `e5d-5`, `f1-accounting-1`, `f1-accounting-2`, `f1-accounting-3`, `f1-accounting-4`, `f1-accounting-5`, `f1-accounting-6`, `f5d-2`, `f5d-5`, `f5d-7`, `f5e-2`, `f5e-5`, `f5g-6`, `f7a-2`, `f7a-7`, `f7c-1`, `f7c-3`, `f7c-4`, `f7d-3`, `f3-2`, `f3-3`, `f3-4`, `f3-5`.
- **research (26 rows):** Needs an ordered/surveyed/paid/declined research fixture and its billing/delivery trace; this run read idle contracts only under the instruction never to buy anything in the C5 checks. Check IDs: `c5a-1`, `c5a-2`, `c5a-3`, `c5a-4`, `c5b-1`, `c5b-2`, `c5b-3`, `c5b-4`, `c5c-3`, `c5c-4`, `c5c-5`, `c5c-6`, `c5d-2`, `c5d-3`, `c5d-4`, `c5d-5`, `c5d-6`, `c5d-8`, `c5e-agency-dialog-3`, `c5f-delivery-reports-3`, `c5f-delivery-reports-4`, `c5f-delivery-reports-5`, `c5f-delivery-reports-6`, `c5f-delivery-reports-7`, `c5f-delivery-reports-9`, `c5f-delivery-reports-10`.
- **serving (70 rows):** Needs a serving food fixture and exact PC receipt/order/wallet joins. The acquired shop also refused native-control service (unavailable); setup alone is not checkout proof. Check IDs: `a3-1`, `a3-2`, `a3-3`, `a3-4`, `a3-5`, `a3-6`, `a3-7`, `a3-9`, `a3-10`, `a3-11`, `a3-12`, `b1-1`, `b1-2`, `b1-3`, `b1-4`, `b1-5`, `b1-6`, `b1-7`, `b1-8`, `b2-1`, `b2-2`, `b2-3`, `b2-4`, `b2-5`, `b2-6`, `b2-7`, `b2-8`, `c2a-s2-1`, `c2a-s2-2`, `c2a-s2-3`, `c2a-s2-4`, `c2a-s2-7`, `d1-1`, `d1-2`, `d1-3`, `d1-4`, `d1-5`, `d2-1`, `d2-2`, `d2-3`, `d2-4`, `d2-5`, `d2-6`, `d2-8`, `d3a-1`, `d3a-2`, `d3a-3`, `d3a-4`, `d3a-5`, `d3a-6`, `d4-1`, `d4-2`, `d4-3`, `d4-4`, `d4-5`, `d4-6`, `f5g-5`, `f6i-1`, `f6i-2`, `f6i-3`, `f6j-1`, `f6j-2`, `f6j-3`, `f6j-4`, `f6j-5`, `c2a-1`, `c2a-2`, `c2a-3`, `c2a-4`, `c2a-7`.
- **stress (7 rows):** Needs RIG-PCFIX/fixture builder for the named 10k/50k/100k or controlled population. Native fresh-city growth reached 2,871 persons only. Check IDs: `f5g-2`, `f5g-3`, `f5h-2`, `f7a-5`, `f7c-5`, `f7d-5`, `f7p-2`.
- **ui (2 rows):** Needs automated inspector/panel geometry, screenshots and controlled UI replay. Those arms were not exercised in this background batch. Check IDs: `f5c-lite-4`, `f5h-6`.
- **vendor (20 rows):** Needs the Street Food vendor API and vehicle fixture. The pinned mod set returned bridge=Absent (Street Food vendor API missing). Check IDs: `e6b-2`, `e6b-3`, `e6b-4`, `e6b-5`, `e6b-6`, `e6b-7`, `e6c-2`, `e6c-3`, `e6c-4`, `e6c-6`, `e6d-2`, `e6d-3`, `e6d-4`, `e6d-5`, `e6d-6`, `e6d-7`, `e6d-8`, `e6e-1`, `e6e-2`, `e6e-3`.

Available tooling is not marked missing merely because an issue remains open: RIG-SAVETYPES, RIG-SAVEPROF, PCSTATE, raw PCSAVE extraction and the supported union were exercised. Conversely, placeholder PCFIX population controls, compact semantic comparison, exact PC ledger/export, controlled weekly/access/shift fixtures and unimplemented E5b execution were not assumed. UI arms not exercised here are listed as such; background operation itself did not require foreground/real input.

## Fixtures

Both fresh FT cities and the player business **exist**. Native saves remain in the isolated agent1 profile and are copied, with metadata/portraits, to `<stage>\fixtures`. `AGENT1_PROFILE` means `%USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests`; this is the rig profile, never the ordinary profile. The external `<stage>\evidence-audit.json` records resolved absolute paths and SHA-256 for every created file; the committed [fixture manifest](rig-2026-11-09/fixtures.json) uses this alias to avoid a personal path. No save is committed or deleted.

| Logical fixture / checkpoint | Native file under AGENT1_PROFILE/SaveGames/Big Ambitions | SHA-256 | Reuse for final batch |
|---|---|---|---|
| FT E5b PC Rig1109 Head c2fc1b0 Ready | `nmDTcedbWUOhPd2Hrm2IgQ==/FT E5b PC Rig1109 Head c2fc1b0 Ready.hsg` | `af853176d3754cda088b4a3919b0acba4ad8daff6af97da69b9697b70d689886` | Yes, writer-34 starting city if the final candidate still accepts it |
| FT E5b PC Rig1109 Base f8a7f5d Ready | `1I6Vh5uCKUGwhLbkAa5Ag==/FT E5b PC Rig1109 Base f8a7f5d Ready.hsg` | `9ef84a34969ba3593ef96967d6401c71a3e5e6297d624fda40c54ae839ef4d40` | Base-only writer-33 reference/refusal asset; not a positive head fixture |
| FT PC Rig1109 Head c2fc1b0 Business | `nmDTcedbWUOhPd2Hrm2IgQ==/FT rig checkpoint 125e48d3de7c431ea4498bd75b293093.hsg` | `43e49b82e9f196429dd729fd220686c7a880747867e1459a2597965f6a0059f2` | Ownership/setup only; not serving |
| FT PC Rig1109 Head Business Day2 Ready | `nmDTcedbWUOhPd2Hrm2IgQ==/FT rig checkpoint 0a5213660d0441d5ad68c2866f3ba659.hsg` | `21058c8983796fdfacaafbd7d83a1765fd1a7f1e6c78c7c26ddba03ef1a06e73` | Ownership/timing setup only; native-control service unavailable |
| FT PC Rig1109 Head Business Day3 Ready | `nmDTcedbWUOhPd2Hrm2IgQ==/FT rig checkpoint eaff59fe8d534721b92dc5f648077006.hsg` | `1dc5a00c48e6add8b94b9ddff25e27d39a11400d64bf3cdf868bfac081dd6832` | Ownership/setup continuation; no serving proof |
| FT PC Rig1109 Head Business Day3 Tested | `nmDTcedbWUOhPd2Hrm2IgQ==/FT rig checkpoint 794cfe18b1d246d9a7f2e7f4a6eec795.hsg` | `0825c15cd946e1ea7d688ca535288b07a941481152a2ae19e7123c788128b965` | Writer-34 continuation/setup only; verify serving before checkout use |

All additional retained timed-save/refusal/declared-load checkpoints and their exact hashes are in the fixture manifest. The documented save campaign restores/removes temporary native recovery slots; those profiling outputs are not listed as reusable checkpoints. Retained FT Default checkpoints, metadata and portraits stay in profile/stage. Writer changes require newly produced positive cities; these assets never authorise migration or use of an ordinary save.

The final declared-load native recovery-slot outputs were also copied before cleanup, solely from this batch’s head FT character. These external archives have not been re-imported/reloaded; reuse is unverified.

| Native output | Retained archive | SHA-256 |
|---|---|---|
| Recover Midnight.hsg | `<stage>\fixtures\native-recovery-head-declared-types\Recover Midnight.hsg` | `0384413e65e067e7de9d5d27485c482af3cd83a61391b6e98bd59493fc3e6bdf` |
| Recover #0.hsg | `<stage>\fixtures\native-recovery-head-declared-types\Recover #0.hsg` | `afb0cb664f0e7e30f1ca924a0e1e14bff59c1f805a7f8762aff02cf019d68620` |
| Recover #1.hsg | `<stage>\fixtures\native-recovery-head-declared-types\Recover #1.hsg` | `5712b0fc30faba1eaa39f1e40ac0a9e1c1717df0e8c7d11d2781c2e9d4124871` |
| Recover #2.hsg | `<stage>\fixtures\native-recovery-head-declared-types\Recover #2.hsg` | `5e03399f39a7e8caa91327af1622c9b66033f4715a6be6f284351efcda410c41` |

## Restoration and lease

Protected-manifest verification: **303/303 entries** match (301 file SHA-256 hashes and 2 original absences); ModsLocal extras=0; isolated players remaining=0; owned flow workers remaining=0. All recorded lifecycle restores verified the isolated registry key and preferences. Documented overlay rollback restored the initial install revision. Lease status and final repository/process checks are in [cleanup.json](rig-2026-11-09/cleanup.json). Disposable saves intentionally remain.

## Per-check results

Rows retain the 6 November IDs: 494 rows in 77 candidate files, plus 16 NOT RUN rows in the three slices merged while this run was active (510 rows total). The two C2a handoff sections retain separate `c2a-*` / `c2a-s2-*` IDs, as in the earlier catalogue. Historical writer numbers and migration requests remain in their owning files; current-writer smoke does not silently pass them. PASS means the full stated assertion was observed; PARTIAL names the observed subset. Flow setup/input failures do not become product FAILs.

The short requirement labels refer to the grouped NOT RUN explanation above. [checks.json](rig-2026-11-09/checks.json) is a compact excerpt (id, status, group, key); full check titles and unabridged per-row requirements are in the archived blob `05d505b:docs/checks/rig-2026-11-09/checks.json` (SHA-256 in the excerpt's `archive` block); native/public diagnostic readbacks are in [readbacks.json](rig-2026-11-09/readbacks.json), key hashes/prefixes in [headers.json](rig-2026-11-09/headers.json), and save assertion audits in [save-audit.json](rig-2026-11-09/save-audit.json).

### [a3-weighted-choice.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/a3-weighted-choice.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `a3-1` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `a3-2` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `a3-3` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `a3-4` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `a3-5` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `a3-6` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `a3-7` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `a3-8` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `a3-9` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `a3-10` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `a3-11` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `a3-12` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |

### [a5-integrated-acceptance.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/a5-integrated-acceptance.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `a5-1` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `a5-2` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `a5-3` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `a5-4` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `a5-5` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `a5-6` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `a5-7` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `a5-8` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `a5-9` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `a5-10` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `a5-11` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `a5-12` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |

### [b0-schema18.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/b0-schema18.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `b0-1` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `b0-2` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `b0-3` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `b0-4` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `b0-5` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `b0-6` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `b0-7` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |

### [b0b-held-tuning.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/b0b-held-tuning.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `b0b-1` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `b0b-2` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `b0b-3` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `b0b-4` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |
| `b0b-5` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |

### [b1-memory-loyalty.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/b1-memory-loyalty.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `b1-1` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `b1-2` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `b1-3` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `b1-4` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `b1-5` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `b1-6` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `b1-7` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `b1-8` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |

### [b2-regulars-inspector.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/b2-regulars-inspector.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `b2-1` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `b2-2` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `b2-3` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `b2-4` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `b2-5` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `b2-6` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `b2-7` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `b2-8` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |

### [c1-campaign-reader.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/c1-campaign-reader.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `c1-1` | NOT RUN | Native campaign purchase/setup was explicitly prohibited for this run; no campaign buy/stop flow was invoked. |
| `c1-2` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |
| `c1-3` | NOT RUN | Needs prepared campaign/payment fixture; purchases prohibited. |
| `c1-4` | NOT RUN | Needs prepared campaign/payment fixture; purchases prohibited. |
| `c1-5` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |

### [c2a-core-awareness.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/c2a-core-awareness.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `c2a-s2-1` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `c2a-s2-2` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `c2a-s2-3` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `c2a-s2-4` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `c2a-s2-5` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `c2a-s2-6` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |
| `c2a-s2-7` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `c2a-1` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `c2a-2` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `c2a-3` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `c2a-4` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `c2a-5` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `c2a-6` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |
| `c2a-7` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |

### [c2b-campaign-awareness.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/c2b-campaign-awareness.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `c2b-1` | NOT RUN | Native campaign purchase/setup was explicitly prohibited for this run; no campaign buy/stop flow was invoked. |
| `c2b-2` | NOT RUN | Needs prepared campaign/payment fixture; purchases prohibited. |
| `c2b-3` | NOT RUN | Needs prepared campaign/payment fixture; purchases prohibited. |
| `c2b-4` | NOT RUN | Needs prepared campaign/payment fixture; purchases prohibited. |
| `c2b-5` | NOT RUN | Needs prepared campaign/payment fixture; purchases prohibited. |
| `c2b-6` | NOT RUN | Needs prepared campaign/payment fixture; purchases prohibited. |
| `c2b-7` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |

### [c3-agency-payment.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/c3-agency-payment.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `c3-1` | NOT RUN | Needs prepared campaign/payment fixture; purchases prohibited. |
| `c3-2` | NOT RUN | Native campaign purchase/setup was explicitly prohibited for this run; no campaign buy/stop flow was invoked. |
| `c3-3` | NOT RUN | Needs prepared campaign/payment fixture; purchases prohibited. |
| `c3-4` | NOT RUN | Native campaign purchase/setup was explicitly prohibited for this run; no campaign buy/stop flow was invoked. |
| `c3-5` | NOT RUN | Needs prepared campaign/payment fixture; purchases prohibited. |
| `c3-6` | NOT RUN | Needs prepared campaign/payment fixture; purchases prohibited. |

### [c4-market-research.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/c4-market-research.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `c4-1` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `c4-2` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `c4-3` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `c4-4` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |

### [c5a.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/c5a.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `c5a-1` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5a-2` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5a-3` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5a-4` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |

### [c5b.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/c5b.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `c5b-1` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5b-2` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5b-3` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5b-4` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |

### [c5c.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/c5c.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `c5c-1` | PARTIAL | Fresh current writer-34 city saved and reloaded with PC active; prefix/epoch schema and length matched. This does not assert the historical slice writer or unavailable compact assignment/vendor/order semantics. Evidence: `head-producer`, `head-save`, `head-reload`. |
| `c5c-2` | NOT RUN | Needs the specified historical research-writer fixture; current writer-33 refusal alone does not verify that older checkpoint. |
| `c5c-3` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5c-4` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5c-5` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5c-6` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |

### [c5d.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/c5d.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `c5d-1` | PASS | Idle research status exposed billingContract=true and attached/available=true; no billing-off warning was emitted. No order or purchase was created. Evidence: `head-diag-r2`. |
| `c5d-2` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5d-3` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5d-4` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5d-5` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5d-6` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5d-7` | NOT RUN | Needs the named foreign-prefix/runtime-stop fault fixture and billing assertions. No payload mutation or injected stop was invoked. |
| `c5d-8` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |

### [c5e-agency-dialog.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/c5e-agency-dialog.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `c5e-agency-dialog-1` | PARTIAL | Idle status available/attached with dialogContract=true and deliveryContract=true. Button visibility/geometry/native dialog interaction were not captured. Evidence: `head-diag-r2`. |
| `c5e-agency-dialog-2` | NOT RUN | Native campaign purchase/setup was explicitly prohibited for this run; no campaign buy/stop flow was invoked. |
| `c5e-agency-dialog-3` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5e-agency-dialog-4` | PARTIAL | Idle status ledgerAttached=true with no orders/messages/billing and closedWindowBuilds=0. Matched open/closed research UI and its geometry were not exercised. Evidence: `head-diag-r2`. |
| `c5e-agency-dialog-5` | NOT RUN | Needs the named foreign-prefix/runtime-stop fault fixture and billing assertions. No payload mutation or injected stop was invoked. |

### [c5f-delivery-reports.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/c5f-delivery-reports.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `c5f-delivery-reports-1` | PARTIAL | Idle status available/attached with dialogContract=true and deliveryContract=true. Button visibility/geometry/native dialog interaction were not captured. Evidence: `head-diag-r2`. |
| `c5f-delivery-reports-2` | NOT RUN | Native campaign purchase/setup was explicitly prohibited for this run; no campaign buy/stop flow was invoked. |
| `c5f-delivery-reports-3` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5f-delivery-reports-4` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5f-delivery-reports-5` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5f-delivery-reports-6` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5f-delivery-reports-7` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5f-delivery-reports-8` | PARTIAL | Fresh writer-33 city was refused by current head; PC values preserved across native save, research unavailable/unattached. The specifically named writer-32 fixture was not produced. Evidence: `head-refusal`. |
| `c5f-delivery-reports-9` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |
| `c5f-delivery-reports-10` | NOT RUN | Needs ordered/paid research and billing/delivery trace; purchases prohibited. |

### [d1-rival-catalogue.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/d1-rival-catalogue.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `d1-1` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `d1-2` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `d1-3` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `d1-4` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `d1-5` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |

### [d2-rival-outflow.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/d2-rival-outflow.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `d2-1` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `d2-2` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `d2-3` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `d2-4` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `d2-5` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `d2-6` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `d2-7` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |
| `d2-8` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |

### [d3a-outflow-line.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/d3a-outflow-line.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `d3a-1` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `d3a-2` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `d3a-3` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `d3a-4` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `d3a-5` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `d3a-6` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |

### [d4-rival-memory.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/d4-rival-memory.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `d4-1` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `d4-2` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `d4-3` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `d4-4` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `d4-5` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `d4-6` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |

### [e1-rain.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e1-rain.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e1-1` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |
| `e1-2` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |
| `e1-3` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |
| `e1-4` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `e1-5` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |
| `e1-6` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |

### [e2a-street-life.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e2a-street-life.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e2a-1` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `e2a-2` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `e2a-3` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `e2a-4` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `e2a-5` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `e2a-6` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `e2a-7` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `e2a-8` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |

### [e2b-street-life.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e2b-street-life.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e2b-1` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `e2b-2` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `e2b-3` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `e2b-4` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `e2b-5` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `e2b-6` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `e2b-7` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |

### [e2c-interior-guard.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e2c-interior-guard.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e2c-1` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `e2c-2` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `e2c-3` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `e2c-4` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |

### [e3-cupcake.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e3-cupcake.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e3-cupcake-1` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-cupcake-2` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-cupcake-3` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-cupcake-4` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-cupcake-5` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-cupcake-6` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |

### [e3-donut.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e3-donut.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e3-donut-1` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-donut-2` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-donut-3` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-donut-4` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-donut-5` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |

### [e3-frenchfries.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e3-frenchfries.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e3-frenchfries-1` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-frenchfries-2` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-frenchfries-3` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-frenchfries-4` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-frenchfries-5` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-frenchfries-6` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-frenchfries-7` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-frenchfries-8` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |

### [e3-icecream.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e3-icecream.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e3-icecream-1` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-icecream-2` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-icecream-3` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-icecream-4` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-icecream-5` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-icecream-6` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-icecream-7` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-icecream-8` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |

### [e3-pizza.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e3-pizza.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e3-pizza-1` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-pizza-2` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-pizza-3` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-pizza-4` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |
| `e3-pizza-5` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-pizza-6` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-pizza-7` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |

### [e3-remaining-batch.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e3-remaining-batch.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e3-remaining-1` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-remaining-2` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-remaining-3` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-remaining-4` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `e3-remaining-5` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-remaining-6` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-remaining-7` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-remaining-8` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-remaining-9` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |
| `e3-remaining-10` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-remaining-11` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |

### [e3-salad.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e3-salad.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e3-salad-1` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-salad-2` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-salad-3` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-salad-4` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-salad-5` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-salad-6` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e3-salad-7` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |

### [e4-wider-menu.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e4-wider-menu.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e4-1` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e4-2` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e4-3` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e4-4` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e4-5` | NOT RUN | Needs genuine product/holder fixture and native reader/transaction comparison. |
| `e4-6` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |

### [e5a-catalogue-eligibility.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e5a-catalogue-eligibility.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e5a-1` | PARTIAL | Live catalogue count=807 and filtered original products/dumplings/chopped-cheese/holder definitions captured. Griddle whitelist contained tacos/chopped cheese. Independent tag-cache, building-placeability and consuming-holder identity were not exposed; no completed holder-negative result is claimed. Evidence: `head-diag-r2`. |
| `e5a-2` | NOT RUN | Not implemented on this head / needs real mixed-menu and lifecycle trace. |
| `e5a-3` | NOT RUN | Not implemented on this head / needs real mixed-menu and lifecycle trace. |
| `e5a-4` | NOT RUN | Not implemented on this head / needs real mixed-menu and lifecycle trace. |
| `e5a-5` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |

### [e5b-complete-menu.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e5b-complete-menu.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e5b-1` | PARTIAL | Live catalogue count=807 and filtered original products/dumplings/chopped-cheese/holder definitions captured. Griddle whitelist contained tacos/chopped cheese. Independent tag-cache, building-placeability and consuming-holder identity were not exposed; no completed holder-negative result is claimed. Evidence: `head-diag-r2`. |
| `e5b-2` | PARTIAL | Native rental/setup succeeded at Third Street 54 (75 sqm), with register, bakery showcase, stock, price, hours and saved staff/shifts. It is a single-croissant setup, not a serving mixed/eight-offer fixture; no visual clearance proof. Evidence: `head-shop-3`, `head-business-age`. |
| `e5b-3` | PARTIAL | PC-absent control exercised the same setup: four croissant attempts all unavailable, zero purchases/orders/revenue, stock unchanged, log lost=false/hook_faults=0. No positive paid control or modded-product refusal checkout was reached. Evidence: `native-control`. |
| `e5b-4` | NOT RUN | Not implemented on this head / needs real mixed-menu and lifecycle trace. |
| `e5b-5` | NOT RUN | Not implemented on this head / needs real mixed-menu and lifecycle trace. |
| `e5b-6` | NOT RUN | Not implemented on this head / needs real mixed-menu and lifecycle trace. |

### [e5b2.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e5b2.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e5b2-1` | NOT RUN | Merged after c2fc1b0 during this run (origin advanced to ece4878). Not present in the tested DLL; needs a new candidate batch. |
| `e5b2-2` | NOT RUN | Merged after c2fc1b0 during this run (origin advanced to ece4878). Not present in the tested DLL; needs a new candidate batch. |
| `e5b2-3` | NOT RUN | Merged after c2fc1b0 during this run (origin advanced to ece4878). Not present in the tested DLL; needs a new candidate batch. |
| `e5b2-4` | NOT RUN | Merged after c2fc1b0 during this run (origin advanced to ece4878). Not present in the tested DLL; needs a new candidate batch. |

### [e5c-neutral-metadata.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e5c-neutral-metadata.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e5c-1` | NOT RUN | Not exercised: metadata/bad-config arms and likes/held-state fixtures. |
| `e5c-2` | NOT RUN | Not exercised: metadata/bad-config arms and likes/held-state fixtures. |
| `e5c-3` | NOT RUN | Not exercised: metadata/bad-config arms and likes/held-state fixtures. |
| `e5c-4` | NOT RUN | Not exercised: metadata/bad-config arms and likes/held-state fixtures. |
| `e5c-5` | NOT RUN | Not exercised: metadata/bad-config arms and likes/held-state fixtures. |

### [e5d-name-identity-save.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e5d-name-identity-save.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e5d-1` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `e5d-2` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `e5d-3` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `e5d-4` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `e5d-5` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `e5d-6` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |

### [e5e-metadata-api.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e5e-metadata-api.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e5e-1` | NOT RUN | Needs actual metadata provider/identity-conflict fixture. |
| `e5e-2` | NOT RUN | Needs actual metadata provider/identity-conflict fixture. |
| `e5e-3` | NOT RUN | Needs actual metadata provider/identity-conflict fixture. |

### [e6b.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e6b.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e6b-1` | PARTIAL | Pinned Street Food set returned bridge=Absent, Street Food vendor API missing, without PC stop. Contract-1/old-API variant and positive vehicle fixture were unavailable. Evidence: `head-diag-r2`. |
| `e6b-2` | NOT RUN | Needs compatible Street Food vendor API and vehicle fixture. |
| `e6b-3` | NOT RUN | Needs compatible Street Food vendor API and vehicle fixture. |
| `e6b-4` | NOT RUN | Needs compatible Street Food vendor API and vehicle fixture. |
| `e6b-5` | NOT RUN | Needs compatible Street Food vendor API and vehicle fixture. |
| `e6b-6` | NOT RUN | Needs compatible Street Food vendor API and vehicle fixture. |
| `e6b-7` | NOT RUN | Needs compatible Street Food vendor API and vehicle fixture. |

### [e6c.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e6c.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e6c-1` | PARTIAL | Fresh current writer-34 city saved and reloaded with PC active; prefix/epoch schema and length matched. This does not assert the historical slice writer or unavailable compact assignment/vendor/order semantics. Evidence: `head-producer`, `head-save`, `head-reload`. |
| `e6c-2` | NOT RUN | Needs compatible Street Food vendor API and vehicle fixture. |
| `e6c-3` | NOT RUN | Needs compatible Street Food vendor API and vehicle fixture. |
| `e6c-4` | NOT RUN | Needs compatible Street Food vendor API and vehicle fixture. |
| `e6c-5` | NOT RUN | Needs the specifically named older vendor-writer fixture; writer-33/34 refusal alone does not verify that vendor save assertion. |
| `e6c-6` | NOT RUN | Needs compatible Street Food vendor API and vehicle fixture. |

### [e6d.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e6d.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e6d-1` | PARTIAL | Pinned Street Food set returned bridge=Absent, Street Food vendor API missing, without PC stop. Contract-1/old-API variant and positive vehicle fixture were unavailable. Evidence: `head-diag-r2`. |
| `e6d-2` | NOT RUN | Needs compatible Street Food vendor API and vehicle fixture. |
| `e6d-3` | NOT RUN | Needs compatible Street Food vendor API and vehicle fixture. |
| `e6d-4` | NOT RUN | Needs compatible Street Food vendor API and vehicle fixture. |
| `e6d-5` | NOT RUN | Needs compatible Street Food vendor API and vehicle fixture. |
| `e6d-6` | NOT RUN | Needs compatible Street Food vendor API and vehicle fixture. |
| `e6d-7` | NOT RUN | Needs compatible Street Food vendor API and vehicle fixture. |
| `e6d-8` | NOT RUN | Needs compatible Street Food vendor API and vehicle fixture. |

### [e6e.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e6e.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `e6e-1` | NOT RUN | Needs compatible Street Food vendor API and vehicle fixture. |
| `e6e-2` | NOT RUN | Needs compatible Street Food vendor API and vehicle fixture. |
| `e6e-3` | NOT RUN | Needs compatible Street Food vendor API and vehicle fixture. |

### [f0-interior-view.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f0-interior-view.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f0-1` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `f0-2` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `f0-3` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `f0-4` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `f0-5` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `f0-6` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |
| `f0-7` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |

### [f1-accounting-backlog.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f1-accounting-backlog.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f1-accounting-1` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `f1-accounting-2` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `f1-accounting-3` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `f1-accounting-4` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `f1-accounting-5` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `f1-accounting-6` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |

### [f1-shop-view.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f1-shop-view.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f1-shop-view-1` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `f1-shop-view-2` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `f1-shop-view-3` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `f1-shop-view-4` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `f1-shop-view-5` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `f1-shop-view-6` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `f1-shop-view-7` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |

### [f1b-street-proxy.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f1b-street-proxy.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f1b-1` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `f1b-2` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `f1b-3` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `f1b-4` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `f1b-5` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |
| `f1b-6` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `f1b-7` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `f1b-8` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |

### [f2a.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f2a.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f2a-1` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |
| `f2a-2` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |
| `f2a-3` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |
| `f2a-4` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |
| `f2a-5` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |
| `f2a-6` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |

### [f2b.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f2b.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f2b-1` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `f2b-2` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `f2b-3` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |

### [f2c.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f2c.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f2c-1` | NOT RUN | Merged after c2fc1b0 during this run (origin advanced to ece4878). Not present in the tested DLL; needs a new candidate batch. |
| `f2c-2` | NOT RUN | Merged after c2fc1b0 during this run (origin advanced to ece4878). Not present in the tested DLL; needs a new candidate batch. |
| `f2c-3` | NOT RUN | Merged after c2fc1b0 during this run (origin advanced to ece4878). Not present in the tested DLL; needs a new candidate batch. |
| `f2c-4` | NOT RUN | Merged after c2fc1b0 during this run (origin advanced to ece4878). Not present in the tested DLL; needs a new candidate batch. |
| `f2c-5` | NOT RUN | Merged after c2fc1b0 during this run (origin advanced to ece4878). Not present in the tested DLL; needs a new candidate batch. |

### [f2d.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f2d.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f2d-1` | NOT RUN | Merged after c2fc1b0 during this run (origin advanced to ece4878). Not present in the tested DLL; needs a new candidate batch. |
| `f2d-2` | NOT RUN | Merged after c2fc1b0 during this run (origin advanced to ece4878). Not present in the tested DLL; needs a new candidate batch. |
| `f2d-3` | NOT RUN | Merged after c2fc1b0 during this run (origin advanced to ece4878). Not present in the tested DLL; needs a new candidate batch. |
| `f2d-4` | NOT RUN | Merged after c2fc1b0 during this run (origin advanced to ece4878). Not present in the tested DLL; needs a new candidate batch. |
| `f2d-5` | NOT RUN | Merged after c2fc1b0 during this run (origin advanced to ece4878). Not present in the tested DLL; needs a new candidate batch. |
| `f2d-6` | NOT RUN | Merged after c2fc1b0 during this run (origin advanced to ece4878). Not present in the tested DLL; needs a new candidate batch. |
| `f2d-7` | NOT RUN | Merged after c2fc1b0 during this run (origin advanced to ece4878). Not present in the tested DLL; needs a new candidate batch. |

### [f2f.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f2f.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f2f-1` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |
| `f2f-2` | PARTIAL | Stationary physical logs stayed within 48 pedestrian/24 vehicle caps with zero reported index mismatches; reloads completed. Camera sweeps, hidden-skip and exact body-ID/full-scan arms were not run. Evidence: `head-business-age`. |
| `f2f-3` | NOT RUN | Not exercised: specified body/camera/input equivalence arms. |

### [f2h.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f2h.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f2h-1` | PARTIAL | All actions and PC readbacks worked in background -rig-test without focus or real input. The controlled nearby-object/player-position route-equivalence arms and compact projections were not run. Evidence: `head-business-age`. |

### [f5a.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f5a.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f5a-1` | PASS | Native census published: 335 homes, 1,064 beds, 957 residents, 20 portals; no over-band/without-home resident; home/job/student consistency checks were ok. Census and workplace rows were retained. Evidence: `head-diag-r2`, `head-business-age`. |
| `f5a-2` | PARTIAL | Player business independently read as 75 sqm and player-owned; census reports one player-owned workplace, intrinsic band 6 and no assigned PC worker. Exact compact saved-place comparison is unavailable. Evidence: `head-shop-3`, `head-business-age`. |
| `f5a-3` | PARTIAL | Paused diagnostics left all PC key hashes unchanged across readback. The full five-read native staff/business fingerprint assertion was not run. Evidence: `head-diag-r2`. |
| `f5a-4` | PARTIAL | Census and runtime remained active after fresh-process and same-process reloads of current writer 34. Pending-station and separate-city scoping arms were not exercised. Evidence: `head-reload`. |
| `f5a-5` | PARTIAL | Current-head ordinary mod-work union and separate whole-frame timing were captured with visitors and one owned non-serving shop. Check-specific matched earlier-build/camera/cadence/inspector arms and fine native observation counters were not exercised. Evidence: `head-business-perf`. |

### [f5b-identity-core.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f5b-identity-core.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f5b-1` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `f5b-2` | PARTIAL | Fixture retained its native employee/register/Default shifts across setup and reload; PC census seated nobody at the player business. No full three-day all-rival aiEmployees fingerprint was captured. Evidence: `head-shop-3`, `head-business-age`. |
| `f5b-3` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `f5b-4` | PARTIAL | Current-head ordinary mod-work union and separate whole-frame timing were captured with visitors and one owned non-serving shop. Check-specific matched earlier-build/camera/cadence/inspector arms and fine native observation counters were not exercised. Evidence: `head-business-perf`. |

### [f5c-lite.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f5c-lite.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f5c-lite-1` | PARTIAL | Fresh current writer-34 city saved and reloaded with PC active; prefix/epoch schema and length matched. This does not assert the historical slice writer or unavailable compact assignment/vendor/order semantics. Evidence: `head-producer`, `head-save`, `head-reload`. |
| `f5c-lite-2` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `f5c-lite-3` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `f5c-lite-4` | NOT RUN | Needs an automated selected-person inspector text arm; background control itself worked without focus. |
| `f5c-lite-5` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `f5c-lite-6` | PARTIAL | Current-head ordinary mod-work union and separate whole-frame timing were captured with visitors and one owned non-serving shop. Check-specific matched earlier-build/camera/cadence/inspector arms and fine native observation counters were not exercised. Evidence: `head-business-perf`. |

### [f5d.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f5d.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f5d-1` | PARTIAL | Fresh current writer-34 city saved and reloaded with PC active; prefix/epoch schema and length matched. This does not assert the historical slice writer or unavailable compact assignment/vendor/order semantics. Evidence: `head-producer`, `head-save`, `head-reload`. |
| `f5d-2` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `f5d-3` | PARTIAL | Named resident c0 and commuter pv:1 were read over the working day; aggregate travelling/route counts changed. Complete per-person schedule and exact cadence equivalence were not captured. Evidence: `head-business-age`. |
| `f5d-4` | PARTIAL | Physical diagnostics reported withheld visitors, body caps and zero index mismatches. Complete body/car/lease identity membership against all visitors was not exported. Evidence: `head-business-age`. |
| `f5d-5` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `f5d-6` | PARTIAL | Ordinary advancement/budget/lag counters and full mod-work intervals were observed. Controlled frame-cap/body-cap arms and exact event admission stream were not exercised. Evidence: `head-business-age`. |
| `f5d-7` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `f5d-8` | PARTIAL | Current-head ordinary mod-work union and separate whole-frame timing were captured with visitors and one owned non-serving shop. Check-specific matched earlier-build/camera/cadence/inspector arms and fine native observation counters were not exercised. Evidence: `head-business-perf`. |

### [f5e.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f5e.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f5e-1` | PARTIAL | Fresh current writer-34 city saved and reloaded with PC active; prefix/epoch schema and length matched. This does not assert the historical slice writer or unavailable compact assignment/vendor/order semantics. Evidence: `head-producer`, `head-save`, `head-reload`. |
| `f5e-2` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `f5e-3` | PARTIAL | Independent shop area=75 sqm; census intrinsic band=6, owner=player, excluded from PC work seating, assigned=0, jobs-within-capacity ok. Saved compact place capacity/assignment comparison is unavailable; no mismatch is inferred from the census band. Evidence: `head-shop-3`, `head-business-age`. |
| `f5e-4` | PARTIAL | Fixture retained its native employee/register/Default shifts across setup and reload; PC census seated nobody at the player business. No full three-day all-rival aiEmployees fingerprint was captured. Evidence: `head-shop-3`, `head-business-age`. |
| `f5e-5` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `f5e-6` | PARTIAL | Current-head ordinary mod-work union and separate whole-frame timing were captured with visitors and one owned non-serving shop. Check-specific matched earlier-build/camera/cadence/inspector arms and fine native observation counters were not exercised. Evidence: `head-business-perf`. |

### [f5f.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f5f.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f5f-1` | PARTIAL | Stationary physical logs stayed within 48 pedestrian/24 vehicle caps with zero reported index mismatches; reloads completed. Camera sweeps, hidden-skip and exact body-ID/full-scan arms were not run. Evidence: `head-business-age`. |
| `f5f-2` | PARTIAL | Stationary physical logs stayed within 48 pedestrian/24 vehicle caps with zero reported index mismatches; reloads completed. Camera sweeps, hidden-skip and exact body-ID/full-scan arms were not run. Evidence: `head-business-age`. |
| `f5f-3` | PARTIAL | Physical diagnostics reported withheld visitors, body caps and zero index mismatches. Complete body/car/lease identity membership against all visitors was not exported. Evidence: `head-business-age`. |
| `f5f-4` | NOT RUN | Not exercised: check-specific matched earlier-build/config/cadence/camera arms. |

### [f5g.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f5g.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f5g-1` | PARTIAL | Head natural midnight fill reached 1,914 visitors plus 957 residents; target/cap=1,914, pool target=0, 957 creations in 15 units. Base predates fill and stayed at 957 visitors. Positive day-visitor-pool fixture unavailable. Evidence: `head-declared`, `base-declared`. |
| `f5g-2` | NOT RUN | Needs RIG-PCFIX controlled above-target/shrinking census and positive day-visitor-pool fixtures; four-week soak was not run. |
| `f5g-3` | NOT RUN | Needs RIG-PCFIX controlled above-target/shrinking census and positive day-visitor-pool fixtures; four-week soak was not run. |
| `f5g-4` | PARTIAL | Physical diagnostics reported withheld visitors, body caps and zero index mismatches. Complete body/car/lease identity membership against all visitors was not exported. Evidence: `head-business-age`. |
| `f5g-5` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `f5g-6` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `f5g-7` | PARTIAL | Clamped target and pool target were read before/after natural midnight fill. No controlled mid-day census mutation or shrinking-target arm was run. Evidence: `head-declared`, `head-business-age`. |
| `f5g-8` | PARTIAL | Fill completed at the native boundary and portal/route/lag counters were retained. No declared stress/camera/fill-budget sweep was available. Evidence: `head-declared`, `head-business-age`. |
| `f5g-9` | PARTIAL | Base writer 33 was created fresh and reloaded; current writer 34 refuses that older city and keeps all PC values across a native save. Four-week writer-33 and sealed writer-32 arms were not run. Evidence: `base-producer`, `base-save`, `head-refusal`. |

### [f5h.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f5h.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f5h-1` | FAIL | F-PERF-1: provisional shared-PC head union p95 breached <5 ms (d2-14-18=5.0849 ms, d2-18-22=5.0698 ms). Timing valid, coverage complete, zero save callbacks and sampled dotnet/testhost/MSBuild=0. Likely integration follow-up: F3/PR #98; optional-stop tail provider F2h/PR #159 with #128 diagnostics PR #133. No single-PR regression attribution. Evidence: `head-business-perf`. |
| `f5h-2` | NOT RUN | Needs supported population/fixture builder for the specified load. |
| `f5h-3` | PARTIAL | Ordinary advancement/budget/lag counters and full mod-work intervals were observed. Controlled frame-cap/body-cap arms and exact event admission stream were not exercised. Evidence: `head-business-age`. |
| `f5h-4` | NOT RUN | Only the intermediate day-cycle batch was run; no seven-day save/reload-each-day acceptance sequence. |
| `f5h-5` | PARTIAL | Candidate refused fresh writer 33 with the distinct start-a-new-city message; all three PC values stayed byte-identical after native save, with no stop key. Historical fixture matrix/second negative reload were not run. Evidence: `head-refusal`. |
| `f5h-6` | NOT RUN | Not exercised: automated panel/inspector/geometry/screenshot arms. |

### [f6a-intervention-harness.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f6a-intervention-harness.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f6a-1` | PARTIAL | Native day-1 ledger reconciled bank 5,599 to start 10,000 minus deposit/setup 4,110, payroll 225 and rent 66; revenue/orders=0, stock=100 croissants/300 bags. PC aggregates were captured; no paid-sale PC/native receipt join was possible. Evidence: `head-business-age`, `native-control`. |
| `f6a-2` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6a-3` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6a-4` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6a-5` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6a-6` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6a-7` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6a-8` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6a-9` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |

### [f6b.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f6b.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f6b-1` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6b-2` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6b-3` | PARTIAL | Rival diagnostics exposed ServiceLimits/SellOut/Availability/NativeRivalCheckout estimate labels. No captured served rival decision or native rival settlement was exercised. Evidence: `head-diag-r2`. |
| `f6b-4` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6b-5` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6b-6` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6b-7` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6b-8` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |

### [f6c.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f6c.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f6c-1` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6c-2` | PARTIAL | Observed FoodFeedbackOverview reported countersMatchRescan=true, rebuildsBefore=1, panelBuilds=0. Full hourly/day/week/migration and paid-shop counter coverage was not run. Evidence: `head-diag-r2`, `head-business-age`. |
| `f6c-3` | PARTIAL | Feedback recorded 82 evaluated offers refused as No staffed counter. Saved fixture shifts existed, but native control also refused service; no controlled staff-removal/restoration arm or successful-serving baseline. Evidence: `head-business-age`, `native-control`. |
| `f6c-4` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6c-5` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6c-6` | PARTIAL | Repeated paused projections did not alter saved PC keys; panel stayed closed with panelBuilds=0. Matched open-panel/performance/native field-fingerprint arms were not run. Evidence: `head-diag-r2`. |
| `f6c-7` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6c-8` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |

### [f6d.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f6d.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f6d-1` | PARTIAL | Fresh current writer-34 city saved and reloaded with PC active; prefix/epoch schema and length matched. This does not assert the historical slice writer or unavailable compact assignment/vendor/order semantics. Evidence: `head-producer`, `head-save`, `head-reload`. |
| `f6d-2` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `f6d-3` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |

### [f6e.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f6e.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f6e-1` | PARTIAL | Aggregate EconomyOverview reported nonzero day income and a balanced aggregate ledger; named-worker shift/attendance/posting reconciliation and partial-attendance checks were not captured. Evidence: `head-diag-r2`, `head-business-age`. |
| `f6e-2` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6e-3` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6e-4` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6e-5` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6e-6` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |

### [f6f.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f6f.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f6f-1` | NOT RUN | Needs selected venue/admission/transfer/endowment fixture. |
| `f6f-2` | NOT RUN | Needs selected venue/admission/transfer/endowment fixture. |
| `f6f-3` | NOT RUN | Needs selected venue/admission/transfer/endowment fixture. |
| `f6f-4` | PARTIAL | Commuter pv:1 exposed stable portal/work identity and carried resource/attendance fields. No controlled low-endowment spending/admission/debit fixture was run. Evidence: `head-diag-r2`, `head-business-age`. |
| `f6f-5` | NOT RUN | Needs selected venue/admission/transfer/endowment fixture. |
| `f6f-6` | NOT RUN | Needs selected venue/admission/transfer/endowment fixture. |

### [f6g.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f6g.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f6g-1` | PARTIAL | Fresh current writer-34 city saved and reloaded with PC active; prefix/epoch schema and length matched. This does not assert the historical slice writer or unavailable compact assignment/vendor/order semantics. Evidence: `head-producer`, `head-save`, `head-reload`. |
| `f6g-2` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6g-3` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6g-4` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6g-5` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6g-6` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6g-7` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6g-8` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |

### [f6h.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f6h.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f6h-1` | PARTIAL | Native day-1 ledger reconciled bank 5,599 to start 10,000 minus deposit/setup 4,110, payroll 225 and rent 66; revenue/orders=0, stock=100 croissants/300 bags. PC aggregates were captured; no paid-sale PC/native receipt join was possible. Evidence: `head-business-age`, `native-control`. |
| `f6h-2` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6h-3` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6h-4` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6h-5` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6h-6` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6h-7` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6h-8` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6h-9` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6h-10` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6h-11` | PARTIAL | Fresh current writer-34 city saved and reloaded with PC active; prefix/epoch schema and length matched. This does not assert the historical slice writer or unavailable compact assignment/vendor/order semantics. Evidence: `head-producer`, `head-save`, `head-reload`. |
| `f6h-12` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6h-13` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6h-14` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6h-15` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6h-16` | PARTIAL | Commuter pv:1 exposed stable portal/work identity and carried resource/attendance fields. No controlled low-endowment spending/admission/debit fixture was run. Evidence: `head-diag-r2`, `head-business-age`. |
| `f6h-17` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6h-18` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6h-19` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6h-20` | NOT RUN | Needs exact PC ledger/export + serving/intervention/week fixtures. |
| `f6h-21` | PARTIAL | Current-head ordinary mod-work union and separate whole-frame timing were captured with visitors and one owned non-serving shop. Check-specific matched earlier-build/camera/cadence/inspector arms and fine native observation counters were not exercised. Evidence: `head-business-perf`. |

### [f6i.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f6i.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f6i-1` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `f6i-2` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `f6i-3` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |

### [f6j.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f6j.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f6j-1` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `f6j-2` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `f6j-3` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `f6j-4` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |
| `f6j-5` | NOT RUN | Needs serving food fixture + exact PC receipt/wallet/order join (serving). |

### [f6k.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f6k.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f6k-1` | NOT RUN | Needs runtime effective-config readback for FoodChoicePriceWeight. The baseline config omits the key and compiled default is 1.2; neither proves the requested visible live value. |
| `f6k-2` | NOT RUN | Needs effective-config readback and explicit config arms. |
| `f6k-3` | NOT RUN | Needs effective-config readback and explicit config arms. |
| `f6k-4` | NOT RUN | Needs effective-config readback and explicit config arms. |
| `f6k-5` | NOT RUN | Needs effective-config readback and explicit config arms. |

### [f7a.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f7a.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f7a-1` | PASS | Default-off load/save returned requested=false, save=null and activation=null. Armed reloads of the same fresh head city returned successful activation with sequence >=1. Evidence: `head-diag-r2`, `head-reload`. |
| `f7a-2` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `f7a-3` | PASS | Representative head: five saves of each Default/MidnightSave/RecoverSave. All success; diagnostics sequence increments by one, phase sums within 0.01 ms, recordCountMs/native adjusted totals and characters/UTF-8/UTF-16/records retained. Every >=30 ms sample flagged as save latency. This is measurement proof, not a met pause budget. Evidence: `head-save`, `head-types-r2`. |
| `f7a-4` | PARTIAL | Settled head fresh-process activation: success/hadPayload, background read/validate and main-thread timers captured. Not every timed save was separately reloaded, and calendar-held/native load timeline was not asserted. Evidence: `head-reload`. |
| `f7a-5` | NOT RUN | Needs supported population/fixture builder for the specified load. |
| `f7a-6` | PASS | All nine ordinary windows: SaveCostDiagnostics.save unchanged, save-epoch sequence unchanged and zero union save callbacks; guard verified off at begin. Explicit saves occurred between/after windows. Evidence: `head-business-perf`. |
| `f7a-7` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |

### [f7b.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f7b.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f7b-1` | PARTIAL | Current compact prefix and newline-v1 epoch schema/length/sequence observed; PC active with no accounting-stop after fresh/same-process reload. Paid/Pending receipt and exact compact wallet/native epoch reconciliation unavailable. Evidence: `head-save`, `head-reload`, `head-business-age`. |
| `f7b-2` | PARTIAL | Explicit save, native return-to-menu/load in the same process and fresh-process reload all reached attached=true without stop. Exact native post-unload file hash/mtime sequence was not captured. Evidence: `head-reload`. |
| `f7b-3` | NOT RUN | Not exercised: named runtime/encode/marker/relocation fault arm. |
| `f7b-4` | NOT RUN | Not exercised: named runtime/encode/marker/relocation fault arm. |
| `f7b-5` | NOT RUN | Not exercised: named runtime/encode/marker/relocation fault arm. |
| `f7b-6` | NOT RUN | Not exercised: named runtime/encode/marker/relocation fault arm. |
| `f7b-7` | FAIL | F-PERF-1: provisional shared-PC head union p95 breached <5 ms (d2-14-18=5.0849 ms, d2-18-22=5.0698 ms). Timing valid, coverage complete, zero save callbacks and sampled dotnet/testhost/MSBuild=0. Likely integration follow-up: F3/PR #98; optional-stop tail provider F2h/PR #159 with #128 diagnostics PR #133. No single-PR regression attribution. Evidence: `head-business-perf`. |

### [f7c.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f7c.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f7c-1` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `f7c-2` | PARTIAL | Both builds measured all requested phases at 1,914 persons. Head also reached 2,871; base day-2 stayed at 1,914 (no F5g fill). A matched base 2,871 fixture, common seed and readable peak transient metric are unavailable; comparison is unmatched and provisional. Evidence: `head-save`, `base-save`, `head-types-r2`, `base-types`, `head-declared`, `base-declared`. |
| `f7c-3` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `f7c-4` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `f7c-5` | NOT RUN | Needs supported population/fixture builder for the specified load. |
| `f7c-6` | FAIL | F-PERF-1: provisional shared-PC head union p95 breached <5 ms (d2-14-18=5.0849 ms, d2-18-22=5.0698 ms). Timing valid, coverage complete, zero save callbacks and sampled dotnet/testhost/MSBuild=0. Likely integration follow-up: F3/PR #98; optional-stop tail provider F2h/PR #159 with #128 diagnostics PR #133. No single-PR regression attribution. Evidence: `head-business-perf`. |

### [f7d.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f7d.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f7d-1` | PARTIAL | Current compact prefix and newline-v1 epoch schema/length/sequence observed; PC active with no accounting-stop after fresh/same-process reload. Paid/Pending receipt and exact compact wallet/native epoch reconciliation unavailable. Evidence: `head-save`, `head-reload`, `head-business-age`. |
| `f7d-2` | PARTIAL | Both builds measured all requested phases at 1,914 persons. Head also reached 2,871; base day-2 stayed at 1,914 (no F5g fill). A matched base 2,871 fixture, common seed and readable peak transient metric are unavailable; comparison is unmatched and provisional. Evidence: `head-save`, `base-save`, `head-types-r2`, `base-types`, `head-declared`, `base-declared`. |
| `f7d-3` | NOT RUN | Needs named interrupted checkpoints + compact semantic comparison (replay). |
| `f7d-4` | PARTIAL | Writer 33 -> head refused with start-a-new-city; writer 34 -> base refused as Unsupported population schema; all three PC values unchanged after native save in both directions. Literal Future enum and malformed/historical matrix are not exposed/tested. Evidence: `head-refusal`, `base-refusal`. |
| `f7d-5` | NOT RUN | Needs supported population/fixture builder for the specified load. |

### [f7f.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f7f.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f7f-1` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `f7f-2` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `f7f-3` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `f7f-4` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |

### [f7p.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f7p.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f7p-1` | NOT RUN | Needs the named historical checkpoint/build (historical); no migration. |
| `f7p-2` | NOT RUN | Needs supported population/fixture builder for the specified load. |
| `f7p-3` | PARTIAL | Native serialization/compression, payload sizes/UTF-16 share and ms per million characters retained for writers 33/34. This is not the specified writer-25/prototype comparison or stress fixture. Evidence: `head-save`, `base-save`, `head-declared`, `base-declared`. |

### [fix-128-rush-spike.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/fix-128-rush-spike.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `fix-128-rush-spike-1` | PARTIAL | Requested day-3 window(s) and all coarse unit/GC/optional-stop counters retained, with no union advancement >=30 ms. Exact block alignment/old mean comparison and a matched pre-fix seed are not a complete before/after proof. Evidence: `head-business-perf`. |
| `fix-128-rush-spike-2` | FAIL | F-PERF-1: >=30 ms advancement observed in the requested day-3 window(s), with production slowest-unit/optional-stop/GC attribution retained. Likely path owner F2h/PR #159 and #128 diagnostic slice PR #133; route/GC tails also belong to F3/PR #98. Provisional shared-PC result. Evidence: `head-business-perf`. |
| `fix-128-rush-spike-3` | PARTIAL | Park scans were 0 in observed prep/day windows. Unmatched pre-fix build/Hamptons load/bench-count equivalence arms were not run; >=30 ms optional-stop tails are flagged separately. Evidence: `head-declared`, `base-declared`, `head-business-age`. |
| `fix-128-rush-spike-4` | PASS | Owned-business day-3 evening: complete supported union, p95=4.8478 ms, 3 intervals >=30 ms, all listed with components; zero save callbacks. Provisional shared-PC measurement. The broader strict interval gate is assessed separately under F-PERF-1; likely integration owner F3/PR #98 if p95 breaches. Evidence: `head-business-perf`. |

### [pc-f3-bounded-bulk-work.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/pc-f3-bounded-bulk-work.md)

| Check | Result | Evidence / remaining requirement |
|---|---|---|
| `f3-1` | FAIL | F-PERF-1: valid current-head ordinary windows breached the union p95/<30 ms interval gate, provisional on shared PC. Likely follow-up owners F3/PR #98 (integration/route units), F2h/PR #159 + #128/PR #133 (optional-stop reads), and F2b/PR #151 (clock-rate rebuild). Stress, cadence and open-UI arms remain unrun. Evidence: `head-business-perf`. |
| `f3-2` | NOT RUN | Needs stop-after-posting-unit/native-query/refresh-churn/campaign-publication assertion streams and controlled fixtures. The integrated union measures callbacks but does not create these interventions. |
| `f3-3` | NOT RUN | Needs stop-after-posting-unit/native-query/refresh-churn/campaign-publication assertion streams and controlled fixtures. The integrated union measures callbacks but does not create these interventions. |
| `f3-4` | NOT RUN | Needs stop-after-posting-unit/native-query/refresh-churn/campaign-publication assertion streams and controlled fixtures. The integrated union measures callbacks but does not create these interventions. |
| `f3-5` | NOT RUN | Needs stop-after-posting-unit/native-query/refresh-churn/campaign-publication assertion streams and controlled fixtures. The integrated union measures callbacks but does not create these interventions. |
