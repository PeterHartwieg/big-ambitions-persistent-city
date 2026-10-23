# Isolated native food pilot

This is a diagnostic integration harness for the owned Windows `agent1` rig. It is not part of the normal mod package. Read the sibling Driven Rides `tools/agent-tests/README.md`, the Windows connection memory and this repository's AGENTS.md before use.

The dated scripts and evidence paths belong to the 22 October campaign. For another campaign, choose new local/remote directories and a unique owner; preserve previous evidence. Acquire the rig lock before any mutation. `pc.py` creates and verifies a complete backup of the rig profile, installation manifest and preferences. Never replace another owner's lock, update a running game or address the normal game profile.

## Scope

The read-only discovery confirms actual assigned-home district composition and reports rival inventory as unknown. The explicit `setup-pilot` operation requires a disposable `FT Food Native Pilot …` city on the test executable/profile. It selects an unfurnished native full-service food shop near homes, converts that isolated fixture to player ownership, adds a native register and stocked bakery showcase, and hires/schedules a native employee. Its 300 pastries and 300 bags are fixture inputs, not inferred rival stock. Capacity comes from native furniture/staff/building rules.

The helper suppresses native hourly simulation and customer draws for the selected shop only. Once per day for three days, from 10:00, it considers nearby residents who are actually at home and have enough time before their next scheduled event. Opportunity timing is controlled; this is not a natural citywide demand or profitability measurement. Individual budgets use observed home-district mixes; the existing food model can decline a purchase. Native walking quotes are limited to one candidate's outward/return pair per frame.

An accepted choice uses `CitySimulator.TryBeginOptionalShopVisit` to replace that resident's idle-home interval with one actual scheduled walking outing. After admission and service time, stock, staff, hours and affordability are checked again. The helper invokes native retail `ProcessCustomer`, scoped so the chosen resident's approved price replaces the random native customer's price veto. Native methods consume pastry/bag stock and create the completed order. The game's normal daily settlement books revenue. A receipt checks one product, one bag and one completed order. The resident resumes their normal scheduler afterward.

No household money is debited: temporary allowances remain the current policy. The helper has no durable commerce recovery and does not arm itself after load. It must not be installed for normal play. Offline population save/resume checks cover the optional journey; they do not establish native transaction recovery across reloads.

## Campaign workflow

1. Acquire `agent1` as `persistent-city-food-pilot-20261022`, verify it is stopped, then run `python3 tools/food-native-pilot/pc.py`.
2. Run `prepare.py` to collect pinned PC references and freeze/build the candidate; run the unit tests and `build_probe.py`. Check the actual source and binary manifests before installation.
3. `session.py start` installs only the candidate/helper in the backed-up rig and starts it. `session.py call new_game '{"name":"FT Food Native Pilot …"}'` creates the disposable city.
4. `run_campaign.py` waits for readiness, records home/supply discovery, sets up the fixture and requires positive native availability. It records population routines, checks native stock/bag/staff/closure behavior at day 2 09:00, then runs through day 5. Native fast-forward uses bridge multiplier 1. Each live transaction window advances in ten-minute checkpoints, pausing for the population scheduler to catch up; this does not establish sustained fast-forward performance. It collects purchase/return/settlement evidence and a separate paused three-day shadow price sweep.
5. Inspect `campaign.log`, `pilot-final.json`, `supply-checks.json`, `bank-final.json` and the comparison result. The campaign pauses on failure so the owned instance can be inspected; failure is not completion.
6. Always run `session.py restore` when finished. It stops the rig, preserves probe/log evidence, restores and verifies every preexisting profile file, restores preferences and the installation manifest, removes the helper and releases ownership. Verify the stopped/unheld state separately.

All raw native captures, native references, saves and decompilation remain under ignored `build/food-native-pilot-20261022/` or the private PC rollback directory. Findings belong in the dated review note.

After collecting final evidence, run `verify.py build/food-native-pilot-20261022` for native receipt/return/settlement conservation and `dotnet run --project tools/food-native-pilot/Replay.csproj -- build/food-native-pilot-20261022` for the separate 31-point offline shadow replay. It retains the original input and writes bounded outcome reports. If a complete native sweep exists, counts/revenue are also compared against it. `plot.py` needs Matplotlib and reads `offline-comparison.json`, labeling those curves separately from actual native receipts. The paused route diagnostic processes at most eight walking queries per frame with a soft two-ms slice; a single native call can exceed that budget.

The completed r6 native connection passed, but its full shadow baseline result exceeded the codec's document limit. Its tested binary/source are preserved separately under `Probe-r6-tested/`. The source now writes compact baseline outcomes; that reporting correction has compilation and offline verification, not a repeated PC sweep. The size limit was not raised. See the dated review for the exact evidence boundary.
