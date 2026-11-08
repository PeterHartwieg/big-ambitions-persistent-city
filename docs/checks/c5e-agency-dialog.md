Status: implemented (offline); not verified by rig

# C5e: agency dialog and ordering (8 November 2026)

Plan: [C5 section 4, row C5e](../plans/c5-agency-research.md#c5e--agency-dialog-and-ordering); design in sections 3.1 and 3.6; defaults in [design defaults](../decisions/2026-11-06-design-defaults.md). Base `3f734cb` (F6i #150, writer 33). Perf: [docs/perf/c5e-agency-dialog.md](../perf/c5e-agency-dialog.md).

## Scope

- **Implemented:**
  - `Runtime/ResearchAgencyDialog.cs`: the postfix on private native `MarketingAgencyDialog.CampaignSettings()`, the click handler, the menu walk, order placement through the C5b runner, and the dialog contract check.
  - `Runtime/ResearchAgencyMenu.cs`: the pure gate, click rule, menu tree and localization keys. It is linked into the test project.
  - `Runtime/Localization/en.json`, the English table.
  - Wiring in `Runtime/PersistentCityMod.cs`: `ResearchBilling.Install` and `ResearchAgencyDialog.Install` at initialization. At attach, the saved `CityState.Research` goes through C5b's `AttachResearch`, `ResearchBilling.Attach` and the dialog desk. Detach and uninstall at detach and shutdown.
  - `tests/ResearchMenuTests.cs` (16 cases).
- **Small edits outside the owned files, each needed for the wiring:**
  - one `Compile Include` in `tests/PersistentCity.Tests.csproj`;
  - a copy of `Runtime/Localization/*.json` to `<package>/Locales/` in `tools/build.py`;
  - the C5d pin in `tests/ResearchChargeTests.cs`. It said "no caller references `ResearchBilling`" and now says "only `PersistentCityMod.cs` attaches it".
- **Unchanged:**
  - `MarketResearchSettings.Available` stays false, so the button is never added. No order can be placed in this build.
  - No schema change: writer 33 and its golden hash are unchanged, and no Core or Persistence file was touched.
  - No delivery or decline message, report window, `pcresearch` console, activation or C3 removal. Those are C5f.

## What the code does

1. **Postfix.** The patched method is `MarketingAgencyDialog.CampaignSettings()`. `ResearchDialogGate.TryExtend` reads the preconditions cheapest first. The button is added only when all of these hold:
   - Persistent City is attached to the current save and not failed or stopped;
   - the dialog contract and the C5d billing contract passed;
   - `Available` is true;
   - the entry still has the native shape: Input template, `MarketingCampaignSettings`, and both `OnConfirm` and `OnCancel` set;
   - the second-option slot and its text are unused;
   - the mod key `persistentcity_research_button` resolves;
   - no foreign Harmony prefix, transpiler or finalizer is on `CampaignSettings`;
   - at least one registration is eligible.

   Then it sets only `OnSecondOption` and `SecondOptionTextOverride`. If any check fails, or an exception is contained, the native entry is returned as native built it.
2. **Eligibility** (plan Q4): the business is rented by the player, has a business name, and its `NativeFoodMarket.PlaceId` is a Shop-channel food scope with `ObservedOwnShop`. The billing port (C5d) uses the same check.
3. **Click.** The handler reads native `MarketingCampaignSettings.selectedBusiness`.
   - If no business is selected, it shows native `Notifications.ShowError("common_notification_select_business")` and returns null.
   - If the business is not eligible, it shows the research error and returns null.

   Native `DialogController.SecondOptionCurrentEntry` shows nothing new for null, so the campaign screen stays, as native `OnMarketingSettingsSet` does.
4. **Menu.** `ResearchMenu.Build(ResearchProducts.All)` is a binary tree of text entries. Each entry has a confirm button, a second option, and native `CancelDialog` as cancel.
   - Product entry: confirm chooses the product. The second option offers the next product; on the last product it goes back to the native campaign screen, through the patched method.
   - Confirmation entry: it states the product, the business, the exact price ("$500", "$2,000", "$6,000"), "billed with tonight's marketing bill" and "the report arrives tomorrow morning". Confirm places the order; the second option goes back.
5. **Order.** `ResearchSurveyRunner.TryOrder` receives:
   - the product code;
   - the business place ID (the C5d key);
   - the agency ID `address:<street>:<number>:agency`;
   - the agency's `SpecialService.hasTaxDeductiblePurchases`;
   - the configured catchment and the native minute.

   On a refusal, the handler shows a research error (open order for the business, city limit, or unavailable) and stays on the entry. After an order, a text entry confirms it, and its `OnVisible` shows native `OnMoreHelpOffered()` (private, called by reflection), as the native campaign change does.
6. **Contract check (3.6).** Reflection resolves every native member the dialog uses, with exact types:
   - `CampaignSettings()` and `OnMoreHelpOffered()` returning `DialogEntry`;
   - the `DialogEntry` fields and the `TemplateType` and `InputTemplateName` enum names;
   - `MarketingCampaignSettings.selectedBusiness`;
   - `DialogController.current`, `contact`, `CancelDialog`, `SecondOptionCurrentEntry` and `GetInputTransformSafe<T>`;
   - `Contact.streetName`, `streetNumber` and `Address`;
   - `BuildingHelper.GetBuilding(Address)`, `Building.SpecialService` and `SpecialService.hasTaxDeductiblePurchases`;
   - `Notifications.ShowError(string, string, bool)`;
   - `LocalizorManager.Localize` and `IsLocalizedKey`;
   - the foreign-patch check.

   On a mismatch, the postfix is not installed and the log gets the status line "Research dialog off: …". Persistent City keeps running, and no substitute dialog is written. The foreign-patch check runs again on every screen that passed the cheaper checks.

## Native contract, re-verified

Read with read-only ILSpy on 8 November 2026. `BigAmbitions.dll` SHA-256 is `9427561b81aaab2ada09c22a3c98dd9a4017a65225b389513885bf336852e911`, the plan's hash. `HGPlugins.dll` and `BigAmbitions.ModsInternal.dll` are from the same install. No decompiled code is committed.

- `CampaignSettings()` is private and returns a new Input entry with `OnConfirm = OnMarketingSettingsSet` and `OnCancel = CancelDialog`. It leaves `OnSecondOption` null. The plan's text was confirmed.
- `DialogController.SetCurrentEntryButtons` draws one button per non-null `OnCancel`, `OnSecondOption` and `OnConfirm`. `SecondOptionTextOverride` is a key string. `SecondOptionCurrentEntry` shows the returned entry only when it is not null.
- `OnMarketingSettingsSet` with no selected business calls `Notifications.ShowError("common_notification_select_business")` and returns null. After a change, it returns a text entry with `OnVisible = OnMoreHelpOffered().ShowEntry`.
- **Localization.** `LocalizorManager.LoadLocalizationTables` loads `<path>/<locale>.json` for the loaded and fallback locales from every registered external path. A table is one flat JSON object of strings. Keys are lower-cased and trimmed, and external keys overwrite. Locale codes come from `locale.json` (`en` is English).
- **Mod loader.** `ModDiscoveryRegistry.SyncDiscoveredLocalizationPaths` already registers `<ModFolder>/Locales` for every discovered mod, through `SyncExternalLocalizationPaths`. That call also removes every registered path it did not list. A `RegisterExternalLocalization("persistentcity", <mod>/Localization)` call would therefore be dropped at the next discovery refresh.
- **Decision:** ship the table as `<mod>/Locales/en.json` and make no registration call. The source stays in `Runtime/Localization/en.json`; `tools/build.py` copies it.
- **Missing key.** In game mode a missing key renders as the raw key. The postfix requires `IsLocalizedKey("persistentcity_research_button")`, so a table that did not load keeps the screen native.

## Offline checks run (8 November 2026)

| Check | Result |
| --- | --- |
| `python tools/build.py --game-managed "...\Big Ambitions_Data\Managed"` | passed; package has `Locales/en.json` |
| `dotnet test tests/PersistentCity.Tests.csproj` | **3,911 / 3,911 passed** (16 new C5e cases), including the pinned writer-33 golden hash |
| `dotnet test ... --filter ResearchMenuTests` | 16 / 16 passed; perf lines in [docs/perf/c5e-agency-dialog.md](../perf/c5e-agency-dialog.md) |

Cases in `tests/ResearchMenuTests.cs`:

- **Menu tree for 1 to 6 products.** There are 2n entries. Each has exactly two choices with text keys. Every product is reachable from the root, and only confirmation entries place orders. There is one exit to the campaign screen, and confirmation goes back to its product.
- **Confirmation text.** It renders the product, business and exact price, "billed with tonight's marketing bill" and "the report arrives tomorrow morning". Every price string parses back to its whole-dollar value.
- **Null or ineligible business.** A null business shows the native select-business error. An ineligible one shows the research error. Both return null (stay) and build no menu.
- **Gate.** For all 128 precondition combinations, `OnConfirm`, `OnCancel`, the template and the input template are the same objects and values afterwards. Only the all-true combination sets the slot. The first failed precondition is reported. With the real `MarketResearchSettings.Available`, which is false and has no setter, the entry is refused as `Unavailable`. A null entry is never read.
- **Source pins.**
  - `Extend` has exactly the two slot assignments, and the postfix assigns no other native field.
  - The precondition read order is pinned.
  - The dialog uses native `ShowError`, `CancelDialog` and `OnMoreHelpOffered`.
  - The dialog has no campaign, money, message or `RegisterExternalLocalization` code, and places orders through `runner.TryOrder`.
  - `AttachResearch(state.Research, …)` runs after the food market and the save identity. Detach drops the dialog and the bill first, and only `PersistentCityMod.cs` calls the seam.
- **Localization table.** It is a flat object of strings with lower-case, prefixed keys, equal to exactly the set of keys the dialog uses. Every placeholder is an argument the dialog passes. English is the only file, and `build.py` copies it to `Locales/`.
- **Perf.** Per-call cost of the postfix gate (hidden and visible), the menu build (3 and 6 products) and the entry arguments.

## In-game checks for the next batched rig

These run on a named disposable FT city at the development head with the C5e build, a native marketing agency contact and one PC-owned food business. Run them during agency hours with the player outside the agency, as the `campaign` op requires (plan section 7). Autosave is recorded and turned off, as for every rig perf window. They are drawn from plan checks 1, 2 and 10. Checks 3 to 9 need an order, which the C5e build cannot place while `Available` is false, so they stay with C5f and the C5b, C5c and C5d checks files.

1. **Contract check passes and the button stays hidden (plan check 1, C5e half).**
   - Setup: C5e build, fixture loaded.
   - Steps: read the game log for "Research dialog off" and "Research billing off". Open the agency call through the `campaign` handshake up to the campaign screen and read the entry's button slots without clicking.
   - Expected: neither warning in the log. The campaign screen has the native cancel and confirm buttons and no second option (`OnSecondOption` null), because `Available` is false.
   - Rig-automated: existing capability `call: console` and log capture for the warnings; pending capability R1 `research` op, `inspect` action, for the button slots.
2. **Native campaigns unchanged (plan check 2).**
   - Setup: same fixture, C5e build.
   - Steps: run the existing `campaign` buy and stop sequence and the rig's `flows/campaign/active-campaign.json` flow.
   - Expected: the same receipts and campaign records as on the F6i base build. The `OnConfirm` target is still verified as native `OnMarketingSettingsSet`. No research order is created, and the saved `Research` section stays `[1,1,[],[],[]]` in `pcsave`.
   - Rig-automated: existing capability `campaign` op and flow, RIG-PCSAVE extraction.
3. **Localization table loaded from the mod folder.**
   - Setup: C5e build deployed with `Locales/en.json` next to `PersistentCity.dll`.
   - Steps: in the console, evaluate `LocalizorManager.IsLocalizedKey("persistentcity_research_button")` and `"persistentcity_research_button".GetLocalization()`.
   - Expected: true and "Market research". No "Localization for key 'persistentcity_research_…' not found" warning in the log.
   - Rig-automated: existing capability `call: console` and log capture.
4. **Saved ledger attached, idle (plan check 10, idle window).**
   - Setup: C5e build, autosave off and checked before the window.
   - Steps: a matched idle window against the F6i base build, then a window with the agency campaign screen opened and closed five times.
   - Expected: mod-work p95 under 5 ms; no research survey or index unit counted (no open order); every spike of 30 ms or more flagged with condition and component; whole-frame timing reported separately; autosave restored at cleanup.
   - Rig-automated: existing capability RIG-PERF-UNION clean windows and `campaign` handshake; pending capability R1 `research` op `inspect` for the screen openings.
5. **Foreign-prefix refusal keeps the city running.**
   - Setup: disposable FT city, C5e build, plus the rig's test-only Harmony prefix on `MarketingAgencyDialog.CampaignSettings` under another owner, installed before Persistent City.
   - Steps: load the city; read the log and the accounting status.
   - Expected: "Research dialog off: foreign Harmony patch on MarketingAgencyDialog.CampaignSettings by …" in the log. Persistent City is active, and the campaign screen is unchanged.
   - Rig-automated: pending capability, a rig test-only foreign patch fixture, plus existing capability log capture and `call: console`.

C5f adds the visible-button half of plan check 1, plus checks 3 to 10 with an order placed, once `Available` derives from these checks.
