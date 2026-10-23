# Calendar pace comparison

`run_mac.py` owns, backs up and restores the isolated Mac rig. It never contacts the PC. Read the sibling Driven Rides `tools/agent-tests/MAC.md` first. It requires the frozen candidate and helper under ignored `build/clock-pace-mac-20261022/`; the helper is built by `tools/food-demand-audit/build_probe.py` with `PC_FOOD_CAMPAIGN=clock-pace-mac-20261022` and explicit native Mac assembly references.

The runner creates one disposable `FT Food Routines` city, configures a finite-stock single-product shop, lets its first day settle, and saves a checkpoint. Both runs reload that checkpoint, with the same DLL and clock factors 1 and 1/3. Each observes one full game day using native 6× fast-forward, with physical population bodies disabled. All sales still pass through the actual native stock and settlement code. The optional helper times population advancement and captures decisions; queue and lag peaks are samples every 0.2 real seconds. This is not a rendered FPS benchmark.

`verify.py` checks clock rates, matched inputs, inventory/customer conservation, settlement, returns, saved reload and duplicate-sale prevention. The runner's `finally` block stops the game, retains evidence, restores verified preexisting profile files, preferences and install manifest, and releases ownership. New disposable saves remain. Existing evidence directories refuse reuse.

The completed comparison uses `PC_FOOD_SOURCE=source-r3` and output `build/clock-pace-mac-20261022/mac-run-r3`. `--checkpoint` reuses the retained disposable baseline. `--ride-only` performs the separate native taxi compatibility check from the retained settled fixture, using an observed active taxi stand and a bounded recording window. Its output is `build/clock-pace-mac-20261022/ride-check`. All modes still acquire ownership and restore the rig in `finally`.

Verify the retained paired evidence without launching the game:

```sh
python3 tools/clock-pace/verify.py build/clock-pace-mac-20261022/mac-run-r3
```
