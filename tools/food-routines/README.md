# Food opportunities from observed routines

Run the actual population engine over a controlled two-neighborhood fixture, record completed stops, and compare food markets against the same residents and needs:

```sh
dotnet run --project tools/food-routines/FoodRoutines.csproj --configuration Release -- build/food-routines-demo 957
dotnet run --project tools/food-demand/FoodDemand.csproj --configuration Release -- build/food-routines-insight build/food-routines-demo/insight-input.json routine-two-neighborhoods
```

The comparison covers seven days and one purchased-meal opportunity per resident/day. It compares one 15/hour shop, two 15/hour shops in different neighborhoods, a single 30/hour shop, two shops at one location, unavailable service and a price change. These are controlled effective service rates. The fixture is not a native customer count, geography, staffing or profitability measurement. The small HTML insight replays a 24-resident subset; it does not retain the full cohort's queues.

`FoodRoutineRecorder` subscribes to `CitySimulator` activity events only while explicitly attached. It records completed presence at places, leaving travel and opening waits out. `FoodRoutinePlanning` derives a reproducible daily food window from personal quiet/wake policy and modest appetite-time variation. An actual stop supplies the origin and limits the available time. Work/study gets a provisional 45-minute lunch break; other stops get at most 75 minutes. Eating takes 15 minutes; queue patience is 10–15 minutes at work/study and 15–20 elsewhere. These are stated game-scale policies, not measured employee breaks or physiology.

`FoodRouteBatch` considers at most three nearby candidates by default (configurable up to eight). Distance is only a shortlist and walking-tolerance refusal; successful outbound and return quotes require actual complete routes. `Step(2)` performs at most two route calls, and the existing native walking adapter caches path geometry. Missing candidate quotes remain unknown. Quotes are tied to a single opportunity/origin; hypothetical food trips do not rewrite the observed population's history.

In routine mode, stock and effective service throughput must be known before fulfillment. A continuous aggregate queue smooths the hourly rate into service intervals, reserves stock for queued customers, checks patience/return deadlines, and rechecks price, opening, service and stock before completion. Native hourly capacity is a ceiling; this smoothing is our policy. There is no same-day retry after a failed outing, home-food supply or automatic restock. The first eligible observed stop is evaluated; choosing a later opportunity after rejecting all shops remains future work.

Food documents now use schema 3, separate from the unchanged population schema 7. Schema 2 introduced routine/service fields; schema 3 adds customer-service and district-product observations plus personal choice weights. Strict older experiments retain zero new weights. Strict schema-1 documents migrate into `FixedDailyReference` mode with their old numerical behavior; migration never supplies missing service evidence. Routine-mode saved queues resume deterministically.

## Native diagnostic

The runtime exposes explicit `ObserveFoodRoutines()` and `QuoteFoodRoutes(...)` entry points. Neither runs automatically or writes sales. Quote batches must be stepped on the main thread and refuse a changed city/navigation instance.

`native/Probe.cs` is a Mac test-rig-only helper. It records one complete day of actual population activity, then uses the native catalogue reader and bounded walking quotes while paused. It runs both strict unknown-stock/service mode and a clearly labeled controlled-service counterfactual. It verifies that paused population and selected native market/financial fields remain unchanged by the diagnostic.

`build_probe.py` references the frozen candidate under ignored `build/food-routines-20261022/source-repaired/build/PersistentCity/`. `run_mac.py` refuses a busy rig, backs up the isolated profile/preferences/manifest, installs the candidate with physical representation disabled plus the helper, runs a fresh FT city, and restores the rig. It never contacts Windows or the normal profile. Follow the shared [Mac ownership instructions](../../../big-ambitions-driven-rides/tools/agent-tests/MAC.md); retained evidence must use a new output directory. The diagnostic uses the native fast-forward mode with the clock multiplier left at normal and is not a rendering or normal-play performance benchmark.

Customer service and district appeal: [choice-signal comparison](../food-choice-signals/README.md).

Routine evidence: [routine-based food demand](../../docs/reviews/2026-10-22-food-routines.md).
