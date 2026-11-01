# Schema 24: bounded free rival outflow

Schema 24 adds only `Food.Outflow`, a bounded ASCII scalar. D2's saved consumer
tuples, external-spend accounts, settlement watermark, prices and receipts are
unchanged. D4 requires no changes here. No native rival state is written.

Why a new contract is necessary: D2 saves lifetime external spending and the
current meal's price/watermark, but no dated rival amounts/counts. Current-day
outcomes have no price and roll over. Player-shop `History` deliberately excludes
rivals. Fourteen days cannot be reconstructed from those facts without invention.

The wire is:

```
StartDay|PartialStart|ThroughDay|BaselineUnits|ExpiredUnits|ExpiredCount[|Day,Units,Count]...
```

Six header fields plus zero to fourteen strictly ascending nonempty daily cells.
Days are nonnegative, no later than the saved clock, and within the last fourteen
days including today. `PartialStart` is exactly 0/1. Numeric text is canonical
unsigned decimal (no signs, leading zeroes, fractions, exponent or whitespace),
at most 26 digits; days also respect the existing billion-minute clock limit.
Amounts use D2's exact money units. Counts/aggregates use bounded BigInteger
arithmetic, so summing many valid consumer accounts cannot overflow one wallet's
Int64 range. Rows require positive counts/amounts and the existing maximum native
price per purchase; expired totals obey the same constraints.

Reconciliation is exact:

```
BaselineUnits + ExpiredUnits + sum(retained daily Units)
    == sum(Budget.ExternalFoodSpent or LegacyExternalFoodSpent)
```

Recorded counts (expired plus retained) cannot exceed D2's lifetime all-purchase
count. Pre-history rival counts are unknown: D2 has no separate lifetime rival
count. A migration never infers that count from amounts or all-shop purchases.

`ExternalFoodSettlement.TryPay` records the daily debit within the existing
non-yielding payment operation, before publishing D2's paid watermark. Production
passes the actual settlement minute, so an earlier meal paid late belongs to the
debit day. Repeated outcomes do no history work; repeated payment attempts fail
on D2's saved status/watermark, including after reload and after row expiry.
Historical late settlements inside retention join their dated total; ones older
than retention join only the expired rollup. There is no second intent set,
per-consumer journal, native receipt, or named rival bucket.

Rollover prunes at payment or coherent save preparation; a free projection filters
to its requested fourteen-day window even without either event. No frame poll or
day scan is added. An idle city's in-memory rows can await that next payment/save,
but never exceed fourteen, and expired dates cannot appear in its free projection
or next written payload. Expired totals retain lifetime reconciliation indefinitely
with constant cardinality. Cached immutable free projections/text invalidate on
payment, rollover, or requested display-day change and are never serialized.

Exact migration order is the existing 13–20 readers → 21 → 22 → 23, then **23 →
24**. Each predecessor passes its own original validation before the new step.
Schema 21/22/23 readers exclude `Outflow`; foreign injected fields refuse. The
new step freezes the existing external ledger as `BaselineUnits`, creates empty
dated rows, and records the current start day (partial if not midnight or if the
undated baseline is nonzero; even midnight cannot exclude a boundary-minute debit).
Earlier days display unknown; a partial start day explicitly excludes earlier
spending; fully covered empty days display zero. No historical debit is replayed,
no identity or price changes, and the caller's source string is untouched.

Schema 24 requires the field and non-null history whenever food state exists.
Malformed, missing, inconsistent, oversized, duplicate/unordered/expired rows,
foreign predecessor fields, unsupported menu capabilities and future schema 25+
refuse without replacing the saved source through the existing store/session
rules. A population-only city gains no field or record.

Cost: at most **1,024 ASCII characters** of history plus **13 bytes** for the
quoted property and separator, **1,037 bytes/city**, **zero bytes per consumer**.
One rollup plus each retained row is charged in both streaming and semantic
counts: **1–15 embedded records**. These consume the existing D3a history reserve
once; no cap increases. A 365-day synthetic fixture uses **280 characters / 293
property bytes / 15 records**. Managed retention is measured separately in
[D3a performance](perf/d3a-outflow-line.md).

The public free DTO contains only `Days`, exact `Units` and `Count`; daily cells
contain day, known/partial flags, units and count. There is no identity, location,
named grouping or market denominator. Ctrl+F8 uses this DTO/text on the existing
three-second open-panel refresh; explicit D2 bridge diagnostics expose the same
free DTO. Unknown flags are never rendered as zero. Paid research remains off;
named paid inputs and entitlement/report integration belong to D3b/C5.
