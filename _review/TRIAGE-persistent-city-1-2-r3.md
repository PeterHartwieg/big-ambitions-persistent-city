# Independent review round 3 triage
HIGH_MEDIUM_FIX: 2

| ID | Severity | PR | file:line | Finding | Raised by (sol/astra/opus) | Decision (FIX / SKIP / QUESTION) | Reason |
| --- | --- | --- | --- | --- | --- | --- | --- |
| R3-01 | Medium | #2 | Runtime/NativeFoodMarket.cs:313-318; Runtime/FoodBusinessCatalogue.cs:70,76,97,110 | Other known pre-sale catalogue refusals strand held funds. | opus | FIX | The catalogue explicitly rejects ambiguous/invalid observed data before sale. Broaden the dedicated refusal type to those checks, not arbitrary exceptions or post-sale outcomes. Test refunds and unchanged native state. |
| R3-02 | Medium | #2 | tools/food-checkout-regression/Program.cs:428; Runtime/NativeFoodMarket.cs:250 | Address replacement leaves aliases that enumerate one controller twice; narrowed regression hid full-read failure. | opus | FIX | Restore the full-read assertion and deduplicate input controller references. This avoids deleting persistent scopes or changing ownership while retaining rejection of distinct controllers with conflicting addresses. Native address-change reachability is unverified, but the supported regression demonstrably fails. |

Sol and Astra approve both PRs with no findings. Opus approves #1 and approves #2 with nits, raising the two Medium findings above. No product/design questions; no #1-only changes. PR #3 remains excluded.

## Completion

Both FIX items are implemented in `9ee38e9` ? Fix review round 3: refund catalogue refusals and deduplicate observations. Pushed normally to #2. No #1-only fix or merge was necessary. No new product/design questions.

Both failures reproduced before fixes. Validation: 329 checkout checks, 29 catalogue checks, 1,304 unit tests; Steam-DLL warnings-as-errors build and git diff --check pass. Logs are ignored under build/review-r3/. The refund checks use HouseholdFinance directly; production FoodCommerce's unpaid-result refund wiring was inspected, not replaced. No game/native financial clearance.
