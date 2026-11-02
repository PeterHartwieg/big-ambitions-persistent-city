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
}
