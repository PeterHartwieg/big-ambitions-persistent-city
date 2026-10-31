# VERDICT: PASS

Role: `rig_test`. Separate authorized native red-to-green proof for [PR #11](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/11). Candidate **`1c242412d9670428719d5eedea7fbb9761d917f2`**, control **`ee9486b17db082aff1f764251568fb4670ffca2a`**. Fetch and PR inspection confirmed the requested candidate head was unchanged. Evidence base: `origin/codex/persistent-city` at `7a344a7`. No merge, PR comment/approval, issue or production change was made.

All five requested gating checks pass within the locked-session seam scope. Actual pointer gestures remain deferred. This is address-fix evidence, not release or ordinary-save deployment clearance.

| Check | Result | Evidence |
| --- | --- | --- |
| 1. Baseline red, candidate green; at least five same residents, worker/student/food traveller/meal line; exact native formatting | PASS | [Full control strings](control-inspector-summary.json), [full candidate strings](candidate-inspector-summary.json), [string comparisons](content-comparison.json). Every displayed address matches an independently called `Streets.AddressHelper.ToFormattedString` result, including number/street order. No raw localization key occurs in candidate display strings. |
| 2. Identities, PlaceIds, map-link arguments and saved payload values | PASS | [Comparison](content-comparison.json), [payload hashes and load details](payload-comparison.json). Raw Home/Work Street, Number, PlaceId and CanNavigate remain identical. Complete source saved payload bytes are identical between control save/candidate load. Each frozen inspector capture leaves its complete live serialized payload unchanged. |
| 3. Candidate manual save/reload | PASS | [Full reloaded strings](candidate-reloaded-inspector-summary.json), [comparisons](content-comparison.json), [payload evidence](payload-comparison.json). All displayed fields are identical, source saved bytes match, autosave off is reverified after each load. |
| 4. Interleaved >=60s base/candidate/base/candidate + candidate popup open | PASS | [All distributions, load, clocks, validity, attribution and raw files](performance-summary.json). Every measured mod-work p95 <5 ms; >=30 ms spikes: **0**. |
| 5. Persistent City errors/exceptions | PASS | [Native log checks](log-checks.json). No Persistent City error/exception or address fallback warning; baseline missing-street localization warnings reproduce the defect. Native outdoor/culling NullReferenceException groups occur in both builds (base1/cand1/base2/cand2: 2/6/6/4), with no Persistent City or observer frame in their stacks; recorded separately. |
| Hover, pointer movement, map-link click and other gesture checks | DEFERRED-INPUT | Locked session cannot supply those inputs; Peter's playtest. Every popup operation here is **seam, not gesture proof**. |

| Field | Control red | Candidate green |
| --- | --- | --- |
| c0 homeLabel | ba:street_fourthavenue 49  ↗ | 49 4th Avenue  ↗ |
| c0 workLabel | Joko Tech, ba:street_fifthavenue 21  ↗ | Joko Tech, 21 5th Avenue  ↗ |
| c102 workLabel | Manhattan Business School, ba:street_fourthavenue 7  ↗ | Manhattan Business School, 7 4th Avenue  ↗ |
| c149 Lives at | Vera Thorne, 50, is a parent. Lives at ba:street_thirdstreet 38. Works at Eagle Eye Law, ba:street_sixthavenue 22. Right now: walking to Persistent City food routines, ba:street_thirdstreet 30. | Vera Thorne, 50, is a parent. Lives at 38 3rd Street. Works at Eagle Eye Law, 22 6th Avenue. Right now: walking to Persistent City food routines, 30 3rd Street. |
| c149 Right now: | Vera Thorne, 50, is a parent. Lives at ba:street_thirdstreet 38. Works at Eagle Eye Law, ba:street_sixthavenue 22. Right now: walking to Persistent City food routines, ba:street_thirdstreet 30. | Vera Thorne, 50, is a parent. Lives at 38 3rd Street. Works at Eagle Eye Law, 22 6th Avenue. Right now: walking to Persistent City food routines, 30 3rd Street. |
| c149 Heading to | Heading to Persistent City food routines, ba:street_thirdstreet 30, arriving about 08:40 (to buy croissant for breakfast) | Heading to Persistent City food routines, 30 3rd Street, arriving about 08:40 (to buy croissant for breakfast) |
| c149 Breakfast: | Breakfast: on the way to Persistent City food routines, ba:street_thirdstreet 30 for croissant | Breakfast: on the way to Persistent City food routines, 30 3rd Street for croissant |

Five residents: `c0, c102, c149, c1, c10`. Student `c102` studies at Manhattan Business School. Food traveller `c149` has a real existing outward food journey and meal line; no itinerary or meal was manufactured. Full JSON includes all three meal lines and every story/destination field. [Control capture](control-popup.jpg) and [candidate capture](candidate-popup.jpg) were visually inspected; both render the popup in the locked game. The JSON covers all five residents; screenshots show the last selected resident.

All content captures show day 5 **08:40**. The [matched control reload](control-reloaded-inspector-summary.json) and candidate capture are at exactly **7720.349502563477 native minutes**, with **all 957 citizens' complete live values identical**: [matched reload comparison](matched-reload-comparison.json). The initial pre-save control capture and later candidate roundtrip have native minutes 7720.016170502, 7720.349502563, 7720.682834625. Native loading advances about one third of a game minute; displayed strings remain exactly identical after the native address substitutions and after candidate reload. Two live food-traveller timing doubles differ by approximately `1.6e-7` minutes between the initial pre-save control state and its reconstruction, equally in both builds. They are recorded explicitly; all original persisted values remain byte-identical. This evidence does not claim identical whole traffic runtime state across native startup.

All timing values below are **p50 / p95 / max, milliseconds**. Mod work = Update/authority union plus same-frame whole PopulationRuntime.OnGUI; nested work is not added twice. Existing input guard callbacks outside those scopes are not separately measured. Whole-frame intervals and stalls are informational, separate from the mod gate.

| Window (in execution order) | Seconds | Mod work ms | Whole frame ms | CPU before/after | Load wait / label |
| --- | --- | --- | --- | --- | --- |
| base1 | 60.112 | 0.351 / 0.638 / 14.060 | 12.339 / 17.311 / 66.647 | 72.7/29.3% | 390.1s; dotnet present |
| cand1 | 60.038 | 0.271 / 0.430 / 9.776 | 11.243 / 13.072 / 52.588 | 33.0/76.5% | 0.0s; other system load |
| base2 | 60.131 | 0.298 / 0.435 / 7.364 | 11.142 / 13.416 / 51.886 | 33.2/32.1% | 0.0s; other system load |
| cand2 | 60.212 | 0.382 / 0.678 / 11.689 | 12.300 / 17.231 / 65.477 | 68.1/47.9% | 0.0s; dotnet present |
| cand2-open | 60.230 | 0.339 / 0.443 / 5.169 | 11.160 / 12.841 / 29.236 | 20.7/24.8% | 330.1s; dotnet present |

Whole-frame stalls **>50 / >100 / >250 ms**: base1 1/0/0; cand1 1/0/0; base2 1/0/0; cand2 3/0/0; cand2-open 0/0/0. These remain separate from mod-work spikes; every mod spike >=30 ms has its window/frame/phase recorded in the performance JSON.

Identical retained morning FT save bytes, settings and saved clock were used for every measurement, with normal speed and an unpaused warm-up. Exact measured start clocks are retained in the performance JSON; transport/warm-up edges differ by fractions of a game minute, so these are near-matched windows, not frame-synchronized simulation replays. No speedup or causal whole-frame performance claim is made. The open window reloads the same morning fixture, opens the popup through its seam and lets the normal refresh cadence run. No extra perf-only rerun occurred.

Before every window the driver checks all dotnet/testhost processes and waits up to 15 minutes for them to finish; it never controls them. CPU, top-process CPU-delta rankings, dotnet/testhost/codex counts and available RAM are recorded before/after every window. All windows coexist with other system work; dotnet-present edges are explicitly labelled above. Top-process deltas sum across cores and are not machine-percent samples. All five brackets verify autosave off, no saving in progress, unchanged save files and no native save activity. Manual saves/reloads are outside the brackets. All attribution error/drop counters are zero.

Closed candidate windows record **zero inspector refreshes and zero inspector formatter calls**. The popup-open window records **62 refreshes / 248 formatter calls**, **0 calls in Draw**, **0 attributed calls outside refresh**. Candidate source diff confirms its formatter delegate is referenced only at popup build/refresh; Update/Draw/picking code is unchanged. The observer counts native formatting while inside inspector Refresh/Draw, excluding independent native comparison calls and unrelated game use. The runtime's thread-allocation counter is unavailable, so its zero byte field is **not an allocation measurement**. Zero closed formatting calls plus the unchanged closed Update/Draw source establish no new per-frame allocation attributable to address formatting. Open formatting allocations occur at refresh only. [Open popup capture](candidate-open-perf.jpg).

| Build | DLL SHA-256 |
| --- | --- |
| Control ee9486b | `4cb1181f46ec5536ed9922799601c27379c296ddece39b278c4e4f84222b414c` |
| Candidate 1c24241 | `d6de34e7c6a09d86d9ba5aa90ff3e5633ba4807a61d5a86c5240c39cdb13c0b9` |

Both unchanged detached worktrees built successfully with the agent1 Managed references and the per-user dotnet directory prefixed on PATH. **No offline dotnet test suite ran.** [Build/loaded-assembly hashes, helper hashes and fixture hashes](tested-builds.json) establish what each of the four owned game processes actually loaded. Unchanged RLO, bridge-only host and read-only observers were used; Workshop disabled, normal visible 1920x1080 Low D3D11 window. This is not a full-modpack test. Existing locked feasibility and A1/A3 tooling was copied/adapted only in the private stage; no game, production or RLO binary was patched. Two helper compilation member-name corrections occurred before launch; one content-only pause-guard retry is recorded separately from mod defects.

**Cleanup: PASS — restored, verified, released.** All four owned games quit normally, no kill. Original profile **4828/4828** hashes and **107/107** install references match. Original ModsLocal, configuration, saves, game preferences/autosave, root receipt/run/native log and preference-export hashes match the backup. Original autosave was enabled (five minutes, three slots); restored game preference bytes verify restoration. [Cleanup, backup/root hashes, restore and lease receipts](cleanup-summary.json). Lease is released (`held=false`, `pending=0`), no owned game/build/helper process remains. [Final read-only locked-session check](session-final.json) confirms session 11 stayed locked.

Private saves, full logs, original backups and the lease token remain outside Git. Public artifacts contain no personal profile paths or credentials. No Windows setting, session connection, ordinary save/profile, Steam/Workshop, RLO source, other harness/worktree or unowned process was changed.

Deferred to Peter: actual walker hover/movement and highlighting, real popup-open click, map-link click routing/focus, HUD exclusion and pointer click/hold/release/wheel/drag safety. These are **DEFERRED-INPUT**, with no assumed pass or defect.

Evidence integrity: `python verify_evidence.py` checks the manifest, decompresses the raw CSVs and recomputes the five window distributions and >=30 ms spike records. Raw native saves/logs and dependencies are not included.
