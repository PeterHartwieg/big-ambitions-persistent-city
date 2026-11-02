using System;

namespace PersistentCity.Core
{
    /// <summary>Frozen E4 inputs for predecessor replay. This does not resolve new products.
    /// FoodProducts' additional definitions exist only in the offline E4 envelope harness.</summary>
    public static class LegacyFoodOfferMetadata
    {
        public static FoodOfferMetadata Capture(ResidentTraits traits, int seed, string consumer, string product)
        {
            if (!FoodProducts.Known(product)) throw new ArgumentException("Uncaptured dynamic offer metadata.");
            return new FoodOfferMetadata(1, FoodMealClass.Food, FoodMenuPolicy.Taste(traits, seed, consumer, product),
                FoodProducts.Penalty(MealKind.Breakfast, product), FoodProducts.Penalty(MealKind.Lunch, product),
                FoodProducts.Penalty(MealKind.Dinner, product));
        }
    }
}
