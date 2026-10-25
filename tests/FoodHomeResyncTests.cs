using System;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed partial class FoodCommerceTests
{
    private static (CitySimulator City, double Arrival, double Shift) BriefHomeArrival(CitySimulator city, IRoutePlanner routes)
    {
        var person = city.State.Citizens.Single();
        var shift = DailyRoutine.NextWork(person, city.State.Seed, city.State.Minute)!.Value;
        Advance(city, shift.StartsAt - 10);
        person.PlaceId = "work"; person.Activity = Activity.Work;
        person.Phase = CitizenPhase.Travelling; person.DestinationId = person.HomeId;
        person.DestinationActivity = Activity.Home; person.DepartureMinute = city.State.Minute;
        person.Journey = new Route { Legs = new() { new() {
            From = city.State.Places.Single(p => p.Id == "work").Position,
            To = city.State.Places.Single(p => p.Id == person.HomeId).Position,
            Minutes = 5, Mode = TravelMode.Walk } } };
        person.NextEventMinute = city.State.Minute + 5; person.ActivityUntilMinute = shift.StartsAt;
        return (city.RebuildRoutes(routes), person.NextEventMinute, shift.StartsAt);
    }

    [Fact]
    public void BriefHomeStayResynchronizesIdleConsumerBeforeItsLaterWorkMeal()
    {
        var (city, market, routes) = Ready(atWork: true, representation: 2);
        var extra = city.State.Food!.Residents.Single(m => m.Slot == 1);
        extra.NeedsHomeResync = true; // A completed return missed the shared routine departure.
        var staged = BriefHomeArrival(city, routes); city = staged.City;
        double mealAt = extra.DesiredAt, due = extra.DueAt;
        Assert.True(mealAt > staged.Shift + 10);
        Assert.True(extra.NeedsHomeResync); Assert.Null(extra.Excursion);
        Advance(city, staged.Arrival);
        Assert.False(extra.NeedsHomeResync); Assert.Equal(due, extra.DueAt);
        Assert.Equal(0, extra.Purchases); Assert.Empty(market.Orders);
        Advance(city, staged.Shift + 10);
        Assert.Equal("work", city.State.Citizens.Single().PlaceId);
        Assert.Null(city.State.Citizens.Single().Journey);
        city = Reload(city, market, routes);
        string? extraOrigin = null;
        city.FoodDecisionObserved += trace => { if (trace.Consumer.Id == extra.ResidentId) extraOrigin = trace.Intent.OriginPlaceId; };
        Advance(city, mealAt + 60);
        extra = city.State.Food!.Residents.Single(m => m.Slot == 1);
        Assert.Equal("work", extraOrigin); Assert.Equal(1, extra.Purchases);
        Assert.False(extra.NeedsHomeResync); Assert.Null(extra.Excursion);
        Assert.Single(city.State.Citizens);
    }

    [Fact]
    public void HomeResyncNeverSettlesAnUncertainReceiptOrReleasesItsHold()
    {
        var (city, market, routes) = Ready(atWork: true, representation: 2);
        city.EnableHouseholdFinances();
        var extra = city.State.Food!.Residents.Single(m => m.Slot == 1);
        market.ThrowForResident = extra.ResidentId;
        Advance(city, extra.DesiredAt + 60);
        Assert.Equal(MealStatus.Uncertain, extra.Status); Assert.Null(extra.Excursion);
        var staged = BriefHomeArrival(city, routes); city = staged.City;
        extra.NeedsHomeResync = true;
        string receipt = extra.IntentId; long balance = extra.Budget!.BalanceCents;
        long hold = extra.Budget.HeldPurchaseCents, income = extra.Budget.IncomeReceivedCents;
        Assert.True(hold > 0);
        Advance(city, staged.Arrival);
        Assert.False(extra.NeedsHomeResync); Assert.Equal(MealStatus.Uncertain, extra.Status);
        Assert.Equal(receipt, extra.IntentId); Assert.Equal(hold, extra.Budget.HeldPurchaseCents);
        Assert.Equal(balance, extra.Budget.BalanceCents); Assert.Equal(income, extra.Budget.IncomeReceivedCents);
        Assert.Single(market.Orders, id => id == receipt);
        Assert.Equal("", SaveCodec.Read(SaveCodec.Write(city.State)).Error);
    }

    [Fact]
    public void HomeObservationLeavesUnresolvedAwayConsumerAndItsReceiptUntouched()
    {
        var (city, market, _) = Ready(atWork: true, representation: 2);
        var routes = new ValidatedWalking(); city = city.RebuildRoutes(routes);
        var extra = city.State.Food!.Residents.Single(m => m.Slot == 1);
        Advance(city, extra.DesiredAt + 15); routes.Accessible = false;
        Advance(city, extra.DesiredAt + 40);
        Assert.True(extra.Excursion!.Unresolved);
        var excursion = extra.Excursion; string receipt = extra.IntentId;
        var staged = BriefHomeArrival(city, routes); city = staged.City;
        Advance(city, staged.Arrival);
        Assert.Same(excursion, extra.Excursion); Assert.True(extra.Excursion!.Unresolved);
        Assert.Equal(receipt, extra.IntentId); Assert.Equal(1, extra.Purchases);
        Assert.False(extra.NeedsHomeResync); Assert.Single(market.Orders, id => id == receipt);
    }
}
