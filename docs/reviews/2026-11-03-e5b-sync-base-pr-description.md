# Sync E5b with the E5e metadata API base

Target: `codex/pc-e5b-complete-menu`. This branch merges `00506ed` from `codex/persistent-city` into E5b `241e648` with a real two-parent merge; land this sync with a merge commit to retain ancestry.

Conflicts and resolution:

- `CHECKS.md`: retain both complete E5b and E5e index lines and their evidence links.
- `docs/roadmap.md`, Step E: retain E5b's partial/native-incomplete status and E5e's API/precedence status.
- `docs/roadmap.md`, perf wins: retain both slice entries and original measurement qualifications.

README automatically retains both descriptions. Both slice checks, historical receipts, regression tools and rig-proof tooling are retained. E5b's six In-game checks and LOCKED-PC/input-only automation qualifications are preserved; the shared 6 November checklist also includes E5e's three numbered handoffs from its owning checks file.

Minimal code integration fix: **both E5b and E5e Core code changed**. Eight new combined-source cases first failed because E5b's `CompleteFoodMenu.Check` queried only config overrides, bypassing API class metadata. It now calls `FoodProductMetadataResolver.ResolveMealClass`, which shares the full resolver's config > API > defaults expression without computing resident tastes. Coverage includes both Street Food products, all four excluded classes, taste-only config fallback, explicit config override, immutable API publication/removal and independent stock/native refusal. All eight pass after the fix. No additional Runtime behavior was implemented; dynamic native authority and held execution remain refused.

Schema remains **25**. No persistence source, historical migration reader or save-contract regression was changed. Unknown products remain Food by default with existing seeded taste; bundles stay deferred and cart/truck sales remain later roadmap scope.

Validation: full merged-tree local matrix pending; results will be added before handoff. No game or rig was launched. In-game checks remain in the batched combined locked-PC session under merge rule v2; this integration does not claim rig proof or release approval.
