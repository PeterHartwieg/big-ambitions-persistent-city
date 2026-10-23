# Automatic food routines — 22 October 2026

The subsequent [demand and scaling audit](2026-10-22-food-demand-audit.md) explains the low volume, adds varied work/study break flexibility and reduces repeated decision/path work. The evidence below belongs to the earlier fixed-45-minute work-break candidate.

The current candidate connects recurring meal decisions to the population scheduler and a saved, explicitly selected native retail scope. All 1,023 offline tests and the native compilation pass. On the isolated PC rig, 957 residents produced 24 natural purchases over three days at one 15/hour fixture, settling $246.0672 through the game's accounting. Four native reload checkpoints and deliberate paid-purchase replays passed. This is an opt-in development candidate; ordinary saves are not enrolled.

## Behavior

Each resident gets one discretionary midday buying opportunity per day, timed from their existing wake rhythm with stable daily variation. At a suitable actual stop, the model considers a break of up to 45 minutes at work/study or 75 minutes at home/leisure, including outward travel, service, eating and return before the next commitment. Work breaks are provisional policy, not employee roster observations. Preparing/bringing food can beat buying; no minimum-sales or viability rule is added.

The same price, allowance, customer-service quality, district demand and travel model chooses among the selected shops. Stock is reserved for committed outings; service is serialized at each native-derived counter capacity. Stock, staffing, open hours and the approved price are checked again at checkout. A resident returns to the original activity through the existing journey scheduler. No separate simulated food traveller exists.

Schema 8 stores the retail scope, resident meal records, allowance, appointment state and cumulative individual purchase counts/spending alongside the population. Schemas 1–7 migrate without opting into food sales. Each resident/day has one stable intention. Saving before travel, in service or after payment preserves ownership. Native mutations and the resulting record occur synchronously before the native save snapshot. A small native-save receipt also blocks replay if the population callback stops or retains an older snapshot; pending/uncertain receipts are never retried. Receipt retention is bounded by pruning older native days at the next checkout. This does not claim recovery from arbitrary torn/corrupted native files. Current native stock is not used to invent historical sales when the population catches up across native hours.

The explicit entry point is `PopulationRuntime.StartFoodRoutines`. Saved enrolled cities resume their scope automatically; ordinary cities have no food scope. The first native adapter supports one stocked pastry at one to eight selected player-owned full-service shops advertising only that product. Rivals and prepared/shared ingredients remain outside this slice. The native hooks suppress customer generation, customer draws and duplicate native sale processing for those registrations. Hourly restocking, stock tasks and daily order/revenue settlement remain native.

## Offline verification

Sixteen new cases cover natural opportunities, return home and to work, five interruption checkpoints, repeat reload and route rebuild, daily recurrence and different frame/event budgets, counter spacing, staffing changes during travel, stock/price changes after departure, uncertain native acknowledgements, catch-up without historical sales, no-sales pricing, migration, invalid ownership and native receipts with an older population snapshot. All existing tests pass after legacy test payload builders were updated to omit schema-8 fields. The complete suite has 1,023 cases. Native compilation uses the freshly copied owned PC references and warnings as errors.

## Native campaign and retained failures

The first attempt exposed an invalid fixture: its register, support and showcase had unset positions. The native compatibility validator deletes zero-position items on load and removes their work shifts. Resident intention and journey ownership survived, but the stock/staff assertion correctly failed. The fixture now uses nonzero native layout positions for the same building size and the native registration insertion method. No inventory is restored or fabricated during loading to hide this failure; visual furniture placement is not validated.

The corrected preliminary run passed travel, service and paid-order reloads and completed five natural sales. It was stopped after those checks to add the native-save receipt guard. The final main build passed the same checkpoints and explicitly blocked a duplicate checkout. The first two natural days produced eight sales each. The diagnostic's implicit work-shift enum was then found to mean Cleaning, which the native end-of-shift restocker excludes. The helper now explicitly uses Default; the retained city received that fixture-only correction after day two's trading. A third natural day passed actual end-of-shift restocking with the same main mod binary and preserved every earlier sale and receipt. These fixture corrections do not alter household choices, prices or the economy's model.

## Final native results

The disposable city `FT Food Routines 20261022 r4` used a controlled player shop at 30 Third Street: one advertised croissant, 300 pastries and bags, a supported register, 08:00–18:00 hours and a 75-skill employee. The native-derived capacity was 15/hour. The helper supplied the fixture and activated the production scheduler; it did not select buyers, appetite times or acceptance decisions.

| Native day | Purchases | Settled gross revenue |
|---|---:|---:|
| 1 | 8 | $82.0224 |
| 2 | 8 | $82.0224 |
| 3 | 8 | $82.0224 |
| Total | 24 | $246.0672 |

Pastries and raw bag inventory both fell from 300 to 276, exactly matching the purchase count. All pending orders cleared through native daily settlement; native daily customer counts summed to the same 24 resident purchases. Sixteen paid receipts remained for days two and three after bounded pruning. No uncertain sale remained and all meal outings had returned by the final day-four 01:00 checkpoint. The same eight people bought on these three days; this fixture establishes recurrence, not daily customer turnover or calibrated citywide demand.

Native save/reload checks preserved the resident intention and journey while travelling, while serving and after payment. A fourth reload after daily settlement preserved purchases, spending, native history, receipts and stock. Explicit replays of a paid intention before and after settlement returned its receipt without another order or stock debit. The corrected third-day shift recorded ten restocking checks, one native restocking call and ten stock-task updates. With no reserve inventory, this verifies that native housekeeping still executes, not delivery or replenishment of consumed stock.

The first natural buyer left work at 32 Third Street, walked to the shop and returned to the same activity. Native unit price was $10.2528; the individual allowance records rounded cents ($10.25). The resulting $0.0672 aggregate difference across 24 purchases is rounding, not an additional native credit. These are logical native journeys and purchases; physical rendering was disabled throughout.

### Build and retained evidence

The exact tested main DLL is `28dfe482e2277819391a4d87a302e1aba0c4c24b36c6fa68dd081221492ae70e`. All 64 compiled source files are frozen in `build/food-routine-sales-20261022/source-manifest-r4.json`. Helper r4 (`025d00fd…`) ran the first two days and interruption checks; helper r5 (`208307f7…`) corrected only fixture shift typing and ran the third day and settled reload. Both reference the same final main DLL. The private package contains that DLL with `Enabled=false`.

Ignored `build/food-routine-sales-20261022/` retains `full-tests-r4.log`, `native-build-r4.log`, before/after checkpoint captures, `duplicate-attempt.json`, `fixture-shift-correction.json`, `final.json`, `native-checks.json`, `after-settled-reload.json`, `duplicate-after-settlement.json` and `settled-reload-checks.json`. Failed/preliminary attempts remain separately identified; no save, game assembly or decompilation belongs in Git. `final-source-check.json` confirms all 64 compiled sources and both packaged binaries still match their tested manifests.

### Rig restoration

All 3,721 preexisting profile files, original preferences and the installation manifest were restored and verified against the fresh complete backup at `C:\BigAmbitions-AgentTests-Rollback\FoodRoutineSales-20261022`. The helper was removed, logs/results retained and ownership released. Fresh status confirms the rig stopped, no held reservation and zero pending operations. `cleanup.json`, `final-rig-status.json` and `final-lock-status.json` retain that evidence. New disposable FT saves remain available; the normal game/profile, PC power, network and backups were untouched.

## Limits and next step

Temporary individual allowances are purchasing power, not measured wages or a cash ledger. Preparing or packing food remains an outside-option comparison, not a simulated ingredient purchase or meal consumption. Work/study breaks are provisional. Native checkout is logical; visible counter interaction and sustained physical/rendered performance require separate validation. Current scope does not establish small-shop profitability or complete mod/no-mod sales parity.

Next, give households coherent income and expenses before broadening product categories and supply chains. Keep realistic diversity and failure possible.
