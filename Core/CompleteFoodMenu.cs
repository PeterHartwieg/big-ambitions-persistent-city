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
                if (resolver.ResolveMealClass(offer.ProductId) != MealClass.Food)
                    return CompleteFoodMenuRefusal.MealClass;
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

        /// <summary>E5b3 Decision 8: the products already in this business's history row for <paramref name="day"/>,
        /// the row a sale today would add a cell to. Appends distinct names to <paramref name="into"/>.</summary>
        public static void HistoryProducts(IReadOnlyList<FoodShopDay> history, string placeId, string businessId, int day, List<string> into)
        {
            into.Clear();
            for (int i = 0; i < history.Count; i++)
            {
                var row = history[i];
                if (row == null || row.Day != day || row.PlaceId != placeId || row.BusinessId != businessId || row.ProductSales == null) continue;
                foreach (var cell in row.ProductSales)
                    if (cell != null && !into.Contains(cell.ProductId)) into.Add(cell.ProductId);
            }
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

    /// <summary>E5b3 Decision 8: a superset of the city's referenced product-name table, kept by the shop market.
    /// Seeded once from the loaded food state and economy ledger with the same sources the save writer uses, then
    /// grown by every product a scope adopts. Every name the save can reference enters through a scope's products,
    /// so the set never undercounts; a retired name only makes the preflight stricter until reload.</summary>
    public sealed class FoodReferencedNames
    {
        private readonly HashSet<string> set = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> names = new List<string>();
        public IReadOnlyList<string> Names => names;
        public int Count => names.Count;
        public bool Add(string? name)
        {
            if (string.IsNullOrEmpty(name) || !set.Add(name!)) return false;
            names.Add(name!); return true;
        }
        public static FoodReferencedNames From(FoodCommerceState? food, EconomicLedger? economy)
        {
            var result = new FoodReferencedNames();
            if (food != null)
            {
                if (food.Shops != null) foreach (var shop in food.Shops)
                {
                    if (shop == null) continue;
                    result.Add(shop.ProductId);
                    if (shop.AdditionalProductIds != null) foreach (var name in shop.AdditionalProductIds) result.Add(name);
                }
                if (food.Residents != null) foreach (var meal in food.Residents)
                {
                    if (meal == null) continue;
                    result.Add(meal.ProductId); result.Add(meal.PreferredProductId);
                    if (meal.Decision != null)
                    {
                        result.Add(meal.Decision.ProductId);
                        if (meal.Decision.Shops != null) foreach (var shop in meal.Decision.Shops)
                            if (shop?.Offers != null) foreach (var offer in shop.Offers) if (offer != null) result.Add(offer.ProductId);
                    }
                    if (meal.Today?.Meals != null) foreach (var outcome in meal.Today.Meals)
                        if (outcome != null && FoodNameIdentity.IsCanonical(outcome.ProductId)) result.Add(outcome.ProductId);
                }
                if (food.History != null) foreach (var row in food.History)
                    if (row?.ProductSales != null) foreach (var cell in row.ProductSales)
                        if (cell != null && !cell.HasOpaqueIdentity() && FoodNameIdentity.IsCanonical(cell.ProductId)) result.Add(cell.ProductId);
            }
            if (economy?.Shops != null) foreach (var shop in economy.Shops)
            {
                if (shop?.Changes != null) foreach (var change in shop.Changes)
                    if (change.Kind == ShopChangeKind.Price || change.Kind == ShopChangeKind.Stock) result.Add(change.ProductId);
                if (shop?.Supply != null) foreach (var offer in shop.Supply) result.Add(offer.ProductId);
            }
            return result;
        }
    }
}
