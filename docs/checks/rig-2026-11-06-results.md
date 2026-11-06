# Batched rig results — 6 November 2026

First batched in-game rig session since the 30 October performance review, against the development head of `codex/persistent-city`. It covers [the combined checklist](rig-2026-11-06-checklist.md) and every slice's "In-game checks" section under `docs/checks/`. Under merge rule v2 these results gate nothing; each real failure becomes a fix-slice issue.

**Summary.** The smoke test passed on a fresh head city: PC starts, runs two game days, survives an explicit save plus fresh-process and in-session reloads, and stays at writer 25 with no refusal. Of 310 checks, **6 passed** (5 of them partially), **1 failed** and **303 were not run**, each with the capability it needs. On mod-work timing, PC advancement stayed well under 5 ms on average, but one **135 ms** spike at the evening rush became [#128](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/128). The full combined-union p95 was not measured.

## Build and rig

| Item | Value |
|---|---|
| Head | `e9995e5` (City audit F6a #104) |
| Build | `python tools/build.py --game-managed "<Steam>\Big Ambitions_Data\Managed"` |
| PersistentCity.dll SHA-256 | `678cdb58f58c3c3b79f57d4954b0612a7318a685d36c959d7d742ef22173e80d` |
| Timing probe (rig-only, never packaged) | `tools/e5b-native-rig-proof`, E5bNativeRigProof.dll `635078df6cb946699f3f4c1572d030e5bac8e72dd1204eee64f80dd38b7bbaca` (deterministic rebuild of the 3 Nov artifact against this head) |
| Rig | `big-ambitions-agent-rig` `70021ed`, installed overlay `reviewed-live4-58dc76e`, runtime 7, background `-rig-test` mode |
| Install path | Per-flow RIG-MODSET: 31-DLL hashed set, stopped-player transaction, original set and receipt restored after every run; `run_flow.py` restores `run.json`, `PlayerSettings.json` and the isolated HKCU key |
| PC config | Rig's pinned `persistent-city.json` (5 keys); every other setting at its compiled default, including `GameClockFactor` 1/3 |
| Autosave | Rig-test native guard blocks recovery/midnight/unknown saves; health reported `rig_test.autosave_disabled: true` in every run. The isolated HKCU key has no native autosave preference (game default), so there was nothing to change or restore |
| Dotnet contamination | `build/rig-1106/dotnet-activity.tsv` samples every 15 s; a sample counts as contaminated when a `dotnet test`/`run`/`build`, testhost or MSBuild process from another worktree was active |

## Runs

| Run | Fixture | Pace | Result |
|---|---|---|---|
| `rig1106-smoke-01` | 3 Nov `FT rig checkpoint add8db43…` (schema 25, written by `70165cc`) | speed 1; PC stopped, so native clock factor 1 | INFRA exit 3 — see Smoke A |
| `rig1106-producer-01` | native `new_game` → `FT E5b Rig1106 Head e9995e5 Ready` | — | PASS |
| `rig1106-smoke-b-01` | fresh head city, day 1 08:02 → day 3 10:00 | speed 1, PC clock 1/3, player BelowNormal (rig default) | INFRA exit 3: two days, census and explicit save passed; my flow then ran the probe before the in-session `load` finished (`ops_city_required`) |
| `rig1106-smoke-c-01` | smoke B's saved checkpoint `FT rig checkpoint 4bf3ce86…` | speed 1, player High | Fresh-process load, in-session save + reload and readbacks passed; then FAIL at my flow's `walk exit` (player already outside) |
| `rig1106-shop-01..03` | smoke C's reload checkpoint | — | FAIL: native rental refused (`ops_shop_native_rental_refused`), also at the building's entrance |
| `rig1106-probe08-01` | 3 Nov `FT rig checkpoint 08ef900e…` (rented shop) | — | PASS as a probe check: PC refuses this save (as in Smoke A); probe `arm` binds with complete timing coverage |
| `rig1106-perf-01` | smoke C's reload checkpoint | windows speed 1 (PC 1/3); transitions native speed 6; player High | PASS |

Evidence lives in ignored `build/e5b/review-r3/rig_test/<run>/` (reports, step requests/responses, Player.log, modset/lifecycle receipts). Raw saves are not committed.

## Smoke

### Smoke A: 3 Nov fixture (pre-F1 writer)

PC **refused** the 3 Nov schema-25 checkpoints (`add8db43…`, and in `probe08` also `08ef900e…`) at activation:

```
[Mod:PersistentCity] Persistent City stopped: … This stopped save stays refused; native time continues. Accounting input epoch was lost. …
```

Probe `runtime` showed `failed: true`, `saved_schema: 25` and `own_namespace_present: true`. The stop comes from `AccountingClock.Compatible` or the drained-epoch check at activation (`Runtime/PersistentCityMod.cs:624-630`).

These saves were written by `70165cc`, before F1, and F1 documents refusing an out-of-epoch save instead of repairing it. So this is the designed refusal, not a fix slice. It does mean **every 3 Nov E5b fixture is unusable for PC checks on this head.**

The run then ended INFRA when an 18.3 s whole-frame stall (CityPerformance `max_ms=18279`) timed out a bridge `state` poll. PC was already stopped by then, so the stall is not PC work.

### Smoke B/C: fresh city at head, passed

**Fixture.** `produce_fixture.py` built a fresh FT city at head in about 3 minutes: native `new_game`, green initialization, and a saved Ready checkpoint `FT E5b Rig1106 Head e9995e5 Ready` (`42bdcd1c…`), schema observed 25. **The rig can build and age a fresh FT city at head.**

**Start.** `Persistent population active: 957 residents, 1058 places`. Probe `runtime` showed `failed: false`, `simulation_ready: true` and `saved_schema: 25`. RLO was Ready with 400 identities, and the physical population was active.

**Two game days at speed 1** (day 1 08:02 → day 3 10:00): no PC stop, refusal or exception. Logical lag stayed at 0.0–0.3 game minutes.

**Save and reload.** After the explicit save `FT rig checkpoint 4bf3ce86…` (1,500,335 bytes), I did two reloads:
- a **fresh-process load** of that save (smoke C);
- an **in-session save + reload** (`16c3e006…`).

After both, PC was active with 957 residents and 1,058 places, schema 25 and no refusal. The census was identical across the two reloads: hash `e97e457a`, 957 residents, 331 households and 2,871 persisted persons.

**Census before the save vs after reload.** The PC counts are identical. The load-time native catalogue gained two workplaces (places 1,058 → 1,060, workplaces 383 → 385), probably businesses the rival mods opened during the two days. PC's own saved state still reports 1,058 places.

**Log scan** (`log_check` plus grep over the fresh-city Player.logs): no PC exception and no PC stop. The F7b warning `Persistent City detached from the current save; later native saves of this city keep a refusal marker.` appears on **every** ordinary unload (return to menu, quit). This is Rule 9 (`Runtime/SaveEpochRuntime.cs:84`) and reloading works, but the wording alarms on a normal exit. Noted, not filed.

**Awaiting a route.** The count went 1 → 34 at peaks and never dropped to 0 after the first morning; it ranged 10–34 across three game days. Core expires route waits (`route-wait-expired`, `Core/CitySimulator.cs:615`), so this is not proven stuck. Listing which residents they are needs a resident-phase readback that the rig does not have (inspector UI only).

**Bridge check.** The coordinator briefly recycled `AgentRig/requests` and `responses` (~13:39 UTC). Afterwards, an HTTP `state` and a rig-ops `weather read` both round-tripped. No step was lost or retried.

## Performance

Timing had to tolerate a busy PC: parallel harness builds and tests ran through almost the whole session (96% of 15 s samples during smoke B). Two changes made that workable.

**1. Priority.** The rig launches its player at **BelowNormal**, the same class as the capped test suites. From 13:58 UTC (smoke C, shop, probe08 and perf runs), this session's rig player ran at **High**. `build/rig-1106/load-priority.tsv` logs each change and the total CPU every ~5 s. Nothing outside this session's own process was touched, and the priority ends with the process.

**2. Counters that exist in this build.** The full mod-work union probe could not arm without a player business. So mod work comes from PC's own `Advancement this observation` counters: frames, total and maximum single advance, roughly every 1,770 frames.
- These cover **advancement calls only**, not route preparation, physical callbacks, OnGUI or food suppression.
- Whole-frame timing comes separately from CityPerformance's PERF lines.
- Segments are split at the bridge's own clock-command lines (`build/rig-1106/analyze_log.py`).

| Run / segment | Player priority | Frames | PC advance mean/frame | Max single advance | Observations ≥30 ms | Whole-frame p95 (≤, worst block) | Whole-frame max |
|---|---|---:|---:|---:|---:|---:|---:|
| smoke B, 2 days at speed 1 | BelowNormal (contaminated) | 273,661 | 0.372 ms | 1,917.2 ms | 18 / 168 | ≤250 ms | 25,111 ms |
| perf lunch 11:30–14:30, speed 1 | High | 17,407 | 0.120 ms | 12.2 ms | 0 / 10 | ≤53 ms | 216 ms |
| perf evening 17:30–20:30, speed 1 | High | 17,753 | 0.439 ms | **134.96 ms** | **1 / 10** | ≤41 ms | 826 ms |
| perf morning 07:00–09:30, speed 1 | High | 14,235 | 0.130 ms | 4.6 ms | 0 / 8 | ≤40 ms | 539 ms |
| perf speed-6 transitions | High | 12,363 | 0.29–0.51 ms | 5.1 ms | 0 / 7 | ≤56 ms | 600 ms |

### Verdict

**Mod-work p95 < 5 ms: passed for advancement calls in two of three windows, not established for evening.**
- Morning: proven, because every advancement call stayed under 5 ms.
- Lunch: follows from Markov's bound. A mean of 0.12 ms means at most 2.4% of frames reached 5 ms.
- Evening: not established by the bound, which allows up to 8.8% (mean 0.44 ms).
- The combined union p95 was **not measured**.

**Spikes ≥30 ms: one genuine spike.** **134.96 ms** at the evening rush: day 3 ~19:19, 1,788 events in the observation and 56 in one frame. It happened at High priority with total CPU at 28–50%. Filed as [#128](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/128). Smoke B's 18 spikes cluster at the same commute and meal peaks, but CPU starvation at BelowNormal inflated them, so they count as contaminated evidence only.

**Whole-frame stalls (reported separately):** 18.3 s (Smoke A, PC stopped), 25.1 s, and several of 1.3–3.7 s at BelowNormal; up to 0.83 s at High. They coincide with long UpdateRectTransform/LateUpdate frames in RLO's frame diagnostics. Not attributed to PC.

**Autosave:** health showed `rig_test.autosave_disabled: true` in every run. No manual save fell inside a measured window, and perf-01 made no save at all.

## Findings

1. **Fix slice:** a 135 ms PC advancement spike at the evening rush with 957 residents. [#128](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/128).
2. **Designed refusal, no slice:** pre-F1 writer-25 saves (all 3 Nov fixtures) stop with "Accounting input epoch was lost". The next batch needs fresh head fixtures, which the producer now makes in minutes; F5c-lite's schema 26 will refuse schema-25 cities anyway.
3. **Rig follow-ups (rig repo, not PC):**
   - The player launches at BelowNormal priority and is starved by parallel builds.
   - `shop_fixture` native rental is refused in a fresh head city, with no reason surfaced.
   - Flows need a `wait_state` after an in-session `load` before rig ops run.
4. **Observations:**
   - The Rule 9 detach warning appears on every unload.
   - Route waits never drain to 0; explaining them needs a per-resident readback.

## Run order for the next batch

The checks below are grouped by the fixture they share, so each fixture is built once and the checks that need it run back to back:

1. **Fresh head city** (`produce_fixture.py`; age it at native speed 6, measure only at speed 1, and raise the player to High): f5a-1, f5a-4 (partial), f5b-1 (partial), e2c-1 (partial), f5f-1/f5f-3 (log fields only).
2. **Same city plus one player business**: blocked until native rental works in a head city (refused this session). The business is needed for probe `arm`, then the shared mod-work windows for e5b-6, e5e-3, f3-1, f6b-8, f7a-7, f7b-7 and f7f-4 (combined p95 and ≥30 ms parts only).
3. **Complete staffed/stocked food shop**: blocked. It needs native equipment placement, which failed on 3 Nov (NullReferenceException in `InputActionHelper.WasRightClickReleasedThisFrame`). Every shop, sales, receipt and accounting check waits for it.
4. **Pending rig capabilities**, named per check below: RIG-LIVE, RIG-LEDGER, RIG-CAMPAIGN, RIG-PCSAVE, RIG-SAVECOST, RIG-PCPROP, RIG-MODSET per-flow PC config, RIG-API, RIG-FAULT, RIG-PCFIX, RIG-PERF-UNION and PC component counters, plus unlocked-session (NEEDS-INPUT) UI checks.

## Per-check results

Catalogue of 310 checks. Duplicate pages are skipped in favour of their owning slice files: `f1-accounting-backlog-pr.md`, `pc-f3-bounded-bulk-work-pr.md`, `e5c-pr-description.md`, `e5e-pr-description.md`, and the checklist's own E5b/E5e/F3/F6b entries.

### A3 ([a3-weighted-choice.md](a3-weighted-choice.md))

| Check | Result |
|---|---|
| a3-1 Controlled baseline red to green on fixed seeds | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| a3-2 Seed variation and exact replay | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| a3-3 More than eight shops sampling bounds | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| a3-4 Distant winner outside nearest eight | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| a3-5 Household food choice funded vs unfunded | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| a3-6 Reload mid-decision at every cursor | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| a3-7 Journey and checkout replay across midnight | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| a3-8 Schema-16 migration and next-day activation | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| a3-9 Inspector truth wording | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| a3-10 Contexts and optional-stop interplay | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| a3-11 Accounting reconciliation incl. 950/1000-cent fixture | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| a3-12 Performance and route attribution windows | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |

### A5 ([a5-integrated-acceptance.md](a5-integrated-acceptance.md))

| Check | Result |
|---|---|
| a5-1 Walking-resident hover and click selection | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| a5-2 Popup content and address links | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| a5-3 Input ownership around popup | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| a5-4 Inspector read-only seams and identity | not run: needs check-specific setup — Bridge-select/read popup/map arguments; payload, IDs, routes, market calls, orders, money unchanged across repeated capture, pool reuse and save/reload |
| a5-5 Traits, memory and outcomes dumps | not run: needs check-specific setup — Dump traits, B memory, C2a words (campaigns unknown), Today meals; no numeric readouts, no represented plate on visible resident, no researched/rival/rain claims |
| a5-6 Shortlist and continuation counters | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| a5-7 Red to green decision replay | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| a5-8 Migration, activation and refusal schemas 16-19 to 20 | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| a5-9 Retry, stops and unsafe input changes | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| a5-10 Native accounting across a full day | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| a5-11 Long-session bounds after soak | not run: needs check-specific setup — After soak/reload dump population/consumer/history/memory/pending counts and payload size headroom; use reference 957 and 5k fixtures; no absent-fixture certification |
| a5-12 Integrated matched performance | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction (hover/popup extension) |

### B0 ([b0-schema18.md](b0-schema18.md))

| Check | Result |
|---|---|
| b0-1 Preserve originals and record baseline metadata | not run: needs check-specific setup — Record hashes, versions, seed, setup; use real schema-17 A3 checkpoint untouched; capture A3 ceiling failure and B0 pass separately |
| b0-2 Real schema 17 to 18 migration | not run: needs check-specific setup — Migrate real 17 save to 18, reload twice; all IDs, traits, visits, contexts identical; B empty/inactive; copied active-capability save refused bytes-preserved |
| b0-3 Accounting reconciliation around migration | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| b0-4 Interrupted A3 decision save/reload | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| b0-5 Fill 32 retained visits and inspect payload | not run: needs check-specific setup — Run reference city until 32 retained visits; save/reload compare histories/counters; inspect payload size, refusal preservation, 95% warning |
| b0-6 Separate save-specific profiling run | not run: needs separately authorized save-specific profiling run |
| b0-7 Ordinary-play idle/rush/soak performance | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |

### B0b ([b0b-held-tuning.md](b0b-held-tuning.md))

| Check | Result |
|---|---|
| b0b-1 In-flight held tuning image from saved payload | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| b0b-2 Config edit replay keeps held image | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| b0b-3 Save/reload continuity and schema-17 migration | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| b0b-4 Combined performance windows | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| b0b-5 Saved payload size | not run: needs check-specific setup — Read payload chars/UTF-8 and native save sizes at reference and 5,000-consumer fixture vs B0; image adds 405 bytes, no new records |

### B1 ([b1-memory-loyalty.md](b1-memory-loyalty.md))

| Check | Result |
|---|---|
| b1-1 Freeze setup and autosave protocol | not run: needs check-specific setup — Freeze commit/DLL/config/IDs/seed/checkpoint; record, disable, verify autosave before every window and after reload; restore on cleanup |
| b1-2 Loyal vs curious cohort behaviour | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| b1-3 Induced stockout/closure/queue failures via shop UI | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| b1-4 Recovery days 3-6 after failures | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| b1-5 Memory save/reload and budget replay | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| b1-6 Full native day reconciliation | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| b1-7 Pre-B1 save load and capability refusal | not run: needs check-specific setup — Load pre-B1 save: empty memory, preserved identity/money, B inactive until midnight; lower-capacity load untrimmed; future capability refused preserving payload |
| b1-8 Memory active vs weight-zero performance | not run: needs config change FoodMemoryWeight:0 |

### B2 ([b2-regulars-inspector.md](b2-regulars-inspector.md))

| Check | Result |
|---|---|
| b2-1 Freeze setup and autosave protocol | not run: needs check-specific setup — Freeze hashes, settings, seed, checkpoint, known consumer IDs; autosave disabled and verified before windows and after reload; restore |
| b2-2 Regular qualification over served days | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| b2-3 Stockout ends regular status via shop UI | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| b2-4 Dump-only flag/count transition check | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| b2-5 Regular counts across save/reload and replacement | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| b2-6 Ctrl+F8 Regulars line vs dump | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| b2-7 Popup regular words via hover/click | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| b2-8 Panel/popup closed vs open performance | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |

### C1 ([c1-campaign-reader.md](c1-campaign-reader.md))

| Check | Result |
|---|---|
| c1-1 Native agency options and lifecycle | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| c1-2 Detached campaign snapshot dumps and reload | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c1-3 Campaign mapping evidence stays Unknown | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c1-4 Reader native noninterference | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c1-5 Matched reader performance 1/8 scope | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |

### C2a ([c2a-core-awareness.md](c2a-core-awareness.md))

| Check | Result |
|---|---|
| c2a-1 Awareness source/position capture bounds | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c2a-2 Distant shop becomes eligible | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c2a-3 Refusal after admission | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c2a-4 Campaign and memory boundaries | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c2a-5 Schema 18 to 19 migration, replay, size | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c2a-6 Matched performance vs B0b | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c2a-7 Visible journey sanity | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| c2a-s2-1 Awareness source/position capture bounds (2nd section) | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c2a-s2-2 Distant shop becomes eligible (2nd section) | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c2a-s2-3 Refusal after admission (2nd section) | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c2a-s2-4 Campaign and memory boundaries (2nd section) | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c2a-s2-5 Schema 18 to 19 migration, replay, size (2nd section) | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c2a-s2-6 Matched performance vs B0b (2nd section) | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c2a-s2-7 Visible journey sanity (2nd section) | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |

### C2b ([c2b-campaign-awareness.md](c2b-campaign-awareness.md))

| Check | Result |
|---|---|
| c2b-1 Prepare native campaign before acquisition | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| c2b-2 First eligible boundary and stable hours | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c2b-3 Active campaign effect across all residents | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c2b-4 Stop campaign before next hour | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| c2b-5 Stop, ineligibility, retirement and fade | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c2b-6 Save/reload and native noninterference | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c2b-7 Final-head matched performance | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |

### C3 ([c3-agency-payment.md](c3-agency-payment.md))

| Check | Result |
|---|---|
| c3-1 Agency binding diagnostic capture | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c3-2 One native campaign purchase via UI | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| c3-3 First daily marketing charge reconciliation | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c3-4 Insufficient-funds campaign charge | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| c3-5 100 repeated C3 reads noninterference | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| c3-6 Diagnostic vs idle performance | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |

### C4 ([c4-market-research.md](c4-market-research.md))

| Check | Result |
|---|---|
| c4-1 No research command/job/report present | not run: needs check-specific setup — Enumerate PC diagnostics/bridge commands; no research command/job/cache; NoPaymentPath.TryGet returns false/null; zero C4 route requests vs B0b |
| c4-2 No research UI reachable visually | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| c4-3 Payload bytes identical vs B0b | not run: needs check-specific setup — Same schema-18 checkpoint under B0b and candidate: identical persistentcity:state_v1 bytes/length/SHA-256 after round trip and save/reload; no research fields |
| c4-4 Candidate vs B0b performance | not run: needs check-specific setup — Matched idle/meal windows panels closed/open; mod-work p95<5 ms, flag >=30 ms; zero C4 idle work, allocation or route requests |

### D1 ([d1-rival-catalogue.md](d1-rival-catalogue.md))

| Check | Result |
|---|---|
| d1-1 Rival reader facts vs native state | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| d1-2 D1 refresh native noninterference | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| d1-3 Real rival price/hour change | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| d1-4 Ownership transfer or replacement identity refusal | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| d1-5 Exercised reader vs idle performance | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |

### D2 ([d2-rival-outflow.md](d2-rival-outflow.md))

| Check | Result |
|---|---|
| d2-1 Rival chosen over player shop | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| d2-2 Timed rival outing and return | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| d2-3 One exact external debit | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| d2-4 Changed rival terms and ownership | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| d2-5 Interrupted reload of rival journeys | not run: needs config change (rival weight zero) |
| d2-6 Native non-mutation and player control | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| d2-7 Matched rival performance windows | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| d2-8 Visible rival customer continuity | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |

### D3a ([d3a-outflow-line.md](d3a-outflow-line.md))

| Check | Result |
|---|---|
| d3a-1 Free outflow totals and exact debits | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| d3a-2 Native rival non-mutation and player control | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| d3a-3 Managed price/stock/hours changes via UI | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| d3a-4 Reload with B1 inactive, campaigns disabled | not run: needs prepared settings (memory/campaign disabled) |
| d3a-5 Closed-panel rush and late-burst performance | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| d3a-6 Open free panel visual and perf | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |

### D4 ([d4-rival-memory.md](d4-rival-memory.md))

| Check | Result |
|---|---|
| d4-1 Rival memory served counts and reload | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| d4-2 Induced rival closure and recovery | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| d4-3 Replay closure/recovery checkpoints | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| d4-4 No native rival write in memory arms | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| d4-5 Active vs empty rival memory performance | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| d4-6 Inspector rival memory wording | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |

### E1 ([e1-rain.md](e1-rain.md))

| Check | Result |
|---|---|
| e1-1 Dry vs rain matched decisions | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e1-2 Rain transition safety | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e1-3 Captured weather replay across reload | not run: needs E1 tuning config change |
| e1-4 Schema 19 to 20 migration | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e1-5 Matched performance vs C2a 9b13698 | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e1-6 Visible rain continuity and selection | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |

### E2a ([e2a-street-life.md](e2a-street-life.md))

| Check | Result |
|---|---|
| e2a-1 Street-life state and accounting matrix | not run: needs config change between 0/25/100 arms |
| e2a-2 Visible street-life matrix | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| e2a-3 Mid-stop reload | not run: needs check-specific setup — Save/reload during each stop kind; IDs, start/end, membership, deadline unchanged; rain cancellation never restarts or replays |
| e2a-4 Food priority over work break | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e2a-5 E2b before-red state evidence | not run: needs check-specific setup — Record R1 rain-cancelled future break, R4 queue lead reload; R2 entrance multiplicity; R3 frontage ownership; no fabricated red |
| e2a-6 E2b before-red visual evidence | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| e2a-7 Candidate venue facts | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction (frontage confirmation) |
| e2a-8 Matched street-life performance | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction (UI states) |

### E2b ([e2b-street-life.md](e2b-street-life.md))

| Check | Result |
|---|---|
| e2b-1 Frontage ownership and refusals | not run: needs check-specific setup — Bridge-read controllers, registrations, entrances, buildings cache before/after reload; each frontage one address/controller; refusal categories; bounded dumps |
| e2b-2 Bound queue line in rain and reload | not run: needs check-specific setup — Bound queuer stays in PlanQueues through rain change; move-up only to free spot; reload during wait reconstructs per weather; no extra visit |
| e2b-3 Default vs tuned stop timing ledger | not run: needs config arms with city unloaded |
| e2b-4 R1 future-break evidence | not run: needs check-specific setup — Find assigned-work admission with future break; record rain onset/clearing, cancellation minute, state at planned start incl. reload |
| e2b-5 R3 nightclub ownership evidence | not run: needs check-specific setup — Nightclub frontage: type flags, registration, spawner, body ownership vs queue/chat positions; show whether native bodies coincide with stops |
| e2b-6 Visual confirmation for R1/R3 | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| e2b-7 Matched performance with rebind counters | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction (UI states) |

### E2c ([e2c-interior-guard.md](e2c-interior-guard.md))

| Check | Result |
|---|---|
| e2c-1 Guard not detected on normal profile | passed (partial): load log `ThirdPartyInteriorGuard: not detected; not detected; effective view=off`. Shop and interior-view checkpoints not run: needs a complete food-shop fixture |
| e2c-2 Harmless stand-in detection | not run: needs isolated external-path stand-in loading |
| e2c-3 Guard save/reload payload unchanged | not run: needs check-specific setup — Payload bytes/hash identical with guard auto/off and stand-in refusal; save/reload unchanged; evaluation once per load |
| e2c-4 Guard auto vs off performance | not run: needs config change guard auto/off |

### E3-cupcake ([e3-cupcake.md](e3-cupcake.md))

| Check | Result |
|---|---|
| e3-cupcake-1 cupcake reader vs independent bridge fields | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-cupcake-2 cupcake fixture setup and visual purchase | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| e3-cupcake-3 Natural cupcake transaction and settlement | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-cupcake-4 cupcake repeated-read and reload noninterference | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-cupcake-5 Shared-reader donut regression | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-cupcake-6 Reader matched performance and cleanup | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |

### E3-donut ([e3-donut.md](e3-donut.md))

| Check | Result |
|---|---|
| e3-donut-1 donut reader vs independent bridge fields | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-donut-2 donut fixture setup and visual purchase | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| e3-donut-3 Natural donut transaction and settlement | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-donut-4 donut repeated-read and reload noninterference | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-donut-5 Reader matched performance and cleanup | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |

### E3-frenchfries ([e3-frenchfries.md](e3-frenchfries.md))

| Check | Result |
|---|---|
| e3-frenchfries-1 frenchfries reader vs independent bridge fields | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-frenchfries-2 frenchfries fixture setup and visual purchase | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| e3-frenchfries-3 Natural frenchfries transaction and settlement | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-frenchfries-4 frenchfries repeated-read and reload noninterference | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-frenchfries-5 Shared-reader donut regression | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-frenchfries-6 Shared-reader cupcake regression | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-frenchfries-7 Shared-reader salad STOP regression | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-frenchfries-8 Reader matched performance and cleanup | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |

### E3-icecream ([e3-icecream.md](e3-icecream.md))

| Check | Result |
|---|---|
| e3-icecream-1 icecream reader vs independent bridge fields | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-icecream-2 icecream fixture setup and visual purchase | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| e3-icecream-3 Natural icecream transaction and settlement | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-icecream-4 icecream repeated-read and reload noninterference | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-icecream-5 Shared-reader donut regression | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-icecream-6 Shared-reader cupcake regression | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-icecream-7 Shared-reader salad regression | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-icecream-8 Reader matched performance and cleanup | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |

### E3-pizza ([e3-pizza.md](e3-pizza.md))

| Check | Result |
|---|---|
| e3-pizza-1 pizza reader vs independent bridge fields | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-pizza-2 pizza fixture setup and visual purchase | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| e3-pizza-3 Natural pizza transaction observations | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-pizza-4 Pizza settlement and reload observations | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-pizza-5 pizza repeated-read and reload noninterference | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-pizza-6 Donut/cupcake/salad shared-reader regressions | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-pizza-7 Reader matched performance and cleanup | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |

### E3-remaining ([e3-remaining-batch.md](e3-remaining-batch.md))

| Check | Result |
|---|---|
| e3-remaining-1 Cheeseplatter fields and concessions contract | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-remaining-2 Cottoncandy fields and independence | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-remaining-3 Popcorn fields and independence | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-remaining-4 Kabob actual retail path | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-remaining-5 Candycane retail and seasonal availability | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-remaining-6 Christmascookies retail and availability | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-remaining-7 Remaining-batch fixture setup and visual purchases | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| e3-remaining-8 Natural transaction and settlement per candidate | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-remaining-9 Batch repeated-read noninterference | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-remaining-10 All six existing readers regression | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-remaining-11 Batch matched performance and cleanup | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |

### E3-salad ([e3-salad.md](e3-salad.md))

| Check | Result |
|---|---|
| e3-salad-1 salad reader vs independent bridge fields | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-salad-2 salad fixture setup and visual purchase | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| e3-salad-3 Natural salad transaction and settlement | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-salad-4 salad repeated-read and reload noninterference | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-salad-5 Shared-reader donut regression | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-salad-6 Shared-reader cupcake regression | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e3-salad-7 Reader matched performance and cleanup | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |

### E4 ([e4-wider-menu.md](e4-wider-menu.md))

| Check | Result |
|---|---|
| e4-1 Largest supported menu bridge dump | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e4-2 Menu UI edits and shortages | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| e4-3 Boundary, shared shortages and counters | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e4-4 Exact checkout and save/reload | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e4-5 Unsupported-edit handoff and reacquisition | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction (preparing edits) |
| e4-6 Base vs candidate performance | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |

### E5a ([e5a-catalogue-eligibility.md](e5a-catalogue-eligibility.md))

| Check | Result |
|---|---|
| e5a-1 Independent loaded catalogue truth | not run: needs bounded loaded-catalogue/definition dump; explicit E5a continuation invocation |
| e5a-2 Actual shop/holder contract | not run: needs normal-gameplay disposable mixed-shop fixture preparation; independent holder/menu field capture |
| e5a-3 Lifecycle, removal and atomicity | not run: needs instrumented lifecycle trace; E5a dirty/cache harness; isolated mod-present/absent/returned load sequence |
| e5a-4 Read noninterference with 10k cache reads | not run: needs exact paused before/after native field fingerprints; E5a read counters |
| e5a-5 Batched performance and cleanup | not run: needs E5a cold/warm/dirty/backlog component counters |

### E5b ([e5b-complete-menu.md](e5b-complete-menu.md))

| Check | Result |
|---|---|
| e5b-1 Catalogue and definition truth | not run: needs independent loaded tag-cache and effective-holder identity trace |
| e5b-2 Mixed-shop fixture and automated visuals | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| e5b-3 Native-owned checkout and accounting | not run: needs independent PC receipt/authority and native producer/resource/order delta comparator |
| e5b-4 Dirty changes and whole-business boundary | not run: needs PC notification/publication/requested-from-until trace; config-variant staging |
| e5b-5 Identity, removal/return and held replay | not run: needs Street Food absent control transport; PC semantic held/name/receipt comparator |
| e5b-6 Noninterference and batched performance | not run: needs the full mod-work union, i.e. probe `arm` on a player business (fresh-city rental refused) or PC component counters. PC advancement counters only: speed-1 means 0.12–0.44 ms/frame, one 135 ms spike flagged in [#128](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/128) |

### E5c ([e5c-neutral-metadata.md](e5c-neutral-metadata.md))

| Check | Result |
|---|---|
| e5c-1 Isolated config and Food-default control | not run: needs transactional isolated config staging; effective per-resident E5c metadata projection |
| e5c-2 Bad config and once-only diagnostics | not run: needs automated invalid-config matrix; per-load diagnostic counting |
| e5c-3 Stable resident likes across save/reload | not run: needs per-resident name-taste snapshot comparison; reordered-input orchestration |
| e5c-4 Native-contract separation and noninterference | not run: needs matched metadata-projection noninterference; combined E5b class/control assertions |
| e5c-5 Metadata batched performance and cleanup | not run: needs metadata-table/offer-width batch orchestration with transactional cleanup |

### E5d ([e5d-name-identity-save.md](e5d-name-identity-save.md))

| Check | Result |
|---|---|
| e5d-1 Real schema 24 to 25 migration | not run: needs PC semantic save-projection comparator |
| e5d-2 Refusal and downgrade preserve source | not run: needs bounded PC payload fault matrix in copied FT checkpoints |
| e5d-3 Save cost and limits on real checkpoint | not run: needs PC payload counts, byte budgets and read/write/activation timers in flow evidence |
| e5d-4 Names with Street Food present/absent/returned | not run: needs isolated mod-profile matrix with independent transport and PC identity assertions |
| e5d-5 Quote/journey/receipt replay | not run: needs deterministic PC continuation checkpoints and independent receipt/order delta assertions |
| e5d-6 Batched performance and noninterference | not run: needs unified PC mod-work/whole-frame evidence assertions |

### E5e ([e5e-metadata-api.md](e5e-metadata-api.md))

| Check | Result |
|---|---|
| e5e-1 Real provider lifecycle, precedence and conflicts | not run: needs RIG-API; RIG-MODSET; RIG-LIVE (prerequisite) |
| e5e-2 Held replay and native-contract separation | not run: needs RIG-API; RIG-MODSET; RIG-CAT/RIG-SHOPEDIT; RIG-LIVE (prerequisite) |
| e5e-3 Provider noninterference and performance | not run: needs the full mod-work union, i.e. probe `arm` on a player business (fresh-city rental refused) or PC component counters. PC advancement counters only: speed-1 means 0.12–0.44 ms/frame, one 135 ms spike flagged in [#128](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/128) |

### F0 ([f0-interior-view.md](f0-interior-view.md))

| Check | Result |
|---|---|
| f0-1 Default/off equals today | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| f0-2 View on in ground-floor shop | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| f0-3 Native street activity while inside | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| f0-4 Elevator/upper-floor fallback | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| f0-5 Continuity and restore across transitions | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| f0-6 Interior off/on matched performance | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| f0-7 Failure and unload fail-closed | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |

### F1-accounting ([f1-accounting-backlog.md](f1-accounting-backlog.md))

| Check | Result |
|---|---|
| f1-accounting-1 One-day cadence/input replay | not run: needs fixed 30/60/120 rendered-cadence and scheduler-budget arms; minute-stamped input publication; independent PC/native ledger comparator |
| f1-accounting-2 Overload and recovery | not run: needs ready/no-due-work control; controlled scheduler stall/budget injection; AccountingDiagnostics sampling; automated pacing-notice capture |
| f1-accounting-3 Meal/hour/midnight and skip boundaries | not run: needs exact native float-fence/ending-accounting hook trace with time-machine control and pending-work assertions |
| f1-accounting-4 In-flight save/reload equivalence | not run: needs named held-decision/rounding-gap/midnight-backlog checkpoints; receipt/funds comparator |
| f1-accounting-5 Complete cost and throughput | not run: needs complete mod-work union; independent whole-frame/presented-frame intervals correlated with F1 lag |
| f1-accounting-6 Clock/authority interop and refusal | not run: needs final tick-prefix/Driven Rides observer trace; disposable bypass-clock fault arm; same-session save-identity/attachment/stop-marker lifecycle assertions |

### F1-shop-view ([f1-shop-view.md](f1-shop-view.md))

| Check | Result |
|---|---|
| f1-shop-view-1 Three own shops and elevator facts | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| f1-shop-view-2 Visible street at prepared viewpoints | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction (missing viewpoints) |
| f1-shop-view-3 Committed customer through door | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| f1-shop-view-4 Interactive customer following | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| f1-shop-view-5 Save/reload, re-entry and exit paths | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| f1-shop-view-6 E2c forced-off guard | not run: needs isolated stand-in loading |
| f1-shop-view-7 F0 off vs on perf windows | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |

### F1b ([f1b-street-proxy.md](f1b-street-proxy.md))

| Check | Result |
|---|---|
| f1b-1 Explicit/off contract and cached guard | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| f1b-2 Exact door IDs and candidate frames | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| f1b-3 Independent edge residuals | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction (if landmarks need visual input) |
| f1b-4 Bounded street inventory at all radii | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| f1b-5 Repeated-read and reload read-only | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| f1b-6 One-shot command costs and ordinary windows | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| f1b-7 Prepare medium/large/elevator fixtures | not run: needs NEEDS-INPUT unlocked-session pointer/keyboard/UI interaction |
| f1b-8 Repeat probes on prepared fixtures | not run: needs FIXTURE-NEEDED (check 7) |

### F5a ([f5a.md](f5a.md))

| Check | Result |
|---|---|
| f5a-1 Reference-city census publication | passed (log part only): fresh head city census `policy=world-census-v1`, no `refused=`, residents=957, homes=335, beds=1064, `check_households_within_beds=ok`, persisted_persons=2871 (<=150000). `discovered_stations=20` on the first completed read after a fresh-process load (`rig1106-smoke-c-01` step 5). The census ran at speed 1, not paused |
| f5a-2 Census vs independent native reads | not run: needs read-only native station-name list with DLC visibility; rival aiEmployees count readback (extension of rival list); RIG-LIVE |
| f5a-3 Census read-only: no save/staff/business change | not run: needs RIG-PCSTATE; RIG-LIVE |
| f5a-4 Census reload stability and pending stations | passed (partial): census `status=pending` right after both the fresh-process load and the in-session reload; the completed reads give the same hash `e97e457a` at minute 4920. Paused PC state hash comparison not run: needs RIG-PCSTATE |
| f5a-5 Census performance subset | not run: needs RIG-PCFIX; RIG-LIVE |

### F5b ([f5b-identity-core.md](f5b-identity-core.md))

| Check | Result |
|---|---|
| f5b-1 Save text unchanged across builds | passed (partial): probe `runtime` shows saved schema 25, header in the supported range, no refusal, after the fresh-process load and the in-session reload. Byte comparison not run: needs the base-build reference |
| f5b-2 No native employment change | not run: needs RIG-LIVE |
| f5b-3 RLO identity contract and identities unchanged | not run: needs RIG-LIVE |
| f5b-4 F5b performance subset | not run: needs RIG-LIVE; RIG-PCFIX |

### F5f ([f5f.md](f5f.md))

| Check | Result |
|---|---|
| f5f-1 Bodies capped and index matches scan | passed (log part only): 36/36 physical lines have `journeyIndexMismatches=0`, `journeyIndexFallback=False`, `journeyIndexWithheldVisitors=0`; pedestrians <=48, cars <=7. Camera moves and `screen_pos` body IDs not run: needs RIG-VIEW live proof (RIG-LIVE) |
| f5f-2 Reload, hidden skip, lease release | not run: needs RIG-PCSTATE; RIG-LIVE |
| f5f-3 No visitor receives a body | passed (log part only): journey-index counters as f5f-1. Body identity across camera moves not run: needs RIG-VIEW live proof (RIG-LIVE) |
| f5f-4 F5f selection performance subset | not run: needs RIG-PCFIX (reference and stress fixtures); RIG-LIVE |

### F6a ([f6a-intervention-harness.md](f6a-intervention-harness.md))

| Check | Result |
|---|---|
| f6a-1 Baseline ledger reconciliation 7 days | not run: needs RIG-LEDGER |
| f6a-2 Unlimited rival baseline before F6b | not run: needs RIG-RIVALFP; per-hour rival settlement counts from PC rival diagnostics |
| f6a-3 Price intervention over 3 weeks | not run: needs RIG-SHOPEDIT price edit; RIG-CAMPAIGN; RIG-LEDGER |
| f6a-4 Staffing intervention | not run: needs RIG-SHOPEDIT staff removal; RIG-CAMPAIGN; RIG-LEDGER |
| f6a-5 Hours intervention | not run: needs RIG-SHOPEDIT hours; RIG-CAMPAIGN; RIG-LEDGER |
| f6a-6 Shortage intervention | not run: needs RIG-SHOPEDIT set/zero stock; RIG-CAMPAIGN; RIG-LEDGER |
| f6a-7 Employment shock baseline | not run: needs RIG-ACCESS; RIG-CAMPAIGN; RIG-LEDGER |
| f6a-8 Access intervention and second site | not run: needs RIG-ACCESS; RIG-CAMPAIGN; RIG-LEDGER |
| f6a-9 Household sustainability soak | not run: needs RIG-CAMPAIGN |

### F6b ([f6b.md](f6b.md))

| Check | Result |
|---|---|
| f6b-1 Lane and sell-out refusals | not run: needs RIG-MODSET per-flow PC config selection with restore |
| f6b-2 Calibration: native staffed throughput | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| f6b-3 Estimate labels | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| f6b-4 More own-shop custom when rival full | not run: needs RIG-MODSET per-flow PC config; RIG-LEDGER |
| f6b-5 No native rival write | not run: needs RIG-RIVALFP |
| f6b-6 Reload mid-queue and mid-service | not run: needs a complete staffed/stocked food-shop fixture with native sales (no such fixture exists; native equipment placement refused on 3 Nov) |
| f6b-7 Profile change across reload | not run: needs RIG-MODSET per-flow PC config selection with restore |
| f6b-8 F6b performance window | not run: needs the full mod-work union, i.e. probe `arm` on a player business (fresh-city rental refused) or PC component counters. PC advancement counters only: speed-1 means 0.12–0.44 ms/frame, one 135 ms spike flagged in [#128](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/128) |

### F6c ([f6c.md](f6c.md))

| Check | Result |
|---|---|
| f6c-1 Panel text equals PC projection | not run: needs RIG-PCPROP; RIG-LIVE |
| f6c-2 Counters equal rescans through reload/migration | not run: needs RIG-PCPROP; RIG-MODSET predecessor build; RIG-LIVE |
| f6c-3 Staffing gap shows as evaluated offers | not run: needs RIG-SHOPEDIT staff removal with readback; RIG-PCPROP; RIG-LIVE |
| f6c-4 Week boundary and no-record labels | not run: needs RIG-PCPROP; RIG-CAMPAIGN; RIG-LIVE |
| f6c-5 No research, rival names or shares | not run: needs RIG-PCPROP; RIG-LIVE |
| f6c-6 Read-only hidden panel and performance | not run: needs RIG-PCSTATE; unified PC mod-work/whole-frame evidence assertions; RIG-PCPROP; RIG-MODSET; RIG-LIVE |
| f6c-7 Inspector budget words | not run: needs RIG-INSPECT; RIG-PCSTATE; RIG-LIVE |
| f6c-8 Panel layout and screenshots | not run: needs RIG-LIVE |

### F7a ([f7a.md](f7a.md))

| Check | Result |
|---|---|
| f7a-1 Default off does no save-cost work | not run: needs RIG-SAVECOST |
| f7a-2 Diagnostics never change payload | not run: needs RIG-SAVECOST; RIG-PCSAVE |
| f7a-3 Save phase cost on 957 checkpoint | not run: needs RIG-SAVECOST; RIG-SAVEPROF; RIG-SAVETYPES; pc-representative-957@1 fixture |
| f7a-4 Load main-thread cost | not run: needs RIG-SAVECOST; RIG-SAVEPROF load timers |
| f7a-5 Large-city save and load | not run: needs RIG-SAVECOST; RIG-SAVEPROF; city-50k@1 fixture |
| f7a-6 Clean windows contain no save callback | not run: needs RIG-SAVECOST; RIG-PERF-UNION |
| f7a-7 Armed diagnostics add no mod-work | not run: needs the full mod-work union, i.e. probe `arm` on a player business (fresh-city rental refused) or PC component counters. PC advancement counters only: speed-1 means 0.12–0.44 ms/frame, one 135 ms spike flagged in [#128](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/128) |

### F7b ([f7b.md](f7b.md))

| Check | Result |
|---|---|
| f7b-1 Coherent save-epoch success pairs | not run: needs RIG-PCSAVE |
| f7b-2 Save, quit to menu, reload | not run: needs RIG-PCSAVE |
| f7b-3 Relocation copy | not run: needs RIG-PCSAVE extended with relocation-path trigger |
| f7b-4 Forced encode failure then native progress | not run: needs RIG-FAULT |
| f7b-5 Marker mismatch and unknown version | not run: needs RIG-FAULT; RIG-PCSAVE |
| f7b-6 Runtime failure outside a save | not run: needs RIG-FAULT |
| f7b-7 No ordinary-play save-epoch cost | not run: needs the full mod-work union, i.e. probe `arm` on a player business (fresh-city rental refused) or PC component counters. PC advancement counters only: speed-1 means 0.12–0.44 ms/frame, one 135 ms spike flagged in [#128](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/128) |

### F7f ([f7f.md](f7f.md))

| Check | Result |
|---|---|
| f7f-1 Historical payloads through frozen readers | not run: needs RIG-PCSAVE; production and sealing of fixtures |
| f7f-2 Historical fixture loads under current build | not run: needs RIG-PCSAVE; production of fixtures |
| f7f-3 Refused payloads stay unchanged | not run: needs RIG-PCSAVE; RIG-FAULT-style modData key replacement |
| f7f-4 No runtime change and no save work | not run: needs the full mod-work union, i.e. probe `arm` on a player business (fresh-city rental refused) or PC component counters. PC advancement counters only: speed-1 means 0.12–0.44 ms/frame, one 135 ms spike flagged in [#128](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/128) |

### F3 ([pc-f3-bounded-bulk-work.md](pc-f3-bounded-bulk-work.md))

| Check | Result |
|---|---|
| f3-1 Integrated peak and backlog | failed (partial run): 957-resident evening speed-1 window had a single 134.96 ms PC advancement call (assertion: single mod-work intervals <30 ms), [#128](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/128). 10k/100k stress, backlog and the 60 FPS arm not run: needs the F3 work-union/backlog profiler with stress-fixture provisioning |
| f3-2 Posting interruption and save/reload | not run: needs stop-after-posting-unit checkpoint and canonical event/ledger comparison |
| f3-3 Route restart stress | not run: needs managed-refresh churn injection, native invalidation and event-lease assertions |
| f3-4 Campaign observation capture/coherence | not run: needs candidate lifecycle/campaign-scope fixtures and interrupted publication/save assertions |
| f3-5 Non-preemptible native query tails | not run: needs native query attribution/histograms, reserve readback and finite-admission assertion stream |

