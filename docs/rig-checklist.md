# Rig checklist

In-game checks waiting for a batched rig run. A rig run never gates a merge. Each slice lists its checks in its PR description. After merge the coordinator copies them here, and removes a check once a rig run passes it at a build that still contains the change. This list authorizes no run.

Every check runs in a named disposable FT city in the isolated rig profile, never in Peter's ordinary save. Autosave is off before every measured window, including after a reload, and is restored at cleanup.

Detailed specs: [F8 §7](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/plans/f8-capability-boundaries.md#7-in-game-checks-for-the-batched-rig), [F9 §7](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/plans/f9-city-acceptance.md#7-in-game-checks-for-the-next-batched-rig), [E6 §7](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/plans/e6-carts-food-trucks.md#7-in-game-checks-for-the-batched-rig), [F4 plan](plans/f4-perf-gate.md), [11j report](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/checks/rig-2026-11-11j-mac-results.md), [12b report](checks/rig-2026-11-12b-mac-results.md). Checks written before 10 November are in the [docs history tag](https://github.com/PeterHartwieg/big-ambitions-persistent-city/tree/docs-history-2026-11-10/docs/checks); many test save migration, which no longer exists.

## Latest evidence

- **14 November l, Mac, build `da8b495b`:** 0 passed / 1 partial / 3 failed / 4 NOT RUN groups. Day/night mod 313.5/166.4 MB, gen0 8/7, pause p95 33.488/36.717 ms; street fail; restored ([14l results](#rig-results--14-november-2026-l-mac-gr-5-gcstreet-recheck-after-270-at-da8b495b)).
- **14 November k, Mac, build `8eb87be2`:** 0 passed / 0 partial / 3 failed / 5 NOT RUN groups. Day/night gen0 6/7, pause p95 219.813/234.512 ms; advance 85.040/35.434 MB; street union p95 23.2135 ms; restored ([14k results](#rig-results--14-november-2026-k-mac-gr-5-gctraffic-recheck-after-268-at-8eb87be2)).
- **14 November j, Mac, build `50f52c18`:** 2 passed / 2 partial / 0 failed / 5 NOT RUN groups. Day/night gen0 7/6, pause p95 28.581/32.902 ms; street p95 3.7732/3.8197 ms with three spikes; night pool 16/16; look identity partial; restored ([14j results](#rig-results--14-november-2026-j-mac-gr-5-gc-recheck-after-266-at-50f52c18)).
- **14 November i, Mac, build `65b41db5`:** 1 passed / 1 partial / 0 failed / 4 NOT RUN groups. Five unrentable fixture subjects, rig tally 3 housed / 1 unreadable / 1 empty; PC applied Home evidence not observable in-load; restored ([14i results](#rig-results--14-november-2026-i-mac-246-checks-4-5-with-unrentable_fixture-at-65b41db5)).
- **14 November h, PC, candidate `650fc893`:** 0 passed / 0 partial / 0 failed / 9 NOT RUN. Local staging/windowed start worked; throwaway time/perf/modwork FT guards and fixture population blocked all four windows; exact restoration, lease free ([14h results](#rig-results--14-november-2026-h-pc-gr-5-60-fps-streetbusy-shop--gc-vs-mac-14g-at-650fc893)).
- **14 November g, Mac, build `7ff6a87a`:** 2 passed / 2 partial / 1 failed / 4 NOT RUN groups. Appearance 62.2/26.1 MB, gen0 7/6, pause p95 67.048/141.801 ms; street p95 5.2988/5.3683 ms; look/refill partial; restored ([14g results](#rig-results--14-november-2026-g-mac-gr-5-gc-recheck-after-262-at-7ff6a87a)).
- **14 November f, Mac, build `2f5e72eb`:** 4 passed / 1 partial / 0 failed / 5 NOT RUN; ordinary-save refusal verified, food sales at speed 1 checked, footprint fixture gap; restored ([14f results](#rig-results--14-november-2026-f-mac-244-p-d-7--246-checks-4-5--260-checks-1-3-at-2f5e72eb)).
- **14 November f, PC, candidate `0795aa2c`:** 0 passed / 0 partial / 0 failed / 11 not run. Windows scoped receipt-safe staging missing; installed-player PowerShell probe succeeded; restored, lease free ([14f PC results](#rig-results--14-november-2026-f-pc-244-p-d-7--246-check-4-footprint--gr-5-60-fps-arms-at-0795aa2c)).
- **14 November e, PC, build `2448bc40`:** 0 passed / 0 partial / 0 failed / 9 not run. Default start failed with SSH `homepc` resolution error; P-D 7 tooling gap; restored, lease free ([14e results](#rig-results--14-november-2026-e-pc-244-p-d-check-7-at-2448bc40)).
- **14 November d, PC, build `913abd7d`:** 0 passed / 0 partial / 0 failed / 9 not run. Windows CLI refused `--rig-test`; neither 60 fps window started. Files/hash/lease restored ([14d results](#rig-results--14-november-2026-d-pc-gr-5-60-fps-arms-at-913abd7d)).
- **14 November c, Mac, build `1d5d1b76`:** 1 passed / 1 partial / 1 failed / 6 NOT RUN groups. #255 runtime retime passed; rush-hour continuous visual proof partial; off/on paid receipts absent; restored ([14c results](#rig-results--14-november-2026-c-mac-255-retime-checks-1-3-at-1d5d1b76)).
- **14 November b, Mac, build `eb38a061`:** 0 passed / 0 partial / 3 failed / 7 not run. Day/night GC 11/8 gen0, pause p95 28.191/61.316 ms; physical+advance 1,021.6/433.6 MB; street UpdateCity p95 5.5170 ms; appearance/footprint/actual 60 fps proof blocked; restored ([14b results](#rig-results--14-november-2026-b-mac-gr-5-gc-254-footprint-60-fps-at-eb38a061)).
- **14 November a, Mac, build `b3d58a6a`:** 1 passed / 0 partial / 4 failed / 1 not run. Day/night GC 11/9 gen0, pause p95 103.863/70.122 ms; street UpdateCity p95 5.2683 ms; Living-off passed, toggle repeat stopped, P-D console blocked; restored ([14a results](#rig-results--14-november-2026-a-mac-gr-5-gc-rlo-p-d-7-at-b3d58a6a)).
- **14 November g, Mac, build `37a150d3`:** 0 passed / 0 partial / 3 failed / 6 not run. Day/night GC 11/8 gen0, pause p95 56.671/104.551 ms; street UpdateCity p95 5.1413 ms, two spikes; restored, run complete ([14g results](#rig-results--14-november-2026-g-mac-gr-5-gc-at-37a150d3)).
- **14 November f, Mac, build `d237f443`:** 1 passed / 2 partial / 2 failed / 1 not run. PC callback save p95 742.826 ms (recorded phases 301.027 ms); day/night GC 12/6 gen0, pause p95 65.028/31.977 ms; street mod-work p95 4.2557 ms; old-format reload passed ([14f results](#rig-results--14-november-2026-f-mac-gr-5-at-d237f443)).
- **13 November e, Mac, build `5fe5eb23`:** GR-5: 0 passed / 1 partial / 3 failed / 1 not run. Save p95 731.144 ms; GC 10 gen0 / pause p95 66.260 ms; broader shop mod-work p95 5.0406 ms; 60 fps and footprint proof unavailable ([13e results](#rig-results--13-november-2026-e-mac-gr-5-at-5fe5eb23)).
- **13 November d, Mac, build `f3e3d538` (code `83b6156e`):** P-D 5 passed / 1 partial / 1 not run; RLO 1.0.6 #239 1 passed / 3 partial / 2 not run; GR-9 2 partial / 8 not run. Three GC windows and background capture ran; Living ops remain blocked ([13d results](#rig-results--13-november-2026-d-mac-p-d-mono-gc-rlo-106-239-and-gr-9-at-f3e3d538)).
- **13 November, Mac, build `a3a6c8db`:** GR-4, GR-6/7/8, GR-9 and RLO #239 rows: 2 passed, 6 partial, 1 failed, 11 not run; blocked. The rig ran RLO 0.45.0, not 1.0 ([13 November report](checks/rig-2026-11-13-mac-results.md)).
- **12b, Mac, build `060a7947`:** 15 targeted rows verified by rig: food, goods and service probe sales, ceiling refusal, mid-probe cut refusal and pre-probe recovery, #77 goods and service fixtures, F7-3 Pending capture. This proves those rows at that build only. It predates GR-3, GR-4, GR-6 to GR-9 and #239.
- **12a, Mac:** the old "loaded cut has no half sale" assertion failed. #233 replaced it with refuse-and-recover; 12b passed the new contract, and 12a's failure stands ([12a](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/checks/rig-2026-11-12a-mac-results.md)).
- **11j, Mac:** natural first sale passed; partial rows are listed below.

## 1. Re-run after later product changes

1. **Sales at the current head.** Setup: food, goods and service fixtures from 12b. Steps: repeat 12b's S2 food, goods, service and ceiling probes and the #233 cut and recovery arms. Expected: the 12b results unchanged. Needed because GR-3 (#234) and GR-4 (#236) changed the purchase path.
2. **PR checks since 12b.** The "In-game checks" lists in [#234](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/234), [#236](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/236), [#237](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/237), [#238](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/238) and [#239](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/239). The coordinator copies them into this section. They cover needs purchases, venue readiness, staffed capacity, evenings and households, workplaces, through-traffic, own cars and bodies, and the RLO overlap rules.
3. **RLO overlap and own cars.** Setup: full modpack with RLO 1.0, Living population on, then off. Steps: load, run two game hours, save, reload. Expected: Persistent City overrules RLO where they overlap, as #239 specifies; no native car is borrowed; walkers and parked cars persist across reload (GR-9).

## 2. Shop menus and storms (E5b2 to E5b5)

Missing: original-three, mixed original and dynamic, eight- and nine-offer, catalogue lifecycle, metadata override, refill and resource, and 8- and 64-shop fixtures. The single-croissant fixture and `shop_edit` cannot add the holders and prices. Spec: [E5b probe](../tools/e5b-native-rig-proof/README.md).

4. **Mixed and original-three menus.** Expected: each offer sells at its own price, and catalogue changes reconcile without lost or duplicated receipts.
5. **Eight- and nine-offer menus, refill.** Expected: every offer is reachable; refill and resource use match native.
6. **Repair storms at 8 and 64 shops.** Expected: mutation storms reconcile; mod-work stays inside the perf gate.

## 3. Street Food, vehicle sales and research

7. **E6 checks 1 to 13** ([E6 §7](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/plans/e6-carts-food-trucks.md#7-in-game-checks-for-the-batched-rig)). Missing: RIG-SFAPI, RIG-VLEDGER, RIG-SFFAULT.
8. **Street Food absent, then returned (F8-2).** Steps: save, load without FoodTrucks, run two hours, save, restore and load. Expected: `vendor.streetfood absent` with its reason; shop money equals a run with no cart; the cart requalifies after return. Missing: RIG-PCSAVE.
9. **Research and a truck in one city (F9-7).** Steps: order a neighbourhood snapshot, save and reload while it is pending, run to morning. Expected: one charge on that night's marketing bill, one report, receipts unique across reload. Missing: RIG-LIVE.
10. **Mechanism report (F8-9; 11j partial).** Expected: `shop.fullservice-bag.v1`, `vehicle.streetfood-api.v1`, and `shop.dynamic-catalogue off` for a non-historical product. 11j saw only the shop row.

## 4. Throughput and wholesale

11. **Representative throughput (F6b-2; 11j partial).** Setup: a cohort of staffed food shops. Expected: hourly completions against native MealsPerHour support a median and the default ceiling. 11j had one shop, which is not a cohort.
12. **Price response at weight 1.2 (F9-9).** Steps: raise every price by 50% in one copy for two days. Expected: fewer meals and revenue up by well under 50%. Native evidence, not calibration. Missing: RIG-LEDGER, RIG-PCCONFIG.

## 5. Saves, refusals and interrupted state

13. **Larger F7-1 matrix (11j partial).** Expected: Pending, same-minute fault, relocation and menu arms each keep identities, wallets and receipts across cold reload. Only F7-3's capture arm passed (12b).
14. **X3-1 literal read (11j partial).** Expected: the population read emits `Loaded` with schema 1. 11j saw the reload accepted but not that line.
15. **Unreadable or wrong-schema city refused and kept (F9-8).** Expected: the refusal status; payload and other PC keys byte-identical across a native save. Missing: a wrong-schema FT loader, RIG-PCSAVE.
16. **RLO absent at load (F8-4).** Expected: no city starts; status says RLO is required; payload hash unchanged; native customers serve the shop.
17. **Changed native members (F8-5 to F8-7).** Expected: the named capability goes `off`, the payload is unchanged, owned shops hand back at the next accounting boundary. Missing: RIG-FAULT.
18. **Foreign checkout prefix (F8-10).** Expected: `food.checkout.shop off: foreign prefix by <owner>`; zero PC receipts. Missing: RIG-FOREIGNPATCH.
19. **Business lifecycle (GR-1 checks 2, 3, 5).** Close and replace, modded type, older city. Missing: a lifecycle fixture, a modded-type fixture, a pre-GR-1 save.

## 6. Cadence, bodies, config and seven days (F9)

20. **Seven days with daily save and reload (F9-4).** Expected: PC extracts and native orders equal an uninterrupted copy; ledger conserved daily. Missing: RIG-PCSAVE, RIG-LEDGER, RIG-LIVE.
21. **Native changes across cadences (F9-5).** Frame caps 30, 60, 120 and a hidden skip. Expected: identical extracts; each change at the same minute; lag at most one game minute. Missing: RIG-FRAMECAP.
22. **Detail transitions and in-flight reload (F9-6; F2 matrix).** Bodies on and off. Expected: equal extracts and orders. 11j read movement diagnostics only. Missing: RIG-PCCONFIG.
23. **Provenance, capabilities and summary (F9-1 to F9-3, F9-10).** Expected: a declared fingerprint set, every capability active or inactive with a reason, one merged summary. Missing: RIG-ACCEPT.

## 7. Scale, presentation and performance

24. **Population and GC scale.** FT-F4-Stress10k and city-50k. Expected: M1 and M2 per the [F4 plan](plans/f4-perf-gate.md); GC spikes flagged. Missing: RIG-PCFIX.
25. **Sealed F4 fixtures and windows (F4f, F7e).** Missing: implementation and manifest sealing.
26. **Present, foreground and priority flow steps.** Missing: RIG-PRESENT, RIG-FOREGROUND, RIG-PRIORITY.
27. **Capability report cost (F8-11).** Expected: mod-work p95 under 5 ms; every spike of 30 ms or more listed with its component.

## 8. Owner input

28. **Pointer, hover, focus and click.** Peter's playtest, not the rig.
29. **Windows and PC-overlay rows.** Need the Windows rig.

## Rig results — 13 November 2026 b, Mac (P-D Mono GC at 83b6156e)

Role `rig_test`, where `mac`, isolated `agentmac`. **0 passed / 0 partial / 0 failed / 7 NOT RUN. RESULT: BLOCKED.** Counts cover the seven checks in [PR #244](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/244); the setup failure is reported separately. No in-game evidence was obtained. GR-5 still waits for the P-D measurements; this run provides no population-size or GC-cost conclusion. In-game proof gates no merge.

`<stage>` = `/Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13b-mac`. Raw commands, errors, backup manifest and originals remain there. A report copy is at `~/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-13b-mac.md`. No raw logs, binaries, saves or credentials are in this diff.

### Build and rig

| Item | Observation |
|---|---|
| Candidate / verified tip | `83b6156e56275069ad684b26cfa8677dbca4b2a4`. `git fetch origin` followed by `git rev-parse origin/codex/persistent-city` returned the briefing candidate before the build; it had not moved. |
| Build checkout | Detached `rig-2026-11-13b-mac`, created from that tip. Original Persistent City working tree stayed clean at `9c617c62efee91f999695ebc6a47171e1218282e`. |
| Build command / reference | `python3 tools/build.py --game-managed "/Users/peterhartwieg/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"` exited 0. `PATH=/opt/homebrew/bin:$PATH`; `DOTNET_ROOT` came from `/opt/homebrew/bin/brew --prefix dotnet` plus `/libexec`. |
| Product DLL SHA-256 | `61b8292227e17fe9609098ef40d52238b556e2e2fc73da782870b848d1a074cc`. Staged only in the isolated ModsLocal, then restored. |
| Official rig checkout | `eea054728f546f07dc74305112fb429da8997cea`, clean before and after; no edits, commits, installs or pushes there. Read `docs/MAC.md` and the relevant `docs/rig-ops.md` sections before operating. |
| Live installed AgentRigOps SHA-256 | `6478e76cb4c04b44f8d8ef315d4ef75fb5d912034f957696fdc4ad7a6b22691c`, unchanged before and after. This identifies the installed file; no runtime loaded it. |
| Rollback retained, unused | `/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/updates/20261112T070545Z`. The install was not broken by this run; no rollback or rig reinstall attempted. |
| Resources / lease | 45.8 GiB disk free and 66% memory free. Lease was free; acquired with purpose `Persistent City 13 Nov b Mac rig (P-D GC)` and released after restore. No lease wait or resource refusal. |
| Start | `python3 scripts/mac-agent start --mode headless --rig-test`, exit 1 during install verification, before process launch. No background graphics or captures attempted. |
| Evidence branch | `codex/mac-rig-2026-11-13b`, separate `rig-2026-11-13b-mac-evidence` worktree. Remote name was free; no numeric suffix needed. |
| Configuration | Only this run's Persistent City DLL/locale/config and corresponding isolated install-receipt entries were staged. Price and household weights were not overridden or changed; schema stays 1. No production code changes. |

### Per-size measurements

| Requested persons | Status / reached | gen0/1/2 | pause_ms p50/p95/max | frame_ms_plain p95 | mod_ms_plain p95 | GC/frame spikes >=30 ms | incremental / dropped / autosave |
|---|---|---|---|---|---|---|---|
| 3,000 | NOT RUN / unavailable | unavailable | unavailable | unavailable | unavailable | unavailable | unavailable |
| 10,000 | NOT RUN / unavailable | unavailable | unavailable | unavailable | unavailable | unavailable | unavailable |
| 20,000 | NOT RUN / unavailable | unavailable | unavailable | unavailable | unavailable | unavailable | unavailable |

All sizes refer to head `83b6156e56275069ad684b26cfa8677dbca4b2a4`. No `[Persistent City] gc report {json}` line or following human line was produced. Persons, reached/visitor_capacity/portals/refusals, generation counts, heap start/end/peak/before_gc/after_gc, Mono heap/used, pause percentiles, all spikes, frame/mod percentiles, max frame, incremental, dropped frames and autosave-stayed-off are unavailable. Missing values are explicit nulls in [the small measurement record](perf/pd-mono-gc-2026-11-13-mac.json), SHA-256 `8af922735fb5013ce147bff34f38d43a55ca21f76df26bffd5fabe7a0c478807`. These nulls are not zeros or performance passes.

The requested 3,000 → 10,000 → 20,000 same-city sequence did not begin. No top-speed advance, settling hour or 10-minute measured window ran. No conclusion about the 5 ms mod-work p95 target or >=30 ms spikes is possible. No incremental flag was read: if a later run reports incremental=true, collection-frame pause_ms understates total GC work, and frame_ms_plain p95, frame_spikes_30ms and max_frame_ms must be compared across sizes as PR #244 specifies. Other agent sessions were present in the process inventory; any measurements would have been provisional on this shared Mac. This run has no timings to label or compare.

### Per-check results

| Check | Result | Observation |
|---|---|---|
| 1. Sampler off costs nothing | **NOT RUN** | No client launched; no usual mod-work window or previous-head comparison. Absence of a report is not a pass. |
| 2. 3,000 persons | **NOT RUN** | No FT city loaded; no target growth, settling hour or 10-minute window. |
| 3. 10,000 persons | **NOT RUN** | Depends on check 2 in the same fresh city; no window. |
| 4. 20,000 persons | **NOT RUN** | Depends on check 3; no reached count or capacity/portal/refusal status. The reason for not reaching 20,000 is startup refusal, not an observed population limit. |
| 5. Report does not sample itself | **NOT RUN** | No sampler or three reports 10 seconds apart; frame growth and max_frame_ms not observed. |
| 6. Target gone after reload | **NOT RUN** | No measurement city, saved baseline or reload; target=off not observed. |
| 7. Ordinary saves refused | **NOT RUN** | No second disposable non-FT city created; the command refusal and unchanged persons not observed. |

### Findings and tooling gaps

The required startup op failed with exit 1 and exact stderr:

```text
Installed file changed: /Users/peterhartwieg/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/RealLifeOverhaul/Bundles/reallife_anim
```

Offline comparison found **13 pre-existing receipt mismatches**, all under isolated `ModsLocal/RealLifeOverhaul`: two bundles (`reallife_anim`, `reallife_props`), nine locale files (`de`, `en`, `es`, `fr`, `nl`, `pt`, `ru`, `tr`, `zh-cn`), `RealLifeOverhaul.dll` and `thumbnail.jpg`. Every actual hash equals the pre-staging backup hash; the mismatches existed before this run's staging. Expected and actual hashes are in the measurement record and `<stage>/install-discrepancies.json`. The first bundle expected `d8c8e95cba530293961d2185d26251029f595d8391f77740683c0dc4afc4cdcd`, actual `ebd6d965b2550eb31eae3b833dcaba259442c3d0a3a98f25d4dda82c8c414319`.

The existing install verifier was not bypassed, rebaselined for RLO or repaired. RLO was not installed or updated. No code root cause is assigned. The offline installed DLL has numeric version strings `1.0.6.0` and `1.0.6+7024b945c199c5b31a5754ab189c70d03a2d9904` and SHA-256 `c96709779d8bb534c05a6743d243a000eeb3e03901995da03d8d9b5593a9f6fe`; this is an offline file observation, not a verified loaded version or a #239 pass.

Optional read-only capability listing `python3 scripts/mac-agent op ops` exited 1:

```text
No bridge at http://127.0.0.1:28330: start the local Mac fallback with tools/agent-tests/mac-agent start in the Driven Rides checkout; no tunnel is needed
```

Thus current physical player-car control / walker identity binding availability could not be re-verified. No GR-9 operations ran. Console, top-speed and Player.log capabilities are documented but were not exercised; this run establishes a startup installation-verification gap, not their absence. No old Player.log was substituted for evidence at this head. No `pc-gc-measure@1` or manual console/log run could start.

### NOT RUN grouped

- P-D checks 1–7: dependent on a running private rig, blocked by the exact startup error above.
- All three size windows and their GC/human report lines: no client, no city and no measured frames.
- Optional live RLO version and current capability enumeration: startup refused; the ops listing additionally returned the exact No bridge error above.

### Fixtures, autosave and cleanup

Fixture names: **none created**. Planned fresh cities were `FT-PD-GC-20261113b` and `PD-GC-Refusal-20261113b` (the second intentionally non-FT). Neither was created or loaded, so subway-station availability and ordinary-save refusal were not verified. No existing saves were opened, renamed or copied; no fixture save exists from this run.

Before staging, **267 original files** were copied with a SHA-256 manifest: isolated ModsLocal, isolated profile root files, preferences plist and install receipt. The original plist had no key containing `autosav` (`autosave-original.json` is {}). No game-level autosave readback was possible. `--rig-test` was requested, but process launch never occurred, so autosave suppression is not claimed. No measured window or native save occurred.

Cleanup ran `python3 scripts/mac-agent stop` successfully without `--force`, restored all backed-up originals including persistent-city.json and preferences, and verified **267 files / 0 hash mismatches / autosave plist restored=true**. RLO files retain their pre-run hashes, including the original receipt discrepancy. The lease was then released: held=false, pending=0; status running=false, process=null. No game PID was started and no owned rig process remains. Windows, Steam's app, the ordinary profile, other sessions and macOS settings were untouched. No wrap marker was present. Build and evidence worktrees are removed after committing and opening the results PR; raw stage evidence remains outside Git.

### Validation

Build passed. Docs-only evidence adds no production or test changes; no full or regression suite was run. Markdown-ban and changed-document link validation are recorded in the PR. The seven in-game checks remain NOT RUN; offline restore and hash checks are separate evidence.

## Rig results — 13 November 2026 c, Mac (P-D Mono GC at 83b6156e)

Role `rig_test`, where `mac`, isolated `agentmac`. This is the 13c rerun after 13b was BLOCKED at startup at 13:27 Europe/Berlin. **0 passed / 0 partial / 0 failed / 7 NOT RUN. RESULT: BLOCKED.** The setup failure is separate from those seven counts. The existing 13b section remains unchanged. GR-5 still waits for P-D GC measurements: no population-size, pause-cost or mod-work conclusion follows from this run. In-game proof gates no merge.

`<stage>` is `/Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13c-mac`. Backup originals, manifests, errors and restore evidence remain there. Report copy: `/Users/peterhartwieg/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-13c-mac.md`. No raw logs, saves or binaries are added to Git.

### Build and rig

| Item | Observation |
|---|---|
| Candidate / verified tip | `83b6156e56275069ad684b26cfa8677dbca4b2a4`. `git fetch origin` and `git rev-parse origin/codex/persistent-city` returned the briefing candidate before build; the tip had not moved. |
| Build checkout | Detached `rig-2026-11-13c-mac`. Original Persistent City checkout remained clean at `9c617c62efee91f999695ebc6a47171e1218282e`. |
| Build / reference | `python3 tools/build.py --game-managed "/Users/peterhartwieg/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0. PATH prepended `/opt/homebrew/bin`; DOTNET_ROOT was the Homebrew dotnet prefix plus `/libexec`. |
| Candidate DLL SHA-256 | `61b8292227e17fe9609098ef40d52238b556e2e2fc73da782870b848d1a074cc`. Copied to isolated PersistentCity, then restored. |
| Official rig checkout | `e6a82372103ef3e379b5556882a433a3613a56e0`, clean before and after. No edits, commits, pushes or install commands. Read `docs/MAC.md` and relevant `docs/rig-ops.md` sections first. |
| Live AgentRigOps SHA-256 | `387dce1d54c869d4d14e6e87546659419f85c308456909bdbe11a60b773cb8b2`, identical before and after. No runtime launched to load it. |
| Rollback path, unused | `/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/updates/20261113T124248Z`. No rig install was attempted or broken; no rollback ran. |
| Resources / lease | 44.9 GiB disk, 67% memory free. Lease was free; acquired at 12:45:22 UTC with purpose `Persistent City 13 Nov c Mac rig (P-D GC)`, then released after restoration. |
| Configuration | No production code changes. Saved schema stays 1. FoodChoicePriceWeight and FoodChoiceHouseholdWeight were unchanged. Existing persistent-city.json was preserved. |
| Evidence | Existing branch `codex/mac-rig-2026-11-13b`, separate `rig-2026-11-13c-mac-evidence` worktree; `git pull --rebase origin codex/mac-rig-2026-11-13b` was already up to date at `dc9d1b07`. Existing PR #245 only. |

### Per-size measurements

| Requested persons | Status / reached | gen0/1/2 | pause_ms p50/p95/max | frame_ms_plain p95 | mod_ms_plain p95 | GC/frame spikes >=30 ms | incremental / dropped / autosave |
|---|---|---|---|---|---|---|---|
| 3,000 | NOT RUN / unavailable | unavailable | unavailable | unavailable | unavailable | unavailable | unavailable |
| 10,000 | NOT RUN / unavailable | unavailable | unavailable | unavailable | unavailable | unavailable | unavailable |
| 20,000 | NOT RUN / unavailable | unavailable | unavailable | unavailable | unavailable | unavailable | unavailable |

All sizes refer to `83b6156e56275069ad684b26cfa8677dbca4b2a4`. No `[Persistent City] gc report {json}` line or following human line was produced. Persons, reached/visitor_capacity/portals/refusals, generation counts, heap start/end/peak/before_gc/after_gc, Mono heap/used, pause percentiles, spike counts, frame/mod percentiles, max frame, incremental, dropped frames and autosave-stayed-off are unavailable. Explicit nulls are in [the 13c measurement record](perf/pd-mono-gc-2026-11-13c-mac.json), SHA-256 `901459ca6fad6cb7411b3ce9aacedb6d0638f0b97579ec23b1fd0a54c530cc8b`. The 13b JSON is unchanged.

No midnight growth, settling hour or ten-minute window began. The reason 20,000 was not reached is startup refusal, not an observed portal, capacity or population limit. There is no evidence against which to apply the <5 ms mod-work p95 target or enumerate >=30 ms spikes. No incremental flag was read. For a later incremental=true run, pause_ms understates collection work; compare frame_ms_plain p95, frame_spikes_30ms and max_frame_ms across sizes as PR #244 directs. Other agent sessions were present on this Mac; any timing would have been provisional, but this run has none.

### Per-check results

| Check | Result | Observation |
|---|---|---|
| 1. Sampler off costs nothing | **NOT RUN** | No client, usual mod-work window or previous-head comparison. No report line is not a pass. |
| 2. 3,000 persons | **NOT RUN** | No fresh FT city, target, settling hour or ten-minute window. |
| 3. 10,000 persons | **NOT RUN** | The same-city sequence never started. |
| 4. 20,000 persons | **NOT RUN** | No reached count or reason from a population status line. |
| 5. Report does not sample itself | **NOT RUN** | No sampler or three reports ten seconds apart; frames and max_frame_ms unobserved. |
| 6. Target gone after reload | **NOT RUN** | No baseline save, measurement target or reload; target=off unobserved. |
| 7. Ordinary saves refused | **NOT RUN** | No second disposable non-FT city; refusal and unchanged persons unobserved. |

### Findings and tooling gaps

One foreground start attempt ran, with no retry:

```text
python3 scripts/mac-agent start --mode headless --rig-test
exit 1
Installed file changed: /Users/peterhartwieg/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity/PersistentCity.dll
```

The unchanged install receipt pins the original Persistent City DLL SHA-256 `e0756b4a74764c4b04e65cf0edd9a2043cff9ccb4e34dea2942a9063c0582124`. The staged candidate hash is in the build table. Startup validates that receipt before launching. Peter explicitly prohibited edits to install.json and receipts; none were changed or bypassed. This is a receipt gate for the candidate DLL. The earlier RLO startup error was not returned in this run, and read-only RLO/receipt hashes remained unchanged. No code root cause is assigned.

The staging helper copied the candidate DLL, then exited 1 because it assumed the isolated old package already contained the candidate locale path:

```text
Expected existing Persistent City file: /Users/peterhartwieg/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity/Locales/en.json
```

This helper error is recorded separately from the startup refusal. No locale was copied and persistent-city.json was preserved. The single start attempt still ran and produced the exact refusal above. The original DLL was restored; no staging retry or package repair followed.

PR #244 GraphQL read returned `GraphQL: API rate limit already exceeded for user ID 95130991.` REST `gh api repos/PeterHartwieg/big-ambitions-persistent-city/pulls/244 --jq .body` succeeded. No weaker criteria were substituted. Console, speed control and Player.log access were documented but not exercised; startup refusal does not establish that those capabilities are absent. No older Player.log was used as evidence. No captures, GR-9 or #239 checks ran.

Optional offline RLO version strings read `0.45.0.0` and `0.45.0+b12d3581860ce380397223a2790fcce8b72b3482`, consistent with the requested isolated 0.45.0. This is an installed-file observation. No RLO file, receipt, backup tree or install was modified.

### NOT RUN grouped

- P-D checks 1–7 and the 3,000 → 10,000 → 20,000 windows: no client launched because candidate DLL verification refused startup.
- All gc report JSON/human lines and growth/reload/refusal evidence: dependent on that client and fresh disposable cities.
- Optional live capability enumeration: not attempted after the startup failure; no GR-9 or Living-population work.

### Fixtures, autosave and cleanup

Fixture names: **none created**. Planned names were `FT-PD-GC-20261113c` and `PD-GC-Refusal-20261113c` (intentionally non-FT). No city was opened; subway-station availability was not tested. No Peter save or Windows PC was used.

Before copying the DLL, 34 existing files were backed up with SHA-256: the isolated Persistent City tree, isolated profile root files and isolated preference plist. Backup manifest SHA-256: `030d7f1dbb77f0d63fcd5ddfef49203a974ef6c1cdd027470e9d12a5efe0d53a`. RLO and install.json were hashed read-only (49 files) and never copied for restoration. The original plist had no autosave-named key (`autosave-original.json` is {}). No game-level autosave readback was possible. The requested --rig-test process never launched, so suppression is not claimed. No measured window or save occurred.

`python3 scripts/mac-agent stop` returned stopped=true without --force. Only the changed Persistent City DLL needed restoration; all 34 original-file hashes matched afterward, including config, profile preferences and plist. All 49 read-only RLO/receipt hashes matched. AgentRigOps retained its recorded hash. `lock release` returned held=false, pending=0; final status running=false, process=null. No game process was started by this run and none remains. The existing unrelated processes were left alone. No wrap marker was present. Only the 13c build/evidence worktrees are removed after committing and updating PR #245; 13b leftovers remain untouched.

### Validation

Build passed. Evidence-only changes require no production tests; no full or regression suite ran. Markdown-ban, changed-document links, JSON parsing and diff whitespace checks passed. Raw evidence remains outside Git. PR #245 remains open and unmerged, with no reviewers requested.

## Rig results — 13 November 2026 d, Mac (P-D Mono GC, RLO 1.0.6 #239 and GR-9 at f3e3d538)

Role `rig_test`, where `mac`, isolated `agentmac`. This is 13d after 13b was blocked by startup receipt validation and 13c by the missing candidate-staging path. Both earlier sections remain unchanged. **P-D: 5 passed / 1 partial / 0 failed / 1 NOT RUN. RLO #239: 1 passed / 3 partial / 0 failed / 2 NOT RUN. GR-9: 0 passed / 2 partial / 0 failed / 8 NOT RUN. RESULT: BLOCKED** by the Living-option operation and remaining check coverage. In-game proof gates no merge.

The requested three ten-minute Mono GC windows ran. The Living-on owned-food check is verified by rig under RLO 1.0.6. Background capture now completes without the earlier client crash. No production source, schema, economy defaults, RLO files, version or changelog was changed. No release, merge or Workshop action occurred. The Windows PC, Steam app, normal profile and other sessions were not used.

`<stage>` = `/Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13d-mac`. Raw logs, screenshots, fixture extractions, commands, input corrections and backup originals stay there. Report copy: `/Users/peterhartwieg/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-13d-mac.md`. [Complete GC JSON/human lines and sampler-off records](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/codex/mac-rig-2026-11-13b/docs/perf/pd-mono-gc-2026-11-13d-mac.json), 15,771 bytes, SHA-256 `2d4e4f94cb4470955666761534dd2629e40341eea50a29957c55ba03f4cc6086`.

### Build and rig

| Item | Observation |
|---|---|
| Built HEAD | `f3e3d53859f8b21e0619edd61af1d5db22e3e259`, the existing results-branch head. Its non-docs content equals candidate `83b6156e56275069ad684b26cfa8677dbca4b2a4` (#244). |
| Base / evidence branch | `codex/mac-rig-2026-11-13b`, tracking the existing remote branch, in the new `rig-2026-11-13d-mac-evidence` worktree. The candidate was already an ancestor; no candidate-base merge was needed before build. The final fetch/normal merge of the remote results branch was already up to date at f3e3d538; no rebase or force-push. |
| Build | `python3 tools/build.py --game-managed "/Users/peterhartwieg/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0. `/opt/homebrew/bin` was prepended to PATH; DOTNET_ROOT was the Homebrew dotnet prefix plus `/libexec`. |
| Candidate DLL SHA-256 | `61b8292227e17fe9609098ef40d52238b556e2e2fc73da782870b848d1a074cc`. The installed file matched before the run. Startup receipt verification and the live measurement command identify the staged package. Player.log has the population activation line but no commit-stamped build/version line; none is invented. |
| Agent-rig HEAD | `0b18d38ece6e8fe77c35ee5ea694849c7478d054`, clean; `git fetch origin` / `git pull --ff-only` were already up to date. No edits, commits or pushes to that checkout. The final HEAD and clean tree matched the initial check. |
| Live AgentRigOps SHA-256 | `5578f9f4657fa2269ee50aa86adfc6d699881fac7e102084918da625045cd59e`; live health also reports this hash and `ready`. The final on-disk hash was unchanged. |
| RLO loaded | Player.log: `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` Capability status reports Living population on. The Living read/set operations fail as recorded below. |
| Initial resources / lease | 62.1 GiB disk / 61% memory free; lease was free and acquired at 17:55:58 UTC for this run. The initial game was stopped. All lifecycle commands used owner `persistent-city-rig-2026-11-13d-mac`. |
| Autosave original | Native setting 5 minutes / 3 saves; no autosave-named plist key. Every process used `--rig-test`, with #70 suppression confirmed after loads and before/after each window. No preference-based autosave workaround. |
| Configurations | GC and sampler-off windows used the built package defaults, physical caps 96/24 and radius 120 m. The later overlap package changed only the test setting `NeedPurchasesEnabled=true`; its DLL stayed identical. Price/household weights remained 1.2/1.0. |
| Previous-head comparison | Detached build worktree at `9c617c62efee91f999695ebc6a47171e1218282e`, the direct parent of #244; DLL `bdc95999f6b55f54da398ee8eca81079cbd7f91c5ee54e34e56cf73209556641`. Built with the same command, then staged only for the sampler-off comparison. |

Every staging command below ran from the official agent-rig checkout, stopped and leased, first with `--what-if` and then without it. The helper accepted all three; no refresh fallback, manual receipt edit or RLO install/rollback command was used. Each printed rollback is retained verbatim in `<stage>/stage.txt`, `previous-stage.txt` or `candidate-rlo-stage.txt`.

| Exact staging command | Actual receipt-backups folder |
|---|---|
| `python3 scripts/install-mac.py --stage-mod PersistentCity /Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13d-mac/package` | `/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261113T175700Z` |
| `python3 scripts/install-mac.py --stage-mod PersistentCity /Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13d-mac/package-previous` | `/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261113T192637Z` |
| `python3 scripts/install-mac.py --stage-mod PersistentCity /Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13d-mac/package-rlo` | `/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261113T193130Z` |

The first stage added `Locales/en.json` as expected. The initial backup, not either later candidate backup, is the cleanup source. Independent manifests/copies were taken before staging; the original set has 34 files, including the entire original PersistentCity folder, isolated profile root files and preference plist. RLO mod files were hashed read-only, never staged or restored.

### P-D measurements and GR-5

Timings are **provisional on the shared Mac**: other live sessions were present (coding-agent and desktop app processes were observed, without inspecting or changing their work). All three GC windows were headless, at 1× on one fixed street, after one in-game settling hour. They ran from about 01:10 to 02:50 in the city, not a daytime crowd stress scene. The 30 FPS rig cap makes an ordinary frame interval exceed the 30 ms reporting threshold. Whole-frame intervals remain separate from mod work.

| Requested / actual persons | Day / stored frames / dropped | reached / visitor_capacity / portals / refusals | gen0/1/2 | pause_ms p50 / p95 / max | frame_ms_plain p95 / max_frame_ms | mod_ms_plain p95 / max_mod_ms | GC / frame spikes >=30 ms | incremental / autosave off |
|---|---|---|---|---|---|---|---|---|
| 3,000 / **3,222** | 3 / 16,960 / 0 | 3,222 / 1,389 / 20 / 0 | 5 / 0 / 0 | 33.526 / 35.941 / 35.941 | 38.354 / 385.641 | 0.946 / 10.939 | 5 / 16,955 | true / yes |
| 10,000 / **10,000** | 4 / 16,949 / 0 | 10,000 / 8,389 / 20 / 0 | 5 / 0 / 0 | 29.533 / 36.843 / 36.843 | 38.639 / 212.053 | 1.189 / 5.605 | 5 / 16,941 | true / yes |
| 20,000 / **20,000** | 5 / 16,916 / 0 | 20,000 / 18,389 / 20 / 0 | 5 / 0 / 0 | 40.339 / 44.193 / 44.193 | 38.545 / 276.460 | 0.841 / **35.364** | 5 / 16,900 | true / yes |

All times are ms. `mod_ms_plain` times UpdateCity as #244 defines it; the sampler-off windows below use the rig's broader mod-work union. The **35.364 ms** 20,000-person maximum is flagged. The GC command exports a maximum, not a per-frame mod-work spike list; its exact spike count is unavailable. Every exported GC/frame spike count is above. Mono reported `max_generation=0` in each window.

| Actual persons | heap_start_bytes | heap_end_bytes | heap_peak_bytes | heap_before_gc_bytes | heap_after_gc_bytes | mono_heap_bytes | mono_used_bytes |
|---|---|---|---|---|---|---|---|
| 3,222 | 1303191552 | 1220288512 | 1471025152 | 1458471731 | 1281739980 | 1471660032 | 1220288512 |
| 10,000 | 1302716416 | 1327161344 | 1454264320 | 1446135398 | 1303608524 | 1458585600 | 1327161344 |
| 20,000 | 1501503488 | 1456799744 | 1560170496 | 1556833075 | 1391149875 | 1562296320 | 1456799744 |

The fresh city started above 3,000. The target never removes people; 3,222 persisted across two consecutive midnights, satisfying the stabilization stop. The later targets reached exactly. Growth stayed outside sampling. Save readback before/after the sequence stayed at `default_allowed=1`, `armed_allowed=0`, `unrestricted=0`, with saving/compression idle; all automatic attempts were refused. The one allowed save was the initial fixture checkpoint, not a measured-window save.

With `incremental=true`, `pause_ms` describes cycle-ending frames and understates total collection work. Across these observed windows, plain-frame p95 stayed 38.354–38.639 ms; maximum frame time was not monotonic with population. GR-5 now has the requested size measurements, including exact 10,000 and 20,000 populations. These data do not identify a code cause or verify GR-5's broader gameplay/performance target. P-D check 5 and the non-FT refusal check remain incomplete.

| P-D check | Result | Evidence |
|---|---|---|
| 1. Sampler off costs nothing | **PASS** | Same RLO 1.0.6 city/checkpoint, headless, 1×, 120-second union windows: candidate p95 4.8769 ms, previous head 4.8969 ms (−0.0200 ms, −0.41%). The earlier candidate window was 4.9141 ms. Previous-head p95 lies within those candidate observations. No GC command preceded the matched candidate window; no gc report line was emitted. Both matched windows have complete 61-entry-point coverage, valid timing and zero dropped frames. |
| 2. Near 3,000 persons | **PASS** | Full 600-real-second window at actual 3,222; stabilized at two midnights, zero dropped frames. Requested and actual sizes are kept distinct. |
| 3. 10,000 persons | **PASS** | Same city; full 600-real-second window, exact target and zero dropped frames. |
| 4. 20,000 persons | **PASS** | Same city; full 600-real-second window, exact target and zero refusals/dropped frames. |
| 5. Report does not sample itself | **PARTIAL** | Three reports ten seconds apart gave frames 284→566→850; stop gave 853. Max frame grew 174.007→208.456→298.525 ms; gen0 was 0→0→1. Frame growth is observed, but these aggregate reports cannot attribute the maximum increases or establish that reporting caused none of them. No weaker pass criterion is substituted. |
| 6. Target gone after reload | **PASS** | Cold restart/reload of the original FT city without saving the measurement state: `target=off reached=3222`. #70 suppression confirmed after reload. |
| 7. Ordinary save refused | **NOT RUN** | The supported new_game path refuses the disposable non-FT name before creating it. No ordinary/Peter save was used and the mod's own refusal was not exercised. |

Sampler-off windows and every recorded union-work spike >=30 ms:

| Window | Seconds / frames | Mod-work p95 / max / spikes | Whole-frame p95 / max / spikes |
|---|---|---|---|
| Initial candidate | 120.416 / 3152 | 4.9141 / 44.7533 / 1 | 64.4839 / 176.2406 / 3035 |
| Matched candidate | 120.375 / 3081 | 4.8769 / 34.3841 / 1 | 67.7599 / 209.623 / 2931 |
| Matched previous head | 120.395 / 3084 | 4.8969 / 33.5161 / 2 | 69.3143 / 283.8007 / 2928 |

| Window / Unity frame | Mod-work ms | Whole-frame ms | Dominant component |
|---|---|---|---|
| Initial candidate / 4748 | 44.7533 | 94.5057 | PopulationRuntime.Update |
| Matched candidate / 4085 | 34.3841 | 172.3043 | PopulationRuntime.Update |
| Previous / 3436 | 33.5161 | 68.0442 | PopulationRuntime.Update |
| Previous / 3911 | 31.5054 | 68.3142 | PopulationRuntime.Update |

### RLO #239

Both fixtures used the documented Mac recipes with unique id/name/checkpoint overrides, $100M funding/readback, staffed registers on cabinets and independent ready=true reads. Food: `ba:street_fourthavenue#19`; service: `ba:street_fourthavenue#23`. The service package included the head wash and all four service offers. The source/default JSON was not edited; the test override was staged through the sanctioned helper. During overlap, rlo.donor reported transient Refreshing with its saved donor catalogue in use; checkout stayed active.

| Check | Result | Observation |
|---|---|---|
| 1. Living on, owned food shop | **PASS** | The shop became managed at 10:00. From 10:00–12:00, native revenue $35→$110 matches 15 paid Persistent City receipts of 500 cents. The native log has exactly 15 visits and 15 croissant orders at $5, plus 15 normal zero-price paper bags, with no other product lines. No dropped entries or hook faults. Two native draws and hourly batches were suppressed. Checkout remained active; hooks list all five RLO calculator replacements as overruled; status says Living population on. |
| 2. Living off before load | **NOT RUN** | The pre-load set-false request failed with `ops_rlo_option_unavailable`; no off-state city was loaded. |
| 3. Mid-session toggle | **NOT RUN** | The set-false request failed with the same error. No successful transition, two-toggle status or toggled sales interval is claimed. |
| 4. Crowds | **PARTIAL** | Native walking at the RLO university, 26 Second Avenue, and background views ran. Activation/status count 574 native zones, 115 RLO zones, and report ordinary standers suppressed. Street-performer/homeless preservation and RLO CityPeople identity were not independently established in the views. The retained campus views are roof-occluded, so they are not evidence of an unobstructed crowd census. |
| 5. Traffic and RLO scenes | **PARTIAL** | A full 600.544-real-second pavement walk ran at 1×, with 363/363 arrived=true, complete-path segments and periodic background captures. The initial off-pavement waypoint refusal is recorded below. Activation logs 2/2 traffic scene switches stood down and “a scene under way ended”. Physical player-car driving, a controlled rival-door event, scene-car retention and the second Accidents-off-during-hold arm were not run; the rig exposes no supported control for those steps. Cleanup release evidence is below. |
| 6. Native-owned non-food / need basket | **PARTIAL** | Need purchases were enabled and the runtime attached without fault. The ready hairdresser remained unmanaged by food authority (`BusinessContract`, next boundary native). The 13:00–14:00 observation produced two ordinary native visits and sales ($90→$125), but natural need status stayed bought=0/refused=0 with no resident purchase receipts. RLO loyalty/interiors values were not exposed by the queried native Mods page or supported rlo_option names. Therefore no resident basket/one-entry-retirement comparison is claimed. |

The owned-food reconciliation uses actual saved `persistentcity:food-sale-v1:*` paid values, not a probe or a zero-sales equality. Its extraction is `<stage>/rlo-owned-12-pcsave.json`; the compact arithmetic is `rlo-on-reconciliation.json`. A purchase logger added `agent-rig.shop-log` observer postfixes after every performance window and before the sales interval. The hooks report identifies them; no foreign refusing prefix was added. The non-food hour used the logger independently and did not substitute ordinary native customers for residents.

### GR-9 and background capture

Five initial background `capture take` calls completed with `capture_method=render_texture`, `fallback_reason=ops_view_capture_background`, Metal, 640×480 PNGs and live rig-ops readiness afterward. Actual game/UI pixels were inspected; changed UI/view content appears in subsequent captures. The busy-street sequence then produced 94 PNGs over 121.142 seconds, all render_texture; median spacing 1.271 s (range 1.137–1.895 s). No TransformStreamHandle error or capture-induced client exit occurred. No capture-crash restart was needed. The ordinary comparison/reload restarts were separate lifecycle steps. Captures ran after all P-D timing windows.

Only the capture-dependent rows that can run without player-car control or walker/resident identity binding were re-run. Other earlier results are not silently upgraded.

| GR-9 row | Result | Observation / remaining coverage |
|---|---|---|
| 1. Walker fade-in | **PARTIAL** | Two-minute street sequence has moving walkers and clear pavement, without the prior central modal. Its 1.271-second median spacing cannot resolve a 0.75-second fade or exclude a pop between images. |
| 2. Mode-change fades | **NOT RUN** | No supported resident→walker→own-car binding for following both transitions. |
| 3. Parked cars across reload | **NOT RUN** | The full row needs model/body/owner binding and the owner's departure/destination transition; those capabilities remain absent. Not re-run as a weaker logical-record comparison. |
| 4. Own cars / no borrowing hooks | **PARTIAL** | No physicalvehicles/physicalintersections owner in the retained hooks report; ownCars was positive and Personal was held. Observed ownCars reached 3; retired rose 0→55 and loadsRetired 0→44 during the session. The full-modpack log still contains DrivenRides Patch DrivingAI.WaypointRequested, Patch GameSpeedController.SetPause and Patch TrafficManager.FixedUpdate lines. Taxi/service driving was not identified independently, so the full literal criterion is not met. |
| 5. Wardrobe and looks | **NOT RUN** | Opaque walker handles do not bind to a Persistent City resident/donor across reload. |
| 6. 96/48 performance | **NOT RUN** | Outside this capture-fix rerun; the earlier provisional FAIL remains. The sampler-off windows are not a replacement cap comparison. |
| 7. Native/player traffic against own cars | **NOT RUN** | Physical player-car control and a controlled junction sequence are absent. |
| 8. Parked occupancy | **NOT RUN** | No controlled two-resident/curb/player-car/bollard fixture or identity binding. |
| 9. Own cars stop for traffic/player car | **NOT RUN** | No stopped player-car / red-light / adjacent-turn fixture or control. |
| 10. Own cars stop for walkers | **NOT RUN** | No identified own-car / bound walker crossing or exposed layer readback. Casual walking is not collision proof. |

### Tooling gaps, exact refusals and corrected inputs

| Operation | Exact outcome | Consequence |
|---|---|---|
| `python3 scripts/mac-agent op rlo_option '{"action":"read","name":"living_population"}'` | `ops_rlo_option_unavailable` | Original live on-state was read from capability status; stored-value readback unavailable. |
| `python3 scripts/mac-agent op rlo_option '{"action":"set","name":"living_population","value":false}'` (once for each requested arm) | `ops_rlo_option_unavailable` | #239 checks 2 and 3 NOT RUN. No UI toggle, file edit or replacement RLO was used. |
| `python3 scripts/mac-agent call new_game '{"name":"PD-GC-Refusal-13d"}'` | `The bridge starts only games whose name starts with FT, not 'PD-GC-Refusal-13d'.` | P-D 7 NOT RUN. The guard was not bypassed. |
| `op ops {}`, `op screen_pos list`, `call drive {"op":"status"}` | The schemas have opaque walker/business/position targets, no stable resident/donor/car binding or physical ordinary-player-car control. Drive status returned `running:false, phase:none`; the documented drive path is an FT simulated trip. | Binding/car-control rows remain NOT RUN; no invented failing op is used as evidence. |
| `call ui {"op":"escape"}` from native Options | `Nothing is open to close (Esc would open the pause menu).` | Closed through the observed Back [Escape] and MiniMenu Close buttons. No setting value was changed. |
| Initial campus walk to x −163.39, z 335.27 | `No walkable ground within 3 m of x -163.4, z 335.3.` | Stopped the attempt safely after two successful pavement walks; restarted the full ten-minute walk on the verified two-point pavement route. |

Two input mistakes were refused before the affected action: the stage recipe used `FT-RLO...` for a display name instead of required `FT ` (`Shop name must start with "FT " so it stays recognisably disposable`); and the first teleport omitted `who` (`Missing "who": "player" or a food vehicle's id.`). Both were corrected and the documented commands succeeded. An obsolete CloseButton request after collapsing the campaign panel returned `No button or option reads "CloseButton".`; a fresh state read showed no held screen. These are setup corrections, not product failures or capture crashes.

### NOT RUN grouped

- P-D 7: supported city creation/console guards do not provide the non-FT fixture path.
- RLO #239 2–3: Living-population read/set operation unavailable. The off-before-load and two-toggle scenarios remain unverified under 1.0.6.
- GR-9 2, 3, 5, 7, 8, 9, 10: controlled physical player-car and/or stable resident/donor/body bindings are absent. GR-9 6 was deliberately not re-run because it did not depend on the background-capture fix.
- Partial portions: report self-isolation, subsecond fade observation, RLO scene/crowd identity preservation, controlled traffic and option-release arms, and a non-food resident basket purchase with verified RLO loyalty/interiors settings.

### Fixtures, restoration and validation

Created fresh cities: `FT-PD-GC-13d` and `FT-RLO-13d`. Created checkpoints: `FT RLO 13d Food v1`, `FT RLO 13d Owned 12`, `FT RLO 13d Service v1`, `FT RLO 13d Service 14`. The planned `PD-GC-Refusal-13d` was not created. Disposable fixture saves stay in the isolated profile. Actual saved population payloads report schema 1; no measurement target was saved.

The Living restore request, before stopping, also returned `ops_rlo_option_unavailable`. No Living mutation had succeeded; capability status was **on before and after** the restore attempt, matching the original live state. The stored option/journal could not be read through that op. The original isolated preferences were restored and verified independently.

All four game processes stopped normally, without `--force`. The final log reports `[Persistent City] RLO traffic scene switches given back (the player's stored options).` No after-release options-screen verification is claimed.

The execution layer rejected the printed destructive shell rollback before it ran: **`rm -f style commands are not permitted. Use a safer approach`**. Restoration then used a non-destructive equivalent: the staged PersistentCity folder was atomically moved to `<stage>/retained-staged-mod`, the initial `20261113T175700Z/mod` copied into the empty original path, and that backup's unchanged install.json copied back. The staged candidate remains retained for review. No receipt was hand-edited and no RLO backup was restored.

Verification: **34 original-file hashes matched; 0 mismatches**. The full original PersistentCity file manifest matched, including removal of the staged locale addition. Isolated preferences/configuration match their original hashes. The two preexisting RLO diagnostic text files were checked read-only, never restored. **All 53 RLO mod-file hashes match.** install.json SHA-256 is **`6668d8ef07ae4bc4281a8ac53f7611edeb85a2068ee6b60f8bdb6c45284b846b`**, equal to both the initial independent copy and the helper's backup. AgentRigOps retained its recorded hash. Autosave preferences returned to their original bytes; process-only suppression ended with the game.

Final status: `running=false`, `process=null`, lease `held=false`, `pending=0`. None of the four owned game PIDs (22012, 32132, 32403, 32630) or their caffeinate assertions remains. No post-restore game start was needed. Only the two 13d worktrees are removed after the evidence commit and PR update; 13b/13c and other sessions' leftovers remain untouched.

Candidate and previous-head builds passed. Final checks: JSON parses with all three complete report/human lines; all requested person observations and zero dropped samples match raw reports; autosave guard counts show no measured save; 363 complete walking segments; full RLO folder manifest unchanged; restore hashes and stopped/free-lease state verified. `python3 tools/docs-links/check.py`: 20 Markdown files, 0 broken relative links. `python3 tools/md-ban.py --base origin/codex/persistent-city`: no new markdown files. `git diff --check`: clean. No test source changed; no full xUnit or regression suite ran.

## Rig results — 13 November 2026 e, Mac (GR-5 at 5fe5eb23)

Role `rig_test`, where `mac`, isolated `agentmac`. **0 passed / 1 partial / 3 failed / 1 NOT RUN. RESULT: BLOCKED.** Check 1 has partial coverage and an observed broader mod-work gate failure. In-game proof gates no merge. This is the requested 13 November e session; work crossed into 14 November local time. The JSON records UTC window times.

Timing is **provisional on the shared Mac**: other coding and desktop sessions were live. Their work, files, branches and processes were not changed. No Windows PC was contacted or started.

`<stage>` = `/Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13e-mac`. Raw logs, native save profiles, screenshots, fixture extraction, requests, original-file backups and restoration evidence stay there. Report copy: `/Users/peterhartwieg/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-13e-mac.md`. [Compact GC/save evidence](perf/gr5-2026-11-13e-mac.json): 16,764 bytes, SHA-256 `5bad8740f95deef3c88483c347ee5e10644553602f9cd3584a2af3d916646905`. It includes every complete GC JSON/human line, every listed mod-work spike and all 20 valid save rows.

### Build and rig

| Item | Observation |
|---|---|
| Candidate | `5fe5eb23b3be9dcf4843592de56288abfde0193c` (#246). `origin/codex/persistent-city` was unchanged at the initial and final fetch. |
| Evidence/build checkout | New `codex/mac-rig-2026-11-13e`, based on that candidate, in `rig-2026-11-13e-mac-evidence`. One isolated worktree served build and evidence. The original Persistent City checkout was not edited. |
| Build | `python3 tools/build.py --game-managed "/Users/peterhartwieg/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0; `/opt/homebrew/bin` prepended to PATH, DOTNET_ROOT = Homebrew dotnet prefix + `/libexec`. |
| PersistentCity DLL SHA-256 | `a4991f98e95cda22318f100aacf0c7edd7f8fdfbfee069ce82c95c61ae3c30b5`. The staged DLL matched the built package; startup verified the receipt. |
| Agent-rig HEAD | `0b18d38ece6e8fe77c35ee5ea694849c7478d054`, clean before and after; fetch/pull were already up to date. No edits, commits or pushes to that checkout. |
| Live AgentRigOps SHA-256 | `5578f9f4657fa2269ee50aa86adfc6d699881fac7e102084918da625045cd59e`, unchanged at cleanup. Live ops reported ready. |
| RLO version | Player.log: `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` RLO files/options were not staged or edited. |
| Staging command | `python3 scripts/install-mac.py --stage-mod PersistentCity /Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13e-mac/package` |
| Receipt backup | `/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261113T223637Z` |
| Lease/resources | Initially stopped/free; acquired 22:36:08 UTC. 54.8 GiB disk and 84% memory free. No takeover or resource override. |
| Launches | `start --mode headless --rig-test` twice (PIDs 72169, 79205); `start --mode background --rig-test` once (PID 79665). Each start succeeded on its first attempt. |
| Autosave | Original native setting 5 minutes / 3 saves. #70 `autosave` readbacks confirmed suppression after each load and before/after every measured window. All three windows have allowed=0, native_saving_frames=0 and clean=true. |
| Configuration | Candidate defaults throughout. Price/household weights 1.2/1.0. PC SaveCostDiagnostics remained false. Only the separate save pass used the rig request `{"save_specific":true,"allow":["Default"]}`. Saved schema is 1. |
| Build-line limitation | Player.log has the Persistent City activation line but no commit-stamped build line. Identity comes from the matching package/live DLL and receipt validation; no log build stamp is claimed. |

### Per-check results

| #246 check | Result | Observation |
|---|---|---|
| 1. Frame time | **FAIL; partial coverage** | Exact 1,000 residents + 2,000 visitors, target off, 20 subway portals, zero fill refusals. Both 600-second 30-cap windows completed. UpdateCity p95 stayed below 5 ms; broader shop mod-work p95 **5.0406 ms** fails the project gate. The shop arm achieved only **5.92 fps**. Both 60 fps arms are NOT RUN because the supported request was refused. |
| 2. Save latency | **FAIL** | 20 valid paused-city manual saves: PC main-thread p50/p95/max **551.221/731.144/733.817 ms**; p95 target <40 ms. Whole native Save main-thread p50/p95/max **745.126/996.450/1011.803 ms**, separately. |
| 3. GC | **FAIL** | Separate cold-reload, settled, 600-second P-D window with modwork disarmed: **10 gen0**, pause p95 **66.260 ms**, Mono heap **1,561,460,736 bytes**. Expectations: at most 5, 35.9 ms, 1.47 GB. |
| 4. Unrentable footprint | **NOT RUN / tooling gap** | No footprint-derived Home place was exposed. Saved catalogue: 557 residential Homes, all interior-area evidence, plus 20 subway portal places. Neither exposed Home fields nor ops provide native bounds/storeys for matching the three required buildings. Captures work, but cannot supply that missing correspondence. No substitute rentable-building comparison is counted. |
| 5. Unreadable/empty tally | **PARTIAL** | Initial load and both reloads log `unrentable homes housed=0 beds=0 unreadable=0 empty=0`; no Residential places omitted warning. The line is read; no under-one-storey or unreadable candidate was exercised, so their classification/warning distinction remains unverified. |

The same fresh `FT-GR5-13e` city was filled across midnight, then sampled with 3,000 persons. No measurement population target was set. Street: player x=215, z=0. Shop: staffed/open `FT GR5 13e Food`, `ba:street_fourthavenue#21`, entrance x=176.142853, z=-36.35211. Shop revenue rose $65 to $180 and croissants fell 87 to 64 during its window. Before sampling it had already sold 13 croissants. The GC comparison cold-loaded `FT GR5 13e Timing v1`, returned to the street and settled one game hour outside timing.

### Frame time and spikes

All times are ms; fps is stored native window frames / real seconds. The 30 fps value is the requested rig cap, not an assertion of achieved cadence. P-D plain UpdateCity and explicit UpdateCity component percentiles are both retained. Whole-frame values come from the separate native `perf` window.

| Window | Seconds / frames / achieved fps | UpdateCity p95 plain / component | Broader mod-work p95 / max / spikes >=30 | Whole-frame p95 / max / spikes >=30 |
|---|---|---|---|---|
| street-30 | 600.408 / 17110 / 28.50 | 3.890 / 3.8978 | 3.9507 / 100.5736 / 4 | 36.7802 / 304.2106 / 17060 |
| shop-30 | 600.622 / 3554 / 5.92 | 4.997 / 4.9972 | 5.0406 / 43.0733 / 1 | 169.3709 / 304.5454 / 3554 |

Both modwork windows report complete 62-entry-point coverage, valid timing and zero dropped frames. Every exported mod-work spike >=30 ms is listed below; no spike was chased.

| Window / Unity frame | Mod-work ms | UpdateCity component ms | Whole-frame ms in that modwork row |
|---|---|---|---|
| street-30 / 12719 | 100.5736 | 100.5105 | 143.5717 |
| street-30 / 12781 | 30.3934 | 30.3449 | 66.5047 |
| street-30 / 12788 | 38.3335 | 38.2870 | 66.3306 |
| street-30 / 12792 | 60.8035 | 60.7607 | 95.9041 |
| shop-30 / 32241 | 43.0733 | 43.0271 | 193.1222 |

The separate GC-only pass also flags UpdateCity maxima **68.541 ms on plain frames** and **52.530 ms on GC frames**. That sampler exports maxima, not individual mod-work spike rows; those rows/counts are unavailable in this pass. Whole-frame intervals commonly exceed 30 ms at a 30 fps cap and remain separate from mod work.

### Save latency

The native profiler times `PersistentCity.Runtime.PopulationRuntime.Save` in place on the main thread; PC's record-count diagnostics were off. Whole native `Save` phase time includes the rest of that call and is shown separately. Compression ran on its named worker thread and is excluded from main-thread columns. Each next attempt waited for save/compression idle.

The first attempt was too early: the log says `[Mod:PersistentCity] Persistent City skipped the save during initialization; no Persistent City state was written: Persistent City initialization has not completed.`. Its PC callback took 0.509 ms, native Save 460.704 ms; it is retained as invalid, excluded for that explicit initialization refusal. Attempt 21 replaced it. Samples below are all **20 valid attempts 02–21**; all returned, none threw, each retained the same 9,363,620-character PC payload. Percentiles use nearest rank (10th and 19th sorted observations); no valid warm sample was discarded.

| Valid sample / checkpoint suffix | PC main-thread ms | Whole native Save main-thread ms |
|---|---|---|
| 1 / 02 | 599.606 | 779.435 |
| 2 / 03 | 568.778 | 745.126 |
| 3 / 04 | 516.741 | 716.789 |
| 4 / 05 | 731.144 | 980.679 |
| 5 / 06 | 514.815 | 696.991 |
| 6 / 07 | 551.221 | 750.381 |
| 7 / 08 | 517.043 | 701.865 |
| 8 / 09 | 555.988 | 759.827 |
| 9 / 10 | 573.853 | 791.106 |
| 10 / 11 | 523.417 | 704.978 |
| 11 / 12 | 581.189 | 762.802 |
| 12 / 13 | 512.942 | 697.476 |
| 13 / 14 | 733.817 | 996.450 |
| 14 / 15 | 522.438 | 705.399 |
| 15 / 16 | 576.107 | 762.761 |
| 16 / 17 | 518.285 | 699.884 |
| 17 / 18 | 516.532 | 695.133 |
| 18 / 19 | 577.696 | 756.426 |
| 19 / 20 | 529.525 | 712.989 |
| 20 / 21 | 594.261 | 1011.803 |
| p50 | 551.221 | 745.126 |
| p95 | **731.144** | **996.450** |
| max | 733.817 | 1011.803 |

At 1,000 residents this observed city does not meet the 40 ms save budget. No lower cap was measured and no code cause is inferred. The chosen cap is not verified by this run.

### GC

The primary comparison is `gc-clean`: a cold process, candidate defaults, one settling hour, modwork disarmed. Its complete `[Persistent City] gc report {json}` line is in the linked JSON. The first two rows are the frame-time windows with modwork armed and remain separately labelled.

| Window | Persons / frames / dropped | gen0/1/2 | pause p50 / p95 / max ms | Mono heap bytes | sampled heap peak bytes |
|---|---|---|---|---|---|
| street-30 | 3000 / 17103 / 0 | 10/0/0 | 29.574 / 65.087 / 65.087 | 1767194624 | 1766961152 |
| shop-30 | 3000 / 3551 / 0 | 4/0/0 | 34.851 / 37.354 / 37.354 | 1809498112 | 1805901824 |
| gc-clean | 3000 / 17012 / 0 | 10/0/0 | 29.844 / 66.260 / 66.260 | 1561460736 | 1545633792 |
| 13d reference | 3222 / 16960 / 0 | 5/0/0 | 33.526 / 35.941 / 35.941 | 1471660032 | 1471025152 |

Incremental GC was true; `pause_ms` measures cycle-ending frame excess, as in 13d, rather than total incremental collection work. `gc-clean` had 600.392 native window seconds, zero dropped GC samples, no allowed save, and whole-frame p95 37.8646 ms. All three requested GC comparison limits were exceeded.

### Exact operations, gaps and NOT RUN

| Operation | Exact result / gap |
|---|---|
| Documented `agent-frame-cap.json` request `{"fps":60,"vsync":0}`, then `start --mode headless --rig-test`; `health` readback | `frame cap requires a rig-test windowed launch`; requested=null, applied=false, target_frame_rate=30. `mac-agent --mode` exposes only background/headless. Both street/shop 60 fps arms NOT RUN. No launcher bypass. |
| `op pcstate {"action":"list"}` | Eight diagnostics projections; none for Home footprint/bounds/storeys. `op ops {}` has no native building-bounds/storeys operation. |
| `op pcsave {"action":"take","name":"FT GR5 13e Save 20","timeout_s":180}` | Success, schema 1, 1,000 citizens, 2,000 visitors, 1,690 places; 557 non-portal Homes, zero building-bounds evidence. Home fields expose usable area/evidence but not raw bounds/floor count. Required three-building footprint/floor comparison NOT RUN. |
| `op capture {"action":"take"}` | Two successful 640x480 Metal render_texture captures, fallback_reason=`ops_view_capture_background`; initial campaign overlay then clear street inspected. No footprint/storey proof is claimed from them. |
| `op rlo_option {"action":"read","name":"living_population"}` | `ops_rlo_option_unavailable`. Optional #239 checks 2–3 NOT RUN; no option mutation occurred. |
| `call drive {"op":"status"}`; `op ops {}` | running=false, phase=none. No supported physical ordinary-player-car control or walker-to-resident identity binding in the exposed schemas; no such check is claimed. |
| `call new_game {"name":"GR5-NonFT-13e"}` | `The bridge starts only games whose name starts with FT, not 'GR5-NonFT-13e'.` No non-FT city created. |
| Printed rollback shell | Rejected before execution: `rm -f style commands are not permitted. Use a safer approach`. Non-destructive restoration below completed instead. |

NOT RUN grouped: check 1's two 60 fps arms; all of check 4's rotated/ground-overlap/third-building comparison; optional #239 checks 2–3. The unreadable/empty classification cases remain the partial portion of check 5. The clean GC sampler's per-spike rows and a commit-stamped PC log build line are tooling limitations.

Corrected setup inputs: pre-load `op ops`/`op pcstate list` returned `ops_city_required`; early console calls said `console works in the city only; the game is at the loading now.` `call ui {"op":"state"}` returned `Unknown ui op 'state': list, click, set, type, escape or tree.`; `ui list` was then used. Speed 100 returned `speed must be above 0 and at most 60.`; fill used accepted speed 60, while every measured window used 1x. The campaign overlay was collapsed before time-based sampling. These are setup/tool refusals, not additional product failures.

### Fixtures, restoration and validation

Disposable city `FT-GR5-13e`; checkpoints `FT GR5 13e Food v1`, `FT GR5 13e Timing v1`, `FT GR5 13e Save 01` through `21`. Attempt 01 is the invalid initialization-time save; 02–21 are the 20 valid measurements. Fixture saves stay only in the isolated profile. The non-FT probe name was not created.

Independent originals/manifests were taken before staging. The helper printed this rollback verbatim:

```sh
rm -rf "/Users/peterhartwieg/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261113T223637Z/mod" "/Users/peterhartwieg/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261113T223637Z/install.json" "/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

The execution layer rejected that shell before it ran. With game stopped and lease held, the staged mod was moved intact to `<stage>/retained-staged-mod`; the helper's original `mod` folder was copied into the empty live path and its unchanged `install.json` copied back. Original preferences/root configuration were copied back; RLO files were checked read-only. Both request files returned to their original absent state. No receipt was hand-edited.

Verification: **35 original-file hashes matched, zero mismatches; the complete PersistentCity file set matched, including removal of the added locale; all 53 RLO hashes matched**. Original preferences/config/autosave bytes match. install.json SHA-256 **`1d3243f0854087b6d61603fb9611df1048f19a3b8189b61b362bcdc13555d2a9`** matches both independent and helper backups. AgentRigOps hash stayed unchanged. Process-only autosave suppression ended at stop.

All three game stops were normal, without force. Owned PIDs 72169/79205/79665 and their caffeinate assertions are gone. Final `status`: running=false, process=null, lease held=false, pending=0. The official agent-rig checkout remains clean at its recorded HEAD. Only this run's worktree is removed after its evidence commit is pushed and its PR is opened; other sessions' work is untouched.

Candidate build passed. Evidence JSON parses; all three native timing windows are save-clean; 20 valid save profiles and all listed mod-work spikes were reconciled to raw records. Documentation links passed (20 Markdown files, zero broken relative links); markdown ban passed (no new Markdown paths); diff whitespace and the under-100,000-character diff limit passed. No unit-test source changed, so no full xUnit or regression suite ran. No production code, version/changelog, RLO file, schema or food-choice weight changed. No merge or release action.

## Rig results — 14 November 2026 f, Mac (GR-5 at d237f443)

Role `rig_test`, where `mac`, isolated `agentmac`. **1 passed / 2 partial / 2 failed / 1 NOT RUN. RESULT: DONE.** These six rows count #248 checks 1–3 and the GR-5 street, footprint and tally rows once each; unavailable sub-arms are grouped below. The requested run completed; DONE does not mean the save or GC gates passed. In-game proof gates no merge.

All timing is **provisional on the shared Mac**: other live coding sessions were observed by executable name (12 matching processes at preparation), without inspecting or changing their work. No Windows PC, Steam app, normal profile or ordinary save was used. No production source, schema, RLO files/options, price/household weights, version or changelog was changed. No release, Workshop upload, issue creation or merge occurred.

`<stage>` = `/Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13f-mac`. Raw logs, complete native save profiles, diagnostics, extracted fixture payloads, backups and restoration evidence stay there. Report copy: `/Users/peterhartwieg/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-13f-mac.md`. [Compact save/GC/traffic evidence](perf/gr5-2026-11-13f-mac.json): **12,635 bytes**, SHA-256 `eeeb288985b73d3416e4ad70a80ab7b0aedee56d470858ddebe72356e0252503`. It retains the complete GC report lines, all 20 valid save phase/section rows and all exported mod-work spikes. Its raw-evidence manifest gives SHA-256 and filenames for the larger records in the stage.

### Build and rig

| Item | Observation |
|---|---|
| Candidate | `d237f443c689ec946484832443d8adf0f15de852` (#248), unchanged on origin at initial and final fetch. |
| Build/evidence checkout | New `codex/mac-rig-2026-11-13f` from that tip, in `rig-2026-11-13f-mac-evidence`. This one isolated worktree served build and evidence; the original checkout stayed clean. |
| Build | `python3 tools/build.py --game-managed "/Users/peterhartwieg/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0, with `/opt/homebrew/bin` prepended to PATH and Homebrew dotnet `/libexec` as DOTNET_ROOT. |
| PersistentCity DLL SHA-256 | `5d9ac8542e23bcc570af0b2f0098aa976fe922d2e562a137555428266891d7e6`; live staged DLL matched, and each start validated the install receipt. Both staged packages contain this DLL. |
| Agent-rig HEAD | `0b18d38ece6e8fe77c35ee5ea694849c7478d054`, clean before/after; fetch and ff-only pull were already up to date. No edits, commits or pushes to that checkout. |
| AgentRigOps SHA-256 | `5578f9f4657fa2269ee50aa86adfc6d699881fac7e102084918da625045cd59e`, unchanged at cleanup; live ops reported ready. |
| RLO | Player.log: `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` No RLO package or option was staged or edited. |
| PC log identity | Fresh activation: `Persistent population active: 1000 residents, 1000 visitors, 1683 places.` After fill, readback is 1,000/2,000. The log has no commit-stamped PC build line; identity is established by the matching DLL and receipt validation, rather than an invented log stamp. |
| Lease/resources | Initially stopped/free; acquired 2026-11-14 01:49:46 UTC. 54.8 GiB disk / 78% memory free. No takeover, resource override or process cancellation. |
| Launches | Six headless `start --mode headless --rig-test` launches, each successful on its first attempt. Cold starts covered save profiling, GC, the separate street window and both old-format loads. Every stop was graceful; no --force. |
| Autosave original | Native 5 minutes / 3 saves. #70 process suppression was confirmed after each load and before/after every measured window. Original preference/config bytes are restored; the process-only guard ended at stop. |
| Configuration | SaveCostDiagnostics=true for save-pass preparation/profiling; staged false before the cold GC run and kept false thereafter. Only the save-profiling process used profile `agent-save-mode.json` with `{"save_specific":true,"allow":["Default"]}`. FoodChoicePriceWeight/FoodChoiceHouseholdWeight stayed 1.2/1.0. Schema stays 1. |

Both stage commands ran from the official agent-rig checkout with the game stopped and lease held. Packages contain no RLO files. The first backup is the restoration source, not the later diagnostic package backup.

| Exact command | Receipt backup |
|---|---|
| `python3 scripts/install-mac.py --stage-mod PersistentCity /Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13f-mac/package` | `/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T015030Z` |
| `python3 scripts/install-mac.py --stage-mod PersistentCity /Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13f-mac/package-default` | `/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T020250Z` |

### Per-check results

| Check | Result | Observation |
|---|---|---|
| #248 1 / GR-5 save latency | **FAIL** | Full PC callback p95 742.826 ms; recorded phase total p95 301.027 ms. Both exceed p95 <40 ms. The full callback includes the explicitly separate diagnostic record count. Whole native Save p95 935.216 ms. |
| #248 2 / GR-5 GC | **FAIL against the 13d reference** | Day: 12 gen0 / pause p95 65.028 ms / heap 1,493,630,976 bytes. Night: 6 / 31.977 ms / 1,491,255,296 bytes. Night pause is below 13d; night collections and heap are higher. No runtime exception; the observed comparison limits are exceeded. |
| #248 3 / old-format reload | **PASS** | The 5fe5eb23 fixture's arrays loaded, ran 2.022 game minutes, saved with string traffic and cold-reloaded without refusal. Waiting queue state survived; an in-flight car advanced nine route legs. The post-reload traffic projection is identical to the converted checkpoint. |
| GR-5 1 / street frame time | **PARTIAL** | Requested 600 s at 30 fps completed. UpdateCity p95 4.2071 ms; broader mod-work p95 4.2557 ms, both <5 ms. Two >=30 ms spikes are listed below. The 60 fps arms remain NOT RUN; the shop arm was outside this requested street rerun. |
| GR-5 4 / building bounds/storeys | **NOT RUN / tooling gap** | `op ops {}` and `op pcstate {"action":"list"}` expose no native Home bounds/storeys correspondence. No rotated/ground-overlap/third-building visual comparison was substituted. |
| GR-5 5 / unreadable-empty tally | **PARTIAL** | Fresh activation and reloads log `unrentable homes housed=0 beds=0 unreadable=0 empty=0`. The line is verified; unreadable and under-one-storey fixtures were not exercised, so their classification distinction is not established. |

### Save latency and per-section costs

**FAIL.** Twenty valid paused-city saves, attempts 02–21, with SaveCostDiagnostics=true and the native save profiler installed. Attempt 01 is excluded for the exact initialization refusal: `Persistent City initialization has not completed.` It has no successful diagnostic save record; attempt 21 replaces it. Every next attempt waited for native save and compression idle. No valid warm sample was discarded. Percentiles use nearest rank (10th and 19th of 20).

| Measurement | p50 ms | p95 ms | max ms |
|---|---|---|---|
| Full PC main-thread callback | 649.028 | 742.826 | 800.880 |
| Whole native Save main thread | 811.508 | 935.216 | 994.246 |
| PC recorded phase total | 233.823 | 301.027 | 303.928 |
| captureMs | 18.792 | 22.188 | 25.553 |
| validateMs | 51.373 | 86.864 | 91.344 |
| encodeMs | 148.096 | 177.431 | 205.459 |
| recordCountMs (outside phases) | 372.582 | 507.699 | 573.419 |

The full PC callback includes the diagnostic record count; `recordCountOutsidePhases=true` explicitly excludes that count from `totalMs` and the phase columns. Both the full callback and the recorded phase total exceed the p95 <40 ms target. Compression uses the named worker thread and is excluded from the main-thread columns.

13e, diagnostics off: PC callback 551.221/731.144/733.817 ms; whole native Save 745.126/996.450/1011.803 ms. This pass has diagnostics on, so its callback comparison includes additional measurement work. #248 projected about 415 ms; that projection is not substituted for these observations.

Payloads span **5,791,344–5,798,806 characters**, against 9,363,620 in 13e and about 6.2 M expected by #248. The extracted fresh fixture has 1,000 residents, 2,000 visitors, 1,683 places and 13,965 traffic links/definitions. It is a different saved city from 13e's 1,690-place fixture. Schema is 1; Traffic.Links and Traffic.Definitions are strings.

| Section | Encode mean ms | Encode p95 ms | Validate mean ms | Validate p95 ms |
|---|---|---|---|---|
| header | 0.056 | 0.060 | 0.001 | 0.001 |
| places | 25.258 | 25.422 | 2.468 | 2.869 |
| residents | 59.984 | 84.657 | 20.319 | 23.571 |
| visitors | 42.627 | 48.522 | not exported | not exported |
| traffic | 6.855 | 7.157 | 19.556 | 44.719 |
| food | 16.870 | 17.023 | 12.244 | 14.555 |
| economy | 0.027 | 0.029 | 0.624 | 0.764 |

Validation exports no independent visitors field. The compact evidence retains every sample's capture/validate/encode times and both section maps; full readbacks and native profiles remain in the stage.

| Attempt / checkpoint suffix | PC callback ms | Whole native Save ms | Recorded phases ms | Record-count ms | Payload chars |
|---|---|---|---|---|---|
| 02 | 669.010 | 822.071 | 243.879 | 424.285 | 5,798,806 |
| 03 | 659.091 | 805.678 | 231.358 | 427.708 | 5,798,806 |
| 04 | 649.028 | 796.537 | 227.092 | 421.914 | 5,798,806 |
| 05 | 800.880 | 994.246 | 227.430 | 573.419 | 5,798,806 |
| 06 | 628.130 | 808.629 | 235.450 | 392.657 | 5,798,806 |
| 07 | 670.573 | 816.199 | 303.928 | 366.622 | 5,798,806 |
| 08 | 669.616 | 815.830 | 301.027 | 368.568 | 5,792,868 |
| 09 | 670.026 | 814.780 | 298.986 | 371.020 | 5,791,344 |
| 10 | 664.476 | 811.508 | 253.460 | 410.993 | 5,791,344 |
| 11 | 658.220 | 803.668 | 229.472 | 428.728 | 5,791,344 |
| 12 | 665.670 | 812.479 | 233.655 | 431.992 | 5,791,344 |
| 13 | 647.415 | 925.418 | 233.781 | 413.602 | 5,791,344 |
| 14 | 598.631 | 814.887 | 232.530 | 366.078 | 5,791,344 |
| 15 | 605.430 | 810.528 | 233.429 | 371.979 | 5,791,344 |
| 16 | 604.876 | 808.164 | 232.273 | 372.582 | 5,791,344 |
| 17 | 604.639 | 815.314 | 233.823 | 370.792 | 5,791,344 |
| 18 | 598.014 | 808.733 | 234.933 | 363.058 | 5,791,344 |
| 19 | 598.578 | 768.685 | 235.788 | 362.768 | 5,791,344 |
| 20 | 599.542 | 751.933 | 236.556 | 362.965 | 5,791,344 |
| 21 | 742.826 | 935.216 | 235.104 | 507.699 | 5,791,344 |

### GC windows

The same fresh city and fixed street (player x=215, z=0) were used, at 1×. After a cold reload of `FT GR5 13f Timing v1`, the city settled outside timing to 15:30. The night window followed an untimed advance to 01:00. Mod-work sampling and native save profiling were off throughout both GC windows; SaveCostDiagnostics readback was false. All measured windows have allowed=0, native_saving_frames=0 and clean=true. No save entered native work.

| Window | Native seconds / frames / dropped | Game clock | gen0/1/2 | Pause p50/p95/max ms | Mono heap bytes | Sampled heap peak bytes | Whole-frame p95/max ms |
|---|---|---|---|---|---|---|---|
| gc-day | 600.504 / 16768 / 0 | day 2 15:30–18:24 | 12/0/0 | 25.328/65.028/65.028 | 1,493,630,976 | 1,483,522,048 | 44.4502/236.0063 |
| gc-night | 600.363 / 17076 / 0 | day 3 01:00–04:14 | 6/0/0 | 17.381/31.977/31.977 | 1,491,255,296 | 1,414,967,296 | 37.7826/327.4926 |
| 13d reference | 600 s / 16960 / 0 | 01:10–04:22 | 5/0/0 | 33.526/35.941/35.941 | 1,471,660,032 | 1,471,025,152 | plain-frame p95 38.354 |
| 13e reference | 600.392 / 17012 / 0 | daytime, around 15:27 | 10/0/0 | 29.844/66.260/66.260 | 1,561,460,736 | 1,545,633,792 | 37.8646 |

Night is the requested time-of-day comparison with 13d, which had 3,222 persons; this run and 13e have 3,000. Day versus 13e: gen0 increased 10→12, pause p95 66.260→65.028 ms, heap 1.561→1.494 GB. Night versus 13d: 5→6, 35.941→31.977 ms, 1.472→1.491 GB. This is observed comparison evidence, not a code root cause.

Incremental GC is true and max_generation=0. Pause measures cycle-ending frame excess, not all incremental collection work. Day flags UpdateCity maxima **65.605 ms on plain frames** and **31.580 ms on GC frames**. Night maxima are 6.387 and 4.795 ms. The GC sampler exports maxima, not individual mod-work spike rows; those rows/counts cannot be recovered from this operation. Both complete `[Persistent City] gc report {json}` lines are retained verbatim in the compact evidence.

### Street mod-work and whole-frame timing

The street pass cold-reloaded the same checkpoint and settled to the same 15:30 start. It used a separate 600 s window with the GC sampler off. Autosave was checked before/after. Coverage is complete at 62 entry points, timing valid, zero dropped frames and no native save work.

| Native seconds / frames / achieved fps | UpdateCity p95 / max ms | Broader mod-work p95 / max / >=30 ms spikes | Whole-frame p95 / max / >=30 ms frames |
|---|---|---|---|
| 600.215 / 16771 / 27.94 | 4.2071 / 74.0022 | 4.2557 / 74.0481 / 2 | 44.2859 / 343.1558 / 16428 |

13e street: UpdateCity p95 3.8978 ms, broader mod-work 3.9507 ms, whole-frame 36.7802 ms. The new street observations stay below the 5 ms mod-work gate. They do not replace 13e's separate shop-window result of 5.0406 ms.

| Unity frame | Mod-work ms | UpdateCity component ms | Whole-frame ms in that mod-work row |
|---|---|---|---|
| 3411 | 32.3857 | 32.3608 | 64.5939 |
| 3440 | 74.0481 | 74.0022 | 98.1135 |

These are all exported mod-work spikes >=30 ms; none was chased. Whole-frame intervals at a 30 fps cap commonly exceed 30 ms and are reported separately.

### Old-format save and reload

The retained `FT GR5 13e Save 20` was written by 5fe5eb23. Its source SHA-256 is `a0526acf16f10bac951968f8bf96d368b823743001eb796ba6943c1a3c717297`. Extraction confirmed arrays for Traffic.Links/Definitions, 13,965 entries each, and three live traffic vehicles. `c342` was ready/waiting; `pc:197` and `pc:431` were in flight. No previous build or RLO restaging was needed, and the source save was not overwritten.

A cold load on d237f443 succeeded. At 1× it ran **2.0221786 native game minutes**, then paused and saved `FT GR5 13f Converted v1`. A further cold process loaded that checkpoint, waited for physical initialization, stayed paused and saved the verification checkpoint `FT GR5 13f Reloaded v1`. Autosave suppression was checked after each load. Both extractions have schema 1, string-valued Traffic.Links and Traffic.Definitions, 13,965 records per string and no accounting-stop marker.

| Saved logical traffic | Old source | After two-minute interval / conversion | After cold reload / verification save |
|---|---|---|---|
| Minute | 3747.9336547851562 | 3750.0 | 3750.0 |
| Active traffic vehicle count | 3 | 2 | 2 |
| In-flight example `pc:197` | route index 72, order 173 | route index 81, order 173, now queued | same route index, order and timing |
| Waiting example `c342` | route index 45, order 177 | still queued, same record | same queue entry, order and timing |

The complete converted/reloaded traffic projections compare equal, including queues, IDs, route indices, ReadyAt/EnteredAt/MotionStartedAt and orders. This verifies saved logical traffic continuity; it makes no physical player-car or walker identity claim.

### NOT RUN and tooling gaps

| Exact request / discovery | Result | Coverage |
|---|---|---|
| Isolated profile `agent-frame-cap.json` = `{"fps":60,"vsync":0}`, then `python3 scripts/mac-agent start --mode headless --rig-test`, health readback | `frame cap requires a rig-test windowed launch`; applied=false, target_frame_rate=30 | Both GR-5 60 fps arms NOT RUN. Supported Mac modes are background/headless; no launcher bypass. |
| `python3 scripts/mac-agent op ops '{}'`; `op pcstate '{"action":"list"}'` | Ops have no native Home bounds/storeys operation; eight diagnostics projections, none for Home footprint/floor count | GR-5 4 rotated, ground-overlap and third-building comparison NOT RUN. No invented failing op. |
| `python3 scripts/mac-agent op rlo_option '{"action":"read","name":"living_population"}'` | `ops_rlo_option_unavailable` | Optional #239 checks 2 and 3 NOT RUN. No setter or option mutation was attempted. |
| `python3 scripts/mac-agent call drive '{"op":"status"}'`; `op ops '{}'` | running=false, phase=none; schemas expose no physical ordinary-player-car control or walker/resident identity binding | Availability recorded only; no unsupported car/binding check claimed. |
| `python3 scripts/mac-agent call new_game '{"name":"GR5-NonFT-13f"}'` | `The bridge starts only games whose name starts with FT, not 'GR5-NonFT-13f'.` | No non-FT fixture created; guard not bypassed. |
| Printed rollback shell below | Rejected before execution: `rm -f style commands are not permitted. Use a safer approach` | Non-destructive equivalent completed and verified below. |

NOT RUN grouped: both 60 fps arms; all of GR-5 4's three-building comparison; optional #239 2–3. GR-5 5's unreadable/under-one-storey classification remains partial. Missing per-spike rows in the GC-only sampler and a commit-stamped PC log build line are readback limitations. Discovery advertised: ops, panel, rival, weather, catalog, metadata, campaign, shop_fixture, shop_state, shop_log, shop_edit, forced_event, camera, screen_pos, capture, save, pcsave, perf, modwork, pcstate, ledger, access, rival_fingerprint, foreign_patch, probe_checkpoint and rlo_option.

### Findings, fixtures and restoration

At 1,000 residents, this city still spends hundreds of milliseconds in a manual save on the main thread. Its 30-cap street mod-work p95 passes, while the 40 ms save budget and the collection/heap comparison with 13d do not. This run does not verify the 1,000 cap against the save budget. No lower cap was measured or changed, and no code root cause is inferred.

Created: fresh city `FT-GR5-13f`; checkpoints `FT GR5 13f Timing v1`, `FT GR5 13f Save 01`–`21`, `FT GR5 13f Converted v1`, `FT GR5 13f Reloaded v1`. Attempt 01 is the initialization-time non-measurement. Reused, not overwritten: `FT GR5 13e Save 20`. All disposable saves stay in the isolated profile. `GR5-NonFT-13f` was not created.

Before staging, an independent manifest and originals covered the entire original PersistentCity folder, isolated root preference/config files, plist, install.json and request-file absence. RLO mod files were hashed read-only. The helper printed this first rollback verbatim:

```sh
rm -rf "/Users/peterhartwieg/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T015030Z/mod" "/Users/peterhartwieg/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T015030Z/install.json" "/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

The execution layer rejected that shell before it ran. With the game stopped and lease held, the staged folder was retained intact as `<stage>/retained-staged-mod`; the helper's original `mod` folder was copied into the empty live path and its unchanged install.json copied back. Changed original preference/root configuration files were copied back from the independent backup. RLO mod files were never restored or rewritten. No receipt was hand-edited.

Verification: **35 original-file hashes and 4 absent paths matched, zero mismatches; complete PersistentCity file set matched; all 53 RLO hashes matched.** Original preferences, persistent-city.json and autosave bytes match. install.json SHA-256 `35e1dccafc8fed62ef9406ec7834c6526a290abb07de6a0f5f912d03cc8144dc` matches both independent and helper backups. AgentRigOps stayed unchanged. All stops were graceful. Final status: running=false, process=null, held=false, pending=0. All six owned game PIDs (12298, 12988, 18392, 19657, 20303, 20539) and their caffeinate assertions are gone. The rig checkout remains clean at 0b18d38.

Validation: candidate build passed; compact evidence JSON parses and is under 20 KB; report is appended to the existing checklist with no new markdown path. The diff contains only the checklist and the small evidence JSON. No full unit suite or regression suite was run for this docs-only result. The single build/evidence worktree is removed after commit/push and PR creation.

## Rig results — 14 November 2026 g, Mac (GR-5 GC at 37a150d3)

Role `rig_test`, where `mac`, isolated `agentmac`. **Counts: 0 passed / 0 partial / 3 failed / 6 NOT RUN.** The three failures are #250 checks 1–3; the six NOT RUN arms are enumerated below. Housing and optional capability readbacks are observations outside those verdict counts. **RESULT: DONE** means the run and restoration are complete; the performance targets did not pass. In-game proof gates no merge.

Timings are **provisional on the shared Mac**: other coding-agent and desktop processes were live. None was contacted, stopped or inspected beyond the process inventory. All commands and three 600-second waits ran in the foreground.

`<stage>` = the rig staging directory for this run. Raw command output, logs and backups remain there. Report copy: `<report-copy>/persistent-city-rig-2026-11-13g-mac.md`. [Compact evidence](perf/gr5-2026-11-13g-mac.json): **19,027 bytes**, SHA-256 `9a6b270848f81ee6f6e47cb47e2746643211a2c0a8461109756a0014b9be7c55`. It retains both complete `pc gc report` JSON/human lines, allocation sample counts, window/save guards, both street spikes, camera readbacks and a filename/hash manifest for larger raw records.

### Build and rig

| Item | Observation |
|---|---|
| Candidate | `37a150d330c294bb64943a39dc1eaa125618cc77` (#250). `origin/codex/persistent-city` was unchanged at initial and final fetch. |
| Checkout | New `codex/mac-rig-2026-11-13g`, based on that tip, in `rig-2026-11-13g-mac-evidence`. One new isolated worktree served build and evidence. Original checkout stayed clean. |
| Build | `python3 tools/build.py --game-managed "<managed-assemblies>"`, passed. PATH included `/opt/homebrew/bin`; DOTNET_ROOT was the Homebrew dotnet prefix plus `/libexec`. |
| PersistentCity DLL SHA-256 | `5557daaed9fe142c12978f66add0de8831d822244bc4c8e42273a3391d9b1bdc`; built, packaged and live staged files matched. |
| Agent-rig HEAD | `0b18d38ece6e8fe77c35ee5ea694849c7478d054`, clean before/after. `git fetch origin` and `git pull --ff-only` were already up to date. No checkout edits, commits or pushes. |
| AgentRigOps SHA-256 | `5578f9f4657fa2269ee50aa86adfc6d699881fac7e102084918da625045cd59e`, unchanged at cleanup; live ops ready. |
| RLO Player.log line | `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` No RLO file or option was changed. |
| PC Player.log identity | `[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places.` No commit-stamped PC build line is emitted; the matching DLL and validated receipt establish package identity. |
| Lease / resources | Initially stopped/free; acquired `2026-11-14T04:41:23.7511250Z` for this run. 54.5 GiB disk / 78% memory free. No takeover or resource override. |
| Start / stop | Two `python3 scripts/mac-agent start --mode headless --rig-test` launches, each successful on its first attempt. One process for day/night GC; one cold restart for street. Both stops graceful; no force. |
| Autosave original | Native 5 minutes / 3 saves, `prevent_autosave=false`. #70 `autosave` readback confirmed `suppressed=true` after both loads and before/after every window. Original preference/config bytes restored; process-only suppression ended at stop. |
| Config | SaveCostDiagnostics=false; native save profiler not installed. Physical caps 96/24, radius 120 m. FoodChoicePriceWeight/FoodChoiceHouseholdWeight stayed 1.2/1.0; writer schema 1. |

Staged once, stopped and leased, from the official agent-rig checkout. The package contained no RLO files.

| Exact staging command | Receipt backup |
|---|---|
| `python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package` | `<receipt-backups>/20261114T044216Z` |

### Per-check outcomes

| #250 check | Result | Observed versus expected |
|---|---|---|
| 1. Day GC, locked-PC | **FAIL** | 11 gen0, pause p95 56.671 ms versus <= about 5 / 36 ms. Reported Mono heap 1,366,654,976 bytes is below the about 1.47 GB target. Physical is the largest measured segment: 805,834,752 bytes. |
| 2. Night GC, locked-PC | **FAIL** | 8 gen0, pause p95 104.551 ms versus <= 5 / about 36 ms. Physical is the largest measured segment: 379,510,784 bytes. |
| 3. Street mod-work, input-only | **FAIL** | UpdateCity p95 5.1413 ms versus <5 ms. Broader mod-work p95 5.1897 ms. Two >=30 ms spikes, listed below. Nine camera pans succeeded. |

These are observed threshold failures, with no operation exception ending a measured window. Both GC windows used `pc gc start`, 600 s without game input, then `pc gc report` and `pc gc stop`. Mod-work sampling was unarmed throughout GC. Street used a separate cold load with the GC sampler off. All three used the same `FT GR5 13f Timing v1` checkpoint, player x=215, z=0, 1,000 residents / 2,000 visitors / 1,683 runtime places. The census reports 1,663 native places plus 20 discovered stations; it is a separate projection.

### GC and allocation results

The day clock began at day 2 15:30 and ended 18:24. An untimed advance then reached day 3 01:00; night ended 04:10. Speed was 1 during measurement. Day and night native union windows were 600.636 / 600.799 s, with 0 allowed saves, 0 native-saving frames and `clean=true`. Automatic attempts were refused (11,600 / 17,054). No save latency was measured.

| Window | Stored frames / dropped | gen0/1/2 | Pause p50 / p95 / max ms | Mono heap bytes | Sampled managed heap peak bytes | Whole-frame p95 / max ms |
|---|---|---|---|---|---|---|
| 14g day | 16868 / 0 | 11/0/0 | 25.904 / 56.671 / 56.671 | 1,366,654,976 | 1,433,260,032 | 43.2988 / 484.8029 |
| 14g night | 17048 / 0 | 8/0/0 | 29.953 / 104.551 / 104.551 | 1,465,794,560 | 1,689,186,304 | 37.8310 / 450.2231 |
| 13f day, d237f443 | 16768 / 0 | 12/0/0 | 25.328 / 65.028 / 65.028 | 1,493,630,976 | 1,483,522,048 | 44.4502 / 236.0063 |
| 13f night, d237f443 | 17076 / 0 | 6/0/0 | 17.381 / 31.977 / 31.977 | 1,491,255,296 | 1,414,967,296 | 37.7826 / 327.4926 |
| 13d night reference, code 83b6156e | 16960 / 0 | 5/0/0 | 33.526 / 35.941 / 35.941 | 1,471,660,032 | 1,471,025,152 | plain-frame p95 38.354 |
| #250 expectation | 600 s | day/night <= about 5 | p95 <= about 36 | day <= about 1.47 GB | — | separate from mod work |

Reported Mono heap is the report-time value; the larger night managed-heap peak is retained separately. The 13d reference had 3,222 persons; 13f and this run have 3,000. Day collections fell 12→11 and pause p95 65.028→56.671 ms versus 13f; night rose 6→8 and 31.977→104.551 ms. Neither reaches the #250 GC targets. Incremental GC=true, max_generation=0; pause describes cycle-ending frame excess, not all incremental collection work.

| Counter | Day bytes | Day samples / skipped | Night bytes | Night samples / skipped |
|---|---:|---|---:|---|
| frame_alloc_bytes | 1,787,936,768 | alloc_frames=16,857 | 1,241,608,192 | alloc_frames=17,040 |
| mod_alloc_bytes | 1,091,997,696 | 16,868 / 1 | 517,779,456 | 17,048 / 1 |
| advance_alloc_bytes | 219,332,608 | 16,775 / 0 | 91,938,816 | 16,955 / 0 |
| physical_alloc_bytes | 805,834,752 | 16,774 / 1 | 379,510,784 | 16,954 / 1 |
| mod minus advance minus physical | 66,830,336 | derived total | 46,329,856 | derived total |

These diagnostic counters sum observed heap growth and exclude segment calls that see a collection. Physical has the largest measured segment total in both windows; this identifies the segment, without a code root-cause claim.

GC-only UpdateCity maxima >=30 ms: **day plain 94.984 ms, day GC 57.374 ms; night plain 86.925 ms, night GC 91.871 ms**. Day plain exceeds 13f's 65.605 ms. The GC operation exports maxima rather than individual spike frames/counts, so those four maxima cannot be expanded into a per-frame list with the available operation.

### Street mod-work and whole-frame results

Cold-reloaded the same checkpoint and settled outside timing to day 2 15:30; street ended 17:36. The camera initially looked at (215,1,0) from (215,4,-9). `op camera look_at` and `release` changed its target between x=210 and 220 every roughly 60 s (nine successful pans), retaining y=4 m. The initial movement readback had 7 followers. This is the 13f street location; no visual crowd-density or building comparison is claimed from headless readbacks.

| Window | Native seconds / whole-frame samples / achieved fps | UpdateCity p95 / max ms | Broader mod-work p95 / max / >=30 ms | Whole-frame p95 / max / >=30 ms frames |
|---|---|---|---|---|
| 14g street | 600.184 / 3493 / 5.82 | 5.1413 / 66.8762 | 5.1897 / 66.9196 / 2 | 197.0065 / 541.8071 / 3493 |
| 13f street | 600.215 / 16771 / 27.94 | 4.2071 / 74.0022 | 4.2557 / 74.0481 / 2 | 44.2859 / 343.1558 / 16428 |

The mod-work recorder spans 601.020 s / 3,494 frames; the separate native union spans 600.184 s / 3,493 samples. Both bracket the full 600-second wait; their command boundaries differ. Coverage complete at 62 entry points, validity=true, no missing components, zero dropped frames, zero off-main-thread/cross-frame/unbalanced scopes and zero UpdateCity exceptions. Autosave suppression confirmed before/after, allowed=0, native_saving_frames=0, clean=true. Whole-frame counts include the rig's 30 FPS cap; this run achieved only 5.82 FPS. The shared-host and camera conditions limit causal comparisons with 13f.

Every exported mod-work spike >=30 ms (2 listed / 2 total):

| Unity frame | Mod-work ms | Dominant component / group | UpdateCity ms | Whole-frame ms in that row | Condition |
|---|---|---|---|---|---|
| 4282 | 66.9196 | updatecity / updatecity | 66.8762 | 224.4750 | hour=15 |
| 7308 | 39.5754 | updatecity / updatecity | 39.5327 | 190.1125 | hour=17 |

Full top-component maps are in the compact JSON. Spikes are reported without a follow-up code investigation.

### Save latency and housing readback

| Save-latency check | Samples | Result |
|---|---:|---|
| Native / Persistent City save latency | 0 | **NOT RUN**: explicitly deferred pending the owner's save-route decision. No save trigger, save profiling or measurement checkpoint in this run. |

Player.log on both loads: `[Persistent City] unrentable homes housed=0 beds=0 unreadable=0 empty=0`. Recorded as requested; this reused checkpoint does not demonstrate new housing allocation. Census: 1,000 residents, 471 households, 557 homes, 1,791 beds, residents_without_home=0; household/job/student capacity checks=ok.

### NOT RUN and tooling gaps

The six counted NOT RUN arms: **save latency (1); GR-5 check 4 building bounds/storeys (1); both 60 FPS arms (2); optional #239 checks 2–3 (2)**.

| Exact operation / discovery | Result | Coverage |
|---|---|---|
| `python3 scripts/mac-agent op ops '{}'`; `op pcstate '{"action":"list"}'` | No native Home bounds/storeys op; eight diagnostics projections, none for Home footprint/floors. | GR-5 4 rotated / ground-overlap / third-building comparison NOT RUN. No invented failing op. |
| `op ops '{}'` and health frame_cap readback | No FPS-changing operation advertised; target_frame_rate=30. Agent-rig HEAD unchanged from 13f; supported Mac launcher modes remain headless/background. | Both 60 FPS arms NOT RUN. No frame-cap request file or launcher override was created. |
| `python3 scripts/mac-agent op rlo_option '{"action":"read","name":"living_population"}'` | `ops_rlo_option_unavailable` | #239 2–3 NOT RUN. No option setter attempted; option left unchanged. |
| `python3 scripts/mac-agent call drive '{"op":"status"}'`; `op ops '{}'` | running=false, phase=none; no physical ordinary-player-car control or walker/resident identity binding advertised. | Availability readback only. |
| `python3 scripts/mac-agent call new_game '{"name":"GR5-NonFT-13g"}'` | `The bridge starts only games whose name starts with FT, not 'GR5-NonFT-13g'.` | Non-FT disposable city unavailable; guard not bypassed. |

Additional readback limitations: Player.log lacks a commit-stamped PC build line; GC-only sampling lacks individual mod-work spike rows. Neither has been invented.

Setup command errors, all outside measurement and corrected before the relevant window:

| Request | Exact error / correction |
|---|---|
| `call move {"x":215,"z":0}` | `Unknown command 'move'. Commands: state, console, debug, time, money, teleport, walk, drive, vehicle, commissary, crew, route, price, marketing, use, taxi, phone, call, eat, floorplan, place, drop, pickup, handtruck, fixture, logistics, parks, day_summary, places, map, ui, shoot, save, load, new_game, quit, op.` Used documented teleport. |
| `call teleport {"x":215,"z":0}` | `Missing "who": "player" or a food vehicle's id.` Corrected to `{"who":"player","x":215,"z":0}`. |
| `call console {"line":"pc inspect"}` | `Warning: ERROR: 'pc' doesn't take 1 parameter(s). Available command(s):` followed by the signatures retained in `<stage>/inspect.json`. Used supported census/movement/pcstate readbacks. |
| `call time {"run_until":{"time":"01:00"},"speed":20,"keep_needs":true}` | `day 2 01:00 has passed already: it is day 2 18:24.` Corrected to `day:"+1"`; paused exactly at day 3 01:00. |

Exact `pc inspect` response:

```text
Warning: ERROR: 'pc' doesn't take 1 parameter(s). Available command(s):
    - <b>pc [String area] [String action]</b>: pc population census | pc gc start|report|stop | pc vendor status | pc vendor sales | pc food decisions | pc food contract | pc probe status | pc probe businesses
    - <b>pc [String area] [String action] [String detail]</b>: pc population person <id> | pc gc population <persons|off> (FT cities only, measurement) | pc vendor withdraw <vehicle> | pc food contract <status|capabilities|hints|stamps|catalogue> (FT cities only)
    - <b>pc [String area] [String action] [String detail] [String value]</b>: pc movement corrections since <seq> | pc probe <resident> <place> <ceiling cents> (FT cities only)
```

The first early population read returned `[Persistent City] gc population refused: no active city.` after native load completion but before PC activation. A later read confirmed all 3,000 persons before sampling. No command was replayed after an ambiguous timeout.

### Findings, fixtures and restoration

At the 1,000-resident cap, these observed day/night GC windows exceed the requested collection/pause targets, and the panning street UpdateCity p95 exceeds 5 ms. The report-time Mono heaps are lower than 13f, while the night sampled managed peak is higher. This run does not establish the cap against those performance targets. Save latency is unmeasured here. No code cause or lower-cap result is inferred.

Reused fixture: `FT-GR5-13f`, loaded checkpoint `FT GR5 13f Timing v1`. No new city or checkpoint was created and no existing fixture save was overwritten. `GR5-NonFT-13g` was refused. Disposable saves remain in the isolated profile.

Before staging, an independent SHA-256 manifest and originals covered all original PersistentCity files, isolated root preferences/configuration, plist, install.json and four absent request paths. RLO files were hashed read-only. The helper printed this rollback, shown here with paths replaced by placeholders:

```sh
rm -rf "<isolated-profile>/ModsLocal/PersistentCity" && cp -Rp "<receipt-backups>/20261114T044216Z/mod" "<isolated-profile>/ModsLocal/PersistentCity" && cp "<receipt-backups>/20261114T044216Z/install.json" "<agent-tests-root>/install.json"
```

Restoration used the helper's original `mod/` and unchanged install.json with the game stopped and lease held. The staged folder was moved intact to `<stage>/retained-staged-mod` instead of deleted, then the helper's original mod folder copied into the empty live path and its receipt copied back. This is the non-destructive equivalent of the printed folder-replacement rollback; no receipt was hand-edited. Changed original agent-runtime.json, tracked_commands.json and isolated plist were restored from the independent backup. RLO files were never rewritten.

Verification: **35 original file hashes and 4 absent paths matched; complete PersistentCity file set matched; all 53 RLO hashes matched; zero mismatches**. Preferences, persistent-city.json and original autosave bytes match. install.json SHA-256 `35e1dccafc8fed62ef9406ec7834c6526a290abb07de6a0f5f912d03cc8144dc` matches both backups. AgentRigOps hash unchanged. Final status running=false, process=null, held=false, pending=0. Both owned game PIDs (39439, 45325) and their caffeinate assertions are gone. No Windows PC, Steam app, normal profile or other session's workspace was used.

Validation: candidate build passed; compact JSON parses and is under 20 KB; only this existing checklist and the small JSON are changed. No full unit or regression suite was run for docs-only evidence. The isolated build/evidence worktree is removed after commit, push and opening the one unmerged docs-only PR.

## Rig results — 14 November 2026 a, Mac (GR-5 GC, RLO, P-D 7 at b3d58a6a)

Role `rig_test`, where `mac`, isolated `agentmac`. **Counts: 1 passed / 0 partial / 4 failed / 1 NOT RUN**, counting #252 checks 1–3, #239 checks 2–3 and #244 check 7 once each. #239 check 3 includes a partially successful first attempt and a failed repeat; it is counted as failed. **RESULT: BLOCKED** by the non-FT console tooling gap. In-game proof gates no merge.

All performance numbers are **provisional on the shared Mac**. Commands and all three 600-second windows ran in the foreground. Other sessions were left alone. No Windows PC, Steam app, ordinary profile or ordinary save was used. Production code, schema 1, FoodChoicePriceWeight 1.2 and FoodChoiceHouseholdWeight 1.0 were unchanged.

Placeholders: `<stage>` is this run's local staging folder, `<agent-logs>` the local agent-log folder, `<managed>` the local game Managed assemblies, `<rig-home>` the isolated rig's tooling folder and `<rig-data>` the isolated game profile's data folder. Raw command replies, full logs, backups and extracted fixture data remain in `<stage>`, outside Git. Report copy: `<agent-logs>/persistent-city-rig-2026-11-14a-mac.md`. [Complete day/night GC JSON and compact street evidence](perf/gr5-2026-11-14a-mac.json): **18,423 bytes**, SHA-256 `6ebe2b30eba12e7410ac415f23b341735257fb08b69172e77fe9bb84fd251ccf`. It includes every allocation key and sample count, both human lines, all street spikes, guard evidence and a raw-record filename/hash-manifest pointer.

### Build and rig

| Item | Observation |
|---|---|
| Candidate | `b3d58a6a8c3f53eaa82652203b35e5ccf73b9241` (#252). `origin/codex/persistent-city` was unchanged at the initial and final fetch. |
| Build/evidence checkout | New `codex/mac-rig-2026-11-14a` from that tip, in `rig-2026-11-14a-mac-evidence`. One isolated worktree served build and evidence. Original checkout remained clean. |
| Build | `python3 tools/build.py --game-managed "<managed>"`, exit 0. PATH included `/opt/homebrew/bin`; DOTNET_ROOT was Homebrew dotnet prefix plus `/libexec`. |
| PersistentCity DLL SHA-256 | `c5fa3d98fed91d8a219eb8ba88ebcb2fe377817458def47c498873db622335c3`. Built, packaged and staged DLLs matched; every start validated install.json. |
| Agent-rig HEAD | `f6a2738eec9de76ff2b6e614263a0e528fbf7974`, clean before/after; fetch and ff-only pull were already up to date. No checkout edits, commits or pushes. |
| AgentRigOps SHA-256 | `51b387f84f41508e3a5bd6765dce582982c3ac40cc2810f4a4e69ec55e6e8142`, unchanged at cleanup; live ops ready. |
| RLO Player.log | `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` RLO files were never staged, updated or restored. |
| PC Player.log identity | `[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places.` No commit-stamped PC build line is emitted; package identity is established by the matching live DLL hash and receipt, rather than an invented log stamp. |
| Lease/resources | Initially stopped/free; acquired `2026-11-14T09:09:12.2716300Z` for this run. Startup checks reported 48.4 GiB disk / 61% memory free. No takeover, resource refusal or process cancellation. |
| Starts/stops | Six `python3 scripts/mac-agent start --mode headless --rig-test` launches, each successful on its first attempt. All six stops graceful; no force. |
| Autosave original | Native 5 minutes / 3 saves, prevent_autosave=false. #70 process-only suppression verified after each load and before/after every measured window. Preferences restored byte for byte; suppression ended at stop. |
| Config | SaveCostDiagnostics=false throughout; native save profiler not installed. Physical caps 96/24, radius 120 m. Saved schema remained 1. |

The package contained no RLO files. Staging ran stopped and leased from the official agent-rig checkout:

| Exact staging command | Receipt backup |
|---|---|
| `python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package` | `<rig-home>/receipt-backups/20261114T090950Z` |

### Per-check outcomes

| Check | Result | Observed against expected |
|---|---|---|
| #252 1, day GC | **FAIL** | 11 gen0 meets <=11 but does not move toward 5; pause p95 103.863 ms fails <56.7 ms; Mono heap 1,416,757,248 bytes passes <=1.47 GB. Advance 106,491,904 bytes is 51.4% below 13g's 219,332,608; physical 809,504,768 exceeds both <806 MB and 13g's 805,834,752. |
| #252 2, night GC | **FAIL** | 9 gen0 fails <=8; pause p95 70.122 ms passes <104.6 ms; Mono heap 1,536,327,680 fails <=1.47 GB. Advance 20,717,568 is 77.5% below 13g's 91,938,816; physical 360,747,008 passes <380 MB. |
| #252 3, street | **FAIL** | UpdateCity p95 5.2683 ms fails <5 ms; broader mod-work p95 5.3216 ms. Nine successful pans and three spikes >=30 ms, listed below. |
| #239 2, Living off before load | **PASS** | Set/read off before cold loading the owned food checkpoint. From 12:00–14:00, three new paid PC receipts ($15) match three native visits, three croissants and revenue $110→$125. Status off; checkout active throughout. |
| #239 3, mid-session toggle | **FAIL; first attempt partial** | First attempt detected both changes within 0.443/0.236 s and reported exactly 2 toggles, with checkout active and seven matched receipts ($35). Those sales were not proven to occur after the transitions; 14:00–17:30 added none. A fresh earlier-checkpoint repeat stopped the PC runtime with the exact route-retiming error below and lost active checkout. No full pass is claimed. |
| #244 P-D 7, ordinary save refused | **NOT RUN / tooling gap** | `new_game` accepted the throwaway RIGTMP city, but the bridge refused `console` before `pc gc population 3000` reached the mod. The mod's expected refusal and unchanged-person assertion were not exercised. |

### GC versus 13g

Both 600-second windows used `FT GR5 13f Timing v1`, 1,000 residents / 2,000 visitors / 1,683 runtime places, target off, 20 portals, zero fill refusals, fixed street player x=215,z=0 and speed 1. Day ran Tuesday 15:30–18:25; an untimed speed-20 advance positioned night at Wednesday 01:00–04:10. Mod-work sampling was unarmed for both GC windows. Autosave checks and native union guards show zero allowed saves, zero native-saving frames and clean=true in every measured window. No sample contains a save.

| Window | Stored frames / dropped | gen0/1/2 | Pause p50/p95/max ms | Mono heap bytes | Sampled managed heap peak bytes | Whole-frame p95/max ms |
|---|---|---|---|---|---|---|
| 14a day | 16,834 / 0 | 11/0/0 | 28.007/103.863/103.863 | 1,416,757,248 | 1,416,048,640 | 38.5703/235.8520 |
| 13g day | 16,868 / 0 | 11/0/0 | 25.904/56.671/56.671 | 1,366,654,976 | 1,433,260,032 | 43.2988/484.8029 |
| 14a night | 17,077 / 0 | 9/0/0 | 30.104/70.122/70.122 | 1,536,327,680 | 1,466,814,464 | 37.6928/194.3882 |
| 13g night | 17,048 / 0 | 8/0/0 | 29.953/104.551/104.551 | 1,465,794,560 | 1,689,186,304 | 37.8310/450.2231 |
| 13d night reference, code 83b6156e | 16,960 / 0 | 5/0/0 | 33.526/35.941/35.941 | 1,471,660,032 | 1,471,025,152 | plain-frame p95 38.354 |

13d had 3,222 persons; this run and 13g have 3,000. The previous run is called 13g by its stage/JSON/PR #251 and 14g by its dated checklist heading. Both GC windows report incremental=true, max_generation=0: pause is cycle-ending frame excess, not total incremental collector work. MB/GB comparisons here use decimal bytes.

| Allocation counter | Day bytes | Day samples / skipped | Night bytes | Night samples / skipped |
|---|---:|---|---:|---|
| frame_alloc_bytes | 1,789,841,408 | 16823 | 1,309,499,392 | 17068 |
| mod_alloc_bytes | 978,952,192 | 16832 / 3 | 424,472,576 | 17076 / 2 |
| advance_alloc_bytes | 106,491,904 | 16735 / 0 | 20,717,568 | 16975 / 0 |
| physical_alloc_bytes | 809,504,768 | 16733 / 2 | 360,747,008 | 16974 / 1 |
| physical_ambient_alloc_bytes | 1,134,592 | 16735 / 0 | 798,720 | 16975 / 0 |
| physical_vehicles_alloc_bytes | 12,288 | 16735 / 0 | 0 | 16975 / 0 |
| physical_bindings_alloc_bytes | 4,096 | 16735 / 0 | 8,192 | 16975 / 0 |

For frame allocation the sample column is alloc_frames; no frame-skipped key is exported. These diagnostics sum observed heap growth and exclude segment calls that see a collection.

13g frame/mod/advance/physical bytes: day **1,787,936,768 / 1,091,997,696 / 219,332,608 / 805,834,752**; night **1,241,608,192 / 517,779,456 / 91,938,816 / 379,510,784**. 13g did not export the three finer physical counters. Physical remains the largest measured mod segment in both current windows. Day physical+advance is **915,996,672 bytes**, above #252's about-100-MB task goal. The three finer physical counters total only 1,150,976 day / 806,912 night bytes; they do not account for most of the full physical total. This is segment evidence, not a code root cause.

GC-only UpdateCity maxima >=30 ms: day plain **66.442**, day GC **83.274**; night plain **86.786**, night GC **63.454**. The GC sampler exports maxima, not individual mod-work spike frames/counts; those rows cannot be reconstructed. All full JSON fields and counts are in the linked evidence.

### Street timing and every mod-work spike

Cold-loaded the same checkpoint and settled to Tuesday 15:30 outside timing; street ended 17:38, speed 1, GC sampler off. Initial camera target (215,1,0), camera (215,4,-9). Nine scheduled `camera release` / `look_at` pans between targets x=210 and 220 succeeded over the 600-second wait. These are headless control readbacks, not a visual crowd-density claim.

| Window | Native seconds / frames / achieved fps | UpdateCity p95/max ms | Broader mod-work p95/max ms / spikes >=30 | Whole-frame p95/max ms / spikes >=30 |
|---|---|---|---|---|
| 14a street | 600.246 / 3,493 / 5.82 | 5.2683/221.5981 | 5.3216/221.6495 / 3 | 196.3584/506.9880 / 3,493 |
| 13g street | 600.184 / 3,493 / 5.82 | 5.1413/66.8762 | 5.1897/66.9196 / 2 | 197.0065/541.8071 / 3,493 |

The mod-work recorder covers 601.278 seconds / 3,494 frames; native union covers 600.246 seconds / 3,493 samples. Both bracket the full 600-second wait. Coverage complete at 62 entry points, validity=true, no missing components, zero dropped frames and zero off-main-thread/cross-frame/unbalanced/depth-overflow faults. Whole-frame counts include the rig's 30 fps cap; achieved cadence was 5.82 fps.

| Unity frame | Mod-work ms | Dominant component/group | UpdateCity ms | Whole-frame ms in row | Condition |
|---|---|---|---|---|---|
| 4387 | 221.6495 | updatecity/updatecity | 221.5981 | 453.8571 | hour=15 |
| 4388 | 44.1359 | updatecity/updatecity | 44.0248 | 335.1515 | hour=15 |
| 5114 | 35.4132 | updatecity/updatecity | 35.3696 | 223.7165 | hour=16 |

All three exported spikes are listed; none was chased. The native whole-frame record is separate from mod-work scope time.

Player.log tally on the GR-5 loads: `[Persistent City] unrentable homes housed=0 beds=0 unreadable=0 empty=0`. This reused checkpoint is not a new housing-allocation test.

### RLO 1.0.6 checks 2 and 3

Original `rlo_option` read: stored=null, live=true, original_saved=false. Off-before-load used `op rlo_option {"action":"set","name":"living_population","value":false}`, verified stored=0/live=false, then a cold restart and load of `FT RLO 13d Owned 12`. The eligible owned shop is `FT RLO 13d Food v1`, `ba:street_fourthavenue#19`, with ready=true, staffed register, croissants $5 and normal free paper bags. The player remained outside. Native shop logging was installed only after all performance windows.

| Sales observation | PC new paid receipts / cents | Native visits / paid croissants / free bags | Revenue | Dropped / hook faults |
|---|---|---|---|---|
| Off, 12:00–14:00 | 3 / 1,500 | 3 / 3 / 3 | $110→$125 | 0 / 0 |
| First toggle attempt, 12:00–14:00 | 7 / 3,500 | 7 / 7 / 7 | $110→$145 | 0 / 0 |

Saved receipt keys were compared with pre-interval live keys; every new receipt is paid, 500 cents, croissant, with no extra paid line. The native log has no additional visits/products beyond the matched receipts and free bags. Both intervals suppressed two native draws and two hourly batches. Hooks list all five `CustomerEntriesCalculatorFactory.* [non-Harmony]` RLO replacements as overruled; checkout stayed active in the off interval and first toggle attempt. Extracted saves have schema 1.

Check 3 first cold-loaded the owned checkpoint with Living on. At speed 1 it set off, waited five real seconds, set on, waited five seconds, then read capability status. Exactly one Player.log transition line per change appeared within **0.443 / 0.236 seconds** from each setter call. Status was `Living population on, 2 toggle(s) this session`. The seven sales are positive matching evidence for the interval, but their timing does not establish selling after the transitions. A separate 14:00–17:30 observation added no receipts, revenue or PC purchases (7→7).

To obtain that missing observation, a fresh process loaded the earlier `FT RLO 13d Food v1` at 08:00, advanced at speed 20 to 10:30, confirmed an active coordinator with one committed image, then resumed at speed 1 for the same off/five-seconds/on/five-seconds sequence. The runtime stopped during this repeat, before it could report either transition; by 12:30 FoodDiagnostics.active=false, capability status `start refused (clock.city)`, checkout not checked, purchases=0. Exact error:

```text
System.ArgumentException: Cannot retime a maximum-size native car route without splitting its current edge.
Parameter name: state
```

The retained stack names `PersistentCity.Core.TravelTiming.Apply` then `PopulationRuntime.UpdateCity`; raw log `<stage>/rlo-toggle-early-Player.log`, first stop line 2722. The repeat's resume operation was `call time {"speed":1,"pause":false,"keep_needs":true}`, after the speed-20 preparation. No causal claim about the RLO setter is made. The failed fixture's later checkpoint retains the documented refusal marker and is not a recovery fixture. No production fix or further investigation was attempted.

Restore succeeded through `op rlo_option {"action":"restore","name":"living_population"}`; independent readback returned **stored=null, live=true, original_saved=false**, matching the original. No RLO file was edited.

### P-D check 7 and exact tooling gaps

`python3 scripts/mac-agent call new_game '{"name":"RIGTMP-PD-GC-Refusal-14a","throwaway":true}'` succeeded under a fresh `--rig-test` start; load.status reached done and population activation occurred. The required call was then attempted exactly:

```sh
python3 scripts/mac-agent call console '{"line":"pc gc population 3000"}'
```

Exact bridge refusal:

```text
console changes the save, and the loaded save 'RIGTMP-PD-GC-Refusal-14a' doesn't start with FT: load an FT save first.
```

The same guard first refused the optional `pc gc start` person-count setup read. The exact required population command was still attempted and recorded afterward. The mod never received it, so neither its `refused: measurement mode needs a disposable FT city` message nor before/after person equality is verified. No normal save or guard bypass was used. Raw replies: `pd-new-game.json`, `pd-population-command.json`, `pd-check.txt`, `pd-summary.json` in the stage.

Other readback/tooling limitations: no commit-stamped PC build line; no individual GC-only mod-work spike frames. The unused legacy `call console {"line":"pc inspect"}` in the reused preparation script returned `Warning: ERROR: 'pc' doesn't take 1 parameter(s). Available command(s):`; supported population, census, movement and pcstate reads supplied the required setup evidence. Two local result-inspection reads raised `KeyError: 'preferences'` and `FileNotFoundError` for the not-yet-written `gc-night-perf-end.json`; neither was a game operation failure, and both required records were retained before analysis.

### NOT RUN grouped

- **Counted: P-D #244 check 7**, because the bridge blocks console input in the successfully created throwaway non-FT city.
- **Outside the six-check count: save latency**, explicitly deferred pending the owner's route decision. No latency sample or save profiler was run; RLO receipt checkpoints were outside all performance windows.
- Remaining readback limits are listed above; unrelated 60 fps, building-footprint and GR-9 rows were not added to this run's count.

### Findings, fixtures and cleanup

At the 1,000-resident cap, advance allocation is lower in both observed GC windows, while physical remains the largest measured mod segment. The day pause/physical limits, night collection/heap limits and street p95 limit do not pass. These data do not establish the cap against #252's performance expectations. Save latency remains unmeasured. Living-off checkout is verified by rig; mid-session detection works in the first attempt but the full continuing-sales check remains failed after the repeat's runtime stop. No lower cap or code root cause is inferred.

Reused: `FT GR5 13f Timing v1`, `FT RLO 13d Owned 12`, `FT RLO 13d Food v1`. Created checkpoints: `FT RLO 14a Off 14`, `FT RLO 14a Toggle 14`, `FT RLO 14a Toggle 1730`, `FT RLO 14a Toggle Early 1230` (stopped-runtime refusal marker). Created throwaway city: `RIGTMP-PD-GC-Refusal-14a`; it was not explicitly saved, and no undocumented removal was attempted. Disposable fixture saves stay in the isolated profile; existing checkpoints were not overwritten.

An independent pre-stage manifest backed up the full PersistentCity folder, isolated root preferences/config files, plist, install.json and four absent request paths. The helper printed this rollback verbatim, retained in `<stage>/stage.txt`:

```sh
rm -rf "<rig-data>/ModsLocal/PersistentCity" && cp -Rp "<rig-home>/receipt-backups/20261114T090950Z/mod" "<rig-data>/ModsLocal/PersistentCity" && cp "<rig-home>/receipt-backups/20261114T090950Z/install.json" "<rig-home>/install.json"
```

The execution layer rejected it before execution: `rm -f style commands are not permitted. Use a safer approach`. With the game stopped and lease held, the non-destructive equivalent moved the staged folder intact to `<stage>/retained-staged-mod`, copied the helper's original `mod/` into the empty live path, then copied its unchanged receipt back. Changed original profile files and the plist were restored from the independent backup. RLO mod files were checked read-only, never restored.

Verification: **35 original file hashes and 4 absent paths matched; full PersistentCity file set matched; all 53 RLO hashes matched; zero mismatches**. Preferences, persistent-city.json and original autosave bytes match. install.json SHA-256 **`94b9b916dd66398dd1728ef3976256d1897751f0dffb0e9af6d4fe2a7bb1bb0d`** matches independent and helper backups; AgentRigOps hash is unchanged. Final status running=false, process=null, lease held=false, pending=0. All six owned game PIDs (91977, 97422, 98252, 98597, 99212, 99546) and their caffeinate assertions are gone.

Validation: candidate build passed; compact JSON parses and is under 20 KB; docs links, markdown ban and diff whitespace checks run for this docs-only result. No full unit or regression suite. Only the existing checklist and compact JSON are changed. The isolated build/evidence worktree is removed after commit, push and opening the one unmerged docs-only PR. No issue, release, Workshop upload, version/changelog change or merge.

## Rig results — 14 November 2026 b, Mac (GR-5 GC #254, footprint, 60 fps at eb38a061)

Role `rig_test`, where `mac`, isolated `agentmac`. **Counts: 0 passed / 0 partial / 3 failed / 7 NOT RUN. RESULT: BLOCKED.** Counts cover #254 checks 1–4, #246 footprint and the two requested 60 fps arms, plus the three owner-excluded checks. The two 600-second windowed diagnostics are recorded below; neither establishes the requested 60 fps arm. In-game proof gates no merge.

All performance numbers are **provisional on the shared Mac**. Every command and all five 600-second windows ran in the foreground. Other sessions were left alone. No Windows PC, Steam app, ordinary profile, normal bridge instance or ordinary save was used. No production code, schema, food weights, release, version/changelog or Workshop change was made.

`<stage>` is the rig stage folder `persistent-city-rig-2026-11-14b-mac`; `<app-support>` is the rig user's `Library/Application Support`; `<agent-logs>` is the agent report folder. Raw evidence: `<stage>`. [Complete GC reports and compact frame evidence](perf/gr5-2026-11-14b-mac.json): **16,572 bytes**, SHA-256 `6442f6a2b6c21fd5965fa3597cc1821cf6df37fadbca92742b5ec4c46bd4707f`. Both full GC JSON objects retain every allocation key and sample/skipped count. The raw filename/hash manifest is `raw-evidence-manifest.json` there (SHA-256 `44cddecdb4b5b4d8dc587d03e7a1388b36ee53214539620cd8096f0a1c21d60d`). Logs, requests, two body screenshots, obstructed captures, backups and scripts remain outside Git. Report copy: `<agent-logs>/persistent-city-rig-2026-11-14b-mac.md`.

### Build and rig

| Item | Observation |
|---|---|
| Tested candidate | `eb38a061200ff2469d4bc5a568c0046144494686` (#254), exactly the remote tip at initial fetch. Final fetch moved to `1d5d1b765218974d41e848926f17137609bf4a58` (#255); that later code was not built or tested here. |
| Branch/worktree | New `codex/mac-rig-2026-11-14b`, based on the tested tip, in `rig-2026-11-14b-mac-evidence`. One isolated worktree served build and evidence; original checkout stayed clean. |
| Build | `python3 tools/build.py --game-managed "<app-support>/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, passed; Homebrew PATH and DOTNET_ROOT used. |
| PersistentCity DLL SHA-256 | `296f4d44ed7f0ac61e79bd99e883b66485ffb011f1b61e65f3fc736640f83f6c`; built/package/staged hashes match, and each start validated the receipt. |
| Agent-rig HEAD | `2b8553606426f3e3e94f8a876dd58d935768732d`, clean before and after; fetch and ff-only pull were already current. No edits, commits or pushes to that checkout. |
| AgentRigOps SHA-256 | `dd11e3fdea78c1c0a25ce33e0168ffbce6e785950d9e10caed55015254d748f8`, unchanged at cleanup; ops ready in the loaded city. |
| RLO Player.log | `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` No RLO files were staged or written. |
| PC Player.log | `[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places.` No commit-stamped build line is emitted; binary identity is established by the matching DLL hashes and validated receipt. Shop fixture has 1,690 runtime places. |
| Lease/resources | Initially stopped/free; acquired `2026-11-14T11:56:58.6792540Z`. Initial resources: 51.5 GiB disk / 79% memory free by `mac-agent resources`; no resource override or refusal. |
| Starts/stops | Six starts, each successful on its first attempt: three headless, three windowed. All stops graceful; no force. Wrong-population and wrong-shop fixture setups were stopped before sampling. |
| Autosave original | 5 minutes / 3 saves, prevent_autosave=false. #70 suppression checked after every load and before/after every measured window. All five guards report clean=true, zero allowed saves and zero native-saving frames. |
| Config/options | SaveCostDiagnostics=false; native save profiler not installed. Schema 1, FoodChoicePriceWeight 1.2 and FoodChoiceHouseholdWeight 1.0. `living_population` unchanged: stored=null, live=true, original_saved=false before/after. |

Staged once, stopped and leased, from the official agent-rig checkout. Package contains no RLO files.

| Exact staging command | Receipt backup |
|---|---|
| `python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package` | `<app-support>/BigAmbitionsAgentTestsMac/receipt-backups/20261114T115729Z` |

### Per-check outcomes

| Check | Result | Observed against expected |
|---|---|---|
| #254 1, day GC | **FAIL** | boehm_total accepted; physical+advance **1,021.6 MB** fails well-under-about-100-MB target. 11 gen0 versus 14a 11; pause p95 **28.191 ms** versus 103.863 ms. Physical is largest; appearance is its largest child (745.4 MB). |
| #254 2, night GC | **FAIL** | boehm_total accepted; physical+advance **433.6 MB** fails the same target. 8 gen0 versus 14a 9; pause p95 **61.316 ms** versus 70.122 ms. Physical is largest; appearance is its largest child (327.8 MB). |
| #254 3, street | **FAIL** | UpdateCity p95 **5.5170 ms**, broader mod-work **5.5824 ms**, both above <5 ms. Four spikes >=30 ms, all listed below. |
| #254 4, appearance release/reacquire | **NOT RUN to required proof / tooling gap** | Windowed start and two independent body images captured. Original walker handle disappears; return walk ends short; no stable PC resident identity or successful reacquisition established. No matching appearance comparison or reuse-count rise is claimed. |
| #246 4, footprint | **NOT RUN / tooling gap** | `op building {"action":"list"}` returns `ops_game_api_unavailable`. No three targets, Home-area comparisons, rotated/yard coverage or visible storey comparison obtained. |
| #246 1, street 60 fps arm | **NOT RUN at required cadence** | Cap 60 applied, vSync=0; seven pre-window readings 4.96–5.71 fps fail 51–66. A separate full 600-second diagnostic ran at 4.74 fps. |
| #246 1, busy-shop 60 fps arm | **NOT RUN at required cadence/setup** | Cap 60 applied; readings 5.00–44.15 fps fail 51–66. Separate 600-second diagnostic at 4.98 fps used the known 13e food fixture; ready=true but no observed sales. It is not the same saved city as the street arm. |
| #239 3, toggle recheck | **NOT RUN / owner-excluded** | #255 was unmerged when this run was launched and is absent from the tested DLL. It merged during the run; a later run covers it. |
| #244 P-D 7 | **NOT RUN / owner-excluded** | Run request defers this until agent-rig #85 console-guard review is complete. No throwaway city or console refusal test attempted here. |
| GR-5 save latency | **NOT RUN / owner-excluded** | Owner decision pending. No latency sample, save profiler or manual save was run. |

### GC against 14a

Day: Tuesday 15:30; night: Wednesday 01:00. Both use `FT GR5 13f Timing v1`, 1,000 residents / 2,000 visitors, target off, street x=215,z=0, speed 1, mod-work sampler unarmed. Untimed speed-20 positioning was limited to required clock setup. No TravelTiming.Apply stop occurred. 14a measured a different allocation counter; raw byte totals below are context, not a like-for-like improvement claim. `boehm_total` is cumulative/process-wide; 14a `heap_used` measures used heap. Legacy frame allocation remains separately labelled heap_used.

| Window | Stored / dropped | gen0/1/2 | Pause p50 / p95 / max ms | Mono heap bytes | Sampled managed heap peak bytes | Native whole-frame p95 / max ms |
|---|---|---|---|---|---|---|
| 14b day | 16781 / 0 | 11/0/0 | 26.459/28.191/28.191 | 1535655936 | 1535467520 | 43.9235/494.3215 |
| 14b night | 16965 / 0 | 8/0/0 | 30.116/61.316/61.316 | 1578450944 | 1510080512 | 38.0592/897.9312 |
| 14a day | 16834 / 0 | 11/0/0 | 28.007/103.863/103.863 | 1416757248 | 1416048640 | 38.5703/235.8520 |
| 14a night | 17077 / 0 | 9/0/0 | 30.104/70.122/70.122 | 1536327680 | 1466814464 | 37.6928/194.3882 |

The original #246/13d reference is 5 gen0, pause p95 35.9 ms and about 1.47 GB Mono heap. Both current collection counts remain above that reference; both Mono heap readings exceed 1.47 GB. GC is incremental, max_generation=0; pause is cycle-ending frame excess, not all incremental collector work. MB/GB here are decimal bytes.

| Allocation bytes | 14b day | 14b night | 14a day / night (heap_used) |
|---|---|---|---|
| Probed whole-frame counter | 2462840480 | 1909833056 | unavailable |
| Legacy frame heap-used counter | 1830817792 | 1328979968 | 1789841408 / 1309499392 |
| Mod intervals | 1142457656 | 557799024 | 978952192 / 424472576 |
| Advance | 236447944 | 72745504 | 106491904 / 20717568 |
| Physical | 785139168 | 360896416 | 809504768 / 360747008 |

Every physical child below includes bytes / samples / skipped. Samples count charged spans, not frames. All 18 child rows sum exactly to physical in both windows; unattributed is zero.

| Physical child | Day bytes / samples / skipped | Night bytes / samples / skipped |
|---|---|---|
| ambient | 3207280 / 16689 / 0 | 3242800 / 16874 / 0 |
| vehicles | 1137328 / 16689 / 0 | 399552 / 16874 / 0 |
| bindings | 23808 / 605584 / 0 | 19376 / 255918 / 0 |
| vendor_meals | 0 / 16689 / 0 | 0 / 16874 / 0 |
| fade | 0 / 16689 / 0 | 0 / 16874 / 0 |
| focus | 0 / 16689 / 0 | 0 / 16874 / 0 |
| observe | 286560 / 588494 / 0 | 103968 / 238834 / 0 |
| release | 6720 / 401 / 0 | 14000 / 210 / 0 |
| parked | 1961776 / 583 / 0 | 1710688 / 586 / 0 |
| index | 80 / 2199 / 0 | 80 / 2191 / 0 |
| queues | 8960 / 2199 / 0 | 17200 / 2191 / 0 |
| roster | 292768 / 2199 / 0 | 194848 / 2191 / 0 |
| selection | 1261480 / 2702 / 0 | 977352 / 2428 / 0 |
| spawn_pedestrian | 29832816 / 858 / 0 | 25655416 / 377 / 0 |
| spawn_appearance | 745426168 / 1281 / 0 | 327776384 / 561 / 0 |
| spawn_check | 1518400 / 854 / 0 | 665088 / 374 / 0 |
| spawn_vehicle | 175024 / 72 / 0 | 119664 / 47 / 0 |
| unattributed | 0 / 33378 / 0 | 0 / 33748 / 0 |

Appearance contributes 94.94% day / 90.82% night of physical allocation. The appearance-check children are 1,518,400 / 665,088 bytes. counter_frames=16,781 / 16,965. Largest measured parent: physical; largest child: spawn_appearance. No code cause or lower-cap recommendation is inferred.

| Window | Last logged pedestrianLooks |
|---|---|
| gc-day | `pedestrianLooks=398 built/4 reused` |
| gc-night | `pedestrianLooks=188 built/1 reused` |
| street | `pedestrianLooks=155 built/2 reused` |
| street60 | `pedestrianLooks=142 built/2 reused` |
| shop60 | `pedestrianLooks=146 built/4 reused` |

### Frame windows and every mod-work spike >=30 ms

Main street: cold load of the GC fixture, Tuesday 15:30, speed 1, nine successful pans. Windowed street diagnostic began 15:33 after the appearance attempt, same fixture. Shop diagnostic cold-loaded `FT GR5 13e Timing v1`, 1,000 residents / 2,000 visitors, Tuesday 15:30, outside the shop entrance x=176.142853,z=-36.35211. All GC samplers were off for these windows.

| Window | Native seconds / frames / achieved fps | UpdateCity p95 / max ms | Broader mod-work p95 / max ms / spikes >=30 | Whole-frame p95 / max ms / spikes >=30 |
|---|---|---|---|---|
| street | 600.284 / 3370 / 5.61 | 5.517/78.1533 | 5.5824/78.2031/4 | 238.9091/1002.2687/3370 |
| street60 | 600.54 / 2848 / 4.74 | 11.3598/154.6731 | 11.6335/183.0037/52 | 312.994/1737.3838/2848 |
| shop60 | 600.365 / 2988 / 4.98 | 11.1759/191.5129 | 11.4017/191.6901/21 | 304.8277/850.0762/2988 |
| 14a street | 600.246 / 3493 / 5.82 | 5.2683/221.5981 | 5.3216/221.6495/3 | 196.3584/506.9880/3493 |

All three mod-work records have complete coverage, valid=true and zero dropped/off-main-thread/cross-frame/unbalanced/depth-overflow faults. Every exported spike is listed: 4 main street, 52 windowed street diagnostic, 21 shop diagnostic (77 total). None was chased. Native whole-frame timings stay separate from the mod-work union.

| Window / Unity frame | Mod-work ms | Dominant component/group | UpdateCity ms | Whole-frame ms in row | Condition |
|---|---|---|---|---|---|
| street / 3961 | 78.2031 | updatecity/updatecity | 78.1533 | 325.0361 | hour=15 |
| street / 4686 | 38.7129 | updatecity/updatecity | 38.667 | 191.5107 | hour=16 |
| street / 6278 | 40.6406 | updatecity/updatecity | 40.4722 | 316.4531 | hour=17 |
| street / 6730 | 38.2282 | updatecity/updatecity | 38.1749 | 189.0002 | hour=17 |
| street60 / 5274 | 36.9974 | updatecity/updatecity | 36.9423 | 192.1885 | hour=16 |
| street60 / 5521 | 39.3364 | updatecity/updatecity | 39.2721 | 205.0291 | hour=16 |
| street60 / 5541 | 60.0369 | updatecity/updatecity | 59.829 | 574.4246 | hour=16 |
| street60 / 5542 | 34.3638 | updatecity/updatecity | 33.6831 | 434.9791 | hour=16 |
| street60 / 5563 | 65.3679 | updatecity/updatecity | 64.5883 | 851.0843 | hour=16 |
| street60 / 5565 | 33.9637 | updatecity/updatecity | 33.8666 | 752.1063 | hour=16 |
| street60 / 5569 | 43.1151 | updatecity/updatecity | 42.6499 | 822.3757 | hour=16 |
| street60 / 5601 | 64.8532 | updatecity/updatecity | 63.3008 | 815.4749 | hour=16 |
| street60 / 5603 | 30.3043 | updatecity/updatecity | 29.077 | 1725.5583 | hour=16 |
| street60 / 5604 | 65.2218 | updatecity/updatecity | 63.9547 | 1385.5413 | hour=16 |
| street60 / 5605 | 100.7534 | updatecity/updatecity | 99.795 | 1539.5576 | hour=16 |
| street60 / 5606 | 100.0533 | updatecity/updatecity | 89.7965 | 1613.6252 | hour=16 |
| street60 / 5607 | 92.9644 | updatecity/updatecity | 89.068 | 1123.5389 | hour=16 |
| street60 / 5609 | 64.0901 | updatecity/updatecity | 63.2534 | 1683.8818 | hour=16 |
| street60 / 5610 | 116.6991 | updatecity/updatecity | 114.6702 | 1184.2536 | hour=16 |
| street60 / 5611 | 32.582 | updatecity/updatecity | 32.1146 | 375.535 | hour=16 |
| street60 / 5617 | 33.7542 | updatecity/updatecity | 32.2659 | 873.402 | hour=16 |
| street60 / 5618 | 50.568 | updatecity/updatecity | 49.3611 | 1384.8985 | hour=16 |
| street60 / 5619 | 40.2232 | updatecity/updatecity | 37.5156 | 1323.0545 | hour=16 |
| street60 / 5620 | 183.0037 | updatecity/updatecity | 154.6731 | 1364.8021 | hour=16 |
| street60 / 5625 | 63.7154 | updatecity/updatecity | 61.8779 | 1078.0434 | hour=16 |
| street60 / 5626 | 39.746 | updatecity/updatecity | 38.6603 | 1119.0615 | hour=16 |
| street60 / 5643 | 40.3283 | updatecity/updatecity | 39.6341 | 821.0729 | hour=16 |
| street60 / 5648 | 97.6177 | updatecity/updatecity | 97.513 | 316.1594 | hour=16 |
| street60 / 5701 | 56.3378 | updatecity/updatecity | 56.2591 | 255.4415 | hour=16 |
| street60 / 5705 | 48.8614 | updatecity/updatecity | 48.7502 | 265.6071 | hour=16 |
| street60 / 5717 | 62.8242 | updatecity/updatecity | 61.466 | 953.6815 | hour=16 |
| street60 / 5720 | 37.7738 | updatecity/updatecity | 36.4564 | 692.257 | hour=16 |
| street60 / 5725 | 42.9559 | updatecity/updatecity | 42.8722 | 370.1785 | hour=16 |
| street60 / 5753 | 37.0138 | updatecity/updatecity | 36.4056 | 949.689 | hour=16 |
| street60 / 5756 | 35.3189 | updatecity/updatecity | 34.2657 | 756.6054 | hour=16 |
| street60 / 5774 | 35.142 | updatecity/updatecity | 34.9927 | 295.7526 | hour=16 |
| street60 / 5780 | 31.0095 | updatecity/updatecity | 30.8932 | 208.3352 | hour=16 |
| street60 / 5782 | 111.1083 | updatecity/updatecity | 110.9008 | 348.6953 | hour=16 |
| street60 / 5783 | 99.7644 | updatecity/updatecity | 99.1704 | 890.4571 | hour=16 |
| street60 / 5787 | 69.0672 | updatecity/updatecity | 68.9753 | 239.5268 | hour=16 |
| street60 / 5790 | 61.4323 | updatecity/updatecity | 61.2462 | 237.5178 | hour=16 |
| street60 / 5793 | 82.4707 | updatecity/updatecity | 82.4019 | 264.0663 | hour=16 |
| street60 / 5801 | 89.9307 | updatecity/updatecity | 89.5512 | 378.6045 | hour=16 |
| street60 / 5807 | 31.9207 | updatecity/updatecity | 31.7754 | 346.0769 | hour=16 |
| street60 / 5817 | 45.0353 | updatecity/updatecity | 44.9328 | 235.4635 | hour=16 |
| street60 / 5820 | 36.4515 | updatecity/updatecity | 36.3678 | 231.797 | hour=16 |
| street60 / 6197 | 35.9882 | updatecity/updatecity | 35.9403 | 200.8827 | hour=16 |
| street60 / 6655 | 49.3145 | updatecity/updatecity | 49.2487 | 280.1985 | hour=16 |
| street60 / 6857 | 56.55 | updatecity/updatecity | 56.4291 | 385.8042 | hour=17 |
| street60 / 6988 | 50.7617 | updatecity/updatecity | 50.537 | 482.1713 | hour=17 |
| street60 / 7034 | 33.1742 | updatecity/updatecity | 33.0605 | 220.7119 | hour=17 |
| street60 / 7039 | 34.038 | updatecity/updatecity | 33.9771 | 232.95 | hour=17 |
| street60 / 7165 | 38.9581 | updatecity/updatecity | 38.7875 | 351.0833 | hour=17 |
| street60 / 7196 | 40.1609 | updatecity/updatecity | 40.1122 | 234.5326 | hour=17 |
| street60 / 7201 | 53.9309 | updatecity/updatecity | 53.8344 | 272.0931 | hour=17 |
| street60 / 7237 | 36.3964 | updatecity/updatecity | 36.2329 | 850.1287 | hour=17 |
| shop60 / 2665 | 191.6901 | updatecity/updatecity | 191.5129 | 562.5442 | hour=15 |
| shop60 / 2666 | 50.3743 | updatecity/updatecity | 50.2249 | 245.71 | hour=15 |
| shop60 / 2694 | 42.1562 | updatecity/updatecity | 42.0404 | 316.4843 | hour=15 |
| shop60 / 2699 | 48.7071 | updatecity/updatecity | 48.6165 | 207.8582 | hour=15 |
| shop60 / 3518 | 72.7335 | updatecity/updatecity | 72.6502 | 239.0418 | hour=16 |
| shop60 / 3642 | 32.2273 | updatecity/updatecity | 31.6131 | 840.353 | hour=16 |
| shop60 / 3646 | 33.5739 | updatecity/updatecity | 32.9509 | 664.2844 | hour=16 |
| shop60 / 3649 | 34.426 | updatecity/updatecity | 33.9819 | 400.6688 | hour=16 |
| shop60 / 3669 | 31.2136 | updatecity/updatecity | 23.8037 | 376.2538 | hour=16 |
| shop60 / 3854 | 36.3836 | updatecity/updatecity | 36.1673 | 224.0276 | hour=16 |
| shop60 / 3869 | 54.7504 | updatecity/updatecity | 54.6565 | 232.5811 | hour=16 |
| shop60 / 3897 | 31.2478 | updatecity/updatecity | 31.1166 | 219.9265 | hour=16 |
| shop60 / 4126 | 42.6629 | updatecity/updatecity | 42.5006 | 249.0814 | hour=16 |
| shop60 / 4143 | 63.7596 | updatecity/updatecity | 63.6726 | 252.4926 | hour=16 |
| shop60 / 4147 | 44.0425 | updatecity/updatecity | 43.9212 | 263.8383 | hour=16 |
| shop60 / 4178 | 31.8277 | updatecity/updatecity | 31.4836 | 551.9618 | hour=16 |
| shop60 / 4345 | 34.1677 | updatecity/updatecity | 33.8036 | 303.2314 | hour=16 |
| shop60 / 4377 | 44.6788 | updatecity/updatecity | 44.5777 | 275.8483 | hour=16 |
| shop60 / 4481 | 51.5786 | updatecity/updatecity | 51.4705 | 254.3372 | hour=16 |
| shop60 / 4531 | 30.4385 | updatecity/updatecity | 30.2818 | 352.38 | hour=16 |
| shop60 / 4734 | 46.5659 | updatecity/updatecity | 46.5059 | 209.6307 | hour=16 |

GC-only UpdateCity maxima (not individual spike exports): gc-day plain 72.146 ms / GC 33.253 ms; gc-night plain 81.497 ms / GC 59.904 ms; individual frame/count rows are not available from that sampler.

All five native windows are clean: no allowed saves and no native-saving frames. Autosave was checked before and after each. No save latency was measured.

### Appearance and exact tooling gaps

The windowed client started with `agent-frame-cap.json` containing `{"fps":60,"vsync":0}`. The first combined captures show a Camera Tools update overlay and native panels. Native panels were closed through supported UI button ops. Virtual input did not dismiss the update overlay. Desktop selection of the private app failed; no other app or desktop setting was changed.

| Operation | Exact result / consequence |
|---|---|
| `op building {"action":"list"}` | `ops_game_api_unavailable`; retained full failed reply in `footprint-error.json`. A read-only repeat returned the same error. No building data was guessed. |
| `cua.getApp("<app-support>/BigAmbitionsAgentTestsMac/Big Ambitions Agent Tests Mac.app")` | `Computer Use server error -10005: cgWindowNotFound` |
| `call ui {"op":"escape"}` | `Nothing is open to close (Esc would open the pause menu).` Native panels were subsequently closed through their listed buttons. |
| `call walk {"x":215,"z":0}` (return, op 29) | `Operation 29 is still pending: use tools/ft op 29; do not repeat the command`. Polled the existing operation, without replay. Final status done, arrived=false, x=230.57,z=-10.04, from_goal=18.53 m, waited_for_traffic_s=30. |
| Street cap verification | applied=true, target_frame_rate=60, vsync_count=0, error=null; measured 5.58, 5.71, 5.32, 5.71, 4.96, 5.58, 5.54 fps. Required 51–66 not reached. |
| Shop cap verification | applied=true, target_frame_rate=60, vsync_count=0, error=null; measured 44.15, 5.00, 5.45, 5.95, 5.32, 5.82, 5.37 fps. Required 51–66 not reached. |

Building-op documentation says storeys=null; the requested substitution was bounds.height_m, compared with PC’s 3.5 m policy. The failed list prevented that comparison, including rotated and yard/sidewalk targets. No footprint/storey conclusion is claimed.

Appearance outward `call walk {"x":305,"z":0}` completed a 100 m pavement route in about 30.3 seconds, ending x=304.09,z=-0.30. The original opaque walker handle was absent after the trip. Rig walker handles do not guarantee a PC resident identity across pool reuse; the required resident identity/reacquisition was not established. Last appearance-attempt counters include 12 built/0 reused before, 4 built/0 reused after; no reuse increase is proven. The attempt log has zero matches for `Appearance meshes unavailable` and `Empty appearance`, and no TravelTiming.Apply stop.

Two independent, visually inspected 1600x900 images are retained outside Git:

| Image | Observation | SHA-256 |
|---|---|---|
| `appearance-body-before.png` | Candidate body in purple shirt/burgundy shorts, short grey hair and red/white shoes. | `95fe425be4db400add52a6284b8b1cfdb9146d2632c2f37f349b3cb627bc4721` |
| `appearance-body-after.png` | Same camera position; empty sidewalk. No matching resident available for comparison. | `c2cebcaf6d58e5869f9f59aee606e9528ac650cbf23e3e0eeeb481c0a9d06659` |

The shop diagnostic used ready=true `FT GR5 13e Food`, `ba:street_fourthavenue#21`, one staffed register, croissants $5. Revenue remained $180 and stock remained 64 croissants / 265 bags, so no busy-sales proof is claimed. The same address in the 14a fixture is native gym `Pump Up`, rented=false; that setup was discarded before measurement. No fixture was created or saved by this run.

### Findings, fixtures and NOT RUN groups

At 1,000 residents, physical+advance remains above #254’s about-100-MB target in both windows, with appearance the largest measured child. Main street UpdateCity and broader mod-work p95 exceed 5 ms. Day pause p95 is lower than 14a, night has one fewer collection, but these observations do not establish the cap against the requested GC/allocation and street gates. Save latency remains unmeasured. No code root cause, alternative cap or release decision is inferred.

**Required proof blocked (four NOT RUN arms):** #254 appearance identity/reacquire comparison; #246 three-building footprint; actual 60 fps street; actual 60 fps busy shop. Captures and low-cadence diagnostics above are retained as observations, not passes.

**Owner-excluded (three NOT RUN checks):** #239 check 3 (later run with #255); #244 P-D check 7 (agent-rig #85 console guard); GR-5 save latency (owner decision pending).

Fixtures: requested `FT RLO 13d Food v1` existed but loaded 1,611 residents / 1,611 visitors, so it could not supply the required cap. GC/main street/appearance/windowed street used the 14a fallback `FT GR5 13f Timing v1` (1,000/2,000; 1,683 runtime places). Shop diagnostic used `FT GR5 13e Timing v1` (1,000/2,000; 1,690 places). Existing saves were never overwritten; no new checkpoint or RIGTMP city was made. The load tally is `[Persistent City] unrentable homes housed=0 beds=0 unreadable=0 empty=0`; reused saves are not a fresh housing-allocation proof.

### Restoration and validation

An independent pre-stage manifest backed up the original PersistentCity folder, isolated root preference/config files, plist and install receipt, and recorded four absent request/config paths. RLO hashes and its original living_population values were recorded without changing them. The helper printed this rollback verbatim:

```sh
rm -rf "<app-support>/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "<app-support>/BigAmbitionsAgentTestsMac/receipt-backups/20261114T115729Z/mod" "<app-support>/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "<app-support>/BigAmbitionsAgentTestsMac/receipt-backups/20261114T115729Z/install.json" "<app-support>/BigAmbitionsAgentTestsMac/install.json"
```

With the game stopped and lease held, the non-destructive equivalent moved the staged mod intact to the raw evidence folder, copied the helper’s original mod folder into the empty path, restored the unchanged receipt, then restored independent preference/config bytes and original frame-cap absence. RLO files were checked read-only.

Verification: **35 original file hashes, 4 absent paths, full PersistentCity file set and all 53 RLO hashes match; zero mismatches**. install.json SHA-256 `2381eebf577366ad720a6a18fcf82a2e8c9b72a1854605ebefdb67f37a413ace`; AgentRigOps SHA-256 unchanged. Original preferences, persistent-city.json, autosave bytes and frame-cap absence match. Process-only autosave suppression ended at stop.

Final status: running=false, process=null, lease held=false, pending=0. All six owned PIDs (28591, 29551, 38926, 40985, 48859, 49413) and their caffeinate assertions are gone. All stops were graceful. No process belonging to another session was stopped.

Validation for this docs-only result: candidate build passed; compact JSON parses and is under 20 KB; docs-links, markdown ban and diff whitespace checks are run before commit. No full unit or regression suite. Only this existing checklist and the compact JSON are changed. The build/evidence worktree is removed after commit, push and opening the one unmerged docs-only PR. No issue or merge is created.

## Rig results — 14 November 2026 d, PC (GR-5 60 fps arms at 913abd7d)

Role `rig_test`, where `pc`, isolated `agent1`. **Counts: 0 passed / 0 partial / 0 failed / 9 NOT RUN. RESULT: BLOCKED.** Counts cover the two requested 60 fps arms and seven excluded rows below; the launch tooling failure is not a measured performance failure. The pre-fix baseline was built and staged, but the required start command was refused before a game process launched. No 600-second window ran. In-game proof gates no merge.

### Build and rig

| Item | Evidence |
|---|---|
| Candidate | `913abd7dbfdcd7f7651baad768169f66bf698dfb`, initial and final `origin/codex/persistent-city`; unchanged. The unmerged spawn-appearance implementation was not used. |
| Worktrees | Results branch `codex/pc-rig-2026-11-14d`, evidence worktree `rig-2026-11-14d-pc-evidence`; detached build worktree `rig-2026-11-14d-pc`. Original checkout remained clean. |
| Official agent-rig | `4bcf80aa04431f3b54cac19335c9c4cbb57a4b48`; clean, fetched, `git pull --ff-only` reported already up to date. No checkout edits, build or overlay installation by this run. |
| Live AgentRigOps | `C:\BigAmbitions-AgentTests\Big Ambitions Agent Tests_Data\Managed\AgentRigOps.dll`, SHA-256 `fe4c40c1ba70512eb0b14704e275c1fa4842e98100ba563cc2d559a13be6bf6a` before acquisition and after cleanup. Matches the existing local overlay DLL/manifest and installed receipt. No binary difference observed; a source-commit stamp is not in that manifest. |
| Lease | Owner `pc-rig-2026-11-14d`. Initially held by an overlay-update role; no takeover. Acquired after status became free at 14:02:53 UTC; released after verified restoration. |
| Build | `python tools/build.py --game-managed "C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"`, exit 0. PATH prefixed with `%LOCALAPPDATA%\Microsoft\dotnet`; requested wait helper ran first. No offline tests. |
| PersistentCity DLL | Built, packaged and staged SHA-256 `087fe9aa8c506c3d2fa43df62fec0f08450095ca415f7c3ffebf64bfd28bde29`. Package contains no RLO or game DLLs. No startup build line was available because launch was refused. |
| Package / raw evidence | `<stage>` = `%USERPROFILE%\HART-Builds\persistent-city-rig-2026-11-14d-pc`; package at `<stage>\package\PersistentCity`. Full backups, build/start output, machine inventory, restore checks and SHA-256 manifest remain outside Git. |
| Staging command | Python `shutil.copytree(package, profile / 'ModsLocal/PersistentCity', dirs_exist_ok=True)`, then compare SHA-256 for every package file. This is the authorized copy fallback; no receipt hand-edit. |
| Backup / rollback source | `<stage>\pre-stage-backup`. Entire original mod folder, receipt/run files, profile configuration and complete isolated registry export, with original bytes or absence in `manifest.json`. Restoration used this run's backup only. |
| Frame-cap file | `%USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests\agent-frame-cap.json`; staged exactly `{"fps":60,"vsync":0}` (20 bytes, no newline). Original absence restored. |
| Autosave / configuration | Original runtime autosave setting unavailable without a running player; complete preferences/configuration preserved and hash-verified. No setting changed or measured window started. Candidate package has SaveCostDiagnostics=false. No claim of live suppression. |
| RLO / Food Trucks | No current-run version log: no player launched. Owner-provided Food Trucks 0.10.2 was accepted as a run input, not independently verified. Neither mod was installed, updated or edited. |
| Machine load | Post-cleanup preparation inventory at 14:04:53 UTC: CPU 51%; used RAM 38.55 / 63.84 GiB, free 25.29 GiB. Notable working sets: other game 7.00 GiB, memory compression 5.94 GiB, WSL 4.53 GiB, browser processes, compiler server 0.40 GiB and other agent sessions. This is not a measurement-start sample. No timing numbers exist to label; any later timings on this host need **provisional (PC shared)**. |

### Required arms and exact failure

One required start attempt, in the foreground from the official checkout with the lease held:

```text
python scripts/agent start --mode windowed --rig-test
agent: error: unrecognized arguments: --rig-test
```

The official Windows CLI parser does not accept `--rig-test`. Its ordinary `status` command also uses the SSH path on this PC; the prep attempt returned exactly `ssh: Could not resolve hostname homepc: No such host is known.` Local lock commands worked. No alternate launch, second start attempt, launcher edit, overlay update or receipt bypass was attempted, following the requested one-attempt BLOCKED path.

| Arm | Outcome | Measured fps | Whole-frame p95 / p99 ms | Gen0 / pause p95 / heap | UpdateCity / street mod-work p95 | Every >=30 ms spike |
|---|---|---|---|---|---|---|
| Street, cap 60, 600 s | **NOT RUN / tooling gap**; launch refused | unavailable | unavailable | unavailable | unavailable | unavailable; no window |
| Busy shop, cap 60, 600 s | **NOT RUN / tooling gap**; launch refused before city/fixture | unavailable | unavailable | unavailable | unavailable | unavailable; no window |

Cap health readback: `requested`, `applied`, `vsync_count`, `target_frame_rate`, `measured_fps`, `window_s` are all **unavailable**. The staged request is not evidence that a running player applied it. The Rig bot's earlier health is not reused as this run's proof.

No city was created. Planned fixture `RIGTMP-GR5-14d` remains uncreated; there was no throwaway refusal and no FT fallback. Residents, visitors and `unrentable homes housed=... beds=...` are unavailable. No busy-shop operation was attempted, no save/load or save profiling occurred, and no current-run Player-agent.log is claimed. No <5 ms mod-work or 60 fps pass can be inferred.

### Findings, tooling gaps and NOT RUN

The Windows launch CLI needs a supported PC-local route carrying `RigTest` before these arms can run through the prescribed command. A second staging limitation was found: the only scoped package refresh discovered is `install-mac.py --stage-mod`, which targets the Mac. The Windows `rig_modset.ModSet` transaction replaces the complete DLL set, including RLO/Food Trucks; this run did not use it because those files were outside scope. The authorized copy fallback verified the package but deliberately left `agent-install.json` byte-identical. Its existing PersistentCity entries therefore still described the old package while staged. Launch receipt validation was not reached, so a receipt refusal is a potential next blocker, not an observed error. Both gaps need a reviewed tooling path; no production or rig tooling change was made here.

**Launch-blocked (2 NOT RUN):** street-60 and busy-shop-60, exact parser error above. The missing timing/GC values are nulls, not zeros, in [compact evidence](perf/gr5-2026-11-14d-pc.json) (3382 bytes; SHA-256 `5c3fb29d4fed6179d9f7d147684e214e7595340d6901393a0cc76008ef9a8635`).

**Excluded by this run request (7 NOT RUN):**

1. GR-5 #246 check 4 footprint: known `op building` response `ops_game_api_unavailable` on both rigs; not attempted here.
2. #254 check 4 appearance: stable resident identity across release/reacquire is unavailable; not attempted.
3. GR-5 check 2 save latency: owner save-route decision pending; no saves attempted.
4. Street 30 fps: already attempted on Mac 14b; this run is the 60 fps host only.
5. Busy-shop 30 fps: same reason.
6. #255 / #239 check 3 retime group: Mac 14c already covers it; Mac was not contacted or used.
7. P-D check 7: separate console-guard work; not attempted.

No GitHub issue, release, Workshop upload, merge, version/changelog change or production change was made. Schema stays 1; FoodChoicePriceWeight 1.2 and FoodChoiceHouseholdWeight are unchanged. Other sessions, their files and branches were left alone. No mouse/keyboard input or Windows setting change occurred.

### Restoration and validation

Before copying, SHA-256 backup covered the full isolated PersistentCity tree, `agent-install.json`, `run.json`, root/profile configuration, both optional request files, persistent-city.json, cap-file absence and complete isolated registry preferences. Cleanup removed this run's staged package files, restored the complete original mod folder and exact file bytes/absences, and compared all hashes with `pre-stage-backup\manifest.json`. `restore.json` verifies every recorded file, the full mod inventory and registry export. No changed registry required import. Autosave preferences are byte-identical; runtime state was never started.

No process was launched by this run, so no game stop or force-stop was needed. Process inventory confirmed the isolated player closed before staging and after cleanup. Agent1 lease status was free with pending=0 after release. Original cap absence restored; overlay DLL hash unchanged. No new fixture save exists. No process started by this run remains.

Validation: build passed; compact JSON parsed; documentation diff checked; no offline test suite. Build worktree is removed after evidence commit, and evidence worktree after PR creation. Report copy: `%USERPROFILE%\Coding_Projects\.agent-logs\persistent-city-rig-2026-11-14d-pc.md`.

**RESULT: BLOCKED.**

## Rig results — 14 November 2026 c, Mac (#255 retime checks 1-3 at 1d5d1b76)

Role `rig_test`, where `mac`, isolated `agentmac`. **Counts: 1 passed / 1 partial / 1 failed / 6 NOT RUN groups.** The verdicts count #255 checks 1–3; the six excluded groups are listed below. Check 2 is partial because still screenshots cannot establish motion continuity at every change. No runtime stop occurred. In-game proof gates no merge. **RESULT: DONE** means execution, evidence delivery and restoration completed.

Raw evidence: `<stage>`; report copy: `<agent-logs>/persistent-city-rig-2026-11-14c-mac.md`. Local roots in this section are placeholders: `<stage>` is the run's staging and evidence folder, `<agent-logs>` the local agent-log folder, `<managed>` the local game-managed reference folder, `<rig-tools>` the isolated rig helper's Application Support folder and `<rig-profile>` the isolated game profile's Application Support folder. Raw logs, screenshots, extracted saves, backup manifests and operation replies stay outside Git. All commands ran in the foreground and were awaited. Other sessions were left alone. No Windows PC, ordinary profile, Steam app or ordinary save was used. No production code, weights, schema, version or changelog changed; schema remains 1, FoodChoicePriceWeight 1.2 and FoodChoiceHouseholdWeight 1.0.

### Build and rig

| Item | Observation |
|---|---|
| Candidate | `1d5d1b765218974d41e848926f17137609bf4a58`, origin/codex/persistent-city at preparation. Final fetch: `913abd7dbfdcd7f7651baad768169f66bf698dfb`. |
| Evidence/build branch | `codex/mac-rig-2026-11-14c`, new isolated worktree `rig-2026-11-14c-mac-evidence`; original checkout untouched. One worktree served build and evidence; the unpushed results branch was advanced to the docs-only 913abd7d tip before the report commit. |
| Build | `python3 tools/build.py --game-managed "<managed>"`, exit 0; Homebrew PATH and DOTNET_ROOT as requested. |
| PersistentCity DLL SHA-256 | `087fe9aa8c506c3d2fa43df62fec0f08450095ca415f7c3ffebf64bfd28bde29`; build, package and live staged DLL matched. |
| Agent-rig HEAD | `2b8553606426f3e3e94f8a876dd58d935768732d`, clean; fetch and ff-only pull already up to date. No checkout edits, commits or pushes. |
| AgentRigOps SHA-256 | `dd11e3fdea78c1c0a25ce33e0168ffbce6e785950d9e10caed55015254d748f8`; live op status ready; unchanged at cleanup. |
| RLO version, exact Player.log | `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` RLO mod files unchanged. |
| PC activation, exact Player.log | `[Mod:PersistentCity] Persistent population active: 1611 residents, 1611 visitors, 1685 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.` No commit-stamped PC build line is emitted; matching DLL hashes and validated install receipt establish build identity. |
| Lease/resources | Free/stopped before acquisition; acquired 2026-11-14T13:42:00.5324480Z. Initial resources 69.2 GiB free disk / 57% reported free memory. No refusal, takeover or resource override. |
| Starts/stops | Four cold processes: headless check 1, windowed first check-2 attempt, windowed check-2 repeat, headless check 3; `--rig-test` throughout. Each start succeeded on its first attempt; all stops graceful, no force. |
| Autosave original | Native 5 minutes / 3 saves, prevent_autosave=false. #70 `autosave` confirmed suppressed=true after every load. Process-only suppression ended at stop; original preference/config bytes restored. |
| Frame cap | Temporary `agent-frame-cap.json` with fps=30, vsync=0 for windowed screenshots; removed before headless check 3 and restored to its original absence. No cadence/performance gate. |
| Independent backup | 39 file/absence manifest entries, including full PersistentCity, install.json, isolated root preferences/config and plist; 53 RLO files hashed read-only. |

Exact staging command, run stopped and leased from the official agent-rig checkout:

```sh
python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package
```

Receipt backup: `<rig-tools>/receipt-backups/20261114T134245Z`. Package contains no RLO files; only PersistentCity was staged.

### Per-check outcomes

| #255 check | Result | Evidence |
|---|---|---|
| 1. Maximum native route, speed 20 → 1 | **PASS** | Fresh 08:00 fixture, speed 20 to 10:30, exact `call time {"speed":1,"pause":false,"keep_needs":true}`. FoodDiagnostics.active=true, capability `city running`, no stop line. Next manual save `FT RLO 14c Retime 1030` at 10:31 has schema=1, published epoch sequence=2 and accounting_stop_present=false. |
| 2. Rush-hour changes and visible continuity | **PARTIAL** | Cold repeat ran 1 → 3 → 20 → 1 → 20 → 3 → 1 during rush hour, plus 3 → 1 → 3 within one game minute. Active food diagnostics and no stop; one degraded-retime line at the repeat’s 08:50 speed 20 → 3 change; zero at every other requested change. Two usable screenshot pairs show no obvious jump. Continuous visual motion at every change is unverified. |
| 3. #239 mid-session Living off/on | **FAIL: paid receipts absent** | Fresh 08:00 fixture and speed-20 10:30 setup, then speed 1 and off/on through rlo_option. Both transitions logged; runtime stayed active. Off interval has 0 new paid receipts; on interval adds 0 paid receipts. The continuing-sales expectation was not met; no runtime exception or op error caused the verdict. Original option restored. |

No Persistent City runtime-stop exception or stack was emitted in any of the four processes. Check 1 before/after FoodDiagnostics travelClockRate changed from the speed-20 rate to 0.3333333333333333 at speed 1; capability stayed city running. Save epoch text:

```text
v1
sequence=2
schema=1
minute=2071.2668972015381
population=rlo:0.40.4:724c64860fe2a780eb29eccd135780ac1938ad6a49c20490683a6df7fe7377b0
payload=25429111 persistentcity:state_v1
```

Exact fixture tally (all four loads):

```text
[Persistent City] unrentable homes housed=0 beds=0 unreadable=0 empty=0
```

### Rush-hour sequence and screenshots

The final fetch moved origin from 1d5d1b76 to 913abd7d (#256), changing only docs/rig-checklist.md and docs/perf/gr5-2026-11-14b-mac.json. Production code was identical; the tested DLL remains the build from 1d5d1b76.

First attempt speed changes returned at 08:10, 08:20, 08:30, 08:40, 09:05 and 09:06. Camera correction delayed the last two beyond the requested window, so that attempt supplies runtime observations only. Its process was stopped and a cold repeat ran the requested sequence. No game restart occurred within either attempt.

| Repeat event | Clock returned | Speed |
|---|---|---|
| `check2b-change1-speed3` | 08:10 | 3 |
| `check2b-change2-speed20` | 08:20 | 20 |
| `check2b-change3-speed1` | 08:30 | 1 |
| `check2b-change4-speed20` | 08:40 | 20 |
| `check2b-change5-speed3` | 08:50 | 3 |
| `check2b-change6-speed1` | 08:59 | 1 |
| `check2b-rapid-speed1` | 08:10 | 1 |
| `check2b-rapid-speed3` | 08:10 | 3 |

Cars were present: Player.log reports 65 persistent cars and 63 scheduled road events before the first repeated speed change. Usable raw pairs: `check2b-change1-before.png` / `check2b-change1-after.png` (1 → 3), and `check2b-change3-before.png` / `check2b-change3-after.png` (20 → 1), 960×540. The fixed camera looks along the street from x=215,y=18,z=-18 toward x=215,y=1,z=3. Both show street traffic, kerbside cars and pedestrians; the first has a yellow car beside dark vehicles, the second a yellow taxi and white delivery vehicle. Vehicle and pedestrian positions show no obvious displacement in the sampled pairs. Still frames do not establish continuous trajectories or stable resident identity, so the full visual expectation remains partial.

### Living transitions and paid receipts

Original op read: stored=null, live=true, original_saved=false. For each transition the run resumed at speed 1, called `rlo_option set` with name=living_population and the required boolean, waited five real seconds, and read capability/FoodDiagnostics. Off sales interval was 10:30–12:30; on interval was 12:30–14:30. No sale probe fabricated a receipt. Native shop_log was enabled only for this check, after all rush-hour observations, and stopped afterward. The owned fixture shop is `ba:street_fourthavenue#19`.

| Interval | Paid receipts newly arriving in interval | Paid cents |
|---|---|---|
| Living off | 0 | 0 |
| Living on after second transition | 0 | 0 |

The on count compares saved keys after the on interval against all off-interval keys. Before the toggle, FoodDiagnostics already showed zero purchases and the live key set contained no paid food receipts. The ready shop held 100 croissants at $5, a staffed register and free paper bags, with revenue $0. No causal claim about the option toggle is made: the required continuing-sale evidence did not occur in the two observed intervals. Both native shop_log snapshots report purchases=0, dropped=0 and hook_faults=0. Revenue stayed $0 and croissant stock stayed 100. Final food-contract readback reports capabilitiesPassed=true, firstFailure=null, coordinator mode=Active, images=1, Commits=1 and Refusals=0. These reads establish an active contract, without explaining why no sales arrived. Raw extracted PC keys and native shop visit replies are retained separately. Exact transition lines:

```text
[Mod:PersistentCity] RLO Living population on -> Living population off during play; Persistent City keeps owning its shops, crowds and traffic.
[Mod:PersistentCity] RLO Living population off -> Living population on during play; Persistent City keeps owning its shops, crowds and traffic.
```

Restore op and independent read both returned stored=null, live=true, original_saved=false. No RLO file was edited.

### Player.log around every speed change and toggle

Each block quotes exact numbered lines captured around the operation, including up to three preceding context lines. Long unrelated periodic Population and RLO whole-frame Session diagnostics are omitted here; numbered gaps identify those omissions, and the complete excerpts stay in the raw evidence. The range JSON alongside each raw excerpt records line boundaries and operation arguments; the full process logs remain in the stage. Some successful `time` calls emit no new line, so their preceding context is retained. No missing retime message is invented. The only edit to quoted lines replaces local path roots with the placeholders defined above. The repeat contains one `Clock retime degraded 1 journeys` line (line 2066), inside the 08:50 speed 20 → 3 operation range 2065–2071. Every other rush-hour change has zero, meeting the at-most-one-per-change bound. Other process totals are recorded below. The exact line is in that event’s block below.

`check1-speed1-Player-excerpt.txt`:

```text
2699: crowds: ordinary standers off in every native zone, 115 of them RLO's; RLO's own scene people kept
2700: traffic: ordinary native groups held for Persistent City cars; RLO staged accidents and reckless drivers stood down; cars loaded into held groups past the shares retired off screen; cars RLO holds are never retired
2701: [Mod:FoodTrucks] Test bridge: console 'pc capability status' (1 lines).
2702: [Mod:RealLifeOverhaul] City life check (ground): chat at (252, 0, -13) would stand on parking or a driveway, the place refused: PASS
2703: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2704: [Mod:PersistentCity] Road snapshot started: frame=5645, reason=clock-rate index=-1, scene searches in this planner=0.
2705: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'DeliveryTruck' at 23 km/h began to slow for a sharp bend 2 m ahead, needing 7.4 m/s2 to reach it at 21 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): a firm stop: it was 2 m off when it came into view
2706: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 40 walker(s), 0 standing or sitting; the nearest: nobody within 12 m: PASS.
```

`check1-speed20-to1030-Player-excerpt.txt`:

```text
2432: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: the game is busy (a pause, the time machine, a question, the activity panel, the hospital, the subway or the casino boat); out: 0 walker(s), 0 standing or sitting; the nearest: not looked for: PASS.
2433: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: the game is busy (a pause, the time machine, a question, the activity panel, the hospital, the subway or the casino boat); out: 0 walker(s), 0 standing or sitting; the nearest: not looked for: PASS.
2434: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: the game is busy (a pause, the time machine, a question, the activity panel, the hospital, the subway or the casino boat); out: 0 walker(s), 0 standing or sitting; the nearest: not looked for: PASS.
2435: [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 1 10:30.
2436: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2437: [Mod:PersistentCity] Road snapshot started: frame=3509, reason=clock-rate index=-1, scene searches in this planner=0.
2438: [Mod:RealLifeOverhaul] Street people: besides the walking pedestrians, the standing and sitting people of 7 of the game's pools count as people on the street (BenchPedestrian, CasualStationaryAi, SportPedestrian, SwimmingPoolBaseHuman, StreetPerformer, Homeless, Golfer).
2440: [Mod:RealLifeOverhaul] Session check (work named for a long frame): the line of the frame of 400 ms leaves out 8 piece(s) of work begun after it, before the line was written; what the mod named up to its end is still named (a ring of 16): PASS.
2441: [Mod:RealLifeOverhaul] Meta: welcome shown (after 0.45.0).
2442: [Mod:RealLifeOverhaul] Meta: props notice shown: the props of reallife_props cannot be drawn: the game's lit shader is not supported on this graphics setting.
2443: [Mod:RealLifeOverhaul] Meta: Meta check (welcome notice): shown as the game's own notification with a click that opens the Guide on 'What changed': PASS.
2444: [Mod:RealLifeOverhaul] Car ownership: records read.
2445: [Mod:RealLifeOverhaul] City events: state loaded; day 1 of a 60-day year (Winter), today: nothing on.
2446: [Mod:RealLifeOverhaul] Traffic check (senses): cars sense layers [Player, Human, Vehicles]; pedestrians are on 'Human' with their capsule on; sensor box 4.5 m long on 'AiVehicles', which does not meet the pedestrians' layer in the physics settings; mode: stand-ins on 'Player' for everybody in a car's way.
2447: [Mod:RealLifeOverhaul] Traffic check (proxy): 0 of 16 in use: PASS
2448: [Mod:RealLifeOverhaul] Traffic check (cost): 1 looks in the last minute, 990 microseconds on average and 990 at worst (wanted: under 100 and 300); at most 4 cars looked at, 39 people in the grid, 0 of 16 stand-ins in use, 0 cars braked, 0 watched for contact now; mode StandIns: over budget; where it went, on average and in the worst look (microseconds): the cars gathered 30 and 30, the people 918 and 918, the strips and the decisions 32 and 32, the stand-ins and the fades 10 and 10; the diagnostics' own lines are not counted; people looked over in a look, at most: 40 walking and 0 standing or sitting, 0 of those near enough when last listed to be read; outside the looks, 11 car(s) measured in frames of their own (139 microseconds at most) and 0 that took the figures of a car of their kind.
2449: [Mod:RealLifeOverhaul] Traffic check (city drivers' cost): 1 looks in the last minute, 165 microseconds a look on average and 165 at worst, over 12 cars a frame (wanted: under 150 and 600); at most 3 cars read, 4 things on the roads, 0 cars slowed at once, 0 hold line(s): FAIL, over budget; of it the street read and its cars gathered 77 on average and 77 in the worst look; outside the looks, 2 car(s) measured in frames of their own (148 microseconds at most) and 4 that took the figures of a car of their kind.
2451: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'Taxi' at 27 km/h began to slow for a sharp bend 11 m ahead, needing 1.5 m/s2 to reach it at 19 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
2452: [Mod:RealLifeOverhaul] Civic: ready. Election 0 None, election day 15, approval 58, 6 site(s) in progress.
2453: [Mod:RealLifeOverhaul] Contacts check (picture): 'reallife-cityhall' is a logo, from the game's logo shape 'headquarter3', kept (its file read on a worker, 0.03 ms of the main thread, wanted under 3 ms); 27 contact picture(s): 9 face(s), 18 logo(s), 27 drawn: PASS
```

`check2-change1-speed3-Player-excerpt.txt`:

```text
1824: [Mod:RealLifeOverhaul] Traffic check (proxy): 0 of 16 in use: PASS
1825: [Mod:RealLifeOverhaul] Clips: the 'smoke' clip is played by a street character (said the first time only).
1826: [Mod:RealLifeOverhaul] City life: smoker started 62 m away; 1 of 3 sites out, 1 of 8 people.
1827: [Persistent City] RLO traffic scene switches given back (the player's stored options).
1828: [Mod:PersistentCity] Road snapshot started: frame=2734, reason=clock-rate index=-1, scene searches in this planner=0.
1829: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 40 walker(s), 1 standing or sitting; the nearest: nobody within 12 m: PASS.
1830: [Mod:PersistentCity] RLO single-pass read context=periodic status=Ready identities=400 elapsedMs=3.068 reads=9 totalMs=21.348 maxMs=5.964
```

`check2-change2-speed20-Player-excerpt.txt`:

```text
1934: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'FreightTruckT1' at 40 km/h began to slow for a red light 29 m ahead, needing 2.2 m/s2 to reach it at 0 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
1935: Couldn't find AI business logo: "BusinessLogos/Summit Care Clinic/Billboard" with extension .jpg or .png
1936: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 0 walker(s), 1 standing or sitting; the nearest: nobody within 12 m: PASS.
1937: Couldn't find AI business logo: "BusinessLogos/The Cigar Box/Billboard" with extension .jpg or .png
1938: [Persistent City] RLO traffic scene switches given back (the player's stored options).
1939: [Mod:PersistentCity] Road snapshot started: frame=4010, reason=clock-rate index=-1, scene searches in this planner=0.
```

`check2-change3-speed1-Player-excerpt.txt`:

```text
2005: Couldn't find AI business logo: "BusinessLogos/Maple Leaf Tea/Billboard" with extension .jpg or .png
2006: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 3 walker(s), 1 standing or sitting; the nearest: nobody within 12 m: PASS.
2007: [Mod:FoodTrucks] Test bridge: shot check2-change3-before (960x540) from x 176.0, y 18.0, z -56.0 (the camera stands inside something solid (a building, a prop or a vehicle); the picture may be dark); uncovered SM_FlatironAreaBuilding_03_Variation 02 (CityBuildingController, reveal 0.00, blocking the game's camera); outline lifted from SM_1StStreet_CheapRezidential_1; hover lifted from SM_FlatironAreaBuilding_06_Variation_Corner (under the mouse, its building card open, 6 objects outlined): <rig-profile>/FoodTrucksTest/shots/check2-change3-before.png.
2008: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2009: [Mod:PersistentCity] Road snapshot started: frame=4631, reason=clock-rate index=-1, scene searches in this planner=0.
2010: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'Taxi' at 35 km/h began to slow for a car 35 m ahead, needing 1.2 m/s2 to reach it at 7 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
```

`check2-change4-speed20-Player-excerpt.txt`:

```text
2108: Couldn't find AI business logo: "BusinessLogos/Summit Care Clinic/Billboard" with extension .jpg or .png
2109: [Mod:PersistentCity] RLO single-pass read context=periodic status=Ready identities=400 elapsedMs=3.267 reads=13 totalMs=34.063 maxMs=5.964
2110: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 2 walker(s), 1 standing or sitting; the nearest: nobody within 12 m: PASS.
2111: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2112: [Mod:PersistentCity] Road snapshot started: frame=5779, reason=clock-rate index=-1, scene searches in this planner=0.
2113: [Mod:RealLifeOverhaul] Session: the managed heap grew by 116 MB in one frame of 34 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "city life: a site being set up" 98.3 s before, "city life: a character's look being built (a mesh merge)" 104.9 s before, "city life: a site being set up" 104.9 s before; on foot in the street, game hour 8.
2114: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'Taxi' at 28 km/h began to slow for a red light 26 m ahead, needing 1.2 m/s2 to reach it at 0 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
```

`check2-change5-speed3-Player-excerpt.txt`:

```text
2178: Couldn't find AI business logo: "BusinessLogos/Summit Care Clinic/Billboard" with extension .jpg or .png
2179: [Mod:FoodTrucks] Test bridge: shot check2-change5-before (960x540) from x 215.0, y 18.0, z -18.0; uncovered SM_FlatironAreaBuilding_03_Variation 02 (CityBuildingController, reveal 0.00, blocking the game's camera); outline lifted from SM_1StStreet_CheapRezidential_1; hover lifted from SM_FlatironAreaBuilding_06_Variation_Corner (under the mouse, its building card open, 6 objects outlined): <rig-profile>/FoodTrucksTest/shots/check2-change5-before.png.
2180: Couldn't find AI business logo: "BusinessLogos/Maple Leaf Tea/Billboard" with extension .jpg or .png
2181: [Mod:CityPerformance] PERF frames n=725 mean_ms=41.38 p50_le_ms=35 p95_le_ms=61 p99_le_ms=192 max_ms=652.79 over50=85 bin250plus=7 refill=False
2182: [Mod:CityPerformance] PERF scope=CullingGroupController.Update calls=1450 total_ms=1.622 max_ms=0.017 exceptions=0
2183: [Mod:CityPerformance] PERF scope=TrafficManager.FixedUpdate calls=1469 total_ms=430.697 max_ms=2.165 exceptions=0
2184: [Mod:CityPerformance] PERF scope=PedestrianSpawner.SetActivePedestrians calls=2 total_ms=58.800 max_ms=58.519 exceptions=0
2185: [Mod:CityPerformance] PERF scope=SaveGameManager.SerializeSaveGame calls=0 total_ms=0.000 max_ms=0.000 exceptions=0
2186: [Mod:CityPerformance] PERF scope=GameManager.RunMainGameTick calls=725 total_ms=137.141 max_ms=88.989 exceptions=0
2187: [Mod:CityPerformance] DETAIL hooks=0; retained=0; overwritten=0; daily_retained=0; daily_overwritten=0; session_worst_method=none; max_ms=0; full breakdown in exported methods CSV and capabilities.
2188: [Mod:CityPerformance] TREE requested=True; installed=False; faulted=False; registered_seen=0; configured_records=0; skipped=0; changed_elsewhere=0; map_hold=False; lod_bias=1; maximum_lod=0; configured records are not visible-tree or selected-LOD counts.
2189: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2190: [Mod:PersistentCity] Road snapshot started: frame=6250, reason=clock-rate index=-1, scene searches in this planner=0.
```

`check2-change6-speed1-Player-excerpt.txt`:

```text
2227: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'Bima320' at 35 km/h began to slow for a car 6 m ahead, needing 1.4 m/s2 to reach it at 26 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
2229: [Mod:FoodTrucks] Test bridge: shot check2-change6-before (960x540) from x 215.0, y 18.0, z -18.0; uncovered SM_FlatironAreaBuilding_03_Variation 02 (CityBuildingController, reveal 0.00, blocking the game's camera); outline lifted from SM_1StStreet_CheapRezidential_1; hover lifted from SM_FlatironAreaBuilding_06_Variation_Corner (under the mouse, its building card open, 6 objects outlined): <rig-profile>/FoodTrucksTest/shots/check2-change6-before.png.
2231: [Mod:RealLifeOverhaul] Session: the managed heap grew by 12 MB in one frame of 33 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "flare: the hour's milestones" 8.1 s before, "flare: the hour's words asked ahead" 8.1 s before, "flare: the hour's unread count" 8.1 s before; on foot in the street, game hour 9.
2232: [Mod:RealLifeOverhaul] Traffic check (cost): 413 looks in the last minute, 67 microseconds on average and 606 at worst (wanted: under 100 and 300); at most 16 cars looked at, 43 people in the grid, 2 of 16 stand-ins in use, 1 cars braked, 0 watched for contact now; mode StandIns: over budget; where it went, on average and in the worst look (microseconds): the cars gathered 18 and 20, the people 39 and 14, the strips and the decisions 6 and 45, the stand-ins and the fades 3 and 528; the diagnostics' own lines are not counted; people looked over in a look, at most: 40 walking and 1 standing or sitting, 1 of those near enough when last listed to be read; outside the looks, 0 car(s) measured in frames of their own (0 microseconds at most) and 0 that took the figures of a car of their kind.
2234: [Mod:RealLifeOverhaul] Session: the managed heap grew by 12 MB in one frame of 201 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "flare: the hour's milestones" 8.3 s before, "flare: the hour's words asked ahead" 8.4 s before, "flare: the hour's unread count" 8.4 s before; on foot in the street, game hour 9.
2236: [Mod:RealLifeOverhaul] Traffic check (city drivers' cost): 412 looks in the last minute, 86 microseconds a look on average and 512 at worst, over 12 cars a frame (wanted: under 150 and 600); at most 16 cars read, 17 things on the roads, 9 cars slowed at once, 0 hold line(s): PASS; of it the street read and its cars gathered 57 on average and 94 in the worst look; outside the looks, 0 car(s) measured in frames of their own (0 microseconds at most) and 0 that took the figures of a car of their kind.
```

`check2-initial-speed1-Player-excerpt.txt`:

```text
1746: [Mod:CityPerformance] PERF scope=GameManager.RunMainGameTick calls=664 total_ms=86.297 max_ms=32.667 exceptions=0
1747: [Mod:CityPerformance] DETAIL hooks=0; retained=0; overwritten=0; daily_retained=0; daily_overwritten=0; session_worst_method=none; max_ms=0; full breakdown in exported methods CSV and capabilities.
1748: [Mod:CityPerformance] TREE requested=True; installed=False; faulted=False; registered_seen=0; configured_records=0; skipped=0; changed_elsewhere=0; map_hold=False; lod_bias=1; maximum_lod=0; configured records are not visible-tree or selected-LOD counts.
1749: [Mod:RealLifeOverhaul] Street people: besides the walking pedestrians, the standing and sitting people of 7 of the game's pools count as people on the street (BenchPedestrian, CasualStationaryAi, SportPedestrian, SwimmingPoolBaseHuman, StreetPerformer, Homeless, Golfer).
1751: [Mod:RealLifeOverhaul] Session check (work named for a long frame): the line of the frame of 133 ms leaves out 2 piece(s) of work begun after it, before the line was written; what the mod named up to its end is still named (a ring of 16): PASS.
1752: [Mod:RealLifeOverhaul] Traffic check (senses): cars sense layers [Player, Human, Vehicles]; pedestrians are on 'Human' with their capsule on; sensor box 4.5 m long on 'AiVehicles', which does not meet the pedestrians' layer in the physics settings; mode: stand-ins on 'Player' for everybody in a car's way.
1753: [Mod:RealLifeOverhaul] Traffic check (proxy): 0 of 16 in use: PASS
1754: [Mod:RealLifeOverhaul] Traffic check (cost): 1 looks in the last minute, 965 microseconds on average and 965 at worst (wanted: under 100 and 300); at most 2 cars looked at, 10 people in the grid, 0 of 16 stand-ins in use, 0 cars braked, 0 watched for contact now; mode StandIns: over budget; where it went, on average and in the worst look (microseconds): the cars gathered 28 and 28, the people 892 and 892, the strips and the decisions 34 and 34, the stand-ins and the fades 12 and 12; the diagnostics' own lines are not counted; people looked over in a look, at most: 10 walking and 1 standing or sitting, 1 of those near enough when last listed to be read; outside the looks, 9 car(s) measured in frames of their own (208 microseconds at most) and 0 that took the figures of a car of their kind.
1755: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'DeliveryTruck' at 33 km/h began to slow for a red light 21 m ahead, needing 2.1 m/s2 to reach it at 0 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
1756: [Mod:RealLifeOverhaul] Traffic check (city drivers' cost): 1 looks in the last minute, 229 microseconds a look on average and 229 at worst, over 12 cars a frame (wanted: under 150 and 600); at most 4 cars read, 7 things on the roads, 1 cars slowed at once, 0 hold line(s): FAIL, over budget; of it the street read and its cars gathered 127 on average and 127 in the worst look; outside the looks, 8 car(s) measured in frames of their own (130 microseconds at most) and 1 that took the figures of a car of their kind.
1757: [Mod:RealLifeOverhaul] Civic: ready. Election 0 None, election day 15, approval 58, 6 site(s) in progress.
1759: [Mod:RealLifeOverhaul] Contacts check (picture): 'reallife-cityhall' is a logo, from the game's logo shape 'headquarter3', kept (its file read on a worker, 0.04 ms of the main thread, wanted under 3 ms); 27 contact picture(s): 9 face(s), 18 logo(s), 27 drawn: PASS
```

`check2-rapid-Player-excerpt.txt`:

```text
1858: traffic: own cars inactive; native traffic as the game and RLO run it
1859: [Mod:FoodTrucks] Test bridge: console 'pc capability status' (1 lines).
1860: [Mod:RealLifeOverhaul] Session: the managed heap grew by 15 MB in one frame of 68 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "city life: a site being set up" 3.0 s before, "transit: a parking lane beside a bus zone laid out again" 27.8 s before, "transit: a parking lane beside a bus zone laid out again" 28.0 s before; on foot in the street, game hour 8.
1861: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'Taxi' at 21 km/h began to slow for a car 9 m ahead, needing 2.0 m/s2 to reach it at 7 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
1862: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 40 walker(s), 1 standing or sitting; the nearest: nobody within 12 m: PASS.
```

`check2b-change1-speed3-Player-excerpt.txt`:

```text
1796: [Mod:RealLifeOverhaul] Patrol: two officers on the beat, 54 m away, 4 stops.
1797: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: a job board or an employer's door has the interact key; out: 14 walker(s), 2 standing or sitting; the nearest: not looked for: PASS.
1798: [Mod:FoodTrucks] Test bridge: shot check2b-change1-before (960x540) from x 215.0, y 18.0, z -18.0; uncovered SM_FancyBuilding_V03 Variant 3 (CityBuildingController, reveal 0.00, blocking the game's camera); outline lifted from SM_1StStreet_CheapRezidential_1; hover lifted from Prefab_5thAV_BuildingV1_2 (under the mouse, its building card open, 6 objects outlined): <rig-profile>/FoodTrucksTest/shots/check2b-change1-before.png.
1799: [Persistent City] RLO traffic scene switches given back (the player's stored options).
1800: [Mod:PersistentCity] Road snapshot started: frame=2098, reason=clock-rate index=-1, scene searches in this planner=0.
1801: [Mod:RealLifeOverhaul] Session: the managed heap grew by 95 MB in one frame of 33 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "transit: a parking lane beside a bus zone laid out again" 26.2 s before, "transit: a parking lane beside a bus zone laid out again" 26.6 s before, "transit: a parking lane beside a bus zone laid out again" 26.8 s before; on foot in the street, game hour 8.
1802: [Mod:CityPerformance] PERF frames n=744 mean_ms=40.32 p50_le_ms=35 p95_le_ms=68 p99_le_ms=163 max_ms=393.21 over50=68 bin250plus=1 refill=False
1803: [Mod:CityPerformance] PERF scope=CullingGroupController.Update calls=1488 total_ms=1.682 max_ms=0.011 exceptions=0
1804: [Mod:CityPerformance] PERF scope=SaveGameManager.SerializeSaveGame calls=0 total_ms=0.000 max_ms=0.000 exceptions=0
1805: [Mod:CityPerformance] PERF scope=TrafficManager.FixedUpdate calls=1489 total_ms=430.482 max_ms=0.693 exceptions=0
1806: [Mod:CityPerformance] PERF scope=PedestrianSpawner.SetActivePedestrians calls=1 total_ms=39.676 max_ms=39.676 exceptions=0
1807: [Mod:CityPerformance] PERF scope=GameManager.RunMainGameTick calls=744 total_ms=44.723 max_ms=0.181 exceptions=0
1808: [Mod:CityPerformance] DETAIL hooks=0; retained=0; overwritten=0; daily_retained=0; daily_overwritten=0; session_worst_method=none; max_ms=0; full breakdown in exported methods CSV and capabilities.
1809: [Mod:CityPerformance] TREE requested=True; installed=False; faulted=False; registered_seen=0; configured_records=0; skipped=0; changed_elsewhere=0; map_hold=False; lod_bias=1; maximum_lod=0; configured records are not visible-tree or selected-LOD counts.
```

`check2b-change2-speed20-Player-excerpt.txt`:

```text
1869: [Mod:RealLifeOverhaul] Clips: the 'sip' clip is played by a street character (said the first time only).
1870: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: a job board or an employer's door has the interact key; out: 14 walker(s), 2 standing or sitting; the nearest: not looked for: PASS.
1871: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'Taxi' at 16 km/h began to slow for a red light 8 m ahead, needing 1.5 m/s2 to reach it at 0 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
1872: [Persistent City] RLO traffic scene switches given back (the player's stored options).
1873: [Mod:PersistentCity] Road snapshot started: frame=3396, reason=clock-rate index=-1, scene searches in this planner=0.
1874: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: a job board or an employer's door has the interact key; out: 14 walker(s), 2 standing or sitting; the nearest: not looked for: PASS.
```

`check2b-change3-speed1-Player-excerpt.txt`:

```text
1927: Couldn't find AI business logo: "BusinessLogos/Fenwick and Daughters/Billboard" with extension .jpg or .png
1928: Couldn't find AI business logo: "BusinessLogos/Fenwick and Daughters/Billboard" with extension .jpg or .png
1929: [Mod:FoodTrucks] Test bridge: shot check2b-change3-before (960x540) from x 215.0, y 18.0, z -18.0; uncovered SM_FancyBuilding_V03 Variant 3 (CityBuildingController, reveal 0.00, blocking the game's camera); outline lifted from SM_1StStreet_CheapRezidential_1; hover lifted from Prefab_5thAV_BuildingV1_2 (under the mouse, its building card open, 6 objects outlined): <rig-profile>/FoodTrucksTest/shots/check2b-change3-before.png.
1930: [Persistent City] RLO traffic scene switches given back (the player's stored options).
1931: [Mod:PersistentCity] Road snapshot started: frame=4086, reason=clock-rate index=-1, scene searches in this planner=0.
1932: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: a job board or an employer's door has the interact key; out: 14 walker(s), 2 standing or sitting; the nearest: not looked for: PASS.
1933: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'UMCDesert' at 21 km/h began to slow for a sharp bend 1 m ahead, needing 1.7 m/s2 to reach it at 21 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
```

`check2b-change4-speed20-Player-excerpt.txt`:

```text
2022: Couldn't find AI business logo: "BusinessLogos/Fenwick and Daughters/Billboard" with extension .jpg or .png
2023: [Mod:RealLifeOverhaul] Traffic check (proxy): 0 of 16 in use: PASS
2024: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: a job board or an employer's door has the interact key; out: 14 walker(s), 2 standing or sitting; the nearest: not looked for: PASS.
2025: [Mod:RealLifeOverhaul] Traffic check (proxy): 1 of 16 in use: PASS
2026: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2027: [Mod:PersistentCity] Road snapshot started: frame=5231, reason=clock-rate index=-1, scene searches in this planner=0.
2028: [Mod:RealLifeOverhaul] Traffic check (person ahead): 'Taxi' at 7 km/h, the person 3 m ahead, needs 4 m to stop: braking with whatever it takes (nothing is owed: it must stop): PASS
2029: [Mod:RealLifeOverhaul] Horn (a traffic car's): the car's light Green, 3 in earshot: 0 hurry on, 1 step back, 0 stop and glare, 1 look round.
2030: [Mod:RealLifeOverhaul] Traffic check (horn): 'Taxi': somebody stepped out 3 m ahead at 7 km/h: a hard brake: its horn sounds, and the people answer it: PASS
2031: [Mod:RealLifeOverhaul] Traffic check (proxy): 0 of 16 in use: PASS
```

`check2b-change5-speed3-Player-excerpt.txt`:

```text
2062: [Mod:RealLifeOverhaul] People: the 'hands_up_hold' clip is played by a scene character (said the first time only).
2063: Couldn't find AI business logo: "BusinessLogos/Fenwick and Daughters/Billboard" with extension .jpg or .png
2064: Couldn't find AI business logo: "BusinessLogos/Fenwick and Daughters/Billboard" with extension .jpg or .png
2065: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2066: [Mod:PersistentCity] Clock retime degraded 1 journeys: each keeps its old pace on its current road edge or drives outside traffic until its next road section.
2067: [Mod:PersistentCity] Road snapshot started: frame=5591, reason=clock-rate index=-1, scene searches in this planner=0.
2068: [Mod:RealLifeOverhaul] Traffic check (cost): 413 looks in the last minute, 112 microseconds on average and 2844 at worst (wanted: under 100 and 300); at most 12 cars looked at, 44 people in the grid, 4 of 16 stand-ins in use, 1 cars braked, 0 watched for contact now; mode StandIns: over budget; where it went, on average and in the worst look (microseconds): the cars gathered 19 and 37, the people 72 and 2796, the strips and the decisions 12 and 8, the stand-ins and the fades 9 and 2; the diagnostics' own lines are not counted; people looked over in a look, at most: 40 walking and 2 standing or sitting, 2 of those near enough when last listed to be read; outside the looks, 0 car(s) measured in frames of their own (0 microseconds at most) and 0 that took the figures of a car of their kind.
2070: [Mod:RealLifeOverhaul] Traffic check (city drivers' cost): 410 looks in the last minute, 90 microseconds a look on average and 1081 at worst, over 12 cars a frame (wanted: under 150 and 600); at most 12 cars read, 17 things on the roads, 9 cars slowed at once, 0 hold line(s): FAIL, over budget; of it the street read and its cars gathered 60 on average and 1001 in the worst look; outside the looks, 0 car(s) measured in frames of their own (0 microseconds at most) and 0 that took the figures of a car of their kind.
```

`check2b-change6-speed1-Player-excerpt.txt`:

```text
2116: [Mod:RealLifeOverhaul] Traffic check (person ahead): 'Taxi' at 36 km/h, somebody about to step out 5 m ahead, needs 17 m: they wait at the kerb: PASS
2117: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: a job board or an employer's door has the interact key; out: 14 walker(s), 2 standing or sitting; the nearest: not looked for: PASS.
2118: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'DeliveryTruck' at 24 km/h began to slow for a red light 19 m ahead, needing 1.2 m/s2 to reach it at 0 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
2119: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2120: [Mod:PersistentCity] Road snapshot started: frame=6116, reason=clock-rate index=-1, scene searches in this planner=0.
```

`check2b-initial-speed1-Player-excerpt.txt`:

```text
1714: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: the game is busy (a pause, the time machine, a question, the activity panel, the hospital, the subway or the casino boat); out: 0 walker(s), 0 standing or sitting; the nearest: not looked for: PASS.
1715: [Mod:FoodTrucks] Test bridge: teleport to x 215.0, z 0.0: y 0.01, the only ground surface, walkable; walking routes checked from the player's place.
1716: [Mod:FoodTrucks] Test bridge: moving the player to x 215.0, y 0.1, z 0.0.
1717: [Mod:RealLifeOverhaul] Street people: besides the walking pedestrians, the standing and sitting people of 7 of the game's pools count as people on the street (BenchPedestrian, CasualStationaryAi, SportPedestrian, SwimmingPoolBaseHuman, StreetPerformer, Homeless, Golfer).
1718: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 14 walker(s), 2 standing or sitting; the nearest: nobody within 12 m: PASS.
1719: [Mod:RealLifeOverhaul] Jobs check (board panel): at the board of 23 3rd Street (retail's office): the game's own panel offers the board on the game's interact key.
1721: [Mod:RealLifeOverhaul] Session check (work named for a long frame): the line of the frame of 133 ms leaves out 9 piece(s) of work begun after it, before the line was written; what the mod named up to its end is still named (a ring of 16): PASS.
1722: [Campaign Objectives] Visibility: NativeMenuOpen: OverlayUI.IsVisible=true, overlay RealLifeOverhaul.GameUi.WorldPanel (RealLifeOverhaul) (temporary block)
1724: [Mod:RealLifeOverhaul] Traffic check (senses): cars sense layers [Player, Human, Vehicles]; pedestrians are on 'Human' with their capsule on; sensor box 4.5 m long on 'AiVehicles', which does not meet the pedestrians' layer in the physics settings; mode: stand-ins on 'Player' for everybody in a car's way.
1725: [Mod:RealLifeOverhaul] Traffic check (proxy): 0 of 16 in use: PASS
1726: [Mod:RealLifeOverhaul] Traffic check (cost): 1 looks in the last minute, 912 microseconds on average and 912 at worst (wanted: under 100 and 300); at most 2 cars looked at, 9 people in the grid, 0 of 16 stand-ins in use, 0 cars braked, 0 watched for contact now; mode StandIns: over budget; where it went, on average and in the worst look (microseconds): the cars gathered 31 and 31, the people 836 and 836, the strips and the decisions 32 and 32, the stand-ins and the fades 13 and 13; the diagnostics' own lines are not counted; people looked over in a look, at most: 14 walking and 2 standing or sitting, 2 of those near enough when last listed to be read; outside the looks, 9 car(s) measured in frames of their own (219 microseconds at most) and 0 that took the figures of a car of their kind.
1727: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'DeliveryTruck' at 31 km/h began to slow for a red light 27 m ahead, needing 1.4 m/s2 to reach it at 0 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
1728: [Mod:RealLifeOverhaul] Traffic check (city drivers' cost): 1 looks in the last minute, 224 microseconds a look on average and 224 at worst, over 12 cars a frame (wanted: under 150 and 600); at most 3 cars read, 9 things on the roads, 1 cars slowed at once, 0 hold line(s): FAIL, over budget; of it the street read and its cars gathered 108 on average and 108 in the worst look; outside the looks, 8 car(s) measured in frames of their own (128 microseconds at most) and 2 that took the figures of a car of their kind.
1730: [Mod:RealLifeOverhaul] Civic: ready. Election 0 None, election day 15, approval 58, 6 site(s) in progress.
1731: [Mod:RealLifeOverhaul] Civic: site 1 (Rebuild) at 33 3rd Street shows its hoarding without the game's scaffold: no street side has room for the game's scaffold.
1732: [Mod:RealLifeOverhaul] Civic: site 1 (Rebuild) set out at 33 3rd Street, stage 1: 3 prop(s), 2 bay(s), 0 lift(s).
1733: [Mod:RealLifeOverhaul] Clips: the 'kneel_work' clip is played by a street character (said the first time only).
1734: [Mod:RealLifeOverhaul] Clips: the 'clipboard_write' clip is played by a street character (said the first time only).
1735: [Mod:RealLifeOverhaul] Civic: a crew of 2 at site 1 (Rebuild, stage 1).
1736: [Mod:RealLifeOverhaul] Contacts check (picture): 'reallife-cityhall' is a logo, from the game's logo shape 'headquarter3', kept (its file read on a worker, 0.03 ms of the main thread, wanted under 3 ms); 27 contact picture(s): 9 face(s), 18 logo(s), 27 drawn: PASS
```

`check2b-rapid-Player-excerpt.txt`:

```text
1809: [Mod:CityPerformance] TREE requested=True; installed=False; faulted=False; registered_seen=0; configured_records=0; skipped=0; changed_elsewhere=0; map_hold=False; lod_bias=1; maximum_lod=0; configured records are not visible-tree or selected-LOD counts.
1810: [Mod:FoodTrucks] Test bridge: shot check2b-change1-after (960x540) from x 215.0, y 18.0, z -18.0; uncovered SM_FancyBuilding_V03 Variant 3 (CityBuildingController, reveal 0.00, blocking the game's camera); outline lifted from SM_1StStreet_CheapRezidential_1; hover lifted from Prefab_5thAV_BuildingV1_2 (under the mouse, its building card open, 6 objects outlined): <rig-profile>/FoodTrucksTest/shots/check2b-change1-after.png.
1811: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: a job board or an employer's door has the interact key; out: 14 walker(s), 2 standing or sitting; the nearest: not looked for: PASS.
1812: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'MissamVillian' at 28 km/h began to slow for a red light 27 m ahead, needing 1.2 m/s2 to reach it at 0 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
1813: [Mod:RealLifeOverhaul] City life check (ground): sweeper at (304, 0, 4) would stand on parking or a driveway, the place refused: PASS
1814: [Mod:RealLifeOverhaul] Session: the managed heap grew by 14 MB in one frame of 34 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "city life: a site being set up" 0.0 s before, "transit: a parking lane beside a bus zone laid out again" 31.2 s before, "transit: a parking lane beside a bus zone laid out again" 31.5 s before; on foot in the street, game hour 8.
1815: [Mod:RealLifeOverhaul] Session: the managed heap grew by 8 MB in one frame of 33 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "city life: a site being set up" 0.1 s before, "transit: a parking lane beside a bus zone laid out again" 31.2 s before, "transit: a parking lane beside a bus zone laid out again" 31.6 s before; on foot in the street, game hour 8.
1816: [Mod:RealLifeOverhaul] Session: the managed heap grew by 8 MB in one frame of 34 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "city life: a site being set up" 0.1 s before, "transit: a parking lane beside a bus zone laid out again" 31.3 s before, "transit: a parking lane beside a bus zone laid out again" 31.6 s before; on foot in the street, game hour 8.
1817: [Mod:RealLifeOverhaul] Session: the managed heap grew by 13 MB in one frame of 33 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "city life: a site being set up" 0.2 s before, "transit: a parking lane beside a bus zone laid out again" 31.3 s before, "transit: a parking lane beside a bus zone laid out again" 31.7 s before; on foot in the street, game hour 8.
```

`check3-explicit-unpause-Player-excerpt.txt`:

```text
2527: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 0 walker(s), 0 standing or sitting; the nearest: nobody within 12 m: PASS.
2528: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'FreightTruckT1' at 34 km/h began to slow for a sharp bend 26 m ahead, needing 1.2 m/s2 to reach it at 19 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
2529: [Mod:RealLifeOverhaul] Patrol: two officers on the beat, 62 m away, 4 stops.
2530: [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 1 10:30.
2531: [Mod:PersistentCity] RLO single-pass read context=periodic status=Ready identities=400 elapsedMs=2.717 reads=8 totalMs=13.992 maxMs=3.283
2532: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 0 walker(s), 0 standing or sitting; the nearest: nobody within 12 m: PASS.
```

`check3-off-sales-Player-excerpt.txt`:

```text
2708: crowds: ordinary standers off in every native zone, 115 of them RLO's; RLO's own scene people kept
2709: traffic: ordinary native groups held for Persistent City cars; RLO staged accidents and reckless drivers stood down; cars loaded into held groups past the shares retired off screen; cars RLO holds are never retired
2710: [Mod:FoodTrucks] Test bridge: console 'pc capability status' (1 lines).
2711: [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 1 12:30.
2712: [Mod:CityPerformance] PERF frames n=686 mean_ms=43.78 p50_le_ms=35 p95_le_ms=126 p99_le_ms=250 max_ms=415.53 over50=67 bin250plus=9 refill=False
2713: [Mod:CityPerformance] PERF scope=TrafficManager.FixedUpdate calls=1333 total_ms=295.860 max_ms=0.346 exceptions=0
2714: [Mod:CityPerformance] PERF scope=GameManager.RunMainGameTick calls=686 total_ms=30.606 max_ms=4.437 exceptions=0
2715: [Mod:CityPerformance] PERF scope=CullingGroupController.Update calls=1372 total_ms=1.269 max_ms=0.003 exceptions=0
2716: [Mod:CityPerformance] PERF scope=SaveGameManager.SerializeSaveGame calls=0 total_ms=0.000 max_ms=0.000 exceptions=0
2717: [Mod:CityPerformance] PERF scope=PedestrianSpawner.SetActivePedestrians calls=2 total_ms=33.234 max_ms=33.230 exceptions=0
2718: [Mod:CityPerformance] DETAIL hooks=0; retained=0; overwritten=0; daily_retained=0; daily_overwritten=0; session_worst_method=none; max_ms=0; full breakdown in exported methods CSV and capabilities.
2719: [Mod:CityPerformance] TREE requested=True; installed=False; faulted=False; registered_seen=0; configured_records=0; skipped=0; changed_elsewhere=0; map_hold=False; lod_bias=1; maximum_lod=0; configured records are not visible-tree or selected-LOD counts.
2720: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2721: [Mod:PersistentCity] Clock retime degraded 1 journeys: each keeps its old pace on its current road edge or drives outside traffic until its next road section.
2722: [Mod:PersistentCity] Road snapshot started: frame=5959, reason=clock-rate index=-1, scene searches in this planner=0.
```

`check3-off-speed1-Player-excerpt.txt`:

```text
2655: crowds: ordinary standers off in every native zone, 115 of them RLO's; RLO's own scene people kept
2656: traffic: ordinary native groups held for Persistent City cars; RLO staged accidents and reckless drivers stood down; cars loaded into held groups past the shares retired off screen; cars RLO holds are never retired
2657: [Mod:FoodTrucks] Test bridge: console 'pc capability status' (1 lines).
2658: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2659: [Mod:PersistentCity] Road snapshot started: frame=5760, reason=clock-rate index=-1, scene searches in this planner=0.
2660: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 40 walker(s), 0 standing or sitting; the nearest: nobody within 12 m: PASS.
```

`check3-on-sales-Player-excerpt.txt`:

```text
2992: crowds: ordinary standers off in every native zone, 115 of them RLO's; RLO's own scene people kept
2993: traffic: ordinary native groups held for Persistent City cars; RLO staged accidents and reckless drivers stood down; cars loaded into held groups past the shares retired off screen; cars RLO holds are never retired
2994: [Mod:FoodTrucks] Test bridge: console 'pc capability status' (1 lines).
2996: [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 1 14:30.
2997: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2998: [Mod:PersistentCity] Road snapshot started: frame=8758, reason=clock-rate index=-1, scene searches in this planner=0.
2999: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 40 walker(s), 0 standing or sitting; the nearest: nobody within 12 m: PASS.
```

`check3-on-speed1-Player-excerpt.txt`:

```text
2921: [Mod:RealLifeOverhaul] Navigation: 27 m off the route for 2 s (on foot, 0 m along it): looking for a new route.
2922: [Mod:RealLifeOverhaul] Navigation: route found to 'Police incident' (on foot; off the route): 10 lane points, 47 m, 1 turns, 31 search steps in 1 slices, 0.02 ms in all (longest slice 0.02 ms).
2923: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: the game is busy (a pause, the time machine, a question, the activity panel, the hospital, the subway or the casino boat); out: 0 walker(s), 0 standing or sitting; the nearest: not looked for: PASS.
2924: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2925: [Mod:PersistentCity] Road snapshot started: frame=8568, reason=clock-rate index=-1, scene searches in this planner=0.
```

`check3-speed20-to1030-Player-excerpt.txt`:

```text
2425: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: the game is busy (a pause, the time machine, a question, the activity panel, the hospital, the subway or the casino boat); out: 0 walker(s), 0 standing or sitting; the nearest: not looked for: PASS.
2426: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: the game is busy (a pause, the time machine, a question, the activity panel, the hospital, the subway or the casino boat); out: 0 walker(s), 0 standing or sitting; the nearest: not looked for: PASS.
2427: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: the game is busy (a pause, the time machine, a question, the activity panel, the hospital, the subway or the casino boat); out: 0 walker(s), 0 standing or sitting; the nearest: not looked for: PASS.
2428: [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 1 10:30.
2429: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2430: [Mod:PersistentCity] Road snapshot started: frame=3619, reason=clock-rate index=-1, scene searches in this planner=0.
2431: [Mod:RealLifeOverhaul] Street people: besides the walking pedestrians, the standing and sitting people of 7 of the game's pools count as people on the street (BenchPedestrian, CasualStationaryAi, SportPedestrian, SwimmingPoolBaseHuman, StreetPerformer, Homeless, Golfer).
2434: [Mod:RealLifeOverhaul] Session check (work named for a long frame): the line of the frame of 166 ms leaves out 2 piece(s) of work begun after it, before the line was written; what the mod named up to its end is still named (a ring of 16): PASS.
2435: [Mod:RealLifeOverhaul] Meta: props notice shown: the props of reallife_props cannot be drawn: the game's lit shader is not supported on this graphics setting.
2436: [Mod:RealLifeOverhaul] Meta: Meta check (welcome notice): shown as the game's own notification with a click that opens the Guide on 'What changed': PASS.
2438: [Mod:RealLifeOverhaul] Traffic check (senses): cars sense layers [Player, Human, Vehicles]; pedestrians are on 'Human' with their capsule on; sensor box 4.5 m long on 'AiVehicles', which does not meet the pedestrians' layer in the physics settings; mode: stand-ins on 'Player' for everybody in a car's way.
2439: [Mod:RealLifeOverhaul] Traffic check (proxy): 0 of 16 in use: PASS
2440: [Mod:RealLifeOverhaul] Traffic check (cost): 1 looks in the last minute, 917 microseconds on average and 917 at worst (wanted: under 100 and 300); at most 2 cars looked at, 39 people in the grid, 0 of 16 stand-ins in use, 0 cars braked, 0 watched for contact now; mode StandIns: over budget; where it went, on average and in the worst look (microseconds): the cars gathered 28 and 28, the people 851 and 851, the strips and the decisions 28 and 28, the stand-ins and the fades 10 and 10; the diagnostics' own lines are not counted; people looked over in a look, at most: 40 walking and 0 standing or sitting, 0 of those near enough when last listed to be read; outside the looks, 6 car(s) measured in frames of their own (121 microseconds at most) and 0 that took the figures of a car of their kind.
2441: [Mod:RealLifeOverhaul] Car ownership: records read.
2442: [Mod:RealLifeOverhaul] City events: state loaded; day 1 of a 60-day year (Winter), today: nothing on.
2443: [Mod:RealLifeOverhaul] Civic: ready. Election 0 None, election day 15, approval 58, 6 site(s) in progress.
2444: [Mod:RealLifeOverhaul] Contacts check (picture): 'reallife-cityhall' is a logo, from the game's logo shape 'headquarter3', kept (its file read on a worker, 0.02 ms of the main thread, wanted under 3 ms); 27 contact picture(s): 9 face(s), 18 logo(s), 27 drawn: PASS
2445: [Mod:RealLifeOverhaul] Traffic check (city drivers' cost): 1 looks in the last minute, 177 microseconds a look on average and 177 at worst, over 12 cars a frame (wanted: under 150 and 600); at most 5 cars read, 6 things on the roads, 0 cars slowed at once, 0 hold line(s): FAIL, over budget; of it the street read and its cars gathered 98 on average and 98 in the worst look; outside the looks, 9 car(s) measured in frames of their own (125 microseconds at most) and 1 that took the figures of a car of their kind.
2446: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'VordV150' at 22 km/h began to slow for a red light 11 m ahead, needing 1.8 m/s2 to reach it at 0 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
2447: [Mod:RealLifeOverhaul] Map links: construction on the city map with 6 place(s).
2449: [Mod:RealLifeOverhaul] Map places: 1 row(s) added to the city map's filters (under special buildings).
```

`check3-off-toggle-excerpt.txt`:

```text
2658: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2659: [Mod:PersistentCity] Road snapshot started: frame=5760, reason=clock-rate index=-1, scene searches in this planner=0.
2660: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 40 walker(s), 0 standing or sitting; the nearest: nobody within 12 m: PASS.
2661: [Mod:RealLifeOverhaul] Delivery trips and food spoilage: stood down (the options are off); the game's own logistics run every route again.
2662: [Mod:RealLifeOverhaul] Suppliers: switched off; the contracts and what is owed are kept in the save and go on, each at its next delivery day, when it is on again.
2663: [Mod:RealLifeOverhaul] Rivals seen: switched off; what the save holds of the rivals is kept and goes on when it is on again.
2664: [Mod:RealLifeOverhaul] Flare check (the residents' save): 418 KB in 13.5 ms on 6 worker(s): PASS
2665: [Mod:RealLifeOverhaul] Flare in the city: stood down (the option is off); Flare's talk about a business is in business pressure's figure again.
2666: [Mod:RealLifeOverhaul] Population check (the save): 1307 KB in 50.1 ms on 6 worker(s): PASS
2667: [Mod:RealLifeOverhaul] Living population: stood down (the option is off); the game's own customers and the city economy's own figures stand.
2668: [Mod:PersistentCity] RLO Living population on -> Living population off during play; Persistent City keeps owning its shops, crowds and traffic.
2669: [Mod:RealLifeOverhaul] Session: the managed heap grew by 30 MB in one frame of 198 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "flare: the hour's milestones" 15.4 s before, "flare: the hour's words asked ahead" 15.5 s before, "flare: the hour's unread count" 15.5 s before; on foot in the street, game hour 10.
2670: [Mod:PersistentCity] Slow routing frame=5838, realtime=170.213, prepared=False, preparation_ms=28.894, closure_ms=0.000, last_rebuild=clock-rate index=-1.
2671: [Mod:RealLifeOverhaul] Session: the managed heap grew by 13 MB in one frame of 33 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "flare: the hour's milestones" 16.9 s before, "flare: the hour's words asked ahead" 16.9 s before, "flare: the hour's unread count" 17.0 s before; on foot in the street, game hour 10.
2672: [Mod:RealLifeOverhaul] Session: the managed heap grew by 11 MB in one frame of 34 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "flare: the hour's milestones" 16.9 s before, "flare: the hour's words asked ahead" 17.0 s before, "flare: the hour's unread count" 17.0 s before; on foot in the street, game hour 10.
2673: [Mod:RealLifeOverhaul] Session: the managed heap grew by 16 MB in one frame of 34 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "flare: the hour's milestones" 17.0 s before, "flare: the hour's words asked ahead" 17.0 s before, "flare: the hour's unread count" 17.0 s before; on foot in the street, game hour 10.
2674: [Mod:PersistentCity] Slow routing frame=5863, realtime=171.112, prepared=True, preparation_ms=0.238, closure_ms=157.293, last_rebuild=clock-rate index=-1.
2675: [Persistent City] RLO traffic scenes that take over native cars stood down while Persistent City cars hold traffic (2 of 2 switches, a scene under way ended).
2676: [Persistent City] Ordinary ambient pedestrian requests: suppressed; RLO scenes and global spawning retained.
2677: [Persistent City] Stationary crowds: the game's anonymous standers are off (574 zones, 115 of them RLO's, 0 in view cleared); street performers and the homeless kept; RLO's own scene people untouched.
2678: [Mod:PersistentCity] Physical population active: ordinary pedestrian and vehicle spawns replaced by own bodies and own cars; RLO scenes, customers, workers, taxis and service vehicles retained.
2679: [Mod:RealLifeOverhaul] Session: the managed heap grew by 23 MB in one frame of 33 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "flare: the hour's milestones" 17.5 s before, "flare: the hour's words asked ahead" 17.6 s before, "flare: the hour's unread count" 17.6 s before; on foot in the street, game hour 10.
2680: [Mod:RealLifeOverhaul] Session: the managed heap grew by 8 MB in one frame of 335 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "flare: the hour's milestones" 17.6 s before, "flare: the hour's words asked ahead" 17.6 s before, "flare: the hour's unread count" 17.6 s before (1 begun after it left out); on foot in the street, game hour 10.
2681: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 15 walker(s), 0 standing or sitting; the nearest: nobody within 12 m: PASS.
```

`check3-on-toggle-excerpt.txt`:

```text
2924: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2925: [Mod:PersistentCity] Road snapshot started: frame=8568, reason=clock-rate index=-1, scene searches in this planner=0.
2928: [Mod:RealLifeOverhaul] Delivery trips and food spoilage: started with 0 route(s) kept, 0 trip(s) on the road or waiting, 0 run(s) in the week's log and 1 place(s) with goods that spoil.
2929: [Mod:RealLifeOverhaul] Suppliers: read the save's line (version 1): 0 contract(s) running, $0.00 owed.
2930: [Mod:RealLifeOverhaul] Rivals seen: read the save's line (version 1): 19 rival(s) on record, 1 sighting(s) kept.
2931: [Mod:RealLifeOverhaul] Rivals seen: 1 sighting(s) went out at 12:00 on day 1, 1 of 3 posts today.
2932: [Mod:PersistentCity] RLO Living population off -> Living population on during play; Persistent City keeps owning its shops, crowds and traffic.
2933: [Mod:RealLifeOverhaul] Session: the managed heap grew by 19 MB in one frame of 34 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "the game is saving (the mod writes its state into the save first)" 8.1 s before, "crime: a scene starting" 15.4 s before, "crime: a scene starting" 15.4 s before; on foot in the street, game hour 12.
2934: [Mod:RealLifeOverhaul] Game UI: the game's app icon could not be read back, the glyph alone is used.
2935: [Mod:RealLifeOverhaul] Game UI: app 'RealLifeParties' is on the phone. Button copied from appButtonTemplate (HUD) and appButtonTemplate (full menu); icon Icon-People; it goes into the grids with the others of its line, after the last; its panel is built when it is first opened.
2936: [Mod:RealLifeOverhaul] Game UI: the game's app icon could not be read back, the glyph alone is used.
2937: [Mod:RealLifeOverhaul] Game UI: app 'RealLifeSuppliers' is on the phone. Button copied from appButtonTemplate (HUD) and appButtonTemplate (full menu); icon Icon-Box; it goes into the grids with the others of its line, after the last; its panel is built when it is first opened.
2938: [Mod:RealLifeOverhaul] Phone apps check (paced install): 3 item(s) over 3 frame(s), 2.8 ms in all; the slowest frame 1.06 ms, the slowest item 1.06 ms (RealLifeSuppliers); wanted: no frame over 2 ms; at most 1 app(s) put on in a frame (wanted 1); the phones searched 0 time(s) for 2 name(s) asked (wanted at most 1); the pages laid out 0 time(s) while they went on (wanted 0), then once on each phone (34 apps on the HUD phone, 3 page(s); the full menu's row 3 page(s)): PASS.
2939: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'VordTiaraVic' at 34 km/h began to slow for a red light 36 m ahead, needing 1.3 m/s2 to reach it at 0 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
2940: [Mod:RealLifeOverhaul] Session: the managed heap grew by 20 MB in one frame of 35 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "the game is saving (the mod writes its state into the save first)" 8.7 s before, "crime: a scene starting" 16.0 s before, "crime: a scene starting" 16.0 s before; on foot in the street, game hour 12.
2941: [Mod:RealLifeOverhaul] Session: the managed heap grew by 17 MB in one frame of 34 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "the game is saving (the mod writes its state into the save first)" 8.8 s before, "crime: a scene starting" 16.0 s before, "crime: a scene starting" 16.0 s before; on foot in the street, game hour 12.
2942: [Mod:RealLifeOverhaul] Session: the managed heap grew by 21 MB in one frame of 46 ms; under the mod's own scripts: 4 KB (Camera: ThirdPersonCamera.Update 4 KB); the rest is the game's, or another mod's; the mod's last pieces of work: "the game is saving (the mod writes its state into the save first)" 8.8 s before, "crime: a scene starting" 16.1 s before, "crime: a scene starting" 16.1 s before; on foot in the street, game hour 12.
2943: [Mod:RealLifeOverhaul] Navigation: 27 m off the route for 2 s (on foot, 0 m along it): looking for a new route.
2944: [Mod:RealLifeOverhaul] Navigation: route found to 'Police incident' (on foot; off the route): 10 lane points, 47 m, 1 turns, 31 search steps in 1 slices, 0.02 ms in all (longest slice 0.02 ms).
2945: [Mod:RealLifeOverhaul] Population check (the customers' count at the load): the factory hands out the mod's calculators: PASS
2946: [Mod:RealLifeOverhaul] Living population: loaded (version 5) on day 1: 19738 households, 56356 people in 1426 buildings.
2947: [Mod:PersistentCity] Slow routing frame=8656, realtime=279.088, prepared=True, preparation_ms=0.236, closure_ms=144.560, last_rebuild=clock-rate index=-1.
2948: [Persistent City] RLO traffic scenes that take over native cars stood down while Persistent City cars hold traffic (2 of 2 switches, a scene under way ended).
2949: [Persistent City] Ordinary ambient pedestrian requests: suppressed; RLO scenes and global spawning retained.
2950: [Persistent City] Stationary crowds: the game's anonymous standers are off (574 zones, 115 of them RLO's, 0 in view cleared); street performers and the homeless kept; RLO's own scene people untouched.
2951: [Mod:PersistentCity] Physical population active: ordinary pedestrian and vehicle spawns replaced by own bodies and own cars; RLO scenes, customers, workers, taxis and service vehicles retained.
2952: [Mod:RealLifeOverhaul] Session: the managed heap grew by 32 MB in one frame of 35 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "the game is saving (the mod writes its state into the save first)" 9.3 s before, "crime: a scene starting" 16.6 s before, "crime: a scene starting" 16.6 s before; on foot in the street, game hour 12.
2954: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 33 walker(s), 0 standing or sitting; the nearest: nobody within 12 m: PASS.
2956: [Mod:RealLifeOverhaul] Flare in the city: read (version 1): 992 accounts, 0 measured posts, 0 ads; the residents' day runs in slices from now.
2957: [Mod:RealLifeOverhaul] Flare in the city: day 1, 56356 residents' follows read back: 34 residents follow you, 0 reviews by customers, 0 new followers of businesses from their visits, 6 ad(s) shown, 0 post(s) heard, 0 voice(s) new; Flare's attention brought 0 of takings yesterday.
2958: [Mod:RealLifeOverhaul] Flare check (the residents' day): 34 residents follow you against the account's city share of 34 (43040 adults), 136 businesses with attention for tomorrow (11 thousandths on average), 0 of takings moved yesterday: PASS
2960: [Mod:RealLifeOverhaul] People: first runner of the session (Jog): asked for 4.0 m/s, reached 4.0 m/s and covered 10.3 m in 2.7 s; acceleration 14 m/s2 (the game's own 8), turning 360 deg/s (the game's own 120), gives way at priority 15, pavement agent, animator 'CharacterAnimator'.
```

Clock completion and degraded-retime lines from all full logs:

```text
check1-final:2672: [Mod:FoodTrucks] Test bridge: run_until day 1 10:30 reached at day 1 10:30; paused, speed 1.
check2b-final:2066: [Mod:PersistentCity] Clock retime degraded 1 journeys: each keeps its old pace on its current road edge or drives outside traffic until its next road section.
check3-final:2625: [Mod:FoodTrucks] Test bridge: run_until day 1 10:30 reached at day 1 10:30; paused, speed 1.
check3-final:2721: [Mod:PersistentCity] Clock retime degraded 1 journeys: each keeps its old pace on its current road edge or drives outside traffic until its next road section.
check3-final:2871: [Mod:FoodTrucks] Test bridge: run_until day 1 12:30 reached at day 1 12:30; paused, speed 1.
check3-final:3154: [Mod:FoodTrucks] Test bridge: run_until day 1 14:30 reached at day 1 14:30; paused, speed 1.
```


Both headless setups repeated 08:00 clock readings, then advanced after an explicit same-speed `pause:false` retry; no cause is inferred. Its second speed-20 log line is also retained verbatim:

```text
2563: [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 1 10:30.
```

### Logged durations of 30 ms or more

No perf window, GC sampler, mod-work sampler or save profiler was armed. Only already emitted PC log durations were inspected; these are not p95 measurements or a timing gate. Aggregate/maximum fields on one log line can describe overlapping work and are not independent spike events.

| Process log | Line | Reported ms values at least 30 |
|---|---|---|
| `check1-final-Player.log` | 2399 | 601 |
| `check1-final-Player.log` | 2430 | 138.523, 138.523, 138.376, 118.031, 187.143, 125.616 |
| `check1-final-Player.log` | 2543 | 3557.040, 136.719, 136.717, 187.143, 31.968, 3320.256 |
| `check2-final-Player.log` | 1692 | 601 |
| `check2-final-Player.log` | 1721 | 141.643, 141.643, 141.485, 120.765, 227.103, 128.823, 30.877, 30.877 |
| `check2-final-Player.log` | 1873 | 1013.691, 139.487, 139.484, 227.103, 560.337, 103.958, 491.590 |
| `check2-final-Player.log` | 1994 | 4235.352, 227.103, 1293.234, 154.076, 4010.874 |
| `check2-final-Player.log` | 2153 | 1772.778, 40.925, 227.103, 1829.786, 216.511, 1446.327 |
| `check2b-final-Player.log` | 1688 | 601 |
| `check2b-final-Player.log` | 1711 | 144.766, 144.766, 144.602, 121.587, 217.058, 131.001, 32.025, 32.025 |
| `check2b-final-Player.log` | 1828 | 1919.786, 145.466, 145.464, 217.058, 715.933, 98.993, 1395.213 |
| `check2b-final-Player.log` | 1920 | 3903.212, 217.058, 1269.306, 150.772, 3671.550 |
| `check2b-final-Player.log` | 2040 | 1611.571, 42.828, 217.058, 1850.484, 190.646, 1245.211 |
| `check3-final-Player.log` | 2388 | 601 |
| `check3-final-Player.log` | 2423 | 135.620, 135.620, 135.471, 128.051, 209.479, 135.878 |
| `check3-final-Player.log` | 2536 | 3596.458, 135.575, 135.573, 209.479, 38.882, 3322.466 |
| `check3-final-Player.log` | 2737 | 2479.368, 209.479, 1744.508, 157.293, 2106.248 |
| `check3-final-Player.log` | 2818 | 5794.997, 209.479, 2103.403, 5461.415 |
| `check3-final-Player.log` | 3060 | 2492.885, 62.414, 209.479, 1900.764, 144.560, 2127.974 |
| `check3-final-Player.log` | 3145 | 5777.878, 92.920, 209.479, 2109.712, 5505.244 |

### NOT RUN grouped

1. **#244 P-D check 7:** Rig bot console-guard fix agent-rig #85 remains open; no non-FT console attempt in this run.
2. **GR-5 GC/street re-checks:** #254 measured by rig 14b, PR #256; next step pending owner route. No 600-second performance window in this run.
3. **#254 check 4 appearance:** rig 14b tooling gap, reacquired-resident identity; sent to the Rig bot.
4. **GR-5 check 4 footprint:** rig 14b building op returned `ops_game_api_unavailable`; sent to the Rig bot. Not retried here.
5. **GR-5 60 fps arms:** rig 14b Mac cadence 5 fps; sent to the Rig bot. Temporary 30-fps screenshot configuration is not 60-fps evidence.
6. **GR-5 save latency:** owner decision pending. Manual functional checkpoints are not latency measurements.

### Tooling gaps and recovered input errors

- Still captures do not establish continuous visible no-jump proof; check 2 is partial.
- Exact rejected operation: `call shoot` with `frames:1`, including the initial and first 1 → 3 pair attempts. Error: `frames must be a whole number from 8 to 240.` Corrected to frames=8. No additional runtime-stop failure is attributed to it.
- First camera x=176,y=18,z=-56 returned `pose_note: the camera stands inside something solid (a building, a prop or a vehicle); the picture may be dark`. Images show building walls and cannot support a visual pass. Corrected to the documented street camera and repeated cold.
- Optional `call ui {}` returned `Missing "op": list, click, set, escape or tree.` Corrected to `call ui {"op":"list"}`; no native UI state was changed by the read.
- There is no commit-stamped PC build log. The staged DLL hash and receipt establish the tested build; activation lines establish the loaded runtime.

### Fixtures and restoration

Reused only `FT RLO 13d Food v1`. Created disposable manual checkpoints: `FT RLO 14c Retime 1030`, `FT RLO 14c Off 1230`, `FT RLO 14c On 1430`. No existing fixture was overwritten; these saves remain isolated. No throwaway non-FT city was needed.

The helper printed this rollback verbatim:

```sh
rm -rf "<rig-profile>/ModsLocal/PersistentCity" && cp -Rp "<rig-tools>/receipt-backups/20261114T134245Z/mod" "<rig-profile>/ModsLocal/PersistentCity" && cp "<rig-tools>/receipt-backups/20261114T134245Z/install.json" "<rig-tools>/install.json"
```

The execution layer rejected the printed rollback before executing it: `rm -f style commands are not permitted. Use a safer approach`. With the game stopped and the lease held, the safe equivalent moved the staged PersistentCity folder intact to `<stage>/retained-staged-mod`, copied the helper’s original `mod/` into the empty live path, then copied its original install.json. No receipt was hand-edited. Changed original profile files and the plist were restored from the independent backup. All 39 original file/absence hashes match, the mod file set has no extra or missing file, all 53 RLO file hashes match, and the full install receipt validates. AgentRigOps retains its original SHA-256. The RLO op restore independently returned stored=null/live=true/original_saved=false before shutdown. All four game stops were graceful; no force. Final status: running=false, process=null, lease held=false, pending=0. No process started for this run remains running. Frame-cap config is back to its original absence; native autosave preferences are restored, and process-only suppression ended at stop. The official agent-rig checkout remains clean.


Validation: candidate build passed. `git diff --check`, markdown ban and docs-links passed for the docs-only result. Only the existing checklist was changed; no raw evidence, binary, save or new markdown path is committed. The build/evidence worktree is removed after commit/push and PR creation. The PR is left open, unmerged, without reviewers.

## Rig results — 14 November 2026 e, PC (#244 P-D check 7 at 2448bc40)

**RESULT: BLOCKED.** Counts: **0 passed / 0 partial / 0 failed / 9 NOT RUN arms**. The required P-D check 7 is NOT RUN: the single default Windows start failed before launching a player. The other eight arms were excluded by the run scope. This is no in-game proof and gates no merge.

### Build and rig

| Item | Evidence |
|---|---|
| Role / instance | rig_test, Windows PC, agent1; owner `pc-rig-2026-11-14e` throughout. No Mac contacted. |
| Candidate | `2448bc40f47c67c0a84813e0e4f6a9b5c58dd9fe`, fetched `origin/codex/persistent-city`. Moved from requested minimum `913abd7d` by docs-only PR #258. |
| Worktrees | New branch `codex/pc-rig-2026-11-14e` in `rig-2026-11-14e-pc-evidence`; detached build in `rig-2026-11-14e-pc`. Original checkout stayed clean. |
| Agent-rig | Official checkout `4bcf80aa04431f3b54cac19335c9c4cbb57a4b48`; clean before fetch and `git pull --ff-only` (`Already up to date.`). No checkout edit or overlay update. |
| Live AgentRigOps.dll SHA-256 | `fe4c40c1ba70512eb0b14704e275c1fa4842e98100ba563cc2d559a13be6bf6a`; unchanged after cleanup. |
| Build | `python tools/build.py --game-managed "C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"` passed. PATH prefixed with `%LOCALAPPDATA%\Microsoft\dotnet`; wait-other-dotnet helper printed `dotnet lock removed 2026-11-10; continuing`. No offline test suite. |
| PersistentCity.dll SHA-256 | `087fe9aa8c506c3d2fa43df62fec0f08450095ca415f7c3ffebf64bfd28bde29`; built package and copied candidate matched. No loaded-build line: player never started. |
| Package / raw evidence | `<stage>`, the run's local staging folder `persistent-city-rig-2026-11-14e-pc`; package under `<stage>/package/PersistentCity`. Build/start output, pre-stage manifest, backups, retained candidate and restore verification stay outside Git. No RLO package files copied. |
| Staging command | `python <stage>/backup-and-stage.py`: full pre-copy backup, then hash-verified `shutil.copytree(package, isolated ModsLocal/PersistentCity, dirs_exist_ok=True)`. Windows scoped receipt-refresh helper was not found; used the authorized copy fallback. No receipt hand-edit. |
| Backup / rollback source | `<stage>/pre-stage-backup`. Restoration used this run's `PersistentCity` folder and independently backed-up bytes, via `python <stage>/restore.py`. |
| RLO version / PC startup | NOT OBSERVED: no player launched and no current startup lines exist. Prior log contents are not evidence for this build. |
| Machine load | Setup observation: CPU 32%; physical memory 66,938,060 KiB total / 25,647,708 KiB free (about 63.84 / 24.46 GiB). No game process observed. Owner-reported live harness `20261114-144821-pc-fix-gr5-spawn-appearance` remained independent; no wait for load, cancellation, debugging or access to its branch/files. |
| Cap health | Local launcher status after cleanup: `running=false`, `pid=null`, `health=null`. No frame-cap health readback was available. Background default was requested; no windowed/60-fps start or frame-cap file change. |

### P-D check 7 and exact tooling errors

Read PR #244 and PR #246 through REST and the prior 14a / 14b / 13g results. The intended fixture was unsaved non-FT `RIGTMP-PD7-14e`. Agent1 was free and acquired without takeover at `2026-11-14T14:35:54.7615080Z`.

The single start attempt in the official agent-rig checkout, with `BA_LOCK_OWNER=pc-rig-2026-11-14e`, was:

```text
python scripts/agent start
ssh: Could not resolve hostname homepc: No such host is known.
START_EXIT_CODE=1
```

The CLI uses its SSH transport for lifecycle operations. Its earlier read-only `python scripts/agent status` produced the same exact hostname error. No SSH configuration or CLI was changed. No retry, `--rig-test` start, direct launch or FT fallback was attempted after the failed start, as required by the one-attempt rule.

| P-D 7 evidence | Result |
|---|---|
| `call new_game {"name":"RIGTMP-PD7-14e","throwaway":true}` | NOT ATTEMPTED: no player. No new_game refusal is claimed. |
| `call console {"line":"pc gc population"}` | NOT ATTEMPTED. Exact mod console output unavailable. |
| `call console {"line":"pc gc population 3000"}` | NOT ATTEMPTED. Expected substring `refused: measurement mode needs a disposable FT city` was not observed. No `MEASUREMENT MODE` output is claimed. |
| Persons before / immediately after / after one in-game hour | Unavailable / unavailable / unavailable. No city ran; no hour elapsed. |
| Matching Player-agent.log lines | None for this run. Existing log stayed 86,702 bytes with last write `2026-11-14T14:01:58.840534Z`, before the lease/start. No stale startup/refusal lines substituted. |
| Autosave | Complete original isolated preference/config bytes backed up and matched after cleanup. Numeric native autosave setting was not identified from offline settings; runtime OFF could not be verified because startup failed. No city or timed window ran and no autosave setting was changed. |

### Findings and tooling gaps

The observed blocker is default PC CLI transport, not a measurement-mode refusal in the mod. Agent-rig #85 is present in the official checkout and the live Ops hash is recorded, but this run did not exercise its RIGTMP console guard.

There is also a staging limitation: the Windows checkout has no scoped package/receipt refresh comparable to the Mac helper. The authorized copy fallback verified all six package files, but left the original receipt unchanged. The copied `PersistentCity.dll`, `persistent-city.json` and `README.md` differ from the original pinned receipt. A receipt-validated local launch was not attempted after the transport failure; this is a pending staging requirement, not a second observed launch error. No receipt protection was bypassed.

The documentation still requires a rig-test launch for throwaway `new_game` and console acceptance. This run obeyed the default-start-only instruction and did not retry the previously reported unsupported flag. The earlier `agent: error: unrecognized arguments: --rig-test` is background from rig 14d, not an error reproduced here.

### NOT RUN grouped

Nine counted arms, with no product FAIL recorded:

1. **Required P-D 7 (one arm):** default start failed with the exact SSH hostname error above; console refusal and unchanged-person proof unavailable.
2. **GR-5 60 fps (two arms: street / busy shop):** excluded; rig 14d / PR #257 found Windows CLI refuses `--rig-test` and no Windows scoped package stage exists. Rig bot request pending. No timing or cap proof attempted.
3. **GR-5 check 4 footprint (one arm):** excluded; previous building op `ops_game_api_unavailable`, Rig bot fix pending. Not retried here.
4. **#254 check 4 appearance (one arm):** excluded; no stable resident identity through release and reacquire. No appearance attempt.
5. **GR-5 check 2 save latency (one arm):** excluded; owner save-route decision pending. No save or save profiling.
6. **#255 checks 1–3 (three arms):** excluded; covered by Mac rig 14c / PR #258. The zero-receipt finding has a fix run. No rerun or Mac contact.

### Fixtures, restoration and validation

Created fixtures: **none**. `RIGTMP-PD7-14e` was planned but never created; no save, checkpoint, fixture removal or existing save overwrite. No game process was started, so no stop/force-stop was needed and no owned game process remains.

The candidate folder was moved intact into this run's `retained-staged-PersistentCity`; the original folder was copied back from this run's pre-stage backup. Original receipt, preferences and configuration were preserved or restored only from this run's manifest. Cleanup verified **101 checks, zero mismatches**: 47 original file hashes, two expected absences (`agent-frame-cap.json` and root `persistent-city.json`), the isolated registry export, complete 41-file PersistentCity set, 48 RLO hashes read-only, unchanged Ops hash and unchanged log length. Registry export SHA-256: `8adb52a0d6a12694aa30f2ae209f94149e519240f3b32d97c11e2d6b389f9e4c`. RLO, Food Trucks and the overlay were never written.

Released with `python scripts/local_lock.py release`. Final local lease status: `held=false`, `owner=null`, `pending=0`. Direct read-only installed-launcher status: `installed=true`, `running=false`, `pid=null`, `health=null`. No other session's process was stopped. No normal game/profile or bridge ownership was used.

Only this existing checklist is changed. Build passed; docs-links, markdown-ban and whitespace checks are recorded in the results PR. Report copy: `<stage>/../../Coding_Projects/.agent-logs/persistent-city-rig-2026-11-14e-pc.md`, a path relative to `<stage>`; the report stays in the agent-log folder outside the stage. Build worktree is removed after commit; evidence worktree after opening the unmerged docs-only results PR. No production change, issue, release, version/changelog change or merge.

## Rig results — 14 November 2026 f, PC (#244 P-D 7 + #246 check 4 footprint + GR-5 60 fps arms at 0795aa2c)

Role `rig_test`, where `pc`, isolated `agent1`. **Counts: 0 passed / 0 partial / 0 failed / 11 NOT RUN. RESULT: BLOCKED.** Four required arms are waiting on Rig run `20261114-154927-rig-windows-pc-cli-stage`; seven excluded arms are counted separately below. The single installed-player start/status probe succeeded and is outside the product-check counts. In-game proof gates no merge.

### Build and rig

| Item | Evidence |
|---|---|
| Candidate | `0795aa2c503869e5ab1feb91bc0738e2a163bfb9`, newer than required `b243c43b19f374b982c38d8e96633aa4f8f2ee90` by ancestry check. Initial and final fetch agree. This is the base tip, without the unmerged spawn_appearance fix. |
| Worktrees | Results branch `codex/pc-rig-2026-11-14f` in `rig-2026-11-14f-pc-evidence`; detached build in `rig-2026-11-14f-pc`. Original checkout unchanged. |
| Official agent-rig | `022630f7d6df8c533a230ac218413a68b181ed95`; clean, fetched, ff-only pull already current, clean after use. No edit, commit, push or overlay update. |
| Live AgentRigOps SHA-256 | `9a8ad56e01df5d30920da35f5ba7f60e65009528b9c1abf3d5e8667bb1a2378b`; independently hashed before the run, health reports ready with that hash, unchanged at cleanup. |
| Lease | Stable owner `pc-rig-2026-11-14f`; acquired while free at `2026-11-14T15:02:18.4755551Z`, no takeover. Released after stopped-player restoration. |
| Build command | `python tools/build.py --game-managed "C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"`, exit 0. PATH prefixed with `%LOCALAPPDATA%\Microsoft\dotnet`; required wait helper printed `dotnet lock removed 2026-11-10; continuing`. No offline test suite. |
| Built/package PersistentCity DLL SHA-256 | `087fe9aa8c506c3d2fa43df62fec0f08450095ca415f7c3ffebf64bfd28bde29`; matching package retained outside Git. No RLO or game DLL packaged. |
| Installed PersistentCity DLL SHA-256 | `6e0e5b58776a5a4430b4b585e03f7233d1dc5dedfd94200cb7f603a44ea55de1`, mtime 11 November 2026 16:53:38 local. Matches its existing receipt entry; too old for all four required arms. |
| Installed receipt SHA-256 | `agent-install.json`: `7f4f73e05d6ee2eb70c31987f6a3f6c70ece33f4119f5fe61ad0e57aebad1761`, matching launch input and post-cleanup backup. |
| Staged DLL / staging command | **None / skipped.** One search after official fetch/pull found scoped stage/refresh only in Mac `install-mac.py`, documented in `docs/MAC.md`. Windows has no documented scoped receipt-safe helper. The whole-modset transaction is not a PersistentCity-only receipt refresh. No manual copy fallback, receipt hand-edit or unmerged tooling branch was used. Waiting on `20261114-154927-rig-windows-pc-cli-stage`. |
| Stage / backup | Package in `<stage>\package\PersistentCity`, this run's exact-byte backup in `<stage>\pre-stage-backup`, independent manifest `<stage>\manifest.json`. |
| RLO probe log | `[Mod:RealLifeOverhaul] Real Life Overhaul 0.45.0 initialised.` This is the installed probe's observed version. RLO was not changed. |
| PC probe log identity | No Persistent City build/activation line emitted during the menu-only probe. Health includes the loaded `PersistentCity` assembly; the installed DLL hash and launch receipt establish binary identity. No candidate or city activation is claimed. |
| Food Trucks | Health `mod_version=0.10.2`, bridge version 1. No update. |
| Autosave | Numeric native interval/count not obtained in the menu-only probe; no matching autosave-named registry value found. Complete isolated registry/preferences backed up. Probe health `rig_test.enabled=true`, `autosave_disabled=true`, `save_specific=false`; process-only suppression ended at stop. No city or measured window, no persistent autosave setting changed. |
| Frame-cap file | Original absence backed up and preserved; no request staged under the version gate. |
| Machine load | Setup only at `2026-11-14T15:03:43.8482826Z`: CPU 69%; RAM 66,938,060 KiB total / 26,991,224 KiB free (63.84 / 25.74 GiB). Notable working sets: unrelated game 6.74 GiB, WSL 6.26 GiB, memory compression 5.27 GiB, browser and other agent sessions. No measurement-start sample exists. Future timing on this host needs **provisional (PC shared)**. |

Read PR #244 and #246 through REST, prior 14a/14b/14d/14e checklist sections, and the official Windows/ops/throwaway/frame-cap documentation. No Mac contact, lifecycle CLI through `scripts/agent`, real mouse/keyboard action, Windows setting change, normal profile access or other session intervention occurred.

### Required checks

| Arm | Outcome | Missing evidence |
|---|---|---|
| #244 P-D 7, ordinary saves refused | **NOT RUN / version gate** | No `new_game`, `pc gc population` or `pc gc population 3000` call. Exact console refusal and matching log lines unavailable; persons before/immediately after/after one game hour all unavailable. |
| #246 check 4, unrentable home footprint | **NOT RUN / version gate** | No `op building {"action":"list"}` or building reads attempted. No current-run op error, three targets, rotated/yard/plain comparison, Home area, height or storey evidence. Prior fixed building-tool claims are not reused as this run's proof. |
| GR-5 street, cap 60, 600 s | **NOT RUN / version gate** | Measured fps, whole-frame p95/p99, gen0, pause p95, heap, UpdateCity/street mod-work p95 and every >=30 ms spike unavailable. No measurement window. |
| GR-5 busy shop, cap 60, 600 s | **NOT RUN / version gate** | Same unavailable metrics; no city or busy-shop setup. No measurement window. |

All four wait on Rig run `20261114-154927-rig-windows-pc-cli-stage`. No old-build measurement is substituted for the required pre-fix baseline.

### PowerShell route probe and exact tooling observations

With the lease held and `BA_LOCK_TOKEN` set privately from this owner's verified credential, one foreground start ran:

```powershell
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "C:\BigAmbitions-AgentTests\Agent-Windows.ps1" -Action start -Mode windowed -RigTest
```

Exit 0; launched only isolated PID **25848**. Status through the same PowerShell launcher returned running=true, scene=menu, session `7705384b`, Ops ready. No city was created or loaded. The startup log identifies isolated agent1, windowed, runtime 7, audio muted, Steam disconnected. Raw evidence: `<stage>\start.log`, `probe-status.json`, `probe-Player-agent.log`.

Probe cap health, not a 60 fps arm: `requested=null`, `applied=false`, `vsync_count=0`, `target_frame_rate=90`, `measured_fps=66.36`, `window_s=2.0`, `error=null`. No 60 fps compliance or product performance pass is claimed from menu cadence.

Graceful `-Action stop` exited 0: `Quit requested for the verified agent process`. Immediate status still showed quit pending; the first lease release returned exactly **`Commands or lifecycle work are still pending; wait for completion before releasing the lock`**. It was not forced or broken. After process exit, launcher status confirmed running=false/pid=null/health=null, restoration was verified again, and release succeeded with pending=0. No force-stop or second launch.

Recovered local inspection errors: `rg` was unavailable, so searches used PowerShell; registry `Get-ItemProperty` returned `Specified cast is not valid.`, so complete `reg export` and native registry enumeration were used; an initial read targeted the `tools/docs-links` directory and returned access denied, corrected to `tools/docs-links/check.py`. These were local read errors, not product/op failures.

### NOT RUN grouped

1. **Required version-gated group, four arms:** P-D 7, footprint check 4, street 60 fps and busy-shop 60 fps. Receipt-safe Windows stage missing; waiting on `20261114-154927-rig-windows-pc-cli-stage`.
2. **Appearance, one arm:** #254 check 4; no stable resident identity through release/reacquire, identity op still queued.
3. **Save latency, one arm:** GR-5 check 2; owner save-route decision pending. No save profiling.
4. **Retime, three arms:** #255 checks 1–3 / #239-3 already covered by rig 14c / PR #258; zero-receipt finding has a fix run.
5. **30 fps, two arms:** street and busy shop already attempted by rig 14b; this is the Windows 60 fps run.

### Fixtures, restoration and validation

Fixtures created/loaded/saved: **none**. Planned names `RIGTMP-PD7-14f` and `RIGTMP-GR5-14f` were not created. No save/load, fixture removal, existing save overwrite or native sales occurred.

Before startup, this run backed up the entire isolated PersistentCity file set, isolated root settings/configuration, persistent-city.json bytes/absence, frame-cap absence, install receipts/run receipt and complete isolated registry export. No candidate was staged. Post-exit restoration used only this run's backups; file bytes/absences and registry preferences match. **120 verification checks, zero mismatches**, including full PersistentCity file set, read-only RLO/Food Trucks hashes and the Ops DLL. Registry export SHA-256 `d44a9390f89ea5d15b98020c6901b32e226fcd0bca76be6009f4fabcfb93440a`. The generated probe log is retained as evidence, not rolled back. No RLO, Food Trucks or overlay file was written.

Final launcher status: **installed=true, running=false, pid=null, health=null**. PID 25848 no longer exists. Final agent1 lease **held=false, owner=null, pending=0**; no process started by this run remains. Release followed post-exit re-verification. Build worktree removed after the evidence commit; evidence worktree removed after PR creation.

Validation: build passed; docs-links, markdown-ban and `git diff --check` run for this existing-file-only docs change. No offline dotnet tests or regression suites. Report copy: `<home>\Coding_Projects\.agent-logs\persistent-city-rig-2026-11-14f-pc.md`. Raw evidence/backups stay outside Git. No issue, merge, release, Workshop upload, version/changelog or production code change. Schema 1 and both food-choice weights unchanged.

**RESULT: BLOCKED.**

## Rig results — 14 November 2026 f, Mac (#244 P-D 7 + #246 checks 4-5 + #260 checks 1-3 at 2f5e72eb)

**RESULT: DONE. Counts: 4 passed / 1 partial / 0 failed / 5 NOT RUN.** Six required checks: 4 passed, 1 partial, 0 failed and 1 NOT RUN. Four owner-excluded groups are also NOT RUN, giving five NOT RUN report items. In-game proof gates no merge; this is an unmerged docs-only evidence PR.

Role `rig_test`, instance `agentmac`, where `mac`; owner `persistent-city-rig-2026-11-14f-mac`. Raw evidence and independent backups: `<stage>` = `<home>/HART-Builds/persistent-city-rig-2026-11-14f-mac`. Report copy: `<home>/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-14f-mac.md`. All commands ran in the foreground and were awaited. No Windows connection, ordinary profile, Steam app, ordinary save, other session, release, merge, version/changelog or production-code change. Schema remains 1; FoodChoicePriceWeight 1.2 and FoodChoiceHouseholdWeight unchanged.

### Build and rig

| Item | Evidence |
| --- | --- |
| Candidate | `2f5e72eb5b51ad2449bfb692983ff7ce9cf062db`, exactly origin/codex/persistent-city at initial fetch. Final remote tip is recorded below. |
| Worktree | New `codex/mac-rig-2026-11-14f` in `rig-2026-11-14f-mac-evidence`; one isolated worktree served build and evidence. Original checkout stayed clean. |
| Build | `python3 tools/build.py --game-managed "<home>/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0, Homebrew PATH and DOTNET_ROOT. No full test suite. |
| PersistentCity DLL SHA-256 | `bc3a0bac4357d8c4e9435b8e506fc4c839d013440202fddb5c61c523a09b3e2f`; built, packaged and staged hashes match. Every start validated the install receipt. |
| Official agent-rig HEAD | `022630f7d6df8c533a230ac218413a68b181ed95`, clean; fetch and ff-only pull were already up to date. No edits, commit, push or overlay update. Includes #85 and #86. |
| Live AgentRigOps SHA-256 | `154be3036954c314d3bd9eb4409da98f6a9b3a508748f2b1811c457abce07955`, unchanged while running and at cleanup. Live ops ready. |
| RLO startup | `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` All 53 RLO package files hashed unchanged. No RLO install/edit. |
| PC log identity | A: `Persistent population active: 1000 residents, 1000 visitors, 1686 places.` B: `1000 residents, 2000 visitors, 1683 places.` C: `1611 residents, 1611 visitors, 1685 places.` Full lines below. No commit-stamped build line is emitted; the matching live DLL hash and validated receipt establish binary identity. |
| Lease / resources | Initially stopped/free, acquired `2026-11-14T15:52:58.0952490Z`. Startup resources 67.9 GiB disk / 76% reported memory free; vm_stat free+inactive+speculative about 6.76 GiB. No resource refusal, takeover or other-process termination. |
| Starts / stops | Three cold processes, one attempt each: A headless, B windowed, C headless, all with `--rig-test`. All starts succeeded. All stops graceful, no force. |
| Autosave | Native original 5 minutes / 3 saves, prevent_autosave=false. #70 process suppression confirmed after each load and before/after sales intervals. Preferences and configs restored byte for byte; suppression ends with the process. No save or checkpoint created. Final guard attempts/refused=20185/20185, default_allowed=0, armed_allowed=0, unrestricted=0; in_progress=false and compress_thread_alive=false. |
| Frame cap / backup | Temporary B `agent-frame-cap.json` fps=30, vsync=0, removed for C and restored to original absence. Independent pre-stage manifest covers 38 file/absence entries, including the whole PC folder, install receipt, isolated root preferences/config and plist. |

Exact staging command, game stopped and lease held, official agent-rig checkout:

```sh
python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package
```

Receipt backup: `<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T155326Z`. Package contains no RLO files. The rollback printed by staging is recorded below with paths redacted. Automatic command review rejected its removal command (`rm -f style commands are not permitted. Use a safer approach`). Stopped and leased, restoration instead renamed the staged folder into the raw evidence folder, copied back the entire backup mod folder and then copied back install.json. No receipt was edited; hashes and the exact file set were verified:

```sh
rm -rf "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T155326Z/mod" "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T155326Z/install.json" "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

### Per-check outcomes

| Required check | Result | Evidence |
| --- | --- | --- |
| A, #244 item 7, ordinary saves refused | PASS | Unsaved throwaway `RIGTMP-PD7-14f`. Both status and target calls reached the mod and returned its disposable-FT refusal. Live manifest: 2,000 persons before, immediately after and after more than one game hour. |
| B, #246 item 4, unrentable footprint | NOT RUN / fixture gap | Fixed `building list` succeeds: 585 residential rows, all rentable_interior=true, zero unrentable subjects. No eligible three-building sample, Home footprint/floor comparison or screenshot proof. |
| B, #246 item 5, unreadable/empty tally | PARTIAL | Exact load tally recorded: housed=0 beds=0 unreadable=0 empty=0. No unreadable or empty unrentable subjects, so the classification and omitted-warning behavior are unexercised. |
| C, #260 item 1, speed 20 then speed 1 sales | PASS | 10:30–12:30 adds 21 paid $5 receipts, matching native purchases and paid shop_log units, revenue and stock. Raw log purchase counter also includes free bags, detailed below. |
| C, #260 item 2, Living off/on | PASS | Off adds 21 paid receipts; on adds 29. Exactly 2 transition lines, active runtime, original option restored. |
| C, #260 item 3, reach / outcomes | PASS | Exactly 2 reach lines for the two clock-rate changes; 5.85 / ~117 m readbacks. Missed dominates at 10:30; paid native outcome present by 12:30. |

### A — ordinary-save refusal

Fresh headless process; #70 suppression true. `call new_game {"name":"RIGTMP-PD7-14f","throwaway":true}` succeeded, load.status done. The throwaway city was never saved or removed. Exact console calls and outputs, including the confirmed repeat after load settled:

```text
call console {"line":"pc gc population"}
Warning: [Persistent City] gc population refused: measurement mode needs a disposable FT city.
call console {"line":"pc gc population 3000"}
Warning: [Persistent City] gc population refused: measurement mode needs a disposable FT city.
```

Matching Player.log lines (the same two lines occur in the initial attempt and the settled repeat):

```text
[Persistent City] gc population refused: measurement mode needs a disposable FT city.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Persistent City] gc population refused: measurement mode needs a disposable FT city.
[Mod:FoodTrucks] Test bridge: console 'pc gc population 3000' (1 lines).
```

No `MEASUREMENT MODE` line was emitted: neither rejected command entered that mode. Exact read-only manifest counts:

| Observation | City minute / clock | Residents | Visitors | Persons | Fill created |
| --- | --- | --- | --- | --- | --- |
| Before settled refusal | 1922.044656 / 08:02 | 1000 | 1000 | 2000 | 0 |
| Immediately after | 1922.155444 / 08:02 | 1000 | 1000 | 2000 | 0 |
| After one hour | 1990.115011 / 09:10 | 1000 | 1000 | 2000 | 0 |

The early census computation first reported persisted_persons=1000 while stations were pending, then a planned total of 3000 after station discovery. Those are census targets, not the live count; the `pc population manifest` replies above measure the actual persons. Each is retained in the raw evidence. No time mutation was sent to the non-FT city; the hour elapsed naturally.

### B — footprint subjects and load tally

Cold windowed rig-test process, `FT GR5 13f Timing v1`, the 14a/14b cap fixture, paused after load. Exact operation `op building {"action":"list"}` succeeded: residential=585, rentable=585, unrentable=0, pc_home_candidate=557, rotated=3. Three individual `read` calls also succeeded; these are tooling observations, not eligible check-4 evidence:

| Place / address | Yaw | Interior area m² | PC candidate | Eligibility |
| --- | --- | --- | --- | --- |
| `address:ba%3Astreet_broadwaystreet:2:home` / 2 Broadway | 0° | 204 | true | rentable, excluded |
| `address:ba%3Astreet_firststreet:22:home` / 22 1st Street | 268.953° | 204 | true | rentable, excluded |
| `address:ba%3Astreet_hovgaardlane:1:home` / 1 Hovgaard Lane | 315° | 8496 | false | rentable, blocked Hamptons villa, excluded |

`storeys=null`, `storeys_error=ops_building_storeys_not_native` is the documented readback, not an op failure. No floor count or visible ground area is guessed. There is no eligible rotated or yard/sidewalk subject to compare with footprint × floors × 0.6. No screenshots were needed or claimed. The existing fixture was extracted read-only to raw evidence; it was not saved or overwritten.

Exact post-load Player.log:

```text
[Persistent City] unrentable homes housed=0 beds=0 unreadable=0 empty=0
```

The previous `ops_game_api_unavailable` failure did not recur. No missing rig capability blocked the list/read calls; the remaining check-4 gap is the requested fixture's lack of unrentable subjects.

### C — speed-1 sales and Living transitions

Cold headless rig-test process; fixture `FT RLO 13d Food v1` loaded at 08:00; `shop_log start` filtered to `ba:street_fourthavenue#19`, capacity 10000. Ready=true, croissant $5, stock 100 and free bags 300, revenue $0. Speed 20 was used only for the 08:00–10:30 positioning advance. An explicit unpause at 08:34 cleared run_until; it was re-armed at 09:22 for the same 10:30 target, without changing speed. It arrived paused at 10:30.01. Both sales windows used `time {"speed":1,"pause":false,"keep_needs":true}`, ordinary clock polling and a pause at their endpoint. No sales window used run_until or speed 20.

| Readback | Living | Paid receipt total | New receipts | Native purchase total | Paid shop_log units | Raw purchase lines | Revenue | Croissants / bags |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 10:30 | on | 0 | 0 | 0 | 0 | 0 | $0 | 100 / 300 |
| 12:30 | off | 21 | 21 | 21 | 21 | 42 | $105 | 79 / 279 |
| 14:30 | on | 50 | 29 | 50 | 50 | 100 | $250 | 50 / 250 |

All receipt keys match the exact 138-character paid receipt value for the observed BusinessId and croissant. Its UTF-8 SHA-256 is `ca326c1e66a05d6ebcc0df039620e1dd3892881a12db58244853f8ff960bb517`. `pcstate keys` exposes hashes and lengths; comparison with the expected `paid|500|<escaped business>|ba%3Aitemname_croissant` proves their status and price without a save. No pending or conflicting receipt hash appeared. BusinessId: `business:ba%3Astreet_fourthavenue:19:1:ba%3Abusinesstype_coffeeshop:player:generation:1`.

Every paid croissant has one native visit, one paid product line and one $0 paper-bag line. Thus raw shop_log `purchases` is twice the paid receipt count; paid product lines and distinct orders equal receipts. All log snapshots have dropped=0, lost=false, hook_faults=0. Shop readiness and FoodDiagnostics.active remain true. Final accounting_stop_present=false. Revenue changes are $5 per new receipt; both croissants and bags decrease by that count. No probe fabricated a sale.

Original living_population: stored=null, live=true, original_saved=false. Off setter: stored=0/live=false; on setter: stored=1/live=true; restore and independent read: stored=null/live=true/original_saved=false. No RLO file changed. Exact Player.log speed-change/toggle excerpts, in process order (line numbers refer to C-final.log):

```text
2421: [Mod:PersistentCity] Food reach at clock rate 6.667 game minutes per second: walking covers 0.3 m per game minute, so a 60-minute lunch break with 20 minutes eating and 1 minute service reaches food shops within 6 m one way.
2792: [Mod:PersistentCity] RLO Living population on -> Living population off during play; Persistent City keeps owning its shops, crowds and traffic.
2831: [Mod:PersistentCity] Food reach at clock rate 0.333 game minutes per second: walking covers 6.0 m per game minute, so a 60-minute lunch break with 20 minutes eating and 1 minute service reaches food shops within 117 m one way.
3500: [Mod:PersistentCity] RLO Living population off -> Living population on during play; Persistent City keeps owning its shops, crowds and traffic.
```

FoodDiagnostics readbacks:

| Clock | Clock rate | Walking m/game minute | Lunch reach m | Top settled outcome / paid outcome |
| --- | --- | --- | --- | --- |
| 10:30 | 6.66666667 | 0.3 | 5.85 | 1208 × `Missed: No suitable stop in the meal window`; paid=0 |
| 12:30 | 0.333333343 | 5.99999983 | 116.999997 | 1191 × `Declined: Prefers the paid household food basket to the observed purchase options; no shop spend or stock consumed; household food basket is funded`; paid=21 |
| 14:30 | 0.333333343 | 5.99999983 | 116.999997 | 1436 × `Declined: Prefers the paid household food basket to the observed purchase options; no shop spend or stock consumed; household food basket is funded`; paid=50 |

The exact paid outcome reason in this runtime is `Paid: Native product, bag and completed order confirmed; daily settlement remains native`, rather than the shorter `Paid: Native purchase` wording in #260. It is backed by the matching paid receipts and native orders above. At 10:30 the miss count is 1208, exceeding the 1064 household-basket declines. The speed-1 reach also reads 116.999996731 m in the mid-window read.

### Runtime / logged spikes

Persistent City runtime stop: none. No TravelTiming.Apply stop, activation refusal or op failure. An unrelated mod logged this stop verbatim:

```text
[Bakery] HelpSystem was not created within 120 seconds; Help patch stopped to avoid permanent background polling.
```

No performance sampler, GC window or save profiler was armed. The following existing PC log warnings and observation maxima are >=30 ms; these are component observations, not a whole-mod p95 or an exhaustive per-frame export. Repeated maxima can refer to the same warning. Whole-frame timing was not measured. Full source lines remain in logged-spikes.json outside Git.

| Log:line | Kind / clock | Observed ms |
| --- | --- | --- |
| A-final.log:2564 | routing warning | preparation_ms=132.826, closure_ms=0.0 |
| A-final.log:2637 | routing warning | preparation_ms=1.202, closure_ms=47.742 |
| A-final.log:2642 | observation maximum / 1920.0 | advance=146.405, closure_refresh=47.742, preparation_frame=132.826 |
| A-final.log:2813 | observation maximum / 1926.3 | advance=140.178 |
| B-final.log:1702 | routing warning | preparation_ms=132.976, closure_ms=0.0 |
| B-final.log:1726 | observation maximum / 3780.0 | advance=147.673, preparation_frame=132.976 |
| C-final.log:2552 | routing warning | preparation_ms=127.809, closure_ms=0.0 |
| C-final.log:2576 | observation maximum / 1920.0 | advance=144.229, preparation_frame=127.809 |
| C-final.log:2728 | observation maximum / 2009.4 | advance=135.177 |
| C-final.log:2841 | routing warning | preparation_ms=0.256, closure_ms=162.199 |
| C-final.log:2882 | observation maximum / 2074.0 | closure_refresh=162.199 |
| C-final.log:3059 | observation maximum / 2104.0 | advance=141.141 |
| C-final.log:3131 | observation maximum / 2118.4 | advance=128.399 |
| C-final.log:3216 | observation maximum / 2135.9 | advance=131.71 |
| C-final.log:3290 | observation maximum / 2154.1 | advance=129.631 |
| C-final.log:3381 | observation maximum / 2171.1 | advance=130.721 |
| C-final.log:3463 | observation maximum / 2190.0 | advance=130.443 |
| C-final.log:3578 | observation maximum / 2207.8 | advance=132.387 |
| C-final.log:3673 | observation maximum / 2225.4 | advance=138.127 |
| C-final.log:3764 | observation maximum / 2244.9 | advance=132.295 |
| C-final.log:3851 | observation maximum / 2262.7 | advance=135.229 |
| C-final.log:3915 | observation maximum / 2278.5 | advance=136.171 |
| C-final.log:3994 | observation maximum / 2293.8 | advance=321.675 |

### NOT RUN groups / tooling gaps

1. **Required #246 item 4:** fixture gap, all 585 residential buildings expose a rentable interior; no eligible three-home comparison. Building list/read now work.
2. **GR-5 GC/street re-checks:** excluded; spawn_appearance fix still in progress at run authorization; recheck follows its merge. No performance window in this run.
3. **#254 item 4 appearance:** excluded; resident identity through reacquire is a Rig bot gap.
4. **GR-5 60 fps arms:** excluded; Mac reaches about 5 fps in prior runs, Windows CLI lacks --rig-test, both Rig bot gaps. Neither arm measured here; Windows untouched.
5. **GR-5 save latency:** excluded; owner decision pending. No save profiling.

Exact in-game op failures in this run: none. The printed shell rollback was rejected before execution with `rm -f style commands are not permitted. Use a safer approach`; the verified rename/copy restoration completed cleanup without that removal command. Building read returns the documented `ops_building_storeys_not_native` field; visible-storey comparison remains unavailable because there are no eligible subjects. The zero tally is partial evidence, not proof of nonzero empty/unreadable classification. No issue was opened and no bot was contacted.

### Fixtures and cleanup

Created unsaved throwaway: `RIGTMP-PD7-14f`. Reused only `FT GR5 13f Timing v1` and `FT RLO 13d Food v1`. No new fixture save/checkpoint, overwrite or removal. Existing disposable fixture saves stay in the isolated profile.

Cleanup: three graceful stops; no --force. The safer equivalent restore returned the whole PC folder and install.json; the candidate folder was retained outside the profile. Isolated preference cache was restored from this run's own backup, then original bytes copied back. All 38 manifest entries match=true, exact PC file set matches=true, all 53 RLO files unchanged=true. Frame-cap and absent-config entries restored. living_population restored through its op before stop. Original autosave readback at cleanup remained 5 minutes / 3 saves, prevent_autosave=false; guard confirms no allowed save. Lease released; final status running=false, held=false, no process started by this run remains. Agent-rig and original PC checkout clean.

`[Mod:PersistentCity] Persistent population active: 1000 residents, 1000 visitors, 1686 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.`

`[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.`

`[Mod:PersistentCity] Persistent population active: 1611 residents, 1611 visitors, 1685 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.`

Final origin/codex/persistent-city: `7ff6a87acf75e35a3a56d41abd842a43980c19d7`; candidate binary remains the SHA recorded above.

The evidence branch was fast-forwarded to the final tip after testing to preserve the intervening checklist entries. #262 (the appearance fix) merged during the run; its production code was not built or tested here. GC/street re-checks remain excluded.

Offline verification: candidate build passed; docs-links, markdown-ban and git diff --check recorded in the PR. No test source changed, no focused classes, no full suite. Raw evidence filename/hash manifest: `raw-evidence-manifest.json` in the stage. Build/evidence worktree removed after this evidence commit and PR creation.

Refs #244
Refs #246
Refs #260

## Rig results — 14 November 2026 g, Mac (GR-5 GC recheck after #262 at 7ff6a87a)

**RESULT: DONE. Counts: 2 passed / 2 partial / 1 failed / 4 NOT RUN groups.** The five #262 checks count once each; the four excluded groups below are additional NOT RUN items. Day/night pass #262's allocation and collection-count expectations. The street <5 ms gate fails. Neither visual check is a full pass. This does not establish the overall GR-5 GC/street gates: sampler pause p95 regressed. In-game proof gates no merge.

Role `rig_test`, where `mac`, isolated `agentmac`, owner `persistent-city-rig-2026-11-14g-mac`. Raw evidence `<stage>` = `<home>/HART-Builds/persistent-city-rig-2026-11-14g-mac`. Report copy: `<home>/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-14g-mac.md`. [Complete GC reports and compact window/visual evidence](perf/gr5-2026-11-14g-mac.json): **17,069 bytes**, SHA-256 `0244af1a5674dca5850cd85c41cf781cc922f8487d5ef340f17802725b91a8cf`. Every GC allocation key, sample/skipped count, counter_frame_bytes and human report is retained. Larger records and screenshot hashes are in `<stage>/raw-evidence-manifest.json`, SHA-256 `2c9fa0a5a2ee8928e7c32c6aa23657955f7cee36310ff071a23dab4af0a04643`.

All performance observations are provisional on the shared Mac. Every command and measurement ran in the foreground and was awaited. No Windows contact, other-session intervention, Steam/ordinary profile or ordinary save access, production change, RLO file change, schema/weight/version/changelog change, issue creation, release, Workshop upload or merge. Schema stays 1; FoodChoicePriceWeight 1.2 and FoodChoiceHouseholdWeight 1.0.

### Build and rig

| Item | Evidence |
| --- | --- |
| Tested candidate | `7ff6a87acf75e35a3a56d41abd842a43980c19d7`, exactly the initial origin/codex/persistent-city tip and requested #262 merge. Final fetch moved to `844ce2b95b23a53431ce18de26cf09fe44966159` (#263, 14f docs only). The results branch was fast-forwarded to that docs-only tip before appending this section; tested production code is unchanged. |
| Branch / worktree | New `codex/mac-rig-2026-11-14g`, `rig-2026-11-14g-mac-evidence`, serving both build and evidence. Original checkout stayed clean. |
| Build | `python3 tools/build.py --game-managed "<home>/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0; Homebrew PATH and DOTNET_ROOT. |
| PersistentCity DLL SHA-256 | `a0a1ef89db96420ce375100a09f6236c13d47b10f3856f9c1963079b0613341b`; built, package and live staged DLL matched; both starts validated the receipt. |
| Official agent-rig HEAD | `022630f7d6df8c533a230ac218413a68b181ed95`, clean before/after. Fetch/ff-only pull already current. No edits, commit or push. |
| Live AgentRigOps SHA-256 | `154be3036954c314d3bd9eb4409da98f6a9b3a508748f2b1811c457abce07955`, matched live ready health and cleanup. |
| RLO version, exact Player.log | `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` All 53 package files unchanged. |
| PC activation, exact Player.log | `[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.` No commit-stamped build line is emitted; the matching staged hash and validated receipt establish binary identity. |
| Lease | 14f held the rig at first status. One foreground loop checked every 120 s, without takeover, from 16:09:36 to 16:19:37 UTC. Acquired `2026-11-14T16:19:37.590259Z` after running=false/held=false. No build or stage before ownership. |
| Resources / starts | Pre-stage 67.7 GiB free disk / 74% reported free memory; before B 64.7 GiB / 79%. A: one successful `start --mode headless --rig-test`, PID 47738. B: one successful cold `start --mode windowed --rig-test`, PID 72372. Two graceful stops, no force; no refusal or resource override. |
| Autosave original / suppression | Native 5 minutes / 3 saves, prevent_autosave=false. #70 `autosave` suppressed=true after every completed load and before/after every measured window. All four perf guards clean=true, zero allowed saves, zero native-saving frames. No manual save, checkpoint or save-latency measurement. |
| Config / options | SaveCostDiagnostics=false throughout; readback requested=false, save=null, activation=null. Native save profiler installed=false. Temporary B frame cap fps=30/vsync=0 applied, original absence restored. living_population unchanged: stored=null, live=true, original_saved=false before/after. No RLO toggle was performed. |

Exact staging command from official agent-rig, stopped and leased:

```sh
python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package
```

Receipt backup: `<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T162033Z`. Package contains no RLO files. Printed rollback, path-sanitized (`<home>` replaces the user home directory):

```sh
rm -rf "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T162033Z/mod" "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T162033Z/install.json" "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

### Per-check outcomes

| #262 check | Result | Evidence against the PR expectation |
| --- | --- | --- |
| 1. Day, 600 s at 15:30 | PASS | Appearance **62.155 MB / 1,203 samples**, 11.99x below 14b's 745.426 MB / 1,281, better than the projected 90–130 MB; **7 gen0 < 11**. Pause p95 **67.048 ms**, regressed from 28.191; physical+advance **309.570 MB**, from 1,021.6. |
| 2. Night, 600 s at 01:00 | PASS | Appearance **26.120 MB / 492 samples**, 12.55x below 327.776 MB / 561; **6 gen0 < 8**. Pause p95 **141.801 ms**, regressed from 61.316; physical+advance **134.021 MB**, from 433.6. |
| 3. Street, 600 s | FAIL | Qualified window: UpdateCity p95 **5.2988 ms**, mod-work p95 **5.3683 ms**, both exceed strict <5 ms. One spike >=30 ms, **64.7872 ms**, frame 56448, updatecity. 14b: 5.5170/5.5824 ms, four spikes, max 78.2031. |
| 4. Looks, release/reacquire and pan | PARTIAL / resident identity tooling gap | Three native walker handles attempted; two follow calls succeeded, third stale. No stable PC resident identity or verified release/reacquire comparison. Pan completed **60.759 s** with five screenshots; visible dressed walkers showed no evident undressed, distorted or T-posed body in inspected images. Both required blendshape error searches returned 0. Stills do not prove the absence of a brief flash or invisible body. |
| 5. Deferred burst / refill | PARTIAL / fixture density and comparison gaps | Five-position rapid camera sweep completed. Fresh 7th Street address: at most 3 nearby walkers over 38.645 s. Busy-street return after speed-mode pool clearing: 2 at 0.631 s, >=12 by 6.754 s, >=24 by 12.636 s, >=32 by 30.620 s, 35 at 42.619 s. Neither arm reached 48–96, so the 12–24 s full-density expectation is unverified. No wrong-look flash evident in inspected stills; no continuous flash proof. 14b's candidate was not restarted for a paired refill measurement; its 3–6 s figure remains the PR expectation. |

### GC windows and residual decision

Fixture: `FT GR5 13f Timing v1`, the 14a/14b fallback, 1,000 residents / 2,000 visitors / 1,683 places, person target off, street player x=215,z=0, speed 1. `FT RLO 13d Food v1` exists but again loaded 1,611/1,611, so was rejected before sampling. Day began Tuesday 15:30, night Wednesday 01:00. GC samplers were stopped outside their windows; mod-work timing was unarmed for both GC windows. The 600 s waits are bounded by the start/stop console replies; native guard unions were 601.466/600.556 s. Every allocation counter is `boehm_total`; legacy frame_alloc_bytes remains separately labelled `heap_used`. MB below means decimal MB.

| Window | Stored / dropped frames | gen0/1/2 | Pause p50 / p95 / max ms | Mono heap bytes | Counter frames / counter_frame_bytes | Native whole-frame p95 / max ms |
| --- | --- | --- | --- | --- | --- | --- |
| day | 16469 / 0 | 7/0/0 | 37.458/67.048/67.048 | 1629356032 | 16469 / 1740692800 | 49.0074/861.6949 |
| night | 16950 / 0 | 6/0/0 | 42.231/141.801/141.801 | 1894055936 | 16950 / 1611097296 | 41.8950/1330.2122 |

Every allocation field and sample/skipped count is in the linked full GC JSON. The segment table gives bytes / samples / skipped; child spans overlap their parents. `physical_spawn_randomize` and `physical_spawn_bake` are inside `physical_spawn_appearance` and are not added to physical or to the totals.

| Allocation segment | Day bytes / samples / skipped | Night bytes / samples / skipped |
| --- | --- | --- |
| mod | 431063872 / 16470 / 0 | 254285376 / 16951 / 0 |
| advance | 209050960 / 16373 / 0 | 71735360 / 16861 / 0 |
| physical | 100518648 / 16373 / 0 | 62285312 / 16861 / 0 |
| physical | 100518648 / 16373 / 0 | 62285312 / 16861 / 0 |
| physical_ambient | 3341808 / 16373 / 0 | 3239296 / 16861 / 0 |
| physical_vehicles | 1087184 / 16373 / 0 | 399840 / 16861 / 0 |
| physical_bindings | 15984 / 583598 / 0 | 11424 / 224962 / 0 |
| physical_vendor_meals | 0 / 16373 / 0 | 144 / 16861 / 0 |
| physical_fade | 0 / 16373 / 0 | 0 / 16861 / 0 |
| physical_focus | 0 / 16373 / 0 | 192 / 16861 / 0 |
| physical_observe | 274160 / 566823 / 0 | 104000 / 207918 / 0 |
| physical_release | 15008 / 402 / 0 | 13808 / 183 / 0 |
| physical_parked | 1961392 / 583 / 0 | 1709504 / 586 / 0 |
| physical_index | 80 / 2179 / 0 | 80 / 2195 / 0 |
| physical_queues | 8960 / 2179 / 0 | 17392 / 2195 / 0 |
| physical_roster | 287328 / 2179 / 0 | 191312 / 2195 / 0 |
| physical_selection | 1353912 / 2680 / 0 | 691288 / 2395 / 0 |
| physical_spawn_pedestrian | 29837120 / 824 / 0 | 29701224 / 331 / 0 |
| physical_spawn_appearance | 62154768 / 1203 / 0 | 26120448 / 492 / 0 |
| physical_spawn_check | 0 / 802 / 0 | 0 / 328 / 0 |
| physical_spawn_vehicle | 180944 / 78 / 0 | 85360 / 33 / 0 |
| physical_unattributed | 0 / 32746 / 0 | 0 / 33722 / 0 |
| physical_spawn_randomize | 3555456 / 401 / 0 | 1490912 / 164 / 0 |
| physical_spawn_bake | 1849408 / 401 / 0 | 1633872 / 164 / 0 |

Appearance residual excluding randomize and bake: **56,749,904 / 22,995,664 bytes** day/night. Bake plus that remainder is **58,599,312 / 24,629,536 bytes**, **7.86% / 7.51%** of 14b appearance. Appearance as a whole is **8.34% / 7.97%** of 14b. Thus **neither reading exceeds about 10%**; #262's proposed combiner replacement trigger is not met by these measurements. Randomize/bake are not double-counted.

Largest measured allocation parent is now **advance**: 209,050,960 / 71,735,360 bytes. Largest physical child is **spawn_appearance** by day (62,154,768); **spawn_pedestrian** by night (29,701,224, versus 26,120,448 appearance). Appearance's uninstrumented remainder exceeds its separately measured randomize and bake children. No code root cause for the pause regression is inferred. The total process-wide counter stays much larger than PC's measured spans.

| Window | Last periodic pedestrianLooks built / reused / deferred | Pool hit rate reused/(built+reused) | Exact builds within GC span |
| --- | --- | --- | --- |
| Day | 340 / 22 / 1,034, 0 idle / 96 owned | 6.08% | 401, from 1,203 appearance samples / 3; randomize and bake each 401 samples |
| Night | 165 / 2 / 1,118, 96 idle / 96 owned | 1.20% | 164, from 492 appearance samples / 3; randomize and bake each 164 samples |

Look counts and hit rates above are the last periodic log snapshots, not exact window deltas; the rig exposes no end-of-window counter projection. They are kept distinct from the allocation sampler's exact build counts. The latest snapshot need not coincide with `pc gc stop`.

### Street windows and every mod-work spike >=30 ms

The first street window used the documented inner-component profile (advance, route preparation, physical population, inspector) but omitted a separate UpdateCity timer. Its complete mod-work evidence is retained as diagnostic, not used to claim the required UpdateCity result. After discovering the omission, the untimed advance toward next afternoon was paused at Wednesday 20:18 and the unchanged fixture reloaded in the same A process; no measurement was open then. The qualified window reloaded the saved fixture state, positioned Tuesday 15:30 and timed UpdateCity, matching 14b's component boundary. Nine pans succeeded in each street window. No spike was chased.

| Window | Seconds / frames / achieved fps | UpdateCity p95 / max ms | Mod-work p95 / max ms / spikes >=30 | Whole-frame p95 / max ms / spikes >=30 |
| --- | --- | --- | --- | --- |
| initial diagnostic | 600.332/4429/7.38 | not separately timed | 6.1662/196.9031/19 | 226.6872/1192.4037/4278 |
| qualified | 600.399/3485/5.80 | 5.2988/64.7304 | 5.3683/64.7872/1 | 200.5798/506.5189/3485 |
| 14b reference | 600.284/3370/5.61 | 5.5170/78.1533 | 5.5824/78.2031/4 | 238.9091/1002.2687/3370 |

Both records have coverage.complete=true, validity.valid=true, zero dropped frames and zero off-main-thread/cross-frame/unbalanced/depth-overflow faults. Whole-frame timing is separate from mod-work. The component profile and day differ between the diagnostic and qualified windows, so their difference is not an optimization claim.

| Window / Unity frame | Mod-work ms | Dominant component / group | Whole-frame ms in row |
| --- | --- | --- | --- |
| diagnostic / 48744 | 196.9031 | PopulationRuntime.Update / frame_update | 227.1576 |
| diagnostic / 50687 | 42.8869 | physical_population / physical_population | 224.2295 |
| diagnostic / 50775 | 41.6359 | physical_population / physical_population | 481.2128 |
| diagnostic / 50784 | 94.1541 | PopulationRuntime.Update / frame_update | 589.6928 |
| diagnostic / 50816 | 36.4798 | PopulationRuntime.Update / frame_update | 228.5977 |
| diagnostic / 50933 | 32.1965 | advancement / advancement | 347.1219 |
| diagnostic / 50939 | 38.3795 | physical_population / physical_population | 244.2708 |
| diagnostic / 51385 | 45.1151 | physical_population / physical_population | 220.1075 |
| diagnostic / 51414 | 68.4536 | physical_population / physical_population | 491.4984 |
| diagnostic / 51439 | 49.7060 | physical_population / physical_population | 781.2728 |
| diagnostic / 51444 | 44.1786 | PopulationRuntime.Update / frame_update | 265.4448 |
| diagnostic / 51535 | 40.5315 | physical_population / physical_population | 231.5308 |
| diagnostic / 51545 | 35.3069 | physical_population / physical_population | 228.2260 |
| diagnostic / 51560 | 37.0766 | physical_population / physical_population | 660.9367 |
| diagnostic / 51561 | 39.0091 | physical_population / physical_population | 266.2937 |
| diagnostic / 51569 | 47.0807 | physical_population / physical_population | 222.7891 |
| diagnostic / 51587 | 31.8290 | physical_population / physical_population | 223.1465 |
| diagnostic / 51589 | 31.9059 | physical_population / physical_population | 468.6219 |
| diagnostic / 51592 | 30.2596 | physical_population / physical_population | 593.9217 |
| qualified / 56448 | 64.7872 | updatecity / updatecity | 221.6390 |

All 20 exported mod-work spikes are listed, including the extra diagnostic. The qualified spike's UpdateCity contribution is 64.7304 ms. Qualified mod-work p95 is 0.2141 ms (3.8%) below 14b and UpdateCity 0.2182 ms (4.0%) below, but both still fail <5 ms. Shared-host variation prevents attributing those small differences to the fix alone.

### Windowed visual evidence and exact op failures

A cold windowed rig-test process used the same 1,000/2,000 fixture. Two before images show dressed moving walkers (red top and green top); the third handle was stale. Return images include occluded/empty views and do not establish the same resident's clothes, skin or body shape. The rig documentation explicitly says walker handles can be reused without a persistent resident identity guarantee. Exact resident-identity attempts:

- `op screen_pos {"action":"get","target":{"kind":"walker","id":"walker-a066d42e3d714e15a31af9e25ed74fee"}}`: returned a current native position, with no PC resident identity guarantee.
- `op screen_pos {"action":"get","target":{"kind":"walker","id":"walker-e88571aea7fd48a0b2179fdd1f4bf003"}}`: returned a current native position, with no PC resident identity guarantee.
- `op screen_pos {"action":"get","target":{"kind":"walker","id":"walker-a4e5250f45f5449db478eff8b3c0aead"}}`: `ops_view_stale_target`. The preceding `camera follow` on this same target also returned `ops_view_stale_target`.

The 60.759 s pan retained `look-pan-shot-0/3/6/9/11.png`. Refill screenshots and active-walker counts are retained for both arms; some views are occluded by buildings, limiting visual conclusions. No undressed/distorted/T-posed body or wrong-look flash is evident in inspected images, but still captures are not a continuous burst record. Both Player.log strings `Merged body already has blendshapes` and `Merged body vertex layout differs` have **0 matches**. No Persistent City runtime-stop line was observed.

Fresh-street setup: `screen_pos get` for business `ba:street_seventhstreet#2` returned x=-140.1295,z=372.905426, more than 500 m from the original focus. `call teleport {"who":"player","place":"address:ba:street_seventhstreet#2"}` succeeded; only 0–3 nearby walkers were observed through 38.645 s. A return to x=215,z=0 after the 17:30 speed-mode advance clears the body pool and supplies the second observation. It is a return to an already visited street, not a paired unseen-street benchmark. The observed 35-walker maximum does not establish demand for 48–96 walkers. Full-density refill time and a fresh paired 14b comparison are NOT RUN within this partial check; no weaker pass threshold is used.

Other exact errors and recovery:

| Command | Exact error / action |
| --- | --- |
| `call saves {}` | `Unknown command 'saves'. Commands: state, console, debug, time, money, teleport, walk, drive, vehicle, commissary, crew, route, price, use, taxi, phone, call, eat, floorplan, place, drop, pickup, handtruck, fixture, logistics, parks, day_summary, places, map, ui, shoot, save, load, new_game, quit, op.` Preparatory enumeration unavailable; documented FT fixtures loaded directly. |
| `call time {"run_until":{"time":"15:30"},"speed":20,"keep_needs":true}` when already at the target | `day 2 15:30 has passed already: it is day 2 15:30.` No window opened; current-time positioning was skipped. |
| `call time {"pause":true}` during same-name reload dispatch | `time works in the city only; the game is at the loading now.` Reload had briefly reported the previous done snapshot; waited for current load to finish, then confirmed autosave and paused. No load command replay. |
| `call teleport {"who":"player","x":500,"z":0}` for look departure and initial refill | `No ground found at x 500.0, z 0.0.` Both refusals retained. Used a native business entrance for the subsequent valid trip. |

### Player.log excerpts for clock changes

The untimed speed-20 positioning and reached/paused-speed-1 lines are retained below. Explicit speed-1 measurement calls also read back speed=1, paused=false in the raw replies. RLO was never toggled.

```text
A [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 2 15:30.
A [Mod:FoodTrucks] Test bridge: run_until day 2 15:30 reached at day 2 15:30; paused, speed 1.
A [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 01:00.
A [Mod:FoodTrucks] Test bridge: run_until day 3 01:00 reached at day 3 01:00; paused, speed 1.
A [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 15:30.
A [Mod:FoodTrucks] Test bridge: run_until day 3 15:30 reached at day 3 15:30; paused, speed 1.
A [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 4 15:30.
A [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 2 15:30.
A [Mod:FoodTrucks] Test bridge: run_until day 2 15:30 reached at day 2 15:30; paused, speed 1.
B [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 2 15:30.
B [Mod:FoodTrucks] Test bridge: run_until day 2 15:30 reached at day 2 15:30; paused, speed 1.
B [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 2 17:30.
B [Mod:FoodTrucks] Test bridge: run_until day 2 17:30 reached at day 2 17:30; paused, speed 1.
```

The A `day 4 15:30` advance has no reached line because it was explicitly paused before the corrective reload. No sample was running during this setup. No runtime-stop/error line is hidden by that interruption.

### NOT RUN groups, fixtures and cleanup

1. **GR-5 street 60 fps:** excluded; Mac prior runs reach about 5 fps at 1,000 residents. Windows scoped staging remains missing (agent-rig #87); Windows was not contacted.
2. **GR-5 busy-shop 60 fps:** same cadence/staging exclusions. No shop performance arm in this request.
3. **GR-5 save latency:** excluded; owner decision pending. SaveCostDiagnostics stayed off, no profiler or explicit save.
4. **All other acceptance checks:** outside this run's A/B scope, including P-D refusal, footprint/tally, native sales and RLO toggle arms. The newly merged 14f report was read after final fetch, not rerun here.

Sub-arms not established within partial checks are not counted again: stable PC resident identity across release/reacquire, full-density 48–96 refill time, continuous wrong-look flash proof and paired 14b refill. These are proof/tooling or fixture limits, not passing results.

Reused `FT RLO 13d Food v1` for population validation only, and `FT GR5 13f Timing v1` for all measurements/visual checks. No new save, checkpoint or throwaway city; existing fixtures were not overwritten or removed.

Cleanup, stopped and leased: renamed the staged candidate into `<stage>/staged-candidate-restored-out`, copied the full receipt backup mod folder into the empty path, then restored install.json unchanged. Original isolated root preferences/config, PlayerSettings, plist, persistent-city.json and frame-cap absence were restored from this run's independent backup. All **35 original file hashes / 4 absent paths / exact PersistentCity file set / 53 RLO file hashes match; zero mismatches**. install.json SHA-256 `c57df8e5b67cc581e5006705e028f7dab2898681545ce96d4da1a57854e52529`. AgentRigOps hash unchanged. The native autosave readback remained the original 5 minutes / 3 saves; process-only suppression ended at stop. living_population was never changed. All 23 helper-generated PNGs were copied/hash-verified into `<stage>` and removed from their isolated helper output paths; raw screenshots remain outside Git.

Lease released: final running=false, process=null, held=false, pending=0. Both owned player PIDs and their caffeinate assertions are gone. Official agent-rig and original PC checkout remain clean. One worktree served build/evidence and is removed after commit, push and opening the unmerged PR.

Offline validation: candidate build passed; compact JSON parses and is under 20 KB. Docs-links passed (20 files, 0 broken relative links); markdown-ban and `git diff --check` passed for this docs-only change. No test files changed, so no focused classes; no full suite or regression suite. Only this existing checklist and the compact JSON are changed.

Refs #262

## Rig results ? 14 November 2026 h, PC (GR-5 60 fps street/busy-shop + GC vs Mac 14g at 650fc893)

**RESULT: BLOCKED. Counts: 0 passed / 0 partial / 0 failed / 9 NOT RUN.** Four required measurement arms were blocked during setup; five excluded groups are counted separately below. No 600 s window began. The Windows baseline, allocation/GC comparison and strict UpdateCity / mod-work p95 <5 ms gates remain unverified. In-game proof gates no merge.

Role `rig_test`, where `pc`, owner `pc-rig-2026-11-14h`, isolated `agent1`. All observed cadence values below are **PC 60 fps windowed, provisional (PC shared)**. A configured cap is verified; sustained 60 fps is not. The Mac was not contacted, started or used. The seventh-fix branch and other sessions were untouched.

[Compact evidence](perf/gr5-2026-11-14h-pc.json) contains explicit null measurement fields, cap/guard readbacks, machine-load snapshot, Mac reference values and restoration results. `<stage>` is this run's local staging folder and `<home>` the rig user's home folder. Raw evidence and this run's backups: `<stage>`. The raw manifest records file sizes and SHA-256 values; no logs, binaries, credentials or saves are committed. Report copy: `<home>\Coding_Projects\.agent-logs\persistent-city-rig-2026-11-14h-pc.md`.

### Build and rig

| Item | Evidence |
| --- | --- |
| Candidate | `650fc893d0119921e2b37576ac42b115d20e5ed8`; initial and final fetch agree. Same production code as Mac 14g's `7ff6a87a`; #264 added only results. Seventh fix absent. |
| Worktrees | New results branch `codex/pc-rig-2026-11-14h`, evidence `rig-2026-11-14h-pc-evidence`; separate detached build `rig-2026-11-14h-pc`. Original checkout unchanged. |
| Official agent-rig | `e77831929bd4d347ba064e02f485c056d523107e`; clean before/after, fetch and ff-only pull already current. No edits, commits, push or overlay update. |
| AgentRigOps SHA-256 | `1e9c612a3a96fa1f967553ddc0af466dd9e2819f59692cdf5a7766228a85d673`; live hash, ready health and cleanup agree. |
| Build | Windows Steam-Managed build: `python tools/build.py --game-managed "C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"`, exit 0. PATH prefixed with `%LOCALAPPDATA%\Microsoft\dotnet`; required wait helper printed `dotnet lock removed 2026-11-10; continuing`. No offline dotnet suite. |
| PersistentCity DLL SHA-256 | `a0a1ef89db96420ce375100a09f6236c13d47b10f3856f9c1963079b0613341b`, identical to Mac 14g. Built package copied into this run's stage; receipt-validated start accepted it. No RLO/game DLL packaged. |
| Staging / rollback | Official #87 scoped stage succeeded. Backup `C:\BigAmbitions-AgentTests\receipt-backups\20261114T183607Z`; 1 added, 41 existing package receipt paths rehashed, 42 pinned entries. What-if preceded the real stage. Other mod receipt entries left unchanged. |
| Start / stop | One `python scripts\agent start --mode windowed --rig-test`, successful first attempt, isolated PID 4596. Official `status` and `health` succeeded locally. One graceful `scripts\agent stop`; no force, no other process stopped. |
| Food Trucks | Health `mod_version=0.10.2`; unchanged. |
| RLO, exact log | `[Mod:RealLifeOverhaul] Real Life Overhaul 0.45.0 initialised.` Mac 14g used 1.0.6. This run did not install/update/edit RLO. PC activated; no RLO incompatibility is inferred. All 48 original RLO file hashes match at cleanup. |
| PC activation, exact log | `[Mod:PersistentCity] Persistent population active: 957 residents, 957 visitors, 1081 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.` No commit-stamped build line is emitted; deterministic DLL identity and successful receipt validation establish the build. |
| Autosave / SaveCost | Original native autosave 5 minutes / 3 saves, prevent_autosave=false. Rig-test guard enabled=true, autosave_disabled=true, save_specific=false, allow=[], error=null. SaveCostDiagnostics requested=false, save=null, activation=null; native profiler installed=false. Read before stop: no save attempts/allowed/refused/native save activity, no perf window active. No save or save-latency probe. |
| Lease | Acquired free at 18:34:33 UTC. Initial release followed stop/rollback; cleanup audit required reacquisition at 18:39:38 UTC under the same owner, then final release after exact verification. No takeover, other owner or pending work. |
| Machine load | Setup snapshot 18:38:24 UTC: Intel i5-13600KF, CPU 64%, total RAM 63.84 GiB, free 22.57 GiB. Notable resident processes: isolated player 5.49 GiB, PathOfExileSteam 4.93 GiB, vmmemWSL 2.85 GiB, Path of Building 1.34 GiB, browser and another CLI session. These are working sets, not attributed CPU rates. No measured window started, so no per-window load readings exist. |

Exact stage command, stopped and leased, preceded by the same command with `--what-if`:

```text
python scripts\stage_mod_windows.py --stage-mod PersistentCity <stage>\package
```

Printed rollback, executed stopped and leased:

```text
python scripts\stage_mod_windows.py --rollback "C:\BigAmbitions-AgentTests\receipt-backups\20261114T183607Z" PersistentCity
```

### Cap health and fixture

Staged exactly `{"fps":60,"vsync":0}` in the isolated profile's `agent-frame-cap.json`; original absence restored. Initial menu readback: requested=60, applied=true, vsync_count=0, target_frame_rate=60, measured_fps=56.46, window_s=2.00, error=null. Loaded-city observations ranged 46.42?49.61 fps in retained setup snapshots. Last pre-stop readback: requested=60, applied=true, vsync_count=0, target_frame_rate=60, measured_fps=49.61, window_s=2.02, error=null. These are **PC 60 fps windowed, provisional (PC shared)** setup observations, not a 600 s measurement. The applied cap stayed present; sustained 51?66 fps qualification was not established. No cold restart was needed for cap loss.

Neither `FT RLO 13d Food v1` nor the Mac fallback `FT GR5 13f Timing v1` exists in this isolated PC SaveGames inventory. Used the authorized fallback:

```text
new_game {"name":"RIGTMP-GR5-14h","throwaway":true}
```

It completed with load.status=done, Monday 08:00, speed 1, unpaused, player x=215,z=0. Last live manifest: **957 residents / 957 visitors**, 20 portals, person-target unchanged; no `pc gc population 3000` command. Census resident_target=957, commuter_target=1914 and persisted_persons=2871 are census plans, not evidence of a live 1,000/2,000 city. Load tally: `[Persistent City] unrentable homes housed=0 beds=0 unreadable=0 empty=0`. No busy-shop fixture was created. The throwaway city was never saved; native creation left only its portrait JPEG, with no `.hsg`/city save. No undocumented deletion was attempted.

### Required arms and comparison

| Arm, in requested order | Result | PC 60 fps windowed evidence |
| --- | --- | --- |
| C day, 15:30, 600 s | NOT RUN / tooling and fixture gaps | `time` positioning refused the non-FT city; `perf begin` refused it. Required population/cadence unverified. |
| C night, 01:00, 600 s | NOT RUN / same setup gate | No valid day window or supported time positioning in this city. |
| A street, 600 s | NOT RUN / same setup gate | `modwork arm` refused the non-FT city. No UpdateCity or mod-work timing. |
| B busy-shop, 600 s | NOT RUN / same setup gate | Same city has no established busy-shop fixture; scoped mutation guard and required population/cadence prevent qualification. |

For all four arms, measured fps, whole-frame p95/p99, gen0, pause p95, heap, UpdateCity/mod-work p95 and >=30 ms spike counts/frames/components are **unavailable**, not zero. No GC sampler was started; `pc gc report` confirmed `gc measurement not started`. Consequently there is no complete GC report or allocation-segment/sample-count/pedestrianLooks/pool-hit comparison to publish. All physical_spawn_* segments, physical_unattributed and counter_frame_bytes remain unavailable. Randomize and bake remain children of appearance and must not be added to it.

Reference only, from [Mac 14g](#rig-results--14-november-2026-g-mac-gr-5-gc-recheck-after-262-at-7ff6a87a) and [its complete JSON](perf/gr5-2026-11-14g-mac.json):

| Metric | Mac 14g day / night | Mac 14b day / night | PC 60 fps windowed |
| --- | --- | --- | --- |
| gen0 | 7 / 6 | 11 / 8 | unavailable |
| Pause p95 ms | 67.048 / 141.801 | 28.191 / 61.316 | unavailable |
| Mono heap bytes | 1,629,356,032 / 1,894,055,936 | see 14b full report | unavailable |
| Appearance MB / samples | 62.155 / 1,203; 26.120 / 492 | 745.426 / 1,281; 327.776 / 561 | unavailable |
| Physical + advance MB | 309.570 / 134.021 | 1,021.6 / 433.6 | unavailable |
| Street UpdateCity / mod-work p95 ms | 5.2988 / 5.3683 | 5.5170 / 5.5824 | unavailable |
| Street >=30 ms mod-work spikes | 1, max 64.7872 ms | 4, max 78.2031 ms | unavailable |

Mac 14g GC windows were headless at about 30 Hz; this PC was windowed with a 60 fps target. They are not like-for-like frame-time comparisons. The required GC pause/gen0/heap/allocation comparison remains blocked by absent PC samples. No improvement, regression, <5 ms pass or product failure is inferred from this run.

### Exact op failures and tooling gaps

1. Day positioning, `time {"run_until":{"day":1,"time":"15:30"},"speed":20,"keep_needs":true}`: `time changes the save, and the loaded save 'RIGTMP-GR5-14h' doesn't start with FT: load an FT save first.` No time mutation occurred.
2. `perf {"action":"begin","label":"gc-day"}`: `ops_ft_save_required`. No window opened; subsequent perf read confirmed active=false.
3. `modwork {"action":"arm","components":[{"name":"updatecity","type":"PersistentCity.Runtime.PopulationRuntime","method":"UpdateCity"}]}`: `ops_ft_save_required`. Subsequent status confirmed armed=false, bound=0, no open window.
4. Preparatory `help {}` at the menu: `help works in the city only; the game is at the menu now.` Switched to documented commands and direct isolated inventory; no gameplay effect.

The #87 local lifecycle and scoped staging paths both worked; the earlier CLI/staging blockers did not recur. The remaining gap is that the authorized non-FT throwaway supports console but retains FT-only time/perf/mod-work mutation guards. No guard bypass, tooling edit, unrelated FT fallback or production fix was attempted. Fresh-world housing/population differs from the requested fixture, and RLO differs from Mac 14g. Those observations do not establish a code cause.

### Excluded NOT RUN groups

1. #246 checks 4?5, unrentable footprint/tally: Rig bot fixture still in flight, run `20261114-192649-rig-unrentable-residential`.
2. #262 checks 4?5, look stability/burst-refill: no stable resident identity through release/reacquire and no dense-walkers fixture; queue resident-reacquire=14g gap 1, dense-walkers=14g gap 2.
3. GR-5 check 2, save latency: owner save-route decision pending via Spock. No save profiling.
4. P-D 7 and #260 food checks: covered by Mac 14f; outside this run.
5. Seventh fix: still in flight on the Mac; later recheck. Its branch, worktree, lease, compiler and game were not used or disturbed.

### Restoration and validation

Gracefully stopped only owned PID 4596. Official status showed running=false, health=null, pid=null; process inventory found no Big Ambitions process. Executed this run's printed rollback. Full PersistentCity file inventory matches, all **45 original file hashes and four absences** match, and all **48 RLO files** match. No RLO files or overlay were restored/edited by the tester. Exact isolated registry preference export matches; PlayerSettings, persistent-city config, original autosave preferences and frame-cap absence are restored.

The scoped rollback reserialized/reordered `agent-install.json`, so its first post-rollback whole-file hash differed despite identical top-level data and complete path-to-entry maps. After verifying those maps, restored the exact original receipt backup bytes under the documented stopped-player/lifecycle reservation. No receipt entries were hand-edited or refreshed. Final receipt SHA-256 `4ace1ba1365a2dababda6861be7ce27bbe72c0fba4f48122a5af5cb08c9b5111` matches the pre-stage manifest. Registry export discrepancies in the first audit were resolved by importing this run's original export. Final audit: zero mismatches. AgentRigOps hash unchanged. Final lease free, pending=0; no owned process remains. The unsaved throwaway portrait is retained because no documented removal operation was used.

Build passed; compact JSON parsed; docs-links, markdown ban and whitespace checks recorded with the evidence commit. No offline test suite or regression suite. Only this checklist and the compact JSON changed. No production code, RLO, schema 1, food weights, version/changelog, release, Workshop, GitHub issue or merge. Build worktree removed after commit; evidence worktree removed after PR creation. All steps ran in the foreground and were awaited.

**RESULT: BLOCKED.**

## Rig results — 14 November 2026 i, Mac (#246 checks 4-5 with unrentable_fixture at 65b41db5)

**RESULT: DONE. Counts: 1 passed / 1 partial / 0 failed / 4 NOT RUN groups.** #246 check 4 is partial; check 5 passes the explicitly documented rig-classification fallback. Persistent City's applied-state Home evidence and tally are **not observable in-load**. No PC mismatch is claimed. In-game proof gates no merge.

Role `rig_test`, where `mac`, isolated `agentmac`, owner `persistent-city-rig-2026-11-14i-mac`. `<stage>` means `<home>/HART-Builds/persistent-city-rig-2026-11-14i-mac`; report copy `<home>/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-14i-mac.md`. Raw replies, both Player logs and nine screenshots stay outside Git. Raw evidence manifest `<stage>/raw-evidence-manifest.json`, SHA-256 `3d3e3839a60a8e6e53090a2ffda47957e2cbbcf399af2a678b4767ba11c4ab65`.

### Build and rig

| Item | Evidence |
| --- | --- |
| Candidate | `65b41db5a9567a1c79aa5cb972232800d0710b59`; initial and final fetched origin/codex/persistent-city tip, unchanged. |
| Branch / worktree | New `codex/mac-rig-2026-11-14i` / `rig-2026-11-14i-mac-evidence`; one isolated worktree served build and evidence. Other sessions and the original checkout were untouched. |
| Build | `python3 tools/build.py --game-managed "<home>/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0, Homebrew PATH and DOTNET_ROOT. No test sources changed; no full suite or regression suite. |
| PersistentCity DLL SHA-256 | `a0a1ef89db96420ce375100a09f6236c13d47b10f3856f9c1963079b0613341b`; built, package and live staged DLL matched. Both starts validated the receipt. |
| Official agent-rig HEAD | `17ff9f86ef07a8c071a90886fa76ed2e98f56c57`, clean; fetch / ff-only pull already current. Includes agent-rig #89. No checkout edit, commit, push or overlay update. |
| Live AgentRigOps SHA-256 | `d3188b0e837188aec8be85535846953170da99807ad22914ccf4a3c38d77ed3c`; live binary contains `unrentable_fixture`, loaded health reports the same hash, unchanged at cleanup. First in-game fixture use recorded below. |
| RLO Player.log | `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` All 53 package files hash unchanged; no RLO file installation, edit or restoration. |
| PC Player.log | `[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places.` The full line is below. No commit-stamped build line is emitted; matching DLL hashes and the validated receipt establish binary identity. |
| Lease / resources | Free/stopped initially; acquired `2026-11-14T19:16:55.201839Z`. Pre-stage 64.0 GiB disk / 77% reported free memory; after filter timeout 61.0 GiB / 44%. No resource refusal, takeover or other-process termination. |
| Starts / loads / stops | Two windowed rig-test processes, PIDs 26504 and 26861; one attempt per start, both successful. Cold source load 51.5 s, disposable load 65.5 s. Two graceful stops; no force. Temporary frame cap 30/vsync 0 restored to original absence; no performance window. |
| Autosave / saves | Original native 5 minutes / 3 saves, prevent_autosave=false; #70 suppression confirmed before and after both loads and at finish. One explicit save created only `FT RIGTMP-GR5-14i`, before apply. In the applied-state process, guard attempts/default_allowed/armed_allowed/unrestricted all 0; native save/compression idle. No save after apply, including after revert. |
| RLO options | Before/after equal: living_population stored=null, live=true; social_media stored=1, live=true; social_media_notices stored=0, live=false; original_saved=false for all. No option mutation. All saved RLO preference values backed up and restored with the isolated plist. |

Exact staging command, game stopped and lease held, from official agent-rig:

```sh
python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package
```

Receipt backup `<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T191747Z`. Package contains no RLO files. Printed rollback, verbatim apart from `<home>` path substitution:

```sh
rm -rf "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T191747Z/mod" "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T191747Z/install.json" "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

Automatic command review rejected that shell command before execution: `rm -f style commands are not permitted. Use a safer approach`. The stopped, leased restore retained the candidate folder in raw evidence, copied the entire backup mod into the empty live path, then copied back the backup receipt. No receipt was hand-edited.

### Per-check results

| Check | Result | Evidence |
| --- | --- | --- |
| #246 item 4, unrentable Home footprint | PARTIAL | All three required housed subjects exist, including rotated and yard/sidewalk cases. Their native geometry and rig-policy floors were read. PC reads places before ops run and has no documented reload route that preserves this fixture, so applied PC area evidence/floors and PC-to-visible ratios are not observable in-load. Screenshots show roof/ground context; facade occlusion prevents a reliable independent storey count. |
| #246 item 5, unreadable / empty tally | PASS, documented fallback | All 5 unrentable rows have pc_home_candidate=true: housed=3, unreadable=1, empty=1. Both class filters match 1. Empty's sole enabled collider has height 0, floors 0, capacity 0; it is empty, not unreadable. PC's own line and omitted-places warning for the applied state are not observable in-load. This is rig `UnrentableHome` evidence under agent-rig's prescribed fallback, not a nonzero PC runtime tally. |

Followed agent-rig `docs/rig-fixtures.md` “Unrentable residential subjects” and `docs/rig-ops.md` building contract. Loaded `FT GR5 13f Timing v1`, paused, saved the new name `FT RIGTMP-GR5-14i`, stopped and cold-loaded that disposable copy. Status before apply: applied=false, subjects=[], missing=[]. Apply's `after`: applied=true, 5 subjects, **missing=[]**. Each subject had size `agentrig:unrentable_fixture`, rentable_interior=false and the role's expected class. Original sizes: `ba:buildingsize_l` for rotated/plain, `ba:buildingsize_b` for yard, `ba:buildingsize_n` for unreadable/empty.

| Role / place | Rotated | Yard / sidewalk | Rig world footprint / policy floors (height) | PC Home area evidence / floors | PC-to-visible ratio |
| --- | --- | --- | --- | --- | --- |
| housed_rotated / `address:ba%3Astreet_firststreet:22:home` | true | false | 1488.514 m² / 13 (48.719 m high) | not observable in-load | not observable in-load |
| housed_yard / `address:ba%3Astreet_fifthavenue:25:home` | false | true | 426.709 m² / 5 (18.389 m high) | not observable in-load | not observable in-load |
| housed_plain / `address:ba%3Astreet_broadwaystreet:2:home` | false | false | 3826.156 m² / 11 (40.332 m high) | not observable in-load | not observable in-load |
| unreadable / `address:ba%3Astreet_broadwaystreet:13:home` | false | false | unavailable / unavailable | not observable in-load | not observable in-load |
| empty / `address:ba%3Astreet_broadwaystreet:23:home` | false | false | 2528.192 m² / 0 (0.000 m high) | not observable in-load | not observable in-load |

For the housed subjects only, the geometry proxy comparison is below. Local collider area removes yaw; renderer AABB is a visible-bounds envelope that can include roof structures and parts. These are not measured ground polygons or independent storey counts. All world/local ratios are below 1.5; this alone does not pass check 4. The last column is **rig input arithmetic**, world footprint × policy floors × 0.6, not a Home value observed from Persistent City.

| Role | Local collider area m² | Visible renderer AABB area m² | Visible envelope height m | World / local area | World / renderer AABB area | Rig implied usable area m² |
| --- | --- | --- | --- | --- | --- | --- |
| housed_rotated | 1431.112 | 1493.488 | 49.920 | 1.040 | 0.997 | 11610.409 |
| housed_yard | 426.709 | 437.253 | 23.514 | 1.000 | 0.976 | 1280.127 |
| housed_plain | 3826.154 | 3826.155 | 47.418 | 1.000 | 1.000 | 25252.630 |

Rotated yaw is 268.953°. Yard ground/bottom plane extends beyond the collider envelope (outside_footprint=true); its part is present but active=false at readback, so the fixture's “yard/sidewalk” selector establishes geometric overlap, not an active yard visual. Nine independent camera PNGs from the windowed rig-test start are retained only in `<stage>`. All were opened for inspection. The top views retain roofs/street context. First facade views are partly blocked by neighbouring buildings; the rotated/yard close views are inside solid geometry and the plain close view is obscured by a tree. None is used to invent a visible-storey count.

### Tally, exact operations and tooling gaps

`building {"action":"list","rentable":false}`: residential=585, matched=5, total=5, housed=3, unreadable=1, empty=1. Every matched row is a PC Home candidate. `rotated:true` matches only 22 1st Street. The original `meets_yard_or_sidewalk:true` request's eventual reply matches only 25 5th Avenue. The class selectors unreadable and empty each match their respective Broadway address. No missing role or `ops_building_fixture_no_native_subject` occurred.

Exact in-game operation errors:

```text
python3 scripts/mac-agent op building '{"action":"list","rentable":false,"meets_yard_or_sidewalk":true}'
{"error":"ops_timeout_do_not_retry"}
```

The 30 s client deadline expired while the hook reported `ops_heartbeat_stalled`. No request replay. The hook later resumed naturally; the original response `62034df8a23d444f9e96b693ce3833ab.json` was read and copied to raw evidence with ok=true, matched=1 and the expected filters. Thus the selected yard subject is established, while the client timeout remains a tooling failure. No cause inside the rig is asserted.

```text
python3 scripts/mac-agent op building '{"action":"read","place":"address:ba%3Astreet_broadwaystreet:13:home"}'
{"error":"ops_building_bounds_unavailable"}
```

This is the deliberately collider-free unreadable subject. Its detailed geometry is NOT RUN / tooling gap; the successful class/list replies establish the fallback tally. `read` for the empty subject succeeded. Every successful geometry read returns storeys=null / `ops_building_storeys_not_native`, the documented native-storey limitation, not another dispatch error. PC Home evidence, applied PC tally and applied PC warning are not observable in-load: the docs explicitly prohibit saving this applied fixture and state that reload would lose the disabled-collider part. No applied-state reload/save was attempted.

### Player.log excerpts and observed durations

Both loads log:

```text
[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.
[Persistent City] unrentable homes housed=0 beds=0 unreadable=0 empty=0
[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.
```

The zero tally describes the city at load, before apply. It is **not observable in-load** for the applied state and is not a mismatch. Temporary load-time hook warnings (`ops_heartbeat_stalled` in A, `ops_pump_missing` in B) recovered with `ops_pump_resumed`; both bounded loads completed. The yard-filter interval additionally logs:

```text
[AgentTests] Rig ops: disabled (ops_heartbeat_stalled)
[AgentTests] Rig ops: ready (ops_pump_resumed)
```

No perf, GC, modwork or save-profiler measurement was armed. Already-emitted PC log observations at or above 30 ms are listed below; component durations are not whole-frame measurements, and cumulative totals are not spikes.

| Process / log evidence | Observed single-call / maximum durations >=30 ms |
| --- | --- |
| A, source fixture, Slow routing frames 775 / 860 | preparation_ms=130.646 / 77.051 |
| A, Population observation | maximum advance 160.440; slowest unit food-continuation 160.394; historical planner scene-search maximum 121.085 (overlaps preparation evidence) |
| B, disposable fixture, Slow routing frames 868 / 1019 | preparation_ms=215.592; closure_ms=46.749 |
| B, first Population observation | maximum advance 178.292; slowest unit food-continuation 178.027; historical planner scene-search maximum 185.771 (overlaps preparation evidence) |
| B, next Population observation | maximum advance 154.314; slowest unit food-continuation 154.264 |

These logs do not establish event-by-event spike counts or p95. No extra timing window was run and no performance investigation followed the spikes.

### NOT RUN grouped

1. **GR-5 GC/street re-checks:** excluded; the seventh fix, pause/advance/street, is still in progress on this Mac; recheck follows its merge. Other session/worktree untouched.
2. **#262 checks 4–5:** excluded; no stable walker resident identity across reacquire, and no dense-street 48–96 walker fixture with paired refill. Rig bot gaps.
3. **GR-5 60 fps arms:** excluded, Windows-only; PC rig profile lacks FT fixtures, a Rig bot gap. Windows was not contacted or started.
4. **GR-5 save latency:** excluded; owner decision pending. The single disposable-copy save was setup only, with no save latency measurement.

Partial-check omissions are not counted again: PC's applied Home evidence/tally/warning, independent visible storeys and unreadable subject detailed geometry remain unverified as described above. No production changes, release, Workshop upload, version/changelog update, issue, reviewers, bot messages or merge. Schema stays 1; FoodChoicePriceWeight and FoodChoiceHouseholdWeight unchanged.

### Fixtures, restoration and validation

Reused source `FT GR5 13f Timing v1`; created only disposable copy **`FT RIGTMP-GR5-14i`** before mutation. The source fixture's four matching save/metadata file hashes are unchanged. Applied fixture was reverted under the lease; after.applied=false, subjects=[], missing=[], and a fresh unrentable list reports matched=0 / all class counts 0. Quit without saving. The disposable save remains in the isolated profile; no undocumented removal.

Independent pre-stage backup covered 35 files and 4 absent paths: whole PC package, receipt, isolated root configuration/preferences, PlayerSettings, persistent-city.json and isolated plist. Original native autosave and RLO options were recorded. After graceful stop, the whole original package/receipt was restored through the non-destructive equivalent above; isolated preference cache was imported from the backup, then original file bytes restored. Original frame-cap absence restored. All **35 original file hashes / 4 absent paths / exact PC file set / 53 RLO file hashes / 4 source fixture file hashes match; zero mismatches**. install.json SHA-256 `dea36ce03586cc53d3b2c7e5cd0fda6d9a54e61c670a21d0676ad18feee54a95`. AgentRigOps hash unchanged. All three RLO option readbacks match. Nine helper PNGs were copied/hash-verified to raw evidence and removed from their own helper output paths while stopped.

Lease released; final running=false, process=null, held=false, pending=0. Owned PIDs 26504/26861 and their caffeinate assertions are gone. Official agent-rig and original PC checkout clean. Build/evidence worktree is removed after commit, push and opening the unmerged docs-only PR. Only this existing checklist changes; raw evidence and report copy stay outside Git.

Offline validation: candidate build passed. Docs-links, markdown-ban and whitespace checks are recorded in the PR. No test source changed, no focused classes, no full suite. Final fetch left the candidate tip unchanged at `65b41db5`.

Refs #246

## Rig results — 14 November 2026 j, Mac (GR-5 GC recheck after #266 at 50f52c18)

**RESULT: DONE. Counts: 2 passed / 2 partial / 0 failed / 5 NOT RUN groups.** Day GC meets #266's allocation/count/heap expectation; night heap and pool shrink. Street **UpdateCity / mod-work p95 = 3.7732 / 3.8197 ms**, both below 5 ms, with **three >=30 ms spikes**. Their body-growth attribution is unverified, so the street check is partial. Look stability is partial through the resident-reacquire tooling gap. In-game proof gates no merge; no merge was performed.

Role `rig_test`, where `mac`, isolated `agentmac`, owner `persistent-city-rig-2026-11-14j-mac`. `<stage>` = `<home>/HART-Builds/persistent-city-rig-2026-11-14j-mac`. [Complete GC reports and compact evidence](perf/gr5-2026-11-14j-mac.json): **15,640 bytes**, SHA-256 `67f6d43e6a734ffbc1b8f350a3e508423e42b676ae5437f37f53d99f048ca1dc`. Raw command replies, logs, scripts, nine inspected PNGs and backups stay in `<stage>`. Raw manifest SHA-256 `267c207a05ed25b04db202ba2788216f0a7d8840d0f62a598f8ba0f115d00629`. Report copy: `<home>/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-14j-mac.md`.

### Build and rig

| Item | Evidence |
| --- | --- |
| Candidate | `50f52c18a840260daa7dec1ae47b51199d7db712`, initial and final fetched `origin/codex/persistent-city` agree; #266 included. |
| Worktree / branch | New `rig-2026-11-14j-mac-evidence` / `codex/mac-rig-2026-11-14j`; one isolated worktree served build and evidence. Original checkout unchanged. |
| Official agent-rig HEAD | `17ff9f86ef07a8c071a90886fa76ed2e98f56c57`; clean; fetch and ff-only pull already current. No tooling edit, build or overlay update. |
| Live AgentRigOps SHA-256 | `d3188b0e837188aec8be85535846953170da99807ad22914ccf4a3c38d77ed3c`; pre-start, loaded health and cleanup agree. |
| Build | `PATH=/opt/homebrew/bin:$PATH`, `DOTNET_ROOT` from Homebrew dotnet; `python3 tools/build.py --game-managed "<home>/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0. |
| PersistentCity DLL SHA-256 | `d1114d9094da71bb18f4137a6a446211c0654c235a7ca14d81f3514e0590247d`. Staged package contains no RLO or game DLL. |
| Loaded build identity | Candidate module ID `598a81ab-915b-4c39-a889-0f06c6c69325` matches loaded health. Receipt-validated starts accepted the staged hashes. No commit-stamped build line is emitted; the exact activation line is quoted below. A brief local module-ID inspection ran during the street window; shared-host cadence is observational. |
| Stage | `python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package`; stopped and leased. Receipt backup `<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T201646Z`; refreshed 28 scoped receipt entries. |
| Starts | One successful headless `start --mode headless --rig-test`, PID 41540; one successful cold windowed `start --mode windowed --rig-test`, PID 50499. No forced stop or load failure. |
| B frame cap | Isolated `agent-frame-cap.json` = `{"fps":30,"vsync":0}`; final applied=true, target=30, vSync=0; final short reading 5.72 fps. This is visual evidence, not a 30/60 fps performance arm. |
| RLO | `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` living_population stored absent/live=true; social_media=1/true; social_media_notices=0/false. No option toggle; no RLO file edit. |
| Autosave / SaveCost | Original native 5 minutes / 3 saves, prevent_autosave=false. #70 guard enabled and suppressed before/after each window and after both bounded loads. All window allowed saves and native saving frames = 0. SaveCostDiagnostics requested=false, writerSchema=1, save=null, activation=null; native save profiler installed=false. No manual save or save-latency measurement. |
| Lease / resources | Free at inspection; acquired 20:15:50.794588 UTC. Initial resources 62.9 GiB disk / 85% memory free; before B 62.8 GiB / 77%. No takeover, resource refusal, PC contact, other-session intervention or bot message. |

Printed rollback, path-sanitized:

```sh
rm -rf "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T201646Z/mod" "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T201646Z/install.json" "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

Cleanup used the stopped, leased equivalent that retains the candidate in `<stage>/staged-candidate-restored-out`, copies the complete backup mod into the empty live path and copies the exact backup receipt. No install receipt was hand-edited.

### Per-check outcome against each Expected line

| #266 check | Result | Expected, and observed |
| --- | --- | --- |
| 1. Day GC, 15:30 / 600 s | PASS | **Expected: advance clearly below 209 MB, gen0 at or below 7, and heap no higher than 14g. Met:** advance 107.466 MB, gen0=7, heap_after_gc 1,321,669,778 bytes, Mono heap 1,527,726,080 bytes; both heap readings below 14g. |
| 2. Night GC, 01:00 / 600 s after speed 20 | PASS | **Expected: owned bodies falling toward 16 after about 2 minutes idle, trimmed rising, and a smaller heap than 14g. Met in the observed idle period:** owned peaks at 42, first recorded trim by snapshot 300 s, reaches 17 by 420 s and 16 by 540 s; trimmed 0→28; Mono heap 1,579,483,136 < 1,894,055,936. Individual-body idle ages are not exposed; the full sampled trajectory is below, rather than a claim that the pool reached 16 at wall-clock t=120 s. |
| 3. Street / 600 s with pans | PARTIAL | **Expected: no spike >=30 ms from body growth; p95 under 5 ms at about 28 fps or more. At about 6 fps, p95 follows the reported build ms.** Strict <5 ms is met: 3.7732/3.8197 ms at 8.13 fps. Three UpdateCity spikes remain; no body-growth subcomponent was timed, so the no-growth-spike clause is **unverified**, not passed. Build estimates are 2.1–2.8 ms over the timed interval; the full-window p95 is compatible with build plus other UpdateCity work, but those summaries cannot prove per-frame correlation. Required 28+ fps is not reached. |
| 4. Look stability / cold windowed | PARTIAL / tooling gap | **Expected: the same look before and after, no undressed or invisible bodies, and no “Appearance meshes unavailable” or “Empty appearance” in the log.** Same-resident comparison unverified: two native targets went stale on return in 11.55/11.47 s. 120.437 s pan completed; nine stills inspected. Visible bodies are dressed; sparse/occluded return views and still captures cannot prove no invisible body or brief flash. All four required error strings have zero matches in both logs. |

The pass counts refer to the GC expectation lines, not every historical GR-5 target. The street p95 gate passes; its spike-source proof remains partial. No weaker numerical gate is substituted.

### GC windows and every allocation segment

Fixture **`FT GR5 13f Timing v1`**, confirmed 1,000 residents / 2,000 visitors / 1,683 places, person-target off, player x=215,z=0. No fallback was needed. Day Tuesday 15:30, night Wednesday 01:00. Speed=1 in each measurement; mod-work sampler unarmed. Untimed speed-20 advances occur outside windows. GC sampler incremental=true, max_generation=0; pause is cycle-ending frame excess, not all incremental collector work. Allocation counter=`boehm_total`; legacy frame allocation remains `heap_used`. MB/GB comparisons use decimal bytes; the logged KB scratch field is bytes/1024.

| Window | Stored / dropped frames | gen0/1/2 | Pause p50 / p95 / max ms | heap_after_gc_bytes | mono_heap_bytes | Mod-work in GC frames p95 / max ms |
| --- | --- | --- | --- | --- | --- | --- |
| day | 16846 / 0 | 7/0/0 | 26.799/28.581/28.581 | 1321669778 | 1527726080 | 2.353 / 2.353 |
| night | 17079 / 0 | 6/0/0 | 28.562/32.902/32.902 | 1372768256 | 1579483136 | 0.88 / 0.88 |

| Reference / metric | 14b day / night | 14g day / night | 14j day / night |
| --- | --- | --- | --- |
| gen0 | 11 / 8 | 7 / 6 | 7 / 6 |
| Pause p95 ms | 28.191 / 61.316 | 67.048 / 141.801 | 28.581 / 32.902 |
| Physical+advance MB | 1,021.587 / 433.642 | 309.570 / 134.021 | 200.927 / 68.086 |
| Advance MB | 236.448 / 72.746 | 209.051 / 71.735 | 107.466 / 33.489 |
| Mono heap bytes | 1,535,655,936 / 1,578,450,944 | 1,629,356,032 / 1,894,055,936 | 1,527,726,080 / 1,579,483,136 |

Day advance is 48.6% below 14g; night 53.3% below. Collection counts equal 14g and below 14b. Pause p95 day is 1.4% above 14b and 57.4% below 14g; night is 46.3% below 14b and 76.8% below 14g. These are measured comparisons, not controlled attribution across shared-host runs.

Every measured allocation segment follows, **bytes / samples / skipped**. Randomize and bake overlap appearance and are not added again. Four advance children sum to advance.

| Allocation segment | Day bytes / samples / skipped | Night bytes / samples / skipped |
| --- | --- | --- |
| mod | 315570912 / 16847 / 0 | 180623280 / 17080 / 0 |
| advance | 107466400 / 16753 / 0 | 33488696 / 16995 / 0 |
| physical | 93461016 / 16753 / 0 | 34597112 / 16995 / 0 |
| physical_ambient | 3219792 / 16753 / 0 | 3266032 / 16995 / 0 |
| physical_vehicles | 317664 / 16753 / 0 | 223296 / 16995 / 0 |
| physical_bindings | 111456 / 534725 / 0 | 736 / 120755 / 0 |
| physical_vendor_meals | 0 / 16753 / 0 | 0 / 16995 / 0 |
| physical_fade | 0 / 16753 / 0 | 0 / 16995 / 0 |
| physical_focus | 0 / 16753 / 0 | 0 / 16995 / 0 |
| physical_observe | 84624 / 517682 / 0 | 58928 / 103656 / 0 |
| physical_release | 7184 / 291 / 0 | 8112 / 132 / 0 |
| physical_parked | 1966928 / 584 / 0 | 1501664 / 587 / 0 |
| physical_index | 0 / 2198 / 0 | 80 / 2203 / 0 |
| physical_queues | 8960 / 2198 / 0 | 17200 / 2203 / 0 |
| physical_roster | 286800 / 2198 / 0 | 207808 / 2203 / 0 |
| physical_selection | 597032 / 2690 / 0 | 750296 / 2376 / 0 |
| physical_spawn_pedestrian | 30124416 / 829 / 0 | 13464480 / 230 / 0 |
| physical_spawn_appearance | 56698832 / 1074 / 0 | 15030256 / 273 / 0 |
| physical_spawn_check | 0 / 716 / 0 | 0 / 182 / 0 |
| physical_spawn_vehicle | 37328 / 21 / 0 | 68224 / 34 / 0 |
| physical_unattributed | 0 / 33506 / 0 | 0 / 33990 / 0 |
| physical_spawn_randomize | 3179248 / 358 / 0 | 832256 / 91 / 0 |
| physical_spawn_bake | 2659968 / 358 / 0 | 1578000 / 91 / 0 |
| advance_planning | 52017312 / 9424 / 0 | 22199992 / 3947 / 0 |
| advance_events | 55449088 / 21638 / 0 | 11288704 / 19875 / 0 |
| advance_stops | 0 / 16753 / 0 | 0 / 16995 / 0 |
| advance_other | 0 / 16753 / 0 | 0 / 16995 / 0 |

| Other counter | Day | Night |
| --- | --- | --- |
| counter_frame_bytes | 1631773400 | 1533437240 |
| counter_frames | 16846 | 17079 |
| frame_alloc_bytes | 1133277184 | 946946048 |
| max_mod_ms | 56.67 | 79.019 |
| gc_spikes_30ms | 7 | 6 |
| frame_spikes_30ms | 16834 | 17076 |

GC `max_mod_ms` is 56.670/79.019 ms. `pc gc stop` exposes aggregate maxima/counts, no frame/component spike list, so no exact-frame rows can be recovered for those GC maxima. This is a sampler readback limit, not zero spikes. Every exported **street mod-work** spike is listed below. Whole-frame counts are separate from mod-work and reflect the low frame rate.

### Residual and retained-state decision

- **day:** largest disjoint parent advance = 107,466,400 bytes, 53.49% of physical+advance. Largest advance subsegment `advance_events` = 55,449,088 bytes (51.60% of advance). Largest physical child `physical_spawn_appearance` = 56,698,832 bytes (60.67% of physical). Appearance 56,698,832 bytes = **7.61% of 14b**; appearance excluding randomize and bake = 50,859,616; bake plus that remainder = 53,519,584 bytes = **7.18% of 14b appearance**. Both appearance readings are below ~10%.
- **night:** largest disjoint parent physical = 34,597,112 bytes, 50.81% of physical+advance. Largest advance subsegment `advance_planning` = 22,199,992 bytes (66.29% of advance). Largest physical child `physical_spawn_appearance` = 15,030,256 bytes (43.44% of physical). Appearance 15,030,256 bytes = **4.59% of 14b**; appearance excluding randomize and bake = 12,620,000; bake plus that remainder = 14,198,000 bytes = **4.33% of 14b appearance**. Both appearance readings are below ~10%.

For clarity, the broader physical+advance residual is **19.67% / 15.70% of 14b**, above 10%; that is not the appearance/combiner trigger. No cause inside advance_events/planning is inferred from the names alone. #266's unimplemented entry projection and native spawn activation remain possible follow-up subjects, not proven causes here.

Bake scratch peaks at **45 sizes / 1,678 KB = 1.718 MB**, about **0.11% of the day Mono heap**. That is small against this heap and the model's 23 MB bake caches; the measured scratch does not support prioritizing a cap as the next slice. Scratch sizes/bytes reset when the physical population is recreated for speed mode; the report does not treat this as a process-wide permanent high-water mark.

### AppearanceBuilds, AppearanceReuses, pool hit rate and pedestrianLooks

Last periodic snapshots, not exact start-to-stop counter deltas. Pool hit rate is reused/(built+reused). GC randomize and bake each have **358 / 91 samples** day/night, giving the exact dressed-build counts inside those spans. The periodic logs are intentionally distinguished from those counts.

| Window | AppearanceBuilds / AppearanceReuses | Pool hit % | Idle / owned | Grown / trimmed | Fitted / waited | ms build | bake sizes / KB bake scratch |
| --- | --- | --- | --- | --- | --- | --- | --- |
| day GC | 298 / 16 | 5.10 | 15 / 88 | 88 / 1 | 238 / 146 | 2.4 | 45 / 1678 |
| night GC | 92 / 1 | 1.08 | 16 / 16 | 43 / 28 | 4 / 129 | 3.3 | 44 / 1634 |
| street | 148 / 7 | 4.52 | 27 / 45 | 54 / 10 | 40 / 160 | 2.1 | 45 / 1678 |

Night owned/idle over time: each row is the last periodic log seen in a snapshot taken at the stated wall-clock offset. Logs update about once a minute, so these are not exact event timestamps or per-body idle-age measurements.

| Snapshot seconds | Owned | Idle | Trimmed | Grown |
| --- | --- | --- | --- | --- |
| 60 | 2 | 1 | 0 | 1 |
| 120 | 28 | 15 | 0 | 27 |
| 180 | 42 | 21 | 0 | 41 |
| 240 | 42 | 35 | 0 | 41 |
| 300 | 41 | 33 | 1 | 41 |
| 360 | 29 | 24 | 13 | 41 |
| 420 | 17 | 16 | 25 | 41 |
| 480 | 18 | 16 | 26 | 43 |
| 540 | 16 | 16 | 28 | 43 |
| 600 | 16 | 16 | 28 | 43 |

### Street timing and every mod-work spike >=30 ms

Wednesday 15:30, same process/fixture after night and untimed advance; unlike 14g's corrective reload, no street reload was needed. GC sampler stopped, player x=215,z=0, nine camera pans successful. Native guard interval 601.677 s includes setup/end readback; timed mod-work interval below. No save allowed, no native saving frame. Coverage.complete=true, validity.valid=true, zero off-main-thread/cross-frame/unbalanced/depth-overflow/drop faults.

| Window | Seconds / frames / achieved fps | UpdateCity p95 / max ms | Mod-work p95 / max ms / >=30 spikes | Whole-frame p95 / max ms / >=30 count |
| --- | --- | --- | --- | --- |
| 14j | 600.815 / 4887 / 8.13 | 3.7732 / 70.1801 | 3.8197 / 70.2369 / 3 | 176.7198 / 450.1921 / 4863 |
| 14g | 600.399 / 3485 / 5.80 | 5.2988 / 64.7304 | 5.3683 / 64.7872 / 1 | 200.5798 / 506.5189 / 3485 |
| 14b | 600.284 / 3370 / 5.61 | 5.5170 / 78.1533 | 5.5824 / 78.2031 / 4 | 238.9091 / 1002.2687 / 3370 |

| Unity frame | Mod-work ms | UpdateCity ms | Dominant component / group | Whole-frame ms | Condition |
| --- | --- | --- | --- | --- | --- |
| 51358 | 70.2369 | 70.1801 | updatecity / updatecity | 103.8241 | speed=1|hour=15 |
| 54091 | 30.9771 | 30.9255 | updatecity / updatecity | 314.6618 | speed=1|hour=16 |
| 55161 | 33.2070 | 33.1629 | updatecity / updatecity | 200.2013 | speed=1|hour=17 |

All three spikes are reported, not chased. UpdateCity is the measured component boundary, so body growth, dressing, advance or another inner source cannot be assigned from these rows. No clean no-body-growth-spike claim follows from the improved p95. UpdateCity p95 is 28.8% below 14g; mod-work p95 is 28.8% below. Achieved cadence and street day differ; no isolated causal claim is made. `NativeCityClock.MaximumLagMinutes=1.0` both during untimed setup and after the window; session maximum includes advances. Final lag=0, caughtUpToTarget=true, refused=false, stoppedAfterAccountingFailure=false, historicalClockMisses=0. No runtime stop occurred.

### Look evidence, exact failures and tooling gaps

Cold windowed rig-test start, same fixture Tuesday 15:30, player x=215,z=0. Two native walker targets were followed and photographed, camera pointed away for 8 s, then their positions and camera-follow were requested again. The returned screenshots occurred within 11.549/11.465 s of departure; both handles were stale. Exact failed operations:

- `python3 scripts/mac-agent op screen_pos '{"action":"get","target":{"kind":"walker","id":"walker-09edc5be52bf4429b628df3fac486280"}}'` → `ops_view_stale_target`.
- `python3 scripts/mac-agent op camera '{"action":"follow","target":{"kind":"walker","id":"walker-09edc5be52bf4429b628df3fac486280"},"offset":{"x":0,"y":4,"z":-6},"seconds":10}'` → `ops_view_stale_target`.
- `python3 scripts/mac-agent op screen_pos '{"action":"get","target":{"kind":"walker","id":"walker-4e18f46e62c24da2b8bfbd748589e425"}}'` → `ops_view_stale_target`.
- `python3 scripts/mac-agent op camera '{"action":"follow","target":{"kind":"walker","id":"walker-4e18f46e62c24da2b8bfbd748589e425"},"offset":{"x":0,"y":4,"z":-6},"seconds":10}'` → `ops_view_stale_target`.

These are native walker handles, with no durable Persistent City resident identity or release/reacquire guarantee. No resident-reacquire op is advertised. No weaker same-look proof is substituted. Two before images show a yellow top/dark trousers and a blue top/shorts; return views contain other walkers or empty road. They are not an appearance-change failure. The 120.437 s pan used 24 camera operations and five additional images. All nine PNGs inspected; visible walkers are dressed, but empty/occluded views and sparse still sampling leave brief flashes/invisible bodies unproven. No refill benchmark was run; #262 dense-walker proof remains excluded.

| Exact Player.log string | A matches | B matches |
| --- | --- | --- |
| Appearance meshes unavailable | 0 | 0 |
| Empty appearance | 0 | 0 |
| Merged body already has blendshapes | 0 | 0 |
| Merged body vertex layout differs | 0 | 0 |

Other observation: the first `call console '{"line":"pc gc population"}'` just after bounded load completion returned `Warning: [Persistent City] gc population refused: no active city.` The native load had completed before the mod's asynchronous activation. No window opened then; the next query confirmed reached=3000 / 1000 residents / 2000 visitors before positioning and sampling. No ops_load_* failure, replay, staging refusal or runtime stop occurred.

Tooling limits within partial checks: durable resident reacquisition; body-growth attribution beneath the UpdateCity timing boundary; GC sampler frame/component lists for its aggregate maxima; no commit-stamped build log line. Exact invoked operations and available provenance are retained above. These omissions are not counted again as excluded groups.

### Player.log excerpts: build, speed changes and toggles

```text
[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.
[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.
A [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 2 15:30.
A [Mod:FoodTrucks] Test bridge: run_until day 2 15:30 reached at day 2 15:30; paused, speed 1.
A [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 01:00.
A [Mod:FoodTrucks] Test bridge: run_until day 3 01:00 reached at day 3 01:00; paused, speed 1.
A [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 15:30.
A [Mod:FoodTrucks] Test bridge: run_until day 3 15:30 reached at day 3 15:30; paused, speed 1.
B [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 2 15:30.
B [Mod:FoodTrucks] Test bridge: run_until day 2 15:30 reached at day 2 15:30; paused, speed 1.
```

Explicit speed=1 replies are retained before each measured window and the visual check. No RLO toggle occurred. Transient startup NotReady/Flare messages precede successful activation; they are not a runtime stop.

### NOT RUN grouped

1. **GR-5 60 fps arms:** excluded; Mac reaches about 5 fps at 1,000 residents, and the PC throwaway time/perf/modwork fix was not landed for this request. No Windows PC contact or start.
2. **GR-5 save latency:** owner decision pending. SaveCostDiagnostics off, no save measurement or manual save.
3. **#262 checks 4–5 full identity/dense-walker proof:** Rig bot fixtures pending. Check 4's limited visual attempt above does not replace stable identity or a 48–96-walker paired refill. Refill outcome: NOT RUN.
4. **#246 check 4 full proof:** Rig bot in-load observation gap; excluded, not repeated here.
5. **Everything outside A/B:** excluded by scope, including other food, footprint, P-D and RLO-toggle checks. No new fixture, throwaway world or unrelated acceptance arm.

### Fixtures, restoration and validation

Only **`FT GR5 13f Timing v1`** was loaded, twice via `python3 scripts/mac-agent load '{"name":"FT GR5 13f Timing v1"}'`; both bounded loads completed. `FT RLO 13d Food v1` was not needed or loaded. No fixture save was created, changed by a save command, overwritten or removed.

Stopped only owned PIDs **41540 / 50499**, gracefully. While stopped and leased, restored full PersistentCity and exact install.json from receipt backup; imported this run's isolated preference backup and restored exact original file bytes, PlayerSettings, runtime configuration, persistent-city config and frame-cap absence. Native autosave remained 5 minutes / 3 saves; process-only suppression ended at stop. RLO live values matched their original stored/default values before stop; no original-saved toggle record. **32 original file hashes / 3 absent paths / exact PersistentCity file set / 53 RLO file hashes match; zero mismatches.** install.json SHA-256 `dea36ce03586cc53d3b2c7e5cd0fda6d9a54e61c670a21d0676ad18feee54a95`; AgentRigOps hash unchanged. All nine own helper PNGs copied/hash-verified into `<stage>` and removed only from their own helper output paths.

Lease released: running=false, process=null, held=false, pending=0. Both owned PIDs and their caffeinate assertions are gone. Official agent-rig and original Persistent City checkout clean. Evidence/build worktree is removed after commit, push and opening the PR. No production code, RLO package, schema 1, FoodChoicePriceWeight (1.2), FoodChoiceHouseholdWeight, version/changelog, release, Workshop, issue, reviewers or merge changed.

Validation: candidate build passed; compact JSON parses and is under 20 KB. Docs-links, markdown-ban and whitespace checks recorded with the PR. No test source changed; no focused test classes, full suite or regression suite. Final fetch left the candidate at 50f52c18. All long commands ran in the foreground and were awaited.

Refs #266

## Rig results — 14 November 2026 k, Mac (GR-5 GC/traffic recheck after #268 at 8eb87be2)

**0 passed / 0 partial / 3 failed / 5 NOT RUN groups. RESULT: DONE.** All three required 600 s windows completed in one fresh headless rig-test process. Counts use the GC gate for day/night and the full traffic expectation for street; completion is not a GC/street acceptance claim.

Role `rig_test`, where `mac`, isolated `agentmac`, owner `persistent-city-rig-2026-11-14k-mac`. `<stage>` = `<home>/HART-Builds/persistent-city-rig-2026-11-14k-mac`. Raw logs, replies, scripts and backups remain outside Git. Report copy: `<home>/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-14k-mac.md`.

[Complete GC reports and compact timing evidence](perf/gr5-2026-11-14k-mac.json): 17,557 bytes, SHA-256 `793d2535e9090707900647ea6ec30a55f93dbe8625320704ae0c4851ab307ef5`. Raw manifest `<stage>/raw-evidence-manifest.json`, SHA-256 `e95cbacfc0c34c7ac1b1b1223db3d2507b95966e9fd6cfc7a86232aea558ac61`.

### Build and rig

| Item | Evidence |
| --- | --- |
| Candidate | `8eb87be2c622357daa2cd6f11e0af590dc23f2d5`; initial tip matched the requested merge. Final fetched tip `8eb87be2c622357daa2cd6f11e0af590dc23f2d5`. Tested binary stayed at the initial SHA. |
| Branch / worktree | New `codex/mac-rig-2026-11-14k` / `rig-2026-11-14k-mac-evidence`; one isolated worktree served build/evidence. Original checkout and other sessions untouched. |
| Official agent-rig HEAD | `17ff9f86ef07a8c071a90886fa76ed2e98f56c57`; clean, fetch and ff-only pull current. No tooling edits or rig overlay update. |
| Live AgentRigOps SHA-256 | `d3188b0e837188aec8be85535846953170da99807ad22914ccf4a3c38d77ed3c`; pre-start, loaded health and cleanup agree. |
| Build | Homebrew PATH / DOTNET_ROOT; `python3 tools/build.py --game-managed "<home>/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0. |
| PersistentCity DLL SHA-256 | `365d427a2b968223d1d19c6d5ca63bfbc440fd01a63627a35e2509dcf7733ecf`; built package and live staged DLL matched; start validated receipt. Package has no RLO or game DLL. |
| Loaded build | Package MVID `45939a7e-b32e-41bb-9936-8638d625d052` matches loaded health (inspection after measurement). No commit-stamped build line is emitted; hash-validated receipt and activation establish provenance. |
| Stage | `python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package`; stopped and leased; refreshed 28 scoped receipt entries. Receipt backup `<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T215916Z`. |
| Start | One headless `start --mode headless --rig-test`, PID 74916; one bounded load. No load failure, cold restart or forced stop. |
| RLO | `Real Life Overhaul 1.0.6 initialised.` living_population stored absent/live=true; social_media=1; social_media_notices=0. No option toggle or RLO file edit. |
| Autosave / SaveCost | Original native 5 minutes / 3 saves, prevent_autosave=false. #70 process guard enabled/suppressed after load and before/after each window. SaveCostDiagnostics requested=false, writerSchema=1, save=null, activation=null; native save profiler installed=false. No manual save or save-latency measurement. |
| Lease / resources | Free/stopped initially; acquired 2026-11-14T21:58:32.3102350Z. Pre-start 63.5 GiB disk / 75% reported memory free. No other session intervention, bot message or Windows contact. |

Printed rollback, path-redacted:

```sh
rm -rf "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T215916Z/mod" "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T215916Z/install.json" "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

### Per-check Expected lines and outcomes

The REST body of PR #268 contains an “In-game checks” instruction and TARGETS, but no separately labelled Expected lines or completed before/after implementation table. The Expected lines below apply its stated target and this run’s explicit GC/street gates; no numerical criterion is relaxed. Offline proxy targets (below 44.73/4.36 MB, busy below 9,303 B/event, idle 0 B) are not re-tested by this in-game evidence.

- **1. Day, 15:30 / 600 s: FAIL. Expected:** report all requested counters; GC pause p95 under about 36 ms **and** gen0 at or under about 5 per window. Day target from #268: advance well under 100 MB per in-game 600 s day window. **Observed:** gen0 6, pause p95 219.813 ms, advance 85.040 MB. GC gate not met. Day advance target: 85.040 MB (15.0% below 100 MB); day advance target met at this measured level.
- **2. Night, 01:00 / 600 s after speed 20: FAIL. Expected:** report all requested counters; GC pause p95 under about 36 ms **and** gen0 at or under about 5 per window. Day target from #268: advance well under 100 MB per in-game 600 s day window. **Observed:** gen0 7, pause p95 234.512 ms, advance 35.434 MB. GC gate not met. The day-only allocation threshold is not silently applied to night.
- **3. Headless street, 600 s with camera pans: FAIL. Expected:** busy-street traffic and arrivals look unchanged versus 14j (same journeys, forecasts, results); UpdateCity/mod-work p95 <5 ms and every >=30 ms spike reported. Timed union and operational counters are below. Headless has no rendered traffic/arrival comparison, so unchanged visual behaviour is unverified; no weaker visual pass substituted.

### GC windows, timing and baselines

Fixture `FT GR5 13f Timing v1`, 1,000 residents / 2,000 visitors / 1,683 places, person-target off, player x=215,z=0. No fallback, new city or save. Day Tuesday 15:30; night Wednesday 01:00; street Wednesday 15:30. Speed=1 in windows, speed-20 advances outside. GC counter=boehm_total; legacy frame_alloc_bytes=heap_used. Incremental=true, max_generation=0; pause is cycle-ending frame excess, not total incremental collector work. MB means decimal bytes.

| Window | frames / dropped | gen0/1/2 | pause p50 / p95 / max ms | heap_after_gc_bytes | mono_heap_bytes | physical+advance MB | advance MB |
| --- | --- | --- | --- | --- | --- | --- | --- |
| gc-day | 15066 / 0 | 6/0/0 | 50.486/219.813/219.813 | 1367141034 | 1568964608 | 156.411 | 85.040 |
| gc-night | 16697 / 0 | 7/0/0 | 33.347/234.512/234.512 | 1333411254 | 1549402112 | 75.158 | 35.434 |

| Metric | 14b day / night | 14g day / night | 14j day / night | 14k day / night |
| --- | --- | --- | --- | --- |
| gen0 | 11 / 8 | 7 / 6 | 7 / 6 | 6 / 7 |
| pause p95 ms | 28.191 / 61.316 | 67.048 / 141.801 | 28.581 / 32.902 | 219.813 / 234.512 |
| physical+advance MB | 1021.587 / 433.642 | 309.570 / 134.021 | 200.927 / 68.086 | 156.411 / 75.158 |
| advance MB | 236.448 / 72.746 | 209.051 / 71.735 | 107.466 / 33.489 | 85.040 / 35.434 |

gc-day vs 14j: gen0 -1; pause p95 +191.232 ms; advance -22.426 MB (-20.9%).
gc-night vs 14j: gen0 +1; pause p95 +201.610 ms; advance +1.946 MB (+5.8%).

Every allocation segment below is bytes / samples / skipped. Randomize and bake overlap appearance and are not added again. Four advance children sum to advance; no finer advance sub-segments are exposed by this candidate.

| Allocation segment | Day bytes / samples / skipped | Night bytes / samples / skipped |
| --- | --- | --- |
| mod | 283686640 / 15067 / 0 | 190858008 / 16698 / 0 |
| advance | 85039992 / 14956 / 0 | 35434288 / 16625 / 0 |
| physical | 71370752 / 14956 / 0 | 39723784 / 16625 / 0 |
| physical_ambient | 2874768 / 14956 / 0 | 3193840 / 16625 / 0 |
| physical_vehicles | 1174928 / 14956 / 0 | 571776 / 16625 / 0 |
| physical_bindings | 17488 / 386991 / 0 | 17376 / 131893 / 0 |
| physical_vendor_meals | 0 / 14956 / 0 | 0 / 16625 / 0 |
| physical_fade | 0 / 14956 / 0 | 0 / 16625 / 0 |
| physical_focus | 0 / 14956 / 0 | 0 / 16625 / 0 |
| physical_observe | 398320 / 371790 / 0 | 144432 / 115147 / 0 |
| physical_release | 9888 / 246 / 0 | 8880 / 152 / 0 |
| physical_parked | 1919152 / 576 / 0 | 1708304 / 585 / 0 |
| physical_index | 80 / 2123 / 0 | 80 / 2184 / 0 |
| physical_queues | 9152 / 2123 / 0 | 17200 / 2184 / 0 |
| physical_roster | 287904 / 2123 / 0 | 193024 / 2184 / 0 |
| physical_selection | 1291128 / 2533 / 0 | 592168 / 2360 / 0 |
| physical_spawn_pedestrian | 21737976 / 569 / 0 | 14445456 / 250 / 0 |
| physical_spawn_appearance | 39317584 / 726 / 0 | 16341024 / 300 / 0 |
| physical_spawn_check | 0 / 484 / 0 | 0 / 200 / 0 |
| physical_spawn_vehicle | 176768 / 83 / 0 | 96224 / 26 / 0 |
| physical_unattributed | 2155616 / 29912 / 0 | 2394000 / 33250 / 0 |
| physical_spawn_randomize | 2161248 / 242 / 0 | 913856 / 100 / 0 |
| physical_spawn_bake | 2829728 / 242 / 0 | 1561072 / 100 / 0 |
| advance_planning | 43076448 / 9019 / 0 | 21750912 / 4236 / 0 |
| advance_events | 39809880 / 18411 / 0 | 11289376 / 19854 / 0 |
| advance_stops | 2153664 / 14956 / 0 | 2394000 / 16625 / 0 |
| advance_other | 0 / 14956 / 0 | 0 / 16625 / 0 |

### Remaining allocation and pool findings

Day advance is 59.3% below 14g and 20.9% below 14j, meeting the below-100 MB day target at 85.040 MB. Both composite GC gates fail on pause p95 and gen0. The mod residual outside physical+advance exceeds either named parent; its source is not instrumented by the allocation sampler. This run does not assign that residual or the pause regression to a production method.

- **gc-day:** largest physical/advance parent `advance` = 85,039,992 bytes (54.37% of physical+advance). Largest advance sub-segment `advance_planning` = 43,076,448 bytes (50.65% of advance). Mod allocation outside physical+advance = 127,275,896 bytes (44.86% of mod). Last logged AppearanceBuilds/Reuses=222/13, pool hit=5.53% (reuse/(build+reuse)); periodic, not exact window deltas.
- **gc-night:** largest physical/advance parent `physical` = 39,723,784 bytes (52.85% of physical+advance). Largest advance sub-segment `advance_planning` = 21,750,912 bytes (61.38% of advance). Mod allocation outside physical+advance = 115,699,936 bytes (60.62% of mod). Last logged AppearanceBuilds/Reuses=101/1, pool hit=0.98% (reuse/(build+reuse)); periodic, not exact window deltas.

| Window | Last logged pedestrianLooks |
| --- | --- |
| gc-day | 222 built/13 reused/3530 deferred/17 idle/65 owned/232 baked/45 bake sizes/1678 KB bake scratch/65 grown/1 trimmed/6 fitted/279 waited/4.9 ms build, ownCars=0, parkedCars=16, instances=26, heldGroups=Personal, retired=34, loadsRetired=29, shareReapplied=2, carAdmission=pc:257:admitted: |
| gc-night | 101 built/1 reused/5242 deferred/16 idle/16 owned/479 baked/43 bake sizes/1618 KB bake scratch/46 grown/31 trimmed/52 fitted/93 waited/6.3 ms build, ownCars=0, parkedCars=18, instances=25, heldGroups=Personal, retired=0, loadsRetired=0, shareReapplied=2, carAdmission=c57:admitted: |
| street | 30 built/0 reused/1166 deferred/16 idle/23 owned/644 baked/36 bake sizes/1289 KB bake scratch/25 grown/3 trimmed/12 fitted/41 waited/11.0 ms build, ownCars=0, parkedCars=18, instances=18, heldGroups=Personal, retired=25, loadsRetired=16, shareReapplied=1, carAdmission=:: |

### UpdateCity, mod-work, whole-frame and every >=30 ms mod-work spike

All windows used the rig union timer with UpdateCity, AdvancePopulation, route preparation, RunPhysical, inspector, body acquire, instantiate and dress components. Component timings are **self time**: children are subtracted. Thus the separately reported UpdateCity p95 is self time, not an inclusive UpdateCity percentile and not directly comparable to 14j’s single-component UpdateCity p95. Inclusive UpdateCity is bounded by the complete mod-work union in each frame, so union p95 <5 ms proves the inclusive <5 ms gate, but its exact percentile is a tooling gap. Observer overhead stays in union. GC windows now also have rig timing patches, unlike 14j; allocation/cadence comparisons are observational on this shared host.

| Window | seconds / frames / fps | UpdateCity self p95 / max ms | mod-work p95 / max / spikes | whole-frame p95 / max ms | MaximumLagMinutes |
| --- | --- | --- | --- | --- | --- |
| gc-day | 600.988 / 15068 / 25.07 | 0.4234 / 63.3563 | 3.8191 / 98.8772 / 16 | 68.1656 / 1466.9142 | 0.7866191864018219 |
| gc-night | 600.673 / 16708 / 27.82 | 0.38 / 118.2388 | 2.6698 / 139.7778 / 2 | 45.2048 / 1158.5734 | 1.0 |
| street | 605.379 / 2583 / 4.27 | 4.9955 / 269.979 | 23.2135 / 387.9724 / 108 | 899.4064 / 6621.1226 | 1.0 |

MaximumLagMinutes is the session maximum and includes untimed speed-20 setup. Final lag/caught-up state is reported separately below.

14j street: UpdateCity 3.7732 ms, union 3.8197 ms, 8.13 fps, three spikes 70.2369/30.9771/33.2070 ms. 14b street: 5.5170/5.5824 ms, 5.61 fps, four spikes.

The nominal waits were 600 s; actual timed spans include command/readback dispatch and are authoritative in the table. Street native clock waiting reached 433.847 s; final lag=0.111 game minutes, caughtUpToTarget=false, refused=false, stoppedAfterAccountingFailure=false.

14k street union vs 14j: +19.3938 ms (+507.7%). Strict union <5 ms not met. Exact inclusive UpdateCity delta unavailable under the component split.

| Window | Unity frame | mod-work ms | dominant self component | whole-frame ms | top self components ms |
| --- | --- | --- | --- | --- | --- |
| gc-day | 5090 | 98.8772 | updatecity | 144.742 | updatecity=63.3563; body_instantiate=16.4223; advance=8.0741; physical=6.462; body_dress=2.3368; body_acquire=1.8998 |
| gc-day | 10095 | 40.4293 | body_instantiate | 166.5232 | body_instantiate=30.578; physical=4.5698; updatecity=2.569; advance=1.8759; prepare=0.2528; NativeCityClock.AccountingPrefix=0.2034 |
| gc-day | 11483 | 39.6167 | body_acquire | 93.3502 | body_acquire=14.2983; prepare=13.5775; body_dress=5.3176; advance=2.8753; physical=2.4443; updatecity=0.968 |
| gc-day | 16269 | 30.6313 | physical | 286.8034 | physical=15.8542; updatecity=7.9927; advance=3.9978; prepare=1.3638; NativeCityClock.AccountingPrefix=1.151; PopulationRuntime.Update=0.0786 |
| gc-day | 16396 | 32.7795 | physical | 202.319 | physical=23.1956; advance=5.416; updatecity=3.4274; prepare=0.288; NativeCityClock.AccountingPrefix=0.2861; PopulationRuntime.Update=0.0576 |
| gc-day | 16526 | 66.3271 | physical | 205.7908 | physical=27.8439; body_acquire=12.3925; updatecity=11.5046; body_dress=10.7138; advance=3.241; prepare=0.2654 |
| gc-day | 17118 | 33.4014 | body_instantiate | 102.6016 | body_instantiate=26.6952; advance=5.0353; physical=0.9077; updatecity=0.5696; body_acquire=0.0632; prepare=0.0429 |
| gc-day | 17301 | 49.2285 | physical | 154.5323 | physical=28.0427; prepare=6.38; body_dress=5.8946; updatecity=3.4652; body_acquire=2.9804; advance=2.0708 |
| gc-day | 17358 | 62.9492 | body_instantiate | 178.318 | body_instantiate=56.1125; physical=2.899; advance=2.6209; updatecity=1.1185; body_acquire=0.0646; prepare=0.0628 |
| gc-day | 17373 | 59.8352 | body_dress | 218.9851 | body_dress=40.5386; body_acquire=15.9226; physical=1.8584; updatecity=1.4126; prepare=0.033; NativeCityClock.AccountingPrefix=0.0198 |
| gc-day | 17391 | 57.7255 | physical | 110.8249 | physical=24.2028; body_acquire=18.3099; body_dress=12.538; updatecity=1.346; advance=1.2423; prepare=0.033 |
| gc-day | 17405 | 65.8161 | body_instantiate | 128.9681 | body_instantiate=49.7429; physical=13.8988; updatecity=1.3245; advance=0.6319; body_acquire=0.0823; prepare=0.0566 |
| gc-day | 17407 | 51.9825 | physical | 218.8943 | physical=26.1501; updatecity=9.7912; body_acquire=8.2832; body_dress=5.5267; prepare=0.8672; advance=0.6093 |
| gc-day | 17491 | 30.3692 | updatecity | 111.0859 | updatecity=28.6766; prepare=1.05; physical=0.5767; advance=0.0209; NativeCityClock.AccountingPrefix=0.0145; inspector=0.0089 |
| gc-day | 17492 | 42.0883 | body_instantiate | 137.6511 | body_instantiate=34.6246; physical=3.1335; body_acquire=2.3849; prepare=1.0637; updatecity=0.7883; NativeCityClock.AccountingPrefix=0.0244 |
| gc-day | 17727 | 48.4438 | body_instantiate | 105.5592 | body_instantiate=44.2604; advance=1.565; physical=1.0938; body_acquire=1.0314; updatecity=0.4059; prepare=0.0321 |
| gc-night | 26129 | 139.7778 | updatecity | 274.3695 | updatecity=118.2388; advance=6.809; physical=5.9871; body_instantiate=4.8419; body_dress=1.8484; body_acquire=1.3078 |
| gc-night | 39311 | 30.896 | updatecity | 68.7367 | updatecity=29.6228; prepare=1.0582; physical=0.1541; advance=0.0139; NativeCityClock.AccountingPrefix=0.0134; inspector=0.0112 |
| street | 50298 | 272.5144 | updatecity | 393.655 | updatecity=269.979; prepare=2.5003; NativeCityClock.AccountingPrefix=0.0145; ResidentInspector.UiRaycastPrefix=0.0064; physical=0.0048; PopulationRuntime.Update=0.0034 |
| street | 50352 | 39.6271 | prepare | 71.7974 | prepare=38.2664; updatecity=1.3163; PopulationRuntime.Update=0.0204; NativeCityClock.AccountingPrefix=0.0116; ResidentInspector.UiRaycastPrefix=0.0053; ResidentInspector.MouseRunPrefix=0.0015 |
| street | 50402 | 71.2955 | updatecity | 104.1166 | updatecity=53.1768; advance=6.8178; body_instantiate=4.275; physical=4.0817; body_dress=1.5605; body_acquire=1.1084 |
| street | 51999 | 74.4791 | physical | 2083.0537 | physical=38.6554; updatecity=27.3742; prepare=2.7778; advance=2.7152; NativeCityClock.AccountingPrefix=2.3225; NativeCityClock.TickPrefix=0.2217 |
| street | 52000 | 57.2924 | physical | 1665.1822 | physical=25.4016; updatecity=15.0222; advance=8.1429; prepare=7.5327; NativeCityClock.AccountingPrefix=0.6629; ResidentInspector.MouseRunPrefix=0.1655 |
| street | 52004 | 42.6555 | physical | 2549.6287 | physical=34.2959; updatecity=6.7726; advance=0.7534; prepare=0.308; NativeCityClock.AccountingPrefix=0.2378; PopulationRuntime.LateUpdate=0.0893 |
| street | 52005 | 45.6479 | physical | 1633.4054 | physical=30.4979; updatecity=10.4702; advance=3.1751; prepare=0.968; NativeCityClock.AccountingPrefix=0.2889; inspector=0.1049 |
| street | 52006 | 34.9193 | physical | 1255.0898 | physical=21.9824; updatecity=8.9995; advance=2.4855; NativeCityClock.AccountingPrefix=0.7507; prepare=0.4031; ResidentInspector.MouseRunPrefix=0.1038 |
| street | 52007 | 34.9595 | physical | 1513.4191 | physical=21.1574; updatecity=9.3278; advance=2.6096; NativeCityClock.AccountingPrefix=1.0298; prepare=0.4378; PopulationRuntime.Update=0.1293 |
| street | 52008 | 77.001 | updatecity | 1414.3722 | updatecity=53.9639; physical=18.6431; advance=2.0384; prepare=1.7572; NativeCityClock.AccountingPrefix=0.3394; NativeFoodOwnership.AfterNativeTick=0.0646 |
| street | 52009 | 36.3526 | physical | 1365.1843 | physical=21.0994; updatecity=12.5195; prepare=1.6793; NativeCityClock.AccountingPrefix=0.7042; advance=0.1067; ResidentInspector.MouseRunPrefix=0.054 |
| street | 52012 | 35.1351 | physical | 1504.0187 | physical=22.9207; updatecity=7.6865; prepare=2.2667; NativeCityClock.AccountingPrefix=1.702; ResidentInspector.MouseRunPrefix=0.19; PopulationRuntime.Update=0.0868 |
| street | 52014 | 37.5512 | physical | 1537.5573 | physical=28.5301; updatecity=5.3109; prepare=2.01; NativeCityClock.AccountingPrefix=1.2496; advance=0.1615; ResidentInspector.MouseRunPrefix=0.0995 |
| street | 52015 | 34.8557 | physical | 1619.5215 | physical=23.1679; updatecity=8.4282; prepare=1.4693; NativeCityClock.AccountingPrefix=0.9094; advance=0.5063; PopulationRuntime.Update=0.1555 |
| street | 52019 | 44.1765 | physical | 1874.4061 | physical=27.2785; updatecity=9.9627; advance=3.8515; prepare=1.5658; NativeCityClock.AccountingPrefix=1.0896; inspector=0.1323 |
| street | 52020 | 30.0645 | physical | 1121.1033 | physical=13.2718; updatecity=8.3038; advance=5.2584; prepare=1.4543; NativeCityClock.AccountingPrefix=0.8821; PopulationRuntime.Update=0.4239 |
| street | 52022 | 30.1564 | physical | 1426.2693 | physical=22.4235; updatecity=4.7463; prepare=1.3333; NativeCityClock.AccountingPrefix=0.8329; ResidentInspector.MouseRunPrefix=0.3916; advance=0.1786 |
| street | 52023 | 59.3145 | updatecity | 1662.2998 | updatecity=35.4283; physical=16.7315; advance=4.8845; prepare=1.4327; NativeCityClock.AccountingPrefix=0.2974; AmbientPedestrians.SetActivePrefix=0.1653 |
| street | 52024 | 43.1328 | physical | 1387.7137 | physical=24.1459; updatecity=11.3417; advance=5.6768; prepare=1.3159; NativeCityClock.AccountingPrefix=0.3235; NativeTravelPace.ObserveTickPostfix=0.1308 |
| street | 52025 | 39.5447 | physical | 1658.8328 | physical=23.9414; updatecity=8.892; advance=4.1344; prepare=1.4034; NativeCityClock.AccountingPrefix=0.8636; ResidentInspector.MouseRunPrefix=0.0809 |
| street | 52026 | 44.967 | physical | 1552.0231 | physical=27.1476; updatecity=8.0358; advance=7.9416; prepare=1.386; NativeCityClock.AccountingPrefix=0.2853; inspector=0.0517 |
| street | 52030 | 31.5307 | physical | 1314.136 | physical=14.7889; updatecity=7.4606; advance=2.8761; NativeCityClock.AccountingPrefix=2.5144; NativeCityClock.TickPrefix=1.393; prepare=1.3078 |
| street | 52032 | 90.7907 | updatecity | 1594.1625 | updatecity=61.3185; physical=22.7539; prepare=2.8371; advance=2.8035; NativeCityClock.AccountingPrefix=0.897; inspector=0.0423 |
| street | 52043 | 32.5013 | physical | 1167.4115 | physical=25.5053; advance=4.3958; updatecity=1.2287; prepare=1.2042; ResidentInspector.UiRaycastPrefix=0.0661; inspector=0.0345 |
| street | 52046 | 40.6142 | physical | 953.2037 | physical=24.5474; updatecity=9.824; prepare=3.0426; advance=2.6256; NativeCityClock.AccountingPrefix=0.3647; NativeFoodOwnership.AfterNativeTick=0.0457 |
| street | 52069 | 62.9893 | physical | 819.2982 | physical=54.2806; updatecity=3.6044; advance=2.3523; prepare=1.4118; NativeCityClock.AccountingPrefix=1.1265; ResidentInspector.MouseRunPrefix=0.0768 |
| street | 52089 | 58.2051 | physical | 537.1635 | physical=42.2739; updatecity=7.4963; advance=5.296; prepare=1.6258; NativeCityClock.AccountingPrefix=1.4625; inspector=0.0235 |
| street | 52119 | 38.0723 | physical | 768.8061 | physical=27.2002; updatecity=4.547; advance=3.0839; NativeCityClock.AccountingPrefix=2.0864; ResidentInspector.MouseRunPrefix=0.6122; prepare=0.3385 |
| street | 52128 | 33.1687 | physical | 486.1929 | physical=13.5707; updatecity=7.687; advance=6.0548; prepare=5.4036; NativeCityClock.AccountingPrefix=0.2266; PopulationRuntime.Update=0.1041 |
| street | 52141 | 30.9297 | advance | 609.1096 | advance=21.8601; physical=8.1489; updatecity=0.7807; prepare=0.0567; NativeCityClock.AccountingPrefix=0.0269; PopulationRuntime.LateUpdate=0.0172 |
| street | 52159 | 48.3188 | physical | 1296.2685 | physical=36.9652; updatecity=7.3693; advance=2.3845; prepare=1.2675; PopulationRuntime.Update=0.119; NativeCityClock.AccountingPrefix=0.0924 |
| street | 52184 | 59.5188 | updatecity | 597.038 | updatecity=36.879; physical=18.0462; advance=3.0889; prepare=1.2908; NativeCityClock.AccountingPrefix=0.1214; PopulationRuntime.Update=0.0304 |
| street | 52203 | 51.2016 | updatecity | 723.8093 | updatecity=22.3202; prepare=16.2354; physical=10.0359; advance=2.1007; PopulationRuntime.Update=0.3822; NativeCityClock.AccountingPrefix=0.073 |
| street | 52205 | 46.6671 | physical | 739.9105 | physical=27.4741; updatecity=15.1779; prepare=1.8493; advance=1.4326; NativeCityClock.AccountingPrefix=0.3892; PopulationRuntime.Update=0.1008 |
| street | 52212 | 37.1554 | physical | 675.3426 | physical=24.3921; updatecity=7.77; advance=3.4847; ResidentInspector.MouseRunPrefix=0.5215; prepare=0.3716; NativeCityClock.AccountingPrefix=0.3067 |
| street | 52358 | 33.9052 | physical | 371.565 | physical=30.1603; advance=1.3587; prepare=1.1049; updatecity=1.0447; NativeCityClock.AccountingPrefix=0.1038; PopulationRuntime.LateUpdate=0.0617 |
| street | 52383 | 36.2511 | updatecity | 386.8748 | updatecity=33.9068; advance=1.6914; physical=0.5603; prepare=0.0344; NativeCityClock.AccountingPrefix=0.0189; ResidentInspector.UiRaycastPrefix=0.0155 |
| street | 52384 | 42.603 | physical | 561.4432 | physical=32.6393; updatecity=4.7186; advance=4.3105; NativeCityClock.AccountingPrefix=0.594; prepare=0.1544; ResidentInspector.UiRaycastPrefix=0.1229 |
| street | 52387 | 53.6516 | physical | 1108.6623 | physical=36.2036; updatecity=12.298; advance=2.4327; prepare=1.0319; NativeCityClock.AccountingPrefix=0.8962; ResidentInspector.UiRaycastPrefix=0.4167 |
| street | 52390 | 69.4839 | physical | 1423.4665 | physical=30.918; updatecity=20.2094; prepare=12.757; advance=5.0538; NativeCityClock.AccountingPrefix=0.2442; inspector=0.1062 |
| street | 52391 | 49.9962 | physical | 1647.0498 | physical=38.361; updatecity=6.7945; prepare=2.9221; advance=1.3441; NativeCityClock.AccountingPrefix=0.2992; PopulationRuntime.LateUpdate=0.0644 |
| street | 52392 | 40.9347 | physical | 815.6728 | physical=31.3877; updatecity=5.8417; advance=2.8996; prepare=0.3588; NativeCityClock.AccountingPrefix=0.2975; inspector=0.0353 |
| street | 52394 | 36.5689 | physical | 1370.0403 | physical=25.4305; updatecity=7.0595; advance=3.1886; prepare=0.3469; NativeCityClock.AccountingPrefix=0.2964; NativeCityClock.TickPrefix=0.1212 |
| street | 52395 | 74.7541 | physical | 1746.6154 | physical=44.5458; updatecity=18.9962; advance=4.5437; prepare=3.6629; NativeCityClock.AccountingPrefix=1.9076; ResidentInspector.MouseRunPrefix=0.3519 |
| street | 52396 | 52.5366 | physical | 1755.2548 | physical=39.4988; updatecity=8.1952; advance=2.4979; prepare=1.4692; NativeCityClock.AccountingPrefix=0.5217; inspector=0.1375 |
| street | 52397 | 66.1695 | physical | 1961.8113 | physical=53.5309; updatecity=7.38; advance=3.8363; prepare=0.4749; NativeCityClock.AccountingPrefix=0.2599; PopulationRuntime.LateUpdate=0.2301 |
| street | 52398 | 36.8622 | physical | 1617.3093 | physical=21.5383; updatecity=11.6424; advance=2.7621; prepare=0.3784; NativeCityClock.AccountingPrefix=0.2909; PopulationRuntime.LateUpdate=0.0661 |
| street | 52399 | 74.9377 | updatecity | 1657.9734 | updatecity=59.421; physical=9.5268; prepare=4.0385; advance=0.9799; NativeCityClock.AccountingPrefix=0.2947; NativeFoodOwnership.AfterNativeTick=0.2441 |
| street | 52400 | 43.8616 | physical | 1386.7293 | physical=22.274; updatecity=10.1679; prepare=6.1033; advance=2.7446; NativeCityClock.AccountingPrefix=1.0959; NativeFoodOwnership.AfterNativeTick=1.009 |
| street | 52401 | 42.3524 | physical | 1520.9839 | physical=27.2973; updatecity=12.4197; prepare=1.4496; advance=0.6048; NativeCityClock.AccountingPrefix=0.3389; PopulationRuntime.Update=0.0468 |
| street | 52402 | 42.6162 | physical | 1966.4081 | physical=28.6034; updatecity=9.639; advance=2.287; prepare=1.5125; NativeCityClock.AccountingPrefix=0.2782; PopulationRuntime.LateUpdate=0.0807 |
| street | 52403 | 55.5318 | physical | 1688.584 | physical=34.1187; updatecity=13.0429; advance=4.641; prepare=1.9403; NativeCityClock.AccountingPrefix=1.4664; PopulationRuntime.Update=0.0972 |
| street | 52404 | 36.9968 | physical | 1405.5906 | physical=19.8434; updatecity=11.1866; advance=2.4698; prepare=2.2058; NativeCityClock.AccountingPrefix=0.7676; ResidentInspector.MouseRunPrefix=0.1939 |
| street | 52405 | 49.3202 | updatecity | 1713.7155 | updatecity=22.8264; physical=20.3533; advance=2.3985; prepare=1.9977; NativeCityClock.AccountingPrefix=1.1198; ResidentInspector.UiRaycastPrefix=0.1855 |
| street | 52406 | 57.6927 | physical | 1506.5843 | physical=40.4515; updatecity=10.493; advance=3.4989; prepare=1.5051; NativeCityClock.AccountingPrefix=1.3088; ResidentInspector.MouseRunPrefix=0.1722 |
| street | 52407 | 40.456 | physical | 1727.6446 | physical=24.4926; updatecity=9.4424; advance=2.7435; prepare=2.3805; NativeCityClock.AccountingPrefix=0.8042; PopulationRuntime.LateUpdate=0.2546 |
| street | 52408 | 32.893 | physical | 1231.2911 | physical=21.5677; updatecity=5.6284; advance=2.9773; prepare=1.8369; NativeCityClock.AccountingPrefix=0.5738; PopulationRuntime.Update=0.1014 |
| street | 52409 | 41.8842 | physical | 1597.4762 | physical=28.6244; updatecity=6.5563; advance=3.9817; prepare=2.1251; NativeCityClock.AccountingPrefix=0.2704; inspector=0.1659 |
| street | 52410 | 57.0009 | physical | 1302.9116 | physical=29.0452; updatecity=14.1055; advance=9.8593; prepare=2.861; NativeCityClock.AccountingPrefix=0.6882; inspector=0.1069 |
| street | 52416 | 43.2323 | physical | 1534.482 | physical=26.745; updatecity=5.0603; prepare=4.7014; advance=3.6525; ResidentInspector.MouseRunPrefix=1.3788; NativeCityClock.AccountingPrefix=1.1763 |
| street | 52417 | 87.3877 | physical | 1491.6774 | physical=51.8977; updatecity=22.9367; advance=6.7794; prepare=4.1818; NativeCityClock.AccountingPrefix=1.2854; ResidentInspector.MouseRunPrefix=0.1431 |
| street | 52418 | 49.492 | physical | 1639.0521 | physical=25.1325; updatecity=17.0389; advance=4.3234; prepare=2.3845; NativeCityClock.AccountingPrefix=0.3438; PopulationRuntime.Update=0.0746 |
| street | 52419 | 34.9736 | physical | 1107.5603 | physical=17.8597; updatecity=9.5331; advance=4.2515; prepare=1.5136; NativeCityClock.AccountingPrefix=1.4661; ResidentInspector.MouseRunPrefix=0.0817 |
| street | 52420 | 45.655 | physical | 555.1228 | physical=24.5574; updatecity=10.7585; advance=7.8599; prepare=1.4602; NativeCityClock.AccountingPrefix=0.6409; ResidentInspector.MouseRunPrefix=0.103 |
| street | 52422 | 75.5374 | physical | 1564.8005 | physical=60.2704; updatecity=8.8369; advance=3.6924; prepare=2.0989; NativeCityClock.AccountingPrefix=0.2509; inspector=0.1151 |
| street | 52423 | 85.3571 | updatecity | 3393.5119 | updatecity=56.4529; physical=22.5448; advance=3.631; prepare=1.8967; NativeCityClock.AccountingPrefix=0.3089; PopulationRuntime.LateUpdate=0.2411 |
| street | 52424 | 51.1842 | physical | 1590.872 | physical=33.5098; advance=5.7665; updatecity=5.4401; prepare=4.7153; NativeCityClock.AccountingPrefix=1.3429; ResidentInspector.MouseRunPrefix=0.1388 |
| street | 52425 | 51.5042 | physical | 1561.1156 | physical=31.4412; updatecity=14.1761; advance=2.9418; prepare=1.6521; NativeCityClock.AccountingPrefix=0.845; inspector=0.1193 |
| street | 52426 | 52.0875 | physical | 1165.4068 | physical=34.7796; updatecity=10.8715; prepare=2.7832; advance=2.284; NativeCityClock.AccountingPrefix=0.933; ResidentInspector.MouseRunPrefix=0.1025 |
| street | 52427 | 65.1053 | physical | 2543.1285 | physical=44.2806; updatecity=12.1298; advance=3.636; prepare=3.3838; NativeCityClock.AccountingPrefix=1.0017; NativeCityClock.TickPrefix=0.2803 |
| street | 52428 | 116.9942 | updatecity | 1867.2504 | updatecity=70.0823; physical=38.4047; advance=3.7542; prepare=2.1184; NativeCityClock.AccountingPrefix=1.8766; NativeFoodOwnership.AfterNativeTick=0.5242 |
| street | 52429 | 121.1725 | physical | 1720.3656 | physical=98.0953; updatecity=13.4878; advance=7.2504; prepare=1.7177; NativeCityClock.AccountingPrefix=0.344; inspector=0.1299 |
| street | 52430 | 51.0121 | physical | 1773.5216 | physical=30.6546; updatecity=13.235; advance=3.6778; prepare=2.9366; NativeCityClock.AccountingPrefix=0.3134; inspector=0.049 |
| street | 52431 | 56.1135 | physical | 2397.1273 | physical=30.3069; prepare=11.1194; updatecity=7.9807; advance=6.0135; NativeCityClock.AccountingPrefix=0.2866; PopulationRuntime.Update=0.1464 |
| street | 52434 | 276.741 | physical | 6621.1226 | physical=259.5299; updatecity=11.3692; advance=2.0356; prepare=1.487; PopulationRuntime.LateUpdate=1.1479; NativeCityClock.AccountingPrefix=0.7157 |
| street | 52435 | 387.9724 | physical | 4650.0313 | physical=230.6636; updatecity=142.6508; prepare=11.9835; advance=2.2011; NativeCityClock.AccountingPrefix=0.2565; inspector=0.0488 |
| street | 52436 | 34.5212 | physical | 1228.8399 | physical=21.6533; advance=6.0212; updatecity=5.0079; prepare=1.4084; NativeCityClock.AccountingPrefix=0.2223; inspector=0.0522 |
| street | 52476 | 50.9713 | updatecity | 762.5587 | updatecity=44.8384; physical=2.51; advance=2.3351; prepare=1.2254; NativeCityClock.AccountingPrefix=0.0166; inspector=0.0145 |
| street | 52483 | 63.4459 | physical | 1268.192 | physical=46.7059; updatecity=8.6435; advance=5.7246; prepare=1.3663; PopulationRuntime.Update=0.5397; NativeCityClock.AccountingPrefix=0.2439 |
| street | 52492 | 43.0119 | physical | 1778.4066 | physical=26.6258; updatecity=9.15; advance=3.6701; prepare=2.6093; PopulationRuntime.Update=0.5413; NativeCityClock.AccountingPrefix=0.2636 |
| street | 52493 | 43.2188 | physical | 1520.4689 | physical=23.0833; updatecity=12.8507; advance=5.0265; prepare=1.6847; NativeCityClock.AccountingPrefix=0.2999; PopulationRuntime.Update=0.1004 |
| street | 52494 | 30.2504 | physical | 1388.585 | physical=21.7624; updatecity=5.6189; NativeCityClock.AccountingPrefix=1.3665; advance=0.6922; prepare=0.351; ResidentInspector.MouseRunPrefix=0.1558 |
| street | 52495 | 34.0295 | physical | 1700.6031 | physical=21.0389; updatecity=8.8824; advance=1.9462; prepare=1.5728; NativeCityClock.AccountingPrefix=0.2988; PopulationRuntime.Update=0.0723 |
| street | 52497 | 55.7549 | physical | 1367.5902 | physical=42.1676; updatecity=8.6284; advance=2.1036; NativeCityClock.AccountingPrefix=1.3654; prepare=0.7908; ResidentInspector.MouseRunPrefix=0.3241 |
| street | 52498 | 40.9364 | physical | 1318.2603 | physical=27.3606; updatecity=8.4882; advance=2.7977; prepare=1.6553; NativeCityClock.AccountingPrefix=0.3942; ResidentInspector.UiRaycastPrefix=0.0674 |
| street | 52499 | 34.656 | physical | 630.2087 | physical=20.695; updatecity=8.391; advance=3.8183; prepare=1.3035; NativeCityClock.AccountingPrefix=0.2655; inspector=0.0418 |
| street | 52515 | 30.1553 | physical | 1047.1921 | physical=14.7797; updatecity=9.0266; prepare=2.627; NativeCityClock.AccountingPrefix=2.0804; advance=1.2193; ResidentInspector.MouseRunPrefix=0.1114 |
| street | 52516 | 38.4923 | physical | 709.1675 | physical=27.0612; advance=5.8913; updatecity=4.4099; prepare=1.0553; NativeCityClock.AccountingPrefix=0.0228; inspector=0.0175 |
| street | 52522 | 44.409 | physical | 531.776 | physical=39.4107; updatecity=2.4504; prepare=1.5015; advance=0.9517; inspector=0.0481; NativeCityClock.AccountingPrefix=0.0166 |
| street | 52663 | 37.4035 | updatecity | 564.903 | updatecity=29.1177; physical=6.8022; prepare=1.103; advance=0.244; NativeCityClock.AccountingPrefix=0.0826; inspector=0.0189 |
| street | 52674 | 41.9129 | physical | 799.7057 | physical=23.4792; advance=8.3144; updatecity=6.0931; prepare=3.557; NativeCityClock.AccountingPrefix=0.266; inspector=0.0843 |
| street | 52680 | 41.641 | physical | 457.5378 | physical=37.3549; updatecity=1.747; advance=1.2225; prepare=1.1254; NativeCityClock.AccountingPrefix=0.1049; inspector=0.0191 |
| street | 52720 | 34.3468 | physical | 571.7182 | physical=24.7333; updatecity=5.0432; advance=2.8018; NativeCityClock.AccountingPrefix=1.0587; prepare=0.3098; ResidentInspector.MouseRunPrefix=0.1134 |
| street | 52723 | 35.2148 | updatecity | 608.9113 | updatecity=31.511; advance=1.942; prepare=1.0661; physical=0.5978; NativeCityClock.AccountingPrefix=0.0638; ResidentInspector.UiRaycastPrefix=0.0102 |
| street | 52726 | 35.8368 | physical | 409.0721 | physical=15.8381; updatecity=12.4446; advance=4.1167; prepare=2.8966; NativeCityClock.AccountingPrefix=0.3668; PopulationRuntime.Update=0.0431 |
| street | 52728 | 46.1448 | physical | 547.9342 | physical=36.1683; updatecity=6.4539; prepare=2.1725; advance=0.8296; NativeCityClock.AccountingPrefix=0.3248; PopulationRuntime.Update=0.0678 |
| street | 52733 | 32.7313 | updatecity | 636.6769 | updatecity=27.4991; advance=2.6117; prepare=1.5069; physical=1.0023; PopulationRuntime.Update=0.0379; NativeCityClock.AccountingPrefix=0.0243 |
| street | 52735 | 82.4642 | physical | 967.7 | physical=66.3581; updatecity=8.3544; advance=3.3129; prepare=2.3887; NativeCityClock.AccountingPrefix=1.4695; ResidentInspector.MouseRunPrefix=0.1689 |
| street | 52741 | 38.6103 | physical | 1838.4415 | physical=24.6646; updatecity=7.2661; advance=2.5373; NativeCityClock.AccountingPrefix=1.8464; prepare=1.3497; ResidentInspector.UiRaycastPrefix=0.6076 |
| street | 52742 | 30.539 | physical | 1005.9267 | physical=17.702; updatecity=5.8487; advance=5.0164; prepare=1.4736; NativeCityClock.AccountingPrefix=0.2868; inspector=0.0515 |
| street | 52753 | 54.1311 | physical | 1488.4953 | physical=28.0818; advance=13.8855; updatecity=10.2398; prepare=1.462; NativeCityClock.AccountingPrefix=0.2571; inspector=0.0783 |
| street | 52766 | 37.4511 | physical | 981.2701 | physical=30.0869; updatecity=3.3287; prepare=2.696; NativeCityClock.AccountingPrefix=0.409; advance=0.4034; inspector=0.3042 |
| street | 52769 | 59.0912 | updatecity | 1073.7471 | updatecity=57.5424; prepare=1.0562; advance=0.2709; physical=0.1737; NativeCityClock.AccountingPrefix=0.0153; ResidentInspector.UiRaycastPrefix=0.0102 |
| street | 52785 | 31.3038 | physical | 1076.2269 | physical=15.8516; updatecity=7.1967; advance=5.4199; NativeCityClock.AccountingPrefix=2.2665; prepare=0.4063; inspector=0.0473 |
| street | 52788 | 32.5683 | physical | 1348.2906 | physical=23.6001; advance=3.592; updatecity=3.4336; inspector=1.1985; prepare=0.3293; NativeCityClock.AccountingPrefix=0.2948 |
| street | 52803 | 30.1877 | physical | 429.8891 | physical=22.4396; updatecity=6.2984; advance=0.5145; prepare=0.3256; NativeCityClock.AccountingPrefix=0.2578; ResidentInspector.MouseRunPrefix=0.1535 |
| street | 52809 | 78.2395 | physical | 1515.0675 | physical=46.7799; updatecity=27.5099; advance=2.3459; prepare=1.1518; NativeCityClock.AccountingPrefix=0.2598; inspector=0.0552 |
| street | 52810 | 88.1387 | physical | 1100.8403 | physical=65.2234; updatecity=13.9137; advance=7.1642; prepare=1.3125; NativeCityClock.AccountingPrefix=0.2608; inspector=0.0918 |

Every exported mod-work spike is listed (126 rows: day 16, night 2, street 108). The compact JSON keeps three largest spike examples per window; complete frame/component lists are in this table and the hash-indexed raw end replies. The dominant self component attributes the measured mod work; it does not establish an allocating method or GC cause. `pc gc stop` exposes aggregate GC pause counts/percentiles but no per-pause Unity frame IDs; those pauses cannot be assigned exact frames from its response. Spikes are reported, not chased. Whole-frame spikes remain separate from mod-work; complete raw whole-frame records are in the stage.

gc-day: coverage.complete=True, validity={"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}; save_activity={"attempts":10081,"allowed":0,"refused":10081,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}; NativeCityClock final={"policy":"minute-epoch-drain-v1","configuredCalendarFactor":0.3333333333333333,"nativeMinute":3960.9490726590157,"logicalMinute":3960.9490726590157,"caughtUpToTarget":true,"lagMinutes":0.0,"maxLagMinutes":0.7866191864018219,"supportedMaxLagMinutes":1.0,"heldOrdinaryTicks":3497,"heldSkipTicks":0,"boundaryClips":180,"withheldGameMinutes":123.5844794174991,"waitingSeconds":0.0,"maxRecoverySeconds":12.1572111,"refused":false,"pacingAffected":true,"stoppedAfterAccountingFailure":false,"historicalClockMisses":0}.
gc-night: coverage.complete=True, validity={"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}; save_activity={"attempts":16722,"allowed":0,"refused":16722,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}; NativeCityClock final={"policy":"minute-epoch-drain-v1","configuredCalendarFactor":0.3333333333333333,"nativeMinute":4562.857531547546,"logicalMinute":4562.857531547546,"caughtUpToTarget":true,"lagMinutes":0.0,"maxLagMinutes":1.0,"supportedMaxLagMinutes":1.0,"heldOrdinaryTicks":8623,"heldSkipTicks":0,"boundaryClips":782,"withheldGameMinutes":1229.0320480729472,"waitingSeconds":0.0,"maxRecoverySeconds":15.5762799,"refused":false,"pacingAffected":true,"stoppedAfterAccountingFailure":false,"historicalClockMisses":0}.
street: coverage.complete=True, validity={"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}; save_activity={"attempts":2591,"allowed":0,"refused":2591,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}; NativeCityClock final={"policy":"minute-epoch-drain-v1","configuredCalendarFactor":0.3333333333333333,"nativeMinute":5280.111106872559,"logicalMinute":5280.0,"caughtUpToTarget":false,"lagMinutes":0.11110687255859375,"maxLagMinutes":1.0,"supportedMaxLagMinutes":1.0,"heldOrdinaryTicks":13861,"heldSkipTicks":0,"boundaryClips":1500,"withheldGameMinutes":2384.727051503306,"waitingSeconds":433.8469514,"maxRecoverySeconds":41.3219146,"refused":false,"pacingAffected":true,"stoppedAfterAccountingFailure":false,"historicalClockMisses":0}.

### Host load and memory pressure at each boundary

| Window | Boundary UTC | uptime load | memory_pressure -Q |
| --- | --- | --- | --- |
| gc-day | 2026-11-14T22:01:18Z | 23:01  up 1 day,  4:39, 1 user, load averages: 13.12 7.96 5.92 | System-wide memory free percentage: 46% |
| gc-day | 2026-11-14T22:11:22Z | 23:11  up 1 day,  4:49, 1 user, load averages: 12.17 14.52 11.64 | System-wide memory free percentage: 78% |
| gc-night | 2026-11-14T22:15:18Z | 23:15  up 1 day,  4:53, 1 user, load averages: 22.75 16.79 13.19 | System-wide memory free percentage: 71% |
| gc-night | 2026-11-14T22:25:21Z | 23:25  up 1 day,  5:03, 1 user, load averages: 3.98 8.02 10.21 | System-wide memory free percentage: 76% |
| street | 2026-11-14T22:29:46Z | 23:29  up 1 day,  5:07, 1 user, load averages: 8.10 6.44 8.79 | System-wide memory free percentage: 77% |
| street | 2026-11-14T22:39:53Z | 23:39  up 1 day,  5:18, 1 user, load averages: 90.45 61.07 35.49 | System-wide memory free percentage: 45% |

Street load rose from 8.10/6.44/8.79 to 90.45/61.07/35.49, and memory free fell 77%→45%. This severe shared-host load qualifies the street regression; it is not isolated production-code attribution. All 108 street spikes were exported: 89 physical, 17 UpdateCity self, 1 advance and 1 prepare dominant.

`vm_stat` snapshots are retained with each boundary. Another harness session may have been building/testing; no other session was inspected, stopped or changed. These loads qualify noise; they do not prove which process caused it.

### Traffic observations and Player.log excerpts

GC sampler was stopped for the street window. Nine look_at pans at x=197/215/233 and z=0/12 completed; player stayed x=215,z=0. Headless rendering cannot show whether traffic/arrivals look identical to 14j. Periodic population logs and route-progress counters are retained as operational evidence, not forecast equality proof.

| Last periodic street snapshot | pedestrians | cars | acquired | released | journeyIndexMismatches | journeyIndexFallback |
| --- | --- | --- | --- | --- | --- | --- |
| 14j | 18 | 0 | 163 | 145 | 0 | False |
| 14k | 7 | 0 | 29 | 22 | 0 | False |

These cumulative physical counts show ongoing acquisition/release, not native shop-arrival or forecast equality. The snapshots occur at different achieved cadences and are not a paired behavioural proof.

```text
[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.
[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 2 15:30.
[Mod:FoodTrucks] Test bridge: run_until day 2 15:30 reached at day 2 15:30; paused, speed 1.
[Mod:FoodTrucks] Test bridge: console 'pc gc start' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (2 lines).
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 01:00.
[Mod:FoodTrucks] Test bridge: run_until day 3 01:00 reached at day 3 01:00; paused, speed 1.
[Mod:FoodTrucks] Test bridge: console 'pc gc start' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (2 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (2 lines).
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 15:30.
[Mod:FoodTrucks] Test bridge: run_until day 3 15:30 reached at day 3 15:30; paused, speed 1.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
```

Explicit speed=1 replies are retained before every window. No RLO toggle occurred. The first gc population read immediately after native load said `Warning: [Persistent City] gc population refused: no active city.` The next read confirmed 3000/1000/2000 after asynchronous activation; no window was opened before confirmation. No ops_load_* failure or runtime stop occurred.

### NOT RUN grouped and tooling gaps

- **Look stability (#266 check 4):** excluded, completed in 14j as far as tooling allows.
- **GR-5 60 fps street/busy-shop arms:** excluded; Mac about 5 fps at this population; PC throwaway time/perf/modwork fix agent-rig #90 not merged for this request. No PC contact.
- **GR-5 save latency:** excluded, owner decision pending; SaveCostDiagnostics off, no save profiling.
- **#262 checks 4–5 and #246 check 4 full proof:** excluded, Rig bot fixture/observation gaps.
- **Everything else:** outside the explicitly requested A checks; no release, Workshop, version/changelog, production, schema or price/household-weight change.

Tooling gaps within performed checks: exact inclusive UpdateCity p95 under nested self-time instrumentation; GC-pause frame IDs absent from the successful `pc gc stop` response; rendered traffic/arrival/forecast equality in the required headless process; no finer advance allocation sub-segments or commit-stamped build log. No op was invoked to bypass these limits. Invoked rig ops had no errors; the startup population warning is quoted above.

### Cleanup and offline validation

Stopped only owned PID 74916 normally. Restored the full PersistentCity folder and exact receipt from the printed backup, stopped and leased; preserved staged candidate in `<stage>/staged-candidate-restored-out`. Restored isolated preferences/config, plist, PlayerSettings and original absent paths. Verified 35 original file hashes, 3 absent paths, exact PersistentCity file set and 53 RLO hashes; mismatches=[]. AgentRigOps unchanged. install.json SHA-256 `dea36ce03586cc53d3b2c7e5cd0fda6d9a54e61c670a21d0676ad18feee54a95`. Original autosave settings restored; process suppression ends at stop. Disposable fixture unchanged; no new save. Final running=False, lease held=False, pending=0; owned PID and caffeinate gone.

Build passed. Compact JSON parses and is under 20 KB; docs-links, markdown-ban and diff checks recorded with this docs-only change. No test sources changed, no focused classes or full/regression suite. Evidence committed and pushed; PR remains unmerged, no reviewers. The isolated worktree is removed after opening the PR.

Refs #268

## Rig results — 14 November 2026 l, Mac (GR-5 GC/street recheck after #270 at da8b495b)

**RESULT: DONE. Counts: 0 passed / 1 partial / 3 failed / 4 NOT RUN groups.** Both GC windows miss #270’s allocation/count/pause targets. Street: FAIL; look stability: partial. In-game proof gates no merge; this evidence PR remains unmerged.

Role `rig_test`, where `mac`, isolated `agentmac`, owner `persistent-city-rig-2026-11-14l-mac`. `<stage>` = `<home>/HART-Builds/persistent-city-rig-2026-11-14l-mac`. [Compact GC/frame evidence](perf/gr5-2026-11-14l-mac.json): 17,571 bytes, SHA-256 `4b6ec80d655663f670f56cb62fbbbffd0d8e6ce38d351e76c7ceb7648e396abc`. Raw replies, full reports, Player logs, scripts, images and pre-stage backups stay there. Report copy: `<home>/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-14l-mac.md`.

Raw evidence index: `<stage>/raw-evidence-manifest.json`, SHA-256 `80174cbec02f22776bdde0d8052602b30eb3f3295575c9a545e00cff91fbf806`; pre-stage manifest and restore verification included.

### Build and rig

| Item | Evidence |
| --- | --- |
| Candidate | `da8b495b6a4b6092a8d18107507044cd2aa1bd94`; initial fetched tip agrees; docs-only #271 merged during the run, final fetched tip `73b6fdbc09878bf7d995d067086fb0fdfa802cc8`. #270 and #268 included. |
| Worktree / branch | New `rig-2026-11-14l-mac-evidence` / `codex/mac-rig-2026-11-14l`; isolated worktree used for build and evidence; original checkout unchanged. |
| Official agent-rig HEAD | `17ff9f86ef07a8c071a90886fa76ed2e98f56c57`; clean before fetch / ff-only pull; already current. No rig edit, commit, build or update. |
| Live AgentRigOps SHA-256 | `d3188b0e837188aec8be85535846953170da99807ad22914ccf4a3c38d77ed3c`; live pre-start hash, loaded health and cleanup agree. |
| Build | `PATH=/opt/homebrew/bin:$PATH`; `DOTNET_ROOT` from Homebrew dotnet; `python3 tools/build.py --game-managed "<home>/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`; exit 0. No RLO or game DLL packaged. |
| PersistentCity DLL SHA-256 | `888f49e4fb0a622896927d7efa85ba6b9eab0b7095370f29d9b467c678d6b239`; package and staged copy agree. |
| Loaded build | Health module ID `e487d483-9a39-4fb1-ba64-bcfca5c8fbdc` occurs once in candidate DLL. Receipt-validated starts accepted staged hashes. No commit-stamped build line exists; activation quoted below. |
| Stage | `python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package`; stopped and leased; 28 scoped receipt entries refreshed. Backup `<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T231443Z`. |
| Starts | Headless rig-test PID 10080: day, night, street in one process. Separate background graphics rig-test PID 26377 for looks. No load failure or forced stop. |
| RLO | Player.log: Real Life Overhaul 1.0.6 initialised. living_population absent/live=true; social_media=1/true; social_media_notices=0/false. No option toggle or RLO file edit. |
| Autosave / save cost | Original native 5 minutes / 3 saves, prevent_autosave=false. #70 process guard suppressed before/after every window and after each load; all measured allowed saves / native saving frames = 0. SaveCostDiagnostics requested=false; native profiler installed=false. No manual save or latency measurement. |
| Lease / resources | Free at inspection; acquired 2026-11-14T23:13:51.461922Z. Initial memory free 81%. Every measurement start load <=8; zero load-wait seconds. No PC contact, takeover, other-session intervention, bot message or macOS setting change. |

Printed rollback verbatim (game stopped, lease held):

```sh
rm -rf "/Users/peterhartwieg/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T231443Z/mod" "/Users/peterhartwieg/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T231443Z/install.json" "/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

Cleanup used the equivalent move-aside/copy-back form, retaining the staged candidate in `<stage>/staged-candidate-restored-out`. The complete original mod folder and exact receipt were restored, then hashes verified. No receipt was hand-edited.

### Per-check results and Expected lines

- **1. Day 15:30 / 600 s: FAIL. Expected (#270 TARGETS):** total mod allocation per 600 s window at least a third below 14j, gen0 about 5 or fewer, pause p95 at or below 14j and under ~36 ms; appearance/body reuse a large share, spawn_appearance + spawn_pedestrian several times smaller. **Observed:** mod 313.467 MB vs 315.571 MB (0.67% lower; one-third threshold 210.381 MB), gen0 8, pause p95 33.488 ms vs 28.581 ms. Allocation, collection count and no-pause-regression clauses not met. Pause under ~36 ms met. Spawn and reuse detail below.
- **2. Night 01:00 / 600 s after speed 20: FAIL. Expected (#270 TARGETS):** total mod allocation per 600 s window at least a third below 14j, gen0 about 5 or fewer, pause p95 at or below 14j and under ~36 ms; appearance/body reuse a large share, spawn_appearance + spawn_pedestrian several times smaller. **Observed:** mod 166.405 MB vs 180.623 MB (7.87% lower; one-third threshold 120.416 MB), gen0 7, pause p95 36.717 ms vs 32.902 ms. Allocation, collection count and no-pause-regression clauses not met. Pause under ~36 ms not met. Spawn and reuse detail below.
- **3. Headless street / 600 s with pans: FAIL. Expected (#270 TARGETS):** UpdateCity / mod-work p95 under 5 ms, no >=30 ms body-work spike. Observed UpdateCity boundary self / union p95 5.4914 / 5.5548 ms; 10 union spikes and 10 PC phase spikes. The p95 clause is not met. Frame 51811 spends 31.038 ms in physical_unattributed; its body-growth source is unresolved, so the no-body-work-spike clause is unverified. Full frame/component rows below. No weaker threshold substituted.
- **4. Look stability: PARTIAL. Expected (#270 TARGETS):** no visual change: dressed bodies, same look per resident across release/reacquire, no invisible or undressed bodies, none of “Appearance meshes unavailable”, “Empty appearance” or “Merged body ...”. Inspected screenshots and exact tooling outcomes below. No durable resident/reacquire op is advertised, so same-resident stability is unverified. Sparse images cannot prove no invisible body or brief flash.

Idle ticks staying 0 B is not separately exercised; the requested windows are moving speed-1 gameplay, and zero-valued segments are reported without treating them as an idle-tick test.

### GC windows and baselines

Fixture `FT GR5 13f Timing v1`, 1,000 residents / 2,000 visitors / 1,683 places, person-target off, player x=215,z=0. Day Tuesday 15:30; night Wednesday 01:00; street Wednesday 15:30. Speed=1 in windows; speed-20 advances outside. `boehm_total` allocation counter; legacy frame allocation uses heap_used. Incremental=true, max_generation=0; pause means cycle-ending frame excess, not all incremental collector work. MB means decimal bytes.

| Window | frames / dropped | gen0/1/2 | pause p50 / p95 / max ms | heap_after_gc_bytes | mono_heap_bytes | mod / physical / advance MB |
| --- | --- | --- | --- | --- | --- | --- |
| gc-day | 16804 / 0 | 8/0/0 | 26.708/33.488/33.488 | 1221783552 | 1457061888 | 313.467 / 113.771 / 101.657 |
| gc-night | 17026 / 0 | 7/0/0 | 31.653/36.717/36.717 | 1337786368 | 1608056832 | 166.405 / 35.991 / 33.572 |

| Metric | 14b day / night | 14j day / night | 14k day / night (load-contaminated) | 14l day / night |
| --- | --- | --- | --- | --- |
| gen0 | 11 / 8 | 7 / 6 | 6 / 7 | 8 / 7 |
| pause p95 ms | 28.191 / 61.316 | 28.581 / 32.902 | 219.8 / 234.5 | 33.488 / 36.717 |
| mod MB | not retained here | 315.571 / 180.623 | 283.687 / 190.858 | 313.467 / 166.405 |
| physical+advance MB | 1021.587 / 433.642 | 200.927 / 68.086 | 156.411 / 75.158 | 215.427 / 69.563 |
| advance MB | 236.448 / 72.746 | 107.466 / 33.489 | 85.0 / 35.4 | 101.657 / 33.572 |

gc-day: vs 14j mod -0.67%, advance -5.41%, pause p95 +4.907 ms, gen0 +1. Vs load-contaminated 14k mod +10.50%, advance +19.54%, pause p95 -186.325 ms, gen0 +2.
gc-night: vs 14j mod -7.87%, advance +0.25%, pause p95 +3.815 ms, gen0 +1. Vs load-contaminated 14k mod -12.81%, advance -5.25%, pause p95 -197.795 ms, gen0 +0.

14k had severe shared-host build/test load, up to load average 90. Its deltas are load-contaminated, not controlled code attribution. 14l measured-window boundaries are below; no build or test ran in this session during timing.

### Every allocation segment and sub-segment

| Segment | Day bytes / samples / skipped | Night bytes / samples / skipped |
| --- | --- | --- |
| mod | 313466760 / 16805 / 0 | 166405264 / 17027 / 0 |
| advance | 101656568 / 16711 / 0 | 33572232 / 16947 / 0 |
| physical | 113770760 / 16806 / 0 | 35990768 / 17028 / 0 |
| physical_ambient | 2140880 / 16711 / 0 | 2172208 / 16947 / 0 |
| physical_vehicles | 1038848 / 16711 / 0 | 385936 / 16947 / 0 |
| physical_bindings | 12464 / 525991 / 0 | 15312 / 274981 / 0 |
| physical_vendor_meals | 0 / 16711 / 0 | 0 / 16947 / 0 |
| physical_fade | 0 / 16711 / 0 | 0 / 16947 / 0 |
| physical_focus | 0 / 16711 / 0 | 0 / 16947 / 0 |
| physical_observe | 261200 / 508967 / 0 | 99952 / 257831 / 0 |
| physical_release | 9968 / 329 / 0 | 2480 / 295 / 0 |
| physical_parked | 1969904 / 586 / 0 | 1723584 / 589 / 0 |
| physical_index | 80 / 2191 / 0 | 80 / 2194 / 0 |
| physical_queues | 8960 / 2191 / 0 | 27184 / 2194 / 0 |
| physical_roster | 285456 / 2191 / 0 | 209184 / 2194 / 0 |
| physical_selection | 1400664 / 2768 / 0 | 654064 / 2413 / 0 |
| physical_spawn_pedestrian | 41773560 / 807 / 0 | 667128 / 281 / 0 |
| physical_spawn_appearance | 50000864 / 936 / 0 | 15208928 / 273 / 0 |
| physical_spawn_check | 0 / 624 / 0 | 0 / 182 / 0 |
| physical_spawn_vehicle | 168352 / 82 / 0 | 90496 / 29 / 0 |
| physical_unattributed | 14699560 / 33517 / 0 | 14734232 / 33975 / 0 |
| physical_spawn_randomize | 2802160 / 312 / 0 | 798624 / 91 / 0 |
| physical_spawn_bake | 2775056 / 312 / 0 | 1691520 / 91 / 0 |
| advance_planning | 45550560 / 9461 / 0 | 20019896 / 3989 / 0 |
| advance_events | 53699624 / 21567 / 0 | 11111936 / 19951 / 0 |
| advance_stops | 2406384 / 16711 / 0 | 2440368 / 16947 / 0 |
| advance_other | 0 / 16711 / 0 | 32 / 16947 / 0 |
| physical_spawn_grow | 38248208 / 132 / 0 | 0 / 0 / 0 |
| mod_prelude | 2584608 / 16805 / 0 | 2642128 / 17027 / 0 |
| mod_rlo | 3541904 / 16805 / 0 | 3530080 / 17027 / 0 |
| mod_food | 1279184 / 16805 / 0 | 1279648 / 17027 / 0 |
| mod_optional_stops | 2498192 / 16805 / 0 | 2455680 / 17027 / 0 |
| mod_routes | 75000152 / 16805 / 0 | 74009272 / 17027 / 0 |
| mod_closures | 10365632 / 16711 / 0 | 10120912 / 16947 / 0 |
| mod_hold_replay | 0 / 16711 / 0 | 0 / 16947 / 0 |
| mod_physical_setup | 0 / 13741 / 0 | 0 / 13475 / 0 |
| mod_inspector | 2406384 / 16711 / 0 | 2440864 / 16947 / 0 |
| mod_summary | 361328 / 16429 / 0 | 360880 / 16216 / 0 |
| mod_other | 272 / 16805 / 0 | 576 / 17027 / 0 |

Advance children partition advance; physical children partition physical. randomize, bake and grow overlap spawn parents and must not be added again. mod_* children partition the outer residual. Samples skipped after collections are explicit.

- gc-day: previously unattributed outer residual now 98,039,432 bytes; mod_* segments account for 98,037,656, leaving 1,776 bytes of boundary/counter difference. `mod_routes`=75,000,152 and `mod_closures`=10,365,632 dominate. These boundary names do not identify an allocating method. Appearance + pedestrian spawn = 91.774 MB versus 86.823 MB in 14j; several-fold reduction not met.
- gc-night: previously unattributed outer residual now 96,842,264 bytes; mod_* segments account for 96,840,040, leaving 2,224 bytes of boundary/counter difference. `mod_routes`=74,009,272 and `mod_closures`=10,120,912 dominate. These boundary names do not identify an allocating method. Appearance + pedestrian spawn = 15.876 MB versus 28.495 MB in 14j; several-fold reduction not met.

### Look counters, pool hit rate and retained bodies

| Window | AppearanceBuilds / AppearanceReuses | reuse / (build+reuse) % | last periodic pedestrianLooks |
| --- | --- | --- | --- |
| gc-day | 280 / 29 | 9.39 | 280 built/29 reused/2718 deferred/32 idle/102 owned/8 adopted/290 baked/45 bake sizes/1678 KB bake scratch/108 grown/15 trimmed/224 fitted/162 waited/2.4 ms build |
| gc-night | 91 / 95 | 51.08 | 91 built/95 reused/1574 deferred/33 idle/33 owned/124 adopted/556 baked/44 bake sizes/1634 KB bake scratch/0 grown/91 trimmed/0 fitted/89 waited/4.3 ms build |
| street | 105 / 34 | 24.46 | 105 built/34 reused/401 deferred/32 idle/53 owned/86 adopted/800 baked/45 bake sizes/1678 KB bake scratch/26 grown/59 trimmed/0 fitted/129 waited/6.9 ms build |

These are last periodic cumulative snapshots, not exact start-to-stop counter deltas. 14j snapshots: day 298/16 (5.10%), night 92/1 (1.08%), street 148/7 (4.52%). Day reuse is 9.39%, still a small share: that target is not met. Night reuse is 51.08%, a majority: that clause is met. No arbitrary numeric threshold replaces the target. Exact sampler randomize/bake/grow sample counts are in the allocation table.

### Timing, top GC frames and every >=30 ms mod-work spike

| Window | seconds / frames / achieved fps | UpdateCity self p95 / max ms | mod-work p95 / max ms / spikes | whole-frame p95 / max ms / spikes | MaximumLagMinutes |
| --- | --- | --- | --- | --- | --- |
| gc-day | 600.603 / 16811 / 27.99 | 0.3146 / 50.3308 | 3.4712 / 61.9973 / 2 | 42.8955 / 172.1595 / 16565 | 0.8821749687199372 |
| gc-night | 600.599 / 17035 / 28.36 | 0.3677 / 66.8887 | 2.6328 / 80.9280 / 1 | 37.8688 / 516.5799 / 16818 | 0.8821749687199372 |
| street | 601.424 / 4573 / 7.60 | 5.4914 / 359.4385 | 5.5548 / 359.4729 / 10 | 221.2817 / 965.5998 / 4541 | 1.0 |

GC windows use nested updatecity/advance/prepare/physical/inspector rig components: UpdateCity is self time, not inclusive p95. Street re-arms with only the UpdateCity component, as in 14j, plus automatic entry-point hooks; its boundary self time excludes any nested automatic hooks. Exact inclusive UpdateCity p95 is not exported. Total per-frame mod-work is the union of all timed scopes, bounds UpdateCity, and is the strict <5 ms gate used here. PC’s phase recorder separately supplies inner attribution; its Update-only values and the all-entry union have different scope. Whole-frame timings are separate.

14j street boundary / union p95 3.7732 / 3.8197 ms, three spikes, 8.13 fps. 14l deltas +1.7182 / +1.7351 ms. 14b 5.5170 / 5.5824 ms, four spikes, 5.61 fps. 14k had 108 street spikes under severe load contamination; the comparison is not isolated evidence of code effects.

**gc-day: every exported union spike** (2 total / 2 listed), matched to PC phase records by Unity frame:

| Frame | union ms | rig dominant / top self components ms | PC mod / phase ms | PC detail ms / gc phase | whole-frame ms |
| --- | --- | --- | --- | --- | --- |
| 3757 | 61.9973 | updatecity; updatecity=50.3308, advance=6.3522, physical=5.037, prepare=0.2374, NativeCityClock.AccountingPrefix=0.0113, PopulationRuntime.Update=0.0075 | 61.947 / physical_unattributed=26.554 | physical_spawn_grow=2.167 / none | 94.0142 |
| 16301 | 33.6589 | physical; physical=33.3021, updatecity=0.2827, prepare=0.0183, advance=0.0137, NativeCityClock.AccountingPrefix=0.011, inspector=0.0094 | 33.614 / physical_spawn_pedestrian=32.474 | physical_spawn_grow=32.462 / physical_spawn_pedestrian | 64.3958 |

**gc-night: every exported union spike** (1 total / 1 listed), matched to PC phase records by Unity frame:

| Frame | union ms | rig dominant / top self components ms | PC mod / phase ms | PC detail ms / gc phase | whole-frame ms |
| --- | --- | --- | --- | --- | --- |
| 26859 | 80.928 | updatecity; updatecity=66.8887, physical=7.25, advance=6.5201, prepare=0.2278, NativeCityClock.AccountingPrefix=0.0115, PopulationRuntime.Update=0.0089 | 80.876 / physical_unattributed=31.984 | physical_spawn_bake=0.512 / none | 128.3859 |

**street: every exported union spike** (10 total / 10 listed), matched to PC phase records by Unity frame:

| Frame | union ms | rig dominant / top self components ms | PC mod / phase ms | PC detail ms / gc phase | whole-frame ms |
| --- | --- | --- | --- | --- | --- |
| 51716 | 359.4729 | updatecity; updatecity=359.4385, NativeCityClock.AccountingPrefix=0.0141, ResidentInspector.UiRaycastPrefix=0.0073, PopulationRuntime.Update=0.0069, NativeTravelPace.ObserveTickPostfix=0.0015, ResidentInspector.MouseRunPrefix=0.0014 | 359.442 / mod_routes=359.288 | none=0 / mod_routes | 409.9833 |
| 51811 | 72.8058 | updatecity; updatecity=72.7426, PopulationRuntime.LateUpdate=0.0329, NativeCityClock.AccountingPrefix=0.0114, PopulationRuntime.Update=0.0074, ResidentInspector.UiRaycastPrefix=0.0058, ResidentInspector.MouseRunPrefix=0.0013 | 72.747 / physical_unattributed=31.038 | physical_spawn_bake=0.914 / none | 95.122 |
| 54291 | 35.8596 | updatecity; updatecity=35.7844, PopulationRuntime.Update=0.0242, PopulationRuntime.LateUpdate=0.0227, NativeCityClock.AccountingPrefix=0.0139, ResidentInspector.UiRaycastPrefix=0.0083, ResidentInspector.MouseRunPrefix=0.0015 | 35.805 / mod_summary=30.356 | none=0 / none | 342.5705 |
| 54636 | 31.6301 | updatecity; updatecity=31.3583, NativeCityClock.AccountingPrefix=0.137, PopulationRuntime.LateUpdate=0.0447, ResidentInspector.MouseRunPrefix=0.026, PopulationRuntime.Update=0.0218, ResidentInspector.UiRaycastPrefix=0.0183 | 31.375 / physical_parked=12.936 | none=0 / none | 462.7409 |
| 55224 | 35.8843 | updatecity; updatecity=35.621, NativeCityClock.AccountingPrefix=0.1496, PopulationRuntime.Update=0.056, PopulationRuntime.LateUpdate=0.0332, ResidentInspector.UiRaycastPrefix=0.008, NativeTravelPace.ObserveTickPostfix=0.008 | 35.667 / physical_roster=9.572 | none=0 / none | 364.8978 |
| 55240 | 31.9082 | updatecity; updatecity=31.8433, PopulationRuntime.LateUpdate=0.0229, PopulationRuntime.Update=0.0144, NativeCityClock.AccountingPrefix=0.0135, ResidentInspector.UiRaycastPrefix=0.007, ResidentInspector.MouseRunPrefix=0.0019 | 31.848 / mod_summary=22.616 | none=0 / none | 228.7934 |
| 55616 | 41.757 | updatecity; updatecity=41.1434, NativeCityClock.AccountingPrefix=0.4363, PopulationRuntime.Update=0.1018, ResidentInspector.MouseRunPrefix=0.0321, PopulationRuntime.LateUpdate=0.0268, ResidentInspector.UiRaycastPrefix=0.0082 | 41.202 / mod_optional_stops=10.234 | none=0 / none | 452.6298 |
| 55618 | 41.5307 | updatecity; updatecity=40.1885, PopulationRuntime.Update=0.4938, NativeCityClock.AccountingPrefix=0.4629, PopulationRuntime.LateUpdate=0.2306, ResidentInspector.UiRaycastPrefix=0.0677, ResidentInspector.MouseRunPrefix=0.0545 | 40.622 / physical_parked=11.948 | none=0 / none | 418.2179 |
| 55620 | 32.9644 | updatecity; updatecity=32.3469, NativeCityClock.AccountingPrefix=0.2878, PopulationRuntime.Update=0.2178, PopulationRuntime.LateUpdate=0.0318, ResidentInspector.MouseRunPrefix=0.0295, ResidentInspector.UiRaycastPrefix=0.0253 | 32.48 / physical_parked=6.898 | none=0 / none | 334.1804 |
| 56085 | 42.5574 | updatecity; updatecity=42.1942, PopulationRuntime.Update=0.2618, NativeCityClock.AccountingPrefix=0.0598, PopulationRuntime.LateUpdate=0.0276, ResidentInspector.UiRaycastPrefix=0.0065, NativeTravelPace.ObserveTickPostfix=0.003 | 42.255 / physical_index=21.736 | none=0 / none | 432.386 |

**gc-day: full 16-row GC top-frame report**, highest PC Update work first:

| Frame | mod ms | phase / ms | detail / ms | gc phase / collections | alloc phase / bytes | total alloc bytes |
| --- | --- | --- | --- | --- | --- | --- |
| 3757 | 61.947 | physical_unattributed / 26.554 | physical_spawn_grow / 2.167 | none / 0 | physical_unattributed / 12279624 | 25317760 |
| 16301 | 33.614 | physical_spawn_pedestrian / 32.474 | physical_spawn_grow / 32.462 | physical_spawn_pedestrian / 1 | physical_spawn_pedestrian / 289680 | 292704 |
| 3760 | 9.896 | physical_vehicles / 4.051 | none / 0 | none / 0 | advance_planning / 764352 | 841600 |
| 7419 | 7.56 | physical_selection / 4.124 | none / 0 | none / 0 | physical_selection / 157760 | 168912 |
| 4549 | 7.29 | physical_spawn_pedestrian / 6.833 | physical_spawn_grow / 6.824 | none / 0 | physical_spawn_pedestrian / 289680 | 292128 |
| 16493 | 7.004 | advance_planning / 3.086 | physical_spawn_bake / 0.57 | none / 0 | physical_spawn_appearance / 152048 | 179312 |
| 14653 | 6.686 | advance_planning / 2.39 | physical_spawn_bake / 0.5 | none / 0 | physical_spawn_appearance / 146080 | 180320 |
| 18786 | 6.65 | advance_planning / 2.084 | physical_spawn_bake / 0.49 | none / 0 | physical_spawn_appearance / 176880 | 206064 |
| 12030 | 6.637 | physical_vehicles / 3.627 | none / 0 | none / 0 | advance_planning / 2656 | 5088 |
| 9842 | 6.594 | advance_planning / 3.02 | physical_spawn_bake / 0.742 | none / 0 | physical_spawn_appearance / 136224 | 147952 |
| 16269 | 6.58 | physical_spawn_appearance / 4.416 | physical_spawn_bake / 0.499 | none / 0 | physical_spawn_appearance / 136960 | 161712 |
| 15367 | 6.51 | advance_planning / 3.291 | physical_spawn_bake / 0.505 | none / 0 | physical_spawn_appearance / 150720 | 167552 |
| 6260 | 6.485 | advance_planning / 2.571 | physical_spawn_bake / 0.548 | none / 0 | physical_spawn_appearance / 224240 | 318912 |
| 18664 | 6.339 | advance_planning / 1.753 | physical_spawn_bake / 0.552 | none / 0 | physical_spawn_appearance / 151328 | 165392 |
| 17220 | 6.33 | advance_planning / 1.73 | physical_spawn_bake / 0.532 | none / 0 | physical_spawn_appearance / 137696 | 166736 |
| 13948 | 6.327 | advance_planning / 3.266 | physical_spawn_bake / 0.718 | none / 0 | physical_spawn_appearance / 134480 | 148528 |

**gc-night: full 16-row GC top-frame report**, highest PC Update work first:

| Frame | mod ms | phase / ms | detail / ms | gc phase / collections | alloc phase / bytes | total alloc bytes |
| --- | --- | --- | --- | --- | --- | --- |
| 26859 | 80.876 | physical_unattributed / 31.984 | physical_spawn_bake / 0.512 | none / 0 | physical_unattributed / 12276280 | 25575168 |
| 26954 | 15.098 | physical_spawn_pedestrian / 10.973 | none / 0 | none / 0 | physical_selection / 1856 | 5568 |
| 27320 | 14.335 | advance_events / 12.129 | none / 0 | none / 0 | advance_events / 218208 | 228368 |
| 27516 | 12.609 | physical_release / 10.336 | none / 0 | none / 0 | advance_planning / 7392 | 11440 |
| 27273 | 11.852 | physical_spawn_pedestrian / 5.354 | none / 0 | none / 0 | advance_events / 4352 | 11072 |
| 27470 | 11.31 | physical_spawn_appearance / 6.494 | physical_spawn_bake / 1.05 | none / 0 | physical_spawn_appearance / 165296 | 176096 |
| 26938 | 10.893 | physical_spawn_pedestrian / 6.502 | none / 0 | none / 0 | advance_events / 8992 | 15024 |
| 27015 | 10.625 | physical_spawn_pedestrian / 7.488 | none / 0 | none / 0 | advance_planning / 13792 | 21104 |
| 27023 | 10.463 | physical_spawn_pedestrian / 6.468 | none / 0 | none / 0 | physical_selection / 14064 | 38400 |
| 38964 | 10.341 | advance_planning / 10.029 | none / 0 | none / 0 | advance_planning / 6736 | 10224 |
| 26946 | 9.048 | physical_spawn_pedestrian / 5.08 | none / 0 | none / 0 | advance_planning / 2928 | 8496 |
| 39574 | 8.58 | advance_planning / 8.038 | none / 0 | none / 0 | advance_planning / 16240 | 21440 |
| 27825 | 7.712 | advance_planning / 2.512 | physical_spawn_bake / 0.534 | none / 0 | physical_spawn_appearance / 198128 | 203888 |
| 28321 | 7.61 | physical_spawn_appearance / 3.264 | physical_spawn_bake / 0.542 | none / 0 | physical_spawn_appearance / 160768 | 217968 |
| 28603 | 7.531 | advance_planning / 2.616 | physical_spawn_bake / 0.49 | none / 0 | physical_spawn_appearance / 149248 | 152352 |
| 27497 | 7.514 | physical_spawn_appearance / 3.306 | physical_spawn_bake / 0.658 | none / 0 | physical_spawn_appearance / 223600 | 234352 |

PC phase recorder: day/night/street spikes and counts retained without chasing them. A collection ending inside a phase is correlation, not proof that the phase caused the collection. GC pause rows still have no per-pause Unity frame IDs; top mod-work frames and their `gc_phase` are exported.

gc-day: coverage.complete=True, validity=`{"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}`; save_activity=`{"attempts":10816,"allowed":0,"refused":10816,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}`; clock final lag=0.022049903869628906, caughtUpToTarget=False, refused=False, stoppedAfterAccountingFailure=False, historicalClockMisses=0.
gc-night: coverage.complete=True, validity=`{"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}`; save_activity=`{"attempts":17046,"allowed":0,"refused":17046,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}`; clock final lag=0.0, caughtUpToTarget=True, refused=False, stoppedAfterAccountingFailure=False, historicalClockMisses=0.
street: coverage.complete=True, validity=`{"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}`; save_activity=`{"attempts":4582,"allowed":0,"refused":4582,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}`; clock final lag=0.09999465942382812, caughtUpToTarget=False, refused=False, stoppedAfterAccountingFailure=False, historicalClockMisses=0.

### Host load and memory at every window boundary

| Window | Boundary UTC | uptime | memory_pressure -Q |
| --- | --- | --- | --- |
| gc-day start | 2026-11-14T23:16:32Z | 0:16  up 1 day,  5:54, 1 user, load averages: 5.32 5.82 9.68 | System-wide memory free percentage: 75% |
| gc-day end | 2026-11-14T23:26:33Z | 0:26  up 1 day,  6:04, 1 user, load averages: 2.90 3.04 6.07 | System-wide memory free percentage: 75% |
| gc-night start | 2026-11-14T23:30:18Z | 0:30  up 1 day,  6:08, 1 user, load averages: 3.50 3.22 5.44 | System-wide memory free percentage: 66% |
| gc-night end | 2026-11-14T23:40:20Z | 0:40  up 1 day,  6:18, 1 user, load averages: 4.35 4.43 5.03 | System-wide memory free percentage: 58% |
| street start | 2026-11-14T23:45:06Z | 0:45  up 1 day,  6:23, 1 user, load averages: 4.53 4.73 5.02 | System-wide memory free percentage: 71% |
| street end | 2026-11-14T23:55:08Z | 0:55  up 1 day,  6:33, 1 user, load averages: 6.35 7.93 6.75 | System-wide memory free percentage: 70% |

At each start, the single foreground load loop inspected the 1-minute average; all were <=8 on the first read, so no 60 s sleeps were required (wait=0 s each). Raw boundary snapshots also retain vm_stat. Other sessions were not inspected or changed.

### Visual check and exact failures

Separate cold background graphics rig-test start, same fixture Tuesday 15:30, player x=215,z=0. Nine 1600×900 PNGs inspected: first close view includes a dressed walker in a red jacket and dark trousers; its return contains dressed walkers but the target is distant. The second close/return view is occluded by a building. Five pan stills contain sparse dressed walkers or empty/occluded sidewalk. No undressed body is seen in the inspected images; the images do not prove no invisible body or brief appearance flash, or unchanged visuals versus a synchronized baseline.

Two native walker handles were selected. After an 8 s camera-away interval, the first handle (`walker-a003dd287c154421abf30362d533be80`) still answered screen_pos and camera-follow; its return capture occurred after 11.914 s. The second (`walker-469c462227ce4d1aa31d9cfaac62f003`) returned `ops_view_stale_target` for both screen_pos get and camera follow; return after 11.871 s. A native body handle has no persistent resident ID and no release/reacquire event proof. Even the surviving handle cannot establish that the same resident was assigned before and after, or that release/reacquire happened. Same-resident look stability remains unverified; this is not an asserted appearance-change failure. The pan sequence used 24 camera operations over 121.914 s. All requested appearance-error strings have zero matches in both Player logs.

| Player.log string | Headless A matches | Background B matches |
| --- | --- | --- |
| Appearance meshes unavailable | 0 | 0 |
| Empty appearance | 0 | 0 |
| Merged body | 0 | 0 |
| Merged body already has blendshapes | 0 | 0 |
| Merged body vertex layout differs | 0 | 0 |

- `python3 scripts/mac-agent call teleport {"x": 215, "z": 0}` → `Missing "who": "player" or a food vehicle's id.`.
- `python3 scripts/mac-agent op screen_pos {"action": "get", "target": {"kind": "walker", "id": "walker-469c462227ce4d1aa31d9cfaac62f003"}}` → `{ "error": "ops_view_stale_target", "reply": { "ok": false, "id": "9832f911693340308264b5eac50a8340", "session": "5cecf2c6bbea47769b9ce83575df334e", "error": "ops_view_stale_target" } }`.
- `python3 scripts/mac-agent op camera {"action": "follow", "target": {"kind": "walker", "id": "walker-469c462227ce4d1aa31d9cfaac62f003"}, "offset": {"x": 0, "y": 4, "z": -6}, "seconds": 10}` → `{ "error": "ops_view_stale_target", "reply": { "ok": false, "id": "1f22729883314256aca2a7a02c524bef", "session": "5cecf2c6bbea47769b9ce83575df334e", "error": "ops_view_stale_target" } }`.

The second walker handle went stale; the first remained readable. The initial teleport omitted who=player and was refused before mutation; the corrected call succeeded. The first gc population read after native load returned `Warning: [Persistent City] gc population refused: no active city.`; asynchronous activation completed before the next read confirmed reached=3000. No window opened before confirmation. No ops_load_* failure, staging refusal or runtime stop occurred.

### Player.log excerpts: activation, speed changes and toggles

```text
[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 2 15:30.
[Mod:FoodTrucks] Test bridge: run_until day 2 15:30 reached at day 2 15:30; paused, speed 1.
[Mod:FoodTrucks] Test bridge: console 'pc gc start' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (3 lines).
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 01:00.
[Mod:FoodTrucks] Test bridge: run_until day 3 01:00 reached at day 3 01:00; paused, speed 1.
[Mod:FoodTrucks] Test bridge: console 'pc gc start' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (3 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (3 lines).
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 15:30.
[Mod:FoodTrucks] Test bridge: run_until day 3 15:30 reached at day 3 15:30; paused, speed 1.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc frames start' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc frames stop' (2 lines).
```

Explicit speed=1 replies and autosave readbacks are retained before each window. No RLO toggle occurred, so there is no toggle excerpt. SaveCostDiagnostics stayed off.

### NOT RUN grouped

- **GR-5 60 fps arms:** excluded; Mac historically about 5 fps at 1,000 residents in graphics mode; PC throwaway time/perf/modwork fix agent-rig #90 not merged for this request. Headless achieved fps does not verify a graphics arm. No Windows PC contact.
- **GR-5 save latency:** excluded, owner decision pending; no save profiling or save-latency measurement.
- **#262 checks 4–5 and #246 check 4 full proof:** excluded, Rig bot fixture/observation gaps.
- **Everything else:** outside the requested GR-5 GC/street/looks checks, including a standalone idle-tick allocation check. No release, Workshop, version/changelog, production-code, schema or food-weight change.

Tooling gaps within performed checks: durable resident identity/reacquisition; sparse visual observation cannot prove no invisible/briefly undressed body; exact inclusive UpdateCity p95 under automatic nested hooks; per-GC-pause frame IDs; no commit-stamped build log. Exact invoked errors are above, with no invented failure for an unadvertised op.

### Cleanup and offline validation

Stopped only owned players normally. Restored full original ModsLocal/PersistentCity, exact install.json, isolated preferences/config/plist/PlayerSettings and original absent paths. Verified 35 file hashes, 3 absent paths, PersistentCity file set and 53 RLO hashes; mismatches=`[]`. AgentRigOps unchanged. install.json SHA-256 `dea36ce03586cc53d3b2c7e5cd0fda6d9a54e61c670a21d0676ad18feee54a95`. Original autosave settings restored; process guard ends at stop. Disposable fixture saves unchanged; no new save. Final running=false, lease held=false, pending=0; both owned player PIDs and their caffeinate processes gone.

Candidate build passed. Compact JSON parses and is under 20 KB; docs-links, markdown-ban and diff checks recorded in the PR. No test source changed, no full/regression suite run. One build/evidence worktree removed after commit, push and PR creation.

Refs #270
