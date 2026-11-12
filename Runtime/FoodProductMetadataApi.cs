using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    public static class FoodProductMetadataApi
    {
        public const int Version = FoodProductMetadataRegistry.ApiVersion;
        private static readonly FoodProductMetadataRegistry Registry = new FoodProductMetadataRegistry();
        public static bool TryCreateProvider(string providerId, out FoodProductMetadataProvider provider, out string reason)
            => Registry.TryCreateProvider(providerId, out provider, out reason);
        internal static void BindMainThread() => Registry.BindPublicationThread();
        internal static void Unload() => Registry.ClearOnUnload();
        internal static FoodProductMetadataResolver Publish(FoodProductMetadataResolver config, FoodProductMetadataLoadedNames? catalogue)
            => config.WithApi(Registry.Publish(catalogue));
        internal static FoodProductMetadataSnapshot PublishSnapshot(FoodProductMetadataLoadedNames? catalogue) => Registry.Publish(catalogue);
    }
    public sealed partial class PopulationRuntime
    {
        public FoodProductMetadataResolver PublishFoodProductMetadata(FoodProductMetadataLoadedNames? catalogue)
            => FoodProductMetadataApi.Publish(settings.FoodProductOverrides.Resolver, catalogue);
    }
}
