using System.Collections.Generic;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5FoodCommitSliceTests
{
    private sealed class Validating : IRoutePlanner, IFoodWalkingPace, IWalkingQuoteValidity, IWalkingQuoteRevision
    {
        public long Revision;
        public long WalkingQuoteRevision => Revision;
        private readonly WeightedFoodChoiceTests.Planner inner;
        public Validating(WeightedFoodChoiceTests.Planner inner) { this.inner = inner; }
        public int Validations;
        public bool Valid = true;
        public double WalkingMetresPerMinute => inner.WalkingMetresPerMinute;
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences) => inner.Plan(from, to, at, preferences);
        public bool IsWalkingQuoteValid(Place from, Place to, Route route, TravelPreferences preferences) { Validations++; return Valid; }
    }

    private static (CitySimulator City, Validating Planner) Validated(int seed)
    {
        var (city, _, planner) = WeightedFoodChoiceTests.Ready(seed, 4);
        var validating = new Validating(planner);
        return (city.RebuildRoutes(validating), validating);
    }

    [Theory]
    [InlineData(1)] [InlineData(7)] [InlineData(23)]
    public void EachFoodUpdateValidatesAtMostOneCapturedWalkAndTheCityMatchesAnUnvalidatedPlanner(int seed)
    {
        var (control, _, _) = WeightedFoodChoiceTests.Ready(seed, 4);
        var (city, planner) = Validated(seed);
        double desired = city.State.Food!.Residents[0].DesiredAt;
        var perUpdate = new List<int>();
        while (!city.IsCaughtUpTo(desired + 30))
        {
            int before = planner.Validations;
            city.AdvanceFrameSlice(desired + 30, 1);
            perUpdate.Add(planner.Validations - before);
        }
        while (!control.AdvanceTo(desired + 30, 1).CaughtUp) { }
        Assert.Equal(2, planner.Validations);
        Assert.All(perUpdate, n => Assert.InRange(n, 0, 1));
        Assert.Equal(control.State.Food!.Residents[0].Status, city.State.Food.Residents[0].Status);
        Assert.Equal(DevelopmentChain.Write(control.State), DevelopmentChain.Write(city.State));
    }

    [Fact]
    public void AStaleReturnWalkStillMissesTheMealInItsOwnUpdate()
    {
        var (city, planner) = Validated(7);
        var meal = city.State.Food!.Residents[0];
        while (planner.Validations == 0) city.AdvanceFrameSlice(meal.DesiredAt, 1);
        planner.Valid = false;
        city.AdvanceFrameSlice(meal.DesiredAt, 1);
        Assert.Equal(2, planner.Validations);
        Assert.Equal(MealStatus.Missed, meal.Status);
        Assert.Equal("Schedule or shop changed before departure", meal.Reason);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public void ChangedWalkingInputsAfterEitherValidationUpdateValidateAgainBeforeDeparture(int validatedBeforeChange)
    {
        var (city, planner) = Validated(7);
        var meal = city.State.Food!.Residents[0];
        while (planner.Validations < validatedBeforeChange) city.AdvanceFrameSlice(meal.DesiredAt, 1);
        planner.Valid = false; planner.Revision++;
        while (meal.Status != MealStatus.Missed && meal.Status != MealStatus.Travelling) city.AdvanceFrameSlice(meal.DesiredAt, 1);
        Assert.Equal(MealStatus.Missed, meal.Status);
        Assert.Equal(validatedBeforeChange + 1, planner.Validations);
        Assert.Equal("Schedule or shop changed before departure", meal.Reason);
    }

    [Fact]
    public void AReplacedPlannerValidatesBothWalksItselfBeforeDeparture()
    {
        var (city, planner) = Validated(7);
        var meal = city.State.Food!.Residents[0];
        while (planner.Validations < 2) city.AdvanceFrameSlice(meal.DesiredAt, 1);
        var replacement = new Validating(new WeightedFoodChoiceTests.Planner()) { Valid = false };
        city = city.RebuildRoutes(replacement);
        meal = city.State.Food!.Residents[0];
        while (meal.Status != MealStatus.Missed && meal.Status != MealStatus.Travelling) city.AdvanceFrameSlice(meal.DesiredAt, 1);
        Assert.Equal(MealStatus.Missed, meal.Status);
        Assert.Equal(1, replacement.Validations);
    }

    [Fact]
    public void UnchangedInputsDepartAfterExactlyTwoValidations()
    {
        var (city, planner) = Validated(7);
        var meal = city.State.Food!.Residents[0];
        while (meal.Status != MealStatus.Missed && meal.Status != MealStatus.Travelling) city.AdvanceFrameSlice(meal.DesiredAt, 1);
        Assert.Equal(MealStatus.Travelling, meal.Status);
        Assert.Equal(2, planner.Validations);
    }
}
