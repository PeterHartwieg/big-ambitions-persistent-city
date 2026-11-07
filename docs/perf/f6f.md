Status: implemented (offline); not verified by rig

# F6f performance

Refs #93. macOS arm64, .NET 10.0.11, Debug for the operation measurements below; full-size economic arms use Release. Measurements run in the foreground after checking for another testhost. No game, rig, ModsLocal or bridge was used. These are component measurements; whole-frame timing is not measured here. All sampled component p95 values are below 5 ms. There are zero ≥30 ms samples in these component windows.

## Reproduce

Use the required Homebrew PATH/DOTNET_ROOT environment, then:

```sh
PC_F6F_MEASURE=/tmp/pc-f6f-perf.jsonl dotnet test tests/PersistentCity.Tests.csproj --filter FullyQualifiedName~F6fMeasure
PC_F6F_SUSTAINABILITY=/tmp/pc-f6f-sustainability.txt dotnet test tests/PersistentCity.Tests.csproj --filter FullyQualifiedName~F6fSustainabilityTests
PC_F6F_FULL_ARMS=1 PC_F6F_SUSTAINABILITY=/tmp/pc-f6f-full-arms.txt dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F6fSustainabilityTests.FullSize
```

Check `pgrep -f testhost` before launching; wait for another run to finish. Run one dotnet process at a time. No regression matrix is invoked.

`F6fMeasure` uses 1,000 operation/timer/allocation-counter warmups and 20,000 samples per gate/debit/lookup arm, plus 5,000 requested total-line builds. Timers include the wallet reset in the debit sample, so its number conservatively includes fixture reset overhead. Allocation uses the calling thread's allocated-byte counter. Day-row creation, attach and wide-integer materialization on a save/output request are outside steady-state payment samples. The ordinary amount and watermark use exact money; no display rounding enters settlement.

A combined soak/benchmark attempt reported 7,728 B during the scalar-gate sampling prelude. It is retained as a harness/warmup anomaly rather than a steady-state allocation result. The final isolated run warms the timer and allocation-counter plumbing as well as the operation, and its zero-allocation assertions pass. A launch that overlapped the F6g testhost was discarded.

## Admission, debit and request-only output

| Component | p50 ms | p95 ms | Maximum ms | B/operation |
| --- | ---: | ---: | ---: | ---: |
| Admission gate | <0.0001 | 0.0001 | 0.0002 | 0 |
| Person-wallet lookup + gate, 1,914 owners | 0.0001 | 0.0001 | 0.0304 | 0 |
| Person-wallet lookup + gate, 10,000 owners | 0.0001 | 0.0001 | 0.0002 | 0 |
| Person-wallet lookup + gate, 100,000 owners | 0.0001 | 0.0002 | 0.0212 | 0 |
| Admission + own-ledger debit + dated counters | 0.0003 | 0.0003 | 0.0128 | 0 |
| Requested outside total line | 0.0007 | 0.0009 | 0.0112 | 656 |

Timers quantize at 0.0001 ms; a reported zero is below that resolution. Both the person wallet and published place owner use ordinal dictionaries; admission performs no catalogue/roster scan. A hidden panel has zero requests and zero string construction from this feature, asserted by `NoOutsideProjectionOrStringIsBuiltBySimulationEvents` and the existing panel-open guard. On-request totals scan at most 14 dated rows, independent of population or shop count.

## Hourly ownership index

The source fixture includes a bounded source dictionary, an empty ambiguity set and the actual `OrdinaryOwnershipIndex`; existing managed place handles stand in for existing native controllers. The table measures the complete source lookup, 16-entry staging/publication and production unit timer. Retained bytes are managed live-heap deltas after collection, excluding the already-owned place/ID objects. Unity controller objects and game getters are not loaded by this headless test.

| Places | Observations per hour | Units per hour | Samples | Unit p50 / p95 ms | Unit maximum ms | Steady B/unit | Retained metadata B |
| --- | ---: | ---: | ---: | --- | ---: | ---: | ---: |
| 1 | 1 | 1 | 1,000 | <0.0001 / <0.0001 | 0.0235 | 0 | 2,072 |
| 1,024 | 1,024 | 64 | 64,000 | 0.0007 / 0.0008 | 0.0321 | 0 | 185,728 |
| 20,000 | 20,000 | 1,250 | 37,500 | 0.0007 / 0.0010 | 0.0329 | 0 | 1,843,464 |

Published place-owner lookup: p95 <0.0001 ms at all three sizes; maxima 0.0029 / 0.0040 / 0.0127 ms, with 0 B/lookup.

Total observed callbacks: 1,000 / 1,024,000 / 600,000 over 1,000 / 1,000 / 30 hourly refreshes. There are no added native reads at admission, panel build or diagnostic request. In Runtime, each observation reads one existing registration; `RentedByPlayer` is read at most once per entry, and `businessOwnerRivalId` at most once after the player guard. Thus these two ownership member reads are bounded by 2 / 2,048 / 40,000 per complete outside-owned refresh and 32 per unit; missing/player entries use fewer. Existing establishment/type fields classify the bounded visit class. Headless callbacks prove work/count bounds, not native getter latency. Actual native read/allocation/retention and whole-frame evidence remain rig check 6 in [checks](../checks/f6f.md).

A failed read publishes none of its unit. Older revisions authorize no debit. F1 holds the calendar until all units finish; no ownership scan runs inline in admission. The loaded hour is not reconstructed. The Runtime controller map retains at most the same 20,000 destination IDs; duplicate-address quarantine is bounded by that set. Planner rebuilds reuse it, and detach releases it.

## Cheap allocation win

Before: directly adding one exact price/count through the existing `BigInteger` properties costs **64 B/payment**, p95 **0.0001 ms**, maximum **0.0084 ms** (20,000 samples). After: private exact deltas on the same public saved properties cost **0 B/payment**, p95 **<0.0001 ms**, maximum **0.0001 ms**. On-request getters fold the deltas into the same wide values; a rare long carry also folds exactly. The unchanged 26-digit wire limit is checked before publication, with refusal leaving both counters and wallet unchanged. No precision, field, schema, retention or writer hash changes. `WideTotalsAndRolloverAreExactAndFreeOutputIsTotalsOnly` and current-writer round trips cover the wide-value path.

The four known hotspots—physical population, closure refresh, optional-stop refresh and save serialization—remain parked. No save-latency win or whole-frame pass is claimed.

## Sustainability evidence

The normal F6f tests run F6a control, 10% workplace closure and 5% blocked-worker-route arms for 42 days, seed 3, at 8 residents plus 8 visitors. There are 2,016 daily conservation checks across the three runs. Every daily 14-day ordinary total equals the corresponding difference in cumulative watermarked wallet debits; native receipts and rival settlements retain their separate checks.

Flagged small-fixture control trend: week-end p10 balances decline each week: approximately **$278, $214, $116, $50, $27, $5**; week-end arrears owners are **0, 0, 0, 1, 1, 1** out of 16 wallets. The closure arm reaches two wallets in arrears and p10 zero. This is measured strain, not F6h sustainability acceptance. F6h owns the multi-seed control band and calibration. No coefficients were changed to hide this trend.

Full-size Release check (repeated on the final cap-guard code in Debug with identical financial metrics): five seeds × control / workplace closure / blocked-worker routes, **15 runs**, each 957 residents + 957 independent visitors for 42 days. **1,205,820** daily wallet conservation checks pass. Every retained ordinary total reconciles with cumulative watermarked debits; zero duplicate receipts and historical clock misses.

| Seed | Control p10, week 1 → week 6 ($) | Week-6 arrears, control / closure / blocked | Unfed owner-days, control / closure / blocked |
| --- | ---: | --- | --- |
| 0 | 688.28 → 687.05 | 1 / 14 / 6 | 1 / 256 / 120 |
| 1 | 702.22 → 680.91 | 5 / 16 / 10 | 5 / 277 / 110 |
| 2 | 694.61 → 685.02 | 5 / 15 / 13 | 3 / 260 / 169 |
| 3 | 690.37 → 672.11 | 1 / 13 / 4 | 1 / 267 / 79 |
| 4 | 680.93 → 716.77 | 4 / 14 / 8 | 0 / 222 / 112 |

The full-size controls end with 1–5 wallets in arrears out of 1,914. Several controls decline after their week-2/3 peak; they do not reproduce the small fixture’s every-week decline. Closure and blocked-route arms show greater strain. These observations are retained for F6h’s combined-loop calibration; they do not establish its control band. Saves used by the harness are outside component timing windows. The native batch must record the original autosave setting, verify it off before every window and after reload, invalidate any window containing a save, then restore settings. No native performance window was taken here.

## Review corrections

Restoring the transient visitor mode during market attachment uses the existing budget-queue insertion pass and cached owner. Policy attachment after the market walks those existing entries once and reflags the attached counters in place. It retains no new collection and adds no simulation-event scan. The shared same-place arrival predicate reads existing fields in O(1); a continued admitted visit skips the affordability lookup because it records no new arrival. New arrivals keep the existing gate and debit. There are no added native reads or panel builds.

These are correctness fixes, with four regression cases failing before and passing afterward (both attachment orders, shop and leisure continuation). The component timing tables above are the original slice measurements; no new timing or whole-frame result is claimed for this review correction. The four parked hotspots stay unchanged.
