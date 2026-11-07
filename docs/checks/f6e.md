Status: implemented (offline); not verified by rig

# F6e — attendance wages and employment feedback

Refs #93. Base: `fb64fa576d` (F6d `eef1bdc174`, E6d `fb64fa576d`). The writer is `AttendanceSchema = EconomicLedgerSchema + 1`, allocated after F6d, currently 32. F6d's fields, E6c's vendor table and F7c's detached snapshot/wire format are reused. No migration or switch-over field is added. The public reader refuses writer 31 and every older development writer with the distinct “start a new city” status; codec, store and session tests keep the original payload.

## Implementation

- Employment income is credited only by logical shift postings into the person's F5 wallet. Daily employment credit is removed from the current policy; pension and support keep their daily posting. Missing work does not substitute support. The frozen development simulation retains its old daily postings for historical golden checks; the product reader refuses those schemas.
- F5's current persona schedule has four 480-minute shifts each week. The hourly rate is the original daily band × 7 × 60 / 1,920. Integer numerator remainders carry between shifts in `UnpostedWage`, initially biased by half the denominator, so cumulative wages round to the nearest exact unit. No displayed-cent rounding is used. A city created during a shift can earn its remaining attended minutes from its first day.
- The existing tuple records the shift-start day, completed attended segments, carry, 14-day miss mask and stage (unset, working, closed, settled, closure day, after midnight with/without prior attendance). An ongoing segment is reconstructed from F5's saved admission record. Departure commits attendance with the citizen event; abandoned route continuations cannot mutate wages. Work-return admission is recorded after a meal outing.
- Shift start/end and midnight portions share the minute/ordinal posting heap, at most 32 postings per unit, before meals at that minute. Its index is derived on load. A day/stage watermark prevents duplicate settlement, including after midnight. F5's current templates end on the same day. Active shifts retain the saved F5 commute-preparation window, so an explicit overnight fixture can traverse the posting heap and strict save/reload without new fields.
- Closed-at-start shifts earn zero and record a closure day without a personal miss, including after reopening/reload. The live adapter reads only native workplace availability and hours at shift start; changed hours update the existing logical place. Native payroll and rival finances are untouched.
- Misses expire after 14 calendar days. Job loss uses the existing person/day draw with `min(0.01, 0.001 × (1 + 0.5 × recent misses))`. Assignment/loss resets the employment tuple. Existing vacancy and reachable-route probes remain the only finding path.
- `pc workplace close <place-id>` and `pc workplace open <place-id>` require the exact disposable city name `FT-F6e-Attendance`. The command refuses an in-flight planning event. This fixture input is transient and must be reapplied after load; a captured closure day is saved. `pc population person <id>` reports attendance, carry, watermark, misses and employment income. Existing city income counters include shift credits without adding a scan.

## Offline checks

`AttendanceWageTests` covers next-free writer/refusal, two current-writer hashes, F7c snapshot equality, full-attendance weekly trace against seven base daily credits, first-day partial attendance, every published shift/event/posting transition across reload, 30/60/120 cadence equality, blocked/closed/reopened shifts, late arrival/early departure, exact rounding carry across 28 shifts and reloads, midnight portions, exactly-once posting, job-loss draw stability, support after loss, conservation and atomic overflow. Core assembly reachability and the Runtime fixture gate are checked. Existing historical hashes are preserved under their original writers.

Completed foreground checks:

- Required `tools/build.py --game-managed` build: passed with the supplied external Managed directory.
- Required `dotnet test tests/PersistentCity.Tests.csproj`: 3,770 passed, zero failed.
- Full-size F6e employment arms plus attendance checks in Release: 23 passed, including five seeds × control / closure / blocked-route arms at 957 residents / 1,914 wallets.
- Final inspector checks after diagnostic wording cleanup: 28 passed.
- F6e performance measurement in Release: passed; steady-state settlement and posting allocation assertions are zero. Performance evidence is in [f6e.md](../perf/f6e.md).
- `git diff --check` and `python3 tools/docs-links/check.py`: passed (369 Markdown files, zero broken relative links).

No regression-matrix tools, game, rig, ModsLocal or bridge were used.


## F6a employment arms

Implemented check: `PC_F6E_ARMS=1 dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F6eEmploymentArmsTests`. Five seeds, 957 residents / 1,914 wallets, six weeks per run (two baseline, two intervention, two recovery), with a matched control and both employment arms: 15 runs. Each run checked all 1,914 wallets on all 42 days (80,388 conservation checks; 1,205,820 across the 15 runs). Every arm had zero duplicate orders and zero historical-clock misses.

Total outside income during the intervention fortnight, relative to the matched control (employment credits plus the unchanged support/pension postings):

| Seed | 10% of workplaces closed | 5% of workers’ routes blocked |
| --- | --- | --- |
| 0 | −9.55% | −4.69% |
| 1 | −9.38% | −4.68% |
| 2 | −9.16% | −4.54% |
| 3 | −9.32% | −5.05% |
| 4 | −9.67% | −5.11% |

The remaining intervention families and integrated calibration stay with F6h; native direction checks stay with the batched rig. These matched employment runs check the F6e causal connection and exact accounting.

## In-game checks for the next batched rig

The rig owns every check below in disposable `FT-F6e-Attendance` checkpoints. Retain before → red / after → green logs, screenshots or assertions on the eventual PR. These checks remain batched before release; none is delegated to Peter or a playtest.

1. **Fresh-city wages from day 1 — locked-PC; rig-automated.** Existing capabilities: FT `console`/`state`/`time`/`save`/`load`. Pending capability: RIG-LEDGER person-wallet export. Setup: create the named fresh city with this writer, before its first shift. Steps: sample the initial wallet, advance through admission and shift end, save/reload, advance again. Expected: no midnight employment credit, an attended first shift credits `EmploymentIncome` once, carry and wallet survive reload, conservation holds.
2. **Older-city refusal and payload preservation — input-only; rig-automated.** Existing capabilities: FT `save`/`load`. Pending capabilities: RIG-MODSET earlier-build selection and byte-exact PC payload readback. Setup: create a copied writer-31 FT checkpoint with its original PC payload. Steps: load it with this build, inspect refusal and payload, attempt an ordinary save. Expected: distinct “start a new city” status, payload kept byte-identical, no replacement PC city, owned-food authority follows the existing defined refusal path.
3. **Full-attendance weekly income — locked-PC; rig-automated.** Existing capabilities: FT `console`/`time`/`save`/`load`. Pending capabilities: named-person full-attendance checkpoints and RIG-LEDGER scheduled/earned comparison. Setup: choose a worker with four open, reachable 480-minute shifts and no absent minutes. Steps: run seven days, reload mid-shift, read `pc population person <id>` and its original income basis daily. Expected: weekly employment units equal seven base daily credits within one exact unit; support stays zero while employed; every daily ledger conserves.
4. **Late, missed and closed shifts — locked-PC; rig-automated.** Existing capabilities: FT `console`/`state`/`time` and `rig flow run`. Pending capabilities: RIG-ACCESS controlled route delay/block, RIG-CAMPAIGN matched employment arms and RIG-LEDGER. Setup: copied control, late, blocked and closed workplace checkpoints. Steps: delay arrival, block a whole shift, close a workplace at shift start, reopen it mid-shift, reload each stage, and run the two-week intervention/recovery arms. Expected: late minutes alone are unpaid; a missed shift pays zero without substitute support and sets one miss; closure pays zero with a closure day and no personal miss; reopening does not retroactively schedule the cancelled shift; repeated misses mildly increase loss on the same seeded draw, and support starts only after loss.
5. **Midnight-crossing settlement — locked-PC; rig-automated.** Existing capabilities: FT `console`/`save`/`load`/`time`. Pending capability: a named logical overnight-work-window fixture and shift-stage checkpoints (ordinary F5 templates currently do not cross midnight). Setup: an explicit 480-minute overnight logical shift in the named disposable fixture. Steps: save before admission, mid-shift, between midnight posting chunks and after shift end; reload each checkpoint and compare with control. Expected: each attended portion posts in units of at most 32, before meals, at its own epoch; total wage, carry, miss mask and watermark match control with no duplicate credit.
6. **Logical closure command and native boundary — input-only; rig-automated.** Existing capabilities: FT `console`/`state`/`time`/`save`/`load`, `shop_state`/`shop_log`. Pending capability: RIG-LEDGER independent native payroll fingerprint and logical-workplace selection. Setup: select 10% of workplace IDs in the named city and a differently named disposable refusal fixture. Steps: issue `pc workplace close <place-id>`, inspect the affected person's accrual after a shift, save/load and reapply the transient fixture input, then issue `open`; try both commands in the refusal fixture. Expected: acceptance only in the exact named city, zero wage and no miss on closure days, resumed wages on later open shifts, native payroll unchanged, daily conservation and zero historical-clock misses.
