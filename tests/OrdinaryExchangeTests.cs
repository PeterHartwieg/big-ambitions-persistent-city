using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using Xunit;
using Activity = PersistentCity.Core.Activity;

namespace PersistentCity.Tests;

public sealed class OrdinaryExchangeTests
{
    internal sealed class Ownership : IOrdinaryVisitOwnership
    {
        internal long Hour = 25;
        internal bool Player, Missing;
        internal OrdinaryVisitClass Kind = OrdinaryVisitClass.Retail;
        public bool Pending => false;
        public void Continue() { }
        public void ObserveBoundary(double minute) { }
        public bool TryGet(string id, out OrdinaryVisitTerms terms)
        { terms = new OrdinaryVisitTerms(Hour, Player, Kind); return !Missing; }
    }
    internal static ResidentBudget Wallet(int cents = 10000) => new() { OpeningBalance = Money.FromCents(cents), Balance = Money.FromCents(cents) };
    internal static CitySimulator Arrival(Ownership ownership, double at = 1501, Activity activity = Activity.Shop)
    {
        var state = Population.Create(new[] { new CitizenIdentity { Id = "person", Name = "Person", District = "d", Persona = Persona.Foodie, Age = 30 } },
            new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, District = "d", Capacity = 1 },
                new Place { Id = "venue", Name = "Venue", Kind = activity == Activity.Shop ? PlaceKind.Shop : PlaceKind.Leisure, Position = new Position(10, 0, 0) } },
            "ordinary", "1", 7, at - 1);
        var p = state.Citizens[0]; p.Phase = CitizenPhase.Travelling; p.PlaceId = "home"; p.DestinationId = "venue";
        p.DestinationActivity = activity; p.ActivityUntilMinute = at + 30; p.NextEventMinute = at; p.DepartureMinute = at - 1;
        p.Journey = new Route { Legs = { new RouteLeg { From = state.Places[0].Position, To = state.Places[1].Position, Minutes = 1, Mode = TravelMode.Walk } } }.Commit();
        state.Food = new FoodCommerceState { RepresentationFactor = 1, HouseholdFinanceVersion = 1, MealRhythmVersion = MealRhythm.PolicyVersion };
        var city = new CitySimulator(state, new AttendanceWageTests.Routes()); city.AttachFoodMarket(new AttendanceWageTests.Market());
        var b = state.Food.Residents[0].Budget!; b.NextPostingMinute = 2880; b.LastPostedDay = 1;
        b.HomeFoodFundedDay = 1; b.OpeningBalance = b.Balance = Money.FromCents(100000);
        city.AttachOrdinaryExchanges(ownership); return city.RebuildRoutes(new AttendanceWageTests.Routes());
    }
    internal static void Drain(CitySimulator city, double at, int budget = 64)
    { while (!city.AdvanceTo(at, budget).CaughtUp) { } }
    private static ResidentBudget Budget(CitySimulator city) => city.State.Food!.Residents[0].Budget!;

    [Fact]
    public void AdmissionDebitsExactlyOneOwnCounterpartAndWatermarkSurvivesReload()
    {
        var city = Arrival(new Ownership()); var b = Budget(city); var opening = b.Balance;
        Drain(city, 1501);
        Assert.Equal(Money.FromCents(500), b.Ledger.Discretionary);
        Assert.Equal(opening - b.Ledger.Discretionary, b.Balance); Assert.Equal(1501, b.Ledger.VisitWatermark);
        Assert.Single(city.State.Citizens[0].RecentVisits);
        Assert.True(HouseholdFinance.CashConserved(b));
        var row = Assert.Single(city.State.Economy.Outside); Assert.Equal(b.Ledger.Discretionary.Units, row.RetailUnits);
        Assert.Equal(BigInteger.One, row.RetailVisits);
        string payload = SaveCodec.Write(city.State); var read = SaveWire.Read(payload);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var saved = read.State!.Food!.Residents[0].Budget!;
        Assert.False(OrdinaryExchanges.TryPay(saved, read.State.Economy, OrdinaryVisitClass.Retail, Money.FromCents(500), 1501.9));
        var next = new CitySimulator(read.State, new AttendanceWageTests.Routes()); next.AttachFoodMarket(new AttendanceWageTests.Market()); next.AttachOrdinaryExchanges(new Ownership());
        Drain(next, 1502); Assert.Equal(Money.FromCents(500), saved.Ledger.Discretionary);
        Assert.Equal(BigInteger.One, Assert.Single(read.State.Economy.Outside).RetailVisits);
    }
    [Theory]
    [InlineData("player")] [InlineData("missing")] [InlineData("stale")] [InlineData("hour-flip")] [InlineData("reload")]
    public void UnknownStaleAndPlayerOwnershipKeepActivityWithoutDebit(string mode)
    {
        var owner = new Ownership(); double at = mode == "hour-flip" ? 1560 : 1501;
        var city = Arrival(owner, at); var b = Budget(city);
        if (mode == "player") owner.Player = true;
        if (mode == "missing") owner.Missing = true;
        if (mode == "stale") owner.Hour--;
        if (mode == "reload")
        {
            var read = SaveWire.Read(SaveCodec.Write(city.State)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            city = new CitySimulator(read.State!, new AttendanceWageTests.Routes()); city.AttachFoodMarket(new AttendanceWageTests.Market());
            var index = new OrdinaryOwnershipIndex(city.State.Places, city.State.Minute, _ => new OrdinaryVisitTerms(0, false, OrdinaryVisitClass.Retail));
            city.AttachOrdinaryExchanges(index); b = Budget(city);
        }
        Drain(city, at);
        Assert.Equal(Money.Zero, b.Ledger.Discretionary); Assert.Empty(city.State.Economy.Outside);
        Assert.Single(city.State.Citizens[0].RecentVisits); Assert.Equal(Activity.Shop, city.State.Citizens[0].Activity);
    }
    [Theory]
    [InlineData("reserve")] [InlineData("arrears")] [InlineData("hold")] [InlineData("overflow")]
    public void RefusesBeforeAdmissionAndDebit(string reason)
    {
        var city = Arrival(new Ownership()); var b = Budget(city);
        if (reason == "reserve") b.OpeningBalance = b.Balance = Money.FromCents(b.DailyHousingCents + b.DailyEssentialsCents + b.DailyHomeFoodCents + 499);
        if (reason == "arrears") b.Arrears = Money.FromCents(1);
        if (reason == "hold") { b.Balance -= Money.FromCents(1); b.HeldPurchase = Money.FromCents(1); }
        if (reason == "overflow") { b.Ledger = new OwnerAccounts { Discretionary = Money.FromUnits(long.MaxValue) }; }
        var balance = b.Balance; Drain(city, 1501);
        Assert.Equal(balance, b.Balance); Assert.Empty(city.State.Economy.Outside);
        Assert.Equal(0, b.Ledger.VisitWatermark); Assert.Empty(city.State.Citizens[0].RecentVisits);
        Assert.NotEqual("venue", city.State.Citizens[0].DestinationId);
    }
    [Fact]
    public void FreeParkIgnoresMoneyAndMealOutingDoesNotChargeOrdinaryRetail()
    {
        var free = Arrival(new Ownership { Kind = OrdinaryVisitClass.ActivityOnly }, activity: Activity.Leisure);
        Budget(free).Balance = Budget(free).OpeningBalance = Money.Zero; Drain(free, 1501);
        Assert.Single(free.State.Citizens[0].RecentVisits); Assert.Empty(free.State.Economy.Outside);
        var meal = Arrival(new Ownership()); var p = meal.State.Citizens[0];
        p.MealOuting = new MealOuting { OriginId = "home", OriginActivity = Activity.Home, ResumeEvent = 1600, ResumeUntil = 1600 };
        Drain(meal, 1501); Assert.Equal(Money.Zero, Budget(meal).Ledger.Discretionary);
    }
    [Fact]
    public void OwnershipIndexStartsNextHourAndPublishesOnlyCompleteSixteenPlaceUnits()
    {
        var places = Enumerable.Range(0, 33).Select(i => new Place { Id = "p" + i }).ToArray();
        int calls = 0; bool player = false, fail = false; OrdinaryOwnershipIndex? index = null;
        index = new OrdinaryOwnershipIndex(places, 1500, p => {
            calls++; if (calls == 16) Assert.False(index!.TryGet(places[0].Id, out _));
            if (fail && p.Id == "p5") throw new InvalidOperationException("fixture read failed");
            return new OrdinaryVisitTerms(0, player, OrdinaryVisitClass.Retail); });
        index.ObserveBoundary(1559); Assert.False(index.Pending); Assert.False(index.TryGet("p0", out _));
        index.ObserveBoundary(1560); Assert.True(index.Pending); index.Continue();
        Assert.Equal(16, index.LastUnitPlaces); Assert.Equal(16, index.NativeReads);
        Assert.True(index.TryGet("p0", out var terms)); Assert.Equal(26, terms.Hour); Assert.False(terms.PlayerOwned);
        Assert.False(index.TryGet("p16", out _));
        // The callback assertion above is specific to the first unit.
        while (index.Pending) index.Continue(); Assert.Equal(33, index.NativeReads);
        player = true; index.ObserveBoundary(1620); Assert.False(index.TryGet("p0", out _));
        fail = true; Assert.Throws<InvalidOperationException>(() => index.Continue()); Assert.False(index.TryGet("p0", out _));
        fail = false; index.Continue(); Assert.True(index.TryGet("p0", out terms)); Assert.True(terms.PlayerOwned);
    }
    [Fact]
    public void BoundaryRefreshHoldsEventsAndNativeClockUntilBoundedWorkCompletes()
    {
        var city = Arrival(new Ownership(), 1560);
        var places = Enumerable.Range(0, 33).Select(i => new Place { Id = "x" + i }).Append(city.State.Places[1]).ToArray();
        var index = new OrdinaryOwnershipIndex(places, 1559, _ => new OrdinaryVisitTerms(0, true, OrdinaryVisitClass.Retail));
        Drain(city, AccountingClock.PreviousEpochTarget(1560));
        city.AttachOrdinaryExchanges(index); Assert.True(city.BeginAccountingObservations(1560));
        Assert.False(city.IsCaughtUpTo(1560)); city.AdvanceTo(1560, 1);
        Assert.Equal(16, index.NativeReads); Assert.Empty(city.State.Citizens[0].RecentVisits);
        Assert.False(city.IsCaughtUpTo(1560)); Drain(city, 1560, 1);
        Assert.Equal(34, index.NativeReads); Assert.Equal(Money.Zero, Budget(city).Ledger.Discretionary);
        Assert.Single(city.State.Citizens[0].RecentVisits);
    }
    [Fact]
    public void VisitorEndowmentSpendsWithoutHousingEssentialsOrDailyIncomeAndRemainsConserved()
    {
        var visitor = new VisitorRoutine { Identity = new CitizenIdentity { Id = "pv:1", Age = 30, Persona = Persona.Foodie } };
        var person = new VisitorPerson { Id = "pv:1", Stay = new VisitorStay() }; VisitorSchedules.Link(person, visitor);
        var b = HouseholdFinance.Create(visitor, 7, 1440, null); var income = b.IncomeReceived;
        var ledger = new EconomicLedger();
        for (int day = 1; day <= 21; day++)
        {
            HouseholdFinance.PostDay(b, day * 1440, visitor, visitorEndowment: true);
            OrdinaryExchanges.TryPay(b, ledger, OrdinaryVisitClass.Leisure, Money.FromCents(1200), day * 1440 + 10);
            Assert.Equal(Money.Zero, b.EssentialsPaid); Assert.Equal(Money.Zero, b.HomeFoodPaid); Assert.Equal(Money.Zero, b.Arrears);
            Assert.Equal(income, b.IncomeReceived); Assert.True(HouseholdFinance.CashConserved(b));
        }
        Assert.True(b.Ledger.Discretionary > Money.Zero); Assert.Equal(0, b.DaysWithoutHomeFood);
        Assert.InRange(ledger.Outside.Count, 1, 14);
    }
    [Fact]
    public void WideTotalsAndRolloverAreExactAndFreeOutputIsTotalsOnly()
    {
        var ledger = new EconomicLedger(); var wide = BigInteger.Pow(10, 24);
        ledger.Outside.Add(new OutsideSpendingDay { Day = 1, RetailUnits = wide, RetailVisits = wide });
        var b = Wallet(); Assert.True(OrdinaryExchanges.TryPay(b, ledger, OrdinaryVisitClass.Retail, Money.FromCents(500), 1500));
        Assert.Equal(wide + Money.FromCents(500).Units, ledger.Outside[0].RetailUnits);
        Assert.Equal(wide + 1, ledger.Outside[0].RetailVisits);
        OrdinaryExchanges.Prune(ledger, 15); Assert.Empty(ledger.Outside);
        Assert.Equal(new[] { "FirstDay", "LastDay", "Units", "Visits" }, typeof(OutsideSpendingTotal).GetProperties().Select(p => p.Name));
        var city = Arrival(new Ownership()); Drain(city, 1501); var view = city.FreeOutsideSpending();
        Assert.Equal(Money.FromCents(500).Units, view.Units); Assert.Equal(BigInteger.One, view.Visits);
        string text = view.Text(); Assert.Contains("Spending outside your businesses", text); Assert.Contains("modeled", text);
        Assert.DoesNotContain("venue", text); Assert.DoesNotContain("Person", text);
    }
    [Fact]
    public void PriceTableIsBoundedAndWrongCounterpartCannotDebit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrdinaryVisitPrices(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrdinaryVisitPrices(500, 10001));
        var b = Wallet(); var ledger = new EconomicLedger();
        Assert.False(OrdinaryExchanges.TryPay(b, ledger, OrdinaryVisitClass.ActivityOnly, Money.FromCents(1), 1501));
        Assert.False(OrdinaryExchanges.TryPay(b, ledger, OrdinaryVisitClass.Retail, Money.Zero, 1501));
        Assert.Equal(b.OpeningBalance, b.Balance); Assert.Empty(ledger.Outside);
    }
    [Fact]
    public void PendingRouteRestartAndSaveCannotDebitBeforeArrival()
    {
        double start = 1440 + 1050 + (int)(StableRandom.Hash("person:hours", 7) % 31) - 15;
        var owner = new Ownership { Hour = (long)(start / 60) };
        var city = Arrival(owner); var p = city.State.Citizens[0];
        p.Phase = CitizenPhase.AtPlace; p.PlaceId = p.HomeId; p.Activity = Activity.Home;
        p.DestinationId = ""; p.DepartureMinute = 0; p.Journey = null; p.ShopId = "venue";
        p.NextEventMinute = p.ActivityUntilMinute = start;
        p.Preferences.OwnsCar = false; p.Preferences.AllowTransit = false;
        var planner = new ResumablePlanningTests.DeferredPlanner { ContinuesFor = _ => 3 };
        city = city.RebuildRoutes(planner);
        while (!city.PlanningPending) Assert.False(city.AdvanceFrameSlice(start, 1).CaughtUp);
        Assert.Equal(Money.Zero, Budget(city).Ledger.Discretionary); Assert.Empty(city.State.Economy.Outside);
        string saved = SaveCodec.Write(city.State);
        // Rebuilding abandons the detached departure; no ordinary debit has happened to roll back.
        city = city.RebuildRoutes(new AttendanceWageTests.Routes());
        Assert.Equal(saved, SaveCodec.Write(city.State)); Drain(city, start + 1);
        Assert.Equal(Money.FromCents(500), Budget(city).Ledger.Discretionary);
        Assert.Equal(BigInteger.One, Assert.Single(city.State.Economy.Outside).RetailVisits);
        var read = SaveWire.Read(saved); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var loaded = new CitySimulator(read.State!, new AttendanceWageTests.Routes()); loaded.AttachFoodMarket(new AttendanceWageTests.Market()); loaded.AttachOrdinaryExchanges(owner);
        Drain(loaded, start + 1); Assert.Equal(Money.FromCents(500), Budget(loaded).Ledger.Discretionary);
    }
    [Fact]
    public void SourceGuardKeepsNativeWritesAndForeignLedgersOutOfOrdinaryExchangePath()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "AGENTS.md"))) root = Directory.GetParent(root)!.FullName;
        string core = File.ReadAllText(Path.Combine(root, "Core/OrdinaryExchanges.cs"));
        foreach (string native in new[] { "UnityEngine", "BuildingRegistration", "BusinessSimulator", "SaveGameManager", ".Buy(" })
            Assert.DoesNotContain(native, core);
        string adapter = File.ReadAllText(Path.Combine(root, "Runtime/NativeOrdinaryOwnership.cs"));
        Assert.Contains("reg.RentedByPlayer", adapter); Assert.Contains("reg.businessOwnerRivalId", adapter);
        foreach (string write in new[] { "RentedByPlayer =", "businessOwnerRivalId =", "retailPrices =", "Bank", "Payroll", "CustomerEntriesHelper" })
            Assert.DoesNotContain(write, adapter);
        string panel = File.ReadAllText(Path.Combine(root, "Runtime/FoodEconomyPanel.cs"));
        // F6g owns the panel projection; render its week comparison once, without the old 14-day total.
        Assert.DoesNotContain("simulation.FreeOutsideSpending().Text()", panel);
        // F9e: the panel reaches it through the city view's one adapter.
        Assert.Equal(1, panel.Split("CityViewReports.AppendCityEconomy(").Length - 1);
        Assert.Equal(1, File.ReadAllText(Path.Combine(root, "Core/CityView.cs")).Split("FoodFeedbackV2.AppendCity(").Length - 1);
    }

    [Fact]
    public void RuntimePricesClampToTheBoundedTable()
    {
        var settings = Newtonsoft.Json.JsonConvert.DeserializeObject<PrototypeSettings>("{\"OrdinaryRetailVisitCents\":0,\"OrdinaryLeisureVisitCents\":999999}")!;
        Assert.Equal(1, settings.OrdinaryRetailVisitCents); Assert.Equal(10000, settings.OrdinaryLeisureVisitCents);
        Assert.Equal(500, new PrototypeSettings().OrdinaryRetailVisitCents); Assert.Equal(1200, new PrototypeSettings().OrdinaryLeisureVisitCents);
    }

    [Fact]
    public void FrozenAttendanceWriterGoldenIsUnchangedWithOrdinaryPolicyAttached()
    {
        var city = AttendanceWageTests.Fresh(42);
        city.AttachOrdinaryExchanges(new OrdinaryOwnershipIndex(Array.Empty<Place>(), city.State.Minute, _ => default));
        Drain(city, 1440 + 1100);
        // F6f pins the attendance writer; F6g separately pins the new current writer.
        Assert.Equal(CityState.EconomicLedgerSchema + 1, CityState.AttendanceSchema);
        Assert.Equal("23d2928c71a4a954255154702a93deca1e63b53249fd9332813a1c7ee762082e", EconomicLedgerTests.Sha(DevelopmentChain.WriteAttendance(city.State)));
    }
    [Fact]
    public void NoOutsideProjectionOrStringIsBuiltBySimulationEvents()
    {
        var city = Arrival(new Ownership()); Drain(city, 1501);
        Assert.Equal(0, city.OutsideSpendingRequests);
        var view = city.FreeOutsideSpending(); Assert.Equal(1, city.OutsideSpendingRequests);
        Assert.Contains("Spending outside your businesses", view.Text());
    }
    [Fact]
    public void VisitorPostingAndReserveAreEndowmentOnlyAfterReloadAtMidnight()
    {
        var (state, market) = Schema28Tests.Fresh();
        var city = new CitySimulator(state, new FreshCityTraceTests.Walk()); city.AttachFoodMarket(market);
        city.AttachOrdinaryExchanges(new OrdinaryOwnershipIndex(Array.Empty<Place>(), state.Minute, _ => default));
        Drain(city, 2880);
        var ids = state.Visitors.Select(v => v.Id).ToHashSet();
        foreach (var meal in state.Food!.Residents.Where(m => ids.Contains(m.ResidentId)))
        { Assert.Equal(Money.Zero, meal.Budget!.EssentialsPaid); Assert.Equal(Money.Zero, meal.Budget.IncomeReceived); }
        var read = SaveWire.Read(SaveCodec.Write(state)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        city = new CitySimulator(read.State!, new FreshCityTraceTests.Walk()); city.AttachFoodMarket(market);
        city.AttachOrdinaryExchanges(new OrdinaryOwnershipIndex(Array.Empty<Place>(), read.State!.Minute, _ => default));
        Drain(city, 4320);
        foreach (var meal in read.State.Food!.Residents.Where(m => ids.Contains(m.ResidentId)))
        {
            Assert.Equal(Money.Zero, meal.Budget!.EssentialsPaid); Assert.Equal(Money.Zero, meal.Budget.HomeFoodPaid);
            Assert.True(HouseholdFinance.CashConserved(meal.Budget));
        }
        Assert.Empty(GraphAudit.Compare(read.State, SaveCodec.Capture(read.State).Wire).Differences);
    }

    [Fact]
    public void DatedWideTokenLimitRefusesBeforePublishingAnyMoneyOrCount()
    {
        var maximum = BigInteger.Pow(10, 26) - 1;
        var row = new OutsideSpendingDay { Day = 1, RetailUnits = maximum, RetailVisits = maximum / Money.FromCents(500).Units };
        var ledger = new EconomicLedger(); ledger.Outside.Add(row); var budget = Wallet();
        var visits = row.RetailVisits;
        Assert.False(OrdinaryExchanges.TryPay(budget, ledger, OrdinaryVisitClass.Retail, Money.FromCents(500), 1501));
        Assert.Equal(budget.OpeningBalance, budget.Balance); Assert.Equal(default, budget.Ledger);
        Assert.Equal(maximum, row.RetailUnits); Assert.Equal(visits, row.RetailVisits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VisitorFeedbackIsRestoredBeforeEventsInEitherAttachmentOrder(bool marketFirst)
    {
        var (state, market) = Schema28Tests.Fresh();
        CitySimulator Attach(CityState snapshot)
        {
            var next = new CitySimulator(snapshot, new FreshCityTraceTests.Walk());
            if (marketFirst) next.AttachFoodMarket(market);
            next.AttachOrdinaryExchanges(new OrdinaryOwnershipIndex(Array.Empty<Place>(), snapshot.Minute, _ => default));
            if (!marketFirst) next.AttachFoodMarket(market);
            return next;
        }
        var city = Attach(state); Drain(city, 2880);
        var visitor = state.Food!.Residents.Single(m => m.ResidentId == state.Visitors[0].Id).Budget!;
        // Leave an endowment below the resident reserve, with no hold or missing visitor basket.
        Assert.True(HouseholdFinance.HoldPurchase(visitor, visitor.Balance - Money.FromCents(1)));
        HouseholdFinance.ConfirmPurchase(visitor);
        var before = city.FoodMoney!.Snapshot();
        Assert.Equal(0, before.WithoutHomeBasket);
        string payload = SaveCodec.Write(state);
        var read = SaveWire.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var restored = Attach(read.State!);
        Assert.Equal(state.Minute, restored.State.Minute);
        Assert.Equal(before, restored.FoodMoney!.Snapshot());
        Assert.Equal(FoodMoneyCounters.Rescan(restored.State.Food!), restored.FoodMoney.Snapshot());
        Assert.All(restored.State.Food!.Residents, m => Assert.Equal(restored.FindCitizen(m.ResidentId)!.IsVisitor, m.Budget!.VisitorEndowment));
        Assert.Equal(payload, SaveCodec.Write(restored.State));
    }

    [Theory]
    [InlineData(Activity.Shop)]
    [InlineData(Activity.Leisure)]
    public void SameVenueContinuationRetainsTheVisitAfterItsPaymentDrainsTheLimit(Activity activity)
    {
        int shift = (int)(StableRandom.Hash("person:hours", 7) % 31) - 15;
        double start = 1440 + (activity == Activity.Shop ? 1050 : 1140) + shift;
        var owner = new Ownership { Hour = (long)((start + 1) / 60), Kind = activity == Activity.Shop ? OrdinaryVisitClass.Retail : OrdinaryVisitClass.Leisure };
        var city = Arrival(owner, start + 1, activity); var person = city.State.Citizens[0];
        if (activity == Activity.Leisure) person.Identity.Persona = Persona.Sporty;
        person.ShopId = activity == Activity.Shop ? "venue" : "";
        person.LeisureId = activity == Activity.Leisure ? "venue" : "";
        // First admitted slot ends while the next routine slot continues at the same venue.
        person.ActivityUntilMinute = start + 21;
        var budget = Budget(city); var price = new OrdinaryVisitPrices().Price(owner.Kind);
        budget.OpeningBalance = budget.Balance = HouseholdFinance.SpendingReserve(budget) + price;
        city = city.RebuildRoutes(new AttendanceWageTests.Routes());
        Drain(city, start + 1);
        Assert.Equal(price, budget.Ledger.Discretionary); Assert.Equal(Money.Zero, HouseholdFinance.SpendingLimit(budget));
        var arrival = Assert.Single(person.RecentVisits);
        var nextSlot = DailyRoutine.At(person, city.State.Seed, start + 21);
        Assert.Equal(activity, nextSlot.Activity); Assert.True(nextSlot.EndsAt > start + 21);
        owner.Hour = (long)((start + 21) / 60);
        Drain(city, start + 21);
        Assert.Equal("venue", person.PlaceId); Assert.Equal(activity, person.Activity);
        Assert.Equal(CitizenPhase.AtPlace, person.Phase); Assert.Equal(nextSlot.EndsAt, person.NextEventMinute);
        Assert.Equal(0, person.MissedActivities); Assert.Same(arrival, Assert.Single(person.RecentVisits));
        Assert.Equal(price, budget.Ledger.Discretionary); Assert.Equal((long)(start + 1), budget.Ledger.VisitWatermark);
        Assert.Equal(BigInteger.One, city.FreeOutsideSpending().Visits); Assert.True(HouseholdFinance.CashConserved(budget));
    }

}
