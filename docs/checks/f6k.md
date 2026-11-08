Status: implemented (offline); not verified by rig

Refs #93. Task `pc-f6k-price-weight`, base `codex/persistent-city` at `077d27c7` (F7d #158, includes F6h-after-F6j #160 and F6j #156). Writer 34, no schema change. Decision: [price weight 1.2](../decisions/2026-11-08-price-weight.md). Numbers: [performance](../perf/f6k.md).

## What changed

- `Core/FoodChoiceSettings.cs`: `FoodChoicePriceWeight` defaults to 1.2, and the NaN fallback in `Clamp` is 1.2. The 0–3 clamp is unchanged. `FoodChoiceHouseholdWeight` stays 1.
- `config/persistent-city.json`: `FoodChoicePriceWeight` 1.0 → 1.2. The file set the key explicitly, so leaving it would have kept new installs at 1.
- `Core/FoodChoicePolicy.cs`: comment only (the neutral +50% weight is now 0.38, not 4/9). `PriceFactor`, `TicketFactor` and `MealReferenceCents` are unchanged.
- `tools/economy-interventions`: `RunOptions.PriceWeight` and the `--price-weight` fallback follow the production default, so a run without the flag measures the shipped setting. Pass `--price-weight 1` to reproduce #160.
- Docs: the F6h checks file, the F6 plan and the 6 November defaults file record that F6h no longer stops on profit non-dominance.

## Saves and existing cities

There is no migration and no schema bump. The live weight is not a per-city save field: `PersistentCityMod` reads `persistent-city.json` at every load, so an existing development city loaded with the shipped file uses 1.2 for new decisions. Held decisions keep the tuning image they captured (a captured 1 stays 1 until that decision resolves). A user file that sets 1 keeps 1.

## Tests

New `tests/F6kPriceWeightTests.cs`:

- (a) compiled default 1.2, also through `PrototypeSettings` and an empty JSON object; (b) shipped config 1.2; (e) `FoodChoiceHouseholdWeight` 1 in both.
- (c) clamp: −1 → 0, 9 → 3, ±∞ → 0/3, NaN → 1.2.
- (d) a +50% offer against $12.50 keeps a strictly smaller share of its weight at 1.2 than at 1, for bands 0, 2 and 4; neutral is (2/3)^2.4 versus 4/9.
- (g) writer 34; the tuning wire keeps its length, round-trips, and a held image captured at 1 reads back as 1.

Updated where the 1 → 1.2 default legitimately moves numbers:

- `F6hIntegratedTests.ExistingDefaultsStayPinnedUntilTradeoffGateCanPass` is renamed `OtherF6DefaultsStayPinnedAfterF6kPriceWeight`. It pins 1.2 for the price weight; the household weight, extras constants, rival lanes/units, job-loss k and C9 prices stay pinned as before.
- `F6iPriceAnchorTests`: the +50% retention lower bound goes from 0.25 to 0.2 (band 4 is now 0.232). The native-price comparability band goes from 0.4–2.5 to 0.33–3, because band 4 now spans 0.35 ($16.69) to 2.97 ($9.24), from 0.42–2.48. The neutral 1250/1875 ratio band moves from 2.1–2.4 to 2.5–2.8 (now 2.645). The cheaper-of-two count is 2,925 of 4,096 draws; its band moves from 2,700–3,000 to 2,800–3,050. The sole-shop test is unchanged (reference 2,036, markup 1,114). The markup-above-floor check moves from 5× to 3× the 0.02 floor, because band 4's +50% offer at $16.69 keeps 0.082 (was 0.124). The strength-knob test compares weight 3 with an explicit weight 1 instead of the default.
- `F6jMealExtrasTests.TicketExpenseLowersAShopWeightButNotTheMainAnchor`: the expected ticket factor now includes the price weight in its exponent, as `TicketFactor` does.
- (f) all other F6i main-dish and F6j extras tests pass unchanged.

## Offline validation

- Required foreground build: passed, DLL SHA256 `e76151c7fceaa0b5787a4943ddd9fad4a55f613e7d1c642d3b83a6a8606d8dd6`.
- `dotnet test tests/PersistentCity.Tests.csproj`: 4,042 passed, 1 failed, 6 min 38 s. The failure is `PresentationGridTests.GridHasNoIdentityOrEconomyEffect`, which asserts `ChangeJournalSchema` (33) equals `CurrentSchema` (34). That already fails on the base after F7d's writer bump; F6k touches neither constant. All golden-hash and writer-34 pins pass unchanged.
- An earlier full run also failed `ResearchChargeTests.PostfixCostWithZeroOneAndEightOrdersOnAFullQueue` while it shared the machine with another worktree's test process (10 min run). It passed alone and in the final run.
- F6h/F6i/F6j/F6k classes on their own: 76 passed.
- `git diff --check` and `tools/docs-links/check.py` pass. No game, rig, ModsLocal, bridge, `tools/*-regression` suite or calibration sweep was run.

## In-game checks for the next batched rig

All checks are rig-automated in a named disposable FT city (resolve `rig_test` fresh, prefer the Mac). None is assigned to Peter or a playtest. Retain before → red / after → green logs.

1. **Shipped default loads as 1.2.** Setup: install the F6k build with the shipped `persistent-city.json` into the FT profile; fresh FT city. Steps: attach, read the effective `FoodChoicePriceWeight` from the settings log or a diagnostics export; repeat with the key removed from the file. Expected: 1.2 both times, no clamp message. Before (F6j/F7d build): 1.
2. **Existing development city picks up 1.2 for new decisions only.** Setup: a writer-34 FT city saved on the pre-F6k build with a resident holding an in-flight meal decision. Steps: load on the F6k build, export the held decision's tuning image, then let a new decision form and export its image. Expected: the held image still carries weight 1 and resolves unchanged; the new image carries 1.2; the city loads with no "start a new city" status; save/reload byte-stable.
3. **Explicit user value is kept.** Setup: FT profile config with `"FoodChoicePriceWeight": 1.0`, then 7.5. Steps: attach each time and read the effective value and log. Expected: 1.0 kept; 7.5 clamped to 3 with a clamp log line.
4. **+50% whole-menu markup costs demand at 1.2.** Setup: two matched copies of a native-price FT shop city, one week, autosave off. Steps: run control and a +50% whole-menu arm; export own meals, items and revenue. Expected: fewer own meals in the markup arm than control, and a larger loss than the same arm on the pre-F6k build. This is evidence for later fine-tuning, not a pass gate on profitability.
5. **Unchanged rules.** Setup: same city as check 4. Steps: export ticket lines and household choices. Expected: extras stay in their 75%/125% bands; the funded-home weight behaves as before; no unaffordable meal is bought.
