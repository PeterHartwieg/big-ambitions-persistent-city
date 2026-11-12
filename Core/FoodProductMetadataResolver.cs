using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public sealed class FoodProductMetadataResolver
    {
        public const int MaximumOverrides = 256;
        public const int MaximumNameLength = FoodNameIdentity.MaximumNameLength;
        public const int MaximumTableCharactersAndBytes = 512 * 1024;
        public const MealClass UnclassifiedMealClass = MealClass.Food;
        public const string MetadataSourcePolicy = "config > API > defaults";
        public const bool BundleMealAdmissionPolicy = false;
        public static readonly FoodProductMetadataOverride Defaults = new FoodProductMetadataOverride(UnclassifiedMealClass, null, 900, 100, 450);
        public static readonly FoodProductMetadataResolver Empty = new FoodProductMetadataResolver(Array.Empty<KeyValuePair<string, FoodProductMetadataOverride>>());
        private readonly Dictionary<string, FoodProductMetadataOverride> overrides;
        private readonly FoodProductMetadataSnapshot api = FoodProductMetadataSnapshot.Empty;

        private FoodProductMetadataResolver(FoodProductMetadataResolver config, FoodProductMetadataSnapshot snapshot)
        { overrides = config.overrides; api = snapshot; }
        public FoodProductMetadataResolver WithApi(FoodProductMetadataSnapshot snapshot)
            => new FoodProductMetadataResolver(this, snapshot ?? throw new ArgumentNullException(nameof(snapshot)));

        public FoodProductMetadataResolver(IEnumerable<KeyValuePair<string, FoodProductMetadataOverride>> entries)
        {
            overrides = new Dictionary<string, FoodProductMetadataOverride>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (overrides.Count == MaximumOverrides || !ValidName(entry.Key) || overrides.ContainsKey(entry.Key))
                    throw new ArgumentException("Invalid or overbound metadata table.");
                overrides.Add(entry.Key, entry.Value);
            }
        }
        public int Count => overrides.Count;
        public IEnumerable<string> Names => overrides.Keys;
        public MealClass ResolveMealClass(string canonicalName)
        {
            if (FoodProducts.KnownLegacy(canonicalName)) return MealClass.Food;
            overrides.TryGetValue(canonicalName, out var configured);
            api.TryGet(canonicalName, out var supplied);
            return EffectiveMealClass(configured, supplied);
        }
        private static MealClass EffectiveMealClass(FoodProductMetadataOverride configured, FoodProductMetadataOverride supplied)
            => configured.OptionalMealClass ?? supplied.OptionalMealClass ?? Defaults.MealClass;
        public FoodProductMetadata Resolve(string canonicalName, ResidentTraits traits, int seed, string consumerId)
        {
            if (FoodProducts.KnownLegacy(canonicalName))
                return new FoodProductMetadata(MealClass.Food, FoodChoicePolicy.Taste(traits, canonicalName),
                    FoodProducts.Penalty(MealKind.Breakfast, canonicalName), FoodProducts.Penalty(MealKind.Lunch, canonicalName),
                    FoodProducts.Penalty(MealKind.Dinner, canonicalName));
            overrides.TryGetValue(canonicalName, out var configured);
            api.TryGet(canonicalName, out var supplied);
            return new FoodProductMetadata(EffectiveMealClass(configured, supplied),
                configured.TasteBand ?? supplied.TasteBand ?? DefaultTaste(seed, consumerId, canonicalName),
                configured.OptionalBreakfastFitCents ?? supplied.OptionalBreakfastFitCents ?? Defaults.BreakfastFitCents,
                configured.OptionalLunchFitCents ?? supplied.OptionalLunchFitCents ?? Defaults.LunchFitCents,
                configured.OptionalDinnerFitCents ?? supplied.OptionalDinnerFitCents ?? Defaults.DinnerFitCents);
        }
        public FoodProductMetadataSources Sources(string canonicalName)
        {
            if (FoodProducts.KnownLegacy(canonicalName)) return FoodProductMetadataSources.Original;
            overrides.TryGetValue(canonicalName, out var config);
            api.TryGet(canonicalName, out var supplied);
            return new FoodProductMetadataSources(config, supplied);
        }
        public string ApiReason(string canonicalName) => api.Reason(canonicalName);
        public FoodOfferMetadata Capture(string canonicalName, ResidentTraits traits, int seed, string consumerId)
        {
            var value = Resolve(canonicalName, traits, seed, consumerId);
            return new FoodOfferMetadata(2, (FoodMealClass)value.MealClass, value.TasteBand,
                value.BreakfastFitCents, value.LunchFitCents, value.DinnerFitCents);
        }
        public static int DefaultTaste(int seed, string consumerId, string canonicalName)
        {
            if (string.IsNullOrEmpty(consumerId) || !ValidName(canonicalName)) throw new ArgumentException("Invalid product taste identity.");
            uint hash=StableRandom.Append(StableRandom.Begin(seed), "pc:product-name-taste:v1|");
            hash=StableRandom.Append(StableRandom.AppendDecimal(hash,consumerId.Length), ':');
            hash=StableRandom.Append(StableRandom.Append(hash,consumerId), '|');
            hash=StableRandom.Append(StableRandom.AppendDecimal(hash,canonicalName.Length), ':');
            uint percentile=StableRandom.Finish(StableRandom.Append(hash,canonicalName)) % 100;
            return percentile<10 ? 0 : percentile<30 ? 1 : percentile<70 ? 2 : percentile<90 ? 3 : 4;
        }
        public bool TryGetOverride(string canonicalName, out FoodProductMetadataOverride metadata)
            => overrides.TryGetValue(canonicalName, out metadata);
        public static bool ValidName(string? name) => FoodNameIdentity.IsCanonical(name);
    }
}
