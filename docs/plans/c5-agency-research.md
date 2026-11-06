# C5 paid market research through marketing agencies

6 November 2026. Design plan, docs only. Base `origin/codex/persistent-city` at `b798b7e` (includes `e9995e5`; writer 25). Every file and line cited below was re-read at this base. Native members were re-read with read-only ILSpy from the installed `BigAmbitions.dll`, SHA-256 `9427561b81aaab2ada09c22a3c98dd9a4017a65225b389513885bf336852e911`, the same hash C3 inspected. No decompiled code is committed.

Peter's decision of 6 November 2026: paid research ships in the first release, in game, as a phone contact. It extends the native marketing agencies and their path to payment. It uses the native agency contact, dialog and billing, not a separate fee path.

## 1. Goal and acceptance

The player calls a marketing agency they already have as a contact. On the agency's campaign screen, next to the native campaign controls, there is one extra button, "Market research". The player picks one of their food businesses in the agency's own business dropdown, picks a research product and confirms a fixed price. The agency adds the price to that business's marketing bill for the day. When the native daily marketing bill is paid at midnight, the report is delivered. The agency sends a phone message, and the player reads the report in the Persistent City panel. If the bill cannot be paid, the order is declined, nothing is charged and no report is delivered.

The report is C4's modelled demand estimate for the residents around the business (or the whole city), as of the minute the order was placed. It never shows resident rows, native sales, footfall or rival finances.

Acceptance:

1. The agency's campaign screen shows the research button only when Persistent City is attached, research is available, the native contract checks passed, and the player has at least one eligible business. Otherwise the native dialog is unchanged.
2. Native campaign signup and cancellation behave exactly as before, with the research button present. The rig `campaign` op still passes.
3. An order is stored with a stable order ID, business, agency, product, exact `Money` price and the logical minute of the order.
4. The survey runs in bounded units while native time is held at the order's epoch. It never changes sale, route, budget or traffic state.
5. The charge is one native `GameManager.ChangeMoneySafe` call per order, made from the native `MarketingHelper.RunDaily` boundary. It uses the native marketing transaction type and category, the business address and the day being billed. It appears in that day's `MarketingExpenses` for the business.
6. Each order is charged at most once, across save, reload, crash and repeated daily boundaries. An exception after native entry leaves the order blocked as payment unknown. It is never retried or refunded.
7. Insufficient funds decline the order with no money change, no native record and no change to the player's campaigns. A charge the binary32 balance cannot subtract exactly is declined the same way, before any native call.
8. A report is readable only through `MarketResearchReport.TryRead` with a paid, delivered, unreplaced grant from the saved ledger.
9. Orders and delivered reports survive save and reload byte for byte. The new schema refuses older development cities with the "start a new city" status.
10. Mod-work p95 stays under 5 ms in matched windows with a survey and a billing boundary. Every spike of 30 ms or more is flagged. Whole-frame timing is reported separately.
11. Rival finances, RLO and native campaign records are never written.

## 2. Current state at the base

What exists:

- C4 engine, inactive. `Core/MarketResearchEngine.cs:107` `MarketResearchEngine` is internal. Its cap is `MaximumConsumers = 150000` (line 109). `Continue` (line 163) examines one bounded unit of consumers from an `IMarketResearchSnapshot` that must be ordinally sorted and unique. A changed identity or count restarts at zero. Tiers Basic, Detailed and In-depth have 13, 62 and 64 cells. In-depth adds only two cells, loyalty and rival share, which are always `null` today. [Contract](../market-research.md).
- C4 access gate. `Core/MarketResearchAccess.cs:33` `IMarketResearchEntitlement`. Line 47 `NoPaymentPath` always denies. Line 60 the report constructor is internal. Line 73 `TryRead` is the only public way to the numbers.
- Settings. `Core/MarketResearchSettings.cs:10` `Available => false`, with no switch. `Runtime/PrototypeSettings.cs:39-43` loads the catchment radius (default 500 m) and unit size (default 256).
- Friend access. `Core/MarketResearchFriends.cs:3-4` grants internals to `PersistentCity.Tests` and `MarketResearchRegression` only.
- C3 placeholder. `Core/AgencyOrders.cs:62` `AgencyOrderBook` is a single-slot, in-memory order book. `Runtime/AgencyPaymentPort.cs:9` always refuses. `Runtime/AgencyBindingDiagnostic.cs` is a read-only dump. [C3 findings](../archive/c3-agency-payment.md), [C3 checks](../checks/c3-agency-payment.md).
- C1 and C2 campaign code. `Runtime/NativeCampaignReader.cs` and `Runtime/NativeCampaignAwareness.cs` read native campaigns for awareness. Research does not use or change them.
- F1 hold. `Core/CitySimulator.cs:180` `IsCaughtUpTo` holds native time while logical work is pending. Lines 216-219 show the precedent: a pending campaign publication holds time without counting as unfinished previous-epoch work. Lines 247-248 run that continuation first in `AdvanceCore`.
- F3 ordinal order. `Core/FoodFinances.cs:25-27` sorts the posting roster by `ResidentId` with `StringComparer.Ordinal` when the derived index is built.
- F7b save epoch. `Persistence/SaveEpoch.cs:44` `PublishedKeys` lists only `persistentcity:state_v1`.
- Schema. `Core/Model.cs:256` `CurrentSchema = 25`. `Persistence/SaveCodec.cs:14` `SaveReadStatus { Missing, Loaded, Invalid, Future }` has no "start a new city" status yet. F5c-lite is adding it (roadmap, "planned (running)").
- Panel and console. `Runtime/FoodEconomyPanel.cs:51` `OnGUI`, line 80 Ctrl+F8. `Runtime/PopulationCensusDiagnostics.cs:43` registers the `pc` console command.

Native members, verified at the hash above:

- `Dialogs.MarketingAgencyDialog.CampaignSettings()` is private and returns the input `DialogEntry` with `OnConfirm = OnMarketingSettingsSet` and `OnCancel = CancelDialog`. Its `OnSecondOption` slot is unused.
- `Entities.DialogEntry` has `OnConfirm`, `OnSecondOption`, `OnCancel`, `SecondOptionTextOverride`, `ConfirmTextOverride` and `CancelTextOverride`. `DialogController.SetCurrentEntryButtons` draws one button per non-null slot. `SecondOptionCurrentEntry` shows the entry that `OnSecondOption` returns.
- `UI.Dialog.MarketingCampaignSettings.selectedBusiness` holds the business the player chose in the native dropdown.
- `Helpers.BusinessHelper.RunDaily` calls `MarketingHelper.RunDaily()` and then `FinancialSummaryHelper.CreateFinancialSummary(Day - 1)`.
- `Helpers.MarketingHelper.RunDaily` charges each player registration once with `TransactionInfo("ba:transaction_marketing", "ba:transactioncategory_marketing", {businessName})`, sets `tax_marketing` when an enabled campaign's agency has `hasTaxDeductiblePurchases`, and calls `ChangeMoneySafe(-GetDailyMarketingExpenses(), info, Day - 1, address)`. On `false` it disables all campaigns of that business.
- `GameManager.ChangeMoneySafe(float, TransactionInfo, int?, Address, bool force, bool showNotification)` returns `false` before any change when money is short and `force` is false. Otherwise it calls `ChangeMoney`, which is not atomic against exceptions.
- `Transaction.transactionData` is a serialized `Dictionary<string,string>` copied from `TransactionInfo.Data`.
- `Localizor.LocalizorManager.RegisterExternalLocalization(modId, directoryPath)` merges external tables. In game mode a missing key renders as the raw key.

What is reusable: the C4 engine and gate unchanged; the C3 member list and the `ChangeMoneySafe` contract; the F1 hold and the F3 continuation pattern; `Money` and `MoneyJson`; the existing `pc` console pattern.

Invariants to keep:

- Native sale authority: research never books, blocks or changes a sale, order, stock or receipt.
- Exact money: prices are `Money`, whole dollars that are exact in binary32; no rounding and no unchecked cast.
- F1 accounting boundary: the survey is logical work that holds native time; it never runs inside a native tick. The charge runs inside native daily settlement and executes no simulation.
- F3 bounded units: no unbounded synchronous scan; every survey unit examines at most the configured consumers.
- F7b save epoch: the ledger lives inside the population payload, so `PublishedKeys` is unchanged and the payload and native money are in the same save.
- No rival finance writes, no native campaign record writes, no direct money edits, RLO unchanged.

## 3. Design

### 3.1 Ordering in the agency call

Decision: add the research entry to the native campaign screen through a Harmony postfix on the private `MarketingAgencyDialog.CampaignSettings()`. The postfix only sets `OnSecondOption` and `SecondOptionTextOverride` on the returned entry. It leaves `OnConfirm`, `OnCancel` and the input template untouched. Reason: C3 found no product extension API. The `OnSecondOption` slot is a native slot that the dialog already draws, and this entry already holds the native business dropdown. A fake `MarketingTypeName` value would break `MarketingTypeSettings.Get` and the daily bill, so it is refused.

The postfix adds the button only if all of these hold: Persistent City is attached and not stopped; the native contract check passed (3.6); `MarketResearchSettings.Available` is true; and at least one player business is an eligible research target. Otherwise the native entry is returned unchanged.

On click, the handler reads `selectedBusiness` from the native input. If it is null it shows the native `common_notification_select_business` error and stays on the screen, as the native confirm does. If the business is not eligible it shows a research message and stays. Otherwise it walks a small menu built from the product table (3.3). Each entry has at most two choices (confirm and second option) plus cancel, so the table is a binary tree of text entries. The last entry states product, business, price, "billed with tonight's marketing bill" and "report tomorrow morning". Confirm places the order. Second option goes back. Cancel hangs up through the native `CancelDialog`. After an order the call continues with the native "more help" entry, reached through the patched type's private `OnMoreHelpOffered`.

Decision: dialog text uses the mod's own localization keys, registered with `LocalizorManager.RegisterExternalLocalization("persistentcity", <mod>/Localization)`, unless C5e's DLL check shows the native mod loader already registers mod folders. C5e pins the table file name and format from `LoadLocalizationTables`. English is the only shipped language.

Eligibility, technical default: the business is rented by the player and is a food scope that Persistent City owns (section 9, Q4). At most one open order per business, and at most 8 open orders per city.

### 3.2 Order ledger (Core)

Decision: replace C3's single-slot `AgencyOrderBook` with a per-city `ResearchLedger` in Core. Reason: C3's book is in memory, single slot and blocks forever after one payment; research needs many orders with saved state.

Each order holds: order ID `pcr-<sequence>` (sequence saved, never reused), business address, agency address, product code, price `Money`, tax-deductible flag read from the agency's `SpecialService.hasTaxDeductiblePurchases` at order time, order minute, state, and at most one sealed report.

States and the only allowed moves:

- `Ordered` → `Surveyed` when the survey finishes.
- `Surveyed` → `Paid` on matching native evidence (3.5). `Paid` means paid and delivered.
- `Surveyed` → `Declined` when `ChangeMoneySafe` returns false.
- `Surveyed` → `Cancelled` when the business is no longer eligible at billing.
- `Surveyed` → `PaymentUnknown` when native code throws after the entry marker, or the evidence does not match.
- `Paid` → `Replaced` when retention drops it (section 9, Q6).

`PaymentUnknown`, `Declined`, `Cancelled` and `Replaced` are terminal. Nothing retries, refunds or resets them. An unknown order blocks only itself; every order has its own key, so a new order cannot double charge an old one.

The ledger implements `IMarketResearchEntitlement`. `TryGet` returns a grant with `Paid = Delivered = true` only for a `Paid` order, `Replaced` for a replaced one, and nothing otherwise. The report ID is the C4 query key, so `TryRead` checks the exact pairing.

### 3.3 Products

Decision: a fixed product table in Core, keyed by a stable product code. Each row has tier (C4 Basic, Detailed or In-depth), coverage (catchment of the chosen business or the whole city), radius (the configured catchment, default 500 m) and price in whole dollars. Section 9 proposes the rows. Codes are saved; changing a row's meaning means a new code. Prices are not settings.

### 3.4 Survey (Core)

Decision: survey at the order's epoch, not at midnight. Reason: midnight already carries postings, closures and billing. Freezing at the order also gives the "as of when you asked" meaning, and the hold is short.

When an order is stored, it records `SurveyAtMinute`: the accounting target `AccountingClock.Target(now)` at the moment of the order. The survey describes the city after every event through that minute has run, and before any later event.

Decision: the survey starts only when the simulator has drained through `SurveyAtMinute`: no pending route job (`PlanningPending` false, `CitySimulator.cs:316`), no pending campaign publication, and no due citizen, food or traffic event at or before that minute. Reason: an event already in `ContinuePlanning` (driven before `AdvanceFrameSlice` at `Runtime/PersistentCityMod.cs:407`, committed at `CitySimulator.cs:369`) can change inputs such as `DayPlaceId` without changing the minute or roster count. A save drops that transient job, and reload replays the event from the start. Waiting for the drain point gives both paths the same input: uninterrupted, the job commits and then the survey runs; after reload, the replayed event commits and then the survey runs. Giving research priority over `ContinuePlanning` was rejected because it would need the uncommitted event to survive reload.

Until the survey finishes, `NoEventsThrough` reports pending research work, as the campaign publication does at `CitySimulator.cs:216-219`: it holds native time but is not previous-epoch work. In the `AdvanceCore` loop (next to lines 247-248), a survey unit runs only when the next event is later than `SurveyAtMinute` and no route job is pending, and it runs before that later event. While the survey runs no Persistent City event runs, and native writes cannot change the fields it reads.

`CityResearchSnapshot : IMarketResearchSnapshot` reads the food roster (`FoodCommerceState.Residents`) through an ordinal index. Each row maps to C4's `MarketResearchConsumer`: resident ID; represented when `ResidentMeal.Slot > 0` (`Core/FoodCommerce.cs:349`); home and day-place positions from `CitizenState.HomeId`/`DayPlaceId` and `Places`; day activity; A2 trait bands; three meal bits for the logical day; individual home-food funding for the day; discretionary budget in cents. A missing input stays `null`, as C4 requires. The identity is the population key, logical day, `SurveyAtMinute` standing in for all three revisions (the hold keeps them fixed), and the order ID, so two orders in the same minute never share an identity. The adapter also checks the minute and roster count before and after each unit. If either changed, the engine restarts, as C4 already does.

Decision: the ordinal index is built in bounded units at the start of the hold (bottom-up merge sort over an `int[]`, at most 4,096 element moves per unit), not with one synchronous sort. Reason: at 100,000 consumers a single sort would be a nonpreemptible spike. C5b measures both and keeps the bounded version unless the single sort's p95 stays under 1 ms at 100,000.

At completion the engine's report is sealed in the order. The survey is transient: a save during the hold writes the order as `Ordered` with its `SurveyAtMinute`. Reload replays the events through that minute, then restarts the survey from zero on the same state. No cursor is saved. This is C4's restart rule.

### 3.5 Charge through the native agency bill (Runtime)

Decision: a Harmony postfix on `Helpers.MarketingHelper.RunDaily` makes one separate `ChangeMoneySafe` call per `Surveyed` order. It uses the native type `ba:transaction_marketing`, category `ba:transactioncategory_marketing`, data `{businessName, persistentcity_research: <order ID>}`, `tax_marketing` when the saved flag is set, `dayOfTransaction = Day - 1` and the business address, with `force: false`.

Reasons: it is the agency's own billing moment and the same transaction kind, so the charge is in the business's `MarketingExpenses` for the billed day, because the postfix runs before `CreateFinancialSummary`. A separate call keeps native campaign failure separate: a declined research order never disables the player's campaigns. Folding the price into `GetDailyMarketingExpenses` was rejected for that reason and because that getter also feeds the native UI.

Exactly once:

1. Before charging, scan the native transaction queue for a marketing record whose data holds this order ID. If exactly one exists with amount equal to the negated price, mark `Paid` without charging. If one exists with another amount, or there are several, mark `PaymentUnknown`.
2. Exact-debit preflight. Read the native balance `B` (binary32, exact as a rational value). The exact result `B - price` must be representable in binary32. Native `ChangeMoney` subtracts with correctly rounded binary32 addition, so the debit is then exact; otherwise it is not. The native clamp is inactive for any such balance and price. If the result is not representable, the order is `Declined` with reason `BalancePrecision` and nothing is called. Example: at `B = $100,000,000` (binary32 spacing $8) a $500 debit would remove $496, so it is refused; a $504 or $512 price would pass.
3. Remember the current last queue record by reference, then mark the native entry immediately before the call.
4. `false` → `Declined` with reason `InsufficientFunds`, with no money change by native contract.
5. `true` → look only at the records appended after the remembered one (the queue may drop old records from the front). Exactly one of them must hold this order ID. It must be a marketing record with this address and day, amount exactly the negated price, and native `balance` equal to `floor(B - price)`. Then `Paid`. Zero or several matches, or a field mismatch, → `PaymentUnknown`. Other appended records, for example from a money-change listener, are ignored.
6. An exception after the entry marker → `PaymentUnknown`. Exceptions are contained, so native `RunDaily` continues.

Save-epoch guard: native `ChangeMoney` raises `ba:gameevent_moneychange` before it returns, so a listener could start a save while the order is still `Surveyed`. Synchronous code alone does not prevent that. The whole billing attempt (scan, preflight, charge, evidence, ledger update) therefore runs inside the F7b guarded unit, the same depth counter `saveEpochUnits` that `Runtime/SaveEpochRuntime.cs:26-30` keeps around `Update`. A save callback inside it follows F7b rule 5: it writes the failure markers, publishes no payload and stops Persistent City after the unit returns. So a saved city never holds the debit without the ledger update. C5d adds one small guarded-unit helper to `SaveEpochRuntime.cs` and uses it.

Tagged evidence is required. The order ID in `transactionData` is how reload, a repeated boundary and the scan in step 1 recognise a debit. There is no keyless fallback: if the native contract check or the rig shows that the extra key is not stored, saved and reloaded intact, research charging stays off and the research button is not shown.

If Persistent City is stopped or detached at midnight, the postfix does nothing and nothing is charged. Native `RunDaily` failing before the postfix leaves the order `Surveyed` for the next day's bill.

### 3.6 Native contract check

At attach, Runtime resolves by reflection every member in section 2 with its exact signature, and checks that no foreign Harmony prefix is on `MarketingHelper.RunDaily` or `CampaignSettings`. Any mismatch turns research off for the session, with a status line, and leaves Persistent City running. It never writes a substitute dialog or fee path. This follows the Compatibility rule: validate, refuse, never substitute.

### 3.7 Delivery and reading

On `Paid`, Runtime sends the agency contact a native `TextMessage` with a mod localization key ("Your market research for {businessName} is ready. Open Persistent City (Ctrl+F8) to read it."). On `Declined` it sends a decline message with its own key ("We could not bill the market research for {businessName}, so it was not delivered."). C5f owns both messages. A new IMGUI window, opened from a "Research reports" button in the Ctrl+F8 panel and by a console line, lists orders and their states. For a `Paid` order it calls `TryRead` with the ledger and draws the cells with English labels made from C4's fixed codes, plus C4's `Evidence` text. Nothing is built while the window is closed.

`pcresearch` console lines (a separate command name, so the census file is not touched): `pcresearch status`, `pcresearch orders`, `pcresearch report <id>` (numbers only for `Paid`), `pcresearch window open|close`.

### 3.8 Persistence

The ledger is a new `CityState.Research` section in the population payload: next sequence, open orders and retained reports. Sealed cells are written by the Persistence codec through a new `InternalsVisibleTo("PersistentCity.Persistence")` in `MarketResearchFriends.cs`. Runtime still has no access to cells outside `TryRead`. Section 5 has the bounds.

### 3.9 What is removed

The C3 placeholders `Core/AgencyOrders.cs`, `Runtime/AgencyPaymentPort.cs` and `tests/AgencyOrdersTests.cs`, and the `tools/agency-payment-regression` project that links them, are removed in C5f once the ledger replaces them. `AgencyBindingDiagnostic` stays as a read-only diagnostic.

## 4. Slice breakdown

Each slice writes `docs/checks/c5x-<name>.md` and `docs/perf/c5x-<name>.md`. Local tests for every slice: build, `dotnet test tests/PersistentCity.Tests.csproj`, and the slice's new tests.

### C5a — Core ledger, products and entitlement

- Scope: `ResearchLedger`, order states, product table, `IMarketResearchEntitlement` implementation, bounds. Not attached to `CityState`.
- Owns: new `Core/ResearchLedger.cs`, `Core/ResearchProducts.cs`, `tests/ResearchLedgerTests.cs`.
- Effort: 1 day.
- Tests: every allowed and forbidden move; one charge per order under repeated boundaries; unknown blocks only itself; the declined and cancelled paths; caps of 8 open orders and one per business; `TryRead` succeeds only for `Paid` and fails after `Replaced`; exact `Money` for every product price and its binary32 value.
- Perf: order and grant calls p50/p95/max and allocation.
- Schema: none.
- Ledger fields include `SurveyAtMinute` and the decline reason (`InsufficientFunds`, `BalancePrecision`).
- Dependencies: none. Can run in parallel with C5d.

### C5b — Survey runner and time hold

- Scope: `CityResearchSnapshot`, bounded ordinal index, survey scheduling inside the F1 hold.
- Owns: new `Core/ResearchSurvey.cs`, `tests/ResearchSurveyTests.cs`. In `Core/CitySimulator.cs`: a new `AttachResearch(ResearchLedger)` seam, the `NoEventsThrough` clause and the `AdvanceCore` survey unit next to the campaign publication ones. `ContinuePlanning` is not changed. C5b does not touch `Core/Model.cs` or the codec; the ledger reaches the simulator only through the seam.
- Effort: 1.5 days.
- Tests: the hold keeps `IsCaughtUpTo` false until completion, then events run in their old order; a cadence change (unit size 16, 256, 1,024) gives the same cells; reload in the middle restarts and gives the same cells (the reload test saves `CityState` with the writer-25 codec and passes an in-memory clone of the ledger through the seam, until C5c lands); an order placed while an employment route probe is pending (`PlanningPending`) gives the same cells uninterrupted and after a save and reload before the probe commits, and the survey sees the probe's committed `DayPlaceId`; two orders in the same minute get different identities; missing inputs stay unknown; roster change mid-survey restarts; a city with no food roster refuses the order; no sale, receipt, budget, route or traffic field changes (state hash before and after).
- Perf: index units and survey units at 1,914, 10,000 and 100,000 consumers; total hold frames at 60 FPS budgets; single sort versus bounded sort.
- Schema: none.
- Dependencies: C5a. Runs in parallel with C5c and C5d through the `AttachResearch` seam. C5e attaches the saved `CityState.Research` ledger through the same seam.

### C5c — Save schema

- Scope: `CityState.Research`, codec, bounds, refusal of older development cities.
- Owns: `Core/Model.cs` (one property and `CurrentSchema`), `Core/MarketResearchFriends.cs` (one line), new `Persistence/ResearchWire<N>.cs`, the research section in `Persistence/SaveCodec.cs`, new `tests/ResearchSaveTests.cs`, the new writer golden hash and reader-corpus files.
- Effort: 1.5 days.
- Tests: round trip byte for byte with open, surveyed, paid, unknown and replaced orders; strict refusal of unknown product codes, unknown states, cells over 64, out-of-range counts, duplicate or reused sequence, non-canonical money; the older development schema returns the "start a new city" status and the payload is kept unchanged; golden hash pinned.
- Perf: save and load cost of the section at the maximum bounds.
- Schema: one bump (section 5).
- Dependencies: C5a; F5c-lite merged first for the refusal status and writer 26. Runs in parallel with C5b and C5d.

### C5d — Native billing

- Scope: `RunDaily` postfix, `ChangeMoneySafe` charge, evidence check, queue scan, native contract check for billing members.
- Owns: new `Runtime/ResearchBilling.cs`, new `Core/ResearchCharge.cs` (pure `IResearchChargePort`, the exact-debit preflight and the evidence rules), `tests/ResearchChargeTests.cs`, and one guarded-unit helper in `Runtime/SaveEpochRuntime.cs`.
- Effort: 1.5 days.
- Tests (pure port doubles): success; insufficient funds; exception before and after entry; appended-record mismatch in amount, address, day, balance or ID; zero and two matching records; an unrelated record appended by a money-change listener still gives `Paid`; queue front dropped during the call; an existing record found by the scan; repeated boundary; stopped runtime does nothing; campaigns are never touched; tax flag carried.
- Preflight tests: balances $0, $16,777,216 and $100,000,000 with a $500 price (the last refused as `BalancePrecision`, with no native call) and with $512 (charged exactly); a sweep of every product price against balances at each binary32 exponent.
- Save-guard tests: a save invoked from inside the charge port writes the failure markers and publishes no payload, so the ledger and the debit are never saved apart; a port that reports the tagged key as unsupported keeps charging off.
- Perf: postfix cost with 0, 1 and 8 surveyed orders and a full 1,000-record queue.
- Schema: none.
- Dependencies: C5a. Runs in parallel with C5b and C5c.

### C5e — Agency dialog and ordering

- Scope: `CampaignSettings` postfix, the product menu, order placement, localization table and registration, dialog contract check. The button is still hidden because `Available` stays false.
- Owns: new `Runtime/ResearchAgencyDialog.cs`, new `Runtime/Localization/` table, new `tests/ResearchMenuTests.cs` (menu tree built from the product table, pure), and the attach and install lines in `Runtime/PersistentCityMod.cs`, including attaching the saved `CityState.Research` ledger through C5b's `AttachResearch` seam.
- Effort: 1.5 days.
- Tests: menu tree for 1 to 6 products; null business and ineligible business stay on the screen; the native entry is unchanged whenever a precondition fails (`OnConfirm`, `OnCancel` and template identical).
- Perf: postfix and menu build cost per call.
- Schema: none.
- Dependencies: C5a, C5b, C5c, C5d.

### C5f — Delivery, report window, activation and cleanup

- Scope: delivery message and decline message (localization keys and agency `TextMessage` for `InsufficientFunds` and `BalancePrecision`), report window, `pcresearch` console lines, `MarketResearchSettings.Available` derived from the attach and contract checks, removal of the C3 placeholders.
- Owns: new `Runtime/ResearchReportWindow.cs`, new `Runtime/ResearchDiagnostics.cs`, `Core/MarketResearchSettings.cs`, one button line in `Runtime/FoodEconomyPanel.cs`, deletion of `Core/AgencyOrders.cs`, `Runtime/AgencyPaymentPort.cs`, `tests/AgencyOrdersTests.cs`, `tools/agency-payment-regression/`.
- Effort: 1 day.
- Tests: delivery message only for `Paid` and decline message only for `Declined`, once per order; window and console show numbers only for `Paid`; closed window builds nothing; `Available` is false when any check fails.
- Perf: window draw cost when open; zero work when closed.
- Schema: none.
- Dependencies: C5e. The `FoodEconomyPanel.cs` line can collide with F6 panel slices; rebase and keep both.

Parallel plan: C5a first. Then C5b, C5c and C5d in parallel; their files do not overlap, and only C5b touches `CitySimulator.cs`. Then C5e, then C5f.

## 5. Save schema impact

One bump, in C5c only. The writer becomes the next number after the writer at C5c's merge: 27 if F5c-lite's 26 is merged first. No migration and no switch-over day. A city written by an older development schema gets the "start a new city" status and its payload is kept. Future, unknown, malformed and over-bound data are refused without overwriting.

Section `Research` in `persistentcity:state_v1`:

- `NextSequence`: integer, at least 1, never decreasing.
- `Orders`: at most 8 non-terminal orders plus at most 16 retained terminal or paid orders (section 9, Q6).
- Each order: ID, business address, agency address, product code, price (`MoneyJson` canonical units), tax flag, order minute, `SurveyAtMinute`, state, decline reason, and for `Surveyed` or `Paid` the sealed report: query key, tier, logical day, scope address, radius and up to 64 cells (fixed metric and band codes, value or null).

Maximum size is about 24 orders times 64 cells, well under 20 KB. `SaveEpochStore.PublishedKeys` is unchanged. The new writer gets a pinned golden hash and a reader-corpus entry.

## 6. Performance

Gate: mod-work p95 under 5 ms; flag every mod-work spike of 30 ms or more; whole-frame timing reported separately.

Expected cost:

- Idle (no open order): zero. No per-frame check beyond one boolean in `NoEventsThrough`.
- Dialog: one postfix per agency call, O(player businesses) for eligibility. Expected well under 1 ms, once per call.
- Order: O(1).
- Survey: C4 measured 256-consumer units at p95 0.03-0.07 ms and 50,000-consumer completion at p95 8.6-9.5 ms spread over units ([C4 perf](../perf/c4-market-research.md)). At the 60 FPS allowance of 16 units per frame this is about 0.6 ms per frame, for about 13 frames at 50,000 consumers. The bounded index adds about the same number of units. The hold is visible as F1's "calendar held" status for under one second.
- Billing: once per game day, O(surveyed orders × queue scan of about 1,000 records), expected under 1 ms.
- Window: only while open.
- Save: the research section at its bounds, expected well under 1 ms.

Each slice's perf file records its own offline measurement (section 4). The batched rig measures the combined windows in section 7.

## 7. In-game checks for the batched rig

Run on a named disposable FT city at the development head with a native marketing agency contact and one PC-owned food business. Use the agency's opening hours with the player outside the agency, as the `campaign` op requires.

1. **Contract check and hidden button.** Setup: research build, fixture loaded. Steps: run `pcresearch status`; open the agency call through the `campaign` handshake up to the campaign screen and read the entry's button slots. Expected: contract check passed; with `Available` false (C5e build) no second option; with C5f the second option is present. Rig-automated (pending capability: R1 `research` op, `inspect` action).
2. **Native campaigns unchanged.** Setup: same fixture, C5f build. Steps: run the existing `campaign` buy and stop sequence and the rig's `flows/campaign/active-campaign.json` flow. Expected: same receipts as without research; `OnConfirm` target verified; no research order created. Rig-automated (existing capability: `campaign` op and flow).
3. **Order and survey hold.** Steps: order the Basic catchment product for the fixture business; read `pcresearch orders` and the accounting status until the survey ends. Expected: order `Ordered`, then `Surveyed`; native time held only during the survey; no change in money, campaigns or native transactions. Rig-automated (pending capability: R1 `order` action; existing capability: `call: console` for `pcresearch` and accounting lines).
4. **Charge at midnight.** Steps: advance through the next midnight; read money, native marketing transactions with their data, the day's `MarketingExpenses` and `pcresearch orders`. Expected: one extra marketing record for the business on the billed day with amount equal to the negated price and the order ID in its data; `MarketingExpenses` includes it; order `Paid`; campaigns unchanged. Rig-automated (existing capability: `call: time` advance, `ledger` step; pending capability: R2 transaction data in `ledger`).
5. **Delivery and reading.** Steps: read the agency contact's last message; open the window with `pcresearch window open`; capture a screenshot; run `pcresearch report <id>`. Expected: delivery message shown with readable text (not a raw key); window shows the cells and evidence text; numbers match the console. Rig-automated (existing capability: `capture` op and `call: console`; pending capability: R3 contact message read).
6. **Insufficient funds.** Setup: set the disposable money below the price after the survey. Steps: advance through midnight. Expected: order `Declined`; no research record; money changes only by native bills; native campaigns of that business not disabled by research. Rig-automated (existing capability: bridge money set as in the `campaign` zero-money step, `call: time`, `ledger`).
7. **Save and reload.** Steps: save while `Ordered`, reload, finish; separately save after `Paid`, reload. Expected: the survey restarts and gives the same cells; after `Paid`, no second charge at the next midnight and the report reads the same; `pcsave` compare of the research section equal. Rig-automated (existing capability: `save` trigger, `call: load`, RIG-PCSAVE extraction and compare).
8. **Older development city.** Steps: load a writer-25 (or writer-26) FT city on the C5c build. Expected: "start a new city" status; payload unchanged in `pcsave`; native play continues. Rig-automated (existing capability: fixture load and RIG-PCSAVE).
9. **Native display of the record.** Steps: open EconoView and BizMan after check 4; capture. Expected: the research charge displays as a normal marketing expense; no error in the game log. Rig-automated (existing capability: `panel` op and `capture` op).
10. **Performance.** Setup: autosave recorded, turned off and checked before every window and after reload. Steps: matched idle, order-plus-survey and midnight-billing windows, with the window closed and open. Expected: mod-work p95 under 5 ms; every spike of 30 ms or more flagged with condition and component; whole-frame timing reported separately; idle research work zero; autosave restored at cleanup. Rig-automated (existing capability: RIG-PERF-UNION clean windows; pending capability: R1 `order` action for the order window).

Rig capability requests for PeterHartwieg/big-ambitions-agent-rig:

- **R1 `research` op.** `list` (lease-free: products, eligible businesses, `pcresearch` state), `inspect` (open the agency call to the campaign screen and report the entry's button slots without clicking), `order` (select the business in the native dropdown, click the second option, walk the menu by product code, confirm, then read back). Same gates as `campaign`: owner, token, session, FT prefix, agency hours, player outside, bounded handshake, no replay after a timeout.
- **R2 `ledger` transaction data.** Include `transactionData` keys and values for marketing records, filtered to `businessName`, `taxDeductibleName` and `persistentcity_research`.
- **R3 contact message read.** Read the last N messages of one contact by agency address, returning localization keys and rendered text.

## 8. Dependencies

- F5c-lite (running): adds the "start a new city" status and writer 26. C5c merges after it and takes the next writer number.
- F1, F3, F7b (merged): C5b reuses the F1 hold and the F3 continuation pattern; C5c keeps the ledger inside the F7b-published payload.
- F7c, F7d (planned): the snapshot and wire split must carry the `Research` section. C5c's tests give them the shape.
- F6 panel slices (planned): C5f adds one line to `FoodEconomyPanel.cs`.
- C1, C2b: no change. Research is not a campaign and has no awareness effect.
- Agent rig: R1, R2, R3 above. Checks 2, 4 (without data), 6, 7, 8, 9 run with existing capabilities.

## 9. Game-design questions for Peter

Q1. Which research products ship, and at what price?
Recommended default:

| Code | Product | Coverage | C4 tier | Price |
| --- | --- | --- | --- | ---: |
| `basic-local` | Neighbourhood snapshot: residents, meal opportunities, household food, budget bands | 500 m around the business | Basic (13 cells) | $500 |
| `detailed-local` | Customer profile: snapshot plus taste and lifestyle bands and work/study groups | 500 m around the business | Detailed (62 cells) | $2,000 |
| `detailed-city` | City profile: the same for the whole city | Whole city | Detailed (62 cells) | $6,000 |

Prices are one-off and billed with that night's marketing bill. Compare them with the native campaign prices that `campaign list` reports before choosing. Above about $16.8 million the native balance cannot subtract every whole-dollar price exactly; such a charge is declined (3.5). Prices that are multiples of a larger power of two (for example $512) stay chargeable at higher balances.

Q2. Should the In-depth tier be sold now? Its two extra cells (loyalty, rival share) are always "unknown" today.
Recommended default: no. Hide In-depth until both cells have real inputs.

Q3. When is the report delivered?
Recommended default: the next morning, after the night's bill is paid. The other option is immediate delivery with billing at night, which gives a free report when the bill later fails.

Q4. Which businesses can order research?
Recommended default: only player food businesses that Persistent City owns, because the report describes food demand.

Q5. What happens when the bill cannot be paid?
Recommended default: the order is declined, nothing is charged, no report, and the agency sends a message. The player's campaigns are not affected by the research order.

Q6. How many delivered reports does the player keep?
Recommended default: the last 16 per city; the oldest is dropped when a new one is delivered.

Q7. How many orders can wait for the night's bill?
Recommended default: one per business and at most 8 per city.

## 10. Risks and refusals

Risks:

- The extra `transactionData` key may affect native display or the save. Check 9 and check 7 test it. There is no keyless fallback: if the key does not survive, research charging stays off (3.5).
- Mod localization may not load from the mod folder. Check 5 tests it. Fallback: register the folder explicitly with `RegisterExternalLocalization`.
- Another mod may patch `RunDaily` or `CampaignSettings`. The contract check turns research off when it sees a foreign prefix.
- A game update may change the private dialog methods. The contract check turns research off; Persistent City keeps running.
- The survey hold is visible as "calendar held". At 100,000 consumers it is about one second at 60 FPS. C5b measures it.
- Binary32 balance changes are inexact at very large balances. The exact-debit preflight declines such charges before the native call, and the evidence checks the record amount and balance.
- The rig's `campaign` op verifies the `OnConfirm` target; the added second option must not change its checks. Check 2 tests it.

Refusals:

- No fabricated `MarketingTypeName`, no native campaign record written by research.
- No direct cash edit, no refund, no retry of an unknown payment, no `force: true` debit.
- No report numbers before `Paid`, in logs, bridge replies or diagnostics.
- No research when Persistent City is stopped, detached or failed its contract check.
- No rival finance writes, no RLO changes, no native sale or receipt changes.
- No migration of older development cities.
