using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class Gr5FoodDecisionTests
{
    private readonly ITestOutputHelper output;
    public Gr5FoodDecisionTests(ITestOutputHelper output) { this.output = output; }

    internal sealed class CachedMarket : IFoodMarket, IFoodSaleEvidence
    {
        private readonly RushSpikeReplay.Market inner = new RushSpikeReplay.Market();
        private IReadOnlyList<FoodBusiness>? cached;
        public double EarliestMinute => 0;
        public IReadOnlyList<FoodBusiness> Read(double minute) => cached ??= inner.Read(minute);
        public FoodSale Buy(ResidentMeal meal, double minute) => inner.Buy(meal, minute);
        public bool IsConfirmedPaid(ResidentMeal meal) => inner.IsConfirmedPaid(meal);
    }

    internal static (CityState State, CitySimulator City) FoodCity()
    {
        var state = RushSpikeReplay.Create();
        var city = new CitySimulator(state, new RushSpikeReplay.Planner());
        city.AttachFoodMarket(new CachedMarket()); city.EnableHouseholdFinances();
        city.AttachOptionalStops(new RushSpikeReplay.Stops(), OptionalStopPolicy.ApprovedChoicePercentage);
        return (state, city);
    }

    internal static long[] Run(CitySimulator city, double from, double to, long[]? bytes = null, long[]? calls = null, Action<CitySimulator>? after = null)
    {
        bytes ??= new long[GcPauseSampler.Segments]; calls ??= new long[GcPauseSampler.Segments];
        for (double minute = from; minute < to; minute += 1.0 / 30)
        {
            for (int unit = 0; unit < 64; unit++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                var result = city.AdvanceFrameSlice(minute, 1);
                long used = GC.GetAllocatedBytesForCurrentThread() - before;
                var segment = GcPauseSampler.AdvanceEventSegment(result.TrafficEvents, result.FoodContinuationEvents, result.CitizenEvents, result.PlanningPending);
                bytes[(int)segment] += used; calls[(int)segment]++;
                if (result.CaughtUp) break;
            }
            after?.Invoke(city);
        }
        return calls;
    }

    private const string Lunch = "D0E23E996CB5848A3FC1DFCD6DB215D2F4F670C3639D267571D6C24CF7FFAC6E";

    [Fact]
    public void WeightedFoodDecisionsKeepTheSaveHashThroughALunchAndDinner()
    {
        var (state, city) = FoodCity();
        while (!city.AdvanceTo(2 * 1440 + 11 * 60, 256).CaughtUp) { }
        var calls = Run(city, 2 * 1440 + 11 * 60, 2 * 1440 + 13.5 * 60);
        Run(city, 2 * 1440 + 18 * 60, 2 * 1440 + 19.5 * 60, null, calls);
        Assert.True(calls[(int)GcAllocationSegment.AdvanceEventsFood] > 500, calls[(int)GcAllocationSegment.AdvanceEventsFood].ToString());
        Assert.Contains(state.Food!.Residents, m => m.Purchases > 0);
        city.SynchronizeSnapshot();
        string hash = RushSpikeReplay.Sha(DevelopmentChain.Write(state));
        output.WriteLine(hash);
        Assert.Equal(Lunch, hash);
    }

    [Fact]
    public void FoodContinuationAndCitizenEventsStayUnderTheirBytesPerCall()
    {
        var (_, city) = FoodCity();
        while (!city.AdvanceTo(2 * 1440 + 11 * 60, 256).CaughtUp) { }
        var bytes = new long[GcPauseSampler.Segments]; var calls = new long[GcPauseSampler.Segments];
        Run(city, 2 * 1440 + 11 * 60, 2 * 1440 + 13 * 60, bytes, calls);
        double Per(GcAllocationSegment s) => calls[(int)s] == 0 ? 0 : bytes[(int)s] / (double)calls[(int)s];
        foreach (var s in new[] { GcAllocationSegment.AdvanceEventsFood, GcAllocationSegment.AdvanceEventsCitizen, GcAllocationSegment.AdvanceEventsTraffic, GcAllocationSegment.AdvanceEventsOther })
            output.WriteLine(GcPauseSampler.SegmentName(s) + ": " + calls[(int)s] + " calls, " + Per(s).ToString("0") + " B/call");
        Assert.True(calls[(int)GcAllocationSegment.AdvanceEventsFood] > 300);
        PerfGate.Under(Per(GcAllocationSegment.AdvanceEventsFood), 1150, "bytes per food continuation (.NET)");
        PerfGate.Under(Per(GcAllocationSegment.AdvanceEventsCitizen), 2300, "bytes per citizen event (.NET)");
        PerfGate.Under(Per(GcAllocationSegment.AdvanceEventsOther), 1, "bytes per idle advance unit (.NET)");
    }

    private static List<FoodBusiness> RandomShops(Random random)
    {
        string[] ids = { "b1", "b10", "b2", "B2", "a", "b1", "c", "b" };
        string[] products = { FoodProducts.Burger, FoodProducts.HotDog, FoodProducts.Croissant, "dyn:9", "dyn:10" };
        var shops = new List<FoodBusiness>();
        int count = random.Next(1, 9);
        for (int i = 0; i < count; i++)
        {
            var b = new FoodBusiness { Id = ids[random.Next(ids.Length)], PlaceId = "p" + i, MealsPerHour = random.Next(3) == 0 ? (int?)null : random.Next(0, 90),
                NextServiceMinute = 700 + random.Next(30), CustomerServicePercent = random.Next(2) == 0 ? (int?)null : random.Next(101),
                Hours = new List<OpeningWindow> { new OpeningWindow { Day = 0, FromMinute = random.Next(600), ToMinute = 600 + random.Next(840) } } };
            int offers = random.Next(0, 4);
            for (int o = 0; o < offers; o++)
                b.Offers.Add(new FoodOffer { ProductId = products[random.Next(products.Length)], Price = Money.FromCents(random.Next(100, 2000)),
                    Units = random.Next(3) == 0 ? (int?)null : random.Next(0, 3), DistrictDemandPercent = random.Next(2) == 0 ? (int?)null : random.Next(101) });
            shops.Add(b);
        }
        return shops;
    }

    private static string Describe(FoodIntent intent) => string.Join("|", intent.Choices.Select(c => c.BusinessId + "/" + c.ProductId + "/" + c.Price.ApproximateCents()
        + "/" + c.TravelMinutes + "/" + c.AdmissionMinute + "/" + c.ScoreCents + "/" + c.Reason + "/" + c.ReturnMinutes + "/" + c.ServiceEndsAt
        + "/" + c.CustomerServicePercent + "/" + c.DistrictDemandPercent + "/" + c.ServiceQualityAdjustmentCents + "/" + c.DistrictAppealAdjustmentCents))
        + "#" + intent.Status + intent.BusinessId + intent.ProductId;

    [Fact]
    public void ObservedChoicesKeepTheOrdinalShopAndProductOrderAndReusedChoicesMatchFreshOnes()
    {
        var random = new Random(282);
        var spare = new List<FoodChoice>();
        for (int round = 0; round < 400; round++)
        {
            var shops = RandomShops(random);
            var consumer = new FoodConsumer { Id = "c", DailyAllowance = Money.FromCents(random.Next(300, 2500)), ServiceQualityValueCents = 150, DistrictAppealValueCents = 80, PreferredProductId = FoodProducts.Burger };
            var quotes = shops.Where(_ => random.Next(4) != 0).Select(b => new FoodTravelQuote { BusinessId = b.Id, ConsumerId = "c", IntentId = "i",
                Minutes = random.Next(5) == 0 ? (double?)null : random.Next(1, 20), ReturnMinutes = random.Next(5) == 0 ? (double?)null : random.Next(1, 20) }).ToList();
            var refusals = new Dictionary<string, string>(StringComparer.Ordinal);
            if (random.Next(3) == 0 && shops.Count > 0) refusals[shops[0].Id] = "Unreachable";
            FoodIntent Intent() => new FoodIntent { Id = "i", ConsumerId = "c", OriginPlaceId = "home", Deadline = 760 + random.Next(120), MaximumQueueMinutes = 20, EatingMinutes = 15 };
            var fresh = Intent(); var reused = new FoodIntent { Id = fresh.Id, ConsumerId = "c", OriginPlaceId = "home", Deadline = fresh.Deadline, MaximumQueueMinutes = 20, EatingMinutes = 15 };
            bool evaluateOnly = random.Next(2) == 0;
            FoodDemand.ChooseObserved(consumer, shops, quotes, fresh, 700, refusals, MealKind.Lunch, evaluateOnly);
            FoodDemand.ChooseObserved(consumer, shops, quotes, reused, 700, refusals, MealKind.Lunch, evaluateOnly, spare);
            Assert.Equal(Describe(fresh), Describe(reused));
            var expected = shops.OrderBy(b => b.Id, StringComparer.Ordinal).SelectMany(b => b.Offers.OrderBy(o => o.ProductId, StringComparer.Ordinal).Select(o => (b, o))).ToList();
            Assert.Equal(expected.Count, fresh.Choices.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i].b.Id, fresh.Choices[i].BusinessId);
                Assert.Equal(expected[i].o.ProductId, fresh.Choices[i].ProductId);
                Assert.Equal(expected[i].o.Price, fresh.Choices[i].Price);
            }
            foreach (var b in shops)
            {
                string? linq = !DailyRoutine.NextOpen(b.OpeningPlace, 700, 900).HasValue ? "Closed or too late"
                    : b.MealsPerHour == 0 ? (b.ServiceUnavailableReason.Length == 0 ? "Service unavailable" : b.ServiceUnavailableReason)
                    : b.Offers.Count > 0 && b.Offers.All(o => o.Units == 0) ? "Out of stock"
                    : b.Offers.Count > 0 && b.Offers.All(o => o.Price > consumer.DailyAllowance) ? "Over allowance" : null;
                Assert.Equal(linq, FoodDemand.KnownUnavailable(b, consumer, 700, 900));
            }
            foreach (var c in reused.Choices) { c.Reason = "dirty"; c.ScoreCents = -1; c.TravelMinutes = -1; c.AdmissionMinute = -1; c.ReturnMinutes = -1; c.ServiceEndsAt = -1; }
            spare.AddRange(reused.Choices);
        }
    }
}
