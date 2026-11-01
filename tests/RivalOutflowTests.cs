using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

public sealed class RivalOutflowTests
{
    internal sealed class Market : IFoodMarket, IFoodCandidateMarket, IFoodAwarenessRevision, IRivalFoodMarket, IFoodBusinessAuthority
    {
        sealed class Batch : IDisposable { public void Dispose() { } }
        public Dictionary<string, FoodScope> Rivals = new();
        public Dictionary<string, FoodAlternativeObservation> Observations = new();
        public FoodBusiness[] Players = Array.Empty<FoodBusiness>();
        public int Reads, Predicates, RefreshChecks, RefreshUnits, NativeBuys;
        public long AwarenessRevision { get; set; }
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
            Assert.Equal(FoodAlternativeKind.Player, meal.TargetKind); NativeBuys++;
            return new FoodSale(true, "Confirmed native player purchase");
        }
        public bool NeedsRivalRefresh(double minute) { RefreshChecks++; return false; }
        public void ContinueRivalRefresh(double minute) { RefreshUnits++; }
        public IEnumerable<FoodScope> RivalScopes => Rivals.Values;
        public bool IsRivalCandidate(FoodScope scope) { Predicates++; return Observations.TryGetValue(scope.BusinessId, out var o) && o.Kind == FoodAlternativeKind.Rival; }
        public bool RivalAtPlace(string place) => Rivals.Values.Any(s => s.PlaceId == place); // fixture only; production is indexed
        public FoodAlternativeObservation? ReadRival(string id) { Reads++; return Observations.GetValueOrDefault(id); }
    }
    internal static FoodAlternativeObservation Observation(string id = "rival", string place = "s0", int cents = 100,
        FoodAlternativeKind kind = FoodAlternativeKind.Rival, bool closed = false, int opens = 0)
        => new(id, place, "street", 1, 1, "food", "rival-owner", kind, true, true, closed, null, 0,
            new[] { new FoodAdvertisedOffer(FoodProducts.Croissant, Money.FromCents(cents)) },
            Enumerable.Range(0, 7).Select(d => new FoodAdvertisedHours(d, opens, 1440)));

    internal static (CitySimulator city, Market market, WeightedFoodChoiceTests.Planner paths) Ready(int count = 1, int representation = 1, double weight = 1, bool finances = false)
    {
        var (prepared, _, paths) = WeightedFoodChoiceTests.Ready(79, count, representation: representation);
        var food = prepared.State.Food!; food.Shops.Clear();
        var market = new Market();
        for (int i = 0; i < count; i++)
        {
            string id = "rival" + i;
            market.Rivals.Add(id, new FoodScope { AlternativeKind = FoodAlternativeKind.Rival, BusinessId = id, PlaceId = "s" + i, ProductId = FoodProducts.Croissant });
            market.Observations.Add(id, Observation(id, "s" + i));
            prepared.State.Places[i + 1].Position = new Position(10, 0, 0);
        }
        var city = new CitySimulator(prepared.State, paths);
        city.AttachFoodMarket(market, rivalSettings: new RivalFoodSettings { RivalChoiceWeight = weight },
            campaign: new ThrowCampaign());
        food.ChoiceStartsDay = food.AwarenessStartsDay = food.RivalStartsDay = 1;
        if (finances)
        {
            city.EnableHouseholdFinances();
            city.InspectFoodAwareness("r"); Advance(city, city.State.Minute);
            foreach (var meal in food.Residents) meal.Budget!.HomeFoodFundedDay = -1;
        }
        return (city, market, paths);
    }
    sealed class ThrowCampaign : IFoodAwarenessProvider
    { public string? Reference(string consumer, int day, int slot) => throw new Exception("Campaign provider must stay absent"); }

    static CitySimulator Reload(CitySimulator city, Market market, WeightedFoodChoiceTests.Planner paths, double weight = 1)
    {
        string checkpoint = SaveCodec.Write(city.State); var read = SaveCodec.Read(checkpoint);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var next = new CitySimulator(read.State!, paths); next.AttachFoodMarket(market, rivalSettings: new RivalFoodSettings { RivalChoiceWeight = weight });
        Assert.Equal(checkpoint, SaveCodec.Write(next.State));
        // Transient index construction intentionally restarts on reload. Finish it once before
        // testing a saved semantic/route boundary rather than restarting each private build unit.
        if (next.State.Food!.Residents.All(m => m.Decision == null)) next.InspectFoodAwareness("r");
        return next;
    }
    static void Advance(CitySimulator city, double minute)
    { while (!city.AdvanceTo(minute, 1).CaughtUp) { } }

    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void RealOutingAndRepresentedReturnSettleOnceAcrossEveryReloadBoundary(int representation)
    {
        var (city, market, paths) = Ready(representation: representation);
        city.InspectFoodAwareness("r");
        double at = city.State.Food!.Residents.Max(m => m.DesiredAt);
        // Save at every continuation and timed boundary, with changing live weights after commitment.
        for (int i = 0; i < 400 && city.State.Food!.Residents.Any(m => m.Purchases == 0); i++)
        {
            double next = Math.Max(at, city.State.Minute);
            var result = city.AdvanceTo(next, 1);
            city = Reload(city, market, paths);
            if (result.CaughtUp) at += 1;
        }
        Assert.All(city.State.Food!.Residents, m => {
            Assert.Equal(1, m.Purchases); Assert.Equal(FoodAlternativeKind.Rival, m.TargetKind);
            Assert.Equal(Money.FromCents(100), m.DayAllowanceUsed); Assert.Equal('1', m.External[1]);
            Assert.Equal(FoodAlternativeKind.Rival, m.Today!.Find(m.Kind)!.TargetKind);
            Assert.Empty(m.Memory.Entries); Assert.Null(m.Memory.Watermark);
        });
        var person = city.State.Citizens.Single();
        Assert.NotNull(person.MealOuting);
        city = Reload(city, market, paths, weight: 0);
        Advance(city, city.State.Minute + 20);
        Assert.Null(city.State.Citizens.Single().MealOuting);
        Assert.All(city.State.Food!.Residents, m => { Assert.Null(m.Excursion); Assert.Equal(1, m.Purchases); });
        Assert.Equal(0, market.NativeBuys); Assert.Empty(city.State.Food.History);
    }
    [Theory] [InlineData("withdraw")] [InlineData("price")] [InlineData("hours")] [InlineData("closed")] [InlineData("owner")]
    public void ChangedTermsAtArrivalCancelWithoutDebitOrSubstitution(string change)
    {
        var (city, market, _) = Ready(); var meal = city.State.Food!.Residents.Single();
        Advance(city, meal.DesiredAt); Assert.Equal(MealStatus.Travelling, meal.Status);
        market.Observations[meal.BusinessId] = Observation(meal.BusinessId, meal.PlaceId,
            cents: change == "price" ? 101 : 100, kind: change == "owner" ? FoodAlternativeKind.Player : FoodAlternativeKind.Rival,
            closed: change == "closed", opens: change == "hours" ? 1 : 0);
        if (change == "withdraw") market.Observations.Clear();
        Advance(city, city.State.Minute + 10);
        Assert.Equal(MealStatus.Missed, meal.Status); Assert.Equal(0, meal.Purchases); Assert.Equal(Money.Zero, meal.DayAllowanceUsed);
        Assert.Equal(0, market.NativeBuys); Assert.Empty(meal.Memory.Entries);
    }
    [Theory] [InlineData(1024)] [InlineData(20000)]
    public void ProductionCaptureHasNoAllShopPassAndRetainsEnvelope(int count)
    {
        // Place ceiling includes the home.
        var (city, market, paths) = Ready(count);
        var dump = city.InspectFoodAwareness("r")!; market.Reads = market.Predicates = 0;
        var meal = city.State.Food!.Residents.Single(); Advance(city, meal.DesiredAt);
        Assert.InRange(market.Predicates, 0, 24); Assert.InRange(market.Reads, 1, 10);
        Assert.InRange(paths.Out + paths.Back, 0, 16); Assert.InRange(dump.Shortlist.Length, 1, 8);
        Assert.InRange(dump.Image.Union.Count, 1, 24);
        Assert.All(dump.Samples.Where(s => s.Source == FoodAwarenessSource.Memory), s => Assert.Equal("", s.BusinessId));
        Assert.All(dump.Samples.Where(s => s.Source == FoodAwarenessSource.Campaign), s => Assert.Equal("unknown", s.Result));
        Assert.Empty(city.State.Food.Shops);
    }
    [Fact]
    public void RainAndRivalWeightBothApplyToShortlistAndFinalChoice()
    {
        var traits=new ResidentTraits {Bands="2222222222"};var settings=new FoodChoiceSettings();
        var entries=Enumerable.Range(0,16).Select(i=>new FoodAwarenessCatalogue.Entry("b"+i,"p"+i,
            new Position(i<8 ? 120 : 10800,0,0),i<8 ? FoodAwarenessAlternativeKind.Managed : FoodAwarenessAlternativeKind.External)).ToArray();
        var catalogue=new FoodAwarenessCatalogue(entries);
        var image=new FoodAwarenessImage {CatalogueRevision=catalogue.Revision,Union=entries.Select(e=>new FoodAwarenessCandidate {BusinessId=e.BusinessId,Eligible=true}).ToList()};
        int drySlots=0,rainSlots=0,lightSlots=0,dryWins=0,rainWins=0,lightWins=0;
        EvaluatedFoodOffer[] Offers(double weight)=>new[] {
            new EvaluatedFoodOffer("near",FoodProducts.Croissant,Money.FromCents(100),1,0,1,200,null,null,MealKind.Lunch),
            new EvaluatedFoodOffer("rival",FoodProducts.Croissant,Money.FromCents(100),90,0,1,200,null,null,MealKind.Lunch,0,weight)};
        for(int seed=0;seed<2000;seed++)
        {
            var draw=new FoodDrawContext(seed,"consumer",1,MealKind.Lunch,"context",1);
            int Slots(double weight,double rain)=>catalogue.Rank(image,new Position(0,0,0),traits,settings,draw,120,
                rivalWeight:weight,weatherTravelMultiplier:rain).Count(e=>e.Kind==FoodAwarenessAlternativeKind.External);
            int dry=Slots(1,1),rain=Slots(1,1.3),light=Slots(.5,1.3);
            Assert.True(light<=rain && rain<=dry);Assert.Equal(0,Slots(0,1.3));
            drySlots+=dry;rainSlots+=rain;lightSlots+=light;
            int d=FoodChoicePolicy.Select(Offers(1),traits,Money.FromCents(1000),draw,settings,false).Index;
            int r=FoodChoicePolicy.Select(Offers(1),traits,Money.FromCents(1000),draw,settings,false,1.3).Index;
            int l=FoodChoicePolicy.Select(Offers(.5),traits,Money.FromCents(1000),draw,settings,false,1.3).Index;
            if(d==1) dryWins++;if(r==1) {rainWins++;Assert.Equal(1,d);}if(l==1) {lightWins++;Assert.Equal(1,r);}
            Assert.Equal(0,FoodChoicePolicy.Select(Offers(0),traits,Money.FromCents(1000),draw,settings,false,1.3).Index);
        }
        Assert.InRange(rainSlots,1,drySlots-1);Assert.InRange(lightSlots,1,rainSlots-1);
        Assert.InRange(rainWins,1,dryWins-1);Assert.InRange(lightWins,1,rainWins-1);
    }
    [Fact]
    public void ZeroWeightDoesNoNewRivalWork()
    {
        var (city, market, _) = Ready(weight: 0); var m = city.State.Food!.Residents.Single();
        Advance(city, m.DesiredAt + 10);
        Assert.Equal(0, market.Reads); Assert.Equal(0, market.Predicates); Assert.Equal(0, market.RefreshChecks); Assert.Equal(0, market.RefreshUnits);
        Assert.Equal(0, m.Purchases);
    }
    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void HeldRivalContextRetainsItsWeightAndServiceEstimateWhenReloadDisablesNewChoices(int units)
    {
        var (city, market, paths) = Ready(); city.InspectFoodAwareness("r");
        city.AttachWeather(new FrameWeatherSource(()=>1,()=>true));
        double at=city.State.Food!.Residents.Single().DesiredAt;
        for(int i=0;i<units;i++) city.AdvanceTo(at,1);
        Assert.NotNull(city.State.Food.Residents[0].Decision);
        Assert.Equal(WeatherSample.Rain,city.State.Food.Residents[0].Decision!.Weather);
        Assert.Equal(1.3,city.State.Food.Residents[0].Decision!.WeatherTravelMultiplier);
        var read=SaveCodec.Read(SaveCodec.Write(city.State)); Assert.Equal(SaveReadStatus.Loaded,read.Status);
        city=new CitySimulator(read.State!,paths);
        city.AttachWeather(new FrameWeatherSource(()=>2,()=>throw new Exception("Held decision must not reread weather")),new WeatherSettings {Enabled=false,RainTravelWeightMultiplier=2});
        city.AttachFoodMarket(market,rivalSettings:new RivalFoodSettings {RivalChoiceWeight=0,RivalAssumedServiceMinutes=30});
        Advance(city,at+7);
        var meal=city.State.Food!.Residents.Single();Assert.Equal(1,meal.Purchases);Assert.Equal(5,meal.ServiceMinutes);
        Assert.Equal(Money.FromCents(100),meal.LegacyExternalFoodSpent);Assert.Equal(0,market.NativeBuys);
    }
    [Fact]
    public void EstimatedAvailabilityNeverInventsStockOrPlayerCapacity()
    {
        var b = RivalFoodTerms.Capture(Observation(), 5);
        Assert.Equal(FoodAvailabilityEvidenceKind.EstimatedFromPriceAndHours, b.AvailabilityEvidence);
        Assert.Null(b.MealsPerHour); Assert.Null(b.Offers.Single().Units); Assert.Equal(5, b.ServiceMinutes);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void RivalCaptureAndVisitKeepSavedPlaceHoursUnchanged(bool withdraw)
    {
        var (city, market, _) = Ready();
        var place=city.State.Places.Single(p=>p.Id=="s0");place.OpenMinute=place.CloseMinute=0;place.WeeklyHours.Clear();
        var hours=place.WeeklyHours;string before=JsonConvert.SerializeObject(place);
        var meal=city.State.Food!.Residents.Single();city.InspectFoodAwareness("r");
        for(int units=0;units<512&&meal.Decision==null;units++) city.AdvanceTo(meal.DesiredAt,1);
        Assert.NotNull(meal.Decision);Assert.Same(hours,place.WeeklyHours);Assert.Equal(before,JsonConvert.SerializeObject(place));
        if(withdraw) market.Observations.Clear();
        Advance(city,meal.DesiredAt+7);
        Assert.Equal(withdraw?0:1,meal.Purchases);Assert.Equal(0,market.NativeBuys);
        Advance(city,city.State.Minute+20);Assert.Null(city.State.Citizens.Single().MealOuting);
        Assert.Same(hours,place.WeeklyHours);Assert.Equal(before,JsonConvert.SerializeObject(place));
        Assert.False(DailyRoutine.Open(place,city.State.Minute));
    }
    static ResidentMeal Payable() => new() { External = "10" + RivalFoodTerms.Fingerprint(Observation()), Status = MealStatus.Serving,
        Price = Money.FromCents(100), Allowance = Money.FromCents(1000), Budget = new ResidentBudget {
            OpeningBalance = Money.FromCents(10000), Balance = Money.FromCents(10000), DailyHousingCents = 100,
            DailyEssentialsCents = 100, DailyHomeFoodCents = 100 } };
    [Theory] [InlineData(1f)] [InlineData(7.30f)]
    public void AtomicExternalLedgerPreservesConservationAndCannotReplay(float nativePrice)
    {
        var m = Payable();m.Price=Money.FromNative(nativePrice);var balance=m.Budget!.Balance;
        Assert.True(ExternalFoodSettlement.TryPay(m));
        Assert.True(HouseholdFinance.CashConserved(m.Budget!)); Assert.Equal(m.Price, m.Budget!.ExternalFoodSpent);
        Assert.Equal(balance-m.Price,m.Budget.Balance);
        Assert.Equal(Money.Zero, m.Budget.PreparedFoodSpent); Assert.Equal(Money.Zero, m.Budget.HeldPurchase);
        string before = JsonConvert.SerializeObject(m); Assert.False(ExternalFoodSettlement.TryPay(m)); Assert.Equal(before, JsonConvert.SerializeObject(m));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void HouseholdOutflowDoesNotWriteB1WhenActiveOrInactive(bool memoryActive)
    {
        var (city, market, _) = Ready(finances: true);
        city.State.Capabilities.Active = memoryActive;
        var meal = city.State.Food!.Residents.Single();
        Advance(city, meal.DesiredAt + 7);
        Assert.Equal(1, meal.Purchases); Assert.Equal(Money.FromCents(100), meal.Budget!.ExternalFoodSpent);
        Assert.True(HouseholdFinance.CashConserved(meal.Budget)); Assert.Empty(meal.Memory.Entries); Assert.Null(meal.Memory.Watermark);
        Assert.Equal(0, market.NativeBuys);
        var read = SaveCodec.Read(SaveCodec.Write(city.State)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(meal.Budget.ExternalFoodSpent, read.State!.Food!.Residents[0].Budget!.ExternalFoodSpent);
    }
    [Fact]
    public void RivalAndPlayerCompeteForTheSameShortlistAndEitherCanWin()
    {
        var winners = new HashSet<FoodAlternativeKind>();
        for (int seed = 0; seed < 32; seed++)
        {
            var (city, market, _) = Ready(); city.State.Seed = seed;
            city.State.Places.Add(new Place { Id="player",Name="Player",Kind=PlaceKind.Shop,Position=new(10,0,0) });
            // Reattach after changing the immutable place image.
            city.State.Food!.Shops.Add(new FoodScope {BusinessId="player",PlaceId="player",ProductId=FoodProducts.Croissant});
            market.Players = new[] {new FoodBusiness {Id="player",PlaceId="player",MealsPerHour=12,
                Hours=Enumerable.Range(0,7).Select(d=>new OpeningWindow {Day=d,FromMinute=0,ToMinute=1440}).ToList(),
                Offers=new(){new FoodOffer {ProductId=FoodProducts.Croissant,Price=Money.FromCents(100),Units=100}}}};
            city=new CitySimulator(city.State,new WeightedFoodChoiceTests.Planner()); city.AttachFoodMarket(market);
            var meal=city.State.Food!.Residents.Single(); Advance(city,meal.DesiredAt);
            Assert.Equal(MealStatus.Travelling,meal.Status); winners.Add(meal.TargetKind);
        }
        Assert.Contains(FoodAlternativeKind.Player,winners); Assert.Contains(FoodAlternativeKind.Rival,winners);
    }
    [Fact]
    public void PlayerTransferWithdrawsItsOldAuthorityBeforeAnyCheckout()
    {
        var (prepared, source, paths)=WeightedFoodChoiceTests.Ready(79,1);
        var market=new Market {Players=source.Shops};
        var city=new CitySimulator(prepared.State,paths);city.AttachFoodMarket(market);
        city.State.Food!.AwarenessStartsDay=1;
        var meal=city.State.Food.Residents.Single(); Advance(city,meal.DesiredAt);
        Assert.Equal(MealStatus.Travelling,meal.Status); Assert.Equal(FoodAlternativeKind.Player,meal.TargetKind);
        market.Players=Array.Empty<FoodBusiness>();
        market.Observations[meal.BusinessId]=Observation(meal.BusinessId,meal.PlaceId);
        Advance(city,city.State.Minute+10);
        Assert.Equal(MealStatus.Missed,meal.Status); Assert.Equal(0,market.NativeBuys); Assert.Equal(0,meal.Purchases);
    }
    [Theory] [InlineData("arrears")] [InlineData("hold")] [InlineData("reserve")] [InlineData("account")] [InlineData("spent")] [InlineData("count")]
    public void RefusalsAreAtomicAndProtectFutureEssentialsAndAllCounters(string reason)
    {
        var m = Payable();
        if (reason == "arrears") m.Budget!.Arrears = Money.FromCents(1);
        if (reason == "hold") m.Budget!.HeldPurchase = Money.FromCents(1);
        if (reason == "reserve") m.Budget!.Balance = Money.FromCents(399);
        if (reason == "account") m.Budget!.ExternalFoodSpent = Money.FromUnits(long.MaxValue);
        if (reason == "spent") m.Spent = Money.FromUnits(long.MaxValue);
        if (reason == "count") m.Purchases = long.MaxValue;
        string before = JsonConvert.SerializeObject(m); Assert.False(ExternalFoodSettlement.TryPay(m)); Assert.Equal(before, JsonConvert.SerializeObject(m));
    }
    [Fact]
    public void NativeReceiptsCannotProveAModeledPaymentAndPlayerWireIsUnchanged()
    {
        var m = Payable(); m.External = ""; m.ResidentId = "r"; m.BusinessId = "b"; m.ProductId = FoodProducts.Croissant;
        var receipts = new Dictionary<string, string>(); FoodSaleReceipts.Begin(receipts, m); FoodSaleReceipts.Confirm(receipts, m);
        Assert.True(FoodSaleReceipts.AlreadyPaid(receipts, m));
        string saved = receipts.Single().Value;
        m.External = "10" + RivalFoodTerms.Fingerprint(Observation());
        Assert.False(FoodSaleReceipts.AlreadyPaid(receipts, m)); Assert.Equal(saved, receipts.Single().Value);
        Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.Begin(new Dictionary<string, string>(), m));
    }
    [Theory] [InlineData(19)] [InlineData(20)]
    public void ExactPredecessorMigrationAndForeignOrFutureRefusalsPreserveSource(int schema)
    {
        var (city, _, _) = Ready(); city.State.Schema = schema;
        if(schema==20)
        {
            city.State.WeatherStartsDay=1;
            city.State.Citizens[0].W=new WeatherCommitment(city.State.Minute,WeatherSample.Rain,true);
            city.State.Food!.Residents[0].Memory.Weather=WeatherSample.Rain;
        }
        string weather=JsonConvert.SerializeObject(city.State.Citizens[0].W);
        string source = schema==20 ? PopulationSchema20.Write(city.State) : PopulationSchema19.Write(city.State);
        var store = new Dictionary<string, string> { [SaveCodec.StateKey] = source };
        var read = SaveCodec.Read(source); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(23, read.State!.Schema); Assert.Equal(schema, read.SourceSchema);
        Assert.Equal(schema==20 ? 1 : 2,read.State.WeatherStartsDay);
        Assert.Equal(weather,JsonConvert.SerializeObject(read.State.Citizens[0].W));
        Assert.Equal(schema==20 ? WeatherSample.Rain : WeatherSample.Unknown,read.State.Food!.Residents[0].Memory.Weather);
        Assert.Equal(2, read.State.Food!.RivalStartsDay); Assert.Equal(source, store[SaveCodec.StateKey]);
        string current = SaveCodec.Write(read.State); Assert.Equal(current, SaveCodec.Write(SaveCodec.Read(current).State!));
        foreach (int futureSchema in new[] { 24, 99 })
        { var json = JObject.Parse(source); json["Schema"] = futureSchema; Assert.NotEqual(SaveReadStatus.Loaded, SaveCodec.Read(json.ToString(Formatting.None)).Status); }
        var foreign = JObject.Parse(source); foreign["Food"]!["RivalStartsDay"] = 0;
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(foreign.ToString(Formatting.None)).Status);
        foreign = JObject.Parse(source); ((JArray)foreign["Food"]!["Residents"]![0]!).Add("");
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(foreign.ToString(Formatting.None)).Status);
        Assert.Equal(source, store[SaveCodec.StateKey]);
    }
    [Theory] [InlineData(false,21)] [InlineData(true,21)] [InlineData(false,22)] [InlineData(true,22)]
    public void Schema21And22MigrationPreservesHeldAndSettledRivalWeatherAndMoney(bool paid,int schema)
    {
        var (city, market, paths)=Ready();city.InspectFoodAwareness("r");
        city.AttachWeather(new FrameWeatherSource(()=>1,()=>true));
        double at=city.State.Food!.Residents.Single().DesiredAt;
        if(paid) Advance(city,at+7);else city.AdvanceTo(at,1);
        var meal=city.State.Food.Residents.Single();
        if(paid) Assert.Equal(MealStatus.Paid,meal.Status);else Assert.NotNull(meal.Decision);
        city.State.Schema=schema;
        string source=SaveCodec.WriteForSchema(city.State,schema);
        var read=SaveCodec.Read(source);Assert.True(read.Status==SaveReadStatus.Loaded,read.Error);
        Assert.Equal(schema,read.SourceSchema);Assert.Equal(23,read.State!.Schema);
        Assert.Equal(FoodMenuPolicy.Wire,read.State.MenuPolicy);
        read.State.Schema=schema;Assert.Equal(source,SaveCodec.WriteForSchema(read.State,schema));
        read.State.Schema=23;
        var resumed=new CitySimulator(read.State,paths);
        resumed.AttachWeather(new FrameWeatherSource(()=>2,()=>throw new Exception("Captured input must survive migration")),
            new WeatherSettings {Enabled=false,RainTravelWeightMultiplier=2});
        resumed.AttachFoodMarket(market,rivalSettings:new RivalFoodSettings {RivalChoiceWeight=0,RivalAssumedServiceMinutes=30});
        Advance(resumed,at+7);
        var after=resumed.State.Food!.Residents.Single();
        Assert.Equal(1,after.Purchases);Assert.Equal(Money.FromCents(100),after.LegacyExternalFoodSpent);
        Assert.Equal(0,market.NativeBuys);
    }
    [Fact]
    public void FlatSettingsClampOnceAndRetainExistingNumericConventions()
    {
        var settings = JsonConvert.DeserializeObject<PrototypeSettings>("{\"RivalChoiceWeight\":\"Infinity\",\"RivalAssumedServiceMinutes\":2.5,\"RivalRefreshGameMinutes\":999,\"RivalReadsPerUnit\":0}")!;
        var logs = new List<string>(); settings.Rivals.Clamp(logs.Add);
        Assert.Equal(2, settings.RivalChoiceWeight); Assert.Equal(5, settings.RivalAssumedServiceMinutes);
        Assert.Equal(240, settings.RivalRefreshGameMinutes); Assert.Equal(1, settings.RivalReadsPerUnit); Assert.Equal(4, logs.Count);
        settings.Rivals.Clamp(logs.Add); Assert.Equal(4, logs.Count);
    }
    [Theory]
    [InlineData("kind")] [InlineData("stock")] [InlineData("capacity")] [InlineData("service")]
    [InlineData("terms")] [InlineData("weight")] [InlineData("memory")] [InlineData("queue")]
    [InlineData("source-kind")] [InlineData("legacy-account")] [InlineData("surplus")]
    public void MalformedSchema21CapturesAndAccountsRefuseWithoutReplacingSource(string damage)
    {
        var (city, _, _) = Ready(); city.InspectFoodAwareness("r");
        city.AdvanceTo(city.State.Food!.Residents.Single().DesiredAt, 1);
        var root=JObject.Parse(SaveCodec.Write(city.State));
        var meal=(JArray)root["Food"]!["Residents"]![0]!;
        var decision=meal[9]!;var shop=decision["Shops"]![0]!;
        switch(damage)
        {
            case "kind": shop["AlternativeKind"]=2; break;
            case "stock": shop["Offers"]![0]!["Units"]=1; break;
            case "capacity": shop["MealsPerHour"]=12; break;
            case "service": shop["RivalServiceMinutes"]=0; break;
            case "terms": shop["RivalTerms"]="bad"; break;
            case "weight": shop["AlternativeWeight"]=0; break;
            case "memory": shop["MemoryContribution"]=1; break;
            case "queue": shop["NextServiceAt"]=(double)shop["NextServiceAt"]!+1; break;
            case "source-kind": foreach(var c in decision["Awareness"]!["Union"]!) c["AlternativeKind"]=0; break;
            case "legacy-account": meal[3]=-1; break;
            case "surplus": meal.Add(0); break;
        }
        string source=root.ToString(Formatting.None);
        var data=new Dictionary<string,string>{[SaveCodec.StateKey]=source};
        Assert.Equal(SaveReadStatus.Invalid,SaveCodec.Read(source).Status);
        Assert.False(SaveStore.WriteTo(data,city.State).Success);Assert.Equal(source,data[SaveCodec.StateKey]);
    }
    [Fact]
    public void AllowanceLedgerIsExactAndCannotDisappearWhenEnablingHouseholdFinances()
    {
        var (city, _, _) = Ready(); var meal=city.State.Food!.Residents.Single();
        Advance(city,meal.DesiredAt+7); Assert.Equal(Money.FromCents(100),meal.LegacyExternalFoodSpent);
        Assert.Throws<InvalidOperationException>(()=>city.EnableHouseholdFinances());
        var loaded=SaveCodec.Read(SaveCodec.Write(city.State));Assert.Equal(SaveReadStatus.Loaded,loaded.Status);
        Assert.Equal(meal.LegacyExternalFoodSpent,loaded.State!.Food!.Residents[0].LegacyExternalFoodSpent);
    }
}
