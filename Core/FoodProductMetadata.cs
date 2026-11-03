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
    // Partial metadata; absence falls through config > API > defaults, per field.
    public readonly struct FoodProductMetadataOverride
    {
        public FoodProductMetadataOverride(MealClass mealClass, int? tasteBand, int breakfastFitCents, int lunchFitCents, int dinnerFitCents)
            : this((MealClass?)mealClass, tasteBand, (int?)breakfastFitCents, lunchFitCents, dinnerFitCents) { }
        public FoodProductMetadataOverride(MealClass? mealClass, int? tasteBand, int? breakfastFitCents, int? lunchFitCents, int? dinnerFitCents)
        {
            _ = new FoodProductMetadata(mealClass ?? MealClass.Food, tasteBand ?? 2, breakfastFitCents ?? 900, lunchFitCents ?? 100, dinnerFitCents ?? 450);
            OptionalMealClass=mealClass; TasteBand=tasteBand; OptionalBreakfastFitCents=breakfastFitCents;
            OptionalLunchFitCents=lunchFitCents; OptionalDinnerFitCents=dinnerFitCents;
        }
        public MealClass? OptionalMealClass { get; }
        public int? OptionalBreakfastFitCents { get; }
        public int? OptionalLunchFitCents { get; }
        public int? OptionalDinnerFitCents { get; }
        public MealClass MealClass => OptionalMealClass ?? MealClass.Food;
        public int? TasteBand { get; }
        public int BreakfastFitCents => OptionalBreakfastFitCents ?? 900;
        public int LunchFitCents => OptionalLunchFitCents ?? 100;
        public int DinnerFitCents => OptionalDinnerFitCents ?? 450;
    }

    // Shared immutable E5 DTO only. Classification is authored policy, never native eligibility.
    public enum FoodMealClass { Unknown, Food, Drink, Bundle, NonFood }

    /// <summary>Effective inputs of one held offer. Revision 1 preserves E4's code-based
    /// policy; revision 2 reserves name-based inputs for E5c/E5b. Neither grants sale permission.</summary>
    public sealed class FoodOfferMetadata
    {
        public FoodOfferMetadata(int revision, FoodMealClass mealClass, int tasteBand,
            int breakfastFitCents, int lunchFitCents, int dinnerFitCents)
        {
            Revision = revision; MealClass = mealClass; TasteBand = tasteBand;
            BreakfastFitCents = breakfastFitCents; LunchFitCents = lunchFitCents; DinnerFitCents = dinnerFitCents;
        }
        public int Revision { get; }
        public FoodMealClass MealClass { get; }
        public int TasteBand { get; }
        public int BreakfastFitCents { get; }
        public int LunchFitCents { get; }
        public int DinnerFitCents { get; }
    }
}
