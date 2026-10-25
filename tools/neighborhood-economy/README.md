# Neighborhood economy native checks

These tools use the isolated Mac rig only. Follow the sibling Driven Rides `tools/agent-tests/MAC.md` ownership procedure. Do not point them at a normal game/profile. Each campaign needs a **new** ignored directory under `build/`, a frozen production source/package under `source/`, and a probe compiled against that exact package using `tools/food-demand-audit/build_probe.py`. The runner validates the DLL/reference hashes before installation and refuses a busy rig or an existing backup/helper.

`mac_rig.py` acquires ownership, backs up the profile/preferences/manifest, installs the frozen candidate, and restores and verifies preexisting files before releasing the stopped rig. New named `FT Food Routines …` test checkpoints may remain. Do not reuse an interrupted campaign directory or assume that a failed command has released ownership; inspect its cleanup receipt and live rig status.

## Market and skip comparisons

Run `PC_FOOD_CAMPAIGN=<new-directory-name> python3 tools/neighborhood-economy/run_mac.py`. Optional `PC_CASES` selects comma-separated cases and `PC_RIG_MODE=background` enables the Metal background player. The existing dated FT source checkpoint is a prerequisite; the script does not create a substitute city or contact Windows. The fixture helper validates independent employee schedules and counter assignments before advancing time.

Use `verify.py <campaign-directory>` for cash, native stock, revenue, daily statements, settled reload and paid-receipt replay checks. `buyers.py` compares resident identities across location cases; `plot.py` produces the market figure. `verify_skip.py` compares the complete resident accounts and native statements from an original-budget campaign and a faster-skip campaign, rather than checking sales totals alone. Retain any combined-case provenance explicitly; failed fixture attempts are not comparison results.

## Rendered comparison

`PC_FOOD_CAMPAIGN=<new-directory-name> python3 tools/neighborhood-economy/render_mac.py` uses a normal windowed 1920×1080 player, the configured 48-pedestrian/24-car limits and a candidate/control/candidate sequence. The native-population control retains the one-third calendar. Each sample follows a 30-second warm-up and records 180 seconds of whole-frame intervals and complete population-update time. It also checks a native skip, fast-forward/resume, live opening-hour refresh, management screenshot and save/reload. Avoid competing heavy workloads during the samples.

`verify_render.py <campaign-directory>` verifies matching environment/focus and reports the comparison. Unavailable or all-zero allocation counters are **not** zero allocations. The body limits are limits, not a claim that every sample saturates them. Short Mac samples do not establish Windows performance or enjoyable gameplay.

`PC_RENDER_RESUME=1` is a recovery path for a new campaign that already contains a verified candidate-A sample and catch-up records, with explicit provenance and the matching retained daylight checkpoint. It starts at the disabled-population control. Do not use it to combine unrelated builds, checkpoints or graphics settings.

`control_mac.py` repeats only an invalid control in a new campaign, checks focus at every observation and aborts if focus changes. `panel_mac.py` checks the final management display and a paused save/reload, recording route-discovery timings and account conservation. Retain the exact DLL/source delta when these follow presentation or initialization fixes after a completed steady-state benchmark.

Published interpretations belong in `docs/reviews/2026-10-23-neighborhood-economy.md`. Raw saves, game assemblies, logs and private fixture evidence remain ignored under `build/`.
