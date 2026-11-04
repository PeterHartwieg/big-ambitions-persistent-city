using System.Diagnostics;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;

// F6b offline measurement and behaviour regression: estimated rival lanes and meal-period sell-out.
// Core only. Wall times are this PC's detached Core cost, not in-game mod-work or whole-frame evidence.
static class Program
{
    static int checks;
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }

    sealed class Paths : IRoutePlanner, IFoodWalkingPace
    {
        public double WalkingMetresPerMinute => 120;
        public Route Plan(Place a, Place b, double at, TravelPreferences p)
            => new Route { Legs = new() { new RouteLeg { From = a.Position, To = b.Position, Minutes = 1, Mode = TravelMode.Walk } } };
    }
    sealed class Market : IFoodMarket, IFoodCandidateMarket, IFoodAwarenessRevision, IRivalFoodMarket, IFoodBusinessAuthority
    {
        sealed class Batch : IDisposable { public void Dispose() { } }
        public readonly Dictionary<string, FoodScope> Rivals = new(StringComparer.Ordinal);
        public readonly Dictionary<string, FoodAlternativeObservation> Observations = new(StringComparer.Ordinal);
        public FoodBusiness[] Players = Array.Empty<FoodBusiness>();
        public int PlayerBuys, RivalWrites;
        public long AwarenessRevision => 0;
        public long RivalRevision => 0;
        public void CloseRivals() { }
        public double EarliestMinute => 0;
        public IDisposable BeginCapture(double minute) => new Batch();
        public bool IsCandidate(FoodScope scope) => Players.Any(p => p.Id == scope.BusinessId);
        public FoodBusiness? CaptureBusiness(string id, double minute) => Players.SingleOrDefault(p => p.Id == id);
        public bool CanServe(string id) => Players.Any(p => p.Id == id);
        public IReadOnlyList<FoodBusiness> Read(double minute) => throw new Exception("All-shop read forbidden");
        public FoodSale Buy(ResidentMeal meal, double minute)
        {
            if (meal.TargetKind != FoodAlternativeKind.Player) { RivalWrites++; throw new Exception("Forbidden rival checkout"); }
            PlayerBuys++; return new FoodSale(true, "Native player control");
        }
        public bool NeedsRivalRefresh(double minute) => false;
        public void ContinueRivalRefresh(double minute) { }
        public IEnumerable<FoodScope> RivalScopes => Rivals.Values;
        public bool IsRivalCandidate(FoodScope scope) => Observations.ContainsKey(scope.BusinessId);
        public bool RivalAtPlace(string place) => Rivals.Values.Any(s => s.PlaceId == place);
        public FoodAlternativeObservation? ReadRival(string id) => Observations.GetValueOrDefault(id);
    }
    sealed class NoCampaign : IFoodAwarenessProvider { public string? Reference(string c, int d, int s) => null; }

    static FoodAlternativeObservation Observation(string id, string place) => new(id, place, "street", 1, 1, "ba:businesstype_fastfoodrestaurant",
        "rival-owner", FoodAlternativeKind.Rival, true, true, false, null, 0,
        new[] { new FoodAdvertisedOffer(FoodProducts.Croissant, Money.FromCents(100)) },
        Enumerable.Range(0, 7).Select(d => new FoodAdvertisedHours(d, 0, 1440)));

    /// <summary>One shop next to a home of residents x2 consumers: a rival (estimated limits) or a player shop (30 meals/hour).</summary>
    static (CitySimulator city, Market market) Fixture(int residents, RivalFoodSettings? rival, int seed = 79)
    {
        var places = new List<Place> { new() { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = residents },
            new() { Id = "s0", Name = "Shop", Kind = PlaceKind.Shop, Position = new Position(10, 0, 0) } };
        var state = Population.Create(Enumerable.Range(0, residents).Select(i => new CitizenIdentity { Id = "r" + i, Name = "R" + i, Age = 30, Persona = Persona.Foodie }),
            places, "f6b", "0.40.4", seed, 1440);
        foreach (var p in state.Citizens) { p.NextEventMinute = p.ActivityUntilMinute = 2870; p.Preferences.WalkingToleranceMetres = 5; }
        state.Food = new FoodCommerceState { RepresentationFactor = 2, ChoiceStartsDay = 1 };
        var market = new Market();
        if (rival != null)
        {
            const string id = "business:Test%20Street:1:1:food:rival-owner";
            market.Rivals.Add(id, new FoodScope { AlternativeKind = FoodAlternativeKind.Rival, BusinessId = id, PlaceId = "s0", ProductId = FoodProducts.Croissant });
            market.Observations.Add(id, Observation(id, "s0"));
        }
        else
        {
            state.Food.Shops.Add(new FoodScope { BusinessId = "player", PlaceId = "s0", ProductId = FoodProducts.Croissant });
            market.Players = new[] { new FoodBusiness { Id = "player", PlaceId = "s0", MealsPerHour = RivalServiceCalibration.ReferencePlayerMealsPerHour,
                Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
                Offers = new() { new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(100), Units = 1000000 } } } };
        }
        var city = new CitySimulator(state, new Paths());
        city.AttachFoodMarket(market, rivalSettings: rival ?? new RivalFoodSettings { RivalChoiceWeight = 0 }, campaign: new NoCampaign());
        state.Food.ChoiceStartsDay = state.Food.AwarenessStartsDay = state.Food.RivalStartsDay = 1;
        return (city, market);
    }
    static int ServedToday(CitySimulator city) => city.State.Food!.Residents.Sum(m => m.Today?.Meals.Count(r => r.Status == MealOutcomeStatus.Bought) ?? 0);

    static object Stats(List<double> values, long bytes, int operations)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        return new { samples = sorted.Length, p50 = sorted[sorted.Length / 2], p95 = sorted[(int)Math.Ceiling(sorted.Length * .95) - 1], max = sorted[^1],
            spikesAtLeast30ms = sorted.Count(v => v >= 30), allocatedBytesPerOperation = (double)bytes / operations };
    }

    /// <summary>Served meals/day under rising demand: the default rival estimate, a near-unbounded rival and a 30/hour player shop.</summary>
    static object Saturation()
    {
        var rows = new List<object>();
        foreach (int residents in new[] { 8, 32, 128, 512 })
        {
            var (moderate, m1) = Fixture(residents, new RivalFoodSettings());
            var (wide, m2) = Fixture(residents, new RivalFoodSettings { RivalServiceLanes = 8, RivalMealPeriodUnits = 10000, RivalCapacityPercent = 100 });
            var (player, m3) = Fixture(residents, null);
            foreach (var c in new[] { moderate, wide, player }) while (!c.AdvanceTo(2880, 64).CaughtUp) { }
            Check(m1.RivalWrites + m2.RivalWrites + m3.RivalWrites == 0, "no rival native write");
            Check(m3.PlayerBuys == ServedToday(player), "player sales are exactly native buys");
            Check(moderate.ReconstructedRivalLoadImage() == moderate.RivalLoadImage(), "derived load equals rebuild");
            rows.Add(new { consumers = residents * 2, moderateRival = ServedToday(moderate), nearUnboundedRival = ServedToday(wide), player30PerHour = ServedToday(player) });
        }
        return rows;
    }

    /// <summary>The added F6b work per arrival (lane scan, sell-out count, admit) and completion (paid), at a busy rival.</summary>
    static object ArrivalCompletion(int lanes, int queued)
    {
        var load = new RivalServiceLoad();
        var meals = Enumerable.Range(0, queued + 1).Select(i => new ResidentMeal { PlaceId = "p", Status = MealStatus.Serving, ServiceAt = 2000 + i,
            External = "10" + RivalFoodTerms.Fingerprint(Observation("x", "p")) }).ToArray();
        foreach (var m in meals.Take(queued)) load.Admit(m);
        var arrive = new List<double>(); var complete = new List<double>(); long arriveBytes = 0, completeBytes = 0; double sink = 0;
        for (int i = 0; i < 200000; i++)
        {
            var m = meals[i % meals.Length]; load.Leave(m);
            long b = GC.GetAllocatedBytesForCurrentThread(); long t = Stopwatch.GetTimestamp();
            double start = load.NextFree("p", 1990, lanes);
            sink += start + load.Count("p", RivalServiceCalibration.Period(start + 5));
            load.Admit(m);
            double ms = Stopwatch.GetElapsedTime(t).TotalMilliseconds; arriveBytes += GC.GetAllocatedBytesForCurrentThread() - b;
            if (i >= 1000) arrive.Add(ms);
            b = GC.GetAllocatedBytesForCurrentThread(); t = Stopwatch.GetTimestamp();
            load.Paid(m);
            ms = Stopwatch.GetElapsedTime(t).TotalMilliseconds; completeBytes += GC.GetAllocatedBytesForCurrentThread() - b;
            if (i >= 1000) complete.Add(ms);
            load.Admit(m);
        }
        Check(sink > 0, "measured work kept");
        Check(arriveBytes == 0 && completeBytes == 0, "steady-state arrival/completion allocation is 0 B");
        return new { lanes, servingAtRival = queued, arrival = Stats(arrive, arriveBytes, arrive.Count), completion = Stats(complete, completeBytes, complete.Count) };
    }

    /// <summary>Attach-time reconstruction over consumers with three outcome records each, a third paid at a rival this period.</summary>
    static object Reconstruction(int consumers, int rivals)
    {
        double now = 1440 + 700;
        var meals = new List<ResidentMeal>(consumers);
        for (int i = 0; i < consumers; i++)
        {
            string place = "rival" + (i % rivals);
            var meal = new ResidentMeal { ResidentId = "c" + i, PlaceId = place, Day = 1, Kind = MealKind.Dinner,
                Status = i % 50 == 0 ? MealStatus.Serving : MealStatus.Pending, ServiceAt = now + 1 + i % 5,
                External = i % 50 == 0 ? "10" + RivalFoodTerms.Fingerprint(Observation("x", place)) : "" };
            meal.Today = new ResidentFoodToday { Day = 1, Meals = {
                new MealOutcomeRecord { Kind = MealKind.Breakfast, Status = MealOutcomeStatus.Bought, TargetKind = FoodAlternativeKind.Player, PlaceId = "p", CheckoutAt = 1440 + 400 },
                new MealOutcomeRecord { Kind = MealKind.Lunch, Status = i % 3 == 0 ? MealOutcomeStatus.Bought : MealOutcomeStatus.Skipped,
                    TargetKind = FoodAlternativeKind.Rival, PlaceId = i % 3 == 0 ? place : "", CheckoutAt = 1440 + 680 } } };
            meals.Add(meal);
        }
        var times = new List<double>(); long bytes = 0; string? image = null;
        for (int run = 0; run < 7; run++)
        {
            long b = GC.GetAllocatedBytesForCurrentThread(); long t = Stopwatch.GetTimestamp();
            var load = RivalServiceLoad.Reconstruct(meals, now);
            double ms = Stopwatch.GetElapsedTime(t).TotalMilliseconds; long used = GC.GetAllocatedBytesForCurrentThread() - b;
            if (run > 0) { times.Add(ms); bytes += used; }
            var text = load.Image(now); Check(image == null || image == text, "reconstruction is deterministic"); image = text;
        }
        return new { consumers, rivals, reconstruction = Stats(times, bytes, times.Count) };
    }

    /// <summary>Retained transient bytes when every one of 20,000 rivals carries one paid count and two Serving meals.</summary>
    static object Retained(int rivals)
    {
        var meals = Enumerable.Range(0, rivals * 2).Select(i => new ResidentMeal { PlaceId = "address:Street:" + (i / 2) + ":shop", Status = MealStatus.Serving,
            ServiceAt = 2000 + i % 2, External = "10" + RivalFoodTerms.Fingerprint(Observation("x", "p")) }).ToArray();
        long before = GC.GetTotalMemory(true);
        var load = new RivalServiceLoad();
        foreach (var m in meals) load.Admit(m);
        for (int i = 0; i < meals.Length; i += 2) { meals[i].ServiceAt = 1990; load.Paid(meals[i]); load.Admit(meals[i]); }
        long after = GC.GetTotalMemory(true);
        Check(load.Places == rivals, "one entry per rival");
        GC.KeepAlive(load); GC.KeepAlive(meals);
        return new { rivals, retainedBytes = after - before, bytesPerRival = (double)(after - before) / rivals };
    }

    /// <summary>Whole Core AdvanceTo calls through a one-day crowd at one-minute frames: mod-work-like intervals, not frames.</summary>
    static object Advancement(int residents)
    {
        var (city, market) = Fixture(residents, new RivalFoodSettings());
        var times = new List<double>(); long bytes = GC.GetAllocatedBytesForCurrentThread();
        for (double at = 1441; at <= 2880; at++)
        {
            long t = Stopwatch.GetTimestamp();
            while (!city.AdvanceTo(at, 64).CaughtUp) { }
            times.Add(Stopwatch.GetElapsedTime(t).TotalMilliseconds);
        }
        bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        Check(market.RivalWrites == 0, "no rival native write");
        return new { consumers = residents * 2, served = ServedToday(city), interval = Stats(times, bytes, times.Count) };
    }

    /// <summary>Managed allocation of one whole simulated day at a rival, divided by rival arrivals plus completions.
    /// Used for the RivalMatches before/after; includes all other scheduler work of that day, which is unchanged.</summary>
    static object DayAllocation(int residents)
    {
        var (city, _) = Fixture(residents, new RivalFoodSettings());
        while (!city.AdvanceTo(1441, 64).CaughtUp) { }
        long before = GC.GetAllocatedBytesForCurrentThread();
        while (!city.AdvanceTo(2880, 64).CaughtUp) { }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        int served = ServedToday(city);
        return new { consumers = residents * 2, served, dayBytes = bytes, bytesPerServedMeal = (double)bytes / Math.Max(1, served) };
    }

    static void Behaviour()
    {
        // Reload at every call reproduces the uninterrupted city byte-for-byte, including the derived load.
        var tight = new RivalFoodSettings { RivalServiceLanes = 1, RivalMealPeriodUnits = 3, RivalAssumedServiceMinutes = 20 };
        var (straight, _) = Fixture(32, tight);
        while (!straight.AdvanceTo(2880, 4).CaughtUp) { }
        var (city, market) = Fixture(32, new RivalFoodSettings { RivalServiceLanes = 1, RivalMealPeriodUnits = 3, RivalAssumedServiceMinutes = 20 });
        for (double at = 1445; at <= 2880; at += 5)
            while (true)
            {
                bool caught = city.AdvanceTo(at, 4).CaughtUp;
                string wire = SaveCodec.Write(city.State); var read = SaveCodec.Read(wire);
                Check(read.Status == SaveReadStatus.Loaded, "reload");
                var next = new CitySimulator(read.State!, new Paths());
                next.AttachFoodMarket(market, rivalSettings: new RivalFoodSettings { RivalServiceLanes = 1, RivalMealPeriodUnits = 3, RivalAssumedServiceMinutes = 20 }, campaign: new NoCampaign());
                Check(next.RivalLoadImage() == city.RivalLoadImage(), "reload rebuilds identical lanes and counts");
                city = next;
                if (caught) break;
            }
        Check(SaveCodec.Write(city.State) == SaveCodec.Write(straight.State), "reload equivalence");
        Check(CityState.CurrentSchema == 25, "writer 25 unchanged");
    }

    static void Main(string[] args)
    {
        Behaviour();
        var result = new {
            note = "Detached Core wall time on this PC. Not in-game mod-work, whole-frame or native evidence.",
            writer = CityState.CurrentSchema,
            calibration = new { RivalServiceCalibration.ReferencePlayerMealsPerHour, defaultLanes = new RivalFoodSettings().Lanes(""),
                defaultCeilingMealsPerHour = RivalServiceCalibration.CeilingMealsPerHour(new RivalFoodSettings().Lanes(""), 5),
                defaultMealPeriodUnits = new RivalFoodSettings().MealPeriodUnits(""), bandPercent = RivalServiceCalibration.CalibrationBandPercent },
            saturation = Saturation(),
            arrivalCompletion = new[] { ArrivalCompletion(2, 2), ArrivalCompletion(8, 16) },
            reconstruction = new[] { Reconstruction(1914, 64), Reconstruction(10000, 1024), Reconstruction(100000, 20000) },
            retained = Retained(20000),
            advancement = new[] { Advancement(957) },
            dayAllocation = new[] { DayAllocation(8), DayAllocation(64) },
            checks
        };
        string json = JsonConvert.SerializeObject(result, Formatting.Indented);
        Console.WriteLine(json);
        if (args.Length > 0) File.WriteAllText(args[0], json);
    }
}
