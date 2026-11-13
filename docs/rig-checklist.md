# Rig checklist

In-game checks waiting for the next batched rig run. The run does not gate a merge ([delivery rules](decisions/2026-11-06-delivery-rules.md#in-game-checks-are-batched)). A slice lists its checks in its PR description; the coordinator copies them here after merge and removes them once a rig run has passed them.

## Blocked until the rig can make a sale

On 9 November the rig's test shop refused service with and without the mod, so no sales, receipt or accounting check can run. Fixing that fixture is the first row of the [roadmap](roadmap.md#prove-a-sale).

## Backlog from before 10 November

The checks written by slices merged before 10 November are in the history tag, one section per slice under "In-game checks for the next batched rig": [docs/checks](https://github.com/PeterHartwieg/big-ambitions-persistent-city/tree/docs-history-2026-11-10/docs/checks). The [9 November intermediate run](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/170) catalogued 510 of them: 6 passed, 64 partial, 5 failed, 435 not run. Many test save migration, which no longer exists. The next batch starts by dropping those and carrying the rest into this file.

## Checks

None added since 10 November.

- 13 November 2026: [Mac results — GR-4, GR-6/7/8, GR-9 and RLO #239 at a3a6c8db](checks/rig-2026-11-13-mac-results.md) — 2 passed / 6 partial / 1 failed / 11 not run; BLOCKED.
