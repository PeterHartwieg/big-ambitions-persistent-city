namespace PersistentCity.Core
{
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
