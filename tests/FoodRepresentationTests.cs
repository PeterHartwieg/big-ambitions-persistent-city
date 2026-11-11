using System;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed partial class FoodCommerceTests
{
    private sealed class LocalMarket : IFoodMarket, ILocalFoodMarket
    {
        private readonly Market inner;
        public LocalMarket(Market inner) { this.inner = inner; }
        public int LocalReads, SingleReads;
        public double EarliestMinute => inner.EarliestMinute;
        public System.Collections.Generic.IReadOnlyList<FoodBusiness> Read(double minute)
            => throw new InvalidOperationException("A local decision must not enumerate native supply citywide.");
        public System.Collections.Generic.IReadOnlyList<FoodBusiness> ReadLocal(Place origin, double minute, double tolerance, int maximumCandidates)
        {
            Assert.Equal(8, maximumCandidates); Assert.True(tolerance > 0);
            LocalReads++; return inner.Read(minute);
        }
        public FoodBusiness? ReadBusiness(string id, double minute)
        { SingleReads++; return inner.Read(minute).SingleOrDefault(b => b.Id == id); }
        public FoodSale Buy(ResidentMeal meal, double minute) => inner.Buy(meal, minute);
    }
    private sealed class ValidatedWalking : IRoutePlanner, IWalkingQuoteValidity
    {
        public int Calls, Validations;
        public bool Accessible = true;
        public double Minutes = 5;
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences)
        {
            Calls++;
            return !Accessible ? null : new Route { Legs = new() {
                new RouteLeg { From = from.Position, To = to.Position, Minutes = Minutes, Mode = TravelMode.Walk } } };
        }
        public bool IsWalkingQuoteValid(Place from, Place to, Route route, TravelPreferences preferences)
        {
            Validations++;
            return Accessible && route.Legs.All(l => l.Minutes == Minutes)
                && Position.Distance(from.Position, route.Legs.First().From) < .001
                && Position.Distance(to.Position, route.Legs.Last().To) < .001;
        }
    }

    // Logs planner calls; a scoped subclass brackets each shared-quote request. The same validity
    // sequence goes to both, so a scope may change native work but never Core's calls or decisions.
    private class RecordingWalking : IRoutePlanner, IWalkingQuoteValidity
    {
        // Sequence ignores scopes; Log marks them and anything called outside one.
        public readonly System.Collections.Generic.List<string> Sequence = new(), Log = new();
        public bool Open;
        public int Calls, Validations;
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences)
        {
            Calls++; Sequence.Add("plan"); Log.Add(Open ? "plan" : "unscoped-plan");
            return new Route { Legs = new() { new RouteLeg { From = from.Position, To = to.Position, Minutes = 5, Mode = TravelMode.Walk } } };
        }
        public bool IsWalkingQuoteValid(Place from, Place to, Route route, TravelPreferences preferences)
        {
            // Every fifth check refuses, exercising stale-quote replacement and refused replacements.
            bool valid = ++Validations % 5 != 3;
            Sequence.Add(valid ? "valid" : "invalid"); Log.Add(!Open ? "unscoped-validation" : valid ? "valid" : "invalid");
            return valid;
        }
    }
    private sealed class ScopedWalking : RecordingWalking, IWalkingQuoteOperation
    {
        public int Begins;
        public void BeginWalkingQuote() { Assert.False(Open); Open = true; Begins++; Log.Add("["); }
        public void EndWalkingQuote() { Assert.True(Open); Open = false; Log.Add("]"); }
    }

    [Fact]
    public void K1RequestsOpenNoSharedQuoteScope()
    {
        var (city, market, _) = Ready(atWork: true);
        var scoped = new ScopedWalking(); city = city.RebuildRoutes(scoped);
        Advance(city, city.State.Food!.Residents[0].DesiredAt + 60);
        Assert.Single(market.Orders); Assert.Equal(0, scoped.Begins); Assert.Equal(0, scoped.Validations);
    }

    [Theory]
    [InlineData(2)] [InlineData(7)] [InlineData(12)] [InlineData(25)]
    public void RepresentedExcursionSurvivesReloadWithoutDuplicatePurchaseOrArrival(double offset)
    {
        var (city, market, routes) = Ready(atWork: true, representation: 2);
        var (control, controlMarket, _) = Ready(atWork: true, representation: 2);
        double desired = city.State.Food!.Residents[0].DesiredAt;
        Advance(city, desired + offset, 1);
        city = Reload(city, market, routes);
        Advance(city, desired + 80, 1); Advance(control, desired + 80);
        Assert.Equal(controlMarket.Orders, market.Orders);
        Assert.Equal(DevelopmentChain.Write(control.State), DevelopmentChain.Write(city.State));
        Assert.All(city.State.Food!.Residents, m => Assert.Null(m.Excursion));
    }

    [Fact]
    public void RepresentationFactorCannotExpandExistingWalletsOrAcceptCorruptSlotOwnership()
    {
        var (city, market, routes) = Ready(atWork: true);
        city.State.Food!.RepresentationFactor = 2;
        Assert.Throws<SaveValidationException>(() => DevelopmentChain.Write(city.State));
        Assert.Throws<InvalidOperationException>(() => new CitySimulator(city.State, routes).AttachFoodMarket(market));
    }

    [Fact]
    public void RepresentationMatchesAcrossSchedulerBudgetsAndMealReloads()
    {
        var (a, ma, _) = Ready(count: 8, atWork: true, threeMeals: true, representation: 2);
        var (b, mb, routes) = Ready(count: 8, atWork: true, threeMeals: true, representation: 2);
        a.EnableHouseholdFinances(); b.EnableHouseholdFinances();
        Advance(a, 4 * 1440);
        for (double time = 1440; time <= 4 * 1440; time += 3)
        {
            Advance(b, time, 1);
            if (time % 120 == 0) b = Reload(b, mb, routes);
        }
        Assert.Equal(ma.Orders, mb.Orders);
        Assert.Equal(DevelopmentChain.Write(a.State), DevelopmentChain.Write(b.State));
        Assert.All(a.State.Food!.Residents, m => Assert.False(m.Excursion?.Unresolved == true));
    }

    [Fact]
    public void StaffAndStockRejectionsRemainSeparateSavedEvidence()
    {
        var (city, market, _) = Ready(atWork: true);
        market.Capacity = 0; market.UnavailableReason = "No staffed counter";
        Advance(city, city.State.Food!.Residents.Single().DesiredAt + 1);
        var row = Assert.Single(city.State.Food.History);
        Assert.Equal(1, row.StaffRejections); Assert.Equal(0, row.StockRejections);
        Assert.Equal("b", row.BusinessId);
        Assert.Equal("", DevelopmentChain.Read(DevelopmentChain.Write(city.State)).Error);
    }

    [Fact]
    public void DelayedReceiptBelongsToOriginalBusinessAfterSamePlaceReplacement()
    {
        var (city, market, _) = Ready(atWork: true);
        market.ThrowAfterSale = true;
        double desired = city.State.Food!.Residents.Single().DesiredAt;
        Advance(city, desired + 40); Assert.Equal(MealStatus.Uncertain, city.State.Food.Residents.Single().Status);
        market.BusinessId = "replacement"; city.State.Food.Shops.Single().BusinessId = "replacement";
        market.ThrowAfterSale = false; market.ConfirmEvidence = true;
        Advance(city, 4 * 1440);
        Assert.Equal(1, city.State.Food.History.Where(h => h.BusinessId == "b").Sum(h => h.Purchases));
        Assert.True(city.State.Food.History.Where(h => h.BusinessId == "replacement").Sum(h => h.Purchases) > 0);
        Assert.Equal("", DevelopmentChain.Read(DevelopmentChain.Write(city.State)).Error);
    }

    [Fact]
    public void CommittedRoutineJourneyCannotSpawnARepresentedMealAtItsOldStop()
    {
        var (city, market, routes) = Ready(atWork: true, representation: 2);
        double desired = city.State.Food!.Residents[0].DesiredAt;
        Advance(city, desired - 3);
        var person = city.State.Citizens.Single();
        var trip = new Route { Legs = new() { new RouteLeg { Mode = TravelMode.Walk,
            From = city.State.Places.Single(p => p.Id == "work").Position,
            To = city.State.Places.Single(p => p.Id == "home").Position, Minutes = 30 } } };
        person.Phase = CitizenPhase.Travelling; person.Journey = trip;
        person.DepartureMinute = city.State.Minute; person.NextEventMinute = city.State.Minute + trip.Minutes;
        person.DestinationId = person.HomeId; person.DestinationActivity = Activity.Home;
        var car = person.Car;
        city = city.RebuildRoutes(routes);
        Advance(city, desired + 1);
        Assert.Empty(market.Orders);
        Assert.All(city.State.Food!.Residents, m => Assert.Null(m.Excursion));
        Assert.Same(trip, person.Journey); Assert.Same(car, person.Car);
        Assert.Null(person.MealOuting);
    }
}
