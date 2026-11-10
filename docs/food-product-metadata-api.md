# Product metadata API, revision 1

E5e implements optional metadata integration in `PersistentCity.Runtime.FoodProductMetadataApi` (packaged `PersistentCity.dll`). Products need no PC reference to receive defaults. This API supplies policy only: it does not register game items, advertise products, authorize ownership or checkout, or write native state. Only a currently loaded exact canonical name can contribute API metadata. The complete shop contract remains independent.

```csharp
using PersistentCity.Core;
using PersistentCity.Runtime;

// Keep this handle for the provider's lifetime; do not acquire one per decision.
FoodProductMetadataProvider provider;
if (FoodProductMetadataApi.TryCreateProvider("example:food-metadata", out provider, out string reason))
{
    bool accepted = provider.TryRegisterProductMetadata(
        "foodtrucks:itemname_dumplings",
        new FoodProductMetadataInput { MealClass = MealClass.Food, LunchFitCents = 100 },
        FoodProductMetadataApi.Version, out reason);
    // Check accepted/reason. This example neither creates dumplings nor proves its shop holder.
}
// At provider unload: provider.Dispose(); unregisters all this provider's claims.
```

The second modded example identity is `foodtrucks:itemname_choppedcheese`. Neither example establishes native eligibility. Street Food need not implement this API.

All five input fields are optional nullable scalars: `MealClass` (`Unknown`, `Food`, `Drink`, `Bundle`, `NonFood`), `TasteBand` (integer 0–4) and `BreakfastFitCents`, `LunchFitCents`, `DinnerFitCents` (integer 0–5,000). Null is absence, including when replacing a previous nonnull field. API values outside these bounds reject the whole claim, without clamping or losing previous valid data. Inputs are copied; callers must not mutate the DTO concurrently with the registration call. Later DTO edits have no effect.

API names and provider IDs use exact ordinal identity, 1–256 UTF-16 code units, nonwhitespace, without controls, invalid surrogates, backslash or `|`. There is no trimming, aliasing, case folding, name inference or product allow-list. A registry accepts at most 256 distinct product names and 256 active provider IDs. Duplicate active provider IDs reject; an existing handle is the sole way to replace its own claims. The registry separately caps escaped-character and UTF-8 budgets at 524,288, including bookkeeping. Each unique string charges its escaped/UTF-8 spelling plus 64 units of structural/lifecycle/conflict reserve; each provider/product claim charges another 128 units for references and optional scalar fields. Registration checks incremental charges before adding data. Bounds reject atomically; they never truncate a registry. Managed retained bytes are measured separately from this conservative wire budget. The shared E5c config/taste/capture seam retains E5d canonical-name rules, including backslashes; the stricter backslash check is local to API inputs.

Supported calls return `accepted`. Stable rejection reasons are `invalid-provider-id`, `provider-already-registered`, `provider-bound`, `provider-unregistered`, `incompatible-api-version`, `invalid-product-name`, `invalid-metadata`, `product-bound`, `registry-budget` and `conflicting-providers`. An incompatible version, invalid data or overbound claim never changes the staged registration or published image. Identical same-provider registration is idempotent; changed valid values replace its entire previous optional-field set atomically.

`conflicting-providers` is deliberately different from validation rejection: the second valid, within-budget claim is retained as conflict bookkeeping and **both providers' API values become unusable at the next publication**. This includes equal claims by different providers, avoiding implicit shared ownership. Every subsequent within-budget claimant gets the same reason, independent of registration order. Config fields still win; missing config fields use defaults while API metadata is conflicted. Disposing a provider removes all its claims; a conflict can resolve only in the next coherent publication. Dispose is idempotent. A disposed or unload-invalidated handle cannot register again; reacquire a new handle after load.

Provider registration/disposal may stage from worker threads or before PC initialization. PC binds publication to its host main thread during initialization. At loaded-catalogue publication, E5b supplies `new FoodProductMetadataLoadedNames(completeLoadedNames)`: this copies an ordinal immutable set, accepts at most 16,384 valid unique names and rejects invalid/duplicate/overbound input atomically. It enumerates at most one extra input solely to detect overflow. Construct and reuse this image at catalogue changes, never per frame or context; pass null when loaded evidence is unavailable/dirty. This host image confers no sale permission and neither invokes nor activates E5a's builder/reader.

`PopulationRuntime.PublishFoodProductMetadata(loadedNames)` composes the config resolver with the registry image. Null/missing names supply no API metadata. Replacing the loaded-name image admits/removes metadata only for names actually present; numeric revisions are unnecessary and cannot hide a changed image. Registry publication examines at most 256 distinct names and the bounded retained claims, with no catalogue enumeration. Clean registry publication reuses its image and does no scanning/allocation. Constructing a new resolver wrapper at a context boundary is separate. No frame participant or provider callback exists.

E5b owns calling this seam at each **new uncaptured, uncommitted context**, and composing class changes with its whole-business boundary. It must not resolve metadata again for a held offer or a committed journey. `resolver.Resolve(...)` returns effective immutable scalar inputs; `resolver.Capture(...)` copies them into E5d's existing revision-2 `FoodOfferMetadata` tuple. `Sources(name)` gives each field's Config/Api/Default/Original source; `ApiReason(name)` gives `accepted`, `conflicting-providers`, `unloaded-product` or `not-registered` for explicit diagnostics. These reads call no provider code and have no native authority. A published resolver remains immutable across replacement/removal/unload. Runtime shutdown clears all registry state and invalidates handles; the saved held tuple remains independent.

Precedence is per field: valid per-product config > API > defaults. Missing/rejected config fields fall through; unknown or duplicate config properties reject that whole entry, also falling through. Numeric config endpoint clamping remains explicit config. Config round trips preserve absent fields rather than materializing defaults over API values. Defaults are Food, deterministic seeded per-resident taste, and fits 900/100/450. The original three products always retain their saved traits and established class/fits. Bundle metadata does not implement bundles. No schema or native admission/checkout change belongs to E5e.

[Offline proof and pending automated rig checks](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e5e-metadata-api.md). No product or real second-mod integration is claimed as verified by rig.
