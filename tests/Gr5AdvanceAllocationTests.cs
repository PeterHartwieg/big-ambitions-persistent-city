using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class Gr5AdvanceAllocationTests
{
    private readonly ITestOutputHelper output;
    public Gr5AdvanceAllocationTests(ITestOutputHelper output) { this.output = output; }

    internal const int PlayerShops = 6, RivalShops = 24, LegsPerWalk = 10;
    internal const double WalkMetresPerMinute = 80;
    private static readonly string[] Menu = { FoodProducts.Burger, FoodProducts.Croissant, FoodProducts.HotDog };

    internal sealed class Planner : IResumableRoutePlanner, ICommuteTimingPlanner, IWalkingTimeBound, IFoodWalkingPace,
        IWalkingQuoteValidity, IWalkingQuoteRevision, IWalkingQuoteOperation
    {
        private readonly Dictionary<(string, string), RouteLeg[]> walks = new Dictionary<(string, string), RouteLeg[]>();
        public double WalkingMetresPerMinute => Gr5AdvanceAllocationTests.WalkMetresPerMinute;
        public long WalkingQuoteRevision => 1;
        public long PlannerBytes;
        public void BeginWalkingQuote() { }
        public void EndWalkingQuote() { }
        public double MinimumWalkingMinutes(Place from, Place to) => Position.Distance(from.Position, to.Position) / WalkMetresPerMinute;

        private RouteLeg[] Legs(Place from, Place to)
        {
            if (walks.TryGetValue((from.Id, to.Id), out var known)) return known;
            var legs = new RouteLeg[LegsPerWalk];
            for (int i = 0; i < LegsPerWalk; i++)
            {
                var a = Position.Lerp(from.Position, to.Position, i / (double)LegsPerWalk);
                var b = Position.Lerp(from.Position, to.Position, (i + 1) / (double)LegsPerWalk);
                double length = Position.Distance(a, b);
                legs[i] = new RouteLeg { From = a, To = b, Mode = TravelMode.Walk, Minutes = length < .000001 ? .01 : length / WalkMetresPerMinute };
                legs[i].Freeze();
            }
            walks.Add((from.Id, to.Id), legs);
            return legs;
        }

        private Route Fresh(Place from, Place to)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var legs = Legs(from, to);
            var route = new Route();
            for (int i = 0; i < legs.Length; i++) route.Legs.Add(legs[i]);
            PlannerBytes += GC.GetAllocatedBytesForCurrentThread() - before;
            return route;
        }

        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences)
        {
            if (!preferences.AllowTransit && !preferences.OwnsCar && Position.Distance(from.Position, to.Position) > preferences.WalkingToleranceMetres) return null;
            return Fresh(from, to);
        }

        public bool IsWalkingQuoteValid(Place from, Place to, Route route, TravelPreferences preferences)
        {
            if (!walks.TryGetValue((from.Id, to.Id), out var legs) || legs.Length != route.Legs.Count) return false;
            for (int i = 0; i < legs.Length; i++)
            {
                var q = route.Legs[i];
                if (Position.Distance(q.From, legs[i].From) > .001 || Position.Distance(q.To, legs[i].To) > .001 || Math.Abs(q.Minutes - legs[i].Minutes) > .000001) return false;
            }
            return true;
        }

        public IRoutePlanningJob Begin(RouteRequest request) => new Job(this, request);

        private sealed class Job : IRoutePlanningJob
        {
            private readonly Planner owner; private readonly RouteRequest request; private int slices;
            public Job(Planner owner, RouteRequest request) { this.owner = owner; this.request = request; }
            public Route? Result { get; private set; }
            public bool Continue(double budgetMilliseconds)
            {
                if (++slices < 2 && !double.IsPositiveInfinity(budgetMilliseconds)) return false;
                Result = owner.Fresh(request.From, request.To);
                return true;
            }
            public void Dispose() { }
        }
    }

    internal sealed class Market : IFoodMarket, IFoodCandidateMarket, IFoodBusinessAuthority, IFoodSaleEvidence, IRivalFoodMarket, IFoodAwarenessRevision
    {
        private readonly Dictionary<string, FoodBusiness> players = new Dictionary<string, FoodBusiness>(StringComparer.Ordinal);
        private readonly Dictionary<string, FoodAlternativeObservation> rivals = new Dictionary<string, FoodAlternativeObservation>(StringComparer.Ordinal);
        private readonly List<FoodScope> rivalScopes = new List<FoodScope>();
        private readonly HashSet<string> rivalPlaces = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<FoodBusiness> all = new List<FoodBusiness>();
        private readonly HashSet<string> paid = new HashSet<string>(StringComparer.Ordinal);
        public long Bought;

        public Market(CityState state)
        {
            foreach (var scope in state.Food!.Shops)
            {
                var business = new FoodBusiness { Id = scope.BusinessId, PlaceId = scope.PlaceId, Name = scope.PlaceId, MealsPerHour = 30, CustomerServicePercent = 60,
                    Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 6 * 60, ToMinute = 22 * 60 }).ToList(),
                    Offers = Menu.OrderBy(p => p, StringComparer.Ordinal).Select((p, i) => new FoodOffer { ProductId = p, Price = Money.FromCents(300 + 150 * i), Units = 100000 }).ToList() };
                players.Add(business.Id, business); all.Add(business);
            }
            for (int i = 0; i < RivalShops; i++)
            {
                string id = "rival-" + i, place = "shop-" + (100 + i * 7);
                var products = Menu.Where((_, p) => (i + p) % 3 != 0).ToArray();
                rivals.Add(id, new FoodAlternativeObservation(id, place, "Rival Street", i + 1, 0, "fastfood", "rival-owner-" + i % 5, FoodAlternativeKind.Rival,
                    true, true, false, 40 + i % 40, 0, products.Select((p, k) => new FoodAdvertisedOffer(p, Money.FromCents(280 + 90 * k + 10 * (i % 7)))),
                    Enumerable.Range(0, 7).Select(d => new FoodAdvertisedHours(d, 7 * 60, 23 * 60))));
                var ordered = products.OrderBy(p => p, StringComparer.Ordinal).ToList();
                rivalScopes.Add(new FoodScope { AlternativeKind = FoodAlternativeKind.Rival, BusinessId = id, PlaceId = place, ProductId = ordered[0], AdditionalProductIds = ordered.Skip(1).ToList() });
                rivalPlaces.Add(place);
            }
        }

        public double EarliestMinute => 0;
        public long AwarenessRevision => 1;
        public long RivalRevision => 1;
        public IReadOnlyList<FoodBusiness> Read(double minute) => all;
        public FoodSale Buy(ResidentMeal meal, double minute) { paid.Add(meal.IntentId); Bought++; return new FoodSale(true, "Confirmed"); }
        public bool IsConfirmedPaid(ResidentMeal meal) => paid.Contains(meal.IntentId);
        public IDisposable BeginCapture(double minute) => NoCapture.Instance;
        public bool IsCandidate(FoodScope scope) => players.ContainsKey(scope.BusinessId);
        public FoodBusiness? CaptureBusiness(string businessId, double minute) => players.TryGetValue(businessId, out var b) ? b : null;
        public bool CanServe(string businessId) => players.ContainsKey(businessId);
        public void CloseRivals() { }
        public bool NeedsRivalRefresh(double minute) => false;
        public void ContinueRivalRefresh(double minute) { }
        public IEnumerable<FoodScope> RivalScopes => rivalScopes;
        public bool IsRivalCandidate(FoodScope scope) => rivals.ContainsKey(scope.BusinessId);
        public bool RivalAtPlace(string place) => rivalPlaces.Contains(place);
        public FoodAlternativeObservation? ReadRival(string businessId) => rivals.TryGetValue(businessId, out var r) ? r : null;
        private sealed class NoCapture : IDisposable { public static readonly NoCapture Instance = new NoCapture(); public void Dispose() { } }
    }

    internal static (CityState State, CitySimulator City, Planner Planner) City()
    {
        var state = Gr5AllocationMeasure.RigCity();
        var ordered = Menu.OrderBy(p => p, StringComparer.Ordinal).ToList();
        state.Food = new FoodCommerceState { MealRhythmVersion = MealRhythm.PolicyVersion, HouseholdFinanceVersion = HouseholdFinance.PolicyVersion,
            RepresentationFactor = CityBalance.ConsumerRepresentationFactor, Districts = new[] { "an", "as", "bn", "bs" }.Select((d, i) => new FoodDistrictBudgetMix { DistrictId = d,
                WorkingPercent = 50 - 10 * i, MiddlePercent = 35, UpperPercent = 15 + 10 * i, Evidence = "Fixture district mix" }).ToList(),
            Shops = Enumerable.Range(0, PlayerShops).Select(i => new FoodScope { PlaceId = "shop-" + (1 + 11 * i), BusinessId = "b" + i,
                ProductId = ordered[0], AdditionalProductIds = ordered.Skip(1).ToList() }).ToList() };
        var planner = new Planner();
        var city = new CitySimulator(state, planner);
        city.AttachOptionalStops(new RushSpikeReplay.Stops(), OptionalStopPolicy.ApprovedChoicePercentage);
        city.AttachFoodMarket(new Market(state), rivalSettings: new RivalFoodSettings());
        return (state, city, planner);
    }

    internal static readonly GcAllocationSegment[] Reported =
    {
        GcAllocationSegment.AdvancePlanningSlice, GcAllocationSegment.AdvancePlanningFinish, GcAllocationSegment.AdvanceEventsFood,
        GcAllocationSegment.AdvanceEventsCitizen, GcAllocationSegment.AdvanceEventsRouteBegin, GcAllocationSegment.AdvanceEventsTraffic,
        GcAllocationSegment.AdvanceEventsOther,
    };

    internal sealed class Window
    {
        public readonly long[] Bytes = new long[GcPauseSampler.Segments], Calls = new long[GcPauseSampler.Segments];
        public long PlannerBytes;
        public double Per(GcAllocationSegment s) => Calls[(int)s] == 0 ? 0 : Bytes[(int)s] / (double)Calls[(int)s];
        public long Total => Reported.Sum(s => Bytes[(int)s]);
    }

    internal static void Run(CitySimulator city, Planner planner, double from, double to, Window? window, Action<GcAllocationSegment>? segment = null)
    {
        for (double minute = from; minute < to; minute += 1.0 / 30)
        {
            city.RefreshOptionalStops();
            for (int unit = 0; unit < 64; unit++)
            {
                long plannerBefore = planner.PlannerBytes;
                long before = GC.GetAllocatedBytesForCurrentThread();
                GcAllocationSegment kind;
                bool stop = false;
                if (city.PlanningPending)
                {
                    segment?.Invoke(GcAllocationSegment.AdvancePlanning);
                    bool finished = city.ContinuePlanning(2);
                    kind = finished ? GcAllocationSegment.AdvancePlanningFinish : GcAllocationSegment.AdvancePlanningSlice;
                }
                else
                {
                    segment?.Invoke(GcAllocationSegment.AdvanceEvents);
                    var result = city.AdvanceFrameSlice(minute, 1);
                    kind = GcPauseSampler.AdvanceEventSegment(result.TrafficEvents, result.FoodContinuationEvents, result.CitizenEvents, result.PlanningPending);
                    stop = result.CaughtUp;
                }
                long used = GC.GetAllocatedBytesForCurrentThread() - before - (planner.PlannerBytes - plannerBefore);
                if (window != null) { window.Bytes[(int)kind] += used; window.Calls[(int)kind]++; window.PlannerBytes += planner.PlannerBytes - plannerBefore; }
                if (stop) break;
            }
            city.SynchronizeSnapshot();
        }
    }

    private const double Day = 2 * 1440, Start = Day + 15.5 * 60;

    internal static (Window Window, string Hash) Measure(double minutes)
    {
        var (state, city, planner) = City();
        while (!city.AdvanceTo(Start - 30, 256).CaughtUp) { }
        Run(city, planner, Start - 30, Start, null);
        var window = new Window();
        Run(city, planner, Start, Start + minutes, window);
        city.SynchronizeSnapshot();
        return (window, RushSpikeReplay.Sha(DevelopmentChain.Write(state)));
    }

    private void Print(string label, Window w)
    {
        foreach (var s in Reported)
            output.WriteLine(label + " " + GcPauseSampler.SegmentName(s) + ": " + w.Bytes[(int)s] + " B / " + w.Calls[(int)s] + " calls = " + w.Per(s).ToString("0") + " B/call");
        output.WriteLine(label + " total " + w.Total + " B, planner fixture " + w.PlannerBytes + " B");
    }

    private const string Reference = "5B29BFD3A591DCCD789D537C770FB82F09FD0E1FB2DADDEFE40F9FF7F56660A8";

    [Fact]
    public void AdvanceSegmentsFrom1530StayUnderTheirBytesPerCallAndKeepTheSaveHash()
    {
        var (w, hash) = Measure(240);
        Print("day-15:30", w);
        output.WriteLine(hash);
        Assert.True(w.Calls[(int)GcAllocationSegment.AdvanceEventsFood] > 1000, "food calls " + w.Calls[(int)GcAllocationSegment.AdvanceEventsFood]);
        Assert.True(w.Calls[(int)GcAllocationSegment.AdvancePlanningFinish] > 1000, "planning finishes " + w.Calls[(int)GcAllocationSegment.AdvancePlanningFinish]);
        Assert.True(w.Calls[(int)GcAllocationSegment.AdvanceEventsCitizen] > 1000, "citizen calls " + w.Calls[(int)GcAllocationSegment.AdvanceEventsCitizen]);
        Assert.True(w.Per(GcAllocationSegment.AdvanceEventsFood) < 650, "food bytes per call " + w.Per(GcAllocationSegment.AdvanceEventsFood));
        Assert.True(w.Per(GcAllocationSegment.AdvanceEventsCitizen) < 1000, "citizen bytes per call " + w.Per(GcAllocationSegment.AdvanceEventsCitizen));
        Assert.True(w.Per(GcAllocationSegment.AdvanceEventsRouteBegin) < 650, "route begin bytes per call " + w.Per(GcAllocationSegment.AdvanceEventsRouteBegin));
        Assert.True(w.Per(GcAllocationSegment.AdvancePlanningFinish) < 110, "planning finish bytes per call " + w.Per(GcAllocationSegment.AdvancePlanningFinish));
        Assert.True(w.Per(GcAllocationSegment.AdvancePlanningSlice) < 1, "planning slice bytes per call " + w.Per(GcAllocationSegment.AdvancePlanningSlice));
        Assert.True(w.Per(GcAllocationSegment.AdvanceEventsOther) < 1, "idle bytes per call " + w.Per(GcAllocationSegment.AdvanceEventsOther));
        Assert.Equal(Reference, hash);
    }

    private sealed class Types : EventListener
    {
        public readonly Dictionary<(GcAllocationSegment, string), long> Bytes = new Dictionary<(GcAllocationSegment, string), long>();
        public GcAllocationSegment Segment = GcAllocationSegment.AdvanceOther;
        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "Microsoft-Windows-DotNETRuntime") EnableEvents(source, EventLevel.Verbose, (EventKeywords)0x1);
        }
        protected override void OnEventWritten(EventWrittenEventArgs e)
        {
            if (e.EventName == null || !e.EventName.StartsWith("GCAllocationTick", StringComparison.Ordinal) || e.Payload == null) return;
            int name = e.PayloadNames!.IndexOf("TypeName"), amount = e.PayloadNames.IndexOf("AllocationAmount64");
            if (name < 0) return;
            var key = (Segment, (string)e.Payload[name]!);
            long bytes = amount >= 0 ? Convert.ToInt64(e.Payload[amount]) : 100_000;
            lock (Bytes) Bytes[key] = (Bytes.TryGetValue(key, out long b) ? b : 0) + bytes;
        }
    }

    [Fact]
    public void MeasureAdvanceTypes()
    {
        if (PerfGate.Variable("PC_GR5_ADVANCE_TYPES") == null) return;
        var (state, city, planner) = City();
        while (!city.AdvanceTo(Start - 30, 256).CaughtUp) { }
        Run(city, planner, Start - 30, Start, null);
        using var types = new Types();
        var window = new Window();
        Run(city, planner, Start, Start + 240, window, s => types.Segment = s);
        Print("types", window);
        lock (types.Bytes)
            foreach (var pair in types.Bytes.OrderByDescending(p => p.Value).Take(40))
                output.WriteLine("type " + pair.Key.Item1 + " " + pair.Key.Item2 + " " + pair.Value);
    }
}
