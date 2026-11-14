# Rig checklist

In-game checks waiting for a batched rig run. A rig run never gates a merge. Each slice lists its checks in its PR description. After merge the coordinator copies them here, and removes a check once a rig run passes it at a build that still contains the change. This list authorizes no run.

Every check runs in a named disposable FT city in the isolated rig profile, never in Peter's ordinary save. Autosave is off before every measured window, including after a reload, and is restored at cleanup.

Detailed specs: [F8 §7](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/plans/f8-capability-boundaries.md#7-in-game-checks-for-the-batched-rig), [F9 §7](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/plans/f9-city-acceptance.md#7-in-game-checks-for-the-next-batched-rig), [E6 §7](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/plans/e6-carts-food-trucks.md#7-in-game-checks-for-the-batched-rig), [F4 plan](plans/f4-perf-gate.md), [11j report](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/checks/rig-2026-11-11j-mac-results.md), [12b report](checks/rig-2026-11-12b-mac-results.md). Checks written before 10 November are in the [docs history tag](https://github.com/PeterHartwieg/big-ambitions-persistent-city/tree/docs-history-2026-11-10/docs/checks); many test save migration, which no longer exists.

## Latest evidence

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
