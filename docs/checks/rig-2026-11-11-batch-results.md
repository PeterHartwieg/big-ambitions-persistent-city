# Rig results — 11 November 2026 combined-batch subset (F7 check 2 save-phase + runnable G10/union)

Role `rig_test`, Windows `HomePCHartwieg`. Candidate `97448c04dbe11e29871679df034cfecef62ff1aa`, verified by `git fetch origin` before build; requested tip did not move. Schema 1. Save-specific work runs first and separately from gameplay timing. No production change, release, version/changelog, issue mutation, or merge.

Checklist: [6 November checklist](rig-2026-11-06-checklist.md). Previous procedure/evidence: [11 November b PC results](rig-2026-11-11b-pc-results.md).

Summary (27 requested check rows): **0 pass / 3 partial / 0 fail / 24 not run** at this first save-segment commit. Runnable later segments remain pending in this record. A stage query error is retained under Tooling gaps; it is not a product verdict.

Raw evidence is external: `<stage>` = `C:/Users/Peter/HART-Builds/persistent-city-rig-2026-11-11-batch`. Only this small report is committed. All timings are single observations, provisional (PC shared). Bold timing values flag every measured phase/subscriber >=30 ms. Nested phases overlap; do not sum them into a total.

## Build and rig

| Item | Value |
|---|---|
| Build | Detached `rig-2026-11-11-batch` at the candidate; requested build succeeded. PATH prefixed with `%LOCALAPPDATA%/Microsoft/dotnet`; wait-other-dotnet script ran first (no-op). No offline test suite. |
| PC DLL SHA-256 | `4c500997601b098e18665feca8d2b6a3130e7a0180249f515944ce09d6436850` |
| Rig source | Read-only official checkout `19595b849f83c4afe9a328014a8566ca9af482ae`, clean. No checkout edit/build/install/update. |
| Installed overlay | Ops ProductVersion `1.0.0+cfcc5fde3f652356a5aa3585250f4a92604557fb` (#67); Ops SHA-256 `e9ae1a640fad85d93e30c2afb1383221c012d90a7771691d68bd4567b30ba961`. Runtime 7; Input and receipt checked by selfchecks. |
| Installed ops | Save/read/trigger/profile, pcsave extraction and pcstate reads succeeded. Full current ops inventory and later modwork proof are pending. No #69 foreign_patch/rlo_option capability inferred. |
| Mod set | 30 receipt-hashed isolated DLLs with candidate PC, RLO, FoodTrucks, DrivenRides; default body caps. `<stage>/head-mods.json`. F7a armed via existing RIG-PCCONFIG `SaveCostDiagnostics=true` only in save flows; restored afterward. |
| Backup | CURRENT post-#67 backup, 301 entries: install receipt/launcher/managed files, all ModsLocal, persistent-city.json, run.json and PlayerSettings. `<stage>/backup-manifest.json`; isolated HKCU exact backup `<stage>/original-lifecycle/`. Rollback revision `20261111T064506475` untouched. |
| Isolation | Owned `agent1`, fixed isolated profile, port 18330, background/muted. Stable owner `pc-rig-2026-11-11-batch`. No desktop input/focus or normal profile/bridge access. Existing native producer reused from a stage copy. |
| Autosave | Original native preference 5 minutes / 3 slots, prevent_autosave=false. Rig-test guard verified after loads. Save-specific mode permits only explicit armed declared types; no gameplay timing in these processes. Exact preferences restored per segment. |

## Runs

| Run | Flow result | Evidence / qualification |
|---|---|---|
| creation | PASS | See steps, profile, pcstate, selfchecks and Player.log in run directory; all cleanup actions retained. |
| day1-save | FAIL | Stage query stopped before saves: ActivationDiagnostics absent; corrected subsequent flow. |
| day1-save-cost | PASS | See steps, profile, pcstate, selfchecks and Player.log in run directory; all cleanup actions retained. |
| day3-save-cost | PASS | See steps, profile, pcstate, selfchecks and Player.log in run directory; all cleanup actions retained. |
| day1-native-loads | INPUT_ERROR | Scenario needs checks: name its CHECKS.md rows |
| native-loads | PASS | See steps, profile, pcstate, selfchecks and Player.log in run directory; all cleanup actions retained. |
| g10 | INFRA | See steps, profile, pcstate, selfchecks and Player.log in run directory; all cleanup actions retained. |
| g10-remaining | FAIL | See steps, profile, pcstate, selfchecks and Player.log in run directory; all cleanup actions retained. |
| g10-ready | STOPPED | Stage requested pending after load call had already completed; wait could not succeed. Runner interrupt ended before finally. |
| g10-recovered | PASS | See steps, profile, pcstate, selfchecks and Player.log in run directory; all cleanup actions retained. |

## F7 check 2 / F7e input

Fresh native head city: **957 residents / 957 visitors**, day 1 08:00, no rented food shop. All three declared SaveTypes completed on day 1 and day 3. The second copy reached native day 3 06:00 at speed 20 with **957 residents / 1,914 visitors**, fill created=957, no rented shop. Both Default checkpoints cold-reloaded to their recorded counts. This is not the sealed FT-F4-Declared fixture. The historical writer-25 arm is **NOT RUN**: no documented pre-produced compatible checkpoint; representative/historical recipes blocked, and head refuses older schemas with “start a new city”. This is head-writer input, not a matched before/after campaign.

| Checkpoint / SaveType | Capture ms | Encode ms | Capture / encode | Portrait Create ms | Screenshot request ms | Method-window sum ms |
|---|---|---|---|---|---|---|
| day1-save-cost / Default | 15.137 | **424.848** | 3.563% | 18.364 | 2.423 | 20.787 |
| day1-save-cost / MidnightSave | 8.088 | **365.676** | 2.212% | 10.354 | 0.549 | 10.903 |
| day1-save-cost / RecoverSave | 7.666 | **399.872** | 1.917% | 7.763 | 0.632 | 8.395 |
| day3-save-cost / Default | 24.931 | **1099.964** | 2.267% | 12.532 | 2.413 | 14.945 |
| day3-save-cost / MidnightSave | 15.108 | **1339.811** | 1.128% | 10.283 | 0.598 | 10.881 |
| day3-save-cost / RecoverSave | 14.036 | **1332.085** | 1.054% | 7.985 | 0.516 | 8.501 |

Capture is <=25% of encode in every recorded row. The two method durations give the measured synchronous portrait/request work between callbacks and serialization; they do not time asynchronous image completion. F7e remains an owner decision under plan section 3.5; this run implements no overlap slice or official F4f verdict.

| Checkpoint / type | Native Save ms | PC subscriber ms | Serialize ms | Compress thread ms | PC record-count ms (outside phases) |
|---|---|---|---|---|---|
| day1-save-cost / Default | **1081.325** | **909.375** | **95.603** | **144.085** | **422.478** |
| day1-save-cost / MidnightSave | **1078.188** | **1040.982** | 22.647 | **145.616** | **630.221** |
| day1-save-cost / RecoverSave | **901.078** | **865.900** | 22.436 | **157.779** | **411.426** |
| day3-save-cost / Default | **2640.052** | **2454.670** | **123.025** | **575.218** | **1246.089** |
| day3-save-cost / MidnightSave | **2707.383** | **2654.878** | **34.801** | **683.446** | **1227.505** |
| day3-save-cost / RecoverSave | **2668.425** | **2623.159** | **30.648** | **522.950** | **1203.008** |

Native Save is its main-thread method duration; compression is separate on `SaveGame Compress Thread`. Requested F7a record counting adds substantial diagnostic-only work after publication. It is outside the PC phase total and remains inside the PC subscriber/native Save measurement. Compression destination_bytes=-1 is unavailable; retained fixture file sizes/hashes are authoritative.

| Checkpoint / type | Observe | Synchronize | Capture | Validate | Encode | Publish | PC total |
|---|---|---|---|---|---|---|---|
| day1-save-cost / Default | 2.509 | 7.574 | 15.137 | **35.128** | **424.848** | 0.343 | **485.539** |
| day1-save-cost / MidnightSave | 1.669 | 5.482 | 8.088 | 29.814 | **365.676** | 0.006 | **410.735** |
| day1-save-cost / RecoverSave | 2.148 | 5.246 | 7.666 | **39.513** | **399.872** | 0.005 | **454.450** |
| day3-save-cost / Default | 3.811 | 10.711 | 24.931 | **67.035** | **1099.964** | 0.513 | **1206.965** |
| day3-save-cost / MidnightSave | 2.350 | 8.274 | 15.108 | **61.792** | **1339.811** | 0.007 | **1427.342** |
| day3-save-cost / RecoverSave | 2.327 | 7.734 | 14.036 | **63.932** | **1332.085** | 0.009 | **1420.123** |

| Checkpoint / type | Payload chars | UTF-8 bytes | UTF-16 bytes | UTF-16 share of native temp | GC heap before / after (bytes) |
|---|---|---|---|---|---|
| day1-save-cost / Default | 1293483 | 1293484 | 2586966 | 30.3385% | 916885504 / 944984064 |
| day1-save-cost / MidnightSave | 1293483 | 1293484 | 2586966 | 30.3385% | 970846208 / 904003584 |
| day1-save-cost / RecoverSave | 1321725 | 1321726 | 2643450 | 30.7969% | 984481792 / 992194560 |
| day3-save-cost / Default | 7654642 | 7654643 | 15309284 | 71.8170% | 984309760 / 1427963904 |
| day3-save-cost / MidnightSave | 7654642 | 7654643 | 15309284 | 71.8143% | 1438326784 / 1461886976 |
| day3-save-cost / RecoverSave | 7654642 | 7654643 | 15309284 | 71.8143% | 1473007616 / 1374011392 |

Working-set, peak-working-set and private-bytes readings are all 0 from the installed save profiler; these are **unavailable**, not measured zero use. GC heap figures above are retained.

### onSaveGame subscribers

day1-save-cost / Default:

| Subscriber | ms | Threw |
|---|---|---|
| GameManager+<>c.<Start>b__70_2 | 0.245 | False |
| Helpers.PlayerHelper.SaveCurrentPosition | 0.408 | False |
| UI.DraggableWindows.DraggableWindows.Save | 0.481 | False |
| BuildingManager.<Awake>b__59_1 | 0.005 | False |
| CityPerformance.FrameTrace.Save | 0.013 | False |
| FoodTrucks.FoodTrucksSession.OnSaveGame | 7.100 | False |
| RealLifeOverhaul.Guard+Calls+Caught.Run | 0.102 | False |
| PersistentCity.Runtime.PopulationRuntime.Save | **909.375** | False |
| RealLifeOverhaul.SaveHook.Run | 24.364 | False |

day1-save-cost / MidnightSave:

| Subscriber | ms | Threw |
|---|---|---|
| GameManager+<>c.<Start>b__70_2 | 0.003 | False |
| Helpers.PlayerHelper.SaveCurrentPosition | 0.006 | False |
| UI.DraggableWindows.DraggableWindows.Save | 0.096 | False |
| BuildingManager.<Awake>b__59_1 | 0.002 | False |
| CityPerformance.FrameTrace.Save | 0.002 | False |
| FoodTrucks.FoodTrucksSession.OnSaveGame | 0.567 | False |
| RealLifeOverhaul.Guard+Calls+Caught.Run | 0.002 | False |
| PersistentCity.Runtime.PopulationRuntime.Save | **1040.982** | False |
| RealLifeOverhaul.SaveHook.Run | 0.938 | False |

day1-save-cost / RecoverSave:

| Subscriber | ms | Threw |
|---|---|---|
| GameManager+<>c.<Start>b__70_2 | 0.003 | False |
| Helpers.PlayerHelper.SaveCurrentPosition | 0.004 | False |
| UI.DraggableWindows.DraggableWindows.Save | 0.080 | False |
| BuildingManager.<Awake>b__59_1 | 0.002 | False |
| CityPerformance.FrameTrace.Save | 0.002 | False |
| FoodTrucks.FoodTrucksSession.OnSaveGame | 0.602 | False |
| RealLifeOverhaul.Guard+Calls+Caught.Run | 0.002 | False |
| PersistentCity.Runtime.PopulationRuntime.Save | **865.900** | False |
| RealLifeOverhaul.SaveHook.Run | 0.957 | False |

day3-save-cost / Default:

| Subscriber | ms | Threw |
|---|---|---|
| GameManager+<>c.<Start>b__70_2 | 0.294 | False |
| Helpers.PlayerHelper.SaveCurrentPosition | 0.466 | False |
| UI.DraggableWindows.DraggableWindows.Save | 0.613 | False |
| BuildingManager.<Awake>b__59_1 | 0.007 | False |
| CityPerformance.FrameTrace.Save | 0.012 | False |
| FoodTrucks.FoodTrucksSession.OnSaveGame | 10.435 | False |
| RealLifeOverhaul.Guard+Calls+Caught.Run | 0.135 | False |
| PersistentCity.Runtime.PopulationRuntime.Save | **2454.670** | False |
| RealLifeOverhaul.SaveHook.Run | **30.292** | False |

day3-save-cost / MidnightSave:

| Subscriber | ms | Threw |
|---|---|---|
| GameManager+<>c.<Start>b__70_2 | 0.004 | False |
| Helpers.PlayerHelper.SaveCurrentPosition | 0.004 | False |
| UI.DraggableWindows.DraggableWindows.Save | 0.090 | False |
| BuildingManager.<Awake>b__59_1 | 0.001 | False |
| CityPerformance.FrameTrace.Save | 0.003 | False |
| FoodTrucks.FoodTrucksSession.OnSaveGame | 0.786 | False |
| RealLifeOverhaul.Guard+Calls+Caught.Run | 0.002 | False |
| PersistentCity.Runtime.PopulationRuntime.Save | **2654.878** | False |
| RealLifeOverhaul.SaveHook.Run | 1.943 | False |

day3-save-cost / RecoverSave:

| Subscriber | ms | Threw |
|---|---|---|
| GameManager+<>c.<Start>b__70_2 | 0.003 | False |
| Helpers.PlayerHelper.SaveCurrentPosition | 0.006 | False |
| UI.DraggableWindows.DraggableWindows.Save | 0.079 | False |
| BuildingManager.<Awake>b__59_1 | 0.001 | False |
| CityPerformance.FrameTrace.Save | 0.003 | False |
| FoodTrucks.FoodTrucksSession.OnSaveGame | 0.836 | False |
| RealLifeOverhaul.Guard+Calls+Caught.Run | 0.002 | False |
| PersistentCity.Runtime.PopulationRuntime.Save | **2623.159** | False |
| RealLifeOverhaul.SaveHook.Run | 1.615 | False |

### F7 check 7 — load phases

`day1-save-cost`, evidence `runs/day1-save-cost/steps/016-response.json`:

| Phase | ms | Thread |
|---|---|---|
| DeserializeBinaryDataAsync | **680.441** | task |
| InitializeLoadedSave | 9.063 | main |
| CompleteLoad | 0.248 | main |
| LoadAsync | **903.298** | task |
| InvokeOnGameLoaded | **3045.793** | main |
| Load_total | **27595.036** | main |

`day3-save-cost`, evidence `runs/day3-save-cost/steps/020-response.json`:

| Phase | ms | Thread |
|---|---|---|
| DeserializeBinaryDataAsync | **1080.912** | task |
| InitializeLoadedSave | 14.842 | main |
| CompleteLoad | 0.263 | main |
| LoadAsync | **1522.145** | task |
| InvokeOnGameLoaded | **3292.970** | main |
| Load_total | **41114.442** | main |

`native-loads`, evidence `runs/native-loads/steps/001-response.json`:

| Phase | ms | Thread |
|---|---|---|
| DeserializeBinaryDataAsync | **477.830** | task |
| InitializeLoadedSave | 10.104 | main |
| CompleteLoad | 0.284 | main |
| LoadAsync | **666.849** | task |
| InvokeOnGameLoaded | **2859.236** | main |
| Load_total | **26369.275** | main |

`native-loads`, evidence `runs/native-loads/steps/008-response.json`:

| Phase | ms | Thread |
|---|---|---|
| DeserializeBinaryDataAsync | **665.180** | task |
| InitializeLoadedSave | 8.995 | main |
| CompleteLoad | 0.238 | main |
| LoadAsync | **882.298** | task |
| InvokeOnGameLoaded | **2806.959** | main |
| Load_total | **26587.874** | main |

`native-loads`, evidence `runs/native-loads/steps/015-response.json`:

| Phase | ms | Thread |
|---|---|---|
| DeserializeBinaryDataAsync | **576.940** | task |
| InitializeLoadedSave | 9.095 | main |
| CompleteLoad | 0.267 | main |
| LoadAsync | **804.775** | task |
| InvokeOnGameLoaded | **3337.067** | main |
| Load_total | **30509.027** | main |

`native-loads`, evidence `runs/native-loads/steps/022-response.json`:

| Phase | ms | Thread |
|---|---|---|
| DeserializeBinaryDataAsync | **855.417** | task |
| InitializeLoadedSave | 16.215 | main |
| CompleteLoad | 0.388 | main |
| LoadAsync | **1233.624** | task |
| InvokeOnGameLoaded | **4233.399** | main |
| Load_total | **35127.989** | main |

PC activation `day1-save-cost`:

| Background read ms | Create ms | Validate ms | Main-thread CompleteInitialization ms | Succeeded |
|---|---|---|---|---|
| **1011.924** | 0.667 | **323.662** | **161.112** | True |

PC activation `native-loads`:

| Background read ms | Create ms | Validate ms | Main-thread CompleteInitialization ms | Succeeded |
|---|---|---|---|---|
| **1217.792** | 0.884 | **315.827** | **206.060** | True |

PC activation `native-loads`:

| Background read ms | Create ms | Validate ms | Main-thread CompleteInitialization ms | Succeeded |
|---|---|---|---|---|
| **965.489** | 0.849 | **316.595** | **195.547** | True |

## Findings

- F7a captures and publishes schema 1 at this head. All measured save phases succeeded; records retained per SaveType. No older development save loaded.
- Fresh native save, profile and reload evidence is separate from gameplay performance. No ordinary gameplay gate is inferred from a save-specific process.
- The generic pcsave epoch comparator reports `marker: unknown` for the current side key. It is retained as a tooling gap, never presented as a failed or missing PC epoch. This run does not assert semantic identity equivalence from that comparator.

## NOT RUN grouped by missing capability or fixture

- RIG-PRESENT / RIG-FOREGROUND / RIG-PRIORITY: official F4 G1, G3–G8, G11, G13 and G2 presentation-trace half. Reserved kinds refuse validation before lease acquisition. No foreground/real input attempted.
- Pending FT-F4-Declared / FT-F9 shop fixtures: official production and F4 G2 remainder/G9/G14; F7 check 1 and shop-dependent check 3. Installed #67 native shop rental remains refused (`ops_shop_native_rental_refused`, previous #210 evidence); no rental workaround or new shop attempt. Manifests pending, not sealed.
- Blocked stress fixtures `pc-stress-10k@1` / `city-50k@1`: F4 G15, F7 check 6 and large-city portion of check 7. No supported population setting.
- Historical schema/representative fixtures blocked: F7 check 5 and writer-25 before arm of check 2; no reviewed readback inspector and no eligible produced checkpoint. No recipe production/sealing attempted.
- RIG-FAULT variant / missing checked-in flow: F7 check 4. Shop activity and interrupted-state fixtures: check 3.
- F7e not built: F7 check 8. F4f official verdict and F7e implementation are outside this testing run.
- Installed overlay #67: F8h check 1 and fix-reload-accounting-epoch 3–4 need later foreign_patch / rlo_option installation. Checkout source presence is insufficient.
- Later standalone G10 and clean union segment: pending at first save commit.

## Tooling gaps

| Capability | Error / limitation | Flow | Blocked check? |
|---|---|---|---|
| Stage load-flow input | Scenario needs checks: name its CHECKS.md rows. Fixed required label before any lease/game action. | day1-native-loads | That submission refused pre-lease; corrected native-loads follows. |
| Stage aging controller | BOM read then private in-memory lease credential mismatch; speed-change command refused, no mutation. Native speed20 target remained unchanged. | day3-save-cost; stage-control-refusals.json | No; normal aging finished. |
| Stage pcstate query | `ops_pcstate_property_missing`: requested nonexistent ActivationDiagnostics. Corrected to SaveCostDiagnostics plus its documented SaveCostDiagnostics=true config. | day1-save step 3 | Stopped that attempt before saves; corrected flow succeeded. |
| RIG-SAVEPROF memory | working_set_bytes / peak_working_set_bytes / private_bytes all 0. | save-cost profiles | Process-memory proof partial; GC heap available. |
| RIG-SAVEPROF destination size | CompressSaveGame destination_bytes=-1. | save-cost profiles | No; stopped native checkpoint copies hashed and sized. |
| RIG-PCSAVE epoch parser | Current epoch side key classified unknown by generic comparator. | day1-save-cost pcsave/day1-default.summary.json | Semantic epoch comparator proof not claimed. |
| RIG-PRESENT / FOREGROUND / PRIORITY | Reserved validation refusal, documented at rig 19595b8. | Official generated gate flows | Yes; NOT RUN, no bypass. |
| RIG-SHOPFIX / RIG-PCFIX | Known native rental refusal and pending manifests/readback inspector; not reattempted. | Official FT-F4 / FT-F9, representative recipes | Yes for official fixture rows; did not block save-cost subset. |

## Fixtures

All files below are this run’s fresh native FT character only, retained in `C:/Users/Peter/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests/SaveGames/Big Ambitions/R1CzA3x4k6wqK4jp4SCUA==`. No borrowed old checkpoint. Native recovery bytes are retained as FT-named copies after the player stops; native slot names are cleaned by the standard runner. These copies are not sealed library manifests.

| Fixture file | Bytes | SHA-256 |
|---|---|---|
| FT Batch day1-save-cost Recover Midnight.hsg | 890515 | `05da86bbf54c1b1bc52abd0e73f09069eb4ed0da4a004077b58987550754a34f` |
| FT Batch day1-save-cost Recover slot0.hsg | 894937 | `e5761416484bb9c0b67e613c89f06dc6c417d88ad71be4d5a7accf9d8d74335f` |
| FT Batch day3-save-cost Recover Midnight.hsg | 2179329 | `f94191b23cc97033ebb450d4aa38b916b90abeac162d27c3f359fede6fe08eb6` |
| FT Batch day3-save-cost Recover slot0.hsg | 2179326 | `6888b96345d190a4dd23c149e124da1642e4857702b2e101e752351ddbc43951` |
| FT E5b 11 Nov Batch 957 Ready.hsg | 890619 | `3a08f8e6f25d175d9b27168ab921ca72eedcdc7b2036b692701a7cf4599ffa1c` |
| FT E5b 11 Nov Batch 957.hsg | 502081 | `a52b4386ef413fe51a08a33bc6490dba232690a4476586122e345637d99471c9` |
| FT rig 6c34cb1756ae41ff8368cf9c5422fd92.hsg | 890619 | `3a08f8e6f25d175d9b27168ab921ca72eedcdc7b2036b692701a7cf4599ffa1c` |
| FT rig checkpoint a6d2374da99e4691940094a2e2bde68c.hsg | 2179306 | `d048d7e37875f33fb06bf637cc2667f0f367fd03e8babfe24f0bd87187422dcd` |
| FT rig checkpoint c765060fb4d44eb99e91ff91e555d5fb.hsg | 890515 | `13c6098f465b88e848385c83989a49c549df29b2fb6f0dd48286191aeaa0bbfc` |

## Per-check results

| Check | Status | Result |
|---|---|---|
| F7 check 2 / F4 G12 | PARTIAL | All three 957 head SaveTypes measured; capture/window inputs recorded. Historical before arm and reliable process-memory fields absent. |
| F7 check 7 (957 portion) | PARTIAL | Default cold load measured with native decode/activation and F7a initialization; remaining native-type copies and day-3 load campaign pending. Stress portion NOT RUN. |
| F4 G10 | PARTIAL | Cold/fresh loads measured; in-session activation recovered after staged readiness errors. Native skiptime 120m completed in 9578 ms, running?stopped observed. Stale/pending records excluded. See g10-recovered/report.json for same-owner normal quit, original-backup restoration and free lease; no lease edit or takeover. |
| F7 check 9 | NOT RUN | Later ordinary clean union segment pending; not a substitute for F4 G4. |
| F4 G1–G9, G11, G13–G15; F7 1,3–6,8; F8h1; reload3–4 | NOT RUN | Grouped capability/fixture reasons above. |

## Evidence integrity and cleanup

| Artifact relative to stage | SHA-256 |
|---|---|
| backup-manifest.json | `7b8b4820e80dad526ce83d6d9d87fc4022bd76bbc23362313d238436562e3add` |
| head-mods.json | `76cc17060e5b580cfa3976d4b8e0f943f8901aa84725cdd9f9d1787a1b6d14cb` |
| save-summary.json | `550e9aecc41a7c38ecb330a9eaf083b8d8201f9ae2e9f872f6565f39f4441566` |
| runs/creation/report.json | `8ec864fe7b4d21b701bebab44cf040c40268a66699dfd752cce110d9a18b292e` |
| runs/day1-save/report.json | `0c3c407011c3636e65e89c781a2ecc133a57fcf37853154725b65dfa34b1d5e9` |
| runs/day1-save-cost/report.json | `f42cf628c713968d8aa4c932c4c2dd7055f84c30b1a394e8b258cf9848d00215` |
| runs/day1-save-cost/save-profile/00145-Default.json | `5e327cda90ad001dcee7cf158c752874f1740845c35e2d5e2f355b63008abbeb` |
| runs/day1-save-cost/save-profile/00158-MidnightSave.json | `ae87cdcda295f3dd81fad1a33edbe9a98f564e36921dc3bfca277b0ab14a5a2c` |
| runs/day1-save-cost/save-profile/00171-RecoverSave.json | `98cdb0c4280be739ae2f245840244a779ac6c54cae039cf3298c1172a31178fe` |
| runs/day3-save-cost/save-profile/18144-Default.json | `37860f8ee81548a2e265d51b7d651b710fa92f8fe2f8bf75cef25e5bfc4548a7` |
| runs/day3-save-cost/save-profile/18157-MidnightSave.json | `3d91b34c64e222d53d2e8c3abf7a57abf563abdb66020b382a2c46e1620bc960` |
| runs/day3-save-cost/save-profile/18170-RecoverSave.json | `799f88d91b73bc7e7400d2c424ae3fb2a36b6d4cf04cbfefa88889be323ddf95` |

First save-segment cleanup: producer and profiling processes stopped normally; each flow restored mods/config/save-mode/preferences, removed only its own scratch/temporary copies, verified original fixture hashes and released the lease. This run’s original 301-entry backup remains authoritative for final restoration. Later segments and final whole-backup verification pending.

