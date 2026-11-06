using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F6b: estimated rival lanes and meal-period sell-out, derived from saved state only.</summary>
public sealed class RivalServiceLimitsTests
{
    const string Rival = "business:Test%20Street:1:1:food:rival-owner";
    sealed class NoCampaign : IFoodAwarenessProvider
    { public string? Reference(string consumer, int day, int slot) => null; }
    /// <summary>The shared rival market plus a bounded player ReadBusiness for arrival; native Buy stays counted there.</summary>
    sealed class WithPlayer : IFoodMarket, IFoodCandidateMarket, IFoodAwarenessRevision, IRivalFoodMarket, IFoodBusinessAuthority, ILocalFoodMarket
    {
        public readonly RivalOutflowTests.Market Inner; public WithPlayer(RivalOutflowTests.Market inner) { Inner = inner; }
        public long AwarenessRevision => Inner.AwarenessRevision; public long RivalRevision => Inner.RivalRevision;
        public void CloseRivals() => Inner.CloseRivals(); public double EarliestMinute => Inner.EarliestMinute;
        public IDisposable BeginCapture(double minute) => Inner.BeginCapture(minute);
        public bool IsCandidate(FoodScope scope) => Inner.IsCandidate(scope);
        public FoodBusiness? CaptureBusiness(string id, double minute) => Inner.CaptureBusiness(id, minute);
        public bool CanServe(string id) => Inner.CanServe(id);
        public IReadOnlyList<FoodBusiness> Read(double minute) => Inner.Read(minute);
        public FoodSale Buy(ResidentMeal meal, double minute) => Inner.Buy(meal, minute);
        public bool NeedsRivalRefresh(double minute) => Inner.NeedsRivalRefresh(minute);
        public void ContinueRivalRefresh(double minute) => Inner.ContinueRivalRefresh(minute);
        public IEnumerable<FoodScope> RivalScopes => Inner.RivalScopes;
        public bool IsRivalCandidate(FoodScope scope) => Inner.IsRivalCandidate(scope);
        public bool RivalAtPlace(string place) => Inner.RivalAtPlace(place);
        public FoodAlternativeObservation? ReadRival(string id) => Inner.ReadRival(id);
        public FoodBusiness? ReadBusiness(string id, double minute) => Inner.CaptureBusiness(id, minute);
        public IReadOnlyList<FoodBusiness> ReadLocal(Place origin, double minute, double tolerance, int maximum) => throw new Exception("All-shop read forbidden");
    }

    static ResidentMeal Serving(string place, double finish) => new() { PlaceId = place, ServiceAt = finish, Status = MealStatus.Serving,
        External = "10" + RivalFoodTerms.Fingerprint(RivalOutflowTests.Observation()) };

    internal static (CitySimulator city, RivalOutflowTests.Market market, WeightedFoodChoiceTests.Planner paths) Crowd(
        int residents, RivalFoodSettings settings, int representation = 1, int seed = 79, int atRival = 0, bool player = false)
    {
        var places = new List<Place> { new() { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = residents },
            new() { Id = "s0", Name = "Rival", Kind = PlaceKind.Shop, Position = new Position(10, 0, 0) } };
        if (player) places.Add(new Place { Id = "player", Name = "Player", Kind = PlaceKind.Shop, Position = new Position(10, 0, 0) });
        var identities = Enumerable.Range(0, residents).Select(i => new CitizenIdentity { Id = "r" + i, Name = "Resident " + i, Age = 30, Persona = Persona.Foodie });
        var state = Population.Create(identities, places, "rival-limits", "0.40.4", seed, 1440);
        foreach (var person in state.Citizens) { person.NextEventMinute = person.ActivityUntilMinute = 2870; person.Preferences.WalkingToleranceMetres = 5; }
        // Existing-stop customers: already inside the rival on an ordinary shop stop for the whole day.
        foreach (var person in state.Citizens.Take(atRival)) { person.PlaceId = "s0"; person.Activity = Activity.Shop; person.Phase = CitizenPhase.AtPlace; }
        state.Food = new FoodCommerceState { RepresentationFactor = representation, ChoiceStartsDay = 1 };
        var market = new RivalOutflowTests.Market();
        market.Rivals.Add(Rival, new FoodScope { AlternativeKind = FoodAlternativeKind.Rival, BusinessId = Rival, PlaceId = "s0", ProductId = FoodProducts.Croissant });
        market.Observations.Add(Rival, RivalOutflowTests.Observation(Rival, "s0"));
        if (player)
        {
            state.Food.Shops.Add(new FoodScope { BusinessId = "player", PlaceId = "player", ProductId = FoodProducts.Croissant });
            market.Players = new[] { new FoodBusiness { Id = "player", PlaceId = "player", MealsPerHour = 30,
                Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
                Offers = new() { new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(100), Units = 1000 } } } };
        }
        var paths = new WeightedFoodChoiceTests.Planner();
        var city = new CitySimulator(state, paths);
        city.AttachFoodMarket(player ? new WithPlayer(market) : market, rivalSettings: settings, campaign: new NoCampaign());
        state.Food.ChoiceStartsDay = state.Food.AwarenessStartsDay = state.Food.RivalStartsDay = 1;
        return (city, market, paths);
    }
    static RivalFoodSettings Tight(int lanes = 1, int units = 10000, int service = 30)
        => new() { RivalServiceLanes = lanes, RivalMealPeriodUnits = units, RivalAssumedServiceMinutes = service };
    static RivalFoodSettings Copy(RivalFoodSettings s) => new() { RivalServiceLanes = s.RivalServiceLanes, RivalMealPeriodUnits = s.RivalMealPeriodUnits,
        RivalAssumedServiceMinutes = s.RivalAssumedServiceMinutes, RivalCapacityPercent = s.RivalCapacityPercent };
    static CitySimulator Reload(CitySimulator city, RivalOutflowTests.Market market, WeightedFoodChoiceTests.Planner paths, RivalFoodSettings settings)
    {
        string wire = SaveCodec.Write(city.State); var read = SaveCodec.Read(wire);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var next = new CitySimulator(read.State!, paths);
        next.AttachFoodMarket(next.State.Food!.Shops.Count > 0 ? new WithPlayer(market) : market, rivalSettings: settings, campaign: new NoCampaign());
        Assert.Equal(wire, SaveCodec.Write(next.State));
        Assert.Equal(city.RivalLoadImage(), next.RivalLoadImage());
        if (next.State.Food!.Residents.All(m => m.Decision == null)) next.InspectFoodAwareness("r0");
        return next;
    }
    static long Counter(CitySimulator city, string name)
        => (long)city.DumpRivalFood()!.GetType().GetProperty(name)!.GetValue(city.DumpRivalFood())!;
    static IEnumerable<MealOutcomeRecord> Paid(CitySimulator city) => city.State.Food!.Residents
        .SelectMany(m => m.Today?.Meals ?? new List<MealOutcomeRecord>()).Where(r => r.TargetKind == FoodAlternativeKind.Rival && r.Status == MealOutcomeStatus.Bought);
    /// <summary>Drive one simulated day, checking the derived load against a from-saved-state rebuild after every call.</summary>
    static string Run(CitySimulator city, double until, double step, int budget, Func<CitySimulator, CitySimulator>? after = null)
    {
        double at = city.State.Minute;
        while (at < until)
        {
            at = Math.Min(until, at + step);
            while (true)
            {
                bool caught = city.AdvanceTo(at, budget).CaughtUp;
                Assert.Equal(city.ReconstructedRivalLoadImage(), city.RivalLoadImage());
                if (after != null) city = after(city);
                if (caught) break;
            }
        }
        return SaveCodec.Write(city.State);
    }

    [Fact]
    public void LanesJoinTheNextFreeLaneInFinishOrder()
    {
        var load = new RivalServiceLoad();
        Assert.Equal(100, load.NextFree("p", 100, 2));
        var a = Serving("p", 105); load.Admit(a); Assert.Equal(100, load.NextFree("p", 100, 2));
        var b = Serving("p", 105); load.Admit(b); Assert.Equal(105, load.NextFree("p", 100, 2));
        var c = Serving("p", 110); load.Admit(c); Assert.Equal(105, load.NextFree("p", 101, 2));
        var d = Serving("p", 110); load.Admit(d); Assert.Equal(110, load.NextFree("p", 101, 2));
        Assert.Equal(105, load.NextFree("p", 101, 3)); Assert.Equal(101, load.NextFree("p", 101, 5));
        // Lanes whose last customer finishes by now are free again; other places are independent.
        Assert.Equal(110, load.NextFree("p", 105, 2)); Assert.Equal(106, load.NextFree("p", 106, 3));
        Assert.Equal(100, load.NextFree("other", 100, 1));
        load.Leave(d); Assert.Equal(105, load.NextFree("p", 101, 2));
        load.Leave(a); load.Leave(b); Assert.Equal(101, load.NextFree("p", 101, 2));
        Assert.Equal(110, load.NextFree("p", 101, 1));
    }

    [Fact]
    public void SellOutCountsExactlyNPerMealPeriodAndResetsAtEachPeriodStart()
    {
        Assert.Equal(3 * 5L, RivalServiceCalibration.Period(5 * 1440 + 0));
        Assert.Equal(3 * 5L, RivalServiceCalibration.Period(5 * 1440 + 659.999));
        Assert.Equal(3 * 5L + 1, RivalServiceCalibration.Period(5 * 1440 + 660));
        Assert.Equal(3 * 5L + 1, RivalServiceCalibration.Period(5 * 1440 + 959.5));
        Assert.Equal(3 * 5L + 2, RivalServiceCalibration.Period(5 * 1440 + 960));
        Assert.Equal(3 * 6L, RivalServiceCalibration.Period(6 * 1440));
        var load = new RivalServiceLoad(); double lunch = 1440 + 700; long period = RivalServiceCalibration.Period(lunch);
        for (int i = 0; i < 3; i++) { var m = Serving("p", lunch + i); load.Admit(m); m.ServiceAt = lunch + i; load.Paid(m); }
        var serving = Serving("p", lunch + 10); load.Admit(serving);
        Assert.Equal(4, load.Count("p", period));
        Assert.Equal(0, load.Count("p", period + 1)); Assert.Equal(0, load.Count("q", period));
        load.Leave(serving); Assert.Equal(3, load.Count("p", period));
        // A finish in the next period counts there, not here; a later payment resets the paid slot.
        var dinner = Serving("p", 1440 + 961); load.Admit(dinner);
        Assert.Equal(3, load.Count("p", period)); Assert.Equal(1, load.Count("p", period + 1));
        load.Paid(dinner); Assert.Equal(0, load.Count("p", period)); Assert.Equal(1, load.Count("p", period + 1));
        load.Forget(new MealOutcomeRecord { TargetKind = FoodAlternativeKind.Rival, Status = MealOutcomeStatus.Bought, PlaceId = "p", CheckoutAt = 1440 + 961 });
        Assert.Equal(0, load.Count("p", period + 1));
    }

    [Fact]
    public void SteadyStateLoadOperationsDoNotAllocate()
    {
        var load = new RivalServiceLoad(); var meals = Enumerable.Range(0, 16).Select(i => Serving("p", 2000 + i)).ToArray();
        foreach (var m in meals) load.Admit(m);
        foreach (var m in meals) { load.Leave(m); load.Admit(m); }
        load.Paid(meals[0]); load.Admit(meals[0]);
        long before = GC.GetAllocatedBytesForCurrentThread(); double sink = 0;
        for (int i = 0; i < 1000; i++)
        {
            var m = meals[i % 16];
            sink += load.NextFree("p", 1990, 8) + load.Count("p", RivalServiceCalibration.Period(2000));
            load.Leave(m); load.Admit(m); load.Paid(m); load.Admit(m);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before); Assert.True(sink > 0);
    }

    [Fact]
    public void DefaultsMatchTheModerateCalibrationAndQ2IsOneSetting()
    {
        var settings = new RivalFoodSettings(); settings.Clamp();
        Assert.Equal(RivalServiceCalibration.ModerateCapacityPercent, settings.RivalCapacityPercent);
        int lanes = settings.Lanes("ba:businesstype_fastfoodrestaurant");
        double ceiling = RivalServiceCalibration.CeilingMealsPerHour(lanes, settings.RivalAssumedServiceMinutes);
        double band = RivalServiceCalibration.ReferencePlayerMealsPerHour * RivalServiceCalibration.CalibrationBandPercent / 100.0;
        Assert.InRange(ceiling, RivalServiceCalibration.ReferencePlayerMealsPerHour - band, RivalServiceCalibration.ReferencePlayerMealsPerHour + band);
        Assert.Equal(2, lanes); Assert.Equal(24, ceiling); Assert.Equal(60, settings.MealPeriodUnits(""));
        // Peter's alternative answer is a one-value change.
        var generous = new RivalFoodSettings { RivalCapacityPercent = RivalServiceCalibration.GenerousCapacityPercent }; generous.Clamp();
        Assert.Equal(2 * lanes, generous.Lanes("")); Assert.Equal(120, generous.MealPeriodUnits(""));
        // Per business type profiles override the base estimate and are still scaled by Q2.
        generous.RivalServiceProfiles["ba:businesstype_coffeeshop"] = new RivalServiceProfile { Lanes = 1, MealPeriodUnits = 30 };
        Assert.Equal(2, generous.Lanes("ba:businesstype_coffeeshop")); Assert.Equal(60, generous.MealPeriodUnits("ba:businesstype_coffeeshop"));
        Assert.Equal(4, generous.Lanes("ba:businesstype_fastfoodrestaurant"));
    }

    [Fact]
    public void FlatSettingsAndProfilesAreBoundedWithoutDiscardingTheFile()
    {
        var settings = JsonConvert.DeserializeObject<PrototypeSettings>("{\"RivalCapacityPercent\":9999,\"RivalServiceLanes\":2.5,\"RivalMealPeriodUnits\":\"0\","
            + "\"RivalServiceProfiles\":{\"ba:businesstype_coffeeshop\":{\"Lanes\":99,\"MealPeriodUnits\":\"40\"},\"bad\":7,\" \":{}}}")!;
        var logs = new List<string>(); settings.Rivals.Clamp(logs.Add);
        Assert.Equal(400, settings.RivalCapacityPercent); Assert.Equal(RivalServiceCalibration.DefaultLanes, settings.RivalServiceLanes);
        Assert.Equal(1, settings.RivalMealPeriodUnits);
        var coffee = settings.RivalServiceProfiles["ba:businesstype_coffeeshop"];
        Assert.Equal(8, coffee.Lanes); Assert.Equal(40, coffee.MealPeriodUnits);
        Assert.Equal(RivalServiceCalibration.DefaultLanes, settings.RivalServiceProfiles["bad"].Lanes);
        Assert.False(settings.RivalServiceProfiles.ContainsKey(" "));
        Assert.Equal(8, settings.Rivals.Lanes("ba:businesstype_coffeeshop")); // 8 x 400% still caps at eight lanes
        int logged = logs.Count; settings.Rivals.Clamp(logs.Add); Assert.Equal(logged, logs.Count);
        var round = JsonConvert.DeserializeObject<PrototypeSettings>(JsonConvert.SerializeObject(settings))!; round.Rivals.Clamp();
        Assert.Equal(40, round.RivalServiceProfiles["ba:businesstype_coffeeshop"].MealPeriodUnits);
        var shipped = JsonConvert.DeserializeObject<PrototypeSettings>(File.ReadAllText(Path.Combine(Root(), "config", "persistent-city.json")))!;
        logs.Clear(); shipped.Rivals.Clamp(logs.Add); Assert.Empty(logs);
        Assert.Equal(RivalServiceCalibration.ModerateCapacityPercent, shipped.RivalCapacityPercent);
        Assert.Equal(2, shipped.Rivals.Lanes("")); Assert.Equal(60, shipped.Rivals.MealPeriodUnits(""));
    }

    [Fact]
    public void FullLanesRefuseBeforeAnyDebitAndRecordQueueMemory()
    {
        var settings = Tight(lanes: 1, service: 30);
        var (city, market, _) = Crowd(24, settings, representation: 2); city.State.Capabilities.Active = true;
        Run(city, 2880, 5, 4);
        long lane = Counter(city, "RivalLaneRefusals"), settled = Counter(city, "RivalSettlements");
        Assert.True(lane > 0); Assert.True(settled > 0);
        var food = city.State.Food!;
        // Refused meals never debit: total outflow equals one price per settlement.
        Assert.Equal(Money.FromCents(100 * settled), food.Residents.Aggregate(Money.Zero, (s, m) => s + m.LegacyExternalFoodSpent));
        Assert.Equal(settled, food.Residents.Sum(m => m.Purchases));
        Assert.Contains(food.Residents.SelectMany(m => m.Memory.Entries), e => e.Outcome == ShopExperience.Queue);
        Assert.Equal(0, market.NativeBuys);
        // One lane at 30 minutes: completions at one rival are at least 30 minutes apart.
        var times = Paid(city).Select(r => r.CheckoutAt).OrderBy(t => t).ToArray();
        for (int i = 1; i < times.Length; i++) Assert.True(times[i] - times[i - 1] >= 29, times[i] + " after " + times[i - 1]);
    }

    [Fact]
    public void SellOutAtExactlyNPerPeriodRefusesWithoutDebitAndRecordsStockoutMemory()
    {
        var settings = Tight(lanes: 8, units: 2, service: 1);
        var (city, market, _) = Crowd(40, settings, representation: 2); city.State.Capabilities.Active = true;
        Run(city, 2880, 5, 4);
        var perPeriod = Paid(city).GroupBy(r => RivalServiceCalibration.Period(r.CheckoutAt)).ToArray();
        Assert.NotEmpty(perPeriod);
        Assert.All(perPeriod, g => Assert.InRange(g.Count(), 1, 2));
        Assert.Contains(perPeriod, g => g.Count() == 2);
        long refused = Counter(city, "RivalSellOutRefusals"), excluded = Counter(city, "RivalSellOutExclusions"), settled = Counter(city, "RivalSettlements");
        Assert.True(refused + excluded > 0);
        Assert.Equal(Money.FromCents(100 * settled), city.State.Food!.Residents.Aggregate(Money.Zero, (s, m) => s + m.LegacyExternalFoodSpent));
        if (refused > 0) Assert.Contains(city.State.Food.Residents.SelectMany(m => m.Memory.Entries), e => e.Outcome == ShopExperience.Stockout);
        Assert.Equal(0, market.NativeBuys);
    }

    [Theory] [InlineData(1, 10000)] [InlineData(2, 3)]
    public void ReloadAtEveryBoundaryRebuildsIdenticalLoadAndFuture(int lanes, int units)
    {
        var settings = Tight(lanes, units, service: 20);
        var (straight, _, _) = Crowd(16, Copy(settings), representation: 2);
        string expected = Run(straight, 2880, 5, 4);
        var (city, market, paths) = Crowd(16, Copy(settings), representation: 2);
        int reloads = 0, midService = 0;
        string actual = Run(city, 2880, 5, 4, c => {
            reloads++; if (c.State.Food!.Residents.Any(m => m.Status == MealStatus.Serving)) midService++;
            return Reload(c, market, paths, Copy(settings)); });
        Assert.Equal(expected, actual); Assert.True(reloads > 100); Assert.True(midService > 0);
        Assert.True(Paid(straight).Any());
    }

    [Fact]
    public void ThirtySixtyAndOneTwentyCadenceGiveTheSameCityAndLoad()
    {
        var settings = Tight(lanes: 1, units: 4, service: 15);
        string? first = null;
        foreach (var (step, budget) in new[] { (2.0, 2), (1.0, 4), (0.5, 64) })
        {
            var (city, _, _) = Crowd(16, Copy(settings), representation: 2);
            string wire = Run(city, 2880, step, budget);
            Assert.True(Counter(city, "RivalSettlements") > 0);
            if (first == null) first = wire; else Assert.Equal(first, wire);
        }
    }

    [Fact]
    public void HeldDecisionImageIsNeverRecapturedWhenAReloadChangesTheProfile()
    {
        // The held image keeps its saved terms; nothing about lanes or units is captured into it (writer 25).
        var (city, market, paths) = RivalOutflowTests.Ready();
        city.InspectFoodAwareness("r");
        var meal = city.State.Food!.Residents.Single();
        city.AdvanceTo(meal.DesiredAt, 1);
        Assert.NotNull(meal.Decision); var held = JsonConvert.SerializeObject(meal.Decision);
        // A reload under a much tighter profile must not recapture or reroll the held image.
        var tight = new RivalFoodSettings { RivalMealPeriodUnits = 1, RivalServiceLanes = 1 };
        string wire = SaveCodec.Write(city.State); var read = SaveCodec.Read(wire); Assert.Equal(SaveReadStatus.Loaded, read.Status);
        var resumed = new CitySimulator(read.State!, paths); resumed.AttachFoodMarket(market, rivalSettings: tight);
        Assert.Equal(held, JsonConvert.SerializeObject(resumed.State.Food!.Residents.Single().Decision));
        int reads = market.Reads;
        while (resumed.State.Food!.Residents.Single().Decision != null) resumed.AdvanceTo(meal.DesiredAt, 1);
        var after = resumed.State.Food.Residents.Single();
        Assert.Equal(MealStatus.Travelling, after.Status); Assert.Equal(RivalId(market), after.BusinessId);
        Assert.Equal(5, after.ServiceMinutes);
        Advance(resumed, after.DesiredAt + 30);
        Assert.Equal(1, after.Purchases); Assert.Equal(0, market.NativeBuys);
        Assert.True(market.Reads > reads);
    }
    /// <summary>Known F6d dependency (docs/checks/f6b.md): writer 25 saves no per-commitment lane/unit terms, so a
    /// travelling commitment meets the profile loaded at arrival. Same profile: byte-identical to no reload. Tighter
    /// profile under contention: refused at arrival with no debit and no native write, never served beyond limits.</summary>
    [Fact]
    public void ReloadWithATighterProfileUnderContentionRefusesAtArrivalWithoutDebit()
    {
        var loose = Tight(lanes: 8, units: 100, service: 10);
        var (city, market, paths) = Crowd(24, Copy(loose), representation: 2);
        // Stop at the first boundary where a consumer travels to the rival and another was already paid this period.
        bool found = false; double at = city.State.Minute;
        while (!found && at < 2880)
        {
            at += 1; while (!city.AdvanceTo(at, 4).CaughtUp) { }
            long period = RivalServiceCalibration.Period(at);
            found = city.State.Food!.Residents.Any(m => m.Status == MealStatus.Travelling && m.TargetKind == FoodAlternativeKind.Rival)
                && Paid(city).Any(r => RivalServiceCalibration.Period(r.CheckoutAt) == period);
        }
        Assert.True(found);
        var travellers = city.State.Food!.Residents.Where(m => m.Status == MealStatus.Travelling && m.TargetKind == FoodAlternativeKind.Rival)
            .Select(m => m.ResidentId).ToArray();
        string checkpoint = SaveCodec.Write(city.State);
        // Same profile: exactly the uninterrupted future.
        var same = Reload(city, market, paths, Copy(loose));
        Assert.Equal(Run(city, 2880, 5, 4), Run(same, 2880, 5, 4));
        // Tighter profile: the travellers are refused at arrival, before any debit.
        var read = SaveCodec.Read(checkpoint); Assert.Equal(SaveReadStatus.Loaded, read.Status);
        var tight = new CitySimulator(read.State!, paths);
        tight.AttachFoodMarket(market, rivalSettings: Tight(lanes: 8, units: 1, service: 10), campaign: new NoCampaign());
        var spent = travellers.ToDictionary(id => id, id => tight.State.Food!.Residents.Single(m => m.ResidentId == id).LegacyExternalFoodSpent);
        long period0 = RivalServiceCalibration.Period(at);
        int paidBefore = Paid(tight).Count(r => RivalServiceCalibration.Period(r.CheckoutAt) == period0);
        Run(tight, at + 15, 1, 4);
        Assert.True(Counter(tight, "RivalSellOutRefusals") > 0);
        foreach (var id in travellers)
        {
            var m = tight.State.Food!.Residents.Single(x => x.ResidentId == id);
            if (m.Status != MealStatus.Paid) Assert.Equal(spent[id], m.LegacyExternalFoodSpent);
        }
        // Nothing further sells in the already sold-out period.
        Assert.Equal(paidBefore, Paid(tight).Count(r => RivalServiceCalibration.Period(r.CheckoutAt) == period0));
        Assert.Equal(0, market.NativeBuys);
    }

    /// <summary>Residents already inside a rival on a shop stop decide there and enter Serving without ArriveRival.
    /// Their queued finish must include the lane wait, so concurrent service never exceeds the estimated lanes.</summary>
    [Theory] [InlineData(1)] [InlineData(2)]
    public void ExistingStopCustomersQueueOnEstimatedLanes(int lanes)
    {
        const int service = 30;
        var (city, market, _) = Crowd(16, Tight(lanes, units: 10000, service: service), atRival: 16);
        int maxConcurrent = 0;
        string straight = Run(city, 2880, 1, 4, c => {
            double now = c.State.Minute;
            maxConcurrent = Math.Max(maxConcurrent, c.State.Food!.Residents.Count(m => m.Status == MealStatus.Serving
                && m.TargetKind == FoodAlternativeKind.Rival && m.ServiceAt - service <= now && now < m.ServiceAt));
            return c; });
        long settled = Counter(city, "RivalSettlements");
        Assert.True(settled > lanes, "settled " + settled);
        Assert.InRange(maxConcurrent, 1, lanes);
        var finishes = Paid(city).Select(r => r.CheckoutAt).OrderBy(t => t).ToArray();
        for (int i = lanes; i < finishes.Length; i++) Assert.True(finishes[i] - finishes[i - lanes] >= service - 1, finishes[i] + " vs " + finishes[i - lanes]);
        Assert.Equal(Money.FromCents(100 * settled), city.State.Food!.Residents.Aggregate(Money.Zero, (s, m) => s + m.LegacyExternalFoodSpent));
        Assert.Equal(0, market.NativeBuys);
        // Reload at every boundary, including held stop decisions, reproduces the same city.
        var (again, market2, paths2) = Crowd(16, Tight(lanes, units: 10000, service: service), atRival: 16);
        Assert.Equal(straight, Run(again, 2880, 1, 4, c => Reload(c, market2, paths2, Tight(lanes, units: 10000, service: service))));
    }

    /// <summary>Sell-out applies to the meal period a service finishes in, and never more than N finish in any period.</summary>
    [Fact]
    public void ExistingStopSellOutFollowsTheFinishingPeriod()
    {
        var (city, market, _) = Crowd(16, Tight(lanes: 8, units: 1, service: 30), atRival: 16);
        Run(city, 2880, 1, 4);
        var paid = Paid(city).ToArray();
        Assert.NotEmpty(paid);
        Assert.All(paid.GroupBy(r => RivalServiceCalibration.Period(r.CheckoutAt)), g => Assert.Single(g));
        Assert.True(Counter(city, "RivalSellOutRefusals") + Counter(city, "RivalSellOutExclusions") > 0);
        Assert.Equal(0, market.NativeBuys);
    }

    /// <summary>A rival whose estimated lanes are known busy at decision time is judged by its queue: residents at
    /// home see the existing queue reasons and can choose the free player shop instead.</summary>
    [Fact]
    public void BusyRivalQueueParticipatesInChoiceAgainstAnAvailablePlayerShop()
    {
        var settings = Tight(lanes: 1, units: 10000, service: 30);
        var (city, market, _) = Crowd(24, Copy(settings), atRival: 12, player: true);
        int queueReasons = 0, playerWhileBusy = 0;
        city.FoodDecisionObserved += trace => {
            var rival = trace.Intent.Choices.Where(c => c.BusinessId == Rival).ToArray();
            if (rival.Any(c => c.Reason == "Expected queue exceeds patience" || c.Reason == "Service and return do not fit available time"))
            { queueReasons++; if (trace.Intent.BusinessId == "player") playerWhileBusy++; }
        };
        string straight = Run(city, 2880, 1, 4);
        Assert.True(queueReasons > 0, "queue reasons " + queueReasons);
        Assert.True(playerWhileBusy > 0, "player while busy " + playerWhileBusy);
        Assert.True(market.NativeBuys > 0);
        // The same choices replay after a reload at every boundary (held images keep NextServiceAt == OriginAt).
        var (again, market2, paths2) = Crowd(24, Copy(settings), atRival: 12, player: true);
        Assert.Equal(straight, Run(again, 2880, 1, 4, c => Reload(c, market2, paths2, Copy(settings))));
    }

    /// <summary>Residents already inside a sold-out rival whose stop deadline crosses into the next meal period:
    /// each offer is judged by its actual queued finish, so no commitment is refused after selection, every refusal
    /// forms a D4 Stockout entry for the rival, spending is unchanged and reload replays the same city.</summary>
    [Fact]
    public void CrossingPeriodSellOutIsRefusedBeforeSelectionWithStockoutMemory()
    {
        var settings = Tight(lanes: 8, units: 1, service: 5);
        var (city, market, _) = Crowd(16, Copy(settings), atRival: 16); city.State.Capabilities.Active = true;
        string straight = Run(city, 2880, 1, 4);
        long settled = Counter(city, "RivalSettlements");
        Assert.Equal(0, Counter(city, "RivalSellOutRefusals")); // nothing known sold out is ever committed
        Assert.All(Paid(city).GroupBy(r => RivalServiceCalibration.Period(r.CheckoutAt)), g => Assert.Single(g));
        var stockouts = city.State.Food!.Residents.SelectMany(m => m.Memory.Entries).Where(e => e.Outcome == ShopExperience.Stockout).ToArray();
        Assert.NotEmpty(stockouts);
        Assert.Equal(Money.FromCents(100 * settled), city.State.Food.Residents.Aggregate(Money.Zero, (sum, m) => sum + m.LegacyExternalFoodSpent));
        Assert.Equal(0, market.NativeBuys);
        var (again, market2, paths2) = Crowd(16, Copy(settings), atRival: 16); again.State.Capabilities.Active = true;
        Assert.Equal(straight, Run(again, 2880, 1, 4, c => Reload(c, market2, paths2, Copy(settings))));
    }

    /// <summary>Home residents choosing between a sold-out rival and an available player shop: the rival offer is
    /// refused with the existing stock reason at its queued finish, and the player shop can win instead.</summary>
    [Fact]
    public void SoldOutRivalCannotBeatAnAvailablePlayerShop()
    {
        var settings = Tight(lanes: 8, units: 1, service: 5);
        var (city, market, _) = Crowd(24, Copy(settings), atRival: 12, player: true);
        int soldOutSeen = 0, playerInstead = 0, rivalChosenWhileSoldOut = 0;
        city.FoodDecisionObserved += trace => {
            var rival = trace.Intent.Choices.Where(c => c.BusinessId == Rival).ToArray();
            if (!rival.Any(c => c.Reason == "Out of stock")) return;
            soldOutSeen++;
            if (trace.Intent.BusinessId == "player") playerInstead++;
            if (trace.Intent.BusinessId == Rival) rivalChosenWhileSoldOut++;
        };
        Run(city, 2880, 1, 4);
        Assert.True(soldOutSeen > 0, "sold-out evaluations " + soldOutSeen);
        Assert.True(playerInstead > 0, "player chosen " + playerInstead);
        Assert.Equal(0, rivalChosenWhileSoldOut);
        Assert.All(Paid(city).GroupBy(r => RivalServiceCalibration.Period(r.CheckoutAt)), g => Assert.Single(g));
        Assert.True(market.NativeBuys > 0);
    }

    static string RivalId(RivalOutflowTests.Market market) => market.Rivals.Keys.Single();
    static void Advance(CitySimulator city, double at) { while (!city.AdvanceTo(at, 1).CaughtUp) { } }

    [Fact]
    public void SoldOutRivalIsExcludedAtANewDecisionContextWithoutDebit()
    {
        var (city, market, _) = Crowd(6, Tight(lanes: 8, units: 1, service: 1));
        Run(city, 2880, 5, 4);
        Assert.True(Counter(city, "RivalSellOutExclusions") + Counter(city, "RivalSellOutRefusals") > 0);
        var perPeriod = Paid(city).GroupBy(r => RivalServiceCalibration.Period(r.CheckoutAt));
        Assert.All(perPeriod, g => Assert.Single(g));
        Assert.Equal(0, market.NativeBuys);
    }

    [Theory] [InlineData("owner")] [InlineData("withdraw")]
    public void OwnershipFlipDuringServiceRefusesWithoutDebitAndFreesTheLane(string change)
    {
        var (city, market, _) = RivalOutflowTests.Ready(); city.InspectFoodAwareness("r");
        var meal = city.State.Food!.Residents.Single();
        for (double at = meal.DesiredAt; meal.Status != MealStatus.Serving && at < meal.DesiredAt + 60; at += .25) Advance(city, at);
        Assert.Equal(MealStatus.Serving, meal.Status);
        Assert.Contains("s0|0|", city.RivalLoadImage());
        if (change == "owner") market.Observations[meal.BusinessId] = RivalOutflowTests.Observation(meal.BusinessId, meal.PlaceId, kind: FoodAlternativeKind.Player);
        else market.Observations.Clear();
        Advance(city, meal.ServiceAt + 1);
        Assert.Equal(MealStatus.Missed, meal.Status); Assert.Equal(0, meal.Purchases); Assert.Equal(Money.Zero, meal.LegacyExternalFoodSpent);
        Assert.Equal("", city.RivalLoadImage()); Assert.Equal(city.ReconstructedRivalLoadImage(), city.RivalLoadImage());
        Assert.Equal(0, market.NativeBuys);
    }

    [Fact]
    public void PlayerCheckoutAuthorityAndReceiptsAreUnchangedWhenRivalsSaturate()
    {
        var (city, market, _) = Crowd(24, Tight(lanes: 1, units: 2, service: 30), representation: 2);
        city.State.Places.Add(new Place { Id = "player", Name = "Player", Kind = PlaceKind.Shop, Position = new Position(10, 0, 0) });
        city.State.Food!.Shops.Add(new FoodScope { BusinessId = "player", PlaceId = "player", ProductId = FoodProducts.Croissant });
        market.Players = new[] { new FoodBusiness { Id = "player", PlaceId = "player", MealsPerHour = 30,
            Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
            Offers = new() { new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(100), Units = 1000 } } } };
        var paths = new WeightedFoodChoiceTests.Planner();
        city = new CitySimulator(city.State, paths); city.AttachFoodMarket(new WithPlayer(market), rivalSettings: Tight(lanes: 1, units: 2, service: 30), campaign: new NoCampaign());
        Run(city, 2880, 5, 4);
        var food = city.State.Food!;
        long playerPaid = food.Residents.SelectMany(m => m.Today?.Meals ?? new()).Count(r => r.TargetKind == FoodAlternativeKind.Player && r.Status == MealOutcomeStatus.Bought);
        // Every player sale is exactly one native Buy; rival limits never route a sale through native checkout.
        Assert.Equal(playerPaid, market.NativeBuys); Assert.True(playerPaid > 0);
        Assert.All(food.Residents, m => Assert.NotEqual(MealStatus.Uncertain, m.Status));
        Assert.All(Paid(city).GroupBy(r => RivalServiceCalibration.Period(r.CheckoutAt)), g => Assert.InRange(g.Count(), 1, 2));
    }

    [Fact]
    public void EstimateLabelsArePresentAndNoNativeRivalWritePathExists()
    {
        var (city, _, _) = Crowd(1, new RivalFoodSettings());
        string dump = JsonConvert.SerializeObject(city.DumpRivalFood());
        Assert.Contains("estimated (modeled service limits)", dump); Assert.Contains("modeled sell-out (estimated)", dump);
        Assert.Contains("lanes=2, units/meal period=60, capacity=100%", dump);
        Assert.Contains("modeled service limits", RivalFoodTerms.Capture(RivalOutflowTests.Observation(), 5).ServiceEvidence);
        // Reachability guard: the rival limit code names no native sale, receipt, Runtime or Unity member.
        string root = Root();
        string limits = File.ReadAllText(Path.Combine(root, "Core", "RivalServiceLimits.cs"));
        foreach (var forbidden in new[] { "Buy(", "FoodSaleReceipts", "IFoodSaleEvidence", "IFoodMarket", "UnityEngine", "PersistentCity.Runtime", "Registration", "market." })
            Assert.DoesNotContain(forbidden, limits);
        string rival = File.ReadAllText(Path.Combine(root, "Core", "RivalFood.cs"));
        Assert.DoesNotContain("market.Buy", rival); Assert.DoesNotContain("FoodSaleReceipts", rival);
        string native = File.ReadAllText(Path.Combine(root, "Runtime", "NativeRivalFood.cs"));
        Assert.DoesNotContain("RivalService", native);
    }
    static string Root([CallerFilePath] string path = "") => Path.GetDirectoryName(Path.GetDirectoryName(path)!)!;
}
