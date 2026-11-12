using System;

namespace PersistentCity.Core
{
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
