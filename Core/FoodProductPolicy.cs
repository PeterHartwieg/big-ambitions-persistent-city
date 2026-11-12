using System;

namespace PersistentCity.Core
{
    public interface IFoodProductPolicy
    {
        bool Executable(string name);
        FoodOfferMetadata Capture(string name, ResidentTraits traits, int seed, string consumerId);
        int TasteBand(string name, ResidentTraits traits, int seed, string consumerId);
    }

    public interface IFoodProductPolicyMarket
    {
        IFoodProductPolicy ProductPolicy { get; }
    }

    public sealed class FoodProductPolicy : IFoodProductPolicy
    {
        public static readonly FoodProductPolicy OriginalThree = new FoodProductPolicy(FoodProductMetadataResolver.Empty, null);
        private readonly FoodProductMetadataResolver resolver;
        private readonly FoodProductMetadataLoadedNames? loaded;

        public FoodProductPolicy(FoodProductMetadataResolver resolver, FoodProductMetadataLoadedNames? loaded)
        {
            this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            this.loaded = loaded;
        }
        public FoodProductMetadataResolver Resolver => resolver;
        public FoodProductMetadataLoadedNames? LoadedNames => loaded;

        public bool Executable(string name)
            => name != null && (FoodProducts.Known(name) || loaded != null && FoodNameIdentity.IsCanonical(name) && loaded.Contains(name)
                && resolver.ResolveMealClass(name) == MealClass.Food);

        public FoodOfferMetadata Capture(string name, ResidentTraits traits, int seed, string consumerId)
        {
            if (FoodProducts.Known(name)) return LegacyFoodOfferMetadata.Capture(traits, seed, consumerId, name);
            if (!Executable(name)) throw new ArgumentException("Product is not executable under this policy.");
            return resolver.Capture(name, traits, seed, consumerId);
        }

        public int TasteBand(string name, ResidentTraits traits, int seed, string consumerId)
        {
            if (FoodProducts.Known(name)) return FoodMenuPolicy.Taste(traits, seed, consumerId, name);
            if (!Executable(name)) throw new ArgumentException("Product is not executable under this policy.");
            return resolver.Resolve(name, traits, seed, consumerId).TasteBand;
        }
    }
}
