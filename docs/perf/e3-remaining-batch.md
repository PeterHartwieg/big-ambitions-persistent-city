# E3 remaining batch performance — 2 November 2026

Offline Release .NET 10 measurements of actual production readers against conditional native field doubles. These are neither native combined mod-work nor whole-frame measurements. Six incoming candidates are measured before/after the descriptor addition using the same harness; all six named new candidates are measured after. A fresh synchronously awaited foreground apphost process supplies each candidate's single cold call, including JIT and descriptor/tag metadata initialization. Cold has one sample, **not** a percentile. Warm has 30 warmups then 1,001 individually timed valid calls per size; construction, assertion and sample sorting are outside measured calls. Furniture includes one bag counter. All original cases/facades/read branches remain unchanged.

`tools/e3-remaining-feasibility-regression/measure_baseline.py` reconstructs the exact incoming shared reader, SHA-256 `239650040cb06dde4f0d8030e659c906a5fe09c8b37ea7be9dc763162ddb880d`, from `a1b993a`; one generated ignored project measures all six before candidates. `dotnet run --project tools/e3-remaining-feasibility-regression/E3RemainingFeasibilityRegression.csproj --configuration Release -- measure` builds/runs once for the after batch, with child processes awaited sequentially. No per-candidate dotnet build/run loop or background test job. Logs remain ignored under `build/e3-remaining-batch/`; the dated checks receipt preserves results/hashes.

## Taken win: typed seasonal field reads

The initial seasonal gate read the inspected enum through `FieldInfo.GetValue` and `Convert.ToInt32`, boxing/converting the value on every requested seasonal read. The final gate reads `Item.season` directly, referencing the inspected `BigAmbitions.Seasons` assembly as a read-only build input. Both gates refuse the same seasonal/special-gift/sale/substitution flags and never call season/preferences helpers or cache mutable values. This removes **48 B/call** on each of candycane and christmascookies: **248 -> 200 B** at two entries and **49,152 -> 49,104 B** at 2,000. No shared-reader branch change, native season evaluation or per-frame participant. The reflection prototype was measured before replacement; it was never enabled in normal play. Timing differences are not claimed as causal wins.

| Reader / revision | Cold ms / B (one call) | 2 entries warm p50 / p95 / max ms; B/call | 2,000 entries warm p50 / p95 / max ms; B/call |
| --- | --- | --- | --- |
| Donut before | 1.7287 / 1,112 | 0.0004 / 0.0005 / 0.0079; 200 B | 0.0806 / 0.0983 / 0.9941; 49,104 B |
| Cupcake before | 1.7065 / 1,112 | 0.0004 / 0.0005 / 0.0051; 200 B | 0.0811 / 0.0984 / 1.0329; 49,104 B |
| Salad before | 1.6797 / 1,112 | 0.0004 / 0.0005 / 0.0084; 200 B | 0.0810 / 0.0991 / 1.0333; 49,104 B |
| Icecream before | 1.6694 / 1,112 | 0.0004 / 0.0005 / 0.0051; 200 B | 0.0814 / 0.1011 / 1.0391; 49,104 B |
| FrenchFries before | 1.8683 / 1,112 | 0.0004 / 0.0005 / 0.0074; 200 B | 0.0810 / 0.1004 / 1.0220; 49,104 B |
| Pizza before | 1.6864 / 1,112 | 0.0005 / 0.0005 / 0.0050; 200 B | 0.0805 / 0.0980 / 1.0153; 49,104 B |
| Donut after | 1.7232 / 1,256 | 0.0004 / 0.0005 / 0.0054; 200 B | 0.0802 / 0.1015 / 1.0370; 49,104 B |
| Cupcake after | 1.6944 / 1,256 | 0.0004 / 0.0005 / 0.0040; 200 B | 0.0814 / 0.1041 / 1.0403; 49,104 B |
| Salad after | 1.8312 / 1,256 | 0.0004 / 0.0005 / 0.0047; 200 B | 0.0817 / 0.1040 / 1.0467; 49,104 B |
| Icecream after | 1.7209 / 1,256 | 0.0004 / 0.0005 / 0.0038; 200 B | 0.0811 / 0.1002 / 1.0337; 49,104 B |
| FrenchFries after | 1.7537 / 1,256 | 0.0004 / 0.0005 / 0.0081; 200 B | 0.0832 / 0.1071 / 1.0683; 49,104 B |
| Pizza after | 2.3636 / 1,256 | 0.0005 / 0.0005 / 0.0413; 200 B | 0.0847 / 0.1441 / 1.0914; 49,104 B |
| CheesePlatter after | 1.6559 / 1,256 | 0.0004 / 0.0005 / 0.0081; 200 B | 0.0856 / 0.1072 / 1.1079; 49,104 B |
| CottonCandy after | 1.7342 / 1,256 | 0.0004 / 0.0005 / 0.0078; 200 B | 0.0809 / 0.1066 / 1.0468; 49,104 B |
| Popcorn after | 1.7105 / 1,256 | 0.0004 / 0.0005 / 0.0049; 200 B | 0.0821 / 0.1297 / 1.0408; 49,104 B |
| Kabob after | 1.8991 / 1,256 | 0.0007 / 0.0008 / 0.0066; 200 B | 0.0831 / 0.1046 / 1.0609; 49,104 B |
| CandyCane after | 1.9273 / 1,256 | 0.0005 / 0.0005 / 0.0100; 200 B | 0.0848 / 0.1134 / 1.0322; 49,104 B |
| ChristmasCookies after | 1.8680 / 1,256 | 0.0005 / 0.0008 / 0.0518; 200 B | 0.0793 / 0.0997 / 1.0672; 49,104 B |

All 12 final readers: worst 2,000-entry warm p95 **0.1441 ms**, largest warm sample **1.1079 ms**; largest single cold call **2.3636 ms**. **Zero single-reader samples >=30 ms** across incoming baseline, reflection prototype and final sets. Uncalled allocation control: **0 B** for every reader. Cold initialization for the six existing readers is **1,112 -> 1,256 B**; warm allocations stay **200 / 49,104 B**. No timing improvement is attributed to the descriptors.

| Candidate | Prototype 2 / 2,000 entries B/read | Final 2 / 2,000 entries B/read | Saved B/read |
| --- | --- | --- | --- |
| CandyCane | 248 / 49,152 | 200 / 49,104 | 48 |
| ChristmasCookies | 248 / 49,152 | 200 / 49,104 | 48 |


Six new pinned descriptors add **144 B once** to first-use shared candidate initialization (six objects at 24 B). Existing and final new readers retain **200 / 49,104 B per valid read** at 2 / 2,000 entries; adding an uncalled facade adds no per-frame work. Seasonal reads add one extra loaded-definition lookup and scalar/array-length checks, with no additional allocation. The existing bounded showcase scan dominates large fixtures. No other cheap safe measurable improvement was identified in touched code; metadata already binds once, success avoids diagnostic formatting, and broadening/caching native stock would risk the evidence/refusal boundary.

Production source isolation proves **zero per-frame reflection, scans, polls or allocations and zero reader cost when uncalled**. The offline no-reader control records zero allocation; no tiny timing is presented as a measurement of literal zero CPU cost. Physical population, closure refresh, optional-stop refresh and save serialization remain parked and unchanged.

Native combined mod-work p50/p95/max with **p95 <5 ms** and **every spike >=30 ms flagged**, plus separately recorded whole-frame timings/stalls, remain for the normal combined 6 Nov session. Autosave must be disabled/verified before each native window and after reload, manual saves excluded, any save-contaminated window invalidated, and original settings restored. No special performance-only rig run occurred or is planned. [Dated checks and concrete native handoff](../checks/e3-remaining-batch.md).
