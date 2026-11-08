Status: implemented (offline); not verified by rig

Refs #93. Base `1c295905` (F2b #151); writer 33. Rule and thresholds: [multi-item meals decision](../decisions/2026-11-08-multi-item-meals.md). Headless macOS arm64, .NET 10. All runs sequential and in the foreground. No game, rig, ModsLocal or bridge was used.

## Extra-selection rates

Seed 11, 20,000 consumers, $12.50 main, one candidate extra each, neutral taste, ample wallet (Release):

| Candidate extra | Band | Measured rate |
| --- | --- | --- |
| $6.00 (48%) | cheaper | 0.3507 |
| $0.50 (4%) | cheaper | 0.3507 |
| $9.00 (72%) | cheaper | 0.3507 |
| $12.00 (96%) | similar | 0.0514 |
| $16.00 (128%) | excluded | 0 |

A similarly priced second dish is 6.8× rarer than a cheaper extra. The $0.50, $6 and $9 rows share their draws by construction, so a cheap side gets no price bargain inside its band.

A native-price menu next to a $12.50 burger main ($9.24 and $5 cheaper, $10.25 similar) gives a mean of **0.755 extras per meal**: 7,798 none, 9,402 one, 2,697 two, 103 three.

## Cost

`FoodMealExtras.Plan` on an eight-item menu: **1.51 µs** per call (mean of 200,000 calls after 2,000 warmup, Release). Checkout calls it once per paid player or rival meal. Since review fix Sol-r1-1 every decision also calls it once per feasible candidate (at most 64), so a decision with a full eight-shop, eight-offer shortlist adds about 0.1 ms of pure policy work at this rate; an observed decision calls it once more per candidate for the trace.

Unmeasured native cost, for the rig: a player checkout at an extras-capable shop now makes one decision read (`ReadBusiness`, which may hit the per-minute supply cache) before the hold, and each sold extra adds one `FullServiceBusinessSimulator` setup, one `ProcessCustomer` call and one live menu supply read. With at most three extras that is at most four native orders in one checkout call. Rig check 4 measures it. No mod-work or whole-frame number is claimed here.

## Local validation

- Build (`tools/build.py` with the supplied Managed directory): passed. DLL SHA256 `a80c27715d9752a374a42cc7a83fb08ee7b05f1df7560f4ca08259686b4d8595`; after review round 1, `4ea764e41d72b76ecd2ec0030c8e997b16dc0a472e6eb9c54b0bd573be1aed89`; after round 2, `7ea7982deb187d6e8863f424b82ca04782eed4a9d0b5ada85683f829a1c098a4`.
- `dotnet test tests/PersistentCity.Tests.csproj`: **3,925 passed**, 0 failed or skipped, 3 min 53 s; after review round 1, **3,934 passed** in 3 min 49 s; after round 2, **3,937 passed** in 4 min 25 s. Both writer-33 golden pins unchanged.
- New `F6jMealExtrasTests`: 18 passed; 27 after review round 1; 30 after round 2.
