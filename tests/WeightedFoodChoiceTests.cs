using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Newtonsoft.Json;
using Xunit;

namespace PersistentCity.Tests;

public sealed class WeightedFoodChoiceTests
{
    internal sealed class Planner : IRoutePlanner, IFoodWalkingPace
    {
        public int Out, Back;
        public bool Refuse { get; set; }
        public bool Throw { get; set; }
        public double WalkingMetresPerMinute => 120;
        public Route? Plan(Place a, Place b, double at, TravelPreferences p)
        {
            if (b.Kind == PlaceKind.Shop) Out++; else Back++;
            if(Throw) throw new InvalidOperationException("Path evidence unavailable");
            if (Refuse || Position.Distance(a.Position,b.Position) > p.WalkingToleranceMetres) return null;
            return new Route { Legs = new() { new RouteLeg { From=a.Position, To=b.Position, Mode=TravelMode.Walk, Minutes=1 } } };
        }
    }
    internal sealed class Market : IFoodMarket
    {
        public double EarliestMinute => 0;
        public FoodBusiness[] Shops = Array.Empty<FoodBusiness>();
        public readonly List<string> Orders = new();
        public bool ThrowAfterSale { get; set; }
        public int Attempts;
        public IReadOnlyList<FoodBusiness> Read(double at) => Shops;
        public FoodSale Buy(ResidentMeal m, double at)
        {
            Attempts++;var offer=Shops.Single(b=>b.Id==m.BusinessId).Offers.Single(o=>o.ProductId==m.ProductId);
            if(offer.Price!=m.Price || offer.Units<=0) return new(false,"Price or stock changed");
            Assert.DoesNotContain(m.IntentId, Orders);Orders.Add(m.IntentId);
            if(ThrowAfterSale) throw new InvalidOperationException("Acknowledgement lost");
            return new(true,"Confirmed");
        }
    }
    internal static (CitySimulator city, Market market, Planner planner) Ready(int seed, int shops=2, bool active=true, int representation=1, bool threeMeals=false)
    {
        var places = new List<Place> {new() {Id="home",Name="Home",Kind=PlaceKind.Home,Capacity=1}};
        places.AddRange(Enumerable.Range(0,shops).Select(i=>new Place {Id="s"+i,Name="Shop "+i,Kind=PlaceKind.Shop,Position=new(10+i*10,0,0)}));
        var state=Population.Create(new[]{new CitizenIdentity {Id="r",Name="Resident",Age=30,Persona=Persona.Foodie}},places,"weighted","0.40.4",seed,1440);
        var person=state.Citizens.Single(); person.NextEventMinute=person.ActivityUntilMinute=2870;
        person.Preferences.WalkingToleranceMetres=5;
        state.Food=new FoodCommerceState {MealRhythmVersion=threeMeals?MealRhythm.PolicyVersion:0,RepresentationFactor=representation,ChoiceStartsDay=active?1:2, Shops=places.Skip(1).Select((p,i)=>new FoodScope {PlaceId=p.Id,BusinessId="b"+i,ProductId=FoodProducts.Croissant}).ToList()};
        var market=new Market {Shops=state.Food.Shops.Select(s=>new FoodBusiness {Id=s.BusinessId,PlaceId=s.PlaceId,Name=s.PlaceId,MealsPerHour=60,
            Hours=Enumerable.Range(0,7).Select(d=>new OpeningWindow {Day=d,FromMinute=0,ToMinute=1440}).ToList(),
            Offers=new(){new FoodOffer {ProductId=FoodProducts.Croissant,Price=Money.FromCents(100),Units=100}}}).ToArray()};
        var planner=new Planner(); var city=new CitySimulator(state,planner);city.AttachFoodMarket(market);
        state.Food.ChoiceStartsDay=active?1:2;
        return (city,market,planner);
    }
    [Fact]
    public void LiveNewPolicyVariesAcrossSeedsAndCanTravelBeyondPersonalTolerance()
    {
        var winners=new HashSet<string>();
        for(int seed=0;seed<48;seed++)
        {
            var (city,_,planner)=Ready(seed);
            var meal=city.State.Food!.Residents.Single();
            while(!city.AdvanceTo(meal.DesiredAt,1).CaughtUp) {}
            Assert.Equal(MealStatus.Travelling,meal.Status);
            winners.Add(meal.BusinessId);
            Assert.InRange(planner.Out,1,8);Assert.InRange(planner.Back,1,8);
        }
        Assert.Equal(2,winners.Count);
    }

    private static CitySimulator Reload(CitySimulator city,Market market,Planner planner)
    {
        var data=new Dictionary<string,string>();
        var session=SaveSession.Open(data,city.State.PopulationKey);
        var write=session.WriteTo(data,city.State);Assert.True(write.Success,write.Error);
        var read=SaveSession.Open(data,city.State.PopulationKey).InitialReadResult;
        Assert.Equal(SaveReadStatus.Loaded,read.Status);
        var restored=new CitySimulator(read.State!,planner);restored.AttachFoodMarket(market);return restored;
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(8)] [InlineData(15)] [InlineData(16)] [InlineData(17)] [InlineData(18)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(9)] [InlineData(10)]
    [InlineData(11)] [InlineData(12)] [InlineData(13)] [InlineData(14)]
    public void RealSessionReloadAtCaptureEachQuoteAndSelectedBoundaryReplaysMoneyAndReason(int boundary)
    {
        var (control,mc,pc)=Ready(79,12);
        var (changed,mm,pm)=Ready(79,12);
        double at=control.State.Food!.Residents.Single().DesiredAt;
        for(int i=0;i<boundary;i++) changed.AdvanceTo(at,1);
        string checkpoint=SaveCodec.Write(changed.State);
        changed=Reload(changed,mm,pm);
        Assert.Equal(checkpoint,SaveCodec.Write(changed.State));
        while(!control.AdvanceTo(at+20,128).CaughtUp) {}
        while(!changed.AdvanceTo(at+20,1).CaughtUp) {}
        Assert.Equal(SaveCodec.Write(control.State),SaveCodec.Write(changed.State));
        Assert.Equal(mc.Orders,mm.Orders);Assert.Single(mm.Orders);
        Assert.Equal(pc.Out,pm.Out);Assert.Equal(pc.Back,pm.Back);
        Assert.Equal(8,pm.Out);Assert.InRange(pm.Back,0,9); // one later return revalidation is separate
    }
    [Fact]
    public void CataloguePermutationAndMoreThanEightShopsDoNotChangeShortlistDrawOrResult()
    {
        var (a,_,pa)=Ready(15,24);var (b,mb,pb)=Ready(15,24);
        b.State.Food!.Shops.Reverse();Array.Reverse(mb.Shops);
        FoodDecisionState? da=null,db=null;
        a.FoodDecisionObserved+=t=>da=t.Decision;b.FoodDecisionObserved+=t=>db=t.Decision;
        double at=a.State.Food!.Residents.Single().DesiredAt;
        while(!a.AdvanceTo(at,128).CaughtUp) {}
        while(!b.AdvanceTo(at,1).CaughtUp) {}
        Assert.Equal(JsonConvert.SerializeObject(da),JsonConvert.SerializeObject(db));
        Assert.Equal(8,da!.Shops.Count);Assert.Equal(8,pa.Out);Assert.Equal(8,pb.Out);
        Assert.Equal(8,pa.Back);Assert.Equal(8,pb.Back);
    }
    [Fact]
    public void RefusedShortlistIsNotRefilledAndInspectorDoesNotRequestRoutes()
    {
        var (city,_,planner)=Ready(15,24);planner.Refuse=true;
        FoodDecisionState? decision=null;city.FoodDecisionObserved+=t=>decision=t.Decision;
        var m=city.State.Food!.Residents.Single();while(!city.AdvanceTo(m.DesiredAt,1).CaughtUp) {}
        Assert.Equal(8,planner.Out);Assert.Equal(0,planner.Back);Assert.Equal(FoodDecisionResult.NoFeasible,decision!.Result);
        for(int i=0;i<10;i++) city.Inspect("r");Assert.Equal(8,planner.Out);
    }
    [Fact]
    public void FartherShopOutsideNearestEightWinsForKnownSeed()
    {
        var winners=new List<(int seed,int shop)>();
        for(int seed=0;seed<50;seed++)
        {
            var (city,_,planner)=Ready(seed,16);var m=city.State.Food!.Residents.Single();
            while(!city.AdvanceTo(m.DesiredAt,128).CaughtUp) {}
            int shop=int.Parse(m.BusinessId.Substring(1));
            if(shop>=8) winners.Add((seed,shop));
            Assert.Equal(8,planner.Out);Assert.Equal(8,planner.Back);
        }
        Assert.NotEmpty(winners);
        Assert.Contains((5,15),winners);Assert.Contains((7,10),winners);Assert.Contains((8,13),winners);
    }
    [Fact]
    public void PurePolicyRepeatsAndTastePriceDistanceHaveMonotonicRelativeEffectsAcrossSeeds()
    {
        var settings=new FoodChoiceSettings();var neutral=new ResidentTraits {Bands="2222222222"};
        EvaluatedFoodOffer Offer(string id,string product,int price,double travel) => new(id,product,Money.FromCents(price),travel,0,travel+15,90,null,null,MealKind.Legacy);
        var baseline=new[]{Offer("b0",FoodProducts.Burger,300,5),Offer("b1",FoodProducts.Croissant,300,5)};
        var cheaper=new[]{Offer("b0",FoodProducts.Burger,100,5),baseline[1]};
        var nearer=new[]{Offer("b0",FoodProducts.Burger,300,1),baseline[1]};
        var taste=new ResidentTraits {Bands="2422222222"};
        int a=0,t=0,p=0,d=0;
        for(int seed=0;seed<4096;seed++)
        {
            var key=new FoodDrawContext(seed,"r",1,MealKind.Lunch,"context",1);
            FoodPolicyResult Select(EvaluatedFoodOffer[] offers,ResidentTraits traits) => FoodChoicePolicy.Select(offers,traits,Money.FromCents(1000),key,settings,false);
            var result=Select(baseline,neutral);Assert.Equal(result.Index,Select(baseline,neutral).Index);
            if(result.Index==0)a++;if(Select(baseline,taste).Index==0)t++;if(Select(cheaper,neutral).Index==0)p++;if(Select(nearer,neutral).Index==0)d++;
            Assert.True(FoodChoicePolicy.Weight(nearer[0],neutral,Money.FromCents(1000),settings)>0);
        }
        Assert.True(t>a);Assert.True(p>a);Assert.True(d>a);
    }

    [Fact]
    public void VersionOneCanonicalKeysHavePinnedIndependentVectors()
    {
        var d=new FoodDrawContext(123,"r|:42",7,MealKind.Lunch,"trip:abc",3);
        Assert.Equal(2746120566u,d.ShopHash("b:9"));
        Assert.Equal(360816019u,d.OfferHash("b:9",FoodProducts.Burger));
        Assert.Equal(3912922371u,d.OfferHash("",""));
        Assert.Equal(-Math.Log((2746120566.0+1)/4294967298.0)/.25,FoodDrawContext.Rank(d.ShopHash("b:9"),.25));
        Assert.NotEqual(d.OfferHash("a|b","c"),d.OfferHash("a","b|c"));
    }

    [Theory]
    [InlineData("price")] [InlineData("unknown")] [InlineData("stock")] [InlineData("closed")] [InlineData("queue")]
    public void HardFactsAndUnknownEvidenceNeverBecomeWeightedOffers(string condition)
    {
        var (city,market,planner)=Ready(15,1);var b=market.Shops[0];var m=city.State.Food!.Residents.Single();
        if(condition=="price") b.Offers[0].Price=Money.FromCents(1000000);
        if(condition=="unknown") b.Offers[0].Units=null;
        if(condition=="stock") b.Offers[0].Units=0;
        if(condition=="closed") b.Hours.Clear();
        if(condition=="queue") city.State.Food.Shops[0].NextServiceAt=m.DesiredAt+100;
        while(!city.AdvanceTo(m.DesiredAt,1).CaughtUp) {}
        Assert.Equal(MealStatus.Missed,m.Status);Assert.Empty(market.Orders);
        Assert.All(city.State.Food.History,h=>Assert.Equal(0,h.ChosenOffers));
        if(condition=="price") {Assert.Equal(0,planner.Out);Assert.Equal(1,city.State.Food.History.Single().PriceRejections);}
        if(condition=="queue") Assert.Equal(MealReasonCode.QueueTooLong,m.Today!.Find(m.Kind)!.Reason);
    }

    [Fact]
    public void FundedHouseholdCompetesButUnfundedHouseholdIsNeverInvented()
    {
        int household=0,bought=0;
        for(int seed=0;seed<64;seed++)
        {
            var (city,_,_)=Ready(seed,1);city.EnableHouseholdFinances();var m=city.State.Food!.Residents.Single();
            m.Budget!.HomeFoodFundedDay=m.Day;
            while(!city.AdvanceTo(m.DesiredAt,1).CaughtUp) {}
            if(m.Status==MealStatus.Declined) {household++;Assert.Equal(FoodOutsideOption.FundedHouseholdFood,m.Today!.Find(m.Kind)!.Outside);}
            else {bought++;Assert.Equal(MealStatus.Travelling,m.Status);}
        }
        Assert.True(household>0);Assert.True(bought>0);
    }

    [Fact]
    public void RepresentedPathUsesIndependentTraitsAndSamePolicyAndReloads()
    {
        var (city,market,planner)=Ready(23,12,representation:3);
        var traces=new List<FoodDecisionTrace>();city.FoodDecisionObserved+=traces.Add;
        double until=city.State.Food!.Residents.Max(m=>m.DesiredAt)+10;
        while(!city.AdvanceTo(until,1).CaughtUp) {}
        Assert.Equal(3,traces.Count);Assert.All(traces,t=>Assert.NotNull(t.Decision));
        Assert.All(traces,t=>Assert.InRange(t.RouteCalls,0,16));Assert.Equal(3,market.Orders.Count);
        string saved=SaveCodec.Write(city.State);city=Reload(city,market,planner);Assert.Equal(saved,SaveCodec.Write(city.State));
        while(!city.AdvanceTo(until+10,1).CaughtUp) {}Assert.Equal(3,market.Orders.Count);
    }

    [Fact]
    public void ExistingShopUsesSamePolicyWithoutRoutesAndRetriesAreBoundedAndLatched()
    {
        var (city,market,planner)=Ready(23,1);var meal=city.State.Food!.Residents[0];var p=city.State.Citizens[0];
        market.Shops[0].Offers[0].Price=Money.FromCents(1000000);
        double at=meal.DesiredAt;
        for(int context=1;context<=5;context++)
        {
            city.State.Minute=at+context;p.PlaceId="s0";p.Activity=Activity.Shop;
            p.RecentVisits.Add(new VisitRecord {PlaceId=p.PlaceId,Activity=p.Activity,AtMinute=city.State.Minute});
            meal.Status=MealStatus.Pending;meal.DueAt=city.State.Minute;
            city=new CitySimulator(city.State,planner);city.AttachFoodMarket(market);
            while(!city.AdvanceTo(city.State.Minute,1).CaughtUp) {}
            Assert.Equal(Math.Min(4,context),meal.ContextAttempts);Assert.Equal(MealStatus.Missed,meal.Status);
            // Same logical stay, even observed again later, consumes neither an ordinal nor another draw.
            meal.Status=MealStatus.Pending;meal.DueAt=city.State.Minute;
            city=new CitySimulator(city.State,planner);city.AttachFoodMarket(market);
            while(!city.AdvanceTo(city.State.Minute,1).CaughtUp) {}
            Assert.Equal(Math.Min(4,context),meal.ContextAttempts);
        }
        Assert.Equal(0,planner.Out+planner.Back);Assert.Empty(market.Orders);
        Assert.Contains("limit",meal.Reason);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(5)] [InlineData(12)] [InlineData(20)]
    public void CommittedOutwardServingPaidAndReturningCheckpointsNeverDrawOrPayAgain(int offset)
    {
        var (a,ma,pa)=Ready(97,12);var (b,mb,pb)=Ready(97,12);
        double at=a.State.Food!.Residents[0].DesiredAt;
        while(!b.AdvanceTo(at+offset,1).CaughtUp) {}
        b=Reload(b,mb,pb);
        while(!b.AdvanceTo(at+30,1).CaughtUp) {}
        while(!a.AdvanceTo(at+30,128).CaughtUp) {}
        Assert.Equal(SaveCodec.Write(a.State),SaveCodec.Write(b.State));Assert.Equal(ma.Orders,mb.Orders);
        Assert.Single(mb.Orders);Assert.Equal(1,mb.Attempts);Assert.Equal(pa.Out,pb.Out);Assert.Equal(pa.Back,pb.Back);
    }
    [Fact]
    public void UncertainCheckoutPersistsWithoutRerollOrSecondAttempt()
    {
        var (city,market,planner)=Ready(12,12);market.ThrowAfterSale=true;
        var m=city.State.Food!.Residents[0];double at=m.DesiredAt;
        while(!city.AdvanceTo(at+10,1).CaughtUp) {}
        Assert.Equal(MealStatus.Uncertain,m.Status);string intent=m.IntentId;Money held=m.DayAllowanceUsed;
        city=Reload(city,market,planner);
        while(!city.AdvanceTo(at+1500,128).CaughtUp) {}
        m=city.State.Food!.Residents[0];Assert.Equal(MealStatus.Uncertain,m.Status);Assert.Equal(intent,m.IntentId);
        Assert.Equal(held,m.DayAllowanceUsed);Assert.Single(market.Orders);Assert.Equal(1,market.Attempts);
    }
    [Fact]
    public void ChangedPriceAfterSelectionRefusesExactlyWithoutReplacementOrMoneyLoss()
    {
        var (city,market,planner)=Ready(12,12);var m=city.State.Food!.Residents[0];double at=m.DesiredAt;
        for(int i=0;i<17;i++) city.AdvanceTo(at,1);
        Assert.Equal(FoodDecisionResult.Selected,m.Decision!.Result);string selected=m.Decision.BusinessId;
        city=Reload(city,market,planner);
        market.Shops.Single(b=>b.Id==selected).Offers[0].Price=Money.FromCents(200);
        while(!city.AdvanceTo(at+20,1).CaughtUp) {}
        m=city.State.Food!.Residents[0];Assert.Equal(selected,m.BusinessId);Assert.Empty(market.Orders);
        Assert.Equal(MealStatus.Missed,m.Status);Assert.Equal(Money.Zero,m.DayAllowanceUsed);Assert.Equal(Money.Zero,m.Spent);
        Assert.Equal(1,market.Attempts);Assert.Equal(1,m.ContextAttempts);
    }
    [Fact]
    public void RouteExceptionsSettleSlotsWithoutRefillReservationOrExtraAttempt()
    {
        var (city,market,planner)=Ready(12,24);planner.Throw=true;var m=city.State.Food!.Residents[0];double at=m.DesiredAt;
        while(!city.AdvanceTo(at,1).CaughtUp) {}
        Assert.Equal(8,planner.Out);Assert.Equal(0,planner.Back);Assert.Equal(1,m.ContextAttempts);
        Assert.Equal(MealStatus.Missed,m.Status);Assert.Empty(market.Orders);Assert.Null(m.Decision);
        Reload(city,market,planner);
    }
    [Fact]
    public void PendingFoodKeepsItsCapturedMinuteIfItsDueTimeIsRetimedAndReloaded()
    {
        var (city,market,planner)=Ready(12,12);var m=city.State.Food!.Residents[0];double at=m.DesiredAt;
        city.AdvanceFrameSlice(at,1);Assert.NotNull(m.Decision);
        m.DueAt=at+5;
        city=Reload(city,market,planner);
        Assert.False(city.IsCaughtUpTo(at));
        while(!city.AdvanceFrameSlice(at,1).CaughtUp) {}
        Assert.Equal(at,city.State.Minute);Assert.Equal(1,m.ContextAttempts);
        while(!city.AdvanceTo(at+20,128).CaughtUp) {}
        Assert.Single(market.Orders);Assert.Equal(8,planner.Out);
    }

    private sealed class ReplacingMarket : IFoodMarket, ILocalFoodMarket
    {
        private readonly Market source;
        private readonly FoodCommerceState scopes;
        private bool replaced;
        public ReplacingMarket(Market source,FoodCommerceState scopes) {this.source=source;this.scopes=scopes;}
        public double EarliestMinute=>0;
        public IReadOnlyList<FoodBusiness> Read(double at)=>throw new Exception("Unexpected whole-market read");
        public IReadOnlyList<FoodBusiness> ReadLocal(Place p,double at,double tolerance,int max)=>throw new Exception("Unexpected legacy read");
        public FoodBusiness? ReadBusiness(string id,double at)
        {
            if(!replaced)
            {
                replaced=true;
                foreach(var s in scopes.Shops) s.BusinessId="replacement:"+s.BusinessId;
                foreach(var b in source.Shops) b.Id="replacement:"+b.Id;
            }
            return source.Shops.SingleOrDefault(b=>b.Id==id);
        }
        public FoodSale Buy(ResidentMeal m,double at)=>throw new Exception("A replacement was never sampled");
    }
    [Fact]
    public void BusinessReplacementDuringCaptureCannotInheritAnotherIdentitysShortlistSlot()
    {
        var (city,market,planner)=Ready(12,12);var m=city.State.Food!.Residents[0];double at=m.DesiredAt;
        var replacement=new ReplacingMarket(market,city.State.Food);
        city=new CitySimulator(city.State,planner);city.AttachFoodMarket(replacement);
        FoodDecisionState? d=null;city.FoodDecisionObserved+=t=>d=t.Decision;
        while(!city.AdvanceTo(at,1).CaughtUp) {}
        Assert.Equal(MealStatus.Missed,m.Status);Assert.Empty(d!.Shops);Assert.Equal(0,planner.Out+planner.Back);
        Assert.Equal(1,m.ContextAttempts);
    }

    [Theory]
    [InlineData(50,2)] [InlineData(100,1)]
    public void ThreeMealsWithoutFinanceRespectRemainingAllowanceAcrossDecisionReload(int laterPrice,int orders)
    {
        var (city,market,planner)=Ready(12,1,threeMeals:true);
        var m=city.State.Food!.Residents[0];m.Allowance=Money.FromCents(1000);
        market.Shops[0].Offers[0].Price=Money.FromCents(950);
        while(!city.AdvanceTo(m.DesiredAt+25,1).CaughtUp) {}
        Assert.Equal(Money.FromCents(950),m.DayAllowanceUsed);Assert.Single(market.Orders);
        double lunch=MealRhythm.DesiredAt(city.State.Citizens[0],city.State.Seed,m.Day,MealKind.Lunch);
        market.Shops[0].Offers[0].Price=Money.FromCents(laterPrice);
        city.AdvanceTo(lunch,1);Assert.NotNull(m.Decision);
        city=Reload(city,market,planner);
        while(!city.AdvanceTo(2860,1).CaughtUp) {}
        m=city.State.Food!.Residents[0];Assert.Equal(orders,market.Orders.Count);
        Assert.Equal(Money.FromCents(1000),m.Allowance);
        Assert.True(m.DayAllowanceUsed<=m.Allowance);Assert.Equal(m.DayAllowanceUsed,m.Spent);
    }
    [Theory]
    [InlineData(false,false,18)] [InlineData(true,false,3)] [InlineData(false,true,10)]
    public void DecisionUnitsSkipNoWorkSlotsAndSelectWithTheLastQuote(bool closed,bool refused,int expectedUnits)
    {
        var (city,market,planner)=Ready(12,12);var m=city.State.Food!.Residents[0];planner.Refuse=refused;
        if(closed) foreach(var b in market.Shops)b.Hours.Clear();
        int units=0;while(!city.IsCaughtUpTo(m.DesiredAt)) {city.AdvanceFrameSlice(m.DesiredAt,1);units++;}
        Assert.Equal(expectedUnits,units);
    }
    [Fact]
    public void ExistingShopNeedsOnlyCaptureSelectAndCommitWithoutRouteUnits()
    {
        var (city,_,planner)=Ready(12,1);var m=city.State.Food!.Residents[0];
        var p=city.State.Citizens[0];p.PlaceId="s0";p.Activity=Activity.Shop;
        int units=0;while(!city.IsCaughtUpTo(m.DesiredAt)) {city.AdvanceFrameSlice(m.DesiredAt,1);units++;}
        Assert.Equal(3,units);Assert.Equal(MealStatus.Serving,m.Status);Assert.Equal(0,planner.Out+planner.Back);
    }
    [Fact]
    public void NonBudgetCheckoutRechecksRemainingAllowanceBeforeNativeEntry()
    {
        var (city,market,_)=Ready(12,1);var m=city.State.Food!.Residents[0];m.Allowance=Money.FromCents(1000);
        double at=m.DesiredAt;
        while(m.Status!=MealStatus.Serving) city.AdvanceTo(at+1,1);
        m.DayAllowanceUsed=Money.FromCents(950);
        while(!city.AdvanceTo(at+20,1).CaughtUp) {}
        Assert.Equal(MealStatus.Missed,m.Status);Assert.Empty(market.Orders);Assert.Equal(0,market.Attempts);
        Assert.Equal(Money.FromCents(950),m.DayAllowanceUsed);
    }

    private sealed class PreparedMarket : IFoodMarket,IFoodCandidateMarket
    {
        private readonly Market source;
        public bool Active;public int Batches,Reads;
        public PreparedMarket(Market source) {this.source=source;}
        private sealed class Batch : IDisposable
        {private readonly PreparedMarket owner;public Batch(PreparedMarket owner) {this.owner=owner;}public void Dispose()=>owner.Active=false;}
        public IDisposable BeginCapture(double minute) {Assert.False(Active);Batches++;Active=true;return new Batch(this);}
        public bool IsCandidate(FoodScope scope) {Assert.True(Active);return scope.BusinessId=="b129";}
        public FoodBusiness? CaptureBusiness(string id,double at) {Assert.True(Active);Reads++;return source.Shops.Single(b=>b.Id==id);}
        public double EarliestMinute=>0;
        public IReadOnlyList<FoodBusiness> Read(double at)=>throw new Exception("Detailed reads must follow preparation and sampling");
        public FoodSale Buy(ResidentMeal m,double at)=>source.Buy(m,at);
    }
    [Fact]
    public void CorePreparesCurrentCandidatesBeforeRankingAndSharesOneBatch()
    {
        var (city,source,planner)=Ready(12,131);var m=city.State.Food!.Residents[0];var market=new PreparedMarket(source);
        city=new CitySimulator(city.State,planner);city.AttachFoodMarket(market);
        while(!city.AdvanceTo(m.DesiredAt,1).CaughtUp) {}
        Assert.Equal("b129",m.BusinessId);Assert.Equal(1,market.Batches);Assert.Equal(1,market.Reads);Assert.False(market.Active);
    }
    [Fact]
    public void InspectorRendersChoiceFactorsWithoutRepeatingChoseFood()
    {
        var (city,_,_)=Ready(12,1);var m=city.State.Food!.Residents[0];
        while(!city.AdvanceTo(m.DesiredAt,1).CaughtUp) {}
        m.Reason="Chose food; likes it; values quality";
        string text=JsonConvert.SerializeObject(city.Inspect("r"));
        Assert.DoesNotContain("chose food",text);Assert.Contains("likes it",text);Assert.Contains("values quality",text);
        Assert.Contains("queue",ResidentInsight.ReasonWords(MealReasonCode.NoLongerFits));
    }
}

