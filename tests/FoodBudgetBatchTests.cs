using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

// Batched midnight budget postings against the earlier one-posting-per-event stepping. The reference
// runs the same code with BudgetPostingsPerEvent = 1, which is the earlier StepBudget exactly.
public sealed class FoodBudgetBatchTests
{
    private const double Midnight = 2 * 1440.0;

    private sealed class Walking : IRoutePlanner
    {
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences) => new()
        { Legs = new() { new RouteLeg { From = from.Position, To = to.Position, Minutes = 5, Mode = TravelMode.Walk } } };
    }

    // Records every native call the food layer makes, with the clock and how many budgets had
    // posted for that day when the call happened.
    private sealed class TracingMarket : IFoodMarket, IFoodSaleEvidence
    {
        public double EarliestMinute { get; set; }
        public int Units = 1000;
        public string ThrowForResident = "";
        public readonly List<string> Orders = new();
        public Action<string>? Trace;
        public bool IsConfirmedPaid(ResidentMeal meal) { Trace?.Invoke("evidence " + meal.ResidentId); return false; }
        public IReadOnlyList<FoodBusiness> Read(double minute)
        {
            Trace?.Invoke("read");
            return new[] { new FoodBusiness { Id = "b", PlaceId = "shop", Name = "Bakery", MealsPerHour = 15,
                Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
                Offers = new() { new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(100), Units = Units } } } };
        }
        public FoodSale Buy(ResidentMeal meal, double at)
        {
            Trace?.Invoke("buy " + meal.ResidentId);
            Orders.Add(meal.IntentId + "@" + at.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); Units--;
            if (ThrowForResident == meal.ResidentId) throw new Exception("Lost native acknowledgement");
            return new(true, "Native fake confirmed");
        }
    }

    private sealed class Run
    {
        public CitySimulator City = null!;
        public readonly TracingMarket Market = new();
        public readonly Walking Routes = new();
        public readonly List<string> Trace = new();
        public long Events;
        public bool Legacy;
    }

    // Half the residents have a citizen event exactly at the second midnight, and one resident's checkout
    // stays uncertain so its meal record is due again at that midnight as well.
    private static Run Start(bool legacy, int count = 60)
    {
        var state = Population.Create(Enumerable.Range(0, count).Select(n => new CitizenIdentity {
            Id = "r" + n, Name = "Resident " + n, Persona = n % 3 == 0 ? Persona.Retiree : Persona.Foodie, Age = 30 + n % 40 }).ToArray(), new[] {
                new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = count },
                new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = count, Position = new(1, 0, 0) },
                new Place { Id = "shop", Name = "Bakery", Kind = PlaceKind.Shop, Position = new(2, 0, 0) } }, "food-batch", "0.40.4", 7, 1440);
        foreach (var c in state.Citizens)
        {
            c.Phase = CitizenPhase.AtPlace; c.PlaceId = "home"; c.Activity = Activity.Home;
            c.NextEventMinute = c.ActivityUntilMinute = int.Parse(c.Identity.Id.Substring(1)) % 2 == 0 ? Midnight : 2870;
        }
        state.Food = new FoodCommerceState { RepresentationFactor = 1, MealRhythmVersion = MealRhythm.PolicyVersion,
            Shops = new() { new FoodScope { PlaceId = "shop", BusinessId = "b", ProductId = FoodProducts.Croissant } } };
        var run = new Run { Legacy = legacy };
        run.Market.ThrowForResident = "r5";
        run.City = new CitySimulator(state, run.Routes);
        run.City.AttachFoodMarket(run.Market);
        Attach(run);
        run.City.EnableHouseholdFinances();
        return run;
    }

    private static int Posted(CitySimulator city)
    {
        int day = (int)(city.State.Minute / 1440);
        return city.State.Food!.Residents.Count(m => m.Budget != null && m.Budget.LastPostedDay >= day);
    }

    private static void Attach(Run run)
    {
        var city = run.City;
        string Stamp() => "@" + city.State.Minute.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " posted " + Posted(city);
        run.Market.Trace = s => run.Trace.Add(s + Stamp());
        city.ActivityObserved += (id, _, _) => run.Trace.Add("citizen " + id + Stamp());
        if (run.Legacy) Food(city).BudgetPostingsPerEvent = 1;
    }

    private static FoodCommerce Food(CitySimulator city)
        => (FoodCommerce)(typeof(CitySimulator).GetField("food", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(city)
            ?? throw new InvalidOperationException("CitySimulator no longer exposes its food commerce field to this test."));

    private static void Advance(Run run, double target, int budget)
    {
        while (true)
        {
            var result = run.City.AdvanceTo(target, budget);
            run.Events += result.Events;
            if (result.CaughtUp) return;
        }
    }

    private static void Reload(Run run)
    {
        var read = SaveCodec.Read(SaveCodec.Write(run.City.State)); Assert.Equal("", read.Error);
        run.City = new CitySimulator(read.State!, run.Routes);
        run.City.AttachFoodMarket(run.Market);
        Attach(run);
    }

    [Theory]
    [InlineData(1)] [InlineData(16)] [InlineData(128)] [InlineData(10000)]
    public void BatchedPostingsMatchPerBudgetSteppingByteForByte(int eventBudget)
    {
        var legacy = Start(legacy: true);
        var batched = Start(legacy: false);
        for (int day = 1; day <= 5; day++)
        {
            double target = day * 1440.0 + 1439;
            Advance(legacy, target, eventBudget); Advance(batched, target, eventBudget);
            Assert.Equal(SaveCodec.Write(legacy.City.State), SaveCodec.Write(batched.City.State));
            if (day == 3) { Reload(legacy); Reload(batched); }
        }
        Advance(legacy, 6 * 1440.0 + 700, eventBudget); Advance(batched, 6 * 1440.0 + 700, eventBudget);
        Assert.Equal(SaveCodec.Write(legacy.City.State), SaveCodec.Write(batched.City.State));
        Assert.Equal(legacy.Market.Orders, batched.Market.Orders);
        Assert.NotEmpty(batched.Market.Orders);
        Assert.All(batched.City.State.Food!.Residents, m => Assert.Equal(6, m.Budget!.LastPostedDay));

        // Same native calls and citizen events, in the same order, each seeing the same postings.
        Assert.Equal(legacy.Trace, batched.Trace);

        // Postings ran at the enabling minute and at five midnights: 60 per-budget events became one each.
        int residents = batched.City.State.Food.Residents.Count;
        Assert.Equal(60, residents);
        Assert.Equal(6L * (residents - 1), legacy.Events - batched.Events);
    }

    [Fact]
    public void CitizenEventsPrecedeAndMealEventsFollowEveryBudgetDueAtThatMinute()
    {
        var run = Start(legacy: false);
        Advance(run, Midnight + 30, 16);
        string at = "@" + Midnight.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " posted ";
        var atMidnight = run.Trace.Where(t => t.Contains(at)).ToList();
        var citizens = atMidnight.Where(t => t.StartsWith("citizen ", StringComparison.Ordinal)).ToList();
        var meals = atMidnight.Where(t => !t.StartsWith("citizen ", StringComparison.Ordinal)).ToList();
        Assert.All(Enumerable.Range(0, 30), n => Assert.Contains(citizens, t => t.StartsWith("citizen r" + 2 * n + "@", StringComparison.Ordinal)));
        Assert.All(citizens, t => Assert.EndsWith(at + "0", t));
        Assert.Contains("evidence r5" + at + "60", meals);
        Assert.All(meals, t => Assert.EndsWith(at + "60", t));
        int lastCitizen = run.Trace.FindLastIndex(t => citizens.Contains(t));
        int firstMeal = run.Trace.FindIndex(t => meals.Contains(t));
        Assert.True(lastCitizen < firstMeal);
    }

    [Fact]
    public void ASaveTakenPartWayThroughPerBudgetPostingFinishesIdenticallyUnderTheBatch()
    {
        var legacy = Start(legacy: true);
        var reference = Start(legacy: true);
        Advance(legacy, Midnight - 1, 128); Advance(reference, Midnight - 1, 128);
        bool partial = false;
        while (!partial)
        {
            var result = legacy.City.AdvanceTo(Midnight + 600, 8);
            int posted = Posted(legacy.City);
            partial = legacy.City.State.Minute == Midnight && posted > 0 && posted < 60;
            Assert.False(result.CaughtUp && !partial, "the per-budget reference never stopped part-way through midnight");
        }
        string written = SaveCodec.Write(legacy.City.State);
        legacy.Legacy = false; Reload(legacy); // the batch now owns the rest of the postings
        Assert.Equal(written, SaveCodec.Write(legacy.City.State));
        legacy.Trace.Clear(); reference.Trace.Clear();
        Advance(legacy, 4 * 1440.0 + 700, 128); Advance(reference, 4 * 1440.0 + 700, 128);
        Assert.Equal(SaveCodec.Write(reference.City.State), SaveCodec.Write(legacy.City.State));
        Assert.Equal(reference.Market.Orders, legacy.Market.Orders);
    }

    [Fact]
    public void BudgetsDueAtDifferentMinutesPostAtTheirOwnMinutes()
    {
        var run = Start(legacy: false, count: 4);
        // A resident enrolled later carries a fractional first posting minute; it must not join a batch.
        var budgets = run.City.State.Food!.Residents.Select(m => m.Budget!).ToArray();
        Advance(run, 1440.5, 128);
        Assert.All(budgets, b => Assert.Equal(1, b.LastPostedDay));
        Assert.All(budgets, b => Assert.Equal(Midnight, b.NextPostingMinute));
        var food = Food(run.City);
        Assert.Equal(int.MaxValue, food.BudgetPostingsPerEvent);
        var state = run.City.State;
        var read = SaveCodec.Read(SaveCodec.Write(state)); Assert.Equal("", read.Error);
        var shifted = read.State!;
        shifted.Food!.Residents[1].Budget!.NextPostingMinute = Midnight + .25;
        shifted.Food.Residents[1].Budget!.LastPostedDay = 1;
        var city = new CitySimulator(shifted, run.Routes); city.AttachFoodMarket(new TracingMarket());
        city.AdvanceTo(Midnight, 10000);
        Assert.Equal(3, shifted.Food.Residents.Count(m => m.Budget!.LastPostedDay == 2));
        Assert.Equal(1, shifted.Food.Residents[1].Budget!.LastPostedDay);
        city.AdvanceTo(Midnight + .25, 10000);
        Assert.Equal(2, shifted.Food.Residents[1].Budget!.LastPostedDay);
    }
}
