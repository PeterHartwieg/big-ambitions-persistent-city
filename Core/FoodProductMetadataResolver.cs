using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>Immutable config/API metadata. No memory, persistence or native authority.</summary>
    public sealed class FoodProductMetadataResolver
    {
        public const int MaximumOverrides = 256;
        public const int MaximumNameLength = FoodNameIdentity.MaximumNameLength;
        public const int MaximumTableCharactersAndBytes = 512 * 1024;
        // Peter, 2 Nov 2026: Food by default; E5b owns native meal admission.
        public const MealClass UnclassifiedMealClass = MealClass.Food; // Q1, one policy constant
        public const string MetadataSourcePolicy = "config > API > defaults";
        public const bool BundleMealAdmissionPolicy = false; // Q4, not wired to service
        public static readonly FoodProductMetadataOverride Defaults = new FoodProductMetadataOverride(UnclassifiedMealClass, null, 900, 100, 450);
        public static readonly FoodProductMetadataResolver Empty = new FoodProductMetadataResolver(Array.Empty<KeyValuePair<string, FoodProductMetadataOverride>>());
        private readonly Dictionary<string, FoodProductMetadataOverride> overrides;
        private readonly FoodProductMetadataSnapshot api = FoodProductMetadataSnapshot.Empty;

        private FoodProductMetadataResolver(FoodProductMetadataResolver config, FoodProductMetadataSnapshot snapshot)
        { overrides = config.overrides; api = snapshot; }
        // Call at a coherent uncaptured-context boundary using a published, loaded-name-filtered image.
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
        public FoodProductMetadata Resolve(string canonicalName, ResidentTraits traits, int seed, string consumerId)
        {
            if (FoodProducts.KnownLegacy(canonicalName))
                return new FoodProductMetadata(MealClass.Food, FoodChoicePolicy.Taste(traits, canonicalName),
                    FoodProducts.Penalty(MealKind.Breakfast, canonicalName), FoodProducts.Penalty(MealKind.Lunch, canonicalName),
                    FoodProducts.Penalty(MealKind.Dinner, canonicalName));
            overrides.TryGetValue(canonicalName, out var configured);
            api.TryGet(canonicalName, out var supplied);
            return new FoodProductMetadata(configured.OptionalMealClass ?? supplied.OptionalMealClass ?? Defaults.MealClass,
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
        // Explicit diagnostics only; no logging or success-path strings during choice.
        public string ApiReason(string canonicalName) => api.Reason(canonicalName);
        /// <summary>Copy effective inputs into E5d's existing revision-2 held tuple. The host
        /// calls this once for a new offer; replay uses the saved tuple, never this resolver.</summary>
        public FoodOfferMetadata Capture(string canonicalName, ResidentTraits traits, int seed, string consumerId)
        {
            var value = Resolve(canonicalName, traits, seed, consumerId);
            return new FoodOfferMetadata(2, (FoodMealClass)value.MealClass, value.TasteBand,
                value.BreakfastFitCents, value.LunchFitCents, value.DinnerFitCents);
        }
        // Version 1: FNV-1a over UTF-16 code units with fixed uint finaliser (StableRandom).
        // Domain + length-delimited stable consumer ID and canonical product name, seeded by saved city seed.
        // No allocation, catalogue index, runtime string hash, day or config revision enters the draw.
        public static int DefaultTaste(int seed, string consumerId, string canonicalName)
        {
            if (string.IsNullOrEmpty(consumerId) || !ValidName(canonicalName)) throw new ArgumentException("Invalid product taste identity.");
            uint hash=StableRandom.Append(StableRandom.Begin(seed), "pc:product-name-taste:v1|");
            hash=StableRandom.Append(StableRandom.AppendDecimal(hash,consumerId.Length), ':');
            hash=StableRandom.Append(StableRandom.Append(hash,consumerId), '|');
            hash=StableRandom.Append(StableRandom.AppendDecimal(hash,canonicalName.Length), ':');
            uint percentile=StableRandom.Finish(StableRandom.Append(hash,canonicalName)) % 100;
            // Same 10/20/40/20/10 distribution as A2 and E4, with canonical names replacing codes.
            return percentile<10 ? 0 : percentile<30 ? 1 : percentile<70 ? 2 : percentile<90 ? 3 : 4;
        }
        public bool TryGetOverride(string canonicalName, out FoodProductMetadataOverride metadata)
            => overrides.TryGetValue(canonicalName, out metadata);
        public static bool ValidName(string? name) => FoodNameIdentity.IsCanonical(name) && name!.IndexOf('\\') < 0;
    }
}
