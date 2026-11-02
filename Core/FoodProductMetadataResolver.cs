using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>Immutable config-only metadata. No catalogue, memory, persistence or native authority.</summary>
    public sealed class FoodProductMetadataResolver
    {
        public const int MaximumOverrides = 256;
        public const int MaximumNameLength = 256;
        public const int MaximumTableCharactersAndBytes = 512 * 1024;
        // Open E5 questions, recommended policies only. E5b owns meal admission, not this resolver.
        public const MealClass UnclassifiedMealClass = MealClass.Unknown; // Q1
        public const string MetadataSourcePolicy = "config-only"; // Q2, no registration API
        public const int UnknownTasteBand = 2; // Q3, no individual name/code hash
        public const bool BundleMealAdmissionPolicy = false; // Q4, not wired to service
        public static readonly FoodProductMetadata Neutral = new FoodProductMetadata(UnclassifiedMealClass, UnknownTasteBand, 900, 100, 450);
        public static readonly FoodProductMetadataResolver Empty = new FoodProductMetadataResolver(Array.Empty<KeyValuePair<string, FoodProductMetadata>>());
        private readonly Dictionary<string, FoodProductMetadata> overrides;

        public FoodProductMetadataResolver(IEnumerable<KeyValuePair<string, FoodProductMetadata>> entries)
        {
            overrides = new Dictionary<string, FoodProductMetadata>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (overrides.Count == MaximumOverrides || !ValidName(entry.Key) || overrides.ContainsKey(entry.Key))
                    throw new ArgumentException("Invalid or overbound metadata table.");
                overrides.Add(entry.Key, entry.Value);
            }
        }
        public int Count => overrides.Count;
        public IEnumerable<string> Names => overrides.Keys;
        public FoodProductMetadata Resolve(string canonicalName, ResidentTraits traits)
        {
            if (FoodProducts.KnownLegacy(canonicalName))
                return new FoodProductMetadata(MealClass.Food, FoodChoicePolicy.Taste(traits, canonicalName),
                    FoodProducts.Penalty(MealKind.Breakfast, canonicalName), FoodProducts.Penalty(MealKind.Lunch, canonicalName),
                    FoodProducts.Penalty(MealKind.Dinner, canonicalName));
            return overrides.TryGetValue(canonicalName, out var metadata) ? metadata : Neutral;
        }
        public static bool ValidName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name) || name!.Length > MaximumNameLength) return false;
            for (int i=0; i<name.Length; i++)
            {
                char c=name[i];
                if (char.IsControl(c) || c=='|') return false;
                if (char.IsHighSurrogate(c)) { if (++i==name.Length || !char.IsLowSurrogate(name[i])) return false; }
                else if (char.IsLowSurrogate(c)) return false;
            }
            return true;
        }
    }
}
