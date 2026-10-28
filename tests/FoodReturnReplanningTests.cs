using System;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed partial class FoodCommerceTests
{
    private sealed class ReturnReplanningWalking : IRoutePlanner, IWalkingQuoteValidity
    {
        public bool Changed, Accessible = true;
        public double ReturnMinutes = 8;
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences)
        {
            if (!Accessible) return null;
            if (!Changed || from.Id != "shop") return new Route { Legs = new() {
                new RouteLeg { From = from.Position, To = to.Position, Minutes = 5, Mode = TravelMode.Walk } } };
            var corner = new Position(1.5, 0, 1);
            return new Route { Legs = new() {
                new RouteLeg { From = from.Position, To = corner, Minutes = ReturnMinutes / 2, Mode = TravelMode.Walk },
                new RouteLeg { From = corner, To = to.Position, Minutes = ReturnMinutes / 2, Mode = TravelMode.Walk } } };
        }
        public bool IsWalkingQuoteValid(Place from, Place to, Route route, TravelPreferences preferences)
        {
            var current = Plan(from, to, 0, preferences);
            return current != null && route.Legs.Count == current.Legs.Count
                && route.Legs.Zip(current.Legs).All(pair => pair.First.Minutes == pair.Second.Minutes
                    && Position.Distance(pair.First.From, pair.Second.From) < .001
                    && Position.Distance(pair.First.To, pair.Second.To) < .001);
        }
    }

    private static (CitySimulator city, Market market, ReturnReplanningWalking routes, ResidentMeal meal)
        ReadyForRepresentedReturn()
    {
        var (initial, market, _) = Ready(atWork: true, representation: 2);
        initial.State.Food!.Residents.Single(m => m.Slot == 0).Allowance = Money.FromCents(0);
        var routes = new ReturnReplanningWalking();
        var city = initial.RebuildRoutes(routes);
        var meal = city.State.Food!.Residents.Single(m => m.Slot == 1);
        Advance(city, meal.DesiredAt + 10);
        Assert.Equal(MealStatus.Paid, meal.Status);
        Assert.Equal(1, meal.Purchases);
        Assert.False(meal.Excursion!.Returning);
        Assert.True(meal.Excursion.ReturnStartsAt > city.State.Minute);
        return (city, market, routes, meal);
    }

    [Fact]
    public void ChangedFeasibleReturnCommitsCurrentGeometryAndDurationWithoutAnotherSale()
    {
        var (city, market, routes, meal) = ReadyForRepresentedReturn();
        var excursion = meal.Excursion!;
        string originalGeometry = excursion.ReturnGeometry, receipt = meal.IntentId;
        Money spent = meal.Spent;
        routes.Changed = true;
        Advance(city, excursion.ReturnStartsAt + .01);
        Assert.True(excursion.Returning); Assert.False(excursion.Unresolved);
        Assert.NotEqual(originalGeometry, excursion.ReturnGeometry);
        Assert.Equal(routes.ReturnMinutes, meal.ReturnMinutes);
        Assert.Equal(excursion.ReturnStartsAt + routes.ReturnMinutes, excursion.ReturnsAt, 6);
        Advance(city, excursion.ReturnsAt);
        Assert.Null(meal.Excursion);
        Assert.False(meal.NeedsHomeResync);
        Assert.Equal(receipt, meal.IntentId); Assert.Equal(spent, meal.Spent);
        Assert.Equal(1, meal.Purchases); Assert.Single(market.Orders);
        Assert.Single(city.State.Citizens);
    }

    [Fact]
    public void ChangedSlowerReturnCannotBorrowOldDurationToFitSavedDeadline()
    {
        var (city, market, routes, meal) = ReadyForRepresentedReturn();
        var excursion = meal.Excursion!;
        meal.Deadline = excursion.ReturnStartsAt + 7;
        double limit = Math.Min(meal.Deadline, excursion.SharedDepartureAt);
        Assert.True(excursion.ReturnStartsAt + meal.ReturnMinutes < limit);
        routes.Changed = true; routes.ReturnMinutes = 8;
        string originalGeometry = excursion.ReturnGeometry;
        Advance(city, excursion.ReturnStartsAt + .01);
        Assert.True(excursion.Unresolved); Assert.False(excursion.Returning);
        Assert.Equal(0, excursion.ReturnsAt);
        Assert.Equal(originalGeometry, excursion.ReturnGeometry);
        Assert.Equal(5, meal.ReturnMinutes);
        Assert.Equal(1, meal.Purchases); Assert.Single(market.Orders);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ChangedReturnStillRefusesMovedEndpointOrUnavailablePath(bool movedEndpoint)
    {
        var (city, market, routes, meal) = ReadyForRepresentedReturn();
        var excursion = meal.Excursion!;
        routes.Changed = true;
        if (movedEndpoint) city.State.Places.Single(p => p.Id == "work").Position = new Position(9, 0, 0);
        else routes.Accessible = false;
        Advance(city, excursion.ReturnStartsAt + .01);
        Assert.True(excursion.Unresolved); Assert.False(excursion.Returning);
        Assert.Equal(0, excursion.ReturnsAt);
        Assert.Equal(1, meal.Purchases); Assert.Single(market.Orders);
    }

    [Fact]
    public void ReloadAfterChangedReturnCommitKeepsItsArrivalDespiteLaterRouteTimingChanges()
    {
        var (city, market, routes, meal) = ReadyForRepresentedReturn();
        routes.Changed = true;
        Advance(city, meal.Excursion!.ReturnStartsAt + .01);
        Assert.True(meal.Excursion.Returning);
        double arrival = meal.Excursion.ReturnsAt;
        string geometry = meal.Excursion.ReturnGeometry, receipt = meal.IntentId;
        Money spent = meal.Spent;
        var read = SaveCodec.Read(SaveCodec.Write(city.State)); Assert.Equal("", read.Error);
        routes.ReturnMinutes = 100;
        city = new CitySimulator(read.State!, routes); city.AttachFoodMarket(market);
        meal = city.State.Food!.Residents.Single(m => m.Slot == 1);
        Advance(city, arrival - .01);
        Assert.True(meal.Excursion!.Returning);
        Assert.Equal(arrival, meal.Excursion.ReturnsAt);
        Assert.Equal(geometry, meal.Excursion.ReturnGeometry);
        Assert.Equal(8, meal.ReturnMinutes);
        Advance(city, arrival);
        Assert.Null(meal.Excursion);
        Assert.Equal(receipt, meal.IntentId); Assert.Equal(spent, meal.Spent);
        Assert.Equal(1, meal.Purchases); Assert.Single(market.Orders);
    }
}
