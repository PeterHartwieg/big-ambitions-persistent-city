using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>Immutable config-only metadata. No catalogue, memory, persistence or native authority.</summary>
    public sealed class FoodProductMetadataResolver
    {
        public const int MaximumOverrides = 256;
        public const int MaximumNameLength = FoodNameIdentity.MaximumNameLength;
        public const int MaximumTableCharactersAndBytes = 512 * 1024;
        // Peter, 2 Nov 2026: Food by default; E5b owns native meal admission.
        public const MealClass UnclassifiedMealClass = MealClass.Food; // Q1, one policy constant
        public const string MetadataSourcePolicy = "config > future E5e provider > defaults"; // Q2; no API in E5c
        public const bool BundleMealAdmissionPolicy = false; // Q4, not wired to service
        public static readonly FoodProductMetadataOverride Defaults = new FoodProductMetadataOverride(UnclassifiedMealClass, null, 900, 100, 450);
        public static readonly FoodProductMetadataResolver Empty = new FoodProductMetadataResolver(Array.Empty<KeyValuePair<string, FoodProductMetadataOverride>>());
        private readonly Dictionary<string, FoodProductMetadataOverride> overrides;

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
            var metadata = overrides.TryGetValue(canonicalName, out var configured) ? configured : Defaults;
            return new FoodProductMetadata(metadata.MealClass, metadata.TasteBand ?? DefaultTaste(seed, consumerId, canonicalName),
                metadata.BreakfastFitCents, metadata.LunchFitCents, metadata.DinnerFitCents);
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
        public static bool ValidName(string? name) => FoodNameIdentity.IsCanonical(name);
    }
}
