using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    /// <summary>Optional version-1 integration for other mods. Providers may stage registration
    /// before PC loads. Dispose the handle when the provider unloads. No PC dependency is required
    /// for products to use the default policy. No native objects or provider callbacks cross this API.</summary>
    public static class FoodProductMetadataApi
    {
        public const int Version = FoodProductMetadataRegistry.ApiVersion;
        private static readonly FoodProductMetadataRegistry Registry = new FoodProductMetadataRegistry();
        public static bool TryCreateProvider(string providerId, out FoodProductMetadataProvider provider, out string reason)
            => Registry.TryCreateProvider(providerId, out provider, out reason);
        internal static void BindMainThread() => Registry.BindPublicationThread();
        internal static void Unload() => Registry.ClearOnUnload();
        // E5b supplies its already copied loaded-name image at a new uncaptured context, never a native scan.
        internal static FoodProductMetadataResolver Publish(FoodProductMetadataResolver config, FoodProductMetadataLoadedNames? catalogue)
            => config.WithApi(Registry.Publish(catalogue));
        // E5b4: the API image alone, so the market's policy source rebuilds only when the registry publishes a new one.
        internal static FoodProductMetadataSnapshot PublishSnapshot(FoodProductMetadataLoadedNames? catalogue) => Registry.Publish(catalogue);
    }
    public sealed partial class PopulationRuntime
    {
        /// <summary>Main-thread resolver composition seam for E5b and explicit rig diagnostics.
        /// Does not capture/rewrite a held decision or grant native eligibility.</summary>
        public FoodProductMetadataResolver PublishFoodProductMetadata(FoodProductMetadataLoadedNames? catalogue)
            => FoodProductMetadataApi.Publish(settings.FoodProductOverrides.Resolver, catalogue);
    }
}
