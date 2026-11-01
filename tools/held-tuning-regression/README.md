# Held tuning offline benchmark

Run in the foreground after the repository's other-dotnet wait:

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" run --project tools/held-tuning-regression/HeldTuningRegression.csproj --configuration Release
```

The same tool compiles against archived B0 Core/Persistence sources with
`-p:SourceRoot=<absolute-baseline-root>` (the root contains Core/ and Persistence/).
The B0b manifest records the exact source head, commands and run ordering.
No game or rig files are read or written.

Each fresh process warms 300 decisions, then measures 3,000 decisions per market
size (one/eight shops). Capture includes the normal bounded shortlist and native
input copies through the evaluating checkpoint, with zero routes. Continuation
covers every quote, selection and commit: exactly two routes per shop and zero
checkouts, stopping at travel. Setup is outside timers. Per-stage allocations,
p50/p95/max and counts of samples >=30 ms are reported. These are synthetic Core
CPU costs, not ordinary-play mod-work or whole-frame measurements.

Retained measurements keep 20,000 individual DTO graphs alive across collection,
without constructing or saving a city that holds them all. Exact allocated graph
size provides the stable structural before/after comparison; managed retained
heap deltas also include GC accounting noise. Prebound delegates keep reflection
and expression compilation outside those counters. The shape-precheck microbench
compares an image-materializing prototype with the production nonmaterializing
precheck, 10,000 warmups / 300,000 calls. Reflection here is benchmark infrastructure,
never production simulation work.

For payload/record limits and actual reference/5,000-consumer sizes use
`tools/save-contract-regression --capacity reference` and `--capacity consumers5000`.
The historical `--capacity 5000` means **5,000 residents / 10,000 consumers**.
CF32/CF64 retain B0's complete future reservations and deduct B0b's real 405-byte
image once from the already allocated aggregate policy/header allowance.
