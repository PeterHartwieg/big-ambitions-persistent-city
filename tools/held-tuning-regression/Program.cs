using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;

static class Program
{
    sealed class Paths : IRoutePlanner
    {
        public int Calls;
        public Route Plan(Place a, Place b, double at, TravelPreferences preferences)
        { Calls++; return new Route { Legs = new() { new RouteLeg { From = a.Position, To = b.Position, Mode = TravelMode.Walk, Minutes = 1 } } }; }
    }
    sealed class Market : IFoodMarket
    {
        public double EarliestMinute => 0;
        public FoodBusiness[] Businesses = Array.Empty<FoodBusiness>();
        public int Orders;
        public IReadOnlyList<FoodBusiness> Read(double at) => Businesses;
        public FoodSale Buy(ResidentMeal meal, double at) { Orders++; throw new Exception("Benchmark entered checkout"); }
    }
    static (CitySimulator city, Paths paths, Market market, ResidentMeal meal) Ready(int shops, double taste = 0)
    {
        var places = new List<Place> { new() { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 1 } };
        places.AddRange(Enumerable.Range(0, shops).Select(i => new Place { Id = "shop" + i, Name = "Shop", Kind = PlaceKind.Shop, Position = new(10 + i, 0, 0) }));
        var state = Population.Create(new[] { new CitizenIdentity { Id = "resident", Name = "Resident", Age = 30, Persona = Persona.Foodie } }, places, "held-tuning", "0.40.4", 2, 1440);
        var resident = state.Citizens.Single(); resident.NextEventMinute = resident.ActivityUntilMinute = 2870;
        resident.Traits = new ResidentTraits { Bands = "4022222222" };
        state.Food = new FoodCommerceState { Shops = places.Skip(1).Select((p, i) => new FoodScope { PlaceId = p.Id, BusinessId = "generation:" + i,
            ProductId = FoodProducts.Croissant, AdditionalProductIds = new() { FoodProducts.Burger } }).ToList() };
        var market = new Market { Businesses = state.Food.Shops.Select(s => new FoodBusiness { Id = s.BusinessId, PlaceId = s.PlaceId, Name = "Shop", MealsPerHour = 60,
            Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
            Offers = new() { new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(100), Units = 100 },
                new FoodOffer { ProductId = FoodProducts.Burger, Price = Money.FromCents(100), Units = 100 } } }).ToArray() };
        var paths = new Paths(); var city = new CitySimulator(state, paths); city.AttachFoodMarket(market, new FoodChoiceSettings { FoodChoiceTasteWeight = taste });
        state.Food.ChoiceStartsDay = 1;
        return (city, paths, market, state.Food.Residents.Single());
    }
    static object Stats(List<double> values) {
        values.Sort(); return new { p50Us = values[values.Count / 2], p95Us = values[(int)Math.Ceiling(values.Count * .95) - 1], maxUs = values.Last(), spikesAtLeast30Ms = values.Count(v => v >= 30000) };
    }
    static object Decisions(int shops)
    {
        var capture = new List<double>(); var continuation = new List<double>(); long captureBytes = 0, continuationBytes = 0;
        for (int i = -300; i < 3000; i++) {
            var (city, paths, market, meal) = Ready(shops);
            long bytes = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
            city.AdvanceFrameSlice(meal.DesiredAt, 1);
            double us = Stopwatch.GetElapsedTime(started).TotalMicroseconds; long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
            if (meal.Decision == null || paths.Calls != 0) throw new Exception("Missing capture boundary");
            if (i >= 0) { capture.Add(us); captureBytes += allocated; }
            bytes = GC.GetAllocatedBytesForCurrentThread(); started = Stopwatch.GetTimestamp();
            while (!city.AdvanceFrameSlice(meal.DesiredAt, 1).CaughtUp) { }
            us = Stopwatch.GetElapsedTime(started).TotalMicroseconds; allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
            if (meal.Status != MealStatus.Travelling || paths.Calls != 2 * shops || market.Orders != 0) throw new Exception("Changed decision work");
            if (i >= 0) { continuation.Add(us); continuationBytes += allocated; }
        }
        return new { shops, samples = capture.Count, capture = Stats(capture), continuation = Stats(continuation), captureBytes = captureBytes / (double)capture.Count, continuationBytes = continuationBytes / (double)continuation.Count };
    }
    static Action<FoodDecisionState, FoodChoiceSettings> CaptureSetter()
    {
        var property = typeof(FoodDecisionState).GetProperty("Tuning");
        if (property == null) return (_, _) => { };
        var decision = Expression.Parameter(typeof(FoodDecisionState)); var settings = Expression.Parameter(typeof(FoodChoiceSettings));
        return Expression.Lambda<Action<FoodDecisionState, FoodChoiceSettings>>(Expression.Assign(Expression.Property(decision, property),
            Expression.Call(property.PropertyType.GetMethod("Capture")!, settings)), decision, settings).Compile();
    }
    static object Retained(int shops)
    {
        var set = CaptureSetter(); var settings = new FoodChoiceSettings(); const int n = 20000;
        var held = new FoodDecisionState[n];
        // Warm JIT/delegate binding outside both counters.
        set(new FoodDecisionState(), settings); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long before = GC.GetTotalMemory(true), allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < n; i++) { var d = new FoodDecisionState(); set(d, settings); for (int j = 0; j < shops; j++) d.Shops.Add(new FoodDecisionShop()); held[i] = d; }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated, retained = GC.GetTotalMemory(true) - before;
        GC.KeepAlive(held); return new { shops, retainedBytesPerHeld = retained / (double)n, allocatedBytesPerHeld = bytes / (double)n };
    }
    static object ShapePrecheck()
    {
        var type = typeof(FoodDecisionState).Assembly.GetType("PersistentCity.Core.FoodDecisionTuning"); if (type == null) return new { available = false };
        var capture = (Func<FoodChoiceSettings, object>)Delegate.CreateDelegate(typeof(Func<FoodChoiceSettings, object>), type.GetMethod("Capture")!);
        var wire = (string)type.GetMethod("ToWire")!.Invoke(capture(new FoodChoiceSettings()), null)!;
        var check = (Func<string, bool>)Delegate.CreateDelegate(typeof(Func<string, bool>), type.GetMethod("IsCanonicalWire")!);
        var parse = type.GetMethod("TryParse")!; var input = Expression.Parameter(typeof(string)); var output = Expression.Variable(type);
        var oldCheck = Expression.Lambda<Func<string, bool>>(Expression.Block(new[] { output }, Expression.Call(parse, input, output)), input).Compile();
        object Micro(Func<string, bool> action) { for (int i = 0; i < 10000; i++) action(wire); long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp(); for (int i = 0; i < 300000; i++) if (!action(wire)) throw new Exception("Bad image"); return new { ns = Stopwatch.GetElapsedTime(start).TotalNanoseconds / 300000, bytes = (GC.GetAllocatedBytesForCurrentThread() - bytes) / 300000.0 }; }
        return new { materializing = Micro(oldCheck), nonmaterializing = Micro(check) };
    }
    static void Main() => Console.WriteLine(JsonConvert.SerializeObject(new { schema = CityState.CurrentSchema, heldTuning = typeof(FoodDecisionState).GetProperty("Tuning") != null,
        decisions = new[] { Decisions(1), Decisions(8) }, retained = new[] { Retained(0), Retained(8) }, shapePrecheck = ShapePrecheck() }));
}
