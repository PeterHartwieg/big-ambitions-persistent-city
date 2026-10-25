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

    [Fact]
    public void RepresentationCreatesIndependentSavedConsumersWithoutPhysicalResidentsOrCars()
    {
        var (city, market, routes) = Ready(atWork: true, representation: 2);
        var person = Assert.Single(city.State.Citizens);
        var car = person.Car;
        var food = city.State.Food!;
        Assert.Equal(2, food.Residents.Count);
        Assert.Equal(new[] { "r0", "consumer:r0:1" }, food.Residents.Select(m => m.ResidentId));
        Assert.All(food.Residents, m => Assert.Equal("r0", m.AnchorId));
        city.EnableHouseholdFinances();
        var budgets = food.Residents.Select(m => m.Budget!).ToArray();
        Assert.NotSame(budgets[0], budgets[1]);
        Assert.NotEqual(budgets[0].BaseIncomeCents, budgets[1].BaseIncomeCents);
        var balances = budgets.Select(b => b.BalanceCents).ToArray();
        city = Reload(city, market, routes);
        Assert.Equal(balances, city.State.Food!.Residents.Select(m => m.Budget!.BalanceCents));
        Assert.Single(city.State.Citizens);
        Assert.Equal(car?.Id, city.State.Citizens.Single().Car?.Id);
        Assert.Equal("", food.Residents[0].PreferredProductId);
        Assert.True(FoodProducts.Known(food.Residents[1].PreferredProductId));
        Assert.InRange(food.Residents[1].PreferenceBonusCents, 50, 150);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void RepresentedPurchasesConsumeSeparateStockMoneyAndQueueSlots(int factor)
    {
        var (city, market, _) = Ready(atWork: true, representation: factor);
        city.EnableHouseholdFinances();
        double desired = city.State.Food!.Residents[0].DesiredAt;
        Advance(city, desired + 60);
        Assert.Equal(factor, market.Orders.Count);
        Assert.Equal(100 - factor, market.Units);
        Assert.Equal(factor, market.Orders.Distinct().Count());
        Assert.All(market.Times.Zip(market.Times.Skip(1)), p => Assert.True(p.Second - p.First >= 4 - 1e-7));
        Assert.All(city.State.Food.Residents, m => {
            Assert.Equal(1, m.Purchases); Assert.Equal(100, m.Budget!.PreparedFoodSpentCents);
            Assert.Equal(m.Budget.OpeningBalanceCents + m.Budget.IncomeReceivedCents,
                m.Budget.BalanceCents + m.Budget.PreparedFoodSpentCents + m.Budget.EssentialsPaidCents + m.Budget.HomeFoodPaidCents);
            Assert.Null(m.Excursion); Assert.False(m.NeedsHomeResync);
        });
        Assert.Single(city.State.Citizens);
        Assert.Equal(2, city.State.Citizens[0].CompletedJourneys);
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
        Assert.Equal(SaveCodec.Write(control.State), SaveCodec.Write(city.State));
        Assert.All(city.State.Food!.Residents, m => Assert.Null(m.Excursion));
    }

    [Fact]
    public void AnchorPersonalDetourDoesNotRelocateLaterRepresentedDecision()
    {
        var (city, market, routes) = Ready(atWork: true, representation: 2);
        var extra = city.State.Food!.Residents.Single(m => m.Slot == 1);
        double desired = extra.DesiredAt;
        // Delay the independent intention so slot zero is already at the shop.
        extra.DesiredAt += 6; extra.DueAt += 6;
        city = city.RebuildRoutes(routes);
        string? extraOrigin = null;
        city.FoodDecisionObserved += trace => { if (trace.Consumer.Id == extra.ResidentId) extraOrigin = trace.Intent.OriginPlaceId; };
        Advance(city, desired + 7);
        Assert.Equal("work", extraOrigin);
        Assert.Equal("work", city.State.Food!.Residents.Single(m => m.Slot == 1).Excursion!.OriginId);
        Assert.Equal("shop", city.State.Citizens.Single().PlaceId);
        Advance(city, desired + 60); Assert.Equal(2, market.Orders.Count);
    }

    [Fact]
    public void UncertainConsumerRetainsItsReceiptAndHoldWhileOtherConsumerContinues()
    {
        var (city, market, routes) = Ready(atWork: true, threeMeals: true, representation: 2);
        city.EnableHouseholdFinances();
        var extra = city.State.Food!.Residents.Single(m => m.Slot == 1);
        market.ThrowForResident = extra.ResidentId;
        double first = extra.DesiredAt;
        Advance(city, first + 60); extra = city.State.Food.Residents.Single(m => m.Slot == 1);
        Assert.Equal(MealStatus.Uncertain, extra.Status); Assert.Equal(100, extra.Budget!.HeldPurchaseCents);
        string receipt = extra.IntentId;
        city = Reload(city, market, routes); Advance(city, 3 * 1440);
        extra = city.State.Food!.Residents.Single(m => m.Slot == 1);
        Assert.Equal(receipt, extra.IntentId); Assert.Equal(MealStatus.Uncertain, extra.Status);
        Assert.Equal(100, extra.Budget!.HeldPurchaseCents);
        Assert.Single(market.Orders, o => o == receipt);
        Assert.True(city.State.Food.Residents.Single(m => m.Slot == 0).Purchases > 1);
    }

    [Fact]
    public void MissingReturnPreservesAwayOwnershipAcrossReloadAndSafeHomeStay()
    {
        var (city, market, _) = Ready(atWork: true, representation: 2);
        var routes = new ValidatedWalking(); city = city.RebuildRoutes(routes);
        double desired = city.State.Food!.Residents[0].DesiredAt;
        Advance(city, desired + 15); Assert.Equal(2, market.Orders.Count);
        routes.Accessible = false;
        Advance(city, desired + 40);
        var extra = city.State.Food.Residents.Single(m => m.Slot == 1);
        Assert.True(extra.Excursion!.Unresolved);
        string receipt = extra.IntentId;
        var read = SaveCodec.Read(SaveCodec.Write(city.State)); Assert.Equal("", read.Error);
        city = new CitySimulator(read.State!, routes); city.AttachFoodMarket(market);
        routes.Accessible = true; Advance(city, 3 * 1440);
        extra = city.State.Food!.Residents.Single(m => m.Slot == 1);
        Assert.True(extra.Excursion!.Unresolved); Assert.Equal(receipt, extra.IntentId);
        Assert.Equal(1, extra.Purchases);
    }

    [Fact]
    public void NativeClockPassingTheOpportunityCannotSilentlyCompleteAnOverrunReturn()
    {
        var (city, market, _) = Ready(atWork: true, representation: 2);
        var routes = new ValidatedWalking(); city = city.RebuildRoutes(routes);
        double desired = city.State.Food!.Residents[0].DesiredAt;
        Advance(city, desired + 2);
        market.EarliestMinute = city.State.Food.Residents.Single(m => m.Slot == 1).Deadline + 100;
        Advance(city, market.EarliestMinute + 1);
        var extra = city.State.Food.Residents.Single(m => m.Slot == 1);
        Assert.True(extra.Excursion!.Unresolved); Assert.False(extra.Excursion.Returning);
        Assert.Contains("exceeds", extra.Excursion.UnresolvedReason);
    }

    [Fact]
    public void SharedQuotesRevalidateCurrentAccessAndEndpointAndK1BypassesReuse()
    {
        var (one, _, _) = Ready(atWork: true);
        var original = new ValidatedWalking(); one = one.RebuildRoutes(original);
        double desired = one.State.Food!.Residents[0].DesiredAt;
        Advance(one, desired + 2); Assert.Equal(0, original.Validations);
        var (two, _, _) = Ready(atWork: true, representation: 2);
        var shared = new ValidatedWalking(); two = two.RebuildRoutes(shared);
        Advance(two, desired + 2);
        Assert.Equal(2, shared.Calls); Assert.True(shared.Validations >= 4);
        // The represented return must not use a quote whose endpoint moved.
        two.State.Places.Single(p => p.Id == "work").Position = new Position(9, 0, 0);
        Advance(two, desired + 40);
        Assert.True(two.State.Food!.Residents.Single(m => m.Slot == 1).Excursion!.Unresolved);
    }

    [Fact]
    public void RepresentationFactorCannotExpandExistingWalletsOrAcceptCorruptSlotOwnership()
    {
        var (city, market, routes) = Ready(atWork: true);
        city.State.Food!.RepresentationFactor = 2;
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(city.State));
        Assert.Throws<InvalidOperationException>(() => new CitySimulator(city.State, routes).AttachFoodMarket(market));
    }

    [Fact]
    public void IndependentSavedTasteChangesOnlyThatConsumersProductChoice()
    {
        var (city, market, _) = Ready(atWork: true, representation: 2);
        market.Products = new[] { FoodProducts.Croissant, FoodProducts.Burger, FoodProducts.HotDog };
        var scope = city.State.Food!.Shops.Single();
        scope.ProductId = market.Products[0]; scope.AdditionalProductIds = market.Products.Skip(1).ToList();
        var extra = city.State.Food.Residents.Single(m => m.Slot == 1);
        extra.PreferredProductId = FoodProducts.HotDog; extra.PreferenceBonusCents = 150;
        var choices = new System.Collections.Generic.Dictionary<string, string>();
        city.FoodDecisionObserved += trace => choices[trace.Consumer.Id] = trace.Intent.ProductId;
        Advance(city, extra.DesiredAt + 60);
        Assert.Equal(FoodProducts.HotDog, choices[extra.ResidentId]);
        Assert.NotEqual(choices[extra.ResidentId], choices["r0"]);
        Assert.Equal(2, market.Orders.Count);
    }

    [Fact]
    public void RepresentedCommittedTimingSurvivesNativePaceChangeWhileAnchorRetimes()
    {
        var (city, market, routes) = Ready(atWork: true, representation: 2);
        city.State.TravelTimingVersion = 1; city.State.TravelClockRate = 1;
        double desired = city.State.Food!.Residents[0].DesiredAt;
        Advance(city, desired + 2);
        var extra = city.State.Food.Residents.Single(m => m.Slot == 1);
        double arrival = extra.Excursion!.ArrivesAt, back = extra.ReturnMinutes;
        Assert.True(TravelTiming.Apply(city.State, 2, 10)); routes.Minutes *= 10;
        Assert.Equal(arrival, extra.Excursion.ArrivesAt); Assert.Equal(back, extra.ReturnMinutes);
        city = Reload(city, market, routes); Advance(city, desired + 40);
        extra = city.State.Food!.Residents.Single(m => m.Slot == 1);
        Assert.Null(extra.Excursion); Assert.Equal(1, extra.Purchases);
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
        Assert.Equal(SaveCodec.Write(a.State), SaveCodec.Write(b.State));
        Assert.All(a.State.Food!.Residents, m => Assert.False(m.Excursion?.Unresolved == true));
    }

    [Fact]
    public void ConfirmedReceiptSettlesCashOnceEvenWhenIndependentReturnIsBlocked()
    {
        var (city, market, _) = Ready(atWork: true, threeMeals: true, representation: 2);
        city.EnableHouseholdFinances();
        var routes = new ValidatedWalking(); city = city.RebuildRoutes(routes);
        var extra = city.State.Food!.Residents.Single(m => m.Slot == 1);
        market.ThrowForResident = extra.ResidentId;
        Advance(city, extra.DesiredAt + 15); routes.Accessible = false;
        Advance(city, extra.DesiredAt + 40);
        Assert.Equal(MealStatus.Uncertain, extra.Status); Assert.True(extra.Excursion!.Unresolved);
        Assert.Equal(100, extra.Budget!.HeldPurchaseCents);
        string originalReceipt = extra.IntentId;
        market.ConfirmEvidence = true; routes.Accessible = true;
        Advance(city, 3 * 1440);
        Assert.Equal(MealStatus.Paid, extra.Status); Assert.Equal(0, extra.Budget.HeldPurchaseCents);
        Assert.Equal(100, extra.Budget.PreparedFoodSpentCents); Assert.Equal(1, extra.Purchases);
        Assert.True(extra.Excursion!.Unresolved); Assert.Equal(originalReceipt, extra.IntentId);
        Assert.Single(market.Orders, key => key == originalReceipt);
        Assert.True(city.State.Food.Residents.Single(m => m.Slot == 0).Purchases > 1);
        var read = SaveCodec.Read(SaveCodec.Write(city.State)); Assert.Equal("", read.Error);
    }

    [Fact]
    public void ConsumerCannotResynchronizeAtHomeUntilItsReturnActuallyCompletes()
    {
        var (city, market, routes) = Ready(atWork: true, representation: 2);
        city.State.Food!.Residents.Single(m => m.Slot == 0).AllowanceCents = 0;
        var extra = city.State.Food.Residents.Single(m => m.Slot == 1);
        double desired = extra.DesiredAt;
        Advance(city, desired + 10);
        Assert.Equal(1, extra.Purchases);
        var person = city.State.Citizens.Single();
        // Independent routine change while the represented consumer is still eating away.
        person.PlaceId = person.HomeId; person.Activity = Activity.Home;
        person.NextEventMinute = person.ActivityUntilMinute = 10000;
        city = city.RebuildRoutes(routes);
        Advance(city, desired + 20);
        Assert.NotNull(extra.Excursion); Assert.Equal("work", extra.Excursion!.OriginId);
        Advance(city, desired + 31);
        Assert.Null(extra.Excursion); Assert.True(extra.NeedsHomeResync);
        double next = extra.DesiredAt;
        Advance(city, next + 40);
        Assert.False(extra.NeedsHomeResync); Assert.Equal(2, extra.Purchases);
        Assert.Equal(0, person.CompletedJourneys);
        Assert.Equal(2, market.Orders.Count);
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
        Assert.Equal("", SaveCodec.Read(SaveCodec.Write(city.State)).Error);
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
        Assert.Equal("", SaveCodec.Read(SaveCodec.Write(city.State)).Error);
    }

    [Fact]
    public void LocalDecisionAndArrivalNeverRequireCitywideNativeSupplyReads()
    {
        var (initial, market, routes) = Ready(atWork: true, representation: 2);
        var local = new LocalMarket(market);
        var city = new CitySimulator(initial.State, routes); city.AttachFoodMarket(local);
        double desired = city.State.Food!.Residents[0].DesiredAt;
        Advance(city, desired + 60);
        Assert.Equal(2, local.LocalReads); Assert.Equal(2, local.SingleReads);
        Assert.Equal(2, market.Orders.Count);
    }

    [Fact]
    public void AbstractConsumerIdentityCollisionFailsBeforeInitializingAnyWallet()
    {
        var (initial, market, routes) = Ready(count: 2, representation: 1);
        initial.State.Citizens[1].Identity.Id = FoodRepresentation.ConsumerId("r0", 1);
        initial.State.Food!.Residents.Clear(); initial.State.Food.RepresentationFactor = 2;
        var city = new CitySimulator(initial.State, routes);
        Assert.Throws<InvalidOperationException>(() => city.AttachFoodMarket(market));
        Assert.Empty(city.State.Food!.Residents);
    }

    [Fact]
    public void RepresentedArrivalWaitsForActualOpeningAndPreservesWaitAcrossReload()
    {
        var (city, market, routes) = Ready(atWork: true, representation: 2);
        double desired = city.State.Food!.Residents[0].DesiredAt;
        market.OpenFromMinute = (int)(desired % 1440) + 10;
        city.State.Places.Single(p => p.Id == "shop").WeeklyHours = market.Read(city.State.Minute)[0].Hours;
        Advance(city, desired + 6);
        Assert.Empty(market.Orders);
        var extra = city.State.Food.Residents.Single(m => m.Slot == 1);
        Assert.True(extra.Excursion!.AdmissionAt > extra.Excursion.ArrivesAt);
        city = Reload(city, market, routes);
        Advance(city, desired + 60);
        Assert.Equal(1, city.State.Food!.Residents.Single(m => m.Slot == 1).Purchases);
        Assert.All(market.Times, at => Assert.True(at % 1440 >= market.OpenFromMinute + 4));
        Assert.Null(city.State.Food!.Residents.Single(m => m.Slot == 1).Excursion);
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
