# Native price precision and accounting migration

Decision: Peter selected Q3 A on 28 October 2026. Resident payments and receipts must retain native price precision; changing native prices or loosening the financial gate is not authorized.

## Read-only installed-assembly evidence

Inspected `BigAmbitions.dll` in the Steam installation?s `Big Ambitions_Data/Managed` directory using the existing local ILSpy CLI. Assembly SHA-256: `9427561b81aaab2ada09c22a3c98dd9a4017a65225b389513885bf336852e911`. `RetailPrice.price` and `Entities.OrderEntry.price` are `System.Single` (IEEE 754 binary32), in dollars. `ItemHelper.GetPrice` returns the matching retail float directly; `RetailBusinessSimulator` assigns it to the order and asks the citizen to approve it. That path has no cent rounding. Native statement values exposed by our adapter are also floats.

Only source findings and the assembly hash are recorded here. Extracted inspection text stays in ignored `build/pc-decisions/`; no game assembly, decompiled implementation or raw save enters Git. The game was not launched and its files were not changed.

## Unit and rounding

Fields named `*Cents` continue to mean cents, now using `decimal` for prices, allowances, held/spent funds, wallet ledgers and revenue. Fixed policy income, housing, essentials and preference coefficients remain integer cents. The ledger unit is **10^-12 cents (10^-14 dollars)**. `FoodMoney.FromNative` decodes the binary32 significand/exponent, scales dollars to cents, and rounds to twelve fractional cent digits using nearest/ties-to-even. Conversion refuses nonfinite/nonpositive/out-of-range values, subnormal values and any result that cannot round-trip to the identical native float. The supported maximum remains $10,000. No decimal string/display price is substituted for the native float.

For `$10.2528f`, the charge is `1025.279998779297` cents; 109 such charges total `$1117.55519866943373`, instead of the previous `$1117.25`. Rounding error in a supported per-sale conversion is at most 5?10^-15 dollars, below native representable precision. Wallet/receipt/history arithmetic uses the same amount; confirmation and refusal do not round again. The management panel retains its existing whole-dollar display formatting; formatting never changes settlement amounts. Scores may convert prices to double for utility comparisons; scores never fund or settle a sale.

Native sums still use native float arithmetic, so this is not a promise that decimal summation reproduces every aggregate float rounding bit. The existing acceptance tolerance is unchanged. A native campaign must verify stock, bags, receipts, wallets, history, accounts and cash together. The historical 30.52-cent failure is not rewritten as a pass.

A hold checks that both subtraction and subsequent spent-ledger addition are representable before touching funds. Precision exhaustion is refused, not silently rounded. Save validation retains the historical signed-long upper bound on accumulated money and checks the fractional unit, alongside every existing conservation, identity, journey and ownership invariant.

## Checkout and receipts

The decision cache remains unchanged. Checkout reads live offers, compares the full decimal price, checks it again after native setup and before creating a receipt, and requires exact native float equality in the price-acceptance hook and post-sale order evidence. A one-ULP price change, even one far below a cent, cannot use stale approval. Native prices, orders and statement arithmetic remain native-owned.

New markers use `persistentcity:food-sale-v2:` with canonical invariant decimal cents in the existing pending/paid identity payload. The reader recognizes unchanged v1 integer-cent markers for migrated intentions. Pending/unknown/mismatching evidence still blocks retry; simultaneous v1/v2 markers conflict and survive pruning. Unresolved old markers remain protected. There is no retroactive price upgrade or historical debit.

## Save compatibility

City schema **14** accepts strictly shaped schema **13** as well as current data. Migration widens historical integer monetary values exactly, sets the in-memory schema to 14 and otherwise retains identities, balances, old prices, meal/traffic phases, holds and histories. It neither writes the source payload during reading nor buys anything. Normal successful saves publish schema 14 only after full validation. Invalid, foreign, future and pre-13 experimental payloads remain preserved/refused; those older experimental formats were already unsupported. No identity regeneration or new baseline is required for valid schema-13 cities.

The numeric unit has not changed, so multiplying old values by a scale would be wrong. Legacy prices retain their agreed cents: unexecuted sales with a now-different live fractional price are refused/refunded by the existing flow, while paid/uncertain evidence is reconciled at its original amount. Old recorded revenue discrepancies remain historical evidence.

Detached food-shadow documents move from schema 4 to **5**, because their shared offer/choice DTOs also retain fractional prices. Existing supported shadow schemas 1?4 migrate their integer amounts and retain prior behavior; the shadow remains hypothetical. Strict raw shape checking precedes typed decimal deserialization. Passing decimal amounts through an intermediate JSON double is specifically avoided and regression-tested. Current whole decimal numbers may serialize with `.0`; this accounting-format change is the authorized Q3 exception to Q6?s exact-byte performance rule. No save-performance shortcut was added.

## Evidence boundaries

Offline tests cover the 109-sale regression, exact wallet/receipt conservation, native-float round trips, refusal of unsupported prices, interrupted paid/uncertain reload, v1 receipt compatibility and conflicts, schema-13 interrupted phases and invalid-payload preservation. Actual adapter harnesses cover fractional checkout, one-ULP changes, late price mutation, stock and idempotent replay against native doubles. These are source/offline tests, not native financial gate clearance. A separately authorized game run remains required.
