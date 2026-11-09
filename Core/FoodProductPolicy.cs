using System;

namespace PersistentCity.Core
{
    /// <summary>E5b4 (Decision 11): which product names a new decision context may hold, and the metadata it captures for
    /// them. A market supplies one immutable policy per uncaptured context; held tuples replay as saved and never read it.
    /// Executable is a choice permission only. Native sale permission stays with the market's own admission.</summary>
    public interface IFoodProductPolicy
    {
        /// <summary>An original product, or a canonical name in the published loaded-name image that the resolver classes
        /// as Food. Drink, Bundle, NonFood and Unknown never enter a decision.</summary>
        bool Executable(string name);
        /// <summary>Revision-1 legacy metadata for an original product, revision-2 resolver metadata otherwise.</summary>
        FoodOfferMetadata Capture(string name, ResidentTraits traits, int seed, string consumerId);
    }

    /// <summary>A market that supplies the product policy for its next uncaptured context.</summary>
    public interface IFoodProductPolicyMarket
    {
        IFoodProductPolicy ProductPolicy { get; }
    }

    public sealed class FoodProductPolicy : IFoodProductPolicy
    {
        /// <summary>The policy of a market that supplies none, and of every writer before the held-policy schema.</summary>
        public static readonly FoodProductPolicy OriginalThree = new FoodProductPolicy(FoodProductMetadataResolver.Empty, null);
        private readonly FoodProductMetadataResolver resolver;
        private readonly FoodProductMetadataLoadedNames? loaded;

        /// <param name="resolver">Config over API over defaults, already published for this context.</param>
        /// <param name="loaded">The published loaded-name image; null while none is published, which admits no dynamic name.</param>
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
    }
}
