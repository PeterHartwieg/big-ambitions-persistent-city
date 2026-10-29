# PC rig proof (agent1), option B, rerun at 7a104d6

**Verdict: PASS.** PC / `rig_test`, isolated `C:\BigAmbitions-AgentTests`. All AFTER measurements were rerun at **`7a104d6f41d7c92579dcdf2e790274856d31cb29`**, production code **`067e8c589e3fa96294cfe03360df479bd822ffab`**. Before remains **`7357f78ca13edd5200e7e708950ffbc7a6ade1fe`**. Its primary native measurements and native-written schema-14 input are explicitly reused from [evidence commit 7937557](https://github.com/PeterHartwieg/big-ambitions-persistent-city/tree/7937557935ef658c8410d2f2070a21058311e307/docs/reviews/rig/2026-10-29-pr3-optionB); its entire offline gate was rerun, and an additional three-window BEFORE performance confirmation was measured fresh. [Reuse hashes](baseline-reuse.json), [native before feature-absence trace](before-optional.jsonl). No production code or repository tests changed. This proof authorizes no merge, Workshop publish, release or ordinary-save deployment.

| Expected check | Before | After observed | Result |
| --- | --- | --- | --- |
| Offline build/tests/discovery/harnesses green | 30/30 commands; 1,403 tests | 31/31 commands; 1,551 tests; zero warnings | PASS |
| PR #3 regression absence/green | New scheduling/share/handoff/routine/settings cases absent | All present cases pass; [discovery](regression-presence.json) | PASS |
| Default about 25% long-run | Optional stops absent | 28-day cold 24.1467%; warm 24.9844% | PASS |
| 0 / 100 and chance accounting | No optional stops | 0: 0/9553; 100: 5041/11092; all audits exact | PASS |
| Clamp/fallback and four decrease arms | Feature absent | Eight attached settings; four matched arms; [settings](config-summary.json), [decreases](decrease-comparison.json) | PASS |
| 13/14 to 16 exact migration and refusal preservation | Supplies schema 13 and native-written 14 | 13/14 migrate exactly; 13 codec cases and five full native refusal cases | PASS |
| Pending, transfers, conversations and native reload | Schema-14 save preserves historical money | Eight reload comparisons; actual hand-on/pass-on/conversation; pending retained at zero | PASS |
| Normal-speed visible dwell/return | Feature absent | PASS: selected pc:136 native approach/dwell/reload/return/continuation | PASS |
| Exact finance (a)/(b), drift (c) | 110/110 exact; 1,914 ledgers | 110/110 exact; 1,914 ledgers; native float drift separate | PASS |
| Native QA: no new PC exceptions/inactive patches | 132 active; four unrelated inactive capabilities | Same capabilities; zero PC exceptions | PASS |
| Three 60-second autosave-off frame windows | Three reused primary and three fresh confirmation windows | Three fresh primary and three fresh confirmation windows; no timed exceptions; fresh whole-frame metrics within spread | PASS |
| Full restore, hashes, rig stopped and lease released | Fresh backup verified | All original files/preferences restored and verified; lease held=false | PASS |

**Default participation and accounting.** Denominator = independently observed `Choose` calls / `OptionalStopChances.Eligible`, after the eligibility guards. Numerator = unique scheduled recipients keyed by citizen/place/visit minute, including the waiting member of each conversation. Native logical skip telemetry counts scheduling; the normal-speed body observation below verifies that a selected scheduled stop actually approaches, dwells, returns and continues. It is not an exhaustive physical-completion census. [Compact independent audit](rate-audit.csv.gz), [audit verification](rate-audit-verification.json).

The owner criterion is **about 25% long-run**; about 24-25% over a month or warm window counts. A short cold-start shortfall is reported with draws, change in pending stock and drops, and is not failed by itself. Wilson 95% intervals below are descriptive binomial references for deterministic visit hashes, not guaranteed intervals for a fixed seed. No catch-up or rate change was made.

| Window | Realised / eligible | Realised | 95% interval | Draws (share; 95%) | Pending | Drops | Delta-pending/N / drops/N |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Cold 10 days, traced | 2297/10038 | 22.8830% | 22.0717-23.7151% | 2482 (24.7260%; 23.8919-25.5796%) | 0 -> 185 | 0 | 1.8430 pp / 0.0000 pp |
| Cold 28 days, untraced | 7902/32725 | 24.1467% | 23.6860-24.6134% | 8032 (24.5439%; 24.0807-25.0132%) | 0 -> 130 | 0 | 0.3972 pp / 0.0000 pp |
| Warm 28-day continuation | 8016/32084 | 24.9844% | 24.5137-25.4611% | 8012 (24.9719%; 24.5013-25.4486%) | 185 -> 181 | 0 | -0.0125 pp / 0.0000 pp |
| Traced 38 days combined | 10313/42122 | 24.4836% | 24.0754-24.8966% | 10494 (24.9133%; 24.5026-25.3287%) | 0 -> 181 | 0 | 0.4297 pp / 0.0000 pp |

Every audited epoch reconciles **draws = realised + closing pending - opening pending + drops**. Hand-ons and pass-ons transfer existing chances; they are not extra draws. [Full hourly shares and stock](rates-hourly.csv), [daily 24-hour changes](rates-daily.csv), [all causes and arm summaries](rate-summary.json), [untraced month](light25-summary.json). Native geometry sampling and weather can differ between reloads, so the 100 arm is an observed feasible output rather than a counterfactual paired to every default visit.

| Arm | Stops / eligible | Share | 95% interval | Hand-ons | Pass-ons | Drops: cap / no slot / trim / 100 policy | Hourly pending peak |
| --- | --- | --- | --- | --- | --- | --- | --- |
| rate0 | 0/9553 | 0.0000% | 0.0000-0.0402% | 0 | 0 | 0 / 0 / 0 / 0 | 0 |
| rate25 | 2297/10038 | 22.8830% | 22.0717-23.7151% | 1112 | 30 | 0 / 0 / 0 / 0 | 209 |
| rate100 | 5041/11092 | 45.4472% | 44.5223-46.3752% | 0 | 0 | 0 / 0 / 0 / 6051 | 638 |
| rate10 | 815/8087 | 10.0779% | 9.4406-10.7531% | 473 | 0 | 0 / 0 / 0 / 0 | 97 |
| rate50 | 3392/9829 | 34.5101% | 33.5765-35.4559% | 423 | 2525 | 0 / 599 / 0 / 0 | 951 |
| rate25-continued | 8016/32084 | 24.9844% | 24.5137-25.4611% | 3967 | 181 | 0 / 0 / 0 / 0 | 336 |

Pending stock fills in cold periods, drains when feasibility permits and then cycles with weekly routines and weather. These measured peaks do not imply a universal drain duration or ceiling. The default bound is one chance per resident, 957 here. The complete hourly record is retained through the warm continuation.

**Kind, venue and citizen distribution.** The pre-option-B reference has 98 direct scheduler records: 87 window-shopping, ten resting and one conversation-arrival record; it did not separately count that conversation's waiting partner. This campaign counts both members. [Per-venue counts and sources](rate-summary.json), [all citizens including zeros](stops-per-citizen.csv).

| Arm | Break | WindowShopping | Resting | Audience | Conversation | Conversation groups / members |
| --- | --- | --- | --- | --- | --- | --- |
| rate25 | 895 | 1228 | 114 | 0 | 60 | 30 / 60 |
| rate25-continued | 3778 | 3731 | 339 | 0 | 168 | 84 / 168 |
| rate100 | 1989 | 2723 | 129 | 0 | 200 | 100 / 200 |

| Window / lowest identities | Lowest group: stops/eligible, share | Rest: stops/eligible, share |
| --- | --- | --- |
| rate25 / 2% | 43/200 (21.5000%) | 2254/9838 (22.9112%) |
| rate25 / 5% | 111/497 (22.3340%) | 2186/9541 (22.9116%) |
| rate25-continued / 2% | 169/675 (25.0370%) | 7847/31409 (24.9833%) |
| rate25-continued / 5% | 402/1634 (24.6022%) | 7614/30450 (25.0049%) |

These default samples do not show a consistent excess for the lowest-identity groups. Ordinal donor/recipient/partner order remains a reviewed concentration risk in other conditions; it is not unaccounted chance loss.

**Settings and decreases.** Configuration was edited only while unloaded, then parsed and actually attached on native load. Numeric text `"75"` remains accepted as 75 by the pre-existing converter; nonnumeric text and fractional numbers fall back to 25. This exception to a blanket string-fallback description is explicit.

| Input | Expected | Parsed / attached | Result |
| --- | --- | --- | --- |
| 0 | 0 | 0 / 0 | PASS |
| 100 | 100 | 100 / 100 | PASS |
| -5 | 0 | 0 / 0 | PASS |
| 150 | 100 | 100 / 100 | PASS |
| 12.5 | 25 | 25 / 25 | PASS |
| "invalid" | 25 | 25 / 25 | PASS |
| "75" | 75 | 75 / 75 | PASS |
| missing | 25 | 25 / 25 | PASS |

| Decrease arm | Pending | Trim count | Daily realised % | Matched constant-rate daily % |
| --- | --- | --- | --- | --- |
| dec100-25 | 0 -> 85 | 0 | 18.6 / 50.7 / 18.5 / 25.7 / 17.5 / 22.7 / 46.2 / 36.0 / 19.3 / 24.3 | 31.0 / 24.7 / 23.5 / 26.5 / 16.9 / 22.3 / 23.7 / 100.0 / 29.7 / 25.5 |
| dec100-0-25 | 0 -> 200 | 0 | 18.6 / 25.8 / 17.3 / 23.1 / 43.8 / 26.7 / 26.2 / 25.0 / 24.2 / 16.7 | 23.5 / 26.5 / 16.9 / 22.3 / 23.7 / 100.0 / 29.7 / 25.5 / 23.9 / 19.1 |
| dec50-25 | 923 -> 98 | 0 | 33.0 / 63.3 / 52.6 / 29.2 / 17.1 / 20.8 / 24.7 / 28.6 / 26.4 / 25.6 | 31.0 / 24.7 / 23.5 / 26.5 / 16.9 / 22.3 / 23.7 / 100.0 / 29.7 / 25.5 |
| dec50-10 | 923 -> 16 | 541 | 28.7 / 56.0 / 10.8 / 10.2 / 7.6 / 6.2 / 13.0 / 10.6 / 10.3 / 9.4 | 8.4 / 10.5 / 8.9 / 10.1 / 8.2 / 33.3 / 8.3 / 10.9 / 10.1 / 9.0 |

Controls use the same checkpoint family and calendar dates; weather and unsaved geometry limit causal day-for-day matching. Retained in-bound stock after 50 -> 25 is expected and can elevate several daily shares. Only above-bound stock is trimmed; [full ordered trim IDs and controls](decrease-comparison.json). At 0 no chances are drawn, spent, transferred or trimmed. [High-stock zero save/reload proof](pending-zero-summary.json). Original config bytes were restored at cleanup.

**Exact financial gate.** [Authoritative gate (a)/(b)/(c)](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/2#issuecomment-6473698507). The identical PR #2 rerun disposable checkpoint is used: SHA-256 `bb8ac25721dcea825cdf40e462a606e6244fe93668c8940b9688f4ca482d9273`. Native croissant bits `1092881272` represent exactly **$10.25279998779296875 = 275221504000 units**. One unit is 2^-28 cents, stored in checked Int64. No tolerance, rounding, fixture-guard change, native price change or RLO change.

| Build | (a) exact payments | (b) native rational sum $ | Resident payments $ | Receipts $ | Wallet debit $ | Exact difference $ | Ledgers / buyers |
| --- | --- | --- | --- | --- | --- | --- | --- |
| before | 110/110 | 1127.8079986572265625 | 1127.8079986572265625 | 1127.8079986572265625 | 1127.8079986572265625 | 0 | 1914 / 53 |
| after | 110/110 | 1127.8079986572265625 | 1127.8079986572265625 | 1127.8079986572265625 | 1127.8079986572265625 | 0 | 1914 / 53 |

The 110-order sum is **30274365440000 units**. Prepared-food ledgers equal the same rational sum; exact ledger failures, final holds and observer faults are zero. One-unit payment, one-unit wallet and one-bit native-price negative controls fail as expected. [Before raw orders](before-financial.jsonl), [after raw orders](after-financial.jsonl), [before verification](before-financial-verification.json), [after verification](after-financial-verification.json), [all ledgers before](before-resident-ledgers.json) and [after](after-resident-ledgers.json), [negative controls](verifier-self-checks.json).

| Build | (c) native float32 revenue $ | Native revenue minus exact sum $ | Native cash accumulation residual $ | Cash-implied gross minus exact sum $ |
| --- | --- | --- | --- | --- |
| before | 1127.8079833984375 | -0.0000152587890625 | 0.000091552734375 | 0.0000762939453125 |
| after | 1127.8079833984375 | -0.0000152587890625 | 0.000091552734375 | 0.0000762939453125 |

These running-total differences come from the game's float32 accumulation and are not the resident reference. Optional-stop overlays themselves make **zero native food orders**: the owned food business is excluded by the scheduler. All 110 AFTER food payments were verified with option B active. No overlapping optional-stop/owned-food venue was found. [Place separation](optional-food-separation.json).

**Migrations, refusals and persistence.**

| Native input | Source -> migrated -> saved -> reloaded schema | Monetary fields | Exact amounts / identities / stored reload bytes |
| --- | --- | --- | --- |
| schema13 | 13 -> 16 -> 16 -> 16 | 34453 | PASS |
| schema14 | 14 -> 16 -> 16 -> 16 | 34456 | PASS |

Schema 13 is the whole-cent fixture. Schema 14 is the native save actually written by the unchanged BEFORE build; its original source hash and 110 historical receipts are preserved. Valid exact-unit and exact legacy-cent schema-15/16 cases migrate to 16. Non-exact legacy rounded cents, mixed field names, hexadecimal/exponent tokens and future schema 17 are refused and preserve original payload bytes through native save/reload. [Migration comparisons](migration-summary.json), [13 codec cases](native-codec-summary.json), [five native startup refusal cases](native-refusal-summary.json).

| Reload case | Identities / assignments | Existing-visit rerolls | Stop changes | Wallets / receipts | Stored bytes |
| --- | --- | --- | --- | --- | --- |
| after-qa-1 | 0 | 0 | 0 | equal | identical |
| after-qa-2 | 0 | 0 | 0 | equal | identical |
| after-qa-3 | 0 | 0 | 0 | equal | identical |
| after-finance-save | 0 | 0 | 0 | equal | identical |
| optional-dwell-control | 0 | 0 | 0 | equal | identical |
| event-hand-on-control | 0 | 0 | 0 | equal | identical |
| event-pass-on-control | 0 | 0 | 0 | equal | identical |
| event-conversation-control | 0 | 0 | 0 | equal | identical |

Native reload advances the clock slightly; any newly admitted visit is listed separately in [full persistence comparisons](persistence-summary.json), rather than treated as a reroll of an existing draw. Actual hand-on, pass-on and conversation events were observed and paused before native save/reload. [Event checkpoints](event-persistence-cases.json), [donor/pass-on and reciprocal pair checks](event-detail-verification.json). The conversation verifies two distinct members, reciprocal facing/membership, common start/end and safe spot separation.

Selected normal-speed visible stop: resident **pc:136**, **WindowShopping**. Identity, choice, stop geometry/deadlines, receipts and wallet counters survive reload. The native body approaches, dwells, returns to the entrance and continues the original visit/journey; linked checks are **PASS**. [Physical stage/body trace](optional-reload-summary.json), [native scene-camera image](screenshots/default-optional-dwell.png), [image source/hash](visual-source.json). This is bounded physical evidence, not an exhaustive city-wide completion or every-kind/cancellation census.

**Frame time and admission work.** Three BEFORE windows are reused at the identical code/DLL SHA; three AFTER windows are fresh. Same checkpoint, normal speed, ten-second warmup, 60-second sample, manual save/reload outside timing. Detailed optional traces/admission probes are off. [Method and pre-recorded noise rule](performance-method.json), [all quantiles/tails/environment](performance-summary.json), [spread comparison](performance-noise.json).

| Build/window | Seconds | Mod p50 / p95 ms | Whole-frame p50 / p95 ms | Whole-frame p99 / max ms | Save-clean |
| --- | --- | --- | --- | --- | --- |
| before/1 | 60.303 | 0.1697 / 0.2518 | 11.1785 / 14.4893 | 87.2917 / 103.8684 | True |
| after/1 | 60.175 | 0.3237 / 0.5325 | 11.2874 / 15.0391 | 120.6283 / 159.1545 | True |
| before/2 | 60.436 | 0.1753 / 0.2603 | 11.1129 / 14.4998 | 88.4675 / 108.4220 | True |
| after/2 | 60.355 | 0.3048 / 0.4949 | 11.2814 / 14.7252 | 114.4980 / 147.9840 | True |
| before/3 | 60.171 | 0.1813 / 0.3410 | 11.2823 / 16.6666 | 89.3566 / 286.0859 | True |
| after/3 | 60.324 | 0.3302 / 0.5398 | 11.3593 / 15.2212 | 120.8820 / 180.7445 | True |

**Fresh-process confirmation.** The original AFTER p99 values (114.498-120.882 ms) exceeded the historical BEFORE tail values (87.2917-89.3566 ms), despite passing the primary p50/p95 rule. External CPU activity was logged, but was not accepted as sufficient attribution. A second, predeclared three-window comparison per build used a fresh process for each build, BEFORE then AFTER, with identical checkpoint/load/warmup/measure/save/reload steps and no finance replay before either first window. Every original and confirmation sample is retained. [Plan](performance-confirmation-plan.json), [all fresh windows](performance-confirmation-summary.json), [all five metrics](performance-confirmation-noise.json), [contextual CPU evidence](performance-interference.json).

| Fresh build/window | Mod p50 / p95 ms | Whole-frame p50 / p95 ms | Whole-frame p99 / max ms | Save-clean |
| --- | --- | --- | --- | --- |
| before/1 | 0.1554 / 0.2187 | 11.0886 / 13.6349 | 83.8218 / 102.4965 | True |
| before/2 | 0.1607 / 0.2248 | 11.1102 / 13.2759 | 87.0505 / 102.5880 | True |
| before/3 | 0.1626 / 0.2277 | 11.1010 / 13.1863 | 87.8211 / 113.4618 | True |
| after/1 | 0.2241 / 0.2938 | 11.0913 / 13.5034 | 84.4983 / 94.1484 | True |
| after/2 | 0.2311 / 0.2970 | 11.0667 / 13.3823 | 86.7419 / 108.8378 | True |
| after/3 | 0.2319 / 0.2994 | 11.0979 / 13.3533 | 86.9268 / 100.0768 | True |

Fresh whole-frame median changes are **-0.0097 / +0.1064 ms p50/p95**, and **-0.3086 ms p99**. None of p50, p95, p99, mean or maximum worsens beyond the larger within-build spread. The earlier tail shift is not reproduced in this controlled fresh-process comparison; its unique cause is not established. Fresh mod-work p50/p95 increase **+0.0704 / +0.0722 ms**, retained as an optimisation issue. [Confirmation result](performance-confirmation-result.json), [autosave checks](autosave-confirmation-evidence.json).

Primary p50/p95 whole-frame median changes: **+0.1089 ms p50 / +0.5393 ms p95**, within the recorded spread rule. This descriptive comparison is not an equivalence test; baseline reuse and uncontrolled system work limit attribution. Mod-work changes: **+0.1484 / +0.2722 ms**, retained as an open optimisation issue, not a failure under the owner criterion. The previous option-B campaign reported +0.0832/+0.1837 ms; the pre-option-B reference reported +0.076/+0.085 ms. Different native workload and system noise prevent attributing the difference between campaigns solely to the nullable repair.

| Setting | Admissions | Scheduling p50 / p95 ms | Spots calls | Candidate spots | Occupancy calls | Occupied | Facade deferrals |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 25 | 1315 | 0.07740 / 1.11860 | 3290 | 1111 | 289 | 161 | 257 |
| 100 | 1315 | 0.08010 / 1.60540 | 3489 | 1534 | 382 | 198 | 269 |

Admission probes use separate 48-hour catch-up workloads with detailed event logging off; they include small counting-hook overhead and refiling inside scheduling. Outer event refiling and attach/rebuild are not separately isolated. These skips/recovery saves are outside clean frame windows. An extra autosave-off 100% frame window is exploratory. [Admission metrics](admission-metrics.json), [100% frame window](after100-window-1-stop.json).

**Autosave, native QA and cleanup.** Original autosave was enabled: `preventAutoSave=false`, five minutes, three retained saves. Every clean frame window verifies autosave off before/after, no save in progress, unchanged save size/mtime and no actual save-start/completion lines; it is disabled again after each reload. Manual saves are outside timing. Native time-machine recovery saves in logical catch-up arms are not clean performance samples. [Autosave records](autosave-evidence.json).

Native primary logs have 132 active patch lines and the same four pre-existing inactive CityPerformance capabilities; Persistent City exception count is zero. The functional helper's pre-existing `Respond` file-already-exists exception occurs on reload outside the measured windows (primary before 7, after 8); it is retained, not attributed to production or removed from the logs. All completed finance/rate observers report no faults. [QA inventory](native-qa-summary.json), [raw-log hashes](native-log-provenance.json).

Cleanup restores and hash-verifies **4828 original profile files**, **107 installation references**, **287 ModsLocal files**, original root files, config and preferences. Config SHA-256 `abfecb215a3c3ea89e6a1137c7445726dde66947c06d361adb2be1337cf78c66`; preferences SHA-256 `66fccbcc5f63bc492e36ba7f342d9d2480adb2b87404a650a8607094d207381d`. Original autosave restoration is established by complete original profile/registry hashes, without relaunching the restored game. Rig stopped; lease held=False, pending=0. [Backup](backup-receipt.json), [restore](restore-receipt.json), [release](lock-released.json).

**Production DLL hashes.**

| Build / tested SHA | DLL SHA-256 |
| --- | --- |
| before / `7357f78ca13edd5200e7e708950ffbc7a6ade1fe` | `54aa9dc8be97f0a3c89b629b7bc06813de0dad61e515ecd6d02d1f5cee470f54` |
| after / `7a104d6f41d7c92579dcdf2e790274856d31cb29` | `f7f0c36e42dcd2b66067245602f27622a2020c2df5670ecf636e9b9525ca471c` |

Reproduction: decompress offline-output.txt.gz for complete command logs; offline-results.json records each command and exit. Run verify_financial.py on each *-financial.jsonl, verify_resident_ledgers.py verify on each *-resident-ledgers.json, verify_verifiers.py, and verify_rate_audit.py. Native scripts require private FT inputs, local referenced game assemblies, an agent1 lease and a fresh verified backup. Script paths are sanitized placeholders. No raw save, game DLL, registry export or lease credential is included. file-manifest.json hashes the final public files.

**Open issues / limits:** increased mod work; ordinal identity concentration; retained stock after 50 -> 25; high-setting capacity/recipient drops; weather/geometry and system-workload variation between native arms; scheduled counts versus bounded physical coverage; outer-refile/attach cost not isolated. These remain explicit and do not grant merge, release or ordinary-save clearance.

**Measurement notes.** The first hand-on observation used the high-stock50 checkpoint and found no hand-on in four normal-speed game hours; it remains **INCOMPLETE** in [initial attempts](event-persistence-initial-attempts.json). Its many carried stops and pass-ons are retained in the arm summary. A separate default25 run observed an actual hand-on and completed native save/reload; only that successful case fulfills the hand-on checkpoint. No draw, arrival, weather or safety rule was injected.

One initial analyzer classification was corrected with exact chronology: c117 departs at minute 22067.119422455427, between snapshots 22067.03840637207 and 22067.371738433838. Its old declined false flag clears to null as it changes from AtPlace to Travelling and starts the saved departure. No draw, transfer or stop occurs in the post-load epoch; stored payload bytes stay identical. This is a new journey, not a rerolled existing-visit choice. [Chronology and counters](persistence-chronology.json), [tooling versions and corrections](tooling-notes.json).

The two 100-origin decrease arms inherited zero pending at this campaign's selected checkpoint; they do not establish nonzero waiting-stock behavior by themselves. Nonzero preservation at zero is exercised separately with 923 chances, and both 50-origin decrease arms exercise inherited stock. All config values and safety rules remain unchanged outside the explicit test arms.
