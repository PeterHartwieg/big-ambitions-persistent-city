using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public enum CompleteFoodMenuRefusal
    {
        None, Dirty, StockContract, MealClass, NativeContract, HistoryCapacity, NameCapacity
    }

    /// <summary>Admission of a complete detached E5a image. A match is conditional evidence,
    /// not native authority: Runtime must also establish timely notifications and safe handoff.</summary>
    public static class CompleteFoodMenu
    {
        public static CompleteFoodMenuRefusal Check(FoodEligibilityImage image,
            FoodProductMetadataResolver resolver, bool currentRevision, bool nativeContract,
            IReadOnlyList<string> historyProducts, IReadOnlyList<string> referencedNames)
        {
            if (!currentRevision) return CompleteFoodMenuRefusal.Dirty;
            if (!image.StockContractMatched) return CompleteFoodMenuRefusal.StockContract;
            // Admission is resident-independent. Original-three overrides remain inert in the resolver.
            for (int i = 0; i < image.Offers.Count; i++)
            {
                var offer = image.Offers[i];
                if (!FoodProducts.KnownLegacy(offer.ProductId)
                    && resolver.TryGetOverride(offer.ProductId, out var metadata)
                    && metadata.MealClass != MealClass.Food) return CompleteFoodMenuRefusal.MealClass;
            }
            if (!Fits(historyProducts, image.Offers, FoodMenuPolicy.MaximumOffers))
                return CompleteFoodMenuRefusal.HistoryCapacity;
            if (!Fits(referencedNames, image.Offers, FoodNameIdentity.MaximumNames))
                return CompleteFoodMenuRefusal.NameCapacity;
            return nativeContract ? CompleteFoodMenuRefusal.None : CompleteFoodMenuRefusal.NativeContract;
        }

        // At most eight incoming names. Count the union without building a set on every check.
        private static bool Fits(IReadOnlyList<string> existing, IReadOnlyList<FoodStockOfferImage> offers, int maximum)
        {
            if (existing.Count > maximum) return false;
            int count = existing.Count;
            for (int i = 0; i < offers.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < existing.Count; j++) if (string.Equals(existing[j], offers[i].ProductId, StringComparison.Ordinal)) { found = true; break; }
                if (!found && ++count > maximum) return false;
            }
            return true;
        }

        public static FoodOfferMetadata Capture(FoodProductMetadata metadata)
            => new FoodOfferMetadata(2, WireClass(metadata.MealClass), metadata.TasteBand,
                metadata.BreakfastFitCents, metadata.LunchFitCents, metadata.DinnerFitCents);

        // Keep E5d's wire enum fixed. Never depend on the unrelated enums' numeric coincidence.
        public static FoodMealClass WireClass(MealClass value) => value switch
        {
            MealClass.Unknown => FoodMealClass.Unknown,
            MealClass.Food => FoodMealClass.Food,
            MealClass.Drink => FoodMealClass.Drink,
            MealClass.Bundle => FoodMealClass.Bundle,
            MealClass.NonFood => FoodMealClass.NonFood,
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }
}
