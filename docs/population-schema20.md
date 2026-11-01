# Population schema 20 - E1 weather

Allocated after C2a schema 19, integrated against merged C2a `692b8b5` and C4 base `1f8f4b4`, refreshed through development `2f12ae9` (read-only E3 salad and docs-only F1). Only population weather fields change.
The source 13-17 codec, schema-18 tuple/memory/tuning contracts and schema-19
awareness contract retain their wire shapes and refusal rules. The merged schema-19 wire is identical to E1's `9b13698` source shape; B1's
active revision-1 capability and bounded nonzero held memory contributions remain
executable. The strict 19->20 reader adds no source field or tuple allowance.

`WeatherStartsDay` is one required root integer, 0 through saved logical day + 1.
On migration from 19 (also through older supported sources), initialize weather
unknown and activate the new policy on the next logical day. New cities start at
day 0. This activation is separate from ordinary tuning: loaded flat settings apply
to the next uncaptured context, including on the same day.

Each resident has required nullable `W`, the one optional-outing or queue capture.
Its nonnull wire is exactly 15 literal ASCII characters:

| Offset | Meaning |
| --- | --- |
| 0 | Weather contract revision `1` |
| 1 | `O` for discretionary outing admission, `Q` for opening queue |
| 2 | Weather: `0` unknown, `1` dry, `2` rain |
| 3 | Outing admitted `0`/`1`, or queue rain maximum lead `2` through `A` (10) |
| 4-14 | Exact binary64 logical boundary as little-endian bytes, canonical base64 with its final `=` omitted |

An outing's boundary is the existing event minute; a queue's boundary is its
scheduled opening. The event publishes its immutable capture to the live resident
before yielding a route, then copies/commits it with the existing event state.
Reload/restart uses the capture at that same boundary. A new boundary replaces it;
there is no list or weather history. A declined outing retains the ordinary home
fallback deadline and cannot retry because rain changes.

The consumer's existing charged `Memory` tuple scalar appends exactly `|0`, `|1`
or `|2`, recording its last captured food-context weather. Its original version-1
prefix/watermark/four memories remain unchanged. Schema 20 requires the suffix;
18/19 refuse it. Current maximum length is 50, including the two extra characters.
Shape checking validates the prefix directly without a temporary substring.

The one held food decision adds required `Weather` (integer 0-2) and
`WeatherTravelMultiplier` (finite binary64 1-2). Dry/unknown require multiplier 1.
The existing context key, attempt, origin minute and schema identify the boundary
and revision; the captured multiplier belongs only to this image. It affects the
same soft distance coefficient in shortlist and final offer weights. No coefficient
enters draw keys, and no awareness source or route allowance changes. Resuming an
old held decision retains unknown/1 and its existing shortlist/quote progress.

All new fields are required in a schema-20 payload (nullable W is explicit).
Unknown versions/codes, escaped or noncanonical compact encodings, invalid boundary
or lead values, dry/unknown declined outings, missing fields and nonfinite/out-of-range/nonneutral food inputs
refuse. Reads never clamp saved inputs. A failed/future read leaves the original
payload owned by the save store/session. Migration runs complete semantic validation
and a counting serialization/UTF-8 preflight before publishing the upgraded state.

## Byte and record ledger

| Allocation | E1 actual maximum, including existing field/slot overhead | Headroom |
| --- | ---: | ---: |
| Physical optional-outing/queue: 24 ASCII bytes/resident | 22 (`"W":"<15 chars>",`), null costs 9 | 2/resident |
| Consumer weather: 8 extra bytes inside the charged extension | 2; full extension now 53 including quotes/separator | 6/consumer |
| At 50,000 residents x3 consumers, combined weather reservation 2,400,000 | 1,400,000 | 1,000,000 |

The root activation integer consumes the existing 64 KiB header allowance; the
two scalars on the single held image consume the existing 1 MiB decision allowance.
They are constant per city, never daily configuration snapshots. The capacity tool
subtracts their actual bytes and every physical/consumer weather byte from the
existing future reservation, so none is charged twice. It populates real captures
in its capacity fixtures. Actual CF32/CF64 results are recorded in [E1 checks](checks/e1-rain.md).

A nonnull W charges one record in both counters, bounded by the existing E1 50,000
physical-resident reservation. Null W charges zero. The two consumer suffix bytes
remain inside the already-charged extension, adding zero records. The two held
decision scalars and root activation add zero collection records. All previous
record/character/UTF-8 caps remain fixed.

## Runtime and settings

The native source reads only `OptionalStopEnvironment.Raining => RainHelper.isRaining`,
lazily cached by Unity frame for new boundaries. Missing/throwing/unknown input is
neutral. Work/study, committed travel, paid food, road capacity and speed have no
weather branch. Existing optional-stop native-rain cancellation and paired
continuation rules remain intact. A customer already at the destination captures
its Q weather/lead when the logical event commits `WaitingForOpening`, regardless
of camera radius or body allocation. Physical refresh reads that capture without
resampling. Only a migrated/pre-existing wait lacking a matching Q capture uses
the legacy first-lookup fallback. Disabled/pre-activation E1 still captures the
native queue rain rule at commitment, using its existing five-minute maximum;
only enabled, activated new commitments use the configured E1 lead.
Even with E1 disabled or before activation, a new wait freezes the native rain
sample at commitment instead of re-reading live rain on each physical refresh.

The template and unchanged loader expose flat `WeatherEnabled` (true),
`WeatherRainOptionalOutingMultiplier` (.7, 0-1),
`WeatherRainTravelWeightMultiplier` (1.3, 1-2) and
`WeatherQueueRainLeadMinutes` (5, integer 2-10, bounded by the existing dry maximum).
Clamp/default diagnostics run once at load. The disable switch affects new E1
outing/food captures; native optional-stop safety and native queue rain rules still
apply, with the existing five-minute rain maximum for new queues when disabled.
Held contexts retain their inputs after a tuning change or disable.

`PopulationRuntime.Weather(residentId)` is a read-only, on-demand bridge projection
of saved physical and food weather captures, activation, context/quote counters
and the unknown-neutral explanation. It samples no live weather, creates no
decision/route and performs no native write.

The existing awareness diagnostic uses the held weather multiplier when an image
is held. Its uncaptured proposal uses neutral weather; inspection never samples
live rain or creates an E1 capture. The new weather bridge projection labels saved
inputs separately from any future context.
