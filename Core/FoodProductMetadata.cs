using System;

namespace PersistentCity.Core
{
    // Detached effective inputs only: never a loaded-definition, stock or checkout permission.
    public enum MealClass { Unknown, Food, Drink, Bundle, NonFood }

    public readonly struct FoodProductMetadata
    {
        public FoodProductMetadata(MealClass mealClass, int tasteBand, int breakfastFitCents, int lunchFitCents, int dinnerFitCents)
        {
            if (mealClass < MealClass.Unknown || mealClass > MealClass.NonFood || tasteBand < 0 || tasteBand > 4
                || breakfastFitCents < 0 || breakfastFitCents > 5000 || lunchFitCents < 0 || lunchFitCents > 5000
                || dinnerFitCents < 0 || dinnerFitCents > 5000) throw new ArgumentException("Invalid food metadata.");
            MealClass=mealClass; TasteBand=tasteBand; BreakfastFitCents=breakfastFitCents;
            LunchFitCents=lunchFitCents; DinnerFitCents=dinnerFitCents;
        }
        public MealClass MealClass { get; }
        public int TasteBand { get; }
        public int BreakfastFitCents { get; }
        public int LunchFitCents { get; }
        public int DinnerFitCents { get; }
        public int Penalty(MealKind meal) => meal == MealKind.Legacy ? 0 : meal == MealKind.Breakfast ? BreakfastFitCents
            : meal == MealKind.Lunch ? LunchFitCents : meal == MealKind.Dinner ? DinnerFitCents : throw new ArgumentOutOfRangeException(nameof(meal));
    }
    // Partial config metadata: null taste preserves the per-resident seeded default.
    public readonly struct FoodProductMetadataOverride
    {
        public FoodProductMetadataOverride(MealClass mealClass, int? tasteBand, int breakfastFitCents, int lunchFitCents, int dinnerFitCents)
        {
            _ = new FoodProductMetadata(mealClass, tasteBand ?? 2, breakfastFitCents, lunchFitCents, dinnerFitCents);
            MealClass=mealClass; TasteBand=tasteBand; BreakfastFitCents=breakfastFitCents;
            LunchFitCents=lunchFitCents; DinnerFitCents=dinnerFitCents;
        }
        public MealClass MealClass { get; }
        public int? TasteBand { get; }
        public int BreakfastFitCents { get; }
        public int LunchFitCents { get; }
        public int DinnerFitCents { get; }
    }

}
